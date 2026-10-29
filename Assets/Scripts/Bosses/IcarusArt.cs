using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// What Icarus looks like at one moment - everything the drawing reads, set by his fight each
    /// frame. Plain fields, so a domain reload keeps them.
    /// </summary>
    [System.Serializable]
    public struct IcarusLook
    {
        /// <summary>+1 facing right (east), -1 left. The face-on picture is mirrored, as the player's is.</summary>
        public int Dir;
        /// <summary>The blade's angle in his own frame: 0 straight ahead, negative up, positive down.</summary>
        public float Aim;
        /// <summary>0..1 lifted into a high two-handed guard, the hilt by his head (it raises the wing's arm).</summary>
        public float Raise;
        /// <summary>The fan: 0 none .. 1 a full two-handed sweep up across his body, the wing fanned.</summary>
        public float Fan;
        /// <summary>World units above the floor (the rise and the dive).</summary>
        public float Lift;
        public float Lean, Crouch, Stride, Shake;
        /// <summary>0..1 the wing glinting white (the fan's tell).</summary>
        public float Glint;
        /// <summary>0..1 the wing glowing red (the enrage).</summary>
        public float Heat;
        /// <summary>The star has risen off the guard (the well is open).</summary>
        public bool StarGone;
        /// <summary>Stars circling over the helm (the parried lunge's stun).</summary>
        public bool Dazed;
        /// <summary>The blow's smear: from and to (his frame), and how strong.</summary>
        public float SmearFrom, SmearTo, Smear;
        /// <summary>His death: 0..1 the blade's light thrown out every way, 0..1 he melts away.</summary>
        public float Rays, Melt;
    }

    /// <summary>
    /// Icarus (the user's design and references, 2026-10-28 - BOSSES.md "Icarus"): a DARK KNIGHT in
    /// black plate, drawn on the PLAYER'S JOINTS (neck, two shoulders, elbows, wrists and fists, the
    /// waist, hips, knees and feet) face-on and mirrored to turn as the player's rig is. Both arms reach
    /// the hilt by two-bone IK; the legs are posed from the feet. A plumed great helm, no face; one
    /// pauldron (the right); a navy shawl and a cape from the right shoulder, the plume the same navy;
    /// the WING OF STEEL FEATHERS on the left arm, from his back over the shoulder down the upper arm to
    /// the elbow, the whole arm armoured; the dying-star blade, two-handed, about 1.15x his height.
    ///
    /// Ported from the look mock's rig (y down there, in rig units 49 to the helm's top) at
    /// <see cref="KT"/> texels a rig unit, so he stands twice the player's height at the player's own
    /// density (37.5). Every texel goes through <see cref="Q"/>, which is where he melts.
    /// </summary>
    public abstract class IcarusCanvas : GroveCanvas
    {
        public const float Ppu37 = 37.5f;
        /// <summary>Texels a rig unit: the player is 0.7733 units (29 texels at 37.5), he is twice that to the helm.</summary>
        const float KT = 58f / 49.1f;

        static Color32 C(int hex) => new((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        public static readonly Color32[] Plate = { C(0x8a8a92), C(0x5c5c66), C(0x3c3c46), C(0x28282f), C(0x19191f), C(0x0e0e12) };
        public static readonly Color32[] Navy = { C(0x4a6488), C(0x2e4466), C(0x1c2c48), C(0x0e1a30) };
        public static readonly Color32[] Metal = { C(0xffffff), C(0xe2e6ec), C(0xb4bcc8), C(0x7c8694), C(0x4a525e) };
        public static readonly Color32[] Star = { C(0xfff0d0), C(0xffb466), C(0xf08a30), C(0xb04a10) };
        public static readonly Color32[] Steel = { C(0xffffff), C(0xe4eaf2), C(0xa8b4c4), C(0x6a7688) };
        public static readonly Color32[] Leather = { C(0xb06a3a), C(0x8a4e2a), C(0xd8d4cc) };
        public static readonly Color32[] Ember = { C(0xfff2b0), C(0xffb040), C(0xff6020), C(0xa02810) };
        public static readonly Color32[] Sky = { C(0xc9d6ee), C(0xa8badc), C(0x7d93bf) };
        public static readonly Color32 Black = C(0x050307), White = C(0xffffff);
        static readonly Color32 SunGlint = C(0xffe39a);

        IcarusLook _o;
        float _t;
        Vector2 _starAt;

        /// <summary>Where the star sat at the guard this frame, texels from the centre (y up).</summary>
        protected Vector2 StarAt => _starAt;

        // ---- the rig's space -> texels (y up), mirrored by the facing, lifted ----
        Vector2 T(float x, float y) => new(x * KT * _o.Dir + _o.Shake, (-y + _o.Lift * Ppu37 / KT) * KT);
        Vector2 T(Vector2 p) => T(p.x, p.y);

        /// <summary>Every texel of the figure passes here: the melt.</summary>
        void Q(float x, float y, Color32 c, float z)
        {
            if (_o.Melt > 0f && z < 9f)
            {
                float h = Hash(Mathf.RoundToInt(x), Mathf.RoundToInt(y), 77);
                float drop = Mathf.Clamp01(_o.Melt * 1.7f - h * 0.5f - (60f - y) / 120f * 0.3f);
                if (drop >= 1f) return;
                c = Lerp(c, drop > 0.2f ? Ember[1] : Star[1], Mathf.Min(1f, _o.Rays * 0.4f + drop));
                x += (h - 0.5f) * drop * 12f;
                y = Mathf.Max(0f, y - drop * 70f);
            }
            P(x, y, c, z);
        }

        protected static float Smooth(float t) => t * t * (3f - 2f * t);

        void Disc(Vector2 c, float r, System.Func<float, float, Color32> col, float z)
        {
            int e = Mathf.CeilToInt(r);
            for (int dy = -e; dy <= e; dy++)
                for (int dx = -e; dx <= e; dx++)
                    if (dx * dx + dy * dy <= r * r + 0.3f) Q(Mathf.Round(c.x) + dx, Mathf.Round(c.y) + dy, col(dx / Mathf.Max(0.5f, r), -dy / Mathf.Max(0.5f, r)), z);
        }

        void Stroke(Vector2 a, Vector2 b, System.Func<float, float> w, System.Func<int, int, float, Color32> col, float z)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) * 2f));
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, ww = w(u);
                var p = Vector2.Lerp(a, b, u);
                int cw = Mathf.CeilToInt(ww);
                for (int dy = -cw; dy <= cw; dy++)
                    for (int dx = -cw; dx <= cw; dx++)
                        if (dx * dx + dy * dy <= ww * ww + 0.25f)
                        {
                            int x = Mathf.RoundToInt(p.x + dx), y = Mathf.RoundToInt(p.y + dy);
                            Q(x, y, col(x, y, u), z);
                        }
            }
        }
        void Stroke(Vector2 a, Vector2 b, float w, System.Func<int, int, float, Color32> col, float z) => Stroke(a, b, _ => w, col, z);

        void Poly(Vector2[] pts, System.Action<int, int> fn)
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
        Vector2[] TP(params float[] xy) { var r = new Vector2[xy.Length / 2]; for (int i = 0; i < r.Length; i++) r[i] = T(xy[i * 2], xy[i * 2 + 1]); return r; }

        /// <summary>Two-bone IK: the joint for a limb from a to b; <paramref name="pick"/> chooses the side.</summary>
        static Vector2 Ik(Vector2 a, ref Vector2 b, float l1, float l2, System.Func<Vector2, Vector2, bool> pick)
        {
            var d = b - a; float len = d.magnitude, reach = l1 + l2 - 0.01f;
            if (len > reach) { b = a + d / len * reach; d = b - a; len = reach; }
            len = Mathf.Max(len, Mathf.Abs(l1 - l2) + 0.01f);
            float ca = (l1 * l1 + len * len - l2 * l2) / (2f * l1 * len), bas = Mathf.Atan2(d.y, d.x), off = Mathf.Acos(Mathf.Clamp(ca, -1f, 1f));
            var e1 = a + new Vector2(Mathf.Cos(bas + off), Mathf.Sin(bas + off)) * l1;
            var e2 = a + new Vector2(Mathf.Cos(bas - off), Mathf.Sin(bas - off)) * l1;
            return pick(e1, e2) ? e1 : e2;
        }

        // ------------------------------------------------------------------ the figure

        protected void Figure(IcarusLook o, float t)
        {
            _o = o; if (_o.Dir == 0) _o.Dir = 1; _t = t;
            Begin();
            float sp = o.Raise, fan = o.Fan;
            float fanW = fan > 0f ? Mathf.Clamp01(fan * 4f) * Mathf.Clamp01((1f - fan) * 4f + 0.001f) : 0f;
            float aim = fan > 0f ? Mathf.Lerp(o.Aim, Mathf.Lerp(1.1f, -2.3f, Smooth(Mathf.Clamp01(fan))), fanW) : o.Aim;
            float ca = Mathf.Cos(aim), sa = Mathf.Sin(aim);
            float lean = o.Lean, cr = o.Crouch;
            float upS = Mathf.Max(Mathf.Clamp01(sp), fanW * Mathf.Clamp01(fan * 1.6f));

            // the skeleton, in rig units (y down)
            var pelvis = new Vector2(lean * 0.35f, -23.5f + cr);
            var S = new Vector2(lean, -36.5f + cr * 1.1f);
            var hc = new Vector2(S.x + 0.3f, S.y - 5.8f);
            var hipL = new Vector2(pelvis.x - 2.8f, pelvis.y); var hipR = new Vector2(pelvis.x + 2.8f, pelvis.y);
            var footL = new Vector2(-4.4f, 0f); var footR = new Vector2(4.4f + o.Stride * 2.5f, 0f);
            var kneeL = Ik(hipL, ref footL, 12.2f, 11.8f, (a, b) => a.x < b.x);
            var kneeR = Ik(hipR, ref footR, 12.2f, 11.8f, (a, b) => a.x > b.x);
            var shL = new Vector2(S.x - 6f, S.y + 0.8f); var shR = new Vector2(S.x + 6f, S.y + 0.8f);
            var piv = new Vector2(Mathf.Lerp(S.x + 1.5f, S.x + 0.5f, upS), Mathf.Lerp(S.y + 3.5f, S.y - 6.5f, upS));
            float rr = Mathf.Lerp(7.8f, 3.5f, upS);
            var gripR = piv + new Vector2(ca, sa) * rr; var gripL = gripR - new Vector2(ca, sa) * 2.8f;
            System.Func<Vector2, Vector2, bool> lower = (a, b) => a.y > b.y;
            System.Func<Vector2, Vector2, bool> upOutR = (a, b) => a.y - 0.6f * a.x < b.y - 0.6f * b.x;
            System.Func<Vector2, Vector2, bool> upOutL = (a, b) => a.y + 0.6f * a.x < b.y + 0.6f * b.x;
            var handR = gripR; var elR = Ik(shR, ref handR, 6.6f, 6.6f, upS > 0.4f ? upOutR : lower);
            var handL = gripL; var elL = Ik(shL, ref handL, 6.6f, 6.6f, upS > 0.4f ? upOutL : lower);

            System.Func<float, Color32> pl = s => Ix(Plate, 0.4f + Mathf.Clamp01(s) * 4.2f);

            // ---- the cape: from the right shoulder only, flaring to a tattered hem at the floor; behind all ----
            {
                float sw = Mathf.Sin(t * 1.4f) * 0.8f;
                var pts = new System.Collections.Generic.List<Vector2> { T(S.x - 1f, S.y - 0.5f), T(S.x + 5.5f, S.y - 0.5f), T(S.x + 9f, S.y + 10f) };
                for (int i = 0; i <= 12; i++)
                {
                    float u = i / 12f, x = Mathf.Lerp(15f + sw, 1f, u), d = (i & 1) == 1 ? -3f - Hash(i, 2, 41) * 4f : 0f;
                    pts.Add(T(x + Mathf.Sin(t * 2f + i) * 0.4f, -0.5f + d));
                }
                pts.Add(T(S.x + 1f, S.y + 14f));
                var arr = pts.ToArray();
                Poly(arr, (x, y) =>
                {
                    float lx = x / KT, f = Mathf.Sin(lx * 0.9f + Mathf.Sin(y / KT * 0.12f + t) * 0.8f);
                    Q(x, y, f > 0.6f ? Navy[1] : f < -0.5f ? Navy[3] : Navy[2], -3f);
                });
            }

            // ---- legs: chevron greaves, knee cops, sabatons ----
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 hip = s < 0 ? hipL : hipR, knee = s < 0 ? kneeL : kneeR, foot = s < 0 ? footL : footR;
                float z = s < 0 ? 0.2f : 0.3f;
                Stroke(T(hip), T(knee), 2.6f * KT, (x, y, u) => pl((s < 0 ? 0.35f : 0.1f) + ((x / KT * _o.Dir - hip.x) * s < 0f ? 0f : 0.2f)), z);
                Stroke(T(knee), T(foot.x, foot.y - 1f), 2.2f * KT, (x, y, u) => pl(0.15f + u * 0.3f + (s < 0 ? 0.2f : 0f)), z);
                for (int i = 0; i < 3; i++)
                {
                    float u = 0.2f + i * 0.25f; var p = Vector2.Lerp(knee, foot, u);
                    Q(T(p.x - 0.8f, p.y).x, T(p.x - 0.8f, p.y).y, Plate[1], z + 0.02f); Q(T(p.x, p.y + 0.7f).x, T(p.x, p.y + 0.7f).y, Plate[1], z + 0.02f); Q(T(p.x + 0.8f, p.y).x, T(p.x + 0.8f, p.y).y, Plate[1], z + 0.02f);
                }
                Disc(T(knee), Mathf.Max(0.9f, 1.6f * KT), (nx, ny) => Plate[nx - ny < -0.3f ? 1 : 2], z + 0.05f);
                Stroke(T(foot.x - 1.4f, -0.6f), T(foot.x + 1.8f, -0.6f), 1.1f * KT, (x, y, u) => Plate[3], z + 0.04f);
            }

            // ---- faulds: layered chevrons, a long front plate to the knee ----
            for (int r = 0; r < 3; r++)
            {
                float px = pelvis.x, py = pelvis.y;
                Poly(TP(px - 5.6f - r * 0.4f, py - 3f + r * 2.4f, px, py - 1.6f + r * 2.4f, px + 5.6f + r * 0.4f, py - 3f + r * 2.4f,
                        px + 5.6f + r * 0.4f, py - 0.8f + r * 2.4f, px, py + 0.8f + r * 2.4f, px - 5.6f - r * 0.4f, py - 0.8f + r * 2.4f),
                    (x, y) => Q(x, y, pl(0.2f + (x / KT * _o.Dir - (px - 6f)) / 12f * 0.6f + r * 0.1f), 0.5f + r * 0.01f));
            }
            Poly(TP(pelvis.x - 2.2f, pelvis.y - 1f, pelvis.x + 2.2f, pelvis.y - 1f, pelvis.x + 1.8f, pelvis.y + 10f, pelvis.x, pelvis.y + 12f, pelvis.x - 1.8f, pelvis.y + 10f),
                (x, y) => Q(x, y, x * _o.Dir < T(pelvis).x * _o.Dir ? Plate[2] : Plate[3], 0.6f));

            // ---- the breastplate (nothing at the heart), chevrons ----
            Poly(TP(S.x - 6.2f, S.y, S.x + 6.2f, S.y, pelvis.x + 4.6f, pelvis.y - 2.6f, pelvis.x, pelvis.y - 0.6f, pelvis.x - 4.6f, pelvis.y - 2.6f),
                (x, y) => Q(x, y, pl(0.1f + (x / KT * _o.Dir - (S.x - 6f)) / 12f * 0.75f), 1f));
            foreach (float dy in new[] { 7f, 9.6f })
            {
                Stroke(T(S.x - 5f, S.y + dy - 2f), T(S.x, S.y + dy), Mathf.Max(0.5f, 0.45f * KT), (x, y, u) => Plate[4], 1.05f);
                Stroke(T(S.x, S.y + dy), T(S.x + 5f, S.y + dy - 2f), Mathf.Max(0.5f, 0.45f * KT), (x, y, u) => Plate[4], 1.05f);
            }
            // the strap that holds the wing on
            Stroke(T(S.x - 5.5f, S.y + 0.5f), T(pelvis.x + 4f, pelvis.y - 3f), Mathf.Max(0.5f, 0.9f * KT), (x, y, u) => Leather[u > 0.45f && u < 0.53f ? 2 : ((x + y) % 3 != 0 ? 0 : 1)], 1.2f);
            // the gorget: a high collar with points
            Poly(TP(S.x - 3.6f, S.y - 2.6f, S.x - 2f, S.y - 4f, S.x - 0.8f, S.y - 2.8f, S.x + 0.8f, S.y - 4.2f, S.x + 2.2f, S.y - 2.8f, S.x + 3.6f, S.y - 3.8f, S.x + 4.2f, S.y + 0.5f, S.x - 4.2f, S.y + 0.5f),
                (x, y) => Q(x, y, pl(0.2f + (x / KT * _o.Dir - (S.x - 4f)) / 8f * 0.6f), 1.3f));

            // ---- the wing on the left arm: back, shoulder, upper arm, stopping at the elbow; feathers to the knee ----
            {
                var ud = elL - shL; float ul = Mathf.Max(1e-3f, ud.magnitude);
                var past = elL + ud / ul * 1.5f;
                var spine = new[] { new Vector2(shL.x + 1.2f, shL.y - 2.4f), shL, elL, past };
                ArmWing(spine, fan > 0f ? Mathf.Clamp01(fan * 3f) : 0f, fan > 0.05f ? 4.95f : -2.2f);
                if (o.Heat > 0f)
                    for (int i = 0; i < 6 * o.Heat + 2; i++)
                    {
                        float ph = Mathf.Repeat(t * 1.1f + i * 0.19f, 1f), h = Hash(i, Mathf.FloorToInt(t * 1.1f + i * 0.19f), 5);
                        var p = T(shL.x - 2f - h * 6f + Mathf.Sin(ph * 6f + i), shL.y + h * 8f - ph * 10f);
                        Q(p.x, p.y, Ember[Mathf.Min(3, Mathf.FloorToInt(ph * 4f))], 6f);
                    }
            }

            // ---- the arms: the left armoured in steel scales to the hand; the right in plate ----
            ArmL(shL, elL, handL, fan > 0.05f ? 5f : 2.4f);
            ArmR(shR, elR, handR, 2.6f, pl);

            // ---- the right pauldron (none on the wing's side): three layered plates and a spike ----
            for (int r = 2; r >= 0; r--)
            {
                float oy = r * 1.6f, ox = r * 0.7f, x0 = shR.x, y0 = shR.y;
                int rr2 = r;
                Poly(TP(x0 - 3.2f + ox, y0 - 3.4f + oy, x0 + 1f + ox, y0 - 4.6f + oy, x0 + 4.6f + ox, y0 - 2.2f + oy, x0 + 5.6f + ox, y0 + 0.8f + oy, x0 + 2.6f + ox, y0 + 1.6f + oy, x0 - 2.4f + ox, y0 + 1f + oy),
                    (x, y) => Q(x, y, pl(0.1f + rr2 * 0.25f + ((x / KT * _o.Dir - x0) > 2f ? 0.25f : 0f)), 2.7f + (2 - rr2) * 0.02f));
            }
            Stroke(T(shR.x + 4f, shR.y - 2.6f), T(shR.x + 7.4f, shR.y - 6.4f), u => Mathf.Max(0.5f, 1.1f * KT * (1f - u)), (x, y, u) => Plate[1], 2.75f);

            // ---- the shawl (reference 2): a tall cowl round the neck, over the right shoulder in swags to a torn hem ----
            {
                Poly(TP(S.x - 3.4f, S.y - 5.4f, S.x + 3.6f, S.y - 5.6f, S.x + 4.6f, S.y - 1f, S.x - 4.4f, S.y - 0.6f),
                    (x, y) => Q(x, y, Mathf.Repeat(y / KT, 2.2f) < 0.8f ? Navy[2] : x * _o.Dir < T(S).x * _o.Dir ? Navy[0] : Navy[1], 4.6f));
                float hy = Mathf.Max(elR.y, shR.y + 6f) + 1.5f;
                var pts = new System.Collections.Generic.List<Vector2> { T(S.x - 1.5f, S.y - 1.5f), T(shR.x - 1f, shR.y - 4.6f), T(shR.x + 3.4f, shR.y - 3.8f), T(shR.x + 6f, shR.y - 0.6f), T(shR.x + 6.6f, shR.y + 4f) };
                for (int i = 0; i <= 8; i++)
                {
                    float u = i / 8f, d = (i & 1) == 1 ? -1.8f - Hash(i, 4, 9) * 1.8f : 0.6f;
                    pts.Add(T(Mathf.Lerp(shR.x + 6.2f, S.x - 1.5f, u), Mathf.Lerp(hy + 2f, S.y + 7.5f, u) + d + Mathf.Sin(t * 2.2f + i) * 0.3f));
                }
                Poly(pts.ToArray(), (x, y) =>
                {
                    float ly = -y / KT - shR.y, lx = x / KT * _o.Dir - shR.x, swag = Mathf.Sin(ly * 1.15f - lx * 0.25f + Mathf.Sin(lx * 0.4f + t) * 0.4f);
                    Q(x, y, swag > 0.55f ? Navy[2] : swag < -0.6f ? Navy[0] : Navy[1], 4.55f);
                });
            }

            // ---- the dying-star blade, in both fists ----
            Blade(handR, ca, sa, o);

            // ---- the helm: plumed, a slit visor, breathing holes, no face ----
            Helm(hc, pl, t);

            // ---- both fists on the hilt, over everything ----
            Disc(T(handL), Mathf.Max(0.9f, 1.7f * KT), (a, b) => Metal[a - b < -0.2f ? 0 : a - b < 0.4f ? 2 : 3], 6.5f);
            Disc(T(handR), Mathf.Max(0.9f, 1.7f * KT), (a, b) => Plate[a - b < -0.2f ? 1 : 3], 6.5f);

            Flush();

            // ---- over the outline: the smear, the stun's stars ----
            if (o.Smear > 0f) SmearArc(piv, rr + 52f, o.SmearFrom, o.SmearTo, o.Smear);
            if (o.Dazed)
                for (int i = 0; i < 3; i++)
                {
                    float a = t * 5f + i * Mathf.PI * 2f / 3f;
                    var p = T(hc.x + Mathf.Cos(a) * 6f, hc.y - 10f + Mathf.Sin(a) * 1.6f);
                    Over(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), SunGlint); Over(Mathf.RoundToInt(p.x) + 1, Mathf.RoundToInt(p.y), White);
                }
        }

        void ArmWing(Vector2[] spine, float fan, float z0)
        {
            float heat = _o.Heat;
            var segs = new float[spine.Length - 1]; float tot = 0f;
            for (int i = 0; i < segs.Length; i++) { segs[i] = Vector2.Distance(spine[i], spine[i + 1]); tot += segs[i]; }
            System.Func<float, Vector3> along = u =>
            {
                float d = u * tot;
                for (int i = 0; i < segs.Length; i++)
                {
                    if (d <= segs[i] || i == segs.Length - 1)
                    {
                        float r = segs[i] > 0f ? Mathf.Min(1f, d / segs[i]) : 0f;
                        var p = Vector2.Lerp(spine[i], spine[i + 1], r);
                        return new Vector3(p.x, p.y, Mathf.Atan2(spine[i + 1].y - spine[i].y, spine[i + 1].x - spine[i].x));
                    }
                    d -= segs[i];
                }
                return Vector3.zero;
            };
            var first = spine[0]; var last = spine[spine.Length - 1];
            float mean = Mathf.Atan2(last.y - first.y, last.x - first.x);
            // feathers fall to the side that is down (y+ in the rig) and away from the body (x-)
            float down = (Mathf.Sin(mean + Mathf.PI / 2f) - 0.6f * Mathf.Cos(mean + Mathf.PI / 2f)) > (Mathf.Sin(mean - Mathf.PI / 2f) - 0.6f * Mathf.Cos(mean - Mathf.PI / 2f)) ? 1f : -1f;
            const int N = 17; const float len = 21f;
            // ONE CONNECTED PIECE (the user's call): the wing's whole outline - along the arm, then back
            // along the longest feathers' tips - filled solid first, the feathers drawn over it as detail
            {
                var outline = new System.Collections.Generic.List<Vector2>();
                for (int i = 0; i < N; i++) { var b0 = along(i / (float)(N - 1)); outline.Add(T(b0.x, b0.y)); }
                for (int i = N - 1; i >= 0; i--)
                {
                    float u = i / (float)(N - 1);
                    var b0 = along(u);
                    float h0 = mean + down * Mathf.PI / 2f, hd = Mathf.Atan2(Mathf.Sin(Mathf.PI / 2f - h0), Mathf.Cos(Mathf.PI / 2f - h0));
                    float hang = h0 + hd * 0.35f * (1f - fan), outA = b0.z + down * 0.75f;
                    float dA = Mathf.Atan2(Mathf.Sin(outA - hang), Mathf.Cos(outA - hang));
                    float a = hang + dA * Mathf.Pow(u, 0.9f), L = len * (1f - 0.35f * u) * 0.97f;
                    outline.Add(T(b0.x + Mathf.Cos(a) * L, b0.y + Mathf.Sin(a) * L));
                }
                var poly = outline.ToArray();
                Poly(poly, (x, y) =>
                {
                    var c = Metal[2];
                    if (_o.Glint > 0f) c = Lerp(c, White, _o.Glint * 0.6f);
                    if (heat > 0f) c = Lerp(c, Ember[2], heat * 0.6f);
                    Q(x, y, c, z0 - 0.02f);
                });
            }
            for (int layer = 0; layer < 4; layer++)
                for (int i = 0; i < N; i++)
                {
                    float u = i / (float)(N - 1);
                    var b = along(u); float la = b.z;
                    // as in the user's photo: the inner feathers longest, hanging straight down; along the upper arm
                    // they shorten and sweep back and down. Angles blended the short way round.
                    float h0 = mean + down * Mathf.PI / 2f, hd = Mathf.Atan2(Mathf.Sin(Mathf.PI / 2f - h0), Mathf.Cos(Mathf.PI / 2f - h0));
                    float hang = h0 + hd * 0.35f * (1f - fan), outA = la + down * 0.75f;
                    float dA = Mathf.Atan2(Mathf.Sin(outA - hang), Mathf.Cos(outA - hang));
                    float a = hang + dA * Mathf.Pow(u, 0.9f) + down * layer * 0.05f;
                    float L = len * (1f - 0.35f * u) * (layer == 0 ? 1f : layer == 1 ? 0.74f : layer == 2 ? 0.5f : 0.28f);
                    var bp = new Vector2(b.x, b.y);
                    var ep = bp + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * L;
                    float pa = a + down * Mathf.PI / 2f; var off = new Vector2(Mathf.Cos(pa), Mathf.Sin(pa)) * 0.9f;
                    float zf = z0 + layer * 0.03f + i * 0.001f;
                    int ii = i, ll = layer;
                    Stroke(T(bp + off), T(ep + off), v => Mathf.Max(0.5f, (1.0f - v * 0.4f) * KT), (x, y, v) => heat > 0.5f ? Ember[3] : Metal[4], zf - 0.0005f);
                    Stroke(T(bp), T(ep), v => Mathf.Max(0.5f, (1.7f - v * 0.8f) * KT), (x, y, v) =>
                    {
                        var c = v > 0.86f ? Metal[3] : v > 0.25f && v < 0.45f && ll > 0 ? Metal[0] : Metal[((ii + ll) & 1) == 1 ? 1 : 2];
                        if (_o.Glint > 0f) c = Lerp(c, White, _o.Glint * (v > 0.3f ? 0.8f : 0.3f));
                        if (heat > 0f) c = Lerp(c, v > 0.6f ? Ember[2] : Ember[1], heat * (0.35f + 0.6f * v));
                        return c;
                    }, zf);
                }
        }

        void ArmL(Vector2 sh, Vector2 el, Vector2 hd, float z)
        {
            Stroke(T(sh), T(el), 1.8f * KT, (x, y, u) => Metal[(x + y) % 3 != 0 ? 2 : 3], z);
            Stroke(T(el), T(hd), 1.6f * KT, (x, y, u) => Metal[((x + y) & 1) == 1 ? 1 : 3], z + 0.02f);
            var f = hd - el; float fl = Mathf.Max(1e-3f, f.magnitude); var uv = f / fl;
            for (int i = 0; i < 6; i++)
            {
                // scales pointing down the arm toward the wrist, overlapping, alternating sides
                float u = 0.08f + i * 0.16f, sd = (i & 1) == 1 ? 1f : -1f; var p = Vector2.Lerp(el, hd, u);
                int ii = i;
                Stroke(T(p.x - uv.y * sd * 0.9f, p.y + uv.x * sd * 0.9f), T(p + uv * 2.2f), Mathf.Max(0.5f, 0.75f * KT), (x, y, v) => Metal[v > 0.7f ? 4 : (ii & 1) == 1 ? 0 : 1], z + 0.05f + i * 0.002f);
            }
            Disc(T(el), Mathf.Max(0.8f, 1.4f * KT), (nx, ny) => Metal[nx - ny < -0.3f ? 1 : 3], z + 0.03f);
        }

        void ArmR(Vector2 sh, Vector2 el, Vector2 hd, float z, System.Func<float, Color32> pl)
        {
            Stroke(T(sh), T(el), 1.8f * KT, (x, y, u) => pl(0.35f), z);
            Stroke(T(el), T(hd), 1.6f * KT, (x, y, u) => pl(0.45f), z + 0.02f);
            Disc(T(el), Mathf.Max(0.8f, 1.4f * KT), (nx, ny) => Plate[nx - ny < -0.3f ? 1 : 3], z + 0.03f);
        }

        void Blade(Vector2 g, float ca, float sa, IcarusLook o)
        {
            const float L = 52f, z0 = 3f;
            System.Func<float, Vector2> A0 = u => T(g.x + ca * u, g.y + sa * u);
            Stroke(A0(-6.5f), A0(0.5f), Mathf.Max(0.5f, 0.7f * KT), (x, y, u) => Mathf.FloorToInt(u * 9f) % 2 == 1 ? Leather[1] : Steel[3], z0);
            Disc(A0(-7f), Mathf.Max(0.7f, 1.1f * KT), (a, b) => Steel[2], z0);
            Disc(A0(1.6f), Mathf.Max(1f, 1.7f * KT), (a, b) => Steel[a - b < 0f ? 2 : 3], z0 + 0.05f);
            var s0 = A0(4.6f); float sr = Mathf.Max(1.2f, 2.6f * KT);
            _starAt = s0;
            // the blade's direction and its side, in texels
            var fwd = (A0(10f) - A0(0f)).normalized; var side = new Vector2(-fwd.y, fwd.x);
            if (!o.StarGone)
            {
                for (int s = -1; s <= 1; s += 2)
                    Stroke(s0, s0 + side * s * 5.5f * KT + fwd * 0.8f * KT, v => Mathf.Max(0.5f, 0.9f * KT * (1f - v)), (x, y, v) => Star[2], z0 + 0.1f);
                Disc(s0, sr + Mathf.Max(0.6f, 0.8f * KT), (a, b) => Star[1], z0 + 0.12f);
                Disc(s0, sr, (a, b) => Black, z0 + 0.14f);
            }
            else Disc(s0, sr * 0.55f, (a, b) => Star[3], z0 + 0.12f);   // the empty socket, still warm
            Stroke(A0(7.2f), A0(L), u => Mathf.Max(0.6f, 1.8f * KT * (1f - u * 0.7f)), (x, y, u) => Star[u > 0.92f ? 0 : (x + y) % 3 == 0 ? 2 : 1], z0 + 0.1f);
            Stroke(A0(7.2f), A0(L - 4f), u => Mathf.Max(0.45f, 1.05f * KT * (1f - u * 0.8f)), (x, y, u) => Black, z0 + 0.15f);
            foreach (float u in new[] { 10f, 13f })
            {
                var r = A0(u);
                Stroke(r - side * 2.2f * KT, r + side * 2.2f * KT, Mathf.Max(0.5f, 0.6f * KT), (x, y, v) => Steel[v < 0.5f ? 1 : 3], z0 + 0.2f);
            }
        }

        void Helm(Vector2 hc, System.Func<float, Color32> pl, float t)
        {
            Vector2 H(float x, float y) => T(hc.x + x, hc.y + y);
            var helm = new[] { H(-3.7f, -2f), H(-3.3f, -5.4f), H(-1.6f, -7f), H(1.6f, -7f), H(3.3f, -5.4f), H(3.7f, -2f), H(3.6f, 3.4f), H(2.1f, 5.6f), H(0f, 6.8f), H(-2.1f, 5.6f), H(-3.6f, 3.4f) };
            float hx0 = H(-3.7f, 0f).x, hy3 = H(0f, 3f).y;
            Poly(helm, (x, y) => Q(x, y, pl(0.05f + (x - hx0) * _o.Dir / (7.4f * KT) * 0.5f + (y < hy3 ? 0.2f : 0f)), 5f));
            Stroke(H(-3.8f, -1.4f), H(3.8f, -1.4f), Mathf.Max(0.5f, 0.5f * KT), (x, y, u) => Plate[4], 5.05f);
            Stroke(H(0f, -1.2f), H(0f, 5.8f), Mathf.Max(0.5f, 0.55f * KT), (x, y, u) => Plate[5], 5.1f);
            for (int s = -1; s <= 1; s += 2) Stroke(H(s * 1.4f, -1.2f), H(s * 0.6f, 5.4f), Mathf.Max(0.5f, 0.4f * KT), (x, y, u) => Plate[1], 5.08f);
            foreach (var d in new[] { new Vector2(1.8f, -4.2f), new Vector2(2.8f, -3.6f), new Vector2(2f, -3f), new Vector2(3f, -2.6f), new Vector2(1.4f, -2.6f) })
            {
                var p = H(d.x, d.y); Q(p.x, p.y, Plate[5], 5.12f);
            }
            // the plume, in the cloth's navy: up off the crown and curling back
            float sw = Mathf.Sin(t * 1.6f) * 0.5f;
            var plume = new[] { new Vector2(-0.4f, -6.9f), new Vector2(0.8f, -9.8f), new Vector2(-0.6f, -12.6f), new Vector2(-3.4f, -14.4f + sw), new Vector2(-6.6f, -13.6f + sw), new Vector2(-8.2f, -11.2f + sw) };
            for (int i = 0; i < plume.Length - 1; i++)
            {
                int ii = i;
                Stroke(H(plume[i].x, plume[i].y), H(plume[i + 1].x, plume[i + 1].y), v => Mathf.Max(0.5f, (1.6f - ii * 0.22f) * KT), (x, y, v) => ii < 1 ? Navy[2] : (x + y) % 4 == 0 ? Navy[0] : Navy[1], 4.9f + i * 0.001f);
            }
            Stroke(H(-0.4f, -9.6f), H(-3.6f, -11.2f + sw), v => Mathf.Max(0.5f, 0.9f * KT * (1f - v * 0.6f)), (x, y, v) => Navy[2], 4.95f);
            Stroke(H(-3.4f, -14.4f + sw), H(-5.2f, -16.4f + sw * 1.4f), v => Mathf.Max(0.5f, 0.9f * KT * (1f - v * 0.6f)), (x, y, v) => Navy[2], 4.95f);
            Stroke(H(-6.6f, -13.6f + sw), H(-9.6f, -14.4f + sw * 1.4f), v => Mathf.Max(0.5f, 0.9f * KT * (1f - v * 0.6f)), (x, y, v) => Navy[2], 4.95f);
            Stroke(H(0.4f, -8f), H(-1.8f, -13f), Mathf.Max(0.5f, 0.45f * KT), (x, y, u) => Navy[0], 4.96f);
        }

        /// <summary>The blow's swept path, hard-edged in the star's light (the player's SwingSmear rule).</summary>
        void SmearArc(Vector2 piv, float r, float a0, float a1, float al)
        {
            int n = Mathf.CeilToInt(Mathf.Abs(a1 - a0) * r * KT * 1.5f) + 2;
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, a = Mathf.Lerp(a0, a1, u), f = al * (0.35f + 0.65f * u);
                for (int k = 0; k < 4; k++)
                {
                    var p = T(piv.x + Mathf.Cos(a) * (r - k * 0.9f), piv.y + Mathf.Sin(a) * (r - k * 0.9f));
                    Over(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Star[k], f * (k < 2 ? 1f : 0.7f));
                }
            }
        }
    }

    public class IcarusFigure : IcarusCanvas
    {
        public IcarusLook Look = new() { Dir = 1, Aim = 0.75f };

        /// <summary>Where the star sits at the guard, in the world (the well rises from it).</summary>
        public Vector2 StarWorld => (transform.parent != null ? (Vector2)transform.parent.position : Vector2.zero) + Anchor + StarAt / Ppu37;

        protected override bool Draw()
        {
            Figure(Look, Age);
            return true;
        }
    }

    /// <summary>
    /// His death (the user's words): the sword's light stretches out every way as its centre burns
    /// white-hot; he forcefully PLANTS the sword point-down; he and the sword MELT AWAY - and only the
    /// star sphere from the middle of the blade is left, BLACKENED, on the floor. Its own object, lying
    /// there until the floor ends.
    /// </summary>
    public class IcarusDeath : IcarusCanvas
    {
        [SerializeField] IcarusLook _last;

        public static void At(Vector2 at, IcarusLook last)
        {
            var go = new GameObject("icarus.death");
            if (GameBootstrap.FloorRoot != null) go.transform.SetParent(GameBootstrap.FloorRoot, false);
            go.transform.position = at;
            var p = Attach<IcarusDeath>(go.transform, "figure", Vector2.zero, 90, 16, 150, 0, Ppu37);
            last.Lift = 0f; last.Fan = 0f; last.Glint = 0f; last.Smear = 0f; last.Dazed = false; last.StarGone = false; last.Shake = 0f;
            p._last = last;
            DepthSorted.Attach(go, 0f, false, p.GetComponent<SpriteRenderer>());
            Convergence.Art.Lighting.Glow.Flash(at + Vector2.up * 0.8f, 6f, new Color(1f, 0.72f, 0.4f), 3.2f, 1f);
        }

        protected override bool Draw()
        {
            var o = _last;
            float t = Age;
            float lift = Mathf.Clamp01(t / 0.35f), drive = Smooth(Mathf.Clamp01((t - 0.5f) / 0.25f));
            o.Aim = Mathf.Lerp(Mathf.Lerp(o.Aim, -1.4f, lift), 1.57f, drive);
            o.Raise = 0.6f * lift * (1f - drive);
            o.Crouch = drive * 5f;
            o.Rays = Smooth(Mathf.Clamp01((t - 0.9f) / 2.3f));
            o.Melt = Smooth(Mathf.Clamp01((t - 2.4f) / 2.8f));
            float fade = Smooth(Mathf.Clamp01((t - 5f) / 1f));

            if (o.Melt < 1f) Figure(o, Mathf.Min(t, 0.4f));
            else Begin();
            var star = StarAt;
            float sr = 2.6f * 58f / 49.1f + 0.8f;

            // the plant: the floor cracks under the point with a ring of dust
            if (t > 0.72f && t < 1.3f)
            {
                float u = (t - 0.72f) / 0.58f;
                Ring(star.x, 0f, 4f + u * 30f, 5.5f + u * 30f, (x, y, d, a) => Over(x, Mathf.RoundToInt(y * 0.4f), White, 0.55f * (1f - u)));
            }
            // the rays: the blade's light thrown out every way, growing, then gone
            if (o.Rays > 0f && fade < 1f)
                for (int i = 0; i < 24; i++)
                {
                    float a = i / 24f * Mathf.PI * 2f + Hash(i, 4) * 0.12f, L = o.Rays * (18f + Hash(i, 5) * 60f) * (1f - fade);
                    for (float r = 3f; r < L; r += 0.7f)
                        Over(Mathf.RoundToInt(star.x + Mathf.Cos(a) * r), Mathf.RoundToInt(star.y + Mathf.Sin(a) * r), Star[r < L * 0.3f ? 0 : r < L * 0.7f ? 1 : 2], (1f - r / L) * 0.9f * (1f - fade));
                }
            // the pool of what he was
            float pool = o.Melt * 24f * (1f - fade);
            for (int dx = -Mathf.RoundToInt(pool); dx <= pool; dx++) for (int dy = 0; dy <= 2; dy++) Over(dx, dy, Ember[dy == 0 ? 1 : 2], 0.85f * (1f - fade));

            // the sphere: white-hot while he burns, then it falls to the floor and cools BLACK, a last ember dying on its rim
            float fall = Smooth(Mathf.Clamp01((t - 5f) / 0.5f)), cool = Smooth(Mathf.Clamp01((t - 4.4f) / 2f));
            float sy = Mathf.Lerp(star.y, sr, fall * fall);
            float hot = (1f + o.Rays * 5f) * (1f - cool);
            for (int dy = -9; dy <= 9; dy++)
                for (int dx = -9; dx <= 9; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    int x = Mathf.RoundToInt(star.x) + dx, y = Mathf.RoundToInt(sy) + dy;
                    if (d < sr) Over(x, y, Lerp(Star[0], new Color32(26, 22, 20, 255), cool * (0.6f + 0.4f * (dx - dy > 0 ? 1f : 0.6f))));
                    else if (d < sr + 1f) Over(x, y, GroveCanvas.K, 0.9f);
                    if (d < hot && cool < 1f) Over(x, y, d < hot * 0.5f ? White : Star[0], (1f - cool) * 0.9f);
                }
            if (cool > 0.6f && t < 7.6f) Over(Mathf.RoundToInt(star.x + sr - 1f), Mathf.RoundToInt(sy + 1f), Ember[2], (0.5f + 0.5f * Mathf.Sin(t * 4f)) * (1f - Mathf.Clamp01((t - 6.4f) / 1.2f)));
            return true;
        }
    }
}
