using System;
using System.Collections.Generic;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// What the player HOLDS, as opposed to what exists.
    ///
    /// <see cref="GearCatalog"/> answers "what items are there"; this answers "which of them can
    /// this player pick". The gear picker, the relic socket, the perception list and the armoury
    /// wall all read it, so what the wall lights up and what the picker offers can never disagree.
    ///
    /// A player holds the minted pieces of the character being played (<see cref="HoldsInstance"/>
    /// - the catalogue keeps every character's registered, so it cannot answer that) and the
    /// <see cref="Starters"/>. The AUTHORED pieces - the gear made in development - stay with the
    /// test wallet (<see cref="AuthoredHeld"/>; the user's call, 2026-10-31). The showcase holds
    /// no starters either: its players have their kit and what they find (SHOWCASE.md).
    /// </summary>
    public static class GearOwnership
    {
        /// <summary>
        /// Items to treat as NOT held, this session only. A dev switch - there is no real
        /// ownership to take away yet, so without it the armoury's empty-slot silhouettes could
        /// never be seen. Not persisted; a domain reload clears it.
        /// </summary>
        public static HashSet<string> DevUnowned = new();

        /// <summary>Whether this player holds the authored catalogue - the Editor and the test
        /// wallet (GameBootstrap.DevGearHeld). Null holds it, as before anything is wired.</summary>
        public static Func<bool> AuthoredHeld;

        /// <summary>Whether the character being played holds this minted instance id. Null holds
        /// every registered one.</summary>
        public static Func<string, bool> HoldsInstance;

        /// <summary>Whether the <see cref="Starters"/> are held (not in the showcase).</summary>
        public static Func<bool> StartersHeld;

        /// <summary>The authored pieces every new character is given (GameBootstrap.EnsureStarterGear,
        /// CharacterProfile.NewDefault) - held by everyone outside the showcase.</summary>
        public static readonly string[] Starters = { "silver_blade", "silver_plate", "leather_pouch" };

        /// <summary>Whether this player holds <paramref name="item"/> - a minted instance of this
        /// character's, a starter, or an authored piece when the authored catalogue is held.</summary>
        public static bool Held(GearItem item)
        {
            if (item == null) return false;
            if (!string.IsNullOrEmpty(item.StackKey)) return HoldsInstance == null || HoldsInstance(item.ItemId);
            if (AuthoredHeld == null || AuthoredHeld()) return true;
            return (StartersHeld == null || StartersHeld()) && Array.IndexOf(Starters, item.ItemId) >= 0;
        }

        /// <summary>What may go in the relic socket beside <paramref name="heldWeapon"/>, of what
        /// is held (GearCatalog.RelicsFor).</summary>
        public static List<GearItem> Relics(GearItem heldWeapon)
        {
            var list = GearCatalog.RelicsFor(heldWeapon);
            list.RemoveAll(i => (i.ForgeOnly && string.IsNullOrEmpty(i.StackKey)) || !Held(i));
            return list;
        }

        /// <summary>Everything the player may pick for a slot - what the gear picker lists.</summary>
        public static List<GearItem> Owned(GearSlot slot)
        {
            var list = GearCatalog.ForSlot(slot);
            // A FORGE-ONLY design (Tria Prima and its seal) is never held from the catalogue -
            // only a minted instance wearing it is, and that instance is its own catalogue entry.
            // Without this every player would already own the one sword that has to be forged.
            list.RemoveAll(i => i.ForgeOnly && string.IsNullOrEmpty(i.StackKey));
            if (DevUnowned is { Count: > 0 }) list.RemoveAll(i => DevUnowned.Contains(i.ItemId));
            list.RemoveAll(i => !Held(i));
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
