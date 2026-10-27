using UnityEngine;

namespace Convergence.Hub
{
    /// <summary>
    /// The room's floor, north wall face and loft drawn texel by texel in a design - texture pictures
    /// at the hub's density (<see cref="HubArt.Ppu"/>), built once per (design, size) and cached.
    /// Every pattern is hashed from the texel, never random, so a room looks the same every visit.
    /// </summary>
    public static class HubRoomArt
    {
        static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static float Hash(int a, int b = 0, int c = 0) => Combat.FireArt.Hash(a, b, c);
        static float Noise(float x, float y) => Combat.FireArt.Noise(x, y);
        static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, Mathf.Clamp01(t));

        static readonly Color32[] Stone = HubArt.Stone, Oak = HubArt.Oak, Dark = HubArt.DarkWood, Brass = HubArt.Brass;
        static Color32 Lava(int x, int y) => Combat.FireArt.At(Mathf.Clamp01(0.15f + Noise(x * 0.3f, y * 0.4f) * 0.6f));
        static readonly Color32 K = TexelCanvas.K;

        /// <summary>The floor's texel at (x, y) - y counted UP from the bottom.</summary>
        public static Color32 Floor(HubTheme d, int x, int y, int w = 0, int h = 0)
        {
            switch (d)
            {
                case HubTheme.Study:
                {
                    int row = y / 4, off = (row * 7) % 13, board = (x + off) / 13;
                    bool seam = y % 4 == 3 || (x + off) % 13 == 12;
                    return seam ? Dark[4] : Mix(Oak[2], Oak[3], Hash(board, row) * 0.6f + (Hash(x, y, 3) > 0.85f ? 0.3f : 0f));
                }
                case HubTheme.Observatory:
                {
                    bool tile = x % 8 == 0 || y % 8 == 0;
                    var c = tile ? C("#141a30") : Mix(C("#1e2848"), C("#26325a"), Hash(x >> 3, y >> 3) * 0.5f);
                    if (Hash(x, y, 9) > 0.985f) c = Brass[1];
                    // The star chart: brass rings inlaid round the room's middle.
                    float dd = Mathf.Sqrt(Mathf.Pow(x - w * 0.5f, 2) + Mathf.Pow((y - h * 0.5f) * 1.2f, 2));
                    if (w > 0 && (Mathf.Abs(dd - h * 0.32f) < 0.7f || Mathf.Abs(dd - h * 0.46f) < 0.7f)) c = Brass[3];
                    return c;
                }
                case HubTheme.Crypt:
                {
                    int cx = x / 5, cy = y / 4, jx = (x + (cy % 2) * 2) % 5, jy = y % 4;
                    bool edge = jx == 4 || jy == 3;
                    return edge ? C("#16141a") : Mix(C("#3a3640"), C("#4c4652"), Hash(cx, cy) * 0.7f + (jx == 0 && jy == 0 ? 0.3f : 0f));
                }
                case HubTheme.Atrium:
                {
                    bool tile = x % 9 == 8 || y % 6 == 5;
                    var c = tile ? C("#5a3424") : Mix(C("#a0583a"), C("#b8684a"), Hash(x >> 3, y / 6) * 0.5f);
                    if (Noise(x * 0.15f, y * 0.2f) > 0.62f) c = Mix(c, C("#4c7a34"), 0.8f);
                    return c;
                }
                case HubTheme.Salt:
                {
                    int r = y / 5, q = (x + (r % 2) * 4) / 8;
                    bool edge = (x + (r % 2) * 4) % 8 == 0 || y % 5 == 0;
                    return edge ? C("#c8c4ba") : Mix(C("#f0eee6"), C("#e2ded2"), Hash(q, r) * 0.7f);
                }
                case HubTheme.Terrace:
                {
                    bool tile = x % 12 == 11 || y % 8 == 7;
                    return tile ? C("#a8a4a0") : Mix(C("#e8e4dc"), C("#d4d0c8"), Hash(x >> 3, y >> 3) * 0.6f + (Noise(x * 0.3f, y * 0.3f) > 0.7f ? 0.3f : 0f));
                }
                case HubTheme.Sanctum:
                {
                    // Black glass (the user's pick, round 4): large polished tiles, a faint reflection streak, no red.
                    int tx = x % 24, ty = y % 16;
                    if (tx == 23 || ty == 15) return Pal.NB[5];
                    return Mathf.Abs(tx - ty * 1.2f - 4f) < 1.2f ? Pal.NB[2] : Pal.NB[4];
                }
                case HubTheme.Smithy:
                {
                    int row = y / 6, off = (row % 2) * 6, col = (x + off) / 12;
                    bool seam = y % 6 == 5 || (x + off) % 12 == 11;
                    var c = seam ? C("#141010") : Mix(C("#3a3230"), C("#4a3e3a"), Hash(col, row) * 0.6f);
                    if (Noise(x * 0.2f, y * 0.2f) > 0.7f) c = Mix(c, C("#1a1210"), 0.6f);
                    if (Hash(x, y, 5) > 0.97f) c = Pal.Iron[1];
                    return c;
                }
                case HubTheme.Chapel:
                {
                    int u = Mathf.Abs((x + 600) % 12 - 6) + Mathf.Abs((y + 600) % 12 - 6);
                    return u == 6 ? Pal.Gold[3] : u < 6 ? C("#8a1a20") : C("#5a1016");
                }
                case HubTheme.Shore:
                {
                    if (Noise(x * 0.08f + 5f, y * 0.14f) > 0.7f) return (x + y * 2) % 9 == 0 ? Pal.Sea[1] : Pal.Sea[3];
                    float rip = Mathf.Sin(x * 0.25f + Mathf.Sin(y * 0.3f) * 2f + y * 0.9f);
                    var c = rip > 0.85f ? Pal.Sand[1] : rip < -0.85f ? Pal.Sand[3] : Pal.Sand[2];
                    if (Hash(x, y, 4) > 0.993f) c = Pal.Coral[1];
                    if (Hash(x, y, 5) > 0.995f) c = Pal.White;
                    return c;
                }
                case HubTheme.Volcano:
                {
                    bool ch = Mathf.Abs((x + 640) % 32 - 16) < 1 || Mathf.Abs((y + 648) % 24 - 12) < 1;
                    if (ch) return Lava(x, y);
                    bool edge = x % 8 == 7 || y % 6 == 5;
                    return edge ? C("#06060a") : Mix(C("#1a1820"), C("#24222c"), Hash(x >> 3, y / 6) * 0.6f);
                }
                case HubTheme.Dojo:
                {
                    int mx = x / 16, my = y / 8, bx = x % 16, by = y % 8;
                    if (bx == 0 || by == 0) return C("#2a2418");
                    bool half = (mx + my) % 2 == 1;
                    return (half ? x % 2 : y % 2) == 1 ? Pal.Tatami[2] : Pal.Tatami[1];
                }
                case HubTheme.TechLab:
                {
                    bool edge = x % 12 == 0 || y % 12 == 0;
                    var c = edge ? C("#3a4250") : Mix(C("#4a5468"), C("#56607a"), Hash(x >> 2, y >> 2) * 0.5f);
                    if (Hash(x, y, 7) > 0.9f) c = C("#6a7490");
                    return c;
                }
                default:
                {
                    int row = y / 7, off = (row % 2) * 6, col = (x + off) / 12;
                    bool seam = y % 7 == 6 || (x + off) % 12 == 11;
                    return seam ? Stone[5] : Mix(Stone[3], Stone[2], Hash(col, row) * 0.7f);
                }
            }
        }

        /// <summary>The north wall's face at (x, y) - y counted DOWN from its top row, h its height.</summary>
        public static Color32 Wall(HubTheme d, int x, int y, int h)
        {
            if (y < 2) return Stone[4];   // the coping
            switch (d)
            {
                case HubTheme.Study:
                    if (y >= h - 9) return y == h - 9 ? Dark[1] : x % 10 == 0 ? Dark[4] : y % 4 == 0 ? Dark[3] : Dark[2];
                    if (y == 7 || y == 8) return y == 7 ? Oak[1] : Oak[3];
                    if (y >= 3 && y < 7 && (x % 6 == 1 || x % 6 == 2) && Hash(x / 6, 5) > 0.3f)
                    {
                        Color32[] jars = { C("#b83850"), C("#4cc8bc"), C("#c09030"), C("#4fd46a") };
                        return y == 3 ? Stone[1] : jars[Mathf.FloorToInt(Hash(x / 6, 6) * 4f) & 3];
                    }
                    return Mix(C("#4a4038"), C("#564a40"), Hash(x >> 2, y >> 2) * 0.5f);
                case HubTheme.Observatory:
                {
                    var c = Mix(C("#121a34"), C("#1a2444"), y / (float)h);
                    if (Hash(x, y, 4) > 0.97f) c = C("#e8eeff");
                    if (y == h - 1) c = Brass[3];
                    return c;
                }
                case HubTheme.Crypt:
                {
                    int nx = x % 24;
                    if (nx > 8 && nx < 16 && y > 5 && y < h - 5)
                    {
                        if (y == h - 6) return Stone[3];
                        if (nx == 12 && y == h - 8) return C("#e8e0c8");
                        if (nx == 12 && y == h - 9) return C("#ffd65a");
                        return C("#0e0c10");
                    }
                    int row = y / 3;
                    return y % 3 == 2 || (x + row * 4) % 9 == 8 ? C("#1a181e") : Mix(C("#3a3640"), C("#46404c"), Hash(x >> 3, row) * 0.5f);
                }
                case HubTheme.Atrium:
                {
                    var c = Mix(Stone[3], Stone[2], Hash(x >> 2, y >> 2) * 0.5f);
                    if (Noise(x * 0.25f, y * 0.3f) > 0.55f) c = Hash(x, y) > 0.5f ? C("#3c6a2c") : C("#5a8c3c");
                    return c;
                }
                case HubTheme.Salt:
                {
                    int band = Mathf.FloorToInt((y + Mathf.Sin(x * 0.08f) * 2f) / 3f);
                    Color32[] strata = { C("#e8c8c4"), C("#f2e2d2"), C("#d8b4b0"), C("#f6eee2") };
                    var c = strata[((band % 4) + 4) % 4];
                    if (Hash(x >> 1, y >> 1, 7) > 0.96f) c = C("#ffffff");
                    return c;
                }
                case HubTheme.Terrace:
                    if (y > h - 7) return y == h - 6 ? C("#f4f0e8") : x % 9 == 0 ? C("#a8a4a0") : C("#d8d4cc");
                    return Noise(x * 0.08f + 3f, y * 0.25f) > 0.6f ? Pal.White : Mix(C("#8ec0f0"), C("#c8e2fa"), y / (float)h);
                case HubTheme.Sanctum:
                {
                    // Dark stone, a band of red crystal at its foot only (the user's pick, round 4).
                    if (y >= h - 3) return Hash(x, 3) > 0.6f ? Pal.RedX[2] : Pal.RedX[4];
                    int row = y / 4, off = (row % 2) * 6;
                    return y % 4 == 3 || (x + off) % 12 == 11 ? Pal.NB[5] : Mix(Pal.NB[3], Pal.NB[2], Hash((x + off) / 12, row) * 0.5f);
                }
                case HubTheme.Smithy:
                {
                    int row = y / 3, off = (row % 2) * 3;
                    var c = y % 3 == 2 || (x + off) % 6 == 5 ? C("#3a2a24") : Mix(C("#8a4a34"), C("#a05a3e"), Hash((x + off) / 6, row) * 0.5f);
                    int t = x % 18;
                    if (y > h * 0.18f && y < h * 0.7f && (t == 6 || t == 7)) c = Pal.Iron[2];
                    if (y > h * 0.55f && y < h * 0.7f && t > 4 && t < 10) c = Pal.Iron[1];
                    return c;
                }
                case HubTheme.Chapel:
                {
                    int ax = x % 20 - 10;
                    if (Mathf.Abs(Mathf.Abs(ax) - 6) < 0.6f && y > 4) return Pal.Gold[2];
                    if (y == 4 + Mathf.RoundToInt(Mathf.Abs(ax) * 0.5f) && Mathf.Abs(ax) < 7) return Pal.Gold[1];
                    return Mix(C("#6a1218"), C("#801820"), Hash(x >> 2, y >> 2) * 0.4f);
                }
                case HubTheme.Shore:
                {
                    float sky = h * 0.36f;
                    if (y < sky) return Mix(C("#8ec8f0"), C("#d0eafc"), y / sky);
                    if (y < h - 3) return Mathf.Sin(x * 0.4f + y * 1.3f) > 0.8f ? Pal.Sea[1] : Mix(Pal.Sea[3], Pal.Sea[4], (y - sky) / (h - sky));
                    return Mathf.Sin(x * 0.5f) > 0f ? Pal.White : Pal.Sea[1];
                }
                case HubTheme.Volcano:
                {
                    int t = x % 22;
                    if (t < 3) return t == 1 ? C("#3a3842") : C("#0e0c12");
                    return Mathf.Sin(x * 0.7f + y * 0.2f) * 0.5f + Noise(x * 0.2f, y * 0.15f) > 0.7f ? Lava(x, y) : Mix(C("#3a0c08"), C("#701c10"), y / (float)h);
                }
                case HubTheme.Dojo:
                {
                    int t = x % 24;
                    if (t < 2 || y == h - 1) return Pal.DarkWood[2];
                    return t % 6 == 1 || y % 5 == 1 ? Pal.DarkWood[3] : Pal.Paper[1];
                }
                case HubTheme.TechLab:
                {
                    int t = x % 20, top = Mathf.RoundToInt(h * 0.18f), bot = Mathf.RoundToInt(h * 0.68f);
                    if (y >= top && y < bot && t >= 3 && t < 17)
                    {
                        if (t == 3 || t == 16 || y == top || y == bot - 1) return Pal.Beige[3];
                        return (y - top - 2) % 3 == 0 && t < 5 + (x * 7) % 9 ? Pal.Crt[2] : Pal.Crt[5];
                    }
                    if (y == bot + 2 || y == bot + 3) return x % 7 == 0 ? C("#222222") : C("#3a3a3a");
                    return y % 6 == 0 ? Pal.Beige[3] : Pal.Beige[2];
                }
                default:
                {
                    int ax = ((x % 32) + 32) % 32 - 16;
                    bool arch = Mathf.Abs(ax) < 7 && y > 6 + Mathf.Max(0, Mathf.Abs(ax) - 3) * 0.8f && y < h - 1;
                    if (arch) return y > h - 4 ? Stone[4] : C("#151318");
                    if (Mathf.Abs(Mathf.Abs(ax) - 8) < 1) return Stone[1];
                    int row = y / 4, off = (row % 2) * 5;
                    return y % 4 == 3 || (x + off) % 10 == 9 ? Stone[5] : Mix(Stone[3], Stone[2], Hash((x + off) >> 3, row) * 0.6f);
                }
            }
        }

        static System.Collections.Generic.Dictionary<string, Sprite> _cache = new();

        static Sprite Build(string key, int w, int h, Vector2 pivot, System.Func<int, int, Color32> at)
        {
            _cache ??= new System.Collections.Generic.Dictionary<string, Sprite>();
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            var c = new TexelCanvas(w, h);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) c.Px[y * w + x] = at(x, y);
            return _cache[key] = c.ToSprite(key, Mathf.RoundToInt(pivot.x * w), Mathf.RoundToInt(pivot.y * h), HubArt.Ppu);
        }

        /// <summary>The floor, <paramref name="width"/> x <paramref name="height"/> world units, pivoted in its middle.</summary>
        public static Sprite FloorSprite(HubTheme d, float width, float height)
        {
            int w = Mathf.CeilToInt(width * HubArt.Ppu), h = Mathf.CeilToInt(height * HubArt.Ppu);
            return Build($"floor.{d}.{w}.{h}", w, h, new Vector2(0.5f, 0.5f), (x, y) => Floor(d, x, y, w, h));
        }

        /// <summary>The north wall's face, pivoted at its bottom middle; a dark line where it meets the floor.</summary>
        public static Sprite WallSprite(HubTheme d, float width, float height)
        {
            int w = Mathf.CeilToInt(width * HubArt.Ppu), h = Mathf.CeilToInt(height * HubArt.Ppu);
            return Build($"wall.{d}.{w}.{h}", w, h, new Vector2(0.5f, 0f), (x, y) =>
            {
                if (y == 0) return K;
                var c = Wall(d, x, h - 1 - y, h);
                c.a = 255;
                return c;
            });
        }

        /// <summary>The loft's TOP - the floor's own pattern lit a step brighter (it is raised).</summary>
        public static Sprite LoftTop(HubTheme d, float width, float height)
        {
            int w = Mathf.CeilToInt(width * HubArt.Ppu), h = Mathf.CeilToInt(height * HubArt.Ppu);
            return Build($"lofttop.{d}.{w}.{h}", w, h, new Vector2(0.5f, 0.5f), (x, y) =>
                x == 0 ? K : Mix(Floor(d, x + 7, y + 3), new Color32(255, 255, 255, 255), 0.14f));
        }

        /// <summary>The loft's FRONT face (its riser), <paramref name="faceHeight"/> world units, in the
        /// wall's material, a lit lip along its top. Pivoted at its top middle - it hangs below the edge.</summary>
        public static Sprite LoftFace(HubTheme d, float width, float faceHeight)
        {
            int w = Mathf.CeilToInt(width * HubArt.Ppu), h = Mathf.Max(3, Mathf.CeilToInt(faceHeight * HubArt.Ppu));
            return Build($"loftface.{d}.{w}.{h}", w, h, new Vector2(0.5f, 1f), (x, y) =>
            {
                if (y == h - 1) return Stone[0];
                if (y == 0 || x == 0) return K;
                return Mix(Wall(d, x, 4 + (h - 1 - y) * 2, 40), new Color32(0, 0, 0, 255), 0.25f);
            });
        }
    }
}
