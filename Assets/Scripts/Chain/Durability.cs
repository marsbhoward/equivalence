using System;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;

namespace Convergence.Chain
{
    /// <summary>
    /// Wear on each owned item, keyed by item ID like the loadout is.
    ///
    /// Durability changes constantly during a fight, so it is mutated in memory and only written
    /// at a run boundary - the same discipline the rest of the chain layer follows. Nothing here
    /// ever triggers a per-hit transaction.
    ///
    /// Two effects, deliberately different:
    ///   ARMOUR  wears when the player is HIT   -> damage taken goes UP
    ///   WEAPONS wear when the player LANDS hits -> damage dealt goes DOWN
    /// </summary>
    [Serializable]
    public class Durability
    {
        public List<DurabilityEntry> Entries = new();

        /// <summary>How much worse things get at zero durability.</summary>
        public const float MaxDamageTakenPenalty = 0.5f;   // +50% incoming
        public const float MaxDamageDealtPenalty = 0.4f;   // -40% outgoing

        /// <summary>
        /// Effective ceiling for one item's pool. <paramref name="armorBonus"/> is the character's
        /// total ARMOUR stat (percentage points, gear + mastery) - it scales every armour-kind
        /// item's own pool up together, rather than being a pool of its own to track and drain.
        /// </summary>
        static float EffectiveMax(GearItem item, float armorBonus)
        {
            float max = item != null ? item.MaxDurability : 100f;
            return StatPercents.Apply(max, armorBonus);
        }

        public float Get(string itemId, float armorBonus = 0f)
        {
            var item = GearCatalog.Get(itemId);
            float max = EffectiveMax(item, armorBonus);
            foreach (var e in Entries)
                if (e.ItemId == itemId) return Mathf.Clamp(e.Current, 0f, max);
            return max;   // unseen items start pristine
        }

        public void Set(string itemId, float value, float armorBonus = 0f)
        {
            var item = GearCatalog.Get(itemId);
            float max = EffectiveMax(item, armorBonus);
            value = Mathf.Clamp(value, 0f, max);

            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].ItemId != itemId) continue;
                Entries[i].Current = value;
                return;
            }
            Entries.Add(new DurabilityEntry { ItemId = itemId, Current = value });
        }

        public float Fraction01(string itemId, float armorBonus = 0f)
        {
            var item = GearCatalog.Get(itemId);
            float max = EffectiveMax(item, armorBonus);
            return max <= 0f ? 1f : Mathf.Clamp01(Get(itemId, armorBonus) / max);
        }

        /// <summary>
        /// Spend durability across every equipped item of one kind. <paramref name="amount"/> is
        /// the RAW wear - fold DamageResistance and any ledger ArmourWearMul into it at the call
        /// site, the same way both already have to be composed together there.
        /// </summary>
        public void Wear(Loadout loadout, SlotKind kind, float amount, float armorBonus = 0f)
        {
            if (loadout == null || amount <= 0f) return;
            foreach (var entry in loadout.Equipped)
            {
                if (GearSlots.Kind(entry.Slot) != kind) continue;
                Set(entry.ItemId, Get(entry.ItemId, armorBonus) - amount, armorBonus);
            }
        }

        /// <summary>Restore a fraction of every equipped item's pool. The repair reward.</summary>
        public int Repair(Loadout loadout, float fractionOfMax, float armorBonus = 0f)
        {
            if (loadout == null) return 0;
            int touched = 0;
            foreach (var entry in loadout.Equipped)
            {
                if (GearSlots.Kind(entry.Slot) == SlotKind.Cosmetic) continue;
                var item = GearCatalog.Get(entry.ItemId);
                float max = EffectiveMax(item, armorBonus);
                float before = Get(entry.ItemId, armorBonus);
                if (before >= max) continue;
                Set(entry.ItemId, before + max * fractionOfMax, armorBonus);
                touched++;
            }
            return touched;
        }

        /// <summary>Mean condition of everything equipped in one kind. 1 = pristine.</summary>
        public float Condition01(Loadout loadout, SlotKind kind, float armorBonus = 0f)
        {
            if (loadout == null) return 1f;
            float sum = 0f; int n = 0;
            foreach (var entry in loadout.Equipped)
            {
                if (GearSlots.Kind(entry.Slot) != kind) continue;
                sum += Fraction01(entry.ItemId, armorBonus);
                n++;
            }
            return n == 0 ? 1f : sum / n;
        }

        /// <summary>Incoming damage multiplier. 1 pristine, up to 1.5 with armour destroyed.</summary>
        public float DamageTakenMultiplier(Loadout loadout, float armorBonus = 0f)
            => 1f + (1f - Condition01(loadout, SlotKind.Armor, armorBonus)) * MaxDamageTakenPenalty;

        /// <summary>
        /// Outgoing damage multiplier. Currently ALWAYS 1 - weapon degradation is switched off.
        ///
        /// A blunting weapon punished the player for the one thing the game most wants them to
        /// do (keep attacking), and did it invisibly: the loss spreads across every swing instead
        /// of landing on any one of them, so it reads as the game feeling worse rather than as a
        /// cost being paid. The idea may return as SHARPNESS - something spent and restored
        /// rather than a decay that only ever goes down - so the plumbing stays and only the
        /// effect is neutralised.
        ///
        /// Armour wear is unaffected; see <see cref="DamageTakenMultiplier"/>.
        /// </summary>
        public float DamageDealtMultiplier(Loadout loadout) => 1f;
    }

    [Serializable]
    public class DurabilityEntry
    {
        public string ItemId;
        public float Current;
    }
}
