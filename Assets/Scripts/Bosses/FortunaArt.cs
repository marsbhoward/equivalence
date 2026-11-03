using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// What Fortuna looks like at one moment - everything the drawing reads, set by her fight each frame.
    /// Plain fields, so a domain reload keeps them.
    /// </summary>
    [System.Serializable]
    public struct FortunaLook
    {
        /// <summary>The band slipping off the cat eye (a tell), 0..1; her right hand coming up to push it back, 0..1.</summary>
        public float Slip, Hand;
        /// <summary>The enrage: the band falling to the floor, the cape crumpling there (both stay), 0..1.</summary>
        public float Fall, CapeDrop;
        /// <summary>How broken the dark plate is (EnemyBands.FortunaTatter), 0..1.</summary>
        public float Tatter;
        /// <summary>The top bands' enrage: the long panel gone, the belt a bikini.</summary>
        public bool Bikini;
        /// <summary>The tell playing (FortunaTell), how far into it 0..1, and whether it is a FAKE (no glint).</summary>
        public int Tell;
        public float TellAmt;
        public bool Fake;
        /// <summary>The tail's mood (FortunaMood).</summary>
        public int Mood;
        /// <summary>The Bluff's wind-up: her cat-side arm raised 0..1, its claws out (a REAL blow's honest tell) 0..1.</summary>
        public float Raise, Claws;
        /// <summary>The time her idle runs on - faster when enraged at high bands.</summary>
        public float Clock;
        /// <summary>The window: she sags, open.</summary>
        public float Open;
    }

    public static class FortunaTell { public const int None = 0, Slip = 1, Ear = 2, Mask = 3, Sleeve = 4, Serpent = 5, Smirk = 6; }
    public static class FortunaMood { public const int Sway = 0, Alert = 1, Flick = 2, Lash = 3, Low = 4, Puff = 5; }

    /// <summary>
    /// Fortuna (the user's picks over fourteen rounds, 2026-11-02 - BOSSES.md "Fortuna"): a slender cat-woman, black
    /// fur, brown skin down her front, her face split down the middle - human on her right, cat on her left. Drawn at
    /// WEAPON density (75 px/unit, the user's call for boss figures), about 116 texels to the crown, as tall as Icarus.
    ///
    /// Authored in BODY texels (as the mockup was), y DOWN and up negative, feet at 0; every point goes through
    /// <see cref="Put"/>, which doubles it onto the 75 grid and flips y. The outline is one body texel thick (two here):
    /// an outline is a world size, not a texel count. Ported from the mockup's figure code, the same shapes and numbers.
    /// </summary>
    public abstract class FortunaCanvas : TexelPicture
    {
        public const float Ppu75 = 75f;
        protected const int SC = 2;
        const float ST = 0.5f;

        static Color32 C(int hex) => new((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        static Color32[] R(params int[] h) { var r = new Color32[h.Length]; for (int i = 0; i < h.Length; i++) r[i] = C(h[i]); return r; }

        protected static readonly Color32 K = C(0x0a0609), White = C(0xffffff);
        public static readonly Color32[] Fur = R(0x5c536c, 0x3a3448, 0x25202f, 0x15121c);
        public static readonly Color32[] Skin = R(0xcf9c70, 0xa8724a, 0x7a4e30, 0x4e3020);
        public static readonly Color32[] Gold = R(0xfff2c0, 0xf2c35c, 0xc88a2a, 0x7a4a14);
        public static readonly Color32[] GoldP = R(0xfff2c0, 0xf2c35c, 0xc88a2a, 0x6e420e);
        public static readonly Color32[] Crim = R(0xff8a8a, 0xe03a48, 0xa81c30, 0x5e0e1c);
        public static readonly Color32[] Satin = R(0x5a4c68, 0x33293e, 0x1c1624, 0x0f0b14);
        public static readonly Color32[] Linen = R(0xfbf7ee, 0xe8e0cc, 0xc4b898, 0x8e8466);
        public static readonly Color32[] Lapis = R(0x5a8ae0, 0x2a5ab0, 0x183a7a);
        public static readonly Color32[] Turq = R(0x7ae0d8, 0x3ab0b0, 0x1e7a7a);
        public static readonly Color32[] Carn = R(0xf07a50, 0xc04a30, 0x7a2a18);
        public static readonly Color32[] DarkP = R(0xa4a0c4, 0x6e6a90, 0x4a4864, 0x2a2838);
        public static readonly Color32[] Navy = R(0x5a6aaa, 0x2e3e7a, 0x1a2450);
        public static readonly Color32[] Cape = R(0xffffff, 0xeeeae0, 0xcfc8b8, 0x9a9282);
        public static readonly Color32[] Strap = R(0xa8724a, 0x7a4a2a, 0x4e2e1a);
        public static readonly Color32[] Viol = R(0xa07ad8, 0x6a44a8, 0x3e2470, 0x22123e);
        public static readonly Color32[] CardFace = R(0xffffff, 0xece4d8, 0xb8ab9a);
        static readonly Color32[] Lacq = R(0x8a8298, 0x6a6278, 0x4a4458);
        static readonly Color32[] Lip = R(0xe8949a, 0xc0606c, 0x8a3a48);
        static readonly Color32 Pink = C(0xf2a0b4), Nose = C(0xe07a94), Eye = C(0xffd24a), Tear = C(0x9ad0ff), Silver = C(0x8a8494), Mouth = C(0x5a1a24);

        // the build (the user's picks): slender, the back and waist slimmer still, the hips wider, the fuller bust
        const float Sh = 0.71f, Wa = 0.68f, Hp = 0.97f, Limb = 0.74f, Bump = 0.55f;
        static readonly float[] ProfY = { -47, -46, -45, -44, -42, -40, -38, -36, -35, -33, -31, -29, -27, -25, -23 };
        static readonly float[] ProfW = { 1.8f, 2.4f, 5.8f, 7.7f, 7.5f, 6.9f, 6.9f, 6.3f, 5.7f, 5.1f, 5.1f, 5.9f, 7.1f, 7.5f, 7.0f };

        // ------------------------------------------------------------------ the buffer (fine texels)

        Color32[] _col; float[] _z, _a; bool[] _has;
        System.Func<float, float, Vector3> _shift;   // (dx, dy, alpha) for a piece drawn moved

        protected void Begin()
        {
            int n = W * H;
            if (_col == null || _col.Length != n) { _col = new Color32[n]; _z = new float[n]; _a = new float[n]; _has = new bool[n]; }
            System.Array.Clear(_has, 0, n);
        }

        int Index(int fx, int fy)
        {
            int px = Cx + fx, py = Cy - fy;   // y down in the drawing, up in the texture
            if (px < 0 || py < 0 || px >= W || py >= H) return -1;
            return py * W + px;
        }

        /// <summary>A fine texel (75 grid), y down.</summary>
        protected void PutF(int fx, int fy, Color32 c, float z, float a = 1f)
        {
            if (c.a == 0 || a <= 0f) return;
            int i = Index(fx, fy);
            if (i < 0) return;
            if (_has[i] && _z[i] > z) return;
            _col[i] = c; _z[i] = z; _a[i] = a; _has[i] = true;
        }

        /// <summary>A body-texel point (37.5 grid), y down.</summary>
        protected void Put(float x, float y, Color32 c, float z, float a = 1f)
        {
            if (_shift != null) { var s = _shift(x, y); x += s.x; y += s.y; a *= s.z; }
            PutF(Mathf.RoundToInt(x * SC), Mathf.RoundToInt(y * SC), c, z, a);
        }

        protected bool Get(float x, float y, out Color32 c, out float z)
        {
            int i = Index(Mathf.RoundToInt(x * SC), Mathf.RoundToInt(y * SC));
            c = default; z = 0f;
            if (i < 0 || !_has[i]) return false;
            c = _col[i]; z = _z[i];
            return true;
        }

        /// <summary>Outline every texel two deep with the ink line (one body texel), then lay the picture down.</summary>
        protected void Flush()
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (!_has[i] || _a[i] < 0.8f) continue;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            if ((dx == 0 && dy == 0) || Mathf.Abs(dx) + Mathf.Abs(dy) > 2) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= W || ny >= H || _has[ny * W + nx]) continue;
                            Over(nx - Cx, ny - Cy, K, 0.85f);
                        }
                }
            for (int i = 0; i < _has.Length; i++)
                if (_has[i])
                {
                    if (_a[i] >= 0.999f) Put(i % W - Cx, i / W - Cy, _col[i]);
                    else Over(i % W - Cx, i / W - Cy, _col[i], _a[i]);
                }
        }

        // ------------------------------------------------------------------ primitives (body texels, y down)

        protected static float Hash(int a, int b = 0, int c = 0) => FireArt.Hash(a, b, c);
        protected static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        protected static float Ease(float t, float a, float b) => Smooth((t - a) / (b - a));
        protected static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);
        static Color32 Sh3(Color32[] r, float rel) => r[rel < -0.45f ? 0 : rel > 0.45f ? 2 : 1];
        static Color32 Metal(Color32[] r, float u, float v, float d) => d > 0.82f ? r[3] : u + v < -0.75f ? r[0] : u + v > 0.55f ? r[2] : r[1];
        static int FX(float x) => Mathf.RoundToInt(x * SC);

        delegate Color32 StrokeCol(float x, float y, float u, float s);
        delegate Color32 DiscCol(float u, float v, float x, float y);

        void Stroke(float x0, float y0, float x1, float y1, System.Func<float, float> w, StrokeCol col, float z, float a = 1f)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) * 2f * SC));
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, x = x0 + (x1 - x0) * u, y = y0 + (y1 - y0) * u, ww = w(u), cw = Mathf.Ceil(ww);
                for (float dy = -cw; dy <= cw; dy += ST)
                    for (float dx = -cw; dx <= cw; dx += ST)
                        if (dx * dx + dy * dy <= ww * ww + 0.25f / SC)
                            Put(x + dx, y + dy, col(Mathf.Round((x + dx) * SC) / SC, Mathf.Round((y + dy) * SC) / SC, u, dx / Mathf.Max(0.5f, ww)), z, a);
            }
        }
        void Stroke(float x0, float y0, float x1, float y1, float w, StrokeCol col, float z) => Stroke(x0, y0, x1, y1, _ => w, col, z);

        void Disc(float cx, float cy, float rx, float ry, DiscCol col, float z, float a = 1f)
        {
            float ex = Mathf.Ceil(rx), ey = Mathf.Ceil(ry);
            for (float dy = -ey; dy <= ey; dy += ST)
                for (float dx = -ex; dx <= ex; dx += ST)
                    if (dx * dx / (rx * rx) + dy * dy / (ry * ry) <= 1.05f) Put(cx + dx, cy + dy, col(dx / rx, dy / ry, cx + dx, cy + dy), z, a);
        }

        void Poly(Vector2[] pts, System.Action<float, float> fn, float step = ST)
        {
            float x0 = 1e9f, x1 = -1e9f, y0 = 1e9f, y1 = -1e9f;
            foreach (var p in pts) { x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y); }
            for (float y = Mathf.Floor(y0); y <= Mathf.Ceil(y1); y += step)
                for (float x = Mathf.Floor(x0); x <= Mathf.Ceil(x1); x += step)
                {
                    bool ins = false;
                    for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                    {
                        float xi = pts[i].x, yi = pts[i].y, xj = pts[j].x, yj = pts[j].y;
                        if (((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi)) ins = !ins;
                    }
                    if (ins) fn(x, y);
                }
        }

        static Vector2 Ik(Vector2 a, Vector2 b, float l1, float l2)
        {
            var d = b - a; float m = d.magnitude, reach = l1 + l2 - 0.01f;
            if (m > reach) { b = a + d / m * reach; d = b - a; m = reach; }
            m = Mathf.Max(m, Mathf.Abs(l1 - l2) + 0.01f);
            float ca = (l1 * l1 + m * m - l2 * l2) / (2f * l1 * m), bs = Mathf.Atan2(d.y, d.x), off = Mathf.Acos(Mathf.Clamp(ca, -1f, 1f));
            var e1 = a + new Vector2(Mathf.Cos(bs + off), Mathf.Sin(bs + off)) * l1;
            var e2 = a + new Vector2(Mathf.Cos(bs - off), Mathf.Sin(bs - off)) * l1;
            return Mathf.Abs(e1.x) > Mathf.Abs(e2.x) ? e1 : e2;   // the elbow out, away from the body
        }

        static float BuildAt(float y)
        {
            if (y <= -40f) return Sh;
            if (y <= -33f) return Mathf.Lerp(Sh, Wa, (y + 40f) / 7f);
            if (y <= -29f) return Wa;
            if (y <= -25f) return Mathf.Lerp(Wa, Hp, (y + 29f) / 4f);
            return Hp;
        }
        static float Prof0(float y)
        {
            if (y <= ProfY[0]) return ProfW[0];
            for (int i = 1; i < ProfY.Length; i++) if (y <= ProfY[i]) return Mathf.Lerp(ProfW[i - 1], ProfW[i], (y - ProfY[i - 1]) / (ProfY[i] - ProfY[i - 1]));
            return ProfW[ProfW.Length - 1];
        }
        static float Prof(float y) => Prof0(y) * BuildAt(y) + (y > -41f && y < -35.5f ? Bump * Mathf.Exp(-((y + 38.4f) / 1.6f) * ((y + 38.4f) / 1.6f)) : 0f);

        // ------------------------------------------------------------------ the figure

        FortunaLook _o;
        float _t, _breathe, _hipX;
        Vector2[] _hip = new Vector2[2], _knee = new Vector2[2], _foot = new Vector2[2];

        float TellOf(int tell) => _o.Tell == tell ? _o.TellAmt : 0f;

        /// <summary>The whole figure for <paramref name="o"/>, laid down and flushed.</summary>
        protected void Figure(FortunaLook o)
        {
            _o = o; _t = o.Clock;
            Begin();
            _breathe = Mathf.Sin(_t * 2.2f) * 0.35f * (1f - o.Open * 0.5f);
            float sway = Mathf.Sin(_t * 1.1f);
            _hipX = sway * 0.4f;
            _hip ??= new Vector2[2]; _knee ??= new Vector2[2]; _foot ??= new Vector2[2];

            Tail();
            Hair();
            Legs();
            Torso();
            Head();
            Arms();
            CapeB();
            LegsD();
            WaistB();
            TopA();
            BandAndMask();
            if (o.Tatter > 0f) TatterPass(o.Tatter);
            Flush();
        }

        // ---- the tail: a chain, a wave travelling base to tip; its MOOD follows the fight ----
        struct Mood { public float Base, Turn, Curl, Amp, W, Lag; public bool Tw, Flick, Puff; }
        static Mood MoodOf(int m) => m switch
        {
            FortunaMood.Alert => new Mood { Base = -0.2f, Turn = -1.5f, Curl = -1.7f, Amp = 0.03f, W = 0.9f, Lag = 0.2f },
            FortunaMood.Flick => new Mood { Base = 0.45f, Turn = -2.25f, Curl = -0.25f, Amp = 0.08f, W = 1.3f, Lag = 0.28f, Flick = true },
            FortunaMood.Lash => new Mood { Base = 0.2f, Turn = -1.8f, Curl = -0.3f, Amp = 0.34f, W = 5.2f, Lag = 0.22f },
            FortunaMood.Low => new Mood { Base = 0.75f, Turn = 1.6f, Curl = 0.5f, Amp = 0.07f, W = 0.8f, Lag = 0.3f },
            FortunaMood.Puff => new Mood { Base = -0.1f, Turn = -1.4f, Curl = 0.15f, Amp = 0.02f, W = 8f, Lag = 0.1f, Puff = true },
            _ => new Mood { Base = 0.45f, Turn = -2.25f, Curl = -0.25f, Amp = 0.10f, W = 1.3f, Lag = 0.28f, Tw = true },
        };

        void Tail()
        {
            const int n = 24; const float len = 45f;
            var M = MoodOf(_o.Mood);
            float seg = len / n, per = 3.1f; int k = Mathf.FloorToInt(_t / per); float ph = (_t - k * per) / 0.35f;
            float twitch = M.Tw && Hash(k, 5) > 0.35f && ph < 1f ? Mathf.Sin(ph * Mathf.PI) * 0.9f : 0f;
            float fl = M.Flick ? Mathf.Sin(Mathf.Clamp01((Mathf.Repeat(_t, 1.6f) - 0.6f) / 0.45f) * Mathf.PI) * 1.6f : 0f;
            var pts = new Vector2[n + 1];
            float a = M.Base, x = 0f, y = 0f;
            pts[0] = Vector2.zero;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                a += M.Turn / n + (u > 0.62f ? M.Curl / (n * 0.38f) : 0f);
                a += M.Amp * Mathf.Sin(_t * M.W - i * M.Lag) * (0.4f + u) / n * 6f;
                if (u > 0.66f) a += (twitch + fl) / (n * 0.34f) * (u > 0.85f ? 1.3f : 0.7f);
                x += Mathf.Cos(a) * seg; y += Mathf.Sin(a) * seg;
                pts[i + 1] = new Vector2(x, y);
            }
            var b = new Vector2(3f, -24f);
            float puff = M.Puff ? 1f : 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float w = Mathf.Lerp(1.5f, 2.2f, Mathf.Sin(u * Mathf.PI) * 0.8f + u * 0.2f) * (1f - u * 0.25f) * (1f + puff * (0.8f + 0.35f * Hash(i, 3)));
                Stroke(b.x + pts[i].x, b.y + pts[i].y, b.x + pts[i + 1].x, b.y + pts[i + 1].y, _ => w,
                       (px, py, v, s) => Fur[s < -0.3f ? 0 : s > 0.4f ? 2 : 1], -0.4f);
            }
            SerpentHead(b, pts);
        }

        /// <summary>The tail ends in a gold serpent head (the user's pick, after reference 1). Its tell: a red eye and a forked tongue.</summary>
        void SerpentHead(Vector2 b, Vector2[] pts)
        {
            int n = pts.Length - 1;
            var d = pts[n] - pts[n - 1]; float l = Mathf.Max(1e-4f, d.magnitude); float ux = d.x / l, uy = d.y / l;
            float hx = b.x + pts[n].x + ux * 0.6f, hy = b.y + pts[n].y + uy * 0.6f, tell = TellOf(FortunaTell.Serpent);
            Disc(hx, hy, 1.2f, 1.2f, (u, v, x, y) => Metal(GoldP, u, v, Mathf.Sqrt(u * u + v * v)), -0.3f);
            Stroke(hx + ux * 0.8f, hy + uy * 0.8f, hx + ux * 4f, hy + uy * 4f, u => Mathf.Lerp(0.7f, 0.12f, u), (x, y, u, q) => GoldP[q < 0 ? 0 : 2], -0.3f);
            Put(hx - uy * 0.5f, hy + ux * 0.5f, tell > 0.3f ? Crim[0] : Crim[1], -0.2f);
            if (tell > 0.2f)
            {
                float f = Mathf.Sin(_t * 22f) > 0f ? 1f : 0.6f, tx = hx + ux * 4.4f, ty = hy + uy * 4.4f;
                Stroke(tx, ty, tx + ux * 1.8f * f, ty + uy * 1.8f * f, 0.22f, (x, y, u, q) => Crim[1], -0.25f);
                for (int q = -1; q <= 1; q += 2)
                    Stroke(tx + ux * 1.8f * f, ty + uy * 1.8f * f, tx + ux * 2.6f * f - uy * q * 0.6f, ty + uy * 2.6f * f + ux * q * 0.6f, 0.18f, (x, y, u, s) => Crim[1], -0.25f);
                Glint(FX(hx - uy * 0.5f), FX(hy + ux * 0.5f), (tell - 0.2f) / 0.8f, 3);
            }
        }

        /// <summary>Every REAL tell glints (gold, a star with a twinkling tip); a fake never does.</summary>
        void Glint(int fx, int fy, float g, int n = 4)
        {
            if (g <= 0f || _o.Fake) return;
            float tw = 0.6f + 0.4f * Mathf.Sin(_t * 18f);
            PutF(fx, fy, White, 6.6f, g);
            for (int k = 1; k <= n; k++)
            {
                var c = k < 2 ? Gold[0] : Gold[1]; float a = g * (k < n ? 1f : tw);
                PutF(fx + k, fy, c, 6.5f, a); PutF(fx - k, fy, c, 6.5f, a); PutF(fx, fy + k, c, 6.5f, a); PutF(fx, fy - k, c, 6.5f, a);
            }
        }

        void Hair()
        {
            var pts = new[] { new Vector2(-5.6f, -58), new Vector2(5.6f, -58), new Vector2(7.2f * Sh, -50), new Vector2(7.6f * Sh, -41 + _breathe), new Vector2(6.8f * Sh, -38.5f), new Vector2(5.4f * Sh, -37),
                              new Vector2(-5.4f * Sh, -37), new Vector2(-6.8f * Sh, -38.5f), new Vector2(-7.6f * Sh, -41 + _breathe), new Vector2(-7.2f * Sh, -50) };
            Poly(pts, (x, y) =>
            {
                bool strand = Mathf.RoundToInt((x * 1.3f + Mathf.Sin(y * 0.4f) * 0.8f) * SC) % 3 == 0;
                Put(x, y, Fur[x > 3f ? 2 : strand ? 0 : 1], -2f);
            });
        }

        void Legs()
        {
            _hip[0] = new Vector2(-3.9f * Hp + _hipX, -24); _knee[0] = new Vector2(-3.6f * Hp, -13); _foot[0] = new Vector2(-3.4f * Hp, -1);
            _hip[1] = new Vector2(3.9f * Hp + _hipX, -24); _knee[1] = new Vector2(2.1f * Hp, -13.3f); _foot[1] = new Vector2(1.8f * Hp, -1);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                StrokeCol col = (x, y, u, s) => Fur[Rel(s * 0.9f + 0.2f * side)];
                Stroke(_hip[i].x, _hip[i].y, _knee[i].x, _knee[i].y, u => (Mathf.Lerp(3.6f, 2.1f, u) + Mathf.Sin(u * Mathf.PI) * 0.25f) * Limb, col, 0f);
                Stroke(_knee[i].x, _knee[i].y, _foot[i].x, _foot[i].y, u => (1.9f + Mathf.Sin(Mathf.Clamp01(u * 1.4f) * Mathf.PI) * 0.55f - u * 0.95f) * Limb, col, 0f);
            }
        }
        static int Rel(float rel) => rel < -0.5f ? 0 : rel > 0.5f ? 2 : 1;

        static bool IsSkin(float x, float y, float rel)
        {
            if (y < -45f || y > -24f) return false;
            float j = (Hash(FX(x), FX(y), 31) - 0.5f) * 0.18f;
            return Mathf.Abs(rel) < 0.64f + j && y > -44.5f + (Mathf.Abs(rel) > 0.4f ? 1f : 0f);
        }

        void Torso()
        {
            var SR = Skin;
            for (float y = -47f; y <= -23f; y += ST)
            {
                float yy = y - (y < -34f ? _breathe : 0f), hw = Prof(y), cx = _hipX * Mathf.Clamp01((y + 34f) / 10f);
                for (float x = Mathf.Floor(-hw); x <= Mathf.Ceil(hw); x += ST)
                {
                    if (Mathf.Abs(x) > hw + 0.25f) continue;
                    float rel = x / hw; bool sk = IsSkin(x, y, rel);
                    var c = sk ? SR[Rel(rel)] : Fur[Rel(rel)];
                    if (sk)
                    {
                        float ax = Mathf.Abs(x);
                        if (y >= -41.6f && y <= -36.4f && ax < 0.3f) c = SR[y < -40.6f ? 2 : 3];
                        if (y == -35f && ax <= 4f) c = SR[2];
                        if (y == -35.5f && ax <= 3f && ax >= 1f) c = SR[rel < 0 ? 1 : 2];
                        if (Mathf.Abs(y + 29f) < 0.3f && ax < 0.3f) c = SR[3];
                        if (Mathf.Abs(y + 29.5f) < 0.3f && ax < 0.3f) c = SR[2];
                        if (y == -44f && ax >= 1f && ax <= 4.5f) c = SR[2];
                        if (y > -34f && y < -30f && ax < 0.3f) c = SR[2];
                        if ((y == -32.5f || y == -31f) && ax >= 0.5f && ax <= 2f) c = SR[rel < 0 ? 1 : 2];
                        if (y > -33.5f && y < -27f && Mathf.Abs(ax - (3.3f - (y + 33.5f) * 0.17f)) < 0.3f) c = SR[2];
                    }
                    Put(x + cx, yy, c, 1f);
                }
            }
            // fur tufts on the silhouette
            float[,] tufts = { { -8 * Sh, -43, -1 }, { 8 * Sh, -43, 1 }, { -8.4f * Hp, -25, -1 }, { 8.4f * Hp, -25, 1 }, { -5 * Hp, -15, -1 }, { -7.4f * Wa, -33, -1 }, { 3.9f * Hp, -8, 1 } };
            for (int i = 0; i < tufts.GetLength(0); i++)
            {
                float x = tufts[i, 0], y = tufts[i, 1], d = tufts[i, 2];
                if (Hash(FX(x), FX(y), Mathf.FloorToInt(_t * 0.5f)) > 0.2f) Stroke(x, y, x + d * 1.1f, y - 0.6f, 0.3f, (a, b, u, s) => Fur[d < 0 ? 1 : 2], 0f);
            }
        }

        void Head()
        {
            Stroke(0, -46, 0, -49, 1.6f, (x, y, u, s) => x < 0 ? Skin[1] : Fur[x > 0 ? 2 : 1], 1f);
            const float hx = 0f, hy = -52.5f;
            // half human, half cat, split down the middle: skin and a jaw on her right (viewer's left), fur on her left
            Disc(hx, hy, 4.8f, 4.6f, (u, v, x, y) =>
            {
                float split = Mathf.Round(Mathf.Sin(y * 1.7f) * 0.6f) / SC, by = y - hy;
                if (x < split)
                {
                    if (by > 2.2f && -x > 4.7f - (by - 2.2f) * 1.6f) return default;   // the human jaw, trimmed
                    return Skin[u < -0.55f ? 0 : v > 0.55f ? 2 : 1];
                }
                return Fur[Rel(u)];
            }, 3f);
            for (int i = 0; i < 2; i++)
            {
                float a = i == 0 ? 0.8f : 1.6f, b = i == 0 ? 1.2f : 2.2f, l = i == 0 ? 1.6f : 1.2f;
                Stroke(hx + 4.2f, hy + a, hx + 4.2f + l, hy + b, 0.45f, (x, y, u, s) => Fur[2], 3f);
            }
            // ears: the cat-side one flattens back for its tell, a little twitch now and then
            float twitch = (Mathf.FloorToInt(_t * 0.7f) % 4 == 0 && Mathf.Repeat(_t * 0.7f, 1f) < 0.15f) ? 1f : 0f, ea = TellOf(FortunaTell.Ear);
            for (int s = -1; s <= 1; s += 2)
            {
                var tip = new Vector2(s * 4.8f + (s > 0 ? ea * 2.2f : 0f), -62.2f + (s > 0 ? twitch + ea * 3.4f : 0f));
                int ss = s;
                Poly(new[] { new Vector2(s * 1.6f, -56.5f), new Vector2(s * 5.6f, -55f), tip }, (x, y) => Put(x, y, Fur[ss < 0 ? 0 : 1], 3.5f));
                Poly(new[] { new Vector2(s * 2.6f, -56.6f), new Vector2(s * 4.9f, -55.8f), new Vector2(s * 4.5f + (s > 0 ? ea * 1.8f : 0f), -60.4f + (s > 0 ? ea * 2.8f : 0f)) }, (x, y) => Put(x, y, y > -57.5f ? Mix(Pink, White, 0.4f) : Pink, 3.6f));
            }
            Face();
            if (ea > 0.5f) Glint(FX(6.4f), FX(-59.6f), (ea - 0.5f) * 2f, 3);
            // a lock of hair past the human cheek, the fringe
            for (int i = 0; i < 2; i++)
            {
                int ii = i;
                Stroke(-4.4f - i * 0.9f, -56.8f, -5.6f - i * 0.7f + Mathf.Sin(_t * 0.8f + i) * 0.15f, -47.5f + i * 0.6f, 0.4f, (x, y, u, s) => Fur[ii == 1 ? 2 : 1], 4.3f);
            }
            for (float x = -4f; x <= 4f; x += ST)
            {
                float d = Mathf.RoundToInt(x * SC) % 3 == 0 ? 1f : 0.5f;
                for (float y = -57f; y <= -56f + d; y += ST) Put(x, y, Fur[x > 2f ? 2 : Mathf.RoundToInt(x * SC) % 2 != 0 ? 0 : 1], 4.2f);
            }
        }

        /// <summary>Where the finer density is spent: a human eye under a brow, a slit cat eye, a nose, lips, fangs, whiskers.</summary>
        void Face()
        {
            void F(int x, int y, Color32 c, float z = 4.5f) => PutF(x, y, c, z);
            var SRh = Skin; var brow = Fur[3];
            // her right: the human eye
            for (int x = -7; x <= -3; x++) F(x, -109, K);
            F(-8, -110, K);
            for (int x = -7; x <= -3; x++) F(x, -108, x == -7 || x == -3 ? Mix(White, SRh[1], 0.4f) : White);
            for (int x = -6; x <= -4; x++) F(x, -107, x == -5 ? K : Eye);
            F(-5, -108, Mix(Eye, K, 0.3f)); F(-4, -108, Eye); F(-6, -108, White, 4.6f);
            F(-7, -107, White); F(-3, -107, White);
            for (int x = -6; x <= -4; x++) F(x, -106, SRh[2]);
            int[,] bw = { { -8, -112 }, { -7, -113 }, { -6, -113 }, { -5, -113 }, { -4, -112 }, { -3, -112 } };
            for (int i = 0; i < bw.GetLength(0); i++) F(bw[i, 0], bw[i, 1], brow);
            for (int y = -106; y <= -102; y++) F(-1, y, SRh[2]);
            F(-2, -101, SRh[2]); F(-1, -100, SRh[3]);
            // her left: the cat's slit eye
            for (int x = 3; x <= 6; x++) F(x, -109, K);
            F(7, -110, K); F(8, -111, K);
            for (int x = 3; x <= 6; x++) { F(x, -108, Eye); F(x, -107, x == 3 || x == 6 ? Mix(Eye, K, 0.4f) : Eye); }
            F(4, -106, Mix(Eye, K, 0.4f)); F(5, -106, Mix(Eye, K, 0.4f));
            for (int y = -108; y <= -106; y++) F(5, y, K, 4.6f);
            F(4, -108, White, 4.6f);
            F(0, -101, Nose); F(1, -101, Nose); F(0, -100, Mix(Nose, K, 0.3f));
            // lips across the middle, a fang under each corner, the cat's mouth line
            for (int x = -3; x <= 0; x++) F(x, -98, Lip[1]);
            for (int x = -2; x <= 0; x++) F(x, -97, Lip[0]);
            F(-4, -98, Lip[2]); F(1, -98, Lip[2]);
            F(2, -98, Fur[3]); F(3, -98, Fur[3]); F(4, -99, Fur[3]); F(1, -99, Fur[3]); F(0, -99, Fur[3]);
            F(-3, -97, White, 4.7f); F(-3, -96, CardFace[1], 4.7f); F(1, -97, White, 4.7f); F(1, -96, CardFace[1], 4.7f);
            float sm = TellOf(FortunaTell.Smirk);
            if (sm > 0.3f)
            {
                F(-4, -99, Lip[2], 4.8f); F(-5, -100, Lip[2], 4.8f); F(-4, -98, Lip[1], 4.8f); F(-3, -95, White, 4.8f); F(-3, -94, CardFace[1], 4.8f); F(-2, -97, Lip[1], 4.8f);
                Glint(-3, -95, (sm - 0.3f) / 0.7f, 3);
            }
            int[,] wk = { { -101, -1 }, { -99, 0 }, { -97, 1 } };
            var wc = Mix(Fur[0], White, 0.6f);
            for (int j = 0; j < 3; j++)
                for (int i = 0; i < 5; i++)
                    F(6 + i, wk[j, 0] + Mathf.RoundToInt(i * wk[j, 1] * 0.4f) - (wk[j, 1] < 0 ? i / 2 : 0), wc, 4.4f);
        }

        // ---- arms: the riffle, her right hand pushing the band, her cat-side arm raised for the Bluff ----
        void Card(float cx, float cy, float z, bool faceUp, bool rot)
        {
            float w = rot ? 2f : 1f, h = rot ? 1f : 1.5f;
            for (float dy = -h; dy <= h; dy += ST)
                for (float dx = -w; dx <= w; dx += ST)
                {
                    bool rim = Mathf.Abs(dx) > w - ST * 0.6f || Mathf.Abs(dy) > h - ST * 0.6f;
                    Put(cx + dx, cy + dy, faceUp ? (rim ? CardFace[2] : CardFace[0]) : (rim ? Gold[1] : Viol[1]), z);
                }
            Put(cx, cy, faceUp ? Crim[1] : Gold[1], z + 0.01f);
        }

        void Arms()
        {
            float rp = Mathf.Repeat(_t * 0.9f, 1f), ly = -28.5f + _breathe * 0.5f;
            float hp = Smooth(_o.Hand), slipDy = _o.Slip * 2.6f;
            var handR = new Vector2(Mathf.Lerp(-8.6f * Wa, -4.2f, hp), Mathf.Lerp(ly, -52.4f + slipDy * 0.6f, hp));
            Arm(new Vector2(-7.4f * Sh, -43.5f + _breathe), handR, 8f, 7.6f, 5f);
            float rz = Smooth(_o.Raise);
            var handL = new Vector2(Mathf.Lerp(8.6f * Wa, 7.4f, rz), Mathf.Lerp(ly - 1f, -56f, rz));
            Arm(new Vector2(7.4f * Sh, -43.5f + _breathe), handL, 8f, 7.6f, 5f);
            if (_o.Claws > 0f)
                for (int k = -1; k <= 1; k++)
                    Stroke(handL.x + k * 0.7f, handL.y - 0.8f, handL.x + k * 1.1f, handL.y - 0.8f - 2.6f * _o.Claws, u => Mathf.Lerp(0.35f, 0.1f, u), (x, y, u, s) => u > 0.6f ? White : Crim[1], 5.6f);
            for (int i = 0; i < 3; i++) Card(handR.x + i * 0.7f, handR.y - 1.8f - i * 0.4f, 5.3f + i * 0.01f, false, false);
            if (rz < 0.2f && hp < 0.2f) Card(Mathf.Lerp(-7.5f, 8.2f, rp), ly - 2f - Mathf.Sin(rp * Mathf.PI) * 4.5f, 5.5f, rp > 0.5f, rp > 0.3f && rp < 0.7f);
        }

        void Arm(Vector2 sh, Vector2 hand, float l1, float l2, float z)
        {
            var el = Ik(sh, hand, l1, l2);
            float side = sh.x < 0 ? -1f : 1f;
            StrokeCol col = (x, y, u, s) => Fur[Rel(s * 0.8f + side * 0.35f)];
            Stroke(sh.x, sh.y, el.x, el.y, u => (Mathf.Lerp(2.0f, 1.45f, u) + Mathf.Sin(u * Mathf.PI) * 0.15f) * Limb, col, z);
            Stroke(el.x, el.y, hand.x, hand.y, u => Mathf.Lerp(1.5f, 1.05f, u) * Limb, col, z + 0.1f);
            Disc(hand.x, hand.y, 1.2f, 1.2f, (u, v, x, y) => Fur[u + v < 0 ? 0 : 1], z + 0.15f);
            // the dark plate, spiked (reference 3): plates down both arms, a layered pauldron with two spikes
            Stroke(sh.x, sh.y, el.x, el.y, u => Mathf.Lerp(2.1f, 1.7f, u), (x, y, u, q) => (Mathf.FloorToInt(u * 4f) % 2 == 1 && Mathf.Abs(q) > 0.6f) ? DarkP[3] : q * side > 0.3f ? DarkP[2] : DarkP[1], z + 0.3f);
            var fa = Vector2.Lerp(el, hand, 0.85f);
            Stroke(el.x, el.y, fa.x, fa.y, u => Mathf.Lerp(1.8f, 1.35f, u), (x, y, u, q) => (Mathf.FloorToInt(u * 5f) % 2 == 1 && Mathf.Abs(q) > 0.6f) ? DarkP[3] : q * side > 0.3f ? DarkP[2] : DarkP[0], z + 0.3f);
            Pauldron(side, z + 0.4f);
            int spikes = _o.Tatter > 0.75f ? (side < 0 ? 1 : 0) : _o.Tatter > 0.4f ? 1 : 2;
            for (int k = 0; k < spikes; k++)
            {
                float dx = k == 0 ? 1.2f : 2.6f, dy = k == 0 ? 0f : 1.4f;
                Stroke(side * (7.4f * Sh + dx), -45f + dy, side * (7.4f * Sh + dx + 2.2f), -47.6f + dy, u => Mathf.Lerp(0.55f, 0.1f, u), (x, y, u, q) => DarkP[0], z + 0.45f);
            }
            // the tell: a card edge at the cat-side wrist plate
            float sl = TellOf(FortunaTell.Sleeve);
            if (sl > 0f && side > 0f)
            {
                float wx = Mathf.Lerp(el.x, hand.x, 0.72f) + 1.4f, wy = Mathf.Lerp(el.y, hand.y, 0.72f) - 0.6f * sl;
                for (float dy = -1.2f * sl; dy <= 0f; dy += ST) for (float dx = 0f; dx <= 1.2f; dx += ST) Put(wx + dx, wy + dy, dx > 0.9f ? CardFace[2] : CardFace[0], z + 0.5f);
                Put(wx + 0.5f, wy - 0.6f * sl, Crim[1], z + 0.52f);
                Glint(FX(wx + 0.6f), FX(wy - 1.2f * sl), sl, 3);
            }
        }

        void Pauldron(float s, float z)
        {
            for (int k = 2; k >= 0; k--)
            {
                float cx = s * (7.4f * Sh + 0.2f + k * 0.35f), cy = -44.2f + k * 1.35f, rx = 2.7f - k * 0.15f, ry = 1.7f;
                for (float y = cy - ry; y <= cy + ry * 0.55f; y += ST)
                    for (float x = cx - rx; x <= cx + rx; x += ST)
                    {
                        float u = (x - cx) / rx, v = (y - cy) / ry, d = Mathf.Sqrt(u * u + v * v);
                        if (d > 1f) continue;
                        Put(x, y, y > cy + ry * 0.4f ? DarkP[3] : Metal(DarkP, -u * s, v, d * 0.9f), z + (3 - k) * 0.01f);
                    }
            }
        }

        // ---- the outfit (the user's round 9 mix): the white cape, the plated boots, the white bottom and long panel, the Bastet top ----
        void CapeB()
        {
            float dr = Smooth(_o.CapeDrop);
            if (dr > 0f) _shift = (x, y) => { float k = Mathf.Lerp(1f, 0.1f, dr), ny = -0.6f + (y + 0.6f) * k + Mathf.Sin(x * 1.3f) * 0.5f * dr; return new Vector3(0f, ny - y, 1f); };
            for (float y = -44.5f; y <= -1f; y += ST)
            {
                float u = (y + 44.5f) / 43.5f, hw = Mathf.Lerp(6.6f * Sh, 14f, Mathf.Pow(u, 0.8f)), sw = Mathf.Sin(_t * 0.9f + u * 2f) * u * 0.8f;
                for (float x = -hw; x <= hw; x += ST)
                {
                    float yb = -1.6f - 0.9f * Mathf.Sin(x * 0.55f + _t * 0.9f);
                    if (y > yb) continue;
                    bool fold = Mathf.Abs(Mathf.Sin((x - sw) * 0.75f)) < 0.12f; float q = x / hw;
                    var c = fold ? Cape[3] : Cape[Rel(q)];
                    if (y > yb - 1.1f) c = Gold[1]; else if (y > yb - 2.3f) c = Navy[1]; else if (y > yb - 2.8f) c = Gold[2];
                    if (Mathf.Abs(x) > hw - 0.4f) c = Gold[2];
                    Put(x + sw, y, c, -0.6f);
                }
            }
            _shift = null;
        }

        float LegAt(int i, float y)
        {
            if (y <= _knee[i].y) return Mathf.Lerp(_hip[i].x, _knee[i].x, Mathf.Clamp01((y - _hip[i].y) / (_knee[i].y - _hip[i].y)));
            return Mathf.Lerp(_knee[i].x, _foot[i].x, Mathf.Clamp01((y - _knee[i].y) / (_foot[i].y - _knee[i].y)));
        }
        float LegW(int i, float y) => (y <= _knee[i].y ? Mathf.Lerp(3.6f, 2.1f, Mathf.Clamp01((y - _hip[i].y) / (_knee[i].y - _hip[i].y)))
                                                     : Mathf.Lerp(2.2f, 1.0f, Mathf.Clamp01((y - _knee[i].y) / (_foot[i].y - _knee[i].y)))) * Limb;

        static int NearFur(Color32 c)
        {
            int bi = 0; float bd = 1e9f;
            for (int i = 0; i < Fur.Length; i++) { float d = (Fur[i].r - c.r) * (Fur[i].r - c.r) + (Fur[i].g - c.g) * (Fur[i].g - c.g) + (Fur[i].b - c.b) * (Fur[i].b - c.b); if (d < bd) { bd = d; bi = i; } }
            return bi;
        }

        /// <summary>The dark plated thigh-high boots with garters (reference 3): the legs re-coloured below the boot top.</summary>
        void LegsD()
        {
            for (int i = 0; i < _has.Length; i++)
            {
                if (!_has[i] || _z[i] != 0f) continue;
                int fy = Cy - i / W;   // back to y down
                if (fy > -19.4f * SC && fy < 0) _col[i] = DarkP[Mathf.Min(3, NearFur(_col[i]) + 1)];
            }
            for (int i = 0; i < 2; i++)
            {
                float kx = _knee[i].x, ky = _knee[i].y;
                Poly(new[] { new Vector2(kx, ky - 3.6f), new Vector2(kx + 1.6f, ky - 0.6f), new Vector2(kx + 0.8f, ky + 2.6f), new Vector2(kx, ky + 1.4f), new Vector2(kx - 0.8f, ky + 2.6f), new Vector2(kx - 1.6f, ky - 0.6f) },
                     (x, y) => Put(x, y, DarkP[x < kx ? 0 : 1], 2.5f));
                float tx = LegAt(i, -19.4f), hw = LegW(i, -19.4f);
                for (float x = tx - hw; x <= tx + hw; x += ST) Put(x, -19.4f + (Mathf.FloorToInt((x - tx) * 1.6f) % 2 != 0 ? 0.5f : 0f), DarkP[3], 2.4f);
                for (float dx = -1.2f; dx <= 1.2f; dx += ST) Put(_foot[i].x + dx, 0f, DarkP[dx < 0 ? 1 : 2], 3f);
                float bx = LegAt(i, -20.6f), bw = LegW(i, -20.6f) + 0.15f;
                Stroke(bx - bw, -20.6f, bx + bw, -20.6f, 0.35f, (x, y, u, s) => Strap[u < 0.35f ? 0 : u > 0.7f ? 2 : 1], 2.42f);
            }
        }

        /// <summary>The white bottom with gold strings high on the hips and the long gold-worked panel (reference 1).
        /// Torn short as the plate breaks; gone at the top bands' enrage (the bikini).</summary>
        void WaistB()
        {
            Poly(new[] { new Vector2(-5.6f * Hp, -26.3f), new Vector2(5.6f * Hp, -26.3f), new Vector2(1.2f, -22.4f), new Vector2(-1.2f, -22.4f) },
                 (x, y) => Put(x, y, y < -25.9f ? Gold[1] : Cape[x < -2f ? 0 : x > 2f ? 2 : 1], 2.3f));
            for (int s = -1; s <= 1; s += 2) Stroke(s * 5.6f * Hp, -26.3f, s * 6.2f * Hp, -28.2f, 0.3f, (x, y, u, q) => Gold[2], 2.35f);
            if (_o.Bikini) return;
            float tat = _o.Tatter, pend = Mathf.Lerp(-3.5f, -11f, tat);
            for (float y = -25f; y <= pend; y += ST)
            {
                float u = (y + 25f) / 21.5f, sw = Mathf.Sin(_t * 1.1f + u * 2.2f) * u * 0.7f, hw = 1.8f + u * 0.5f;
                for (float x = -hw; x <= hw; x += ST)
                {
                    var c = Sh3(Cape, x / hw);
                    float z2 = Mathf.Repeat(x * 1.4f + y * 0.9f, 2f) - 1f;
                    if (Mathf.Abs(x) < hw - 0.6f && y > -18f && y < -8f) c = Mathf.Abs(z2) < 0.25f ? Gold[2] : Gold[1];
                    if (y > -7f && y < -5.6f) c = Navy[1];
                    if (y >= -5.6f && y < -5f) c = Gold[1];
                    if (Mathf.Abs(Mathf.Abs(x) - hw) < 0.3f) c = Gold[2];
                    if (tat > 0f && y > pend - 2.6f * tat * Hash(FX(x), 7) - 0.3f) continue;
                    Put(x + sw, y, c, 2.7f);
                }
            }
        }

        /// <summary>The broad collar of gold, lapis, turquoise and carnelian, and two fitted linen cups trimmed in gold.</summary>
        void TopA()
        {
            for (float y = -47.5f; y <= -40f; y += ST)
                for (float x = -8f; x <= 8f; x += ST)
                {
                    float d = Mathf.Sqrt(x * x + (y + 47.6f) * 1.18f * (y + 47.6f) * 1.18f);
                    if (d < 2.4f || d > 6.9f || Mathf.Abs(x) > Prof(y) + 0.6f) continue;
                    Color32 c; int k = Mathf.FloorToInt((d - 2.4f) / 0.75f);
                    if (d > 6.2f) { if ((Mathf.FloorToInt(Mathf.Atan2(y + 47.6f, x) * 14f / Mathf.PI) + 40) % 2 != 0) continue; c = Gold[2]; }
                    else c = (k % 6) switch { 0 => Gold[1], 1 => Lapis[1], 2 => Gold[0], 3 => Turq[1], 4 => Gold[1], _ => Carn[1] };
                    if (x > 2.5f) c = Mix(c, K, 0.18f);
                    Put(x, y - _breathe, c, 2.8f);
                }
            float by = -38.6f - _breathe, cx = Mathf.Max(2.3f, Prof(-38.4f) * 0.47f), rx = 2.2f + Bump * 0.7f, rt = 2.0f + Bump * 0.35f, rb = 2.2f + Bump * 0.4f;
            for (int s = -1; s <= 1; s += 2)
            {
                for (float y = by - rt; y <= by + rb; y += ST)
                    for (float x = s * cx - rx; x <= s * cx + rx; x += ST)
                    {
                        if (x * s < 0.3f) continue;
                        float us = (x - s * cx) / rx, v = (y - by) / (y < by ? rt : rb), d = us * us + v * v;
                        if (d > 1f) continue;
                        float kk = us * 0.55f + v * 0.7f;
                        var c = kk < -0.45f ? Linen[0] : kk > 0.4f ? Linen[2] : Linen[1];
                        if (d > 0.66f) c = Gold[v < 0.2f ? 1 : 2];
                        Put(x, y, c, 2.2f);
                    }
                for (float x = s * 0.6f; x * s <= cx + rx * 0.8f; x += s * ST)
                {
                    float u = (x - s * cx) / rx, yy = by + rb * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)) + ST;
                    if (u * s < 0.85f) Put(x, yy, Skin[3], 1.5f);
                }
                Stroke(s * (cx + rx * 0.35f), by - rt * 0.85f, s * 2.5f * Sh + s * 0.4f, -44.6f - _breathe, 0.42f, (x, y, u, q) => Mathf.Abs(q) > 0.6f ? Gold[2] : Linen[1], 2.15f);
                for (float x = s * (cx + rx * 0.8f); x * s <= Prof(-37.6f) + 0.15f; x += s * ST)
                    for (float dy = -0.45f; dy <= 0.45f; dy += ST) Put(x, by + 0.9f + dy, Mathf.Abs(dy) > 0.3f ? Gold[2] : Linen[2], 2.15f);
            }
            for (float y = by - rt * 0.9f; y <= by + rb * 0.4f; y += ST) Put(0, y, Skin[3], 1.6f);
            Disc(0, by + 0.5f, 0.6f, 0.7f, (u, v, x, y) => u + v < -0.4f ? Lapis[0] : Lapis[1], 2.3f);
        }

        // ---- the band over the cat eye (it slips for a tell, falls at the enrage) and the two-faced mask worn as a crown ----
        void BandAndMask()
        {
            float slip = _o.Slip, fall = _o.Fall;
            _shift = (x, y) => new Vector3(fall * 1.5f * Mathf.Sin(x * 0.3f), slip * (2.6f + 0.12f * x) + fall * (53f - 0.1f * x), 1f);
            const float x0 = 6.5f, y0 = -52.2f, x1 = -6.5f, y1 = -58f;
            for (float x = -7f; x <= 7f; x += ST)
            {
                float u = (x - x0) / (x1 - x0), yc = Mathf.Lerp(y0, y1, u);
                for (float dy = -1.5f; dy <= 1.5f; dy += ST)
                {
                    bool fold = Mathf.Abs(dy) < 0.3f && Hash(FX(x), 9) > 0.6f;
                    Put(x, yc + dy, Crim[dy < -1f ? 0 : dy > 1f ? 2 : fold ? 2 : 1], 5f);
                }
            }
            var knot = new Vector2(6.8f, -51.5f); const float dir = 1f;
            Disc(knot.x, knot.y, 1.1f, 0.9f, (u, v, x, y) => Crim[u * dir + v < 0 ? 1 : 2], 5.1f);
            for (int j = 0; j < 2; j++)
            {
                var tp = new Vector2[8];
                for (int i = 0; i <= 7; i++) tp[i] = new Vector2(knot.x + dir * (0.6f + i * 0.5f + j * 0.7f), knot.y + 0.8f + i * 1.05f + Mathf.Sin(_t * 3f + i * 0.7f + j * 1.6f) * i * 0.18f + j * 0.5f);
                for (int i = 0; i < 7; i++) { int ii = i; Stroke(tp[i].x, tp[i].y, tp[i + 1].x, tp[i + 1].y, u => Mathf.Lerp(0.75f, 0.45f, ii / 7f), (x, y, u, s) => Crim[ii > 5 ? 3 : s * dir > 0 ? 2 : 1], 5.1f); }
            }
            _shift = null;
            if (slip > 0.55f && !_o.Fake)
            {
                float g = (slip - 0.55f) / 0.45f;
                Glint(5, -107, g, 5);
            }
            // the mask: its faces swap sides for a tell (it turns through edge-on)
            float ma = TellOf(FortunaTell.Mask), c = Mathf.Cos(Smooth(ma) * Mathf.PI);
            if (ma > 0f) _shift = (x, y) => new Vector3(x * (c - 1f) * (Mathf.Abs(c) < 0.15f ? 0.85f : 1f), 0f, 1f);
            MaskAt(0f, -60.6f, 2.5f, 3.55f);
            _shift = null;
            if (ma > 0.6f) Glint(FX(1.6f), FX(-61.6f), (ma - 0.6f) / 0.4f, 3);
            Stroke(-2.2f, -59f, -3.6f, -57.6f, 0.35f, (x, y, u, s) => Crim[2], 3.5f);
            Stroke(2.2f, -59f, 3.6f, -57.6f, 0.35f, (x, y, u, s) => Crim[2], 3.5f);
        }

        /// <summary>Split down the middle: gold and smiling on her right, lacquer and frowning with a tear on her left.</summary>
        void MaskAt(float cx, float cy, float size, float z)
        {
            for (float dy = -size; dy <= size; dy += ST)
                for (float dx = -size; dx <= size; dx += ST)
                {
                    float u = dx / size, v = dy / size;
                    if (u * u + v * v * 1.1f > 1f) continue;
                    bool L = u < 0f;
                    var c = L ? Gold[u < -0.6f ? 0 : 1] : Satin[u > 0.6f ? 2 : 1];
                    if (Mathf.Abs(u) < 0.07f) c = Gold[2];
                    if ((Mathf.Abs(u) - 0.45f) * (Mathf.Abs(u) - 0.45f) / 0.03f + (v + 0.2f) * (v + 0.2f) / 0.012f < 1f) c = K;
                    if (L && u < -0.12f && u > -0.8f && Mathf.Abs(v - (0.55f - 1.5f * (u + 0.45f) * (u + 0.45f))) < 0.09f) c = Gold[3];
                    if (!L && u > 0.12f && u < 0.8f && Mathf.Abs(v - (0.42f + 1.5f * (u - 0.45f) * (u - 0.45f))) < 0.09f) c = Silver;
                    if (!L && (u - 0.52f) * (u - 0.52f) / 0.008f + (v - 0.12f) * (v - 0.12f) / 0.02f < 1f) c = Tear;
                    Put(cx + dx, cy + dy, c, z);
                }
        }

        /// <summary>The enrage breaks the dark plate: holes through to the fur, scorched edges, more at higher bands.</summary>
        void TatterPass(float tat)
        {
            for (int i = 0; i < _has.Length; i++)
            {
                if (!_has[i]) continue;
                var c = _col[i];
                bool plate = false;
                for (int k = 0; k < DarkP.Length; k++) if (DarkP[k].r == c.r && DarkP[k].g == c.g && DarkP[k].b == c.b) { plate = true; break; }
                if (!plate) continue;
                int x = i % W, y = i / W;
                float h = Hash(x, y, 13), h2 = FireArt.Noise(x * 0.175f, y * 0.175f);
                if (h2 > 1f - tat * 0.5f || h > 1f - tat * 0.18f) _col[i] = Fur[2];
                else if (h2 > 1f - tat * 0.62f || h > 1f - tat * 0.3f) _col[i] = DarkP[3];
            }
        }
    }

    public class FortunaFigure : FortunaCanvas
    {
        public FortunaLook Look;

        protected override bool Draw()
        {
            Figure(Look);
            return true;
        }
    }
}
