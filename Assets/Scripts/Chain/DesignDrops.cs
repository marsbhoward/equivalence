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
    /// Never eligible: a RING (rings are never designs), FORGE-ONLY designs (Tria Prima and its
    /// seal - fused, never dropped), and at
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
                if (item == null || item.Tier != tier || item.ForgeOnly || GearCatalog.IsDyedCopy(item)) continue;
                // NEVER A RING (the user's call, 2026-10-13): at their size in game rings can hardly
                // be made unique, and they are not to be paid for - every ring is stat-bearing gear
                // that does not trade. The service's pool refuses them too (designs.ts).
                if (item.Slot == GearSlot.Ring) continue;
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

        /// <summary>
        /// One thing a box can pay out: a weapon (with its relic at Black Diamond), a lone piece, or
        /// a whole Diamond armour SET (ArmourSets - one design, one token). <see cref="Id"/> is what
        /// the service rolls and mints: an item id, or a set's <c>set_&lt;prefix&gt;</c>.
        /// </summary>
        public sealed class Design
        {
            public string Id;
            public string Name;
            /// <summary>Every piece this design is worn as - one, or a set's pieces in slot order.</summary>
            public List<GearItem> Pieces = new();
            public bool IsSet => ArmourSets.IsSet(Id);
            /// <summary>The single piece, for a design that is not a set.</summary>
            public GearItem Item => Pieces.Count > 0 ? Pieces[0] : null;
        }

        /// <summary>
        /// What a <paramref name="tier"/> box pays out, one entry per DESIGN: <see cref="Pool"/>'s
        /// pieces with every armour set folded into one entry. Sorted by id so a seeded pick is
        /// stable. This, not Pool, is what boxes and the service's manifest roll from.
        /// </summary>
        public static List<Design> Designs(LootTier tier)
        {
            var designs = new List<Design>();
            var sets = new HashSet<string>();
            foreach (var item in Pool(tier))
            {
                string set = ArmourSets.SetOf(item);
                if (set == null)
                {
                    designs.Add(new Design { Id = item.ItemId, Name = item.DisplayName, Pieces = { item } });
                    continue;
                }
                if (!sets.Add(set)) continue;
                designs.Add(new Design { Id = set, Name = ArmourSets.Name(set), Pieces = new List<GearItem>(ArmourSets.Pieces(set)) });
            }
            designs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return designs;
        }

        /// <summary>A design by the id the service minted it under - an item or a set - or null.</summary>
        public static Design DesignById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (ArmourSets.IsSet(id))
            {
                var pieces = ArmourSets.Pieces(id);
                return pieces.Count == 0 ? null : new Design { Id = id, Name = ArmourSets.Name(id), Pieces = new List<GearItem>(pieces) };
            }
            var item = GearCatalog.Get(id);
            return item == null ? null : new Design { Id = id, Name = item.DisplayName, Pieces = { item } };
        }

        /// <summary>A seeded pick from <paramref name="designs"/>, or null if it is empty.</summary>
        public static Design Pick(List<Design> designs, int seed)
            => designs == null || designs.Count == 0 ? null : designs[(seed & 0x7fffffff) % designs.Count];

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
        /// The records one minted DESIGN wears: a set's pieces (each its own record, so the game can
        /// still wear and dye them one by one, all naming the set in <see cref="MintedGearRecord.SetDesign"/>
        /// and sharing the token's unit), or the one piece plus a Black Diamond weapon's relic.
        /// <paramref name="instanceId"/> is the design's - a set's pieces add their own id to it.
        /// </summary>
        public static List<MintedGearRecord> RecordsFor(Design design, string instanceId, string unit = null)
        {
            var made = new List<MintedGearRecord>();
            if (design.IsSet)
            {
                foreach (var piece in design.Pieces)
                {
                    var r = RecordFor(piece, $"{instanceId}.{piece.ItemId}");
                    r.SetDesign = design.Id;
                    r.Unit = unit;
                    made.Add(r);
                }
                return made;
            }
            var one = RecordFor(design.Item, instanceId);
            one.Unit = unit;
            made.Add(one);
            return made;
        }

        /// <summary>A box's payout minted locally (no service): the design's records, a set's pieces
        /// or a weapon with its relic.</summary>
        public static List<MintedGearRecord> Mint(Design design, string idPrefix)
        {
            if (design.IsSet)
                return RecordsFor(design, $"{idPrefix}-Set-{System.Guid.NewGuid():N}");
            return Mint(design.Item, idPrefix);
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
