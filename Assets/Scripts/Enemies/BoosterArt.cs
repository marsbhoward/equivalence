using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>The Booster's frames, in the order <see cref="BoosterArt.For"/> returns them.</summary>
    public enum BoosterFrame { Idle0, Idle1, Idle2, Idle3, Breath0, Breath1, Roar0, Roar1, Spent0, Spent1, Jolt }

    /// <summary>
    /// The Booster as the GREEN LION (the user's sign-off, 2026-10-19, from the mock-up at
    /// https://claude.ai/artifact/DXMhUhZrs7M4JopKNiwEY8): a gaunt lion lying couchant under a
    /// ragged green-black mane - the alchemists' Green Lion, without the sun (the user's call).
    ///
    /// THE POWER-UP IS A ROAR (the user's call). Through the channel it rises onto its forepaws
    /// and DRAWS BREATH - head low, chest swelling, the veins in its hide filling with green
    /// (BREATH0); for the last 30% its head comes up, its jaws part and its mane bristles
    /// (BREATH1, the tell). ROAR0/1 are the moment the power lands - jaws wide, throat lit, the
    /// first waves leaving them (the rest roll out to the ally as <see cref="BoosterRoar"/>).
    /// While its ally lives it lies SPENT, head on its paws and eyes dark; a flinch JOLTS it.
    /// No line is drawn to the ally (the user's call) - its facing says who.
    ///
    /// THE ELITE (the user's call: it boosts two at a time) is 1.2x, with a wilder, spikier mane
    /// and a heavier tail tuft. ONE RIG, TWO MATERIALS: Construct is GAUNT (olive hide over the
    /// ribs, dark veins), Automaton is IRONBOUND (cast iron, the veins become piping, a ruff of
    /// iron blades, rivets on the haunch). Ported from the mock-up's JavaScript line for line -
    /// change both or neither.
    /// </summary>
    public static class BoosterArt
    {
        const float RigScale = 1.1f, EliteScale = 1.2f;
        const int W = 52, H = 40;
        const float PivotX = 24f, PivotY = 7f;

        enum Slot { Body, Mane, Fang, Maw, Nose, Socket, Eye }
        enum Decor { None, Tail, Tuft, Leg, Paw, Thigh, Torso, Mane, Jaw, Ear, Cranium, Muzzle, Brow }

        sealed class Mat
        {
            public Color[] Hide, Mane;
            public Color Maw, Fang, Nose, Rivet, Out;
            public bool Iron;
        }

        static readonly Mat Gaunt = new()
        {
            Hide = TexelRig.Hex("#231f17", "#3b3324", "#5c4f36", "#837153"),
            Mane = TexelRig.Hex("#0f1510", "#1b2619", "#2b3c26", "#3f5634"),
            Maw = TexelRig.Hex("#140c0a")[0],
            Fang = TexelRig.Hex("#d8ccb0")[0],
            Nose = TexelRig.Hex("#1a1210")[0],
            Out = TexelRig.Hex("#0b0a08")[0],
        };

        static readonly Mat Ironbound = new()
        {
            Hide = TexelRig.Hex("#1f2126", "#363a42", "#565c67", "#878e99"),
            Mane = TexelRig.Hex("#17191d", "#2a2d33", "#434852", "#646a76"),
            Maw = TexelRig.Hex("#100f12")[0],
            Fang = TexelRig.Hex("#a3abb7")[0],
            Nose = TexelRig.Hex("#121316")[0],
            Rivet = TexelRig.Hex("#b6bdc7")[0],
            Out = TexelRig.Hex("#0a0a0c")[0],
            Iron = true,
        };

        static readonly Color Socket = TexelRig.Hex("#0c0f0a")[0];

        /// <summary>The roar's green, dim to hot - the Booster's own pale green.</summary>
        public static readonly Color[] Accent = TexelRig.Hex("#163a20", "#2e8a44", "#8cf28c", "#e0ffe0");

        // veins (Ironbound: the piping), each in its part's own frame, so they ride the body
        static readonly Vector2[][] TorsoVeins =
        {
            new[] { V(-0.95f, 0.3f), V(-0.4f, 0.66f), V(0.3f, 0.7f), V(0.92f, 0.45f) },
            new[] { V(-0.1f, 0.68f), V(0.05f, 0.2f) },
        };
        static readonly Vector2[][] ThighVeins = { new[] { V(0.35f, 0.8f), V(-0.15f, 0.25f), V(0.1f, -0.45f) } };
        static readonly Vector2[][] CraniumVeins = { new[] { V(-0.9f, 0.45f), V(-0.25f, 0.62f), V(0.55f, 0.25f) } };

        static Vector2 V(float x, float y) => new(x, y);

        // ---------------------------------------------------------------- poses

        sealed class Pose
        {
            public float Rise, Bob, HeadRot, Jaw, Chest, Bristle, Tail;
            public int Eye = 1, Vein, Roar;
            public Vector2 Head;
        }

        // rise: 0 lying .. 1 up on its forepaws; head/headRot/jaw: the head; chest: the breath
        // 0..1; bristle: the mane 0..1; eye/vein: 0..3 green light; tail: the tip's flick; roar:
        // 0 none, 1/2 the waves leaving the jaws. Indexed by BoosterFrame.
        static readonly Pose[] Poses =
        {
            new(),
            new() { Bob = 0.3f, Tail = 1 },
            new() { Bob = 0.5f, Tail = 2, HeadRot = 2 },
            new() { Bob = 0.2f, Tail = 1 },
            new() { Rise = 0.6f, Head = V(0.2f, -0.6f), HeadRot = -8, Chest = 0.6f, Bristle = 0.15f, Eye = 2, Vein = 1 },
            new() { Rise = 1, Head = V(-0.4f, 0.6f), HeadRot = 14, Jaw = 12, Chest = 1, Bristle = 0.3f, Eye = 2, Vein = 2, Tail = 2 },
            new() { Rise = 1, Head = V(1.4f, 1.2f), HeadRot = 18, Jaw = 44, Chest = 0.3f, Bristle = 0.55f, Eye = 3, Vein = 2, Tail = 2, Roar = 1 },
            new() { Rise = 1, Head = V(1.5f, 1.3f), HeadRot = 20, Jaw = 48, Chest = 0.2f, Bristle = 0.6f, Eye = 3, Vein = 2, Tail = 1, Roar = 2 },
            new() { Head = V(0.5f, -3.2f), HeadRot = -14, Eye = 0 },
            new() { Head = V(0.5f, -3.4f), HeadRot = -15, Eye = 0, Bob = 0.3f, Tail = 1 },
            new() { Rise = 0.3f, Head = V(-1.4f, 0.8f), HeadRot = -24, Jaw = 20, Chest = 0.2f, Eye = 0 },
        };

        // ---------------------------------------------------------------- cache

        static readonly Dictionary<int, Sprite[]> _cache = new();

        /// <summary>Every frame of one look and tier, indexed by <see cref="BoosterFrame"/>.</summary>
        public static Sprite[] For(EnemyLook look, bool elite)
        {
            int key = (look == EnemyLook.Automaton ? 1 : 0) | (elite ? 2 : 0);
            if (_cache.TryGetValue(key, out var hit) && hit != null && hit.Length > 0 && hit[0] != null) return hit;

            var mat = look == EnemyLook.Automaton ? Ironbound : Gaunt;
            var frames = new Sprite[Poses.Length];
            for (int i = 0; i < frames.Length; i++) frames[i] = Render(i, mat, elite);
            _cache[key] = frames;
            return frames;
        }

        /// <summary>Where the roar leaves the jaws on <paramref name="frame"/>, in world units from
        /// the body's position, facing +x (mirror x when facing left).</summary>
        public static Vector2 MouthOffset(BoosterFrame frame, bool elite)
        {
            float scale = (elite ? EliteScale : 1f) * RigScale;
            var m = Build(Poses[(int)frame], elite, out _);
            var pivot = new Vector2(Mathf.Round(PivotX * scale), Mathf.Round(PivotY * scale));
            return (m * scale - pivot) / TexelRig.TexelsPerUnit;
        }

        // ---------------------------------------------------------------- the rig (facing +x)

        static Vector2 Rot(Vector2 p, Vector2 o, float deg)
        {
            float c = Mathf.Cos(TexelRig.Rad(deg)), s = Mathf.Sin(TexelRig.Rad(deg)), dx = p.x - o.x, dy = p.y - o.y;
            return new Vector2(o.x + dx * c - dy * s, o.y + dx * s + dy * c);
        }

        /// <summary>A ragged ring of points: the mane.</summary>
        static TexelRig.Shape Ragged(Vector2 c, float rx, float ry, int seed, float spike)
        {
            const int n = 22;
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                float k = (i % 2 == 1 ? 1f + 0.13f * spike : 1f - 0.14f * spike) + (TexelRig.Hash(i, seed) - 0.5f) * 0.14f;
                pts[i] = V(c.x + Mathf.Cos(a) * rx * k, c.y + Mathf.Sin(a) * ry * k);
            }
            return TexelRig.Poly(pts, c.x, c.y, rx, ry);
        }

        sealed class RoarFx { public Vector2 C; public float Dir; public int Phase; }

        /// <summary>Builds the rig into <paramref name="rig"/> and returns where the mouth is.</summary>
        static Vector2 Build(Pose p, bool elite, out TexelRig.Rig rig, List<RoarFx> fx = null)
        {
            var r_ = new TexelRig.Rig();
            rig = r_;
            void Add(float order, Slot slot, TexelRig.Shape test, bool far = false, bool sep = false, Decor decor = Decor.None, float level = 0)
                => r_.Add(order, (int)slot, test, far, sep, (int)decor, level);

            float wild = elite ? 0.7f : 0f, mm = elite ? 1.1f : 1f;
            float r = p.Rise, oy = p.Bob;
            Vector2 X(float x, float y) => new(x, y + oy);
            var hp = X(14, 6.5f);
            var sh = X(29 - 2 * r, 7.5f + 6 * r);

            // the tail, along the floor behind it, the tip flicking
            var tail = new[] { X(9, 5.5f), X(5, 3), X(2.6f, 2.2f + p.Tail * 0.5f), X(1.4f, 3.2f + p.Tail * 1.5f) };
            for (int i = 0; i + 1 < tail.Length; i++)
                Add(0.5f + i * 0.01f, Slot.Body, TexelRig.Taper(tail[i], tail[i + 1], 1.3f - i * 0.3f, 1.0f - i * 0.3f), decor: Decor.Tail);
            Add(0.55f, Slot.Mane, TexelRig.Ellipse(tail[3].x, tail[3].y, elite ? 2.2f : 1.7f, elite ? 1.8f : 1.4f), decor: Decor.Tuft);

            // forelegs: out in front lying down, straight under the chest risen
            void Leg(float order, bool far, float dx)
            {
                var s = V(sh.x + 1 + dx * 0.3f, sh.y - 1.5f);
                var paw = V(Mathf.Lerp(40, 34.5f, r) + dx, 1.1f);
                var el = V(Mathf.Lerp(33, 32.5f, r) + dx * 0.6f, Mathf.Lerp(3.2f, 6.5f, r) + oy * 0.5f);
                Add(order, Slot.Body, TexelRig.Taper(s, el, 2.6f, 1.8f), far, !far, Decor.Leg);
                Add(order + 0.01f, Slot.Body, TexelRig.Taper(el, paw, 1.8f, 1.4f), far, !far, Decor.Leg);
                Add(order + 0.02f, Slot.Body, TexelRig.Ellipse(paw.x + 0.8f, paw.y + 0.1f, 2.2f, 1.2f), far, decor: Decor.Paw);
                for (int i = 0; i < 3; i++)
                    Add(order + 0.03f, Slot.Fang, TexelRig.Taper(V(paw.x + 2.2f, paw.y + 0.1f + i * 0.4f), V(paw.x + 3.1f, paw.y - 0.5f + i * 0.3f), 0.35f, 0.12f), far);
            }
            Leg(1f, true, -2.5f);
            // the haunch and the tucked hind foot
            var th = X(14.5f, 6.2f);
            Add(2f, Slot.Body, TexelRig.Ellipse(th.x, th.y, 6.6f, 5.6f, -10f), decor: Decor.Thigh);
            Add(2.1f, Slot.Body, TexelRig.Taper(X(17, 2.6f), V(23, 1.0f), 1.7f, 1.2f), sep: true, decor: Decor.Leg);
            for (int i = 0; i < 3; i++)
                Add(2.15f, Slot.Fang, TexelRig.Taper(V(24.2f, 0.8f + i * 0.4f), V(25.1f, 0.2f + i * 0.3f), 0.35f, 0.12f));
            // the body, long and low; tilted up as it rises
            var mid = Vector2.Lerp(hp, sh, 0.5f);
            float ang = Mathf.Atan2(sh.y - hp.y, sh.x - hp.x) * Mathf.Rad2Deg, len = Vector2.Distance(hp, sh);
            Add(3f, Slot.Body, TexelRig.Ellipse(mid.x + 0.5f + p.Chest * 0.6f, mid.y + 0.4f + p.Chest * 0.3f,
                len / 2f + 5f + p.Chest * 0.4f, 5.3f + p.Chest * 1.1f, ang), decor: Decor.Torso);

            // the head, posed about its centre
            var hc = V(Mathf.Lerp(35.5f, 34, r) + p.Head.x, Mathf.Lerp(14, 20, r) + p.Head.y + oy);
            float a = TexelRig.Rad(p.HeadRot), ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            Vector2 Hd(float lx, float ly) => new(hc.x + lx * ca - ly * sa, hc.y + lx * sa + ly * ca);
            Add(3.5f, Slot.Mane, Ragged(Vector2.Lerp(sh, hc, 0.5f), 7.2f * mm * (1 + p.Bristle * 0.2f), 7.0f * mm * (1 + p.Bristle * 0.2f), 3, 1 + p.Bristle + wild), decor: Decor.Mane);
            Leg(4f, false, 0f);
            Add(4.5f, Slot.Mane, Ragged(Hd(-2.0f, 0.4f), 8.6f * mm * (1 + p.Bristle * 0.15f), 9.2f * mm * (1 + p.Bristle * 0.15f), 7, 1 + p.Bristle + wild), decor: Decor.Mane);
            var j = Hd(0.4f, -2.2f);
            var je = Rot(Hd(5.8f, -2.9f), j, -p.Jaw);
            if (p.Jaw > 8f) Add(5.52f, Slot.Maw, TexelRig.Poly(new[] { Hd(1.6f, -1.4f), Hd(6.8f, -1.9f), je, j }, j.x, j.y, 3, 3));
            Add(5.53f, Slot.Body, TexelRig.Taper(j, je, 1.4f, 0.9f), decor: Decor.Jaw);
            var ear = Hd(-0.6f, 4.0f);
            Add(5.3f, Slot.Body, TexelRig.Ellipse(ear.x, ear.y, 1.4f, 1.5f), decor: Decor.Ear);
            var cr = Hd(0, 0.3f);
            Add(5.4f, Slot.Body, TexelRig.Ellipse(cr.x, cr.y, 4.7f, 4.2f, p.HeadRot), decor: Decor.Cranium);
            Add(5.5f, Slot.Body, TexelRig.Taper(Hd(1.6f, -0.7f), Hd(5.6f, -1.3f), 2.5f, 1.8f), decor: Decor.Muzzle);
            var nose = Hd(6.6f, -0.6f);
            Add(5.55f, Slot.Nose, TexelRig.Ellipse(nose.x, nose.y, 1.0f, 0.8f));
            var up = V(-sa, ca);
            var jt = Vector2.Lerp(j, je, 0.85f);
            Add(5.7f, Slot.Fang, TexelRig.Taper(Hd(5.0f, -2.0f), Hd(5.3f, -3.3f - Mathf.Min(p.Jaw, 20f) * 0.02f), 0.55f, 0.18f));
            if (p.Jaw > 20f) Add(5.69f, Slot.Fang, TexelRig.Taper(Hd(3.4f, -1.9f), Hd(3.5f, -2.9f), 0.45f, 0.15f));
            Add(5.71f, Slot.Fang, TexelRig.Taper(jt, jt + up * 1.5f, 0.5f, 0.18f));
            Add(5.75f, Slot.Body, TexelRig.Taper(Hd(-0.6f, 2.0f), Hd(3.6f, 1.3f), 1.2f, 0.8f), decor: Decor.Brow);
            var so = Hd(2.2f, 0.4f);
            Add(5.8f, Slot.Socket, TexelRig.Ellipse(so.x, so.y, 1.4f, 0.95f, p.HeadRot));
            var eye = Hd(2.5f, 0.4f);
            Add(5.85f, Slot.Eye, TexelRig.Ellipse(eye.x, eye.y, 1.0f, 0.65f, p.HeadRot), level: p.Eye);

            if (p.Roar > 0) fx?.Add(new RoarFx { C = Hd(7.6f, -2.4f), Dir = p.HeadRot - p.Jaw * 0.3f, Phase = p.Roar });
            return Hd(7.0f, -2.4f);
        }

        // ---------------------------------------------------------------- raster

        static Sprite Render(int frame, Mat mat, bool elite)
        {
            var p = Poses[frame];
            float scale = (elite ? EliteScale : 1f) * RigScale;
            int w = Mathf.RoundToInt(W * scale / 2f) * 2, h = Mathf.RoundToInt(H * scale);
            var fx = new List<RoarFx>();
            Build(p, elite, out var rig, fx);

            string name = $"enemy.booster.{(mat.Iron ? "iron" : "gaunt")}{(elite ? ".elite" : "")}.{(BoosterFrame)frame}";
            return TexelRig.Rasterise(name, rig, w, h, scale, mat.Out,
                (in TexelRig.Cell c, int x, int y, int s) => ColourOf(c, x, y, s, mat, p),
                new Vector2(PivotX, PivotY), (px, cw, ch, sc, filled) => Light(px, cw, ch, sc, filled, fx));
        }

        /// <summary>The first waves of the roar, leaving the jaws.</summary>
        static void Light(Color[] px, int w, int h, float scale, System.Func<int, int, bool> filled, List<RoarFx> fx)
        {
            bool Empty(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && !filled(x, y);
            foreach (var f in fx)
                foreach (float r0 in new[] { 2.5f, 5.5f })
                {
                    float R = r0 + (f.Phase - 1) * 1.5f;
                    for (int d = -40; d <= 40; d += 6)
                    {
                        float ang = TexelRig.Rad(f.Dir + d);
                        int x = Mathf.FloorToInt((f.C.x + Mathf.Cos(ang) * R) * scale), y = Mathf.FloorToInt((f.C.y + Mathf.Sin(ang) * R) * scale);
                        if (Empty(x, y)) TexelRig.Put(px, w, x, y, R < 4f ? Accent[3] : Accent[2], (Mathf.Abs(d) > 30 ? 150f : 255f) / 255f);
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

        static Vector2[][] VeinsOf(Decor decor) => decor switch
        {
            Decor.Torso => TorsoVeins,
            Decor.Thigh => ThighVeins,
            Decor.Cranium => CraniumVeins,
            _ => null,
        };

        static Color? ColourOf(in TexelRig.Cell c, int x, int y, int s, Mat mat, Pose p)
        {
            var part = c.Part;
            var hit = c.Hit;
            bool iron = mat.Iron;
            int g = p.Vein;
            var decor = (Decor)part.Decor;
            switch ((Slot)part.Slot)
            {
                case Slot.Eye: return Accent[Mathf.Clamp(part.Level, 0, 3)];
                case Slot.Socket: return Socket;
                case Slot.Maw: return p.Eye >= 3 ? (Mathf.Sqrt(hit.Nx * hit.Nx + hit.Ny * hit.Ny) < 0.55f ? Accent[2] : Accent[1]) : mat.Maw;
                case Slot.Fang: return mat.Fang;
                case Slot.Nose: return mat.Nose;
                case Slot.Mane:
                {
                    var mr = mat.Mane;
                    float rr = Mathf.Sqrt(hit.Nx * hit.Nx + hit.Ny * hit.Ny);
                    if (g > 0 && rr > 0.95f && TexelRig.Hash(x, y) < 0.3f + 0.1f * g) return Accent[Mathf.Min(2, g)];
                    if (iron)
                    {
                        if (Mathf.FloorToInt(Mathf.Atan2(hit.Ny, hit.Nx) * 4f) % 2 == 0) s = Mathf.Max(0, s - 1);
                        return mr[s];
                    }
                    if (TexelRig.Hash(x, Mathf.FloorToInt(y / 2f)) < 0.28f) s = Mathf.Max(0, s - 1);
                    return mr[s];
                }
            }

            var ramp = mat.Hide;
            if (decor == Decor.Jaw) s = Mathf.Max(1, s);
            var veins = VeinsOf(decor);
            if (veins != null)
            {
                float d = 9f;
                foreach (var l in veins) d = Mathf.Min(d, SegDist(l, hit.Nx, hit.Ny));
                if (iron && d < 0.09f) return g == 0 ? Accent[0] : Accent[Mathf.Min(2, g)];
                if (!iron && d < 0.07f) return g == 0 ? ramp[0] : Accent[Mathf.Min(2, g)];
            }
            if (!iron)
            {
                // ribs showing through the flank, and mottled hide
                if (decor == Decor.Torso && hit.Ny < 0.3f && hit.Ny > -0.6f && Mathf.Abs(hit.Nx) < 0.5f && (hit.Nx + 1f) * 4.5f % 1f < 0.2f)
                    return ramp[Mathf.Max(0, s - 1)];
                if (TexelRig.Hash(x, y) < 0.1f) s = Mathf.Max(0, s - 1);
            }
            else if (decor == Decor.Thigh && Mathf.Abs(Mathf.Sqrt(hit.Nx * hit.Nx + hit.Ny * hit.Ny) - 0.72f) < 0.08f && Mathf.FloorToInt(c.Px * 2f) % 3 == 0)
                return mat.Rivet;
            return ramp[s];
        }
    }
}
