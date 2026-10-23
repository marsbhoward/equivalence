using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>The Bubbles' frames, in the order <see cref="BubblesArt.For"/> returns them.</summary>
    public enum BubblesFrame { Idle0, Idle1, Idle2, Idle3, Fill0, Fill1, Blow0, Blow1, Tend0, Tend1, Jolt }

    /// <summary>
    /// Bubbles as the DROWNED BELL (the user's sign-off, 2026-10-19, from the mock-up at
    /// https://claude.ai/artifact/MzGdK82zs5h5KFUj3R4ZJ5): a jellyfish the size of a man hovering a
    /// stride above the floor - a domed bell with a scalloped rim, a dark four-lobed core, two
    /// frilled arms and a curtain of tentacles to the ground.
    ///
    /// THE BODY SAYS THE CAST. It DRAWS WATER through the channel (FILL0: the bell swelling, core
    /// and canals lit), the last 30% swollen tight, the core blazing, tentacles lifting (FILL1, the
    /// tell); BLOW0/1 contract it hard and jet it up as the bubble leaves from under the bell (the
    /// bubble then drifts to the ally as <see cref="BubbleShell"/>). It TENDS - dim, slack,
    /// drooping - while its bubble holds and through its recovery; a flinch JOLTS it, water flying.
    ///
    /// THE ELITE is 1.2x, a taller bell with a ring of barbs round the rim and eight tentacles.
    /// ONE RIG, TWO MATERIALS: Construct is DROWNED (a translucent blue-grey bell, pale canals, a
    /// violet core, blue glow); Automaton is IRONBOUND (a riveted iron diving bell with three
    /// portholes, the canals as piping, segmented iron tentacles) and glows PURPLE, its bubbles
    /// too (the user's call) - so the accent is per material (<see cref="AccentOf"/>). Ported from
    /// the mock-up's JavaScript line for line - change both or neither.
    /// </summary>
    public static class BubblesArt
    {

        /// <summary>Colours the biome weathering never touches - the glows and tells (CreatureWeathering).</summary>
        internal static Color[] Reserved => _reserved ??= TexelRig.Hex("#14324a", "#2e78a8", "#8cd9ff", "#e6f8ff", "#2a1440", "#5e2c8c", "#b77cf0", "#f0e0ff");
        static Color[] _reserved;
        const float RigScale = 1.1f, EliteScale = 1.2f;
        const int W = 40, H = 38;
        const float CX = 20f, PivotX = 20f, PivotY = 10f;

        enum Slot { Tent, Arm, Bell, Frill, Barb, Organ, Port, Bub }

        sealed class Mat
        {
            public Color[] Bell, Organ, Tent, Acc;
            public Color Frill, Barb, Rivet, Out;
            public bool Iron;
        }

        static readonly Mat Drowned = new()
        {
            Bell = TexelRig.Hex("#16222e", "#2a4458", "#4d7690", "#8cb6cc"),
            Frill = TexelRig.Hex("#b4d8e6")[0],
            Organ = TexelRig.Hex("#24183a", "#3e2a5c"),
            Tent = TexelRig.Hex("#1e3446", "#3a6078"),
            Barb = TexelRig.Hex("#d8eef6")[0],
            Out = TexelRig.Hex("#08090c")[0],
            Acc = TexelRig.Hex("#14324a", "#2e78a8", "#8cd9ff", "#e6f8ff"),
        };

        static readonly Mat Ironbound = new()
        {
            Bell = TexelRig.Hex("#1f2126", "#363a42", "#565c67", "#878e99"),
            Frill = TexelRig.Hex("#6a707b")[0],
            Tent = TexelRig.Hex("#2a2d33", "#4c5059"),
            Barb = TexelRig.Hex("#a3abb7")[0],
            Rivet = TexelRig.Hex("#b6bdc7")[0],
            Out = TexelRig.Hex("#0a0a0c")[0],
            Acc = TexelRig.Hex("#2a1440", "#5e2c8c", "#b77cf0", "#f0e0ff"),
            Iron = true,
        };

        /// <summary>The glow and the bubbles of a look, dim to hot: water-blue, or Ironbound's purple.</summary>
        public static Color[] AccentOf(EnemyLook look) => look == EnemyLook.Automaton ? Ironbound.Acc : Drowned.Acc;

        // degrees from the rim's middle, up over the dome
        static readonly float[] Canals = { 20, 45, 70, 90, 110, 135, 160 };
        static readonly float[] Pipes = { 30, 90, 150 };   // Ironbound: three pipes between the portholes

        static Vector2 V(float x, float y) => new(x, y);

        // ---------------------------------------------------------------- poses

        sealed class Pose
        {
            public float Bob, Pulse, Lean, Sway, Flare, Blow;
            public int Glow, Seed = 1;
            public bool Spray;
        }

        // bob: hover; pulse: the bell (-1 contracted .. 1 swollen); lean: tilt; glow: 0..3; sway:
        // the tentacles' drift; flare: tentacles lifted outward (negative droops); blow: the bubble
        // leaving 0..1; spray: water flung off it. Indexed by BubblesFrame.
        static readonly Pose[] Poses =
        {
            new(),
            new() { Pulse = 0.5f, Bob = 0.4f, Sway = 1 },
            new() { Pulse = 0, Bob = 0.6f, Sway = 2 },
            new() { Pulse = -0.5f, Bob = 0.2f, Sway = 3 },
            new() { Pulse = 0.8f, Bob = 0.8f, Glow = 1, Flare = 0.2f, Sway = 1.5f },
            new() { Pulse = 1.2f, Bob = 1.0f, Glow = 3, Flare = 0.5f, Sway = 2 },
            new() { Pulse = -1, Bob = 1.6f, Glow = 2, Flare = 0.8f, Blow = 0.5f, Sway = 2.5f },
            new() { Pulse = -0.6f, Bob = 2.0f, Glow = 1, Flare = 0.4f, Blow = 1, Sway = 3 },
            new() { Pulse = 0.1f, Bob = -0.5f, Flare = -0.3f, Sway = 0.5f },
            new() { Pulse = -0.1f, Bob = -0.7f, Flare = -0.3f, Sway = 1.2f },
            new() { Pulse = -0.4f, Lean = -20, Bob = 0.4f, Flare = 0.3f, Spray = true, Seed = 2, Sway = 4 },
        };

        // ---------------------------------------------------------------- cache

        static readonly Dictionary<int, Sprite[]> _cache = new();

        /// <summary>Every frame of one look and tier, indexed by <see cref="BubblesFrame"/>.</summary>
        public static Sprite[] For(EnemyLook look, bool elite) => For(look, elite, Art.Biomes.BiomeKind.Tower);

        /// <summary>The same, wearing the place's accent (CreatureAccents) - weathered after by
        /// CreatureWeathering.</summary>
        public static Sprite[] For(EnemyLook look, bool elite, Art.Biomes.BiomeKind biome)
        {
            int key = (look == EnemyLook.Automaton ? 1 : 0) | (elite ? 2 : 0) | ((int)biome << 2);
            if (_cache.TryGetValue(key, out var hit) && hit != null && hit.Length > 0 && hit[0] != null) return hit;

            var mat = look == EnemyLook.Automaton ? Ironbound : Drowned;
            var frames = new Sprite[Poses.Length];
            for (int i = 0; i < frames.Length; i++) frames[i] = Render(i, mat, elite, biome);
            _cache[key] = frames;
            return frames;
        }

        /// <summary>Where the bubble leaves from under the bell on its first blow frame, in world
        /// units from the body's position (the bell is symmetric, so no mirror is needed).</summary>
        public static Vector2 MouthOffset(bool elite)
        {
            float scale = (elite ? EliteScale : 1f) * RigScale;
            var m = Build(Poses[(int)BubblesFrame.Blow0], false, elite, out _, null);
            var pivot = new Vector2(Mathf.Round(PivotX * scale), Mathf.Round(PivotY * scale));
            return (m * scale - pivot) / TexelRig.TexelsPerUnit;
        }

        // ---------------------------------------------------------------- the rig (facing +x)

        static Vector2 Build(Pose p, bool iron, bool elite, out TexelRig.Rig rig, List<Vector2> spray)
        {
            var r_ = new TexelRig.Rig();
            rig = r_;
            void Add(float order, Slot slot, TexelRig.Shape test, bool far = false, float level = 0, float extra = 0)
                => r_.Add(order, (int)slot, test, far, false, 0, level, extra);

            float h0 = 15.5f + p.Bob;
            // the bell swells wide (pulse > 0) or contracts narrow and tall (pulse < 0)
            float rx = 10.5f * (p.Pulse >= 0 ? 1 + 0.1f * p.Pulse : 1 + 0.16f * p.Pulse);
            float ry = 9f * (p.Pulse >= 0 ? 1 + 0.12f * p.Pulse : 1 - 0.12f * p.Pulse) * (elite ? 1.1f : 1f);
            float a = TexelRig.Rad(p.Lean), ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            Vector2 L(float lx, float ly) => new(CX + lx * ca - ly * sa, h0 + lx * sa + ly * ca);   // origin mid-rim
            Vector2 ToL(float x, float y) { float dx = x - CX, dy = y - h0; return new Vector2(dx * ca + dy * sa, -dx * sa + dy * ca); }

            // the tentacles: a curtain from the rim, the odd ones behind
            int nT = elite ? 8 : 6;
            for (int i = 0; i < nT; i++)
            {
                float u = -0.85f + 1.7f * i / (nT - 1);
                bool far = i % 2 == 1;
                const int segs = 7;
                var prev = L(u * rx * 0.9f, -0.5f);
                for (int k = 1; k <= segs; k++)
                {
                    float t = k / (float)segs;
                    var q = L(u * rx * 0.9f + Mathf.Sin(k * 0.8f + p.Sway + i * 1.7f) * 1.3f * t + u * p.Flare * k * 0.9f,
                              -0.5f - k * 2.1f * (1 - 0.2f * p.Flare));
                    q.y = Mathf.Max(q.y, 0.9f);
                    Add(far ? 0.5f + i * 0.001f : 3f + i * 0.001f, Slot.Tent,
                        TexelRig.Taper(prev, q, 0.62f - (t - 1f / segs) * 0.3f, 0.62f - t * 0.3f), far, extra: k);
                    prev = q;
                }
            }
            // the two frilled arms under the bell
            foreach (int sgn in new[] { -1, 1 })
            {
                var prev = L(sgn * 1.6f, -0.6f);
                for (int k = 1; k <= 5; k++)
                {
                    float t = k / 5f;
                    var q = L(sgn * (1.6f + Mathf.Sin(k * 0.9f + p.Sway * 0.7f + sgn) * 0.8f * t), -0.6f - k * 1.6f * (1 - 0.15f * p.Flare));
                    Add(3.5f + k * 0.01f, Slot.Arm, TexelRig.Taper(prev, q, 1.5f - (t - 0.2f) * 0.7f, 1.5f - t * 0.7f), sgn < 0, extra: k);
                    prev = q;
                }
            }
            // the bell: the top of an ellipse, cut just under its middle (S/E carry its own frame, T/K its radii)
            Add(4f, Slot.Bell, (float x, float y, out TexelRig.Hit h) =>
            {
                var l = ToL(x, y);
                float nx = l.x / rx, ny = l.y / ry;
                h = new TexelRig.Hit { Nx = nx, Ny = ny, S = l.x, E = l.y, T = rx, K = ry };
                return l.y >= -0.6f && nx * nx + ny * ny <= 1f;
            });
            // the scalloped rim
            Add(4.1f, Slot.Frill, (float x, float y, out TexelRig.Hit h) =>
            {
                var l = ToL(x, y);
                h = new TexelRig.Hit { Nx = l.x / rx, Ny = -0.3f, S = l.x, E = l.y };
                if (Mathf.Abs(l.x) > rx * 0.97f) return false;
                float yb = -0.6f - 1.3f * Mathf.Abs(Mathf.Sin(l.x * 1.25f));
                return l.y >= yb && l.y <= 0.7f;
            });
            if (elite)
                for (int j = 0; j < 9; j++)
                {
                    float lx = -rx * 0.9f + rx * 1.8f * j / 8f;
                    Add(4.15f, Slot.Barb, TexelRig.Taper(L(lx, -0.3f), L(lx * 1.06f, -2.4f), 0.5f, 0.12f));
                }
            if (!iron)
            {
                var o = L(0, 3.2f);
                Add(4.2f, Slot.Organ, TexelRig.Ellipse(o.x, o.y, 4.6f, 2.6f, p.Lean));
            }
            else
                foreach (var pp in new[] { V(-5.6f, 3.4f), V(0, 5.0f), V(5.6f, 3.4f) })
                {
                    var o = L(pp.x, pp.y);
                    Add(4.3f, Slot.Port, TexelRig.Ellipse(o.x, o.y, 2.0f, 2.0f), level: p.Glow);
                }

            // the bubble leaving from under the bell, in front of the arms
            if (p.Blow > 0f)
            {
                var bc = L(0, -3.4f - p.Blow * 2.6f);
                float br = 1.4f + p.Blow * 3.2f;
                Add(5f, Slot.Bub, (float x, float y, out TexelRig.Hit h) =>
                {
                    h = new TexelRig.Hit { Nx = (x - bc.x) / br, Ny = (y - bc.y) / br };
                    return Vector2.Distance(V(x, y), bc) <= br;
                });
            }
            if (p.Spray) spray?.Add(L(0, 2));
            // THE PLACE'S ACCENT: weed trailing among the tentacles, salt crystals on the bell's
            // rim, or little clouds round it
            if (CreatureAccents.Forest)
                CreatureAccents.Strands(r_, 3.2f, CreatureAccents.Kind.Weed,
                    new[] { L(-rx * 0.55f, -0.8f), L(-rx * 0.05f, -1f), L(rx * 0.5f, -0.8f) }, 10f, 1.15f);
            if (CreatureAccents.Salt) CreatureAccents.Shelves(r_, 4.2f, L(-rx * 0.78f, 1.2f), L(rx * 0.72f, 1f), 3.3f);
            if (CreatureAccents.Sky)
                CreatureAccents.Puffs(r_, 4.2f, new[] { L(-rx - 1.4f, 1.5f), L(rx + 1.2f, -0.5f), L(rx * 0.2f, ry + 1.2f) }, 2f);
            return L(0, -3.4f);
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
            var spray = new List<Vector2>();
            Build(p, mat.Iron, elite, out var rig, spray);

            string name = $"enemy.bubbles.{(mat.Iron ? "iron" : "drowned")}{(elite ? ".elite" : "")}.{(BubblesFrame)frame}";

            if (biome != Art.Biomes.BiomeKind.Tower) name += "." + biome;
            return TexelRig.Rasterise(name, rig, w, h, scale, mat.Out,
                (in TexelRig.Cell c, int x, int y, int s) => ColourOf(c, x, y, s, mat, p),
                new Vector2(PivotX, PivotY), (px, cw, ch, sc, filled) => Light(px, cw, ch, sc, filled, spray, p, mat));
        }

        /// <summary>A flinch's water, flung off the bell.</summary>
        static void Light(Color[] px, int w, int h, float scale, System.Func<int, int, bool> filled, List<Vector2> spray, Pose p, Mat mat)
        {
            bool Empty(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && !filled(x, y);
            foreach (var c in spray)
                for (int i = 0; i < 7; i++)
                {
                    int x = Mathf.FloorToInt((c.x - 9f + TexelRig.Hash(i, p.Seed) * 18f) * scale);
                    int y = Mathf.FloorToInt((c.y + 4f + TexelRig.Hash(p.Seed, i) * 8f) * scale);
                    if (Empty(x, y)) TexelRig.Put(px, w, x, y, i % 2 == 1 ? mat.Acc[2] : mat.Acc[3], 1f);
                }
        }

        static float SegDist(Vector2 a, Vector2 b, float x, float y)
        {
            var d = b - a;
            float t = Mathf.Clamp01(((x - a.x) * d.x + (y - a.y) * d.y) / d.sqrMagnitude);
            return Vector2.Distance(V(x, y), a + d * t);
        }

        static Color A(Color c, float a255) { c.a = a255 / 255f; return c; }

        static Color? ColourOf(in TexelRig.Cell c, int x, int y, int s, Mat mat, Pose p)
        {
            var part = c.Part;
            var hit = c.Hit;
            bool iron = mat.Iron;
            int g = p.Glow, k = Mathf.RoundToInt(part.Extra);
            var acc = mat.Acc;
            float rr = Mathf.Sqrt(hit.Nx * hit.Nx + hit.Ny * hit.Ny);
            switch ((Slot)part.Slot)
            {
                case Slot.Port: return rr > 0.7f ? mat.Rivet : acc[Mathf.Clamp(part.Level, 0, 3)];
                case Slot.Bub: return rr > 0.72f ? A(hit.Nx < -0.2f && hit.Ny > 0.2f ? acc[3] : acc[2], 235) : A(acc[1], 90);
                case Slot.Barb: return mat.Barb;
                case Slot.Organ:
                {
                    bool lobe = Mathf.Abs(Mathf.Sin(Mathf.Atan2(hit.Ny, hit.Nx) * 2f)) > 0.35f;
                    if (g > 0) return rr < 0.5f ? acc[Mathf.Min(3, g)] : lobe ? acc[Mathf.Max(1, Mathf.Min(2, g))] : mat.Organ[1];
                    return A(lobe ? mat.Organ[1] : mat.Organ[0], 235);
                }
                case Slot.Bell:
                {
                    var ramp = mat.Bell;
                    float cd = 9f;
                    foreach (float d in iron ? Pipes : Canals)
                    {
                        float cr = Mathf.Cos(TexelRig.Rad(d)), sr = Mathf.Sin(TexelRig.Rad(d));
                        cd = Mathf.Min(cd, SegDist(V(cr * hit.T * 0.25f, 1.5f + sr * hit.K * 0.2f), V(cr * hit.T * 0.82f, sr * hit.K * 0.82f), hit.S, hit.E));
                    }
                    bool canal = hit.E > 1.6f && cd < (iron ? 0.45f : 0.42f);
                    if (iron)
                    {
                        if (canal) return g == 0 ? acc[0] : acc[Mathf.Min(2, g)];
                        if (hit.E > 0.9f && hit.E < 1.9f && x % 3 == 0) return mat.Rivet;
                        return ramp[s];
                    }
                    if (canal) return g == 0 ? A(ramp[Mathf.Min(3, s + 1)], 235) : A(acc[Mathf.Min(2, g)], 235);
                    return A(ramp[s], TexelRig.Hash(x, y) < 0.08f ? 200 : 220);
                }
                case Slot.Frill:
                    if (iron) return Mathf.FloorToInt(hit.S * 0.8f + 50f) % 2 == 0 ? mat.Frill : mat.Bell[1];
                    return A(TexelRig.Hash(x, y) < 0.3f ? mat.Bell[3] : mat.Frill, 230);
                case Slot.Arm:
                    if (iron) return k % 2 == 1 ? mat.Bell[Mathf.Min(3, s)] : mat.Bell[Mathf.Max(0, s - 1)];
                    return A(TexelRig.Hash(x, y) < 0.3f ? mat.Frill : mat.Bell[Mathf.Min(3, s)], 225);
            }
            // tentacles
            if (iron) return k % 2 == 1 ? mat.Tent[1] : mat.Tent[0];
            return A(s >= 2 ? mat.Tent[1] : mat.Tent[0], 225);
        }
    }
}
