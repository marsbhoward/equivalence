using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>The Mortar's frames, in the order <see cref="MortarArt.For"/> returns them.</summary>
    public enum MortarFrame { Walk0, Walk1, Walk2, Walk3, Plant0, Plant1, Lob, Ready, Breath0, Breath1, Rest, Jolt }

    /// <summary>
    /// The Mortar as the SALAMANDER (the user's sign-off, 2026-10-19, from the mock-up at
    /// https://claude.ai/artifact/RfxwLWxMjmozYiJtNoQSE5): the alchemists' fire elemental, a long,
    /// low newt of basalt crust with magma in its cracks - belly near the floor, short legs splayed
    /// out to the side, a flat wide head held low with bulging eyes, gill fronds swept back like an
    /// axolotl's and a thick tail. Drawn 1.75x on the grid (the user asked for it nearer the
    /// character's size, and less like a dog); the body the game hits stays MortarSize.
    ///
    /// THE BODY SAYS THE ATTACK. It walks with a newt's sprawl (WALK0-3, diagonal legs together,
    /// paced by distance); to lob it rears back as its throat swells with magma (PLANT0), the sac
    /// full and blazing for the last 30% (PLANT1, the tell), and throws its head up to hurl the
    /// glob (LOB) - the shell then flies as <see cref="MortarShell"/>. Rushed, it crouches with
    /// its gills flared and jaws opening (READY, the tell) and breathes fire (BREATH0/1 - the gout
    /// is <see cref="MortarFlame"/>). It recovers head low and dimmed (REST); a flinch JOLTS it.
    ///
    /// The magma is CRIMSON (<see cref="Accent"/>), kept apart from the Stinger's and the Brimstone
    /// Heart's orange, and the red its shells always blinked. THE ELITE is 1.2x, with tall crust
    /// spines down its back, a fourth gill frond and a longer tail. ONE RIG, TWO MATERIALS:
    /// Construct is BASALT (black-brown crust, a ruddy belly, cracks that light), Automaton is
    /// IRONBOUND (cast iron, the cracks as piping, rivets down the flank). Ported from the mock-up's
    /// JavaScript line for line - change both or neither.
    /// </summary>
    public static class MortarArt
    {
        const float RigScale = 1.75f, EliteScale = 1.2f;
        const int W = 54, H = 28;
        const float PivotX = 21f, PivotY = 4f;

        enum Slot { Body, Fang, Maw, Glob, Sac, Gill, Socket, Eye }
        enum Decor { None, Tail, Leg, Torso, Spine, Crust, Jaw, Cranium, Snout }

        sealed class Mat
        {
            public Color[] Hide, Belly;
            public Color Crack, Maw, Fang, Rivet, Out;
            public bool Iron;
        }

        static readonly Mat Basalt = new()
        {
            Hide = TexelRig.Hex("#18120f", "#2c221c", "#45362c", "#64503f"),
            Belly = TexelRig.Hex("#3e2418", "#5c3624"),
            Crack = TexelRig.Hex("#2a0806")[0],
            Maw = TexelRig.Hex("#140806")[0],
            Fang = TexelRig.Hex("#d8c8b0")[0],
            Out = TexelRig.Hex("#090605")[0],
        };

        static readonly Mat Ironbound = new()
        {
            Hide = TexelRig.Hex("#1f2126", "#363a42", "#565c67", "#878e99"),
            Belly = TexelRig.Hex("#2a2d33", "#434852"),
            Crack = TexelRig.Hex("#2a0806")[0],
            Maw = TexelRig.Hex("#100f12")[0],
            Fang = TexelRig.Hex("#a3abb7")[0],
            Rivet = TexelRig.Hex("#b6bdc7")[0],
            Out = TexelRig.Hex("#0a0a0c")[0],
            Iron = true,
        };

        static readonly Color Socket = TexelRig.Hex("#0c0606")[0];

        /// <summary>Magma, dim to hot - crimson, not the Stinger's orange.</summary>
        public static readonly Color[] Accent = TexelRig.Hex("#3a0a08", "#9a1c10", "#ff4a1c", "#ffd27a");

        /// <summary>Colours the biome weathering never touches - the glows and tells (CreatureWeathering).</summary>
        internal static Color[] Reserved => _reserved ??= Accent;
        static Color[] _reserved;

        // cracks (Ironbound: the piping), each in its part's own frame
        static readonly Vector2[][] TorsoVeins =
        {
            new[] { V(-0.95f, 0.35f), V(-0.4f, 0.62f), V(0.2f, 0.55f), V(0.9f, 0.3f) },
            new[] { V(-0.3f, 0.6f), V(-0.15f, 0.1f) },
            new[] { V(0.45f, 0.5f), V(0.6f, 0.0f) },
        };
        static readonly Vector2[][] CraniumVeins = { new[] { V(-0.8f, 0.4f), V(-0.1f, 0.55f), V(0.6f, 0.3f) } };

        static Vector2 V(float x, float y) => new(x, y);

        // ---------------------------------------------------------------- poses

        sealed class Pose
        {
            public float Step, Bob, Tail, Rear, HeadRot, Jaw, Sac, Gill;
            public int Heat = 1, Eye = 1, Flare, Seed = 1;
            public bool Glob, Chips;
        }

        // step: the walk's phase 0..3; rear: 0 low .. 1 reared (negative: crouched); headRot/jaw:
        // the head; sac: the throat's magma 0..1; heat/eye: 0..3 glow; gill: the fronds flared;
        // glob: a shell leaving; flare: fire at the jaws. Indexed by MortarFrame.
        static readonly Pose[] Poses =
        {
            new() { HeadRot = -3 },
            new() { Step = 1, Bob = 0.2f, Tail = 1, HeadRot = -2 },
            new() { Step = 2, Tail = 2, HeadRot = -3 },
            new() { Step = 3, Bob = 0.2f, Tail = 3, HeadRot = -2 },
            new() { Rear = 0.35f, HeadRot = 15, Sac = 0.6f, Heat = 2, Jaw = 4, Eye = 2, Gill = 0.3f, Tail = 1 },
            new() { Rear = 0.55f, HeadRot = 25, Sac = 1, Heat = 3, Jaw = 8, Eye = 3, Gill = 0.6f, Tail = 2 },
            new() { Rear = 0.7f, HeadRot = 40, Jaw = 36, Heat = 3, Eye = 3, Gill = 0.8f, Glob = true, Tail = 3 },
            new() { Rear = -0.15f, HeadRot = -6, Jaw = 16, Sac = 0.4f, Heat = 3, Eye = 3, Gill = 1, Bob = -0.4f },
            new() { Rear = -0.1f, HeadRot = -2, Jaw = 34, Heat = 3, Eye = 3, Gill = 1, Flare = 1, Bob = -0.3f },
            new() { Rear = -0.1f, HeadRot = 2, Jaw = 38, Heat = 3, Eye = 3, Gill = 0.8f, Flare = 2, Bob = -0.3f, Tail = 1 },
            new() { Heat = 0, Eye = 1, Jaw = 10, HeadRot = -8, Bob = -0.2f },
            new() { Rear = 0.2f, HeadRot = 25, Jaw = 20, Heat = 0, Eye = 0, Chips = true, Seed = 2, Tail = 2 },
        };

        // ---------------------------------------------------------------- cache

        static readonly Dictionary<int, Sprite[]> _cache = new();

        /// <summary>Every frame of one look and tier, indexed by <see cref="MortarFrame"/>.</summary>
        public static Sprite[] For(EnemyLook look, bool elite) => For(look, elite, Art.Biomes.BiomeKind.Tower);

        /// <summary>The same, wearing the place's accent (CreatureAccents) - weathered after by
        /// CreatureWeathering.</summary>
        public static Sprite[] For(EnemyLook look, bool elite, Art.Biomes.BiomeKind biome)
        {
            int key = (look == EnemyLook.Automaton ? 1 : 0) | (elite ? 2 : 0) | ((int)biome << 2);
            if (_cache.TryGetValue(key, out var hit) && hit != null && hit.Length > 0 && hit[0] != null) return hit;

            var mat = look == EnemyLook.Automaton ? Ironbound : Basalt;
            var frames = new Sprite[Poses.Length];
            for (int i = 0; i < frames.Length; i++) frames[i] = Render(i, mat, elite, biome);
            _cache[key] = frames;
            return frames;
        }

        /// <summary>Where the jaws are on <paramref name="frame"/>, in world units from the body's
        /// position, facing +x (mirror x when it faces left).</summary>
        public static Vector2 MouthOffset(MortarFrame frame, bool elite)
        {
            float scale = (elite ? EliteScale : 1f) * RigScale;
            var m = Build(Poses[(int)frame], false, elite, out _, null);
            var pivot = new Vector2(Mathf.Round(PivotX * scale), Mathf.Round(PivotY * scale));
            return (m * scale - pivot) / TexelRig.TexelsPerUnit;
        }

        // ---------------------------------------------------------------- the rig (facing +x)

        static Vector2 Rot(Vector2 p, Vector2 o, float deg)
        {
            float c = Mathf.Cos(TexelRig.Rad(deg)), s = Mathf.Sin(TexelRig.Rad(deg)), dx = p.x - o.x, dy = p.y - o.y;
            return new Vector2(o.x + dx * c - dy * s, o.y + dx * s + dy * c);
        }

        enum FxKind { Flare, Chips }
        sealed class Fx { public FxKind Kind; public Vector2 C; public float Dir; public int N; }

        /// <summary>Builds the rig into <paramref name="rig"/> and returns where the jaws are.</summary>
        static Vector2 Build(Pose p, bool iron, bool elite, out TexelRig.Rig rig, List<Fx> fx)
        {
            var r_ = new TexelRig.Rig();
            rig = r_;
            void Add(float order, Slot slot, TexelRig.Shape test, bool far = false, bool sep = false, Decor decor = Decor.None, float level = 0, float extra = 0)
                => r_.Add(order, (int)slot, test, far, sep, (int)decor, level, extra);

            float oy = p.Bob;
            var hip = V(15, 4.2f + oy);
            Vector2 R(Vector2 q) => Rot(q, hip, p.Rear * 30f);   // rearing lifts the front about the hips

            // the tail: long, thick at the root, swaying
            float tl = elite ? 1.15f : 1f;
            const int segs = 7;
            var prev = V(hip.x - 0.5f, hip.y + 0.2f);
            for (int k = 1; k <= segs; k++)
            {
                float t = k / (float)segs;
                var q = V(hip.x - k * 1.75f * tl, hip.y - t * 2.3f + Mathf.Sin(k * 0.8f + p.Tail) * 0.7f * t + 0.2f);
                q.y = Mathf.Max(q.y, 0.9f);
                Add(0.5f + k * 0.01f, Slot.Body, TexelRig.Taper(prev, q, 2.9f - (t - 1f / segs) * 2.3f, 2.9f - t * 2.3f), decor: Decor.Tail);
                prev = q;
            }
            // the legs: short, splayed out to the side - the elbow at the body's height, the forearm down
            var bs = V(hip.x + 0.6f, hip.y - 0.8f);
            var fs = R(V(23, 4.4f + oy));
            float a = p.Step * Mathf.PI / 2f;
            Vector2 Gait(float ph) => V(2.0f * Mathf.Cos(ph), Mathf.Max(0f, Mathf.Sin(ph)) * 0.9f);
            void Leg(float order, bool far, Vector2 sh, Vector2 foot)
            {
                var el = V(sh.x + (foot.x - sh.x) * 0.55f, sh.y + 0.5f);
                Add(order, Slot.Body, TexelRig.Taper(sh, el, 1.25f, 1.0f), far, !far, Decor.Leg);
                Add(order + 0.01f, Slot.Body, TexelRig.Taper(el, foot, 1.0f, 0.8f), far, !far, Decor.Leg);
                Add(order + 0.02f, Slot.Body, TexelRig.Ellipse(foot.x + 0.3f, foot.y, 1.5f, 0.6f), far, decor: Decor.Leg);
                for (int i = 0; i < 4; i++)
                    Add(order + 0.03f, Slot.Fang, TexelRig.Taper(V(foot.x + 0.6f + i * 0.35f, foot.y - 0.1f),
                        V(foot.x + 1.4f + i * 0.55f, foot.y - 0.5f + (i % 2) * 0.2f), 0.28f, 0.1f), far);
            }
            var g1 = Gait(a);
            var g2 = Gait(a + Mathf.PI);
            float Fy(float lift) => Mathf.Max(0.6f + lift, fs.y - 3.8f);
            Leg(1f, true, V(fs.x - 1.4f, fs.y - 0.3f), V(fs.x + 0.4f + g2.x - 1.4f, Fy(g2.y)));    // far front
            Leg(1.1f, true, V(bs.x - 1.4f, bs.y - 0.3f), V(bs.x - 0.6f + g1.x - 1.4f, 0.6f + g1.y)); // far back
            // the body: long and flat, the belly near the floor; tilted up as it rears
            var mid = Vector2.Lerp(hip, fs, 0.5f);
            float ang = Mathf.Atan2(fs.y - hip.y, fs.x - hip.x) * Mathf.Rad2Deg, len = Vector2.Distance(hip, fs);
            Add(2f, Slot.Body, TexelRig.Ellipse(mid.x + 0.4f, mid.y + 0.6f, len / 2f + 4.2f, 3.3f + (elite ? 0.3f : 0f), ang), decor: Decor.Torso);
            // the crust down its back: low bumps, or the elite's tall spines
            var nrm = V(-Mathf.Sin(TexelRig.Rad(ang)), Mathf.Cos(TexelRig.Rad(ang)));
            for (int j = 0; j < 7; j++)
            {
                var q = Vector2.Lerp(V(hip.x - 2.5f, hip.y), fs, j / 6f);
                var b = V(q.x + nrm.x * 3.4f, q.y + 0.6f + nrm.y * 3.4f);
                if (elite) Add(2.1f, Slot.Body, TexelRig.Taper(b, V(b.x - 0.8f + nrm.x * 2.4f, b.y + nrm.y * 2.4f), 0.9f, 0.2f), decor: Decor.Spine);
                else Add(2.1f, Slot.Body, TexelRig.Ellipse(b.x, b.y - 0.35f, 1.2f, 0.7f), decor: Decor.Crust);
            }
            Leg(3f, false, V(fs.x - 0.6f, fs.y - 0.6f), V(fs.x + 1.0f + g1.x, Fy(g1.y)));          // near front
            Leg(3.1f, false, V(bs.x - 0.4f, bs.y - 0.4f), V(bs.x + 0.4f + g2.x, 0.6f + g2.y));      // near back

            // the head: flat, wide and low, eyes bulging on top
            var n = R(V(26, 5.0f + oy));
            float ha = p.Rear * 30f + p.HeadRot, ca = Mathf.Cos(TexelRig.Rad(ha)), sa = Mathf.Sin(TexelRig.Rad(ha));
            Vector2 Hd(float lx, float ly) => new(n.x + 3 + lx * ca - ly * sa, n.y + lx * sa + ly * ca);
            // the gill fronds, swept back from the sides of the head like an axolotl's
            int nG = elite ? 4 : 3;
            for (int i = 0; i < nG; i++)
            {
                var r0 = Hd(-3.0f, -0.3f + i * 0.8f);
                var r1 = Hd(-6.6f - i * 0.3f - p.Gill * 1.4f, 0.9f + i * 1.3f + p.Gill * 1.4f);
                Add(3.5f + i * 0.01f, Slot.Gill, TexelRig.Taper(r0, r1, 0.9f, 0.4f));
                for (int j = 1; j <= 3; j++)
                {
                    var q = Vector2.Lerp(r0, r1, j / 4f);
                    Add(3.51f + i * 0.01f, Slot.Gill, TexelRig.Taper(q, V(q.x - 0.4f, q.y + 1.0f), 0.42f, 0.14f), extra: 1);
                }
            }
            var sc = Hd(0.4f, -1.5f - p.Sac * 0.8f);
            Add(3.8f, Slot.Sac, TexelRig.Ellipse(sc.x, sc.y, 2.4f + p.Sac * 1.5f, 1.0f + p.Sac * 1.3f, ha));
            var jw = Hd(-1.2f, -1.2f);
            var je = Rot(Hd(5.0f, -1.3f), jw, -p.Jaw);
            if (p.Jaw > 6f) Add(3.9f, Slot.Maw, TexelRig.Poly(new[] { Hd(-0.6f, -0.6f), Hd(5.2f, -0.7f), je, jw }, jw.x, jw.y, 3, 3));
            Add(3.95f, Slot.Body, TexelRig.Taper(jw, je, 1.1f, 1.0f), decor: Decor.Jaw);
            var cr = Hd(0, 0.2f);
            Add(4f, Slot.Body, TexelRig.Ellipse(cr.x, cr.y, 5.0f, 2.7f, ha), decor: Decor.Cranium);
            Add(4.1f, Slot.Body, TexelRig.Taper(Hd(1.5f, -0.1f), Hd(4.9f, -0.4f), 2.4f, 2.0f), decor: Decor.Snout);
            Add(4.15f, Slot.Fang, TexelRig.Taper(Hd(4.4f, -1.1f), Hd(4.5f, -1.7f), 0.35f, 0.12f));
            var so = Hd(1.2f, 1.9f);
            Add(4.2f, Slot.Socket, TexelRig.Ellipse(so.x, so.y, 1.2f, 0.9f, ha));
            var ey = Hd(1.4f, 2.0f);
            Add(4.25f, Slot.Eye, TexelRig.Ellipse(ey.x, ey.y, 0.8f, 0.55f, ha), level: p.Eye);
            // the glob leaving its jaws
            if (p.Glob) { var gc = Hd(6.4f, 0.4f); Add(4.5f, Slot.Glob, TexelRig.Ellipse(gc.x, gc.y, 1.8f, 1.8f)); }

            if (p.Flare > 0) fx?.Add(new Fx { Kind = FxKind.Flare, C = Hd(5.6f, -0.8f), Dir = ha - p.Jaw * 0.35f, N = p.Flare });
            if (p.Chips) fx?.Add(new Fx { Kind = FxKind.Chips, C = Hd(-1, 2) });
            // THE PLACE'S ACCENT along the spine (fungus or salt), or streamers off the shoulders
            {
                var dirB = (fs - hip).normalized;
                CreatureAccents.Shelves(r_, 2.15f, mid + nrm * 3.3f - dirB * 2.2f, mid + nrm * 3.2f + dirB * 1.6f, 1.7f);
                if (CreatureAccents.Sky) CreatureAccents.Ribbons(r_, 2.12f, mid + nrm * 3f + dirB * 2.5f, -1f, 2, 1.5f, 0.85f);
            }
            return Hd(5.6f, -0.8f);
        }

        // ---------------------------------------------------------------- raster

        static Sprite Render(int frame, Mat mat, bool elite, Art.Biomes.BiomeKind biome)
        {
            CreatureAccents.Biome = biome; CreatureAccents.Frame = frame;
            try { return RenderIn(frame, mat, elite, biome); }
            finally { CreatureAccents.Biome = Art.Biomes.BiomeKind.Tower; }
        }

        static Sprite RenderIn(int frame, Mat mat, bool elite, Art.Biomes.BiomeKind biome)
        {
            var p = Poses[frame];
            float scale = (elite ? EliteScale : 1f) * RigScale;
            int w = Mathf.RoundToInt(W * scale / 2f) * 2, h = Mathf.RoundToInt(H * scale);
            var fx = new List<Fx>();
            Build(p, mat.Iron, elite, out var rig, fx);

            string name = $"enemy.mortar.{(mat.Iron ? "iron" : "basalt")}{(elite ? ".elite" : "")}.{(MortarFrame)frame}";

            if (biome != Art.Biomes.BiomeKind.Tower) name += "." + biome;
            return TexelRig.Rasterise(name, rig, w, h, scale, mat.Out,
                (in TexelRig.Cell c, int x, int y, int s) => ColourOf(c, x, y, s, mat, p),
                new Vector2(PivotX, PivotY), (px, cw, ch, sc, filled) => Light(px, cw, ch, sc, filled, fx, p, mat));
        }

        /// <summary>The first of the fire at its jaws, and a flinch's chips of crust.</summary>
        static void Light(Color[] px, int w, int h, float scale, System.Func<int, int, bool> filled, List<Fx> fx, Pose p, Mat mat)
        {
            bool Empty(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && !filled(x, y);
            foreach (var f in fx)
            {
                if (f.Kind == FxKind.Flare)
                    for (int k = 1; k < 6; k++)
                    for (int j = -1; j <= 1; j++)
                    {
                        float d = TexelRig.Rad(f.Dir + j * 14);
                        int x = Mathf.FloorToInt((f.C.x + Mathf.Cos(d) * k) * scale), y = Mathf.FloorToInt((f.C.y + Mathf.Sin(d) * k) * scale);
                        if (Empty(x, y) && TexelRig.Hash(k + f.N * 7, j + 3) < 0.8f) TexelRig.Put(px, w, x, y, k < 2 ? Accent[3] : k < 4 ? Accent[2] : Accent[1], 1f);
                    }
                else
                    for (int i = 0; i < 5; i++)
                    {
                        int x = Mathf.FloorToInt((f.C.x - 3f + TexelRig.Hash(i, p.Seed) * 8f) * scale);
                        int y = Mathf.FloorToInt((f.C.y + 1f + TexelRig.Hash(p.Seed, i) * 5f) * scale);
                        if (Empty(x, y)) TexelRig.Put(px, w, x, y, mat.Hide[2], 1f);
                    }
            }
        }

        static float SegDist(Vector2[] pts, float x, float y)
        {
            float best = 9f;
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                var a = pts[i];
                var d = pts[i + 1] - a;
                float t = Mathf.Clamp01(((x - a.x) * d.x + (y - a.y) * d.y) / d.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(V(x, y), a + d * t));
            }
            return best;
        }

        static Color? ColourOf(in TexelRig.Cell c, int x, int y, int s, Mat mat, Pose p)
        {
            var part = c.Part;
            var hit = c.Hit;
            bool iron = mat.Iron;
            int g = p.Heat;
            float rr = Mathf.Sqrt(hit.Nx * hit.Nx + hit.Ny * hit.Ny);
            var decor = (Decor)part.Decor;
            switch ((Slot)part.Slot)
            {
                case Slot.Eye: return Accent[Mathf.Clamp(part.Level, 0, 3)];
                case Slot.Socket: return Socket;
                case Slot.Fang: return mat.Fang;
                case Slot.Maw: return g >= 2 ? (rr < 0.6f ? Accent[3] : Accent[2]) : mat.Maw;
                case Slot.Glob: return rr < 0.45f ? Accent[3] : rr < 0.75f ? Accent[2] : TexelRig.Hash(x, y) < 0.5f ? mat.Hide[1] : Accent[1];
                case Slot.Sac: return p.Sac > 0f ? (rr < 0.5f ? Accent[p.Sac >= 1f ? 3 : 2] : rr < 0.8f ? Accent[2] : Accent[1]) : mat.Belly[1];
                case Slot.Gill:
                    return part.Extra > 0f || hit.T > 0.5f ? (g > 0 ? Accent[Mathf.Min(3, g + 1)] : mat.Hide[2]) : mat.Hide[Mathf.Max(1, s)];
            }

            var ramp = mat.Hide;
            if (decor is Decor.Spine or Decor.Crust) return ramp[Mathf.Min(3, s + (iron ? 0 : 1))];
            var veins = decor == Decor.Torso ? TorsoVeins : decor == Decor.Cranium ? CraniumVeins : null;
            if (veins != null)
            {
                float d = 9f;
                foreach (var l in veins) d = Mathf.Min(d, SegDist(l, hit.Nx, hit.Ny));
                if (d < (iron ? 0.09f : 0.08f)) return g == 0 ? (iron ? Accent[0] : mat.Crack) : Accent[Mathf.Min(2, g)];
            }
            if (decor is Decor.Torso or Decor.Jaw && hit.Ny < -0.45f) return mat.Belly[s >= 2 ? 1 : 0];
            if (!iron) { if (TexelRig.Hash(x, y) < 0.1f) s = Mathf.Max(0, s - 1); }
            else if (decor == Decor.Torso && Mathf.Abs(hit.Ny - 0.15f) < 0.09f && Mathf.FloorToInt(c.Px) % 3 == 0) return mat.Rivet;
            return ramp[s];
        }
    }
}
