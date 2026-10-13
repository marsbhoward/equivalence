using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ============================================================== The Gemini Blade
    //
    // A Black Diamond greatsword that is TWO single-edged blades laid spine to spine as mirror
    // images - its weapon art (not built yet) pulls them apart, one into each hand. Designed with
    // the user over a mock (https://claude.ai/artifact/9VT64jpN7dT3ASggffR7iX), 2026-10-12; every
    // shape and colour below is that mock's, ported as is.
    //
    //   CORE    blackened steel two thirds up the blade, ending in a chevron: a dark seam where the
    //           halves meet, a solid dark-grey border all round. From the guard, ICE climbs it and
    //           ends in one point on the seam.
    //   BLADE   silver, a lit bevel on the edge and a ridge up the all-silver point. FANGED from the
    //           cradle's tips down to the guard after Frostmourne - uneven fangs hooking toward the
    //           hilt. The second fang from the top is grown out of the core in blackened steel,
    //           past the edge; below it an arrowhead of ice points straight out the same way.
    //   GUARD   ice to its points, 24 cells across, a crystal shard rising off each end - the Blue
    //           Rose sword's frozen look. The grip stays dark leather bound in steel and the pommel
    //           silver, a spike either side.
    //   CRADLE  a black back plate after Frostmourne's mount: a V into the grip below the guard,
    //           arms rising beside the fangs above it to points. A 1-cell gap wherever it meets the
    //           sword, except at the guard, which runs out into it.
    //
    // Over the shared budgets ON PURPOSE, the user's calls: the guard (24 of 16) and the cradle
    // (about 18) - Phantom's gas is the precedent. Height, blade width (10) and grip are standard.
    //
    // ONE continuous field (GeminiTexel), measured from the axis and mirrored, so the two halves
    // are exact mirror images; sampled once per cell for the arena and four times for the menu.
    // Hashes are in DOUBLE, as the mock's JavaScript was - a float sin amplified by 43758 would
    // scatter every facet and fang.

    public static partial class DemoGear
    {
        public const string GeminiBladeId = "gemini_blade";
        public const string GeminiSayaId = "gemini_saya";
        public const string SolveCoagulaId = "solve_et_coagula";

        static void AddGemini(List<GearItem> items)
        {
            var pal = GeminiPal();
            // Black Diamond, power 0, cosmetic - the weapon art will ride a relic, the split every
            // other Black Diamond sword uses. Until it exists the sword pays out alone.
            var gemini = WithMenu(Make(GeminiBladeId, "Gemini Blade", GearSlot.Weapon, LootTier.BlackDiamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.gemini", GeminiRows, pal, 0f, -10f,
                       pivotTexel: GeminiGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.gemini.menu", Detail(GeminiTexel, SwordCanvas),
                       pal, 0f, -10f, pivotTexel: GeminiGrip * EmberDetailScale, ppu: MenuPpu));
            // The art rides the relic; the weapon carries the same id so a design drop pays the
            // two out together, and the sequence itself (HasGeminiSplit) - Zanmato's split.
            gemini.SignatureFinisher = SolveCoagulaId;
            gemini.HasGeminiSplit = true;
            gemini.HalfBlades = GeminiHalfBlades(pal);
            // Hung up to be looked at - the rack, the armoury wall, a gear card - its two halves
            // stand slightly APART and STAGGERED, one higher than the other (the user's calls), so
            // the sword reads as the pair it is. Held, it is one blade.
            gemini.DisplayLayer = Pixels(RigLayer.Weapon, "gear.weapon.gemini.display",
                GeminiDisplayRows(1), pal, 0f, -10f,
                pivotTexel: GeminiDisplayGrip, ppu: FinePpu, upscale2x: true);
            gemini.DisplayMenuLayer = Pixels(RigLayer.Weapon, "gear.weapon.gemini.display.menu",
                GeminiDisplayRows(EmberDetailScale), pal, 0f, -10f,
                pivotTexel: GeminiDisplayGrip * EmberDetailScale, ppu: MenuPpu);
            // Its minted image is the joined sword, as held - not the halves apart (the user's call).
            gemini.ImageShowsHeld = true;
            items.Add(gemini);

            // The relic: the Gemini Saya, ONE SHEATH AT EACH HIP (the user's call) - each hangs from
            // the belt, its mouth at the hip and its tip trailing back. The near one is drawn over
            // the body (Trinket), the far one behind it (TrinketBack), a shade darker and mostly
            // hidden. Facing right the black sheath is near; every half turn shows the other hip's
            // sheath nearer (TurnedLayers), so each keeps its own hip. Both swing back from the belt
            // as the body walks (HipSwingPivot). The weapon art it will carry is not built yet.
            var sayaPal = GeminiSayaPal();
            Vector2 near = GeminiSayaOffsetCells(far: false), farHip = GeminiSayaOffsetCells(far: true);
            var saya = Make(GeminiSayaId, "Gemini Saya", GearSlot.Relic, LootTier.BlackDiamond, 0f,
                Pixels(RigLayer.Trinket, "gear.trinket.gemini_saya", GeminiSayaRows(frosted: false, far: false),
                       sayaPal, near.x, near.y, ppu: BodyPpu),
                Pixels(RigLayer.TrinketBack, "gear.trinket.gemini_saya.far", GeminiSayaRows(frosted: true, far: true),
                       sayaPal, farHip.x, farHip.y, ppu: BodyPpu));
            // PixelsCore, like a cloak's back view: not the item's own layers, so they stay out of
            // _sources and the menu-art pass.
            saya.TurnedLayers = new[]
            {
                PixelsCore(RigLayer.Trinket, "gear.trinket.gemini_saya.turned",
                           GeminiSayaRows(frosted: true, far: false), sayaPal, near.x, near.y, ppu: BodyPpu),
                PixelsCore(RigLayer.TrinketBack, "gear.trinket.gemini_saya.far.turned",
                           GeminiSayaRows(frosted: false, far: true), sayaPal, farHip.x, farHip.y, ppu: BodyPpu),
            };
            saya.HipSwingPivot = GeminiSayaMouthFromCentre / BodyPpu;
            RelicGrants(saya, SolveCoagulaId);
            items.Add(saya);
        }

        // ---------------------------------------------------------------- layout, in cells

        const int GeminiGuardRows = 4, GeminiCollarRows = 1, GeminiPommelRows = 3;
        static int GeminiBladeRows => BladeRowsFor(GeminiGuardRows + GeminiCollarRows + SwordGripRows + GeminiPommelRows);
        static int GeminiGuardTop => GeminiBladeRows;                            // 57
        static int GeminiCollarTop => GeminiGuardTop + GeminiGuardRows;          // 61
        static int GeminiGripTop => GeminiCollarTop + GeminiCollarRows;          // 62
        static int GeminiPommelTop => GeminiGripTop + SwordGripRows;             // 74
        static Vector2Int GeminiGrip => new(13, GripCentre(GeminiGripTop, SwordGripRows));

        const double GeminiBladeHalf = 5, GeminiCoreHalf = 2.2, GeminiCoreTop = 17;
        const double GeminiSerrateTop = 37;                                      // where the cradle's pincers end

        static string[] _geminiRows;
        static string[] GeminiRows => _geminiRows ??= Arena(GeminiTexel, SwordCanvas);

        static char GeminiTexel(float x, float y, bool fine)
            => GeminiHalf(System.Math.Abs(x - SwordAxis), y, fine);

        // ---------------------------------------------------------------- palette

        static Dictionary<char, Color> GeminiPal()
        {
            var map = new Dictionary<char, Color>();
            void Put(string keys, params string[] hex)
            {
                for (int i = 0; i < keys.Length; i++)
                    map[keys[i]] = ColorUtility.TryParseHtmlString(hex[i], out var c) ? c : Color.magenta;
            }
            // silver - the blade and the pommel
            Put("123456", "#3b4252", "#5e6779", "#8992a5", "#b8c0ce", "#e0e5ee", "#ffffff");
            // hilt steel - the grip's binding, the cradle's inset line
            Put("opq", "#4f5765", "#717b8b", "#9ea7b5");
            // dark leather and the cradle's black
            Put("KSDL", "#04050a", "#0a0c17", "#131726", "#323d5c");
            // ice: deep, dark, base, light, glow
            Put("fgijz", "#2f6a9a", "#5b9ccc", "#93c8ee", "#cfeafd", "#ffffff");
            // blackened steel: seam, dark, base, border
            Put("PRTU", "#040405", "#15161b", "#22242b", "#3a3d47");
            // the gap cut round the cradle, in the outline's own black
            Put("O", "#06070b");
            return map;
        }

        // ---------------------------------------------------------------- shared helpers

        static double GeminiHash(double i, double j)
        {
            double n = System.Math.Sin(i * 127.1 + j * 311.7) * 43758.5453;
            return n - System.Math.Floor(n);
        }

        static double GeminiHypot(double a, double b) => System.Math.Sqrt(a * a + b * b);

        struct GeminiFang { public double Top, Len, Depth; }

        static GeminiFang[] _geminiFangs;

        /// <summary>The fangs, top to bottom: uneven lengths and depths, hashed so every build
        /// draws the same saw. Lazy - a static initialiser elsewhere may reach it first.</summary>
        static GeminiFang[] GeminiFangs
        {
            get
            {
                if (_geminiFangs != null) return _geminiFangs;
                var list = new List<GeminiFang>();
                double y = GeminiSerrateTop + 0.5;
                int n = 0;
                while (y < GeminiGuardTop - 2.5)
                {
                    double len = 3.6 + 2.4 * GeminiHash(n, 41);
                    double depth = 1.3 + 1.0 * GeminiHash(n, 43);
                    list.Add(new GeminiFang { Top = y, Len = System.Math.Min(len, GeminiGuardTop - 0.5 - y), Depth = depth });
                    y += len;
                    n++;
                }
                return _geminiFangs = list.ToArray();
            }
        }

        // ---------------------------------------------------------------- the blade

        /// <summary>The silver edge's distance from the axis: a point over the first 13 rows,
        /// then the fangs from the cradle's tips to the guard.</summary>
        static double GeminiEdgeAt(double y, bool fine)
        {
            if (y < 0.4) return 0;
            const double tip = 13;
            double e = y < tip ? GeminiBladeHalf * System.Math.Pow(y / tip, 0.85) : GeminiBladeHalf;
            if (y > GeminiSerrateTop && y < GeminiGuardTop - 0.5)
            {
                // Deepest just under the fang above, curving back out to a point at the bottom,
                // then a hard step into the next notch - every fang hooks down toward the hilt.
                foreach (var f in GeminiFangs)
                {
                    if (y < f.Top || y >= f.Top + f.Len) continue;
                    double u = (y - f.Top) / f.Len;
                    double cut = f.Depth * System.Math.Pow(1 - u, 1.5);
                    e -= fine ? cut : System.Math.Floor(cut * 1.25 + 0.5);
                }
            }
            return e;
        }

        /// <summary>The core's half-width: nothing above its point, then a slanted cut (a chevron
        /// with its mirror) widening to full over five rows.</summary>
        static double GeminiCoreHalfAt(double y)
            => y < GeminiCoreTop ? 0 : GeminiCoreHalf * System.Math.Min(1, (y - GeminiCoreTop) / 5);

        static char GeminiSword(double d, double y, bool fine)
        {
            char c;
            if ((c = GeminiIceSpike(d, y, fine)) != '.') return c;
            if ((c = GeminiBlackFang(d, y, fine)) != '.') return c;
            if ((c = GeminiShard(d, y, 9.8, GeminiGuardTop + 1.2, 12.2, GeminiGuardTop - 6, 1.45, fine)) != '.')
                return c;                                                        // rising off each guard end
            if (y >= GeminiGuardTop - 0.5) return GeminiHilt(d, y, fine);
            double e = GeminiEdgeAt(y, fine), core = GeminiCoreHalfAt(y);
            if (d < core) return GeminiCore(d, y, core, fine);
            if (d < e) return GeminiSilver(d, y, core, e, fine);
            return '.';
        }

        /// <summary>Silver: a lit bevel on the cutting edge, a ridge up the point above the core,
        /// and a shaded line where it meets the core.</summary>
        static char GeminiSilver(double d, double y, double core, double e, bool fine)
        {
            if (core == 0 && d < (fine ? 0.22 : 0.6)) return fine ? '6' : '5';
            if (e - d < (fine ? 0.5 : 1)) return fine && e - d < 0.2 ? '6' : '5';
            if (fine && core > 0 && d - core < 0.18) return '2';
            if (!fine) return d - core < 1 && core > 0 ? '3' : '4';
            return (e - d) / System.Math.Max(e - core, 0.01) < 0.55 ? '4' : '3';
        }

        /// <summary>How high the ice climbs the core: one point on the seam, its tip halfway down
        /// the black fang, the sides sloping down to the core's rim.</summary>
        static double GeminiFrostTopAt(double d)
        {
            var f = GeminiFangs[1];
            return f.Top + f.Len / 2 + 1.9 * d;
        }

        static char GeminiCore(double d, double y, double core, bool fine)
        {
            if (y > GeminiFrostTopAt(d))
            {
                if (fine && core - d < 0.16) return 'j';                         // a bright skin against the silver
                return GeminiIce(d, y, fine);
            }
            // Blackened steel: plain, a dark seam where the halves meet, and a solid dark-grey
            // border all round - not across the join with the black fang, so the two are
            // bordered as one shape.
            if (d < (fine ? 0.14 : 1)) return fine ? 'P' : 'R';
            if (core - d < (fine ? 0.5 : 1) && GeminiBlackFang(core + 0.3, y, fine) == '.') return 'U';
            return 'T';
        }

        const double GeminiBlackFangReach = 7.9;

        /// <summary>The second fang from the top, grown out of the core in blackened steel: from
        /// the core's rim at the fang's top it slopes out and down to a point past the edge, over
        /// the cradle arm, and its underside runs flat back in.</summary>
        static char GeminiBlackFang(double d, double y, bool fine)
        {
            var f = GeminiFangs[1];
            if (y < f.Top || y > f.Top + f.Len) return '.';
            double c = GeminiCoreHalf;
            double t = (y - f.Top) / f.Len;
            double out_ = c + (GeminiBlackFangReach - c) * System.Math.Min(1, t / 0.8);
            if (d < c - 0.01 || d > out_) return '.';
            double rim = fine ? 0.5 : 1;
            if (out_ - d < rim || f.Top + f.Len - y < rim) return 'U';
            return 'T';
        }

        /// <summary>One ice spike each side, below the black fang: an arrowhead leaving the ice and
        /// pointing straight out across the silver and over the cradle arm, tip at its middle.</summary>
        static char GeminiIceSpike(double d, double y, bool fine)
        {
            double top = GeminiGuardTop - 6.8, tip = GeminiGuardTop - 4.4, bottom = GeminiGuardTop - 2.6;
            const double reach = 8.1;
            if (y < top || y > bottom) return '.';
            double c = GeminiCoreHalf;
            bool above = y <= tip;
            double t = above ? (y - top) / (tip - top) : (bottom - y) / (bottom - tip);
            double out_ = c + (reach - c) * t;
            if (d < c || d > out_) return '.';
            if (!fine) return above ? 'j' : 'i';
            if (out_ - d < 0.25) return above ? 'z' : 'g';                       // lit along the top, shaded beneath
            return GeminiIce(d, y, fine);
        }

        /// <summary>
        /// Ice after the Blue Rose sword: long shards leaning outward from the axis, each lit on one
        /// face and shaded on the other, with white glints along the facet lines.
        /// </summary>
        static char GeminiIce(double d, double y, bool fine)
        {
            const double sx = 1.45, sy = 3.6;
            double u = d - (GeminiGuardTop - y) * 0.22;
            double gi = System.Math.Floor(u / sx), gj = System.Math.Floor(y / sy);
            double best = 1e9, second = 1e9, id = 0, px = 0, py = 0;
            for (int a = -1; a <= 1; a++)
            for (int b = -1; b <= 1; b++)
            {
                double i = gi + a, j = gj + b;
                double cx = (i + 0.2 + 0.6 * GeminiHash(i + 31, j)) * sx;
                double cy = (j + 0.2 + 0.6 * GeminiHash(j, i + 57)) * sy;
                double dd = GeminiHypot(u - cx, (y - cy) * 0.45);
                if (dd < best) { second = best; best = dd; id = i * 131 + j; px = cx; py = cy; }
                else if (dd < second) second = dd;
            }
            double edge = second - best;
            if (edge < (fine ? 0.1 : 0.12)) return GeminiHash(id, 3) > 0.55 ? 'z' : 'g';
            if (fine && edge < 0.2 && GeminiHash(id, 9) > 0.6) return 'j';
            bool lit = (u - px) + (y - py) * 0.25 < 0;
            if (GeminiHash(id, 13) > 0.86) return lit ? 'i' : 'g';             // a deeper shard here and there
            return lit ? 'j' : 'i';
        }

        /// <summary>A crystal shard: a long diamond from (ax, ay) to (bx, by), widest a third of
        /// the way along, a ridge down its middle.</summary>
        static char GeminiShard(double d, double y, double ax, double ay, double bx, double by, double w, bool fine)
        {
            double vx = bx - ax, vy = by - ay, len2 = vx * vx + vy * vy;
            double t = ((d - ax) * vx + (y - ay) * vy) / len2;
            if (t < 0 || t > 1) return '.';
            double qx = ax + t * vx, qy = ay + t * vy, dist = GeminiHypot(d - qx, y - qy);
            double half = w * (t < 0.33 ? t / 0.33 : (1 - t) / 0.67) + 0.1;
            if (dist > half) return '.';
            double side = (d - ax) * vy - (y - ay) * vx;
            if (!fine) return side < 0 ? 'j' : 'i';
            if (dist < 0.12) return 'z';
            return side < 0 ? (half - dist < 0.2 ? 'z' : 'j') : (half - dist < 0.2 ? 'g' : 'i');
        }

        // ---------------------------------------------------------------- the hilt

        static char GeminiHilt(double d, double y, bool fine)
        {
            // The guard: ice to its points, a white star where the halves meet.
            if (y >= GeminiGuardTop - 0.5 && y < GeminiCollarTop - 0.5)
            {
                double mid = GeminiGuardTop + 1.5, dy = System.Math.Abs(y - mid);
                double hw = 12 - 2.4 * System.Math.Max(0, dy - 0.3);
                if (d > hw) return '.';
                bool star = fine
                    ? d * 2.2 + dy < 1.05 || (d < 0.18 && dy < 1.9) || (dy < 0.18 && d < 1.7)
                    : d < 1 && dy < 1;
                if (star) return 'z';
                double rim = fine ? 0.28 : 0.6;
                if (hw - d < rim || 2 - dy < rim) return y < mid ? 'z' : 'g';
                return GeminiIce(d, y, fine);
            }
            if (y >= GeminiCollarTop - 0.5 && y < GeminiGripTop - 0.5)
                return d < 2.5 ? (fine && y < GeminiCollarTop ? 'j' : 'i') : '.';

            // The grip: dark leather, bound in a steel diamond wrap.
            if (y >= GeminiGripTop - 0.5 && y < GeminiPommelTop - 0.5)
            {
                if (d > 2) return '.';
                double w = fine ? 0.33 : 0.5;
                double p = ((y - GeminiGripTop + d) % 3 + 3) % 3, q = ((y - GeminiGripTop - d + 30) % 3 + 3) % 3;
                if (p < w || q < w) return fine && p < w * 0.5 ? 'q' : 'p';
                return d > 1.4 ? 'S' : 'D';
            }

            // The pommel: a silver cap lit from above, one spike each side, down and out.
            if (y >= GeminiPommelTop - 0.5 && y < SwordHeightRows - 0.5)
            {
                double r = y - GeminiPommelTop;
                double hw = r < 1.5 ? 2.5 : 1.7;
                if (d <= hw)
                {
                    if (!fine) return r < 0.5 ? '5' : r < 1.5 ? '4' : '3';
                    if (r < 0.3 || hw - d < 0.25) return r < 1 ? '5' : '2';
                    return r < 0.9 ? '5' : r < 1.8 ? '4' : '3';
                }
                const double ax = 2.0, ay = -0.2, bx = 6.0, by = 2.4;
                double vx = bx - ax, vy = by - ay, len2 = vx * vx + vy * vy;
                double t = System.Math.Max(0, System.Math.Min(1, ((d - ax) * vx + (r - ay) * vy) / len2));
                double dist = GeminiHypot(d - (ax + t * vx), r - (ay + t * vy));
                double rad = (fine ? 0.85 : 0.95) * (1 - t) + 0.12;
                if (dist < rad)
                {
                    if (!fine) return t < 0.5 ? '4' : '3';
                    double side = (d - ax) * vy - (r - ay) * vx;
                    return side < 0 ? (dist > rad - 0.25 ? '6' : '5') : (dist > rad - 0.25 ? '2' : '3');
                }
            }
            return '.';
        }

        // ---------------------------------------------------------------- the cradle

        static readonly double[,] GeminiCradle =
        {
            { 2.2, 66.5 }, { 8.0, 60.8 }, { 8.7, 57.6 }, { 6.8, 50.5 }, { 6.8, GeminiSerrateTop },
        };
        const double GeminiCradleHalf = 1, GeminiCradleTaper = 5;

        static char GeminiHalf(double d, double y, bool fine)
        {
            char s = GeminiSword(d, y, fine);
            return s != '.' ? s : GeminiCradleTexel(d, y, fine);
        }

        static bool GeminiGuardRow(double y) => y >= GeminiGuardTop - 0.5 && y < GeminiCollarTop - 0.5;

        static char GeminiCradleTexel(double d, double y, bool fine)
        {
            // Distance to the band's centre line, and the nearest point on it.
            double best = 1e9, px = 0, py = 0;
            for (int i = 0; i < GeminiCradle.GetLength(0) - 1; i++)
            {
                double ax = GeminiCradle[i, 0], ay = GeminiCradle[i, 1];
                double vx = GeminiCradle[i + 1, 0] - ax, vy = GeminiCradle[i + 1, 1] - ay;
                double t = System.Math.Max(0, System.Math.Min(1, ((d - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy)));
                double qx = ax + t * vx, qy = ay + t * vy, dist = GeminiHypot(d - qx, y - qy);
                if (dist < best) { best = dist; px = qx; py = qy; }
            }
            double tipT = System.Math.Min(1, (py - GeminiSerrateTop) / GeminiCradleTaper);   // 0 at the point
            double half = GeminiCradleHalf * System.Math.Pow(System.Math.Max(tipT, 0), 0.8) + 0.05;
            double sd = best - half;
            if (sd >= 0) return '.';
            bool outer = d > px || (y > py + 0.01 && py > GeminiGuardTop);

            // A dark gap where it meets the sword - but not the guard, which runs out into it.
            double g = fine ? 0.5 : 1;
            for (int k = 0; k < 8; k++)
            {
                double a = GeminiGapX[k] * g, b = GeminiGapY[k] * g;
                if (!GeminiGuardRow(y + b) && GeminiSword(System.Math.Abs(d + a), y + b, fine) != '.') return 'O';
            }
            double depth = -sd;
            if (!fine) return outer ? 'D' : 'S';
            if (depth < 0.4) return outer ? 'L' : 'K';                            // lit outside, dark within
            double centre = GeminiCradleHalf * System.Math.Pow(tipT, 0.8);
            if (System.Math.Abs(depth - centre) < 0.12 && 1 - tipT < 0.8) return 'o';   // inset steel line
            return outer ? 'D' : 'S';
        }

        static readonly int[] GeminiGapX = { 1, -1, 0, 0, 1, -1, 1, -1 };
        static readonly int[] GeminiGapY = { 0, 0, 1, -1, 1, 1, -1, -1 };

        /// <summary>
        /// The two halves for Solve et Coagula - the near hip's (the canvas's left columns) and the
        /// far's (the right) - each on the WHOLE canvas with the other's columns blank, the whole
        /// sword's grip as pivot. Placed on one pose they are the sword, texel for texel.
        /// </summary>
        static Sprite[] GeminiHalfBlades(Dictionary<char, Color> pal)
        {
            string[] Half(bool left)
            {
                var rows = new string[GeminiRows.Length];
                int mid = SwordCanvas / 2;
                for (int y = 0; y < rows.Length; y++)
                {
                    var r = GeminiRows[y];
                    rows[y] = left ? r.Substring(0, mid) + new string('.', SwordCanvas - mid)
                                   : new string('.', mid) + r.Substring(mid);
                }
                return rows;
            }
            return new[]
            {
                StageSprite("gear.weapon.gemini.half.near", Half(left: true), pal, GeminiGrip),
                StageSprite("gear.weapon.gemini.half.far", Half(left: false), pal, GeminiGrip),
            };
        }

        /// <summary>A Gemini Saya sheath's hang point - its mouth - from the sprite's centre, in
        /// world units (the item's HipSwingPivot).</summary>
        public static Vector2 GeminiSayaHang => GeminiSayaMouthFromCentre / BodyPpu;

        // ---------------------------------------------------------------- the display

        /// <summary>Cells of gap either side of the seam when the sword is hung up - 2 in all.</summary>
        const int GeminiDisplayGap = 1;
        const int GeminiDisplayCanvas = SwordCanvas + 2 * GeminiDisplayGap;

        /// <summary>Cells the near half stands ABOVE the middle and the far half below it - 4
        /// between them, so guard, grip and pommel no longer line up.</summary>
        const int GeminiDisplayStagger = 2;

        /// <summary>The held grip, moved to the middle of the wider, taller canvas - between the
        /// two halves' own grips.</summary>
        static Vector2Int GeminiDisplayGrip
            => new(GeminiGrip.x + GeminiDisplayGap, GeminiGrip.y + GeminiDisplayStagger);

        /// <summary>The whole field cut down the seam: the near (left) half where it was, the far
        /// half moved over by the gap and down by twice the stagger, nothing between.</summary>
        static char GeminiDisplayTexel(float x, float y, bool fine)
        {
            if (x < SwordAxis) return GeminiTexel(x, y, fine);
            if (x < SwordAxis + 2 * GeminiDisplayGap) return '.';
            return GeminiTexel(x - 2 * GeminiDisplayGap, y - 2 * GeminiDisplayStagger, fine);
        }

        /// <summary>The display grid, <paramref name="k"/> samples a cell each way (1 for the arena,
        /// <see cref="EmberDetailScale"/> for the menu) - taller than the held sword by the stagger.</summary>
        static string[] GeminiDisplayRows(int k)
        {
            var rows = new string[(SwordHeightRows + 2 * GeminiDisplayStagger) * k];
            var line = new char[GeminiDisplayCanvas * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++) line[X] = GeminiDisplayTexel((X + 0.5f) / k - 0.5f, y, k > 1);
                rows[Y] = new string(line);
            }
            return rows;
        }

        /// <summary>Down a Gemini Saya sheath, sprite-local (y up) - mouth to tip.</summary>
        public static Vector2 GeminiSayaAxis
            => new Vector2(GeminiSayaTip.x - GeminiSayaMouth.x, -(GeminiSayaTip.y - GeminiSayaMouth.y)).normalized;

        // ================================================================ the Gemini Saya
        //
        // ONE SHEATH PER PICTURE, 14 x 36 body texels - one at each hip (the user's call; it began
        // as a bound pair on one hip, https://claude.ai/artifact/MhDFLSxjJsj53Yd9Abg675). Mouth top
        // right on the belt, tip trailing back to the left, ten degrees at rest - walking swings it
        // further (PrimitiveCharacterRig.AnimateHipPieces). Black lacquer or frosted ice, the
        // blade's own materials, with a silver mouth, two silver bands and a silver tip. The FAR
        // hip's copy is a shade darker.

        const int GeminiSayaW = 14, GeminiSayaH = 36;
        static readonly Vector2 GeminiSayaMouth = new(9.8f, 1.5f), GeminiSayaTip = new(3.8f, 34.5f);

        /// <summary>The mouth's top relative to the sprite's centre, in body texels, y up - the
        /// point it hangs from on the belt.</summary>
        static Vector2 GeminiSayaMouthFromCentre
            => new(GeminiSayaMouth.x - (GeminiSayaW - 1) * 0.5f, (GeminiSayaH - 1) * 0.5f);

        /// <summary>The layer's Offset, in layout cells: wherever puts the mouth on the belt at the
        /// near hip (-x, the pouch's own hip), or the far one (+x).</summary>
        static Vector2 GeminiSayaOffsetCells(bool far)
        {
            float hipX = far ? SayaMouthAt.x : -SayaMouthAt.x;
            return new(PrimitiveCharacterRig.Proportions.Cells(hipX - GeminiSayaMouthFromCentre.x),
                       PrimitiveCharacterRig.Proportions.Cells(SayaMouthAt.y - GeminiSayaMouthFromCentre.y));
        }

        static Dictionary<char, Color> GeminiSayaPal()
        {
            var map = new Dictionary<char, Color>();
            void Put(string keys, params string[] hex)
            {
                for (int i = 0; i < keys.Length; i++)
                    map[keys[i]] = ColorUtility.TryParseHtmlString(hex[i], out var c) ? c : Color.magenta;
            }
            Put("123456", "#3b4252", "#5e6779", "#8992a5", "#b8c0ce", "#e0e5ee", "#ffffff");   // silver
            Put("fgijz", "#2f6a9a", "#5b9ccc", "#93c8ee", "#cfeafd", "#ffffff");               // ice
            Put("PRTUV", "#040405", "#15161b", "#22242b", "#3a3d47", "#5a5e6b");               // black lacquer
            return map;
        }

        /// <summary>One shade darker, for the far hip.</summary>
        static char GeminiSayaFar(char c) => c switch
        {
            '6' => '5', '5' => '4', '4' => '3', '3' => '2', '2' => '1',
            'U' => 'T', 'V' => 'U', 'T' => 'R', 'R' => 'P',
            'z' => 'j', 'j' => 'i', 'i' => 'g', 'g' => 'f',
            _ => c,
        };

        static string[] GeminiSayaRows(bool frosted, bool far)
        {
            var rows = new string[GeminiSayaH];
            var line = new char[GeminiSayaW];
            for (int y = 0; y < GeminiSayaH; y++)
            {
                for (int x = 0; x < GeminiSayaW; x++)
                {
                    char c = GeminiSayaTexel(x, y, false, frosted);
                    line[x] = far ? GeminiSayaFar(c) : c;
                }
                rows[y] = new string(line);
            }
            return rows;
        }

        static char GeminiSayaTexel(double x, double y, bool fine, bool frosted)
        {
            double ax = GeminiSayaTip.x - GeminiSayaMouth.x, ay = GeminiSayaTip.y - GeminiSayaMouth.y;
            double len = GeminiHypot(ax, ay);
            double ux = ax / len, uy = ay / len, vx = -uy, vy = ux;
            double t = (x - GeminiSayaMouth.x) * ux + (y - GeminiSayaMouth.y) * uy;   // 0 at the mouth
            // Across, signed so NEGATIVE is the left - the lit side, as everywhere in this file.
            double s = -((x - GeminiSayaMouth.x) * vx + (y - GeminiSayaMouth.y) * vy);
            return GeminiSheath(s, t, len, fine, frosted);
        }

        /// <summary>One sheath, in its own across (<paramref name="sc"/>) and along
        /// (<paramref name="t"/>) coordinates: tapering a little to a rounded tip, silver at the
        /// mouth, two bands and the tip.</summary>
        static char GeminiSheath(double sc, double t, double len, bool fine, bool frosted)
        {
            const double tubeW = 2.6;
            if (t < -0.6 || t > len + 0.2) return '.';
            double half = tubeW * (1 - 0.18 * (t / len));
            double tipRound = t > len - half
                ? System.Math.Sqrt(System.Math.Max(0, half * half - (t - (len - half)) * (t - (len - half))))
                : half;
            if (System.Math.Abs(sc) > tipRound) return '.';
            double a = sc / half;                                             // -1..1 across

            bool band = System.Math.Abs(t - len * 0.36) < 0.8 || System.Math.Abs(t - len * 0.66) < 0.8;
            if (t < 1.6 || band || t > len - 2.2)
            {
                if (t < 0.4 && System.Math.Abs(a) < 0.62) return 'P';         // the open mouth
                if (!fine) return a < -0.2 ? '5' : a > 0.5 ? '3' : '4';
                return a < -0.55 ? '6' : a < -0.1 ? '5' : a > 0.6 ? '2' : '4';
            }
            if (!frosted)                                                     // black lacquer
            {
                if (!fine) return a < -0.35 ? 'U' : a > 0.5 ? 'R' : 'T';
                if (a > -0.62 && a < -0.38) return 'V';
                return a < -0.75 ? 'U' : a > 0.55 ? 'R' : 'T';
            }
            if (fine && a < -0.8) return 'z';                                 // frosted: a lit edge, then ice
            return GeminiSayaIce(sc + 10, t, fine, 0.3);
        }

        /// <summary>The blade's ice at the saya's scale: shorter facets, leaning along the sheath.</summary>
        static char GeminiSayaIce(double x, double y, bool fine, double lean)
        {
            const double sx = 1.45, sy = 3.0;
            double u = x + y * lean;
            double gi = System.Math.Floor(u / sx), gj = System.Math.Floor(y / sy);
            double best = 1e9, second = 1e9, id = 0, px = 0, py = 0;
            for (int a = -1; a <= 1; a++)
            for (int b = -1; b <= 1; b++)
            {
                double i = gi + a, j = gj + b;
                double cx = (i + 0.2 + 0.6 * GeminiHash(i + 31, j)) * sx;
                double cy = (j + 0.2 + 0.6 * GeminiHash(j, i + 57)) * sy;
                double dd = GeminiHypot(u - cx, (y - cy) * 0.5);
                if (dd < best) { second = best; best = dd; id = i * 131 + j; px = cx; py = cy; }
                else if (dd < second) second = dd;
            }
            if (second - best < (fine ? 0.1 : 0.14)) return GeminiHash(id, 3) > 0.55 ? 'z' : 'g';
            bool lit = (u - px) + (y - py) * 0.25 < 0;
            if (GeminiHash(id, 13) > 0.86) return lit ? 'i' : 'g';
            return lit ? 'j' : 'i';
        }
    }
}
