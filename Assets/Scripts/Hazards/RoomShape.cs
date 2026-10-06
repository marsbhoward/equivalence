using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// A floor's SHAPE: interior walls carving the arena rectangle into a plaza, a hall, three
    /// chambers. The crowd is a positioning problem, and geometry changes positioning more than
    /// anything else a floor can roll - a pack funnelled through a doorway is a different fight
    /// from the same pack in the open.
    ///
    /// Shapes are PICTURES IN SOURCE, the gear grids' convention: 24 columns by 14 rows, one cell
    /// per world unit, north at the top.
    ///
    ///   .  floor     #  wall     %  CRACKED wall - breakable, a shortcut opened mid-fight
    ///   ~  CHASM - nothing stands on it; shots and sight cross it; a displaced enemy falls in
    ///
    /// Every shape (and each of its four mirror images) is validated at first use: the door's
    /// and the arrival point's zones are open, every floor cell is reachable WITHOUT breaking
    /// anything, and no corridor is narrower than two cells. A shape that fails is dropped with a
    /// warning naming it, never built.
    ///
    /// Ordinary combat and Rift floors only - a boss or puzzle room is built around the open
    /// rectangle. Seeded per floor (FloorPlanner.ShapeSeed), with System.Random so a preview
    /// does not move UnityEngine.Random's state.
    /// </summary>
    public sealed class RoomShape
    {
        public const int Cols = 24, Rows = 14;

        public readonly string Name;
        /// <summary>Rows NORTH first, exactly as authored (mirrors applied).</summary>
        public readonly string[] Grid;

        RoomShape(string name, string[] grid) { Name = name; Grid = grid; }

        public char At(int col, int rowFromTop) => Grid[rowFromTop][col];

        // ------------------------------------------------------------------ the catalogue

        static readonly (string Name, string[] Grid)[] Authored =
        {
            // A solid centre block; the fight circles it. Its ends are cracked.
            ("plaza", new[]
            {
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........%######%........",
                "........%######%........",
                "........%######%........",
                "........%######%........",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
            }),
            // A wall across the middle, gaps near each end - and a cracked shortcut straight up the
            // centre, between the arrival point and the door.
            ("divide", new[]
            {
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "##...#####%%%%#####...##",
                "##...#####%%%%#####...##",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
            }),
            // Solid corners, a plus-shaped floor. No corner to back into.
            ("crossroads", new[]
            {
                "######............######",
                "######............######",
                "######............######",
                "######............######",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "######............######",
                "######............######",
                "######............######",
                "######............######",
            }),
            // A long hall, alcoves for the door and the arrival point. The pack comes from the ends.
            ("hall", new[]
            {
                "#########......#########",
                "#########......#########",
                "#########......#########",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "#########......#########",
                "#########......#########",
                "#########......#########",
            }),
            // Three rooms, the doorways at different heights, a cracked segment in each wall.
            ("chambers", new[]
            {
                ".......#........#.......",
                ".......#........#.......",
                ".......#........%.......",
                ".......#........%.......",
                ".......#........#.......",
                "................#.......",
                "................#.......",
                "........................",
                ".......#................",
                ".......#................",
                ".......%........#.......",
                ".......%........#.......",
                ".......#........#.......",
                ".......#........#.......",
            }),
            // One wall hangs from the north, one rises from the south: an S round the flanks.
            ("baffles", new[]
            {
                ".....##.................",
                ".....##.................",
                ".....##.................",
                ".....##.................",
                ".....##.................",
                ".....##..........##.....",
                ".....##..........##.....",
                ".....##..........##.....",
                ".....##..........##.....",
                ".................##.....",
                ".................##.....",
                ".................##.....",
                ".................##.....",
                ".................##.....",
            }),
            // A U-shaped fort in the centre, open to the south; its north wall cracked in the middle.
            ("bastion", new[]
            {
                "........................",
                "........................",
                "........................",
                "........................",
                ".......####%%####.......",
                ".......##......##.......",
                ".......##......##.......",
                ".......##......##.......",
                ".......##......##.......",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
            }),
            // A hole in the middle of the room - the plaza's block, open. Anything a Heavy
            // finisher sends toward the centre goes in.
            ("maw", new[]
            {
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........~~~~~~~~........",
                "........~~~~~~~~........",
                "........~~~~~~~~........",
                "........~~~~~~~~........",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
            }),
            // A two-deep rift across the middle - a dash clears it - crossed by two bridges.
            ("bridges", new[]
            {
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "~~~...~~~~~~~~~~~~...~~~",
                "~~~...~~~~~~~~~~~~...~~~",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
            }),
            // The east and west floor drops away: knock them toward the walls.
            ("ledges", new[]
            {
                "........................",
                "........................",
                "~~~..................~~~",
                "~~~..................~~~",
                "~~~..................~~~",
                "~~~..................~~~",
                "~~~..................~~~",
                "~~~..................~~~",
                "~~~..................~~~",
                "~~~..................~~~",
                "~~~..................~~~",
                "~~~..................~~~",
                "........................",
                "........................",
            }),
        };

        /// <summary>Every valid shape, by name, each with its mirror images (index 0 = as authored,
        /// 1 = mirrored east-west, 2 = north-south, 3 = both).</summary>
        static Dictionary<string, RoomShape[]> _catalogue;
        static List<string> _names;

        static void EnsureCatalogue()
        {
            if (_catalogue != null) return;
            _catalogue = new Dictionary<string, RoomShape[]>();
            _names = new List<string>();
            foreach (var (name, grid) in Authored)
            {
                var variants = new RoomShape[4];
                bool ok = true;
                for (int v = 0; v < 4 && ok; v++)
                {
                    var g = Mirror(grid, (v & 1) != 0, (v & 2) != 0);
                    var why = Validate(g);
                    if (why != null)
                    {
                        Debug.LogWarning($"[RoomShape] '{name}' (variant {v}) dropped: {why}");
                        ok = false;
                    }
                    variants[v] = new RoomShape(name, g);
                }
                if (!ok) continue;
                _catalogue[name] = variants;
                _names.Add(name);
            }
        }

        public static IReadOnlyList<string> Names { get { EnsureCatalogue(); return _names; } }

        /// <summary>A named shape's mirror image (0 as authored, 1 east-west, 2 north-south, 3
        /// both), or null - for dev tools and previews.</summary>
        public static RoomShape Get(string name, int variant = 0)
        {
            EnsureCatalogue();
            return _catalogue.TryGetValue(name, out var v) ? v[variant & 3] : null;
        }

        /// <summary>One-shot: the next shaped-eligible floor takes this shape ("open" for none).</summary>
        public static string DevForceNext;

        /// <summary>
        /// The shape for <paramref name="floor"/>, or null for the open rectangle. The caller
        /// decides eligibility (combat floors only). The same shape never comes twice running:
        /// the previous floor's own draw is re-made from ITS seed and excluded, so the rule needs
        /// no memory of what was actually played.
        /// </summary>
        public static RoomShape For(int floor, int seed, int previousSeed)
        {
            EnsureCatalogue();
            if (DevForceNext != null)
            {
                string forced = DevForceNext;
                DevForceNext = null;
                if (_catalogue.TryGetValue(forced, out var fv))
                    return fv[new System.Random(seed).Next(4)];
                return null;
            }

            string name = Draw(floor, seed);
            if (name == null) return null;
            string previous = Draw(floor - 1, previousSeed);
            var rng = new System.Random(unchecked(seed * 31 + 7));
            if (name == previous && _names.Count > 1)
            {
                int i = _names.IndexOf(name);
                name = _names[(i + 1 + rng.Next(_names.Count - 1)) % _names.Count];
            }
            return _catalogue[name][rng.Next(4)];
        }

        /// <summary>The raw draw for a floor: a name, or null for open.</summary>
        static string Draw(int floor, int seed)
        {
            if (floor < Tuning.Rooms.FromFloor || _names.Count == 0) return null;
            var rng = new System.Random(seed);
            if (rng.NextDouble() >= Tuning.Rooms.ShapedChance) return null;
            return _names[rng.Next(_names.Count)];
        }

        // ------------------------------------------------------------------ mirrors and checks

        static string[] Mirror(string[] grid, bool eastWest, bool northSouth)
        {
            var g = new string[grid.Length];
            for (int r = 0; r < grid.Length; r++)
            {
                var row = grid[northSouth ? grid.Length - 1 - r : r];
                if (eastWest)
                {
                    var chars = row.ToCharArray();
                    System.Array.Reverse(chars);
                    row = new string(chars);
                }
                g[r] = row;
            }
            return g;
        }

        /// <summary>Null when the grid is a buildable room, else what is wrong with it.</summary>
        static string Validate(string[] g)
        {
            if (g.Length != Rows) return $"{g.Length} rows, not {Rows}";
            for (int r = 0; r < Rows; r++)
            {
                if (g[r].Length != Cols) return $"row {r} is {g[r].Length} wide, not {Cols}";
                foreach (char c in g[r])
                    if (c != '.' && c != '#' && c != '%' && c != '~') return $"row {r} has '{c}'";
            }

            bool Floor(int r, int c) => r >= 0 && c >= 0 && r < Rows && c < Cols && g[r][c] == '.';

            // The door (north) and the arrival point (south): four cells wide, four deep, open.
            for (int r = 0; r < 4; r++)
            for (int c = Cols / 2 - 2; c < Cols / 2 + 2; c++)
            {
                if (!Floor(r, c)) return "the door's zone is walled";
                if (!Floor(Rows - 1 - r, c)) return "the arrival zone is walled";
            }

            // Reachable from the arrival point ON FOOT - without breaking anything, and without
            // a dash over a chasm (a room the player can only finish by dashing is a trap).
            var seen = new bool[Rows, Cols];
            var stack = new Stack<(int, int)>();
            stack.Push((Rows - 2, Cols / 2));
            seen[Rows - 2, Cols / 2] = true;
            int reached = 1, total = 0;
            for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++) if (Floor(r, c)) total++;
            while (stack.Count > 0)
            {
                var (r, c) = stack.Pop();
                foreach (var (dr, dc) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nr = r + dr, nc = c + dc;
                    if (!Floor(nr, nc) || seen[nr, nc]) continue;
                    seen[nr, nc] = true;
                    reached++;
                    stack.Push((nr, nc));
                }
            }
            if (reached != total) return $"{total - reached} floor cell(s) unreachable";

            // Two cells wide everywhere: every floor cell sits in some 2x2 of floor.
            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                if (!Floor(r, c)) continue;
                bool wide = false;
                for (int r0 = r - 1; r0 <= r && !wide; r0++)
                for (int c0 = c - 1; c0 <= c && !wide; c0++)
                    wide = Floor(r0, c0) && Floor(r0 + 1, c0) && Floor(r0, c0 + 1) && Floor(r0 + 1, c0 + 1);
                if (!wide) return $"a one-cell corridor at row {r}, column {c}";
            }
            return null;
        }
    }
}
