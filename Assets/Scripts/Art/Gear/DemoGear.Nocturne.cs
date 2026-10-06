using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Nocturne: black plate edged in gold, a grey cape
    //
    // After the user's references - a 3D render and a four-view turnaround of the same swordsman:
    // a tall standing collar; a chest of two gold-edged plates, a shield-shaped panel over a
    // chevron; boxy pauldrons with a band jutting out under each and a short white tassel hanging
    // on the chest from a gold knot; vambraces with a heart-shaped elbow guard and a gold-rimmed
    // cuff over fingerless gloves; a grey belt with a hanging strap and gold-edged plates at the
    // hips; a long CLOSED black coat with white piping; and a GREY cape from the shoulders to the
    // floor. Diamond, power 0, like every reference-built cosmetic set.
    //
    //   nocturne_cuirass    Torso      the collar, the panel over the chevron, and the coat between
    //                                  them and the belt - its two front lines start here
    //   nocturne_pauldrons  Shoulders  a box with a lit top face, a band jutting out under it, and
    //                                  the tassel on its gold knot
    //   nocturne_vambraces  Gloves     sleeve, heart-shaped couter, forearm plate, gold-rimmed cuff,
    //                                  black glove
    //   nocturne_belt       Belt       grey, a gold buckle, a strap hanging from it across the coat
    //   nocturne_coat       Legs       the coat from the waist to the ankles, closed down the front,
    //                                  piped in white, with notched hip plates over it
    //   nocturne_boots      Boots      black, cuff and toe rimmed in gold
    //   nocturne_cape       Back       grey, shoulders to the floor, a light line down each side
    //
    // GOLD ON THE PLATES, WHITE ON THE COAT. The first build read the 3D render's dull trim as
    // silver; the turnaround shows it is gold. The coat's piping is white in both and stays white -
    // two different trims, one for the armour and one for the cloth, so neither reads as the other.
    // Gold is an EDGE, one texel, never a stripe across a plate.
    //
    // The coat's piping and the tassels use two extra palette letters, 'w' and 'v' (white and its
    // shade), past the three ramps Palette.Of carries: the derived menu art shades only the ramps,
    // and a line of white has no surface to shade.
    //
    // Fields in each joint's own frame (DemoGear.Fields.cs), lit from +X like every piece here.
    public static partial class DemoGear
    {
        const string NocPlate = "ksdblh", NocGold = "KSDBLH", NocCloth = "123456";
        const char NocWhite = 'w', NocWhiteShade = 'v';

        static void AddNocturne(List<GearItem> items)
        {
            // Black plate: shadows placed below LIGHT so the seams survive (Palette.Undersuit's
            // reasoning), and a gloss well up the ramp - what separates plate from the black cloth.
            var plate = new Palette.Ramp(new Color(0.18f, 0.19f, 0.23f), lift: 0.32f, shade: 0.34f, line: 0.80f)
                .WithShadowsBelowLight(0.40f, 0.26f);
            // Antique gold, muted - the turnaround's trim is a brown-gold line, not a bright one -
            // lit toward a pale gold rather than white (Palette.Bronze's note on warm metals).
            var gold = new Palette.Ramp(new Color(0.64f, 0.52f, 0.30f), shade: 0.36f)
                .WithHighlightsToward(new Color(1.00f, 0.90f, 0.62f), 0.40f);
            // The coat: a matte navy-black, the silk recipe (Shogun) - never 'H'.
            var cloth = new Palette.Ramp(new Color(0.15f, 0.15f, 0.20f), lift: 0.16f, shade: 0.34f, line: 0.80f)
                .WithShadowsBelowLight(0.48f, 0.32f);
            // The cape and the belt: a cool mid grey, well above the coat so the cape reads as a
            // second cloth from behind and past the coat's edges from the front.
            var grey = new Palette.Ramp(new Color(0.40f, 0.41f, 0.46f), lift: 0.20f, shade: 0.30f, line: 0.75f);

            var pal = NocWithWhite(Palette.Of(plate, gold, cloth), 1f);
            var far = NocWithWhite(Palette.Of(plate.Scaled(0.85f), gold.Scaled(0.82f), cloth.Scaled(0.85f)), 0.85f);
            // The belt draws its band in the cloth's letters; here they are a darker grey.
            var beltPal = Palette.Of(plate, gold, grey.Scaled(0.72f));
            var capePal = NocWithWhite(Palette.Of(grey), 0.92f);

            items.Add(Defends(Make("nocturne_cuirass", "Nocturne Cuirass", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.nocturne", NocCuirassRows, pal,
                       0f, FieldCentreCells(NocCuirassBottom, NocCuirassTop), ppu: BodyPpu)),
                // A duellist's coat over light plate - built to get out of the way.
                DefensiveAbility.Dash));

            items.Add(Make("nocturne_pauldrons", "Nocturne Pauldrons", GearSlot.Shoulders, LootTier.Diamond, 0f,
                Pixels(RigLayer.Shoulders, "gear.pauldron.nocturne", NocPauldronNear, pal,
                       -ShoulderX - NocPauldronCentreCells, NocPauldronY, ppu: BodyPpu),
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.nocturne.dark", NocPauldronFar, far,
                       ShoulderX + NocPauldronCentreCells, NocPauldronY, ppu: BodyPpu)));

            items.Add(Make("nocturne_vambraces", "Nocturne Vambraces", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesFront, "gear.gloves.nocturne", NocVambraceRows, pal,
                       0f, FieldCentreCells(NocVambraceBottom, NocVambraceTop), ppu: BodyPpu),
                Pixels(RigLayer.GlovesBack, "gear.gloves.nocturne.dark", NocVambraceRows, far,
                       0f, FieldCentreCells(NocVambraceBottom, NocVambraceTop), ppu: BodyPpu)));

            items.Add(Make("nocturne_belt", "Nocturne Belt", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.nocturne", NocBeltRows, beltPal,
                       0f, FieldCentreCells(NocBeltBottom, NocBeltTop), ppu: BodyPpu)));

            items.Add(Make("nocturne_coat", "Nocturne Coat", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.Tasset, "gear.legs.nocturne.coat", NocCoatRows, pal,
                       0f, FieldCentreCells(NocCoatBottom, NocCoatTop), ppu: BodyPpu)));

            items.Add(Make("nocturne_boots", "Nocturne Boots", GearSlot.Boots, LootTier.Diamond, 0f,
                Pixels(RigLayer.BootsFront, "gear.boots.nocturne", NocBootRows, pal,
                       0f, FieldCentreCells(NocBootBottom, NocBootTop), ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.nocturne.dark", NocBootRows, far,
                       0f, FieldCentreCells(NocBootBottom, NocBootTop), ppu: BodyPpu)));

            var cape = Make("nocturne_cape", "Nocturne Cape", GearSlot.Back, LootTier.Diamond, 0f,
                Pixels(RigLayer.Back, "gear.back.nocturne", NocCapeRows, capePal,
                       0f, FieldCentreCells(NocCapeBottom, NocCapeTop), ppu: BodyPpu));
            // Floor length: at a short cape's full angle the hem sweeps half a body sideways
            // (the Hellspawn Cape's number).
            cape.CapeSwingScale = 0.45f;
            items.Add(cape);

            Dyeable(items, "nocturne_", DyeChannel.Of("Plate", DyeMaterial.Metal, plate, 0.85f),
                    DyeChannel.Of("Trim", DyeMaterial.Metal, gold, 0.82f));
            // The cape is its own grey cloth, which neither channel reaches - so it gets its own.
            Dyeable(items, "nocturne_cape", DyeChannel.Of("Cloth", DyeMaterial.Cloth, grey), null);
        }

        /// <summary>The white piping and the tassels - see the header on why they sit outside the
        /// three ramps.</summary>
        static Dictionary<char, Color> NocWithWhite(Dictionary<char, Color> pal, float f)
        {
            pal[NocWhite] = new Color(0.92f * f, 0.93f * f, 0.95f * f);
            pal[NocWhiteShade] = new Color(0.66f * f, 0.68f * f, 0.73f * f);
            return pal;
        }

        // ------------------------------------------------------------------ the cuirass (Torso)
        //
        // Torso-local. A tall black collar, a light seam down its front. The chest is TWO plates:
        // a shield-shaped panel under the collar, gold all round and coming to a shallow V, and
        // under it a CHEVRON - a band whose gold lower edge points down at the sternum - which
        // also runs out under the pauldrons to the shoulders. Under the chevron the coat shows to
        // the waist, with the two white lines that run on down the coat's front (NocFrontEdge).
        //
        // The panel is narrow enough that its gold sides stand clear of the pauldrons' outline
        // (their inner edge, outlined, is 7 out). No emblem - the user left the chest marks off.

        const int NocCuirassTop = 44, NocCuirassBottom = 6, NocCuirassHalf = 16;

        /// <summary>The coat's front lines, |x| in texels: the white line's inner side. The coat
        /// below the belt carries them on from here. At 4 the near arm's outline, hanging in
        /// front of the torso, covered the near line from the elbow down.</summary>
        const float NocFrontEdge = 3f;

        static string[] _nocCuirassRows;
        static string[] NocCuirassRows => _nocCuirassRows ??=
            PaintField(-NocCuirassHalf, NocCuirassHalf, NocCuirassBottom, NocCuirassTop, NocCuirassTexel);

        const float NocPanelHalf = 6.5f, NocPanelTop = 34f;
        static float NocPanelBottom(float ax) => 26f + ax * 0.5f;
        static float NocChevronBottom(float ax) => 18f + ax * 0.42f;

        /// <summary>The white piping: two texels, white then its shade - the reference's lines are
        /// doubled, and at one screen pixel a pair reads as one line with a lit edge.</summary>
        static char NocPiping(float distIn) => distIn < 1f ? NocWhite : NocWhiteShade;

        static char NocCuirassTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 7f, -8f);

            // ---- the collar ----
            float collarHalf = 4.5f + (y - NocPanelTop) * 0.25f;
            if (y >= NocPanelTop && ax <= collarHalf)
            {
                if (ix == 0 && iy >= 37) return NocWhiteShade;                              // the seam
                if (ax > collarHalf - 1f) return RampChar(NocPlate, 1);                     // its edge
                return RampChar(NocPlate, 2 + LitSide(x, 1f, -2f));
            }

            // ---- the panel ----
            float panelBottom = NocPanelBottom(ax);
            if (y < NocPanelTop && y >= panelBottom && ax <= NocPanelHalf)
            {
                if (y >= NocPanelTop - 1f || ax > NocPanelHalf - 1f || y < panelBottom + 1.2f)
                    return RampChar(NocGold, 4 + Mathf.Max(side, 0));
                if (x > 1.5f && x < 4f && y > panelBottom + 2f) return RampChar(NocPlate, 4);   // gloss
                return RampChar(NocPlate, 3 + LitSide(x, 3f, -3f));
            }

            // ---- the chevron, and the plate under the pauldrons ----
            float top = ax <= 7f ? NocPanelTop : NocPanelTop - (ax - 7f) * 0.40f;
            float chev = NocChevronBottom(ax);
            float half = iy >= 28 ? 14f : 13.5f;
            if (y < top && y >= chev && ax <= half)
            {
                if (y < chev + 1.2f) return RampChar(NocGold, 3 + side);                    // its edge
                if (ax > NocPanelHalf && y >= top - 1f) return RampChar(NocGold, 4);         // the shoulder line
                // in the panel's shadow just under it, lit lower down, the edge falling away
                int tone = y > panelBottom - 2f && ax <= NocPanelHalf + 1f ? 2 : 3;
                return RampChar(NocPlate, tone + side - (y < chev + 3f ? 1 : 0));
            }

            // ---- the coat, from the chevron to the waist ----
            if (y < chev && ax <= 13f)
            {
                if (ax >= NocFrontEdge && ax < NocFrontEdge + 2f) return NocPiping(ax - NocFrontEdge);
                return RampChar(NocCloth, 3 + side - (ax < NocFrontEdge ? 1 : 0));
            }
            return '.';
        }

        // ------------------------------------------------------------------ the pauldrons (Shoulders)
        //
        // From the turnaround: a BOX - one plate, its top face catching the light over a darker
        // front face, its outer top corner standing a little above the chin - and under it ONE
        // BAND jutting further out than the box, gold along its top, its bottom and its outer end.
        // The band is the widest point of the shoulder from both front and back. (The first two
        // passes stacked lames, stepping out and then in; the reference has the one band.)
        //
        // The TASSEL hangs from a gold diamond knot at the box's inner bottom corner: a white cord
        // and a short fringe, on the chest beside the arm, as in every view of the reference.
        //
        // Outward offsets from the joint, mirrored for the far side. The grid's top row is
        // NocPauldronRise above the chin; only the outer corner reaches it - the inner end starts on
        // the chin as every pauldron does (the head covers the rest). A CAP, not HangsAlongArm:
        // wider than tall, it would stand up beside a raised arm as a slab.

        const int NocPauldronRows = 22, NocPauldronMin = -6, NocPauldronMax = 16;
        const int NocPauldronRise = 2;
        static float NocPauldronCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((NocPauldronMin + NocPauldronMax) * 0.5f);
        static float NocPauldronY
            => PlateY(NocPauldronNear) + PrimitiveCharacterRig.Proportions.Cells(NocPauldronRise);

        static string[] _nocPauldronNear, _nocPauldronFar;
        static string[] NocPauldronNear => _nocPauldronNear ??= NocPauldron(outwardIsPlusX: false);
        static string[] NocPauldronFar => _nocPauldronFar ??= NocPauldron(outwardIsPlusX: true);

        static string[] NocPauldron(bool outwardIsPlusX)
            => PaintField(NocPauldronMin, NocPauldronMax, 0, NocPauldronRows, (ix, iy) =>
            {
                int o = outwardIsPlusX ? ix : NocPauldronMin + NocPauldronMax - 1 - ix;
                return NocPauldronTexel(o, NocPauldronRows - 1 - iy, outwardIsPlusX);
            });

        /// <summary>The box's top edge, in rows: on the chin at the inner end, rising to the
        /// grid's top at the outer corner.</summary>
        static float NocPauldronTop(float oc)
            => NocPauldronRise * (1f - Mathf.Clamp01((oc - 4f) / 7f));

        /// <param name="o">Outward offset from the joint's column, in texels.</param>
        /// <param name="r">Row from the top.</param>
        static char NocPauldronTexel(int o, int r, bool outwardIsPlusX)
        {
            float oc = o + 0.5f, rc = r + 0.5f;
            float towardLight = outwardIsPlusX ? oc : -oc;
            int side = towardLight > 6f ? 1 : towardLight < -8f ? -1 : 0;

            // ---- the knot: a dark diamond rimmed in gold, on the box's inner corner ----
            // Solid gold over the white cord, it read as a candle's flame.
            {
                float dk = Mathf.Abs(oc + 4f) + Mathf.Abs(rc - 11.5f);
                if (dk <= 2.6f) return dk <= 1.1f ? RampChar(NocPlate, 1) : RampChar(NocGold, rc < 11.5f ? 5 : 3);
            }

            // ---- the tassel under it: a cord, then a fringe a texel wider each side ----
            if (r >= 14 && r < 21)
            {
                bool cord = (o == -5 || o == -4) && r < 18;
                bool fringe = o >= -6 && o <= -3 && r >= 18;
                if (cord || fringe)
                    return r == 20 || (fringe && (o == -6 || o == -3)) ? NocWhiteShade : NocWhite;
            }

            // ---- the box ----
            float top = NocPauldronTop(oc);
            const float boxInner = -5f, boxOuter = 12.5f;
            if (rc >= top && r < 12 && oc >= boxInner && oc <= boxOuter)
            {
                float down = rc - top;
                if (down < 1f || oc > boxOuter - 1f) return RampChar(NocGold, 4);          // top edge, outer edge
                if (down < 4f) return RampChar(NocPlate, Mathf.Min(4 + side, 5));          // the top face
                if (down < 5f) return RampChar(NocPlate, 1);                               // where it turns
                return RampChar(NocPlate, 3 + side - (r >= 10 ? 1 : 0));                    // the front face
            }

            // ---- the band, jutting past the box: rows 12..17 ----
            const float bandInner = -2.5f, bandOuter = 15.5f;
            if (r >= 12 && r < 18 && oc >= bandInner && oc <= bandOuter)
            {
                if (r == 12 || r == 17 || oc > bandOuter - 1f) return RampChar(NocGold, r == 17 ? 3 + side : 4);
                return RampChar(NocPlate, (r == 13 ? 4 : r == 16 ? 2 : 3) + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the vambraces (Gloves)
        //
        // ARM-local: 0 at the shoulder, the elbow cut at -14, the wrist at -22, the hand below. A
        // black sleeve, its rows UNIFORM down to the cut - the rig lengthens the upper arm by
        // repeating the row just above the elbow band (-12), so anything drawn there would repeat
        // into a stripe. Below the cut, so it bends with the forearm: a HEART-shaped couter, gold
        // all round - two lobes on top, a point below - the turnaround's elbow; a black forearm
        // plate; a cuff railed in gold top and bottom; and a black glove.

        const int NocVambraceTop = 0, NocVambraceBottom = -32, NocVambraceHalf = 7;

        static string[] _nocVambraceRows;
        static string[] NocVambraceRows => _nocVambraceRows ??=
            PaintField(-NocVambraceHalf, NocVambraceHalf, NocVambraceBottom, NocVambraceTop, NocVambraceTexel);

        static bool NocCouterIn(float x, float y)
        {
            float ax = Mathf.Abs(x);
            float top = -14.5f - (ax < 1.5f ? 1f : 0f);              // the dip between the lobes
            float bottom = -18f - Mathf.Max(0f, 6f - ax) * 0.45f;    // the point below
            return ax <= 6f && y <= top && y >= bottom;
        }

        static char NocVambraceTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 2.5f, -3f);

            // The sleeve.
            if (iy >= -14 && iy < -1)
                return ax > 5.5f ? '.' : RampChar(NocCloth, 3 + side);

            // The couter.
            if (NocCouterIn(x, y))
            {
                bool rim = !NocCouterIn(x - 1f, y) || !NocCouterIn(x + 1f, y)
                        || !NocCouterIn(x, y - 1f) || !NocCouterIn(x, y + 1f);
                if (rim) return RampChar(NocGold, y > -16f ? 4 : 3 + side);
                return RampChar(NocPlate, (y > -17f ? 4 : 3) + side);
            }
            // The forearm plate, under the couter's point.
            if (iy >= -22 && iy < -14)
            {
                if (ax > 5f) return '.';
                return RampChar(NocPlate, (ix == 1 ? 4 : 3) + side);
            }
            // The cuff. No wider than the couter: the near arm hangs in front of the torso, and
            // a wider cuff's outline reached the coat's front line.
            if (iy >= -25 && iy < -22)
            {
                if (ax > 6f) return '.';
                if (iy == -24) return RampChar(NocPlate, 2 + side);
                return RampChar(NocGold, iy == -23 ? 4 + Mathf.Max(side, 0) : 3 + side);
            }
            // The glove: a lit knuckle row, the fingers in shadow.
            if (iy >= NocVambraceBottom)
            {
                float half = iy < -29 ? 4.5f : 5.5f;
                if (ax > half) return '.';
                if (iy == -27) return RampChar(NocCloth, 4 + side);
                return RampChar(NocCloth, (iy < -29 ? 2 : 3) + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the belt
        //
        // On BeltY's band: a grey belt (the cloth letters, which this item's palette maps to grey),
        // a gold-rimmed buckle at the front, and a strap hanging from under the buckle down across
        // the coat to a gold tip - the one diagonal on a figure otherwise all verticals.

        const int NocBeltTop = 12, NocBeltBottom = -14, NocBeltHalf = 16;

        static string[] _nocBeltRows;
        static string[] NocBeltRows => _nocBeltRows ??=
            PaintField(-NocBeltHalf, NocBeltHalf, NocBeltBottom, NocBeltTop, NocBeltTexel);

        static char NocBeltTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f;

            // ---- the buckle ----
            if (ix >= 1 && ix < 6 && iy >= 3 && iy < 10)
            {
                bool rim = ix == 1 || ix == 5 || iy == 3 || iy == 9;
                return rim ? RampChar(NocGold, iy >= 8 ? 5 : iy <= 4 ? 3 : 4) : RampChar(NocPlate, 1);
            }

            // ---- the strap: from under the buckle, down and back across the coat ----
            float t = (4f - y) / 17f;                                // 0 at the buckle, 1 at the tip
            float cx = Mathf.Lerp(3.5f, -4.5f, t);
            if (t >= 0f && t <= 1f && Mathf.Abs(x - cx) < 1.6f)
            {
                if (iy < -11) return RampChar(NocGold, iy == -12 ? 4 : 3);                // the tip
                return RampChar(NocCloth, x > cx ? 4 : 2);
            }

            // ---- the band ----
            if (iy >= 4 && iy < 9)
            {
                float half = (iy == 4 || iy == 8) ? 15f : 15.5f;
                if (Mathf.Abs(x) > half) return '.';
                int tone = iy switch { 8 => 4, 4 => 2, _ => 3 };
                return RampChar(NocCloth, tone + LitSide(x, 10f, -11f));
            }
            return '.';
        }

        // ------------------------------------------------------------------ the coat (Legs)
        //
        // TORSO-local on RigLayer.Tasset - hung from the belt, the legs striding under it, as the
        // Shogun robe's skirt is. CLOSED down the front (the turnaround's coat never opens; the
        // first build split it and showed the legs) and long - the hem at the ankles, the lower
        // boots showing under it.
        //
        // The piping, white: the two front lines carry straight on from the cuirass to the hem,
        // and from the knee a line on each side branches OUT, runs a few texels on the slant and
        // drops to the hem - the reference's coat panels.
        //
        // Over the coat at each hip, a black PLATE hung from the belt, gold-edged, its outer edge
        // and hem NOTCHED in steps - the turnaround's tassets, which every view shows.

        const int NocCoatTop = 6, NocCoatBottom = -44, NocCoatHalf = 24;

        static string[] _nocCoatRows;
        static string[] NocCoatRows => _nocCoatRows ??=
            PaintField(-NocCoatHalf, NocCoatHalf, NocCoatBottom, NocCoatTop, NocCoatTexel);

        static float NocCoatOuter(float y) => 14f + Mathf.Clamp01((NocCoatTop - y) / 48f) * 7f;
        static float NocCoatHem(float ax) => -41f - Mathf.Clamp01((ax - 6f) / 14f) * 1.5f;

        /// <summary>Where a side panel's line runs, |x|: from the front line at the knee, out on
        /// the slant, then straight down.</summary>
        static float NocPanelLine(float y) => NocFrontEdge + 2f + Mathf.Clamp01((-17f - y) / 6f) * 6f;

        static bool NocTassetIn(float x, float y)
        {
            float ax = Mathf.Abs(x);
            float outer = 16.5f - (y < -3f ? 1.5f : 0f) - (y < -9f ? 2f : 0f);    // notched in, twice
            float hem = ax > 12.5f ? -11f : -14f;                                    // and stepped up outside
            return ax >= 8f && ax <= outer && y <= 4f && y >= hem;
        }

        static char NocCoatTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 12f, -13f);

            // ---- the hip plates ----
            if (NocTassetIn(x, y))
            {
                bool rim = !NocTassetIn(x - 1f, y) || !NocTassetIn(x + 1f, y)
                        || !NocTassetIn(x, y - 1f) || !NocTassetIn(x, y + 1f);
                if (rim) return RampChar(NocGold, y > 0f ? 4 : 3 + Mathf.Max(side, 0));
                return RampChar(NocPlate, (y > 1f ? 4 : 3) + side);
            }

            float outer = NocCoatOuter(y), hem = NocCoatHem(ax);
            if (ax > outer || y < hem) return '.';

            // ---- the piping ----
            if (ax >= NocFrontEdge && ax < NocFrontEdge + 2f) return NocPiping(ax - NocFrontEdge);
            if (y < -17f)
            {
                float line = NocPanelLine(y);
                if (ax >= line && ax < line + 2f) return NocPiping(ax - line);
            }

            // ---- the cloth ----
            if (y < hem + 2f) return RampChar(NocCloth, 1);
            return RampChar(NocCloth, 3 + side - (ax < NocFrontEdge ? 1 : 0));
        }

        // ------------------------------------------------------------------ the boots (Boots)
        //
        // LEG-local, art bottom on the sole (BootY's rule). A folded cuff standing proud of the
        // shaft with a gold rim, a lit shaft, and a toe rimmed in gold over a dark sole.

        const int NocBootTop = -32, NocBootBottom = -48, NocBootHalf = 8;

        static string[] _nocBootRows;
        static string[] NocBootRows => _nocBootRows ??=
            PaintField(-NocBootHalf, NocBootHalf, NocBootBottom, NocBootTop, NocBootTexel);

        static char NocBootTexel(int ix, int iy)
        {
            float x = ix + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 2f, -3f);
            float half = iy >= -37 ? 6f : iy >= -43 ? 5f : 6.5f;
            if (ax > half) return '.';
            if (iy == -33) return RampChar(NocGold, 4 + side);                      // the cuff's rim
            if (iy >= -37) return RampChar(NocPlate, iy == -37 ? 1 : 3 + side);      // the cuff
            if (iy == -43) return RampChar(NocGold, 3 + side);                      // the toe's rim
            if (iy <= -47) return RampChar(NocPlate, 1);                            // the sole
            if (iy < -43) return RampChar(NocPlate, 3 + side);
            return RampChar(NocPlate, (x > 1f && x < 3f ? 4 : 3) + side);           // a lit shin
        }

        // ------------------------------------------------------------------ the cape (Back)
        //
        // TORSO-local texels. GREY, from the shoulders to the floor - the turnaround's back view.
        // Sloping off behind the head (CapeRows' rule: square corners above the shoulder line read
        // from behind as shoulders three cells too high), between the pauldrons at the top, and
        // flaring a little to the hem, which is near straight. A thin light line runs down each
        // side a couple of texels in, the reference's edge. From the front it shows past the
        // coat on either side, as the grey does in the reference's front view.
        //
        // Broad fan folds (the Hellspawn's), and the two long creases the reference draws running
        // out and down from the collar - long strokes, not CapeRows' dashes, which read as squares.

        const string NocCape = "ksdblh";
        const int NocCapeTop = 48, NocCapeBottom = -52, NocCapeHalf = 32;

        static string[] _nocCapeRows;
        static string[] NocCapeRows => _nocCapeRows ??=
            PaintField(-NocCapeHalf, NocCapeHalf, NocCapeBottom, NocCapeTop, NocCapeTexel);

        static float NocCapeHalfAt(float y) => 20f + Mathf.Max(0f, 30f - y) * 0.125f;
        static float NocCapeTopAt(float ax) => ax <= 6f ? 46f : 46f - (ax - 6f) * 0.36f;

        static char NocCapeTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            float half = NocCapeHalfAt(y), top = NocCapeTopAt(ax);
            float hem = -50f + Mathf.Pow(Mathf.Clamp01(ax / half), 4f) * 2f;
            if (ax > half || y > top || y < hem) return '.';

            // the light line down each side
            if (y < 38f && ax >= half - 3f && ax < half - 2f) return NocWhite;

            // the hem's underside
            if (y < hem + 2f) return RampChar(NocCape, 1);
            // the shoulders catch the light
            if (top - y < 2f) return RampChar(NocCape, 4);

            // the two creases out from the collar: a dark stroke with a lit lip above it
            float along = (44f - y) / 16f;                            // 0 at the collar, 1 at its end
            if (along > 0f && along < 1f)
            {
                float cx = Mathf.Lerp(4f, 13f, along);
                float d = ax - cx;
                if (d >= 0f && d < 1f) return RampChar(NocCape, 1);
                if (d >= -1f && d < 0f) return RampChar(NocCape, 4);
            }

            // folds widening toward the hem
            float fold = 0f;
            if (y < 10f)
            {
                float u = x / Mathf.Max(half, 1f);
                fold = Mathf.Sin(u * Mathf.PI * 2.5f + 0.9f) * Mathf.Clamp01((10f - y) / 50f);
            }
            int tone = 3;
            if (fold > 0.55f) tone++;
            else if (fold < -0.6f) tone--;
            if (half - ax < 2f) tone += x > 0f ? 1 : -1;                // lit edge toward the light
            return RampChar(NocCape, Mathf.Clamp(tone, 1, 4));
        }
    }
}
