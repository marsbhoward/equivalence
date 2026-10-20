using UnityEngine;

namespace Convergence.Core
{
    /// <summary>
    /// THE RUN CLOCK and the HEAT it sets (SHORT-DESCENT.md, "Difficulty: depth and the clock").
    ///
    /// The clock runs while a floor is being fought: from the floor's start (its entering snapshot)
    /// to its clear, less a few seconds of walking in - exactly the rule the SERVICE times it by
    /// (web/service/src/runProgress.ts), so the hourglass and the weekly guarantee never disagree.
    /// It counts REAL time: hit-stops and bullet time do not slow it, and the pause menu and a
    /// hidden tab during a fight are on it (the user's call, 2026-10-20 - the pause screen shows it
    /// still running). Menus after a clear are off it.
    ///
    ///     floor start         an ATTEMPT opens (OpenAttempt)
    ///     the clear           it closes - its seconds join the clock (CloseAttempt)
    ///     a resume            the clock comes back as the last heartbeat or snapshot had it
    ///                         (Restore), and the replayed floor opens a new attempt
    ///
    /// HEAT (the Short Descent only - Classic's is always 1) multiplies every hit the player takes,
    /// stepping up each ten minutes and compounding past the hour, in the GATE COLLAPSE, with no
    /// cap. A BOSS floor locks the heat at the boss's arrival (its adds and hazards with it).
    /// Mechanic hits sized as a share of max health ignore it (DamageInfo.IgnoresHeat).
    ///
    /// A component on the bootstrap's object, its state in serialized fields: a script edit in Play
    /// mode reloads the domain, and a static clock would come back at zero mid-run.
    /// </summary>
    public class RunClock : MonoBehaviour
    {
        [SerializeField] float _closed;
        [SerializeField] double _attemptAt = -1;
        [SerializeField] int _attemptFloor;
        [SerializeField] bool _locked;
        [SerializeField] float _lockedHeat = 1f;

        static RunClock _current;

        /// <summary>The live clock, found again after a domain reload.</summary>
        public static RunClock Current => _current != null ? _current : _current = FindAnyObjectByType<RunClock>();

        void OnEnable() => _current = this;

        static double Now => Time.realtimeSinceStartupAsDouble;

        /// <summary>Seconds of run clock: every closed attempt, and the one open, to this moment.</summary>
        public float Seconds
            => _closed + (_attemptAt >= 0 ? Mathf.Max(0f, (float)(Now - _attemptAt) - Tuning.RunClock.WalkInSeconds) : 0f);

        public bool AttemptOpen => _attemptAt >= 0;

        /// <summary>A run starts at zero, or a resume where the run left off.</summary>
        public void Restore(float seconds)
        {
            _closed = Mathf.Max(0f, seconds);
            _attemptAt = -1;
            _attemptFloor = 0;
            _locked = false;
        }

        /// <summary>A floor starts. An attempt still open (a floor abandoned) counts to now first.</summary>
        public void OpenAttempt(int floor)
        {
            if (_attemptAt >= 0) CloseAttempt();
            _attemptAt = Now;
            _attemptFloor = floor;
            _locked = false;
        }

        /// <summary>The floor is cleared: its seconds join the clock, and any boss lock lifts.</summary>
        public void CloseAttempt()
        {
            _closed = Seconds;
            _attemptAt = -1;
            _locked = false;
        }

        /// <summary>A boss has arrived: its floor fights at the heat of this moment to the end.</summary>
        public void LockHeat()
        {
            _lockedHeat = HeatAt(Seconds);
            _locked = true;
        }

        /// <summary>The heat now - the multiplier on every hit the player takes. 1 outside the Short
        /// Descent.</summary>
        public float Heat => !RunFormat.Short ? 1f : _locked ? _lockedHeat : HeatAt(Seconds);

        /// <summary>What heat does to one incoming hit: nothing to a hit that ignores it.</summary>
        public static float IncomingScale(Combat.DamageInfo info)
            => info.IgnoresHeat || Current == null ? 1f : Current.Heat;

        // ------------------------------------------------------------------ the tiers

        /// <summary>The tier a moment of clock is in: 0..5 the ten-minute tiers, 6 the Gate Collapse.</summary>
        public static int TierAt(float seconds)
            => Mathf.Clamp(Mathf.FloorToInt(seconds / Tuning.RunClock.TierSeconds), 0, Tuning.RunClock.Tiers);

        public static bool InCollapse(float seconds) => seconds >= Tuning.RunClock.CollapseAtSeconds;

        /// <summary>
        /// The heat at a moment of clock: x TierStep per ten-minute tier (1.00 .. 1.40), then in the
        /// Gate Collapse x CollapseStep more every five minutes past the hour - uncapped (the user's
        /// call), so a stalled run ends.
        /// </summary>
        public static float HeatAt(float seconds)
        {
            var t = Tuning.RunClock.Tiers;
            float heat = Mathf.Pow(Tuning.RunClock.TierStep, Mathf.Min(TierAt(seconds), t - 1));
            if (InCollapse(seconds))
            {
                int steps = Mathf.FloorToInt((seconds - Tuning.RunClock.CollapseAtSeconds) / Tuning.RunClock.CollapseStepSeconds);
                heat *= Mathf.Pow(Tuning.RunClock.CollapseStep, steps);
            }
            return heat;
        }

        /// <summary>
        /// The heat an ON-PACE player carries on <paramref name="floor"/> (60 minutes over the run's
        /// floors), smoothed and centred on its tier - what the Short Descent's depth damage is
        /// divided by, so an on-pace player meets Classic's damage curve within half a step
        /// (FloorDifficulty.Damage). 1 in Classic.
        /// </summary>
        public static float OnPaceHeat(int floor)
        {
            if (!RunFormat.Short) return 1f;
            float minutes = floor * Tuning.RunClock.CollapseAtSeconds / 60f / RunFormat.FinalFloor;
            return Mathf.Pow(Tuning.RunClock.TierStep, minutes / (Tuning.RunClock.TierSeconds / 60f) - 0.5f);
        }

        /// <summary>The tiers' names - the seven operations, the last the user's GATE COLLAPSE.</summary>
        public static string TierName(int tier) => tier switch
        {
            0 => "Calcination",
            1 => "Dissolution",
            2 => "Separation",
            3 => "Conjunction",
            4 => "Fermentation",
            5 => "Distillation",
            _ => "Gate Collapse",
        };
    }
}
