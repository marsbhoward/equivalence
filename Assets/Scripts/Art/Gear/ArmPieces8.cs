using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// EIGHT DIRECTIONS (RIG.md "Arm pieces"): the eleven arm pieces as the user approved them built in 3D (method A,
    /// 2026-11-11; Tools/tier-art/views-extras.js ARM3D), baked by bake8x.js into Resources/Body8/ArmPieces8.bytes
    /// (GENERATED - rerun Tools/tier-art/regen8x.sh, never edit). Each piece is a cloud of surface points in its
    /// segment's own art frame - the shoulder cap, the upper arm, the forearm (X across, 5 the axis, + toward the
    /// body's left; Y down the arm; n toward the arm's front) - with the normal there and a colour RULE: the colour
    /// for each band of a light value along a direction (the kit's litTone for most; the chrome's and the cage's own),
    /// so <see cref="Body8"/> poses them on the drawn arm, any swing, and lights them per view.
    /// </summary>
    public static class ArmPieces8
    {
        public sealed class Rule
        {
            public Vector3 D;
            public float[] T;
            public Color32[] C;
            public bool HasRim;
            public Color32 Rim;

            /// <summary>The colour for a view normal <paramref name="n"/> (x right, y up, z toward the camera).</summary>
            public Color32 At(Vector3 n)
            {
                if (HasRim && Mathf.Abs(n.z) < 0.35f && n.x < -0.2f) return Rim;   // the ghost's violet rim (arms.js)
                float h = Vector3.Dot(n, D);
                int i = 0;
                while (i < T.Length && h > T[i]) i++;
                return C[i];
            }
        }

        public sealed class Prim
        {
            public int OutR;
            public float Bias, Alpha;
            /// <summary>Drawn only where nothing else is (Body8.PutUnder) - Sanguis's flecks, which side-on would stand
            /// between the camera and the arm. Baked as a bias under <see cref="UnderBias"/>, taken back off here.</summary>
            public bool Under;
        }

        const float UnderBias = -50f, UnderShift = 100f;

        public sealed class Cloud
        {
            public string Id;
            public Prim[] Prims;
            public int Count;
            public byte[] Seg, PrimOf;
            public Vector3[] P, N;      // art (X, Y, n); the art frame's normal
            public Rule[] RuleOf;
        }

        static Dictionary<string, Cloud> _clouds;
        static Cloud _glacies;

        /// <summary>A copy of <paramref name="c"/> sharing its points, each colour rule passed through <paramref name="map"/>
        /// (Glacies: Sanguis in ice).</summary>
        static Cloud Recoloured(Cloud c, string id, System.Func<Color32, Color32> map)
        {
            var rules = new Dictionary<Rule, Rule>();
            var r = new Cloud { Id = id, Prims = c.Prims, Count = c.Count, Seg = c.Seg, PrimOf = c.PrimOf, P = c.P, N = c.N, RuleOf = new Rule[c.Count] };
            for (int i = 0; i < c.Count; i++)
            {
                var src = c.RuleOf[i];
                if (!rules.TryGetValue(src, out var dst))
                {
                    dst = new Rule { D = src.D, T = src.T, C = new Color32[src.C.Length], HasRim = src.HasRim, Rim = src.HasRim ? map(src.Rim) : src.Rim };
                    for (int k = 0; k < src.C.Length; k++) dst.C[k] = map(src.C[k]);
                    rules[src] = dst;
                }
                r.RuleOf[i] = dst;
            }
            return r;
        }

        /// <summary>Fulmen's bolts: this many strikes baked, the rig cycling them (the mock re-strikes 12 times a second).</summary>
        public const int FulmenStrikes = 6;
        /// <summary>Sanguis's smoke: its one-second loop baked at this many points, the rig cycling them.</summary>
        public const int SanguisFrames = 8;

        /// <summary>The arm piece's cloud for an accessory kind, or null; <paramref name="strike"/> picks Fulmen's bolts or
        /// Sanguis's smoke picture.</summary>
        public static Cloud For(AccessoryKind kind, int strike = 0)
        {
            string id = kind switch
            {
                AccessoryKind.QuicksilverArm => "quicksilver",
                AccessoryKind.RubedoArm => "rubedo",
                AccessoryKind.VerdigrisArm => "verdigris",
                AccessoryKind.SalamanderArm => "salamander",
                AccessoryKind.SaltArm => "salt",
                AccessoryKind.CrucibleArm => "crucible",
                AccessoryKind.SanguisArm => "sanguis-smoke",
                AccessoryKind.HomunculusPlatedArm => "homunculus-plated",
                AccessoryKind.HomunculusHydraulicArm => "homunculus-hydraulic",
                AccessoryKind.FulmenArm => "fulmen-cage",
                AccessoryKind.PenumbraArm => "penumbra-echo",
                AccessoryKind.GlaciesArm => "sanguis-crystal",
                _ => null,
            };
            if (id == null) return null;
            if (_clouds == null) Load();
            if (kind == AccessoryKind.GlaciesArm) return _glacies ??= _clouds.TryGetValue(id, out var sg) ? Recoloured(sg, "glacies", ArmPieces.SanguisToIce) : null;
            if (kind == AccessoryKind.FulmenArm && strike % FulmenStrikes != 0) id += "#" + (strike % FulmenStrikes);
            if (kind == AccessoryKind.SanguisArm && strike % SanguisFrames != 0) id += "#" + (strike % SanguisFrames);
            return _clouds.TryGetValue(id, out var c) ? c : null;
        }

        static void Load()
        {
            _clouds = new Dictionary<string, Cloud>();
            _glacies = null;
            var asset = Resources.Load<TextAsset>("Body8/ArmPieces8");
            if (asset == null) { Debug.LogWarning("ArmPieces8: Resources/Body8/ArmPieces8.bytes missing (Tools/tier-art/regen8x.sh)"); return; }
            var b = asset.bytes;
            int o = 0;
            byte U8() => b[o++];
            ushort U16() { var v = System.BitConverter.ToUInt16(b, o); o += 2; return v; }
            uint U32() { var v = System.BitConverter.ToUInt32(b, o); o += 4; return v; }
            float F32() { var v = System.BitConverter.ToSingle(b, o); o += 4; return v; }
            short I16() { var v = System.BitConverter.ToInt16(b, o); o += 2; return v; }
            Color32 Rgba(uint v) => new Color32((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
            if (b.Length < 4 || b[0] != 'B' || b[1] != '8' || b[2] != 'A' || b[3] != '1') { Debug.LogWarning("ArmPieces8: bad header"); return; }
            o = 4;
            var rules = new Rule[U16()];
            for (int i = 0; i < rules.Length; i++)
            {
                var r = new Rule { D = new Vector3(F32(), F32(), F32()) };
                r.T = new float[U8()];
                for (int k = 0; k < r.T.Length; k++) r.T[k] = F32();
                r.C = new Color32[r.T.Length + 1];
                for (int k = 0; k < r.C.Length; k++) r.C[k] = Rgba(U32());
                uint rim = U32();
                r.HasRim = rim != 0; r.Rim = Rgba(rim);
                rules[i] = r;
            }
            int pieces = U8();
            for (int pc = 0; pc < pieces; pc++)
            {
                int len = U8();
                var c = new Cloud { Id = System.Text.Encoding.UTF8.GetString(b, o, len) };
                o += len;
                c.Prims = new Prim[U8()];
                for (int i = 0; i < c.Prims.Length; i++)
                {
                    var pr = new Prim { OutR = U8(), Bias = F32(), Alpha = F32() };
                    if (pr.Bias < UnderBias) { pr.Under = true; pr.Bias += UnderShift; }
                    c.Prims[i] = pr;
                }
                int n = (int)U32();
                c.Count = n; c.Seg = new byte[n]; c.PrimOf = new byte[n]; c.P = new Vector3[n]; c.N = new Vector3[n]; c.RuleOf = new Rule[n];
                for (int i = 0; i < n; i++)
                {
                    c.Seg[i] = U8(); c.PrimOf[i] = U8();
                    c.P[i] = new Vector3(I16() / 8f, I16() / 8f, I16() / 8f);
                    c.N[i] = new Vector3((sbyte)U8() / 127f, (sbyte)U8() / 127f, (sbyte)U8() / 127f);
                    c.RuleOf[i] = rules[U16()];
                }
                _clouds[c.Id] = c;
            }
        }
    }
}
