using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// The Sky Ruin: the tower's own masonry, floating. Its boundary is a pale parapet with the sky
    /// beyond, its north face a wall of arches open onto the sky, and its chasms drop through the
    /// slab's thickness into clouds far below (the user's call: a chasm falls into whatever sky
    /// the biome has). Texel-drawn in the floor tiles' flat style.
    /// </summary>
    public static class SkyArt
    {
        const float Ppu = FloorArt.PixelsPerUnit;
        const int SkyTile = 192;
        /// <summary>How far a broken tile hangs over a chasm, texels at 75 a cell - under the
        /// chasm's edge margin, so drawn floor never promises footing the mask refuses.</summary>
        const float OverhangTexels = 9f;
        static readonly Dictionary<int, Sprite> _cache = new();

        public static readonly Color[] SkyRamp =
        {
            new(0.36f, 0.52f, 0.78f), new(0.44f, 0.60f, 0.84f), new(0.54f, 0.68f, 0.88f), new(0.66f, 0.77f, 0.92f),
        };
        static readonly Color[] Cloud = { new(0.72f, 0.78f, 0.88f), new(0.84f, 0.88f, 0.94f), new(0.95f, 0.97f, 1f) };
        static readonly Color[] Pale =
        {
            new(0.40f, 0.38f, 0.34f), new(0.52f, 0.50f, 0.45f), new(0.63f, 0.60f, 0.54f), new(0.73f, 0.70f, 0.63f), new(0.82f, 0.79f, 0.72f),
        };
        static readonly Color Mortar = new(0.36f, 0.34f, 0.31f), Vine = new(0.24f, 0.40f, 0.18f), VineLit = new(0.36f, 0.54f, 0.24f);

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
            int x0 = period > 0 ? Mod(xi, period) : xi, x1 = period > 0 ? Mod(xi + 1, period) : xi + 1;
            int y0 = period > 0 ? Mod(yi, period) : yi, y1 = period > 0 ? Mod(yi + 1, period) : yi + 1;
            float a = Hash(x0, y0, s), b = Hash(x1, y0, s), c = Hash(x0, y1, s), d = Hash(x1, y1, s);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        /// <summary>Clouds: a lumpy field, squashed flat (clouds are wider than tall), shaded underneath.</summary>
        static Color CloudAt(int x, int y, int period, int s, Color sky, float coverage)
        {
            // Two broad octaves only: a fine one made every cloud's edge a ragged scatter of blocks.
            float n = Noise(x / 32f, y / 16f, period > 0 ? period / 32 : 0, s) * 0.68f
                    + Noise(x / 12f, y / 8f, period > 0 ? period / 12 : 0, s + 1) * 0.32f;
            float edge = 1f - coverage;
            if (n < edge) return sky;
            float below = Noise(x / 32f, (y + 3) / 16f, period > 0 ? period / 32 : 0, s) * 0.68f
                        + Noise(x / 12f, (y + 3) / 8f, period > 0 ? period / 12 : 0, s + 1) * 0.32f;
            // the cloud's underside, where the field thins below it, is its shade
            int idx = n - edge < 0.04f ? 0 : below < n - 0.03f ? 1 : 2;
            return Cloud[idx];
        }

        // ------------------------------------------------------------------ the sky beyond the walls

        public static Sprite Sky()
        {
            const int key = 1;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            int S = SkyTile;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                var sky = SkyRamp[1 + (Hash(x >> 3, y >> 3, 3) > 0.7f ? 1 : 0)];
                px[y * S + x] = CloudAt(x, y, S, 5, sky, 0.42f);
            }
            return _cache[key] = Make(px, S, S, "sky.beyond", TextureWrapMode.Repeat);
        }

        // ------------------------------------------------------------------ the parapet

        public static Sprite Parapet()
        {
            const int key = 2;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            int S = 96;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int row = y / 12, off = (row & 1) * 12;
                bool joint = (x + off) % 24 == 0 || y % 12 == 0;
                float tone = (Hash((x + off) / 24, row, 7) - 0.5f) * 0.12f;
                Color c = joint ? Mortar : Pale[Mathf.Clamp(Mathf.FloorToInt((0.6f + tone) * Pale.Length), 0, Pale.Length - 1)];
                if (!joint && y % 12 == 11) c = Pale[4];                               // each block's lit upper edge
                if (joint && Noise(x / 6f, y / 6f, S / 6, 9) > 0.6f) c = Vine;          // grass in the joints
                px[y * S + x] = c;
            }
            return _cache[key] = Make(px, S, S, "sky.parapet", TextureWrapMode.Repeat);
        }

        // ------------------------------------------------------------------ the north face: arches

        /// <summary>A pale wall of piers and round arches, the sky showing through each arch.</summary>
        public static Color[] Face(int W, int H, int seed)
        {
            var px = new Color[W * H];
            int bay = 150, archW = 90;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int bx = Mod(x + bay / 2, bay) - (bay - archW) / 2;   // position within the bay's arch
                bool inArch = false;
                if (bx >= 0 && bx < archW)
                {
                    float cx = archW / 2f, spring = H * 0.45f, rad = archW / 2f;
                    float u = (bx + 0.5f - cx) / rad;
                    float top = spring + Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)) * rad * 0.55f;
                    inArch = y >= 6 && y < top;
                }
                Color c;
                if (inArch)
                {
                    float k = y / (float)H;
                    var sky = Color.Lerp(SkyRamp[3], SkyRamp[1], k);
                    c = CloudAt(x, y + seed % 97, 0, seed, sky, 0.38f);
                }
                else
                {
                    int row = y / 10, off = (row & 1) * 11;
                    bool joint = (x + off) % 22 == 0 || y % 10 == 0;
                    float tone = (Hash((x + off) / 22, row, seed) - 0.5f) * 0.1f;
                    float foot = 1f - Mathf.Clamp01(y / (H * 0.5f));
                    c = joint ? Mortar : Pale[Mathf.Clamp(Mathf.FloorToInt((0.55f + tone - foot * 0.2f) * Pale.Length), 0, Pale.Length - 1)];
                    if (Noise(x / 8f, y / 8f, 0, seed + 3) > 0.72f && (x + y) % 3 != 0) c = (x & 1) == 0 ? Vine : VineLit;   // ivy
                }
                px[y * W + x] = c;
            }
            return px;
        }

        // ------------------------------------------------------------------ the chasm: open sky below

        /// <summary>
        /// One chasm cell, looking DOWN into the sky: a deeper blue with clouds drifting far below,
        /// drawn in the cell's WORLD position so they run on between cells. Where the cell to the
        /// north stands, the slab's own thickness - pale stone in strata - falls away into the sky;
        /// a pale lip on every other side that meets ground.
        /// </summary>
        public static Sprite ChasmCell(int open, int cellX, int cellY)
        {
            int key = 1000 + (open | (cellX & 63) << 4 | (cellY & 63) << 10);
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;
            const int N = 75;
            bool north = (open & 1) != 0, south = (open & 2) != 0, west = (open & 4) != 0, east = (open & 8) != 0;
            int face = north ? Mathf.RoundToInt(N * 0.5f) : 0;
            var px = new Color[N * N];
            var deep = new Color(0.27f, 0.42f, 0.70f);
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int wx = cellX * N + x, wy = cellY * N + y;
                int fromTop = N - 1 - y;
                Color c;
                if (fromTop < face)
                {
                    float k = fromTop / (float)face;
                    int strata = (fromTop + (wx / 13) % 3) / 6;
                    c = Pale[Mathf.Clamp(3 - strata, 0, 4)];
                    if ((fromTop + wx / 9) % 6 == 0) c = Mortar;
                    if (k > 0.65f) c = Color.Lerp(c, deep, (k - 0.65f) / 0.35f);   // the slab's underside, lost in the haze
                }
                else
                {
                    var sky = Color.Lerp(deep, SkyRamp[0], Hash(wx >> 4, wy >> 4, 21) * 0.2f);
                    c = CloudAt(wx / 2, wy, 0, 31, sky, 0.36f);
                    c = Color.Lerp(c, deep, 0.25f);   // far below: hazier than the sky beyond the walls
                }
                px[y * N + x] = c;
            }
            // Broken tiles hanging over the edge where the cell meets ground - drawn INSIDE the
            // void by less than the edge margin, so drawn floor never promises footing the mask
            // refuses. Each tile snapped at its own depth, mortar between.
            int Over(int along, int side) => 3 + (int)(Hash((cellX * N + along) / 12, cellY * 4 + side, 41) * (OverhangTexels - 3f));
            for (int i = 0; i < N; i++)
            {
                int wx = cellX * N + i, wy = cellY * N + i;
                if (north) for (int d = 0; d < Over(i, 0); d++) px[(N - 1 - d) * N + i] = wx % 12 == 0 ? Mortar : Pale[2 + (d == Over(i, 0) - 1 ? -1 : 0)];
                if (south) for (int d = 0; d < Over(i, 1); d++) px[d * N + i] = wx % 12 == 0 ? Mortar : Pale[3 - (d == Over(i, 1) - 1 ? 2 : 0)];
                if (west) for (int d = 0; d < Over(i, 2); d++) px[i * N + d] = wy % 12 == 0 ? Mortar : Pale[3 - (d == Over(i, 2) - 1 ? 2 : 0)];
                if (east) for (int d = 0; d < Over(i, 3); d++) px[i * N + N - 1 - d] = wy % 12 == 0 ? Mortar : Pale[3 - (d == Over(i, 3) - 1 ? 2 : 0)];
            }
            for (int i = 0; i < N; i++)
            {
                if (north) { px[(N - 1) * N + i] = Pale[4]; px[(N - 2) * N + i] = Pale[3]; }
                if (south) { px[i] = Pale[4]; px[N + i] = Pale[1]; }
                if (west) { px[i * N] = Pale[4]; px[i * N + 1] = Pale[1]; }
                if (east) { px[i * N + N - 1] = Pale[4]; px[i * N + N - 2] = Pale[1]; }
            }
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0f), N);
            s.hideFlags = HideFlags.HideAndDontSave;
            return _cache[key] = s;
        }

        static Sprite Make(Color[] px, int w, int h, string name, TextureWrapMode wrap)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = wrap, hideFlags = HideFlags.HideAndDontSave, name = name };
            tex.SetPixels(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }
}
