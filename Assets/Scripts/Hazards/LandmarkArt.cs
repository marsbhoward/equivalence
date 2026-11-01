using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Biomes;

namespace Convergence.Hazards
{
    /// <summary>
    /// The pictures of a room's LANDMARKS (RoomShape.Landmark): one drawing over the whole
    /// footprint, at the floor's density, wearing the place - a toppled giant head (weathered
    /// stone in the tower, moss-grown in the forest, gilt-veined marble wound with ivy in the sky
    /// ruin, crusted white on the flats) and a fallen column (a log in the forest, a six-sided salt
    /// pillar on the flats). Pivoted at the footprint's foot, so it sorts as one block.
    /// Placeholders in the procedural style (Core/Spr), cached per kind, place and facing.
    /// </summary>
    static class LandmarkArt
    {
        const float Ppu = Art.Scenery.Ppu;
        static readonly Dictionary<int, Sprite> _cache = new();

        /// <summary>How far the picture rises above its footprint's north edge, in texels.</summary>
        const int Rise = 34;

        struct Pal { public Color Lit, Mid, Dark, Line, Accent, AccentDark; public bool Moss; }

        static Pal Palette => Biome.Current switch
        {
            BiomeKind.Forest => new Pal { Lit = new(0.40f, 0.42f, 0.36f), Mid = new(0.28f, 0.30f, 0.26f), Dark = new(0.16f, 0.17f, 0.15f),
                                          Line = new(0.06f, 0.07f, 0.05f), Accent = new(0.30f, 0.46f, 0.20f), AccentDark = new(0.17f, 0.30f, 0.12f), Moss = true },
            BiomeKind.SkyRuin => new Pal { Lit = new(0.88f, 0.86f, 0.80f), Mid = new(0.72f, 0.70f, 0.65f), Dark = new(0.52f, 0.51f, 0.50f),
                                           Line = new(0.26f, 0.25f, 0.24f), Accent = new(0.36f, 0.54f, 0.28f), AccentDark = new(0.80f, 0.66f, 0.30f), Moss = true },
            BiomeKind.SaltFlats => new Pal { Lit = new(0.98f, 0.97f, 0.95f), Mid = new(0.88f, 0.84f, 0.84f), Dark = new(0.70f, 0.62f, 0.66f),
                                             Line = new(0.42f, 0.34f, 0.40f), Accent = new(1f, 1f, 1f), AccentDark = new(0.80f, 0.74f, 0.78f), Moss = false },
            BiomeKind.Caves => new Pal { Lit = new(0.40f, 0.38f, 0.44f), Mid = new(0.28f, 0.26f, 0.31f), Dark = new(0.16f, 0.15f, 0.19f),
                                         Line = new(0.05f, 0.05f, 0.07f), Accent = new(0.45f, 0.92f, 0.86f), AccentDark = new(0.20f, 0.50f, 0.52f), Moss = true },
            BiomeKind.SnowyPeaks => new Pal { Lit = new(0.50f, 0.52f, 0.58f), Mid = new(0.36f, 0.38f, 0.44f), Dark = new(0.22f, 0.23f, 0.28f),
                                              Line = new(0.08f, 0.09f, 0.12f), Accent = new(0.92f, 0.94f, 0.98f), AccentDark = new(0.76f, 0.82f, 0.92f), Moss = true },
            _ => new Pal { Lit = new(0.52f, 0.47f, 0.42f), Mid = new(0.38f, 0.34f, 0.31f), Dark = new(0.24f, 0.21f, 0.19f),
                           Line = new(0.09f, 0.08f, 0.07f), Accent = new(0.30f, 0.27f, 0.24f), AccentDark = new(0.20f, 0.18f, 0.16f), Moss = false },
        };

        public static Sprite For(LandmarkKind kind, int w, int h, bool flip)
        {
            int key = (int)kind | ((int)Biome.Current << 4) | (flip ? 1 << 8 : 0) | (w << 10) | (h << 16);
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            int W = Mathf.RoundToInt(w * Ppu), H = Mathf.RoundToInt(h * Ppu) + Rise;
            var px = new Color32[W * H];
            var pal = Palette;
            if (kind == LandmarkKind.Head) Head(px, W, H, pal);
            else if (Biome.Is(BiomeKind.Forest)) Log(px, W, H, pal);
            else if (Biome.Is(BiomeKind.SaltFlats)) Prism(px, W, H, pal);
            else Drum(px, W, H, pal);
            if (flip)
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W / 2; x++)
                {
                    int a = y * W + x, b = y * W + W - 1 - x;
                    (px[a], px[b]) = (px[b], px[a]);
                }
            Outline(px, W, H, pal.Line);
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), Ppu, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            _cache[key] = s;
            return s;
        }

        // ------------------------------------------------------------------ the toppled head

        /// <summary>A giant's head on its side, the face turned up toward the camera: the crown to
        /// the west, the jaw to the east, brow, closed eye and the nose's ridge standing off it.</summary>
        static void Head(Color32[] px, int W, int H, Pal p)
        {
            float cx = W * 0.5f, cy = H * 0.42f, rx = W * 0.47f, ry = H * 0.40f;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f - cx) / rx, v = (y + 0.5f - cy) / ry;
                // An egg: fuller at the crown (west), narrowing to the jaw (east).
                float squeeze = 1f + 0.18f * u;
                float r = u * u + v * v * squeeze * squeeze;
                if (r > 1f) continue;
                // Lit from the north-west: the top of the form bright, its south flank in shade.
                float n = v * 0.8f - u * 0.3f;
                Color c = n > 0.35f ? p.Lit : n > -0.25f ? p.Mid : p.Dark;
                if (r > 0.86f && v < 0f) c = p.Dark;                       // the shadowed underside
                px[y * W + x] = c;
            }
            // The face, big enough to read at a glance: a deep brow shadow, a sunken eye, the nose
            // standing up off the cheek with its shadow cast east, the lips, the ear's whorl.
            void Ellipse(float fx, float fy, float rx2, float ry2, Color c)
            {
                for (int y = Mathf.FloorToInt(fy - ry2); y <= fy + ry2; y++)
                for (int x = Mathf.FloorToInt(fx - rx2); x <= fx + rx2; x++)
                {
                    if (x < 0 || y < 0 || x >= W || y >= H || px[y * W + x].a == 0) continue;
                    float u2 = (x + 0.5f - fx) / rx2, v2 = (y + 0.5f - fy) / ry2;
                    if (u2 * u2 + v2 * v2 <= 1f) px[y * W + x] = c;
                }
            }
            Ellipse(W * 0.50f, H * 0.62f, W * 0.15f, H * 0.07f, p.Dark);     // the brow's shadow
            Ellipse(W * 0.50f, H * 0.52f, W * 0.09f, H * 0.06f, p.Line);     // the sunken eye
            Ellipse(W * 0.49f, H * 0.53f, W * 0.05f, H * 0.025f, p.Mid);     // its closed lid
            for (int y = 0; y < H * 0.28f; y++)                                // the nose: a ridge widening down
            {
                float half = 2f + y * 0.18f;
                int yy = Mathf.RoundToInt(H * 0.62f - y);
                for (int x = Mathf.RoundToInt(W * 0.66f - half); x <= W * 0.66f + half; x++)
                    if (x >= 0 && x < W && yy >= 0 && px[yy * W + x].a != 0)
                        px[yy * W + x] = x < W * 0.66f ? p.Lit : p.Mid;
                for (int x = Mathf.RoundToInt(W * 0.66f + half + 1); x <= W * 0.66f + half + 4; x++)
                    if (x < W && yy >= 0 && px[yy * W + x].a != 0) px[yy * W + x] = p.Dark;   // its shadow
            }
            Ellipse(W * 0.80f, H * 0.30f, W * 0.07f, H * 0.03f, p.Line);     // the lips
            Ellipse(W * 0.22f, H * 0.40f, W * 0.05f, H * 0.11f, p.Dark);     // the ear
            Ellipse(W * 0.22f, H * 0.40f, W * 0.025f, H * 0.06f, p.Mid);
            // A crack across the crown.
            for (int k = 0; k < 22; k++)
            {
                int x = Mathf.RoundToInt(W * 0.10f + k * 1.3f), y = Mathf.RoundToInt(H * 0.60f + Mathf.Sin(k * 0.9f) * 3f + k * 0.5f);
                if (x < W && y < H && px[y * W + x].a > 0) px[y * W + x] = p.Line;
            }
            Growth(px, W, H, p);
        }

        // ------------------------------------------------------------------ the fallen column

        /// <summary>A column drum on its side: a long cylinder, its cut end toward the viewer's
        /// side, fluting along it.</summary>
        static void Drum(Color32[] px, int W, int H, Pal p)
        {
            int top = H - 6, bottom = 4;
            float endW = 16f;
            for (int y = bottom; y < top; y++)
            for (int x = 0; x < W; x++)
            {
                float v = (y - bottom) / (float)(top - bottom);            // 0 underside .. 1 top
                float endEdge = W - endW * Mathf.Sqrt(Mathf.Max(0f, 1f - (2f * v - 1f) * (2f * v - 1f)));
                if (x > W - 2) continue;
                Color c = v > 0.72f ? p.Lit : v > 0.35f ? p.Mid : p.Dark;
                if (x > endEdge)
                {
                    // The cut end: pale stone in rings.
                    float d = Mathf.Abs(2f * v - 1f);
                    c = ((int)(d * 6f) & 1) == 0 ? p.Lit : p.Mid;
                }
                else if ((y - bottom) % 6 == 0) c = Color.Lerp(c, p.Line, 0.4f);   // fluting
                px[y * W + x] = c;
            }
            // A broken west end: bites out of it.
            for (int y = bottom; y < top; y++)
            {
                int bite = Mathf.RoundToInt(3f + 3f * Mathf.Sin(y * 0.9f));
                for (int x = 0; x < bite; x++) px[y * W + x] = default;
            }
            Growth(px, W, H, p);
        }

        /// <summary>A fallen trunk: bark in ridges, a cut end showing its rings, moss along the top.</summary>
        static void Log(Color32[] px, int W, int H, Pal p)
        {
            var bark = new Color(0.30f, 0.22f, 0.15f);
            var barkDark = new Color(0.17f, 0.12f, 0.08f);
            var wood = new Color(0.62f, 0.48f, 0.30f);
            int top = H - 8, bottom = 3;
            for (int y = bottom; y < top; y++)
            for (int x = 1; x < W - 1; x++)
            {
                float v = (y - bottom) / (float)(top - bottom);
                Color c = v > 0.6f ? Color.Lerp(bark, Color.white, 0.08f) : v > 0.25f ? bark : barkDark;
                if (((x / 3) + (int)(Mathf.Sin(y * 0.5f) * 2f)) % 5 == 0) c = barkDark;
                if (x > W - 14)
                {
                    float d = Mathf.Abs(2f * v - 1f), ring = Mathf.Sqrt(d * d + ((x - (W - 7f)) / 7f) * ((x - (W - 7f)) / 7f));
                    if (ring < 1f) c = ((int)(ring * 5f) & 1) == 0 ? wood : Color.Lerp(wood, barkDark, 0.4f);
                }
                px[y * W + x] = c;
            }
            Growth(px, W, H, p);
        }

        /// <summary>A six-sided salt pillar on its side: three long faces in three values.</summary>
        static void Prism(Color32[] px, int W, int H, Pal p)
        {
            int top = H - 4, bottom = 3;
            for (int y = bottom; y < top; y++)
            for (int x = 0; x < W - 1; x++)
            {
                float v = (y - bottom) / (float)(top - bottom);
                int inset = Mathf.RoundToInt(Mathf.Abs(2f * v - 1f) * 6f);
                if (x < inset || x > W - 1 - inset) continue;
                px[y * W + x] = v > 0.66f ? p.Lit : v > 0.33f ? p.Mid : p.Dark;
            }
        }

        // ------------------------------------------------------------------ shared

        /// <summary>Moss (forest), ivy (sky ruin) along the top of the form; a few salt flecks.</summary>
        static void Growth(Color32[] px, int W, int H, Pal p)
        {
            if (!p.Moss) return;
            for (int x = 0; x < W; x++)
            {
                int y = H - 1;
                while (y > 0 && px[y * W + x].a == 0) y--;
                if (y <= 0) continue;
                int depth = 2 + (int)(3f * (0.5f + 0.5f * Mathf.Sin(x * 0.37f) * Mathf.Sin(x * 0.11f + 1f)));
                for (int k = 0; k < depth && y - k > 0; k++)
                    px[(y - k) * W + x] = k == 0 ? p.Accent : (x + k) % 3 == 0 ? p.AccentDark : p.Accent;
            }
        }

        /// <summary>A one-texel line round the shape, as the blocks have.</summary>
        static void Outline(Color32[] px, int W, int H, Color line)
        {
            var src = (Color32[])px.Clone();
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (src[y * W + x].a != 0) continue;
                bool edge = (x > 0 && src[y * W + x - 1].a != 0) || (x < W - 1 && src[y * W + x + 1].a != 0)
                         || (y > 0 && src[(y - 1) * W + x].a != 0) || (y < H - 1 && src[(y + 1) * W + x].a != 0);
                if (edge) px[y * W + x] = line;
            }
        }
    }
}
