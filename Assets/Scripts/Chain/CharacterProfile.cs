using System;
using System.Collections.Generic;
using Convergence.Core;

namespace Convergence.Chain
{
    /// <summary>
    /// Mirrors the CIP-68 reference-NFT datum from the on-chain handoff: cumulative
    /// meta-progression only. Mid-run state (HP, current room, in-progress loot) is
    /// deliberately absent - that never touches the store.
    /// </summary>
    [Serializable]
    public class CharacterProfile
    {
        public string ProfileId = "local-dev-0";

        /// <summary>
        /// A brand-new character, with its one default relic already equipped so the socket does
        /// not sit visibly blank before a real one is found or bought.
        ///
        /// Both profile stores construct a fresh profile the same way (local and chain), and both
        /// call THIS rather than each authoring the grant themselves - the exact copy/behaviour
        /// split this project keeps warning itself about elsewhere. If the default relic ever
        /// changes, there is one place to change it.
        /// </summary>
        public static CharacterProfile NewDefault(string profileId)
        {
            var profile = new CharacterProfile { ProfileId = profileId };
            profile.Gear.Set(Art.Gear.GearSlot.Relic, "leather_pouch");
            return profile;
        }

        /// <summary>
        /// What this character is CALLED. The id is the key a datum is stored under and is never
        /// shown; this is the label on the couch, and the thing a player actually thinks of as
        /// "my earth character". Blank falls back to the id so an older save still lists.
        /// </summary>
        public string Name = "";
        public int Level = 1;
        public int Xp;
        public ElementType LastElement = ElementType.Fire;
        public List<RunSummary> RecentRuns = new();

        /// <summary>
        /// Equipped gear as slot -> item ID. Permanent and account-level per the economy doc
        /// ("permanent account-level gear, not run-scoped"), so it belongs in the datum rather
        /// than in per-run state.
        /// </summary>
        public Loadout Gear = new();

        /// <summary>
        /// How the character LOOKS - transmogged gear art, skin, hair, eyes, hairstyle, face.
        ///
        /// Belongs in the datum rather than in local settings because this is the half of the
        /// profile a token actually depicts: the stat loadout churns with every run's loot, and
        /// an image that churned with it could never be an identity. Set at the transmutation
        /// circle; see <see cref="Appearance"/>.
        /// </summary>
        public Appearance Look = new();

        /// <summary>
        /// LEGACY. The room moved to <see cref="AccountProfile"/> - it belongs to the player, not
        /// to one character, or your trophies would disappear whenever you switched who you were
        /// playing. Kept only so a save written before that move can be migrated on load; see
        /// GameBootstrap.MigrateLegacyRoom. Never written to.
        /// </summary>
        public RoomLayout Room = new();

        /// <summary>
        /// A cosmetic preference, not a slot: the Head item stays equipped (and its stats still
        /// apply) but the HeadArmor layer is hidden. Standard in every game with collectible
        /// helmets, since a lot of hair/face customisation is otherwise never seen.
        /// </summary>
        public bool HelmHidden;

        /// <summary>
        /// Wear on owned items. Mutated during a run, committed at the run-end checkpoint -
        /// never per hit.
        /// </summary>
        public Durability Wear = new();

        /// <summary>
        /// Per-element mastery and sphere-grid unlocks. Earned only by playing; the daily cap on
        /// completed runs is what bounds it.
        /// </summary>
        public MasteryProfile Mastery = new();

        // Cumulative counters - the fields a checkpoint transaction would actually write.
        public int TotalRuns;
        public int TotalKills;
        public int BestKills;

        /// <summary>
        /// Deepest floor ever reached, across every run. Distinct from RunSummary.Floors, which
        /// is one run's own count - this is the high-water mark, and it is what the PFP carries.
        ///
        /// A character's PORTRAIT says what they are known for, and depth is the honest measure
        /// of that in a roguelite: kills accumulate with time played, a floor has to be earned.
        /// </summary>
        public int BestFloor;

        /// <summary>
        /// Unredeemed gear vouchers - earned during a run (one per floor cleared, banked at the
        /// run-end checkpoint like mastery XP, forfeit on a false start), spent at the Forge.
        /// </summary>
        public int PendingGearVouchers;

        /// <summary>
        /// Rift Boxes banked and safe, spendable on any future run.
        ///
        /// FINDABLE ONLY, NEVER PURCHASABLE, and that is a design line rather than a missing
        /// feature: a box bought with money is securing loot with money, which is the one thing
        /// the whole extraction economy is arranged not to allow. The cost of having to FIND them,
        /// plus the levels spent on the Rift capacity domain, is the entire constraint on how much
        /// a player can pull out of a deep run.
        ///
        /// Additive field: a profile written before Rift Boxes existed loads at zero, which is
        /// exactly right - they had not found any.
        /// </summary>
        public int RiftBoxes;

        /// <summary>
        /// Tiered loot boxes, banked and safe - the Forge's own currency for redemption,
        /// combining, and tier-upgrade. Bronze/Silver/Gold are stat-bearing (map onto the
        /// matching LootTier); Diamond/BlackDiamond are their cosmetic counterparts, same split
        /// GearRoller.TierScale already draws between rollable and cosmetic tiers.
        ///
        /// Found the same way Rift Boxes are - an occasional floor-clear drop instead of a direct
        /// item (see RunLoot) - and carried at risk the same way, banked only at extraction or a
        /// run-end checkpoint. A profile written before this existed loads at all-zero, which is
        /// exactly right: it had not found any.
        /// </summary>
        public LootBoxes Boxes = new();

        /// <summary>
        /// Every item this character has redeemed at the Forge - the LOCAL stand-in for "an item
        /// that is actually a separate NFT," since GearCatalog otherwise only knows the fixed
        /// DemoGear/Resources catalog. See MintedGearRecord for why this is a plain record rather
        /// than a GearItem directly.
        /// </summary>
        public List<MintedGearRecord> MintedGear = new();

        /// <summary>
        /// The piece staked on the run in progress, or empty. See <see cref="GearStake"/>.
        ///
        /// IN THE DATUM, NOT IN RUN STATE, and that is the point: it is written at the run-start
        /// checkpoint, so a run that never reaches its run-end checkpoint (the game closed in a
        /// losing fight) still has an unsettled stake on disk, and loading forfeits it. Held only
        /// in memory it could be dodged by quitting. Additive: older saves load blank, no stake.
        /// </summary>
        public string StakedInstanceId = "";

        /// <summary>The floor the stake's run must clear for an extraction to pay out - fixed
        /// when the stake is placed, so a mid-run change to the tuning cannot move it.</summary>
        public int StakeGateFloor;

        /// <summary>The stake was INSURED with a Rift Box at the door: a death (or a run that
        /// never ended) brings it home instead of destroying it. The box is spent either way.
        /// Additive: older saves load false.</summary>
        public bool StakeInsured;

        /// <summary>Name for display, never blank.</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? ProfileId : Name;
    }

    /// <summary>Written once, at run end. One of these per checkpoint transaction.</summary>
    [Serializable]
    public class RunSummary
    {
        public string RunId;
        public ElementType Element;
        public int Kills;
        public int Floors;
        public float DurationSeconds;

        /// <summary>
        /// XP this run earned, including any floor XP upgrades. Computed by the game, not the
        /// store - a persistence layer should record what happened, not decide the rules.
        /// </summary>
        public int XpEarned;
        public bool Survived;
        public string EndedAtUtc;

        /// <summary>
        /// Design-tier boxes this run SECURED - banked at its end over what the profile held when it
        /// began. The service's box ledger is credited from these (once per run id the run-start
        /// checkpoint registered, capped by what the depth makes plausible) - never from
        /// <see cref="CharacterProfile.Boxes"/>, which is an absolute count the client writes.
        /// Additive: older summaries read 0.
        /// </summary>
        public int SecuredDiamondBoxes;
        public int SecuredBlackDiamondBoxes;
    }

    /// <summary>
    /// Tiered loot boxes, one field per LootTier - a plain struct rather than a
    /// Dictionary&lt;LootTier,int&gt; because this is JsonUtility-persisted and JsonUtility does
    /// not serialise dictionaries at all.
    /// </summary>
    [Serializable]
    public class LootBoxes
    {
        public int Bronze;
        public int Silver;
        public int Gold;
        public int Diamond;
        public int BlackDiamond;

        public int Get(Art.Gear.LootTier tier) => tier switch
        {
            Art.Gear.LootTier.Bronze => Bronze,
            Art.Gear.LootTier.Silver => Silver,
            Art.Gear.LootTier.Gold => Gold,
            Art.Gear.LootTier.Diamond => Diamond,
            Art.Gear.LootTier.BlackDiamond => BlackDiamond,
            _ => 0,
        };

        public void Add(Art.Gear.LootTier tier, int amount)
        {
            switch (tier)
            {
                case Art.Gear.LootTier.Bronze: Bronze += amount; break;
                case Art.Gear.LootTier.Silver: Silver += amount; break;
                case Art.Gear.LootTier.Gold: Gold += amount; break;
                case Art.Gear.LootTier.Diamond: Diamond += amount; break;
                case Art.Gear.LootTier.BlackDiamond: BlackDiamond += amount; break;
            }
        }
    }

    /// <summary>
    /// One item redeemed at the Forge - the plain-data record CharacterProfile can actually
    /// persist. GearItem is a ScriptableObject built for hand-authored, shared catalog entries
    /// (sprites, weapon-class flags, a dozen other fields); a minted item is the opposite shape -
    /// one player's own instance, entirely described by a handful of rolled values. This is that
    /// handful, and ToGearItem() is the one place it turns into something GearCatalog can resolve.
    ///
    /// InstanceId (not ItemId) is what the Loadout stores for one of these, and what a real mint
    /// would eventually use as the on-chain asset name - see web/service's own GEAR-&lt;itemId&gt;-
    /// &lt;uuid&gt; naming, which this mirrors on purpose so the local and chain shapes agree.
    /// </summary>
    [Serializable]
    public class MintedGearRecord
    {
        public string InstanceId;
        public string DisplayName;
        public Art.Gear.GearSlot Slot;
        public Art.Gear.LootTier Tier;
        public Art.Gear.StatPercents Grants = new();
        public Art.Gear.DefensiveAbility DefensiveAbility;

        /// <summary>
        /// The finisher a RELIC rolled, or empty. Additive to the record, so a profile written
        /// before relics rolled anything loads with it blank and simply seeds nothing - the same
        /// JsonUtility default-on-missing-field rule Appearance.Beard already relies on.
        /// </summary>
        public string Finisher;

        /// <summary>
        /// Which single StatPercents field Grants' pool stat landed on. Additive - a record
        /// minted before this existed loads at StatKind.None, meaning it simply never matches
        /// anything at the Forge's combine screen (correct: nobody can vouch for what an old
        /// item's roll actually was).
        /// </summary>
        public Art.Gear.StatKind PrimaryStat;

        /// <summary>
        /// Star level: 0 = base, then one to three stars (GearRoller.MaxLevel). Scales every stat
        /// on the piece. Independent of Tier - see GearRoller.UpgradeScale's own note on why the
        /// two must not be conflated.
        /// </summary>
        public int UpgradeLevel;

        /// <summary>
        /// Rolled secondary stats - Bronze 1, Silver 2, Gold 3 (GearRoller.SubStatCount). Values
        /// are UNSCALED rolls; Grants is rebuilt from these plus the primary via RebuildGrants.
        /// Additive: a record written before sub-stats existed loads with an empty list.
        /// </summary>
        public List<Art.Gear.SubStat> SubStats = new();

        /// <summary>
        /// Weapon-slot only, ignored otherwise (same conditional treatment as
        /// DefensiveAbility being Torso-only). Needed because a minted weapon's class was never
        /// recorded before the Forge could redeem toward a chosen one - see RedeemForgeBoxTargeted.
        /// </summary>
        public Art.Gear.WeaponClass Class;

        /// <summary>
        /// The AUTHORED item this instance wears - its art, class, grip and any signature - or
        /// empty for a piece that is only stats (everything minted before this existed, and every
        /// Bronze/Silver/Gold roll). Set on the Diamond weapons a Diamond box can redeem into and
        /// on what the Forge fuses from them: a fusion has to be able to tell a Mercury Reactor
        /// from any other Diamond weapon, which no stat on the record can. Additive, so older
        /// saves load it blank and wear nothing, exactly as they did.
        /// </summary>
        public string Design;

        /// <summary>
        /// Which gear tables the stored sub-stat rolls were made against (GearRoller.TablesVersion).
        /// A sub-stat's Value is an absolute number, so when the tables change it must be carried
        /// to the same place in its new range - GearForge.Migrate does that at load and stamps the
        /// current version. The primary needs nothing: it is rebuilt from the tables every time.
        ///
        /// ZERO IS "OLD", deliberately, with no initializer: every record written before this
        /// existed loads at 0 and is migrated. That makes stamping the job of whoever CREATES a
        /// record - every `new MintedGearRecord` sets it to GearRoller.TablesVersion, or the next
        /// load would remap rolls that were already current.
        /// </summary>
        public int StatsVersion;

        /// <summary>
        /// The on-chain CIP-68 USER (222) unit when this record was minted to the wallet - a
        /// Diamond / Black Diamond design redeemed through the service (Chain.Web.ChainDesigns).
        /// Empty for everything that lives only in the character's own datum. Additive.
        /// </summary>
        public string Unit;

        /// <summary>Recomputes Grants from tier, primary, star level and sub-stats - the only
        /// way Grants should change once an item exists, so the stored total can never disagree
        /// with the rolls it is made of.</summary>
        public void RebuildGrants()
            => Grants = Art.Gear.GearRoller.BuildGrants(Slot, Tier, PrimaryStat, UpgradeLevel, SubStats);

        /// <summary>
        /// Builds the runtime GearItem GearCatalog resolves this instance as. No art of its own -
        /// a minted item wears whatever placeholder shape DemoGear already draws for its slot,
        /// the same way a real NFT's art would need a real asset pipeline this project does not
        /// have yet. Stats and identity are what the Forge actually promises.
        /// </summary>
        public Art.Gear.GearItem ToGearItem()
        {
            // Wearing a design: start from a COPY of it, so the instance carries every picture and
            // behaviour flag the authored piece has - layers, menu art, flipbooks, split blades,
            // signature - and the lines below overwrite only what makes it this instance.
            var design = string.IsNullOrEmpty(Design) ? null : Art.Gear.GearCatalog.Get(Design);
            var item = design != null
                ? UnityEngine.Object.Instantiate(design)
                : UnityEngine.ScriptableObject.CreateInstance<Art.Gear.GearItem>();
            item.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
            item.DesignId = design != null ? Design : null;
            item.ItemId = InstanceId;
            item.DisplayName = DisplayName;
            item.Slot = Slot;
            item.Tier = Tier;
            item.Grants = Grants;
            if (Slot == Art.Gear.GearSlot.Torso) item.DefensiveAbility = DefensiveAbility;
            if (Slot == Art.Gear.GearSlot.Weapon && design == null) item.Class = Class;

            // A rolled relic seeds slot 3 without locking it - GearItem.Signature gates on Black
            // Diamond, which nothing minted here ever is, so this only ever answers
            // GrantedFinisher. See PlayerController.SeedThirdSlot.
            if (!string.IsNullOrEmpty(Finisher)) item.SignatureFinisher = Finisher;
            item.StackKey = StackSignature();
            item.Stars = Slot != Art.Gear.GearSlot.Relic &&
                         Tier is Art.Gear.LootTier.Bronze or Art.Gear.LootTier.Silver or Art.Gear.LootTier.Gold
                ? UpgradeLevel : -1;
            item.AppearanceKey = AppearanceSignature();
            return item;
        }

        /// <summary>
        /// Two instances stack in a picker if and only if this string matches - every attribute
        /// the item actually carries, and nothing else.
        ///
        /// IT MIRRORS ToGearItem LINE FOR LINE, and that is the rule rather than a nicety. The
        /// defensive ability is only copied onto a TORSO, so it is only keyed on a torso: keyed
        /// unconditionally, two otherwise-identical boots would refuse to stack over an ability
        /// neither of them has, which is a difference the player can never see. Anything added to
        /// ToGearItem has to be added here in the same breath or the two answers drift, and a
        /// stack key that ignores a real attribute is exactly the bug this exists to fix.
        ///
        /// DisplayName is in it as the item's NAME rather than as decoration - two pieces the
        /// player reads as different things must never merge, however their numbers landed.
        /// InstanceId deliberately is NOT: it is unique by construction, so keying on it would
        /// mean nothing ever stacks, which is where this started.
        ///
        /// Durability is also NOT in it. Wear is STATE, not an attribute - it changes every fight,
        /// so keying on it would fragment a stack into one card per hit taken. The stack is what
        /// the items ARE; which copy you get when you pick one is settled separately, by taking
        /// the least worn.
        /// </summary>
        public string StackSignature()
        {
            string ability = Slot == Art.Gear.GearSlot.Torso ? DefensiveAbility.ToString() : "-";
            string weaponClass = Slot == Art.Gear.GearSlot.Weapon ? Class.ToString() : "-";
            string grants = Grants != null ? Grants.Signature() : "-";

            // Sub-stats are keyed on their own, not only through Grants: two Damage 2.5 rolls and
            // a Damage 2 + Damage 3 pair sum to the same Grants but combine differently.
            var c = System.Globalization.CultureInfo.InvariantCulture;
            string subs = SubStats == null ? "" : string.Join(",", SubStats.ConvertAll(
                x => x == null ? "-" : $"{x.Kind}:{x.Value.ToString("0.##", c)}"));
            return $"{Slot}|{Tier}|{DisplayName}|{grants}|{ability}|{Finisher}|{weaponClass}|{PrimaryStat}|{UpgradeLevel}|{subs}|{Design}";
        }

        /// <summary>
        /// The same question asked where only the PICTURE counts - what the transmog tab stacks
        /// on. Everything the disguise actually carries, which is the slot, the tier the swatch
        /// draws and the name, and nothing it does not.
        ///
        /// DELIBERATELY BLIND TO STATS, ABILITY AND FINISHER, which is the whole difference from
        /// StackSignature: a skin grants nothing, so two rolls that differ only in those are one
        /// choice on that tab and two on the gear tab. Offering the same picture twice because of
        /// numbers the disguise ignores would be a decision the player cannot act on.
        /// </summary>
        public string AppearanceSignature() => $"{Slot}|{Tier}|{DisplayName}|{Design}";
    }
}
