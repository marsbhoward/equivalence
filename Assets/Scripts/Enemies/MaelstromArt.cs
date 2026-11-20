using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>The Maelstrom's frames, in the order <see cref="MaelstromArt.For"/> returns them: a turning
    /// loop, and four steps of draining into the floor (played back to front as it pours up).</summary>
    public enum MaelstromFrame { Turn0, Turn1, Turn2, Turn3, Turn4, Turn5, Turn6, Turn7, Sink0, Sink1, Sink2, Sink3 }

    /// <summary>
    /// The MAELSTROM (the user's sign-off 2026-11-20, mock https://claude.ai/artifact/AeFpbH6KKevKh6F9F6bURn,
    /// body B, version 6): a figure of WATER built of turning rings - each a filled ellipse with a light arc
    /// and an accent arc running round it, the look of the first standing whirlpool kept exactly (the
    /// user's call) - in a new silhouette from their two references: a broad barrel chest, heavy arms
    /// bowed at the elbow with fists at the hip, NO head and no neck (the shoulders are its top line), a
    /// waist that narrows into a tail sweeping off to one side to a point on the floor. A drowned stone
    /// heart with one eye in the chest, specks of spray orbiting it (behind the body, then over it).
    ///
    /// ONE RIG, TWO MATERIALS: Construct is WATER (the teal of the mock), Automaton is IRONBOUND (the
    /// rings cast in iron, the water run through it as the accent). Ported from the mock's JavaScript -
    /// change both or neither. The mock's y runs down; here it runs up from the floor.
    /// </summary>
    public static class MaelstromArt
    {
        const float EliteScale = 1.2f;
        const int W = 48, H = 42;
        const float CX = 24f, Floor = 2f;
        const float PivotX = 24f, PivotY = 9f;
        const int TurnFrames = 8;

        enum Slot { Ring, Heart, Speck }

        sealed class Mat
        {
            public Color D, M, L, Hi, Out, StoneD, StoneL;
            public Color[] Acc;
            public bool Iron;
        }

        /// <summary>The water's teal, dim to hot - the accent arcs, the eye, the ring on the floor.</summary>
        static readonly Color[] Acc = TexelRig.Hex("#123a3a", "#2a8a86", "#5fe0d0", "#c8fff6");

        /// <summary>Colours the biome weathering never touches - the glows and tells (CreatureWeathering).</summary>
        internal static Color[] Reserved => Acc;

        static readonly Mat Water = new()
        {
            D = TexelRig.Hex("#20313a")[0], M = TexelRig.Hex("#35505a")[0], L = TexelRig.Hex("#5f8486")[0], Hi = TexelRig.Hex("#9fc0b8")[0],
            Out = TexelRig.Hex("#0a0d10")[0], StoneD = TexelRig.Hex("#20313a")[0], StoneL = TexelRig.Hex("#5f8486")[0], Acc = Acc,
        };

        static readonly Mat Ironbound = new()
        {
            D = TexelRig.Hex("#1f2126")[0], M = TexelRig.Hex("#363a42")[0], L = TexelRig.Hex("#878e99")[0], Hi = TexelRig.Hex("#b6bdc7")[0],
            Out = TexelRig.Hex("#0a0a0c")[0], StoneD = TexelRig.Hex("#2a2d33")[0], StoneL = TexelRig.Hex("#8f96a1")[0], Acc = Acc, Iron = true,
        };

        /// <summary>A look's accent ramp, dim to hot.</summary>
        public static Color[] Accent(EnemyLook look) => Acc;

        // ---------------------------------------------------------------- cache

        static readonly Dictionary<int, Sprite[]> _frames = new();

        public static Sprite[] For(EnemyLook look, bool elite) => For(look, elite, Art.Biomes.BiomeKind.Tower);

        public static Sprite[] For(EnemyLook look, bool elite, Art.Biomes.BiomeKind biome)
        {
            int key = (look == EnemyLook.Automaton ? 1 : 0) | (elite ? 2 : 0) | ((int)biome << 2);
            if (_frames.TryGetValue(key, out var hit) && hit != null && hit.Length > 0 && hit[0] != null) return hit;
            var mat = look == EnemyLook.Automaton ? Ironbound : Water;
            var frames = new Sprite[TurnFrames + 4];
            for (int i = 0; i < frames.Length; i++) frames[i] = Render(i, mat, elite, biome);
            _frames[key] = frames;
            return frames;
        }

        // ---------------------------------------------------------------- the figure

        /// <summary>Half-width at height <paramref name="h"/> above the floor: a tail thin at the floor,
        /// the waist, the barrel chest, the shoulders (the top - no head, no neck).</summary>
        static float Half(float h) => h < 12f ? 0.5f + h * 0.31f
                                    : h < 16f ? 4.2f + (h - 12f) * 0.2f
                                    : h < 27f ? 5f + (h - 16f) * 0.6f
                                    : 11.6f + (h - 27f) * 0.2f;

        /// <summary>The tail's sweep off to one side, and its curl with the turn.</summary>
        static float Curl(float h, float t)
        {
            if (h >= 14f) return 0f;
            float u = 1f - h / 14f;
            return u * u * 8f + Mathf.Sin(t * 3f + h * 0.4f) * 1.6f * u;
        }

        /// <summary>One RING: the filled ellipse (rx, ry + 1.4), reporting its offset from the centre and its
        /// width so the colourer can find the arc that turns round it (rx - 1, ry).</summary>
        static TexelRig.Shape Ring(float cx, float cy, float rx, float ry) => (float x, float y, out TexelRig.Hit h) =>
        {
            float fy = ry + 1.4f, nx = (x - cx) / rx, ny = (y - cy) / fy;
            h = new TexelRig.Hit { Nx = nx, Ny = ny, S = x - cx, E = y - cy, K = rx, T = ry };
            return nx * nx + ny * ny <= 1f;
        };

        static void AddRing(TexelRig.Rig rig, float order, float x, float y, float rx, float ry, int k, float t)
            => rig.Add(order, (int)Slot.Ring, Ring(x, y, rx, ry), level: k, extra: t * 5f + k * 0.9f);

        static void Arm(TexelRig.Rig rig, float order, int side, float t)
        {
            float sw = Mathf.Sin(t * 2.4f + side) * 1.2f;
            for (int k = 0; k <= 7; k++)
            {
                float s = k / 7f, ex = side * 16f + sw, ey = 19f;
                float x = s < 0.45f ? side * 11f + (ex - side * 11f) * (s / 0.45f) : ex + (side * 13f + sw * 1.5f - ex) * ((s - 0.45f) / 0.55f);
                float y = s < 0.45f ? 27f + (ey - 27f) * (s / 0.45f) : ey + (11f - ey) * ((s - 0.45f) / 0.55f);
                float r = s < 0.2f ? 3.6f : s > 0.85f ? 3.2f : 2.6f;
                AddRing(rig, order + k * 0.001f, CX + x, Floor + y, r, r * 0.55f, k + (side > 0 ? 1 : 0), t);
            }
        }

        static TexelRig.Rig Build(float t, float drop)
        {
            var rig = new TexelRig.Rig();
            // the specks ORBIT THE HEART: the far half behind everything
            for (int k = 0; k < 6; k++)
            {
                float a = t * 2.6f + k * 1.047f, sx = Mathf.Cos(a) * 9f, sy = -Mathf.Sin(a) * 3.2f;
                rig.Add(Mathf.Sin(a) < 0f ? 0.5f : 9f, (int)Slot.Speck,
                        TexelRig.Ellipse(CX + Mathf.Round(sx) + 0.5f, Floor + 21f + Mathf.Round(sy) + 0.5f, 0.55f, 0.55f),
                        level: Mathf.Sin(a) > 0.6f ? 1 : 0);
            }
            Arm(rig, 1f, -1, t);
            // the body, top ring first so each lower ring lies over the one above (as the mock drew it)
            for (float h = 29f; h >= 0f; h -= h < 13f ? 1.6f : 2.6f)
                AddRing(rig, 3f + (30f - h) * 0.01f, CX + Curl(h, t), Floor + h, Half(h), 1.9f, Mathf.RoundToInt(h / 2.6f), t);
            Arm(rig, 6f, 1, t);
            // the drowned stone heart in the chest
            rig.Add(8f, (int)Slot.Heart, TexelRig.Ellipse(CX, Floor + 21f, 5f, 5f));
            // draining: the whole figure lowered into the floor (what passes the floor line is cut in Render)
            if (drop > 0f) foreach (var p in rig.Parts) p.Test = Drop(p.Test, drop);
            return rig;
        }

        /// <summary>A shape lowered into the floor by <paramref name="drop"/> texels.</summary>
        static TexelRig.Shape Drop(TexelRig.Shape s, float drop) => (float x, float y, out TexelRig.Hit h) => s(x, y + drop, out h);

        // ---------------------------------------------------------------- raster

        static Sprite Render(int frame, Mat mat, bool elite, Art.Biomes.BiomeKind biome)
        {
            float scale = elite ? EliteScale : 1f;
            int w = Mathf.RoundToInt(W * scale / 2f) * 2, h = Mathf.RoundToInt(H * scale);
            bool sink = frame >= TurnFrames;
            int step = sink ? frame - TurnFrames : 0;
            // the turn loop: one full turn of the rings, one sway of the arms and tail, the specks a sixth round
            float t = sink ? 0f : frame / (float)TurnFrames * (2f * Mathf.PI / 5f);
            float drop = sink ? (step + 1) * 7.5f : 0f;
            var rig = Build(t, drop);
            string name = $"enemy.maelstrom.{(mat.Iron ? "iron" : "water")}{(elite ? ".elite" : "")}.{(MaelstromFrame)frame}";
            if (biome != Art.Biomes.BiomeKind.Tower) name += "." + biome;
            int floorRow = Mathf.RoundToInt(Floor * scale);
            return TexelRig.Rasterise(name, rig, w, h, scale, mat.Out,
                (in TexelRig.Cell c, int x, int y, int s, System.Func<int, int, TexelRig.Part> at) => ColourOf(c, mat),
                new Vector2(PivotX, PivotY),
                // what has drained below the floor line is gone
                sink ? (px, cw, ch, sc, filled) => { for (int yy = 0; yy < floorRow && yy < ch; yy++) for (int xx = 0; xx < cw; xx++) px[yy * cw + xx] = Color.clear; } : null);
        }

        static Color? ColourOf(in TexelRig.Cell c, Mat mat)
        {
            var hit = c.Hit;
            switch ((Slot)c.Part.Slot)
            {
                case Slot.Speck:
                    return c.Part.Level == 1 ? mat.Acc[3] : mat.Hi;
                case Slot.Heart:
                {
                    // dark stone, a lit edge up-left, the one eye (3x2, its hot centre)
                    float dx = hit.Nx * 5f, dy = hit.Ny * 5f;
                    if (dy >= -1f && dy < 1f && dx >= -1.5f && dx < 1.5f) return dx >= -0.5f && dx < 0.5f && dy >= 0f ? mat.Acc[3] : mat.Acc[2];
                    if (dy >= 3.5f && dx >= -3.5f && dx < -0.5f) return mat.StoneL;
                    return mat.StoneD;
                }
            }

            // a ring: the arc turning round it (rx - 1, ry), light for 1.6 rad and accent for 0.6 rad half a turn on
            float rx = Mathf.Max(0.5f, hit.K - 1f), ry = hit.T;
            float sx = hit.S / rx, sy = hit.E / ry;
            float d = Mathf.Sqrt(sx * sx + sy * sy);
            if (Mathf.Abs(d - 1f) * Mathf.Min(rx, ry) < 0.6f)
            {
                // the mock's angle, measured on a canvas whose y runs down
                float ang = Mathf.Atan2(-sy, sx);
                float a0 = c.Part.Extra;
                float rel = Mathf.Repeat(ang - a0, 2f * Mathf.PI);
                if (rel < 1.6f) return mat.L;
                if (rel >= 3.2f && rel < 3.8f) return Color.Lerp(c.Part.Level % 2 == 1 ? mat.M : mat.D, mat.Acc[2], 0.67f);
            }
            return c.Part.Level % 2 == 1 ? mat.M : mat.D;
        }
    }
}
