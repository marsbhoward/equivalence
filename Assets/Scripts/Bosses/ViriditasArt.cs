using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// What Viriditas looks like at one moment - everything the drawing reads, set by its fight each
    /// frame. Plain fields, so a domain reload keeps them.
    /// </summary>
    [System.Serializable]
    public struct ViriditasLook
    {
        public bool Walk;
        /// <summary>0 standing .. 1 curled into its seed (the roll).</summary>
        public float Curl;
        /// <summary>The seed's spin, radians.</summary>
        public float Roll;
        /// <summary>0..1 reared up for the stomp; it slams back down to 0.</summary>
        public float Rear;
        /// <summary>0..1 its shell split open down the seam, the body glowing gold (vulnerable).</summary>
        public float Split;
        /// <summary>Flowers open in its seams (two a sapling it rolled into).</summary>
        public int Flowers;
        /// <summary>0..1 the carapace of beetles crawling round it (the ward).</summary>
        public float Ward;
        /// <summary>0..1 the great lotus open over it (the Bloom, the regrowth).</summary>
        public float Bloom;
        /// <summary>Enraged: a small lotus stays open on its back.</summary>
        public bool Flowered;
        public bool Dazed;
        /// <summary>0..1 its legs clamped round the player (the grab).</summary>
        public float Grab;
        /// <summary>Its death: 0..1 the husk darkening, 0..1 the sapling growing out of it.</summary>
        public float Husk, Sprout;
    }

    /// <summary>
    /// A texel picture drawn into a depth-sorted scratch grid, then outlined and laid down - shared
    /// by the beetle and the grove. Offsets from the centre texel, y UP.
    /// </summary>
    public abstract class GroveCanvas : TexelPicture
    {
        static Color32 C(int hex) => new((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        public static readonly Color32[] Husk = { C(0xf0b88a), C(0xc98a5a), C(0x9a5f3a), C(0x6a3d26), C(0x40241a) };
        public static readonly Color32[] Horn = { C(0x9a8ad8), C(0x6a5ab0), C(0x463a80), C(0x2a2250), C(0x181430) };
        public static readonly Color32[] Plate = { C(0xb8f0e0), C(0x7fd0bc), C(0x4a9a8c), C(0x2c6058) };
        public static readonly Color32[] Pollen = { C(0xfffbe0), C(0xfff0a0), C(0xf2d050), C(0xc49a20), C(0x7a5c10) };
        public static readonly Color32[] Lotus = { C(0xfff6fa), C(0xffd6e6), C(0xf6a6c8), C(0xd8709e), C(0x9a3c6c) };
        public static readonly Color32[] Pod = { C(0xfff0b0), C(0xf2c45a), C(0xb8862a), C(0x6e4e14) };
        public static readonly Color32[] Moss = { C(0xc8e07a), C(0x8fbf4f), C(0x5e9440), C(0x3d6a33), C(0x244424) };
        public static readonly Color32[] Bark = { C(0xd9b27a), C(0xb0844e), C(0x83603a), C(0x5a4029), C(0x3a2a1c) };
        public static readonly Color32[] Leaf = { C(0xc8e07a), C(0x8fbf4f), C(0xf6a6c8), C(0x5e9440), C(0xfff0a0) };
        public static readonly Color32[] Sap = { C(0xffe9a8), C(0xf2b640), C(0xc07a18), C(0x7a4a0e) };
        /// <summary>Six-step ramps for TexelFx.Rubble (it reads six: lit top .. its shadow).</summary>
        public static readonly Color32[] BarkChips = { C(0xd9b27a), C(0xb0844e), C(0x83603a), C(0x5a4029), C(0x3a2a1c), C(0x1e150e) };
        public static readonly Color32[] SproutChips = { C(0xc8e07a), C(0x8fbf4f), C(0x5e9440), C(0x3d6a33), C(0x244424), C(0x12220f) };
        protected static readonly Color32 K = C(0x0d0b0e);

        Color32[] _col; float[] _z; bool[] _has;

        protected static float Hash(int a, int b = 0, int c = 0) => FireArt.Hash(a, b, c);
        protected static Color32 Ix(Color32[] r, float v) => r[Mathf.Clamp(Mathf.RoundToInt(v), 0, r.Length - 1)];

        protected void Begin()
        {
            int n = W * H;
            if (_col == null || _col.Length != n) { _col = new Color32[n]; _z = new float[n]; _has = new bool[n]; }
            System.Array.Clear(_has, 0, n);
        }

        protected bool Has(int x, int y)
        {
            int px = Cx + x, py = Cy + y;
            return px >= 0 && py >= 0 && px < W && py < H && _has[py * W + px];
        }

        protected void P(float fx, float fy, Color32 c, float z = 0f)
        {
            if (c.a == 0) return;   // a colour function's "not here" (the shell's other half)
            int px = Cx + Mathf.RoundToInt(fx), py = Cy + Mathf.RoundToInt(fy);
            if (px < 0 || py < 0 || px >= W || py >= H) return;
            int i = py * W + px;
            if (_has[i] && _z[i] > z) return;
            _col[i] = c; _z[i] = z; _has[i] = true;
        }

        /// <summary>An ellipse, coloured per texel by <paramref name="col"/> (texel x, y, and the
        /// normalised nx, ny - y up).</summary>
        protected void Blob(float cx, float cy, float rx, float ry, System.Func<int, int, float, float, Color32> col, float z = 0f)
        {
            int ex = Mathf.CeilToInt(rx), ey = Mathf.CeilToInt(ry);
            for (int dy = -ey; dy <= ey; dy++)
                for (int dx = -ex; dx <= ex; dx++)
                {
                    float nx = dx / rx, ny = dy / ry;
                    if (nx * nx + ny * ny > 1.02f) continue;
                    int x = Mathf.RoundToInt(cx) + dx, y = Mathf.RoundToInt(cy) + dy;
                    P(x, y, col(x, y, nx, ny), z);
                }
        }

        /// <summary>Lit from the top left.</summary>
        protected static System.Func<int, int, float, float, Color32> Shade(Color32[] r, float lo = 0f)
            => (x, y, nx, ny) => Ix(r, lo + (1f + nx * 0.8f - ny * 0.9f) * (r.Length - 1) / 2.6f);

        protected void Limb(Vector2 a, Vector2 b, float w, Color32[] r, float z)
        {
            int n = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f);
            for (int i = 0; i <= n; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)Mathf.Max(1, n));
                for (float dx = -w; dx <= w; dx += 0.5f)
                    for (float dy = -w; dy <= w; dy += 0.5f)
                        if (dx * dx + dy * dy <= w * w) P(p.x + dx, p.y + dy, r[dx > 0.3f ? 3 : dx < -0.3f ? 1 : 2], z);
            }
        }

        /// <summary>A small open flower: a pollen eye in a cross of petals.</summary>
        protected void Flower(float x, float y, Color32[] r, float z)
        {
            P(x, y, Pollen[1], z + 0.01f);
            P(x + 1, y, r[1], z); P(x - 1, y, r[1], z); P(x, y + 1, r[0], z); P(x, y - 1, r[2], z);
            P(x + 1, y + 1, r[2], z); P(x - 1, y - 1, r[3], z); P(x + 1, y - 1, r[3], z); P(x - 1, y + 1, r[2], z);
        }

        /// <summary>
        /// A LOTUS (the user's call): two rows of pointed petals rising in a cup - white at the base,
        /// pink to the tips - round a gold seed pod pitted with seeds. <paramref name="k"/> 0 shut ..
        /// 1 fully open; the petals lean out as it opens. (x, y) is the foot of the cup.
        /// </summary>
        protected void LotusAt(float x, float y, float size, float k, float t, float z)
        {
            if (k <= 0.02f) return;
            float open = Mathf.Lerp(0.15f, 1f, k);
            // the back row, then the front: each petal a pointed leaf along an angle from upright
            for (int row = 0; row < 2; row++)
            {
                int n = row == 0 ? 7 : 6;
                float len = size * (row == 0 ? 1f : 0.78f), wid = size * (row == 0 ? 0.24f : 0.22f);
                for (int i = 0; i < n; i++)
                {
                    float u = (i + (row == 0 ? 0f : 0.5f)) / (n - 1) - 0.5f;          // -0.5 .. 0.5 across
                    float ang = u * Mathf.PI * 1.15f * open + Mathf.Sin(t * 1.3f + i) * 0.03f;
                    var dir = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang));
                    var side = new Vector2(dir.y, -dir.x);
                    float zz = z + (row == 1 ? 0.2f : 0f) + (1f - Mathf.Abs(u)) * 0.05f;
                    int steps = Mathf.CeilToInt(len * 1.5f);
                    for (int s = 0; s <= steps; s++)
                    {
                        float v = s / (float)steps;
                        float w = wid * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Min(0.999f, 0.08f + v * 0.92f)), 0.8f) * (1f - v * 0.35f);
                        var c0 = new Vector2(x, y) + dir * (v * len) + Vector2.up * (row == 1 ? -1f : 0f);
                        for (float q = -w; q <= w; q += 0.5f)
                        {
                            bool rim = Mathf.Abs(q) > w - 0.8f;
                            var col = rim ? Lotus[4] : Ix(Lotus, v * 3.2f + (q > 0 ? 0.6f : 0f));
                            if (Mathf.Abs(q) < 0.4f && v > 0.15f && v < 0.85f) col = Lotus[Mathf.Min(4, Mathf.RoundToInt(v * 2f) + 1)];
                            P(c0.x + side.x * q, c0.y + side.y * q, col, zz);
                        }
                    }
                }
            }
            // the seed pod: a flat-topped gold cup with dark seed holes
            float pr = Mathf.Max(1.5f, size * 0.22f);
            Blob(x, y + size * 0.42f, pr, pr * 0.6f, (px, py, nx, ny) =>
                (Mathf.Abs(nx) < 0.75f && ny > -0.2f && ((px + py) & 1) == 0 && Hash(px, py, 7) > 0.45f) ? Pod[3] : Ix(Pod, 0.6f + nx * 0.8f - ny * 0.9f), z + 0.3f);
        }

        /// <summary>Outline every texel with the ink line, then lay the picture down.</summary>
        protected void Flush()
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (!_has[i]) continue;
                    if (x > 0 && !_has[i - 1]) Over(x - 1 - Cx, y - Cy, K, 0.9f);
                    if (x < W - 1 && !_has[i + 1]) Over(x + 1 - Cx, y - Cy, K, 0.9f);
                    if (y > 0 && !_has[i - W]) Over(x - Cx, y - 1 - Cy, K, 0.9f);
                    if (y < H - 1 && !_has[i + W]) Over(x - Cx, y + 1 - Cy, K, 0.9f);
                }
            for (int i = 0; i < _has.Length; i++)
                if (_has[i]) Put(i % W - Cx, i / W - Cy, _col[i]);
        }
    }

    /// <summary>
    /// One spot of the grove: nothing, a SAPLING (a stem and two leaves, swaying), or a TREE (a
    /// trunk and a round mossy crown with a few blossoms) - grown up from the sapling over
    /// Tuning.Viriditas.GrowSeconds when the stomp reaches it.
    /// </summary>
    public class GroveSprout : GroveCanvas
    {
        [SerializeField] Grove.State _state;
        [SerializeField] float _since;
        [SerializeField] int _seed;

        public static GroveSprout On(Transform spot, int seed)
        {
            var p = Attach<GroveSprout>(spot, "sprout", Vector2.zero, 20, 4, 44, 0);
            p._seed = seed;
            DepthSorted.Attach(spot.gameObject, 0f, false, p.GetComponent<SpriteRenderer>());
            return p;
        }

        public void Show(Grove.State s, bool instant)
        {
            _state = s;
            _since = instant ? -99f : Age;
        }

        protected override bool Draw()
        {
            if (_state == Grove.State.Empty) return false;
            Begin();
            float t = Age + _seed * 0.7f;
            float grow = Mathf.Clamp01((Age - _since) / Tuning.Viriditas.GrowSeconds);
            if (_state == Grove.State.Sapling)
            {
                float k = Mathf.Clamp01((Age - _since) / 0.3f);
                int h = Mathf.RoundToInt(2 + 6 * k);
                int sway = Mathf.Sin(t * 1.7f) > 0.6f ? 1 : 0;
                for (int y = 0; y <= h; y++) P(y > h - 2 ? sway : 0, y, Moss[y > h - 2 ? 1 : 2]);
                if (k > 0.5f)
                {
                    P(sway - 1, h, Moss[1]); P(sway - 2, h + 1, Moss[0]); P(sway + 1, h - 1, Moss[1]); P(sway + 2, h, Moss[0]);
                }
            }
            else
            {
                // a tree, shooting up out of the sapling (the stomp's picture)
                float k = Mathf.SmoothStep(0f, 1f, grow);
                int th = Mathf.RoundToInt(6 + k * 16), tw = k < 0.3f ? 0 : k < 0.7f ? 1 : 2;
                for (int y = 0; y <= th; y++)
                    for (int x = -tw; x <= tw; x++) P(x, y, Ix(Bark, 1f + (x + tw) / (2f * tw + 0.01f) * 2.4f));
                if (k > 0.6f) { P(-tw - 1, 0, Bark[3]); P(tw + 1, 0, Bark[3]); P(-tw - 2, 0, Bark[4]); }
                float rx = 3 + k * 11, ry = 3 + k * 8, cy = th + ry - 2;
                float sway = Mathf.Sin(t * 0.9f) * k * 0.6f;
                Blob(sway, cy, rx, ry, (x, y, nx, ny) =>
                {
                    if (Hash(x, y, 13 + _seed) > 0.95f) return Hash(x, y, 14) > 0.5f ? Lotus[1] : Pollen[1];
                    float v = 0.4f + (1f + nx * 0.8f - ny * 0.9f) * 1.6f + (Hash(x >> 1, y >> 1, _seed) > 0.75f ? 1f : 0f);
                    return Ix(Moss, v);
                }, 1f);
            }
            Flush();
            return true;
        }
    }

    /// <summary>
    /// The beetle knight (the user's picks, 2026-10-27): CHESTNUT HUSK - a chestnut shell with a burr
    /// crown and a dark seam down its back, pale mint plates down its sides, violet legs and a long
    /// curved horn. It curls into a SEED to roll. Flowers open in its seams as it grows stronger;
    /// beetles crawl round it while warded; its shell splits open, glowing gold, when it can be hurt;
    /// a LOTUS opens over it for the Bloom.
    ///
    /// Offsets from its feet, y up, drawn S times the mock's size.
    /// </summary>
    public abstract class ViriditasCanvas : GroveCanvas
    {
        const float S = 1.5f;
        static readonly Vector2[] Seams =
        {
            new(0, 22), new(0, 17), new(-9, 14), new(9, 14), new(-5, 20), new(5, 20),
            new(0, 12), new(-12, 10), new(12, 10), new(-3, 25), new(3, 25), new(0, 8),
        };

        protected void Figure(ViriditasLook o, float t)
        {
            Begin();
            // the contact shadow
            float sh = o.Curl >= 0.5f ? 12f : 20f;
            for (int y = -3; y <= 3; y++)
                for (int x = -22; x <= 22; x++)
                    if (x * x / (sh * sh) + y * y / 6f <= 1f) Over(x, y, K, 0.4f);

            if (o.Curl >= 0.5f) Seed(o);
            else Beetle(o, t);
            if (o.Ward > 0f) Beetles(o.Ward, t);
            float lotus = Mathf.Max(o.Bloom, o.Flowered ? 0.35f : 0f);
            if (lotus > 0f && o.Curl < 0.5f) LotusAt(0, 37 * S - (o.Curl * 9f), 12f + 16f * lotus, Mathf.Clamp01(lotus * 1.4f), t, 3f);
            if (o.Dazed)
                for (int k = 0; k < 3; k++)
                {
                    float a = t * 5f + k * 2.1f;
                    float x = Mathf.Cos(a) * 10f, y = (o.Curl >= 0.5f ? 40f : 52f) + Mathf.Sin(a) * 2f;
                    P(x, y, Pollen[0], 5f); P(x + 1, y, Pollen[2], 5f); P(x - 1, y, Pollen[2], 5f); P(x, y + 1, Pollen[2], 5f); P(x, y - 1, Pollen[2], 5f);
                }
            Flush();
        }

        void Seed(ViriditasLook o)
        {
            float r = 12f * S, ca = Mathf.Cos(o.Roll), sa = Mathf.Sin(o.Roll);
            int e = Mathf.CeilToInt(r);
            for (int dy = -e; dy <= e; dy++)
                for (int dx = -e; dx <= e; dx++)
                {
                    if (dx * dx + dy * dy > (r + 0.3f) * (r + 0.3f)) continue;
                    float rx = dx * ca + dy * sa, ry = -dx * sa + dy * ca;
                    int irx = Mathf.RoundToInt(rx), iry = Mathf.RoundToInt(ry);
                    Color32 c;
                    if (ry > 3f) c = ((((irx + iry) % 3) + 3) % 3 == 0 || (((irx - iry) % 3) + 3) % 3 == 0) ? Husk[4] : Husk[3];
                    else c = Ix(Husk, 0.8f + dx / r * 1.2f - dy / r * 1.3f);
                    if (Mathf.Abs(ry - 3f) < 0.7f) c = Plate[1];
                    if (o.Husk > 0f) c = Color32.Lerp(c, Husk[4], o.Husk * 0.7f);
                    P(dx, dy + r, c);
                }
            // the stem, turning with it
            float sx = -sa * (r + 1f), sy = ca * (r + 1f) + r;
            P(sx, sy, Husk[3], 1f); P(sx - sa, sy + ca, Husk[4], 1f);
            // its death: a sapling grows out of the top and opens a lotus
            if (o.Sprout > 0f)
            {
                int h = Mathf.RoundToInt(o.Sprout * 26f);
                for (int i = 0; i < h; i++)
                {
                    float x = Mathf.Round(Mathf.Sin(i * 0.35f) * 1.2f), y = 2f * r + i;
                    P(x, y, Moss[2], 2f);
                    if (i % 6 == 3 && i < h - 2) { P(x + 1, y, Moss[1], 2f); P(x + 2, y + 1, Moss[0], 2f); P(x - 1, y + 2, Moss[1], 2f); P(x - 2, y + 3, Moss[0], 2f); }
                }
                if (o.Sprout > 0.85f) LotusAt(0, 2f * r + h - 1, 9f, (o.Sprout - 0.85f) / 0.15f, Age, 3f);
            }
        }

        void Beetle(ViriditasLook o, float t)
        {
            float dn = o.Curl * 6f * S, rear = o.Rear * 6f, split = o.Split;
            float hd = -dn * 0.5f + rear;
            int sL = o.Walk && Mathf.Sin(t * 9f) > 0f ? 1 : 0, sR = o.Walk && sL == 0 ? 1 : 0;
            // six legs: three pairs; clamped forward round the player in the grab
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 3; k++)
                {
                    float lx = new[] { 8f, 12f, 15f }[k] * S;
                    float lift = (k % 2 == 1 ? sL : sR) * 2f;
                    var hip = new Vector2(s * lx * 0.6f, 9f * S - dn + rear * (k == 0 ? 1f : 0.5f));
                    var foot = new Vector2(s * (lx + 2f * S - o.Grab * (k == 0 ? 12f : 6f)), lift + o.Grab * (k == 0 ? 10f : 0f));
                    Limb(hip, foot, 0.9f, Horn, -0.1f);
                }

            // the shell: two halves, sliding apart when it splits open
            float ox = split * 7f, cyS = (15f * S) - dn + rear;
            for (int half = -1; half <= 1; half += 2)
            {
                float hx = half * ox;
                Blob(hx, cyS, 14f * S, 10f * S, (x, y, nx, ny) =>
                {
                    float lx = (x - hx) / S, ly = (y - cyS) / S;
                    if ((half < 0 && lx > 0.5f) || (half > 0 && lx < -0.5f)) return default;   // the other half's
                    if (Mathf.Abs(lx) < 0.7f) return Husk[4];
                    if (ly > 7f) return (((x + y) & 1) == 0) ? Husk[4] : Husk[3];
                    float side = lx < 0 ? lx + 7f : lx - 7f;
                    return Hash(x, y, 6) > 0.9f ? Husk[3] : Ix(Husk, 1.2f + side / 7f * 1.1f - ly / 10f * 1.2f);
                }, 0f);
            }
            // the burr on the crown
            for (int k = -5; k <= 5; k++)
            {
                float x = k * 2.4f * S, top = cyS + 10f * S * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / (14f * S)) * (x / (14f * S))));
                P(x + (x < 0 ? -ox : ox), top + 1f, Husk[4], 0.2f);
            }
            // the gold body inside, when it splits open
            if (split > 0.15f)
                for (float y = 6f * S; y <= 22f * S; y++)
                    for (float x = -ox + 1f; x <= ox - 1f; x++)
                        P(x, y - dn + rear, Mathf.Abs(x) < ox * 0.5f ? Pollen[1] : Pollen[2], -0.05f + 0.4f);
            // pale plates down its sides
            for (float y = 6f; y <= 14f; y++)
                for (float x = -11f; x <= 11f; x++)
                {
                    float ax = Mathf.Abs(x);
                    if (ax <= 6f || ax >= 12f - (14f - y) * 0.4f) continue;
                    P((x + (x < 0 ? -ox / S : ox / S)) * S, y * S - dn + rear, Ix(Plate, 0.4f + (14f - y) / 8f * 2.4f), 0.25f);
                }
            // flowers in the seams (the user's pick) - two for every sapling it has broken
            for (int i = 0; i < Mathf.Min(o.Flowers, Seams.Length); i++)
                Flower(Seams[i].x * S + (Seams[i].x < 0 ? -ox : Seams[i].x > 0 ? ox : 0f), Seams[i].y * S - dn + rear, i % 2 == 0 ? Lotus : Plate, 2f);

            if (split < 0.6f)
            {
                // the head, low at the front, and the horn sweeping up over the shell
                float hy = 7f * S + hd;
                Blob(0, hy, 5f * S, 3.5f * S, Shade(Horn, 0.6f), 0.5f);
                var eye = Pollen[o.Dazed ? 2 : 1];
                P(-3, hy + 1, eye, 0.6f); P(3, hy + 1, eye, 0.6f);
                float hl = (16f + o.Rear * 4f) * S;
                for (int i = 0; i <= hl; i++)
                {
                    float v = i / hl, x = Mathf.Sin(v * 2.4f) * 5f * S * v, y = hy + 3f + i + o.Rear * 4f * v;
                    float w = v < 0.6f ? 1.5f : v < 0.85f ? 1f : 0f;
                    for (float q = -w; q <= w; q += 0.5f) P(x + q, y, Horn[q < 0 ? 0 : q > 0 ? 2 : 1], 0.7f);
                }
                for (int s = -1; s <= 1; s += 2) { P(s * 7, hy + 6, Horn[1], 0.7f); P(s * 9, hy + 7, Horn[0], 0.7f); }
            }
        }

        /// <summary>The ward (the user's pick): small beetles crawling round and round its body.</summary>
        void Beetles(float k, float t)
        {
            int n = Mathf.RoundToInt(16 * k);
            for (int i = 0; i < n; i++)
            {
                float a = t * (0.7f + Hash(i, 1) * 0.5f) + i * 0.9f, s = Mathf.Sin(a);
                float x = Mathf.Cos(a) * (19f + Hash(i, 2) * 7f), y = 6f + Hash(i, 3) * 44f;
                float z = s > 0f ? 4f : -1f;
                P(x, y, Plate[1], z); P(x + 1, y, Plate[2], z); P(x, y - 1, Plate[2], z); P(x + 1, y - 1, Plate[3], z);
                P(x, y + 1, Horn[2], z); P(x + 1, y + 1, Horn[2], z);
                int leg = Mathf.FloorToInt(t * 12f + i) % 2;
                P(x - 1, y - 1 + leg, K, z); P(x + 2, y - leg, K, z);
            }
        }
    }

    public class ViriditasFigure : ViriditasCanvas
    {
        public ViriditasLook Look;

        protected override bool Draw()
        {
            Figure(Look, Age);
            return true;
        }
    }

    /// <summary>
    /// Its death (the user's pick, "a seed again"): it curls into its seed, the husk darkens, and a
    /// sapling grows out of it and opens a small lotus. Its own object, lying there until the floor ends.
    /// </summary>
    public class ViriditasDeath : ViriditasCanvas
    {
        [SerializeField] ViriditasLook _last;

        public static void At(Vector2 at, ViriditasLook last)
        {
            var go = new GameObject("viriditas.death");
            if (GameBootstrap.FloorRoot != null) go.transform.SetParent(GameBootstrap.FloorRoot, false);
            go.transform.position = at;
            var p = Attach<ViriditasDeath>(go.transform, "figure", Vector2.zero, 44, 8, 100, 0);
            last.Ward = 0f; last.Dazed = false; last.Bloom = 0f; last.Grab = 0f; last.Walk = false; last.Split = 0f;
            p._last = last;
            DepthSorted.Attach(go, 0f, false, p.GetComponent<SpriteRenderer>());
        }

        protected override bool Draw()
        {
            var o = _last;
            o.Curl = Mathf.Clamp01(Age / 0.4f) < 1f ? Mathf.Clamp01(Age / 0.4f) * 0.49f : 1f;
            o.Husk = Mathf.Clamp01((Age - 1.0f) / 1.0f);
            o.Sprout = Mathf.Clamp01((Age - 1.1f) / 1.6f);
            Figure(o, Mathf.Min(Age, 3.5f));
            return true;
        }
    }
}
