using UnityEngine;
using static Convergence.Hub.HubSketch;

namespace Convergence.Hub
{
    /// <summary>
    /// Every hub item in every theme (the user's group 8 picks), carried over from the mocks line for
    /// line in their own coordinates (see <see cref="HubSketch"/>). One file per item; the Cloister's
    /// pieces that already existed stay in <see cref="HubArt"/>.
    /// </summary>
    public static partial class HubThemeArt
    {
        static Color32 F(int i) => Fire(i / 6f + 0.02f);   // the mock's FIRE[i]
        static Color32 H(string hex) => Pal.Hex(hex);
        static readonly Color32[] Red = { H("#ffe9e4"), H("#ff9a8a"), H("#ff4a3a"), H("#c02020"), H("#701010"), H("#380808") };
        static Color32 Lava(float x, float y, float t) => Fire(0.15f + Noise(x * 0.3f, y * 0.4f - t * 2f) * 0.6f);

        public const int ForgeFrames = 8;
        public const float ForgeFps = 10f;

        /// <summary>The forge in <paramref name="theme"/>, frame <paramref name="frame"/>; full fire while
        /// it has something to do (<paramref name="lit"/>), embers otherwise. Pivot at its foot.</summary>
        public static Sprite Forge(HubTheme theme, int frame, bool lit)
        {
            if (theme == HubTheme.Cloister) return HubArt.Hearth(frame, lit);
            float t = frame / (float)ForgeFrames * 0.8f;
            float k = lit ? 1f : 0.45f;   // how high the fire stands
            return Cached($"forge.{theme}.{frame}.{lit}", t, s => DrawForge(s, theme, t, k));
        }

        static void DrawForge(HubSketch s, HubTheme theme, float t, float k)
        {
            switch (theme)
            {
                case HubTheme.Study:   // Athanor
                {
                    var brick = new[] { H("#c87850"), H("#a85a3a"), H("#88442a"), H("#66301e"), H("#441e12"), H("#24100a") };
                    float top = s.Cyl3(0, 0, 10, 26, brick, (x, y, u) => (y % 4 == 0 || (x + (Mathf.Abs(Mathf.FloorToInt(y / 4f)) % 2) * 3) % 6 == 0) ? H("#3a1c10") : Ramp(brick, 0.1f + u * 0.8f));
                    for (int y = -10; y <= -2; y++) for (int x = -4; x <= 4; x++) if (Mathf.Sqrt(x * x / 16f + (y + 6) * (y + 6) / 16f) < 1 || y > -6) s.Set(x, y, Fire(C01(0.2f + Noise(x * 0.7f, y * 0.5f - t * 6f) * 0.6f / k)));
                    for (float a = 0; a < 6.3f; a += 0.15f) s.Set(Mathf.Cos(a) * 5, top - 6 + Mathf.Sin(a) * 5, Pal.Teal[1], 0.9f);
                    s.Rect(-3, top - 9, 2, top - 3, Pal.Teal[3], 0.35f);
                    s.Line(5, top - 8, 18, top - 2, Pal.Teal[1]);
                    s.Box3(15, -2, 7, 2, 10, Pal.Oak);
                    for (float a = 0; a < 6.3f; a += 0.3f) s.Set(18 + Mathf.Cos(a) * 3, -15 + Mathf.Sin(a) * 3, Pal.Teal[1]);
                    if ((t * 2f) % 1f < 0.5f) s.Set(18, top + 1 + (t * 2f % 1f) * 8, Pal.Teal[0]);
                    break;
                }
                case HubTheme.Crypt:   // Brazier and crucible
                {
                    foreach (int lx in new[] { -8, 0, 8 }) s.Line(lx * 0.6f, -12, lx, lx == 0 ? 2 : 0, (x, y, f) => { s.Set(x + 1, y, K); return Pal.Iron[3]; });
                    s.Cyl3(0, -10, 11, 4, Pal.Iron, null, (x, y) => Fire(0.3f / k + Noise(x * 0.5f, y * 0.8f + t * 3f) * 0.6f));
                    s.Cyl3(1, -17, 4, 6, new[] { F(0), F(1), F(2), F(3), F(4), F(5) });
                    for (int i = 0; i < 6; i++) { float age = (t * 0.8f + i / 6f) % 1f; if (age < k) s.Set(-6 + i * 2 + Mathf.Sin(age * 6 + i), -20 - age * 16, Fire(0.2f + age * 0.7f), 1 - age); }
                    for (int y = 0; y < 10; y++) { float w = 6 - y * 0.4f; s.Rect(-26, -y, -26 + w, -y, y % 3 == 0 ? Pal.DarkWood[3] : H("#6a4a3a")); }
                    s.Line(-20, -8, -13, -11, Pal.Iron[2]);
                    break;
                }
                case HubTheme.Observatory:   // Star furnace
                {
                    foreach (int lx in new[] { -9, 0, 9 }) s.Line(lx * 0.4f, -10, lx, lx == 0 ? 2 : 0, Pal.Brass[3]);
                    s.Disc(0, -20, 10, 10, (x, y, d) => s.Set(x, y, d > 0.9f ? K : Ramp(Pal.Brass, 0.15f + (x + 10) / 20f * 0.5f)));
                    for (int i = 0; i < 7; i++)
                    {
                        float a = Hash(i, 3) * 6.3f, r = Hash(i, 4) * 6; float x = Mathf.Cos(a) * r, y = -20 + Mathf.Sin(a) * r;
                        var c = Fire(0.1f / k + Noise(i, t * 4) * 0.5f); s.Set(x, y, c); s.Set(x + 1, y, F(2)); s.Set(x, y + 1, F(2));
                    }
                    s.Ring(0, -20, 12, 13, 0.3f, (x, y, d, a) => { if (Mathf.Sin(a) > 0) s.Set(x, y, Pal.Brass[1]); });
                    break;
                }
                case HubTheme.Atrium:   // Garden kiln
                {
                    s.Box3(-13, 0, 26, 6, 6, Pal.Terra);
                    s.Disc(0, -12, 13, 12, (x, y, d) => { if (y > -12) return; s.Set(x, y, d > 0.92f ? K : (Mathf.FloorToInt(y / 3f) + (x >> 2)) % 5 == 0 ? Pal.Terra[3] : Ramp(Pal.Terra, 0.2f + (x + 13) / 26f * 0.5f)); });
                    for (int y = -12; y <= -6; y++) for (int x = -4; x <= 4; x++) if (Mathf.Sqrt(x * x / 16f + (y + 9) * (y + 9) / 16f) < 1 || y > -9) s.Set(x, y, Fire(C01(0.2f + Noise(x * 0.7f, y * 0.5f - t * 6) * 0.6f / k)));
                    Ivy(s, 4, -22, 13, -6, 16, 3);
                    break;
                }
                case HubTheme.Salt:   // Brine pan
                {
                    s.Box3(-14, 0, 28, 6, 10, Pal.SaltW, (x, y) => (x + Mathf.FloorToInt(y / 3f) * 4) % 8 == 0 || y % 3 == 0 ? Pal.SaltW[3] : Pal.SaltW[1]);
                    for (int x = -6; x <= 6; x++) for (int y = 1; y < 5; y++) s.Set(x, -y, Fire(C01(0.3f / k + Noise(x * 0.6f, y - t * 5) * 0.5f)));
                    s.Cyl3(0, -12, 15, 2, Pal.Iron, null, (x, y) => Hash(x, y, Mathf.FloorToInt(t * 2)) > 0.8f ? Pal.SaltW[0] : H("#8aa0a8"));
                    for (int i = 0; i < 6; i++) { float age = (t * 0.6f + i / 6f) % 1f; s.Set(-8 + i * 3, -20 - age * 10, H("#e8eef0"), (1 - age) * k); }
                    break;
                }
                case HubTheme.Terrace:   // Sun brazier
                {
                    s.Box3(-5, 0, 10, 3, 18, Pal.Marble, Flute(Pal.Marble));
                    s.Cyl3(0, -18, 12, 4, Pal.Marble, null, (x, y) => Fire(C01(0.3f / k + Noise(x * 0.5f, y * 0.8f + t * 3) * 0.6f)));
                    s.Flame(0, -26, 6, Mathf.RoundToInt(9 * k), t);
                    s.Rect(-12, -22, 12, -22, Pal.Gold[1]);
                    break;
                }
                case HubTheme.Sanctum:   // Crucible of the Stone
                {
                    s.Box3(-14, 0, 28, 6, 6, Pal.Blacks);
                    s.Cyl3(0, -6, 9, 12, Pal.Blacks, null, (x, y) => Ramp(Pal.RedX, C01(0.2f / k + Noise(x * 0.5f, y + t * 3) * 0.6f)));
                    foreach (int cx in new[] { -14, -10, 11, 15 }) s.Crystal(cx, -5, 7 + (cx & 3), 2, Pal.RedX);
                    for (int i = 0; i < 5; i++) { float age = (t * 0.7f + i / 5f) % 1f; s.Set(-4 + i * 2, -22 - age * 12, Pal.RedX[1], (1 - age) * k); }
                    break;
                }
                case HubTheme.Smithy:   // Great forge
                {
                    s.Box3(-14, -2, 26, 7, 14, Pal.Brick, (x, y) => (x + Mathf.FloorToInt(y / 3f) * 3) % 6 == 0 || y % 3 == 0 ? Pal.Brick[4] : Pal.Brick[2]);
                    for (int y = -22; y > -48; y--) { int w = 9; s.Rect(-1 - w, y, -1 + w, y, y % 3 == 0 ? Pal.Brick[4] : Pal.Brick[3]); s.Set(-1 - w, y, K); s.Set(-1 + w, y, K); }
                    for (int yy = 0; yy < Mathf.RoundToInt(8 * k); yy++) for (int xx = -8; xx <= 6; xx++)
                    {
                        float v = Noise(xx * 0.6f, (yy - t * 18) * 0.5f) - Mathf.Abs(xx + 1) / 8f * 0.4f - yy / 8f * 0.4f;
                        if (v > 0.15f) s.Set(xx, -9 - yy, Fire(0.8f - v * 1.6f));
                    }
                    int p = 3 + Mathf.RoundToInt(Mathf.Sin(t * 4) * 2);
                    for (int y = 0; y < p * 2; y++) for (int x = 0; x < 10 - y * 0.3f; x++) s.Set(16 + x, -4 - y, H("#6a4a3a"));
                    s.Rect(12, -6, 15, -6, Pal.Iron[2]);
                    break;
                }
                case HubTheme.Chapel:   // Crimson athanor
                {
                    s.Cyl3(0, 0, 10, 26, Pal.Crim, (x, y, u) => y % 7 == 0 ? Pal.Gold[2] : Ramp(Pal.Crim, 0.1f + u * 0.7f));
                    for (int y = -10; y <= -2; y++) for (int x = -4; x <= 4; x++) if (Mathf.Sqrt(x * x / 16f + (y + 6) * (y + 6) / 16f) < 1 || y > -6) s.Set(x, y, Ramp(Pal.Gold, C01(Noise(x * 0.7f, y * 0.5f - t * 6) * 0.8f / k)));
                    break;
                }
                case HubTheme.Shore:   // Driftwood fire
                {
                    for (int i = 0; i < 12; i++) { float a = i / 12f * 6.3f; s.Disc(Mathf.Cos(a) * 11, -4 + Mathf.Sin(a) * 4, 2, 1.5f, (x, y, d) => s.Set(x, y, Pal.Stone[1 + i % 3])); }
                    s.Line(-7, -3, 6, -6, Pal.Drift[2]); s.Line(-5, -3, 7, -6, Pal.Drift[2]);
                    s.Flame(0, -6, 5, Mathf.RoundToInt(9 * k), t);
                    foreach (int lx in new[] { -9, 9 }) s.Line(0, -26, lx, -2, Pal.Drift[3]);
                    s.Cyl3(0, -16, 4, 4, Pal.Iron);
                    break;
                }
                case HubTheme.Volcano:   // Lava font
                {
                    s.Cyl3(0, 0, 13, 8, Pal.Obsid, null, (x, y) => Lava(x, y, t));
                    for (int y = 2; y < 12; y++) for (int x = -1; x <= 1; x++) s.Set(x, y, Lava(x, y, t));
                    for (int i = 0; i < 6; i++) { float age = (t * 0.8f + i / 6f) % 1f; s.Set(-8 + i * 3, -14 - age * 14, Fire(0.2f + age * 0.7f), (1 - age) * k); }
                    break;
                }
                case HubTheme.Dojo:   // Swordsmith's hearth
                {
                    var clay = new[] { H("#c8a888"), H("#a88868"), H("#886a4c"), H("#6a4e36"), H("#4a3424"), H("#261a10") };
                    s.Box3(-14, 0, 28, 8, 6, clay, null, (x, y) => x > 6 && x < 21 && y > 1 && y < 6 ? Fire(C01(0.4f / k + Noise(x * 0.6f, y + t * 3) * 0.5f)) : clay[2]);
                    s.Rect(-4, -10, 8, -10, Mix(Pal.Steel[1], F(2), 0.5f * k));
                    s.Line(8, -10, 18, -6, Pal.Iron[2]);
                    break;
                }
                case HubTheme.TechLab:   // Laser cutter
                {
                    s.Box3(-16, 0, 32, 12, 14, Pal.Beige, null, (x, y) => x > 2 && x < 29 && y > 1 && y < 10 ? H("#202830") : Pal.Beige[1]);
                    float a = t * 2 * k;
                    for (int i = 0; i < 20; i++) { float aa = a - i * 0.15f; s.Set(Mathf.Cos(aa) * 8, -20 + Mathf.Sin(aa) * 3, Pal.RedX[3]); }
                    float bx = Mathf.Cos(a) * 8, by = -20 + Mathf.Sin(a) * 3;
                    s.Set(bx, by, Pal.White); s.Set(bx + 1, by, Pal.RedX[1]); s.Set(bx, by - 1, Pal.RedX[1]);
                    for (int i = 0; i < 3; i++) s.Set(10 + i * 2, -6, (t * 2 + i) % 1 < 0.5f ? Pal.Crt[1] : Pal.Crt[4]);
                    break;
                }
            }
        }

        /// <summary>The forge's light, in the theme's own fire.</summary>
        public static Color ForgeLight(HubTheme t) => t switch
        {
            HubTheme.Sanctum => new Color(0.9f, 0.2f, 0.15f),
            HubTheme.Chapel => new Color(1f, 0.75f, 0.35f),
            HubTheme.TechLab => new Color(1f, 0.3f, 0.25f),
            HubTheme.Salt => new Color(1f, 0.6f, 0.3f),
            _ => new Color(1f, 0.55f, 0.2f),
        };

        // ---- small shared strokes ----

        static System.Func<int, int, Color32?> Flute(Color32[] r) => (x, y) => x % 3 == 0 ? r[3] : x % 3 == 1 ? r[1] : r[2];

        static void Ivy(HubSketch s, float x0, float y0, float x1, float y1, int n, int seed)
        {
            for (int k = 0; k < n; k++)
            {
                float x = x0 + Hash(k, seed) * (x1 - x0), y = y0 + Hash(k, seed, 2) * (y1 - y0);
                s.Set(x, y, Pal.Leaf[1 + (Hash(k, seed, 3) > 0.5f ? 1 : 0)]); s.Set(x + 1, y, Pal.Leaf[2]);
            }
        }
    }
}
