using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.Enemies
{
    /// <summary>
    /// How every walking enemy gets AROUND the room: one grid over the arena, one route outward
    /// from the player, shared by the whole wave.
    ///
    /// ONE ROUTE FOR EVERYONE, NOT ONE PATH EACH. Every enemy is headed for the same body, so the
    /// cheapest way there from EVERY cell is computed once (Dijkstra outward from the player) and
    /// each enemy just reads the cell it is standing in. Fifteen enemies cost the same as one.
    ///
    /// WHAT BLOCKS IS ASKED OF PHYSICS, NOT LISTED. A cell is closed when a solid static collider
    /// (wall, column, a risen spire, Medusa's pillars) or a boss's body lies within
    /// <see cref="Tuning.Steering.Clearance"/> of its centre - so a new kind of obstacle needs no
    /// code here. Triggers (force fields, rifts) and corpses (which enemies walk through) never
    /// close a cell.
    ///
    /// PITS ARE COSTS, NOT WALLS (<see cref="FloorPit.RouteCost"/>): a route crosses sand when the
    /// detour is long, goes round lit fire, and walks over ash. Re-read every route, so a fire's
    /// cost follows its cycle.
    ///
    /// Driven LAZILY - every enemy calls <see cref="Tick"/> from its FixedUpdate and the work runs
    /// at most once per interval. Nothing owns it, so nothing has to be torn down, and a domain
    /// reload (which nulls every static array here) simply rebuilds on the next call.
    /// </summary>
    public static class NavField
    {
        // Not readonly, null-guarded: see the domain-reload notes in CLAUDE.md.
        static int _w, _h;
        static Vector2 _origin, _builtFor;
        static bool[] _blocked;
        static float[] _terrain;   // extra route cost per unit, from pits
        static float[] _cost;      // route cost to the player; +inf = unreachable
        static int[] _heapCell;
        static float[] _heapKey;
        static int _heapCount;
        static List<Collider2D> _hits;

        static bool _dirty = true;
        static float _nextRoute = float.NegativeInfinity, _nextScan = float.NegativeInfinity;

        static float Cell => Tuning.Steering.CellSize;

        /// <summary>True once a route exists - every query falls back to a straight line before.</summary>
        public static bool Ready => _cost != null;

        /// <summary>Re-scan the obstacles on the next tick: a column broke, a floor was built.</summary>
        public static void MarkDirty() => _dirty = true;

        /// <summary>Bring the grid and the route up to date. Cheap when nothing is due.</summary>
        public static void Tick(Vector2 target)
        {
            float now = Time.time;
            bool route = now >= _nextRoute;

            if (_blocked == null || _builtFor != Arena.HalfExtents) { Allocate(); _dirty = true; }
            if (_dirty || now >= _nextScan)
            {
                ScanObstacles();
                _dirty = false;
                _nextScan = now + Tuning.Steering.ObstacleInterval;
                route = true;
            }
            if (!route) return;

            LayTerrain();
            Route(target);
            _nextRoute = now + Tuning.Steering.RouteInterval;
        }

        // ------------------------------------------------------------------ building

        static void Allocate()
        {
            _builtFor = Arena.HalfExtents;
            _w = Mathf.Max(1, Mathf.CeilToInt(_builtFor.x * 2f / Cell));
            _h = Mathf.Max(1, Mathf.CeilToInt(_builtFor.y * 2f / Cell));
            _origin = -_builtFor;
            int n = _w * _h;
            _blocked = new bool[n];
            _terrain = new float[n];
            _cost = null;   // not Ready until the first route
            _heapCell = new int[n * 8 + 8];
            _heapKey = new float[n * 8 + 8];
        }

        static void ScanObstacles()
        {
            _hits ??= new List<Collider2D>();
            var filter = ContactFilter2D.noFilter;
            filter.useTriggers = false;

            float clear = Tuning.Steering.Clearance;
            for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                int i = y * _w + x;
                var c = Center(x, y);

                // Off the floor by more than the clearance - the walls are there whether or not
                // their colliders reach this far in.
                if (Mathf.Abs(c.x) > _builtFor.x - clear || Mathf.Abs(c.y) > _builtFor.y - clear)
                {
                    _blocked[i] = true;
                    continue;
                }

                // A chasm closes the cell too - nothing walks on void, and a route along its lip
                // would hand every Heavy finisher a free kill. Asked of the room, not of physics:
                // a chasm has no collider (shots and knocked bodies must cross it).
                if (Arena.ChasmNear(c, clear))
                {
                    _blocked[i] = true;
                    continue;
                }

                _hits.Clear();
                Physics2D.OverlapCircle(c, clear, filter, _hits);
                bool blocked = false;
                foreach (var h in _hits)
                    if (Solid(h)) { blocked = true; break; }
                _blocked[i] = blocked;
            }
        }

        /// <summary>Static colliders (walls, columns, the spire) and bosses' kinematic bodies.
        /// Not corpses - enemies walk through those - unless it is Ossuary's, which blocks them,
        /// and not a column already breaking.</summary>
        internal static bool Solid(Collider2D c)
        {
            if (c == null || !c.enabled) return false;
            var rb = c.attachedRigidbody;
            if (rb == null)
            {
                var column = c.GetComponent<Column>();
                return column == null || !column.Broken;
            }
            if (rb.bodyType == RigidbodyType2D.Static)
            {
                var corpse = rb.GetComponent<Combat.Corpse>();
                return corpse != null && corpse.BlocksEnemies;
            }
            return rb.bodyType == RigidbodyType2D.Kinematic && rb.GetComponent<Bosses.Boss>() != null;
        }

        static void LayTerrain()
        {
            System.Array.Clear(_terrain, 0, _terrain.Length);
            var pits = FloorPits.Live;
            for (int p = 0; p < pits.Count; p++)
            {
                var pit = pits[p];
                if (pit == null) continue;
                float cost = pit.RouteCost;
                if (cost <= 0f) continue;

                // The FULL footprint, not the inset stand-test: a body whose centre is just outside
                // still has half of itself in the pit.
                var r = pit.Footprint;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((r.xMin - _origin.x) / Cell));
                int x1 = Mathf.Min(_w - 1, Mathf.FloorToInt((r.xMax - _origin.x) / Cell));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((r.yMin - _origin.y) / Cell));
                int y1 = Mathf.Min(_h - 1, Mathf.FloorToInt((r.yMax - _origin.y) / Cell));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (r.Contains(Center(x, y)))
                    {
                        int i = y * _w + x;
                        _terrain[i] = Mathf.Max(_terrain[i], cost);
                    }
            }
        }

        static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        static void Route(Vector2 target)
        {
            int n = _w * _h;
            if (_cost == null || _cost.Length != n) _cost = new float[n];
            for (int i = 0; i < n; i++) _cost[i] = float.PositiveInfinity;
            _heapCount = 0;

            // Seed: the player's own cell, or - when they stand closer to a column than an enemy
            // could - every open cell around them, priced by the straight distance.
            int tc = IndexOf(target);
            if (tc >= 0 && !_blocked[tc]) Seed(tc, Vector2.Distance(target, CenterOf(tc)));
            else
            {
                int r = Mathf.CeilToInt(1.5f / Cell);
                CellXY(target, out int tx, out int ty);
                for (int y = ty - r; y <= ty + r; y++)
                for (int x = tx - r; x <= tx + r; x++)
                {
                    if (x < 0 || y < 0 || x >= _w || y >= _h) continue;
                    int i = y * _w + x;
                    if (_blocked[i]) continue;
                    float d = Vector2.Distance(target, Center(x, y));
                    if (d <= 1.5f) Seed(i, d);
                }
            }

            while (_heapCount > 0)
            {
                Pop(out int cur, out float key);
                if (key > _cost[cur]) continue;   // stale entry
                int cx = cur % _w, cy = cur / _w;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + Dx[k], ny = cy + Dy[k];
                    if (nx < 0 || ny < 0 || nx >= _w || ny >= _h) continue;
                    int ni = ny * _w + nx;
                    if (_blocked[ni]) continue;

                    float len = 1f;
                    if (k >= 4)
                    {
                        // No cutting a corner past a closed cell.
                        if (_blocked[cy * _w + nx] || _blocked[ny * _w + cx]) continue;
                        len = 1.41421356f;
                    }
                    float step = len * Cell * (1f + 0.5f * (_terrain[cur] + _terrain[ni]));
                    float c = key + step;
                    if (c < _cost[ni]) { _cost[ni] = c; Push(ni, c); }
                }
            }
        }

        static void Seed(int i, float c)
        {
            if (c >= _cost[i]) return;
            _cost[i] = c;
            Push(i, c);
        }

        // A plain binary min-heap with lazy deletion - ~1300 cells needs nothing cleverer.
        static void Push(int cell, float key)
        {
            if (_heapCount >= _heapCell.Length) return;   // can't happen at 8 pushes per cell
            int i = _heapCount++;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (_heapKey[p] <= key) break;
                _heapCell[i] = _heapCell[p];
                _heapKey[i] = _heapKey[p];
                i = p;
            }
            _heapCell[i] = cell;
            _heapKey[i] = key;
        }

        static void Pop(out int cell, out float key)
        {
            cell = _heapCell[0];
            key = _heapKey[0];
            int lastCell = _heapCell[--_heapCount];
            float lastKey = _heapKey[_heapCount];
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1;
                if (l >= _heapCount) break;
                int r = l + 1;
                int m = r < _heapCount && _heapKey[r] < _heapKey[l] ? r : l;
                if (_heapKey[m] >= lastKey) break;
                _heapCell[i] = _heapCell[m];
                _heapKey[i] = _heapKey[m];
                i = m;
            }
            _heapCell[i] = lastCell;
            _heapKey[i] = lastKey;
        }

        // ------------------------------------------------------------------ queries

        static Vector2 Center(int x, int y) => _origin + new Vector2((x + 0.5f) * Cell, (y + 0.5f) * Cell);

        static Vector2 CenterOf(int i) => Center(i % _w, i / _w);

        static void CellXY(Vector2 p, out int x, out int y)
        {
            x = Mathf.FloorToInt((p.x - _origin.x) / Cell);
            y = Mathf.FloorToInt((p.y - _origin.y) / Cell);
        }

        static int IndexOf(Vector2 p)
        {
            if (_blocked == null) return -1;
            CellXY(p, out int x, out int y);
            if (x < 0 || y < 0 || x >= _w || y >= _h) return -1;
            return y * _w + x;
        }

        /// <summary>
        /// How far a line may run through closed cells it STARTED in before they count. A body
        /// pressed against a column stands in a cell the clearance closed, and must be able to see
        /// out of it - but not THROUGH the column, which is what ignoring the start without a
        /// limit allowed.
        /// </summary>
        static float Grace => Tuning.Steering.Clearance + Cell * 0.5f;

        static bool Finite(int i) => i >= 0 && _cost != null && !float.IsPositiveInfinity(_cost[i]);

        /// <summary>
        /// Can the route reach this point - its own cell, or one right beside it (a body hugging a
        /// column stands in a cell the clearance closed)? False means walled into a pocket, or
        /// inside geometry. Always true before the first route.
        /// </summary>
        public static bool Reachable(Vector2 p)
        {
            if (!Ready) return true;
            CellXY(p, out int cx, out int cy);
            for (int y = cy - 1; y <= cy + 1; y++)
            for (int x = cx - 1; x <= cx + 1; x++)
                if (x >= 0 && y >= 0 && x < _w && y < _h && Finite(y * _w + x)) return true;
            return false;
        }

        /// <summary>The nearest point the route reaches, searching outward a few metres;
        /// <paramref name="p"/> itself when nothing is found.</summary>
        public static Vector2 NearestReachable(Vector2 p)
        {
            if (!Ready) return p;
            CellXY(p, out int cx, out int cy);
            int maxRing = Mathf.CeilToInt(4f / Cell);
            for (int ring = 0; ring <= maxRing; ring++)
            {
                int best = -1;
                float bestD = float.PositiveInfinity;
                for (int y = cy - ring; y <= cy + ring; y++)
                for (int x = cx - ring; x <= cx + ring; x++)
                {
                    if (Mathf.Max(Mathf.Abs(x - cx), Mathf.Abs(y - cy)) != ring) continue;
                    if (x < 0 || y < 0 || x >= _w || y >= _h) continue;
                    int i = y * _w + x;
                    if (!Finite(i)) continue;
                    float d = (Center(x, y) - p).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best >= 0) return CenterOf(best);
            }
            return p;
        }

        /// <summary>
        /// Is the straight line from <paramref name="a"/> to <paramref name="b"/> walkable - no
        /// closed cell on it, and no pit costlier than <see cref="Tuning.Steering.ShortcutCostLimit"/>?
        /// Whatever <paramref name="a"/> starts IN (a closed cell it was pushed into, a pit it is
        /// already crossing) is ignored until the line leaves it, or nothing standing there could
        /// ever see out.
        /// </summary>
        public static bool ClearLine(Vector2 a, Vector2 b)
        {
            if (_blocked == null) return true;
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return true;
            float step = Cell * 0.5f;
            int steps = Mathf.CeilToInt(len / step);
            // What the line STARTS in is the start's own cell - judging it by the first sample
            // ahead let a line whose first step entered a pit count as already being in it.
            int i0 = IndexOf(a);
            bool leftWall = i0 < 0 || !_blocked[i0];
            bool leftPit = i0 < 0 || _terrain[i0] <= Tuning.Steering.ShortcutCostLimit;
            for (int s = 1; s <= steps; s++)
            {
                float t = Mathf.Min(s * step, len);
                var p = a + d * (t / len);
                int i = IndexOf(p);
                if (i < 0) return false;
                if (_blocked[i]) { if (leftWall || t > Grace) return false; }
                else leftWall = true;
                if (_terrain[i] > Tuning.Steering.ShortcutCostLimit) { if (leftPit) return false; }
                else leftPit = true;
            }
            return true;
        }

        /// <summary>
        /// Which way to walk from <paramref name="pos"/> toward the player at
        /// <paramref name="target"/>. Straight at them when the line is clear; otherwise down the
        /// route, aimed at the FARTHEST cell along it still in a straight line - so the walk is a
        /// smooth curve round a column rather than a staircase of grid steps. Unit length.
        /// </summary>
        public static Vector2 RouteDirection(Vector2 pos, Vector2 target)
        {
            var straight = target - pos;
            if (straight.sqrMagnitude < 1e-6f) return Vector2.zero;
            straight.Normalize();
            if (!Ready || ClearLine(pos, target)) return straight;

            int cur = IndexOf(pos);
            if (!Finite(cur))
            {
                // Pressed against a column, the body stands in a cell the clearance closed. Head
                // for the CHEAPEST open cell close by - the nearest one can be behind it, and
                // going there and back again is what slid a body slowly round a column's face.
                // Nothing close (walled into a pocket): the stuck watch takes over.
                int best = CheapestNear(pos, 3);
                var to = (best >= 0 ? CenterOf(best) : NearestReachable(pos)) - pos;
                return to.sqrMagnitude > 1e-6f ? to.normalized : straight;
            }

            var aim = CenterOf(cur);
            bool any = false;
            for (int k = 0; k < Tuning.Steering.RouteLookaheadCells; k++)
            {
                int next = Downhill(cur);
                if (next < 0) break;
                cur = next;
                var c = CenterOf(cur);
                if (any && !ClearLine(pos, c)) break;
                aim = c;
                any = true;
            }
            var dir = aim - pos;
            return dir.sqrMagnitude > 1e-6f ? dir.normalized : straight;
        }

        static int CheapestNear(Vector2 p, int rings)
        {
            CellXY(p, out int cx, out int cy);
            int best = -1;
            float bestCost = float.PositiveInfinity;
            for (int y = cy - rings; y <= cy + rings; y++)
            for (int x = cx - rings; x <= cx + rings; x++)
            {
                if (x < 0 || y < 0 || x >= _w || y >= _h) continue;
                int i = y * _w + x;
                if (_cost[i] < bestCost) { bestCost = _cost[i]; best = i; }
            }
            return best;
        }

        static int Downhill(int cur)
        {
            int cx = cur % _w, cy = cur / _w;
            int best = -1;
            float bestCost = _cost[cur];
            for (int k = 0; k < 8; k++)
            {
                int nx = cx + Dx[k], ny = cy + Dy[k];
                if (nx < 0 || ny < 0 || nx >= _w || ny >= _h) continue;
                if (k >= 4 && (_blocked[cy * _w + nx] || _blocked[ny * _w + cx])) continue;
                int ni = ny * _w + nx;
                if (_cost[ni] < bestCost) { bestCost = _cost[ni]; best = ni; }
            }
            return best;
        }

        /// <summary>
        /// How far along <paramref name="dir"/> from <paramref name="pos"/> before a closed cell,
        /// up to <paramref name="max"/>; and the costliest pit met on the way. Like
        /// <see cref="ClearLine"/>, whatever the body already stands in is ignored until the ray
        /// leaves it. The edge of the grid counts as closed.
        /// </summary>
        public static float FreeDistance(Vector2 pos, Vector2 dir, float max, out float pitCost)
        {
            pitCost = 0f;
            if (_blocked == null) return max;
            float step = Cell * 0.5f;
            int i0 = IndexOf(pos);
            bool leftWall = i0 < 0 || !_blocked[i0];
            bool leftPit = i0 < 0 || _terrain[i0] <= 0f;
            for (float t = step; t <= max + 1e-4f; t += step)
            {
                int i = IndexOf(pos + dir * t);
                // Off the grid is a wall - and one reached without ever leaving the closed cells
                // it started in is no floor at all (a body in the wall's clearance band, looking
                // into the wall).
                if (i < 0) return leftWall ? t - step : 0f;
                if (_blocked[i])
                {
                    if (leftWall) return t - step;
                    if (t > Grace) return 0f;   // still inside it: this way goes deeper, not out
                }
                else leftWall = true;
                if (_terrain[i] > 0f) { if (leftPit) pitCost = Mathf.Max(pitCost, _terrain[i]); }
                else leftPit = true;
            }
            return max;
        }
    }
}
