using System.Collections.Generic;
using Convergence.Art.Gear;

namespace Convergence.Chain
{
    /// <summary>
    /// What a DIAMOND or BLACK DIAMOND drop becomes: one of the authored pieces of that tier.
    ///
    /// Bronze, Silver and Gold are ROLLS - stats on a slot, no design. The two top tiers carry no
    /// stats at all (GearRoller rolls them nothing), so a roll there was a nameless blank with no
    /// art. They are DESIGNS instead: every authored piece of the tier is eligible, whatever its
    /// slot or class, and the minted record wears it (MintedGearRecord.Design). One place, so the
    /// Forge's boxes and the floor-100 drop cannot disagree about what a Diamond is.
    ///
    /// Never eligible: FORGE-ONLY designs (Tria Prima and its seal - fused, never dropped), and at
    /// Black Diamond a RELIC that belongs to a weapon. A Black Diamond weapon drops TOGETHER with
    /// its relic - the weapon is the look, the relic the finisher, and the pair is one payout - so
    /// the relic on its own is not a separate thing to land on.
    /// </summary>
    public static class DesignDrops
    {
        public static bool IsDesignTier(LootTier tier) => tier is LootTier.Diamond or LootTier.BlackDiamond;

        /// <summary>
        /// Every piece a <paramref name="tier"/> drop may become, optionally narrowed to a slot
        /// (and, for a weapon, a class). Sorted by id so a seeded pick is stable.
        /// </summary>
        public static List<GearItem> Pool(LootTier tier, GearSlot? slot = null, WeaponClass? weaponClass = null)
        {
            var pool = new List<GearItem>();
            foreach (var item in GearCatalog.All.Values)
            {
                if (item == null || item.Tier != tier || item.ForgeOnly) continue;
                // Authored only: a minted instance is the catalogue entry for one player's copy.
                if (!string.IsNullOrEmpty(item.StackKey) || !string.IsNullOrEmpty(item.DesignId)) continue;
                if (slot.HasValue && item.Slot != slot.Value) continue;
                if (weaponClass.HasValue && item.Slot == GearSlot.Weapon && item.Class != weaponClass.Value) continue;
                if (tier == LootTier.BlackDiamond && item.Slot == GearSlot.Relic && WeaponFor(item) != null) continue;
                pool.Add(item);
            }
            pool.Sort((a, b) => string.CompareOrdinal(a.ItemId, b.ItemId));
            return pool;
        }

        /// <summary>A seeded pick from <paramref name="pool"/>, or null if it is empty.</summary>
        public static GearItem Pick(List<GearItem> pool, int seed)
            => pool == null || pool.Count == 0 ? null : pool[(seed & 0x7fffffff) % pool.Count];

        /// <summary>Slots a <paramref name="tier"/> drop can land in - what the Forge's targeted
        /// redemption offers, so it never offers a slot with nothing to give.</summary>
        public static List<GearSlot> Slots(LootTier tier)
        {
            var slots = new List<GearSlot>();
            foreach (var item in Pool(tier))
                if (!slots.Contains(item.Slot)) slots.Add(item.Slot);
            slots.Sort();
            return slots;
        }

        /// <summary>Weapon classes a <paramref name="tier"/> weapon drop can be.</summary>
        public static List<WeaponClass> Classes(LootTier tier)
        {
            var classes = new List<WeaponClass>();
            foreach (var item in Pool(tier, GearSlot.Weapon))
                if (!classes.Contains(item.Class)) classes.Add(item.Class);
            classes.Sort();
            return classes;
        }

        /// <summary>
        /// The relic that belongs to a Black Diamond weapon - the one granting the same signature
        /// - or null. Every Black Diamond weapon/relic pair shares its SignatureFinisher id, which
        /// is what says they are two halves of one item.
        /// </summary>
        public static GearItem RelicFor(GearItem weapon)
        {
            if (weapon == null || weapon.Slot != GearSlot.Weapon || weapon.Tier != LootTier.BlackDiamond ||
                string.IsNullOrEmpty(weapon.SignatureFinisher)) return null;
            foreach (var item in GearCatalog.All.Values)
                if (IsAuthoredPair(item, GearSlot.Relic, weapon)) return item;
            return null;
        }

        static GearItem WeaponFor(GearItem relic)
        {
            foreach (var item in GearCatalog.All.Values)
                if (IsAuthoredPair(item, GearSlot.Weapon, relic)) return item;
            return null;
        }

        static bool IsAuthoredPair(GearItem item, GearSlot slot, GearItem other)
            => item != null && item != other && item.Slot == slot && item.Tier == LootTier.BlackDiamond &&
               string.IsNullOrEmpty(item.StackKey) && string.IsNullOrEmpty(item.DesignId) &&
               item.ForgeOnly == other.ForgeOnly &&
               !string.IsNullOrEmpty(item.SignatureFinisher) && item.SignatureFinisher == other.SignatureFinisher;

        /// <summary>
        /// A minted record wearing <paramref name="design"/>. Carries no stats - both design tiers
        /// are cosmetic - but a relic keeps its finisher, which is the whole of what it is.
        /// </summary>
        public static MintedGearRecord RecordFor(GearItem design, string instanceId)
        {
            var record = new MintedGearRecord
            {
                InstanceId = instanceId,
                DisplayName = design.DisplayName,
                Slot = design.Slot,
                Tier = design.Tier,
                Class = design.Class,
                DefensiveAbility = design.DefensiveAbility,
                Finisher = design.Slot == GearSlot.Relic ? design.SignatureFinisher : null,
                Design = design.ItemId,
                StatsVersion = GearRoller.TablesVersion,
            };
            record.RebuildGrants();
            return record;
        }

        /// <summary>
        /// Everything one drop of <paramref name="design"/> pays out: the piece, plus its relic if
        /// it is a Black Diamond weapon. The first entry is always the piece itself.
        /// </summary>
        public static List<MintedGearRecord> Mint(GearItem design, string idPrefix)
        {
            string stamp = System.Guid.NewGuid().ToString("N");
            var made = new List<MintedGearRecord> { RecordFor(design, $"{idPrefix}-{design.Slot}-{stamp}") };
            var relic = RelicFor(design);
            if (relic != null) made.Add(RecordFor(relic, $"{idPrefix}-{relic.Slot}-{stamp}"));
            return made;
        }
    }
}
