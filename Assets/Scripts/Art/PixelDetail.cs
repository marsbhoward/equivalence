using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art
{
    /// <summary>
    /// Turns a grid into a higher-density version of ITSELF, for the character screen.
    ///
    /// The project's standing rule is that block-upscaling buys nothing: doubling a 16x14 head to
    /// 32x28 and drawing it at twice the ppu is provably the same image, measured at zero
    /// differing screen pixels across an eighth-texel sweep. That rule is about a PURE scale-up,
    /// and it still holds. This is not one - the scale is only the canvas, and what follows is
    /// what the extra cells are actually spent on:
    ///
    ///   1. the outline is SMOOTHED, so a diagonal stops being a staircase of whole blocks
    ///   2. every tone ramp gets ONE LIGHT - bright facing up and left, dark facing right,
    ///      darkest along the bottom edge
    ///
    /// Neither adds a FEATURE, and that line matters. The earlier 32x28 head attempt crossed it -
    /// it grew a nose and a shaded crown to justify the cells - and stopped being this character.
    /// A piece put through here is the same piece with a light on it.
    ///
    /// DERIVED rather than authored, so one source of truth stays one. Hand-drawing menu art for
    /// every slot would be a second full set of grids and a second chance for the character on the
    /// sheet to stop matching the one in the arena.
    /// </summary>
    public static class PixelDetail
    {
        /// <summary>
        /// The tone ramps a grid can use, darkest to brightest, by the convention in Palette:
        /// lowercase is the main material, uppercase a second, digits a third.
        /// </summary>
        static readonly string[] Ramps = { "ksdblh", "KSDBLH", "123456" };

        public static string[] Enrich(string[] rows, int scale)
        {
            if (rows == null || rows.Length == 0 || scale < 2) return rows;

            var big = Upscale(rows, scale);
            SmoothOutline(big, rows, scale);
            foreach (var ramp in Ramps) Shade(big, ramp);
            return big;
        }

        /// <summary>
        /// The scale-up with its corners chamfered and NO light - for a material that is itself
        /// the light, where shading toward one would invent a surface that isn't there.
        /// </summary>
        public static string[] Smooth(string[] rows, int scale)
        {
            if (rows == null || rows.Length == 0 || scale < 2) return rows;

            var big = Upscale(rows, scale);
            SmoothOutline(big, rows, scale);
            return big;
        }

        public static string[] Upscale(string[] rows, int k)
        {
            var big = new string[rows.Length * k];
            for (int y = 0; y < rows.Length; y++)
            {
                var sb = new System.Text.StringBuilder(rows[y].Length * k);
                foreach (var c in rows[y]) sb.Append(c, k);
                for (int r = 0; r < k; r++) big[y * k + r] = sb.ToString();
            }
            return big;
        }

        public static char At(string[] rows, int x, int y)
            => y < 0 || y >= rows.Length || x < 0 || x >= rows[y].Length ? '.' : rows[y][x];

        public static void Put(string[] rows, int x, int y, char c)
        {
            if (y < 0 || y >= rows.Length || x < 0 || x >= rows[y].Length) return;
            var line = rows[y].ToCharArray();
            line[x] = c;
            rows[y] = new string(line);
        }

        /// <summary>
        /// Chamfer the corners the scale-up squared off.
        ///
        /// At 4x every diagonal in the source becomes a four-texel step, which is the loudest tell
        /// that a sprite was enlarged rather than drawn. Where the source turns a corner, the
        /// outermost texel of that block is dropped - one texel deep, which at this density reads
        /// as a curve rather than as a bite taken out of the shape.
        /// </summary>
        public static void SmoothOutline(string[] big, string[] flat, int k)
        {
            int h = flat.Length;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < flat[y].Length; x++)
            {
                if (At(flat, x, y) == '.') continue;
                foreach (var (dx, dy) in Corners)
                {
                    if (At(flat, x + dx, y) != '.' || At(flat, x, y + dy) != '.') continue;
                    if (At(flat, x + dx, y + dy) != '.') continue;
                    Put(big, x * k + (dx < 0 ? 0 : k - 1), y * k + (dy < 0 ? 0 : k - 1), '.');
                }
            }
        }

        static readonly (int dx, int dy)[] Corners = { (-1, -1), (1, -1), (-1, 1), (1, 1) };

        /// <summary>How far a texel of this tone is from the edge of its own region, one way.</summary>
        static int Reach(string[] big, int x, int y, int dx, int dy, char tone, int cap)
        {
            for (int d = 1; d <= cap; d++)
                if (At(big, x + dx * d, y + dy * d) != tone) return d;
            return cap + 1;
        }

        /// <summary>
        /// One consistent light across a ramp: every tone shifts a step BRIGHTER where the shape
        /// faces up and left, and a step darker where it faces right or sits on the bottom edge.
        ///
        /// This is the change that makes a flat block read as a surface, and it is most of what
        /// separates hand-shaded pixel art from a scaled-up sprite - not more colours, one light.
        ///
        /// It shifts ALONG THE RAMP rather than writing fixed tones, which matters twice over.
        /// The first version only touched the base tone, and on gear that is nearly nothing: these
        /// grids are already bands of light-base-dark, so there was rarely a big enough field of
        /// base for a light to fall across and the pass came out as chamfered corners and little
        /// else. Shifting instead means a piece's existing bands keep their relationships and the
        /// whole thing turns toward the light together.
        ///
        /// The LINE tone is never shifted - it is the silhouette, and lighting it eats the edge.
        /// The glow is never shifted up out of the ramp either; there is nothing above it.
        ///
        /// Worked in the UPSCALED grid, so the bands are continuous and follow the outline.
        /// Applied per source cell instead, every block got its own identical stripe and the
        /// result read as corduroy.
        /// </summary>
        public static void Shade(string[] big, string ramp)
        {
            var outp = (string[])big.Clone();
            for (int y = 0; y < big.Length; y++)
            for (int x = 0; x < big[y].Length; x++)
            {
                char c = At(big, x, y);
                int idx = ramp.IndexOf(c);
                if (idx < 1) continue;              // '.' or the line tone: leave alone

                // Distance to the edge of THIS TONE'S own region, per direction.
                int up = Reach(big, x, y, 0, -1, c, 3);
                int left = Reach(big, x, y, -1, 0, c, 3);
                int right = Reach(big, x, y, 1, 0, c, 3);
                int down = Reach(big, x, y, 0, 1, c, 3);

                int shift = 0;
                if (Mathf.Min(up, left) <= 1) shift = 1;
                if (right <= 1) shift = -1;
                // The bottom wins over everything else: a surface lit from below is the one thing
                // that reads as wrong rather than merely flat.
                if (down <= 1) shift = -1;

                if (shift != 0) Put(outp, x, y, ramp[Mathf.Clamp(idx + shift, 1, ramp.Length - 1)]);
            }
            for (int y = 0; y < big.Length; y++) big[y] = outp[y];
        }
    }
}
