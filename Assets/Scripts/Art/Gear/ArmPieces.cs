using System;
using System.Collections.Generic;
using UnityEngine;
using static Convergence.Art.Gear.TexelKit;
using Shader = Convergence.Art.Gear.TexelKit.Shader;

namespace Convergence.Art.Gear
{
    /// <summary>What an arm piece is drawn from this frame - filled by the rig (PrimitiveCharacterRig.ArmPiece.cs).</summary>
    public sealed class ArmPieceState
    {
        public float T;
        public bool Walk;
        public Core.ElementType Element;
        /// <summary>Where a point on the shoulder piece is now, in the picture's coordinates: the shoulder
        /// turns with the arm (the mock's shoulderPoint). Particles rise from there.</summary>
        public Func<float, float, Vector2> Sp;
    }

    /// <summary>
    /// THE ARM PIECES (the user's eleven, picked over six mock rounds 2026-11-08/09 - Tools/tier-art/arms.js;
    /// the mock is the source, this its port, number for number): accessories (GearSlot.Ring) that REPLACE
    /// the whole near arm, the weapon arm, from the shoulder to the fingertips. Each draws layers tagged with
    /// the joint they ride: <see cref="JArm"/> (cut at the elbow and wrist as the rig cuts every arm),
    /// <see cref="JShoulder"/> (the shoulder piece, which turns with the arm) or <see cref="JTorso"/> (a chest
    /// strap, embers). <see cref="Layer.Rise"/> marks what stands above the shoulder: from behind it draws
    /// over the cape. Coordinates are the mock's: body texels, y down, the near arm at x 0..9.
    /// </summary>
    public static class ArmPieces
    {
        public const int JArm = 0, JShoulder = 1, JTorso = 2;

        /// <summary>The shoulder piece's centre on the arm's axis; it turns about this (the mock's JOINT.cap).</summary>
        public static readonly Vector2 Cap = new(4.5f, 34f);
        /// <summary>The arm piece's shoulder turns with this share of the upper arm's angle (the user's
        /// wider arc, 2026-11-08: 0.8, up from 0.5).</summary>
        public const float ShoulderShare = 0.8f;

        public static bool Is(AccessoryKind k) => k >= AccessoryKind.SalamanderArm;

        /// <summary>The whole piece's opacity - the afterimage is see-through.</summary>
        public static float Alpha(AccessoryKind k) => k == AccessoryKind.PenumbraArm ? 0.72f : 1f;

        /// <summary>Copies drawn where the arm was a moment ago, fainter (seconds back, alpha) - the
        /// afterimage's trail.</summary>
        public static readonly (float dt, float alpha)[] PenumbraEcho = { (0.12f, 0.42f), (0.24f, 0.2f) };

        /// <summary>The torso layers of these sit OVER the arm and head (embers) rather than under them (a strap).</summary>
        public static bool TorsoOver(AccessoryKind k) => k == AccessoryKind.SalamanderArm;

        // ---------------------------------------------------------------- palette (the mock's AR)

        static readonly Tones Obsidian = Ramp("#2e2632", "#a898c0", 0.36f), Lava = Ramp("#ff6418", "#fff2a0", 0.55f, 0.4f),
            Horn = Ramp("#9a8060", "#fff0d0", 0.45f, 0.4f), Merc = Ramp("#7f92ae", "#ffffff", 0.62f, 0.5f),
            Salt = Ramp("#dcc8d0", "#ffffff", 0.55f, 0.3f), Rose = Ramp("#d88aa2", "#fff0f4", 0.5f, 0.32f),
            SaltRock = Ramp("#7c7078", "#e0d4d8", 0.34f), Iron = Ramp("#4c4e58", "#bcc4d4", 0.32f),
            Ember = Ramp("#ff7a22", "#ffe28a", 0.5f, 0.45f), Leather = Ramp("#5e3c26", "#c89868", 0.3f),
            Brass = Ramp("#a8823a", "#ffe6a0", 0.42f), Gold = Ramp("#e0a82a", "#fff6b8", 0.5f),
            Copper = Ramp("#b0663a", "#ffd0a0", 0.38f), Patina = Ramp("#4f9e88", "#c8f4e0", 0.36f),
            Lacquer = Ramp("#8a1424", "#ff9a8a", 0.4f), Stone = Ramp("#d0142e", "#ffb0a0", 0.5f),
            Violet = Ramp("#7a52d8", "#e4d8ff", 0.45f), Blood = Ramp("#7c0c1c", "#ff7070", 0.45f, 0.42f),
            BloodGlass = Ramp("#b8142c", "#ffc8c8", 0.5f), Volt = Ramp("#2e62e0", "#eefaff", 0.58f),
            Ghost = Ramp("#1c1828", "#7a6aa8", 0.36f), Steel = Ramp("#9aa6b4", "#ffffff", 0.52f, 0.36f),
            Cable = Ramp("#2a2a32", "#9c9cb4", 0.5f), Synth = Ramp("#2c2c34", "#9a9ab0", 0.42f);
        static readonly Color32 White = Hex("#ffffff"), Cyan = Hex("#7af0ff");

        // ---------------------------------------------------------------- helpers

        static Vector2 V(float x, float y) => new(x, y);
        static float Lerp(float a, float b, float k) => a + (b - a) * k;
        static float Hypot(float x, float y) => Mathf.Sqrt(x * x + y * y);
        /// <summary>Texel noise, 0..1 (the mock's hash2 - a different stream, the same job).</summary>
        static float H2(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967296f;
            }
        }
        static int Cyl(float cx, float rx, int x, float bias = 0f)
        {
            float nx = Mathf.Clamp((x + 0.5f - cx) / rx, -1f, 1f);
            return LitTone(nx, -0.15f, Mathf.Sqrt(1f - nx * nx), bias);
        }
        static int DomeI(float cx, float cy, float rx, float ry, int x, int y, float bias = 0f)
        {
            float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry, d = nx * nx + ny * ny;
            if (d > 1f) { float s = 1f / Mathf.Sqrt(d); nx *= s; ny *= s; d = 1f; }
            return LitTone(nx, ny, Mathf.Sqrt(1f - d), bias);
        }
        static Layer AL(float depth, int joint, bool rise = false, bool outlined = true, int outR = OutR) =>
            new(depth) { Joint = joint, Rise = rise, Outlined = outlined, OutR = outR };
        static int Mod(int a, int m) => ((a % m) + m) % m;

        /// <summary>A thick line of texels from A to B, w wide, round brush; col gets (x, y, k along).</summary>
        static void Stroke(Layer l, float ax, float ay, float bx, float by, float w, Func<int, int, float, Color32> col)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Hypot(bx - ax, by - ay) * 2f));
            float r = w / 2f;
            for (int i = 0; i <= n; i++)
            {
                float x = Lerp(ax, bx, (float)i / n), y = Lerp(ay, by, (float)i / n);
                for (int yy = Mathf.FloorToInt(y - r); yy <= Mathf.CeilToInt(y + r); yy++)
                    for (int xx = Mathf.FloorToInt(x - r); xx <= Mathf.CeilToInt(x + r); xx++)
                        if ((xx + 0.5f - x) * (xx + 0.5f - x) + (yy + 0.5f - y) * (yy + 0.5f - y) <= r * r + 0.15f)
                            l.Set(xx, yy, col(xx, yy, (float)i / n));
            }
        }
        static void Stroke(Layer l, float ax, float ay, float bx, float by, float w, Color32 c) => Stroke(l, ax, ay, bx, by, w, (_, _, _) => c);

        static Mask PrismMask(float bx, float by, float dx, float dy, float len, float w)
        {
            float L0 = Hypot(dx, dy); dx /= L0; dy /= L0;
            float nx = -dy * w / 2f, ny = dx * w / 2f, s = len - w * 0.8f;
            return Poly(V(bx + nx, by + ny), V(bx + nx + dx * s, by + ny + dy * s), V(bx + dx * len, by + dy * len),
                        V(bx - nx + dx * s, by - ny + dy * s), V(bx - nx, by - ny));
        }
        /// <summary>A crystal prism from its base along (dx, dy), its face to the upper left lit.</summary>
        static void Prism(Layer l, float bx, float by, float dx, float dy, float len, float w, Tones r)
        {
            float L0 = Hypot(dx, dy), ux = dx / L0, uy = dy / L0;
            l.Fill(PrismMask(bx, by, dx, dy, len, w), (x, y, _) =>
            {
                float px = x + 0.5f - bx, py = y + 0.5f - by, along = px * ux + py * uy, across = (px * -uy + py * ux) / (w / 2f);
                bool litSide = (-uy) * -0.6f + ux * -0.7f > 0f ? across > 0f : across < 0f;
                if (along > len - w * 0.8f - 0.5f) return r[litSide ? 5 : 3];
                if (Mathf.Abs(across) < 0.25f) return r[5];
                return r[litSide ? 4 : 2];
            });
        }

        static Mask ArmCol(float w = 0f) => Poly(V(-1 - w, 37), V(10 + w, 37), V(10 + w, 61), V(-1 - w, 61));
        static Mask FistM(float r = 5.2f) => Ell(4.8f, 65f, r, r * 0.88f);

        /// <summary>A jagged bolt from A to B, re-rolled each frame: a white core, a blue fringe.</summary>
        static void Zap(Layer l, float ax, float ay, float bx, float by, int seed, int frame, float jag = 3f, int n = 5)
        {
            float px = ax, py = ay;
            for (int i = 1; i <= n; i++)
            {
                float k = (float)i / n;
                float x = Lerp(ax, bx, k) + (i == n ? 0f : (H2(frame * 13 + seed, i) - 0.5f) * jag * 2f);
                float y = Lerp(ay, by, k) + (i == n ? 0f : (H2(seed * 7 + i, frame) - 0.5f) * jag);
                l.Line(px, py, x, y, White, false);
                l.Line(px + 1, py, x + 1, y, Volt[4], false);
                px = x; py = y;
            }
        }

        // ---------------------------------------------------------------- lights

        static List<AccessoryGlow> _glows;
        /// <summary>A light on the piece: on the arm it rides the upper arm or (from the elbow down, in the
        /// forearm picture's unstretched rows) the forearm.</summary>
        static void Glow(float x, float y, float r, string hex, float a, int joint)
        {
            if (_glows == null) return;
            int j = joint == JShoulder ? 1 : joint == JTorso ? 0 : y < ElbowRow ? 2 : 3;
            if (j == 3) y -= Accessories.ElbowExtra;
            _glows.Add(new AccessoryGlow { At = new Vector2(x, y), R = r, A = a, C = Hex(hex), Joint = j });
        }

        /// <summary>The elbow joint's row in the (stretched) arm picture: the arm is cut here.</summary>
        public const int ElbowRow = 50;

        // ---------------------------------------------------------------- drawing

        /// <summary>Draw <paramref name="kind"/> into <paramref name="layers"/> (cleared first).</summary>
        public static void Draw(AccessoryKind kind, ArmPieceState st, List<Layer> layers, List<AccessoryGlow> glows = null)
        {
            layers.Clear();
            glows?.Clear();
            _glows = glows;
            st.Sp ??= (x, y) => new Vector2(x, y);
            switch (kind)
            {
                case AccessoryKind.SalamanderArm: Salamander(st, layers); break;
                case AccessoryKind.QuicksilverArm: Quicksilver(st, layers); break;
                case AccessoryKind.SaltArm: SaltArm(st, layers); break;
                case AccessoryKind.CrucibleArm: Crucible(st, layers); break;
                case AccessoryKind.VerdigrisArm: Verdigris(st, layers); break;
                case AccessoryKind.RubedoArm: Rubedo(st, layers); break;
                case AccessoryKind.SanguisArm: SanguisCrystal(st, layers); break;
                case AccessoryKind.FulmenArm: FulmenCage(st, layers); break;
                case AccessoryKind.PenumbraArm: PenumbraEchoArm(st, layers); break;
                case AccessoryKind.HomunculusPlatedArm: HomunculusPlated(st, layers); break;
                case AccessoryKind.HomunculusHydraulicArm: HomunculusHydraulic(st, layers); break;
            }
            _glows = null;
        }

        // 1. SALAMANDER (sulphur): obsidian scales split by lava seams that pulse down the arm; horns off the
        // shoulder, fins down the forearm, three talons past the fist, embers lifting off the shoulder.
        static void Salamander(ArmPieceState st, List<Layer> o)
        {
            float t = st.T;
            Layer arm = AL(12.6f, JArm), fist = AL(13.1f, JArm), claw = AL(13.0f, JArm), sh = AL(14.2f, JShoulder), horns = AL(14.1f, JShoulder, rise: true);
            Color32 Scales(int x, int y, int i)
            {
                int v = Mod(y, 3), u = Mod(x + (Mathf.FloorToInt(y / 3f) % 2 != 0 ? 1 : 0), 3);
                if (v == 2) i -= 1; else if (v == 0 && u == 1) i += 1;
                return Obsidian[i];
            }
            arm.Fill(Poly(V(-1, 37), V(10, 36), V(10, 49), V(9, 50.5f), V(10, 52), V(10, 61), V(-1, 61), V(-1, 52), V(0, 50.5f), V(-1, 49)),
                     (x, y, _) => Scales(x, y, Cyl(4.5f, 6.5f, x, 0.25f)));
            foreach (var (y0, y1, tip) in new[] { (51f, 56f, -6f), (56f, 61f, -5f) })
                arm.Fill(Poly(V(0, y0), V(0, y1), V(tip, y1 + 0.5f)), (x, y, _) => Horn[y < (y0 + y1) / 2f + 1f ? 3 : 2]);
            fist.Fill(Ell(4.8f, 65f, 5.4f, 4.6f), (x, y, _) => Obsidian[DomeI(4, 64, 6, 5, x, y)]);
            foreach (var (x0, y0, x1, y1) in new[] { (1.5f, 67f, -0.5f, 76f), (5f, 68.5f, 5f, 78f), (8.5f, 67f, 10.5f, 75.5f) })
                Stroke(claw, x0, y0, x1, y1, 2.4f, (x, y, k) => Horn[k > 0.7f ? 5 : x <= Mathf.Min(x0, x1) + 0.5f ? 4 : 2]);
            sh.Fill(Ell(4f, 35f, 7.8f, 6.2f), (x, y, _) => Scales(x, y, DomeI(3, 33, 8, 7, x, y, 0.25f)));
            foreach (var p in new[] { new[] { V(-5, 35), V(-1.5f, 30.5f), V(-11, 23) }, new[] { V(-1.5f, 30), V(3, 29), V(-2, 19.5f) }, new[] { V(3.5f, 29), V(7.5f, 29.5f), V(6, 21.5f) } })
            {
                float mid = (p[0].x + p[1].x) / 2f;
                horns.Fill(Poly(p), (x, y, _) => Horn[x < mid ? 4 : 2]);
            }
            void Seam(Layer l, Vector2[] pts)
            {
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    var (a, b) = (pts[i], pts[i + 1]);
                    int n = Mathf.RoundToInt(Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)) * 2f);
                    for (int k = 0; k <= n; k++)
                    {
                        int x = R(Lerp(a.x, b.x, (float)k / n)), y = R(Lerp(a.y, b.y, (float)k / n));
                        if (!l.Has(x, y)) continue;
                        float run = Mathf.Sin(t * 3.2f - y * 0.35f);
                        l.Set(x, y, Lava[run > 0.5f ? 5 : run > -0.3f ? 4 : 3]);
                    }
                }
            }
            Seam(arm, new[] { V(3, 37), V(5, 40), V(3, 44), V(6, 48), V(4, 52), V(6, 55), V(3, 58), V(5, 61) });
            Seam(arm, new[] { V(8, 41), V(7, 45), V(8, 49) });
            Seam(sh, new[] { V(-2, 36), V(1, 33), V(4, 35), V(7, 32), V(10, 34) });
            foreach (int x in new[] { 2, 5, 8 }) fist.Dot(x, 63, Lava[4]);
            var em = AL(15f, JTorso, rise: true, outlined: false);
            for (int i = 0; i < 4; i++)
            {
                float p = Mathf.Repeat(t * 0.55f + i * 0.27f, 1f);
                var o0 = st.Sp(-1 + i * 3, 29);
                int x = R(o0.x + Mathf.Sin(t * 2 + i * 2) * 1.5f), y = R(o0.y - p * 22f);
                if (p < 0.9f) em.Fill(Rect(x, y, x + (p < 0.5f ? 1 : 0), y + (p < 0.5f ? 1 : 0)), Lava[p < 0.4f ? 5 : 4]);
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
            Glow(4, 46, 11, "#ff6418", 0.18f + 0.14f * pulse, JArm);
            Glow(5, 64, 7, "#ff8a30", 0.22f, JArm);
            Glow(3, 33, 8, "#ff6418", 0.15f + 0.1f * pulse, JShoulder);
            o.AddRange(new[] { arm, fist, claw, sh, horns, em });
        }

        // 2. QUICKSILVER (mercury): liquid metal in the arm's shape, mirror-bright; molten runs off the cap and
        // the elbow lengthen and draw back, a ripple slides down it.
        static Color32 Chrome(float cx, float cy, float rx, float ry, int x, int y)
        {
            float nx = Mathf.Clamp((x + 0.5f - cx) / rx, -1f, 1f), ny = Mathf.Clamp((y + 0.5f - cy) / ry, -1f, 1f), h = nx * 0.9f + ny * 0.35f;
            return Merc[h < -0.62f ? 5 : h < -0.28f ? 4 : h < 0.02f ? 1 : h < 0.42f ? 2 : h < 0.78f ? 3 : 1];
        }
        static void Quicksilver(ArmPieceState st, List<Layer> o)
        {
            float t = st.T;
            Layer arm = AL(12.6f, JArm), sh = AL(14.2f, JShoulder);
            foreach (var (cx, cy, rx, ry) in new[] { (4f, 43f, 5.6f, 7.2f), (4.5f, 50.5f, 4.6f, 3.2f), (4.6f, 55.5f, 5f, 6.5f), (5f, 65f, 5.2f, 4.6f) })
                arm.Fill(Ell(cx, cy, rx, ry), (x, y, _) => Chrome(cx, cy, rx, ry, x, y));
            sh.Fill(Ell(4.5f, 35f, 7.4f, 6.4f), (x, y, _) => Chrome(4.5f, 35f, 7.4f, 6.4f, x, y));
            foreach (var (x, y, ph, n) in new[] { (-1.5f, 38f, 0f, 6f), (1.5f, 40.5f, 2.1f, 4f), (9.5f, 39f, 4.2f, 5f), (0f, 52f, 1.3f, 4f) })
            {
                float len = n * (0.55f + 0.45f * Mathf.Sin(t * 1.6f + ph));
                Stroke(y > 45f ? arm : sh, x, y, x, y + len, 2f, (xx, yy, _) => Merc[xx + 0.5f < x ? 5 : 2]);
                sh.Fill(Ell(x, y + len, 1.4f, 1.4f), (xx, yy, _) => Merc[xx + 0.5f < x && yy + 0.5f < y + len ? 5 : 2]);
            }
            float yr = 34f + Mathf.Repeat(t * 0.6f, 1f) * 38f;
            foreach (var l in new[] { arm, sh })
                foreach (int k in new List<int>(l.C.Keys))
                {
                    float d = Mathf.Abs(KY(k) + 0.5f - yr);
                    if (d < 1f) l.C[k] = Merc[5]; else if (d < 2f) l.C[k] = Merc[4];
                }
            Glow(3, 40, 9, "#e8f0ff", 0.1f, JArm);
            o.AddRange(new[] { arm, sh });
        }

        // 3. SALT: grey rock salt grown through with rose and white crystals; a glint jumps between their tips.
        static void SaltArm(ArmPieceState st, List<Layer> o)
        {
            float t = st.T;
            Layer arm = AL(12.6f, JArm), xa = AL(12.8f, JArm), sh = AL(14.2f, JShoulder), xs = AL(14.3f, JShoulder, rise: true);
            Shader Rock(float cx) => (x, y, _) =>
            {
                float h = H2(x >> 1, y >> 1);
                return SaltRock[Cyl(cx, 6f, x) + (h > 0.72f ? 1 : h < 0.2f ? -1 : 0)];
            };
            arm.Fill(Poly(V(-1, 37), V(10, 37), V(9, 50), V(10, 61), V(9, 69), V(1, 69), V(-1, 64), V(0, 50)), Rock(4.5f));
            foreach (var (x, y) in new[] { (2, 62), (6, 63), (4, 66) }) arm.Dot(x, y, SaltRock[1]);
            sh.Fill(Poly(V(-3, 37), V(-2, 31), V(3, 28), V(9, 29), V(12, 33), V(11, 40), V(-1, 41)), Rock(4f));
            var tips = new List<(int x, int y)>();
            void P(Layer l, float bx, float by, float dx, float dy, float len, float w, Tones r)
            {
                Prism(l, bx, by, dx, dy, len, w, r);
                float L0 = Hypot(dx, dy);
                tips.Add((R(bx + dx / L0 * (len - 1)), R(by + dy / L0 * (len - 1))));
            }
            P(xs, 1, 33, -0.45f, -1, 13, 4, Salt);
            P(xs, 5, 31, 0.05f, -1, 9, 3, Rose);
            P(xs, -2, 36, -1, -0.55f, 9, 3, Rose);
            P(xs, 8, 31, 0.45f, -1, 6, 3, Salt);
            P(xa, 0, 53, -1, 0.15f, 6, 3, Salt);
            P(xa, 0, 57, -1, 0.5f, 5, 3, Rose);
            P(xa, 0, 44, -1, -0.3f, 4, 2, Salt);
            P(xa, 8, 66, 0.5f, 1, 4, 2, Rose);
            P(xa, 4, 67, 0, 1, 4, 2, Salt);
            int gi = Mod(Mathf.FloorToInt(t * 1.4f), tips.Count);
            float f = Mathf.Repeat(t * 1.4f, 1f);
            var gl = AL(15f, gi >= 4 ? JArm : JShoulder, outlined: false);
            if (f < 0.45f)
            {
                var (x, y) = tips[gi];
                gl.Set(x, y, White);
                if (f < 0.3f) foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) gl.Set(x + dx, y + dy, Salt[5]);
            }
            Glow(1, 25, 9, "#ffd8e4", 0.12f, JShoulder);
            o.AddRange(new[] { arm, xa, sh, xs, gl });
        }

        // 4. CRUCIBLE: black iron worked like a furnace - a box pauldron with a glowing grate, a riveted arm on a
        // brass elbow cog, a gauntlet with fire behind its slits; a leather strap across the chest holds it on.
        static void Crucible(ArmPieceState st, List<Layer> o)
        {
            float t = st.T;
            Layer arm = AL(12.6f, JArm), cog = AL(12.9f, JArm), gaunt = AL(13.0f, JArm), sh = AL(14.2f, JShoulder), strap = AL(8.7f, JTorso);
            int Fl(float k) { float v = Mathf.Sin(t * 9 + k * 2.1f) + Mathf.Sin(t * 5.3f + k); return v > 0.6f ? 5 : v > -0.6f ? 4 : 3; }
            Accessories.Strap(strap, 8, 33, 30, 60, 4, Leather);
            strap.Fill(Rect(18, 45, 21, 48), (x, y, _) => Brass[x == 18 || y == 45 ? 5 : 2]);
            strap.Fill(Rect(19, 46, 20, 47), Leather[1]);
            arm.Fill(Rect(-1, 40, 10, 50), (x, y, _) => Iron[Cyl(4.5f, 6.5f, x)]);
            foreach (int y0 in new[] { 43, 47 }) arm.Over(Rect(-2, y0, 11, y0), (x, y, _) => Iron[Cyl(4.5f, 6.5f, x) + 1]);
            foreach (var (x, y) in new[] { (1, 45), (8, 45) }) arm.Dot(x, y, Iron[5]);
            cog.Fill(Ell(4.5f, 51f, 4.6f, 3.4f), (x, y, _) => Brass[DomeI(4, 50, 5, 4, x, y)]);
            foreach (int x in new[] { 0, 9 }) cog.Fill(Rect(x, 50, x, 51), Brass[2]);
            gaunt.Fill(Poly(V(-3, 53), V(12, 53), V(11, 62), V(-1, 62)), (x, y, _) => Iron[Cyl(4.5f, 7.5f, x) + (y == 53 ? 1 : 0)]);
            foreach (int x in new[] { 1, 4, 7 }) for (int y = 55; y <= 60; y++) gaunt.Set(x, y, y == 55 ? Iron[0] : Ember[Fl(x + y * 0.2f)]);
            gaunt.Fill(Rect(-1, 62, 10, 69), (x, y, _) => Iron[(y - 62) % 3 == 0 ? 4 : Cyl(4.5f, 6.5f, x)]);
            foreach (int x in new[] { 1, 4, 7 }) gaunt.Fill(Rect(x, 63, x + 1, 64), (xx, y, _) => Brass[y == 63 ? 5 : 3]);
            var box = Poly(V(-5, 31), V(-1, 26), V(11, 26), V(14, 31), V(14, 42), V(-5, 42));
            var bb = box.BBox();
            // the mock's plate(): a lit-left gradient across its own box, under a bevel
            sh.Fill(box, (x, y, m) =>
            {
                float g = (x + 0.5f - bb.x0) / bb.w;
                int face = g < 0.22f ? 4 : g < 0.7f ? 3 : 2;
                return Bevel(Iron, 1, face)(x, y, m);
            });
            sh.Over(Rect(-6, 41, 15, 42), (x, y, _) => Brass[y == 41 ? 4 : 2]);
            foreach (int x in new[] { 0, 3, 6 }) for (int y = 31; y <= 38; y++) sh.Set(x, y, y == 31 ? Iron[0] : Ember[Fl(x)]);
            foreach (var (x, y) in new[] { (-3, 33), (11, 30), (11, 38), (-3, 39) }) sh.Dot(x, y, Iron[5]);
            float fk = 0.5f + 0.5f * Mathf.Sin(t * 7f);
            Glow(3.5f, 35, 9, "#ff7a22", 0.2f + 0.1f * fk, JShoulder);
            Glow(4.5f, 58, 8, "#ff7a22", 0.18f + 0.1f * fk, JArm);
            o.AddRange(new[] { strap, arm, cog, gaunt, sh });
        }

        // 5. VERDIGRIS: copper clockwork gone green - riveted plates crusted with patina, a piston down the outer
        // elbow that pumps, a cog on the shoulder that turns.
        static void Verdigris(ArmPieceState st, List<Layer> o)
        {
            float t = st.T;
            Layer arm = AL(12.6f, JArm), pis = AL(12.5f, JArm, outR: 2), sh = AL(14.2f, JShoulder);
            Shader Cu(float cx, float rx) => (x, y, _) => H2(x >> 2, (y + 1) >> 2) > 0.8f ? Patina[Cyl(cx, rx, x)] : Copper[Cyl(cx, rx, x)];
            arm.Fill(ArmCol(), Cu(4.5f, 6.5f));
            foreach (int y in new[] { 44, 50, 56 }) arm.Over(Rect(-3, y, 12, y), Copper[1]);
            foreach (var (x, y) in new[] { (0, 41), (8, 41), (0, 53), (8, 53), (0, 59), (8, 59) }) arm.Dot(x, y, Copper[5]);
            arm.Fill(FistM(), (x, y, _) => Copper[DomeI(4, 64, 6, 5, x, y)]);
            foreach (int x in new[] { 2, 5, 8 }) arm.Dot(x, 62, Patina[4]);
            float k = 0.5f + 0.5f * Mathf.Sin(t * 4f);
            pis.Fill(Rect(-4, 42, -3, 47 + R(k * 3)), (x, y, _) => Patina[x == -4 ? 4 : 2]);
            pis.Fill(Rect(-5, 50 + R(k * 3), -2, 58), (x, y, _) => Copper[x == -4 ? 4 : x == -1 ? 2 : 3]);
            float rot = t * 1.2f;
            var cogM = new Mask();
            for (int y = 24; y <= 44; y++)
                for (int x = -6; x <= 15; x++)
                {
                    float dx = x + 0.5f - 4.5f, dy = y + 0.5f - 34f, r = Hypot(dx, dy), a = Mathf.Atan2(dy, dx) + rot;
                    if (r < 6.6f || (r < 9f && Mathf.Repeat(a / (Mathf.PI / 4f), 1f) < 0.5f)) cogM.Add(x, y);
                }
            sh.Fill(cogM, (x, y, _) => Copper[DomeI(3, 32, 9, 9, x, y)]);
            sh.Over(Ell(4.5f, 34f, 3.6f, 3.6f), (x, y, _) => Patina[DomeI(3.5f, 33, 4, 4, x, y)]);
            sh.Over(Rect(4, 33, 5, 34), Copper[5]);
            o.AddRange(new[] { arm, pis, sh });
        }

        // 6. RUBEDO: crimson lacquered plate traced in gold, the Stone set in the back of the hand, its light
        // running up the arm in thin veins.
        static void Rubedo(ArmPieceState st, List<Layer> o)
        {
            float t = st.T;
            Layer arm = AL(12.6f, JArm), fist = AL(13.0f, JArm), sh = AL(14.2f, JShoulder);
            arm.Fill(ArmCol(), (x, y, _) => Lacquer[Cyl(4.5f, 6.5f, x)]);
            foreach (int y0 in new[] { 41, 49, 53 }) arm.Over(Rect(-3, y0, 12, y0), (x, y, _) => Gold[x < 2 ? 4 : 3]);
            foreach (int y0 in new[] { 37, 61 }) arm.Over(Rect(-3, y0, 12, y0), Gold[2]);
            float pulse = Mathf.Repeat(t * 0.8f, 1f);
            foreach (var (x, ph) in new[] { (3, 0f), (6, 0.35f) })
                for (int y = 42; y <= 60; y++)
                {
                    if (y == 49 || y == 53) continue;
                    bool run = Mathf.Abs((60 - y) / 18f - Mathf.Repeat(pulse + ph, 1f)) < 0.12f;
                    arm.Set(x + (y % 4 < 2 ? 0 : 1), y, run ? Stone[5] : Lacquer[1]);
                }
            fist.Fill(FistM(), (x, y, _) => Lacquer[DomeI(4, 64, 6, 5, x, y)]);
            fist.Fill(Ell(4.8f, 64.5f, 2.6f, 2.4f), Gold[3]);
            fist.Fill(Ell(4.8f, 64.5f, 1.8f, 1.7f), (x, y, _) => Stone[x < 5 && y < 64 ? 5 : 3 + R(Mathf.Sin(t * 3f))]);
            for (int i = 2; i >= 0; i--)
            {
                float cy = 31 + i * 3.4f, rx = 8 - i * 0.6f;
                var m = Ell(4.5f, cy, rx, 4.2f);
                sh.Fill(m, (x, y, _) => !m.Has(x, y + 1) ? Gold[3] : Lacquer[DomeI(3, cy - 1, rx + 1, 5, x, y)]);
            }
            sh.Fill(Ell(4.5f, 30.5f, 2.4f, 2.4f), (x, y, _) => Gold[x < 4 ? 5 : 3]);
            float k = 0.5f + 0.5f * Mathf.Sin(t * 3f);
            Glow(4.8f, 64.5f, 8, "#ff2a3a", 0.3f + 0.15f * k, JArm);
            o.AddRange(new[] { arm, fist, sh });
        }

        // 7. SANGUIS (Crystal): blood frozen hard - dark ruby in irregular facets with bright cracks, a shard off
        // the elbow, shard fingers, broad crystal plates on the shoulder; a glint sweeps across.
        static void SanguisCrystal(ArmPieceState st, List<Layer> o)
        {
            float t = st.T;
            Layer arm = AL(12.6f, JArm), sh = AL(14.2f, JShoulder, rise: true);
            float glint = Mathf.Repeat(t * 0.5f, 1f) * 60f - 10f;
            var seeds = new Vector3[26];
            for (int k = 0; k < seeds.Length; k++) seeds[k] = new Vector3(-4 + H2(k, 3) * 17f, 24 + H2(5, k) * 50f, H2(k, k + 9));
            int Own(int x, int y)
            {
                int b = 0; float bd = 1e9f;
                for (int k = 0; k < seeds.Length; k++) { float d = (x - seeds[k].x) * (x - seeds[k].x) + (y - seeds[k].y) * (y - seeds[k].y); if (d < bd) { bd = d; b = k; } }
                return b;
            }
            Color32 Facet(int x, int y, Mask _)
            {
                int k = Own(x, y);
                bool edge = Own(x + 1, y) != k || Own(x, y + 1) != k;
                if (Mathf.Abs(x + y - glint - 40f) < 1.2f && seeds[k].z > 0.4f) return BloodGlass[5];
                return edge ? BloodGlass[4] : Blood[seeds[k].z > 0.66f ? 4 : seeds[k].z > 0.33f ? 3 : 2];
            }
            var m = U(Poly(V(-1, 37), V(10, 36), V(11, 43), V(10, 49), V(11, 56), V(9.5f, 61), V(-1, 61), V(-1.5f, 55), V(-0.5f, 49), V(-1.5f, 43)),
                      Poly(V(0, 61), V(9.5f, 61), V(10, 68), V(5, 70), V(0, 68)), Poly(V(-1, 48), V(-7, 44), V(-1, 52)));
            foreach (var (x, len) in new[] { (1f, 5f), (3.8f, 6f), (6.6f, 5.5f), (9f, 4f) }) m.Union(Poly(V(x - 1.1f, 69), V(x + 1.1f, 69), V(x, 69 + len)));
            arm.Fill(m, Facet);
            sh.Fill(U(Poly(V(-5, 38), V(-3, 30), V(3, 27), V(5, 33), V(0, 40)), Poly(V(2, 33), V(5, 25), V(10, 27), V(12, 34), V(6, 38)),
                      Poly(V(-2, 36), V(4, 31), V(11, 33), V(10, 40), V(1, 41))), Facet);
            Glow(4.5f, 48, 11, "#ff2a3a", 0.12f, JArm);
            Glow(3, 31, 9, "#ff4a5a", 0.14f, JShoulder);
            o.AddRange(new[] { arm, sh });
        }

        // 8. FULMEN (Cage): no body - the arm is only lightning in its shape, bolts down both edges with zigzags
        // across, struck fresh every frame; a ring of bolts for the shoulder, sparks off the fingers.
        static void FulmenCage(ArmPieceState st, List<Layer> o)
        {
            float t = st.T;
            int frame = Mathf.FloorToInt(t * 12f);
            Layer arm = AL(12.6f, JArm, outR: 2), sh = AL(14.2f, JShoulder, rise: true, outR: 2);
            Zap(arm, -0.5f, 37, -0.5f, 64, 1, frame, 1.2f, 7); Zap(arm, 9.5f, 37, 9, 64, 2, frame, 1.2f, 7);
            for (int i = 0; i < 5; i++) { float y = 39 + i * 6; Zap(arm, i % 2 != 0 ? 9 : 0, y, i % 2 != 0 ? 0 : 9, y + 4, 3 + i, frame, 1, 3); }
            Zap(arm, 0, 64, 9, 64, 9, frame, 1, 3);
            foreach (var (bx, ex, ey, sd) in new[] { (0.5f, -1.5f, 73f, 11), (3.5f, 3f, 75f, 12), (6.5f, 7f, 75f, 13), (9f, 11f, 72f, 14) })
                Zap(arm, bx, 64, ex, ey, sd, frame, 1.2f, 3);
            for (int i = 0; i < 8; i++)
            {
                float a0 = i / 8f * Mathf.PI * 2f, a1 = (i + 1) / 8f * Mathf.PI * 2f;
                Zap(sh, 4.5f + Mathf.Cos(a0) * 6.5f, 33 + Mathf.Sin(a0) * 6, 4.5f + Mathf.Cos(a1) * 6.5f, 33 + Mathf.Sin(a1) * 6, 20 + i, frame, 0.8f, 2);
            }
            float fk = H2(frame, 7);
            Glow(4.5f, 33, 11, "#3a7aff", 0.2f + 0.15f * fk, JShoulder);
            Glow(4.5f, 52, 12, "#3a7aff", 0.18f + 0.15f * fk, JArm);
            o.AddRange(new[] { arm, sh });
        }

        // 9. PENUMBRA (Echo): a dark see-through arm (the rig draws it at Alpha) that drifts a little and trails
        // fainter copies of itself where it has just been (PenumbraEcho).
        public static Vector2 Drift(float t) => new(Mathf.Sin(t * 1.6f) * 2.4f, Mathf.Sin(t * 2.3f) * 0.8f);
        static Layer Shifted(Layer l, float dx, float dy)
        {
            var s = new Layer(l.Depth) { Joint = l.Joint, Rise = l.Rise, Outlined = l.Outlined, Far = l.Far, OutR = l.OutR };
            int ix = R(dx), iy = R(dy);
            foreach (var kv in l.C) s.C[K(KX(kv.Key) + ix, KY(kv.Key) + iy)] = kv.Value;
            return s;
        }
        static void PenumbraEchoArm(ArmPieceState st, List<Layer> o)
        {
            var d = Drift(st.T);
            var m = U(Poly(V(-0.5f, 37), V(9.5f, 37), V(10, 49), V(9.5f, 61), V(-0.5f, 61), V(-1, 49)), Ell(4.8f, 65f, 5f, 4.4f));
            var sm = Ell(4.5f, 34.5f, 7.2f, 5.8f);
            Shader GhostFill(Mask mm) => (x, y, _) => !mm.Has(x - 1, y) ? Violet[3] : Ghost[Cyl(4.5f, 6f, x)];
            var arm = AL(12.6f, JArm).Fill(m, GhostFill(m));
            var sh = AL(14.2f, JShoulder).Fill(sm, GhostFill(sm));
            foreach (var (x, y) in new[] { (1.5f, 70f), (4f, 71f), (6.5f, 70f), (9f, 69f) }) Stroke(arm, x, 68, x + (x - 5) * 0.2f, y + 2, 1.4f, Ghost[3]);
            o.Add(Shifted(arm, d.x, d.y));
            o.Add(Shifted(sh, d.x * 0.5f, d.y * 0.5f));
        }

        // HOMUNCULUS, the synthetic arm: steel with exposed black cables (the user's close-ups of a prop).
        static void CableRun(Layer l, float x0, float x1, float y0, float y1, int ph = 0)
        {
            for (int y = R(y0); y <= R(y1); y++)
                for (int x = R(x0); x <= R(x1); x++)
                    l.Set(x, y, Mod(y + ph, 2) != 0 ? Cable[x == R(x0) ? 5 : 4] : Cable[2]);
        }
        static Shader SteelPlate(float cx, float rx) => (x, y, m) => !m.Has(x, y - 1) ? Steel[5] : !m.Has(x, y + 1) ? Steel[1] : Steel[Cyl(cx, rx, x)];
        static void MeshPanel(Layer l, int x0, int y0, int x1, int y1) =>
            l.Over(Rect(x0, y0, x1, y1), (x, y, _) => x == x0 || y == y0 ? Steel[1] : (Mod(x + y, 2) != 0 ? Cable[1] : Steel[2]));
        /// <summary>The synthetic hand: a dark (or steel) back with a spring tendon to each steel knuckle cap,
        /// long jointed fingers with a spring down each back.</summary>
        static void SynthHand(Layer l, float t, int top = 62, int longF = 11, bool steelBack = false)
        {
            l.Fill(Poly(V(-0.5f, top), V(9.5f, top), V(10.5f, top + 6), V(-0.5f, top + 6)), (x, y, _) => steelBack ? Steel[Cyl(5, 6, x)] : Synth[Cyl(5, 6, x)]);
            var fx = new[] { (-0.5f, longF - 2, -0.35f), (2.5f, longF, -0.12f), (5.5f, longF, 0.12f), (8.5f, longF - 2, 0.35f) };
            for (int i = 0; i < fx.Length; i++)
            {
                var (x0, len, lean) = fx[i];
                int kx = R(x0 + 0.5f), ky = top + 5;
                for (int y = top + 1; y < ky; y++) l.Set(kx, y, Mod(y, 2) != 0 ? Steel[5] : Steel[2]);
                l.Fill(Rect(kx - 1, ky, kx + 1, ky + 1), (x, y, _) => Steel[y == ky ? 5 : 3]);
                float flex = Mathf.Sin(t * 1.8f + i) * 0.15f;
                for (int j = 0; j < len; j++)
                {
                    int x = R(x0 + (lean + flex * ((float)j / len)) * j), y = ky + 2 + j;
                    l.Set(x, y, j % 4 == 3 ? Steel[3] : Synth[4]);
                    l.Set(x + 1, y, j % 2 != 0 && j < len - 2 ? Steel[4] : Synth[2]);
                }
            }
        }

        // 10. HOMUNCULUS (Plated): steel plates with mesh panels, bundles of black cable at the elbow round a
        // hinge disc, a piston in a spring down the inside, cables arching off the shoulder cap.
        static void HomunculusPlated(ArmPieceState st, List<Layer> o)
        {
            float t = st.T, k = 0.5f + 0.5f * Mathf.Sin(t * 3f);
            Layer arm = AL(12.6f, JArm), hose = AL(12.4f, JShoulder, outR: 2), sh = AL(14.2f, JShoulder);
            arm.Fill(Poly(V(-1, 34), V(10.5f, 34), V(10, 46), V(-0.5f, 46)), SteelPlate(4.5f, 6.5f));
            MeshPanel(arm, 2, 37, 7, 42);
            CableRun(arm, -0.5f, 1.5f, 46, 53, 0); CableRun(arm, 2.5f, 5, 46, 53, 1); CableRun(arm, 6, 9.5f, 46, 53, 0);
            arm.Fill(Ell(4.5f, 49.5f, 2.6f, 2.6f), (x, y, _) => Hypot(x + 0.5f - 4.5f, y + 0.5f - 49.5f) < 1f ? Steel[1] : Steel[DomeI(4, 49, 3, 3, x, y)]);
            arm.Fill(Poly(V(-1.5f, 53), V(10.5f, 53), V(10, 61), V(0, 61)), SteelPlate(4.5f, 7f));
            MeshPanel(arm, 1, 55, 5, 59);
            foreach (int x in new[] { 7, 9 }) arm.Over(Rect(x, 55, x, 59), Cable[1]);
            arm.Fill(Rect(10, 41, 11, 58), (x, y, _) => Steel[x == 10 ? 4 : 2]);
            for (int y = 47 + R(k * 2); y <= 54; y++) arm.Fill(Rect(9.5f, y, 11.5f, y), y % 2 != 0 ? Steel[5] : Steel[1]);
            SynthHand(arm, t, 61);
            sh.Fill(Poly(V(-4, 33), V(-4, 30), V(-1, 26.5f), V(9, 26.5f), V(12, 30), V(12, 33)), (x, y, m) => !m.Has(x, y - 1) ? Steel[5] : Steel[DomeI(2, 30, 10, 8, x, y)]);
            MeshPanel(sh, 2, 29, 8, 31);
            foreach (var (ax, bx, bulge, ph) in new[] { (-3f, -1f, -7f, 0), (-2f, 0f, -5f, 1) })
                for (int i = 0; i <= 14; i++)
                {
                    float u = i / 14f, x = Lerp(ax, bx, u) + Mathf.Sin(u * Mathf.PI) * (bulge - ax), y = Lerp(34, 47, u);
                    for (int w = 0; w < 2; w++) hose.Set(R(x) + w, R(y), (i + ph) % 2 != 0 ? Cable[w != 0 ? 3 : 5] : Cable[2]);
                }
            o.AddRange(new[] { hose, arm, sh });
        }

        // 11. HOMUNCULUS (Hydraulic): a clean steel shell with panel lines, an elbow pivot and two cyan lights
        // blinking in turn; thick ribbed black hoses loop off it, shoulder to elbow and elbow to wrist.
        static void HomunculusHydraulic(ArmPieceState st, List<Layer> o)
        {
            float t = st.T;
            Layer arm = AL(12.6f, JArm), hose = AL(12.4f, JArm, outR: 2), shHose = AL(12.4f, JShoulder, outR: 2), sh = AL(14.2f, JShoulder);
            arm.Fill(Poly(V(-0.5f, 34), V(9.5f, 34), V(10, 47), V(9, 49), V(10, 51), V(10.5f, 61), V(-0.5f, 61), V(-1, 51), V(0, 49), V(-1, 47)),
                     (x, y, _) => Steel[Cyl(4.5f, 6.5f, x)]);
            foreach (int y in new[] { 41, 49, 50, 56 }) arm.Over(Rect(-2, y, 12, y), Steel[1]);
            arm.Over(Rect(6, 36, 6, 40), Steel[1]); arm.Over(Rect(3, 52, 3, 55), Steel[1]);
            arm.Fill(Ell(-0.5f, 49.5f, 2, 2), (x, y, _) => Steel[x < 0 ? 5 : 2]);
            bool blink = Mathf.Repeat(t * 1.2f, 1f) < 0.5f;
            arm.Dot(8, 44, blink ? Cyan : Steel[2]); arm.Dot(8, 58, !blink ? Cyan : Steel[2]);
            SynthHand(arm, t, 61);
            void Loop(Layer l, float ay, float by, float bulge, int ph)
            {
                for (int i = 0; i <= 18; i++)
                {
                    float u = i / 18f, x = -1 - Mathf.Sin(u * Mathf.PI) * bulge, y = Lerp(ay, by, u);
                    for (int w = 0; w < 3; w++) l.Set(R(x) - w + 1, R(y), (i + ph) % 2 != 0 ? Cable[w == 2 ? 5 : 4] : Cable[2]);
                }
            }
            Loop(shHose, 34, 48, 6, 0); Loop(hose, 51, 61, 5, 1);
            sh.Fill(Poly(V(-3.5f, 33), V(-3.5f, 30), V(-0.5f, 26.5f), V(9.5f, 26.5f), V(12, 30), V(12, 33)), (x, y, m) => !m.Has(x, y - 1) ? Steel[5] : Steel[DomeI(2, 30, 10, 8, x, y)]);
            sh.Over(Rect(-4, 30, 13, 30), Steel[1]);
            Glow(8, 44, 4, "#7af0ff", blink ? 0.4f : 0.1f, JArm);
            o.AddRange(new[] { shHose, hose, arm, sh });
        }
    }
}
