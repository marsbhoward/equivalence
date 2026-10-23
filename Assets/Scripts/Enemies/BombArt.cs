using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>The Bomb's frames, in the order <see cref="BombArt.For"/> returns them.</summary>
    public enum BombFrame
    {
        Drift0, Drift1, Drift2, Drift3, Idle, Gather, Throw0, Throw1, Spent,
        Critical0, Critical1, Arming, Bursting, Banked0, Banked1,
    }

    /// <summary>
    /// The Bomb as a BRIMSTONE HEART (the user's sign-off, 2026-10-18, from the mock-up at
    /// https://claude.ai/artifact/XkSsnZEUhs3pNbbop75ArK - a bloated creature came first and was
    /// turned down for something more elemental): a hovering knot of sulphur fire with two dark
    /// slits for a face, held in a loose shell of slag plates, flames licking off the top.
    ///
    /// THE BODY SAYS THE ATTACK. It fights at arm's length: the heart SWELLS through the wind-up
    /// (GATHER) and PULSES with each fireball it throws straight out of itself (THROW) - no
    /// apparatus, the Bomb is the fire it throws - then gutters (SPENT). Under 20% health it
    /// stops fighting: the shell is shaken loose and the heart flares (CRITICAL) as it rushes in
    /// to self-destruct, forcing the shell open (ARMING) until it is wide and white-hot with a halo
    /// (BURSTING, the tell). Flinched, the shell clamps tight and the heart banks to ember red
    /// (BANKED). An elite's three orbiting MOTES are the cluster's shards.
    ///
    /// ONE RIG, TWO MATERIALS: Construct is SLAG (pitted black stone, each plate lit along its
    /// inner edge, a live fire), Automaton is IRONBOUND - an IRON FLAME: the heart and its tongues
    /// cast in steel with the fire run through them like piping (the user's call), in the same
    /// riveted cast-iron shell. Fire is drawn without an outline; the slag (and the iron flame) is.
    /// Ported from the mock-up's JavaScript line for line - change both or neither.
    /// </summary>
    public static class BombArt
    {
        /// <summary>
        /// The Bomb's BLAST colour - its warning ring, its explosion flash, and an elite's cluster
        /// shards. Yellow, so a Bomb's blast reads apart from the Mortar's red-orange shell: the
        /// colour belongs to the danger zone, not to the thing carrying it.
        /// </summary>
        public static readonly Color Blast = new(1f, 0.86f, 0.2f);

        const float EliteScale = 1.2f;
        const int W = 36, H = 34;
        const float PivotX = 17f, PivotY = 13f; // the heart - the body's centre and the shot's origin

        enum Slot { Flame, Heart, Eye, Plate }

        sealed class Mat
        {
            public Color[] Plate, Steel;
            public Color? Rivet;
            public Color Out;
            public bool Iron;
        }

        static readonly Mat Slag = new()
        {
            Plate = TexelRig.Hex("#1b1719", "#2d2628", "#453b3c", "#625555"),
            Out = TexelRig.Hex("#0b090a")[0],
        };

        static readonly Mat Ironbound = new()
        {
            Plate = TexelRig.Hex("#1f2126", "#363a42", "#565c67", "#878e99"),
            Steel = TexelRig.Hex("#3d434c", "#69717c", "#9aa3ae", "#cfd6de"),
            Rivet = TexelRig.Hex("#a8afb9")[0],
            Out = TexelRig.Hex("#0a0a0c")[0],
            Iron = true,
        };

        static readonly Color[] Sulphur = TexelRig.Hex("#4d3b0c", "#a8841e", "#ffdb33", "#fff4b0"); // the heart
        static readonly Color[] Flame = TexelRig.Hex("#6b2e0a", "#c8641a", "#ffb726", "#fff1a0");   // warmer toward the tips
        static readonly Color[] Dull = TexelRig.Hex("#2a1208", "#5a2410", "#8a3a16", "#b85a24");    // banked low

        /// <summary>Colours the biome weathering never touches - the glows and tells (CreatureWeathering).</summary>
        internal static Color[] Reserved => _reserved ??= Concat(Sulphur, Flame, Dull, new[] { Blast });
        static Color[] _reserved;
        static Color[] Concat(params Color[][] parts) { var l = new System.Collections.Generic.List<Color>(); foreach (var p in parts) l.AddRange(p); return l.ToArray(); }
        static readonly Color Eye = TexelRig.Hex("#140a06")[0];

        // ---------------------------------------------------------------- poses

        struct Pose
        {
            public float Bx, By, Lift, Swell, Open, Spin, Flare, Orbit, Jitter;
            public int Glow, Sparks, Seed;
            public bool Halo;
            public Vector4[] Flames; // x offset, length, half width, sway
        }

        static Vector4[] F(float a, float b, float c) => new[]
        {
            new Vector4(-2.2f, a, 1.5f, 0.8f), new Vector4(0.4f, b, 1.8f, -1.0f), new Vector4(2.6f, c, 1.3f, 0.6f),
        };

        static Pose P(float bx = 0, float by = 0, float lift = 0, float swell = 1, float open = 0, float spin = 0,
                      int glow = 1, float flare = 1, bool halo = false, float orbit = 0, int sparks = 2, int seed = 1,
                      float jitter = 0, Vector4[] flames = null)
            => new()
            {
                Bx = bx, By = by, Lift = lift, Swell = swell, Open = open, Spin = spin, Glow = glow, Flare = flare,
                Halo = halo, Orbit = orbit, Sparks = sparks, Seed = seed, Jitter = jitter, Flames = flames ?? F(6, 8, 5),
            };

        // open: the shell pushed outward; swell: the heart's size; flare: flame length; spin: the
        // shell's turn. Indexed by BombFrame.
        static readonly Pose[] Poses =
        {
            P(flames: F(6, 8.5f, 5), orbit: 0, seed: 1),                                 // Drift0
            P(by: 1, flames: F(7, 7.5f, 5.5f), orbit: 30, seed: 2, spin: 3),               // Drift1
            P(by: 1.5f, flames: F(5.5f, 8, 6.5f), orbit: 60, seed: 3, spin: 5),            // Drift2
            P(by: 0.5f, flames: F(6.5f, 9, 5), orbit: 90, seed: 4, spin: 2),               // Drift3
            P(),                                                                         // Idle
            P(swell: 1.1f, glow: 2, flare: 1.25f, by: 0.5f, sparks: 3, seed: 7, flames: F(7, 9, 6)),          // Gather
            P(swell: 1.15f, glow: 3, flare: 1.35f, bx: -0.5f, sparks: 4, seed: 8),                           // Throw0
            P(swell: 1.03f, glow: 3, flare: 1.15f, sparks: 5, seed: 9, flames: F(5, 9.5f, 6)),               // Throw1
            P(swell: 0.95f, glow: 1, flare: 0.7f, sparks: 1, seed: 10, flames: F(5, 6.5f, 4.5f)),            // Spent
            P(open: 0.6f, jitter: 18, glow: 3, flare: 1.35f, sparks: 6, seed: 11, flames: F(7, 10, 6.5f), swell: 1.05f), // Critical0
            P(open: 0.9f, jitter: 22, glow: 2, flare: 1.15f, by: 1, sparks: 5, seed: 12, flames: F(8, 8, 7.5f)),        // Critical1
            P(open: 1.6f, swell: 1.1f, glow: 2, flare: 1.25f, orbit: 20, sparks: 4, seed: 5, spin: -4),                  // Arming
            P(open: 3.4f, swell: 1.25f, glow: 3, flare: 1.55f, lift: 1.2f, halo: true, orbit: 45, sparks: 7, seed: 6, spin: -8), // Bursting
            P(open: -0.6f, swell: 0.82f, glow: 0, flare: 0.45f, bx: -0.5f, by: 0.5f, sparks: 0, flames: F(5, 6, 4), orbit: 10), // Banked0
            P(open: -0.6f, swell: 0.82f, glow: 0, flare: 0.4f, bx: -0.5f, by: 1, sparks: 0, flames: F(4.5f, 6.5f, 4.5f), orbit: 25), // Banked1
        };

        // The shell: five plates round the heart - angle, length, near side.
        static readonly (float a, float len, bool near)[] Plates =
            { (200f, 3.6f, false), (125f, 3.4f, false), (55f, 3.4f, true), (-15f, 3.6f, true), (-95f, 3.8f, true) };

        // ---------------------------------------------------------------- cache

        static readonly Dictionary<int, Sprite[]> _cache = new();
        static readonly Dictionary<int, Sprite> _singles = new();

        /// <summary>Every frame of one look and tier, indexed by <see cref="BombFrame"/>.</summary>
        public static Sprite[] For(EnemyLook look, bool elite) => For(look, elite, Art.Biomes.BiomeKind.Tower);

        /// <summary>The same, wearing the place's accent (CreatureAccents) - weathered after by
        /// CreatureWeathering.</summary>
        public static Sprite[] For(EnemyLook look, bool elite, Art.Biomes.BiomeKind biome)
        {
            int key = (look == EnemyLook.Automaton ? 1 : 0) | (elite ? 2 : 0) | ((int)biome << 2) | (Art.Biomes.Nigredo.Shards << 5);
            if (_cache.TryGetValue(key, out var hit) && hit != null && hit.Length > 0 && hit[0] != null) return hit;

            var mat = look == EnemyLook.Automaton ? Ironbound : Slag;
            var frames = new Sprite[Poses.Length];
            for (int i = 0; i < frames.Length; i++) frames[i] = Render(i, mat, elite, biome);
            _cache[key] = frames;
            return frames;
        }

        // ---------------------------------------------------------------- geometry

        static float Hash(int x, int y) => TexelRig.Hash(x, y);

        /// <summary>A tongue of flame: a wedge that sways, widest at its root.</summary>
        static TexelRig.Shape Tongue(Vector2 root, float len, float half, float sway) => (float x, float y, out TexelRig.Hit h) =>
        {
            h = default;
            float t = (y - root.y) / len;
            if (t < 0f || t > 1f) return false;
            float cx = root.x + Mathf.Sin(t * Mathf.PI * 1.2f) * sway * t;
            float hw = half * Mathf.Pow(1f - t, 0.8f) * (t < 0.15f ? 0.7f + 2f * t : 1f);
            h = new TexelRig.Hit { Nx = (x - cx) / Mathf.Max(0.01f, hw), T = t, HasT = true };
            return Mathf.Abs(x - cx) <= hw;
        };

        sealed class Lights
        {
            public (Vector2 c, float r)? Halo;
            public readonly List<Vector2> Sparks = new();
        }

        static TexelRig.Rig Build(in Pose p, bool elite, Lights fx)
        {
            var rig = new TexelRig.Rig();
            float cx = 17f + p.Bx, cy = 13f + p.By + p.Lift, r = 4.6f * p.Swell;

            // flames off the top, behind the shell's top plates
            for (int i = 0; i < p.Flames.Length; i++)
            {
                var f = p.Flames[i];
                rig.Add(1f + i * 0.01f, (int)Slot.Flame, Tongue(new Vector2(cx + f.x, cy + r * 0.55f), f.y * p.Flare, f.z, f.w), level: p.Glow);
            }

            // the heart, with a face of two dark slits
            rig.Add(3f, (int)Slot.Heart, TexelRig.Ellipse(cx, cy, r, r * 0.95f), level: p.Glow);
            float eyeY = cy + r * 0.12f;
            foreach (float ex in new[] { 0.38f, -0.05f })
                rig.Add(3.5f, (int)Slot.Eye, TexelRig.Ellipse(cx + ex * r + 0.9f, eyeY, 0.75f, 1.15f - (p.Glow == 0 ? 0.5f : 0f), -12f));

            // the shell plates: pushed out as it arms, clamped tight when flinched, loose when critical
            foreach (var (a0, len, near) in Plates)
            {
                int ai = Mathf.RoundToInt(a0);
                float a = a0 + p.Spin + (p.Jitter > 0f ? (Hash(ai, p.Seed) - 0.5f) * p.Jitter : 0f);
                var c = TexelRig.Pt(new Vector2(cx, cy), a, r + 0.9f + p.Open + (p.Jitter > 0f ? Hash(p.Seed, ai) * 0.8f : 0f));
                rig.Add(near ? 5f : 2f, (int)Slot.Plate, TexelRig.Ellipse(c.x, c.y, len, 1.55f, a + 90f), level: p.Glow, extra: near ? 1f : 0f);
            }

            // elite: three burning motes in orbit - the cluster's shards
            if (elite)
                for (int i = 0; i < 3; i++)
                {
                    float a = p.Orbit + i * 120f, orbitR = r + 6.2f;
                    var m = new Vector2(cx + Mathf.Cos(TexelRig.Rad(a)) * orbitR, cy + Mathf.Sin(TexelRig.Rad(a)) * orbitR * 0.55f);
                    bool front = Mathf.Sin(TexelRig.Rad(a)) < 0f;
                    rig.Add(front ? 6f : 0.5f, (int)Slot.Heart, TexelRig.Ellipse(m.x, m.y, 1.5f, 1.45f), level: Mathf.Max(1, p.Glow));
                    rig.Add(front ? 6.1f : 0.6f, (int)Slot.Plate, TexelRig.Ellipse(m.x - 0.6f, m.y - 0.7f, 1.2f, 0.6f, 20f), level: p.Glow);
                }

            if (p.Halo) fx.Halo = (new Vector2(cx, cy), r + p.Open + 3.2f);
            for (int i = 0; i < p.Sparks; i++)
                fx.Sparks.Add(new Vector2(cx - 3f + Hash(i, p.Seed) * 6f, cy + r + 3f + Hash(p.Seed, i) * 6f * p.Flare));
            // THE PLACE'S ACCENT on the shell's lower flanks (the flames own the top)
            CreatureAccents.Shelves(rig, 5.5f, new Vector2(cx - r * 0.7f, cy - r * 0.4f), new Vector2(cx + r * 0.55f, cy - r * 0.65f), 3.2f);
            CreatureAccents.StoneShards(rig, 5.6f, new Vector2(cx - r * 0.95f, cy - r * 0.1f), 1f);
            if (CreatureAccents.Sky)
                CreatureAccents.Puffs(rig, 5.5f, new[] { new Vector2(cx - r - 2.2f, cy - r * 0.9f), new Vector2(cx + r + 1.8f, cy - r * 0.6f), new Vector2(cx + 0.5f, cy - r - 2.6f) }, 1.9f);
            return rig;
        }

        // ---------------------------------------------------------------- raster

        static Sprite Render(int frame, Mat mat, bool elite, Art.Biomes.BiomeKind biome)
        {
            CreatureAccents.Biome = biome; CreatureAccents.Frame = frame; CreatureAccents.Shards = Art.Biomes.Nigredo.Shards;
            try { return RenderIn(frame, mat, elite, biome); }
            finally { CreatureAccents.Biome = Art.Biomes.BiomeKind.Tower; CreatureAccents.Shards = 0; }
        }

        static Sprite RenderIn(int frame, Mat mat, bool elite, Art.Biomes.BiomeKind biome)
        {
            var p = Poses[frame];
            float scale = elite ? EliteScale : 1f;
            int w = Mathf.RoundToInt(W * scale / 2f) * 2, h = Mathf.RoundToInt(H * scale);
            var fx = new Lights();
            var rig = Build(p, elite, fx);
            string name = $"enemy.bomb.{(mat.Iron ? "iron" : "slag")}{(elite ? ".elite" : "")}.{(BombFrame)frame}";
            if (biome != Art.Biomes.BiomeKind.Tower) name += "." + biome;
            return TexelRig.Rasterise(name, rig, w, h, scale, mat.Out,
                (in TexelRig.Cell c, int x, int y, int s, System.Func<int, int, TexelRig.Part> at) => ColourOf(c, x, y, mat, p, at),
                new Vector2(PivotX, PivotY), (px, cw, ch, sc, filled) => Light(px, cw, ch, sc, filled, fx),
                // fire has no outline; the slag does, and the iron flame is all metal
                outlines: part => mat.Iron || part.Slot == (int)Slot.Plate);
        }

        static void Light(Color[] px, int w, int h, float scale, System.Func<int, int, bool> filled, Lights fx)
        {
            foreach (var s in fx.Sparks)
            {
                int x = Mathf.FloorToInt(s.x * scale), y = Mathf.FloorToInt(s.y * scale);
                if (x >= 0 && y >= 0 && x < w && y < h && px[y * w + x].a == 0f) TexelRig.Put(px, w, x, y, Flame[3], 220f / 255f);
            }
            if (fx.Halo is not { } halo) return;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (filled(x, y)) continue;
                float d = Vector2.Distance(new Vector2((x + 0.5f) / scale, (y + 0.5f) / scale), halo.c);
                if (d <= halo.r && (x + y) % 2 == 0) TexelRig.Put(px, w, x, y, Sulphur[2], 120f / 255f);
            }
        }

        static Color Pipe(int g) => g == 0 ? Dull[2] : Sulphur[Mathf.Min(3, g + 1)];

        static Color? ColourOf(in TexelRig.Cell c, int x, int y, Mat mat, in Pose p, System.Func<int, int, TexelRig.Part> at)
        {
            var part = c.Part;
            var hit = c.Hit;
            int g = part.Level;
            switch ((Slot)part.Slot)
            {
                case Slot.Flame:
                {
                    float t = hit.T, edge = Mathf.Abs(hit.Nx);
                    if (mat.Iron)
                    {
                        // an iron flame: steel, with the fire run up its middle like piping
                        var st = mat.Steel;
                        if (edge < 0.3f && t < 0.9f) return Pipe(g);
                        if (t > 0.85f) return st[1];
                        return edge > 0.65f ? st[hit.Nx < 0f ? 3 : 0] : st[2];
                    }
                    var ramp = g == 0 ? Dull : Flame;
                    if (t > 0.82f) { var tip = ramp[1]; tip.a = 200f / 255f; return tip; }
                    if (edge > 0.7f) return ramp[1];
                    return ramp[t < 0.35f && edge < 0.4f ? 3 : 2];
                }
                case Slot.Eye:
                    return mat.Iron ? Pipe(Mathf.Max(1, p.Glow)) : Eye;
                case Slot.Heart:
                {
                    float r2 = hit.Nx * hit.Nx + hit.Ny * hit.Ny;
                    if (mat.Iron)
                    {
                        // one ring of piping at rest; an inner ring and a line across it as it heats
                        float r = Mathf.Sqrt(r2);
                        if (Mathf.Abs(r - 0.8f) < 0.08f) return Pipe(g);
                        if (g >= 2 && (Mathf.Abs(r - 0.42f) < 0.08f || (r < 0.42f && Mathf.Abs(hit.Ny) < 0.09f))) return Pipe(g);
                        if (r < 0.2f && g >= 2) return Sulphur[3];
                        return mat.Steel[TexelRig.BaseShade(hit.Nx, hit.Ny)];
                    }
                    if (g == 0) return Dull[r2 < 0.3f ? 3 : r2 < 0.7f ? 2 : 1];
                    if (r2 < 0.18f + 0.1f * g) return Sulphur[3];   // white at the middle, hotter nearer bursting
                    if (r2 < 0.75f) return Sulphur[2];
                    return Hash(x, y) < 0.5f ? Sulphur[1] : Flame[2]; // the rim boils
                }
            }

            // a plate: dark stone (or cast iron) lit along the edge that faces the fire
            var pr = mat.Plate;
            int s = TexelRig.BaseShade(hit.Nx, hit.Ny);
            if (part.Extra > 0f)
            {
                // a near plate's border against the fire it covers is softened, not cut to black
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var n = at(x + dx, y + dy);
                    if (n != null && n.Slot != (int)Slot.Plate && n.Order < part.Order) { s = Mathf.Min(s, 1); break; }
                }
            }
            if (hit.Ny < -0.45f) return g == 0 ? Dull[2] : Flame[Mathf.Min(3, 1 + g)]; // the inner edge, lit
            if (mat.Iron)
            {
                if (Mathf.Abs(hit.Nx) > 0.62f && Mathf.Abs(hit.Ny) < 0.35f && mat.Rivet.HasValue) return mat.Rivet.Value;
                if (Mathf.Abs(hit.Ny - 0.2f) < 0.16f) return pr[0];   // a cast ridge
            }
            else if (Hash(x, y) < 0.18f) return pr[Mathf.Max(0, s - 1)]; // pitted slag
            return pr[s];
        }

        // ---------------------------------------------------------------- the fireball, a mote, a plate

        /// <summary>
        /// The thrown fireball, 0.2 in radius (0.4 units across): a white-hot core, sulphur, an
        /// orange rim and a ragged dark-red edge, brighter toward the top. Three flicker frames.
        /// </summary>
        public static Sprite[] Fireball()
        {
            if (_singles.TryGetValue(100, out var f0) && f0 != null)
                return new[] { _singles[100], _singles[101], _singles[102] };
            float R = Core.Tuning.Enemy.BombFireballRadius * TexelRig.TexelsPerUnit;
            int n = Mathf.CeilToInt(R) * 2 + 4;
            var frames = new Sprite[3];
            for (int k = 0; k < 3; k++)
            {
                var px = new Color[n * n];
                float c = n / 2f;
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy) / R;
                    float noise = Hash(x + k * 13, y + k * 7);
                    if (d > 1f + (noise - 0.5f) * 0.35f) continue;
                    float lit = d - (dy > 0f ? 0.12f : 0f); // brighter toward the top
                    px[y * n + x] = d > 0.88f ? TexelRig.Hex("#6b1e0c")[0]
                                  : lit < 0.35f ? Sulphur[3] : lit < 0.6f ? Sulphur[2]
                                  : noise < 0.4f ? Flame[1] : Flame[2];
                }
                frames[k] = Single($"enemy.bomb.fireball.{k}", px, n, n, new Vector2(0.5f, 0.5f));
                _singles[100 + k] = frames[k];
            }
            return frames;
        }

        /// <summary>One of an elite's orbiting motes, flung loose as a cluster shard.</summary>
        public static Sprite Mote(EnemyLook look) => Piece(look, 200, "mote", rig =>
        {
            rig.Add(1f, (int)Slot.Heart, TexelRig.Ellipse(6f, 6f, 1.5f, 1.45f), level: 2);
            rig.Add(2f, (int)Slot.Plate, TexelRig.Ellipse(5.4f, 5.3f, 1.2f, 0.6f, 20f), level: 2);
        });

        /// <summary>One shell plate, flying loose when the heart bursts.</summary>
        public static Sprite Plate(EnemyLook look) => Piece(look, 300, "plate", rig =>
            rig.Add(1f, (int)Slot.Plate, TexelRig.Ellipse(6f, 6f, 3.6f, 1.55f, 0f), level: 2));

        static Sprite Piece(EnemyLook look, int key, string name, System.Action<TexelRig.Rig> build)
        {
            key += look == EnemyLook.Automaton ? 1 : 0;
            if (_singles.TryGetValue(key, out var hit) && hit != null) return hit;
            var mat = look == EnemyLook.Automaton ? Ironbound : Slag;
            var rig = new TexelRig.Rig();
            build(rig);
            var pose = Poses[(int)BombFrame.Idle];
            var sprite = TexelRig.Rasterise($"enemy.bomb.{name}.{(mat.Iron ? "iron" : "slag")}", rig, 12, 12, 1f, mat.Out,
                (in TexelRig.Cell c, int x, int y, int s, System.Func<int, int, TexelRig.Part> at) => ColourOf(c, x, y, mat, pose, at),
                new Vector2(6f, 6f), null, part => mat.Iron || part.Slot == (int)Slot.Plate);
            _singles[key] = sprite;
            return sprite;
        }

        static Sprite Single(string name, Color[] px, int w, int h, Vector2 pivot01)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave, name = name,
            };
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), pivot01, TexelRig.TexelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

    }
}
