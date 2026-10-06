using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// A floor's interior walls, built from its <see cref="RoomShape"/>: the colliders, the stone,
    /// the cracked segments, and the wall mask published to <see cref="Arena"/> - which is what
    /// every placement (spawns, pits, columns, the spire, a Rift, a blink) asks.
    ///
    /// SOLID TO EVERYTHING BUT A LOB. A wall is a static collider, so enemy routing (NavField asks
    /// physics) and sight (HazardQuery.Query raycasts) see it with no code of their own; and unlike
    /// a column it also stops a SHOT - HazardQuery.CheckCrossing asks Arena.WallBetween. A mortar
    /// shell is lobbed and lands where it was aimed, the answer to a player hiding behind one.
    ///
    /// ONE BLOCK PER CELL for the picture, each depth-sorted at its own foot like a column, so a
    /// player can walk the length of a north-south wall without being drawn behind all of it.
    /// Colliders are merged per row-run (fewer bodies for physics to test), cracked segments are
    /// their own objects so a swing finds their Health.
    ///
    /// The mask is kept here SERIALIZED as well as in Arena's static, and re-published in OnEnable:
    /// a domain reload empties the static while these walls stand, and placement would then walk
    /// straight through them.
    /// </summary>
    public class RoomWalls : MonoBehaviour
    {
        [SerializeField] bool[] _wall;
        [SerializeField] bool[] _chasm;
        [SerializeField] int _cols, _rows;
        [SerializeField] string _shapeName;

        static RoomWalls _current;

        /// <summary>The shape standing now ("open" for none).</summary>
        public static string CurrentName => _current != null ? _current._shapeName : "open";

        /// <summary>
        /// Replace the floor's walls with <paramref name="shape"/>'s, or take them down (null).
        /// The old walls are switched off on THIS frame - Destroy is deferred, and the next floor's
        /// placement and nav scan run before the end of it.
        /// </summary>
        public static void Apply(RoomShape shape, Transform parent)
        {
            if (_current != null)
            {
                _current.gameObject.SetActive(false);
                Destroy(_current.gameObject);
                _current = null;
            }
            Arena.SetWalls(null, null, 0, 0);
            Enemies.NavField.MarkDirty();
            if (shape == null || parent == null) return;

            var go = new GameObject($"room.{shape.Name}");
            go.transform.SetParent(parent, false);
            var walls = go.AddComponent<RoomWalls>();
            walls.Build(shape);
            _current = walls;
            Debug.Log($"[Room] {shape.Name}");
        }

        void OnEnable()
        {
            if (_wall == null || _wall.Length == 0) return;
            _current = this;
            Arena.SetWalls(_wall, _chasm, _cols, _rows);
        }

        void OnDestroy()
        {
            if (_current != this) return;
            _current = null;
            Arena.SetWalls(null, null, 0, 0);
            Enemies.NavField.MarkDirty();
        }

        /// <summary>A cracked segment broke: its cells are floor from now on.</summary>
        internal void Open(List<Vector2Int> cells)
        {
            foreach (var c in cells)
            {
                _wall[c.y * _cols + c.x] = false;
                Arena.OpenCell(c.x, c.y);
            }
            Enemies.NavField.MarkDirty();
        }

        // ------------------------------------------------------------------ building

        void Build(RoomShape shape)
        {
            _shapeName = shape.Name;
            _cols = RoomShape.Cols;
            _rows = RoomShape.Rows;
            _wall = new bool[_cols * _rows];
            _chasm = new bool[_cols * _rows];

            var half = Arena.HalfExtents;
            var cell = new Vector2(half.x * 2f / _cols, half.y * 2f / _rows);

            // Grid rows are authored north first; the mask is bottom row first, like world y.
            char At(int x, int y) => x < 0 || y < 0 || x >= _cols || y >= _rows ? '#'
                                   : shape.At(x, _rows - 1 - y);
            // A WALL for the stone's neighbour tests: '#' and '%', and outside the room.
            bool Solid(int x, int y) { char c = At(x, y); return c == '#' || c == '%'; }

            for (int y = 0; y < _rows; y++)
            for (int x = 0; x < _cols; x++)
            {
                _wall[y * _cols + x] = Solid(x, y);
                _chasm[y * _cols + x] = At(x, y) == '~';
            }

            Vector2 Corner(int x, int y) => new Vector2(x * cell.x, y * cell.y) - half;

            // ---- permanent walls: one collider per horizontal run ----
            for (int y = 0; y < _rows; y++)
            {
                int x = 0;
                while (x < _cols)
                {
                    if (At(x, y) != '#') { x++; continue; }
                    int start = x;
                    while (x < _cols && At(x, y) == '#') x++;
                    var box = gameObject.AddComponent<BoxCollider2D>();
                    var lo = Corner(start, y);
                    box.size = new Vector2((x - start) * cell.x, cell.y);
                    box.offset = lo + box.size * 0.5f;
                }
            }

            // ---- the stone, one block per cell ----
            for (int y = 0; y < _rows; y++)
            for (int x = 0; x < _cols; x++)
            {
                char c = At(x, y);
                if (c != '#') continue;
                Block(transform, x, y, cell, Corner(x, y), Neighbours(x, y, Solid), cracked: false);
            }

            // ---- cracked segments: each connected run of '%' is one breakable object ----
            var taken = new bool[_cols * _rows];
            for (int y = 0; y < _rows; y++)
            for (int x = 0; x < _cols; x++)
            {
                if (At(x, y) != '%' || taken[y * _cols + x]) continue;
                var cells = new List<Vector2Int>();
                var stack = new Stack<Vector2Int>();
                stack.Push(new Vector2Int(x, y));
                taken[y * _cols + x] = true;
                while (stack.Count > 0)
                {
                    var p = stack.Pop();
                    cells.Add(p);
                    foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    {
                        var n = p + d;
                        if (At(n.x, n.y) != '%' || taken[n.y * _cols + n.x]) continue;
                        taken[n.y * _cols + n.x] = true;
                        stack.Push(n);
                    }
                }
                CrackedWall.Spawn(this, cells, cell, Corner, Solid);
            }

            // ---- chasms: one cell of void each, a lip wherever it meets something standing ----
            // Outside the room counts as chasm here, so a chasm against the outer wall shows the
            // wall's own face going down rather than a lip that is not there.
            bool Void(int x, int y) => x < 0 || y < 0 || x >= _cols || y >= _rows || At(x, y) == '~';
            for (int y = 0; y < _rows; y++)
            for (int x = 0; x < _cols; x++)
            {
                if (At(x, y) != '~') continue;
                var c = Corner(x, y);
                var go = new GameObject($"chasm.{x}.{y}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(c.x + cell.x * 0.5f, c.y, 0f);
                go.transform.localScale = new Vector3(cell.x, cell.y, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ChasmArt.Cell(Neighbours(x, y, Void), (x * 5 + y * 3) & 3);
                // The pit band: a chasm REPLACES the floor like a pit does, and everything that
                // stands draws over it.
                sr.sortingOrder = SortingOrders.PitBase;
            }

            // Published HERE, not only in OnEnable: AddComponent runs OnEnable before Build has
            // filled the mask, so the first floor in a shape was placed as if it were open.
            Arena.SetWalls(_wall, _chasm, _cols, _rows);
        }

        /// <summary>Which sides of cell (x, y) face open floor: bit 0 north, 1 south, 2 west, 3 east.</summary>
        internal static int Neighbours(int x, int y, System.Func<int, int, bool> solid)
            => (solid(x, y + 1) ? 0 : 1) | (solid(x, y - 1) ? 0 : 2)
             | (solid(x - 1, y) ? 0 : 4) | (solid(x + 1, y) ? 0 : 8);

        /// <summary>One cell's stone block, pivoted at its foot so DepthSorted sorts it there.</summary>
        internal static GameObject Block(Transform parent, int x, int y, Vector2 cell, Vector2 corner,
                                         int open, bool cracked)
        {
            var go = new GameObject($"wall.{x}.{y}");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(corner.x + cell.x * 0.5f, corner.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WallArt.Block(open, cracked, (x * 7 + y * 13) & 3);
            sr.sortingOrder = 0;
            // The sprite is one world unit square; a non-square cell (an arena resized off 24x14)
            // stretches it rather than leaving gaps.
            go.transform.localScale = new Vector3(cell.x, cell.y, 1f);
            DepthSorted.Attach(go, 0f, true, sr);

            // A contact shadow on the floor under every south face - what sets the block ON the
            // ground. On the decal layer, under everything that stands, so a body walking past
            // the wall is never darkened by it.
            if ((open & 2) != 0)
            {
                var shadow = new GameObject("shadow");
                shadow.transform.SetParent(parent, false);
                shadow.transform.position = new Vector3(corner.x + cell.x * 0.5f, corner.y - cell.y * 0.09f, 0f);
                shadow.transform.localScale = new Vector3(cell.x, cell.y * 0.18f, 1f);
                var ss = shadow.AddComponent<SpriteRenderer>();
                ss.sprite = Spr.Square;
                ss.color = new Color(0f, 0f, 0f, 0.32f);
                ss.sortingOrder = SortingOrders.GroundDecal;
            }
            return go;
        }
    }

    /// <summary>
    /// A connected run of '%' cells: one wall that breaks as a whole - the shortcut a shape
    /// offers mid-fight. A Column's rules: Health but never in EnemyRegistry, so only a swing
    /// (and anything else that hits whatever Health it overlaps) can break it, never an auto-aimed
    /// shot. Breaking it opens its cells in the mask and re-routes the pack.
    /// </summary>
    public class CrackedWall : MonoBehaviour
    {
        [SerializeField] RoomWalls _room;
        [SerializeField] List<Vector2Int> _cells = new();
        bool _broken;

        public bool Broken => _broken;

        internal static void Spawn(RoomWalls room, List<Vector2Int> cells, Vector2 cell,
                                   System.Func<int, int, Vector2> corner, System.Func<int, int, bool> solid)
        {
            var go = new GameObject($"wall.cracked.{cells.Count}");
            go.transform.SetParent(room.transform, false);
            var wall = go.AddComponent<CrackedWall>();
            wall._room = room;
            wall._cells = cells;

            // ONE collider over the run's bounds: melee adds a candidate per collider it overlaps,
            // so a collider per cell would let one swing hit the same wall several times. Every
            // authored run is a rectangle; a ragged one warns and still gets its bounds.
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var c in cells)
            {
                x0 = Mathf.Min(x0, c.x); y0 = Mathf.Min(y0, c.y);
                x1 = Mathf.Max(x1, c.x); y1 = Mathf.Max(y1, c.y);
                RoomWalls.Block(go.transform, c.x, c.y, cell, corner(c.x, c.y),
                                RoomWalls.Neighbours(c.x, c.y, solid), cracked: true);
            }
            if ((x1 - x0 + 1) * (y1 - y0 + 1) != cells.Count)
                Debug.LogWarning($"[RoomWalls] a cracked run of {cells.Count} cells is not a rectangle");

            // The Health's own position is what melee measures sight to (HazardQuery.Query from the
            // swing's origin) - the run's middle, so a swing at either end still finds it.
            var lo = corner(x0, y0);
            var size = new Vector2((x1 - x0 + 1) * cell.x, (y1 - y0 + 1) * cell.y);
            var centre = lo + size * 0.5f;
            foreach (Transform child in go.transform) child.position -= (Vector3)centre;
            go.transform.position = centre;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = size;

            var health = go.AddComponent<Health>();
            health.Configure(Tuning.Rooms.CrackedHpPerCell * cells.Count);
            health.Immovable = true;
            health.Died += _ => wall.Break();
        }

        public void Break()
        {
            if (_broken) return;
            _broken = true;
            if (_room != null) _room.Open(_cells);
            foreach (var box in GetComponents<BoxCollider2D>()) box.enabled = false;
            foreach (Transform child in transform)
                Spr.Flash(child.position + Vector3.up * 0.5f, 0.9f, new Color(0.75f, 0.66f, 0.55f), 0.3f);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// The procedural stone for interior walls - a warm masonry block to sit between the brick
    /// floor and the outer walls' dark stone. Point-filtered at LayoutUnit (75 texels a cell), one
    /// sprite per (open sides, cracked, variant), cached.
    ///
    /// Seen from the 3/4 camera a block shows its TOP (flagstones, lit along the north edge where
    /// it meets open floor) and, where the cell to the south is open, its FRONT FACE (coursed
    /// brick in shade, Tuning.Rooms.FaceFraction of the cell) - which is what reads as height.
    /// Sides facing open floor get an outline; sides against more wall get none, so a run of
    /// blocks reads as one wall rather than a row of tiles.
    /// </summary>
    static class WallArt
    {
        const int N = 75;
        static readonly Dictionary<int, Sprite> _cache = new();

        // The OUTER walls' dark stone (0.21, 0.17, 0.13), lifted a step on top so the block's
        // face still reads under it: the same material inside the room as round it. A first pass
        // at the brick floor's own mid value read as a raised rug, not a wall.
        static readonly Color Top = new(0.27f, 0.22f, 0.17f), TopLight = new(0.31f, 0.26f, 0.20f),
                              Seam = new(0.20f, 0.16f, 0.12f), Rim = new(0.44f, 0.37f, 0.29f),
                              Face = new(0.15f, 0.12f, 0.09f), FaceSeam = new(0.09f, 0.07f, 0.05f),
                              Line = new(0.06f, 0.05f, 0.04f);
        // Cracked stone is LIGHTER and warmer - a breakable segment must read as different from
        // across the room, and lighter is also "weaker" at a glance.
        static readonly Color CrackTop = new(0.46f, 0.37f, 0.28f), CrackTopLight = new(0.53f, 0.43f, 0.32f),
                              CrackFace = new(0.28f, 0.22f, 0.16f), Crack = new(0.08f, 0.06f, 0.05f);

        public static Sprite Block(int open, bool cracked, int variant)
        {
            int key = open | (cracked ? 16 : 0) | (variant << 5);
            if (_cache.TryGetValue(key, out var s) && s != null) return s;

            bool north = (open & 1) != 0, south = (open & 2) != 0, west = (open & 4) != 0, east = (open & 8) != 0;
            int face = south ? Mathf.RoundToInt(N * Tuning.Rooms.FaceFraction) : 0;
            var top = cracked ? CrackTop : Top;
            var topLight = cracked ? CrackTopLight : TopLight;
            var faceCol = cracked ? CrackFace : Face;

            var px = new Color[N * N];
            var rng = new System.Random(key * 7919 + 17);
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Color c;
                if (y < face)
                {
                    // Coursed brick: three courses, bricks staggered a half apart per course.
                    int course = y * 3 / Mathf.Max(face, 1);
                    int cy = y - course * face / 3;
                    int bx = (x + (course % 2) * 12 + variant * 5) % 25;
                    c = cy == 0 || bx == 0 ? FaceSeam : faceCol;
                    // Lighter toward the top of the face - the lip catches the light.
                    if (y == face - 1) c = Color.Lerp(faceCol, top, 0.5f);
                }
                else
                {
                    // Flagstones: three rows of slabs, the joints staggered by row and variant.
                    int ty = y - face;
                    int rows = 3, h = Mathf.Max(1, (N - face) / rows);
                    int row = Mathf.Min(rows - 1, ty / h);
                    int slab = (x + row * 11 + variant * 9) % 25;
                    bool seam = ty % h == 0 && ty > 0 || slab == 0;
                    c = seam ? Seam : (slab < 3 || ty % h == 1 ? topLight : top);
                    if (north && y >= N - 3) c = Rim;
                }
                px[y * N + x] = c;
            }

            if (cracked)
            {
                // Two fissures wandering across the top, one down the face.
                for (int k = 0; k < 3; k++)
                {
                    int x = rng.Next(10, N - 10), y = k < 2 ? rng.Next(face + 6, N - 6) : Mathf.Max(1, face - 2);
                    int len = k < 2 ? rng.Next(18, 32) : face;
                    for (int i = 0; i < len; i++)
                    {
                        if (x < 1 || x >= N - 1 || y < 1 || y >= N - 1) break;
                        px[y * N + x] = Crack;
                        if (rng.Next(3) == 0) px[y * N + x + 1] = Crack;
                        x += rng.Next(-1, 2);
                        y += k < 2 ? (rng.Next(2) == 0 ? 1 : -1) : -1;
                    }
                }
            }

            // Outline on every side that faces open floor.
            for (int i = 0; i < N; i++)
            {
                if (west) { px[i * N] = Line; px[i * N + 1] = Line; }
                if (east) { px[i * N + N - 1] = Line; px[i * N + N - 2] = Line; }
                if (south) { px[i] = Line; px[N + i] = Line; }
                if (north) { px[(N - 1) * N + i] = Line; }
            }

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0f), N);
            s.hideFlags = HideFlags.HideAndDontSave;
            _cache[key] = s;
            return s;
        }
    }

    /// <summary>
    /// The void of a chasm cell: near-black going down, and where the cell to the NORTH stands
    /// (floor or wall) the far side of the drop - a rock face lit at its top, falling away into
    /// the dark - which is what says "a hole" rather than "a black tile". A pale lip on every
    /// other side that meets standing ground. Point-filtered at 75 texels a cell, cached.
    /// </summary>
    static class ChasmArt
    {
        const int N = 75;
        static readonly System.Collections.Generic.Dictionary<int, Sprite> _cache = new();

        static readonly Color Deep = new(0.025f, 0.022f, 0.03f), Speck = new(0.06f, 0.05f, 0.065f),
                              RockTop = new(0.34f, 0.28f, 0.22f), RockLow = new(0.07f, 0.06f, 0.06f),
                              Strata = new(0.18f, 0.14f, 0.11f), Lip = new(0.52f, 0.44f, 0.35f),
                              LipShade = new(0.20f, 0.16f, 0.13f);

        /// <param name="open">Neighbours' bits as RoomWalls.Neighbours gives them for "is void":
        /// a SET bit is a side that meets standing ground.</param>
        public static Sprite Cell(int open, int variant)
        {
            int key = open | (variant << 4);
            if (_cache.TryGetValue(key, out var s) && s != null) return s;

            bool north = (open & 1) != 0, south = (open & 2) != 0, west = (open & 4) != 0, east = (open & 8) != 0;
            int face = north ? Mathf.RoundToInt(N * 0.42f) : 0;
            var rng = new System.Random(key * 104729 + 3);
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Color c = rng.Next(23) == 0 ? Speck : Deep;
                int fromTop = N - 1 - y;
                if (fromTop < face)
                {
                    // The far wall of the drop: lit where it leaves the floor, gone by the bottom.
                    float k = fromTop / (float)face;
                    c = Color.Lerp(RockTop, RockLow, Mathf.Pow(k, 0.7f));
                    if ((fromTop + (x / 9 + variant) % 3) % 7 == 0) c = Color.Lerp(c, Strata, 0.6f);
                    if (k > 0.85f) c = Color.Lerp(c, Deep, (k - 0.85f) / 0.15f);
                }
                px[y * N + x] = c;
            }
            for (int i = 0; i < N; i++)
            {
                if (north) { px[(N - 1) * N + i] = Lip; px[(N - 2) * N + i] = Lip; }
                if (south) { px[i] = Lip; px[N + i] = LipShade; }
                if (west) { px[i * N] = Lip; px[i * N + 1] = LipShade; }
                if (east) { px[i * N + N - 1] = Lip; px[i * N + N - 2] = LipShade; }
            }

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0f), N);
            s.hideFlags = HideFlags.HideAndDontSave;
            _cache[key] = s;
            return s;
        }
    }
}
