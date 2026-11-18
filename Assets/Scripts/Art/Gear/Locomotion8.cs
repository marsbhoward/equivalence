using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// EIGHT DIRECTIONS, PHASE 2 (RIG.md): the character's legs as a 3D skeleton posed by a gait and projected through the
    /// arena's 3/4 camera, then DRAWN - capsules lit from the left and outlined - rather than flat sprites turned in the
    /// picture plane (the user's call, 2026-11-11: "draw like the mock"). Ported from Tools/views5/mock/mock8.js, the
    /// walk and run the user signed off on: 62-texel legs (thigh 29, shin 29, ankle 4), hips 6 either side.
    ///
    /// Space: texels, x right, y up, z TOWARD the camera; a facing of 0 degrees is screen right, 90 straight up the
    /// screen (away). Screen = (x, y - z * DepthY). Pictures come back as palette rows ('k'..'h' the leg ramp), each
    /// drawn once per (direction, gait, frame, stride size) and reused.
    /// </summary>
    public static class Locomotion8
    {
        public const float Thigh = 29f, Shin = 29f, Ankle = 4f, HipWidth = 6f, DepthY = 0.45f;
        public const int CanvasW = 100, CanvasH = 128, GroundX = 50, GroundY = 110;

        public sealed class Gait
        {
            public string Name; public int Frames;
            public float[][] Foot, Pitch;          // keys [phase, a, b]
            public float Bob, BobPhase, BobOffset, Lean, Base, HipDrop;
            public float Arm, Elbow;               // the arms' swing and the elbow's bend at full stride, degrees (Body8)
            public float Trail;                    // how far a cape's hem trails behind at full stride, texels (Body8)

            // PLANTED (the scaled stride, StrideFor): the foot sweeps back at exactly the ground's speed while it is
            // down (Stance of its cycle, from Reach ahead to Push behind), then swings back along a spline (Swing:
            // [u, forward, lift] over the swing, ends included) - no key the foot stops at, and no slide.
            public bool Planted;
            public float Stance, Reach, Push, Speed, SpeedTex, Cycle;
            public float[][] Swing, PitchSpline;   // PitchSpline: [phase, degrees], cyclic
            // THE ANIME TIMING (the Dash lean, the user's pick 2026-11-18): Hold, the swing's time curve (u -> u, slow on
            // the two flight drawings, quick between: KeyedSwing), or null for an even swing; Hang, the body's rise and
            // fall shaped to float through the flight and drop sharply onto the contact (1 is an even wave).
            public float[] Hold;
            public float Hang = 1f;

            public float Height(float p)
            {
                if (!Planted) return Bob * Mathf.Cos(4f * Mathf.PI * (p - BobPhase)) + BobOffset;
                float d = 0.5f * (1f + Mathf.Cos(4f * Mathf.PI * (p - Stance * 0.5f)));   // 1 mid-stance, 0 mid-flight
                return Bob - 2f * Bob * (Hang == 1f ? d : Mathf.Pow(d, Hang));        // Hang 1: -Bob cos, lowest mid-stance
            }
            /// <summary>The arms' swing at <paramref name="p"/>, degrees (Body8: the character's right arm; the left is its
            /// negative). Planted: forward as its own side's foot leaves the ground.</summary>
            public float ArmAt(float p) => Planted ? Arm * Mathf.Cos(2f * Mathf.PI * (p - Stance)) : Arm * Mathf.Sin(2f * Mathf.PI * p);
            /// <summary>A run rather than a walk (the scarf's streaming, Body8.Extras).</summary>
            public bool Running => Planted ? Speed >= JogSpeed : Name == "run";

            /// <summary>Where the foot is at leg phase <paramref name="p"/>: forward and lift in leg lengths, the sole's pitch.</summary>
            public void LegAt(float p, out float fw, out float lift, out float pitch)
            {
                if (!Planted)
                {
                    var foot = Keys(Foot, p); fw = foot[0]; lift = foot[1]; pitch = Keys(Pitch, p)[0];
                    return;
                }
                p = Mathf.Repeat(p, 1f);
                float sweep = Reach + Push, vStance = -sweep / Stance;
                if (p < Stance) { fw = Reach - sweep * (p / Stance); lift = 0f; }
                else
                {
                    float u = (p - Stance) / (1f - Stance), du = 1f - Stance;
                    if (Hold != null) { float x = u * (Hold.Length - 1); int j = Mathf.Min(Hold.Length - 2, (int)x); u = Mathf.Lerp(Hold[j], Hold[j + 1], x - j); }
                    int i = 0; while (i < Swing.Length - 2 && u > Swing[i + 1][0]) i++;
                    var a = Swing[i]; var b = Swing[i + 1]; float span = b[0] - a[0], t = (u - a[0]) / span;
                    fw = Hermite(a[1], b[1], SwingTangent(i, 1, vStance, du) * span, SwingTangent(i + 1, 1, vStance, du) * span, t);
                    lift = Mathf.Max(0f, Hermite(a[2], b[2], SwingTangent(i, 2, vStance, du) * span, SwingTangent(i + 1, 2, vStance, du) * span, t));
                }
                pitch = CyclicSpline(PitchSpline, p);
            }

            // d(value)/du at swing point k: the ends leave and land at the stance's own speed (the landing at 0.6 of it)
            float SwingTangent(int k, int j, float vStance, float du)
            {
                if (k == 0) return j == 1 ? vStance * du : (Swing[1][j] - Swing[0][j]) / (Swing[1][0] - Swing[0][0]);
                int n = Swing.Length - 1;
                if (k == n) return j == 1 ? vStance * du * 0.6f : (Swing[n][j] - Swing[n - 1][j]) / (Swing[n][0] - Swing[n - 1][0]);
                return (Swing[k + 1][j] - Swing[k - 1][j]) / (Swing[k + 1][0] - Swing[k - 1][0]);
            }
        }

        static float Hermite(float p0, float p1, float m0, float m1, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * p0 + (t3 - 2f * t2 + t) * m0 + (-2f * t3 + 3f * t2) * p1 + (t3 - t2) * m1;
        }

        /// <summary>A Catmull-Rom through cyclic [phase, value] keys: the value keeps its speed through every key.</summary>
        static float CyclicSpline(float[][] keys, float p)
        {
            p = Mathf.Repeat(p, 1f); int n = keys.Length, i = n - 1;
            for (int j = 0; j < n; j++) if (keys[j][0] <= p) i = j;
            float At(int k, out float v) { int w = ((k % n) + n) % n; v = keys[w][1]; return keys[w][0] + Mathf.Floor(k / (float)n); }
            float pa = At(i - 1, out float va), pb = At(i, out float vb), pc = At(i + 1, out float vc), pd = At(i + 2, out float vd);
            float span = pc - pb, t = (p - pb) / span;
            return Hermite(vb, vc, (vc - va) / (pc - pa) * span, (vd - vb) / (pd - pb) * span, t);
        }

        // ---------------------------------------------------------------- the scaled stride
        // ONE run for every speed (the user, 2026-11-18, off the Pace Lab https://claude.ai/artifact/PGfExupKu1riKqNFuUHNvG:
        // "too many or too little steps at any given time looks unpolished"). Three anchor poses - a jog, a run and a
        // sprint, the Dash lean's since 2026-11-18 (Long stride's, E, before) - are blended by speed. Steps a second
        // follow ONE smooth rise with speed (StepRate x DashRate x (v / 4.6)^StepPower), each anchor sets how far the foot sweeps, and how long it is down FOLLOWS from
        // keeping it planted - so the steps lengthen far more than they quicken, the way a runner speeds up. Below the
        // jog (only while starting or stopping) it keeps the jog's rate and shortens every step by v / jog, the hips rising
        // to standing: short quick steps out of the stand, never slow floating ones.

        /// <summary>Texels a world unit the body is drawn at in the ARENA (150 ppu under ArenaVisualScale 4/3): speeds here
        /// are arena units a second, and a rig at another scale converts (PrimitiveCharacterRig.PlaceLegs8).</summary>
        public const float ArenaTexelsPerUnit = 150f / (4f / 3f);
        public const float StepRate = 3.45f, StepPower = 0.3f, StepRateAt = 4.6f, JogSpeed = 1.4f;
        /// <summary>THE DASH LEAN (the user's pick, 2026-11-18, off the Anime Run Lab
        /// https://claude.ai/artifact/R1P8e13PfTyBazrw3b3QH2; it replaced Long stride's anchors): pitched forward on low
        /// hips, the rear leg flicked long and high behind, the front foot skimming. Its steps come 15% quicker than the
        /// Pace Lab's rate; the swing eases into two held flight drawings and snaps between them (Keyed); the body hangs
        /// through the flight and drops onto the contact (Hang). Both fade out below the jog, toward a stand.</summary>
        public const float DashRate = 1.15f, Keyed = 0.45f, DashHang = 1.6f;
        /// <summary>Where the swing slows to a hold (share of the swing): the rear leg kicked up behind, the front reaching
        /// for the ground - both in flight - and how wide each hold is.</summary>
        static readonly float[] Holds = { 0.25f, 0.86f };
        const float HoldWidth = 0.18f;

        /// <summary>The swing's time curve for hold strength <paramref name="k"/>: a density with a dip at each hold,
        /// integrated and normalised (slow 1-k at a hold, quicker between).</summary>
        static float[] KeyedSwing(float k)
        {
            const int N = 64;
            var t = new float[N + 1];
            for (int i = 0; i < N; i++)
            {
                float x = (i + 0.5f) / N, b = 0f;
                foreach (float h in Holds) { float q = Mathf.Abs(x - h) / HoldWidth; if (q < 1f) b += 0.5f * (1f + Mathf.Cos(Mathf.PI * q)); }
                t[i + 1] = t[i] + 1f - k * Mathf.Min(1f, b);
            }
            for (int i = 0; i <= N; i++) t[i] /= t[N];
            return t;
        }
        /// <summary>Under this (arena units a second) the figure stands.</summary>
        public const float StandBelow = 0.2f;

        sealed class Anchor
        {
            public float V, Reach, Push, Base, Bob, Heel, Toe, Flick, Lean, Arm, Elbow, Trail;
            public float[] Kick, Pass, Knee, Fore;   // the swing's keys at 0.25, 0.5, 0.66 and 0.86 of it
        }
        static Anchor[] _anchors;
        // the knee drive (Knee) comes while the OTHER foot is down: in the flights the legs are split, the rear folded up
        // behind and the front reaching low - a high front foot there read as a tucked jump (the mock's first try)
        static Anchor[] Anchors => _anchors ??= new[]
        {
            new Anchor { V = 1.4f, Reach = 0.30f, Push = 0.42f, Base = 51.0f, Bob = 1.4f, Kick = new[] { -0.52f, 0.32f }, Pass = new[] { -0.14f, 0.34f }, Knee = new[] { 0.12f, 0.20f }, Fore = new[] { 0.32f, 0.06f },
                         Heel = 10f, Toe = 32f, Flick = 58f, Lean = 14f, Arm = 22f, Elbow = 92f, Trail = 10f },
            new Anchor { V = 4.6f, Reach = 0.40f, Push = 0.60f, Base = 46.4f, Bob = 1.6f, Kick = new[] { -0.74f, 0.50f }, Pass = new[] { -0.20f, 0.52f }, Knee = new[] { 0.18f, 0.30f }, Fore = new[] { 0.46f, 0.08f },
                         Heel = 12f, Toe = 40f, Flick = 84f, Lean = 28f, Arm = 30f, Elbow = 100f, Trail = 18f },
            new Anchor { V = 7.4f, Reach = 0.42f, Push = 0.64f, Base = 45.4f, Bob = 1.8f, Kick = new[] { -0.78f, 0.56f }, Pass = new[] { -0.18f, 0.58f }, Knee = new[] { 0.20f, 0.32f }, Fore = new[] { 0.48f, 0.08f },
                         Heel = 12f, Toe = 42f, Flick = 90f, Lean = 32f, Arm = 34f, Elbow = 104f, Trail = 22f },
        };
        /// <summary>The speeds a stride is drawn for (arena units a second): a dozen pictures-sets, not one per speed.</summary>
        public static readonly float[] StrideSpeeds = { 0.35f, 0.7f, 1.05f, 1.4f, 1.8f, 2.3f, 2.9f, 3.6f, 4.6f, 5.6f, 6.6f, 7.4f };
        static Gait[] _strides;

        /// <summary>The scaled stride nearest <paramref name="v"/> arena units a second.</summary>
        public static Gait StrideFor(float v)
        {
            var speeds = StrideSpeeds;
            int at = 0;
            for (int q = 1; q < speeds.Length; q++) if (Mathf.Abs(speeds[q] - v) < Mathf.Abs(speeds[at] - v)) at = q;
            _strides ??= new Gait[speeds.Length];
            return _strides[at] ??= BuildStride(at, speeds[at]);
        }

        static Gait BuildStride(int index, float vb)
        {
            var an = Anchors;
            int i = 0; while (i < an.Length - 2 && vb > an[i + 1].V) i++;
            Anchor a0 = an[i], a1 = an[i + 1];
            float t = Mathf.Clamp01((vb - a0.V) / (a1.V - a0.V)), k = Mathf.Min(1f, vb / JogSpeed);
            float M(float x0, float x1, bool step = true) { float x = Mathf.Lerp(x0, x1, t); return step ? x * k : x; }
            float[] MA(float[] x0, float[] x1) => new[] { M(x0[0], x1[0]), M(x0[1], x1[1]) };
            float reach = M(a0.Reach, a1.Reach), push = M(a0.Push, a1.Push), speedTex = vb * ArenaTexelsPerUnit;
            float cycle = 2f / (StepRate * DashRate * Mathf.Pow(Mathf.Max(JogSpeed, vb) / StepRateAt, StepPower));
            float stance = Mathf.Min(0.62f, (reach + push) * (Thigh + Shin) / (speedTex * cycle));
            float heel = M(a0.Heel, a1.Heel), toe = M(a0.Toe, a1.Toe), flick = M(a0.Flick, a1.Flick);
            float Sw(float q) => stance + (1f - stance) * q;
            var kick = MA(a0.Kick, a1.Kick); var pass = MA(a0.Pass, a1.Pass); var knee = MA(a0.Knee, a1.Knee); var fore = MA(a0.Fore, a1.Fore);
            return new Gait
            {
                Name = "s" + index, Frames = Mathf.Max(4, Mathf.RoundToInt(cycle * 25f)), Planted = true,
                Stance = stance, Reach = reach, Push = push, Speed = vb, SpeedTex = speedTex, Cycle = cycle,
                Swing = new[] { new[] { 0f, -push, 0f }, new[] { 0.25f, kick[0], kick[1] }, new[] { 0.5f, pass[0], pass[1] }, new[] { 0.66f, knee[0], knee[1] },
                                new[] { 0.86f, fore[0], fore[1] }, new[] { 1f, reach, 0f } },
                PitchSpline = new[] { new[] { 0f, heel }, new[] { stance * 0.5f, 0f }, new[] { stance, -toe }, new[] { Sw(0.25f), -flick }, new[] { Sw(0.55f), -flick * 0.3f }, new[] { Sw(0.86f), heel * 0.8f } },
                // the holds and the hang fade toward a stand below the jog, as the steps shorten
                Hold = KeyedSwing(Keyed * k), Hang = 1f + (DashHang - 1f) * k,
                // the hips rise toward standing as the steps shorten below the jog
                Base = k < 1f ? Mathf.Lerp(Thigh + Shin - 2.5f, Mathf.Lerp(a0.Base, a1.Base, t), k) : Mathf.Lerp(a0.Base, a1.Base, t),
                Bob = M(a0.Bob, a1.Bob), Lean = M(a0.Lean, a1.Lean), Arm = M(a0.Arm, a1.Arm), Elbow = Mathf.Lerp(a0.Elbow, a1.Elbow, t),
                Trail = M(a0.Trail, a1.Trail),
            };
        }

        // Pictures a stride: 25 a second for both (the walk's 0.96 s, the run's 0.64 s), the hand-drawn sprite rate - the
        // user's call, 2026-11-15, from the walk mock (https://claude.ai/artifact/Joy242wN6r6yHV7Zyxgp4i). Was 48 and 32,
        // 50 a second, which doubled the pictures the pose cache had to hold for no difference anyone could see.
        public static readonly Gait Run = new Gait
        {
            Name = "run", Frames = 16,
            Foot = new[] { new[] { 0f, 0.30f, 0f }, new[] { 0.33f, -0.36f, 0f }, new[] { 0.48f, -0.55f, 0.42f }, new[] { 0.66f, -0.08f, 0.55f }, new[] { 0.84f, 0.46f, 0.24f } },
            Pitch = new[] { new[] { 0f, 18f }, new[] { 0.12f, 0f }, new[] { 0.33f, -40f }, new[] { 0.5f, -55f }, new[] { 0.7f, -20f }, new[] { 0.86f, 10f } },
            Bob = 3.2f, BobPhase = 0.42f, BobOffset = -1.5f, Lean = 11f, Base = 54.4f, HipDrop = 0f, Arm = 34f, Elbow = 88f, Trail = 11f,
        };

        public static readonly Gait Walk = new Gait
        {
            Name = "walk", Frames = 24,
            Foot = new[] { new[] { 0f, 0.30f, 0f }, new[] { 0.6f, -0.30f, 0f }, new[] { 0.72f, -0.18f, 0.10f }, new[] { 0.86f, 0.12f, 0.13f }, new[] { 0.96f, 0.32f, 0.03f } },
            Pitch = new[] { new[] { 0f, 16f }, new[] { 0.1f, 0f }, new[] { 0.55f, -8f }, new[] { 0.62f, -30f }, new[] { 0.75f, -10f }, new[] { 0.92f, 8f } },
            Bob = 1.3f, BobPhase = 0.30f, BobOffset = 0f, Lean = 3f, Base = 56.3f, HipDrop = 1f, Arm = 14f, Elbow = 28f, Trail = 4f,
        };

        internal static float[] Keys(float[][] keys, float p)
        {
            p = Mathf.Repeat(p, 1f);
            for (int i = 0; i < keys.Length; i++)
            {
                var k0 = keys[i]; var k1 = keys[(i + 1) % keys.Length];
                float p1 = i + 1 < keys.Length ? k1[0] : 1f + k1[0];
                if (p >= k0[0] && p <= p1)
                {
                    float t = (p - k0[0]) / (p1 - k0[0]), s = t * t * (3f - 2f * t);
                    var o = new float[k0.Length - 1];
                    for (int j = 0; j < o.Length; j++) o[j] = k0[j + 1] + (k1[j + 1] - k0[j + 1]) * s;
                    return o;
                }
            }
            var first = new float[keys[0].Length - 1];
            System.Array.Copy(keys[0], 1, first, 0, first.Length);
            return first;
        }

        public struct Leg { public Vector3 Hip, Knee, AnkleJoint, Heel, Toe; public int Side; }
        public struct Pose { public Leg A, B; public float HipY, Lean; public Vector3 Forward, Right; }

        /// <summary>A swing's body on the legs (PrimitiveCharacterRig.Body's SwingBody, in texels): the hips dropped,
        /// the left foot forward and the right back along the facing (the split stance), the right foot also held where it
        /// stood while the figure steps (<see cref="Hold"/>, on the ground), and the torso's extra lean (forward +).</summary>
        public struct Stance { public float Crouch, Front, Lean; public Vector3 Hold; }

        /// <summary>The legs at <paramref name="phase"/> (0..1) of <paramref name="g"/>, facing <paramref name="facingDeg"/>;
        /// <paramref name="size"/> 0..1 scales the stride (0 = standing).</summary>
        public static Pose PoseAt(float facingDeg, Gait g, float phase, float size, Stance st = default)
        {
            float a = facingDeg * Mathf.Deg2Rad;
            var f = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));          // up the screen = away from the camera = -z
            var r = new Vector3(-f.z, 0f, f.x);                              // the body's right (the mock's convention)
            float stand = Thigh + Shin;
            float hipY = Ankle + Mathf.Lerp(stand, g.Base + g.Height(phase), size) - st.Crouch;
            var pose = new Pose { HipY = hipY, Lean = g.Lean * size + st.Lean, Forward = f, Right = r };
            for (int n = 0; n < 2; n++)
            {
                int side = n == 0 ? 1 : -1;
                float p = phase + (side == 1 ? 0f : 0.5f);
                g.LegAt(p, out float fw0, out float lift0, out float pitch0);
                float pitch = pitch0 * size, fw = fw0 * size, lift = lift0 * size;
                float drop = g.HipDrop * size * Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * (p - 0.6f)));
                var hip = new Vector3(0f, hipY - drop, 0f) + r * (side * HipWidth);
                var ankle = new Vector3(hip.x, Ankle + lift * stand, hip.z) + f * (fw * stand);
                // the swing's stance: the left foot forward, the right back and held where it stood
                ankle += side < 0 ? f * st.Front : -f * st.Front + st.Hold;
                var v = ankle - hip; float d = v.magnitude;
                if (d > Thigh + Shin - 0.01f) { v *= (Thigh + Shin - 0.01f) / d; ankle = hip + v; d = Thigh + Shin - 0.01f; }
                float vf = Vector3.Dot(v, f), vu = v.y;
                float h = Mathf.Sqrt(Mathf.Max(0f, Thigh * Thigh - (d * 0.5f) * (d * 0.5f)));
                float pf = -vu / d, pu = vf / d, sgn = pf >= 0f ? 1f : -1f;   // the knee bends FORWARD
                var knee = hip + v * 0.5f + (f * (pf * h * sgn) + Vector3.up * (pu * h * sgn));
                float pr = pitch * Mathf.Deg2Rad;
                var toeDir = f * Mathf.Cos(pr) + Vector3.up * Mathf.Sin(pr);
                var sole = ankle + new Vector3(0f, -3f, 0f);
                var leg = new Leg { Side = side, Hip = hip, Knee = knee, AnkleJoint = ankle, Heel = sole - toeDir * 3f, Toe = sole + toeDir * 9f };
                if (n == 0) pose.A = leg; else pose.B = leg;
            }
            return pose;
        }

        public static Vector2 Screen(Vector3 p) => new Vector2(GroundX + p.x, GroundY - p.y + p.z * DepthY);

        static readonly string Tones = "ksdblh";
        static int ShadeIdx(float s) => s < -0.45f ? 4 : s > 0.5f ? 2 : 3;   // lit from the left (index into Line..Glow)
        static char Tone(int i) => Tones[Mathf.Clamp(i, 0, 5)];

        /// <summary>The two legs drawn: rows of <see cref="CanvasW"/> x <see cref="CanvasH"/>, the ground point at
        /// (<see cref="GroundX"/>, <see cref="GroundY"/>). The far leg first, each part outlined in 'k'.</summary>
        public static string[] DrawLegs(Pose pose)
        {
            var parts = new List<(float z, System.Action<Dictionary<int, char>> draw)>();
            foreach (var g in new[] { pose.A, pose.B })
            {
                var leg = g;
                parts.Add(((leg.Hip.z + leg.Knee.z) * 0.5f, c => Capsule(c, Screen(leg.Hip), Screen(leg.Knee), 5f,
                    (u, s) => Tone(ShadeIdx(s) + (u < 0.15f ? -1 : 0)))));
                parts.Add(((leg.Knee.z + leg.AnkleJoint.z) * 0.5f + 0.01f, c => Capsule(c, Screen(leg.Knee), Screen(leg.AnkleJoint), 4.6f,
                    (u, s) => Tone(ShadeIdx(s)))));
                parts.Add(((leg.Heel.z + leg.Toe.z) * 0.5f + 0.02f, c => Capsule(c, Screen(leg.Heel), Screen(leg.Toe), 2.6f,
                    (u, s) => Tone(1 + (s < 0f ? 0 : 1)))));
            }
            parts.Sort((p, q) => p.z.CompareTo(q.z));
            var outMap = new Dictionary<int, char>();
            foreach (var pt in parts)
            {
                var c = new Dictionary<int, char>(); pt.draw(c);
                foreach (var k in c.Keys)
                {
                    int x = k % 1000, y = k / 1000;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int n = (y + dy) * 1000 + (x + dx);
                        if (!c.ContainsKey(n)) outMap[n] = 'k';
                    }
                }
                foreach (var kv in c) outMap[kv.Key] = kv.Value;
            }
            var rows = new char[CanvasH][];
            for (int y = 0; y < CanvasH; y++) { rows[y] = new char[CanvasW]; for (int x = 0; x < CanvasW; x++) rows[y][x] = '.'; }
            foreach (var kv in outMap)
            {
                int x = kv.Key % 1000, y = kv.Key / 1000;
                if (x >= 0 && x < CanvasW && y >= 0 && y < CanvasH) rows[y][x] = kv.Value;
            }
            var o = new string[CanvasH];
            for (int y = 0; y < CanvasH; y++) o[y] = new string(rows[y]);
            return o;
        }

        static void Capsule(Dictionary<int, char> c, Vector2 a, Vector2 b, float rad, System.Func<float, float, char> col)
        {
            float dx = b.x - a.x, dy = b.y - a.y, l2 = Mathf.Max(1e-6f, dx * dx + dy * dy);
            for (int y = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - rad); y <= Mathf.CeilToInt(Mathf.Max(a.y, b.y) + rad); y++)
            for (int x = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - rad); x <= Mathf.CeilToInt(Mathf.Max(a.x, b.x) + rad); x++)
            {
                float px = x + 0.5f - a.x, py = y + 0.5f - a.y;
                float u = Mathf.Clamp01((px * dx + py * dy) / l2);
                float qx = px - u * dx, qy = py - u * dy;
                if (Mathf.Sqrt(qx * qx + qy * qy) > rad) continue;
                float sx = (x + 0.5f - (a.x + u * dx)) / rad;
                if (x >= 0 && y >= 0) c[y * 1000 + x] = col(u, sx);
            }
        }
    }
}
