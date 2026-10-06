using UnityEngine;

namespace Convergence.Art
{
    /// <summary>
    /// The menu-density FACE.
    ///
    /// The first attempt derived it with the same pass gear uses and came out hard and mechanical.
    /// Reading the grid rather than the render said exactly why, and none of it was about detail
    /// being wrong in principle:
    ///
    ///   THE OUTLINE BECAME A WALL. A 1-texel line scaled 4x is a 4-texel line, so the head had
    ///   perfectly straight vertical sides four texels thick. That is a machined box, not a skull -
    ///   the same mistake OutlinePadFor already exists to prevent elsewhere: an outline is a
    ///   WORLD-SIZE decision, not a texel count.
    ///
    ///   EVERY SHAPE WAS A RECTANGLE. The eye was a solid lash bar over a rectangular iris over a
    ///   rectangular lid, with a rectangular glint - four stacked boxes, every edge axis-aligned.
    ///
    ///   THE HAIR WAS HORIZONTAL BANDS running straight across the head, which reads as a striped
    ///   helmet rather than as hair.
    ///
    /// The hair, the silhouette and the light were all fixed by RULES rather than by redrawing,
    /// and those are the passes below - hair is surface, and surface is exactly what more pixels
    /// are good for.
    ///
    /// THE EYE WAS THE EXCEPTION FOR A LONG TIME, AND IT IS NOT ANY MORE. This file used to say,
    /// at length, that the hair carries the detail and the eye does not: three treatments were
    /// compared, the winner took its eyes from the LEAST worked-up of them, and the conclusion
    /// drawn was that an eye given parts always reads as mechanical. Held against a real reference
    /// that conclusion did not survive - what it had actually found was that an eye built out of
    /// RECTANGLES reads as mechanical, which is a fact about boxes and not about structure. A flat
    /// disc in a square has no sclera, no lid and no lash; it is not a restrained eye, it is a
    /// bead. <see cref="Eyes.Draw"/> now builds the whole thing - aperture, black lash, its cast
    /// shadow, iris and white - out of continuous fields, and every edge in it is a curve the
    /// arithmetic produced rather than a shape that was placed.
    /// </summary>
    public static class FaceDetail
    {
        /// <summary>
        /// How many texels of outline to keep, at any density.
        ///
        /// TWO, not the scale factor. The 16x14 face carries a one-texel line, and the point of
        /// drawing at 4x is that the line keeps its WEIGHT on screen while everything else gains
        /// resolution. Left at four it is the loudest thing in the picture.
        /// </summary>
        const int OutlineTexels = 2;

        /// <param name="hairValue">
        /// The hair swatch's own brightness, 0..1. The hair pass leans its whole exposure on it -
        /// see <see cref="Hair.PaleShift"/> - so a snow head and a black one both come out with
        /// real shading instead of one blowing out and the other crushing.
        /// </param>
        /// <param name="beard">
        /// The EXPANDED beard grid (same size as <paramref name="flat"/>), or null for none - see
        /// the note on <see cref="Hair.Draw"/> for why this exists.
        /// </param>
        public static string[] Build(string[] flat, string[] expression, int k, float hairValue,
                                     bool openTop = false, string[] beard = null,
                                     bool leftEyeGlow = false)
        {
            var big = PixelDetail.Upscale(flat, k);

            ThinOutline(big, OutlineTexels);
            RoundSilhouette(big);
            SoftLight(big, "ksdblh");
            Hair.Draw(big, k, BodyLook.ManeHeadTop, hairValue, openTop, Mask(beard, k));

            foreach (var box in Eyes.Boxes(expression)) Eyes.Draw(big, box, k, leftEyeGlow);
            return big;
        }

        /// <summary>Upscale a same-size grid's OCCUPANCY (non-'.' cells) rather than its tones.</summary>
        static bool[,] Mask(string[] grid, int k)
        {
            if (grid == null) return null;
            int h = grid.Length, w = grid[0].Length;
            var mask = new bool[h * k, w * k];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (grid[y][x] == '.') continue;
                for (int dy = 0; dy < k; dy++)
                for (int dx = 0; dx < k; dx++)
                    mask[y * k + dy, x * k + dx] = true;
            }
            return mask;
        }

        /// <summary>
        /// The same treatment for a grid that is ONLY hair - the mane.
        ///
        /// It has to be the same pass, and it has to be told where its own top edge sits: the
        /// crown the shading fans and rings from is stated in the rig's texels, so the head and
        /// the mane can share one, and a mane shaded from its own local centre would put its
        /// highlight somewhere the fringe's highlight is not.
        /// </summary>
        /// <param name="upscale">Cells per texel OF THIS GRID - 2 for art already authored at
        /// twice the density, so the result comes out the same size either way.</param>
        /// <param name="cells">Cells per SOURCE texel, which is what the shading is measured in.
        /// It stays at the full detail scale however finely the art was drawn, because a rim width
        /// and a crown distance are world-size decisions - the same rule OutlinePadFor states for
        /// an outline. Passing the upscale here instead would halve both on finer art, which is the
        /// one thing drawing it at 2x must not change.</param>
        public static string[] BuildHair(string[] flat, int upscale, int cells, int topTexel,
                                         float hairValue)
        {
            var big = PixelDetail.Upscale(flat, upscale);

            ThinOutline(big, OutlineTexels);
            RoundSilhouette(big);
            Hair.Draw(big, cells, topTexel, hairValue);
            return big;
        }

        // ---------------------------------------------------------------- silhouette

        static bool Solid(string[] g, int x, int y) => PixelDetail.At(g, x, y) != '.';

        /// <summary>
        /// Push the interior out through the scaled outline until only a thin rim is left.
        ///
        /// An outline texel more than <paramref name="keep"/> from the transparent outside is not
        /// carrying the silhouette, it is filling space that should have been face.
        /// </summary>
        static void ThinOutline(string[] big, int keep)
        {
            // ONLY THE SILHOUETTE. An interior line - a brow, and historically the mouth before it
            // was removed from BodyLook entirely - is drawn in the same outline tone, and thinning
            // those filled them in with skin: the first version of this deleted the mouth from
            // every face, and it took a side-by-side against the original to notice, because a
            // chin shadow sat roughly where it had been. Reachability from the border separates
            // the two - the outer rim connects to the outside, an interior mark does not. The
            // check still earns its keep on the brow even now that there is no mouth to protect.
            var rim = Silhouette(big);

            var outp = (string[])big.Clone();
            for (int y = 0; y < big.Length; y++)
            for (int x = 0; x < big[y].Length; x++)
            {
                char c = PixelDetail.At(big, x, y);
                if (c != 'k' && c != 'K') continue;
                if (!rim[y][x]) continue;
                if (NearEdge(big, x, y, keep)) continue;

                // Take the nearest non-outline tone, so a line between hair and skin resolves to
                // the side it is actually on.
                char fill = Nearest(big, x, y, c);
                if (fill != '\0') PixelDetail.Put(outp, x, y, fill);
            }
            for (int y = 0; y < big.Length; y++) big[y] = outp[y];
        }

        /// <summary>Outline texels connected to the outside - the rim, as opposed to a mouth.</summary>
        static bool[][] Silhouette(string[] big)
        {
            // WIDTH IS THE WIDEST ROW, not the first one. Ragged grids are legal everywhere else
            // in this project - PixelSprite warns and pads them - so this pass must not be the one
            // place that hard-crashes on one. Sizing off big[0] is what threw IndexOutOfRange on
            // the "long" mane, whose first row was ten characters against the grid's own 44: the
            // caller iterates x to big[y].Length and read straight past the end of rim[y].
            int h = big.Length, w = 0;
            foreach (var row in big) if (row.Length > w) w = row.Length;
            var seen = new bool[h][];
            for (int y = 0; y < h; y++) seen[y] = new bool[w];

            var q = new System.Collections.Generic.Queue<(int, int)>();
            for (int x = 0; x < w; x++) { q.Enqueue((x, 0)); q.Enqueue((x, h - 1)); }
            for (int y = 0; y < h; y++) { q.Enqueue((0, y)); q.Enqueue((w - 1, y)); }

            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue();
                if (x < 0 || y < 0 || x >= w || y >= h || seen[y][x]) continue;
                char c = PixelDetail.At(big, x, y);
                if (c != '.' && c != 'k' && c != 'K') continue;   // stop at the first real tone
                seen[y][x] = true;
                q.Enqueue((x + 1, y)); q.Enqueue((x - 1, y));
                q.Enqueue((x, y + 1)); q.Enqueue((x, y - 1));
            }
            return seen;
        }

        static bool NearEdge(string[] g, int x, int y, int r)
        {
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
                if (!Solid(g, x + dx, y + dy)) return true;
            return false;
        }

        static char Nearest(string[] g, int x, int y, char outline)
        {
            for (int r = 1; r <= 6; r++)
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                char c = PixelDetail.At(g, x + dx, y + dy);
                if (c != '.' && c != outline && c != 'k' && c != 'K') return c;
            }
            return '\0';
        }

        /// <summary>
        /// Knock the shoulders off the silhouette so it curves instead of stepping.
        ///
        /// A texel with five or more transparent neighbours in its 3x3 is a corner nobody drew -
        /// it is left over from the scale-up. Two passes, because removing one exposes the next.
        /// </summary>
        static void RoundSilhouette(string[] big)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                var outp = (string[])big.Clone();
                for (int y = 0; y < big.Length; y++)
                for (int x = 0; x < big[y].Length; x++)
                {
                    if (!Solid(big, x, y)) continue;
                    int open = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if ((dx != 0 || dy != 0) && !Solid(big, x + dx, y + dy)) open++;
                    // SIX, not five. At five this ate the anime styles alive: a two-texel lock
                    // standing off the crown has five open neighbours along its whole length, so
                    // every spike was shaved back into the cap and the messier styles came out as
                    // confetti. Six only catches a genuine single-texel corner.
                    if (open >= 6) PixelDetail.Put(outp, x, y, '.');
                }
                for (int y = 0; y < big.Length; y++) big[y] = outp[y];
            }
        }

        // ---------------------------------------------------------------- light

        /// <summary>
        /// One soft light on the skin, from the upper left, and NOTHING on the right or bottom.
        ///
        /// The gear pass darkens the facing-away side too, which is right for a plate and wrong
        /// for a face: a dark band down one cheek and along the jaw reads as stubble or as grime.
        /// A face wants the light and not the shadow.
        /// </summary>
        static void SoftLight(string[] big, string ramp)
        {
            char baseT = ramp[3], lit = ramp[4];
            var outp = (string[])big.Clone();
            for (int y = 0; y < big.Length; y++)
            for (int x = 0; x < big[y].Length; x++)
            {
                if (PixelDetail.At(big, x, y) != baseT) continue;
                bool edge = PixelDetail.At(big, x - 1, y) != baseT
                         || PixelDetail.At(big, x, y - 1) != baseT;
                if (edge) PixelDetail.Put(outp, x, y, lit);
            }
            for (int y = 0; y < big.Length; y++) big[y] = outp[y];
        }

        // ---------------------------------------------------------------- hair

        static bool IsHair(char c) => c is 'L' or 'H' or 'D' or 'B' or 'S';

        /// <summary>
        /// The menu-density HAIR treatment: structure, not a gradient.
        ///
        /// The pass this replaces only laid a soft radial light - a highlight near the crown
        /// falling away outward - which at 4x is a smooth blob on a shape that should be made of
        /// locks. What the two references actually have was measured off them rather than guessed:
        /// ONE dominant tone over about 45% of the hair, a COHERENT shadow at roughly 0.7 of its
        /// value over another 25%, and a small specular blow-out near 10%. Our own ramp's Dark sits
        /// at exactly 0.70 of Base, so the tones already existed; what was missing was that the
        /// shadow has to be STRUCTURAL - roots, and the edges of locks - rather than a falloff.
        ///
        /// Four terms, and each one is answering something a single gradient got wrong:
        ///
        ///   SHAPE    every lock is rounded against its OWN silhouette, so a tail sweeping
        ///            sideways and a fall hanging straight down are both lit along their middle.
        ///   FAN      subdivides a mass too wide to read as one lock. Only the crown is, so it
        ///            fades out with distance - a fan applied everywhere cuts ACROSS the locks the
        ///            art already drew, which is the "lines scored into a hard surface" failure
        ///            this file has now recorded three times.
        ///   RIBBON   the anime highlight: a wavy band at a roughly fixed radius from the crown.
        ///   ROOT     hair meeting skin is under everything in front of it.
        ///
        /// THE CUTS ARE QUANTILES, SOLVED OFFLINE, exactly as the eye's numbers were. They were
        /// fitted to the measured reference proportions across every style crossed with every hair
        /// swatch, so "more shadow" is a decision about the terms above and never about retuning
        /// five thresholds by hand.
        /// </summary>
        static class Hair
        {
            /// <summary>
            /// Texels above the neck joint that hair is treated as growing from. The head sprite's
            /// own top edge is +16, so this sits just proud of the skull - and it is stated in the
            /// RIG's texels rather than any one grid's, which is what lets the head and the mane
            /// fan and ring from the same point instead of each from its own centre.
            /// </summary>
            /// <summary>
            /// How deep into a lock the light reaches, in source texels.
            ///
            /// This is now the ONLY term that shapes the hair, and the reference is what says so.
            /// Measured over its hair region: the light tone sits a mean 22px from the nearest
            /// edge, the mid tone 14px, and the dark tone 2.3px - it is a distance-to-edge ramp
            /// and nothing else. No band, no fan, no radial anything.
            /// </summary>
            const float ShapeFull = 3.0f;

            /// <summary>
            /// How far the dark edge reaches INTO a lock, in source texels - and it is ABSOLUTE,
            /// not a fraction of the lock's width.
            ///
            /// Measured as a fraction first, and that was the error: our locks are 4-5 texels wide
            /// against the reference's 6-8, so "the outer 40% is dark" spent nearly half of every
            /// lock on shadow and came out as a thick rim round everything - an outline, not
            /// shading. The reference's dark band sits a mean 2.3px from an edge whatever the lock
            /// it is on, out of a mass averaging 14.7px deep. An absolute rim is what makes a wide
            /// lock read as mostly lit and a thin strand as mostly shadow, which is the difference
            /// between the two in the reference as well.
            /// </summary>
            const float RimFull = 1.6f;

            /// <summary>
            /// Every lock keeps a lit middle even when it is thinner than <see cref="RimFull"/>.
            /// Without it a tapering tip is inside the rim along its whole length and goes solid
            /// dark - the failure that made the ends of the hair black in an earlier pass.
            /// </summary>
            const float SpineLift = 0.30f;

            /// <summary>Hair meeting skin sits under whatever is in front of it.</summary>
            const float Root = 0.55f; const int RootDepth = 2;

            /// <summary>
            /// A slight downward lean, because the reference's dark band sits 30px LOWER in the
            /// mass than its light band does. Hair is lit from above; the undersides of locks and
            /// the length below the crown are where the shadow collects.
            /// </summary>
            const float Below = 0.14f;

            /// <summary>
            /// HOW MUCH HAIR IS STACKED DIRECTLY ABOVE THIS TEXEL, and it is what turns a rim into
            /// shading.
            ///
            /// An absolute rim alone came out as flat colour inside a dark outline, because a
            /// distance-to-edge ramp is symmetric - it darkens the top of a lock exactly as much as
            /// the underside. The reference is not symmetric: its mid tone sits a mean 14px deep,
            /// which is nowhere near an edge, and it forms big connected regions under the fringe
            /// and along the undersides of locks. That is light arriving from ABOVE.
            ///
            /// Counting the hair overhead is the cheapest honest version of it: a lock's top
            /// surface has nothing above it and stays lit, anything tucked under another lock goes
            /// to the mid tone, and the deepest stacks reach the dark. It also means the lock
            /// boundaries the ART draws are what cast the shadows, which is the same principle the
            /// rest of this pass runs on.
            /// </summary>
            const float Occlusion = 0.62f; const int OcclusionDepth = 5;

            /// <summary>
            /// Exposure lean on the swatch's own brightness - see the pale/dark note below.
            /// </summary>
            const float PaleShift = 0.34f, DarkShift = 0.12f, NeutralValue = 0.55f;

            /// <summary>
            /// THREE TONES, NOT FIVE, AND NOTHING ABOVE THE MASS.
            ///
            /// This is the whole of the "it looks glossy" fix and it was measured rather than
            /// judged. The reference's hair quantises to three plateaus - 45% at its brightest,
            /// 30% at 0.86 of that, 24% at 0.71 - and CRITICALLY its brightest tone IS the mass.
            /// There is no fourth, brighter band anywhere in it.
            ///
            /// The pass this replaces put a wavy specular ribbon above the mass, which is exactly
            /// a gloss highlight: it says the hair is a hard reflective surface. Anime hair of this
            /// kind is not lit that way - it reads as hair because each LOCK is a clean shape with
            /// a bright middle and a dark edge, and the lock boundaries come from the art rather
            /// than from anything drawn on top of it.
            ///
            /// The cuts are the reference's own proportions, as quantiles of the luminance this
            /// pass produces.
            /// </summary>
            static readonly (float Cut, char Tone)[] Steps =
            {
                (-0.08f, 'L'), (-0.31f, 'D'),
            };

            /// <param name="openTop">
            /// The hair does NOT end at the top of this grid - a mane continues it on its own
            /// sprite. Without this the head's own cap rims itself dark along a boundary the mane
            /// is running straight through, which draws a hard bar across the top of the head with
            /// the spray rising out of it as a separate object. The two are one head of hair; only
            /// the sprites are separate.
            /// </param>
            /// <param name="exclude">
            /// Cells that read as hair-toned but are NOT hair - a beard, painted in the hair ramp
            /// for the reason <see cref="BodyLook.Beards"/> gives (one swatch row, not two), which
            /// makes it indistinguishable from a lock of hair to <see cref="IsHair"/> even though
            /// it sits at the jaw, disconnected from the actual cap. Left unexcluded, this whole
            /// pass ran its lock-shading model over it anyway: the beard's own bounding box was
            /// folded into the hair mass this pass measures "top" and "bottom" from, and the
            /// rim/spine/occlusion terms - tuned against a strand tapering out of a scalp - turned
            /// a flat authored block into a converging light/dark wedge with nothing to do with the
            /// art. A beard is meant to stay the same flat blocks at every density; excluding it
            /// here is what keeps it that way once the character screen upscales everything else.
            /// </param>
            public static void Draw(string[] big, int k, int topTexel, float hairValue,
                                    bool openTop = false, bool[,] exclude = null)
            {
                int w = big[0].Length, h = big.Length;
                bool Hair(int x, int y) => IsHair(PixelDetail.At(big, x, y))
                                         && (exclude == null || !exclude[y, x]);

                int rootCap = RootDepth * k + 1;
                float airFull = ShapeFull * k;
                int airCap = Mathf.CeilToInt(airFull) + 1;

                var skin = Distance(big, c => c is 'k' or 's' or 'd' or 'b' or 'l' or 'h', rootCap);

                // Everything directly above this column's topmost hair, which openTop excuses from
                // counting as an edge.
                bool[,] sky = null;
                if (openTop)
                {
                    sky = new bool[h, w];
                    for (int x = 0; x < w; x++)
                    {
                        int first = -1;
                        for (int y = 0; y < h && first < 0; y++) if (Hair(x, y)) first = y;
                        if (first < 0) first = h;
                        for (int y = 0; y < first; y++) sky[y, x] = true;
                    }
                }
                var air = Distance(big, c => c == '.', airCap, sky);
                // The widest this lock gets nearby, capped so a genuinely wide mass saturates.
                // Without it a lock tapering to a point is thinner than the gradient everywhere
                // along its length, so every tip came out at the darkest tone - the ends of the
                // hair went black while the crown stayed lit.
                var ridge = LocalMax(air, airCap, airFull);

                float lean = hairValue > NeutralValue ? PaleShift : DarkShift;
                float bias = (hairValue - NeutralValue) * lean;

                // Top and bottom of the hair mass, for the downward lean.
                int top = h, bot = -1;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (Hair(x, y))
                    { if (y < top) top = y; if (y > bot) bot = y; }
                float span = Mathf.Max(1f, bot - top);

                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!Hair(x, y)) continue;

                    float lum = -bias;

                    // EVERY LOCK ROUNDED BY ITS OWN SILHOUETTE, against its own half-width. This
                    // is the entire shading model now: bright where a lock is thick, dark within a
                    // texel or two of any boundary - which is where the art's own gaps, outlines
                    // and overlaps already are, so the locks it draws are the locks that light.
                    float rim = RimFull * k;
                    lum += Mathf.Min(air[y, x], rim) / rim - 0.5f;
                    // On this lock's own medial axis, so a strand narrower than the rim still has
                    // a lit spine instead of being shadow all the way through.
                    if (air[y, x] >= ridge[y, x] - 0.5f) lum += SpineLift;

                    lum -= Root * Mathf.Max(0f, 1f - skin[y, x] / (float)rootCap);
                    lum -= (y - top) / span * Below;

                    int cap = OcclusionDepth * k;
                    int over = 0;
                    while (over < cap && IsHair(PixelDetail.At(big, x, y - over - 1))) over++;
                    lum -= Occlusion * over / (float)cap;

                    PixelDetail.Put(big, x, y, Tone(lum));
                }
            }

            static char Tone(float lum)
            {
                foreach (var (cut, tone) in Steps) if (lum >= cut) return tone;
                return 'S';
            }

            /// <summary>Chebyshev distance to the nearest seed, by two chamfer sweeps.</summary>
            static int[,] Distance(string[] g, System.Func<char, bool> seed, int cap,
                                   bool[,] ignore = null)
            {
                int h = g.Length, w = g[0].Length;
                var d = new int[h, w];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    d[y, x] = seed(PixelDetail.At(g, x, y)) && (ignore == null || !ignore[y, x])
                        ? 0 : cap;

                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int best = d[y, x];
                    if (y > 0)
                    {
                        best = Mathf.Min(best, d[y - 1, x] + 1);
                        if (x > 0) best = Mathf.Min(best, d[y - 1, x - 1] + 1);
                        if (x < w - 1) best = Mathf.Min(best, d[y - 1, x + 1] + 1);
                    }
                    if (x > 0) best = Mathf.Min(best, d[y, x - 1] + 1);
                    d[y, x] = Mathf.Min(best, cap);
                }
                for (int y = h - 1; y >= 0; y--)
                for (int x = w - 1; x >= 0; x--)
                {
                    int best = d[y, x];
                    if (y < h - 1)
                    {
                        best = Mathf.Min(best, d[y + 1, x] + 1);
                        if (x > 0) best = Mathf.Min(best, d[y + 1, x - 1] + 1);
                        if (x < w - 1) best = Mathf.Min(best, d[y + 1, x + 1] + 1);
                    }
                    if (x < w - 1) best = Mathf.Min(best, d[y, x + 1] + 1);
                    d[y, x] = Mathf.Min(best, cap);
                }
                return d;
            }

            /// <summary>Separable max filter - a square window, which at this radius reads the
            /// same as a disc and costs r rather than r squared per texel.</summary>
            static float[,] LocalMax(int[,] d, int r, float cap)
            {
                int h = d.GetLength(0), w = d.GetLength(1);
                var mid = new int[h, w];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int m = 0;
                    for (int i = -r; i <= r; i++)
                    {
                        int xx = x + i;
                        if (xx >= 0 && xx < w && d[y, xx] > m) m = d[y, xx];
                    }
                    mid[y, x] = m;
                }
                var outp = new float[h, w];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int m = 0;
                    for (int i = -r; i <= r; i++)
                    {
                        int yy = y + i;
                        if (yy >= 0 && yy < h && mid[yy, x] > m) m = mid[yy, x];
                    }
                    outp[y, x] = Mathf.Min(m, cap);
                }
                return outp;
            }
        }

        // ---------------------------------------------------------------- the eye

        static class Eyes
        {
            /// <summary>
            /// The boxes the expression drew an eye in, in source cells.
            ///
            /// Only regions that exactly fill a box at least two cells on a side. That is what
            /// separates a real eye from Dazzled's sparkles and Starry's plus, which are marks and
            /// are simply scaled - an iris rendered into a sparkle is not a more detailed sparkle.
            /// The faces that paint their own thing entirely (flame, hearts, a closed lid) never
            /// reach here, since none of them uses the eye ramp's digits.
            /// </summary>
            public static System.Collections.Generic.List<(int x0, int y0, int x1, int y1)> Boxes(string[] grid)
            {
                var found = new System.Collections.Generic.List<(int, int, int, int)>();
                var seen = new System.Collections.Generic.HashSet<(int, int)>();

                for (int y = 0; y < grid.Length; y++)
                for (int x = 0; x < grid[y].Length; x++)
                {
                    if (seen.Contains((x, y)) || !IsEye(grid[y][x])) continue;

                    var stack = new System.Collections.Generic.Stack<(int, int)>();
                    var cells = new System.Collections.Generic.List<(int, int)>();
                    stack.Push((x, y)); seen.Add((x, y));
                    while (stack.Count > 0)
                    {
                        var (cx, cy) = stack.Pop();
                        cells.Add((cx, cy));
                        foreach (var (dx, dy) in Around)
                        {
                            int nx = cx + dx, ny = cy + dy;
                            if (seen.Contains((nx, ny))) continue;
                            if (!IsEye(PixelDetail.At(grid, nx, ny))) continue;
                            seen.Add((nx, ny)); stack.Push((nx, ny));
                        }
                    }

                    int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
                    foreach (var (cx, cy) in cells)
                    {
                        x0 = Mathf.Min(x0, cx); x1 = Mathf.Max(x1, cx);
                        y0 = Mathf.Min(y0, cy); y1 = Mathf.Max(y1, cy);
                    }
                    // A SINGLE ROW STILL COUNTS, and it is a CLOSED eye - see Draw. Calm and
                    // Weeping both draw exactly that, and while the open eyes were the only ones
                    // being rendered those two were left as raw scaled bars, which beside four
                    // real eyes reads as the face having been half finished.
                    //
                    // Relaxing the height is safe only because the "fills its own bounding box"
                    // test below carries the weight: Starry's star, Dazzled's diamond and Dazed's
                    // spiral are all connected regions that do NOT fill their box, so none of them
                    // is mistaken for an eye, and Dazzled's loose sparkles are single cells that
                    // fail the width. Checked against all six of those grids rather than assumed.
                    int w = x1 - x0 + 1, h = y1 - y0 + 1;
                    if (w >= 2 && h >= 1 && cells.Count == w * h) found.Add((x0, y0, x1, y1));
                }
                return found;
            }

            static readonly (int dx, int dy)[] Around = { (1, 0), (-1, 0), (0, 1), (0, -1) };

            /// <summary>
            /// Any tone from the EYE ramp, line included.
            ///
            /// Not just the bright ones. Most expressions build an eye out of two rows - a colour
            /// row over a darker lash row - so testing only for the base tones found a 2x1 region,
            /// failed the "at least two on a side" check, and left every face except Neutral with
            /// the raw scaled blocks. Neutral was the only one that happened to be square.
            /// </summary>
            static bool IsEye(char c) => c is '1' or '2' or '3' or '4' or '5' or '6';

            // ---------------------------------------------------------- the eye's proportions
            //
            // Every one of these is a MULTIPLE of the block the expression drew, never a texel
            // count, so the eye still follows the face if the grid or the density ever moves.

            /// <summary>Aperture width, against the source block. The drawn eye is deliberately
            /// WIDER than the block - see the note on Draw.</summary>
            const float WidthGain = 1.55f;

            /// <summary>
            /// Aperture height, against the source block. It grows DOWNWARD only: the lash's own
            /// top edge stays on the block's top row, which is what keeps a big eye clear of the
            /// brow four texels above it.
            ///
            /// Against WidthGain this settles the eye's ASPECT, and the aspect is what decides
            /// whether the thing reads as an eye at all. An early tuning came out near square
            /// with a domed top and a flat white bottom, and the whole shape read as an open
            /// MOUTH - dome over teeth. Wider than tall is what breaks that read.
            /// </summary>
            const float HeightGain = 1.38f;

            /// <summary>How much higher the outer corner sits than the inner one. The single
            /// cheapest thing that stops an eye reading as a symmetrical bean.</summary>
            const float Tilt = 0.20f;

            /// <summary>The outer half of the aperture is longer than the inner half, so the eye
            /// runs out to a point away from the nose rather than being a mirrored oval.</summary>
            const float OuterStretch = 1.10f, InnerStretch = 0.92f;

            /// <summary>The aperture's lateral falloff. 2 is the honest ellipse; the lid curves
            /// below run ON TOP of it, and they are what decide whether it reads round or almond.
            /// </summary>
            const float ApertureN = 2.00f;

            /// <summary>
            /// Lid curvature, as the exponent on the aperture's own falloff. ABOVE 0.5 is the
            /// almond and below it is the coin, and getting this backwards was the first render's
            /// most obvious failure: at 0.60 the upper lid held nearly full height right across
            /// the middle and then dropped, which drew a flat bar across the top of the eye and
            /// read as a visor rather than a lid. Both lids are peaked now, the lower one more
            /// so - it is the shallower, more sharply cornered curve on a real eye.
            /// </summary>
            const float UpperPow = 0.72f, LowerPow = 0.82f, LowerScale = 0.76f;

            /// <summary>Lash thickness at the middle and how much more of it lands at the outer
            /// corner, as a FRACTION OF THE APERTURE'S OWN HEIGHT at that point rather than as a
            /// distance. The taper is the whole difference between a lash and a drawn-on border.
            /// </summary>
            const float LashBase = 0.15f, LashOuter = 0.36f;

            /// <summary>
            /// The iris, as a fraction of the aperture's HEIGHT, and where its centre sits
            /// relative to the aperture's own.
            ///
            /// THE IRIS IS A CIRCLE AND THE WHITE GOES BESIDE IT - that is the whole difference
            /// between an eye and a mouth, and it took two wrong tunings to arrive at. Filling
            /// the aperture with horizontal bands and putting the white underneath them draws a
            /// lens shape striped dark-over-red-over-white, which is a mouth with teeth in it, at
            /// any aspect ratio and with any amount of curvature on the lids. What reads as an
            /// eye is a ROUND thing with white to its LEFT AND RIGHT. So the iris is sized to
            /// nearly fill the opening vertically while leaving the aperture wider than it is -
            /// the leftover at each end is the sclera, and it needs no band of its own.
            /// </summary>
            const float IrisRadius = 0.80f, IrisRise = 0.00f;

            /// <summary>
            /// SQUINTING DOES NOT SHRINK THE EYEBALL, so the iris is sized against the aperture
            /// the expression would have had OPEN and only the lids move. Tied to the squinted
            /// height instead, the iris shrank while the opening kept its width, so a narrowed eye
            /// showed MORE white than a wide one - backwards, and it came out as a rag of white
            /// crumbs at the inner corner of every squinting face.
            ///
            /// The centre also sits on the APERTURE's own middle rather than on the block's,
            /// because the outer half is the longer one; centred on the block the white came out
            /// lopsided, thick outward and absent inward.
            /// </summary>
            const bool IrisFollowsOpenEye = true;

            /// <summary>
            /// The lash's own CAST SHADOW on the eyeball, as a fraction of the aperture's height
            /// measured down from the lash itself.
            ///
            /// A lid is a solid thing sitting in front of a curved wet surface, so it throws a
            /// shadow, and in the reference that shadow is the darkest part of the eye after the
            /// lash - a distinct band hugging the underside of the lid rather than a gradual fade.
            /// IT IS A SEMICIRCLE ON THE EYEBALL, not a band parallel to the lid, and the
            /// difference is which shape it belongs to. Measured as a band under the LASH it
            /// follows the lid's own curve, so it arcs the same way the lid does - highest in the
            /// middle - and comes out as a lid drawn twice. Measured down the IRIS instead it is
            /// the top half of that circle: rounded at both ends, level underneath, and
            /// unmistakably a shadow lying ON something round. Which is what the reference draws.
            ///
            /// So this is a fraction of the iris's own height, and 0.5 is exactly its diameter -
            /// a true half-disc. The lash covers the top of it, so what actually shows is the
            /// band between the two.
            ///
            /// Drawn in the EYE's own darkest tone, never in the lash's black: a shadow is the eye
            /// seen with less light on it, and painting it black would just thicken the lash. The
            /// two stay legible apart despite sitting close in value because they differ in HUE -
            /// the lash is a neutral black, the shadow is the swatch's own colour starved of light.
            /// </summary>
            const float ShadowHalf = 0.50f;

            // ---------------------------------------------------------- the eye ramp's own order
            //
            // THE DIGITS ARE NOT IN BRIGHTNESS ORDER, and assuming they were is a real bug this
            // pass shipped once. Palette.Ramp derives Deep by lerping the base 66% toward BLACK
            // and Line by lerping it 55% toward a near-black TINT, so Line lands ABOVE Deep:
            // measured on the crimson eye ramp, luminance runs
            //
            //     '2' Deep 0.103  <  '1' Line 0.162  <  '3' Dark 0.176  <  '4' Base 0.304
            //
            // and the same order holds for every swatch, since it falls out of the arithmetic
            // rather than out of any one colour. A ramp written 1-2-3-4 therefore goes down, up,
            // up - which is exactly the odd dark band an earlier render showed through the middle
            // of the iris. Named constants below, so the order is stated once and cannot be
            // re-guessed at a call site.

            const char Darkest = '2';   // Deep
            const char Line    = '1';   // Line - lighter than Deep, despite the digit
            const char Mid     = '3';   // Dark
            const char Full    = '4';   // Base
            const char White   = 'w';

            // GearItem.HasGlowingEye's override, mirroring the eye ramp's own digit order onto
            // the fixed glow ramp BodyLook.HeadPalette reserves ('A'=Line,'E'=Deep,'F'=Dark,
            // 'G'=Base) - see BodyLook.OverrideLeftEye for the identical mapping used on the flat
            // (non-menu) head. Sclera and lash are unaffected: only the iris changes colour, the
            // same way a real red eye still has a white and a lash.
            const char GlowDarkest = 'E';
            const char GlowLine    = 'A';
            const char GlowMid     = 'F';
            const char GlowFull    = 'G';

            /// <summary>
            /// The lash, and it is BLACK rather than the eye ramp's own darkest tone.
            ///
            /// Taken off the ramp it can only be the swatch lerped toward black, so a red eye got
            /// a maroon lash and a blue one got a navy lash - which reads as the iris continuing
            /// upward at a lower brightness rather than as a black lid in FRONT of a coloured eye.
            /// The reference is unambiguous about this: the lash is black and the iris is not.
            /// Its colour is a fixed entry in BodyLook.HeadPalette.
            /// </summary>
            const char Lash = 'x';

            /// <summary>A squint is a SHORTER aperture and a heavier lash, not a darker fill.
            /// </summary>
            const float SquintHeight = 0.84f, SquintLash = 0.10f;

            /// <summary>
            /// A CLOSED lid - one source row, which is how Calm and Weeping both draw their eyes.
            ///
            /// It is the same aperture, squashed and filled solid, and that is the whole trick:
            /// a lens shape tapering to a point at each corner IS what a shut eye looks like, so
            /// the closed lid needs no geometry of its own and inherits the open eye's tilt and
            /// its longer outer corner for free. Height first, then where the line sits - a shut
            /// eye's lids meet around the MIDDLE of the opening, not at the top of it, so this is
            /// dropped well below the block the expression drew rather than centred on it.
            /// </summary>
            const float ClosedHeight = 0.90f, ClosedDrop = 1.15f;

            /// <summary>A bound on how high the tilted upper lid ever reaches, in aperture
            /// heights. See its use in Draw.</summary>
            const float TopBound = 1.06f;

            /// <summary>
            /// DRAW A WHOLE EYE ON THE BLOCK THE EXPRESSION DREW - do not merely reshape it.
            ///
            /// Two earlier passes both stayed strictly inside that 2x2 block: the first rounded its
            /// corners, the second banded its fill into a gradient and put a catchlight on it. Held
            /// up against a real reference the second was still wrong, and the reason had nothing
            /// to do with the shading - it was that A BALL OF COLOUR IN A SQUARE IS NOT AN EYE'S
            /// STRUCTURE. An eye is an APERTURE with things inside it. What the reference has and
            /// neither pass had:
            ///
            ///   SCLERA. White, at the bottom and in both corners. This is the single biggest
            ///   missing piece - with no white anywhere, an iris is not an iris, it is a bead.
            ///
            ///   A LASH LINE that is a lid rather than a rim: thick, angled, and visibly HEAVIER
            ///   at the outer corner, running out past the aperture into a small wing.
            ///
            ///   AN IRIS THAT IS CUT OFF. It is a circle whose top disappears under the upper lid,
            ///   which is what makes the lid read as being in front of it. A disc that fits
            ///   entirely inside its own opening always reads as painted on.
            ///
            ///   A TILTED, WIDE APERTURE, longer on the outer side, rather than a circle.
            ///
            /// NONE OF THAT CONTRADICTS the "an assembled eye reads as mechanical" finding this
            /// file has warned about since the first detail pass, and it is worth being exact
            /// about why, because the two look like opposites. What failed then was an eye built
            /// out of RECTANGLES - a lash bar, an iris box, a lid box, a glint box, four
            /// axis-aligned primitives stacked up. Nothing here is a primitive: the aperture, the
            /// lash, its shadow and the iris are all CONTINUOUS FIELDS evaluated per texel, so
            /// every edge in the eye is a curve the arithmetic produced rather than a shape that
            /// was placed. Structure was never the problem; boxes were.
            ///
            /// IT IS BIGGER THAN THE BLOCK, and that is the other half of the change. Four texels
            /// per source cell is not enough room for a lid, an iris and a white all at once, so
            /// the eye spills into the skin around it - which is exactly what the extra density is
            /// FOR, and is why this can only ever be the menu face. It refuses to paint over hair,
            /// brow or line tones, so a fringe still hangs in front of it and a brow still sits
            /// above it; and it clears any leftover of the flat block it replaced, or the new eye
            /// would sit inside a square of the old one.
            ///
            /// SQUINT is still read off the ramp's LINE tone in the source - Determined draws its
            /// lower row with it, the one expression left that does - but it now means what it says: the
            /// aperture is SHORTER and the lash HEAVIER, so the lid comes down over the iris. The
            /// previous pass could only express it as a darker fill, which is a squint drawn in
            /// paint rather than in anatomy.
            /// </summary>
            /// <param name="leftEyeGlow">GearItem.HasGlowingEye - see BodyLook.Head's own note.
            /// Only the box on the character's own LEFT (outward > 0, the viewer's right in the
            /// default pose) switches to the override tones; the other eye is untouched.</param>
            public static void Draw(string[] big, (int x0, int y0, int x1, int y1) box, int k,
                                    bool leftEyeGlow = false)
            {
                int bx0 = box.x0 * k, by0 = box.y0 * k;
                int bw = (box.x1 - box.x0 + 1) * k, bh = (box.y1 - box.y0 + 1) * k;
                float cx = bx0 + bw * 0.5f;

                bool squint = false;
                for (int r = 0; r < bh; r++)
                for (int c = 0; c < bw; c++)
                    if (PixelDetail.At(big, bx0 + c, by0 + r) == '1') squint = true;

                // Which way is AWAY from the nose, for this eye. Everything asymmetric below - the
                // tilt, the longer outer half, the thickening lash - is expressed
                // against this rather than against the screen, so the pair comes out as mirror
                // images of each other and the face still survives being mirrored as a whole.
                float faceCx = big[0].Length * 0.5f;
                float outward = cx < faceCx ? -1f : 1f;
                bool useGlow = leftEyeGlow && outward > 0f;

                // One source row means a shut eye rather than a narrow one.
                bool closed = box.y1 == box.y0;

                float ax = bw * 0.5f * WidthGain;
                float ay = bh * 0.5f * HeightGain
                         * (closed ? ClosedHeight : squint ? SquintHeight : 1f);

                // DERIVED, not chosen: place the centre so the highest the upper lid ever reaches
                // lands on the block's own top row, and the eye grows DOWNWARD into bare cheek
                // instead of upward into the brow four texels above it. TopBound is a bound on
                // that peak rather than the peak itself - tilt moves it off centre, so it is
                // slightly over 1 - and erring high is what keeps the lash clear of a brow.
                float ecx = cx;
                float ecy = closed ? by0 + bh * ClosedDrop : by0 + ay * TopBound;

                float ayOpen = bh * 0.5f * HeightGain * (closed ? ClosedHeight : 1f);
                float ir = IrisRadius * (IrisFollowsOpenEye ? ayOpen : ay);
                float ix = ecx + outward * (OuterStretch - InnerStretch) * 0.5f * ax;
                float iy = ecy - IrisRise * ay;

                int scanX0 = Mathf.FloorToInt(ecx - ax * OuterStretch) - 1;
                int scanX1 = Mathf.CeilToInt(ecx + ax * OuterStretch) + 1;
                int scanY0 = Mathf.Min(by0, Mathf.FloorToInt(ecy - ay * (1f + Tilt))) - 1;
                int scanY1 = Mathf.Max(by0 + bh, Mathf.CeilToInt(ecy + ay)) + 1;

                for (int py = scanY0; py <= scanY1; py++)
                for (int px = scanX0; px <= scanX1; px++)
                {
                    char had = PixelDetail.At(big, px, py);
                    if (had == '.') continue;

                    char want = Tone(px + 0.5f, py + 0.5f, ecx, ecy, ax, ay, outward, squint,
                                     closed, ix, iy, ir, useGlow);

                    if (want == '\0')
                    {
                        // Anything of the flat block still lying outside the new aperture has to
                        // go back to skin, or the eye is drawn inside a square of its own old self.
                        // Checked against BOTH digit sets - the flat head this upscales from may
                        // already carry the glow ramp's own characters here instead of '1'-'6' if
                        // BodyLook.Head applied its own override before this ever ran.
                        if (had >= '1' && had <= '6' || IsGlowChar(had)) PixelDetail.Put(big, px, py, 'b');
                        continue;
                    }

                    // The fringe and the brow are IN FRONT. Hair paints last in the composition
                    // for exactly that reason, and an eye that grew into it would undo the one
                    // ordering rule the composed head has.
                    if (IsHair(had) || had == 'K' || had == 'k' || had == 'n' || had == 'N') continue;

                    PixelDetail.Put(big, px, py, want);
                }
            }

            /// <summary>
            /// What the eye wants at one texel: '\0' for "not the eye at all", otherwise the tone.
            ///
            /// EVERY BAND IS MEASURED AGAINST THE APERTURE'S OWN HEIGHT AT THAT COLUMN, not
            /// against the texel grid, and that is the fix for the tuning that came out as a
            /// mouth. Placing the lash by distance and the iris by its own circle let the three
            /// disagree about where the eye's edge was: the iris ended flat and level while the
            /// lids curved, so the white left underneath ran the full width as a straight bar
            /// with a straight red band above it. Sharing one `frac` - 0 at the upper lid, 1 at
            /// the lower one - means every band follows the lids by construction, so the white
            /// can only ever be a sliver that thins where the eye does.
            ///
            /// Read top to bottom this IS the anatomy: outside the lids, the black lash, the
            /// white, then the eyeball - the lash's own cast shadow over the top half of it, and
            /// the iris's ramp filling the rest.
            /// </summary>
            static char Tone(float px, float py, float ecx, float ecy, float ax, float ay,
                             float outward, bool squint, bool closed,
                             float ix, float iy, float ir, bool useGlow = false)
            {
                float s = (px - ecx) / ax * outward;              // +1 at the outer corner
                s = s >= 0f ? s / OuterStretch : s / InnerStretch;
                float t = (py - ecy) / ay;                        // +1 at the bottom lid

                float w = 1f - Mathf.Pow(Mathf.Abs(s), ApertureN);
                if (w <= 0f) return '\0';

                float up = Mathf.Pow(w, UpperPow) * (1f + Tilt * s);
                float lo = Mathf.Pow(w, LowerPow) * LowerScale;
                if (t < -up || t > lo) return '\0';

                // Shut: the whole aperture is the lash, so there is nothing else to decide.
                if (closed) return Lash;

                float span = up + lo;
                if (span <= 0f) return '\0';
                float frac = (t + up) / span;                     // 0 at the upper lid, 1 at the lower

                float lash = LashBase + LashOuter * Mathf.Max(0f, s) + (squint ? SquintLash : 0f);
                if (frac < lash) return Lash;

                // The iris: a real circle in TEXEL space, so it reads as round however the lids
                // curve around it. Anything inside the aperture it does not cover is sclera, and
                // because the circle is narrower than the opening that leftover lands where white
                // actually belongs - at the two ends, beside the iris.
                float dx = px - ix, dy = py - iy;
                if (dx * dx + dy * dy > ir * ir) return White;

                // Everything below is measured down the CIRCLE rather than down the aperture, so
                // the bands curve with the iris instead of striping across the whole eye.
                //
                // NO CATCHLIGHT. One was drawn here and is deliberately gone: a white dot is a
                // second thing competing with the eye's SHAPE to carry the expression, and the
                // shape is what should be carrying it. The reference has none either - what reads
                // as life there is the value ramp under the lid, not a highlight over it.
                float g = (py - (iy - ir)) / (2f * ir);

                // The lash's shadow, thrown onto the eyeball it sits in front of.
                if (g < ShadowHalf) return useGlow ? GlowDarkest : Darkest;

                if (useGlow) return g < 0.68f ? GlowLine : g < 0.84f ? GlowMid : GlowFull;
                return g < 0.68f ? Line : g < 0.84f ? Mid : Full;
            }

            static bool IsGlowChar(char c) => c is 'A' or 'E' or 'F' or 'G' or 'I' or 'J';
        }
    }
}
