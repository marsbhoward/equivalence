using System.Collections.Generic;

namespace Convergence.Art
{
    /// <summary>
    /// Hair drawn for the two views the rig actually shows: TURNED (the 3/4 view the head is always
    /// held in, authored facing right - the rig's mirror handles left) and FACING AWAY.
    ///
    /// Every style STARTS FROM ITS FACE-ON ORIGINAL (kept in Tools/hair/legacy/BodyLook_faceon.cs)
    /// and changes only what the view requires. Turned: the fringe moves one cell with the gaze, the
    /// far side's hair recedes behind the cheek (it would cover the far eye), and the back of the
    /// head fills out a cell. From behind: the style's own outline, filled where the face was, down
    /// to a nape; long styles close into a curtain. A redraw that went further - new shapes, finer
    /// detail - read as a different set of haircuts and was thrown away. Don't drift. (Backswept,
    /// added later from a reference, has no original - its two views ARE the design.)
    ///
    /// DENSITY: THE HEAD'S DESIGN DENSITY, not the body's. The head sprite is drawn at the body's
    /// ppu, but everything on it - skull outline, eyes, brows, beards - is the 16x14-era design
    /// block-doubled, stepping in 2-texel units. So the grids are written ONE CHARACTER PER DESIGN
    /// CELL (16 wide) and doubled on load: a detail finer than the face's own pixels cannot be drawn
    /// at all. Drawn in single texels beside a face in doubles, the hair was two pixel sizes on one
    /// head, and the extra resolution invited detail that changed the characters.
    ///
    /// FORMAT (cells). <see cref="HairView.Cap"/> is the hair on the head sprite: 16 x 18, FINAL
    /// composed rows (the two overhead rows included, and the two lower-face rows Expand inserts -
    /// so no Expand here). It is COMPOSED into the head like any other layer and goes through the
    /// same passes (outline thinning, silhouette rounding, the hair pass). <see cref="HairView.Mane"/>
    /// is everything past the head sprite, on RigLayer.HairBack, top edge <see cref="HairView.ManeTop"/>
    /// TEXELS above the neck joint, centred on the head. Only occupancy, 'K' outlines and gaps
    /// survive the hair pass (FaceDetail.Hair re-lights the rest); lowercase skin tones (Fade's
    /// faded sides) are kept as drawn.
    ///
    /// The grids are in BodyLook.HairArt.cs; Tools/hair/compare.py sets them beside the originals.
    /// </summary>
    public static partial class BodyLook
    {
        public sealed class HairView
        {
            /// <summary>In texels - doubled from the cells they were written in.</summary>
            public readonly string[] Cap, Mane;
            public readonly int ManeTop;

            public HairView(string[] capCells, string[] maneCells = null, int maneTop = ManeHeadTop)
            {
                Cap = Double(capCells);
                Mane = maneCells == null ? null : Double(maneCells);
                ManeTop = maneTop;
            }

            static string[] Double(string[] cells)
            {
                var rows = new string[cells.Length * 2];
                for (int y = 0; y < cells.Length; y++)
                {
                    var sb = new System.Text.StringBuilder(cells[y].Length * 2);
                    foreach (char c in cells[y]) sb.Append(c, 2);
                    rows[y * 2] = rows[y * 2 + 1] = sb.ToString();
                }
                return rows;
            }
        }

        /// <summary>The style's hair for one view, by its exact key (normalise through
        /// <see cref="HairStyle"/> first); null only for a key no style has.</summary>
        public static HairView View(string hairStyle, bool back)
        {
            var table = back ? BackArt : SideArt;
            return table != null && hairStyle != null && table.TryGetValue(hairStyle, out var v) ? v : null;
        }

        /// <summary>
        /// The back of the head: the bare skull with the style's BACK cap composed over it, through
        /// the same passes the front head goes through, so the two views are one head of hair.
        /// Null when the style has no back view.
        ///
        /// <paramref name="tie"/> knots a cloth band round the head (GearItem.HasTieBack), in
        /// digit chars - the THIRD ramp of the palette the caller hands in. Laid on after the hair
        /// pass so the pass cannot re-light cloth as if it were a lock of hair.
        /// </summary>
        public static string[] HeadBackDetail(string hairStyle, string hair, int scale, bool tie)
        {
            var view = View(hairStyle, back: true);
            if (view == null) return null;

            var flat = Compose(Expand(Skull, solid: true), view.Cap);
            var noFace = new string[flat.Length];
            for (int i = 0; i < noFace.Length; i++) noFace[i] = new string('.', flat[i].Length);

            var big = FaceDetail.Build(flat, noFace, scale, HairValue(hair), view.Mane != null);
            if (tie) StampTie(big, scale);
            return big;
        }

        /// <summary>
        /// The tie-back's band and tails, in composed back-head texels: the band across the head
        /// at the brow line, two tails down the temples past the jaw, drifting out one row-pair
        /// apart (staggered reads as cloth settling; in step reads as a decal).
        /// </summary>
        static void StampTie(string[] big, int scale)
        {
            var cells = new List<(int x, int y, char c)>();
            for (int x = 2; x <= 29; x++)
            {
                char c = x is 14 or 15 or 16 or 17 ? '5' : '4';   // the knot catches the light
                cells.Add((x, 22, c)); cells.Add((x, 23, c));
            }
            for (int y = 24; y <= 29; y++)
                foreach (int x in new[] { 2, 3, 4, 5, 26, 27, 28, 29 }) cells.Add((x, y, '4'));
            for (int y = 30; y <= 31; y++)
                foreach (int x in new[] { 4, 5, 26, 27 }) cells.Add((x, y, '4'));
            for (int y = 32; y <= 35; y++)
                foreach (int x in new[] { 2, 3, 28, 29 }) cells.Add((x, y, '4'));

            foreach (var (x, y, c) in cells)
            for (int dy = 0; dy < scale; dy++)
            for (int dx = 0; dx < scale; dx++)
                PixelDetail.Put(big, x * scale + dx, y * scale + dy, c);
        }
    }
}
