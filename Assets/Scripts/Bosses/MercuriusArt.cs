using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// What Mercurius looks like at one moment - everything the drawing reads, set by its fight each
    /// frame. Plain fields, so a domain reload keeps them. Limbs by <see cref="Mercurius"/>'s numbers
    /// (ClawL, ClawR, Wings, Halo, Armour, Tail).
    /// </summary>
    [System.Serializable]
    public struct MercuriusLook
    {
        /// <summary>One bit per limb that is gone (the stump drips).</summary>
        public int Lost;
        /// <summary>The limb unwinding into the swirl (-1 none), and how far: 0 whole .. 1 all swirl.</summary>
        public int Spinning;
        public float Spin;
        /// <summary>The limb lifted as a phase gathers (-1 none), 0..1.</summary>
        public int Raised;
        public float Raise;
        /// <summary>0..1 slumped in a recovery.</summary>
        public float Sag;
        /// <summary>0 standing .. 1 under the floor's water (the regroup).</summary>
        public float Sink;
        public float Shake;
        public bool Immune;
        /// <summary>The recovery: bronze pieces circling back to <see cref="PieceLimb"/>'s stump,
        /// <see cref="Gather"/> 0 far out .. 1 home.</summary>
        public int PieceLimb, Pieces;
        public float Gather;
        /// <summary>The sequence's shape (0 beads .. 4 bones), hung round its head as a tell.</summary>
        public int Shape;
        /// <summary>Its death: 0..1 the water drained into the floor, 0..1 the hollow bronze toppled.</summary>
        public float Drain, Topple;
    }

    /// <summary>
    /// Mercurius (the user's picks, 2026-10-28): a serpent REARING front on out of a coil of water, like a
    /// cobra - water holding up bronze. Six limbs, each in its own place in the outline so its loss reads:
    /// two bronze CLAWS out to the sides, bronze-boned WINGS with water membranes, a rayed bronze HALO disc
    /// behind its head, ARMOUR of bronze scales down its belly and a collar, a TAIL of vertebrae ending in
    /// a bronze blade. Then the horned SKULL. Colours from the user's reference: sea-glass water, orange
    /// bronze, verdigris flecks.
    ///
    /// Drawn from the mock's coordinates (y down there) at S times its size; every shape is rasterised
    /// at the native grid, never scaled up texel by texel. Every texel goes through <see cref="Q"/>, which
    /// is where a limb unwinds into the swirl, sinks, drains or topples.
    /// </summary>
    public abstract class MercuriusCanvas : GroveCanvas
    {
        const float S = 1.5f;
        static Color32 C(int hex) => new((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        public static readonly Color32[] Water = { C(0xf4fffb), C(0xc4f7ea), C(0x8ee3d6), C(0x5cc4c0), C(0x3a94a0), C(0x24607a), C(0x173a52) };
        public static readonly Color32[] Bronze = { C(0xffe0a8), C(0xeaa95c), C(0xc67c38), C(0x94532a), C(0x5f331c), C(0x341b10) };
        public static readonly Color32[] Verd = { C(0xa8e6c8), C(0x5fae90), C(0x33705e) };
        static readonly Color32 Pale = C(0xc8d4e4);

        const int Torso = -1, Skull = -2;
        int _part;
        bool _skip;
        float _spinU, _t;
        MercuriusLook _o;
        readonly Vector2[] _stump = new Vector2[6];

        static Vector2 M(float x, float y) => new(x * S, -y * S);
        static bool IsWater(Color32 c) => c.b > c.r;

        Color32 Wc(int x, int y, float shade)
        {
            float f = Mathf.Sin(y * 0.4f - x * 0.2f + _t * 6f) + (FireArt.Noise(x * 0.23f + 40f, y * 0.23f + _t * 2.4f) - 0.5f) * 2.4f;
            return Ix(Water, 1.4f + Mathf.Clamp01(shade) * 3.2f + (f > 1.3f ? -1.4f : f < -0.9f ? 0.9f : 0f));
        }

        static Color32 Bc(int x, int y, float shade)
        {
            if (Hash(x, y, 11) < 0.06f) return Verd[Hash(x, y, 12) > 0.5f ? 1 : 2];
            return Ix(Bronze, 0.7f + Mathf.Clamp01(shade) * 2.8f);
        }

        void Part(int i)
        {
            _part = i;
            bool lost = i >= 0 && (_o.Lost & (1 << i)) != 0;
            _spinU = i >= 0 && i == _o.Spinning ? _o.Spin : 0f;
            _skip = lost && _spinU <= 0f;
        }

        /// <summary>Every texel of the figure passes here: the swirl, the drain, the topple, the sink.</summary>
        void Q(float x, float y, Color32 c, float z)
        {
            if (_skip) return;
            if (_spinU > 0f)
            {
                float su = Mathf.SmoothStep(0f, 1f, _spinU), cy = 24f * S;
                float h = Hash(Mathf.RoundToInt(x), Mathf.RoundToInt(y), 5);
                if (h < su * 0.45f) return;
                float dx = x, dy = y - cy, r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                float R = Tuning.Mercurius.SwirlRadius * TexelFx.Body;
                float r2 = r + (R + (h - 0.5f) * 10f - r) * su, a2 = a + su * (2.5f + h * 2f) + _t * 4f * su;
                x = Mathf.Cos(a2) * r2; y = cy + Mathf.Sin(a2) * r2 * 0.9f; z += 1f;
            }
            bool water = IsWater(c);
            if (_o.Drain > 0f && water)
            {
                if (_o.Drain > 0.95f) return;
                y -= _o.Drain * 80f * (0.5f + Hash(Mathf.RoundToInt(x), Mathf.RoundToInt(y), 13));
                if (y < 0f) return;
            }
            if (_o.Topple > 0f && !water)
            {
                float a = _o.Topple * 80f * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float nx = x * ca + y * sa, ny = -x * sa + y * ca;
                x = nx; y = Mathf.Max(1f, ny);
            }
            if (_o.Sink > 0f) { y -= _o.Sink * 110f; if (y < 0f) return; }
            x += _o.Shake;
            if (_o.Immune) c = Lerp(c, Pale, 0.35f);
            P(x, y, c, z);
        }

        void Disk(float cx, float cy, float r, System.Func<int, int, float, float, Color32> col, float z)
        {
            int e = Mathf.CeilToInt(r);
            for (int dy = -e; dy <= e; dy++)
                for (int dx = -e; dx <= e; dx++)
                {
                    if (dx * dx + dy * dy > r * r + 0.3f) continue;
                    int x = Mathf.RoundToInt(cx) + dx, y = Mathf.RoundToInt(cy) + dy;
                    Q(x, y, col(x, y, dx / Mathf.Max(0.5f, r), dy / Mathf.Max(0.5f, r)), z);
                }
        }

        void Stroke(Vector2 a, Vector2 b, float w, System.Func<int, int, float, Color32> col, float z)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) * 1.5f));
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n;
                var p = Vector2.Lerp(a, b, u);
                Disk(p.x, p.y, w, (x, y, nx, ny) => col(x, y, u), z);
            }
        }

        static void FillPoly(Vector2[] pts, System.Action<int, int> fn)
        {
            float x0 = 1e9f, x1 = -1e9f, y0 = 1e9f, y1 = -1e9f;
            foreach (var p in pts) { x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y); }
            for (int y = Mathf.FloorToInt(y0); y <= Mathf.CeilToInt(y1); y++)
                for (int x = Mathf.FloorToInt(x0); x <= Mathf.CeilToInt(x1); x++)
                {
                    bool ins = false;
                    for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                        if (((pts[i].y > y) != (pts[j].y > y)) && (x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)) ins = !ins;
                    if (ins) fn(x, y);
                }
        }

        static bool InTri(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            float S1(Vector2 p1, Vector2 p2) => (x - p2.x) * (p1.y - p2.y) - (p1.x - p2.x) * (y - p2.y);
            float d1 = S1(a, b), d2 = S1(b, c), d3 = S1(c, a);
            return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
        }

        // ------------------------------------------------------------------ the figure

        protected void Figure(MercuriusLook o, float t)
        {
            _o = o; _t = t;
            Begin();
            float sink = 1f - o.Sink;
            // the contact shadow and the ripple round its coil
            if (o.Drain < 1f && sink > 0.05f)
                for (int y = -6; y <= 6; y++)
                    for (int x = -26; x <= 26; x++)
                        if (x * x / 676f + y * y / 36f <= 1f) Over(x, y, K, 0.35f * sink);
            for (int k = 0; k < 3; k++)
            {
                float ph = Mathf.Repeat(t * 0.6f + k / 3f, 1f), r = 24f * (0.6f + ph * 1.2f);
                for (int i = 0; i < 96; i++) { float a = i / 96f * Mathf.PI * 2f; Over(Mathf.RoundToInt(Mathf.Cos(a) * r), Mathf.RoundToInt(Mathf.Sin(a) * r * 0.32f), Water[3], (1f - ph) * 0.35f); }
            }

            float bob = Mathf.Round(Mathf.Sin(t * 2f) + o.Sag * 3f);
            float Sway(float y) => Mathf.Sin(y * 0.15f + t * 2f) * 1.4f * (-y / 36f);

            // ---- the coil and the risen body (always there: the water itself) ----
            Part(Torso);
            for (int i = 0; i < 260; i++)
            {
                float a = i / 260f * Mathf.PI * 2f;
                var c = M(Mathf.Cos(a) * 13f, -3f + Mathf.Sin(a) * 3.5f);
                bool back = Mathf.Sin(a) < 0f;
                Disk(c.x, c.y, 2.4f * S, (x, y, nx, ny) => Wc(x, y, 0.5f - ny * 0.15f + Mathf.Cos(a) * 0.25f), back ? -0.3f : 0.2f);
            }
            for (float my = -4f; my >= -35f; my -= 0.5f)
            {
                float k = (-4f - my) / 31f, w = (4.6f - k * 1.4f) * S;
                var c = M(Sway(my), my + (my < -8f ? bob : 0f));
                Disk(c.x, c.y, w, (x, y, nx, ny) => Wc(x, y, 0.45f + nx * 0.35f - ny * 0.2f), 0.1f);
            }
            _stump[Mercurius.Armour] = M(0f, -20f + bob);

            // ---- the tail: vertebrae on a whip of water, a bronze blade at its tip ----
            Part(Mercurius.Tail);
            Vector2[] tail = { M(12, -3), M(18, -2), M(24, -4), M(27, -9), M(26, -13) };
            for (int s = 0; s < tail.Length - 1; s++)
                for (float u = 0f; u <= 1f; u += 0.1f)
                {
                    float g = (s + u) / (tail.Length - 1);
                    var p = Vector2.Lerp(tail[s], tail[s + 1], u);
                    Disk(p.x, p.y, (2.2f - g * 1.4f) * S, (x, y, nx, ny) => Wc(x, y, 0.4f + nx * 0.3f), 0.25f);
                    if (Mathf.Repeat(g * 10f, 1f) < 0.2f) { Q(p.x, p.y + 2f, Bronze[1], 0.3f); Q(p.x + 1, p.y + 2f, Bronze[2], 0.3f); Q(p.x, p.y + 3f, Bronze[0], 0.3f); }
                }
            {
                var tip = tail[tail.Length - 1];
                var dir = new Vector2(-0.2f, 1f).normalized; var side = new Vector2(dir.y, -dir.x);
                FillPoly(new[] { tip, tip + dir * 6f + side * 6f, tip + dir * 13f, tip + dir * 6f - side * 6f },
                    (x, y) => Q(x, y, Bc(x, y, 0.3f + (Vector2.Dot(new Vector2(x, y) - tip, side) > 0f ? 0f : 0.4f)), 0.31f));
            }
            _stump[Mercurius.Tail] = tail[0];

            // ---- the armour: bronze scales down its belly, a collar at its throat ----
            Part(Mercurius.Armour);
            for (float my = -31f; my <= -11f; my += 2f)
            {
                float hw = 3f - Mathf.Abs(my + 21f) * 0.06f, cx = Sway(my);
                for (float mx = -hw; mx <= hw + 0.01f; mx += 1f / S)
                {
                    var p = M(cx + mx, my + bob);
                    Q(p.x, p.y, Bc(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), 0.2f + (mx + hw) / (2f * hw) * 0.8f), 0.5f);
                    Q(p.x, p.y + 1f, Bc(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y) + 1, 0.2f + (mx + hw) / (2f * hw) * 0.8f), 0.5f);
                    Q(p.x, p.y - 1f, Bronze[3], 0.5f);
                }
            }
            for (float mx = -6f; mx <= 6f; mx += 1f / S)
            {
                var p = M(mx + Sway(-32f), -32f + bob + Mathf.Abs(mx) * 0.3f);
                Disk(p.x, p.y, 1.2f, (x, y, nx, ny) => Bc(x, y, 0.3f + (mx + 6f) / 12f * 0.6f - ny * 0.2f), 0.55f);
            }

            // ---- the claws: bronze forelegs out to the sides ----
            for (int s = -1; s <= 1; s += 2)
            {
                int limb = s < 0 ? Mercurius.ClawL : Mercurius.ClawR;
                Part(limb);
                float r = o.Raised == limb ? o.Raise : 0f;
                var sh = M(s * 5f + Sway(-27f), -27f + bob);
                var el = M(s * 13f, -24f + bob - r * 8f);
                var hd = M(s * (16f + r * 2f), -16f + bob - r * 20f + o.Sag * 3f);
                Stroke(sh, el, 1.5f * S, (x, y, u) => Bc(x, y, 0.25f + u * 0.3f), 0.6f);
                Stroke(el, hd, 1.1f * S, (x, y, u) => Bc(x, y, 0.35f + u * 0.4f), 0.61f);
                Q(el.x + s * 2f, el.y + 3f, Bronze[1], 0.62f); Q(el.x + s * 3f, el.y + 4f, Bronze[0], 0.62f); Q(el.x + s * 4f, el.y + 5f, Bronze[0], 0.62f);
                float ca0 = Mathf.Atan2(hd.y - el.y, hd.x - el.x);
                for (int f = -1; f <= 1; f++)
                {
                    float ca = ca0 + f * 0.7f;
                    for (int k = 1; k <= 5; k++) Q(hd.x + Mathf.Cos(ca) * k, hd.y + Mathf.Sin(ca) * k, Bronze[k >= 4 ? 0 : 1], 0.62f);
                }
                _stump[limb] = sh;
            }

            // ---- the wings: bronze spars, a water membrane bitten between the tips ----
            Part(Mercurius.Wings);
            for (int s = -1; s <= 1; s += 2)
            {
                float fl = Mathf.Sin(t * 3f + s) * 2f + o.Sag * 4f;
                if (o.Raised == Mercurius.Wings) fl -= o.Raise * 4f;
                var root = M(s * 3f + Sway(-30f), -30f + bob);
                Vector2[] tips = { M(s * 15f, -50f + bob + fl), M(s * 28f, -46f + bob + fl), M(s * 33f, -33f + bob + fl * 0.6f), M(s * 25f, -22f + bob + fl * 0.3f) };
                var poly = new Vector2[tips.Length + 1]; poly[0] = root; for (int i = 0; i < tips.Length; i++) poly[i + 1] = tips[i];
                float span = 0f; foreach (var q in tips) span = Mathf.Max(span, Vector2.Distance(q, root));
                FillPoly(poly, (x, y) =>
                {
                    for (int i = 0; i < tips.Length - 1; i++)
                    {
                        var mid = Vector2.Lerp((tips[i] + tips[i + 1]) * 0.5f, root, 0.3f);
                        if (InTri(x, y, tips[i], tips[i + 1], mid)) return;
                    }
                    Q(x, y, Wc(x, y, 0.15f + 0.55f * Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), root) / span)), -1f);
                });
                foreach (var q in tips) { Stroke(root, q, 0.8f, (x, y, u) => Ix(Bronze, 1.1f + u * 1.8f), -0.9f); Q(q.x, q.y, Bronze[0], -0.89f); }
                for (int i = 0; i < 6; i++)
                {
                    int k = Mathf.FloorToInt(t * 8f) + i * 13;
                    if (Hash(k, 7 + s, 3) > 0.5f) { var q = tips[i % tips.Length]; Q(q.x + (Hash(k, 1) - 0.5f) * 6f, q.y + Hash(k, 2) * 4f, Water[0], -0.88f); }
                }
                _stump[Mercurius.Wings] = root;
            }

            // ---- the halo: a rayed bronze disc behind its head ----
            Part(Mercurius.Halo);
            {
                var hc = M(Sway(-40f), -41f + bob - (o.Raised == Mercurius.Halo ? o.Raise * 3f : 0f));
                float R = 9f * S;
                for (int i = 0; i < 240; i++)
                {
                    float a = i / 240f * Mathf.PI * 2f;
                    bool lit = Mathf.Cos(a - t * 2f) > 0.75f;
                    for (int k = 0; k < 3; k++)
                        Q(hc.x + Mathf.Cos(a) * (R - k), hc.y + Mathf.Sin(a) * (R - k), lit && k == 0 ? Bronze[0] : k == 2 ? Bronze[3] : Bronze[2], -0.7f);
                }
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f + 0.13f;
                    for (int k = 1; k <= 5; k++) Q(hc.x + Mathf.Cos(a) * (R + k), hc.y + Mathf.Sin(a) * (R + k), Bronze[k >= 4 ? 3 : 1], -0.7f);
                }
                for (int i = 0; i < 6; i++)
                {
                    float a = -Hash(i, Mathf.FloorToInt(t * 3f), 9) * Mathf.PI, dr = Mathf.Repeat(t * 3f + i * 0.37f, 1f);
                    Q(hc.x + Mathf.Cos(a) * R, hc.y + Mathf.Sin(a) * R - dr * 9f, Water[1], -0.65f);
                }
                _stump[Mercurius.Halo] = hc;
            }

            // ---- the skull: horned bronze, water for eyes ----
            Part(Skull);
            {
                float hy = bob - 3f - Mathf.Round(o.Sag);
                for (float my = -42f; my <= -31f; my += 1f / S)
                {
                    int row = Mathf.FloorToInt(my);
                    float hw = row < -40 ? 3f : row < -38 ? 5f : row > -33 ? 3f : 5f;
                    for (float mx = -hw; mx <= hw + 0.01f; mx += 1f / S)
                    {
                        var p = M(mx, my + hy);
                        int ix = Mathf.RoundToInt(p.x), iy = Mathf.RoundToInt(p.y), col = Mathf.FloorToInt(mx + 0.5f);
                        Color32 c = Bc(ix, iy, (mx + hw) / (2f * hw) * 0.9f + (row < -40 ? -0.3f : 0f));
                        if (row == -36 && Mathf.Abs(col) <= 3 && col != 0) c = Water[o.Immune ? 4 : 1];
                        if (row == -37 && Mathf.Abs(col) <= 3 && col != 0) c = Bronze[4];
                        if (row >= -32 && ((col + 10) & 1) == 0) c = Bronze[4];
                        Q(p.x, p.y, c, 0.7f);
                    }
                }
                for (int s = -1; s <= 1; s += 2)
                {
                    System.Func<int, int, float, Color32> hc = (x, y, u) => Ix(Bronze, 1.2f + u * 1.8f);
                    Stroke(M(s * 4f, -41f + hy), M(s * 9f, -49f + hy), 0.8f, hc, 0.72f);
                    Stroke(M(s * 7f, -45f + hy), M(s * 4f, -50f + hy), 0.6f, hc, 0.72f);
                    Stroke(M(s * 5f, -38f + hy), M(s * 10f, -41f + hy), 0.6f, hc, 0.72f);
                }
            }

            // ---- drips from every stump, the pieces circling back, the shape it carries ----
            Part(Torso);
            for (int i = 0; i < 6; i++)
            {
                if ((o.Lost & (1 << i)) == 0 || i == o.Spinning) continue;
                var s = _stump[i];
                for (int k = 0; k < 2; k++) Q(s.x + k, s.y - 1f - Mathf.Floor(Mathf.Repeat(t * 9f + k * 2.3f + s.x, 6f)), Water[2 + k], 2f);
            }
            if (o.Pieces > 0 && o.PieceLimb >= 0 && o.PieceLimb < 6)
            {
                var s = _stump[o.PieceLimb];
                float r = Mathf.Lerp(40f, 3f, Mathf.SmoothStep(0f, 1f, o.Gather));
                for (int i = 0; i < o.Pieces; i++)
                {
                    float a = i / (float)Tuning.Mercurius.Pieces * Mathf.PI * 2f + t * 1.6f;
                    float x = s.x + Mathf.Cos(a) * r, y = s.y + Mathf.Sin(a) * r * 0.6f;
                    Q(x, y, Bronze[1], 3f); Q(x + 1, y, Bronze[2], 3f); Q(x, y - 1, Bronze[2], 3f); Q(x + 1, y - 1, Bronze[3], 3f); Q(x - 1, y, Bronze[0], 3f);
                }
            }
            if (o.Sink < 0.5f && o.Drain <= 0f)
            {
                var head = M(0f, -38f + bob);
                for (int i = 0; i < 5; i++)
                {
                    float a = t * 0.9f + i * Mathf.PI * 2f / 5f;
                    float x = head.x + Mathf.Cos(a) * 28f, y = head.y + Mathf.Sin(a) * 10f;
                    ShapeIcon(o.Shape, x, y, a, Mathf.Sin(a) > 0f ? -1.2f : 2.5f);
                }
            }
            Flush();
        }

        /// <summary>The sequence's projectile, small - the tell that says which one it is.</summary>
        void ShapeIcon(int shape, float x, float y, float a, float z)
        {
            switch (shape)
            {
                case 0: Q(x, y, Water[0], z); Q(x + 1, y, Water[1], z); Q(x, y - 1, Water[2], z); Q(x + 1, y - 1, Water[3], z); break;
                case 1: for (int k = -1; k <= 2; k++) Q(x + k, y + k * 0.5f, Bronze[k == 2 ? 0 : 2], z); break;
                case 2: for (int k = 0; k < 8; k++) { float b = k / 8f * Mathf.PI * 2f; Q(x + Mathf.Cos(b) * 1.6f, y + Mathf.Sin(b) * 1.6f, Bronze[k < 4 ? 1 : 3], z); } break;
                case 3: for (int k = -2; k <= 2; k++) Q(x + k, y + (Mathf.Abs(k) == 2 ? -1 : 0), Water[Mathf.Abs(k) == 2 ? 2 : 0], z); break;
                default: Q(x, y, Bronze[1], z); Q(x + 1, y, Bronze[2], z); Q(x + 2, y, Water[2], z); Q(x + 3, y, Bronze[1], z); break;
            }
        }
    }

    public class MercuriusFigure : MercuriusCanvas
    {
        public MercuriusLook Look = new() { Spinning = -1, Raised = -1, PieceLimb = -1 };

        protected override bool Draw()
        {
            Figure(Look, Age);
            return true;
        }
    }

    /// <summary>
    /// Its death (the user's pick, "hollow armour"): the water drains into the floor, the empty bronze
    /// stands a moment, then topples. Its own object, lying there until the floor ends.
    /// </summary>
    public class MercuriusDeath : MercuriusCanvas
    {
        [SerializeField] MercuriusLook _last;

        public static void At(Vector2 at, MercuriusLook last)
        {
            var go = new GameObject("mercurius.death");
            if (GameBootstrap.FloorRoot != null) go.transform.SetParent(GameBootstrap.FloorRoot, false);
            go.transform.position = at;
            var p = Attach<MercuriusDeath>(go.transform, "figure", Vector2.zero, 64, 12, 100, 0);
            last.Spinning = -1; last.Raised = -1; last.Pieces = 0; last.Sag = 0f; last.Sink = 0f; last.Shake = 0f; last.Immune = false;
            p._last = last;
            DepthSorted.Attach(go, 0f, false, p.GetComponent<SpriteRenderer>());
        }

        protected override bool Draw()
        {
            var o = _last;
            o.Drain = Mathf.Clamp01((Age - 0.2f) / 1.0f);
            o.Topple = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Age - 1.8f) / 0.7f));
            Figure(o, Mathf.Min(Age, 0.2f));
            return true;
        }
    }
}
