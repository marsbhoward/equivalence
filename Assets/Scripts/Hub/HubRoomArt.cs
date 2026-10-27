using UnityEngine;

namespace Convergence.Hub
{
    /// <summary>
    /// The hub room's designs (the user's group-6 picks, 2026-10-26: all six, a reward pool beside the
    /// portrait poses - which are earned and how is a later grouping). Ids are PERMANENT and the list
    /// APPEND-ONLY: <see cref="Chain.RoomLayout.Design"/> saves the number.
    /// </summary>
    public enum RoomDesign { Cloister = 0, Study = 1, Observatory = 2, Crypt = 3, Atrium = 4, Salt = 5 }

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
        static readonly Color32 K = TexelCanvas.K;

        public static string NameOf(RoomDesign d) => d switch
        {
            RoomDesign.Study => "Alchemist's study",
            RoomDesign.Observatory => "Observatory",
            RoomDesign.Crypt => "Crypt",
            RoomDesign.Atrium => "Garden atrium",
            RoomDesign.Salt => "Salt chamber",
            _ => "Cloister",
        };

        /// <summary>The floor's texel at (x, y) - y counted UP from the bottom.</summary>
        public static Color32 Floor(RoomDesign d, int x, int y, int w = 0, int h = 0)
        {
            switch (d)
            {
                case RoomDesign.Study:
                {
                    int row = y / 4, off = (row * 7) % 13, board = (x + off) / 13;
                    bool seam = y % 4 == 3 || (x + off) % 13 == 12;
                    return seam ? Dark[4] : Mix(Oak[2], Oak[3], Hash(board, row) * 0.6f + (Hash(x, y, 3) > 0.85f ? 0.3f : 0f));
                }
                case RoomDesign.Observatory:
                {
                    bool tile = x % 8 == 0 || y % 8 == 0;
                    var c = tile ? C("#141a30") : Mix(C("#1e2848"), C("#26325a"), Hash(x >> 3, y >> 3) * 0.5f);
                    if (Hash(x, y, 9) > 0.985f) c = Brass[1];
                    // The star chart: brass rings inlaid round the room's middle.
                    float dd = Mathf.Sqrt(Mathf.Pow(x - w * 0.5f, 2) + Mathf.Pow((y - h * 0.5f) * 1.2f, 2));
                    if (w > 0 && (Mathf.Abs(dd - h * 0.32f) < 0.7f || Mathf.Abs(dd - h * 0.46f) < 0.7f)) c = Brass[3];
                    return c;
                }
                case RoomDesign.Crypt:
                {
                    int cx = x / 5, cy = y / 4, jx = (x + (cy % 2) * 2) % 5, jy = y % 4;
                    bool edge = jx == 4 || jy == 3;
                    return edge ? C("#16141a") : Mix(C("#3a3640"), C("#4c4652"), Hash(cx, cy) * 0.7f + (jx == 0 && jy == 0 ? 0.3f : 0f));
                }
                case RoomDesign.Atrium:
                {
                    bool tile = x % 9 == 8 || y % 6 == 5;
                    var c = tile ? C("#5a3424") : Mix(C("#a0583a"), C("#b8684a"), Hash(x >> 3, y / 6) * 0.5f);
                    if (Noise(x * 0.15f, y * 0.2f) > 0.62f) c = Mix(c, C("#4c7a34"), 0.8f);
                    return c;
                }
                case RoomDesign.Salt:
                {
                    int r = y / 5, q = (x + (r % 2) * 4) / 8;
                    bool edge = (x + (r % 2) * 4) % 8 == 0 || y % 5 == 0;
                    return edge ? C("#c8c4ba") : Mix(C("#f0eee6"), C("#e2ded2"), Hash(q, r) * 0.7f);
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
        public static Color32 Wall(RoomDesign d, int x, int y, int h)
        {
            if (y < 2) return Stone[4];   // the coping
            switch (d)
            {
                case RoomDesign.Study:
                    if (y >= h - 9) return y == h - 9 ? Dark[1] : x % 10 == 0 ? Dark[4] : y % 4 == 0 ? Dark[3] : Dark[2];
                    if (y == 7 || y == 8) return y == 7 ? Oak[1] : Oak[3];
                    if (y >= 3 && y < 7 && (x % 6 == 1 || x % 6 == 2) && Hash(x / 6, 5) > 0.3f)
                    {
                        Color32[] jars = { C("#b83850"), C("#4cc8bc"), C("#c09030"), C("#4fd46a") };
                        return y == 3 ? Stone[1] : jars[Mathf.FloorToInt(Hash(x / 6, 6) * 4f) & 3];
                    }
                    return Mix(C("#4a4038"), C("#564a40"), Hash(x >> 2, y >> 2) * 0.5f);
                case RoomDesign.Observatory:
                {
                    var c = Mix(C("#121a34"), C("#1a2444"), y / (float)h);
                    if (Hash(x, y, 4) > 0.97f) c = C("#e8eeff");
                    if (y == h - 1) c = Brass[3];
                    return c;
                }
                case RoomDesign.Crypt:
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
                case RoomDesign.Atrium:
                {
                    var c = Mix(Stone[3], Stone[2], Hash(x >> 2, y >> 2) * 0.5f);
                    if (Noise(x * 0.25f, y * 0.3f) > 0.55f) c = Hash(x, y) > 0.5f ? C("#3c6a2c") : C("#5a8c3c");
                    return c;
                }
                case RoomDesign.Salt:
                {
                    int band = Mathf.FloorToInt((y + Mathf.Sin(x * 0.08f) * 2f) / 3f);
                    Color32[] strata = { C("#e8c8c4"), C("#f2e2d2"), C("#d8b4b0"), C("#f6eee2") };
                    var c = strata[((band % 4) + 4) % 4];
                    if (Hash(x >> 1, y >> 1, 7) > 0.96f) c = C("#ffffff");
                    return c;
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
        public static Sprite FloorSprite(RoomDesign d, float width, float height)
        {
            int w = Mathf.CeilToInt(width * HubArt.Ppu), h = Mathf.CeilToInt(height * HubArt.Ppu);
            return Build($"floor.{d}.{w}.{h}", w, h, new Vector2(0.5f, 0.5f), (x, y) => Floor(d, x, y, w, h));
        }

        /// <summary>The north wall's face, pivoted at its bottom middle; a dark line where it meets the floor.</summary>
        public static Sprite WallSprite(RoomDesign d, float width, float height)
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
        public static Sprite LoftTop(RoomDesign d, float width, float height)
        {
            int w = Mathf.CeilToInt(width * HubArt.Ppu), h = Mathf.CeilToInt(height * HubArt.Ppu);
            return Build($"lofttop.{d}.{w}.{h}", w, h, new Vector2(0.5f, 0.5f), (x, y) =>
                x == 0 ? K : Mix(Floor(d, x + 7, y + 3), new Color32(255, 255, 255, 255), 0.14f));
        }

        /// <summary>The loft's FRONT face (its riser), <paramref name="faceHeight"/> world units, in the
        /// wall's material, a lit lip along its top. Pivoted at its top middle - it hangs below the edge.</summary>
        public static Sprite LoftFace(RoomDesign d, float width, float faceHeight)
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
