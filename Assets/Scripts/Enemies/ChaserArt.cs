using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>The Chaser's frames, in the order <see cref="ChaserArt.For"/> returns them.</summary>
    public enum ChaserFrame { Walk0, Walk1, Walk2, Walk3, Idle, Raise, Cocked, Strike, Recover, StillLit, Riposte }

    /// <summary>
    /// The Chaser as a GRAVE BRUTE (the user's sign-off, 2026-10-18, from the mock-up at
    /// https://claude.ai/artifact/1xKKCcSWF8hmfxfgSKUQ5q): a hunched, broad-backed thing whose
    /// weapon arm has grown into a cleaver, every mass of it leaning toward the player.
    ///
    /// THE BODY SAYS THE ATTACK. Pressure kind: all its weight low and forward, the head sunk under
    /// a high hump, a heavy walk that drops a texel on every footfall. The 0.35 s telegraph REARS
    /// it back with the cleaver over the shoulder - the silhouette at its tallest - while the eye
    /// and a vein in the blade light, hottest on the COCKED frame (the tell). The hit is a lunge
    /// step and a chop with a smear in the Chaser's magenta; the recovery plants the blade in the
    /// floor and puts every light out (the safe window is the dark frame). An elite's riposte is
    /// told by the vein STAYING LIT after the first hit, then a backhand.
    ///
    /// ONE RIG, TWO MATERIALS: Construct is ROTTEN (grave-flesh, a cleaver of its own bone),
    /// Automaton is IRONBOUND (rusting plate, a visor slit, a forged cleaver). Same silhouette, so
    /// a player who learned one has learned both.
    ///
    /// AT THE ARENA'S OWN TEXEL SIZE: <see cref="TexelsPerUnit"/> is the player's body density
    /// (37.5) DIVIDED by the arena's visual scale (4/3) - the player's texels are drawn 4/3 larger
    /// there, so fewer of them fit a unit - so a brute's texel is the player's texel and
    /// the camera's whole-pixel zoom holds for both. Drawn at scale 1, never normalised - an elite
    /// is the same rig RASTERISED 1.2x larger (<see cref="EliteScale"/>), so it stays on the grid
    /// instead of being stretched off it.
    ///
    /// The rig is authored in small units and drawn <see cref="RigScale"/>x through
    /// <see cref="TexelRig"/>. Ported from the mock-up's JavaScript line for line - change both
    /// or neither.
    /// </summary>
    public static class ChaserArt
    {
        public const float TexelsPerUnit = TexelRig.TexelsPerUnit;
        const float RigScale = 1.25f;
        const float EliteScale = 1.2f;
        const int W = 48, H = 40;

        /// <summary>The body line the brute mirrors about, in authored units.</summary>
        const float PivotX = 19.5f, PivotY = 13f;

        // ---------------------------------------------------------------- palettes

        enum Slot { Skin, Bone, Cloth, Acc, Biome }

        sealed class Mat
        {
            public Color[] Skin, Bone, Cloth, Rust;
            public Color Out;
            public bool Iron;
        }

        static readonly Mat Rotten = new()
        {
            Skin = Hex("#2c332d", "#4a5443", "#6c765d", "#959b7b"),
            Bone = Hex("#665c4b", "#94876b", "#c2b692", "#e9dfc1"),
            Cloth = Hex("#251b18", "#3a2a24", "#523c2e", "#6a513e"),
            Out = Hex("#0e0c10")[0],
        };

        static readonly Mat Ironbound = new()
        {
            Skin = Hex("#24262c", "#3d414a", "#626874", "#949ba6"),
            Bone = Hex("#434850", "#737b86", "#adb5bf", "#e1e6ec"),
            Cloth = Hex("#1c1c20", "#2c2d32", "#42434a", "#5b5c62"),
            Rust = Hex("#5c3220", "#82492a"),
            Out = Hex("#0c0b0f")[0],
            Iron = true,
        };

        /// <summary>The Chaser's magenta (its old placeholder tint), dim to hot - the eye, the
        /// blade's vein, the smear.</summary>
        static readonly Color[] Acc = Hex("#4c1d2f", "#9a3358", "#f2598c", "#ffc6da");

        /// <summary>Colours the biome weathering never touches - the glows and tells (CreatureWeathering).</summary>
        internal static Color[] Reserved => _reserved ??= Acc;
        static Color[] _reserved;

        static Color[] Hex(params string[] hex) => TexelRig.Hex(hex);

        // ---------------------------------------------------------------- poses

        struct Pose
        {
            public float Bx, By, Hx, Hy, U, F, Fu, Ff, Glow, Eye;
            public Vector2 FootN, FootF;
            public int Smear; // a ChaserFrame the swing came from, or -1
        }

        static Pose P(float bx = 0, float by = 0, float hx = 0, float hy = 0, float u = -52, float f = -18,
                      float fu = -75, float ff = -45, float footNx = 24, float footNy = 1.5f,
                      float footFx = 15, float footFy = 1.5f, int glow = 0, int eye = 1, int smear = -1)
            => new()
            {
                Bx = bx, By = by, Hx = hx, Hy = hy, U = u, F = f, Fu = fu, Ff = ff, Glow = glow, Eye = eye,
                FootN = new Vector2(footNx, footNy), FootF = new Vector2(footFx, footFy), Smear = smear,
            };

        // u/f: the weapon arm's upper arm and forearm angles (0 = toward the player, 90 = up);
        // fu/ff the far arm's. Indexed by ChaserFrame.
        static readonly Pose[] Poses =
        {
            P(footNx: 26.5f, footFx: 13, u: -46, f: -14, fu: -85),                                  // Walk0 contact
            P(footNx: 23, footNy: 2.8f, footFx: 16.5f, by: 1, u: -52, f: -20, fu: -76, hy: 0.4f),     // Walk1 passing
            P(footNx: 19.5f, footFx: 20, u: -58, f: -26, fu: -66),                                    // Walk2 contact
            P(footNx: 23, footFx: 16.5f, footFy: 2.8f, by: 1, u: -52, f: -20, fu: -76, hy: 0.4f),     // Walk3 passing
            P(),                                                                                      // Idle
            P(bx: -1, by: 1, hx: -0.6f, hy: 1, u: 110, f: 150, fu: -95, ff: -70, footNx: 25, glow: 1, eye: 2),
            P(bx: -1.5f, by: 1.5f, hx: -1, hy: 1.4f, u: 128, f: 178, fu: -100, ff: -80, footNx: 25.5f, glow: 3, eye: 3),
            P(bx: 2, by: -1.5f, hx: 1, hy: -1.2f, u: -22, f: -62, fu: -60, ff: -20, footNx: 28.5f, footFx: 14,
              glow: 2, eye: 3, smear: (int)ChaserFrame.Cocked),
            P(bx: 1, by: -2, hx: 1.2f, hy: -2, u: -78, f: -96, fu: -88, ff: -70, footNx: 27, glow: 0, eye: 0),
            P(bx: 2, by: -1.5f, hx: 1, hy: -1.2f, u: -22, f: -80, fu: -60, ff: -20, footNx: 28.5f, footFx: 14, glow: 3, eye: 3),
            P(bx: 1.5f, hx: 0.5f, hy: 0.5f, u: 30, f: 58, fu: -50, ff: -10, footNx: 28, footFx: 14,
              glow: 2, eye: 3, smear: (int)ChaserFrame.StillLit),
        };

        // ---------------------------------------------------------------- cache

        static readonly Dictionary<int, Sprite[]> _cache = new();

        /// <summary>Every frame of one look and tier, indexed by <see cref="ChaserFrame"/>.</summary>
        public static Sprite[] For(EnemyLook look, bool elite) => For(look, elite, Art.Biomes.BiomeKind.Tower);

        /// <summary>The same, with the place's ACCENT built into the rig (CreatureWeathering lays
        /// the rest over it): a bracket fungus on the hump's back flank in the forest, a shelf of
        /// salt cubes there on the flats, two torn streamers off the hump in the sky ruin. Parts
        /// of the rig, so the body's own pose carries them and the arm and cleaver draw over them.</summary>
        public static Sprite[] For(EnemyLook look, bool elite, Art.Biomes.BiomeKind biome)
        {
            int key = (look == EnemyLook.Automaton ? 1 : 0) | (elite ? 2 : 0) | ((int)biome << 2) | (Art.Biomes.Nigredo.Shards << 5);
            if (_cache.TryGetValue(key, out var hit) && hit != null && hit.Length > 0 && hit[0] != null) return hit;

            var mat = look == EnemyLook.Automaton ? Ironbound : Rotten;
            var frames = new Sprite[Poses.Length];
            for (int i = 0; i < frames.Length; i++) frames[i] = Render(i, mat, elite, biome);
            _cache[key] = frames;
            return frames;
        }

        // ---------------------------------------------------------------- the rig

        enum Decor { None, Hump, Chest, Belly, Cloth, Head, Jaw, Bevor, Limb, Blade, Fungus, SaltShelf, Streamer }

        static Vector2 Pt(Vector2 p, float a, float l) => TexelRig.Pt(p, a, l);
        static float Rad(float d) => TexelRig.Rad(d);

        /// <summary>The cleaver: a spine on one side of its line, a ragged, bellied edge on the
        /// other, hooked to a point. The edge is on the side that LEADS the chop.</summary>
        static TexelRig.Shape Blade(Vector2 b, float ang, float len) => (float x, float y, out TexelRig.Hit h) =>
        {
            h = default;
            if (y < 0.9f) return false; // a planted blade goes INTO the floor
            var d = new Vector2(Mathf.Cos(Rad(ang)), Mathf.Sin(Rad(ang)));
            var n = new Vector2(d.y, -d.x);
            float rx = x - b.x, ry = y - b.y;
            float t = rx * d.x + ry * d.y, s = rx * n.x + ry * n.y;
            if (t < 0f || t > len) return false;
            float k = t / len;
            float e = 1.5f + 2.5f * Mathf.Sin(Mathf.PI * Mathf.Min(k * 0.95f, 1f) * 0.85f), sp = 1.05f;
            if (Mathf.FloorToInt(t) % 3 == 2 && k > 0.15f && k < 0.85f) e -= 0.9f;
            if (k > 0.8f) { float q = (k - 0.8f) / 0.2f; e *= 1f - q * 0.85f; sp *= 1f - q; }
            h = new TexelRig.Hit { T = t, S = s, E = e, K = k };
            return s >= -sp && s <= e;
        };

        static readonly Vector2 ShoulderN = new(22.5f, 19f);

        static void ArmAt(Vector2 sh, float u, float f, out Vector2 el, out Vector2 wr)
        {
            el = Pt(sh, u, 6.5f);
            wr = Pt(el, f, 6f);
        }

        static TexelRig.Rig Build(in Pose p, Mat mat, bool elite, Art.Biomes.BiomeKind biome, int frame)
        {
            var rig = new TexelRig.Rig();
            void Add(float order, Slot slot, TexelRig.Shape test, bool far = false, bool sep = false,
                     Decor decor = Decor.None, float level = 0)
                => rig.Add(order, (int)slot, test, far, sep, (int)decor, level);
            TexelRig.Shape Ellipse(float cx, float cy, float rx, float ry) => TexelRig.Ellipse(cx, cy, rx, ry);
            TexelRig.Shape Taper(Vector2 a, Vector2 b, float ra, float rb) => TexelRig.Taper(a, b, ra, rb);
            Vector2 Ik(Vector2 hip, Vector2 foot, float l1, float l2) => TexelRig.Ik(hip, foot, l1, l2);

            float bx = p.Bx, by = p.By;
            bool iron = mat.Iron;

            // far leg and far arm, a shade darker
            var hipF = new Vector2(17 + bx, 11 + by);
            var kneeF = Ik(hipF, p.FootF, 5.2f, 5.2f);
            Add(1f, Slot.Skin, Taper(hipF, kneeF, 2.6f, 2.2f), far: true);
            Add(1.1f, Slot.Skin, Taper(kneeF, p.FootF, 2.1f, 1.7f), far: true);
            Add(1.2f, Slot.Skin, Ellipse(p.FootF.x + 1.2f, p.FootF.y - 0.2f, 2.4f, 1.2f), far: true);
            var shF = new Vector2(16.5f + bx, 19.5f + by);
            var elF = Pt(shF, p.Fu, 5.5f);
            var hF = Pt(elF, p.Ff, 5f);
            Add(2f, Slot.Skin, Taper(shF, elF, 2.1f, 1.8f), far: true);
            Add(2.1f, Slot.Skin, Taper(elF, hF, 1.8f, 1.5f), far: true);
            foreach (float da in new[] { -28f, 0f, 28f })
                Add(2.2f, Slot.Bone, Taper(hF, Pt(hF, p.Ff + da, 2.4f), 0.8f, 0.15f), far: true);

            // the body: a high hump, a deep chest, a hanging belly, rags
            float humpX = 15.5f + bx, humpY = 21f + by, humpRx = 6f, humpRy = 5.5f;
            Add(3f, Slot.Skin, Ellipse(humpX, humpY, humpRx, humpRy), decor: Decor.Hump);
            Add(4f, Slot.Skin, Ellipse(19.5f + bx, 16.5f + by, 8f, 6.5f), decor: Decor.Chest);
            Add(5f, Slot.Skin, Ellipse(21.5f + bx, 12.5f + by, 5.5f, 3.6f), decor: Decor.Belly);
            var rag = new[] { new Vector2(16.5f, 11.6f), new Vector2(25f, 11.6f), new Vector2(23.6f, 6.4f),
                              new Vector2(21.2f, 7.6f), new Vector2(19.2f, 5.8f), new Vector2(17f, 8f) };
            for (int i = 0; i < rag.Length; i++) rag[i] += new Vector2(bx, by);
            Add(6f, Slot.Cloth, TexelRig.Poly(rag), decor: Decor.Cloth);
            foreach (float a in elite ? new[] { 100f, 120f, 140f, 160f } : new[] { 105f, 128f, 150f })
            {
                var b0 = new Vector2(humpX + Mathf.Cos(Rad(a)) * humpRx * 0.8f, humpY + Mathf.Sin(Rad(a)) * humpRy * 0.8f);
                Add(7f, Slot.Bone, Taper(b0, Pt(b0, a + 8f, elite ? 4.8f : 3.3f), 1.25f, 0.2f));
            }

            // THE PLACE'S ACCENT, on the hump's back flank (below the spines, which stand at
            // 105-160 degrees) or trailing off its top - placed off the hump, so it rides the pose.
            if (biome == Art.Biomes.BiomeKind.Forest || biome == Art.Biomes.BiomeKind.SaltFlats)
            {
                var d = biome == Art.Biomes.BiomeKind.Forest ? Decor.Fungus : Decor.SaltShelf;
                Add(7.5f, Slot.Biome, Ellipse(humpX - humpRx - 1.1f, humpY + 1.2f, 3.1f, 1.45f), decor: d);
                Add(7.5f, Slot.Biome, Ellipse(humpX - humpRx - 0.4f, humpY - 2.3f, 2.3f, 1.15f), decor: d);
            }
            // the Nigredo's shards of the Stone, out of the hump's crown
            CreatureAccents.StoneShards(rig, 7.6f, new Vector2(humpX - 1.2f, humpY + humpRy - 0.8f), 0.8f);
            if (biome == Art.Biomes.BiomeKind.SkyRuin)
            {
                // Behind the hump (order 2.5), so it grows from under it; the wind's phase moves
                // with the walk's frames and holds through the attack's.
                float ph = frame < 4 ? frame * 1.57f : frame * 0.9f;
                var root = new Vector2(humpX - humpRx * 0.85f, humpY + humpRy * 0.45f);
                for (int r = 0; r < 2; r++)
                {
                    int segs = r == 0 ? 4 : 3;
                    var prev = root + new Vector2(0f, r * -2.2f);
                    for (int k = 1; k <= segs; k++)
                    {
                        var next = root + new Vector2(-k * 1.9f, r * -2.2f + Mathf.Sin(k * 0.9f + ph + r * 1.7f) * 1.0f - k * 0.3f);
                        Add(2.5f, Slot.Biome, Taper(prev, next, 1.35f - k * 0.12f, 1.25f - k * 0.16f), decor: Decor.Streamer);
                        prev = next;
                    }
                }
            }

            // the head, sunk low and forward
            float hx = 28.6f + bx + p.Hx, hy = 19.6f + by + p.Hy;
            Add(8f, Slot.Skin, Ellipse(hx, hy, 3.3f, 2.9f), sep: true, decor: Decor.Head);
            Add(9f, Slot.Skin, Ellipse(hx + 1.6f, hy - 2.2f, 2.7f, 1.5f), sep: true, decor: iron ? Decor.Bevor : Decor.Jaw);
            if (elite)
            {
                Add(9.5f, Slot.Bone, Taper(new Vector2(hx - 1f, hy + 2.2f), new Vector2(hx - 3.6f, hy + 6.2f), 1.15f, 0.2f));
                Add(9.5f, Slot.Bone, Taper(new Vector2(hx + 1.2f, hy + 2.6f), new Vector2(hx + 2.6f, hy + 6.4f), 1.0f, 0.2f));
            }
            if (iron) Add(10f, Slot.Acc, Ellipse(hx + 1.4f, hy + 0.4f, 2.2f, 0.55f), level: p.Eye);
            else
            {
                Add(10f, Slot.Acc, Ellipse(hx + 2f, hy + 0.5f, 0.9f, 0.75f), level: p.Eye);
                if (elite) Add(10f, Slot.Acc, Ellipse(hx + 0.3f, hy + 1.1f, 0.6f, 0.6f), level: p.Eye);
            }

            // near leg
            var hipN = new Vector2(21 + bx, 10.5f + by);
            var kneeN = Ik(hipN, p.FootN, 5.2f, 5.2f);
            Add(11f, Slot.Skin, Taper(hipN, kneeN, 2.9f, 2.4f), sep: true, decor: Decor.Limb);
            Add(11.1f, Slot.Skin, Taper(kneeN, p.FootN, 2.3f, 1.9f), sep: true, decor: Decor.Limb);
            Add(11.2f, Slot.Skin, Ellipse(p.FootN.x + 1.5f, p.FootN.y - 0.2f, 2.8f, 1.3f), sep: true);

            // the weapon arm: swollen toward the wrist, the cleaver growing out of it
            var shN = ShoulderN + new Vector2(bx, by);
            ArmAt(shN, p.U, p.F, out var el, out var wr);
            Add(12f, Slot.Skin, Taper(shN, el, 3.1f, 2.5f), sep: true, decor: Decor.Limb);
            Add(13f, Slot.Skin, Taper(el, wr, 2.6f, 3.2f), sep: true, decor: Decor.Limb);
            Add(14f, Slot.Bone, Blade(wr, p.F - 10f, elite ? 11f : 10f), decor: Decor.Blade, level: p.Glow);
            return rig;
        }

        /// <summary>The swing's path, sampled between the pose it came from and the one it
        /// reached: (point, how far through the swing).</summary>
        static List<(Vector2 q, float k)> SmearOf(in Pose p, bool elite)
        {
            var from = Poses[p.Smear];
            var shN = ShoulderN + new Vector2(p.Bx, p.By);
            float len = elite ? 11f : 10f;
            var pts = new List<(Vector2, float)>();
            for (int i = 0; i <= 48; i++)
            {
                float k = i / 48f, u = Mathf.Lerp(from.U, p.U, k), f = Mathf.Lerp(from.F, p.F, k);
                ArmAt(shN, u, f, out _, out var wr);
                foreach (float along in new[] { 0.35f, 0.5f, 0.65f, 0.8f, 0.92f, 1f })
                    pts.Add((Pt(wr, f - 10f, len * along), k));
            }
            return pts;
        }

        // ---------------------------------------------------------------- raster

        static Sprite Render(int frame, Mat mat, bool elite, Art.Biomes.BiomeKind biome)
        {
            var p = Poses[frame];
            float scale = RigScale * (elite ? EliteScale : 1f);
            int w = Mathf.RoundToInt(W * scale), h = Mathf.RoundToInt(H * scale);
            w += w & 1; // an even width mirrors onto a texel boundary
            CreatureAccents.Shards = Art.Biomes.Nigredo.Shards;
            var rig = Build(p, mat, elite, biome, frame);
            CreatureAccents.Shards = 0;

            TexelRig.Overlay smear = null;
            if (p.Smear >= 0)
            {
                var pts = SmearOf(p, elite);
                smear = (px, cw, ch, sc, filled) =>
                {
                    for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                    {
                        if (filled(x, y)) continue;
                        float best = -1f;
                        foreach (var (q, k) in pts)
                        {
                            float dx = (x + 0.5f) / sc - q.x, dy = (y + 0.5f) / sc - q.y;
                            if (dx * dx + dy * dy <= 1.6f) best = Mathf.Max(best, k);
                        }
                        if (best >= 0f && best < 0.97f)
                            TexelRig.Put(px, cw, x, y, best > 0.6f ? Acc[3] : Acc[2], Mathf.Round(60f + 140f * best) / 255f);
                    }
                };
            }

            string name = $"enemy.chaser.{(mat.Iron ? "iron" : "rotten")}{(elite ? ".elite" : "")}{(biome != Art.Biomes.BiomeKind.Tower ? "." + biome : "")}.{(ChaserFrame)frame}";
            return TexelRig.Rasterise(name, rig, w, h, scale, mat.Out,
                (in TexelRig.Cell c, int x, int y, int s) => ColourOf(c, x, y, mat, p, elite, s),
                new Vector2(PivotX, PivotY), smear);
        }

        static Color ColourOf(in TexelRig.Cell c, int x, int y, Mat mat, in Pose p, bool elite, int s)
        {
            var part = c.Part;
            var hit = c.Hit;
            if (part.Slot == (int)Slot.Acc) return Acc[part.Level];
            if (part.Slot == (int)Slot.Biome)
                switch ((Decor)part.Decor)
                {
                    case Decor.Fungus:   // a bracket: lit cap on top, the gills dark beneath
                        return hit.Ny > 0.35f ? CreatureWeathering.Fungus[3] : hit.Ny > -0.1f ? CreatureWeathering.Fungus[2]
                             : hit.Ny > -0.55f ? CreatureWeathering.Fungus[1] : CreatureWeathering.Fungus[0];
                    case Decor.SaltShelf:   // cubes: tops alternating, a pink face, shadow under
                        return hit.Ny > 0.2f ? CreatureWeathering.SaltCubes[(x >> 1) % 2 == 0 ? 3 : 2]
                             : hit.Ny > -0.5f ? CreatureWeathering.SaltCubes[1] : CreatureWeathering.SaltCubes[0];
                    default:                // torn cloth, shaded with the light
                        return CreatureWeathering.Streamer[s];
                }
            var ramp = (Slot)part.Slot switch { Slot.Bone => mat.Bone, Slot.Cloth => mat.Cloth, _ => mat.Skin };

            bool iron = mat.Iron;
            switch ((Decor)part.Decor)
            {
                case Decor.Blade:
                    if (hit.T < 1.4f) return ramp[1];
                    if (Mathf.Abs(hit.S - hit.E * 0.42f) < 0.5f && hit.K > 0.14f && hit.K < 0.8f) return Acc[part.Level];
                    return hit.S > hit.E - 0.95f ? ramp[3] : hit.S < -0.25f ? ramp[1] : ramp[2];
                case Decor.Chest:
                    if (iron)
                    {
                        if (Mathf.Abs(hit.Ny - 0.05f) < 0.13f) return ramp[0];               // the plate seam
                        if (Mathf.Abs(hit.Ny - 0.3f) < 0.12f && x % 3 == 0) return ramp[3];   // rivets
                    }
                    else if (hit.Nx > 0.35f && hit.Nx < 0.8f && hit.Ny > -0.5f && hit.Ny < 0.2f &&
                             Mathf.FloorToInt(c.Py) % 3 == 0 && x % 2 == 0)
                        return ramp[0];                                                      // ribs
                    break;
                case Decor.Hump:
                    if (iron && mat.Rust != null && TexelRig.Hash(x, y) < 0.16f) return mat.Rust[s > 1 ? 1 : 0];
                    if (elite && hit.Nx < -0.1f && hit.Ny > -0.1f && (x + 2 * y) % 7 == 0) return Acc[Mathf.Max(1, (int)p.Eye)];
                    break;
                case Decor.Belly:
                    if (iron && Mathf.Abs(hit.Ny + 0.1f) < 0.18f) return ramp[0];
                    break;
                case Decor.Limb:
                    if (iron && hit.HasT && (Mathf.Abs(hit.T - 0.34f) < 0.07f || Mathf.Abs(hit.T - 0.68f) < 0.07f)) return ramp[0];
                    if (iron && mat.Rust != null && TexelRig.Hash(x + 9, y) < 0.08f) return mat.Rust[0];
                    break;
                case Decor.Jaw:
                    if (hit.Ny > 0.1f && x % 2 == 0) return mat.Bone[3]; // teeth along the jaw's top
                    break;
                case Decor.Head:
                    return ramp[Mathf.Min(3, s + 1)]; // a tone up, so it parts from the shoulder's mass
                case Decor.Bevor:
                    if (Mathf.Abs(hit.Ny - 0.1f) < 0.2f) return ramp[0];
                    break;
                case Decor.Cloth:
                    if (iron) return ramp[(x + y) % 2 != 0 ? 1 : 2]; // mail
                    if (TexelRig.Hash(x, y + 3) < 0.15f) return ramp[0];
                    return ramp[c.Py > 9f + p.By ? 2 : 1];
            }
            return ramp[s];
        }
    }
}
