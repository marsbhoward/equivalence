using System;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// One equippable item. The on-chain design keeps items as separate NFTs and stores only
    /// their IDs in the character datum, so <see cref="ItemId"/> is the canonical identity here
    /// too - the rig, the loadout and the profile all refer to items by ID, never by reference.
    /// </summary>
    [CreateAssetMenu(menuName = "Convergence/Gear Item", fileName = "GearItem")]
    public class GearItem : ScriptableObject
    {
        [Tooltip("Stable identity. This is what gets written into the character datum.")]
        public string ItemId;

        public string DisplayName = "Unnamed";

        /// <summary>
        /// What this item STACKS as in a picker, or empty for one that never stacks.
        ///
        /// Empty on every hand-authored asset, and deliberately: an authored piece is unique by
        /// ItemId, so two of them must stay two cards even if their numbers happen to coincide -
        /// Gilded Cuirass and Black Steel Cuirass are different objects, not two of one. Only a
        /// MINTED instance fills it, from every attribute it actually carries (see
        /// MintedGearRecord.StackSignature), so two rolls collapse into one card when they are
        /// genuinely the same item and stay apart the moment any stat, defensive ability or
        /// rolled finisher differs.
        ///
        /// NonSerialized on purpose. It is derived from the record every time ToGearItem runs, so
        /// a serialized copy could only ever go stale against the fields it was computed from -
        /// and on an authored .asset it would be a field an artist could fill in and accidentally
        /// weld two catalogue entries together.
        /// </summary>
        [NonSerialized] public string StackKey;

        /// <summary>
        /// The same idea for a list where only the PICTURE matters - the transmog tab. Empty on
        /// authored assets for the same reason <see cref="StackKey"/> is.
        ///
        /// Looser on purpose: a disguise grants nothing, so two minted pieces that differ only in
        /// stats, defensive ability or rolled finisher are genuinely the same choice there and
        /// splitting them would be offering the player a decision that does not exist. The gear
        /// tab keeps them apart because there the difference is the whole point.
        /// </summary>
        [NonSerialized] public string AppearanceKey;

        /// <summary>
        /// Star level (0-3) of a minted Bronze/Silver/Gold piece, or -1 for anything that has no
        /// stars at all - authored catalogue items, the cosmetic tiers, relics. Screens draw the
        /// star row only when this is 0 or more. Derived from the record in ToGearItem, never
        /// authored, for the same reason as StackKey.
        /// </summary>
        [NonSerialized] public int Stars = -1;

        public GearSlot Slot;
        public LootTier Tier = LootTier.Silver;

        [Tooltip("Stat roll. Silver/gold carry power; diamond tiers are cosmetic (0) UNLESS the " +
                 "item is mechanical - see the note on the field.")]
        public float Power;

        [Tooltip("Durability pool. Armour spends this when you are hit, weapons when you land " +
                 "hits. Cosmetic-slot items ignore it entirely.")]
        public float MaxDurability = 100f;

        [Tooltip("What this piece is worth, in percentage points on the character's baselines. " +
                 "Zero throughout means it grants no stats - which is most items today.")]
        public StatPercents Grants = new();

        [Tooltip("Weapon slot only. Brings the off hand across onto the same hilt, and makes both " +
                 "arms swing together instead of counter-swinging. Default is on because the " +
                 "greatsword was the only weapon; a one-hander must clear it explicitly.")]
        public bool TwoHanded = true;

        [Tooltip("Weapon slot only. How this weapon is fought with - which basics it produces " +
                 "and which weapon arts it may roll. Ignored on every other slot.")]
        public WeaponClass Class = WeaponClass.Greatsword;

        [Tooltip("WEAPON or RELIC only. Moveset id of the weapon art this item contributes to slot " +
                 "3 of the rotation. At Black Diamond tier it LOCKS that slot, same as a signature " +
                 "always has; on any other tier a relic instead just seeds the slot's starting " +
                 "content, freely overwritable by a floor reward like any other unlocked slot. " +
                 "Empty on every other item.")]
        public string SignatureFinisher;

        [Tooltip("TORSO slot only. Every torso item rolls one of these - see DefensiveAbility's " +
                 "own doc for why there is no 'none.' Ignored on every other slot.")]
        public DefensiveAbility DefensiveAbility = DefensiveAbility.Dash;

        [Tooltip("Weapon slot only. This weapon runs a heat cycle (Combat.WeaponHeat) - its blade " +
                 "changes colour as weapon arts land, and steps marked ScalesWithHeat grow with it.")]
        public bool HasHeatCycle;

        [Tooltip("Weapon slot only. Every attack in this weapon's chain lands a second, echoed " +
                 "hit for a fraction of the first (Tuning.Shadow.ChainEchoFraction), and the " +
                 "figure that threw it is drawn beside you when this blade is the one on screen.")]
        public bool HasEchoChain;

        [Tooltip("Weapon slot only. Every swing in the ordinary chain (not the signature leap) " +
                 "teleports the character to a random point within their own melee reach and " +
                 "back - purely cosmetic, see Tuning.Phantom. Read from the DRAWN weapon, the " +
                 "same as the Rift Blade's shards or Saint's halo, because it changes only the " +
                 "picture and nothing the game resolves.")]
        public bool HasPhantomFlicker;

        [Tooltip("Weapon slot only. The signature weapon art plays its full sheathe-and-draw " +
                 "sequence instead of resolving as an ordinary swing. Read from the DRAWN " +
                 "weapon, like every other signature animation: the RELIC grants the weapon art, " +
                 "and the matching weapon - held or worn as a transmog - is what buys the " +
                 "animation. Socket the saya beside any other sword and Crosscut still lands, " +
                 "it just swings; there is no scabbard on that character to draw from.")]
        public bool HasSheathAnimation;

        [Tooltip("Relic slot only. Worn on the character's LEFT hip whichever way they face: the " +
                 "rig mirrors it with the carry, the way it mirrors the Wraithguard's drape, so it " +
                 "stays opposite the sword hand. The saya - the right hand crosses the body to put " +
                 "the blade in it, and facing left it would otherwise hang under that same hand.")]
        public bool WornOnLeftHip;

        [Tooltip("Back slot only. The drape (RigLayer.BackOver) swings WITH the cape: bent by the " +
                 "same springs about the cape's own hinge, so the two read as one cloth. Off " +
                 "for a yoke lying across both shoulders (the poncho), which should sit still.")]
        public bool DrapeSwingsWithCape;

        [Tooltip("Weapon slot only. Kills IMPLODE - the Rift's own collapse (RiftImplosion) instead " +
                 "of the ordinary flash. Read from the DRAWN weapon: a picture, so it follows the " +
                 "picture. The Rift Blade and the Rift Disc.")]
        public bool ImplodesKills;

        [Tooltip("Weapon slot only. The weapon leaves a light cycle's wall behind it wherever it " +
                 "moves - in the hand, in a swing, in flight (Combat/LightCycleTrail). Read from the " +
                 "DRAWN weapon. The Rift Disc.")]
        public bool LightCycleTrail;

        [Tooltip("Weapon slot only. The weapon LIGHTS whoever holds it - a soft 2D point light at " +
                 "its centre (Art/Gear/HeldGlow), in the hand, on a display, in flight. The light's " +
                 "colour; see GlowIntensity. Read from the DRAWN weapon. Singularity.")]
        public Color GlowColor = Color.white;

        [Tooltip("Weapon slot only. How strongly the weapon glows on its holder - 0 (every weapon " +
                 "but Singularity) casts no light at all. ADDED on top of the scene: 0.3 is a soft " +
                 "glow, 0.5 already fogs the character. See HeldGlow.")]
        public float GlowIntensity;

        [Tooltip("Weapon slot only. The glow comes from the weapon's RIM, not its middle - dark over " +
                 "the hub, brightest at the edge. For a disc whose light is its ring (Singularity: " +
                 "a black hole's core gives no light).")]
        public bool GlowFromRim;

        [Tooltip("Shoulders slot only. The plate HANGS DOWN the upper arm (a sode) rather than " +
                 "capping the shoulder: in the rest carry it turns with the arm's WHOLE raise, " +
                 "not the quarter a cap turns (PrimitiveCharacterRig.PauldronFollow) - a quarter " +
                 "lays a cap along the raised arm, but leaves a hanging plate off the shoulder " +
                 "while the arm lifts out from under it.")]
        public bool HangsAlongArm;

        /// <summary>
        /// Some of this piece's art is BARE SKIN, authored in <see cref="BodyLook.GearSkin"/> (and
        /// its shaded copy on a far limb) and repainted in the wearer's tone wherever a body wears
        /// it - see BodyLook.Reskin. The Talon set's unarmoured arm.
        /// </summary>
        public bool ShowsSkin;

        /// <summary>
        /// Some of this piece's art is CLOTH that takes the colour of the worn Back piece -
        /// authored white in <see cref="ClothDye.Undyed"/> and repainted at Apply in a ramp built
        /// round the cape's own colour (see ClothDye). With no Back piece it stays white, which is
        /// also what a picker card, the armoury wall and an NFT image show. The Survivor tabard.
        /// </summary>
        public bool DyedByBack;

        /// <summary>
        /// Its two arm (or two shoulder) layers are DIFFERENT things worn on the character's two
        /// DIFFERENT arms - the Talon set's plate on the left arm, the bare right; the Wraithguard
        /// Pauldron's cap and mantle. Authored for facing the camera; turned away, where the rig
        /// keeps the carry in the anatomical right hand by moving it to arm.back, the two trade
        /// sides, mirrored, so each stays on its own arm. On a CAPE, the cape turns over in place
        /// (the Vermilion Cape's lining stays on the right). Every other pair is one piece drawn
        /// twice and needs nothing; the Wraithguard Cloak's drape has its own rule (SyncDrapeSide).
        /// </summary>
        public bool Lopsided;

        [Tooltip("Back slot only. How far below the cape sprite's DRAWN top edge (the outline's " +
                 "border included) its spring hinges, in cells. 0 - every cape before the Hellspawn " +
                 "Cape - hinges on the top edge; a cape " +
                 "whose collar stands up past the neckline must hinge at the neck instead, or the " +
                 "swing slides its shoulders about a point over the character's head.")]
        public float CapeHingeCells;

        [Tooltip("Back slot only. The cape spring's swing as a fraction of the rig's own " +
                 "(PrimitiveCharacterRig.CapeSwing). A floor-length cape swung the full angle " +
                 "sweeps its hem half a body sideways.")]
        public float CapeSwingScale = 1f;

        [Tooltip("Weapon slot only. Lightning ARCS off the weapon at random, out to a greatsword's " +
                 "length - in the hand, on a display, in flight (Combat/LightningArcs). Read from " +
                 "the DRAWN weapon. Rai.")]
        public bool LightningArcs;

        /// <summary>
        /// A flipbook of the gas blade at evenly spaced phases - see DemoGear.BuildPhantomFrames.
        /// Null for every item except Phantom's own weapon. Consumed by PhantomHaze, which cycles
        /// through it on a timer so the blade reads as moving gas rather than a static wavy
        /// silhouette; not run through JsonUtility, so it never touches a save file.
        /// </summary>
        public Sprite[] PhantomHazeFrames;

        /// <summary>
        /// A flipbook the weapon layer loops through while this piece is drawn, at
        /// <see cref="IdleFrameSeconds"/> a frame - the Pacemaker's quicksilver bead running its
        /// track. Cycled by the same <see cref="PhantomHaze"/> ticker Phantom's gas uses; this is
        /// the general form of that field, which predates there being a second user. Arena art
        /// only, never saved.
        /// </summary>
        public Sprite[] IdleFrames;

        /// <summary>
        /// A revolver cylinder turning one chamber, played ONCE on the attack after a finisher
        /// (see CylinderSpin) rather than looped. Null for anything without a cylinder.
        /// </summary>
        public Sprite[] SpinFrames;
        public float IdleFrameSeconds = 0.1f;

        /// <summary>The flipbook the weapon layer should loop while this piece is drawn - Phantom's
        /// haze or an idle loop - and its frame time. Null frames for a still weapon.</summary>
        public (Sprite[] Frames, float Seconds) WeaponFlipbook
            => PhantomHazeFrames is { Length: > 0 } ? (PhantomHazeFrames, Core.Tuning.Phantom.HazeFrameSeconds)
             : IdleFrames is { Length: > 0 } ? (IdleFrames, IdleFrameSeconds)
             : (null, 0f);

        /// <summary>
        /// The parts a fused weapon comes apart into, in the order its signature finisher hands
        /// them out - Tria Prima's Reactor, Ripsaw and Pacemaker, one to each figure Separatio
        /// throws. Read from the DRAWN weapon: socket the relic beside any other sword and the
        /// figures hold that sword whole, because there is nothing on screen to pull apart.
        /// </summary>
        public Sprite[] SplitBlades;

        /// <summary>
        /// Never offered from the catalogue - only a MINTED instance wearing this design is held.
        /// Set on Tria Prima and its seal, which no box drops: the only way to own them is to burn
        /// the three Diamond parts at the Forge (see GearForge.Fusions). Read by GearOwnership.
        /// </summary>
        public bool ForgeOnly;

        /// <summary>
        /// On a MINTED item, the authored design it wears (see MintedGearRecord.Design), or empty.
        /// Lets the armoury light a Forge-only design's bay once an instance of it is held.
        /// Derived in ToGearItem, never authored - NonSerialized for StackKey's reason.
        /// </summary>
        [NonSerialized] public string DesignId;

        [Tooltip("One blade sprite per heat stage, in WeaponHeat.Stage order. Only meaningful " +
                 "with HasHeatCycle.")]
        public Sprite[] HeatStages = Array.Empty<Sprite>();

        /// <summary>The blade for a heat stage, or null if this weapon has no cycle.</summary>
        public Sprite BladeFor(Combat.WeaponHeat.Stage stage)
            => HasHeatCycle && HeatStages != null && (int)stage < HeatStages.Length
                ? HeatStages[(int)stage] : null;

        [Tooltip("Weapon slot only. This weapon carries a gem per element, and lights whichever " +
                 "one matches the run's played element - purely cosmetic, unlike the heat cycle: " +
                 "it does not evolve during the run, it just reads a choice made once at the door.")]
        public bool HasElementGems;

        [Tooltip("One blade sprite per Core.ElementType, indexed by (int)ElementType. Only " +
                 "meaningful with HasElementGems.")]
        public Sprite[] ElementBlades = Array.Empty<Sprite>();

        /// <summary>The blade with the gem for this element lit, or null if this weapon has none.</summary>
        public Sprite BladeFor(Core.ElementType element)
            => HasElementGems && ElementBlades != null && (int)element < ElementBlades.Length
                ? ElementBlades[(int)element] : null;

        /// <summary>The same four variants at MENU density, for anything drawing the menu art -
        /// swapping an arena variant into it would drop the whole sword to arena density.</summary>
        public Sprite[] MenuElementBlades = Array.Empty<Sprite>();

        public Sprite BladeFor(Core.ElementType element, bool menu)
        {
            if (!menu) return BladeFor(element);
            return HasElementGems && MenuElementBlades != null && (int)element < MenuElementBlades.Length
                ? MenuElementBlades[(int)element] : BladeFor(element);
        }

        [Tooltip("Armour. One layer of this piece (SigilLayer) bears the WEARER's element sigil - " +
                 "the attuned element's (see Attunement) wherever a character wears it. The " +
                 "layer's own sprite is the default picture every item view draws: all four " +
                 "combined. The Herald pauldron.")]
        public bool BearsSigil;

        public RigLayer SigilLayer;

        [Tooltip("One sprite per Core.ElementType for SigilLayer, indexed by (int)ElementType.")]
        public Sprite[] SigilSprites = Array.Empty<Sprite>();

        [Tooltip("The same four at MENU density.")]
        public Sprite[] MenuSigilSprites = Array.Empty<Sprite>();

        /// <summary>
        /// The worn picture of <paramref name="layer"/> for a wearer attuned to
        /// <paramref name="element"/>, or null when this layer bears no sigil (draw its own sprite).
        /// </summary>
        public Sprite SigilFor(RigLayer layer, Core.ElementType element, bool menu)
        {
            if (!BearsSigil || layer != SigilLayer) return null;
            var set = menu && MenuSigilSprites is { Length: > 0 } ? MenuSigilSprites : SigilSprites;
            return set != null && (int)element < set.Length ? set[(int)element] : null;
        }

        /// <summary>
        /// Marks are cut into this piece's art - texels in SecretFire's reserved colours - and burn
        /// in the wearer's element (see SecretFire): the Aether set and its greatsword. Purely a picture,
        /// so it follows the DRAWN piece like any other look. Its own sprite shows them unlit, black;
        /// anything drawing the piece live lays the lit overlay on top (KindledMarks, a card's
        /// UI.KindledImage). The flag tells those where to look - the overlay itself is derived from
        /// whatever sprite is on screen, so cut, mirrored or dyed copies all still burn.
        /// </summary>
        public bool Kindled;

        [Tooltip("Weapon or relic. This item grants the Blood Blade's signature: the fuller " +
                 "stains one shade deeper on every landed hit from the filling weapon art, and a " +
                 "full vial releases a Heavy AoE burst instead. See Combat/BloodVial.")]
        public bool HasBloodVial;

        [Tooltip("One blade sprite per fill level, 0 (empty) through Tuning.Blood.MaxFill " +
                 "(full). Only meaningful on the WEAPON - a relic has no blade art to fill.")]
        public Sprite[] BloodStages = Array.Empty<Sprite>();

        /// <summary>The blade at this fill level, or null if this weapon has no vial art.</summary>
        public Sprite BladeFor(int fillLevel)
            => HasBloodVial && BloodStages != null && fillLevel >= 0 && fillLevel < BloodStages.Length
                ? BloodStages[fillLevel] : null;

        [Tooltip("World-Y offset from the grip to each element's gem, indexed by (int)ElementType - " +
                 "where a reactive glow effect belongs. Only meaningful with HasElementGems.")]
        public float[] ElementGemAlong = Array.Empty<float>();

        /// <summary>The grip-relative offset of this element's gem, or 0 if this weapon has none.</summary>
        public float GemAlong(Core.ElementType element)
            => HasElementGems && ElementGemAlong != null && (int)element < ElementGemAlong.Length
                ? ElementGemAlong[(int)element] : 0f;

        /// <summary>
        /// The locked finisher this RELIC contributes, or null.
        ///
        /// RELIC ONLY. A Black Diamond weapon used to answer this too, and FinisherSource took
        /// the wielded weapon before the socket - which meant owning the cosmetic sword was worth
        /// a finisher, in direct contradiction of the rule the tier rests on: a player holding
        /// only the relic is mechanically identical to one holding both, so the cosmetic half can
        /// trade freely without anyone buying an advantage by owning it. The relic is the
        /// mechanic; the weapon is the picture, and what the matching weapon buys is the bespoke
        /// animation, never the move itself.
        ///
        /// Gated on the TIER as well as on the field being filled: the extra slot is what the
        /// ceiling tier is for, and an id typed onto a silver charm would otherwise hand out a
        /// black-diamond privilege for free. The gate lives here rather than at the call site so
        /// there is one place to change it if the tiers ever move.
        ///
        /// BLACK DIAMOND IS A SOURCE, NOT A LOOK. It is the box a piece drops from, and its
        /// pieces come in as many flavours as any other tier - there is no canonical black
        /// diamond sword or disc, and nothing here should give one a shared palette or silhouette.
        /// What the tier grants is bespoke art per ITEM plus a locked finisher on its relic.
        /// </summary>
        public Combat.Moveset Signature
            => Slot == GearSlot.Relic && Tier == LootTier.BlackDiamond
               && !string.IsNullOrEmpty(SignatureFinisher)
                ? Combat.MovesetLibrary.ById(SignatureFinisher)
                : null;

        /// <summary>
        /// RELIC only. What a relic wants in slot 3, at ANY tier - unlike Signature, this does
        /// not gate on Black Diamond, because a relic below that tier still gets to seed the
        /// slot's starting content, it just cannot lock it there. A Black Diamond relic answers
        /// both properties with the same moveset; the caller uses Signature to decide whether to
        /// lock and this to decide what to seed either way.
        /// </summary>
        public Combat.Moveset GrantedFinisher
            => Slot == GearSlot.Relic && !string.IsNullOrEmpty(SignatureFinisher)
                ? Combat.MovesetLibrary.ById(SignatureFinisher)
                : null;

        [Tooltip("Neck slot only. This piece is a scarf rather than a fixed pendant: it swings " +
                 "with the same spring the cape uses, hinged at the collar instead of the " +
                 "shoulders - purely cosmetic. Left off, an ordinary amulet keeps sitting still " +
                 "on the same RigLayer.Neck layer.")]
        public bool HasFlowingScarf;

        [Tooltip("Neck slot only. This piece's shroud (RigLayer.NeckOver) is the OUTERMOST " +
                 "garment: over the cape while turned away (a neck item otherwise goes under " +
                 "it), and over a worn HOOD in both facings - so over the chin and the back of " +
                 "the head too, where the hood would cover them. The Drifter's Shroud.")]
        public bool ShroudOverCloak;

        [Tooltip("Back slot only. This cloak has a cowl (RigLayer.Hood, painted alongside the " +
                 "cape body on RigLayer.Back): the mane clips the same way it does under a " +
                 "helmet rather than being disabled outright, and any equipped scarf's tail " +
                 "stops swinging, since the hood already covers the collar it would trail out " +
                 "from under.")]
        public bool HasHood;

        [Tooltip("Back slot only. This cloak's RigLayer.BackOver drape covers ONE shoulder - the " +
                 "character's RIGHT, whichever way they face - and this is that drape seen from " +
                 "BEHIND, swapped in while turned away (the front art would lay a seam across " +
                 "the cape). Left empty, BackOver is a front-only piece and hides while turned " +
                 "away - the poncho's yoke, which its own back panel covers from behind.")]
        public LayerSprite DrapeBack;

        [Tooltip("HasHood only. The cowl's own base colour, so the back of the head can be " +
                 "painted as HOOD when the rig turns around - RigLayer.Hood is a front-view " +
                 "opening into the face and has nothing true to say from behind, and painting " +
                 "the stand-in with the character's own hair would show hair instead of the " +
                 "hood the player is actually wearing.")]
        public Color HoodColor = Color.black;

        [Tooltip("HasHood only. The cowl seen from BEHIND - the front cowl's own silhouette with " +
                 "the face opening closed, at the same offset and size, so turning round changes " +
                 "what is inside the outline and nothing about the outline itself. Swapped onto " +
                 "RigLayer.Hood while the rig faces away. Left null, the rig falls back to " +
                 "painting the back of the head in HoodColor.")]
        public LayerSprite HoodBack;

        [Tooltip("Head slot only. This piece is tied on with a cloth band (RigLayer.HeadArmor " +
                 "shows only the plate/gem from the front): the tie's own knot and tails are " +
                 "painted onto RigLayer.HeadBack, visible only while the rig faces away - the " +
                 "same 'the front sprite has nothing true to say from behind' reasoning HasHood " +
                 "already uses, applied to a headband instead of a full cowl.")]
        public bool HasTieBack;

        [Tooltip("HasTieBack only. The tie's own cloth colour, painted at the hairline on " +
                 "RigLayer.HeadBack.")]
        public Color TieColor = Color.black;

        [Tooltip("Head slot only. This piece covers the FACE but not the hairline - a mask, not a " +
                 "helm. The rig treats any Head item with art as covering the skull, and blanks " +
                 "the mane above a helm's hairline so an oversized hairstyle does not sprout out " +
                 "of solid plate; a mask that stops below the fringe has no such hairline, and " +
                 "blanking for it deletes hair that is plainly still on show above the mask.")]
        public bool CoversFaceOnly;

        /// <summary>
        /// The head piece is FULLY ENCLOSED - no hair escapes it anywhere, not even below the
        /// hairline.
        ///
        /// The ordinary helm rule is that a covered head blanks the mane's rows ABOVE the
        /// hairline and leaves the length below it alone, so a long-haired character in a helmet
        /// still has hair falling down their back. That is right for a great-helm and wrong for a
        /// sealed one: a helmet that wraps the jaw has nowhere for that hair to come out of, and
        /// it reads as hair growing through steel.
        ///
        /// The OPPOSITE END of the same axis <see cref="CoversFaceOnly"/> sits on - mask, helm,
        /// sealed helm - and a separate flag rather than a mode on that one, because a piece
        /// could in principle be neither (an open-faced helm that still encloses the skull).
        /// </summary>
        public bool SealsHead;

        [Tooltip("Head slot only. Overwrites the character's own LEFT eye to a fixed red," +
                 "regardless of the Eyes colour chosen on the body tab - purely cosmetic. This " +
                 "changes the FACE itself (see BodyLook.Head's leftEyeGlow parameter), not an " +
                 "overlay, so it survives independently of whatever the item's own Layers art " +
                 "is (or isn't - see DemoGear's wraith_eye, which paints nothing else).")]
        public bool HasGlowingEye;

        [Header("Placeholder rig (PrimitiveCharacterRig)")]
        [Tooltip("Which rig layers this item paints into. A chest piece might cover Chest and " +
                 "Shoulders; a two-hander only MainHand.")]
        public LayerSprite[] Layers = Array.Empty<LayerSprite>();

        [Tooltip("Optional higher-detail art for the character screen, which magnifies a texel " +
                 "to around 14 screen pixels and can show detail the arena cannot. Empty means " +
                 "'use Layers' - which is the right answer unless the piece has genuinely finer " +
                 "art, since a block-upscaled grid is pixel-identical and would only cost memory.")]
        public LayerSprite[] MenuLayers = Array.Empty<LayerSprite>();

        /// <summary>
        /// The materials a dye can recolour - [0] MAIN, [1] ACCENT - or empty for a piece that
        /// can't be dyed. Declared on the DESIGN (DemoGear.Dyeable); which swatch each channel
        /// wears is the INSTANCE's (the minted record). Diamond armour only. See Dye.cs.
        /// </summary>
        public DyeChannel[] DyeChannels = Array.Empty<DyeChannel>();

        /// <summary>
        /// The art to paint. Menu art is opt-in per PIECE, not per density - a piece with none
        /// falls through to the arena set, so adding one finer helmet does not require redrawing
        /// eleven other slots to keep the character consistent.
        /// </summary>
        public LayerSprite[] LayersFor(bool menu)
            => menu && MenuLayers is { Length: > 0 } ? MenuLayers : Layers;

        /// <summary>
        /// A disc pair whose two hands hold DIFFERENT halves (the Armillary: Rising in the main
        /// hand, Falling in the off hand). Null for every ordinary disc, whose off hand is a copy of
        /// the main - the rig and every pair display fall back to that. Same grid, ppu and grip as
        /// the main layer, so the off-hand placement needs nothing new.
        /// </summary>
        public LayerSprite OffhandLayer;
        /// <summary>The off-hand half at menu density - see <see cref="OffhandLayer"/>.</summary>
        public LayerSprite OffhandMenuLayer;
        /// <summary>The off-hand half's flipbook, frame for frame with <see cref="IdleFrames"/>.</summary>
        public Sprite[] OffhandIdleFrames;

        /// <summary>What a split pair becomes when its halves come TOGETHER - the Armillary's whole
        /// four-element armillary, held overhead through Quintessence. A flipbook at
        /// <see cref="IdleFrameSeconds"/>. Null for everything else.</summary>
        public Sprite[] CombinedFrames;

        /// <summary>The off-hand half to draw, or null when the off hand copies the main.</summary>
        public LayerSprite OffhandFor(bool menu)
            => menu && OffhandMenuLayer?.Sprite != null ? OffhandMenuLayer : OffhandLayer;

        /// <summary>
        /// How the piece is POSED when it is hung up to be looked at - the rack, the armoury wall,
        /// a gear card - where that differs from how it is held. The King and Queen are held with
        /// its heads at 135/45 deg (the strut level for the fist) but hangs turned so the two lions
        /// face each other across the pair. Null for everything else: a display shows the held
        /// picture. Same grid, ppu and pivot as the held layer.
        /// </summary>
        public LayerSprite DisplayLayer;
        /// <summary>The display pose at menu density - see <see cref="DisplayLayer"/>.</summary>
        public LayerSprite DisplayMenuLayer;
        /// <summary>The off-hand half's display pose - see <see cref="DisplayLayer"/>.</summary>
        public LayerSprite OffhandDisplayLayer;
        /// <summary>The off-hand half's display pose at menu density.</summary>
        public LayerSprite OffhandDisplayMenuLayer;

        /// <summary>The display pose to draw, or null when the piece hangs as it is held.</summary>
        public LayerSprite DisplayFor(bool menu)
            => menu && DisplayMenuLayer?.Sprite != null ? DisplayMenuLayer
             : DisplayLayer?.Sprite != null ? DisplayLayer : null;

        /// <summary>The off-hand half's display pose, or null.</summary>
        public LayerSprite OffhandDisplayFor(bool menu)
            => menu && OffhandDisplayMenuLayer?.Sprite != null ? OffhandDisplayMenuLayer
             : OffhandDisplayLayer?.Sprite != null ? OffhandDisplayLayer : null;

        [Header("Authored rig (SpriteLibraryCharacterRig)")]
        [Tooltip("SpriteResolver category this item resolves - normally the slot name, e.g. \"Helmet\".")]
        public string Category;

        [Tooltip("Label within that category, e.g. \"gilded\". The unequipped state is \"none\".")]
        public string Label;

        [Tooltip("Optional. Injects this item's sprites into the character's SpriteLibrary at " +
                 "runtime, so gear authored separately from the base character still resolves.")]
        public SpriteLibraryAsset GearLibrary;

        public bool IsCosmeticTier => Tier is LootTier.Diamond or LootTier.BlackDiamond;
    }

    /// <summary>One layer's worth of an item's art.</summary>
    [Serializable]
    public class LayerSprite
    {
        public RigLayer Layer;
        public Sprite Sprite;

        [Tooltip("Position relative to this layer's joint, in rig units (the body is ~1 unit tall).")]
        public Vector2 Offset;

        [Tooltip("Rendered size in rig units. The sprite is scaled to hit this regardless of its " +
                 "resolution or Pixels Per Unit, so art never has to match an import setting.")]
        public Vector2 Size = new(0.3f, 0.3f);

        public Color Tint = Color.white;
    }
}
