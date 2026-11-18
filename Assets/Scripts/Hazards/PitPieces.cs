using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>Which hazard fills a pit. Append only.</summary>
    public enum PitKind { Fire, Sand, Spike, Water }

    /// <summary>
    /// FLOOR PITS AS PIECES (the user's calls, 2026-11-18, on the "Pit Pieces" mock; WORLD.md
    /// "Pit pieces"). A floor's pits are drawn from a LIBRARY of small pictures in the room shapes'
    /// own alphabet, turned to any of eight orientations, sometimes laid as an east-west twin, and
    /// added until the floor's budget is spent - so the randomness stays, and every pit sits where
    /// the room gives it a reason: a trough at the foot of its own wall, a spill under its crack,
    /// a gauntlet in a gap, a lip along a chasm.
    ///
    ///   .  clearance (must land on open floor)     p  pit cell     w  edge water (spills to the lake)
    ///   #  wall   %  cracked wall   ~  chasm        s  island (chasm stones: floor a dash reaches)
    ///
    /// LAID INTO THE ROOM AS DRAWN, BEFORE ITS WALLS GO UP. The room (RoomShape.For, a platform's,
    /// or the open floor) is never redrawn: the pieces go into it, and a piece that carries a wall,
    /// cracked wall or chasm hands back the room with it (<see cref="Plan.Shape"/>). Pit cells are
    /// not the room's - HazardBuilder spawns them from <see cref="Plan.Pits"/>.
    ///
    /// The rules every piece is laid by (each checked as it goes in; only that piece is taken back):
    ///   - the room's own: door and arrival zones open, every floor cell reached on foot (pits are
    ///     floor - you walk through them), nothing narrower than two cells;
    ///   - a DRY route from the arrival point to the door;
    ///   - tiers: a pit on one height, never on or beside stairs or a gate, moves by the stairs only;
    ///   - a bridge floor is checked with its bridge raised, its landings kept clear; a LIFT has none;
    ///   - EDGE WATER (inlet, creek, the ford) only where the floor rolled water and the place's
    ///     void is water (forest, salt), weighted up there;
    ///   - CHASM STONES only against a mostly ranged wave (the user's call: otherwise a bow on the
    ///     islands is never answered). Islands are exempt from the on-foot check.
    ///
    /// Pure and seeded (FloorPlanner.PitSeed), System.Random: a restart meets the same pits.
    ///   unity command eval --code 'return Convergence.Hazards.PitPieces.Sample(8, 1);'
    ///   unity command eval --code 'return Convergence.Hazards.PitPieces.Measure(400);'
    /// </summary>
    public static class PitPieces
    {
        const int Cols = RoomShape.Cols, Rows = RoomShape.Rows;

        /// <summary>What the floor is, for the pieces.</summary>
        public struct Context
        {
            public int Floor, Seed;
            /// <summary>Ranged, Turret and Mortar hold most of the wave (Enemies.Wave.RangedShare).</summary>
            public bool RangedWave;
            /// <summary>The place's void is water (Biome.EdgeIsWater) - edge water may be laid.</summary>
            public bool EdgeWater;
            public bool Lift, Bridge;
        }

        /// <summary>One pit to spawn: its kind and cells (x column, y row north first).</summary>
        public sealed class Planned
        {
            public PitKind Kind;
            public string Name;
            public bool Spills;
            public readonly List<Vector2Int> Cells = new();
        }

        /// <summary>A floor's pits, and the room to build if a piece changed it.</summary>
        public sealed class Plan
        {
            /// <summary>The room to build - the one passed in unless a piece carried walls, a chasm
            /// or islands (then a new one; an open floor may gain a shape this way).</summary>
            public RoomShape Shape;
            public readonly List<Planned> Pits = new();
            public int Cells;
            public string Describe = "";
        }

        enum Tag { Free, Attached, Shore, Edge, Ford, Stones }

        /// <summary>One-shot (dev): the next floor's pits are all this kind.</summary>
        public static PitKind? DevForceKind;
        /// <summary>One-shot (dev): the next floor tries this piece first ("inlet", "trough", ...).</summary>
        public static string DevForcePiece;
        /// <summary>One-shot (dev): the next floor counts as a mostly ranged wave, and lays the
        /// chasm stones if they fit.</summary>
        public static bool DevForceStones;

        readonly struct Piece
        {
            public readonly string Name; public readonly Tag Tag; public readonly float Weight; public readonly string[] Grid;
            public Piece(string name, Tag tag, float weight, params string[] grid) { Name = name; Tag = tag; Weight = weight; Grid = grid; }
        }

        // ------------------------------------------------------------------ the library (drafts)

        static readonly Piece[] Library =
        {
            // Free shapes: laid on open floor anywhere they fit, a dry ring round each.
            new("pool", Tag.Free, 1f, ".....", ".ppp.", ".ppp.", ".ppp.", "....."),
            new("long pool", Tag.Free, 1f, ".......", ".ppppp.", ".ppppp.", "......."),
            new("kidney", Tag.Free, 1f, "......", ".ppp..", ".pppp.", "..ppp.", "......"),
            new("ell", Tag.Free, 0.9f, "......", ".pp...", ".pp...", ".pppp.", ".pppp.", "......"),
            new("crescent", Tag.Free, 0.9f, "......", "..ppp.", ".pp...", ".pp...", "..ppp.", "......"),
            new("ring", Tag.Free, 0.6f, "........", ".pppppp.", ".pp..pp.", ".pp..pp.", ".pppppp.", "........"),
            new("diagonal run", Tag.Free, 0.8f, ".........", ".ppp.....", "..ppp....", "...ppp...", "....ppp..", ".....ppp.", "........."),
            new("dumbbell", Tag.Free, 0.8f, "........", ".pp..pp.", ".pppppp.", ".pp..pp.", "........"),
            new("rosette", Tag.Free, 0.8f, "......", "..pp..", ".pppp.", ".pppp.", "..pp..", "......"),
            new("diamond", Tag.Free, 0.8f, ".......", "...p...", "..ppp..", ".ppppp.", "..ppp..", "...p...", "......."),
            new("scatter", Tag.Free, 0.9f, ".......", ".pp....", ".pp.pp.", "....pp.", ".pp....", ".pp....", "......."),
            new("stepping stones", Tag.Free, 0.6f, "..........", ".pppppppp.", ".pp..pppp.", ".pppp..pp.", ".pppppppp.", ".........."),
            // Shapes that bring their own wall: the hazard where the wall gives it a reason.
            new("trough", Tag.Attached, 1.2f, "........", ".######.", ".pppppp.", "........"),
            new("corner trough", Tag.Attached, 1f, "......", ".####.", ".#ppp.", ".#pp..", ".#....", "......"),
            new("moat", Tag.Attached, 1f, "......", ".pppp.", ".p##p.", ".p##p.", ".p..p.", "......"),
            new("spill", Tag.Attached, 1f, "........", ".##%%##.", "...pp...", "..pppp..", "...pp...", "........"),
            new("gauntlet", Tag.Attached, 0.9f, "..........", ".###pp###.", ".###pp###.", ".........."),
            new("basin", Tag.Attached, 0.8f, ".......", ".#...#.", "..ppp..", "..ppp..", ".#...#.", "......."),
            new("tee pools", Tag.Attached, 0.8f, ".......", ".#####.", ".pp#pp.", ".pp#pp.", "......."),
            // By a chasm: the hazard on the lip where enemies are thrown off.
            new("lip", Tag.Shore, 1f, "........", ".pppppp.", ".~~~~~~.", ".~~~~~~.", "........"),
            new("ledge pool", Tag.Shore, 0.8f, "......", ".ppp..", ".~~pp.", ".~~~p.", ".~~~~.", "......"),
            // Edge water, drawn facing SOUTH (its last row on the room's edge) and turned to face
            // whichever open side it is laid against.
            new("inlet", Tag.Edge, 1f, "........", "...ww...", "..wwww..", ".wwwwww.", ".wwwwww."),
            new("creek", Tag.Edge, 1f, ".......", ".www...", ".www...", "..ww...", "...ww..", "...ww.."),
            new("ford", Tag.Ford, 0.7f),
        };

        /// <summary>Islands two cells apart in a chasm - a dash between each. Not a pit: the room's.</summary>
        static readonly string[] Stones =
            { "..........", ".~~~~~~~~.", ".~ss~~ss~.", ".~ss~~ss~.", ".~~~~~~~~.", ".........." };

        // ------------------------------------------------------------------ laying a floor

        /// <summary>The pits for a floor whose room is <paramref name="shape"/> (null = the open
        /// floor), and that room with whatever the pieces brought.</summary>
        public static Plan Lay(RoomShape shape, Context ctx)
        {
            var plan = new Plan { Shape = shape };
            var forceKind = DevForceKind; var forcePiece = DevForcePiece; bool forceStones = DevForceStones;
            DevForceKind = null; DevForcePiece = null; DevForceStones = false;
            if (ctx.Lift) { plan.Describe = "lift: none"; return plan; }   // the lift is round one's whole room

            var rng = new System.Random(unchecked(ctx.Seed * 7919 + 13));
            var grid = new char[Rows][];
            for (int r = 0; r < Rows; r++) grid[r] = (shape != null ? shape.Grid[r] : new string('.', Cols)).ToCharArray();
            var start = Copy(grid);
            var room = new Room(shape?.Tiers, ctx.Bridge);
            var names = new List<string>();

            // CHASM STONES first, before any pit: they are the room's, and only against a ranged
            // wave. Never on a tiered room or a platform.
            if ((ctx.RangedWave || forceStones) && shape?.Tiers == null && !ctx.Bridge && (rng.NextDouble() < Tuning.Hazards.StonesChance || forceStones)
                && TryLay(grid, room, Orient(Stones, rng.Next(8)), rng, false, null)) names.Add("chasm stones");

            if (rng.NextDouble() < Tuning.Hazards.PitsFloorChance || forceKind != null || forcePiece != null)
            {
                bool mixed = RunFormat.Depth(ctx.Floor) >= Tuning.Hazards.PitMixedKindsFromFloor;
                var floorKind = (PitKind)rng.Next(4);
                int budget = rng.Next(Tuning.Hazards.PitCellsMin, Tuning.Hazards.PitCellsMax + 1);
                int cells = 0;
                var pool = new List<Piece>();
                for (int tries = 0; cells < budget && tries < Tuning.Hazards.PitTries; tries++)
                {
                    var kind = forceKind ?? (mixed ? (PitKind)rng.Next(4) : floorKind);
                    bool edgeOk = kind == PitKind.Water && ctx.EdgeWater && room.Heights == null;
                    pool.Clear();
                    foreach (var p in Library) if ((p.Tag != Tag.Edge && p.Tag != Tag.Ford) || edgeOk) pool.Add(p);
                    var pc = Pick(rng, pool, edgeOk);
                    if (forcePiece != null && tries == 0)
                        foreach (var p in Library) if (p.Name == forcePiece) { pc = p; if (p.Tag == Tag.Edge || p.Tag == Tag.Ford) kind = PitKind.Water; }

                    if (pc.Tag == Tag.Ford)
                    {
                        // The ford is the floor's whole budget: drawn first, or not at all.
                        if (cells > 0) continue;
                        var ford = TryFord(grid, room, rng);
                        if (ford == null) continue;
                        Add(plan, kind, "ford", true, ford);
                        cells += ford.Count;
                        break;
                    }
                    if (pc.Tag == Tag.Edge)
                    {
                        int n = Count(pc.Grid);
                        if (cells + n > budget + Tuning.Hazards.PitOvershoot) continue;
                        var laid = TryEdge(grid, room, pc.Grid, rng, out string side);
                        if (laid == null) continue;
                        Add(plan, kind, $"{pc.Name} ({side})", true, laid);
                        cells += laid.Count;
                        continue;
                    }
                    bool twin = pc.Tag == Tag.Free && rng.NextDouble() < Tuning.Hazards.PitTwinChance;
                    var piece = Orient(pc.Grid, rng.Next(8));
                    int count = Count(piece) * (twin ? 2 : 1);
                    if (cells + count > budget + Tuning.Hazards.PitOvershoot) continue;
                    var made = new List<List<Vector2Int>>();
                    if (!TryLay(grid, room, piece, rng, twin, made)) continue;
                    foreach (var m in made) Add(plan, kind, pc.Name + (twin ? " (twin)" : ""), false, m);
                    cells += count;
                }
                plan.Cells = cells;
            }

            // The room keeps its walls, chasms and islands; the pits are not the room's.
            bool changed = false;
            var rows = new string[Rows];
            for (int r = 0; r < Rows; r++)
            {
                var row = new char[Cols];
                for (int c = 0; c < Cols; c++)
                {
                    char ch = grid[r][c];
                    row[c] = ch == 'p' || ch == 'w' ? '.' : ch;
                    if (row[c] != start[r][c]) changed = true;
                }
                rows[r] = new string(row);
            }
            if (changed)
            {
                var styleRng = new System.Random(unchecked(ctx.Seed * 31 + 5));
                plan.Shape = shape != null ? shape.Reshaped(rows, styleRng) : RoomShape.Opened(rows, styleRng);
            }
            foreach (var p in plan.Pits) names.Add($"{p.Name}:{p.Kind}");
            plan.Describe = names.Count > 0 ? string.Join(", ", names) : "none";
            return plan;
        }

        static void Add(Plan plan, PitKind kind, string name, bool spills, List<Vector2Int> cells)
        {
            var p = new Planned { Kind = kind, Name = name, Spills = spills };
            p.Cells.AddRange(cells);
            plan.Pits.Add(p);
        }

        static Piece Pick(System.Random rng, List<Piece> pool, bool edgeOk)
        {
            float W(Piece p) => p.Tag == Tag.Edge || p.Tag == Tag.Ford ? p.Weight * Tuning.Hazards.EdgeWaterWeight : p.Weight;
            float total = 0f;
            foreach (var p in pool) total += W(p);
            double roll = rng.NextDouble() * total;
            foreach (var p in pool) if ((roll -= W(p)) <= 0) return p;
            return pool[pool.Count - 1];
        }

        static int Count(string[] g)
        {
            int n = 0;
            foreach (var row in g) foreach (char c in row) if (c == 'p' || c == 'w') n++;
            return n;
        }

        // ------------------------------------------------------------------ the room being laid into

        /// <summary>The room's heights and stairs (north first), and what pits keep off.</summary>
        sealed class Room
        {
            public readonly int[,] Heights;
            public readonly bool[,] Stairs;
            public readonly bool Bridge;

            public Room(string[] tiers, bool bridge)
            {
                Bridge = bridge;
                if (tiers == null) return;
                Heights = new int[Rows, Cols];
                Stairs = new bool[Rows, Cols];
                for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    Heights[r, c] = HeightAt(tiers, r, c);
                    Stairs[r, c] = tiers[r][c] == '^' || tiers[r][c] == 'g';
                }
            }

            static int HeightAt(string[] t, int r, int c)
            {
                char ch = t[r][c];
                if (ch == '^' || ch == 'g') return r > 0 ? HeightAt(t, r - 1, c) - 1 : 0;
                if (ch == '-') return -1;
                return ch >= '1' && ch <= '3' ? ch - '0' : 0;
            }

            public int H(int r, int c) => Heights != null ? Heights[r, c] : 0;
            public bool Stair(int r, int c) => Stairs != null && Stairs[r, c];

            /// <summary>The door's and the arrival's zones a cell wider (RoomShape.InZone), and a
            /// bridge's landings.</summary>
            public bool Keep(int r, int c)
            {
                if (c >= Cols / 2 - 3 && c < Cols / 2 + 3 && (r < 5 || r >= Rows - 5)) return true;
                return Bridge && r >= RoomShape.BridgeGapRow0 - 2 && r < RoomShape.BridgeGapRow0 + RoomShape.BridgeGapRows + 2
                       && c >= RoomShape.BridgeCol0 - 1 && c < RoomShape.BridgeCol0 + RoomShape.BridgeCols + 1;
            }

            /// <summary>A cell the raised bridge will be - floor to every check.</summary>
            public bool BridgeCell(int r, int c)
                => Bridge && r >= RoomShape.BridgeGapRow0 && r < RoomShape.BridgeGapRow0 + RoomShape.BridgeGapRows
                   && c >= RoomShape.BridgeCol0 && c < RoomShape.BridgeCol0 + RoomShape.BridgeCols;
        }

        // ------------------------------------------------------------------ placing

        static char[][] Copy(char[][] g)
        {
            var c = new char[g.Length][];
            for (int i = 0; i < g.Length; i++) c[i] = (char[])g[i].Clone();
            return c;
        }

        static void Restore(char[][] grid, char[][] from) { for (int i = 0; i < grid.Length; i++) grid[i] = from[i]; }

        static string[] Mirror(string[] g, bool eastWest, bool northSouth)
        {
            var o = new string[g.Length];
            for (int r = 0; r < g.Length; r++)
            {
                var row = g[northSouth ? g.Length - 1 - r : r];
                if (eastWest) { var ch = row.ToCharArray(); System.Array.Reverse(ch); row = new string(ch); }
                o[r] = row;
            }
            return o;
        }

        /// <summary>One of eight orientations, as the stamps are: o &amp; 4 transposes, o &amp; 1
        /// mirrors east-west, o &amp; 2 north-south.</summary>
        static string[] Orient(string[] g, int o)
        {
            var t = g;
            if ((o & 4) != 0)
            {
                t = new string[g[0].Length];
                for (int c = 0; c < g[0].Length; c++)
                {
                    var row = new char[g.Length];
                    for (int r = 0; r < g.Length; r++) row[r] = g[r][c];
                    t[c] = new string(row);
                }
            }
            return Mirror(t, (o & 1) != 0, (o & 2) != 0);
        }

        static bool Fits(char[][] grid, Room room, string[] s, int r0, int c0)
        {
            int h0 = int.MinValue;
            for (int r = 0; r < s.Length; r++)
            for (int c = 0; c < s[r].Length; c++)
            {
                char ch = s[r][c];
                if (ch == ' ') continue;
                int R = r0 + r, C = c0 + c;
                if (R < 0 || C < 0 || R >= Rows || C >= Cols || grid[R][C] != '.' || room.BridgeCell(R, C)) return false;
                if (ch == '.') continue;
                if (room.Keep(R, C) || room.Stair(R, C)) return false;
                if (room.Heights != null)
                {
                    // On a tiered room the whole piece stands on one height (its walls too), no
                    // chasm is cut into a tier, and no pit lies on or beside stairs.
                    if (ch == '~' || ch == 's') return false;
                    int h = room.H(R, C);
                    if (h0 == int.MinValue) h0 = h; else if (h != h0) return false;
                    if ((ch == 'p' || ch == 'w')
                        && ((R > 0 && room.Stair(R - 1, C)) || (R < Rows - 1 && room.Stair(R + 1, C))
                            || (C > 0 && room.Stair(R, C - 1)) || (C < Cols - 1 && room.Stair(R, C + 1)))) return false;
                }
            }
            return true;
        }

        /// <summary>Lays the piece (and its east-west twin at the mirrored spot) and keeps it only
        /// if the room still validates. <paramref name="made"/> gets each copy's pit cells.</summary>
        static bool TryLay(char[][] grid, Room room, string[] s, System.Random rng, bool twin, List<List<Vector2Int>> made)
        {
            int h = s.Length, w = s[0].Length;
            var tw = twin ? Mirror(s, true, false) : null;
            var spots = new List<Vector2Int>();
            for (int r = 0; r + h <= Rows; r++)
            for (int c = 0; c + w <= Cols; c++)
            {
                if (!Fits(grid, room, s, r, c)) continue;
                if (twin)
                {
                    int tc = Cols - c - w;
                    if (tc < c + w && c < tc + w) continue;
                    if (!Fits(grid, room, tw, r, tc)) continue;
                }
                spots.Add(new Vector2Int(c, r));
            }
            // A few spots before giving up: one hard against an edge can leave a one-cell corridor,
            // which the check refuses, while the next spot along is fine.
            for (int attempt = 0; attempt < Tuning.Hazards.PitSpotTries && spots.Count > 0; attempt++)
            {
                int k = rng.Next(spots.Count);
                var at = spots[k];
                spots.RemoveAt(k);
                var before = Copy(grid);
                var a = Lay(grid, s, at.y, at.x);
                var b = twin ? Lay(grid, tw, at.y, Cols - at.x - w) : null;
                if (!Validate(grid, room)) { Restore(grid, before); continue; }
                if (made != null) { if (a.Count > 0) made.Add(a); if (b != null && b.Count > 0) made.Add(b); }
                return true;
            }
            return false;
        }

        static List<Vector2Int> Lay(char[][] grid, string[] s, int r0, int c0)
        {
            var pits = new List<Vector2Int>();
            for (int r = 0; r < s.Length; r++)
            for (int c = 0; c < s[r].Length; c++)
            {
                char ch = s[r][c];
                if (ch == ' ') continue;
                grid[r0 + r][c0 + c] = ch;
                if (ch == 'p' || ch == 'w') pits.Add(new Vector2Int(c0 + c, r0 + r));
            }
            return pits;
        }

        /// <summary>An edge piece against an open side - south, west or east, the piece turned to
        /// face it - or null.</summary>
        static List<Vector2Int> TryEdge(char[][] grid, Room room, string[] g, System.Random rng, out string side)
        {
            string[] sides = { "S", "W", "E" };
            for (int i = sides.Length - 1; i > 0; i--) { int k = rng.Next(i + 1); (sides[i], sides[k]) = (sides[k], sides[i]); }
            foreach (var sd in sides)
            {
                // South as drawn (or mirrored); east transposed so the edge row is the last column;
                // west transposed and mirrored so it is the first.
                int o = sd == "S" ? rng.Next(2) : sd == "E" ? 4 | (rng.Next(2) << 1) : 5 | (rng.Next(2) << 1);
                var s = Orient(g, o);
                int h = s.Length, w = s[0].Length;
                var spots = new List<Vector2Int>();
                for (int r = 0; r + h <= Rows; r++)
                for (int c = 0; c + w <= Cols; c++)
                {
                    if (sd == "S" && r != Rows - h) continue;
                    if (sd == "E" && c != Cols - w) continue;
                    if (sd == "W" && c != 0) continue;
                    if (Fits(grid, room, s, r, c)) spots.Add(new Vector2Int(c, r));
                }
                while (spots.Count > 0)
                {
                    int k = rng.Next(spots.Count);
                    var at = spots[k];
                    spots.RemoveAt(k);
                    var before = Copy(grid);
                    var laid = Lay(grid, s, at.y, at.x);
                    if (Validate(grid, room)) { side = sd == "S" ? "south" : sd == "E" ? "east" : "west"; return laid; }
                    Restore(grid, before);
                }
            }
            side = null;
            return null;
        }

        /// <summary>THE FORD: a stream two cells wide from the west edge to the east, waving a row,
        /// with a dry ford two cells wide. Generated, not drawn. Never across a tier edge.</summary>
        static List<Vector2Int> TryFord(char[][] grid, Room room, System.Random rng)
        {
            if (room.Heights != null) return null;
            for (int t = 0; t < 6; t++)
            {
                int baseRow = 5 + rng.Next(2), gap = 3 + rng.Next(Cols - 7);
                double ph = rng.NextDouble() * 6.28;
                var cells = new List<Vector2Int>();
                bool ok = true;
                for (int c = 0; c < Cols && ok; c++)
                {
                    if (c == gap || c == gap + 1) continue;
                    int y = baseRow + (System.Math.Sin(c * 0.45 + ph) > 0.3 ? 1 : 0);
                    for (int r = y; r <= y + 1; r++)
                    {
                        if (grid[r][c] != '.' || room.Keep(r, c) || room.BridgeCell(r, c)) ok = false;
                        cells.Add(new Vector2Int(c, r));
                    }
                }
                if (!ok) continue;
                var before = Copy(grid);
                foreach (var c in cells) grid[c.y][c.x] = 'w';
                if (Validate(grid, room)) return cells;
                Restore(grid, before);
            }
            return null;
        }

        // ------------------------------------------------------------------ the check

        /// <summary>
        /// The room's own rules (RoomShape.Validate) with pit cells counted as floor and tier moves
        /// by the stairs only, plus a DRY route from the arrival point to the door. Islands are
        /// exempt from the on-foot check; a bridge floor is checked with its bridge raised.
        /// </summary>
        static bool Validate(char[][] g, Room room)
        {
            bool Ok(int r, int c) => r >= 0 && c >= 0 && r < Rows && c < Cols;
            bool Walk(int r, int c) => Ok(r, c) && (g[r][c] == '.' || g[r][c] == 'p' || g[r][c] == 'w' || room.BridgeCell(r, c));
            bool Dry(int r, int c) => Ok(r, c) && (g[r][c] == '.' || room.BridgeCell(r, c));
            for (int r = 0; r < 4; r++)
            for (int c = Cols / 2 - 2; c < Cols / 2 + 2; c++)
                if (!Dry(r, c) || !Dry(Rows - 1 - r, c)) return false;

            bool Step(int r, int c, int nr, int nc)
                => room.H(r, c) == room.H(nr, nc)
                || (nr == r - 1 && room.Stair(r, c) && room.H(nr, nc) == room.H(r, c) + 1)
                || (nr == r + 1 && room.Stair(nr, nc) && room.H(r, c) == room.H(nr, nc) + 1);

            bool[,] Flood(System.Func<int, int, bool> test)
            {
                var seen = new bool[Rows, Cols];
                var stack = new Stack<Vector2Int>();
                seen[Rows - 2, Cols / 2] = true;
                stack.Push(new Vector2Int(Cols / 2, Rows - 2));
                while (stack.Count > 0)
                {
                    var p = stack.Pop();
                    foreach (var d in Dirs)
                    {
                        int nr = p.y + d.y, nc = p.x + d.x;
                        if (!test(nr, nc) || seen[nr, nc] || !Step(p.y, p.x, nr, nc)) continue;
                        seen[nr, nc] = true;
                        stack.Push(new Vector2Int(nc, nr));
                    }
                }
                return seen;
            }

            var reach = Flood(Walk);
            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                if (!Walk(r, c)) continue;
                if (!reach[r, c]) return false;
                bool wide = false;
                for (int r0 = r - 1; r0 <= r && !wide; r0++)
                for (int c0 = c - 1; c0 <= c && !wide; c0++)
                    wide = Walk(r0, c0) && Walk(r0 + 1, c0) && Walk(r0, c0 + 1) && Walk(r0 + 1, c0 + 1);
                if (!wide) return false;
            }
            return Flood(Dry)[1, Cols / 2];
        }

        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        // ------------------------------------------------------------------ dev

        /// <summary>
        /// <paramref name="count"/> floors from consecutive seeds as text - the room as drawn with
        /// its pits (p, w, s for islands) - for judging the library without playing.
        ///   unity command eval --code 'return Convergence.Hazards.PitPieces.Sample(8, 1);'
        /// </summary>
        public static string Sample(int count, int firstSeed, bool edgeWater = true, bool ranged = false)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                int seed = firstSeed + i;
                int floor = 6 + (seed * 7) % 40;
                var shape = RoomShape.For(floor, seed * 101, seed * 101 - 1);
                var plan = Lay(shape, new Context { Floor = floor, Seed = seed, EdgeWater = edgeWater, RangedWave = ranged });
                sb.AppendLine($"seed {seed} floor {floor}: {plan.Shape?.Describe ?? "open"} | {plan.Cells} cells | {plan.Describe}");
                var g = new char[Rows][];
                for (int r = 0; r < Rows; r++) g[r] = (plan.Shape != null ? plan.Shape.Grid[r] : new string('.', Cols)).ToCharArray();
                foreach (var p in plan.Pits) foreach (var c in p.Cells) g[c.y][c.x] = p.Spills ? 'w' : 'p';
                foreach (var row in g) sb.AppendLine(new string(row));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// Rolls <paramref name="count"/> floors per kind of room and reports pit cells (median,
        /// range), how often no pit fitted, and how often each piece and the chasm stones were laid.
        ///   unity command eval --code 'return Convergence.Hazards.PitPieces.Measure(400);'
        /// </summary>
        public static string Measure(int count)
        {
            var sb = new StringBuilder();
            var cases = new (string Name, System.Func<int, RoomShape> Room, bool Bridge, bool Lift)[]
            {
                ("as drawn", s => RoomShape.For(6 + s % 40, s * 101, s * 101 - 1), false, false),
                ("tiered", s => RoomShape.Tiered(new System.Random(s)), false, false),
                ("bridge", s => RoomShape.Bridge(), true, false),
                ("gates", s => RoomShape.Gates(new System.Random(s)), false, false),
            };
            foreach (var cs in cases)
            {
                var cells = new List<int>();
                var pieces = new Dictionary<string, int>();
                int empty = 0, stones = 0, reshaped = 0;
                for (int s = 1; s <= count; s++)
                {
                    var shape = cs.Room(s);
                    var plan = Lay(shape, new Context { Floor = 6 + s % 40, Seed = s, EdgeWater = s % 2 == 0, RangedWave = s % 4 == 0, Bridge = cs.Bridge });
                    if (plan.Pits.Count == 0) empty++;
                    else cells.Add(plan.Cells);
                    if (plan.Describe.Contains("chasm stones")) stones++;
                    if (plan.Shape != shape) reshaped++;
                    foreach (var p in plan.Pits)
                    {
                        string k = p.Name.Replace(" (twin)", "");
                        int paren = k.IndexOf(" (");
                        if (paren > 0) k = k.Substring(0, paren);
                        pieces[k] = pieces.TryGetValue(k, out int v) ? v + 1 : 1;
                    }
                }
                cells.Sort();
                string med = cells.Count > 0 ? $"{cells[cells.Count / 2]} (range {cells[0]}-{cells[cells.Count - 1]})" : "-";
                sb.AppendLine($"{cs.Name}: {count} floors, no pits {empty} (incl. the {(1f - Tuning.Hazards.PitsFloorChance):P0} roll), cells median {med}, stones {stones}, room changed {reshaped}");
                var list = new List<KeyValuePair<string, int>>(pieces);
                list.Sort((a, b) => b.Value.CompareTo(a.Value));
                sb.Append("   ");
                foreach (var kv in list) sb.Append($"{kv.Key} {kv.Value}, ");
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
