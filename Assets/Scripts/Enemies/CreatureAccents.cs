using UnityEngine;
using Convergence.Art.Biomes;

namespace Convergence.Enemies
{
    /// <summary>
    /// The hand-drawn ACCENT each kind wears in each biome (the user's call, 2026-10-22), built as
    /// PARTS OF THE KIND'S RIG so the body's own pose carries them and nearer limbs draw over them -
    /// never pasted on a finished frame (the mock-up did that, and the accent rode up onto a raised
    /// arm). A kind's Build places them from its own anchors through these builders; the rasteriser
    /// colours any part on <see cref="Slot"/> here (TexelRig), so no kind's colourer changes.
    ///
    /// The forest and the salt mirror each other (fungus / salt cubes, moss / salt crust); the sky
    /// ruin is wind - streamers, cloud, feathers, ivy on stone. Every accent colour is outside the
    /// weathering (<see cref="IsAccent"/>), and none is a tell colour.
    ///
    /// <see cref="Biome"/> and <see cref="Frame"/> are set by a kind's Render round its Build - a
    /// static, so the many other callers of Build (muzzle offsets, anchors) see the Tower and add
    /// nothing.
    /// </summary>
    public static class CreatureAccents
    {
        /// <summary>The rig slot the rasteriser colours from here, whatever kind the rig is.</summary>
        public const int Slot = 1000;

        public enum Kind { None, Fungus, SaltCubes, Streamer, Moss, Cloud, Weed, Leaf, Ivy, Feather, Crystal }

        public static BiomeKind Biome = BiomeKind.Tower;
        public static int Frame;
        /// <summary>The Nigredo's crystals on the creature: 0 none, 1-3 larger (Art.Biomes.Nigredo.Shards).</summary>
        public static int Shards;

        static readonly Color[] FungusRamp = CreatureWeathering.Fungus;
        static readonly Color[] SaltRamp = CreatureWeathering.SaltCubes;
        static readonly Color[] StreamerRamp = CreatureWeathering.Streamer;
        static readonly Color[] MossRamp = TexelRig.Hex("#1c2c12", "#2e461a", "#466424", "#62842e");
        static readonly Color[] CloudRamp = TexelRig.Hex("#9aa4b4", "#c4ccd8", "#e4e8ee", "#f6f8fb");
        static readonly Color[] WeedRamp = TexelRig.Hex("#3a5418", "#557a22", "#76a02e", "#9cc444");   // brighter than the tentacles it trails among
        static readonly Color[] LeafRamp = TexelRig.Hex("#3a2a10", "#6a4a1a", "#9a6e26", "#c49a3a");
        static readonly Color[] IvyRamp = TexelRig.Hex("#1c3014", "#2e4c1c", "#467026", "#6a9634");
        static readonly Color[] FeatherRamp = TexelRig.Hex("#8a8a90", "#b4b4ba", "#dcdce0", "#f4f4f6");
        /// <summary>Shards of the Stone: dark side, faces, lit edge, the hot core.</summary>
        static readonly Color[] CrystalRamp = TexelRig.Hex("#600c18", "#9e1828", "#d6333c", "#ff9690");

        static Color[][] _all;

        /// <summary>Is this one of the accent colours (left alone by the weathering)?</summary>
        public static bool IsAccent(Color c)
        {
            _all ??= new[] { FungusRamp, SaltRamp, StreamerRamp, MossRamp, CloudRamp, WeedRamp, LeafRamp, IvyRamp, FeatherRamp, CrystalRamp };
            foreach (var ramp in _all)
                foreach (var k in ramp)
                    if (Mathf.Abs(k.r - c.r) + Mathf.Abs(k.g - c.g) + Mathf.Abs(k.b - c.b) < 0.04f) return true;
            return false;
        }

        /// <summary>What the rasteriser paints for an accent part (its kind is the part's Level).</summary>
        public static Color Colour(Kind kind, in TexelRig.Hit hit, int x, int y, int shade)
        {
            switch (kind)
            {
                case Kind.Fungus:
                    return hit.Ny > 0.35f ? FungusRamp[3] : hit.Ny > -0.1f ? FungusRamp[2] : hit.Ny > -0.55f ? FungusRamp[1] : FungusRamp[0];
                case Kind.SaltCubes:
                    return hit.Ny > 0.2f ? SaltRamp[(x >> 1) % 2 == 0 ? 3 : 2] : hit.Ny > -0.5f ? SaltRamp[1] : SaltRamp[0];
                case Kind.Streamer: return StreamerRamp[shade];
                case Kind.Moss: return MossRamp[Mathf.Min(3, shade + (TexelRig.Hash(x, y) < 0.25f ? 1 : 0))];
                case Kind.Cloud: return CloudRamp[Mathf.Min(3, shade + 1)];
                case Kind.Weed: return WeedRamp[shade];
                case Kind.Leaf: return TexelRig.Hash(x >> 1, y >> 1) < 0.5f ? LeafRamp[Mathf.Max(1, shade)] : IvyRamp[Mathf.Max(1, shade)];
                case Kind.Ivy: return IvyRamp[shade];
                case Kind.Feather: return FeatherRamp[Mathf.Min(3, shade + 1)];
                case Kind.Crystal: return CrystalRamp[Mathf.Min(3, shade)];
            }
            return Color.magenta;
        }

        static void Add(TexelRig.Rig rig, float order, Kind kind, TexelRig.Shape s) => rig.Add(order, Slot, s, level: (int)kind);

        public static bool Forest => Biome == BiomeKind.Forest;
        public static bool Salt => Biome == BiomeKind.SaltFlats;
        public static bool Sky => Biome == BiomeKind.SkyRuin;

        /// <summary>The forest/salt pair: two fungus brackets, or two shelves of salt cubes, at
        /// <paramref name="a"/> (the larger) and <paramref name="b"/>. Nothing in the other places.</summary>
        public static void Shelves(TexelRig.Rig rig, float order, Vector2 a, Vector2 b, float size)
        {
            if (!Forest && !Salt) return;
            var k = Forest ? Kind.Fungus : Kind.SaltCubes;
            Add(rig, order, k, TexelRig.Ellipse(a.x, a.y, size, size * 0.47f));
            Add(rig, order, k, TexelRig.Ellipse(b.x, b.y, size * 0.75f, size * 0.37f));
        }

        /// <summary>Torn cloth trailing from <paramref name="root"/> toward <paramref name="dirX"/>
        /// (-1 = behind a body facing right), waving with the frame.</summary>
        public static void Ribbons(TexelRig.Rig rig, float order, Vector2 root, float dirX, int count, float segLen, float width)
        {
            float ph = Frame * 1.3f;
            for (int r = 0; r < count; r++)
            {
                int segs = r == 0 ? 4 : 3;
                var prev = root + new Vector2(0f, -r * width * 1.6f);
                for (int k = 1; k <= segs; k++)
                {
                    var next = root + new Vector2(dirX * k * segLen, -r * width * 1.6f + Mathf.Sin(k * 0.9f + ph + r * 1.7f) * width * 0.75f - k * width * 0.22f);
                    Add(rig, order, Kind.Streamer, TexelRig.Taper(prev, next, width * (1f - k * 0.09f), width * (0.93f - k * 0.12f)));
                    prev = next;
                }
            }
        }

        /// <summary>Strands hanging from each of <paramref name="tops"/> (moss or weed), swaying.</summary>
        public static void Strands(TexelRig.Rig rig, float order, Kind kind, Vector2[] tops, float len, float width)
        {
            for (int i = 0; i < tops.Length; i++)
            {
                float l = len * (0.65f + 0.35f * TexelRig.Hash(i, 3));
                var prev = tops[i];
                const int segs = 3;
                for (int k = 1; k <= segs; k++)
                {
                    float t = k / (float)segs;
                    var next = tops[i] + new Vector2(Mathf.Sin(Frame * 0.9f + i * 1.9f + k) * width * 0.6f * t, -l * t);
                    Add(rig, order, kind, TexelRig.Taper(prev, next, width * (1.05f - t * 0.3f), width * (0.95f - t * 0.45f)));
                    prev = next;
                }
            }
        }

        /// <summary>Little clouds round a body.</summary>
        public static void Puffs(TexelRig.Rig rig, float order, Vector2[] at, float r)
        {
            for (int i = 0; i < at.Length; i++)
            {
                float rr = r * (0.8f + 0.4f * TexelRig.Hash(i, 7));
                var c = at[i] + new Vector2(Mathf.Sin(Frame * 0.7f + i) * r * 0.25f, 0f);
                Add(rig, order, Kind.Cloud, TexelRig.Ellipse(c.x, c.y, rr * 1.4f, rr * 0.8f));
                Add(rig, order, Kind.Cloud, TexelRig.Ellipse(c.x + rr * 0.6f, c.y + rr * 0.45f, rr * 0.8f, rr * 0.65f));
            }
        }

        /// <summary>Leaves caught on a body, each turned its own way.</summary>
        public static void Leaves(TexelRig.Rig rig, float order, Vector2[] at, float size)
        {
            for (int i = 0; i < at.Length; i++)
                Add(rig, order, Kind.Leaf, TexelRig.Ellipse(at[i].x, at[i].y, size, size * 0.5f, -30f + 60f * TexelRig.Hash(i, 11)));
        }

        /// <summary>A vine from <paramref name="from"/> to <paramref name="to"/>, leaves along it.</summary>
        public static void Ivy(TexelRig.Rig rig, float order, Vector2 from, Vector2 to, float width)
        {
            Add(rig, order, Kind.Ivy, TexelRig.Taper(from, to, width * 0.5f, width * 0.35f));
            var d = to - from;
            var n = new Vector2(-d.y, d.x).normalized;
            for (int k = 1; k <= 4; k++)
            {
                var q = from + d * (k / 4.6f) + n * (k % 2 == 0 ? 1f : -1f) * width * 0.9f;
                Add(rig, order + 0.01f, Kind.Ivy, TexelRig.Ellipse(q.x, q.y, width * 0.9f, width * 0.6f, k * 40f));
            }
        }

        /// <summary>The NIGREDO's shards of the Stone, grown out of the body at <paramref name="at"/>:
        /// two to four deep red points fanned up and back, larger each step of
        /// <see cref="Shards"/>. Nothing below the Nigredo. <paramref name="unit"/> scales them to the
        /// kind's rig units. The light they give is the creature's Glow (TexelVisual).</summary>
        public static void StoneShards(TexelRig.Rig rig, float order, Vector2 at, float unit)
        {
            if (Shards <= 0) return;
            int n = Shards + 1;
            float len = (2.4f + Shards * 0.9f) * unit, w = (0.75f + Shards * 0.18f) * unit;
            for (int i = 0; i < n; i++)
            {
                // fanned well apart, so each reads as its own crystal and not one red cone
                float a = TexelRig.Rad(100f + (i - (n - 1) * 0.5f) * 34f);
                float l = len * (i == n / 2 ? 1f : 0.68f);
                var root = at + new Vector2((i - (n - 1) * 0.5f) * w * 2.1f, -Mathf.Abs(i - (n - 1) * 0.5f) * w * 0.4f);
                Add(rig, order + i * 0.001f, Kind.Crystal, TexelRig.Taper(root, root + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * l, w, w * 0.15f));
            }
        }

        /// <summary>A tuft of pale feathers from <paramref name="root"/>, fanned up and back.</summary>
        public static void Feathers(TexelRig.Rig rig, float order, Vector2 root, int count, float len, float dirX)
        {
            for (int i = 0; i < count; i++)
            {
                float a = TexelRig.Rad(90f + dirX * (20f + i * 22f));
                var tip = root + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * len * (1f - i * 0.15f);
                Add(rig, order, Kind.Feather, TexelRig.Taper(root, tip, len * 0.16f, len * 0.07f));
            }
        }
    }
}
