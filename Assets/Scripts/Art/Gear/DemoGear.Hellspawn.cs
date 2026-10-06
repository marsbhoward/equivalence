using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Hellspawn Cape: Spawn's red cape
    //
    // The user's brief: a back piece after Spawn's cape (Todd McFarlane's, Image Comics). What makes
    // that cape is the opposite of every cloak here, which are hip-length and the torso's width and
    // so barely show from the front:
    //
    //     FLOOR LENGTH, FLARED    wider than the arms at the shoulders and wider still at the hem,
    //                             so from the front it frames the whole figure
    //     TATTERED               the hem is torn into V-cut points, some dragging past the feet,
    //                             the lower side edges are ragged, and the cloth is torn through
    //     A HIGH SPIKED COLLAR    jagged points standing up either side of the head, leaning out -
    //                             from the front it is the one part of the cape seen ABOVE the
    //                             shoulders, and it is what says "Spawn" in a silhouette
    //
    // Blood red, hand-picked rather than a Ramp: a ramp lifts toward white, and a lit red that goes
    // pink stops being blood. No glow tone - cloth stays off the glow end (CapeRows' note).
    //
    // Hinged at the NECK, not the sprite's top (GearItem.CapeHingeCells), since the collar stands
    // up past it; and swung at a fraction of a short cape's angle (CapeSwingScale), or a
    // floor-length hem sweeps half a body sideways.
    //
    // ONE field (HellspawnTexel) in the TORSO's own frame - cells, y up from the waist the Back
    // layer hangs from - sampled at BodyPpu, so every number below is a height on the body:
    // shoulders at 21, the neck 22, the crown 34, the soles -24.
    public static partial class DemoGear
    {
        /// <summary>The grid, in cells: x -25..25 (even texel width, mirrored about the centre
        /// line), y 36 (past the crown, for the collar's points) down to -26 (past the soles).
        /// About twice the body's width at the hem - Spawn's cape hangs open that wide. At 19.5 it
        /// showed from the front as a strip down each side, not a cape framing the figure.</summary>
        const float HellHalfW = 25f, HellTop = 36f, HellBottom = -26f;

        /// <summary>Where the collar stands behind the neck, and where the spikes rise from.</summary>
        const float HellCollar = 23.5f;
        /// <summary>The collar runs out to here, then drops over the shoulder.</summary>
        const float HellCollarHalf = 12.5f;
        /// <summary>The solid cloth ends here; below it only the tatters hang.</summary>
        const float HellHemLine = -15.5f;

        /// <summary>The collar spikes, per side: base centre, tip height, base half-width and how
        /// far the tip sweeps OUT. Two big ones a side - three even ones a side read as a crown's
        /// battlement. The inner one stands behind the head and shows past it; the outer one is
        /// the front view's.</summary>
        static readonly (float X, float Tip, float Half, float Lean)[] HellSpikes =
        {
            (7.0f, 35.5f, 3.4f, 3.0f),
            (11.4f, 32.5f, 3.0f, 6.0f),
        };

        /// <summary>Rips through the cloth: centre, radii - tall, because cloth hanging under its
        /// own weight tears DOWN. Big enough that the outline's bite on either side leaves a hole:
        /// at 1.5 x 2.2 cells it closed to a dark rune.</summary>
        static readonly (float X, float Y, float Rx, float Ry)[] HellTears =
        {
            (-16.0f, -7.0f, 2.1f, 4.0f),
            (15.5f, -12.0f, 2.0f, 3.6f),
            (4.2f, 3.5f, 1.8f, 3.2f),
            (-5.5f, -12.5f, 1.8f, 3.2f),
        };

        /// <summary>The hem's tatters - x, and how far each point hangs below the hem line.
        /// Seeded, so the cape tears the same way every time it is built.</summary>
        static List<(float X, float Drop)> _hellTatters;
        static List<(float X, float Drop)> HellTatters
        {
            get
            {
                if (_hellTatters != null) return _hellTatters;
                var rng = new System.Random(666);
                float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
                _hellTatters = new List<(float, float)>();
                for (float x = -HellHalfW + 0.8f; x < HellHalfW - 0.8f; x += R(2.2f, 3.6f))
                    _hellTatters.Add((x, rng.NextDouble() < 0.4 ? R(5.5f, 9.2f) : R(2.5f, 5f)));
                return _hellTatters;
            }
        }

        /// <summary>The cape's half-width at height y: over the shoulder, then flaring out to
        /// about twice the body's width at the hem.</summary>
        static float HellHalf(float y)
        {
            if (y >= 17f) return Mathf.Lerp(13.2f, 15f, Mathf.InverseLerp(21.5f, 17f, y));
            return 15f + (17f - y) * 0.23f;
        }

        /// <summary>The top edge above a column |x|: the collar and its spikes, then the drop over
        /// the shoulder.</summary>
        static float HellTopAt(float ax)
        {
            if (ax > HellCollarHalf) return 22.5f - (ax - HellCollarHalf) * 2.6f;
            float top = HellCollar - ax * 0.08f;
            float spikeFrom = HellCollar - 1f;
            foreach (var s in HellSpikes)
            {
                // the spike's centre leans out as it rises, and it narrows to a point
                for (float y = spikeFrom; y <= s.Tip; y += 0.25f)
                {
                    float t = (y - spikeFrom) / (s.Tip - spikeFrom);
                    // swept, not straight: the lean grows with the square of the height
                    if (Mathf.Abs(ax - (s.X + s.Lean * t * t)) < s.Half * (1f - t)) top = Mathf.Max(top, y);
                }
            }
            return top;
        }

        /// <summary>The lowest cloth below x: the hem line, or a tatter's V hanging past it.</summary>
        static float HellBottomAt(float x)
        {
            float bottom = HellHemLine;
            foreach (var t in HellTatters)
                bottom = Mathf.Min(bottom, HellHemLine - t.Drop + Mathf.Abs(x - t.X) * 2.4f);
            return Mathf.Max(bottom, HellBottom + 0.5f);
        }

        /// <summary>A ragged side edge below the waist: notches in and out of the flare.</summary>
        static float HellSideRag(float y, bool left)
        {
            if (y > 2f) return 0f;
            float p = Mathf.Repeat(y / 3.1f + (left ? 0.37f : 0.81f), 1f);
            float tri = Mathf.Abs(p - 0.5f) * 2f;                           // 0..1..0
            return (tri - 0.5f) * 2.4f * Mathf.InverseLerp(2f, -6f, y);
        }

        static bool HellTorn(float x, float y)
        {
            foreach (var t in HellTears)
            {
                float dx = (x - t.X) / t.Rx, dy = (y - t.Y) / t.Ry;
                float a = Mathf.Atan2(dy, dx);
                float wobble = 1f + 0.18f * Mathf.Sin(a * 3f + t.X) + 0.12f * Mathf.Sin(a * 7f + t.Y);
                if (dx * dx + dy * dy < wobble * wobble) return true;
            }
            return false;
        }

        /// <summary>
        /// The cape at (x, y) in torso cells. Deep s, dark d, base b, lit l; '.' where there is no
        /// cloth. <paramref name="folds"/> adds broad fan folds - see HellspawnFolds.
        /// </summary>
        static char HellspawnTexel(float x, float y, bool folds)
        {
            float ax = Mathf.Abs(x);
            float top = HellTopAt(ax);
            float half = HellHalf(y) + HellSideRag(y, x < 0f);
            float bottom = HellBottomAt(x);
            if (y > top || y < bottom || ax > half) return '.';
            if (HellTorn(x, y)) return '.';

            const string tones = "sdbl";
            int tone;

            // the collar standing up behind the head: its inner face, lit along the rim
            if (y > HellCollar - 1f && ax <= HellCollarHalf)
                tone = top - y < 0.8f ? 2 : 1;
            // over the shoulders: the light catches the top of the cloth
            else if (top - y < 1.4f) tone = 3;
            else
            {
                // Base cloth, going dark lower down - along a line that dips under each ridge and
                // rises over each trough, so the shade follows the folds. A straight line across
                // the cape read as two cloths sewn together.
                float fold = 0f;
                if (folds && y < 19f)
                {
                    float u = x / Mathf.Max(half, 1f);
                    fold = Mathf.Sin(u * Mathf.PI * 3.5f + 0.6f) * Mathf.Clamp01((19f - y) / 22f);
                }
                tone = y > 6f - fold * 12f ? 2 : 1;
                if (fold > 0.62f) tone++;            // a ridge catching the light
                else if (fold < -0.8f) tone--;       // the deepest of a trough
                // lit down the left edge, shaded down the right
                if (half - ax < 1.1f) tone += x < 0f ? 1 : -1;
            }

            // the hem and the tatters in deep shade
            if (y - bottom < 1.4f) tone = 0;
            return tones[Mathf.Clamp(tone, 0, 3)];
        }

        /// <summary>Broad fan folds down the cloth - a spread of light and shade that widens with
        /// the flare. Not crease marks (CapeRows' rule against those stands): whole planes of
        /// cloth, several cells wide.</summary>
        const bool HellspawnFolds = true;

        static string[] BuildHellspawn(bool folds)
        {
            int w = Mathf.RoundToInt(HellHalfW * 2f * 2f), h = Mathf.RoundToInt((HellTop - HellBottom) * 2f);
            var rows = new string[h];
            var sb = new System.Text.StringBuilder(w);
            for (int row = 0; row < h; row++)
            {
                sb.Clear();
                for (int col = 0; col < w; col++)
                    sb.Append(HellspawnTexel((col + 0.5f) / 2f - HellHalfW, HellTop - (row + 0.5f) / 2f, folds));
                rows[row] = sb.ToString();
            }
            return StripSpecks(rows);
        }

        /// <summary>Blood red, by hand: a lit red that stays red, not a ramp lifted toward pink.</summary>
        static Dictionary<char, Color> HellspawnPal() => new()
        {
            ['l'] = new Color(0.90f, 0.20f, 0.16f),
            ['b'] = new Color(0.70f, 0.07f, 0.09f),
            ['d'] = new Color(0.46f, 0.03f, 0.06f),
            ['s'] = new Color(0.26f, 0.01f, 0.04f),
        };

        static GearItem HellspawnCape()
        {
            var rows = BuildHellspawn(HellspawnFolds);
            var item = Make("hellspawn_cape", "Hellspawn Cape", GearSlot.Back, LootTier.Diamond, 0f,
                Pixels(RigLayer.Back, "gear.back.hellspawn", rows, HellspawnPal(),
                       0f, (HellTop + HellBottom) / 2f, ppu: BodyPpu));
            // hinge at the neck (22), not the collar's spikes - measured from the DRAWN top,
            // which is the grid's top plus the outline's border
            item.CapeHingeCells = HellTop + PrimitiveCharacterRig.Proportions.Cells(PixelSprite.OutlineCanvasPadFor(BodyPpu))
                                  - PrimitiveCharacterRig.Proportions.NeckYCells;
            item.CapeSwingScale = 0.45f;
            return item;
        }
    }
}
