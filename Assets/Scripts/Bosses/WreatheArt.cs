using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// What the Wreathe looks like at one moment - everything the drawing reads, set by its owner
    /// each frame. Plain fields, so a domain reload keeps them.
    /// </summary>
    [System.Serializable]
    public struct WreatheLook
    {
        public float Clock;
        /// <summary>0 whole .. 1 gone INTO ITS HOOD (the teleport, the user's pick): the body sinks
        /// up into the hood, the hood drops and is gone. Arriving runs it backwards.</summary>
        public float Blink;
        /// <summary>0 whole .. 1 UNRAVELLED (open to be hit): the smoke parts in hanging strands,
        /// the heart bare between them.</summary>
        public float Open;
        public bool HeartBeat;
        /// <summary>0..1 through the prelude's rising rings (the phrase is about to begin); &lt;0 none.</summary>
        public float Prelude;
        /// <summary>The hood turned away - the tell for a phrase answered backwards.</summary>
        public bool TurnedAway;
        /// <summary>The guard: one ring of smoke per note; <see cref="WreatheCanvas.CutSince"/> says
        /// how long ago each was cut (negative: whole).</summary>
        public int Rings;
        /// <summary>0..1 through its death (unravelling upward); 0 alive.</summary>
        public float Death;
        /// <summary>Enraged: a darker figure lags a beat behind it.</summary>
        public bool Doubled;
    }

    /// <summary>
    /// The Wreathe drawn texel by texel at body density (the user's picks, group 5, 2026-10-26): the
    /// CHOIR ROBE - a peaked hood over an empty dark, a mantle torn into points, wide sleeves
    /// clasped in front, a robe of smoke churning upward and breaking into tongues, never touching
    /// the ground - its EMBER light (on purpose: the alchemist's shadow) inside the hood's rim and
    /// through the worn places, a thin ring of it standing behind the hood, STREAMERS trailing off
    /// the hem, and smoke peeling off its shoulders. No face, ever.
    ///
    /// Local coordinates: lx from the centre line, ly from the hood's peak (0) down to the hem
    /// (<see cref="Height"/>); the hem hovers <see cref="Hover"/> texels over the shadow.
    /// </summary>
    public abstract class WreatheCanvas : TexelPicture
    {
        public const int Height = 46, Hover = 5;
        const int GL = -20, GR = 20, GT = -16, GB = Height + 2;   // the local grid's bounds
        const int GW = GR - GL + 1, GH = GB - GT + 1;

        static Color32 C(int hex) => new((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        public static readonly Color32[] Smoke = { C(0x77707f), C(0x55505f), C(0x403c48), C(0x302d37), C(0x221f28), C(0x151318) };
        public static readonly Color32[] Ember = { C(0xfff4d6), C(0xffd65a), C(0xff8a1f), C(0xd8401a), C(0x8a1d10) };
        public static readonly Color32[] Cold = { C(0xffffff), C(0xe4e8f2), C(0xb4bace), C(0x7e8498), C(0x4e5266) };
        public static readonly Color32[] Red = { C(0xffe9e4), C(0xff9a8a), C(0xff4a3a), C(0xc02020), C(0x701010), C(0x380808) };
        static readonly Color32 K = C(0x0d0b0e), Void = C(0x050408);

        // scratch: the cloth on the local grid, then where each texel lands on the canvas
        Color32[] _cloth; float[] _clothA; bool[] _has;
        Color32[] _out; float[] _outA;
        readonly List<(float x, float y, Color32 c, float a, bool fine)> _behind = new(), _over = new();

        /// <summary>How long ago guard ring <paramref name="i"/> was cut; negative while it holds.</summary>
        public virtual float CutSince(int i) => -1f;

        static float Hash(int a, int b = 0, int c = 0) => FireArt.Hash(a, b, c);
        static float Noise(float x, float y) => FireArt.Noise(x, y);
        static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>The density: 1 body (37.5), 2 weapon (75) - the same figure redrawn on the finer grid. The cloth
        /// is authored in body texels (lx, ly, y down); its loops step <see cref="Step"/>, so at 2 every fine texel is
        /// asked; the overlays (rings, peels, streamers) are body texels, laid down whole.</summary>
        [SerializeField] protected int Fine = 1;
        float Step => 1f / Mathf.Max(1, Fine);
        public void UseFine(int fine) => Fine = Mathf.Max(1, fine);
        int FW => GW * Fine;
        int FH => GH * Fine;

        void Ensure()
        {
            if (_cloth == null || _cloth.Length != FW * FH) { _cloth = new Color32[FW * FH]; _clothA = new float[FW * FH]; _has = new bool[FW * FH]; }
            if (_out == null || _out.Length != W * H) { _out = new Color32[W * H]; _outA = new float[W * H]; }
        }

        int Cell(float l) => Mathf.FloorToInt((l + 0.5f) * Fine);   // a body coordinate's fine texel
        void Put(float lx, float ly, Color32 c, float a = 1f)
        {
            int fx = Cell(lx) - GL * Fine, fy = Cell(ly) - GT * Fine;
            if (fx < 0 || fy < 0 || fx >= FW || fy >= FH) return;
            int i = fy * FW + fx;
            _cloth[i] = c; _clothA[i] = a; _has[i] = true;
        }
        bool Has(float lx, float ly)
        {
            int fx = Cell(lx) - GL * Fine, fy = Cell(ly) - GT * Fine;
            return fx >= 0 && fy >= 0 && fx < FW && fy < FH && _has[fy * FW + fx];
        }
        /// <summary>An overlay at a body texel of the canvas: its whole cell.</summary>
        void OverB(int x, int y, Color32 c, float a = 1f)
        {
            for (int j = 0; j < Fine; j++) for (int k = 0; k < Fine; k++) Over(x * Fine + k, y * Fine + j, c, a);
        }

        /// <summary>The shadow under it, <paramref name="share"/> of its full size.</summary>
        protected void Shadow(int ox, float share)
        {
            if (share <= 0.05f) return;
            float rx = 8f * share, ry = 2f;
            for (int y = -2; y <= 2; y++)
                for (int x = -9; x <= 9; x++)
                    if (x * x / (rx * rx) + y * y / (ry * ry) <= 1f) OverB(ox + x, y, K, 0.45f);
        }

        /// <summary>Draw the figure with its hem's centre <paramref name="ox"/> texels from the canvas
        /// centre. <paramref name="tint"/> &gt; 0 pulls every colour toward a dark red (the double).</summary>
        protected void Figure(WreatheLook o, int ox, float alpha = 1f, float tint = 0f)
        {
            Ensure();
            System.Array.Clear(_has, 0, _has.Length);
            _behind.Clear(); _over.Clear();
            float t = o.Clock;

            float Rag(float ly, float side, float amt = 2f) => (Noise(ly * 0.45f + t * 0.9f, side) - 0.5f) * amt;
            Color32 Shade(float baseI, float lx, float ly, float hw)
            {
                float i = baseI + Mathf.Clamp01((lx + hw) / (2f * hw + 0.01f)) * 1.4f;
                float m = Noise(lx * 0.32f + 5f, ly * 0.32f + t * 0.9f);
                if (m > 0.7f) i -= 1.6f; else if (m > 0.6f) i -= 0.8f; else if (m < 0.3f) i += 0.7f;
                return Smoke[Mathf.Clamp(Mathf.FloorToInt(i), 0, 5)];
            }

            // the ring of its light behind the hood, three short rays
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f);
            for (float a = 0f; a < 6.283f; a += 0.05f)
                _behind.Add((Mathf.RoundToInt(Mathf.Cos(a) * 11f), Mathf.RoundToInt(2f + Mathf.Sin(a) * 10f), Ember[pulse > 0.5f ? 1 : 2], 0.95f, false));
            for (int k = 1; k <= 3; k++)
            {
                _behind.Add((-11 - k, 2, Ember[2], 0.8f, false)); _behind.Add((11 + k, 2, Ember[2], 0.8f, false)); _behind.Add((0, -8 - k, Ember[2], 0.8f, false));
            }

            // the robe: from the chest down, breaking into tongues of smoke below the knee
            for (float ly = 18; ly <= Height; ly += Step)
            {
                float hw = 7.5f + (ly - 18) * 0.22f;
                for (float lx = -15; lx <= 15; lx += Step)
                {
                    if (Mathf.Abs(lx) > hw + Rag(ly, lx > 0 ? 11 : 17)) continue;
                    float hem = 42f + 6f * Noise(lx * 0.35f + 3f, t * 1.2f) + 2f * Mathf.Sin(t * 2.2f + lx * 0.6f);
                    if (ly > hem) continue;
                    if (ly > 32 && Noise(lx * 0.75f, ly * 0.12f - t * 0.8f) < 0.3f + (ly - 32) * 0.012f) continue;
                    Put(lx, ly, Shade(3.1f + (ly > 34 ? 0.5f : 0f), lx, ly, hw), ly < hem - 3f ? 1f : 0.75f);
                }
            }
            // the sleeves, belled, clasped in front
            for (int s = -1; s <= 1; s += 2)
            {
                float sx = s * 8, sy = 20, hx = s * 2, hy = 30;
                int n0 = Mathf.CeilToInt(Mathf.Sqrt((hx - sx) * (hx - sx) + (hy - sy) * (hy - sy)));
                float nx = -(hy - sy) / n0, ny = (hx - sx) / n0;
                int n = n0 * Fine;
                for (int k = 0; k <= n; k++)
                {
                    float u = k / (float)n, x = sx + (hx - sx) * u, y = sy + (hy - sy) * u, wdt = 1.5f + u * 2.5f;
                    for (float d = -wdt; d <= wdt; d += 0.5f * Step)
                        for (float e = -0.5f; e <= 0.5f; e += Step)
                            Put(x + nx * d - ny * e * 0.5f, y + ny * d + nx * e * 0.5f, d * s > wdt - 0.9f ? Smoke[5] : Smoke[u > 0.75f ? 4 : 3]);
                }
            }
            // the mantle over the shoulders, its lower edge torn into points
            float Mb(float lx) => 25f - Mathf.Abs(Mathf.Repeat(lx + 40f, 5f) - 2f) * 1.6f + Rag(lx, 23, 2.5f) - Mathf.Abs(lx) * 0.1f;
            for (float ly = 11; ly <= 30; ly += Step)
            {
                float hw = Mathf.Min(10f, 6.5f + (ly - 11) * 0.9f);
                for (float lx = -15; lx <= 15; lx += Step)
                {
                    float mb = Mb(lx);
                    if (Mathf.Abs(lx) > hw + Rag(ly, lx > 0 ? 31 : 37) * 0.6f || ly > mb) continue;
                    Put(lx, ly, ly > mb - 1f ? Smoke[4] : Shade(2.8f, lx, ly, hw));
                }
            }
            for (float lx = -12; lx <= 12; lx += Step) { float y = Mathf.Floor(Mb(lx) * Fine) / Fine + Step; if (Has(lx, y)) Put(lx, y, Smoke[5]); }
            // the hood, peaked
            float[] hood = { 3, 4, 5, 5.5f, 6, 6.5f, 6.5f, 7, 7, 7, 7, 7, 7, 6.5f, 6 };
            for (float ly = -4; ly <= 14; ly += Step)
            {
                // the hood's profile, eased between its rows (a whole-texel table at body density)
                float HoodAt(float y) { if (y < 0) return 1f + (y + 4) * 0.5f; int a = Mathf.Clamp(Mathf.FloorToInt(y), 0, hood.Length - 1), b = Mathf.Min(hood.Length - 1, a + 1); return Mathf.Lerp(hood[a], hood[b], y - Mathf.Floor(y)); }
                float hh = Fine == 1 ? (ly < 0 ? 1f + (ly + 4) * 0.5f : hood[(int)ly]) : HoodAt(ly);
                float sh = ly < 4 ? 1 : 0;
                for (float lx = -9; lx <= 9; lx += Step) if (Mathf.Abs(lx - sh) <= hh) Put(lx, ly, Shade(2.4f, lx, ly, hh));
            }
            // the opening: no face, only the dark, the inner edge catching its light. Turned away,
            // the back of the hood.
            if (!o.TurnedAway)
                for (float ly = 3; ly <= 14; ly += Step)
                    for (float lx = -6; lx <= 6; lx += Step)
                    {
                        float d = Mathf.Sqrt(((lx - 0.5f) / 4.6f) * ((lx - 0.5f) / 4.6f) + ((ly - 8.8f) / 5.2f) * ((ly - 8.8f) / 5.2f));
                        if (d >= 1f && d < 1.25f && Has(lx, ly)) Put(lx, ly, Smoke[ly < 9 ? 1 : 2]);
                        if (d < 1f) Put(lx, ly, Void);   // no inner glow: a lit lower rim read as a mouth
                    }
            else
                for (float ly = 2; ly <= 12; ly += Step) for (float e = -0.5f; e < 0.5f; e += Step) Put(Mathf.Sin(ly * 0.6f) * 0.6f + (ly < 4 ? 1 : 0) + e, ly, Smoke[4]);
            // worn through: its light shows faintly where the smoke wears thin
            for (float ly = 16; ly < 34; ly += Step)
                for (float lx = GL; lx <= GR; lx += Step)
                    if (Has(lx, ly) && Noise(lx * 0.5f + 11f, ly * 0.5f - t * 0.6f) > 0.86f) Put(lx, ly, Smoke[1]);
            // at the fine grid, thin strands of smoke churning up through the robe and mantle: the
            // noise's contour lines, one lit, one dark, rising with the clock
            if (Fine > 1)
                for (float ly = 12; ly < 46; ly += Step)
                    for (float lx = GL; lx <= GR; lx += Step)
                    {
                        if (!Has(lx, ly)) continue;
                        float n = Noise(lx * 0.26f + 31f + Mathf.Sin(ly * 0.2f + t) * 0.4f, ly * 0.15f + t * 0.9f);
                        if (Mathf.Abs(n - 0.5f) < 0.022f) Put(lx, ly, Smoke[ly > 34 ? 3 : 2]);
                        else if (Mathf.Abs(n - 0.68f) < 0.018f) Put(lx, ly, Smoke[5]);
                    }

            // smoke peeling off the shoulders and hood: thin curls rising behind
            int[,] peel = { { -7, 3 }, { 7, 4 }, { -12, 13 }, { 12, 14 }, { -14, 19 }, { 14, 20 }, { -5, 0 }, { 9, 9 } };
            for (int k = 0; k < 8; k++)
            {
                float P = 1.6f + Hash(k, 41) * 0.8f, ph = Mathf.Repeat(t + Hash(k, 42) * P, P) / P;
                int len = (9 + (int)(Hash(k, 43) * 6f)) * (Fine == 1 ? 1 : Fine * 2), x0 = peel[k, 0], y0 = peel[k, 1];
                for (int i = 0; i < len; i++)
                {
                    float u = i / (float)len, life = ph - u * 0.6f;
                    if (life < 0f || life > 0.75f) continue;
                    float lx = x0 + Mathf.Sign(x0) * (u * 3f + Mathf.Sin(u * 5f + t * 2f + k) * 1.6f), ly = y0 - u * 14f - ph * 4f;
                    var c = Smoke[1 + Mathf.FloorToInt(u * 3f)]; float a = (1f - u) * (1f - life) * 0.95f;
                    if (Fine == 1) _behind.Add((Mathf.RoundToInt(lx), Mathf.RoundToInt(ly), c, a, false));
                    else
                    {
                        // a fine curl, two texels thick at the root and one at the tip, a second wisp
                        // curling off it
                        _behind.Add((lx, ly, c, a, true));
                        if (u < 0.45f) _behind.Add((lx + Step, ly, c, a, true));
                        if (u > 0.3f) _behind.Add((lx - Mathf.Sign(x0) * (Mathf.Sin(u * 9f + t * 3f + k) * 1.2f + 0.8f), ly + 1.2f, Smoke[2 + Mathf.FloorToInt(u * 2.99f)], a * 0.6f, true));
                    }
                }
            }
            // streamers trailing off the hem
            for (int k = 0; k < 6; k++)
            {
                int lx0 = -12 + Mathf.RoundToInt((k + 0.5f) / 6f * 24f), ly0 = -1;
                for (int ly = Height; ly > 28; ly--) if (Has(lx0, ly)) { ly0 = ly; break; }
                if (ly0 < 0) continue;
                int len = 18 * Fine;
                for (int ii = 0; ii <= len; ii++)
                {
                    float i = ii / (float)Fine, u = ii / (float)len, x = lx0 + i * 0.8f + Mathf.Sin(i * 0.4f - t * 5f + k) * 1.6f * u;
                    float y = ly0 - 1 + Mathf.Min(i, 5) * 0.5f + Mathf.Sin(i * 0.3f + k) * u;
                    var c = Smoke[2 + Mathf.FloorToInt(u * 2.99f)];
                    if (Fine == 1) { for (int d = 0; d <= (1f - u) * 1.8f; d++) _over.Add((Mathf.RoundToInt(x), Mathf.RoundToInt(y + d), c, 1f - u * 0.6f, false)); continue; }
                    for (float d = 0; d <= (1f - u) * 1.8f; d += Step) _over.Add((x, y + d, c, 1f - u * 0.6f, true));
                }
            }

            // ---- out to the canvas, through the moment's warp ----
            System.Array.Clear(_outA, 0, _outA.Length);
            bool Warp(float lx, float ly, out float wx, out float wy, out float wa)
            {
                wx = lx; wy = ly; wa = 1f;
                if (o.Death > 0f)
                {
                    float k = o.Death;
                    if (ly < 14) { float f = Mathf.Clamp01((k - 0.55f) / 0.45f); if (f >= 1f) return false; wx = lx + f * 4f; wy = ly + f * 32f; wa = 1f - f * 0.6f; return true; }
                    int hx = Mathf.RoundToInt(lx), hy = Mathf.RoundToInt(ly);
                    float sp = 0.4f + Hash(hx, 9) * 1.2f;
                    if (Hash(hx, hy, 2) < k * 1.1f - 0.15f) return false;
                    wx = lx + Mathf.Sin(ly * 0.3f + t * 4f) * k * 3f; wy = ly - k * k * 40f * sp; wa = 1f - k * 0.5f; return true;
                }
                if (o.Blink > 0f)
                {
                    float d = o.Blink, line = Height - d * (Height + 2);
                    if (ly < 14) { if (d > 0.75f) return false; wx = lx + d * 3f; wy = ly + d * 30f; return true; }
                    if (ly > line) return false;
                    wy = ly + d * 18f; return true;
                }
                if (o.Open > 0f)
                {
                    float k = o.Open;
                    int band = Mathf.FloorToInt((lx + 20) / 4f);
                    if (ly < 14) { wy = ly - k * 3f; return true; }
                    wx = lx + (band - 5) * k * 2.2f; wy = ly + Mathf.Sin(band + t * 3f) * k * 2f; wa = 1f - k * 0.15f; return true;
                }
                return true;
            }
            int CX(float wx) => ox + Mathf.RoundToInt(wx);
            int CY(float wy) => Hover + Mathf.RoundToInt(Height - wy);
            // the fine canvas texel for a body point (y up on the canvas)
            int FXc(float wx) => (ox * Fine) + Mathf.FloorToInt((wx + 0.5f) * Fine);
            int FYc(float wy) => (Hover + Height) * Fine + (Fine - 1) - Mathf.FloorToInt((wy + 0.5f) * Fine);
            Color32 Tint(Color32 c) => tint > 0f ? Color32.Lerp(c, Red[4], tint) : c;

            void Lay((float x, float y, Color32 c, float a, bool fine) e)
            {
                if (!Warp(e.x, e.y, out var wx, out var wy, out var wa)) return;
                if (e.fine) Over(FXc(wx), FYc(wy), Tint(e.c), e.a * wa * alpha);
                else OverB(CX(wx), CY(wy), Tint(e.c), e.a * wa * alpha);
            }
            foreach (var e in _behind) Lay(e);

            // the guard's back halves, behind the body
            Guard(o, ox, false, alpha);

            for (int fy = 0; fy < FH; fy++)
                for (int fx = 0; fx < FW; fx++)
                {
                    int i = fy * FW + fx;
                    float lx = (fx + 0.5f) / Fine - 0.5f + GL, ly = (fy + 0.5f) / Fine - 0.5f + GT;
                    if (!_has[i] || !Warp(lx, ly, out var wx, out var wy, out var wa)) continue;
                    int x = Cx + FXc(wx), y = Cy + FYc(wy);
                    if (x < 0 || y < 0 || x >= W || y >= H) continue;
                    _out[y * W + x] = _cloth[i]; _outA[y * W + x] = wa * _clothA[i];
                }
            // the outline, round every solid texel: one body texel thick
            for (int y = Fine; y < H - Fine; y++)
                for (int x = Fine; x < W - Fine; x++)
                {
                    if (_outA[y * W + x] > 0f) continue;
                    bool next = false;
                    for (int dy = -Fine; dy <= Fine && !next; dy++)
                        for (int dx = -Fine; dx <= Fine; dx++)
                            if ((dx != 0 || dy != 0) && Mathf.Abs(dx) + Mathf.Abs(dy) <= Fine && _outA[(y + dy) * W + x + dx] > 0.85f) { next = true; break; }
                    if (next) Over(x - Cx, y - Cy, K, 0.9f * alpha);
                }
            for (int i = 0; i < _outA.Length; i++)
                if (_outA[i] > 0f) Over(i % W - Cx, i / W - Cy, Tint(_out[i]), _outA[i] * alpha);
            foreach (var e in _over) Lay(e);

            Guard(o, ox, true, alpha);

            // the heart, bare when it is open - drawn whole, not through the strands' warp
            if (o.Open > 0.3f && o.Death <= 0f)
            {
                int r = o.HeartBeat ? 4 : 3, hy = CY(24f);
                for (int dy = -r - 1; dy <= r + 1; dy++)
                    for (int dx = -r - 1; dx <= r + 1; dx++)
                    {
                        int m = Mathf.Abs(dx) + Mathf.Abs(dy);
                        if (m == r + 1) OverB(ox + dx, hy + dy, K, alpha);
                        else if (m <= r) OverB(ox + dx, hy + dy, TexelFx.Gold[m > r - 1 ? 2 : o.HeartBeat ? 0 : 1], alpha);
                    }
            }

            // the prelude: two rings rising out of the hood - the phrase is about to begin
            if (o.Prelude >= 0f)
                for (int k = 0; k < 2; k++)
                {
                    float u = o.Prelude * 1.6f - k * 0.35f;
                    if (u < 0f || u > 1f) continue;
                    float r = 3f + u * 9f, cy = 6f - u * 10f;
                    for (float a = 0f; a < 6.283f; a += 0.12f)
                        if (Mathf.Sin(a) < 0.2f || u < 0.3f)
                            OverB(CX(1f + Mathf.Cos(a) * r), CY(cy + Mathf.Sin(a) * r * 0.35f), Ember[1 + Mathf.FloorToInt(u * 2.99f)], (1f - u * 0.6f) * alpha);
                }
        }

        /// <summary>The guard: one ring of smoke turning round it per note, the near half in front of
        /// it; a cut ring snaps open, falls and is gone.</summary>
        void Guard(WreatheLook o, int ox, bool front, float alpha)
        {
            if (o.Rings <= 0 || o.Open > 0f || o.Death > 0f) return;
            float t = o.Clock, step = Mathf.Min(6f, 26f / Mathf.Max(1, o.Rings - 1));
            for (int i = 0; i < o.Rings; i++)
            {
                float since = CutSince(i);
                if (since > 0.3f) continue;
                float ly = 16f + i * step, rx = 13f + (i % 2), ry = 2.5f;
                for (float a = 0f; a < 6.283f; a += 0.05f / Fine)
                {
                    float s = Mathf.Sin(a + t * 2f + i);
                    if ((s > 0f) != front) continue;
                    if (since >= 0f && Mathf.Abs(Mathf.Cos(a)) < since * 3f) continue;
                    // smoke, not a band: broken into wisps that drift round, tilted a little
                    if (Hash(Mathf.FloorToInt((a + t * 0.7f) * 6f), i, 7) < 0.35f) continue;
                    float fall = since >= 0f ? since * 20f : 0f, c = Mathf.Cos(a + t * 2f + i);
                    float wy = ly + s * ry + c * (i % 2 == 0 ? 1.2f : -1.2f) + fall, fa = (front ? 0.8f : 0.5f) * (since >= 0f ? 1f - since * 3f : 1f) * alpha;
                    if (Fine == 1) { OverB(ox + Mathf.RoundToInt(c * rx), Hover + Mathf.RoundToInt(Height - wy), front ? Smoke[0] : Smoke[2], fa); continue; }
                    // fine: a thread of smoke with a fainter one trailing under it, frayed apart in places
                    int fx = ox * Fine + Mathf.FloorToInt((c * rx + 0.5f) * Fine), fy = (Hover + Height) * Fine + (Fine - 1) - Mathf.FloorToInt((wy + 0.5f) * Fine);
                    Over(fx, fy, front ? Smoke[0] : Smoke[2], fa);
                    if (Hash(Mathf.FloorToInt((a - t * 0.5f) * 20f), i, 8) > 0.3f) Over(fx, fy - 1, front ? Smoke[1] : Smoke[3], fa * 0.6f);
                }
            }
        }
    }

    /// <summary>The living Wreathe's picture, depth sorted on its body; its owner sets <see cref="Look"/>.</summary>
    public class WreatheFigure : WreatheCanvas
    {
        public WreatheLook Look;
        [SerializeField] float[] _cutAt = new float[0];

        /// <summary>Guard ring <paramref name="i"/> is cut now (or whole again, with &lt;0).</summary>
        public void SetRings(int count) { Look.Rings = count; _cutAt = new float[count]; for (int i = 0; i < count; i++) _cutAt[i] = -1f; }
        public void Cut(int i) { if (_cutAt != null && i >= 0 && i < _cutAt.Length) _cutAt[i] = Age; }
        public override float CutSince(int i) => _cutAt == null || i >= _cutAt.Length || _cutAt[i] < 0f ? -1f : Age - _cutAt[i];

        protected override bool Draw()
        {
            Look.Clock = Age;
            if (Look.Blink >= 1f) return false;
            Shadow(0, 1f - Look.Blink);
            if (Look.Doubled)
            {
                var lag = Look; lag.Clock = Age - 0.35f; lag.Prelude = -1f; lag.Rings = 0;
                Figure(lag, Mathf.RoundToInt(Mathf.Sin(Age * 2.4f) * 5f) + 4, 0.55f, 0.6f);
            }
            Figure(Look, 0);
            return true;
        }
    }

    /// <summary>Its death (the user's pick): the smoke pulls apart in strands rising and thinning,
    /// the empty hood drops last, the light winks out. Its own object - the boss is destroyed the
    /// frame it dies.</summary>
    public class WreatheDeath : WreatheCanvas
    {
        const float Seconds = 1.6f;

        public static void At(Vector2 at)
        {
            var go = new GameObject("wreathe.death");
            go.transform.position = at;
            var p = Attach<WreatheDeath>(go.transform, "figure", Vector2.zero, 34 * Tuning.Boss.FigureFine, 6 * Tuning.Boss.FigureFine, 64 * Tuning.Boss.FigureFine, 0, TexelFx.Body * Tuning.Boss.FigureFine);
            p.UseFine(Tuning.Boss.FigureFine);
            DepthSorted.Attach(go, 0f, false, p.GetComponent<SpriteRenderer>());
            Destroy(go, Seconds + 0.2f);
        }

        protected override bool Draw()
        {
            float k = Mathf.Clamp01(Age / Seconds);
            if (k >= 1f) return false;
            Shadow(0, 1f - k);
            Figure(new WreatheLook { Clock = Age, Death = Mathf.Max(0.001f, k), Prelude = -1f }, 0);
            if (k > 0.85f)
            {   // the light winks out at its chest
                int cy = Hover + Height - 24;
                Over(0, cy, Ember[0]);
                for (int i = 1; i <= 3; i++) { var c = Ember[Mathf.Min(4, i)]; Over(i, cy, c); Over(-i, cy, c); Over(0, cy + i, c); Over(0, cy - i, c); }
            }
            return true;
        }
    }
}
