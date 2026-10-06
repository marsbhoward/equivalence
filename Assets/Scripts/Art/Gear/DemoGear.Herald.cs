using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Herald: gunmetal plate, the wearer's sigil on one shoulder
    //
    // After the user's reference: a knight in dark, green-grey gunmetal plate edged in gold, a
    // huge pauldron on one shoulder, a chain hanging from that arm's fist, and red cloth hanging
    // from the belt. The reference's pauldron wears a beast's face; this one, the user's call,
    // bears the WEARER'S ELEMENT SIGIL instead. The reference's cloak is NOT part of the set (the
    // user's call) - instead the leg cloth takes the colour of whatever Back piece is worn
    // (GearItem.DyedByBack, see ClothDye, the Survivor mechanism), white with none. The swords on
    // its back and hip are weapons, not armour.
    //
    //   herald_cuirass    Torso      a high gorget of two bands, sculpted pectorals rimmed in
    //                                gold under them, a ridge down the sternum, split ab plates
    //   herald_pauldrons  Shoulders  the SIGIL pauldron (far): a big dome bearing the element's
    //                                alchemical triangle in gold, a rolled lip and two lames
    //                                below; a small lamed cap (near)
    //   herald_gauntlets  Gloves     the far arm heavy - lames, a fanned couter, a gold band at
    //                                the wrist, and a CHAIN hanging from the fist; the near arm
    //                                lighter plate
    //   herald_belt       Belt       one wide belt, a big gold-framed square buckle, steel
    //                                plaques either side of it
    //   herald_tassets    Legs       cloth panels from the belt, gold piping down their front
    //                                edges, hems TORN; steel faulds over the hips
    //   herald_greaves    Boots      big knee cops rimmed in gold, a lame above and below,
    //                                ridged greaves and sabatons
    //
    // THE SIGIL FOLLOWS THE ATTUNEMENT (GearItem.BearsSigil): worn, the pauldron shows the
    // element being played - the run's, or in the hub the sigil selector's - exactly as a Prism
    // lights its gem. Its BASE picture, which is everything that draws the item rather than a
    // wearer (cards, the armour stand, an NFT image), is all four COMBINED: the six-pointed star,
    // which already holds all four marks (each triangle's flat edge is the other one's bar).
    //
    // THE SIGIL PAULDRON IS ON THE FAR ARM - the character's left, facing right (+X) - so the
    // sword arm carries and the mark hangs still: the Talon rule, and the reference's own split
    // too (its beast arm is not the one holding the sword). Both arm pieces are GearItem.Lopsided,
    // so turned away it stays on the left.
    //
    // Fields in each joint's own frame (DemoGear.Fields.cs), lit from +X like every piece here.
    // Diamond, power 0, like every reference-built cosmetic set.
    public static partial class DemoGear
    {
        // The third ramp is the DARK leather on most pieces and the dyed CLOTH on the tassets -
        // the same digits, a different palette per piece.
        const string HerSteel = "ksdblh", HerGold = "KSDBLH", HerDark = "123456", HerCloth = "123456";

        static void AddHerald(List<GearItem> items)
        {
            // Gunmetal, a cool green-grey, lit toward a pale mint rather than white so the lit
            // face keeps the hue the reference's plate has.
            var steel = new Palette.Ramp(new Color(0.42f, 0.49f, 0.47f), lift: 0.40f, shade: 0.38f, line: 0.80f)
                .WithShadowsBelowLight(0.50f, 0.32f)
                .WithHighlightsToward(new Color(0.90f, 1.00f, 0.95f), 0.42f);
            // Gold trim, lit toward pale gold (Bronze's reasoning: a warm metal lit toward white
            // reads as wood).
            var gold = new Palette.Ramp(new Color(0.76f, 0.56f, 0.20f), shade: 0.42f)
                .WithHighlightsToward(new Color(1.00f, 0.90f, 0.56f), 0.42f);
            // Straps, the belt and the hands: a dark slate, a step above the undersuit.
            var dark = new Palette.Ramp(new Color(0.23f, 0.24f, 0.29f), lift: 0.28f, shade: 0.34f, line: 0.80f)
                .WithShadowsBelowLight(0.50f, 0.32f);
            const float farF = 0.85f;

            var pal = Palette.Of(steel, gold, dark);
            var far = Palette.Of(steel.Scaled(farF), gold.Scaled(farF), dark.Scaled(farF));
            // The cloth MUST be ClothDye.Undyed, tone for tone: it is what the dye matches.
            var clothPal = Palette.Of(steel, gold, ClothDye.Undyed);

            items.Add(Defends(Make("herald_cuirass", "Herald Cuirass", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.herald", HerCuirassRows, pal,
                       0f, FieldCentreCells(HerCuirassBottom, HerCuirassTop), ppu: BodyPpu)),
                // Heavy plate and a standard on the shoulder - built to stand its ground.
                DefensiveAbility.Bulwark));

            // The base picture is all four sigils combined; the four worn variants are the same
            // field with one mark, each with its own derived menu art.
            var pauldrons = Make("herald_pauldrons", "Herald Pauldrons", GearSlot.Shoulders, LootTier.Diamond, 0f,
                HerSigilPauldron("combined", ArmBands.All, far),
                Pixels(RigLayer.Shoulders, "gear.pauldron.herald.near", HerNearCapRows, pal,
                       -ShoulderX - HerNearCapCentreCells, HerNearCapY, ppu: BodyPpu));
            pauldrons.Lopsided = true;
            pauldrons.BearsSigil = true;
            pauldrons.SigilLayer = RigLayer.ShouldersBack;
            pauldrons.SigilSprites = new Sprite[4];
            pauldrons.MenuSigilSprites = new Sprite[4];
            foreach (Core.ElementType e in System.Enum.GetValues(typeof(Core.ElementType)))
            {
                var worn = HerSigilPauldron(e.ToString().ToLowerInvariant(), HerBandOf(e), far);
                pauldrons.SigilSprites[(int)e] = worn.Sprite;
                pauldrons.MenuSigilSprites[(int)e] = DerivedMenuLayer(worn).Sprite;
            }
            items.Add(pauldrons);

            var gauntlets = Make("herald_gauntlets", "Herald Gauntlets", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesBack, "gear.gloves.herald", HerChainArmRows, far,
                       0f, FieldCentreCells(HerChainBottom, HerArmTop), ppu: BodyPpu),
                Pixels(RigLayer.GlovesFront, "gear.gloves.herald.near", HerNearArmRows, pal,
                       0f, FieldCentreCells(HerArmBottom, HerArmTop), ppu: BodyPpu));
            gauntlets.Lopsided = true;
            items.Add(gauntlets);

            items.Add(Make("herald_belt", "Herald Belt", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.herald", HerBeltRows, pal,
                       0f, FieldCentreCells(HerBeltBottom, HerBeltTop), ppu: BodyPpu)));

            var tassets = Make("herald_tassets", "Herald Tassets", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.Tasset, "gear.legs.herald", HerSkirtRows, clothPal,
                       0f, FieldCentreCells(HerSkirtBottom, HerSkirtTop), ppu: BodyPpu));
            tassets.DyedByBack = true;
            items.Add(tassets);

            items.Add(Make("herald_greaves", "Herald Greaves", GearSlot.Boots, LootTier.Diamond, 0f,
                Pixels(RigLayer.BootsFront, "gear.boots.herald", HerGreaveRows, pal,
                       0f, FieldCentreCells(HerGreaveBottom, HerGreaveTop), ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.herald.dark", HerGreaveRows, far,
                       0f, FieldCentreCells(HerGreaveBottom, HerGreaveTop), ppu: BodyPpu)));

            Dyeable(items, "herald_", DyeChannel.Of("Plate", DyeMaterial.Metal, steel, farF),
                    DyeChannel.Of("Trim", DyeMaterial.Metal, gold, farF));
        }

        // ------------------------------------------------------------------ the cuirass (Torso)
        //
        // Torso-local, the waist at 0, the chin at 40. A GORGET of two bands at the neck; under it
        // two sculpted PECTORALS meeting at a lit ridge down the sternum, their lower edge a gold
        // rim dipping under each one; under them the ABDOMEN in two rows of plates split down the
        // middle. The flanks in dark leather - the arms cover nearly all of it.

        const int HerCuirassTop = 46, HerCuirassBottom = 4, HerCuirassHalf = 16;

        static string[] _herCuirassRows;
        static string[] HerCuirassRows => _herCuirassRows ??=
            PaintField(-HerCuirassHalf, HerCuirassHalf, HerCuirassBottom, HerCuirassTop, HerCuirassTexel);

        static float HerGorgetBottom(float ax) => 34.5f + ax * 0.15f;

        /// <summary>The pectorals' lower edge: lowest under each pec, rising to the sternum and the sides.</summary>
        static float HerPecBottom(float ax) => 21.5f + Mathf.Abs(ax - 5f) * 0.38f;

        /// <summary>The abdomen's half-width: narrowing to the waist.</summary>
        static float HerAbHalf(float y) => 7.5f - (20f - y) * 0.12f;

        static char HerCuirassTexel(int ix, int iy)
        {
            const string S = HerSteel, G = HerGold, D = HerDark;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 5f, -6f);

            float top = 40.5f - Mathf.Max(0f, ax - 6f) * 0.45f;
            float half = x < 0f ? (iy >= 26 ? 13.5f : 13f) : TalBodyEdge(y);
            if (y >= top || ax > half) return '.';

            // ---- the gorget: two bands, each with a rolled lower edge ----
            float gorget = HerGorgetBottom(ax);
            if (y >= gorget)
            {
                if (y < gorget + 1f) return RampChar(S, 4 + Mathf.Max(side, 0));
                if (y >= gorget + 2.5f && y < gorget + 3.5f) return RampChar(S, 4 + Mathf.Max(side, 0));
                if (y >= gorget + 1.5f && y < gorget + 2.5f) return RampChar(S, 1);  // the upper band's shadow
                return RampChar(S, 2 + Mathf.Max(side, 0));
            }
            if (y >= gorget - 1f) return RampChar(S, 0);                                 // the gorget's shadow

            // ---- the pectorals ----
            float pec = HerPecBottom(ax);
            if (y >= pec)
            {
                if (y < pec + 1f) return RampChar(G, x > 0f ? 4 : 3);                     // the gold rim
                if (ix == 0) return RampChar(S, 5);                                      // the sternum ridge
                if (ix == -1) return RampChar(S, 2);
                if (ax > 10.5f) return RampChar(S, 2);                                   // turning under the arm
                // Each pec is a dome lit up and right: a highlight high on the lit pec.
                float hx = x - (x > 0f ? 5f : -5f), hy = y - 29f;
                if (hx * hx + hy * hy < 5f) return RampChar(S, x > 0f ? 5 : 4);
                return RampChar(S, (y < pec + 2.5f ? 2 : 3) + side);
            }
            if (y >= pec - 1f && ax < HerAbHalf(y) + 1f) return RampChar(S, 0);         // its shadow

            // ---- the abdomen: two rows of plates, split down the middle ----
            float abHalf = HerAbHalf(y);
            if (ax < abHalf)
            {
                if (ix == -1 || ix == 0) return RampChar(S, ix == 0 ? 2 : 1);            // the split
                float rowTop = pec - 1f;
                int row = y >= 13f ? 0 : 1;
                float lTop = row == 0 ? rowTop : 13f;
                if (y >= lTop - 1f) return RampChar(S, 4 + Mathf.Max(side, 0));          // the lit top edge
                if (y < (row == 0 ? 14f : 5f)) return RampChar(S, 1);                    // the edge over the next
                if (ax > abHalf - 1f) return RampChar(S, 2);
                return RampChar(S, 3 + side);
            }

            // ---- the flanks ----
            return RampChar(D, 3 + side);
        }

        // ------------------------------------------------------------------ the pauldrons (Shoulders)
        //
        // FAR (+X): THE SIGIL PAULDRON. A big DOME over the shoulder, standing well above the
        // chin, bearing the element's ALCHEMICAL SIGIL in gold, set into the plate with a shadow
        // on its lower-left - the sigil door's triangles, Fire and Air pointing up, Water and
        // Earth down, Air and Earth crossed by a bar through the triangle's middle. The combined
        // picture is the six-pointed STAR, which holds all four: each triangle's flat edge is the
        // other's bar. A
        // rolled LIP round the dome's lower half, a black gap under it, and two lames hanging
        // below, out over the upper arm.
        //
        // NEAR (-X): a small cap and three lames, the cap rimmed in gold - kept small so the
        // sigil owns the silhouette.
        //
        // (Outward, height above the chin) texels from the joint, Talon's frame.

        const int HerSigilMin = -6, HerSigilMax = 22, HerSigilRowCount = 32, HerSigilRise = 12;

        static float HerSigilCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((HerSigilMin + HerSigilMax) * 0.5f);
        static float HerSigilY
            => PlateY(HerSigilRows(ArmBands.All)) + PrimitiveCharacterRig.Proportions.Cells(HerSigilRise);

        static readonly Vector2 HerDomeCentre = new(7.5f, 0.5f);
        const float HerDomeRx = 10.5f, HerDomeRy = 10.5f;

        static bool HerDomeIn(float o, float h)
        {
            float dx = (o - HerDomeCentre.x) / HerDomeRx, dy = (h - HerDomeCentre.y) / HerDomeRy;
            return dx * dx + dy * dy < 1f;
        }

        /// <summary>The rolled lip: a ring a little wider than the dome, round its lower half only.</summary>
        static bool HerLipIn(float o, float h)
        {
            if (h > HerDomeCentre.y - 3f) return false;
            float dx = (o - HerDomeCentre.x) / (HerDomeRx + 1.6f), dy = (h - HerDomeCentre.y) / (HerDomeRy + 1.4f);
            return dx * dx + dy * dy < 1f;
        }

        /// <summary>The sigil's centre and its triangles' circumradius; the stroke's half-width.</summary>
        static readonly Vector2 HerMarkCentre = new(8f, 0f);
        const float HerMarkRadius = 7.6f, HerMarkHalf = 0.9f;

        static ArmBands HerBandOf(Core.ElementType e) => e switch
        {
            Core.ElementType.Fire => ArmBands.Fire,
            Core.ElementType.Water => ArmBands.Water,
            Core.ElementType.Earth => ArmBands.Earth,
            _ => ArmBands.Air,
        };

        /// <summary>Whether (o, h) is on the gold of the sigil for <paramref name="marks"/>.</summary>
        static bool HerMarkAt(float o, float h, ArmBands marks)
        {
            bool up = (marks & ArmBands.Rising) != 0, down = (marks & ArmBands.Falling) != 0;
            bool bar = (marks & (ArmBands.Air | ArmBands.Earth)) != 0;
            var p = new Vector2(o, h) - HerMarkCentre;
            float inr = HerMarkRadius * 0.5f;

            // Distance outward through a triangle's nearest edge: the largest projection on its
            // three outward edge normals (ArmSigil's measure).
            float Reach(bool pointingUp)
            {
                float best = float.MinValue;
                for (int i = 0; i < 3; i++)
                {
                    float a = ((pointingUp ? 270f : 90f) + i * 120f) * Mathf.Deg2Rad;
                    best = Mathf.Max(best, Vector2.Dot(p, new Vector2(Mathf.Cos(a), Mathf.Sin(a))));
                }
                return best;
            }
            float mUp = up ? Reach(true) : float.MaxValue, mDown = down ? Reach(false) : float.MaxValue;
            if ((up && Mathf.Abs(mUp - inr) < HerMarkHalf) || (down && Mathf.Abs(mDown - inr) < HerMarkHalf))
                return true;
            // BOTH triangles draw no bar of their own: the star already holds Air's and Earth's,
            // each triangle's flat edge crossing the other. A bar through the centre as well (the
            // Armillary's rule, on a far bigger mark) closed the middle into a "B" at this size.
            if (!bar || (up && down) || (mUp > inr && mDown > inr)) return false;
            // One triangle: the bar through its middle.
            float barY = up ? HerMarkRadius * 0.25f : -HerMarkRadius * 0.25f;
            return Mathf.Abs(p.y - barY) < HerMarkHalf * 0.85f;
        }

        static readonly Dictionary<ArmBands, string[]> _herSigilRows = new();
        static string[] HerSigilRows(ArmBands marks)
        {
            if (_herSigilRows.TryGetValue(marks, out var rows) && rows != null) return rows;
            return _herSigilRows[marks] = PaintField(HerSigilMin, HerSigilMax, 0, HerSigilRowCount, (ix, iy) =>
            {
                int r = HerSigilRowCount - 1 - iy;                       // outward is +X
                return HerSigilTexel(ix + 0.5f, HerSigilRise - r - 0.5f, marks);
            });
        }

        static LayerSprite HerSigilPauldron(string name, ArmBands marks, Dictionary<char, Color> palette)
            => Pixels(RigLayer.ShouldersBack, "gear.pauldron.herald." + name, HerSigilRows(marks), palette,
                      ShoulderX + HerSigilCentreCells, HerSigilY, ppu: BodyPpu);

        static char HerSigilTexel(float o, float h, ArmBands marks)
        {
            const string S = HerSteel, G = HerGold;
            float lo = o - HerDomeCentre.x;
            int side = lo > 3f ? 1 : lo < -5f ? -1 : 0;

            if (HerDomeIn(o, h))
            {
                // ---- the sigil ----
                if (HerMarkAt(o, h, marks))
                {
                    bool lit = HerMarkAt(o - 1f, h - 1f, marks) && !HerMarkAt(o + 1f, h + 1f, marks);
                    return RampChar(G, lit ? 5 : (o - HerMarkCentre.x) + h > 0f ? 4 : 3);
                }
                if (HerMarkAt(o + 1f, h + 1f, marks)) return RampChar(S, 1);            // its shadow, lower left

                // ---- the dome ----
                if (!HerDomeIn(o, h + 1f)) return RampChar(S, side >= 0 ? 5 : 4);       // the lit crown
                if (!HerDomeIn(o + 1f, h)) return RampChar(S, 4);                        // the outer edge catches the light
                if (!HerDomeIn(o - 1f, h) || !HerDomeIn(o, h - 1f)) return RampChar(S, 1);
                return RampChar(S, 3 + side);
            }

            // ---- the lip ----
            if (HerLipIn(o, h))
            {
                if (HerDomeIn(o, h + 1f)) return RampChar(S, 0);                         // the gap under the dome
                if (!HerLipIn(o, h - 1f)) return RampChar(S, 1);                         // its lower edge
                return RampChar(G, side >= 0 ? 4 : 3);                                   // the gilded roll
            }

            // ---- two lames below, out over the upper arm ----
            for (int k = 0; k < 2; k++)
            {
                float lTop = -9.5f - 4.2f * k - 0.12f * o, lBottom = lTop - 4.6f;
                float inner = -2.5f + k, outer = 15.5f - 1.5f * k;
                if (h >= lTop || h < lBottom || o < inner || o > outer) continue;
                if (o > outer - 1.2f && h < lBottom + 1.2f) return '.';                 // the rounded corner
                if (h >= lTop - 1f) return RampChar(S, 4 + Mathf.Max(side, 0));
                if (h < lBottom + 1f) return RampChar(S, 1);
                return RampChar(S, 3 + side);
            }
            return '.';
        }

        const int HerNearCapMin = -4, HerNearCapMax = 14, HerNearCapRowCount = 20, HerNearCapRise = 6;

        static float HerNearCapCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((HerNearCapMin + HerNearCapMax) * 0.5f);
        static float HerNearCapY
            => PlateY(HerNearCapRows) + PrimitiveCharacterRig.Proportions.Cells(HerNearCapRise);

        static bool HerNearCapIn(float o, float h)
        {
            float dx = (o - 4.5f) / 8.5f, dy = (h + 0.5f) / 6f;
            return h > -3.5f && dx * dx + dy * dy < 1f;
        }

        static string[] _herNearCapRows;
        static string[] HerNearCapRows => _herNearCapRows ??=
            PaintField(HerNearCapMin, HerNearCapMax, 0, HerNearCapRowCount, (ix, iy) =>
            {
                int o = HerNearCapMin + HerNearCapMax - 1 - ix;           // outward is -X
                int r = HerNearCapRowCount - 1 - iy;
                return HerNearCapTexel(o + 0.5f, HerNearCapRise - r - 0.5f);
            });

        static char HerNearCapTexel(float o, float h)
        {
            const string S = HerSteel, G = HerGold;
            // Drawn for -X: the light comes from the INSIDE.
            int side = o < 2f ? 1 : o > 8f ? -1 : 0;
            if (HerNearCapIn(o, h))
            {
                if (!HerNearCapIn(o, h - 1f)) return RampChar(G, 3 + Mathf.Max(side, 0)); // the gold rim
                if (!HerNearCapIn(o, h + 1f)) return RampChar(S, 5);
                return RampChar(S, 3 + side);
            }
            for (int k = 0; k < 3; k++)
            {
                float lTop = -3.5f - 3.2f * k - 0.1f * o, lBottom = lTop - 3.6f;
                float outer = 12f - 0.8f * k;
                if (h >= lTop || h < lBottom || o < -3f || o > outer) continue;
                if (o > outer - 1.2f && h < lBottom + 1.2f) return '.';
                if (h >= lTop - 1f) return RampChar(S, 0);                               // the gap under the plate above
                if (h < lBottom + 1f) return RampChar(S, 1);
                return RampChar(S, (h >= lTop - 2f ? 4 : 3) + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the gauntlets (Gloves)
        //
        // ARM-local: 0 at the shoulder, the elbow cut at -14, the wrist at -22, the hand below.
        // Row -12 is repeated by the rig to lengthen the upper arm, so only plain plate sits there.
        //
        // FAR (+X): the sigil pauldron's arm, heavy. Lames down the upper arm, a big COUTER with a fan
        // flaring out from it, a lamed vambrace, a GOLD band at the wrist, a dark plated fist -
        // and the CHAIN hanging from it, the reference's. Links in MID steel with one dark texel
        // in three (the Survivor chain's finding). The grid runs past the hand to carry it; the
        // wrist cut puts it on the hand layer, so it swings with the fist.
        //
        // NEAR (-X): the sword arm, lighter - two lames, a round couter, a plain vambrace, a gold
        // cuff edge.

        const int HerArmTop = 0, HerArmBottom = -34, HerChainBottom = -48;

        static string[] _herChainArmRows, _herNearArmRows;
        static string[] HerChainArmRows => _herChainArmRows ??=
            PaintField(-6, 6, HerChainBottom, HerArmTop, HerChainArmTexel);
        static string[] HerNearArmRows => _herNearArmRows ??=
            PaintField(-6, 6, HerArmBottom, HerArmTop, HerNearArmTexel);

        static char HerChainArmTexel(int ix, int iy)
        {
            const string S = HerSteel, G = HerGold, D = HerDark;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2f);

            // ---- the rerebrace: lames ----
            if (iy >= -14)
            {
                if (ax > 4.6f) return '.';
                if (iy == -3 || iy == -7) return RampChar(S, 1);                         // each lame's lower edge
                if (iy == -4 || iy == -8) return RampChar(S, 4 + Mathf.Max(side, 0));
                return RampChar(S, 3 + side);
            }

            // ---- the couter, a fan flaring outward from it ----
            {
                float dx = (x - 0.3f) / 5f, dy = (y + 17f) / 3.4f;
                float r2 = dx * dx + dy * dy;
                bool fan = x > 3f && x < 6f && y > -19.5f + (x - 3f) * 0.5f && y < -14.5f - (x - 3f) * 0.5f;
                if (r2 < 1f || fan)
                {
                    if (fan && r2 >= 1f) return RampChar(S, x > 5f ? 2 : 4);
                    if (dy < -0.6f) return RampChar(G, 2);                                // the gilt lower rim
                    if (dx > -0.1f && dx < 0.45f && dy > 0f && dy < 0.65f) return RampChar(S, 5);
                    return RampChar(S, 3 + side);
                }
            }

            // ---- the vambrace: two lames ----
            if (iy >= -27)
            {
                float half = 5f - (-20f - y) * 0.04f;
                if (ax > half) return '.';
                if (iy == -23) return RampChar(S, 1);
                if (iy == -24 || iy == -20) return RampChar(S, 4 + Mathf.Max(side, 0));
                if (ix == 1) return RampChar(S, 5);                                     // the ridge
                if (ix == 0) return RampChar(S, 2);
                return RampChar(S, 3 + side);
            }

            // ---- the gold band at the wrist ----
            if (iy >= -29)
            {
                if (ax > 5.6f) return '.';
                return RampChar(G, iy == -28 ? 4 + Mathf.Max(side, 0) : 2);
            }

            // ---- the fist ----
            if (iy >= -34)
            {
                float half = iy < -32 ? 3.8f : 4.6f;
                if (ax > half) return '.';
                if (iy == -30) return RampChar(S, 4 + Mathf.Max(side, 0));               // the knuckle plate
                if (ix == -2 || ix == 0 || ix == 2) return RampChar(D, 1);
                return RampChar(D, 3 + Mathf.Max(side, 0));
            }

            // ---- the chain, hanging from the fist ----
            if (ix == 0 || ix == 1)
                return RampChar(S, WrapMod(iy, 3) switch { 0 => 4, 1 => 3, _ => 1 } - (ix == 0 ? 1 : 0));
            return '.';
        }

        static char HerNearArmTexel(int ix, int iy)
        {
            const string S = HerSteel, G = HerGold, D = HerDark;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2f);

            // ---- the rerebrace ----
            if (iy >= -14)
            {
                if (ax > 4.5f) return '.';
                if (iy == -5) return RampChar(S, 1);
                if (iy == -6) return RampChar(S, 4 + Mathf.Max(side, 0));
                return RampChar(S, 3 + side);
            }

            // ---- the couter ----
            {
                float dx = (x + 0.3f) / 4.6f, dy = (y + 17f) / 3.2f;
                float r2 = dx * dx + dy * dy;
                if (r2 < 1f)
                {
                    if (dy < -0.6f) return RampChar(S, 1);
                    if (dx > 0f && dx < 0.5f && dy > 0f && dy < 0.65f) return RampChar(S, 5);
                    return RampChar(S, 3 + side);
                }
            }

            // ---- the vambrace ----
            if (iy >= -27)
            {
                float half = 4.7f - (-20f - y) * 0.04f;
                if (ax > half) return '.';
                if (ix == 1) return RampChar(S, 5);
                if (ix == 0) return RampChar(S, 2);
                return RampChar(S, 3 + side);
            }

            // ---- the cuff, gold-edged ----
            if (iy >= -29)
            {
                if (ax > 5.3f) return '.';
                return iy == -28 ? RampChar(G, 4 + Mathf.Max(side, 0)) : RampChar(S, 1);
            }

            // ---- the hand ----
            {
                float half = iy < -32 ? 3.5f : 4.5f;
                if (ax > half) return '.';
                if (iy == -30) return RampChar(S, 4 + Mathf.Max(side, 0));
                if (ix == -2 || ix == 0 || ix == 2) return RampChar(D, 1);
                return RampChar(D, 3 + Mathf.Max(side, 0));
            }
        }

        // ------------------------------------------------------------------ the belt
        //
        // Torso-local, on the belt's band. ONE wide belt with a big SQUARE buckle - a gold frame
        // round a steel plate - and a steel plaque either side of it, a rivet in each.

        const int HerBeltTop = 14, HerBeltBottom = -12, HerBeltHalf = 18;

        static string[] _herBeltRows;
        static string[] HerBeltRows => _herBeltRows ??=
            PaintField(-HerBeltHalf, HerBeltHalf, HerBeltBottom, HerBeltTop, HerBeltTexel);

        static char HerBeltTexel(int ix, int iy)
        {
            const string S = HerSteel, G = HerGold, D = HerDark;
            float x = ix + 0.5f, y = iy + 0.5f;
            int side = LitSide(x, 8f, -9f);
            float farEdge = TalBodyEdge(y) + 0.5f;

            // ---- the buckle ----
            if (ix >= -4 && ix < 4 && iy >= 3 && iy < 13)
            {
                if (iy == 12) return RampChar(G, 5);
                if (ix == 3) return RampChar(G, 4);
                if (ix == -4) return RampChar(G, 2);
                if (iy == 3) return RampChar(G, 1);
                if (ix == -3 || iy == 4) return RampChar(S, 1);                          // the plate's shaded inner edge
                if (ix == 2 && iy == 11) return RampChar(S, 5);
                return RampChar(S, ix >= 0 ? 4 : 3);
            }

            // ---- the belt ----
            if (iy >= 4 && iy < 12 && x >= -15.5f && x <= farEdge)
            {
                // A plaque either side of the buckle.
                bool plaque = (ix >= -12 && ix < -8) || (ix >= 6 && ix < 10);
                if (plaque && iy >= 5 && iy < 11)
                {
                    if (iy == 10) return RampChar(S, 4 + Mathf.Max(side, 0));
                    if (iy == 5) return RampChar(S, 1);
                    if (iy == 8 && (ix == -10 || ix == 8)) return RampChar(S, 5);         // its rivet
                    return RampChar(S, 3 + side);
                }
                return RampChar(D, iy switch { 11 => 4, 4 => 1, _ => 3 } + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the tassets (Legs)
        //
        // TORSO-local on RigLayer.Tasset. The reference's red CLOTH, in ClothDye.Undyed so it
        // takes the cape's colour: a panel down the FRONT of each thigh from under the belt,
        // parted at the middle where the dark trousers show, GOLD PIPING down each parted edge,
        // the hems TORN above the knee so the cops stay in view - big tears (the Hellspawn rule).
        // The near panel runs on outside the leg as one long torn TAIL to the shin. A steel FAULD
        // over each hip, the lower lame gilt-edged.
        //
        // THE FAR SIDE STOPS AT THE BODY'S EDGE. The far arm draws behind this layer, and its fist
        // hangs at the belt with the chain below it: a far panel or fauld out past the hip buried
        // both (Talon's finding, for the same reason).

        const int HerSkirtTop = 4, HerSkirtBottom = -36, HerSkirtHalf = 18;
        const float HerPanelInner = 1.5f, HerFarEdge = 8.5f;

        static string[] _herSkirtRows;
        static string[] HerSkirtRows => _herSkirtRows ??=
            PaintField(-HerSkirtHalf, HerSkirtHalf, HerSkirtBottom, HerSkirtTop, HerSkirtTexel);

        static float HerPanelOuter(float y, bool nearSide) => nearSide ? 14.5f + (2f - y) * 0.05f : HerFarEdge;

        /// <summary>The torn hem's height at x: teeth along it, a V torn up into the near panel,
        /// and the near panel's outer part hanging on as a long tail.</summary>
        static float HerHem(float x)
        {
            float ax = Mathf.Abs(x);
            float teeth = Mathf.Abs(WrapMod(Mathf.FloorToInt(x + 40f), 4) - 1.5f);
            if (x >= 0f) return -13.5f + teeth;
            float hem = -15f + teeth;
            float tear = 1f - Mathf.Abs(ax - 6f) / 2.2f;
            if (tear > 0f) hem += tear * 5f;
            float tail = Mathf.Clamp01((ax - 8.5f) / 1.5f);
            return Mathf.Lerp(hem, -31f + (ax - 10f) * 0.9f + teeth, tail);
        }

        static char HerSkirtTexel(int ix, int iy)
        {
            const string S = HerSteel, G = HerGold, C = HerCloth;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 5f, -6f);
            bool nearSide = x < 0f;
            if (y >= 3f) return '.';

            // ---- the faulds ----
            for (int k = 0; k < 2; k++)
            {
                float lTop = 2.5f - 4.5f * k - 0.15f * (ax - 8f), lBottom = lTop - 5f;
                float inner = nearSide ? 7f + k : 5f + k, outer = nearSide ? 15f + k * 0.5f : HerFarEdge;
                if (y >= lTop || y < lBottom || ax < inner || ax > outer) continue;
                if (nearSide && ax > outer - 1.2f && y < lBottom + 1.2f) return '.';   // the rounded corner
                if (y >= lTop - 1f) return RampChar(S, 4 + Mathf.Max(side, 0));
                if (y < lBottom + 1f) return k == 1 ? RampChar(G, 3 + Mathf.Max(side, 0)) : RampChar(S, 1);
                if (y < lBottom + 2f) return RampChar(S, 2);
                return RampChar(S, 3 + side);
            }

            // ---- the cloth panels ----
            if (ax < HerPanelInner || ax > HerPanelOuter(y, nearSide)) return '.';
            float hem = HerHem(x);
            if (y < hem) return '.';
            if (y < hem + 1f) return RampChar(C, 2);                                     // the frayed edge
            if (y >= 1f) return RampChar(C, 2);                                          // the belt's shadow
            if (ax < HerPanelInner + 1f) return RampChar(C, 4 + Mathf.Max(side, 0));    // the parted edge, lit
            if (ax < HerPanelInner + 2f) return RampChar(G, 3 + Mathf.Max(side, 0));    // the piping
            if (nearSide && ax >= 9f && ax < 10f && y < -4f) return RampChar(C, 1);      // the fold the tail hangs from
            if (ax >= 5.5f && ax < 6.5f && y < -2f && y > hem + 2f) return RampChar(C, 2); // a fold down the thigh
            return RampChar(C, 3 + side);
        }

        // ------------------------------------------------------------------ the greaves (Boots)
        //
        // LEG-local, the sole at -48, the knee at -24. A big round KNEE COP, wider than the leg
        // (Errant's finding: at the leg's width it read as an "E"), gold along its lower rim and a
        // ridge down its middle; a lame above it and one below. A GREAVE to the ankle with a lit
        // ridge down the shin, and a SABATON of two lames.

        const int HerGreaveTop = -12, HerGreaveBottom = -48, HerGreaveHalf = 6;

        static string[] _herGreaveRows;
        static string[] HerGreaveRows => _herGreaveRows ??=
            PaintField(-HerGreaveHalf, HerGreaveHalf, HerGreaveBottom, HerGreaveTop, HerGreaveTexel);

        static bool HerKneeCopIn(float x, float y)
        {
            float dx = x / 5.6f, dy = (y + 23.5f) / 4.4f;
            return dx * dx + dy * dy < 1f;
        }

        static char HerGreaveTexel(int ix, int iy)
        {
            const string S = HerSteel, G = HerGold, D = HerDark;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2f);

            // ---- the knee cop ----
            if (HerKneeCopIn(x, y))
            {
                if (!HerKneeCopIn(x, y - 1f)) return RampChar(G, x >= 0f ? 4 : 2);       // the gold lower rim
                if (!HerKneeCopIn(x, y + 1f)) return RampChar(S, x >= 0f ? 5 : 4);
                if (!HerKneeCopIn(x - 1f, y) || !HerKneeCopIn(x + 1f, y)) return RampChar(S, 1);
                if (ix == 0) return RampChar(S, 5);                                     // the ridge
                if (ix == -1) return RampChar(S, 2);
                return RampChar(S, 3 + side);
            }
            // ---- the lame above it ----
            if (iy >= -19 && iy < -15)
            {
                if (ax > 4.6f) return '.';
                return RampChar(S, iy == -16 ? 4 + Mathf.Max(side, 0) : iy == -19 ? 1 : 3);
            }
            if (y >= -27f) return '.';

            // ---- the lame below it ----
            if (iy >= -31)
            {
                if (ax > 4.8f) return '.';
                if (iy == -28) return RampChar(S, 0);                                   // the gap under the cop
                return RampChar(S, iy == -29 ? 4 + Mathf.Max(side, 0) : iy == -31 ? 1 : 3);
            }

            // ---- the sabaton ----
            if (iy < -42)
            {
                if (ax > 5.5f) return '.';
                if (iy == -48) return RampChar(D, 0);                                   // the sole
                if (iy == -43 || iy == -46) return RampChar(S, 4 + Mathf.Max(side, 0)); // two lames
                if (iy == -44) return RampChar(S, 1);
                return RampChar(S, 3 + side);
            }

            // ---- the greave ----
            float half = 4.6f - (-32f - y) * 0.04f;
            if (ax > half) return '.';
            if (ix == 1) return RampChar(S, 5);                                         // the shin ridge
            if (ix == 0) return RampChar(S, 2);
            return RampChar(S, 3 + side);
        }
    }
}
