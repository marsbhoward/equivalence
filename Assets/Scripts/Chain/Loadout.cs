using System;
using System.Collections.Generic;
using Convergence.Art.Gear;

namespace Convergence.Chain
{
    /// <summary>
    /// What the character is wearing, as SLOT -> ITEM ID.
    ///
    /// The on-chain handoff is explicit that equipment is separate NFTs with "item IDs
    /// referenced from character datum rather than embedded, to keep datum size manageable" -
    /// so this stores IDs only. No stats, no art, no item copies. Resolving an ID to an actual
    /// item is <see cref="GearCatalog"/>'s job (and the backend's, later).
    ///
    /// A List rather than a Dictionary because Unity's JsonUtility cannot serialise dictionaries,
    /// and this has to round-trip through the profile store.
    /// </summary>
    [Serializable]
    public class Loadout
    {
        public List<LoadoutEntry> Equipped = new();

        public string Get(GearSlot slot)
        {
            foreach (var e in Equipped)
                if (e.Slot == slot) return e.ItemId;
            return null;
        }

        public void Set(GearSlot slot, string itemId)
        {
            for (int i = 0; i < Equipped.Count; i++)
            {
                if (Equipped[i].Slot != slot) continue;
                if (string.IsNullOrEmpty(itemId)) Equipped.RemoveAt(i);
                else Equipped[i].ItemId = itemId;
                return;
            }
            if (!string.IsNullOrEmpty(itemId))
                Equipped.Add(new LoadoutEntry { Slot = slot, ItemId = itemId });
        }

        public void Clear(GearSlot slot) => Set(slot, null);

        /// <summary>
        /// Summed Power of everything equipped.
        ///
        /// The RELIC contributes none, and that is the point of the socket. It grants a finisher
        /// and nothing else; counting its power would let a black-diamond weapon be worn twice -
        /// once in the hand for stats and once in the socket for the same stats again - which is
        /// the "a disguise must never be worth power" rule wearing a different hat.
        /// </summary>
        public float TotalPower()
        {
            float total = 0f;
            foreach (var e in Equipped)
            {
                if (e.Slot == GearSlot.Relic) continue;
                var item = GearCatalog.Get(e.ItemId);
                if (item != null) total += item.Power;
            }
            return total;
        }

        /// <summary>
        /// Everything equipped, summed into one block of percentage points.
        ///
        /// SUMMED, not multiplied - see <see cref="Art.Gear.StatPercents"/> for why the whole
        /// system is points. The relic is skipped for the same reason it contributes no power: it
        /// is a socket for a finisher, not a thirteenth piece of kit.
        /// </summary>
        public StatPercents TotalStats()
        {
            var total = new StatPercents();
            foreach (var e in Equipped)
            {
                if (e.Slot == GearSlot.Relic) continue;
                total.Add(GearCatalog.Get(e.ItemId)?.Grants);
            }
            return total;
        }

        public int Count => Equipped.Count;

        /// <summary>
        /// How many of the twelve WORN slots are filled. The relic is carried, not worn, so it is
        /// not one of the twelve and counting it read "13 / 12" on the loadout screen.
        /// </summary>
        public int WornCount()
        {
            int n = 0;
            foreach (var e in Equipped)
                if (e.Slot != GearSlot.Relic && !string.IsNullOrEmpty(e.ItemId)) n++;
            return n;
        }

        /// <summary>
        /// Drop entries that no longer make sense, and report how many went.
        ///
        /// Necessary because JsonUtility serialises GearSlot as an INT. Reordering the slot enum
        /// silently re-points every saved entry at whatever now holds that number - a saved glove
        /// came back as a neck item - and an item whose own Slot disagrees with the entry it sits
        /// in will render into the wrong place rather than fail loudly. Self-healing beats a
        /// version stamp here: the check is "does this item actually belong in this slot", which
        /// stays true for every future reshuffle.
        /// </summary>
        public int DropStale()
        {
            int removed = 0;
            var held = GearCatalog.Get(Get(GearSlot.Weapon));
            for (int i = Equipped.Count - 1; i >= 0; i--)
            {
                var item = GearCatalog.Get(Equipped[i].ItemId);
                if (GearSlots.Accepts(Equipped[i].Slot, item, held)) continue;
                Equipped.RemoveAt(i);
                removed++;
            }
            return removed;
        }
    }

    [Serializable]
    public class LoadoutEntry
    {
        public GearSlot Slot;
        public string ItemId;
    }
}
