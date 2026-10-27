using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// What Medusa looks like at one moment - everything the drawing reads, set by her fight each
    /// frame. Plain fields, so a domain reload keeps them.
    /// </summary>
    [System.Serializable]
    public struct MedusaLook
    {
        public float Clock;
        /// <summary>0 level .. 1 fully tipped: the scales are the gaze's CLOCK (the user's call).</summary>
        public float Tilt;
        /// <summary>0 at rest .. 1 her free hand gripping the blindfold (held through the tell).</summary>
        public float HandUp;
        /// <summary>0 over her eyes .. 1 torn down round her neck (the look).</summary>
        public float FoldDown;
        public float EyeLit;
        /// <summary>0..1 how far the black tears have run since the blindfold came off.</summary>
        public float Tears;
        /// <summary>0 hidden under the brow serpents .. 1 the red third eye open (the enrage).</summary>
        public float ThirdOpen;
        /// <summary>The gem at her girdle: 0 dark, 1 gold, 2 gold and bright (the beat while open).</summary>
        public int Gem;
        /// <summary>0 held high .. 1 lying on the floor beside her (open to be hit).</summary>
        public float DropScales;
        /// <summary>0..1 the coil heaving outward (her tail behind a push or pull).</summary>
        public float Loosen;
        public float Bow, Limp;
        public int Sink;
        public bool Shut;
        /// <summary>0..1 her crown rearing and hissing (it wakes her statues; it spits venom).</summary>
        public float Hiss;
    }

    /// <summary>
    /// Medusa drawn texel by texel at body density (the user's picks, group 5, 2026-10-26): a lamia
    /// Justice - verdigris skin and scales, a COILED serpent's tail in two loops on the floor, a
    /// KNOTTED CROWN of serpents round her head (two more lying across her brow over the hidden
    /// third eye), a WHITE LINEN blindfold, the bronze SCALES OF JUSTICE held high in one hand.
    ///
    /// Local coordinates: lx from her centre line, ly from the top of her head (0) down to the foot
    /// of her coil (<see cref="Height"/>). The coil's middle (<see cref="Base"/>) is her body's position.
    /// </summary>
    public abstract class MedusaCanvas : TexelPicture
    {
        public const int Height = 50, Base = 45;
        const int GL = -34, GR = 34, GT = -12, GB = Height + 6;
        const int GW = GR - GL + 1, GH = GB - GT + 1;

        static Color32 C(int hex) => new((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        public static readonly Color32[] Skin = { C(0xe4e8da), C(0xbcc4b2), C(0x929d8a), C(0x677160) };
        public static readonly Color32[] Scale = { C(0xa8d0b0), C(0x76aa8e), C(0x4e816b), C(0x315849), C(0x1b352b) };
        static readonly Color32[] Belly = { C(0xe6e2c6), C(0xc0ba9a), C(0x918b6d) };
        public static readonly Color32[] Hair = { C(0xa6d4b0), C(0x74a888), C(0x4c7a62), C(0x2c4c3d) };
        static readonly Color32[] Cloth = { C(0xece6d6), C(0xc6bea8), C(0x958c75) };
        static readonly Color32[] Scalp = { C(0x3b4d42), C(0x26342c) };
        static readonly Color32[] Linen = { C(0xf6f2e8), C(0xd4cdbd), C(0xa39b88) };
        public static readonly Color32[] Bronze = { C(0xfbe2a2), C(0xd6a656), C(0x9a6c2c), C(0x5c3e16) };
        public static readonly Color32[] Stone = { C(0xd4d0c8), C(0xaaa59c), C(0x807b72), C(0x59554e), C(0x3a3733) };
        public static readonly Color32[] EyeWhite = { C(0xffffff), C(0xe6ecef), C(0xb9c3c8) };
        public static readonly Color32[] Red = { C(0xffe9e4), C(0xff9a8a), C(0xff4a3a), C(0xc02020), C(0x701010), C(0x380808) };
        static readonly Color32 K = C(0x0d0b0e), Mouth = C(0x7a3038), Tear = C(0x141014), SnakeEye = C(0xe04040);

        // scratch: a depth-sorted map on the local grid
        Color32[] _col; float[] _z; bool[] _has;
        readonly List<(int x, int y)> _drawn = new();

        static float Hash(int a, int b = 0, int c = 0) => FireArt.Hash(a, b, c);

        void Ensure()
        {
            if (_col == null || _col.Length != GW * GH) { _col = new Color32[GW * GH]; _z = new float[GW * GH]; _has = new bool[GW * GH]; }
        }

        void P(float fx, float fy, Color32 c, float z)
        {
            int lx = Mathf.RoundToInt(fx), ly = Mathf.RoundToInt(fy);
            if (lx < GL || lx > GR || ly < GT || ly > GB) return;
            int i = (ly - GT) * GW + (lx - GL);
            if (_has[i] && _z[i] > z) return;
            _col[i] = c; _z[i] = z; _has[i] = true;
        }

        /// <summary>The contact shadow under the coil, <paramref name="share"/> of its full size.</summary>
        protected void Shadow(int ox, float share)
        {
            if (share <= 0.05f) return;
            float rx = 18f * share, ry = 2.5f;
            for (int y = -3; y <= 3; y++)
                for (int x = -19; x <= 19; x++)
                    if (x * x / (rx * rx) + y * y / (ry * ry) <= 1f) Over(ox + x, -(Height - Base) + y, K, 0.45f);
        }

        // a tube along points: the tail
        void Tube(List<Vector3> pts, System.Func<float, float> rad, System.Func<int, bool, float, float, float, bool> belly)
        {
            float s = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i];
                if (i > 0) s += Vector2.Distance(p, pts[i - 1]);
                float r = rad(i / (float)(pts.Count - 1));
                int e = Mathf.CeilToInt(r);
                for (int dy = -e; dy <= e; dy++)
                    for (int dx = -e; dx <= e; dx++)
                    {
                        if (dx * dx + dy * dy > r * r + 0.3f) continue;
                        float v = 1.2f + dy / (r + 0.01f) * 1.4f + (dx > 0 ? 0.5f : 0f);
                        if (Hash(Mathf.RoundToInt(p.x + dx), Mathf.RoundToInt(p.y + dy), 3) > 0.82f) v -= 0.8f;
                        bool rim = dx * dx + dy * dy > (r - 0.9f) * (r - 0.9f) && dy > -r * 0.3f;
                        var c = Scale[Mathf.Clamp(Mathf.FloorToInt(v), 0, 4)];
                        if (belly != null && belly(i, false, dx, dy, r)) c = Belly[(Mathf.RoundToInt(p.y + dy) % 2 == 0 ? 1 : 0) + (dx > r * 0.4f ? 1 : 0)];
                        if (rim) c = Scale[4];
                        P(p.x + dx, p.y + dy, c, p.z);
                    }
            }
        }

        static void Line(float x0, float y0, float x1, float y1, System.Action<float, float, float> fn)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))));
            for (int i = 0; i <= n; i++) fn(Mathf.Round(x0 + (x1 - x0) * i / n), Mathf.Round(y0 + (y1 - y0) * i / n), i / (float)n);
        }

        void Limb(Vector2 a, Vector2 b, float w, float z)
        {
            int n = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f);
            for (int i = 0; i <= n; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)Mathf.Max(1, n));
                for (float dx = -w; dx <= w; dx += 0.5f)
                    for (float dy = -w; dy <= w; dy += 0.5f)
                        if (dx * dx + dy * dy <= w * w) P(p.x + dx, p.y + dy, Skin[dx > 0.2f ? 2 : 1], z);
            }
        }

        void Snake(List<Vector2> pts, float z)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                P(pts[i].x, pts[i].y, Hair[i % 4 == 0 ? 0 : 1], z);
                P(pts[i].x, pts[i].y + 1, Hair[2], z);
            }
            if (pts.Count < 2) return;
            Vector2 p = pts[pts.Count - 1], q = pts[pts.Count - 2];
            float ang = Mathf.Atan2(p.y - q.y, p.x - q.x);
            float hx = p.x + Mathf.Cos(ang), hy = p.y + Mathf.Sin(ang);
            for (int dy = -1; dy <= 0; dy++) for (int dx = -1; dx <= 0; dx++) P(hx + dx, hy + dy, Hair[1], z + 0.02f);
            P(hx + (Mathf.Cos(ang) > 0f ? 0 : -1), hy - 1, SnakeEye, z + 0.03f);
        }

        static List<Vector2> SnakePath(float bx, float by, float ang, int len, float t, float ph, float amp)
        {
            var pts = new List<Vector2> { new(bx, by) };
            for (int i = 1; i <= len; i++)
            {
                float u = i / (float)len, a = ang + Mathf.Sin(t * 3.2f + ph + u * 4f) * amp * u;
                pts.Add(pts[i - 1] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            }
            return pts;
        }

        /// <summary>Draw her with her coil's middle <paramref name="ox"/> texels from the canvas
        /// centre. <paramref name="stoneLine"/>: every texel below that ly is grey stone (her death);
        /// <paramref name="shatter"/> &gt; 0 breaks her into blocks falling into a heap.</summary>
        protected void Figure(MedusaLook o, int ox, float stoneLine = 999f, float shatter = 0f)
        {
            Ensure();
            System.Array.Clear(_has, 0, _has.Length);
            float t = o.Clock;
            int ty = o.Sink;

            // ---- the coil: down from the waist, two loops outward and down, the tip flicking out
            var down = new List<Vector3>();
            for (int i = 0; i <= 10; i++) down.Add(new Vector3(Mathf.Sin(i * 0.3f) * 1.2f, 30 + ty + i, 0.6f));
            Tube(down, u => 4.3f - u * 0.6f, (i, _, dx, dy, r) => Mathf.Abs(dx) < r * 0.55f && i > 2);
            var coil = new List<Vector3>();
            const int n = 120;
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, a = Mathf.PI / 2f + u * 2f * 6.283f;
                float rx = 8.5f + u * 7.5f + o.Loosen * 5f, ry = 2.6f + u * 1.8f + o.Loosen, cy = 40.5f + u * 5.5f;
                coil.Add(new Vector3(Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry, Mathf.Sin(a)));
            }
            Tube(coil, u => 3.4f - u * 0.8f, null);
            var tip = new List<Vector3>();
            Vector3 e = coil[n];
            float lash = o.Loosen;
            for (int i = 0; i <= 12; i++)
            {
                float u = i / 12f;
                tip.Add(new Vector3(e.x + i * (0.95f + lash * 0.5f), e.y + Mathf.Sin(i * 0.5f - t * (4f + lash * 10f)) * u * (1.5f + lash * 2f) - u * u * 4f, 1.1f));
            }
            Tube(tip, u => 2.6f * (1f - u) + 0.4f, null);

            // ---- the torso
            bool breathe = Mathf.Sin(t * 2.2f) > 0.6f;
            for (int ly = 14; ly <= 31; ly++)
            {
                float hw = ly < 16 ? 1.5f : ly < 20 ? 6f : ly < 24 ? 5.5f - (ly - 20) * 0.2f : ly < 28 ? 4.5f - (ly - 24) * 0.12f : 4.2f;
                if (breathe && ly >= 17 && ly < 23) hw += 0.4f;
                for (int lx = -7; lx <= 7; lx++)
                {
                    if (Mathf.Abs(lx) > hw) continue;
                    float u = (lx + hw) / (2f * hw + 0.01f);
                    var c = Skin[Mathf.Min(3, Mathf.FloorToInt(0.4f + u * 2.6f))];
                    if (ly >= 18 && ly <= 22) c = ly == 18 || ly == 22 ? Cloth[2] : Cloth[Mathf.Min(2, Mathf.FloorToInt(u * 2.4f))];
                    if (ly >= 16 && ly <= 18 && lx >= 2 && lx <= 4 && Mathf.Abs(lx - (3 - (ly - 16) * 0.5f)) < 1f) c = Cloth[1];
                    if (ly == 27 || ly == 28) c = Bronze[(lx + 20) % 3 == 0 ? 0 : 1 + (u > 0.6f ? 1 : 0)];
                    if (ly >= 29) c = Scale[1 + Mathf.FloorToInt(u * 2.5f)];
                    P(lx, ly + ty, c, 0f);
                }
            }
            var gem = o.Gem == 0 ? Scale[3] : Bronze[o.Gem > 1 ? 0 : 1];
            P(0, 27 + ty, gem, 0.1f); P(0, 28 + ty, gem, 0.1f);

            // ---- the head
            int hx = Mathf.RoundToInt(o.Bow * 2f), hy = Mathf.RoundToInt(ty + o.Bow * 2f);
            int[] headW = { 2, 3, 4, 4, 4, 4, 4, 4, 3, 2 };
            for (int ly = 4; ly <= 13; ly++)
                for (int lx = -4; lx <= 4; lx++)
                {
                    if (Mathf.Abs(lx) > headW[ly - 4]) continue;
                    var c = Skin[Mathf.Min(3, Mathf.FloorToInt(0.6f + (lx + 4) / 8f * 2.4f))];
                    if (ly <= 6 || (Mathf.Abs(lx) >= 4 && ly <= 11)) c = Scalp[lx > 1 ? 1 : 0];
                    if (ly == 12 && (lx == 0 || lx == -1)) c = Mouth;
                    if (ly == 10 && lx == 0) c = Skin[2];
                    P(lx + hx, ly + hy, c, 0.2f);
                }
            float fd = Mathf.Clamp01(o.FoldDown);
            int eyeRow = 9 + hy;
            if (o.Shut) { foreach (int x in new[] { -2, -1, 1, 2 }) P(x + hx, eyeRow, Skin[3], 0.3f); }
            else
                foreach (int ex in new[] { -2, 1 })
                {
                    if (fd > 0.5f) { P(ex + hx, eyeRow - 1, Skin[3], 0.29f); P(ex + 1 + hx, eyeRow - 1, Skin[3], 0.29f); }
                    P(ex + hx, eyeRow, EyeWhite[o.EyeLit > 0.5f ? 0 : 1], 0.3f);
                    P(ex + 1 + hx, eyeRow, EyeWhite[o.EyeLit > 0.5f ? 0 : 2], 0.3f);
                    int len = fd > 0.5f ? Mathf.RoundToInt(1f + Mathf.Clamp01(o.Tears) * 4f) : 0;
                    for (int k = 1; k <= len; k++) P(ex + (ex < 0 ? 0 : 1) + hx, eyeRow + k, Tear, 0.32f);
                }

            // ---- the third eye: red, upright, between the brow serpents
            float third = Mathf.Clamp01(o.ThirdOpen);
            if (third > 0.25f)
            {
                int tall = third > 0.7f ? 2 : 1;
                for (int k = -tall + 1; k <= tall - 1; k++) P(hx, 6 + hy + k, Red[k != 0 ? 1 : 0], 0.45f);
                P(hx, 6 + hy, Red[0], 0.46f);
                P(hx - 1, 6 + hy, Red[2], 0.44f); P(hx + 1, 6 + hy, Red[2], 0.44f);
                if (tall > 1)
                    foreach (var (dx, dy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1), (0, -3), (0, 3) })
                        P(hx + dx, 6 + hy + dy, K, 0.44f);
            }

            // ---- the blindfold: white linen; pulled down, it lies round her neck
            int foldY = 9 + hy + Mathf.RoundToInt(fd * 6f);
            for (int lx = -5; lx <= 5; lx++)
                for (int k = 0; k <= 1; k++)
                {
                    int w = fd > 0.6f ? 2 : 4;
                    if (Mathf.Abs(lx) > w + (fd > 0.6f ? 0 : 1)) continue;
                    P(lx + hx, foldY + k - 1, Linen[Mathf.Min(2, k + (lx > 2 ? 1 : 0))], 0.5f);
                    if (k == 1 && Mathf.Abs(lx) <= 3) P(lx + hx, foldY + 1, Skin[3], 0.49f);
                }
            for (int s2 = 0; s2 < 2; s2++)
                for (int i = 1; i <= 6; i++)
                    P(5 + i + hx, foldY - 1 + s2 + Mathf.Round(Mathf.Sin(t * 5f + i * 0.8f + s2 * 2f) * (i / 6f) * 1.6f + i * 0.25f), Linen[Mathf.Min(2, s2 + 1)], -0.1f);

            // ---- the crown: serpents knotted in a ring round her head, heads breaking out of it
            float limp = o.Limp, hiss = Mathf.Clamp01(o.Hiss);
            for (float a = 0f; a < 6.283f; a += 0.08f)
            {
                float r = 7f + Mathf.Sin(a * 5f + t * (2f + hiss * 6f)) * (0.8f + hiss * 0.6f) - limp * 1.5f;
                float x = hx + Mathf.Cos(a) * r, y = hy + 6f + Mathf.Sin(a) * r * 0.75f;
                if (y > hy + 9 + limp * 3f) continue;
                float z = Mathf.Sin(a) > 0.3f ? 0.4f : -0.3f;
                P(x, y, Hair[Mathf.FloorToInt(a * 6f) % 2], z); P(x, y + 1, Hair[2], z);
            }
            for (int k = 0; k < 6; k++)
            {
                float a = Mathf.PI * (1.08f + k * 0.17f), r = 7f;
                float x = hx + Mathf.Cos(a) * r, y = hy + 6f + Mathf.Sin(a) * r * 0.75f;
                float dir = a + Mathf.Sin(t * 3f + k) * 0.3f + limp * 1.2f * (Mathf.Cos(a) > 0f ? 1f : -1f);
                var pts = SnakePath(x, y, dir, 4 + Mathf.RoundToInt(hiss * 3f), t, k, 0.3f * (1f - hiss * 0.6f));
                Snake(pts, 0.42f);
                if (hiss > 0.3f)
                {   // reared and hissing: an open mouth and a flicking tongue
                    var p = pts[pts.Count - 1]; var q = pts[pts.Count - 2];
                    float ang = Mathf.Atan2(p.y - q.y, p.x - q.x);
                    P(p.x + Mathf.Cos(ang) * 2.4f, p.y + Mathf.Sin(ang) * 2.4f, Red[3], 0.45f);
                    if (Hash(k, Mathf.FloorToInt(t * 14f)) > 0.4f) P(p.x + Mathf.Cos(ang) * 3.4f, p.y + Mathf.Sin(ang) * 3.4f, Red[2], 0.45f);
                }
            }
            // the two across her brow, over the third eye, parting when it opens
            foreach (int s in new[] { -1, 1 })
            {
                var pts = new List<Vector2>();
                for (int i = 0; i <= 9; i++)
                {
                    float u = i / 9f, part = third * 4.5f;
                    pts.Add(new Vector2(hx - s * 4 + s * u * 8f - s * part * u,
                                        hy + 5 + u * 1.5f + (s < 0 ? 0f : Mathf.Sin(u * 3.14f) * 0.6f) - third * u * 2f + Mathf.Sin(t * 3f + u * 5f) * 0.3f));
                }
                Snake(pts, 0.48f + (s > 0 ? 0.01f : 0f));
            }

            // ---- her free arm: at rest on the coil, or gripping the blindfold
            {
                float up = Mathf.Clamp01(o.HandUp);
                var sh = new Vector2(5.5f, 17 + ty);
                var el = Vector2.Lerp(new Vector2(8, 23 + ty), new Vector2(9, 13 + hy), up);
                var hd = Vector2.Lerp(new Vector2(6.5f, 29 + ty), new Vector2(4 + hx, 10 + hy + Mathf.Round(fd * 5f)), up);
                Limb(sh, el, 1f, 0.6f + up * 0.2f); Limb(el, hd, 0.9f, 0.62f + up * 0.2f);
                P(hd.x, hd.y, Skin[1], 0.9f); P(hd.x + 1, hd.y, Skin[2], 0.9f);
            }

            // ---- the scales arm, and the scales
            {
                var sh = new Vector2(-5.5f, 17 + ty);
                float drop = Mathf.Clamp01(o.DropScales);
                Vector2 el, hd;
                if (drop > 0f) { el = new Vector2(-8, 23 + ty); hd = new Vector2(-9, 28 + ty); }
                else { el = new Vector2(-12, 13 + ty); hd = new Vector2(-15, 5 + ty); }
                Limb(sh, el, 1f, 0.55f); Limb(el, hd, 0.9f, 0.56f);
                P(hd.x, hd.y, Skin[1], 0.9f); P(hd.x, hd.y - 1, Skin[1], 0.9f);
                if (drop <= 0f) Scales(-15f, 7f + ty, Mathf.Clamp01(o.Tilt) * 0.45f);
                else if (drop < 1f) Scales(-15f - drop * 6f, 7f + ty + drop * 36f, drop * 1.2f);
                else
                {   // on the floor: the beam lying askew, both pans upturned
                    Line(-25, 44, -13, 46, (x, y, _) => P(x, y, Bronze[1], 1.5f));
                    P(-19, 44, Bronze[0], 1.5f); P(-19, 45, Bronze[0], 1.5f);
                    foreach (var (cx, cy) in new[] { (-27, 46), (-11, 48) })
                    {
                        for (int k = -3; k <= 3; k++) P(cx + k, cy, Bronze[Mathf.Abs(k) == 3 ? 2 : 1], 1.5f);
                        for (int k = -2; k <= 2; k++) P(cx + k, cy - 1, Bronze[k < 0 ? 0 : 1], 1.5f);
                    }
                }
            }

            // ---- out to the canvas: outline, then the texels (stone below the line; blocks falling)
            _drawn.Clear();
            for (int ly = GT; ly <= GB; ly++)
                for (int lx = GL; lx <= GR; lx++)
                    if (_has[(ly - GT) * GW + (lx - GL)]) _drawn.Add((lx, ly));
            bool Has(int lx, int ly) => lx >= GL && lx <= GR && ly >= GT && ly <= GB && _has[(ly - GT) * GW + (lx - GL)];
            if (shatter <= 0f)
                foreach (var (lx, ly) in _drawn)
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        if (!Has(lx + dx, ly + dy)) Over(ox + lx + dx, Base - (ly + dy), K, 0.9f);
            foreach (var (lx, ly) in _drawn)
            {
                var c = _col[(ly - GT) * GW + (lx - GL)];
                if (ly >= stoneLine)
                {
                    float l = (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) / 255f;
                    c = Stone[Mathf.Clamp(Mathf.FloorToInt((1f - l) * 5f), 0, 4)];
                }
                int x = ox + lx, y = Base - ly;
                if (shatter > 0f)
                {   // in blocks of 4 x 5: each starts falling on its own beat and lands in a heap
                    int bx = Mathf.FloorToInt((lx + 40) / 4f), by = Mathf.FloorToInt((ly + 20) / 5f);
                    float d = Hash(bx, by, 7), g = shatter - 0.1f - d * 0.5f;
                    if (g > 0f)
                    {
                        float heap = Height + 1 - Hash(bx, by, 3) * 2f - Mathf.Max(0f, 5f - Mathf.Abs(lx) * 0.3f);
                        float fy = Mathf.Min(heap, ly + g * g * 700f);
                        x += Mathf.RoundToInt((Hash(bx, by, 8) - 0.5f) * Mathf.Min(g, 0.3f) * 40f);
                        y = Base - Mathf.RoundToInt(fy);
                        Over(x, y - 1, K, 0.5f);
                    }
                }
                Over(x, y, c);
            }
        }

        void Scales(float px, float py, float tilt)
        {
            float L = 7f, dx = Mathf.Cos(tilt) * L, dy = Mathf.Sin(tilt) * L;
            Line(px - dx, py - dy, px + dx, py + dy, (x, y, _) => P(x, y, Bronze[1], 1.5f));
            P(px, py - 1, Bronze[0], 1.5f); P(px, py, Bronze[0], 1.5f);
            foreach (int s in new[] { -1, 1 })
            {
                float ex = px + s * dx, ey = py + s * dy;
                for (int k = 1; k <= 6; k++) { P(ex - 2 + k * 0.15f, ey + k, Bronze[2], 1.5f); P(ex + 2 - k * 0.15f, ey + k, Bronze[2], 1.5f); }
                for (int k = -3; k <= 3; k++) P(ex + k, ey + 7, Bronze[Mathf.Abs(k) == 3 ? 2 : 1], 1.5f);
                for (int k = -2; k <= 2; k++) P(ex + k, ey + 8, Bronze[2], 1.5f);
            }
        }
    }

    /// <summary>The living Medusa's picture, depth sorted on her body; her fight sets <see cref="Look"/>.</summary>
    public class MedusaFigure : MedusaCanvas
    {
        public MedusaLook Look;

        protected override bool Draw()
        {
            Look.Clock = Age;
            Shadow(0, 1f);
            Figure(Look, 0);
            return true;
        }
    }

    /// <summary>
    /// Her death (the user's pick, "her own stone"): stone climbs her from the tail to the crown,
    /// then she breaks into blocks that fall into a heap - left lying until the floor ends. Its own
    /// object under <see cref="GameBootstrap.FloorRoot"/>: the boss is destroyed the frame it dies.
    /// </summary>
    public class MedusaDeath : MedusaCanvas
    {
        const float Climb = 0.9f, Hold = 0.25f, Break = 0.9f;
        [SerializeField] MedusaLook _last;
        [SerializeField] bool _broke;

        public static void At(Vector2 at, MedusaLook last)
        {
            var go = new GameObject("medusa.death");
            if (GameBootstrap.FloorRoot != null) go.transform.SetParent(GameBootstrap.FloorRoot, false);
            go.transform.position = at;
            var p = Attach<MedusaDeath>(go.transform, "figure", Vector2.zero, 38, 10, 62, 0);
            last.EyeLit = 0f; last.Hiss = 0f; last.Loosen = 0f;
            p._last = last;
            DepthSorted.Attach(go, 0f, false, p.GetComponent<SpriteRenderer>());
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Petrify, at);
        }

        protected override bool Draw()
        {
            var o = _last;
            o.Clock = Mathf.Min(Age, Climb);   // still once she is stone
            float line = Height + 6 - Mathf.Clamp01(Age / Climb) * (Height + 20);
            float shatter = Mathf.Clamp01((Age - Climb - Hold) / Break);
            if (shatter > 0f && !_broke)
            {
                _broke = true;
                Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Rubble, transform.position);
                TexelFx.Dust(transform.parent != null ? (Vector2)transform.parent.position : (Vector2)transform.position, TexelFx.RubbleOf(true), 1.6f);
            }
            if (shatter <= 0f) Shadow(0, 1f);
            Figure(o, 0, line, Mathf.Max(0.0001f, shatter) * (shatter > 0f ? 1f : 0f));
            return true;
        }
    }
}
