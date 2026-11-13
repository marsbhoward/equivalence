using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // EIGHT DIRECTIONS, THE EXTRAS (RIG.md "Scarves, accessories, arm pieces", the user's approved 3D mocks of
    // 2026-11-11, Tools/tier-art/views-extras.js): what moves is drawn here, live, into the figure's depth buffer -
    // the scarf's tail (scarf3d), the wrist wraps' tails (wraps3d) and an arm piece's cloud posed on the drawn arm
    // (ARM3D, baked by bake8x.js). The static pieces (the scarf's collar, the accessories) are Body8Art pieces.
    public static partial class Body8
    {
        /// <summary>What the rig wears beyond the baked pieces (PrimitiveCharacterRig.Dir8Extras.cs).</summary>
        public sealed class Extras
        {
            /// <summary>The scarf tail's tones (line .. glow), or null: no flowing scarf.</summary>
            public Color[] ScarfTones;
            /// <summary>Standing, the tail's drift through its loop (0..1; moving, it runs on the stride's phase).</summary>
            public float ScarfStand;
            /// <summary>The wearer's back surface (<see cref="BackTable"/>), which the tail is kept clear of.</summary>
            public float[] Back;
            /// <summary>The wrist wraps' tails' tones, or null.</summary>
            public Color[] WrapTones;
            /// <summary>The arm piece worn, or null.</summary>
            public ArmPieces8.Cloud Arm;
            /// <summary>Penumbra's idle drift now (ArmPieces.Drift, whole texels as the flat art shifts it), and its echoes:
            /// the arm where it was (<see cref="LastArm"/> of an earlier picture), its drift then, and how faint.</summary>
            public Vector2Int ArmDrift = Vector2Int.zero;
            public Frame[][] Echoes;
            public Vector2Int[] EchoDrift;
            public float[] EchoAlpha;
            /// <summary>The wraps' tails' wobble clock (the mock's t).</summary>
            public float WrapT = 0.4f;
            /// <summary>The wraps on a dash: how far they stream out (0 hanging .. 1), the way out behind the travel (drawn
            /// frame, level) and the flutter's angle (the flat rig's T * 14).</summary>
            public float WrapDash;
            public Vector3 WrapBack = Vector3.left;
            public float WrapFlutter;
            /// <summary>The accessory's pieces that move with the game (Body8.Live.cs), or null.</summary>
            public Live Live;
        }

        // ------------------------------------------------------------- the wearer's back
        const int BackX0 = -14, BackW = 33, BackH = 124;

        /// <summary>The worn pieces' back surface: per body (x, y) (texels, x the character's left) the farthest-back
        /// z of anything worn there (the pieces' back views, with the legs at rest), or +inf.</summary>
        public static float[] BackTable(IReadOnlyList<Body8Art.Piece> pieces)
        {
            var t = new float[BackW * BackH];
            for (int i = 0; i < t.Length; i++) t[i] = float.PositiveInfinity;
            void Mark(float x, float y, float z)
            {
                int xi = Mathf.RoundToInt(x) - BackX0, yi = Mathf.RoundToInt(y);
                if (xi < 0 || xi >= BackW || yi < 0 || yi >= BackH) return;
                if (z < t[yi * BackW + xi]) t[yi * BackW + xi] = z;
            }
            foreach (var piece in pieces)
            {
                if (piece.OverHead) continue;   // an accessory is not what the tail rests on
                var v = piece.Views?[Body8Art.Back];
                if (v == null || v.Depth == null) continue;
                for (int r = 0; r < v.H; r++)
                for (int c = 0; c < v.W; c++)
                {
                    byte d = v.Depth[r * v.W + c];
                    if (d == 255) continue;
                    // the back view (yaw 180): body x = -view x, z = -view z; the screen row sees y - z_view * DepthY
                    float vz = d * 0.5f - 64f, vx = v.X0 + c + 0.5f, sy = Body8Art.Top - (v.Y0 + r) - 0.5f;
                    Mark(-vx, sy + vz * Locomotion8.DepthY, -vz);
                }
            }
            // the legs at rest (views.js: x +-8, radius 4.2, soles to hips)
            for (int y = 4; y <= 62; y++)
                for (int x = -12; x <= 12; x++)
                {
                    float dx = Mathf.Abs(Mathf.Abs(x) - 8f);
                    if (dx < 4.2f) Mark(x, y, -Mathf.Sqrt(4.2f * 4.2f - dx * dx));
                }
            // fill single-row gaps the shear leaves
            for (int y = 1; y < BackH - 1; y++)
                for (int x = 0; x < BackW; x++)
                {
                    int i = y * BackW + x;
                    if (float.IsPositiveInfinity(t[i]) && !float.IsPositiveInfinity(t[i - BackW]) && !float.IsPositiveInfinity(t[i + BackW]))
                        t[i] = Mathf.Min(t[i - BackW], t[i + BackW]);
                }
            return t;
        }

        static float BackAt(float[] t, float x, float y)
        {
            if (t == null) return float.PositiveInfinity;
            int xi = Mathf.RoundToInt(x) - BackX0, yi = Mathf.RoundToInt(y);
            float best = float.PositiveInfinity;
            // within a texel: the tail is a ribbon round its centre line, not a ray
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int X = xi + dx, Y = yi + dy;
                if (X < 0 || X >= BackW || Y < 0 || Y >= BackH) continue;
                best = Mathf.Min(best, t[Y * BackW + X]);
            }
            return best;
        }

        /// <summary>Deeper than this behind the knot (texels) the back under it is a pack, not cloth: a cape lies 8-13 back,
        /// the Athanor pack 23.5.</summary>
        const float ScarfPackDepth = 18f;

        /// <summary>A pack covers the scarf's knot: the tail would come out of the pack's face, so it is tucked under it
        /// and not drawn (the collar still is). Read off what is worn, so any pack does it.</summary>
        public static bool ScarfUnderPack(float[] back)
        {
            if (back == null) return false;
            for (float y = ScarfKnot.y - 20f; y <= ScarfKnot.y; y += 4f)
                if (BackAt(back, ScarfKnot.x, y) < -ScarfPackDepth) return true;
            return false;
        }

        // ------------------------------------------------------------- the scarf's tail (scarf3d)
        // The approved flow: 80 long, 14 wide with a slight swell and pinch, tapering to 2 over its last quarter; turned
        // back from hanging by the gait's lift (stand 3, walk 38, run 68 degrees at the knot, the hem lagging up to 45 %
        // more, capped at 85), a wave travelling down it; over the cape, kept 1.1 clear of the wearer.
        struct ScarfGait { public float Lift, Amp, Cycles, Lambda, Side; }
        static readonly ScarfGait ScarfStandGait = new() { Lift = 3f, Amp = 1.4f, Cycles = 1f, Lambda = 70f, Side = 1.2f };
        static readonly ScarfGait ScarfWalkGait = new() { Lift = 38f, Amp = 6.0f, Cycles = 1f, Lambda = 42f, Side = 2.6f };
        static readonly ScarfGait ScarfRunGait = new() { Lift = 68f, Amp = 8.5f, Cycles = 2f, Lambda = 34f, Side = 3.4f };
        const int ScarfN = 40;
        const float ScarfLen = 80f, ScarfWidth = 14f, ScarfRZ = 9.8f, ScarfHalfThick = 0.8f;
        static readonly Vector3 ScarfKnot = new(2f, 101f, -ScarfRZ + 0.5f);
        static Vector3[] _scarfPts;
        static float[] _scarfW;

        static void ScarfTail(Extras ex, System.Func<Vector3, Vector3> body, Vector3 left, Vector3 f, Locomotion8.Gait gait, float size, float phase, List<Part> parts)
        {
            var g = gait == Locomotion8.Run ? ScarfRunGait : ScarfWalkGait;
            float k = Mathf.Clamp01(size);
            // standing to the gait by the stride's size: the lift and its hem lag, the wave
            float lift = Mathf.Lerp(ScarfStandGait.Lift, g.Lift, k), lag = Mathf.Lerp(0f, 0.45f * g.Lift, k);
            float amp = Mathf.Lerp(ScarfStandGait.Amp, g.Amp, k), lambda = Mathf.Lerp(ScarfStandGait.Lambda, g.Lambda, k), sideAmp = Mathf.Lerp(ScarfStandGait.Side, g.Side, k);
            float cycles = k > 0f ? g.Cycles : 1f, ph = phase * Mathf.PI * 2f;
            _scarfPts ??= new Vector3[ScarfN + 1];
            _scarfW ??= new float[ScarfN + 1];
            var P = ScarfKnot;
            float step = ScarfLen / ScarfN;
            for (int i = 0; i <= ScarfN; i++)
            {
                float s = i * step, t = s / ScarfLen;
                if (i > 0)
                {
                    float a = Mathf.Min((lift + lag * t) * Mathf.Deg2Rad, 85f * Mathf.Deg2Rad)
                            + amp * Mathf.Pow(t, 1.1f) * Mathf.Sin(Mathf.PI * 2f * s / lambda - ph * cycles) / 10f;
                    float sideA = sideAmp * Mathf.Pow(t, 1.2f) * Mathf.Sin(Mathf.PI * 2f * s / (lambda * 1.3f) - ph * cycles + 1.1f) / 12f;
                    var d = new Vector3(Mathf.Sin(sideA) + 0.035f, -Mathf.Cos(a), -Mathf.Sin(a)).normalized;
                    var Q = P + d * step;
                    // over the cape and clear of the body and legs: pushed back until 1.1 behind them
                    for (int it = 0; it < 30 && Q.z > BackAt(ex.Back, Q.x, Q.y) - 1.1f; it++) Q.z -= 0.4f;
                    P = Q;
                }
                _scarfPts[i] = P;
                _scarfW[i] = t < 0.75f ? ScarfWidth * (1f + 0.08f * Mathf.Sin(s * 0.13f + 0.6f)) : ScarfWidth - 12f * Mathf.Pow((t - 0.75f) / 0.25f, 1.2f);
            }

            int id = parts.Count; parts.Add(new Part { OutR = 4 });
            var tones = ex.ScarfTones;
            var xAxis = left;   // the ribbon's width runs along the body's x
            for (int i = 0; i < ScarfN; i++)
            {
                var A = _scarfPts[i]; var B = _scarfPts[i + 1];
                var seg = B - A;
                var nm = new Vector3(0f, -seg.z, seg.y).normalized;          // the face's normal, in the y-z plane
                var nv = (left * nm.x + Vector3.up * nm.y + f * nm.z).normalized;
                if (nv.z < 0f) nv = -nv;                                       // the face toward the camera
                var face = tones[Mathf.Clamp(TexelKit.LitTone(nv.x, -nv.y, nv.z, 0f), 1, 4)];
                // the edges: the ribbon's sides along x
                var ex1 = left.z >= 0f ? left : -left;
                var edge = tones[Mathf.Clamp(TexelKit.LitTone(ex1.x, -ex1.y, ex1.z, 0f), 1, 4)];
                int nu = Mathf.Max(2, Mathf.CeilToInt(seg.magnitude / 0.35f));
                for (int u = 0; u < nu; u++)
                {
                    float fu = (u + 0.5f) / nu;
                    var C = Vector3.Lerp(A, B, fu);
                    float hw = Mathf.Lerp(_scarfW[i], _scarfW[i + 1], fu) * 0.5f;
                    int nx = Mathf.Max(1, Mathf.CeilToInt(hw * 2f / 0.4f));
                    for (int xi = 0; xi <= nx; xi++)
                    {
                        float xx = -hw + 2f * hw * xi / nx;
                        bool rim = xi == 0 || xi == nx;
                        for (int o = -1; o <= 1; o++)
                        {
                            var q = C + new Vector3(xx, 0f, 0f) + nm * (o * ScarfHalfThick);
                            var p = body(q);
                            var sc = Locomotion8Screen(p);
                            int X = Mathf.FloorToInt(sc.x), Y = Mathf.FloorToInt(sc.y);
                            if (X < 0 || Y < 0 || X >= _W || Y >= _H) continue;
                            Put(X, Y, p.z, rim && o != 0 ? edge : face, id);
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------- the wrist wraps' tails (wraps3d)
        // Two from the outer edge of each wrist, hanging straight down: 24 and 18 long, 3 wide narrowing to 2 over the
        // last 6, the second a tone darker; the approved wobble (its clock, Extras.WrapT). On a dash they stream out
        // level behind the travel, fluttering, as the flat rig's (Accessories.WrapsFor), the ribbon turned on its edge.
        static void WrapTails(Extras ex, (Vector3, Vector3)[] fore, Vector3 r3, Vector3 left, Vector3 f, List<Part> parts)
        {
            float T = ex.WrapT, dash = ex.WrapDash;
            var tones = ex.WrapTones;
            var back = ex.WrapBack;
            var side = Vector3.Cross(Vector3.up, back).normalized;
            // the ribbon's width: across the body hanging, up and down streaming; its thickness the other way
            var across = Vector3.Lerp(left, Vector3.up, dash).normalized;
            var thick = Vector3.Lerp(f, side, dash).normalized;
            var deep = Vector3.Cross(across, thick).normalized;
            for (int arm = 0; arm < 2; arm++)
            {
                var (el, wr) = fore[arm];
                var outward = arm == 0 ? r3 : -r3;
                var top = Vector3.Lerp(el, wr, 0.82f) + outward * 4.6f;
                foreach (var (len, ph, ti) in new[] { (24, 0f, 0), (18, 1.7f, 1) })
                {
                    int id = parts.Count; parts.Add(new Part { OutR = 4 });
                    // the face toward the camera: the body's x or z, whichever looks at it more (streaming, the side)
                    Vector3 n = dash > 0.5f ? (side.z >= 0f ? side : -side)
                        : Mathf.Abs(left.z) > Mathf.Abs(f.z) ? (left.z >= 0f ? left : -left) : (f.z >= 0f ? f : -f);
                    var col = tones[Mathf.Clamp(Mathf.Min(4, TexelKit.LitTone(n.x, -n.y, n.z, 0f)) - ti, 0, 5)];
                    for (int kk = 0; kk < len; kk++)
                    {
                        float half = kk < len - 6 ? 1.5f : 1.0f;
                        float wob = Mathf.Sin(T * 3.2f - kk * 0.3f + ph) * Mathf.Min(1f, kk / 6f) * 1.3f;
                        var hang = top + outward * (kk * 0.18f + ti * 1.5f) + left * wob - Vector3.up * kk - f * (ti * 0.8f);
                        var c = hang;
                        if (dash > 0f)
                        {
                            float flut = Mathf.Sin(ex.WrapFlutter - kk * 0.5f + ph) * Mathf.Min(1f, kk / 5f) * 2f;
                            var stream = top + back * (kk + 1 + ti) + outward * (ti * 1.5f) + Vector3.up * (ti * 2f - kk * 0.2f - flut);
                            c = Vector3.Lerp(hang, stream, dash);
                        }
                        for (float a = -half; a <= half + 0.01f; a += 0.5f)
                        for (float zz = -0.8f; zz <= 0.81f; zz += 0.8f)
                        for (float yy = -0.5f; yy <= 0.51f; yy += 0.5f)
                        {
                            var p = c + across * a + thick * zz + deep * yy;
                            var sc = Locomotion8Screen(p);
                            int X = Mathf.FloorToInt(sc.x), Y = Mathf.FloorToInt(sc.y);
                            if (X < 0 || Y < 0 || X >= _W || Y >= _H) continue;
                            Put(X, Y, p.z, col, id);
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------- an arm piece (ARM3D, method A)
        /// <summary>The mock's arm (armFrames): the shoulder joint at (-19, 101, 0) in the body's frame (2.5 out from the
        /// bare arm - the pieces are thicker), the elbow 20 down the upper arm (art rows 29.7 .. 49.7), the hand 15.3
        /// down the forearm (art row 65). The cap turns 0.8 of the upper arm (arms.js SHOULDER_SHARE).</summary>
        static readonly Vector3 PieceShoulder = new(-19f, 101f, 0f);
        const float PieceUpper = 20f, PieceFore = 15.3f, PieceUpperY0 = 29.7f, PieceForeY0 = 49.7f, PieceCapY0 = 34f, PieceCapShare = 0.8f;

        /// <summary>An arm piece's segment (cap, upper arm, forearm) as posed: its joint, its art's across (X), down
        /// (D) and out (N) axes in the drawn frame, the art row at the joint and the stretch.</summary>
        public struct Frame { public Vector3 J, X, D, N; public float Y0, Scale; }

        /// <summary>The arm piece's three segments in the last picture drawn or fetched from the cache, or null.</summary>
        public static Frame[] LastArm;

        /// <summary>Where an arm piece's light (ArmPieces' art coordinates: x across 0..10, y the art row; joint 1 the
        /// shoulder cap, 2 the upper arm, 3 the forearm in its unstretched rows) sits in the drawn frame - on the
        /// arm's surface toward the camera.</summary>
        public static Vector3 ArmLight(Frame[] arm, int joint, Vector2 at)
        {
            float y = at.y + 0.5f;
            var F = joint == 1 ? arm[0] : joint == 3 ? arm[2] : arm[1];
            if (joint == 3) y += Accessories.ElbowExtra;   // back to the stretched rows the cloud is in
            return F.J + F.X * (at.x + 0.5f - 5f) + F.D * ((y - F.Y0) * F.Scale) + new Vector3(0f, 0f, 3f);
        }

        static void ArmPiece(Extras ex, System.Func<Vector3, Vector3> body, Vector3 left, Vector3 f,
                             Vector3 sh, Vector3 el, Vector3 wr, List<Part> parts)
        {
            var cloud = ex.Arm;
            // the piece's own arm (the mock's proportions) from its shoulder to the drawn hand, bending as the drawn
            // elbow does; a reach longer than it stretches it along the arm
            var J = body(PieceShoulder);
            var D = wr - J; float L = Mathf.Max(0.01f, D.magnitude);
            float k = Mathf.Max(1f, L / (PieceUpper + PieceFore - 0.01f));
            float a = PieceUpper * k, b = PieceFore * k;
            var dn = D / L;
            L = Mathf.Min(L, a + b - 0.01f);
            float x = (a * a - b * b + L * L) / (2f * L), h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            var pole = (el - J) - dn * Vector3.Dot(el - J, dn);
            pole = pole.sqrMagnitude > 1e-4f ? pole.normalized : -f;
            var E = J + dn * x + pole * h;
            var W = J + dn * L;
            var d1 = (E - J).normalized; var d2 = (W - E).normalized;

            Frame Of(Vector3 j, Vector3 d, float y0, float scale)
            {
                var R = Quaternion.FromToRotation(Vector3.down, d);
                return new Frame { J = j, X = R * left, D = d, N = R * f, Y0 = y0, Scale = scale };
            }
            var dc = Vector3.Slerp(Vector3.down, d1, PieceCapShare).normalized;
            var frames = new[] { Of(J, dc, PieceCapY0, 1f), Of(J, d1, PieceUpperY0, k), Of(E, d2, PieceForeY0, k) };
            _drawnArm = frames;

            CloudOn(cloud, frames, ex.ArmDrift, 1f, 0f, parts);
            // Penumbra's echoes: fainter copies where the arm just was, behind it on a tie (the flat rig's)
            if (ex.Echoes != null)
                for (int e = 0; e < ex.Echoes.Length; e++)
                    if (ex.Echoes[e] != null) CloudOn(cloud, ex.Echoes[e], ex.EchoDrift[e], ex.EchoAlpha[e], -0.5f - e, parts);
        }

        /// <summary>The mock's drift baked into Penumbra's cloud (ARM_ST.t 0.4): the arm's whole-texel shift, the cap half.</summary>
        static readonly Vector2Int BakedDrift = new(1, 1), BakedDriftCap = new(1, 0);

        /// <summary>A cloud posed on an arm's segments: each point to the drawn frame, lit by its rule, one part a prim.
        /// <paramref name="drift"/> shifts it in the art (flat texels: x across, y down the arm; the cap half of it) - zero
        /// is the piece as baked.</summary>
        static void CloudOn(ArmPieces8.Cloud cloud, Frame[] frames, Vector2Int drift, float alpha, float bias, List<Part> parts)
        {
            int first = parts.Count;
            foreach (var pr in cloud.Prims) parts.Add(new Part { OutR = pr.OutR, Bias = pr.Bias + bias, Alpha = pr.Alpha * alpha });
            var dArm = drift == Vector2Int.zero ? Vector2.zero : (Vector2)(drift - BakedDrift);
            var dCap = drift == Vector2Int.zero ? Vector2.zero : new Vector2(Mathf.Round(drift.x * 0.5f), Mathf.Round(drift.y * 0.5f)) - (Vector2)BakedDriftCap;
            for (int i = 0; i < cloud.Count; i++)
            {
                var F = frames[cloud.Seg[i]];
                var P = cloud.P[i];
                var d = cloud.Seg[i] == 0 ? dCap : dArm;
                var p = F.J + F.X * (P.x - 5f + d.x) + F.D * ((P.y - F.Y0 + d.y) * F.Scale) + F.N * P.z;
                var sc = Locomotion8Screen(p);
                int X = Mathf.FloorToInt(sc.x), Y = Mathf.FloorToInt(sc.y);
                if (X < 0 || Y < 0 || X >= _W || Y >= _H) continue;
                var Nn = cloud.N[i];
                var nv = (F.X * Nn.x + F.D * Nn.y + F.N * Nn.z).normalized;
                Put(X, Y, p.z, cloud.RuleOf[i].At(nv), first + cloud.PrimOf[i]);
            }
        }
    
        // ------------------------------------------------------------- the orbiting stones (stones3d)
        /// <summary>Where a point of the body's frame (views.js: x the character's left, y up, z forward) is drawn in
        /// this pose's picture: texels from the picture's pivot (the ground under the hips), x right, y UP - riding
        /// the gait's lean and bob as the worn pieces do.</summary>
        public static Vector3 BodyPoint(Locomotion8.Pose pose, bool mirrored, Vector3 q)
        {
            var f = pose.Forward; var left = mirrored ? pose.Right : -pose.Right;
            float lean = Mathf.Tan(pose.Lean * Mathf.Deg2Rad), h = q.y - Pelvis;
            return new Vector3(0f, pose.HipY + 4f, 0f) + Vector3.up * h + f * (h * lean) + left * q.x + f * q.z;
        }

        /// <summary>The stones' orbit (accessories.js stones(), the approved level circle): radius 28 round the waist,
        /// centred 89 up, each bobbing 1.5; a stone at angle <paramref name="a"/> of the orbit, in the body's frame.</summary>
        public const float StoneOrbit = 28f, StoneY = 89f;

        static Dictionary<int, Sprite> _stoneSprites;
        public const int StoneBins = 24;
        const int StoneW = 24, StoneH = 30, StonePX = 12, StonePY = 18;

        /// <summary>One cut stone (a pentagon profile crossed with itself: four faces), turned <paramref name="bin"/> /
        /// <see cref="StoneBins"/> of a quarter turn in the picture, lit by the kit's light, outlined 2 - pivoted on its
        /// centre. Cached per turn.</summary>
        public static Sprite StoneSprite(int bin, Color[] tones, float ppu)
        {
            _stoneSprites ??= new Dictionary<int, Sprite>();
            bin = ((bin % StoneBins) + StoneBins) % StoneBins;
            if (_stoneSprites.TryGetValue(bin, out var hit) && hit != null) return hit;
            float th = bin * (Mathf.PI * 0.5f) / StoneBins, c = Mathf.Cos(th), sn = Mathf.Sin(th);
            float Prof(float u, float y)
            {
                const float hw = 4.5f;
                float d = float.MinValue;
                var P = new[] { (0f, 8f), (hw, 1f), (hw * 0.6f, -5.6f), (-hw * 0.6f, -5.6f), (-hw, 1f) };
                for (int i = 0; i < P.Length; i++)
                {
                    var (x0, y0) = P[i]; var (x1, y1) = P[(i + 1) % P.Length];
                    float ex = x1 - x0, ey = y1 - y0, L = Mathf.Sqrt(ex * ex + ey * ey);
                    d = Mathf.Max(d, ((y - y0) * ex - (u - x0) * ey) / L);
                }
                return d;
            }
            float Sdf(Vector3 p) => Mathf.Max(Prof(p.x * c + p.z * sn, p.y), Prof(-p.x * sn + p.z * c, p.y));
            var px = new Color[StoneW * StoneH];
            var won = new bool[StoneW * StoneH];
            for (int row = 0; row < StoneH; row++)
            for (int col = 0; col < StoneW; col++)
            {
                float sx = col + 0.5f - StonePX, sy = StonePY - row - 0.5f;
                float t = 0f; bool hitP = false; Vector3 p = default;
                while (t < 24f)
                {
                    float vz = 12f - t;
                    p = new Vector3(sx, sy + vz * Locomotion8.DepthY, vz);
                    float d = Sdf(p);
                    if (d < 0.02f) { hitP = true; break; }
                    t += Mathf.Max(0.1f, d * 0.8f);
                }
                if (!hitP) continue;
                const float e = 0.2f;
                var n = new Vector3(Sdf(p + Vector3.right * e) - Sdf(p - Vector3.right * e), Sdf(p + Vector3.up * e) - Sdf(p - Vector3.up * e),
                                    Sdf(p + Vector3.forward * e) - Sdf(p - Vector3.forward * e)).normalized;
                int l = TexelKit.LitTone(n.x, -n.y, n.z, 0f);
                px[row * StoneW + col] = tones[p.y > 5f ? 5 : l >= 4 ? 4 : l >= 3 ? 3 : 2];
                won[row * StoneW + col] = true;
            }
            const int R = 2, R2 = R * R + 2;
            var outp = (Color[])px.Clone();
            for (int y = 0; y < StoneH; y++)
            for (int x = 0; x < StoneW; x++)
            {
                if (!won[y * StoneW + x]) continue;
                for (int dy = -R; dy <= R; dy++)
                for (int dx = -R; dx <= R; dx++)
                {
                    int X = x + dx, Y = y + dy;
                    if (dx * dx + dy * dy > R2 || X < 0 || Y < 0 || X >= StoneW || Y >= StoneH || won[Y * StoneW + X]) continue;
                    outp[Y * StoneW + X] = Outline;
                }
            }
            var sprite = PixelSprite.FromPixels(outp, StoneW, StoneH, new Vector2Int(StonePX, StonePY), ppu);
            _stoneSprites[bin] = sprite;
            return sprite;
        }
    }
}
