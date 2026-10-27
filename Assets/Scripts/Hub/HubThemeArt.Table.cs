using UnityEngine;
using static Convergence.Hub.HubSketch;

namespace Convergence.Hub
{
    public static partial class HubThemeArt
    {
        /// <summary>Where the board's sketch lies on a theme's table: its centre (texels above the foot)
        /// and size (texels), and the ink it is drawn in.</summary>
        public struct TableTop { public Vector2 Centre, Size; public Color Ink; }

        /// <summary>The mastery table in <paramref name="theme"/>, <paramref name="w"/> wide and
        /// <paramref name="d"/> deep on top (texels). The real board's sketch is laid on it by GridTable
        /// at <see cref="Top"/> - each theme draws the furniture and picks the ink.</summary>
        public static Sprite Table(HubTheme theme, int w, int d)
        {
            if (theme == HubTheme.Cloister) return HubArt.MapTable(w, d);
            return Cached($"table.{theme}.{w}.{d}", 0f, s => DrawTable(s, theme, w, d));
        }

        static bool RoundTable(HubTheme t) => t == HubTheme.Observatory || t == HubTheme.Crypt || t == HubTheme.Terrace;

        public static TableTop Top(HubTheme theme, int w, int d)
        {
            var top = new TableTop { Centre = new Vector2(0f, 14f + d * 0.5f), Size = new Vector2(w - 10, d - 5), Ink = new Color(0.24f, 0.19f, 0.13f) };
            if (RoundTable(theme)) { float rx = w / 2f, ry = Mathf.Max(1f, rx * 0.38f); top.Centre = new Vector2(0f, 13f + ry); top.Size = new Vector2(rx * 1.3f, ry * 1.3f); }
            if (theme == HubTheme.Study) top.Centre = new Vector2(0f, 14f + d * 0.6f);
            if (theme == HubTheme.TechLab) { top.Centre = new Vector2(0f, 16f); top.Size = new Vector2(w - 12, 22); }
            top.Ink = theme switch
            {
                HubTheme.Observatory => new Color(0.91f, 0.93f, 1f),
                HubTheme.Crypt => new Color(1f, 0.55f, 0.2f),
                HubTheme.Atrium => new Color(0.36f, 0.6f, 0.24f),
                HubTheme.Salt => new Color(0.88f, 0.6f, 0.6f),
                HubTheme.Terrace => new Color(0.95f, 0.76f, 0.35f),
                HubTheme.Sanctum => new Color(0.85f, 0.15f, 0.12f),
                HubTheme.Smithy => new Color(0.86f, 0.86f, 0.82f),
                HubTheme.Chapel => new Color(0.95f, 0.76f, 0.35f),
                HubTheme.Shore => new Color(0.06f, 0.2f, 0.32f),
                HubTheme.Volcano => new Color(1f, 0.55f, 0.15f),
                HubTheme.Dojo => new Color(0.08f, 0.06f, 0.06f),
                HubTheme.TechLab => new Color(0.3f, 0.82f, 0.25f),
                _ => top.Ink,
            };
            return top;
        }

        static void Legs(HubSketch s, int w, Color32[] r, int width = 3)
        {
            int x0 = -w / 2;
            s.Box3(x0 + 2, 0, width, 1, 11, r);
            s.Box3(x0 + w - 2 - width, 0, width, 1, 11, r);
        }

        /// <summary>A flat top on legs - most themes' table, its top filled by <paramref name="top"/>.</summary>
        static void Slab(HubSketch s, int w, int d, Color32[] r, System.Func<int, int, Color32?> top = null, System.Func<int, int, Color32?> front = null)
            => s.Box3(-w / 2, -11, w, d, 3, r, front, top);

        static void DrawTable(HubSketch s, HubTheme theme, int w, int d)
        {
            int x0 = -w / 2;
            switch (theme)
            {
                case HubTheme.Study:   // Drafting table: a tilted board on a trestle, a stool beside it
                    s.Line(x0 + 4, 0, x0 + 8, -14, Pal.Oak[3]); s.Line(x0 + w - 4, 0, x0 + w - 8, -14, Pal.Oak[3]);
                    for (int y = 0; y < d + 6; y++) for (int x = 0; x < w; x++) s.Set(x0 + x, -14 - y, x == 0 || x == w - 1 || y == 0 || y == d + 5 ? Pal.Oak[2] : Pal.Parch[1]);
                    s.Cyl3(x0 + w + 6, 2, 4, 7, Pal.Oak);
                    break;
                case HubTheme.Observatory:   // Star-chart table
                    s.Cyl3(0, 0, 5, 10, Pal.Navy);
                    s.Cyl3(0, -10, w / 2, 3, Pal.Navy, null, (x, y) => Hash(x, y, 4) > 0.97f ? H("#e8eeff") : Pal.Navy[3]);
                    break;
                case HubTheme.Crypt:   // Stone sigil table
                    s.Cyl3(0, 0, 5, 10, Pal.Stone);
                    s.Cyl3(0, -10, w / 2, 3, Pal.Stone);
                    break;
                case HubTheme.Terrace:   // Wind-rose table
                    s.Cyl3(0, 0, 5, 10, Pal.Marble);
                    s.Cyl3(0, -10, w / 2, 3, Pal.Marble, null, (x, y) => (Mathf.Abs(x) < 1 || Mathf.Abs(Mathf.Abs(x) - Mathf.Abs((y + 20) * 2.6f)) < 1) ? Pal.Gold[2] : Pal.Marble[1]);
                    break;
                case HubTheme.Atrium:   // Seed-tray table
                    Legs(s, w, Pal.Wicker); Slab(s, w, d, Pal.Wicker, (x, y) => x > 1 && x < w - 2 && y > 1 && y < d - 2 ? ((x % 6 == 0 || y % 5 == 0) ? Pal.Wicker[3] : H("#4a3020")) : Pal.Wicker[1]);
                    break;
                case HubTheme.Salt:   // Salt slab table
                    s.Box3(x0 + 4, 0, 6, 4, 9, Pal.SaltW); s.Box3(x0 + w - 10, 0, 6, 4, 9, Pal.SaltW);
                    s.Box3(x0, -9, w, d, 3, Pal.SaltW);
                    break;
                case HubTheme.Sanctum:   // Veined obsidian table
                    Legs(s, w, Pal.Blacks); Slab(s, w, d, Pal.Blacks, (x, y) => Pal.Blacks[2]);
                    break;
                case HubTheme.Smithy:   // Workbench
                    Legs(s, w, Pal.DarkWood); Slab(s, w, d, Pal.DarkWood, (x, y) => Hash(x, y) > 0.9f ? Pal.DarkWood[4] : Pal.DarkWood[1]);
                    s.Rect(x0 + w - 8, -15 - d + 4, x0 + w - 3, -15 - d + 4, Pal.Iron[2]);
                    break;
                case HubTheme.Chapel:   // Embroidered table
                    Legs(s, w, Pal.DarkWood); s.Box3(x0, -11, w, d, 4, Pal.Crim, (x, y) => y == 0 ? Pal.Gold[2] : Pal.Crim[3], (x, y) => Pal.Crim[2]);
                    break;
                case HubTheme.Shore:   // Sea-chart table
                    Legs(s, w, Pal.Drift); Slab(s, w, d, Pal.Drift, (x, y) => x > 1 && x < w - 2 && y > 1 && y < d - 2 ? (Noise(x * 0.4f, y * 0.6f) > 0.62f ? Pal.Sand[2] : Pal.Sea[1]) : Pal.Drift[1]);
                    break;
                case HubTheme.Volcano:   // Lava-veined table
                    s.Box3(x0 + 4, 0, 5, 3, 10, Pal.Obsid); s.Box3(x0 + w - 9, 0, 5, 3, 10, Pal.Obsid);
                    s.Box3(x0, -10, w, d, 3, Pal.Obsid, null, (x, y) => Mathf.Abs(Noise(x * 0.25f, y * 0.4f) - 0.5f) < 0.05f ? Lava(x, y, 0) : Pal.Obsid[2]);
                    break;
                case HubTheme.Dojo:   // Go board
                {
                    var kaya = new[] { H("#f0c878"), H("#d8a858"), H("#b88840"), H("#906a30"), H("#604620"), H("#302310") };
                    s.Box3(x0 + 3, 0, w - 6, d, 11, kaya, null, (x, y) => kaya[1]);
                    break;
                }
                case HubTheme.TechLab:   // Patch panel: the board stands on its face
                {
                    s.Box3(x0, 0, w, 4, 30, Pal.Beige, (x, y) => (x % 4 == 2 && y % 5 == 2 && x > 1 && x < w - 2 && y > 1 && y < 28) ? H("#141414") : Pal.Beige[2]);
                    Color32[] cols = { H("#ff4a4a"), H("#4a8aff"), H("#ffd04a"), H("#4ad06a") };
                    for (int i = 0; i < 4; i++) { int cx = x0 + 4 + i * (w - 8) / 4; s.Line(cx, -6 - (i % 3) * 5, cx + 6, 6 + i, cols[i]); }
                    break;
                }
            }
        }
    }
}
