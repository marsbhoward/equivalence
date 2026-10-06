using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Tepes: black plate scrolled in gold, a torn navy cape
    //
    // After the user's "General of Tepes" reference, as close to one-to-one as the density allows
    // (the reference's red under-glow left off, the user's call): a knight in near-black plate with
    // a violet cast, every plate edged in antique gold; a high layered gorget with a brooch at the
    // throat; a gilt medallion at the chest with a gold spine running down from it; ONE big spiked
    // pauldron, scrolled in gold, on the SWORD arm; shield-shaped tassets over the thighs either
    // side of a pointed centre plate, a navy loincloth hanging between them, torn below the knee;
    // big knee cops; and a floor-length navy cape, shredded at the hem and torn through, its FUR
    // MANTLE thrown over the other shoulder. The sword and the hair are not armour.
    //
    //   tepes_cuirass    Torso      a three-band gorget and its brooch, a yoke rimmed in gold, the
    //                               chest medallion and the gold spine under it to the waist
    //   tepes_pauldrons  Shoulders  the SPIKED pauldron (near, the sword arm): a dome scrolled in
    //                               gold, one tall spike by the neck and two along its outer rim,
    //                               two gilt-hemmed lames below; a smaller one-spike dome (far)
    //   tepes_gauntlets  Gloves     lamed rerebrace, a pointed couter, a vambrace banded in gold,
    //                               a flared gilt cuff, a lamed fist
    //   tepes_belt       Belt       a plate band between two gold lines, a gilt clasp
    //   tepes_tassets    Legs       the pointed centre plate, a shield tasset over each thigh, the
    //                               navy loincloth between them, torn into strips below the knee
    //   tepes_greaves    Boots      cuisse, a big gilt-rimmed knee cop, greave, sabaton
    //   tepes_cape       Back       navy, floor length, shredded and torn through, and its fur
    //                               mantle over the shoulder opposite the sword arm (BackOver)
    //
    // THE REFERENCE'S SPLIT, KEPT: its spiked pauldron is on the SWORD arm and its cape drapes the
    // other shoulder. Facing the camera the sword arm is the NEAR one (-X) - the viewer's left, as
    // in the picture - and the rig already mirrors a Back item's drape onto the shoulder opposite
    // the carry (SyncDrapeSide). So the pauldron is authored near and Lopsided: turned away, the
    // carry moves to arm.back, and the pauldron trades sides with it. (Talon, Errant and Herald put
    // their showpiece on the far arm instead; flipping this one is swapping the two layers.)
    //
    // THE GOLD IS AN EDGE (Sovereign's rule), with three exceptions the reference insists on: the
    // brooch, the chest medallion and its spine, and the scroll on the pauldron's dome.
    //
    // Fields in each joint's own frame (DemoGear.Fields.cs), lit from +X like every piece here.
    // Diamond, power 0, like every reference-built cosmetic set.
    public static partial class DemoGear
    {
        const string TepPlate = "ksdblh", TepGold = "KSDBLH", TepCloth = "123456";

        static void AddTepes(List<GearItem> items)
        {
            // Black plate with a violet cast, the reference's; shadows placed below LIGHT so the
            // seams survive (Nocturne's black), and lit toward a cold lavender rather than white.
            var plate = new Palette.Ramp(new Color(0.20f, 0.18f, 0.23f), lift: 0.34f, shade: 0.34f, line: 0.80f)
                .WithShadowsBelowLight(0.42f, 0.26f)
                .WithHighlightsToward(new Color(0.72f, 0.70f, 0.84f), 0.36f);
            // Antique gold, muted - the reference's trim is tarnished, not bright - lit toward pale
            // gold (Bronze's note on warm metals lit toward white).
            var gold = new Palette.Ramp(new Color(0.64f, 0.51f, 0.30f), shade: 0.38f)
                .WithHighlightsToward(new Color(0.98f, 0.88f, 0.60f), 0.40f);
            // Navy: indigo, lit toward a cooler blue. Cloth stays off the glow end (CapeRows' note).
            var navy = new Palette.Ramp(new Color(0.20f, 0.21f, 0.44f), lift: 0.26f, shade: 0.36f, line: 0.80f)
                .WithShadowsBelowLight(0.50f, 0.30f)
                .WithHighlightsToward(new Color(0.46f, 0.50f, 0.86f), 0.30f);
            const float farF = 0.85f;

            var pal = Palette.Of(plate, gold, navy);
            var far = Palette.Of(plate.Scaled(farF), gold.Scaled(farF), navy.Scaled(farF));

            items.Add(Defends(Make("tepes_cuirass", "Tepes Cuirass", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.tepes", TepCuirassRows, pal,
                       0f, FieldCentreCells(TepCuirassBottom, TepCuirassTop), ppu: BodyPpu)),
                // A general's plate - meet the blow and turn it.
                DefensiveAbility.ParryStance));

            var pauldrons = Make("tepes_pauldrons", "Tepes Pauldrons", GearSlot.Shoulders, LootTier.Diamond, 0f,
                Pixels(RigLayer.Shoulders, "gear.pauldron.tepes", TepPauldronNear, pal,
                       -ShoulderX - TepPauldronCentreCells, TepPauldronY(TepPauldronNear), ppu: BodyPpu),
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.tepes.dark", TepPauldronFar, far,
                       ShoulderX + TepPauldronCentreCells, TepPauldronY(TepPauldronFar), ppu: BodyPpu));
            pauldrons.Lopsided = true;
            items.Add(pauldrons);

            items.Add(Make("tepes_gauntlets", "Tepes Gauntlets", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesFront, "gear.gloves.tepes", TepArmRows, pal,
                       0f, FieldCentreCells(TepArmBottom, TepArmTop), ppu: BodyPpu),
                Pixels(RigLayer.GlovesBack, "gear.gloves.tepes.dark", TepArmRows, far,
                       0f, FieldCentreCells(TepArmBottom, TepArmTop), ppu: BodyPpu)));

            items.Add(Make("tepes_belt", "Tepes Belt", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.tepes", TepBeltRows, pal,
                       0f, FieldCentreCells(TepBeltBottom, TepBeltTop), ppu: BodyPpu)));

            items.Add(Make("tepes_tassets", "Tepes Tassets", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.Tasset, "gear.legs.tepes", TepSkirtRows, pal,
                       0f, FieldCentreCells(TepSkirtBottom, TepSkirtTop), ppu: BodyPpu)));

            items.Add(Make("tepes_greaves", "Tepes Greaves", GearSlot.Boots, LootTier.Diamond, 0f,
                Pixels(RigLayer.BootsFront, "gear.boots.tepes", TepGreaveRows, pal,
                       0f, FieldCentreCells(TepGreaveBottom, TepGreaveTop), ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.tepes.dark", TepGreaveRows, far,
                       0f, FieldCentreCells(TepGreaveBottom, TepGreaveTop), ppu: BodyPpu)));

            var cape = Make("tepes_cape", "Tepes Cape", GearSlot.Back, LootTier.Diamond, 0f,
                Pixels(RigLayer.Back, "gear.back.tepes", TepCapeRows, pal,
                       0f, (TepCapeTop + TepCapeBottom) / 2f, ppu: BodyPpu),
                // The fur mantle: authored on -X, mirrored by the rig onto the shoulder OPPOSITE the
                // sword arm and bent with the cape (SyncDrapeSide, DrapeSwingsWithCape).
                Pixels(RigLayer.BackOver, "gear.back.tepes.mantle", TepMantleRows, pal,
                       (TepMantleMinX + TepMantleMaxX) / 2f, (TepMantleTop + TepMantleBottom) / 2f, ppu: BodyPpu));
            // PixelsCore, like the Wraithguard's: a back view is not one of the item's own layers,
            // so it stays out of _sources and the menu-art pass.
            var ink = Palette.Of(plate, gold, navy);
            ink['o'] = Palette.Outline;
            cape.DrapeBack = PixelsCore(RigLayer.BackOver, "gear.back.tepes.mantle.back", TepMantleBackRows,
                ink, (TepMantleMinX + TepMantleMaxX) / 2f,
                (TepMantleTop + TepMantleBottom) / 2f, ppu: BodyPpu, outline: false);
            cape.DrapeSwingsWithCape = true;
            // Hinged at the neck, measured from the DRAWN top (the Hellspawn Cape's rule), and swung
            // at a floor-length cape's fraction.
            cape.CapeHingeCells = TepCapeTop + PrimitiveCharacterRig.Proportions.Cells(PixelSprite.OutlineCanvasPadFor(BodyPpu))
                                  - PrimitiveCharacterRig.Proportions.NeckYCells;
            cape.CapeSwingScale = 0.45f;
            items.Add(cape);

            Dyeable(items, "tepes_", DyeChannel.Of("Plate", DyeMaterial.Metal, plate, farF),
                    DyeChannel.Of("Cloth", DyeMaterial.Cloth, navy, farF));
        }

        /// <summary>True where <paramref name="inside"/> holds at (x, y) but at one of its four
        /// neighbours a texel away does not - a shape's own one-texel rim (VerRim, in texels).</summary>
        static bool TepRim(System.Func<float, float, bool> inside, float x, float y, float step = 1f)
            => !inside(x - step, y) || !inside(x + step, y) || !inside(x, y - step) || !inside(x, y + step);

        // ------------------------------------------------------------------ the cuirass (Torso)
        //
        // Torso-local, the waist at 0, the chin at 40. Top to bottom:
        //
        //   the GORGET     three bands stepping out as they fall, each lit along its top, a gilt
        //                  BROOCH on the lowest at the throat
        //   the YOKE       across the upper chest, its lower edge a gold rim dipping to the centre
        //   the BREAST     sculpted, lit high on the +X side, a shadow under each breast; the gilt
        //                  MEDALLION at the centre and a gold SPINE falling from it to a point at
        //                  the waist, widening once into a lozenge on the way
        //   the ABDOMEN    plain plate down to the belt
        //
        // The +X side stops at the body's edge (TalBodyEdge): the far arm hangs half behind it.

        const int TepCuirassTop = 46, TepCuirassBottom = 4, TepCuirassHalf = 16;

        static string[] _tepCuirassRows;
        static string[] TepCuirassRows => _tepCuirassRows ??=
            PaintField(-TepCuirassHalf, TepCuirassHalf, TepCuirassBottom, TepCuirassTop, TepCuirassTexel);

        static float TepGorgetBottom(float ax) => 32.5f + ax * 0.12f;
        /// <summary>The yoke's gilt rim: a V from under the arms down to the medallion - the reference's
        /// necklace line.</summary>
        static float TepYokeBottom(float ax) => 25.2f + ax * 0.58f;

        static bool TepBroochIn(float x, float y)
        {
            float dx = x / 2.4f, dy = (y - 34.5f) / 1.8f;
            return dx * dx + dy * dy < 1f;
        }

        static readonly Vector2 TepMedallion = new(0f, 23f);
        const float TepMedallionR = 2.9f;

        static bool TepMedallionIn(float x, float y)
            => (new Vector2(x, y) - TepMedallion).sqrMagnitude < TepMedallionR * TepMedallionR;

        /// <summary>The spine's half-width at y: two texels, swelling into a lozenge, ending in a point.</summary>
        static float TepSpineHalf(float y)
        {
            if (y > TepMedallion.y - TepMedallionR + 0.5f || y < 6f) return 0f;
            // Plain, tapering to a point: a lozenge on the way (the first pass) made the
            // medallion and spine a chess pawn.
            float half = 1f;
            if (y < 10f) half *= (y - 6f) / 4f;                                          // the point
            return half;
        }

        static char TepCuirassTexel(int ix, int iy)
        {
            const string P = TepPlate, G = TepGold;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 5f, -6f);

            float top = 40.5f - Mathf.Max(0f, ax - 6f) * 0.45f;
            float half = x < 0f ? (iy >= 26 ? 13.5f : 13f) : TalBodyEdge(y);
            if (y >= top || ax > half) return '.';

            // ---- the gorget: three bands ----
            float gorget = TepGorgetBottom(ax);
            if (y >= gorget)
            {
                if (TepBroochIn(x, y))
                {
                    if (TepRim(TepBroochIn, x, y)) return RampChar(G, x + (y - 34.5f) > 0.5f ? 4 : 2);
                    return RampChar(G, x > 0f ? 5 : 4);
                }
                float band = (y - gorget) / 2.6f;
                float within = band - Mathf.Floor(band);
                if (within < 0.38f) return RampChar(P, 1);                               // the shadow under the band above
                if (within > 0.72f) return RampChar(P, 4 + Mathf.Max(side, 0));          // each band's lit top
                return RampChar(P, 2 + Mathf.Max(side, 0));
            }
            if (y >= gorget - 1f) return RampChar(P, 0);                                 // the gorget's shadow

            // ---- the yoke ----
            float yoke = TepYokeBottom(ax);
            if (y >= yoke)
            {
                if (y < yoke + 1f) return RampChar(G, x > 0f ? 4 : 3);                    // the gold rim
                if (ax > 11f) return RampChar(P, 2);                                     // turning under the arm
                return RampChar(P, (gorget - 1f - y < 1.2f ? 2 : 3) + side);
            }

            // ---- the medallion and the spine ----
            if (TepMedallionIn(x, y))
            {
                var d = new Vector2(x, y) - TepMedallion;
                if (d.sqrMagnitude > (TepMedallionR - 1f) * (TepMedallionR - 1f))
                    return RampChar(G, d.x + d.y > 0f ? 4 : 2);                          // its rim, lit up-right
                return RampChar(G, d.x + d.y > 0f ? 5 : 3);
            }
            float spine = TepSpineHalf(y);
            if (spine > 0f && ax < spine)
                return RampChar(G, x >= 0f ? 4 : 3);
            if (spine > 0f && ax < spine + 1f && x < 0f) return RampChar(P, 1);          // its shadow, the unlit side

            // ---- the breast ----
            if (y >= 13f)
            {
                // Under each breast: a soft shadow curving up toward the sides.
                float bx = ax - 5.5f, under = 15.5f + bx * bx * 0.07f;
                if (ax > 1.5f && ax < 11f && y < under + 1f && y >= under) return RampChar(P, 1);
                if (ax > 11.5f) return RampChar(P, 2);                                   // turning under the arm
                // A highlight high on each breast, the lit one brighter.
                float hx = x - (x > 0f ? 5.5f : -5.5f), hy = y - 21.5f;
                if (x > 0f && hx * hx * 0.8f + hy * hy < 2.6f) return RampChar(P, 4);
                return RampChar(P, (y < under + 2f ? 2 : 3) + side);
            }

            // ---- the abdomen ----
            //
            // No lame line: it sat on the belt's top edge and, with the belt's outline, read as a
            // black gap between cuirass and belt.
            if (ax > 10.5f) return RampChar(P, 2);
            return RampChar(P, 3 + side);
        }

        // ------------------------------------------------------------------ the pauldrons (Shoulders)
        //
        // A DOME over the shoulder, lit high toward its crown, GOLD along its lower rim and a gold
        // SCROLL coiled on its face (the reference's dense relief, at the one size it survives:
        // a single line coiling out from the centre). Spikes stand off its rim - spread ALONG the
        // rim, not from one point (Talon's finding: blades sprung from one root read as a flower):
        //
        //   NEAR (the sword arm)  one TALL spike by the neck, standing well past the chin, and two
        //                         shorter ones off the outer rim, leaning out
        //   FAR                   smaller, the outer spike only - the cape's mantle lies over it
        //
        // Under the dome two lames flare down the arm, each hemmed in gold.
        //
        // (Outward, height above the chin) texels from the joint; one field, sampled mirrored for
        // the near side (Vermilion's).

        const int TepPauldronMin = -6, TepPauldronMax = 18, TepPauldronRowCount = 34, TepPauldronRise = 14;

        static float TepPauldronCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((TepPauldronMin + TepPauldronMax) * 0.5f);
        static float TepPauldronY(string[] rows)
            => PlateY(rows) + PrimitiveCharacterRig.Proportions.Cells(TepPauldronRise);

        static string[] _tepPauldronNear, _tepPauldronFar;
        static string[] TepPauldronNear => _tepPauldronNear ??= TepPauldron(outwardIsPlusX: false, big: true);
        static string[] TepPauldronFar => _tepPauldronFar ??= TepPauldron(outwardIsPlusX: true, big: false);

        static string[] TepPauldron(bool outwardIsPlusX, bool big)
            => PaintField(TepPauldronMin, TepPauldronMax, 0, TepPauldronRowCount, (ix, iy) =>
            {
                int o = outwardIsPlusX ? ix : TepPauldronMin + TepPauldronMax - 1 - ix;
                int r = TepPauldronRowCount - 1 - iy;
                return TepPauldronTexel(o + 0.5f, TepPauldronRise - r - 0.5f, outwardIsPlusX, big);
            });

        /// <summary>The dome: (centre, radii) - the far one a size down.</summary>
        static (Vector2 C, float Rx, float Ry) TepDome(bool big)
            => big ? (new Vector2(5f, 1f), 7.5f, 7f) : (new Vector2(4.5f, 0f), 6.5f, 6f);

        static bool TepDomeIn(float o, float h, bool big)
        {
            var (c, rx, ry) = TepDome(big);
            float dx = (o - c.x) / rx, dy = (h - c.y) / ry;
            return dx * dx + dy * dy < 1f;
        }

        /// <summary>Spikes: base centre, tip, base half-width.</summary>
        static readonly (Vector2 Base, Vector2 Tip, float Half)[] TepSpikesBig =
        {
            (new Vector2(6f, 6.5f), new Vector2(7.5f, 14.5f), 2.2f),
            (new Vector2(10.5f, 3f), new Vector2(16.5f, 5f), 1.7f),
        };
        static readonly (Vector2 Base, Vector2 Tip, float Half)[] TepSpikesSmall =
        {
            (new Vector2(9f, 2f), new Vector2(13.5f, 3.5f), 1.4f),
        };

        /// <summary>Whether (o, h) is on a spike, and how far across it (-1 lit edge .. +1 far edge).</summary>
        static bool TepSpikeAt(float o, float h, bool big, out float across, out float along)
        {
            across = along = 0f;
            foreach (var (b, t, half) in big ? TepSpikesBig : TepSpikesSmall)
            {
                var axis = t - b;
                float len = axis.magnitude;
                var dir = axis / len;
                var p = new Vector2(o, h) - b;
                float a = Vector2.Dot(p, dir) / len;
                if (a < -0.2f || a > 1f) continue;
                float w = half * (1f - Mathf.Max(a, 0f));
                float perp = p.x * dir.y - p.y * dir.x;                                 // + toward outward-and-down
                if (Mathf.Abs(perp) < w)
                {
                    across = perp / Mathf.Max(w, 0.01f);
                    along = a;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The gold relief on the dome: a SCALLOPED arc inset under its crown, the two
        /// together a gilt frame with the lower rim. A vine waving across the face (the second
        /// pass) closed with the rim into a mouth; a coil from the centre (the first) read as a
        /// snail's shell.</summary>
        static bool TepScrollAt(float o, float h, bool big)
        {
            var (c, rx, ry) = TepDome(big);
            float dx = (o - c.x) / rx, dy = (h - c.y) / ry;
            if (dy < 0.12f) return false;
            float d = Mathf.Sqrt(dx * dx + dy * dy), theta = Mathf.Atan2(dy, dx);
            float line = 0.64f + 0.08f * Mathf.Abs(Mathf.Sin(theta * 3.5f));
            return Mathf.Abs(d - line) < 0.55f / ((rx + ry) * 0.5f);
        }

        static char TepPauldronTexel(float o, float h, bool outwardIsPlusX, bool big)
        {
            const string P = TepPlate, G = TepGold;
            float towardLight = outwardIsPlusX ? o : -o;
            var (c, rx, ry) = TepDome(big);
            int side = towardLight > c.x + 3f ? 1 : towardLight < c.x - 6f ? -1 : 0;

            // ---- the dome ----
            if (TepDomeIn(o, h, big))
            {
                // NO gilt rim round the dome's foot: with the arc above it the gold closed into a
                // ring. The lames below carry the gold hems instead.
                if (!TepDomeIn(o, h - 1f, big)) return RampChar(P, 1);
                if (TepScrollAt(o, h, big))
                {
                    bool lit = !TepScrollAt(o - (outwardIsPlusX ? 1f : -1f), h + 1f, big);
                    return RampChar(G, lit ? 4 : 3);
                }
                if (!TepDomeIn(o, h + 1f, big)) return RampChar(P, side >= 0 ? 5 : 4);  // the lit crown
                float dx = (o - c.x) / rx, dy = (h - c.y) / ry;
                // A highlight inside the arc, toward the light: without it the dome read as a flat
                // black disc.
                float lx = dx + (outwardIsPlusX ? -0.15f : 0.15f), ly = dy - 0.15f;
                if (lx * lx + ly * ly < 0.10f) return RampChar(P, 5);
                if (lx * lx + ly * ly < 0.24f) return RampChar(P, 4);
                if (dx * dx + dy * dy > 0.6f && dy < 0f) return RampChar(P, 2);          // its foot
                return RampChar(P, 3 + side);
            }

            // ---- the spikes, behind the dome's rim ----
            if (TepSpikeAt(o, h, big, out float across, out float along))
            {
                // The light comes from +X: the spike's edge facing it is lit.
                bool litEdge = outwardIsPlusX ? across > 0.1f : across < -0.1f;
                if (along > 0.82f) return RampChar(P, 4);                                 // the tip catches light
                return RampChar(P, litEdge ? 4 : across * (outwardIsPlusX ? -1f : 1f) > 0.45f ? 1 : 2);
            }

            // ---- two lames below, out over the upper arm ----
            float lameTop0 = c.y - ry + 0.5f;
            for (int k = 0; k < 2; k++)
            {
                float lTop = lameTop0 - 3.6f * k - 0.10f * o, lBottom = lTop - 4f;
                float inner = -2.5f + k, outer = (big ? 12f : 10.5f) - 1.2f * k;
                if (h >= lTop || h < lBottom || o < inner || o > outer) continue;
                if (o > outer - 1.2f && h < lBottom + 1.2f) return '.';                 // the rounded corner
                if (h < lBottom + 1f) return RampChar(G, 3 + Mathf.Max(side, 0));        // the gilt hem
                if (h >= lTop - 1f) return k == 0 ? RampChar(P, 0) : RampChar(P, 1);     // under the plate above
                if (h >= lTop - 2f) return RampChar(P, 4 + Mathf.Max(side, 0));
                return RampChar(P, 3 + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the gauntlets (Gloves)
        //
        // ARM-local: 0 at the shoulder, the elbow cut at -14, the wrist at -22, the hand below.
        // Row -12 is repeated by the rig to lengthen the upper arm, so only plain plate sits there.
        // Both arms one grid (Vermilion's): lames down the rerebrace, a COUTER pointed off the -X
        // side (outward on the near arm), a vambrace with a gold band under the elbow and a gilt
        // ridge, a FLARED cuff rimmed in gold, a fist in lames - the reference's articulated
        // fingers, as two knuckle rows rather than a grid of plates.
        //
        // The cuff is no wider than 6: the near arm hangs in front of the torso (Nocturne's note).

        const int TepArmTop = 0, TepArmBottom = -34, TepArmHalf = 8;

        static string[] _tepArmRows;
        static string[] TepArmRows => _tepArmRows ??=
            PaintField(-TepArmHalf, TepArmHalf, TepArmBottom, TepArmTop, TepArmTexel);

        static char TepArmTexel(int ix, int iy)
        {
            const string P = TepPlate, G = TepGold;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2f);

            // ---- the rerebrace: lames ----
            if (iy >= -14)
            {
                if (ax > 4.6f) return '.';
                if (iy == -4 || iy == -8) return RampChar(P, 1);                         // each lame's lower edge
                if (iy == -5 || iy == -9) return RampChar(P, 4 + Mathf.Max(side, 0));
                return RampChar(P, 3 + side);
            }

            // ---- the couter: a cop with its point off the -X side ----
            {
                float dx = (x + 0.3f) / 4.8f, dy = (y + 16.5f) / 3.2f;
                float r2 = dx * dx + dy * dy;
                bool point = x < -3.5f && x > -7.5f && Mathf.Abs(y + 17f) < 2.2f * (1f - (-3.5f - x) / 4f);
                if (r2 < 1f || point)
                {
                    if (point && r2 >= 1f) return RampChar(G, y > -17f ? 3 : 2);
                    if (dy < -0.62f) return RampChar(G, 3 + Mathf.Max(side, 0));          // the gilt lower rim
                    if (dx > 0f && dx < 0.45f && dy > 0.2f && dy < 0.7f) return RampChar(P, 4);
                    return RampChar(P, 3 + side);
                }
            }

            // ---- the vambrace ----
            if (iy >= -25)
            {
                float half = 4.8f - (-20f - y) * 0.05f;
                if (ax > half) return '.';
                if (ix == 1) return RampChar(P, 4);                                     // the ridge
                if (ix == 0) return RampChar(P, 2);
                return RampChar(P, 3 + side);
            }

            // ---- the cuff, flared ----
            //
            // NO gold: the fists hang at the belt, and a gilt cuff top a texel under the belt's
            // gold line read as more belt plates either side of it, each a different width.
            if (iy >= -28)
            {
                float half = 5.2f + (iy + 25) * -0.3f;
                if (ax > Mathf.Min(half, 6f)) return '.';
                if (iy == -26) return RampChar(P, 4 + Mathf.Max(side, 0));
                if (iy == -28) return RampChar(P, 1);
                return RampChar(P, 3 + side);
            }

            // ---- the fist: two knuckle rows ----
            {
                float half = iy < -32 ? 3.6f : 4.5f;
                if (ax > half) return '.';
                if (iy == -29 || iy == -31) return RampChar(P, 4 + Mathf.Max(side, 0));
                if (iy == -30 || iy == -32) return RampChar(P, 1);
                return RampChar(P, 2 + Mathf.Max(side, 0));
            }
        }

        // ------------------------------------------------------------------ the belt
        //
        // Torso-local, on the belt's band: black plate between two gold lines, and a gilt CLASP at
        // the front - an oval boss, pointed below where the centre plate hangs from it.

        const int TepBeltTop = 14, TepBeltBottom = -12, TepBeltHalf = 18;

        static string[] _tepBeltRows;
        static string[] TepBeltRows => _tepBeltRows ??=
            PaintField(-TepBeltHalf, TepBeltHalf, TepBeltBottom, TepBeltTop, TepBeltTexel);

        static bool TepClaspIn(float x, float y)
        {
            float dx = x / 3.4f, dy = (y - 8f) / 3.2f;
            if (y < 8f) dx = Mathf.Abs(x) / Mathf.Max(0.1f, 3.4f * (1f - (8f - y) / 4.6f));
            return dx * dx + dy * dy < 1f || (y < 8f && Mathf.Abs(dx) < 1f && y > 3.4f);
        }

        static char TepBeltTexel(int ix, int iy)
        {
            const string P = TepPlate, G = TepGold;
            float x = ix + 0.5f, y = iy + 0.5f;
            // The band is the CUIRASS's width at the waist on both sides (13 near, the body's
            // edge far), level, one tone end to end. Running past the cuirass on the near side,
            // stepping in with TalBodyEdge on the far and lit lighter there, it read as uneven
            // plates rather than one belt.
            const float nearEdge = -13f, farEdge = 9.5f;

            if (TepClaspIn(x, y))
            {
                if (TepRim(TepClaspIn, x, y)) return RampChar(G, x + (y - 8f) > 0f ? 3 : 2);
                return RampChar(G, x + (y - 8.5f) > 0f ? 5 : 4);
            }

            if (iy >= 4 && iy < 12 && x >= nearEdge && x <= farEdge)
            {
                if (iy == 11) return RampChar(G, 4);
                if (iy == 4) return RampChar(G, 2);
                if (iy == 10) return RampChar(P, 1);
                return RampChar(P, iy == 9 ? 3 : 2);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the tassets (Legs)
        //
        // TORSO-local on RigLayer.Tasset. Front to back:
        //
        //   the CENTRE PLATE   hung from the clasp, narrowing to a point at the top of the thighs,
        //                      gold all round; it overlaps the tassets' inner edges
        //   the TASSETS        a curved plate over the upper thigh, FLARING out as it falls and
        //                      rounded below, gold all round; their inner edges part downward
        //                      into a V where the loincloth shows. Shaded ACROSS the plate - lit
        //                      on the face toward the light, turning into shade at the far edge -
        //                      so it wraps the thigh. Straight-sided with a lame line across (the
        //                      first pass) they read as two boxes with lids. Both plates start
        //                      RIGHT under the belt (its outline covers their top row) - a shaded
        //                      row there widened the outline into a gap, the plates hanging loose.
        //                      They stop at
        //                      MID-THIGH, the reference's: run lower, they buried the knee cops.
        //   the LOINCLOTH      navy, between the legs from under the centre plate to past the
        //                      knee, torn at the hem into three strips that hang uneven
        //
        // BOTH TASSETS ARE ONE WIDTH. Herald stops its far side at the body's edge (the far arm
        // draws behind this layer, its fist at the belt); here that made a gold-edged sliver
        // beside a broad near plate, so the far plate overlaps the fist's inner edge a little.

        const int TepSkirtTop = 4, TepSkirtBottom = -36, TepSkirtHalf = 18;

        static string[] _tepSkirtRows;
        static string[] TepSkirtRows => _tepSkirtRows ??=
            PaintField(-TepSkirtHalf, TepSkirtHalf, TepSkirtBottom, TepSkirtTop, TepSkirtTexel);

        static bool TepCentrePlateIn(float x, float y)
            => y < 4f && Mathf.Abs(x) < 5f - Mathf.Max(0f, 0f - y) * 0.6f && y > -9f;

        /// <summary>A tasset's inner and outer edge at y: the inner parting downward, the outer flaring.</summary>
        static float TepTassetInner(float y) => 2f + Mathf.Max(0f, 1f - y) * 0.14f;
        /// <summary>The SAME on both sides, from the belt's width flaring to just past it. Near
        /// out past the hip and far cut at the body's edge (Herald's rule), the pair was lopsided:
        /// a broad plate on one thigh, a gold-edged sliver on the other.</summary>
        static float TepTassetOuter(float y, bool near) => 9.5f + Mathf.Max(0f, 2f - y) * 0.13f;

        static bool TepTassetIn(float x, float y)
        {
            bool near = x < 0f;
            float ax = Mathf.Abs(x);
            float inner = TepTassetInner(y), outer = TepTassetOuter(y, near);
            const float top = 4f, bottom = -13f, round = 3.5f;
            if (y >= top || y < bottom || ax < inner || ax > outer) return false;
            // Rounded below, both corners: a shield, not a box.
            float cy = bottom + round;
            if (y < cy)
            {
                float cxIn = inner + round, cxOut = outer - round;
                float px = ax < cxIn ? cxIn - ax : ax > cxOut ? ax - cxOut : 0f;
                float py = cy - y;
                if (px * px + py * py > round * round) return false;
            }
            return true;
        }

        /// <summary>The loincloth's hem below x: three strips, the middle one longest.</summary>
        static float TepLoinHem(float x)
        {
            if (x < -1.5f) return -27f + (x + 3.5f) * 0.6f;
            if (x < 1.5f) return -32f + Mathf.Abs(x) * 0.8f;
            return -25.5f - (x - 1.5f) * 0.5f;
        }

        static char TepSkirtTexel(int ix, int iy)
        {
            const string P = TepPlate, G = TepGold, C = TepCloth;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 5f, -6f);
            if (y >= 4f) return '.';

            // ---- the centre plate ----
            if (TepCentrePlateIn(x, y))
            {
                if (TepRim(TepCentrePlateIn, x, y)) return RampChar(G, x >= 0f ? 4 : 3);
                // A ridge down the middle, lit on its +X face. Lit all across (the first pass) the
                // plate read as glare, not metal.
                if (ix == 0) return RampChar(P, 4);
                return RampChar(P, x > 0f ? 3 : 2);
            }

            // ---- the tassets ----
            if (TepTassetIn(x, y))
            {
                bool near = x < 0f;
                // Gold on the OUTER edge and the foot only. The inner edges tuck under the centre
                // plate in shade: gilt, their slant and the centre plate's V made a gold zigzag.
                if (TepRim(TepTassetIn, x, y))
                    return ax < TepTassetInner(y) + 1.5f && y > -9f ? RampChar(P, 1) : RampChar(G, near ? 3 : 4);
                // Across the plate, 0 at its inner edge, 1 at its outer: the near plate faces the
                // light along its inner half, the far one along its outer half.
                float inner = TepTassetInner(y), outer = TepTassetOuter(y, near);
                float t = (ax - inner) / Mathf.Max(outer - inner, 1f);
                float lit = near ? 1f - t : t;
                int tone = lit > 0.62f ? 4 : lit > 0.3f ? 3 : 2;
                if (y < -9.5f) tone--;                                                   // turning under at the foot
                return RampChar(P, Mathf.Max(tone, 1));
            }

            // ---- the loincloth ----
            if (x > -3.5f && x < 3f && y < 0f)
            {
                float hem = TepLoinHem(x);
                if (y < hem) return '.';
                // The strips part: a gap between each pair below the knee.
                if (y < -22f && (Mathf.Abs(x + 1.5f) < 0.5f || Mathf.Abs(x - 1.5f) < 0.5f)) return '.';
                if (y < hem + 1f) return RampChar(C, 1);
                if (x > 1.5f) return RampChar(C, 4);                                     // lit
                if (x < -1.5f) return RampChar(C, 2);
                return RampChar(C, 3);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the greaves (Boots)
        //
        // LEG-local, the sole at -48, the knee at -24. A CUISSE over the lower thigh; a big round
        // KNEE COP wider than the leg (Errant's finding), domed and plain; a
        // lame under it hemmed in gold; a GREAVE lit down the shin; an ankle lame edged in gold;
        // a SABATON of two lames.

        const int TepGreaveTop = -12, TepGreaveBottom = -48, TepGreaveHalf = 6;

        static string[] _tepGreaveRows;
        static string[] TepGreaveRows => _tepGreaveRows ??=
            PaintField(-TepGreaveHalf, TepGreaveHalf, TepGreaveBottom, TepGreaveTop, TepGreaveTexel);

        static bool TepKneeCopIn(float x, float y)
        {
            float dx = x / 5.8f, dy = (y + 23.5f) / 5.2f;
            return dx * dx + dy * dy < 1f;
        }

        static char TepGreaveTexel(int ix, int iy)
        {
            const string P = TepPlate, G = TepGold;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2f);

            // ---- the knee cop ----
            if (TepKneeCopIn(x, y))
            {
                // A plain DOME, lit up and toward +X, its lower edge in shade; the gold is on the
                // lame under it. Gold round its lower half read as a U under a dark dome, and a gilt
                // ridge down its middle as a pair of bars across the two knees.
                if (!TepKneeCopIn(x, y - 1f)) return RampChar(P, 1);
                if (!TepKneeCopIn(x, y + 1f)) return RampChar(P, x >= 0f ? 4 : 3);
                // Lit as a BAND across its upper part, not a spot: a spot sat under the loincloth
                // on the near knee and read as a button on the far one.
                float dy = (y + 23.5f) / 5.2f;
                if (dy > 0.3f && ax < 4.6f) return RampChar(P, 4);
                if (dy < -0.45f || ax > 4.2f) return RampChar(P, 2);
                return RampChar(P, 3);
            }

            // ---- the cuisse ----
            if (iy >= -19)
            {
                if (ax > 4.6f) return '.';
                if (iy == -16) return RampChar(P, 1);
                if (iy == -17) return RampChar(P, 4 + Mathf.Max(side, 0));
                if (ix == 1) return RampChar(P, 4);
                return RampChar(P, 3 + side);
            }
            if (y >= -29f) return '.';

            // ---- the lame under the cop ----
            if (iy >= -31)
            {
                if (ax > 4.8f) return '.';
                if (iy == -29) return RampChar(P, 0);                                   // the gap under the cop
                if (iy == -31) return RampChar(G, 3 + Mathf.Max(side, 0));              // its gilt hem
                return RampChar(P, 4 + Mathf.Max(side, 0));
            }

            // ---- the sabaton ----
            if (iy < -42)
            {
                if (ax > 5.5f) return '.';
                if (iy == -48) return RampChar(P, 0);                                   // the sole
                if (iy == -43) return RampChar(G, 3 + Mathf.Max(side, 0));               // the ankle's gilt edge
                if (iy == -46) return RampChar(P, 4 + Mathf.Max(side, 0));
                if (iy == -45) return RampChar(P, 1);
                return RampChar(P, 3 + side);
            }

            // ---- the greave ----
            float half = 4.6f - (-32f - y) * 0.04f;
            if (ax > half) return '.';
            if (ix == 1) return RampChar(P, 4);                                         // the shin ridge
            if (ix == 0) return RampChar(P, 2);
            return RampChar(P, 3 + side);
        }

        // ------------------------------------------------------------------ the cape (Back)
        //
        // TORSO-local CELLS, y up from the waist (the Hellspawn Cape's frame): the neck at 22, the
        // shoulders 21, the knee -12, the soles -24. NAVY, from the shoulders to the floor, flaring
        // to about one and a half times the body's width. The lower third is SHREDDED - the
        // reference's: long tatters dragging to the floor, the side edges ragged, and the cloth
        // torn through in big holes (the Hellspawn rule: at less than ~4 x 8 cells the outline
        // closes a hole into a dark rune). Broad fan folds, never crease marks.

        const float TepCapeTop = 25f, TepCapeBottom = -26f, TepCapeHalfW = 22f;
        /// <summary>The solid cloth ends here; below it only the tatters hang.</summary>
        const float TepCapeHemLine = -10f;

        static float TepCapeHalf(float y) => y >= 14f ? 12f + (21f - Mathf.Min(y, 21f)) * 0.06f : 12.4f + (14f - y) * 0.21f;
        static float TepCapeTopAt(float ax) => ax <= 5f ? 24.5f : 24.5f - (ax - 5f) * (3.5f / 7f);

        static List<(float X, float Drop)> _tepTatters;
        static List<(float X, float Drop)> TepTatters
        {
            get
            {
                if (_tepTatters != null) return _tepTatters;
                var rng = new System.Random(1431);
                float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
                _tepTatters = new List<(float, float)>();
                for (float x = -TepCapeHalfW + 0.8f; x < TepCapeHalfW - 0.8f; x += R(2.0f, 3.2f))
                    _tepTatters.Add((x, rng.NextDouble() < 0.5 ? R(9f, 14f) : R(3f, 7f)));
                return _tepTatters;
            }
        }

        static readonly (float X, float Y, float Rx, float Ry)[] TepTears =
        {
            (-9.5f, -5f, 2.8f, 5.2f),
            (9f, -8f, 2.8f, 5f),
            (1.5f, -12f, 2.6f, 4.6f),
        };

        static float TepCapeBottomAt(float x)
        {
            float bottom = TepCapeHemLine;
            foreach (var t in TepTatters)
                bottom = Mathf.Min(bottom, TepCapeHemLine - t.Drop + Mathf.Abs(x - t.X) * 2.3f);
            return Mathf.Max(bottom, TepCapeBottom + 0.5f);
        }

        static float TepCapeSideRag(float y, bool left)
        {
            if (y > 4f) return 0f;
            float p = Mathf.Repeat(y / 2.8f + (left ? 0.29f : 0.73f), 1f);
            return (Mathf.Abs(p - 0.5f) * 2f - 0.5f) * 2.6f * Mathf.InverseLerp(4f, -6f, y);
        }

        static bool TepTorn(float x, float y)
        {
            foreach (var t in TepTears)
            {
                float dx = (x - t.X) / t.Rx, dy = (y - t.Y) / t.Ry;
                float a = Mathf.Atan2(dy, dx);
                float wobble = 1f + 0.18f * Mathf.Sin(a * 3f + t.X) + 0.12f * Mathf.Sin(a * 7f + t.Y);
                if (dx * dx + dy * dy < wobble * wobble) return true;
            }
            return false;
        }

        static bool TepCapeIn(float x, float y)
        {
            float ax = Mathf.Abs(x);
            if (y > TepCapeTopAt(ax) || y < TepCapeBottomAt(x)) return false;
            if (ax > TepCapeHalf(y) + TepCapeSideRag(y, x < 0f)) return false;
            return !TepTorn(x, y);
        }

        /// <summary>The cape's tone at (x, y), whether or not there is cloth there - the mantle's
        /// back view reads its colour off this beside the cape's edge (the Wraithguard's).</summary>
        static int TepCapeTone(float x, float y)
        {
            float ax = Mathf.Abs(x);
            float top = TepCapeTopAt(ax), half = TepCapeHalf(y);
            if (top - y < 1.4f) return 4;                                               // light on the shoulders
            float u = x / half;
            float fold = Mathf.Sin(u * Mathf.PI * 3f + 0.5f) * Mathf.Clamp01((18f - y) / 24f);
            int tone = y > 6f - fold * 10f ? 3 : 2;
            if (fold > 0.6f && tone == 2) tone++;
            else if (fold < -0.8f) tone--;
            if (half - ax < 1.1f) tone += x > 0f ? 1 : -1;
            if (y - TepCapeBottomAt(x) < 1.4f) tone = 1;                                // the torn edge in shade
            return Mathf.Clamp(tone, 1, 4);
        }

        static string[] _tepCapeRows;
        static string[] TepCapeRows => _tepCapeRows ??= StripSpecks(SampleCells(
            -TepCapeHalfW, TepCapeHalfW, TepCapeBottom, TepCapeTop,
            (x, y) => TepCapeIn(x, y) ? RampChar(TepCloth, TepCapeTone(x, y)) : '.'));

        // ------------------------------------------------------------------ the mantle (Back, BackOver)
        //
        // Torso CELLS, authored on -X (the rig mirrors it opposite the sword arm). The reference's
        // FUR thrown over the off shoulder: a cap from beside the gorget out past the arm, its top
        // lumpy with tufts, its lower edge hanging in fur points over the upper arm; outside the arm
        // it falls on as a panel of the cape's cloth to the hip, torn at its end. Fur in the navy's
        // darker tones with lit tips, so it reads as a second texture on the same cloth.

        const float TepMantleTop = 23f, TepMantleBottom = 3f, TepMantleMinX = -16f, TepMantleMaxX = -2f;
        const float TepMantleInner = -3.5f, TepMantlePanelInner = -9.5f;

        /// <summary>The fur's top: lumpy, falling at the arm.</summary>
        static float TepFurTop(float x)
        {
            float t = Mathf.Max(0f, (-8f - x) / 6f);
            float lumps = Mathf.Abs(Mathf.Sin(x * 1.35f)) * 0.9f;
            return 21.8f - t * t * 3.2f + lumps;
        }

        /// <summary>The fur's lower edge over the arm: a row of tufts hanging in points.</summary>
        static float TepFurHem(float x)
        {
            float tooth = Mathf.Abs(Mathf.Repeat(x * 0.62f, 1f) - 0.5f) * 2f;          // 1..0..1
            return 13.5f + (x - TepMantleInner) * 0.12f - (1f - tooth) * 2.2f;
        }

        static float TepMantleOuter(float y) => -14.5f - Mathf.Max(0f, 14f - y) * 0.07f;

        /// <summary>The panel's torn end, above the waist - the forearm comes out from under it
        /// at the hip, as the reference's does.</summary>
        static float TepPanelBottom(float x)
        {
            float tooth = Mathf.Abs(Mathf.Repeat((x + 0.4f) * 0.45f, 1f) - 0.5f) * 2f;
            return 6.5f - (1f - tooth) * 2.6f + (x - TepMantlePanelInner) * 0.45f;
        }

        static bool TepFurIn(float x, float y)
            => x <= TepMantleInner && x >= TepMantleOuter(y) && y <= TepFurTop(x) && y >= TepFurHem(x);

        static bool TepMantleIn(float x, float y)
        {
            if (x > TepMantleInner || x < TepMantleOuter(y) || y > TepFurTop(x)) return false;
            if (y >= TepFurHem(x)) return true;                                           // the fur
            return x < TepMantlePanelInner && y >= TepPanelBottom(x);                     // the panel
        }

        static char TepMantleTexel(float x, float y)
        {
            if (!TepMantleIn(x, y)) return '.';
            const string C = TepCloth;

            if (TepFurIn(x, y))
            {
                // Fur reads by its EDGES - the lumpy top, the tufted hem - so the body of it is
                // shaded in broad planes falling down and out from the neck. A hatch of short
                // locks (the first pass) read as knitting.
                float top = TepFurTop(x), hem = TepFurHem(x);
                if (top - y < 0.8f) return RampChar(C, 4);                                // the light on top
                if (y - hem < 0.6f) return RampChar(C, 1);                                // the tips, in shade
                float v = x * 0.8f + y * 0.6f;
                float plane = Mathf.Sin(v * (2f * Mathf.PI / 5f) + 0.8f);
                int t = (y - hem) / Mathf.Max(top - hem, 0.1f) > 0.55f ? 3 : 2;
                if (plane > 0.6f) t++;
                else if (plane < -0.55f) t--;
                if (x < TepMantleOuter(y) + 0.6f) t = Mathf.Min(t, 2);                    // turning away at the edge
                return RampChar(C, Mathf.Clamp(t, 1, 4));
            }

            // The panel: the cape's cloth, darker as it falls, its torn end in shade.
            if (y - TepPanelBottom(x) < 0.8f) return RampChar(C, 1);
            if (x < TepMantleOuter(y) + 0.6f) return RampChar(C, 2);
            return RampChar(C, y > 6f ? 3 : 2);
        }

        static string[] _tepMantleRows;
        static string[] TepMantleRows => _tepMantleRows ??= StripSpecks(SampleCells(
            TepMantleMinX, TepMantleMaxX, TepMantleBottom, TepMantleTop, TepMantleTexel));

        /// <summary>The mantle from BEHIND (WgDrapeBackRows' construction): only what overhangs the
        /// cape, in the cape's own tone beside it, stroked ('o') where it meets open air.</summary>
        static string[] _tepMantleBackRows;
        static string[] TepMantleBackRows => _tepMantleBackRows ??= SampleCells(
            TepMantleMinX, TepMantleMaxX, TepMantleBottom, TepMantleTop, (x, y) =>
            {
                bool Strip(float px, float py) => TepMantleIn(px, py) && !TepCapeIn(px, py);
                bool Open(float px, float py) => !TepMantleIn(px, py) && !TepCapeIn(px, py);
                if (!Strip(x, y)) return '.';
                const float s = 0.5f;
                for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) <= 2 && Open(x + dx * s, y + dy * s)) return 'o';
                float edge = -TepCapeHalf(y) + 1.3f;
                return RampChar(TepCloth, TepCapeTone(edge, Mathf.Min(y, TepCapeTopAt(Mathf.Abs(edge)) - 1.5f)));
            });
    }
}
