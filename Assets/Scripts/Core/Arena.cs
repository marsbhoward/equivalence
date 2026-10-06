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

        // ------------------------------------------------------------------ the room shape

        // Cells of the shape grid, bottom row first. Null = no shape (the open rectangle). Set and
        // cleared by Hazards.RoomWalls, which keeps its own serialized copy and hands it back here
        // after a domain reload empties this static.
        //
        // TWO KINDS OF NOT-FLOOR. A WALL stops bodies, sight and shots. A CHASM stops nothing in
        // the air - sight and shots cross it - but nothing stands on it: a body over one falls
        // (EnemyController.TickChasm, PlayerController's edge). Neither is standing room.
        static bool[] _wall, _chasm;
        static int _cols, _rows;

        /// <summary>True while the floor has a shape (walls, chasms or both).</summary>
        public static bool HasWalls => _wall != null;

        /// <summary>True while the floor has at least one chasm cell.</summary>
        public static bool HasChasms { get; private set; }

        /// <summary>Install a shape (each array cols x rows, bottom row first; chasm may be null),
        /// or clear it with null.</summary>
        public static void SetWalls(bool[] wall, bool[] chasm, int cols, int rows)
        {
            _wall = wall;
            _chasm = wall != null ? chasm : null;
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

        /// <summary>Not standing room: a wall or a chasm.</summary>
        static bool BlockedAt(int x, int y) => WallAt(x, y) || ChasmAt(x, y);

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
