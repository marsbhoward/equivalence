using UnityEngine;
using static Convergence.Hub.HubSketch;

namespace Convergence.Hub
{
    public static partial class HubThemeArt
    {
        public const int BoothFrames = 8;
        public const float BoothFps = 4f;

        /// <summary>The photo booth in <paramref name="theme"/>: <paramref name="flash"/> the moment a
        /// portrait is taken (the glass goes white); <paramref name="near"/> while the player stands at
        /// it (a veil lifts, a scanner sweeps, the easel is painted).</summary>
        public static Sprite Booth(HubTheme theme, int frame, bool flash, bool near)
        {
            float t = frame / BoothFps;
            return Cached($"booth.{theme}.{frame}.{flash}.{near}", t, s => DrawBooth(s, theme, t, frame, flash, near));
        }

        /// <summary>An oval glass in a frame - every mirror's shape. <paramref name="rim"/> paints the frame.</summary>
        static void Oval(HubSketch s, float cy, float rx, float ry, System.Func<int, int, Color32> rim, Color32 dark, Color32 light, float t, bool flash, float rimAt = 0.86f)
        {
            s.Disc(0, cy, rx, ry, (x, y, d) =>
            {
                if (d > rimAt) { s.Set(x, y, rim(x, y)); return; }
                if (d > rimAt - 0.06f) { s.Set(x, y, K); return; }
                s.Set(x, y, flash ? Pal.White : Mix(dark, light, C01(1f - (x + rx) / (2f * rx) + Mathf.Sin((y + x) * 0.2f + t * 2f) * 0.1f)));
            });
        }

        static void DrawBooth(HubSketch s, HubTheme theme, float t, int frame, bool flash, bool near)
        {
            switch (theme)
            {
                case HubTheme.Cloister:   // Painter's easel (round 2's pick)
                {
                    s.Line(-8, 0, -2, -34, Pal.Oak[2]); s.Line(8, 0, 2, -34, Pal.Oak[2]); s.Line(0, -30, 4, 2, Pal.Oak[3]);
                    s.Rect(-9, -30, 9, -12, flash ? Pal.White : Pal.Parch[1]);
                    // The portrait painted a stroke at a time while someone stands there.
                    int strokes = near ? Mathf.Min(12, 3 + frame * 2) : 3;
                    Color32[] paint = { Pal.Velvet[2], Pal.Oak[2], H("#d9a07a") };
                    for (int i = 0; i < strokes && !flash; i++) { s.Set(-6 + (i % 6) * 2, -26 + (i / 6) * 5, paint[i % 3]); s.Set(-5 + (i % 6) * 2, -26 + (i / 6) * 5, paint[i % 3]); }
                    s.Cyl3(20, 2, 4, 7, Pal.Oak);
                    s.Box3(-26, 2, 9, 4, 4, Pal.DarkWood);
                    break;
                }
                case HubTheme.Study:   // Camera obscura
                {
                    foreach (int lx in new[] { -7, 0, 7 }) s.Line(0, -14, lx, lx == 0 ? 3 : 0, Pal.Oak[3]);
                    s.Box3(-8, -14, 12, 4, 10, Pal.DarkWood);
                    for (int i = 0; i < 6; i++) s.Rect(4 + i, -22 + i * 0.3f, 4 + i, -16 - i * 0.3f, i % 2 == 1 ? H("#3a2a20") : H("#5a4030"));
                    s.Cyl3(12, -17, 2, 2, Pal.Brass);
                    if (flash) s.Disc(12, -20, 2, 2, (x, y, d) => s.Set(x, y, Pal.White));
                    s.Box3(-30, 2, 8, 3, 7, Pal.Oak);
                    s.Rect(-31, -9, -21, -6, H("#1a1820"));
                    break;
                }
                case HubTheme.Observatory:   // Telescope
                {
                    foreach (int lx in new[] { -8, 0, 8 }) s.Line(0, -14, lx, lx == 0 ? 3 : 0, Pal.DarkWood[2]);
                    s.Line(-8, -14, 8, -30, (x, y, f) => { for (int k = -1; k <= 1; k++) s.Set(x, y + k, k == -1 ? Pal.Brass[1] : k == 1 ? Pal.Navy[3] : f > 0.7f ? Pal.Brass[2] : Pal.Navy[2]); return null; });
                    s.Disc(9, -31, 2.5f, 2.5f, (x, y, d) => s.Set(x, y, d > 0.7f ? Pal.Brass[1] : flash ? Pal.White : H("#7ab0e0")));
                    break;
                }
                case HubTheme.Crypt:   // Veiled mirror: the veil slides off as you step up to it
                {
                    Oval(s, -22, 10, 18, (x, y) => Pal.Iron[2], H("#30343c"), H("#7a8494"), t, flash);
                    float off = near ? 1f : 0f;
                    s.Disc(0, -22, 12, 21, (x, y, d) =>
                    {
                        float top = -42 + off * 30;
                        if (y < top) return;
                        s.Set(x, y, Mathf.Sin(x * 1.3f) > 0.5f ? H("#26222c") : H("#141218"));
                    });
                    foreach (int fx in new[] { -6, 6 }) s.Line(fx * 0.4f, -5, fx, 0, Pal.Iron[3]);
                    break;
                }
                case HubTheme.Atrium:   // Arbour mirror
                {
                    Oval(s, -21, 8, 15, (x, y) => Pal.Verd[2], H("#4a6a5c"), H("#b0d8c8"), t, flash, 0.85f);
                    s.Ring(0, -21, 11, 14, 1.5f, (x, y, d, a) => { if (Mathf.Sin(a) > 0.6f) return; s.Set(x, y, Hash(x, y) > 0.85f ? Pal.Velvet[1] : Pal.Leaf[1 + (Hash(x, y, 2) > 0.5f ? 1 : 0)]); });
                    s.Rect(-13, -4, -12, 0, Pal.Wicker[3]); s.Rect(12, -4, 13, 0, Pal.Wicker[3]);
                    break;
                }
                case HubTheme.Salt:   // Salt mirror
                    s.Box3(-12, 0, 24, 5, 40, Pal.SaltW, (x, y) => (x > 3 && x < 20 && y > 4 && y < 36)
                        ? (flash ? Pal.White : Mix(Pal.Pink[2], Pal.Pink[0], 1f - x / 24f))
                        : (x + y / 5 * 3) % 6 == 0 || y % 5 == 0 ? Pal.SaltW[3] : Pal.SaltW[1]);
                    break;
                case HubTheme.Terrace:   // Mirror on a column
                    s.Box3(-5, 0, 10, 3, 16, Pal.Marble, Flute(Pal.Marble));
                    Oval(s, -28, 10, 10, (x, y) => Hash(x, y) > 0.5f ? Pal.Marble[0] : Pal.Marble[2], Pal.SkyB[3], Pal.SkyB[0], t, flash);
                    break;
                case HubTheme.Sanctum:   // Obsidian mirror
                    Oval(s, -22, 10, 18, (x, y) => Mathf.Abs(Noise(x * 0.5f, y * 0.5f) - 0.5f) < 0.06f ? Pal.RedX[2] : Pal.Blacks[2], H("#08060a"), H("#3a3040"), t, flash);
                    foreach (int fx in new[] { -6, 6 }) s.Line(fx * 0.4f, -5, fx, 0, Pal.Blacks[1]);
                    break;
                case HubTheme.Smithy:   // Polished shield
                {
                    s.Rect(-1, -14, 1, 0, Pal.Iron[3]); s.Rect(-8, 0, 8, 0, Pal.Iron[3]);
                    s.Disc(0, -28, 12, 12, (x, y, d) => s.Set(x, y, d > 0.88f ? Pal.Iron[3] : Mathf.Abs(d - 0.45f) < 0.05f ? Pal.Iron[2] : flash ? Pal.White : Mix(Pal.Iron[2], Pal.Iron[0], 1f - (x + 12) / 24f)));
                    s.Disc(0, -28, 2, 2, (x, y, d) => s.Set(x, y, Pal.Brass[1]));
                    break;
                }
                case HubTheme.Chapel:   // Icon mirror
                {
                    for (int y = -42; y <= -4; y++) for (int x = -10; x <= 10; x++)
                    {
                        int ax = Mathf.Abs(x); float top = -42 + Mathf.Pow(ax / 10f, 2) * 10f;
                        if (y < top) continue;
                        bool e = ax >= 9 || y < top + 2 || y > -6;
                        s.Set(x, y, e ? Pal.Gold[ax % 2 == 1 ? 1 : 2] : flash ? Pal.White : Mix(H("#4a2a2a"), H("#c8a8a0"), 1f - (x + 10) / 20f));
                    }
                    s.Rect(-11, -3, 11, 0, Pal.Crim[3]);
                    break;
                }
                case HubTheme.Shore:   // Shell mirror
                    Oval(s, -22, 9, 16, (x, y) => Hash(x >> 1, y >> 1) > 0.5f ? Pal.Coral[1] : H("#fff4ec"), Pal.Sea[3], Pal.Sea[0], t, flash, 0.84f);
                    foreach (int fx in new[] { -6, 6 }) s.Line(fx * 0.4f, -7, fx, 0, Pal.Drift[2]);
                    break;
                case HubTheme.Volcano:   // Black glass panel
                    s.Box3(-9, 0, 18, 3, 40, Pal.Obsid, (x, y) => x < 2 || x > 15 || y < 2 || y > 37 ? Pal.Plast[3] : flash ? Pal.White : Mix(H("#0a080e"), H("#4a1a1a"), 1f - x / 18f));
                    break;
                case HubTheme.Dojo:   // Bronze mirror on a lacquer stand
                {
                    s.Rect(-1, -18, 1, 0, Pal.Lacq[5]); s.Rect(-7, 0, 7, 0, Pal.Lacq[5]);
                    s.Disc(0, -28, 10, 10, (x, y, d) => s.Set(x, y, d > 0.88f ? Pal.Brass[3] : flash ? Pal.White : Mix(H("#8a6a3a"), H("#f0d8a0"), 1f - (x + 10) / 20f)));
                    for (int y = 0; y < 5; y++) s.Set(y % 2, -17 + y, Pal.Lacq[1]);
                    break;
                }
                case HubTheme.TechLab:   // Flatbed scanner: the light sweeps while someone stands at it
                {
                    s.Rect(-10, -10, -9, 0, Pal.Plast[3]); s.Rect(9, -10, 10, 0, Pal.Plast[3]);
                    s.Box3(-14, -10, 28, 12, 4, Pal.Beige, null, (x, y) => x > 2 && x < 25 && y > 1 && y < 10 ? (flash ? Pal.White : H("#304050")) : Pal.Beige[1]);
                    s.Box3(-14, -26, 28, 2, 12, Pal.Beige);
                    if (near || flash) { int sx = (frame * 3) % 24; for (int y = 0; y < 8; y++) s.Set(-11 + sx, -16 - y, H("#e8fff0")); }
                    break;
                }
            }
        }
    }
}
