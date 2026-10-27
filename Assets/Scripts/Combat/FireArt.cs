using UnityEngine;
using T = Convergence.Core.Tuning.FireFx;

namespace Convergence.Combat
{
    /// <summary>
    /// What every one of Fire's pictures shares: the ramp, a hash and a value noise. Never
    /// <c>UnityEngine.Random</c> - seeded floor rolls share its state - so every flicker is a
    /// function of where it is and the time.
    /// </summary>
    public static class FireArt
    {
        /// <summary>The ramp at <paramref name="u"/>: 0 the hottest (white), 1 the coolest (ember).</summary>
        public static Color32 At(float u)
        {
            var ramp = T.Ramp;
            int i = Mathf.Clamp(Mathf.FloorToInt(u * ramp.Length), 0, ramp.Length - 1);
            return ramp[i];
        }

        public static float Hash(int a, int b = 0, int c = 0)
        {
            unchecked
            {
                int h = a * 374761393 + b * 668265263 + c * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7fffffff) / (float)0x7fffffff;
            }
        }

        /// <summary>Smooth value noise in 0..1.</summary>
        public static float Noise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            xf = xf * xf * (3f - 2f * xf); yf = yf * yf * (3f - 2f * yf);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return a + (b - a) * xf + (c - a) * yf + (a - b - c + d) * xf * yf;
        }

        /// <summary>
        /// A flame tongue's texel <paramref name="i"/> above its base in column <paramref name="col"/>:
        /// whether it is lit, and in what colour. The noise scrolls up, so tongues rise and break
        /// off at the tips.
        /// </summary>
        public static bool Tongue(int col, int i, int height, float time, int seed, out Color32 colour)
        {
            float u = height <= 0 ? 1f : i / (float)height;
            float v = Noise((col + seed) * 0.55f, (i - time * 16f) * 0.42f + seed * 0.13f);
            colour = At(0.15f + u * 0.85f);
            return v > 0.28f + u * 0.62f;
        }
    }
}
