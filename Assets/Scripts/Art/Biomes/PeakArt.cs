using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// THE SNOWY PEAKS (the user's call, 2026-11-01; first pass, out of the rotation until signed
    /// off): a ledge high on a mountain - packed snow underfoot with blue shadow in its hollows and
    /// rock showing through, a cliff of dark rock for the north face with snow on every ledge and
    /// icicles off its brow, and past the lip the valley far below. Kept off the salt's white: blue
    /// shadows, dark rock, a cold sky. Texel-drawn in the floor tiles' flat style; periodic tiles.
    /// </summary>
    public static class PeakArt
    {
        const float Ppu = FloorArt.PixelsPerUnit;
        static readonly Dictionary<int, Sprite> _cache = new();

        static readonly Color[] Snow =
        {
            new(0.62f, 0.68f, 0.80f), new(0.70f, 0.76f, 0.87f), new(0.78f, 0.83f, 0.92f), new(0.85f, 0.89f, 0.95f),
        };
        static readonly Color RockDark = new(0.20f, 0.21f, 0.25f), RockMid = new(0.30f, 0.31f, 0.36f),
                              RockLit = new(0.42f, 0.43f, 0.48f), Ice = new(0.80f, 0.92f, 1f);

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

        static Sprite Make(Color[] px, int w, int h, bool repeat)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        /// <summary>Packed snow: wind ripples, blue in the hollows, rock showing through in
        /// patches. Kept a step under white so a body and a telegraph still read on it.</summary>
        public static Sprite Ground()
        {
            const int key = 1;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int S = 256;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = Noise(x / 32f, y / 32f, S / 32, 3) * 0.75f + Noise(x / 8f, y / 8f, S / 8, 4) * 0.25f;
                // Wind ripples: long low waves across the snow (whole waves across the tile, so it
                // repeats without a seam).
                float rip = Mathf.Sin(2f * Mathf.PI * (x * 9 + y * 2) / S + Noise(x / 64f, y / 64f, S / 64, 5) * 6f);
                var c = Pick(Snow, n * 0.85f + (rip > 0.86f ? 0.18f : 0f));
                // Rock showing through in a few worn patches, a mid grey - never a black hole.
                float bare = Noise(x / 32f, y / 32f, S / 32, 8);
                if (bare < 0.08f) c = Hash(x >> 1, y >> 1, 9) > 0.4f ? RockLit : RockMid;
                else if (bare < 0.11f) c = Color.Lerp(c, RockLit, 0.5f);
                px[y * S + x] = c;
            }
            var s = Make(px, S, S, true);
            _cache[key] = s;
            return s;
        }

        /// <summary>Past the north wall: a cold sky over higher peaks (the far layers carry the
        /// mountains themselves).</summary>
        public static Sprite Beyond()
        {
            const int key = 2;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int S = 64;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                px[y * S + x] = Color.Lerp(new Color(0.55f, 0.64f, 0.80f), new Color(0.62f, 0.70f, 0.84f), (Hash(x >> 3, y >> 3, 2)) * 0.3f);
            var s = Make(px, S, S, true);
            _cache[key] = s;
            return s;
        }

        /// <summary>The cap over the north face: snow lying on rock.</summary>
        public static Sprite Top()
        {
            const int key = 3;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int S = 96;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                px[y * S + x] = Pick(Snow, 0.55f + Noise(x / 12f, y / 12f, S / 12, 6) * 0.45f);
            var s = Make(px, S, S, true);
            _cache[key] = s;
            return s;
        }

        /// <summary>The north face: dark rock in tilted strata, snow lying on every ledge, icicles
        /// off the brow. Bottom row first.</summary>
        public static Color[] Face(int w, int h, int seed)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // Strata tilted a little, each band its own value.
                float band = (y + x * 0.12f + Noise(x / 40f, 0f, 4096, seed) * 8f) / 9f;
                int b = Mathf.FloorToInt(band);
                float f = band - b;
                Color c = (b % 3) switch { 0 => RockDark, 1 => RockMid, _ => Color.Lerp(RockMid, RockLit, 0.5f) };
                if (f > 0.82f)
                {
                    // The ledge's top: snow lies on it where it is wide enough.
                    c = Noise(x / 6f, b, 4096, seed + 1) > 0.35f ? Snow[3] : RockLit;
                }
                else if (f < 0.1f) c = Color.Lerp(c, Color.black, 0.25f);
                if (Hash(x, y, seed + 2) > 0.97f) c = Color.Lerp(c, Snow[1], 0.6f);   // driven flecks
                c *= Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(y / (h * 0.5f)));
                c.a = 1f;
                px[y * w + x] = c;
            }
            // Snow along the brow, and icicles off it.
            for (int x = 0; x < w; x++)
            {
                int depth = 3 + (int)(Noise(x / 5f, 1f, 4096, seed + 3) * 4f);
                for (int k = 0; k < depth; k++) px[(h - 1 - k) * w + x] = k == 0 ? Snow[3] : Snow[2];
                if (x % 4 == 0 && Hash(x, 3, seed + 4) > 0.55f)
                {
                    int len = 3 + (int)(Hash(x, 4, seed + 4) * 10f);
                    for (int k = 0; k < len; k++)
                    {
                        int y = h - 1 - depth - k;
                        if (y < 0) break;
                        px[y * w + x] = Ice;
                        if (k < len / 3 && x + 1 < w) px[y * w + x + 1] = Color.Lerp(Ice, RockMid, 0.4f);
                    }
                }
            }
            return px;
        }
    }
}
