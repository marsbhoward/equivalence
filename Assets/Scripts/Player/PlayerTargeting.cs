using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Enemies;

namespace Convergence.Player
{
    /// <summary>
    /// Picks the enemy the character faces and attacks.
    ///
    /// Targeting is STICKY rather than strictly nearest-every-frame. Two enemies at almost the
    /// same distance would otherwise make the character flip back and forth every frame, which
    /// looks broken and makes the attack arc miss both. So the current target is kept until it
    /// dies, leaves <see cref="DropRange"/>, or another enemy becomes decisively closer
    /// (<see cref="SwitchAdvantage"/>).
    /// </summary>
    public class PlayerTargeting : MonoBehaviour
    {
        // Central values: Tuning.Targeting.

        [Tooltip("Lock on only within the player's own reach (plus AcquireMargin) rather than " +
                 "at a fixed distance. Off falls back to AcquireRange.")]
        public bool AcquireWithinReach = true;

        [Tooltip("World units of lock-on reach beyond the pending swing's own range, so the " +
                 "character starts turning a step before an enemy is actually in range rather " +
                 "than exactly as it arrives. Central value: Tuning.Targeting.AcquireMargin.")]
        public float AcquireMargin = Tuning.Targeting.AcquireMargin;

        [Tooltip("Fallback acquire distance when AcquireWithinReach is off.")]
        public float AcquireRange = Tuning.Targeting.AcquireRange;

        [Tooltip("How far past the acquire distance a held target may drift before it is " +
                 "dropped, in world units. Above zero, or a target sitting exactly on the " +
                 "boundary flickers in and out every frame.")]
        public float DropMargin = Tuning.Targeting.DropMargin;

        [Tooltip("Unused while AcquireWithinReach is on - kept for the fixed-range fallback.")]
        public float DropRange = Tuning.Targeting.DropRange;

        /// <summary>
        /// Resolved lazily: the factory adds this component before the controller, so capturing
        /// it in Awake would leave it permanently null and silently fall back to fixed range.
        /// </summary>
        PlayerController _playerCache;
        PlayerController Player => _playerCache != null
            ? _playerCache
            : _playerCache = GetComponent<PlayerController>();

        /// <summary>
        /// How far a lock can be made this frame.
        ///
        /// Tied to the weapon's ENGAGEMENT reach plus <see cref="AcquireMargin"/> - which for a
        /// melee weapon is its swing, and for a thrown one is how far it throws. See
        /// PlayerController.AcquireReach. It grows when a long finisher is
        /// banked and shrinks back for a basic. Locking at a fixed 14 units meant the character
        /// was permanently turned toward something far out of range, which broke every deliberate
        /// use of facing: throwing the blade somewhere, lining up a cone, or simply running where
        /// you are looking.
        ///
        /// The margin exists because locking at EXACTLY reach read as late - the turn began only
        /// once the enemy was already hittable, so it carried no anticipation. A step of warning
        /// is not the same thing as targeting across the room.
        /// </summary>
        float Acquire => AcquireWithinReach && Player != null
            ? Player.AcquireReach + Mathf.Max(0f, AcquireMargin)
            : AcquireRange;

        /// <summary>Hysteresis band: always wider than <see cref="Acquire"/>, never equal to it.</summary>
        float Drop => AcquireWithinReach ? Acquire + Mathf.Max(0.05f, DropMargin) : DropRange;

        /// <summary>
        /// The distance a lock can actually be made at right now, for anything drawing it.
        /// Published because <c>AcquireRange</c> is only the fallback and reading that field
        /// would draw a 14-unit ring around a character who locks on at two.
        /// </summary>
        public float AcquireRadius => Acquire;

        [Tooltip("A rival must be this fraction of the current target's distance to steal focus. " +
                 "1 = switch to any closer enemy (jittery); lower = stickier.")]
        [Range(0.1f, 1f)] public float SwitchAdvantage = Tuning.Targeting.SwitchAdvantage;

        /// <summary>The run's ledger. Wandering Eye loosens the stickiness below.</summary>
        public System.Func<Exchange.Mods> ModsSource;
        static readonly Exchange.Mods NoMods = new();
        Exchange.Mods Mods => ModsSource?.Invoke() ?? NoMods;

        /// <summary>
        /// Stickiness in play. Higher means a rival steals focus more easily, so Wandering Eye
        /// ADDS to it. Clamped below 1: at 1 every closer enemy takes the lock and the character
        /// flickers between two equidistant bodies every frame, which is the exact failure this
        /// whole mechanism exists to prevent.
        /// </summary>
        float CurrentSwitchAdvantage
            => Mathf.Clamp(SwitchAdvantage + Mods.SwitchAdvantageDelta, 0.1f, 0.98f);

        public Health Target { get; private set; }
        public bool HasTarget => Target != null && !Target.IsDead;

        void Update()
        {
            EnemyRegistry.Prune();
            Retarget();
        }

        void Retarget()
        {
            var origin = (Vector2)transform.position;

            // Drop a target that died or wandered off.
            if (Target != null)
            {
                if (Target.IsDead || Vector2.Distance(origin, Target.transform.position) > Drop)
                    Target = null;
            }

            float currentDist = Target != null
                ? Vector2.Distance(origin, Target.transform.position)
                : float.MaxValue;

            // A room's interior wall between the player and the lock: any enemy in the open is
            // the better target - the shot would die on the wall, the swing could not reach.
            // Kept when nothing else is in view, so the rim still says where the pack is.
            bool walled = Target != null && Core.Arena.WallBetween(origin, Target.transform.position);

            Health best = null;
            float bestDist = float.MaxValue;

            foreach (var enemy in EnemyRegistry.All)
            {
                if (enemy == null) continue;
                var hp = enemy.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;

                float d = Vector2.Distance(origin, enemy.transform.position);
                if (d > Acquire || d >= bestDist) continue;
                if (Core.Arena.WallBetween(origin, enemy.transform.position)) continue;

                best = hp;
                bestDist = d;
            }

            if (best == null) return;

            // The run's ledger may take the choice over: Blind Rage picks at random within reach,
            // Basilisk the weakest.
            var mode = Mods.Targeting;
            if (mode != Exchange.TargetMode.Normal)
            {
                PickByMode(mode, origin);
                return;
            }

            // Take the new target only if there is nothing held, or the rival is decisively closer.
            // The ledger's Wandering Eye makes "decisively" easier to meet.
            if (Target == null || walled || (best != Target && bestDist < currentDist * CurrentSwitchAdvantage))
                Target = best;
        }

        float _repickIn;

        /// <summary>
        /// BLIND RAGE: a target drawn at random from everything in reach, kept until it dies or
        /// leaves - or for a moment and a half, whichever is first. BASILISK: always the enemy in
        /// reach with the least health left.
        /// </summary>
        void PickByMode(Exchange.TargetMode mode, Vector2 origin)
        {
            var near = new List<Health>();
            foreach (var enemy in EnemyRegistry.All)
            {
                if (enemy == null) continue;
                var hp = enemy.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                if (Vector2.Distance(origin, enemy.transform.position) > Acquire) continue;
                if (Core.Arena.WallBetween(origin, enemy.transform.position)) continue;
                near.Add(hp);
            }
            if (near.Count == 0) return;

            if (mode == Exchange.TargetMode.Weakest)
            {
                Health weakest = null;
                foreach (var hp in near)
                    if (weakest == null || hp.Current < weakest.Current - 0.01f) weakest = hp;
                Target = weakest;
                return;
            }

            _repickIn -= Time.deltaTime;
            if (Target != null && !Target.IsDead && near.Contains(Target) && _repickIn > 0f) return;
            Target = near[Random.Range(0, near.Count)];
            _repickIn = 1.5f;
        }
    }
}
