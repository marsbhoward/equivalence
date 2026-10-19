using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>The Dasher's frames, in the order <see cref="DasherArt.For"/> returns them.</summary>
    public enum DasherFrame
    {
        Run0, Run1, Run2, Run3, Idle, Tell, Dash0, Dash1, Braced,
        Rush0, Rush1, Bite, Spent0, Spent1, Leap, Land, Reel0, Reel1,
    }

    /// <summary>
    /// The Dasher as a GALE HOUND (the user's sign-off, 2026-10-18, from the mock-up at
    /// https://claude.ai/artifact/TtmquU5W7d2PF4AaKUz4vc): a starved hound, hide shrunk onto its
    /// ribs and a bare skull for a head, with a gale caught in its lungs - wind streams off its
    /// spine for a mane and its tail is nothing but wind.
    ///
    /// THE BODY SAYS THE ATTACK. Long legs, a deep chest and a wasp waist: the quickest, frailest
    /// kind. Both tells are a stalking crouch - eyes and the gaps between the ribs lit acid green,
    /// the wind drawn IN; the line-up dash is stretched with no smear (the floor trail shows its
    /// line, DasherTrail); BRACED is the landing level with you; the charge is stretched with the
    /// wind smeared off its back, and BITE is the frame it goes through you, jaws open on the gale
    /// in its throat; SPENT stands heaving, jaws slack; the retreat bounds; a slam into a column
    /// REELS, the wind gone out of it. The elite has bone horns, a longer mane and two tails.
    ///
    /// The acid green is the Dasher's own tint in both materials, deliberately not the telegraphs'
    /// red. ONE RIG, TWO MATERIALS: Construct is CARRION (ashen hide, yellowed bone), Automaton is
    /// IRONBOUND (the hound cast in iron, an iron skull-mask, vanes down the spine, the gale run
    /// through it as piping - spine, collar, a chest grille). Wind is drawn without an outline.
    /// Drawn through <see cref="TexelRig"/>, ported from the mock-up's JavaScript line for line -
    /// change both or neither.
    /// </summary>
    public static class DasherArt
    {
        const float EliteScale = 1.2f;
        const int W = 54, H = 28;
        const float PivotX = 24.5f, PivotY = 6f;

        enum Slot { Hide, Paw, Wind, Skull, Maw, Socket, Eye, Vane }
        enum Decor { None, Leg, Stub, Chest, Loin, Haunch, Neck, Jaw, Ear, Horn, Cranium, Snout }
        enum Fx { None, Smear, Gather, Daze }

        sealed class Mat
        {
            public Color[] Hide, Bone;
            public Color Wind, Maw, Socket, Rivet, Out, Dark;
            public bool Iron;
        }

        static readonly Mat Carrion = new()
        {
            Hide = TexelRig.Hex("#1c1719", "#332b2b", "#4f4441", "#6f625a"),
            Bone = TexelRig.Hex("#3e382e", "#7d7563", "#b9b099", "#e6dec6"),
            Wind = TexelRig.Hex("#e8f7d0")[0],
            Maw = TexelRig.Hex("#1a0b0e")[0],
            Socket = TexelRig.Hex("#120d0c")[0],
            Dark = TexelRig.Hex("#120d0e")[0],
            Out = TexelRig.Hex("#0b0a0d")[0],
        };

        static readonly Mat Ironbound = new()
        {
            Hide = TexelRig.Hex("#1f2126", "#363a42", "#565c67", "#878e99"),
            Bone = TexelRig.Hex("#2a2d33", "#4a4f58", "#6f7682", "#a3abb7"),
            Wind = TexelRig.Hex("#e8f7d0")[0],
            Maw = TexelRig.Hex("#120f10")[0],
            Socket = TexelRig.Hex("#0d0f12")[0],
            Rivet = TexelRig.Hex("#b6bdc7")[0],
            Out = TexelRig.Hex("#0a0a0c")[0],
            Iron = true,
        };

        /// <summary>The Dasher's own acid green, dim to hot - the gale in it.</summary>
        static readonly Color[] Acc = TexelRig.Hex("#2c3a0c", "#6f9a1c", "#bff240", "#f2ffc4");

        /// <summary>The gale's colours, for the pieces drawn beside the body (the tells' pointer,
        /// the floor trail): [0] the wind's pale, [1] the accent, [2] its hottest.</summary>
        public static Color[] WindRamp => new[] { Carrion.Wind, Acc[2], Acc[3] };

        // ---------------------------------------------------------------- poses

        sealed class Pose
        {
            public Vector2 Hip = new(18, 12), Sh = new(31, 12.5f), Neck, Ear;
            public float Tilt, HeadRot, Jaw, Wind = 1, WindLen, TailLift, Phase;
            public int Glow = 1, Seed = 1;
            public Fx Fx;
            public Vector2 Fn = new(32, 1), Ff = new(30, 1), Hn = new(19.5f, 1), Hf = new(17, 1);
        }

        static Vector2 V(float x, float y) => new(x, y);

        // hip / sh: haunch and shoulder; fn ff hn hf: near and far fore, near and far hind feet.
        // Indexed by DasherFrame.
        static readonly Pose[] Poses =
        {
            new() { Hip = V(18, 12.3f), Sh = V(31, 12), Fn = V(36, 2), Ff = V(34, 1), Hn = V(12.5f, 1.5f), Hf = V(14.5f, 1), HeadRot = 2, Phase = 0 },
            new() { Hip = V(18, 12.8f), Sh = V(31, 13), Fn = V(33, 3.5f), Ff = V(31, 1.2f), Hn = V(17, 2.5f), Hf = V(15, 1.2f), Phase = 30, TailLift = 1 },
            new() { Hip = V(18, 12.5f), Sh = V(31, 12.2f), Fn = V(29, 1), Ff = V(31.5f, 2.5f), Hn = V(22, 1), Hf = V(20, 2.5f), Phase = 60 },
            new() { Hip = V(18, 12), Sh = V(31, 12.6f), Fn = V(30, 2.5f), Ff = V(27, 1), Hn = V(19, 1), Hf = V(22, 1.5f), Phase = 90, HeadRot = -2 },
            new(),                                                                                                    // Idle
            new() { Hip = V(18, 11.5f), Sh = V(31, 8.5f), Tilt = -8, Neck = V(1.5f, -3), HeadRot = -6, Ear = V(-1, -1.2f), Glow = 3,   // Tell
                    Fn = V(35, 1), Ff = V(33.5f, 1), Hn = V(20, 1), Hf = V(18, 1), WindLen = -3, TailLift = -1, Fx = Fx.Gather, Seed = 3 },
            new() { Hip = V(17, 10.5f), Sh = V(32, 10), Tilt = -3, Neck = V(2, -1.5f), HeadRot = -4, Ear = V(-1.5f, -1.5f), Glow = 3,   // Dash0
                    Fn = V(41, 4), Ff = V(39, 3), Hn = V(9, 4), Hf = V(11, 3), WindLen = 3, TailLift = 3, Phase = 10 },
            new() { Hip = V(17, 11), Sh = V(32, 10.5f), Tilt = -2, Neck = V(2, -1.5f), HeadRot = -4, Ear = V(-1.5f, -1.5f), Glow = 3,    // Dash1
                    Fn = V(35, 2.5f), Ff = V(37, 3.5f), Hn = V(16, 2.5f), Hf = V(14, 3.5f), WindLen = 3, TailLift = 4, Phase = 55 },
            new() { Hip = V(17.5f, 11.5f), Sh = V(31, 11), Tilt = 3, Neck = V(0.5f, -1), HeadRot = -3, Glow = 2, Ear = V(-0.5f, -0.6f),  // Braced
                    Fn = V(36.5f, 1), Ff = V(35, 1), Hn = V(17, 1), Hf = V(15, 1), WindLen = 1, Phase = 70 },
            new() { Hip = V(17, 10.5f), Sh = V(32, 10), Tilt = -3, Neck = V(2, -1.5f), HeadRot = -4, Ear = V(-1.5f, -1.5f), Glow = 3,   // Rush0
                    Fn = V(41, 4), Ff = V(39, 3), Hn = V(9, 4), Hf = V(11, 3), WindLen = 5, TailLift = 1, Fx = Fx.Smear, Seed = 1, Phase = 10 },
            new() { Hip = V(17, 11), Sh = V(32, 10.5f), Tilt = -2, Neck = V(2, -1.5f), HeadRot = -4, Ear = V(-1.5f, -1.5f), Glow = 3,    // Rush1
                    Fn = V(35, 2.5f), Ff = V(37, 3.5f), Hn = V(16, 2.5f), Hf = V(14, 3.5f), WindLen = 5, TailLift = 2, Fx = Fx.Smear, Seed = 2, Phase = 55 },
            new() { Hip = V(17, 10.8f), Sh = V(32, 10.5f), Neck = V(2.6f, -0.8f), HeadRot = 10, Jaw = 50, Ear = V(-1.5f, -1.5f), Glow = 3, // Bite
                    Fn = V(40, 4.5f), Ff = V(38, 3), Hn = V(10, 4), Hf = V(12, 3), WindLen = 5, TailLift = 1, Fx = Fx.Smear, Seed = 3, Phase = 30 },
            new() { Hip = V(18, 11.6f), Sh = V(31, 11.2f), Neck = V(1, -2.4f), HeadRot = -15, Jaw = 24, Glow = 1, Ear = V(0, -1),        // Spent0
                    Wind = 0.6f, WindLen = -2, TailLift = -1, Phase = 10 },
            new() { Hip = V(18, 11.4f), Sh = V(31, 11.5f), Neck = V(1, -2), HeadRot = -11, Jaw = 9, Glow = 1, Ear = V(0, -1),            // Spent1
                    Wind = 0.55f, WindLen = -2, TailLift = -1, Phase = 40 },
            new() { Hip = V(18, 13.5f), Sh = V(31, 13.5f), Fn = V(37, 5), Ff = V(35, 4), Hn = V(10.5f, 4.5f), Hf = V(12.5f, 4),         // Leap
                    HeadRot = 6, WindLen = 3, TailLift = 1, Phase = 20 },
            new() { Hip = V(18, 12), Sh = V(31, 11.5f), Fn = V(30, 1), Ff = V(32, 1.5f), Hn = V(23, 1.5f), Hf = V(21, 1), Phase = 50 },  // Land
            new() { Hip = V(18, 11.5f), Sh = V(31, 9), Tilt = -6, Neck = V(1, -4), HeadRot = -28, Jaw = 10, Glow = 0, Ear = V(0.5f, -1.5f), // Reel0
                    Fn = V(35.5f, 1), Ff = V(27, 1), Hn = V(23, 1), Hf = V(13, 1), Wind = 0.35f, WindLen = -4, TailLift = -2, Fx = Fx.Daze, Seed = 1 },
            new() { Hip = V(18, 11.2f), Sh = V(31, 9.4f), Tilt = -4, Neck = V(0.5f, -3.5f), HeadRot = -18, Jaw = 6, Glow = 0, Ear = V(0.5f, -1.5f), // Reel1
                    Fn = V(35.5f, 1), Ff = V(27, 1), Hn = V(23, 1), Hf = V(13, 1), Wind = 0.3f, WindLen = -4, TailLift = -2, Phase = 40, Fx = Fx.Daze, Seed = 2 },
        };

        // ---------------------------------------------------------------- cache

        static readonly Dictionary<int, Sprite[]> _cache = new();

        /// <summary>Every frame of one look and tier, indexed by <see cref="DasherFrame"/>.</summary>
        public static Sprite[] For(EnemyLook look, bool elite)
        {
            int key = (look == EnemyLook.Automaton ? 1 : 0) | (elite ? 2 : 0);
            if (_cache.TryGetValue(key, out var hit) && hit != null && hit.Length > 0 && hit[0] != null) return hit;

            var mat = look == EnemyLook.Automaton ? Ironbound : Carrion;
            var frames = new Sprite[Poses.Length];
            for (int i = 0; i < frames.Length; i++) frames[i] = Render(i, mat, elite);
            _cache[key] = frames;
            return frames;
        }

        // ---------------------------------------------------------------- the rig (facing +x)

        static TexelRig.Rig Build(Pose p, Mat mat, bool elite)
        {
            var rig = new TexelRig.Rig();
            TexelRig.Part Add(float order, Slot slot, TexelRig.Shape test, bool far = false, bool sep = false,
                              Decor decor = Decor.None, float level = 0, float extra = 0)
                => rig.Add(order, (int)slot, test, far, sep, (int)decor, level, extra);
            bool iron = mat.Iron;
            Vector2 hip = p.Hip, sh = p.Sh;

            void FrontLeg(float order, bool far, Vector2 top, Vector2 foot)
            {
                var knee = TexelRig.Ik(top, foot, 5f, 5.4f, -1f);
                Add(order, Slot.Hide, TexelRig.Taper(top, knee, 2.1f, 1.3f), far, !far, Decor.Leg);
                Add(order + 0.01f, Slot.Hide, TexelRig.Taper(knee, foot, 1.25f, 0.85f), far, !far, Decor.Leg);
                Add(order + 0.02f, Slot.Paw, TexelRig.Ellipse(foot.x + 0.7f, foot.y + 0.3f, 1.5f, 0.85f), far);
            }
            void HindLeg(float order, bool far, Vector2 top, Vector2 foot)
            {
                var hock = new Vector2(foot.x - 1.3f, foot.y + 2.7f);
                var knee = TexelRig.Ik(top, hock, 5f, 4.8f, 1f);
                Add(order, Slot.Hide, TexelRig.Taper(top, knee, 2.8f, 1.6f), far, !far, Decor.Leg);
                Add(order + 0.01f, Slot.Hide, TexelRig.Taper(knee, hock, 1.4f, 1.0f), far, !far, Decor.Leg);
                Add(order + 0.02f, Slot.Hide, TexelRig.Taper(hock, foot, 1.0f, 0.8f), far, !far, Decor.Leg);
                Add(order + 0.03f, Slot.Paw, TexelRig.Ellipse(foot.x + 0.6f, foot.y + 0.3f, 1.4f, 0.8f), far);
            }

            // far legs first, behind everything
            FrontLeg(0.5f, true, new Vector2(sh.x - 1.2f, sh.y - 1.5f), p.Ff);
            HindLeg(0.6f, true, new Vector2(hip.x + 0.8f, hip.y - 1f), p.Hf);

            // the tail: wind leaving the rump (Ironbound: through a short iron stub)
            var rump = new Vector2(hip.x - 2.6f, hip.y + 1f);
            var tailFrom = rump;
            if (iron)
            {
                var stub = new Vector2(hip.x - 5.2f, hip.y + 2f + p.TailLift * 0.3f);
                Add(1f, Slot.Hide, TexelRig.Taper(rump, stub, 1.2f, 0.8f), decor: Decor.Stub);
                tailFrom = stub;
            }
            int plumes = elite ? 2 : 1;
            for (int i = 0; i < plumes; i++)
            {
                float dx = i == 0 ? 0f : -1.2f, dy = i == 0 ? 0f : -2.2f;
                float len = (9f + p.WindLen) * p.Wind;
                var end = new Vector2(tailFrom.x - len + dx,
                                      tailFrom.y + 2f + p.TailLift + dy + Mathf.Sin(TexelRig.Rad(p.Phase * 4f + i * 90f)) * 1.2f);
                if (p.Wind > 0.05f)
                    Add(1.2f + i * 0.01f, Slot.Wind, TexelRig.Taper(tailFrom, end, 1.6f * Mathf.Max(0.5f, p.Wind), 0.35f), extra: p.Phase + i * 40f);
            }

            // the body: a deep chest, a wasp waist, lean haunches
            Add(2f, Slot.Hide, TexelRig.Ellipse(sh.x - 1f, sh.y, 5.6f, 4.3f, p.Tilt), decor: Decor.Chest);
            Add(1.9f, Slot.Hide, TexelRig.Taper(new Vector2(sh.x - 3.5f, sh.y + 0.8f), new Vector2(hip.x + 0.5f, hip.y + 0.8f), 3.4f, 2.5f), decor: Decor.Loin);
            Add(2.1f, Slot.Hide, TexelRig.Ellipse(hip.x, hip.y, 3.4f, 3.6f), decor: Decor.Haunch);

            // the neck and head (the head is posed in its own frame about the neck's top)
            var n = new Vector2(sh.x + 2.5f + p.Neck.x, sh.y + 3f + p.Neck.y);
            Add(2.5f, Slot.Hide, TexelRig.Taper(new Vector2(sh.x + 1.2f, sh.y + 0.8f), n, 3f, 2.1f), decor: Decor.Neck);
            float a = TexelRig.Rad(p.HeadRot), ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            Vector2 Hd(float lx, float ly) => new(n.x + lx * ca - ly * sa, n.y + lx * sa + ly * ca);
            var j = Hd(1.6f, -1.2f);
            float ja = TexelRig.Rad(p.HeadRot - 14f - p.Jaw);
            var jawEnd = new Vector2(j.x + Mathf.Cos(ja) * 4.7f, j.y + Mathf.Sin(ja) * 4.7f);
            if (p.Jaw > 6f) Add(3.8f, Slot.Maw, TexelRig.Poly(new[] { Hd(2.0f, -0.4f), Hd(7.2f, -1.1f), jawEnd, j }, j.x, j.y, 3f, 3f));
            Add(3.85f, Slot.Skull, TexelRig.Taper(j, jawEnd, 1.15f, 0.6f), decor: Decor.Jaw);
            Add(3.95f, Slot.Hide, TexelRig.Taper(Hd(0.4f, 1.9f), Hd(-2.6f + p.Ear.x, 4.3f + p.Ear.y), 1.05f, 0.3f), decor: Decor.Ear);
            if (elite)
            {
                Add(3.96f, Slot.Skull, TexelRig.Taper(Hd(1.0f, 1.6f), Hd(-2.8f, 3.4f), 0.9f, 0.3f), decor: Decor.Horn);
                Add(4.6f, Slot.Skull, TexelRig.Taper(Hd(1.8f, 1.7f), Hd(-1.8f, 4.1f), 0.85f, 0.3f), decor: Decor.Horn);
            }
            var cr = Hd(1.6f, 0.6f);
            Add(4f, Slot.Skull, TexelRig.Ellipse(cr.x, cr.y, 2.8f, 2.4f, p.HeadRot), decor: Decor.Cranium);
            Add(4.1f, Slot.Skull, TexelRig.Taper(Hd(2.5f, 0.3f), Hd(7.4f, -0.9f), 1.75f, 0.9f), decor: Decor.Snout);
            var so = Hd(2.6f, 1.0f);
            Add(4.2f, Slot.Socket, TexelRig.Ellipse(so.x, so.y, 1.45f, 0.95f, p.HeadRot));
            var ey = Hd(2.9f, 1.0f);
            Add(4.3f, Slot.Eye, TexelRig.Ellipse(ey.x, ey.y, 0.85f, 0.55f, p.HeadRot), level: p.Glow);

            // the mane: wind streaming off the spine (Ironbound: off a row of iron vanes)
            int manes = elite ? 5 : 3;
            var back0 = new Vector2(n.x - 0.8f, n.y + 1.2f);
            var back1 = new Vector2(hip.x + 2f, hip.y + 3.2f);
            for (int i = 0; i < manes; i++)
            {
                float t = i / (float)(manes - 1);
                var b = Vector2.Lerp(back0, back1, t);
                if (iron) Add(2.6f + i * 0.01f, Slot.Vane, TexelRig.Taper(b, new Vector2(b.x - 1.6f, b.y + 1.9f), 0.85f, 0.35f));
                float len = (4.2f + (elite ? 1.2f : 0f) + p.WindLen * 0.5f - t * 1.2f) * p.Wind;
                float w = Mathf.Sin(TexelRig.Rad(p.Phase * 3f + i * 70f)) * 0.9f;
                var from = iron ? new Vector2(b.x - 1.4f, b.y + 1.8f) : new Vector2(b.x, b.y + 0.6f);
                if (p.Wind > 0.05f && len > 0.6f)
                    Add(1.5f + i * 0.01f, Slot.Wind, TexelRig.Taper(from, new Vector2(from.x - len, from.y + 1.4f + w), 0.95f, 0.3f), extra: p.Phase + i * 25f);
            }

            // near legs over the body
            FrontLeg(3f, false, new Vector2(sh.x, sh.y - 2f), p.Fn);
            HindLeg(3.1f, false, new Vector2(hip.x, hip.y - 1f), p.Hn);
            return rig;
        }

        // ---------------------------------------------------------------- raster

        static Sprite Render(int frame, Mat mat, bool elite)
        {
            var p = Poses[frame];
            float scale = elite ? EliteScale : 1f;
            int w = Mathf.RoundToInt(W * scale / 2f) * 2, h = Mathf.RoundToInt(H * scale);
            var rig = Build(p, mat, elite);

            string name = $"enemy.dasher.{(mat.Iron ? "iron" : "carrion")}{(elite ? ".elite" : "")}.{(DasherFrame)frame}";
            return TexelRig.Rasterise(name, rig, w, h, scale, mat.Out,
                (in TexelRig.Cell c, int x, int y, int s, System.Func<int, int, TexelRig.Part> at) => ColourOf(c, x, y, s, mat, p, at),
                new Vector2(PivotX, PivotY), (px, cw, ch, sc, filled) => Light(px, cw, ch, sc, filled, p, mat),
                part => (Slot)part.Slot != Slot.Wind);
        }

        /// <summary>What leaves the body: the charge's smear, the tells' wind drawn in, a reel's dust.</summary>
        static void Light(Color[] px, int w, int h, float scale, System.Func<int, int, bool> filled, Pose p, Mat mat)
        {
            int T(float v) => Mathf.FloorToInt(v * scale);
            bool Empty(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && !filled(x, y);
            switch (p.Fx)
            {
                case Fx.Smear:
                    // streaks of wind behind it, hard-edged on the grid
                    for (int y = T(4); y < T(18); y++)
                    {
                        int lead = -1;
                        for (int x = 0; x < w; x++) if (filled(x, y)) { lead = x; break; }
                        if (lead < 0 || (y + p.Seed) % 2 != 0) continue;
                        int len = Mathf.FloorToInt(6 + TexelRig.Hash(y, p.Seed) * 14);
                        for (int k = 1; k <= len; k++)
                        {
                            int x = lead - k;
                            if (x < 0) break;
                            if (Empty(x, y)) TexelRig.Put(px, w, x, y, k < 3 ? Acc[3] : mat.Wind, Mathf.Floor(220f * (1f - k / (len + 1f)) + 0.5f) / 255f);
                        }
                    }
                    break;
                case Fx.Gather:
                {
                    // the wind drawn IN: short streaks on a closing ring
                    float cx = (p.Sh.x + p.Hip.x) / 2f, cy = (p.Sh.y + p.Hip.y) / 2f + 1f;
                    for (int i = 0; i < 9; i++)
                    {
                        float ang = TexelRig.Hash(i, p.Seed) * 360f, r = 11f + TexelRig.Hash(p.Seed, i) * 4f;
                        for (int k = 0; k < 4; k++)
                        {
                            int x = T(cx + Mathf.Cos(TexelRig.Rad(ang)) * r), y = T(cy + Mathf.Sin(TexelRig.Rad(ang)) * r * 0.75f);
                            if (Empty(x, y)) TexelRig.Put(px, w, x, y, k == 3 ? Acc[2] : mat.Wind, (120f + k * 30f) / 255f);
                            ang += 6f; r -= 1.1f;
                        }
                    }
                    break;
                }
                case Fx.Daze:
                    // a few dust puffs where it skidded
                    for (int i = 0; i < 5; i++)
                    {
                        int x = T(p.Hip.x + 4f + TexelRig.Hash(i, p.Seed) * 14f), y = T(1f + TexelRig.Hash(p.Seed, i) * 3f);
                        foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (0, 1) })
                            if (Empty(x + dx, y + dy)) TexelRig.Put(px, w, x + dx, y + dy, Dust, 150f / 255f);
                    }
                    break;
            }
        }

        static readonly Color Dust = TexelRig.Hex("#7a7066")[0];

        static Color? ColourOf(in TexelRig.Cell c, int x, int y, int s, Mat mat, Pose p, System.Func<int, int, TexelRig.Part> at)
        {
            var part = c.Part;
            var hit = c.Hit;
            bool iron = mat.Iron;
            switch ((Slot)part.Slot)
            {
                case Slot.Wind:
                {
                    // streaks running back along the plume, with gaps: wind, not a ribbon
                    float u = Mathf.Repeat(hit.T * 6f + part.Extra / 45f, 2f);
                    if (u > 1.45f && hit.T > 0.2f) return null;
                    var col = mat.Wind;
                    col.a = Mathf.Floor((hit.T < 0.25f ? 230f : 170f) * (1f - hit.T * 0.55f) + 0.5f) / 255f; // half up, as the mock-up
                    return col;
                }
                case Slot.Eye:
                    return part.Level == 0 ? Acc[0] : part.Level == 1 ? Acc[2] : Acc[3];
                case Slot.Socket:
                    return mat.Socket;
                case Slot.Maw:
                {
                    // the open throat: dark, the gale glowing deep in it; teeth along both edges
                    var up = at(x, y + 1);
                    var dn = at(x, y - 1);
                    if (up != null && up.Decor == (int)Decor.Snout && x % 2 == 0) return mat.Bone[3];
                    if (dn != null && dn.Decor == (int)Decor.Jaw && x % 2 == 1) return mat.Bone[3];
                    return TexelRig.Hash(x, y) < 0.35f ? Acc[1] : mat.Maw;
                }
                case Slot.Paw:
                    return iron ? mat.Hide[Mathf.Min(3, s + 1)] : mat.Hide[Mathf.Max(0, s - 1)];
                case Slot.Vane:
                    return mat.Hide[Mathf.Min(3, s + 1)];
                case Slot.Skull:
                    if (iron && part.Decor == (int)Decor.Cranium && Mathf.Abs(hit.Nx + 0.15f) < 0.12f && hit.Ny > 0f) return mat.Rivet; // the mask's ridge
                    if (!iron && TexelRig.Hash(x, y) < 0.12f) return mat.Bone[Mathf.Max(0, s - 1)];
                    return mat.Bone[Mathf.Min(3, s + (part.Decor == (int)Decor.Horn ? 0 : 1))];
            }

            // hide (Carrion) or plate (Ironbound)
            var ramp = mat.Hide;
            int g = p.Glow;
            var decor = (Decor)part.Decor;
            if (decor == Decor.Chest)
            {
                if (!iron && hit.Ny < 0.5f && hit.Ny > -0.8f && hit.Nx > -0.95f && hit.Nx < 0.25f)
                {
                    if (x % 3 == 0) return ramp[0];                                                                 // a rib's shadow
                    if (x % 3 == 2 && hit.Ny < 0.1f && hit.Ny > -0.55f) return g == 0 ? mat.Dark : Acc[Mathf.Min(3, g)]; // between the ribs, the gale
                    if (x % 3 == 2) return g == 0 ? mat.Dark : Acc[Mathf.Max(0, g - 1)];
                }
                if (iron && hit.Ny < 0.2f && hit.Ny > -0.6f && hit.Nx > -0.4f && hit.Nx < 0.45f && y % 2 == 0)
                    return g == 0 ? Acc[0] : Acc[Mathf.Min(3, g + 1)];                                                // the chest grille
            }
            if (iron && (decor == Decor.Chest || decor == Decor.Loin) && hit.Ny > 0.62f && hit.Ny < 0.86f)
                return g == 0 ? Acc[0] : Acc[Mathf.Min(3, g)];                                                        // the spine's pipe
            if (iron && decor == Decor.Neck && hit.T > 0.32f && hit.T < 0.46f)
                return g == 0 ? Acc[0] : Acc[Mathf.Min(3, g)];                                                        // the collar
            if (iron && decor == Decor.Haunch && Mathf.Abs(hit.Nx * hit.Nx + hit.Ny * hit.Ny - 0.35f) < 0.06f && hit.Ny > -0.2f)
                return mat.Rivet;
            if (!iron)
            {
                float nz = TexelRig.Hash(Mathf.FloorToInt(x / 2f), y);
                if (nz < 0.18f) s = Mathf.Max(0, s - 1);                                                              // mangy, patchy hide
                if (decor == Decor.Haunch && hit.Ny > 0.35f && hit.Nx < 0.2f && nz > 0.75f) return mat.Bone[1];      // a hip bone through it
            }
            return ramp[s];
        }
    }
}
