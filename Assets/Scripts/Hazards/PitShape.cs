using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// A floor pit's SHAPE: a signed distance field over the cells it was laid on, rounded, so a
    /// pit can be any picture the pieces draw (Hazards.PitPieces) and still answer "is this point
    /// in it" exactly where it is drawn. Positive inside, in world units.
    ///
    /// ROUNDED IN THE FIELD, NOT IN THE ART. The cells are rasterised finely, blurred and cut at
    /// half (the open ground's own trick, Art.Biomes.Ground), then measured - so the hazard's edge
    /// and the drawn edge are the same curve, and <see cref="Contains"/> with the stand inset is
    /// the drawn shape pulled in, as the rect's test always was.
    ///
    /// SPILLING (edge water, the user's call 2026-11-18): a water piece laid against an open side
    /// is extended past the room's edge, so the field never closes there - no lip, no recess
    /// wall where the wading water runs out to the lake. <see cref="Clip"/> is the room; what is
    /// drawn past it is the shelf into the lake (WaterPit).
    ///
    /// Serializable with public fields only, so a pit's shape comes back from a domain reload with
    /// the pit (FloorPit's own rule: a lost footprint silently moves the hazard to the origin).
    /// </summary>
    [System.Serializable]
    public class PitShape
    {
        /// <summary>Field samples per world unit.</summary>
        public const int Res = 8;

        /// <summary>World position of the field's lower-left corner.</summary>
        public Vector2 Origin;
        public int W, H;
        /// <summary>Signed distance, world units, positive inside. Row-major, bottom row first.</summary>
        public float[] Sdf;
        /// <summary>The cells' own world rect - what a pit's Footprint reports.</summary>
        public Rect Bounds;
        /// <summary>The room: nothing of the pit is drawn past it but a spill's shelf.</summary>
        public Rect Clip;
        /// <summary>Which sides run on past <see cref="Clip"/> into the lake (bit 0 west, 1 east, 2 south).</summary>
        public int Spills;
        /// <summary>Square units inside - flame and jack counts scale with it.</summary>
        public float Area;

        public const int SpillWest = 1, SpillEast = 2, SpillSouth = 4;

        // ------------------------------------------------------------------ building

        /// <summary>
        /// A shape over room cells (<paramref name="cells"/>: x the column, y the row NORTH FIRST,
        /// as RoomShape's pictures are). A cell on the room's west, east or south border spills
        /// past it when <paramref name="spill"/> is set.
        /// </summary>
        public static PitShape FromCells(IReadOnlyList<Vector2Int> cells, bool spill = false)
        {
            var half = Arena.HalfExtents;
            float cw = half.x * 2f / RoomShape.Cols, ch = half.y * 2f / RoomShape.Rows;
            var rects = new List<Rect>();
            int spills = 0;
            float reach = Tuning.Hazards.PitSpillReach;
            Rect bounds = default;
            bool first = true;
            foreach (var c in cells)
            {
                var r = new Rect(-half.x + c.x * cw, half.y - (c.y + 1) * ch, cw, ch);
                rects.Add(r);
                bounds = first ? r : Rect.MinMaxRect(Mathf.Min(bounds.xMin, r.xMin), Mathf.Min(bounds.yMin, r.yMin),
                                                     Mathf.Max(bounds.xMax, r.xMax), Mathf.Max(bounds.yMax, r.yMax));
                first = false;
                if (!spill) continue;
                if (c.x == 0) { rects.Add(new Rect(r.xMin - reach, r.yMin, reach, r.height)); spills |= SpillWest; }
                if (c.x == RoomShape.Cols - 1) { rects.Add(new Rect(r.xMax, r.yMin, reach, r.height)); spills |= SpillEast; }
                if (c.y == RoomShape.Rows - 1) { rects.Add(new Rect(r.xMin, r.yMin - reach, r.width, reach)); spills |= SpillSouth; }
            }
            return Make(rects, bounds, spills, Tuning.Hazards.PitRoundRadius);
        }

        /// <summary>A rectangle, its corners softened a little - the pits the exchange lays (Tide,
        /// Encroach), which are placed freely rather than on cells.</summary>
        public static PitShape FromRect(Rect rect)
            => Make(new List<Rect> { rect }, rect, 0, Tuning.Hazards.PitRoundRadius * 0.6f);

        static PitShape Make(List<Rect> rects, Rect bounds, int spills, float round)
        {
            var all = rects[0];
            foreach (var r in rects)
                all = Rect.MinMaxRect(Mathf.Min(all.xMin, r.xMin), Mathf.Min(all.yMin, r.yMin),
                                      Mathf.Max(all.xMax, r.xMax), Mathf.Max(all.yMax, r.yMax));
            const float margin = 1f;
            var s = new PitShape
            {
                Origin = all.min - Vector2.one * margin,
                W = Mathf.CeilToInt((all.width + margin * 2f) * Res),
                H = Mathf.CeilToInt((all.height + margin * 2f) * Res),
                Bounds = bounds,
                Clip = new Rect(-Arena.HalfExtents, Arena.HalfExtents * 2f),
                Spills = spills,
            };
            int n = s.W * s.H;

            // Rasterise, then round: blurred twice (near a gaussian) and cut at half, so convex
            // corners round off and concave ones fill in by the same radius.
            var raw = new float[n];
            for (int y = 0; y < s.H; y++)
            for (int x = 0; x < s.W; x++)
            {
                var p = s.Origin + new Vector2((x + 0.5f) / Res, (y + 0.5f) / Res);
                foreach (var r in rects) if (r.Contains(p)) { raw[y * s.W + x] = 1f; break; }
            }
            int rad = Mathf.Max(0, Mathf.RoundToInt(round * Res));
            var soft = rad > 0 ? Blur(Blur(raw, s.W, s.H, rad), s.W, s.H, rad) : raw;
            var inside = new bool[n];
            int count = 0;
            for (int i = 0; i < n; i++) if (inside[i] = soft[i] > 0.5f) count++;
            s.Area = count / (float)(Res * Res);

            // Distances: a two-pass chamfer each way (to the nearest outside sample from inside,
            // and the reverse), half a sample taken off so the zero lies between the two.
            var dIn = Chamfer(inside, s.W, s.H, true);
            var dOut = Chamfer(inside, s.W, s.H, false);
            s.Sdf = new float[n];
            for (int i = 0; i < n; i++)
                s.Sdf[i] = inside[i] ? (dIn[i] - 0.5f) / Res : -(dOut[i] - 0.5f) / Res;
            return s;
        }

        static float[] Blur(float[] src, int w, int h, int rad)
        {
            var tmp = new float[w * h];
            var outp = new float[w * h];
            float k = 1f / (rad * 2 + 1);
            for (int y = 0; y < h; y++)
            {
                float sum = 0f;
                for (int i = -rad; i <= rad; i++) sum += src[y * w + Mathf.Clamp(i, 0, w - 1)];
                for (int x = 0; x < w; x++)
                {
                    tmp[y * w + x] = sum * k;
                    sum += src[y * w + Mathf.Clamp(x + rad + 1, 0, w - 1)] - src[y * w + Mathf.Clamp(x - rad, 0, w - 1)];
                }
            }
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int i = -rad; i <= rad; i++) sum += tmp[Mathf.Clamp(i, 0, h - 1) * w + x];
                for (int y = 0; y < h; y++)
                {
                    outp[y * w + x] = sum * k;
                    sum += tmp[Mathf.Clamp(y + rad + 1, 0, h - 1) * w + x] - tmp[Mathf.Clamp(y - rad, 0, h - 1) * w + x];
                }
            }
            return outp;
        }

        /// <summary>Distance (in samples) from each sample of the <paramref name="of"/> side to the
        /// nearest sample of the other - 8-neighbour chamfer, two passes.</summary>
        static float[] Chamfer(bool[] inside, int w, int h, bool of)
        {
            const float Big = 1e6f, D = 1.41421356f;
            var d = new float[w * h];
            for (int i = 0; i < d.Length; i++) d[i] = inside[i] == of ? Big : 0f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (d[i] == 0f) continue;
                float v = d[i];
                if (x > 0) v = Mathf.Min(v, d[i - 1] + 1f);
                if (y > 0)
                {
                    v = Mathf.Min(v, d[i - w] + 1f);
                    if (x > 0) v = Mathf.Min(v, d[i - w - 1] + D);
                    if (x < w - 1) v = Mathf.Min(v, d[i - w + 1] + D);
                }
                d[i] = v;
            }
            for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                int i = y * w + x;
                if (d[i] == 0f) continue;
                float v = d[i];
                if (x < w - 1) v = Mathf.Min(v, d[i + 1] + 1f);
                if (y < h - 1)
                {
                    v = Mathf.Min(v, d[i + w] + 1f);
                    if (x < w - 1) v = Mathf.Min(v, d[i + w + 1] + D);
                    if (x > 0) v = Mathf.Min(v, d[i + w - 1] + D);
                }
                d[i] = v;
            }
            return d;
        }

        // ------------------------------------------------------------------ asking

        /// <summary>Signed distance at <paramref name="p"/> (world units, positive inside),
        /// bilinear between samples. Far negative off the field.</summary>
        public float Sample(Vector2 p)
        {
            if (Sdf == null || Sdf.Length == 0) return -10f;
            float fx = (p.x - Origin.x) * Res - 0.5f, fy = (p.y - Origin.y) * Res - 0.5f;
            if (fx < 0f || fy < 0f || fx > W - 1 || fy > H - 1) return -10f;
            int x0 = (int)fx, y0 = (int)fy;
            int x1 = Mathf.Min(x0 + 1, W - 1), y1 = Mathf.Min(y0 + 1, H - 1);
            float tx = fx - x0, ty = fy - y0;
            float a = Mathf.Lerp(Sdf[y0 * W + x0], Sdf[y0 * W + x1], tx);
            float b = Mathf.Lerp(Sdf[y1 * W + x0], Sdf[y1 * W + x1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        /// <summary>Standing in it: inside by the stand inset (Tuning.Hazards.PitStandInset), so
        /// clipping the very edge is not being in it.</summary>
        public bool Contains(Vector2 p) => Sample(p) > Tuning.Hazards.PitStandInset;

        /// <summary>Within <paramref name="pad"/> of it - for a body's whole circle (routes, a
        /// tornado's ring, a column's rim).</summary>
        public bool Covers(Vector2 p, float pad) => Sample(p) > -pad;

        /// <summary>Whether <paramref name="p"/> is past a SPILLING side of the room - drawn as the
        /// shelf into the lake, never as the pit.</summary>
        public bool PastSpill(Vector2 p, out float past)
        {
            past = 0f;
            if (Spills == 0) return false;
            if ((Spills & SpillWest) != 0 && p.x < Clip.xMin) past = Mathf.Max(past, Clip.xMin - p.x);
            if ((Spills & SpillEast) != 0 && p.x > Clip.xMax) past = Mathf.Max(past, p.x - Clip.xMax);
            if ((Spills & SpillSouth) != 0 && p.y < Clip.yMin) past = Mathf.Max(past, Clip.yMin - p.y);
            return past > 0f;
        }

        /// <summary>The rect the art is baked over: the bounds, plus the drawn lip past a spill.</summary>
        public Rect DrawRect
        {
            get
            {
                float s = Tuning.Hazards.PitShelfDrawn;
                var r = Bounds;
                if ((Spills & SpillWest) != 0) r.xMin -= s;
                if ((Spills & SpillEast) != 0) r.xMax += s;
                if ((Spills & SpillSouth) != 0) r.yMin -= s;
                return r;
            }
        }
    }
}
