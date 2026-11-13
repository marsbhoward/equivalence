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
            public float Height(float p) => Bob * Mathf.Cos(4f * Mathf.PI * (p - BobPhase)) + BobOffset;
        }

        public static readonly Gait Run = new Gait
        {
            Name = "run", Frames = 32,
            Foot = new[] { new[] { 0f, 0.30f, 0f }, new[] { 0.33f, -0.36f, 0f }, new[] { 0.48f, -0.55f, 0.42f }, new[] { 0.66f, -0.08f, 0.55f }, new[] { 0.84f, 0.46f, 0.24f } },
            Pitch = new[] { new[] { 0f, 18f }, new[] { 0.12f, 0f }, new[] { 0.33f, -40f }, new[] { 0.5f, -55f }, new[] { 0.7f, -20f }, new[] { 0.86f, 10f } },
            Bob = 3.2f, BobPhase = 0.42f, BobOffset = -1.5f, Lean = 11f, Base = 54.4f, HipDrop = 0f, Arm = 34f, Elbow = 88f, Trail = 11f,
        };

        public static readonly Gait Walk = new Gait
        {
            Name = "walk", Frames = 48,
            Foot = new[] { new[] { 0f, 0.30f, 0f }, new[] { 0.6f, -0.30f, 0f }, new[] { 0.72f, -0.18f, 0.10f }, new[] { 0.86f, 0.12f, 0.13f }, new[] { 0.96f, 0.32f, 0.03f } },
            Pitch = new[] { new[] { 0f, 16f }, new[] { 0.1f, 0f }, new[] { 0.55f, -8f }, new[] { 0.62f, -30f }, new[] { 0.75f, -10f }, new[] { 0.92f, 8f } },
            Bob = 1.3f, BobPhase = 0.30f, BobOffset = 0f, Lean = 3f, Base = 56.3f, HipDrop = 1f, Arm = 14f, Elbow = 28f, Trail = 4f,
        };

        static float[] Keys(float[][] keys, float p)
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
                var foot = Keys(g.Foot, p); float pitch = Keys(g.Pitch, p)[0] * size;
                float fw = foot[0] * size, lift = foot[1] * size;
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
