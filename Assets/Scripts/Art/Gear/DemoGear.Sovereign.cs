using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Sovereign: white and gold over blue cloth
    //
    // After the user's reference (a lacquered court uniform: tiered shoulder plates, a chevron
    // yoke on the chest, a standing collar, a gold belt with a round buckle, hip plates over cloth
    // panels), RECOLOURED on the user's instruction: the cloth underneath is BLUE and the armour
    // over it WHITE with GOLD trim. Named as materials - white enamel, blue cloth, gold - and
    // Diamond, power 0, like every reference-built cosmetic set.
    //
    //   sovereign_cuirass    Torso      white breastplate under a two-tier chevron yoke, each tier
    //                                   edged in gold; the blue standing collar above it
    //   sovereign_pauldrons  Shoulders  two stepped plates, the lower one standing further out,
    //                                   each with a gold hem and outer edge
    //   sovereign_belt       Belt       a gold band and a round buckle - blue enamel, a white
    //                                   crescent. The game's own mark, not the reference's
    //   sovereign_tassets    Legs       white hip plates over blue split-skirt panels (Tasset,
    //                                   torso-parented), and blue trousers (LegsFront/Back)
    //   sovereign_sleeves    Gloves     blue sleeves, shoulder to wrist, and a gold-rimmed bracer.
    //                                   The HAND is left bare - the undersuit's black shows, as the
    //                                   reference's bare hand does against its sleeve
    //
    // THE GOLD IS ALWAYS AN EDGE. Every white plate is bounded by gold on the sides it shows, and
    // nothing gold sits in the middle of a plate - that is what makes the trim read as the plate's
    // rim rather than as stripes painted across it. Two texels thick (one layout cell), since at
    // one texel it is a single screen pixel in the arena and breaks up on every diagonal.
    //
    // Fields in each joint's own frame (DemoGear.Fields.cs), lit from +X like every piece here.
    public static partial class DemoGear
    {
        const string SovWhite = "ksdblh", SovBlue = "KSDBLH", SovGold = "123456";

        static void AddSovereign(List<GearItem> items)
        {
            // A cool white, off the top of the range for Conclave's reason: a base at 1.0 has
            // nowhere to put its Light and Glow. A softer shade than the default, or Dark lands at
            // 0.60 - a different grey rather than the same white turned from the light.
            var white = new Palette.Ramp(new Color(0.86f, 0.87f, 0.90f), shade: 0.22f);
            // Royal blue, deep enough that the white plates stand off it.
            var blue = new Palette.Ramp(new Color(0.20f, 0.33f, 0.66f), shade: 0.32f);
            // Saturated rather than pale: gold's job here is to separate from WHITE, and a pale
            // gold beside a white plate is the same value in a different hue.
            var gold = new Palette.Ramp(new Color(0.84f, 0.62f, 0.20f));
            var pal = Palette.Of(white, blue, gold);
            // The far limb and far shoulder. White moves least - see conclaveWhiteDark on how a
            // near-white darkened like a mid metal reads as a different material, not shadow.
            var far = Palette.Of(white.Scaled(0.92f), blue.Scaled(0.80f), gold.Scaled(0.88f));

            items.Add(Defends(Make("sovereign_cuirass", "Sovereign Cuirass", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.sovereign", SovCuirassRows, pal,
                       0f, FieldCentreCells(SovCuirassBottom, SovCuirassTop), ppu: BodyPpu)),
                // Light plate over cloth, built to move in - the reference is a fast fighter.
                DefensiveAbility.Dash));

            var pauldrons = Make("sovereign_pauldrons", "Sovereign Pauldrons", GearSlot.Shoulders, LootTier.Diamond, 0f,
                Pixels(RigLayer.Shoulders, "gear.pauldron.sovereign", SovPauldronNear, pal,
                       -ShoulderX - SovPauldronCentreCells, PlateY(SovPauldronNear), ppu: BodyPpu),
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.sovereign.dark", SovPauldronFar, far,
                       ShoulderX + SovPauldronCentreCells, PlateY(SovPauldronFar), ppu: BodyPpu));
            // The stepped plates HANG to the elbow, like the Shogun sode, so in the rest carry they
            // turn with the arm's whole raise - as a cap they jutted off the raised shoulder like a
            // shelf. See GearItem.HangsAlongArm.
            pauldrons.HangsAlongArm = true;
            items.Add(pauldrons);

            items.Add(Make("sovereign_belt", "Sovereign Belt", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.sovereign", SovBeltRows, pal,
                       0f, FieldCentreCells(SovBeltBottom, SovBeltTop), ppu: BodyPpu)));

            items.Add(Make("sovereign_tassets", "Sovereign Tassets", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.Tasset, "gear.legs.sovereign.tasset", SovTassetRows, pal,
                       0f, FieldCentreCells(SovTassetBottom, SovTassetTop), ppu: BodyPpu),
                Pixels(RigLayer.LegsFront, "gear.legs.sovereign", SovTrouserRows, pal,
                       0f, FieldCentreCells(SovTrouserBottom, SovTrouserTop), ppu: BodyPpu),
                Pixels(RigLayer.LegsBack, "gear.legs.sovereign.dark", SovTrouserRows, far,
                       0f, FieldCentreCells(SovTrouserBottom, SovTrouserTop), ppu: BodyPpu)));

            items.Add(Make("sovereign_sleeves", "Sovereign Sleeves", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesFront, "gear.gloves.sovereign", SovSleeveRows, pal,
                       0f, FieldCentreCells(SovSleeveBottom, SovSleeveTop), ppu: BodyPpu),
                Pixels(RigLayer.GlovesBack, "gear.gloves.sovereign.dark", SovSleeveRows, far,
                       0f, FieldCentreCells(SovSleeveBottom, SovSleeveTop), ppu: BodyPpu)));

            Dyeable(items, "sovereign_", DyeChannel.Of("Plate", DyeMaterial.Metal, white, 0.92f),
                    DyeChannel.Of("Cloth", DyeMaterial.Cloth, blue, 0.80f));
        }

        // ------------------------------------------------------------------ the cuirass (Torso)
        //
        // Torso-local. The yoke is two chevron plates stacked under the collar, each edged in gold
        // along its lower V, and the breastplate hangs under the lower V to the waist. The V is
        // what carries the reference's read - two lines converging on the sternum - and it is
        // drawn as the plates' own edges, not as lines laid over one plate.

        const int SovCuirassTop = 44, SovCuirassBottom = 6, SovCuirassHalf = 16;
        const int SovYokeTop = 38;

        static string[] _sovCuirassRows;
        static string[] SovCuirassRows => _sovCuirassRows ??=
            PaintField(-SovCuirassHalf, SovCuirassHalf, SovCuirassBottom, SovCuirassTop, SovCuirassTexel);

        /// <summary>The upper yoke tier's lower edge, by distance from the centre line.</summary>
        static float SovYokeUpper(float ax) => 31f + ax * 0.40f;
        /// <summary>The lower yoke tier's lower edge - where the breastplate begins.</summary>
        static float SovYokeLower(float ax) => 24f + ax * 0.50f;

        static char SovCuirassTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 8f, -9f);

            // ---- the standing collar, blue, over the yoke's top ----
            if (iy >= 35 && iy < 43 && ax <= 8f)
            {
                if (ax < 1f && iy >= 39) return 'S';                     // the collar's front opening
                int t = 3 + LitSide(x, 4f, -5f) + (iy == 42 ? 1 : 0);
                return RampChar(SovBlue, Mathf.Min(t, 4));
            }

            // ---- the yoke ----
            float lower = SovYokeLower(ax);
            if (iy < SovYokeTop && y >= lower && ax <= (iy >= SovYokeTop - 1 ? 13f : 15f))
            {
                if (iy == SovYokeTop - 1) return RampChar(SovGold, 4 + side);          // top rim
                if (y < lower + 2f) return RampChar(SovGold, 3 + side);                 // lower V
                float upper = SovYokeUpper(ax);
                if (y >= upper && y < upper + 2f) return RampChar(SovGold, 4 + side);   // upper V
                // The row under each gold edge catches the light; the plate falls away below it.
                bool underRim = (y >= upper + 2f && y < upper + 3f) || iy == SovYokeTop - 2;
                return RampChar(SovWhite, 3 + side + (underRim ? 1 : 0));
            }

            // ---- the breastplate ----
            float half = iy > 16 ? 14f : 13f + (iy - 6) * 0.1f;
            if (y < lower && ax <= half)
            {
                // A ridge down the centre: lit on one side of it, shaded on the other.
                if (ix == -1) return RampChar(SovWhite, 4 + Mathf.Min(side, 0));
                if (ix == 0) return RampChar(SovWhite, 2);
                if (ax > half - 1f) return RampChar(SovWhite, 2 + side);               // the plate turning away
                return RampChar(SovWhite, 3 + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the pauldrons (Shoulders)
        //
        // Two plates, stepped: a domed upper plate, and a bigger one tucked under it that reaches
        // further out and lower, its hem sloping DOWN going outward. Gold hems and outer edges on
        // both. Painted in OUTWARD offsets from the joint, like the Shogun sode, and mirrored for
        // the far side, because the step has a direction. Top edge on the chin (PlateY); 18 rows
        // puts the lower plate's hem on the elbow.
        //
        // WHITE, NOT GREY: the shadow tone is kept to the outermost rim. The first pass shaded the
        // whole outer half, and a white plate that is mostly 'd' reads as grey steel beside the
        // cuirass rather than as the same enamel turned from the light.

        const int SovPauldronRows = 18, SovPauldronMin = -7, SovPauldronMax = 13;
        static float SovPauldronCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((SovPauldronMin + SovPauldronMax) * 0.5f);

        static string[] _sovPauldronNear, _sovPauldronFar;
        static string[] SovPauldronNear => _sovPauldronNear ??= SovPauldron(outwardIsPlusX: false);
        static string[] SovPauldronFar => _sovPauldronFar ??= SovPauldron(outwardIsPlusX: true);

        static string[] SovPauldron(bool outwardIsPlusX)
            => PaintField(SovPauldronMin, SovPauldronMax, 0, SovPauldronRows, (ix, iy) =>
            {
                int o = outwardIsPlusX ? ix : SovPauldronMin + SovPauldronMax - 1 - ix;
                return SovPauldronTexel(o, SovPauldronRows - 1 - iy, outwardIsPlusX);
            });

        /// <param name="o">Outward offset from the joint's column, in texels.</param>
        /// <param name="r">Row from the top.</param>
        static char SovPauldronTexel(int o, int r, bool outwardIsPlusX)
        {
            float oc = o + 0.5f, rc = r + 0.5f;

            // Toward torso +X is toward the light: the near plate's inner edge, the far plate's outer.
            float towardLight = outwardIsPlusX ? oc : -oc;
            int side = towardLight > 6f ? 1 : towardLight < -9f ? -1 : 0;

            // ---- the upper plate: a dome over the shoulder ----
            float aInner = -6f, aOuter = 8.5f + rc * 0.25f;
            if (r == 0) { aInner = -3f; aOuter = 5f; }
            else if (r == 1) { aInner = -5f; aOuter = 7.5f; }
            float aBottom = 8.5f + (oc + 6f) * 0.08f;
            if (rc < aBottom && oc >= aInner && oc <= aOuter)
            {
                if (rc >= aBottom - 2f || oc > aOuter - 2f) return RampChar(SovGold, 4 + side);
                int dome = r <= 1 ? 2 : r <= 3 ? 1 : 0;                       // lit crown, falling away
                return RampChar(SovWhite, Mathf.Min(3 + side + dome, 5));
            }

            // ---- the lower plate: wider, lower, its hem sloping out and down ----
            float bInner = -5f, bOuter = Mathf.Min(aOuter + 2.5f, 12.5f);
            float bBottom = aBottom + 5f + (oc + 5f) * 0.14f;
            if (rc < bBottom && rc >= aBottom - 1f && oc >= bInner && oc <= bOuter)
            {
                if (rc >= bBottom - 2f || oc > bOuter - 2f) return RampChar(SovGold, 3 + side);
                // Its first row sits in the upper plate's shadow, the next catches the light.
                int under = rc < aBottom + 1f ? -1 : rc < aBottom + 2f ? 1 : 0;
                return RampChar(SovWhite, Mathf.Min(3 + side + under, 5));
            }
            return '.';
        }

        // ------------------------------------------------------------------ the belt
        //
        // Torso-local, on BeltY's band so it sits where every belt does. The buckle is bigger than
        // the band on purpose, like the reference's: a disc standing proud of the strap reads as a
        // buckle; one flush with it reads as a pattern on the belt.

        const int SovBeltTop = 12, SovBeltBottom = 0, SovBeltHalf = 16;
        const float SovBuckleX = 3f, SovBuckleY = 6f;

        static string[] _sovBeltRows;
        static string[] SovBeltRows => _sovBeltRows ??=
            PaintField(-SovBeltHalf, SovBeltHalf, SovBeltBottom, SovBeltTop, SovBeltTexel);

        static char SovBeltTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f;

            // ---- the buckle ----
            float dx = x - SovBuckleX, dy = y - SovBuckleY;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            if (r < 5.3f)
            {
                if (r >= 3.6f) return RampChar(SovGold, 4 + (dy > 1.5f ? 1 : dy < -2f ? -1 : 0));
                if (r >= 3.1f) return RampChar(SovGold, 2);                               // the bezel's inner lip
                // A white crescent on blue enamel: inside one circle, outside an offset one.
                float cx = dx + 0.4f, cy = dy;
                float ex = dx - 0.9f, ey = dy - 0.7f;
                if (cx * cx + cy * cy < 2.6f * 2.6f && ex * ex + ey * ey > 2.1f * 2.1f)
                    return RampChar(SovWhite, dy > 0f ? 5 : 4);
                return RampChar(SovBlue, dy > 1f ? 4 : 3);
            }

            // ---- the band ----
            if (iy >= 3 && iy < 10)
            {
                float half = (iy == 3 || iy == 9) ? 15f : 16f;
                if (Mathf.Abs(x) > half) return '.';
                int t = iy switch { 9 => 5, 3 => 2, 4 => 3, _ => 4 };
                return RampChar(SovGold, t + LitSide(x, 11f, -12f));
            }
            return '.';
        }

        // ------------------------------------------------------------------ the tassets (Legs)
        //
        // TORSO-local on RigLayer.Tasset, like gold_greaves: the skirt and its plates hang from the
        // belt and do not travel with either thigh. A white plate over each hip - two lames,
        // flaring, each hem sloping down going outward like the pauldrons' - over blue cloth in
        // two panels, SPLIT up the front between the legs.
        //
        // The split is an OPEN inverted V, not a seam. A first pass hung a third, pointed panel in
        // the middle between two seam lines, and on the body it read as a bulge at the crotch
        // rather than as cloth. Open, the navy legs show through it and the skirt reads as what it
        // is. It needs no clearance from the outline: where the gap is narrower than the stroke it
        // closes to a dark line, which is the slit continuing up to its apex.
        //
        // The first plates were square and flush, and read as a pair of white shorts. What makes
        // them hip armour is the FLARE and the sloped, stepped hems - the same move the pauldrons
        // make, so the set has one silhouette at the shoulder and the hip.
        //
        // KNEE LENGTH, and no longer: this body's legs are short against its torso, and a skirt
        // hemmed below the knee (the first pass, at -34) left the figure standing on its ankles.
        //
        // The trousers are the leg's own layers, leg-local, so they stride. NAVY, a step below the
        // skirt's blue: at the same blue the skirt and the legs merged into one pair of culottes.

        const int SovTassetTop = 6, SovTassetBottom = -28, SovTassetHalf = 20;

        static string[] _sovTassetRows;
        static string[] SovTassetRows => _sovTassetRows ??=
            PaintField(-SovTassetHalf, SovTassetHalf, SovTassetBottom, SovTassetTop, SovTassetTexel);

        static char SovTassetTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 10f, -12f);

            // ---- the hip plates ----
            const float plateTop = 2f, plateInner = 2.5f;
            float flare = Mathf.Clamp01((plateTop - y) / 14f);
            float plateOuter = 13f + flare * 5f;
            float split = -3f - (ax - plateInner) * 0.12f;                     // the upper lame's hem
            float hem = -11f - (ax - plateInner) * 0.20f;                      // the lower lame's hem
            if (ax >= plateInner && ax <= plateOuter && y >= hem)
            {
                if (y < hem + 2f || (y >= split && y < split + 2f)) return RampChar(SovGold, (y < hem + 1f ? 3 : 4) + side);
                if (ax > plateOuter - 1.5f || ax < plateInner + 1f) return RampChar(SovGold, 4 + side);
                // The lower lame's first row is in the upper one's shadow.
                int under = y < split && y >= split - 1f ? -1 : y >= split + 2f && y < split + 3f ? 1 : 0;
                return RampChar(SovWhite, Mathf.Min(3 + side + under, 5));
            }

            // ---- the cloth panels ----
            float fall = Mathf.Clamp01((SovTassetTop - y) / 30f);
            float halfW = x < 0f ? 14f + fall * 6f : 14f + fall * 4f;
            if (ax > halfW) return '.';
            // The split, opening from its apex under the plates to the hem.
            const float splitApex = -8f;
            float gap = (splitApex - y) * 0.32f;
            if (y < splitApex && ax < gap) return '.';

            float clothHem = x < 0f ? -25f : -24f;                                   // the back panel longer
            if (y < clothHem) return '.';
            // The darker border runs round every edge the panel shows: hem, side, and the split.
            bool splitEdge = y < splitApex + 1f && ax < gap + 1.5f;
            if (y < clothHem + 2f || ax > halfW - 1f || splitEdge) return RampChar(SovBlue, 1);
            return RampChar(SovBlue, 3 + side);
        }

        const int SovTrouserTop = 2, SovTrouserBottom = -44, SovTrouserHalf = 5;

        static string[] _sovTrouserRows;
        static string[] SovTrouserRows => _sovTrouserRows ??=
            PaintField(-SovTrouserHalf, SovTrouserHalf, SovTrouserBottom, SovTrouserTop, SovTrouserTexel);

        /// <summary>Leg-local: 0 at the hip, the sole at -48. Navy, gathered in at the ankle.</summary>
        static char SovTrouserTexel(int ix, int iy)
        {
            float x = ix + 0.5f;
            bool ankle = iy < -38;
            if (ankle && Mathf.Abs(x) > 4f) return '.';
            if (ankle) return RampChar(SovBlue, iy == -39 ? 3 : 1);
            return RampChar(SovBlue, 2 + LitSide(x, 2f, -3f));
        }

        // ------------------------------------------------------------------ the sleeves (Gloves)
        //
        // ARM-local, on the arm pivot like every glove - 0 at the shoulder, the elbow line at -14
        // (where PaintElbowSplit cuts every glove), the wrist at -22, the hand below it. The upper
        // sleeve is plain cloth on purpose: the rig lengthens the upper arm by repeating the row
        // above the elbow, so anything drawn there would repeat into a stripe.

        const int SovSleeveTop = 0, SovSleeveBottom = -24, SovSleeveHalf = 7;

        static string[] _sovSleeveRows;
        static string[] SovSleeveRows => _sovSleeveRows ??=
            PaintField(-SovSleeveHalf, SovSleeveHalf, SovSleeveBottom, SovSleeveTop, SovSleeveTexel);

        static char SovSleeveTexel(int ix, int iy)
        {
            float x = ix + 0.5f;
            int side = LitSide(x, 3f, -3f);

            // The sleeve, shoulder to the bracer.
            if (iy >= -14 && iy < -1)
                return Mathf.Abs(x) > 6f ? '.' : RampChar(SovBlue, 3 + side);

            // The bracer: gold rims, a deeper blue body, a lit band at the wrist - flaring out.
            if (iy >= -22 && iy < -14)
            {
                float half = iy < -19 ? 7f : 6f;
                if (Mathf.Abs(x) > half) return '.';
                if (iy == -15) return RampChar(SovGold, 4 + side);
                if (iy == -22) return RampChar(SovGold, 3 + side);
                if (iy == -21) return RampChar(SovBlue, 4 + side);
                return RampChar(SovBlue, 2 + side);
            }
            return '.';
        }
    }
}
