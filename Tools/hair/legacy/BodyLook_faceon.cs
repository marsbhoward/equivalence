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
    /// skin skull, then a beard, then an expression, then a hairstyle. Later layers win wherever
    /// they are not transparent, so a fringe can legitimately hang over a brow. Composition
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
    /// weapons it's worn beside, and this grid (Skull, every Expression/Beard/Brow/HairStyle
    /// below) is now genuinely 32x28 - a MECHANICAL block-double of the 16x14 original, not a
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

            /// <summary>What sits ON the skull: the cap and the fringe, composed into the head.</summary>
            public readonly string[] Grid;

            /// <summary>
            /// The LENGTH - everything that falls past the skull, or rises off it. Its own sprite
            /// on <see cref="Gear.RigLayer.HairBack"/>, behind the whole body, at whatever size it
            /// needs; null for a style that fits inside the head grid.
            ///
            /// Authored in FINAL composed rows, not through <see cref="Expand"/>: it is not one of
            /// the four families that have to stay aligned by index, so putting it through the
            /// lower-face insert would only ever stretch a row of it for no reason.
            /// </summary>
            public readonly string[] Mane;

            /// <summary>
            /// Texel y of the mane's TOP edge, measured from the neck joint the head hangs off -
            /// the same frame <see cref="ManeHeadTop"/> is stated in. Above 16 is hair standing
            /// proud of the skull; below it is hair that only ever falls.
            /// </summary>
            public readonly int ManeTop;

            /// <summary>
            /// The same hair drawn at <see cref="MenuHairScale"/>x, for the character screen and
            /// the portrait - or null to fall through to <see cref="Grid"/> upscaled, which is
            /// what every style did before any of these existed.
            ///
            /// OPT-IN PER STYLE, exactly as <c>GearItem.MenuLayers</c> is per piece: one finer
            /// hairstyle does not oblige anyone to redraw sixteen others, and a style with none
            /// is not broken, only coarser.
            ///
            /// Authored in FINAL COMPOSED SPACE - 18 rows at 1x, so 36 here - and NOT through
            /// Expand. It is stamped over a head composed bare rather than composed into one, so
            /// there is no by-index alignment with the skull or the expression to preserve.
            /// </summary>
            public readonly string[] MenuGrid;

            /// <summary>The mane at <see cref="MenuHairScale"/>x; same opt-in, same fall-through.</summary>
            public readonly string[] MenuMane;

            public Style(string key, string name, string[] grid,
                         string[] mane = null, int maneTop = ManeHeadTop,
                         string[] menuGrid = null, string[] menuMane = null)
            {
                Key = key; Name = name; Grid = grid; Mane = mane; ManeTop = maneTop;
                MenuGrid = menuGrid; MenuMane = menuMane;
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
        /// How much finer menu-density hair is authored than the arena grid.
        ///
        /// ONE, not two - this used to be two, back when the arena grid was the coarser of the
        /// pair. Moving LayoutUnit from 37.5 to 75 block-doubled the arena grid itself (Grid and
        /// Mane on every Style below), and menuGrid/menuMane were deliberately left untouched -
        /// they are genuine hand-authored detail, not a mechanical scale-up, so there was nothing
        /// to double. The arena grid's own resolution caught up to what menu art already was,
        /// which is what makes this 1 now: the two are the same density, and only the STRUCTURE
        /// pass (FaceDetail.BuildMenuHair's stamp, ManeDetail's Clip/BuildHair) still treats them
        /// differently. See <see cref="DetailScale"/>'s own note for the matching half of this.
        /// </summary>
        public const int MenuHairScale = 1;

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
                "....111111............111111....",
                "....111111............111111....",
                "......cc................cc......",
                "......cc................cc......",
                "......cckk............kkcc......",
                "......cckk............kkcc......",
                "......CC................CC......",
                "......CC................CC......",
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
                "......44................44......",
                "......44................44......",
                "....444444............444444....",
                "....444444............444444....",
                "..4444444444........4444444444..",
                "..4444444444........4444444444..",
                "..4444..4444........4444..4444..",
                "..4444..4444........4444..4444..",
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
            new("dazzled", "Dazzled", new[]
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
                "..66........................66..",
                "..66........................66..",
                "......44................44......",
                "......44................44......",
                "....446644............446644....",
                "....446644............446644....",
                "......44................44......",
                "......44................44......",
                "....66....................66....",
                "....66....................66....",
                "................................",
                "................................",
            }),
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
                "....444444............444444....",
                "....444444............444444....",
                "....44..44............44..44....",
                "....44..44............44..44....",
                "....4444................4444....",
                "....4444................4444....",
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
        /// tuft) and 'K' for a RIM. Stubble is the exception and uses 'S' alone: it is a shadow on
        /// skin, not hair sitting on top of it, so it never takes a mass tone at all.
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
                "....SS....SS..SSSS..SS....SS....",
                "....SS....SS..SSSS..SS....SS....",
                "..SSSSSSSS..SSSSSSSS..SSSSSSSS..",
                "..SSSSSSSS..SSSSSSSS..SSSSSSSS..",
                "....SSSSSSSSSSSSSSSSSSSSSSSS....",
                "....SSSSSSSSSSSSSSSSSSSSSSSS....",
                "......SSSSSSSSSSSSSSSSSSSS......",
                "......SSSSSSSSSSSSSSSSSSSS......",
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
                "........BBBBBB....BBBBBB........",
                "........BBBBBB....BBBBBB........",
                "......BBBBBBBBBBBBBBBBBBBB......",
                "......BBBBBBBBBBBBBBBBBBBB......",
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
                "..BBBBBB................BBBBBB..",
                "..BBBBBB................BBBBBB..",
                "KKBBBBBBBBBB........BBBBBBBBBBKK",
                "KKBBBBBBBBBB........BBBBBBBBBBKK",
                "KKBBBBBBBBBB........BBBBBBBBBBKK",
                "KKBBBBBBBBBB........BBBBBBBBBBKK",
                "KKBBBBBBBBBB........BBBBBBBBBBKK",
                "KKBBBBBBBBBB........BBBBBBBBBBKK",
                "KKBBBBBBBB............BBBBBBBBKK",
                "KKBBBBBBBB............BBBBBBBBKK",
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
                "..BBBBBB................BBBBBB..",
                "..BBBBBB................BBBBBB..",
                "KKBBBBBBBBBBBB....BBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBB....BBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "..KKDDDDDDDDDDDDDDDDDDDDDDDDKK..",
                "..KKDDDDDDDDDDDDDDDDDDDDDDDDKK..",
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
        /// Most live on row 7 alone; Arched, Angry, Worried and Thick also reach row 6. Row 6 and
        /// above is under the hair on most of the original caps - the same rule the expressions
        /// are held to.
        /// </summary>
        public static readonly Style[] Brows =
        {
            new("natural", "Natural", new[]
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
                "....nnnn................nnnn....",
                "....nnnn................nnnn....",
                "......nnnnnn........nnnnnn......",
                "......nnnnnn........nnnnnn......",
                "................................",
                "................................",
                "................................",
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
                "....nnnnnnnn........nnnnnnnn....",
                "....nnnnnnnn........nnnnnnnn....",
                "....nnnnnnnn........nnnnnnnn....",
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
                "......nnnn............nnnn......",
                "......nnnn............nnnn......",
                "................................",
                "................................",
                "................................",
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

        public static Style Brow(string key)
        {
            foreach (var b in Brows) if (b.Key == key) return b;
            return Brows[0];
        }

        public static readonly Style[] HairStyles =
        {
            // ---- anime locks ----
            //
            // Shaped differently from the caps below them, and the difference is the whole point:
            // a cap is a solid block filling the top of the skull with a straight bottom edge, and
            // it reads as a helmet however it is shaded. What makes the reference's hair read as
            // hair is its SILHOUETTE - locks that leave the head, and a fringe that hangs over the
            // forehead at uneven lengths with skin showing between.
            //
            // That has to come from the art. No amount of shading rescues a rounded rectangle,
            // which is what the procedural strand pass kept trying to do and why it came out first
            // as a striped hat, then a barcode, then a shell - it was scoring grooves into a
            // helmet.
            //
            // Two rules, both learned by getting them wrong:
            //
            //   GAPS ARE TRANSPARENT, never the dark hair tone. Drawing partings in 'K' put dark
            //   specks inside a white mass - confetti, not hair. A gap only reads where it shows
            //   something else: the forehead under a fringe, or the background past a lock.
            //
            //   NOTHING NARROWER THAN TWO TEXELS. The silhouette pass rounds off single-texel
            //   corners, so a one-texel spike is shaved away before it is ever drawn.
            //
            // Sixteen rows, not fourteen: the top two are above the skull - see HairRowsAdded.
            //
            // THE TWO BELOW ARE THE FIRST STYLES BUILT AROUND A MANE, and the mane is why they can
            // look like the references they were drawn from at all. Two rows above the skull is a
            // cap with a bumpy edge; a mane can be any size, so the head grid is left carrying only
            // the cap and the fringe and every lock that leaves the skull is drawn at its real
            // length on its own layer.
            //
            // BOTH WERE BUILT FROM DIRECTIONAL LOCKS, not from a serrated dome, and that was the
            // pass that made them work. Four earlier attempts cut notches into a rounded mass and
            // every one came out as a cloud or a jester's crown, because every shape in them was
            // VERTICAL. A lock has a direction; hair reads when the locks sweep.

            // Reference: a white-haired shounen lead. A wide mass of locks radiating up and OUT
            // past the head's own width, a single thin ahoge standing above all of them, and a
            // side lock in front of each ear.
            new("shock", "Shock", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "......LLLLLLLLLLLLLLLLLLLL......",
                "......LLLLLLLLLLLLLLLLLLLL......",
                "..LLLLLLLLLLLLLLLLLLLLLLLLLLLL..",
                "..LLLLLLLLLLLLLLLLLLLLLLLLLLLL..",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLDDLLLLLLLLLLLLLLLLLLLLLLLLDDLL",
                "LLDDLLLLLLLLLLLLLLLLLLLLLLLLDDLL",
                "LLDDLLLL..LLLLLLLLLLLL..LLLLDDLL",
                "LLDDLLLL..LLLLLLLLLLLL..LLLLDDLL",
                "KKDDLL......LLLLLLLL......LLDDKK",
                "KKDDLL......LLLLLLLL......LLDDKK",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }, new[]
            {
                "........KKKK......KKLLKK....KKLLLLKK............",
                "........KKKK......KKLLKK....KKLLLLKK............",
                "......KKLLLLKK....KKLLLLKKKKLLLLLLLLKK..........",
                "......KKLLLLKK....KKLLLLKKKKLLLLLLLLKK..........",
                "......KKLLLLKK....KKLLLLKKKKLLLLLLLLKK..........",
                "......KKLLLLKK....KKLLLLKKKKLLLLLLLLKK..........",
                "......KKLLLLLLKK..KKLLLLLLKKLLLLLLLLKK..........",
                "......KKLLLLLLKK..KKLLLLLLKKLLLLLLLLKK..........",
                "........KKLLLLLLKKKKLLLLLLLLLLLLLLLLKK..KKKKKK..",
                "........KKLLLLLLKKKKLLLLLLLLLLLLLLLLKK..KKKKKK..",
                "....KK..KKLLLLLLLLKKLLLLLLLLLLLLLLLLKKKKLLLLLLKK",
                "....KK..KKLLLLLLLLKKLLLLLLLLLLLLLLLLKKKKLLLLLLKK",
                "..KKLLKKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "..KKLLKKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "..KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKKKKKKKKKKKKKKKKKKLLLLLLLLLLKK..",
                "....KKLLLLLLLLLLKKKKKKKKKKKKKKKKKKLLLLLLLLLLKK..",
                "....KKLLLLLLLLKK..................KKLLLLLLLLKK..",
                "....KKLLLLLLLLKK..................KKLLLLLLLLKK..",
                "....KKLLLLLLLLKK....................KKLLLLLLKK..",
                "....KKLLLLLLLLKK....................KKLLLLLLKK..",
                "..KKLLLLLLLLKK......................KKLLLLLLKK..",
                "..KKLLLLLLLLKK......................KKLLLLLLKK..",
                "..KKLLLLLLKK..........................KKKKKK....",
                "..KKLLLLLLKK..........................KKKKKK....",
                "....KKKKKK......................................",
                "....KKKKKK......................................",
            }, maneTop: 44,
            // MENU DENSITY, 2x. Everything here is detail the arena grid has no cells for: tips
            // that taper to one texel instead of two, thin locks threaded between the fat ones,
            // and 1-cell partings - half an arena texel - which read as creases rather than as
            // the gouges a parting that wide becomes at 1x.
            menuGrid: new[]
            {
                "..........LLLLLLLLLLLL..........",
                ".........LLLLLLLLLLLLLL.........",
                "........LLLLLLLLLLLLLLLL........",
                ".......LLLLLLLLLLLLLLLLLL.......",
                "......LLLLLLLLLLLLLLLLLLLL......",
                ".....LLLLLLLLLLLLLLLLLLLLLL.....",
                "...LLLLLLLLLLLLLLLLLLLLLLLLLL...",
                "..LLLLLLLLLLLLLLLLLLLLLLLLLLLL..",
                ".LLLLLLLLLLLLLLLLLLLLLLLLLLLLLL.",
                "LLLLLLLLLDLLLLLLLLLLLLLDLLLLLLLL",
                "LLLLLLLLLDLLLLLLLLLLLLLDLLLLLLLL",
                "LLLLLLLLLDLLLLLLLLLLLLLDLLLLLLLL",
                "LLLLLLLLLDLLLLLLLLLLLLLDLLLLLLLL",
                "LLLLLLLLLDLLLLLLLLLLLLLDLLLLLLLL",
                "LLLLLLLLLDLLLLLLLLLLLLLDLLLLLLLL",
                "LLLLLLLLLDLLLLLLLLLLLLLDLLLLLLLL",
                "LLLLLLLLLDLLLLLLLLLLLLLDLLLLLLLL",
                "LLLLLLLKLLLLLLLLLLLLLLLKLLLLLLLL",
                "LLLLLLK.KKLLLLLKKLLLLLK.KLLLLLLL",
                "KLLLLK....KLLLK..KLLLK...KLLLLLK",
                ".KLLK......KKK....KLK.....KLLLK.",
                "..KK...............K.......KKK..",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }, menuMane: new[]
            {
                "....................KK.......KLLK...............",
                "...................KLLK.....KLLLKKK.............",
                "........KK.........KLLK.....KLLKKLLK............",
                ".......KLLK.......KLLLLK..KKLLLKLLLK............",
                ".......KLLLK.......KLLLK.KLLLLLKLLLK............",
                ".......KLLLLK.KK...KLLLLKKLLLLLLLLLK............",
                ".......KLLLLLKLLK..KLLLLKKLLLLLLLLLK.KK.........",
                "........KLLLLLLLK..KLLLLLKLLLLLLLLLKKLLK........",
                "........KLLLLLLLLK.KLLLLLKLLLLLLLLLKLLLK........",
                ".........KLLLLLLLK.KLLLLLKLLLLLLLLLKLLK..KKK....",
                ".........KLLLLLLLLKKLLLLLLLLLLLLLLKLLLKKKLLLK...",
                "........KKKLLLLLLLLKLLLLLLLLLLLLLLLLLKKLLLLLK...",
                "...KKKKKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLK...",
                "..KLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLK....",
                "..KLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLK.....",
                "...KLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLK.....",
                "....KLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLK......",
                ".....KLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLK......",
                "......KLLLLLLLLLLLLDLLLLLLLLLLLLLDLLLLLLK.......",
                ".......KLLLLLLLLLLLDLLLLLLLLLLLLLDLLLLLLK.......",
                ".......KLLLLLLLLLLLDLLLLLLLLLLLLLDLLLLLLK.......",
                ".......KLLLLLLLLLLLDLLLLLLLLLLLLLDLLLLLLK.......",
                ".......KLLLLLLLLLLLDLLLLLLLLLLLLLDLLLLLLLK......",
                ".......KLLLLLLLLLLLDLLLLLLLLLLLLLDLLLLLLLK......",
                ".......KLLLLLLLLLLLDLLLLLLLLLLLLLDLLLLLLLK......",
                ".......KLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLK......",
                "......KLLLLLLLLKKKKKKKKKKKKKKKKKKKLLLLLLLLK.....",
                "......KLLLLLLLK...................KLLLLLLLK.....",
                "......KLLLLLLK.....................KLLLLLLK.....",
                ".....KLLLLLLLK......................KLLLLLK.....",
                ".....KLLLLLKK........................KLLLLLK....",
                ".....KLLLLLK..........................KLLLLK....",
                "....KLLLLLK...........................KLLLLK....",
                "....KLLLLK.............................KLKK.....",
                "....KLLLLK..............................K.......",
                ".....KKLK.......................................",
                ".......K........................................",
                "................................................",
            }),

            // Reference: a red-haired knight. A bushy high tail off the crown, with the length
            // falling past the shoulders either side - the style that made the case for the mane in
            // the first place, since none of it fits inside a head sprite.
            //
            // THE TAIL IS A SPRAY OF LOCKS, NOT ONE STROKE, and that is the whole of it. Built as a
            // single tapering sweep it read as SOFT SERVE - a smooth cone balanced on a head - and
            // no amount of tuning its width or angle fixed that, because the fault was that it had
            // no internal edges at all. What makes the reference bushy is that its tail is a bundle
            // of separate pointed strands that splay apart, with background showing between the
            // tips. Six locks off one gather point now, and three rules hold it together:
            //
            //   THEY SEPARATE ABOVE THE HEAD LINE. The gather is hidden behind the head, so the
            //   locks have to be distinct by the time they clear it or the notches have nowhere to
            //   cut and the whole thing closes back into a blob.
            //
            //   NO TWO TIPS END TOGETHER. Even lengths read as a hand, which two passes did.
            //
            //   ONE LOCK CROSSES ANOTHER, or the remaining five are an evenly spaced fan - regular
            //   spacing is what makes a spray read as a mechanism rather than as hair.
            //
            // An even earlier version curved the tail back down beside the crown and the two ends
            // met, closing into a ring with a hole through it: a handle, not hair.
            //
            // The falls were pulled IN toward the body at the same time. Bulging at the shoulder
            // they widened the character's whole silhouette; the reference's front locks hug the
            // face and come down narrow.
            new("hightail", "High Tail", new[]
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "......LLLLLLLLLLLLLLLLLLLL......",
                "......LLLLLLLLLLLLLLLLLLLL......",
                "..LLLLLLLLLLLLLLLLLLLLLLLLLLLL..",
                "..LLLLLLLLLLLLLLLLLLLLLLLLLLLL..",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLDDLLLLLLLLLLLLLLLLLLLLLLLLDDLL",
                "LLDDLLLLLLLLLLLLLLLLLLLLLLLLDDLL",
                "LLLLLLLLLLLLLLLL....LLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLL....LLLLLLLLLLLL",
                "LLLLLLLLLLLL..........LLLLLLLLLL",
                "LLLLLLLLLLLL..........LLLLLLLLLL",
                "LLLLLL..................LLLLLLLL",
                "LLLLLL..................LLLLLLLL",
                "LLLL......................LLLLLL",
                "LLLL......................LLLLLL",
                "LLLL........................LLLL",
                "LLLL........................LLLL",
                "DDLL........................LLDD",
                "DDLL........................LLDD",
                "DDLL..........................DD",
                "DDLL..........................DD",
                "DD..............................",
                "DD..............................",
            }, new[]
            {
                "........................KKLLLLKK........................",
                "........................KKLLLLKK........................",
                "........................KKLLLLKK..KKKK....KKKK..........",
                "........................KKLLLLKK..KKKK....KKKK..........",
                "........................KKLLLLKKKKLLLLKKKKLLLLKK........",
                "........................KKLLLLKKKKLLLLKKKKLLLLKK........",
                "........................KKLLLLKKKKLLLLKKKKLLLLKK........",
                "........................KKLLLLKKKKLLLLKKKKLLLLKK........",
                "....................KK..KKLLLLLLKKLLLLKKLLLLLLKK........",
                "....................KK..KKLLLLLLKKLLLLKKLLLLLLKK........",
                "..................KKLLKKKKLLLLLLLLLLLLLLLLLLKK..........",
                "..................KKLLKKKKLLLLLLLLLLLLLLLLLLKK..........",
                "................KKLLLLLLKKLLLLLLLLLLLLLLLLKK....KKKK....",
                "................KKLLLLLLKKLLLLLLLLLLLLLLLLKK....KKKK....",
                "..................KKLLLLLLLLLLLLLLLLLLLLLLKKKKKKLLLLKK..",
                "..................KKLLLLLLLLLLLLLLLLLLLLLLKKKKKKLLLLKK..",
                "..................KKKKLLLLLLLLLLLLLLLLLLKKLLLLLLLLLLLLKK",
                "..................KKKKLLLLLLLLLLLLLLLLLLKKLLLLLLLLLLLLKK",
                "................KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "................KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "................KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKKKK....",
                "................KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKKKK....",
                "................KKLLLLLLLLLLLLLLLLLLLLLLLLLLKKKK........",
                "................KKLLLLLLLLLLLLLLLLLLLLLLLLLLKKKK........",
                "................KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "................KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "................KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "................KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "................KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "................KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "................KKLLLLLLLLLLLLLLLLLLLLKKKKKKKK..........",
                "................KKLLLLLLLLLLLLLLLLLLLLKKKKKKKK..........",
                "..............KKLLLLLLLLKKKKKKKKKKKKKKLLKK..............",
                "..............KKLLLLLLLLKKKKKKKKKKKKKKLLKK..............",
                "..............KKLLLLLLLLKK......KKLLLLLLLLKK............",
                "..............KKLLLLLLLLKK......KKLLLLLLLLKK............",
                "..............KKLLLLLLLLKK......KKLLLLLLLLKK............",
                "..............KKLLLLLLLLKK......KKLLLLLLLLKK............",
                "............KKLLLLLLLLLLKK......KKLLLLLLLLKK............",
                "............KKLLLLLLLLLLKK......KKLLLLLLLLKK............",
                "............KKLLLLLLLLKK........KKLLLLLLLLLLKK..........",
                "............KKLLLLLLLLKK........KKLLLLLLLLLLKK..........",
                "............KKLLLLLLLLKK..........KKLLLLLLLLKK..........",
                "............KKLLLLLLLLKK..........KKLLLLLLLLKK..........",
                "..........KKLLLLLLLLLLKK..........KKLLLLLLLLLLKK........",
                "..........KKLLLLLLLLLLKK..........KKLLLLLLLLLLKK........",
                "..........KKLLLLLLLLKK..............KKLLLLLLLLKK........",
                "..........KKLLLLLLLLKK..............KKLLLLLLLLKK........",
                "..........KKLLLLLLLLKK..............KKLLLLLLLLKK........",
                "..........KKLLLLLLLLKK..............KKLLLLLLLLKK........",
                "..........KKLLLLLLLLKK..............KKLLLLLLLLKK........",
                "..........KKLLLLLLLLKK..............KKLLLLLLLLKK........",
                "..........KKLLLLLLKK..................KKLLLLLLKK........",
                "..........KKLLLLLLKK..................KKLLLLLLKK........",
                "..........KKLLLLLLKK..................KKLLLLLLKK........",
                "..........KKLLLLLLKK..................KKLLLLLLKK........",
                "..........KKLLLLLLKK..................KKLLLLLLKK........",
                "..........KKLLLLLLKK..................KKLLLLLLKK........",
                "..........KKLLLLLLKK..................KKLLLLLLKK........",
                "..........KKLLLLLLKK..................KKLLLLLLKK........",
                "..........KKLLLLKK....................KKLLLLLLKK........",
                "..........KKLLLLKK....................KKLLLLLLKK........",
                "..........KKLLLLKK....................KKLLLLLLKK........",
                "..........KKLLLLKK....................KKLLLLLLKK........",
                "..........KKLLLLKK......................KKKKKK..........",
                "..........KKLLLLKK......................KKKKKK..........",
                "............KKKK........................................",
                "............KKKK........................................",
            }, maneTop: 48,
            menuGrid: new[]
            {
                "..........LLLLLLLLLLLL..........",
                ".........LLLLLLLLLLLLLL.........",
                "........LLLLLLLLLLLLLLLL........",
                ".......LLLLLLLLLLLLLLLLLL.......",
                "......LLLLLLLLLLLLLLLLLLLL......",
                ".....LLLLLLLLLLLLLLLLLLLLLL.....",
                "...LLLLLLLLLLLLLLLLLLLLLLLLLL...",
                "..LLLLLLLLLLLLLLLLLLLLLLLLLLLL..",
                ".LLLLLLLLLLLLLLLLLLLLLLLLLLLLLL.",
                "LLLLLLLLLLLLLDLLLLLLLLLLLLDLLLLL",
                "LLLLLLLLLLLLLDLLLLLLLLLLLLDLLLLL",
                "LLLLLLLLLLLLLDLLLLLLLLLLLLDLLLLL",
                "LLLLLLLLLLLLLDLLLLLLLLLLLLDLLLLL",
                "LLLLLLLLLLLLLDLLLLLLLLLLLLDLLLLL",
                "LLLLLLLLLLLLLDLLLLLLLLLLLLDLLLLL",
                "LLLLLLLLLLLLLDLLLLLLLLLLLLDLLLLL",
                "LLLLLLLLLLLLLDLLLLLLKKLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLKKK..KKKLLLLLLL",
                "LLLLLLLLLLLLLLKKK........KKLLLLL",
                "LLLLLLLLLLLKKK.............KLLLL",
                "LLLLLLLLKKK................KLLLL",
                "LLLLLLKK...................KLLLL",
                "LLLLLLK....................KLLLL",
                "LLLLLLK....................KLLLL",
                "LLLLLLK....................KLLLL",
                "LLLLLLK....................KLLLL",
                "LLLLLLK....................KLLLL",
                "LLLLLK......................KLLL",
                "LLLLLK......................KLLL",
                "LLLLLK......................KLLL",
                "LLLLLK......................KLLL",
                "KLLLK.......................KLLL",
                ".KKK.........................KKK",
                "................................",
                "................................",
                "................................",
            }, menuMane: new[]
            {
                ".........................KLLK..........KK...............",
                ".......................KKKLLK.........KLLK..............",
                "......................KLLKLLLK........KLLK..............",
                "......................KLLKLLLK....KK..KLLK..............",
                "......................KLLKLLLK...KLLKKLLLK.KK...........",
                "...................KK.KLLLLLLK...KLLKKLLK.KLLK..........",
                "..................KLLK.KLLLLLLK..KLLLLLLKKLLLK..........",
                "..................KLLLKKLLLLLLK.KLLLLLLKKLLLLK..........",
                "..................KLLLLKLLLLLLK.KLLLKLLKLLLLK..KK.......",
                "..................KLLLLKKLLLLLLKLLLLLLLLLLLLK.KLLK......",
                "...................KLLLLLLLLLLLKLLLLLLLLLLLK.KLLLK......",
                "....................KLLLLLLLLLLLLLLLLLLLLLLKKLLLK.......",
                "....................KLLLLLLLLLLLLLLLLLLLLLKLLLLK........",
                ".....................KLLLLLLLLLLLLLLLLLLLKKLLLK.........",
                ".....................KLLLLLLLLLLLLLLLLLLLLLLLK..KKKK....",
                ".....................KLLLLLLLLLLLLLLLLLLLLLLKKKKLLLLK...",
                "......................KLLLLLLLLLLLLLLLLLLLLLLLLLLLLLK...",
                "................KKKKKKKLLLLLLLLLLLLLLLLLLLLLLLLLLLLK....",
                "...............KLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK.....",
                "...............KLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLK.......",
                "...............KLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "...............KLLLLLLLLLLLLLLLLLLLLLLLLLLLLLK..........",
                "...............KLLLLLLLLLLLLLLLLLDLLLLLLLLLKK...........",
                "...............KLLLLLLLLLLLLLLLLLDLLLLLLLLK.............",
                "...............KLLLLLLLLLLLLLLLLLDLLLLLLLLLKK...........",
                "...............KLLLLLLLLLLLLLLLLLDLLLLLLLLLLLK..........",
                "...............KLLLLLLLLLLLLLLLLLDLLLLLLLLLLLLK.........",
                "...............KLLLLLLLLLLLLLLLLLDLLLLLLLLLLLK..........",
                "...............KLLLLLLLLLLLLLLLLLDLLLLLLLLLKK...........",
                "...............KLLLLLLLLLLLLLLLLLDLLLLLLKKK.............",
                "...............KLLLLLLLLLLLLLLLLLDLLLLLLK...............",
                "...............KLLLLLLLLLLLLLLLLLLLLLLLLK...............",
                "................KLLLLLKKKKKKKKKKKKKKKKKK................",
                "...............KLLLLLLLLK..........KKLLLK...............",
                "...............KLLLLLLLLK........KKLLLLLK...............",
                "...............KLLLLLLLKKK......KLLLLLLLLK..............",
                "..............KLLLLLLLLLLLK.....KLLLLLLLLK..............",
                "..............KLLLLLLLLLLLK.....KKLLLLLLLK..............",
                "..............KLLLLLLLLLLLK....KLLLLLLLLLLK.............",
                ".............KLLLLLLLLLLLLK....KLLLLLLLLLLK.............",
                ".............KLLLDLLLLLLLK.....KLLLLLLLLLLLK............",
                ".............KLLLDLLLLLLLK.....KLLLLLLLLLLLK............",
                "............KLLLLDLLLLLLLK......KLLLLLLLLDLK............",
                "............KLLLLDLLLLLLLK......KLLLLLLLLDLLK...........",
                "............KLLLLDLLLLLLLK......KLLLLLLLLDLLK...........",
                "...........KLLLLLDLLLLLLK.......KLLLLLLLLDLLK...........",
                "...........KLLLLLDLLLLLLK.......KLLLLLLLLDLLLK..........",
                "...........KLLLLLDLLLLLK........KLLLLLLLLDLLLK..........",
                "..........KLLLLLLDLLLLLK........KLLLLLLLLDLLLLK.........",
                "..........KLLLLLLDLLLLLK.........KLLLLLLLDLLLLK.........",
                "..........KLLLLLLDKLLLLK.........KLLLLKLLDLLLLK.........",
                "..........KLLLLLLDLLLLLK.........KLLLLLLLDLLLK..........",
                "..........KLLLLLLKLLLLK...........KLLLLKLDLLLK..........",
                "..........KLLLLLLKLLLLK...........KLLLLKLDLLLK..........",
                "..........KLLLLLKKLLLLK...........KLLLLKLDLLLK..........",
                "..........KLLLLLKKLLLLK...........KLLLLKLDLLLK..........",
                "..........KLLLLLKKLLLLK...........KLLLLKLDLLLK..........",
                "..........KLLLLLK.KKKK............KLLLLKLLLLLK..........",
                "..........KLLLLK..................KLLLLKLLLLLK..........",
                "..........KLLLLK...................KKKK.KLLLK...........",
                "...........KLLLK........................KLLLK...........",
                "..........KLLLK.........................KLLLK...........",
                "..........KLLLK..........................KLK............",
                "...........KLLK...........................K.............",
                "...........KLLK.........................................",
                "............KK..........................................",
                "........................................................",
                "........................................................",
            }),

            new("spiky", "Spiky", new[]
            {
                "......LLLL......LLLL....LLLL....",
                "......LLLL......LLLL....LLLL....",
                "....KKLLLLKK..KKLLLLKKKKLLLLKK..",
                "....KKLLLLKK..KKLLLLKKKKLLLLKK..",
                "....KKLLLLKKKKKKLLLLKKKKLLLLKK..",
                "....KKLLLLKKKKKKLLLLKKKKLLLLKK..",
                "..KKKKLLLLLLLLLLLLLLLLLLLLLLKK..",
                "..KKKKLLLLLLLLLLLLLLLLLLLLLLKK..",
                "KKKKLLLLLLLLLLLLLLLLLLLLLLLLKKKK",
                "KKKKLLLLLLLLLLLLLLLLLLLLLLLLKKKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKLL........LLLLLLLL........DDKK",
                "KKLL........LLLLLLLL........DDKK",
                "DDLL..........LLLL..........DD..",
                "DDLL..........LLLL..........DD..",
                "DD..........................DD..",
                "DD..........................DD..",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("bangs", "Bangs", new[]
            {
                "................................",
                "................................",
                "........LLLLLLLLLLLLLLLL........",
                "........LLLLLLLLLLLLLLLL........",
                "......KKLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLKK......",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKLL........LLLLLLLL........LLKK",
                "KKLL........LLLLLLLL........LLKK",
                "DDLL..........LLLL..........LLDD",
                "DDLL..........LLLL..........LLDD",
                "DDLL..........LLLL..........LLDD",
                "DDLL..........LLLL..........LLDD",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("messy", "Messy", new[]
            {
                "....LLLL....LLLLLL......LLLL....",
                "....LLLL....LLLLLL......LLLL....",
                "..KKLLLLKKKKLLLLLLKK..KKLLLLKK..",
                "..KKLLLLKKKKLLLLLLKK..KKLLLLKK..",
                "....KKLLLLKKLLLLLLKKKKLLLLLLKK..",
                "....KKLLLLKKLLLLLLKKKKLLLLLLKK..",
                "..KKKKLLLLLLLLLLLLLLLLLLLLLLKK..",
                "..KKKKLLLLLLLLLLLLLLLLLLLLLLKK..",
                "KKKKLLLLLLLLLLLLLLLLLLLLLLLLKKKK",
                "KKKKLLLLLLLLLLLLLLLLLLLLLLLLKKKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKLL........LLLLLLLL........LLKK",
                "KKLL........LLLLLLLL........LLKK",
                "DDLL..........LLLL..........LLDD",
                "DDLL..........LLLL..........LLDD",
                "DD............LL............DD..",
                "DD............LL............DD..",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            // Short twists, from a reference PFP: chunky locs radiating up and OUTWARD off the
            // crown at uneven heights, over a clean high hairline with the forehead fully clear.
            //
            // FOUR locs on a 4-texel pitch, and the SHADING is what makes them locs rather than
            // another spiky cap. Every other style in this file draws hair in three tones - K, L
            // and D - so a silhouette is the only thing telling any two of them apart, and at two
            // free rows above the skull there is not enough silhouette to go round: authored that
            // way, this came out near-identical to `spiky`.
            //
            // So each lock is a rope with its own light, on a 4-texel pitch. That reads as strand
            // rather than as a mass with bumps, and being TONAL it survives every hair colour,
            // which a silhouette-only style cannot.
            //
            // THE STRANDS ARE CUT IN LIGHT, NOT IN SHADOW, and that is the whole lesson of this
            // one. The obvious way to draw a rope is a dark groove between locks, and it fails on
            // exactly the hair this was drawn for. Ramp.Dark is the base 30% of the way to black
            // and Deep is 54%, so on BLACK hair both land within a few hundredths of the room's
            // own background: drawn with dark grooves the head came out as a picket fence, five
            // lit bars floating with holes between them, because the groove was not reading as a
            // groove at all - it was reading as absence.
            //
            // So the mass carries NO tone below L, and the locks are defined by an H crest riding
            // on each one. The silhouette stays solid on every hair colour in the list, the
            // strand definition comes from highlights - which is how a rope is lit anyway - and
            // the value still averages to the flat L every other style paints, so black hair
            // stays black hair. Only the last row, already sitting on the hairline, may go to D.
            //
            // THE CRESTS KINK, one texel, every two rows. Run dead straight for the full height
            // they read as a fluted column rather than as rope - the corduroy this project's
            // detail pass already warns about, arrived at from the other direction. A real twist's
            // highlight wraps, and two rows on, two rows off is as much of that helix as six rows
            // at a 4-texel pitch can carry.
            //
            // THE GAPS BETWEEN THEM ARE TRANSPARENT, and they live ONLY in the two rows above the
            // skull. Both halves of that were learned by rendering it. Separating the locs with
            // their own K outlines instead is the confetti mistake this file's header warns about
            // wearing a different hat - it came out as a woven checkerboard, because K against K
            // against L at this density is texture, not silhouette. And carrying the gaps down
            // into rows 2-3, which sit ON the skull, punched holes: what showed through was SKIN,
            // so the locks read as bites taken out of the head. A gap only reads where the thing
            // behind it is the background.
            // Short twists, from a reference PFP: chunky locs at uneven heights off the crown,
            // over a clean high hairline with the forehead clear and sideburns in front of the
            // ears - the sideburns being the part none of the other short styles have.
            //
            // THE GAPS BETWEEN LOCS ARE TRANSPARENT, and they live ONLY in the two rows above the
            // skull. Both halves of that were learned by rendering it. Separating the locs with
            // their own K outlines instead is the confetti mistake this file's header warns about
            // wearing a different hat - it came out as a woven checkerboard, because K against K
            // against L at this density is texture, not silhouette. And carrying the gaps down
            // into the rows that sit ON the skull punched holes: what showed through was SKIN, so
            // the locks read as bites taken out of the head. A gap only reads where the thing
            // behind it is the background.
            //
            // Shading was tried here to separate it further from `spiky` and moved to `braids`
            // instead, where flat rows are the point - see that entry for what tonal strands cost
            // and why the crest has to be cut in light.
            new("twists", "Twists", new[]
            {
                "......LLLL....LLLL......LLLL....",
                "......LLLL....LLLL......LLLL....",
                "..LLLLLL..LLLLLL..LLLLLL..LLLL..",
                "..LLLLLL..LLLLLL..LLLLLL..LLLL..",
                "..KKLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKDD........................DDKK",
                "KKDD........................DDKK",
                "KKDD........................DDKK",
                "KKDD........................DDKK",
                "KKLL........................LLKK",
                "KKLL........................LLKK",
                "KKDD........................DDKK",
                "KKDD........................DDKK",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }),
            new("wolf", "Wolf Cut", new[]
            {
                "......LLLL....LLLLLLLL..LLLL....",
                "......LLLL....LLLLLLLL..LLLL....",
                "....KKLLLLKKKKLLLLLLLLKKLLLLKK..",
                "....KKLLLLKKKKLLLLLLLLKKLLLLKK..",
                "..KKKKLLLLLLLLLLLLLLLLLLLLLLKK..",
                "..KKKKLLLLLLLLLLLLLLLLLLLLLLKK..",
                "KKKKLLLLLLLLLLLLLLLLLLLLLLLLKKKK",
                "KKKKLLLLLLLLLLLLLLLLLLLLLLLLKKKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKLL........LLLLLLLL........LLKK",
                "KKLL........LLLLLLLL........LLKK",
                "DDLLLL........LLLL........LLLLDD",
                "DDLLLL........LLLL........LLLLDD",
                "DDLLLL....................LLLLDD",
                "DDLLLL....................LLLLDD",
                "DDLL........................LLDD",
                "DDLL........................LLDD",
                "DD............................DD",
                "DD............................DD",
                "................................",
                "................................",
                "................................",
                "................................",
            }, new[]
            {
                "......KKKKKKKKKKKKKKKKKKKKKKKKKKKK......",
                "......KKKKKKKKKKKKKKKKKKKKKKKKKKKK......",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLKKLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLKKLLLLLLLLKK....",
                "..KKLLLLLLLLKKLLLLLLLLLLKKKKLLLLLLLLKK..",
                "..KKLLLLLLLLKKLLLLLLLLLLKKKKLLLLLLLLKK..",
                "..KKLLLLLLLLKKLLLLLLLLLLKK..KKLLLLLLKK..",
                "..KKLLLLLLLLKKLLLLLLLLLLKK..KKLLLLLLKK..",
                "..KKLLLLLLKK..KKLLLLLLKK......KKKKKK....",
                "..KKLLLLLLKK..KKLLLLLLKK......KKKKKK....",
                "....KKKKKK....KKLLLLLLKK................",
                "....KKKKKK....KKLLLLLLKK................",
                "..............KKLLLLLLKK................",
                "..............KKLLLLLLKK................",
                "................KKKKKK..................",
                "................KKKKKK..................",
            }, maneTop: 22),
            new("twintail", "Twin Tails", new[]
            {
                "................................",
                "................................",
                "........LLLLLLLLLLLLLLLL........",
                "........LLLLLLLLLLLLLLLL........",
                "......KKLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLKK......",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "LLLLKKLLLLLLLLLLLLLLLLLLLLKKLLLL",
                "LLLLKKLLLLLLLLLLLLLLLLLLLLKKLLLL",
                "LLLLKKLLLLLLLLLLLLLLLLLLLLKKLLLL",
                "LLLLKKLLLLLLLLLLLLLLLLLLLLKKLLLL",
                "LLLLKKLLLLLLLLLLLLLLLLLLLLKKLLLL",
                "LLLLKKLLLLLLLLLLLLLLLLLLLLKKLLLL",
                "LLLLDDLLLLLLLLLLLLLLLLLLLLDDLLLL",
                "LLLLDDLLLLLLLLLLLLLLLLLLLLDDLLLL",
                "LLLLKK......LLLLLLLL......KKLLLL",
                "LLLLKK......LLLLLLLL......KKLLLL",
                "LLLLDD........LLLL........DDLLLL",
                "LLLLDD........LLLL........DDLLLL",
                "LLLLLL....................LLLLLL",
                "LLLLLL....................LLLLLL",
                "DDLLDD....................DDLLDD",
                "DDLLDD....................DDLLDD",
                "DDDD........................DDDD",
                "DDDD........................DDDD",
                "................................",
                "................................",
                "................................",
                "................................",
            }, new[]
            {
                "............KKKKKKKKKKKKKKKKKKKKKKKKKKKK............",
                "............KKKKKKKKKKKKKKKKKKKKKKKKKKKK............",
                "..........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK..........",
                "..........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK..........",
                "........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "..KKLLLLLLLLLLLLKKKKKKKKKKKKKKKKKKKKLLLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLLLKKKKKKKKKKKKKKKKKKKKLLLLLLLLLLLLKK..",
                "KKLLLLLLLLLLLLLLKK................KKLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLKK................KKLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLKK....................KKLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLKK....................KKLLLLLLLLLLLLKK",
                "..KKLLLLLLLLKK........................KKLLLLLLLLLLKK",
                "..KKLLLLLLLLKK........................KKLLLLLLLLLLKK",
                "..KKLLLLLLKK..........................KKLLLLLLLLLLKK",
                "..KKLLLLLLKK..........................KKLLLLLLLLLLKK",
                "..KKLLLLLLKK..........................KKLLLLLLLLLLKK",
                "..KKLLLLLLKK..........................KKLLLLLLLLLLKK",
                "..KKLLLLLLKK............................KKLLLLLLKK..",
                "..KKLLLLLLKK............................KKLLLLLLKK..",
                "..KKLLLLLLKK............................KKLLLLLLKK..",
                "..KKLLLLLLKK............................KKLLLLLLKK..",
                "..KKLLLLLLKK............................KKLLLLLLKK..",
                "..KKLLLLLLKK............................KKLLLLLLKK..",
                "..KKLLLLLLKK............................KKLLLLLLKK..",
                "..KKLLLLLLKK............................KKLLLLLLKK..",
                "..KKLLLLLLKK..............................KKLLKK....",
                "..KKLLLLLLKK..............................KKLLKK....",
                "..KKLLLLLLKK..............................KKLLKK....",
                "..KKLLLLLLKK..............................KKLLKK....",
                "....KKKKKK..................................KK......",
                "....KKKKKK..................................KK......",
            }, maneTop: 22),
            new("hime", "Hime", new[]
            {
                "................................",
                "................................",
                "........LLLLLLLLLLLLLLLL........",
                "........LLLLLLLLLLLLLLLL........",
                "......KKLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLKK......",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKLLLLKKKKKKKKKKKKKKKKKKKKLLLLKK",
                "KKLLLLKKKKKKKKKKKKKKKKKKKKLLLLKK",
                "DDLLLL....................LLLLDD",
                "DDLLLL....................LLLLDD",
                "DDLLLL....................LLLLDD",
                "DDLLLL....................LLLLDD",
                "DDLLLL....................LLLLDD",
                "DDLLLL....................LLLLDD",
                "DDLL........................LLDD",
                "DDLL........................LLDD",
                "................................",
                "................................",
                "................................",
                "................................",
            }, new[]
            {
                "............KKKKKKKKKKKKKKKKKKKK............",
                "............KKKKKKKKKKKKKKKKKKKK............",
                "..........KKLLLLLLLLLLLLLLLLLLLLKK..........",
                "..........KKLLLLLLLLLLLLLLLLLLLLKK..........",
                "..........KKLLLLLLLLLLLLLLLLLLLLKK..........",
                "..........KKLLLLLLLLLLLLLLLLLLLLKK..........",
                "......KKKKKKLLLLLLLLLLLLLLLLLLLLKKKKKK......",
                "......KKKKKKLLLLLLLLLLLLLLLLLLLLKKKKKK......",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKKKKKKKKKKKKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKKKKKKKKKKKKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "..KKLLLLLLLLLLLLKK........KKLLLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLLLKK........KKLLLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "....KKKKKKKKKK................KKKKKKKKKK....",
                "....KKKKKKKKKK................KKKKKKKKKK....",
            }, maneTop: 22),

            // ---- the original caps ----
            new("crop", "Crop", new[]
            {
                "......KKKKKKKKKKKKKKKKKKKK......",
                "......KKKKKKKKKKKKKKKKKKKK......",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
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
            // The same cut as "hightop", worn SHORT - flat top, one hard fade line, straight
            // stubble down the temples. The difference is the one that separates the real
            // haircuts: there is barely any hair above the line.
            //
            // THE FADE LINE SITS AT THE SAME HEIGHT AS THE HIGH TOP'S, on composed rows 12-13,
            // and that is not cosmetic - it is where the HAIRLINE is, so it decides how much
            // forehead the face has. Cut two rows higher (which is where "make it shorter" first
            // put it) the fade gives the same character a forehead a third taller than the high
            // top does, and the two stop looking like the same person in two haircuts. Shortness
            // is bought above the line, never below it: check any change here against
            // BodyLook.Head for both styles and count the skin rows down to the eyes at 20.
            //
            // Authored in the 28-row form rather than the high top's 32, which is the whole point:
            // those four extra rows are the ones above the skull, and short hair does not use
            // them. It starts at the crown.
            //
            // TRACED ONTO THE SKULL, unlike the high top, which is why it has no constant margin:
            // the head is only cols 6-25 wide at the crown and does not reach full width until
            // three rows down (dump BodyLook.Head with "shaved" to see it). Held at one margin
            // the way the high top is, the top rows hang four columns off the head into open
            // background and the cut stops reading as hair at all - it reads as a slab resting
            // on someone. The high top gets away with a constant margin because it is SUPPOSED
            // to overhang; short hair has no overhang to spend.
            //
            // THE FADE ACTUALLY FADES, and it does it in the SKIN's ramp rather than the hair's.
            // The head is composed as one grid - lowercase is the skin ramp, uppercase the hair's
            // (see Palette.Of) - so a hair style may paint skin tones, and this one walks the
            // temple down 'D' (hair, dark) -> 's' (skin, deep) -> 'd' (skin, dark) -> bare skin.
            // That is what a fade is: not hair getting darker, but progressively more SKIN
            // showing through progressively less hair. Doing it in the hair ramp instead can only
            // march toward black, which on a pale swatch is a grey stripe down the face and on a
            // dark one is invisible - and neither is a fade. In the skin's own ramp it lands
            // correctly for every skin/hair pairing in the picker without special-casing any.
            new("fade", "Fade", new[]
            {
                "......KKKKKKKKKKKKKKKKKKKK......",
                "......KKKKKKKKKKKKKKKKKKKK......",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK",
                "KKKKKKKKKKKKKKKKKKKKKKKKKKKKKKKK",
                "..DD........................DD..",
                "..DD........................DD..",
                "..ss........................ss..",
                "..ss........................ss..",
                "..dd........................dd..",
                "..dd........................dd..",
                "................................",
                "................................",
                "................................",
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
            new("long", "Long", new[]
            {
                "......KKKKKKKKKKKKKKKKKKKK......",
                "......KKKKKKKKKKKKKKKKKKKK......",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKDD........................DDKK",
                "KKDD........................DDKK",
                "KKDD........................DDKK",
                "KKDD........................DDKK",
                "KKDD........................DDKK",
                "KKDD........................DDKK",
                "KKDD........................DDKK",
                "KKDD........................DDKK",
                "..KK........................KK..",
                "..KK........................KK..",
                "................................",
                "................................",
                "................................",
                "................................",
            }, new[]
            {
                // Two blank rows of headroom. They were "ssttiiffff" - a stray word typed
                // into the grid, ten characters wide where every other row here is 44.
                // Invisible in play (t, i and f map to nothing, so they rasterised to
                // transparency and PixelSprite padded the rows out), which is why it sat
                // here unnoticed - but the ragged width crashed FaceDetail's menu-density
                // pass. Kept as two rows rather than deleted: ManeTop and the mane's own
                // centring are both measured against this grid's row COUNT.
                "............................................",
                "............................................",
                "........KKKKKKKKKKKKKKKKKKKKKKKKKKKK........",
                "........KKKKKKKKKKKKKKKKKKKKKKKKKKKK........",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLKKKKKKKKLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLKKKKKKKKLLLLLLLLLLKK......",
                "....KKLLLLLLLLLLLLKK....KKLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLKK....KKLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLKK........KKLLLLLLLLLLKK....",
                "..KKLLLLLLLLLLLLKK........KKLLLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLLLKK........KKLLLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK............KKLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLKK..............KKLLLLLLKK....",
                "..KKLLLLLLLLLLKK..............KKLLLLLLKK....",
                "..KKLLLLLLLLLLKK..............KKLLLLLLKK....",
                "..KKLLLLLLLLLLKK..............KKLLLLLLKK....",
                "....KKLLLLLLKK................KKLLLLLLKK....",
                "....KKLLLLLLKK................KKLLLLLLKK....",
                "....KKLLLLLLKK................KKLLLLLLKK....",
                "....KKLLLLLLKK................KKLLLLLLKK....",
                "......KKKKKK....................KKKKKK......",
                "......KKKKKK....................KKKKKK......",
            }, maneTop: 22),
            new("ponytail", "Ponytail", new[]
            {
                "......KKKKKKKKKKKKKKKKKKKK......",
                "......KKKKKKKKKKKKKKKKKKKK......",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKDDLLLLLLLLLLLLLLLLLLLLLLLLDDKK",
                "KKDD............................",
                "KKDD............................",
                "KKBB............................",
                "KKBB............................",
                "KKDD............................",
                "KKDD............................",
                "..KK............................",
                "..KK............................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }, new[]
            {
                "..........KKKKKKKKKKKKKKKKKKKKKKKKKKKK..........",
                "..........KKKKKKKKKKKKKKKKKKKKKKKKKKKK..........",
                "........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "..........KKKKKKKKKKKKKKKKKKKKKKKKLLLLLLLLLLKK..",
                "..........KKKKKKKKKKKKKKKKKKKKKKKKLLLLLLLLLLKK..",
                "..................................KKLLLLLLLLLLKK",
                "..................................KKLLLLLLLLLLKK",
                "..................................KKLLLLLLLLLLKK",
                "..................................KKLLLLLLLLLLKK",
                "..................................KKLLLLLLLLLLKK",
                "..................................KKLLLLLLLLLLKK",
                "....................................KKLLLLLLKK..",
                "....................................KKLLLLLLKK..",
                "....................................KKLLLLLLKK..",
                "....................................KKLLLLLLKK..",
                "....................................KKLLLLLLKK..",
                "....................................KKLLLLLLKK..",
                "....................................KKLLLLLLKK..",
                "....................................KKLLLLLLKK..",
                "....................................KKLLLLLLKK..",
                "....................................KKLLLLLLKK..",
                "......................................KKLLKK....",
                "......................................KKLLKK....",
                "......................................KKLLKK....",
                "......................................KKLLKK....",
                "......................................KKLLKK....",
                "......................................KKLLKK....",
                "........................................KK......",
                "........................................KK......",
            }, maneTop: 22),
            // Cornrows: the rows themselves, braided flat to the scalp, with no length hanging
            // off them. Long Braids below is the same crown plus plaits, and the two are
            // deliberately separate entries rather than one style with a toggle - the length is
            // most of the silhouette, so they read as different haircuts from across the arena,
            // which is the only thing a hairstyle has to do at this size.
            //
            // KEYS SAY WHAT THEY DREW FIRST. "braids" is the LONG one, because that is what a
            // save holding that key has been wearing; this one owns "cornrows". A hairstyle key
            // is a plain string in a saved Appearance and an unmatched one silently falls back to
            // the first style in this list (see HairStyle), so keys do not follow renames.
            //
            // Two things stop it reading as the striped cap the first attempt at this style was:
            //
            //   THE SPINE IS STRAIGHT. Each row carries one lit texel down its middle. The zigzag
            //   crest that makes a hanging plait read as plaited is what a rope does once it is
            //   swinging free, and over six rows of cap it comes out as noise.
            //
            //   THE HAIRLINE IS A CUT, its own full row rather than the bottom edge of the fill,
            //   the way Fade draws its own. That matters more here than it does there: the
            //   partings are transparent, so a hem takes their notch down with it and scallops
            //   itself. A cut is a straight edge the rows stop against, and it is what puts the
            //   hairline at the FRONT of the head rather than somewhere up the curve of it.
            //   Scalloped ends were tried first (each row running one row past the fringe at full
            //   width) and are recorded here as the thing that was rejected, not lost.
            //
            //   IT IS THE HAIR'S OWN DEEP TONE ('S'), NOT THE LINE TONE FADE USES. Line is 68% of
            //   the way to near-black, so on the black swatch it lands at 0.09 against a room that
            //   sits at 0.11 - a hole across the forehead rather than a hairline, and on every
            //   swatch a bar that has stopped being the colour of the hair. Deep is a fixed
            //   fraction of LIGHT (see HairRamp), so it holds the same distance below the mass on
            //   every swatch in the picker and still reads as that swatch. Fade can spend the line
            //   tone because its cut is a razored edge with bare skin below it; this one is hair
            //   meeting a forehead.
            new("cornrows", "Cornrows", new[]
            {
                "......KKKKKKKKKKKKKKKKKKKK......",
                "......KKKKKKKKKKKKKKKKKKKK......",
                "..KKLLLLLLHHLLLLLLLLHHLLLLLLKK..",
                "..KKLLLLLLHHLLLLLLLLHHLLLLLLKK..",
                "KKHHLL..LLHHLL....LLHHLL..LLHHKK",
                "KKHHLL..LLHHLL....LLHHLL..LLHHKK",
                "KKHHLL..LLHHLL....LLHHLL..LLHHKK",
                "KKHHLL..LLHHLL....LLHHLL..LLHHKK",
                "KKHHLL..LLHHLL....LLHHLL..LLHHKK",
                "KKHHLL..LLHHLL....LLHHLL..LLHHKK",
                "KKHHLL..LLHHLL....LLHHLL..LLHHKK",
                "KKHHLL..LLHHLL....LLHHLL..LLHHKK",
                "SSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSS",
                "SSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSS",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
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
            // Braids. What makes this read as braids is the LENGTH, not the scalp: the version
            // before it was cornrows and nothing else - four lit ropes over the crown, flat to the
            // skull, with no hair at all below the hairline - and a head of hair that stops where
            // the face starts reads as a striped cap however carefully the ropes are shaded.
            //
            // So the style is two halves now. The crown keeps its cornrows, with the SCALP SHOWING
            // between them - a parting only reads where it shows something else (see the header),
            // and skin is the something else. The plaits those ropes gather into hang past the
            // shoulders, on the mane, which is where the silhouette actually comes from.
            //
            // A PLAIT STEPS SIDEWAYS; IT IS NOT A TUBE WITH A STRIPE DOWN IT. Each is authored
            // five texels wide (ten drawn - everything here is block-doubled, see below) and
            // shifts one every third row, swapping which side its crest sits on as it goes:
            // silhouette, not shading, which is the half that survives on black hair. Pinching one
            // to a waist instead - the obvious way to draw a knot - was tried first and cannot
            // work at this width, because two of the five texels are outline, so a pinched row
            // comes out as outline and almost nothing else.
            //
            // AUTHORED AT HALF THIS GRID'S RESOLUTION AND DOUBLED, like the rest of the body art:
            // bodyPpu is 75 against art at 37.5, so one authored texel lands on a whole number of
            // screen pixels. The first pass drew one-texel partings and one-texel crests because
            // they looked better in isolation; both sit on half a screen pixel and shimmer as the
            // character moves, which is the same reason PixelPerfectZoom exists.
            //
            // THE PLAITS FALL OUTSIDE THE HEAD'S OWN WIDTH, and that is a constraint rather than a
            // style choice. The mane draws behind the whole body, so a plait hanging where one
            // really would - against the cheek, down the chest - sits behind the head or behind an
            // arm for its entire length. Two per side: the inner one short, reading as ending at
            // the jaw, the outer one long, and no two tips level (see the High Tail note).
            //
            // THE PARTINGS RUN ALL THE WAY TO THE HAIRLINE. They stopped a row short of it for a
            // while, which laid a band of hair across their ends; that is a fringe, and this
            // style does not have one - the scalp is parted to the front and each row ends on
            // its own. Cornrows above keeps a solid front edge instead, but it has a CUT there
            // to stop against; a row of hair laid over open partings is just a hem.
            new("braids", "Long Braids", new[]
            {
                "......KKKKKKKKKKKKKKKKKKKK......",
                "......KKKKKKKKKKKKKKKKKKKK......",
                "..LLHHLLLLLLHHLLLLLLLLHHLLLLLL..",
                "..LLHHLLLLLLHHLLLLLLLLHHLLLLLL..",
                "LLHHLL..LLHHLL....LLHHLL..LLHHLL",
                "LLHHLL..LLHHLL....LLHHLL..LLHHLL",
                "LLHHLL..LLHHLL....LLHHLL..LLHHLL",
                "LLHHLL..LLHHLL....LLHHLL..LLHHLL",
                "LLLLHH..LLLLHH....LLLLHH..LLLLHH",
                "LLLLHH..LLLLHH....LLLLHH..LLLLHH",
                "LLLLHH..LLLLHH....LLLLHH..LLLLHH",
                "LLLLHH..LLLLHH....LLLLHH..LLLLHH",
                "LLHHLL..LLHHLL....LLHHLL..LLHHLL",
                "LLHHLL..LLHHLL....LLHHLL..LLHHLL",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }, new[]
            {
                "........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "........KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLHHKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLHHKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLHHKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLHHKK......",
                "......KKHHLLLLLLLLLLLLLLLLLLLLLLLLLLLLHHLLLLKK..",
                "......KKHHLLLLLLLLLLLLLLLLLLLLLLLLLLLLHHLLLLKK..",
                "....KKHHLLLLKKLLLLKK............KKHHLLKKHHLLLLKK",
                "....KKHHLLLLKKLLLLKK............KKHHLLKKHHLLLLKK",
                "....KKHHLLLLKKLLKK................KKHHKKHHLLLLKK",
                "....KKHHLLLLKKLLKK................KKHHKKHHLLLLKK",
                "KKLLLLHHKKLLHHKK................KKLLLLKKLLLLHHKK",
                "KKLLLLHHKKLLHHKK................KKLLLLKKLLLLHHKK",
                "KKLLLLHHKKHHKK..................KKLLLLKKLLLLHHKK",
                "KKLLLLHHKKHHKK..................KKLLLLKKLLLLHHKK",
                "KKLLLLHHKKHHKK..................KKLLHHKKLLLLHHKK",
                "KKLLLLHHKKHHKK..................KKLLHHKKLLLLHHKK",
                "..KKHHLLLLKKLLKK..................KKHHLLKKHHLLLL",
                "..KKHHLLLLKKLLKK..................KKHHLLKKHHLLLL",
                "..KKHHLLLLKKKK....................KKHHLLKKHHLLLL",
                "..KKHHLLLLKKKK....................KKHHLLKKHHLLLL",
                "..KKHHLLLLKK........................KKKKKKHHLLLL",
                "..KKHHLLLLKK........................KKKKKKHHLLLL",
                "KKLLLLHHKK............................KKLLLLHHKK",
                "KKLLLLHHKK............................KKLLLLHHKK",
                "KKLLLLHHKK............................KKLLLLHHKK",
                "KKLLLLHHKK............................KKLLLLHHKK",
                "KKLLLLHHKK............................KKLLLLHHKK",
                "KKLLLLHHKK............................KKLLLLHHKK",
                "..KKHHLLLLKK............................KKHHLLLL",
                "..KKHHLLLLKK............................KKHHLLLL",
                "..KKHHLLLLKK............................KKHHLLKK",
                "..KKHHLLLLKK............................KKHHLLKK",
                "..KKHHLLLLKK............................KKHHLLKK",
                "..KKHHLLLLKK............................KKHHLLKK",
                "KKLLLLHHKK............................KKLLHHKK..",
                "KKLLLLHHKK............................KKLLHHKK..",
                "KKLLHHKK..................................KKKK..",
                "KKLLHHKK..................................KKKK..",
                "KKLLHHKK........................................",
                "KKLLHHKK........................................",
                "..KKHHLLKK......................................",
                "..KKHHLLKK......................................",
                "....KKKK........................................",
                "....KKKK........................................",
                "................................................",
                "................................................",
            }, maneTop: 22),
            new("mohawk", "Mohawk", new[]
            {
                "..........KKKKKKKKKKKK..........",
                "..........KKKKKKKKKKKK..........",
                "..........KKLLLLLLLLKK..........",
                "..........KKLLLLLLLLKK..........",
                "..........KKLLLLLLLLKK..........",
                "..........KKLLLLLLLLKK..........",
                "..........KKLLLLLLLLKK..........",
                "..........KKLLLLLLLLKK..........",
                "..........KKLLLLLLLLKK..........",
                "..........KKLLLLLLLLKK..........",
                "..........KKDDDDDDDDKK..........",
                "..........KKDDDDDDDDKK..........",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
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
            new("swept", "Swept", new[]
            {
                "......KKKKKKKKKKKKKKKKKKKK......",
                "......KKKKKKKKKKKKKKKKKKKK......",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "..KKKKLLLLLLLLLLLLLLLLLLLLKKKK..",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLDDDDKK",
                "KKLLLLLLLLLLLLLLLLLLLLLLLLDDDDKK",
                "KKLLLLLLLLLLLLLLLLLLLLDDDDKK....",
                "KKLLLLLLLLLLLLLLLLLLLLDDDDKK....",
                "KKDDLLLLLLLLLLLLDDDDKK..........",
                "KKDDLLLLLLLLLLLLDDDDKK..........",
                "..KKDDDDKK......................",
                "..KKDDDDKK......................",
                "................................",
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
            // A round mass standing well past the head's own width, and the WIDTH is the whole
            // style. The version before it was drawn entirely inside the 32-wide head grid with a
            // jittered top edge, which is a cap with a bumpy rim: there is no room in that grid for
            // an afro to be bigger than the skull, because the skull already fills it edge to edge.
            //
            // It is ONE ellipse asked twice. The mane carries everything outside the head (56
            // texels wide, half as wide again as the head) and the head grid carries the front of
            // the same shape clipped to the skull, so the two cannot disagree about where the edge
            // is. The hairline sits on the forehead, but the mass carries on DOWN THE TEMPLES past
            // the ear instead of stopping at it - that is what frames the face rather than capping
            // it.
            //
            // THE CAP IS CLIPPED TO THE SKULL ON EVERY ROW, including the two above it, even though
            // the mane behind it is wider and would happily cover a cap drawn out to the grid's own
            // edge. A cap wider at the top than the skull below it puts a STEP in the head sprite's
            // silhouette; in the arena that step is invisible (the mane showing through it is the
            // same tone), and at menu density it came out as a dark hook drawn along an edge that
            // is not an edge, because the detail pass rims whatever silhouette it is given. Cost a
            // render of the character screen to spot.
            //
            // The rim is lumpy by TWO INDEPENDENT WAVES (5 and 9 cycles round, different phases).
            // One wave mirrors itself and reads as scalloped trim rather than as hair.
            //
            // The inside is LIT, not textured. Speckling curls across the mass was tried twice and
            // reads as static both times - dark specks are the header's own confetti, light ones
            // are polka dots. What works is a band just inside the upper-left rim whose INNER
            // boundary is scalloped by its own wave: clumps catching the light, which is what a
            // curl does. Nothing below L anywhere in the mass, for the reason the Braids note
            // gives - on black hair a dark tone is indistinguishable from the room behind it.
            new("afro", "Afro", new[]
            {
                "......HHHHHHLLLLLLHHHHLLLL......",
                "......HHHHHHLLLLLLHHHHLLLL......",
                "......LLLLLLLLLLLLLLLLLLLL......",
                "......LLLLLLLLLLLLLLLLLLLL......",
                "......LLLLLLLLLLLLLLLLLLLL......",
                "......LLLLLLLLLLLLLLLLLLLL......",
                "..LLLLLLLLLLLLLLLLLLLLLLLLLLLL..",
                "..LLLLLLLLLLLLLLLLLLLLLLLLLLLL..",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "HHHHLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "HHHHLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLL........................LLLL",
                "LLLL........................LLLL",
                "LLLL........................LLLL",
                "LLLL........................LLLL",
                "LLLL........................LLLL",
                "LLLL........................LLLL",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
            }, new[]
            {
                "..........................KKKKKKKKKKKK..................",
                "..........................KKKKKKKKKKKK..................",
                "............KKKKKKKKKKKKKKLLHHHHHHHHLLKKKK..............",
                "............KKKKKKKKKKKKKKLLHHHHHHHHLLKKKK..............",
                "..........KKLLHHHHHHHHHHHHHHHHHHHHHHHHHHHHKK............",
                "..........KKLLHHHHHHHHHHHHHHHHHHHHHHHHHHHHKK............",
                "..........KKHHHHHHHHHHHHHHLLLLHHHHHHHHHHHHHHKK..........",
                "..........KKHHHHHHHHHHHHHHLLLLHHHHHHHHHHHHHHKK..........",
                "........KKLLHHHHHHHHHHHHLLLLLLHHHHLLLLLLHHHHLLKK........",
                "........KKLLHHHHHHHHHHHHLLLLLLHHHHLLLLLLHHHHLLKK........",
                "........KKHHHHHHHHLLLLLLLLLLLLLLLLLLLLLLHHHHLLKK........",
                "........KKHHHHHHHHLLLLLLLLLLLLLLLLLLLLLLHHHHLLKK........",
                "......KKLLHHHHLLLLLLLLLLLLLLLLLLLLLLLLLLHHHHLLKK........",
                "......KKLLHHHHLLLLLLLLLLLLLLLLLLLLLLLLLLHHHHLLKK........",
                "......KKHHHHLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "......KKHHHHLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "......KKHHHHLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKHHHHLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLHHHHHHLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "......KKLLHHHHHHLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK....",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "....KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK......",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "......KKLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLLKK........",
                "........KKKKKKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKKKK..........",
                "........KKKKKKLLLLLLLLLLLLLLLLLLLLLLLLLLLLKKKK..........",
                "..............KKLLLLLLLLLLLLLLLLLLLLLLKKKK..............",
                "..............KKLLLLLLLLLLLLLLLLLLLLLLKKKK..............",
                "................KKLLLLLLLLLLLLLLLLLLLLKK................",
                "................KKLLLLLLLLLLLLLLLLLLLLKK................",
                "..................KKKKKKKKLLLLLLLLLLKK..................",
                "..................KKKKKKKKLLLLLLLLLLKK..................",
                "..........................KKKKLLLLKK....................",
                "..........................KKKKLLLLKK....................",
            }, maneTop: 40),
            // Reference: a high-top fade - a squared-off mass of hair sitting on the crown, cut
            // FLAT across the top, OVERHANGING the temples, and flaring slightly wider the
            // higher it goes. Below it the sides are shaved to the skin, so the whole cut reads
            // as two pieces meeting at one hard horizontal line.
            //
            // Built narrow first and that was wrong twice over: a column narrower than the head
            // reads as a block balanced on top of someone, and it put the fade on the OUTSIDE of
            // the silhouette where there was nothing left to fade. The box has to be the widest
            // thing on the head - wider than crop's dome - so that the shaved sides
            // sit UNDER an overhang. That overhang is the whole silhouette.
            //
            // The flare is what keeps it from reading as a hat: real ones are cut wider at the
            // top than at the base, so the sides lean OUT going up (margin 2 -> 1 here). The hard
            // line is drawn as its own full row of 'K' rather than left as the bottom edge of the
            // fill, because a fade is a CUT, not an outline.
            //
            // Below the line are the shaved sides, as two texels of 'D' stubble down each temple.
            // They run STRAIGHT, at the same margin as the cut line's own ends and the box side
            // above them. Stepping them inward a texel as the skull narrows was the obvious thing
            // to do and it was wrong: a two-texel mark that moves sideways reads as a hook curling
            // off the temple, not as hair. Held on one column they continue the box's edge, which
            // is what makes them read as the sides of the same haircut.
            new("hightop", "High Top", new[]
            {
                ".KKKKKKKKKKKKKKKKKKKKKKKKKKKKKK.",
                ".KKKKKKKKKKKKKKKKKKKKKKKKKKKKKK.",
                ".KKLLLLLLLLLLLLLLLLLLLLLLLLLLKK.",
                ".KKLLLLLLLLLLLLLLLLLLLLLLLLLLKK.",
                ".KKLLLLLLLLLLLLLLLLLLLLLLLLLLKK.",
                ".KKLLLLLLLLLLLLLLLLLLLLLLLLLLKK.",
                ".KKLLLLLLLLLLLLLLLLLLLLLLLLLLKK.",
                ".KKLLLLLLLLLLLLLLLLLLLLLLLLLLKK.",
                "..KKLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "..KKLLLLLLLLLLLLLLLLLLLLLLLLKK..",
                "..KKKKKKKKKKKKKKKKKKKKKKKKKKKK..",
                "..KKKKKKKKKKKKKKKKKKKKKKKKKKKK..",
                "..DD........................DD..",
                "..DD........................DD..",
                "..DD........................DD..",
                "..DD........................DD..",
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
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
            new("shaved", "Shaved", new[]
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
        };

        // ---------------------------------------------------------------- lookup

        public static Color SkinColor(string key) => Find(Skins, key);
        public static Color HairColor(string key) => Find(Hairs, key);
        public static Color EyeColor(string key) => Find(Eyes, key);

        public static Palette.Ramp SkinRamp(string key) => new(SkinColor(key));
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
                                                          Palette.Ramp eyes, Palette.Ramp? brow = null)
        {
            var map = Palette.Of(skin, hair, eyes);

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

        public static Style HairStyle(string key) => Find(HairStyles, key);
        public static Style Beard(string key) => Find(Beards, key);
        public static Style Expression(string key) => Find(Expressions, key);

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
        /// A hairstyle opts in by being authored two rows taller. Everything else - the skull, the
        /// expressions, the beards, and the eight original caps - is authored at 14 and gets two
        /// blank rows here, so none of them changes by a pixel.
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
        static readonly string[] BrowedExpressions = { "determined" };

        /// <param name="leftEyeGlow">
        /// Overwrite the character's own LEFT eye (the one facing the viewer's right in the
        /// default, unmirrored pose - see GlowingEye's own note) to a fixed red, regardless of
        /// the Eyes swatch chosen on the body tab. GearItem.HasGlowingEye asks for this - the
        /// item is meant to REPLACE the eye's colour, not merely glow in front of it, so the
        /// override has to happen where the colour is actually decided rather than as a sprite
        /// laid on top.
        /// </param>
        public static string[] Head(string expression, string hairStyle, string beard,
                                    string brows = "natural", bool leftEyeGlow = false,
                                    int gaze = 0)
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
                           Expand(face, solid: false),
                           BeardGrid(beard),
                           Expand(Brow(brows).Grid, solid: false),
                           Expand(TrailHair(HairStyle(hairStyle).Grid, gaze), solid: true));

            head = leftEyeGlow ? OverrideLeftEye(head) : head;
            return gaze == 0 ? head : Gaze(head, gaze);
        }

        /// <summary>
        /// Slide the EYES sideways within the face, so the character looks where they are facing
        /// instead of out at the player.
        ///
        /// The rig has always had three directions - facing right, its mirror, and facing away -
        /// but the face is drawn dead-on, so the two mirrored directions both read as "staring at
        /// the camera". This is the cheapest thing that breaks that: the head SHAPE does not
        /// change, only where the eyes sit inside it, which is most of what reads as a turn at
        /// this resolution.
        ///
        /// ONLY THE EYE DIGITS MOVE. The skull outline, the hair and the brow stay exactly where
        /// they are - a post-pass that shifted everything would just be a head drawn off-centre,
        /// and one that shifted the silhouette would tear it. Same reasoning, and the same
        /// column-wise mechanism, as OverrideLeftEye above.
        ///
        /// The vacated cells are filled from the run's own NEIGHBOUR rather than a hardcoded skin
        /// character, because what sits behind an eye differs by expression - skin for most, but a
        /// mask or a full white sclera for others. Copying the neighbour is right in every case
        /// without this pass needing to know which.
        ///
        /// Shift is in composed texels. Features are authored on 2x2 blocks (see the 16x14 note at
        /// the top of this file), so a shift of 2 is exactly ONE authored cell - the smallest move
        /// that is not a half-cell smear.
        /// </summary>
        /// <summary>
        /// Pull the hair mass BACKWARD, away from where the character is facing.
        ///
        /// This is the half of the turn the eyes cannot do. At arena size the SILHOUETTE carries
        /// the read and interior detail does not: a head whose outline is symmetric reads as
        /// facing the viewer no matter where its eyes are pointed, which is why shifting the eyes
        /// alone left the head looking front-on against a body that had turned.
        ///
        /// A UNION, NOT A SHIFT, and that distinction is the whole reason this is safe. Sliding
        /// the grid backward would strip hair off the forehead and expose bare skull - and this
        /// file's own rule for the hair grids is explicit that gaps cut into rows sitting on the
        /// skull "show raw skin and read as bites taken out of the head". Unioning the grid with a
        /// copy of itself displaced backward only ever ADDS: the front hairline stays exactly
        /// where it was drawn, and the mass grows a trailing edge behind the skull.
        ///
        /// Costs nothing per style. Every hairstyle gets a trailing mass from its own art, so
        /// nothing here has to know which of them is a crop, a mane or a topknot.
        /// </summary>
        static string[] TrailHair(string[] hair, int gaze)
        {
            if (hair == null || gaze == 0) return hair;

            var outRows = new string[hair.Length];
            for (int r = 0; r < hair.Length; r++)
            {
                string row = hair[r];
                var chars = row.ToCharArray();
                for (int c = 0; c < row.Length; c++)
                {
                    if (row[c] != '.') continue;          // only ever fill a hole
                    int from = c + gaze;                  // the cell this one trails
                    if (from >= 0 && from < row.Length && row[from] != '.')
                        chars[c] = row[from];
                }
                outRows[r] = new string(chars);
            }
            return outRows;
        }

        /// <summary>
        /// How many composed texels come off the OUTER edge of the far eye - see Gaze. Two, which
        /// is one authored cell.
        /// </summary>
        const int FarEyeTrim = 2;

        static string[] Gaze(string[] head, int shift)
        {
            var outRows = new string[head.Length];
            for (int r = 0; r < head.Length; r++)
            {
                string row = head[r];
                bool any = false;
                foreach (char c in row) if (c >= '1' && c <= '6') { any = true; break; }
                if (!any) { outRows[r] = row; continue; }

                var chars = row.ToCharArray();

                // Lift the eyes out, backfilling each with the nearest non-eye char on that row.
                for (int c = 0; c < chars.Length; c++)
                {
                    if (row[c] < '1' || row[c] > '6') continue;
                    char fill = '.';
                    for (int d = 1; d < row.Length; d++)
                    {
                        int l = c - d, rr = c + d;
                        if (l >= 0 && (row[l] < '1' || row[l] > '6')) { fill = row[l]; break; }
                        if (rr < row.Length && (row[rr] < '1' || row[rr] > '6')) { fill = row[rr]; break; }
                    }
                    chars[c] = fill;
                }

                // Set them down again, shifted. A move off the grid is dropped rather than wrapped.
                for (int c = 0; c < row.Length; c++)
                {
                    if (row[c] < '1' || row[c] > '6') continue;
                    int to = c + shift;
                    if (to >= 0 && to < chars.Length) chars[to] = row[c];
                }

                // AND NARROW THE FAR EYE, which is what actually sells the turn. Sliding both eyes
                // across is a character glancing sideways; the far eye also losing width to the
                // curve of the skull is a character whose HEAD has turned. One authored cell off
                // its outer edge - enough to read, not enough to look injured.
                //
                // The far eye is the one on the LEADING side - the side the head has turned toward.
                // Turning carries that eye round the curve of the skull and away from the camera,
                // while the trailing eye comes round toward the middle of the head and is seen more
                // nearly face-on. This used to trim the trailing eye, which made the eye that should
                // be foreshortened the biggest thing on the face - and pushed it, full width, into
                // the skull's outline. Which end to trim flips with the shift's sign; found by
                // scanning in from that side for the first eye texel.
                int step = shift > 0 ? -1 : 1;
                int from = shift > 0 ? chars.Length - 1 : 0;
                for (int c = from; c >= 0 && c < chars.Length; c += step)
                {
                    if (chars[c] < '1' || chars[c] > '6') continue;
                    char fill = '.';
                    for (int d = 1; d < chars.Length; d++)
                    {
                        int o = c - step * d;
                        if (o >= 0 && o < chars.Length && (chars[o] < '1' || chars[o] > '6'))
                        { fill = chars[o]; break; }
                    }
                    for (int k = 0; k < FarEyeTrim; k++)
                    {
                        int t = c + step * k;
                        if (t >= 0 && t < chars.Length && chars[t] >= '1' && chars[t] <= '6')
                            chars[t] = fill;
                    }
                    break;
                }

                outRows[r] = new string(chars);
            }
            return outRows;
        }

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

        static string[] OverrideLeftEye(string[] grid)
        {
            int w = grid[0].Length, half = w / 2;
            var outp = (string[])grid.Clone();
            for (int y = 0; y < grid.Length; y++)
            {
                char[] row = null;
                for (int x = half; x < w; x++)
                {
                    if (!EyeGlowChars.TryGetValue(grid[y][x], out var rep)) continue;
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

        public static string HeadKey(Chain.Appearance a, bool leftEyeGlow = false, int gaze = 0)
            => $"body.head.{a.Skin}.{a.Hair}.{a.Eyes}.{a.HairStyle}.{a.Expression}.{a.Beard}.{a.Brows}.{a.BrowColour}" +
               (leftEyeGlow ? ".glow" : "") + (gaze != 0 ? ".g" + gaze : "");

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
            var view = View(hairStyle, back);
            if (view != null)
                return view.Mane == null ? null : Clip(view.Mane, view.ManeTop, 1, helm);

            var mane = HairStyle(hairStyle).Mane;
            if (mane == null || mane.Length == 0) return null;
            return Clip(mane, HairStyle(hairStyle).ManeTop, 1, helm);
        }

        /// <summary>The top edge of the mane <see cref="Mane"/> returns for this view - see
        /// <see cref="Style.ManeTop"/> for the frame it is stated in.</summary>
        public static int ManeTopFor(string hairStyle, bool back = false)
            => View(hairStyle, back)?.ManeTop ?? HairStyle(hairStyle).ManeTop;

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
        // arena grids (Skull, Expressions, Beards, Brows, HairStyles' Grid/Mane) are now
        // block-doubled versions of themselves, so reaching the same absolute menu resolution
        // needs half the additional multiplier it used to.
        //
        // DERIVED from the (now 32x28) grids, not authored beside them. Authoring would mean a
        // second set of one skull, eight hairstyles, twelve expressions and six beards - twenty-
        // seven grids at 64x56 - and every one of them a chance for the detailed face to stop being
        // the same character as the one in the arena. Here there is one source of truth and the
        // detail is a PASS over it, so a new hairstyle gets its menu version for free.
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
                                          int scale = DetailScale, int gaze = 0)
        {
            var style = HairStyle(hairStyle);
            // The EXPANDED expression grid, not the authored one. Both the lower-face rows and the
            // rows above the skull shift every mark down, so the raw grid's eye coordinates are
            // two rows out - which put the eyes on the hairline and mangled them, with nothing
            // about the render saying the cause was an index rather than the art.
            var face = Expand(Expression(expression).Grid, solid: false);
            if (gaze != 0) face = Gaze(face, gaze);
            // The beard in the same final space Head composes it in, so FaceDetail can tell its own
            // hair-toned cells apart from a beard's - see the note on FaceDetail.Hair.Draw.
            var beardMask = BeardGrid(beard);

            // The SIDE view, where a style has one: drawn at the body's own density, stamped over
            // a head composed bare - the same route menu hair took, now the only one.
            var side = View(hairStyle, back: false);
            if (side != null)
                return FaceDetail.BuildMenuHair(Head(expression, "shaved", beard, brows, gaze: gaze), face,
                                                scale, HairValue(hair), side.Cap, 1, side.Mane != null,
                                                beardMask, leftEyeGlow);

            if (style.MenuGrid == null)
                return FaceDetail.Build(Head(expression, hairStyle, beard, brows, gaze: gaze), face,
                                        scale, HairValue(hair), style.Mane != null,
                                        beardMask, leftEyeGlow);

            // Composed BARE - "shaved" is the empty hairstyle - so the skull is intact under the
            // finer hair rather than already overwritten by the coarse version of it.
            return FaceDetail.BuildMenuHair(Head(expression, "shaved", beard, brows, gaze: gaze), face,
                                            scale, HairValue(hair),
                                            style.MenuGrid, MenuHairScale, style.Mane != null,
                                            beardMask, leftEyeGlow);
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
            var view = View(hairStyle, back);
            if (view != null)
                return view.Mane == null
                    ? null
                    : FaceDetail.BuildHair(Clip(view.Mane, view.ManeTop, 1, helm), scale, scale,
                                           view.ManeTop, HairValue(hair));

            var style = HairStyle(hairStyle);
            if (style.MenuMane != null)
                return FaceDetail.BuildHair(Clip(style.MenuMane, style.ManeTop, MenuHairScale, helm),
                                            scale / MenuHairScale, scale,
                                            style.ManeTop, HairValue(hair));

            var grid = Mane(hairStyle, helm);
            return grid == null
                ? null
                : FaceDetail.BuildHair(grid, scale, scale, style.ManeTop,
                                       HairValue(hair));
        }
    }
}
