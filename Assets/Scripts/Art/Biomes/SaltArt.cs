using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// The Salt Flats: a white crust broken into plates, glaring; salt cliffs in pink and cream
    /// strata for the north face; a pale haze past the room's edge. Its chasm is a brine sinkhole -
    /// the lake system, in salt's colours (LakeSurface). Texel-drawn in the floor tiles' flat style;
    /// the tiles are periodic so they repeat without a seam.
    /// </summary>
    public static class SaltArt
    {
        const float Ppu = FloorArt.PixelsPerUnit;
        static readonly Dictionary<int, Sprite> _cache = new();

        static readonly Color[] Crust =
        {
            new(0.72f, 0.68f, 0.66f), new(0.79f, 0.76f, 0.73f), new(0.85f, 0.82f, 0.80f), new(0.90f, 0.88f, 0.86f),
        };
        static readonly Color Ridge = new(0.97f, 0.96f, 0.95f), RidgeShade = new(0.62f, 0.57f, 0.58f),
                              Blush = new(0.88f, 0.78f, 0.78f);
        static readonly Color[] Strata =
        {
            new(0.86f, 0.74f, 0.72f), new(0.93f, 0.88f, 0.81f), new(0.80f, 0.65f, 0.66f), new(0.90f, 0.82f, 0.76f),
        };

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

        /// <summary>Distance to the nearest plate edge (second-nearest minus nearest feature),
        /// periodic over a tile of <paramref name="cells"/> plates square.</summary>
        static float Edge(float x, float y, int cells, float size, int s)
        {
            int gx = Mathf.FloorToInt(x / size), gy = Mathf.FloorToInt(y / size);
            float d1 = 1e9f, d2 = 1e9f;
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int cx = gx + i, cy = gy + j;
                int wx = Mod(cx, cells), wy = Mod(cy, cells);
                float px = (cx + 0.15f + Hash(wx, wy, s) * 0.7f) * size, py = (cy + 0.15f + Hash(wx, wy, s + 1) * 0.7f) * size;
                float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
            }
            return d2 - d1;
        }

        static Color Pick(Color[] ramp, float v) => ramp[Mathf.Clamp(Mathf.FloorToInt(v * ramp.Length), 0, ramp.Length - 1)];

        // ------------------------------------------------------------------ the crust

        /// <summary>Salt plates: raised white ridges where they meet, each lit on its upper edge and
        /// shadowed below; a blush of pink across some plates.</summary>
        public static Sprite Ground()
        {
            const int key = 1;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int S = 384, cells = 8;
            float size = S / (float)cells;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float e = Edge(x, y, cells, size, 3);
                float above = Edge(x, y + 1, cells, size, 3), under = Edge(x, y - 1, cells, size, 3);
                float n = Noise(x / 24f, y / 24f, S / 24, 5);
                Color c = Pick(Crust, n * 0.9f + (Hash(x >> 1, y >> 1, 6) - 0.5f) * 0.12f + 0.1f);
                if (Noise(x / 64f, y / 64f, S / 64, 7) > 0.62f) c = Color.Lerp(c, Blush, 0.5f);
                // The raised seam, one texel and close to the crust - drawn bright and thick, the
                // plates' web drew the eye away from what stood on them.
                if (e < 1.2f) c = Crust[3];
                else if (above < 1.2f) c = Color.Lerp(c, RidgeShade, 0.45f);   // its shadow, cast down the screen
                if (Hash(x, y, 9) > 0.9965f) c = Color.white;   // a crystal catching the sun
                px[y * S + x] = c;
            }
            return _cache[key] = Make(px, S, S, "salt.ground");
        }

        /// <summary>The cliffs' tops: rough white salt, crystals standing in it.</summary>
        public static Sprite Top()
        {
            const int key = 2;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int S = 96;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = Noise(x / 8f, y / 8f, S / 8, 11) * 0.7f + Noise(x / 3f, y / 3f, S / 3, 12) * 0.3f;
                Color c = Pick(Crust, n + 0.15f);
                if ((x / 3 + y / 3) % 7 == 0 && Hash(x / 3, y / 3, 13) > 0.6f) c = Ridge;   // cubic crystals
                px[y * S + x] = c;
            }
            return _cache[key] = Make(px, S, S, "salt.top");
        }

        /// <summary>The haze beyond the room: pale lilac to white, a slow mottling.</summary>
        public static Sprite Haze()
        {
            const int key = 3;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int S = 128;
            var px = new Color[S * S];
            // Nearly flat: two close tones. A wide two-tone threshold drew camouflage blobs.
            var lo = new Color(0.84f, 0.80f, 0.86f); var hi = new Color(0.87f, 0.84f, 0.89f);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = Noise(x / 32f, y / 32f, S / 32, 21);
                px[y * S + x] = n > 0.62f ? hi : lo;
            }
            return _cache[key] = Make(px, S, S, "salt.haze");
        }

        /// <summary>The north face: a salt cliff in strata, the bands wavering, crystals in them.</summary>
        public static Color[] Face(int W, int H, int seed)
        {
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float wobble = Noise(x / 30f, seed % 13, 4096, seed) * 6f;
                int band = Mathf.FloorToInt((y + wobble) / 7f);
                Color c = Strata[Mod(band, Strata.Length)];
                float foot = 1f - Mathf.Clamp01(y / (H * 0.5f));
                c = Color.Lerp(c, RidgeShade, foot * 0.35f);
                if (Mathf.FloorToInt(y + wobble) % 7 == 0) c = Color.Lerp(c, RidgeShade, 0.35f);   // the bedding line
                if (Hash(x / 2, y / 2, seed + 4) > 0.985f) c = Ridge;
                px[y * W + x] = c;
            }
            return px;
        }

        static Sprite Make(Color[] px, int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave, name = name };
            tex.SetPixels(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }
}
