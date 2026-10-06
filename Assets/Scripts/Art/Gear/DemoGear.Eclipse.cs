using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Eclipse Blades: a total eclipse, and two moons
    //
    // After the user's sketch: an oval disc rimmed in clear GLASS holding a total eclipse -
    // a black moon, a white corona streaming off it and fading to grey at the rim - and behind it
    // two large TRANSLUCENT YELLOW crescents edged in the same glass, apart at the top and
    // tucking behind the disc at the bottom. (The sketch drew them in silver; the user moved every
    // edge to glass.) The note on the sketch: the white light
    // SHIMMERS, with parallax.
    //
    // Measured off the sketch in its own pixels and scaled into cells (layout cells; see EclipseK). Each crescent
    // is an outer ellipse minus an inner one, and the two stand apart at the centre line - they
    // cross at the notch in the sketch, but the user wants them disconnected.
    //
    // The sketch has no handle. The hand takes the central disc's glass rim at the TOP, where a
    // short leather wrap marks the grip: the fist is painted over the grip at the pivot
    // (SetWeaponSplit's note), and anywhere lower it would cover the eclipse.
    //
    // The corona is ANIMATED (EclipseFrames, arena only): each ray pulses on its own phase, and the
    // corona's glow drifts a little against the moon, which holds still - the parallax. Every
    // term is a whole number of cycles per loop, so the flipbook loops clean. SetWeaponSprite
    // carries each frame to the off-hand copy.
    //
    // Mirror-safe at rest: the lobes are built as a left/right pair and the rays read |x|, so the
    // unmirrored off-hand copy is the same picture. ONE field (EclipseTexel), sampled per cell for
    // the arena and per quarter cell for the menu.
    public static partial class DemoGear
    {
        /// <summary>
        /// The GRID is the disc standard tall (DiscHeightCells, 28) and 32 wide. Everything below
        /// is MEASURED in the sketch's own layout (30 x 26 cells, the size it was first built at)
        /// and the field is sampled through <see cref="EclipseK"/>, so the design is one set of
        /// numbers at any size. 16/15 puts the layout's 25.5-cell height at 27.2 and keeps the
        /// centre line (layout 15) on a whole cell (16).
        /// </summary>
        const int EclipseW = 32, EclipseH = DiscHeightCells;
        const float EclipseK = 16f / 15f;
        const float EclipseAxis = 15f;                     // the centre line: layout 15 = grid 16, a texel boundary

        /// <summary>The central disc: an oval, glass rim included.</summary>
        static readonly Vector2 EclipseDiscCentre = new(EclipseAxis, 16.4f);
        const float EclipseDiscA = 7.6f, EclipseDiscB = 8.6f;
        /// <summary>The disc's glass rim - thicker than the sketch's line, which would be under a
        /// cell in the arena. In layout cells, sized to stay 1.4 REAL cells after EclipseK: a rim
        /// that shrank with the design dropped to one cell and broke up.</summary>
        const float EclipseRim = 1.4f / EclipseK;
        /// <summary>The moon's radius, in cells. At 4.8 the corona had under two cells to fall off
        /// in and never reached the sketch's grey at the rim.</summary>
        const float EclipseMoon = 4.0f;

        // The crescents, measured off the sketch: the LEFT lobe's outer and inner ellipses (the
        // right is the mirror image), in cells.
        static readonly Vector2 EclipseLobeOuter = new(9.37f, 13.4f);
        const float EclipseLobeOuterA = 8.96f, EclipseLobeOuterB = 12.1f;
        static readonly Vector2 EclipseLobeInner = new(12.16f, 13.2f);
        const float EclipseLobeInnerA = 6.75f, EclipseLobeInnerB = 9.2f;
        /// <summary>The glass edging round each crescent - layout cells, kept at 0.9 real cells
        /// after EclipseK, like the rim.</summary>
        const float EclipseEdge = 0.9f / EclipseK;

        /// <summary>Half the gap between the crescents at the centre line, in layout cells.</summary>
        const float EclipseGapHalf = 0.7f;

        /// <summary>Half the width of the leather wrap on the rim, in cells.</summary>
        const float EclipseWrapHalf = 2.2f;

        const int EclipseDetailScale = 4;
        /// <summary>On the rim at its top - see the header.</summary>
        static readonly Vector2Int EclipseGrip = new(Mathf.RoundToInt(EclipseAxis * EclipseK), 9);
        static readonly Vector2Int EclipseDetailGrip = new(EclipseGrip.x * EclipseDetailScale,
                                                           EclipseGrip.y * EclipseDetailScale);

        const int EclipseFrameCount = 24;
        const float EclipseFrameSeconds = 0.125f;          // a three-second loop

        /// <summary>
        /// Approximate signed distance to an ellipse's edge, in cells - negative inside - and the
        /// outward normal (rows DOWN). First-order (f - 1) / |grad f|: exact on the edge, which is
        /// the only place the bands measured with it need it to be.
        /// </summary>
        static float EclipseEllipse(float x, float y, Vector2 c, float a, float b, out Vector2 n)
        {
            float px = (x - c.x) / a, py = (y - c.y) / b;
            float f = Mathf.Sqrt(px * px + py * py);
            if (f < 1e-4f) { n = Vector2.up; return -Mathf.Min(a, b); }
            var g = new Vector2(px / a, py / b) / f;
            float gl = g.magnitude;
            n = g / gl;
            return (f - 1f) / gl;
        }

        static Vector2 EclipseMirror(Vector2 p) => new(2f * EclipseAxis - p.x, p.y);

        /// <summary>
        /// Clear GLASS, for every edge - the disc's rim, the crescents' edging and their cut ends.
        /// Glass reads by its EDGES (Saint's lesson): a dense bright line along both sides of the
        /// band, a nearly clear middle, a highlight streak on the side facing the light and a
        /// darker refraction line on the side away from it. The arena has a cell or so of band -
        /// no room for a clear middle, which would just read as a hole - so there it is the
        /// dense edge tone, lit where it faces the light.
        /// <paramref name="across"/> runs 0..1 over the band.
        /// </summary>
        static char EclipseGlass(Vector2 nDown, float across, bool menu, float px, float bandCells)
        {
            var n = new Vector2(nDown.x, -nDown.y);                           // to y-up
            float val = Vector2.Dot(n, new Vector2(-0.7071f, 0.7071f));
            if (!menu) return val > 0.35f ? 'u' : 'g';
            float t = across * bandCells;                                       // cells into the band
            if (t < 1.1f * px || t > bandCells - 1.1f * px) return 'g';
            if (val > 0.1f && Mathf.Abs(t - bandCells * 0.32f) < 0.7f * px) return 'u';
            if (val < -0.1f && Mathf.Abs(t - bandCells * 0.68f) < 0.7f * px) return 'j';
            return 'q';
        }

        static float EclipseHash(int i)
        {
            unchecked
            {
                int h = i * 374761393 + 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>
        /// The whole piece at (x, y), rows DOWN, at <paramref name="scale"/> texels per cell;
        /// <paramref name="phase"/> 0..1 through the shimmer loop. Front to back: the wrap, the
        /// central disc (rim, corona, moon), the crescents' clear-glass edging, their yellow glass.
        /// </summary>
        static char EclipseTexel(float x, float y, int scale, float phase)
        {
            bool menu = scale > 1;
            // (x, y) arrive in GRID cells; the design is measured in layout cells
            x /= EclipseK;
            y /= EclipseK;
            float px = 1f / (scale * EclipseK);                            // a texel, in layout cells
            float tau = 2f * Mathf.PI;

            // ---- the central disc
            float dDisc = EclipseEllipse(x, y, EclipseDiscCentre, EclipseDiscA, EclipseDiscB, out var nDisc);
            if (dDisc <= 0f)
            {
                float intoRim = -dDisc;
                if (intoRim < EclipseRim)
                {
                    // the wrap, where the hand takes the rim
                    if (y < EclipseDiscCentre.y && Mathf.Abs(x - EclipseAxis) < EclipseWrapHalf)
                    {
                        if (!menu) return 'B';
                        float w = (Mathf.Abs(x - EclipseAxis) + intoRim * 0.9f) / (4f * px);
                        float f = w - Mathf.Floor(w);
                        return f < 0.22f ? 'S' : f < 0.4f ? 'L' : 'B';
                    }
                    // a dark line inside the rim in the menu, or the glass runs into the white
                    // corona as one band
                    if (menu && intoRim > EclipseRim - 1.1f * px) return 'j';
                    return EclipseGlass(nDisc, intoRim / EclipseRim, menu, px, EclipseRim);
                }

                // the moon holds still; the corona's glow drifts round it - the parallax
                var m = EclipseDiscCentre;
                float r = Vector2.Distance(new Vector2(x, y), m);
                if (r < EclipseMoon)
                {
                    // a thin bright limb, the diamond ring's edge, in the menu
                    if (menu && r > EclipseMoon - 0.9f * px) return 'C';
                    return 'M';
                }
                var glowAt = m + new Vector2(0.35f * Mathf.Sin(tau * phase), 0.35f * Mathf.Cos(tau * phase));
                float rg = Vector2.Distance(new Vector2(x, y), glowAt);

                // rays: streamers of different strengths, each pulsing on its own phase
                float ang = Mathf.Atan2(y - m.y, Mathf.Abs(x - m.x)) / Mathf.PI + 0.5f;   // 0..1, mirror-safe
                const int Rays = 9;
                float slot = ang * Rays;
                int i = Mathf.FloorToInt(slot);
                float within = 1f - Mathf.Abs(slot - i - 0.5f) * 2f;                     // 1 at the ray's heart
                float strength = 0.35f + 0.65f * EclipseHash(i);
                float pulse = 0.55f + 0.45f * Mathf.Sin(tau * (phase + EclipseHash(i + 40)));
                float ray = strength * pulse * within * within;

                float reach = 0.9f + 1.5f * ray;                                         // cells
                float bright = Mathf.Exp(-(rg - EclipseMoon) / reach);
                if (menu) bright += (EclipseHash(Mathf.FloorToInt(x * scale) * 131 + Mathf.FloorToInt(y * scale)) - 0.5f) * 0.08f;

                return bright > 0.78f ? 'A' : bright > 0.56f ? 'C' : bright > 0.38f ? 'E'
                     : bright > 0.24f ? 'F' : bright > 0.13f ? 'G' : 'I';
            }

            // ---- the crescents: inside either outer lobe, outside both inner ones
            var oR = EclipseMirror(EclipseLobeOuter);
            var iR = EclipseMirror(EclipseLobeInner);
            float dOl = EclipseEllipse(x, y, EclipseLobeOuter, EclipseLobeOuterA, EclipseLobeOuterB, out var nOl);
            float dOr = EclipseEllipse(x, y, oR, EclipseLobeOuterA, EclipseLobeOuterB, out var nOr);
            float dIl = EclipseEllipse(x, y, EclipseLobeInner, EclipseLobeInnerA, EclipseLobeInnerB, out var nIl);
            float dIr = EclipseEllipse(x, y, iR, EclipseLobeInnerA, EclipseLobeInnerB, out var nIr);

            float depth = Mathf.Max(-dOl, -dOr);             // how far inside the silhouette
            float clear = Mathf.Min(dIl, dIr);               // how far outside the hollow
            if (depth < 0f || clear < 0f) return '.';

            // DISCONNECTED at the notch (the user's call): each crescent stops short of the centre
            // line with a glass edge of its own. Where the two lobes cross, the join was a
            // hairline the menu drew and the arena grid skipped - this makes both agree.
            float fromAxis = Mathf.Abs(x - EclipseAxis) - EclipseGapHalf;
            if (fromAxis < 0f) return '.'; 
            if (fromAxis < EclipseEdge && depth > fromAxis && clear > fromAxis)
                return EclipseGlass(new Vector2(x < EclipseAxis ? 1f : -1f, 0f),
                                     1f - fromAxis / EclipseEdge, menu, px, EclipseEdge);

            if (depth < EclipseEdge)
                return EclipseGlass(-dOl > -dOr ? nOl : nOr, depth / EclipseEdge, menu, px, EclipseEdge);
            if (clear < EclipseEdge)
                return EclipseGlass(-(dIl < dIr ? nIl : nIr), 1f - clear / EclipseEdge, menu, px, EclipseEdge);

            // yellow glass: denser at its edges, clearer in the middle - glass reads by its edges
            float inset = Mathf.Min(depth - EclipseEdge, clear - EclipseEdge);
            if (!menu) return inset < 1f ? 'y' : 'x';
            // a highlight running along the lit (upper-left) side of each crescent's outer curve
            var n = -dOl > -dOr ? nOl : nOr;
            if (depth - EclipseEdge < 3f * px && depth - EclipseEdge > px && n.x - n.y < -0.6f) return 'z';
            return inset < 2.5f * px ? 'y' : inset < 1.2f ? 'w' : 'x';
        }

        static string[] BuildEclipse(int scale, float phase = 0f)
        {
            int w = EclipseW * scale, h = EclipseH * scale;
            var rows = new string[h];
            var sb = new System.Text.StringBuilder(w);
            for (int row = 0; row < h; row++)
            {
                sb.Clear();
                for (int col = 0; col < w; col++)
                    sb.Append(EclipseTexel((col + 0.5f) / scale, (row + 0.5f) / scale, scale, phase));
                rows[row] = sb.ToString();
            }
            return scale == 1 ? StripSpecks(rows) : RoseDropIslands(rows);
        }

        static string[] _eclipseRows, _eclipseDetail;
        static string[] EclipseRows => _eclipseRows ??= BuildEclipse(1);
        static string[] EclipseDetail => _eclipseDetail ??= BuildEclipse(EclipseDetailScale);

        /// <summary>
        /// The grip's leather (uppercase K S D B L H). Everything else on spare letters, placed by
        /// hand: the CORONA's greys brightest to darkest A C E F G I and the moon M, since a ramp's
        /// lerps never reach white or black; the crescents' YELLOW GLASS with alpha - y the dense
        /// edge, w and x clearer toward the middle, z the highlight; and the CLEAR GLASS of every
        /// edge, a cool white - g the dense edge line, q the clear body, u the highlight, j the
        /// refraction line.
        /// </summary>
        static Dictionary<char, Color> EclipsePal()
        {
            var pal = Palette.Of(Palette.Leather, Palette.Leather);        // only the UPPERCASE leather is drawn
            pal['A'] = new Color(1.00f, 1.00f, 1.00f);
            pal['C'] = new Color(0.88f, 0.90f, 0.94f);
            pal['E'] = new Color(0.70f, 0.72f, 0.77f);
            pal['F'] = new Color(0.50f, 0.51f, 0.56f);
            pal['G'] = new Color(0.32f, 0.33f, 0.37f);
            pal['I'] = new Color(0.19f, 0.19f, 0.22f);
            pal['M'] = new Color(0.02f, 0.02f, 0.03f);
            // Saturated and fairly dense: over the dark arena floor a paler, clearer yellow
            // came out khaki
            pal['y'] = new Color(1.00f, 0.84f, 0.26f, 0.92f);
            pal['w'] = new Color(1.00f, 0.87f, 0.36f, 0.74f);
            pal['x'] = new Color(1.00f, 0.90f, 0.44f, 0.60f);
            pal['z'] = new Color(1.00f, 0.98f, 0.80f, 0.95f);
            pal['g'] = new Color(0.86f, 0.93f, 1.00f, 0.90f);
            pal['q'] = new Color(0.80f, 0.90f, 1.00f, 0.28f);
            pal['u'] = new Color(1.00f, 1.00f, 1.00f, 0.98f);
            pal['j'] = new Color(0.36f, 0.44f, 0.56f, 0.80f);
            return pal;
        }

        /// <summary>The corona shimmering, a loop. Arena only (GearItem.IdleFrames).</summary>
        static Sprite[] EclipseFrames()
        {
            var pal = EclipsePal();
            var frames = new Sprite[EclipseFrameCount];
            for (int i = 0; i < EclipseFrameCount; i++)
                frames[i] = StageSprite($"gear.weapon.disc.eclipse.shimmer{i}",
                                        BuildEclipse(1, i / (float)EclipseFrameCount), pal, EclipseGrip);
            return frames;
        }

        static GearItem EclipseBlades()
        {
            var item = Disc(WithMenu(Make("eclipse_blades", "Eclipse Blades", GearSlot.Weapon, LootTier.Diamond, 0f,
                    Pixels(RigLayer.Weapon, "gear.weapon.disc.eclipse", EclipseRows, EclipsePal(),
                           DiscHandX, DiscHandY, pivotTexel: EclipseGrip, ppu: FinePpu, upscale2x: true)),
                    Pixels(RigLayer.Weapon, "gear.weapon.disc.eclipse.menu", EclipseDetail, EclipsePal(),
                           DiscHandX, DiscHandY, pivotTexel: EclipseDetailGrip, ppu: MenuPpu)));
            item.IdleFrames = EclipseFrames();
            item.IdleFrameSeconds = EclipseFrameSeconds;
            return item;
        }
    }
}
