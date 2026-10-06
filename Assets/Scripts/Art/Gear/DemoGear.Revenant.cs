using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Revenant: black plate over grey quilting
    //
    // After the user's reference (a hooded dark lord in scarred black armour): a respirator over
    // the lower face, broad stepped shoulder slabs, a chest bib carrying a control panel over a
    // ribbed, quilted torso, a plated belt, ribbed upper arms and lit bracers, quilted thighs with
    // ribbed tubes down their outer sides, black shin plates and boots. Named as materials -
    // glossy black plate, grey quilting, red lights - and Diamond, power 0.
    //
    // NO CLOAK, by the user's call: the reference's hooded cloak is black_hood, which already
    // exists, and the set is meant to be worn under it. The mask was placed to read inside that
    // hood's face window as well as bare-headed.
    //
    //   revenant_mask       Head       the respirator, eyes left bare (CoversFaceOnly)
    //   revenant_pauldrons  Shoulders  two stepped slabs, the lower reaching further out
    //   revenant_cuirass    Torso      the bib and its panel, over ribbed quilting, plated flanks
    //   revenant_belt       Belt       plates on a black strap, an angular buckle
    //   revenant_gauntlets  Gloves     ribbed upper arm, elbow plate, a bracer with its lights
    //   revenant_greaves    Legs       quilted thigh with its ribbed tube, knee plate, shin plate
    //   revenant_boots      Boots      armoured, a toe cap
    //
    // The reference's ribbed groin piece is deliberately left out - drawn at this size, a ribbed
    // shape between the legs reads as anatomy, not armour (the Sovereign skirt's lesson).
    //
    // BLACK THAT READS AS BLACK ARMOUR: a dark base with shadows placed below LIGHT (Palette.Undersuit's
    // reasoning, so the seams survive), and a glossy highlight well up the ramp - gloss is what
    // separates the plate from the black undersuit it is strapped over. The grey quilting is the
    // one mid value on the figure, and it is what the black is read against.
    public static partial class DemoGear
    {
        const string RevPlate = "ksdblh", RevQuilt = "KSDBLH", RevRed = "123456";

        static void AddRevenant(List<GearItem> items)
        {
            var plate = new Palette.Ramp(new Color(0.19f, 0.20f, 0.23f), lift: 0.30f, shade: 0.34f, line: 0.80f)
                .WithShadowsBelowLight(0.40f, 0.26f);
            var quilt = new Palette.Ramp(new Color(0.38f, 0.40f, 0.48f));
            var red = new Palette.Ramp(new Color(0.95f, 0.16f, 0.12f));
            var pal = Palette.Of(plate, quilt, red);
            // Far limbs and the far shoulder. The lights keep their colour - a lit LED does not
            // fall into shadow with the plate it is set in.
            var far = Palette.Of(plate.Scaled(0.85f), quilt.Scaled(0.80f), red);

            var mask = Make("revenant_mask", "Revenant Mask", GearSlot.Head, LootTier.Diamond, 0f,
                Pixels(RigLayer.HeadArmor, "gear.head.revenant", RevMaskRows, pal,
                       FieldCentreCells(RevMaskMinX, RevMaskMaxX), FieldCentreCells(RevMaskBottom, RevMaskTop),
                       ppu: BodyPpu, outline: false));
            // A respirator, not a helm: the eyes and everything above stay the character's own.
            mask.CoversFaceOnly = true;
            items.Add(mask);

            items.Add(Make("revenant_pauldrons", "Revenant Pauldrons", GearSlot.Shoulders, LootTier.Diamond, 0f,
                Pixels(RigLayer.Shoulders, "gear.pauldron.revenant", RevPauldronNear, pal,
                       -ShoulderX - RevPauldronCentreCells, PlateY(RevPauldronNear), ppu: BodyPpu),
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.revenant.dark", RevPauldronFar, far,
                       ShoulderX + RevPauldronCentreCells, PlateY(RevPauldronFar), ppu: BodyPpu)));

            items.Add(Defends(Make("revenant_cuirass", "Revenant Cuirass", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.revenant", RevCuirassRows, pal,
                       0f, FieldCentreCells(RevCuirassBottom, RevCuirassTop), ppu: BodyPpu)),
                // Heavy plate that stands its ground.
                DefensiveAbility.Barrier));

            items.Add(Make("revenant_belt", "Revenant Belt", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.revenant", RevBeltRows, pal,
                       0f, FieldCentreCells(RevBeltBottom, RevBeltTop), ppu: BodyPpu)));

            items.Add(Make("revenant_gauntlets", "Revenant Gauntlets", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesFront, "gear.gloves.revenant", RevGauntletRows, pal,
                       0f, FieldCentreCells(RevGauntletBottom, RevGauntletTop), ppu: BodyPpu),
                Pixels(RigLayer.GlovesBack, "gear.gloves.revenant.dark", RevGauntletRows, far,
                       0f, FieldCentreCells(RevGauntletBottom, RevGauntletTop), ppu: BodyPpu)));

            // Two grids: the ribbed tube runs down the OUTER side of each thigh, and the near leg's
            // outside is +X while the far leg's is -X.
            items.Add(Make("revenant_greaves", "Revenant Greaves", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.LegsFront, "gear.legs.revenant", RevGreaveFront, pal,
                       0f, FieldCentreCells(RevGreaveBottom, RevGreaveTop), ppu: BodyPpu),
                Pixels(RigLayer.LegsBack, "gear.legs.revenant.dark", RevGreaveBack, far,
                       0f, FieldCentreCells(RevGreaveBottom, RevGreaveTop), ppu: BodyPpu)));

            items.Add(Make("revenant_boots", "Revenant Boots", GearSlot.Boots, LootTier.Diamond, 0f,
                Pixels(RigLayer.BootsFront, "gear.boots.revenant", RevBootRows, pal,
                       0f, FieldCentreCells(RevBootBottom, RevBootTop), ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.revenant.dark", RevBootRows, far,
                       0f, FieldCentreCells(RevBootBottom, RevBootTop), ppu: BodyPpu)));

            Dyeable(items, "revenant_", DyeChannel.Of("Plate", DyeMaterial.Metal, plate, 0.85f),
                    DyeChannel.Of("Quilting", DyeMaterial.Cloth, quilt, 0.80f));
        }

        /// <summary>
        /// Scars and scratches: a sparse, fixed scatter of lit texels across a plate, the
        /// reference's battle wear. Hashed from position so it never moves between frames or builds.
        /// </summary>
        static bool RevScratch(int ix, int iy, int salt)
        {
            uint h = (uint)(ix * 73856093) ^ (uint)(iy * 19349663) ^ (uint)(salt * 83492791);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return h % 23 == 0;
        }

        // ------------------------------------------------------------------ the mask (Head)
        //
        // HEAD-local texels, MEASURED off the rendered head rather than assumed: skin spans x
        // -14..13 with the turned face looking toward +X, the eyes sit at x -7..-3 and 7..12 with
        // their lowest row at y 7.5, and the chin's last row is y 0.5. Seven texels of face between
        // the eyes and the chin is all a respirator gets.
        //
        // NO AUTO-OUTLINE. At this density the stroke is four texels thick, which is more than the
        // gap between the mask's top and the eyes - the first pass sat in the right place and its
        // outline alone blacked the eyes out. (conclave_mask has the same problem today.) So the
        // mask draws its own one-texel edge: the tone ramp's line on every texel that borders
        // nothing.
        //
        // A band across the lower face, a nose piece up to the eyes' line, a mouthpiece narrowing
        // to a snout under the chin, its front standing out past the profile - which is what reads
        // as a respirator from the side rather than a scarf - and a strap back over the ear.
        //
        // Under black_hood only the face window (about x -5..9) shows: the band, the nose piece,
        // the near light and the snout, which is enough.

        const int RevMaskMinX = -16, RevMaskMaxX = 18, RevMaskBottom = -6, RevMaskTop = 8;

        static string[] _revMaskRows;
        static string[] RevMaskRows => _revMaskRows ??=
            PaintField(RevMaskMinX, RevMaskMaxX, RevMaskBottom, RevMaskTop, RevMaskTexel);

        static char RevMaskTexel(int ix, int iy)
        {
            char c = RevMaskFill(ix, iy);
            if (c == '.' || char.IsDigit(c)) return c;
            // The mask's own edge, one texel - see above.
            bool edge = RevMaskFill(ix - 1, iy) == '.' || RevMaskFill(ix + 1, iy) == '.'
                     || RevMaskFill(ix, iy - 1) == '.' || RevMaskFill(ix, iy + 1) == '.';
            return edge ? RampChar(RevPlate, 0) : c;
        }

        static char RevMaskFill(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f;

            // The lights: one on each cheek, one on the snout.
            if ((ix >= -6 && ix < -4 && iy >= 3 && iy < 5) || (ix >= 10 && ix < 12 && iy >= 3 && iy < 5)
                || (ix >= 3 && ix < 5 && iy >= -4 && iy < -2))
                return RampChar(RevRed, iy == 4 || iy == -3 ? 5 : 4);

            // The strap, back over the ear.
            if (iy >= 3 && iy < 5 && ix >= -15 && ix < -9) return RampChar(RevPlate, 2);

            // Its front, standing out past the face's profile.
            if (iy >= -1 && iy < 5 && ix >= 13 && ix < 16) return RampChar(RevPlate, iy == 4 ? 5 : 4);
            // The nose piece, up to the eyes' line.
            if (iy >= 5 && iy < 8 && ix >= 0 && ix < 5) return RampChar(RevPlate, iy == 7 ? 4 : 3);
            // The band across the lower face.
            if (iy >= 1 && iy < 6 && x >= -10f && x <= 13f)
            {
                if (iy == 5) return RampChar(RevPlate, x > 6f ? 5 : 4);          // the rim, glossy
                return RampChar(RevPlate, x > 8f ? 4 : x < -5f ? 2 : 3);
            }
            // The mouthpiece, narrowing to a snout under the chin.
            float lo = -5f + (1f - y) * 1.1f, hi = 13f - (1f - y) * 0.9f;
            if (iy >= -5 && iy < 1 && x >= lo && x <= hi)
            {
                // The grille: two slits across it.
                if ((iy == -1 || iy == -3) && x > lo + 1.5f && x < hi - 1.5f) return RampChar(RevPlate, 1);
                return RampChar(RevPlate, x > 7f ? 3 : 2);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the pauldrons (Shoulders)
        //
        // Two thick slabs, stepped: the upper sits flat across the shoulder with a bevelled, glossy
        // top edge; the lower steps out and down beneath it. Their outer ends are CUT AT AN ANGLE
        // rather than rounded - the reference's shoulders are machined plate, and every other
        // pauldron here is a curve. Outward offsets, mirrored for the far side, top on the chin.
        //
        // ONE POINT, where the slabs meet. The outer ends share a single edge - out from the crown
        // to a tip at the seam, then back in to the hem. Each slab cut back on its own made two
        // tips, one above the other, and the pair read as a double-pointed shoulder (the user's
        // note).

        const int RevPauldronRows = 14, RevPauldronMin = -7, RevPauldronMax = 15;
        static float RevPauldronCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((RevPauldronMin + RevPauldronMax) * 0.5f);

        static string[] _revPauldronNear, _revPauldronFar;
        static string[] RevPauldronNear => _revPauldronNear ??= RevPauldron(outwardIsPlusX: false);
        static string[] RevPauldronFar => _revPauldronFar ??= RevPauldron(outwardIsPlusX: true);

        static string[] RevPauldron(bool outwardIsPlusX)
            => PaintField(RevPauldronMin, RevPauldronMax, 0, RevPauldronRows, (ix, iy) =>
            {
                int o = outwardIsPlusX ? ix : RevPauldronMin + RevPauldronMax - 1 - ix;
                return RevPauldronTexel(o, RevPauldronRows - 1 - iy, outwardIsPlusX);
            });

        static char RevPauldronTexel(int o, int r, bool outwardIsPlusX)
        {
            float oc = o + 0.5f, rc = r + 0.5f;
            float towardLight = outwardIsPlusX ? oc : -oc;
            int side = towardLight > 6f ? 1 : towardLight < -8f ? -1 : 0;

            // ---- the upper slab: rows 0..6, its outer end running OUT to the tip ----
            float aOuter = 9f + rc * 0.7f;
            if (r < 7 && oc >= -6f && oc <= aOuter)
            {
                if (r == 0) return RampChar(RevPlate, 5);                          // the glossy bevel
                if (r == 1) return RampChar(RevPlate, 4 + Mathf.Min(side, 0));
                if (r == 6) return RampChar(RevPlate, 1);                          // its underside
                if (oc > aOuter - 1.5f) return RampChar(RevPlate, 4);               // the cut end, lit
                if (RevScratch(o, r, 1)) return RampChar(RevPlate, 4);
                return RampChar(RevPlate, 3 + side);
            }

            // ---- the lower slab: from the tip at its top, its end cut back IN to the hem ----
            float bOuter = 14f - (rc - 7.5f) * 0.7f;
            if (r >= 7 && r < 13 && oc >= -4f && oc <= bOuter)
            {
                if (r == 7) return RampChar(RevPlate, 4);                          // its top edge, clear of the slab above
                if (r == 12) return RampChar(RevPlate, 1);
                if (oc > bOuter - 1.5f) return RampChar(RevPlate, 4);
                if (RevScratch(o, r, 2)) return RampChar(RevPlate, 4);
                return RampChar(RevPlate, 3 + side - (r >= 10 ? 1 : 0));
            }
            return '.';
        }

        // ------------------------------------------------------------------ the cuirass (Torso)
        //
        // Torso-local. The BIB: black plate from the collar down to a curved lower edge, lowest at
        // the sternum, carrying a grey control panel (one large dial, two small ones) and a block
        // of red lights. Under it the grey QUILTING, ribbed horizontally, framed by black plate
        // down each flank - the grey only shows in the middle, which is what makes it read as the
        // padded suit under the armour rather than as a grey shirt.

        const int RevCuirassTop = 44, RevCuirassBottom = 6, RevCuirassHalf = 16;

        static string[] _revCuirassRows;
        static string[] RevCuirassRows => _revCuirassRows ??=
            PaintField(-RevCuirassHalf, RevCuirassHalf, RevCuirassBottom, RevCuirassTop, RevCuirassTexel);

        static char RevCuirassTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 9f, -10f);

            // ---- the bib ----
            float bibBottom = 26f + (ax / 16f) * (ax / 16f) * 8f;
            if (y >= bibBottom && ax <= 15.5f)
            {
                // The panel: a grey plate set into the bib, front of centre.
                if (ix >= -4 && ix < 8 && iy >= 29 && iy < 41)
                {
                    float dx = x - 2f, dy = y - 37f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r < 3.2f) return r < 1.2f ? 'D' : r < 2.2f ? 'L' : 'S';     // the big dial
                    float sx = x - 0.5f, sx2 = x - 5f, sy = y - 31.5f;
                    if (sx * sx + sy * sy < 2.3f || sx2 * sx2 + sy * sy < 2.3f) return 'L'; // two small ones
                    if (ix == -4 || iy == 40) return 'L';                             // lit edge
                    if (ix == 7 || iy == 29) return 'S';
                    return 'B';
                }
                // The lights, left of the panel.
                if (ix >= -8 && ix < -6 && iy >= 30 && iy < 35)
                    return RampChar(RevRed, iy == 32 ? 3 : iy == 34 ? 5 : 4);
                if (y < bibBottom + 1f) return RampChar(RevPlate, 1);                 // its lower edge
                if (iy >= 42) return RampChar(RevPlate, 5);                             // the collar, glossy
                if (iy == 41) return RampChar(RevPlate, 2);
                if (RevScratch(ix, iy, 3)) return RampChar(RevPlate, 4);
                return RampChar(RevPlate, 3 + side);
            }

            // ---- the flanks ----
            float half = iy > 14 ? 14.5f : 13.5f;
            if (ax > half) return '.';
            if (ax > 10.5f)
            {
                if (ax < 11.5f) return RampChar(RevPlate, x > 0f ? 5 : 2);            // the plate's inner edge
                return RampChar(RevPlate, 3 + side);
            }

            // ---- the quilting ----
            int rib = WrapMod(iy - 7, 4);
            return RampChar(RevQuilt, rib switch { 3 => 4, 0 => 1, _ => 3 } + (x > 6f ? 1 : x < -6f ? -1 : 0));
        }

        // ------------------------------------------------------------------ the belt
        //
        // On BeltY's band: a black strap carrying grey plates with bevelled corners, and a taller
        // angular buckle standing proud of it at the front.

        const int RevBeltTop = 12, RevBeltBottom = 2, RevBeltHalf = 16;

        static string[] _revBeltRows;
        static string[] RevBeltRows => _revBeltRows ??=
            PaintField(-RevBeltHalf, RevBeltHalf, RevBeltBottom, RevBeltTop, RevBeltTexel);

        static readonly float[] RevBeltPlates = { -11f, -5f, 9f };

        static char RevBeltTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f;

            // The buckle: an elongated hexagon, taller than the strap.
            float bx = x - 2.5f, by = y - 7f;
            if (Mathf.Abs(by) + Mathf.Abs(bx) * 0.55f < 4.6f && Mathf.Abs(bx) < 5f)
            {
                if (Mathf.Abs(by) < 1f && Mathf.Abs(bx) < 2.5f) return RampChar(RevPlate, 1);   // its slot
                return by > 1.5f ? 'L' : by < -1.5f ? 'D' : 'B';
            }

            if (iy < 4 || iy >= 10 || Mathf.Abs(x) > 15.5f) return '.';
            // Plates on the strap, corners bevelled off.
            foreach (float px in RevBeltPlates)
            {
                float dx = Mathf.Abs(x - px), dy = Mathf.Abs(y - 7f);
                if (dx < 2.5f && dy < 3f && dx + dy < 4.6f)
                    return dy > 1.5f && y > 7f ? 'L' : y < 5.5f ? 'D' : 'B';
            }
            return RampChar(RevPlate, iy == 9 ? 4 : iy == 4 ? 1 : 3);
        }

        // ------------------------------------------------------------------ the gauntlets (Gloves)
        //
        // ARM-local: 0 at the shoulder, the elbow line at -14, the wrist at -22, the hand below.
        // A RIBBED upper arm (the reference's corrugated sleeve), a plain elbow plate - plain on
        // purpose, because the rig lengthens the upper arm by repeating the row above the elbow
        // and a rib there would repeat into a stripe - then a bracer with its lights, and a black
        // armoured hand.

        const int RevGauntletTop = 0, RevGauntletBottom = -32, RevGauntletHalf = 7;

        static string[] _revGauntletRows;
        static string[] RevGauntletRows => _revGauntletRows ??=
            PaintField(-RevGauntletHalf, RevGauntletHalf, RevGauntletBottom, RevGauntletTop, RevGauntletTexel);

        static char RevGauntletTexel(int ix, int iy)
        {
            float x = ix + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 3f, -3f);

            // Ribbed upper arm.
            if (iy >= -9 && iy < -1)
            {
                if (ax > 5.5f) return '.';
                int rib = WrapMod(iy, 3);
                return RampChar(RevPlate, (rib == 2 ? 4 : rib == 1 ? 3 : 1) + (rib == 0 ? 0 : side));
            }
            // Elbow plate - uniform rows, see above.
            if (iy >= -15 && iy < -9)
            {
                if (ax > 6f) return '.';
                return RampChar(RevPlate, iy == -10 ? 5 : 3 + side);
            }
            // Bracer, with its lights on the outer face.
            if (iy >= -23 && iy < -15)
            {
                if (ax > 7f) return '.';
                if (ix >= 2 && ix < 4)
                {
                    if (iy == -17 || iy == -21) return RampChar(RevQuilt, 5);         // white lights
                    if (iy == -19) return RampChar(RevRed, 5);                        // red
                }
                if (ix >= -1 && ix < 1 && iy == -19) return RampChar(RevRed, 4);
                if (iy == -16) return RampChar(RevPlate, 5);
                if (iy == -23) return RampChar(RevPlate, 1);
                if (RevScratch(ix, iy, 4)) return RampChar(RevPlate, 4);
                return RampChar(RevPlate, 3 + side);
            }
            // The hand.
            if (iy >= RevGauntletBottom)
            {
                float half = iy < -29 ? 5f : 6.5f;
                if (ax > half) return '.';
                return RampChar(RevPlate, iy == -24 ? 4 : iy < -30 ? 1 : 3 + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the greaves (Legs)
        //
        // LEG-local: 0 at the hip, the knee at -24, the sole at -48. Grey quilting down the thigh
        // with vertical stitching, a ribbed black TUBE bulging out along its outer side, a black
        // knee plate, and a black shin plate running down under the boot.

        const int RevGreaveTop = 2, RevGreaveBottom = -40, RevGreaveHalf = 6;

        static string[] _revGreaveFront, _revGreaveBack;
        static string[] RevGreaveFront => _revGreaveFront ??= RevGreave(outerIsPlusX: true);
        static string[] RevGreaveBack => _revGreaveBack ??= RevGreave(outerIsPlusX: false);

        static string[] RevGreave(bool outerIsPlusX)
            => PaintField(-RevGreaveHalf, RevGreaveHalf, RevGreaveBottom, RevGreaveTop,
                          (ix, iy) => RevGreaveTexel(ix, iy, outerIsPlusX));

        static char RevGreaveTexel(int ix, int iy, bool outerIsPlusX)
        {
            float x = ix + 0.5f;
            float outward = outerIsPlusX ? x : -x;                 // + toward the outer side
            int side = LitSide(x, 1.5f, -2.5f);

            if (iy >= -21)
            {
                // The tube: bulging in segments along the outer thigh.
                int rib = WrapMod(iy, 3);
                float bulge = rib == 1 ? 6f : 5.5f;
                if (outward > 2f && outward <= bulge && iy < 0)
                    return RampChar(RevPlate, rib == 2 ? 4 : rib == 1 ? 3 : 1);
                if (Mathf.Abs(x) > 4.5f) return '.';
                // Quilting: vertical stitch lines.
                if (WrapMod(ix, 3) == 0) return RampChar(RevQuilt, 2);
                return RampChar(RevQuilt, 3 + side);
            }
            if (Mathf.Abs(x) > 5f) return '.';
            // Knee plate.
            if (iy >= -27) return RampChar(RevPlate, iy == -22 ? 5 : iy == -27 ? 1 : 3 + side);
            // Shin plate: a lit ridge down its front, battle-scratched.
            if (ix == 0) return RampChar(RevPlate, 4);
            if (ix == 1) return RampChar(RevPlate, 2);
            if (RevScratch(ix, iy, 5)) return RampChar(RevPlate, 4);
            return RampChar(RevPlate, 3 + side);
        }

        // ------------------------------------------------------------------ the boots (Boots)
        //
        // LEG-local, art bottom on the sole (BootY's rule, so the boot stands on the ground and not
        // a texel off it). An ankle cuff over the shin plate, a toe cap, a heavy sole.

        const int RevBootTop = -34, RevBootBottom = -48, RevBootHalf = 8;

        static string[] _revBootRows;
        static string[] RevBootRows => _revBootRows ??=
            PaintField(-RevBootHalf, RevBootHalf, RevBootBottom, RevBootTop, RevBootTexel);

        static char RevBootTexel(int ix, int iy)
        {
            float x = ix + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 2f, -3f);
            float half = iy >= -42 ? 5.5f : 7f;
            if (ax > half) return '.';
            if (iy == -35 || iy == -43) return RampChar(RevPlate, 5);             // cuff rim, toe cap
            if (iy == -36) return RampChar(RevPlate, 2);
            if (iy <= -47) return RampChar(RevPlate, 1);                           // the sole
            if (RevScratch(ix, iy, 6)) return RampChar(RevPlate, 4);
            return RampChar(RevPlate, 3 + side);
        }
    }
}
