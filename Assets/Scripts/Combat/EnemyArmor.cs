using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// A shield-style absorb pool standing in front of HP: fully absorbs incoming damage until
    /// it is spent, and only the remainder reaches Health. Every Basic enemy grows one of these
    /// every 20 floors (each bar worth half the enemy's own max HP, additive on top of whatever
    /// base armor its own type carries by default); Elites start with a full extra HP's worth
    /// from floor 1 and grow at the same per-20-floor rate on top of THAT - see EnemyFactory for
    /// the actual numbers, this component only knows the pool itself.
    ///
    /// Hooked in through Health.ModifyIncoming rather than a new field on Health - the same seam
    /// the exchange ledger's boons and costs already use to reach in without Health needing to
    /// know what an armor bar even is.
    /// </summary>
    public class EnemyArmor : MonoBehaviour
    {
        public float Max { get; private set; }
        public float Current { get; private set; }

        /// <summary>How much absorb capacity one visual bar represents.</summary>
        public float BarSize { get; private set; }

        /// <summary>
        /// Whole bars left to show. A bar counts as present until its OWN chunk of capacity is
        /// fully spent, not drained smoothly - "drop off a bar" is a discrete event, not a
        /// continuous shrink, so this rounds UP: a bar sitting at 1 HP of its own 40 still shows.
        /// </summary>
        public int BarsRemaining => Mathf.CeilToInt(Current / Mathf.Max(0.0001f, BarSize));
        public int TotalBars => Mathf.CeilToInt(Max / Mathf.Max(0.0001f, BarSize));

        public static EnemyArmor Attach(GameObject go, float max, float barSize)
        {
            var a = go.AddComponent<EnemyArmor>();
            a.Max = max;
            a.Current = max;
            a.BarSize = barSize;

            var health = go.GetComponent<Health>();
            if (health != null) health.ModifyIncoming = a.Absorb;
            return a;
        }

        /// <summary>
        /// Consumes as much of the incoming amount as the pool has left and returns whatever
        /// remains for Health to actually apply. Runs AFTER Vulnerability and a Mark multiplier
        /// (Health.ModifyIncoming is consulted post both), so a crit or a marked hit chews
        /// through armor at its full amplified value rather than the base one.
        /// </summary>
        /// <summary>
        /// Set for exactly one incoming hit, immediately before it is applied, by Mercury's Phase
        /// Strike. A flag rather than a parameter because absorption happens inside
        /// Health.ModifyIncoming, which takes a bare float and has no room for one - and a flag
        /// set and consumed around a single synchronous Take cannot be left dangling.
        ///
        /// It PASSES THROUGH rather than breaking: the pool is untouched, the damage simply does
        /// not meet it. That is Mercury's signature - amalgamation, not combustion - and it is why
        /// the chain is penetration rather than a damage bonus.
        /// </summary>
        public bool PierceNextHit;

        float Absorb(float amount)
        {
            if (PierceNextHit) { PierceNextHit = false; return amount; }
            if (Current <= 0f) return amount;
            float absorbed = Mathf.Min(Current, amount);
            Current -= absorbed;
            return amount - absorbed;
        }
    }
}
