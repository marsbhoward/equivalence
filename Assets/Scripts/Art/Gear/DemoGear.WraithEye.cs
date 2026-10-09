using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    public static partial class DemoGear
    {
        // ------------------------------------------------------------------ the Wraith's Eye
        //
        // WORN it has no art at all - the item IS the character's own left eye, recoloured to a
        // fixed red and pulsing (HasGlowingEye, BodyLook.Head, GlowingEye). A helm would cover the
        // very eye it exists to show. But a card, the armoury wall and its NFT image need a
        // picture, so it gets a DISPLAY picture only (GearItem.DisplayLayer - the disc pair's
        // mechanism): never on the body, so the worn look is untouched.
        //
        // The user's pick (2026-10-09): a DISEMBODIED EYE - almond eye, dark sclera, the item's
        // own red iris round a slit pupil, heavy lids, wreathed in wisps of smoke trailing off
        // both corners. NO auto-outline: smoke has no hard edge (outlined, the wisps read as
        // tentacles), and the heavy lids already edge the eye. The smoke and the glow round it
        // are TRANSLUCENT. One function, sampled at both densities (arena FinePpu for cards, menu
        // for the NFT), so the two cannot drift - the crescent's method. The smoke is hashed
        // noise, so the picture is the same every time.

        const int EyeCells = 48, EyeRowsCells = 40;   // 0.32 x 0.27 units at FinePpu

        /// <summary>The eye sampled at <paramref name="k"/> texels per cell, top row first.</summary>
        static string[] WraithEyeSample(int k)
        {
            var rows = new string[EyeRowsCells * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[EyeCells * k];
                // Grid rows run top-down; the shape is written y-up.
                float y = EyeRowsCells - (Y + 0.5f) / k;
                for (int X = 0; X < line.Length; X++)
                    line[X] = WraithEyeTexel((X + 0.5f) / k, y, k);
                rows[Y] = new string(line);
            }
            return rows;
        }

        // Geometry, in cells, y up.
        const float EyeCx = 24f, EyeCy = 14f, EyeHalfW = 13f, EyeHalfH = 7.6f;
        const float IrisR = 6.0f;

        /// <summary>The almond's half-height at x - pointed at both corners.</summary>
        static float EyeHalfHeightAt(float x)
        {
            float u = (x - EyeCx) / EyeHalfW;
            if (Mathf.Abs(u) >= 1f) return -1f;
            return EyeHalfH * Mathf.Pow(1f - u * u, 0.85f);
        }

        static char WraithEyeTexel(float x, float y, int k)
        {
            float h = EyeHalfHeightAt(x);
            float dy = y - EyeCy;

            // -------- inside the almond: the eye itself
            if (h > 0f && Mathf.Abs(dy) < h)
            {
                // Iris, a touch high so the heavy upper lid hoods it.
                float ix = x - EyeCx, iy = y - (EyeCy + 0.4f);
                float r = Mathf.Sqrt(ix * ix + iy * iy);
                if (r < IrisR)
                {
                    // The slit: narrow, full height of the iris, pinched at its ends.
                    float slitHalf = 0.75f * Mathf.Sqrt(Mathf.Max(0f, 1f - (iy / IrisR) * (iy / IrisR)));
                    if (Mathf.Abs(ix) < slitHalf) return 'p';
                    // The glint - upper left, where the light is.
                    if (ix > -3.4f && ix < -2.0f && iy > 1.9f && iy < 3.3f) return 'w';
                    // Bright beside the pupil, deep at the rim; lit from above.
                    if (r > IrisR - 0.75f) return 'R';
                    float tone = r / IrisR - iy * 0.06f;
                    return tone < 0.42f ? 'H' : tone < 0.72f ? 'G' : 'F';
                }

                // The lids' inner shadow, then the sclera shaded toward the corners.
                float edge = h - Mathf.Abs(dy);
                if (dy > 0f && edge < 1.9f) return 'L';
                if (edge < 0.7f) return 'L';
                // Lightest round the iris, bruising toward the corners and under the lid.
                // Shaded along the almond's own shape: lightest in the middle of the lower white,
                // bruising toward the corners and under the hooding lid.
                float ux = (x - EyeCx) / EyeHalfW, uy = dy / h;
                float fade = Mathf.Sqrt(ux * ux * 1.1f + uy * uy * 0.6f) + (dy > 0f ? 0.3f : 0f);
                return fade < 0.62f ? 'S' : fade < 0.9f ? 's' : 'L';
            }

            // -------- the lids: heavy above, thin below
            float hLid = EyeHalfHeightAt(x);
            if (hLid > 0f)
            {
                if (dy > 0f && dy < hLid + 2.4f) return 'K';
                if (dy < 0f && -dy < hLid + 0.8f) return 'K';
            }

            // -------- the smoke
            return Smoke(x, y, k);
        }

        /// <summary>Thin wisps of smoke rising off the eye and curling, fraying into lighter tips,
        /// over a dithered red glow that thins away from the lids.</summary>
        static char Smoke(float x, float y, int k)
        {
            char best = '.';
            // start x/y, run dx/dy, wave amplitude, waves along it, starting width, seed. Two broad
            // trails off the corners, sweeping up and out - vapour, not fingers.
            Tendril(x, y, k, 35.0f, 16.0f, 10.5f, 17.0f, 4.0f, 0.75f, 3.4f, 11, ref best);   // right corner
            Tendril(x, y, k, 13.0f, 16.0f, -9.5f, 14.0f, 3.2f, 0.7f, 2.8f, 23, ref best);    // left corner
            Tendril(x, y, k, 27.0f, 23.0f, 4.0f, 14.0f, 2.4f, 0.9f, 2.0f, 37, ref best);     // off the brow
            Tendril(x, y, k, 37.0f, 12.0f, 9.0f, -3.5f, 1.2f, 0.7f, 1.6f, 67, ref best);     // a low trail
            if (best != '.') return best;

            // The glow: dark red texels around the lids, sparser the further out.
            float h = EyeHalfHeightAt(Mathf.Clamp(x, EyeCx - EyeHalfW + 0.01f, EyeCx + EyeHalfW - 0.01f));
            float ex = (x - EyeCx) / (EyeHalfW + 4.5f), ey = (y - EyeCy) / (h + 5.5f);
            float r = Mathf.Sqrt(ex * ex + ey * ey);
            if (r < 1f)
            {
                float n = Hash01(Mathf.FloorToInt(x * k), Mathf.FloorToInt(y * k), 91);
                float keep = 1f - r;
                if (n < keep * 1.5f) return keep > 0.4f ? 'g' : 'q';
            }
            return '.';
        }

        static void Tendril(float x, float y, int k, float x0, float y0, float dx, float dy,
                            float wave, float freq, float width, int seed, ref char best)
        {
            float len2 = dx * dx + dy * dy;
            // Nearest parameter along the straight run, then bend it with a wave that grows
            // toward the tip - smoke curls more the further it gets from its source.
            float t = Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / len2);
            float bend = wave * t * Mathf.Sin(t * Mathf.PI * 2f * freq + seed);
            float len = Mathf.Sqrt(len2);
            float nx = -dy / len, ny = dx / len;
            float cx = x0 + dx * t + nx * bend, cy = y0 + dy * t + ny * bend;
            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

            // Swells a little, then thins to nothing; frays at the edges (hashed on the texel, so
            // it is stable) and breaks up toward the tip.
            float w = width * (0.7f + 0.6f * Mathf.Sin(Mathf.Min(t, 0.9f) * Mathf.PI)) * (1f - t * 0.6f);
            int ix = Mathf.FloorToInt(x * k), iy = Mathf.FloorToInt(y * k);
            float n = Hash01(ix, iy, seed);
            if (d > w + (n - 0.5f) * 1.1f) return;
            if (t > 0.45f && n < (t - 0.45f) * 1.7f) return;

            // Denser in the core, thinner (more see-through) at the edges and the tip; the upper,
            // lit side paler.
            float core = 1f - d / Mathf.Max(w, 0.01f);
            float side = (x - cx) * nx + (y - cy) * ny;
            char c = core > 0.45f && t < 0.6f ? 'c' : core > 0.2f && t < 0.85f ? 'd' : 'm';
            if (side > w * 0.35f && c == 'c') c = 'd';
            if (Rank(c) > Rank(best)) best = c;
        }

        static int Rank(char c) => c switch { 'c' => 3, 'd' => 2, 'm' => 1, _ => 0 };

        static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        static Dictionary<char, Color> WraithEyePalette()
        {
            static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
            return new Dictionary<char, Color>
            {
                // The iris: the worn eye's own red (BodyLook's glow ramp, 0.85/0.10/0.08) and its ramp.
                ['R'] = Hex("#4a0a0c"), ['F'] = Hex("#8c1414"), ['G'] = Hex("#d91a14"), ['H'] = Hex("#ff6a3c"),
                ['p'] = Hex("#0b0608"), ['w'] = Hex("#ffd9c8"),
                // Sclera: not white - a wraith's eye is jaundiced, bruised at the corners.
                ['S'] = Hex("#b8ad9c"), ['s'] = Hex("#7c6e70"), ['L'] = Hex("#3e3038"),
                // Lids and the smoke round it: violet-black, lit violet-grey.
                ['K'] = Hex("#1a1220"), ['c'] = WithAlpha(Hex("#2a2134"), 0.92f), ['d'] = WithAlpha(Hex("#4a3c58"), 0.72f),
                ['m'] = WithAlpha(Hex("#7a6a88"), 0.45f),
                // The glow round it, in the iris's own reds, dithered.
                ['g'] = WithAlpha(Hex("#c8161c"), 0.62f), ['q'] = WithAlpha(Hex("#8a1014"), 0.4f),
            };
        }

        /// <summary>The Wraith's Eye's display picture (cards, the wall, its NFT image).</summary>
        static void WraithEyeDisplay(GearItem item)
        {
            var pal = WraithEyePalette();
            var pivot = new Vector2Int(EyeCells / 2, EyeRowsCells / 2);
            item.DisplayLayer = Pixels(RigLayer.HeadArmor, "gear.head.wraith_eye.display", WraithEyeSample(1), pal,
                                       0f, 0f, pivotTexel: pivot, ppu: FinePpu, outline: false);
            item.DisplayMenuLayer = Pixels(RigLayer.HeadArmor, "gear.head.wraith_eye.display.menu", WraithEyeSample(2), pal,
                                           0f, 0f, pivotTexel: pivot * 2, ppu: MenuPpu, outline: false);
        }
    }
}
