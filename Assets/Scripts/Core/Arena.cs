using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Core
{
    /// <summary>
    /// The playfield's extents, published by <see cref="GameBootstrap"/> when it builds the arena.
    ///
    /// Exists because anything that MOVES something to a computed point needs to know where the
    /// walls are - the blade throw's teleport most of all, since landing the player outside the
    /// arena is unrecoverable rather than merely wrong.
    ///
    /// It also holds the floor's ROOM SHAPE (Hazards.RoomShape): which cells of the arena are wall.
    /// The outer rectangle is still <see cref="HalfExtents"/>; the shape carves walls INSIDE it, so
    /// <see cref="Clamp"/> stays a valid outer bound and <see cref="OnFloor"/> /
    /// <see cref="NearestFloor"/> are the questions anything PLACING something must ask.
    /// </summary>
    public static class Arena
    {
        public static Vector2 HalfExtents = new(12f, 7f);

        /// <summary>Nearest point inside the walls, keeping a body-width of clearance.</summary>
        public static Vector2 Clamp(Vector2 p, float margin = 0.6f)
            => new(Mathf.Clamp(p.x, -HalfExtents.x + margin, HalfExtents.x - margin),
                   Mathf.Clamp(p.y, -HalfExtents.y + margin, HalfExtents.y - margin));

        /// <summary>How far off the north/south wall the floor door and the run's arrival point
        /// sit - shared so GameBootstrap and HazardBuilder can't drift apart on where these two
        /// fixed spots actually are.</summary>
        public const float DoorWallInset = 1.6f;

        /// <summary>Where the FloorDoor spawns once a floor is cleared - fixed, not a hazard
        /// lattice point, so nothing that scatters onto the lattice may be filtered into it.</summary>
        public static Vector2 NorthDoorPoint => new(0f, HalfExtents.y - DoorWallInset);

        /// <summary>Where the player arrives at the start of a floor - mirrors NorthDoorPoint off
        /// the south wall.</summary>
        public static Vector2 SouthSpawnPoint => new(0f, -HalfExtents.y + DoorWallInset);

        /// <summary>The foot of the north wall's face - where a door IN the wall stands. Keep
        /// <see cref="NorthDoorPoint"/> for "in front of the door" (what placement keeps clear).</summary>
        public static float NorthWallFoot => HalfExtents.y - Tuning.Scenery.WallInset;

        // ------------------------------------------------------------------ doorways in the north wall

        // x of each open doorway, keyed by the door holding it. Plain (not readonly): a domain
        // reload re-runs every door's OnEnable, which registers it again.
        static Dictionary<int, float> _gaps = new();

        /// <summary>Bumped whenever a doorway opens or closes; Core.NorthWall rebuilds its
        /// colliders when this moves.</summary>
        public static int NorthGapVersion { get; private set; }

        public static IEnumerable<float> NorthGaps => _gaps.Values;

        public static void OpenNorthGap(int owner, float x)
        {
            _gaps ??= new Dictionary<int, float>();
            if (_gaps.TryGetValue(owner, out float was) && Mathf.Approximately(was, x)) return;
            _gaps[owner] = x;
            NorthGapVersion++;
        }

        public static void CloseNorthGap(int owner)
        {
            if (_gaps != null && _gaps.Remove(owner)) NorthGapVersion++;
        }

        // ------------------------------------------------------------------ the room shape

        // Cells of the shape grid, bottom row first. Null = no shape (the open rectangle). Set and
        // cleared by Hazards.RoomWalls, which keeps its own serialized copy and hands it back here
        // after a domain reload empties this static.
        //
        // TWO KINDS OF NOT-FLOOR. A WALL stops bodies, sight and shots. A CHASM stops nothing in
        // the air - sight and shots cross it - but nothing stands on it: a body over one falls
        // (EnemyController.TickChasm, PlayerController's edge). Neither is standing room.
        static bool[] _wall, _chasm, _edge;
        static int _cols, _rows;

        // ------------------------------------------------------------------ open edges
        //
        // THE ROOM'S EDGE IS A LIP, NOT A WALL (the user's call, 2026-11-01): on an ordinary floor
        // the west, east and south sides - and every outline cell a composed room opened to them -
        // end at the place's void (a drop, a lake, brine, cloud). The north wall stays: the camera
        // sees its face and the door is cut into it.
        //
        // A BARRIER holds every body at an open edge (the outer walls' colliders, and a collider
        // over each opened outline cell): the player is never charged a fall there, dash or no
        // dash. NOTHING ELSE TOUCHES IT. A wall that stands interacts; an open edge never does -
        // shots fly on over the void, a harpoon has nothing to snag, the thrown blade does not
        // rebound, sight is not cut. An enemy THROWN at an open edge goes over it as into a chasm
        // (EnemyController.ThrowIfChasm) - the glide leaves physics, so the barrier never sees it.

        /// <summary>True while the floor's west, east and south sides are open edges.</summary>
        public static bool EdgesOpen { get; private set; }

        /// <summary>True while the void past the open edges is water (a fall there sinks).</summary>
        public static bool EdgeIsWater { get; private set; }

        /// <summary>Set by the floor's dressing, before anything is placed.</summary>
        public static void SetOpenEdges(bool open, bool water)
        {
            EdgesOpen = open;
            EdgeIsWater = open && water;
        }

        /// <summary>The raised dais before the door this floor (Hazards.RoomShape.Dais), in world
        /// units; zero size when there is none. Its cells are floor - only its edge holds bodies.</summary>
        public static Rect Dais;

        /// <summary>Would a round thing of radius <paramref name="r"/> at <paramref name="p"/>
        /// straddle the dais's edge or its face? (Wholly on it, or wholly off it, is fine.)</summary>
        public static bool NearDaisEdge(Vector2 p, float r)
        {
            if (Dais.width <= 0f) return false;
            var d = Dais;
            bool outside = p.x < d.xMin - r || p.x > d.xMax + r || p.y < d.yMin - Tuning.Rooms.DaisFace - r;
            bool inside = p.x > d.xMin + r && p.x < d.xMax - r && p.y > d.yMin + r;
            return !outside && !inside;
        }

        /// <summary>The inner faces of the outer walls - where the floor ends.</summary>
        public static Vector2 Inner => HalfExtents - Vector2.one * Tuning.Scenery.WallInset;

        /// <summary>Is <paramref name="p"/> past an open side of the room by more than
        /// <paramref name="inset"/> - over the void beyond the floor's edge?</summary>
        public static bool PastOpenEdge(Vector2 p, float inset = 0f)
        {
            if (!EdgesOpen) return false;
            var i = Inner;
            if (p.y > i.y) return false;   // the north wall
            return p.x < -i.x - inset || p.x > i.x + inset || p.y < -i.y - inset;
        }

        /// <summary>
        /// <see cref="Clamp"/> for a SHOT: held only by the sides that are walls. An open side
        /// lets it fly on - past the edge by <see cref="Tuning.Edge.ShotReach"/> it is gone
        /// (<see cref="ShotGone"/>), which is the mover's call to make.
        /// </summary>
        public static Vector2 ShotClamp(Vector2 p, float margin = 0.6f)
        {
            if (!EdgesOpen) return Clamp(p, margin);
            float far = Tuning.Edge.ShotReach + 1f;
            return new Vector2(Mathf.Clamp(p.x, -HalfExtents.x - far, HalfExtents.x + far),
                               Mathf.Clamp(p.y, -HalfExtents.y - far, HalfExtents.y - margin));
        }

        /// <summary>Has a shot flown far enough out over an open edge to be gone?</summary>
        public static bool ShotGone(Vector2 p) => PastOpenEdge(p, Tuning.Edge.ShotReach);

        /// <summary>True while the floor has a shape (walls, chasms or both).</summary>
        public static bool HasWalls => _wall != null;

        /// <summary>True while the floor has at least one chasm cell.</summary>
        public static bool HasChasms { get; private set; }

        /// <summary>True while the floor's chasms are a LAKE (Hazards.RoomShape.Lake). Every rule
        /// is the same; what asks is only how a fall LOOKS - a splash, not the dark.</summary>
        public static bool ChasmIsWater { get; private set; }

        /// <summary>Install a shape (each array cols x rows, bottom row first; chasm may be null),
        /// or clear it with null.</summary>
        public static void SetWalls(bool[] wall, bool[] chasm, int cols, int rows, bool chasmIsWater = false,
                                    bool[] edge = null)
        {
            _wall = wall;
            ChasmIsWater = wall != null && chasmIsWater;
            _chasm = wall != null ? chasm : null;
            _edge = wall != null ? edge : null;
            _cols = cols;
            _rows = rows;
            HasChasms = false;
            if (_chasm != null) foreach (bool c in _chasm) if (c) { HasChasms = true; break; }
        }

        /// <summary>A cracked segment broke: its cells are floor from now on.</summary>
        public static void OpenCell(int x, int y)
        {
            if (_wall != null && x >= 0 && y >= 0 && x < _cols && y < _rows) _wall[y * _cols + x] = false;
        }

        static Vector2 CellSize => new(HalfExtents.x * 2f / _cols, HalfExtents.y * 2f / _rows);

        static bool WallAt(int x, int y)
            => _wall != null && x >= 0 && y >= 0 && x < _cols && y < _rows && _wall[y * _cols + x];

        static bool ChasmAt(int x, int y)
            => _chasm != null && x >= 0 && y >= 0 && x < _cols && y < _rows && _chasm[y * _cols + x];

        /// <summary>An outline cell opened into the void (an open edge inside the grid).</summary>
        static bool EdgeAt(int x, int y)
            => _edge != null && x >= 0 && y >= 0 && x < _cols && y < _rows && _edge[y * _cols + x];

        /// <summary>Not standing room: a wall, a chasm or an opened edge.</summary>
        static bool BlockedAt(int x, int y) => WallAt(x, y) || ChasmAt(x, y) || EdgeAt(x, y);

        /// <summary>Void of either kind - a chasm, an opened edge cell, or past an open side
        /// (cells off the grid on an open side).</summary>
        static bool VoidAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _cols || y >= _rows)
                return EdgesOpen && y < _rows;
            return ChasmAt(x, y) || EdgeAt(x, y);
        }

        static Vector2Int CellOf(Vector2 p)
        {
            var q = p + HalfExtents;
            var cell = CellSize;
            return new Vector2Int(Mathf.FloorToInt(q.x / cell.x), Mathf.FloorToInt(q.y / cell.y));
        }

        /// <summary>True while anything on the floor is void a body can be thrown into.</summary>
        public static bool HasVoid => HasChasms || EdgesOpen;

        /// <summary>
        /// Is <paramref name="p"/> over void a THROWN body falls into - a chasm, or an open edge -
        /// at least <paramref name="inset"/> in from the lip? (<see cref="DeepInChasm"/> is the
        /// chasm alone: what the player's own walk and landings are judged against.)
        /// </summary>
        public static bool DeepInVoid(Vector2 p, float inset = 0f)
        {
            if (DeepInChasm(p, inset)) return true;
            if (PastOpenEdge(p, inset)) return true;
            if (_edge == null) return false;
            var c = CellOf(p);
            if (!EdgeAt(c.x, c.y)) return false;
            return inset <= 0f || !Near(p, inset, (x, y) => !VoidAt(x, y));
        }

        /// <summary>Is <paramref name="p"/> over an open edge's void - past an open side, or in
        /// an outline cell opened into it? (Chasms are not edges.)</summary>
        public static bool OpenEdgeAt(Vector2 p)
        {
            if (PastOpenEdge(p)) return true;
            if (_edge == null) return false;
            var c = CellOf(p);
            return EdgeAt(c.x, c.y);
        }

        /// <summary>Does a body falling at <paramref name="p"/> go into WATER (it sinks) rather
        /// than down a drop?</summary>
        public static bool WaterAt(Vector2 p)
        {
            var c = CellOf(p);
            bool edge = PastOpenEdge(p) || EdgeAt(c.x, c.y);
            return edge ? EdgeIsWater : ChasmIsWater;
        }

        /// <summary>Is any cell matching <paramref name="test"/> within <paramref name="margin"/>
        /// of <paramref name="p"/> (or under it)? Circle against each cell's rectangle.</summary>
        static bool Near(Vector2 p, float margin, System.Func<int, int, bool> test)
        {
            var cell = CellSize;
            var local = p + HalfExtents;
            int x0 = Mathf.FloorToInt((local.x - margin) / cell.x), x1 = Mathf.FloorToInt((local.x + margin) / cell.x);
            int y0 = Mathf.FloorToInt((local.y - margin) / cell.y), y1 = Mathf.FloorToInt((local.y + margin) / cell.y);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!test(x, y)) continue;
                // Distance from p to the cell's rectangle - a circle test, not the bounding box,
                // so a point diagonal to a corner is not refused for the corner's sake.
                float dx = Mathf.Max(x * cell.x - local.x, 0f, local.x - (x + 1) * cell.x);
                float dy = Mathf.Max(y * cell.y - local.y, 0f, local.y - (y + 1) * cell.y);
                if ((dx == 0f && dy == 0f) || dx * dx + dy * dy < margin * margin) return true;
            }
            return false;
        }

        /// <summary>The world y of the NEAR lip under <paramref name="p"/>: walking south from it
        /// down its own column of chasm cells, the bottom edge of the last one. In this 3/4 view a
        /// falling body moves down the screen, and this is the edge that swallows it. Void running
        /// to the room's edge ends at the outer wall's inner face instead.</summary>
        public static float ChasmLipBelow(Vector2 p)
        {
            // Off the floor's open edge there is no near lip: the body falls clear in front of
            // the cliff, nothing swallowing it.
            if (PastOpenEdge(p)) return NoLip;
            var cell = CellSize;
            var local = p + HalfExtents;
            int x = Mathf.FloorToInt(local.x / cell.x), y = Mathf.FloorToInt(local.y / cell.y);
            if (!VoidAt(x, y)) return p.y;
            while (y > 0 && VoidAt(x, y - 1)) y--;
            if (y == 0) return EdgesOpen ? NoLip : -HalfExtents.y + 0.3f;
            return y * cell.y - HalfExtents.y;
        }

        /// <summary>A lip far below anything drawn: nothing is cut.</summary>
        public const float NoLip = -1000f;

        /// <summary>Is there a chasm under <paramref name="p"/> or within <paramref name="margin"/>
        /// of it - the edge the player's walk stops at, and the band the enemy route keeps off.</summary>
        public static bool ChasmNear(Vector2 p, float margin = 0f)
            => _chasm != null && HasChasms && Near(p, margin, ChasmAt);

        /// <summary>Is <paramref name="p"/> over a chasm, at least <paramref name="inset"/> in from
        /// its lip - where a body FALLS. The inset is the forgiveness: a foot over the edge is not
        /// a fall.</summary>
        public static bool DeepInChasm(Vector2 p, float inset = 0f)
        {
            if (_chasm == null || !HasChasms) return false;
            var q = p + HalfExtents;
            var cell = CellSize;
            if (!ChasmAt(Mathf.FloorToInt(q.x / cell.x), Mathf.FloorToInt(q.y / cell.y))) return false;
            return inset <= 0f || !Near(p, inset, (x, y) => !ChasmAt(x, y));
        }

        /// <summary>Is <paramref name="p"/> standing room - inside the outer walls by
        /// <paramref name="margin"/>, and no interior wall or chasm within <paramref name="margin"/>
        /// of it?</summary>
        public static bool OnFloor(Vector2 p, float margin = 0f)
        {
            if (Mathf.Abs(p.x) > HalfExtents.x - margin || Mathf.Abs(p.y) > HalfExtents.y - margin) return false;
            return _wall == null || !Near(p, margin, BlockedAt);
        }

        /// <summary>Is every point of <paramref name="rect"/>, grown by <paramref name="margin"/>,
        /// clear of interior walls and chasms? (The outer bound is the caller's own business.)</summary>
        public static bool RectOnFloor(Rect rect, float margin = 0f)
        {
            // A pit (or anything laid as a rect) never cuts the dais or its face.
            if (Dais.width > 0f)
            {
                var d = Dais;
                var near = new Rect(d.xMin - margin - 0.2f, d.yMin - Tuning.Rooms.DaisFace - margin - 0.2f,
                                    d.width + 2f * margin + 0.4f, d.height + Tuning.Rooms.DaisFace + 2f * margin + 0.4f);
                if (near.Overlaps(rect)) return false;
            }
            if (_wall == null) return true;
            var cell = CellSize;
            int x0 = Mathf.FloorToInt((rect.xMin - margin + HalfExtents.x) / cell.x);
            int x1 = Mathf.CeilToInt((rect.xMax + margin + HalfExtents.x) / cell.x) - 1;
            int y0 = Mathf.FloorToInt((rect.yMin - margin + HalfExtents.y) / cell.y);
            int y1 = Mathf.CeilToInt((rect.yMax + margin + HalfExtents.y) / cell.y) - 1;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (BlockedAt(x, y)) return false;
            return true;
        }

        /// <summary>
        /// <paramref name="p"/> if it is standing room, else the nearest point that is - for
        /// anything that would otherwise put a body, a pickup or a tear inside a wall (a blink to
        /// a blade resting against one, a Rift torn at a random offset). Searches cell centres
        /// and the clamped point itself; with no shape it is just <see cref="Clamp"/>.
        /// </summary>
        public static Vector2 NearestFloor(Vector2 p, float margin = 0.5f)
        {
            p = Clamp(p, margin);
            if (OnFloor(p, margin)) return p;

            var cell = CellSize;
            Vector2 best = p;
            float bestD = float.MaxValue;
            for (int y = 0; y < _rows; y++)
            for (int x = 0; x < _cols; x++)
            {
                if (BlockedAt(x, y)) continue;
                var c = new Vector2((x + 0.5f) * cell.x, (y + 0.5f) * cell.y) - HalfExtents;
                float d = (c - p).sqrMagnitude;
                if (d >= bestD || !OnFloor(c, margin)) continue;
                best = c;
                bestD = d;
            }
            // Pull the answer back toward p along the line while it stays standing room, so the
            // result hugs the wall rather than snapping to a cell's centre.
            for (int i = 0; i < 6; i++)
            {
                var mid = Vector2.Lerp(best, p, 0.5f);
                if (OnFloor(mid, margin)) best = mid; else p = mid;
            }
            return best;
        }

        /// <summary>
        /// Does the segment a-b pass through an interior wall? What stops a SHOT (projectiles fly
        /// over columns but not over walls - Hazards.HazardQuery.CheckCrossing). Sampled at a
        /// quarter cell, which no projectile step outruns.
        /// </summary>
        public static bool WallBetween(Vector2 a, Vector2 b)
        {
            if (_wall == null) return false;
            var cell = CellSize;
            float step = Mathf.Min(cell.x, cell.y) * 0.25f;
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
            for (int i = 0; i <= n; i++)
            {
                var q = Vector2.Lerp(a, b, i / (float)n) + HalfExtents;
                if (WallAt(Mathf.FloorToInt(q.x / cell.x), Mathf.FloorToInt(q.y / cell.y))) return true;
            }
            return false;
        }
    }
}
