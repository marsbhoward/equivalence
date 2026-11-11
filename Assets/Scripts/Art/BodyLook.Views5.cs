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
    
        // ================================================================ faces in the new views (review, 2026-11-11)
        //
        // Brows, beards and marks for SIDE and BACK 3/4, drafted from the face-on grids and then fixed by hand in
        // Tools/faces (Editor FaceViews5 loads them). All grids here are composed texels, 32 wide, BeardRows tall.

        /// <summary>A brow, beard or mark grid in composed space, face-on.</summary>
        public static string[] FaceGrid(string kind, string key) => kind switch
        {
            "brow" => Expand(Brow(key).Grid, solid: false),
            "beard" => BeardGrid(key),
            "mark" => Mark(key).Grid ?? Blank(),
            _ => Blank(),
        };

        static string[] Blank()
        {
            var rows = new string[BeardRows];
            for (int i = 0; i < rows.Length; i++) rows[i] = new string('.', 32);
            return rows;
        }

        /// <summary>The front-most column of the profile eye, composed texels.</summary>
        public static int ProfileEyeFront(string expression)
        {
            var f = ProfileFace(Expand(Expression(expression).Grid, solid: false));
            int last = -1;
            foreach (var row in f)
                for (int x = 0; x < row.Length; x++) if (row[x] >= '1' && row[x] <= '6' && x > last) last = x;
            return last;
        }

        /// <summary>
        /// First drafts, for hand fixing. Brow: the near brow carried to the front like the eye
        /// (<see cref="ProfileFace"/>) and cut to a short stroke over the profile eye. Beard and mark: the near
        /// half of the face-on grid STRETCHED from the ear to the front of the face (the jaw runs ear to chin).
        /// Back 3/4: whatever of the side draft lies on the cheek's back half, carried to the near edge.
        /// </summary>
        public static string[] DraftSide(string kind, string key, string expression = "neutral", bool otherWay = false)
        {
            var g = FaceGrid(kind, key);
            // FACING THE OTHER WAY the near side is the face-on grid's RIGHT half. The rig mirrors the right-facing
            // picture for left, which is only right for a symmetric part (Quizzical's raised brow, a scar over one
            // eye): drawn from the mirrored grid, then mirrored for display, the other side shows its own feature.
            if (otherWay) g = Mirror(g);
            if (kind == "brow")
            {
                var b = ProfileFace(g);
                int front = ProfileEyeFront(expression);
                for (int y = 0; y < b.Length; y++)
                {
                    var c = b[y].ToCharArray();
                    for (int x = 0; x < c.Length; x++) if (x < front - ProfileBrowLength + 1 || x > front + 1) c[x] = '.';
                    b[y] = new string(c);
                }
                return b;
            }
            // A mark sits on the face like the eye and brow, so it travels to the front with them.
            if (kind == "mark") return ProfileFace(g);
            int w = g[0].Length, half = w / 2;
            var outRows = new string[g.Length];
            for (int y = 0; y < g.Length; y++)
            {
                var c = new string('.', w).ToCharArray();
                for (int x = ProfileEar; x < w; x++)
                {
                    int src = (int)((x - ProfileEar + 0.5f) * half / (w - ProfileEar));
                    if (src >= 0 && src < half && src < g[y].Length) c[x] = g[y][src];
                }
                outRows[y] = new string(c);
            }
            return outRows;
        }

        public static string[] DraftBackQuarter(string kind, string key)
        {
            if (kind != "beard") return Blank();       // brows and marks are on the face: not seen from behind
            var side = DraftSide(kind, key);
            var outRows = new string[side.Length];
            for (int y = 0; y < side.Length; y++)
            {
                var c = new string('.', 32).ToCharArray();
                // the side's ear-to-cheek strip (ProfileEar .. +8) seen past the near cheek, at the edge
                for (int i = 0; i < 6; i++)
                {
                    int sx = ProfileEar + i, dx = 26 + i;
                    if (sx < side[y].Length) c[dx] = side[y][sx];
                }
                outRows[y] = new string(c);
            }
            return outRows;
        }

        public static string[] Mirror(string[] g)
        {
            var o = new string[g.Length];
            for (int i = 0; i < g.Length; i++) { var a = g[i].ToCharArray(); System.Array.Reverse(a); o[i] = new string(a); }
            return o;
        }

        /// <summary>A face part that differs left to right, and so needs its own picture facing the other way.</summary>
        public static bool Asymmetric(string kind, string key)
        {
            var g = FaceGrid(kind, key);
            for (int i = 0; i < g.Length; i++) if (Mirror(new[] { g[i] })[0] != g[i]) return true;
            return false;
        }

        /// <summary>Where the ear sits in the profile, and how long a brow stroke is there (composed texels).</summary>
        const int ProfileEar = 10, ProfileBrowLength = 6;

        /// <summary>The head SIDE-ON with every face part given (hair cap, brow, beard, mark - composed grids).</summary>
        public static string[] HeadProfileFull(string expression, string hair, string[] cap, bool openTop,
                                               string[] brow, string[] beard, string[] mark,
                                               string[] mane = null, int maneTop = 0, int scale = DetailScale)
        {
            var featuresFace = ProfileFace(Expand(Expression(expression).Grid, solid: false));
            var head = Compose(Expand(Skull, solid: true), featuresFace, beard ?? Blank(), brow ?? Blank(), cap ?? new string[0]);
            var big = FaceDetail.Build(head, featuresFace, scale, HairValue(hair), openTop, beard,
                                       maneAbove: ManeAbove(mane, maneTop, head));
            BeardOwnInk(big, beard, cap, scale);
            StampGrid(big, mark, scale);
            return big;
        }

        /// <summary>The head from behind, a step round, with the near cheek's beard and mark.</summary>
        public static string[] HeadBackQuarterFull(string hair, string[] cap, bool openTop, string[] beard, string[] mark,
                                                   int scale = DetailScale)
        {
            var flat = Compose(Expand(Skull, solid: true), beard ?? Blank(), cap ?? new string[0]);
            var noFace = new string[flat.Length];
            for (int i = 0; i < noFace.Length; i++) noFace[i] = new string('.', flat[i].Length);
            var big = FaceDetail.Build(flat, noFace, scale, HairValue(hair), openTop, beard);
            BeardOwnInk(big, beard, cap, scale);
            StampGrid(big, mark, scale);
            return big;
        }

        /// <summary><see cref="StampMark"/> for a grid already in place.</summary>
        static void StampGrid(string[] big, string[] m, int scale)
        {
            if (m == null) return;
            for (int y = 0; y < m.Length; y++)
            for (int x = 0; x < m[y].Length; x++)
            {
                char ch = m[y][x];
                if (ch == '.') continue;
                for (int dy = 0; dy < scale; dy++)
                for (int dx = 0; dx < scale; dx++)
                {
                    int px = x * scale + dx, py = y * scale + dy;
                    if (PixelDetail.At(big, px, py) is 's' or 'd' or 'b' or 'l' or 'h' or 'n' or 'N')
                        PixelDetail.Put(big, px, py, ch);
                }
            }
        }
    }
}
