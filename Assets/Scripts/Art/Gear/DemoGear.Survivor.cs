using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Survivor: a white tabard over steel
    //
    // After the user's reference ("The Lone Survivor"): a knight in grey steel under a plain
    // white tabard, silver at its collar. The reference's hood and cape are NOT part of the set
    // (the user's call) - instead the tabard's CLOTH takes the colour of whatever Back piece is
    // worn (GearItem.DyedByBack, see ClothDye), white with none.
    //
    //   survivor_tabard     Torso      the white cloth panel down the chest, a steel gorget and a
    //                                  silver yoke at its top, steel at the flanks, and a CHAIN
    //                                  across the chest from shoulder to shoulder
    //   survivor_pauldrons  Shoulders  ONE big rounded plate a shoulder, both alike: SOLID reactive
    //                                  metal in the cape's colour inside a SILVER trim, thicker at
    //                                  the top toward the neck; a dark lame under it, and a ring at
    //                                  the inner end where the chain runs under it
    //   survivor_gauntlets  Gloves     steel couter and vambrace, dark plated hands
    //   survivor_belt       Belt       a wide belt with a round silver buckle, a second belt slung
    //                                  low across the hips, a pouch on the far hip
    //   survivor_tassets    Legs       the tabard's skirt from the belt to below the knee, its hem
    //                                  TORN with one long tail; steel faulds at the hips, banded
    //                                  cuisses and a knee cop on each leg
    //   survivor_greaves    Boots      dark greaves and sabatons
    //
    // THE CHAINS ARE THE TABARD'S, not the pauldrons'. A pauldron turns with its arm (a quarter of
    // the carry's raise, PrimitiveCharacterRig.PauldronFollow), so a chain drawn on one swings
    // loose across the chest; on the torso they hang still and run UNDER both pauldrons' inner
    // edges, which is what reads as the plates being chained together.
    //
    // THE CLOTH IS ON TWO PIECES (tabard and tassets), both DyedByBack, so the panel stays one
    // colour from the collar to the hem. The pauldrons' plates are a THIRD dyed piece - metal,
    // but painted in the cloth ramp so they take the same colour. Silver and steel never dye -
    // only the cloth ramp's texels are repainted.
    //
    // Fields in each joint's own frame (DemoGear.Fields.cs), lit from +X like every piece here.
    // Diamond, power 0, like every reference-built cosmetic set.
    public static partial class DemoGear
    {
        const string SurSteel = "ksdblh", SurDark = "KSDBLH", SurCloth = "123456";

        static void AddSurvivor(List<GearItem> items)
        {
            // Grey steel, near white where lit: Errant's steel, the reference's plate is the same.
            var steel = new Palette.Ramp(new Color(0.60f, 0.61f, 0.65f), lift: 0.40f, shade: 0.38f, line: 0.78f)
                .WithShadowsBelowLight(0.52f, 0.34f);
            // Dark steel and worn leather share one warm charcoal - the belts, the hands, the shins.
            var dark = new Palette.Ramp(new Color(0.30f, 0.28f, 0.28f), lift: 0.28f, shade: 0.34f, line: 0.80f)
                .WithShadowsBelowLight(0.50f, 0.32f);
            const float farF = ClothDye.Far;   // the dye matches the far cloth at this shade

            // The cloth MUST be ClothDye.Undyed, tone for tone: it is what the dye matches.
            var pal = Palette.Of(steel, dark, ClothDye.Undyed);
            var far = Palette.Of(steel.Scaled(farF), dark.Scaled(farF), ClothDye.Undyed.Scaled(farF));

            var tabard = Defends(Make("survivor_tabard", "Survivor Tabard", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.survivor", SurTabardRows, pal,
                       0f, FieldCentreCells(SurTabardBottom, SurTabardTop), ppu: BodyPpu)),
                // The one left standing: turns the blow rather than taking it.
                DefensiveAbility.ParryStance);
            tabard.DyedByBack = true;
            items.Add(tabard);

            // The plates are REACTIVE metal in the cloth ramp, dyed with the tabard; only the
            // silver trim, the gap and the ring stay steel.
            var pauldrons = Make("survivor_pauldrons", "Survivor Pauldrons", GearSlot.Shoulders, LootTier.Diamond, 0f,
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.survivor", SurPauldronFar, far,
                       ShoulderX + SurPauldronCentreCells, SurPauldronY, ppu: BodyPpu),
                Pixels(RigLayer.Shoulders, "gear.pauldron.survivor.near", SurPauldronNear, pal,
                       -ShoulderX - SurPauldronCentreCells, SurPauldronY, ppu: BodyPpu));
            pauldrons.DyedByBack = true;
            items.Add(pauldrons);

            items.Add(Make("survivor_gauntlets", "Survivor Gauntlets", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesFront, "gear.gloves.survivor", SurArmRows, pal,
                       0f, FieldCentreCells(SurArmBottom, SurArmTop), ppu: BodyPpu),
                Pixels(RigLayer.GlovesBack, "gear.gloves.survivor.dark", SurArmRows, far,
                       0f, FieldCentreCells(SurArmBottom, SurArmTop), ppu: BodyPpu)));

            items.Add(Make("survivor_belt", "Survivor Belt", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.survivor", SurBeltRows, pal,
                       0f, FieldCentreCells(SurBeltBottom, SurBeltTop), ppu: BodyPpu)));

            var tassets = Make("survivor_tassets", "Survivor Tassets", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.Tasset, "gear.legs.survivor.skirt", SurSkirtRows, pal,
                       0f, FieldCentreCells(SurSkirtBottom, SurSkirtTop), ppu: BodyPpu),
                Pixels(RigLayer.LegsFront, "gear.legs.survivor", SurCuisseRows, pal,
                       0f, FieldCentreCells(SurCuisseBottom, SurCuisseTop), ppu: BodyPpu),
                Pixels(RigLayer.LegsBack, "gear.legs.survivor.dark", SurCuisseRows, far,
                       0f, FieldCentreCells(SurCuisseBottom, SurCuisseTop), ppu: BodyPpu));
            tassets.DyedByBack = true;
            items.Add(tassets);

            items.Add(Make("survivor_greaves", "Survivor Greaves", GearSlot.Boots, LootTier.Diamond, 0f,
                Pixels(RigLayer.BootsFront, "gear.boots.survivor", SurGreaveRows, pal,
                       0f, FieldCentreCells(SurGreaveBottom, SurGreaveTop), ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.survivor.dark", SurGreaveRows, far,
                       0f, FieldCentreCells(SurGreaveBottom, SurGreaveTop), ppu: BodyPpu)));

            // The cloth is the CAPE's (ClothDye), so it is never a channel - the dye takes the metal
            // and the straps, and a dyed cape still recolours the tabard through the measurement.
            Dyeable(items, "survivor_", DyeChannel.Of("Plate", DyeMaterial.Metal, steel, farF),
                    DyeChannel.Of("Straps", DyeMaterial.Leather, dark, farF));
        }

        // ------------------------------------------------------------------ the tabard (Torso)
        //
        // Torso-local, the waist at 0, the chin at 40 (the head covers everything above it).
        // Top to bottom: a steel GORGET at the neck; a SILVER YOKE along the cloth's top edge,
        // dipping to a clasp at the centre; the white CLOTH panel to the belt, a soft fold either
        // side of the middle. STEEL at the flanks, though the body's own arms cover nearly all of
        // it - the head and arms leave about a dozen texels of chest, so it holds ONE chain.

        const int SurTabardTop = 46, SurTabardBottom = 4, SurTabardHalf = 16;

        static string[] _surTabardRows;
        static string[] SurTabardRows => _surTabardRows ??=
            PaintField(-SurTabardHalf, SurTabardHalf, SurTabardBottom, SurTabardTop, SurTabardTexel);

        /// <summary>The cloth panel's half-width: straight over the chest, narrowing to the waist.</summary>
        static float SurClothHalf(float y) => y >= 24f ? 8.5f : 8.5f - (24f - y) * 0.07f;

        static float SurGorgetBottom(float ax) => 35.5f + ax * 0.12f;

        /// <summary>The silver yoke's lower edge: a point at the centre, rising to the cloth's edges.</summary>
        static float SurYokeBottom(float ax) => 31f + ax * 0.42f;

        /// <summary>
        /// A chain swag hanging from (+/-<see cref="SurChainEnd"/>, <paramref name="endY"/>) to its
        /// lowest point at the centre: how far (perpendicular, roughly) a texel sits above the chain.
        /// </summary>
        static float SurSwag(float x, float y, float endY, float lowY)
        {
            float t = x / SurChainEnd;
            float curve = lowY + (endY - lowY) * t * t;
            float slope = 2f * (endY - lowY) * x / (SurChainEnd * SurChainEnd);
            return (y - curve) / Mathf.Sqrt(1f + slope * slope);
        }

        const float SurChainEnd = 12.5f;

        /// <summary>
        /// The chain: ONE swag, hanging below the yoke's point. Two (and an engraved row on the
        /// yoke) were three rows of dots in the dozen texels of chest the arms and head leave.
        /// </summary>
        const float SurChainEndY = 33f, SurChainLowY = 25.5f;

        static char SurTabardTexel(int ix, int iy)
        {
            const string S = SurSteel, C = SurCloth;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 5f, -6f);

            float top = 40.5f - Mathf.Max(0f, ax - 6f) * 0.45f;
            float half = x < 0f ? (iy >= 26 ? 13.5f : 13f) : TalBodyEdge(y);
            if (y >= top || ax > half) return '.';

            // ---- the gorget ----
            float gorget = SurGorgetBottom(ax);
            if (y >= gorget)
            {
                if (y < gorget + 1f) return RampChar(S, 5);                              // the rolled lower edge
                return RampChar(S, (y >= top - 1.5f ? 2 : 3) + Mathf.Max(side, 0));
            }

            // ---- the chain, over everything below the gorget ----
            bool chainShadow = false;
            if (ax <= SurChainEnd + 0.5f)
            {
                float d = SurSwag(x, y, SurChainEndY, SurChainLowY);
                if (Mathf.Abs(d) < 0.75f)
                    // Links in MID steel, one dark texel in three: lit steel is the white cloth's
                    // own value and vanished into it, leaving only the dark texels as dots.
                    return RampChar(S, WrapMod(ix, 3) switch { 0 => 4, 1 => 3, _ => 1 });
                if (d < -0.75f && d > -1.8f) chainShadow = true;
            }

            float clothHalf = SurClothHalf(y);
            if (ax < clothHalf)
            {
                // ---- the silver yoke ----
                float yoke = SurYokeBottom(ax);
                if (y >= yoke)
                {
                    if (y >= gorget - 1f) return RampChar(S, 1);                          // under the gorget's lip
                    if (y < yoke + 1f) return RampChar(S, 2);                             // its lower edge
                    if (ax < 1.5f && y < yoke + 3f)                                       // the clasp at its point
                        return RampChar(S, x > 0f ? 5 : 4);
                    return RampChar(S, x > 0f ? 4 : 3);
                }

                // ---- the cloth ----
                if (y >= yoke - 1f) return RampChar(C, 2);                                // the yoke's shadow
                if (chainShadow) return RampChar(C, 2);
                if (ax > clothHalf - 1f) return RampChar(C, x > 0f ? 3 : 1);              // its edges turn away
                // Two soft folds below the chest: a shaded plane left of the middle, a lit one right.
                if (y < 24f && x > -3.5f && x < -1.5f) return RampChar(C, 2);
                if (y < 24f && x > 1.5f && x < 2.5f) return RampChar(C, 4);
                return RampChar(C, 3 + side);
            }

            // ---- the steel flanks ----
            if (chainShadow) return RampChar(S, 1);
            if (ax < clothHalf + 1f) return RampChar(S, 1);                               // the cloth's edge shadow
            if (iy == 20 || iy == 13) return RampChar(S, 1);                              // lame edges
            if (iy == 19 || iy == 12) return RampChar(S, 4 + Mathf.Max(side, 0));
            return RampChar(S, 3 + side);
        }

        // ------------------------------------------------------------------ the pauldrons (Shoulders)
        //
        // After the user's templar reference, cross left off: ONE big rounded plate over the
        // shoulder, high and smooth, its inner edge sloping down the chest and its outer side
        // rounding over the upper arm; a dark steel lame peeking out under it. The plate is SOLID
        // REACTIVE METAL - its face in the cloth ramp (ClothDye.Undyed), so it takes the worn cape's
        // colour with the tabard - shaded as a DOME, with a hard specular toward the light (glow
        // straight against base is what reads as metal; cloth is shaded soft).
        //
        // The SILVER trim (steel, never dyed) is THICKER at the top, the extra on the side leading
        // to the neck: one texel at the outer edge and the bottom, growing to three along the crown's
        // inner half and down the inner edge (SurTrimWidth). A thick band is a raised rim: lit at
        // its outer edge, a dark texel where it meets the face. A ring at the inner end, over the
        // place the tabard's chain runs under it.
        //
        // Both shoulders are the same piece (not Lopsided): the near one is the far one mirrored,
        // lit from the inside. (Outward, height above the chin) texels from the joint.

        const int SurPauldronMin = -4, SurPauldronMax = 20, SurPauldronRowCount = 26;
        const int SurPauldronRise = 8;

        static float SurPauldronCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((SurPauldronMin + SurPauldronMax) * 0.5f);
        static float SurPauldronY
            => PlateY(SurPauldronFar) + PrimitiveCharacterRig.Proportions.Cells(SurPauldronRise);

        const float SurDomeO = 7f, SurDomeH = -4f, SurDomeRx = 11.5f, SurDomeRy = 10.5f;

        /// <summary>The plate: an ellipse, its inner-lower side cut by a slope down the chest.</summary>
        static bool SurDomeIn(float o, float h)
        {
            float dx = (o - SurDomeO) / SurDomeRx, dy = (h - SurDomeH) / SurDomeRy;
            return dx * dx + dy * dy < 1f && h > -7.5f - 0.42f * (o + 2f);
        }

        /// <summary>The lame under it: the plate's own outline dropped three texels, outer side only.</summary>
        static bool SurLameIn(float o, float h) => o > 3f && SurDomeIn(o, h + 3f) && !SurDomeIn(o, h);

        /// <summary>The silver trim's width at <paramref name="o"/>: one outside, three toward the neck.</summary>
        static int SurTrimWidth(float o) => o > 10f ? 1 : o > 6f ? 2 : 3;

        /// <summary>The ring where the chain goes under: on the plate's inner end.</summary>
        static readonly Vector2 SurChainRing = new(-1.5f, -6.5f);

        static string[] _surPauldronFar, _surPauldronNear;
        static string[] SurPauldronFar => _surPauldronFar ??=
            PaintField(SurPauldronMin, SurPauldronMax, 0, SurPauldronRowCount, (ix, iy) =>
            {
                int r = SurPauldronRowCount - 1 - iy;
                return SurPauldronTexel(ix + 0.5f, SurPauldronRise - r - 0.5f, litOut: true);
            });
        static string[] SurPauldronNear => _surPauldronNear ??=
            PaintField(SurPauldronMin, SurPauldronMax, 0, SurPauldronRowCount, (ix, iy) =>
            {
                int o = SurPauldronMin + SurPauldronMax - 1 - ix;             // outward is -X
                int r = SurPauldronRowCount - 1 - iy;
                return SurPauldronTexel(o + 0.5f, SurPauldronRise - r - 0.5f, litOut: false);
            });

        /// <param name="litOut">The light is on the OUTER side - the far (+X) shoulder.</param>
        static char SurPauldronTexel(float o, float h, bool litOut)
        {
            const string S = SurSteel, D = SurDark, M = SurCloth;              // silver, dark steel, dyed metal
            float toLight = litOut ? o - SurDomeO : SurDomeO - o;              // + toward the light
            int side = toLight > 4f ? 1 : toLight < -5f ? -1 : 0;

            // ---- the ring, over the plate's inner end ----
            float rx = o - SurChainRing.x, ry = h - SurChainRing.y;
            if (rx * rx + ry * ry < 2.3f) return RampChar(S, ry > 0f ? 5 : 2);

            // ---- the lame peeking out underneath ----
            if (SurLameIn(o, h))
            {
                if (SurDomeIn(o, h + 1f)) return RampChar(D, 1);                         // the plate's shadow
                if (!SurLameIn(o, h - 1f)) return RampChar(D, 2);                        // lower edge
                return RampChar(D, 3 + Mathf.Max(side, 0));
            }

            if (!SurDomeIn(o, h)) return '.';

            // ---- the silver trim: depth in from the edge, measured up (the crown) and in
            //      (toward the neck, upper half only) out to the trim's width there, one texel
            //      everywhere else - the slope down the chest stays thin ----
            int w = SurTrimWidth(o), depth = 0;
            for (int k = 1; k <= w && depth == 0; k++)
                if (!SurDomeIn(o, h + k) || (o < SurDomeO && h > SurDomeH && !SurDomeIn(o - k, h))) depth = k;
            if (depth == 0 && (!SurDomeIn(o, h - 1f) || !SurDomeIn(o + 1f, h) || !SurDomeIn(o - 1f, h)))
            {
                bool under = !SurDomeIn(o, h - 1f);
                return RampChar(S, under ? (side >= 0 ? 3 : 2) : (side > 0 ? 4 : 3));    // the thin trim
            }
            if (depth > 0)
            {
                if (depth == w && w > 1) return RampChar(S, 2);                          // where the rim meets the face
                if (depth == 1) return RampChar(S, side >= 0 ? 5 : 4);                   // the rim's lit edge
                return RampChar(S, 3 + Mathf.Max(side, 0));
            }

            // ---- the face: a dome lit from the light side and above ----
            float nx = (o - SurDomeO) / SurDomeRx * (litOut ? 1f : -1f);
            float ny = (h - SurDomeH) / SurDomeRy;
            float lit = nx * 0.55f + ny * 0.83f;
            float sx = nx - 0.42f, sy = ny - 0.55f;
            if (sx * sx * 4f + sy * sy * 6f < 0.10f) return RampChar(M, 5);             // the specular
            if (!SurDomeIn(o, h - 2f)) return RampChar(M, 2);                            // turning under
            return RampChar(M, lit > 0.30f ? 4 : lit > -0.20f ? 3 : lit > -0.55f ? 2 : 1);
        }

        // ------------------------------------------------------------------ the gauntlets (Gloves)
        //
        // ARM-local: 0 at the shoulder, the elbow cut at -14, the wrist at -22, the hand below.
        // Row -12 is repeated by the rig to lengthen the upper arm, so only plain cloth sits there.
        // A dark padded sleeve under the pauldron, a round steel COUTER drawn below the elbow cut
        // (so it bends with the forearm - Nocturne's finding), a steel VAMBRACE with a lit ridge,
        // a flared cuff, and a dark plated HAND, the fingers split by shade.

        const int SurArmTop = 0, SurArmBottom = -34;

        static string[] _surArmRows;
        static string[] SurArmRows => _surArmRows ??=
            PaintField(-6, 6, SurArmBottom, SurArmTop, SurArmTexel);

        static char SurArmTexel(int ix, int iy)
        {
            const string S = SurSteel, D = SurDark;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2f);

            // ---- the sleeve ----
            if (iy >= -14)
            {
                if (ax > 4.5f) return '.';
                return RampChar(D, 3 + side);
            }

            // ---- the couter ----
            {
                float dx = (x - 0.3f) / 4.8f, dy = (y + 17f) / 3.2f;
                float r2 = dx * dx + dy * dy;
                if (r2 < 1f)
                {
                    if (dy < -0.55f) return RampChar(S, 1);
                    if (dx > -0.1f && dx < 0.45f && dy > 0f && dy < 0.6f) return RampChar(S, 5);
                    return RampChar(S, 3 + side);
                }
            }

            // ---- the vambrace ----
            if (iy >= -27)
            {
                float half = 4.6f - (-20f - y) * 0.06f;
                if (ax > half) return '.';
                if (ix == 1) return RampChar(S, 5);                                     // the ridge
                if (ix == 0) return RampChar(S, 2);
                return RampChar(S, 3 + side);
            }

            // ---- the cuff ----
            if (iy >= -29)
            {
                if (ax > 5.2f) return '.';
                return RampChar(S, iy == -28 ? 4 + Mathf.Max(side, 0) : 1);
            }

            // ---- the hand ----
            {
                float half = iy < -32 ? 3.5f : 4.5f;
                if (ax > half) return '.';
                if (iy == -30) return RampChar(D, 4 + Mathf.Max(side, 0));               // the knuckle plate
                if (ix == -2 || ix == 0 || ix == 2) return RampChar(D, 1);
                return RampChar(D, 3 + Mathf.Max(side, 0));
            }
        }

        // ------------------------------------------------------------------ the belt
        //
        // Torso-local, on the belt's band. ONE wide belt with a ROUND silver buckle - a domed
        // disc, lit up and right, a boss at its middle; not a ring (a ring round a small emblem
        // closes up into "$" at this size). A second, narrower belt SLUNG from the near hip down
        // across the far one, and a pouch hanging from it on the far hip (the near arm covers
        // the near hip, as on Errant).

        const int SurBeltTop = 14, SurBeltBottom = -12, SurBeltHalf = 18;

        static string[] _surBeltRows;
        static string[] SurBeltRows => _surBeltRows ??=
            PaintField(-SurBeltHalf, SurBeltHalf, SurBeltBottom, SurBeltTop, SurBeltTexel);

        static readonly Vector2 SurSlungFrom = new(-14f, 6f), SurSlungTo = new(11f, -1f);

        static char SurBeltTexel(int ix, int iy)
        {
            const string S = SurSteel, D = SurDark;
            float x = ix + 0.5f, y = iy + 0.5f;
            int side = LitSide(x, 8f, -9f);
            float farEdge = TalBodyEdge(y) + 0.5f;

            // ---- the buckle ----
            {
                float dx = (x - 0.5f) / 3.4f, dy = (y - 8f) / 3.8f;
                float r2 = dx * dx + dy * dy;
                if (r2 < 1f)
                {
                    if (r2 > 0.6f) return RampChar(S, dy + dx * 0.5f > 0f ? 5 : 1);         // the rim
                    if (Mathf.Abs(dx) < 0.32f && Mathf.Abs(dy) < 0.3f)
                        return RampChar(S, dy > 0f ? 5 : 2);                                 // the boss
                    return RampChar(S, dx > 0f ? 4 : 3);
                }
            }

            // ---- the wide belt ----
            if (iy >= 5 && iy < 11 && x >= -15.5f && x <= farEdge)
                return RampChar(D, iy switch { 10 => 4, 5 => 1, _ => 3 } + side);

            // ---- the pouch, hung from the slung belt on the far hip ----
            if (ix >= 7 && ix < 11 && iy >= -7 && iy < 1)
            {
                if (iy >= -3)                                                           // the flap
                {
                    if (ix == 8 && iy == -2) return RampChar(S, 5);                     // its stud
                    return RampChar(D, iy == 0 ? 4 : iy == -3 ? 1 : 3);
                }
                return RampChar(D, (ix >= 9 ? 3 : 2) - (iy == -7 ? 1 : 0));
            }

            // ---- the slung belt ----
            {
                var d = (SurSlungTo - SurSlungFrom).normalized;
                var n = new Vector2(-d.y, d.x);
                var p = new Vector2(x, y) - SurSlungFrom;
                float across = Vector2.Dot(p, n), along = Vector2.Dot(p, d);
                if (Mathf.Abs(across) < 1.1f && along > 0f && along < (SurSlungTo - SurSlungFrom).magnitude
                    && x <= farEdge)
                {
                    if (WrapMod(Mathf.RoundToInt(along), 6) == 3 && Mathf.Abs(across) < 0.6f)
                        return RampChar(S, 4);                                          // rivets
                    return RampChar(D, across > 0.3f ? 4 : 2);
                }
            }
            return '.';
        }

        // ------------------------------------------------------------------ the tassets (Legs)
        //
        // THE SKIRT, TORSO-local on RigLayer.Tasset: the tabard's cloth from under the belt to
        // below the knee, as wide at the top as the tabard is at the waist, its hem TORN - a ragged
        // edge, one big V tear on the lit side, and one long tail hanging lower on the near side.
        // The tears are big on purpose (the Hellspawn Cape's finding: the outline closes small
        // ones into runes). Either side of it a STEEL FAULD of two lames over the hip.
        //
        // THE CUISSES, LEG-local on LegsFront/LegsBack: three lames down the thigh under the
        // fauld, and a round knee COP with a lame under it - the cop is cut at the knee with the
        // leg (PaintKneeSplit), the greave starts below its lame.

        const int SurSkirtTop = 12, SurSkirtBottom = -40, SurSkirtHalf = 16;

        static string[] _surSkirtRows;
        static string[] SurSkirtRows => _surSkirtRows ??=
            PaintField(-SurSkirtHalf, SurSkirtHalf, SurSkirtBottom, SurSkirtTop, SurSkirtTexel);

        /// <summary>The torn hem's height at x: a small zigzag, a V torn up on the lit side, a long
        /// tail down on the near side.</summary>
        static float SurHem(float x)
        {
            float hem = -27f + Mathf.Abs(WrapMod(Mathf.FloorToInt(x + 20f), 3) - 1f) * 1.3f;
            float tail = 1f - Mathf.Abs(x + 4.5f) / 2.5f;
            if (tail > 0f) hem -= tail * 9f;
            float tear = 1f - Mathf.Abs(x - 3.5f) / 2.5f;
            if (tear > 0f) hem += tear * 6f;
            return hem;
        }

        static char SurSkirtTexel(int ix, int iy)
        {
            const string S = SurSteel, C = SurCloth;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 3f, -4f);
            if (y >= 11f) return '.';

            // ---- the cloth ----
            float half = SurClothHalf(10f) - (10f - y) * 0.03f;
            if (ax < half)
            {
                float hem = SurHem(x);
                if (y < hem) return '.';
                if (y < hem + 1f) return RampChar(C, 2);                                  // the frayed edge
                if (y >= 3.5f && y < 5f) return RampChar(C, 2);                           // the belt's shadow
                if (ax > half - 1f) return RampChar(C, x > 0f ? 3 : 1);
                if (x > -3.5f && x < -1.5f) return RampChar(C, 2);                        // the folds, continued
                if (x > 1.5f && x < 2.5f) return RampChar(C, 4);
                return RampChar(C, 3 + side);
            }

            // ---- the faulds ----
            for (int k = 0; k < 2; k++)
            {
                float lTop = 4f - k * 5.5f, lBottom = lTop - 6f;
                float outer = 13.5f + (4f - y) * 0.15f;
                if (y >= lTop || y < lBottom || ax > outer) continue;
                if (ax > outer - 1.5f && y < lBottom + 1.5f) return '.';                 // the rounded corner
                if (y >= lTop - 1f) return RampChar(S, 4 + Mathf.Max(side, 0));
                if (y < lBottom + 1f) return RampChar(S, 1);
                if (ax < half + 1f) return RampChar(S, 1);                                // under the cloth's edge
                return RampChar(S, 3 + side);
            }
            return '.';
        }

        const int SurCuisseTop = -4, SurCuisseBottom = -32, SurCuisseHalf = 6;

        static string[] _surCuisseRows;
        static string[] SurCuisseRows => _surCuisseRows ??=
            PaintField(-SurCuisseHalf, SurCuisseHalf, SurCuisseBottom, SurCuisseTop, SurCuisseTexel);

        static bool SurKneeCopIn(float x, float y)
        {
            float dx = x / 4.8f, dy = (y + 24f) / 3.6f;
            return dx * dx + dy * dy < 1f;
        }

        static char SurCuisseTexel(int ix, int iy)
        {
            const string S = SurSteel;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2f);

            // ---- the knee cop ----
            if (SurKneeCopIn(x, y))
            {
                if (!SurKneeCopIn(x, y + 1f)) return RampChar(S, x >= 0f ? 5 : 4);
                if (!SurKneeCopIn(x, y - 1f) || !SurKneeCopIn(x - 1f, y) || !SurKneeCopIn(x + 1f, y))
                    return RampChar(S, 1);
                if (ix == 0) return RampChar(S, 5);                                     // the ridge
                if (ix == -1) return RampChar(S, 2);
                return RampChar(S, 3 + side);
            }
            // ---- the lame under it ----
            if (iy >= -30 && iy < -27)
            {
                if (ax > 4.2f) return '.';
                return RampChar(S, iy == -28 ? 4 + Mathf.Max(side, 0) : iy == -30 ? 1 : 3);
            }

            // ---- the cuisse: three lames, each over the next ----
            if (iy >= -21 && iy < -5)
            {
                float half = 4.6f - (-5f - y) * 0.02f;
                if (ax > half) return '.';
                int row = WrapMod(-6 - iy, 5);
                if (row == 0) return RampChar(S, 4 + Mathf.Max(side, 0));               // lit top
                if (row == 4) return RampChar(S, 1);                                    // edge over the next
                return RampChar(S, 3 + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the greaves (Boots)
        //
        // LEG-local, the sole at -48. A dark GREAVE from under the knee cop's lame to the ankle,
        // a lit ridge down the shin; a SABATON of two lames over the foot.

        const int SurGreaveTop = -30, SurGreaveBottom = -48, SurGreaveHalf = 6;

        static string[] _surGreaveRows;
        static string[] SurGreaveRows => _surGreaveRows ??=
            PaintField(-SurGreaveHalf, SurGreaveHalf, SurGreaveBottom, SurGreaveTop, SurGreaveTexel);

        static char SurGreaveTexel(int ix, int iy)
        {
            const string D = SurDark;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2f);

            // ---- the sabaton ----
            if (iy < -42)
            {
                if (ax > 5.5f) return '.';
                if (iy == -48) return RampChar(D, 0);                               // the sole
                if (iy == -43 || iy == -46) return RampChar(D, 4 + Mathf.Max(side, 0));  // two lames
                if (iy == -44) return RampChar(D, 1);
                return RampChar(D, 3 + side);
            }

            // ---- the greave ----
            float half = 4.6f - (-30f - y) * 0.04f;
            if (ax > half) return '.';
            if (iy == -31) return RampChar(D, 4 + Mathf.Max(side, 0));                   // its rolled top
            if (ix == 1) return RampChar(D, 5);                                         // the shin ridge
            if (ix == 0) return RampChar(D, 2);
            return RampChar(D, 3 + side);
        }
    }
}
