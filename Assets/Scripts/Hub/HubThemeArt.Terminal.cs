using UnityEngine;
using static Convergence.Hub.HubSketch;

namespace Convergence.Hub
{
    public static partial class HubThemeArt
    {
        public const int TerminalFrames = 12;
        /// <summary>Frames a second at rest; a write settling runs it four times faster.</summary>
        public const float TerminalFps = 3f;
        const float Receipt = 0.65f;   // the mocks' seconds between receipts

        /// <summary>The terminal - the session's receipt roll - in <paramref name="theme"/>. Every theme
        /// shows a receipt being written (a line, a mark, a bell, a dove...) on its own clock.</summary>
        public static Sprite Terminal(HubTheme theme, int frame)
        {
            float t = frame / TerminalFps;
            return Cached($"terminal.{theme}.{frame}", t, s => DrawTerminal(s, theme, t));
        }

        static void Basin(HubSketch s, float t, Color32[] stone, Color32[] water)
        {
            s.Cyl3(0, 0, 4, 14, stone);
            s.Cyl3(0, -14, 13, 5, stone, null, (x, y) =>
            {
                float k = ((y * 3 + x * 0.2f + t * 8f) % 6f + 6f) % 6f;
                return k < 1f ? water[0] : water[2 + (Hash(x, y) > 0.7f ? 1 : 0)];
            });
        }

        static void DrawTerminal(HubSketch s, HubTheme theme, float t)
        {
            int n = Mathf.FloorToInt(t / Receipt);
            float into = (t % Receipt) / Receipt;
            switch (theme)
            {
                case HubTheme.Cloister: Basin(s, t, Pal.Stone, Pal.Teal); break;   // the scrying basin (round 1's pick)
                case HubTheme.Crypt: Basin(s, t, Pal.CryptS, Pal.ColdViolet); break;   // its own version
                case HubTheme.Study:   // Ledger desk
                {
                    s.Box3(-16, 0, 32, 10, 12, Pal.Oak, (x, y) => (x == 6 || x == 25) && y < 11 ? Pal.Oak[3] : y > 8 ? Pal.Oak[2] : x % 15 == 7 && y == 4 ? Pal.Brass[2] : Pal.Oak[2]);
                    int top = -12;
                    for (int y = top - 9; y <= top - 2; y++) for (int x = -12; x <= 10; x++) s.Set(x, y, x == -1 ? Pal.Parch[4] : Pal.Parch[1]);
                    int m = n % 4;
                    for (int i = 0; i < 4; i++) for (int x = 1; x <= 8; x++) if (i < m || (i == m && x < 1 + into * 8)) s.Set(x, top - 8 + i * 2, i == m ? H("#9fd8ff") : Pal.Parch[4]);
                    for (int i = 0; i < 4; i++) for (int x = -11; x <= -3; x++) s.Set(x, top - 8 + i * 2, Pal.Parch[3]);
                    Candle(s, 13, top - 4, t);
                    s.Rect(-15, top - 5, -13, top - 3, K); s.Line(-14, top - 5, -11, top - 11, H("#f4f0e6"));
                    break;
                }
                case HubTheme.Observatory:   // Orrery
                {
                    s.Cyl3(0, 0, 6, 8, Pal.Brass); s.Rect(0, -20, 0, -10, Pal.Brass[3]);
                    s.Disc(0, -22, 3, 3, (x, y, d) => s.Set(x, y, Pal.Yellow[1]));
                    float[] rr = { 7, 11, 15 }, w = { 1.4f, 0.8f, 0.4f }; Color32[] cs = { Pal.Navy[0], Red[2], Pal.Teal[2] };
                    for (int i = 0; i < 3; i++)
                    {
                        float st = i == 2 ? n * 0.5f : t * w[i];
                        float x = Mathf.Cos(st) * rr[i], y = -22 + Mathf.Sin(st) * rr[i] * 0.4f;
                        s.Line(0, -22, x, y, (xx, yy, f) => { s.Set(xx, yy, Pal.Brass[3], 0.6f); return null; });
                        var c = i == 2 && into < 0.15f ? Pal.White : cs[i];
                        s.Disc(x, y, 1.6f, 1.6f, (xx, yy, d) => s.Set(xx, yy, c));
                    }
                    break;
                }
                case HubTheme.Atrium:   // Dovecote
                {
                    s.Rect(0, -20, 1, 0, Pal.Oak[3]);
                    s.Box3(-9, -20, 18, 6, 12, Pal.Wicker);
                    for (int i = 0; i < 3; i++) s.Disc(-5 + i * 5, -26, 1.2f, 1.5f, (x, y, d) => s.Set(x, y, K));
                    for (int y = 0; y < 5; y++) s.Rect(-11 + y, -33 - y, 11 - y, -33 - y, Pal.Terra[2 + y % 2]);
                    float u = (t % 1.6f) / 1.6f;
                    if (u < 0.8f)
                    {
                        float a = u / 0.8f * 6.3f, x = Mathf.Sin(a) * 18, y = -30 - Mathf.Sin(a * 0.5f) * 12;
                        int flap = Mathf.FloorToInt(t * 10) % 2;
                        s.Set(x, y, Pal.White); s.Set(x - 1, y - flap, H("#e8e8e8")); s.Set(x + 1, y - flap, H("#e8e8e8"));
                    }
                    break;
                }
                case HubTheme.Salt:   // Salt hourglass
                {
                    s.Box3(-9, 0, 18, 5, 3, Pal.SaltW); s.Box3(-9, -30, 18, 5, 3, Pal.SaltW);
                    s.Rect(-8, -29, -7, -3, Pal.SaltW[2]); s.Rect(7, -29, 8, -3, Pal.SaltW[2]);
                    float run = (t % 2.6f) / 2.6f;
                    for (int y = 0; y < 26; y++)
                    {
                        int w = Mathf.RoundToInt(Mathf.Abs(y - 13) * 0.4f + 1);
                        for (int x = -w; x <= w; x++)
                        {
                            bool fill = y < 13 ? y > 13 - (1 - run) * 10 : y > 25 - run * 8;
                            s.Set(x, -29 + y, Mathf.Abs(x) == w ? H("#c8e0e8") : fill ? Pal.SaltW[0] : H("#2a3038"));
                        }
                    }
                    s.Rect(0, -16, 0, -8, Pal.SaltW[0]);
                    for (int i = 0; i < n % 6; i++) s.Set(-7, -26 + i * 3, K);
                    break;
                }
                case HubTheme.Terrace:   // Wind chimes
                {
                    s.Box3(-13, 0, 3, 2, 30, Pal.Marble); s.Box3(10, 0, 3, 2, 30, Pal.Marble); s.Rect(-13, -32, 12, -31, Pal.Marble[1]);
                    for (int i = 0; i < 5; i++)
                    {
                        bool hit = i == n % 5;
                        float sw = hit ? Mathf.Sin(into * Receipt * 20) * 2 * (1 - into) : Mathf.Sin(t * 2 + i) * 0.6f;
                        int x0 = -8 + i * 4, len = 8 + i % 3 * 3;
                        s.Line(x0, -30, x0 + sw, -30 + len, hit ? Pal.Gold[0] : Pal.Gold[2]);
                    }
                    break;
                }
                case HubTheme.Sanctum:   // Record crystal
                {
                    s.Box3(-7, 0, 14, 5, 6, Pal.Blacks); s.Crystal(0, -6, 24, 5, Pal.RedX);
                    int m = n % 6;
                    for (int i = 0; i <= m; i++) s.Rect(-2, -10 - i * 3, 2, -10 - i * 3, i == m ? Pal.White : Pal.RedX[0]);
                    break;
                }
                case HubTheme.Smithy:   // Tally board
                {
                    s.Box3(-14, 0, 28, 3, 24, Pal.DarkWood, (x, y) => x < 2 || x > 25 || y < 2 || y > 21 ? Pal.DarkWood[1] : H("#1a1818"));
                    int m = n % 13;
                    for (int i = 0; i < m; i++)
                    {
                        int g = i / 5, k = i % 5;
                        if (k < 4) s.Rect(-10 + g * 9 + k * 2, -20, -10 + g * 9 + k * 2, -12, H("#e8e8e0"));
                        else s.Line(-11 + g * 9, -13, -3 + g * 9, -19, H("#e8e8e0"));
                    }
                    break;
                }
                case HubTheme.Chapel:   // Illuminated book
                {
                    s.Rect(-1, -14, 1, 0, Pal.Gold[2]); s.Rect(-6, 0, 6, 0, Pal.Gold[2]);
                    for (int y = -24; y <= -14; y++) for (int x = -11; x <= 11; x++) s.Set(x, y, x == 0 ? Pal.Parch[4] : Pal.Parch[1]);
                    int m = n % 5;
                    for (int i = 0; i <= m; i++) s.Rect(2, -22 + i * 2, 9, -22 + i * 2, i == m ? Pal.Gold[0] : Pal.Gold[2]);
                    s.Rect(-9, -22, -3, -17, Pal.Crim[2]);
                    break;
                }
                case HubTheme.Shore:   // Ship's bell
                {
                    s.Box3(-2, 0, 4, 2, 26, Pal.Drift); s.Rect(-2, -28, 10, -27, Pal.Drift[2]);
                    float sw = Mathf.Sin(into * 14) * 3 * (1 - into);
                    for (int y = 0; y < 8; y++) { float w = 2 + y * 0.4f; for (int x = -Mathf.RoundToInt(w); x <= Mathf.RoundToInt(w); x++) s.Set(7 + x + sw * y / 8f, -26 + y, x < 0 ? Pal.Brass[1] : Pal.Brass[2]); }
                    break;
                }
                case HubTheme.Volcano:   // Black console
                {
                    s.Box3(-14, 0, 28, 8, 14, Pal.Obsid);
                    for (int y = 0; y < 6; y++) for (int x = 0; x < 28 - y; x++) s.Set(-14 + x + y, -22 - y, y == 0 ? Pal.Obsid[0] : Pal.Obsid[3]);
                    int m = n % 4;
                    for (int i = 0; i <= m; i++) for (int x = 0; x < 16; x++) if ((x + i) % 3 != 0) s.Set(-8 + x + (i > 1 ? 2 : 0), -8 - i * 2, i == m ? H("#ff8a7a") : Pal.RedX[3]);
                    break;
                }
                case HubTheme.Dojo:   // Scroll and brush
                {
                    s.Box3(-14, 0, 28, 8, 5, Pal.DarkWood);
                    for (int y = 0; y < 8; y++) for (int x = -10; x <= 10; x++) s.Set(x, -6 - y, Pal.Paper[1]);
                    int m = n % 6;
                    for (int i = 0; i <= m; i++) for (int y = 0; y < 6; y++) if (Hash(i, y) > 0.3f) s.Set(8 - i * 3, -7 - y, i == m && y > into * 6.5f ? Pal.Paper[1] : H("#141010"));
                    s.Line(12, -8, 15, -16, Pal.DarkWood[1]);
                    break;
                }
                case HubTheme.TechLab:   // CRT terminal
                {
                    s.Box3(-16, 0, 32, 8, 10, Pal.Beige);
                    s.Box3(-10, -10, 20, 4, 18, Pal.Beige);
                    for (int y = 2; y < 16; y++) for (int x = 2; x < 18; x++)
                    {
                        int sx = x - 2, sy = y - 2, row = (13 - sy) / 2;
                        Color32 c = Pal.Crt[5];
                        if ((13 - sy) % 2 == 0)
                        {
                            int len = 3 + Mathf.FloorToInt(Hash(row + n, 3) * 12);
                            if (sx < len && (row < 5 || sx < into * len)) c = row == 5 ? Pal.Crt[0] : Pal.Crt[2];
                        }
                        s.Set(-10 + x, -10 - y, c);
                    }
                    s.Box3(-9, 2, 18, 3, 2, Pal.Beige);
                    break;
                }
            }
        }

        public static Color TerminalLight(HubTheme t) => t switch
        {
            HubTheme.Cloister => new Color(0.45f, 0.85f, 0.8f),
            HubTheme.Crypt => new Color(0.65f, 0.5f, 0.95f),
            HubTheme.Sanctum or HubTheme.Volcano => new Color(0.9f, 0.25f, 0.2f),
            HubTheme.TechLab => new Color(0.4f, 1f, 0.45f),
            HubTheme.Observatory => new Color(1f, 0.9f, 0.5f),
            _ => new Color(1f, 0.85f, 0.5f),
        };

        static void Candle(HubSketch s, float x, float y, float t)
        {
            s.Rect(x, y - 4, x, y, Pal.Parch[1]);
            s.Set(x, y - 5, Mathf.FloorToInt(t * 9) % 2 == 1 ? F(0) : F(2));
            s.Set(x, y - 6, F(1), 0.7f);
        }
    }
}
