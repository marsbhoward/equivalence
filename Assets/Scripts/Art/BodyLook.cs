using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art
{
    /// <summary>
    /// The character's own body: skin, hair, eyes, hairstyle and expression.
    ///
    /// Everything here is a CURATED LIST, not a free colour picker, for two reasons that happen
    /// to agree. Pixel art at this size lives on a small coherent palette - the whole argument in
    /// <see cref="Palette"/> - and a free RGB picker would let a player build a character that
    /// simply does not sit in the game's colour world. And the appearance is the NFT-facing half
    /// of the datum, so a short list of stable keys is far cheaper and far more durable on chain
    /// than three packed colour values whose meaning depends on the client that wrote them.
    ///
    /// HOW THE HEAD IS BUILT
    ///
    /// The head used to be one 16x14 grid with the hair painted into its top seven rows, which
    /// made hairstyle and hair colour the same decision as the face. It is now COMPOSED: a bare
    /// skin skull, then an expression, a beard and a brow, and the hairstyle STAMPED over that at
    /// the body's own density (see BodyLook.HairViews - hair has its own art per view, no longer a
    /// grid composed by index with the face). Later layers win wherever they are not transparent,
    /// so a fringe can legitimately hang over a brow. Composition
    /// rather than a new RigLayer is deliberate - RigLayer's declaration order IS the paint order
    /// and two sorting permutation tables are indexed by it, so adding one costs far more than it
    /// looks like it should.
    ///
    /// THE BEARD NOW PAINTS OVER THE FACE, not the other way round - reversed from the order
    /// this used to document. That order existed so a mouth would show through a beard rather
    /// than being swallowed by it; with the mouth gone, painting the face last only ever meant
    /// the six dramatic expressions (Ablaze, Smitten, Weeping, Starry, Dazzled) - relocated into
    /// the beard's own jaw rows once the mouth's removal freed that space - tore holes in
    /// whatever beard was worn beneath them. A beard trimming the tip of a flame or the point of
    /// a heart reads as the beard sitting in front of the face, which is what a beard does; a
    /// beard with a bite taken out of it reads as broken art. Hair still paints last, so a
    /// fringe covers a sideburn.
    ///
    /// DENSITY: NOT WHAT BUYS ANYTHING (but moved anyway)
    ///
    /// The head was tried at 32x28 / 75 ppu - the gear's density - and reverted once, for STYLE
    /// reasons only: having the cells invited spending them - rim shading, a shaded crown, a nose,
    /// finer brows - and the result was a different character, even though block-doubling a grid
    /// is provably the same image, pixel for pixel. That finding is still true and still the whole
    /// argument for why more RESOLUTION never had to mean more DETAIL.
    ///
    /// It moved anyway: PixelSprite.LayoutUnit went from 37.5 to 75 so armour would match the
    /// weapons it's worn beside, and this grid (Skull, every Expression/Beard/Brow below - the
    /// hairstyles have since left it, see BodyLook.HairViews) is now genuinely 32x28 - a MECHANICAL block-double of the 16x14 original, not a
    /// second attempt at the redraw that got reverted. Every feature below still budgets itself
    /// against the ORIGINAL 16x14 cells (now represented as 2x2 blocks); the extra resolution is
    /// spent on nothing, exactly as the finding above says it should be.
    ///
    /// So anything added still has to fit the (now-doubled) 32x28 budget as if it were 16x14: the
    /// jaw and the cheek beside the eyes are spare, the eyes are not. See "More pixels is free;
    /// more DETAIL is what costs" in CLAUDE.md.
    /// </summary>
    public static partial class BodyLook
    {
        public readonly struct Swatch
        {
            public readonly string Key, Name;
            public readonly Color Color;
            public Swatch(string key, string name, Color color) { Key = key; Name = name; Color = color; }
        }

        public readonly struct Style
        {
            public readonly string Key, Name;

            /// <summary>The grid itself - an expression, brow or beard. Null for a hairstyle,
            /// whose art is its two views (see BodyLook.HairViews).</summary>
            public readonly string[] Grid;

            public Style(string key, string name, string[] grid = null)
            {
                Key = key; Name = name; Grid = grid;
            }
        }

        /// <summary>
        /// Texel y of the HEAD sprite's top edge, from the neck joint it hangs off. The composed
        /// head is 36 rows drawn at an offset of 14, so it spans -4..+32 and a mane sharing this
        /// top aligns with it row for row. (Doubled alongside every body/armour grid when
        /// PixelSprite.LayoutUnit moved from 37.5 to 75 - was 18 rows at an offset of 7, spanning
        /// -2..+16.)
        /// </summary>
        public const int ManeHeadTop = 32;

        /// <summary>
        /// Where hair stops when the head is covered - the head sprite's own vertical MIDPOINT,
        /// not a fact about any one piece of headwear. The composed head spans -4..+32 (see
        /// <see cref="ManeHeadTop"/>), so the middle sits at +14.
        ///
        /// This used to be +9, measured off DemoGear's specific helm (14x6 auto-outlined to
        /// 16x8 at an offset of 12, covering +8..+16) - which left hair poking out above a
        /// HOOD, whose cowl reaches further up the crown than that helmet's brim does. Cutting
        /// at the true midpoint instead means hair never continues above the lower half of the
        /// face for EITHER kind of headwear, while still flowing out from under both and down
        /// over the shoulders, armour or a scarf below it.
        /// </summary>
        public const int HelmHairLine = 14;

        // ---------------------------------------------------------------- palettes

        public static readonly Swatch[] Skins =
        {
            new("fair",    "Fair",    new Color(0.92f, 0.78f, 0.66f)),
            new("light",   "Light",   new Color(0.84f, 0.70f, 0.58f)),
            new("tan",     "Tan",     new Color(0.72f, 0.56f, 0.42f)),
            new("olive",   "Olive",   new Color(0.62f, 0.50f, 0.36f)),
            new("bronze",  "Bronze",  new Color(0.55f, 0.40f, 0.28f)),
            new("deep",    "Deep",    new Color(0.40f, 0.28f, 0.20f)),
            new("umber",   "Umber",   new Color(0.28f, 0.19f, 0.14f)),
            // Two off-human tones. The game is four elementals in an arena, not a village sim.
            new("ashen",   "Ashen",   new Color(0.62f, 0.60f, 0.66f)),
            new("verdant", "Verdant", new Color(0.50f, 0.62f, 0.46f)),

            // Infernal tones. These are saturated hues rather than complexions, so they are named
            // for the colour and kept together at the end of the list - a player scanning for a
            // skin tone should not have to step through them to reach "deep".
            //
            // Every one of them still has to produce a READABLE OUTLINE. Ramp derives Line by
            // lerping the base 68% toward near-black, so a base that is already dark leaves the
            // head with no rim and the silhouette dissolves against a dark arena. That is the
            // whole reason "Obsidian" is not actually black: at a true black base its own outline
            // is indistinguishable from it. 0.24 reads as black beside every other skin here and
            // still leaves the rim a measurable step below it.
            new("crimson", "Crimson", new Color(0.74f, 0.22f, 0.20f)),
            new("saffron", "Saffron", new Color(0.90f, 0.76f, 0.26f)),
            new("obsidian","Obsidian",new Color(0.24f, 0.23f, 0.29f)),
            new("amber",   "Amber",   new Color(0.90f, 0.47f, 0.16f)),
            new("orchid",  "Orchid",  new Color(0.60f, 0.34f, 0.76f)),
        };

        public static readonly Swatch[] Hairs =
        {
            new("ash",     "Ash",     new Color(0.86f, 0.87f, 0.90f)),
            new("black",   "Black",   new Color(0.18f, 0.17f, 0.22f)),
            new("brown",   "Brown",   new Color(0.42f, 0.28f, 0.18f)),
            new("auburn",  "Auburn",  new Color(0.62f, 0.28f, 0.16f)),
            new("blonde",  "Blonde",  new Color(0.90f, 0.78f, 0.44f)),
            new("silver",  "Silver",  new Color(0.72f, 0.75f, 0.82f)),
            // A true red: blue kept BELOW green, or it leans rose.
            new("crimson", "Crimson", new Color(0.70f, 0.13f, 0.11f)),
            new("teal",    "Teal",    new Color(0.20f, 0.60f, 0.60f)),
            // Pale cyan-blue, sampled off the Backswept reference. Red held well under green
            // and blue or it greys into Silver beside it.
            new("sky",     "Sky",     new Color(0.58f, 0.82f, 0.90f)),
            new("violet",  "Violet",  new Color(0.52f, 0.34f, 0.72f)),
            new("ember",   "Ember",   new Color(0.95f, 0.45f, 0.15f)),
        };

        public static readonly Swatch[] Eyes =
        {
            new("slate",   "Slate",   new Color(0.36f, 0.42f, 0.50f)),
            new("dark",    "Dark",    new Color(0.22f, 0.18f, 0.20f)),
            new("amber",   "Amber",   new Color(0.80f, 0.55f, 0.18f)),
            new("green",   "Green",   new Color(0.32f, 0.60f, 0.34f)),
            new("blue",    "Blue",    new Color(0.28f, 0.52f, 0.85f)),
            new("violet",  "Violet",  new Color(0.55f, 0.35f, 0.80f)),
            new("crimson", "Crimson", new Color(0.75f, 0.18f, 0.22f)),
            new("gold",    "Gold",    new Color(0.92f, 0.78f, 0.30f)),
        };

        // ---------------------------------------------------------------- grids
        //
        // All 16 wide by 14 tall, matching the head the rest of the rig is measured against
        // (head top +0.373u, widest point +/-0.213u). A style that changed those numbers would
        // move every piece of head gear with it.
        //
        // '.' is transparent - it means "let the layer underneath show", not "draw nothing".
        //
        // Landmarks every grid is drawn around, in cells from the top-left:
        //
        //   eyes   cols 3-4 and 11-12, rows 8-9     brows   row 7 (row 6 too for a steep one -
        //   jaw    rows 10-12                               that's the independent Brows axis
        //   cheek beside the eyes  cols 1-2                 below, not any expression's own art)
        //   hairline (a full cap)  row 6
        //
        // THERE IS NO MOUTH. Every expression used to draw one as a run of 'k' at row 11 (grin and
        // weary spent a second row on an upturn or a downturn); all of it is gone. The character
        // reads off the eyes and brows alone now. Rows 10-12 are still jaw, still where a beard
        // sits, and still off limits for anything below the eye line - a mark there with no mouth
        // beside it now reads as a blemish rather than as one half of an expression.
        //
        // ROW 7 (AND 6) BELONG TO THE BROW AXIS, NOT TO AN EXPRESSION - see the collision note
        // below Determined. Only Determined still keeps a default mark there, because it is the
        // one expression the Brows list is allowed to override; nothing else may draw above row 8.
        //
        // Beards stop at ROW 12. Row 13 is the chin's own outline, and anything laid on it reads
        // as a hole in the silhouette rather than as a feature.

        /// <summary>The bare skull: skin only, no features. Every head starts here.</summary>
        static readonly string[] Skull =
        {
                "......kkkkkkkkkkkkkkkkkkkk......",
                "......kkkkkkkkkkkkkkkkkkkk......",
                "..kkkkbbbbbbbbbbbbbbbbbbbbkkkk..",
                "..kkkkbbbbbbbbbbbbbbbbbbbbkkkk..",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "..kkbbbbbbbbbbbbbbbbbbbbbbbbkk..",
                "..kkbbbbbbbbbbbbbbbbbbbbbbbbkk..",
                "....kkddddddddddddddddddddkk....",
                "....kkddddddddddddddddddddkk....",
                "........kkkkkkkkkkkkkkkk........",
                "........kkkkkkkkkkkkkkkk........",
        };

        /// <summary>
        /// Faces. Eyes sit at columns 3-4 and 11-12 on rows 8-9 in every one of them, because
        /// that is where head gear and the hairstyles are drawn around - an expression that moved
        /// them would put a helmet's eye slot in the wrong place.
        ///
        /// They are SOLID BLOCKS, with no sclera. A white was tried and taken back out: at two
        /// texels of iris there is nowhere to put a white that does not either halve the colour or
        /// push the eye wider, and either one is a redesign of the face wearing the name of a
        /// small addition.
        ///
        /// "Blank" is not a counter-example to that. A pure white eye is a whole FACE the player
        /// chose, not a sclera added underneath one they already had - it spends both texels on
        /// the white instead of trying to fit two things into two cells.
        ///
        /// THE FACE HAS NO MOUTH. It was a run of 'k' at row 11 on every expression - and grin and
        /// weary a second row on top for the turn of it - and it is all removed. What is left is
        /// carried by the eyes alone.
        ///
        /// GRIN, FIERCE AND WEARY ARE GONE TOO, and that took a second pass to see. Each one's
        /// entire distinguishing feature was the mouth; once that was cut, Grin and Fierce were
        /// left drawing the exact same squint as Determined (rows 8-9, "44"/"11") and Weary the
        /// exact same open eye as Neutral ("44"/"44") - the only thing setting any of them apart
        /// was a brow mark on row 7. That looked like enough right up until the Brows axis below
        /// was checked against them: Brows composes AFTER the expression and always wins wherever
        /// it isn't transparent, so the moment a player picks anything but "Natural" that brow
        /// mark is gone. Simulated the actual composition rather than eyeballing it - Determined,
        /// Grin and Fierce render BYTE-IDENTICAL under every one of the six named brows, and
        /// Neutral and Weary do the same. Four names bought nothing four different expressions
        /// weren't already buying for free once a brow was chosen, so they're cut rather than kept
        /// as decoration that only works while the player leaves Brows on "Natural".
        ///
        /// Determined survives as the one squint, and it is still allowed to own a default brow
        /// at row 7 for the "Natural" case - see <see cref="BrowedExpressions"/>. Nothing else may
        /// draw there any more; the six special faces below all had a mark on that row and have
        /// been moved down into the jaw space the mouth's removal freed up, for the same reason.
        /// </summary>
        public static readonly Style[] Expressions =
        {
            new("neutral", "Neutral", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......4444............4444......",
                "......4444............4444......",
                "......4444............4444......",
                "......4444............4444......",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("bright", "Bright", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......4444............4444......",
                "......4444............4444......",
                "......4444............4444......",
                "......4444............4444......",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("determined", "Determined", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......kkkk............kkkk......",
                "......kkkk............kkkk......",
                "......4444............4444......",
                "......4444............4444......",
                "......1111............1111......",
                "......1111............1111......",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("steely", "Steely", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......kkkk............kkkk......",
                "......kkkk............kkkk......",
                "......4444............4444......",
                "......4444............4444......",
                "......1111............1111......",
                "......1111............1111......",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("calm", "Calm", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......1111............1111......",
                "......1111............1111......",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("serene", "Serene", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                ".....111111..........111111.....",
                ".....111111..........111111.....",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),

            // ---- the six below are FACES, not eye colours ----
            //
            // Three rules they all keep, each of which was a real constraint rather than a
            // preference:
            //
            //   NOTHING ABOVE ROW 8. Row 7 (and 6) belong to the Brows axis now, full stop - not
            //   "usually", the way it read before Ablaze and Starry were caught drawing their own
            //   tip there. Composed the same way the Determined/Grin/Fierce collision above was
            //   checked: neither survived any brow but "Natural" and "Worried" (the one brow whose
            //   marks happen to miss their column by luck, not by design) - every other brow choice
            //   silently clipped the flame's tip and the star's point. Moved down a row apiece, into
            //   the jaw space the mouth used to occupy, which clears them of the brow's row for
            //   good instead of hoping no future brow ever reaches those columns.
            //
            //   THE SKULL'S OUTLINE IS OFF LIMITS, same as the beards - a sparkle on the rim
            //   replaces the head's own dark edge and the silhouette goes soft.
            //
            //   MIRROR-SYMMETRIC ABOUT COLUMN 7.5. The rig mirrors the whole character by
            //   negating scale, so an asymmetric face is a face that changes when you turn round.
            //
            // Stars and tears take the EYE ramp (the digits) so the eye swatch still does
            // something - gold eyes give classic gold stars. White, flame and hearts are their
            // own fixed colours; see OverridesEyeColour, which is what the screen reads.

            new("blank", "Blank", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......wwww............wwww......",
                "......wwww............wwww......",
                "......wwww............wwww......",
                "......wwww............wwww......",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("hollow", "Hollow", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                ".....wwwwww..........wwwwww.....",
                ".....wwwwww..........wwwwww.....",
                ".....wwwwww..........wwwwww.....",
                ".....wwwwww..........wwwwww.....",
                ".....wwwwww..........wwwwww.....",
                ".....wwwwww..........wwwwww.....",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("joyful", "Joyful", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......4444............4444......",
                "......4444............4444......",
                "......4444............4444......",
                "......4444............4444......",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("wide", "Wide", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......4444............4444......",
                "......4444............4444......",
                "......4444............4444......",
                "......4444............4444......",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            // TAPERED, and it had to be. Three even bands of flame colour came out as a striped
            // block - the colours said fire and the shape said nothing. Widening the base to three
            // texels and narrowing the tip to one gives the silhouette a flame has, which is the
            // half that survives being two texels across. The tip sits on the OUTER column so each
            // flame leans away from the nose.
            //
            // It sits at rows 8-10, in the jaw space the mouth used to occupy, rather than reaching
            // up to row 7 the way it first did. Row 7 belongs to the Brows axis now (see the
            // "NOTHING ABOVE ROW 8" note above) - drawn one row higher, every brow but Natural and
            // Worried clipped the flame's own tip off, confirmed by composing it against all six.
            new("ablaze", "Ablaze", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......gg................gg......",
                "......gg................gg......",
                "......yyyy............yyyy......",
                "......yyyy............yyyy......",
                "....ffffff............ffffff....",
                "....ffffff............ffffff....",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            // A heart needs THREE columns - two lobes and a gap - so this is the one face that
            // widens the eye, out to columns 2-4 and 11-13. That is the cheek the beards leave
            // spare, and the point below sits on row 10, which is jaw.
            new("smitten", "Smitten", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "....rr..rr............rr..rr....",
                "....rr..rr............rr..rr....",
                "....rrrrrr............rrrrrr....",
                "....rrrrrr............rrrrrr....",
                "......ee................ee......",
                "......ee................ee......",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            // Closed lids are the eye's LINE tone, the same trick Calm uses. The tear runs from
            // the outer corner down three rows to a brighter droplet, which is the only part of
            // it that has to be seen at arena distance.
            // NO BROW, deliberately. A brow in the skin's line tone directly above a lid in the
            // eye's line tone is two dark bars a texel apart, and they merge into one blob that
            // reads as neither. The lid is widened to three texels instead: at two it is a dot,
            // at three it is unmistakably a closed eye, and it carries the whole expression.
            new("weeping", "Weeping", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "...11111111..........11111111...",
                "...11111111..........11111111...",
                "......cc................cc......",
                "......cc................cc......",
                "......CC................CC......",
                "......CC................CC......",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("starry", "Starry", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......4444............4444......",
                "......4444............4444......",
                "......4444............4444......",
                "......4444............4444......",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            // The cluster is one star plus two single texels. A second FULL star does not fit
            // anywhere legal - rows 0-7 are under the hair and the rim is off limits - so the
            // outriders are single glow cells, which is all a sparkle needs to be.
            //
            // Both of them sit OUTBOARD of the eye, on a diagonal running away from it. Placed
            // inboard they land either side of the nose bridge and read as a nose; placed beside
            // the mouth they merge with its corners. Outboard is the only diagonal that is clear
            // of both.
            //
            // Sits at rows 8-12, one lower than it was drawn originally. Its own outriders never
            // actually collided with a brow (col 1 and 14 sit outside every brow's span, checked
            // against all six), but moving it down anyway means that stays true if a future brow
            // is ever drawn wider - the row-7 rule is "nothing draws there", not "nothing happens
            // to yet".
            // Dazzled was removed (2026-10-27, the user's call). A save still naming it draws the first
            // expression - Find's fallback - so it loads as Neutral.
            // SPIRALS, and they are the one asymmetric mark on any face. Each eye is a swirl with
            // its opening on the outside, so the pair are mirror images of each other - which
            // still satisfies the rule, because the rule is that the FACE survives being mirrored
            // and a mirrored pair of opposite swirls is the same pair. A single chiral mark
            // repeated identically on both eyes would not be.
            new("dazed", "Dazed", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......4444............4444......",
                "......4444............4444......",
                "......4444............4444......",
                "......4444............4444......",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
        };

        /// <summary>
        /// Beards, and they are the ONE grid family authored in FINAL composed space - 36 rows,
        /// not the 28 every other family is written in and <see cref="Expand"/> grows.
        ///
        /// That is the whole reason the previous set was broken rather than merely dated. Expand
        /// inserts FaceRowsAdded rows of lower face at FaceRowsAt, and a beard passes solid: true
        /// so it STRETCHES into them rather than being cut by a blank row - correct in principle
        /// (a gap through a beard is a bald stripe across the jaw) and ruinous in practice, since
        /// what actually stretches is one authored row repeated five times. Rows 25-29 of every
        /// old beard were forced to share a single horizontal profile: the moustache became an
        /// eight-row slab hanging under the eyes, and the stubble's scatter became vertical
        /// dribbles running down the cheek. Neither was a drawing mistake - no drawing in that
        /// format could have avoided it.
        ///
        /// Authored here at 36 rows, a beard aligns with the composed head BY CONSTRUCTION, which
        /// is the same thing Expand buys the other families and the reason they still use it.
        ///
        /// THE LANDMARKS, in final rows (two rows per texel, so texel T occupies rows 2T/2T+1):
        ///
        ///   T08 hairline   T09 brow   T10-11 EYES, cols 3-4 and 11-12
        ///   T12-T14  mid-face, interior cols 1-14      the rows Expand inserts
        ///   T15      jaw narrows, interior cols 2-13   outline at 1 and 14
        ///   T16      chin band, interior cols 3-12     outline at 2 and 13
        ///   T17      chin outline, cols 4-11           nothing below this row
        ///
        /// NOTHING STARTS ABOVE T12 - a full texel row BELOW the eyes, which sit at T10-11.
        /// Chinstrap, Chops and Full were drawn from T08 first and moved twice. At the hairline
        /// they read as hair, not beard: a beard and the hair cap take the same ramp, so a
        /// sideburn reaching the cap meets it with no boundary between them. Level with the eyes
        /// they read as HAIRY EYES - a mass of hair tone flanking the eye at its own height
        /// reads as part of the eye before it reads as a cheek. The clear row of skin under the
        /// eye is what separates the two, so T12 is the ceiling, not a preference.
        ///
        /// NO NOTCHES. A beard's OUTER edge may only move inward as the skull's own interior
        /// does (col 1 down to T14, col 2 at T15, col 3 at T16). Chinstrap stepped in a column at
        /// T14 while the rows either side stayed out, which trapped two cells of skin between the
        /// beard and the head's outline with beard above and below them - that reads as pixels
        /// missing from the art, because that is what it is. Only the INNER edge is free to move.
        ///
        /// TONES. Mass is 'B' - the hair ramp's BASE, so a beard is the same material as the hair
        /// on the head rather than a muddy relative of it. The old set painted everything in 'D'
        /// (Dark), which is why a brown-haired character wore a brown beard that agreed with
        /// nothing. 'D' is kept for an UNDERSIDE (a moustache's drooping tip, the bottom of a
        /// tuft) and 'K' for a RIM. Stubble is the exception: it is a shadow on skin, not hair sitting
        /// on top of it, so it takes 'm' (and 'M' along the jaw) - tones MIXED from the skin and the hair
        /// (HeadPalette). Drawn in the hair's 'S' alone it read as a patchy dark beard.
        ///
        /// TURNED, A BEARD IS WARPED, not slid - see BeardTurned. Chosen off the facial-hair sheet
        /// (2026-10-27): Stubble, Chops and Full redrawn, Goatee's moustache slimmed, Horseshoe to Forked
        /// added; keys are saved (Appearance.Beard) - append, never rename.
        ///
        /// A BEARD MAY LEAVE THE SKULL - and this reverses the rule this list used to state.
        /// "Never paint the outline" is right for a mark that sits inside the face, where
        /// replacing the head's dark edge with hair-dark softens the silhouette the whole rig is
        /// measured against. It is wrong for the one feature that is SUPPOSED to change the
        /// outline: gear is held to "must break the silhouette" (see CLAUDE.md), and every beard
        /// in the old set was a colour patch inside the face's own shape - the same head in a
        /// different colour, which is exactly the failure that rule exists to name.
        ///
        /// The test that separates the two cases is whether the dark edge MOVED or was DELETED. A
        /// beard may cross the outline only where it continues outward past it and carries 'K' on
        /// its own new boundary. Chops and Full do; nothing else needs to. The room is real but
        /// small: the skull narrows below T15, so cols 0/15 are transparent at T15, cols 0-1/14-15
        /// at T16, and cols 0-3/12-15 at T17.
        ///
        /// NOTHING HANGS BELOW T17, and that is a decision rather than a limit of the grid. A
        /// chest-length beard cannot live in the head sprite at all - it would need its own
        /// sprite and its own RigLayer, the bill <see cref="Gear.RigLayer.HairBack"/> paid for
        /// hair length. Hair could afford it because it falls BEHIND the body; a beard falls in
        /// front, and the chibi body has no neck, so it would start on the chest and sit
        /// permanently over Neck, Trinket and TorsoArmor - the gear the economy exists to show -
        /// with no motion that ever swings it clear. RigLayer.NeckBack already settled that exact
        /// argument one layer over, for a scarf tail that at least moves. Revisit it only if a
        /// beard is ever worth a layer of its own; the jaw flare below is what buys the
        /// silhouette in the meantime.
        /// </summary>
        public static readonly Style[] Beards =
        {
            new("none", "Clean", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("stubble", "Stubble", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "..MMmmmmmmmmmmmmmmmmmmmmmmmmMM..",
                "..MMmmmmmmmmmmmmmmmmmmmmmmmmMM..",
                "....MMmmmmmmmmmmmmmmmmmmmmMM....",
                "....MMmmmmmmmmmmmmmmmmmmmmMM....",
                "......MMmmmmmmmmmmmmmmmmMM......",
                "......MMmmmmmmmmmmmmmmmmMM......",
                "................................",
                "................................",
            }),
            new("moustache", "Moustache", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "........BBBBBB....BBBBBB........",
                "........BBBBBB....BBBBBB........",
                "......BBBBBBBBBBBBBBBBBBBB......",
                "......BBBBBBBBBBBBBBBBBBBB......",
                "......DDDDDD........DDDDDD......",
                "......DDDDDD........DDDDDD......",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("goatee", "Goatee", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "........BBBBBBB..BBBBBBB........",
                "........BBBBBBB..BBBBBBB........",
                ".......DD..............DD.......",
                "................................",
                "................................",
                "........BBBBBBBBBBBBBBBB........",
                "........BBBBBBBBBBBBBBBB........",
                "..........KKDDDDDDDDKK..........",
                "..........KKDDDDDDDDKK..........",
            }),
            new("chinstrap", "Chinstrap", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "..BBBB....................BBBB..",
                "..BBBB....................BBBB..",
                "..BBBBBB................BBBBBB..",
                "..BBBBBB................BBBBBB..",
                "..BBBBBBBB............BBBBBBBB..",
                "..BBBBBBBB............BBBBBBBB..",
                "....BBBBBBBB........BBBBBBBB....",
                "....BBBBBBBB........BBBBBBBB....",
                "......BBBBBBBBBBBBBBBBBBBB......",
                "......BBBBBBBBBBBBBBBBBBBB......",
                "................................",
                "................................",
            }),
            new("chops", "Chops", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "..BBBB....................BBBB..",
                "..BBBB....................BBBB..",
                "KKBBBBBB................BBBBBBKK",
                "KKBBBBBB................BBBBBBKK",
                "KKBBBBBBBB............BBBBBBBBKK",
                "KKBBBBBBBB............BBBBBBBBKK",
                "KKBBBBBBBB............BBBBBBBBKK",
                "KKBBBBBBBB............BBBBBBBBKK",
                "..KKBBBBD..............DBBBBKK..",
                "..KKBBBBD..............DBBBBKK..",
                "................................",
                "................................",
            }),
            new("full", "Full", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "..BBBB....................BBBB..",
                "..BBBB....................BBBB..",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBDDDDDBBBBDDDDDBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "..KKBBBBBBBBBBBBBBBBBBBBBBBBKK..",
                "..KKBBBBBBBBBBBBBBBBBBBBBBBBKK..",
                "....KKDDDDDDDDDDDDDDDDDDDDKK....",
                "....KKDDDDDDDDDDDDDDDDDDDDKK....",
            }),
            new("horseshoe", "Horseshoe", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "........BBBBBBBBBBBBBBBB........",
                "........BBBBBBBBBBBBBBBB........",
                "........BBB..........BBB........",
                "........BBB..........BBB........",
                "........BBB..........BBB........",
                "........BBB..........BBB........",
                "........DDD..........DDD........",
                "........DDD..........DDD........",
                "................................",
                "................................",
            }),
            new("vandyke", "Van Dyke", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "........BBBBBBB..BBBBBBB........",
                ".......BBBBBBBB..BBBBBBBB.......",
                "......DDD..............DDD......",
                "................................",
                "................................",
                ".............BBBBBB.............",
                ".............BBBBBB.............",
                ".............BBBBBB.............",
                ".............KDDDDK.............",
                ".............KDDDDK.............",
            }),
            new("circle", "Circle", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                ".........BBBBBBBBBBBBBB.........",
                ".........BBBBBBBBBBBBBB.........",
                ".........BB..........BB.........",
                ".........BB..........BB.........",
                ".........BB..........BB.........",
                ".........BB..........BB.........",
                ".........BBBBBBBBBBBBBB.........",
                ".........BBBBBBBBBBBBBB.........",
                "...........KDDDDDDDDK...........",
                "...........KDDDDDDDDK...........",
            }),
            new("soulpatch", "Soul Patch", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "..............BBBB..............",
                "..............BBBB..............",
                "..............BBBB..............",
                "................................",
                "................................",
            }),
            new("boxed", "Boxed", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "..BBB......................BBB..",
                "..BBB......................BBB..",
                "..BBB....BBBBBB..BBBBBB....BBB..",
                "..BBB....BBBBBB..BBBBBB....BBB..",
                "..BBBBBB.BB..........BB.BBBBBB..",
                "..BBBBBB.BB..........BB.BBBBBB..",
                "....BBBBBB............BBBBBB....",
                "....BBBBBB............BBBBBB....",
                "......BBBBBBBBBBBBBBBBBBBB......",
                "......BBBBBBBBBBBBBBBBBBBB......",
                "........KDDDDDDDDDDDDDDK........",
                "........KDDDDDDDDDDDDDDK........",
            }),
            new("forked", "Forked", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "..BBBB....................BBBB..",
                "..BBBB....................BBBB..",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBDDDDDBBBBDDDDDBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "..KKBBBBBBBBBBBBBBBBBBBBBBBBKK..",
                "..KKBBBBBBBBBBBBBBBBBBBBBBBBKK..",
                "......KKDDDDDK....KDDDDDKK......",
                "......KKDDDDDK....KDDDDDKK......",
            }),
        };

        /// <summary>
        /// Eyebrows, as their own axis.
        ///
        /// Composed AFTER the expression, so a chosen brow paints over whatever the expression
        /// drew at row 7. That ordering is the whole design decision, and the alternative was
        /// worse: put brows underneath and Determined would keep its own and the picker would
        /// silently do nothing on it.
        ///
        /// "NATURAL" IS AN EMPTY GRID, and it is first for that reason - it means "whatever this
        /// expression already draws", so every face is unchanged until the player deliberately
        /// overrides it.
        ///
        /// THIS USED TO BE A LARGER TRADE-OFF THAN IT LOOKED. Grin, Fierce and Weary each used to
        /// differ from Determined or Neutral by nothing but their own brow, and this note used to
        /// call the resulting convergence under a chosen brow "a choice the player made rather
        /// than a collision they did not ask for" - which sounded right and was not: composing the
        /// actual grids showed Determined, Grin and Fierce render byte-identical under any of the
        /// six brows below, and so do Neutral and Weary. That is not a player's choice converging
        /// two looks, it is three (and two) names for one picture the instant Natural is left. The
        /// three duplicates are cut - see the Expressions header - so Determined is now the only
        /// expression a brow choice can visibly change.
        ///
        /// Most live on rows 12-15; Raised, Quizzical's raised brow and Dots sit at 10-11, where a fringe
        /// hides them first. Keys are SAVED (Appearance.Brows) - append, never rename: "natural" shows as
        /// None, and Flat, Arched and Thin were redrawn under their own keys. Chosen off the brow mock-up
        /// sheet, 2026-10-27 (Soft through Bushy added; Knit, with creases between the brows, was not).
        /// </summary>
        public static readonly Style[] Brows =
        {
            new("natural", "None", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("flat", "Flat", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "....nnnnnn............nnnnnn....",
                "....nnnnnn............nnnnnn....",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("arched", "Arched", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......nnnn............nnnn......",
                ".....nnnnnn..........nnnnnn.....",
                "....nn....nn........nn....nn....",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("angry", "Angry", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "....nnnnnn............nnnnnn....",
                "....nnnnnn............nnnnnn....",
                "........nnnn........nnnn........",
                "........nnnn........nnnn........",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("worried", "Worried", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "........nnnn........nnnn........",
                "........nnnn........nnnn........",
                "....nnnnnn............nnnnnn....",
                "....nnnnnn............nnnnnn....",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("thick", "Thick", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                ".....nnnnnn..........nnnnnn.....",
                "....nnnnnnnn........nnnnnnnn....",
                "....nnnnnnnn........nnnnnnnn....",
                ".....NNNNNNN........NNNNNNN.....",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("thin", "Thin", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "....nnnnnnn..........nnnnnnn....",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("soft", "Soft", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                ".....nnnnnn..........nnnnnn.....",
                "....n......n........n......n....",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("raised", "Raised", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......nnnn............nnnn......",
                "....nnnnnnnn........nnnnnnnn....",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("quizzical", "Quizzical", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......................nnnn......",
                "....................nnnnnnnn....",
                "................................",
                "....nnnnnn......................",
                "....nnnnnn......................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("slit", "Slit", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "....nnn.nn............nnnnnn....",
                "....nnn.nn............nnnnnn....",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("tapered", "Tapered", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                ".........nnn........nnn.........",
                "......nnnnnn........nnnnnn......",
                "....nnnnnnnn........nnnnnnnn....",
                ".........NNN........NNN.........",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("sharp", "Sharp", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "...nn......................nn...",
                "....nnn..................nnn....",
                "......nnn..............nnn......",
                "........nnn..........nnn........",
                "..........nn........nn..........",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("dots", "Dots", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "......nnn..............nnn......",
                "......nnn..............nnn......",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("joined", "Joined", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "....nnnnnnnn........nnnnnnnn....",
                "....nnnnnnnnnnnnnnnnnnnnnnnn....",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("bushy", "Bushy", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                ".....n..nn............nn..n.....",
                "....nnnnnnnn........nnnnnnnn....",
                "...nnnnnnnnn........nnnnnnnnn...",
                "....nnnnnnnn........nnnnnnnn....",
                "....NNNNNNNN........NNNNNNNN....",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
        };

        /// <summary>
        /// The hair swatches with a MATCH entry in front, for the brow row.
        ///
        /// Its key is the empty string, which is what BrowColour holds when it is following the
        /// hair - so selecting it is the same operation as selecting any other swatch and there is
        /// no separate "reset" the player has to find. Its colour is a mid grey rather than the
        /// live hair colour: the row is built once per tab and would otherwise be stale the moment
        /// the hair above it changed, which is worse than a swatch that is honestly an icon.
        /// </summary>
        public static Swatch[] BrowSwatches
        {
            get
            {
                if (_browSwatches != null) return _browSwatches;
                var list = new List<Swatch> { new("", "Match", new Color(0.45f, 0.45f, 0.50f)) };
                list.AddRange(Hairs);
                _browSwatches = list.ToArray();
                return _browSwatches;
            }
        }

        static Swatch[] _browSwatches;

        /// <summary>The beard's row: the same MATCH-then-hair swatches as the brows'.</summary>
        public static Swatch[] BeardSwatches => BrowSwatches;

        public static Style Brow(string key)
        {
            if (_devBrows != null && key != null && _devBrows.TryGetValue(key, out var dev)) return dev;
            foreach (var b in Brows) if (b.Key == key) return b;
            return Brows[0];
        }

        // DEV ONLY: trial brows for the brow mock-up sheet (EditorTools.FaceSheet) - never saved. Rows joined by '|'.
        static Dictionary<string, Style> _devBrows;
        public static string DevAddBrow(string key, string name, string rows)
        {
            var grid = rows.Split('|');
            if (grid.Length != AuthoredRows) return $"{key}: {grid.Length} rows, want {AuthoredRows}";
            (_devBrows ??= new Dictionary<string, Style>())[key] = new Style(key, name, grid);
            return "ok";
        }

        /// <summary>
        /// The hairstyles, in the order the picker offers them. KEYS ARE SAVED - on chain, in the
        /// appearance datum - so a key is never renamed or reused, and "crop" (the default in
        /// Chain.Appearance) must stay. Each style's art is its two views, in BodyLook.HairArt.cs;
        /// see BodyLook.HairViews for the format and how they derive from the face-on originals.
        /// </summary>
        public static readonly Style[] HairStyles =
        {
            new("shock", "Shock"),
            new("hightail", "High Tail"),
            new("spiky", "Spiky"),
            new("backswept", "Backswept"),
            new("bangs", "Bangs"),
            new("messy", "Messy"),
            new("twists", "Twist Fade"),   // the original Twists' tips on Fade's cut - see BodyLook.HairViews
            new("wolf", "Wolf Cut"),
            new("twintail", "Twin Tails"),
            new("hime", "Hime"),
            new("crop", "Buzz"),   // key kept: saves name it
            new("fade", "Fade"),
            new("long", "Long"),
            new("ponytail", "Ponytail"),
            new("cornrows", "Cornrows"),
            new("braids", "Long Braids"),
            new("mohawk", "Mohawk"),
            new("swept", "Swept"),
            new("afro", "Afro"),
            new("hightop", "High Top"),
            new("hero", "Hero"),
            new("afrohawk", "Afro-Hawk"),
            new("bowl", "Bowl"),
            new("undercut", "Undercut"),
            new("sidebraid", "Side Braid"),
            new("topknot", "Top Knot"),
            new("curls", "Long Curls"),
            new("veil", "Veil"),
            new("talltop", "Tall Top"),
            new("loctail", "Loc Tail"),
            new("curtains", "Curtains"),
            new("curltop", "Curly Top"),
            new("locs", "Locs"),
            new("waves", "Waves"),
            new("shaved", "Shaved"),
        };

        // ---------------------------------------------------------------- lookup

        public static Color SkinColor(string key) => Find(Skins, key);
        public static Color HairColor(string key) => Find(Hairs, key);
        public static Color EyeColor(string key) => Find(Eyes, key);

        public static Palette.Ramp SkinRamp(string key) => new(SkinColor(key));

        // ---------------------------------------------------------------- bare skin on gear
        //
        // A piece that leaves skin bare (the Talon set's unarmoured arm) is AUTHORED in one known
        // tone - the default swatch, so anywhere that draws it without a wearer (a picker card, an
        // NFT image) still shows a plausible arm - and the rig repaints those texels in the
        // wearer's own tone at Apply (GearItem.ShowsSkin). The far arm sits in shade, so it is
        // authored in the same ramp scaled by GearSkinFar and repainted the same way.

        public const string GearSkinKey = "fair";
        public const float GearSkinFar = 0.86f;
        public static Palette.Ramp GearSkin => SkinRamp(GearSkinKey);
        public static Palette.Ramp GearSkinShaded => GearSkin.Scaled(GearSkinFar);

        /// <summary>A gear sprite with its bare skin in the wearer's tone. Unchanged for the tone it
        /// was authored in.</summary>
        public static Sprite Reskin(Sprite gear, string skinKey)
            => string.IsNullOrEmpty(skinKey) || skinKey == GearSkinKey
                ? gear
                : Reskin(gear, SkinRamp(skinKey), "skin." + skinKey);

        /// <summary>A gear sprite with its bare skin repainted in any ramp - the armour stand's
        /// wood, so the mannequin's own arm shows where a person's would.</summary>
        public static Sprite Reskin(Sprite gear, Palette.Ramp to, string tag)
        {
            Palette.Ramp from = GearSkin, fromFar = GearSkinShaded, toFar = to.Scaled(GearSkinFar);
            return PixelSprite.Recolour(gear, tag,
                new[] { from.Line, from.Deep, from.Dark, from.Base, from.Light, from.Glow,
                        fromFar.Line, fromFar.Deep, fromFar.Dark, fromFar.Base, fromFar.Light, fromFar.Glow },
                new[] { to.Line, to.Deep, to.Dark, to.Base, to.Light, to.Glow,
                        toFar.Line, toFar.Deep, toFar.Dark, toFar.Base, toFar.Light, toFar.Glow });
        }
        /// <summary>
        /// Hair's ramp, and the one place in the project whose shadows are placed by VALUE rather
        /// than by lerping toward black.
        ///
        /// A hairstyle is mostly one flat tone (<see cref="Palette.Ramp.Light"/>) in the arena, and
        /// at menu density it has to carry THREE readable tones - the measured reference profile is
        /// 45% mass, 30% at 0.86 of it, 24% at 0.71. The default ramp cannot supply that at both
        /// ends of the swatch list: on silver, Glow/Light/Base are within a few hundredths of each
        /// other and the next tone down is a third of the way to black, so there is simply nothing
        /// in between; on black the bottom three collapse the same way. Placing Dark and Deep at
        /// fractions of LIGHT gives every swatch the same spacing, so one set of tone cuts serves
        /// all of them.
        ///
        /// THE MASS IS THE SWATCH. Hair is painted in Light (the hair pass's tones are Light, Dark
        /// and Deep - never Base), and Light used to be the swatch lifted 34% toward WHITE: every
        /// colour came out paler than the chip the picker shows, and a red lifted toward white is
        /// pink - Crimson wore as (0.82, 0.46, 0.50). Lift 0 makes Light the swatch itself, so the
        /// colour picked is the colour worn, and the two shadows are that colour at 0.86 and 0.71
        /// (same hue, same saturation, only darker). Beards and brows share this ramp and follow.
        /// </summary>
        public static Palette.Ramp HairRamp(string key)
            => new Palette.Ramp(HairColor(key), lift: 0f).WithShadowsBelowLight(HairDarkOfLight, HairDeepOfLight);

        /// <summary>Measured off the reference: its three hair tones sit at 1.00 / 0.86 / 0.71.</summary>
        public const float HairDarkOfLight = 0.86f, HairDeepOfLight = 0.71f;

        /// <summary>
        /// Eyes get a HARDER ramp than skin or hair. At two texels across, an eye rendered with
        /// the default gentle lift and shade comes out as two nearly identical mid-tones and the
        /// colour choice stops being visible at all; pushing the light and dark apart is what
        /// makes an amber eye read as amber next to a brown one.
        /// </summary>
        public static Palette.Ramp EyeRamp(string key) => new(EyeColor(key), 0.45f, 0.42f, 0.55f);

        /// <summary>
        /// The head's palette: skin, hair and eyes as the three ramps, plus a handful of FIXED
        /// colours the faces below draw with.
        ///
        /// Those exist because some expressions are not an eye colour at all - a flame, a heart
        /// and a pure white are their own thing, and running them through the eye swatch would
        /// mean a "fire eyes" face that comes out blue. They take free characters: the three ramps
        /// reserve k s d b l h / K S D B L H / 1-6, and everything else is available.
        ///
        /// Only three faces override the swatch this way, and it is worth keeping that number
        /// small - see <see cref="OverridesEyeColour"/>, which is what the customisation screen
        /// reads to avoid offering a colour that cannot do anything.
        /// </summary>
        public static Dictionary<char, Color> HeadPalette(Palette.Ramp skin, Palette.Ramp hair,
                                                          Palette.Ramp eyes, Palette.Ramp? brow = null,
                                                          Palette.Ramp? beard = null)
        {
            var map = Palette.Of(skin, hair, eyes);

            // The beard's own six, for the same reason the brows have theirs: a beard can be another colour
            // than the hair. HeadDetail moves a beard's hair-ramp cells onto these (BeardChars).
            var bd = beard ?? hair;
            map['t'] = bd.Line;
            map['o'] = bd.Deep;
            map['O'] = bd.Dark;
            map['p'] = bd.Base;
            map['P'] = bd.Light;
            map['T'] = bd.Glow;

            // Brows get their own two characters rather than borrowing the hair's uppercase set.
            // They need to be settable INDEPENDENTLY of the hair - dark brows under pale hair is
            // most of what makes a silver-haired character read as deliberate rather than as an
            // old man - and the three ramps Palette.Of hands out are already spoken for.
            // THE SWATCH COLOUR ITSELF, not the ramp's line tone. Ramp.Line lerps 68% toward
            // near-black, so a gold brow came out near-black and every brow colour looked like the
            // same muted brown - the picker showed one thing and the character wore another, which
            // is the one thing a colour picker may never do. Base is exactly what the swatch draws.
            var b = brow ?? hair;
            map['n'] = b.Base;
            map['N'] = b.Dark;
            map['w'] = new Color(1.00f, 1.00f, 1.00f);   // pure white
            // STUBBLE is a shadow on skin, so it is mixed from the skin and the hair - drawn in a hair tone
            // alone ('S', the old stubble) it read as a dark beard with holes in it.
            map['m'] = Color.Lerp(skin.Base, bd.Dark, 0.38f);   // stubble
            map['M'] = Color.Lerp(skin.Base, bd.Dark, 0.58f);   // stubble, along the jaw

            // THE LASH IS BLACK, not a dark version of the eye's own colour, and it needs its own
            // fixed entry to be so. Drawn from the eye ramp it can only ever be that swatch lerped
            // toward black, which on a saturated eye comes out a dark maroon or a dark blue - and
            // then the lash and the iris are the same material at two brightnesses rather than a
            // black lid in front of a coloured eye. Only the menu-density face uses it; see
            // FaceDetail.Eyes.
            //
            // Not pure black: at 0.07 it still clears the darkest skin in the list (obsidian sits
            // at 0.24) while reading as black against every other one.
            map['x'] = new Color(0.07f, 0.06f, 0.10f);   // lash

            // GearItem.HasGlowingEye's fixed red, on the same "harder ramp" EyeRamp uses (an eye
            // is only two texels across, and the default lift/shade collapses to one tone at that
            // size). Reserved unconditionally, the same way the flame/heart/tear characters above
            // are always present whether or not the expression uses them - cheap, and it means
            // OverrideLeftEye never has to know whether the item is actually equipped, only where
            // to look.
            var glow = new Palette.Ramp(new Color(0.85f, 0.10f, 0.08f), 0.45f, 0.42f, 0.55f);
            map['A'] = glow.Line;
            map['E'] = glow.Deep;
            map['F'] = glow.Dark;
            map['G'] = glow.Base;
            map['I'] = glow.Light;
            map['J'] = glow.Glow;
            map['f'] = new Color(0.78f, 0.16f, 0.06f);   // flame, deep
            map['g'] = new Color(0.95f, 0.46f, 0.10f);   // flame, mid
            map['y'] = new Color(1.00f, 0.85f, 0.42f);   // flame, bright
            map['r'] = new Color(0.86f, 0.16f, 0.24f);   // heart
            map['e'] = new Color(0.55f, 0.08f, 0.16f);   // heart, deep
            map['c'] = new Color(0.55f, 0.82f, 0.96f);   // tear
            map['C'] = new Color(0.88f, 0.97f, 1.00f);   // tear, highlight
            // The Wraith's Eye weeps blood: OverrideLeftEye turns that eye's tear to these.
            map['q'] = new Color(0.62f, 0.06f, 0.06f);   // tear, the Wraith's Eye
            map['Q'] = new Color(0.92f, 0.22f, 0.16f);   // tear highlight, the Wraith's Eye
            return map;
        }

        /// <summary>
        /// True when the expression paints its eyes in its own fixed colours, so the EYES swatch
        /// changes nothing.
        ///
        /// The screen says so rather than leaving a row of colours that quietly do nothing - the
        /// same rule as the loadout page not labelling a slot "(disguised)" while the disguise is
        /// being refused. Stars and tears deliberately do NOT appear here: they take the eye ramp,
        /// so a gold-eyed character gets gold stars and the swatch keeps working.
        /// </summary>
        public static bool OverridesEyeColour(string expression)
            => expression == "blank" || expression == "ablaze" || expression == "smitten";

        public static Style HairStyle(string key) => TryDevStyle(key, out var dev) ? dev : Find(HairStyles, key);
        public static Style Beard(string key)
            => _devBeards != null && key != null && _devBeards.TryGetValue(key, out var dev) ? dev : Find(Beards, key);

        // DEV ONLY: trial beards for the facial-hair mock-up sheet (EditorTools.FaceSheet) - never saved. Rows joined
        // by '|', in the beard's own final 36-row space.
        static Dictionary<string, Style> _devBeards;
        public static string DevAddBeard(string key, string name, string rows)
        {
            var grid = rows.Split('|');
            if (grid.Length != BeardRows) return $"{key}: {grid.Length} rows, want {BeardRows}";
            (_devBeards ??= new Dictionary<string, Style>())[key] = new Style(key, name, grid);
            return "ok";
        }
        public static Style Expression(string key)
        {
            if (_devExpressions != null && key != null && _devExpressions.TryGetValue(key, out var dev))
                return new Style(dev.Key, dev.Name, GrowEyes(dev.Grid, 0, liftOpen: true));
            var style = Find(Expressions, key);
            if (DevEyeGrow == 0 && !DrawnEyes.ContainsKey(style.Key)) return style;
            return new Style(style.Key, style.Name, GrowEyes(style.Grid, DevEyeGrow, liftOpen: true));
        }

        /// <summary>
        /// Expressions whose open eyes are HAND-DRAWN rather than drawn by FaceDetail's eye pass, and at which
        /// drawing (see <see cref="EyeArt"/>). Their grid keeps the eye block only as a
        /// marker: <see cref="Expression"/> lifts it, <see cref="StampEyes"/> draws the eye centred on it. Chosen
        /// off the face mock-up sheet (2026-10-27) beside today's eyes, which stay as their own expressions.
        /// </summary>
        static readonly Dictionary<string, string> DrawnEyes = new()
            { ["bright"] = "larger", ["steely"] = "larger", ["starry"] = "star", ["dazed"] = "spiral",
              ["joyful"] = "joyful", ["wide"] = "wide" };

        /// <summary>The drawn eye an expression uses, or null for FaceDetail's own.</summary>
        static string DrawnEyeArt(string expression)
        {
            if (_devExpressions != null && expression != null && _devArt.TryGetValue(expression, out var a)) return a;
            if (DrawnEyes.TryGetValue(Find(Expressions, expression).Key, out var n)) return n;
            return DevEyeGrow switch { 1 => "large", 2 => "larger", _ => null };
        }

        // DEV ONLY: trial expressions for the face mock-up sheet (EditorTools.FaceSheet) - never saved.
        static Dictionary<string, Style> _devExpressions;
        static Dictionary<string, string> _devArt;
        public static void DevAddExpression(string key, string name, string[] grid, string eyeArt)
        {
            (_devExpressions ??= new Dictionary<string, Style>())[key] = new Style(key, name, grid);
            (_devArt ??= new Dictionary<string, string>())[key] = eyeArt;
        }

        /// <summary>
        /// DEV ONLY, for the face mock-up sheet (EditorTools.FaceSheet): every expression's eye blocks drawn
        /// <c>DevEyeGrow</c> texel-pairs bigger - wider toward the outside of the face and taller downward,
        /// never up into the brows - so a larger eye can be judged on every face and brow before any grid is
        /// re-authored. 0 is the game as it stands. Part of the head's cache key.
        /// </summary>
        public static int DevEyeGrow;

        static string[] GrowEyes(string[] grid, int grow, bool liftOpen = false)
        {
            static bool Eyeish(char c) => c is '1' or '2' or '3' or '4' or '5' or '6' or 'w';
            int h = grid.Length, w = grid[0].Length;
            var src = new char[h][]; var dst = new char[h][];
            for (int y = 0; y < h; y++) { src[y] = grid[y].ToCharArray(); dst[y] = grid[y].ToCharArray(); }
            var seen = new bool[h, w];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (seen[y, x] || !Eyeish(src[y][x])) continue;
                // the block: a connected run of eye cells that FILLS its bounding box and is at least four
                // wide - an eye. A star, a diamond, a spiral or a sparkle is a symbol, left as drawn.
                var cells = new List<(int x, int y)>(); var stack = new Stack<(int x, int y)>();
                stack.Push((x, y)); seen[y, x] = true;
                int x0 = x, x1 = x, y0 = y, y1 = y;
                while (stack.Count > 0)
                {
                    var (cx, cy) = stack.Pop(); cells.Add((cx, cy));
                    x0 = System.Math.Min(x0, cx); x1 = System.Math.Max(x1, cx);
                    y0 = System.Math.Min(y0, cy); y1 = System.Math.Max(y1, cy);
                    foreach (var (nx, ny) in new[] { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
                        if (nx >= 0 && ny >= 0 && nx < w && ny < h && !seen[ny, nx] && Eyeish(src[ny][nx]))
                        { seen[ny, nx] = true; stack.Push((nx, ny)); }
                }
                if (cells.Count != (x1 - x0 + 1) * (y1 - y0 + 1) || x1 - x0 + 1 < 4) continue;
                int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
                bool open = bh >= 4;
                // An open EYE (not Blank's white) is drawn by hand at the bigger sizes - StampEyes - so
                // the procedural one is taken out here and the block's place is all that is kept.
                if (liftOpen && open && src[y0][x0] != 'w')
                {
                    for (int yy = y0; yy <= y1; yy++) for (int xx = x0; xx <= x1; xx++) dst[yy][xx] = '.';
                    continue;
                }
                // Height stops at row 21: Expand inserts the lower face's rows at 22, which would split the eye in two.
                int dw = 2 * grow, dh = open ? System.Math.Max(0, System.Math.Min(2 * grow, 21 - y1)) : 0;
                int nx0 = x0 - dw / 2, nx1 = x1 + dw / 2, ny1 = y1 + dh;   // evenly: outward pushed them apart
                for (int yy = y0; yy <= ny1 && yy < h; yy++)
                for (int xx = System.Math.Max(0, nx0); xx <= nx1 && xx < w; xx++)
                {
                    int sy = y0 + (yy - y0) * bh / (ny1 - y0 + 1), sx = x0 + (xx - nx0) * bw / (nx1 - nx0 + 1);
                    dst[yy][xx] = src[sy][sx];
                }
                // a lid drawn right above the block (Determined's) widens with it
                for (int yy = System.Math.Max(0, y0 - 2); yy < y0; yy++)
                {
                    char lid = src[yy][x0];
                    if (lid == '.' || Eyeish(lid)) continue;
                    for (int xx = System.Math.Max(0, nx0); xx <= nx1 && xx < w; xx++) dst[yy][xx] = lid;
                }
            }
            var rows = new string[h];
            for (int y = 0; y < h; y++) rows[y] = new string(dst[y]);
            return rows;
        }

        static Color Find(Swatch[] set, string key)
        {
            foreach (var s in set) if (s.Key == key) return s.Color;
            return set[0].Color;   // unknown key from a newer client: render, do not fail
        }

        static Style Find(Style[] set, string key)
        {
            foreach (var s in set) if (s.Key == key) return s;
            return set[0];
        }

        public static int IndexOf(Swatch[] set, string key)
        {
            for (int i = 0; i < set.Length; i++) if (set[i].Key == key) return i;
            return 0;
        }

        public static int IndexOf(Style[] set, string key)
        {
            for (int i = 0; i < set.Length; i++) if (set[i].Key == key) return i;
            return 0;
        }

        // ---------------------------------------------------------------- composition

        /// <summary>
        /// Skull, then face, then beard, then brow, then hair. Every grid is the same size, so
        /// this is a straight cell-wise overwrite where the upper layer is not transparent.
        ///
        /// The beard goes on AFTER the face now - see the reordering note on <see cref="Head"/>.
        /// It used to go on before, so a mouth would sit IN the beard rather than being swallowed
        /// by it; with the mouth gone, that order only ever meant a beard's jaw content lost cells
        /// to whichever of the six dramatic expressions had marks in the same rows.
        /// </summary>
        /// <summary>
        /// TWO ROWS OF LOWER FACE, added at composition time rather than re-authored into
        /// twenty-seven grids.
        ///
        /// The head was 16x14 and the features were crowded into it: the eyes sit at rows 8-9 and
        /// the mouth at 11, so there was exactly ONE row between them. Each part was proportioned
        /// correctly and the face still read as bunched, which is what too little space between
        /// correct parts looks like.
        ///
        /// The rows go in at index 10 - below the eyes, above the mouth - so the forehead, the
        /// hairline and the row-7 rule for expressions are all untouched. Only the mid-face opens
        /// up, from one row between eyes and mouth to three.
        ///
        /// Done here, once, because the four grid families are composed BY INDEX: inserting the
        /// same rows in all of them is what keeps them aligned. Editing the literals instead would
        /// be twenty-seven separate chances to put a row in the wrong place, and every expression
        /// would have to be re-checked against the "nothing above row 7" rule for no reason.
        ///
        /// The SKULL duplicates the row above rather than inserting a gap - it is the solid the
        /// others are drawn onto, so a blank row there would be a hole through the face.
        /// </summary>
        /// <summary>
        /// ELEVEN, not ten, and the difference is six broken faces.
        ///
        /// Expressions build the eye area across rows 7-10 and put the mouth at 11. Inserting at
        /// 10 cut straight through that: Grin's smile corners, Smitten's hearts, Weeping's tears,
        /// Starry's points and Dazzled's sparkles all had their bottom row torn two rows away from
        /// the rest, leaving a mark hanging in the middle of the cheek. Inserting at 11 puts the
        /// new rows between the eye region and the mouth, which is where the space was wanted
        /// anyway - the whole complaint was that the two were crowded together.
        /// </summary>
        // Doubled alongside every grid this indexes into when LayoutUnit moved 37.5 -> 75 (was
        // FaceRowsAt = 11, FaceRowsAdded = 2) - these count rows of the block-doubled 28-row
        // authored grids below, so the row-number prose throughout this file (row 7, rows 8-9,
        // row 11, etc.) is now stated at HALF the current row index; multiply by two to find the
        // literal row in the doubled grids.
        const int FaceRowsAt = 22, FaceRowsAdded = 4;

        /// <summary>
        /// Rows ABOVE the skull, for hair that leaves the head.
        ///
        /// Anime hair is not a cap - it rises off the crown in locks, and there was nowhere for
        /// those to go: the skull's dome IS row 0, so the tallest possible "spike" was flush with
        /// the top of the head, which is a cap with a bumpy edge.
        ///
        /// The skull, expressions, brows and beards are authored without them and get blank rows
        /// here; the hairstyles' own views (BodyLook.HairViews) are drawn in the full composed
        /// space and use them directly.
        /// </summary>
        // Doubled with everything else (was AuthoredRows = 14, HairRowsAdded = 2) - see the note
        // on FaceRowsAt/FaceRowsAdded above.
        const int AuthoredRows = 28, HairRowsAdded = 4;

        /// <summary>
        /// Expressions whose own artwork includes a BROW.
        ///
        /// Only Determined, now - it is the reason a chosen brow has to erase as well as overpaint,
        /// or the two would sit stacked on row 7 at once. Fierce and Weary used to be here too, each
        /// drawing its own brow on top of the exact same eyes as another expression; once the mouth
        /// was gone that brow was their entire identity, and composing them against a real brow
        /// choice showed it disappearing - see the Expressions header. Cut rather than kept as an
        /// entry with nothing of its own to strip.
        ///
        /// Ablaze, Starry and Dazzled all used to mark row 7 too and were deliberately never added
        /// here - a flame tip and a star's point are not brows, and stripping them would have gutted
        /// the face. They no longer reach row 7 at all; see their own entries.
        /// </summary>
        static readonly string[] BrowedExpressions = { "determined", "steely" };

        /// <param name="leftEyeGlow">
        /// Overwrite the character's own LEFT eye (the one facing the viewer's right in the
        /// default, unmirrored pose - see GlowingEye's own note) to a fixed red, regardless of
        /// the Eyes swatch chosen on the body tab. GearItem.HasGlowingEye asks for this - the
        /// item is meant to REPLACE the eye's colour, not merely glow in front of it, so the
        /// override has to happen where the colour is actually decided rather than as a sprite
        /// laid on top.
        /// </param>
        /// <param name="hair">A hair cap in FINAL composed texels (a BodyLook.HairView's Cap), laid
        /// on last so a fringe covers a sideburn; null for a bare head.</param>
        public static string[] Head(string expression, string beard,
                                    string brows = "natural", bool leftEyeGlow = false,
                                    int gaze = 0, string[] hair = null)
        {
            var face = Expression(expression).Grid;

            // A chosen brow REPLACES the expression's own rather than sitting on top of it.
            if (brows != "natural" && System.Array.IndexOf(BrowedExpressions, expression) >= 0)
                face = StripBrow(face);

            var head = Compose(Expand(Skull, solid: true),
                           // THE BEARD IS NOT EXPANDED - it is the one family authored in final
                           // composed space, so it goes in as-is. It used to pass solid: true and
                           // STRETCH into the inserted rows, on the reasoning that a blank row
                           // through a beard is a bald stripe across the jaw. That reasoning is
                           // still right and the mechanism was still wrong: what stretches is one
                           // authored row repeated five times, which forced rows 25-29 of every
                           // beard to share a single horizontal profile. See the Beards header.
                           //
                           // BEARD NOW PAINTS AFTER THE EXPRESSION, and that reverses the order
                           // this file used to document. It was Beard-then-Expression so a mouth
                           // would show through a beard - "a full beard's whole job is to surround
                           // a mouth... In this order the mouth sits IN the beard." That reason is
                           // gone with the mouth itself, and the order kept the one property that
                           // made sense to keep (an expression can still veto a beard - Blank's
                           // full white eyes, for instance) while creating a new one nothing
                           // asked for: Ablaze, Smitten, Weeping, Starry and Dazzled were all
                           // moved down "into the jaw space the mouth's removal freed up," which
                           // is exactly the beard's own territory, and the old order let their
                           // lower marks punch holes in a beard wherever the two overlapped -
                           // a torn sideburn, a moustache band chopped into three pieces by a
                           // flame's licks. Verified by rendering every beard against every
                           // expression: the eye-level rows (8-9) never collided, only the
                           // decorative tips these six expressions carry below them did.
                           //
                           // A HOLE IS WORSE THAN A TRIM. The header rule for the beard's own
                           // grids is explicit that a gap reads as damage rather than as a
                           // feature - "anything laid on [the chin outline] reads as a hole in the
                           // silhouette." A beard clipping the last texel off a flame's tip or a
                           // heart's point reads as the beard sitting in front of the face, which
                           // is physically what a beard does; a beard with a bite taken out of it
                           // reads as broken art. Painting Beard after Expression (and still
                           // before Brow and Hair, which is unaffected by this - see below) makes
                           // that trade explicitly rather than leaving it to whichever grid
                           // happened to load last.
                           Turned(Expand(face, solid: false), gaze),
                           BeardTurned(BeardGrid(beard), gaze, Beard(beard).Key),
                           Turned(Expand(Brow(brows).Grid, solid: false), gaze),
                           hair ?? new string[0]);

            // After the turn, so it recolours the far eye where the turn left it (still in the
            // right half - the far side only moves by the gaze itself).
            return leftEyeGlow ? OverrideLeftEye(head) : head;
        }

        /// <summary>
        /// TURN THE FACE: move its features - eyes, brows, and everything an expression draws round
        /// them (tears, flames, hearts) - toward where the character is facing, the NEAR side
        /// further than the far side, and narrow the far eye.
        ///
        /// The rig has three directions - facing right, its mirror, and facing away - and the face
        /// grids are drawn dead-on, so the turn is made here. It used to slide both eyes by the same
        /// amount, which kept the face-on spacing: the eyes stayed eight cells apart and the near eye
        /// sat close to the back of the head, so a turned head read as a front view with the hair
        /// in the wrong place. In a 3/4 view the near eye comes round toward the middle of the face
        /// and the far eye stays near the edge, foreshortened - so the near half moves
        /// <paramref name="shift"/> + <see cref="NearEyeLead"/>, the far half <paramref name="shift"/>.
        ///
        /// Runs on the FACE-FEATURE grids (expression, brow) before they are composed, not on the
        /// finished head: moving whole halves carries each brow and mark with its own eye (the old
        /// pass moved only the eye digits, leaving brows and tears behind), and the skull, beard and
        /// hair are never touched. The halves split at the grid's middle - every feature is authored
        /// symmetric about it.
        ///
        /// Shift is in composed texels, always a whole number of 2-texel design cells.
        /// </summary>
        static string[] Gaze(string[] face, int shift)
        {
            int w = face.Length > 0 ? face[0].Length : 0, half = w / 2;
            int near = shift > 0 ? shift + NearEyeLead : shift - NearEyeLead;
            var outRows = new string[face.Length];
            for (int r = 0; r < face.Length; r++)
            {
                var chars = new string('.', w).ToCharArray();
                for (int c = 0; c < w && c < face[r].Length; c++)
                {
                    char ch = face[r][c];
                    if (ch == '.') continue;
                    bool trailing = shift > 0 ? c < half : c >= half;
                    int to = c + (trailing ? near : shift);
                    if (to >= 0 && to < w) chars[to] = ch;
                }

                // AND NARROW THE FAR EYE: the eye on the LEADING side goes round the curve of the
                // skull and away from the camera - see FarEyeTrim.
                int step = shift > 0 ? -1 : 1;
                int from = shift > 0 ? w - 1 : 0;
                for (int c = from; c >= 0 && c < w; c += step)
                {
                    if (chars[c] < '1' || chars[c] > '6') continue;
                    for (int k = 0; k < FarEyeTrim; k++)
                    {
                        int t = c + step * k;
                        if (t >= 0 && t < w && chars[t] >= '1' && chars[t] <= '6') chars[t] = '.';
                    }
                    break;
                }
                outRows[r] = new string(chars);
            }
            return outRows;
        }

        /// <summary>
        /// How much further the NEAR half of the face moves than the far half, in composed texels -
        /// see Gaze. The difference between the two is what closes the eyes' face-on spacing.
        /// </summary>
        const int NearEyeLead = 2;

        static string[] Turned(string[] face, int gaze) => gaze == 0 ? face : Gaze(face, gaze);

        /// <summary>
        /// A beard in the turned view. It cannot slide like the eyes (<see cref="Gaze"/>): it covers the jaw, and
        /// the head's outline does not turn, so a slid beard left bare jaw on one side and hung off the other. It
        /// is WARPED instead, row by row: both edges stay where they are and the face's middle moves as far as
        /// the features do (halfway between the two eyes' moves), so the near cheek stretches and the far one
        /// narrows - the moustache and chin sit under the turned eyes, the sideburns stay on the skull.
        /// </summary>
        static string[] BeardTurned(string[] beard, int gaze, string key = null)
        {
            if (gaze == 0 || beard.Length == 0) return beard;
            int w = beard[0].Length;
            float mid = w / 2f, delta = (gaze + (gaze > 0 ? gaze + NearEyeLead : gaze - NearEyeLead)) / 2f;
            // Knots of a piecewise-linear map, source x -> drawn x. A beard over the whole jaw bends about the
            // middle; one built round the MOUTH (a moustache) moves as a rigid piece, so its two halves keep their
            // shape - warped, the near half stretched to twice the far one and read as lopsided (the user's note,
            // 2026-10-27). Only the strip at each jaw edge gives: the near one stretches, the far one narrows.
            (float s, float d)[] knots = System.Array.IndexOf(RigidBeards, key) < 0
                ? new[] { (0f, 0f), (mid, mid + delta), ((float)w, (float)w) }
                : delta > 0
                    ? new[] { (0f, 0f), (RigidNearEdge, RigidNearEdge + delta), (w - RigidFarEdge, w - RigidFarEdge + delta), ((float)w, (float)w) }
                    : new[] { (0f, 0f), (RigidFarEdge, RigidFarEdge + delta), (w - RigidNearEdge, w - RigidNearEdge + delta), ((float)w, (float)w) };
            var rows = new string[beard.Length];
            for (int r = 0; r < beard.Length; r++)
            {
                var chars = new char[w];
                for (int x = 0; x < w; x++)
                {
                    float dx = x + 0.5f, sx = dx;
                    for (int k = 1; k < knots.Length; k++)
                    {
                        if (dx > knots[k].d && k < knots.Length - 1) continue;
                        var (s0, d0) = knots[k - 1]; var (s1, d1) = knots[k];
                        sx = s0 + (dx - d0) * (s1 - s0) / (d1 - d0);
                        break;
                    }
                    int src = System.Math.Clamp((int)sx, 0, w - 1);
                    chars[x] = src < beard[r].Length ? beard[r][src] : '.';
                }
                rows[r] = new string(chars);
            }
            return rows;
        }

        /// <summary>Beards built round the mouth, which turn as a rigid piece - see BeardTurned.</summary>
        static readonly string[] RigidBeards = { "moustache", "goatee", "horseshoe", "vandyke" };
        /// <summary>BeardTurned's jaw-edge strips for a rigid beard, in composed texels: the near edge's strip
        /// stretches, the far edge's (wider, so the moustache's far tip lands inside the jaw) narrows.</summary>
        const float RigidNearEdge = 4f, RigidFarEdge = 8f;

        /// <summary>
        /// How many composed texels come off the OUTER edge of the far eye - see Gaze. One: the far
        /// eye ends up about three-quarters of the near one. A whole cell (two) left it half width,
        /// a sliver; that was needed only while the eyes slid by one amount and the trim was the
        /// only cue the head had turned. Finer than the design grid is fine here - FaceDetail
        /// redraws the eye inside its box.
        /// </summary>
        const int FarEyeTrim = 1;

        /// <summary>
        /// The eye ramp's characters ('1'-'6'), remapped to the fixed glow ramp's own
        /// ('A'/'E'/'F'/'G'/'I'/'J' - see HeadPalette) wherever they land in the RIGHT half of
        /// the grid - the character's own left eye, by the same column-midpoint test
        /// FaceDetail.Eyes.Draw uses to tell the two eyes apart. A post-pass over the composed
        /// grid rather than a change to Expression/Compose: the two eyes are drawn from the
        /// exact same digits by construction (one Eyes swatch colours both), so there is nothing
        /// upstream that already knows which blob is which - only a position does.
        /// </summary>
        static readonly Dictionary<char, char> EyeGlowChars = new()
            { ['1'] = 'A', ['2'] = 'E', ['3'] = 'F', ['4'] = 'G', ['5'] = 'I', ['6'] = 'J' };

        /// <summary>The same eye's TEAR (Weeping's) goes red with it - kept apart from EyeGlowChars, so the
        /// glow's anchor (FindLeftEyeBox) stays on the eye and never stretches down the tear.</summary>
        static readonly Dictionary<char, char> TearGlowChars = new() { ['c'] = 'q', ['C'] = 'Q' };

        /// <summary>The left eye's tear in the Wraith's red on a detailed head (FaceDetail draws the eye's own red;
        /// the tear is a plain mark, so it is recoloured after, by OverrideLeftEye's right-half rule).</summary>
        static void RedTear(string[] big)
        {
            int w = big[0].Length;
            for (int y = 0; y < big.Length; y++)
            {
                char[] row = null;
                for (int x = w / 2; x < w; x++)
                {
                    if (!TearGlowChars.TryGetValue(big[y][x], out var rep)) continue;
                    row ??= big[y].ToCharArray();
                    row[x] = rep;
                }
                if (row != null) big[y] = new string(row);
            }
        }

        static string[] OverrideLeftEye(string[] grid)
        {
            int w = grid[0].Length, half = w / 2;
            var outp = (string[])grid.Clone();
            for (int y = 0; y < grid.Length; y++)
            {
                char[] row = null;
                for (int x = half; x < w; x++)
                {
                    if (!EyeGlowChars.TryGetValue(grid[y][x], out var rep)
                        && !TearGlowChars.TryGetValue(grid[y][x], out rep)) continue;
                    row ??= grid[y].ToCharArray();
                    row[x] = rep;
                }
                if (row != null) outp[y] = new string(row);
            }
            return outp;
        }

        /// <summary>
        /// Where <see cref="OverrideLeftEye"/> actually landed, in the SAME texel grid <see
        /// cref="Head"/> (the coarse, non-menu build) returns - or null if the grid carries no
        /// glow characters, which only happens if it was built with leftEyeGlow false.
        ///
        /// Measured off the grid rather than assumed from a fixed column/row, the same way the
        /// override itself is: an expression's eye box moves a texel or two between styles (see
        /// the Skull's own doc on where the raw art places it), and a glow effect placed by a
        /// constant would drift off whichever expression it wasn't tuned against. The caller uses
        /// this to anchor an actual glow sprite - GlowingEye needs a real world position and the
        /// baked pixel art alone cannot pulse.
        /// </summary>
        public static (int x0, int y0, int x1, int y1)? FindLeftEyeBox(string[] grid)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            for (int y = 0; y < grid.Length; y++)
            for (int x = 0; x < grid[y].Length; x++)
            {
                if (!IsGlowChar(grid[y][x])) continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
            return x1 >= x0 ? (x0, y0, x1, y1) : null;
        }

        static bool IsGlowChar(char c) => c is 'A' or 'E' or 'F' or 'G' or 'I' or 'J';

        /// <summary>Clear rows 6-7, where an expression's own brow lives.</summary>
        static string[] StripBrow(string[] grid)
        {
            var rows = (string[])grid.Clone();
            for (int y = 6; y <= 7 && y < rows.Length; y++) rows[y] = new string('.', rows[y].Length);
            return rows;
        }

        /// <summary>What <see cref="Expand"/> returns, which is the space beards are authored in.</summary>
        const int BeardRows = AuthoredRows + FaceRowsAdded + HairRowsAdded;

        /// <summary>
        /// A beard's grid, which is ALREADY in final composed space and must not be expanded.
        ///
        /// The guard is the same one <see cref="Expand"/> carries and exists for the same reason:
        /// a grid of the wrong length lines up with nothing and renders as marks scattered across
        /// the wrong rows, which looks like bad art rather than a bad array. Beards are the one
        /// family where the two formats are only eight rows apart, so a new one written in the
        /// 28-row shape every other family uses would compose without erroring and simply draw
        /// the whole beard eight rows too high.
        /// </summary>
        static string[] BeardGrid(string key)
        {
            var grid = Beard(key).Grid;
            if (grid.Length != BeardRows)
                Debug.LogWarning($"[BodyLook] beard '{key}' is {grid.Length} rows - beards are " +
                                 $"authored in FINAL composed space and must be exactly " +
                                 $"{BeardRows}. Composing it unchanged; it will sit " +
                                 $"{BeardRows - grid.Length} rows out of place.");
            return grid;
        }

        static string[] Expand(string[] grid, bool solid)
        {
            // A short grid is returned UNEXPANDED, which lines up with nothing and renders as
            // marks scattered across the wrong rows. It cost a render to spot on Starry and Dazed,
            // both written one row short, because the failure looks like bad art rather than a
            // bad array - so it says so now.
            if (grid.Length < AuthoredRows)
            {
                Debug.LogWarning($"[BodyLook] a {grid.Length}-row grid cannot be expanded - every " +
                                 $"grid must be {AuthoredRows} rows (or {AuthoredRows + HairRowsAdded} " +
                                 "for hair that leaves the head). Composing it unchanged.");
                return grid;
            }

            string blank = new('.', grid[0].Length);

            // A grid authored two rows tall carries its own hair rows at the top; every other one
            // gets blanks there. Split them off first so the lower-face insert below can be
            // counted against the 14 rows the skull is authored in, whichever kind this is.
            bool tall = grid.Length >= AuthoredRows + HairRowsAdded;
            var above = new List<string>(HairRowsAdded);
            for (int i = 0; i < HairRowsAdded; i++) above.Add(tall ? grid[i] : blank);

            int from = tall ? HairRowsAdded : 0;

            var rows = new List<string>(AuthoredRows + FaceRowsAdded + HairRowsAdded);
            rows.AddRange(above);
            for (int y = from; y < grid.Length; y++)
            {
                if (y - from == FaceRowsAt)
                    for (int i = 0; i < FaceRowsAdded; i++)
                        rows.Add(solid ? grid[from + FaceRowsAt - 1] : blank);
                rows.Add(grid[y]);
            }
            return rows.ToArray();
        }

        static string[] Compose(string[] under, params string[][] overs)
        {
            var rows = (string[])under.Clone();
            foreach (var over in overs)
            {
                for (int y = 0; y < rows.Length && y < over.Length; y++)
                {
                    var line = rows[y].ToCharArray();
                    for (int x = 0; x < line.Length && x < over[y].Length; x++)
                        if (over[y][x] != '.') line[x] = over[y][x];
                    rows[y] = new string(line);
                }
            }
            return rows;
        }

        /// <summary>
        /// Cache key for the composed head sprite. PixelSprite.From caches by key and never looks
        /// at the grid, so every field that changes a pixel has to appear here - miss one and the
        /// first head built wins forever, which looks exactly like the setting being ignored.
        /// </summary>
        /// <summary>The brow's ramp: its own colour when set, the hair's when not.</summary>
        public static Palette.Ramp BrowRamp(Chain.Appearance a)
            => HairRamp(string.IsNullOrEmpty(a.BrowColour) ? a.Hair : a.BrowColour);

        /// <summary>The beard's ramp: its own colour when set, the hair's when not.</summary>
        public static Palette.Ramp BeardRamp(Chain.Appearance a)
            => HairRamp(string.IsNullOrEmpty(a.BeardColour) ? a.Hair : a.BeardColour);

        public static string HeadKey(Chain.Appearance a, bool leftEyeGlow = false, int gaze = 0)
            => $"body.head.{a.Skin}.{a.Hair}.{a.Eyes}.{a.HairStyle}.{a.Expression}.{a.Beard}.{a.Brows}.{a.BrowColour}" +
               (string.IsNullOrEmpty(a.BeardColour) ? "" : ".bc" + a.BeardColour) +
               (leftEyeGlow ? ".glow" : "") + (gaze != 0 ? ".g" + gaze : "") + (DevEyeGrow != 0 ? ".eye" + DevEyeGrow : "") + (string.IsNullOrEmpty(a.Mark) || a.Mark == "none" ? "" : ".m" + a.Mark);

        // ---------------------------------------------------------------- the mane

        /// <summary>
        /// The hair that falls past the skull, as its own grid - or null for a style that has none.
        ///
        /// COVERED HEAD CLIPS IT to the LOWER HALF of the face: rows with any part at or above
        /// <see cref="HelmHairLine"/> are dropped, and everything below keeps drawing. Hair
        /// therefore flows out from under a helm or hood rather than disappearing with it, and
        /// nothing pokes through the crown or the cowl.
        ///
        /// Blanked rather than removed: dropping the rows would move the grid's top edge, and the
        /// sprite is placed by that edge - a clipped mane would slide up the head by however much
        /// was cut. The transparent rows cost nothing but keep the anchor honest.
        /// </summary>
        public static string[] Mane(string hairStyle, bool helm, bool back = false)
        {
            var view = View(HairStyle(hairStyle).Key, back);
            return view?.Mane == null ? null : Clip(view.Mane, view.ManeTop, 1, helm);
        }

        /// <summary>The top edge of the mane <see cref="Mane"/> returns for this view - see
        /// <see cref="HairView.ManeTop"/> for the frame it is stated in.</summary>
        public static int ManeTopFor(string hairStyle, bool back = false)
            => View(HairStyle(hairStyle).Key, back)?.ManeTop ?? ManeHeadTop;

        /// <summary>
        /// Blank the rows a covered head hides, at whatever density the grid was drawn at.
        ///
        /// <paramref name="scale"/> is rows per source texel, so the same line lands in the same
        /// place on a 1x mane and a 2x one - the clip is a fact about the head, not about how
        /// finely the hair happens to be drawn.
        ///
        /// Keyed on the row's TOP edge, not its bottom: a row is dropped the moment ANY of it
        /// reaches the line, rather than surviving whole because most of it sits below one. The
        /// bottom-edge version let a row that merely STARTED below the line keep the sliver of
        /// itself poking out above it too - which is exactly the "clips through the top" bug this
        /// exists to fix, not a rounding nicety.
        /// </summary>
        static string[] Clip(string[] mane, int top, int scale, bool helm)
        {
            if (!helm) return mane;
            var rows = (string[])mane.Clone();
            for (int r = 0; r < rows.Length; r++)
            {
                float rowTop = top - r / (float)scale;
                if (rowTop >= HelmHairLine) rows[r] = new string('.', rows[r].Length);
            }
            return rows;
        }

        /// <summary>
        /// Cache key for the mane sprite. Only the hairstyle, the hair colour and the helm state
        /// change a texel of it - no skin, no eyes, no expression, since none of them is drawn
        /// here - and leaving any of the three out would hand back the first mane ever built.
        /// </summary>
        public static string ManeKey(Chain.Appearance a, bool helm, bool back = false)
            => $"body.mane.{a.HairStyle}.{a.Hair}.{(helm ? "helm" : "bare")}" + (back ? ".back" : "");

        // ================================================================ menu-density head
        //
        // The same face at four times the arena's OWN density, for the character screen only -
        // still four times, even though the arena density itself doubled (37.5 -> 75) when armour
        // and body moved to match weapons. DetailScale below halved (4 -> 2) to compensate: the
        // arena grids (Skull, Expressions, Beards, Brows) are now
        // block-doubled versions of themselves, so reaching the same absolute menu resolution
        // needs half the additional multiplier it used to.
        //
        // DERIVED from the (now 32x28) grids, not authored beside them. Authoring would mean a
        // second set of one skull, eight hairstyles, twelve expressions and six beards - twenty-
        // seven grids at 64x56 - and every one of them a chance for the detailed face to stop being
        // the same character as the one in the arena. Here there is one source of truth and the
        // detail is a PASS over it. (Hair is the exception: its views are drawn at the body's own
        // density and stamped at 2x here, through the same hair pass - see BodyLook.HairViews.)
        //
        // The scale-up alone is worth nothing: block-doubling is provably pixel-identical (see
        // "More pixels is free; more DETAIL is what costs"). Everything below is what the extra
        // cells are actually spent on, and each one is a rule rather than a redrawing:
        //
        //   1. the outline is SMOOTHED, so a diagonal stops being a four-texel staircase
        //   2. the skull gets a light from the upper left and a shadow under the jaw
        //   3. the hair gets a highlight band and a parting, following the cap it already has
        //   4. an EYE becomes an eye - lash line, iris, highlight - instead of a 2x2 block
        //
        // None of them adds a FEATURE. That is the line an earlier same-numbered 32x28 attempt
        // crossed, back when the arena grid was still 16x14: it grew a nose and a shaded crown to
        // justify the cells, and stopped being this character. The 32x28 the arena grid now IS is
        // a different thing - a mechanical block-double, pixel-identical to the 16x14 it replaced -
        // and is not a second attempt at that same mistake.

        // Halved from 4 - see the section header above. bodyPpu * DetailScale (75 * 2 = 150) must
        // keep landing on DemoGear.MenuPpu, which did not move.
        public const int DetailScale = 2;

        public static string DetailKey(Chain.Appearance a, bool leftEyeGlow = false)
            => "menu." + HeadKey(a, leftEyeGlow);

        /// <summary>
        /// The composed head at <paramref name="scale"/>x, rendered rather than merely enlarged.
        ///
        /// <paramref name="scale"/> is <see cref="DetailScale"/> for the character screen and 1
        /// for the ARENA, which draws this same pass at the body's own density: the composed grid
        /// is a block-double of the 16x14 the features were authored on, so at 1x the pass has the
        /// same two texels per authored cell to spend that the menu face was first built with.
        ///
        /// <paramref name="gaze"/> is the arena's turned look (see <see cref="Gaze"/>). It is
        /// applied to the EXPRESSION grid as well as the composed head, because that is where
        /// FaceDetail finds its eye boxes - shifting only the head would draw the detailed eyes
        /// where the flat ones used to be, and leave the far eye untrimmed.
        /// </summary>
        public static string[] HeadDetail(string expression, string hairStyle, string beard,
                                          string brows, string hair, bool leftEyeGlow = false,
                                          int scale = DetailScale, int gaze = 0, string mark = "none")
        {
            // The EXPANDED expression grid, not the authored one. Both the lower-face rows and the
            // rows above the skull shift every mark down, so the raw grid's eye coordinates are
            // two rows out - which put the eyes on the hairline and mangled them, with nothing
            // about the render saying the cause was an index rather than the art.
            var face = Expand(Expression(expression).Grid, solid: false);
            if (gaze != 0) face = Gaze(face, gaze);
            // The beard in the same final space Head composes it in, so FaceDetail can tell its own
            // hair-toned cells apart from a beard's - see the note on FaceDetail.Hair.Draw.
            var beardMask = BeardTurned(BeardGrid(beard), gaze, Beard(beard).Key);

            // The style's TURNED view, composed into the head like any other layer - see
            // BodyLook.HairViews. Every style has one.
            var side = View(HairStyle(hairStyle).Key, back: false);
            var big = FaceDetail.Build(Head(expression, beard, brows, gaze: gaze, hair: side?.Cap), face,
                                       scale, HairValue(hair), side?.Mane != null, beardMask, leftEyeGlow);
            BeardOwnInk(big, beardMask, side?.Cap, scale);
            var drawn = DrawnEyeArt(expression);
            if (drawn != null) StampEyes(big, expression, gaze, scale, drawn, leftEyeGlow);
            if (leftEyeGlow) RedTear(big);
            StampMark(big, mark, gaze, scale);
            return big;
        }

        /// <summary>The hair ramp's characters and the beard's own, in the same order (HeadPalette).</summary>
        const string HairChars = "KSDBLH", BeardChars = "toOpPT";

        /// <summary>
        /// Moves the beard's cells off the hair ramp onto its own (Appearance.BeardColour). A beard is authored in
        /// the hair's characters, so it is told apart by its grid: a cell the beard covers and the hair cap does not
        /// (hair paints over the beard, and a lock there stays hair).
        /// </summary>
        static void BeardOwnInk(string[] big, string[] beard, string[] cap, int scale)
        {
            if (beard == null || beard.Length == 0) return;
            for (int py = 0; py < big.Length; py++)
            {
                int y = py / scale;
                if (y >= beard.Length) break;
                char[] line = null;
                for (int px = 0; px < big[py].Length; px++)
                {
                    int x = px / scale;
                    if (x >= beard[y].Length || beard[y][x] == '.') continue;
                    if (cap != null && y < cap.Length && x < cap[y].Length && cap[y][x] != '.') continue;
                    int i = HairChars.IndexOf(big[py][px]);
                    if (i < 0) continue;
                    line ??= big[py].ToCharArray();
                    line[px] = BeardChars[i];
                }
                if (line != null) big[py] = new string(line);
            }
        }

        // ---------------------------------------------------------------- hand-drawn big eyes
        //
        // The face mock-up's Large and Larger eyes, after the user's reference sprite: tall, a heavy black
        // upper lash, an iris filling most of the eye (dark under the lash, light below), a white glint, a
        // sliver of white at the OUTER corner only. Drawn per eye - the near one wide, the far one
        // foreshortened - in eye-ramp digits, so the swatch still colours them. Measured off the reference:
        // each eye ~30% of the face's width and nearly half its height, ~28% apart, low on the face.
        //   x lash   w white   1 line   2 deep   3 dark   4 base   5 light   6 glow   . leave the skin
        /// <summary>
        /// How far the hand-drawn eyes sit ABOVE the block they replace, in texels. They are twice the block's
        /// height and reached down into the rows every beard starts on (the moustache line), so a beard drew over
        /// the bottom of the eye. Chosen off the face mock-up sheet, 2026-10-30, over trimming the beard round the
        /// eye (holes in the beard) and starting beards lower (most of a beard gone on these faces).
        /// </summary>
        const int DrawnEyeLift = 3;

        static readonly Dictionary<string, (string[] near, string[] far)> EyeArt = new()
        {
            ["large"] = (new[] { ".xxxxx.", "xxxxxxx", ".w2w22x", ".w3333x", "..4554.", "...11.." },
                         new[] { "xxxx.", "xxxxx", "x2w2w", "x333w", ".455.", ".11.." }),
            ["larger"] = (new[] { ".xxxxxxx.", "xxxxxxxxx", ".w22222x.", ".w2ww33x.", ".w3ww34x.", "..34444..", "..45554..", "...111..." },
                          new[] { "xxxxx.", "xxxxxx", "x222w.", "xww3w.", "xww3..", ".444..", ".554..", ".11..." }),
            // Starry: the glint is a four-point star
            ["star"] = (new[] { ".xxxxxxx.", "xxxxxxxxx", ".w22w22x.", ".w2www3x.", ".wwwwwwx.", "..3www4..", "..45w54..", "...111..." },
                        new[] { "xxxxx.", "xxxxxx", "x2w2w.", "xwwwww", "x3w3w.", ".444..", ".554..", ".11..." }),
            // Joyful: closed, upturned arcs
            ["joyful"] = (new[] { ".........", ".........", ".........", "..xxxxx..", ".xx...xx.", ".x.....x.", ".........", "........." },
                          new[] { "......", "......", "......", ".xxxx.", "xx..xx", "x....x", "......", "......" }),
            // Wide: a small iris in a white eye, the lash thin
            ["wide"] = (new[] { ".xxxxxxx.", "xwwwwwwwx", ".wwwwwww.", ".www2www.", ".ww212ww.", ".www2www.", "..wwwww..", "...xxx..." },
                        new[] { "xxxxx.", "xwwwwx", "xwwww.", "xw2ww.", "x212w.", "xw2ww.", ".www..", ".xx..." }),
            // Dazed: the iris is a spiral
            ["spiral"] = (new[] { ".xxxxxxx.", "xxxxxxxxx", ".w11111x.", ".w14441x.", ".w14141x.", "..14111..", "..14444..", "...111..." },
                          new[] { "xxxxx.", "xxxxxx", "x1111w", "x1441w", "x1411.", ".1444.", ".111..", "......" }),
        };

        /// <summary>
        /// Face marks: drawn over the composed face in its own 32 x 36 grid and turned with it, ONLY over skin and
        /// brow - never over an eye, whichever eye the face has (a scar over the eye passes behind it). Chosen off
        /// the face mock-up sheet, 2026-10-27 (Scar) and 2026-10-30 (the rest: scars in the skin's own highlight
        /// and shadow, stitches and the Sulfur Brand in its line and deep, ink in the lash's black); keys are saved
        /// (Appearance.Mark), so append, never rename.
        /// </summary>
        public static readonly Style[] Marks =
        {
            new("none", "None"),
            new("scar", "Scar", new[] { "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "......hd........................", "......hd........................", "......h.d.......................", ".......hd.......................", ".......hd.......................", ".......hd.......................", "........d.......................", "........hd......................", "........hd......................", "........hd......................", "........h.d.....................", ".........hd.....................", ".........hd.....................", ".........hd.....................", "..........d.....................", "..........hd....................", "..........hd....................", "................................", "................................", "................................", "................................" }),
            new("cheek", "Cheek Scar", new[] { "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "...........................hd...", "..........................hd....", ".........................hd.....", "........................hd......", ".......................hd.......", "................................", "................................", "................................", "................................", "................................" }),
            new("crossed", "Crossed", new[] { "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "......hd..h.....................", ".......hdh......................", "........hd......................", ".......h.hd.....................", "......h...hd....................", "................................", "................................", "................................", "................................", "................................" }),
            new("stitched", "Stitched", new[] { "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "......................k.k.k.k...", ".....................dddddddd...", "......................k.k.k.k...", "................................", "................................", "................................", "................................", "................................", "................................" }),
            new("freckles", "Freckles", new[] { "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", ".....d...d...d....d...d...d.....", ".......d...d...d....d...d.......", "........d..............d........", "................................", "................................", "................................", "................................", "................................", "................................", "................................" }),
            new("tally", "Tally", new[] { "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "......................x.x.x.....", "......................x.x.x.....", "......................x.x.x.....", "......................x.x.x.....", "................................", "................................", "................................", "................................", "................................", "................................" }),
            new("sulfur", "Sulfur Brand", new[] { "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "........s.......................", ".......s.s......................", "......sssss.....................", "........s.......................", "......sssss.....................", "........s.......................", "................................", "................................", "................................", "................................" }),
            new("browcut", "Brow Cut", new[] { "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", ".........................hd.....", ".........................hd.....", "........................hd......", "........................hd......", ".......................hd.......", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................", "................................" }),
        };

        public static Style Mark(string key)
            => _devMarks != null && key != null && _devMarks.TryGetValue(key, out var dev) ? dev
             : Find(Marks, string.IsNullOrEmpty(key) ? "none" : key);

        // DEV ONLY: trial marks for the face mock-up sheet (EditorTools.FaceSheet) - never saved. Rows joined by
        // '|', in the mark's own 32 x 36 space.
        static Dictionary<string, Style> _devMarks;
        public static string DevAddMark(string key, string name, string rows)
        {
            var grid = rows.Split('|');
            if (grid.Length != BeardRows) return $"{key}: {grid.Length} rows, want {BeardRows}";
            (_devMarks ??= new Dictionary<string, Style>())[key] = new Style(key, name, grid);
            return "ok";
        }

        static void StampMark(string[] big, string markKey, int gaze, int scale)
        {
            var mark = Mark(markKey).Grid;
            if (mark == null) return;
            var m = gaze != 0 ? Gaze(mark, gaze) : mark;
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

        /// <summary>Where each hand-drawn eye goes: its picture and its top-left, in the composed face's texels
        /// (the coarse head's own grid). Centred on the eye block the expression keeps as a marker, nudged a texel
        /// in toward the nose, its top lifted DrawnEyeLift - 1 texels above the block's, clear of the beard.</summary>
        static List<(string[] pic, int ax, int ay)> DrawnEyePlacements(string expression, int gaze, string art)
        {
            var placed = new List<(string[] pic, int ax, int ay)>();
            var src = _devExpressions != null && _devExpressions.TryGetValue(expression, out var d) ? d.Grid : Find(Expressions, expression).Grid;
            var face = Expand(src, solid: false);
            if (gaze != 0) face = Gaze(face, gaze);
            // Eyes only: a connected run of eye digits that FILLS its bounding box (the rule GrowEyes and
            // FaceDetail use) - a star, a diamond or a spiral is a symbol and gets no eye drawn on it.
            static bool Dig(char c) => c >= '1' && c <= '6';
            var boxes = new List<(int x0, int y0, int x1, int y1)>();
            int fh = face.Length, fw = face[0].Length;
            var seen = new bool[fh, fw];
            for (int y = 0; y < fh; y++)
            for (int x = 0; x < fw; x++)
            {
                if (seen[y, x] || !Dig(face[y][x])) continue;
                var stack = new Stack<(int x, int y)>(); stack.Push((x, y)); seen[y, x] = true;
                int n = 0, x0 = x, x1 = x, y0 = y, y1 = y;
                while (stack.Count > 0)
                {
                    var (cx, cy) = stack.Pop(); n++;
                    x0 = System.Math.Min(x0, cx); x1 = System.Math.Max(x1, cx);
                    y0 = System.Math.Min(y0, cy); y1 = System.Math.Max(y1, cy);
                    foreach (var (nx, ny) in new[] { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
                        if (nx >= 0 && ny >= 0 && nx < fw && ny < fh && !seen[ny, nx] && Dig(face[ny][nx]))
                        { seen[ny, nx] = true; stack.Push((nx, ny)); }
                }
                // the far eye is trimmed to 3 wide in the turned view
                if (n == (x1 - x0 + 1) * (y1 - y0 + 1) && x1 - x0 + 1 >= 3 && y1 - y0 + 1 >= 4) boxes.Add((x0, y0, x1, y1));
            }
            if (boxes.Count == 0) return placed;
            int nearX = int.MaxValue; foreach (var b in boxes) nearX = System.Math.Min(nearX, b.x0);
            foreach (var b in boxes)
            {
                bool near = b.x0 == nearX;
                var pair = EyeArt[art]; var pic = near ? pair.near : pair.far;
                float cx = (b.x0 + b.x1 + 1) / 2f + (near ? 1 : -1);
                placed.Add((pic, (int)System.Math.Round(cx - pic[0].Length / 2f), b.y0 + 1 - DrawnEyeLift));
            }
            return placed;
        }

        /// <summary>The character's own LEFT eye - the one on the grid's right half, FaceDetail's and
        /// OverrideLeftEye's rule - judged by the drawn picture's centre.</summary>
        static bool IsLeftEye(string[] pic, int ax, int faceWidth) => ax + pic[0].Length / 2f > faceWidth / 2f;

        /// <summary>A drawn eye's texel in the Wraith's Eye's red: the iris digits onto the glow ramp; an eye with
        /// no iris at all (Joyful's closed arcs) has its line drawn in the glow's line instead.</summary>
        static char GlowTone(char ch, bool hasIris)
            => EyeGlowChars.TryGetValue(ch, out var g) ? g : (!hasIris && ch == 'x' ? 'A' : ch);

        static bool HasIris(string[] pic)
        {
            foreach (var row in pic) foreach (var ch in row) if (ch >= '1' && ch <= '6') return true;
            return false;
        }

        static void StampEyes(string[] big, string expression, int gaze, int scale, string art, bool leftEyeGlow = false)
        {
            bool squint = System.Array.IndexOf(BrowedExpressions, Find(Expressions, expression).Key) >= 0;   // Determined's narrowed eye
            int faceW = big[0].Length / scale;
            foreach (var (pic, ax, ay) in DrawnEyePlacements(expression, gaze, art))
            {
                bool glow = leftEyeGlow && IsLeftEye(pic, ax, faceW);
                bool iris = HasIris(pic);
                for (int r = 0; r < pic.Length; r++)
                for (int c = 0; c < pic[r].Length; c++)
                {
                    char ch = pic[r][c];
                    if (squint && r <= 2 && ch != '.') ch = 'x';   // Determined's narrowed eye
                    if (ch == '.') continue;
                    if (glow) ch = GlowTone(ch, iris);
                    for (int dy = 0; dy < scale; dy++)
                    for (int dx = 0; dx < scale; dx++)
                        PixelDetail.Put(big, (ax + c) * scale + dx, (ay + r) * scale + dy, ch);
                }
            }
        }

        /// <summary>
        /// Where the Wraith's Eye's glow sits, in the coarse head's texels: the red iris of whichever eye the face
        /// draws - <see cref="FindLeftEyeBox"/> over the override for today's eyes, the drawn picture's iris for a
        /// hand-drawn eye (its block is lifted out of the grid, so the override finds nothing there).
        /// </summary>
        public static (int x0, int y0, int x1, int y1)? GlowEyeBox(string expression, string beard, string brows, int gaze)
        {
            var art = DrawnEyeArt(expression);
            if (art == null) return FindLeftEyeBox(Head(expression, beard, brows, leftEyeGlow: true, gaze: gaze));
            int faceW = Head(expression, beard, brows, gaze: gaze)[0].Length;
            foreach (var (pic, ax, ay) in DrawnEyePlacements(expression, gaze, art))
            {
                if (!IsLeftEye(pic, ax, faceW)) continue;
                bool iris = HasIris(pic);
                int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
                for (int r = 0; r < pic.Length; r++)
                for (int c = 0; c < pic[r].Length; c++)
                {
                    char ch = pic[r][c];
                    if (iris ? !(ch >= '1' && ch <= '6') : ch == '.') continue;
                    x0 = System.Math.Min(x0, ax + c); x1 = System.Math.Max(x1, ax + c);
                    y0 = System.Math.Min(y0, ay + r); y1 = System.Math.Max(y1, ay + r);
                }
                if (x1 >= x0) return (x0, y0, x1, y1);
            }
            return null;
        }

        /// <summary>
        /// A BARE head through the same detail pass - no hair, no beard, natural brows - for
        /// pictures that want the head's own shape rather than a character (the Wraith's Eye's
        /// display art). Same grid and eye placement as <see cref="HeadDetail"/>, ungazed.
        /// </summary>
        public static string[] BareHeadDetail(string expression, bool leftEyeGlow, int scale)
        {
            var face = Expand(Expression(expression).Grid, solid: false);
            return FaceDetail.Build(Head(expression, "none", leftEyeGlow: leftEyeGlow), face,
                                    scale, 0.5f, false, BeardGrid("none"), leftEyeGlow);
        }

        /// <summary>
        /// The head with the hair that LEAVES the skull removed - its <see cref="HairRowsAdded"/>
        /// overhead rows blanked, at whatever <paramref name="scale"/> the grid was built at.
        ///
        /// For a head under a hood or helm: the same rule <see cref="Mane"/> already applies to
        /// the length ("nothing pokes through the crown or the cowl"), applied to the part of the
        /// hair the head sprite carries itself. The flat caps never used these rows, which is why
        /// this was never needed; the detail pass draws the hand-authored menu hair, and a style
        /// like High Tail domes up through them and out past a cowl's narrow crown.
        /// </summary>
        public static string[] ClipOverhead(string[] head, int scale = 1)
        {
            var rows = (string[])head.Clone();
            int n = Mathf.Min(rows.Length, HairRowsAdded * scale);
            for (int r = 0; r < n; r++) rows[r] = new string('.', rows[r].Length);
            return rows;
        }

        /// <summary>
        /// A hair swatch's own brightness, which is what the menu-density pass leans its exposure
        /// on. Max channel rather than a luminance weighting: what matters here is how much
        /// headroom the ramp has left above the base, and that is set by its brightest channel.
        /// </summary>
        public static float HairValue(string hair)
        {
            var c = HairColor(hair);
            return Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        }

        /// <summary>
        /// The mane at <see cref="DetailScale"/>x, through the same hair pass the head's own cap
        /// goes through.
        ///
        /// It HAS to be the same pass. The mane is most of the hair on a long style, so running the
        /// head through a detail treatment and block-scaling the mane beside it would put a
        /// rendered fringe on top of a blocky length, joined down the middle of one head of hair.
        /// </summary>
        public static string[] ManeDetail(string hairStyle, bool helm, string hair,
                                          int scale = DetailScale, bool back = false)
        {
            var view = View(HairStyle(hairStyle).Key, back);
            return view?.Mane == null
                ? null
                : FaceDetail.BuildHair(Clip(view.Mane, view.ManeTop, 1, helm), scale, scale,
                                       view.ManeTop, HairValue(hair));
        }
    }
}
