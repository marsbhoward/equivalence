using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Orichalc: copper plate over yellow cloth
    //
    // After the user's reference (a knight in silver plate over blue cloth: a standing collar, a
    // blue panel on the upper chest, a rounded breastplate over banded faulds, big peaked
    // pauldrons with a band round the arm below them, blue sleeves under plated
    // forearms, two straps crossed at the waist, plate hip guards over a long coat open at the
    // front, full leg harness and sabatons), RECOLOURED on the user's instruction: the armour
    // COPPER and the cloth YELLOW. Named for the material - orichalcum, the old copper-gold alloy -
    // and Diamond, power 0, like every reference-built cosmetic set.
    //
    //   orichalc_cuirass    Torso      a yellow collar and chest panel, the domed breastplate,
    //                                  three faulds to the waist
    //   orichalc_pauldrons  Shoulders  one big plate rising to a peak at its outer top, the yellow
    //                                  cloth under it, and a band jutting out round the upper arm
    //   orichalc_gauntlets  Gloves     yellow sleeve, a copper couter, a ridged vambrace and a
    //                                  plated gauntlet
    //   orichalc_crossbelt  Belt       two yellow straps crossing at a shallow angle on a copper ring
    //   orichalc_tassets    Legs       copper hip guards, the coat's yellow front panels hanging
    //                                  OUTSIDE the legs, and the leg harness - cuisse, knee cop,
    //                                  greave
    //   orichalc_sabatons   Boots      a copper cuff, a ridged shaft, a foot of lames
    //   orichalc_coat       Back       the coat's back, floor length, BORDERED in copper - a broad
    //                                  band with a thin line inside it, the reference's double trim
    //
    // THE SWAP IS WHOLESALE: everything the reference draws silver is copper, everything blue is
    // yellow - including the coat's light trim, which is the armour's material on the reference.
    // Copper on yellow carries by VALUE more than hue (both are warm), so the yellow is kept
    // high - below its base only in a seam or a fold's trough - and the copper sits a step lower,
    // its highlight lifted toward a rosy sheen rather than white (a warm metal lit toward white
    // reads as wood - Palette.Bronze's note).
    //
    // Two silhouettes are deliberately unlike Nocturne's, its sister set from the same sheet: the
    // pauldrons are ROUND where Nocturne's are cut, and the coat is a straight A-line with square
    // corners where Nocturne's flares and sweeps up. At arena size the shape is what says which.
    //
    // Fields in each joint's own frame (DemoGear.Fields.cs), lit from +X like every piece here.
    public static partial class DemoGear
    {
        const string OriCopper = "ksdblh", OriCloth = "KSDBLH";

        static void AddOrichalc(List<GearItem> items)
        {
            var copper = new Palette.Ramp(new Color(0.70f, 0.38f, 0.21f), shade: 0.40f)
                .WithHighlightsToward(new Color(1.00f, 0.80f, 0.62f), 0.42f);
            // A marigold yellow. Its shadows are placed by hand toward OCHRE: a yellow lerped
            // toward black goes olive, which reads as mould on cloth, not shade.
            var yellow = new Palette.Ramp(new Color(0.95f, 0.77f, 0.20f), lift: 0.22f);
            var pal = Palette.Of(copper, yellow);
            OchreShadows(pal, 1f);
            var far = Palette.Of(copper.Scaled(0.82f), yellow.Scaled(0.84f));
            OchreShadows(far, 0.84f);

            items.Add(Defends(Make("orichalc_cuirass", "Orichalc Cuirass", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.orichalc", OriCuirassRows, pal,
                       0f, FieldCentreCells(OriCuirassBottom, OriCuirassTop), ppu: BodyPpu)),
                // A knight's full harness: it holds the line.
                DefensiveAbility.Bulwark));

            var pauldrons = Make("orichalc_pauldrons", "Orichalc Pauldrons", GearSlot.Shoulders, LootTier.Diamond, 0f,
                Pixels(RigLayer.Shoulders, "gear.pauldron.orichalc", OriPauldronNear, pal,
                       -ShoulderX - OriPauldronCentreCells, OriPauldronY, ppu: BodyPpu),
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.orichalc.dark", OriPauldronFar, far,
                       ShoulderX + OriPauldronCentreCells, OriPauldronY, ppu: BodyPpu));
            // Plate and band reach the elbow: in the rest carry they turn with the arm's whole
            // raise, as Sovereign's stepped plates do (GearItem.HangsAlongArm) - the band wraps the
            // upper arm, and left behind as a cap it would hang in the air beside the raised arm.
            pauldrons.HangsAlongArm = true;
            items.Add(pauldrons);

            items.Add(Make("orichalc_gauntlets", "Orichalc Gauntlets", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesFront, "gear.gloves.orichalc", OriGauntletRows, pal,
                       0f, FieldCentreCells(OriGauntletBottom, OriGauntletTop), ppu: BodyPpu),
                Pixels(RigLayer.GlovesBack, "gear.gloves.orichalc.dark", OriGauntletRows, far,
                       0f, FieldCentreCells(OriGauntletBottom, OriGauntletTop), ppu: BodyPpu)));

            items.Add(Make("orichalc_crossbelt", "Orichalc Crossbelt", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.orichalc", OriBeltRows, pal,
                       0f, FieldCentreCells(OriBeltBottom, OriBeltTop), ppu: BodyPpu)));

            items.Add(Make("orichalc_tassets", "Orichalc Tassets", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.Tasset, "gear.legs.orichalc.tasset", OriTassetRows, pal,
                       0f, FieldCentreCells(OriTassetBottom, OriTassetTop), ppu: BodyPpu),
                Pixels(RigLayer.LegsFront, "gear.legs.orichalc", OriLegRows, pal,
                       0f, FieldCentreCells(OriLegBottom, OriLegTop), ppu: BodyPpu),
                Pixels(RigLayer.LegsBack, "gear.legs.orichalc.dark", OriLegRows, far,
                       0f, FieldCentreCells(OriLegBottom, OriLegTop), ppu: BodyPpu)));

            items.Add(Make("orichalc_sabatons", "Orichalc Sabatons", GearSlot.Boots, LootTier.Diamond, 0f,
                Pixels(RigLayer.BootsFront, "gear.boots.orichalc", OriSabatonRows, pal,
                       0f, FieldCentreCells(OriSabatonBottom, OriSabatonTop), ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.orichalc.dark", OriSabatonRows, far,
                       0f, FieldCentreCells(OriSabatonBottom, OriSabatonTop), ppu: BodyPpu)));

            var coat = Make("orichalc_coat", "Orichalc Long Coat", GearSlot.Back, LootTier.Diamond, 0f,
                Pixels(RigLayer.Back, "gear.back.orichalc", OriCoatRows, pal,
                       0f, FieldCentreCells(OriCoatBottom, OriCoatTop), ppu: BodyPpu));
            coat.CapeSwingScale = 0.45f;
            items.Add(coat);

            Dyeable(items, "orichalc_", DyeChannel.Of("Plate", DyeMaterial.Metal, copper, 0.82f),
                    DyeChannel.Of("Cloth", DyeMaterial.Cloth, yellow, 0.84f));
        }

        /// <summary>
        /// Yellow's dark, deep and line tones re-placed toward ochre and umber - see AddOrichalc.
        /// <paramref name="f"/> is the far-limb scale, applied to the hand-picked tones too.
        /// </summary>
        static void OchreShadows(Dictionary<char, Color> pal, float f)
        {
            Color S(float r, float g, float b) => new(r * f, g * f, b * f);
            pal['D'] = S(0.82f, 0.56f, 0.12f);
            pal['S'] = S(0.60f, 0.36f, 0.08f);
            pal['K'] = S(0.32f, 0.18f, 0.06f);
        }

        // ------------------------------------------------------------------ the cuirass (Torso)
        //
        // Torso-local. A yellow standing collar edged in copper; under it the CHEST PANEL - yellow,
        // shield-shaped, coming to a point on the sternum, rimmed in copper and PLAIN (the
        // reference's cross was left off by the user's call) - which is the reference's one patch
        // of cloth colour on the front of the torso and what tells this set from a plain copper
        // suit. The breastplate is DOMED (lit high on the
        // near side, falling away at the far flank) with a rolled lower rim, and under it three
        // faulds to the waist, each a lit top edge, two base rows and a seam.

        const int OriCuirassTop = 44, OriCuirassBottom = 6, OriCuirassHalf = 16;

        static string[] _oriCuirassRows;
        static string[] OriCuirassRows => _oriCuirassRows ??=
            PaintField(-OriCuirassHalf, OriCuirassHalf, OriCuirassBottom, OriCuirassTop, OriCuirassTexel);

        /// <summary>The chest panel's half-width at height y: square-shouldered, then a point.</summary>
        static float OriPanelHalf(float y) => y >= 30f ? 6.5f : Mathf.Max(0f, (y - 23f) * 0.93f);

        static char OriCuirassTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 6f, -7f);

            // ---- the collar ----
            float collarHalf = 4.5f + (y - 35f) * 0.25f;
            if (iy >= 35 && ax <= collarHalf)
            {
                if (ax > collarHalf - 1.5f) return RampChar(OriCopper, 4 + LitSide(x, 3f, -4f));
                return RampChar(OriCloth, (iy >= 39 ? 3 : 4) + Mathf.Min(LitSide(x, 1f, -2f), 0));
            }

            // ---- the chest panel ----
            float ph = OriPanelHalf(y);
            if (iy < 35 && y >= 23f && ax <= ph + 1f)
            {
                if (ax > ph || iy == 34) return RampChar(OriCopper, 4 + side);                // its rim
                return RampChar(OriCloth, iy >= 32 ? 4 : 3 + Mathf.Min(side, 0));
            }

            // ---- the breastplate ----
            float half = iy >= 26 ? 14f : 13.5f;
            if (iy >= 18 && iy < 35 && ax <= half)
            {
                if (iy == 18) return RampChar(OriCopper, 1);                                 // under the roll
                if (iy == 19) return RampChar(OriCopper, 5);                                 // the rolled rim
                // the dome: brightest high on the near side, round toward the far flank
                float dx = x - 4f, dy = y - 28f;
                float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                int tone = d < 3.5f ? 5 : d < 7.5f ? 4 : 3;
                if (x < -8f) tone = 2;
                if (ax > half - 1f) tone = Mathf.Min(tone, x > 0f ? 3 : 1);
                return RampChar(OriCopper, tone);
            }

            // ---- the faulds ----
            if (iy >= 6 && iy < 18 && ax <= 13f - (17 - iy) * 0.06f)
            {
                int row = WrapMod(17 - iy, 4);                  // 0 at each fauld's top edge
                int tone = row switch { 0 => 4, 3 => 1, _ => 3 };
                return RampChar(OriCopper, row == 3 ? tone : tone + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the pauldrons (Shoulders)
        //
        // The reference's shoulder is ONE big plate, not a stack: it starts on the collar, RISES
        // going outward to a peak at its outer top - standing above the chin beside the head - and
        // rounds off down the outside of the arm. Under it a strip of the yellow cloth, and below
        // that a separate BAND round the upper arm, jutting out as far as the plate does, ending on
        // the elbow. Plate and band are the silhouette: a peaked shoulder over a flared cuff, with
        // cloth between - nothing like Sovereign's stepped plates, which this first copied.
        //
        // Lit high under the peak, the outer flank turning into shade, a rolled edge along the
        // top and hem. Outward offsets from the joint, mirrored for the far side. The grid's top
        // row is OriPauldronRise above the chin; only the peak reaches it - the inner end starts on
        // the chin, as every pauldron does (the head covers the rest).

        const int OriPauldronRows = 26, OriPauldronMin = -6, OriPauldronMax = 16;
        const int OriPauldronRise = 6;
        static float OriPauldronCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((OriPauldronMin + OriPauldronMax) * 0.5f);
        static float OriPauldronY
            => PlateY(OriPauldronNear) + PrimitiveCharacterRig.Proportions.Cells(OriPauldronRise);

        static string[] _oriPauldronNear, _oriPauldronFar;
        static string[] OriPauldronNear => _oriPauldronNear ??= OriPauldron(outwardIsPlusX: false);
        static string[] OriPauldronFar => _oriPauldronFar ??= OriPauldron(outwardIsPlusX: true);

        static string[] OriPauldron(bool outwardIsPlusX)
            => PaintField(OriPauldronMin, OriPauldronMax, 0, OriPauldronRows, (ix, iy) =>
            {
                int o = outwardIsPlusX ? ix : OriPauldronMin + OriPauldronMax - 1 - ix;
                return OriPauldronTexel(o, OriPauldronRows - 1 - iy, outwardIsPlusX);
            });

        /// <summary>The plate's top edge, in rows from the grid's top: on the chin at the inner
        /// end, a straight rise to a POINT, then a steeper fall to the outer side. A rounded fall
        /// (the first pass) read as a hump, not the reference's peaked corner.</summary>
        static float OriPauldronTop(float oc)
        {
            const float peak = 10f;
            if (oc <= 0f) return OriPauldronRise;
            if (oc <= peak) return OriPauldronRise * (1f - oc / peak);
            return (oc - peak) * 1.7f;
        }

        static char OriPauldronTexel(int o, int r, bool outwardIsPlusX)
        {
            float oc = o + 0.5f, rc = r + 0.5f;
            float towardLight = outwardIsPlusX ? oc : -oc;
            int side = towardLight > 6f ? 1 : towardLight < -8f ? -1 : 0;

            // ---- the plate ----
            const float inner = -5.5f, outer = 13.5f;
            float top = OriPauldronTop(oc);
            float hem = 16.5f + (oc - inner) * 0.08f;
            if (oc >= inner && oc <= outer && rc >= top && rc < hem)
            {
                if (rc - top < 1f || rc >= hem - 1f) return RampChar(OriCopper, 4);       // rolled edges
                if (rc >= hem - 2f) return RampChar(OriCopper, 2);                        // in the hem's lee
                float hx = (oc - 7f) / 4.5f, hy = (rc - top - 3f) / 3.5f;
                if (hx * hx + hy * hy < 1f) return RampChar(OriCopper, 5);                // under the peak
                if (oc > outer - 2f) return RampChar(OriCopper, 2);                       // the outer flank
                return RampChar(OriCopper, Mathf.Min(3 + side, 4));
            }

            // ---- the cloth between plate and band ----
            if (rc >= hem && r < 20 && oc >= -1f && oc <= 12.5f)
                return RampChar(OriCloth, rc < hem + 1f ? 2 : 3 + Mathf.Min(side, 0));

            // ---- the band round the upper arm, rows 20..24, its outer corners eased ----
            if (r >= 20 && r < 25)
            {
                float reach = r == 20 || r == 24 ? 14.5f : 15.5f;
                if (oc < -2f || oc > reach) return '.';
                int tone = r switch { 20 => 5, 24 => 1, 23 => 2, _ => 3 };
                return RampChar(OriCopper, tone is 5 or 1 ? tone : tone + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the gauntlets (Gloves)
        //
        // ARM-local: 0 at the shoulder, the elbow line at -14, the wrist at -22, the hand below.
        // A yellow sleeve; a copper COUTER over the elbow, its rows uniform above the elbow line
        // (the rig repeats that row to lengthen the upper arm); a vambrace flaring to the wrist with
        // a lit ridge down its face; a plated gauntlet, its knuckle ridge lit.

        const int OriGauntletTop = 0, OriGauntletBottom = -32, OriGauntletHalf = 7;

        static string[] _oriGauntletRows;
        static string[] OriGauntletRows => _oriGauntletRows ??=
            PaintField(-OriGauntletHalf, OriGauntletHalf, OriGauntletBottom, OriGauntletTop, OriGauntletTexel);

        static char OriGauntletTexel(int ix, int iy)
        {
            float x = ix + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 2.5f, -3f);

            // The sleeve.
            if (iy >= -10 && iy < -1)
                return ax > 5.5f ? '.' : RampChar(OriCloth, 3 + side);
            // The couter: a lit cap over uniform rows.
            if (iy >= -15 && iy < -10)
            {
                if (ax > 6.5f) return '.';
                return RampChar(OriCopper, iy == -11 ? 5 : 3 + side);
            }
            // The vambrace: one plate flaring to the wrist, a lit ridge down its face. Banded in
            // lames like the faulds, every copper piece on the figure was stripes.
            if (iy >= -23 && iy < -15)
            {
                float half = iy < -19 ? 6.5f : 6f;
                if (ax > half) return '.';
                if (iy == -16) return RampChar(OriCopper, 4 + side);
                if (iy == -23) return RampChar(OriCopper, 1);
                if (ix == 1) return RampChar(OriCopper, 5);
                return RampChar(OriCopper, 3 + side);
            }
            // The gauntlet: a rolled cuff, a lit knuckle ridge, the fingers falling into shade.
            if (iy >= OriGauntletBottom)
            {
                float half = iy < -29 ? 4.5f : 5.5f;
                if (ax > half) return '.';
                if (iy == -24) return RampChar(OriCopper, 5);
                if (iy == -27) return RampChar(OriCopper, 4 + side);
                return RampChar(OriCopper, (iy < -29 ? 2 : 3) + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the crossbelt (Belt)
        //
        // On BeltY's band: two yellow straps worn one over the other, crossing at a SHALLOW angle
        // off-centre and held there by a small copper ring - two belts wrapped round the waist, the
        // reference's. Each is lit along its upper edge and dark along its lower; the one rising
        // toward the light lies on top.
        //
        // Steep and centred (the first pass) the pair spread from the ring like a bow tie; one
        // level and one steep read as a scroll. Shallow, they stay belts: at the far end they have
        // only just parted, with the faulds showing between them.

        const int OriBeltTop = 14, OriBeltBottom = -2, OriBeltHalf = 16;
        const float OriBossX = -3f, OriBossY = 6f;

        static string[] _oriBeltRows;
        static string[] OriBeltRows => _oriBeltRows ??=
            PaintField(-OriBeltHalf, OriBeltHalf, OriBeltBottom, OriBeltTop, OriBeltTexel);

        static char OriBeltTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            if (ax > 15.5f) return '.';

            // the ring
            float bx = x - OriBossX, by = y - OriBossY;
            float br = Mathf.Sqrt(bx * bx + by * by);
            if (br < 2.4f) return RampChar(OriCopper, br < 1f ? 1 : by > 0f ? 5 : 3);

            const float slope = 0.19f, halfW = 1.4f;
            float up = y - OriBossY - slope * (x - OriBossX);
            float down = y - OriBossY + slope * (x - OriBossX);
            if (Mathf.Abs(up) < halfW) return RampChar(OriCloth, up > 0.3f ? 4 : up < -0.7f ? 2 : 3);
            if (Mathf.Abs(down) < halfW) return RampChar(OriCloth, down > 0.3f ? 3 : 2);
            return '.';
        }

        // ------------------------------------------------------------------ the tassets (Legs)
        //
        // TORSO-local on RigLayer.Tasset. Copper HIP GUARDS: two lames a side, each hem dropping
        // going outward, rounded at the outer bottom corner, a rolled rim. Under them the coat's
        // FRONT: two yellow panels down the outsides of the legs, bordered in copper down the front
        // edge and along the hem - the leg harness is meant to be seen whole, as the reference's
        // is. Nothing on the centre line - Sovereign's skirt found a middle piece reads as anatomy.
        //
        // The leg harness is the leg's own layers, leg-local, so it strides: a cuisse in two
        // plates, a KNEE COP standing proud of it with a lit boss, and a ridged greave down into
        // the sabaton.

        const int OriTassetTop = 6, OriTassetBottom = -32, OriTassetHalf = 24;

        static string[] _oriTassetRows;
        static string[] OriTassetRows => _oriTassetRows ??=
            PaintField(-OriTassetHalf, OriTassetHalf, OriTassetBottom, OriTassetTop, OriTassetTexel);

        static char OriTassetTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 11f, -12f);

            // ---- the hip guards: two lames a side, each hem dropping going outward ----
            for (int lame = 0; lame < 2; lame++)
            {
                float top = 5f - lame * 6f;
                float inner = 3f + lame * 1.5f, outer = 14f + lame * 1.5f;
                float hem = top - 7f - (ax - inner) * 0.18f;
                // round the outer bottom corner
                float t = Mathf.Clamp01((top - y) / 8f);
                float reach = outer - Mathf.Max(0f, t - 0.5f) * 5f;
                if (y > top || y <= hem || ax < inner || ax > reach) continue;
                if (y < hem + 1f) return RampChar(OriCopper, 5);                               // rolled rim
                if (y < hem + 2f) return RampChar(OriCopper, 2);
                return RampChar(OriCopper, 3 + side + (top - y < 2f ? 1 : 0));
            }

            // ---- the coat's front panels, OUTSIDE the legs ----
            // Over the thighs, their outline swallowed the leg harness the reference shows whole.
            float split = 12.5f + Mathf.Max(0f, 2f - y) * 0.08f;
            float outerEdge = 15f + Mathf.Clamp01((OriTassetTop - y) / 38f) * 8f;
            float hemY = -29f - Mathf.Clamp01((ax - 12f) / 10f) * 1.5f;
            if (ax < split || ax > outerEdge || y < hemY) return '.';
            if (ax < split + 2f || y < hemY + 2f) return RampChar(OriCopper, y < hemY + 2f ? 3 + side : 4);
            return RampChar(OriCloth, 3 + side);
        }

        const int OriLegTop = 2, OriLegBottom = -38, OriLegHalf = 6;

        static string[] _oriLegRows;
        static string[] OriLegRows => _oriLegRows ??=
            PaintField(-OriLegHalf, OriLegHalf, OriLegBottom, OriLegTop, OriLegTexel);

        /// <summary>Leg-local: 0 at the hip, the knee at -24, the sole at -48.</summary>
        static char OriLegTexel(int ix, int iy)
        {
            float x = ix + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2.5f);

            // the knee cop: wider than the leg, a lit boss, a point below
            if (iy >= -28 && iy < -19)
            {
                float half = iy < -25 ? 5.5f - (-25 - iy) * 1.5f : 5.5f;
                if (ax > half) return '.';
                if (iy == -20) return RampChar(OriCopper, 4);
                float dx = x - 0.5f, dy = iy + 23f;
                if (dx * dx + dy * dy < 4.5f) return RampChar(OriCopper, dx * dx + dy * dy < 1.5f ? 5 : 4);
                return RampChar(OriCopper, 3 + side);
            }
            if (ax > 4.5f) return '.';
            // the cuisse: two plates, a lit ridge down the front
            if (iy >= -19)
            {
                if (iy == -9 || iy == -19) return RampChar(OriCopper, 1);
                if (iy == -8 || iy == 0) return RampChar(OriCopper, 4);
                return RampChar(OriCopper, (ix == 0 ? 4 : 3) + side);
            }
            // the greave
            if (ix == 0) return RampChar(OriCopper, 5);
            if (ix == 1) return RampChar(OriCopper, 2);
            return RampChar(OriCopper, 3 + side);
        }

        // ------------------------------------------------------------------ the sabatons (Boots)
        //
        // LEG-local, art bottom on the sole. A rolled cuff, a ridged shaft, and a foot of three
        // lames over a dark sole.

        const int OriSabatonTop = -32, OriSabatonBottom = -48, OriSabatonHalf = 8;

        static string[] _oriSabatonRows;
        static string[] OriSabatonRows => _oriSabatonRows ??=
            PaintField(-OriSabatonHalf, OriSabatonHalf, OriSabatonBottom, OriSabatonTop, OriSabatonTexel);

        static char OriSabatonTexel(int ix, int iy)
        {
            float x = ix + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 2f, -3f);
            float half = iy >= -35 ? 6f : iy >= -41 ? 5f : 6.5f;
            if (ax > half) return '.';
            if (iy == -33) return RampChar(OriCopper, 5);                          // the cuff's roll
            if (iy == -35) return RampChar(OriCopper, 1);
            if (iy >= -35) return RampChar(OriCopper, 3 + side);
            if (iy <= -47) return RampChar(OriCopper, 0);                          // the sole
            if (iy == -42) return RampChar(OriCopper, 4 + side);                   // the instep's lame
            if (iy == -44) return RampChar(OriCopper, 2);
            if (iy < -42) return RampChar(OriCopper, 3 + side);
            if (ix == 0) return RampChar(OriCopper, 5);                            // the shaft's ridge
            return RampChar(OriCopper, 3 + side);
        }

        // ------------------------------------------------------------------ the coat (Back)
        //
        // TORSO-local texels, the same frame and length as Nocturne's coat, a different cut: a
        // straight A-line from the waist and a SQUARE hem with only its corners eased, where
        // Nocturne's sweeps up. BORDERED, not piped: a broad copper band down each side and along
        // the hem, a strip of yellow, then a thin copper line inside it - the reference's double
        // trim. The vent up the back is bordered too, so from behind it reads as two panels.

        const int OriCoatTop = 48, OriCoatBottom = -52, OriCoatHalf = 36;

        static string[] _oriCoatRows;
        static string[] OriCoatRows => _oriCoatRows ??=
            PaintField(-OriCoatHalf, OriCoatHalf, OriCoatBottom, OriCoatTop, OriCoatTexel);

        static float OriCoatHalfAt(float y)
        {
            if (y >= 30f) return 22f;
            if (y >= 4f) return Mathf.Lerp(22f, 23.5f, (30f - y) / 26f);
            return 23.5f + (4f - y) * 0.20f;
        }

        static float OriCoatTopAt(float ax) => ax <= 8f ? 47f : 47f - (ax - 8f) * 0.36f;

        static float OriCoatHemAt(float ax, float half)
        {
            float corner = Mathf.Max(0f, ax - (half - 3f));
            return -50f + corner * corner * 0.35f;
        }

        static char OriCoatTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            float half = OriCoatHalfAt(y), top = OriCoatTopAt(ax), hem = OriCoatHemAt(ax, half);
            if (ax > half || y > top || y < hem) return '.';

            // the vent: a gap up the back from the hem, each edge bordered
            const float ventTop = -14f;
            float vent = y < ventTop ? (ventTop - y) * 0.05f : -1f;
            if (ax < vent) return '.';
            float fromVent = y < ventTop ? ax - vent : 99f;

            // distance in from the nearest bordered edge: the sides (below the waist), the hem,
            // the vent's edges
            float fromSide = y < 4f ? half - ax : 99f;
            float fromHem = y - hem;
            float inFrom = Mathf.Min(fromSide, Mathf.Min(fromHem, fromVent));
            if (inFrom < 3f) return RampChar(OriCopper, inFrom < 1f ? 2 : (x > 0f ? 4 : 3));   // the band
            if (inFrom >= 4f && inFrom < 5f) return RampChar(OriCopper, 3);                  // the line

            // the shoulders catch the light. Cloth never takes 'H' (CapeRows' rule).
            if (top - y < 2.5f) return RampChar(OriCloth, 4);

            // the waist seam, where the skirt is sewn to the body: from behind, without it the
            // coat was one yellow slab from the collar to the floor
            if (y >= 4f && y < 5.5f) return RampChar(OriCloth, 2);
            // the body, turning from the light across the back
            if (y >= 5.5f) return RampChar(OriCloth, 3 + LitSide(x, 12f, -13f));

            // folds widening toward the hem, as Nocturne's
            float fold = 0f;
            if (y < 8f)
            {
                float u = x / Mathf.Max(half, 1f);
                fold = Mathf.Sin(u * Mathf.PI * 2.5f + 0.9f) * Mathf.Clamp01((8f - y) / 44f);
            }
            // Base yellow, a ridge lit, a trough in ochre. A shade LINE across the whole coat (as
            // Nocturne's has) turned the lower half ochre and the coat into two colours.
            int tone = 3;
            if (fold > 0.55f) tone++;
            else if (fold < -0.6f) tone--;
            if (half - ax < 1.5f && y >= 6f) tone += x > 0f ? 1 : -1;
            return RampChar(OriCloth, Mathf.Clamp(tone, 2, 4));
        }
    }
}
