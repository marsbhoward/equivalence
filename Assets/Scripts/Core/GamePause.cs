using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;

namespace Convergence.Core
{
    /// <summary>
    /// The one owner of <see cref="Time.timeScale"/>.
    ///
    /// Every screen used to save and restore the timescale itself, which works right up until two
    /// of them overlap: opening the loadout over the floor-reward screen and closing it again
    /// restored time to 1 while the reward screen was still up, so the game ran underneath a
    /// binding power-up choice. Whoever closed last won, regardless of who was still open.
    ///
    /// Reference-counted instead: time resumes when the LAST holder releases, so screens can nest
    /// in any order without knowing about each other.
    /// </summary>
    public static class GamePause
    {
        static readonly HashSet<Object> _holders = new();

        public static bool IsPaused => _holders.Count > 0;
        public static int HolderCount => _holders.Count;

        public static void Hold(Object who)
        {
            if (who == null) return;
            _holders.Add(who);
            Time.timeScale = 0f;
        }

        public static void Release(Object who)
        {
            if (who == null) return;
            _holders.Remove(who);
            Prune();
            // A hitstop mid-freeze must not be cut short by a screen closing under it - Hitstop's
            // own Tick is what hands timescale back once ITS freeze ends.
            if (_holders.Count == 0 && !Hitstop.IsActive) Time.timeScale = 1f;
        }

        /// <summary>
        /// Run teardown destroys screens outright, and a destroyed holder can never release. Drop
        /// them here so one torn-down screen cannot freeze the game forever.
        /// </summary>
        static void Prune() => _holders.RemoveWhere(o => o == null);

        /// <summary>Hard reset for run boundaries, where no screen should still be holding.</summary>
        public static void ReleaseAll()
        {
            _holders.Clear();
            Time.timeScale = 1f;
        }
    }
}
