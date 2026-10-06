using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== the Wraithguard Cloak: a wrapped mantle
    //
    // Reworked after the user's hooded-knight reference (the user's calls: keep the crimson, keep
    // the shared hood). What the reference's cloak has that the old one did not:
    //
    //     A BUNCHED WRAP     two rolls of cloth gathered round the throat, inside the hood's
    //                        opening, running across the chest to the bare shoulder's collar
    //     FOLDS              the cloth over the cloaked shoulder falls in broad DIAGONAL planes
    //                        from the neck down and out, not as one flat panel
    //     A LONG, TORN CAPE  to the calf, the hem torn into points; the drape in front ends at
    //                        the hip in its own ragged hem, so the cape is seen continuing below
    //
    // Built FROM the old cloak, not beside it: the drape keeps its footprint (the cap over the
    // pauldron envelope, the panel outside the arm from x -9, the outer edge at -14), its top
    // silhouette over the shoulder, and its place in the rig - BackOver, authored on -X, mirrored
    // to the shoulder opposite the sword arm and bent with the cape (SyncDrapeSide,
    // DrapeSwingsWithCape). The cape keeps CapeRows' sloped shoulders and width at the top.
    //
    // Two fields in the TORSO's frame, in CELLS, y up from the waist (the Hellspawn Cape's frame):
    // the chin at 20, the neck 22, the knee -12, the soles -24; the arm's outer edge at -9.5.
    // Sampled at BodyPpu. Cloth rules throughout: no glow tone, no crease dashes - folds are whole
    // planes several texels wide.
    public static partial class DemoGear
    {
        const string WgCloth = "sdbl";

        // ------------------------------------------------------------------ the cape (Back)

        const float WgCapeTop = 25.5f, WgCapeBottom = -21f, WgCapeHalfW = 15f;
        /// <summary>The solid cloth ends here; below it only the torn points hang.</summary>
        const float WgCapeHemLine = -12.5f;

        /// <summary>CapeRows' width over the shoulders, flaring below the chest.</summary>
        static float WgCapeHalf(float y) => y >= 12f ? 11.5f : 11.5f + (12f - y) * 0.11f;

        /// <summary>CapeRows' sloped top: full height behind the head, down to the shoulder line.</summary>
        static float WgCapeTopAt(float ax) => ax <= 5f ? 25f : 25f - (ax - 5f) * (4f / 6.5f);

        static List<(float X, float Drop)> _wgTatters;
        static List<(float X, float Drop)> WgTatters
        {
            get
            {
                if (_wgTatters != null) return _wgTatters;
                var rng = new System.Random(4113);
                float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
                _wgTatters = new List<(float, float)>();
                for (float x = -WgCapeHalfW + 0.8f; x < WgCapeHalfW - 0.8f; x += R(2.4f, 3.8f))
                    _wgTatters.Add((x, rng.NextDouble() < 0.35 ? R(4.5f, 7.5f) : R(1.8f, 4f)));
                return _wgTatters;
            }
        }

        static float WgCapeBottomAt(float x)
        {
            float bottom = WgCapeHemLine;
            foreach (var t in WgTatters)
                bottom = Mathf.Min(bottom, WgCapeHemLine - t.Drop + Mathf.Abs(x - t.X) * 2.2f);
            return Mathf.Max(bottom, WgCapeBottom + 0.5f);
        }

        /// <summary>The lower side edges, notched in and out (Hellspawn's rag, shallower).</summary>
        static float WgCapeSideRag(float y, bool left)
        {
            if (y > 0f) return 0f;
            float p = Mathf.Repeat(y / 2.9f + (left ? 0.21f : 0.66f), 1f);
            return (Mathf.Abs(p - 0.5f) * 2f - 0.5f) * 1.8f * Mathf.InverseLerp(0f, -8f, y);
        }

        static bool WgCapeIn(float x, float y)
        {
            float ax = Mathf.Abs(x);
            return y <= WgCapeTopAt(ax) && y >= WgCapeBottomAt(x)
                && ax <= WgCapeHalf(y) + WgCapeSideRag(y, x < 0f);
        }

        /// <summary>The cape's tone at (x, y), whether or not there is cloth there - the back view
        /// of the drape reads its colour off this beside the cape's edge.</summary>
        static int WgCapeTone(float x, float y)
        {
            float ax = Mathf.Abs(x);
            float top = WgCapeTopAt(ax), half = WgCapeHalf(y);
            if (top - y < 1.4f) return 3;                                       // light on the shoulders
            // Broad fan folds, widening with the flare; the shade line dips under each ridge.
            float u = x / half;
            float fold = Mathf.Sin(u * Mathf.PI * 2.5f + 0.4f) * Mathf.Clamp01((17f - y) / 24f);
            int tone = y > 7f - fold * 10f ? 2 : 1;
            // A ridge lifts shadow to base, never base to light: where a ridge's tip just clears
            // the shade line it left single lit texels, specks on the back of the cape.
            if (fold > 0.6f && tone == 1) tone++;
            else if (fold < -0.8f) tone--;
            if (half - ax < 1.1f) tone += x < 0f ? 1 : -1;
            if (y - WgCapeBottomAt(x) < 1.4f) tone = 0;                          // the torn edge in shade
            return Mathf.Clamp(tone, 0, 3);
        }

        static string[] _wgCapeRows;
        static string[] WgCapeRows => _wgCapeRows ??= StripSpecks(SampleCells(
            -WgCapeHalfW, WgCapeHalfW, WgCapeBottom, WgCapeTop,
            (x, y) => WgCapeIn(x, y) ? WgCloth[WgCapeTone(x, y)] : '.'));

        // ------------------------------------------------------------------ the drape (BackOver)
        //
        // Authored on -X, the cloaked shoulder. Three regions of one cloth:
        //
        //   the WRAP     x -8..5.5, from under the chin down to a lower edge that drops toward the
        //                cloaked side; two rolls, each lit along its top and tucked under at its
        //                foot. It ends short of the bare shoulder's pauldron, over its inner edge.
        //   the CAP      over the cloaked shoulder, the old drape's top silhouette, its hem 10.5 at
        //                the arm rising inward to meet the wrap
        //   the PANEL    outside the arm (x < -9) down to the hip, ending in torn points
        //
        // The cap and panel are shaded in DIAGONAL planes falling from the neck down and out.

        const float WgDrapeTop = 22f, WgDrapeBottom = -10f, WgDrapeMinX = -16f, WgDrapeMaxX = 6f;
        const float WgWrapEnd = 5.5f, WgWrapOut = -8f, WgPanelInner = -9f;
        const float WgPanelHemLine = -3f;

        /// <summary>The wrap's lower edge: lower toward the cloaked side, rounding up at its end.</summary>
        static float WgWrapBottom(float x)
        {
            float b = 14.5f + 0.35f * x;
            if (x > 3.5f) b += (x - 3.5f) * 1.6f;
            return b;
        }

        /// <summary>Where the upper roll meets the lower one.</summary>
        static float WgRollSplit(float x) => WgWrapBottom(x) + 3.2f - Mathf.Max(0f, x - 3.5f) * 0.8f;

        /// <summary>The old drape's top over the shoulder: level behind the hood, falling at the arm.</summary>
        static float WgDrapeTopAt(float x)
        {
            if (x >= -9.5f) return 21.5f;
            float t = (-9.5f - x) / 4.5f;
            return 21.5f - t * t * 2.6f;
        }

        static float WgDrapeOuter(float y) => -14f - Mathf.Max(0f, 12f - y) * 0.06f;

        /// <summary>The cap's hem inside the arm, rising inward to meet the wrap.</summary>
        static float WgCapHem(float x) => 10.5f + (x - WgPanelInner) * 0.49f;

        static List<(float X, float Drop)> _wgPanelTatters;
        static List<(float X, float Drop)> WgPanelTatters => _wgPanelTatters ??= new()
        {
            (-14.2f, 3.6f), (-12.4f, 1.6f), (-10.8f, 4.8f), (-9.3f, 1.2f),
        };

        static float WgPanelBottom(float x)
        {
            float bottom = WgPanelHemLine + 0.6f * (x - WgPanelInner);         // higher at the arm
            foreach (var t in WgPanelTatters)
                bottom = Mathf.Min(bottom, WgPanelHemLine - t.Drop + Mathf.Abs(x - t.X) * 2.4f);
            return bottom;
        }

        static bool WgDrapeIn(float x, float y)
        {
            if (x > WgWrapEnd || y > WgDrapeTopAt(x) || x < WgDrapeOuter(y)) return false;
            if (x >= WgWrapOut && y >= WgWrapBottom(x)) return true;            // the wrap
            if (x >= WgPanelInner) return x < WgWrapOut + 5f && y >= WgCapHem(x); // the cap
            return y >= WgPanelBottom(x);                                       // the panel
        }

        static bool WgWrapIn(float x, float y) => x >= WgWrapOut && x <= WgWrapEnd && y >= WgWrapBottom(x);

        static char WgDrapeTexel(float x, float y)
        {
            if (!WgDrapeIn(x, y)) return '.';

            // ---- the wrap: two rolls ----
            if (WgWrapIn(x, y))
            {
                const float h = 0.5f;                                           // one texel, in cells
                // Its outer end lies ON the shoulder cloth: a tucked edge, in shade.
                if (x < WgWrapOut + h) return WgCloth[0];
                float split = WgRollSplit(x), bottom = WgWrapBottom(x);
                bool upper = y >= split;
                float foot = upper ? split : bottom;
                float rollTop = upper ? 21.5f : split;
                if (y < foot + h) return WgCloth[0];                            // tucked under
                if (y < foot + 2f * h) return WgCloth[1];
                if (rollTop - y < 2f * h) return WgCloth[3];                    // lit along its top
                return WgCloth[x > 2f ? 3 : 2];                                 // turned toward the light
            }

            // ---- the cap and panel: diagonal planes from the neck down and out ----
            float top = WgDrapeTopAt(x);
            if (top - y < 0.9f) return WgCloth[3];                              // light on the shoulder
            float v = x * 0.94f - y * 0.34f;                                    // across the folds
            // Wide planes, few of them: at a 3.6-cell period the lit ridges came out as thin
            // parallel stripes - crease marks by another name.
            float fold = Mathf.Sin(v * (2f * Mathf.PI / 5.5f) + 1.1f);
            int t = 2;
            if (fold > 0.55f) t = 3;
            else if (fold < -0.25f) t = 1;
            if (y < 5f && t > 0) t--;                                           // darker as it falls
            if (x < WgDrapeOuter(y) + 0.6f) t = Mathf.Min(t, 1);                // turning away at the edge
            if (x >= WgPanelInner && y < WgCapHem(x) + 0.6f) t = 0;             // the cap's hem
            if (x < WgPanelInner && y < WgPanelBottom(x) + 0.6f) t = 0;         // the panel's torn hem
            return WgCloth[Mathf.Clamp(t, 0, 3)];
        }

        static string[] _wgDrapeRows;
        static string[] WgDrapeRows => _wgDrapeRows ??= StripSpecks(SampleCells(
            WgDrapeMinX, WgDrapeMaxX, WgDrapeBottom, WgDrapeTop, WgDrapeTexel));

        /// <summary>
        /// The drape seen from BEHIND, on the same grid. The cape is the outermost garment there and
        /// covers everything inside its own outline, so this is only the cloth that overhangs it -
        /// the strip past the cape's edge over the cloaked shoulder - in the cape's OWN tone beside
        /// it, and stroked ('o', the outline's ink) everywhere except where it butts onto the cape,
        /// so the two draw one silhouette with no seam between them. Derived from the two fields,
        /// so it cannot disagree with either.
        /// </summary>
        static string[] _wgDrapeBackRows;
        static string[] WgDrapeBackRows => _wgDrapeBackRows ??= SampleCells(
            WgDrapeMinX, WgDrapeMaxX, WgDrapeBottom, WgDrapeTop, (x, y) =>
            {
                bool Strip(float px, float py) => WgDrapeIn(px, py) && !WgCapeIn(px, py);
                bool Open(float px, float py) => !WgDrapeIn(px, py) && !WgCapeIn(px, py);
                if (!Strip(x, y)) return '.';
                const float s = 0.5f;
                for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) <= 2 && Open(x + dx * s, y + dy * s)) return 'o';
                // The cape's tone just INSIDE its edge - its edge column is lit, and the strip
                // drawn in that read as a pale stripe rather than the same cloth carrying on.
                float edge = -WgCapeHalf(y) + 1.3f;
                return WgCloth[WgCapeTone(edge, Mathf.Min(y, WgCapeTopAt(Mathf.Abs(edge)) - 1.5f))];
            });

        /// <summary>Sample a field in torso CELLS at BodyPpu (two texels a cell), top row first.</summary>
        static string[] SampleCells(float xMin, float xMax, float yMin, float yMax, System.Func<float, float, char> at)
        {
            int w = Mathf.RoundToInt((xMax - xMin) * 2f), h = Mathf.RoundToInt((yMax - yMin) * 2f);
            var rows = new string[h];
            var sb = new System.Text.StringBuilder(w);
            for (int row = 0; row < h; row++)
            {
                sb.Clear();
                for (int col = 0; col < w; col++)
                    sb.Append(at(xMin + (col + 0.5f) / 2f, yMax - (row + 0.5f) / 2f));
                rows[row] = sb.ToString();
            }
            return rows;
        }

        // ------------------------------------------------------------------ the item

        static GearItem WraithCloak(Dictionary<char, Color> cloth, Dictionary<char, Color> clothInk, Color hoodColor)
        {
            var item = Hood(Make("wraith_cloak", "Wraithguard Cloak", GearSlot.Back, LootTier.Gold, 4f,
                Pixels(RigLayer.Back, "gear.back.wraith.cape", WgCapeRows, cloth,
                       0f, (WgCapeTop + WgCapeBottom) / 2f, ppu: BodyPpu),
                // The shared cowl - black_hood's construction on the crimson (the user's call).
                Pixels(RigLayer.Hood, "gear.back.wraith.cowl", HoodRows, clothInk, 0f, HoodY, ppu: BodyPpu,
                       outline: false),
                // THE ASYMMETRY LIVES HERE, on the cloak, not on a pauldron: authored on -X; the
                // rig mirrors it onto the shoulder OPPOSITE the sword arm and bends it with the
                // cape (DrapeSwingsWithCape) - see SyncDrapeSide.
                Pixels(RigLayer.BackOver, "gear.back.wraith.drape", WgDrapeRows, cloth,
                       (WgDrapeMinX + WgDrapeMaxX) / 2f, (WgDrapeTop + WgDrapeBottom) / 2f, ppu: BodyPpu)),
                hoodColor);
            // PixelsCore, like HoodBack: a back view is not one of the item's own layers, so it
            // stays out of _sources and the menu-art pass.
            item.DrapeBack = PixelsCore(RigLayer.BackOver, "gear.back.wraith.drape.back", WgDrapeBackRows, clothInk,
                (WgDrapeMinX + WgDrapeMaxX) / 2f, (WgDrapeTop + WgDrapeBottom) / 2f, ppu: BodyPpu, outline: false);
            item.DrapeSwingsWithCape = true;
            // Hinged at the neck (22), not the sprite's top, which stands up behind the head -
            // measured from the DRAWN top, outline included (the Hellspawn Cape's rule).
            item.CapeHingeCells = WgCapeTop + PrimitiveCharacterRig.Proportions.Cells(PixelSprite.OutlineCanvasPadFor(BodyPpu))
                                  - PrimitiveCharacterRig.Proportions.NeckYCells;
            // To the calf: the full swing sweeps a hem this long too far sideways.
            item.CapeSwingScale = 0.7f;
            return item;
        }

        /// <summary>
        /// The Drifter's Shroud: a NECK piece after the user's desert-ranger reference - red cloth
        /// bunched round the throat and thrown over the shoulders. Started FROM the Wraithguard's
        /// drape with the hood and cape taken off (the user's call), now its own MANTLE over both
        /// shoulders (ShroudIn), shaded as one woven cloth (ShroudTexel), on
        /// <see cref="RigLayer.NeckOver"/> - worn over whatever cloak is on, from behind too, and
        /// over a hood (ShroudOverCloak). It hides both pauldrons, as a hood clips the mane - see
        /// PrimitiveCharacterRig.SyncShroudSide.
        /// </summary>
        static GearItem DrifterShroud()
        {
            // BROWN, unlike the reference's red (the user's call) - a dusty earth brown, a shade
            // warmer and lighter than the Wanderer's Hood so the two don't read as one cloth.
            var ramp = new Palette.Ramp(new Color(0.47f, 0.33f, 0.22f));
            var pal = Palette.Of(ramp);
            // The weave's UNDER threads (see Woven): each tone part way to the next one down -
            // a full step would turn the weave into a checkerboard louder than the folds.
            pal['L'] = Color.Lerp(ramp.Light, ramp.Base, WeaveDepth);
            pal['B'] = Color.Lerp(ramp.Base, ramp.Dark, WeaveDepth);
            pal['D'] = Color.Lerp(ramp.Dark, ramp.Deep, WeaveDepth);
            pal['S'] = Color.Lerp(ramp.Deep, ramp.Line, WeaveDepth);
            var item = Make("drifter_shroud", "Drifter's Shroud", GearSlot.Neck, LootTier.Gold, 0f,
                Pixels(RigLayer.NeckOver, "gear.neck.shroud", ShroudRows, pal,
                       0f, (ShroudTopY + ShroudBottomY) / 2f, ppu: BodyPpu));
            // Over the cloak from behind as well as in front, and over hoods in both facings
            // (the user's calls for this piece).
            item.ShroudOverCloak = true;
            return item;
        }

        /// <summary>How far an under-thread drops toward the next tone down.</summary>
        const float WeaveDepth = 0.3f;

        /// <summary>
        /// ONE PIECE OF CLOTH (the user's call): no rolls, no tucks, no hem line, no fold planes.
        /// Shaded by depth below the top edge only, so the tone bands follow the cloth down from
        /// the shoulders and nothing cuts across it: lit along the top, base over the shoulders,
        /// dark lower down, deep only at the longest torn points.
        /// </summary>
        static char ShroudTexel(float x, float y)
        {
            if (!ShroudIn(x, y)) return '.';
            float d = ShroudTop(x) - y;
            int t = d < 1f ? 3 : d < 7f ? 2 : d < 14f ? 1 : 0;
            return WgCloth[t];
        }

        // ---- the shape: a MANTLE OVER BOTH SHOULDERS ----
        //
        // After the reference (the user's call): cloth bunched round the throat and falling over
        // BOTH shoulders to about the elbow, torn all along its hem. The Wraithguard's one-shoulder
        // drape was the starting point and was dropped once the shroud went over hoods: its wrap
        // ended at x 5.5, which the cloak's own hood hides under the head, and over a hood it sat
        // on the face as a flap attached to nothing. This one runs level across the throat past
        // the widest hood (~11 cells out at that height) on both sides, so it reads as wound round
        // the neck from either facing.
        //
        // In the drape's frame and on its rule: authored with the LONGER side on -X, which the
        // rig mirrors to the shoulder opposite the sword arm (SyncShroudSide).

        const float ShroudHalfW = 15f, ShroudTopY = 22f, ShroudBottomY = 0f;
        /// <summary>Where the hem sits over each arm - the -X (free) side hangs longer.</summary>
        const float ShroudArmHemNear = 4f, ShroudArmHemFar = 7f;

        static List<(float X, float Drop)> _shroudTatters;
        static List<(float X, float Drop)> ShroudTatters
        {
            get
            {
                if (_shroudTatters != null) return _shroudTatters;
                var rng = new System.Random(2251);
                float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
                _shroudTatters = new List<(float, float)>();
                for (float x = -ShroudHalfW + 1f; x < ShroudHalfW - 1f; x += R(2.2f, 3.4f))
                    _shroudTatters.Add((x, rng.NextDouble() < 0.3 ? R(2.4f, 3.4f) : R(0.8f, 1.8f)));
                return _shroudTatters;
            }
        }

        /// <summary>The top edge: level across the throat and both collarbones, rounding down
        /// over each shoulder - the Wraithguard drape's own shoulder curve, both sides. BOXY on
        /// purpose (the user's call): sloped shoulders and a lower chest were tried and read
        /// worse than this.</summary>
        static float ShroudTop(float x)
        {
            float ax = Mathf.Abs(x);
            if (ax <= 9.5f) return 21.5f;
            float t = (ax - 9.5f) / 4.5f;
            return 21.5f - t * t * 2.6f;
        }

        /// <summary>The outer edges, flaring a little as the cloth falls.</summary>
        static float ShroudOuter(float y) => 14f + Mathf.Max(0f, 12f - y) * 0.06f;

        /// <summary>The hem before tearing: across the chest the wrap's own slant (lower toward the
        /// free side), falling to elbow height over each arm.</summary>
        static float ShroudHemLine(float x)
        {
            float chest = 13.5f + 0.35f * x;
            float arm = x < 0f ? ShroudArmHemNear : ShroudArmHemFar;
            return Mathf.Lerp(chest, arm, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, 9.5f, Mathf.Abs(x))));
        }

        static float ShroudBottom(float x)
        {
            float line = ShroudHemLine(x), bottom = line;
            foreach (var t in ShroudTatters)
                bottom = Mathf.Min(bottom, line - t.Drop + Mathf.Abs(x - t.X) * 2.4f);
            return bottom;
        }

        static bool ShroudIn(float x, float y)
            => Mathf.Abs(x) <= ShroudOuter(y) && y <= ShroudTop(x) && y >= ShroudBottom(x);

        static string[] _shroudRows;
        static string[] ShroudRows => _shroudRows ??= Woven(SampleCells(
            -ShroudHalfW, ShroudHalfW, ShroudBottomY, ShroudTopY, ShroudTexel));

        /// <summary>
        /// WOVEN CLOTH as a PLAIN weave: every other texel, checkerboard-wise, is a thread passing
        /// under - its cloth tone ('sdbl') swapped for its under-thread key ('SDBL'). One texel
        /// each and no direction, so it reads as the grain of one cloth rather than a pattern laid
        /// on it (a 2x2 basket weave, alternating across and down, broke the shroud into patches).
        /// </summary>
        static string[] Woven(string[] rows)
        {
            var woven = new string[rows.Length];
            var sb = new System.Text.StringBuilder();
            for (int r = 0; r < rows.Length; r++)
            {
                sb.Clear();
                for (int c = 0; c < rows[r].Length; c++)
                {
                    char ch = rows[r][c];
                    bool under = (r + c) % 2 == 1;
                    sb.Append(under && WgCloth.IndexOf(ch) >= 0 ? char.ToUpperInvariant(ch) : ch);
                }
                woven[r] = sb.ToString();
            }
            return woven;
        }
    }
}
