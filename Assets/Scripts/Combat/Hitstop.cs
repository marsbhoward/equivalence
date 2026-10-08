using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// A brief freeze on the player landing a hit - weight, not a combat mechanic. Hooked onto a
    /// body's own Damaged event, the same "attach a listener, keep Health ignorant" shape
    /// DamageNumbers already uses (Attach/SetPlayer/Teardown mirror it exactly, and Source
    /// identity is what decides player-driven here too).
    ///
    /// Deliberately NOT GamePause - that is a reference-counted hold for menus, indefinite until
    /// released. This is a short, self-expiring freeze timed on UNSCALED seconds (Time.timeScale
    /// is what it's setting, so it cannot measure itself with scaled time) that must not fight a
    /// menu pause: Trigger never lowers an already-zero timescale below itself, and Tick only
    /// hands timescale back to 1 when GamePause is not also holding it.
    /// </summary>
    public static class Hitstop
    {
        static GameObject _player;
        static float _remaining;

        /// <summary>True while a freeze is still running - read by GamePause.Release so a screen
        /// closing under a hitstop doesn't cut it short.</summary>
        public static bool IsActive => _remaining > 0f;

        /// <summary>Set once from BuildPlayer. Null-safe elsewhere - no player means no hitstop,
        /// which is also what a torn-down run should look like.</summary>
        public static void SetPlayer(GameObject player) => _player = player;

        /// <summary>
        /// Subscribe a body to freeze on impact whenever the PLAYER hits it. Call once per Health
        /// at creation - EnemyFactory and Boss.CreateBody both do, right alongside
        /// DamageNumbers.Attach.
        /// </summary>
        public static void Attach(Health hp)
        {
            hp.Damaged += info => OnDamaged(info);
        }

        static void OnDamaged(DamageInfo info)
        {
            if (info.Amount <= 0f) return;
            if (_player == null || info.Source != _player) return;
            if (!info.IsFinisher) return;   // basics carry no hitstop - see Tuning.Hitstop
            if (info.SuppressHitstop) return;   // e.g. the katana sequence's pre-cuts
            Trigger(info.Crit ? Tuning.Hitstop.CritFinisherSeconds : Tuning.Hitstop.FinisherSeconds);
        }

        /// <summary>Called directly by a successful parry - it negates the hit rather than
        /// landing one, so there's no Damaged event to hook.</summary>
        public static void TriggerParry() => Trigger(Tuning.Hitstop.ParrySeconds);

        static void Trigger(float seconds)
        {
            if (seconds <= _remaining) return;   // keep the longer freeze already running
            _remaining = seconds;
            Time.timeScale = 0f;
        }

        /// <summary>Advanced once a frame, from GameBootstrap.Update - unscaled, since this is
        /// what's driving Time.timeScale itself.</summary>
        public static void Tick()
        {
            if (_remaining <= 0f) return;
            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f)
            {
                _remaining = 0f;
                if (!GamePause.IsPaused) Time.timeScale = GamePause.BaseScale;   // back to bullet time, if any
            }
        }

        /// <summary>Torn down between runs so a stale freeze can't outlive the run it belongs
        /// to - the same discipline DamageNumbers.Teardown follows.</summary>
        public static void Teardown()
        {
            _player = null;
            _remaining = 0f;
        }
    }
}
