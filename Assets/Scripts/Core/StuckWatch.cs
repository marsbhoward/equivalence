using UnityEngine;

namespace Convergence.Core
{
    /// <summary>
    /// Watches a run for the two shapes a softlock actually takes, and logs one line when it sees
    /// one.
    ///
    /// WHY THIS EXISTS EVEN THOUGH PLAYERS CAN REPORT BUGS: almost nobody reports. A player who
    /// gets stuck closes the app, and under the run economy that costs them only the loot they
    /// were carrying - so the cheapest thing they can do is walk away and start another run. That
    /// makes reports a filter that selects for patient players rather than for common bugs. A
    /// detector fires on every occurrence, which is the difference between knowing a floor layout
    /// can trap someone and never hearing about it.
    ///
    /// It only ever OBSERVES. There is deliberately no auto-resolve and no player-facing escape:
    /// anything that ends a run on the game's own initiative is a door out of a committed run,
    /// which is precisely what the extraction rules exist to close.
    ///
    /// The two conditions:
    ///
    ///   STALLED FLOOR   nothing alive and nothing spawning, with no floor transition happening,
    ///                   held for longer than a transition could plausibly take. This is the
    ///                   floor-clear branch in GameBootstrap failing to fire - the enemy list
    ///                   emptied and nothing picked it up.
    ///
    ///   UNREACHABLE     enemies alive, but no damage has moved in EITHER direction for a long
    ///                   window. Neither side can touch the other, which is what being walled off
    ///                   by hazard geometry looks like from the outside. Deliberately long, since
    ///                   a cautious player circling a pack is the same picture for a few seconds.
    /// </summary>
    public class StuckWatch : MonoBehaviour
    {
        /// <summary>Seconds an empty, non-spawning floor may sit before it counts as stalled.</summary>
        public const float StalledFloorSeconds = 6f;

        /// <summary>Seconds of total mutual silence, with enemies alive, before it reads as unreachable.</summary>
        public const float UnreachableSeconds = 25f;

        float _emptyFor, _silentFor;
        bool _reportedStall, _reportedSilence;
        float _lastFingerprint = float.NaN;

        /// <summary>Called when a floor actually advances, so a slow transition is not a stall.</summary>
        public void NotifyFloorChanged()
        {
            _emptyFor = 0f;
            _reportedStall = false;
        }

        /// <param name="aliveCount">Enemies currently alive.</param>
        /// <param name="settled">True when nothing is spawning and no screen is holding the floor.</param>
        /// <param name="healthFingerprint">
        /// Player HP plus the sum of living enemy HP. Damage is detected as a CHANGE in this
        /// rather than by hooking the damage paths, deliberately: there are many ways damage
        /// lands (arcs, thrown blades, projectiles, burn and bleed ticks, reflected bolts, echo
        /// duplicates) and a notification wired into some of them would report "unreachable"
        /// during a fight that was merely being won by a damage-over-time. A number that moves is
        /// proof something connected, whatever route it took.
        /// </param>
        public void Tick(int aliveCount, bool settled, float healthFingerprint)
        {
            if (!float.IsNaN(_lastFingerprint)
                && Mathf.Abs(healthFingerprint - _lastFingerprint) > 0.01f)
            {
                _silentFor = 0f;
                _reportedSilence = false;
            }
            _lastFingerprint = healthFingerprint;

            float dt = Time.unscaledDeltaTime;

            if (settled && aliveCount == 0)
            {
                _emptyFor += dt;
                if (_emptyFor > StalledFloorSeconds && !_reportedStall)
                {
                    _reportedStall = true;
                    Debug.LogWarning($"[StuckWatch] STALLED FLOOR - nothing alive, nothing spawning, " +
                                     $"no transition for {_emptyFor:F1}s. The floor-clear branch did not fire.");
                }
            }
            else _emptyFor = 0f;

            if (aliveCount > 0)
            {
                _silentFor += dt;
                if (_silentFor > UnreachableSeconds && !_reportedSilence)
                {
                    _reportedSilence = true;
                    Debug.LogWarning($"[StuckWatch] UNREACHABLE - {aliveCount} alive but no damage in either " +
                                     $"direction for {_silentFor:F1}s. Player and pack may be walled apart.");
                }
            }
            else _silentFor = 0f;
        }
    }
}
