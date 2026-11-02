using System;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// What the arena's transmutation circle (<see cref="CircleArt"/>) and the hub's
    /// (Hub.HubCircleArt) draw alike (the user's picks off the circle mock, 2026-11-01): today's two
    /// grooves with the title's sigil cut inside, the three principles' glyphs DRAWN at the
    /// character's density (<see cref="Tuning.Circle.Ppu"/>), the Tree of Life over the sigil, and
    /// the ring of light that gathers in from the rim. Each owner decides WHEN; this only draws.
    /// </summary>
    public abstract class CircleFigureArt : Combat.TexelPicture
    {
        protected static readonly Color32 GoldLit = new(242, 194, 90, 255), GoldHot = new(255, 227, 154, 255), White = new(255, 255, 255, 255);
        protected const float Tau = Mathf.PI * 2f;

        /// <summary>The glyphs sit where today's did: at 90, 210 and 330 degrees, between the grooves.</summary>
        protected static readonly float[] GlyphAngles = { 90f, 210f, 330f };

        /// <summary>Clockwise from the top, 0..2pi - how the rim is lit a third at a time.</summary>
        protected static float FromTop(float x, float y) => Mathf.Repeat(Mathf.PI * 0.5f - Mathf.Atan2(y, x), Tau);

        protected float Outer(float radius) => radius * Ppu;
        protected static float Inner(float r) => r * Tuning.Circle.InnerShare;
        protected static float Band(float r) => (r - Tuning.Circle.OuterGroove + Inner(r)) * 0.5f;

        protected static Vector2 Polar(float r, float deg) => new(Mathf.Cos(deg * Mathf.Deg2Rad) * r, Mathf.Sin(deg * Mathf.Deg2Rad) * r);

        // ---- grooves ---------------------------------------------------------------------------------

        /// <summary>A groove cut in the floor: its dark bed, and a one-texel lip lit on the far side
        /// (the outer edge above the centre, the inner edge below), as today's.</summary>
        protected void Groove(float r, int w, Vector2 at, Func<int, int, (Color32 bed, Color32 lip)> tones, float a)
            => Ring(at.x, at.y, r - w, r, (x, y, d, _) =>
            {
                var (bed, lip) = tones(x, y);
                Over(x, y, (y - Mathf.RoundToInt(at.y) > 0 ? d > r - 1f : d < r - w + 1f) ? lip : bed, a);
            });

        /// <summary>A groove painted one colour, only where <paramref name="keep"/> says.</summary>
        protected void Paint(float r, int w, Vector2 at, Color32 c, float a, Func<int, int, bool> keep = null)
            => Ring(at.x, at.y, r - w, r, (x, y, _, _) => { if (keep == null || keep(x, y)) Over(x, y, c, a); });

        /// <summary>The title's sigil (four rings of 47 at 53 from the centre, 212 across) fitted inside
        /// the inner groove. Their centres, and their radius.</summary>
        protected static (Vector2[] at, float r) SigilRings(float R)
        {
            float k = (Inner(R) - 6f) / 100f, sd = 53f * k;
            return (new[] { new Vector2(0f, sd), new Vector2(-sd, 0f), new Vector2(sd, 0f), new Vector2(0f, -sd) }, 47f * k);
        }

        // ---- the glyphs ----------------------------------------------------------------------------

        /// <summary>Sulfur, Mercury, Salt as strokes in a unit box (y up): segments (ax, ay, bx, by) and
        /// arcs (x, y, r, from, to radians) - rasterised at whatever density the circle is drawn.</summary>
        static readonly float[][][] Segs =
        {
            new[] { new[] { 0f, .95f, -.72f, -.1f }, new[] { 0f, .95f, .72f, -.1f }, new[] { -.72f, -.1f, .72f, -.1f }, new[] { 0f, -.1f, 0f, -.98f }, new[] { -.42f, -.56f, .42f, -.56f } },
            new[] { new[] { 0f, -.29f, 0f, -.98f }, new[] { -.34f, -.66f, .34f, -.66f } },
            new[] { new[] { -.82f, 0f, .82f, 0f } },
        };
        static readonly float[][][] Arcs =
        {
            new float[0][],
            new[] { new[] { 0f, .5f, .42f, Mathf.PI * 1.08f, Mathf.PI * 1.92f }, new[] { 0f, .05f, .34f, 0f, Tau } },
            new[] { new[] { 0f, 0f, .82f, 0f, Tau } },
        };

        /// <summary>Glyph <paramref name="i"/> centred at (<paramref name="gx"/>, <paramref name="gy"/>) texels.</summary>
        protected void Glyph(int i, float gx, float gy, Color32 c, float a)
        {
            float half = Tuning.Circle.GlyphHalf * Ppu;
            int e = Mathf.CeilToInt(half) + 1, ox = Mathf.RoundToInt(gx), oy = Mathf.RoundToInt(gy);
            for (int y = -e; y <= e; y++)
                for (int x = -e; x <= e; x++)
                {
                    float ux = x / half, uy = y / half, d = float.MaxValue;
                    foreach (var s in Segs[i]) d = Mathf.Min(d, SegDist(ux, uy, s[0], s[1], s[2], s[3]));
                    foreach (var r in Arcs[i]) d = Mathf.Min(d, ArcDist(ux, uy, r));
                    if (d * half <= Tuning.Circle.GlyphStroke) Over(ox + x, oy + y, c, a);
                }
        }

        static float SegDist(float x, float y, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy));
            return Mathf.Sqrt((x - ax - vx * t) * (x - ax - vx * t) + (y - ay - vy * t) * (y - ay - vy * t));
        }

        static float ArcDist(float x, float y, float[] a)
        {
            float ang = Mathf.Repeat(Mathf.Atan2(y - a[1], x - a[0]), Tau);
            if (a[4] - a[3] >= Tau - 1e-4f || (ang >= a[3] && ang <= a[4]))
                return Mathf.Abs(Mathf.Sqrt((x - a[0]) * (x - a[0]) + (y - a[1]) * (y - a[1])) - a[2]);
            float e0 = Vector2.Distance(new Vector2(x, y), new Vector2(a[0] + Mathf.Cos(a[3]) * a[2], a[1] + Mathf.Sin(a[3]) * a[2]));
            float e1 = Vector2.Distance(new Vector2(x, y), new Vector2(a[0] + Mathf.Cos(a[4]) * a[2], a[1] + Mathf.Sin(a[4]) * a[2]));
            return Mathf.Min(e0, e1);
        }

        // ---- the Tree of Life ------------------------------------------------------------------------

        /// <summary>The ten spheres on three pillars, in the order they are drawn: the LEFT pillar (0),
        /// the RIGHT (1), then the MIDDLE (2), which joins them. (column, row, pillar)</summary>
        static readonly (int x, int y, int pillar)[] Spheres =
        {
            (0, 0, 2), (1, 1, 1), (-1, 1, 0), (1, 3, 1), (-1, 3, 0), (0, 4, 2), (1, 5, 1), (-1, 5, 0), (0, 6, 2), (0, 8, 2),
        };
        /// <summary>The twenty-two paths, as indices into <see cref="Spheres"/>.</summary>
        static readonly (int a, int b)[] Paths =
        {
            (0, 1), (0, 2), (0, 5), (1, 2), (1, 5), (1, 3), (2, 5), (2, 4), (3, 4), (3, 5), (3, 6),
            (4, 5), (4, 7), (5, 6), (5, 8), (5, 7), (6, 7), (6, 8), (6, 9), (7, 8), (7, 9), (8, 9),
        };

        static Vector2 SpherePos(int i, float rt)
        {
            float u = 0.1675f * rt;
            return new Vector2(Spheres[i].x * 2.05f * u, 0.74f * rt - Spheres[i].y * u);
        }
        static float SphereR(float rt) => 0.062f * rt;

        /// <summary>
        /// The tree fitted inside the inner groove (radius <paramref name="rt"/>). <paramref name="drawn"/>
        /// says how far each pillar is drawn, 0..1: a path extends once the pillar that completes it is
        /// drawing, a sphere traces round from the top. <paramref name="colour"/> per pillar;
        /// <paramref name="keep"/> limits it to some texels (the gathering ring passing over).
        /// </summary>
        protected void Tree(float rt, Func<int, float> drawn, Func<int, Color32> colour, float a, Func<int, int, bool> keep = null)
        {
            float sr = SphereR(rt), half = Tuning.Circle.TreeStroke * 0.5f;
            foreach (var (pa, pb) in Paths)
            {
                int pillar = Mathf.Max(Spheres[pa].pillar, Spheres[pb].pillar);
                float f = drawn(pillar);
                if (f <= 0f) continue;
                Vector2 A = SpherePos(pa, rt), B = SpherePos(pb, rt), dir = (B - A).normalized;
                A += dir * (sr + 1f); B -= dir * (sr + 1f);
                Stroke(A, A + (B - A) * Mathf.Clamp01(f), half, colour(pillar), a, keep);
            }
            for (int i = 0; i < Spheres.Length; i++)
            {
                float f = drawn(Spheres[i].pillar);
                if (f <= 0f) continue;
                var c = SpherePos(i, rt);
                var col = colour(Spheres[i].pillar);
                Ring(c.x, c.y, sr - Tuning.Circle.TreeStroke - 1f, sr, (x, y, _, _) =>
                {
                    if (FromTop(x - c.x, y - c.y) > f * Tau) return;
                    if (keep == null || keep(x, y)) Over(x, y, col, a);
                });
            }
        }

        /// <summary>A straight stroke <paramref name="half"/> texels either side of A-B, each texel once.</summary>
        protected void Stroke(Vector2 A, Vector2 B, float half, Color32 c, float a, Func<int, int, bool> keep = null)
        {
            int x0 = Mathf.FloorToInt(Mathf.Min(A.x, B.x) - half - 1), x1 = Mathf.CeilToInt(Mathf.Max(A.x, B.x) + half + 1);
            int y0 = Mathf.FloorToInt(Mathf.Min(A.y, B.y) - half - 1), y1 = Mathf.CeilToInt(Mathf.Max(A.y, B.y) + half + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (SegDist(x, y, A.x, A.y, B.x, B.y) <= half && (keep == null || keep(x, y))) Over(x, y, c, a);
        }

        // ---- the ring of light ------------------------------------------------------------------------

        /// <summary>
        /// The gathering: a ring of light at <paramref name="waveR"/> closing on the centre, a gold
        /// trail inside it, and white on whatever of the circle it is passing (the grooves, the sigil,
        /// the glyphs and the tree are the owner's to pass in).
        /// </summary>
        protected void GatheringRing(float R, float waveR, float a)
        {
            float w = Tuning.Circle.TreeStroke;
            Ring(0f, 0f, Mathf.Max(0f, waveR - 2f * w), waveR, (x, y, d, _) => { if (d < R) Over(x, y, GoldHot, 0.8f * a); });
            Ring(0f, 0f, Mathf.Max(0f, waveR - 5f * w), Mathf.Max(0f, waveR - 2f * w), (x, y, d, _) => { if (d < R) Over(x, y, GoldLit, 0.25f * a); });
        }

        /// <summary>Where the gathering ring is, <paramref name="t"/> seconds in (R past the rim at 0, 0 at the end).</summary>
        protected static float WaveR(float R, float t) => R * 1.06f * (1f - Mathf.Clamp01(t / Tuning.Circle.WaveSeconds));

        /// <summary>The tree's colour and strength once the ring arrives: white, then a hot gold settling.</summary>
        protected static (Color32 c, float a) Flare(float sinceArrival)
        {
            if (sinceArrival < Tuning.Circle.FlareWhite) return (White, 1f);
            float k = (sinceArrival - Tuning.Circle.FlareWhite) / Tuning.Circle.FlareSettle;
            return (GoldHot, Mathf.Max(Tuning.Circle.FlareFloor, 1f - k));
        }

        /// <summary>How strongly the ring lights something at radius <paramref name="r"/> (1 on it, 0 a band away).</summary>
        protected static float OnWave(float R, float waveR, float r)
            => Mathf.Clamp01(1f - Mathf.Abs(r - waveR) / (Tuning.Circle.WaveBand * R));

        /// <summary>Draw the whole figure's light at the ring: the grooves, the sigil, the glyph spots and the
        /// tree texels under it, in white.</summary>
        protected void LightUnderRing(float R, float waveR, float a, bool tree)
        {
            float Ri = Inner(R);
            float o = OnWave(R, waveR, R);
            if (o > 0f) Paint(R, Tuning.Circle.OuterGroove, Vector2.zero, White, o * a);
            o = OnWave(R, waveR, Ri);
            if (o > 0f) Paint(Ri, Tuning.Circle.InnerGroove, Vector2.zero, White, o * a);
            var (at, sr) = SigilRings(R);
            foreach (var c in at)
            {
                o = OnWave(R, waveR, c.magnitude);
                if (o > 0f) Paint(sr, Tuning.Circle.SigilGroove, c, White, o * a);
            }
            if (tree)
            {
                float band = Tuning.Circle.WaveBand * R * 0.6f;
                Tree(Ri, _ => 1f, _ => White, a, (x, y) => Mathf.Abs(Mathf.Sqrt(x * x + y * y) - waveR) < band);
            }
        }

        /// <summary>Behind the ring its light is taken: true for something at radius <paramref name="r"/>.</summary>
        protected static bool Taken(float R, float waveR, float r) => r > waveR + Tuning.Circle.WaveBand * R * 0.5f;
    }
}
