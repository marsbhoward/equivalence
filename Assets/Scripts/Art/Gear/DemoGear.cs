using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using Convergence.Art;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// Runtime-generated stand-in gear, used only while Assets/Resources/Gear/ is empty.
    ///
    /// The point is that gear swapping is demonstrable and testable before any art exists -
    /// the same reason the rest of the game draws itself from primitives. Authoring one real
    /// GearItem asset switches the whole catalog over; nothing here needs deleting.
    /// </summary>
    public static partial class DemoGear
    {
        public static List<GearItem> Build()
        {
            var items = new List<GearItem>();

            var goldPal   = Palette.Of(Palette.Tier(LootTier.Gold));
            var goldDark  = Palette.Of(new Palette.Ramp(Color.Lerp(Palette.Tier(LootTier.Gold).Base, Color.black, 0.26f)));
            var goldGrip  = Palette.Of(Palette.Tier(LootTier.Gold), Palette.Leather);
            var silverPal = Palette.Of(Palette.Tier(LootTier.Silver));
            // Discs need a SECOND ramp: their grip bar is drawn in uppercase (K/B), and an
            // unmapped char rasterises to Color.clear - silver discs were being drawn with a
            // transparent slot straight through the middle where the handle should be.
            var silverGrip= Palette.Of(Palette.Tier(LootTier.Silver), Palette.Leather);
            var silverDrk = Palette.Of(new Palette.Ramp(Color.Lerp(Palette.Tier(LootTier.Silver).Base, Color.black, 0.26f)));
            var leatherPal= Palette.Of(Palette.Leather);
            var gemPal    = Palette.Of(Palette.Tier(LootTier.Gold), Palette.Tier(LootTier.Diamond));
            var voidPal   = Palette.Of(Palette.Tier(LootTier.BlackDiamond), Palette.Tier(LootTier.Diamond));
            // A material, not a tier - see Palette.Void. The grip is leather like every other
            // disc's; only the metal differs.
            var voidGrip  = Palette.Of(Palette.Void, Palette.Leather);

            // The two black-diamond weapons' RELIC marks - what shows on RigLayer.Trinket
            // whichever slot currently holds the item, weapon or relic. Opaque, unlike the
            // blades themselves: a worn stone isn't the sword, so it doesn't borrow ShadowPal's
            // alpha cut. Shadow keeps the same cool violet-black family as its blade; Emberline
            // keeps the flame on its OWN ramp so it can glow independently of the dark frame.
            var shadowRelicPal = Palette.Of(new Palette.Ramp(new Color(0.17f, 0.16f, 0.24f)));
            var emberRelicPal  = Palette.Of(new Palette.Ramp(new Color(0.15f, 0.13f, 0.12f)),
                                            new Palette.Ramp(new Color(0.95f, 0.55f, 0.15f)));

            // A dark stained wood stave, leather-wrapped at the grip - named as materials rather
            // than a tier, per the rule that a tier is a rarity and never a palette. Darker and
            // more saturated than the first pass: at (0.62,0.46,0.28) the wood sat almost on top
            // of the body's own skin tone and the whole weapon read as an extension of the arm
            // rather than an object held in it.
            var bowPal = Palette.Of(new Palette.Ramp(new Color(0.40f, 0.24f, 0.12f)), Palette.Leather);

            // ---- one item per slot, so all twelve read on the character and in the loadout ----

            items.Add(Make("gold_helm", "Gilded Helm", GearSlot.Head, LootTier.Gold, 7f,
                // 12, following the crown - the head grew two texels taller. A helm left at 10
                // sits two texels into the skull it is supposed to cap.
                Pixels(RigLayer.HeadArmor, "gear.head.gold", HelmRows, goldPal, 0f, 12f, ppu: BodyPpu)));

            items.Add(Make("gold_pauldrons", "Gilded Pauldrons", GearSlot.Shoulders, LootTier.Gold, 6f,
                // Symmetric shape, so the far side is the same grid on a darker ramp.
                //
                // X comes FROM THE JOINT, with the sign matched to the arm it caps: the rig builds
                // _armFront at -ShoulderHalfSpanCells and _armBack at +, and RigLayer.Shoulders is
                // the near plate (it draws after ArmFront), so it takes the negative side. Typed
                // literals are what put the old pair five cells outboard of the shoulders.
                //
                // Y is the shoulder joint less half the plate: the grid is 16 texels = 8 cells tall
                // and centre-pivoted, so a cap whose lower edge reaches two cells past the joint
                // sits at ShoulderYCells - 2. Placed against the joint rather than the torso's top,
                // because what it caps is the arm.
                Pixels(RigLayer.Shoulders, "gear.pauldron.gold", PauldronRows, goldPal,
                       -ShoulderX, ShoulderPlateY, ppu: BodyPpu),
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.gold.dark", PauldronRows, goldDark,
                       ShoulderX, ShoulderPlateY, ppu: BodyPpu)));

            // One of four cuirasses built to cover DefensiveAbility's whole enum deliberately -
            // see the block after silver_plate below for the other two and the full rationale.
            // Gilded pairs with Barrier: an ornate knight's plate reads as frontal protection
            // more than any of the other three.
            items.Add(Defends(Make("gold_plate", "Gilded Cuirass", GearSlot.Torso, LootTier.Gold, 11f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.gold", CuirassRows, goldPal, 0f, 14f, ppu: Convergence.Art.Gear.PrimitiveCharacterRig.Proportions.BodyPpu)),
                DefensiveAbility.Barrier));

            items.Add(Make("diamond_cloak", "Prismatic Cloak", GearSlot.Back, LootTier.Diamond, 0f,
                Pixels(RigLayer.Back, "gear.back.diamond", CapeRows,
                       Palette.Of(Palette.Tier(LootTier.Diamond)), 0f, 8f)));

            // The same cape body as diamond_cloak, plus a cowl on RigLayer.Hood. One ramp for
            // both pieces, so the cape and its hood read as one garment rather than two.
            //
            // Black, on purpose - and the ONLY colour hand-authored here. A tier is a rarity, not
            // a palette (see the weapon-class notes), and a cloak's colour carries no identity of
            // its own the way the four named scarves' dyes do, so this does not repeat that
            // pattern: further colours are left to the general loot roll rather than hand-authored
            // as separate catalog items.
            //
            // The base sits at 0.14 luminance rather than lower - see Obsidian's own note on why a
            // "black" material cannot sit at the bottom of its own ramp: any darker and Ramp's Dark
            // and Deep tones collapse to within a hundredth of the base and the fold lines in
            // CapeRows stop reading at all.
            var hoodColor = new Color(0.14f, 0.13f, 0.17f);
            var hoodPal = Palette.Of(new Palette.Ramp(hoodColor));
            // The cowl bakes its own outline ('o') - see DemoGear.Hood.cs for why it cannot use
            // the auto-outline any more.
            var hoodInk = Palette.Of(new Palette.Ramp(hoodColor));
            hoodInk['o'] = Palette.Outline;
            items.Add(Hood(Make("black_hood", "Hooded Cloak", GearSlot.Back, LootTier.Silver, 2f,
                Pixels(RigLayer.Back, "gear.back.hood.cape", CapeRows, hoodPal, 0f, 8f),
                // Placed in the head's own frame, where the cowl's field is drawn - its crown sits
                // where the hand-typed grid's did. Re-render against the composed head after any
                // change to it: the face it frames was measured, not derived.
                Pixels(RigLayer.Hood, "gear.back.hood.cowl", HoodRows, hoodInk, 0f, HoodY, ppu: BodyPpu,
                       outline: false)),
                hoodColor));

            // A poncho, not a cape - same cape+cowl construction as black_hood above (RigLayer.Back
            // + RigLayer.Hood, one ramp for both so it reads as one garment), but PonchoBackRows is
            // hip-length rather than ground-length: a poncho drapes over both shoulders and stops
            // short, it doesn't trail. A third layer, RigLayer.BackOver (see that layer's own doc),
            // carries a YOKE over the front: across BOTH shoulders, over whatever pauldrons are
            // worn there, and down to a point on the chest. Back alone can never do that (it sits
            // behind everything), and a poncho that only shows from behind isn't wrapping anything.
            // The first front piece was an 18-wide flap on the collar under the pauldrons - every
            // shoulder piece drew straight over the poncho that was meant to be covering it.
            //
            // ALL THREE LAYERS outline: false - a stroke around every step of a shape this jagged
            // (the cowl's crown steps and opening especially) reads as a lot of small hard lines
            // rather than a soft edge, which is wrong for cloth however the stroke is coloured -
            // tried first as a same-colour-family stroke (softer than black, still a drawn line)
            // and it wasn't enough; fully removing it is what actually reads as fabric.
            //
            // That cost the one thing the stroke was quietly doing for every OTHER Back-slot item:
            // padding the silhouette out by OutlinePadFor's thickness against whatever is behind
            // it, which is what kept the mane's own edges from showing past the hood/cape. Losing
            // it clipped hair at the temples and again lower down past the cape's sides. Fixed by
            // baking the same padding back in as ordinary FILL instead of a stroke - PonchoBackRows,
            // PonchoYokeRows and PonchoHoodRows (the same cowl as HoodRows, sampled unlined - see
            // DemoGear.Hood.cs) are each grown one texel into their own transparent neighbours,
            // which is the same geometry change StrokeOutline made, just invisible instead of
            // drawn in an accent colour.
            var wandererColor = new Color(0.42f, 0.28f, 0.16f);
            var wandererPal = Palette.Of(new Palette.Ramp(wandererColor));
            items.Add(Hood(Make("wanderers_hood", "Wanderer's Hood", GearSlot.Back, LootTier.Silver, 2f,
                // Top edge on the yoke's own (DrapeY), same width and shoulder curve - see
                // PonchoBackRows.
                Pixels(RigLayer.Back, "gear.back.poncho.back", PonchoBackRows, wandererPal,
                       0f, DrapeY(PonchoBackRows), ppu: BodyPpu,
                       outline: false),
                Pixels(RigLayer.Hood, "gear.back.poncho.cowl", PonchoHoodRows, wandererPal, 0f, HoodY, ppu: BodyPpu,
                       outline: false),
                // At BODY density and placed off the same chin anchor the pauldrons are, so the
                // yoke covers them by construction rather than by a tuned literal: its top edge
                // sits a cell over the chin (under the head anyway) and it is wide and deep enough
                // to swallow the largest resized pauldron plus its outline - see PonchoYokeRows.
                Pixels(RigLayer.BackOver, "gear.back.poncho.yoke", PonchoYokeRows, wandererPal,
                       0f, DrapeY(PonchoYokeRows), ppu: BodyPpu,
                       outline: false)),
                wandererColor));

            // Spawn's cape: floor-length, tattered, a spiked collar - DemoGear.Hellspawn.cs.
            items.Add(HellspawnCape());

            items.Add(Make("gold_amulet", "Gilded Amulet", GearSlot.Neck, LootTier.Gold, 0f,
                Pixels(RigLayer.Neck, "gear.neck.gold", AmuletRows, gemPal, 0f, 16f, ppu: FinePpu, upscale2x: true)));

            // A scarf rather than a pendant: TWO layers, not one. The collar paints RigLayer.Neck,
            // same as the amulet above, and sits still. The tail paints RigLayer.NeckBack -
            // behind the torso, offset toward one side - and is what HasFlowingScarf swings with
            // the cape's own spring; see AnimateScarf. Named by colour, on purpose - a tier is a
            // rarity, not a palette, and these four are the same silhouette wearing four
            // different dyes, not four different rarities.
            var scarletPal = Palette.Of(new Palette.Ramp(new Color(0.74f, 0.16f, 0.19f)));
            var azurePal   = Palette.Of(new Palette.Ramp(new Color(0.20f, 0.42f, 0.74f)));
            var verdantPal = Palette.Of(new Palette.Ramp(new Color(0.22f, 0.56f, 0.30f)));
            var amberPal   = Palette.Of(new Palette.Ramp(new Color(0.82f, 0.55f, 0.16f)));
            // Charcoal, a step above black_hood's own cloth so the cowl under the chin reads as a
            // second layer inside the hood - the Errant set's (DemoGear.Errant.cs).
            var ashenPal   = Palette.Of(new Palette.Ramp(new Color(0.27f, 0.27f, 0.30f), lift: 0.24f));

            items.Add(Scarf(Make("scarlet_scarf", "Scarlet Scarf", GearSlot.Neck, LootTier.Silver, 0f,
                Pixels(RigLayer.Neck, "gear.neck.scarlet", ScarfCollarRows, scarletPal, 0f, ScarfCollarY, ppu: BodyPpu),
                Pixels(RigLayer.NeckBack, "gear.neck.scarlet.tail", ScarfTailRowsData, scarletPal,
                       ScarfTailX, ScarfTailY, ppu: BodyPpu))));
            items.Add(Scarf(Make("azure_scarf", "Azure Scarf", GearSlot.Neck, LootTier.Silver, 0f,
                Pixels(RigLayer.Neck, "gear.neck.azure", ScarfCollarRows, azurePal, 0f, ScarfCollarY, ppu: BodyPpu),
                Pixels(RigLayer.NeckBack, "gear.neck.azure.tail", ScarfTailRowsData, azurePal,
                       ScarfTailX, ScarfTailY, ppu: BodyPpu))));
            items.Add(Scarf(Make("verdant_scarf", "Verdant Scarf", GearSlot.Neck, LootTier.Silver, 0f,
                Pixels(RigLayer.Neck, "gear.neck.verdant", ScarfCollarRows, verdantPal, 0f, ScarfCollarY, ppu: BodyPpu),
                Pixels(RigLayer.NeckBack, "gear.neck.verdant.tail", ScarfTailRowsData, verdantPal,
                       ScarfTailX, ScarfTailY, ppu: BodyPpu))));
            items.Add(Scarf(Make("amber_scarf", "Amber Scarf", GearSlot.Neck, LootTier.Silver, 0f,
                Pixels(RigLayer.Neck, "gear.neck.amber", ScarfCollarRows, amberPal, 0f, ScarfCollarY, ppu: BodyPpu),
                Pixels(RigLayer.NeckBack, "gear.neck.amber.tail", ScarfTailRowsData, amberPal,
                       ScarfTailX, ScarfTailY, ppu: BodyPpu))));
            items.Add(Scarf(Make("ashen_scarf", "Ashen Scarf", GearSlot.Neck, LootTier.Silver, 0f,
                Pixels(RigLayer.Neck, "gear.neck.ashen", ScarfCollarRows, ashenPal, 0f, ScarfCollarY, ppu: BodyPpu),
                Pixels(RigLayer.NeckBack, "gear.neck.ashen.tail", ScarfTailRowsData, ashenPal,
                       ScarfTailX, ScarfTailY, ppu: BodyPpu))));

            // A shroud over the shoulders, after the user's desert-ranger reference - see
            // DemoGear.WraithCloak.cs.
            items.Add(DrifterShroud());

            // Two texels on the index finger. Tiny on purpose - the brief asked for every slot to
            // be PRESENT, and a ring is the honest limit of what reads at this density.
            items.Add(Make("void_ring", "Void Signet", GearSlot.Ring, LootTier.BlackDiamond, 0f,
                Pixels(RigLayer.Ring, "gear.ring.void", RingRows, voidPal, 0f, -14f, ppu: FinePpu, upscale2x: true)));

            items.Add(Make("gold_greaves", "Gilded Greaves", GearSlot.Legs, LootTier.Gold, 5f,
                // ONE LAYER, not the usual front/back pair. RigLayers.Pairs exists because a boot
                // or greave on a single limb reads as a bug - but a tasset is one object spanning
                // both hips, so there is nothing to pair it with. The tall boot covers everything
                // below it, which is why this slot no longer paints a per-leg plate at all.
                Pixels(RigLayer.Tasset, "gear.legs.gold", TassetRows, goldPal, 0f, TassetY,
                       ppu: BodyPpu)));

            // The merged Trinket/Relic slot's default - cosmetic only, so the socket is never
            // blank before a real relic is found. Trinket itself is retired: no item declares
            // that slot any more, so DropStale self-heals any old save still pointing at it, the
            // same way it already handles any other reshuffled slot.
            items.Add(Make("leather_pouch", "Wayfarer's Pouch", GearSlot.Relic, LootTier.Silver, 0f,
                // THE STANDARD every worn relic is sized and hung against - see HipRelic.
                HipRelic("gear.trinket.leather", PouchRows, leatherPal)));

            // A relic below Black Diamond that still grants a finisher - a real pre-run playstyle
            // pick, changeable by a floor reward like any other unlocked slot rather than locked
            // the way a Black Diamond signature is.
            items.Add(RelicGrants(
                Make("relic_wanderer", "Wanderer's Charm", GearSlot.Relic, LootTier.Gold, 0f,
                    HipRelic("gear.trinket.charm", PouchRows, gemPal)),
                "cleave"));

            items.Add(Make("silver_boots", "Silver Boots", GearSlot.Boots, LootTier.Silver, 2f,
                Pixels(RigLayer.BootsFront, "gear.boots.silver", BootRows, silverPal, 0f, BootY, ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.silver.dark", BootRows, silverDrk, 0f, BootY, ppu: BodyPpu)));

            items.Add(Make("silver_belt", "Silver Belt", GearSlot.Belt, LootTier.Silver, 2f,
                Pixels(RigLayer.Belt, "gear.belt.silver", BeltRows, silverPal, 0f, BeltY, ppu: BodyPpu)));

            items.Add(Make("silver_gloves", "Silver Gauntlets", GearSlot.Gloves, LootTier.Silver, 2f,
                // ARM-LOCAL, not torso-local: GlovesFront/Back hang off the arm pivot, so 0 is the
                // shoulder and the limb runs to -Cells(ArmH). See ArmHarnessY.
                Pixels(RigLayer.GlovesFront, "gear.gloves.silver", GloveRows, silverPal, 0f, ArmHarnessY,
                       ppu: BodyPpu),
                Pixels(RigLayer.GlovesBack, "gear.gloves.silver.dark", GloveRows, silverDrk, 0f, ArmHarnessY,
                       ppu: BodyPpu)));

            // The Gilded Greatsword: a gold blade on an ANTIQUE-GOLD hilt - a solid gold
            // crossguard with sharp antique-gold ends, a braid up the blade from the guard, a twisted
            // rope grip and an arrowhead cap. See BuildGildedSword. Menu art hand-authored at true
            // MenuPpu, as Emberline's and the Silver Blade's are.
            items.Add(WithMenu(Make("gold_greatsword", "Gilded Greatsword", GearSlot.Weapon, LootTier.Gold, 14f,
                Pixels(RigLayer.Weapon, "gear.weapon.gold", GildedSwordRows, GildedSwordPal(), 0f, -10f,
                       pivotTexel: GreatswordGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.gold.menu", GildedSwordDetail, GildedSwordPal(), 0f, -10f,
                       pivotTexel: EmberDetailGrip, ppu: MenuPpu)));

            // The Gilded Greatsword's own silhouette, forged in IRON with a BRONZE cutting edge -
            // the entry-tier sword of the family. The edge is a separate metal, as if the blade
            // were tempered to strike with it: bronze runs down the lit edge from the point to the
            // ricasso (BronzeEdge) and the handle is cast in it; the flat, guard and pommel are iron. Same outline as the
            // gilded sword on purpose - this is the BASELINE the bespoke swords depart from, not a
            // design of its own. Power sits under Silver's 6, level with the Bronze Cuirass.
            //
            // The HANDLE is bronze too, the same metal as the edge: the grip is the second
            // material (K/B/S) and nothing else on the greatsword grid uses it, so bronze there
            // turns the whole handle bronze without touching the grid.
            var bronzeEdged = Palette.Of(Palette.Iron, Palette.Bronze, Palette.Bronze);
            items.Add(WithMenu(Make("bronze_greatsword", "Bronze Greatsword", GearSlot.Weapon, LootTier.Bronze, 3f,
                Pixels(RigLayer.Weapon, "gear.weapon.bronze", BronzeEdgedRows, bronzeEdged, 0f, -10f,
                       pivotTexel: GreatswordGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.bronze.menu", BronzeEdgedDetail, bronzeEdged, 0f, -10f,
                       pivotTexel: GreatswordDetailGrip, ppu: MenuPpu, upscale2x: true)));

            // A second option per slot so the loadout screen can cycle rather than just toggle.
            items.Add(Make("silver_helm", "Silver Helm", GearSlot.Head, LootTier.Silver, 3f,
                Pixels(RigLayer.HeadArmor, "gear.head.silver", HelmRows, silverPal, 0f, 12f, ppu: BodyPpu)));
            // Silver Plate is the default starting torso (see GameBootstrap's starter loadout) -
            // Dash pairs with it so a fresh character's one defensive option is the simplest of
            // the four to read: get out of the way.
            items.Add(Defends(Make("silver_plate", "Silver Plate", GearSlot.Torso, LootTier.Silver, 5f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.silver", CuirassRows, silverPal, 0f, 14f, ppu: Convergence.Art.Gear.PrimitiveCharacterRig.Proportions.BodyPpu)),
                DefensiveAbility.Dash));


            // The other two cuirasses, so all four DefensiveAbility values are carried by a
            // named, deliberately-picked piece instead of whatever RollDefensiveAbility's hash
            // happens to land on. That roll still stands for anything minted afterward (RunLoot,
            // the Forge) - it just shouldn't be the only reason a hand-authored catalog piece
            // ends up with the ability it has.
            items.Add(Defends(Make("bronze_plate", "Bronze Cuirass", GearSlot.Torso, LootTier.Bronze, 3f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.bronze", CuirassRows,
                       Palette.Of(Palette.Tier(LootTier.Bronze)), 0f, 14f,
                       ppu: Convergence.Art.Gear.PrimitiveCharacterRig.Proportions.BodyPpu)),
                // Cheapest cooldown of the four and no fallback beyond the shared parry window -
                // fits the floor tier's "skill, not tech" feel.
                DefensiveAbility.ParryStance));

            items.Add(Defends(Make("diamond_plate", "Prismatic Cuirass", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.diamond", CuirassRows,
                       Palette.Of(Palette.Tier(LootTier.Diamond)), 0f, 14f,
                       ppu: Convergence.Art.Gear.PrimitiveCharacterRig.Proportions.BodyPpu)),
                // A shimmering damage-reducing ward suits the cosmetic tier's prismatic look far
                // better than a hard directional block would.
                DefensiveAbility.Bulwark));

            // ---- Tide Ward Circlet: the one piece kept from a wider Water tabard set that
            // didn't work out (tabard, legguards, spiral gloves - all scrapped). Glass ramp built
            // the same way Prism's own panels are (a pale base lifted hard toward white, shade
            // kept moderate) so the gem reads as material rather than a flat colour wash.
            var waterGlass = new Palette.Ramp(
                Color.Lerp(ElementInfo.Tint(ElementType.Water), Color.white, 0.35f),
                lift: 0.55f, shade: 0.35f);
            var bandSteel = new Palette.Ramp(new Color(0.14f, 0.15f, 0.18f));
            var blackCloth = new Palette.Ramp(new Color(0.05f, 0.05f, 0.06f));

            // The tie reads as the same "black cloth" as the front band but can't share its
            // exact colour: the front band sits against skin/hair and blackCloth.Base (0.05
            // luminance) reads fine there, while the tie sits against RigLayer.HeadBack's own
            // near-black hair-outline tone and collapsed into it at that luminance - the same
            // floor Black Steel's own note already found (below ~0.24, Ramp's Dark/Deep tones
            // stop being distinguishable from a neighbouring near-black at all).
            var tieCloth = new Color(0.22f, 0.22f, 0.25f);
            items.Add(Tied(Make("water_headband", "Tide Ward Circlet", GearSlot.Head, LootTier.Silver, 3f,
                Pixels(RigLayer.HeadArmor, "gear.head.water",
                       HeadbandRows, Palette.Of(blackCloth, bandSteel, waterGlass),
                       0f, 6f, ppu: BodyPpu)),
                tieCloth));

            // ---- Black Steel: a diamond-tier cosmetic armour set, seven pieces ----
            //
            // Diamond and power 0 throughout - the same "purely cosmetic tier" rule Prismatic
            // Cuirass and Prismatic Cloak above already carry, not a downgrade invented for this
            // set. Black steel names a MATERIAL, not a tier-colour reuse (see "A TIER IS A
            // RARITY, NOT A PALETTE" in the weapon-class notes) - Diamond doesn't mean prismatic
            // here any more than it means gold on the greatsword pieces of the same tier.
            //
            // TWO-TONE, NOT GOLD-TRIMMED: a very dark gunmetal grey body with BLACK accents -
            // rivets, seams, rims - rather than the gilded-armour trim every other tier here
            // wears. Reworked from a first pass that reused the shared Helm/Pauldron/Cuirass/
            // Glove/Boot/Belt grids with a gold second material; that read as "a gold set
            // recoloured" rather than as its own silhouette, so every piece below is its own
            // grid now, not a shared one.
            //
            // Base sits at 0.24 luminance rather than lower, the same floor Obsidian's own note
            // and hoodPal both already use: any darker and Ramp's Dark/Deep tones collapse to
            // within a hundredth of the base, and every seam on a seven-piece set goes flat at
            // once rather than just one blade's fuller. 0.24 rather than 0.14 specifically
            // because it has to read as DARK GREY next to the accent ramp's near-black, not as
            // the same black the accent is - two tones need daylight between them.
            var blackSteel = new Palette.Ramp(new Color(0.24f, 0.24f, 0.27f));
            // The accent material - true near-black, for rivets, seams and rims. A second real
            // colour rather than just this ramp's own Dark/Deep, because those still read as
            // "shaded grey" at a glance and the reference's trim is unambiguously black. Only
            // ever painted in its own BASE tone below (never Light/Glow), since lightening a
            // near-black ramp toward white is exactly the wrong direction for "black accents".
            var blackSteelAccent = new Palette.Ramp(new Color(0.05f, 0.05f, 0.06f));
            var blackSteelPal = Palette.Of(blackSteel, blackSteelAccent);
            // The "Back" layers (the far shoulder/hand/foot, half-hidden behind the body) use a
            // single darker black-steel ramp with no accent colour - the same convention
            // goldDark and silverDrk already set for those layers on every other tiered set.
            var blackSteelDark = Palette.Of(new Palette.Ramp(Color.Lerp(blackSteel.Base, Color.black, 0.26f)));

            // OFFSET 9, NOT THE 12 EVERY OTHER HELM IN THIS FILE USES, and that is the fix rather
            // than a deviation. 12 centres a helm on the CROWN, which is all the others try to
            // cover - HelmRows is six rows tall and caps the skull. This one is worn ACROSS THE
            // FACE, so it is centred on the head instead: 20 padded rows at an offset of 9 span
            // y -1..+19, which is the head's own -1..+17 plus clearance above for the dome. See
            // BlackSteelHelmRows for the measured geometry that puts it there.
            items.Add(Make("blacksteel_helm", "Black Steel Helm", GearSlot.Head, LootTier.Diamond, 0f,
                Pixels(RigLayer.HeadArmor, "gear.head.blacksteel", BlackSteelHelmRows, blackSteelPal, 0f, 9f, ppu: BodyPpu)));

            // Bare-headed on purpose - no Layers at all, the same "no art" answer the Relic slot
            // gives for a piece whose whole point is an effect rather than a shape. A helm here
            // would cover the very eye the item exists to show.
            var wraithEye = Make("wraith_eye", "Wraith's Eye", GearSlot.Head, LootTier.Diamond, 0f);
            wraithEye.HasGlowingEye = true;
            items.Add(wraithEye);

            // The reference's most distinctive piece - a domed cap plate over a flared lower
            // skirt, the two separated by a full black accent seam rather than the thin gold
            // rivet-edge the first pass used, so the pauldron reads as two overlapping plates.
            items.Add(Make("blacksteel_pauldrons", "Black Steel Pauldrons", GearSlot.Shoulders, LootTier.Diamond, 0f,
                Pixels(RigLayer.Shoulders, "gear.pauldron.blacksteel", BlackSteelPauldronRows, blackSteelPal,
                       -ShoulderX, PlateY(BlackSteelPauldronRows), ppu: BodyPpu),
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.blacksteel.dark", BlackSteelPauldronRows, blackSteelDark,
                       ShoulderX, PlateY(BlackSteelPauldronRows), ppu: BodyPpu)));

            // A black keel line down the sternum plus two full-width accent seams marking the
            // fauld's articulation - the closest a 10-wide grid gets to the reference's centre
            // ridge and layered skirt without the lines dissolving into noise.
            //
            // Barrier, matching the reference: a heavy plate carried with a shield reads as
            // frontal protection more than the other three DefensiveAbility values would.
            items.Add(Defends(Make("blacksteel_plate", "Black Steel Cuirass", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.blacksteel", Reproportioned(BlackSteelCuirassRows), blackSteelPal,
                       0f, 10f, ppu: BodyPpu)),
                DefensiveAbility.Barrier));

            // A full arm harness on Silver's frame (GloveRows) - see BlackSteelHarnessRows. The
            // far arm keeps the black seams: blackSteelDark has no accent colour, and a harness
            // painted in it would draw every seam as a HOLE (an unmapped letter is transparent).
            var blackSteelFarArm = Palette.Of(new Palette.Ramp(Color.Lerp(blackSteel.Base, Color.black, 0.26f)),
                                              blackSteelAccent);
            items.Add(Make("blacksteel_gloves", "Black Steel Gauntlets", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesFront, "gear.gloves.blacksteel", BlackSteelHarnessRows, blackSteelPal,
                       0f, ArmHarnessY, ppu: BodyPpu),
                Pixels(RigLayer.GlovesBack, "gear.gloves.blacksteel.dark", BlackSteelHarnessRows, blackSteelFarArm,
                       0f, ArmHarnessY, ppu: BodyPpu)));

            items.Add(Make("blacksteel_belt", "Black Steel Belt", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.blacksteel", BlackSteelBeltRows, blackSteelPal, 0f, BeltY,
                       ppu: BodyPpu)));

            items.Add(Make("blacksteel_boots", "Black Steel Boots", GearSlot.Boots, LootTier.Diamond, 0f,
                Pixels(RigLayer.BootsFront, "gear.boots.blacksteel", PerLeg(BlackSteelBootRows), blackSteelPal,
                       0f, SoleY(PerLeg(BlackSteelBootRows)), ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.blacksteel.dark", PerLeg(BlackSteelBootRows), blackSteelDark,
                       0f, SoleY(PerLeg(BlackSteelBootRows)), ppu: BodyPpu)));

            // Legs - missing from the first pass entirely. Knee cop, shin plate, and the same
            // black accent-seam motif the pauldron and cuirass carry, so the set reads as one
            // language rather than six pieces that happen to share a palette.
            items.Add(Make("blacksteel_greaves", "Black Steel Greaves", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.LegsFront, "gear.legs.blacksteel", PerLeg(BlackSteelGreaveRows), blackSteelPal,
                       0f, -6f, ppu: BodyPpu),
                Pixels(RigLayer.LegsBack, "gear.legs.blacksteel.dark", PerLeg(BlackSteelGreaveRows), blackSteelDark,
                       0f, -6f, ppu: BodyPpu)));

            // ---- Conclave: a diamond-tier ceremonial set, five pieces (Torso/Gloves/Boots/Legs/
            // Head) ----
            //
            // Black lacquer and electrum throughout (see the gorget's own palette below), tied
            // together by the reference's own three-value split: near-black body, white hands and
            // feet, gold at the joints between them. Built piece by piece rather than planned as a
            // set up front - this header is retroactive, added once the fifth piece (the mask)
            // landed and the five stopped being "the gorget plus four things that match it".
            //
            // conclave_gorget   Torso    the crescent collar - see its own header immediately below
            // conclave_gauntlets  Gloves  white cuff, gold band; the reference's forearm bracer has
            //                             no slot of its own, so the glove's cuff carries its band
            // conclave_boots    Boots    white, calf-height, gold band at the top
            // conclave_leggings Legs     plain black lacquer - the fourth value the bare body's
            //                             default brown trousers were otherwise adding
            // conclave_mask     Head     half mask, nose down; CoversFaceOnly so hair still shows
            //
            // All five Diamond/power 0/cosmetic, matching every other Diamond piece in this file.
            // No relic, no signature finisher - Conclave is regalia, not a combat set.
            //
            // ---- Conclave Gorget ----
            //
            // A CRESCENT, not a dish - a constant-width band whose centreline arcs up and outward,
            // so the horns finish beside the cheeks rather than below the shoulders. That is a
            // revision, and the first version is worth keeping on the record because the two shapes
            // are easy to confuse in words and read nothing alike on a body: it was a bowl hung
            // under the neck, top edge peaking at the centre and FALLING outward to blunt low tips.
            // Same width, same materials, same layer - and it read as a wide collar, where this
            // reads as a piece of ceremonial armour standing up around the head.
            //
            // Three properties carry it, none of them ornament:
            //
            //   1. THE BAND IS A CONSTANT SIX LAMES THICK everywhere - centre, shoulder and horn
            //      alike. That is what makes it an annulus sector instead of a shape that happens
            //      to be curved, and it is why the lames can be authored as a flat ramp (see
            //      ConclaveGorgetRows) rather than needing a per-column depth fraction.
            //   2. THE INNER EDGE CRADLES THE HEAD. It passes just under the chin at the centre
            //      (y -1) and climbs to +8 at the horns, which is cheek height - the head sits IN
            //      the crescent rather than on top of it.
            //   3. THE HORNS ARE THE HIGHEST POINTS AND THE WIDEST, both at once. They are the
            //      whole silhouette read; everything below is mass supporting them.
            //
            // 52 x 20 texels, y +8 down to -12 - 0.720 x 0.293 world units once the auto-outline
            // has added its texel a side. MEASURED against what was already here rather than
            // picked: the head, the widest part of the bare body, is 0.427; the Vanguard cuirass
            // was the widest gear in the file at 0.560 and the Wraithguard cloak the widest Back
            // piece at 0.453. So this is 1.69x head width and 29% past the previous widest thing
            // anyone wears. It is the widest gear
            // grid in the project, deliberately - "oversized" is the brief, and at this density a
            // piece only a little past the body outline just reads as a slightly bigger cuirass.
            //
            // THE HEAD DRAWS OVER THE CRESCENT, AND THAT IS THE NECK. The chibi body has no neck
            // (see PrimitiveCharacterRig's proportions block), so there is nothing to draw one on.
            // Measured against the head's own taper, the crescent clears it exactly where it has
            // to: at y 0..+2 the band starts at x 6 against a +/-8 jaw, so it emerges right at the
            // jawline; by y +6..+8 it starts at x 18 against a +/-16 skull, so the horn tips stand
            // free beside the head with daylight between. Nothing is drawn behind the centre of the
            // head at all - the crescent simply stops under the chin, and the head filling that
            // gap IS the neck.
            //
            // The overhang is what needed RigLayer.TorsoOver - see that layer's own doc for the
            // measurement and for what the alternatives cost. The crescent needs it MORE than the
            // dish did: the horns sit at x 18..26, entirely past the arms at x 8..16, so on
            // TorsoArmor each arm would cut the band clean through and leave a horn floating.
            //
            // LAMES ARE TONE STEPS, NEVER GAPS. StrokeOutline erodes a texel off every side of a
            // transparent gap bordering opaque art, so any incised line under ~5 texels closes up
            // completely - measured on the disc's grip hollow, the Rift Blade's tear and Sniper's
            // prong gap before this.
            //
            // AN ELECTRUM PLATE WITH ONE THIN LACQUER TRIM, and it took three passes to land
            // there. All three are recorded because each one was cut for a different reason and
            // the reasons are what generalise, not the answer:
            //
            //   BLACK PLATE, GOLD RIM ON BOTH EDGES. Cut. At two texels a lame, a pale line down
            //   each side of a curving band reads as PIPING - gold braid on a costume, or worse,
            //   as chevrons on the horns.
            //
            //   BLACK PLATE, GOLD RIM ON THE OUTER EDGE ONLY. Cut. A bright line on the
            //   silhouette's OUTSIDE competes with the horns for the read; the eye follows the
            //   gold instead of the shape.
            //
            //   BLACK PLATE, GOLD RIM ON THE INNER EDGE. Better - it frames the face and lets the
            //   horns finish dark against the room - but still a dark piece with a highlight.
            //
            // Inverted, the materials swap roles: electrum IS the plate, and one lacquer lame on
            // the inner edge is the trim, separating the metal from the face. What carried over
            // unchanged is the RULE both earlier passes established - one thin line, on the inner
            // edge - it is just made of the other material now.
            //
            // Black lacquer and electrum, named as MATERIALS - Diamond here is a rarity, not a
            // palette, the same rule Black Steel above states at length. Power 0 and cosmetic,
            // matching every other Diamond piece in this file.
            var conclaveLacquer = new Palette.Ramp(new Color(0.17f, 0.16f, 0.20f));
            // Electrum - a pale gold, warmer than silver and cooler than Vanguard's brass
            // (0.80, 0.62, 0.22), which it has to stay clear of so the two gilded sets read as
            // different metals rather than the same trim at different sizes.
            //
            // IT WAS PALER STILL (0.78, 0.74, 0.55) while it was only a rim, and that had to
            // change when it became the plate. A two-texel line reads as "bright metal" on almost
            // any hue - there is not enough of it to have a colour - but the same swatch spread
            // over a 52-wide crescent stopped being metal and read as KHAKI, closer to canvas
            // than to plate. Saturation is what a large area needs and a thin line does not.
            var conclaveElectrum = new Palette.Ramp(new Color(0.80f, 0.67f, 0.36f));
            var conclavePal = Palette.Of(conclaveLacquer, conclaveElectrum);

            // The set's extremities are WHITE, not lacquer - straight off the reference, and the
            // reason it works is that the reference is a three-value costume: near-black body,
            // white hands and feet, gold at every joint between them. Painting the gloves and
            // boots in the same lacquer as the gorget would collapse two of those three values
            // and the figure would read as one dark mass with a gold collar floating on it.
            //
            // 0.84 rather than pure white, and the same reasoning blackSteel's 0.24 floor states
            // from the other end: Ramp lerps Light and Glow TOWARD white, so a base already at
            // 1.0 has nowhere to put them and the top three tones collapse into one flat block.
            // At 0.84 the ramp still resolves (Light 0.89, Glow 0.94) and reads as white cloth
            // with shape in it rather than as a cut-out.
            var conclaveWhite = new Palette.Ramp(new Color(0.84f, 0.84f, 0.87f));
            var conclaveWhitePal = Palette.Of(conclaveWhite, conclaveElectrum);
            // The far-side convention every tiered set in this file already uses (goldDark,
            // silverDrk, blackSteelDark): one darker ramp, no second material. The far glove and
            // boot lose their gold band entirely, which is correct - that side is half-hidden
            // behind the body and a trim line on it reads as noise, not as symmetry.
            //
            // 0.12, NOT THE 0.26 EVERY OTHER SET USES. That figure was tuned against mid-dark
            // metals, where a quarter step toward black is a shade; applied to a 0.84 white it
            // lands at 0.62, which is not "white in shadow" but GREY - the far boot read as a
            // different MATERIAL from the near one, not the same one further away. The darkening
            // has to be a fraction of the headroom the ramp actually has, and a near-white ramp
            // has very little. Still deliberately non-zero: two white boots at identical value
            // merge into one blob at this size, and the far side existing as its own shape is
            // the whole reason these Back layers are painted separately at all.
            var conclaveWhiteDark = Palette.Of(new Palette.Ramp(Color.Lerp(conclaveWhite.Base, Color.black, 0.12f)));
            // The far LEG's lacquer. 0.26 is safe here where it was not on the white: lacquer sits
            // at 0.17, so a quarter step toward black is a shade on an already-dark ramp rather
            // than a change of material - the same figure blackSteelDark uses on a 0.24 base.
            var conclaveLacquerDark = Palette.Of(new Palette.Ramp(Color.Lerp(conclaveLacquer.Base, Color.black, 0.26f)));

            items.Add(Defends(Make("conclave_gorget", "Conclave Gorget", GearSlot.Torso, LootTier.Diamond, 0f,
                // The waist band goes on first (TorsoArmor, the ordinary cuirass layer) and the
                // yoke over it - the band exists only because the yoke stops short of the waist,
                // which would otherwise leave bare cloth under a ceremonial collar. Since the yoke
                // halved with the head, the band (on the torso's Reproportioned rule) runs from
                // the waist up to exactly the yoke's lower edge.
                Pixels(RigLayer.TorsoArmor, "gear.torso.conclave.band", Reproportioned(ConclaveBandRows), conclavePal,
                       0f, 7f, ppu: BodyPpu),
                Pixels(RigLayer.TorsoOver, "gear.torso.conclave", ConclaveGorgetRows, conclavePal,
                       0f, ConclaveGorgetY, ppu: BodyPpu)),
                // Parry Stance rather than Barrier or Bulwark (both already spoken for by the other
                // two Diamond cuirasses): a rigid collar standing off the body deflects rather
                // than absorbs, which is the one of the four that describes this shape.
                DefensiveAbility.ParryStance));

            // A FULL ARM HARNESS on Silver's frame. It was a glove alone, carrying the gold as a
            // cuff band because on the arm the white glove meets a separate gold bracer and there
            // was no bracer to put it on. The harness has room for the reference's bracer: black lacquer upper arm (the body), a GOLD bracer down the
            // forearm (the joint), the white glove (the hand) - the costume's own three values in
            // order, so the glove no longer carries the gold as a cuff. See ConclaveHarnessRows.
            // Three materials, so the digits carry the lacquer; the far arm darkens each of them
            // (white least - see conclaveWhiteDark) rather than dropping any to a hole.
            var conclaveArm = Palette.Of(conclaveWhite, conclaveElectrum, conclaveLacquer);
            var conclaveFarArm = Palette.Of(new Palette.Ramp(Color.Lerp(conclaveWhite.Base, Color.black, 0.12f)),
                                            new Palette.Ramp(Color.Lerp(conclaveElectrum.Base, Color.black, 0.20f)),
                                            new Palette.Ramp(Color.Lerp(conclaveLacquer.Base, Color.black, 0.26f)));
            items.Add(Make("conclave_gauntlets", "Conclave Gauntlets", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesFront, "gear.gloves.conclave", ConclaveHarnessRows, conclaveArm,
                       0f, ArmHarnessY, ppu: BodyPpu),
                Pixels(RigLayer.GlovesBack, "gear.gloves.conclave.dark", ConclaveHarnessRows, conclaveFarArm,
                       0f, ArmHarnessY, ppu: BodyPpu)));

            // Calf-height, which is why the offset is -12 where every other boot in this file is
            // -14: the grid is 12 texels rather than 8 or 10, and the anchor is what keeps the
            // SOLE on the floor while the extra height goes up the leg instead of down through it.
            // Spans root -36..-24 against a leg of -34..-18, so it laps two texels under the foot
            // (the sole, exactly as BootRows does) and leaves six texels of leg showing above it.
            //
            // A first pass went a row taller and it was wrong: at -36..-22 the boot covered
            // fourteen of the leg's sixteen texels, the legs stopped existing as a shape, and the
            // gold band ended up level with the belt where the torso piece crowds it. The
            // reference has black leg visible between the boot cuff and the knee guard, and that
            // gap is what keeps the white from reading as one continuous block from foot to hip.
            items.Add(Make("conclave_boots", "Conclave Boots", GearSlot.Boots, LootTier.Diamond, 0f,
                Pixels(RigLayer.BootsFront, "gear.boots.conclave", PerLeg(ConclaveBootRows), conclaveWhitePal,
                       0f, SoleY(PerLeg(ConclaveBootRows)), ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.conclave.dark", PerLeg(ConclaveBootRows), conclaveWhiteDark,
                       0f, SoleY(PerLeg(ConclaveBootRows)), ppu: BodyPpu)));

            // BLACK LEGS, and the reason is the reference's own three-value split: near-black body,
            // white hands and feet, gold at the joints. The bare body's default trousers are brown,
            // which is a FOURTH value sitting between the white boots and the dark torso, and it
            // was the one thing still breaking the costume up.
            //
            // Spans root -28..-16 against a leg of -34..-18, so it starts two texels into the hip
            // (meeting the waist plate rather than leaving a seam at it) and ends four texels below
            // the boot cuff at -24. That overlap is deliberate: the boot laps OVER the trouser the
            // way a boot actually does, and it means neither piece has to land on an exact texel
            // for the leg to read as continuously covered.
            items.Add(Make("conclave_leggings", "Conclave Leggings", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.LegsFront, "gear.legs.conclave", PerLeg(ConclaveLegRows), conclavePal,
                       0f, -4f, ppu: BodyPpu),
                Pixels(RigLayer.LegsBack, "gear.legs.conclave.dark", PerLeg(ConclaveLegRows), conclaveLacquerDark,
                       0f, -4f, ppu: BodyPpu)));

            // A MASK, NOT A HELM - it covers the face and stops below the fringe, so the character's
            // own hairstyle still sits on top of it. That distinction is not something the rig can
            // see in the art: SyncHeadCoverage derives "the head is covered" from HeadArmor simply
            // HAVING a sprite, which is correct for every helm in this file and wrong here, because
            // the mane then gets blanked at a hairline this piece does not have. CoversFaceOnly is
            // the item saying so outright - see GearItem's own note on the flag.
            //
            // Measured against the composed head rather than eyeballed. The head sprite runs
            // y -2..+34; hair occupies its rows 4..19, the eyes sit at rows 20..23, and the jaw
            // runs to row 35. The grid below is rows 24..35 - from the bridge of the nose down -
            // which puts its top edge at y +10 and its chin at y -2, hence the offset of 2.
            var conclaveMask = Make("conclave_mask", "Conclave Mask", GearSlot.Head, LootTier.Diamond, 0f,
                Pixels(RigLayer.HeadArmor, "gear.head.conclave", ConclaveMaskRows, conclavePal, 0f, 1f, ppu: BodyPpu));
            conclaveMask.CoversFaceOnly = true;
            items.Add(conclaveMask);

            // ---- Seraph Mantle: a winged gorget with an eye medallion at the crux ----
            //
            // Built from a sketch, and the sketch's own instruction was that it IGNORES THE BODY
            // SILHOUETTE - the wings jet out past the shoulders rather than being tucked inside
            // the torso's 24 texels the way every cuirass in this file is. That makes it the
            // second item to need RigLayer.TorsoOver (see that layer's own doc): at +/-28 it is
            // wider than the gorget, so the arms would otherwise punch a limb-shaped bite out of
            // each wing and leave the tips reading as two detached blades floating in mid-air.
            //
            // Three materials, named as materials rather than tiers: pale steel (white at its top
            // tone, silver at its base - one ramp, two jobs), gilt for the fangs and the disc, and
            // ivory for the tablet the eye is set into. Diamond tier at power 0, the same purely-cosmetic rule Prismatic,
            // Black Steel and Conclave all carry.
            //
            // The ivory ramp takes line: 0.92 rather than the default 0.68 - the PUPIL is that
            // ramp's Line tone, and lerping a 0.97 swatch only 0.68 of the way to near-black
            // lands at ~0.34 grey, which reads as a hole in the tablet rather than as an eye.
            // lift 0.50, not the default 0.34. This ramp does two jobs at once - its BASE is the set's
            // silver and its GLOW is the set's white - and at the default lift the glow landed on
            // 0.85 grey, which reads as pale steel beside the gilt rather than as white cloth. The
            // base is untouched, so the silver the spikes and the medallion's triangle are painted
            // in has not moved; only the top of the ramp has.
            var seraphSteel = new Palette.Ramp(new Color(0.60f, 0.62f, 0.68f), lift: 0.50f);
            var seraphGilt  = new Palette.Ramp(new Color(0.84f, 0.63f, 0.40f));
            var seraphIvory = new Palette.Ramp(new Color(0.97f, 0.92f, 0.72f), line: 0.92f);
            var seraphPal   = Palette.Of(seraphSteel, seraphGilt, seraphIvory);
            // The far leg / far foot, half hidden behind the near one - the same single darker
            // ramp convention goldDark, silverDrk and blackSteelDark already set for Back layers.
            // Only the steel darkens: the gilt is a trim, and darkening a trim on the far side
            // makes the two legs look like they are wearing different items.
            var seraphDarkPal = Palette.Of(new Palette.Ramp(Color.Lerp(seraphSteel.Base, Color.black, 0.26f), lift: 0.50f),
                                           seraphGilt, seraphIvory);

            // ONE LAYER, no under-plate. conclave_gorget pairs its yoke with a waist band because a
            // collar over bare cloth looks unfinished, and the same band was built here and cut:
            // the sketch is a mantle and nothing else, and a plate invented to fill the gap under
            // it is exactly the kind of addition that buries a clean drawing.
            //
            // Hung from the CHIN (SeraphMantleY) at half its old size, because it is drawn against
            // the head and the head halved - see the grid's own note on why the head decides it.
            items.Add(Defends(Make("seraph_mantle", "Seraph Mantle", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoOver, "gear.torso.seraph", SeraphMantleRows, seraphPal,
                       0f, SeraphMantleY, ppu: BodyPpu)),
                // Dash - the one DefensiveAbility no Diamond torso had yet, and the one a pair of
                // wings describes. Barrier, Bulwark and Parry Stance are spoken for by Black Steel,
                // Prismatic and Conclave respectively.
                DefensiveAbility.Dash));

            // The legwear the set was missing - built from a paint-over of the rendered character
            // rather than from the original sketch, which stopped at the chest. White thigh, a
            // black band at the knee, gilt shin, white boot: the mantle's own three materials in
            // the same order it wears them, so the costume reads as one set rather than as a
            // chest piece with separate trousers.
            //
            // No Shoulders piece, deliberately - the mantle already occupies both shoulders and
            // then some, and a pauldron under a wing this wide has nowhere to be seen.
            items.Add(Make("seraph_leggings", "Seraph Leggings", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.LegsFront, "gear.legs.seraph", SeraphLegBodyRows, seraphPal,
                       0f, HipY(SeraphLegBodyRows), ppu: BodyPpu),
                Pixels(RigLayer.LegsBack, "gear.legs.seraph.dark", SeraphLegBodyRows, seraphDarkPal,
                       0f, HipY(SeraphLegBodyRows), ppu: BodyPpu)));

            // A domed white helmet with a black visor in a gilt frame, built against a White
            // Ranger reference. 36 x 40 at offset 16, spanning root y -2..+38 over a head that
            // runs -1..+34 and is 32 wide.
            //
            // FOUR ROWS TALLER than BlackSteelHelmRows' own footprint, which is what every other
            // helm here is built to, and the extra rows are all at the BOTTOM. At 36 rows the grid
            // bottoms out at +2 while the head keeps going to -1, and the jaw showed as bare skin
            // under the chin. A sealed helmet has to reach PAST the head it encloses, not to it.
            //
            // A FULL helm, not a mask: CoversFaceOnly stays unset, so SyncHeadCoverage blanks the
            // mane behind it. The reference has no hair showing and a visor that covers the eyes
            // outright, which is the case that flag exists to distinguish itself FROM.
            var seraphHelm = Make("seraph_helm", "Seraph Helm", GearSlot.Head, LootTier.Diamond, 0f,
                Pixels(RigLayer.HeadArmor, "gear.head.seraph", SeraphHelmRows, seraphPal, 0f, 8f, ppu: BodyPpu));
            // Fully enclosed: no hair escapes anywhere. The ordinary helm rule blanks the mane
            // above the hairline and leaves the length below it, which is right for a great-helm
            // and wrong for one that wraps the jaw - see GearItem.SealsHead.
            seraphHelm.SealsHead = true;
            items.Add(seraphHelm);

            // The gloves and the boots are deliberate SIBLINGS - identical black cuff, identical
            // white body, differing only in how they finish (the glove rounds in at the fingers,
            // the boot carries an extra pair of rows and rounds at the sole). Two extremities of
            // one costume, the same call the Conclave gauntlet and boot already make, rather than
            // two pieces that happen to share a palette.
            //
            // A full arm harness now, on Silver's frame - the LEG's order on the arm: white upper
            // arm, the black band at the elbow as at the knee, a gilt forearm as the shin, then the
            // glove's own black cuff and white hand. See SeraphHarnessRows.
            items.Add(Make("seraph_gloves", "Seraph Gauntlets", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesFront, "gear.gloves.seraph", SeraphHarnessRows, seraphPal,
                       0f, ArmHarnessY, ppu: BodyPpu),
                Pixels(RigLayer.GlovesBack, "gear.gloves.seraph.dark", SeraphHarnessRows, seraphDarkPal,
                       0f, ArmHarnessY, ppu: BodyPpu)));

            items.Add(Make("seraph_boots", "Seraph Boots", GearSlot.Boots, LootTier.Diamond, 0f,
                Pixels(RigLayer.BootsFront, "gear.boots.seraph", PerLeg(SeraphBootRows), seraphPal,
                       0f, SoleY(PerLeg(SeraphBootRows)), ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.seraph.dark", PerLeg(SeraphBootRows), seraphDarkPal,
                       0f, SoleY(PerLeg(SeraphBootRows)), ppu: BodyPpu)));

            // A brown belt with a gilt buckle and a pale blue ring set into it. Its own palette,
            // not the set's: LEATHER is a material this costume does not otherwise wear, and the
            // blue is the one cool note in a set that is otherwise white, silver and gold - the
            // point of a single accent is that nothing else shares it.
            //
            // Palette.Of takes three ramps and this uses all three, so the belt's white is simply
            // absent - there is none on it, and the strap's own brown is the lowercase ramp.
            var seraphLeather = new Palette.Ramp(Palette.Leather.Base);
            var seraphBlue    = new Palette.Ramp(new Color(0.55f, 0.80f, 0.95f));
            items.Add(Make("seraph_belt", "Seraph Belt", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.seraph", SeraphBeltRows,
                       Palette.Of(seraphLeather, seraphGilt, seraphBlue), 0f, SeraphBeltY, ppu: BodyPpu)));


            // ---- Vanguard: a gold-tier ornate set, four pieces ----
            //
            // Built against a reference: horned great-helm, layered spiked pauldrons, an ornate
            // gilt-trimmed cuirass, a crimson sash hanging from the waist. Deliberately the
            // opposite move from Black Steel above - that set is two-tone and reads as PLAIN,
            // tactical steel; this one spends a third material (gold trim) and a fourth (bone
            // horns, crimson cloth) specifically to carry more silhouette and more colour
            // information at the same 75 ppu density everything else here uses. The lesson from
            // "Gear must break the silhouette" is the whole design: horns extend the helm's own
            // outline upward past the skull, the pauldron's crown spike does the same past the
            // shoulder, and the cuirass's flared fauld plates do it at the waist - none of that
            // costs a single extra texel of density, it is geometry, not shading.
            //
            // Named as MATERIALS (steel, gold trim, bone, crimson cloth), never as the tier - the
            // standing rule ("A TIER IS A RARITY, NOT A PALETTE") this file states repeatedly.
            // Gold tier here is a coincidence of rarity, not the reason it is gold-coloured.
            var vanguardSteel = new Palette.Ramp(new Color(0.20f, 0.21f, 0.27f));
            var vanguardGold  = new Palette.Ramp(new Color(0.80f, 0.62f, 0.22f));
            var vanguardBone  = new Palette.Ramp(new Color(0.86f, 0.80f, 0.66f));
            var vanguardCrimson = new Palette.Ramp(new Color(0.55f, 0.10f, 0.14f));
            var vanguardPal3  = Palette.Of(vanguardSteel, vanguardGold, vanguardBone);
            var vanguardPal2  = Palette.Of(vanguardSteel, vanguardGold);
            var vanguardCloth = Palette.Of(vanguardCrimson);
            // The far-shoulder / dark-side convention every tiered pauldron in this file already
            // uses (goldDark, silverDrk, blackSteelDark) - a single darker ramp, no second
            // material, since the far side is half-hidden and doesn't need the trim to read.
            var vanguardDark  = Palette.Of(new Palette.Ramp(Color.Lerp(vanguardSteel.Base, Color.black, 0.28f)));

            items.Add(Make("vanguard_helm", "Vanguard Helm", GearSlot.Head, LootTier.Gold, 7f,
                // 12, matching every other crown-mounted helm in this file - the horns are the
                // extra rows ABOVE the dome, so the dome itself still caps the skull at the same
                // anchor gold_helm and blacksteel_helm both use.
                Pixels(RigLayer.HeadArmor, "gear.head.vanguard", VanguardHelmRows, vanguardPal3, 0f, 12f, ppu: BodyPpu)));

            items.Add(Make("vanguard_pauldrons", "Vanguard Pauldrons", GearSlot.Shoulders, LootTier.Gold, 6f,
                Pixels(RigLayer.Shoulders, "gear.pauldron.vanguard", VanguardPauldronRows, vanguardPal2,
                       -ShoulderX, PlateY(VanguardPauldronRows), ppu: BodyPpu),
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.vanguard.dark", VanguardPauldronRows, vanguardDark,
                       ShoulderX, PlateY(VanguardPauldronRows), ppu: BodyPpu)));

            items.Add(Defends(Make("vanguard_plate", "Vanguard Cuirass", GearSlot.Torso, LootTier.Gold, 11f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.vanguard", Reproportioned(Undoubled(VanguardCuirassRows)), vanguardPal2,
                       0f, 12f, ppu: BodyPpu)),
                DefensiveAbility.Barrier));

            // The crimson sash - RigLayer.Belt, which already sits OVER the cuirass (see the
            // layer's own doc: "a belt is cinched on top of body armour"). That is exactly where
            // the reference's cloth panel hangs, so no new layer is needed for it: the fauld's
            // own flared plates on VanguardCuirassRows frame a gap, and the sash fills it.
            items.Add(Make("vanguard_sash", "Vanguard Sash", GearSlot.Belt, LootTier.Gold, 3f,
                Pixels(RigLayer.Belt, "gear.belt.vanguard", VanguardTassetRows, vanguardCloth, 0f, VanguardSashY,
                       ppu: BodyPpu)));

            // ---- Wraithguard: a hooded, single-shouldered gold-tier set (Head/Shoulders/Torso/Back) ----
            //
            // Built against two references: a hooded knight in dark plate with one shoulder bared
            // by an asymmetric cloak drape, and a masked, skeletal-faced hood-and-armour figure
            // with the same asymmetry and cloth trailing from the waist. The set keeps the
            // asymmetry and the helm-under-hood layering; it does not keep either reference's
            // near-black palette - here the plate is a cool tempered silver and the cloak (with its
            // shoulder drape) and cuirass sash are all one deep crimson cloth, so the same silhouette reads
            // as a different character rather than as a recolour of either drawing.
            //
            // Named as MATERIALS, per the standing rule - gold tier here is a coincidence of
            // rarity, not the reason it is silver-and-red. Only two materials, deliberately: unlike
            // Vanguard's steel/gold/bone, the whole point of this look is exactly two colours
            // meeting at one asymmetric seam, and a third would blur which shoulder is which.
            //
            // Per the current design position that armour need not extend past the body's own
            // outline to read (an explicit change from "Gear must break the silhouette" above):
            // every piece here stays close to the body it sits on rather than flaring past it. The
            // read comes from the ASYMMETRY - one bare, plated shoulder against one cloth-draped
            // one - not from size.
            var wraithSteel = new Palette.Ramp(new Color(0.68f, 0.72f, 0.78f));
            var wraithCrimson = new Palette.Ramp(new Color(0.38f, 0.06f, 0.09f));
            var wraithPal = Palette.Of(wraithSteel);
            var wraithCuirassPal = Palette.Of(wraithSteel, wraithCrimson);
            var wraithCloth = Palette.Of(wraithCrimson);
            // Far-shoulder convention every tiered pauldron here uses: one darker ramp, no trim.
            var wraithDark = Palette.Of(new Palette.Ramp(Color.Lerp(wraithSteel.Base, Color.black, 0.28f)));

            items.Add(Make("wraith_helm", "Wraithguard Helm", GearSlot.Head, LootTier.Gold, 7f,
                // A full face plate rather than a dome (HelmRows and VanguardHelmRows both cap only
                // the skull): both references show a masked face under the hood, not a bare jaw, so
                // this one runs an eye-slit band and tapers all the way to a chin point. Offset 9,
                // not the 12 every dome helm here uses - the same reasoning BlackSteelHelmRows
                // states for its own face-covering helm: 12 centres on the crown, and a piece worn
                // ACROSS the face has to centre on the head instead.
                Pixels(RigLayer.HeadArmor, "gear.head.wraith", WraithHelmRows, wraithPal, 0f, 7f, ppu: BodyPpu)));

            var wraithPauldron = Make("wraith_pauldron", "Wraithguard Pauldron", GearSlot.Shoulders, LootTier.Gold, 5f,
                // Two different SHAPES, one material: a steel cap on +X and the rounded,
                // ragged-hemmed mantle shape on -X, on the far-shoulder convention's darker steel.
                //
                // The mantle used to be crimson CLOTH - the set's asymmetry carried by the
                // pauldron. That was wrong: the crimson belongs to the CLOAK, which now drapes it
                // over the -X shoulder of whatever pauldron is worn (see wraith_cloak), so wearing
                // this piece without the cloak shows plate on both shoulders, and wearing the cloak
                // over any other set still reads as the Wraithguard's bared shoulder.
                Pixels(RigLayer.Shoulders, "gear.pauldron.wraith", WraithPauldronRows, wraithPal,
                       ShoulderX, PlateY(WraithPauldronRows), ppu: BodyPpu),
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.wraith.mantle", WraithMantleRows, wraithDark,
                       -ShoulderX, PlateY(WraithMantleRows), ppu: BodyPpu));
            // Two different shapes, so they keep to their own shoulders turned away, as the
            // cloak's drape does (GearItem.Lopsided) - otherwise they swapped arms under it.
            wraithPauldron.Lopsided = true;
            items.Add(wraithPauldron);

            items.Add(Defends(Make("wraith_cuirass", "Wraithguard Cuirass", GearSlot.Torso, LootTier.Gold, 11f,
                // CuirassRows' own shape, plate for plate, with one change: a crimson sash band
                // replaces the shared dark fauld seam, so the "fabric flowing from the belt" both
                // references show reads through the torso piece itself, without needing a Belt-slot
                // item of its own.
                Pixels(RigLayer.TorsoArmor, "gear.torso.wraith", Reproportioned(WraithCuirassRows), wraithCuirassPal,
                       0f, 10f, ppu: BodyPpu)),
                // A duelist's block rather than a heavy tank's - fits a single-plated, hooded
                // swordsman better than Barrier's frontal-block read.
                DefensiveAbility.ParryStance));

            // 'o' is the outline's own ink, for the lines the cowl and the drape's back view draw by
            // hand (see DemoGear.Hood.cs and DemoGear.WraithCloak.cs).
            var wraithClothInk = Palette.Of(wraithCrimson);
            wraithClothInk['o'] = Palette.Outline;
            // A wrapped mantle over one shoulder and a long torn cape, after the user's
            // hooded-knight reference - see DemoGear.WraithCloak.cs.
            items.Add(WraithCloak(wraithCloth, wraithClothInk, wraithCrimson.Base));

            // The Silver Blade: bright steel on a GUNMETAL cross hilt - an X-shaped guard, every
            // arm ending in an arrowhead, a blue gem in the boss, a gunmetal cone pommel, and a
            // diamond flare where the blade meets the guard. See BuildSilverSword. Menu art
            // is hand-authored at true MenuPpu, the same way Emberline's is.
            items.Add(WithMenu(Make("silver_blade", "Silver Blade", GearSlot.Weapon, LootTier.Silver, 6f,
                Pixels(RigLayer.Weapon, "gear.weapon.silver", SilverSwordRows, SilverSwordPal(), 0f, -10f,
                       pivotTexel: SilverGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.silver.menu", SilverSwordDetail, SilverSwordPal(), 0f, -10f,
                       pivotTexel: SilverDetailGrip, ppu: MenuPpu)));

            // ---- Emberline: the first black diamond weapon ----
            //
            // It is what the tier is FOR, and the first thing to actually use the mechanism: art
            // that belongs to no other item, and a locked finisher nothing else can roll. Five
            // sprites, one per heat stage, held on the item so the rig can swap between them
            // without going through Apply - the blade's colour is the ONLY readout of the cycle,
            // so the art is load bearing rather than decorative.
            //
            // POWER MATCHES THE GILDED GREATSWORD, and that is the whole reason it is not 0.
            //
            // The "diamond tiers carry no power" rule was written for genuinely cosmetic items -
            // a cloak, a signet - and it does not survive contact with a weapon whose value is
            // mechanical. At 0 power Emberline was unequippable in practice: you would wield a
            // gold sword for its stat line and skin this over it, except a skin grants nothing
            // (Appearance.Resolve is art-only, deliberately), so the item was dead in both hands.
            //
            // Matching gold rather than beating it keeps it a LATERAL choice: no stat penalty for
            // wielding it, no stat advantage either, and the exclusive finisher is what the tier
            // actually buys. Beating gold would make black diamond strictly best on stats AND
            // effects, which is a different game.
            //
            // Its menu art is HAND-AUTHORED (EmberlineDetail), not derived - it is what an NFT
            // image of the item is rendered from. Drawn at the cool red stage, the item as it drops.
            items.Add(Heat(WithMenu(Make("ember_blade", "Emberline", GearSlot.Weapon, LootTier.BlackDiamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.fire.red", FireBlade(0), FirePal(0), 0f, -10f,
                       pivotTexel: GreatswordGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.fire.menu", EmberlineDetail(), FirePal(0), 0f, -10f,
                       pivotTexel: EmberDetailGrip, ppu: MenuPpu)),
                HeatSprites(), signature: "conflagration"));

            // Emberline's RELIC half - the flame caged in iron, which used to be a second layer on
            // the weapon itself so one item could paint either slot. That was the legacy model, and
            // it is retired: GearSlots.Accepts no longer lets a WEAPON sit in the relic socket, so
            // the socket needs a real Relic-slot item. HasHeatCycle is set here as well as on the
            // blade, for the same reason blood_relic carries HasBloodVial - it is what tells
            // GameBootstrap to build the cycle at all, independent of which item is DRAWN. It
            // carries no HeatStages, so a plain sword socketing this gets the growing blast and a
            // blade that stays its own colour, which is the documented row of that table.
            var emberRelic = Make("ember_relic", "Emberline Mark", GearSlot.Relic,
                LootTier.BlackDiamond, 0f,
                HipRelic("gear.trinket.ember_relic", EmberlineRelicRows, emberRelicPal));
            emberRelic.HasHeatCycle = true;
            emberRelic.SignatureFinisher = "conflagration";
            items.Add(emberRelic);

            // ---- Shadow: the second black diamond weapon ----
            //
            // Power matches Emberline and the Gilded Greatsword at 14, for the reason written out
            // above: black diamond is a LATERAL choice, no stat penalty for wielding one and no
            // stat advantage either, with the locked finisher as what the tier actually buys.
            // Beating gold on stats as well would make the tier strictly best at everything.
            //
            // What it buys here is Echo - a chain that lands twice, its finisher slot one more
            // swing of it that never roots you, the light finisher's damage spread across every
            // echoed hit. Deliberately not comparable to Emberline: that one rewards patience
            // with a single growing blast, this one rewards keeping the swings coming.
            items.Add(Echo(WithMenu(Make("shadow_blade", "Shadow", GearSlot.Weapon,
                                         LootTier.BlackDiamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.shadow", ShadowSwordRows, ShadowPal(), 0f, -10f,
                       pivotTexel: GreatswordGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.shadow.menu", ShadowSwordDetail, ShadowPal(),
                       0f, -10f, pivotTexel: EmberDetailGrip, ppu: MenuPpu)),
                signature: "shadow_echo"));

            // Shadow's RELIC half - the squaring-the-circle seal. Split out of the weapon for the
            // same reason Emberline's was; HasEchoChain rides here too so the passive exists when
            // only the relic is socketed, which is the brief's own "relic only" row.
            var shadowRelic = Make("shadow_relic", "Shadow Seal", GearSlot.Relic,
                LootTier.BlackDiamond, 0f,
                HipRelic("gear.trinket.shadow_relic", ShadowRelicRows, shadowRelicPal));
            shadowRelic.HasEchoChain = true;
            shadowRelic.SignatureFinisher = "shadow_echo";
            items.Add(shadowRelic);

            // ---- the Rift Blade ----
            //
            // DIAMOND, AND POWER 0, because that is precisely what this item is. Bronze, Silver and
            // Gold are the stat-bearing tiers; DIAMOND IS THE PURELY COSMETIC ONE - GearRoller
            // rolls it nothing at all - and Black Diamond is the ceiling, whose promise is bespoke
            // art PLUS a finisher nothing else can roll.
            //
            // It first went in at Gold with power 14, which was wrong twice: it made an art-only
            // piece a stat-bearing weapon competing with the Gilded Greatsword, and it read as a
            // placeholder tier chosen to avoid over-claiming rather than as the tier that actually
            // describes the object.
            //
            // A 0-power weapon is not dead, it is a TRANSMOG SOURCE - which is the whole point of
            // the cosmetic tier. Skin a real sword as Rift Blade and you get its look, its drifting
            // shards and its implosion deaths, and none of its stats, because all three of those
            // follow the DRAWN weapon (see CharacterRigFactory and GameBootstrap.RiftBladeDrawn).
            // That is the same rule a skinned Emberline still heating already lives by, and it is
            // exactly what "a skin grants nothing" means: no power, no mechanic, all of the look.
            //
            // See BuildRiftSword for what makes it a different weapon from Shadow rather than a
            // recolour - the tear is ragged and LIT, and the outline is chewed where it widens.
            var riftBlade = WithMenu(Make("rift_blade", "Rift Blade", GearSlot.Weapon, LootTier.Diamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.rift", RiftSwordRows, RiftPal(), 0f, -10f,
                       pivotTexel: RiftGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.rift.menu", RiftSwordDetail, RiftPal(), 0f, -10f,
                       pivotTexel: RiftDetailGrip, ppu: MenuPpu));
            riftBlade.ImplodesKills = true;   // kills collapse into the Rift (RiftImplosion)
            items.Add(riftBlade);

            // ---- Saint ----
            //
            // Diamond and power 0, like the Rift Blade and for the same reason: art with no
            // mechanic belongs in the purely cosmetic tier, where its value is as a transmog
            // source. See BuildSaintSword for why the winged guard is the part that makes it a
            // different weapon rather than a differently-coloured one.
            items.Add(WithMenu(Make("saint_blade", "Saint", GearSlot.Weapon, LootTier.Diamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.saint", SaintSwordRows, SaintPal(), 0f, -10f,
                       pivotTexel: SaintGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.saint.menu", SaintSwordDetail, SaintPal(), 0f, -10f,
                       pivotTexel: SaintDetailGrip, ppu: MenuPpu)));

            // ---- Prism ----
            //
            // Diamond and power 0, on the same reasoning as the Rift Blade and Saint above: art
            // with no mechanic belongs in the purely cosmetic tier, where its value is as a
            // transmog source. The gem lighting is cosmetic for the same reason Rift Blade's
            // shard drift and implosion deaths are - it reacts to the game without granting
            // anything, which is what keeps a Diamond-tier reaction distinct from a Black
            // Diamond-tier mechanic.
            items.Add(WithMenu(Attune(Make("prism_blade", "Prism", GearSlot.Weapon, LootTier.Diamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.prism", PrismRows, PrismPal(Core.ElementType.Fire),
                       0f, -10f, pivotTexel: PrismGrip, ppu: FinePpu, upscale2x: true)),
                BuildPrismSet(), PrismGemAlongs()),
                Pixels(RigLayer.Weapon, "gear.weapon.prism.menu", PrismSwordDetail, PrismPal(Core.ElementType.Fire),
                       0f, -10f, pivotTexel: PrismDetailGrip, ppu: MenuPpu)));

            // ---- Sniper ----
            //
            // Diamond and power 0, same reasoning as every cosmetic weapon above: art with no
            // mechanic belongs in the purely cosmetic tier, where its value is as a transmog
            // source.
            var sniper = WithMenu(Make("sniper_blade", "Sniper", GearSlot.Weapon, LootTier.Diamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.sniper", SniperRows, SniperPal(), 0f, -10f,
                       pivotTexel: SniperGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.sniper.menu", SniperSwordDetail, SniperPal(), 0f, -10f,
                       pivotTexel: SniperDetailGrip, ppu: MenuPpu));
            // The cylinder turns a chamber on the attack after a finisher - see CylinderSpin.
            sniper.SpinFrames = SniperSpinFrames();
            items.Add(sniper);

            // ---- Crossblade ----
            //
            // Diamond and power 0, same reasoning as every cosmetic weapon above: art with no
            // mechanic belongs in the purely cosmetic tier, where its value is as a transmog
            // source.
            items.Add(WithMenu(Make("crossblade", "Crossblade", GearSlot.Weapon, LootTier.Diamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.crossblade", CrossbladeRows, CrossbladePal(), 0f, -10f,
                       pivotTexel: CrossbladeGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.crossblade.menu", CrossbladeSwordDetail, CrossbladePal(), 0f, -10f,
                       pivotTexel: CrossbladeDetailGrip, ppu: MenuPpu)));

            // ---- Lumen ----
            //
            // Diamond and power 0, same reasoning as every cosmetic weapon above: art with no
            // mechanic belongs in the purely cosmetic tier, where its value is as a transmog
            // source.
            //
            // NO OUTLINE - built directly against PixelSprite rather than through Pixels(), which
            // hardcodes one. Every other piece in this file wants the automatic border (see
            // PixelsCore's own note on why - twelve hand-drawn borders on one body reads as
            // noise), but a black cartoon line around a beam of light is exactly backwards: real
            // energy has no outline, it just stops being bright. SizeOf is called with the SAME
            // outline:false, or the rig's Size/bounds scaling stops being exactly (1,1) - the
            // identical warning PixelsCore's own comment carries for the true/true pairing.
            items.Add(WithMenu(Make("lumen_blade", "Lumen", GearSlot.Weapon, LootTier.Diamond, 0f,
                OutlinelessWeapon("gear.weapon.lumen", LumenRows, LumenPal(), LumenGrip, -5f,
                                  unlit: true)),
                OutlinelessMenuWeapon("gear.weapon.lumen.menu", LumenSwordDetail, LumenPal(),
                                      LumenDetailGrip, -5f)));

            // ---- Obsidian ----
            //
            // Diamond and power 0, same reasoning as every cosmetic weapon above: art with no
            // mechanic belongs in the purely cosmetic tier, where its value is as a transmog
            // source.
            //
            // Built from a photograph of a Central African executioner's sword rather than from
            // any other weapon in this file - a flared, wing-tipped blade with a raised centre
            // ridge on a fluted wood grip, nothing like the plain tapers every other greatsword
            // here shares. The silhouette IS the reference; nothing about it is a recolour.
            //
            // PRIMARILY BLACK, WITH GLASS ONLY AT THE EDGES - the inverse of Shadow's own split,
            // on the SAME material rather than a second colour. Shadow alphas almost the whole
            // blade and leaves a thin OPAQUE rim; this keeps the whole body OPAQUE black and
            // alphas only a thin rim at the true edges instead - not tinted glass, black glass,
            // exactly as see-through as Shadow's own blade. ObsidianGlassAlphas mirrors
            // ShadowAlphas' own weighting (lit tones near-solid, dark tones nearly gone), pointed
            // at the edge instead of the whole blade. See BuildObsidianSword for the wing
            // geometry and ObsidianPal for the one black ramp shared by both, plus wood for the
            // grip.
            //
            // NO OUTLINE, AND THE GLASS IS THEREFORE THE OUTERMOST THING ON THE BLADE - which is
            // the whole point of an edge-only treatment. Built directly against PixelSprite
            // rather than through Pixels(), which hardcodes one, the same way Lumen is: an
            // automatic border would ring the sword in a hard black line and put that line
            // OUTSIDE the see-through edge, so the first thing the eye meets would be a cartoon
            // outline rather than the glass. SizeOf takes the same outline:false, or the rig's
            // Size/bounds scaling stops being exactly (1,1) and quietly stretches the art.
            items.Add(WithMenu(Make("obsidian_blade", "Obsidian", GearSlot.Weapon, LootTier.Diamond, 0f,
                OutlinelessWeapon("gear.weapon.obsidian", ObsidianRows, ObsidianPal(),
                                  ObsidianGrip, -5f)),
                OutlinelessMenuWeapon("gear.weapon.obsidian.menu", ObsidianSwordDetail, ObsidianPal(),
                                      ObsidianDetailGrip, -5f)));

            // ---- Phantom: weapon and relic, the first BLACK DIAMOND pair built as one from the
            // start rather than retrofitted onto an existing plain weapon. See "Black diamond:
            // relic and cosmetic, never one weapon" - Emberline and Shadow still predate the
            // split and remain plain weapons until that migration happens; Phantom is the first
            // content actually built the new way.
            //
            // BLACK DIAMOND, POWER 0 on the WEAPON - unlike Emberline (14, because its mechanic
            // is the weapon's whole reason to exist) and more like the relic-split model's own
            // description of what the split buys: "a player holding only the relic is
            // mechanically identical to one holding both, so the cosmetic half can trade freely
            // on the open market without anyone buying an advantage by owning it." The mechanic
            // lives entirely on the relic below; the weapon is bespoke art and nothing else.
            var phantomWeapon = WithMenu(Make("phantom_blade", "Phantom", GearSlot.Weapon,
                LootTier.BlackDiamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.phantom", PhantomRows, PhantomPal(), 0f, -10f,
                       pivotTexel: PhantomGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.phantom.menu", PhantomSwordDetail, PhantomPal(), 0f, -10f,
                       pivotTexel: PhantomDetailGrip, ppu: MenuPpu));
            phantomWeapon.HasPhantomFlicker = true;
            phantomWeapon.SignatureFinisher = "phantom_reckoning";
            phantomWeapon.PhantomHazeFrames = BuildPhantomFrames();
            items.Add(phantomWeapon);

            // The RELIC: no art beyond the worn mark, no power (Loadout.TotalPower and WornCount
            // both skip Relic - it is carried, not worn), and the same SignatureFinisher id as
            // the weapon above, so wielding either one alone still grants Reckoning. Class and
            // TwoHanded are left at their Greatsword/true defaults deliberately - GearSlots.Accepts
            // reads them to refuse this relic in a socket beside a weapon of the wrong class.
            var phantomRelic = Make("phantom_relic", "Phantom Mark", GearSlot.Relic,
                LootTier.BlackDiamond, 0f,
                HipRelic("gear.trinket.phantom_relic", PhantomRelicRows,
                         Palette.Of(new Palette.Ramp(new Color(0.16f, 0.14f, 0.18f)),
                                    new Palette.Ramp(new Color(0.97f, 0.92f, 0.58f)))));
            phantomRelic.SignatureFinisher = "phantom_reckoning";
            items.Add(phantomRelic);

            // ---- Blood Blade: weapon and relic, the second pair built the new way. ----
            //
            // BLACK DIAMOND, POWER 0 on the WEAPON, same reasoning as Phantom's own entry above -
            // the mechanic lives on the relic, so the cosmetic half trades freely without anyone
            // buying an advantage by owning it.
            //
            // The blade is a BAT WING whose membrane is the vial's meter - see BuildBloodSword.
            // Menu art is hand-authored at true MenuPpu, drawn empty.
            var bloodWeapon = WithMenu(Make("blood_blade", "Blood Blade", GearSlot.Weapon,
                LootTier.BlackDiamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.blood", BuildBloodSword(0), BatPal(), 0f, -10f,
                       pivotTexel: BloodGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.blood.menu", BuildBloodSwordDetail(), BatPal(), 0f, -10f,
                       pivotTexel: BloodDetailGrip, ppu: MenuPpu));
            bloodWeapon.HasBloodVial = true;
            bloodWeapon.SignatureFinisher = "blood_drink";
            bloodWeapon.BloodStages = BuildBloodStages();
            items.Add(bloodWeapon);

            // The RELIC: no art beyond the worn mark, no power, same SignatureFinisher id as the
            // weapon so wielding either alone still grants Bloodletting. HasBloodVial is set here
            // too - it is what tells GameBootstrap to create the BloodVial component at all,
            // independent of which item is actually drawn.
            var bloodRelic = Make("blood_relic", "Blood Vial", GearSlot.Relic,
                LootTier.BlackDiamond, 0f,
                HipRelic("gear.trinket.blood_relic", BloodRelicRows, BloodPal()));
            bloodRelic.HasBloodVial = true;
            bloodRelic.SignatureFinisher = "blood_drink";
            items.Add(bloodRelic);

            // ---- Zanmato: the katana, and its saya relic. Third pair built the new
            // way. Power 0 on both - the Crosscut signature (sheathe, two crossing cuts, an
            // unsheathing draw that halves a non-boss) is granted by either piece; the weapon is
            // art only. Greatsword class / two-handed like every other Black Diamond sword, so it
            // skins with them and needs no new class.
            var zanmato = WithMenu(Make("zanmato_blade", "Zanmato", GearSlot.Weapon,
                LootTier.BlackDiamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.zanmato", ZanmatoRows, ZanmatoPal(),
                       0f, -10f, pivotTexel: ZanmatoGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.zanmato.menu", ZanmatoSwordDetail, ZanmatoPal(),
                       0f, -10f, pivotTexel: ZanmatoDetailGrip, ppu: MenuPpu));
            zanmato.SignatureFinisher = "crosscut";
            // The ANIMATION rides the weapon; the finisher itself rides the saya. Held or worn as
            // a transmog, this is what puts the scabbard sequence on the move.
            zanmato.HasSheathAnimation = true;
            items.Add(zanmato);

            // On the LEFT hip, opposite the sword hand, its mouth on the belt - see SayaMouthAt.
            // Authored at +x (the left hip facing right); WornOnLeftHip has the rig mirror it
            // facing left, when the right hand is on that side.
            var zanmatoSaya = Make("zanmato_saya", "Zanmato Saya", GearSlot.Relic,
                LootTier.BlackDiamond, 0f,
                Pixels(RigLayer.Trinket, "gear.trinket.zanmato_saya", ZanmatoSayaRows,
                       ZanmatoPal(), SayaOffsetCells.x, SayaOffsetCells.y, ppu: BodyPpu));
            zanmatoSaya.SignatureFinisher = "crosscut";
            zanmatoSaya.WornOnLeftHip = true;
            items.Add(zanmatoSaya);

            // ---- discs: the second weapon class ----
            //
            // TwoHanded false so the rig gives them a one-handed grip, and Class Disc so the run
            // rolls disc finishers rather than greatsword ones.
            items.Add(Disc(WithMenu(Make("silver_discs", "Silver Discs", GearSlot.Weapon, LootTier.Silver, 6f,
                Pixels(RigLayer.Weapon, "gear.weapon.disc.silver", DiscRows, silverGrip, DiscHandX, DiscHandY,
                       pivotTexel: DiscGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.disc.silver.menu", DiscDetail, silverGrip, DiscHandX, DiscHandY,
                       pivotTexel: DiscDetailGrip, ppu: MenuPpu, upscale2x: true))));

            // A second GOLD disc that is not gold coloured, which is the point of it: the tier is
            // the box it drops from and the violet is a material. Same tier and power band as the
            // gilded pair, different silhouette and different metal.
            items.Add(Disc(WithMenu(Make("gold_quatrefoil", "Nullpoint", GearSlot.Weapon, LootTier.Gold, 13f,
                Pixels(RigLayer.Weapon, "gear.weapon.disc.quatrefoil", QuatrefoilRows, voidGrip,
                       DiscHandX, DiscHandY, pivotTexel: DiscGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.disc.quatrefoil.menu", QuatrefoilDetail, voidGrip,
                       DiscHandX, DiscHandY, pivotTexel: DiscDetailGrip, ppu: MenuPpu, upscale2x: true))));

            items.Add(Disc(WithMenu(Make("gold_discs", "Gilded Discs", GearSlot.Weapon, LootTier.Gold, 13f,
                Pixels(RigLayer.Weapon, "gear.weapon.disc.gold", DiscRows, goldGrip, DiscHandX, DiscHandY,
                       pivotTexel: DiscGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.disc.gold.menu", DiscDetail, goldGrip, DiscHandX, DiscHandY,
                       pivotTexel: DiscDetailGrip, ppu: MenuPpu, upscale2x: true))));

            // ---- dual blades: still the disc class, a different silhouette ----
            //
            // SetWeaponSplit (the off-hand copy that makes a disc read as a PAIR) does not mirror -
            // it draws the same sprite at the same local rotation, only pushed outward, because
            // that is correct for a radially symmetric ring and nothing has ever asked it to be
            // anything else. A directional blade - an asymmetric hook, a single-bevelled edge -
            // would come out backwards in the off hand with no code path fixing it. So both of
            // these are drawn LEFT-RIGHT SYMMETRIC about their own centreline, the same discipline
            // every row in DiscRows/QuatrefoilRows already follows for the opposite reason (the
            // rig's own mirror needs an even width; this needs the CONTENT itself to be a
            // palindrome, so an unmirrored duplicate in the off hand is indistinguishable from a
            // second one actually held there). No new rig work, no new WeaponClass - just a
            // silhouette that survives being copied unmirrored.
            items.Add(Disc(Make("void_fang_blades", "Void Fang", GearSlot.Weapon, LootTier.Diamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.dualblade.voidfang", VoidFangRows, voidGrip,
                       DiscHandX, DiscHandY, pivotTexel: VoidFangGrip, ppu: FinePpu, upscale2x: true))));

            items.Add(Disc(Make("sunfire_cleavers", "Sunfire Cleavers", GearSlot.Weapon, LootTier.Gold, 13f,
                Pixels(RigLayer.Weapon, "gear.weapon.dualblade.sunfire", SunfireCleaverRows, goldGrip,
                       DiscHandX, DiscHandY, pivotTexel: SunfireCleaverGrip, ppu: FinePpu, upscale2x: true))));

            // A rose stem bent into a ring, thorns all round - DemoGear.Rose.cs.
            items.Add(BriarRose());

            // Pennywise's mouth, three crimson lights in its throat - DemoGear.Deadlights.cs.
            items.Add(Deadlights());

            // A total eclipse between two glass crescents - DemoGear.Eclipse.cs.
            items.Add(EclipseBlades());

            // An obsidian ring whose LEDs leave a light cycle's trail - DemoGear.RiftDisc.cs.
            items.Add(RiftDisc());

            // Lightning in disc form, arcing a greatsword's length off the ring - DemoGear.Rai.cs.
            items.Add(Rai());

            // A clock's planetary gear train in brass, ticking in mesh - DemoGear.Horologe.cs.
            items.Add(Horologe());

            // A black hole's ring of light, spiralling into a translucent horizon - DemoGear.Singularity.cs.
            items.Add(Singularity());

            // The disc Forge set: four element bands and the Armillary - DemoGear.Armillary.cs.
            AddArmillary(items);

            // King and Queen: a red lion and a white lioness, each an ouroboros, cracked with the
            // Secret Fire - DemoGear.LionOuroboros.cs.
            items.Add(LionOuroboros());

            // ---- bow: the third weapon class, pure ranged ----
            //
            // First-pass silhouette, not yet iterated against a live render the way the discs
            // and the relics were - flag for a follow-up pass once it has been seen on the rig.
            items.Add(Bow(Make("hunting_bow", "Hunting Bow", GearSlot.Weapon, LootTier.Silver, 6f,
                Pixels(RigLayer.Weapon, "gear.weapon.bow", BowRows, bowPal, 0f, 0f,
                       pivotTexel: BowGrip, ppu: FinePpu, upscale2x: true))));

            // ---- Verdant Warden: a living-tree gold-tier set (Shoulders/Torso) ----
            //
            // Built from a concept pass: a gold trunk rises from the belt across the cuirass and
            // forks at the collarbone, then the fork keeps going as gold vine trim wrapped around
            // each pauldron, ending in a spray of gold leaves at the shoulder's own crown. The
            // steel underneath is real, load-bearing plate the same size as every other gold-tier
            // pauldron in this file (VanguardPauldronRows' own footprint) - the leaves are
            // ornament ON a thick plate, not a replacement for one, which is the whole point after
            // the first pass read as a handful of leaves floating past a bare shoulder.
            //
            // Named as a MATERIAL (tempered plate + gold leaf trim), never the tier - gold tier
            // here is a coincidence of rarity like every other set in this file.
            var verdantSteel = new Palette.Ramp(new Color(0.68f, 0.73f, 0.65f));
            var verdantGold  = new Palette.Ramp(new Color(0.85f, 0.65f, 0.16f));
            var verdantPal2  = Palette.Of(verdantSteel, verdantGold);
            // Far-shoulder convention every tiered pauldron here uses: one darker ramp, no trim.
            var verdantDark  = Palette.Of(new Palette.Ramp(Color.Lerp(verdantSteel.Base, Color.black, 0.28f)));

            items.Add(Make("verdant_pauldrons", "Verdant Warden Pauldrons", GearSlot.Shoulders, LootTier.Gold, 6f,
                Pixels(RigLayer.Shoulders, "gear.pauldron.verdant", VerdantPauldronRows, verdantPal2,
                       -ShoulderX, PlateY(VerdantPauldronRows), ppu: BodyPpu),
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.verdant.dark", VerdantPauldronRows, verdantDark,
                       ShoulderX, PlateY(VerdantPauldronRows), ppu: BodyPpu)));

            items.Add(Defends(Make("verdant_plate", "Verdant Warden Cuirass", GearSlot.Torso, LootTier.Gold, 11f,
                // CuirassRows' own footprint (10 x 9), with the shared centre seam replaced by the
                // gold trunk: a wide band across the collarbone yoke tapers to a two-texel spine
                // through the dark body, sprouts one pair of leaf sprigs, and ends as a root tip
                // just above the fauld - the same trunk the pauldrons' vine trim continues onto.
                Pixels(RigLayer.TorsoArmor, "gear.torso.verdant", Reproportioned(VerdantCuirassRows), verdantPal2,
                       0f, 10f, ppu: BodyPpu)),
                DefensiveAbility.Barrier));

            // ---- Shogun: red lamellar over a black robe - DemoGear.Shogun.cs.
            AddShogun(items);

            // ---- Sovereign: white and gold over blue cloth - DemoGear.Sovereign.cs.
            AddSovereign(items);

            // ---- Revenant: black plate over grey quilting, worn under black_hood - DemoGear.Revenant.cs.
            AddRevenant(items);

            // ---- Nocturne: black plate and a long black coat piped in white - DemoGear.Nocturne.cs.
            AddNocturne(items);

            // ---- Orichalc: copper plate over yellow cloth, its sister set - DemoGear.Orichalc.cs.
            AddOrichalc(items);

            // ---- Vermilion: red plate trimmed in white, a grey cape - DemoGear.Vermilion.cs.
            AddVermilion(items);

            // ---- Talon: one arm in plate, one bare. See DemoGear.Talon.cs.
            AddTalon(items);

            // ---- Geode: stone plate, diamond in the seams, a black glass visor - DemoGear.Geode.cs.
            AddGeode(items);

            // ---- Errant: a hooded knight, silver on one side, worn under black_hood with the
            // Ashen scarf - DemoGear.Errant.cs.
            AddErrant(items);

            // ---- Survivor: a white tabard over grey steel, its cloth dyed by the worn cape -
            // DemoGear.Survivor.cs.
            AddSurvivor(items);

            // ---- Herald: gunmetal plate edged in gold, the wearer's element sigil on one shoulder, its leg cloth
            // dyed by the worn cape - DemoGear.Herald.cs.
            AddHerald(items);

            // ---- Tepes: black plate scrolled in gold, a spiked pauldron on the sword arm, a torn
            // navy cape with its fur over the other shoulder - DemoGear.Tepes.cs.
            AddTepes(items);

            // ---- Aether: black plate whose curse marks burn in the wearer's element, and the Aether
            // Greatsword, its Black Diamond sword, burning on the same beat - DemoGear.Aether.cs.
            AddAether(items);

            // ---- Tria Prima: three Diamond parts, and the Black Diamond sword they forge into.
            // See DemoGear.TriaPrima.cs.
            AddTriaPrima(items);

            // ---- menu art for everything that has none ----
            //
            // Six weapons carry HAND-DRAWN menu art, and those are left exactly as they are: a
            // bevel across the disc's ring and a fuller down the sword are decisions somebody
            // made, not something a pass can infer. Every other piece - the twelve armour and
            // jewellery slots - gets a DERIVED one instead of nothing.
            //
            // This is the note in PixelSprite about not block-upscaling to fill in missing sets,
            // and it is deliberately no longer being followed, because its premise changed. A
            // pure scale-up really is the same image and really would buy nothing. PixelDetail
            // chamfers the outline and puts one light across every ramp, so what comes out is the
            // same piece with form on it - which is exactly what the character screen is for and
            // what the arena has no pixels to show.
            // ---- dye channels for the Diamond armour built in this file (the partial-file sets
            // declare their own at the end of each AddX). Far layers here are the older
            // Lerp-toward-black convention, which is a shade of 1 - t on the base.
            Dyeable(items, "blacksteel_", DyeChannel.Of("Plate", DyeMaterial.Metal, blackSteel, 0.74f),
                    DyeChannel.Of("Trim", DyeMaterial.Metal, blackSteelAccent));
            Dyeable(items, "conclave_", DyeChannel.Of("Lacquer", DyeMaterial.Metal, conclaveLacquer, 0.74f),
                    DyeChannel.Of("White", DyeMaterial.Cloth, conclaveWhite, 0.88f));
            Dyeable(items, "seraph_", DyeChannel.Of("Plate", DyeMaterial.Metal, seraphSteel, 0.74f),
                    DyeChannel.Of("Ivory", DyeMaterial.Cloth, seraphIvory));
            Dyeable(items, "diamond_", DyeChannel.Of("Crystal", DyeMaterial.Gem, Palette.Tier(LootTier.Diamond)), null);
            // The belt is the one Seraph piece in its own palette (leather, the gilt, a blue stone).
            Dyeable(items, "seraph_belt", DyeChannel.Of("Leather", DyeMaterial.Leather, seraphLeather),
                    DyeChannel.Of("Stone", DyeMaterial.Gem, seraphBlue));

            foreach (var item in items) AddDerivedMenuArt(item);

            return items;
        }

        /// <summary>
        /// Give a piece a menu-density art set derived from its arena art, unless it already has
        /// one somebody drew.
        /// </summary>
        static void AddDerivedMenuArt(GearItem item)
        {
            if (item.MenuLayers is { Length: > 0 }) return;
            if (item.Layers == null || item.Layers.Length == 0) return;

            var made = new List<LayerSprite>(item.Layers.Length);
            foreach (var layer in item.Layers) made.Add(DerivedMenuLayer(layer));
            item.MenuLayers = made.ToArray();
        }

        /// <summary>
        /// One layer's derived menu art, or the layer itself where none can be derived. Also used
        /// directly for a piece's swappable variants (the Herald's sigils), which are not layers
        /// of the item and so are never reached by <see cref="AddDerivedMenuArt"/>.
        /// </summary>
        static LayerSprite DerivedMenuLayer(LayerSprite layer)
        {
            if (!_sources.TryGetValue(layer, out var src)) return layer;

            // Whole-number scale only. A fractional one puts art texels on fractions of a
            // menu texel, which is the whole thing PixelSprite's fixed densities exist to
            // avoid - armour, weapons and jewellery are all at 75 now (LayoutUnit == FinePpu
            // numerically) and go up 2x to MenuPpu(150); this is still derived rather than a
            // literal 2 so it keeps working if any of the three densities ever moves again.
            int scale = Mathf.RoundToInt(MenuPpu / src.Ppu);
            if (scale < 2) return layer;

            var rows = _unlit.Contains(layer)
                ? PixelDetail.Smooth(src.Rows, scale)
                : PixelDetail.Enrich(src.Rows, scale);
            var pivot = src.Pivot.HasValue
                ? new Vector2Int(src.Pivot.Value.x * scale, src.Pivot.Value.y * scale)
                : (Vector2Int?)null;

            var menu = PixelsCore(src.Layer, src.Key + ".menu.auto", rows, src.Palette,
                                  src.OffX, src.OffY, pivot, MenuPpu, src.Outline,
                                  src.OutlineColor);
            _drawnFrom[menu.Sprite] = new DrawnFrom(src.Key + ".menu.auto", rows, src.Palette, pivot,
                                                    MenuPpu, src.Outline, src.OutlineColor);
            return menu;
        }

        /// <summary>
        /// What a pixel layer's SPRITE was drawn from - <see cref="_sources"/> again, but keyed by
        /// the sprite, which survives what the LayerSprite does not: a minted instance is an
        /// Instantiate of its design, and Instantiate deep-copies a serializable class while
        /// keeping UnityEngine.Object references. Derived menu art is recorded too, with its
        /// already-enriched rows, so a repaint never pays for PixelDetail twice.
        ///
        /// Static and rebuilt with the catalogue: a domain reload empties it, and an item built
        /// before the reload simply repaints to itself until the catalogue is rebuilt.
        /// </summary>
        sealed class DrawnFrom
        {
            public readonly string Key;
            public readonly string[] Rows;
            public readonly Dictionary<char, Color> Palette;
            public readonly Vector2Int? Pivot;
            public readonly float Ppu;
            public readonly bool Outline;
            public readonly Color? OutlineColor;

            public DrawnFrom(string key, string[] rows, Dictionary<char, Color> palette, Vector2Int? pivot,
                             float ppu, bool outline, Color? outlineColor)
            {
                Key = key; Rows = rows; Palette = palette; Pivot = pivot;
                Ppu = ppu; Outline = outline; OutlineColor = outlineColor;
            }
        }

        static readonly Dictionary<Sprite, DrawnFrom> _drawnFrom = new();

        /// <summary>The palette a pixel layer's sprite was drawn with, or null for art this file
        /// didn't draw (or drew before a domain reload).</summary>
        public static IReadOnlyDictionary<char, Color> PaletteOf(Sprite sprite)
            => sprite != null && _drawnFrom.TryGetValue(sprite, out var src) ? src.Palette : null;

        /// <summary>
        /// <paramref name="layer"/> redrawn from its own rows with its palette repainted - the
        /// dye's way in (GearDye). <paramref name="repaint"/> returns the new palette, or null for
        /// "nothing here changes", in which case the layer comes back as it was. Cached by the
        /// layer's key plus <paramref name="tag"/>, so the tag must name everything the repaint
        /// depends on. Placement is copied from the layer: same rows, same ppu, same size.
        /// </summary>
        public static LayerSprite Repaint(LayerSprite layer, string tag,
                                          System.Func<IReadOnlyDictionary<char, Color>, Dictionary<char, Color>> repaint)
        {
            if (layer?.Sprite == null || !_drawnFrom.TryGetValue(layer.Sprite, out var src)) return layer;
            var pal = repaint(src.Palette);
            if (pal == null) return layer;
            return new LayerSprite
            {
                Layer = layer.Layer,
                Sprite = PixelSprite.From(src.Key + tag, src.Rows, pal, outline: src.Outline,
                                          pivotTexel: src.Pivot, pixelsPerUnit: src.Ppu,
                                          outlineColor: src.OutlineColor),
                Offset = layer.Offset,
                Size = layer.Size,
                Tint = layer.Tint,
            };
        }

        /// <summary>
        /// Declare a set's dye channels on every Diamond ARMOUR piece whose id starts with
        /// <paramref name="idPrefix"/> (weapons are never dyed - their effects aren't in the
        /// sprite). <paramref name="accent"/> may be null for a one-material piece.
        /// </summary>
        static void Dyeable(List<GearItem> items, string idPrefix, DyeChannel main, DyeChannel accent)
        {
            foreach (var item in items)
                if (item.ItemId.StartsWith(idPrefix, System.StringComparison.Ordinal)
                    && item.Tier == LootTier.Diamond && item.Slot != GearSlot.Weapon)
                    item.DyeChannels = accent != null ? new[] { main, accent } : new[] { main };
        }

        /// <summary>
        /// Stamp a weapon as running a heat cycle, and hand it the blade art for each stage.
        /// One place, so the flag and the sprites cannot drift apart - a weapon with the flag and
        /// no art would cycle invisibly, which is the one failure this design cannot survive.
        /// </summary>
        static GearItem Heat(GearItem item, Sprite[] stages, string signature)
        {
            item.HasHeatCycle = true;
            item.HeatStages = stages;
            item.SignatureFinisher = signature;
            item.TwoHanded = true;
            return item;
        }

        /// <summary>
        /// One swappable stage of a weapon (heat, vial, gem). Every weapon grid here is authored at
        /// half <see cref="FinePpu"/> and the base layer goes through <see cref="Pixels"/>'
        /// upscale2x, so a stage built straight from the same grid must be doubled the same way -
        /// built without it, it rendered at HALF the base layer's size the moment the rig swapped
        /// to it (only a stage sharing the base layer's cache key escaped, by accident).
        /// </summary>
        static Sprite StageSprite(string key, string[] rows, Dictionary<char, Color> palette,
                                  Vector2Int pivot)
            => PixelSprite.From(key, Double2x(rows), palette, outline: true,
                                pivotTexel: new Vector2Int(pivot.x * FineUpscale, pivot.y * FineUpscale),
                                pixelsPerUnit: FinePpu);

        static Sprite[] HeatSprites()
        {
            var all = new Sprite[5];
            string[] names = { "red", "orange", "yellow", "white", "blue" };
            for (int i = 0; i < 5; i++)
                all[i] = StageSprite("gear.weapon.fire." + names[i], FireBlade(i), FirePal(i), GreatswordGrip);
            return all;
        }

        /// <summary>Stamp a weapon as the disc class. One place, so the two flags cannot drift.</summary>
        static GearItem Disc(GearItem item, string signature = null)
        {
            // The disc standard (DiscHeightCells), checked against the height actually DRAWN - the
            // grid plus the auto-outline's canvas pad. An outlineless disc (Rai) has to make
            // the pad up out of its own grid; counting grid rows alone let one through 4 cells short.
            int pad = PixelSprite.OutlineCanvasPadFor(FinePpu);
            foreach (var layer in item.Layers)
                if (layer != null && layer.Layer == RigLayer.Weapon && _sources.TryGetValue(layer, out var src)
                    && src.Rows.Length + (src.Outline ? 2 * pad : 0) != DiscHeightCells * FineUpscale + 2 * pad
                    && !DiscHeightExceptions.ContainsKey(item.ItemId))
                    Debug.LogWarning($"[DemoGear] disc '{item.ItemId}' draws {(src.Rows.Length + (src.Outline ? 2 * pad : 0)) / FineUpscale} " +
                                     $"cells tall; the disc standard is {DiscHeightCells} (DiscHeightCells) plus the outline, " +
                                     $"{DiscHeightCells + 2 * pad / FineUpscale}. Build it to that height, or list it in " +
                                     "DiscHeightExceptions with the reason.");

            item.Class = WeaponClass.Disc;
            item.TwoHanded = false;
            item.SignatureFinisher = signature;
            return item;
        }

        /// <summary>
        /// Stamp a weapon as the bow class. TwoHanded true - both hands are genuinely engaged,
        /// one on the grip and one drawing - which also reuses the greatsword's own two-handed
        /// rig machinery for now rather than a bespoke draw-specific grip.
        /// </summary>
        static GearItem Bow(GearItem item, string signature = null)
        {
            item.Class = WeaponClass.Bow;
            item.TwoHanded = true;
            item.SignatureFinisher = signature;
            return item;
        }

        /// <summary>
        /// Stamp a relic as granting a finisher. Class/TwoHanded default to the greatsword, since
        /// that is what every other demo item not otherwise stamped already assumes - a relic
        /// meant for a different class would set them explicitly, the same way Disc() does.
        /// </summary>
        static GearItem RelicGrants(GearItem item, string signature,
            WeaponClass matchClass = WeaponClass.Greatsword, bool twoHanded = true)
        {
            item.SignatureFinisher = signature;
            item.Class = matchClass;
            item.TwoHanded = twoHanded;
            return item;
        }

        /// <summary>
        /// TORSO only. Overrides Make's own RollDefensiveAbility hash-roll with a deliberate
        /// pick - the same "small setter called on Make's result" shape as RelicGrants/Heat/Echo,
        /// used so the hand-authored cuirasses cover DefensiveAbility's whole enum on purpose
        /// instead of by hash luck. Minted loot (RunLoot, the Forge) still rolls its own ability
        /// at random; this only touches the catalog's own named pieces.
        /// </summary>
        static GearItem Defends(GearItem item, DefensiveAbility ability)
        {
            item.DefensiveAbility = ability;
            return item;
        }

        /// <summary>Flags a Neck item as a scarf rather than a fixed pendant - see AnimateScarf.</summary>
        static GearItem Scarf(GearItem item)
        {
            item.HasFlowingScarf = true;
            return item;
        }

        /// <summary>
        /// Flags a Back item as a hooded cloak rather than a plain cape - see
        /// PrimitiveCharacterRig's own _hoodOn (mane clipping) and AnimateScarf (freezes any
        /// worn scarf's tail, since the hood already covers the collar it trails from).
        ///
        /// <paramref name="color"/> is the cowl's own base colour, carried on
        /// <see cref="GearItem.HoodColor"/> so the back-of-head stand-in can be painted as hood
        /// fabric rather than hair once the rig turns around - see PrimitiveCharacterRig's
        /// HeadBack note.
        /// </summary>
        static GearItem Hood(GearItem item, Color color)
        {
            item.HasHood = true;
            item.HoodColor = color;

            // The back view, DERIVED from the cowl's own grid rather than drawn beside it, so the
            // two cannot disagree about the silhouette - the same reason the menu art is derived.
            foreach (var layer in item.Layers)
            {
                if (layer.Layer != RigLayer.Hood || !_sources.TryGetValue(layer, out var src)) continue;
                item.HoodBack = PixelsCore(src.Layer, src.Key + ".back", HoodBackRows(src.Rows),
                                           src.Palette, src.OffX, src.OffY, src.Pivot, src.Ppu,
                                           src.Outline, src.OutlineColor);
                break;
            }
            return item;
        }

        /// <summary>
        /// A cowl grid seen from behind: every row filled solid between its own outer edges.
        ///
        /// That closes the face opening AND the slit under the chin in one rule - from behind,
        /// a hood is one continuous mass of cloth from the crown down onto the shoulders, and the
        /// front's V is a fold only the front has. Filled with the tone the frame already carries
        /// at that row's inner edge, so the vertical banding (lit crown, shadowed skirt) runs
        /// straight across instead of breaking at where the window was.
        ///
        /// An INKED cowl ('o', see DemoGear.Hood.cs) outlines its opening on the cloth, so the fill
        /// runs between the first and last CLOTH texels of the row and swallows that inner line
        /// too - left in, it would draw the front's opening on the back of the head.
        ///
        /// A SEAM down the middle, one tone darker, is the only thing added. Without it a filled
        /// hood from behind is a flat blob that reads as a helmet or a sack; a centre-back seam is
        /// what says "cowl", and it costs two columns of existing tones rather than any new art.
        /// It sits at the GRID's centre, not each row's - the opening is deliberately lopsided
        /// (see DemoGear.Hood.cs), and a seam chasing each row's midpoint would jog.
        /// </summary>
        static string[] HoodBackRows(string[] front)
        {
            const string ramp = "sdblh";            // deep -> glow, one step apart
            var rows = new string[front.Length];
            int w = front[0].Length;
            int seamL = w / 2 - 1, seamR = w / 2;
            for (int r = 0; r < front.Length; r++)
            {
                var row = front[r];
                int first = -1, last = -1;
                for (int c = 0; c < row.Length; c++)
                    if (row[c] != '.' && row[c] != 'o') { if (first < 0) first = c; last = c; }
                if (first < 0) { rows[r] = row; continue; }

                var chars = row.ToCharArray();
                char fill = chars[first];
                for (int c = first; c <= last; c++)
                {
                    if (chars[c] != '.' && chars[c] != 'o') { fill = chars[c]; continue; }
                    chars[c] = fill;                  // the tone to the LEFT of the gap
                }
                foreach (int c in new[] { seamL, seamR })
                {
                    if (c < first || c > last) continue;
                    int i = ramp.IndexOf(chars[c]);
                    if (i > 0) chars[c] = ramp[i - 1];
                }
                rows[r] = new string(chars);
            }
            return rows;
        }

        /// <summary>Flags a Head item as tied on with cloth - see GearItem.HasTieBack.</summary>
        static GearItem Tied(GearItem item, Color color)
        {
            item.HasTieBack = true;
            item.TieColor = color;
            return item;
        }

        static LayerSprite Layer(RigLayer layer, Sprite sprite, Color tint, Vector2 offset, Vector2 size)
            => new() { Layer = layer, Sprite = sprite, Tint = tint, Offset = offset, Size = size };

        /// <summary>
        /// A pixel-art layer. Offset is in TEXELS, and both Size and Tint are derived rather than
        /// typed: Size comes from the grid so the rig's Size/bounds scaling is exactly (1,1) and
        /// the art cannot be stretched, and Tint stays white because pixel art carries its own
        /// colour - any other tint multiplies it darker.
        /// </summary>
        /// <summary>
        /// What every pixel layer was drawn FROM, so a higher-density version can be derived
        /// without restating any of it.
        ///
        /// The alternative was a second argument list at every one of the twenty-odd Pixels calls,
        /// which is twenty-odd chances for a menu piece to drift from the arena piece it is
        /// supposed to be the same object as. Bounded by the declared gear; nothing here is freed.
        /// </summary>
        static readonly Dictionary<LayerSprite, (RigLayer Layer, string Key, string[] Rows,
                                                 Dictionary<char, Color> Palette,
                                                 float OffX, float OffY,
                                                 Vector2Int? Pivot, float Ppu,
                                                 bool Outline, Color? OutlineColor)> _sources = new();

        /// <summary>
        /// Layers the menu-art pass smooths but does NOT light. PixelDetail's one light turns a
        /// surface toward it, and a beam of light has no surface: on Lumen's plasma it framed every
        /// run of the vein as its own bevelled brick, and the edge read as tiles instead of energy.
        /// </summary>
        static readonly HashSet<LayerSprite> _unlit = new();

        static LayerSprite Pixels(RigLayer layer, string key, string[] rows,
                                  Dictionary<char, Color> palette, float offX, float offY,
                                  Vector2Int? pivotTexel = null, float ppu = GearPpu,
                                  bool outline = true, Color? outlineColor = null,
                                  bool upscale2x = false)
        {
            // Done HERE rather than inside PixelsCore, and before _sources records anything: the
            // menu-art pass re-derives from the stored rows and ppu, so it has to see the doubled
            // grid or it would compute its own scale against art that no longer exists.
            //
            // Flagged rather than inferred from the ppu. Testing ppu == FinePpu would have been
            // true for every piece already converted to the body's own 150 - the cuirass, the
            // greaves, the boots - and doubled them a second time.
            //
            // Used by BOTH raised densities. FinePpu went 75 -> 150 and MenuPpu 150 -> 300, and in
            // each case the grids in this file are still authored at the old scale. Caught on the
            // hand-authored menu art: MenuPpu doubled while those grids did not, so every piece
            // carrying its own menu layer - the greatswords, the discs - rendered at half world
            // size while the DERIVED pieces were fine, because deriving recomputes its own scale.
            if (upscale2x)
            {
                rows = Double2x(rows);
                if (pivotTexel.HasValue)
                    pivotTexel = new Vector2Int(pivotTexel.Value.x * FineUpscale,
                                                pivotTexel.Value.y * FineUpscale);
            }

            var made = PixelsCore(layer, key, rows, palette, offX, offY, pivotTexel, ppu, outline,
                                  outlineColor);
            _sources[made] = (layer, key, rows, palette, offX, offY, pivotTexel, ppu, outline,
                              outlineColor);
            _drawnFrom[made.Sprite] = new DrawnFrom(key, rows, palette, pivotTexel, ppu, outline, outlineColor);
            return made;
        }

        static LayerSprite PixelsCore(RigLayer layer, string key, string[] rows,
                                  Dictionary<char, Color> palette, float offX, float offY,
                                  Vector2Int? pivotTexel = null, float ppu = GearPpu,
                                  bool outline = true, Color? outlineColor = null)
            => new()
            {
                Layer = layer,
                // outline: true - a single automatic border around the piece's own silhouette,
                // rather than every piece hand-drawing one. Hand-drawn borders stacked
                // border-on-border at every seam once twelve pieces were on the same body at
                // once, which is most of what made a fully equipped character read as noise.
                //
                // The stroke isn't just cosmetic - it's what pads a piece's silhouette out by
                // OutlinePadFor's own thickness against whatever is behind it (see that constant's
                // doc), which is the only thing keeping a mane's own edges from showing past a
                // hood/cape that would otherwise be drawn at exactly its raw, un-padded grid size.
                // wanderers_hood learned this the hard way: turning outline off entirely to avoid a
                // hard black seam also shrank the silhouette back to its raw size and let hair
                // clip past it. outlineColor is the actual fix - keep outline ON (keep the pad,
                // keep the mane covered) but stroke it in a soft, low-contrast shade instead of
                // Palette.Outline's near-black, so the border reads as a fold of the fabric rather
                // than an inked line.
                Sprite = PixelSprite.From(key, rows, palette, outline: outline, pivotTexel: pivotTexel,
                                          pixelsPerUnit: ppu, outlineColor: outlineColor),
                Offset = PixelSprite.Px(offX, offY),
                // SizeOf MUST be given the same ppu and the same outline flag as From, or the
                // rig's Size/bounds scaling stops being exactly (1,1) and quietly stretches the art.
                Size = PixelSprite.SizeOf(rows, outline: outline, pixelsPerUnit: ppu),
                Tint = Color.white,
            };

        /// <summary>
        /// Gear is drawn at the BODY's density - which, as of this constant's own doc history
        /// below, is no longer a separate, coarser density from weapons and jewellery.
        ///
        /// THE HISTORY, kept because it is still the reasoning that would apply again if this ever
        /// moves a third time: gear spent a while at 75 ppu once before and came back to the body's
        /// own (then 37.5) density. The thing that bump actually bought was the six-tone ramp - a
        /// glow above the light and a deep below the dark - which is what made plate read as metal
        /// instead of as a flat block, and that turned out to be a property of the PALETTE, not of
        /// the grid: the tones fit at the coarser density too, because these shapes are horizontal
        /// tone bands rather than fine detail, and halving a band still leaves a band. What the
        /// extra texels were really buying was a gear silhouette twice as fine as the body wearing
        /// it, which was judged the one thing a paper doll cannot afford. "One density, one
        /// character" was the rule that followed from that.
        ///
        /// WHAT CHANGED: <see cref="PixelSprite.LayoutUnit"/> itself moved from 37.5 to 75, taking
        /// the body and every armour grid with it (each block-doubled, not redrawn - see that
        /// constant's own doc). The paper-doll objection above was about RESOLUTION creating room
        /// for detail that doesn't belong on this character, and a block-double by construction
        /// adds none - so the objection doesn't re-apply just because the number now matches
        /// <see cref="FinePpu"/>. This alias is kept as its own named constant (rather than gear
        /// code reading LayoutUnit directly) so that history, and the option to move armour back
        /// down on its own again, both stay legible.
        /// </summary>
        const float GearPpu = PixelSprite.LayoutUnit;

        // ---- the blueprint set's shared anchors ----
        //
        // The silver/gold pieces re-placed against the reproportioned body take their positions
        // FROM THE RIG'S OWN JOINTS rather than from literals. That is not tidiness. Every piece in
        // this file was placed by hand against a body that has since changed shape twice, and the
        // failures were identical each time and invisible in source: a plate five cells outboard of
        // the shoulder it caps, a shadow at a sole the legs no longer reach, a neckline cancelled
        // by an overlap nobody re-read. A joint that moves now takes its armour with it.
        //
        // These are the anchors the rest of the armour set should be re-placed against as it is
        // converted, which is what "use it as a blueprint" means in practice.

        /// <summary>The body's own density - gear re-placed against the new outline is drawn at it.</summary>
        const float BodyPpu = PrimitiveCharacterRig.Proportions.BodyPpu;

        /// <summary>
        /// How far a shoulder plate sits off the centre line: the arm pivot itself. Sign is applied
        /// at the call site, because the near and far plates take opposite sides - see
        /// gold_pauldrons.
        /// </summary>
        const float ShoulderX = PrimitiveCharacterRig.Proportions.ShoulderHalfSpanCells;

        /// <summary>
        /// The chin, in torso-local cells: the neck joint, less the overlap that fuses head to
        /// torso. The rig places the head sprite by this same rule (see ChinOnNeckline), so armour
        /// hung off it stays put if the head's proportions move again.
        ///
        /// DECLARED BEFORE ShoulderPlateY, which reads it. Static field initialisers run in
        /// TEXTUAL ORDER, so the other way round the plate would be placed against a chin of zero -
        /// and silently, since a float has no "not set yet" to complain about.
        /// </summary>
        static readonly float ChinYCells =
            PrimitiveCharacterRig.Proportions.NeckYCells
            - PrimitiveCharacterRig.Proportions.Cells(PrimitiveCharacterRig.Proportions.NeckOverlap);

        /// <summary>
        /// Where a shoulder plate's CENTRE goes, in torso-local cells - measured DOWN FROM THE
        /// CHIN, not up from the shoulder joint, because the chin is what actually constrains it.
        ///
        /// The head is 32 texels across at its own density, which is wider than the whole shoulder
        /// span: at any height, a pauldron and the head overlap horizontally. Vertical clearance is
        /// therefore the only separation there is, and two passes anchored to ShoulderYCells both
        /// put the plate's top edge above the jaw line - gold headphones, not armour. The chin sits
        /// one cell BELOW the shoulder joint on this body (the neckline less the overlap that fuses
        /// head to torso), so a cap that clears it necessarily sits a little lower than the joint
        /// it caps. That is not a compromise: the reference wears its pauldron on the upper arm,
        /// under the collar line, for the same reason every chibi build does.
        ///
        /// PauldronRows is 10 texels = 5 cells tall and centre-pivoted, so half of that below the
        /// chin puts its top edge exactly there.
        /// </summary>
        static readonly float ShoulderPlateY = ChinYCells - 2.5f;

        /// <summary>
        /// <see cref="ShoulderPlateY"/> for any pauldron grid at <see cref="BodyPpu"/>: top edge
        /// on the chin, whatever the plate's height. The gold pair is the blueprint every other
        /// pauldron is placed against - before this, the rest sat on the old literal offsets at
        /// the old gear density and stood two to three times gold's size.
        /// </summary>
        static float PlateY(string[] rows)
            => ChinYCells - PrimitiveCharacterRig.Proportions.Cells(rows.Length) * 0.5f;

        /// <summary>
        /// Where a shoulder-covering drape's centre goes: its top edge one cell OVER the chin, so
        /// it starts above every pauldron (each tops out on the chin, plus a cell of outline).
        /// The head draws over that top band on every permutation, so the margin never shows.
        /// </summary>
        static float DrapeY(string[] rows)
            => ChinYCells + 1.5f - PrimitiveCharacterRig.Proportions.Cells(rows.Length) * 0.5f;

        /// <summary>
        /// Where the belt's centre goes, in torso-local cells - clear of CuirassRows' fauld rather
        /// than butted against it. See BeltRows on why the gap is the point.
        /// </summary>
        const float BeltY = 3f;

        /// <summary>
        /// Half a piece's REAL height in cells - its grid plus the auto-outline's own border.
        ///
        /// THE OUTLINE IS PART OF THE PIECE, and forgetting that is what hid the tasset. The belt's
        /// grid is 6 texels (3 cells) tall, so measuring it by the grid puts its lower edge 1.5
        /// cells below its centre; the stroke adds a texel of border on every side, so the sprite
        /// that actually draws is 10 texels (5 cells) and reaches 2.5 cells down. The leg plate was
        /// placed against the first number, landed a full cell inside the belt, and - because Belt
        /// sorts above LegsFront - lost its own lit top lip behind it. What was left was the plate's
        /// base and shadow rows, which read as a shadow under the belt rather than as a plate.
        ///
        /// Everything below measures with this rather than with grid rows, so two plates asked to
        /// sit flush actually do.
        /// </summary>
        static float OutlinedHalfCells(int gridRows)
            => PrimitiveCharacterRig.Proportions.Cells(gridRows) * 0.5f
             + PrimitiveCharacterRig.Proportions.Cells(PixelSprite.OutlinePadFor(BodyPpu));

        // These are PROPERTIES, not static readonly fields, and that is load-bearing: they read the
        // grid arrays declared much further down this file, and a static field initialiser runs in
        // TEXTUAL order - it would see a null array and throw at class init. ChinYCells above can
        // be a field only because it reads nothing but Proportions.

        /// <summary>
        /// The belt's real lower edge, in cells. Torso-local 0 and leg-local 0 are both the hip, so
        /// this reads directly as the height the leg armour hangs from.
        /// </summary>
        static float BeltBottomYCells => BeltY - OutlinedHalfCells(BeltRows.Length);

        /// <summary>
        /// Where the tasset's centre goes, in TORSO-local cells - the same pivot the belt it hangs
        /// from uses, now that the piece has moved off the leg pivots (see RigLayer.Tasset). Hung so
        /// its outlined TOP meets the belt's outlined bottom exactly, leaving none of it behind the
        /// strap, and so its outlined bottom lands on the boot's outlined top with no bare gap.
        ///
        /// Torso-local 0 and leg-local 0 are both the hip, so the arithmetic did not change when the
        /// frame did - which is exactly the kind of coincidence worth stating, because the next
        /// person to move one of these pieces will want to know the two frames share an origin
        /// rather than rediscovering it.
        /// </summary>
        static float TassetY => BeltBottomYCells - OutlinedHalfCells(TassetRows.Length);

        /// <summary>
        /// Where the tall boot's centre goes, in leg-local cells. Pinned at the SOLE and measured
        /// upward, not placed by eye: the leg ends at -Cells(LegH), so the boot's ART bottom sits
        /// there and its centre is half the grid above it. The outline is deliberately NOT included
        /// here - it should hang below the sole the way every other piece's does, rather than
        /// lifting the boot off the ground by a texel. A boot whose sole floats is the one error on
        /// this piece nobody would forgive, and the contact shadow is drawn at this same sole line
        /// (see BuildShadow), so the two agree by construction rather than by tuning.
        /// </summary>
        static float BootY => SoleY(BootRows);

        /// <summary>
        /// Where the arm harness's centre goes, in arm-local cells: 0 is the shoulder and the limb
        /// runs to -Cells(ArmH), so a piece centred here starts just below the shoulder - clear of
        /// the pauldron - and ends on the hand. See GloveRows.
        /// </summary>
        static float ArmHarnessY
            => -PrimitiveCharacterRig.Proportions.Cells(PrimitiveCharacterRig.Proportions.ArmH) * 0.5f - 1f;


        /// <summary>
        /// Weapons' and jewellery's own density - NO LONGER "the doubled one" now that
        /// <see cref="GearPpu"/> matches it exactly (both trace back to the same 75). Kept as a
        /// separate named constant because the REASON these categories need this density is
        /// independent of whatever the body happens to be at, and didn't stop being true when the
        /// body caught up:
        ///
        /// WEAPONS, because a weapon is held AWAY from the body and looked at directly, and the
        /// disc in particular is a ring whose thickness, hole and outline all have to fit across
        /// the same span. Halved, the hole barely survived its own outline and the round edge went
        /// lumpy.
        ///
        /// JEWELLERY, for the blunter reason that there is nothing left. Reduced, the signet came
        /// out as a 2x1 grid - two texels of stone inside an outline - and a ring you can see
        /// through the disc's hollow, sitting on the glove, is worth more than two texels.
        ///
        /// Every weapon/jewellery GRID and pivotTexel is untouched by the LayoutUnit move (they
        /// were already drawn at this density); only their offX/offY position literals moved,
        /// because <see cref="PixelSprite.Px(float,float)"/> divides by the global LayoutUnit
        /// regardless of a piece's own ppu - see that constant's own note.
        /// </summary>
        const float FinePpu = 150f;

        /// <summary>
        /// How much a FinePpu grid is block-doubled on its way to a sprite.
        ///
        /// FinePpu moved 75 -> 150 and every weapon and jewellery grid in this file is still
        /// authored at the old scale, so each is doubled here. That is FREE and LOSSLESS - a
        /// block-double at twice the ppu is the same image at the same world size, pixel for
        /// pixel - which is the only reason this could be a one-line density change instead of
        /// redrawing twenty-seven pieces.
        ///
        /// WHY IT HAD TO MOVE. FinePpu existed because "a weapon is held away from the body and
        /// looked at directly", and at 75 against a 37.5 body it was the FINER of the two. Moving
        /// the body to 150 silently inverted that: the sword became half the resolution of the
        /// armour next to it, and read chunkier than the character holding it. 150 restores the
        /// relationship the constant was created to express.
        ///
        /// Anything authored at true 150 must NOT pass upscale2x - see Pixels.
        /// </summary>
        const int FineUpscale = 2;

        /// <summary>Block-double a grid: every texel becomes a 2x2 block. See FineUpscale.</summary>
        static string[] Double2x(string[] rows)
        {
            if (rows == null || rows.Length == 0) return rows;
            var big = new string[rows.Length * FineUpscale];
            for (int r = 0; r < rows.Length; r++)
            {
                var sb = new System.Text.StringBuilder(rows[r].Length * FineUpscale);
                foreach (char c in rows[r]) for (int k = 0; k < FineUpscale; k++) sb.Append(c);
                for (int k = 0; k < FineUpscale; k++) big[r * FineUpscale + k] = sb.ToString();
            }
            return big;
        }

        /// <summary>
        /// How much narrower a torso piece is drawn on the reproportioned body: CuirassRows went
        /// from 20 texels at GearPpu to 32 at BodyPpu (40 body texels to 32) and kept its height.
        /// The body narrowed and did not shrink, so its armour does the same.
        /// </summary>
        const float ReproportionWidth = 0.8f;

        /// <summary>
        /// A GearPpu torso grid re-sampled for <see cref="BodyPpu"/> the way CuirassRows was
        /// re-sized by hand: every row doubled (the same height in the world) and the columns
        /// nearest-neighbour sampled down to <see cref="ReproportionWidth"/> of their world width,
        /// rounded to an even count. Each source column lands on one or two texels, never none,
        /// so no feature is lost. The right half's columns are the left half's mirrored, so a
        /// symmetric grid stays symmetric whatever the ratio.
        ///
        /// For the pieces still on the old 75 grid, against the blueprint rather than redrawn.
        /// An oversized piece stays oversized by the same factor against the standard cuirass.
        /// </summary>
        /// <summary>A block-doubled grid back at its design size: every other row and column.</summary>
        static string[] Undoubled(string[] rows)
        {
            var half = new string[rows.Length / 2];
            var sb = new System.Text.StringBuilder(rows[0].Length / 2);
            for (int r = 0; r < half.Length; r++)
            {
                sb.Clear();
                for (int c = 0; c < rows[r * 2].Length; c += 2) sb.Append(rows[r * 2][c]);
                half[r] = sb.ToString();
            }
            return half;
        }

        static string[] Reproportioned(string[] gearRows)
            => Resampled(gearRows,
                         Mathf.RoundToInt(gearRows[0].Length * BodyPpu / GearPpu * ReproportionWidth * 0.5f) * 2);

        /// <summary>
        /// A GearPpu grid at BodyPpu: every row doubled (the same height in the world) and the
        /// columns nearest-neighbour sampled to <paramref name="width"/> body texels. The right
        /// half's columns are the left half's mirrored, so a symmetric grid stays symmetric
        /// whatever the ratio.
        /// </summary>
        static string[] Resampled(string[] gearRows, int width)
        {
            int src = gearRows[0].Length;
            float bodyPerGear = BodyPpu / GearPpu;
            float step = (float)src / width;

            var map = new int[width];
            for (int j = 0; j < width / 2; j++)
            {
                map[j] = Mathf.Min(src - 1, Mathf.FloorToInt((j + 0.5f) * step));
                map[width - 1 - j] = src - 1 - map[j];
            }

            int rowsPer = Mathf.RoundToInt(bodyPerGear);
            var outRows = new string[gearRows.Length * rowsPer];
            var sb = new System.Text.StringBuilder(width);
            for (int r = 0; r < gearRows.Length; r++)
            {
                sb.Clear();
                for (int j = 0; j < width; j++) sb.Append(gearRows[r][map[j]]);
                for (int k = 0; k < rowsPer; k++) outRows[r * rowsPer + k] = sb.ToString();
            }
            return outRows;
        }

        /// <summary>
        /// One leg's armour across, in body texels: the bare leg's 8 plus two either side, under
        /// Silver's boot (10 at the shaft, 14 at the foot). The old per-leg grids were 16 at
        /// GearPpu - 32 body texels, four legs wide - so each pair merged into one block across
        /// both legs and the gap between them.
        /// </summary>
        const int LegArmourWidth = 12;

        /// <summary>A GearPpu leg or boot grid at one leg's width - see <see cref="Resampled"/>.</summary>
        static string[] PerLeg(string[] gearRows) => Resampled(gearRows, LegArmourWidth);

        /// <summary>
        /// Leg-local centre that stands any boot grid on the SOLE - <see cref="BootY"/>'s rule
        /// for a grid of any height. The old boots sat on literal offsets against a shorter leg
        /// and stopped five cells above the foot.
        /// </summary>
        static float SoleY(string[] rows)
            => -PrimitiveCharacterRig.Proportions.Cells(PrimitiveCharacterRig.Proportions.LegH)
             + PrimitiveCharacterRig.Proportions.Cells(rows.Length) * 0.5f;

        /// <summary>
        /// The density of MENU art - now DOUBLE the arena density (was four times the body, twice
        /// the weapons; the body and weapons are the same density now, so this is one multiplier,
        /// not two).
        ///
        /// This is past what the arena can draw: at 150 ppu a texel is at most one screen pixel
        /// anywhere the game runs, and below one at a 1080 render height. The CHARACTER SCREEN is
        /// the exception - it magnifies a body texel to about 14 screen pixels, so a 150 ppu one
        /// still gets three and a half, and detail that would alias in the fight resolves cleanly
        /// there.
        ///
        /// Menu art is OPT-IN per piece (<see cref="GearItem.MenuLayers"/>). A piece with none
        /// falls through to its arena art, which is the right answer until someone actually draws
        /// finer pixels: block-upscaling a grid to fit a higher density is provably the same
        /// image, so it would cost memory and buy nothing.
        /// </summary>
        const float MenuPpu = 300f;

        /// <summary>Attach the character screen's art to an item. Chains like <see cref="Disc"/>.</summary>
        static GearItem WithMenu(GearItem item, params LayerSprite[] layers)
        {
            item.MenuLayers = layers;
            return item;
        }

        // ---- gear grids. Row 0 is the TOP row, as drawn. Widths are EVEN for the mirror.
        //
        // Tones run s / d / b / l / h (deep, dark, base, light, glow); a second material uses the
        // uppercase set. No hand-drawn border - Pixels() auto-outlines each piece, one art pixel
        // thick at this density (PixelSprite.OutlinePadFor derives that from the ppu, so the line
        // is the same WIDTH on screen as the two-pixel one it replaces).
        //
        // Reduced 2:1 from the 75-ppu versions, keeping every tone: a block that was half opaque
        // stays opaque, and the surviving tone is the block's most common one. Symmetric pieces
        // were re-mirrored afterwards, because a diagonal crossing a block boundary can lose a
        // texel on one side and the rig mirrors the whole character by negating scale. The sword
        // and the disc are deliberately NOT re-mirrored - both are lit from one side, and
        // mirroring would flatten the gradient this conversion exists to preserve.

        // 28 x 12. Domes the 32-wide head: glow crown, a lit brow band, base into a
        // dark-then-deep rim, with short cheek guards dropping past the jaw.
        static readonly string[] HelmRows =
        {
                "........hhhhhhhhhhhh........",
                "........hhhhhhhhhhhh........",
                "....hhhhllllllllllllhhhh....",
                "....hhhhllllllllllllhhhh....",
                "..hhbbbbbbbbbbbbbbbbbbbbhh..",
                "..hhbbbbbbbbbbbbbbbbbbbbhh..",
                "..bbbbbbbbbbbbbbbbbbbbbbbb..",
                "..bbbbbbbbbbbbbbbbbbbbbbbb..",
                "..ssddddddddddddddddddddss..",
                "..ssddddddddddddddddddddss..",
                "..ssss................ssss..",
                "..ssss................ssss..",
        };

        // 18 x 16 at Proportions.BodyPpu (150) - re-placed against the reproportioned shoulder, the
        // same move CuirassRows documents for the chest and TassetRows for the hips.
        //
        // The old grid was 12 x 6 at 75: 0.16 x 0.08 world units, sitting on a shoulder whose arm
        // is 0.067 across. Rendered, the two of them read as epaulettes floating clear of the body
        // rather than caps on it - which was not only size. They were offset +/-12 CELLS while the
        // arm pivots sit at +/-ShoulderHalfSpanCells (7), so each plate hung five cells outboard of
        // the joint it was supposed to cap. Both offsets are now taken FROM that constant with the
        // matching sign, so the near plate lands on the near arm by construction instead of by a
        // literal that was right once.
        //
        // WIDER THAN THE ARM, DELIBERATELY: 18 against the limb's 10. The project rule is that gear
        // must break the silhouette, and a shoulder is where that is cheapest to do - it is the
        // widest point of a standing figure, so the overhang reads as armour at a glance and costs
        // nothing in legibility lower down.
        //
        // TWO PIECES, NOT A DOME. The cap alone is a smooth blob at any size; what says "pauldron"
        // is the LAME below it, and what makes a lame read is the deep groove between the two
        // (row 8) with the lame's own lit top edge directly under it. A single bright-to-dark
        // gradient over sixteen rows reads as a ball. The groove borders opaque art on every side,
        // so the auto-outline has nothing to erode it from - the rule that closed the disc's grip
        // hollow.
        // FLAT, at 18 x 10 rather than 18 x 16. The first pass was almost square, and a square
        // footprint under a smooth glow-to-deep gradient reads as a SPHERE - rendered, the pair sat
        // either side of the jaw looking like gold headphones. Nearly 2:1 is what makes the same
        // gradient read as a curved plate seen from slightly above, and it costs nothing: the rows
        // that went were interior banding, not silhouette.
        static readonly string[] PauldronRows =
        {
                "....hhhhhhhhhh....",
                "..llllllllllllll..",
                "llllllllllllllllll",
                "bbbbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbbbb",
                "ssssssssssssssssss",
                "llllllllllllllllll",
                "bbbbbbbbbbbbbbbbbb",
                ".dddddddddddddddd.",
                "..ssssssssssssss..",
        };

        // 20 x 16. Sits inside the 24-wide torso so the body's own outline still frames it. Lit
        // yoke across the collarbones, a faint centre seam, then dark and deep to a tapered fauld.
                // 32 x 32 at Proportions.BodyPpu (150), NOT the gear ppu the rest of the file uses.
        //
        // THE TRACER for re-placing gear against the reproportioned body. Shared by the gold,
        // silver, bronze and diamond plates, so one grid answers four pieces.
        //
        // The old grid was 20 x 16 at 75, which is 0.267 world units across. The torso it is worn
        // on is now 0.173 across - so the cuirass was half again wider than the body inside it.
        // Density was never the problem and doubling the grid alone would not have helped: a gear
        // grid can move 75 -> 150 by block-doubling for free, pixel-identical at the same world
        // size. What changed is the body's WORLD proportions, so the piece has to be re-sized and
        // re-placed, not just re-sampled.
        //
        // Sized against the torso rather than by eye: half-width 16 texels against the torso's 13,
        // so the plate overhangs by three either side - armour sits ON the body, and a cuirass cut
        // flush to the silhouette reads as paint. Height runs the shoulder line down past the
        // waist into a fauld, which is what stops the legs starting at the chest.
        //
        // ASYMMETRIC, following the torso underneath it - see the Torso grid's own note. A
        // symmetric cuirass on a turned body throws the turn away, because the plate is most of
        // what the silhouette actually shows once armour is on. Straight back edge, chest carried
        // forward, fauld drawing in. Authored toward +X; the mirror gives the other direction.
        //
        // THE COLLAR IS LOAD-BEARING, not decoration. RigLayer.TorsoOver's own note: "the chibi
        // body has no neck, so the head sitting IN FRONT OF the collar's top rows is what makes
        // the head read as set INTO it." The head just went from 45% of the figure to 21%, so it
        // needs that framing more than it ever did - the first four rows are the gorget it sits in.
        static readonly string[] CuirassRows =
        {
                "..........khhlllllllhhk.........",
                "..........khhlllllllhhk.........",
                "..........kdddbbbbbblllk........",
                "..........kdddbbbbbblllk........",
                ".....kdddddllllllllllhhhhhk.....",
                ".....kdddddllllllllllhhhhhk.....",
                "...kddddddllllllllllllhhhhhhk...",
                "...kddddddllllllllllllhhhhhhk...",
                "..kdddddddbbbbbbbbbbbbbblllllllk",
                "..kdddddddbbbbbbbbbbbbbblllllllk",
                "..kdddddddbbbbbbbbbbbbbblllllllk",
                "..kdddddddbbbbbbbbbbbbbblllllllk",
                "..kdddddddbbbbbbbbbbbbbblllllllk",
                "..kdddddddbbbbbbbbbbbbbblllllllk",
                "..kdddddddbbbbbbbbbbbbbblllllllk",
                "..kdddddddbbbbbbbbbbbbbblllllllk",
                "..kdddddddbbbbbbbbbbbbbblllllllk",
                "..kdddddddbbbbbbbbbbbbbblllllllk",
                "..kdddddddbbbbbbbbbbbbbblllllllk",
                "..kdddddddbbbbbbbbbbbbbblllllllk",
                "..kdddddddbbbbbbbbbbbbbblllllllk",
                "..kdddddddbbbbbbbbbbbbbblllllllk",
                "..kdddddddddddddddddddddddddddk.",
                "..kdddddddddddddddddddddddddddk.",
                "..kddddddbbbbbbbbbbbbbbllllllk..",
                "..kddddddbbbbbbbbbbbbbbllllllk..",
                "..kddddddbbbbbbbbbbbbbbllllllk..",
                "..kddddddbbbbbbbbbbbbbbllllllk..",
                "..kddddddbbbbbbbbbbbbbbllllllk..",
                "..kddddddbbbbbbbbbbbbbbllllllk..",
                "...ksssssdddddddddddddbbbbbk....",
                "...ksssssdddddddddddddbbbbbk....",
        };

        // 52 x 20 - the widest gear grid in the file, and the only one that leaves the body's own
        // outline sideways. Authored as 26 x 10 and block-doubled, the same way every grid here is;
        // the design reasoning lives on the conclave_gorget item above.
        //
        // Drawn at BodyPpu since the reproportion, grid untouched: every measurement above is
        // against the HEAD (1.69x its width, the inner edge under the chin, horn tips at cheek
        // height), and the head is the same 32 x 28 drawn at half its old size - so the gorget
        // halves exactly with it rather than following the torso's Reproportioned rule.
        //
        // Reading the shape out of the rows: every column is exactly six lames deep, and the whole
        // band steps up one lame at a time as it goes out. That is the crescent - a constant-width
        // annulus, not a shape that happens to curve - and it is why the lames can be a flat
        // inner-to-outer ramp rather than needing a per-column depth fraction.
        //
        // THE STAIRCASE IS THE CURVE, so its RUN LENGTHS are the whole game. They must shrink
        // monotonically going outward - here 4,3,2,2,2. A run sequence that stops shrinking and
        // then grows again reads as a wobble, and that is not a theory: the first crescent was
        // hand-typed at 4,2,2,2,2,3 and the edge visibly waved on the body. A true circle through
        // the same two endpoints quantises to 5,3,2,2,1, which is monotone but ends in a run of
        // ONE - a spike where the horn wants a squared cap - so one column is moved from the flat
        // centre to the tip. Same rise, same width, still monotone, blunt 4-texel horn cap.
        //
        // THE TRIM'S STAIR CORNERS ARE JOINED (the doubled 's' cells at each step). A one-lame
        // line steps a full line-width per stair, so a block and the next share only a CORNER, and
        // on the body that read as a row of detached dashes - a dotted wavy line, not an edge.
        // Thickening it to two lames also fixes it and was tried; it doubles the trim and the
        // piece stops being a plate with an edge on it. One lame plus a corner cell costs one cell
        // per step, changes no silhouette (the extra cell is inside the band), and is the ordinary
        // way a diagonal of thickness one is drawn in pixel art.
        //
        // This was worked out while the trim was the GOLD one on a black plate. The materials have
        // since swapped - electrum plate, lacquer trim - and the rule survived the swap untouched,
        // which is the tell that it is about line width and quantisation rather than about colour.
        //
        // The 's' column is the front join, and the only ornament. Radiating segment seams were
        // tried and cut: near the neck the rays sit close enough together to merge into one dark
        // blob across the middle. It is a GROOVE, not a notch cut from the silhouette - it borders
        // opaque art on every side, so the auto-outline has nothing to erode it from (the rule
        // that closed the disc's grip hollow and Sniper's prong gap).
        /// <summary>
        /// The gorget's centre, torso-local cells. The grid runs from 4 cells over the chin (the
        /// horn tips) to 6 under it at this density, so its centre is one cell below the chin.
        /// </summary>
        static float ConclaveGorgetY => ChinYCells - 1f;

        static readonly string[] ConclaveGorgetRows =
        {
                "ssss............................................ssss",
                "ssss............................................ssss",
                "HHssssss....................................ssssssHH",
                "HHssssss....................................ssssssHH",
                "LLLLHHssssss............................ssssssHHLLLL",
                "LLLLHHssssss............................ssssssHHLLLL",
                "BBBBLLLLHHssssssss................ssssssssHHLLLLBBBB",
                "BBBBLLLLHHssssssss................ssssssssHHLLLLBBBB",
                "BBBBBBBBLLLLHHHHssssssssssssssssssssHHHHLLLLBBBBBBBB",
                "BBBBBBBBLLLLHHHHssssssssssssssssssssHHHHLLLLBBBBBBBB",
                "DDDDBBBBBBBBLLLLLLHHHHHHssssHHHHHHLLLLLLBBBBBBBBDDDD",
                "DDDDBBBBBBBBLLLLLLHHHHHHssssHHHHHHLLLLLLBBBBBBBBDDDD",
                "....DDDDBBBBBBBBBBLLLLLLssssLLLLLLBBBBBBBBBBDDDD....",
                "....DDDDBBBBBBBBBBLLLLLLssssLLLLLLBBBBBBBBBBDDDD....",
                "........DDDDBBBBBBBBBBBBssssBBBBBBBBBBBBDDDD........",
                "........DDDDBBBBBBBBBBBBssssBBBBBBBBBBBBDDDD........",
                "............DDDDDDBBBBBBssssBBBBBBDDDDDD............",
                "............DDDDDDBBBBBBssssBBBBBBDDDDDD............",
                "..................DDDDDDssssDDDDDD..................",
                "..................DDDDDDssssDDDDDD..................",
        };

        // 16 x 12, logical 8 x 6. Plain lacquer: a lit thigh, base, and the shadow the boot cuff
        // will sit in. Deliberately featureless - it is the one piece of this costume that is
        // FIELD rather than object, and every line drawn on it competes with the crescent.
        static readonly string[] ConclaveLegRows =
        {
                "llllllllllllllll",
                "llllllllllllllll",
                "bbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbb",
                "dddddddddddddddd",
                "dddddddddddddddd",
                "dddddddddddddddd",
                "dddddddddddddddd",
                "..ssssssssssss..",
                "..ssssssssssss..",
        };

        // 32 x 12, logical 16 x 6 - the composed head's own rows 24..35.
        //
        // A HALF MASK: it starts at the NOSE and covers everything below it. The head grid places
        // the eyes at rows 20..23, so row 24 is the first row past them - the bridge of the nose -
        // and cutting there is what leaves the eyes reading as eyes rather than as two holes in a
        // faceplate. Everything above the cut (brow, eyes, fringe, crown) is the character's own
        // face, untouched.
        //
        // It still narrows over its last three row pairs, because it is still wearing the skull's
        // own jaw - those are the head's rows 30..35 verbatim.
        //
        // The full-face version that came before covered rows 16..35, and the thing worth keeping
        // from it: the face proper starts at row 18, but stopping there left a two-texel band of
        // bare SKIN between the fringe and the mask - read off the render at (71,48,36), not
        // guessed. Any future piece cut at the hairline has to clear row 16, not row 18. A mask
        // cut at the nose never meets that edge at all.
        //
        // Stays on the bottom of the ramp - base, dark, deep, no 'l' anywhere. An 'l' row is 0.45
        // grey, which is a dome catching the light; this piece has no dome, and against black it
        // read as a separate grey stripe rather than as the top of anything.
        static readonly string[] ConclaveMaskRows =
        {
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "dddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddd",
                "..dddddddddddddddddddddddddddd..",
                "..dddddddddddddddddddddddddddd..",
                "....ssssssssssssssssssssssss....",
                "....ssssssssssssssssssssssss....",
                "........ssssssssssssssss........",
                "........ssssssssssssssss........",
        };

        // 14 x 28 at BodyPpu - the WHOLE ARM, on GloveRows' own frame (same widths, same elbow
        // and wrist breaks, so the elbow split cuts in the same place): the costume's three
        // values in order down the limb. Lacquer upper arm (digits - the body), a GOLD bracer
        // down the forearm (electrum, uppercase - the joint), the white glove (lowercase - the
        // hand). The boot is still the glove's sibling at the far end of the other limb: white,
        // meeting gold - there as its own cuff, here as the bracer above it.
        //
        // THE GOLD IS MORE THAN TWO ROWS, the rule this glove's old cuff was built on: a two-texel
        // gold band reads as the outline having gone gold. The bracer is eight - and carries a
        // highlight band, because a flat block of electrum reads as khaki (the gorget's lesson).
        static readonly string[] ConclaveHarnessRows =
        {
                "..5555555555..",
                "..5555555555..",
                ".444444444444.",
                ".444444444444.",
                "44444444444444",
                "44444444444444",
                "44444444444444",
                "33333333333333",
                "33333333333333",
                ".222222222222.",
                ".LLLLLLLLLLLL.",
                ".HHHHHHHHHHHH.",
                ".BBBBBBBBBBBB.",
                ".BBBBBBBBBBBB.",
                ".BBBBBBBBBBBB.",
                ".BBBBBBBBBBBB.",
                "..DDDDDDDDDD..",
                "..DDDDDDDDDD..",
                "..SSSSSSSSSS..",
                "llllllllllllll",
                "llllllllllllll",
                "bbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbb",
                "dddddddddddddd",
                ".dddddddddddd.",
                "..dddddddddd..",
        };

        static readonly string[] ConclaveBootRows =
        {
                "..LLLLLLLLLLLL..",
                "..LLLLLLLLLLLL..",
                "BBBBBBBBBBBBBBBB",
                "BBBBBBBBBBBBBBBB",
                "llllllllllllllll",
                "llllllllllllllll",
                "bbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbb",
                "dddddddddddddddd",
                "dddddddddddddddd",
                "..dddddddddddd..",
                "..dddddddddddd..",
        };

        // 24 x 8, the torso's own full width, spanning y -18..-10 at its offset. The strip of
        // waist the gorget above does not reach, and nothing more - it is almost entirely covered
        // and has no business competing with the piece it sits under. One electrum keel at centre,
        // echoing the gorget's own groove so the two read as one item rather than two.
        static readonly string[] ConclaveBandRows =
        {
                "llllllllllllllllllllllll",
                "llllllllllllllllllllllll",
                "bbbbbbbbbbBBBBbbbbbbbbbb",
                "bbbbbbbbbbBBBBbbbbbbbbbb",
                "bbbbbbbbbbBBBBbbbbbbbbbb",
                "bbbbbbbbbbBBBBbbbbbbbbbb",
                "ddddddddddBBBBdddddddddd",
                "ddddddddddBBBBdddddddddd",
                "ddddddddddBBBBdddddddddd",
                "ddddddddddBBBBdddddddddd",
                "LLLLLLLLLLLLLLLLLLLLLLLL",
                "LLLLLLLLLLLLLLLLLLLLLLLL",
                "....dddddddddddddddd....",
                "....dddddddddddddddd....",
        };

        // ---- Seraph Mantle grid. See the item in Build() for the design reasoning. ----

        // 56 x 26 - the widest grid in the file, past the gorget's own 52. It spans x -28..+28
        // against a 24-wide torso and arms that occupy x 8..16, so a third of each wing hangs over
        // the arm and the tips clear the body entirely. That is the whole brief: the sketch's wings
        // jet out from the shoulders rather than sitting inside the silhouette.
        //
        // >>> THE HEAD IS WHY THIS GRID IS SHAPED THE WAY IT IS. READ THIS BEFORE MOVING ANY EDGE.
        //
        // The head is 32 x 28 spanning y 0..+28, and it draws ABOVE TorsoOver on every sorting
        // permutation - deliberately, see RigLayer.TorsoOver's own doc, because the chibi body has
        // no neck and the head sitting in front of a collar's top rows is what makes the head read
        // as set INTO it. The consequence for a piece this size is absolute: EVERY TEXEL ABOVE
        // y 0 AND INSIDE x +/-16 IS INVISIBLE ON THE BODY, however good it looks in isolation.
        //
        // The first pass ignored that and spanned y +10..-14. Nearly half the drawing - the neck V,
        // both scallop humps, every fang's root - sat in that dead band, so the grid previewed
        // beautifully and the character wore something else entirely: a thin silver wishbone with a
        // gold blob at the bottom. Nothing was wrong with the art; it was behind the chin.
        //
        // So the grid is now laid out against the chin line, not against the sketch's paper:
        //
        //   the COLLAR (x inward of 16) lives entirely between y 0 and y -18, the torso's own
        //   bottom - the top edge never rises above row 4, which is exactly y 0;
        //
        //   the WINGS are free to climb only once they pass x 16 and leave the head's width, which
        //   is why the top edge sits flat under the chin and then steps up four times in a row
        //   from column 22 outward. That step is not a stylistic choice, it is the head's edge.
        //
        // MIXED RESOLUTION, DELIBERATELY, and the one place in this file that does it. The COLLAR
        // and the SPIKES are block-doubled like every other armour grid here (they are big flat
        // shapes, and a block-double provably adds no detail - see CLAUDE.md); the MEDALLION and
        // the FANGS are authored at native 75ppu texels. Both have to be. The medallion is a circle
        // containing a triangle containing a tablet containing a pupil, and at 2x2 blocks that is a
        // five-cell icon holding four nested shapes. A fang has to narrow to a POINT, and a shape
        // two texels wide cannot taper at all. The same argument this project already makes for
        // weapons over armour (a thing looked at DIRECTLY needs the finer grid) covers both.
        //
        // WHERE EACH MATERIAL GOES, which is the whole identity of the piece and was wrong once:
        //
        //   SILVER is the centre triangle inside the medallion, and the hem along the bottom of the
        //   collar - which runs outward and becomes the SPIKES themselves. One continuous region,
        //   not two that happen to share a ramp: the spike is simply where the band has left the
        //   head (column 21 out) and the hem has become the whole of it.
        //
        //   GILT is the medallion's outer disc and the fangs. Nothing else, ever.
        //
        //   WHITE is everything left - the flat section of the collar the fangs hang into.
        //
        // The first pass had silver and white doing each other's jobs (a grey plate on top, a white
        // lip underneath) and it read as a generic pauldron rather than as this drawing.
        //
        // THE TWO EDGES:
        //
        //   The LOWER edge is one clean staircase from each tip to the crux - twelve rows over
        //   fourteen columns, so two steps have to be doubles, and both sit at the INNER end
        //   (runs of 2,2,1,1,1...), because run lengths on a staircase must shrink monotonically
        //   going outward or the edge reads as a wobble rather than a line. That is the lesson
        //   ConclaveGorgetRows paid for; it is restated here because the edge is twice as long.
        //
        //   The UPPER edge, read outward from the centre: a V NOTCH AT THE NECK cut four texels
        //   below the chin, the two humps flanking it sitting right ON the chin line, a valley, a
        //   second hump, and only then the climb out to the tip. The notch is the one feature that
        //   makes this shape individual rather than a generic chevron, and it was lost twice - once
        //   by climbing straight out from the centre, and once by hanging a medallion big enough to
        //   fill it. The medallion is sized by that notch (10 texels, hung low at the crux), not by
        //   how much detail would fit in a bigger one.
        //
        // FLAT FILLS, NOT A SHADED PLATE. The first pass shaded this the way every other grid in
        // the file is shaded - a lit top edge, base, a deep seam, a light lower lip, gilt blades
        // darkening to their tips - and it read as texture smeared over a drawing that was clean to
        // begin with. The sketch is flat colour inside heavy line, so the art is too: one tone per
        // material, and the auto-outline supplies all the line this piece has. The single exception
        // is one texel of gilt LIGHT down each fang's outward edge, which is what keeps a blade
        // three texels across from reading as a stripe at body scale.
        //
        // Every fang is rooted on the collar's own top edge in its column, outermost included, and
        // none may extend the silhouette - the builder refuses to paint a fang texel that is not
        // already collar, because a fang poking out past the edge it hangs from stops being a fang
        // under it.
        //
        // Drawn at BodyPpu since the reproportion, grid untouched - it is laid out against the
        // head, and the head is the same 32 x 28 at half its old size, so x +/-16 and "row 4 is
        // y 0" hold texel for texel at the new density. Hung by that rule: row 4's top on the chin.
        static float SeraphMantleY
            => ChinYCells + PrimitiveCharacterRig.Proportions.Cells(4)
             - PrimitiveCharacterRig.Proportions.Cells(SeraphMantleRows.Length) * 0.5f;

        static readonly string[] SeraphMantleRows =
        {
                "........................................................",
                "........................................................",
                "kkkk................................................kkkk",
                "kkkk................................................kkkk",
                "..bbkkkk........................................kkkkbb..",
                "..bbkkkk........................................kkkkbb..",
                "....bbkkkkkk................................kkkkkkbb....",
                "....bbkkkkkk................................kkkkkkbb....",
                "......bbkkkk..BBBh..hhhh........hhhh..BBBB..kkkkbb......",
                "......bbkkkk..BBBh..hhhh........hhhh..hBBB..kkkkbb......",
                "........bbbBkkBBBhBBBhhh........hhBBBBBBBBkkBbbb........",
                "........bbbBkkhBBhBBBhhh........hhhBBBhBBhkkBbbb........",
                "..........bbkkbBBhBBBhhhhhhhhhhhhhhBBBhBBbkkBb..........",
                "..........bbkkbBBhBBBhhhhhhhhhhhhhhBBBhBBbkkbb..........",
                "............bbbbBBhBBhhhhhBBBBhhhhhBBhBBbbbb............",
                "............bbbbBBhBBhhhBBBBBBBBhhhBBhBBbbbb............",
                "..............bbBBBBBhhBBBBBBBBBBhhBBbBbbb..............",
                "..............bbbbbbBBhBBBBbbBBBBhBBbbbbbb..............",
                "................bbbbBBBBBBBbbBBBBBBBbbbb................",
                "................bbbbBBBBBBb11bBBBBBBbbbb................",
                "..................bbBBBBBb1111bBBBBbbb..................",
                "..................bbbbBBBb1111bBBBbbbb..................",
                "....................bbbBbb1111bbBbbb....................",
                "....................bbbBbbb11bbbBbbb....................",
                "........................BBBBBBBB........................",
                "........................bbBBBBbb........................",
        };

        // 16 x 16, logical 8 x 8 - the leg's own full height, spanning root y -34..-18 at
        // offset -8. White thigh, a black band at the knee, gilt shin: the mantle's materials in
        // the order it wears them. Deliberately three flat blocks and nothing else - it is the
        // one piece of this costume that is FIELD rather than object, the same call
        // ConclaveLegRows makes, and every line drawn on it competes with the mantle.
        //
        // THE BANDS ARE PLACED AGAINST THE BOOT, NOT AGAINST THE GRID. The boot laps over the
        // lower leg (root -36..-28), so the bottom ten texels of this grid are never seen on the
        // body. A first pass split the grid evenly - white, band, gilt - and the whole leg came
        // out white with one gilt line showing, because the entire gilt section was under the
        // boot. Everything that has to READ sits in the top six texels; the gilt simply runs on
        // down behind the boot rather than stopping at it, so no seam can open up between them.
        // At BodyPpu one leg wide (PerLeg), and the THIGH LENGTHENED: this grid was the old leg's
        // full 16-cell height, and the leg is 24 now with a knee joint at 12 - at the old offset
        // the black band sat mid-thigh. The white runs on until the band ENDS on the knee (where
        // the rig cuts the leg); centred on it, the boot's outline left a single cell of gilt
        // showing where the original showed two. The gilt keeps its length.
        static string[] _seraphLegBodyRows;
        static string[] SeraphLegBodyRows => _seraphLegBodyRows ??= KneeBandOnKnee(PerLeg(SeraphLegRows), 'k');

        /// <summary>
        /// Repeat a leg grid's top row until the rows holding <paramref name="band"/> end on
        /// <see cref="PrimitiveCharacterRig.Proportions.KneeTexels"/>, counted from the grid's
        /// top edge - which must be on the hip (see <see cref="HipY"/>).
        /// </summary>
        static string[] KneeBandOnKnee(string[] rows, char band)
        {
            int top = System.Array.FindIndex(rows, r => r.IndexOf(band) >= 0);
            int depth = System.Array.FindLastIndex(rows, r => r.IndexOf(band) >= 0) - top + 1;
            int extra = PrimitiveCharacterRig.Proportions.KneeTexels - depth - top;
            if (extra <= 0) return rows;
            var outRows = new string[rows.Length + extra];
            for (int r = 0; r < extra; r++) outRows[r] = rows[0];
            System.Array.Copy(rows, 0, outRows, extra, rows.Length);
            return outRows;
        }

        /// <summary>Leg-local centre that hangs a grid's top edge on the hip.</summary>
        static float HipY(string[] rows) => -PrimitiveCharacterRig.Proportions.Cells(rows.Length) * 0.5f;

        static readonly string[] SeraphLegRows =
        {
                "hhhhhhhhhhhhhhhh",
                "hhhhhhhhhhhhhhhh",
                "hhhhhhhhhhhhhhhh",
                "hhhhhhhhhhhhhhhh",
                "kkkkkkkkkkkkkkkk",
                "kkkkkkkkkkkkkkkk",
                "BBBBBBBBBBBBBBBB",
                "BBBBBBBBBBBBBBBB",
                "BBBBBBBBBBBBBBBB",
                "BBBBBBBBBBBBBBBB",
                "BBBBBBBBBBBBBBBB",
                "BBBBBBBBBBBBBBBB",
                "BBBBBBBBBBBBBBBB",
                "BBBBBBBBBBBBBBBB",
                "BBBBBBBBBBBBBBBB",
                "BBBBBBBBBBBBBBBB",
        };

        // 28 x 8, at BodyPpu since the reproportion: authored at native 75 rather than
        // block-doubled, so halving it lands exactly on Silver's 28-texel strap and the one-texel
        // ring wall survives. Back on BeltY with every other belt: the mantle halved with the head
        // and its medallion now ends mid-chest, so the reason below for hanging it low is gone.
        // The paragraphs below are the original reasoning, written at the old density.
        //
        // 28 WIDE, NOT THE TORSO'S 24. A belt is drawn at the waist but what the eye measures it
        // against is whatever is WIDEST at that height, and here that is the leggings - 16 texels
        // per leg at +/-6, so +/-14 across. Built at 24 (the torso's own width, which is what
        // BeltRows and every other belt here uses) the strap stopped two texels inside the
        // trousers on each side and read as a strap laid on the body rather than one going round
        // it. Nothing about the belt was wrong; it was being compared to the wrong silhouette.
        //
        // LOWER THAN EVERY OTHER BELT IN THIS FILE, which sit at offset 2 (root -14..-18), and the
        // mantle is why. This set's chest piece hangs its medallion to y -17, so a belt at the
        // usual height is drawn UNDER it (Belt is order 14, TorsoOver 22) and simply does not
        // exist on the body - built at offset 2 first, and the buckle was invisible behind the
        // jewel. Dropped to the hip line it clears the crux and the buckle reads in full.
        //
        // The BUCKLE stands proud of the strap and it stands proud DOWNWARD - eight texels against
        // the strap's four, all four of the extra ones below it. Growing it upward is the obvious
        // move and it puts the ring straight back behind the medallion; growing it down is what
        // puts the one piece of this belt that has to be legible in clear air.
        //
        // The ring is a ONE-TEXEL wall around a four-texel hole. Two-texel walls were tried first
        // and at six texels across there is no hole left to speak of - it reads as a blue disc,
        // not an open circle. The hole shows the GILT of the buckle behind it rather than cutting
        // through to the body: a hole punched all the way through would put the character's own
        // hip in the middle of the buckle, which at this size reads as a mistake, not a ring.
        /// <summary>The strap (rows 1-4) centred on BeltY - one texel above the grid's centre,
        /// since the buckle stands proud downward.</summary>
        static float SeraphBeltY => BeltY - PrimitiveCharacterRig.Proportions.Cells(1);

        static readonly string[] SeraphBeltRows =
        {
                ".........BBBBBBBBBB.........",
                "bbbbbbbbbBBBB44BBBBbbbbbbbbb",
                "bbbbbbbbbBBB4BB4BBBbbbbbbbbb",
                "bbbbbbbbbBB4BBBB4BBbbbbbbbbb",
                "bbbbbbbbbBB4BBBB4BBbbbbbbbbb",
                ".........BBB4BB4BBB.........",
                ".........BBBB44BBBB.........",
                ".........BBBBBBBBBB.........",
        };

        // 36 x 40 at offset 16. Authored at NATIVE texel resolution like the mantle's medallion:
        // it is a dome, an ellipse and an arc, and none of the three survive a 2x2 block grid.
        //
        // TWO BLACK SHAPES, NOT ONE, and this is the whole reference. A large CIRCLE over the
        // crown and forehead, a separate VISOR band below it, and a gilt arc dividing them. The
        // first pass merged the two into a single ellipse with a pointed chin and it lost the
        // helmet completely: the circle is the feature the reference is recognisable BY, and a
        // visor on its own is a motorcycle helmet.
        //
        // THEY ARE CONCENTRIC, which is the part that took three passes to see. The visor does
        // not sit UNDER the circle as a straight band - it CURVES, and the top of its own gilt
        // trim is what shapes the circle's lower edge. Circle, arc and visor are rings about one
        // centre, so a single boundary reads as the circle's edge and the visor's edge at once.
        // Built as stacked shapes instead, the visor is a letterbox under a coin and the two
        // never belong to each other, however carefully each one is drawn.
        //
        // The gilt ARC is three texels and is structural, not trim. Without it the circle and the
        // visor close up into each other at this size and the two shapes become one again, which
        // is the exact failure it exists to prevent.
        //
        // THE VISOR'S TWO EDGES ARE NOT THE SAME CURVE, and this is the correction that finally
        // made it read. Its TOP follows the circle - that shared boundary is the whole reason the
        // arc is the edge of both shapes at once. Its BOTTOM is a STRAIGHT horizontal cut across
        // the face. Running the bottom concentric as well, which is what a third ring gives you,
        // produces a crescent of even thickness, and the reference's visor is nothing of the
        // sort: it is THIN at the centre, where the arc dips lowest, and FLARES at the temples,
        // where the arc has climbed away from the cut. That taper IS the shape.
        //
        // The outer ends slope inward as they rise rather than finishing on two vertical cuts.
        // Squared off, the visor reads as a black rectangle with a gilt smile on it.
        //
        // The ARC is limited by ANGLE, not by row. Clamping by row came first and wrapped it out
        // to the shell's edge: concentric rings reaching the sides are a bullseye, not a helmet.
        //
        // The circle is radius 8 rather than the 9.5 it started at: at 9.5 it touched the shell
        // top and sides and there was no white dome left for it to sit on. A feature that fills
        // its own field stops reading as a feature.
        //
        // One gilt ring at the bottom and nothing else. The reference carries three vents on the
        // temple; at this size each is a single texel, which is noise, not detail - the same call
        // the mantle's own notes make about shapes below three texels.
        static readonly string[] SeraphHelmRows =
        {
                "....................................",
                "....................................",
                "....................................",
                "...............hhhhhh...............",
                "............hhhhhhhhhhhh............",
                ".........hhhhhkkkkkkkkhhhhh.........",
                ".......hhhhhhkkkkkkkkkkhhhhhh.......",
                ".....hhhhhhhkkkkkkkkkkkkhhhhhhh.....",
                "....hhhhhhhkkkkkkkkkkkkkkhhhhhhh....",
                "...hhhhhhhhkkkkkkkkkkkkkkhhhhhhhh...",
                "..hhhhhhhhkkkkkkkkkkkkkkkkhhhhhhhh..",
                ".hhhhhhhhhkkkkkkkkkkkkkkkkhhhhhhhhh.",
                ".hhhhhhhhhkkkkkkkkkkkkkkkkhhhhhhhhh.",
                ".hhhhhhBBBkkkkkkkkkkkkkkkkBBBhhhhhh.",
                ".hhhhhhBBBkkkkkkkkkkkkkkkkBBBhhhhhh.",
                ".hhhhhhBBBBkkkkkkkkkkkkkkBBBBhhhhhh.",
                ".hhhhhhhBBBkkkkkkkkkkkkkkBBBhhhhhhh.",
                ".hhhhhhhBBBBkkkkkkkkkkkkBBBBhhhhhhh.",
                ".hhhhhhhhBBBBkkkkkkkkkkBBBBhhhhhhhh.",
                ".hhhhhhhhhBBBBkkkkkkkkBBBBhhhhhhhhh.",
                ".hhhhhhkkkBBBBBBBBBBBBBBBBkkkhhhhhh.",
                ".hhhhhkkkkkkBBBBBBBBBBBBkkkkkkhhhhh.",
                ".hhhhhkkkkkkkBBBBBBBBBBkkkkkkkhhhhh.",
                ".hhhhkkkkkkkkkkkkkkkkkkkkkkkkkkhhhh.",
                ".hhhhkkkkkkkkkkkkkkkkkkkkkkkkkkhhhh.",
                ".hhhkkkkkkkkkkkkkkkkkkkkkkkkkkkkhhh.",
                ".hhhkkkkkkkkkkkkkkkkkkkkkkkkkkkkhhh.",
                ".hhhkkkkkkkkkkkkkkkkkkkkkkkkkkkkhhh.",
                ".hhhBBBBBBBBBBBBBBBBBBBBBBBBBBBBhhh.",
                ".hhhBBBBBBBBBBBBBBBBBBBBBBBBBBBBhhh.",
                ".hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh.",
                "..hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh..",
                "..hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh..",
                "...hhhhhhhhhhhhhhhhhhhhhhhhhhhhhh...",
                "....hhhhhhhhhhhhhhhhhhhhhhhhhhhh....",
                ".....hhhhhhhhhhhhhhhhhhhhhhhhhh.....",
                ".....hBBBBBBBBBBBBBBBBBBBBBBBBh.....",
                "......hBBBBBBBBBBBBBBBBBBBBBBh......",
                ".......hhhhhhhhhhhhhhhhhhhhhh.......",
                ".......hhhhhhhhhhhhhhhhhhhhhh.......",
        };

        // 14 x 28 at BodyPpu - the WHOLE ARM, on GloveRows' own frame, in the LEG's order
        // (SeraphLegRows): white upper arm, the black band at the elbow as at the knee, a gilt
        // forearm as the shin. The hand is the old glove, which was the boot's own grid with the
        // sole rows off: its black cuff and white glove. The white is flat, like the leg - this
        // costume's limbs are FIELD, and a line drawn on them competes with the mantle.
        //
        // THE GILT IS SHADED, where the leg's is flat: a lit rim, a highlight, a shaded foot. Flat,
        // this gilt between a white sleeve and a white glove read as a BARE FOREARM - it is close
        // to a skin tone, and metal only reads as metal by its highlights. The shin never showed
        // it, being almost all under the boot.
        static readonly string[] SeraphHarnessRows =
        {
                "..hhhhhhhhhh..",
                "..hhhhhhhhhh..",
                ".hhhhhhhhhhhh.",
                ".hhhhhhhhhhhh.",
                "hhhhhhhhhhhhhh",
                "hhhhhhhhhhhhhh",
                "hhhhhhhhhhhhhh",
                "hhhhhhhhhhhhhh",
                "kkkkkkkkkkkkkk",
                ".kkkkkkkkkkkk.",
                ".LLLLLLLLLLLL.",
                ".HHHHHHHHHHHH.",
                ".BBBBBBBBBBBB.",
                ".BBBBBBBBBBBB.",
                ".BBBBBBBBBBBB.",
                ".DDDDDDDDDDDD.",
                "..DDDDDDDDDD..",
                "..SSSSSSSSSS..",
                "..kkkkkkkkkk..",
                "kkkkkkkkkkkkkk",
                "hhhhhhhhhhhhhh",
                "hhhhhhhhhhhhhh",
                "hhhhhhhhhhhhhh",
                "hhhhhhhhhhhhhh",
                "hhhhhhhhhhhhhh",
                "hhhhhhhhhhhhhh",
                ".hhhhhhhhhhhh.",
                "..hhhhhhhhhh..",
        };

        // 16 x 8 at offset -14 - the same footprint every other boot in this file uses
        // (BootRows is 8 texels tall at this same offset, BlackSteelBootRows 10), rather than a
        // size invented for this set. An earlier pass shortened it to 6 to stop it covering the
        // gilt shin; that fixed the shin by making the character wear a boot two texels shorter
        // than everyone else's, which is the wrong lever. The SHIN moved up instead - see the
        // legging's own note.
        //
        // A black cuff of TWO logical rows rather than one, for the reason the Conclave gauntlet's
        // gold band already documents: one row is two real texels, and a two-texel line at the top
        // of a piece this small reads as the auto-outline having changed colour, not as a cuff.
        static readonly string[] SeraphBootRows =
        {
                "kkkkkkkkkkkkkkkk",
                "kkkkkkkkkkkkkkkk",
                "hhhhhhhhhhhhhhhh",
                "hhhhhhhhhhhhhhhh",
                "hhhhhhhhhhhhhhhh",
                "hhhhhhhhhhhhhhhh",
                "..hhhhhhhhhhhh..",
                "..hhhhhhhhhhhh..",
        };

        // ---- Tide Ward Circlet ----
        //
        // The one survivor of a wider Water tabard set (tabard, legguards, spiral gloves) that
        // didn't read well and was scrapped.

        // 14 x 3. A black cloth band, not a domed helm - a small metal plaque (second material)
        // holds the glass gem (third material) at centre front. The knot and its tails are
        // GearItem.HasTieBack's job (painted onto RigLayer.HeadBack, visible only from behind) -
        // this front sprite has nothing true to say about a tie's back, same reasoning HasHood
        // already applies to a cowl.
        static readonly string[] HeadbandRows =
        {
                "..llllllllllllllllllllllll..",
                "..llllllllllllllllllllllll..",
                "bbbbbbbbBBBB4455BBBBbbbbbbbb",
                "bbbbbbbbBBBB4455BBBBbbbbbbbb",
                "....dddddddddddddddddddd....",
                "....dddddddddddddddddddd....",
        };

        // ---- Verdant Warden grids: tempered plate + a gold tree-trunk/leaf trim ----
        //
        // Two materials, Palette.Of(main, second)'s convention: lowercase steel, uppercase gold.

        // 16 x 9, VanguardPauldronRows' own footprint. A steel dome, load-bearing on its own, with
        // gold vine trim wrapped around it (rows 5-6) and a spray of gold leaves at the crown
        // (rows 0-2) and in the gap between the lower lames (rows 7-8) - foliage the whole way
        // down the plate, never just a crest sitting on top of a bare shoulder.
        static readonly string[] VerdantPauldronRows =
        {
                "..H...hhhh...H..",
                ".HH..llllll..HH.",
                "LLllllllllllllLL",
                "LbbbbbbbbbbbbbbL",
                "bbbbbbbbbbbbbbbb",
                "ddddBBBBBBBBdddd",
                "ddddDDDDDDDDdddd",
                ".dddd..HH..dddd.",
                "..ssss.BB.ssss..",
        };

        // 10 x 9, CuirassRows' own footprint. The shared centre seam becomes a gold trunk: a wide
        // band across the collarbone yoke (the fork's source) tapers through the lit/base rows
        // into a two-texel spine, sprouts one pair of leaf sprigs at the dark band, and ends as a
        // root tip just above the fauld - the same trunk the pauldrons' vine trim continues onto.
        static readonly string[] VerdantCuirassRows =
        {
                "..hhHHHHHHHHHHHHhh..",
                "..hhHHHHHHHHHHHHhh..",
                "hhllLLLLLLLLLLLLllhh",
                "hhllLLLLLLLLLLLLllhh",
                "bbbbbbBBBBBBBBbbbbbb",
                "bbbbbbBBBBBBBBbbbbbb",
                "bbbbbbbbBBBBbbbbbbbb",
                "bbbbbbbbBBBBbbbbbbbb",
                "ddddddddBBBBdddddddd",
                "ddddddddBBBBdddddddd",
                "ddddHHddBBBBddHHdddd",
                "ddddHHddBBBBddHHdddd",
                "ddddddddBBBBdddddddd",
                "ddddddddBBBBdddddddd",
                "..ssssssBBBBssssss..",
                "..ssssssBBBBssssss..",
                "....ssssssssssss....",
                "....ssssssssssss....",
        };

        // ---- Vanguard grids: gold-trimmed dark steel + bone horns + a crimson cloth sash ----
        //
        // See the "Vanguard" block in Build() for the design reasoning. Three-material grids use
        // digits for the third (bone) material, uppercase for the second (gold trim), lowercase
        // for the first (steel) - Palette.Of(main, second, third)'s own convention.

        // 16 x 13. Horns rise off the top three rows and taper into the dome; a lit gold browband
        // breaks the steel; cheek guards flare a texel past the jaw on both edges, widening the
        // silhouette outward exactly where Black Steel's own helm stays flush with the skull.
        static readonly string[] VanguardHelmRows =
        {
                "..55........................55..",
                "..55........................55..",
                "..44........................44..",
                "..44........................44..",
                "....44....................44....",
                "....44....................44....",
                "....33....................33....",
                "....33....................33....",
                "......33hhhhhhhhhhhhhhhh33......",
                "......33hhhhhhhhhhhhhhhh33......",
                "....llllllllllllllllllllllll....",
                "....llllllllllllllllllllllll....",
                "..bbbbbbHHHHHHHHHHHHHHHHbbbbbb..",
                "..bbbbbbHHHHHHHHHHHHHHHHbbbbbb..",
                "..bbbbbbLLLLLLLLLLLLLLLLbbbbbb..",
                "..bbbbbbLLLLLLLLLLLLLLLLbbbbbb..",
                "..bbbbbbbbbbbbbbbbbbbbbbbbbbbb..",
                "..bbbbbbbbbbbbbbbbbbbbbbbbbbbb..",
                "..dddddddddddddddddddddddddddd..",
                "..dddddddddddddddddddddddddddd..",
                "dddddddd................dddddddd",
                "dddddddd................dddddddd",
                "ssssssss................ssssssss",
                "ssssssss................ssssssss",
                "ssssss....................ssssss",
                "ssssss....................ssssss",
        };

        // 16 x 9. A crowned spike rather than a plain dome - the top row alone extends past
        // Black Steel's own pauldron silhouette - with a gold trim band across the front face and
        // a tapered underside so the shoulder still reads as capped rather than boxy.
        static readonly string[] VanguardPauldronRows =
        {
                "......hhhh......",
                "....hhhhhhhh....",
                "..llllllllllll..",
                ".bbbbbbbbbbbbbb.",
                "bbbbbbbbbbbbbbbb",
                "ddddBBBBBBBBdddd",
                "ddddDDDDDDDDdddd",
                ".dddd......dddd.",
                "..ssss....ssss..",
        };

        // 20 x 14. A lit gold collar line across the collarbones, a raised gold sternum bar down
        // the centre, and flared fauld plates at the waist that leave a gap for the sash beneath
        // to show through - the reference's centre panel and layered skirt, in three materials.
        //
        // Drawn UNDOUBLED, then Reproportioned: authored at 75 and block-doubled again with the
        // 37.5-era grids, it stood twice the width of every other cuirass - a barrel past both
        // arms. At the standard cuirass's envelope the set's own helm and pauldrons (already at
        // BodyPpu) finally match it. Not one of the pieces oversized on purpose.
        static readonly string[] VanguardCuirassRows =
        {
                "........hhhhhhhhhhhhhhhhhhhhhhhh........",
                "........hhhhhhhhhhhhhhhhhhhhhhhh........",
                "....llllllllllllllllllllllllllllllll....",
                "....llllllllllllllllllllllllllllllll....",
                "..bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb..",
                "..bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb..",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbHHHHHHHHHHHHHHHHbbbbbbbbbbbb",
                "bbbbbbbbbbbbHHHHHHHHHHHHHHHHbbbbbbbbbbbb",
                "ddddddddddddLLLLLLLLLLLLLLLLdddddddddddd",
                "ddddddddddddLLLLLLLLLLLLLLLLdddddddddddd",
                "ddddddddddddddddddBBBBdddddddddddddddddd",
                "ddddddddddddddddddBBBBdddddddddddddddddd",
                "ddddddddddddddddddBBBBdddddddddddddddddd",
                "ddddddddddddddddddBBBBdddddddddddddddddd",
                "ddddddddddddddddddBBBBdddddddddddddddddd",
                "ddddddddddddddddddBBBBdddddddddddddddddd",
                "ssssssssssssssssssDDDDssssssssssssssssss",
                "ssssssssssssssssssDDDDssssssssssssssssss",
                "..ssssssssssssssBBBBBBBBssssssssssssss..",
                "..ssssssssssssssBBBBBBBBssssssssssssss..",
                "....ssssssssssssssssssssssssssssssss....",
                "....ssssssssssssssssssssssssssssssss....",
                "dddddddddddddd............dddddddddddddd",
                "dddddddddddddd............dddddddddddddd",
                "ssssssssssss................ssssssssssss",
                "ssssssssssss................ssssssssssss",
        };

        // 10 x 9. The crimson sash - single ramp, no trim material - with a scalloped three-point
        // hem so the bottom edge breaks into a shape rather than reading as a cut rectangle.
        //
        // Block-doubled to 20 x 18 and drawn at BodyPpu, so a design texel is the amulet's 2x2.
        // Hung from the belt line: its three-tone waistband (the top six rows) sits on BeltY
        // where every other belt's strap does, and the panel falls from there.
        static float VanguardSashY
            => BeltY + PrimitiveCharacterRig.Proportions.Cells(6) * 0.5f
             - PrimitiveCharacterRig.Proportions.Cells(VanguardTassetRows.Length) * 0.5f;

        static readonly string[] VanguardTassetRows =
        {
                "..hhhhhhhhhhhhhhhh..",
                "..hhhhhhhhhhhhhhhh..",
                "..llllllllllllllll..",
                "..llllllllllllllll..",
                "..bbbbbbbbbbbbbbbb..",
                "..bbbbbbbbbbbbbbbb..",
                "..ddbbbbbbbbbbbbdd..",
                "..ddbbbbbbbbbbbbdd..",
                "..ddbbbbbbbbbbbbdd..",
                "..ddbbbbbbbbbbbbdd..",
                "..dddddddddddddddd..",
                "..dddddddddddddddd..",
                "....dddddddddddd....",
                "....dddddddddddd....",
                "..dddd..dddd..dddd..",
                "..dddd..dddd..dddd..",
                "..ss....ssss..ss....",
                "..ss....ssss..ss....",
        };

        // ---- Wraithguard grids: a masked full-face helm, an asymmetric shoulder pair, and a
        // sashed cuirass. See the "Wraithguard" block in Build() for the design reasoning.

        // 16 x 18 - MEASURED against the bare head's own baked texture, not guessed at. Dumping
        // Head's sprite as a character grid (same ASCII technique this file's other sections use to
        // check a bake rather than trust the source) put the head at exactly 16x18 texels: two rows
        // of headroom, then hair from row 2 to row 8 (seven rows), then skin, with the EYES at rows
        // 10-11 - roughly six tenths of the way down the whole head, nowhere near the hairline. Two
        // earlier passes placed the T-opening by eye (by feel, not by measurement) and both were
        // wrong in the same direction: the first put the crossbar in the hair, which read back as
        // "the opening shows the hairline" - literally correct, that is what it was doing.
        //
        // SIZED TO THE HEAD'S OWN 16x18, not wider or taller. An earlier 18-wide pass eliminated
        // the cheek skin it was chasing by overshooting the actual head into a slab; a later 20-row
        // pass eliminated the chin skin by running the mask onto the neck and shoulders. Matching
        // the head's own measured footprint fixes both at once without repeating either mistake.
        //
        // A T-SHAPED OPENING, not a slit: a wide crossbar at the eye rows so both eyes show at its
        // ends, and a stem hanging from its centre down over the nose - the one place on a masked
        // face a T-opening is actually named for. Below the stem the mask closes solid again and
        // tapers to the chin: the opening is the eyes and the nose bridge, not the whole lower face.
        //
        // THE FIRST CUT OF THIS SHAPE ERODED SHUT, and it is the exact trap this file's own
        // old hood grid and Shadow's-fuller notes documented: a one-row-tall opening bordered by
        // opaque above and below is eaten from both sides in the same auto-outline pass and
        // survives as nothing. The crossbar is FOUR raw rows so the middle TWO survive - matching
        // the eyes' own two-row height exactly - and the stem is three raw rows so two survive
        // (the top one takes no vertical erosion at all, since it borders the crossbar's own open
        // cells rather than solid steel) before the last one closes into the resumed jaw. Horizontal
        // erosion costs the same 1 texel a side, so both openings are cut 2 texels wider than the
        // gap meant to actually show.
        //
        // The offset below is solved from the dump, not eyeballed a third time: it places THIS
        // grid's rows 9-10 (the crossbar's surviving pair) exactly on the head's own measured
        // rows 10-11 - eye row for eye row - with the rest of the mask's rows numbered to match.
        static readonly string[] WraithHelmRows =
        {
                "..........hhhhhhhhhhhh..........",
                "..........hhhhhhhhhhhh..........",
                "......hhhhllllllllllllhhhh......",
                "......hhhhllllllllllllhhhh......",
                "....llbbbbbbbbbbbbbbbbbbbbll....",
                "....llbbbbbbbbbbbbbbbbbbbbll....",
                "....bbbbbbbbbbbbbbbbbbbbbbbb....",
                "....bbbbbbbbbbbbbbbbbbbbbbbb....",
                "....bbbbbbbbbbbbbbbbbbbbbbbb....",
                "....bbbbbbbbbbbbbbbbbbbbbbbb....",
                "....bbbbbbbbbbbbbbbbbbbbbbbb....",
                "....bbbbbbbbbbbbbbbbbbbbbbbb....",
                "....bbbbbbbbbbbbbbbbbbbbbbbb....",
                "....bbbbbbbbbbbbbbbbbbbbbbbb....",
                "....bbbbbbbbbbbbbbbbbbbbbbbb....",
                "....bbbbbbbbbbbbbbbbbbbbbbbb....",
                "..bb........................bb..",
                "..bb........................bb..",
                "..bb........................bb..",
                "..bb........................bb..",
                "..bb........................bb..",
                "..bb........................bb..",
                "..bb........................bb..",
                "..bb........................bb..",
                "bbbbbbbbbbbb........bbbbbbbbbbbb",
                "bbbbbbbbbbbb........bbbbbbbbbbbb",
                "bbbbbbbbbbbb........bbbbbbbbbbbb",
                "bbbbbbbbbbbb........bbbbbbbbbbbb",
                "bbbbbbbbbbbb........bbbbbbbbbbbb",
                "bbbbbbbbbbbb........bbbbbbbbbbbb",
                "..bbbbbbbbbbbbbbbbbbbbbbbbbbbb..",
                "..bbbbbbbbbbbbbbbbbbbbbbbbbbbb..",
                "....dddddddddddddddddddddddd....",
                "....dddddddddddddddddddddddd....",
                "........ssssssssssssssss........",
                "........ssssssssssssssss........",
        };

        // 10 x 6. The ARMOURED shoulder, widened from a first, plate-sized-to-the-joint pass that
        // left a sliver of arm showing past its own edge - per the current design position that
        // armour need not hug the body it sits on, the fix is a bigger cap, not a narrower gap.
        static readonly string[] WraithPauldronRows =
        {
                "......hhhhhhhh......",
                "......hhhhhhhh......",
                "..hhhhllllllllhhhh..",
                "..hhhhllllllllhhhh..",
                "llbbbbbbbbbbbbbbbbll",
                "llbbbbbbbbbbbbbbbbll",
                "bbbbbbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbbbbbb",
                "..dddddddddddddddd..",
                "..dddddddddddddddd..",
                "....dddddddddddd....",
                "....dddddddddddd....",
        };

        // 8 x 6 authored, drawn block-doubled at BodyPpu. wraith_pauldron's -X piece - a rounded
        // cap with a ragged hem, kept as that SHAPE in steel. It was drawn as crimson cloth once;
        // the cloth moved to wraith_cloak's own drape (DemoGear.WraithCloak.cs), which now covers it.
        static readonly string[] WraithMantleRows =
        {
                "....llllllll....",
                "....llllllll....",
                "..llbbbbbbbbll..",
                "..llbbbbbbbbll..",
                "bbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbb",
                "dddddddddddddddd",
                "dddddddddddddddd",
                "..ddssssssssdd..",
                "..ddssssssssdd..",
                "....ssss....ssss",
                "....ssss....ssss",
        };

        // 10 x 8. CuirassRows, with the shared dark centre seam and the fauld seam both swapped for
        // a crimson sash band - the torso's own answer to "fabric flowing from the belt" (see the
        // Wraithguard block for why this stays out of the Belt slot).
        static readonly string[] WraithCuirassRows =
        {
                "..hhhhhhhhhhhhhhhh..",
                "..hhhhhhhhhhhhhhhh..",
                "hhllllllllllllllllhh",
                "hhllllllllllllllllhh",
                "bbbbbbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbbbbbb",
                "bbbbbbbbDDDDbbbbbbbb",
                "bbbbbbbbDDDDbbbbbbbb",
                "dddddddddddddddddddd",
                "dddddddddddddddddddd",
                "..BBBBBBBBBBBBBBBB..",
                "..BBBBBBBBBBBBBBBB..",
                "dddddddddddddddddddd",
                "dddddddddddddddddddd",
                "....ssssssssssss....",
                "....ssssssssssss....",
        };

        // 28 x 34. Solid cloth, flared at the hem.
        //
        // THE SHOULDERS SLOPE. The top edge is full height only across the collar (x +/-5, behind
        // the head and hood) and steps down a row per column past it, to y 21 - the body's own
        // shoulder line - at the outer edge. It used to be a flat, full-width edge at y 25: hidden
        // by the head from the front, but from behind its square corners read as shoulders three
        // cells above the body's, and every pauldron beside them looked like it had slipped down
        // the arm. The lit and base bands follow the slope, so the rim reads as light on a
        // shoulder rather than a straight stripe. Same 34 rows, so offY and the hinge are untouched.
        //
        // NO GLOW ON THE COLLAR - a real correction, not a tint. This project's own note on why
        // gear moved to 37.5 ppu says outright that the six-tone ramp (a glow ABOVE the light,
        // a deep BELOW the dark) "is what made plate read as metal instead of a flat block" -
        // which means a piece that wants to read as cloth has to stay off the glow end of its own
        // ramp, not just wear a duller colour. The old collar led with 'h', a tight specular point
        // exactly like a polished helm's crown; it's 'l' now; nothing here uses 'h' at all.
        //
        // NO CREASE MARKS. It had a fold first as one straight "ss" crease (a seam, not a drape),
        // then as short dashes wandering down the body - which, seen from behind where the cape
        // is the whole picture, read as a scatter of contrasting squares rather than folds. The
        // body is one flat tone now, shaded only top to bottom; every cloak that uses this grid
        // (black_hood, diamond_cloak) and the poncho's back follow the same rule; the Wraithguard
        // Cloak has its own cape now, with broad fold PLANES rather than crease marks.
        static readonly string[] CapeRows =
        {
                "......llllllllll......",
                ".....llllllllllll.....",
                "....llllllllllllll....",
                "...lllbbbbbbbbbblll...",
                "..lllbbbbbbbbbbbblll..",
                "..llbbddddddddddbbll..",
                "..lbbddddddddddddbbl..",
                "..bbddddddddddddddbb..",
                "..bddddddddddddddddb..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..dddddddddddddddddd..",
                "..sdddddddddddddddds..",
                "..sdddddddddddddddds..",
                "..ssssssssssssssssss..",
                "..ssssssssssssssssss..",
                ".....ssssssssssss.....",
                ".....ssssssssssss.....",
        };

        // 58 x 44 at BodyPpu. The poncho's BACK - the same cloth as PonchoYokeRows seen from the
        // other side of the body, so it is exactly the yoke's width with the yoke's own shoulder
        // curve on top: where the yoke's hem runs out past the arm, this carries on straight
        // down to the hip and the two read as one garment rather than a yoke on a box.
        //
        // It was 42 x 24 at GearPpu - half again wider than the yoke and on a different grid -
        // which drew a second, bigger silhouette behind it with its own lit top band.
        //
        // The top rows sit behind the yoke on every front-facing stack and the body covers the
        // middle, so what actually shows is the two side panels below the yoke and the hem. No
        // crease dashes - from behind they read as scattered squares, not folds (CapeRows
        // dropped its own for the same reason). outline: false, like every
        // poncho layer.
        static readonly string[] PonchoBackRows =
        {
                ".........llllllllllllllllllllllllllllllllllllllll.........",
                ".......llllllllllllllllllllllllllllllllllllllllllll.......",
                ".....llllllllllllllllllllllllllllllllllllllllllllllll.....",
                "...llllllbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbllllll...",
                "..lllllbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbblllll..",
                ".llllbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbllll.",
                "lllbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbblll",
                "llbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbll",
                "lbbbbbbbbddddddddddddddddddddddddddddddddddddddddbbbbbbbbl",
                "bbbbbbbddddddddddddddddddddddddddddddddddddddddddddbbbbbbb",
                "bbbbbddddddddddddddddddddddddddddddddddddddddddddddddbbbbb",
                "bbbddddddddddddddddddddddddddddddddddddddddddddddddddddbbb",
                "bbddddddddddddddddddddddddddddddddddddddddddddddddddddddbb",
                "bddddddddddddddddddddddddddddddddddddddddddddddddddddddddb",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "ssddddddddddddddddddddddddddddddddddddddddddddddddddddddss",
                "ssssssssssssssssssssssssssssssssssssssssssssssssssssssssss",
                "..ssssssssssssssssssssssssssssssssssssssssssssssssssssss..",
        };

        // 58 x 30 at BodyPpu. wanderers_hood's front YOKE - see RigLayer.BackOver's own doc.
        //
        // SIZED OFF THE PAULDRONS IT COVERS, not off the torso. Every shoulder piece is placed
        // from the same two anchors (ShoulderX, PlateY), and the widest of them - gold, 18 texels
        // plus a texel of outline either side - reaches 25 texels out from the centre line and
        // 16 below the chin. 58 wide puts the yoke's edge four texels past that, and the hem
        // at the shoulder tips (row 22, eleven cells under the top) is below the deepest one.
        // A pauldron grown past that envelope will poke out; that is the envelope to grow with it.
        //
        // The hem is a V to a point on the chest - "part of the chest", not a bib - and the two
        // creases fall from the inside of each shoulder, where the cloth breaks over the plate.
        // Top rows are under the head and hood on every permutation, so only the shoulder
        // curves of the lit band ever show. outline: false like the rest of the poncho; the
        // yoke is FILL all the way to its edge for the same reason (see wanderers_hood).
        static readonly string[] PonchoYokeRows =
        {
                ".........llllllllllllllllllllllllllllllllllllllll.........",
                ".......llllllllllllllllllllllllllllllllllllllllllll.......",
                ".....llllllllllllllllllllllllllllllllllllllllllllllll.....",
                "...llllllbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbllllll...",
                "..lllllbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbblllll..",
                ".llllbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbllll.",
                "lllbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbblll",
                "llbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbll",
                "lbbbbbbbbddddddddddssddddddddddddddddssddddddddddbbbbbbbbl",
                "bbbbbbbddddddddddddssddddddddddddddddssddddddddddddbbbbbbb",
                "bbbbbddddddddddddddssddddddddddddddddssddddddddddddddbbbbb",
                "bbbddddddddddddddddssddddddddddddddddssddddddddddddddddbbb",
                "bbdddddddddddddddddssddddddddddddddddssdddddddddddddddddbb",
                "bddddddddddddddddddssddddddddddddddddssddddddddddddddddddb",
                "dddddddddddddddddddssddddddddddddddddssddddddddddddddddddd",
                "dddddddddddddddddddssddddddddddddddddssddddddddddddddddddd",
                "dddddddddddddddddddssddddddddddddddddssddddddddddddddddddd",
                "dddddddddddddddddddssddddddddddddddddssddddddddddddddddddd",
                "dddddddddddddddddddssddddddddddddddddssddddddddddddddddddd",
                "dddddddddddddddddddssddddddddddddddddssddddddddddddddddddd",
                "dddddddddddddddddddssddddddddddddddddssddddddddddddddddddd",
                "dddddddddddddddddddssddddddddddddddddssddddddddddddddddddd",
                "dddddddddddddddddddssddddddddddddddddssddddddddddddddddddd",
                "...dddddddsddddddddssddddddddddddddddssddddddddsddddddd...",
                ".......dddsssssddddddddddddddddddddddddddddsssssddd.......",
                "...........ssssssssddddddddddddddddddddssssssss...........",
                "...............ssssssssddddddddddddssssssss...............",
                "...................ssssssssddddssssssss...................",
                ".......................ssssssssssss.......................",
                "...........................ssss...........................",
        };

        // 12 x 6. Chain to a gem (second material). About the honest limit of what reads on the chest.
        static readonly string[] AmuletRows =
        {
                ".D........D.",
                "..D......D..",
                "...D....D...",
                "...DD..DD...",
                "....HBBH....",
                ".....BB.....",
        };

        /// <summary>Width of the scarf's grid, in texels. Even, for the mirror.</summary>
        const int ScarfWidth = 10;

        /// <summary>
        /// Rows spent trailing below the collar band. 18 at BodyPpu is the WORLD length the old 9
        /// had at GearPpu: the reproportion narrowed the body but kept its height, so the tail's
        /// width halves with the collar's and its length does not. Halved too, it never cleared
        /// the torso even at the full swing.
        /// </summary>
        const int ScarfTailRows = 18;

        static char[] ScarfBlankRow()
        {
            var line = new char[ScarfWidth];
            for (int x = 0; x < ScarfWidth; x++) line[x] = '.';
            return line;
        }

        static string ScarfSpan(int center, int half, char left, char mid, char right)
        {
            var line = ScarfBlankRow();
            int from = Mathf.Clamp(center - half, 0, ScarfWidth - 1);
            int to = Mathf.Clamp(center + half, 0, ScarfWidth - 1);
            for (int x = from; x <= to; x++)
                line[x] = x == from ? left : x == to ? right : mid;
            return new string(line);
        }

        /// <summary>
        /// The wound part, on RigLayer.Neck - small, front, and it never moves. A scarf's knot
        /// sits against the neck whatever the tail is doing, the same reason a cape's own hinge
        /// sits at the TOP edge rather than spinning the sprite about its own centre.
        /// </summary>
        static string[] BuildScarfCollar()
        {
            const int collarCenter = 4;
            return new[]
            {
                ScarfSpan(collarCenter, 4, 'h', 'h', 'h'),   // glow
                ScarfSpan(collarCenter, 4, 'l', 'l', 'l'),   // light
                ScarfSpan(collarCenter, 3, 'b', 'b', 'b'),   // base - the knot
            };
        }

        // Block-doubled rather than re-derived at the doubled ScarfWidth/collarCenter constants -
        // the procedural math above is untouched and still produces the old-resolution grid;
        // PixelDetail.Upscale(_, 2) is the exact same "each texel becomes a 2x2 block" transform
        // used everywhere else in this pass, applied here instead of by hand so the geometry
        // constants above never have to be re-derived.
        static readonly string[] ScarfCollarRows = PixelDetail.Upscale(BuildScarfCollar(), 2);

        /// <summary>
        /// The hanging part, on RigLayer.NeckBack - BEHIND the torso, so the body hides most of
        /// it at rest and a run or a turn is what swings it into view. It narrows and drifts to
        /// one side as it falls, the same "give it a curve or the word does no work" instinct
        /// behind every other flowing shape in this file (the Rift Blade's tear, Lumen's ribbon):
        /// AnimateScarf spins this whole sprite about the collar every frame, so a tail that
        /// already reads as a straight, static taper would look worse once it started moving, not
        /// better - the drift is what sells "cloth" before the spring ever touches it.
        ///
        /// SHORT ON PURPOSE. A tail sized like the cape's ran well past the torso's bottom edge and
        /// kept reappearing below the boots - the very obstruction this was built to fix, just
        /// moved a few texels down. About the torso's own length keeps the whole thing inside its
        /// footprint at rest, with only the swing carrying it out past the silhouette.
        ///
        /// The drift only ever grows POSITIVE (never negative), and the item's own offset leans
        /// the same way - RigLayer.LegFront/BootsFront draw in FRONT of NeckBack and sit in that
        /// same positive-x column, so the sliver that does fall past the torso's bottom edge is
        /// still caught by the near leg rather than hanging in open air beside it.
        /// </summary>
        static string[] BuildScarfTail()
        {
            var rows = new List<string>();
            const int collarCenter = 4;

            for (int i = 0; i < ScarfTailRows; i++)
            {
                float t = i / (float)(ScarfTailRows - 1);
                // A SINGLE curve, not a wobble - an early pass added a sine ripple on top of the
                // drift and it read as a jagged notch rather than as cloth, because a wiggle needs
                // more texels than a 10-wide tail has to spend before it reads as motion rather
                // than as an error. The swing itself is what supplies the motion; the shape only
                // has to curve to one side.
                int half = Mathf.Max(0, Mathf.RoundToInt(Mathf.Lerp(3f, 0f, Mathf.Pow(t, 1.4f))));
                int drift = Mathf.RoundToInt(Mathf.Lerp(0f, 3f, t * t));
                char tone = t < 0.65f ? 'd' : 's';
                rows.Add(ScarfSpan(collarCenter + drift, half, tone, tone, tone));
            }
            return rows.ToArray();
        }

        // See ScarfCollarRows' own note: block-doubled after the fact rather than re-deriving the
        // drift/taper math at 2x.
        static readonly string[] ScarfTailRowsData = PixelDetail.Upscale(BuildScarfTail(), 2);

        // At BODY density, with the 2x2 blocks kept - the amulet's own pixel size, so the two neck
        // pieces are drawn at one density. At GearPpu the collar was 0.267 units across on a torso
        // that is now 0.173, and stood at mid-chest on a literal offset the body had moved away
        // from. Placed off the chin like the pauldrons: the collar's top edge on it.

        /// <summary>The collar's centre, torso-local cells: its top edge on the chin.</summary>
        static float ScarfCollarY => PlateY(ScarfCollarRows);

        /// <summary>
        /// The tail's centre: toward the near leg (see BuildScarfTail), its top - the swing's
        /// hinge - tucked behind the torso half the old distance under the collar.
        /// </summary>
        const float ScarfTailX = 2f;
        static float ScarfTailY
            => ScarfCollarY - 3.5f - PrimitiveCharacterRig.Proportions.Cells(ScarfTailRowsData.Length) * 0.5f;

        // 4 x 2. A gem on the finger; auto-outline gives it its band.
        static readonly string[] RingRows =
        {
                ".LL.",
                ".BB.",
        };

        // 12 x 10. Knee cop over a shin panel: glow rim, base, dark, deep to the ankle.
                // 14 x 20 at Proportions.BodyPpu (150) - a TASSET AND THIGH PLATE, where this grid was a
        // narrow shin tube running most of the leg's length.
        //
        // Restructured against the reference rather than re-tuned. The leg armour there is two
        // masses, not one: a broad plate hanging straight off the belt over the hips and thighs,
        // and a tall boot coming up to meet it (see BootRows, which now runs past the knee). The
        // old single tube covered the same span at a uniform width and read as a pipe.
        //
        // WIDE AT THE TOP IS THE WHOLE POINT, and it reverses this grid's own previous note. That
        // note said "no wider than the leg itself", because at 16 the two plates met across the
        // centre line and closed the gap between the legs. The width is the thing that was
        // measured wrongly, not the conclusion: the legs hang at +/-HipHalfSpanCells, which is 8
        // body texels off centre, so two plates meet at 16 and 14 leaves exactly two texels of
        // seam between them. That is what the reference has - the upper thighs are nearly one mass
        // with a dark line down the middle - and the gap opens up below because the plate TAPERS,
        // which a uniform tube could never do. The old rule was right about a tube and wrong about
        // a tasset.
        //
        // It stops above the knee rather than running to the ankle. Everything below is the boot's
        // now, and the two meet with the boot's cuff lapping over this plate's lower edge because
        // BootsFront draws after LegsFront - which is the right way round for a boot pulled on over
        // a thigh plate, and is why neither piece needs to know the other's exact length.
        // TWO OF THE LEG'S THREE SECTIONS - the tasset over the hip and the thigh plate under it.
        // BootRows carries the third. The leg steps DOWN in width as it descends, 14 to 12 to 10,
        // and each step is a hard edge rather than a taper.
        //
        // THE STEPS HAVE TO BE IN THE OUTLINE, AND EACH NEW PLATE NEEDS A LIT TOP LIP. The previous
        // pass had this same 14-12-10 geometry and did not read as three sections at all, because
        // the width changes were spread over a gradual taper and the only thing marking a join was
        // a single row of shadow tone. At this size an interior tone change inside an unbroken
        // outline is invisible - the same thing that made the knee cop disappear until it was
        // flared. So: the section ends on two rows of deep shade, the next one starts one texel in
        // on each side with a GLOW row across its top, and the eye reads a plate edge catching
        // light over the plate beneath it. That is what a lame actually is.
        //
        // IT STARTS AT FULL WIDTH, with no bevel on the top row. A pass before that tapered the
        // top the way every other plate in this file does, and it read as a separate short
        // floating below the belt rather than a tasset hanging off it: the belt is 28 texels across
        // and a 10-texel top edge under it leaves daylight at both corners. A tasset is suspended
        // FROM the strap, so the join has to be the widest part of it.
        // THE TASSET - hip armour hanging from the belt across BOTH hips, ending in THREE POINTS:
        // one over each thigh and one on the centre line.
        //
        // 28 x 14 at Proportions.BodyPpu (150), on RigLayer.Tasset (torso-parented). This piece was
        // twice authored on LegsFront/LegsBack, which is where every other bit of leg armour lives,
        // and that is what made the shape impossible rather than merely wrong. Those layers hang off
        // the LEG pivots, one copy per limb - so there is no sprite on the centre line for a middle
        // point to be drawn on, and the two halves of a faked one would pull apart on the first
        // stride. See RigLayer.Tasset for why hip armour belongs on the torso regardless.
        //
        // 28 WIDE IS THE BELT'S OWN WIDTH, not a guess. A tasset is suspended from the strap, so
        // the two read as one assembly only if they share an edge; narrower and it reads as a
        // separate thing hung underneath with daylight at both corners.
        //
        // THE VALLEYS ARE SEVEN TEXELS AND THAT IS A FLOOR, NOT A STYLE CHOICE. StrokeOutline
        // paints every transparent texel within OutlinePadFor (2 here) of opaque art, from BOTH
        // sides - so a notch narrower than five closes completely, which is the rule that swallowed
        // the disc's grip hollow and Sniper's prong gap. At seven, three texels survive and the
        // three points stay three points. They are cut UP FROM THE BOTTOM rather than punched
        // through, so they are open silhouette rather than enclosed holes.
        //
        // Symmetric, deliberately, where the torso and boot are authored asymmetric toward +X: the
        // whole point of the shape is one point per side and one in the middle, and an off-centre
        // middle point is not a middle point. The turn is carried by everything above and below it.
        static readonly string[] TassetRows =
        {
                "llllllllllllllllllllllllllll",
                "llllllllllllllllllllllllllll",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "dddddddddddddddddddddddddddd",
                "ssssssssssssssssssssssssssss",
                "llll.......llllll.......llll",
                "bbbb.......bbbbbb.......bbbb",
                "bbbb.......bbbbbb.......bbbb",
                "bbbb.......bbbbbb.......bbbb",
                "dddd.......bbbbbb.......dddd",
                "dddd.......dddddd.......dddd",
                ".dd........dddddd........dd.",
                ".ss.........dddd.........ss.",
                "............ssss............",
                ".............ss.............",
        };

        // 10 x 12 at Proportions.BodyPpu (150) - re-placed against the reproportioned hip.
        //
        // The old grid was 8 x 8 at FinePpu with upscale2x, which is 16 x 16 texels at 150 = 0.107
        // world units on a torso 0.173 across. Rendered, the pouch was nearly two thirds of the
        // body's width and hung clear of the hip in open air, because its offX of 10 cells put it
        // well outside the torso's own half-width of 6.5.
        //
        // A FLAP, NOT A DRAWSTRING. The old shape was a rounded bag with one lit band across the
        // top, and at ten texels that band is indistinguishable from a highlight - the piece read
        // as a brown lozenge. A flap folding over the front is a real break in the OUTLINE (rows
        // 5-6 step in by a texel on each side), and outline is what survives at this size; the
        // project relearns this on every small piece.
        //
        // Hung from the belt rather than floating beside it: offY 1 puts its top rows under the
        // strap's own bottom edge, which is what a pouch is actually attached to.
        //
        // THE POUCH IS THE STANDARD FOR EVERY WORN RELIC - see HipRelic.
        static readonly string[] PouchRows =
        {
                "..kkkkkk..",
                ".llllllll.",
                "bbbbbbbbbb",
                "bbbbbbbbbb",
                "dddddddddd",
                ".ssssssss.",
                ".bbbbbbbb.",
                ".bbbbbbbb.",
                ".bbbbbbbb.",
                ".dddddddd.",
                "..ssssss..",
                "..ssssss..",
        };

        /// <summary>The pouch's hip anchor, in torso-local cells: offX 6 is just inside the torso's
        /// own half-width of 6.5, so a relic overlaps the body and reads as strapped to it (the old
        /// 10 put every one of them entirely in open air); offY 1 hangs the pouch off the belt.</summary>
        const float RelicHipX = 6f, PouchY = 1f;

        /// <summary>Where every worn relic's OUTLINED top sits, in torso-local cells: the pouch's.
        /// A property, not a field - PouchRows is declared below it (see BeltBottomYCells).</summary>
        static float RelicTopYCells => PouchY + OutlinedHalfCells(PouchRows.Length);

        /// <summary>
        /// A worn relic, sized and hung the way the POUCH is - the standard they are all measured
        /// against.
        ///
        /// AT THE BODY'S DENSITY, one grid cell to one body texel, which is what the pouch is. The
        /// Black Diamond marks and seals were drawn as weapons (FinePpu, upscale2x), so every cell
        /// landed as a 2x2 block: the Shadow Seal was 30 texels across on a 26-texel torso and the
        /// rest were twice the pouch, hanging in open air off offX 10. The grids are untouched -
        /// dropping the doubling halves each one, which lands them all around the pouch (9-15
        /// against its 10x12) without redrawing a mark. Not uniform, and not meant to be.
        ///
        /// HUNG FROM THE BELT: the outlined top meets the pouch's, so a taller mark hangs lower
        /// rather than riding up the torso, and every edge lands on a texel because the top does.
        ///
        /// An odd-width grid gets one transparent column: a centred pivot on an odd width sits half
        /// a texel off after the mirror (PixelSprite warns). The marks are authored odd on purpose -
        /// a symmetric design with a one-texel apex needs a middle column.
        /// </summary>
        static LayerSprite HipRelic(string key, string[] rows, Dictionary<char, Color> palette)
        {
            if ((rows[0].Length & 1) != 0)
            {
                var even = new string[rows.Length];
                for (int i = 0; i < rows.Length; i++) even[i] = rows[i] + ".";
                rows = even;
            }
            return Pixels(RigLayer.Trinket, key, rows, palette,
                          RelicHipX, RelicTopYCells - OutlinedHalfCells(rows.Length), ppu: BodyPpu);
        }

        // 15 x 15. Shadow's worn mark: a circle housing a triangle, the "squaring the
        // circle" seal simplified down to what actually survives at this density - the
        // full four-shape nesting (circle/triangle/square/circle) only reads once magnified
        // to something like the character screen's own menu density, which this does not
        // attempt yet. Lit rim at top, darkening down the triangle's face, same top-lit
        // convention as everything else in this file.
        static readonly string[] ShadowRelicRows =
        {
                ".....lllll.....",
                "...lllllllll...",
                "..lll.....lll..",
                ".lll...l...lll.",
                ".ll...bbb...ll.",
                "bb....bbb....bb",
                "bb...bbbbb...bb",
                "bb...bbbbb...bb",
                "bb..bbbbbbb..bb",
                "bb..bbbbbbb..bb",
                ".dd.ddddddd.dd.",
                ".ddd.......ddd.",
                "..ddd.....ddd..",
                "...ddddddddd...",
                ".....ddddd.....",
        };

        // 9 x 15. Emberline's worn mark: a bright flame caged in iron - a hanging ring and
        // cap, straight corner posts, a matching base, and the flame itself on the SECOND
        // ramp so it can glow independently of the dark frame.
        static readonly string[] EmberlineRelicRows =
        {
                "....l....",
                "...lll...",
                "..l...l..",
                ".lllllll.",
                "l.......l",
                "l..HHH..l",
                "l.HHLHH.l",
                "l.HLLLH.l",
                "l.BLLLB.l",
                "l.BBBBB.l",
                "l..BBB..l",
                "l.......l",
                ".lllllll.",
                "..l...l..",
                "...lll...",
        };

        // 14 x 32 at Proportions.BodyPpu (150) - a TALL BOOT, from the sole up PAST THE KNEE to
        // meet the tasset hanging off the belt.
        //
        // This grid has now been 12 x 8, then 14 x 12, and the two earlier notes were each solving
        // the wrong problem. The 16-row version was cut back to 12 on the grounds that the boot was
        // "42% of the leg's whole length against the reference's ~25%" - measured off the boot's
        // CUFF, which is the visible seam on a short boot and not the top of the leather. Looking
        // at the reference again with the leg as a whole in view, the plate is continuous from the
        // foot to above the knee; there is no bare shin anywhere on it. 32 rows is 16 cells of a
        // 24-cell leg, and the knee is inside it rather than below it.
        //
        // TWO PARTS, and the join is what makes it a boot rather than a greave: a SHAFT at the
        // shin's own width plus one texel either side, and a FOOT that steps out to the full grid
        // width. The shaft carries a lit knee cop at rows 6-9, placed against the leg grid's own
        // pinch (leg-local -11.5) rather than at the middle of the boot, so the plate breaks where
        // the limb does - the one part of the old note worth keeping.
        //
        // THE TOE STILL POINTS FORWARD, and that reasoning is untouched: a symmetric boot is a foot
        // seen head-on, which is the clearest tell that a character is facing the camera. The foot
        // runs to the grid's +X edge while the shaft stops two texels short of it, so the ankle
        // sits over the heel. Authored toward +X like the torso; the mirror gives the other side.
        // EVERYTHING BELOW THE TASSET IS THE BOOT'S. The Legs slot no longer paints a per-leg plate
        // at all (see gold_greaves), so this piece runs from the sole to the tasset's own lower
        // points, and the knee is inside it rather than below it. It opens on a glow row, the same
        // lit-lip that says a new plate begins wherever two meet on this character.
        //
        // THE KNEE COP FLARES PAST THE SHAFT (rows 6-9, the full 14 against the shin's 10), and
        // that is load-bearing rather than decoration. A first pass drew it as a lit band at the
        // shaft's own width and it did not read at all: at this size a tone change inside an
        // unbroken outline is invisible, so the boot was one silver tube from thigh to ankle. The
        // old short greave's own note had already worked this out - "the silhouette gain has to
        // come from the knee cop, not from girth" - and drawing the cop without the flare is
        // exactly the half of that sentence this grid forgot. Grooves above and below it (rows 5
        // and 10) close the joint off at both ends.
        static readonly string[] BootRows =
        {
                "..hhhhhhhhhh..",
                "..llllllllll..",
                "..bbbbbbbbbb..",
                "..dddddddddd..",
                "llllllllllllll",
                "llllllllllllll",
                "bbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbb",
                ".ssssssssssss.",
                "..llllllllll..",
                "..llllllllll..",
                "..bbbbbbbbbb..",
                "..bbbbbbbbbb..",
                "..bbbbbbbbbb..",
                "..bbbbbbbbbb..",
                "..dddddddddd..",
                "..dddddddddd..",
                "..ssssssssss..",
                "..llllllllllll",
                "..llllllllllll",
                "..llllllllllll",
                "..bbbbbbbbbbbb",
                "..bbbbbbbbbbbb",
                "..bbbbbbbbbbbb",
                "..dddddddddddd",
                "..dddddddddddd",
                "..ssssssssssss",
                "..ssssssssssss",
        };

        // 28 x 6 at Proportions.BodyPpu (150) - re-placed against the reproportioned waist.
        //
        // The old grid was 20 x 4 at 75 = 0.267 world units across, worn on a torso that is now
        // 0.173. A belt half again wider than the body it cinches reads as a shelf. 28 texels is
        // the torso's own 26 plus one either side, which is the least a strap can overhang and
        // still sit ON the body rather than in it.
        //
        // IT SITS BELOW THE CUIRASS, NOT UNDER IT. CuirassRows is 16 cells tall centred at offY 14,
        // so its fauld ends at torso-local cell 6; the belt is centred at 3 and so occupies 1.5 to
        // 4.5, leaving about a cell and a half of bare undersuit showing between plate and strap.
        // That gap is not slack in the layout - it is the whole reason the body is black now. The
        // reference reads as separate plates precisely because dark undersuit shows at every seam
        // between them, and a belt butted up against the fauld would close the one seam that runs
        // all the way across the figure.
        //
        // THE BUCKLE IS CARRIED FORWARD, off-centre by three texels. Every other piece in this set
        // follows the torso's own turn (see CuirassRows on why a symmetric plate throws the turn
        // away); a belt is a ring, so its outline cannot turn, but the one feature ON it can. It is
        // the cheapest possible asymmetry and it costs no silhouette.
        static readonly string[] BeltRows =
        {
                "..dddddddddddddddddddddddd..",
                ".llllllllllllllllllllllllll.",
                "bbbbbbbbbbbbbbbhhhhbbbbbbbbb",
                "bbbbbbbbbbbbbbbhhhhbbbbbbbbb",
                ".ddddddddddddddlllldddddddd.",
                "..ssssssssssssssssssssssss..",
        };

        // 14 x 28 at Proportions.BodyPpu (150) - the WHOLE ARM HARNESS: rerebrace, vambrace and
        // gauntlet in one piece, running from just under the pauldron to the fingertips.
        //
        // THIS IS WHY THERE IS NO UPPER-ARM RIG LAYER. The previous pass stopped at the elbow and
        // this note said the reference's upper-arm plate "needs a layer pair on the arm pivot
        // rather than a bigger grid" - because RigLayer.Shoulders hangs off the TORSO, so a
        // pauldron extended downward would stay put while the arm swung out from under it. That
        // much is still true. What it missed is that the piece does not have to come from the
        // shoulder: RigLayer.GlovesFront/Back are ALREADY on the arm pivot (see PivotFor), so a
        // glove grown UPWARD past the elbow covers the upper arm and rotates with the limb for
        // free. Reaching the same plate from the hand end costs two enum entries and six
        // renumbered sorting permutations less than reaching it from the shoulder end.
        //
        // FOUR PLATES SEPARATED BY TWO GROOVES, which is the whole structure. At 28 rows a single
        // tapered run from shoulder to fingertip is a sleeve, not armour. The elbow groove (row 9)
        // and the wrist groove (row 18) are what say "these are separate plates hinged together",
        // and both border opaque art on every side so the auto-outline has nothing to erode them
        // from - the rule that closed the disc's grip hollow.
        //
        // Width follows the arm's own anatomy rather than being uniform: 14 at the rerebrace and
        // again at the gauntlet, 12 through the forearm between them. A hand and a shoulder are the
        // wide ends of an arm, and cutting the piece to the limb's own 10 texels throughout reads
        // as the arm changing colour.
        static readonly string[] GloveRows =
        {
                "..hhhhhhhhhh..",
                "..hhhhhhhhhh..",
                ".llllllllllll.",
                ".llllllllllll.",
                "llllllllllllll",
                "llllllllllllll",
                "bbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbb",
                "dddddddddddddd",
                ".ssssssssssss.",
                ".llllllllllll.",
                ".llllllllllll.",
                ".bbbbbbbbbbbb.",
                ".bbbbbbbbbbbb.",
                ".bbbbbbbbbbbb.",
                ".bbbbbbbbbbbb.",
                "..dddddddddd..",
                "..dddddddddd..",
                "..ssssssssss..",
                "llllllllllllll",
                "llllllllllllll",
                "bbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbb",
                "dddddddddddddd",
                "dddddddddddddd",
                ".ssssssssssss.",
                "..ssssssssss..",
        };

        // ---- Black Steel: dedicated silhouettes, not the shared tier grids above. A first pass
        // reused HelmRows/PauldronRows/CuirassRows/GloveRows/BootRows with a gold second
        // material swapped over the existing trim line - correct in the way every gold/silver/
        // bronze piece already works, wrong for THIS set, which reads as its own reference
        // rather than a recolour of the gilded one. Every grid below is its own shape.
        //
        // The second material is BLACK, not gold - painted only in its own BASE tone ('B'),
        // never 'L'/'H', which would lighten a near-black ramp toward white and defeat the
        // point of it. One recurring motif ties the set together: a full-width black accent
        // SEAM marking where one plate overlaps the next (the pauldron's cap/flare line, the
        // cuirass's two fauld seams, the greave's shin band) - the same idea repeated at every
        // scale rather than a different trim treatment per piece.

        // 18 wide, 18 rows - a WORN helmet, measured against the head rather than guessed at.
        //
        // THE THIRD ATTEMPT, AND THE FIRST ONE FITTED TO REAL GEOMETRY. The first pass was 12x6
        // and the second 16x9; both read as a plate perched on the forehead, and measuring said
        // exactly why rather than leaving it to taste. The head sprite spans y -1..+17 texels
        // from the rig root, and BodyLook's composed grid puts the EYES at y +5.5 and +6.5 (grid
        // columns 3-4 and 11-12), the cheeks at +1.5..+4.5 and the chin at -0.5..+0.5. The old
        // helm sat at y +7.5..+18.5 - its BOTTOM edge was a texel above the top of the eyes, so
        // it could only ever be a cap: nine texels of face hung below it, untouched. No amount of
        // reshaping the dome fixes that, because the problem was never the dome.
        //
        // This one spans y -1..+19 (18 rows plus the auto-outline's own pad, centred on an offset
        // of 9), so it covers the scalp, comes down over the temples, wraps the cheeks and closes
        // under the jaw. The face is seen through a real VISOR APERTURE rather than below a brim:
        //
        //      rows 0-5     dome, widening to the head's own width by y +13.5 so the hair is
        //                   under it rather than beside it
        //      rows 7-8     the temple flare, one texel proud of the dome either side
        //      row 10       the black brow band - the aperture's upper rim, at y +7.5, one texel
        //                   clear above the eyes
        //      rows 11-15   THE APERTURE. Widest at the eye rows (11-12, y +6.5/+5.5) so both
        //                   eyes clear the auto-outline's own one-texel bite into the hole, then
        //                   narrowing as it descends, which is both what a visor does and what
        //                   lets the cheek rails widen 2 -> 3 -> 4 -> 5 texels over the jaw
        //      rows 16-17   the chin bar, closing the silhouette underneath
        //
        // WIDTH AND HEIGHT ARE BOTH EVEN AND THE OFFSET IS A WHOLE TEXEL, which is what keeps
        // this aligned to the head's own grid. The head is 16x18 centred on x 0 / y +8, so its
        // rows and columns land on half-integers; an odd padded height or a fractional offset
        // would put the helm half a texel off the face it is supposed to be worn on.
        //
        // Every row is a palindrome (mirrored about the centre column, both halves sharing one
        // tone) rather than shaded left-to-right - the same discipline Obsidian's own note above
        // states is required for a piece that has to mirror cleanly, since shading by COLUMN
        // instead of by ROW is exactly the mistake that put that blade's hilt off-axis.
        static readonly string[] BlackSteelHelmRows =
        {
                "..............hhhhhhhh..............",
                "..............hhhhhhhh..............",
                "..........hhhhhhhhhhhhhhhh..........",
                "..........hhhhhhhhhhhhhhhh..........",
                "......hhllllllllllllllllllllhh......",
                "......hhllllllllllllllllllllhh......",
                "....hhllbbbbbbbbbbbbbbbbbbbbllhh....",
                "....hhllbbbbbbbbbbbbbbbbbbbbllhh....",
                "..hhllbbbbbbbbbbbbbbbbbbbbbbbbllhh..",
                "..hhllbbbbbbbbbbbbbbbbbbbbbbbbllhh..",
                "..hhbbbbbbbbbbbbbbbbbbbbbbbbbbbbhh..",
                "..hhbbbbbbbbbbbbbbbbbbbbbbbbbbbbhh..",
                "..bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb..",
                "..bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb..",
                "BBBBbbbbbbbbbbbbbbbbbbbbbbbbbbbbBBBB",
                "BBBBbbbbbbbbbbbbbbbbbbbbbbbbbbbbBBBB",
                "..BBbbbbbbbbbbbbbbbbbbbbbbbbbbbbBB..",
                "..BBbbbbbbbbbbbbbbbbbbbbbbbbbbbbBB..",
                "..BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB..",
                "..BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB..",
                "bbbb............................bbbb",
                "bbbb............................bbbb",
                "bbbb............................bbbb",
                "bbbb............................bbbb",
                "bbbb............................bbbb",
                "bbbb............................bbbb",
                "bbbbbb........................bbbbbb",
                "bbbbbb........................bbbbbb",
                "dddddddd....................dddddddd",
                "dddddddd....................dddddddd",
                "dddddddddd................dddddddddd",
                "dddddddddd................dddddddddd",
                "dddddddddddd............dddddddddddd",
                "dddddddddddd............dddddddddddd",
                "..ssssssssssssssssssssssssssssssss..",
                "..ssssssssssssssssssssssssssssssss..",
        };

        // 8 wide. The piece the reference leans on hardest: a domed cap plate over a flared
        // lower plate, the two divided by a full black accent seam so they read as two
        // overlapping pieces of steel rather than one blob, closing on a black rim at the hem.
        static readonly string[] BlackSteelPauldronRows =
        {
                "..hhhhhhhhhhhh..",
                "..hhhhhhhhhhhh..",
                "hhbbbbbbbbbbbbhh",
                "hhbbbbbbbbbbbbhh",
                "BBBBBBBBBBBBBBBB",
                "BBBBBBBBBBBBBBBB",
                "bbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbb",
                "..dddddddddddd..",
                "..dddddddddddd..",
                "....BBBBBBBB....",
                "....BBBBBBBB....",
        };

        // 10 wide. A black keel line down the sternum (the reference's centre ridge, about as
        // much of it as a 10-wide grid can say without it dissolving into noise) crossed by two
        // full-width accent seams marking the fauld's articulation - the layered, segmented
        // read the reference has, built from the same seam motif every other piece here uses.
        static readonly string[] BlackSteelCuirassRows =
        {
                "..hhhhhhhhhhhhhhhh..",
                "..hhhhhhhhhhhhhhhh..",
                "hhbbbb..BBBB..bbbbhh",
                "hhbbbb..BBBB..bbbbhh",
                "bbbbbb..BBBB..bbbbbb",
                "bbbbbb..BBBB..bbbbbb",
                "bbbbbbBBBBBBBBbbbbbb",
                "bbbbbbBBBBBBBBbbbbbb",
                "bbbbbb..BBBB..bbbbbb",
                "bbbbbb..BBBB..bbbbbb",
                "dddddd..BBBB..dddddd",
                "dddddd..BBBB..dddddd",
                "ddddddBBBBBBBBdddddd",
                "ddddddBBBBBBBBdddddd",
                "..ssssssssssssssss..",
                "..ssssssssssssssss..",
                "....ssssssssssss....",
                "....ssssssssssss....",
        };

        // 14 x 28 at BodyPpu - the WHOLE ARM, on GloveRows' own frame (same widths, same elbow
        // and wrist breaks, so the elbow split cuts in the same place), in the set's own language:
        // every plate OPENS on the black accent seam the pauldron, cuirass and greave all carry,
        // where Silver's harness has a shaded groove. A rivet pair on the upper arm, and the old
        // gauntlet as the hand - its black cuff and the knuckle guard's rivets either side (a
        // literal per-finger gap would be swallowed whole by the auto-outline, so the guard is
        // the rivets, not a cut).
        static readonly string[] BlackSteelHarnessRows =
        {
                "..BBBBBBBBBB..",
                "..BBBBBBBBBB..",
                ".llllllllllll.",
                ".llllllllllll.",
                "bbBBbbbbbbBBbb",
                "bbBBbbbbbbBBbb",
                "bbbbbbbbbbbbbb",
                "dddddddddddddd",
                "dddddddddddddd",
                ".BBBBBBBBBBBB.",
                ".llllllllllll.",
                ".llllllllllll.",
                ".bbbbbbbbbbbb.",
                ".bbbbbbbbbbbb.",
                ".bbbbbbbbbbbb.",
                ".bbbbbbbbbbbb.",
                "..dddddddddd..",
                "..dddddddddd..",
                "..BBBBBBBBBB..",
                ".BBBBBBBBBBBB.",
                "bbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbb",
                "bBBbbbbbbbbBBb",
                "bBBbbbbbbbbBBb",
                "dddddddddddddd",
                "dddddddddddddd",
                ".ssssssssssss.",
                "..ssssssssss..",
        };

        // 8 wide. Knee cop, shin plate, and a mid-shin accent seam - the set's missing slot.
        static readonly string[] BlackSteelGreaveRows =
        {
                "..BBBBBBBBBBBB..",
                "..BBBBBBBBBBBB..",
                "bbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbb",
                "ddddBBBBBBBBdddd",
                "ddddBBBBBBBBdddd",
                "dddddddddddddddd",
                "dddddddddddddddd",
                "..ssssssssssss..",
                "..ssssssssssss..",
        };

        // 8 wide, matching the greave's own cuff so boots and greaves read as one leg.
        static readonly string[] BlackSteelBootRows =
        {
                "..BBBBBBBBBBBB..",
                "..BBBBBBBBBBBB..",
                "bbbbbbbbbbbbbbbb",
                "bbbbbbbbbbbbbbbb",
                "bbBBbbbbbbbbBBbb",
                "bbBBbbbbbbbbBBbb",
                "dddddddddddddddd",
                "dddddddddddddddd",
                "..ssssssssssss..",
                "..ssssssssssss..",
        };

        // Same strap shape as BeltRows - a belt has little silhouette to redesign at this
        // width - with a black buckle instead of gold. 28 x 6 at BodyPpu, following BeltRows onto
        // the reproportioned waist (it was 20 x 4 at 75, the old BeltRows' shape and size). The
        // buckle runs the strap's full height as it always did, in the accent's base tone only.
        static readonly string[] BlackSteelBeltRows =
        {
                "..dddddddddddddBBBBddddddd..",
                ".llllllllllllllBBBBllllllll.",
                "bbbbbbbbbbbbbbbBBBBbbbbbbbbb",
                "bbbbbbbbbbbbbbbBBBBbbbbbbbbb",
                ".ddddddddddddddBBBBdddddddd.",
                "..sssssssssssssBBBBsssssss..",
        };

        // 8 x 22. A flat blade, one crossguard bar, a leather-wrapped grip.
        //
        // The pivot sits INSIDE the grip (row 18, hence the row numbering below), not at the
        // sprite's geometric centre like every other piece. A weapon that rotates about its own
        // middle has its blade hanging in mid-air at rest and orbiting empty space mid-swing - no
        // offset choice fixes that, only pivoting from where the fist actually closes does. See
        // PixelSprite.Build's pivotTexel handling.
        //
        //  rows  0-5        tip
        //  6 .. 6+BladeRows  blade
        //  next 2            ricasso, widening into the guard
        //  next 4            crossguard (l / b / d / s bars)
        //  next 1            collar
        //  next 6            grip / fists   <- pivot lands on the 2nd of these
        //  next 3            pommel
        /// <summary>
        /// A two-handed greatsword, 28 x 74 texels at the doubled density - about twice the
        /// character's own height.
        ///
        /// The length is a measurement, not a style choice: `BaseRange` is 1.9 units and the tip
        /// has to arrive at the ring drawn on the ground. 52 blade rows at 75 ppu is the same
        /// reach the old 26 rows had at 37.5 - the resolution doubled, the world size did not.
        ///
        /// Built rather than written out: the blade is 52 identical rows and pasting them as
        /// literals would bury the handful that actually have shape.
        ///
        /// Lowercase is the tier metal, uppercase the leather wrap (see the goldGrip palette).
        /// </summary>
        /// <summary>
        /// Rows in the blade's parallel section - the single number that sets the sword's reach.
        /// <see cref="GreatswordGrip"/> is derived from it, because a pivot that disagrees with
        /// the grid moves the whole sword out of the character's hands.
        ///
        /// 50, raised from 34 at the same time the blade was narrowed from 18 cells to 10. The two
        /// moved TOGETHER on purpose and should stay a pair: narrowing alone leaves the sword the
        /// same height as the character and still reading as a short, thick slab, which was most
        /// of what was wrong with it. At 50 the whole weapon stands about 1.28 times the figure's
        /// own height and the blade's aspect lands near 1:5 - which is what says greatsword rather
        /// than cleaver.
        ///
        /// Every OTHER sword in the game is measured against the result (see CLAUDE.md's "every
        /// sword the same height"), so moving this moves nine other length constants with it.
        /// </summary>
        const int BladeRows = 49;

        /// <summary>
        /// The whole family's height in AUTHORED rows: tip, blade, ricasso, the row widening into
        /// the guard, the guard, the collar, the grip and the pommel. 72.
        ///
        /// This is the number every other greatsword in the game is sized against, so it is
        /// COUNTED here rather than measured off a finished grid - a sword built to match a
        /// literal is a sword that silently stops matching the moment this one moves.
        /// </summary>
        const int SwordHeightRows = 6 + BladeRows + 1 + 1 + 4 + 1 + SwordGripRows + 3;

        /// <summary>
        /// The finished sprite's height in TEXELS, outline included - 148, which is 0.9867 world
        /// units at <see cref="FinePpu"/> against a character 0.7733 tall.
        ///
        /// This is the number "every sword the same height" actually means, and the one to compare
        /// a differently-built weapon against: a sword with its own row structure (Saint's wings,
        /// Zanmato's tsuka, Blood's vial) can never match by counting rows, only by matching here.
        /// </summary>
        static int SwordHeightTexels
            => SwordHeightRows * FineUpscale + PixelSprite.OutlinePadFor(FinePpu) * 2;

        /// <summary>
        /// How many rows a weapon's parallel blade gets, so the finished sword comes out exactly
        /// <see cref="SwordHeightRows"/> tall. <paramref name="others"/> is every row it spends on
        /// something that is not blade - tip, wings, guard, collar, grip, pommel.
        ///
        /// "Every sword the same height" was previously a rule enforced by ten separate literals
        /// that all had to be edited together, and the comments beside them show what that costs:
        /// several still describe a length the sword had stopped having. Each weapon now declares
        /// what it spends and this works out the rest, so moving <see cref="BladeRows"/> carries
        /// the whole set with it and a weapon can only be the wrong height by declaring its own
        /// structure wrongly - which shows up immediately, as a sword with no blade.
        /// </summary>
        static int BladeRowsFor(int others) => Mathf.Max(1, SwordHeightRows - others);

        /// <summary>
        /// The same budget for a weapon drawn WITHOUT the auto-outline. Two of them are (Lumen,
        /// Obsidian - see <see cref="OutlinelessWeapon"/>), and they have to make the stroke's own
        /// thickness up out of their own grid or they come out short of everything else by it.
        /// </summary>
        static int BladeRowsForNoOutline(int others)
            => Mathf.Max(1, SwordHeightTexels / FineUpscale - others);

        /// <summary>
        /// The two weapons that go to <see cref="PixelSprite"/> directly rather than through
        /// <see cref="Pixels"/>, because an auto-outline is wrong for both of them - a black
        /// cartoon line around a beam of light, and a line drawn OUTSIDE the see-through edge that
        /// is Obsidian's entire idea.
        ///
        /// They still need the UPSCALE every other weapon gets, and they did not have it. That is
        /// why both rendered at roughly half the world height of every other sword in the game: a
        /// standing violation of "every sword the same height" that predates the narrowing and was
        /// invisible precisely because it came from bypassing the one function that does the
        /// doubling. Anything else that needs to skip the outline should come through here too.
        /// </summary>
        /// <summary>
        /// Hand-built menu art for an outlineless weapon, at TRUE <see cref="MenuPpu"/> - no
        /// doubling, no outline, and the same Size rule <see cref="OutlinelessWeapon"/> keeps.
        /// </summary>
        static LayerSprite OutlinelessMenuWeapon(string key, string[] rows,
                                                 Dictionary<char, Color> palette,
                                                 Vector2Int grip, float offY)
            => new LayerSprite
            {
                Layer = RigLayer.Weapon,
                Sprite = PixelSprite.From(key, rows, palette, outline: false, pivotTexel: grip,
                                          pixelsPerUnit: MenuPpu),
                Offset = PixelSprite.Px(0f, offY),
                Size = PixelSprite.SizeOf(rows, outline: false, pixelsPerUnit: MenuPpu),
                Tint = Color.white,
            };

        static LayerSprite OutlinelessWeapon(string key, string[] rows,
                                             Dictionary<char, Color> palette,
                                             Vector2Int grip, float offY,
                                             bool unlit = false)
        {
            var scaled = Double2x(rows);
            var pivot = new Vector2Int(grip.x * FineUpscale, grip.y * FineUpscale);
            // Recorded in _sources exactly as Pixels() records its own output, or the menu-art
            // pass never sees these two and hands the character screen the arena sprite instead -
            // the same half-a-step-behind that bypassing the upscale once cost them in height.
            var made = new LayerSprite
            {
                Layer = RigLayer.Weapon,
                Sprite = PixelSprite.From(key, scaled, palette, outline: false, pivotTexel: pivot,
                                          pixelsPerUnit: FinePpu),
                Offset = PixelSprite.Px(0f, offY),
                // The SAME rows and the same outline flag PixelSprite.From was given, or the rig's
                // Size/bounds scaling stops being exactly (1,1) and quietly stretches the art.
                Size = PixelSprite.SizeOf(scaled, outline: false, pixelsPerUnit: FinePpu),
                Tint = Color.white,
            };
            _sources[made] = (RigLayer.Weapon, key, scaled, palette, 0f, offY, pivot, FinePpu,
                              false, null);
            if (unlit) _unlit.Add(made);
            return made;
        }

        // The plain discs' ARENA grids (DiscRows, QuatrefoilRows) were typed out at 24 cells; they
        // are built by BuildDiscAt now, with their menu art, at the disc standard - see there.

        // ====================================================== the greatsword envelope
        //
        // ONE description of the silhouette every greatsword-class weapon in this file shares.
        // Five builders draw this shape - BuildGreatsword, BuildGreatswordDetail, FireBlade,
        // BuildRiftSword and BuildShadowSword/Detail - and each of them used to retype the same
        // "blade over cols 5..22, crossguard the full 28" by hand, the two with menu art doing it
        // twice over at the doubled grid. A blade's WIDTH is exactly the kind of number that gets
        // tuned, and tuning it meant finding seven copies and hoping none was missed.
        //
        // BOTH WIDTHS ARE MEASURED AGAINST THE BODY, in the same LAYOUT CELLS the rig counts its
        // joints in, so they follow a reproportion rather than drifting away from one:
        //
        //     the blade is never wider than the TORSO        Cells(TorsoW) = 13
        //     the guard is never wider than the SHOULDERS    2 x (ShoulderHalfSpan + ArmW/2) = 19
        //
        // each rounded DOWN to even, because PixelSprite mirrors about a texel BOUNDARY and an odd
        // width has no boundary at its middle to mirror about.
        //
        // They were 18 and 28 - a blade wider than the character's own head, hung under a guard
        // nearly twice the width of their shoulders. At that size the weapon stops reading as a
        // sword and reads as a PLANK: the blade's own aspect was 18:82, about 1:4.5 counting the
        // grip, where a slab greatsword wants to be nearer 1:7. Narrowing is the whole fix; the
        // length is untouched, and deliberately (see BladeRows - reach is a measurement).

        /// <summary>Authored columns. Every weapon in the family shares this canvas, and the
        /// canvas does NOT shrink with the blade - the art narrows inside it, so every pivot,
        /// offset and menu-grid counterpart keeps landing where it already did.</summary>
        const int SwordCanvas = 28;

        /// <summary>The centre line in authored columns - 13.5. The canvas is even, so the axis
        /// falls on a boundary between two columns rather than on one.</summary>
        const float SwordAxis = SwordCanvas / 2f - 0.5f;

        /// <summary>Largest even number of cells not exceeding <paramref name="cells"/>.</summary>
        static int EvenAtMost(float cells) => Mathf.FloorToInt(cells * 0.5f) * 2;

        /// <summary>
        /// What the auto-outline costs a weapon grid, in AUTHORED cells per side.
        ///
        /// <see cref="PixelSprite.OutlinePadFor"/> answers in TEXELS of the finished sprite, and
        /// every weapon in this file is authored at half the density it renders at (upscale2x), so
        /// the stroke is worth half as many authored cells as texels.
        ///
        /// It is subtracted from both budgets below rather than ignored, because both are measured
        /// against the BODY and the eye measures the DRAWN edge, not the grid: a blade authored to
        /// exactly the torso's width comes out wider than the torso by one cell either side, and
        /// looks it.
        /// </summary>
        static int SwordOutlineCells
            => Mathf.Max(1, PixelSprite.OutlinePadFor(FinePpu) / FineUpscale);

        /// <summary>The blade's parallel section, in authored cells. 10 - the torso, less the
        /// outline either side, rounded down to even. Widen this and every greatsword in the game
        /// widens with it.</summary>
        static readonly int SwordBladeWidth = EvenAtMost(
            PrimitiveCharacterRig.Proportions.Cells(PrimitiveCharacterRig.Proportions.TorsoW)
            - SwordOutlineCells * 2);

        /// <summary>The crossguard, in authored cells. 16 - the shoulders, less the outline either
        /// side. Outside the fists this is the widest part of any sword here.</summary>
        static readonly int SwordGuardWidth = EvenAtMost(
            2f * (PrimitiveCharacterRig.Proportions.ShoulderHalfSpanCells
                  + PrimitiveCharacterRig.Proportions.Cells(PrimitiveCharacterRig.Proportions.ArmW) * 0.5f)
            - SwordOutlineCells * 2);

        /// <summary>
        /// The blade width every hand-tuned measurement INSIDE the blade was originally taken
        /// against.
        ///
        /// Emberline's glass lens, Shadow's hollow fuller and the Rift Blade's tear are all
        /// absolute texel half-widths, chosen so a specific number of texels of flat survives
        /// either side of them. They are not independent of the blade they are cut into: a fuller
        /// tuned to leave six texels a side on an eighteen-cell blade leaves NONE on a twelve-cell
        /// one, and the sword comes out as a tuning fork. So they are all scaled by
        /// <see cref="SwordBladeScale"/> rather than re-typed.
        /// </summary>
        const int SwordBladeWidthTunedAgainst = 18;

        /// <summary>What the blade is now, as a fraction of the grid those numbers were tuned
        /// against. Everything measured across the blade is multiplied by this.</summary>
        static float SwordBladeScale => SwordBladeWidth / (float)SwordBladeWidthTunedAgainst;

        /// <summary>Leftmost and rightmost authored column of the parallel blade.</summary>
        static int SwordBladeLeft => (SwordCanvas - SwordBladeWidth) / 2;
        static int SwordBladeRight => SwordBladeLeft + SwordBladeWidth - 1;

        /// <summary>How many cells of the blade each shaded edge takes. A quarter, which is what
        /// the 5 : 8 : 5 the eighteen-cell blade carried works out at.</summary>
        static int SwordEdge => Mathf.Max(2, SwordBladeWidth / 4);

        /// <summary>A fragment laid on the canvas's centre line, the rest transparent.</summary>
        static string SwordCentred(string frag, int canvas = SwordCanvas)
        {
            int pad = (canvas - frag.Length) / 2;
            return new string('.', pad) + frag + new string('.', canvas - frag.Length - pad);
        }

        /// <summary>lit / base / dark bands, the ramp every flat in this family is shaded with.
        /// <paramref name="ramp"/> is the four tones, lightest first.</summary>
        static string SwordBand(int lit, int bas, int dark, string ramp)
            => new string(ramp[0], lit) + new string(ramp[1], bas) + new string(ramp[2], dark);

        /// <summary>
        /// How much of the blade each of the six tip rows carries. Taken off the original
        /// eighteen-cell point as fractions rather than as texel counts, so the tip keeps its
        /// bluntness when the blade narrows instead of turning into a needle.
        /// </summary>
        static readonly float[] SwordTipTaper = { 0.22f, 0.39f, 0.67f, 0.83f, 0.94f, 1f };

        /// <summary>
        /// The six rows of tip every weapon in the family shares, as lit / base / dark cell
        /// counts. Handed out as BANDS rather than as finished rows because the menu grids need
        /// the same taper at twice the size with their own specular tone laid over it - which is
        /// exactly the doubling that used to be a second hand-typed copy.
        /// </summary>
        static (int Lit, int Base, int Dark)[] SwordTipBands()
        {
            var bands = new (int, int, int)[SwordTipTaper.Length];
            for (int i = 0; i < bands.Length; i++)
            {
                int w = Mathf.Max(3, Mathf.FloorToInt(SwordTipTaper[i] * SwordBladeWidth + 0.5f));
                int lit = Mathf.Clamp(Mathf.FloorToInt(w * SwordEdge / (float)SwordBladeWidth + 0.5f),
                                      2, SwordEdge);
                // The very point is lit-and-base only: dark arriving at the tip reads as a chip
                // out of it rather than as the far flat turning away.
                int dark = i < 2 ? 0 : i == 2 ? Mathf.Max(1, lit - 1) : lit;
                bands[i] = (lit, w - lit - dark, dark);
            }
            return bands;
        }

        /// <summary>
        /// The six rows of tip every weapon in the family shares. The point is not where any of
        /// them says what it is, so it is drawn once, here.
        /// </summary>
        static string[] SwordTipRows(string ramp)
        {
            var bands = SwordTipBands();
            var rows = new string[bands.Length];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = SwordCentred(SwordBand(bands[i].Lit, bands[i].Base, bands[i].Dark, ramp));
            return rows;
        }

        /// <summary>One row of the parallel blade - lit left edge, shaded right.</summary>
        static string SwordBladeRow(string ramp)
            => SwordCentred(SwordBand(SwordEdge, SwordBladeWidth - SwordEdge * 2, SwordEdge, ramp));

        /// <summary>The row where the blade widens into the guard: a cell proud either side.</summary>
        static string SwordShoulderRow(string ramp)
            => SwordCentred(SwordBand(SwordEdge + 1, SwordBladeWidth - SwordEdge * 2,
                                      SwordEdge + 1, ramp));

        /// <summary>The four crossguard rows. <paramref name="ramp"/> is lit/base/dark/deep.</summary>
        static string[] SwordGuardRows(string ramp) => new[]
        {
            SwordCentred(new string(ramp[0], SwordGuardWidth)),
            SwordCentred(ramp[0] + new string(ramp[1], SwordGuardWidth - 2) + ramp[0]),
            SwordCentred(new string(ramp[2], SwordGuardWidth)),
            SwordCentred(new string(ramp[3], SwordGuardWidth)),
        };

        /// <summary>The collar between the guard and the grip - two cells inside the blade, with
        /// a third of it turned down at each end.</summary>
        static int SwordCollarWidth => Mathf.Max(4, SwordBladeWidth - 2);
        static int SwordCollarEnd => Mathf.Max(1, SwordCollarWidth / 3);

        static string SwordCollarRow(string ramp)
            => SwordCentred(new string(ramp[2], SwordCollarEnd)
                            + new string(ramp[1], SwordCollarWidth - SwordCollarEnd * 2)
                            + new string(ramp[2], SwordCollarEnd));

        /// <summary>
        /// The grip, and it does NOT follow the blade: what closes on it is a fist, and a fist is
        /// the size the body says it is however wide the sword gets.
        ///
        /// SIX cells, down from the plank's eight. Eight was sized when the blade was eighteen
        /// cells; once the blade narrowed to ten, an eight-cell grip was nearly the blade's own
        /// width and the handle read as a continuation of the blade rather than something held.
        /// Six is Zanmato's tsuka, and the outline makes it draw as eight either way. The pommel
        /// (<see cref="SwordPommelWidth"/>) follows it.
        /// </summary>
        // FOUR, down from six - the handles were thinned along with being lengthened: a long grip
        // at six cells read as a second blade stub. Four is still under the fists' own width.
        const int SwordGripWidth = 4;

        /// <summary>
        /// Grip length in authored rows, for every sword built on the shared envelope - SEVEN,
        /// up from six: at six the two-handed grip read as cramped once the blades lengthened.
        /// The extra row comes out of the blade (<see cref="BladeRows"/> 50 -> 49), so the family
        /// height is untouched. Swords with their own grip constants (Zanmato, Obsidian, Sniper,
        /// Lumen) each grew by the same one row.
        /// </summary>
        // TWELVE, up from seven: a two-hander's grip is a fifth to a quarter of its length, and at
        // seven the fists sat hard against the guard and hid every hilt. The five rows were ADDED
        // to the sword (SwordHeightRows grows with this) rather than taken from the blade, so
        // every blade keeps its length. See GripCentre for where the fists now close.
        const int SwordGripRows = 12;

        /// <summary>
        /// The grip's CENTRE row, given its first row and length - every greatsword's pivot. It was
        /// the grip's SECOND row, which put the fists hard against the guard; centred on a longer
        /// grip, the guard stands clear above the hands and the pommel below them.
        /// </summary>
        static int GripCentre(int gripTop, int gripRows) => gripTop + gripRows / 2;

        static string SwordGripRow(string wrap)
            => SwordCentred(wrap[0] + new string(wrap[1], SwordGripWidth - 2) + wrap[0]);

        /// <summary>The pommel, a cell inside the collar and never narrower than the grip it
        /// caps - a cap the fist overhangs reads as the sword sliding out of the hand.</summary>
        static int SwordPommelWidth => Mathf.Max(SwordGripWidth + 2, SwordBladeWidth - 2);

        /// <summary>The three pommel rows, capping the grip a little proud of it.</summary>
        static string[] SwordPommelRows(string ramp)
        {
            int w = SwordPommelWidth;
            return new[]
            {
                SwordCentred(new string(ramp[0], 2) + new string(ramp[1], w - 4)
                             + new string(ramp[0], 2)),
                SwordCentred(new string(ramp[3], w)),
                SwordCentred(new string(ramp[3], Mathf.Max(2, w - 4))),
            };
        }

        static string[] BuildGreatsword()
        {
            var rows = new List<string>(BladeRows + 21);
            rows.AddRange(SwordTipRows("lbd"));                        // tip
            for (int i = 0; i < BladeRows; i++)
                rows.Add(SwordBladeRow("lbd"));                        // lit left edge, shaded right
            rows.Add(SwordBladeRow("lbd"));                            // ricasso
            rows.Add(SwordShoulderRow("lbd"));                         // widening into the guard
            rows.AddRange(SwordGuardRows("lbds"));                     // crossguard
            rows.Add(SwordCollarRow("lbd"));                           // collar
            for (int i = 0; i < SwordGripRows; i++)
                rows.Add(SwordGripRow("KB"));                          // grip; the 2nd is the pivot
            rows.AddRange(SwordPommelRows("lbds"));                    // pommel
            return rows.ToArray();
        }

        static readonly string[] GreatswordRows = BuildGreatsword();

        /// <summary>
        /// Lay a bronze cutting edge down the LEFT (lit) side of a greatsword grid, over the tip,
        /// the blade and the ricasso - every row above the one that widens into the guard.
        /// Bronze is the grid's THIRD material (digits: 6 glow, 5 light, 4 base).
        ///
        /// One edge, not both: a single tempered edge is what makes it read as a second metal
        /// worked onto the blade rather than as a two-tone paint job. On the narrowest tip rows
        /// the band covers the whole row, so the point itself is bronze - the point strikes too.
        /// </summary>
        static string[] BronzeEdge(string[] rows, int bladeRows, int width, string tones)
        {
            var result = (string[])rows.Clone();
            for (int y = 0; y < bladeRows && y < result.Length; y++)
            {
                var line = result[y].ToCharArray();
                int a = System.Array.FindIndex(line, ch => ch != '.');
                if (a < 0) continue;
                int b = System.Array.FindLastIndex(line, ch => ch != '.');
                int n = Mathf.Min(width, b - a + 1);
                for (int k = 0; k < n; k++)
                    line[a + k] = tones[Mathf.Min(k, tones.Length - 1)];
                result[y] = new string(line);
            }
            return result;
        }

        /// <summary>Rows above the guard: tip, blade, ricasso. The shoulder row that widens into
        /// the guard stays iron - the edge ends where the blade does.</summary>
        static int GreatswordEdgeRows => 6 + BladeRows + 1;

        /// <summary>Three cells of bronze in the arena, one lit texel-pair then base.</summary>
        static readonly string[] BronzeEdgedRows =
            BronzeEdge(GreatswordRows, GreatswordEdgeRows, 3, "54");

        // ============================================================== dual blades
        //
        // Both grids are PALINDROMES, row by row - see the note at the two call sites above for
        // why that is load-bearing rather than tidiness: it is what lets the off-hand copy
        // (unmirrored, per SetWeaponSplit) pass for a second blade genuinely held there.

        /// <summary>
        /// Void Fang - a slim double-edged dagger with a glowing centre vein, tapering to a point
        /// at both the blade and the pommel so the two ends echo each other. Void material
        /// (Palette.Void, a violet-black metal - a MATERIAL, not the black-diamond TIER, per the
        /// palette's own rule) with a leather-wrapped grip.
        /// </summary>
        static string[] BuildVoidFang() => new[]
        {
            "........hh........",   // tip
            "......hllllh......",
            "....hllbbbbllh....",
            "..hllbbbbbbbbllh..",
            "ddllbbbbhhbbbblldd",   // blade body - lit bevel, dark edges, a glowing centre vein
            "ddllbbbbhhbbbblldd",
            "ddllbbbbhhbbbblldd",
            "ddllbbbbhhbbbblldd",
            "ddllbbbbhhbbbblldd",
            "ddllbbbbhhbbbblldd",
            "ddllbbbbhhbbbblldd",
            "ddllbbbbhhbbbblldd",
            "ddllbbbbhhbbbblldd",   // (five rows added: the disc standard, DiscHeightCells)
            "ddllbbbbhhbbbblldd",
            "ddllbbbbhhbbbblldd",
            "ddllbbbbhhbbbblldd",
            "ddllbbbbhhbbbblldd",
            "llllllllllllllllll",   // crossguard
            "lbbbbbbbbbbbbbbbbl",
            "dddddddddddddddddd",
            "ssssssssssssssssss",
            "...ddbbbbbbbbdd...",   // collar
            ".....KKBBBBKK.....",   // grip (leather ramp, uppercase)
            ".....KKBBBBKK.....",
            ".....KKBBBBKK.....",
            ".....KKBBBBKK.....",
            ".......bbbb.......",   // pommel neck
            "........hh........",   // pommel cap - the second fang, echoing the tip
        };

        static readonly string[] VoidFangRows = BuildVoidFang();

        /// <summary>Centre of the grip band - see the note on GreatswordGrip.</summary>
        static readonly Vector2Int VoidFangGrip = new(8, 23);

        /// <summary>
        /// Sunfire Cleaver - heavier and wider than Void Fang, a broad flat-headed cleaver rather
        /// than a tapered point, on the Gold tier's own ramp with the same leather grip. Widest at
        /// the head rather than at a point, but still a straight, double-bevelled edge rather than
        /// a single curved one, for the same off-hand-symmetry reason as Void Fang above.
        /// </summary>
        static string[] BuildSunfireCleaver() => new[]
        {
            "..hhllllllllllllhh..",  // broad flat head, not a point - a cleaver, not a dagger
            ".hhbbbbbbbbbbbbbbhh.",
            "hbbbbbbbbbbbbbbbbbbh",
            "dlbbbbbbbbbbbbbbbbld",  // belly - widest point of the cleaver
            "dlbbbbbbbbbbbbbbbbld",
            "dlbbbbbbbbbbbbbbbbld",
            "dlbbbbbbbbbbbbbbbbld",
            "dlbbbbbbbbbbbbbbbbld",
            "dlbbbbbbbbbbbbbbbbld",
            "dlbbbbbbbbbbbbbbbbld",  // (six rows added: the disc standard, DiscHeightCells)
            "dlbbbbbbbbbbbbbbbbld",
            "dlbbbbbbbbbbbbbbbbld",
            "dlbbbbbbbbbbbbbbbbld",
            "dlbbbbbbbbbbbbbbbbld",
            "dlbbbbbbbbbbbbbbbbld",
            "..dbbbbbbbbbbbbbbd..",  // taper in toward the guard
            "....bbbbbbbbbbbb....",
            "llllllllllllllllllll", // crossguard
            "lbbbbbbbbbbbbbbbbbbl",
            "dddddddddddddddddddd",
            "ssssssssssssssssssss",
            "...ddbbbbbbbbbbdd...", // collar
            "....KKBBBBBBBBKK....", // grip (leather ramp, uppercase) - wider band, a heavier hilt
            "....KKBBBBBBBBKK....",
            "....KKBBBBBBBBKK....",
            "....KKBBBBBBBBKK....",
            "........bbbb........", // pommel neck
            ".......hh..hh.......", // pommel cap - two lobes, not one point: a cleaver's butt is
                                     // blunt, so it echoes the flat head rather than Void Fang's
                                     // single taper.
        };

        static readonly string[] SunfireCleaverRows = BuildSunfireCleaver();

        static readonly Vector2Int SunfireCleaverGrip = new(9, 23);

        // ============================================================== Zanmato
        //
        // The katana - a Black Diamond weapon, one half of a pair with the Zanmato Saya relic.
        // Power 0: the mechanic (the sheath-and-draw finisher, and a non-boss it kills left in two
        // halves) lives on the SIGNATURE, granted by either piece; the weapon is bespoke art and
        // nothing else, the same split Phantom and Blood Blade already use.
        //
        // BREAKS "EVERY SWORD THE SAME HEIGHT", signed off explicitly. The first build matched the
        // 1.0667 envelope with a 10-texel blade and read as a MEAT CLEAVER - short and broad. A
        // katana is a LINE: the blade is 6 texels wide now (a third of the greatsword's) and runs
        // the greatsword's full blade length, over a long wrapped tsuka. That lands it at
        // 0.4267 x 1.1467 (texture 32x86), about 7% taller. Nothing derives a layout constant from
        // a sword's height - FitPreviewCamera fits whatever is equipped, and the grip pivot is
        // counted from the tang so the extra length extends the TIP upward and never moves the
        // fist - so the parity was a consistency nicety, not a load-bearing invariant.
        //
        // DELIBERATELY NOT MIRROR-SYMMETRIC. Single edge (lit) one side, spine (dark) the other,
        // and one continuous saber curve (sori) drifting the tip toward the edge - like Lumen's
        // vein the asymmetry IS the weapon, so the mirror-diff check every straight sword passes
        // is expected to fail on the blade. The tsuba, fuchi, tsuka and kashira mirror clean.
        //
        // ONE idea, per the cosmetic-weapon restraint rule: a straight hamon (temper line) two
        // texels inside the edge. Plain steel, indigo ito wrap, small dark-iron tsuba otherwise.

        // Reach, not stubbiness - and now settled by the same accounting every other sword uses
        // (BladeRowsFor), so this weapon is EXACTLY the family's height rather than the 7% over it
        // used to carry. The note above about breaking parity described the old hand-counted
        // build; there is no longer a number here that can drift.
        static readonly int ZanmatoBladeRows = BladeRowsFor(6 + 2 + ZanmatoTsubaRows + 1 + ZanmatoGripRows + ZanmatoClawRows);

        /// <summary>The tsuba's rows: an OVAL, a round guard seen a little from above - five, since
        /// an oval three rows tall is a blob. The two extra came off the blade.</summary>
        const int ZanmatoTsubaRows = 5;
        const int ZanmatoBladeWidth = 6;    // still the slimmest blade here: this is the cleaver fix
        // The gold CLAW at the pommel takes five rows, paid for by the tsuka's own length (14 -> 12)
        // rather than the blade's, so the blade, the pivot and every number the sheathing
        // animation reads off them are exactly what they were.
        const int ZanmatoGripRows   = 17;   // +5 with every grip, the blade untouched
        const int ZanmatoClawRows   = 5;
        /// <summary>
        /// Cells the tip stands off the line of the tang, toward the spine. ZERO - STRAIGHT, and
        /// that is a decision, not a gap: a curve was tried at 2 cells (whole length, steepening
        /// to the tip, arena rounding it into a proper pixel-art run) and at arena size the two
        /// one-cell steps read as a BROKEN blade. The arena's cells are 1.5 screen px at 1080p;
        /// finer would be sub-pixel and shimmer, and a curve in the menu alone would move the
        /// silhouette two cells between screens. The katana has to be read from its other cues.
        /// The machinery (ZanmatoCurve) is kept, so the day the arena gains density this is one
        /// number.
        /// </summary>
        const float ZanmatoSori = 0f;

        /// <summary>
        /// The blade's offset from straight at row <paramref name="y"/>, in cells: nothing at the
        /// habaki, <see cref="ZanmatoSori"/> at the point, and steepening toward the tip (a
        /// curve that bends most in the upper blade - saki-zori). The arena rows round this; the
        /// menu art follows it exactly.
        /// </summary>
        static float ZanmatoCurve(float y)
        {
            float s = Mathf.Clamp01(1f - y / (6f + ZanmatoBladeRows));    // 0 at the habaki, 1 at the tip
            return ZanmatoSori * Mathf.Pow(s, 1.7f);
        }

        static Dictionary<char, Color> ZanmatoPal()
        {
            var map = Palette.Of(new Palette.Ramp(new Color(0.60f, 0.66f, 0.74f)),   // cool steel
                                 new Palette.Ramp(new Color(0.16f, 0.17f, 0.32f)),   // indigo lacquer
                                 // RED braided ito - the tsuka's cord, after the user's reference
                                 new Palette.Ramp(new Color(0.74f, 0.13f, 0.12f), lift: 0.34f, shade: 0.42f));
            // The kashira's GOLD claw. Custom letters: three ramps are spoken for.
            map['W'] = new Color(1.00f, 0.93f, 0.62f);
            map['Y'] = new Color(0.97f, 0.79f, 0.34f);
            map['G'] = new Color(0.82f, 0.60f, 0.20f);
            map['O'] = new Color(0.52f, 0.34f, 0.10f);
            return map;
        }

        static string[] BuildZanmatoSword()
        {
            const int W = 28;
            var rows = new List<string>(84);

            char[] Blank()
            {
                var a = new char[W];
                for (int i = 0; i < W; i++) a[i] = '.';
                return a;
            }
            string Stamp(int at, string frag)
            {
                var a = Blank();
                for (int i = 0; i < frag.Length && at + i < W; i++)
                    if (frag[i] != ' ') a[at + i] = frag[i];
                return new string(a);
            }

            // THE SORI: a real curve now, the whole length of the blade - see ZanmatoCurve. Every
            // row takes its left edge from that one function, rounded, and because the curve
            // steepens toward the tip the rounding hands out steadily SHORTER runs: long steps
            // near the habaki, short ones near the point, which is what a clean pixel-art arc is.
            // The first attempt crammed all its drift into the last quarter and got irregular
            // steps that read as a chipped edge; that was the stepping, not the density.
            int guardLeft = 14 - ZanmatoBladeWidth / 2;   // 11: cols 11..16, centred on the grip
            int LeftAt(float y) => guardLeft + Mathf.RoundToInt(ZanmatoCurve(y));

            // kissaki - the RIGHT way round: the spine (right) runs straight up to the point and
            // the EDGE curves up to meet it (the fukura), lit along its curve. The first version
            // had the point on the edge and the spine sweeping in - a Western tip on a katana.
            // Its last row is the YOKOTE: the hard line across the blade where the point begins,
            // from the edge to the ridge - after the curve, the most katana thing a blade carries.
            string[] kiss = { "s", "hs", "hds", "hdds", "hldds", "sssdds" };
            for (int i = 0; i < kiss.Length; i++)
                rows.Add(Stamp(LeftAt(i) + ZanmatoBladeWidth - kiss[i].Length, kiss[i]));

            // the blade - 6 wide. A HARD SHINOGI, not a gradient: the two edge texels are the
            // polished ha (the hamon's band), the ridge steps down to light, then a hard drop to
            // the dark flat and the deep spine. The break at the ridge says single-edged.
            const string X = "hhldds";
            for (int i = 0; i < ZanmatoBladeRows; i++)
            {
                int left = LeftAt(6 + i);
                var a = Blank();
                for (int k = 0; k < X.Length; k++) a[left + k] = X[k];
                rows.Add(new string(a));
            }

            // habaki - the blade collar, GOLD, one texel proud of the blade each side: a bright
            // stripe at the base of the blade that reads "katana" at any distance, and the same
            // gold as the claw at the other end.
            rows.Add(Stamp(guardLeft - 1, "OYWYYGGO"));
            rows.Add(Stamp(guardLeft - 1, "OGGGGGOO"));
            // tsuba - an OVAL: a round guard seen a little from above, which is what makes it read
            // as a katana's rather than a crossbar. The flat bar with pointed tips it replaces was
            // a Western guard. Iron, the top face catching the light, a dark rim.
            for (int i = 0; i < ZanmatoTsubaRows; i++)
            {
                var a = Blank();
                float dy = i - (ZanmatoTsubaRows - 1) * 0.5f;
                const float hw = 5.8f, hh = 2.45f;
                for (int x = 0; x < W; x++)
                {
                    float dx = x - 13.5f;
                    float e = (dx / hw) * (dx / hw) + (dy / hh) * (dy / hh);
                    if (e >= 1f) continue;
                    // the rim, then the top face lit toward the upper left
                    a[x] = e > 0.62f ? (dy < 0f && dx < 0f ? 'd' : 's')
                         : dy < 0f ? (dx < 0f ? 'l' : 'b') : 'd';
                }
                rows.Add(new string(a));
            }
            // fuchi - the collar at the top of the grip, one texel proud of the wrap each side
            rows.Add(Stamp(10, "dddddddd"));

            // tsuka and kashira - the red braid and the gold claw, from ZanmatoHiltTexel so the
            // menu art draws the same shapes finer.
            int gripTop = rows.Count;
            for (int i = 0; i < ZanmatoGripRows + ZanmatoClawRows; i++)
            {
                var a = Blank();
                for (int x = 0; x < W; x++) a[x] = ZanmatoHiltTexel(x, gripTop + i, fine: false);
                rows.Add(new string(a));
            }
            return rows.ToArray();
        }

        static readonly string[] ZanmatoRows = BuildZanmatoSword();

        static int ZanmatoGripTop => 6 + ZanmatoBladeRows + 2 + ZanmatoTsubaRows + 1;
        static float ZanmatoClawTop => ZanmatoGripTop + ZanmatoGripRows - 0.5f;
        const float ZanmatoAxis = 13.5f;                    // cols 11..16, the tsuka's centre

        /// <summary>
        /// The tsuka and kashira as a field in cells: RED ITO braided into the diamond lattice of a
        /// katana's wrap - two cords crossing, dark red showing through the diamonds between them,
        /// lit on the left - and a GOLD CLAW for a kashira: a cap wider than the wrap, then a talon
        /// hooking down and round to the edge side.
        /// </summary>
        static char ZanmatoHiltTexel(float x, float y, bool fine)
        {
            float dx = x - ZanmatoAxis;

            if (y < ZanmatoClawTop)
            {
                if (y < ZanmatoGripTop - 0.5f || Mathf.Abs(dx) > SwordGripWidth / 2f) return '.';
                // The braid: two cords crossing on the diagonal, the diamonds between them dark.
                float per = fine ? 2.6f : 2.6f;
                float a = (y + dx) / per, b = (y - dx) / per;
                float fa = a - Mathf.Floor(a), fb = b - Mathf.Floor(b);
                float w = fine ? 0.34f : 0.45f;
                bool cordA = fa < w, cordB = fb < w;
                float u = dx / (SwordGripWidth / 2f);
                if (!cordA && !cordB) return fine && (fa < w + 0.06f || fb < w + 0.06f) ? '1' : '2';
                // Where the cords cross, the one on top is lit; along a cord, lit to the left.
                if (fine && (fa < 0.08f || fb < 0.08f)) return '3';
                if (u < -0.4f) return '5';
                if (u > 0.45f) return '3';
                return fine && cordA && cordB ? '5' : '4';
            }

            float sd = ZanmatoClawSd(x, y);
            if (sd >= 0f) return '.';
            const float e = 0.3f;
            float nx = ZanmatoClawSd(x + e, y) - ZanmatoClawSd(x - e, y);
            float ny = ZanmatoClawSd(x, y + e) - ZanmatoClawSd(x, y - e);
            float nl = Mathf.Sqrt(nx * nx + ny * ny);
            if (nl > 1e-4f) { nx /= nl; ny /= nl; }
            float lit = nx * -0.55f + ny * -0.83f;
            if (-sd < (fine ? 0.35f : 0.7f))
            {
                if (lit > 0.5f) return fine ? 'W' : 'Y';
                if (lit > 0.1f) return 'Y';
                if (lit < -0.4f) return 'O';
            }
            return 'G';
        }

        /// <summary>The claw: a gold cap over the wrap's end, and a talon curving down from it and
        /// hooking round to the edge side, tapering to a point.</summary>
        static float ZanmatoClawSd(float x, float y)
        {
            float top = ZanmatoClawTop, dx = x - ZanmatoAxis;
            float cap = Mathf.Max(Mathf.Abs(dx) - (SwordGripWidth / 2f + 0.9f), Mathf.Abs(y - (top + 0.8f)) - 0.8f);

            // The talon: a quadratic curve - down from the cap, then round to the left - sampled.
            var p0 = new Vector2(ZanmatoAxis + 1.2f, top + 1.2f);
            var p1 = new Vector2(ZanmatoAxis + 0.9f, top + ZanmatoClawRows + 0.2f);
            var p2 = new Vector2(ZanmatoAxis - 3.4f, top + ZanmatoClawRows - 0.6f);
            float best = 99f;
            var q = new Vector2(x, y);
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f, it = 1f - t;
                var c = it * it * p0 + 2f * it * t * p1 + t * t * p2;
                float d = (q - c).magnitude - Mathf.Lerp(1.55f, 0.2f, t);
                if (d < best) best = d;
            }
            return Mathf.Min(cap, best);
        }

        /// <summary>
        /// Menu art at TRUE <see cref="MenuPpu"/>: the blade, habaki, tsuba and fuchi exactly as the
        /// derived menu pass always drew them, and the braid and claw redrawn over them at four
        /// samples a cell.
        /// </summary>
        static string[] BuildZanmatoSwordDetail()
        {
            const int k = EmberDetailScale;
            var rows = PixelDetail.Enrich(Double2x(ZanmatoRows), k / FineUpscale);

            // The BLADE, redrawn smooth along the same curve - the one place the menu's four
            // samples a cell buy something the arena cannot have: a curve with no steps at all.
            int bladeEnd = (6 + ZanmatoBladeRows) * k;
            for (int Y = 0; Y < bladeEnd; Y++)
            {
                var line = rows[Y].ToCharArray();
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++)
                    line[X] = ZanmatoBladeTexel((X + 0.5f) / k - 0.5f, y);
                rows[Y] = new string(line);
            }

            int from = ZanmatoGripTop * k;
            for (int Y = from; Y < rows.Length; Y++)
            {
                var line = rows[Y].ToCharArray();
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++)
                    line[X] = ZanmatoHiltTexel((X + 0.5f) / k - 0.5f, y, fine: true);
                rows[Y] = new string(line);
            }
            return rows;
        }

        /// <summary>
        /// The blade as a field in cells, for the menu art: the same six-cell section as the arena
        /// rows - polished edge, ridge, dark flat, spine - riding <see cref="ZanmatoCurve"/>
        /// exactly, with the kissaki's spine sweeping in to a point on the edge, and a HAMON: the
        /// temper line between the polished edge and the body, gently wavy, which the arena's
        /// two-cell edge band can only suggest.
        /// </summary>
        static char ZanmatoBladeTexel(float x, float y)
        {
            float left = 11f - 0.5f + ZanmatoCurve(y);                  // the edge
            float u = x - left;                                         // 0 at the edge, 6 at the spine
            float width = ZanmatoBladeWidth;
            if (u > width || y < -0.5f) return '.';
            // The kissaki: the spine runs straight to the point, the edge CURVES up to meet it,
            // and the yokote crosses the blade where the point begins.
            if (y < 5.5f)
            {
                float f = Mathf.Sqrt(Mathf.Clamp01((y + 0.5f) / 5.5f));
                float from = width * (1f - f);
                if (u < from) return '.';
                if (u < from + 0.35f) return 'h';                       // the curving edge, lit
                return u > width - 0.4f ? 's' : u > 3.5f ? 'd' : 'l';
            }
            if (u < 0f) return '.';
            if (y < 5.9f && u < 2.2f) return 's';                       // the yokote

            float hamon = 1.35f + 0.22f * Mathf.Sin(y * 1.3f) + 0.10f * Mathf.Sin(y * 3.1f + 1f);
            if (u < 0.3f) return 'h';                                   // the polished edge
            if (u < hamon) return u > hamon - 0.18f ? 'l' : 'h';        // the tempered band, and its line
            if (u < 2f) return 'l';                                     // up to the ridge
            if (u < 2.15f) return 'h';                                  // the shinogi, caught
            if (u < 4f) return 'd';                                     // the flat
            return u < width - 0.3f ? 's' : 'd';                        // the spine, its back edge
        }

        static readonly string[] ZanmatoSwordDetail = BuildZanmatoSwordDetail();
        static Vector2Int ZanmatoDetailGrip => new(ZanmatoGrip.x * EmberDetailScale, ZanmatoGrip.y * EmberDetailScale);

        // Front fist in ZanmatoRows: 6 tip rows, the blade, 2 habaki, 3 tsuba, the fuchi, then
        // two rows into the wrap. Derived like GreatswordGrip, for the same reason.
        static readonly Vector2Int ZanmatoGrip =
            new(13, GripCentre(6 + ZanmatoBladeRows + 2 + ZanmatoTsubaRows + 1, ZanmatoGripRows));

        // The saya relic - AN ACTUAL SCABBARD, not a heraldic mark.
        //
        // The other two Black Diamond relics (Phantom Mark, Blood Vial) are emblems in a diamond
        // frame, and copying that shape here failed: blown up, a flat indigo block inside a bright
        // frame read as a window, not a sheath. A relic may be any shape, so this one is simply
        // the object.
        //
        // SIZED TO THE BODY, NOT TO THE BLADE. It is worn, so it is scaled as a person's scabbard
        // - hip to about the shin - rather than to the 1.15-unit sword it nominally holds. That
        // is the foreshortening allowed for from the start.
        //
        // WORN ON THE LEFT HIP, OPPOSITE THE SWORD HAND - the rest carry is in the right hand
        // (arm.front facing right, at -x), so this is authored at +x and the rig mirrors it with
        // the carry facing left (GearItem.WornOnLeftHip). The right hand crosses the body to put
        // the blade in it (ICharacterRig.SetHandTarget). Drawn leaning,
        // the way a sword thrust through a belt actually hangs; a vertical stick on the hip reads
        // as a stray prop.
        //
        // THROUGH THE BELT, NOT BESIDE IT. It used to hang off offX 12 - wholly outside the torso,
        // its mouth out in open air at chest height - so it read as a prop propped against the
        // leg rather than as something worn. The mouth is now PLACED (SayaMouthAt) on the belt at
        // the pouch's own hip, and the rest of the sprite follows from it.
        //
        // AT THE BODY'S DENSITY, like the pouch every relic is sized against (see HipRelic) - it
        // was drawn as a weapon, 2x2 texels a cell. The finer grid also steps the lean one texel
        // at a time instead of two.
        //
        // The mouth is DARK inside: the blade is in the character's hand, so the scabbard the
        // player sees during ordinary play is empty. The finisher is what puts a blade back in it.

        /// <summary>
        /// The mouth's opening, in body texels: EXACTLY THE TSUKA'S WIDTH, because the finisher
        /// leaves the handle standing on it and a hilt that cannot fit through its own mouth reads
        /// as a mistake however well the two are lined up. Derived from SwordGripWidth, which is
        /// what the braid is drawn to - this was a literal 6 cells, the grip's width before the
        /// family's grips were thinned to 4, and the whole scabbard was built half again too wide
        /// around it.
        /// </summary>
        const int SayaBore  = SwordGripWidth * FineUpscale;

        /// <summary>The lacquered tube: the bore itself - a scabbard is barely proud of what it
        /// holds - with the steel fittings a texel proud of it either side.</summary>
        const int SayaTube  = SayaBore;

        /// <summary>Belt to just below the knee (the leg is 48 texels, the knee half way). Was 48
        /// from the chest to the shin: a worn scabbard is a detail, not a second sword.</summary>
        const int SayaRows  = 36;

        /// <summary>Texels of drift between the mouth and the tip - 18 degrees, the old angle.</summary>
        const int SayaLean  = 12;

        /// <summary>
        /// The mouth's centre, in TORSO-LOCAL BODY TEXELS (0 is the hip): at the pouch's own hip
        /// (RelicHipX), one texel above the belt's outlined top, so the sageo cord sits across the
        /// strap and the steel mouth stands just proud of it.
        /// </summary>
        static readonly Vector2Int SayaMouthAt =
            new(Mathf.RoundToInt(RelicHipX * PrimitiveCharacterRig.Proportions.TexelsPerCell), 12);

        /// <summary>
        /// Which way the mouth leans: -1 is IN toward the buckle, with the tip swinging out past
        /// the hip. +1 (mouth outward, tip back) was the old lean and was rendered against this one
        /// once the mouth sat on the belt: the tube then crossed the tasset and both legs and read
        /// as strapped over the thighs. Leaning out, it clears the legs and breaks the silhouette
        /// where a worn scabbard does, and the finisher's handle stands up across the belly the way
        /// a sheathed katana's does. Everything the sheathing animation reads follows the sign.
        /// </summary>
        const int SayaMouthSide = -1;

        const int SayaMargin = 1;                                     // room for the fittings
        const int SayaW = SayaMargin + SayaLean + SayaTube + SayaMargin;

        static string[] BuildZanmatoSaya()
        {
            var rows = new List<string>(SayaRows);

            int LeftAt(int row)
            {
                float t = row / (float)(SayaRows - 1);                 // 0 at the mouth
                return SayaMargin + Mathf.RoundToInt(SayaLean * (SayaMouthSide > 0 ? 1f - t : t));
            }

            // inset -1 puts the metal fittings one texel proud of the lacquered tube each side
            string Row(int row, string frag, int inset = 0)
            {
                var a = new char[SayaW];
                for (int i = 0; i < SayaW; i++) a[i] = '.';
                int at = LeftAt(row) + inset;
                for (int i = 0; i < frag.Length; i++)
                {
                    int c = at + i;
                    if (c >= 0 && c < SayaW) a[c] = frag[i];
                }
                return new string(a);
            }

            // A fitting a texel proud of the tube either side: rim, body, rim.
            string Fit(char rim, string body) => rim + body + rim;
            string Run(char c, int n) => new string(c, n);

            int r = 0;
            // koiguchi - the steel mouth. Dark down the middle because it is standing EMPTY.
            for (int i = 0; i < 3; i++) rows.Add(Row(r++, Fit('s', Run('k', SayaBore)), -1));
            for (int i = 0; i < 2; i++) rows.Add(Row(r++, Fit('s', "dl" + Run('b', SayaBore - 4) + "ld"), -1));
            // sageo - the cord, bound just under the mouth, across the belt
            for (int i = 0; i < 2; i++) rows.Add(Row(r++, Run('K', SayaTube + 2), -1));
            rows.Add(Row(r++, Fit('S', Run('K', SayaTube)), -1));
            // the lacquered tube, lit from the edge side
            const int Kojiri = 6;
            int tube = SayaRows - r - Kojiri;
            string lacquer = "L" + Run('B', SayaTube - 4) + "DSK";
            for (int i = 0; i < tube; i++) rows.Add(Row(r++, lacquer));
            // kojiri - the steel end cap, rounding off
            for (int i = 0; i < 3; i++) rows.Add(Row(r++, Fit('s', "dl" + Run('b', SayaTube - 4) + "ld"), -1));
            rows.Add(Row(r++, "sd" + Run('b', SayaTube - 4) + "ds"));
            rows.Add(Row(r++, "sd" + Run('b', SayaTube - 6) + "ds", 1));
            rows.Add(Row(r++, "s" + Run('d', SayaTube - 6) + "s", 2));

            return rows.ToArray();
        }

        static readonly string[] ZanmatoSayaRows = BuildZanmatoSaya();

        // ---- geometry the SHEATHING ANIMATION reads ----
        //
        // DERIVED from the same constants that draw the grids, never re-typed. The blade has to
        // slide along the scabbard's actual axis and stop with its grip at the actual mouth; two
        // hand-tuned numbers would disagree the first time either grid was touched, and the
        // failure mode is a sword sinking into a hip at the wrong angle.
        //
        // In the grid's OWN units, divided by the density it is actually drawn at. The three
        // numbers below used to divide grid cells by FinePpu while the grids were block-doubled
        // on their way to a sprite, so every one came out HALF: the handle seated half way down
        // the scabbard's upper half instead of on its mouth.

        /// <summary>The mouth's centre relative to the sprite's centre, in body texels. The grid is
        /// symmetric about its own lean, so this is half the lean across and half the rows up.</summary>
        static Vector2 SayaMouthFromCentre => new(SayaMouthSide * SayaLean * 0.5f, SayaRows * 0.5f);

        /// <summary>The saya layer's Offset, in layout cells: wherever puts the mouth on SayaMouthAt.</summary>
        static Vector2 SayaOffsetCells
            => new(PrimitiveCharacterRig.Proportions.Cells(SayaMouthAt.x - SayaMouthFromCentre.x),
                   PrimitiveCharacterRig.Proportions.Cells(SayaMouthAt.y - SayaMouthFromCentre.y));

        /// <summary>The scabbard's lean off vertical, in degrees, signed toward the MOUTH (positive
        /// is +x). The sheathing blade rotates to this (plus 180, since it goes in point-first)
        /// before it slides.</summary>
        public static float SayaAxisDegrees
            => Mathf.Atan2(SayaMouthFromCentre.x, SayaMouthFromCentre.y) * Mathf.Rad2Deg;

        /// <summary>Centre of the saya sprite to its MOUTH, in world units along the axis. The
        /// renderer's transform is the sprite's centre (the pivot is centred), so a caller adds
        /// this along the axis to find where the blade goes in.</summary>
        public static float SayaHalfLength => SayaMouthFromCentre.magnitude / BodyPpu;

        /// <summary>How far the blade reaches ABOVE its grip pivot, in world units - the distance
        /// it has to travel down the axis to be fully inside. Counted off the grid: the pivot sits
        /// ZanmatoGrip.y rows from the top, and every row is drawn FineUpscale texels tall.</summary>
        public static float ZanmatoBladeAboveGrip => ZanmatoGrip.y * FineUpscale / FinePpu;

        // NOTE: the blade is ~3.5x the scabbard's length and deliberately so - the saya is scaled
        // to a BODY, and one sized to the real blade would hang past the character's feet. It is
        // not squashed to fit; KatanaSheathe cuts the sprite down to whatever is still outside
        // the mouth as it goes in. See that file.

        /// <summary>
        /// Column/row of the front fist in <see cref="GreatswordRows"/>: the sprite's pivot.
        /// Counted from the top - 6 tip rows, the blade, 2 ricasso rows, 4 crossguard rows, the
        /// collar, then one grip row.
        /// </summary>
        /// <summary>Centre of the disc's grip bar - see the note on GreatswordGrip.</summary>
        /// <summary>
        /// Where a disc sits relative to the SHOULDER pivot it hangs from.
        ///
        /// The gloves mark the hand at (0,-12); the discs were at (4,-4), eight texels above it and
        /// pulled inward, so they rode the shoulder and read as floating in front of the chest
        /// rather than being gripped. Dropped to the hand and pushed outward so the ring clears
        /// the torso armour and the head, and so the hand itself sits in the ring's hollow centre -
        /// which is what actually sells "holding a chakram", since you see the fist through the hole.
        /// (Doubled alongside every other offX/offY when LayoutUnit moved to 75 - PixelSprite.Px
        /// always divides by the global LayoutUnit, not the sprite's own ppu, so this positional
        /// constant had to move even though the disc's own art and pivot, both at FinePpu, did not.)
        /// </summary>
        const float DiscHandX = 0f;
        const float DiscHandY = -12f;

        static readonly Vector2Int DiscGrip = new(DiscHeightCells / 2, DiscHeightCells / 2);   // centre of the grip bar

        /// <summary>
        /// EVERY DISC IS THIS TALL, in grid cells (FinePpu, before the 2x upscale) - 28, the
        /// tallest disc when the standard was set (Briar Rose), so every other disc grew to it. The disc class's answer to the greatswords' shared height: a pair that
        /// changes size from design to design reads as a different weapon class, not a different
        /// skin. Width is free - an oval mouth, two crescents - but not height.
        ///
        /// A mechanism, not a convention: <see cref="Disc"/> warns about any disc whose grid is
        /// another height unless it is in <see cref="DiscHeightExceptions"/> with its reason.
        /// Fix a disc by building its field or grid to this height, never by touching Size.
        /// </summary>
        const int DiscHeightCells = 28;

        /// <summary>The discs allowed past <see cref="DiscHeightCells"/>, and why - the same kind
        /// of documented exception Saint's wings and Emberline's horns are among the swords.</summary>
        static readonly Dictionary<string, string> DiscHeightExceptions = new()
        {
            // Fire's engulfing flames rise past the ring: when the four elements combine in the
            // Armillary's finisher, Earth's rim is the outermost band, and flames stopping at the
            // same edge would vanish behind it (DemoGear.Armillary, ArmFlameMax). The Armillary
            // itself is here for its main hand - the Rising half, which carries the fire.
            ["ignis_band"] = "flames past the ring",
            ["armillary_discs"] = "flames past the ring (the Rising half)",
            // The ring is 36 cells - the size the user moved discs to - and the mane breaks past
            // it by the user's call (DemoGear.LionOuroboros). Drop this once the standard is 36.
            ["king_and_queen_discs"] = "a 36-cell ring, the mane past it",
        };

        /// <summary>
        /// A plain longbow: a two-texel stave curving away from a taut string, wrapped at the
        /// grip. Hand-drawn rather than generated - a computed curve risks a topologically broken
        /// shape with no render to check it against, where a straight taper is forgiving to get
        /// right on the first pass. First pass in every sense: silhouette only, not yet iterated
        /// against a live render the way the discs and the relics were.
        /// </summary>
        // Mirrored from the first pass: the string sat on the OUTER edge (column 7, away from
        // the body) and the stave's belly on the inner columns, so the bow rendered with its
        // string facing away from the character - backwards from how a bow is actually held,
        // string toward the archer, belly toward the target. Column order reversed so the
        // string (k) is now on column 0, nearest the body.
        static string[] BuildBow() => new[]
        {
            "ld......",
            "ld......",
            "kld.....",
            "kld.....",
            "kld.....",
            "kld.....",
            "k.ld....",
            "k.ld....",
            "k.ld....",
            "k.ld....",
            "k..LD...",
            "k..LD...",
            "k..LD...",
            "k..LD...",
            "k..LD...",
            "k..LD...",
            "k..LD...",
            "k..LD...",
            "k.ld....",
            "k.ld....",
            "k.ld....",
            "k.ld....",
            "kld.....",
            "kld.....",
            "kld.....",
            "kld.....",
            "ld......",
            "ld......",
        };

        static readonly string[] BowRows = BuildBow();

        /// <summary>
        /// Pushed low in the wrapped band rather than dead centre - see the note on
        /// GreatswordGrip for the counted-from-the-string-literal convention. Column mirrored
        /// to 3 (was 4) along with the grid reversal above.
        ///
        /// A real bow is gripped near its own middle, but every OTHER weapon in this rig hangs
        /// from a pivot near one end (the greatsword's sits close to the pommel, with the whole
        /// blade extending one way from it), because the shared arm-pivot math was built around
        /// "one end at the hand, the rest reaches away" - a genuinely centred pivot instead reads
        /// as a shape straddling the hand and drew as a diagonal stripe across the torso rather
        /// than a held object. Moved low so most of the stave reaches away from the hand the same
        /// way a blade does; true to a bow's own anatomy less than it is to how this rig holds
        /// anything at all.
        /// </summary>
        static readonly Vector2Int BowGrip = new(3, 20);

        static readonly Vector2Int GreatswordGrip = new(13, GripCentre(6 + BladeRows + 2 + 4 + 1, SwordGripRows));



        // ---------------------------------------------------------------- menu art
        //
        // Weapons only, for now. Both are computed rather than written out for the same reason
        // BuildGreatsword always was - at this size the shaped rows are a handful among hundreds
        // of identical ones - and both spend their extra texels on ONE idea each, not on fussy
        // noise: a bevel across the disc's ring, a fuller down the sword's blade.

        /// <summary>
        /// The plain disc - a bladed ring, hollow centre, a leather grip bound across it - at
        /// N x N, for BOTH densities: DiscHeightCells for the arena, twice that for the menu, the
        /// same world size either way. It was hand-typed at 24 for the arena and built here only
        /// for the menu; the disc standard moved to 28 and a typed grid cannot follow it.
        ///
        /// The HOLE is the constraint, not the ring: at arena size the auto-outline strokes the
        /// inner edge too, so the hollow is sized to survive its own outline (what sells a chakram
        /// is the fist seen through the middle). The menu's extra texels go into a BEVEL - a lit
        /// outer lip and a shaded inner one - which the arena ring has no width for.
        /// </summary>
        static string[] BuildDiscAt(int N, params float[] notches)
        {
            float k = N / 24f;                                   // cells per ORIGINAL 24-grid cell
            // Measured off the 24x24 disc rather than guessed: its hollow runs to about r 6.2
            // in a grid whose outer radius is 12, so 0.517 of the way out. Set by eye instead,
            // the menu disc had a visibly tighter hole than the arena one - which would show as
            // the ring seen through it shrinking the moment the character screen opened.
            float Inner = 0.517f * (N / 2f);
            float Outer = N / 2f;
            float Centre = (N - 1) / 2f;

            var rows = new string[N];
            for (int y = 0; y < N; y++)
            {
                var sb = new System.Text.StringBuilder(N);
                for (int x = 0; x < N; x++)
                {
                    float dx = x - Centre, dy = y - Centre;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    // A notched rim needs the OUTER radius to vary with the angle - the same four
                    // wedges at every size, so arena and menu agree.
                    float rim = Outer;
                    if (notches != null && notches.Length > 0)
                    {
                        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        foreach (var a in notches)
                            if (Mathf.Abs(Mathf.DeltaAngle(ang, a)) < NotchWidth) rim = Outer - NotchDepth * k;
                    }
                    if (r > rim || r < Inner) { sb.Append('.'); continue; }

                    // lit from the upper left, shading round to the lower right - the same
                    // direction the 24x24 disc is drawn in, which is why neither is mirrored
                    float t = (dx + dy) / (2f * Outer);
                    char c = t < -0.30f ? 'l' : t < 0.05f ? 'b' : t < 0.40f ? 'd' : 's';

                    float across = (r - Inner) / (rim - Inner);
                    if (across > 0.86f) c = c switch { 'l' => 'h', 'b' => 'l', 'd' => 'b', _ => 'd' };
                    else if (across < 0.16f) c = c switch { 'l' => 'b', 'b' => 'd', _ => 's' };
                    sb.Append(c);
                }
                rows[y] = sb.ToString();
            }

            // the leather grip, bound across the hollow: four rows in the arena grid, eight in the
            // menu's, where it gets a dark binding edge instead of being one flat bar
            int s2 = Mathf.Max(1, Mathf.RoundToInt(N / (float)DiscHeightCells));   // 1 arena, 2 menu
            int barTop = N / 2 - 2 * s2, barBottom = N / 2 + 2 * s2;
            int barLeft = Mathf.RoundToInt(N * 10f / 48f), barRight = N - barLeft;
            for (int y = barTop; y < barBottom; y++)
            {
                var line = rows[y].ToCharArray();
                bool binding = y < barTop + s2 || y >= barBottom - s2;
                for (int x = barLeft; x < barRight; x++) line[x] = binding ? 'K' : 'B';
                rows[y] = new string(line);
            }
            return rows;
        }

        /// <summary>
        /// The greatsword at menu density: the same world size as the arena sprite, on a grid with
        /// twice the cells. Every section is exactly twice its counterpart in
        /// <see cref="BuildGreatsword"/> - now DERIVED from the shared envelope rather than typed
        /// out a second time, so the pivot, the reach and the grip cannot drift apart from it.
        ///
        /// The extra texels buy a FULLER - the groove down a real blade's centre - and a specular
        /// line along the lit edge. The arena grid has no room for either: at its width the blade
        /// is three cells of light against six of base, and taking two out of the middle leaves it
        /// reading as two thin blades.
        /// </summary>
        static string[] BuildGreatswordDetail()
        {
            var rows = new List<string>(BladeRows * 2 + 42);
            foreach (var r in SwordDetailTipRows()) { rows.Add(r); rows.Add(r); }
            string blade = SwordDetailBladeRow();
            for (int i = 0; i < BladeRows * 2; i++) rows.Add(blade);
            rows.Add(blade);                                                  // ricasso
            rows.Add(blade);
            string shoulder = SwordDetailFlat((SwordEdge + 1) * 2,
                                              (SwordBladeWidth - SwordEdge * 2) * 2,
                                              (SwordEdge + 1) * 2);
            rows.Add(shoulder);
            rows.Add(shoulder);
            rows.AddRange(SwordDetailGuardRows("hlbds"));                     // crossguard
            rows.AddRange(SwordDetailCollarRows('d', 'l', 'b', 's'));         // collar
            for (int i = 0; i < SwordGripRows * 2; i++)                       // grip / fists
                rows.Add(SwordDetailGripRow('K', 'B', i % 4 < 2 ? 'K' : 'S'));
            rows.AddRange(SwordDetailPommelRows("hlbs"));                     // pommel
            return rows.ToArray();
        }

        // ---- the same envelope at menu density -------------------------------------------
        //
        // Every section here is exactly twice its counterpart above, DERIVED rather than typed, so
        // the pivot, the reach and the grip land in the same place and the sword does not change
        // shape when the character screen opens. The extra texels buy exactly two things the arena
        // grid has no room for: a FULLER down the blade's centre and a specular pair along the lit
        // edge. Nothing else about the shape is allowed to differ.

        const int SwordDetailCanvas = SwordCanvas * 2;
        const float SwordDetailAxis = SwordDetailCanvas / 2f - 0.5f;
        static int SwordDetailBladeLeft => SwordBladeLeft * 2;
        static int SwordDetailBladeRight => SwordBladeRight * 2 + 1;

        /// <summary>A flat at menu density: two specular texels, then lit / base / dark.</summary>
        static string SwordDetailFlat(int lit, int bas, int dark)
            => SwordCentred("hh" + new string('l', Mathf.Max(0, lit - 2))
                            + new string('b', bas) + new string('d', dark), SwordDetailCanvas);

        /// <summary>The parallel blade, with the two-texel fuller the arena grid cannot hold.</summary>
        static string SwordDetailBladeRow()
        {
            int edge = SwordEdge * 2, bas = (SwordBladeWidth - SwordEdge * 2) * 2;
            int half = (bas - 2) / 2;
            return SwordCentred("hh" + new string('l', edge - 2)
                                + new string('b', half) + "ss" + new string('b', bas - 2 - half)
                                + new string('d', edge), SwordDetailCanvas);
        }

        static string[] SwordDetailTipRows()
        {
            var bands = SwordTipBands();
            var rows = new string[bands.Length];
            for (int i = 0; i < rows.Length; i++)
                rows[i] = i == rows.Length - 1
                    ? SwordDetailBladeRow()          // the fuller opens on the last tip row
                    : SwordDetailFlat(bands[i].Lit * 2, bands[i].Base * 2, bands[i].Dark * 2);
            return rows;
        }

        /// <summary>Eight rows - the arena grid's four crossguard rows at twice the height.
        /// <paramref name="ramp"/> is five tones, lightest first.</summary>
        static string[] SwordDetailGuardRows(string ramp)
        {
            int g = SwordGuardWidth * 2;
            string S(string s) => SwordCentred(s, SwordDetailCanvas);
            return new[]
            {
                S(new string(ramp[0], g)),
                S(ramp[0] + new string(ramp[1], g - 2) + ramp[0]),
                S(ramp[1] + new string(ramp[2], g - 2) + ramp[1]),
                S(ramp[1] + new string(ramp[2], g - 2) + ramp[1]),
                S(new string(ramp[3], g)),
                S(new string(ramp[3], g)),
                S(new string(ramp[4], g)),
                S(new string(ramp[4], g)),
            };
        }

        static string[] SwordDetailCollarRows(char dark, char lit, char bas, char deep)
        {
            int end = SwordCollarEnd * 2, mid = (SwordCollarWidth - SwordCollarEnd * 2) * 2;
            int accent = Mathf.Clamp(mid / 4, 1, 2);
            string Row(char a)
                => SwordCentred(new string(dark, end) + new string(a, accent)
                                + new string(bas, mid - accent * 2)
                                + new string(a, accent) + new string(dark, end), SwordDetailCanvas);
            return new[] { Row(lit), Row(deep) };
        }

        /// <summary>One grip row at menu density. <paramref name="shade"/> equals
        /// <paramref name="wrap"/> on the plain rows and darkens on the bound ones, which is what
        /// turns a flat bar into a wrap at this size.</summary>
        static string SwordDetailGripRow(char wrap, char core, char shade)
            => SwordCentred(new string(wrap, 2) + new string(shade, 1)
                            + new string(core, SwordGripWidth * 2 - 6)
                            + new string(shade, 1) + new string(wrap, 2), SwordDetailCanvas);

        static string[] SwordDetailPommelRows(string ramp)
        {
            int w = SwordPommelWidth * 2;
            string S(string s) => SwordCentred(s, SwordDetailCanvas);
            string cap = S(new string(ramp[0], 2) + new string(ramp[1], 2)
                           + new string(ramp[2], w - 8)
                           + new string(ramp[1], 2) + new string(ramp[0], 2));
            string body = S(new string(ramp[3], w));
            string foot = S(new string(ramp[3], Mathf.Max(4, w - 8)));
            return new[] { cap, cap, body, body, foot, foot };
        }

        /// <summary>The rim notches, shared by the arena grid and the menu one so they agree.</summary>
        static readonly float[] Notches = { 45f, 135f, -135f, -45f };
        const float NotchWidth = 15f;
        const float NotchDepth = 2.6f;

        // LAZY: Notches is declared after these would run as static initialisers.
        static string[] _discRows, _discDetail, _quatrefoilRows, _quatrefoilDetail;
        static string[] DiscRows => _discRows ??= StripSpecks(BuildDiscAt(DiscHeightCells));
        static string[] DiscDetail => _discDetail ??= BuildDiscAt(DiscHeightCells * 2);
        static string[] QuatrefoilRows => _quatrefoilRows ??= StripSpecks(BuildDiscAt(DiscHeightCells, Notches));
        static string[] QuatrefoilDetail => _quatrefoilDetail ??= StripSpecks(BuildDiscAt(DiscHeightCells * 2, Notches));

        /// <summary>
        /// Drop opaque texels with fewer than two orthogonal neighbours.
        ///
        /// A notch boundary crossing a row at a shallow angle leaves one-texel islands, and at
        /// this size a stray texel reads as a rendering fault rather than as a chip in the blade.
        /// Two passes, because removing an island can strand its neighbour.
        /// </summary>
        static string[] StripSpecks(string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            for (int pass = 0; pass < 2; pass++)
            {
                var next = new char[h][];
                for (int y = 0; y < h; y++) next[y] = rows[y].ToCharArray();
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (rows[y][x] == '.') continue;
                    int n = 0;
                    if (y > 0     && rows[y - 1][x] != '.') n++;
                    if (y < h - 1 && rows[y + 1][x] != '.') n++;
                    if (x > 0     && rows[y][x - 1] != '.') n++;
                    if (x < w - 1 && rows[y][x + 1] != '.') n++;
                    if (n < 2) next[y][x] = '.';
                }
                for (int y = 0; y < h; y++) rows[y] = new string(next[y]);
            }
            return rows;
        }
        static readonly string[] GreatswordDetail = BuildGreatswordDetail();

        /// <summary>The same edge at menu density: six texels (the arena's three cells, doubled),
        /// with the specular pair the detail grid's flats carry.</summary>
        static readonly string[] BronzeEdgedDetail =
            BronzeEdge(GreatswordDetail, GreatswordEdgeRows * 2, 6, "665544");

        /// <summary>Twice <see cref="DiscGrip"/>, in a grid with twice the cells.</summary>
        static readonly Vector2Int DiscDetailGrip = new(DiscHeightCells, DiscHeightCells);

        /// <summary>
        /// Twice <see cref="GreatswordGrip"/>. Counted the same way: 12 tip rows, the blade, 4
        /// ricasso, 8 crossguard, 2 collar, then the second grip row.
        /// </summary>
        static readonly Vector2Int GreatswordDetailGrip =
            new(27, 12 + BladeRows * 2 + 4 + 8 + 2 + SwordGripRows);   // the 2x grip's centre


        // ---------------------------------------------------------------- the fire blade
        //
        // The greatsword's own silhouette, with a LIQUID GLASS fuller and the blade around it
        // aflame. The glass recipe is donada's (src/App2.css, where the same four ingredients
        // appear twelve times):
        //
        //   1. base    160deg .78 -> .42 @55% -> .64    bright, DIP, recover, along the fuller
        //   2. sheen   135deg .55 -> .18 @30% -> 0      a corner sheen, gone by 60%
        //   3. inset   top/left bright, bottom dark
        //   4. backdrop saturate + brighten what is behind -> here, the glass takes a TINT from
        //      the flames it sits between
        //
        // (1) is what makes it read as liquid rather than as a bevel: a linear ramp is glass, a
        // ramp that dips and recovers is glass with something moving under it. It is ONE step of
        // dip, not two - two turned the middle of the fuller into a grey slab.

        /// <summary>Hottest to coolest, six steps, one set per heat stage.</summary>
        static readonly Color[][] HeatRamps =
        {
            new[] { new Color(1.00f,0.62f,0.34f), new Color(0.95f,0.35f,0.16f), new Color(0.80f,0.20f,0.09f),
                    new Color(0.58f,0.12f,0.06f), new Color(0.34f,0.07f,0.04f), new Color(0.16f,0.04f,0.03f) },
            new[] { new Color(1.00f,0.85f,0.52f), new Color(1.00f,0.62f,0.18f), new Color(0.94f,0.42f,0.10f),
                    new Color(0.72f,0.25f,0.07f), new Color(0.44f,0.13f,0.05f), new Color(0.20f,0.06f,0.03f) },
            new[] { new Color(1.00f,0.98f,0.78f), new Color(1.00f,0.88f,0.32f), new Color(0.99f,0.68f,0.15f),
                    new Color(0.85f,0.44f,0.10f), new Color(0.55f,0.22f,0.07f), new Color(0.26f,0.09f,0.04f) },
            new[] { new Color(1.00f,1.00f,0.98f), new Color(1.00f,0.97f,0.85f), new Color(1.00f,0.86f,0.55f),
                    new Color(0.96f,0.62f,0.25f), new Color(0.68f,0.33f,0.12f), new Color(0.32f,0.13f,0.06f) },
            new[] { new Color(0.96f,1.00f,1.00f), new Color(0.72f,0.92f,1.00f), new Color(0.36f,0.68f,1.00f),
                    new Color(0.16f,0.42f,0.92f), new Color(0.08f,0.22f,0.62f), new Color(0.04f,0.10f,0.30f) },
        };

        static Dictionary<char, Color> FirePal(int stage)
        {
            var f = HeatRamps[stage];
            // A third of the flame's own mid tone. Left neutral the fuller read as a chrome strip
            // laid between two fires; glass takes the colour of what surrounds it.
            Color Glass(Color c) => Color.Lerp(c, f[2], 0.34f);
            return new Dictionary<char, Color>
            {
                ['1'] = f[0], ['2'] = f[1], ['3'] = f[2], ['4'] = f[3], ['5'] = f[4], ['6'] = f[5],
                ['H'] = Glass(new Color(1.00f, 1.00f, 0.99f)),
                ['L'] = Glass(new Color(0.93f, 0.96f, 0.99f)),
                ['B'] = Glass(new Color(0.80f, 0.86f, 0.92f)),
                ['D'] = Glass(new Color(0.62f, 0.70f, 0.80f)),
                ['S'] = Glass(new Color(0.44f, 0.52f, 0.64f)),
                ['l'] = new Color(0.33f, 0.35f, 0.43f), ['b'] = new Color(0.22f, 0.23f, 0.30f),
                ['d'] = new Color(0.15f, 0.16f, 0.21f), ['s'] = new Color(0.10f, 0.10f, 0.14f),
                ['G'] = new Color(0.16f, 0.10f, 0.06f), ['g'] = new Color(0.40f, 0.26f, 0.17f),
                // The hilt's crust - cooled basalt, warm-tinted so it reads as rock that was
                // molten rather than as iron. Fixed across stages; only the heat in its cracks
                // follows the cycle. 'f' stays a shade clear of Palette.Outline so the drawn
                // lines inside the hilt don't merge with the stroke around it.
                ['A'] = new Color(0.50f, 0.40f, 0.36f), ['a'] = new Color(0.34f, 0.26f, 0.24f),
                ['c'] = new Color(0.21f, 0.15f, 0.15f), ['e'] = new Color(0.13f, 0.09f, 0.10f),
                ['f'] = new Color(0.09f, 0.05f, 0.06f),
            };
        }

        /// <summary>A fuller is a LENS - widest mid-blade, closed before the tip and the ricasso.
        /// Held at a constant width it read as a slot cut THROUGH the sword.
        ///
        /// Scaled by <see cref="SwordBladeScale"/>: 2.2 was measured against the eighteen-cell
        /// blade, and left alone on a narrower one the glass eats the flame either side of it.</summary>
        static float FullerHalf(float y)
        {
            float t = Mathf.Clamp01((y - 6f) / (BladeRows - 1f));
            // Clamped: in single precision sin(PI) is a hair NEGATIVE, Pow of that is NaN, and a
            // NaN fuller fails every comparison downstream - the last blade row came out as a band
            // of the brightest flame tone.
            return 2.2f * SwordBladeScale * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.30f);
        }

        /// <summary>
        /// Emberline's six tip rows: the shared taper, in flame rather than steel. Cool edge,
        /// hot core, one band of ember down the trailing side - the same left-to-right progression
        /// the hand-painted rows carried, now following the blade's width instead of fixing it.
        /// </summary>
        static string[] FireTipRows()
        {
            var bands = SwordTipBands();
            var rows = new string[bands.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var (lit, bas, dark) = bands[i];
                int warm = (bas + 1) / 2;
                rows[i] = SwordCentred(new string('2', lit) + new string('3', warm)
                                       + new string('4', bas - warm) + new string('1', dark));
            }
            return rows;
        }

        static float Base160(float t)
            => t < 0.55f ? Mathf.Lerp(0.78f, 0.42f, t / 0.55f)
                         : Mathf.Lerp(0.42f, 0.64f, (t - 0.55f) / 0.45f);

        /// <summary>
        /// Emberline in the arena: the flame blade and glass fuller, on the solidified-lava hilt.
        /// Same row budget as every greatsword (6 tip, blade, 2 transition, 4 guard, collar, 6
        /// grip, 3 pommel), so the height and the grip pivot are exactly
        /// <see cref="GreatswordGrip"/>'s - only what is drawn inside those rows differs.
        /// </summary>
        static string[] FireBlade(int stage)
        {
            // the tip is flame - the fuller has not opened
            var rows = new List<string>(FireTipRows());
            for (int y = FireTipRows().Length; y < SwordHeightRows; y++)
            {
                var line = new char[SwordCanvas];
                for (int x = 0; x < SwordCanvas; x++)
                    line[x] = EmberTexel(x, y, fine: false);
                rows.Add(new string(line));
            }
            return rows.ToArray();
        }

        /// <summary>
        /// Emberline at menu density - hand-authored rather than derived, because this is the art
        /// an NFT image is rendered from (see CLAUDE.md, "NFT item images"). FOUR times the arena
        /// grid in each direction, at TRUE <see cref="MenuPpu"/> (never pass upscale2x): 112 x 288,
        /// the same world size as the arena sprite.
        ///
        /// Not drawn by upscaling the arena grid. Both grids sample the SAME continuous fields
        /// (<see cref="EmberTexel"/>), the arena one at cell centres and this one four times per
        /// cell, so the silhouette cannot drift between them - the menu one just resolves what the
        /// arena can only hint at: the cooling cracks' dark lips, hairline cracks, a cut gem with
        /// a highlight, the tongue's molten seam, the grip's specular line.
        /// </summary>
        static string[] EmberlineDetail()
        {
            const int k = EmberDetailScale;
            int w = SwordCanvas * k, h = SwordHeightRows * k;
            var rows = new string[h];
            for (int Y = 0; Y < h; Y++)
            {
                var line = new char[w];
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < w; X++)
                    line[X] = EmberTexel((X + 0.5f) / k - 0.5f, y, fine: true);
                rows[Y] = new string(line);
            }
            return rows;
        }

        /// <summary>Menu cells per arena cell. 4: the arena grid is authored at half
        /// <see cref="FinePpu"/>, and <see cref="MenuPpu"/> is twice FinePpu.</summary>
        const int EmberDetailScale = 4;

        /// <summary>The arena pivot, at menu density - see <see cref="EmberDetailScale"/>.</summary>
        static readonly Vector2Int EmberDetailGrip =
            new(GreatswordGrip.x * EmberDetailScale, GreatswordGrip.y * EmberDetailScale);

        // ---- the solidified-lava hilt ----------------------------------------------------
        //
        // A crescent guard whose horns curl UP toward the tip, a raised boss holding a molten
        // gem, a pointed tongue (langet) running up onto the blade, a solid crust grip, and
        // a teardrop pommel ending in a bead that never quite cooled.
        //
        // The material is cooled basalt, NOT iron: dark and warm, broken by polygonal cooling
        // cracks (Voronoi cell edges - real lava crust cracks into polygons as it shrinks) with
        // the heat still showing through. The cracks glow HOTTEST where the metal is thickest -
        // the boss - and cool toward the horn tips, which is what reads as a mass that is still
        // setting rather than as rock with lights painted on. Cracks, gem and bead are drawn in the
        // HEAT RAMP's own tones ('1'..'5'), so the hilt runs the heat cycle along with the blade.
        //
        // THE GUARD PASSES THE SHOULDER BUDGET, as Saint's wings and Crossblade's arms do, and
        // for the same reason: the horns ARE this hilt. The body of the guard stays inside
        // SwordGuardWidth; only the horns, curling up beside the blade, reach past it (~21 cells).
        // The gap between horn and blade is kept at three cells so the auto-outline, which bites
        // a texel off each side of a gap, leaves it open.
        //
        // Every dimension below is in AUTHORED ARENA CELLS, x on the same axis as SwordAxis and
        // y counted from the top row, so both densities sample one picture.

        /// <summary>First row below the parallel blade - where the ricasso used to be.</summary>
        static int EmberHiltTop => 6 + BladeRows;                  // 56
        static float EmberGuardY => EmberHiltTop + 3.6f;           // crescent centre line
        static float EmberBossY => EmberHiltTop + 3.5f;
        static float EmberGemY => EmberHiltTop + 3.4f;
        static float EmberCollarY => EmberHiltTop + 6f;
        static float EmberGripTop => EmberHiltTop + 6.5f;          // SwordGripRows of grip
        static float EmberPommelTop => EmberGripTop + SwordGripRows; // 3 rows of pommel

        const float EmberHornReach = 10.8f;     // horn tip, cells from the axis
        const float EmberHornCurl = 0.068f;     // how fast the crescent rises toward the tip
        const float EmberGuardThick = 2.05f;
        const float EmberGemRadius = 1.55f;
        const float EmberLangetLength = 8.5f;
        const float EmberLangetHalf = 3.0f;
        const float EmberGripHalf = SwordGripWidth / 2f;

        /// <summary>
        /// Signed distance to the crust (guard, boss, tongue, collar, pommel), in cells, negative
        /// inside. The GRIP is not in it - it is its own material, a wrap rather than a casting.
        /// </summary>
        static float EmberCrustSd(float x, float y)
        {
            float dx = Mathf.Abs(x - SwordAxis);

            // Crescent. The centre line rises as dx^2; thickness is measured perpendicular to it
            // (dividing by the slope), or the horns thicken as they steepen.
            float crescent = 99f;
            if (dx < EmberHornReach)
            {
                float yc = EmberGuardY - EmberHornCurl * dx * dx;
                float slope = 2f * EmberHornCurl * dx;
                float th = EmberGuardThick * Mathf.Pow(1f - (dx / EmberHornReach) * (dx / EmberHornReach), 0.6f);
                crescent = Mathf.Abs(y - yc) / Mathf.Sqrt(1f + slope * slope) - th;
            }

            // Boss - a rounded diamond carrying the gem, a little proud of the guard.
            float by = Mathf.Abs(y - EmberBossY);
            float boss = 3.1f * (Mathf.Pow(Mathf.Pow(dx / 3.3f, 1.6f) + Mathf.Pow(by / 2.9f, 1.6f),
                                           1f / 1.6f) - 1f);

            // Tongue - up the blade's face to a point, the guard's own crust flowing onto it.
            float langetTip = EmberHiltTop - EmberLangetLength + 2f;
            float along = Mathf.Clamp01((y - langetTip) / EmberLangetLength);
            float langet = Mathf.Max(dx - EmberLangetHalf * Mathf.Pow(along, 0.75f), langetTip - y);
            langet = Mathf.Max(langet, y - EmberBossY);

            float collar = Mathf.Max(dx - (EmberGripHalf + 0.3f), Mathf.Abs(y - EmberCollarY) - 0.5f);

            // Pommel - a teardrop, a drip that set as it fell.
            float pommel = 99f;
            float pb = EmberPommelTop + 3.1f;
            if (y > EmberPommelTop - 0.5f)
            {
                float hw = SwordPommelWidth * 0.46f * Mathf.Sqrt(Mathf.Max(0f, (pb - y) / 3.1f));
                float top = EmberPommelTop + 0.35f * (dx / 5f) * (dx / 5f);
                pommel = Mathf.Max(Mathf.Max(dx - hw, top - y), y - (EmberPommelTop + 3f));
            }

            return Mathf.Min(Mathf.Min(crescent, boss), Mathf.Min(langet, Mathf.Min(collar, pommel)));
        }

        static bool InEmberGrip(float x, float y)
            => Mathf.Abs(x - SwordAxis) < EmberGripHalf
               && y > EmberGripTop - 0.5f && y < EmberGripTop + SwordGripRows - 0.5f;

        /// <summary>
        /// One texel of Emberline at cell coordinate (x, y). The arena passes integer cells
        /// (cell centres, as every grid here is written); the menu passes quarter cells.
        /// </summary>
        static char EmberTexel(float x, float y, bool fine)
        {
            float sd = EmberCrustSd(x, y);
            if (sd < 0f) return EmberCrust(x, y, sd, fine);
            if (InEmberGrip(x, y)) return EmberWrap(x, y, fine);
            if (y < 6f) return fine ? EmberTipTexel(x, y) : '.';
            if (y < EmberHiltTop + 2f) return EmberBladeTexel(x, y, fine);
            return '.';
        }

        /// <summary>
        /// The flame blade and its glass fuller - the original FireBlade loop, as a function of a
        /// continuous position so the menu grid can sample it too. At integer cells it is the
        /// arena blade exactly as it was.
        /// </summary>
        static char EmberBladeTexel(float x, float y, bool fine)
        {
            if (x < SwordBladeLeft - 0.5f || x > SwordBladeRight + 0.5f) return '.';
            float t = Mathf.Clamp01((y - 6f) / (BladeRows - 1f));
            float half = FullerHalf(y);
            float fl = SwordAxis - half, fr = SwordAxis + half;
            if (half > 0f && x >= fl && x <= fr)
            {
                float u = (x - fl) / Mathf.Max(0.5f, fr - fl);
                float sheen = Mathf.Clamp01(1f - (u * 0.55f + t) / 0.60f);
                int g = Base160(t) > 0.62f ? 0 : 1;
                if (sheen > 0.55f) g -= 1;
                if (u < 0.25f) g -= 1;                 // lit left
                if (u > 0.78f) g += 2;                 // dark right
                if (t > 0.96f) g += 1;
                return "HLBDS"[Mathf.Clamp(g, 0, 4)];
            }
            // hottest against the glass, cooling to the blade's edge
            bool left = x < fl;
            float across = left ? (fl - x) / Mathf.Max(1f, fl - SwordBladeLeft)
                                : (x - fr) / Mathf.Max(1f, SwordBladeRight - fr);
            return EmberFlame(x, y, across, fine);
        }

        static char EmberFlame(float x, float y, float across, bool fine)
        {
            float wobble = 1.55f * Mathf.Sin(y * 0.30f + x * 0.85f);
            float tongue = Mathf.Sin(y * 0.44f + x * 2.05f + wobble);
            // Sampled four times per cell, the arena's one wave resolves into smooth diagonal
            // stripes - a glass rod, not fire. The menu adds a finer lick and some noise; the
            // arena is left exactly as it was.
            if (fine)
                tongue = tongue * 0.8f + 0.45f * Mathf.Sin(y * 1.1f - x * 3.3f + 2.2f * wobble)
                         + 0.35f * (EmberNoise(x * 1.6f, y * 0.7f) - 0.5f);
            return "123456"[Mathf.Clamp(Mathf.RoundToInt(across * 3.4f - tongue * 1.25f + 0.5f), 0, 5)];
        }

        /// <summary>
        /// The menu tip: the same taper as <see cref="SwordTipTaper"/>, interpolated between row
        /// centres instead of stepped, so the point is a line rather than a staircase of
        /// four-texel blocks. The arena keeps <see cref="FireTipRows"/>.
        /// </summary>
        static char EmberTipTexel(float x, float y)
        {
            float f = Mathf.Clamp(y, -0.5f, 5f);
            float taper;
            if (f < 0f) taper = Mathf.Lerp(0.05f, SwordTipTaper[0], (f + 0.5f) / 0.5f);
            else
            {
                int i = Mathf.Min(SwordTipTaper.Length - 2, Mathf.FloorToInt(f));
                taper = Mathf.Lerp(SwordTipTaper[i], SwordTipTaper[i + 1], f - i);
            }
            float halfW = taper * SwordBladeWidth * 0.5f;
            float dx = Mathf.Abs(x - SwordAxis);
            if (dx > halfW) return '.';
            return EmberFlame(x, y, dx / Mathf.Max(0.5f, halfW), true);
        }

        /// <summary>The crust, lit from the upper left, with the heat showing through.</summary>
        static char EmberCrust(float x, float y, float sd, bool fine)
        {
            float dx = Mathf.Abs(x - SwordAxis);
            float depth = -sd;

            // The gem, first - it sits IN the boss, in a dark setting on the menu art.
            float gx = x - SwordAxis, gy = y - EmberGemY;
            float dg = Mathf.Sqrt(gx * gx + gy * gy);
            if (dg < EmberGemRadius)
            {
                if (!fine) return dg < 0.8f ? '1' : '2';
                float hx = gx + 0.45f, hy = gy + 0.5f;
                if (hx * hx + hy * hy < 0.14f) return 'H';
                float r = dg / EmberGemRadius;
                int g = r < 0.5f ? 0 : r < 0.82f ? 1 : 2;
                if (gx + gy > 0.3f) g++;               // the far facets turn from the light
                return "1234"[Mathf.Min(g, 3)];
            }
            if (fine && dg < EmberGemRadius + 0.3f) return 'f';

            // The pommel's bead - the last of the drip, still soft.
            if (y > EmberPommelTop + 2.2f && dx < 1.25f)
            {
                if (!fine) return '3';
                float d = Mathf.Sqrt(dx * dx + (y - (EmberPommelTop + 2.4f)) * (y - (EmberPommelTop + 2.4f)));
                return d < 0.35f ? '1' : d < 0.7f ? '2' : '3';
            }

            // Surface normal from the distance field, for the bevel.
            const float e = 0.3f;
            float nx = EmberCrustSd(x + e, y) - EmberCrustSd(x - e, y);
            float ny = EmberCrustSd(x, y + e) - EmberCrustSd(x, y - e);
            float nl = Mathf.Sqrt(nx * nx + ny * ny);
            if (nl > 1e-4f) { nx /= nl; ny /= nl; }

            // On the menu art, a hand-drawn line where the crust meets the BLADE - the auto
            // outline only strokes against transparency, and without it the tongue's edge blurs
            // into the flame behind it.
            if (fine && depth < 0.22f && EmberBladeUnder(x + nx * 0.45f, y + ny * 0.45f)) return 'f';

            // Cooling cracks. Heat is how deep in the mass a point is and how near the centre -
            // the thick boss is still molten inside, the thin horn tips set first.
            //
            // The arena grid only cracks the hot core, and never right around the gem: at cell
            // size a crack is a whole texel, and cracking everywhere turned the hilt into an orange
            // blob with the gem lost in it. The gem must stay the brightest thing on the hilt.
            float heat = Mathf.Clamp01(depth / 2.0f) * Mathf.Clamp01(1.15f - dx / 11f);
            if (depth > (fine ? 0.3f : 0f) && (fine || (heat > 0.25f && dg > EmberGemRadius + 1f)))
            {
                // Domain-warped, so the cells' straight edges wander like real cooling fractures
                // rather than reading as tile grout.
                float wx = x + 1.1f * (EmberNoise(x * 0.6f + 3.7f, y * 0.6f) - 0.5f);
                float wy = y + 1.1f * (EmberNoise(x * 0.6f, y * 0.6f + 9.2f) - 0.5f);
                var (f1, f2) = EmberVoronoi(wx, wy, 2.3f);
                float edge = f2 - f1;
                float w = fine ? 0.27f : 0.42f;
                if (edge < w)
                {
                    bool core = fine && edge < w * 0.45f;
                    if (heat > 0.55f) return core ? '1' : '2';
                    if (heat > 0.30f) return core ? '2' : '3';
                    if (heat > 0.12f) return core ? '3' : '4';
                    return core ? '4' : '5';
                }
                if (fine)
                {
                    if (edge < w + 0.12f) return 'f';               // the crack's dark lip
                    var (h1, h2) = EmberVoronoi(x + 17.3f, y + 5.1f, 1.0f);
                    if (h2 - h1 < 0.10f && heat > 0.2f) return '5'; // hairline cracks
                }
            }

            // The tongue's molten seam - the reference's centre ridge, as heat.
            if (fine && dx < 0.22f && y > EmberHiltTop - EmberLangetLength + 3.5f && y < EmberBossY - 2.4f)
                return '3';

            float lit = nx * -0.55f + ny * -0.83f;         // outward normal against the upper-left light
            if (depth < (fine ? 0.45f : 0.75f))
            {
                if (lit > 0.55f) return fine ? 'A' : 'a';
                if (lit > 0.15f) return 'a';
                if (lit < -0.40f) return 'f';
                if (lit < -0.05f) return 'e';
                return 'c';
            }
            if (fine && EmberNoise(x * 1.3f, y * 1.3f) > 0.68f) return 'e';   // pitting
            return 'c';
        }

        /// <summary>True where a point would be the blade rather than air - used to decide which
        /// crust edges need a drawn line and which the auto-outline already covers.</summary>
        static bool EmberBladeUnder(float x, float y)
            => y >= 6f && y < EmberHiltTop + 2f
               && x >= SwordBladeLeft - 0.5f && x <= SwordBladeRight + 0.5f;

        /// <summary>
        /// The grip: SOLID crust, one rounded bar shaded as a cylinder lit from the left. No wrap
        /// and no glow - it was a spiral lava rope with glowing grooves, and read as a striped
        /// candy handle under all that heat. The heat lives in the guard; the hand holds rock.
        /// </summary>
        static char EmberWrap(float x, float y, bool fine)
        {
            float a = (x - SwordAxis) / EmberGripHalf;
            if (!fine) return a < -0.5f ? 'a' : a > 0.45f ? 'e' : 'c';
            if (a < -0.72f) return 'a';
            if (a < -0.45f) return 'A';                 // the specular line, a little in from the edge
            if (a < -0.15f) return 'a';
            if (a < 0.40f) return 'c';
            if (a < 0.70f) return 'e';
            return 'f';
        }

        static float EmberHash(int i, int j)
        {
            unchecked
            {
                uint h = (uint)(i * 374761393 + j * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777216f;
            }
        }

        /// <summary>Nearest and second-nearest distance to a jittered lattice of points - a
        /// crack lies where the two are nearly equal.</summary>
        static (float F1, float F2) EmberVoronoi(float x, float y, float cell)
        {
            int gx = Mathf.FloorToInt(x / cell), gy = Mathf.FloorToInt(y / cell);
            float f1 = 99f, f2 = 99f;
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int cx = gx + i, cy = gy + j;
                float px = (cx + 0.15f + 0.7f * EmberHash(cx, cy)) * cell;
                float py = (cy + 0.15f + 0.7f * EmberHash(cx + 101, cy - 37)) * cell;
                float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                if (d < f1) { f2 = f1; f1 = d; }
                else if (d < f2) f2 = d;
            }
            return (f1, f2);
        }

        static float EmberNoise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = EmberHash(ix, iy), b = EmberHash(ix + 1, iy);
            float c = EmberHash(ix, iy + 1), d = EmberHash(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }


        // ---------------------------------------------------------------- the Gilded Greatsword
        //
        // A gold blade on an ANTIQUE-GOLD hilt - darker and browner than the blade, or the two
        // run together into one gold shape:
        //
        //   QUILLONS - a long STRAIGHT bar square to the blade, a third of the blade's width
        //       thick at the blade and tapering the WHOLE way out (a spindle), SOLID bright
        //       gold shaded lengthwise, ending in SHARP points of the antique gold
        //   a blocky DIAMOND boss at the crossing, bright gold, with a dark recess at its centre
        //   a BRAID up the centre of the blade from the guard, plaited in the hilt's gold
        //   a TWISTED ROPE grip, and an ARROWHEAD cap for a pommel, wider than the grip
        //
        // Standard row budget, so the height and the pivot are GreatswordGrip's exactly. The
        // quillons pass the shoulder budget (~25 cells tip to tip - the reference's are longer
        // still, the canvas is the limit), as Emberline's horns and the Silver Blade's cross do.
        //
        // One continuous field (GildedTexel), sampled at cell centres for the arena and four
        // times per cell for the menu art, like Emberline and the Silver Blade.

        static int GildedHiltTop => 6 + BladeRows;                       // 56
        static float GildedGuardY => GildedHiltTop + 3f;               // on a row: 3 rows thick
        static float GildedGripTop => GildedHiltTop + 6.5f;              // SwordGripRows of grip
        static float GildedPommelTop => GildedGripTop + SwordGripRows;   // 3 rows of cap

        const float GildedQuillonReach = 12.5f;
        const float GildedQuillonHalf = 2.5f;      // at the axis; tapers to 0 at the reach
        const float GildedQuillonTip = 3.5f;       // the sharp end, in the antique gold
        const float GildedBossHalf = 3.4f;
        const float GildedRecess = 0.9f;
        const float GildedBraidHalf = 2f;          // half-width of the braid up the blade
        const float GildedBraidRows = 15f;
        const float GildedBraidPitch = 3f;
        const float GildedGripHalf = SwordGripWidth / 2f;

        static Dictionary<char, Color> GildedSwordPal()
        {
            var antique = new Palette.Ramp(new Color(0.60f, 0.44f, 0.19f), shade: 0.40f)
                .WithHighlightsToward(new Color(1.00f, 0.92f, 0.62f), 0.45f);
            var stud = new Palette.Ramp(new Color(0.20f, 0.14f, 0.11f), lift: 0.30f);
            return Palette.Of(Palette.Tier(LootTier.Gold), antique, stud);
        }

        static string[] BuildGildedSword()
        {
            var tip = SwordTipRows("lbd");
            var rows = new string[SwordHeightRows];
            for (int y = 0; y < rows.Length; y++)
            {
                if (y < tip.Length) { rows[y] = tip[y]; continue; }
                var line = new char[SwordCanvas];
                for (int x = 0; x < SwordCanvas; x++) line[x] = GildedTexel(x, y, fine: false);
                rows[y] = new string(line);
            }
            return rows;
        }

        /// <summary>Menu art at TRUE <see cref="MenuPpu"/>, four times the arena grid - never
        /// pass it upscale2x.</summary>
        static string[] BuildGildedSwordDetail()
        {
            const int k = EmberDetailScale;
            var rows = new string[SwordHeightRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[SwordCanvas * k];
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++)
                    line[X] = GildedTexel((X + 0.5f) / k - 0.5f, y, fine: true);
                rows[Y] = new string(line);
            }
            return rows;
        }

        static readonly string[] GildedSwordRows = BuildGildedSword();
        static readonly string[] GildedSwordDetail = BuildGildedSwordDetail();

        /// <summary>The hilt's metal: quillons, the diamond at the crossing, collar, cap.</summary>
        static float GildedMetalSd(float x, float y)
        {
            float dx = Mathf.Abs(x - SwordAxis);

            float dy = y - GildedGuardY;
            float quillon = GildedQuillonSd(dx, dy);
            float diamond = (dx + Mathf.Abs(dy) - GildedBossHalf) * 0.7071f;
            float collar = Mathf.Max(dx - (GildedGripHalf + 0.4f), Mathf.Abs(y - (GildedGripTop - 1f)) - 0.5f);

            float cap = 99f;
            if (y > GildedPommelTop - 0.5f)
            {
                float half = 4.6f * (1f - (y - (GildedPommelTop - 0.5f)) / 3.2f);   // arrowhead
                cap = Mathf.Max(dx - half, y - (GildedPommelTop + 2.5f));
            }
            return Mathf.Min(Mathf.Min(quillon, diamond), Mathf.Min(collar, cap));
        }

        /// <summary>The quillons' half-thickness at a distance from the axis: a SPINDLE, widest
        /// at the blade and tapering the whole way out to a sharp point - the reference's shape.
        /// A straight bar that only pointed at the tips read as a plank with the corners cut.</summary>
        static float GildedQuillonHalfAt(float dx)
            => GildedQuillonHalf * Mathf.Max(0f, 1f - dx / GildedQuillonReach);

        static float GildedQuillonSd(float dx, float dy)
            => Mathf.Max(Mathf.Abs(dy) - GildedQuillonHalfAt(dx), dx - GildedQuillonReach);

        static char GildedTexel(float x, float y, bool fine)
        {
            float dx = Mathf.Abs(x - SwordAxis);

            float sd = GildedMetalSd(x, y);
            if (sd < 0f) return GildedMetal(x, y, sd, fine);

            if (dx < GildedGripHalf && y > GildedGripTop - 0.5f && y < GildedPommelTop - 0.5f)
                return GildedRope(x, y, fine);

            if (y < 6f) return fine ? SilverTip(x, y) : '.';
            if (y > GildedGuardY) return '.';
            float half = SwordBladeWidth / 2f;
            if (dx > half) return '.';

            // The BRAID, running up the blade from the guard: two strands plaited down the centre
            // line - each side's lobes slant toward the middle, and the right side runs half a
            // pitch behind the left, so the lobes interleave the way a plait's crossings do.
            // Studs were tried first; the reference is a braid, and holes read as a ladder.
            float stripBase = GildedGuardY - GildedBossHalf - 0.5f;
            float stripTop = stripBase - GildedBraidRows;
            if (dx < GildedBraidHalf && y > stripTop)
            {
                bool right = x > SwordAxis;
                // Arena: no offset and two tones - stacked chevrons, the plait at cell size. The
                // interleave needs more cells than a four-wide strip has; tried, it read as noise.
                float offset = fine && right ? GildedBraidPitch * 0.5f : 0f;
                float phase = (y + dx * 0.9f + offset) / GildedBraidPitch;
                float fr = phase - Mathf.Floor(phase);
                if (!fine) return fr < 0.34f ? 'L' : 'D';
                if (fine && dx < 0.12f) return '3';                         // the parting line
                if (fr > 0.8f) return fine ? '2' : '3';                     // gap between lobes
                if (fr < 0.3f) return fine && fr < 0.12f && !right ? 'H' : 'L';
                return fr < 0.62f ? 'B' : 'D';
            }

            float u = (x - (SwordAxis - half)) / (2f * half);
            if (fine)
            {
                if (u < 0.06f) return 'h';
                if (y > 8f && y < stripTop - 1f && Mathf.Abs(x - SwordAxis + 0.25f) < 0.25f) return 's';
            }
            if (u < 0.2f) return 'l';
            if (u > 0.8f) return 'd';
            return 'b';
        }

        static char GildedMetal(float x, float y, float sd, bool fine)
        {
            float dx = Mathf.Abs(x - SwordAxis), dy = y - GildedGuardY;

            // The boss's dark recess.
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            if (r < GildedRecess) return fine && r < GildedRecess * 0.5f ? '2' : '3';
            if (fine && r < GildedRecess + 0.25f) return 'S';

            // The crossguard - bar and boss - is SOLID bright gold, the blade's own metal, and
            // only its sharp ends are the hilt's antique gold. Everything else (collar, cap) is
            // antique too.
            bool guard = GildedQuillonSd(dx, dy) < 0f || (dx + Mathf.Abs(dy) - GildedBossHalf) < 0f;
            bool tip = dx > GildedQuillonReach - GildedQuillonTip;
            string ramp = guard && !tip ? "hlbds" : "HLBDS";

            // The bar outside the boss is shaded LENGTHWISE, as the reference is: lit along the
            // edge facing the blade, dark along the edge facing the grip.
            bool bar = GildedQuillonSd(dx, dy) < 0f && dx + Mathf.Abs(dy) > GildedBossHalf + 0.2f;
            if (bar)
            {
                float t = dy / Mathf.Max(0.3f, GildedQuillonHalfAt(dx));
                if (!fine && dx < SwordBladeWidth / 2f && GildedMetalSd(x, y - 1f) >= 0f) return 'D';
                if (t < -0.35f) return fine && t < -0.72f ? ramp[0] : ramp[1];
                if (t > 0.35f) return fine && t > 0.72f ? ramp[4] : ramp[3];
                return ramp[2];
            }

            const float e = 0.3f;
            float nx = GildedMetalSd(x + e, y) - GildedMetalSd(x - e, y);
            float ny = GildedMetalSd(x, y + e) - GildedMetalSd(x, y - e);
            float nl = Mathf.Sqrt(nx * nx + ny * ny);
            if (nl > 1e-4f) { nx /= nl; ny /= nl; }
            float lit = nx * -0.55f + ny * -0.83f;

            // A drawn line where the hilt lies over the blade. The ARENA needs it too now: a
            // bright gold guard against a bright gold blade has no edge between them otherwise,
            // and the guard reads as the blade widening.
            if (fine && -sd < 0.2f && y + ny * 0.4f < GildedGuardY
                && Mathf.Abs(x + nx * 0.4f - SwordAxis) < SwordBladeWidth / 2f)
                return 'S';
            if (!fine && guard && y - 1f < GildedGuardY - GildedQuillonHalf + 0.5f
                && dx < SwordBladeWidth / 2f && GildedMetalSd(x, y - 1f) >= 0f)
                return 'D';

            if (-sd < (fine ? 0.4f : 0.7f))
            {
                if (lit > 0.55f) return fine ? ramp[0] : ramp[1];
                if (lit > 0.15f) return ramp[1];
                if (lit < -0.40f) return ramp[4];
                if (lit < -0.05f) return ramp[3];
            }
            return ramp[2];
        }

        /// <summary>A twisted rope: 45-degree strands, period THREE cells - at two, the arena
        /// grid alternated cell by cell and the grip read as a checkerboard. Lit strand, body,
        /// groove, lit on the left of the grip.</summary>
        static char GildedRope(float x, float y, bool fine)
            => GildedStrand(x, y, (x - SwordAxis) / GildedGripHalf, fine);

        /// <summary>The twist shared by the grip and the quillons. <paramref name="a"/> runs
        /// -1..1 across the bar, from its lit side to its shaded one.</summary>
        static char GildedStrand(float x, float y, float a, bool fine)
        {
            float phase = (y + (x - SwordAxis)) / 3f;
            float fr = phase - Mathf.Floor(phase);
            // Arena: TWO tones, one lit strand in three - three tones at cell size read as a
            // checkerboard rather than a twist.
            if (!fine) return fr < 0.34f ? (a > 0.5f ? 'B' : 'L') : 'D';
            if (fr > 0.85f) return 'S';                  // the groove between strands
            int g = fr < 0.25f ? 0 : fr < 0.55f ? 1 : 2;
            if (a < -0.45f) g--;
            if (a > 0.5f) g++;
            return "HLBDS"[Mathf.Clamp(g + 1, 0, 4)];
        }


        // ---------------------------------------------------------------- the Silver Blade
        //
        // Bright steel on a GUNMETAL cross hilt:
        //
        //   a DIAMOND FLARE where the blade meets the guard - the blade widens to a point either
        //       side, then necks in to the boss - with a hollow inner diamond cut into it
        //   an X-SHAPED GUARD - upper arms at 42 degrees, lower at 28 - each ending in an
        //       arrowhead, around a diamond boss holding a blue gem
        //   a dark leather grip, capped by a plain gunmetal CONE pommel
        //
        // THE GUARD PASSES THE SHOULDER BUDGET, as Emberline's horns and Saint's wings do - the
        // cross IS this hilt. The upper arrowheads sit beside the narrow neck under the flare and
        // the lower ones clear of the grip, three cells either way, so the auto-outline cannot
        // close either gap.
        //
        // The cone takes five rows against the plain greatsword's three, so the parallel blade
        // gives up two to keep the family height (BladeRowsFor), and the grip - and so the pivot -
        // sits two rows higher up the sword.
        //
        // Drawn the way Emberline is: ONE continuous field (SilverTexel), sampled at cell centres
        // for the arena and four times per cell for the hand-authored menu art, so the two cannot
        // disagree at the silhouette. Coordinates are authored arena cells on SwordAxis.

        const int SilverGuardZoneRows = 7;     // flare's neck, the guard's boss, the collar
        const int SilverGripRows = SwordGripRows;
        const int SilverPommelRows = 5;
        static readonly int SilverBladeRows =
            BladeRowsFor(6 + SilverGuardZoneRows + SilverGripRows + SilverPommelRows);

        static int SilverHiltTop => 6 + SilverBladeRows;
        static float SilverGuardY => SilverHiltTop + 3f;
        static int SilverGripTop => SilverHiltTop + SilverGuardZoneRows;
        static float SilverPommelTop => SilverGripTop + SilverGripRows - 0.5f;
        const float SilverPommelHalf = 4.6f;

        /// <summary>The grip's second row, like every greatsword's pivot.</summary>
        static readonly Vector2Int SilverGrip = new(13, GripCentre(6 + SilverBladeRows + SilverGuardZoneRows, SilverGripRows));
        static readonly Vector2Int SilverDetailGrip =
            new(SilverGrip.x * EmberDetailScale, SilverGrip.y * EmberDetailScale);

        // The flare: widest at FlareY, back to the blade's own width above, and a SHARP point
        // below into a narrow neck. It sits well up the blade so the guard's upper arrowheads
        // have the neck, not the flare, beside them - at 42 degrees they reach six rows up.
        static float SilverFlareY => SilverGuardY - 9.5f;
        const float SilverFlareHalf = 7.5f;
        const float SilverFlareUp = 5.5f;
        const float SilverFlareDown = 3f;
        const float SilverNeckHalf = 1.8f;

        // The guard's X is NOT symmetric: the upper arms rise steeply (the X has to read in four
        // arena rows), the lower ones droop at a shallower angle so their arrowheads stay clear
        // of the grip. At 30 degrees both ways the arena grid drew four flat bars.
        const float SilverGuardArmUp = 7f;
        const float SilverGuardAngleUp = 42f;
        const float SilverGuardArmDown = 6.5f;
        const float SilverGuardAngleDown = 28f;
        const float SilverArmHalf = 0.95f;
        const float SilverBossRadius = 2.6f;
        const float SilverGemRadius = 1.6f;
        const float SilverGripHalf = SwordGripWidth / 2f;

        static Dictionary<char, Color> SilverSwordPal()
        {
            var gunmetal = new Palette.Ramp(new Color(0.24f, 0.27f, 0.32f), shade: 0.34f)
                .WithHighlightsToward(new Color(0.60f, 0.88f, 1.00f), 0.40f);
            var gem = new Palette.Ramp(new Color(0.22f, 0.52f, 0.95f), lift: 0.45f);
            var pal = Palette.Of(Palette.Tier(LootTier.Silver), gunmetal, gem);
            var leather = new Palette.Ramp(new Color(0.30f, 0.23f, 0.20f));
            pal['w'] = leather.Dark; pal['x'] = leather.Base; pal['y'] = leather.Light;
            return pal;
        }

        static string[] BuildSilverSword()
        {
            var tip = SwordTipRows("lbd");
            var rows = new string[SwordHeightRows];
            for (int y = 0; y < rows.Length; y++)
            {
                if (y < tip.Length) { rows[y] = tip[y]; continue; }
                var line = new char[SwordCanvas];
                for (int x = 0; x < SwordCanvas; x++) line[x] = SilverTexel(x, y, fine: false);
                rows[y] = new string(line);
            }
            return rows;
        }

        /// <summary>Menu art: four times the arena grid at TRUE <see cref="MenuPpu"/> - never pass
        /// it upscale2x. Same field as the arena, sampled per quarter cell.</summary>
        static string[] BuildSilverSwordDetail()
        {
            const int k = EmberDetailScale;
            var rows = new string[SwordHeightRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[SwordCanvas * k];
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++)
                    line[X] = SilverTexel((X + 0.5f) / k - 0.5f, y, fine: true);
                rows[Y] = new string(line);
            }
            return rows;
        }

        static readonly string[] SilverSwordRows = BuildSilverSword();
        static readonly string[] SilverSwordDetail = BuildSilverSwordDetail();

        /// <summary>One arm of a cross: a bar from the boss to <paramref name="len"/>, then an
        /// arrowhead pointing outward. Signed distance in cells, negative inside.</summary>
        static float SilverArmSd(float x, float y, float cx, float cy, float dirX, float dirY, float len)
        {
            float px = x - cx, py = y - cy;
            float along = px * dirX + py * dirY;
            float perp = Mathf.Abs(px * -dirY + py * dirX);

            float t = Mathf.Clamp(along, 0f, len);
            float bar = Mathf.Sqrt((along - t) * (along - t) + perp * perp) - SilverArmHalf;
            if (along > len) bar = 99f;

            // Arrowhead: starts wider than the bar, tapers to a point 1.5 past the bar's end.
            float a = along - len;
            float headHalf = 1.55f * (1f - (a + 0.7f) / 2.2f);
            float head = Mathf.Max(Mathf.Max(perp - headHalf, -0.7f - a), a - 1.5f);
            return Mathf.Min(bar, head);
        }

        static float SilverCrossSd(float x, float y, float cy, float boss,
                                   float upLen, float upAngle, float downLen, float downAngle)
        {
            float cx = SwordAxis;
            float cu = Mathf.Cos(upAngle * Mathf.Deg2Rad), su = Mathf.Sin(upAngle * Mathf.Deg2Rad);
            float cd = Mathf.Cos(downAngle * Mathf.Deg2Rad), sd = Mathf.Sin(downAngle * Mathf.Deg2Rad);
            float d = (Mathf.Abs(x - cx) + Mathf.Abs(y - cy) - boss) * 0.7071f;
            d = Mathf.Min(d, SilverArmSd(x, y, cx, cy,  cu, -su, upLen));
            d = Mathf.Min(d, SilverArmSd(x, y, cx, cy, -cu, -su, upLen));
            d = Mathf.Min(d, SilverArmSd(x, y, cx, cy,  cd,  sd, downLen));
            d = Mathf.Min(d, SilverArmSd(x, y, cx, cy, -cd,  sd, downLen));
            return d;
        }

        /// <summary>Gunmetal: both crosses and the collar.</summary>
        static float SilverMetalSd(float x, float y)
        {
            float g = SilverCrossSd(x, y, SilverGuardY, SilverBossRadius,
                                    SilverGuardArmUp, SilverGuardAngleUp, SilverGuardArmDown, SilverGuardAngleDown);
            float collar = Mathf.Max(Mathf.Abs(x - SwordAxis) - (SilverGripHalf + 0.4f),
                                     Mathf.Abs(y - (SilverGripTop - 1f)) - 0.5f);
            return Mathf.Min(g, collar);
        }

        /// <summary>The steel's half-width at a row: the blade, then the flare, then the neck.</summary>
        static float SilverSteelHalf(float y)
        {
            if (y < 6f || y > SilverGuardY) return -1f;
            float half = SwordBladeWidth / 2f;
            float fy = SilverFlareY;
            if (y >= fy - SilverFlareUp && y < fy)
                half = Mathf.Max(half, SilverFlareHalf * (1f - (fy - y) / SilverFlareUp));
            else if (y >= fy)
                half = Mathf.Max(SilverNeckHalf, SilverFlareHalf * (1f - (y - fy) / SilverFlareDown));
            return half;
        }

        static char SilverTexel(float x, float y, bool fine)
        {
            float dx = Mathf.Abs(x - SwordAxis);

            // Gems first - each sits in its boss.
            char gem = SilverGem(x, y, SilverGuardY, SilverGemRadius, fine);
            if (gem != '\0') return gem;

            float sd = SilverMetalSd(x, y);
            if (sd < 0f) return SilverMetal(x, y, sd, fine);

            if (dx < SilverGripHalf && y > SilverGripTop - 0.5f && y < SilverPommelTop)
                return SilverWrap(x, y, fine);

            // The pommel: a plain cone in the guard's own gunmetal, widest under the grip,
            // tapering to a point - one metal for both ends of the hilt.
            if (y >= SilverPommelTop)
            {
                float t = (y - SilverPommelTop) / (SilverPommelRows + 1f);   // +1: the point lands IN the last row
                float cone = SilverPommelHalf * (1f - t);
                if (dx > cone) return '.';
                float u = (x - (SwordAxis - cone)) / Mathf.Max(0.5f, 2f * cone);
                return u < 0.25f ? 'L' : u > 0.7f ? 'D' : 'B';
            }

            if (y < 6f) return fine ? SilverTip(x, y) : '.';
            float half = SilverSteelHalf(y);
            if (half <= 0f || dx > half) return '.';
            return SilverSteel(x, y, half, fine);
        }

        static char SilverGem(float x, float y, float cy, float r, bool fine)
        {
            float gx = x - SwordAxis, gy = y - cy;
            float d = Mathf.Sqrt(gx * gx + gy * gy);
            if (d >= r) return fine && d < r + 0.3f ? 'S' : '\0';
            if (!fine) return d < r * 0.55f ? '5' : '4';
            if ((gx + 0.35f) * (gx + 0.35f) + (gy + 0.35f) * (gy + 0.35f) < 0.06f) return '6';
            float u = d / r;
            int g = u < 0.35f ? 1 : u < 0.7f ? 2 : 3;
            if (gx + gy > 0.25f) g++;
            return "6543"[Mathf.Min(g, 3)];
        }

        static char SilverMetal(float x, float y, float sd, bool fine)
        {
            const float e = 0.3f;
            float nx = SilverMetalSd(x + e, y) - SilverMetalSd(x - e, y);
            float ny = SilverMetalSd(x, y + e) - SilverMetalSd(x, y - e);
            float nl = Mathf.Sqrt(nx * nx + ny * ny);
            if (nl > 1e-4f) { nx /= nl; ny /= nl; }
            float lit = nx * -0.55f + ny * -0.83f;
            float depth = -sd;

            // On the menu art, a drawn line where the metal lies over the steel - the outline only
            // strokes against transparency, and the guard's arms cross the flare.
            if (fine && depth < 0.2f && SilverSteelHalf(y + ny * 0.4f) > Mathf.Abs(x + nx * 0.4f - SwordAxis))
                return 'S';

            if (depth < (fine ? 0.4f : 0.7f))
            {
                if (lit > 0.55f) return fine ? 'H' : 'L';
                if (lit > 0.15f) return 'L';
                if (lit < -0.40f) return 'S';
                if (lit < -0.05f) return 'D';
            }
            return 'B';
        }

        static char SilverWrap(float x, float y, bool fine)
        {
            float a = (x - SwordAxis) / SilverGripHalf;
            if (!fine)
                return Mathf.Abs(a) > 0.6f || Mathf.RoundToInt(y) % 2 == 1 ? 'w' : 'x';
            float phase = (y + (x - SwordAxis) * 0.6f) / 1.1f;
            bool seam = phase - Mathf.Floor(phase) > 0.78f;
            if (seam || a > 0.62f) return 'w';
            return a < -0.35f ? 'y' : 'x';
        }

        /// <summary>The steel: lit left edge, dark right edge; on the menu art a specular line
        /// and a fuller, and the flare's hollow inner diamond.</summary>
        static char SilverSteel(float x, float y, float half, bool fine)
        {
            float u = (x - (SwordAxis - half)) / (2f * half);

            // The hollow diamond inside the flare: a dark ring, bright inside it.
            float fy = SilverFlareY;
            float iy = y < fy ? (fy - y) / (SilverFlareUp * 0.75f) : (y - fy) / (SilverFlareDown * 0.62f);
            float q = Mathf.Abs(x - SwordAxis) / (SilverFlareHalf * 0.55f) + iy;
            if (y > fy - SilverFlareUp && q < 1f)
                return q > (fine ? 0.78f : 0.6f) ? (fine ? 's' : 'd') : (fine && u < 0.5f ? 'h' : 'l');

            if (fine)
            {
                if (u < 0.06f) return 'h';
                if (y > 8f && y < fy - SilverFlareUp - 0.5f && Mathf.Abs(x - SwordAxis + 0.25f) < 0.25f)
                    return 's';
            }
            if (u < 0.2f) return 'l';
            if (u > 0.8f) return 'd';
            return 'b';
        }

        static char SilverTip(float x, float y)
        {
            float f = Mathf.Clamp(y, -0.5f, 5f);
            float taper;
            if (f < 0f) taper = Mathf.Lerp(0.05f, SwordTipTaper[0], (f + 0.5f) / 0.5f);
            else
            {
                int i = Mathf.Min(SwordTipTaper.Length - 2, Mathf.FloorToInt(f));
                taper = Mathf.Lerp(SwordTipTaper[i], SwordTipTaper[i + 1], f - i);
            }
            float half = taper * SwordBladeWidth * 0.5f;
            if (Mathf.Abs(x - SwordAxis) > half) return '.';
            float u = (x - (SwordAxis - half)) / (2f * half);
            return u < 0.1f ? 'h' : u < 0.25f ? 'l' : u > 0.75f ? 'd' : 'b';
        }


        // ---------------------------------------------------------------- Shadow
        //
        // The second black-diamond weapon, and deliberately nothing like the first. Emberline is
        // opaque, hot and five-coloured; this is one colour, and half of it is not there.
        //
        // Two ideas, and they are the same idea twice:
        //
        //   SEMI-OPAQUE. The blade carries alpha per texel rather than being a dark grey, so what
        //   is behind it shows through - the arm it crosses, the floor, an enemy. It is the only
        //   piece of art in the project that does this, and it is why the palette below is built
        //   by hand instead of coming straight out of Palette.Of, which has no opinion on alpha.
        //
        //   AN EMPTY FULLER. Where Emberline's groove is filled with glass, this one is a hole
        //   straight through the blade. That is the half that survives a screenshot: the two
        //   swords have different SILHOUETTES, not different colours, which is the rule every
        //   weapon in this file is held to.
        //
        // The furniture stays OPAQUE. A guard, grip and pommel you can see through would leave the
        // character gripping nothing, and the contrast is also what tells the player which part of
        // the object is the strange one.

        /// <summary>
        /// Opacity per TONE, not one number for the whole blade - and this is the fix that made
        /// the weapon work at all.
        ///
        /// A blade at one flat alpha is a translucent shape with no edge: over the arena's dark
        /// floor it nearly disappears, and over anything bright it takes that colour and reads as
        /// washed-out brown rather than as black. Rendered that way it looked less like a shadow
        /// than like a sprite that had failed to load.
        ///
        /// Weighting the alpha to the LIT tones instead gives the blade a nearly solid rim and a
        /// middle that is barely there, which is what a shadow of a sword should look like: the
        /// silhouette holds against any background, and the part you see through is the part
        /// between the edges. Indexed by the same grid characters as the ramp, so a tone's
        /// brightness and its opacity are declared side by side.
        /// </summary>
        static readonly (char C, float A)[] ShadowAlphas =
        {
            ('h', 0.92f),   // the lit rim - all but solid, and the whole silhouette rests on it
            ('l', 0.82f),
            ('b', 0.56f),
            ('d', 0.66f),   // the hollow's walls - an edge, so dense
            ('s', 0.28f),   // the middle of each flat: clearest, shadow at its most absent
            ('k', 0.85f),
        };

        static Dictionary<char, Color> ShadowPal()
        {
            // Not black. Palette.Ramp derives its Line by lerping the base 68% toward near-black,
            // so a base that is already black leaves the blade with no rim and no internal tones -
            // the same trap the Obsidian skin documents. A dark blue-grey reads as black beside
            // anything else on screen and still has five tones to shade with.
            var steel = new Palette.Ramp(new Color(0.17f, 0.16f, 0.24f));
            // Lifted well past the blade's own lift: the crescents are dark iron against a dark
            // blade, and only their lit edges can separate the two.
            var iron  = new Palette.Ramp(new Color(0.30f, 0.29f, 0.37f), lift: 0.52f, shade: 0.34f);
            var grip  = new Palette.Ramp(new Color(0.24f, 0.19f, 0.20f));

            var map = Palette.Of(steel, iron, grip);

            // Alpha on the BLADE only - the lowercase set. The uppercase furniture and the digit
            // grip stay solid, because a guard and a grip you can see through leave the character
            // holding nothing, and the contrast is also what tells the player which half of the
            // object is the strange one.
            foreach (var (c, a) in ShadowAlphas)
            {
                var col = map[c];
                col.a = a;
                map[c] = col;
            }
            return map;
        }

        /// <summary>
        /// Half-width of the empty fuller, in texels, at a given blade row.
        ///
        /// Wider than Emberline's (<see cref="FullerHalf"/>), and the extra width is not a style
        /// choice - it is the auto-outline. StrokeOutline paints every transparent texel touching
        /// an opaque one, which includes the INSIDE of a hole, so at 75 ppu (two passes) a fuller
        /// loses two texels off each edge before anything is left of it. This one is six across
        /// and keeps two; four would close up entirely. The disc's hollow already learned exactly
        /// this - see BuildDisc.
        ///
        /// IT WAS EIGHT, and eight was wrong. The blade is eighteen texels wide, and the four
        /// outlined edges (two outer, two round the fuller) already spend eight of them - so at
        /// eight across the groove there were only five texels of metal left on each side, and
        /// the sword read as a TUNING FORK rather than as a blade with a hole in it. Six leaves
        /// six a side, which is the least that still reads as a flat. The menu grid has room for a
        /// generous one and takes it; this is the density where the idea has to be rationed.
        ///
        /// Still a LENS rather than a slot: it closes before the tip and before the ricasso, where
        /// it narrows past what the outline leaves and simply reads as a dark line down the blade.
        /// A constant width would cut the sword into two blades held side by side.
        ///
        /// NARROWING THE BLADE DOES NOT SHRINK THIS PROPORTIONALLY, and that is the whole reason
        /// it has a clamp of its own rather than just riding <see cref="SwordBladeScale"/> like
        /// Emberline's glass does. The outline's bite is an ABSOLUTE number of texels off each of
        /// the four edges, so it takes a steadily larger share of a narrower blade: scaled by width
        /// alone the fuller kept its ratio and the flats lost theirs, and the tuning fork came
        /// straight back. The flats are served FIRST - each keeps <see cref="ShadowMinFlat"/> - and
        /// the groove takes whatever the middle has left.
        /// </summary>
        static float ShadowFullerHalf(float y)
        {
            float t = Mathf.Clamp01((y - 6f) / (BladeRows - 1f));
            float half = Mathf.Min(3.2f * SwordBladeScale,
                                   (SwordBladeWidth - ShadowMinFlat * 2) * 0.5f);
            return half * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.30f);
        }

        /// <summary>
        /// Cells of flat the fuller may never eat into, either side.
        ///
        /// THREE, and the accounting is worth writing down because getting it wrong by one cell
        /// closes the fuller completely. The stroke is added OUTSIDE the silhouette, so the blade's
        /// outer edges cost the flats nothing; it is only the HOLE that pays, losing the stroke's
        /// full thickness off each of its two sides. On a ten-cell blade at two texels of stroke:
        ///
        ///     flat 4  ->  hole 2 cells = 4 texels, less 4 of stroke = NOTHING. A dark line.
        ///     flat 3  ->  hole 4 cells = 8 texels, less 4 = 4 texels of real hole, on 6 of flat.
        ///
        /// Four was the first guess and it is what the sword came back from the narrowing looking
        /// like: solid, with a groove scored down it. Three is the floor.
        /// </summary>
        const int ShadowMinFlat = 3;

        /// <summary>
        /// Left flat and right flat, outer edge inward. One glow texel on the lit side and one
        /// light on the shadowed side, then straight into the dark - and the split is what keeps
        /// the sword BLACK.
        ///
        /// The first pass ran two light tones down each flat, which on a six-texel flat is a
        /// third of the blade. Zoomed up it read as a silver sword with a dark core rather than a
        /// black one with a lit edge. The rim has to be exactly as wide as it needs to be to hold
        /// the silhouette, which is one texel.
        /// </summary>
        const string ShadowLit = "hlbddd";
        const string ShadowShade = "ldssss";

        /// <summary>
        /// Re-tone one row of the greatsword's own tip silhouette into the shadow ramp.
        ///
        /// The SHAPE is deliberately the gilded greatsword's, texel for texel - these are two
        /// weapons of the same class and the point of difference is the fuller and the alpha, not
        /// the outline of the point. Only the tones are recomputed, by the same rule the flats
        /// below use, so the tip cannot end up lit differently from the blade under it.
        /// </summary>
        static string ShadowTone(string shape)
        {
            int lo = shape.IndexOfAny(new[] { 'l', 'b', 'd', 's', 'h' });
            int hi = shape.LastIndexOfAny(new[] { 'l', 'b', 'd', 's', 'h' });
            if (lo < 0) return shape;

            var line = shape.ToCharArray();
            float mid = (lo + hi) * 0.5f;
            for (int x = lo; x <= hi; x++)
            {
                bool left = x <= mid;
                float span = Mathf.Max(1f, mid - lo);
                float u = left ? (x - lo) / span : (hi - x) / span;
                int band = Mathf.Clamp(Mathf.RoundToInt(u * 5f), 0, 5);
                line[x] = left ? ShadowLit[band] : ShadowShade[band];
            }
            return new string(line);
        }

        // ============================================================== Saint
        //
        // A GLASS greatsword with a gold edge, on the "clover" hilt from the user's reference
        // sheet, and (at runtime, see SaintHalo) a ring hanging round the blade:
        //
        //   GUARD   three open gold rings - one each side at the grip's end, one between and
        //           above them that the blade ends in, a small Y inside it - and the Saint's own
        //           WINGS sweeping up and out from the side rings. The wings are what makes it a
        //           different weapon rather than a recolour, so they stay.
        //   GRIP    long ivory, between gold ferrules
        //   POMMEL  a small clover of three rings, with little wings sweeping DOWN and out - the
        //           guard's, turned over to face the way the pommel does
        //
        // THE GLASS reads by its EDGES. It used to be grey bands at 38-88% opacity across the
        // whole width, which on any background is pale steel. Now: nearly clear through the
        // middle, a darker refraction line just inside each gold edge (light bends hardest there,
        // so a pane's edge always reads darker than its face), a bright highlight streak down the
        // lit side, and in the menu art a few diagonal glints.
        //
        // One continuous field (SaintTexel) for the arena (a sample a cell) and the menu art (four),
        // so the two cannot disagree at the silhouette. Coordinates are authored cells, y down.

        const float SaintAxis = SwordAxis;

        // The pommel's clover: two rings side by side under the grip, one below them.
        static float SaintPommelBottomY => SwordHeightRows - 0.5f;
        const float SaintPommelR = 2.3f, SaintPommelBand = 0.8f;
        static Vector2 SaintPommelLow => new(0f, SaintPommelBottomY - SaintPommelR);
        static float SaintPommelSideY => SaintPommelLow.y - 3.0f;
        const float SaintPommelSideX = 2.6f;

        // The grip: SwordGripRows, ending where the pommel's side rings begin.
        static int SaintGripTop => Mathf.FloorToInt(SaintPommelSideY - SaintPommelR) - SwordGripRows + 1;

        // The guard's clover: side rings level with the grip's end, the centre ring above.
        // Holes wide enough to SURVIVE the arena outline, which eats a cell all round any gap:
        // at radius 3.2 they closed to pinholes and the rings read as gold blobs.
        const float SaintRingR = 3.6f, SaintRingBand = 1.05f;
        const float SaintSideRingX = 6.0f;
        static float SaintSideRingY => SaintGripTop - 0.5f - SaintRingR + 0.6f;
        static float SaintCentreRingY => SaintSideRingY - 3.9f;

        /// <summary>Where the blade ends: inside the centre ring's band, so the ring holds it.</summary>
        static float SaintBladeEnd => SaintCentreRingY - SaintRingR + SaintRingBand * 0.5f;

        /// <summary>The grip's second row, like every greatsword's pivot.</summary>
        static Vector2Int SaintGrip => new(13, GripCentre(SaintGripTop, SwordGripRows));
        static Vector2Int SaintDetailGrip => new(SaintGrip.x * EmberDetailScale, SaintGrip.y * EmberDetailScale);

        /// <summary>
        /// How far up the blade the halo hangs, in the sprite's own units above the fist: the
        /// blade's middle, counted off this grid. It was a literal (0.48) counted off the old one,
        /// and the new hilt moved the fist four rows.
        /// </summary>
        /// <summary>
        /// The halo's diameter: a set multiple of the blade's own width, so it rings the blade
        /// closely rather than standing off it. It was a literal 0.60 - four and a half blades
        /// across, a hoop round the character's shoulder rather than a ring on a slim glass sword.
        /// </summary>
        public static float SaintHaloSize => SwordBladeWidth * FineUpscale / FinePpu * 2.4f;

        public static float SaintHaloAlong
            => (SaintGrip.y - (6f + SaintBladeEnd) * 0.5f) * FineUpscale / FinePpu;

        static Dictionary<char, Color> SaintPal()
        {
            // The glass: pale, faintly cyan. Lift LOW on the gold for the reason the edge is one
            // texel wide - at 0.45 its light tone read as a highlight on the glass, not as gold.
            var glass = new Palette.Ramp(new Color(0.80f, 0.92f, 1.00f), lift: 0.70f, shade: 0.40f);
            var gold  = new Palette.Ramp(new Color(0.94f, 0.76f, 0.30f), lift: 0.28f);
            var ivory = new Palette.Ramp(new Color(0.88f, 0.86f, 0.80f));
            var map = Palette.Of(glass, gold, ivory);

            // GLASS IS SEE-THROUGH, and most of it very nearly clear. The highlight and the
            // refraction lines are what is solid - light caught is the part of a pane you cannot
            // see through - and the gold stays fully opaque, so the contrast between them is what
            // says "material" rather than "faded sprite".
            foreach (var (c, a) in SaintGlassAlphas)
            {
                var col = map[c];
                col.a = a;
                map[c] = col;
            }
            return map;
        }

        static readonly (char C, float A)[] SaintGlassAlphas =
        {
            ('h', 0.95f), ('l', 0.70f), ('b', 0.28f), ('d', 0.62f), ('s', 0.50f),
        };

        static string[] BuildSaintSword()
        {
            var rows = new string[SwordHeightRows];
            for (int y = 0; y < rows.Length; y++)
            {
                var line = new char[SwordCanvas];
                for (int x = 0; x < SwordCanvas; x++) line[x] = SaintTexel(x, y, fine: false);
                rows[y] = new string(line);
            }
            return rows;
        }

        /// <summary>Menu art at TRUE <see cref="MenuPpu"/> - never pass it upscale2x.</summary>
        static string[] BuildSaintSwordDetail()
        {
            const int k = EmberDetailScale;
            var rows = new string[SwordHeightRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[SwordCanvas * k];
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++)
                    line[X] = SaintTexel((X + 0.5f) / k - 0.5f, y, fine: true);
                rows[Y] = new string(line);
            }
            return rows;
        }

        static readonly string[] SaintSwordRows = BuildSaintSword();
        static readonly string[] SaintSwordDetail = BuildSaintSwordDetail();

        static char SaintTexel(float x, float y, bool fine)
        {
            char hilt = SaintHilt(x, y, fine);
            if (hilt != '.') return hilt;
            return SaintGlassTexel(x, y, fine);
        }

        /// <summary>The blade: a gold edge each side, glass between.</summary>
        static char SaintGlassTexel(float x, float y, bool fine)
        {
            if (y > SaintBladeEnd || y < -0.5f) return '.';
            float half = Mathf.Min(y + 1f, SwordBladeWidth / 2f);           // the tip's taper
            float left = x - (SaintAxis - half), right = SaintAxis + half - x;
            if (left < 0f || right < 0f) return '.';

            // The gold cutting edge, lit on the near side.
            float edge = fine ? 0.75f : 1f;
            if (left < edge) return 'L';
            if (right < edge) return 'B';

            // Near the point there is no room for anything but glass and its edges.
            float inside = Mathf.Min(left, right) - edge;
            if (half < 2.5f) return inside < 0.5f ? 'd' : 'l';

            // The refraction line just inside each edge.
            if (inside < (fine ? 0.45f : 1f)) return 'd';

            float u = left / (2f * half);
            if (Mathf.Abs(u - 0.30f) < (fine ? 0.035f : 0.06f)) return 'h';    // the highlight
            if (fine)
            {
                if (Mathf.Abs(u - 0.70f) < 0.02f) return 'l';                  // a fainter one
                float g = (y + (x - SaintAxis) * 1.2f) / 11f;
                if (g - Mathf.Floor(g) < 0.045f) return 'l';                    // the glints
            }
            return 'b';
        }

        // ---- the hilt

        /// <summary>Distance to a ring's band (negative inside it), and where round it the point is.</summary>
        static float SaintRingSd(float x, float y, float cx, float cy, float r, float band, out float t, out float nx, out float ny)
        {
            float dx = x - cx, dy = y - cy;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            nx = d > 1e-4f ? dx / d : 0f;
            ny = d > 1e-4f ? dy / d : 0f;
            t = Mathf.Clamp01((d - (r - band)) / band);                          // 0 inner, 1 outer
            return Mathf.Abs(d - (r - band * 0.5f)) - band * 0.5f;
        }

        /// <summary>A tapered feather, root to tip: distance, and how far along it the point is.</summary>
        static float SaintFeatherSd(float x, float y, Vector2 root, Vector2 tip, float rootHalf, float tipHalf,
                                    out float along, out float across)
        {
            Vector2 v = tip - root, p = new Vector2(x, y) - root;
            float len = v.magnitude;
            along = Mathf.Clamp01(Vector2.Dot(p, v) / (len * len));
            Vector2 q = p - v * along;
            across = (q.x * -v.y + q.y * v.x) / len;                            // signed, perpendicular
            return q.magnitude - Mathf.Lerp(rootHalf, tipHalf, along);
        }

        /// <summary>The guard's wings, up and out; the pommel's, the same turned over.</summary>
        static char SaintWings(float ax, float y, bool fine)
        {
            // ax is the distance from the axis - both sides share one wing.
            float gy = SaintSideRingY, py = SaintPommelSideY;
            var feathers = new (Vector2 root, Vector2 tip, float rh, float th)[]
            {
                (new(SaintSideRingX + 2.6f, gy - 1.9f), new(12.9f, gy - 8.6f), 1.25f, 0.30f),
                (new(SaintSideRingX + 3.1f, gy + 0.2f), new(12.6f, gy - 4.6f), 0.95f, 0.25f),
                (new(SaintPommelSideX + 1.9f, py + 0.8f), new(7.6f, py + 4.6f), 0.80f, 0.20f),
                (new(SaintPommelSideX + 1.8f, py - 0.4f), new(7.3f, py + 2.1f), 0.60f, 0.20f),
            };
            for (int i = 0; i < feathers.Length; i++)
            {
                var f = feathers[i];
                float sd = SaintFeatherSd(ax, y, f.root, f.tip, f.rh, f.th, out float along, out float across);
                if (sd >= 0f) continue;
                // Lit along the edge that faces up (and out), shaded along the other: a feather
                // is a blade of its own, and one flat tone reads as a paper cut-out.
                float side = across / Mathf.Max(0.2f, Mathf.Lerp(f.rh, f.th, along));
                bool up = i < 2 ? side > 0f : side < 0f;
                if (fine && -sd < 0.18f) return up ? 'H' : 'S';
                if (-sd < (fine ? 0.4f : 0.6f)) return up ? 'L' : 'D';
                return 'B';
            }
            return '.';
        }

        static char SaintGold(float t, float nx, float ny, bool fine)
        {
            // A ring is a tube: brightest along the crest of the band on the side facing the light
            // (upper left), darkest on the far side and in the band's inner and outer edges.
            float crest = 1f - Mathf.Abs(t * 2f - 1f);
            float facing = -nx * 0.55f - ny * 0.83f;
            float v = crest * 0.6f + facing * 0.5f;
            if (fine && v > 0.72f) return 'H';
            if (v > 0.45f) return 'L';
            if (v > 0.05f) return 'B';
            if (v > -0.30f) return 'D';
            return 'S';
        }

        static char SaintHilt(float x, float y, bool fine)
        {
            float ax = Mathf.Abs(x - SaintAxis);

            // The guard's rings. The centre one first, with its Y.
            float sd = SaintRingSd(x, y, SaintAxis, SaintCentreRingY, SaintRingR, SaintRingBand,
                                   out float t, out float nx, out float ny);
            if (sd < 0f) return SaintGold(t, nx, ny, fine);
            for (int s = -1; s <= 1; s += 2)
            {
                sd = SaintRingSd(x, y, SaintAxis + s * SaintSideRingX, SaintSideRingY, SaintRingR, SaintRingBand,
                                 out t, out nx, out ny);
                if (sd < 0f) return SaintGold(t, nx, ny, fine);
            }

            // The Y inside the centre ring: three spokes from its middle to the band.
            {
                float px = x - SaintAxis, py = y - SaintCentreRingY;
                float reach = SaintRingR - SaintRingBand + 0.2f, w = fine ? 0.32f : 0.5f;
                float up1 = SegmentDistance(px, py, 0f, 0f, -reach * 0.72f, -reach * 0.72f);
                float up2 = SegmentDistance(px, py, 0f, 0f, reach * 0.72f, -reach * 0.72f);
                float down = SegmentDistance(px, py, 0f, 0f, 0f, reach);
                float d = Mathf.Min(up1, Mathf.Min(up2, down));
                if (d < w) return fine && d < w * 0.4f && px < 0.1f ? 'L' : 'B';
            }

            char wing = SaintWings(ax, y, fine);
            if (wing != '.') return wing;

            // The pommel's rings.
            sd = SaintRingSd(x, y, SaintAxis, SaintPommelLow.y, SaintPommelR, SaintPommelBand, out t, out nx, out ny);
            if (sd < 0f) return SaintGold(t, nx, ny, fine);
            for (int s = -1; s <= 1; s += 2)
            {
                sd = SaintRingSd(x, y, SaintAxis + s * SaintPommelSideX, SaintPommelSideY, SaintPommelR, SaintPommelBand,
                                 out t, out nx, out ny);
                if (sd < 0f) return SaintGold(t, nx, ny, fine);
            }

            // The grip: ivory between gold ferrules, lit on the left.
            float gTop = SaintGripTop - 0.5f, gBot = SaintGripTop + SwordGripRows - 0.5f;
            if (y > gTop && y < gBot && ax < SwordGripWidth / 2f)
            {
                if (y < gTop + 0.8f || y > gBot - 0.8f)
                    return x < SaintAxis - 1.5f ? 'L' : x > SaintAxis + 1.5f ? 'D' : 'B';
                float a = (x - SaintAxis) / (SwordGripWidth / 2f);
                if (fine && a < -0.7f) return '6';
                return a < -0.4f ? '5' : a > 0.45f ? '3' : '4';
            }
            return '.';
        }

        // ============================================================== Prism

        /// <summary>
        /// Prism's fixed accents: three hues threaded along the edge (the "prismatic" half) and
        /// four element gems (the "attunement" half). Neither is a shading ramp, so both are
        /// added directly to the dictionary rather than through <see cref="Palette.Of"/>.
        ///
        /// THE FRINGE, NOT THE WHOLE BLADE, IS THE RAINBOW. A real prism throws colour at its
        /// EDGE - a thin fringe where the glass bends light - and keeps the bulk of the material
        /// clear; painting the whole blade in bands of hue reads as a dyed sword, not a prism. So
        /// the interior stays a plain pale glass (see <see cref="PrismGlass"/>) and the two edge
        /// columns carry three hand-off hues down the length: violet at the guard, aqua through
        /// the middle, pale gold toward the tip - one soft sweep, not a repeating stripe.
        /// </summary>
        static readonly Color PrismViolet = new(0.62f, 0.48f, 0.92f);
        static readonly Color PrismAqua   = new(0.42f, 0.86f, 0.88f);
        static readonly Color PrismGold   = new(0.95f, 0.86f, 0.62f);

        /// <summary>
        /// One gem per element, at the colour the game already uses for that element everywhere
        /// else (<see cref="Core.ElementInfo.Tint"/>) - the same white/green/red/blue the sketch
        /// this was built from drew, and not a coincidence: reusing the game's own tint means a
        /// player who already knows "blue is Water" reads the lit gem for free.
        /// </summary>
        static Color GemTint(Core.ElementType e) => Core.ElementInfo.Tint(e);

        /// <summary>
        /// A gem's colour when it is NOT the run's element - dulled toward a cold neutral rather
        /// than left at full saturation, so the one LIT gem is unambiguous at a glance. Kept
        /// short of grey: a fully desaturated gem would stop reading as "this is the fire slot,
        /// just unlit" and start reading as a missing socket.
        /// </summary>
        static Color GemDim(Core.ElementType e)
            => Color.Lerp(GemTint(e), new Color(0.20f, 0.21f, 0.26f), 0.52f);

        /// <summary>
        /// The glass ramp shared by every Prism variant. Deliberately plainer than Saint's - this
        /// sword's one idea is the gem row and the edge fringe, and a second showy material would
        /// fight them for attention. See "spend your uppercase ramp on ONE idea" elsewhere in this
        /// file for why that restraint is the point rather than a shortfall.
        /// </summary>
        static Dictionary<char, Color> PrismPal(Core.ElementType lit)
        {
            var glass = new Palette.Ramp(new Color(0.86f, 0.90f, 0.94f), lift: 0.42f, shade: 0.30f);
            // The hilt is STONE - the cool violet-grey granite Prism's grip always was - in the
            // shape of the user's metal reference: a mineral hilt on a glass and gem weapon, where
            // silver read as a second, unrelated material. The same granite as before, so the
            // sword keeps the family its fringe belongs to.
            var steel = new Palette.Ramp(new Color(0.50f, 0.48f, 0.55f), lift: 0.24f, shade: 0.34f);
            // The handle's BLACK leather wrap, against the stone lattice sleeve and diamond ring.
            var wrap = new Palette.Ramp(new Color(0.13f, 0.13f, 0.15f), lift: 0.30f, shade: 0.40f);
            var map = Palette.Of(glass, steel, wrap);

            map['v'] = PrismViolet; map['q'] = PrismAqua; map['y'] = PrismGold;

            map['p'] = GemTint(Core.ElementType.Air)   is var air   && lit == Core.ElementType.Air   ? air   : GemDim(Core.ElementType.Air);
            map['a'] = GemTint(Core.ElementType.Earth) is var earth && lit == Core.ElementType.Earth ? earth : GemDim(Core.ElementType.Earth);
            map['i'] = GemTint(Core.ElementType.Fire)  is var fire  && lit == Core.ElementType.Fire  ? fire  : GemDim(Core.ElementType.Fire);
            map['u'] = GemTint(Core.ElementType.Water) is var water && lit == Core.ElementType.Water ? water : GemDim(Core.ElementType.Water);

            // A small bright glint on whichever gem is lit - one texel of near-white, the same
            // "the eye reads the highlight, not the flat fill" trick every gear ramp already uses.
            // On the dim gems this glint is the gem's own base tone instead, so it disappears back
            // into the dull socket rather than sparkling on three "off" slots at once.
            map['o'] = lit == Core.ElementType.Air   ? Color.Lerp(GemTint(lit), Color.white, 0.6f) : GemDim(Core.ElementType.Air);
            map['e'] = lit == Core.ElementType.Earth ? Color.Lerp(GemTint(lit), Color.white, 0.6f) : GemDim(Core.ElementType.Earth);
            map['f'] = lit == Core.ElementType.Fire  ? Color.Lerp(GemTint(lit), Color.white, 0.6f) : GemDim(Core.ElementType.Fire);
            map['t'] = lit == Core.ElementType.Water ? Color.Lerp(GemTint(lit), Color.white, 0.6f) : GemDim(Core.ElementType.Water);
            return map;
        }

        /// <summary>Total blade width including the two edge columns. The family's own now: this
        /// sword's one idea is the gem row, so it has no case for being wider than the sword that
        /// sets the standard, and every case for not being wider than the character's torso.</summary>
        static int PrismBladeWidth => SwordBladeWidth;

        /// <summary>The blunt taper steps four cells a row, then holds one row at full width.</summary>
        static int PrismTipRowCount => (PrismBladeWidth - 6) / 4 + 2;

        static int PrismBladeRows =>
            BladeRowsFor(PrismTipRowCount + PrismGuardRows + PrismGripRows + PrismPommelRows);

        /// <summary>Interior glass ramp, one dip and one recover, resampled to fill however many
        /// columns a given row (tip or body) actually has.</summary>
        const string PrismGlassRamp = "hhllbddbbllh";

        /// <summary>One full-width blade row, no gem - the edge fringe hue is passed in per band
        /// so a single row shape serves the whole length.</summary>
        static string PrismBladeRow(char edge) => PrismSpan(PrismBladeWidth, edge);

        /// <summary>
        /// A blunt, rounded tip - the plainest of the four Diamond blades on purpose. Where
        /// Emberline tapers to a point and Saint's wings claim the silhouette, Prism spends nothing
        /// on the outline: the gem row is the only thing this sword asks the eye to look at, and a
        /// dramatic point would be a second thing competing with it.
        /// </summary>
        static string PrismTipRow(int wide, char edge) => PrismSpan(wide, edge);

        /// <summary>Shared row builder: `wide` columns centred on the 28-wide grid, edge colour at
        /// the two ends and <see cref="PrismGlassRamp"/> resampled across whatever is between.</summary>
        static string PrismSpan(int wide, char edge)
        {
            var line = new char[28];
            for (int x = 0; x < 28; x++) line[x] = '.';
            int from = 14 - wide / 2, to = from + wide - 1;
            for (int x = from; x <= to; x++)
            {
                if (x == from || x == to) { line[x] = edge; continue; }
                int inner = wide - 2;
                int i = inner <= 1 ? 0 : (x - from - 1) * (PrismGlassRamp.Length - 1) / (inner - 1);
                line[x] = PrismGlassRamp[i];
            }
            return new string(line);
        }

        /// <summary>
        /// A rounded cabochon socketed into the glass at the blade's centreline - three rows, not
        /// one: a single-row dot measured a texel or two of colour against the ramp's own dark
        /// fold tone and was nearly invisible at this density. Three rows with a bezel top and
        /// bottom reads as a bead set into the blade rather than a stray pixel.
        ///
        ///     . S S .        bezel cap
        ///     S g g S        the fill - `glint`/`gem` are the same colour on the lit gem and a
        ///     . S S .        touch different on a dim one, see BuildPrismSword
        ///
        /// `S` is the steel ramp's DEEP tone rather than its Dark one - the darkest thing on the
        /// blade, so the ring reads as a socket rather than blending into the glass's own shading.
        /// </summary>
        static string[] PrismGemRows(char gem, char glint, char edge)
        {
            var cap = PrismBladeRow(edge).ToCharArray();
            cap[13] = 'S'; cap[14] = 'S';
            var fill = PrismBladeRow(edge).ToCharArray();
            fill[12] = 'S'; fill[13] = glint; fill[14] = gem; fill[15] = 'S';
            return new[] { new string(cap), new string(fill), new string(cap) };
        }

        /// <summary>
        /// Prism: a plain glass greatsword with a hand-off violet/aqua/gold edge and four socketed
        /// gems down the centreline, one per element. Built once per element as the LIT one - see
        /// <see cref="BuildPrismSet"/> - so a repaint is a palette swap, never a re-layout.
        ///
        /// The HILT is the user's reference crossguard, carved in Prism's own violet-grey STONE: long
        /// arms flaring into notched fishtail ends, a lozenge boss (knotwork cut in the menu art)
        /// with a langet running up the blade, and a handle in BLACK AND STONE - black leather wrap
        /// by the guard, a stone diamond ring half way, and a stone lattice sleeve toward the
        /// pommel, which forks into two prongs. See <see cref="PrismHiltTexel"/>.
        /// </summary>
        static string[] BuildPrismSword()
        {
            var rows = new List<string>(SwordHeightRows);

            for (int w = 6; w <= PrismBladeWidth; w += 4) rows.Add(PrismTipRow(w, 'v'));   // blunt taper
            rows.Add(PrismTipRow(PrismBladeWidth, 'v'));

            // Four gem bands, evenly spaced, each stamped with the edge hue for ITS band.
            // Visual top-to-bottom order only: Air, Earth, Fire, Water - independent of
            // ElementType's own declared enum order, which PrismGemAlongs keys off separately.
            var gemCh = new[] { 'p', 'a', 'i', 'u' };
            var glintCh = new[] { 'o', 'e', 'f', 't' };
            int gap = PrismBladeRows / 5;

            int i = 0;
            int nextGem = gap;
            int gemIdx = 0;
            while (i < PrismBladeRows)
            {
                char edge = PrismEdgeAt(i);
                if (gemIdx < 4 && i == nextGem && i + 2 < PrismBladeRows)
                {
                    rows.AddRange(PrismGemRows(gemCh[gemIdx], glintCh[gemIdx], edge));
                    i += 3;
                    gemIdx++;
                    nextGem = gap * (gemIdx + 1);
                    continue;
                }
                rows.Add(PrismBladeRow(edge));
                i++;
            }
            while (rows.Count < SwordHeightRows) rows.Add(new string('.', 28));

            // The hilt, laid over the lot - the langet reaches up onto the blade.
            for (int y = PrismGuardTop - 8; y < rows.Count; y++)
            {
                var line = rows[y].ToCharArray();
                for (int x = 0; x < line.Length; x++)
                {
                    char c = PrismHiltTexel(x, y, fine: false);
                    if (c != '.') line[x] = c;
                }
                rows[y] = new string(line);
            }
            return rows.ToArray();
        }

        /// <summary>The edge fringe's hue for blade row <paramref name="i"/>, counted from the tip.</summary>
        static char PrismEdgeAt(float i)
        {
            float t = i / PrismBladeRows;
            return t < 0.34f ? 'v' : t < 0.67f ? 'q' : 'y';
        }

        static readonly string[] PrismRows = BuildPrismSword();

        const int PrismGuardRows = 4, PrismGripRows = 15, PrismPommelRows = 4;   // grip +5 with every grip
        static int PrismGuardTop => PrismTipRowCount + PrismBladeRows;
        static int PrismGripTop => PrismGuardTop + PrismGuardRows;
        static int PrismPommelTop => PrismGripTop + PrismGripRows;
        static float PrismGuardY => PrismGuardTop + 1.5f;

        /// <summary>The grip's second row, like every greatsword's pivot.</summary>
        static Vector2Int PrismGrip => new(13, GripCentre(PrismGripTop, PrismGripRows));
        static Vector2Int PrismDetailGrip => new(PrismGrip.x * EmberDetailScale, PrismGrip.y * EmberDetailScale);

        /// <summary>Menu art at TRUE <see cref="MenuPpu"/>, the fire gem lit: the blade from
        /// <see cref="PrismBladeTexel"/>, the hilt over it.</summary>
        static string[] BuildPrismSwordDetail()
        {
            const int k = EmberDetailScale;
            var gems = PrismGemRowsFound();
            var rows = new string[SwordHeightRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[SwordCanvas * k];
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++)
                {
                    float x = (X + 0.5f) / k - 0.5f;
                    char c = PrismHiltTexel(x, y, fine: true);
                    line[X] = c != '.' ? c : PrismBladeTexel(x, y, gems);
                }
                rows[Y] = new string(line);
            }
            return rows;
        }

        static readonly string[] PrismSwordDetail = BuildPrismSwordDetail();

        /// <summary>Each gem's fill row in the arena grid, and its two tones - read back off the
        /// built grid so the menu art's gems sit exactly where the arena's do.</summary>
        static (int Row, char Gem, char Glint)[] PrismGemRowsFound()
        {
            var gemCh = new[] { 'p', 'a', 'i', 'u' };
            var glintCh = new[] { 'o', 'e', 'f', 't' };
            var found = new List<(int, char, char)>();
            for (int g = 0; g < 4; g++)
                for (int r = 0; r < PrismRows.Length; r++)
                    if (PrismRows[r].Length > 14 && PrismRows[r][14] == gemCh[g]) { found.Add((r, gemCh[g], glintCh[g])); break; }
            return found.ToArray();
        }

        /// <summary>
        /// The blade as a field for the menu art: the same rounded blunt point, edge fringe and
        /// glass ramp as the arena rows, and round cabochons where the arena has its three-row
        /// gems - a bezel ring, the stone, and a glint on its upper left.
        /// </summary>
        static char PrismBladeTexel(float x, float y, (int Row, char Gem, char Glint)[] gems)
        {
            if (y < -0.5f || y > PrismGuardTop - 0.5f) return '.';
            float half = PrismBladeWidth / 2f * Mathf.Sqrt(Mathf.Clamp01((y + 0.5f) / 1.6f));
            float left = x - (SwordAxis - half), right = SwordAxis + half - x;
            if (left < 0f || right < 0f) return '.';

            foreach (var g in gems)
            {
                float dx = x - SwordAxis, dy = y - g.Row;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < 1.5f)
                {
                    if (d > 1.1f) return 'S';
                    float gx = dx + 0.45f, gy = dy + 0.45f;
                    return gx * gx + gy * gy < 0.12f ? g.Glint : g.Gem;
                }
            }

            char edge = y < PrismTipRowCount ? 'v' : PrismEdgeAt(y - PrismTipRowCount);
            if (left < 0.75f || right < 0.75f) return edge;
            float u = Mathf.Clamp01((left - 0.75f) / Mathf.Max(0.5f, 2f * half - 1.5f));
            return PrismGlassRamp[Mathf.Clamp(Mathf.FloorToInt(u * PrismGlassRamp.Length), 0, PrismGlassRamp.Length - 1)];
        }

        // ---- the hilt: carved stone, and a black and stone handle

        const float PrismArmReach = 10.4f;       // 21 cells tip to tip - the arms ARE this hilt
        const float PrismFlareFrom = 8.2f;

        /// <summary>Signed distance to the crossguard: tapering arms, flared notched ends, the
        /// lozenge boss, and a point up the blade and down onto the grip.</summary>
        static float PrismGuardSd(float x, float y)
        {
            float dx = Mathf.Abs(x - SwordAxis), dy = y - PrismGuardY, ady = Mathf.Abs(dy);

            // Arms: a bar tapering from the boss to the flare, then flaring into a fishtail whose
            // end is notched, so each terminal ends in two points - the reference's split ends.
            float halfH = dx < PrismFlareFrom
                ? Mathf.Lerp(1.15f, 0.75f, dx / PrismFlareFrom)
                : Mathf.Lerp(0.75f, 2.1f, (dx - PrismFlareFrom) / (PrismArmReach - PrismFlareFrom));
            float end = PrismArmReach - 1.0f * (1f - Mathf.Clamp01(ady / 2.1f));
            float arm = Mathf.Max(ady - halfH, dx - end);

            float boss = (dx / 3.3f + ady / 2.3f - 1f) * 1.8f;

            // The langet up the blade, and a shorter point down onto the grip.
            float upTop = PrismGuardY - 7.5f, upBase = PrismGuardY - 1.5f;
            float up = y < upBase && y > upTop ? dx - 2.0f * (y - upTop) / (upBase - upTop) : 99f;
            float dnBase = PrismGuardY + 1.5f, dnTip = PrismGuardY + 3.8f;
            float down = y > dnBase && y < dnTip ? dx - 1.4f * (dnTip - y) / (dnTip - dnBase) : 99f;

            return Mathf.Min(Mathf.Min(arm, boss), Mathf.Min(up, down));
        }

        /// <summary>Signed distance to the pommel: a collar, then two prongs forking down and out.</summary>
        static float PrismPommelSd(float x, float y)
        {
            float dx = Mathf.Abs(x - SwordAxis);
            float top = PrismPommelTop - 0.5f;
            float collar = Mathf.Max(dx - 3.3f, Mathf.Abs(y - (top + 0.5f)) - 0.55f);
            float prong = SegmentDistance(dx, y, 1.0f, top + 1.0f, 3.1f, top + PrismPommelRows - 0.2f)
                          - Mathf.Lerp(0.95f, 0.4f, Mathf.Clamp01((y - top - 1f) / (PrismPommelRows - 1.2f)));
            float bridge = Mathf.Max(dx - 1.6f, Mathf.Abs(y - (top + 1.3f)) - 0.45f);
            return Mathf.Min(collar, Mathf.Min(prong, bridge));
        }

        static char PrismHiltTexel(float x, float y, bool fine)
        {
            float dx = Mathf.Abs(x - SwordAxis);

            float g = PrismGuardSd(x, y);
            if (g < 0f) return PrismSilver(x, y, g, PrismGuardSd, fine, engrave: true);

            float p = PrismPommelSd(x, y);
            if (p < 0f) return PrismSilver(x, y, p, PrismPommelSd, fine, engrave: false);

            float gTop = PrismGripTop - 0.5f, gBot = PrismPommelTop - 0.5f;
            if (y <= gTop || y >= gBot) return '.';

            // The silver diamond ring half way down the handle.
            float ringY = PrismGripTop + 4.5f;
            float ring = (dx / (SwordGripWidth / 2f + 0.8f) + Mathf.Abs(y - ringY) / 1.3f - 1f) * 1.3f;
            if (ring < 0f) return PrismSilver(x, y, ring, (px, py) =>
                (Mathf.Abs(px - SwordAxis) / (SwordGripWidth / 2f + 0.8f) + Mathf.Abs(py - ringY) / 1.3f - 1f) * 1.3f, fine, engrave: false);

            // Toward the pommel: a silver LATTICE sleeve, flaring slightly - diamond crosshatch.
            if (y > ringY)
            {
                float half = Mathf.Lerp(SwordGripWidth / 2f, SwordGripWidth / 2f + 0.4f, (y - ringY) / (gBot - ringY));
                if (dx >= half) return '.';
                float s = x - SwordAxis;
                float a = (y + s) / 1.6f, b = (y - s) / 1.6f;
                float fa = a - Mathf.Floor(a), fb = b - Mathf.Floor(b);
                float line = fine ? 0.22f : 0.42f;
                if (fa < line || fb < line) return 'S';                       // the lattice's gaps
                if (dx > half - (fine ? 0.4f : 0.9f)) return s < 0f ? 'B' : 'D';
                return s < -0.5f ? 'L' : 'B';
            }

            // Toward the guard: BLACK leather wrap, fine ridges, a sheen down the lit side.
            if (dx >= SwordGripWidth / 2f) return '.';
            float wrap = (y + (x - SwordAxis) * 0.25f) / (fine ? 0.75f : 1f);
            bool groove = wrap - Mathf.Floor(wrap) < (fine ? 0.2f : 0.34f);
            float u = (x - SwordAxis) / (SwordGripWidth / 2f);
            if (groove) return '2';
            if (u < -0.55f) return '5';
            return u > 0.5f ? '3' : '4';
        }

        /// <summary>The stone, shaded by the surface normal of whichever shape it belongs to, with
        /// knotwork cut into the boss in the menu art.</summary>
        static char PrismSilver(float x, float y, float sd, System.Func<float, float, float> field, bool fine, bool engrave)
        {
            if (engrave && fine)
            {
                float dx = x - SwordAxis, dy = y - PrismGuardY;
                if (Mathf.Abs(dx) / 3.3f + Mathf.Abs(dy) / 2.3f < 0.82f)
                {
                    // Knotwork: two interlaced sine strands across the boss.
                    float s1 = Mathf.Sin(dx * 2.0f) * 0.75f, s2 = -Mathf.Sin(dx * 2.0f + 0.9f) * 0.75f;
                    if (Mathf.Abs(dy - s1) < 0.13f || Mathf.Abs(dy - s2) < 0.13f) return 'S';
                    if (Mathf.Abs(dy - s1) < 0.26f || Mathf.Abs(dy - s2) < 0.26f) return 'L';
                }
            }

            const float e = 0.3f;
            float nx = field(x + e, y) - field(x - e, y);
            float ny = field(x, y + e) - field(x, y - e);
            float nl = Mathf.Sqrt(nx * nx + ny * ny);
            if (nl > 1e-4f) { nx /= nl; ny /= nl; }
            float lit = nx * -0.55f + ny * -0.83f;
            if (-sd < (fine ? 0.4f : 0.75f))
            {
                if (lit > 0.55f) return fine ? 'H' : 'L';
                if (lit > 0.15f) return 'L';
                if (lit < -0.45f) return 'S';
                if (lit < -0.05f) return 'D';
            }
            return 'B';
        }

        /// <summary>
        /// Bakes the four lit-gem variants, sharing one grid so only the palette differs - the
        /// same shape <see cref="HeatSprites"/> uses for the fire cycle. Identical geometry means
        /// <see cref="ICharacterRig.SetWeaponSprite"/> can swap between them with no offset, pivot
        /// or size change; the only thing moving is which gem is bright.
        /// </summary>
        static Sprite[] BuildPrismSet()
        {
            var all = new Sprite[4];
            foreach (Core.ElementType e in System.Enum.GetValues(typeof(Core.ElementType)))
                all[(int)e] = StageSprite("gear.weapon.prism." + e, PrismRows, PrismPal(e), PrismGrip);
            return all;
        }

        /// <summary>The four lit-gem variants of the MENU art - built exactly as the menu layer is
        /// (outline on, true MenuPpu, the same pivot), so swapping one in changes only the gem.</summary>
        static Sprite[] BuildPrismMenuSet()
        {
            var all = new Sprite[4];
            foreach (Core.ElementType e in System.Enum.GetValues(typeof(Core.ElementType)))
                all[(int)e] = PixelSprite.From("gear.weapon.prism.menu." + e, PrismSwordDetail, PrismPal(e),
                                               outline: true, pivotTexel: PrismDetailGrip, pixelsPerUnit: MenuPpu);
            return all;
        }

        /// <summary>
        /// Where each element's gem actually sits, DERIVED by scanning the built grid rather than
        /// counted by hand - the row-width bug this sword's first pass shipped with is exactly the
        /// class of error hand-counting a 62-row grid invites. Each gem's fill row carries a fixed
        /// character at column 14 regardless of which element ends up lit (see
        /// <see cref="BuildPrismSword"/>), so it is findable after the fact; the offset then
        /// follows the same "rows above the grip, converted by ppu" arithmetic Saint's own `along`
        /// used, and was checked the same way - measured live, not assumed.
        /// </summary>
        static float[] PrismGemAlongs()
        {
            // Indexed by (int)ElementType, NEVER by visual top-to-bottom position - ElementType's
            // declared order (Fire, Water, Earth, Air) does not match the order these gems are
            // drawn in (Air, Earth, Fire, Water), and writing this by loop position instead of by
            // the real enum value is exactly the ordinal mismatch this file's own notes on
            // EnemyDef and JsonUtility enum serialisation warn about. Caught live: Fire's along
            // came back 0.547 - Air's position - before this was keyed off the enum value.
            var charFor = new System.Collections.Generic.Dictionary<Core.ElementType, char>
            {
                [Core.ElementType.Air] = 'p', [Core.ElementType.Earth] = 'a',
                [Core.ElementType.Fire] = 'i', [Core.ElementType.Water] = 'u',
            };
            var along = new float[4];
            foreach (var (element, ch) in charFor)
            {
                for (int r = 0; r < PrismRows.Length; r++)
                {
                    if (PrismRows[r].Length <= 14 || PrismRows[r][14] != ch) continue;
                    // Cells to world units: every weapon grid is DOUBLED before it is drawn
                    // (upscale2x), so a cell is FineUpscale texels. Dividing by the ppu alone put
                    // every glow at half its gem's height once FinePpu doubled.
                    along[(int)element] = (PrismGrip.y - r) * FineUpscale / FinePpu;
                    break;
                }
            }
            return along;
        }

        /// <summary>Stamp a weapon with the four element-gem variants and where each one sits. One
        /// place, so the flags and the art cannot drift apart - see <see cref="Heat"/> for the
        /// identical reasoning.</summary>
        static GearItem Attune(GearItem item, Sprite[] variants, float[] along)
        {
            item.HasElementGems = true;
            item.ElementBlades = variants;
            item.MenuElementBlades = BuildPrismMenuSet();
            item.ElementGemAlong = along;
            return item;
        }

        // ============================================================== Sniper
        //
        // A gunblade built on a COLT PYTHON (the user's references): the blade is the barrel, the
        // hilt is the revolver's frame. The barrel runs up the sword, so the gun's TOP is the
        // sword's LEFT side:
        //
        //   BARREL  a Python's proportion to its frame, not the whole blade: the VENTILATED RIB
        //           down the left (a lit strip over dark vent windows), the round polished barrel
        //           ending in a CROWN with its bore, the full-length UNDERLUG down the right, and
        //           the front sight's ramp and orange insert on the rib behind the muzzle.
        //   BLADES  past the muzzle the rib and the underlug carry on alone as TWIN BLADES, sharp
        //           on their outer edges, each to its own point; the gap between them is where the
        //           barrel ends - exactly its width, and wide enough to survive the outline.
        //   FRAME   the CYLINDER seen from the side, long flutes and chamfered ends, wider than the
        //           barrel - it takes the crossguard's place, there is no crossguard. The HAMMER's
        //           spur sticks back on the rib side, the TRIGGER GUARD loops out on the other with
        //           one curved trigger in it, so the hilt is ASYMMETRIC - the one in the set.
        //   GRIP    walnut, CURVED back toward the hammer side like a revolver's, a checkered panel
        //           inside a smooth border, a gold medallion, a rounded butt.
        //
        // STAINLESS, not blued: polished steel reads by its bright highlights against DARK
        // reflection bands, exactly as the references do - a flat mid-grey is lead.
        //
        // One field (SniperTexel) for the arena and the menu art. It used to be a forked blade on
        // a crossguard with an end-on cylinder and two triggers in a brass box; the fork's gap and
        // the brass box are gone with it.

        static Dictionary<char, Color> SniperPal()
        {
            var steel = new Palette.Ramp(new Color(0.64f, 0.66f, 0.71f), lift: 0.62f, shade: 0.48f);
            var gold  = new Palette.Ramp(new Color(0.86f, 0.68f, 0.30f), lift: 0.35f);
            var wood  = new Palette.Ramp(new Color(0.52f, 0.27f, 0.15f), lift: 0.30f, shade: 0.42f);
            var map = Palette.Of(steel, gold, wood);
            map['o'] = new Color(1.00f, 0.46f, 0.12f);                   // the front sight's insert
            return map;
        }

        /// <summary>Fill a span of a row with a band resampled across it. Shared with Crossblade
        /// and Lumen.</summary>
        static void SniperFill(char[] line, int from, int to, string ramp)
        {
            int wide = to - from + 1;
            for (int x = from; x <= to; x++)
            {
                int i = wide <= 1 ? 0 : (x - from) * (ramp.Length - 1) / (wide - 1);
                line[x] = ramp[i];
            }
        }

        // Rows, top to bottom: the barrel (tip included), the frame (front, cylinder, rear), the
        // grip, the butt.
        const int SniperGripRows = 17;          // curved, walnut to a rounded butt; +5 with every grip
        const int SniperFrameRows = 14;
        static int SniperBarrelRows => BladeRowsFor(SniperFrameRows + SniperGripRows);
        static int SniperFrameTop => SniperBarrelRows;
        static int SniperGripTop => SniperFrameTop + SniperFrameRows;

        /// <summary>The grip's second row, like every greatsword's pivot.</summary>
        static Vector2Int SniperGrip => new(13, GripCentre(SniperGripTop, SniperGripRows));
        static Vector2Int SniperDetailGrip => new(SniperGrip.x * EmberDetailScale, SniperGrip.y * EmberDetailScale);

        // Across the barrel, in cells: the rib, the barrel, the underlug.
        static float SniperLeft => SwordAxis - SwordBladeWidth / 2f;
        static float SniperRight => SwordAxis + SwordBladeWidth / 2f;
        const float SniperCylinderHalf = 7f;

        static string[] BuildSniperSword(float spin = 0f)
        {
            var rows = new string[SwordHeightRows];
            for (int y = 0; y < rows.Length; y++)
            {
                var line = new char[SwordCanvas];
                for (int x = 0; x < SwordCanvas; x++) line[x] = SniperTexel(x, y, fine: false, spin);
                rows[y] = new string(line);
            }
            return rows;
        }

        /// <summary>Menu art at TRUE <see cref="MenuPpu"/> - never pass it upscale2x.</summary>
        static string[] BuildSniperSwordDetail()
        {
            const int k = EmberDetailScale;
            var rows = new string[SwordHeightRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[SwordCanvas * k];
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++)
                    line[X] = SniperTexel((X + 0.5f) / k - 0.5f, y, fine: true);
                rows[Y] = new string(line);
            }
            return rows;
        }

        static readonly string[] SniperRows = BuildSniperSword();

        const int SniperSpinFrameCount = 6;

        /// <summary>
        /// The cylinder turning ONE chamber (60 degrees) - the flutes are identical, so the last
        /// frame hands straight back to the resting sprite with no jump. Played by CylinderSpin on
        /// the attack after a finisher. Through StageSprite, doubled exactly as the base layer is.
        /// </summary>
        static Sprite[] SniperSpinFrames()
        {
            var pal = SniperPal();
            var frames = new Sprite[SniperSpinFrameCount];
            for (int i = 0; i < frames.Length; i++)
            {
                float spin = (i + 1) / (float)(SniperSpinFrameCount + 1) * Mathf.PI / 3f;
                frames[i] = StageSprite("gear.weapon.sniper.spin" + i, BuildSniperSword(spin), pal, SniperGrip);
            }
            return frames;
        }
        static readonly string[] SniperSwordDetail = BuildSniperSwordDetail();

        static char SniperTexel(float x, float y, bool fine, float spin = 0f)
        {
            // The hammer and the trigger guard first: they overhang the frame's rear AND the top
            // of the grip, so they cannot live inside either one's rows. Cut off at the grip's
            // first row, the guard came out a hook rather than a loop.
            char gun = SniperHammerAndGuard(x, y, fine);
            if (gun != '.') return gun;

            if (y < SniperFrameTop - 0.5f) return SniperBarrel(x, y, fine);
            if (y < SniperGripTop - 0.5f) return SniperFrame(x, y, fine, spin);
            return SniperWalnut(x, y, fine);
        }

        // ---- the barrel, and the twin blades past its muzzle

        // Across the blade, in cells: the rib (left), the barrel (middle), the underlug (right).
        // Even thirds-ish so the BARREL is centred - its end is the gap between the twin blades,
        // so the gap is exactly the barrel's width.
        static float SniperRibEdge => SniperLeft + 3f;
        static float SniperLugEdge => SniperRight - 3f;

        /// <summary>The barrel's length: a Python's proportion to its frame, about 1.3 of it.
        /// Past the muzzle the rib and underlug carry on alone as the twin blades.</summary>
        const float SniperBarrelLength = 18f;
        static float SniperMuzzleY => SniperFrameTop - 0.5f - SniperBarrelLength;
        const float SniperBladeTipRows = 6f;

        static char SniperBarrel(float x, float y, bool fine)
        {
            float muzzle = SniperMuzzleY;

            // The front sight: a ramp standing off the rib just behind the muzzle, orange insert.
            float sTop = muzzle - 0.2f, sBot = muzzle + 3.2f;
            if (y > sTop && y < sBot && x < SniperLeft && x > SniperLeft - 1.4f)
            {
                float ramp = SniperLeft - 1.4f * (y - sTop) / (sBot - sTop);    // ramps out going back
                if (x < ramp) return '.';
                if (Mathf.Abs(y - (sTop + 1.6f)) < (fine ? 0.35f : 0.5f) && x < SniperLeft - 0.35f) return 'o';
                return x < SniperLeft - 0.9f ? 'l' : 'b';
            }

            if (x < SniperLeft || x > SniperRight) return '.';

            // ---- past the muzzle: the TWIN BLADES
            if (y < muzzle)
            {
                bool left = x < SniperRibEdge;
                if (!left && x < SniperLugEdge) return '.';                     // the open gap
                // Each tapers to its own point at the top, the point on its OUTER edge: the
                // inner edge slants out to meet it.
                float inner = left ? SniperRibEdge - x : x - SniperLugEdge;      // from the gap
                float width = 3f;
                if (y < SniperBladeTipRows)
                {
                    float keep = width * Mathf.Clamp01((y + 0.5f) / (SniperBladeTipRows + 0.5f));
                    if (width - inner > keep) return '.';
                }
                float fromOuter = left ? x - SniperLeft : SniperRight - x;
                if (fromOuter < (fine ? 0.35f : 1f)) return 'h';                 // the cutting edge
                if (fine && fromOuter < 0.7f) return 'l';
                if (inner < (fine ? 0.3f : 0.6f)) return 'd';                    // the inner bevel
                return left ? 'l' : 'b';
            }

            // ---- the barrel section
            // The CROWN: the barrel's end, a bright chamfer with the bore dark in its middle.
            if (x > SniperRibEdge && x < SniperLugEdge && y < muzzle + (fine ? 0.9f : 1f))
            {
                if (Mathf.Abs(x - SwordAxis) < 0.9f && y < muzzle + (fine ? 0.45f : 1f)) return 's';
                return 'h';
            }

            // The VENTILATED RIB: its lit top strip, then vent windows with posts between them.
            if (x < SniperRibEdge)
            {
                float strip = fine ? 0.6f : 1f;
                if (x < SniperLeft + strip) return 'h';
                float v = (y - muzzle - 1f) / 4f;
                float fv = v - Mathf.Floor(v);
                if (y > muzzle + 1.5f && fv > 0.15f && fv < 0.72f && x > SniperLeft + 1.1f && x < SniperRibEdge - 0.5f)
                    return fine && fv < 0.22f ? 'd' : 's';
                return x > SniperRibEdge - (fine ? 0.25f : 0f) ? 's' : 'l';
            }

            // The seam between barrel and underlug.
            if (Mathf.Abs(x - SniperLugEdge) < (fine ? 0.14f : 0.5f)) return 's';

            // The BARREL: a polished round - highlight, body, a DARK reflection band, body.
            if (x < SniperLugEdge)
            {
                float u = (x - SniperRibEdge) / (SniperLugEdge - SniperRibEdge);
                // A roll mark in the menu art: a line of small stamped characters along the barrel.
                if (fine && Mathf.Abs(u - 0.5f) < 0.08f && y > muzzle + 4f && y < muzzle + 13f
                    && Mathf.Repeat(y * 2.3f, 1f) < 0.55f && Mathf.Repeat(y * 0.42f, 1f) < 0.8f)
                    return 'd';
                if (u < 0.18f) return 'l';
                if (fine && u < 0.28f) return 'h';
                if (u < 0.55f) return 'b';
                if (u < 0.80f) return 'd';
                return 'b';
            }

            // The UNDERLUG: a flat polished face; its outer edge runs on up as the right blade's.
            float e = SniperRight - x;
            if (e < (fine ? 0.35f : 1f)) return 'h';
            if (fine && e < 0.7f) return 'l';
            float w = (x - SniperLugEdge) / (SniperRight - SniperLugEdge);
            return w < 0.35f ? 'l' : 'b';
        }

        // ---- the grip: curved back like a revolver's, walnut to a rounded butt

        /// <summary>The grip's centre line: straight where the hand closes, then bending back
        /// toward the hammer side as a revolver's does.</summary>
        static float SniperGripCentre(float y)
        {
            float t = Mathf.Clamp01((y - (SniperGripTop - 0.5f)) / SniperGripRows);
            return SwordAxis - 2.8f * Mathf.Pow(t, 1.7f);
        }

        /// <summary>The grip's half-width: swelling toward the butt, then rounded off.</summary>
        static float SniperGripHalf(float y)
        {
            float bottom = SniperGripTop - 0.5f + SniperGripRows;
            float t = Mathf.Clamp01((y - (SniperGripTop - 0.5f)) / SniperGripRows);
            float half = Mathf.Lerp(SwordGripWidth / 2f + 0.4f, SwordGripWidth / 2f + 1.1f, t);   // thinned with every grip
            float fromEnd = bottom - y;
            if (fromEnd < 2.2f) half *= Mathf.Sqrt(Mathf.Clamp01(fromEnd / 2.2f));
            return half;
        }

        static bool SniperGripCovers(float x, float y)
            => y > SniperGripTop - 0.5f && y < SniperGripTop - 0.5f + SniperGripRows
               && Mathf.Abs(x - SniperGripCentre(y)) < SniperGripHalf(y);

        static char SniperWalnut(float x, float y, bool fine)
        {
            if (!SniperGripCovers(x, y)) return '.';
            float c = SniperGripCentre(y), half = SniperGripHalf(y);
            float dx = x - c;

            // The medallion, near the top of the panel.
            float my = SniperGripTop + 1.6f;
            float md = Mathf.Sqrt(dx * dx + (y - my) * (y - my));
            if (md < (fine ? 0.95f : 1.1f)) return fine && md < 0.45f && dx < 0f ? 'L' : 'B';

            // A smooth border round a CHECKERED panel, the checkering running along the curve.
            float border = half - Mathf.Abs(dx);
            float bottom = SniperGripTop - 0.5f + SniperGripRows;
            bool rim = border < (fine ? 0.7f : 1f) || y > bottom - 1.6f || y < SniperGripTop + 0.1f;
            float lit = -dx / half;
            if (rim) return lit > 0.4f ? '5' : lit < -0.4f ? '3' : '4';
            if (fine)
            {
                float a = (y + dx) / 0.7f, b = (y - dx) / 0.7f;
                if (Mathf.Repeat(a, 1f) < 0.22f || Mathf.Repeat(b, 1f) < 0.22f) return lit > 0.3f ? '3' : '2';
            }
            else if (((Mathf.RoundToInt(y) + Mathf.RoundToInt(dx + 10f)) & 1) == 0) return lit > 0.3f ? '4' : '3';
            return lit > 0.3f ? '5' : '4';
        }

        static float SniperCylBot => SniperFrameTop - 0.5f + 10f;
        static float SniperRear => SniperGripTop - 0.5f;

        /// <summary>
        /// The HAMMER - a spur standing back off the rib side, checkered in the menu - and the
        /// TRIGGER GUARD looping out on the other side round one curved trigger. Only where they
        /// stand clear of the frame and grip; inside those, the frame or grip is drawn.
        /// </summary>
        static char SniperHammerAndGuard(float x, float y, bool fine)
        {
            float dx = x - SwordAxis, cylBot = SniperCylBot, rear = SniperRear;
            if (y < cylBot - 0.5f || y > rear + 4.5f) return '.';

            // What the frame or grip already covers at this row.
            if (y < rear && Mathf.Abs(dx) < Mathf.Lerp(5f, 3.8f, Mathf.Clamp01((y - cylBot) / (rear - cylBot))))
                return '.';
            if (SniperGripCovers(x, y)) return '.';

            if (dx < 0f)
            {
                // The hammer: from the frame's rear, back and out past the rib, a rounded spur.
                // A broad spur, not a rod: the neck out of the frame, then a round checkered
                // PAD at its end - the thumb's purchase, and what makes a Python's hammer read.
                float root = 1.3f, tip = 0.95f;
                var a = new Vector2(SwordAxis - 3.6f, cylBot + 0.9f);
                var b = new Vector2(SwordAxis - 6.6f, rear + 0.9f);
                Vector2 v = b - a, p = new Vector2(x, y) - a;
                float t = Mathf.Clamp01(Vector2.Dot(p, v) / v.sqrMagnitude);
                float d = (p - v * t).magnitude - Mathf.Lerp(root, tip, t);
                var pad = new Vector2(x, y) - (b + new Vector2(-0.3f, 0.2f));
                float dp = pad.magnitude - 1.45f;
                bool inPad = dp < 0f;
                if (d >= 0f && !inPad) return '.';
                float edge = inPad ? -dp : -d;
                if (inPad && fine && Mathf.Repeat(x * 2.4f + y * 2.4f, 1f) < 0.3f && edge > 0.25f) return 's';
                if (inPad && !fine && edge > 0.6f) return 'd';
                if (edge < (fine ? 0.3f : 0.6f)) return (inPad ? pad.y : (p - v * t).y) < 0f ? 'l' : 'd';
                return 'b';
            }

            // The trigger guard: a loop, its hole wide enough to survive the arena outline.
            float gx = SwordAxis + 5.4f, gy = rear + 0.6f, r = 2.8f;
            float gd = Mathf.Sqrt((x - gx) * (x - gx) + (y - gy) * (y - gy));
            float band = fine ? 0.34f : 0.5f;
            if (Mathf.Abs(gd - r) < band) return (y - gy) < 0f || (x - gx) < 0f ? 'l' : 'd';
            // The trigger: one curved blade hanging in the loop.
            if (gd < r - band)
            {
                float tc = y - (cylBot + 0.6f);
                float tx = SwordAxis + 4.4f + tc * 0.12f + tc * tc * 0.035f;
                if (tc > 0f && y < gy + 1.8f && Mathf.Abs(x - tx) < (fine ? 0.3f : 0.55f))
                    return fine && x < tx - 0.1f ? 'l' : 'd';
            }
            return '.';
        }

        // ---- the frame: cylinder, hammer, trigger guard

        static char SniperFrame(float x, float y, bool fine, float spin)
        {
            float dx = x - SwordAxis, top = SniperFrameTop - 0.5f;
            float cylTop = top + 2f, cylBot = top + 10f, rear = SniperGripTop - 0.5f;

            float frameHalf = Mathf.Lerp(5f, 3.8f, Mathf.Clamp01((y - cylBot) / (rear - cylBot)));

            // The frame's front, in which the barrel seats - and a dark SEAM where the cylinder
            // begins: the gap it turns in. Without it the cylinder ran into the frame as one
            // lump of steel; with it the cylinder is its own part, the one that moves.
            if (y < cylTop)
            {
                if (Mathf.Abs(dx) > 5.2f) return '.';
                if (y > cylTop - (fine ? 0.35f : 1f)) return 's';
                return SniperSteelAcross(dx / 5.2f, fine);
            }

            // The CYLINDER: side on, chamfered ends, long flutes.
            if (y < cylBot)
            {
                float ch = 1.2f;                                                  // the chamfer
                float edgeIn = Mathf.Min(y - cylTop, cylBot - y);
                float half = SniperCylinderHalf - Mathf.Max(0f, ch - edgeIn);
                if (Mathf.Abs(dx) > half) return '.';
                if (edgeIn < (fine ? 0.35f : 0.8f)) return dx < 0f ? 'l' : 'd';   // the rims

                // SIX flutes round the cylinder, 60 degrees apart, each a long lens; seen from the
                // side a flute slides across the face and narrows as it turns away, and the ones
                // round the back are hidden. `spin` turns the cylinder - see SniperSpinFrames.
                float t = (y - cylTop - 0.8f) / (cylBot - cylTop - 1.6f);
                if (t > 0f && t < 1f)
                {
                    float lens = 0.75f * Mathf.Sin(Mathf.PI * t);
                    for (int k = 0; k < 6; k++)
                    {
                        float th = spin + k * Mathf.PI / 3f;
                        float c = Mathf.Cos(th);
                        if (c < 0.15f) continue;                                   // round the back
                        float fx = half * 0.8f * Mathf.Sin(th);
                        float w = lens * (0.35f + 0.65f * c);
                        float fd = dx - fx;
                        if (Mathf.Abs(fd) < w)
                            return fine && fd < -w + 0.25f ? 'l' : 's';           // lit far wall
                    }
                }
                return SniperSteelAcross(dx / half, fine);
            }

            // The frame behind the cylinder, narrowing into the grip, the seam again first.
            if (Mathf.Abs(dx) > frameHalf) return '.';
            if (y < cylBot + (fine ? 0.35f : 1f)) return 's';
            return SniperSteelAcross(dx / frameHalf, fine);
        }

        /// <summary>Stainless across a round or a flat, lit from the left: highlight, body, the
        /// dark reflection band, body.</summary>
        static char SniperSteelAcross(float a, bool fine)
        {
            float u = (a + 1f) * 0.5f;
            if (fine && u < 0.07f) return 'h';
            if (u < 0.22f) return 'l';
            if (fine && u < 0.30f) return 'h';
            if (u < 0.55f) return 'b';
            if (u < 0.78f) return 'd';
            return 'b';
        }

        // ============================================================== Phantom

        // The whole Phantom is ONE continuous field (PhantomTexel), sampled once per cell for the
        // arena, four times per cell for the menu art, and at eight phases for the gas flipbook -
        // so the three cannot disagree at the silhouette.
        //
        //   BLADE   a DARK SOLID BODY in a sword's shape, the face burned into it, wrapped in a
        //           billowing cloud of translucent gas. The cloud is a UNION OF ROUND PUFFS, not a
        //           wobbled edge: two sine waves read as a rippling ribbon; lumps read as gas
        //           boiling off something. Brighter at the cloud's rim, darker toward the body,
        //           the way lit smoke is. The body is what keeps a gas blade a BLADE - with no core
        //           the face floated in fog and the outline said nothing about the weapon.
        //   HILT    metallic purple: guard arms that sweep out and HOOK back toward the point, a
        //           diamond gem at the crossing in the face's own glow, a pointed langet with two
        //           lobes running up the body, a wrapped grip, and a crowned pommel ending in a
        //           spire.
        //
        // The pommel's crown takes five rows and the guard five, so the blade (tip included)
        // gets what is left (BladeRowsFor) and the pivot sits two rows higher than the plain
        // greatsword's, as the Rift Blade's and Silver Blade's do.
        //
        // The gas passes the blade's width budget - about sixteen cells at its widest puffs -
        // and that is the documented exception: a cloud held to the torso is not a cloud.

        const int PhantomGuardRows = 5;
        const int PhantomPommelRows = 5;
        static int PhantomBladeRows => BladeRowsFor(PhantomGuardRows + 1 + SwordGripRows + PhantomPommelRows);
        static int PhantomCollarRow => PhantomBladeRows + PhantomGuardRows;
        static int PhantomGripTop => PhantomCollarRow + 1;
        static int PhantomPommelTop => PhantomGripTop + SwordGripRows;
        static float PhantomGuardY => PhantomBladeRows + 2.5f;

        /// <summary>The grip's second row, like every greatsword's pivot.</summary>
        static Vector2Int PhantomGrip => new(13, GripCentre(PhantomGripTop, SwordGripRows));
        static Vector2Int PhantomDetailGrip => new(PhantomGrip.x * EmberDetailScale, PhantomGrip.y * EmberDetailScale);

        static Dictionary<char, Color> PhantomPal()
        {
            var haze = new Palette.Ramp(new Color(0.46f, 0.28f, 0.64f), lift: 0.40f, shade: 0.30f);
            var glow = new Palette.Ramp(new Color(0.97f, 0.92f, 0.58f), lift: 0.32f);
            // METALLIC purple: a wide ramp with its highlights pushed toward white-lavender. Metal
            // reads by contrast - a narrow ramp is painted purple, not purple metal.
            var metal = new Palette.Ramp(new Color(0.48f, 0.28f, 0.66f), lift: 0.42f, shade: 0.42f)
                .WithHighlightsToward(new Color(0.95f, 0.88f, 1.00f), 0.50f);
            var map = Palette.Of(haze, glow, metal);

            // The body inside the gas is dark purple GLASS. Glass reads by its EDGES, not its
            // fill: nearly clear through the middle, denser at both rims (light crosses more of
            // it there), a bright highlight down the lit side and a few diagonal glints across
            // it. Only fading the body to a flat alpha read as a ghost of a blade, not a glass
            // one. The face burned into it stays solid.
            map['q'] = new Color(0.36f, 0.24f, 0.48f, 0.95f);    // lit rim
            map['o'] = new Color(0.19f, 0.11f, 0.25f, 0.70f);    // the clear middle
            map['p'] = new Color(0.10f, 0.05f, 0.14f, 0.90f);    // shaded rim
            map['r'] = new Color(0.90f, 0.84f, 1.00f, 0.85f);    // the highlight
            map['n'] = new Color(0.62f, 0.50f, 0.82f, 0.45f);    // a glint, fainter

            // The grip's wrap: plum leather, a step darker than the metal so the hilt has two
            // materials rather than one purple lump.
            map['x'] = new Color(0.44f, 0.25f, 0.48f);
            map['w'] = new Color(0.32f, 0.17f, 0.37f);
            map['v'] = new Color(0.21f, 0.10f, 0.26f);
            map['u'] = new Color(0.12f, 0.05f, 0.16f);

            // Gas writes alpha, weighted to the lit tones (the ShadowAlphas idiom): the rim of a
            // cloud is where it is thickest to the eye.
            foreach (var (c, a) in PhantomHazeAlphas)
            {
                var col = map[c];
                col.a = a;
                map[c] = col;
            }
            return map;
        }

        static readonly (char C, float A)[] PhantomHazeAlphas =
        {
            ('h', 0.90f), ('l', 0.72f), ('b', 0.55f), ('d', 0.42f), ('s', 0.30f),
        };

        static string[] BuildPhantomSword(float phase = 0f)
        {
            var rows = new string[SwordHeightRows];
            for (int y = 0; y < rows.Length; y++)
            {
                var line = new char[SwordCanvas];
                for (int x = 0; x < SwordCanvas; x++) line[x] = PhantomTexel(x, y, phase, fine: false);
                rows[y] = new string(line);
            }
            return rows;
        }

        /// <summary>Menu art at TRUE <see cref="MenuPpu"/> - never pass it upscale2x. Still; the
        /// gas only moves in the hand.</summary>
        static string[] BuildPhantomSwordDetail()
        {
            const int k = EmberDetailScale;
            var rows = new string[SwordHeightRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[SwordCanvas * k];
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++)
                    line[X] = PhantomTexel((X + 0.5f) / k - 0.5f, y, 0f, fine: true);
                rows[Y] = new string(line);
            }
            return rows;
        }

        static readonly string[] PhantomRows = BuildPhantomSword();
        static readonly string[] PhantomSwordDetail = BuildPhantomSwordDetail();

        static char PhantomTexel(float x, float y, float phase, bool fine)
        {
            char hilt = PhantomHiltTexel(x, y, fine);
            if (hilt != '.') return hilt;
            if (y > PhantomGuardY) return '.';

            float dx = Mathf.Abs(x - SwordAxis);
            float half = PhantomBodyHalf(y);
            if (dx < half)
            {
                char face = PhantomFace(x, y);
                if (face != '.') return face;
                return PhantomGlass(x, y, half, fine);
            }

            float sd = PhantomGasSd(x, y, phase, dx, half);
            if (sd >= 0f) return '.';
            float depth = -sd;
            if (fine)
                return depth < 0.30f ? 'h' : depth < 0.75f ? 'l' : depth < 1.40f ? 'b' : 'd';
            return depth < 0.55f ? 'l' : depth < 1.30f ? 'b' : 'd';
        }

        /// <summary>The body's half-width: a sword's own point at the top, eight cells wide below
        /// it - inside the blade budget, wide enough to carry the face. A slimmer body under a
        /// bigger cloud was tried and the gas swamped the blade; the body is the object here and
        /// the gas the rim round it.</summary>
        const float PhantomBodyMax = 4f;

        static float PhantomBodyHalf(float y)
        {
            if (y < 1.5f) return 0f;
            float t = Mathf.Clamp01((y - 1.5f) / 7f);
            return PhantomBodyMax * Mathf.Pow(t, 0.75f);
        }

        /// <summary>
        /// The glass body: dense at both rims, clear through the middle, a highlight running down
        /// the lit side, and diagonal glints crossing it at intervals - parallel, as reflections
        /// on one flat face are.
        /// </summary>
        static char PhantomGlass(float x, float y, float half, bool fine)
        {
            float u = (x - (SwordAxis - half)) / (2f * half);
            if (u < (fine ? 0.08f : 0.13f)) return 'q';
            if (u > (fine ? 0.90f : 0.87f)) return 'p';

            // The highlight: a line a quarter of the way across, broken where a glint crosses it.
            // The glints are menu-only: at cell size a diagonal line is a staircase of dots.
            float g = (y + (x - SwordAxis) * 1.3f) / 14f;
            float gf = g - Mathf.Floor(g);
            bool glint = fine && gf < 0.05f;
            if (u > 0.19f && u < (fine ? 0.27f : 0.31f)) return glint ? 'o' : fine ? 'r' : 'n';
            if (glint) return gf < 0.022f ? 'r' : 'n';
            return 'o';
        }

        /// <summary>One puff of gas: where it sits at rest, how big, and its own phase offset.</summary>
        struct PhantomPuff { public float X, Y, R, Phi; }

        static PhantomPuff[] _phantomPuffs;

        /// <summary>
        /// The puffs, alternating sides down the body at uneven offsets and sizes - hashed, not
        /// random, so the art is the same every build. Lazy: a static initialiser elsewhere in
        /// this partial class may reach it first.
        /// </summary>
        static PhantomPuff[] PhantomPuffs
        {
            get
            {
                if (_phantomPuffs != null) return _phantomPuffs;
                var list = new List<PhantomPuff>();
                int n = 0;
                float end = PhantomBladeRows - 3f;
                for (float y = 1.5f; y < end; y += 2.3f, n++)
                {
                    float side = n % 2 == 0 ? -1f : 1f;
                    float off = 2.9f + 1.7f * EmberHash(n, 7);
                    float r = 1.7f + 1.2f * EmberHash(n, 13);
                    if (y < 6f) off *= 0.35f;                     // the cloud closes over the point
                    // Thinning toward the guard - the gas pours off the body, not off the hilt.
                    r *= Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((y - (end - 12f)) / 12f));
                    list.Add(new PhantomPuff { X = side * off, Y = y, R = r, Phi = EmberHash(n, 29) * Mathf.PI * 2f });
                }
                return _phantomPuffs = list.ToArray();
            }
        }

        /// <summary>
        /// Signed distance to the gas cloud's edge: every puff, plus a skin hugging the body so the
        /// cloud is continuous round it. <paramref name="phase"/> (radians) circles each puff about
        /// its rest point and breathes its size, every puff on its own offset - a whole number of
        /// cycles per loop, so the flipbook has no seam.
        /// </summary>
        static float PhantomGasSd(float x, float y, float phase, float dx, float half)
        {
            float sd = y < PhantomBladeRows - 1f && y > 0f ? dx - (half + 1.1f) : 99f;
            foreach (var p in PhantomPuffs)
            {
                float px = SwordAxis + p.X + 0.45f * Mathf.Cos(phase + p.Phi);
                float py = p.Y + 0.55f * Mathf.Sin(phase + p.Phi * 1.7f);
                float r = p.R * (1f + 0.10f * Mathf.Sin(phase * 2f + p.Phi));
                float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py)) - r;
                if (d < sd) sd = d;
            }
            return sd;
        }

        /// <summary>Where the face sits, as a fraction of the blade - its vertical middle.</summary>
        const float PhantomFaceRow = 0.50f;

        /// <summary>
        /// The face, burned into the body in the glow: two eyes, triangles pointing INWARD (angry,
        /// narrowed), and a wide grin with its corners turned up and dark gaps between the teeth.
        /// Continuous shapes, so the menu art gets clean edges and the arena the same face in cells.
        /// </summary>
        static char PhantomFace(float x, float y)
        {
            float fy = Mathf.Round(PhantomBladeRows * PhantomFaceRow);
            float mx = x < SwordAxis ? x : 2f * SwordAxis - x;         // mirror to the left eye

            if (InTriangle(mx, y, 9.6f, fy + 1.6f, 12.6f, fy + 1.6f, 12.2f, fy - 0.7f)) return 'H';

            float d = Mathf.Abs(x - SwordAxis);
            if (d < 3.4f)
            {
                float top = fy + 3.4f - 0.11f * d * d;
                if (y > top && y < top + 1.3f)
                {
                    float g = (x - SwordAxis) / 2f + 0.25f;
                    return g - Mathf.Floor(g) < 0.36f ? 'p' : 'H';
                }
            }
            return '.';
        }

        static bool InTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
            float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
            float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // ---- the hilt

        const float PhantomArmRoot = 2.5f;
        const float PhantomArmReach = 9.5f;
        const float PhantomArmRise = 0.06f;       // how hard the arms curve toward the point

        static float PhantomArmY(float dx)
            => PhantomGuardY + 0.3f - PhantomArmRise * (dx - PhantomArmRoot) * (dx - PhantomArmRoot);

        /// <summary>Signed distance to the hilt's metal (everything but the wrap and the gem).</summary>
        static float PhantomMetalSd(float x, float y)
        {
            float dx = Mathf.Abs(x - SwordAxis);

            // The arms: a curved bar tapering outward, measured perpendicular to its own slope.
            float arm = 99f;
            if (dx > PhantomArmRoot - 0.5f)
            {
                float t = Mathf.Clamp01((dx - PhantomArmRoot) / (PhantomArmReach - PhantomArmRoot));
                float slope = 2f * PhantomArmRise * (dx - PhantomArmRoot);
                float th = Mathf.Lerp(1.6f, 0.75f, t);
                arm = Mathf.Max((Mathf.Abs(y - PhantomArmY(dx)) - th) / Mathf.Sqrt(1f + slope * slope),
                                dx - PhantomArmReach);
                // The hook: each arm's end turns back up and in, toward the blade - a claw.
                float hx = PhantomArmReach - 0.9f, hy = PhantomArmY(PhantomArmReach) - 1.3f;
                arm = Mathf.Min(arm, Mathf.Sqrt((dx - hx) * (dx - hx) + (y - hy) * (y - hy)) - 0.85f);
            }

            float boss = PhantomEllipseSd(x - SwordAxis, y - PhantomGuardY, 4.0f, 2.3f);

            // The langet: a point running up the body, with a lobe either side at its base.
            float lt = PhantomBladeRows - 6.5f, lb = PhantomBladeRows + 0.5f;
            float langet = 99f;
            if (y > lt && y < lb)
                langet = dx - 2.2f * (y - lt) / (lb - lt);
            float lobe = Mathf.Sqrt((dx - 2.7f) * (dx - 2.7f) + (y - (PhantomBladeRows - 0.6f)) * (y - (PhantomBladeRows - 0.6f))) - 1.25f;

            float collar = PhantomEllipseSd(x - SwordAxis, y - PhantomCollarRow, 3.7f, 0.65f);

            // The pommel: a ring, a crowned bulb, and a spire.
            float ring = PhantomEllipseSd(x - SwordAxis, y - PhantomPommelTop, 3.3f, 0.6f);
            float bulb = PhantomEllipseSd(x - SwordAxis, y - (PhantomPommelTop + 1.8f), 3.6f, 1.7f);
            float sTop = PhantomPommelTop + 2.6f, sBot = PhantomPommelTop + 4.6f;
            float spire = y > sTop - 0.5f ? Mathf.Max(dx - 2.2f * (sBot - y) / (sBot - sTop), y - sBot) : 99f;

            float sd = Mathf.Min(Mathf.Min(arm, boss), Mathf.Min(langet, lobe));
            return Mathf.Min(sd, Mathf.Min(collar, Mathf.Min(ring, Mathf.Min(bulb, spire))));
        }

        static float PhantomEllipseSd(float px, float py, float a, float b)
        {
            float k = Mathf.Sqrt(px * px / (a * a) + py * py / (b * b));
            return (k - 1f) * Mathf.Min(a, b);
        }

        static char PhantomHiltTexel(float x, float y, bool fine)
        {
            float dx = Mathf.Abs(x - SwordAxis);

            // The gem at the crossing, in the face's own glow, set in dark metal.
            float gem = dx + Mathf.Abs(y - PhantomGuardY) * 1.1f;
            if (gem < 1.9f)
            {
                if (fine) return gem < 0.7f ? 'H' : gem < 1.35f ? 'L' : 'B';
                return gem < 1.0f ? 'H' : 'L';
            }
            if (gem < 2.5f) return fine ? '1' : '2';

            float sd = PhantomMetalSd(x, y);
            if (sd < 0f)
            {
                // The crown's petals: notches cut into the bulb's grip-side edge.
                if (fine && y < PhantomPommelTop + 1.3f && y > PhantomPommelTop + 0.6f
                    && Mathf.Repeat(dx + 0.6f, 1.6f) < 0.45f)
                    return '2';
                return PhantomMetal(x, y, sd, fine);
            }

            if (y > PhantomCollarRow + 0.5f && y < PhantomPommelTop - 0.5f && dx < SwordGripWidth / 2f)
            {
                // A spiral wrap: diagonal ridges, lit on the left of the grip.
                float g = (y + (x - SwordAxis) * 0.7f) / 2f;
                float fr = g - Mathf.Floor(g);
                if (fr < (fine ? 0.22f : 0.34f)) return 'u';
                float a = (x - SwordAxis) / (SwordGripWidth / 2f);
                if (a < -0.5f) return 'x';
                if (a > 0.45f) return 'v';
                return fine && fr < 0.45f ? 'x' : 'w';
            }
            return '.';
        }

        /// <summary>Metallic shading from the surface normal: a hard lit crest on the upper left,
        /// deep shade on the lower right, and the plain metal between.</summary>
        static char PhantomMetal(float x, float y, float sd, bool fine)
        {
            const float e = 0.3f;
            float nx = PhantomMetalSd(x + e, y) - PhantomMetalSd(x - e, y);
            float ny = PhantomMetalSd(x, y + e) - PhantomMetalSd(x, y - e);
            float nl = Mathf.Sqrt(nx * nx + ny * ny);
            if (nl > 1e-4f) { nx /= nl; ny /= nl; }
            float lit = nx * -0.55f + ny * -0.83f;

            if (-sd < (fine ? 0.45f : 0.75f))
            {
                if (lit > 0.55f) return '6';
                if (lit > 0.15f) return '5';
                if (lit < -0.45f) return '2';
                if (lit < -0.05f) return '3';
            }
            // A second, softer highlight just inside the lit edge - what makes it read as a
            // polished surface rather than a flat plate with a rim.
            if (fine && lit > 0.35f && -sd < 0.9f) return '5';
            return '4';
        }

        /// <summary>How many frames the haze cycles through before looping. Phase runs a full
        /// 0..2*PI across the set, and every puff moves a whole number of cycles in it, so the
        /// loop has no seam.</summary>
        const int PhantomHazeFrameCount = 8;

        /// <summary>
        /// The gas flipbook for <see cref="PhantomHaze"/>. Through <see cref="StageSprite"/>, so
        /// every frame is doubled exactly as the base layer is - built straight from the grid, as
        /// they were, the frames rendered at HALF the resting sword's size the moment the haze
        /// started swapping them in. The face, the body and the hilt hold still; only the cloud
        /// moves.
        /// </summary>
        static Sprite[] BuildPhantomFrames()
        {
            var frames = new Sprite[PhantomHazeFrameCount];
            var pal = PhantomPal();
            for (int i = 0; i < PhantomHazeFrameCount; i++)
            {
                float phase = i / (float)PhantomHazeFrameCount * Mathf.PI * 2f;
                frames[i] = StageSprite("gear.weapon.phantom.haze" + i, BuildPhantomSword(phase), pal, PhantomGrip);
            }
            return frames;
        }

        // 9 x 13. Phantom's worn mark - the same caged shape Emberline's relic uses (a hanging
        // ring, straight corner posts, a base), holding a small version of the blade's own face
        // rather than a flame. The cage is opaque iron; only the face glows.
        //
        // The eyes are the SAME two-row triangle the sword uses (an apex over a three-wide
        // base), just narrower - there was no reason for the mark to carry a different face than
        // the weapon it is a miniature of. The grin stays a plain bar rather than the sword's
        // jagged teeth: at three rows total for the whole face, alternating texels would be a
        // checkerboard smear rather than teeth.
        static readonly string[] PhantomRelicRows =
        {
                "....l....",
                "...lll...",
                "..l...l..",
                ".lllllll.",
                "l.......l",
                "l.H...H.l",
                "lHHH.HHHl",
                "l.......l",
                "l.HHHHH.l",
                "l.......l",
                ".lllllll.",
                "..l...l..",
                "...lll...",
        };

        // ============================================================== Blood Blade

        /// <summary>
        /// Blood Blade's palette: STEEL for the body (lowercase), a dark GLASS tone for the
        /// fuller and gem while empty (uppercase), and BLOOD for both once they have drunk
        /// something (digits). All three ramps are baked into every stage sprite - what changes
        /// per stage is which rows pick the empty tone versus the blood tone, not the palette
        /// itself, which is what lets BuildBloodStages produce five sprites from one Dictionary.
        /// </summary>
        static Dictionary<char, Color> BloodPal()
        {
            var steel = new Palette.Ramp(new Color(0.34f, 0.35f, 0.39f), lift: 0.42f, shade: 0.28f);
            var empty = new Palette.Ramp(new Color(0.13f, 0.12f, 0.16f), lift: 0.30f, shade: 0.35f);
            var blood = new Palette.Ramp(new Color(0.62f, 0.04f, 0.07f), lift: 0.48f, shade: 0.30f);
            return Palette.Of(steel, empty, blood);
        }

        // ---- the bat wing ----------------------------------------------------------------
        //
        // The blade IS a bat's wing (reference: Kingdom Hearts' Way to the Dawn):
        //
        //   the SPINE - a dark gunmetal bone running straight up the lit (left) edge to the point,
        //       knuckled where each finger leaves it
        //   four FINGERS - ribs branching up and across from the spine to claw points on the far
        //       edge
        //   a SCALLOPED trailing edge - the membrane hangs in a concave arc between each pair of
        //       claws, which is what makes the silhouette a wing rather than a serrated blade
        //   the MEMBRANE between the bones - and the membrane is the METER. Empty it is gunmetal;
        //       as the vial fills, blood rises through it from the guard toward the point, with a
        //       bright surface line on the fill (the same rise the old glass fuller showed)
        //
        // The wing is WIDER than the blade budget (~12.5 cells at the claws) - a documented
        // exception like Emberline's horns: at ten cells the panels between the bones were too
        // narrow to read as membrane in the arena.
        //
        // A gunmetal RING guard holds the blood gem, which still brightens with the fill, with a
        // bat's thumb claw hooking up off each side; a dark wrapped grip; a small cone pommel
        // that carries a drop of red once the vial is three-quarters full.
        //
        // Drawn like Emberline, the Silver Blade and the Gilded Greatsword: one continuous field
        // (BatTexel), sampled at cell centres for the arena and four times per cell for the menu
        // art. Standard height; the pivot is the grip's second row like every other sword's.

        static int BatPommelTop => SwordHeightRows - 3;                  // 3 rows of pommel
        static int BatGripTop => BatPommelTop - SwordGripRows;
        static float BatRingY => BatGripTop - 4f;
        static float BatWingBase => BatRingY - 2f;                     // where the wing meets the ring
        const float BatWingTipY = 0.5f;

        const float BatRingOuter = 3.4f;
        const float BatRingInner = 1.9f;
        const float BatBoneHalf = 0.9f;
        const float BatRibHalf = 0.55f;
        const float BatGripHalf = SwordGripWidth / 2f;

        /// <summary>The spine's centre line: from the base of the wing, left of the axis, straight
        /// up to the point just right of it - the wing leans, as a wing held out does.</summary>
        static float BatSpineX(float y)
            => Mathf.Lerp(SwordAxis - 5.2f, SwordAxis + 0.2f,
                          Mathf.Clamp01((BatWingBase - y) / (BatWingBase - BatWingTipY)));

        /// <summary>Finger claw tips on the far edge, lowest first.</summary>
        static readonly float[] BatFingerY = { 46f, 34.5f, 23f, 12f };
        const float BatFingerX = SwordAxis + 6.8f;
        const float BatFingerRise = 9f;          // how far below its tip each finger leaves the spine

        /// <summary>The trailing edge: a concave scallop between each pair of claw tips.</summary>
        static float BatEdgeX(float y)
        {
            float tipX = BatSpineX(BatWingTipY);
            if (y < BatFingerY[3])
            {
                float s = Mathf.InverseLerp(BatWingTipY, BatFingerY[3], y);
                return Mathf.Lerp(tipX, BatFingerX, s) - 2.2f * Mathf.Sin(Mathf.PI * s);
            }
            for (int k = BatFingerY.Length - 1; k > 0; k--)
            {
                if (y < BatFingerY[k - 1])
                {
                    float s = Mathf.InverseLerp(BatFingerY[k], BatFingerY[k - 1], y);
                    return BatFingerX - 3.2f * Mathf.Sin(Mathf.PI * s);
                }
            }
            float b = Mathf.InverseLerp(BatFingerY[0], BatWingBase + 1f, y);
            return Mathf.Lerp(BatFingerX, SwordAxis + 3.5f, b) - 1.0f * Mathf.Sin(Mathf.PI * b);
        }

        static float BatSegmentDist(float x, float y, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy));
            float dx = x - (ax + vx * t), dy = y - (ay + vy * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Distance to the nearest finger rib (root on the spine to a claw just past
        /// the edge), and whether that point is on the claw itself.</summary>
        static float BatRibDist(float x, float y)
        {
            float d = 99f;
            foreach (float fy in BatFingerY)
            {
                float ry = Mathf.Min(fy + BatFingerRise, BatWingBase - 1f);
                d = Mathf.Min(d, BatSegmentDist(x, y, BatSpineX(ry), ry, BatFingerX + 0.6f, fy - 0.4f));
            }
            return d;
        }

        static Dictionary<char, Color> BatPal()
        {
            // Bones and fittings: dark gunmetal, lit toward a cool pale steel.
            var bone = new Palette.Ramp(new Color(0.19f, 0.20f, 0.24f), shade: 0.36f)
                .WithHighlightsToward(new Color(0.78f, 0.81f, 0.88f), 0.42f);
            // Empty membrane: a lighter gunmetal, so the bones still read across it.
            var membrane = new Palette.Ramp(new Color(0.37f, 0.38f, 0.43f), lift: 0.34f, shade: 0.34f);
            var blood = new Palette.Ramp(new Color(0.62f, 0.04f, 0.07f), lift: 0.48f, shade: 0.30f);
            return Palette.Of(bone, membrane, blood);
        }

        static string[] BuildBloodSword(int fillStage)
        {
            var rows = new string[SwordHeightRows];
            for (int y = 0; y < rows.Length; y++)
            {
                var line = new char[SwordCanvas];
                for (int x = 0; x < SwordCanvas; x++) line[x] = BatTexel(x, y, fillStage, fine: false);
                rows[y] = new string(line);
            }
            return rows;
        }

        /// <summary>Menu art at TRUE <see cref="MenuPpu"/>, four times the arena grid - never pass
        /// it upscale2x. Drawn EMPTY, the item as it drops.</summary>
        static string[] BuildBloodSwordDetail()
        {
            const int k = EmberDetailScale;
            var rows = new string[SwordHeightRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[SwordCanvas * k];
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++)
                    line[X] = BatTexel((X + 0.5f) / k - 0.5f, y, 0, fine: true);
                rows[Y] = new string(line);
            }
            return rows;
        }

        static char BatTexel(float x, float y, int fillStage, bool fine)
        {
            float dx = Mathf.Abs(x - SwordAxis);

            // ---- the ring guard, its gem and the thumb claws
            float rx = x - SwordAxis, ry = y - BatRingY;
            float r = Mathf.Sqrt(rx * rx + ry * ry);
            if (r < BatRingInner)
            {
                // The gem brightens with the fill, as the old ricasso gem did.
                char core = fillStage switch { 0 => 'S', 1 => '2', 2 => '3', 3 => '4', _ => '5' };
                if (fine && (rx + 0.5f) * (rx + 0.5f) + (ry + 0.5f) * (ry + 0.5f) < 0.12f)
                    return fillStage == 0 ? 'L' : '6';
                if (r > BatRingInner - (fine ? 0.35f : 0.6f)) return fillStage == 0 ? 'D' : '2';
                return core;
            }
            if (r < BatRingOuter) return BatMetal(rx, ry, r, fine);
            // thumb claws: short hooks off each side of the ring, up and out
            float cd = Mathf.Min(BatSegmentDist(x, y, SwordAxis - 3f, BatRingY - 1f, SwordAxis - 6.2f, BatRingY - 3.6f),
                                 BatSegmentDist(x, y, SwordAxis + 3f, BatRingY - 1f, SwordAxis + 6.2f, BatRingY - 3.6f));
            float along = Mathf.InverseLerp(BatRingY - 1f, BatRingY - 3.6f, y);
            if (cd < 0.75f * (1f - along) + 0.15f)
                return x < SwordAxis ? (fine && cd < 0.25f ? 'h' : 'l') : 'd';

            // ---- grip and pommel
            // The grip: SMOOTH - one rounded bar, lit on the left and turning away on the right.
            // It was banded, and the bands read as ridges against the membrane's flat skin.
            if (dx < BatGripHalf && y > BatGripTop - 0.5f && y < BatPommelTop - 0.5f)
            {
                float a = (x - SwordAxis) / BatGripHalf;
                if (!fine) return a < -0.5f ? 'l' : a > 0.5f ? 'd' : 'b';
                if (a < -0.72f) return 'l';
                if (a < -0.42f) return 'h';                 // the specular line
                if (a < -0.1f) return 'l';
                if (a < 0.45f) return 'b';
                if (a < 0.75f) return 'd';
                return 's';
            }
            if (y > BatPommelTop - 0.5f)
            {
                float t = (y - (BatPommelTop - 0.5f)) / 4f;
                float cone = 4.2f * (1f - t);
                if (dx > cone) return '.';
                if (fillStage >= 3 && dx < 0.8f && y < BatPommelTop + 0.6f) return fine ? '5' : '4';
                float u = (x - (SwordAxis - cone)) / Mathf.Max(0.5f, 2f * cone);
                return u < 0.25f ? 'l' : u > 0.7f ? 's' : 'd';
            }

            // ---- the wing
            if (y < BatWingTipY - 0.5f || y > BatWingBase + 1.5f) return '.';
            float spine = BatSpineX(y);
            float edge = BatEdgeX(y);
            float ribD = BatRibDist(x, y);
            // Thinner in the arena: at cell size a rib plus its crease was half of every panel,
            // and the wing read as a screw thread rather than stretched skin.
            bool onRib = ribD < (fine ? BatRibHalf : 0.45f);
            bool inWing = x >= spine - BatBoneHalf && x <= edge;
            if (!inWing && !onRib) return '.';

            // the spine: lit on its outer edge, knuckled where each finger leaves it
            float sd = x - spine;
            if (Mathf.Abs(sd) < BatBoneHalf)
            {
                bool knuckle = false;
                foreach (float fy in BatFingerY)
                    if (Mathf.Abs(y - Mathf.Min(fy + BatFingerRise, BatWingBase - 1f)) < 0.8f) knuckle = true;
                if (sd < -0.35f) return fine && sd < -0.65f ? 'h' : 'l';
                if (knuckle) return fine ? 'l' : 'b';
                return fine && sd > 0.4f ? 's' : 'b';
            }
            if (onRib) return fine && ribD < 0.2f ? 'b' : 'd';

            // the membrane: the meter. Blood rises from the guard toward the point.
            float fill = fillStage / (float)Core.Tuning.Blood.MaxFill;
            float surface = BatWingBase + 1.5f - fill * (BatWingBase + 1.5f - BatWingTipY + 0.5f);
            bool blood = fillStage > 0 && y >= surface;
            if (blood && fillStage < Core.Tuning.Blood.MaxFill && y < surface + (fine ? 0.3f : 1f))
                return '6';                                              // the surface line

            // Shaded as a stretched skin: dark in the crease under each bone, lit mid-panel.
            float crease = Mathf.Min(ribD, sd);
            float fromEdge = edge - x;
            string tones = blood ? "5432" : "LBDS";
            if (fine && crease < 0.9f) return tones[2];
            if (fromEdge < (fine ? 0.35f : 0.6f)) return tones[3];
            if (fine && crease > 2.2f && x < spine + 2.5f) return tones[0];
            return tones[1];
        }

        /// <summary>The ring guard: gunmetal, lit from the upper left.</summary>
        static char BatMetal(float rx, float ry, float r, bool fine)
        {
            float lit = (rx * -0.55f + ry * -0.83f) / Mathf.Max(0.01f, r);
            if (r > BatRingOuter - (fine ? 0.4f : 0.7f) || r < BatRingInner + (fine ? 0.35f : 0.5f))
            {
                if (lit > 0.5f) return fine ? 'h' : 'l';
                if (lit > 0.1f) return 'l';
                if (lit < -0.4f) return 's';
                if (lit < -0.05f) return 'd';
            }
            return 'b';
        }

        static readonly Vector2Int BloodGrip = new(13, GripCentre(BatGripTop, SwordGripRows));
        static readonly Vector2Int BloodDetailGrip =
            new(BloodGrip.x * EmberDetailScale, BloodGrip.y * EmberDetailScale);

        /// <summary>Five sprites, empty through full - see Tuning.Blood.MaxFill. Baked rather
        /// than computed live, the same reason every stage cycle in this file is: the project
        /// swaps whole sprites, it does not repaint a texture per hit.</summary>
        static Sprite[] BuildBloodStages()
        {
            var stages = new Sprite[Core.Tuning.Blood.MaxFill + 1];
            for (int i = 0; i < stages.Length; i++)
                stages[i] = StageSprite("gear.weapon.blood." + i, BuildBloodSword(i), BatPal(), BloodGrip);
            return stages;
        }

        // 9 x 13. Blood Blade's worn mark - the same caged shape every other black-diamond relic
        // here uses, holding an actual DROP rather than a flat rectangle. The first attempt
        // tapered both ends to a point and read as a lens/eye instead - a droplet is pointed on
        // the end it fell FROM and rounded on the end it's resting on, so only the TOP narrows;
        // the belly holds its full width all the way to the bottom edge instead of mirroring the
        // taper back down. Outlined in the empty-glass ramp's own dark tone so the shape reads
        // against the cage's black interior even at this size, the same D-then-5 framing the
        // sword's own gem uses.
        static readonly string[] BloodRelicRows =
        {
                "....l....",
                "...lll...",
                "..l...l..",
                ".lllllll.",
                "l...D...l",
                "l..D5D..l",
                "l.D555D.l",
                "l.D555D.l",
                "l.D555D.l",
                "l.D555D.l",
                ".lllllll.",
                "..l...l..",
                "...lll...",
        };

        // ============================================================== Crossblade
        //
        // A FLANGED MACE HEAD on the tip of a sword (see CrossbladeHeadTexel) - the head a mace,
        // the rest a blade - with a ringed guard (pointed quillons through a ring round the
        // blade's base, a boss) and a small cross cut into the pommel. The head went through a
        // ringed cross and a spiked ball first; both read as too busy.
        //
        // Two materials nothing else here uses:
        //   DAMASCUS - the blade and the head's arms, flowing layered grain (two independent
        //              waves bending the strata, so it never repeats as a stripe). In the arena
        //              it is a quiet two-tone ripple; the menu art is where it shows.
        //   BLUED    - heat-blued steel, a deep blue-violet sheen, for the guard's ring, the
        //              quillons, boss and pommel. The grip is wrapped in dark leather.
        //
        // It was a plain grey steel sword with a spade-like cross near the tip. The family's grip
        // and pivot (GreatswordGrip), so it sits in the hand as the rest do. One field
        // (CrossbladeTexel) for arena and menu.

        static Dictionary<char, Color> CrossbladePal()
        {
            var steel = new Palette.Ramp(new Color(0.62f, 0.63f, 0.67f), lift: 0.50f, shade: 0.34f);
            var blued = new Palette.Ramp(new Color(0.22f, 0.25f, 0.50f), lift: 0.48f, shade: 0.40f)
                .WithHighlightsToward(new Color(0.70f, 0.62f, 0.95f), 0.45f);
            var leather = new Palette.Ramp(new Color(0.34f, 0.21f, 0.14f), lift: 0.30f, shade: 0.45f);
            return Palette.Of(steel, blued, leather);
        }

        const float CrossbladeAxis = 14f;                        // this grid centres on a COLUMN
        const float CrossbladeHalf = 4.5f;                        // the blade, 9 cells
        // An ODD-width grid (it centres on a column), so the grip is the family's width plus one.
        const float CrossbladeGripHalf = SwordGripWidth / 2f + 0.5f;
        static float CrossbladeGripTop => 6 + BladeRows + 1 + 1 + 4 + 1 - 0.5f;   // the family's
        static float CrossbladeGuardY => CrossbladeGripTop - 2.2f;
        static float CrossbladePommelTop => CrossbladeGripTop + SwordGripRows;

        static string[] BuildCrossbladeSword()
        {
            var rows = new string[SwordHeightRows];
            for (int y = 0; y < rows.Length; y++)
            {
                var line = new char[28];
                for (int x = 0; x < 28; x++) line[x] = CrossbladeTexel(x, y, fine: false);
                rows[y] = new string(line);
            }
            return rows;
        }

        /// <summary>Menu art at TRUE <see cref="MenuPpu"/> - never pass it upscale2x.</summary>
        static string[] BuildCrossbladeSwordDetail()
        {
            const int k = EmberDetailScale;
            var rows = new string[SwordHeightRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[28 * k];
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++)
                    line[X] = CrossbladeTexel((X + 0.5f) / k - 0.5f, y, fine: true);
                rows[Y] = new string(line);
            }
            return rows;
        }

        static readonly string[] CrossbladeRows = BuildCrossbladeSword();
        static readonly string[] CrossbladeSwordDetail = BuildCrossbladeSwordDetail();
        static Vector2Int CrossbladeGrip => GreatswordGrip;
        static Vector2Int CrossbladeDetailGrip => EmberDetailGrip;

        /// <summary>A ring's band round (cy): distance to it, and its facing for shading.</summary>
        static float CrossbladeRingSd(float x, float y, float cy, float r, float band)
        {
            float dx = x - CrossbladeAxis, dy = y - cy;
            return Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r) - band;
        }

        /// <summary>The blued pieces: both rings, the quillons, the boss, the pommel disc.</summary>
        static float CrossbladeBluedSd(float x, float y)
        {
            float ax = Mathf.Abs(x - CrossbladeAxis);
            float guardRing = CrossbladeRingSd(x, y, CrossbladeGuardY, 3.9f, 0.7f);
            // Quillons: tapering to points, a little longer than the ring is wide.
            float gy = CrossbladeGuardY;
            float quill = ax > 8.8f ? 99f : Mathf.Abs(y - gy) - 1.5f * (1f - ax / 8.8f) - 0.15f;
            float boss = Mathf.Sqrt((x - CrossbladeAxis) * (x - CrossbladeAxis) + (y - gy) * (y - gy)) - 1.9f;
            // The pommel GROWS OUT OF THE GRIP: from the grip's own width, flaring into a rounded
            // cap, with no step between them. An oval set under the grip read as a part stuck on.
            float pt = CrossbladePommelTop - 0.5f;
            float u = y - pt;
            float flare = CrossbladeGripHalf + 0.9f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u + 0.6f) / 1.6f));
            float capBottom = pt + 3.45f;                        // to the last row: the family height
            float round = u > 1.2f ? flare * Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Pow((u - 1.2f) / (capBottom - pt - 1.2f + 0.01f), 2f))) : flare;
            float pommel = u < -0.6f ? 99f : Mathf.Max(ax - round, Mathf.Max(pt - 0.6f - y, y - capBottom));
            return Mathf.Min(guardRing, Mathf.Min(quill, Mathf.Min(boss, pommel)));
        }

        /// <summary>The damascus: the blade, and the head's arms tapering to points.</summary>
        static float CrossbladeSteelSd(float x, float y)
        {
            float ax = Mathf.Abs(x - CrossbladeAxis);
            // The blade, from under the head's collar to the guard.
            if (y < CrossbladeCollarBottom - 0.3f || y >= CrossbladeGuardY) return 99f;
            return ax - CrossbladeHalf;
        }

        static char CrossbladeTexel(float x, float y, bool fine)
        {
            float sx = x - CrossbladeAxis, ax = Mathf.Abs(sx);

            // Blued pieces first: the rings pass over the blade and arms.
            float bl = CrossbladeBluedSd(x, y);
            if (bl < 0f)
            {
                // The pommel's cross, cut into it.
                float pt = CrossbladePommelTop - 0.5f, pc = pt + 1.0f;
                if ((ax < (fine ? 0.25f : 0.5f) && Mathf.Abs(y - pc) < 1.1f)
                    || (Mathf.Abs(y - pc) < (fine ? 0.25f : 0.5f) && ax < 1.6f && y > pt))
                    return 'S';
                return CrossbladeShade(x, y, bl, CrossbladeBluedSd, fine, 'H', 'L', 'B', 'D', 'S');
            }

            // The flanged mace head, on the blade's tip.
            if (y < CrossbladeCollarBottom)
            {
                char head = CrossbladeHeadTexel(x, y, fine);
                if (head != '.') return head;
            }

            float st = CrossbladeSteelSd(x, y);
            if (st < 0f)
            {
                // The polished cutting edges, both sides, and the fuller down the centre.
                float edge = -st;
                if (edge < (fine ? 0.4f : 1f)) return sx < 0f ? 'h' : 'l';
                if (y > 12f && y < CrossbladeGuardY - 4f && ax < (fine ? 0.2f : 0.5f)) return 's';
                return CrossbladeDamascus(x, y, fine);
            }

            // The grip: a LEATHER wrap - broad strips in one tone, with the dark line where each
            // turn overlaps the next wide enough to read as a gap, not an engraving.
            if (y > CrossbladeGripTop && y < CrossbladePommelTop - 0.5f && ax < CrossbladeGripHalf)
            {
                float w = (y + sx * 0.6f) / 2f;
                float fr = w - Mathf.Floor(w);
                return fr < 0.34f ? '2' : '4';
            }
            return '.';
        }

        // ---- the head: a FLANGED MACE
        //
        // After the user's reference: a flanged mace head on the blade's tip - a small finial,
        // concave edges flaring to SHARP flange corners about a third of the way down, then
        // curving back in to a collar where the blade begins. The flanges are told by LINE WORK
        // only: the one facing the viewer is a fin seen edge-on down the centre (a bright ridge
        // between two dark lines), the ones turned away are curves nested inside the silhouette,
        // the faces between the lines in the blade's own DAMASCUS - the head and blade are one
        // steel - and a plain steel collar where they meet.
        //
        // It was a spiked ball with a front-facing pyramid before this, and a ringed cross before
        // that; both read as too busy.

        const float CrossbladeHeadTop = 0.8f;       // where the flanges start, under the finial
        // TALLER than it is wide, as the reference's is: corners high, then a long taper into the
        // collar. Wider and shorter it read as a squat fan.
        const float CrossbladeCornerY = 5.2f;       // the flange corners
        const float CrossbladeCornerHalf = 8.4f;
        const float CrossbladeHeadBase = 17.2f;     // back to the blade's width
        const float CrossbladeCollarBottom = 18.4f;

        /// <summary>The head's half-width at row <paramref name="y"/>, scaled by
        /// <paramref name="k"/> for the flanges nested inside it. Both edges CONCAVE.</summary>
        static float CrossbladeHeadHalf(float y, float k = 1f)
        {
            if (y < CrossbladeHeadTop || y > CrossbladeHeadBase) return -1f;
            float root = 1.2f;
            if (y <= CrossbladeCornerY)
            {
                float t = (y - CrossbladeHeadTop) / (CrossbladeCornerY - CrossbladeHeadTop);
                return root + (CrossbladeCornerHalf * k - root) * t * t;
            }
            float u = (y - CrossbladeCornerY) / (CrossbladeHeadBase - CrossbladeCornerY);
            float baseHalf = CrossbladeHalf;
            return baseHalf + (CrossbladeCornerHalf * k - baseHalf) * (1f - u) * (1f - u);
        }

        static float CrossbladeHeadSd(float x, float y)
        {
            float ax = Mathf.Abs(x - CrossbladeAxis);
            float head = 99f;
            float half = CrossbladeHeadHalf(y);
            if (half > 0f) head = ax - half;
            // The finial: a small point on top.
            if (y < CrossbladeHeadTop + 0.4f && y > -0.5f)
                head = Mathf.Min(head, ax - 1.3f * (y + 0.5f) / (CrossbladeHeadTop + 0.9f));
            return head;
        }

        static char CrossbladeHeadTexel(float x, float y, bool fine)
        {
            float sx = x - CrossbladeAxis, ax = Mathf.Abs(sx);

            // The collar under the head: a band of plain steel.
            if (y > CrossbladeHeadBase - 0.2f && y < CrossbladeCollarBottom && ax < CrossbladeHalf + 0.8f)
                return y < CrossbladeHeadBase + 0.35f ? 'h' : sx < -2f ? 'l' : 'd';

            float sd = CrossbladeHeadSd(x, y);
            if (sd >= 0f) return '.';
            float edge = -sd;
            if (y < CrossbladeHeadTop + 0.4f) return sx < 0f ? 'h' : 'l';            // the finial
            if (edge < (fine ? 0.35f : 1f)) return sx < 0f ? 'h' : 'l';              // the outline

            // The flange facing the viewer, edge-on: a bright ridge between two dark lines.
            float w = fine ? 0.16f : 0.5f;
            if (ax < w) return 'h';
            if (fine && ax < 0.42f) return 's';

            // The flanges turned away: curves nested inside the silhouette.
            foreach (float k in new[] { 0.62f, 0.32f })
            {
                float nh = CrossbladeHeadHalf(y, k);
                if (nh > 0f && Mathf.Abs(ax - nh) < (fine ? 0.14f : 0.45f)) return 's';
            }

            // The faces between them: the BLADE's own damascus - the flanges are the same steel.
            return CrossbladeDamascus(x, y, fine);
        }

        /// <summary>Damascus grain: strata bent by two independent waves, so it flows along the
        /// blade without ever repeating as a stripe. The arena gets two tones of it.</summary>
        static char CrossbladeDamascus(float x, float y, bool fine)
        {
            float sx = x - CrossbladeAxis;
            float p = sx * 0.95f + 1.5f * Mathf.Sin(y * 0.21f + sx * 0.3f) + 0.6f * Mathf.Sin(y * 0.57f - sx * 0.9f + 1.3f);
            float f = p / 1.35f - Mathf.Floor(p / 1.35f);
            // Arena: SPARSE dark strata on the body. Two tones in equal measure came out as
            // digital camouflage at cell size.
            if (!fine) return f < 0.18f ? 'd' : 'b';
            if (f < 0.16f) return 'd';
            if (f < 0.45f) return 'b';
            if (f < 0.62f) return 'l';
            return 'b';
        }

        /// <summary>Shading from the surface normal, lit from the upper left, for a solid piece.</summary>
        static char CrossbladeShade(float x, float y, float sd, System.Func<float, float, float> field, bool fine,
                                    char hi, char lit, char body, char dark, char deep)
        {
            const float e = 0.3f;
            float nx = field(x + e, y) - field(x - e, y);
            float ny = field(x, y + e) - field(x, y - e);
            float nl = Mathf.Sqrt(nx * nx + ny * ny);
            if (nl > 1e-4f) { nx /= nl; ny /= nl; }
            float l = nx * -0.55f + ny * -0.83f;
            if (-sd < (fine ? 0.35f : 0.7f))
            {
                if (l > 0.5f) return fine ? hi : lit;
                if (l > 0.1f) return lit;
                if (l < -0.45f) return deep;
                if (l < -0.05f) return dark;
            }
            return body;
        }

        // ============================================================== Lumen

        /// <summary>
        /// Lumen's palette: CYAN plasma at the edges (lowercase, alpha-written), a STEEL core
        /// AND hilt (uppercase, fully opaque - the hilt reuses the blade's own core material
        /// rather than a fourth ramp, since Palette.Of only ever gives three), and a RED wrap
        /// (digits) bound around the grip. The core stays solid and stays METAL rather than
        /// glowing white - "the blade edges should be pure energy" names the EDGES specifically,
        /// so the core is the physical spine the energy is escaping FROM, and steel says that
        /// more plainly than a second glow would. Alpha only ever goes on the cyan tones, the
        /// same restraint Shadow's own blade and the Rift Blade's tear both already keep for the
        /// identical reason: a material that is translucent everywhere stops describing
        /// anything, and a blade that is solid nowhere has nothing for the eye to rest on.
        /// </summary>
        static Dictionary<char, Color> LumenPal()
        {
            var cyan = new Palette.Ramp(new Color(0.18f, 0.85f, 0.95f), lift: 0.55f, shade: 0.20f);
            var steel = new Palette.Ramp(new Color(0.56f, 0.58f, 0.63f), lift: 0.42f, shade: 0.26f);
            var red = new Palette.Ramp(new Color(0.70f, 0.11f, 0.14f), lift: 0.40f, shade: 0.24f);
            var map = Palette.Of(cyan, steel, red);

            // The HILT is a circuit board: solder-mask green, gold traces and contacts, black
            // chips. Custom letters - Palette.Of only ever gives three ramps and the blade has
            // them all.
            map['e'] = new Color(0.04f, 0.20f, 0.10f);          // board edge / shade
            map['g'] = new Color(0.09f, 0.40f, 0.19f);          // the solder mask
            map['f'] = new Color(0.26f, 0.64f, 0.32f);          // lit edge, and trace under mask
            map['y'] = new Color(0.86f, 0.68f, 0.26f);          // gold
            map['Y'] = new Color(1.00f, 0.90f, 0.56f);          // gold, lit
            map['c'] = new Color(0.08f, 0.08f, 0.10f);          // chip body
            map['m'] = new Color(0.22f, 0.23f, 0.27f);          // chip, lit edge
            map['n'] = new Color(0.58f, 0.60f, 0.64f);          // pin-one dot / marking
            map['w'] = new Color(0.86f, 0.92f, 0.86f);          // silkscreen
            // The blade's light on the board. OPAQUE, unlike the blade's own cyan: an alpha texel
            // here would show the arena through the board, not the board lit.
            map['t'] = new Color(0.62f, 0.97f, 1.00f);          // a feed trace, carrying power
            map['i'] = new Color(0.22f, 0.70f, 0.72f);          // a signal trace; a rim in the glow
            map['j'] = new Color(0.14f, 0.55f, 0.42f);          // mask lit by the blade
            map['z'] = new Color(0.11f, 0.47f, 0.29f);          // the spill fading out

            // The digits are the TAIL's red insulation (the grip's old wrap colour), and the
            // tail ends in bare copper, frayed, with sparks at the strand ends - a live wire.
            map['o'] = new Color(0.78f, 0.40f, 0.17f);          // copper
            map['p'] = new Color(1.00f, 0.68f, 0.38f);          // copper, lit
            map['q'] = new Color(1.00f, 0.98f, 0.82f);          // a spark
            map['r'] = new Color(0.55f, 0.92f, 1.00f);          // a spark's cyan halo

            // Weighted to the LIT cyan tones, the same idiom every alpha-written material in this
            // file already uses - a highlight is where the light is thickest, and thickened
            // plasma is the part that is least see-through.
            foreach (var (c, a) in LumenEdgeAlphas)
            {
                var col = map[c];
                col.a = a;
                map[c] = col;
            }
            return map;
        }

        static readonly (char C, float A)[] LumenEdgeAlphas =
        {
            ('h', 0.90f), ('l', 0.68f), ('b', 0.50f), ('d', 0.34f), ('s', 0.24f),
        };

        /// <summary>
        /// The full cross-section, as ONE string resampled across whatever width a given row
        /// needs - the same trick <see cref="SniperFill"/> already does for a steel band, applied
        /// here to a string that mixes BOTH ramps at once (lowercase cyan, uppercase white) rather
        /// than one material. Resampling it for a narrower row keeps the same PROPORTIONS the
        /// full-width blade has, so the tip tapers as a smaller version of the same cross-section
        /// instead of losing one material as it narrows.
        ///
        /// SEVEN cyan texels a side against six steel ones - THICK energy edges, per the brief
        /// (a typo the first pass read as "thin" made this wrong once already). The steel core is
        /// a narrow spine down the centre now, not the dominant material - matching the reference
        /// sketch, where the glow is clearly the thicker of the two bands rather than a fringe on
        /// a mostly-solid blade.
        /// </summary>
        // Reverted after a live look: darkening this for vein contrast (an intermediate pass)
        // read as duller and less alive than the brighter version below, which the user preferred
        // outright once both were seen side by side. The bright band's own jittery edge - where
        // the vein overlay mostly shows up as boundary jitter rather than a clean crack - IS the
        // liked look, not a flaw to fix; see the vein note below for why this is kept anyway.
        const string LumenBand = "dllhhhh" + "HBBBBH" + "hhhhlld";

        /// <summary>
        /// Eleven, down from fifteen - the grid is DOUBLED now (see <see cref="OutlinelessWeapon"/>),
        /// so every cell here is worth twice what it used to be and the old number would have put
        /// a beam of light half again the width of the character's torso. Odd, because this grid
        /// centres on column 14 rather than on the canvas boundary. <see cref="LumenBand"/> is
        /// resampled across whatever this is, so the band's shape survives the change.
        /// </summary>
        const int LumenBladeWidth = 11;

        /// <summary>
        /// A crackling vein wandering through one cyan band, expressed as a FRACTION of that
        /// band's own width (0 at one inner face, 1 at the other) so it works at any row's
        /// actual width rather than a fixed texel offset. Two frequencies summed, the same
        /// multi-scale-noise idiom the Rift Blade's own tear edges use, for the identical reason:
        /// one clean sine is a smooth wobble, and lightning is not smooth at any one scale.
        /// </summary>
        static float LumenVein(int row, float f1, float p1, float f2, float p2)
            => LumenVein((float)row, f1, p1, f2, p2);

        static float LumenVein(float row, float f1, float p1, float f2, float p2)
        {
            float v = 0.5f + 0.32f * Mathf.Sin(row * f1 + p1) + 0.18f * Mathf.Sin(row * f2 + p2);
            return Mathf.Clamp01(v);
        }

        /// <summary>
        /// One row of the blade - the resampled cyan/steel/cyan band, with a bright crackling
        /// vein stamped into EACH cyan half afterward. "The lit parts of the blade should feel
        /// electric" is not answered by a smooth gradient at any brightness - a gradient reads as
        /// glass or glow, and lightning reads as a JAGGED bright line wandering inside a duller
        /// field, which is a different shape, not just a different colour. Two independent veins
        /// (different frequencies and phases) rather than one, so the two cyan bands never crack
        /// in the same place at the same height and the pattern does not read as mirrored.
        /// </summary>
        static string LumenBladeRow(int width, int row)
        {
            var line = new char[28];
            for (int x = 0; x < 28; x++) line[x] = '.';
            int from = 14 - width / 2, to = from + width - 1;

            // TOO NARROW TO SHOW THE FULL CYAN/STEEL/CYAN STRUCTURE, and the resample used to
            // paper over that by just sampling the band's own first character - which is cyan,
            // so the blade's own POINT read as pure plasma with no steel in it at all. A real
            // blade's spine runs all the way to its point; the plasma coating is what only
            // becomes visible once there is room to show both materials at once. Filled in with
            // plain steel instead, so the core reads as present from the very tip rather than
            // fading in a few rows down.
            if (width < 5)
            {
                for (int x = from; x <= to; x++) line[x] = x == from || x == to ? 'H' : 'B';
                return new string(line);
            }

            SniperFill(line, from, to, LumenBand);

            // Cyan bands are the outer 7/20 of the full-width blade on each side - derived from
            // LumenBand's own proportions rather than a second hardcoded ratio, so the two can
            // never quietly disagree if the band string is ever retuned.
            int cyanWidth = Mathf.Max(1, Mathf.RoundToInt(width * 7f / 20f));
            if (cyanWidth >= 2)
            {
                int leftTo = from + cyanWidth - 1;
                int lx = from + Mathf.RoundToInt(LumenVein(row, 0.55f, 0.6f, 1.7f, 2.1f) * (cyanWidth - 1));
                line[Mathf.Clamp(lx, from, leftTo)] = 'h';

                int rightFrom = to - cyanWidth + 1;
                int rx = rightFrom + Mathf.RoundToInt(LumenVein(row, 0.47f, 3.4f, 1.9f, 0.3f) * (cyanWidth - 1));
                line[Mathf.Clamp(rx, rightFrom, to)] = 'h';
            }
            return new string(line);
        }

        static string LumenFlatRow(int width, char left, char mid, char right)
        {
            var line = new char[28];
            for (int x = 0; x < 28; x++) line[x] = '.';
            int from = 14 - width / 2, to = from + width - 1;
            for (int x = from; x <= to; x++) line[x] = x == from ? left : x == to ? right : mid;
            return new string(line);
        }

        /// <summary>Rows in the wrapped grip - named so the hanging tail below can be built
        /// to the SAME length rather than a second hand-typed number that could quietly drift
        /// from it.</summary>
        const int LumenGripRows = SwordGripRows;

        /// <summary>
        /// Settled by the family's own accounting, less the outline this weapon does not carry -
        /// see <see cref="BladeRowsForNoOutline"/>. The tip, the triangle guard, the grip and the
        /// pin-header pommel are what it spends on something other than blade.
        /// </summary>
        static int LumenTipRows => (LumenBladeWidth + 1) / 2;
        // The cylinder's height: about its own width, the proportion of a collar rather than a
        // second grip. The rows the triangle and the disc spent go back to the blade.
        const int LumenGuardRows = 10;
        const int LumenPommelRows = 3;
        static int LumenBladeRows => BladeRowsForNoOutline(
            LumenTipRows + LumenGuardRows + LumenGripRows + LumenPommelRows);
        static int LumenGuardTop => LumenTipRows + LumenBladeRows;
        static int LumenGripTop => LumenGuardTop + LumenGuardRows;
        static int LumenPommelTop => LumenGripTop + LumenGripRows;
        static int LumenHeightRows => LumenPommelTop + LumenPommelRows;

        /// <summary>This grid centres on a COLUMN (14), not on a boundary like the rest.</summary>
        const float LumenAxis = 14f;

        /// <summary>
        /// Lumen - a Halo energy-sword and a greatsword read as one weapon: a perfectly ordinary
        /// straight taper carrying a material no ordinary greatsword could.
        ///
        /// The hilt is a CIRCUIT BOARD - the triangle guard from the user's reference sheet, filled
        /// rather than framed so it has somewhere to carry the chips: one large chip in the middle
        /// wired by gold traces to a smaller one in each lower corner, and a trace running up from
        /// the big chip into the blade in the blade's own cyan - the board is what powers the
        /// light. The grip is the board's edge, gold contact fingers like a memory stick's; the
        /// pommel is a pin header, and the ribbon tail runs out of it as a ribbon cable.
        ///
        /// The blade PINCHES into an emitter socket at the triangle's apex, and every trace on the
        /// board glows its cyan and runs to that socket - see "the socket, and the power it feeds".
        /// </summary>
        static string[] BuildLumenSword()
        {
            var rows = LumenBladeOnly();
            for (int y = LumenGuardTop - 2; y < rows.Length; y++)
            {
                var line = rows[y].ToCharArray();
                for (int x = 0; x < line.Length; x++)
                {
                    char c = LumenHiltTexel(x, y, fine: false);
                    if (c != '.') line[x] = c;
                }
                rows[y] = new string(line);
            }
            return rows;
        }

        /// <summary>The blade alone, pinching into the socket at the board's apex.</summary>
        static string[] LumenBladeOnly()
        {
            var rows = new List<string>(LumenHeightRows);
            int row = 0;
            for (int w = 1; w <= LumenBladeWidth; w += 2) rows.Add(LumenBladeRow(w, row++));
            for (int i = 0; i < LumenBladeRows - LumenPinchRows - 1; i++) rows.Add(LumenBladeRow(LumenBladeWidth, row++));
            // The pinch into the socket: a cell off each side per row, the tip's taper reversed.
            for (int i = 1; i <= LumenPinchRows; i++) rows.Add(LumenBladeRow(LumenBladeWidth - 2 * i, row++));
            while (rows.Count < LumenHeightRows) rows.Add(new string('.', 28));
            return rows.ToArray();
        }

        /// <summary>
        /// Menu art at TRUE <see cref="MenuPpu"/>, four samples a cell - the blade from
        /// <see cref="LumenBladeTexel"/>, the board drawn over it from the hilt's own field.
        ///
        /// The blade used to be the arena grid run through the derived pass's smoothing, which on
        /// a weapon this regular is the arena picture at four times the size: the same cells, the
        /// same stepped veins. It is a field now - the SAME outline, cross-section and veins, only
        /// resolved finer - so the gradient runs smooth and the veins crackle at menu scale.
        /// </summary>
        static string[] BuildLumenSwordDetail()
        {
            const int k = EmberDetailScale;
            var rows = new string[LumenHeightRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[28 * k];
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++)
                {
                    float x = (X + 0.5f) / k - 0.5f;
                    char c = y > LumenSocketTop - 0.5f ? LumenHiltTexel(x, y, fine: true) : '.';
                    line[X] = c != '.' ? c : LumenBladeTexel(x, y);
                }
                rows[Y] = new string(line);
            }
            return rows;
        }

        /// <summary>
        /// The blade as a field in cells, for the menu art. Matches <see cref="LumenBladeRow"/> at
        /// the silhouette: a point widening a cell a side per row to the blade's eleven, then
        /// parallel to the board's base; <see cref="LumenBand"/> resampled across it; a vein
        /// wandering through each cyan band on the same two waves - plus, at this density, a
        /// fast small jitter on top, because a smooth curve at four samples a cell reads as a
        /// wire, and lightning is jagged at every scale you can see.
        /// </summary>
        static char LumenBladeTexel(float x, float y)
        {
            if (y > LumenSocketTop + 0.5f || y < -0.5f) return '.';
            float pinch = LumenGuardTop - 2f - LumenPinchRows;      // the last full-width row
            float half = Mathf.Min(y + 0.5f, LumenBladeWidth / 2f - Mathf.Max(0f, y - pinch));
            float dx = Mathf.Abs(x - LumenAxis);
            if (dx > half) return '.';

            // Too narrow for the full cross-section: the steel spine alone, as the arena's point.
            if (half < 2.5f) return dx > half - 0.35f ? 'H' : 'B';

            float from = LumenAxis - half;                       // the blade's left edge
            float u = Mathf.Clamp01((x - from) / (2f * half));
            char c = LumenBand[Mathf.Clamp(Mathf.FloorToInt(u * LumenBand.Length), 0, LumenBand.Length - 1)];

            // The veins, one per cyan band, each on its own waves.
            float band = 2f * half * 7f / 20f;
            float lv = LumenVein(y, 0.55f, 0.6f, 1.7f, 2.1f) + 0.06f * Mathf.Sin(y * 5.3f + 0.7f);
            float rv = LumenVein(y, 0.47f, 3.4f, 1.9f, 0.3f) + 0.06f * Mathf.Sin(y * 4.6f + 2.2f);
            float lc = from + 0.5f + Mathf.Clamp01(lv) * (band - 1f);
            float rc = LumenAxis + half - band + 0.5f + Mathf.Clamp01(rv) * (band - 1f);
            float d = Mathf.Min(Mathf.Abs(x - lc), Mathf.Abs(x - rc));
            if (d < 0.22f) return 'h';
            if (d < 0.45f && (c == 'l' || c == 'd')) return 'l';

            // A hairline of shine down the steel's middle, which the arena has no cell for.
            if (c == 'B' && Mathf.Abs(x - LumenAxis + 0.4f) < 0.12f) return 'L';
            return c;
        }

        static readonly string[] LumenRows = BuildLumenSword();
        static readonly string[] LumenSwordDetail = BuildLumenSwordDetail();

        static Vector2Int LumenGrip => new(13, GripCentre(LumenGripTop, LumenGripRows));
        static Vector2Int LumenDetailGrip => new(LumenGrip.x * EmberDetailScale, LumenGrip.y * EmberDetailScale);

        // ---- the board
        //
        // A CYLINDER of circuit board, upright on the grip's axis and the pommel's width, so it
        // sits in line with the rest of the hilt rather than spreading past it. The pattern is laid
        // out in SURFACE coordinates - arc length round the cylinder, and height - and projected:
        // the big chip on the front face reads square, and the two small chips low on the sides
        // wrap round the curve, squeezed toward the silhouette and half gone past it, which is
        // what makes it read as round rather than as a flat plate. It was a triangle (the
        // reference's guard) and then a flat disc before this.

        const float LumenBoardRadius = 4.5f;

        static float LumenApexY => LumenGuardTop - 0.5f;               // the cylinder's top
        static float LumenBaseY => LumenGripTop - 0.5f;                // its bottom
        static float LumenChipY => LumenApexY + 4.6f;
        const float LumenChipHalfW = 2.0f, LumenChipHalfH = 2.0f;      // in arc length

        /// <summary>The small chips: centred 72 degrees round from the front, so a good part of
        /// each has already turned out of sight past the silhouette.</summary>
        static float LumenSmallU => LumenBoardRadius * 72f * Mathf.Deg2Rad;
        static float LumenSmallY => LumenBaseY - 2.4f;
        const float LumenSmallHalf = 1.8f;                             // in arc length

        /// <summary>Signed distance to the cylinder's silhouette - a plain rectangle.</summary>
        static float LumenBoardSd(float x, float y)
            => Mathf.Max(Mathf.Abs(x - LumenAxis) - LumenBoardRadius,
                         Mathf.Max(LumenApexY - y, y - LumenBaseY));

        // ---- the socket, and the power it feeds
        //
        // The blade pinches into an emitter SOCKET at the cylinder's top, in the core's own steel,
        // and every trace glows the blade's cyan: the small chips feed the big one and the big one
        // feeds the socket. The blade's light spills onto the top of the board.

        /// <summary>Rows over which the blade pinches from its full width into the socket.</summary>
        const int LumenPinchRows = 4;
        static float LumenSocketTop => LumenGuardTop - 1.5f;
        static float LumenSocketBottom => LumenGuardTop + 1.0f;
        const float LumenSocketHalf = 2.3f;

        /// <summary>Distance from a point to the segment a-b.</summary>
        static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy));
            float qx = ax + vx * t - px, qy = ay + vy * t - py;
            return Mathf.Sqrt(qx * qx + qy * qy);
        }

        /// <summary>
        /// Distance, on the SURFACE, to the power: straight up from the big chip into the socket,
        /// and out of the big chip's lower corners on a 45-degree run to each small chip. Measured
        /// in arc length, so the runs curve round the cylinder with everything else.
        /// </summary>
        static float LumenFeedDistance(float u, float y)
        {
            float au = Mathf.Abs(u);
            float centre = y < LumenChipY - LumenChipHalfH && y > LumenSocketBottom - 0.3f ? au : 99f;
            float u0 = LumenChipHalfW - 0.6f, y0 = LumenChipY + LumenChipHalfH;
            float u1 = LumenSmallU - LumenSmallHalf + 0.3f, y1 = LumenSmallY;
            float run = SegmentDistance(au, y, u0, y0, u1, y1);
            return Mathf.Min(centre, run);
        }

        static char LumenHiltTexel(float x, float y, bool fine)
        {
            float dx = Mathf.Abs(x - LumenAxis);

            // The emitter socket over the top: a steel block, a lit top edge, the slot the beam
            // leaves glowing cyan.
            if (y > LumenSocketTop && y < LumenSocketBottom && dx < LumenSocketHalf)
            {
                float top = y - LumenSocketTop;
                if (top < (fine ? 0.35f : 1f) && dx < 1.6f) return fine && top < 0.18f ? 't' : 'h';
                // In the blade core's own STEEL, not chip black: the spine runs on down into the
                // board through it. Black, it read as a hole the blade fell into.
                if (top < (fine ? 0.6f : 1.6f)) return 'L';
                if (dx > LumenSocketHalf - (fine ? 0.35f : 1f)) return x < LumenAxis ? 'B' : 'S';
                return 'D';
            }

            if (LumenBoardSd(x, y) < 0f) return LumenBoard(x, y, fine);

            // The grip: the board's own edge, gold contact fingers down it.
            // An ODD-width grid, so the family's grip width plus one.
            if (y > LumenBaseY && y < LumenPommelTop - 0.5f && dx < SwordGripWidth / 2f + 0.5f)
            {
                if (dx > SwordGripWidth / 2f - 0.1f) return x < LumenAxis ? 'f' : 'e';
                bool finger = y > LumenGripTop + 0.5f && y < LumenPommelTop - 1f;
                float f = Mathf.Repeat(dx + 0.5f, 2f);          // fingers at dx 0 and 2
                if (finger && (fine ? Mathf.Abs(f - 0.5f) < 0.36f : f < 1f))
                    return fine && x < LumenAxis && Mathf.Abs(f - 0.5f) > 0.18f ? 'Y' : 'y';
                return 'g';
            }

            // The pommel: a strip of board, the header's black body, and its pins.
            float py = y - (LumenPommelTop - 0.5f);
            if (py > 0f && py < 1f && dx < 4.5f) return dx > 4f ? 'e' : fine && py < 0.25f ? 'f' : 'g';
            if (py >= 1f && py < 2f && dx < SwordGripWidth / 2f + 0.5f) return fine && py < 1.25f ? 'm' : 'c';
            if (py >= 2f && py < 3f && dx < SwordGripWidth / 2f + 0.5f)
            {
                float pin = Mathf.Repeat(x - LumenAxis + 0.5f, 2f);
                if (fine ? Mathf.Abs(pin - 0.5f) < 0.3f : pin < 1f) return fine && pin < 0.45f ? 'Y' : 'y';
            }
            return '.';
        }

        /// <summary>
        /// A texel of the cylinder: unproject to the surface (arc length u, height y), pick the
        /// pattern there, and shade it by how far round the curve it faces - lit on the left,
        /// falling into shade on the right.
        /// </summary>
        static char LumenBoard(float x, float y, bool fine)
        {
            float nx = Mathf.Clamp((x - LumenAxis) / LumenBoardRadius, -1f, 1f);
            float u = LumenBoardRadius * Mathf.Asin(nx);
            float light = -nx;                                  // +1 full on the lit side
            bool lit = light > 0.35f, shade = light < -0.55f;

            // The ends: a dark band where the cylinder's bottom turns under, and the top catching
            // the blade's light.
            float fromTop = y - LumenApexY, fromBottom = LumenBaseY - y;
            if (fromBottom < (fine ? 0.4f : 0.9f)) return 'e';
            if (fromTop < (fine ? 0.35f : 0.9f)) return fine && fromTop < 0.18f && !shade ? 't' : 'i';

            // The big chip, facing front: a pin-one dot, a marking, and pins down both sides.
            float cu = u, cy = y - LumenChipY;
            if (Mathf.Abs(cu) < LumenChipHalfW && Mathf.Abs(cy) < LumenChipHalfH)
            {
                if (fine)
                {
                    if (Mathf.Abs(cu + LumenChipHalfW - 0.6f) < 0.25f && Mathf.Abs(cy + LumenChipHalfH - 0.6f) < 0.25f) return 'n';
                    if (Mathf.Abs(cu) < 1.0f && Mathf.Abs(cy - 0.2f) < 0.12f) return 'n';
                    if (cu < -LumenChipHalfW + 0.3f || cy < -LumenChipHalfH + 0.3f) return 'm';
                }
                else if (Mathf.RoundToInt(x - LumenAxis) == -1 && Mathf.RoundToInt(cy) == -1) return 'n';
                return lit && !fine ? 'm' : 'c';
            }
            if (Mathf.Abs(cu) < LumenChipHalfW + (fine ? 0.55f : 0.9f) && Mathf.Abs(cy) < LumenChipHalfH - 0.3f)
            {
                float pin = Mathf.Repeat(cy + 0.5f, 1f);
                if (fine ? pin < 0.5f : Mathf.RoundToInt(cy) % 2 == 0) return lit ? 'Y' : 'y';
            }

            // The small chips, low on the sides, turning away round the curve.
            float su = Mathf.Abs(u) - LumenSmallU, sy = y - LumenSmallY;
            if (Mathf.Abs(su) < LumenSmallHalf && Mathf.Abs(sy) < LumenSmallHalf * 0.8f)
                return fine && lit && su < -LumenSmallHalf + 0.3f ? 'm' : 'c';
            if (su < -LumenSmallHalf && su > -LumenSmallHalf - (fine ? 0.5f : 0.9f)
                && Mathf.Abs(sy) < LumenSmallHalf * 0.8f - 0.25f
                && (fine ? Mathf.Repeat(sy + 0.5f, 1f) < 0.5f : Mathf.RoundToInt(sy) % 2 == 0))
                return lit ? 'Y' : 'y';

            // The power, bright on the lit face, dimmer as it turns away.
            float w = fine ? 0.22f : 0.5f;
            float feed = LumenFeedDistance(u, y);
            if (feed < w) return shade ? 'i' : 't';

            if (fine)
            {
                if (feed < w + 0.3f && !shade) return 'j';       // the trace lights the mask

                // Silkscreen round the big chip.
                float ou = Mathf.Abs(cu) - (LumenChipHalfW + 0.85f), oy = Mathf.Abs(cy) - (LumenChipHalfH + 0.3f);
                float o = Mathf.Max(ou, oy);
                if (o > -0.12f && o < 0.02f) return 'w';

                // A soft specular stripe down the lit side - what sells the curve at this size.
                if (Mathf.Abs(nx + 0.55f) < 0.08f) return 'f';
            }

            // The blade's light spilling over the top, fading down.
            float spill = Mathf.Clamp01(1f - fromTop / 2.5f);
            if (spill > 0.55f) return 'j';
            if (spill > 0.15f) return 'z';

            return lit ? 'f' : shade ? 'e' : 'g';
        }

        /// <summary>
        /// The tail: a LIVE WIRE. Red insulation, then the copper stripped bare and frayed into
        /// splaying strands, with sparks jumping at their ends. Its own small sprite rather than
        /// part of the sword's grid, because it moves - see <see cref="Art.Gear.LumenRibbon"/>,
        /// which hangs it from the pommel's end and swings it on the cape's spring.
        ///
        /// DOUBLED like every other weapon sprite (<see cref="StageSprite"/>). It used to be built
        /// straight from its grid at the weapon density, so each texel was half the sword's: twelve
        /// rows came out a tenth of the character's height, tucked under the fist, and nobody saw it.
        ///
        /// Several frames, identical but for the sparks, for the component to flicker between at
        /// random - a wire that sparks on a beat reads as an animation; at random it reads as live.
        /// </summary>
        const int LumenWireRows = 15;            // insulated length
        const int LumenFrayRows = 6;             // bare copper, splaying
        const int LumenRibbonWidth = 11;
        const int LumenSparkFrames = 4;

        static string[] BuildLumenWire(int frame)
        {
            int rowsTotal = LumenWireRows + LumenFrayRows + 1;
            var grid = new char[rowsTotal][];
            for (int i = 0; i < rowsTotal; i++)
            {
                grid[i] = new char[LumenRibbonWidth];
                for (int x = 0; x < LumenRibbonWidth; x++) grid[i][x] = '.';
            }

            int center = LumenRibbonWidth / 2;
            int endX = center;
            for (int i = 0; i < LumenWireRows; i++)
            {
                // A gentle S so a hanging wire is not a ruler; the spring does the rest.
                float t = i / (float)(LumenWireRows - 1);
                int drift = Mathf.RoundToInt(Mathf.Sin(t * 3.0f) * 1.3f);
                int from = center - 1 + drift;
                grid[i][from] = '5'; grid[i][from + 1] = '4'; grid[i][from + 2] = '2';
                endX = from + 1;
            }

            // The frayed end: five strands leaving the insulation and splaying apart.
            float[] spread = { -1.6f, -0.8f, 0f, 0.8f, 1.6f };
            int[] reach = { LumenFrayRows - 1, LumenFrayRows, LumenFrayRows - 2, LumenFrayRows, LumenFrayRows - 1 };
            for (int k = 0; k < spread.Length; k++)
            {
                int last = -1, lastRow = -1;
                for (int j = 0; j < reach[k]; j++)
                {
                    int y = LumenWireRows + j;
                    int x = Mathf.Clamp(endX + Mathf.RoundToInt(spread[k] * (j + 1) / 2f), 0, LumenRibbonWidth - 1);
                    grid[y][x] = j == 0 ? 'p' : (k + j) % 3 == 0 ? 'p' : 'o';
                    last = x; lastRow = y;
                }
                // Sparks at the strand ends, different ones each frame; one frame in four is dark.
                bool sparks = frame % LumenSparkFrames != LumenSparkFrames - 1
                              && (k * 7 + frame * 3) % 5 < 2;
                if (sparks && last >= 0 && lastRow + 1 < rowsTotal)
                {
                    grid[lastRow + 1][last] = 'q';
                    if (last > 0 && grid[lastRow + 1][last - 1] == '.') grid[lastRow + 1][last - 1] = 'r';
                    if (last < LumenRibbonWidth - 1 && grid[lastRow + 1][last + 1] == '.') grid[lastRow + 1][last + 1] = 'r';
                }
            }

            var rows = new string[rowsTotal];
            for (int i = 0; i < rowsTotal; i++) rows[i] = new string(grid[i]);
            return rows;
        }

        /// <summary>Pivot at the TOP-CENTRE, where the wire leaves the pommel - a rotation about
        /// this point is all the spring needs.</summary>
        static readonly Vector2Int LumenRibbonPivot = new(LumenRibbonWidth / 2, 0);

        static Sprite[] _lumenWireFrames;

        /// <summary>The live wire's frames; the component flickers between them.</summary>
        public static Sprite[] LumenWireFrames
        {
            get
            {
                if (_lumenWireFrames != null) return _lumenWireFrames;
                var pal = LumenPal();
                var frames = new Sprite[LumenSparkFrames];
                for (int f = 0; f < frames.Length; f++)
                    frames[f] = StageSprite("gear.weapon.lumen.wire" + f, BuildLumenWire(f), pal, LumenRibbonPivot);
                return _lumenWireFrames = frames;
            }
        }

        // ============================================================== Obsidian

        /// <summary>
        /// Half-width of the straight shaft, in texels - width 9. Every other cosmetic
        /// greatsword here is this wide or close to it; Obsidian earns its distinct silhouette
        /// at the TIP instead, so the shaft stays an ordinary reference width.
        /// </summary>
        const int ObsidianShaftHalf = 4;

        /// <summary>Half-width at the flare's widest point - the horn tips. Width 25, which is
        /// the whole 28-column canvas bar a column and a half either side; the auto-outline pads
        /// its own room on top of that, so nothing clips.</summary>
        const int ObsidianMaxFlareHalf = 12;

        /// <summary>Half-width at the very top row - the dome's own small flat apex, where the
        /// reference's centre ridge runs out through the top edge. Not zero: a one-texel spike
        /// at the top of a 25-wide head reads as a burr rather than as the crown of an arc.
        /// </summary>
        const int ObsidianApexHalf = 3;

        /// <summary>The row the flare reaches its widest - the horn tips, and the seam between
        /// the top arc and the underside scoop. About a third of the way down the head, which is
        /// where the reference's own tips sit.</summary>
        const int ObsidianTipRow = 4;

        /// <summary>How sharply the underside falls away from the tips. Above 1 the scoop drops
        /// fast at the tip and flattens into the shaft, which is the concave sweep that makes a
        /// horn a horn; at 1 it is a straight taper and the head reads as a triangle.
        ///
        /// 3.0, not 1.6 - measured against the render rather than reasoned about. At 1.6 the
        /// underside was still close enough to a straight diagonal that the head read as a
        /// TRUMPET or a funnel: the horns had no undercut to hang off. At 3 the boundary hugs
        /// the shaft for most of the scoop and then flares out hard in the last two rows before
        /// the tip, which is what actually reads as material scooped out from beneath a horn.
        /// </summary>
        const float ObsidianUndersidePower = 3.0f;

        /// <summary>Glass rim thickness per edge, in texels. Thin on purpose - "primarily black"
        /// means black has to dominate every span, with the glass read as a rim rather than a
        /// band the way Lumen's own edge (7 texels, "thick not thin" by explicit request on a
        /// weapon that IS the energy) is.</summary>
        const int ObsidianEdgeWidth = 2;

        // 12 + 42 + 3 + 14 + 9 = 80 logical rows AND 80 texels of texture, because this piece
        // takes no outline and therefore gets none of the padding an outlined piece does - the
        // four rows that padding used to contribute are carried by the shaft instead, exactly
        // as Lumen's own note describes for the same reason. The total is what is fixed, never
        // any one section: the head has been re-cut twice against the reference and paid for
        // both out of the shaft.
        //
        // THE HEAD IS WIDE, NOT TALL, AND GETTING THAT RATIO WRONG IS WHAT MADE THE SECOND PASS
        // READ AS A KITE. At 20 rows against 25 columns the head was very nearly square, so
        // however well the two edge curves were tuned it came out as a diamond on a stick. The
        // reference's own head measures about 0.4 as tall as it is wide; 12 rows against 25
        // columns is 0.48, close enough at this density, and the eight rows it gave up went
        // straight into the shaft, which the reference also wants long.
        const int ObsidianWingRows = 12;

        /// <summary>
        /// The plain shaft, settled by the family's accounting less the outline this weapon does
        /// not carry - see <see cref="BladeRowsForNoOutline"/>. It was 42, hand-counted against a
        /// grid that was never being doubled and so came out half the height of every other sword.
        /// </summary>
        static readonly int ObsidianShaftRows = BladeRowsForNoOutline(
            ObsidianWingRows + ObsidianCollarRows + ObsidianGripRows + ObsidianPommelRows);
        const int ObsidianCollarRows = 0;          // none: the strands cross right on the grip
        // The family's own grip and a pommel the family's size. They were 15 and 9 rows - a
        // handle and a knob as long as a third of the sword, out of line with every other hilt
        // here - and the fourteen rows they gave back went into the SHAFT, the length the
        // reference wants anyway.
        const int ObsidianGripRows = SwordGripRows;
        const int ObsidianPommelRows = 6;          // the ring pommel

        /// <summary>
        /// Fills columns [from, to) of a 28-wide row: <see cref="ObsidianEdgeWidth"/> texels of
        /// GLASS at each true edge (uppercase, alpha-written below), OPAQUE BLACK everywhere
        /// else (lowercase). <paramref name="ridgeCol"/> is the one column, if any, that takes
        /// the black ramp's own lit tone instead of its base - the raised centre ridge the
        /// reference photo carries down the full length of the shaft. It is BLACK, not glass:
        /// the brief asks for glass at the edges specifically, so the ridge stays inside the
        /// body material rather than quietly becoming a second glass feature.
        /// </summary>
        /// <summary>
        /// Half-width of the blade at a given row, counting from the very top of the head.
        /// Past the head it is simply the shaft's own half-width, so the two hand off with no
        /// seam - the underside curve is built to land exactly on it at its last row.
        ///
        /// THERE IS NO NOTCH, AND THE FIRST BUILD'S WAS THE WHOLE ERROR. That version carved a
        /// gap out of the top centre - two prongs diverging upward - which reads as a heart or a
        /// tuning fork and is nothing like the reference. The reference's head is SOLID: one
        /// continuous convex arc across the top, apex at centre, sweeping down and out to a horn
        /// tip at each end, with the material scooped away UNDERNEATH each horn instead. Both
        /// edges of a horn therefore run inward from its tip - the top arc up-and-in, the
        /// underside down-and-in - so no row is ever split.
        ///
        /// The two curves that remain are the two EDGES of that one solid shape:
        ///
        ///   TOP (rows 0..TipRow)      a flattened ellipse - flat at the apex, so it widens
        ///                             fastest in the first rows and slowest as it reaches
        ///                             the tips, which is what a shallow dome actually does.
        ///   UNDERSIDE (below TipRow)  a concave scoop that falls away from the tip FAST and
        ///                             then flattens into the shaft. That asymmetry is what
        ///                             makes the tips read as horns; a straight taper down from
        ///                             the tips reads as the corners of a triangle instead.
        /// </summary>
        // ---- the head and shaft as ONE FIELD
        //
        // The head was a stack of rounded row widths filled with a grid distance - fine as a
        // silhouette, but its glass rim was measured in whole cells (a chessboard distance), so
        // the band went lumpy round the horns, and the menu art was that grid blown up. Now the
        // outline is a continuous half-width, the rim is a true distance from it (so the band is
        // one width all the way round a curve), and the arena and the menu art both sample it.
        // Same silhouette as before: a broad shallow dome flaring to sharp horns at the widest
        // row, then a hard concave sweep into the shaft.

        const float ObsidianAxis = 14f;                  // this grid centres on a COLUMN
        const float ObsidianHornY = 4f;                  // the widest row: the horns' tips

        /// <summary>The blade's half-width at row <paramref name="y"/>, in cells, to its EDGE.</summary>
        static float ObsidianHalf(float y)
        {
            float shaft = ObsidianShaftHalf + 0.5f, max = ObsidianMaxFlareHalf + 0.5f;
            float wingEnd = ObsidianWingRows - 0.5f;
            if (y >= wingEnd) return shaft;
            if (y <= ObsidianHornY)
            {
                // The dome: broad and shallow, reaching full width only right at the horn row.
                float k = (ObsidianHornY - y) / (ObsidianHornY + 0.5f);
                float arc = Mathf.Pow(Mathf.Max(0f, 1f - k * k), 0.75f);
                return Mathf.Max(ObsidianApexHalf + 0.5f, max * arc);
            }
            // The underside: a hard concave sweep from the horn tip into the shaft.
            float u = (y - ObsidianHornY) / (wingEnd - ObsidianHornY);
            return Mathf.Lerp(shaft, max, Mathf.Pow(1f - u, ObsidianUndersidePower));
        }

        /// <summary>Distance inside the blade's outline, in cells - a true one, corrected for the
        /// outline's slope, so the rim is even round the horns.</summary>
        static float ObsidianDepth(float x, float y)
        {
            float half = ObsidianHalf(y);
            float slope = (ObsidianHalf(y + 0.1f) - ObsidianHalf(y - 0.1f)) / 0.2f;
            float side = (half - Mathf.Abs(x - ObsidianAxis)) / Mathf.Sqrt(1f + slope * slope);
            return Mathf.Min(side, y + 0.5f);                       // and the top edge
        }

        static char ObsidianBladeTexel(float x, float y, bool fine)
        {
            float bladeEnd = ObsidianWingRows + ObsidianShaftRows - 0.5f;
            if (y < -0.5f || y > bladeEnd) return '.';
            float depth = ObsidianDepth(x, y);
            if (depth < 0f) return '.';

            float dx = x - ObsidianAxis;
            // The glass rim: its lit crest where the edge faces up and left, then the band.
            if (depth < (fine ? 0.45f : 1f))
            {
                if (fine && depth < 0.22f && (y < ObsidianHornY + 0.5f || dx < 0f)) return 'H';
                return 'L';
            }
            // The band is the arena's own width in the menu too, or the glass reads thinner there.
            if (depth < ObsidianEdgeWidth) return 'B';

            // The fuller: a hairline down the centre, one texel in the arena.
            if (fine ? Mathf.Abs(dx) < 0.18f : Mathf.Abs(dx) < 0.5f) return 's';

            // An ENGRAVED line following the head's outline a little way in - the incised border
            // these blades carry. Menu only: at cell size it would be the fuller's twin.
            if (fine && y < ObsidianWingRows - 1.5f && Mathf.Abs(depth - 2.9f) < 0.13f) return 's';
            return 'b';
        }

        static void AppendObsidianBlade(List<string> rows)
        {
            int bladeRows = ObsidianWingRows + ObsidianShaftRows;
            for (int y = 0; y < bladeRows; y++)
            {
                var line = ObsidianBlankRow();
                for (int x = 0; x < 28; x++) line[x] = ObsidianBladeTexel(x, y, fine: false);
                rows.Add(new string(line));
            }
        }

        /// <summary>
        /// Menu art at TRUE <see cref="MenuPpu"/>: the blade from the same field at four samples a
        /// cell, and the collar, grip and knob drawn properly rather than blown up from the arena
        /// rows (the derived pass turned them into stacked blocks).
        /// </summary>
        static string[] BuildObsidianSwordDetail()
        {
            const int k = EmberDetailScale;
            var rows = new string[ObsidianRows.Length * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[28 * k];
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++)
                {
                    float x = (X + 0.5f) / k - 0.5f;
                    char h = y > ObsidianHiltTop - ObsidianStrandRise - 2f ? ObsidianHiltTexel(x, y, fine: true) : '.';
                    line[X] = h != '.' ? h : ObsidianBladeTexel(x, y, fine: true);
                }
                rows[Y] = new string(line);
            }
            return rows;
        }

        static char[] ObsidianBlankRow()
        {
            var line = new char[28];
            for (int x = 0; x < 28; x++) line[x] = '.';
            return line;
        }

        /// <summary>A plain flat band, the same centred-odd-width idiom every hilt row in this
        /// file already uses (see <c>LumenFlatRow</c>).</summary>
        static string ObsidianFlatRow(int width, char left, char mid, char right)
        {
            var line = ObsidianBlankRow();
            int from = 14 - width / 2;
            for (int x = 0; x < width; x++)
                line[from + x] = x == 0 ? left : x == width - 1 ? right : mid;
            return new string(line);
        }

        static string[] BuildObsidianSword()
        {
            var rows = new List<string>();
            AppendObsidianBlade(rows);
            int total = rows.Count + ObsidianCollarRows + ObsidianGripRows + ObsidianPommelRows;
            while (rows.Count < total) rows.Add(new string(ObsidianBlankRow()));

            // The hilt, laid over the lot - the langet reaches up onto the blade.
            for (int y = Mathf.Max(0, Mathf.FloorToInt(ObsidianHiltTop - ObsidianStrandRise) - 2); y < rows.Count; y++)
            {
                var line = rows[y].ToCharArray();
                for (int x = 0; x < line.Length; x++)
                {
                    char c = ObsidianHiltTexel(x, y, fine: false);
                    if (c != '.') line[x] = c;
                }
                rows[y] = new string(line);
            }
            return rows.ToArray();
        }

        // ---- the hilt: BLACKENED STEEL, the "elvish" design
        //
        // After the user's reference sheet (#3): two swept ARMS curving out and back toward the
        // grip, tips pointed; two STRANDS rising from the guard up the blade, crossing once and
        // opening into a narrow loop that closes at a point (the interlace); a cloth-wrapped grip;
        // a RING pommel with a slotted bar through it. DARK TONES ONLY - blackened steel, dull
        // gunmetal at the brightest - so the whole weapon reads as a blackened blade. It takes no
        // auto-outline, so the lit edges are what hold the hilt's shape against a dark floor.
        // (An ornate swept-and-lobed guard was built first and dropped: too close to Phantom's.)

        static float ObsidianHiltTop => ObsidianWingRows + ObsidianShaftRows - 0.5f;
        static float ObsidianGripTopY => ObsidianHiltTop + ObsidianCollarRows;
        static float ObsidianPommelTopY => ObsidianGripTopY + ObsidianGripRows;

        // THE GUARD IS THE TWO STRANDS, as the reference draws it - not a crossbar with
        // decoration on it. Each strand starts at a pointed ARM TIP out beside the grip, sweeps in
        // and up, CROSSES the other at the blade's base, then runs on up the blade as one side of
        // a long pointed LOOP about the blade's own width, the two meeting at its point. (A first
        // pass drew a separate crescent guard with thin strands above it; that is not the sketch.)
        static float ObsidianCrossY => ObsidianGripTopY - 0.1f;           // the blade's base
        const float ObsidianArmDrop = 3.2f;                                // arm tips, below the cross
        const float ObsidianArmReach = 7.6f;
        const float ObsidianLoopRise = 12.5f;                              // the loop's point, above it
        const float ObsidianLoopHalf = 3.4f;
        const float ObsidianStrandRise = ObsidianLoopRise;                 // how far up the hilt reaches

        /// <summary>Where the strand that starts on the LEFT arm is at row <paramref name="y"/>:
        /// out on the left below the cross, over on the right in the loop above it.</summary>
        static float ObsidianStrandX(float y)
        {
            float c = ObsidianCrossY;
            if (y >= c)
            {
                // Out nearly level first, then turning back toward the pommel at the tip - the
                // sketch's arms, rather than a straight diagonal.
                float t = Mathf.Clamp01((y - c) / ObsidianArmDrop);
                return -ObsidianArmReach * Mathf.Pow(t, 0.5f);
            }
            float u = Mathf.Clamp01((c - y) / ObsidianLoopRise);
            return ObsidianLoopHalf * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.85f);
        }

        /// <summary>The strand's half-thickness: broad through the arms near the cross, tapering
        /// to a point at the arm tip and thinning toward the loop's point.</summary>
        static float ObsidianStrandTh(float y, bool fine)
        {
            float c = ObsidianCrossY, k = fine ? 1f : 1.25f;
            if (y >= c) return k * Mathf.Lerp(1.15f, 0.2f, Mathf.Clamp01((y - c) / ObsidianArmDrop));
            return k * Mathf.Lerp(0.75f, 0.42f, Mathf.Clamp01((c - y) / ObsidianLoopRise));
        }

        /// <summary>Distance to one strand (side -1 is the left-arm strand, +1 its mirror).</summary>
        static float ObsidianStrandSd(float x, float y, float side, float extra, bool fine)
        {
            float c = ObsidianCrossY;
            if (y > c + ObsidianArmDrop + 0.2f || y < c - ObsidianLoopRise - 0.2f) return 99f;
            float cx = ObsidianAxis + (side < 0f ? ObsidianStrandX(y) : -ObsidianStrandX(y));
            float dxdy = (ObsidianStrandX(y + 0.05f) - ObsidianStrandX(y - 0.05f)) / 0.1f;
            return Mathf.Abs(x - cx) / Mathf.Sqrt(1f + dxdy * dxdy) - (ObsidianStrandTh(y, fine) + extra);
        }

        /// <summary>The strands as one field, for shading: the left-arm strand passes OVER.</summary>
        static float ObsidianStrandsSd(float x, float y)
            => Mathf.Min(ObsidianStrandSd(x, y, -1f, 0f, true), ObsidianStrandSd(x, y, +1f, 0f, true));

        /// <summary>Signed distance to the pommel: a tall oval ring, a smaller oval ring inside
        /// it, and a neck joining it to the grip.</summary>
        static float ObsidianPommelSd(float x, float y) => ObsidianPommelSd(x, y, 0.55f);

        static float ObsidianPommelSd(float x, float y, float band)
        {
            float ax = Mathf.Abs(x - ObsidianAxis);
            float cy = ObsidianPommelTopY + 3.1f;
            float ex = ax / 2.4f, ey = (y - cy) / 2.9f;
            float outer = Mathf.Abs(Mathf.Sqrt(ex * ex + ey * ey) - 0.8f) * 2.4f - band;
            float ix = ax / 1.0f, iy = (y - cy) / 1.9f;
            float inner = Mathf.Abs(Mathf.Sqrt(ix * ix + iy * iy) - 0.72f) * 1.0f - 0.3f;
            float neck = Mathf.Max(ax - 1.4f, Mathf.Max(ObsidianPommelTopY - 0.2f - y, y - (ObsidianPommelTopY + 0.5f)));
            return Mathf.Min(Mathf.Min(outer, inner), neck);
        }

        static char ObsidianHiltTexel(float x, float y, bool fine)
        {
            float sx = x - ObsidianAxis, ax = Mathf.Abs(sx);

            // The strands - guard and interlace in one. The left-arm strand passes OVER the other
            // at the crossing, the one underneath broken either side of it.
            float a = ObsidianStrandSd(x, y, -1f, 0f, fine);
            if (a < 0f) return ObsidianBlackened(x, y, a, (px, py) => ObsidianStrandSd(px, py, -1f, 0f, fine), fine);
            float b = ObsidianStrandSd(x, y, +1f, 0f, fine);
            if (b < 0f)
            {
                if (ObsidianStrandSd(x, y, -1f, fine ? 0.3f : 0.45f, fine) < 0f) return '.';
                return ObsidianBlackened(x, y, b, (px, py) => ObsidianStrandSd(px, py, +1f, 0f, fine), fine);
            }

            // The ring is banded thicker in the arena: at a sample a cell the menu's band broke.
            float band = fine ? 0.55f : 0.8f;
            float p = ObsidianPommelSd(x, y, band);
            if (p < 0f) return ObsidianBlackened(x, y, p, (px, py) => ObsidianPommelSd(px, py, band), fine);

            // The grip: a spiral CLOTH wrap, with a metal ferrule at each end.
            float gTop = ObsidianGripTopY, gBot = ObsidianPommelTopY;
            if (y > gTop && y < gBot && ax < SwordGripWidth / 2f + 0.5f)   // odd grid: the family's width plus one
            {
                if (y < gTop + (fine ? 0.55f : 1f) || y > gBot - (fine ? 0.55f : 1f))
                    return sx < -1.5f ? 'o' : sx > 1.5f ? 'n' : 'm';
                // A WRAP, not an engraving: broad bands of cloth in ONE tone, separated only by
                // the narrow dark line where each turn overlaps the next. Thin dark grooves on a
                // shaded body (the first version) read as lines cut into a solid handle.
                // The gaps have to be WIDE enough to show the grip beneath them, or the strips
                // read as lines drawn on a flat handle - a wrap is strips with space between.
                float w = (y + sx * 0.55f) / 2f;
                float fr = w - Mathf.Floor(w);
                return fr < 0.38f ? '1' : '4';
            }
            return '.';
        }

        /// <summary>Blackened steel shaded by the surface normal: dull gunmetal on the edges that
        /// face the light, near-black in the recesses, the body in between.</summary>
        static char ObsidianBlackened(float x, float y, float sd, System.Func<float, float, float> field, bool fine)
        {
            const float e = 0.3f;
            float nx = field(x + e, y) - field(x - e, y);
            float ny = field(x, y + e) - field(x, y - e);
            float nl = Mathf.Sqrt(nx * nx + ny * ny);
            if (nl > 1e-4f) { nx /= nl; ny /= nl; }
            float lit = nx * -0.55f + ny * -0.83f;

            // ARENA: a SOLID dark-metal shape - gunmetal body, the lit edge a step lighter, the
            // shaded one a step darker. Shaded the menu's way (a rim round a near-black body),
            // pieces a cell or two wide came out as loose light edges with holes where their
            // bodies sank into the floor, since this weapon has no outline to hold them.
            if (!fine)
            {
                if (-sd < 0.7f && lit > 0.35f) return 'p';
                return lit < -0.45f ? 'm' : 'o';
            }

            if (-sd < 0.35f)
            {
                // With no outline the rim is the silhouette: even the shaded edges get a
                // lighter line than the body, just dimmer than the lit ones.
                if (lit > 0.35f) return 'p';
                if (lit > -0.3f) return 'o';
                return 'q';
            }
            return lit < -0.3f ? 'n' : 'm';
        }

        static readonly string[] ObsidianRows = BuildObsidianSword();
        static readonly string[] ObsidianSwordDetail = BuildObsidianSwordDetail();

        /// <summary>Inside the grip, a few rows below the collar - the same "pivot sits near the
        /// top of the grip band" convention every other greatsword's own grip constant uses.</summary>
        /// <summary>The grip's second row, like every other greatsword's pivot - it sat five rows
        /// in while the grip was fifteen long.</summary>
        static readonly Vector2Int ObsidianGrip = new(14,
            GripCentre(ObsidianWingRows + ObsidianShaftRows + ObsidianCollarRows, ObsidianGripRows));
        static Vector2Int ObsidianDetailGrip => new(ObsidianGrip.x * EmberDetailScale, ObsidianGrip.y * EmberDetailScale);

        /// <summary>
        /// Alpha weighting for the EDGE (uppercase) - the exact numbers <c>ShadowAlphas</c>
        /// already uses, pointed at the opposite half of the object. Shadow alphas its whole
        /// blade (lowercase) and keeps its furniture (uppercase) solid; this keeps the whole
        /// BODY (lowercase, black) solid and alphas only the EDGE (uppercase, the same black)
        /// instead - not a tinted glass, black glass, the way Shadow's own blade is see-through
        /// rather than coloured.
        /// </summary>
        static readonly (char C, float A)[] ObsidianGlassAlphas =
        {
            ('H', 0.92f),
            ('L', 0.82f),
            ('B', 0.55f),
            ('D', 0.45f),
            ('S', 0.40f),
            ('K', 0.85f),
        };

        static Dictionary<char, Color> ObsidianPal()
        {
            // THE BODY IS GUNMETAL, NOT BLACK - and that is what finally lets the blade have any
            // interior at all. While the body was itself near-black there was nowhere for a
            // fuller to sit: the ramp's own deepest tone measured a hundredth away from its base
            // and the whole interior read as one flat mass. Gunmetal keeps the weapon reading as
            // dark and cold while giving the fuller somewhere to be darker THAN.
            // A RICH BLACK - "just light enough to see the fuller", which is the whole
            // constraint and the reason this is not simply (0,0,0). It went black -> gunmetal ->
            // back to black across three passes and the lesson each time was the same: the body
            // cannot be at the bottom of its own ramp, because the fuller has to be BELOW it.
            // At 0.125 the body's own Deep tone (its darkest, and what the fuller is cut in)
            // lands near 0.058 - a bit over half - which is enough separation for a one-texel
            // groove to read, and no more black than that anywhere on the blade.
            var richBlack = new Palette.Ramp(new Color(0.125f, 0.125f, 0.145f));

            // ONE BLACK RAMP, USED TWICE AND ALPHA'D ONCE. The uppercase copy is the EDGE: black
            // glass, see-through exactly the way Shadow's own blade is, not a tinted colour
            // standing in for glass. The digit copy is the HANDLE and the FULLER - the same
            // black, left solid, because a groove is a cut in the steel and a grip you can see
            // through leaves the character holding nothing.
            //
            // TINTED BLACK GLASS, NOT A BRIGHT RIM. Two things hold it there, because the ramp
            // alone will not: the LIFT is cut to 0.16, and the edge is emitted on Light/Base
            // rather than on Glow/Light (see AppendObsidianBlade). A ramp's Glow is its base
            // lerped most of the way to white, so on Glow the edge came back at roughly three
            // times the body's value and read as chrome piping round a black sword - which is
            // the opposite of glass with black in it. On Light it lands about twice the body,
            // which is dark enough to read as smoked glass and light enough to still be an edge.
            var glass = new Palette.Ramp(new Color(0.17f, 0.17f, 0.21f), lift: 0.16f);

            // The handle is black too, a hair LIGHTER than the blade rather than equal to it, so
            // the grip still reads as its own part instead of merging into the blade where the
            // two meet. Its rows stay in the dark end of this ramp for a related reason: a
            // ramp's Light tone on a black base is a mid grey, so the obvious lit-band grip
            // would have put a silver handle on a black sword.
            var handle = new Palette.Ramp(new Color(0.165f, 0.16f, 0.175f));

            var map = Palette.Of(richBlack, glass, handle);

            // The hilt: BLACKENED STEEL, dark tones only - gunmetal at the brightest, on the lit
            // edges, never anything that reads as silver or gold.
            map['p'] = new Color(0.40f, 0.40f, 0.45f);          // lit edge
            map['o'] = new Color(0.24f, 0.24f, 0.28f);          // edge, facet
            map['m'] = new Color(0.13f, 0.13f, 0.15f);          // the body
            map['n'] = new Color(0.075f, 0.075f, 0.09f);        // recess, filigree
            map['q'] = new Color(0.035f, 0.035f, 0.045f);       // deepest cut

            // Alpha on the UPPERCASE set only - the edge - which is Shadow's own technique
            // pointed at the opposite half of the object. ShadowAlphas alphas the whole blade
            // and keeps its furniture solid; this alphas only the rim and keeps everything the
            // rim encloses solid, with the identical lit-tones-near-solid curve either way.
            foreach (var (c, a) in ObsidianGlassAlphas)
            {
                var col = map[c];
                col.a = a;
                map[c] = col;
            }
            return map;
        }

        // ============================================================== the Rift Blade





        /// <summary>
        /// The Rift Blade's palette: dark metal, RIFT LIGHT, leather.
        ///
        /// THE SECOND RAMP IS THE LIGHT, NOT A TRIM. Every other weapon here spends its uppercase
        /// set on furniture - a guard, a collar, a wrap. This one spends it on what is coming
        /// through the tear, because that is the object: a blade with somewhere else inside it.
        /// The grip takes the digits, the way Shadow's does, for exactly the same reason - the
        /// alphabet is out of cases.
        ///
        /// The metal is deliberately dim. A bright blade and a bright tear compete, and the tear
        /// has to be the thing the eye lands on - so the steel is dark enough to be a frame.
        /// Cyan for the light rather than violet, and violet only in the glow, because that is the
        /// Rift's own arrangement: a cyan-lit rim around a violet interior (see Rifts.Rift).
        /// </summary>
        static Dictionary<char, Color> RiftPal()
        {
            var steel = new Palette.Ramp(new Color(0.19f, 0.20f, 0.26f));
            // lift 0.20, NOT 0.55. Ramp derives Glow as lerp(base, white, lift + 0.30), so at 0.55
            // the tear's edge came out 85% of the way to white - bright, and no longer cyan. At
            // this size that loses the one thing tying the blade to the Rift and the boxes, and it
            // could be any glowing sword. 0.20 keeps the edge unmistakably cyan and still reads as
            // the brightest thing on the object.
            var light = new Palette.Ramp(new Color(0.45f, 0.88f, 1.00f), lift: 0.20f, shade: 0.42f);
            var map = Palette.Of(steel, light);

            // The hilt is the SIGIL DOOR's stone, to the colour - SigilDoor's Frame, CutShade,
            // StoneDeep, Stone and CutLip, with one step between Stone and the lip for the rims.
            // Digits, as a grid's third material always takes.
            map['1'] = new Color(0.13f, 0.135f, 0.17f);
            map['2'] = new Color(0.115f, 0.12f, 0.15f);
            map['3'] = new Color(0.19f, 0.20f, 0.24f);
            map['4'] = new Color(0.30f, 0.31f, 0.36f);
            map['5'] = new Color(0.38f, 0.39f, 0.45f);
            map['6'] = new Color(0.46f, 0.47f, 0.53f);

            // The grip is wood, as the reference's is: a dark, cool-leaning stain, so it sits with
            // the stone and the dark steel rather than warming the whole hilt.
            map['u'] = new Color(0.14f, 0.10f, 0.09f);
            map['v'] = new Color(0.22f, 0.16f, 0.13f);
            map['w'] = new Color(0.31f, 0.23f, 0.18f);
            map['x'] = new Color(0.40f, 0.30f, 0.23f);

            // The interior of the tear runs VIOLET while its edges stay cyan - the same two-colour
            // arrangement the fixture uses, and the reason a rift reads as a light source rather
            // than as a painted slot. Overwritten after Of() rather than given its own ramp
            // because only two of the six tones want it.
            map['B'] = new Color(0.62f, 0.42f, 0.95f);
            map['D'] = new Color(0.42f, 0.26f, 0.72f);

            // THE TEAR IS PARTLY NOT THERE - the same alpha treatment Shadow uses, applied to the
            // exact opposite half of the object, and the inversion is the point.
            //
            //     Shadow      the BLADE is semi-transparent and the furniture is solid: the sword
            //                 itself is the thing that is half absent.
            //     Rift Blade  the STEEL is solid and the TEAR is semi-transparent: the sword is
            //                 entirely present and the hole in it is not.
            //
            // Without this the tear is painted light - a violet stripe on a blade. With it the
            // arena shows through, faintly, tinted by the light escaping round the edges, and it
            // reads as an opening rather than as a decal. That is the whole idea the fixture and
            // the boxes share: a rift is somewhere else showing through.
            //
            // WEIGHTED TO THE EDGE, exactly as ShadowAlphas is and for the same reason: the cyan
            // rim is what holds the shape, so it stays nearly solid while the middle thins out.
            // A flat alpha across the whole tear makes the rim as absent as the centre and the
            // opening loses its outline against a busy background.
            foreach (var (c, a) in RiftTearAlphas)
            {
                var col = map[c];
                col.a = a;
                map[c] = col;
            }
            return map;
        }

        /// <summary>Opacity of the tear's own tones. The steel is untouched and stays solid.</summary>
        static readonly (char C, float A)[] RiftTearAlphas =
        {
            ('H', 0.90f),   // the cyan rim - nearly solid, and the whole opening rests on it
            ('B', 0.62f),
            ('D', 0.46f),   // the deepest violet is the thinnest: furthest through
        };

        /// <summary>
        /// Half-width of the TEAR at a given blade row, in texels.
        ///
        /// A TEAR, NOT A FULLER, and the difference is the whole weapon. Shadow's groove is a
        /// smooth lens with a clean edge - a machined slot. This one wobbles, because a rip is
        /// something that was pulled apart rather than cut. The wobble is three sine harmonics at
        /// incommensurable frequencies, exactly as `Spr.Tear` does it and for the same reason:
        /// summed harmonics never repeat over the length, where noise at this scale clusters
        /// around its midpoint and comes out as a gentle bulge.
        ///
        /// Same lens envelope as Shadow underneath, so it still closes before the tip and before
        /// the ricasso - a constant-width channel cuts the sword into two blades held side by side.
        /// </summary>
        /// <summary>
        /// Where the tear's LEFT and RIGHT edges sit at a given row - and they are computed
        /// SEPARATELY, which is the fix that made this read as a rip at all.
        ///
        /// The first pass mirrored one half-width about the centre line, so every bulge appeared
        /// on both sides at once and the tear came out as a row of symmetrical lozenges - a chain
        /// of vertebrae down the blade rather than something torn. Nothing about a rip is
        /// symmetric: the two edges came apart from each other and have no reason to agree.
        ///
        /// The harmonics are also spaced further from each other than the first pass's were. At
        /// 0.71 / 1.53 / 2.87 the periods very nearly lined up over 52 rows and the wobble
        /// repeated, which is what made the bulges look placed. These do not resolve over the
        /// blade's length.
        /// </summary>
        static (float L, float R) RiftTearEdges(float y)
        {
            float t = Mathf.Clamp01((y - 6f) / (RiftBladeRows - 1f));

            // The lens envelope, shared: the tear still has to close before the tip and before the
            // ricasso, or the sword reads as two blades held side by side.
            // Clamped at zero: sin(PI) is a hair NEGATIVE in floats, and Pow of that is NaN - which
            // fails every edge comparison open and fills the whole row past the tear's end.
            float lens = 3.6f * SwordBladeScale * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.32f);

            float left  = 1f + 0.30f * Mathf.Sin(y * 0.37f + 1.1f)
                             + 0.17f * Mathf.Sin(y * 0.94f + 3.7f)
                             + 0.09f * Mathf.Sin(y * 2.31f + 0.4f);
            float right = 1f + 0.30f * Mathf.Sin(y * 0.43f + 5.2f)
                             + 0.17f * Mathf.Sin(y * 1.09f + 1.9f)
                             + 0.09f * Mathf.Sin(y * 2.03f + 4.6f);

            // Capped, not just scaled. The wobble multiplies the lens, so the widest rows push
            // half as far again as the envelope alone - fine against eighteen cells, and enough to
            // swallow a twelve-cell blade whole. The flats are served first, exactly as Shadow's
            // are, and the rip gets the middle.
            float cap = SwordBladeWidth * 0.5f - RiftMinFlat;
            return (Mathf.Min(cap, lens * Mathf.Max(0.30f, left)),
                    Mathf.Min(cap, lens * Mathf.Max(0.30f, right)));
        }

        /// <summary>Cells of flat the tear may never reach, either side - before the outline and
        /// before <see cref="RiftEdgeInset"/> take their own bites out of what is left.</summary>
        const float RiftMinFlat = 3.5f;

        /// <summary>
        /// Lay a patch of the tear's light across a row's opaque run, centred at
        /// <paramref name="at"/> along it (0 at the left end, 1 at the right) and
        /// <paramref name="cells"/> wide.
        ///
        /// Measured against the RUN rather than against fixed columns. The patches used to be
        /// typed into the row as literal texels, which is fine until the guard they sit on
        /// narrows - then they bunch at one end of it, or hang off it entirely.
        /// </summary>
        static string Glint(string row, float at, int cells)
        {
            int lo = -1, hi = -1;
            for (int x = 0; x < row.Length; x++)
                if (row[x] != '.') { if (lo < 0) lo = x; hi = x; }
            if (lo < 0 || cells <= 0) return row;

            var line = row.ToCharArray();
            int start = Mathf.RoundToInt(Mathf.Lerp(lo, hi - cells + 1, Mathf.Clamp01(at)));
            for (int i = 0; i < cells; i++)
            {
                int x = start + i;
                if (x >= lo && x <= hi) line[x] = 'H';
            }
            return new string(line);
        }

        /// <summary>
        /// Where the blade's OUTER edge sits at a given row - and it is not constant.
        ///
        /// THE RIP GOES THROUGH THE WHOLE THING. Where the tear is at its widest the outer edges
        /// are bitten inward, so the silhouette itself is irregular. That is what makes this a
        /// different WEAPON from Shadow rather than a recolour of it: the class rule is that two
        /// blades must differ by outline, not by ramp, and a fuller of a different shape is not an
        /// outline. Seen as a black shape on a white page, this one has a chewed edge and Shadow
        /// does not.
        ///
        /// Bounded so the flats never fall under two texels - past that the blade stops reading as
        /// a blade and starts reading as damage.
        /// </summary>
        static int RiftEdgeInset(float half)
        {
            // DRIVEN BY THE TEAR'S OWN WIDTH on that side rather than by a separate wave, so the
            // outline is bitten exactly where the rip is pushing hardest against it. Two
            // independent wobbles would have put notches where nothing was happening, which reads
            // as a damaged blade instead of a torn one.
            //
            // Scaled with the blade: these are texel distances measured against the eighteen-cell
            // grid, and left alone on a narrower one nothing ever reaches them and the outline
            // comes out clean - which is the one thing this weapon cannot be.
            return half > 3.9f * SwordBladeScale ? 2 : half > 3.3f * SwordBladeScale ? 1 : 0;
        }

        /// <summary>
        /// A greatsword with a tear down it - the third black-diamond blade, and deliberately
        /// nothing in common with the other two beyond the class.
        ///
        ///     Emberline   opaque, hot, five stages, a GLASS fuller
        ///     Shadow      semi-transparent, black, an EMPTY fuller you see the arena through
        ///     Rift Blade  dark steel, a RAGGED tear that is LIT from inside, and a chewed outline
        ///
        /// The tear is FILLED rather than empty, which is the one place it deliberately parts
        /// company with the fixture it is named for: a rift you look through shows the hub, and a
        /// sprite cannot show a live view - so what comes through here is the light rather than the
        /// place. An empty tear would also have been Shadow's idea a second time.
        /// </summary>
        static string[] BuildRiftSword()
        {
            // The family's shared point, then the field one sample per cell.
            var tip = SwordTipRows("lbd");
            var rows = new string[SwordHeightRows];
            for (int y = 0; y < rows.Length; y++)
            {
                if (y < tip.Length) { rows[y] = tip[y]; continue; }
                var line = new char[SwordCanvas];
                for (int x = 0; x < SwordCanvas; x++) line[x] = RiftTexel(x, y, fine: false);
                rows[y] = new string(line);
            }
            return rows;
        }

        /// <summary>
        /// Menu art at TRUE <see cref="MenuPpu"/>, four samples per arena cell - never pass it
        /// upscale2x. The SAME field the arena samples once per cell, so the silhouette cannot
        /// drift between them; what the extra samples buy is a smooth tear, one-texel grooves in
        /// the stone and grain in the wood rather than new features.
        /// </summary>
        static string[] BuildRiftSwordDetail()
        {
            const int k = EmberDetailScale;
            var rows = new string[SwordHeightRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[SwordCanvas * k];
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++)
                    line[X] = RiftTexel((X + 0.5f) / k - 0.5f, y, fine: true);
                rows[Y] = new string(line);
            }
            return rows;
        }

        /// <summary>
        /// The whole Rift Blade as one field in authored arena cells: the hilt over the blade,
        /// and the blade running on UNDER the guard to its flat edge, so the dome's shoulders have
        /// steel behind them where they curve away from it. The tear has closed by then.
        /// </summary>
        static char RiftTexel(float x, float y, bool fine)
        {
            char hilt = RiftHiltTexel(x, y, fine);
            if (hilt != '.') return hilt;
            if (y > RiftGuardBase) return '.';
            if (y < 5.5f) return fine ? SilverTip(x, y) : '.';        // arena tip is SwordTipRows

            var (hl, hr) = RiftTearEdges(y);
            float tl = SwordAxis - hl, tr = SwordAxis + hr;

            // The chewed outer edge. The arena takes it in whole cells; the menu art takes the same
            // bite continuously, so the edge is bitten where the arena's is without the steps.
            float lo, hi;
            if (fine)
            {
                lo = SwordBladeLeft - 0.5f + RiftEdgeBite(hl);
                hi = SwordBladeRight + 0.5f - RiftEdgeBite(hr);
                if (x < lo || x > hi) return '.';
            }
            else
            {
                lo = SwordBladeLeft + RiftEdgeInset(hl);
                hi = SwordBladeRight - RiftEdgeInset(hr);
                if (x < lo || x > hi) return '.';
            }

            if (x >= tl && x <= tr)
            {
                // Inside the tear. Cyan hard against each edge; violet through the middle,
                // deepening at the centre. Measured from the tear's OWN edges rather than from
                // the blade's centre line, which is not the tear's middle - the two sides are
                // independent. The menu's rim is half a cell: at a whole one it reads as a tube.
                float rimW = fine ? 0.5f : 1f;
                bool onEdge = x < tl + rimW || x > tr - rimW;
                float across = (x - tl) / Mathf.Max(1f, tr - tl);
                return onEdge ? 'H' : Mathf.Abs(across - 0.5f) > 0.22f ? 'B' : 'D';
            }

            // The metal, lit from the outer edge inward so both flats are brightest where the
            // silhouette is and fall away into the tear - the outer texel is what holds the shape
            // against a dark arena. The menu keeps a hairline of glow on the lit outer edge.
            bool left = x < tl;
            if (fine && left && x < lo + 0.25f) return 'h';
            float u = left ? (x - lo) / Mathf.Max(1f, tl - lo)
                           : (hi - x) / Mathf.Max(1f, hi - tr);
            int band = Mathf.Clamp(Mathf.RoundToInt(u * 4f), 0, 4);
            return "lbdds"[band];
        }

        /// <summary><see cref="RiftEdgeInset"/> without the steps, for the menu art: the same bite
        /// at the same tear widths, ramped in over the band between them.</summary>
        static float RiftEdgeBite(float half)
            => Mathf.Clamp((half - 3.0f * SwordBladeScale) / (0.9f * SwordBladeScale) * 2f, 0f, 2f);

        // ---------------------------------------------------------------- the Rift Blade's hilt
        //
        // The "Aztec sun" from the user's reference sheet, cut in the SIGIL DOOR's stone - the
        // slab the arena is reached through in the hub, so the weapon and the door read as one
        // material (the colours are SigilDoor's own; see RiftPal):
        //
        //   GUARD   a HALF-DISC, flat edge on the grip and the dome rising into the blade, a band
        //           of sun rays CUT round it inside a plain rim
        //   GRIP    wood, straight
        //   POMMEL  a smaller half-disc the other way up - flat on the grip, dome hanging below -
        //           rayed the same, round a core of the tear's own light
        //
        // The rays are INCISIONS exactly as the door's marks are: a CutShade groove with its far
        // wall catching the light (CutLip). A ray painted flat on the stone floats; one cut into
        // it is carved.
        //
        // THE GUARD PASSES THE SHOULDER BUDGET (22 cells against 16), as Emberline's horns and
        // the Silver Blade's cross do - the half-disc IS this hilt. Held to 16 it stood only three
        // cells proud of the blade either side and read as a squat bell, not a sun.
        //
        // The dome takes nine rows and the pommel five, against the family's seven and three, so
        // the blade gives up four (BladeRowsFor). Only the pommel's two push the grip - and the
        // pivot - up the sword, two rows as the Silver Blade's are. The dome fills its zone.

        const int RiftGuardZoneRows = 9;
        const int RiftPommelRows = 5;
        static int RiftBladeRows => BladeRowsFor(6 + RiftGuardZoneRows + SwordGripRows + RiftPommelRows);
        static int RiftGripTop => 6 + RiftBladeRows + RiftGuardZoneRows;

        /// <summary>The grip's second row, like every greatsword's pivot.</summary>
        static Vector2Int RiftGrip => new(13, GripCentre(RiftGripTop, SwordGripRows));

        /// <summary>How far above the fist the Rift's shards orbit - the blade's middle, counted
        /// off this grid. It was a literal 0.52 in CharacterRigFactory, and moving the pivot to the
        /// grip's centre would have left it hanging below the blade's middle.</summary>
        public static float RiftShardsAlong => (RiftGrip.y - (6f + RiftBladeRows) * 0.5f) * FineUpscale / FinePpu;
        static Vector2Int RiftDetailGrip => new(RiftGrip.x * EmberDetailScale, RiftGrip.y * EmberDetailScale);

        /// <summary>The guard's flat edge - the boundary between its last row and the grip's first.</summary>
        static float RiftGuardBase => RiftGripTop - 0.5f;
        const float RiftGuardHalf = 11f;                              // PAST the shoulder budget - see above
        const float RiftGuardHeight = RiftGuardZoneRows;              // apex meets the blade's end

        static float RiftPommelBase => RiftGripTop + SwordGripRows - 0.5f;
        const float RiftPommelHalf = 5f;
        const float RiftPommelHeight = RiftPommelRows;

        const int RiftGuardRays = 7;
        const int RiftPommelRays = 3;

        /// <summary>Half-disc membership as a normalised radius: under 1 is inside. The flat edge
        /// is at <paramref name="baseY"/>; <paramref name="up"/> says which way the dome faces.
        /// <paramref name="angle"/> runs 0..PI round the dome.</summary>
        static float RiftDomeRadius(float x, float y, float baseY, float half, float height, bool up,
                                    out float angle)
        {
            float u = (x - SwordAxis) / half;
            float v = (up ? baseY - y : y - baseY) / height;
            angle = Mathf.Atan2(v, u);
            return v < 0f ? 99f : Mathf.Sqrt(u * u + v * v);
        }

        static char RiftHiltTexel(float x, float y, bool fine)
        {
            float e = RiftDomeRadius(x, y, RiftGuardBase, RiftGuardHalf, RiftGuardHeight, true, out float a);
            if (e < 1f)
            {
                // Menu: a drawn line where the dome lies over the blade - dark stone on dark steel
                // has no edge between them otherwise, and the guard reads as the blade widening.
                if (fine && e > 0.95f && Mathf.Abs(x - SwordAxis) < SwordBladeWidth / 2f + 0.5f)
                    return '1';
                return RiftSun(x - SwordAxis, RiftGuardBase - y, RiftGuardHalf, RiftGuardHeight, e, a,
                               RiftGuardRays, rim: 0.84f, core: 0.40f, light: false, fine);
            }

            e = RiftDomeRadius(x, y, RiftPommelBase, RiftPommelHalf, RiftPommelHeight, false, out a);
            if (e < 1f)
                return RiftSun(x - SwordAxis, RiftPommelBase - y, RiftPommelHalf, -RiftPommelHeight, e, a,
                               RiftPommelRays, rim: 0.84f, core: 0.46f, light: true, fine);

            if (y > RiftGripTop - 0.5f && y < RiftPommelBase && Mathf.Abs(x - SwordAxis) < SwordGripWidth / 2f)
                return RiftWood(x, y, fine);
            return '.';
        }

        /// <summary>
        /// One stone sun: an inner disc ringed by a cut groove, rays cut from it out to a plain
        /// rim. (<paramref name="px"/>, <paramref name="py"/>) is the texel from the flat edge's
        /// centre in CELLS, py pointing up the sword; a negative <paramref name="h"/> hangs the dome
        /// the other way. <paramref name="e"/> is the normalised radius, <paramref name="a"/> the
        /// angle round the dome.
        /// </summary>
        static char RiftSun(float px, float py, float w, float h, float e, float a, int rays,
                            float rim, float core, bool light, bool fine)
        {
            // The rim, shaded as a rounded edge lit from the upper left.
            float nx = Mathf.Cos(a), ny = h > 0f ? -Mathf.Sin(a) : Mathf.Sin(a);   // grid y runs down
            float lit = nx * -0.55f + ny * -0.83f;
            if (e >= rim)
            {
                // The menu's rim gets the door's pale lip on its lit crest.
                if (fine && lit > 0.3f && e > rim + (1f - rim) * 0.35f && e < rim + (1f - rim) * 0.7f)
                    return '6';
                return lit > 0.45f ? '6' : lit > 0.05f ? '5' : lit < -0.45f ? '3' : '4';
            }

            if (e < core)
            {
                // The pommel's centre holds a shard of the tear's light, violet with a cyan edge -
                // the one thing on the hilt that is not stone or wood. The guard's is the sun's
                // own disc, a step lighter than the stone round it.
                if (light)
                {
                    if (fine) return e > core * 0.80f ? 'H' : e > core * 0.40f ? 'B' : 'D';
                    return e > core * 0.62f ? 'H' : 'B';
                }
                return '5';
            }
            // The groove round the disc, its outer wall lit in the menu.
            if (fine)
            {
                if (e < core + 0.06f) return '2';
                if (e < core + 0.10f) return '6';
            }
            else if (e < core + 0.12f) return '2';

            // The rays: straight grooves along the dome's own radii, measured as a DISTANCE IN
            // CELLS from each ray's line - so every ray is the same width however the dome is
            // squashed. Spacing them by angle alone gave wedges a fraction of a cell wide near
            // the disc and three at the rim, which read as scattered blobs. A little wider toward
            // the rim, because a sun's rays fan out.
            float t = Mathf.Clamp01((e - core) / (rim - core));
            // Menu grooves are cut finer - the field's extra samples go on a crisper cut, not a
            // wider one.
            float width = fine ? Mathf.Lerp(0.16f, 0.34f, t) : Mathf.Lerp(0.30f, 0.56f, t);
            float best = 99f;
            for (int k = 0; k < rays; k++)
            {
                float phi = (k + 0.5f) / rays * Mathf.PI;
                float dx = Mathf.Cos(phi) * w, dy = Mathf.Sin(phi) * Mathf.Abs(h);
                float len = Mathf.Sqrt(dx * dx + dy * dy);
                float d = (px * dy - py * Mathf.Sign(h) * dx) / len;  // signed, in cells
                if (Mathf.Abs(d) < Mathf.Abs(best)) best = d;
            }
            if (Mathf.Abs(best) < width) return '2';                  // the groove
            // Its far wall catching the light, as the door's marks do - only out where the groove
            // is wide enough to have one; a lit texel beside a hairline is speckle.
            if (fine) { if (best >= width && best < width + 0.3f) return '6'; }
            else if (t > 0.45f && best >= width && best < width + 0.8f) return '6';
            return '4';
        }

        /// <summary>The wood grip: lit left, shaded right, dark grain, and the guard's shadow
        /// across its first row. The arena carries one wandering grain line; the menu, several
        /// thin ones that drift with the length of the grip.</summary>
        static char RiftWood(float x, float y, bool fine)
        {
            float c = x - (SwordAxis - SwordGripWidth / 2f + 0.5f);   // 0 .. width-1, cell centres
            if (!fine)
            {
                int col = Mathf.RoundToInt(c), row = Mathf.RoundToInt(y);
                if (row == RiftGripTop) return col < 1 ? 'v' : 'u';
                int grain = 2 + ((row - RiftGripTop) / 3) % 2;
                if (col == grain) return 'v';
                return col < 1 ? 'x' : col >= SwordGripWidth - 2 ? 'v' : 'w';
            }

            if (y < RiftGripTop + 0.5f) return c < 0.5f ? 'v' : 'u';
            float g = c * 1.1f + 0.35f * Mathf.Sin(y * 0.55f + c * 0.8f);
            float fr = g - Mathf.Floor(g);
            if (fr < 0.14f) return c < 0.4f ? 'w' : 'v';
            if (c < 0.3f) return 'x';
            if (c > SwordGripWidth - 1.6f) return fr > 0.7f ? 'u' : 'v';
            return c > SwordGripWidth - 2.6f ? 'v' : 'w';
        }

        static readonly string[] RiftSwordRows = BuildRiftSword();
        static readonly string[] RiftSwordDetail = BuildRiftSwordDetail();

        // ---------------------------------------------------------------- Shadow, as one field
        //
        // The blade is SHADOW-GLASS: dense along every edge - both outer rims and both walls of
        // the hollow fuller - and clearest across the middle of each flat, with a thin highlight
        // down the lit side in the menu art. Flat alpha across the flats read as a grey sheet;
        // glass (and shadow) reads by where it is thick.
        //
        // The hilt is the user's "arcane" reference: CRESCENTS round an ECLIPSE. Two arcs a side,
        // an outer and an inner, curve up from the grip and taper to horns either side of the
        // blade, open at the top where the blade leaves; at their centre a dark disc with a
        // bright rim; a small crescent cupped under the grip for a pommel. Dark iron, solid - a
        // hilt you could see through would leave the character holding nothing.
        //
        // The grip and pivot are the family's own (GreatswordGrip), so it sits in the hand
        // exactly as before. One field (ShadowTexel) for arena and menu.

        static float ShadowGripTop => 6 + BladeRows + 1 + 1 + 4 + 1;          // 62, as the family's
        static float ShadowCentreY => ShadowGripTop - 1.5f;
        static float ShadowPommelTop => ShadowGripTop + SwordGripRows;

        const float ShadowOrbR = 2.3f;
        // The crescents stand well OUT past the blade, as the reference's do - the inner arc clears
        // it at the sides. At radius 6.2 they lay over the blade's dark base and vanished into it.
        // The outer spans ~16 cells: the guard budget, exactly.
        const float ShadowOuterR = 7.2f, ShadowOuterTh = 1.45f;
        const float ShadowInnerR = 5.4f, ShadowInnerTh = 0.95f;

        static string[] BuildShadowSword()
        {
            var rows = new string[SwordHeightRows];
            for (int y = 0; y < rows.Length; y++)
            {
                var line = new char[SwordCanvas];
                for (int x = 0; x < SwordCanvas; x++) line[x] = ShadowTexel(x, y, fine: false);
                rows[y] = new string(line);
            }
            return rows;
        }

        /// <summary>Menu art at TRUE <see cref="MenuPpu"/>, four samples a cell.</summary>
        static string[] BuildShadowSwordDetail()
        {
            const int k = EmberDetailScale;
            var rows = new string[SwordHeightRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[SwordCanvas * k];
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++)
                    line[X] = ShadowTexel((X + 0.5f) / k - 0.5f, y, fine: true);
                rows[Y] = new string(line);
            }
            return rows;
        }

        static char ShadowTexel(float x, float y, bool fine)
        {
            char hilt = ShadowHilt(x, y, fine);
            if (hilt != '.') return hilt;
            if (y > ShadowCentreY) return '.';
            return ShadowBlade(x, y, fine);
        }

        /// <summary>The blade's half-width: the family's shared point, then the parallel blade.</summary>
        static float ShadowBladeHalf(float y)
        {
            if (y >= 5.5f) return SwordBladeWidth * 0.5f;
            float f = Mathf.Clamp(y, -0.5f, 5f), taper;
            if (f < 0f) taper = Mathf.Lerp(0.05f, SwordTipTaper[0], (f + 0.5f) / 0.5f);
            else
            {
                int i = Mathf.Min(SwordTipTaper.Length - 2, Mathf.FloorToInt(f));
                taper = Mathf.Lerp(SwordTipTaper[i], SwordTipTaper[i + 1], f - i);
            }
            return taper * SwordBladeWidth * 0.5f;
        }

        static char ShadowBlade(float x, float y, bool fine)
        {
            float half = ShadowBladeHalf(y);
            float dx = Mathf.Abs(x - SwordAxis);
            if (dx > half) return '.';

            // The hollow fuller - wider in the menu, where the outline takes less of it.
            float fh = ShadowFullerHalf(y);
            if (fine && fh > 0.3f) fh = fh * 1.1f + 0.25f;
            if (fh > 0.3f && dx <= fh) return '.';

            bool left = x < SwordAxis;
            float outer = half - dx;                                   // to the outer edge
            float inner = fh > 0.3f ? dx - fh : 99f;                   // to the hole's wall

            // Every EDGE is dense - that is what makes it read as a material, not a tint.
            if (outer < (fine ? 0.45f : 1f)) return left ? 'h' : 'l';
            if (inner < (fine ? 0.4f : 1f)) return left ? 'd' : 'd';
            if (fine && left && outer < 1.0f && outer > 0.7f) return 'h';   // the highlight
            if (fine && outer < 1.3f) return left ? 'b' : 'd';
            return 's';                                                // the clear middle
        }

        // ---- the hilt: crescents round an eclipse

        /// <summary>Distance to an arc of a ring round (cx, cy): the band tapers to a point at
        /// both ends of [from, to] (degrees from straight up), which makes each end a horn.</summary>
        static float ShadowArcSd(float x, float y, float cy, float r, float th, float from, float to)
        {
            float dx = Mathf.Abs(x - SwordAxis), dy = y - cy;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float phi = Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg;          // 0 up, 180 down
            if (phi < from || phi > to) return 99f;
            float t = (phi - from) / (to - from);
            float band = th * 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.55f);
            return Mathf.Abs(d - r) - band;
        }

        static float ShadowIronSd(float x, float y)
        {
            float cy = ShadowCentreY;
            float outer = ShadowArcSd(x, y, cy, ShadowOuterR, ShadowOuterTh, 28f, 168f);
            float inner = ShadowArcSd(x, y, cy, ShadowInnerR, ShadowInnerTh, 46f, 152f);
            float py = ShadowPommelTop - 0.5f;
            float cup = ShadowArcSd(x, y, py - 0.3f, 2.3f, 0.95f, 100f, 180f);
            float dx = Mathf.Abs(x - SwordAxis);
            float knob = Mathf.Sqrt(dx * dx + (y - (py + 2.1f)) * (y - (py + 2.1f))) - 0.95f;
            float cap = Mathf.Max(dx - 3.3f, Mathf.Abs(y - (py + 0.1f)) - 0.45f);
            return Mathf.Min(Mathf.Min(outer, inner), Mathf.Min(cup, Mathf.Min(knob, cap)));
        }

        static char ShadowHilt(float x, float y, bool fine)
        {
            float dx = x - SwordAxis;

            // The ECLIPSE: a dark disc with a bright rim, lit hardest on its upper left.
            float od = Mathf.Sqrt(dx * dx + (y - ShadowCentreY) * (y - ShadowCentreY));
            if (od < ShadowOrbR)
            {
                float rimW = fine ? 0.45f : 0.8f;
                if (od > ShadowOrbR - rimW)
                {
                    float facing = (-dx * 0.55f - (y - ShadowCentreY) * 0.83f) / od;
                    return facing > 0.3f ? 'H' : facing > -0.3f ? 'L' : 'B';
                }
                if (fine)
                {
                    float gx = dx + 0.8f, gy = y - ShadowCentreY + 0.8f;
                    if (gx * gx + gy * gy < 0.10f) return 'L';                     // a glint
                }
                return 'K';
            }

            float sd = ShadowIronSd(x, y);
            if (sd < 0f) return ShadowIron(x, y, sd, fine);

            // The grip: dark leather, a diagonal wrap.
            if (y > ShadowGripTop - 0.5f && y < ShadowPommelTop - 0.5f && Mathf.Abs(dx) < SwordGripWidth / 2f)
            {
                float g = (y + dx * 0.8f) / (fine ? 1.2f : 2f);
                bool ridge = g - Mathf.Floor(g) < (fine ? 0.3f : 0.5f);
                if (fine && dx < -2.4f) return '4';
                return ridge ? '4' : '1';
            }
            return '.';
        }

        static char ShadowIron(float x, float y, float sd, bool fine)
        {
            const float e = 0.3f;
            float nx = ShadowIronSd(x + e, y) - ShadowIronSd(x - e, y);
            float ny = ShadowIronSd(x, y + e) - ShadowIronSd(x, y - e);
            float nl = Mathf.Sqrt(nx * nx + ny * ny);
            if (nl > 1e-4f) { nx /= nl; ny /= nl; }
            float lit = nx * -0.55f + ny * -0.83f;
            if (-sd < (fine ? 0.35f : 0.65f))
            {
                if (lit > 0.5f) return fine ? 'H' : 'L';
                if (lit > 0.1f) return 'L';
                if (lit < -0.45f) return 'S';
            }
            return lit < -0.2f ? 'D' : 'B';
        }

        static readonly string[] ShadowSwordRows = BuildShadowSword();
        static readonly string[] ShadowSwordDetail = BuildShadowSwordDetail();

        /// <summary>
        /// Stamp a weapon as carrying the echo chain, and hand it its signature. One place, the
        /// same as <see cref="Heat"/>, so the flag and the finisher cannot drift apart - a weapon
        /// with the finisher and no chain would summon copies of a passive it does not have.
        /// </summary>
        static GearItem Echo(GearItem item, string signature)
        {
            item.HasEchoChain = true;
            item.SignatureFinisher = signature;
            item.TwoHanded = true;
            return item;
        }

        static GearItem Make(string id, string name, GearSlot slot, LootTier tier, float power,
                             params LayerSprite[] layers)
        {
            var item = ScriptableObject.CreateInstance<GearItem>();
            item.hideFlags = HideFlags.HideAndDontSave;
            item.ItemId = id;
            item.DisplayName = name;
            item.Slot = slot;
            item.Tier = tier;
            item.Power = power;
            item.Layers = layers;

            // Rolled once, here, seeded from the item's own id so the same placeholder always
            // comes back with the same stats across sessions - the same "fixed to identity, not
            // to when it happened to be generated" rule a real mint will need too.
            item.Grants = GearRoller.Roll(slot, tier, id.GetHashCode()).Grants;
            if (slot == GearSlot.Torso)
                item.DefensiveAbility = GearRoller.RollDefensiveAbility(id.GetHashCode());

            // A relic's roll IS its finisher. Only applied when the caller has not already named
            // one: the hand-authored Black Diamond relics set SignatureFinisher themselves after
            // Make returns, and the two demo relics below are deliberately seeded rather than
            // random. Assigned here so an ordinary bronze/silver/gold relic is never empty.
            if (slot == GearSlot.Relic && string.IsNullOrEmpty(item.SignatureFinisher))
                item.SignatureFinisher = GearRoller.RollFinisher(slot, tier, item.Class, id.GetHashCode());
            return item;
        }
    }
}
