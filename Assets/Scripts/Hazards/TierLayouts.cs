using System.Collections.Generic;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// TIERS - ground at more than one height in one room (the user's calls, 2026-11-13, on the
    /// floor-height mock). A tier grid lies over a room's wall grid, the same 24 x 14, north first:
    ///
    ///   0  the ground     1  2  a tier or two above it     -  a sunken tier below it
    ///   ^  STAIRS: a cell of the lower tier whose NORTH edge climbs to the tier above
    ///   g  a GATE: stairs sealed until the floor's second round (the Gates platform floor)
    ///
    /// Height is drawn, never simulated - one flat play plane, as the dais always was. Under every
    /// raised edge facing the camera a face is drawn (Tuning.Tiers.Face), and a BARRIER holds bodies
    /// along every edge between heights but the stairs (Hazards.RoomWalls.BuildTiers). Shots and
    /// sight pass over. A step is never more than one tier, so the faces stay one height.
    ///
    /// Seeded, pure: the same rng gives the same layout. Each layout here is valid by
    /// construction and checked anyway (<see cref="Validate"/>).
    /// </summary>
    public static class TierLayouts
    {
        public enum Kind { Terraces, Sunken, Overlook }

        const int Cols = RoomShape.Cols, Rows = RoomShape.Rows;

        /// <summary>The dais (RoomShape.Dais) as a tier grid: three rows before the door, steps
        /// before it.</summary>
        public static string[] Dais()
        {
            var g = Blank('0');
            Fill(g, RoomShape.DaisCol0, 0, RoomShape.DaisCols, RoomShape.DaisRows, '1');
            for (int c = RoomShape.DaisStepCol0; c < RoomShape.DaisStepCol0 + RoomShape.DaisSteps; c++)
                g[RoomShape.DaisRows][c] = '^';
            return Rows_(g);
        }

        /// <summary>A tiered room's layout, mirrored east-west at random.</summary>
        public static string[] Make(Kind kind, System.Random rng, bool gates = false)
        {
            var g = kind switch
            {
                Kind.Terraces => Terraces(rng, gates),
                Kind.Sunken => Sunken(rng),
                _ => Overlook(rng),
            };
            if (rng.Next(2) == 1)
                foreach (var row in g) System.Array.Reverse(row);
            return Rows_(g);
        }

        /// <summary>Levels stepping down toward the camera: a top tier before the door, a middle
        /// one, the ground. Wide stairs up the middle, a pair at the sides lower down.</summary>
        static char[][] Terraces(System.Random rng, bool gates)
        {
            var g = Blank('0');
            bool three = gates || rng.NextDouble() < 0.7;
            int top = 3, mid = three ? top + 2 + rng.Next(2) : top;
            char stair = gates ? 'g' : '^';
            if (three)
            {
                Fill(g, 0, 0, Cols, top, '2');
                Fill(g, 0, top, Cols, mid - top, '1');
                // The wide flight up the middle, under the door.
                int w = 4;
                for (int c = Cols / 2 - w / 2; c < Cols / 2 + w / 2; c++) g[top][c] = stair;
            }
            else Fill(g, 0, 0, Cols, mid, '1');
            // A flight each side from the ground, symmetric.
            int off = 2 + rng.Next(4);
            foreach (int c0 in new[] { off, Cols - off - 2 })
                for (int c = c0; c < c0 + 2; c++) g[mid][c] = stair;
            return g;
        }

        /// <summary>The middle of the room a level lower, entered by stairs on its north side;
        /// sometimes a plinth standing in it, with its own step.</summary>
        static char[][] Sunken(System.Random rng)
        {
            var g = Blank('0');
            int r0 = 3 + rng.Next(2), r1 = 9 + rng.Next(2);          // inclusive rows
            int c0 = 3 + rng.Next(3), c1 = Cols - 1 - (3 + rng.Next(3));
            Fill(g, c0, r0, c1 - c0 + 1, r1 - r0 + 1, '-');
            int sa = c0 + 2 + rng.Next(2), sb = c1 - 3 - rng.Next(2);
            foreach (int s in new[] { sa, sb }) { g[r0][s] = '^'; g[r0][s + 1] = '^'; }
            if (rng.NextDouble() < 0.6)
            {
                int pr = (r0 + r1) / 2;
                Fill(g, Cols / 2 - 1, pr - 1, 2, 2, '0');
                g[pr + 1][Cols / 2 - 1] = '^';
                g[pr + 1][Cols / 2] = '^';
            }
            return g;
        }

        /// <summary>Ledges along the side walls where the shooters stand: a long one on one side
        /// (sometimes with a second step on top), a short one on the other.</summary>
        static char[][] Overlook(System.Random rng)
        {
            var g = Blank('0');
            int w = 5 + rng.Next(2), len = 8 + rng.Next(3);
            Fill(g, 0, 0, w, len, '1');
            g[len][2] = '^'; g[len][3] = '^';
            if (rng.NextDouble() < 0.55)
            {
                Fill(g, 0, 0, 3, 4, '2');
                g[4][1] = '^';
                g[4][2] = '^';
            }
            if (rng.NextDouble() < 0.75)
            {
                int ew = 5 + rng.Next(2), elen = 4 + rng.Next(2);
                Fill(g, Cols - ew, 0, ew, elen, '1');
                g[elen][Cols - 4] = '^'; g[elen][Cols - 3] = '^';
            }
            return g;
        }

        // ------------------------------------------------------------------ reading a grid

        /// <summary>Each cell's height, bottom row first (world order), and which cells are
        /// stairs (a gate counts while <paramref name="gatesOpen"/>).</summary>
        public static void Read(string[] tiers, bool gatesOpen, out sbyte[] height, out bool[] stair, out bool[] gate)
        {
            height = new sbyte[Cols * Rows];
            stair = new bool[Cols * Rows];
            gate = new bool[Cols * Rows];
            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                int y = Rows - 1 - r;
                height[y * Cols + c] = (sbyte)HeightAt(tiers, r, c);
                char ch = tiers[r][c];
                stair[y * Cols + c] = ch == '^' || (ch == 'g' && gatesOpen);
                gate[y * Cols + c] = ch == 'g';
            }
        }

        static int HeightAt(string[] g, int r, int c)
        {
            char ch = g[r][c];
            if (ch == '^' || ch == 'g') return r > 0 ? HeightAt(g, r - 1, c) - 1 : 0;
            if (ch == '-') return -1;
            if (ch >= '1' && ch <= '3') return ch - '0';
            return 0;
        }

        /// <summary>
        /// Null when <paramref name="tiers"/> suits the wall grid <paramref name="walls"/>, else what
        /// is wrong: every step one tier, every stair climbing one, and every floor cell reachable
        /// from the arrival point on foot BY THE STAIRS (gates counted open - the fight is over by
        /// the time anything needs the far side of one).
        /// </summary>
        public static string Validate(string[] tiers, string[] walls)
        {
            if (tiers == null) return null;
            if (tiers.Length != Rows) return "tier rows";
            Read(tiers, true, out var h, out var stair, out _);
            bool Floor(int x, int y) => x >= 0 && y >= 0 && x < Cols && y < Rows && walls[Rows - 1 - y][x] == '.';
            for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Cols; x++)
            {
                int k = y * Cols + x;
                if (stair[k] && (y + 1 >= Rows || h[k + Cols] != h[k] + 1)) return $"stairs at {x},{y} climb nothing";
                if (x + 1 < Cols && System.Math.Abs(h[k] - h[k + 1]) > 1) return $"a double step at {x},{y}";
                if (y + 1 < Rows && System.Math.Abs(h[k] - h[k + Cols]) > 1) return $"a double step at {x},{y}";
            }
            var seen = new bool[Cols * Rows];
            var stack = new Stack<int>();
            int start = 1 * Cols + Cols / 2;
            if (!Floor(Cols / 2, 1)) return "arrival walled";
            seen[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                int k = stack.Pop(), x = k % Cols, y = k / Cols;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (!Floor(nx, ny)) continue;
                    int n = ny * Cols + nx;
                    if (seen[n]) continue;
                    bool ok = h[n] == h[k]
                           || (dy == 1 && stair[k] && h[n] == h[k] + 1)       // up a flight
                           || (dy == -1 && stair[n] && h[k] == h[n] + 1);     // down one
                    if (!ok) continue;
                    seen[n] = true;
                    stack.Push(n);
                }
            }
            for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Cols; x++)
                if (Floor(x, y) && !seen[y * Cols + x]) return $"tier cell {x},{y} unreachable";
            return null;
        }

        // ------------------------------------------------------------------ helpers

        static char[][] Blank(char ch)
        {
            var g = new char[Rows][];
            for (int r = 0; r < Rows; r++) { g[r] = new char[Cols]; for (int c = 0; c < Cols; c++) g[r][c] = ch; }
            return g;
        }

        static void Fill(char[][] g, int c0, int r0, int w, int h, char ch)
        {
            for (int r = r0; r < r0 + h && r < Rows; r++)
            for (int c = c0; c < c0 + w && c < Cols; c++)
                if (r >= 0 && c >= 0) g[r][c] = ch;
        }

        static string[] Rows_(char[][] g)
        {
            var rows = new string[Rows];
            for (int r = 0; r < Rows; r++) rows[r] = new string(g[r]);
            return rows;
        }
    }
}
