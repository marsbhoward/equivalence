using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Errant: a hooded knight, plate on one side
    //
    // After the user's reference: a hooded swordsman in mixed plate, mail and leather. The HOOD
    // is not here - the existing Hooded Cloak (black_hood) already is the reference's hood and
    // back drape, and the cowl wrapped under the chin is a scarf in the Ashen dye (DemoGear.cs).
    // Everything else is new:
    //
    //   errant_plastron   Torso      a steel breastplate, ridged, a plackart over its lower half;
    //                                dark gambeson at the flanks, mail low on them; a brown
    //                                BANDOLIER from the near shoulder across to the far hip
    //   errant_pauldron   Shoulders  a big SILVER pauldron of three stacked plates (far), a small
    //                                black one (near)
    //   errant_gauntlets  Gloves     the far arm in a black sleeve and a riveted forearm wrap, the
    //                                hand bare; the near arm in black plate
    //   errant_belt       Belt       one wide belt, a narrow steel buckle, a pouch, a dagger
    //   errant_faulds     Legs       a mail skirt under two rows of steel splints, and the coat's
    //                                black front panels hanging outside the legs
    //   errant_greaves    Boots      a steel knee cop pointing DOWN the shin, black boots
    //
    // ONE SIDE IN SILVER. The reference's bright plate is the pauldron, the other shoulder dark.
    // As with Talon (the user's call there, made again here), the silver goes on the FAR arm -
    // the character's left, facing right (+X) - so the sword arm carries and the plated one hangs
    // still. Under it the arm is the reference's too: black wrap, bare hand. Both arm pieces are
    // GearItem.Lopsided, so turned away the silver stays on the left.
    //
    // Fields in each joint's own frame (DemoGear.Fields.cs), lit from +X like every piece here.
    // Diamond, power 0, like every reference-built cosmetic set.
    public static partial class DemoGear
    {
        const string ErrSteel = "ksdblh", ErrLeather = "KSDBLH", ErrBlack = "123456";

        /// <summary>Bare skin, in the far glove layer only - its palette carries skin where the others carry leather.</summary>
        const string ErrSkin = ErrLeather;

        static void AddErrant(List<GearItem> items)
        {
            // Bright steel, the reference's lit plate near white: shadows placed below LIGHT so
            // the seams between lames survive (Palette.Undersuit's reasoning).
            var steel = new Palette.Ramp(new Color(0.60f, 0.61f, 0.65f), lift: 0.40f, shade: 0.38f, line: 0.78f)
                .WithShadowsBelowLight(0.52f, 0.34f);
            // Strap leather, lit toward a warm tan so it never greys out.
            var leather = new Palette.Ramp(new Color(0.46f, 0.29f, 0.18f), shade: 0.34f)
                .WithHighlightsToward(new Color(0.82f, 0.60f, 0.40f), 0.30f);
            // The dark plate, gambeson and coat: a cool near-black, a step above the undersuit so
            // a lit edge still shows on it.
            var black = new Palette.Ramp(new Color(0.18f, 0.18f, 0.21f), lift: 0.30f, shade: 0.34f, line: 0.80f)
                .WithShadowsBelowLight(0.46f, 0.30f);
            const float farF = 0.85f;

            var pal = Palette.Of(steel, leather, black);
            var far = Palette.Of(steel.Scaled(farF), leather.Scaled(farF), black.Scaled(farF));

            items.Add(Defends(Make("errant_plastron", "Errant Plastron", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.errant", ErrPlastronRows, pal,
                       0f, FieldCentreCells(ErrPlastronBottom, ErrPlastronTop), ppu: BodyPpu)),
                // Plate over mail - built to stand and take it.
                DefensiveAbility.Bulwark));

            var pauldron = Make("errant_pauldron", "Errant Pauldron", GearSlot.Shoulders, LootTier.Diamond, 0f,
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.errant", ErrPauldronFar, far,
                       ShoulderX + ErrPauldronFarCentreCells, ErrPauldronFarY, ppu: BodyPpu),
                Pixels(RigLayer.Shoulders, "gear.pauldron.errant.black", ErrPauldronNear, pal,
                       -ShoulderX - ErrPauldronNearCentreCells, ErrPauldronNearY, ppu: BodyPpu));
            pauldron.Lopsided = true;
            items.Add(pauldron);

            // The far arm's hand is BARE: skin in the shaded gear tone, repainted in the wearer's
            // own at Apply (ShowsSkin), in the leather's slot - the arm wears no leather.
            var gauntlets = Make("errant_gauntlets", "Errant Gauntlets", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesBack, "gear.gloves.errant", ErrWrapArmRows,
                       Palette.Of(steel.Scaled(farF), BodyLook.GearSkinShaded, black.Scaled(farF)),
                       FieldCentreCells(ErrWrapArmMin, ErrWrapArmMax),
                       FieldCentreCells(ErrArmBottom, ErrArmTop), ppu: BodyPpu),
                Pixels(RigLayer.GlovesFront, "gear.gloves.errant.black", ErrBlackArmRows, pal,
                       0f, FieldCentreCells(ErrArmBottom, ErrArmTop), ppu: BodyPpu));
            gauntlets.ShowsSkin = true;
            gauntlets.Lopsided = true;
            items.Add(gauntlets);

            items.Add(Make("errant_belt", "Errant Belt", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.errant", ErrBeltRows, pal,
                       0f, FieldCentreCells(ErrBeltBottom, ErrBeltTop), ppu: BodyPpu)));

            items.Add(Make("errant_faulds", "Errant Faulds", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.Tasset, "gear.legs.errant", ErrFauldRows, pal,
                       0f, FieldCentreCells(ErrFauldBottom, ErrFauldTop), ppu: BodyPpu)));

            items.Add(Make("errant_greaves", "Errant Greaves", GearSlot.Boots, LootTier.Diamond, 0f,
                Pixels(RigLayer.BootsFront, "gear.boots.errant", ErrGreaveRows, pal,
                       0f, FieldCentreCells(ErrGreaveBottom, ErrGreaveTop), ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.errant.dark", ErrGreaveRows, far,
                       0f, FieldCentreCells(ErrGreaveBottom, ErrGreaveTop), ppu: BodyPpu)));

            // The black is the set's mass (plate, sleeves, coat); the steel is the near pauldron and
            // the tabard - the accent. The leather is a strap and a pouch, too small to sell a dye.
            Dyeable(items, "errant_", DyeChannel.Of("Plate", DyeMaterial.Metal, black, farF),
                    DyeChannel.Of("Steel", DyeMaterial.Metal, steel, farF));
        }

        /// <summary>Mail: a checker of two dark steel tones, a step lighter toward the light.</summary>
        static char ErrMail(int ix, int iy, int side)
            => RampChar(ErrSteel, (((ix + iy) & 1) == 0 ? 2 : 1) + Mathf.Max(side, 0));

        // ------------------------------------------------------------------ the plastron (Torso)
        //
        // Torso-local. A steel BREASTPLATE over the chest - a ridge down its middle, lit on the +X
        // half, and a PLACKART over its lower half whose top edge arches up at the centre. Dark
        // gambeson either side of it to the body's edge, mail showing low on the flanks. The
        // BANDOLIER runs from the near (-X) shoulder across the plate to the far hip, a steel
        // fitting on it at the chest, and throws a shadow under it.

        const int ErrPlastronTop = 46, ErrPlastronBottom = 6, ErrPlastronHalf = 16;

        static string[] _errPlastronRows;
        static string[] ErrPlastronRows => _errPlastronRows ??=
            PaintField(-ErrPlastronHalf, ErrPlastronHalf, ErrPlastronBottom, ErrPlastronTop, ErrPlastronTexel);

        static float ErrPlateHalf(float y) => y >= 24f ? 9.5f : 9.5f - (24f - y) * 0.12f;
        static float ErrPlateTop(float ax) => 33.5f + ax * 0.45f;

        static bool ErrPlateIn(float x, float y)
        {
            float ax = Mathf.Abs(x);
            return y >= 8.5f && ax <= ErrPlateHalf(y) && y < ErrPlateTop(ax);
        }

        /// <summary>The plackart's top edge: arching up to a point at the centre.</summary>
        static float ErrPlackart(float ax) => 17f + Mathf.Max(0f, 4f - ax) * 0.5f;

        /// <summary>The bandolier, as (distance across it, toward +X/up; distance along it from the shoulder).</summary>
        static readonly Vector2 ErrStrapFrom = new(-13f, 39f), ErrStrapTo = new(11f, 7f);

        static (float across, float along) ErrStrap(float x, float y)
        {
            var d = (ErrStrapTo - ErrStrapFrom).normalized;
            var n = new Vector2(-d.y, d.x);                     // up and toward +X
            var p = new Vector2(x, y) - ErrStrapFrom;
            return (Vector2.Dot(p, n), Vector2.Dot(p, d));
        }

        static char ErrPlastronTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 6f, -7f);

            // On the far side it keeps to the body's own edge (Talon's finding: the far arm hangs
            // half behind the torso, and anything wider hides it).
            float top = 40.5f - Mathf.Max(0f, ax - 6f) * 0.45f;
            float half = x < 0f ? (iy >= 26 ? 13.5f : 13f) : TalBodyEdge(y);
            if (y >= top || ax > half) return '.';

            // ---- the bandolier ----
            var (across, along) = ErrStrap(x, y);
            if (Mathf.Abs(across) < 1.6f)
            {
                if (along > 21f && along < 25f)                                         // the fitting
                {
                    bool frame = along < 22f || along > 24f || Mathf.Abs(across) > 0.8f;
                    if (frame) return RampChar(ErrSteel, along < 22f ? 5 : across > 0f ? 4 : 2);
                }
                return RampChar(ErrLeather, across > 0.6f ? 4 : across < -0.8f ? 2 : 3);
            }
            bool inShadow = across < -1.6f && across > -2.8f;

            // ---- the breastplate ----
            if (ErrPlateIn(x, y))
            {
                if (inShadow) return RampChar(ErrSteel, 1);
                if (!ErrPlateIn(x, y + 1f)) return RampChar(ErrSteel, 5);               // the rolled top edge
                if (VerRim(ErrPlateIn, x, y)) return RampChar(ErrSteel, 1);
                float seam = ErrPlackart(ax);
                if (y >= seam && y < seam + 1f) return RampChar(ErrSteel, 1);           // the plackart's edge
                if (y >= seam - 1f && y < seam) return RampChar(ErrSteel, 4 + Mathf.Max(side, 0));
                if ((ix == -7 || ix == 6) && iy == (int)ErrPlateTop(7f) - 2)
                    return RampChar(ErrSteel, ix > 0 ? 2 : 5);                          // rivets
                if (ix == 0) return RampChar(ErrSteel, y >= seam ? 5 : 4);              // the ridge
                if (ix == -1) return RampChar(ErrSteel, 2);                             // its shade
                int tone = x > 0f ? 4 : 3;
                if (y < seam) tone--;
                if (ax > ErrPlateHalf(y) - 2f) tone--;                                  // turning away
                return RampChar(ErrSteel, tone);
            }

            // ---- the flanks: gambeson, mail low down ----
            if (inShadow) return RampChar(ErrBlack, 1);
            if (y < 16f && ax > 8f) return ErrMail(ix, iy, side);
            if (y >= ErrPlateTop(Mathf.Min(ax, 9.5f)) && ax < 9.5f) return RampChar(ErrBlack, 2);  // the collar
            return RampChar(ErrBlack, 3 + side);
        }

        // ------------------------------------------------------------------ the pauldrons (Shoulders)
        //
        // FAR (+X): the reference's piece. Three SEPARATE SHIELD PLATES of one shape, stacked like
        // scales: the big front plate over the shoulder and upper arm, its point hanging OUT past
        // the arm, and two more behind it, each stepped up and a little IN toward the neck
        // (ErrPlateStep) and a touch smaller (ErrPlateShrink). Each back plate shows a real band
        // of its own face - rim, shading - with a black gap and a shadow where the plate in front
        // lies over it, and its sides notch the outline. Stepped along the plate's own slanted
        // edge, or showing only a sliver, the three merged into one piece of metal.
        //
        // NEAR (-X): a small black cap and two lames - the reference's dark shoulder, kept small so
        // the silver one owns the silhouette.
        //
        // (Outward, height above the chin) texels from the joint, Talon's frame.

        const int ErrPauldronFarMin = -4, ErrPauldronFarMax = 18, ErrPauldronFarRowCount = 26;
        const int ErrPauldronFarRise = 10;
        static float ErrPauldronFarCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((ErrPauldronFarMin + ErrPauldronFarMax) * 0.5f);
        static float ErrPauldronFarY
            => PlateY(ErrPauldronFar) + PrimitiveCharacterRig.Proportions.Cells(ErrPauldronFarRise);

        /// <summary>The front plate: a domed top, tall on the inner side, a point hanging outward.</summary>
        static readonly Vector2[] ErrShieldPlate =
        {
            new(-1.9f, -2.5f), new(-0.25f, -1f), new(2.5f, 0.125f), new(6.9f, 0.125f), new(11.3f, -1f),
            new(15.15f, -2.875f), new(17.35f, -5.5f), new(17.35f, -10f), new(15.7f, -13.375f),
            new(13.5f, -15.625f), new(9.65f, -15.25f), new(5.25f, -13f), new(1.4f, -10f), new(-1.35f, -6.25f),
        };

        /// <summary>The front plate's middle across, and its top: the back plates shrink toward it.</summary>
        static readonly Vector2 ErrPlateCrown = new(8.05f, 0.125f);
        static readonly Vector2 ErrPlateStep = new(-2f, 5f);
        const float ErrPlateShrink = 0.08f;
        const int ErrPlateCount = 3;

        /// <summary>(o, h) in plate k's own frame - 0 the front plate, counting back.</summary>
        static Vector2 ErrPlateLocal(int k, float o, float h)
        {
            float s = 1f - ErrPlateShrink * k;
            var p = new Vector2(o, h) - ErrPlateStep * k;
            return (p - ErrPlateCrown) / s + ErrPlateCrown;
        }

        static bool ErrPlateAt(int k, float o, float h)
        {
            var p = ErrPlateLocal(k, o, h);
            return TalInPoly(p.x, p.y, ErrShieldPlate);
        }

        const float ErrLameHeight = 3.6f;

        static string[] _errPauldronFar;
        static string[] ErrPauldronFar => _errPauldronFar ??=
            PaintField(ErrPauldronFarMin, ErrPauldronFarMax, 0, ErrPauldronFarRowCount, (ix, iy) =>
            {
                int r = ErrPauldronFarRowCount - 1 - iy;                 // outward is +X
                return ErrPauldronFarTexel(ix + 0.5f, ErrPauldronFarRise - r - 0.5f);
            });

        static char ErrPauldronFarTexel(float o, float h)
        {
            const string S = ErrSteel;

            // Front to back: the first plate found here is the one on top.
            for (int k = 0; k < ErrPlateCount; k++)
            {
                if (!ErrPlateAt(k, o, h)) continue;
                float lo = ErrPlateLocal(k, o, h).x - ErrPlateCrown.x;                  // across its own plate
                if (k > 0 && ErrPlateAt(k - 1, o, h - 1f)) return RampChar(S, 0);      // the gap under the plate in front
                if (k > 0 && ErrPlateAt(k - 1, o, h - 2f)) return RampChar(S, 1);      // its shadow
                if (!ErrPlateAt(k, o, h + 1f)) return RampChar(S, lo > -3f ? 5 : 4);    // the rolled rim
                if (!ErrPlateAt(k, o, h - 1f) || !ErrPlateAt(k, o - 1f, h))
                    return RampChar(S, 1);                                               // lower and inner edge
                if (!ErrPlateAt(k, o + 1f, h)) return RampChar(S, 4);                   // the outer edge catches the light
                if (!ErrPlateAt(k, o, h + 2f)) return RampChar(S, lo > -3f ? 4 : 3);    // just under the rim
                return RampChar(S, lo > 3.5f ? 4 : lo < -4f ? 2 : 3);
            }
            return '.';
        }

        const int ErrPauldronNearMin = -4, ErrPauldronNearMax = 12, ErrPauldronNearRowCount = 16;
        const int ErrPauldronNearRise = 5;
        static float ErrPauldronNearCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((ErrPauldronNearMin + ErrPauldronNearMax) * 0.5f);
        static float ErrPauldronNearY
            => PlateY(ErrPauldronNear) + PrimitiveCharacterRig.Proportions.Cells(ErrPauldronNearRise);

        static string[] _errPauldronNear;
        static string[] ErrPauldronNear => _errPauldronNear ??=
            PaintField(ErrPauldronNearMin, ErrPauldronNearMax, 0, ErrPauldronNearRowCount, (ix, iy) =>
            {
                int o = ErrPauldronNearMin + ErrPauldronNearMax - 1 - ix;    // outward is -X
                int r = ErrPauldronNearRowCount - 1 - iy;
                return ErrPauldronNearTexel(o + 0.5f, ErrPauldronNearRise - r - 0.5f);
            });

        static bool ErrNearCapIn(float o, float h)
        {
            float dx = (o - 3.5f) / 7.5f, dy = (h + 1f) / 5.5f;
            return h > -2.5f && dx * dx + dy * dy < 1f;
        }

        static char ErrPauldronNearTexel(float o, float h)
        {
            const string B = ErrBlack;
            // Drawn for -X: the light comes from the INSIDE.
            int side = o < 2f ? 1 : o > 8f ? -1 : 0;
            if (ErrNearCapIn(o, h))
            {
                if (!ErrNearCapIn(o, h + 1f)) return RampChar(B, 5);
                if (!ErrNearCapIn(o, h - 1f)) return RampChar(B, 1);
                return RampChar(B, 3 + side);
            }
            for (int k = 0; k < 2; k++)
            {
                float top = -2f - 2.8f * k - 0.1f * o, outer = 10.5f - 0.5f * k;
                if (h >= top || h < top - ErrLameHeight || o < -3f || o > outer) continue;
                if (o > outer - 1.2f && h < top - ErrLameHeight + 1.2f) return '.';
                float down = top - h;
                if (down > ErrLameHeight - 1f) return RampChar(B, 1);
                return RampChar(B, (down < 1f ? 4 : 3) + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the gauntlets (Gloves)
        //
        // ARM-local: 0 at the shoulder, the elbow cut at -14, the wrist at -22, the hand below.
        // Row -12 is repeated by the rig to lengthen the upper arm, so only plain cloth or plate
        // may sit there.
        //
        // FAR (+X, outward +X): the arm under the silver pauldron, after the reference - a black
        // sleeve to the elbow, a black WRAP of banded leather round the forearm with a silver
        // rivet on each band, and the HAND BARE. The rig's own hand is the undersuit's dark fist,
        // so leaving the rows empty does not show a hand: it is drawn here in skin (ErrSkin, the
        // leather's slot in this layer's palette) and repainted in the wearer's tone.
        //
        // NEAR (-X, outward -X): the same arm in black plate - a lit lame edge here and there,
        // a brown strap at the wrist.

        const int ErrArmTop = 0, ErrArmBottom = -34;
        const int ErrWrapArmMin = -6, ErrWrapArmMax = 6;

        static string[] _errWrapArmRows, _errBlackArmRows;
        static string[] ErrWrapArmRows => _errWrapArmRows ??=
            PaintField(ErrWrapArmMin, ErrWrapArmMax, ErrArmBottom, ErrArmTop, ErrWrapArmTexel);
        static string[] ErrBlackArmRows => _errBlackArmRows ??=
            PaintField(-6, 6, ErrArmBottom, ErrArmTop, ErrBlackArmTexel);

        static char ErrWrapArmTexel(int ix, int iy)
        {
            const string S = ErrSteel, B = ErrBlack, K = ErrSkin;
            float x = ix + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2.5f);

            // ---- the sleeve, under the pauldron and down through the crook ----
            if (iy >= -20)
            {
                if (ax > 4.5f) return '.';
                if (iy == -18 && x < 1f) return RampChar(B, 2);                          // a fold at the crook
                return RampChar(B, 3 + side);
            }

            // ---- the wrap: three bands, each a lit top, its face, a dark edge over the next ----
            if (iy >= -29)
            {
                float half = 5f - (-21f - iy) * 0.08f;
                if (ax > half) return '.';
                int row = WrapMod(-21 - iy, 3);
                if (row == 0) return RampChar(B, 4 + Mathf.Max(side, 0));
                if (row == 2) return RampChar(B, 1);
                if (ix == 2) return RampChar(S, 5);                                      // its rivet
                return RampChar(B, 3 + side);
            }

            // ---- the hand, bare: the knuckles lit, the fingers split by shade ----
            {
                float half = iy < -32 ? 3.5f : 4f;
                if (ax > half) return '.';
                if (iy == -30) return RampChar(K, 4 + Mathf.Max(side, 0));
                if (ix == -2 || ix == 0 || ix == 2) return RampChar(K, 2);
                return RampChar(K, 3 + Mathf.Max(side, 0));
            }
        }

        static char ErrBlackArmTexel(int ix, int iy)
        {
            const string B = ErrBlack, L = ErrLeather;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2f);

            // ---- the rerebrace ----
            if (iy >= -14)
            {
                if (ax > 4.5f) return '.';
                if (iy == -5 || iy == -9) return RampChar(B, 4 + Mathf.Max(side, 0));    // lame edges
                if (iy == -6 || iy == -10) return RampChar(B, 1);
                return RampChar(B, 3 + side);
            }

            // ---- the couter ----
            {
                float dx = (x + 0.5f) / 4.6f, dy = (y + 17.5f) / 3.2f;
                float r2 = dx * dx + dy * dy;
                if (r2 < 1f)
                {
                    if (r2 > 0.62f && y < -17.5f) return RampChar(B, 1);
                    if (dx > 0.1f && dy > 0.1f && r2 < 0.45f) return RampChar(B, 5);
                    return RampChar(B, 3 + side);
                }
            }

            // ---- the vambrace, strapped at the wrist ----
            if (iy >= -27)
            {
                float half = 4.8f - (-21f - y) * 0.06f;
                if (ax > half) return '.';
                if (iy == -25) return RampChar(L, 3 + Mathf.Max(side, 0));
                if (iy == -21) return RampChar(B, 4 + Mathf.Max(side, 0));
                return RampChar(B, 3 + side);
            }

            // ---- the cuff ----
            if (iy >= -29)
            {
                if (ax > 5.5f) return '.';
                return RampChar(B, iy == -28 ? 5 : 2 + Mathf.Max(side, 0));
            }

            // ---- the hand ----
            {
                float half = iy < -32 ? 3.5f : 4.5f;
                if (ax > half) return '.';
                if (iy == -30) return RampChar(B, 4 + Mathf.Max(side, 0));
                return RampChar(B, WrapMod(iy, 2) == 0 ? 3 + Mathf.Max(side, 0) : 1);
            }
        }

        // ------------------------------------------------------------------ the belt
        //
        // Torso-local, on the belt's band: ONE wide belt with a narrow steel buckle - a tall
        // frame, not Talon's big square - a tall POUCH on the far hip with a strap down its flap,
        // and a DAGGER slung across the near side, hilt at the buckle, sheath slanting down.

        const int ErrBeltTop = 14, ErrBeltBottom = -12, ErrBeltHalf = 18;

        static string[] _errBeltRows;
        static string[] ErrBeltRows => _errBeltRows ??=
            PaintField(-ErrBeltHalf, ErrBeltHalf, ErrBeltBottom, ErrBeltTop, ErrBeltTexel);

        static char ErrBeltTexel(int ix, int iy)
        {
            const string L = ErrLeather, S = ErrSteel, B = ErrBlack;
            float x = ix + 0.5f, y = iy + 0.5f;
            int side = LitSide(x, 10f, -11f);

            // ---- the buckle: a tall frame, its tongue across ----
            if (ix >= -3 && ix < 2 && iy >= 4 && iy < 12)
            {
                bool frame = ix == -3 || ix == 1 || iy == 4 || iy == 11;
                if (frame) return RampChar(S, iy == 11 ? 5 : ix == 1 ? 4 : 2);
                if (iy == 8) return RampChar(S, 4);                                     // the tongue
                return RampChar(L, 1);
            }

            // ---- the pouch, on the far hip ----
            if (ix >= 4 && ix < 10 && iy >= -5 && iy < 6)
            {
                if (ix >= 6 && ix < 8 && iy >= -2)                                      // the strap down the flap
                    return iy == -1 || iy == 0 ? RampChar(S, iy == 0 ? 5 : 3) : RampChar(L, ix == 6 ? 4 : 2);
                if (iy >= 0) return RampChar(L, iy == 5 ? 4 : 3);                       // the flap
                if (iy == -1) return RampChar(L, 1);
                return RampChar(L, (ix >= 8 ? 3 : 2) - (iy == -5 ? 1 : 0));
            }

            // ---- the belt ----
            float farEdge = TalBodyEdge(y) + 0.5f;
            if (iy >= 5 && iy < 11 && x >= -15.5f && x <= farEdge)
                return RampChar(L, iy switch { 10 => 4, 5 => 1, _ => 3 } + side);

            // ---- the dagger: the hilt over the belt, the sheath slanting down the near side ----
            // Along the slant, from the guard (u = 0) to the chape; v across it.
            float u = -(x + 3.5f) * 0.83f - (y - 4f) * 0.55f;
            float v = (x + 3.5f) * 0.55f - (y - 4f) * 0.83f;
            if (u >= -4f && u < 9f && Mathf.Abs(v) < 1.3f)
            {
                if (u < -1f) return u < -3f ? RampChar(S, 4) : RampChar(B, v > 0f ? 4 : 2);  // pommel, grip
                if (u < 0f) return RampChar(S, 5);                                       // the guard
                if (u > 7f) return RampChar(S, v > 0f ? 4 : 2);                          // the chape
                return RampChar(L, v > 0.4f ? 4 : 2);
            }
            if (u >= -1f && u < 0f && Mathf.Abs(v) < 2.6f) return RampChar(S, 4);        // the guard's arms
            return '.';
        }

        // ------------------------------------------------------------------ the faulds (Legs)
        //
        // TORSO-local on RigLayer.Tasset. A MAIL SKIRT from the belt to mid-thigh under TWO ROWS
        // of steel splints, the lower row offset by half a splint - the reference's checker of
        // small plates. Either side, the coat's black FRONT PANELS from the belt to the shin,
        // hanging OUTSIDE the legs (Orichalc's finding: over the thighs their outline swallows
        // whatever the legs wear), hemmed lower on the outside.

        const int ErrFauldTop = 4, ErrFauldBottom = -36, ErrFauldHalf = 20;

        static string[] _errFauldRows;
        static string[] ErrFauldRows => _errFauldRows ??=
            PaintField(-ErrFauldHalf, ErrFauldHalf, ErrFauldBottom, ErrFauldTop, ErrFauldTexel);

        static char ErrFauldTexel(int ix, int iy)
        {
            const string S = ErrSteel, B = ErrBlack;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 6f, -7f);
            if (y >= 3f) return '.';

            // ---- the coat panels ----
            float inner = 12f, outer = 16.5f + (3f - y) * 0.08f;
            if (ax >= inner && ax <= outer)
            {
                float t = (ax - inner) / (outer - inner);
                float hem = -28f - t * 4f;
                if (y < hem) return '.';
                if (y < hem + 1f) return RampChar(B, 2);                                 // the hem
                if (ax < inner + 1f) return RampChar(B, 4 + Mathf.Max(side, 0));        // the front edge, lit
                if (y < -6f && Mathf.Abs(t - 0.55f) < 0.1f) return RampChar(B, 2);      // a fold
                return RampChar(B, (t > 0.85f ? 2 : 3) + side);
            }

            // ---- the splints, two rows ----
            if (ax < 11.5f)
            {
                for (int row = 0; row < 2; row++)
                {
                    float rTop = 1.5f - row * 5f, rBottom = rTop - 5f;
                    if (y >= rTop || y < rBottom) continue;
                    int col = WrapMod(ix + row * 2, 4);
                    if (col == 3) break;                                                 // a gap: mail
                    if (y >= rTop - 1f) return RampChar(S, 4 + Mathf.Max(side, 0));    // lit top
                    if (y < rBottom + 1f) return RampChar(S, 1);                         // lower edge
                    if (col == 0) return RampChar(S, 2);                                 // shaded side
                    return RampChar(S, 3 + Mathf.Max(side, 0));
                }
                // ---- the mail skirt ----
                float mailHem = -15f + Mathf.Abs(Mathf.Sin(x * 0.9f)) * 0.8f;
                if (y >= mailHem) return ErrMail(ix, iy, side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the greaves (Boots)
        //
        // LEG-local, art bottom on the sole, the knee at -24. A steel KNEE COP: domed over the
        // knee, coming to a point DOWN the shin (Talon's points up the thigh), ridged down its
        // middle, a lame tucked under its top. Wider than the leg: at the leg's own width, with a
        // dark row and a gap between it and the lame, it read as an "E". Under it a black boot to just below the knee, a folded cuff, a
        // brown strap at the ankle.

        const int ErrGreaveTop = -12, ErrGreaveBottom = -48, ErrGreaveHalf = 6;

        static string[] _errGreaveRows;
        static string[] ErrGreaveRows => _errGreaveRows ??=
            PaintField(-ErrGreaveHalf, ErrGreaveHalf, ErrGreaveBottom, ErrGreaveTop, ErrGreaveTexel);

        static bool ErrKneeCopIn(float x, float y)
        {
            float ax = Mathf.Abs(x);
            if (y >= -23f)
            {
                float dx = x / 5.5f, dy = (y + 23f) / 5f;
                return dx * dx + dy * dy < 1f;
            }
            return y >= -32f && ax < 5.5f * (y + 32f) / 9f;
        }

        static char ErrGreaveTexel(int ix, int iy)
        {
            const string S = ErrSteel, L = ErrLeather, B = ErrBlack;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2f);

            // ---- the knee cop, over the lame above it ----
            if (ErrKneeCopIn(x, y))
            {
                if (!ErrKneeCopIn(x, y + 1f)) return RampChar(S, x >= 0f ? 5 : 4);
                if (!ErrKneeCopIn(x - 1f, y) || !ErrKneeCopIn(x + 1f, y) || !ErrKneeCopIn(x, y - 1f))
                    return RampChar(S, 1);
                if (ix == 0) return RampChar(S, 5);                                     // the ridge
                if (ix == -1) return RampChar(S, 2);
                return RampChar(S, (y > -23f ? 4 : 3) + (x > 0f ? 0 : -1));
            }
            // ---- the lame above the knee, tucked under the cop ----
            if (iy >= -18 && iy < -14)
            {
                if (ax > 4.5f) return '.';
                return RampChar(S, iy == -15 ? 4 + Mathf.Max(side, 0) : iy == -18 ? 1 : 3);
            }
            if (y >= -27f) return '.';

            // ---- the boot ----
            if (y < -42f)
            {
                if (ax > 5.5f) return '.';
                if (y < -47f) return RampChar(B, 0);                                     // the sole
                if (iy == -43) return RampChar(B, 4 + Mathf.Max(side, 0));
                return RampChar(B, 3 + side);
            }
            if (y < -40f) return ax > 5f ? '.' : RampChar(L, iy == -41 ? 4 : 2);         // the ankle strap
            if (iy >= -32 && iy < -29)                                                    // the cuff
            {
                if (ax > 5.2f) return '.';
                return RampChar(B, iy == -30 ? 4 + Mathf.Max(side, 0) : 2);
            }
            if (ax > 4.5f) return '.';
            return RampChar(B, 3 + side);
        }
    }
}
