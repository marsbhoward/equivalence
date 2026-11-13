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
        [SerializeField] bool[] _edge;
        [SerializeField] int _cols, _rows;
        [SerializeField] string _shapeName;
        [SerializeField] bool _lake;
        // The TIERS (Hazards.TierLayouts): the grid as rolled, whether its gates are open, and
        // what was built from it - kept serialized to hand back to Arena after a domain reload.
        [SerializeField] string[] _tiers;
        [SerializeField] bool _gatesOpen;
        [SerializeField] sbyte[] _height;
        [SerializeField] Rect[] _tierBarriers, _tierStairs;
        [SerializeField] bool[] _tierSteps;
        [SerializeField] GameObject _tierRoot;
        [SerializeField] Vector2 _cell;
        /// <summary>Textures and sprites made for this room alone (the tiers' faces), freed with it.
        /// Not readonly: a domain reload brings it back null.</summary>
        List<Object> _made = new();

        static RoomWalls _current;

        /// <summary>The shape standing now ("open" for none), "(lake)" when its chasms are water.</summary>
        public static string CurrentName
            => _current == null ? "open" : _current._lake ? $"{_current._shapeName} (lake)" : _current._shapeName;

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
            Arena.SetTiers(null, null, null);
            Enemies.NavField.MarkDirty();
            if (shape == null || parent == null) { Art.Biomes.Ground.Build(); return; }

            var go = new GameObject($"room.{shape.Name}");
            go.transform.SetParent(parent, false);
            var walls = go.AddComponent<RoomWalls>();
            walls.Build(shape);
            _current = walls;
            Debug.Log($"[Room] {shape.Describe}");
            // The ground in the room's own shape, now the shape is known (open edges).
            Art.Biomes.Ground.Build();
        }

        void OnEnable()
        {
            if (_wall == null || _wall.Length == 0) return;
            _current = this;
            Arena.SetWalls(_wall, _chasm, _cols, _rows, _lake, _edge);
            Arena.SetTiers(_height != null && _height.Length > 0 ? _height : null, _tierBarriers, _tierStairs, _tierSteps);
        }

        void OnDestroy()
        {
            if (_made != null) foreach (var o in _made) if (o != null) Destroy(o);
            if (_current != this) return;
            _current = null;
            Arena.SetWalls(null, null, 0, 0);
            Arena.SetTiers(null, null, null);
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
            _lake = shape.Lake;
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

            _edge = OpenEdges(At);
            for (int y = 0; y < _rows; y++)
            for (int x = 0; x < _cols; x++)
            {
                bool edge = _edge[y * _cols + x];
                _wall[y * _cols + x] = Solid(x, y) && !edge;
                _chasm[y * _cols + x] = At(x, y) == '~';
            }
            bool Edge(int x, int y) => x >= 0 && y >= 0 && x < _cols && y < _rows && _edge[y * _cols + x];
            // A permanent wall that still STANDS ('#', not opened into the void).
            bool Stands(int x, int y) => At(x, y) == '#' && !Edge(x, y);

            Vector2 Corner(int x, int y) => new Vector2(x * cell.x, y * cell.y) - half;

            // ---- permanent walls: one collider per horizontal run ----
            Runs(this.gameObject, Stands);

            // ---- the opened edge: a BARRIER, one collider per run, felt by bodies alone ----
            // On its own object, marked, so sight (HazardQuery.Query) passes over it as it passes
            // over the void - an open edge never cuts a line.
            _cell = cell;
            BuildEdgeBarrier();

            void Runs(GameObject owner, System.Func<int, int, bool> take) => ColliderRuns(owner, take);

            // ---- landmarks: one picture over each footprint, its cells walls to every rule ----
            var landmarkCell = new bool[_cols * _rows];
            foreach (var lm in shape.Landmarks)
            {
                int yBottom = _rows - 1 - (lm.Row + lm.H - 1);
                for (int dy = 0; dy < lm.H; dy++)
                for (int dx = 0; dx < lm.W; dx++)
                    landmarkCell[(yBottom + dy) * _cols + lm.Col + dx] = true;
                var foot = Corner(lm.Col, yBottom);
                var go = new GameObject($"landmark.{lm.Kind}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(foot.x + lm.W * cell.x * 0.5f, foot.y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = LandmarkArt.For(lm.Kind, lm.W, lm.H, lm.Flip);
                DepthSorted.Attach(go, 0f, true, sr);
                var shadow = new GameObject("shadow");
                shadow.transform.SetParent(go.transform, false);
                shadow.transform.position = new Vector3(foot.x + lm.W * cell.x * 0.5f, foot.y - cell.y * 0.08f, 0f);
                shadow.transform.localScale = new Vector3(lm.W * cell.x * 0.95f, cell.y * 0.16f, 1f);
                var ss = shadow.AddComponent<SpriteRenderer>();
                ss.sprite = Spr.Square;
                ss.color = new Color(0f, 0f, 0f, 0.32f);
                ss.sortingOrder = SortingOrders.GroundDecal;
            }

            // ---- the tiers (the dais among them): barriers along their edges, but for the stairs ----
            _tiers = shape.Tiers;
            _gatesOpen = false;

            // ---- the stone, one block per cell ----
            for (int y = 0; y < _rows; y++)
            for (int x = 0; x < _cols; x++)
            {
                if (!Stands(x, y) || landmarkCell[y * _cols + x]) continue;
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
            bool Void(int x, int y) => x < 0 || y < 0 || x >= _cols || y >= _rows || At(x, y) == '~' || Edge(x, y);
            // Which void is WATER: the chasms on a lake floor, the opened edge where the place's
            // void is water (Arena.EdgeIsWater).
            // The opened edge cells are the GROUND's to draw (Art.Biomes.Ground): its lip, its
            // cliff or bank, the place's void past it. Only the chasms are drawn here.
            bool Wet(int x, int y) => At(x, y) == '~' && _lake;
            // A LAKE is drawn as one surface per connected body of water instead (LakeSurface):
            // its shore is rounded and its depth runs out from the bank, neither of which a cell
            // drawn alone can know.
            if (_lake) LakeSurface.BuildAll(transform, _cols, _rows, cell, Corner, Wet, Void);
            else DrawChasms();
            if (shape.Name == "lift") LiftDressing();

            // Published HERE, not only in OnEnable: AddComponent runs OnEnable before Build has
            // filled the mask, so the first floor in a shape was placed as if it were open.
            Arena.SetWalls(_wall, _chasm, _cols, _rows, _lake, _edge);
            // After the mask: the tiers ask which cells are floor.
            BuildTiers();
        }

        [SerializeField] GameObject _edgeBarrier, _liftDressing;
        List<GameObject> _chasmCells = new();

        Vector2 CellCorner(int x, int y) => new Vector2(x * _cell.x, y * _cell.y) - Arena.HalfExtents;

        /// <summary>One box collider per horizontal run of cells <paramref name="take"/> accepts.</summary>
        void ColliderRuns(GameObject owner, System.Func<int, int, bool> take)
        {
            for (int y = 0; y < _rows; y++)
            {
                int x = 0;
                while (x < _cols)
                {
                    if (!take(x, y)) { x++; continue; }
                    int start = x;
                    while (x < _cols && take(x, y)) x++;
                    var box = owner.AddComponent<BoxCollider2D>();
                    var lo = CellCorner(start, y);
                    box.size = new Vector2((x - start) * _cell.x, _cell.y);
                    box.offset = lo + box.size * 0.5f;
                }
            }
        }

        /// <summary>The opened edge's BARRIER: one collider per run, felt by bodies alone - on
        /// its own object, marked, so sight (HazardQuery.Query) passes over it as it passes over
        /// the void. Rebuilt when the lift docks and its void becomes ground.</summary>
        void BuildEdgeBarrier()
        {
            if (_edgeBarrier != null) { _edgeBarrier.SetActive(false); Destroy(_edgeBarrier); }
            _edgeBarrier = new GameObject("barrier");
            _edgeBarrier.transform.SetParent(transform, false);
            _edgeBarrier.AddComponent<Barrier>();
            ColliderRuns(_edgeBarrier, (x, y) => _edge[y * _cols + x]);
        }

        /// <summary>The chasms (a drop - a lake is LakeSurface's): one cell of void each, a lip
        /// wherever it meets something standing. Outside the room counts as chasm, so a chasm
        /// against the outer wall shows the wall's own face going down rather than a lip that is
        /// not there. Redrawn whole when a bridge rises across one.</summary>
        void DrawChasms()
        {
            _chasmCells ??= new List<GameObject>();
            foreach (var o in _chasmCells) if (o != null) Destroy(o);
            _chasmCells.Clear();
            bool Void(int x, int y) => x < 0 || y < 0 || x >= _cols || y >= _rows || _chasm[y * _cols + x] || _edge[y * _cols + x];
            for (int y = 0; y < _rows; y++)
            for (int x = 0; x < _cols; x++)
            {
                if (!_chasm[y * _cols + x]) continue;
                var c = CellCorner(x, y);
                var go = new GameObject($"chasm.{x}.{y}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(c.x + _cell.x * 0.5f, c.y, 0f);
                go.transform.localScale = new Vector3(_cell.x, _cell.y, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SkyRuin) ? Art.Biomes.SkyArt.ChasmCell(Neighbours(x, y, Void), x, y)
                                                 : ChasmArt.Cell(Neighbours(x, y, Void), (x * 5 + y * 3) & 3);
                // The pit band: a chasm REPLACES the floor like a pit does, and everything that
                // stands draws over it.
                sr.sortingOrder = SortingOrders.PitBase;
                _chasmCells.Add(go);
            }
        }

        // ------------------------------------------------------------------ platforms

        /// <summary>
        /// THE BRIDGE RISES (the Bridge platform floor's second round): one row of its cells out of
        /// the gap - <paramref name="row"/> counted north first within RoomShape's bridge rows, so
        /// it is raised from the arrival's side. The cells become floor; the chasm is redrawn
        /// round them; a slab is laid over each so the bridge reads as built, not filled.
        /// </summary>
        public static void RaiseBridgeRow(int row)
        {
            var w = _current;
            if (w == null) return;
            int gridRow = RoomShape.BridgeGapRow0 + row, y = w._rows - 1 - gridRow;
            for (int x = RoomShape.BridgeCol0; x < RoomShape.BridgeCol0 + RoomShape.BridgeCols; x++)
            {
                w._chasm[y * w._cols + x] = false;
                var c = w.CellCorner(x, y);
                var go = new GameObject($"bridge.{x}.{y}");
                go.transform.SetParent(w.transform, false);
                go.transform.position = new Vector3(c.x, c.y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = w.BridgeSlab(x == RoomShape.BridgeCol0, x == RoomShape.BridgeCol0 + RoomShape.BridgeCols - 1, row == RoomShape.BridgeGapRows - 1);
                sr.sortingOrder = Tuning.Ground.Order + 1;
                Combat.TexelFx.Puff(c + w._cell * 0.5f, Combat.TexelFx.Stone, 10, 5f, 0.45f, Combat.TexelFx.Ground);
            }
            w.DrawChasms();
            Arena.SetWalls(w._wall, w._chasm, w._cols, w._rows, w._lake, w._edge);
            Enemies.NavField.MarkDirty();
        }

        /// <summary>One cell of the bridge: dressed slabs, a kerb along an outer side, the slab's
        /// own thickness under its south end where it meets the platform.</summary>
        Sprite BridgeSlab(bool west, bool east, bool south)
        {
            const float Ppu = Art.Scenery.Ppu;
            int W = Mathf.RoundToInt(_cell.x * Ppu), H = Mathf.RoundToInt(_cell.y * Ppu);
            var stone = Art.Scenery.WallStone;
            var px = new Color32[W * H];
            for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                Color c = Color.Lerp(stone * 1.55f, stone * 1.3f, j / (float)H);
                if (j % 8 == 0) c = stone * 0.95f;                                  // the slabs' joints
                if ((west && i < 3) || (east && i >= W - 3)) c = stone * (i == 0 || i == W - 1 ? 0.7f : 1.9f);   // the kerbs
                c.a = 1f;
                px[j * W + i] = c;
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply();
            var spr = Sprite.Create(tex, new Rect(0, 0, W, H), Vector2.zero, Ppu, 0, SpriteMeshType.FullRect);
            _made ??= new List<Object>();
            _made.Add(tex); _made.Add(spr);
            _ = south;
            return spr;
        }

        /// <summary>
        /// THE LIFT DOCKS (the Lift platform floor's second round): the void round it is ground
        /// from here - the edge cells close, their barrier comes down, the ground is painted again
        /// in the room's new shape (Art.Biomes.Ground). The lift's dressing stays: it is still the
        /// lift, standing flush in the ledge.
        /// </summary>
        public static void DockLift()
        {
            var w = _current;
            if (w == null) return;
            for (int i = 0; i < w._edge.Length; i++)
                if (w._edge[i]) { w._edge[i] = false; w._wall[i] = false; }
            w.BuildEdgeBarrier();
            Arena.SetWalls(w._wall, w._chasm, w._cols, w._rows, w._lake, w._edge);
            Enemies.NavField.MarkDirty();
            Art.Biomes.Parallax.Rise = 0f;
            Art.Biomes.Ground.Build();
        }

        /// <summary>The lift's frame: a bronze rim round its cells and chains up from its sides into
        /// the dark above - drawn over the ground, under every body.</summary>
        void LiftDressing()
        {
            const float Ppu = Art.Scenery.Ppu;
            var r = RoomShape.LiftRect;
            int W = Mathf.RoundToInt(r.width * Ppu), H = Mathf.RoundToInt(r.height * Ppu);
            var rim = Tuning.Platforms.LiftRim;
            var dark = rim * 0.55f; dark.a = 1f;
            var px = new Color32[W * H];
            for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                int d = Mathf.Min(Mathf.Min(i, W - 1 - i), Mathf.Min(j, H - 1 - j));
                if (d >= 4) continue;
                Color c = d == 3 ? dark : rim * (d == 0 ? 0.8f : 1.15f);
                if (d < 3 && ((i + j) % 14 == 0)) c = Color.Lerp(rim, Color.white, 0.45f);   // rivets
                c.a = 1f;
                px[j * W + i] = c;
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply();
            var spr = Sprite.Create(tex, new Rect(0, 0, W, H), Vector2.zero, Ppu, 0, SpriteMeshType.FullRect);
            _made ??= new List<Object>();
            _made.Add(tex); _made.Add(spr);
            _liftDressing = new GameObject("lift");
            _liftDressing.transform.SetParent(transform, false);
            var frame = new GameObject("lift.frame");
            frame.transform.SetParent(_liftDressing.transform, false);
            frame.transform.position = new Vector3(r.xMin, r.yMin, 0f);
            var sr = frame.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.sortingOrder = Tuning.Ground.Order + 1;

            // Chains: a column of links up from each side's middle and each north corner.
            int ch = Mathf.RoundToInt((Arena.HalfExtents.y + 4f - r.yMin) * Ppu);
            var cpx = new Color32[3 * ch];
            for (int j = 0; j < ch; j++)
            {
                bool link = (j / 3) % 2 == 0;
                for (int i = 0; i < 3; i++)
                {
                    bool on = link ? (i == 1) : (j % 3 != 1 || i != 1);
                    if (!on) continue;
                    var c = (i == 1 ? rim * 0.9f : rim * 0.6f); c.a = 1f;
                    cpx[j * 3 + i] = c;
                }
            }
            var ctex = new Texture2D(3, ch, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            ctex.SetPixels32(cpx);
            ctex.Apply();
            var cspr = Sprite.Create(ctex, new Rect(0, 0, 3, ch), new Vector2(0.5f, 0f), Ppu, 0, SpriteMeshType.FullRect);
            _made.Add(ctex); _made.Add(cspr);
            foreach (var at in new[] { new Vector2(r.xMin - 0.08f, r.center.y), new Vector2(r.xMax + 0.08f, r.center.y),
                                       new Vector2(r.xMin + 0.3f, r.yMax), new Vector2(r.xMax - 0.3f, r.yMax) })
            {
                var c = new GameObject("lift.chain");
                c.transform.SetParent(_liftDressing.transform, false);
                c.transform.position = new Vector3(at.x, at.y, 0f);
                var csr = c.AddComponent<SpriteRenderer>();
                csr.sprite = cspr;
                csr.sortingOrder = Tuning.Ground.Order + 2;
            }
        }

        /// <summary>The floor's GATES open (the Gates platform floor's second round): the sealed
        /// stairs become stairs. Rebuilds the tiers' barriers and faces.</summary>
        public static void OpenGates()
        {
            if (_current == null || _current._tiers == null || _current._gatesOpen) return;
            _current._gatesOpen = true;
            _current.BuildTiers();
            Enemies.NavField.MarkDirty();
        }

        /// <summary>True while the standing room has gates still sealed.</summary>
        public static bool GatesSealed => _current != null && _current._tiers != null && !_current._gatesOpen
                                          && System.Array.Exists(_current._tiers, r => r.IndexOf('g') >= 0);

        /// <summary>
        /// TIERS (Hazards.TierLayouts; the dais is the smallest). Height is drawn, never simulated:
        /// every edge between two heights holds bodies with a BARRIER - the FACE under a raised
        /// edge facing the camera (Tuning.Tiers.Face deep, drawn as coursed stone), a thin one
        /// along a side or a north-facing edge - but for the STAIRS, which climb it. Nothing else
        /// touches a barrier: shots and sight pass over (Barrier), and a leap goes over
        /// (PlayerController.LeapStrike). Rebuilt whole when the gates open.
        /// </summary>
        void BuildTiers()
        {
            if (_tierRoot != null) { _tierRoot.SetActive(false); Destroy(_tierRoot); _tierRoot = null; }
            _height = null; _tierBarriers = _tierStairs = null;
            if (_tiers == null) { Arena.SetTiers(null, null, null); return; }

            TierLayouts.Read(_tiers, _gatesOpen, out var h, out var stair, out var gate);
            _height = h;
            _tierSteps = new bool[h.Length];
            for (int i = 0; i < h.Length; i++) _tierSteps[i] = stair[i] || gate[i];
            var half = Arena.HalfExtents;
            var cell = _cell;
            Vector2 Corner(int x, int y) => new Vector2(x * cell.x, y * cell.y) - half;
            bool Floor(int x, int y) => x >= 0 && y >= 0 && x < _cols && y < _rows
                                     && !_wall[y * _cols + x] && !_chasm[y * _cols + x] && !_edge[y * _cols + x];
            float face = Tuning.Tiers.Face, t = Tuning.Tiers.SideBarrier;

            var bars = new List<Rect>();
            var stairs = new List<Rect>();
            // Sealed gates to mark: per boundary row, runs of gate cells under the tier they climb to.
            var sealRuns = new List<(int y, int x0, int x1)>();
            for (int y = 0; y < _rows; y++)
            for (int x = 0; x < _cols; x++)
            {
                if (!Floor(x, y)) continue;
                int k = y * _cols + x;
                var lo = Corner(x, y);
                if (y + 1 < _rows && Floor(x, y + 1) && h[k + _cols] != h[k])
                {
                    float b = lo.y + cell.y;
                    if (h[k + _cols] > h[k])
                    {
                        var band = new Rect(lo.x, b - face, cell.x, face);
                        if (stair[k]) stairs.Add(band); else bars.Add(band);
                    }
                    else bars.Add(new Rect(lo.x, b, cell.x, t));
                }
                if (x + 1 < _cols && Floor(x + 1, y) && h[k + 1] != h[k])
                {
                    float bx = lo.x + cell.x;
                    bars.Add(h[k] < h[k + 1] ? new Rect(bx - t, lo.y, t, cell.y) : new Rect(bx, lo.y, t, cell.y));
                }
            }
            for (int y = 0; y + 1 < _rows; y++)
            {
                int x = 0;
                while (x < _cols)
                {
                    bool Sealed(int i) => !_gatesOpen && gate[y * _cols + i] && Floor(i, y);
                    if (!Sealed(x)) { x++; continue; }
                    int s0 = x;
                    while (x < _cols && Sealed(x)) x++;
                    sealRuns.Add((y, s0, x));
                }
            }

            _tierBarriers = Merge(bars).ToArray();
            _tierStairs = Merge(stairs).ToArray();

            _tierRoot = new GameObject("tiers");
            _tierRoot.transform.SetParent(transform, false);
            _tierRoot.AddComponent<Barrier>().Tier = true;
            foreach (var r in _tierBarriers)
            {
                var box = _tierRoot.AddComponent<BoxCollider2D>();
                box.size = r.size;
                box.offset = r.center;
            }
            foreach (var (y, x0, x1) in sealRuns) Seal(Corner(x0, y + 1), Corner(x1, y + 1).x - Corner(x0, y + 1).x, face);

            Arena.SetTiers(_height, _tierBarriers, _tierStairs, _tierSteps);
        }

        /// <summary>Touching rects of one height merged along x, then of one width along y -
        /// fewer bodies for physics, fewer bands for placement to test.</summary>
        static List<Rect> Merge(List<Rect> rects)
        {
            const float E = 0.001f;
            rects.Sort((a, b) => a.yMin != b.yMin ? a.yMin.CompareTo(b.yMin) : a.height != b.height ? a.height.CompareTo(b.height) : a.xMin.CompareTo(b.xMin));
            var row = new List<Rect>();
            foreach (var r in rects)
            {
                if (row.Count > 0)
                {
                    var l = row[^1];
                    if (Mathf.Abs(l.yMin - r.yMin) < E && Mathf.Abs(l.height - r.height) < E && r.xMin <= l.xMax + E)
                    { row[^1] = Rect.MinMaxRect(l.xMin, l.yMin, Mathf.Max(l.xMax, r.xMax), l.yMax); continue; }
                }
                row.Add(r);
            }
            row.Sort((a, b) => a.xMin != b.xMin ? a.xMin.CompareTo(b.xMin) : a.width != b.width ? a.width.CompareTo(b.width) : a.yMin.CompareTo(b.yMin));
            var outp = new List<Rect>();
            foreach (var r in row)
            {
                if (outp.Count > 0)
                {
                    var l = outp[^1];
                    if (Mathf.Abs(l.xMin - r.xMin) < E && Mathf.Abs(l.width - r.width) < E && r.yMin <= l.yMax + E)
                    { outp[^1] = Rect.MinMaxRect(l.xMin, l.yMin, l.xMax, Mathf.Max(l.yMax, r.yMax)); continue; }
                }
                outp.Add(r);
            }
            return outp;
        }

        /// <summary>
        /// A SEALED GATE's mark (the Gates platform floor): a bar of the barrier's light across the
        /// top of its stairs and a fainter one below. The stairs and faces themselves are the
        /// ground's to draw (Art.Biomes.Ground), in the place's own stone.
        /// </summary>
        void Seal(Vector2 topLeft, float width, float face)
        {
            const float Ppu = Art.Scenery.Ppu;
            int W = Mathf.Max(1, Mathf.RoundToInt(width * Ppu)), H = Mathf.RoundToInt(face * Ppu);
            var seal = Tuning.Tiers.GateSeal;
            var px = new Color32[W * H];
            for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                float a = j >= H - 3 ? 0.95f : j == H - 5 ? 0.45f : j == H - 4 ? 0.2f : 0f;
                if (a <= 0f) continue;
                var c = seal;
                if (j >= H - 3 && (i % 6 == 0)) c = Color.Lerp(seal, Color.white, 0.5f);
                c.a = a;
                px[j * W + i] = c;
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply();
            var spr = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0f, 1f), Ppu, 0, SpriteMeshType.FullRect);
            _made ??= new List<Object>();
            _made.Add(tex); _made.Add(spr);
            var go = new GameObject("gate.seal");
            go.transform.SetParent(_tierRoot.transform, false);
            go.transform.position = new Vector3(topLeft.x, topLeft.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.sortingOrder = Tuning.Ground.Order + 1;
        }

        /// <summary>
        /// The outline cells opened into the void: every connected run of permanent wall ('#')
        /// that reaches an open side of the grid (west, east, south) - a composed room's outline,
        /// a fixed shape's corner blocks. A wall touching only the north wall, or standing free in
        /// the room, still stands: walls that are there interact. Walkability is unchanged (wall
        /// and void are both not floor), so no shape's validation is undone. Bottom row first.
        /// </summary>
        bool[] OpenEdges(System.Func<int, int, char> at)
        {
            var edge = new bool[_cols * _rows];
            if (!Arena.EdgesOpen) return edge;
            var stack = new Stack<Vector2Int>();
            void Seed(int x, int y)
            {
                if (at(x, y) != '#' || edge[y * _cols + x]) return;
                edge[y * _cols + x] = true;
                stack.Push(new Vector2Int(x, y));
            }
            for (int y = 0; y < _rows; y++) { Seed(0, y); Seed(_cols - 1, y); }
            for (int x = 0; x < _cols; x++) Seed(x, 0);
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var n = p + d;
                    if (n.x < 0 || n.y < 0 || n.x >= _cols || n.y >= _rows) continue;
                    Seed(n.x, n.y);
                }
            }
            return edge;
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
            sr.sprite = WallArt.Block(open, cracked, (x * 7 + y * 13) & 3, x, y);
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

            // ONE collider: melee adds a candidate per collider it overlaps, so a collider per cell
            // would let one swing hit the same wall several times. A rectangle is a box over the
            // run; a ragged run (the "rubble" stamp's Z) is ONE polygon collider with a path per
            // row segment - its bounds would also cover whatever lies in the gaps, open floor
            // included, as an invisible wall.
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var c in cells)
            {
                x0 = Mathf.Min(x0, c.x); y0 = Mathf.Min(y0, c.y);
                x1 = Mathf.Max(x1, c.x); y1 = Mathf.Max(y1, c.y);
                RoomWalls.Block(go.transform, c.x, c.y, cell, corner(c.x, c.y),
                                RoomWalls.Neighbours(c.x, c.y, solid), cracked: true);
            }
            bool rectangle = (x1 - x0 + 1) * (y1 - y0 + 1) == cells.Count;

            // The Health's own position is what melee measures sight to (HazardQuery.Query from the
            // swing's origin) - the run's middle, so a swing at either end still finds it.
            var lo = corner(x0, y0);
            var size = new Vector2((x1 - x0 + 1) * cell.x, (y1 - y0 + 1) * cell.y);
            var centre = lo + size * 0.5f;
            foreach (Transform child in go.transform) child.position -= (Vector3)centre;
            go.transform.position = centre;
            if (rectangle) go.AddComponent<BoxCollider2D>().size = size;
            else
            {
                var poly = go.AddComponent<PolygonCollider2D>();
                var paths = new List<Vector2[]>();
                var has = new HashSet<Vector2Int>(cells);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!has.Contains(new Vector2Int(x, y)) || has.Contains(new Vector2Int(x - 1, y))) continue;
                    int end = x;
                    while (has.Contains(new Vector2Int(end + 1, y))) end++;
                    var a = corner(x, y) - centre;
                    var b = a + new Vector2((end - x + 1) * cell.x, cell.y);
                    paths.Add(new[] { a, new Vector2(b.x, a.y), b, new Vector2(a.x, b.y) });
                }
                poly.pathCount = paths.Count;
                for (int i = 0; i < paths.Count; i++) poly.SetPath(i, paths[i]);
            }

            var health = go.AddComponent<Health>();
            health.Configure(Tuning.Rooms.CrackedHpPerCell * cells.Count);
            health.Immovable = true;
            health.Died += _ => wall.Break();
            health.Damaged += info => { if (!info.Tick && wall != null) Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Chip, wall.transform.position); };
        }

        public void Break()
        {
            if (_broken) return;
            _broken = true;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Rubble, transform.position);
            if (_room != null) _room.Open(_cells);
            foreach (var col in GetComponents<Collider2D>()) col.enabled = false;
            // Each cell's blocks fly, in the place's own material (the user's pick, group 4).
            foreach (Transform child in transform)
                Combat.TexelFx.Rubble(child.position, Combat.TexelFx.RubbleOf(true), 0.9f, 6, 0.7f);
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
    static partial class WallArt
    {
        const int N = 75;
        static readonly Dictionary<int, Sprite> _cache = new();

        // The OUTER walls' dark stone (0.21, 0.17, 0.13), lifted a step on top so the block's
        // face still reads under it: the same material inside the room as round it. A first pass
        // at the brick floor's own mid value read as a raised rug, not a wall.
        static bool SkyRuin => Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SkyRuin);
        static bool SaltFlats => Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SaltFlats);
        // The Sky Ruin builds in pale stone (Art.Biomes.SkyArt); the cracked rule holds there too.
        static Color Top => SaltFlats ? new(0.86f, 0.82f, 0.81f) : SkyRuin ? new(0.70f, 0.67f, 0.61f) : new(0.27f, 0.22f, 0.17f);
        static Color TopLight => SaltFlats ? new(0.93f, 0.90f, 0.89f) : SkyRuin ? new(0.78f, 0.75f, 0.69f) : new(0.31f, 0.26f, 0.20f);
        static Color Seam => SaltFlats ? new(0.66f, 0.58f, 0.61f) : SkyRuin ? new(0.50f, 0.47f, 0.42f) : new(0.20f, 0.16f, 0.12f);
        static Color Rim => SaltFlats ? new(0.99f, 0.98f, 0.97f) : SkyRuin ? new(0.86f, 0.84f, 0.78f) : new(0.44f, 0.37f, 0.29f);
        static Color Face => SaltFlats ? new(0.68f, 0.60f, 0.63f) : SkyRuin ? new(0.50f, 0.48f, 0.43f) : new(0.15f, 0.12f, 0.09f);
        static Color FaceSeam => SaltFlats ? new(0.52f, 0.45f, 0.49f) : SkyRuin ? new(0.36f, 0.34f, 0.30f) : new(0.09f, 0.07f, 0.05f);
        static Color Line => SaltFlats ? new(0.32f, 0.26f, 0.30f) : SkyRuin ? new(0.22f, 0.21f, 0.19f) : new(0.06f, 0.05f, 0.04f);
        // Cracked stone is LIGHTER and warmer - a breakable segment must read as different from
        // across the room, and lighter is also "weaker" at a glance.
        static Color CrackTop => SaltFlats ? new(0.95f, 0.85f, 0.76f) : SkyRuin ? new(0.86f, 0.76f, 0.60f) : new(0.46f, 0.37f, 0.28f);
        static Color CrackTopLight => SaltFlats ? new(0.99f, 0.92f, 0.84f) : SkyRuin ? new(0.92f, 0.83f, 0.68f) : new(0.53f, 0.43f, 0.32f);
        static Color CrackFace => SaltFlats ? new(0.80f, 0.66f, 0.58f) : SkyRuin ? new(0.66f, 0.56f, 0.42f) : new(0.28f, 0.22f, 0.16f);
        static Color Crack => SaltFlats ? new(0.48f, 0.34f, 0.32f) : SkyRuin ? new(0.30f, 0.24f, 0.18f) : new(0.08f, 0.06f, 0.05f);

        public static Sprite Block(int open, bool cracked, int variant, int cellX = 0, int cellY = 0)
        {
            if (Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.Forest)) return Thicket(open, cracked, variant, cellX, cellY);
            int key = open | (cracked ? 16 : 0) | (variant << 5) | (SkyRuin ? 1 << 8 : 0) | (SaltFlats ? 1 << 10 : 0);
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
