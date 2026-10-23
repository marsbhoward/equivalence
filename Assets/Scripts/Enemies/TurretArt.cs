using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>The Turret's frames, in the order <see cref="TurretArt.For"/> returns them.</summary>
    public enum TurretFrame { Idle0, Idle1, Idle2, Idle3, Charge, Charged, Beam0, Beam1, Overcharge, Lob, Spun0, Spun1 }

    /// <summary>
    /// The Turret as a TEMPEST EYE (the user's sign-off, 2026-10-18, from the mock-up at
    /// https://claude.ai/artifact/NHtsf4ykLcZoKhUhi7Ja9w): a storm caught in a sphere over a stone
    /// plinth - a knot of cloud with one large eye of lightning, a band of wind turning round it,
    /// two stones riding the wind. It never moves.
    ///
    /// THE BODY SAYS THE ATTACK. The eye's PUPIL always looks at its target - on a side-on body
    /// the pupil is the swivel, a separate small sprite (<see cref="Pupils"/>) TurretVisual moves
    /// inside the iris. Through the 0.7 s charge the band tightens and arcs crawl the cloud, the
    /// eye white-hot on CHARGED (the tell); the beam is a crackling lightning arc from the eye
    /// (LightningArc), the orb flickering between two bright frames while it holds. Flinched, the
    /// band sags, the stones drop and smoke leaks (SPUN). An elite's overcharge swells and blazes
    /// it; its mire lob dips the orb, and lands as charged ground (MireField's static style).
    ///
    /// ONE RIG, TWO MATERIALS: Construct is STONE (carved runes that light, a living storm cloud,
    /// the Turret's cyan), Automaton is IRONBOUND (the storm cast in iron, the lightning run
    /// through it like piping, an iron ring for the band - and its accents and lightning YELLOW,
    /// the user's call). Wind has no outline; stone, cloud and iron do. Ported from the mock-up's
    /// JavaScript line for line - change both or neither.
    /// </summary>
    public static class TurretArt
    {
        const float EliteScale = 1.2f;
        const int W = 34, H = 34;
        const float PivotX = 17f, PivotY = 12f; // between the plinth and the orb - the body's centre

        enum Slot { Base, Band, Cloud, Eye, Stone }
        enum Decor { None, Plinth, Cap }

        sealed class Mat
        {
            public Color[] Base, Cloud, Stone, Acc;
            public Color Band, Out;
            public Color? Rivet;
            public bool Iron;
        }

        /// <summary>The Turret's own cyan: the eye, the runes, the lightning.</summary>
        static readonly Color[] Cyan = TexelRig.Hex("#123a44", "#2a8a9c", "#4dd9f2", "#d6f8ff");
        static readonly Color[] Yellow = TexelRig.Hex("#4a3a0c", "#a8841e", "#ffd84a", "#fff6c4");

        /// <summary>Colours the biome weathering never touches - the glows and tells (CreatureWeathering).</summary>
        internal static Color[] Reserved => _reserved ??= Concat(Cyan, Yellow);
        static Color[] _reserved;
        static Color[] Concat(params Color[][] parts) { var l = new System.Collections.Generic.List<Color>(); foreach (var p in parts) l.AddRange(p); return l.ToArray(); }
        static readonly Color PupilDark = TexelRig.Hex("#08121a")[0];
        static readonly Color Smoke = new(0.42f, 0.45f, 0.52f, 140f / 255f);

        static readonly Mat Stone = new()
        {
            Base = TexelRig.Hex("#2a2826", "#45413c", "#625c54", "#857d72"),
            Cloud = TexelRig.Hex("#161a26", "#252d40", "#3a4560", "#56658a"),
            Stone = TexelRig.Hex("#2f2c29", "#4a4540", "#6b645b", "#8d8478"),
            Band = TexelRig.Hex("#cfeef6")[0], Out = TexelRig.Hex("#0b0a0d")[0], Acc = Cyan,
        };

        static readonly Mat Ironbound = new()
        {
            Base = TexelRig.Hex("#1f2126", "#363a42", "#565c67", "#878e99"),
            Cloud = TexelRig.Hex("#24272e", "#3d424c", "#5f6672", "#8f97a4"),
            Stone = TexelRig.Hex("#2a2d33", "#454a53", "#666c77", "#8f96a1"),
            Band = TexelRig.Hex("#9aa3ae")[0], Rivet = TexelRig.Hex("#b6bdc7")[0], Out = TexelRig.Hex("#0a0a0c")[0],
            Acc = Yellow, Iron = true,
        };

        /// <summary>A look's accent ramp, dim to hot - the lightning, the static mire, the flashes.</summary>
        public static Color[] Accent(EnemyLook look) => look == EnemyLook.Automaton ? Yellow : Cyan;

        // ---------------------------------------------------------------- poses

        struct Pose
        {
            public float Bob, Dip, Swell, Span, Sag, Tilt, Phase, Eye, Drop;
            public int Glow, Arcs, Smoke, Seed;
            public bool Halo;
        }

        static Pose P(float bob = 0, float dip = 0, float swell = 1, float span = 1, float sag = 0, float tilt = 0,
                      float phase = 0, float eye = 0.85f, int glow = 1, int arcs = 0, bool halo = false, int smoke = 0,
                      float drop = 0, int seed = 1)
            => new()
            {
                Bob = bob, Dip = dip, Swell = swell, Span = span, Sag = sag, Tilt = tilt, Phase = phase, Eye = eye,
                Glow = glow, Arcs = arcs, Halo = halo, Smoke = smoke, Drop = drop, Seed = seed,
            };

        // span: the wind band's size; phase: its turn; eye: the iris' size; dip/sag/drop: winding
        // down. Indexed by TurretFrame.
        static readonly Pose[] Poses =
        {
            P(phase: 0), P(phase: 30, bob: 0.5f), P(phase: 60, bob: 1), P(phase: 90, bob: 0.5f),
            P(span: 0.85f, phase: 40, eye: 1, glow: 2, arcs: 2, seed: 3),
            P(span: 0.74f, phase: 80, eye: 1.15f, glow: 3, arcs: 4, halo: true, seed: 5),
            P(span: 0.74f, phase: 20, eye: 1.1f, glow: 3, arcs: 3, seed: 7),
            P(span: 0.78f, phase: 60, eye: 1.15f, glow: 3, arcs: 2, bob: 0.5f, seed: 8),
            P(span: 0.7f, phase: 50, eye: 1.3f, glow: 3, arcs: 6, halo: true, swell: 1.12f, seed: 9),
            P(span: 1.05f, phase: 10, dip: 2.2f, eye: 0.9f, glow: 2, sag: 0.8f, tilt: 8),
            P(span: 1.15f, sag: 1.2f, tilt: 10, eye: 0.55f, glow: 0, smoke: 3, drop: 1.6f, dip: 0.8f, seed: 11),
            P(span: 1.18f, sag: 1.4f, tilt: 12, eye: 0.5f, glow: 0, smoke: 4, drop: 2.2f, dip: 1, phase: 20, seed: 12),
        };

        static float OrbY(in Pose p) => 20.5f + p.Bob - p.Dip;
        // The iris sits a little forward of the orb's middle; the pupil moves inside it.
        static Vector2 IrisAt(in Pose p) => new(17f + 1.5f, OrbY(p) + 0.4f);

        // ---------------------------------------------------------------- cache

        static readonly Dictionary<int, Sprite[]> _frames = new(), _pupils = new();

        /// <summary>Every frame of one look and tier, indexed by <see cref="TurretFrame"/>.</summary>
        public static Sprite[] For(EnemyLook look, bool elite) => For(look, elite, Art.Biomes.BiomeKind.Tower);

        /// <summary>The same, wearing the place's accent (CreatureAccents) - weathered after by
        /// CreatureWeathering.</summary>
        public static Sprite[] For(EnemyLook look, bool elite, Art.Biomes.BiomeKind biome)
        {
            int key = (look == EnemyLook.Automaton ? 1 : 0) | (elite ? 2 : 0) | ((int)biome << 2);
            if (_frames.TryGetValue(key, out var hit) && hit != null && hit.Length > 0 && hit[0] != null) return hit;
            var mat = look == EnemyLook.Automaton ? Ironbound : Stone;
            var frames = new Sprite[Poses.Length];
            for (int i = 0; i < frames.Length; i++) frames[i] = Render(i, mat, elite, biome);
            _frames[key] = frames;
            return frames;
        }

        /// <summary>The pupil for each frame (its height follows the iris, its colour the glow),
        /// drawn about its own centre for TurretVisual to place.</summary>
        public static Sprite[] Pupils(EnemyLook look, bool elite)
        {
            int key = (look == EnemyLook.Automaton ? 1 : 0) | (elite ? 2 : 0);
            if (_pupils.TryGetValue(key, out var hit) && hit != null && hit.Length > 0 && hit[0] != null) return hit;
            var mat = look == EnemyLook.Automaton ? Ironbound : Stone;
            float scale = elite ? EliteScale : 1f;
            var pupils = new Sprite[Poses.Length];
            for (int i = 0; i < pupils.Length; i++)
            {
                var p = Poses[i];
                var rig = new TexelRig.Rig();
                rig.Add(1f, 0, TexelRig.Ellipse(3f, 3f, 0.9f, 1.9f * Mathf.Min(1f, p.Eye + 0.2f)));
                var col = p.Glow >= 3 ? mat.Acc[3] : PupilDark;
                pupils[i] = TexelRig.Rasterise($"enemy.turret.pupil.{(mat.Iron ? "iron" : "stone")}{(elite ? ".elite" : "")}.{i}",
                    rig, Mathf.RoundToInt(6 * scale), Mathf.RoundToInt(6 * scale), scale, Color.clear,
                    (in TexelRig.Cell c, int x, int y, int s, System.Func<int, int, TexelRig.Part> at) => col,
                    new Vector2(3f, 3f), null, _ => false);
            }
            _pupils[key] = pupils;
            return pupils;
        }

        /// <summary>
        /// Where the iris is on <paramref name="frame"/>, in WORLD units from the body's centre,
        /// mirrored when it faces left - the pupil's home, and the beam's origin.
        /// </summary>
        public static Vector2 IrisOffset(int frame, bool elite, bool faceLeft)
        {
            float scale = elite ? EliteScale : 1f;
            var iris = IrisAt(Poses[Mathf.Clamp(frame, 0, Poses.Length - 1)]);
            float pivotX = Mathf.Round(PivotX * scale), pivotY = Mathf.Round(PivotY * scale);
            var off = new Vector2(iris.x * scale - pivotX, iris.y * scale - pivotY) / TexelRig.TexelsPerUnit;
            if (faceLeft) off.x = -off.x;
            return off;
        }

        /// <summary>How far, in world units, the pupil may move inside the iris toward its target.</summary>
        public static Vector2 PupilTravel(bool elite) => new Vector2(1.6f, 1.3f) * ((elite ? EliteScale : 1f) / TexelRig.TexelsPerUnit);

        // ---------------------------------------------------------------- geometry

        static float Hash(int x, int y) => TexelRig.Hash(x, y);
        static float Rad(float d) => TexelRig.Rad(d);

        /// <summary>A thin elliptical ring (the band of wind), front or back half; its hit reports
        /// the angle round the ring in Nx/Ny.</summary>
        static TexelRig.Shape RingBand(float cx, float cy, float rx, float ry, float rot, float w, bool front)
        {
            float c = Mathf.Cos(Rad(-rot)), s = Mathf.Sin(Rad(-rot));
            return (float x, float y, out TexelRig.Hit h) =>
            {
                h = default;
                float dx = x - cx, dy = y - cy, lx = dx * c - dy * s, ly = dx * s + dy * c;
                float d = Mathf.Sqrt((lx / rx) * (lx / rx) + (ly / ry) * (ly / ry));
                if (Mathf.Abs(d - 1f) * Mathf.Min(rx, ry) > w) return false;
                if (front != (ly < 0f)) return false;
                h = new TexelRig.Hit { Nx = lx / rx, Ny = ly / ry };
                return true;
            };
        }

        sealed class Lights
        {
            public (Vector2 c, float r)? Halo;
            public readonly List<(Vector2 c, float r, int seed)> Arcs = new();
            public readonly List<Vector2> Smoke = new();
        }

        static TexelRig.Rig Build(in Pose p, Lights fx)
        {
            var rig = new TexelRig.Rig();
            // the plinth: a squat, tapered block with a cap, rooted in the floor
            rig.Add(1f, (int)Slot.Base, TexelRig.Poly(new[] { new Vector2(8.5f, 0f), new Vector2(25.5f, 0f), new Vector2(22.5f, 7f), new Vector2(11.5f, 7f) },
                                                      17f, 4f, 8f, 5f), decor: (int)Decor.Plinth);
            rig.Add(1.5f, (int)Slot.Base, TexelRig.Ellipse(17f, 7.4f, 6.4f, 1.7f), decor: (int)Decor.Cap);

            // the orb, hovering, and the wind band round it (the back half behind, the front over)
            float oy = OrbY(p), r = 6.6f * p.Swell;
            float rx = 10.2f * p.Span, ry = 2.5f * p.Span, rot = 12f + p.Tilt, by = oy - p.Sag;
            rig.Add(2f, (int)Slot.Band, RingBand(17f, by, rx, ry, rot, 0.7f, false), extra: p.Phase);
            rig.Add(6f, (int)Slot.Band, RingBand(17f, by, rx, ry, rot, 0.7f, true), extra: p.Phase);
            rig.Add(3f, (int)Slot.Cloud, TexelRig.Ellipse(17f, oy, r, r * 0.96f));

            // the iris of lightning (the pupil is its own sprite)
            var iris = IrisAt(p);
            rig.Add(4f, (int)Slot.Eye, TexelRig.Ellipse(iris.x, iris.y, 3.7f * p.Eye, 2.95f * p.Eye), level: p.Glow);

            // stones riding the wind
            for (int i = 0; i < 2; i++)
            {
                float a = Rad(p.Phase * 1.3f + i * 180f + 30f);
                float sx = 17f + Mathf.Cos(a) * rx, sy = oy + Mathf.Sin(a) * ry - p.Drop * (2 + i * 2);
                rig.Add(Mathf.Sin(a) < 0f ? 6.5f : 1.8f, (int)Slot.Stone, TexelRig.Ellipse(sx, Mathf.Max(9f, sy), 1.6f, 1.2f, 25f * i));
            }

            for (int i = 0; i < p.Arcs; i++) fx.Arcs.Add((new Vector2(17f, oy), r + 0.4f, p.Seed + i));
            if (p.Halo) fx.Halo = (new Vector2(17f, oy), r + 3.2f);
            for (int i = 0; i < p.Smoke; i++) fx.Smoke.Add(new Vector2(17f - 3f + Hash(i, p.Seed) * 6f, oy + r + 1f + i * 2.4f));
            // THE PLACE'S ACCENT on the plinth, which never moves
            if (CreatureAccents.Forest)
                CreatureAccents.Strands(rig, 1.6f, CreatureAccents.Kind.Moss,
                    new[] { new Vector2(11.6f, 6.3f), new Vector2(13.9f, 5.9f), new Vector2(20.4f, 5.9f), new Vector2(22.6f, 6.3f) }, 4.2f, 0.85f);
            if (CreatureAccents.Salt) CreatureAccents.Shelves(rig, 1.6f, new Vector2(11.4f, 8.4f), new Vector2(22.6f, 8.2f), 2.4f);
            if (CreatureAccents.Sky)
                CreatureAccents.Puffs(rig, 1.7f, new[] { new Vector2(8.6f, 1.2f), new Vector2(25.2f, 1.6f), new Vector2(12.4f, 0.4f) }, 1.9f);
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
            var p = Poses[frame];
            float scale = elite ? EliteScale : 1f;
            int w = Mathf.RoundToInt(W * scale / 2f) * 2, h = Mathf.RoundToInt(H * scale);
            var fx = new Lights();
            var rig = Build(p, fx);
            string name = $"enemy.turret.{(mat.Iron ? "iron" : "stone")}{(elite ? ".elite" : "")}.{(TurretFrame)frame}";
            if (biome != Art.Biomes.BiomeKind.Tower) name += "." + biome;
            return TexelRig.Rasterise(name, rig, w, h, scale, mat.Out,
                (in TexelRig.Cell c, int x, int y, int s, System.Func<int, int, TexelRig.Part> at) => ColourOf(c, x, y, s, mat, p),
                new Vector2(PivotX, PivotY), (px, cw, ch, sc, filled) => Light(px, cw, ch, sc, filled, fx, mat),
                // wind has no outline; stone, cloud and iron do
                outlines: part => mat.Iron || part.Slot != (int)Slot.Band);
        }

        static void Light(Color[] px, int w, int h, float scale, System.Func<int, int, bool> filled, Lights fx, Mat mat)
        {
            var a = mat.Acc;
            void Put(int x, int y, Color c, float alpha) { if (x >= 0 && y >= 0 && x < w && y < h) TexelRig.Put(px, w, x, y, c, alpha); }

            // short arcs crawling across the cloud's face
            foreach (var (c, r, seed) in fx.Arcs)
            {
                float ang = Hash(seed, 3) * Mathf.PI * 2f, rr = r * (0.55f + Hash(seed, 9) * 0.35f);
                for (int k = 0; k < 7; k++)
                {
                    float x = c.x + Mathf.Cos(ang) * rr, y = c.y + Mathf.Sin(ang) * rr * 0.9f;
                    Put(Mathf.FloorToInt(x * scale), Mathf.FloorToInt(y * scale), k % 3 != 0 ? a[2] : a[3], 1f);
                    ang += 0.32f + (Hash(seed, k) - 0.5f) * 0.4f;
                    rr += (Hash(k, seed) - 0.5f) * 1.6f;
                }
            }
            foreach (var s in fx.Smoke)
            {
                int x = Mathf.FloorToInt(s.x * scale), y = Mathf.FloorToInt(s.y * scale);
                foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
                    if (x + dx < w && y + dy < h && x + dx >= 0 && y + dy >= 0 && !filled(x + dx, y + dy)) Put(x + dx, y + dy, Smoke, Smoke.a);
            }
            if (fx.Halo is not { } halo) return;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (filled(x, y)) continue;
                float d = Vector2.Distance(new Vector2((x + 0.5f) / scale, (y + 0.5f) / scale), halo.c);
                if (d <= halo.r && (x + y) % 2 == 0) Put(x, y, a[2], 110f / 255f);
            }
        }

        static Color? ColourOf(in TexelRig.Cell c, int x, int y, int s, Mat mat, in Pose p)
        {
            var part = c.Part;
            var hit = c.Hit;
            var a = mat.Acc;
            switch ((Slot)part.Slot)
            {
                case Slot.Eye:
                {
                    float r2 = hit.Nx * hit.Nx + hit.Ny * hit.Ny;
                    int g = part.Level;
                    if (g == 0) return r2 < 0.5f ? a[1] : a[0];
                    return r2 < 0.25f + g * 0.12f ? a[3] : r2 < 0.7f ? a[2] : a[1];
                }
                case Slot.Band:
                {
                    // wind: long streaks with gaps running round the band, turning with its phase
                    float deg = (Mathf.Atan2(hit.Ny, hit.Nx) * Mathf.Rad2Deg + 360f + part.Extra) % 360f;
                    if (mat.Iron) return Mathf.FloorToInt(deg / 30f) % 3 == 0 ? (mat.Rivet ?? mat.Band) : mat.Base[2];
                    if (Mathf.FloorToInt(deg / 36f) % 2 == 1) return null;
                    var band = mat.Band;
                    band.a = (deg % 36f) < 14f ? 230f / 255f : 150f / 255f;
                    return band;
                }
                case Slot.Stone:
                    return mat.Stone[Mathf.Min(3, TexelRig.BaseShade(hit.Nx, hit.Ny) + 1)];
                case Slot.Cloud:
                {
                    float r2 = hit.Nx * hit.Nx + hit.Ny * hit.Ny;
                    if (mat.Iron)
                    {
                        // an iron storm: a cast sphere, the lightning run through it like piping
                        int g = p.Glow;
                        float r = Mathf.Sqrt(r2);
                        if (Mathf.Abs(r - 0.82f) < 0.07f || (Mathf.Abs(hit.Ny + hit.Nx * 0.3f) < 0.07f && r > 0.45f))
                            return g == 0 ? a[0] : a[Mathf.Min(3, g + 1)];
                        return mat.Cloud[TexelRig.BaseShade(hit.Nx, hit.Ny)];
                    }
                    // a cloud: billows of light and dark, brighter where the charge crawls
                    float n = Hash(Mathf.FloorToInt(x / 3f), Mathf.FloorToInt(y / 3f));
                    int cs = Mathf.Clamp(TexelRig.BaseShade(hit.Nx, hit.Ny) + (n < 0.25f ? -1 : n > 0.8f ? 1 : 0), 0, 3);
                    if (p.Glow >= 2 && n > 0.9f) return a[1];
                    return mat.Cloud[cs];
                }
            }

            // the plinth
            var ramp = mat.Base;
            if (part.Decor == (int)Decor.Plinth)
            {
                // runes cut in its face, lit with the charge
                int rx = x - 13, ry = Mathf.FloorToInt(c.Py) - 2;
                bool rune = (rx == 1 && ry >= 0 && ry <= 3) || (rx == 3 && ry == 2) || (rx == 5 && ry >= 1 && ry <= 3)
                            || (rx == 7 && (ry == 0 || ry == 3)) || (rx == 2 && ry == 0);
                if (!mat.Iron && rune) return p.Glow == 0 ? ramp[0] : a[Mathf.Min(3, p.Glow)];
                if (mat.Iron && Mathf.Abs(hit.Nx) > 0.75f && Mathf.FloorToInt(c.Py) % 3 == 1 && mat.Rivet.HasValue) return mat.Rivet.Value;
                if (mat.Iron && Mathf.Abs(c.Py - 3.5f) < 0.5f) return p.Glow == 0 ? a[0] : a[Mathf.Min(3, p.Glow + 1)]; // a line of piping
                if (!mat.Iron && Hash(x, y) < 0.15f) return ramp[Mathf.Max(0, s - 1)];
            }
            return ramp[s];
        }
    }
}
