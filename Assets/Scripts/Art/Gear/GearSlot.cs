namespace Convergence.Art.Gear
{
    /// <summary>
    /// The twelve WORN slots, plus the relic socket.
    ///
    /// The arena-economy doc prices a "full 12-piece gold set" at 5 boxes x 12 items, so the worn
    /// count is fixed by the economy, not chosen freely, and every one of the twelve is deliberately
    /// a slot that READS VISUALLY - a slot the player cannot see is worth nothing at the tier the
    /// collectible economy depends on.
    ///
    /// RELIC IS THE ONE EXCEPTION AND IS NOT ONE OF THE TWELVE. It draws nothing, it is not part of
    /// a set, and it takes only a black-diamond WEAPON: a socket for carrying that weapon's
    /// signature finisher without wielding it. It exists because a black-diamond weapon was
    /// otherwise only useful in the hand - and a disguise deliberately grants nothing (see
    /// Appearance.Resolve), so wanting a different sword meant giving the finisher up entirely.
    ///
    /// APPEND ONLY. JsonUtility serialises enums as INTS, so inserting anything above Weapon
    /// renumbers every saved loadout underneath it - a saved Gloves(10) would come back as
    /// something else. Relic is last for that reason and anything after it must be too.
    /// </summary>
    public enum GearSlot
    {
        Head,
        Shoulders,
        Torso,
        Back,
        Neck,
        Ring,       // right index finger
        Legs,
        Trinket,    // right hip
        Boots,
        Belt,
        Gloves,
        Weapon,
        Relic,      // no art; holds a black-diamond weapon for its finisher alone
    }

    /// <summary>Loot tiers from the economy doc.</summary>
    public enum LootTier
    {
        Silver,        // common, stat-bearing
        Gold,          // rarer, stat-bearing
        Diamond,       // purely cosmetic, NFT-backed
        BlackDiamond,  // ceiling tier, RNG-only, NFT-backed

        /// <summary>
        /// The floor tier - what the shallow floors pay before Silver starts appearing.
        ///
        /// APPENDED, NOT INSERTED, even though it sits BELOW Silver in value and reading order.
        /// JsonUtility serialises an enum as its ORDINAL, and MintedGearRecord.Tier is a saved
        /// field - putting Bronze first would renumber Silver 0->1, Gold 1->2, Diamond 2->3 and
        /// BlackDiamond 3->4, silently re-tiering every item anyone already owns. Same rule the
        /// Relic slot follows in GearSlot, for the same reason.
        ///
        /// Sort order for display is therefore a UI concern, never the declaration order.
        /// </summary>
        Bronze,
    }

    /// <summary>
    /// Which single field of <see cref="StatPercents"/> a rolled item's pool stat landed on -
    /// what GearRoller.Roll picks but never used to name. Needed for the Forge's combine/upgrade
    /// paths, which must match two items on "the same stat" and rebuild an exact known stat
    /// without re-rolling (a re-roll could silently land on a different one).
    ///
    /// APPEND ONLY, same JsonUtility-ordinal reason as GearSlot/LootTier - MintedGearRecord.
    /// PrimaryStat is a saved field. None is first and is the correct default for every item
    /// minted before this existed (Relic, and anything with no pool stat at all).
    /// </summary>
    public enum StatKind
    {
        None,
        Damage,
        AttackSpeed,
        MoveSpeed,
        Range,
        MaxHp,
        Armor,
        DamageResistance,
        Resilience,
        ElementGrowth,
        AbilityCooldownReduction,

        // ---- appended 2026-09-25, never reorder ----
        // "Sturdiness" is NOT here: DamageResistance already is exactly that stat (it slows the
        // armour pool's wear per hit), so it is shown to the player as Sturdiness instead.
        FinisherPower,
        FinisherKnockback,
        CritChance,
        CritDamage,
        Graze,
        Brace,
        Cleave,
        ElementalEffectiveness,
        ComboTime,
        AoeRadius,
        HealReceived,
        RepairReceived,
        Mend,
        Splash,
        Pierce,

        // ---- appended 2026-10-05 (the rebalance), never reorder ----
        /// <summary>Raises the BOTTOM of a hit's damage range toward its top - see
        /// Tuning.Attack.DamageSpread. 100 points: every hit lands at the top.</summary>
        Accuracy,
    }

    /// <summary>
    /// Paint order for the paper-doll, back to front. Each is one SpriteRenderer on the rig, and
    /// a gear item declares which of these it draws into - so a chest piece can cover the torso
    /// while a cloak sits behind the whole body.
    ///
    /// Declaration order IS the draw order. Inserting a layer re-sorts everything after it.
    ///
    /// This enum is NOT serialized anywhere - a gear item declares its layers in code and only the
    /// item's ID is ever saved - so unlike <see cref="GearSlot"/> it may be reordered. What that
    /// costs instead is the three sorting permutations in PrimitiveCharacterRig, which are indexed
    /// by this and have to be renumbered together; they are checked as exact permutations at
    /// construction so a miss is a loud error rather than a silently vanished layer.
    /// </summary>
    public enum RigLayer
    {
        Back,            // cape, behind everything

        /// <summary>
        /// The LENGTH of the hair - everything that falls past the skull, drawn behind the body.
        ///
        /// A body layer, not a gear one. Hair used to live entirely inside the 16x18 head sprite,
        /// which confined it to the head's own footprint: a waist-length braid had nowhere to go,
        /// and a helmet painted over the whole head erased all of it at once. This is the same
        /// answer KatanaSheathe already uses for a blade longer than the scabbard it belongs to -
        /// the object is drawn at its own real size instead of being cropped to the thing it hangs
        /// from.
        ///
        /// IN FRONT OF THE CAPE, BEHIND THE ARMS. Behind the cape was the cheaper slot (the rig
        /// leaves ten spare orders below its band, which is where the off-hand disc sits) and it is
        /// wrong: a cloak is 0.64 units across against a head of 0.43, so it would swallow almost
        /// every style this layer exists to show.
        /// </summary>
        HairBack,

        ArmBack,         // body
        GlovesBack,
        LegBack,         // body
        LegsBack,        // leg armour over the far leg
        BootsBack,

        /// <summary>
        /// A scarf's trailing tail (see <see cref="GearItem.HasFlowingScarf"/>) - behind the
        /// torso, on every permutation, the same "over the cape, under the body" treatment
        /// <see cref="HairBack"/> already gets. The whole point is that it is MOSTLY HIDDEN at
        /// rest: a tail drawn in front of the chest (the Neck layer's own position) obstructs the
        /// gear underneath it constantly, where one tucked behind the body only shows past the
        /// silhouette when a run or a turn swings it out - which is what makes the motion read as
        /// something happening, instead of a red flag permanently stapled to the chest.
        ///
        /// The COLLAR - the small wound part actually touching the neck - stays on the ordinary
        /// <see cref="Neck"/> layer, in front, exactly where an amulet sits. Only the hanging part
        /// moves back.
        /// </summary>
        NeckBack,

        Torso,           // body
        LegFront,        // body
        LegsFront,
        BootsFront,
        TorsoArmor,

        /// <summary>
        /// A Back-slot item's front drape - the part of a cloak/poncho that lies over the SHOULDERS
        /// and onto the chest, as opposed to <see cref="Back"/> (everything behind the body) and
        /// <see cref="Hood"/> (the cowl). wanderers_hood's yoke covers both shoulders and the top
        /// of the chest; wraith_cloak's drape covers the -X shoulder only. A cloak that wraps the
        /// body but never shows in front reads as a cape with extra steps.
        ///
        /// A DEDICATED layer, the same call Hood already made for the same reason: a Back item
        /// painting an existing TORSO-owned layer (TorsoArmor itself, or TorsoOver) would silently
        /// collide with whatever Torso-slot item is equipped alongside it, each overwriting the
        /// other depending only on application order. This one belongs to no other slot.
        ///
        /// Its enum position is NOT its sort position: every front-facing stack re-ranks it to
        /// directly above <see cref="TorsoOver"/> (PrimitiveCharacterRig.DrapeOverShoulders), so
        /// it draws over both pauldrons - the outer garment covers the armour. It first sat just
        /// after TorsoArmor, under the pauldrons, and every shoulder piece drew over the cloth
        /// meant to be covering it. The belt no longer cinches over it; nothing here reaches the
        /// waist.
        /// </summary>
        BackOver,

        /// <summary>
        /// Hip armour hanging from the belt across BOTH hips - a tasset, faulds, a plated skirt.
        ///
        /// WHY IT IS NOT ON LegsFront/LegsBack, where leg armour otherwise lives. Those two hang
        /// off the LEG pivots, one per limb, so a piece drawn there is really two pieces that swing
        /// independently with each stride. That is right for a greave strapped to a shin and wrong
        /// for a tasset in two separate ways: it cannot have a feature on the CENTRE LINE, because
        /// there is no piece there to draw one on, and hip armour suspended from a belt does not
        /// travel with the thigh in the first place.
        ///
        /// Both showed up trying to build the reference's three-pointed tasset - one point per side
        /// and one in the middle. Per-leg, the middle point has to be faked as two half-points on
        /// two sprites that pull apart the moment the character walks.
        ///
        /// So this is TORSO-PARENTED, which it gets for free: PivotFor falls through to the torso
        /// for anything it does not name, and the same is true of ArmourStand's copy, so neither
        /// switch needed a case adding.
        ///
        /// BETWEEN TorsoArmor AND Belt: over the cuirass's own fauld, under the strap it hangs from.
        /// </summary>
        Tasset,

        Belt,            // OVER the cuirass: a belt is cinched on top of body armour, not buried
                         // under its lower edge where nothing can see it
        Trinket,         // right hip, over the torso piece
        Neck,            // amulet, over the chest
        ArmFront,        // body
        GlovesFront,
        Ring,            // right index finger, over the glove
        ShouldersBack,
        Shoulders,

        /// <summary>
        /// A Torso item's OVERHANGING half - a mantle, gorget or yoke wide enough to pass in front
        /// of the upper arms. Its ordinary chest plate still goes on <see cref="TorsoArmor"/>;
        /// only the part that leaves the torso's own 24-texel footprint comes up here.
        ///
        /// WHY IT EXISTS AT ALL. TorsoArmor draws under ArmFront, which is correct for every
        /// cuirass in the file - they all sit inside the body outline, so the arms cap them the
        /// same way a pauldron does. A collar reaching past +/-16 does not: the arm punches a
        /// limb-shaped bite out of it and leaves the tip beyond the arm reading as a detached
        /// crescent floating in mid-air. Measured against the rig's own layout (arms occupy
        /// x 8..16, y -16..0), a 48-wide yoke puts a third of its width over them.
        ///
        /// ABOVE Shoulders, so the mantle covers a pauldron rather than the reverse - a collar
        /// this size hangs from the neck, over whatever is strapped to the shoulder underneath.
        ///
        /// BELOW Head on every permutation. That is load-bearing rather than incidental: the
        /// chibi body has no neck, so the head sitting IN FRONT OF the collar's top rows is what
        /// makes the head read as set INTO it. The neck the reference draws is the jaw emerging
        /// from the collar's mouth, not a drawn neck.
        ///
        /// Not a Shoulders-slot layer: a Torso item painting Shoulders/ShouldersBack would
        /// silently overwrite equipped pauldrons (or be overwritten by them) depending only on
        /// which got applied last - the same collision Hood's own doc explains, one slot over.
        /// </summary>
        TorsoOver,

        Head,            // body

        /// <summary>
        /// The back of the skull - a body layer, painted by SetAppearance like Head, never by a
        /// loadout. Sits idle (disabled) whenever the rig faces the camera; PrimitiveCharacterRig
        /// swaps it in for Head, HeadArmor and Hood while <see cref="ICharacterRig.SetFacingAway"/>
        /// is on, because none of those three have anything true to say about the back of a head -
        /// a front-on face staring out from a character who is supposed to be walking away breaks
        /// the one thing this state exists to sell.
        /// </summary>
        HeadBack,

        HeadArmor,

        /// <summary>
        /// A hood's cowl (see <see cref="GearItem.HasHood"/>) - a BACK-slot item's second piece,
        /// drawn over the head exactly where a helmet sits. Kept as its own layer rather than
        /// folded onto <see cref="HeadArmor"/>: that layer belongs to the Head slot, and a Back
        /// item painting it too would mean two independently equipped items silently overwriting
        /// each other's sprite on the one that gets applied last, with no error either way.
        ///
        /// AFTER HeadArmor, BEFORE Weapon, on every permutation - a cowl frames whatever the head
        /// is doing (bare or helmed) the same way HeadArmor already frames the bare skull, and the
        /// weapon still has to read as held in front of everything above the shoulders.
        /// </summary>
        Hood,

        Weapon,

        // ---- the elbow: everything below it on each arm ----
        //
        // APPENDED, never inserted - the value is the paint order only through the sorting
        // tables, but it is also an int wherever a RigLayer is serialised, and inserting would
        // shift every layer after it.
        //
        // Nothing authors these. The rig CUTS the arm and glove art at the elbow line when it
        // paints them (see PrimitiveCharacterRig.PaintElbowSplit), so every existing glove, and
        // every one authored later, bends at the elbow without being redrawn in two halves. Each
        // sits directly above its upper half in every sorting table, so a straight arm composites
        // to exactly the picture it was before the joint existed.

        ArmBackLower,     // body - the far forearm
        GlovesBackLower,
        ArmFrontLower,    // body - the near forearm
        GlovesFrontLower,

        // ---- the wrist: the HAND below it on each arm ----
        //
        // APPENDED, for the elbow's reason. Nothing authors these either: the rig cuts each
        // forearm again at the wrist when it paints it (PrimitiveCharacterRig.PaintWristSplit), and
        // hangs the hand from the same elbow pivot. They exist so a GRIP can raise just the hand:
        // a fist has to draw over the weapon it holds, and raising the whole forearm with it drew
        // the forearm over the pauldron hanging beside it - and a pauldron over the forearm, the
        // forearm's hand over the disc and the disc over the pauldron cannot all hold at once.
        // Each sits directly above its forearm in every sorting table (see WithForearms).

        ArmBackHand,      // body - the far hand
        GlovesBackHand,
        ArmFrontHand,     // body - the near hand
        GlovesFrontHand,

        // ---- the knee: the SHIN below it on each leg ----
        //
        // APPENDED, for the elbow's reason. Nothing authors these: the rig cuts the leg, the
        // greave and the boot at the knee when it paints them (PrimitiveCharacterRig.PaintKneeSplit)
        // and hangs the lower halves from a knee pivot, so the run can lift a foot - a leg that
        // only rotates at the hip never leaves the floor. Each sits directly above its upper half
        // in every sorting table (see WithShins), so a straight leg composites exactly as before.

        LegBackLower,     // body - the far shin
        LegsBackLower,
        BootsBackLower,
        LegFrontLower,    // body - the near shin
        LegsFrontLower,
        BootsFrontLower,

        /// <summary>
        /// A NECK item's shroud - cloth wound round the throat and laid over the shoulders, as
        /// opposed to <see cref="Neck"/> (a pendant or a scarf's collar on the chest).
        ///
        /// The rule (the user's): a neck item goes OVER the Back item from the front and UNDER it
        /// from behind. From the front every stack ranks this directly above
        /// <see cref="BackOver"/> (PrimitiveCharacterRig.NeckOverDrape); turned away, directly
        /// under <see cref="Back"/>, the cape being the outermost garment there
        /// (PrimitiveCharacterRig.NeckUnderCape).
        ///
        /// A DEDICATED layer for BackOver's own reason: a Neck item painting BackOver would
        /// collide with whatever cloak is worn. APPENDED, because the sorting tables are written
        /// in enum order - the helpers above insert it rather than every table restating it.
        /// </summary>
        NeckOver,
    }

    /// <summary>
    /// What a slot does to durability. Armour wears when you are HIT and raises damage taken;
    /// weapons wear when you LAND hits and lower damage dealt. Cosmetic slots never wear -
    /// diamond-tier items are collectibles, and degrading them would quietly destroy value.
    /// </summary>
    public enum SlotKind { Armor, Weapon, Cosmetic }

    public static class GearSlots
    {
        public static readonly GearSlot[] All = (GearSlot[])System.Enum.GetValues(typeof(GearSlot));

        /// <summary>
        /// The eleven that carry POWER - Relic never does (it is carried, not worn) and Trinket
        /// no longer can: it merged into Relic, and no item declares that slot any more (see
        /// DemoGear's own note on "leather_pouch"). Kept in the GearSlot enum only so an old save
        /// still pointing at it self-heals through DropStale the same way any other reshuffled
        /// slot does, rather than being removed and risking a JsonUtility int-reorder.
        /// </summary>
        public static readonly GearSlot[] Worn =
            System.Array.FindAll(All, s => s != GearSlot.Relic && s != GearSlot.Trinket);

        /// <summary>
        /// The twelve slots a UI actually shows a card for: everything but the retired Trinket.
        /// Unlike <see cref="Worn"/> this INCLUDES Relic - it carries no power, but since the
        /// merge it is real, visible, paintable art (a pouch, say) and belongs in both the equip
        /// grid and the transmog grid alongside everything else.
        /// </summary>
        public static readonly GearSlot[] Selectable = System.Array.FindAll(All, s => s != GearSlot.Trinket);

        /// <summary>
        /// The slots a DISGUISE may be worn on - Selectable minus Relic.
        ///
        /// A RELIC CANNOT BE DISGUISED, and that is the whole reason the relic slot is worth
        /// anything now. A relic's content IS its finisher, the way a helm's content is its stats,
        /// so a cosmetic override on the slot would be a disguise that decides a mechanic - the
        /// exact thing "a skin grants nothing" exists to prevent, arriving through the one slot
        /// where the picture and the mechanic are the same field. What is EQUIPPED decides.
        ///
        /// It is a legible omission rather than a shuffle, and only because Relic is LAST in the
        /// enum and therefore last in Selectable: the transmog grid simply ends one card early and
        /// every other card stays exactly where the equip grid puts it, which is the alignment
        /// TransmutationScreen.BuildSlots depends on. Insert a slot after Relic and that stops
        /// being free - derive the grid's positions from Selectable in both tabs if it ever does.
        /// </summary>
        public static readonly GearSlot[] Transmoggable =
            System.Array.FindAll(Selectable, s => s != GearSlot.Relic);

        public static SlotKind Kind(GearSlot slot) => slot switch
        {
            GearSlot.Weapon                                    => SlotKind.Weapon,
            // Jewellery does not take the beating armour does, so it never degrades. Neither does
            // a relic: it is never struck and never strikes, it is carried.
            GearSlot.Neck or GearSlot.Ring or GearSlot.Trinket
                or GearSlot.Relic                              => SlotKind.Cosmetic,
            _                                                  => SlotKind.Armor,
        };

        /// <summary>
        /// What may sit in a slot.
        ///
        /// Every slot but Relic takes items declaring that slot. RELIC takes either of two
        /// things: a proper relic item (Slot == Relic - the merged Trinket, now visible and
        /// cosmetic), or a black-diamond WEAPON carried for its signature alone, whose own Slot
        /// stays Weapon because it must remain wieldable.
        ///
        /// BOTH are gated on class the same way, and only when they actually grant a finisher.
        /// A relic contributes its signature to the wheel, and a finisher of the wrong class
        /// rotates in as a move the held weapon cannot perform - the same rule the locked
        /// signature slot already lives by. A greatsword's eruption swung on a pair of discs is
        /// not a balance problem, it is a broken animation. A purely cosmetic relic - no
        /// SignatureFinisher at all - has no such animation to disagree with, so it fits
        /// regardless of what is held.
        /// </summary>
        public static bool Accepts(GearSlot slot, GearItem item, GearItem heldWeapon = null)
        {
            if (item == null) return false;
            if (slot != GearSlot.Relic) return item.Slot == slot;

            // RELIC-SLOT ITEMS ONLY. A Black Diamond WEAPON used to be accepted straight into the
            // socket, from before the tier was split into a relic plus a cosmetic weapon - so the
            // picker offered every greatsword in the game here and the old model stayed reachable
            // beside the new one. Every signature now has a real Relic-slot piece (Emberline Mark,
            // Shadow Seal, Phantom Mark, Blood Vial, Zanmato Saya), so the fallback has nothing
            // left to serve and is retired.
            if (item.Slot != GearSlot.Relic) return false;

            if (string.IsNullOrEmpty(item.SignatureFinisher)) return true;
            if (heldWeapon == null) return true;      // nothing held yet - nothing to disagree with
            return item.Class == heldWeapon.Class && item.TwoHanded == heldWeapon.TwoHanded;
        }
    }

    public static class RigLayers
    {
        /// <summary>
        /// Paired layers for slots that exist on both limbs. Boots, gloves and shoulders each
        /// occupy two layers so they render on the near AND far side of the body - a boot that
        /// only appears on one foot reads as a bug, not as art.
        /// </summary>
        public static readonly (RigLayer Front, RigLayer Back)[] Pairs =
        {
            (RigLayer.BootsFront,   RigLayer.BootsBack),
            (RigLayer.GlovesFront,  RigLayer.GlovesBack),
            (RigLayer.Shoulders,    RigLayer.ShouldersBack),
            (RigLayer.LegsFront,    RigLayer.LegsBack),
        };

        public static readonly RigLayer[] All = (RigLayer[])System.Enum.GetValues(typeof(RigLayer));

        /// <summary>
        /// Layers the bare body draws into; everything else is gear-only.
        ///
        /// Apply's clear loop skips these, so a gear item painting one could never be unequipped -
        /// which is why HairBack is here. It is drawn by SetAppearance like the head, not by a
        /// loadout, and an equip must leave it exactly where it is.
        /// </summary>
        public static bool IsBody(RigLayer l) =>
            l is RigLayer.ArmBack or RigLayer.LegBack or RigLayer.Torso
              or RigLayer.LegFront or RigLayer.ArmFront or RigLayer.Head
              or RigLayer.HeadBack or RigLayer.HairBack
              or RigLayer.ArmBackLower or RigLayer.ArmFrontLower
              or RigLayer.ArmBackHand or RigLayer.ArmFrontHand
              or RigLayer.LegBackLower or RigLayer.LegFrontLower;

        /// <summary>
        /// The layer holding the part of <paramref name="upper"/> below the elbow, or false for
        /// a layer that does not cross the elbow at all.
        /// </summary>
        public static bool LowerOf(RigLayer upper, out RigLayer lower)
        {
            lower = upper switch
            {
                RigLayer.ArmBack     => RigLayer.ArmBackLower,
                RigLayer.GlovesBack  => RigLayer.GlovesBackLower,
                RigLayer.ArmFront    => RigLayer.ArmFrontLower,
                RigLayer.GlovesFront => RigLayer.GlovesFrontLower,
                _                    => upper,
            };
            return lower != upper;
        }

        /// <summary>
        /// The layer holding the part of leg layer <paramref name="upper"/> below the knee, or
        /// false for a layer that does not hang from a hip.
        /// </summary>
        public static bool ShinOf(RigLayer upper, out RigLayer lower)
        {
            lower = upper switch
            {
                RigLayer.LegBack    => RigLayer.LegBackLower,
                RigLayer.LegsBack   => RigLayer.LegsBackLower,
                RigLayer.BootsBack  => RigLayer.BootsBackLower,
                RigLayer.LegFront   => RigLayer.LegFrontLower,
                RigLayer.LegsFront  => RigLayer.LegsFrontLower,
                RigLayer.BootsFront => RigLayer.BootsFrontLower,
                _                   => upper,
            };
            return lower != upper;
        }

        /// <summary>
        /// The layer holding the part of forearm layer <paramref name="lower"/> below the wrist -
        /// the hand - or false for a layer that is not a forearm.
        /// </summary>
        public static bool HandOf(RigLayer lower, out RigLayer hand)
        {
            hand = lower switch
            {
                RigLayer.ArmBackLower     => RigLayer.ArmBackHand,
                RigLayer.GlovesBackLower  => RigLayer.GlovesBackHand,
                RigLayer.ArmFrontLower    => RigLayer.ArmFrontHand,
                RigLayer.GlovesFrontLower => RigLayer.GlovesFrontHand,
                _                         => lower,
            };
            return hand != lower;
        }
    }
}
