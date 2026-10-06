using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;

namespace Convergence.Art
{
    /// <summary>
    /// The colours pixel art is drawn from.
    ///
    /// Pixel art lives or dies on a consistent, small palette - a handful of colours reused
    /// everywhere reads as deliberate, while every piece inventing its own reads as noise. The
    /// project previously had colour literals scattered across DemoGear, ElementInfo, TierColor
    /// and StatusVisuals with no relationship between them, which is exactly the failure mode.
    ///
    /// Everything is expressed as a <see cref="Ramp"/>: a base colour plus its highlight, shade
    /// and outline. Three tones and a line is the classic pixel-art minimum for reading form at
    /// this size, and it means one grid can serve every tier by swapping the ramp.
    /// </summary>
    public static class Palette
    {
        /// <summary>
        /// One material's tone ramp. Grids reference these by character.
        ///
        /// Four tones (light / base / dark / line) were the minimum for reading form at 37.5 ppu.
        /// At 75 ppu a shape has twice the texels on each axis to spend, so the ramp gained a
        /// <see cref="Glow"/> specular step above the light and a <see cref="Deep"/> step between
        /// dark and the line - enough to round a plate edge or a helmet dome without the banding a
        /// four-tone ramp shows once the pixels are that small.
        /// </summary>
        public readonly struct Ramp
        {
            public readonly Color Glow, Light, Base, Dark, Deep, Line;

            public Ramp(Color baseColor, float lift = 0.34f, float shade = 0.30f, float line = 0.68f)
            {
                Base = baseColor;
                Glow = Color.Lerp(baseColor, Color.white, Mathf.Clamp01(lift + 0.30f));
                Light = Color.Lerp(baseColor, Color.white, lift);
                Dark = Color.Lerp(baseColor, Color.black, shade);
                Deep = Color.Lerp(baseColor, Color.black, Mathf.Clamp01(shade + 0.24f));
                Line = Color.Lerp(baseColor, new Color(0.05f, 0.04f, 0.08f), line);
            }

            /// <summary>
            /// The same ramp with <see cref="Dark"/> and <see cref="Deep"/> placed at fixed
            /// FRACTIONS OF LIGHT'S VALUE rather than by lerping the base toward black.
            ///
            /// Lerping toward black spaces a ramp by a fraction of the BASE, which means how far
            /// apart the tones land depends entirely on how bright the base happens to be. On a
            /// 0.97 swatch Glow, Light and Base collapse into one colour and the next tone down is
            /// a third of the way to black - a hole with nothing in it; on a 0.16 swatch the
            /// bottom three collapse instead. Neither can carry three readable tones, which is
            /// what hair needs.
            ///
            /// Multiplying preserves hue and saturation while placing the tones where they are
            /// actually wanted, so any swatch gets the same spacing.
            /// </summary>
            public Ramp WithShadowsBelowLight(float darkOfLight, float deepOfLight)
            {
                var r = this;
                return new Ramp(r.Glow, r.Light, r.Base,
                                Scale(r.Light, darkOfLight), Scale(r.Light, deepOfLight), r.Line);
            }

            /// <summary>
            /// The same ramp with <see cref="Glow"/> and <see cref="Light"/> lifted toward
            /// <paramref name="sheen"/> instead of white.
            ///
            /// A warm metal lit toward WHITE desaturates on its lit face, and a desaturated warm
            /// brown is wood: bronze built that way read as a wooden practice sword. Metal reads by
            /// a highlight in its own hue, bright against a deep shadow.
            /// </summary>
            public Ramp WithHighlightsToward(Color sheen, float lift)
                => new(Color.Lerp(Base, sheen, Mathf.Clamp01(lift + 0.30f)),
                       Color.Lerp(Base, sheen, lift), Base, Dark, Deep, Line);

            /// <summary>
            /// The whole ramp multiplied toward black, tone for tone.
            ///
            /// Not the same as re-deriving a ramp from a darker base, which is what every "far
            /// limb" ramp in the project did before: that re-runs the lerps and so re-spaces the
            /// tones, and on an already-dark material it closes the gaps between them entirely.
            /// Multiplying moves the material into shadow while leaving the spacing that makes it
            /// readable exactly as authored - the same reasoning as
            /// <see cref="WithShadowsBelowLight"/>, applied to the ramp as a whole.
            /// </summary>
            public Ramp Scaled(float f)
                => new(Scale(Glow, f), Scale(Light, f), Scale(Base, f),
                       Scale(Dark, f), Scale(Deep, f), Scale(Line, f));

            /// <summary>A ramp from six tones placed by hand rather than derived from a base - the
            /// cloth dye's, built round a measured cape colour (Gear.ClothDye.RampFor).</summary>
            public static Ramp FromTones(Color glow, Color light, Color b, Color dark, Color deep, Color line)
                => new(glow, light, b, dark, deep, line);

            Ramp(Color glow, Color light, Color b, Color dark, Color deep, Color line)
            {
                Glow = glow; Light = light; Base = b; Dark = dark; Deep = deep; Line = line;
            }

            static Color Scale(Color c, float f) => new(c.r * f, c.g * f, c.b * f, c.a);
        }

        // ---- fixed materials ----

        public static readonly Ramp Skin = new(new Color(0.84f, 0.70f, 0.58f));
        public static readonly Ramp Leather = new(new Color(0.46f, 0.32f, 0.24f));

        /// <summary>
        /// The black bodysuit every character wears under their armour - the whole body below the
        /// neck, so the only skin on the figure is the face.
        ///
        /// WHY A BODY PART IS A MATERIAL. Everything below the neck used to be three different
        /// things: the torso took <see cref="Cloth"/> (the element's accent), the arms took the
        /// player's chosen skin tone, the legs took a darkened cloth. Armour then sat on top of
        /// three surfaces that had nothing to do with each other, so a plate reading correctly
        /// against the chest could read as floating against the thigh. One material underneath
        /// makes the armour the only thing the eye has to resolve, which is what the reference
        /// does - the black is the negative space BETWEEN plates, and it is the same black
        /// everywhere.
        ///
        /// IT IS NOT #000000, AND CANNOT BE. Two separate reasons, and both were measured:
        ///
        /// 1. The arena floor is (0.100, 0.110, 0.140) - itself nearly black. A suit at the bottom
        ///    of its own range is the same value as the ground it stands on, and a body that
        ///    matches the floor inside a dark border reads as a HOLE rather than a figure. The
        ///    base therefore sits at ~0.18, comfortably above the floor, and still four times
        ///    darker than the silver plate it is worn under - which is what actually makes a
        ///    colour read as "black" at this size. Contrast against the neighbour is the whole of
        ///    it; the absolute value is not.
        ///
        /// 2. A near-zero base collapses its own ramp. <see cref="Ramp"/>'s Dark and Deep are
        ///    lerps of the base toward black, so at 0.05 the bottom three tones land within a
        ///    hundredth of each other and every fold in the grids disappears. black_hood hit this
        ///    first and answered it by sitting at 0.14; this goes further and uses
        ///    <see cref="Ramp.WithShadowsBelowLight"/>, which places the shadows as fractions of
        ///    LIGHT instead, so the spacing survives however dark the base is.
        ///
        /// LIFT IS 0.15, well under the 0.34 default. On a dark base the default lift puts the
        /// light tone at 0.45 - a mid grey, which reads as a grey suit with black shadows rather
        /// than a black suit with a sheen on it. The highlight has to stay dark enough to still be
        /// black; it is there to turn an edge, not to light the material.
        ///
        /// DO NOT REACH FOR 'h' ON THIS RAMP. <see cref="Ramp.Glow"/> is derived as lift + 0.30,
        /// which lands at 0.55 luminance here - three times the base, and a mid grey in absolute
        /// terms. None of the three body grids uses it (they spend k/s/d/b/l and stop), and a
        /// future one that did would get a near-white specular on a black suit rather than a
        /// highlight. The tone is left alone rather than clamped because Glow is derived for every
        /// material at once and this is the only one where it is unusable; the constraint belongs
        /// in the note, not in an exception inside Ramp.
        /// </summary>
        public static readonly Ramp Undersuit =
            new Ramp(new Color(0.175f, 0.175f, 0.215f), lift: 0.15f, shade: 0.34f, line: 0.80f)
                .WithShadowsBelowLight(0.46f, 0.30f);

        /// <summary>
        /// The same suit on a limb that is BEHIND the body - the far arm and far leg.
        ///
        /// A flat copy would be wrong: the near and far limbs are the same grid, so depth is
        /// carried entirely by tone, and on a black suit there is very little tone left to spend.
        /// Darkening the base toward black is what the skin/cloth versions did and it is exactly
        /// what does not work here - it walks straight into the collapse the main ramp exists to
        /// avoid. Instead the whole ramp is multiplied down, which keeps the SPACING between the
        /// tones intact while moving the limb as a whole into shadow.
        ///
        /// 0.80, AND THE FLOOR IS WHAT PICKS IT. The arena ground is 0.110 luminance, which does
        /// not sit below this material - it sits INSIDE its range, between Deep and Base. So a far
        /// ramp is not choosing "how much darker"; it is choosing WHICH TONE LANDS ON THE GROUND
        /// and therefore disappears. Measured at a first pass of 0.62, that tone was Base - the
        /// widest one - and the far limb was simply not in the render. At 0.80 only Dark lands
        /// near the floor, one interior column of four, with Base and Light above it and Deep and
        /// Line well below; the limb is bounded top and bottom by tones that read.
        ///
        /// That leaves less depth separation than the old skin/cloth pairs had (0.151 against the
        /// near suit's 0.178), and it is enough because tone was never carrying this alone: the far
        /// limb is drawn behind the torso and mostly occluded by it, so overlap does most of the
        /// work and the ramp only has to finish it.
        /// </summary>
        public static readonly Ramp UndersuitFar = Undersuit.Scaled(0.80f);

        /// <summary>Near-black used where a piece needs a hard edge against anything behind it.</summary>
        public static readonly Color Outline = new(0.08f, 0.07f, 0.11f);

        /// <summary>
        /// A violet-black metal. A MATERIAL, like leather - it says nothing about where the item
        /// came from.
        ///
        /// It exists because <see cref="Tier"/> was being used as a colour source, which quietly
        /// made rarity and appearance the same decision. They are not: a tier is the box an item
        /// drops from, and two items of one tier should be able to look nothing like each other.
        /// </summary>
        public static readonly Ramp Void = new(new Color(0.62f, 0.42f, 0.86f));

        /// <summary>
        /// Cast bronze - a MATERIAL, like <see cref="Void"/>. Lit toward pale gold rather than
        /// white (see <see cref="Ramp.WithHighlightsToward"/>), and kept separate from the Bronze
        /// TIER swatch, a copper-orange chosen to read as a UI chip; borrowing that is
        /// exactly the tier-as-palette shortcut this block exists to stop. A bronze item and the
        /// Bronze tier agreeing here is a coincidence of the demo gear, not a rule.
        /// </summary>
        /// <summary>Plain forged iron - a MATERIAL. Cooler and darker than the Silver tier swatch,
        /// so a sword's body in it reads as working metal rather than as a Silver-tier item.</summary>
        public static readonly Ramp Iron = new(new Color(0.42f, 0.42f, 0.46f));

        public static readonly Ramp Bronze =
            new Ramp(new Color(0.62f, 0.40f, 0.17f), shade: 0.42f)
                .WithHighlightsToward(new Color(1.00f, 0.88f, 0.58f), 0.42f);

        // ---- tier swatches ----
        //
        // These match CharacterScreen.TierColor so the swatch in the loadout UI and the character
        // agree. If one moves, move both.
        //
        // A TIER IS A RARITY, NOT A PALETTE. This is the right colour for a UI chip that has to
        // say "gold" at a glance, and it happens to be the right colour for a gilded helm - but
        // reaching for it as an item's art ramp is what makes gold tier mean gold coloured, and
        // it does not. New pieces should name a MATERIAL above; the demo gear still names tiers
        // here because it was written before the distinction mattered.

        public static Ramp Tier(LootTier tier) => tier switch
        {
            LootTier.Bronze       => new Ramp(new Color(0.72f, 0.45f, 0.20f)),
            LootTier.Silver       => new Ramp(new Color(0.78f, 0.80f, 0.85f)),
            LootTier.Gold         => new Ramp(new Color(0.95f, 0.78f, 0.32f)),
            LootTier.Diamond      => new Ramp(new Color(0.55f, 0.85f, 1.00f)),
            _                     => new Ramp(new Color(0.75f, 0.45f, 1.00f)),
        };

        /// <summary>
        /// Cloth takes the element's accent, so each element reads at a glance.
        ///
        /// CURRENTLY NOTHING CALLS THIS. It had exactly one caller - the rig's torso and legs -
        /// and the body below the neck is now one black material (<see cref="Undersuit"/>), so the
        /// element no longer shows on the character at all. Kept rather than deleted because the
        /// accent does need to come back somewhere and this is the right shape for it; see
        /// PrimitiveCharacterRig.DrawPixelBody for why it was not quietly reintroduced as a tint on
        /// the suit. If it is still unused the next time someone reads this, delete it.
        /// </summary>
        public static Ramp Cloth(Core.ElementType element)
            => new(Color.Lerp(Core.ElementInfo.Tint(element), new Color(0.20f, 0.20f, 0.26f), 0.52f));

        // ---- grid character conventions ----
        //
        // One letter per tone so a grid stays readable as a picture in source:
        //   . transparent   k line   s deep shade   d dark   b base   l light   h glow
        // A second material in the same grid uses uppercase: K S D B L H.

        public static Dictionary<char, Color> Of(Ramp main)
            => new()
            {
                ['k'] = main.Line,
                ['s'] = main.Deep,
                ['d'] = main.Dark,
                ['b'] = main.Base,
                ['l'] = main.Light,
                ['h'] = main.Glow,
            };

        public static Dictionary<char, Color> Of(Ramp main, Ramp second)
        {
            var map = Of(main);
            map['K'] = second.Line;
            map['S'] = second.Deep;
            map['D'] = second.Dark;
            map['B'] = second.Base;
            map['L'] = second.Light;
            map['H'] = second.Glow;
            return map;
        }

        /// <summary>
        /// Three materials in one grid. The third takes DIGITS - 1 line, 2 deep, 3 dark, 4 base,
        /// 5 light, 6 glow - because the alphabet is out of cases and a grid has to stay readable
        /// as a picture in source. Digits are distinct enough from letters at a glance that an
        /// eye or a gem embedded in a face still reads while scanning the rows.
        ///
        /// Added for the head, which needs skin, hair and eyes at once.
        /// </summary>
        public static Dictionary<char, Color> Of(Ramp main, Ramp second, Ramp third)
        {
            var map = Of(main, second);
            map['1'] = third.Line;
            map['2'] = third.Deep;
            map['3'] = third.Dark;
            map['4'] = third.Base;
            map['5'] = third.Light;
            map['6'] = third.Glow;
            return map;
        }
    }
}
