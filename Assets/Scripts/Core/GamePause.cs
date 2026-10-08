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

        // ---- slow motion ----
        //
        // A third state between running and paused: BULLET TIME (the bow's Prima Materia art). Each
        // holder asks for a scale; the slowest wins, and it is what time comes BACK to when a
        // screen closes or a hit-stop ends - both used to resume straight to 1, which would cut
        // bullet time off at its first hit. Not a dictionary of interface or struct shapes a
        // domain reload can't restore - and if one empties it, time simply runs at 1 again.
        static readonly Dictionary<Object, float> _slows = new();

        /// <summary>What time runs at when nothing is pausing or freezing it: 1, or the slowest
        /// slow-motion being held.</summary>
        public static float BaseScale
        {
            get
            {
                float s = 1f;
                List<Object> dead = null;
                foreach (var kv in _slows)
                {
                    if (kv.Key == null) { (dead ??= new List<Object>()).Add(kv.Key); continue; }
                    s = Mathf.Min(s, kv.Value);
                }
                if (dead != null) foreach (var d in dead) _slows.Remove(d);
                return s;
            }
        }

        /// <summary>Hold time at <paramref name="scale"/> (0..1) until <see cref="Unslow"/>. A pause
        /// or a hit-stop still stops it outright, and hands back to this when they end.</summary>
        public static void Slow(Object who, float scale)
        {
            if (who == null) return;
            _slows[who] = Mathf.Clamp(scale, 0.01f, 1f);
            if (!IsPaused && !Hitstop.IsActive) Time.timeScale = BaseScale;
        }

        public static void Unslow(Object who)
        {
            if (who == null || !_slows.Remove(who)) return;
            if (!IsPaused && !Hitstop.IsActive) Time.timeScale = BaseScale;
        }
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
            if (_holders.Count == 0 && !Hitstop.IsActive) Time.timeScale = BaseScale;
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
            _slows.Clear();
            Time.timeScale = 1f;
        }
    }
}
