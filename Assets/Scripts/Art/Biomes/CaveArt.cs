using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// THE CAVES (the user's call, 2026-11-01; first pass, out of the rotation until signed off):
    /// the darkest place - a rock floor worn smooth, a rough rock face to the north veined with
    /// crystal, and past the lip an abyss with the glow of fungus and crystal far below. The room's
    /// light is the crystals'. Texel-drawn in the floor tiles' flat style; the tiles are periodic
    /// so they repeat without a seam.
    /// </summary>
    public static class CaveArt
    {
        const float Ppu = FloorArt.PixelsPerUnit;
        static readonly Dictionary<int, Sprite> _cache = new();

        static readonly Color[] Rock =
        {
            new(0.25f, 0.23f, 0.27f), new(0.29f, 0.27f, 0.31f), new(0.33f, 0.31f, 0.35f), new(0.37f, 0.35f, 0.39f),
        };
        static readonly Color Crack = new(0.15f, 0.14f, 0.17f), Pebble = new(0.44f, 0.42f, 0.46f);
        public static readonly Color Crystal = new(0.45f, 0.92f, 0.86f), CrystalDeep = new(0.20f, 0.50f, 0.52f),
                                     Amethyst = new(0.66f, 0.50f, 0.94f);

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + (s + 1) * 982451653);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        static float Noise(float x, float y, int period, int s)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi, u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            int x0 = Mod(xi, period), x1 = Mod(xi + 1, period), y0 = Mod(yi, period), y1 = Mod(yi + 1, period);
            float a = Hash(x0, y0, s), b = Hash(x1, y0, s), c = Hash(x0, y1, s), d = Hash(x1, y1, s);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        static Color Pick(Color[] ramp, float v) => ramp[Mathf.Clamp(Mathf.FloorToInt(v * ramp.Length), 0, ramp.Length - 1)];

        static Sprite Make(Color[] px, int w, int h, Vector2 pivot, bool repeat)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, Ppu, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        /// <summary>The floor: rock in broad slow patches, worn cracks, pebbles, a rare fleck of
        /// crystal. Mid-dark, low contrast - the bodies stand on it.</summary>
        public static Sprite Ground()
        {
            const int key = 1;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int S = 256;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = Noise(x / 32f, y / 32f, S / 32, 3) * 0.7f + Noise(x / 8f, y / 8f, S / 8, 4) * 0.3f;
                var c = Pick(Rock, n);
                float cr = Mathf.Abs(Noise(x / 16f, y / 16f, S / 16, 7) - 0.5f);
                if (cr < 0.018f) c = Crack;
                else if (cr < 0.03f) c = Color.Lerp(c, Crack, 0.4f);
                float h = Hash(x >> 1, y >> 1, 9);
                if (h > 0.992f) c = Pebble;
                else if (h > 0.9985f - 0.0005f && Hash(x, y, 11) > 0.6f) c = Crystal;
                px[y * S + x] = c;
            }
            var s = Make(px, S, S, new Vector2(0.5f, 0.5f), true);
            _cache[key] = s;
            return s;
        }

        /// <summary>What lies past the north wall's cap: more rock, darker.</summary>
        public static Sprite Beyond()
        {
            const int key = 2;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int S = 128;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = Noise(x / 16f, y / 16f, S / 16, 5);
                px[y * S + x] = Color.Lerp(new Color(0.07f, 0.065f, 0.08f), new Color(0.12f, 0.11f, 0.13f), n);
            }
            var s = Make(px, S, S, new Vector2(0.5f, 0.5f), true);
            _cache[key] = s;
            return s;
        }

        /// <summary>The cap over the north face: rough rock top.</summary>
        public static Sprite Top()
        {
            const int key = 3;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int S = 96;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = Noise(x / 12f, y / 12f, S / 12, 6);
                px[y * S + x] = Pick(Rock, n * 0.9f);
            }
            var s = Make(px, S, S, new Vector2(0.5f, 0.5f), true);
            _cache[key] = s;
            return s;
        }

        /// <summary>The north face: rough rock in slumped blocks, streaked down by seep, crystal
        /// veins catching the light, drips hanging off the top. <paramref name="w"/> x
        /// <paramref name="h"/> texels, bottom row first.</summary>
        public static Color[] Face(int w, int h, int seed)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float n = Noise(x / 14f, y / 22f, 4096, seed) * 0.6f + Noise(x / 5f, y / 30f, 4096, seed + 1) * 0.4f;
                var c = Color.Lerp(Rock[0], Rock[3], n) * 0.82f;
                // Block edges: where a slow field crosses a level, a dark seam.
                float b = Noise(x / 26f, y / 18f, 4096, seed + 2);
                if (Mathf.Abs(b - 0.5f) < 0.02f) c = Crack;
                // Seep streaks running down.
                if (Hash(x / 3, 0, seed + 3) > 0.86f && Noise(x / 3f, y / 9f, 4096, seed + 4) > 0.55f) c *= 0.82f;
                // Crystal veins.
                float v = Mathf.Abs(Noise(x / 30f, y / 12f, 4096, seed + 5) - 0.5f);
                if (v < 0.012f) c = Hash(x, y, seed + 6) > 0.5f ? Crystal : CrystalDeep;
                // Darker toward the foot.
                c *= Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(y / (h * 0.5f)));
                c.a = 1f;
                px[y * w + x] = c;
            }
            // Drips hanging from the top edge.
            for (int x = 2; x < w - 2; x += 5)
            {
                if (Hash(x, 1, seed + 7) < 0.6f) continue;
                int len = 3 + (int)(Hash(x, 2, seed + 7) * 9f);
                for (int k = 0; k < len; k++)
                {
                    int y = h - 1 - k, half = k < len / 2 ? 1 : 0;
                    for (int d = -half; d <= half; d++) px[y * w + x + d] = Rock[2];
                }
            }
            return px;
        }
    }
}
