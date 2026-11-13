using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// WEAPON THICKNESS (RIG.md, the user's pick 2026-11-12: "lean" with the thinner grip, off
    /// https://claude.ai/artifact/G4PJihfBWGfkvuVu9oJzFw, Tools/tier-art/thick8.js). A card keeps its flat art; each of its
    /// texels gets a half-thickness from the part it is read as, and Body8 draws the card as a slab that deep. The parts
    /// are read off the sprite alone, by the weapon's class, so every weapon (tier, Diamond, Black Diamond, placeholder)
    /// takes the same rules: a greatsword's grip is the 12 weapon rows round its pivot, the pommel below, the guard the
    /// rows just above the grip wider than the blade, the blade the rest (a diamond section); a disc is a plate with a
    /// rounded rim; a bow and everything else round.
    /// </summary>
    public static partial class Weapon8
    {
        /// <summary>What a card is read as, for its thickness. None: today's flat card.</summary>
        public enum Form { None, Sword, Disc, Bow, Round }

        /// <summary>A card's slab: per texel the half-thickness (body texels, 0 where clear) and the colour its walls show
        /// (an outline texel's wall shows the colour just inside it). RowLo/RowHi: each row's first and last solid texel
        /// (RowLo > RowHi on an empty row), so a line of sight skips the clear either side.</summary>
        public sealed class Slab
        {
            public float[] Half; public Color32[] Wall; public float HMax, K; public Color32 Outline;
            public int[] RowLo, RowHi;
        }

        // the user's numbers (lean), in body texels - one weapon texel is two
        /// <summary>A blade's half-thickness at its edges and down its spine.</summary>
        public const float BladeEdge = 0.6f, BladeSpine = 2f;
        /// <summary>A guard bar's half-thickness.</summary>
        public const float GuardHalf = 2.5f;
        /// <summary>A grip's depth against its width (an oval, thinner than round).</summary>
        public const float GripRound = 0.55f;
        /// <summary>Pommels, quivers, arrows against round; a bow's stave and string.</summary>
        public const float RoundScale = 0.8f, StaveScale = 0.7f;
        /// <summary>A disc's half-thickness.</summary>
        public const float PlateHalf = 2f;
        /// <summary>No texel is thinner than this (half, body texels).</summary>
        public const float MinHalf = 0.6f;
        /// <summary>The deepest any part goes (half, body texels): an element overlay finds its card within it.</summary>
        public const float MaxHalf = 8f;

        /// <summary>The slab for <paramref name="art"/> drawn <paramref name="k"/> body texels a texture texel, built once
        /// (again if the size changes by a quarter); null for a flat card.</summary>
        public static Slab SlabOf(Art art, float k)
        {
            if (art == null || art.Shape == Form.None || k <= 0f) return null;
            if (art.Thick != null && Mathf.Abs(art.Thick.K - k) < 0.25f * k) return art.Thick;
            return art.Thick = Build(art, k);
        }

        static Slab Build(Art art, float k)
        {
            int W = art.W, H = art.H, n = W * H;
            bool On(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && art.Px[y * W + x].a >= 128;
            float w = 2f / k;                       // texture texels a weapon texel

            // distance from each texel's centre to the clear (chamfer, two passes), texture texels
            var d = new float[n];
            const float Big = 1e6f, D1 = 1f, D2 = 1.41421f;
            for (int i = 0; i < n; i++) d[i] = On(i % W, i / W) ? Big : 0f;
            float Dat(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? 0f : d[y * W + x];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int i = y * W + x; if (d[i] == 0f) continue;
                d[i] = Mathf.Min(d[i], Mathf.Min(Mathf.Min(Dat(x - 1, y) + D1, Dat(x, y - 1) + D1), Mathf.Min(Dat(x - 1, y - 1) + D2, Dat(x + 1, y - 1) + D2)));
            }
            for (int y = H - 1; y >= 0; y--) for (int x = W - 1; x >= 0; x--)
            {
                int i = y * W + x; if (d[i] == 0f) continue;
                d[i] = Mathf.Min(d[i], Mathf.Min(Mathf.Min(Dat(x + 1, y) + D1, Dat(x, y + 1) + D1), Mathf.Min(Dat(x + 1, y + 1) + D2, Dat(x - 1, y + 1) + D2)));
            }
            // the distance from the centre: a texel beside the clear is half a texel in
            for (int i = 0; i < n; i++) if (d[i] > 0f) d[i] -= 0.5f;
            // the outline: the commonest colour on the art's edge; its walls show the colour just inside it
            var count = new System.Collections.Generic.Dictionary<int, int>();
            int bestN = 0; var outline = new Color32(20, 18, 28, 255);
            for (int i = 0; i < n; i++)
            {
                if (d[i] <= 0f || d[i] > 0.6f) continue;
                var c = art.Px[i]; int key = (c.r << 16) | (c.g << 8) | c.b;
                count.TryGetValue(key, out int m); count[key] = ++m;
                if (m > bestN) { bestN = m; outline = new Color32(c.r, c.g, c.b, 255); }
            }
            bool IsOutline(Color32 c) => Mathf.Abs(c.r - outline.r) + Mathf.Abs(c.g - outline.g) + Mathf.Abs(c.b - outline.b) < 24;
            // how thick the art's outline is (the game's sprites carry two weapon texels, the mock one): the shapes are
            // measured inside one weapon texel of outline, as the mock's were
            var runs = new System.Collections.Generic.List<int>();
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                if (!On(x, y)) continue;
                int m = 0; while (On(x + m, y) && IsOutline(art.Px[y * W + x + m])) m++;
                if (m > 0 && On(x + m, y)) runs.Add(m);
                break;
            }
            runs.Sort();
            float extra = Mathf.Max(0f, (runs.Count > 0 ? runs[runs.Count / 2] : w) - w);
            for (int i = 0; i < n; i++) if (d[i] > 0f) d[i] = Mathf.Max(0.5f, d[i] - extra);

            // the ridge: the deepest point nearby (a square max filter, rows then columns)
            int rr = Mathf.Max(1, Mathf.RoundToInt(4f * w));
            var tmp = new float[n]; var ridge = new float[n];
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { float m = 0f; for (int dx = -rr; dx <= rr; dx++) { int xx = x + dx; if (xx >= 0 && xx < W) m = Mathf.Max(m, d[y * W + xx]); } tmp[y * W + x] = m; }
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { float m = 0f; for (int dy = -rr; dy <= rr; dy++) { int yy = y + dy; if (yy >= 0 && yy < H) m = Mathf.Max(m, tmp[yy * W + x]); } ridge[y * W + x] = m; }

            // a row's opaque run through a texel
            void Run(int x, int y, out int a, out int b) { a = x; b = x; while (On(a - 1, y)) a--; while (On(b + 1, y)) b++; b++; }
            // the run as the shape is measured: inside one weapon texel of outline
            float HalfRun(int a, int b) => Mathf.Max(0.5f, (b - a) * 0.5f - extra);
            float Round(int i, float scale) { float dd = Mathf.Min(d[i], ridge[i]), R = Mathf.Max(ridge[i], 0.5f); return scale * Mathf.Sqrt(Mathf.Max(0.1f, dd * (2f * R - dd))) * k; }

            // a greatsword's rows: the grip round the pivot, the pommel below, the guard just above
            var guardRow = new bool[H]; var pommelRow = new bool[H];
            float gripTop = art.Pivot.y - 6f * w, gripBot = art.Pivot.y + 6f * w;
            if (art.Shape == Form.Sword)
            {
                int Width(int y) { int best = 0; for (int x = 0; x < W; x++) if (On(x, y)) { Run(x, y, out int a, out int b); best = Mathf.Max(best, b - a); x = b; } return best; }
                // the blade at its widest (the rows well above the grip), and the grip's own width at its middle
                float blade = 0f;
                for (int y = 0; y < H && y + 0.5f < gripTop - 16f * w; y++) blade = Mathf.Max(blade, Width(y));
                if (blade <= 0f) blade = 8f * w;
                float gripW = Width(Mathf.Clamp(Mathf.FloorToInt(art.Pivot.y), 0, H - 1));
                // the guard: the rows just above the grip wider than the blade, gaps of up to two weapon rows bridged
                int gap = 0, top = Mathf.FloorToInt(gripTop - 0.5f);
                for (int y = top; y >= 0 && y + 0.5f > gripTop - 16f * w; y--)
                {
                    if (Width(y) >= blade + 2f * w) { guardRow[y] = true; for (int g = 1; g <= gap; g++) guardRow[y + g] = true; gap = 0; }
                    else if (++gap > Mathf.RoundToInt(2f * w)) break;
                }
                // rows in the grip's band wider than the grip: the guard's (above the pivot) or the pommel's (below)
                for (int y = Mathf.Max(0, Mathf.FloorToInt(gripTop)); y < H && y + 0.5f <= gripBot; y++)
                    if (Width(y) > gripW + 2f * w) { if (y + 0.5f < art.Pivot.y) guardRow[y] = true; else pommelRow[y] = true; }
            }

            var s = new Slab { Half = new float[n], Wall = new Color32[n], K = k, Outline = outline };
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int i = y * W + x; if (d[i] <= 0f) continue;
                float h;
                switch (art.Shape)
                {
                    case Form.Sword:
                    {
                        float yc = y + 0.5f;
                        if (yc > gripBot || pommelRow[y]) h = Round(i, RoundScale);
                        else if (guardRow[y]) h = GuardHalf * Mathf.Min(1f, 0.55f + 0.3f * d[i] / w);
                        else if (yc >= gripTop)
                        {
                            Run(x, y, out int a, out int b);
                            float hw = HalfRun(a, b), u = Mathf.Min(0.95f, Mathf.Abs(x + 0.5f - (a + b) * 0.5f) / hw);
                            h = GripRound * hw * Mathf.Sqrt(1f - u * u) * k;
                        }
                        else
                        {
                            Run(x, y, out int a, out int b);
                            float hw = HalfRun(a, b), u = Mathf.Min(1f, Mathf.Abs(x + 0.5f - (a + b) * 0.5f) / hw);
                            h = BladeEdge + (BladeSpine - BladeEdge) * (1f - u);
                        }
                        break;
                    }
                    case Form.Disc: h = PlateHalf * Mathf.Min(1f, Mathf.Sqrt(d[i] / w / 2f)); break;
                    case Form.Bow: h = Round(i, StaveScale); break;
                    default: h = Round(i, RoundScale); break;
                }
                s.Half[i] = Mathf.Clamp(h, MinHalf, MaxHalf);
                s.HMax = Mathf.Max(s.HMax, s.Half[i]);
            }
            s.RowLo = new int[H]; s.RowHi = new int[H];
            for (int y = 0; y < H; y++)
            {
                int a = W, b = -1;
                for (int x = 0; x < W; x++) if (s.Half[y * W + x] > 0f) { if (a == W) a = x; b = x; }
                s.RowLo[y] = a; s.RowHi[y] = b;
            }

            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                int i = y * W + x; if (d[i] <= 0f) continue;
                var c = art.Px[i]; s.Wall[i] = c;
                if (!IsOutline(c)) continue;
                float best = float.MaxValue;
                for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++)
                {
                    if (!On(x + dx, y + dy)) continue;
                    var cc = art.Px[(y + dy) * W + x + dx]; if (IsOutline(cc)) continue;
                    float dd = dx * dx + dy * dy; if (dd < best) { best = dd; s.Wall[i] = cc; }
                }
            }
            return s;
        }
    }
}
