using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// COMPOSED rooms (the user's call, 2026-10-22): a room is an OUTER BOUNDARY with STAMPS laid
    /// into it - both hand-drawn pictures in the shapes' own alphabet - so a run meets hundreds of
    /// rooms instead of ten, and every one of them still looks designed. Only chasm OUTLINES are
    /// generated (an organic edge is what a hand-drawn grid does worst).
    ///
    ///   BOUNDARY  a full 24 x 14 picture whose walls carve the room's outline: an octagon, a cave,
    ///             a corner missing. The rectangle is one of them.
    ///   STAMP     a small picture. A space is "leave as it is"; every other cell must land on open
    ///             floor (so a stamp's own '.' cells are clearance it keeps). Laid in any of eight
    ///             orientations, often with its east-west twin, so the room reads as composed.
    ///   CHASM     a generated blob, small or large, drawn as a drop or a lake as ever.
    ///
    /// Every composition goes through <see cref="Validate"/> - the authored shapes' own rules -
    /// and a failure is retried from the same seed's stream; after <c>ComposeTries</c> the floor is
    /// the open rectangle. Seeded from the floor's shape seed only, System.Random, so a preview
    /// and a restart meet the same room.
    ///
    /// Stamps carry TAGS (organic, ruin, cover, ...) for the biomes to weight later - a biome will
    /// change the odds of a piece, never the rules (the user's rule).
    /// </summary>
    public sealed partial class RoomShape
    {
        // ------------------------------------------------------------------ boundaries

        static readonly (string Name, float Weight, string[] Grid)[] Boundaries =
        {
            ("rect", 3f, null),   // null = the plain rectangle
            ("octagon", 1.5f, new[]
            {
                "###..................###",
                "##....................##",
                "#......................#",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "#......................#",
                "##....................##",
                "###..................###",
            }),
            // Ragged walls, as if the room were cut out of rock.
            ("cave", 1.2f, new[]
            {
                "####....##......#...####",
                "##..............#....###",
                "##....................##",
                "#.......................",
                "#.......................",
                "##.....................#",
                "........................",
                "........................",
                "#.......................",
                "##.....................#",
                "##....................##",
                "##....................##",
                "##....................##",
                "####....##......##..####",
            }),
            // A corner of the room is simply gone.
            ("corner", 1f, new[]
            {
                "..................######",
                "..................######",
                "..................######",
                "..................######",
                "..................######",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
                "........................",
            }),
            // The sides pinch in at the middle.
            ("waist", 1f, new[]
            {
                "........................",
                "........................",
                "........................",
                "#......................#",
                "##....................##",
                "####................####",
                "#####..............#####",
                "#####..............#####",
                "####................####",
                "##....................##",
                "#......................#",
                "........................",
                "........................",
                "........................",
            }),
            // Square notches out of every corner and both flanks: a cross-shaped hall.
            ("notched", 1f, new[]
            {
                "####..............####..",
                "####..............####..",
                "####....................",
                "........................",
                "........................",
                "........................",
                "#####..............#####",
                "#####..............#####",
                "........................",
                "........................",
                "........................",
                "####....................",
                "####..............####..",
                "####..............####..",
            }),
        };

        // ------------------------------------------------------------------ stamps

        /// <summary>A stamp: its picture (a space leaves the cell alone), a weight, and its tags.</summary>
        readonly struct Stamp
        {
            public readonly string Name; public readonly float Weight; public readonly string Tags; public readonly string[] Grid;
            public Stamp(string name, float weight, string tags, params string[] grid) { Name = name; Weight = weight; Tags = tags; Grid = grid; }
        }

        static readonly Stamp[] Stamps =
        {
            new("block", 1.2f, "cover ruin",
                "....",
                ".##.",
                ".##.",
                "...."),
            new("slab", 1f, "cover ruin",
                ".....",
                ".###.",
                ".###.",
                "....."),
            new("bar", 1f, "wall",
                "........",
                ".######.",
                "........"),
            new("cracked bar", 0.8f, "wall shortcut",
                "........",
                ".##%%##.",
                "........"),
            new("ell", 1f, "wall",
                ".....",
                ".###.",
                ".#...",
                ".#...",
                "....."),
            new("tee", 0.8f, "wall",
                ".......",
                ".#####.",
                "...#...",
                "...#...",
                "......."),
            new("posts", 1f, "cover ruin",
                ".....",
                ".#.#.",
                ".....",
                ".#.#.",
                "....."),
            new("cross", 0.7f, "cover",
                ".....",
                "..#..",
                ".###.",
                "..#..",
                "....."),
            new("rubble", 1f, "cover ruin shortcut",
                ".....",
                ".%%#.",
                ".#%%.",
                "....."),
            new("chevron", 0.8f, "wall",
                "......",
                ".#....",
                ".##...",
                "..##..",
                "...##.",
                "......"),
            new("shrine", 0.6f, "cover ruin",
                ".......",
                ".#...#.",
                ".......",
                ".......",
                ".#...#.",
                "......."),
            new("gate", 0.7f, "wall shortcut",
                ".........",
                ".###%###.",
                "........."),
            new("rift", 0.8f, "chasm",
                "........",
                ".~~~~~~.",
                ".~~~~~~.",
                "........"),
        };

        // ------------------------------------------------------------------ composing

        /// <summary>
        /// A composed room for this seed, or null when no try validated (the open room). Pure:
        /// the same seed always gives the same room.
        /// </summary>
        public static RoomShape Compose(int seed)
        {
            var rng = new System.Random(unchecked(seed * 7919 + 13));
            for (int attempt = 0; attempt < Tuning.Rooms.ComposeTries; attempt++)
            {
                var name = new StringBuilder();
                var grid = BoundaryGrid(rng, name);

                // Each piece is checked as it goes in, and only THAT piece is taken back if it
                // breaks the room - a whole-room retry threw away every good piece with the bad one.
                if (rng.NextDouble() < Tuning.Rooms.ComposedChasmChance * Art.Biomes.Biome.ChasmChanceMul)
                {
                    bool large = rng.NextDouble() < Art.Biomes.Biome.LargeChasmShare(Tuning.Rooms.LargeChasmShare);
                    if (TryLay(grid, ChasmBlob(rng, large), rng, twin: false)) name.Append(large ? "+great chasm" : "+chasm");
                }

                int pieces = rng.Next(Tuning.Rooms.StampsMin, Tuning.Rooms.StampsMax + 1) - Art.Biomes.Biome.StampsLess;
                for (int p = 0; p < pieces; p++)
                {
                    var stamp = PickStamp(rng);
                    bool twin = rng.NextDouble() < Tuning.Rooms.MirroredPairChance;
                    if (TryLay(grid, Orient(stamp.Grid, rng.Next(8)), rng, twin))
                        name.Append('+').Append(stamp.Name).Append(twin ? " x2" : "");
                }

                var rows = Rows_(grid);
                if (Validate(rows) == null && !IsOpen(rows)) return new RoomShape(name.ToString(), rows);
            }
            return null;
        }

        static string[] Rows_(char[][] grid)
        {
            var rows = new string[Rows];
            for (int r = 0; r < Rows; r++) rows[r] = new string(grid[r]);
            return rows;
        }

        /// <summary>Lays a piece and keeps it only if the room still validates.</summary>
        static bool TryLay(char[][] grid, string[] piece, System.Random rng, bool twin)
        {
            var before = new char[Rows][];
            for (int r = 0; r < Rows; r++) before[r] = (char[])grid[r].Clone();
            if (!Place(grid, piece, rng, twin)) return false;
            if (Validate(Rows_(grid)) == null) return true;
            for (int r = 0; r < Rows; r++) grid[r] = before[r];
            return false;
        }

        static bool IsOpen(string[] rows)
        {
            foreach (var row in rows) if (row.IndexOf('#') >= 0 || row.IndexOf('%') >= 0 || row.IndexOf('~') >= 0) return false;
            return true;
        }

        static char[][] BoundaryGrid(System.Random rng, StringBuilder name)
        {
            float total = 0f;
            foreach (var b in Boundaries) total += b.Weight * Art.Biomes.Biome.BoundaryWeight(b.Name);
            float roll = (float)rng.NextDouble() * total;
            var pick = Boundaries[0];
            foreach (var b in Boundaries) { if ((roll -= b.Weight * Art.Biomes.Biome.BoundaryWeight(b.Name)) <= 0f) { pick = b; break; } }

            var grid = new char[Rows][];
            string[] src = pick.Grid != null ? Mirror(pick.Grid, rng.Next(2) == 1, rng.Next(2) == 1) : null;
            for (int r = 0; r < Rows; r++) grid[r] = src != null ? src[r].ToCharArray() : new string('.', Cols).ToCharArray();
            name.Append(pick.Name);
            return grid;
        }

        static Stamp PickStamp(System.Random rng)
        {
            float total = 0f;
            foreach (var s in Stamps) total += s.Weight * Art.Biomes.Biome.StampWeight(s.Tags);
            float roll = (float)rng.NextDouble() * total;
            foreach (var s in Stamps) if ((roll -= s.Weight * Art.Biomes.Biome.StampWeight(s.Tags)) <= 0f) return s;
            return Stamps[Stamps.Length - 1];
        }

        /// <summary>One of eight orientations: o &amp; 4 transposes, o &amp; 1 mirrors east-west,
        /// o &amp; 2 north-south.</summary>
        static string[] Orient(string[] g, int o)
        {
            string[] t = g;
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

        /// <summary>
        /// Lays the stamp somewhere it fits: every non-space cell on open floor, nothing inside the
        /// door's or the arrival point's zone (plus a cell). With <paramref name="mirrorTwin"/> its
        /// east-west mirror goes at the mirrored spot too - both, or neither.
        /// </summary>
        static bool Place(char[][] grid, string[] stamp, System.Random rng, bool mirrorTwin)
        {
            int h = stamp.Length, w = stamp[0].Length;
            if (h > Rows || w > Cols) return false;
            var twin = mirrorTwin ? Mirror(stamp, true, false) : null;

            var spots = new List<(int r, int c)>();
            for (int r = 0; r + h <= Rows; r++)
            for (int c = 0; c + w <= Cols; c++)
            {
                if (!Fits(grid, stamp, r, c)) continue;
                if (mirrorTwin)
                {
                    int tc = Cols - c - w;
                    // a twin overlapping its original is just a wider stamp - skip
                    if (tc < c + w && c < tc + w) continue;
                    if (!Fits(grid, twin, r, tc)) continue;
                }
                spots.Add((r, c));
            }
            if (spots.Count == 0) return false;

            var (pr, pc) = spots[rng.Next(spots.Count)];
            Lay(grid, stamp, pr, pc);
            if (mirrorTwin) Lay(grid, twin, pr, Cols - pc - w);
            return true;
        }

        static bool Fits(char[][] grid, string[] stamp, int r0, int c0)
        {
            for (int r = 0; r < stamp.Length; r++)
            for (int c = 0; c < stamp[r].Length; c++)
            {
                char s = stamp[r][c];
                if (s == ' ') continue;
                int R = r0 + r, C = c0 + c;
                if (grid[R][C] != '.') return false;
                if (s != '.' && InZone(R, C)) return false;
            }
            return true;
        }

        /// <summary>The door's and the arrival point's zones (Validate's), a cell wider all round.</summary>
        static bool InZone(int r, int c)
            => c >= Cols / 2 - 3 && c < Cols / 2 + 3 && (r < 5 || r >= Rows - 5);

        static void Lay(char[][] grid, string[] stamp, int r0, int c0)
        {
            for (int r = 0; r < stamp.Length; r++)
            for (int c = 0; c < stamp[r].Length; c++)
                if (stamp[r][c] != ' ') grid[r0 + r][c0 + c] = stamp[r][c];
        }

        /// <summary>
        /// A chasm with a GENERATED outline: an ellipse whose edge is pushed in and out by a few
        /// low harmonics, so it reads as a hole worn in the floor rather than a cut rectangle.
        /// Ringed with a cell of clearance, as the stamps are.
        /// </summary>
        static string[] ChasmBlob(System.Random rng, bool large)
        {
            int w = large ? rng.Next(8, 13) : rng.Next(4, 7);
            int h = large ? rng.Next(4, 7) : rng.Next(2, 4);
            float a1 = (float)rng.NextDouble() * 6.283f, a2 = (float)rng.NextDouble() * 6.283f;
            float k1 = 0.08f + (float)rng.NextDouble() * 0.12f, k2 = (float)rng.NextDouble() * 0.1f;
            var rows = new string[h + 2];
            for (int r = 0; r < h + 2; r++)
            {
                var row = new char[w + 2];
                for (int c = 0; c < w + 2; c++)
                {
                    row[c] = '.';
                    if (r == 0 || c == 0 || r == h + 1 || c == w + 1) continue;
                    float x = (c - 0.5f - w / 2f) / (w / 2f), y = (r - 0.5f - h / 2f) / (h / 2f);
                    float ang = Mathf.Atan2(y, x);
                    float edge = 1f + k1 * Mathf.Sin(2f * ang + a1) + k2 * Mathf.Sin(3f * ang + a2);
                    if (x * x + y * y <= edge * edge * 1.05f) row[c] = '~';
                }
                rows[r] = new string(row);
            }
            return rows;
        }

        // ------------------------------------------------------------------ dev

        /// <summary>
        /// <paramref name="count"/> composed rooms from consecutive seeds, as text - for judging the
        /// catalogue's variety without playing. Also reports how many seeds fell back to open.
        ///   unity command eval --code 'return Convergence.Hazards.RoomShape.Sample(12, 1);'
        /// </summary>
        public static string Sample(int count, int firstSeed)
        {
            var sb = new StringBuilder();
            int open = 0, chasm = 0;
            for (int i = 0; i < count; i++)
            {
                var room = Compose(firstSeed + i);
                if (room == null) { open++; continue; }
                if (room.HasChasm) chasm++;
                sb.AppendLine($"[{firstSeed + i}] {room.Name}");
                foreach (var row in room.Grid) sb.AppendLine(row);
            }
            sb.Insert(0, $"{count} seeds: {count - open} composed ({chasm} with a chasm), {open} fell back to open\n");
            return sb.ToString();
        }

        /// <summary>Checks every boundary in all four mirrors on its own - a boundary that fails
        /// can never host a room, so it is named here rather than discovered as fallbacks.</summary>
        public static string CheckBoundaries()
        {
            var sb = new StringBuilder();
            foreach (var b in Boundaries)
            {
                if (b.Grid == null) continue;
                for (int v = 0; v < 4; v++)
                {
                    var why = Validate(Mirror(b.Grid, (v & 1) != 0, (v & 2) != 0));
                    if (why != null) sb.AppendLine($"{b.Name} v{v}: {why}");
                }
            }
            return sb.Length == 0 ? "all boundaries valid" : sb.ToString();
        }
    }
}
