using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Hazards
{
    /// <summary>
    /// The Drowned Forest's columns are TREES (the user's call, 2026-10-22): the trunk stands on
    /// exactly the column's footprint - the collider never changes - and the canopy above it is
    /// drawing only, wider than the trunk, so a player behind the leaves fades it as they would a
    /// shaft (<see cref="TreeCanopyWidth"/>). A broken column is a STUMP, its rings showing; a
    /// cracked (breakable) one is lighter and warmer - split bark, the walls' rule - with a fungus
    /// shelf on it. Same Grid, same outline, same density as the stone columns.
    /// </summary>
    static partial class ColumnArt
    {
        /// <summary>The canopy's half-width as a multiple of the column's radius - also what a
        /// player behind it is looked for under.</summary>
        public const float TreeCanopyWidth = 2.1f;

        static readonly Pal BarkPal = new()
        {
            Ramp = new[] { new Color(0.09f, 0.06f, 0.045f), new Color(0.13f, 0.09f, 0.06f), new Color(0.18f, 0.12f, 0.08f),
                           new Color(0.23f, 0.16f, 0.10f), new Color(0.28f, 0.20f, 0.13f) },
            Top = new(0.42f, 0.31f, 0.19f), TopLight = new(0.52f, 0.40f, 0.25f),
            Seam = new(0.27f, 0.18f, 0.11f), Rim = new(0.33f, 0.24f, 0.15f),
        };

        static readonly Pal SplitBark = new()
        {
            Ramp = new[] { new Color(0.17f, 0.12f, 0.08f), new Color(0.24f, 0.17f, 0.11f), new Color(0.31f, 0.22f, 0.14f),
                           new Color(0.38f, 0.28f, 0.18f), new Color(0.45f, 0.34f, 0.22f) },
            Top = new(0.55f, 0.42f, 0.27f), TopLight = new(0.64f, 0.50f, 0.33f),
            Seam = new(0.36f, 0.26f, 0.16f), Rim = new(0.46f, 0.34f, 0.22f),
        };

        static readonly Color[] Foliage =
        {
            new(0.04f, 0.08f, 0.05f), new(0.07f, 0.13f, 0.07f), new(0.10f, 0.19f, 0.09f),
            new(0.14f, 0.26f, 0.11f), new(0.21f, 0.34f, 0.14f), new(0.30f, 0.43f, 0.18f),
        };
        static readonly Color MossTrunk = new(0.16f, 0.25f, 0.11f), Fungus = new(0.72f, 0.58f, 0.36f), FungusDark = new(0.48f, 0.36f, 0.22f);

        /// <summary>The root flare's own top is hidden under the trunk and the soil: drawn in the
        /// bark's mid tones, or it read as a cut stump at every tree's foot.</summary>
        static Pal Flare(in Pal p) => new() { Ramp = p.Ramp, Top = p.Ramp[1], TopLight = p.Ramp[2], Seam = p.Ramp[0], Rim = p.Ramp[1] };

        static Sprite Tree(ColumnStyle style, ColumnSize size, bool cracked, int variant)
        {
            var rng = new System.Random(((int)size * 31 + variant * 7 + (cracked ? 101 : 0) + (int)style * 13) * 7919 + 211);
            float r = Column.RadiusFor(size) * Ppu;
            float height = HeightOf(style, size, cracked) * Ppu;
            var pal = cracked ? SplitBark : BarkPal;
            var g = new Grid();

            if (style == ColumnStyle.Tall)
            {
                float trunkR = r * 0.62f;
                float crown = height * 0.62f;               // where the leaves begin
                g.Cylinder(r * 0.86f, 0f, Mathf.Max(6f, r * 0.18f), Flare(pal));             // the root flare
                g.Cylinder(trunkR, 0f, crown + r * 0.4f, pal, flutes: 3);                       // the trunk, bark furrows
                g.TrunkMoss(trunkR, crown * 0.45f, rng);
                if (cracked) g.Shelf(trunkR, crown * 0.42f, rng);
                g.Canopy(0f, crown + r * TreeCanopyWidth * 0.55f, r * TreeCanopyWidth, r * TreeCanopyWidth * 0.82f, rng);
                g.Outline();
                if (cracked) g.Fissures(rng, 3, (int)(-trunkR * 0.7f), (int)(trunkR * 0.6f), (int)crown, 6, pal.Ramp[4]);
            }
            else
            {
                // A stump: snapped off, the rings on its jagged top, chips at its foot.
                float trunkR = r * 0.72f;
                int side = rng.NextDouble() < 0.5 ? -1 : 1;
                int n = (int)trunkR + 2;
                var noise = new float[2 * n + 1];
                float v = 0f;
                for (int i = 0; i < noise.Length; i++) { v = Mathf.Clamp(v + Range(rng, -1.8f, 1.8f), -4f, 4f); noise[i] = v; }
                float slope = trunkR * 0.25f * side;
                float Jag(float u) => slope * u + noise[Mathf.Clamp(Mathf.RoundToInt(u * trunkR) + n, 0, noise.Length - 1)];
                g.Cylinder(r * 0.9f, 0f, Mathf.Max(6f, r * 0.2f), Flare(pal));
                g.Cylinder(trunkR, 0f, height * 0.55f, pal, flutes: 3, jag: Jag, fracture: rng);
                g.Rings(trunkR, height * 0.55f);
                g.TrunkMoss(trunkR, height * 0.3f, rng);
                if (cracked) g.Shelf(trunkR, height * 0.3f, rng);
                g.Rubble(rng, r, pal, cracked ? 5 : 3);
                g.Outline();
            }
            return g.ToSprite();
        }

        sealed partial class Grid
        {
            /// <summary>Leaf masses: a lumpy ellipse built of overlapping clumps, each lit from the
            /// upper left - drawn last, over the trunk's top.</summary>
            public void Canopy(float cx, float cy, float rx, float ry, System.Random rng)
            {
                int clumps = 9 + rng.Next(5);
                var centres = new List<Vector3>();
                for (int i = 0; i < clumps; i++)
                {
                    float a = Range(rng, 0f, Mathf.PI * 2f), d = Mathf.Sqrt(Range(rng, 0f, 1f)) * 0.62f;
                    float cr = Range(rng, 0.38f, 0.55f);
                    centres.Add(new Vector3(cx + Mathf.Cos(a) * d * rx, cy + Mathf.Sin(a) * d * ry, cr * Mathf.Min(rx, ry * 1.2f)));
                }
                // one big middle clump so it is never hollow
                centres.Add(new Vector3(cx, cy, Mathf.Min(rx, ry) * 0.62f));
                // far clumps first (higher up), near ones over them
                centres.Sort((p, q) => q.y.CompareTo(p.y));
                foreach (var c in centres)
                {
                    int R = Mathf.CeilToInt(c.z);
                    for (int y = -R; y <= R; y++)
                    for (int x = -R; x <= R; x++)
                    {
                        float d = (x * x + y * y) / (c.z * c.z);
                        if (d > 1f) continue;
                        // lit from the upper left; a darker under-rim
                        float lit = 0.62f - (x - y) / (2.6f * c.z) - d * 0.35f;
                        if (y < -c.z * 0.55f) lit -= 0.18f;
                        float grain = ((x * 7 + y * 13 + (int)c.x * 3) & 3) == 0 ? 0.08f : 0f;   // leaf texture
                        int idx = Mathf.Clamp(Mathf.FloorToInt((lit + grain) * Foliage.Length), 0, Foliage.Length - 1);
                        Put((int)c.x + x, (int)c.y + y, d > 0.86f ? Foliage[Mathf.Max(0, idx - 2)] : Foliage[idx]);
                    }
                }
            }

            /// <summary>Ivy wound round a Sky Ruin column: a spiral of leaves up the face.</summary>
            public void Vines(float r, float H, System.Random rng)
            {
                var vine = new Color(0.22f, 0.38f, 0.16f); var lit = new Color(0.34f, 0.52f, 0.22f);
                float phase = Range(rng, 0f, 6.28f);
                for (int y = 4; y < (int)(H * 0.9f); y++)
                {
                    int x = Mathf.RoundToInt(Mathf.Sin(y * 0.09f + phase) * r * 0.7f);
                    if (!Has(x, y) || Mathf.Cos(y * 0.09f + phase) < -0.2f) continue;   // only the turns in front
                    Put(x, y, vine);
                    if (y % 5 == 0 && Has(x + 1, y)) { Put(x + 1, y, lit); if (Has(x + 1, y + 1)) Put(x + 1, y + 1, lit); }
                    if (y % 7 == 0 && Has(x - 1, y)) Put(x - 1, y, vine);
                }
            }

            /// <summary>Moss on the shaded side of the trunk, up to <paramref name="upTo"/>.</summary>
            public void TrunkMoss(float r, float upTo, System.Random rng)
            {
                int n = Mathf.CeilToInt(r);
                for (int x = -n; x < n; x++)
                {
                    float u = (x + 0.5f) / r;
                    if (u < 0.25f || u >= 1f) continue;                   // the shaded (right) side
                    int top = (int)(upTo * Range(rng, 0.4f, 1f));
                    for (int y = 0; y < top; y++)
                        if (Has(x, y) && (x * 3 + y) % 4 != 0) Put(x, y, MossTrunk);
                }
            }

            /// <summary>A bracket fungus on the trunk's lit side - a breakable tree is a rotten one.</summary>
            public void Shelf(float r, float y0, System.Random rng)
            {
                int x0 = -(int)(r * 0.9f);
                for (int y = 0; y < 3; y++)
                for (int x = -6 + y; x <= 1; x++)
                    Put(x0 + x, (int)y0 + y, y == 2 ? Fungus : FungusDark);
            }

            /// <summary>Growth rings on a stump's top, read from the top's own texels.</summary>
            public void Rings(float r, float topY)
            {
                var keys = new List<Vector2Int>(_px.Keys);
                foreach (var p in keys)
                {
                    float dy = (p.y - topY) / (r * Squash);
                    if (Mathf.Abs(dy) > 1.2f) continue;
                    float d = Mathf.Sqrt((p.x / r) * (p.x / r) + dy * dy);
                    if (d > 0.92f) continue;
                    if (Mathf.Abs(Mathf.Repeat(d * 4f, 1f) - 0.5f) < 0.14f) Put(p.x, p.y, BarkPal.Seam);
                }
            }
        }
    }
}
