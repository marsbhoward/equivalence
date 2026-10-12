using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== The Hive: a bow made of arrows
    //
    // Black Diamond, power 0 - bespoke art and nothing else (the cosmetic weapons' rule). The
    // user's concept (2026-10-10, mocked up at https://claude.ai/artifact/5kiBCNbMqZv8uWF8MR4AXN):
    // the whole silhouette is ARROWS held in formation. Each limb is two staggered lanes of six,
    // heads toward the tip, each head laid over the next arrow's fletching; the grip is three
    // arrows bound with two wraps; the string is a chain of small arrows meeting at the nock.
    // Wood shafts, silver heads, pale fletching with a red crest, outlined (the user's picks).
    //
    // WHAT KEEPS THE ARROWS APART: where one arrow lies over another, a rim just outside it is
    // darkened onto the one beneath (HiveShadow). Without it a limb reads as one striped band.
    //
    // Its relic is the HIVE REMOTE, a drone controller, carrying SWARM: the whole hive is loosed
    // as one swarm (Combat.HiveSwarm), and while it is out the bow is only its grip
    // (HiveSpentSprite). The flying arrows leave from, and come home to, the arrows' own places
    // in the picture (HiveFlight), so the bow comes apart and goes back together.
    //
    // One field (the layout below, rasterised per texel) sampled for the arena and (x4) the menu,
    // as the other designed bows: x across (string LEFT, the back right), y down from the upper
    // tip, 24 x 77 cells. Symmetric about the grip's middle row.
    public static partial class DemoGear
    {
        public const string HiveBowId = "hive_bow", HiveRemoteId = "hive_remote", SwarmArtId = "swarm_art";

        const int HiveCanvas = 24;
        const float HiveGripX = 18f, HiveMidY = 38.5f;
        const string HiveKey = "gear.weapon.hive", HiveSpentKey = "gear.weapon.hive.spent",
                     HiveArrowKey = "gear.weapon.hive.arrow";

        static Vector2Int HiveGrip => new((int)HiveGripX, (int)HiveMidY);
        static Vector2Int HiveDetailGrip => new(HiveGrip.x * EmberDetailScale, HiveGrip.y * EmberDetailScale);

        /// <summary>One arrow of the formation, in cells: its centre, the way its head points, its
        /// length, its size against a full arrow, and its depth (higher draws over lower).</summary>
        struct HiveArrow
        {
            public Vector2 C, D;
            public float L, K, Z;
            public bool Thin, Grip;
        }

        // Lazy: static arrays in the partial files must not depend on another file's statics
        // being initialised first (Tria Prima's note).
        static HiveArrow[] _hiveArrows;
        static HiveArrow[] HiveArrows => _hiveArrows ??= BuildHiveArrows();

        static string[] _hiveRows, _hiveDetail, _hiveSpentRows;
        static string[] HiveRows => _hiveRows ??= HiveSample(1, spent: false);
        static string[] HiveDetail => _hiveDetail ??= HiveSample(EmberDetailScale, spent: false);
        static string[] HiveSpentRows => _hiveSpentRows ??= HiveSample(1, spent: true);

        const int HiveLaneArrows = 6;
        const float HiveLaneStep = 0.155f, HiveFullLength = 10.5f;

        /// <summary>The upper limb as a cubic, grip to tip; the lower is its mirror.</summary>
        static readonly Vector2[] HiveLimb =
        {
            new(18f, 34f), new(20.5f, 20f), new(10.5f, 6.8f), new(4.5f, 2.3f),
        };

        static Vector2 HiveBezier(float s, bool lower)
        {
            float a = 1f - s;
            var p = a * a * a * HiveLimb[0] + 3f * a * a * s * HiveLimb[1]
                  + 3f * a * s * s * HiveLimb[2] + s * s * s * HiveLimb[3];
            if (lower) p.y = 2f * HiveMidY - p.y;
            return p;
        }

        /// <summary>Where along its limb (0 grip, 1 tip) a lane's arrow sits: lane 0 the back,
        /// lane 1 the belly (string side), staggered half a step.</summary>
        static float HiveLaneS(int lane, int i) => (lane == 0 ? 0.07f : 0.145f) + HiveLaneStep * i;

        /// <summary>One limb arrow at <paramref name="s"/> along its limb, in its lane.</summary>
        static HiveArrow HiveLimbArrow(bool lower, int lane, float s)
        {
            var p = HiveBezier(s, lower);
            var t = (HiveBezier(Mathf.Min(1f, s + 0.01f), lower) - HiveBezier(Mathf.Max(0f, s - 0.01f), lower)).normalized;
            var n = new Vector2(t.y, -t.x);
            if (n.x < 0f) n = -n;   // toward the BACK
            float off = (lane == 0 ? 1f : -1f) * 0.9f * 1.55f * (1f - 0.55f * s);
            float len = HiveFullLength - 3f * s;
            // Heads over the next arrow's fletching: tipward arrows sit lower.
            return new HiveArrow
            {
                C = p + n * off, D = t, L = len, K = len / HiveFullLength,
                Z = 60f - s * 40f + (lane == 1 ? 0.5f : 0f),
            };
        }

        /// <summary>
        /// The formation. A limb's SLIDE state (0 whole, 1..HiveSlideStates) is the draw and loose
        /// (Combat.HiveLoose): its belly lane's grip-most arrow has been taken for the shot, and
        /// the rest of the lane has slid that far (state / HiveSlideStates of a step) toward the
        /// grip together - at the last state the tip's place is the empty one the shot comes
        /// home to. Whole again, the picture is identical, so nothing jumps.
        /// </summary>
        static HiveArrow[] BuildHiveArrows(int stateU = 0, int stateL = 0)
        {
            var list = new List<HiveArrow>();
            foreach (bool lower in new[] { false, true })
            {
                int state = lower ? stateL : stateU;
                float slid = state / (float)HiveSlideStates;
                for (int lane = 0; lane < 2; lane++)
                    for (int i = 0; i < HiveLaneArrows; i++)
                    {
                        float s = HiveLaneS(lane, i);
                        if (lane == 1 && state > 0)
                        {
                            if (i == 0) continue;            // taken for the shot
                            s -= slid * HiveLaneStep;
                        }
                        list.Add(HiveLimbArrow(lower, lane, s));
                    }
            }

            // The string: a chain of small arrows from each tip to the nock, pointing in.
            var nock = new Vector2(HiveLimb[3].x, HiveMidY);
            foreach (bool lower in new[] { false, true })
            {
                var tip = HiveBezier(1f, lower);
                var along = nock - tip;
                float len = along.magnitude;
                var d = along / len;
                const int count = 7;
                float seg = len / count;
                for (int i = 0; i < count; i++)
                    list.Add(new HiveArrow
                    {
                        C = tip + d * ((i + 0.5f) * seg), D = d, L = seg * 1.05f, K = 0.48f,
                        Z = 5f + i * 0.01f, Thin = true,
                    });
            }

            // The grip: three arrows side by side, alternating, over the limbs' roots.
            float[] gx = { HiveGripX - 0.9f, HiveGripX, HiveGripX + 0.9f };
            for (int i = 0; i < 3; i++)
                list.Add(new HiveArrow
                {
                    C = new Vector2(gx[i], HiveMidY), D = new Vector2(0f, (i & 1) == 0 ? -1f : 1f),
                    L = 9.4f, K = 0.85f, Z = 80f + i, Grip = true,
                });

            list.Sort((a, b) => a.Z.CompareTo(b.Z));
            return list.ToArray();
        }

        // ---------------------------------------------------------------- one arrow, in its frame
        //
        // u along it from the nock, v across. The parts and their letters:
        //   H/M/N  the silver head, lit edge / middle / shadow edge
        //   W/w    the wood shaft, lit / shadow side
        //   F/f/g  the fletching, lit vane / its tip / the shadow vane
        //   c      the crest band ahead of the fletching
        //   n      the nock
        // HiveShadow's x/X darken whatever lies beneath an overlap.

        const char HivePartNone = '\0';

        static char HiveCover(float u, float v, in HiveArrow a, float grow, float sw)
        {
            float k = a.K, L = a.L, av = Mathf.Abs(v);
            float hl = 2.6f * k, hw = 1.6f * k, fl = 3f * k, fw = (a.Thin ? 1f : 1.5f) * k, f0 = 0.25f * k;
            if (u >= L - hl - grow && u <= L + grow)
            {
                float halfw = hw * Mathf.Min(1f, (L - u) / hl * 1.08f) + grow;
                if (av <= halfw) return 'H';
            }
            if (u >= -0.45f * k - grow && u < f0 && av <= sw + 0.2f * k + grow) return 'n';
            if (u >= f0 - grow && u <= f0 + fl + grow)
            {
                float t = (u - f0) / fl;
                float hv = sw + (fw - sw) * Mathf.Clamp01(1f - Mathf.Max(0f, t - 0.3f) / 0.7f) + grow;
                if (av <= hv) return av <= sw ? 'r' : 'F';
            }
            if (u >= 0f && u <= L - hl && av <= sw + grow)
            {
                if (!a.Thin && u >= f0 + fl + 0.25f && u <= f0 + fl + 0.25f + 0.75f * k) return 'c';
                return 'W';
            }
            return HivePartNone;
        }

        /// <summary>The letter for a part at (u, v): lit from the upper left like everything else.</summary>
        static char HiveShade(char part, float u, float v, in HiveArrow a, float sw)
        {
            // The light comes from the upper left: (-0.6, -0.8) with y DOWN the grid.
            bool lit = Mathf.Sign(v) * (-a.D.y * -0.6f + a.D.x * -0.8f) > 0f;
            float av = Mathf.Abs(v), k = a.K;
            switch (part)
            {
                case 'H':
                    float halfw = 1.6f * k * Mathf.Min(1f, (a.L - u) / (2.6f * k) * 1.08f);
                    if (av > 0.5f * halfw) return lit ? 'H' : 'N';
                    return 'M';
                case 'W': return av > sw * 0.45f && !lit ? 'w' : 'W';
                case 'r': return 'w';
                case 'F': return lit ? (av > 0.9f * k ? 'f' : 'F') : 'g';
                default: return part;
            }
        }

        static bool HiveDarkUnder(char c) => c == 'W' || c == 'w' || c == 'c' || c == 'n' || c == 'R' || c == 'Q';

        /// <summary>The bow sampled at <paramref name="k"/> texels per cell. Spent, only the grip
        /// is left - what the hand holds while the swarm is out.</summary>
        static string[] HiveSample(int k, bool spent, HiveArrow[] arrows = null)
        {
            arrows ??= HiveArrows;
            int w = HiveCanvas * k, h = BowHeightRows * k;
            var grid = new char[h, w];
            var depth = new float[h, w];
            for (int Y = 0; Y < h; Y++)
                for (int X = 0; X < w; X++) { grid[Y, X] = '.'; depth[Y, X] = float.NegativeInfinity; }

            bool fine = k > 1;
            float sw = fine ? 0.34f : 0.55f;
            float grow = fine ? 1.5f / k : 1f;

            foreach (var a in arrows)
            {
                if (spent && !a.Grip) continue;
                var tail = a.C - a.D * (a.L * 0.5f);
                var head = a.C + a.D * (a.L * 0.5f);
                float pad = 2.2f * a.K + 1f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(tail.x, head.x) - pad) * k));
                int x1 = Mathf.Min(w, Mathf.CeilToInt((Mathf.Max(tail.x, head.x) + pad) * k));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(tail.y, head.y) - pad) * k));
                int y1 = Mathf.Min(h, Mathf.CeilToInt((Mathf.Max(tail.y, head.y) + pad) * k));
                for (int Y = y0; Y < y1; Y++)
                    for (int X = x0; X < x1; X++)
                    {
                        float px = (X + 0.5f) / k - tail.x, py = (Y + 0.5f) / k - tail.y;
                        float u = px * a.D.x + py * a.D.y, v = a.D.x * py - a.D.y * px;
                        char part = HiveCover(u, v, a, 0f, sw);
                        if (part != HivePartNone)
                        {
                            grid[Y, X] = HiveShade(part, u, v, a, sw);
                            depth[Y, X] = a.Z;
                        }
                        else if (grid[Y, X] != '.' && depth[Y, X] < a.Z
                                 && HiveCover(u, v, a, grow, sw) != HivePartNone)
                        {
                            grid[Y, X] = HiveDarkUnder(grid[Y, X]) ? 'X' : 'x';
                            depth[Y, X] = a.Z;
                        }
                    }
            }

            // The grip's two wraps, over the grip arrows - banded on the menu art.
            foreach (var (ya, yb) in new[] { (HiveMidY - 3.3f, HiveMidY - 2f), (HiveMidY + 2f, HiveMidY + 3.3f) })
                for (int Y = Mathf.FloorToInt(ya * k); Y < Mathf.CeilToInt(yb * k); Y++)
                    for (int X = Mathf.FloorToInt((HiveGripX - 1.7f) * k); X < Mathf.CeilToInt((HiveGripX + 1.7f) * k); X++)
                        grid[Y, X] = fine && (((X + Y) >> 1) & 1) == 1 ? 'Q' : 'R';

            var rows = new string[h];
            var line = new char[w];
            for (int Y = 0; Y < h; Y++)
            {
                for (int X = 0; X < w; X++) line[X] = grid[Y, X];
                rows[Y] = new string(line);
            }
            return rows;
        }

        static Dictionary<char, Color> _hivePalette;
        static Dictionary<char, Color> HivePalette => _hivePalette ??= new Dictionary<char, Color>
        {
            ['H'] = Hex(0xeef2f4), ['M'] = Hex(0xa9b3ba), ['N'] = Hex(0x5d6870),   // silver head
            ['W'] = Hex(0xc79058), ['w'] = Hex(0x8e5c30),                          // wood shaft
            ['F'] = Hex(0xecebe6), ['f'] = Hex(0xbdbbb4), ['g'] = Hex(0x7d7b76),   // fletching
            ['c'] = Hex(0xb23a2e), ['n'] = Hex(0x2a2320),                          // crest, nock
            ['R'] = Hex(0x5a3a24), ['Q'] = Hex(0x3b2416),                          // grip wraps
            ['x'] = Hex(0x4a484c), ['X'] = Hex(0x3a2a20),                          // overlap shadow
        };

        static Color Hex(int rgb) => new(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);

        static void AddHive(List<GearItem> items)
        {
            var bow = WithMenu(Bow(Make(HiveBowId, "The Hive", GearSlot.Weapon, LootTier.BlackDiamond, 0f,
                Pixels(RigLayer.Weapon, HiveKey, HiveRows, HivePalette, 0f, 0f,
                       pivotTexel: HiveGrip, ppu: FinePpu, upscale2x: true))),
                Pixels(RigLayer.Weapon, HiveKey + ".menu", HiveDetail, HivePalette, 0f, 0f,
                       pivotTexel: HiveDetailGrip, ppu: MenuPpu));
            bow.SignatureFinisher = SwarmArtId;
            bow.Layers[0].Sprite.name = HiveKey;   // HiveFrameState reads frames by name
            items.Add(bow);
            AddHiveRemote(items);
        }

        // ------------------------------------------------------------------ what the swarm needs

        /// <summary>The bow's own arena sprite - the swarm only takes the bow apart when it is
        /// the one drawn (a relic held with another bow still swarms, from its middle).</summary>
        public static bool IsHive(Sprite s) => s != null && s == PixelSprite.Cached(HiveKey);

        // ------------------------------------------------------------------ what the loose needs

        /// <summary>How many steps a limb's lane slides through after its shot is taken.</summary>
        public const int HiveSlideStates = 4;

        /// <summary>The bow with each limb at its slide state (see BuildHiveArrows). (0, 0) is the
        /// bow itself. Built on first use and cached; named, so a renderer's sprite can be read
        /// back with <see cref="HiveFrameState"/>.</summary>
        public static Sprite HiveFrame(int stateU, int stateL)
        {
            stateU = Mathf.Clamp(stateU, 0, HiveSlideStates);
            stateL = Mathf.Clamp(stateL, 0, HiveSlideStates);
            if (stateU == 0 && stateL == 0) return PixelSprite.Cached(HiveKey);
            string key = $"{HiveKey}.slide{stateU}{stateL}";
            var cached = PixelSprite.Cached(key);
            if (cached != null) return cached;
            var made = PixelSprite.From(key, Double2x(HiveSample(1, false, BuildHiveArrows(stateU, stateL))),
                                        HivePalette, outline: true,
                                        pivotTexel: new Vector2Int(HiveGrip.x * FineUpscale, HiveGrip.y * FineUpscale),
                                        pixelsPerUnit: FinePpu);
            made.name = key;
            return made;
        }

        /// <summary>Is this one of the Hive's frames, and at which slide states? False for anything
        /// else on the renderer - a flash's silhouette, Medusa's stone, the swarm's bare grip.</summary>
        public static bool HiveFrameState(Sprite s, out int stateU, out int stateL)
        {
            stateU = stateL = 0;
            if (s == null) return false;
            if (s.name == HiveKey) return true;
            string slide = HiveKey + ".slide";
            if (!s.name.StartsWith(slide) || s.name.Length != slide.Length + 2) return false;
            stateU = s.name[slide.Length] - '0';
            stateL = s.name[slide.Length + 1] - '0';
            return true;
        }

        /// <summary>A belly-lane place on the whole bow, in the sprite's LOCAL units (pivot at the
        /// origin, y up): where the shot is taken from (index 0, by the grip) and where it comes
        /// home to (the last, by the tip). Scale is against the flying arrow sprite.</summary>
        public static (Vector2 Local, Vector2 Dir, float Scale) HiveBellySlot(bool lower, int index)
        {
            var a = HiveLimbArrow(lower, 1, HiveLaneS(1, Mathf.Clamp(index, 0, HiveLaneArrows - 1)));
            return HiveToLocal(a);
        }

        public const int HiveBellyLast = HiveLaneArrows - 1;

        /// <summary>A whole arrow's size against the flying sprite, and half the sprite's length
        /// in its own local units - what a nocked arrow is laid out by.</summary>
        public static float HiveNockScale => HiveFullLength / HiveArrowRows[0].Length;
        public static float HiveArrowHalfLocal => HiveArrowRows[0].Length * 0.5f * FineUpscale / FinePpu;

        /// <summary>A cell-space arrow as the bow sprite's local pose.</summary>
        static (Vector2 Local, Vector2 Dir, float Scale) HiveToLocal(in HiveArrow a)
        {
            // The pivot is texel (grip x 2) on the doubled grid, sampled at its centre: a
            // quarter cell past the grip cell's corner. A cell is two texels at FinePpu.
            var pivot = new Vector2(HiveGrip.x + 0.25f, HiveGrip.y + 0.25f);
            float unit = FineUpscale / FinePpu;
            return (new Vector2(a.C.x - pivot.x, -(a.C.y - pivot.y)) * unit,
                    new Vector2(a.D.x, -a.D.y), a.L / HiveArrowRows[0].Length);
        }

        /// <summary>The bow with every arrow gone but the grip: same canvas and pivot, so it drops
        /// onto the weapon renderer in place of the whole bow.</summary>
        public static Sprite HiveSpentSprite
            => PixelSprite.From(HiveSpentKey, Double2x(HiveSpentRows), HivePalette, outline: true,
                                pivotTexel: new Vector2Int(HiveGrip.x * FineUpscale, HiveGrip.y * FineUpscale),
                                pixelsPerUnit: FinePpu);

        /// <summary>
        /// One arrow pointing right (+x), pivoted on its middle - what flies. Drawn by hand, not
        /// sampled from the formation: lying level, the field's head and vane thresholds fall
        /// exactly on cell edges and it came out a bare shaft (keep thresholds off the grid -
        /// the Artemis note). Same parts and letters as the bow's arrows.
        /// </summary>
        static readonly string[] HiveArrowRows =
        {
            ".FF.......H...",
            "..FF......HH..",
            "nwWWcWWWWWMMMH",
            "..gg......NN..",
            ".gg.......N...",
        };

        public static Sprite HiveArrowSprite
            => PixelSprite.From(HiveArrowKey, Double2x(HiveArrowRows), HivePalette, outline: true,
                                pivotTexel: new Vector2Int(7 * FineUpscale, 2 * FineUpscale),
                                pixelsPerUnit: FinePpu);

        /// <summary>
        /// Every arrow that flies, in the bow sprite's own LOCAL units (pivot at the origin, y up):
        /// where it sits, the way its head points, and its size against the flying arrow sprite
        /// (so it flies at the length it had in the bow). Limbs and
        /// string; the grip stays in the hand.
        /// </summary>
        public static IReadOnlyList<(Vector2 Local, Vector2 Dir, float Scale)> HiveFlight
        {
            get
            {
                if (_hiveFlight != null) return _hiveFlight;
                var list = new List<(Vector2, Vector2, float)>();
                foreach (var a in HiveArrows)
                    if (!a.Grip) list.Add(HiveToLocal(a));
                return _hiveFlight = list;
            }
        }
        static List<(Vector2, Vector2, float)> _hiveFlight;

        // ------------------------------------------------------------------ the relic
        //
        // The HIVE REMOTE: a drone controller, graphite plastic, two sticks, amber buttons and a
        // green status light, an antenna with a red tip. It carries SWARM. At the body's density,
        // hung from the belt like every relic (HipRelic).

        // 10 x 13: k casing shadow, d shade, b body, l lit edge, s/S a stick's cap/well,
        // g the status light, h the amber buttons, A the antenna, a its tip.
        static readonly string[] HiveRemoteRows =
        {
            "........a.",
            "........A.",
            "........A.",
            ".llllllll.",
            "lbbbbbbbbd",
            "lbsbggbsbd",
            "lbSbbbbSbd",
            "lbbbbbbbbd",
            "lbbhbbhbbd",
            "ldbbbbbbdd",
            "lbd....lbd",
            "lbd....lbd",
            ".dd....dd.",
        };

        static void AddHiveRemote(List<GearItem> items)
        {
            var pal = new Dictionary<char, Color>
            {
                ['l'] = Hex(0x5a606b), ['b'] = Hex(0x3b3f47), ['d'] = Hex(0x262930), ['k'] = Hex(0x15171b),
                ['s'] = Hex(0xc9ced6), ['S'] = Hex(0x15171b), ['g'] = Hex(0x5fe08a), ['h'] = Hex(0xe8b44a),
                ['A'] = Hex(0x9aa0aa), ['a'] = Hex(0xff4a3d),
            };
            items.Add(RelicGrants(Make(HiveRemoteId, "Hive Remote", GearSlot.Relic, LootTier.BlackDiamond, 0f,
                                       HipRelic("gear.trinket.hive_remote", HiveRemoteRows, pal)),
                                  SwarmArtId, WeaponClass.Bow, twoHanded: true));
        }
    }
}
