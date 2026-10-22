using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// The Drowned Forest's ground, canopy and tree line - texel-drawn at the floor's density
    /// (FloorArt.PixelsPerUnit), in the flat clustered style of the floor tiles: a value is picked
    /// from a short ramp, never blended, so the ground reads as pixel art rather than a photo.
    ///
    /// Tiles are PERIODIC (their noise wraps), so they tile across the floor and the canopy
    /// without a seam. The forest has taken the floor: a few of the tower's tiles survive under
    /// the moss.
    /// </summary>
    public static class ForestArt
    {
        const float Ppu = FloorArt.PixelsPerUnit;
        const int GroundSize = 384;   // ~10 units - large enough that the repeat is not seen
        const int CanopySize = 192;

        static readonly Dictionary<int, Sprite> _cache = new();

        static readonly Color[] Moss =
        {
            new(0.105f, 0.15f, 0.095f), new(0.13f, 0.19f, 0.115f), new(0.16f, 0.23f, 0.13f),
            new(0.20f, 0.275f, 0.15f), new(0.25f, 0.32f, 0.17f),
        };
        static readonly Color[] Litter = { new(0.34f, 0.25f, 0.14f), new(0.42f, 0.30f, 0.15f), new(0.29f, 0.20f, 0.13f) };
        static readonly Color[] Leaf =
        {
            new(0.035f, 0.07f, 0.05f), new(0.06f, 0.11f, 0.07f), new(0.09f, 0.16f, 0.09f),
            new(0.13f, 0.22f, 0.11f), new(0.19f, 0.30f, 0.14f),
        };
        static readonly Color[] Bark = { new(0.12f, 0.08f, 0.06f), new(0.18f, 0.12f, 0.08f), new(0.25f, 0.17f, 0.11f), new(0.31f, 0.22f, 0.14f) };
        static readonly Color OldStone = new(0.25f, 0.27f, 0.22f), OldMortar = new(0.12f, 0.17f, 0.10f);

        // ------------------------------------------------------------------ noise (periodic)

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + (s + 1) * 982451653);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Value noise that wraps every <paramref name="period"/> lattice cells.</summary>
        static float Noise(float x, float y, int period, int s)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            int x0 = Mod(xi, period), x1 = Mod(xi + 1, period), y0 = Mod(yi, period), y1 = Mod(yi + 1, period);
            float a = Hash(x0, y0, s), b = Hash(x1, y0, s), c = Hash(x0, y1, s), d = Hash(x1, y1, s);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        /// <summary>Three octaves over a tile <paramref name="size"/> texels wide; cell = base period in texels.</summary>
        static float Fbm(int x, int y, int size, int cell, int s)
        {
            float t = 0f, amp = 0.5f, sum = 0f;
            for (int o = 0; o < 3; o++)
            {
                int c = Mathf.Max(2, cell >> o), period = size / c;
                t += Noise(x / (float)c, y / (float)c, period, s + o * 17) * amp;
                sum += amp; amp *= 0.55f;
            }
            return t / sum;
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;
        static Color Pick(Color[] ramp, float v) => ramp[Mathf.Clamp(Mathf.FloorToInt(v * ramp.Length), 0, ramp.Length - 1)];

        // ------------------------------------------------------------------ the ground

        /// <summary>Moss and leaf litter, roots, and old tiles surviving under it all.</summary>
        public static Sprite Ground()
        {
            const int key = 1;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            int S = GroundSize;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = Fbm(x, y, S, 48, 1);
                float grain = (Hash(x >> 1, y >> 1, 2) - 0.5f) * 0.16f;   // two-texel clusters, not salt
                Color c = Pick(Moss, n * 1.15f - 0.08f + grain);

                // old tiles: where a slow field is high, the running bond shows through
                float ruin = Noise(x / 96f, y / 96f, S / 96, 5);
                if (ruin > 0.72f && Noise(x / 12f, y / 12f, S / 12, 6) > 0.42f)
                {
                    int row = y / 12, off = (row & 1) * 12;
                    bool joint = (x + off) % 24 == 0 || y % 12 == 0;
                    float tone = (Hash((x + off) / 24, row, 7) - 0.5f) * 0.08f;
                    c = joint ? OldMortar : Shade(OldStone, tone + grain * 0.4f);
                    if (!joint && Noise(x / 6f, y / 6f, S / 6, 8) > 0.62f) c = Moss[3];   // moss creeping over
                }
                else
                {
                    // leaf litter in small clumps
                    if (Noise(x / 5f, y / 5f, S / 5, 9) > 0.78f && Hash(x, y, 10) > 0.35f)
                        c = Litter[(int)(Hash(x >> 1, y >> 1, 11) * 3f) % 3];
                    // grass tufts: a short lighter stroke
                    if (Hash(x, y >> 2, 12) > 0.993f) c = Moss[4];
                }
                px[y * S + x] = c;
            }

            // roots: dark wandering lines, wrapped
            var rng = new System.Random(4242);
            for (int r = 0; r < 14; r++)
            {
                float x = rng.Next(S), y = rng.Next(S), a = (float)rng.NextDouble() * 6.28f;
                int len = 40 + rng.Next(90);
                for (int i = 0; i < len; i++)
                {
                    a += ((float)rng.NextDouble() - 0.5f) * 0.5f;
                    x += Mathf.Cos(a); y += Mathf.Sin(a) * 0.7f;
                    int ix = Mod(Mathf.RoundToInt(x), S), iy = Mod(Mathf.RoundToInt(y), S);
                    px[iy * S + ix] = Bark[1];
                    px[Mod(iy + 1, S) * S + ix] = Bark[2];
                }
            }
            return _cache[key] = Make(px, S, S, "forest.ground");
        }

        // ------------------------------------------------------------------ the canopy

        /// <summary>Leaf masses seen from above. <paramref name="deep"/> is the forest beyond the
        /// room, darker; otherwise the canopy's edge overhanging it.</summary>
        public static Sprite Canopy(bool deep)
        {
            int key = deep ? 3 : 2;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            int S = CanopySize;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                // clumps: a cellular-ish field from two noises, lit from the upper left
                float n = Fbm(x, y, S, 24, 20);
                float lit = Fbm(x - 3, y + 3, S, 24, 20);   // the same field, offset toward the light
                float v = n * 0.9f + (n - lit) * 2.2f;
                v = deep ? v * 0.7f - 0.05f : v;
                px[y * S + x] = Pick(Leaf, v + (Hash(x >> 1, y >> 1, 21) - 0.5f) * 0.1f);
            }
            return _cache[key] = Make(px, S, S, deep ? "forest.deep" : "forest.canopy");
        }

        // ------------------------------------------------------------------ the tree line

        /// <summary>The north face: trunks standing in the dark under the canopy, ferns at their feet.</summary>
        public static Color[] Face(int W, int H, int seed)
        {
            var px = new Color[W * H];
            var rng = new System.Random(seed * 31 + 11);
            var under = new Color(0.03f, 0.05f, 0.04f);

            // the dark between the trunks, lighter toward the floor
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float k = 1f - y / (float)H;
                px[y * W + x] = Shade(under, k * 0.05f + (Hash(x >> 1, y >> 1, seed) - 0.5f) * 0.02f);
            }

            // trunks: far ones thin and dark, near ones wide and lit; laid far to near
            for (int layer = 0; layer < 2; layer++)
            {
                int x = -rng.Next(20);
                while (x < W)
                {
                    int w = layer == 0 ? 6 + rng.Next(6) : 11 + rng.Next(10);
                    float dim = layer == 0 ? 0.55f : 1f;
                    for (int tx = 0; tx < w; tx++)
                    {
                        int fx = x + tx;
                        if (fx < 0 || fx >= W) continue;
                        float u = tx / (float)(w - 1);
                        for (int y = 0; y < H; y++)
                        {
                            // roots flare at the foot
                            float flare = y < 6 ? (6 - y) * 0.6f : 0f;
                            if (tx < -flare || tx > w - 1 + flare) continue;
                            int idx = Mathf.Clamp(Mathf.FloorToInt((1f - Mathf.Abs(u - 0.35f) * 1.6f) * 4f), 0, 3);
                            var c = Bark[idx];
                            if ((fx * 7 + y / 3) % 11 == 0) c = Bark[0];   // bark furrows
                            px[y * W + fx] = Color.Lerp(under, c, dim);
                        }
                    }
                    x += w + (layer == 0 ? 6 + rng.Next(18) : 18 + rng.Next(34));
                }
            }

            // ferns along the foot
            for (int i = 0; i < W / 5; i++)
            {
                int cx = rng.Next(W), h = 4 + rng.Next(8);
                for (int y = 0; y < h; y++)
                {
                    int spread = (h - y) / 2;
                    for (int dx = -spread; dx <= spread; dx += 2)
                    {
                        int fx = cx + dx + (y & 1);
                        if (fx >= 0 && fx < W) px[y * W + fx] = Leaf[2 + (y > h / 2 ? 1 : 0)];
                    }
                }
            }
            return px;
        }

        // ------------------------------------------------------------------ pieces

        static Color Shade(Color c, float d) => new(Mathf.Clamp01(c.r + d), Mathf.Clamp01(c.g + d), Mathf.Clamp01(c.b + d), 1f);

        static Sprite Make(Color[] px, int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave, name = name,
            };
            tex.SetPixels(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }
}
