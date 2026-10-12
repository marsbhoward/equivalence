using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Puzzles
{
    /// <summary>
    /// THE LABYRINTH. The whole floor is a maze: arrive at the bottom middle, reach the sigil door
    /// at the top middle before the clock runs out. Run out and the walls come down - the door is
    /// sealed, and the side door to the adjacent room opens in the rubble.
    ///
    /// SEEN CLOSE. While it stands the camera is held at MENU density (GameBootstrap's close view,
    /// the armoury's zoom) and the character is drawn in its menu art - so only a few corridors
    /// show at once and the maze cannot be read off the screen. That is the whole difficulty: the
    /// generator alone makes a maze anyone could trace at the arena's own zoom.
    ///
    /// ITS OWN WALL MASK, finer than a room shape's: Tuning.Puzzle.MazeGridCols x MazeGridRows
    /// cells over the same 24x14 arena (0.4 units), so a corridor is three cells (1.2 - the
    /// player is 0.84 across) and a wall one. Published to <see cref="Arena"/> like RoomWalls'
    /// mask, so a blink or a lunge asks the same questions here as in any shaped room; kept
    /// SERIALIZED and re-published in OnEnable for the domain reload (RoomWalls' reason).
    ///
    /// A PERFECT MAZE (recursive backtracker), carved from the GOAL: its first step is forced
    /// south and nothing comes back to it, so the door stands at the end of a corridor that only
    /// reaches it from below - never on top of a wall. Several mazes are drawn from the seed and
    /// the first whose shortest path falls in MazePathMin..Max is kept, since the backtracker's
    /// own spread (10 to 100+ cells between these two points) is most of a minute either way.
    ///
    /// THE CLOCK is Tuning.Puzzle.MazeSeconds on every floor, counted down on the HUD. Scaled
    /// time, so a menu over it stops it.
    ///
    /// WALLS DRAW UNDER EVERYTHING THAT STANDS (a fixed order just over the pit band). A body is
    /// pivoted at its feet and drawn upward, and the walls are low - a body north of a wall never
    /// overlaps it, and a body south of one is in FRONT of it - so the depth band has nothing to
    /// decide, and the whole maze can be one texture cut into pieces for the collapse.
    /// </summary>
    public class MazePuzzle : PuzzleRoom
    {
        [SerializeField] bool[] _wall;
        [SerializeField] int _cols, _rows;
        [SerializeField] float _left;
        [SerializeField] int _pathCells;
        [SerializeField] Rect _goal;

        // The picture: one texture, cut into a sprite per wall piece (an edge between two cells,
        // or a post), so each piece can come down on its own. UnityEngine.Objects - they survive a
        // reload. The lists are plain and non-readonly (see Domain reload traps).
        [SerializeField] Texture2D _tex;
        [SerializeField] SpriteRenderer _shadow;
        [SerializeField] List<SpriteRenderer> _pieces = new();
        [SerializeField] List<RectInt> _pieceTexels = new();
        [SerializeField] List<float> _pieceDelay = new();
        [SerializeField] List<int> _pieceCut = new();
        [SerializeField] List<Vector3> _pieceRest = new();
        [SerializeField] float _collapseT = -1f, _collapseLast;

        static MazePuzzle _owner;

        static readonly Color Top = new(0.27f, 0.22f, 0.17f), TopLight = new(0.31f, 0.26f, 0.20f),
                              Seam = new(0.20f, 0.16f, 0.12f), Rim = new(0.44f, 0.37f, 0.29f),
                              Face = new(0.15f, 0.12f, 0.09f), FaceSeam = new(0.09f, 0.07f, 0.05f),
                              Line = new(0.06f, 0.05f, 0.04f), Dust = new(0.62f, 0.55f, 0.46f);

        public override string Title => "THE LABYRINTH";

        public override string Intro => "find the sigil door before the walls come down";

        /// <summary>The camera stays close while the maze stands; solving or failing it lets the
        /// view back out over the whole floor.</summary>
        public override bool CloseView => State == PuzzleState.Active;

        /// <summary>The side door is in the maze's own space - it opens only in the rubble.</summary>
        public override bool SideDoorFromStart => false;

        public override float Countdown => State == PuzzleState.Active ? Mathf.Max(0f, _left) : -1f;

        public override string Body
        {
            get
            {
                if (State == PuzzleState.Solved) return "Through. The sigil door opens.";
                if (State == PuzzleState.Failed) return "The walls have come down and the sigil door is sealed - take the side door.";
                return "Find your way to the sigil door, at the top of the maze.";
            }
        }

        // ------------------------------------------------------------------ setup

        protected override void Setup()
        {
            int cw = Tuning.Puzzle.MazeCorridorCells, ww = Tuning.Puzzle.MazeWallCells;
            _cols = Tuning.Puzzle.MazeGridCols;
            _rows = Tuning.Puzzle.MazeGridRows;
            Spans(_cols, cw, ww, out var xs, out var xw);
            Spans(_rows, cw, ww, out var ys, out var yh);
            int n = xs.Length, m = ys.Length;
            var start = new Vector2Int(n / 2, 0);
            var goal = new Vector2Int(n / 2, m - 1);

            // ---- the maze: keep the first draw whose path is in the band, else the closest ----
            bool[] east = null, north = null;
            int bestMiss = int.MaxValue;
            for (int t = 0; t < Tuning.Puzzle.MazeTries; t++)
            {
                Carve(n, m, goal, out var e, out var no);
                int len = PathCells(n, m, e, no, start, goal);
                int miss = len < Tuning.Puzzle.MazePathMin ? Tuning.Puzzle.MazePathMin - len
                         : len > Tuning.Puzzle.MazePathMax ? len - Tuning.Puzzle.MazePathMax : 0;
                if (miss >= bestMiss) continue;
                bestMiss = miss;
                east = e; north = no; _pathCells = len;
                if (miss == 0) break;
            }

            // ---- the mask: all wall, then the cells and the passages carved out ----
            _wall = new bool[_cols * _rows];
            for (int i = 0; i < _wall.Length; i++) _wall[i] = true;
            void Open(int x0, int y0, int w, int h)
            {
                for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++) _wall[y * _cols + x] = false;
            }
            for (int j = 0; j < m; j++)
            for (int i = 0; i < n; i++)
            {
                Open(xs[i], ys[j], xw[i], yh[j]);
                if (i < n - 1 && east[j * n + i]) Open(xs[i] + xw[i], ys[j], ww, yh[j]);
                if (j < m - 1 && north[j * n + i]) Open(xs[i], ys[j] + yh[j], xw[i], ww);
            }

            // ---- pieces: every wall edge left standing, and every post with a wall touching it.
            // A post nothing touches is dropped - a lone block in open floor is not part of a maze.
            var pieces = new List<RectInt>();
            for (int j = 0; j < m; j++)
            for (int i = 0; i < n; i++)
            {
                if (i < n - 1 && !east[j * n + i]) pieces.Add(new RectInt(xs[i] + xw[i], ys[j], ww, yh[j]));
                if (j < m - 1 && !north[j * n + i]) pieces.Add(new RectInt(xs[i], ys[j] + yh[j], xw[i], ww));
                if (i < n - 1 && j < m - 1)
                {
                    bool touched = !east[j * n + i] || !east[(j + 1) * n + i]
                                || !north[j * n + i] || !north[j * n + i + 1];
                    var post = new RectInt(xs[i] + xw[i], ys[j] + yh[j], ww, ww);
                    if (touched) pieces.Add(post);
                    else Open(post.x, post.y, post.width, post.height);
                }
            }

            var cell = CellSize;
            Vector2 Corner(int x, int y) => new Vector2(x * cell.x, y * cell.y) - Arena.HalfExtents;

            _goal = new Rect(Corner(xs[goal.x], ys[goal.y]), new Vector2(xw[goal.x] * cell.x, yh[goal.y] * cell.y));
            var startRect = new Rect(Corner(xs[start.x], ys[start.y]), new Vector2(xw[start.x] * cell.x, yh[start.y] * cell.y));

            BuildColliders(cell, Corner);
            Paint(pieces, cell, Corner);
            Publish();

            _left = Tuning.Puzzle.MazeSeconds;
            float walk = _pathCells * (cw + ww) * Mathf.Max(cell.x, cell.y) / Tuning.Player.MoveSpeed;

            // The player arrives at the room's ordinary spawn point, which is not in a corridor
            // here - set them down in the start cell.
            if (Player != null)
            {
                Vector2 at = startRect.center;
                Player.position = at;
                var rb = Player.GetComponent<Rigidbody2D>();
                if (rb != null) { rb.position = at; rb.linearVelocity = Vector2.zero; }
                Physics2D.SyncTransforms();
            }
            Debug.Log($"[Puzzle] labyrinth {n}x{m}: path {_pathCells} cells (~{walk:0}s walking), clock {_left:0}s");
        }

        /// <summary>Where each maze cell starts and how wide it is along one axis of the mask.
        /// The axis rarely divides evenly - the spare cells go to the MIDDLE cell, so the column
        /// the player arrives in and the door stands over stays centred on x = 0.</summary>
        static void Spans(int total, int corridor, int wall, out int[] start, out int[] width)
        {
            int count = Mathf.Max(1, (total + wall) / (corridor + wall));
            int spare = total - (count * (corridor + wall) - wall);
            start = new int[count];
            width = new int[count];
            int at = 0;
            for (int i = 0; i < count; i++)
            {
                start[i] = at;
                width[i] = corridor + (i == count / 2 ? spare : 0);
                at += width[i] + wall;
            }
        }

        /// <summary>A recursive backtracker from the goal, its first step forced south. east[i]
        /// is open between cell i and the one east of it; north[i] between i and the one north.</summary>
        void Carve(int n, int m, Vector2Int goal, out bool[] east, out bool[] north)
        {
            east = new bool[n * m];
            north = new bool[n * m];
            var seen = new bool[n * m];
            seen[goal.y * n + goal.x] = true;
            var first = goal + Vector2Int.down;
            north[first.y * n + first.x] = true;
            seen[first.y * n + first.x] = true;

            var stack = new Stack<Vector2Int>();
            stack.Push(first);
            var next = new List<Vector2Int>(4);
            while (stack.Count > 0)
            {
                var c = stack.Peek();
                next.Clear();
                foreach (var d in Dirs)
                {
                    var q = c + d;
                    if (q.x >= 0 && q.y >= 0 && q.x < n && q.y < m && !seen[q.y * n + q.x]) next.Add(q);
                }
                if (next.Count == 0) { stack.Pop(); continue; }
                var to = next[Rng.Next(next.Count)];
                seen[to.y * n + to.x] = true;
                if (to.x != c.x) east[c.y * n + Mathf.Min(c.x, to.x)] = true;
                else north[Mathf.Min(c.y, to.y) * n + c.x] = true;
                stack.Push(to);
            }
        }

        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        static int PathCells(int n, int m, bool[] east, bool[] north, Vector2Int a, Vector2Int b)
        {
            var dist = new int[n * m];
            for (int i = 0; i < dist.Length; i++) dist[i] = -1;
            var q = new Queue<Vector2Int>();
            q.Enqueue(a);
            dist[a.y * n + a.x] = 0;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                int d = dist[c.y * n + c.x];
                if (c == b) return d;
                void Try(int x, int y, bool open)
                {
                    if (!open || dist[y * n + x] >= 0) return;
                    dist[y * n + x] = d + 1;
                    q.Enqueue(new Vector2Int(x, y));
                }
                if (c.x < n - 1) Try(c.x + 1, c.y, east[c.y * n + c.x]);
                if (c.x > 0) Try(c.x - 1, c.y, east[c.y * n + c.x - 1]);
                if (c.y < m - 1) Try(c.x, c.y + 1, north[c.y * n + c.x]);
                if (c.y > 0) Try(c.x, c.y - 1, north[(c.y - 1) * n + c.x]);
            }
            return 0;
        }

        Vector2 CellSize => new(Arena.HalfExtents.x * 2f / _cols, Arena.HalfExtents.y * 2f / _rows);

        bool WallAt(int x, int y) => x < 0 || y < 0 || x >= _cols || y >= _rows || _wall[y * _cols + x];

        // ------------------------------------------------------------------ the mask

        void Publish()
        {
            _owner = this;
            Arena.SetWalls(_wall, null, _cols, _rows);
            Enemies.NavField.MarkDirty();
        }

        void OnEnable()
        {
            if (_wall == null || _wall.Length == 0 || _collapseT >= 0f || State != PuzzleState.Active) return;
            Publish();
        }

        /// <summary>OnDisable, not only OnDestroy: GameBootstrap switches the puzzle off before
        /// destroying it, and the adjacent room's walls are published on that same frame - a
        /// deferred OnDestroy clearing the mask then would take THEIRS down.</summary>
        void OnDisable()
        {
            if (_owner != this) return;
            _owner = null;
            Arena.SetWalls(null, null, 0, 0);
            Enemies.NavField.MarkDirty();
        }

        /// <summary>One static collider per horizontal run of wall, RoomWalls' way.</summary>
        void BuildColliders(Vector2 cell, System.Func<int, int, Vector2> corner)
        {
            for (int y = 0; y < _rows; y++)
            {
                int x = 0;
                while (x < _cols)
                {
                    if (!_wall[y * _cols + x]) { x++; continue; }
                    int s = x;
                    while (x < _cols && _wall[y * _cols + x]) x++;
                    var box = gameObject.AddComponent<BoxCollider2D>();
                    box.size = new Vector2((x - s) * cell.x, cell.y);
                    box.offset = (Vector2)transform.InverseTransformPoint(corner(s, y) + box.size * 0.5f);
                }
            }
        }

        // ------------------------------------------------------------------ the picture

        /// <summary>
        /// The whole maze painted into one point-filtered texture at Tuning.Puzzle.MazeArtPpu (75 -
        /// the character's own density in its menu art, so wall and body share a grid under the
        /// close view), then cut into one sprite per piece. The same stone as the interior walls
        /// (RoomWalls.WallArt): a lit flagstone top, a coursed-brick south face wherever the floor
        /// in front is open - the only thing that says HEIGHT from this camera - and an outline on
        /// every side that meets floor. Patterns run on the texture's own coordinates, so a wall
        /// cut into pieces is still one wall.
        /// </summary>
        void Paint(List<RectInt> pieces, Vector2 cell, System.Func<int, int, Vector2> corner)
        {
            float ppu = Tuning.Puzzle.MazeArtPpu;
            int t = Mathf.Max(4, Mathf.RoundToInt(cell.x * ppu));
            int w = _cols * t, h = _rows * t;
            int face = Mathf.RoundToInt(t * Tuning.Puzzle.MazeFaceFraction);
            var px = new Color32[w * h];
            var rng = new System.Random(_cols * 7919 + _rows);

            for (int cy = 0; cy < _rows; cy++)
            for (int cx = 0; cx < _cols; cx++)
            {
                if (!_wall[cy * _cols + cx]) continue;
                bool southOpen = !WallAt(cx, cy - 1) && cy > 0, northOpen = !WallAt(cx, cy + 1) && cy < _rows - 1;
                bool westOpen = !WallAt(cx - 1, cy) && cx > 0, eastOpen = !WallAt(cx + 1, cy) && cx < _cols - 1;
                int f = southOpen ? face : 0;
                for (int v = 0; v < t; v++)
                for (int u = 0; u < t; u++)
                {
                    int gx = cx * t + u, gy = cy * t + v;
                    Color c;
                    if (v < f)
                    {
                        int course = v / 5;
                        int bx = (gx + (course % 2) * 6) % 12;
                        c = v % 5 == 0 || bx == 0 ? FaceSeam : Face;
                        if (v == f - 1) c = Color.Lerp(Face, Top, 0.5f);
                    }
                    else
                    {
                        int slab = (gx + (gy / 10) * 7) % 16;
                        c = gy % 10 == 0 || slab == 0 ? Seam : (slab < 3 ? TopLight : Top);
                        if (rng.Next(9) == 0) c = Color.Lerp(c, Seam, 0.5f);
                        if (northOpen && v >= t - 3) c = Rim;
                    }
                    if ((westOpen && u < 2) || (eastOpen && u >= t - 2) || (southOpen && v < 2) || (northOpen && v == t - 1))
                        c = Line;
                    px[gy * w + gx] = c;
                }
            }

            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _tex.SetPixels32(px);
            _tex.Apply();

            // A contact shadow on the floor under every south face (RoomWalls' reason: it is what
            // sets a block ON the ground), its own small texture on the decal layer.
            // Three shadow texels a cell; the top row of the floor cell under a face is shaded.
            const int st = 3;
            var shade = new Color32[_cols * st * _rows * st];
            int sw = _cols * st;
            for (int cy = 1; cy < _rows; cy++)
            for (int cx = 0; cx < _cols; cx++)
            {
                if (!_wall[cy * _cols + cx] || _wall[(cy - 1) * _cols + cx]) continue;
                for (int u = 0; u < st; u++)
                    shade[((cy - 1) * st + st - 1) * sw + cx * st + u] = new Color32(0, 0, 0, 82);
            }
            var stex = new Texture2D(sw, _rows * st, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            stex.SetPixels32(shade);
            stex.Apply();
            var sgo = new GameObject("maze.shadow");
            sgo.transform.SetParent(transform, false);
            sgo.transform.position = -(Vector3)Arena.HalfExtents;
            _shadow = sgo.AddComponent<SpriteRenderer>();
            _shadow.sprite = Sprite.Create(stex, new Rect(0, 0, stex.width, stex.height), Vector2.zero,
                                           stex.width / (Arena.HalfExtents.x * 2f));
            _shadow.sortingOrder = SortingOrders.GroundDecal;

            foreach (var p in pieces)
            {
                var texels = new RectInt(p.x * t, p.y * t, p.width * t, p.height * t);
                var go = new GameObject($"maze.wall.{p.x}.{p.y}");
                go.transform.SetParent(transform, false);
                var foot = corner(p.x, p.y) + new Vector2(p.width * cell.x * 0.5f, 0f);
                go.transform.position = foot;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Cut(texels, 0);
                sr.sortingOrder = SortingOrders.PitBase + 1;
                // A non-square mask cell (an arena resized off 24x14) stretches rather than gaps.
                go.transform.localScale = new Vector3(cell.x * ppu / t, cell.y * ppu / t, 1f);
                _pieces.Add(sr);
                _pieceRest.Add(go.transform.localPosition);
                _pieceTexels.Add(texels);
                _pieceDelay.Add(0f);
                _pieceCut.Add(0);
            }
        }

        /// <summary>The piece's picture with its bottom <paramref name="cut"/> rows sunk below the
        /// floor - its top rows drawn standing on its foot. CPU-cut, never a SpriteMask (see
        /// Sorting in CLAUDE.md: masked sprites can draw another sprite's texture).</summary>
        Sprite Cut(RectInt r, int cut)
        {
            cut = Mathf.Clamp(cut, 0, r.height - 1);
            var s = Sprite.Create(_tex, new Rect(r.x, r.y + cut, r.width, r.height - cut),
                                  new Vector2(0.5f, 0f), Tuning.Puzzle.MazeArtPpu);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        // ------------------------------------------------------------------ play

        protected override void Tick(int stepped)
        {
            if (_goal.Contains(Player.position))
            {
                Solve();
                return;
            }
            _left -= Time.deltaTime;
            if (_left > 0f) return;
            _left = 0f;
            Fail();
            Collapse();
        }

        /// <summary>
        /// The walls come down: every collider off and the mask cleared at once (the room is open
        /// floor from this frame - the side door is about to stand in it), then each piece shakes
        /// and sinks into the floor in a wave running outward from the player, so the first thing
        /// they see is the corridor round them going.
        /// </summary>
        void Collapse()
        {
            foreach (var box in GetComponents<BoxCollider2D>()) box.enabled = false;
            if (_owner == this)
            {
                _owner = null;
                Arena.SetWalls(null, null, 0, 0);
                Enemies.NavField.MarkDirty();
            }
            Vector2 from = Player != null ? (Vector2)Player.position : Vector2.zero;
            for (int i = 0; i < _pieces.Count; i++)
                if (_pieces[i] != null)
                {
                    _pieceDelay[i] = Vector2.Distance(from, _pieces[i].transform.position) / Tuning.Puzzle.MazeCollapseSpeed;
                    _collapseLast = Mathf.Max(_collapseLast, _pieceDelay[i]);
                }
            _collapseT = 0f;
        }

        void LateUpdate()
        {
            if (_collapseT < 0f) return;
            _collapseT += Time.deltaTime;
            float shake = Tuning.Puzzle.MazeCollapseShake, sink = Tuning.Puzzle.MazeCollapseSink;
            float texel = 1f / Tuning.Puzzle.MazeArtPpu;
            for (int i = 0; i < _pieces.Count; i++)
            {
                var sr = _pieces[i];
                if (sr == null || !sr.enabled) continue;
                float k = _collapseT - _pieceDelay[i];
                if (k < 0f) continue;

                var r = _pieceTexels[i];
                var child = sr.transform;
                if (k < shake)
                {
                    // A rattle a texel either way, on the texel grid.
                    int jit = ((int)(k * 40f) + i) % 3 - 1;
                    child.localPosition = _pieceRest[i] + new Vector3(jit * texel, 0f, 0f);
                    continue;
                }
                child.localPosition = _pieceRest[i];
                int cut = Mathf.Min(r.height, Mathf.CeilToInt((k - shake) / sink * r.height));
                if (cut == _pieceCut[i]) continue;
                if (_pieceCut[i] == 0)
                {
                    var mid = (Vector2)child.position + new Vector2(0f, r.height * texel * 0.5f);
                    Spr.Flash(mid, Mathf.Max(r.width, r.height) * texel * 0.9f, Dust, 0.35f, ring: false);
                }
                var old = sr.sprite;
                if (cut >= r.height) sr.enabled = false;
                else sr.sprite = Cut(r, cut);
                if (_pieceCut[i] > 0 && old != null) Destroy(old);
                _pieceCut[i] = cut;
            }

            if (_shadow != null)
            {
                float a = 1f - Mathf.Clamp01(_collapseT / (_collapseLast + shake + sink));
                _shadow.color = new Color(1f, 1f, 1f, a);
            }
        }

        void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }
    }
}
