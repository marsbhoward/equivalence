using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Vermilion: red plate trimmed in white, a grey cape
    //
    // After the user's reference (a knight-commander in red: a tall flared collar; a sculpted
    // breastplate with a white-rimmed pentagon under the collar and a hooked guard over each
    // breast; big ROUND pauldrons standing above the shoulder line; black sleeves under red
    // vambraces spiked at the elbow; white gloves; a black belt with a silver buckle; a red
    // tabard - a pointed centre panel, plates at the hips, long side panels cut into points, white
    // laces ending in diamonds; tall red boots with pointed toes turned out; and a grey cape lined
    // in white). Named for the colour, and Diamond, power 0, like every reference-built cosmetic set.
    //
    //   vermilion_cuirass     Torso      collar, pentagon, the two hooked guards, sculpted plate
    //   vermilion_pauldrons   Shoulders  a dome standing above the shoulder, rimmed in white along
    //                                    its hem and inner edge, and a lame under it
    //   vermilion_vambraces   Gloves     black sleeve, red vambrace with its elbow spike, a flared
    //                                    white-rimmed cuff, white glove
    //   vermilion_belt        Belt       black leather, a silver buckle and keeper
    //   vermilion_tabard      Legs       the hip band, the centre panel and its pointed hem, the hip
    //                                    plates, the long side panels and their laces
    //   vermilion_boots       Boots      tall, peaked at the knee, a stepped ankle guard, the toe
    //                                    pointed and turned OUT (two grids, one per leg)
    //   vermilion_cape        Back       grey, floor length, its white lining turned back on one side
    //
    // NO CROSSES: the reference's tabard carries three white crosses, left off as the user had
    // the chest crosses taken off Nocturne and Orichalc. The panels read as a tabard without them.
    //
    // THE WHITE IS AN EDGE (Sovereign's gold rule): every plate is bounded by it, nothing white sits
    // in the middle of a plate. One texel - white on red carries a single texel.
    //
    // RED, NOT PINK: lit toward a warm orange, not white (Shogun's lacquer, the Hellspawn's note).
    //
    // Fields in each joint's own frame (DemoGear.Fields.cs), lit from +X like every piece here.
    public static partial class DemoGear
    {
        const string VerRed = "ksdblh", VerWhite = "KSDBLH", VerBlack = "123456";

        static void AddVermilion(List<GearItem> items)
        {
            var red = new Palette.Ramp(new Color(0.68f, 0.13f, 0.13f), shade: 0.36f)
                .WithHighlightsToward(new Color(1.00f, 0.52f, 0.38f), 0.30f);
            // The trim, and the gloves: an off-white, cool, a touch below 1.0 so it has a Light
            // and a Glow to spend (Sovereign's note).
            var white = new Palette.Ramp(new Color(0.86f, 0.87f, 0.90f), shade: 0.24f);
            // Leather and the sleeves: the undersuit's black (Palette.Undersuit's recipe).
            var black = new Palette.Ramp(new Color(0.17f, 0.17f, 0.21f), lift: 0.16f, shade: 0.34f, line: 0.80f)
                .WithShadowsBelowLight(0.46f, 0.30f);
            var pal = Palette.Of(red, white, black);
            var far = Palette.Of(red.Scaled(0.80f), white.Scaled(0.86f), black.Scaled(0.85f));
            // The cape: a violet-tinged grey outside (the reference's), the white inside.
            var grey = new Palette.Ramp(new Color(0.45f, 0.44f, 0.51f), lift: 0.20f, shade: 0.30f, line: 0.75f);
            var capePal = Palette.Of(grey, white);

            items.Add(Defends(Make("vermilion_cuirass", "Vermilion Cuirass", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.vermilion", VerCuirassRows, pal,
                       0f, FieldCentreCells(VerCuirassBottom, VerCuirassTop), ppu: BodyPpu)),
                // A commander who holds the line behind a shield.
                DefensiveAbility.Barrier));

            items.Add(Make("vermilion_pauldrons", "Vermilion Pauldrons", GearSlot.Shoulders, LootTier.Diamond, 0f,
                Pixels(RigLayer.Shoulders, "gear.pauldron.vermilion", VerPauldronNear, pal,
                       -ShoulderX - VerPauldronCentreCells, VerPauldronY, ppu: BodyPpu),
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.vermilion.dark", VerPauldronFar, far,
                       ShoulderX + VerPauldronCentreCells, VerPauldronY, ppu: BodyPpu)));

            items.Add(Make("vermilion_vambraces", "Vermilion Vambraces", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesFront, "gear.gloves.vermilion", VerVambraceRows, pal,
                       0f, FieldCentreCells(VerVambraceBottom, VerVambraceTop), ppu: BodyPpu),
                Pixels(RigLayer.GlovesBack, "gear.gloves.vermilion.dark", VerVambraceRows, far,
                       0f, FieldCentreCells(VerVambraceBottom, VerVambraceTop), ppu: BodyPpu)));

            items.Add(Make("vermilion_belt", "Vermilion Belt", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.vermilion", VerBeltRows, pal,
                       0f, FieldCentreCells(VerBeltBottom, VerBeltTop), ppu: BodyPpu)));

            items.Add(Make("vermilion_tabard", "Vermilion Tabard", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.Tasset, "gear.legs.vermilion.tabard", VerTabardRows, pal,
                       0f, FieldCentreCells(VerTabardBottom, VerTabardTop), ppu: BodyPpu)));

            // Two grids: the toe turns OUT, and the near leg's outside is +X while the far leg's
            // is -X (Revenant's greaves make the same split).
            items.Add(Make("vermilion_boots", "Vermilion Boots", GearSlot.Boots, LootTier.Diamond, 0f,
                Pixels(RigLayer.BootsFront, "gear.boots.vermilion", VerBootFront, pal,
                       0f, FieldCentreCells(VerBootBottom, VerBootTop), ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.vermilion.dark", VerBootBack, far,
                       0f, FieldCentreCells(VerBootBottom, VerBootTop), ppu: BodyPpu)));

            var cape = Make("vermilion_cape", "Vermilion Cape", GearSlot.Back, LootTier.Diamond, 0f,
                Pixels(RigLayer.Back, "gear.back.vermilion", VerCapeRows, capePal,
                       0f, FieldCentreCells(VerCapeBottom, VerCapeTop), ppu: BodyPpu));
            cape.CapeSwingScale = 0.45f;
            // The lining is turned back on the right edge (-X facing the camera); turned away the
            // cape turns over with the carry so it stays on the right (GearItem.Lopsided).
            cape.Lopsided = true;
            items.Add(cape);

            Dyeable(items, "vermilion_", DyeChannel.Of("Plate", DyeMaterial.Metal, red, 0.80f),
                    DyeChannel.Of("Trim", DyeMaterial.Metal, white, 0.86f));
            // The cape is grey cloth lined in the white - cloth on both channels, not plate.
            Dyeable(items, "vermilion_cape", DyeChannel.Of("Cloth", DyeMaterial.Cloth, grey),
                    DyeChannel.Of("Lining", DyeMaterial.Cloth, white));
        }

        /// <summary>True where <paramref name="inside"/> holds at (x, y) but not at one of its four
        /// neighbours a texel away - a shape's own one-texel rim.</summary>
        static bool VerRim(System.Func<float, float, bool> inside, float x, float y)
            => !inside(x - 1f, y) || !inside(x + 1f, y) || !inside(x, y - 1f) || !inside(x, y + 1f);

        // ------------------------------------------------------------------ the cuirass (Torso)
        //
        // Torso-local. A tall red collar flaring as it rises, white up its sides, the black of the
        // undersuit's neck showing inside it. Under it a PENTAGON, white-rimmed, pointing down; and
        // over each breast a HOOKED guard - running in from the shoulder and turning down to a
        // point beside the pentagon - most of it under the pauldrons, its hook in the open. The
        // plate itself is SCULPTED in darker red: the line under the pecs, the sternum, two ab
        // lines either side - drawn as tone, never white, so the white stays the plates' edges.

        const int VerCuirassTop = 44, VerCuirassBottom = 6, VerCuirassHalf = 16;

        static string[] _verCuirassRows;
        static string[] VerCuirassRows => _verCuirassRows ??=
            PaintField(-VerCuirassHalf, VerCuirassHalf, VerCuirassBottom, VerCuirassTop, VerCuirassTexel);

        static bool VerPentagonIn(float x, float y)
        {
            float ax = Mathf.Abs(x);
            return ax <= 4f && y < 34f && y >= 27f + ax * 0.85f;
        }

        static bool VerHookIn(float x, float y)
        {
            float ax = Mathf.Abs(x);
            if (ax < 5f || ax > 13.5f) return false;
            float top = 33f - (13.5f - ax) * 0.12f;
            float bottom = ax < 8f ? 29.5f - (8f - ax) * 1.4f : 29.5f + (ax - 8f) * 0.15f;
            return y <= top && y >= bottom;
        }

        /// <summary>The line under the pecs: lowest at the sternum, rising to the flanks.</summary>
        static float VerPecLine(float ax) => 22.5f + (ax / 9f) * (ax / 9f) * 3f;

        static char VerCuirassTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 7f, -8f);

            // ---- the collar ----
            float collarHalf = 4.5f + (y - 34f) * 0.35f;
            if (y >= 34f && ax <= collarHalf)
            {
                if (ax > collarHalf - 1.2f) return RampChar(VerWhite, 4 + LitSide(x, 3f, -4f));
                if (ax < 2.5f && iy >= 37) return RampChar(VerBlack, 2);                 // the neck inside it
                return RampChar(VerRed, 3 + LitSide(x, 1f, -2f));
            }

            // ---- the pentagon ----
            if (VerPentagonIn(x, y))
            {
                if (VerRim(VerPentagonIn, x, y)) return RampChar(VerWhite, 4 + Mathf.Max(side, 0));
                return RampChar(VerRed, 3 + LitSide(x, 1f, -2f));
            }

            // ---- the hooked guards ----
            if (VerHookIn(x, y))
            {
                if (VerRim(VerHookIn, x, y)) return RampChar(VerWhite, y > 30f ? 4 : 3 + Mathf.Max(side, 0));
                return RampChar(VerRed, 4 + side);
            }

            // ---- the plate ----
            float top = ax <= 7f ? 34f : 34f - (ax - 7f) * 0.40f;
            float half = iy >= 26 ? 14f : 13.5f;
            if (y >= top || ax > half) return '.';
            float pec = VerPecLine(ax);
            if (ax < 10f && y >= pec - 1f && y < pec) return RampChar(VerRed, 1);           // under the pecs
            if (ix == 0 && y > 13f && y < pec + 1f) return RampChar(VerRed, 2);              // the sternum
            if ((iy == 17 || iy == 13) && ax > 1.5f && ax < 5.5f) return RampChar(VerRed, 2); // the abs
            if (y > pec && x > 2f && x < 8f) return RampChar(VerRed, 4);                    // the lit pec
            return RampChar(VerRed, 3 + side);
        }

        // ------------------------------------------------------------------ the pauldrons (Shoulders)
        //
        // A DOME - the reference's are near spheres - standing above the shoulder line beside the
        // head (VerPauldronRise over the chin at its crown), lit high toward the crown and the
        // light, falling into shade at its outer foot. WHITE along its hem and up its INNER edge,
        // the crescent that parts it from the chest. Under the dome a red lame flares down the arm,
        // hemmed in white, its outer end rounded. Outward offsets from the joint, mirrored for the
        // far side. A CAP, not HangsAlongArm: as wide as it is tall, it stands up from the shoulder
        // rather than hanging down the arm.

        const int VerPauldronRows = 18, VerPauldronMin = -6, VerPauldronMax = 16;
        const int VerPauldronRise = 4;
        static float VerPauldronCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((VerPauldronMin + VerPauldronMax) * 0.5f);
        static float VerPauldronY
            => PlateY(VerPauldronNear) + PrimitiveCharacterRig.Proportions.Cells(VerPauldronRise);

        static string[] _verPauldronNear, _verPauldronFar;
        static string[] VerPauldronNear => _verPauldronNear ??= VerPauldron(outwardIsPlusX: false);
        static string[] VerPauldronFar => _verPauldronFar ??= VerPauldron(outwardIsPlusX: true);

        static string[] VerPauldron(bool outwardIsPlusX)
            => PaintField(VerPauldronMin, VerPauldronMax, 0, VerPauldronRows, (ix, iy) =>
            {
                int o = outwardIsPlusX ? ix : VerPauldronMin + VerPauldronMax - 1 - ix;
                return VerPauldronTexel(o, VerPauldronRows - 1 - iy, outwardIsPlusX);
            });

        /// <summary>The dome, in (outward, row-from-top) texels: an ellipse cut level-ish at its
        /// hem.</summary>
        static bool VerDomeIn(float oc, float rc)
        {
            float dx = (oc - 4.5f) / 10.5f, dy = (rc - 11f) / 11f;
            return dx * dx + dy * dy < 1f && rc < 12.5f + (oc - 4.5f) * 0.08f;
        }

        static char VerPauldronTexel(int o, int r, bool outwardIsPlusX)
        {
            float oc = o + 0.5f, rc = r + 0.5f;
            float towardLight = outwardIsPlusX ? oc : -oc;
            int side = towardLight > 6f ? 1 : towardLight < -8f ? -1 : 0;

            // ---- the dome ----
            if (VerDomeIn(oc, rc))
            {
                bool hem = !VerDomeIn(oc, rc + 1f);
                bool innerEdge = oc < 2f && (!VerDomeIn(oc - 1f, rc) || !VerDomeIn(oc, rc - 1f));
                if (hem || innerEdge) return RampChar(VerWhite, rc < 8f ? 4 : 3 + Mathf.Max(side, 0));
                float hx = (oc - 3f) / 4.5f, hy = (rc - 4.5f) / 3f;
                if (hx * hx + hy * hy < 1f) return RampChar(VerRed, 5);                    // the crown's light
                float dx = (oc - 4.5f) / 10.5f, dy = (rc - 11f) / 11f;
                if (dx * dx + dy * dy > 0.62f && (oc > 9f || rc > 9f)) return RampChar(VerRed, 2);  // its foot
                return RampChar(VerRed, Mathf.Min(3 + side + (rc < 8f ? 1 : 0), 4));
            }

            // ---- the lame under it ----
            if (rc >= 12.5f && r < 18 && oc >= -2f)
            {
                float reach = 14.5f - Mathf.Max(0f, rc - 15.5f) * 2f;
                if (oc > reach) return '.';
                if (r == 17 || oc > reach - 1f) return RampChar(VerWhite, 3 + Mathf.Max(side, 0));
                if (rc < 14f) return RampChar(VerRed, 1);                                    // in the dome's shadow
                return RampChar(VerRed, 3 + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the vambraces (Gloves)
        //
        // ARM-local: 0 at the shoulder, the elbow cut at -14, the wrist at -22, the hand below. A
        // black sleeve, rows UNIFORM down to the cut (the rig repeats row -12 to lengthen the upper
        // arm). Below the cut, so it bends with the forearm: the red vambrace, white along its top -
        // which steps, higher on the outer side - with a SPIKE standing off the elbow on the -X
        // side (outward on the near arm; the far arm's turns in, behind the torso); a flared cuff
        // rimmed in white top and bottom; and a WHITE glove.
        //
        // The cuff is no wider than 6: the near arm hangs in front of the torso, and Nocturne
        // found a wider cuff's outline reaching onto the chest.

        const int VerVambraceTop = 0, VerVambraceBottom = -32, VerVambraceHalf = 8;

        static string[] _verVambraceRows;
        static string[] VerVambraceRows => _verVambraceRows ??=
            PaintField(-VerVambraceHalf, VerVambraceHalf, VerVambraceBottom, VerVambraceTop, VerVambraceTexel);

        static char VerVambraceTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 2.5f, -3f);

            // The sleeve.
            if (iy >= -14 && iy < -1)
                return ax > 5.5f ? '.' : RampChar(VerBlack, 3 + side);

            // The elbow spike, off the -X side: a triangle, its tip at (-8, -17).
            if (x < -5f && y <= -14.5f && y >= -19.5f)
            {
                float reach = 5f + 3f * (1f - Mathf.Abs(y + 17f) / 2.5f);
                if (-x <= reach)
                    return -x > reach - 1f || y > -15.5f ? RampChar(VerWhite, 3) : RampChar(VerRed, 2);
            }
            // The vambrace, its top stepping up on the outer side.
            float top = x < 0f ? -14f : -16f;
            if (y <= top && iy >= -23)
            {
                if (ax > 5.5f) return '.';
                if (y > top - 1f) return RampChar(VerWhite, 4 + Mathf.Max(side, 0));
                return RampChar(VerRed, (iy == -17 || iy == -15 ? 4 : 3) + side);
            }
            // The cuff.
            if (iy >= -26 && iy < -23)
            {
                if (ax > 6f) return '.';
                if (iy == -25) return RampChar(VerRed, 3 + side);
                return RampChar(VerWhite, iy == -24 ? 4 + Mathf.Max(side, 0) : 3 + side);
            }
            // The glove, white: a lit knuckle row, the fingers turning into shade.
            if (iy >= VerVambraceBottom)
            {
                float half = iy < -29 ? 4.5f : 5.5f;
                if (ax > half) return '.';
                if (iy == -27) return RampChar(VerWhite, 4 + side);
                return RampChar(VerWhite, (iy < -29 ? 2 : 3) + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the belt
        //
        // On BeltY's band: black leather, lit along its top edge, with a squared silver buckle at
        // the front and a silver keeper beside it.

        const int VerBeltTop = 12, VerBeltBottom = 0, VerBeltHalf = 16;

        static string[] _verBeltRows;
        static string[] VerBeltRows => _verBeltRows ??=
            PaintField(-VerBeltHalf, VerBeltHalf, VerBeltBottom, VerBeltTop, VerBeltTexel);

        static char VerBeltTexel(int ix, int iy)
        {
            float x = ix + 0.5f;

            // the buckle: a frame round the leather, a prong across it
            if (ix >= -3 && ix < 3 && iy >= 3 && iy < 10)
            {
                bool frame = ix == -3 || ix == 2 || iy == 3 || iy == 9;
                if (frame) return RampChar(VerWhite, iy >= 8 ? 5 : iy <= 4 ? 3 : 4);
                if (iy == 6 && ix < 1) return RampChar(VerWhite, 3);                         // the prong
                return RampChar(VerBlack, 2);
            }
            // the keeper
            if ((ix == 4 || ix == 5) && iy >= 3 && iy < 10) return RampChar(VerWhite, ix == 4 ? 4 : 3);

            if (iy < 4 || iy >= 9) return '.';
            float half = (iy == 4 || iy == 8) ? 15f : 15.5f;
            if (Mathf.Abs(x) > half) return '.';
            return RampChar(VerBlack, iy switch { 8 => 4, 4 => 1, _ => 3 } + LitSide(x, 10f, -11f));
        }

        // ------------------------------------------------------------------ the tabard (Legs)
        //
        // TORSO-local on RigLayer.Tasset, hung from the belt. Top to bottom:
        //
        //   the HIP BAND      red between two white rims, across the waist under the belt
        //   the CENTRE PANEL  a white-rimmed plate, then the cloth falling to a POINT at mid-thigh
        //   the HIP PLATES    one a side, white all round, flaring as they fall
        //   the SIDE PANELS   long, from under the hip plates to below the knee, their hems cut into
        //                     TWO POINTS each; a white LACE down each inner edge, ending in a diamond
        //
        // The undersuit's black legs show between the centre panel and the side panels - the
        // reference's gaps, and what makes the panels read as panels rather than a skirt.

        const int VerTabardTop = 4, VerTabardBottom = -36, VerTabardHalf = 22;

        static string[] _verTabardRows;
        static string[] VerTabardRows => _verTabardRows ??=
            PaintField(-VerTabardHalf, VerTabardHalf, VerTabardBottom, VerTabardTop, VerTabardTexel);

        /// <summary>A hip plate: a trapezoid FLARING as it falls - both edges slant out. Square
        /// and close beside the centre plate (the first pass) the three read as a row of boxes.</summary>
        static bool VerHipPlateIn(float x, float y)
        {
            float ax = Mathf.Abs(x), down = -1f - y;
            return y < -1f && y >= -12f && ax >= 8f + down * 0.35f && ax <= 13.5f + down * 0.45f;
        }

        static bool VerAppliedPlateIn(float x, float y) => Mathf.Abs(x) <= 5.5f && y < -1f && y >= -10f;

        static float VerSidePanelInner(float y) => 9.5f + (-8f - y) * 0.05f;
        static float VerSidePanelOuter(float y) => 15f + (-8f - y) * 0.12f;

        /// <summary>The side panels' hem: a SWALLOWTAIL - a point at each edge, a notch between.
        /// Two teeth in each half (the first pass) were too small to read as points and the hem
        /// looked torn.</summary>
        static float VerSideHem(float t) => -34f + (1f - Mathf.Abs(t * 2f - 1f)) * 6f;

        static char VerTabardTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 10f, -11f);

            // ---- the hip band ----
            if (y >= -1f && y < 2f)
            {
                if (ax > 14.5f) return '.';
                return iy == 0 ? RampChar(VerRed, 3 + side) : RampChar(VerWhite, iy == 1 ? 4 : 3);
            }

            // ---- the hip plates ----
            if (VerHipPlateIn(x, y))
            {
                if (VerRim(VerHipPlateIn, x, y)) return RampChar(VerWhite, 3 + Mathf.Max(side, 0));
                return RampChar(VerRed, (y > -4f ? 4 : 3) + side);
            }

            // ---- the centre panel: the applied plate, then the cloth to a point ----
            if (VerAppliedPlateIn(x, y))
            {
                if (VerRim(VerAppliedPlateIn, x, y)) return RampChar(VerWhite, 4 + Mathf.Max(side, 0));
                return RampChar(VerRed, 3 + LitSide(x, 2f, -3f));
            }
            if (ax <= 5.5f && y < -10f && y >= -24f + ax * 0.9f)
                return RampChar(VerRed, (ax > 4.5f ? 2 : 3) + LitSide(x, 2f, -3f));

            // ---- the side panels ----
            if (y < -6f)
            {
                float inner = VerSidePanelInner(y), outer = VerSidePanelOuter(y);
                if (ax >= inner && ax <= outer)
                {
                    float t = (ax - inner) / (outer - inner);
                    if (y < VerSideHem(t)) return '.';
                    // the lace down the inner edge, ending in a diamond
                    if (ax < inner + 1f && y > -22f) return RampChar(VerWhite, 4);
                    if (Mathf.Abs(ax - (inner + 0.5f)) + Mathf.Abs(y + 23.5f) <= 1.5f)
                        return RampChar(VerWhite, y > -23.5f ? 4 : 3);
                    return RampChar(VerRed, (t > 0.85f ? 2 : 3) + side);
                }
            }
            return '.';
        }

        // ------------------------------------------------------------------ the boots (Boots)
        //
        // LEG-local, art bottom on the sole. TALL - the shaft rises to the knee and PEAKS at the
        // front, rimmed in white along its top; a stepped ankle guard, white along its top, stands
        // proud of the shaft; and the foot ends in a long POINTED toe turned OUT and curling up -
        // the reference's, and the boot's one shape no other boot here has. Two grids, mirrored by
        // which way is out.

        const int VerBootTop = -20, VerBootBottom = -48, VerBootHalf = 12;

        static string[] _verBootFront, _verBootBack;
        static string[] VerBootFront => _verBootFront ??= VerBoot(outIsPlusX: true);
        static string[] VerBootBack => _verBootBack ??= VerBoot(outIsPlusX: false);

        static string[] VerBoot(bool outIsPlusX)
            => PaintField(-VerBootHalf, VerBootHalf, VerBootBottom, VerBootTop,
                          (ix, iy) => VerBootTexel(ix, iy, outIsPlusX));

        /// <summary>The toe: a HORN off the outer side of the foot, tapering to a point that curls
        /// up past the instep. A wedge off the sole (the first pass) read as the foot flaring like
        /// a bell, not as a pointed toe.</summary>
        static bool VerToeIn(float u, float y)
        {
            if (u < 5f || u > 12f) return false;
            // Never thinner than half a texel, nor climbing more than a texel a column, or the
            // tip comes away from the toe as a loose speck.
            float yc = -46f + (u - 5.5f) * (u - 5.5f) * 0.09f;        // its centre line, curling up
            float half = 2f * (1f - (u - 5f) / 7.5f) + 0.4f;            // tapering to the tip
            return Mathf.Abs(y - yc) < half;
        }

        static char VerBootTexel(int ix, int iy, bool outIsPlusX)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            float u = outIsPlusX ? x : -x;                   // + toward the outside
            int side = LitSide(x, 2f, -3f);

            // ---- the toe, pointed and turned out ----
            if (VerToeIn(u, y))
            {
                float yc = -46f + (u - 5.5f) * (u - 5.5f) * 0.09f;
                return RampChar(VerRed, y > yc + 0.5f ? 4 : y < yc - 1f ? 1 : 3);    // lit on top
            }

            // ---- the foot ----
            if (y < -42.5f)
            {
                if (ax > 5.5f) return '.';
                if (y < -47f) return RampChar(VerRed, 0);                                    // the sole
                return RampChar(VerRed, (y > -44f ? 4 : 3) + side);
            }

            // ---- the ankle guard, stepped up on the outer side ----
            float guardTop = u > 2f ? -35f : -36.5f;
            if (y < guardTop && ax <= 6f)
            {
                bool rim = y > guardTop - 1.5f || (u > 1f && u < 2f && y > -37f);
                if (rim) return RampChar(VerWhite, 4 + Mathf.Max(side, 0));
                return RampChar(VerRed, 3 + side - (y < -41.5f ? 1 : 0));
            }

            // ---- the shaft, peaking at the front ----
            float top = -21f - ax * 0.6f;
            if (ax <= 4.5f && y <= top)
            {
                if (y > top - 1.2f) return RampChar(VerWhite, 4 + Mathf.Max(side, 0));
                return RampChar(VerRed, (ix == 1 ? 4 : 3) + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the cape (Back)
        //
        // TORSO-local texels. GREY, from the shoulders to the floor and flaring to nearly twice the
        // body's width, the reference's. Its WHITE LINING is turned back along the -X edge from
        // the arm down, widening toward the hem - from the front it is the strip of white past the
        // body on one side, as in the reference. Broad fan folds (the Hellspawn's); a fold shadow
        // where the lining turns.

        const int VerCapeTop = 48, VerCapeBottom = -52, VerCapeHalf = 38;

        static string[] _verCapeRows;
        static string[] VerCapeRows => _verCapeRows ??=
            PaintField(-VerCapeHalf, VerCapeHalf, VerCapeBottom, VerCapeTop, VerCapeTexel);

        static float VerCapeHalfAt(float y) => 21f + Mathf.Max(0f, 30f - y) * 0.2f;
        static float VerCapeTopAt(float ax) => ax <= 6f ? 46f : 46f - (ax - 6f) * 0.36f;

        static char VerCapeTexel(int ix, int iy)
        {
            const string grey = "ksdblh", lining = "KSDBLH";
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            float half = VerCapeHalfAt(y), top = VerCapeTopAt(ax);
            float hem = -50f + Mathf.Pow(Mathf.Clamp01(ax / half), 4f) * 3f;
            if (ax > half || y > top || y < hem) return '.';

            // the lining, turned back along the -X edge
            float liningW = Mathf.Clamp((22f - y) * 0.11f, 0f, 7f);
            if (x < 0f && ax > half - liningW)
            {
                if (ax < half - liningW + 1f) return RampChar(grey, 1);                       // the turn
                return RampChar(lining, ax > half - 1.5f ? 2 : y > -20f ? 4 : 3);
            }

            if (y < hem + 2f) return RampChar(grey, 1);                                       // the hem
            if (top - y < 2f) return RampChar(grey, 4);                                       // the shoulders

            float fold = 0f;
            if (y < 10f)
            {
                float u = x / Mathf.Max(half, 1f);
                fold = Mathf.Sin(u * Mathf.PI * 2.5f + 0.9f) * Mathf.Clamp01((10f - y) / 50f);
            }
            int tone = 3;
            if (fold > 0.55f) tone++;
            else if (fold < -0.6f) tone--;
            if (half - ax < 2f) tone += x > 0f ? 1 : -1;
            return RampChar(grey, Mathf.Clamp(tone, 1, 4));
        }
    }
}
