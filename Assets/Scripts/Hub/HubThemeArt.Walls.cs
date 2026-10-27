using UnityEngine;
using static Convergence.Hub.HubSketch;

namespace Convergence.Hub
{
    public static partial class HubThemeArt
    {
        // ---- the sigil selector ----

        /// <summary>True where the selector's rings are LIGHT (tinted gold and breathing, as group 6 built
        /// it); every other theme's rings are a material, drawn in its own colours.</summary>
        public static bool SelectorGlows(HubTheme t) => t == HubTheme.Observatory;

        /// <summary>One of the selector's four rings in <paramref name="theme"/>, <paramref name="radius"/>
        /// world units, pivoted in its middle. <paramref name="i"/> varies a ring's flowers or veins.</summary>
        public static Sprite SelectorRing(HubTheme theme, float radius, int i)
        {
            if (SelectorGlows(theme)) return Combat.TexelFx.RingSprite(radius);
            int rp = Mathf.Max(3, Mathf.RoundToInt(radius * HubArt.Ppu));
            return HubArt.Get($"selector.{theme}.{rp}.{i}", () =>
            {
                int n = rp * 2 + 3; float c = n / 2f;
                var cv = new TexelCanvas(n, n);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d > rp + 0.5f || d < rp - 0.9f) continue;
                        float a = Mathf.Atan2(dy, dx);
                        var col = RingTexel(theme, x, y, a, i, dy > 0);
                        if (col.HasValue) cv.Set(x, y, col.Value);
                    }
                return cv.ToSprite("selector", n / 2, n / 2, HubArt.Ppu);
            });
        }

        static Color32? RingTexel(HubTheme t, int x, int y, float a, int i, bool upper)
        {
            switch (t)
            {
                case HubTheme.Cloister: return upper ? Pal.Stone[1] : H("#121015");   // carved: a lit lip over a dark groove
                case HubTheme.Crypt: return upper ? Pal.CryptS[0] : H("#0a080c");
                case HubTheme.Study: return upper ? Pal.Brass[1] : Pal.Brass[3];     // brass relief
                case HubTheme.Atrium: return Hash(x, y) > 0.86f ? new[] { Pal.Velvet[1], Pal.Yellow[1], Pal.White }[i % 3] : Pal.Leaf[2];
                case HubTheme.Salt: return upper ? Pal.Pink[1] : Pal.Pink[3];
                case HubTheme.Terrace: return upper ? Pal.Gold[1] : Pal.Gold[3];
                case HubTheme.Sanctum: return Ramp(Pal.RedX, 0.3f + 0.2f * Mathf.Sin(a * 2 + i));
                case HubTheme.Smithy: return Mathf.RoundToInt(a * 3f) % 4 == 0 ? Pal.Iron[0] : upper ? Pal.Iron[1] : Pal.Iron[3];
                case HubTheme.Chapel: return Mathf.RoundToInt(a * 4f) % 2 == 0 ? Pal.Gold[1] : Pal.Crim[1];
                case HubTheme.Shore: return Mathf.RoundToInt(a * 4f) % 2 == 0 ? H("#fff4ec") : Pal.Coral[1];
                case HubTheme.Volcano: return Fire(0.15f + Noise(x * 0.3f, y * 0.4f) * 0.6f);
                case HubTheme.Dojo: return Mathf.Sin(a + i) > 0.85f ? (Color32?)null : H("#141010");   // one stroke, left open
                case HubTheme.TechLab: return Mathf.RoundToInt(a * 5f) % 3 == 0 ? (Color32?)null : Pal.Crt[2];
                default: return Pal.Gold[2];
            }
        }

        // ---- the armoury doorway ----

        /// <summary>The armoury doorway in <paramref name="theme"/>: a black opening
        /// (2·<paramref name="halfW"/>+1 by <paramref name="h"/> texels) and its frame. Pivot at the
        /// threshold's middle - the opening stays black, it is a way through.</summary>
        public static Sprite Doorway(HubTheme theme, int halfW, int h)
        {
            if (theme == HubTheme.Cloister) return HubArt.Portcullis(halfW, h);
            return Cached($"door.{theme}.{halfW}.{h}", 0, s => DrawDoorway(s, theme, halfW, h));
        }

        static void DrawDoorway(HubSketch s, HubTheme theme, int hw, int h)
        {
            int x0 = -hw, x1 = hw, top = -h;
            for (int y = top; y < 0; y++) for (int x = x0; x <= x1; x++) s.Set(x, y, Pal.Void);
            void Jambs(int wide, System.Func<int, int, Color32> col)
            {
                for (int y = top - 3; y < 0; y++) for (int k = 1; k <= wide; k++) { s.Set(x0 - k, y, col(-k, y)); s.Set(x1 + k, y, col(k, y)); }
                for (int x = x0 - wide; x <= x1 + wide; x++) for (int y = top - 3; y < top; y++) s.Set(x, y, col(0, y));
            }
            switch (theme)
            {
                case HubTheme.Crypt:   // its own portcullis, rusted
                    Jambs(3, (k, y) => Mathf.Abs(k) == 3 ? K : Pal.Rust[Mathf.Abs(k) == 1 ? 2 : 1]);
                    for (int x = x0; x <= x1; x += 3) for (int y = top; y < top + 7; y++) s.Set(x, y, Pal.Rust[2]);
                    for (int x = x0; x <= x1; x++) s.Set(x, top + 4, Pal.Rust[1]);
                    break;
                case HubTheme.Study:   // timber door, swung back
                    Jambs(2, (k, y) => Pal.DarkWood[k < 0 ? 1 : 2]);
                    for (int y = top; y < 0; y++) for (int x = x0; x < x0 + 5; x++) s.Set(x, y, x == x0 + 4 ? K : Pal.DarkWood[2]);
                    s.Set(x0 + 2, top + h / 2, Pal.Brass[1]);
                    break;
                case HubTheme.Observatory: Jambs(3, (k, y) => (y % 5 == 0 || (k == 0 && Hash(y, 3) > 0.7f)) ? Pal.Brass[1] : Pal.Navy[2]); break;
                case HubTheme.Atrium: Jambs(4, (k, y) => Hash(k + 9, y) > 0.25f ? Pal.Leaf[1 + Mathf.FloorToInt(Hash(k, y, 2) * 3)] : Pal.Stone[2]); break;
                case HubTheme.Salt: Jambs(3, (k, y) => new[] { Pal.Pink[1], Pal.SaltW[1], Pal.Pink[2], Pal.SaltW[2] }[((y / 2) % 4 + 4) % 4]); break;
                case HubTheme.Terrace: Jambs(4, (k, y) => k == 0 ? (y == top - 3 ? Pal.Marble[0] : Pal.Marble[2]) : new[] { Pal.Marble[3], Pal.Marble[1], Pal.Marble[2], Pal.Marble[3] }[Mathf.Abs(k) - 1]); break;
                case HubTheme.Sanctum: Jambs(2, (k, y) => Mathf.Abs(Noise(k * 0.6f, y * 0.5f) - 0.5f) < 0.08f ? Pal.RedX[1] : Pal.Blacks[2]); break;
                case HubTheme.Smithy:
                    Jambs(2, (k, y) => y % 3 == 0 ? Pal.Brick[4] : Pal.Brick[2]);
                    for (int y = top; y < 0; y++) for (int x = x0; x < x0 + 4; x++) s.Set(x, y, y % 5 == 0 ? Pal.Iron[1] : x == x0 + 3 ? K : Pal.Iron[3]);
                    break;
                case HubTheme.Chapel:   // pointed arch, crimson traced in gold
                    for (int y = top - hw; y < top; y++) for (int x = x0 - 2; x <= x1 + 2; x++)
                    {
                        float ax = Mathf.Abs(x), peak = top - hw + ax * ax / Mathf.Max(1, hw);
                        if (y < peak) continue;
                        s.Set(x, y, ax > hw - 1 || y < peak + 2 ? Pal.Gold[2] : Pal.Void);
                    }
                    Jambs(2, (k, y) => Mathf.Abs(k) == 2 ? Pal.Gold[2] : Pal.Crim[2]);
                    break;
                case HubTheme.Shore:   // the mouth of a sea cave
                    for (int y = top - 6; y < 0; y++) for (int x = x0 - 5; x <= x1 + 5; x++)
                    {
                        float d = Mathf.Sqrt(Mathf.Pow(x / (hw + 5f), 2) + Mathf.Pow(y / (h + 6f), 2));
                        if (d > 1f) continue;
                        s.Set(x, y, d > 0.84f ? (y < top + 4 && Hash(x, 1) > 0.5f ? Pal.Leaf[3] : Pal.Drift[3]) : Pal.Void);
                    }
                    break;
                case HubTheme.Volcano:   // blast door drawn up
                    Jambs(3, (k, y) => y % 4 == 0 ? Pal.RedX[2] : Pal.Plast[4]);
                    for (int x = x0 - 3; x <= x1 + 3; x++) { s.Set(x, top + 1, Pal.Yellow[2]); s.Set(x, top, Pal.Plast[3]); }
                    break;
                case HubTheme.Dojo:   // a shoji slid aside
                    Jambs(1, (k, y) => Pal.DarkWood[1]);
                    for (int y = top - 2; y < 0; y++) for (int x = x0 - 10; x < x0; x++) s.Set(x, y, x == x0 - 10 || x == x0 - 1 || y == top - 2 || (x - x0) % 3 == 0 || y % 4 == 0 ? Pal.DarkWood[2] : Pal.Paper[1]);
                    break;
                case HubTheme.TechLab:
                    Jambs(2, (k, y) => Pal.Plast[3]);
                    for (int y = top + 6; y < top + 11; y++) for (int x = x1 + 4; x < x1 + 8; x++) s.Set(x, y, y == top + 6 && x == x1 + 6 ? Pal.Crt[1] : Pal.Plast[4]);
                    break;
            }
            for (int x = x0 - 3; x <= x1 + 3; x++) s.Set(x, 0, Pal.Stone[2]);
        }
    }
}
