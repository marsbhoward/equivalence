namespace Convergence.Art
{
    // EIGHT DIRECTIONS (RIG.md, adopted 2026-11-09): the head's two NEW views - SIDE (profile, facing right) and
    // BACK 3/4 - built through the same composition and hair pass as the turned and back heads, so the new
    // views are judged exactly as the game will draw them. The hair for them is drafted per style in
    // Tools/hair/views5.py and fixed by hand; until a style's grids are written into HairArt they are passed in
    // (Editor HairViews5 loads them from a proposals file). Front and front 3/4 keep today's turned head.
    public static partial class BodyLook
    {
        /// <summary>
        /// The face SIDE-ON: only the NEAR half of every feature grid (expression, brow) survives, carried round to
        /// the front edge of the head, and the eye narrowed to its front two texels - an eye seen from the side.
        /// The face grids are authored symmetric, so the near half is the left half.
        /// </summary>
        static string[] ProfileFace(string[] face)
        {
            int w = face.Length > 0 ? face[0].Length : 0, half = w / 2;
            var outRows = new string[face.Length];
            for (int r = 0; r < face.Length; r++)
            {
                var chars = new string('.', w).ToCharArray();
                for (int c = 0; c < half && c < face[r].Length; c++)
                {
                    char ch = face[r][c];
                    if (ch == '.') continue;
                    int to = c + ProfileLead;
                    if (to < w) chars[to] = ch;
                }
                // narrow the eye to its front texels
                int last = -1;
                for (int c = w - 1; c >= 0; c--) if (chars[c] >= '1' && chars[c] <= '6') { last = c; break; }
                if (last >= 0) for (int c = 0; c <= last - ProfileEyeWidth; c++) if (chars[c] >= '1' && chars[c] <= '6') chars[c] = '.';
                outRows[r] = new string(chars);
            }
            return outRows;
        }

        /// <summary>How far the near half's features travel to reach the front of the head, composed texels.</summary>
        const int ProfileLead = 18;
        /// <summary>An eye seen from the side, composed texels wide.</summary>
        const int ProfileEyeWidth = 2;

        /// <summary>The TURNED head with <paramref name="cap"/> in place of the style's own - for reviewing a
        /// changed turned view (the same composition and pass as <see cref="HeadDetail"/>, no beard or mark).</summary>
        public static string[] HeadTurnedDetail(string expression, string brows, string hair, string[] cap, bool openTop,
                                                int scale = DetailScale, int gaze = 2, string[] mane = null, int maneTop = 0)
        {
            var face = Expand(Expression(expression).Grid, solid: false);
            if (gaze != 0) face = Gaze(face, gaze);
            var flat = Head(expression, "none", brows, gaze: gaze, hair: cap);
            var big = FaceDetail.Build(flat, face, scale, HairValue(hair), openTop, maneAbove: ManeAbove(mane, maneTop, flat));
            var drawn = DrawnEyeArt(expression);
            if (drawn != null) StampEyes(big, expression, gaze, scale, drawn, false);
            return big;
        }

        /// <summary>The head SIDE-ON with <paramref name="cap"/> (composed texel rows) for its hair. No beard yet.</summary>
        public static string[] HeadProfileDetail(string expression, string brows, string hair, string[] cap, bool openTop,
                                                 int scale = DetailScale, string[] mane = null, int maneTop = 0)
        {
            var face = Expression(expression).Grid;
            if (brows != "natural" && System.Array.IndexOf(BrowedExpressions, expression) >= 0) face = StripBrow(face);
            var featuresFace = ProfileFace(Expand(face, solid: false));
            var head = Compose(Expand(Skull, solid: true), featuresFace,
                               ProfileFace(Expand(Brow(brows).Grid, solid: false)), cap ?? new string[0]);
            return FaceDetail.Build(head, featuresFace, scale, HairValue(hair), openTop, maneAbove: ManeAbove(mane, maneTop, head));
        }

        /// <summary>The head from BEHIND, a step round: no face, <paramref name="cap"/> for its hair.</summary>
        public static string[] HeadBackQuarterDetail(string hair, string[] cap, bool openTop, int scale = DetailScale)
        {
            var flat = Compose(Expand(Skull, solid: true), cap ?? new string[0]);
            var noFace = new string[flat.Length];
            for (int i = 0; i < noFace.Length; i++) noFace[i] = new string('.', flat[i].Length);
            return FaceDetail.Build(flat, noFace, scale, HairValue(hair), openTop);
        }

        /// <summary>
        /// The head's texel columns a mane rises straight out of: hair in the mane's row just above the
        /// head's top. The outline pass leaves the head's top border open there instead of rimming it
        /// (FaceDetail.ThinOutline).
        /// </summary>
        static bool[] ManeAbove(string[] mane, int maneTop, string[] head)
        {
            if (mane == null || head == null || head.Length == 0) return null;
            int r = maneTop - ManeHeadTop - 1;
            if (r < 0 || r >= mane.Length) return null;
            int w = head[0].Length, off = (mane[r].Length - w) / 2;
            var open = new bool[w];
            for (int x = 0; x < w; x++)
            {
                int mx = x + off;
                open[x] = mx >= 0 && mx < mane[r].Length && mane[r][mx] != '.';
            }
            return open;
        }

        /// <summary>A mane (composed texel rows, top <paramref name="maneTop"/> texels above the neck) through the hair pass.</summary>
        public static string[] ManeFromRows(string[] mane, int maneTop, string hair, int scale = DetailScale)
            => mane == null ? null : FaceDetail.BuildHair(Clip(mane, maneTop, 1, false), scale, scale, maneTop, HairValue(hair));
    }
}
