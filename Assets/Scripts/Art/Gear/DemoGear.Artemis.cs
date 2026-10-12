using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== the Artemis Hunting Bow: a blade for an arch
    //
    // Diamond, power 0 - bespoke art and nothing else (the cosmetic weapons' rule). After the
    // user's reference (2026-10-10, "Artemis Hunting Bow"): the bow's whole back is ONE CURVED
    // BLADE. A gunmetal frame lies along the blade's belly, wound twice, with a hook curling off it
    // toward each tip; two crescent arms reach in from the frame to hold the grip, so the middle of
    // the bow is open round the hand. Grip and string are BLACK, as in the reference (the user's
    // call - a pale string was tried); the string is nocked where the frame's ends turn in.
    //
    // THE BLADE IS THE SILHOUETTE. The reference also had a moon set in the blade at the grip;
    // it was drawn and dropped by the user's call - at arena size it read as an eye or a hole,
    // and the crescent says the bow without it.
    //
    // WHAT MAKES IT READ AS A BLADE, all four by the user's call (the first pass, one pale band
    // honed along its back, read as bone or porcelain - its tones all sat between 0.80 and 0.97):
    //   - TWO PLANES: a bright bevel along the edge, a crisp step (and on the menu art a line of
    //     light on the ridge) to a mid-steel flat. The frame went to gunmetal with it - at the
    //     original silver it sat at the flat's value and the two blurred into one band.
    //   - POINTS PAST THE NOCKS: the blade keeps its width most of the way and then runs to a
    //     point (ArtemisBelly), carrying on past where the string ties to the frame and over it.
    //     As two arcs through the tips it ran out into the string as a sliver, with no point.
    //   - A MIRROR STREAK: a dark reflection drifting along the flat, and one glint across the
    //     upper limb.
    //   - AN EDGE THAT STARTS: beside the hand the blade is unsharpened, a squared spine; a notch
    //     at each arm's joint (ArtemisNotches) is where the honed edge begins.
    //
    // One field over authored cells (x across the canvas, string at the left, the blade's edge to
    // the right; y down from the upper tip) sampled for the arena and (x4) the menu, so the two
    // can never disagree. As tall as the Aether Longbow (BowHeightRows); the literals below are
    // authored on its 77 rows.
    public static partial class DemoGear
    {
        const int ArtemisCanvas = 24;
        static int ArtemisHeightRows => BowHeightRows;

        const float ArtemisTipX = 2.5f, ArtemisTipInset = 0.4f;
        /// <summary>The blade's edge at the middle of the bow (the crescent's apex), and how wide
        /// the blade stands there.</summary>
        const float ArtemisBackApex = 22f, ArtemisBladeWidth = 4.8f;
        const float ArtemisFrameWidth = 2.2f;
        const float ArtemisStringX = 4.5f;
        const float ArtemisGripX = 8.4f, ArtemisGripTop = 35.5f, ArtemisGripBottom = 44.5f;
        /// <summary>The fraction of the blade's width, from the edge, that is the bevel.</summary>
        const float ArtemisBevel = 0.42f;
        /// <summary>Where the glint crosses the upper limb.</summary>
        const float ArtemisGlintY = 23f;
        static readonly float[] ArtemisWraps = { 12.6f, 65f };
        static readonly float[] ArtemisNocks = { 9f, 68f };
        /// <summary>Where the honed edge begins - level with the arms' joints to the frame.</summary>
        static readonly float[] ArtemisNotches = { 27f, 53f };

        static float ArtemisTipTop => ArtemisTipInset;
        static float ArtemisTipBottom => ArtemisHeightRows - ArtemisTipInset;
        static float ArtemisMidY => (ArtemisTipTop + ArtemisTipBottom) * 0.5f;
        static float ArtemisHalf => (ArtemisTipBottom - ArtemisTipTop) * 0.5f;

        /// <summary>How far toward the tips the frame runs: to the nocks, and no further - past
        /// them the blade is alone.</summary>
        static float ArtemisFrameReach => (ArtemisMidY - ArtemisNocks[0]) / ArtemisHalf + 0.02f;

        /// <summary>The middle of the wrapped grip - the bow's pivot, a little below its middle.</summary>
        static Vector2Int ArtemisGrip => new(8, 40);
        static Vector2Int ArtemisDetailGrip => new(ArtemisGrip.x * EmberDetailScale, ArtemisGrip.y * EmberDetailScale);

        static string[] _artemisRows, _artemisDetail;
        static string[] ArtemisRows => _artemisRows ??= ArtemisSample(1);
        static string[] ArtemisDetail => _artemisDetail ??= ArtemisSample(EmberDetailScale);

        // Lazy: static arrays in the partial files must not depend on another file's statics
        // being initialised first (Tria Prima's note).
        static Vector2[][] _artemisArms, _artemisHooks;

        /// <summary>The two arms holding the grip, each from one end of it out to the frame -
        /// crescents, thick in the middle and fine at both ends (see ArtemisTexel).</summary>
        static Vector2[][] ArtemisArms => _artemisArms ??= new[]
        {
            ArtemisArm(ArtemisGripTop + 0.4f, ArtemisNotches[0], -1f),
            ArtemisArm(ArtemisGripBottom - 0.4f, ArtemisNotches[1], 1f),
        };

        static Vector2[] ArtemisArm(float gripY, float frameY, float sign)
            => ArtemisCurve(new Vector2(ArtemisGripX + 0.2f, gripY),
                            new Vector2(ArtemisGripX + 1f, frameY - sign),
                            new Vector2(ArtemisFrameInside(frameY) + 0.6f, frameY), 16);

        /// <summary>A hook off the frame on each limb, curling in and on toward the tip.</summary>
        static Vector2[][] ArtemisHooks => _artemisHooks ??= new[]
        {
            ArtemisHook(19.5f, -5f),
            ArtemisHook(58f, 5f),
        };

        static Vector2[] ArtemisHook(float y, float toward)
        {
            float x = ArtemisFrameInside(y) + 0.5f;
            return ArtemisCurve(new Vector2(x, y), new Vector2(x - 3.2f, y + toward * 0.2f),
                                new Vector2(x - 2.8f, y + toward), 10);
        }

        static void AddArtemis(List<GearItem> items)
        {
            // Cold steel with a real range of value - the bevel near white, the flat mid-steel -
            // placed by hand, because a ramp lifted toward white puts both planes in the top fifth.
            var blade = Palette.Ramp.FromTones(
                glow: new Color(0.97f, 0.98f, 1.00f), light: new Color(0.84f, 0.87f, 0.93f),
                b: new Color(0.70f, 0.74f, 0.82f), dark: new Color(0.58f, 0.62f, 0.71f),
                deep: new Color(0.40f, 0.42f, 0.50f), line: new Color(0.20f, 0.21f, 0.27f));
            var frame = new Palette.Ramp(new Color(0.44f, 0.46f, 0.53f), lift: 0.30f);
            var wrap = new Palette.Ramp(new Color(0.11f, 0.11f, 0.13f), lift: 0.20f, shade: 0.35f);
            var pal = Palette.Of(blade, frame, wrap);

            items.Add(WithMenu(Bow(Make("artemis_bow", "Artemis Hunting Bow", GearSlot.Weapon, LootTier.Diamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.artemis", ArtemisRows, pal, 0f, 0f,
                       pivotTexel: ArtemisGrip, ppu: FinePpu, upscale2x: true))),
                Pixels(RigLayer.Weapon, "gear.weapon.artemis.menu", ArtemisDetail, pal, 0f, 0f,
                       pivotTexel: ArtemisDetailGrip, ppu: MenuPpu)));
        }

        /// <summary>The whole bow sampled at <paramref name="k"/> texels per authored cell.</summary>
        static string[] ArtemisSample(int k)
        {
            var rows = new string[ArtemisHeightRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[ArtemisCanvas * k];
                float y = (Y + 0.5f) / k;
                for (int X = 0; X < line.Length; X++)
                    line[X] = ArtemisTexel((X + 0.5f) / k, y, k > 1);
                rows[Y] = new string(line);
            }
            return rows;
        }

        static char ArtemisTexel(float x, float y, bool fine)
        {
            float edge = fine ? 0.45f : 0.81f;   // about one drawn texel's worth of cell
            float t = Mathf.Abs(y - ArtemisMidY) / ArtemisHalf;

            // ---- the grip: black, wrapped end to end (the user's call - no silver caps) ----
            float dx = x - ArtemisGripX;
            if (y >= ArtemisGripTop && y <= ArtemisGripBottom && Mathf.Abs(dx) < 1.05f)
            {
                float wrapPhase = (y + dx * 0.4f) / (fine ? 0.9f : 1.3f);
                if (wrapPhase - Mathf.Floor(wrapPhase) < (fine ? 0.25f : 0.34f)) return '2';
                return dx < -0.5f ? '5' : dx > 0.5f ? '3' : '4';
            }

            bool inLength = y > ArtemisTipTop && y < ArtemisTipBottom;

            // ---- the notches, bitten out of the edge where it begins ----
            if (inLength)
                foreach (float ny in ArtemisNotches)
                    if (Mathf.Sqrt((x - ArtemisBack(ny) - 0.35f) * (x - ArtemisBack(ny) - 0.35f)
                                   + (y - ny) * (y - ny)) < 1.55f)
                        return '.';

            // ---- the blade ----
            float back = ArtemisBack(y), belly = ArtemisBelly(y);
            if (inLength && x >= belly && x <= back)
            {
                float fromEdge = back - x, width = back - belly;
                float across = fromEdge / Mathf.Max(width, 1e-4f);

                // Beside the hand: unsharpened, a squared spine and the flat.
                if (y > ArtemisNotches[0] && y < ArtemisNotches[1])
                {
                    if (fromEdge < edge) return 'l';
                    return x - belly < edge ? 's' : 'b';
                }
                // One glint across the upper limb.
                if (y < ArtemisMidY && Mathf.Abs(y - ArtemisGlintY + fromEdge * 0.55f) < (fine ? 0.7f : 0.9f))
                    return 'h';
                if (fromEdge < edge) return 'h';                                  // the honed edge
                if (across < ArtemisBevel || width < 1.6f) return 'l';            // the bevel
                if (fine && across < ArtemisBevel + 0.3f / Mathf.Max(width, 1f))  // light on the ridge
                    return 'h';
                if (x - belly < edge && width > 2.2f) return 's';                 // against the frame
                if (t < 0.8f && width > 2.4f)                                     // the mirror streak
                {
                    float streak = 0.66f + 0.06f * Mathf.Sin(y * 0.21f);
                    if (Mathf.Abs(x - (back - streak * width)) < (fine ? 0.32f : 0.5f)) return 'd';
                }
                return 'b';                                                       // the flat
            }

            // ---- the frame along the belly, nock to nock, wound twice ----
            float frameWidth = ArtemisFrameWidthAt(y), inside = belly - frameWidth;
            bool onFrame = t < ArtemisFrameReach;
            if (onFrame && x >= inside && x < belly)
            {
                foreach (float wy in ArtemisWraps)
                    if (Mathf.Abs(y - wy) < 1.4f) return y > wy ? 'D' : 'L';
                float d = belly - x;
                if (d < edge) return 'B';
                if (frameWidth - d < edge) return 'L';
                if (fine && Mathf.Abs(d - frameWidth * 0.5f) < 0.25f) return 'S';     // its groove
                return 'B';
            }
            // the windings stand proud of the frame on the string side
            if (onFrame && x >= inside - 1f && x < inside)
                foreach (float wy in ArtemisWraps)
                    if (Mathf.Abs(y - wy) < 1.4f - (inside - x)) return y > wy ? 'D' : 'L';

            // ---- the arms holding the grip, and the hooks ----
            foreach (var arm in ArtemisArms)
            {
                float d = CorAlong(x, y, arm, out float along);
                float w = 0.55f + 0.75f * Mathf.Sin(Mathf.PI * along);
                if (d < w) return fine && d < w * 0.35f ? 'L' : 'B';
            }
            foreach (var hook in ArtemisHooks)
            {
                float d = CorAlong(x, y, hook, out float along);
                float w = 1.05f - 0.8f * along;
                if (d < w) return fine && d < w * 0.35f ? 'L' : 'B';
            }

            // ---- the nocks: the frame's ends turned in to the string ----
            foreach (float ny in ArtemisNocks)
            {
                var nock = new[]
                {
                    new Vector2(ArtemisBelly(ny) - 0.8f, ny),
                    new Vector2(ArtemisStringX, ny + (ny < ArtemisMidY ? -0.9f : 0.9f)),
                };
                if (CorAlong(x, y, nock, out _) < 0.75f) return 'B';
            }

            // ---- the string: black, nock to nock ----
            if (y > ArtemisNocks[0] - 1f && y < ArtemisNocks[1] + 1f
                && Mathf.Abs(x - ArtemisStringX) < (fine ? 0.25f : 0.5f))
                return 'K';
            return '.';
        }

        /// <summary>
        /// The blade's edge: a circular arc through both tips, bulging to
        /// <see cref="ArtemisBackApex"/> at the middle - x at height <paramref name="y"/>, or far
        /// off the canvas past the tips.
        /// </summary>
        static float ArtemisBack(float y)
        {
            float sag = ArtemisBackApex - ArtemisTipX, half = ArtemisHalf;
            float r = (half * half + sag * sag) / (2f * sag);
            float dy = y - ArtemisMidY, v = r * r - dy * dy;
            return v > 0f ? ArtemisBackApex - r + Mathf.Sqrt(v) : -1e9f;
        }

        /// <summary>The blade's belly: the edge less a width that holds most of the way out and
        /// then runs to the point (1 - t^4), so the tips are points, not slivers.</summary>
        static float ArtemisBelly(float y)
        {
            float t = Mathf.Abs(y - ArtemisMidY) / ArtemisHalf;
            if (t >= 1f) return -1e9f;
            float t2 = t * t;
            return ArtemisBack(y) - ArtemisBladeWidth * (1f - t2 * t2);
        }

        /// <summary>The frame thins toward the tips.</summary>
        static float ArtemisFrameWidthAt(float y)
        {
            float t = (y - ArtemisMidY) / ArtemisHalf;
            return ArtemisFrameWidth * (1f - 0.45f * t * t);
        }

        /// <summary>The frame's string-side edge at height <paramref name="y"/>.</summary>
        static float ArtemisFrameInside(float y) => ArtemisBelly(y) - ArtemisFrameWidthAt(y);

        /// <summary>A quadratic curve as a polyline of <paramref name="n"/> segments, for CorAlong.</summary>
        static Vector2[] ArtemisCurve(Vector2 a, Vector2 b, Vector2 c, int n)
        {
            var pts = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, v = 1f - u;
                pts[i] = v * v * a + 2f * v * u * b + u * u * c;
            }
            return pts;
        }
    }
}
