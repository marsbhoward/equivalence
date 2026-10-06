using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// Resolves item IDs to items. Mirrors what the backend will eventually do: the character
    /// datum holds IDs, and something else turns those into the art and stats they represent.
    ///
    /// Authored items live in Assets/Resources/Gear/. When that folder is empty the catalog
    /// falls back to <see cref="DemoGear"/> so gear swapping is visible before any art exists.
    /// </summary>
    public static class GearCatalog
    {
        static Dictionary<string, GearItem> _byId;

        public static IReadOnlyDictionary<string, GearItem> All
        {
            get { EnsureLoaded(); return _byId; }
        }

        public static GearItem Get(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            EnsureLoaded();
            return _byId.GetValueOrDefault(itemId);
        }

        /// <summary>Every item that can go in a given slot - what an equip screen would list.</summary>
        public static List<GearItem> ForSlot(GearSlot slot)
        {
            EnsureLoaded();
            var result = new List<GearItem>();
            foreach (var item in _byId.Values)
                if (item.Slot == slot) result.Add(item);
            return result;
        }

        /// <summary>
        /// What may go in the relic socket while <paramref name="heldWeapon"/> is being wielded.
        ///
        /// Not <see cref="ForSlot"/>: a relic candidate declares Slot.Weapon, because the same item
        /// has to stay wieldable. The socket's rule lives in <see cref="GearSlots.Accepts"/> so the
        /// screen that offers relics and the loader that validates saved ones cannot drift apart.
        /// </summary>
        public static List<GearItem> RelicsFor(GearItem heldWeapon)
        {
            EnsureLoaded();
            var result = new List<GearItem>();
            foreach (var item in _byId.Values)
                if (GearSlots.Accepts(GearSlot.Relic, item, heldWeapon)) result.Add(item);
            return result;
        }

        public static void Invalidate() => _byId = null;

        /// <summary>
        /// Adds one item to the catalog at runtime - a minted instance (see MintedGearRecord),
        /// not a hand-authored asset. Call again after Invalidate() or a domain reload, since
        /// EnsureLoaded's normal pass only knows about Resources/DemoGear and has no way to
        /// remember a runtime registration on its own.
        /// </summary>
        public static void Register(GearItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.ItemId)) return;
            EnsureLoaded();
            _byId[item.ItemId] = item;
        }

        static void EnsureLoaded()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, GearItem>();

            foreach (var item in Resources.LoadAll<GearItem>("Gear"))
            {
                if (string.IsNullOrEmpty(item.ItemId))
                {
                    Debug.LogWarning($"[GearCatalog] '{item.name}' has no ItemId - skipped.");
                    continue;
                }
                _byId[item.ItemId] = item;
            }

            if (_byId.Count == 0)
                foreach (var item in DemoGear.Build()) _byId[item.ItemId] = item;
        }
    }
}
