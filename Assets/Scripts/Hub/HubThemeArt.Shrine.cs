using UnityEngine;
using static Convergence.Hub.HubSketch;

namespace Convergence.Hub
{
    public static partial class HubThemeArt
    {
        public const int ShrineFrames = 6;
        public const float ShrineFps = 4f;

        /// <summary>The manual shrine in <paramref name="theme"/>. Pieces picked as WALL niches (the Study,
        /// the Crypt, the Dojo's shelf) stand free here in the same look - the shrine is moved in edit mode,
        /// so it cannot live in the wall.</summary>
        public static Sprite Shrine(HubTheme theme, int frame)
        {
            if (theme == HubTheme.Cloister) return HubArt.Altar(frame);
            float t = frame / ShrineFps;
            return Cached($"shrine.{theme}.{frame}", t, s => DrawShrine(s, theme, t));
        }

        static void Book(HubSketch s, float x0, float x1, float y0, float y1, Color32 page)
        {
            for (float y = y0; y <= y1; y++) for (float x = x0; x <= x1; x++) s.Set(x, y, Mathf.Approximately(x, Mathf.Round((x0 + x1) / 2f)) ? Pal.Parch[4] : page);
        }

        /// <summary>A free-standing arched niche - the book lit gold inside it.</summary>
        static void Niche(HubSketch s, Color32[] stone, float t, bool wood)
        {
            s.Box3(-10, 0, 20, 5, 30, stone, (x, y) =>
            {
                int ax = Mathf.Abs(x - 10);
                if (y > 6 && y < 26 - Mathf.Max(0, ax - 3) && ax < 7)
                    return y < 9 ? stone[3] : Mix(H("#2a2010"), Pal.Gold[3], C01((y - 6) / 18f) * (0.4f + 0.1f * Mathf.Sin(t * 3f)));
                return wood ? (x % 5 == 0 ? stone[3] : stone[2]) : (y % 5 == 0 ? stone[3] : stone[2]);
            });
            Book(s, -6, 6, -12, -10, Pal.Parch[1]);
        }

        static void DrawShrine(HubSketch s, HubTheme theme, float t)
        {
            switch (theme)
            {
                case HubTheme.Study: Niche(s, Pal.DarkWood, t, true); break;
                case HubTheme.Crypt: Niche(s, Pal.CryptS, t, false); break;
                case HubTheme.Observatory:   // Lectern and globe
                {
                    s.Box3(-6, 0, 12, 5, 16, Pal.Navy);
                    for (int y = -25; y <= -19; y++) for (int x = -8; x <= 8; x++) s.Set(x, y, x == 0 ? Pal.Parch[4] : Hash(x, y) > 0.9f ? Pal.Navy[2] : Pal.Parch[1]);
                    s.Cyl3(18, 0, 3, 8, Pal.Brass);
                    s.Disc(18, -16, 5, 5, (x, y, d) => s.Set(x, y, d > 0.85f ? Pal.Brass[2] : ((x * 3 + Mathf.FloorToInt(t * 8)) % 7 == 0 && Hash(x, y) > 0.5f) ? H("#e8eeff") : Pal.Navy[2]));
                    break;
                }
                case HubTheme.Atrium:   // Potting bench
                {
                    s.Box3(-16, 0, 32, 7, 11, Pal.Wicker, (x, y) => x % 4 == 0 ? Pal.Wicker[3] : Pal.Wicker[2]);
                    Book(s, -7, 7, -20, -14, Pal.Parch[1]);
                    foreach (int px in new[] { -13, 11 }) { s.Box3(px, -11, 5, 2, 4, Pal.Terra); Ivy(s, px, -21, px + 5, -16, 6, px & 7); }
                    break;
                }
                case HubTheme.Salt:   // Salt lamp lectern
                {
                    s.Box3(-9, 0, 18, 6, 14, Pal.SaltW);
                    Book(s, -7, 7, -23, -18, Pal.Parch[1]);
                    s.Box3(13, 0, 7, 3, 3, Pal.DarkWood);
                    for (int y = 0; y < 9; y++) { float w = 3 - Mathf.Abs(y - 4) * 0.4f; for (int x = -Mathf.RoundToInt(w); x <= Mathf.RoundToInt(w); x++) s.Set(16 + x, -5 - y, Mix(Pal.Pink[1], Pal.Pink[3], Hash(x, y) * 0.5f)); }
                    break;
                }
                case HubTheme.Terrace:   // Lectern in the wind
                {
                    s.Box3(-5, 0, 10, 4, 16, Pal.Marble, Flute(Pal.Marble));
                    Book(s, -8, 8, -24, -18, Pal.Parch[1]);
                    float f = (t * 1.2f) % 1f;
                    for (int y = 0; y < 6; y++) s.Set(Mathf.Round(Mathf.Cos(f * Mathf.PI) * 7), -19 - y - Mathf.Round(Mathf.Sin(f * Mathf.PI) * 3), Pal.Parch[0]);
                    break;
                }
                case HubTheme.Sanctum:   // Black altar
                {
                    s.Box3(-15, 0, 30, 8, 13, Pal.Blacks);
                    int top = -13;
                    Book(s, -8, 8, top - 7, top - 2, Pal.Parch[2]);
                    foreach (int cx in new[] { -13, 13 }) { s.Rect(cx, top - 4, cx, top - 1, Pal.RedX[3]); s.Set(cx, top - 5, Mathf.FloorToInt(t * 9) % 2 == 1 ? F(0) : F(2)); }
                    break;
                }
                case HubTheme.Smithy:   // Book on a barrel
                {
                    s.Cyl3(0, 0, 9, 14, Pal.Oak, (x, y, u) => y % 5 == 0 ? Pal.Iron[3] : Ramp(Pal.Oak, 0.15f + u * 0.6f));
                    Book(s, -7, 7, -20, -15, Pal.Parch[1]);
                    s.Rect(17, -46, 17, -12, Pal.Iron[3]); s.Rect(12, -46, 17, -46, Pal.Iron[3]); for (int y = -45; y < -40; y += 2) s.Set(12, y, Pal.Iron[2]);
                    s.Cyl3(12, -36, 3, 5, Pal.Iron); s.Set(12, -39, F(1));
                    break;
                }
                case HubTheme.Chapel:   // High altar
                {
                    s.Box3(-15, 0, 30, 8, 13, Pal.Crim, (x, y) => y > 9 ? Pal.Gold[2] : (x == 14 || x == 15) && y < 9 ? Pal.Gold[1] : Pal.Crim[2]);
                    int top = -13;
                    Book(s, -8, 8, top - 7, top - 2, Mix(Pal.Parch[1], Pal.Gold[0], 0.3f));
                    foreach (int cx in new[] { -13, 13 }) { s.Rect(cx, top - 7, cx, top - 1, Pal.Gold[1]); s.Set(cx, top - 8, Mathf.FloorToInt(t * 9) % 2 == 1 ? F(0) : F(2)); }
                    break;
                }
                case HubTheme.Shore:   // Log on a sea chest
                {
                    s.Box3(-12, 0, 24, 8, 11, Pal.DarkWood, (x, y) => y == 4 ? H("#c8b070") : Pal.DarkWood[2]);
                    Book(s, -7, 7, -18, -13, Pal.Parch[1]);
                    s.Cyl3(15, 0, 3, 6, Pal.Brass); s.Set(15, -7, F(1));
                    break;
                }
                case HubTheme.Volcano:   // The throne, the manual open on its arm
                {
                    s.Box3(-16, 0, 32, 8, 4, Pal.Obsid);
                    s.Box3(-9, -4, 18, 6, 8, Pal.Obsid);
                    for (int y = 0; y < 22; y++) { int w = 8 - Mathf.Max(0, y - 16); for (int x = -w; x <= w; x++) s.Set(x, -18 - y, Mathf.Abs(x) == w ? K : x < 0 ? Pal.Obsid[2] : Pal.Obsid[3]); }
                    foreach (int ax in new[] { -12, 9 }) s.Box3(ax, -4, 4, 5, 12, Pal.Obsid);
                    for (int y = -18; y <= -16; y++) for (int x = 9; x <= 15; x++) s.Set(x, y, Pal.Parch[2]);
                    break;
                }
                case HubTheme.Dojo:   // Shrine shelf, here on a post: a little shrine with the manual before it
                {
                    var wood = new[] { H("#e8d0a0"), H("#c8a878"), H("#a88858"), H("#806040"), H("#584028"), H("#2c2014") };
                    s.Rect(-1, -22, 1, 0, Pal.DarkWood[2]);
                    s.Box3(-14, -22, 28, 3, 2, Pal.DarkWood);
                    s.Box3(-6, -25, 12, 3, 8, wood);
                    for (int x = -7; x <= 7; x++) s.Set(x, -37, Pal.DarkWood[1]);
                    foreach (int zx in new[] { -10, 10 }) for (int y = 0; y < 5; y++) s.Set(zx + y % 2, -21 + y, Pal.Paper[1]);
                    s.Rect(-5, -27, 5, -26, Pal.Parch[1]);
                    break;
                }
                case HubTheme.TechLab:   // Binder on the desk
                {
                    s.Box3(-15, 0, 30, 8, 12, Pal.Beige);
                    for (int y = -19; y <= -13; y++) for (int x = -8; x <= 8; x++) s.Set(x, y, x == 0 ? Pal.Plast[3] : H("#fcfcf4"));
                    Color32[] notes = { H("#ffe060"), H("#ff8ac8"), H("#8af0ff"), H("#ffe060") };
                    for (int i = 0; i < 4; i++) s.Set(9, -18 + i * 2, notes[i]);
                    s.Line(-13, -13, -10, -26, Pal.Plast[3]); s.Line(-10, -26, -4, -24, Pal.Plast[3]);
                    s.Disc(-3, -23, 2.5f, 1.5f, (x, y, d) => s.Set(x, y, Pal.Plast[2]));
                    break;
                }
            }
        }

        public static Color ShrineLight(HubTheme t) => t switch
        {
            HubTheme.Sanctum => new Color(0.9f, 0.25f, 0.2f),
            HubTheme.Salt => new Color(1f, 0.7f, 0.7f),
            HubTheme.Volcano => new Color(0.9f, 0.3f, 0.2f),
            HubTheme.TechLab => new Color(1f, 0.95f, 0.8f),
            _ => new Color(1f, 0.82f, 0.45f),
        };
    }
}
