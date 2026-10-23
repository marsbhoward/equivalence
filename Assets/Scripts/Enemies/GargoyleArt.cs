using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>The Gargoyle's frames, in the order <see cref="GargoyleArt.For"/> returns them.</summary>
    public enum GargoyleFrame { Idle0, Idle1, Idle2, Idle3, Gaze0, Gaze1, Cast, Rest0, Rest1, Jolt, Fly0, Fly1, Slam }

    /// <summary>
    /// The Gargoyle as the WATCHER (the user's sign-off, 2026-10-18, from the mock-up at
    /// https://claude.ai/artifact/EgBxtPfduoFna2izGMse7s): a horned, bat-winged beast of stone
    /// crouched on a broken plinth, wings folded round it like a cloak, claws hooked over the edge.
    ///
    /// THE BODY SAYS THE ATTACK - and only the body (the user's pick of two: no line of sight is
    /// drawn). It never moves and never strikes; it LOOKS. Through the gaze the head lifts toward
    /// you and the eyes kindle (GAZE0); for the last 30% the jaws part, the eyes blaze and the
    /// cracks across its stone fill with violet light (GAZE1, the tell); CAST is the root landing.
    /// It RESTS head down with the light gone out of it, and a flinch JOLTS it, chips flying.
    ///
    /// THE ELITE HAS NO PLINTH (the user's call): it flies and lands somewhere new each time, so
    /// it squats on the bare floor - every perched frame is drawn with its claws on the ground
    /// instead of over the plinth's edge. FLY0/1 beat the wings with the talons dangling; SLAM
    /// flares them up and drives both fists into the floor. Bigger, longer horns, a spined tail.
    ///
    /// The violet is the root's own colour (StatusVisuals' petrify tint). ONE RIG, TWO MATERIALS:
    /// Construct is WEATHERED (grey-violet stone, moss, cracks that light), Automaton is
    /// IRONBOUND (cast iron, the cracks become piping, rivets down the plinth). Drawn 1.15x on the
    /// texel grid, the Chaser's way - the biggest body in the roster. Ported from the mock-up's
    /// JavaScript line for line - change both or neither.
    /// </summary>
    public static class GargoyleArt
    {
        const float RigScale = 1.15f, EliteScale = 1.2f;
        const int W = 74, H = 66;
        const float PivotX = 27f, PivotY = 14f;

        enum Slot { Body, Plinth, Wing, Fang, Maw, Socket, Eye }
        enum Decor { None, Block, Cap, Tail, Spade, Spine, Membrane, Bone, Arm, Thigh, Torso, Neck, Horn, Ear, Jaw, Cranium, Snout, Brow }
        enum Fx { Halo, Chips, Flare }

        sealed class Mat
        {
            public Color[] Body, Plinth, Moss;
            public Color Maw, Fang, Rivet, Out;
            public bool Iron;
        }

        static readonly Mat Weathered = new()
        {
            Body = TexelRig.Hex("#25222b", "#3d3845", "#5a5463", "#7e7787"),
            Plinth = TexelRig.Hex("#2a2724", "#423e38", "#5f5a51", "#827b6f"),
            Moss = TexelRig.Hex("#3c4a30", "#56683f"),
            Maw = TexelRig.Hex("#120c13")[0],
            Fang = TexelRig.Hex("#cbc3ae")[0],
            Out = TexelRig.Hex("#0b0a0d")[0],
        };

        static readonly Mat Ironbound = new()
        {
            Body = TexelRig.Hex("#1f2126", "#363a42", "#565c67", "#878e99"),
            Plinth = TexelRig.Hex("#1c1e22", "#30333a", "#4c5059", "#737985"),
            Maw = TexelRig.Hex("#100f12")[0],
            Fang = TexelRig.Hex("#a3abb7")[0],
            Rivet = TexelRig.Hex("#b6bdc7")[0],
            Out = TexelRig.Hex("#0a0a0c")[0],
            Iron = true,
        };

        static readonly Color Socket = TexelRig.Hex("#0e0b12")[0];

        /// <summary>The Gargoyle's stone-violet, dim to hot - the root's own colour.</summary>
        public static readonly Color[] Accent = TexelRig.Hex("#2a1d4a", "#5a3fa0", "#9e73f2", "#e2d4ff");

        /// <summary>Colours the biome weathering never touches - the glows and tells (CreatureWeathering).</summary>
        internal static Color[] Reserved => _reserved ??= Accent;
        static Color[] _reserved;

        // cracks in each part's own frame, so they ride the body
        static readonly Vector2[][] TorsoCracks =
        {
            new[] { V(-0.3f, 0.85f), V(0.05f, 0.45f), V(-0.1f, 0.05f), V(0.3f, -0.35f), V(0.15f, -0.85f) },
            new[] { V(0.05f, 0.45f), V(0.5f, 0.55f) },
        };
        static readonly Vector2[][] ThighCracks = { new[] { V(0.65f, 0.45f), V(0.2f, 0.1f), V(0.35f, -0.55f) } };
        static readonly Vector2[][] CraniumCracks = { new[] { V(-0.7f, 0.6f), V(-0.25f, 0.3f), V(-0.4f, -0.15f) } };
        static readonly Vector2[][] WingCracks = { new[] { V(-0.2f, 0.6f), V(0.0f, 0.2f), V(-0.25f, -0.2f), V(-0.1f, -0.6f) } };
        static readonly Vector2[] PlinthCrackA = { V(18, 9), V(20, 6), V(18.5f, 3), V(21, 0) };
        static readonly Vector2[] PlinthCrackB = { V(33, 9), V(31.5f, 5.5f), V(34, 2) };

        static Vector2 V(float x, float y) => new(x, y);

        // ---------------------------------------------------------------- poses

        sealed class Pose
        {
            public bool Plinth = true, Fly, Halo, Chips, Flare;
            public float Bob, Lean, HeadRot, Jaw, Wing, Flap;
            public int Eye = 1, Crack, Seed = 1;
            public Vector2 Head, Hand = V(37.5f, 1.2f), Foot = V(28.5f, 0.9f);
        }

        // bob: the body's rise; lean: the torso's tilt; head/headRot/jaw: the gaze; eye/crack:
        // 0..3 light; wing: 0 folded .. 1 spread; flap: the wings' turn about the shoulder;
        // hand/foot: where the claws grip. Indexed by GargoyleFrame.
        static readonly Pose[] Poses =
        {
            new(),
            new() { Bob = 0.3f, Wing = 0.03f, Flap = -2 },
            new() { Bob = 0.5f, Wing = 0.05f, Flap = -3, HeadRot = 2 },
            new() { Bob = 0.2f, Wing = 0.02f, Flap = -1 },
            new() { Head = V(0.6f, 0.8f), HeadRot = 10, Eye = 2, Crack = 1, Lean = 4, Wing = 0.06f },
            new() { Head = V(1.2f, 1.2f), HeadRot = 16, Jaw = 22, Eye = 3, Crack = 3, Lean = 7, Wing = 0.12f, Flap = -4, Halo = true },
            new() { Head = V(1.4f, 1.3f), HeadRot = 18, Jaw = 30, Eye = 3, Crack = 3, Lean = 8, Wing = 0.18f, Flap = -6, Flare = true },
            new() { Head = V(-0.4f, -1.4f), HeadRot = -14, Eye = 0, Crack = 0, Lean = -3, Bob = -0.3f },
            new() { Head = V(-0.5f, -1.6f), HeadRot = -16, Eye = 0, Crack = 0, Lean = -3, Bob = -0.4f, Wing = 0.02f },
            new() { Head = V(-1.5f, 0.6f), HeadRot = 28, Jaw = 14, Eye = 0, Crack = 0, Lean = -8, Bob = 0.4f, Chips = true, Seed = 2 },
            new() { Plinth = false, Fly = true, Wing = 1, Flap = 14, Lean = -40, Head = V(1.8f, -2.5f), HeadRot = -6, Hand = V(37, 2), Foot = V(25, -3.5f), Eye = 2, Crack = 1 },
            new() { Plinth = false, Fly = true, Wing = 1, Flap = -32, Lean = -40, Head = V(1.8f, -2.5f), HeadRot = -6, Hand = V(37, 2.5f), Foot = V(25, -3), Eye = 2, Crack = 1, Bob = 2 },
            new() { Plinth = false, Wing = 0.85f, Flap = 28, Lean = 18, Head = V(1.6f, -2.6f), HeadRot = -20, Jaw = 34, Eye = 3, Crack = 3, Hand = V(38, 0.6f), Foot = V(27, 0.3f), Bob = -1.2f, Flare = true },
        };

        /// <summary>An elite's version of a perched pose: no plinth, the claws on the floor.</summary>
        static Pose Grounded(Pose p) => !p.Plinth ? p : new Pose
        {
            Plinth = false, Fly = p.Fly, Halo = p.Halo, Chips = p.Chips, Flare = p.Flare, Bob = p.Bob, Lean = p.Lean,
            HeadRot = p.HeadRot, Jaw = p.Jaw, Wing = p.Wing, Flap = p.Flap, Eye = p.Eye, Crack = p.Crack, Seed = p.Seed,
            Head = p.Head, Hand = V(p.Hand.x - 1f, 0.6f), Foot = V(p.Foot.x, 0.3f),
        };

        // ---------------------------------------------------------------- cache

        static readonly Dictionary<int, Sprite[]> _cache = new();

        /// <summary>Every frame of one look and tier, indexed by <see cref="GargoyleFrame"/>.</summary>
        public static Sprite[] For(EnemyLook look, bool elite) => For(look, elite, Art.Biomes.BiomeKind.Tower);

        /// <summary>The same, wearing the place's accent (CreatureAccents) - weathered after by
        /// CreatureWeathering.</summary>
        public static Sprite[] For(EnemyLook look, bool elite, Art.Biomes.BiomeKind biome)
        {
            int key = (look == EnemyLook.Automaton ? 1 : 0) | (elite ? 2 : 0) | ((int)biome << 2);
            if (_cache.TryGetValue(key, out var hit) && hit != null && hit.Length > 0 && hit[0] != null) return hit;

            var mat = look == EnemyLook.Automaton ? Ironbound : Weathered;
            var frames = new Sprite[Poses.Length];
            for (int i = 0; i < frames.Length; i++) frames[i] = Render(i, mat, elite, biome);
            _cache[key] = frames;
            return frames;
        }

        // ---------------------------------------------------------------- the rig (facing +x)

        static Vector2 Rot(Vector2 p, Vector2 o, float deg)
        {
            float c = Mathf.Cos(TexelRig.Rad(deg)), s = Mathf.Sin(TexelRig.Rad(deg)), dx = p.x - o.x, dy = p.y - o.y;
            return new Vector2(o.x + dx * c - dy * s, o.y + dx * s + dy * c);
        }

        static TexelRig.Rig Build(Pose p, bool elite, List<(Fx kind, Vector2 c)> fx)
        {
            var rig = new TexelRig.Rig();
            TexelRig.Part Add(float order, Slot slot, TexelRig.Shape test, bool far = false, bool sep = false, Decor decor = Decor.None, float level = 0)
                => rig.Add(order, (int)slot, test, far, sep, (int)decor, level);

            // the plinth (the basic only): a broken block and a cracked cap
            float oy = p.Fly ? 14f + p.Bob : (p.Plinth ? 11f : 0.6f) + p.Bob;
            if (p.Plinth)
            {
                Add(0.2f, Slot.Plinth, TexelRig.Poly(new[] { V(13, 0), V(39, 0), V(38, 9), V(14, 9) }, 26, 4.5f, 13, 5), decor: Decor.Block);
                Add(0.3f, Slot.Plinth, TexelRig.Poly(new[] { V(11, 9), V(41, 9), V(40.5f, 11.4f), V(30, 11.6f), V(28, 10.9f), V(26.5f, 11.6f), V(11.5f, 11.4f) }, 26, 10, 15, 2), decor: Decor.Cap);
            }
            Vector2 X(float x, float y) => new(x, y + oy);

            // the tail: hanging down the plinth's back, or curled on the floor
            var tail = p.Fly ? new[] { X(19, 4), X(13, 2), X(8, 0), X(5, 2) }
                     : p.Plinth ? new[] { X(19, 3), X(14, 1), X(11, -4), X(12, -8) }
                     : new[] { X(19, 3), X(13, 1.2f), X(8, 0.6f), X(5, 1.8f) };
            for (int i = 0; i + 1 < tail.Length; i++)
                Add(0.5f + i * 0.01f, Slot.Body, TexelRig.Taper(tail[i], tail[i + 1], 1.7f - i * 0.4f, 1.3f - i * 0.4f), decor: Decor.Tail);
            var tip = tail[^1];
            var prev = tail[^2];
            float dir = Mathf.Atan2(tip.y - prev.y, tip.x - prev.x), dirDeg = dir * Mathf.Rad2Deg;
            Add(0.55f, Slot.Body, TexelRig.Poly(new[]
            {
                tip, Rot(V(tip.x + 2.6f, tip.y + 1.2f), tip, dirDeg),
                V(tip.x + Mathf.Cos(dir) * 3.2f, tip.y + Mathf.Sin(dir) * 3.2f), Rot(V(tip.x + 2.6f, tip.y - 1.2f), tip, dirDeg),
            }, tip.x, tip.y, 2, 2), decor: Decor.Spade);
            if (elite)
                for (int i = 0; i < 3; i++)
                {
                    var q = Vector2.Lerp(tail[0], tail[2], i / 2.5f + 0.1f);
                    Add(0.56f, Slot.Body, TexelRig.Taper(q, V(q.x - 1f, q.y + 1.8f), 0.6f, 0.15f), decor: Decor.Spine);
                }

            // the wings: the leading edge from the shoulder to the wrist, three fingers, the membrane between
            var S = X(27, 17);
            Vector2[] fold = { V(22.5f, 36), V(12.5f, 28), V(10.5f, 16), V(16, 6.5f) };
            Vector2[] spread = { V(34, 37), V(9, 38), V(3, 27), V(11, 16) };
            void Wing(bool far)
            {
                var off = far ? V(2.2f, 1.6f) : Vector2.zero;
                var so = S + off;
                var pts = new Vector2[4];
                for (int i = 0; i < 4; i++)
                    pts[i] = Rot(Vector2.Lerp(X(fold[i].x, fold[i].y), X(spread[i].x, spread[i].y), p.Wing) + off, so, p.Flap * (far ? 0.8f : 1f));
                Vector2 wr = pts[0], f1 = pts[1], f2 = pts[2], f3 = pts[3];
                float o = far ? 0.65f : 1.45f;
                // scalloped trailing edge: the membrane dips between the finger tips
                Vector2 Dip(Vector2 a, Vector2 b) => Vector2.Lerp(Vector2.Lerp(a, b, 0.5f), wr, 0.18f);
                var mc = Vector2.Lerp(so, f2, 0.5f);
                Add(o, Slot.Wing, TexelRig.Poly(new[] { so, wr, f1, Dip(f1, f2), f2, Dip(f2, f3), f3 }, mc.x, mc.y, 8, 8), far, decor: Decor.Membrane);
                Add(o + 0.01f, Slot.Body, TexelRig.Taper(so, wr, 1.6f, 1.1f), far, !far, Decor.Bone);
                foreach (var f in new[] { f1, f2, f3 }) Add(o + 0.02f, Slot.Body, TexelRig.Taper(wr, f, 0.65f, 0.35f), far, decor: Decor.Bone);
                Add(o + 0.03f, Slot.Body, TexelRig.Taper(wr, V(wr.x + 0.8f, wr.y + 2.2f), 0.7f, 0.2f), far, decor: Decor.Bone); // the thumb claw
            }
            Wing(true);

            // the far arm and leg, behind the body
            var hand = X(p.Hand.x, p.Hand.y);
            var handF = X(p.Hand.x - 2.2f, p.Hand.y + (p.Fly ? 1f : 0f));
            void Arm(float order, bool far, Vector2 sh, Vector2 h)
            {
                var elbow = V((sh.x + h.x) / 2f + 1.6f, (sh.y + h.y) / 2f + 0.2f);
                Add(order, Slot.Body, TexelRig.Taper(sh, elbow, 2.7f, 1.9f), far, !far, Decor.Arm);
                Add(order + 0.01f, Slot.Body, TexelRig.Taper(elbow, h, 1.9f, 1.5f), far, !far, Decor.Arm);
                Add(order + 0.02f, Slot.Body, TexelRig.Ellipse(h.x + 0.6f, h.y + 0.2f, 2.1f, 1.3f), far);
                for (int i = 0; i < 3; i++)
                    Add(order + 0.03f, Slot.Fang, TexelRig.Taper(V(h.x + 1.6f, h.y - 0.2f + i * 0.5f),
                        V(h.x + 2.6f + (p.Fly ? 0.6f : 0f), h.y - (p.Plinth ? 2.2f : 0.9f) + i * 0.4f), 0.45f, 0.15f), far);
            }
            Arm(0.8f, true, X(29.5f, 16.5f), handF);
            // the haunches and the hind foot gripping the edge
            var th = X(20.5f, 5.2f + (p.Fly ? 1f : 0f));
            Add(2f, Slot.Body, TexelRig.Ellipse(th.x, th.y, 6.4f, 5.4f, p.Fly ? -30f : -8f), decor: Decor.Thigh);
            var foot = X(p.Foot.x, p.Foot.y);
            Add(2.1f, Slot.Body, TexelRig.Taper(X(23.5f, 2.5f), foot, 1.9f, 1.2f), sep: true, decor: Decor.Arm);
            for (int i = 0; i < 3; i++)
                Add(2.15f, Slot.Fang, TexelRig.Taper(V(foot.x + 0.5f, foot.y - 0.3f + i * 0.4f), V(foot.x + 1.9f, foot.y - 1.2f + i * 0.3f), 0.4f, 0.15f));
            // the torso, hunched forward
            var to = X(26, 11.5f);
            Add(3f, Slot.Body, TexelRig.Ellipse(to.x, to.y, 7.3f, 8.9f, -34f + p.Lean), decor: Decor.Torso);
            Wing(false);
            // the near arm over everything but the head
            Arm(5f, false, X(30, 16), hand);
            // the head, posed about the neck's top
            var n = X(33.5f + p.Head.x, 17.5f + p.Head.y);
            Add(5.5f, Slot.Body, TexelRig.Taper(X(29.5f, 15.5f), n, 3.1f, 2.5f), decor: Decor.Neck);
            float a = TexelRig.Rad(p.HeadRot), ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            Vector2 Hd(float lx, float ly) => new(n.x + 2.5f + lx * ca - ly * sa, n.y + 1.2f + lx * sa + ly * ca);
            float hornL = elite ? 1.4f : 1f;
            Add(5.8f, Slot.Body, TexelRig.Taper(Hd(-0.4f, 2.6f), Hd(-2.2f * hornL, 6.2f * hornL), 1.6f, 1.0f), decor: Decor.Horn);
            Add(5.81f, Slot.Body, TexelRig.Taper(Hd(-2.2f * hornL, 6.2f * hornL), Hd(-5.4f * hornL, 7.4f * hornL), 1.0f, 0.3f), decor: Decor.Horn);
            Add(5.85f, Slot.Body, TexelRig.Taper(Hd(-1.8f, 1.2f), Hd(-5.6f, 2.4f), 1.2f, 0.25f), decor: Decor.Ear);
            var j = Hd(0.6f, -2.0f);
            var je = Rot(Hd(5.6f, -2.9f), j, -p.Jaw);
            if (p.Jaw > 4f) Add(5.9f, Slot.Maw, TexelRig.Poly(new[] { Hd(0.9f, -1.0f), Hd(6.2f, -1.6f), je, j }, j.x, j.y, 3, 3));
            Add(5.95f, Slot.Body, TexelRig.Taper(j, je, 1.5f, 0.95f), decor: Decor.Jaw);
            var cr = Hd(0, 0.4f);
            Add(6f, Slot.Body, TexelRig.Ellipse(cr.x, cr.y, 4.3f, 3.7f, p.HeadRot), decor: Decor.Cranium);
            Add(6.1f, Slot.Body, TexelRig.Taper(Hd(1.8f, -0.6f), Hd(6.4f, -1.4f), 2.4f, 1.5f), decor: Decor.Snout);
            Add(6.15f, Slot.Fang, TexelRig.Taper(Hd(5.0f, -1.9f), Hd(5.3f, -3.6f - p.Jaw * 0.03f), 0.6f, 0.2f));
            Add(6.16f, Slot.Fang, TexelRig.Taper(Hd(2.6f, -2.3f), Hd(2.4f, -0.8f), 0.5f, 0.2f));
            Add(6.2f, Slot.Body, TexelRig.Taper(Hd(-0.8f, 2.2f), Hd(4.0f, 1.4f), 1.2f, 0.8f), decor: Decor.Brow);
            var so2 = Hd(2.2f, 0.5f);
            Add(6.3f, Slot.Socket, TexelRig.Ellipse(so2.x, so2.y, 1.5f, 1.0f, p.HeadRot));
            var eye = Hd(2.5f, 0.5f);
            Add(6.4f, Slot.Eye, TexelRig.Ellipse(eye.x, eye.y, 1.05f, 0.7f, p.HeadRot), level: p.Eye);

            if (p.Halo) fx.Add((Fx.Halo, eye));
            if (p.Chips) fx.Add((Fx.Chips, Hd(0, 2)));
            if (p.Flare) fx.Add((Fx.Flare, eye));
            // THE PLACE'S ACCENT on the plinth (the elite has none - it wears the weathering alone)
            if (p.Plinth && !p.Fly)
            {
                if (CreatureAccents.Forest)
                    CreatureAccents.Strands(rig, 0.35f, CreatureAccents.Kind.Moss,
                        new[] { new Vector2(15.5f, 9.4f), new Vector2(19.5f, 9.2f), new Vector2(32.5f, 9.2f), new Vector2(36.8f, 9.4f) }, 5f, 0.95f);
                if (CreatureAccents.Salt) CreatureAccents.Shelves(rig, 0.35f, new Vector2(14.6f, 12.1f), new Vector2(37.4f, 12f), 2.6f);
                if (CreatureAccents.Sky)
                {
                    CreatureAccents.Ivy(rig, 0.35f, new Vector2(15.4f, 0.2f), new Vector2(17.6f, 10.4f), 1.1f);
                    CreatureAccents.Ivy(rig, 0.35f, new Vector2(36.6f, 0.2f), new Vector2(34.2f, 9.8f), 1.1f);
                }
            }
            return rig;
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
            var p = elite ? Grounded(Poses[frame]) : Poses[frame];
            float scale = (elite ? EliteScale : 1f) * RigScale;
            int w = Mathf.RoundToInt(W * scale / 2f) * 2, h = Mathf.RoundToInt(H * scale);
            var fx = new List<(Fx kind, Vector2 c)>();
            var rig = Build(p, elite, fx);

            string name = $"enemy.gargoyle.{(mat.Iron ? "iron" : "weathered")}{(elite ? ".elite" : "")}.{(GargoyleFrame)frame}";

            if (biome != Art.Biomes.BiomeKind.Tower) name += "." + biome;
            return TexelRig.Rasterise(name, rig, w, h, scale, mat.Out,
                (in TexelRig.Cell c, int x, int y, int s) => ColourOf(c, x, y, s, mat, p),
                new Vector2(PivotX, PivotY), (px, cw, ch, sc, filled) => Light(px, cw, ch, sc, filled, fx, p, mat));
        }

        /// <summary>Light off it: the tell's halo round the eye, the root's flare, a flinch's chips.</summary>
        static void Light(Color[] px, int w, int h, float scale, System.Func<int, int, bool> filled,
                          List<(Fx kind, Vector2 c)> fx, Pose p, Mat mat)
        {
            bool Empty(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && !filled(x, y);
            foreach (var (kind, c) in fx)
                switch (kind)
                {
                    case Fx.Halo:
                        for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            if (!Empty(x, y)) continue;
                            float d = Vector2.Distance(V((x + 0.5f) / scale, (y + 0.5f) / scale), c);
                            if (d <= 5.5f && (x + y) % 2 == 0) TexelRig.Put(px, w, x, y, Accent[2], (d < 5.5f * 0.6f ? 150f : 90f) / 255f);
                        }
                        break;
                    case Fx.Flare:
                        for (int k = 2; k < 9; k++)
                        foreach (var (dx, dy) in new[] { (1f, 0f), (0.7f, 0.7f), (0.7f, -0.7f) })
                        {
                            int x = Mathf.FloorToInt((c.x + dx * k) * scale), y = Mathf.FloorToInt((c.y + dy * k) * scale);
                            if (Empty(x, y)) TexelRig.Put(px, w, x, y, k < 5 ? Accent[3] : Accent[2], (230f - k * 18f) / 255f);
                        }
                        break;
                    case Fx.Chips:
                        for (int i = 0; i < 5; i++)
                        {
                            int x = Mathf.FloorToInt((c.x - 3f + TexelRig.Hash(i, p.Seed) * 9f) * scale);
                            int y = Mathf.FloorToInt((c.y + 1f + TexelRig.Hash(p.Seed, i) * 6f) * scale);
                            foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
                                if (Empty(x + dx, y + dy) && (dx + dy < 2 || i % 2 == 1)) TexelRig.Put(px, w, x + dx, y + dy, mat.Body[2], 1f);
                        }
                        break;
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

        static Vector2[][] CracksOf(Slot slot, Decor decor) =>
            slot == Slot.Wing ? WingCracks : decor switch
            {
                Decor.Torso => TorsoCracks,
                Decor.Thigh => ThighCracks,
                Decor.Cranium => CraniumCracks,
                _ => null,
            };

        static Color? ColourOf(in TexelRig.Cell c, int x, int y, int s, Mat mat, Pose p)
        {
            var part = c.Part;
            var hit = c.Hit;
            bool iron = mat.Iron;
            int g = p.Crack;
            var slot = (Slot)part.Slot;
            var decor = (Decor)part.Decor;
            switch (slot)
            {
                case Slot.Eye: return Accent[Mathf.Clamp(part.Level, 0, 3)];
                case Slot.Socket: return Socket;
                case Slot.Maw: return TexelRig.Hash(x, y) < 0.3f && p.Eye >= 2 ? Accent[1] : mat.Maw;
                case Slot.Fang: return mat.Fang;
                case Slot.Plinth:
                {
                    var pr = mat.Plinth;
                    if (iron && decor == Decor.Block && Mathf.Abs(hit.Nx) > 0.8f && Mathf.FloorToInt(c.Py) % 3 == 1) return mat.Rivet;
                    if (iron && decor == Decor.Block && Mathf.Abs(c.Py - 5f) < 0.5f) return g == 0 ? Accent[0] : Accent[Mathf.Min(3, g)];
                    if (!iron && decor == Decor.Block && (SegDist(PlinthCrackA, c.Px, c.Py) < 0.5f || SegDist(PlinthCrackB, c.Px, c.Py) < 0.5f)) return pr[0];
                    if (!iron && TexelRig.Hash(x, y) < 0.14f) return pr[Mathf.Max(0, s - 1)];
                    return pr[decor == Decor.Cap ? Mathf.Min(3, s + 1) : s];
                }
            }

            var ramp = mat.Body;
            // the cracks (Ironbound: the piping), each in its part's own frame (a wing's poly
            // reports its offset from the membrane's middle over 8, which is the mock-up's frame)
            var cracks = CracksOf(slot, decor);
            if (cracks != null && !part.Far)
            {
                float d = 9f;
                foreach (var l in cracks) d = Mathf.Min(d, SegDist(l, hit.Nx, hit.Ny));
                if (iron && d < 0.09f) return g == 0 ? Accent[0] : Accent[Mathf.Min(3, g)];
                if (!iron && d < 0.075f) return g == 0 ? ramp[0] : Accent[Mathf.Min(3, g)];
            }
            if (slot == Slot.Wing)
            {
                // the membrane: a thinner, darker stone
                int m = Mathf.Max(0, s - 1);
                if (!iron && TexelRig.Hash(Mathf.FloorToInt(x / 2f), Mathf.FloorToInt(y / 2f)) < 0.15f) m = Mathf.Max(0, m - 1);
                return ramp[m];
            }
            if (decor is Decor.Horn or Decor.Spade or Decor.Spine) return ramp[Mathf.Min(3, s + (iron ? 0 : 1))];
            if (!iron)
            {
                // moss on the upper surfaces, pitted stone elsewhere
                float nz = TexelRig.Hash(Mathf.FloorToInt(x / 2f), Mathf.FloorToInt(y / 2f));
                if (hit.Ny > 0.55f && decor is Decor.Torso or Decor.Cranium or Decor.Bone or Decor.Thigh && nz > 0.55f)
                    return mat.Moss[nz > 0.8f ? 1 : 0];
                if (TexelRig.Hash(x, y) < 0.12f) s = Mathf.Max(0, s - 1);
            }
            else if (decor == Decor.Torso && Mathf.Abs(hit.Nx + 0.55f) < 0.07f && Mathf.FloorToInt(c.Py) % 3 == 0) return mat.Rivet;
            return ramp[s];
        }
    }
}
