using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>The Ranged's frames, in the order <see cref="RangedArt.For"/> returns them.</summary>
    public enum RangedFrame { Walk0, Walk1, Walk2, Walk3, Idle, Aim, Locked, LockedVolley, Fire, Kite0, Kite1 }

    /// <summary>
    /// The Ranged as a CINDER STINGER (the user's sign-off, 2026-10-18, from the mock-up at
    /// https://claude.ai/artifact/UF4tzequD46yb7wmh1hZ7c - a hooded caster was rejected first for
    /// being humanoid): a low, spindly six-legged crawler whose segmented tail ends in a gland of
    /// fire, aimed over its own back like a gun on its spine.
    ///
    /// THE BODY SAYS THE ATTACK. The fragile one: the body is barely 8 texels high and all legs,
    /// only the tail gives it height. It scuttles on alternating tripods with the gland carried
    /// high like a lantern. The 0.6 s telegraph arches the tail forward past the head while the
    /// rear lifts; the gland swells and burns through its membrane, white-hot with a halo on the
    /// LOCKED frame (the tell). The gland's fire is the bolt's orange, the colour of the ground
    /// line. Fire snaps the tail flat and empties the gland; backing off throws the tail high while
    /// it refills. An elite's gland has THREE LOBES, all lit only when a volley is next - three
    /// lights, three bolts (<see cref="EnemyController.NextShotIsElitePattern"/>).
    ///
    /// ONE RIG, TWO MATERIALS: Construct is CHITIN (violet-black carapace, the bare gland),
    /// Automaton is IRONBOUND (riveted plates, a bronze ring and bar round the gland). Drawn at
    /// scale 1 in texel units through <see cref="TexelRig"/>, an elite rasterised 1.2x on the same
    /// grid. Ported from the mock-up's JavaScript line for line - change both or neither.
    /// </summary>
    public static class RangedArt
    {
        const float EliteScale = 1.2f;
        const int W = 40, H = 28;
        const float PivotX = 16.5f, PivotY = 6f;

        enum Slot { Shell, Leg, Acc, Cage }
        enum Decor { None, Plates, Seg, Head }

        sealed class Mat
        {
            public Color[] Shell, Leg, Gland, Cage;
            public Color Out;
            public bool Iron;
        }

        static readonly Mat Chitin = new()
        {
            Shell = TexelRig.Hex("#1c1720", "#2f2735", "#463a4e", "#635469"),
            Leg = TexelRig.Hex("#17131a", "#28212d", "#3b3242", "#544858"),
            Gland = TexelRig.Hex("#3a1a0c", "#7a3414", "#b8561c", "#e58a3a"),
            Out = TexelRig.Hex("#0b090d")[0],
        };

        static readonly Mat Ironbound = new()
        {
            Shell = TexelRig.Hex("#22242a", "#3a3e47", "#5d636e", "#8f96a1"),
            Leg = TexelRig.Hex("#1b1d22", "#2d3037", "#454a53", "#666c77"),
            Gland = TexelRig.Hex("#3a1a0c", "#7a3414", "#b8561c", "#e58a3a"),
            Cage = TexelRig.Hex("#4a3418", "#7a5826", "#a87c3a", "#d1a964"),
            Out = TexelRig.Hex("#0b0a0e")[0],
            Iron = true,
        };

        /// <summary>The Ranged's own tint, dim to hot - the eyes.</summary>
        static readonly Color[] Violet = TexelRig.Hex("#2e2452", "#5a45a3", "#8c73f2", "#d2c6ff");

        /// <summary>The bolt's orange (EnemyProjectile, the ground line) - the gland's fire.</summary>
        static readonly Color[] Ember = TexelRig.Hex("#4a2210", "#d0601e", "#ff8c26", "#ffe2b0");

        // ---------------------------------------------------------------- poses

        struct Pose
        {
            public float Bx, By, Rear, A0, Curl, Swell;
            public int Ember, Eye, Lit;
            public bool Halo, Burst;
            public Vector2 GaitA, GaitB; // the two tripods' foot offsets (x, lift)
        }

        static Pose P(float bx = 0, float by = 0, float rear = 0, float a0 = 110, float curl = -15, float swell = 1.9f,
                      int ember = 1, int eye = 1, int lit = 0, bool halo = false, bool burst = false,
                      float ax = 0, float ay = 0, float bxg = 0, float byg = 0)
            => new()
            {
                Bx = bx, By = by, Rear = rear, A0 = a0, Curl = curl, Swell = swell, Ember = ember, Eye = eye, Lit = lit,
                Halo = halo, Burst = burst, GaitA = new Vector2(ax, ay), GaitB = new Vector2(bxg, byg),
            };

        // a0: the tail's first angle; curl: how much each segment turns; swell: the gland's radius.
        // Indexed by RangedFrame.
        static readonly Pose[] Poses =
        {
            P(a0: 112, ax: 1.6f, bxg: -1.6f),                                               // Walk0
            P(a0: 108, by: 0.5f, byg: 1.4f),                                                // Walk1
            P(a0: 112, ax: -1.6f, bxg: 1.6f),                                               // Walk2
            P(a0: 108, by: 0.5f, ay: 1.4f),                                                 // Walk3
            P(),                                                                            // Idle
            P(a0: 104, curl: -19, rear: 0.6f, by: -0.4f, swell: 2.2f, ember: 2, eye: 2, ax: 1, bxg: -0.6f),
            P(a0: 100, curl: -22, rear: 1.2f, by: -0.8f, swell: 2.5f, ember: 3, eye: 3, halo: true, lit: 1, ax: 1.4f, bxg: -1),
            P(a0: 100, curl: -22, rear: 1.2f, by: -0.8f, swell: 2.5f, ember: 3, eye: 3, halo: true, lit: 3, ax: 1.4f, bxg: -1),
            P(a0: 92, curl: -12, rear: 0.4f, bx: -1, swell: 1.3f, ember: 0, eye: 3, burst: true, ax: 1.4f, bxg: -1),
            P(a0: 122, curl: -12, bx: -0.5f, by: 0.4f, swell: 1.6f, ember: 1, eye: 2, ax: -1.8f, bxg: 1.2f, byg: 1.3f),
            P(a0: 124, curl: -12, bx: -0.5f, swell: 1.6f, ember: 1, eye: 2, ax: 1.2f, ay: 1.3f, bxg: -1.8f),
        };

        // ---------------------------------------------------------------- cache

        static readonly Dictionary<int, Sprite[]> _cache = new();

        /// <summary>Every frame of one look and tier, indexed by <see cref="RangedFrame"/>.</summary>
        public static Sprite[] For(EnemyLook look, bool elite)
        {
            int key = (look == EnemyLook.Automaton ? 1 : 0) | (elite ? 2 : 0);
            if (_cache.TryGetValue(key, out var hit) && hit != null && hit.Length > 0 && hit[0] != null) return hit;

            var mat = look == EnemyLook.Automaton ? Ironbound : Chitin;
            var frames = new Sprite[Poses.Length];
            for (int i = 0; i < frames.Length; i++) frames[i] = Render(i, mat, elite);
            _cache[key] = frames;
            return frames;
        }

        // ---------------------------------------------------------------- the rig

        // Six legs: hip x, near side?, which tripod. The tripods alternate in the scuttle.
        static readonly (float hip, bool near, int tri)[] Legs =
            { (19f, true, 0), (16.2f, false, 0), (13.4f, true, 0), (19f, false, 1), (16.2f, true, 1), (13.4f, false, 1) };

        static float RestFoot(float hip) => hip > 18f ? 25.5f : hip > 15f ? 18.5f : 9f;

        static Vector2 Pt(Vector2 p, float a, float l) => TexelRig.Pt(p, a, l);

        sealed class Lights
        {
            public readonly List<(Vector2 c, float r)> Halos = new();
            public (Vector2 c, float r, float dir)? Burst;
        }

        static TexelRig.Rig Build(in Pose p, Mat mat, bool elite, Lights fx)
        {
            var rig = new TexelRig.Rig();
            TexelRig.Part Add(float order, Slot slot, TexelRig.Shape test, bool far = false, bool sep = false,
                              Decor decor = Decor.None, float level = 0, float extra = 0, TexelRig.Part refPart = null)
                => rig.Add(order, (int)slot, test, far, sep, (int)decor, level, extra, refPart);
            float bx = p.Bx, by = p.By;

            // legs: thin, many, knees high - it is all reach and no mass
            foreach (var (hx0, near, tri) in Legs)
            {
                var g = tri == 0 ? p.GaitA : p.GaitB;
                var hip = new Vector2(hx0 + bx, 5.6f + by + p.Rear * (19f - hx0) / -6f);
                float rest = RestFoot(hx0);
                var foot = new Vector2(rest + bx * 0.6f + g.x + (near ? 0f : -1.1f), 1f + g.y + (near ? 0f : 0.6f));
                // knees ride high and out past the body, a spider's - placed, not solved
                float outward = rest > hx0 + 3f ? 1f : rest < hx0 - 3f ? -1f : 0.35f;
                var knee = new Vector2(hip.x + outward * 2.6f + g.x * 0.45f, hip.y + 3.1f + g.y * 0.6f);
                float o = near ? 6f : 1f;
                Add(o, Slot.Leg, TexelRig.Taper(hip, knee, 0.8f, 0.65f), far: !near, sep: near);
                Add(o + 0.1f, Slot.Leg, TexelRig.Taper(knee, foot, 0.65f, 0.3f), far: !near, sep: near);
            }

            // the body: an abdomen the tail grows from, a low thorax, a small head
            Add(2f, Slot.Shell, TexelRig.Ellipse(12f + bx, 6.9f + by + p.Rear, 3.2f, 2.5f), decor: Decor.Plates);
            Add(3f, Slot.Shell, TexelRig.Ellipse(16.4f + bx, 6.1f + by + p.Rear * 0.4f, 4.6f, 2.6f), decor: Decor.Plates);
            Add(4f, Slot.Shell, TexelRig.Ellipse(21.4f + bx, 5.5f + by, 2.3f, 1.95f), sep: true, decor: Decor.Head);
            foreach (float dy in new[] { 0.6f, -0.6f })
                Add(4.5f, Slot.Leg, TexelRig.Taper(new Vector2(22.9f + bx, 4.9f + by + dy * 0.4f),
                                                   new Vector2(24.9f + bx, 3.9f + by + dy), 0.55f, 0.15f), sep: true);
            Add(5f, Slot.Acc, TexelRig.Ellipse(22.4f + bx, 6.3f + by, 0.6f, 0.55f), level: p.Eye);
            Add(5f, Slot.Acc, TexelRig.Ellipse(21.3f + bx, 6.6f + by, 0.5f, 0.5f), level: p.Eye);
            Add(5f, Slot.Acc, TexelRig.Ellipse(23.0f + bx, 5.6f + by, 0.4f, 0.4f), level: p.Eye);

            // the tail: six segments curling over the back, the gland at its end aimed like a gun
            var segs = new List<(Vector2 mid, float r)>();
            var gland = Gland(p, segs, out float aim, out var tip);
            for (int i = 0; i < segs.Count; i++)
                Add(7f + i * 0.1f, Slot.Shell, TexelRig.Ellipse(segs[i].mid.x, segs[i].mid.y, segs[i].r + 0.35f, segs[i].r),
                    sep: true, decor: Decor.Seg);
            float gr = p.Swell;
            if (elite)
                foreach (float da in new[] { -55f, 55f })
                {
                    // three lobes, one per bolt; the side two light only when a volley is next
                    var c = Pt(gland, aim + da, gr * 0.75f);
                    Add(8f, Slot.Acc, TexelRig.Ellipse(c.x, c.y, gr * 0.62f, gr * 0.62f), level: p.Lit >= 3 ? p.Ember : 0, extra: 1f);
                    if (p.Halo && p.Lit >= 3) fx.Halos.Add((c, gr * 1.3f));
                }
            var glandPart = Add(8.5f, Slot.Acc, TexelRig.Ellipse(gland.x, gland.y, gr, gr * 0.9f), level: p.Ember, extra: 1f);
            if (mat.Iron)
                Add(8.6f, Slot.Cage, TexelRig.Ellipse(gland.x, gland.y, gr + 0.2f, gr * 0.9f + 0.2f), refPart: glandPart);
            Add(9f, Slot.Leg, TexelRig.Taper(Pt(gland, aim, gr * 0.7f), tip, 0.55f, 0.12f), sep: true);
            if (p.Halo) fx.Halos.Add((gland, gr * 1.9f));
            if (p.Burst) fx.Burst = (tip, 3.2f, aim);
            return rig;
        }

        /// <summary>The tail's segments (into <paramref name="segs"/> if given), and the gland at
        /// its end: where it is, which way it points, and where its sting's tip is. One function
        /// for the drawing and for <see cref="MuzzleOffset"/>, so the shot cannot drift off the art.</summary>
        static Vector2 Gland(in Pose p, List<(Vector2 mid, float r)> segs, out float aim, out Vector2 tip)
        {
            var at = new Vector2(10.2f + p.Bx, 7.6f + p.By + p.Rear);
            float a = p.A0;
            for (int i = 0; i < 6; i++)
            {
                var next = Pt(at, a, 2.15f);
                segs?.Add(((at + next) * 0.5f, 1.45f - i * 0.12f));
                at = next;
                a += p.Curl;
            }
            aim = a - p.Curl;
            var gland = Pt(at, aim, p.Swell * 0.9f);
            tip = Pt(gland, aim, p.Swell + 1.4f);
            return gland;
        }

        /// <summary>
        /// Where the shot leaves, in WORLD units from the body's centre: the sting's tip on the
        /// LOCKED frame, mirrored when the Stinger faces left. The bolt, its ground line and the
        /// muzzle flash all start here (EnemyController.RangedMuzzle), so they leave the gland
        /// the player was watching rather than the middle of the body under it.
        /// </summary>
        public static Vector2 MuzzleOffset(bool elite, bool faceLeft)
        {
            float scale = elite ? EliteScale : 1f;
            Gland(Poses[(int)RangedFrame.Locked], null, out _, out var tip);
            // the sprite's pivot is rounded to a whole texel (TexelRig.Rasterise); measure from it
            float pivotX = Mathf.Round(PivotX * scale), pivotY = Mathf.Round(PivotY * scale);
            var off = new Vector2(tip.x * scale - pivotX, tip.y * scale - pivotY) / TexelRig.TexelsPerUnit;
            if (faceLeft) off.x = -off.x;
            return off;
        }

        // ---------------------------------------------------------------- raster

        static Sprite Render(int frame, Mat mat, bool elite)
        {
            var p = Poses[frame];
            float scale = elite ? EliteScale : 1f;
            int w = Mathf.RoundToInt(W * scale / 2f) * 2, h = Mathf.RoundToInt(H * scale);
            var fx = new Lights();
            var rig = Build(p, mat, elite, fx);

            string name = $"enemy.ranged.{(mat.Iron ? "iron" : "chitin")}{(elite ? ".elite" : "")}.{(RangedFrame)frame}";
            return TexelRig.Rasterise(name, rig, w, h, scale, mat.Out,
                (in TexelRig.Cell c, int x, int y, int s) => ColourOf(c, x, y, mat, s),
                new Vector2(PivotX, PivotY), (px, cw, ch, sc, filled) => Light(px, cw, ch, sc, filled, fx));
        }

        /// <summary>Light that leaves the body: the locked gland's halo, the shot's burst.</summary>
        static void Light(Color[] px, int w, int h, float scale, System.Func<int, int, bool> filled, Lights fx)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (filled(x, y)) continue;
                float wx = (x + 0.5f) / scale, wy = (y + 0.5f) / scale;
                foreach (var (c, r) in fx.Halos)
                    if (Vector2.Distance(new Vector2(wx, wy), c) <= r && (x + y) % 2 == 0)
                        TexelRig.Put(px, w, x, y, Ember[2], 150f / 255f);
                if (fx.Burst is { } b)
                {
                    float dx = wx - b.c.x, dy = wy - b.c.y, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float cos = Mathf.Cos(TexelRig.Rad(b.dir)), sin = Mathf.Sin(TexelRig.Rad(b.dir));
                    float along = dx * cos + dy * sin, across = Mathf.Abs(-dx * sin + dy * cos);
                    if (d <= b.r * 0.55f) TexelRig.Put(px, w, x, y, Ember[3], 230f / 255f);
                    else if (d <= b.r && (across < 0.6f || Mathf.Abs(along) < 0.6f || Mathf.Abs(Mathf.Abs(along) - across) < 0.5f))
                        TexelRig.Put(px, w, x, y, Ember[2], 200f / 255f);
                }
            }
        }

        static Color? ColourOf(in TexelRig.Cell c, int x, int y, Mat mat, int s)
        {
            var part = c.Part;
            var hit = c.Hit;
            switch ((Slot)part.Slot)
            {
                case Slot.Acc:
                    if (part.Extra > 0f) return Gland(hit, part.Level, mat);
                    return Violet[part.Level];
                case Slot.Cage:
                {
                    // a bronze ring and one bar across the gland; between them the fire shows
                    float r2 = hit.Nx * hit.Nx + hit.Ny * hit.Ny;
                    if (r2 > 0.86f || Mathf.Abs(hit.Nx) < 0.1f) return mat.Cage[r2 > 0.86f && hit.Ny > 0f ? 3 : 1];
                    if (part.Ref != null && part.Ref.Test(c.Px, c.Py, out var under)) return Gland(under, part.Ref.Level, mat);
                    return mat.Cage[1];
                }
            }

            var ramp = (Slot)part.Slot == Slot.Leg ? mat.Leg : mat.Shell;
            bool iron = mat.Iron;
            switch ((Decor)part.Decor)
            {
                case Decor.Plates:
                    if (!iron && Mathf.Abs(hit.Nx % 0.5f) < 0.07f && hit.Ny > -0.6f) return ramp[Mathf.Max(0, s - 1)]; // chitin ridges
                    if (iron && Mathf.Abs(hit.Ny - 0.15f) < 0.16f) return ramp[0];                                    // plate seam
                    if (iron && Mathf.Abs(hit.Ny - 0.45f) < 0.12f && x % 3 == 0) return ramp[3];                      // rivets
                    if (!iron && hit.Ny > 0.45f && TexelRig.Hash(x, y) < 0.25f) return ramp[3];                       // sheen
                    break;
                case Decor.Seg:
                    if (hit.Nx < -0.6f) return ramp[0];                                                               // each segment's rim
                    if (iron && Mathf.Abs(hit.Nx - 0.1f) < 0.15f) return ramp[3];
                    break;
                case Decor.Head:
                    if (iron && hit.Ny > 0.35f) return ramp[0];
                    break;
            }
            return ramp[s];
        }

        /// <summary>A membrane lit from inside: the fire shows through, brightest at the core.</summary>
        static Color Gland(in TexelRig.Hit hit, int level, Mat mat)
        {
            float r2 = hit.Nx * hit.Nx + hit.Ny * hit.Ny;
            if (level >= 2 && r2 < 0.35f) return Ember[3];
            if (level == 0) return mat.Gland[r2 > 0.6f ? 0 : 1];
            if (r2 > 0.75f) return mat.Gland[Mathf.Min(3, level + 1)];
            return Ember[level];
        }
    }
}
