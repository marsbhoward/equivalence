using UnityEngine;
using static Convergence.Hub.HubSketch;

namespace Convergence.Hub
{
    public static partial class HubThemeArt
    {
        /// <summary>The couch in <paramref name="theme"/>, <paramref name="w"/> texels wide (it grows with
        /// the seats). Pivot at the front of the seat, in the middle.</summary>
        public static Sprite Couch(HubTheme theme, int w)
        {
            if (theme == HubTheme.Study) return HubArt.Settle(w);   // the wooden settle, re-paired into the study
            return Cached($"couch.{theme}.{w}", 0f, s => DrawCouch(s, theme, w));
        }

        static void Cushions(HubSketch s, int x0, int w, int y, Color32 top, Color32 front)
        {
            for (int x = x0 + 2; x < x0 + w - 2; x++) if ((x - x0 - 2) % 13 < 11) { s.Set(x, y - 1, top); s.Set(x, y, front); }
        }

        static void DrawCouch(HubSketch s, HubTheme theme, int w)
        {
            int x0 = -w / 2;
            switch (theme)
            {
                case HubTheme.Cloister:   // Stone bench and furs (round 1's pick)
                case HubTheme.Crypt:      // ...and the crypt's own, dark with a black fur
                {
                    bool crypt = theme == HubTheme.Crypt;
                    var stone = crypt ? Pal.CryptS : Pal.Stone;
                    Color32 furA = crypt ? H("#3a3440") : H("#c8b8a0"), furB = crypt ? H("#1e1a22") : H("#8a7a64");
                    s.Box3(x0, 0, w, 9, 8, stone);
                    int f0 = x0 + w / 7, f1 = x0 + w * 7 / 10;
                    for (int x = f0; x < f1; x++) for (int y = 0; y < 9 + (x > f0 + 4 && x < f1 - 6 ? 3 : 0); y++) { if (y > 11) continue; s.Set(x, -17 + y, Hash(x, y) > 0.6f ? furA : furB); }
                    s.Box3(x0 + w - 10, -9, 8, 3, 4, crypt ? Pal.Blacks : Pal.Velvet);
                    break;
                }
                case HubTheme.Observatory:   // Viewing bench
                    s.Box3(x0, 0, w, 8, 6, Pal.Navy, (x, y) => y < 2 ? Pal.Brass[3] : Pal.Navy[2]);
                    for (int i = 0; i < 14; i++) s.Rect(x0 + i, -14 - i * 0.7f, x0 + i, -8, i == 0 ? K : Pal.Navy[1]);
                    break;
                case HubTheme.Atrium:   // Wicker bench
                    s.Box3(x0, -8, w, 3, 14, Pal.Wicker, (x, y) => (x + y) % 3 == 0 ? Pal.Wicker[3] : Pal.Wicker[1]);
                    s.Box3(x0, 0, w, 8, 8, Pal.Wicker, (x, y) => (x + y) % 3 == 0 ? Pal.Wicker[3] : Pal.Wicker[2]);
                    for (int x = x0 + 2; x < x0 + w - 2; x++) if ((x - x0 - 2) % 13 < 11) s.Set(x, -9, Pal.Leaf[2]);
                    break;
                case HubTheme.Salt:
                    s.Box3(x0, 0, w, 9, 8, Pal.SaltW, (x, y) => x % 11 == 0 ? Pal.SaltW[3] : Pal.SaltW[1]);
                    Cushions(s, x0, w, -10, Pal.Pink[1], Pal.Pink[2]);
                    break;
                case HubTheme.Terrace:
                    s.Box3(x0, 0, w, 9, 8, Pal.Marble, (x, y) => x % 11 == 0 ? Pal.Marble[3] : Pal.Marble[1]);
                    Cushions(s, x0, w, -10, Pal.SkyB[1], Pal.SkyB[2]);
                    break;
                case HubTheme.Sanctum:
                    s.Box3(x0, 0, w, 9, 8, Pal.Blacks);
                    Cushions(s, x0, w, -10, Pal.Crim[2], Pal.Crim[3]);
                    break;
                case HubTheme.Smithy:   // Plank on barrels
                    foreach (int bx in new[] { x0 + 7, x0 + w - 7 }) s.Cyl3(bx, 0, 6, 9, Pal.Oak, (x, y, u) => y % 4 == 0 ? Pal.Iron[3] : Ramp(Pal.Oak, 0.15f + u * 0.6f));
                    s.Box3(x0 - 2, -11, w + 4, 6, 3, Pal.Oak);
                    for (int y = 0; y < 8; y++) for (int x = -4; x < 6; x++) s.Set(x, -17 + y, H("#6a4a2a"));
                    break;
                case HubTheme.Chapel:   // Pew
                    s.Box3(x0, -8, w, 4, 18, Pal.DarkWood, (x, y) => x % 11 == 0 ? Pal.DarkWood[3] : Pal.DarkWood[2]);
                    s.Box3(x0, 0, w, 8, 8, Pal.DarkWood);
                    for (int x = x0 + 2; x < x0 + w - 2; x++) s.Set(x, -9, Pal.Crim[2]);
                    break;
                case HubTheme.Shore:   // Driftwood bench
                    foreach (int bx in new[] { x0 + 4, x0 + w - 8 }) s.Box3(bx, 0, 4, 3, 8, Pal.Drift);
                    s.Box3(x0, -8, w, 8, 2, Pal.Drift);
                    for (int x = x0 + 4; x < x0 + w - 4; x++) s.Set(x, -11, Mathf.FloorToInt(x / 3f) % 2 != 0 ? Pal.Sea[2] : Pal.White);
                    break;
                case HubTheme.Volcano:   // Obsidian ledge
                    s.Box3(x0, 0, w, 9, 7, Pal.Obsid);
                    for (int x = x0 + 2; x < x0 + w - 2; x++) s.Set(x, -9, Pal.Crim[2]);
                    break;
                case HubTheme.Dojo:   // Cushions on a platform
                {
                    var indigo = new[] { H("#8a9ad8"), H("#5a6ab0"), H("#3a4a88"), H("#28346a"), H("#1a2246"), H("#0c1024") };
                    s.Box3(x0, 0, w, 10, 4, Pal.DarkWood);
                    int n = Mathf.Max(2, w / 14);
                    for (int i = 0; i < n; i++) s.Box3(x0 + 3 + i * (w - 6) / n, -3, Mathf.Min(10, (w - 6) / n - 2), 6, 2, indigo);
                    break;
                }
                case HubTheme.TechLab:   // Bean bags
                {
                    var pink = new[] { H("#ff8ac8"), H("#e05aa0"), H("#b03a7a"), H("#802456"), H("#501434"), H("#28081a") };
                    var blue = new[] { H("#8af0ff"), H("#4ac0e0"), H("#2a90b0"), H("#1a6080"), H("#0e3a50"), H("#061c28") };
                    int n = Mathf.Max(2, w / 20);
                    for (int i = 0; i < n; i++)
                    {
                        var c = i % 2 == 0 ? pink : blue; float bx = x0 + 9 + i * (w - 18) / Mathf.Max(1, n - 1);
                        s.Disc(bx, -5, 9, 6, (x, y, d) => s.Set(x, y, d > 0.9f ? K : Ramp(c, 0.15f + (y + 11) / 12f * 0.6f)));
                    }
                    s.Box3(-4, 2, 8, 3, 3, Pal.Plast);
                    break;
                }
            }
        }

        // ---- the crate ----

        /// <summary>The crate in <paramref name="theme"/>; <paramref name="parcel"/> while an unopened theme
        /// waits on it (the user's pick: the parcel, glinting). Pivot at its front's foot.</summary>
        public static Sprite Crate(HubTheme theme, bool parcel = false, int frame = 0)
            => Cached($"crate.{theme}.{parcel}.{frame}", frame / 6f, s => { DrawCrate(s, theme); if (parcel) Parcel(s, frame); });

        public const int CrateFrames = 6;

        static void Parcel(HubSketch s, int frame)
        {
            float top = s.TopOfColumn(0) - 1;
            s.Box3(-5, top, 10, 4, 6, Pal.Velvet, (x, y) => x == 5 || y == 3 ? Pal.Gold[1] : Pal.Velvet[2], (x, y) => x == 5 ? Pal.Gold[1] : Pal.Velvet[1]);
            if (frame == 0) { s.Set(4, top - 10, Pal.White); for (int i = 1; i <= 2; i++) { s.Set(4 + i, top - 10, Pal.Gold[1]); s.Set(4 - i, top - 10, Pal.Gold[1]); s.Set(4, top - 10 + i, Pal.Gold[1]); s.Set(4, top - 10 - i, Pal.Gold[1]); } }
        }

        static void DrawCrate(HubSketch s, HubTheme theme)
        {
            switch (theme)
            {
                case HubTheme.Cloister:   // Iron-bound chest (round 2's pick)
                    s.Box3(-12, 0, 24, 8, 13, Pal.Oak, (x, y) => y == 3 || y == 10 || x == 2 || x == 21 ? Pal.Iron[3] : (x == 11 || x == 12) && y > 5 && y < 10 ? Pal.Iron[1] : Pal.Oak[2],
                           (x, y) => x == 2 || x == 21 ? Pal.Iron[2] : Pal.Oak[1]);
                    break;
                case HubTheme.Study:   // Travelling trunk
                    s.Box3(-13, 0, 26, 6, 12, Pal.DarkWood, (x, y) => (x < 3 || x > 22) && (y < 3 || y > 8) ? Pal.Brass[2] : y == 6 ? Pal.Brass[3] : Pal.DarkWood[2]);
                    for (int y = 0; y < 6; y++) { int ww = Mathf.RoundToInt(13 * Mathf.Sqrt(1 - Mathf.Pow(y / 6f, 2))); for (int x = -ww; x <= ww; x++) s.Set(x, -18 - y, Mathf.Abs(x) == ww || y == 5 ? K : x % 8 == 0 ? Pal.Brass[2] : Pal.DarkWood[1]); }
                    s.Rect(-1, -13, 1, -10, Pal.Brass[1]);
                    break;
                case HubTheme.Observatory:   // Instrument case
                    s.Box3(-13, 0, 26, 8, 13, Pal.Navy, (x, y) => (x < 3 || x > 22) && (y < 3 || y > 9) ? Pal.Brass[2] : (x == 8 || x == 17) && y > 9 ? Pal.Brass[1] : Pal.Navy[2]);
                    break;
                case HubTheme.Crypt:   // Sarcophagus chest
                    s.Box3(-14, 0, 28, 9, 12, Pal.CryptS, (x, y) => (x >= 11 && x <= 16 && y >= 3 && y <= 8) ? ((x == 12 || x == 15) && y == 6 ? K : Pal.Bone[1]) : y == 10 ? Pal.CryptS[1] : Pal.CryptS[2]);
                    s.Box3(-15, -12, 30, 9, 2, Pal.CryptS, null, null, Pal.CryptS[0]);
                    break;
                case HubTheme.Atrium:   // Wicker hamper
                    s.Box3(-12, 0, 24, 9, 15, Pal.Wicker, (x, y) => x == 6 || x == 17 ? Pal.DarkWood[2] : (x + y) % 3 == 0 ? Pal.Wicker[3] : Pal.Wicker[1],
                           (x, y) => x == 6 || x == 17 ? Pal.DarkWood[2] : (x + y) % 3 == 0 ? Pal.Wicker[3] : Pal.Wicker[0]);
                    break;
                case HubTheme.Salt:   // Salt-crusted chest
                    s.Box3(-12, 0, 24, 9, 14, Pal.DarkWood, (x, y) => y > 10 - Hash(x, 3) * 3 || x < 2 || x > 21 ? Pal.SaltW[1] : Pal.DarkWood[2], (x, y) => Hash(x, y) > 0.3f ? Pal.SaltW[0] : Pal.DarkWood[1]);
                    break;
                case HubTheme.Terrace:   // Gilt coffer
                    s.Box3(-12, 0, 24, 8, 12, Pal.Marble, (x, y) => y == 2 || y == 9 ? Pal.Gold[2] : Pal.Marble[1], (x, y) => x == 11 || x == 12 ? Pal.Gold[1] : Pal.Marble[0]);
                    s.Line(-6, -21, 4, -25, H("#f4f4f8"));
                    break;
                case HubTheme.Sanctum:   // Caged chest
                    s.Box3(-12, 0, 24, 8, 13, Pal.Blacks, (x, y) => x % 5 == 0 ? Pal.Iron[3] : (x == 11 || x == 12) && y > 4 && y < 8 ? Pal.RedX[1] : Pal.Blacks[2], (x, y) => x % 5 == 0 ? Pal.Iron[2] : Pal.Blacks[1]);
                    break;
                case HubTheme.Smithy:   // Ingot crate
                    s.Box3(-12, 0, 24, 8, 12, Pal.Oak, (x, y) => y % 4 == 0 ? Pal.Oak[3] : Pal.Oak[2]);
                    for (int i = 0; i < 3; i++) s.Box3(-9 + i * 6, -20, 6, 3, 3, Pal.Iron);
                    s.Box3(-6, -23, 6, 3, 3, Pal.Iron);
                    break;
                case HubTheme.Chapel:   // Reliquary chest
                    s.Box3(-11, -2, 22, 7, 11, Pal.Gold, (x, y) => x > 2 && x < 19 && y > 2 && y < 9 ? Pal.Crim[2] : Pal.Gold[2]);
                    foreach (int fx in new[] { -10, 9 }) s.Rect(fx, -2, fx + 1, 0, Pal.Gold[3]);
                    break;
                case HubTheme.Shore:   // Sea chest
                    s.Box3(-12, 0, 24, 8, 13, Pal.Drift, (x, y) => y < 2 && Hash(x, 3) > 0.4f ? H("#e8e4dc") : y == 8 ? Pal.DarkWood[2] : Pal.Drift[2]);
                    foreach (int sx in new[] { -14, 13 }) for (int y = 0; y < 4; y++) s.Set(sx, -6 - y, H("#c8b070"));
                    break;
                case HubTheme.Volcano:   // Black strongbox
                    s.Box3(-12, 0, 24, 8, 13, Pal.Obsid, (x, y) => (x == 11 || x == 12) && y > 5 && y < 9 ? Pal.RedX[1] : y == 2 || y == 10 ? Pal.Plast[4] : Pal.Obsid[2]);
                    break;
                case HubTheme.Dojo:   // Lacquer chest
                    s.Box3(-12, 0, 24, 7, 16, Pal.BlackLacq, (x, y) => y % 5 == 0 ? H("#000000") : (x == 6 || x == 17) && y % 5 == 2 ? Pal.Iron[1] : H("#1a1414"));
                    break;
                case HubTheme.TechLab:   // Box of floppies
                {
                    s.Box3(-11, 0, 22, 9, 12, Pal.Beige);
                    Color32[] fl = { H("#2a2a2a"), H("#ff4a4a"), H("#4a8aff"), H("#ffd04a"), H("#2a2a2a"), H("#4ad06a"), H("#2a2a2a") };
                    for (int i = 0; i < 7; i++) for (int y = 0; y < 6; y++) for (int x = 0; x < 3; x++) s.Set(-9 + i * 3 + x, -13 - y - (i % 2), y == 1 && x == 1 ? Pal.Plast[1] : fl[i]);
                    break;
                }
            }
        }
    }
}
