using System.Collections.Generic;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// What the player HOLDS, as opposed to what exists.
    ///
    /// <see cref="GearCatalog"/> answers "what items are there"; this answers "which of them can
    /// this player pick". Today the two are the same list - every authored piece is offered to
    /// everyone - and the day equippable items are imported from the wallet, this is the one place
    /// that narrows. The gear picker and the armoury wall both read it, so what the wall lights up
    /// and what the picker offers can never disagree.
    /// </summary>
    public static class GearOwnership
    {
        /// <summary>
        /// Items to treat as NOT held, this session only. A dev switch - there is no real
        /// ownership to take away yet, so without it the armoury's empty-slot silhouettes could
        /// never be seen. Not persisted; a domain reload clears it.
        /// </summary>
        public static HashSet<string> DevUnowned = new();

        /// <summary>Everything the player may pick for a slot - what the gear picker lists.</summary>
        public static List<GearItem> Owned(GearSlot slot)
        {
            var list = GearCatalog.ForSlot(slot);
            // A FORGE-ONLY design (Tria Prima and its seal) is never held from the catalogue -
            // only a minted instance wearing it is, and that instance is its own catalogue entry.
            // Without this every player would already own the one sword that has to be forged.
            list.RemoveAll(i => i.ForgeOnly && string.IsNullOrEmpty(i.StackKey));
            if (DevUnowned is { Count: > 0 }) list.RemoveAll(i => DevUnowned.Contains(i.ItemId));
            return list;
        }

        public static bool Owns(GearItem item)
        {
            if (item == null) return false;
            var owned = Owned(item.Slot);
            // A Forge-only DESIGN is held through a minted instance wearing it, never directly.
            return owned.Contains(item) ||
                   (item.ForgeOnly && owned.Exists(o => o.DesignId == item.ItemId));
        }
    }
}
