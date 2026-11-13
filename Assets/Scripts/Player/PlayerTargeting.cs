using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Enemies;

namespace Convergence.Player
{
    /// <summary>
    /// The enemy the character's attacks go at: the LOCK when one is held, else the SOFT ASSIST
    /// (the user's calls, 2026-11-12 - lock-on replaces auto-target).
    ///
    /// THE LOCK is the player's: Tab (the enemy under the cursor), R3 (the one nearest the aim or the
    /// facing), or a tap on an enemy makes it; the same again lets it go; a flick of the right stick
    /// moves it to the next enemy that way. It holds the character's facing (PlayerController) and
    /// survives walls; a kill moves it to the nearest enemy in range, and it drops past <see cref="Drop"/>.
    /// Its ranges are auto-target's, per weapon (the user's call, 2026-11-12): made within the weapon's
    /// engagement reach plus AcquireMargin, plus LockExtra (half a dash, 2026-11-12), dropped DropMargin past that. Wandering Eye breaks it on a hit (I) and a defensive ability
    /// (II); Blind Rage (III) allows none.
    ///
    /// THE ASSIST, with no lock: the enemy in reach within AssistConeDegrees of the aim (or the facing),
    /// not behind a wall. Nothing holds it between swings - a swing turns to it as it starts
    /// (PlayerController.DoAttack), and everything that reads <see cref="Target"/> (throws, arts, the rim)
    /// goes at it.
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

        /// <summary>
        /// The distance a lock can actually be made at right now, for anything drawing it.
        /// Published because <c>AcquireRange</c> is only the fallback and reading that field
        /// would draw a 14-unit ring around a character who locks on at two.
        /// </summary>
        public float AcquireRadius => Acquire;

        /// <summary>A held lock drops past this: always wider than <see cref="Acquire"/>, or a target sitting
        /// on the edge flickers in and out.</summary>
        float Drop => LockReach + Mathf.Max(0.05f, Tuning.Targeting.DropMargin);

        /// <summary>How far a lock can be made: the assist's reach plus Tuning.Targeting.LockExtra (half a dash).</summary>
        float LockReach => Acquire + Tuning.Targeting.LockExtra;

        /// <summary>The run's ledger: Wandering Eye.</summary>
        public System.Func<Exchange.Mods> ModsSource;
        static readonly Exchange.Mods NoMods = new();
        Exchange.Mods Mods => ModsSource?.Invoke() ?? NoMods;

        /// <summary>The lock (the player's choice), or null.</summary>
        public Health Locked { get; private set; }

        /// <summary>With no lock: the soft assist's pick this frame, or null.</summary>
        public Health Assist { get; private set; }

        /// <summary>What attacks go at: the lock, else the assist.</summary>
        public Health Target => Locked != null ? Locked : Assist;
        public bool HasTarget => Target != null && !Target.IsDead;
        public bool IsLocked => Locked != null && !Locked.IsDead;

        /// <summary>Let go of the lock (Wandering Eye; PlayerController on a defensive ability).</summary>
        public void BreakLock() => Locked = null;

        /// <summary>A body going into a chasm is out of the fight before it is dead: its rim is
        /// not masked with it, and would be left falling outside the hole.</summary>
        static bool Gone(Health h) => h.TryGetComponent<EnemyController>(out var e) && e.FellIn;

        // Re-hooked lazily: a domain reload empties the event's delegate list.
        [System.NonSerialized] Health _hookedHealth;

        void Update()
        {
            EnemyRegistry.Prune();
            HookHits();
            Relock();
            AssistPick();
        }

        void HookHits()
        {
            var own = GetComponent<Health>();
            if (own == _hookedHealth) return;
            if (_hookedHealth != null) _hookedHealth.Damaged -= OnHit;
            _hookedHealth = own;
            if (own != null) own.Damaged += OnHit;
        }

        void OnDestroy()
        {
            if (_hookedHealth != null) _hookedHealth.Damaged -= OnHit;
        }

        /// <summary>Wandering Eye I: a hit taken lets go of the lock - not a burn's tick, nor a price paid.</summary>
        void OnHit(DamageInfo info)
        {
            if (info.Tick || info.Price || info.Amount <= 0f) return;
            if (Mods.LockBreaksOnHit) BreakLock();
        }

        // ---------------------------------------------------------------- the lock

        void Relock()
        {
            var origin = (Vector2)transform.position;
            if (Mods.NoLock) { Locked = null; return; }

            // A kill moves the lock on to the nearest enemy in range; drifting out of range drops it.
            if (Locked != null)
            {
                if (Locked.IsDead || Gone(Locked))
                    Locked = Nearest(origin, LockReach, null);
                else if (Vector2.Distance(origin, Locked.transform.position) > Drop)
                    Locked = null;
            }

            // Nothing in the world takes input under a screen.
            if (GamePause.IsPaused) return;

            foreach (var tap in Controls.WorldTaps)
            {
                var hp = AtScreenPoint(tap);
                if (hp == null) continue;
                Locked = hp == Locked ? null : hp;   // tap it again to let go
                return;
            }

            if (Controls.LockTapped)
            {
                var point = Controls.LockPointScreen;
                var under = point.HasValue ? AtScreenPoint(point.Value) : null;
                if (Locked != null)
                    Locked = under != null && under != Locked ? under : null;   // the cursor on another enemy moves it
                else
                    Locked = under != null ? under : InCone(origin, AimOrFacing(), LockReach,
                                                            Tuning.Targeting.LockConeDegrees, walls: false)
                                                     ?? Nearest(origin, LockReach, null);
                return;
            }

            var flick = Controls.LockFlick;
            if (Locked != null && flick.HasValue)
            {
                var from = (Vector2)Locked.transform.position;
                var next = InCone(from, flick.Value, LockReach,
                                  Tuning.Targeting.FlickConeDegrees, walls: false, skip: Locked, rangeFrom: origin);
                if (next != null) Locked = next;
            }
        }

        /// <summary>The aim when there is one, else the character's facing.</summary>
        Vector2 AimOrFacing()
        {
            var aim = Controls.Aim;
            if (aim.HasValue && aim.Value.sqrMagnitude > 0.0001f) return aim.Value.normalized;
            return Player != null ? Player.Facing : Vector2.right;
        }

        /// <summary>The live enemy nearest the world point under <paramref name="screen"/>, within
        /// Tuning.Targeting.LockPickRadius of it and LockReach of the character.</summary>
        Health AtScreenPoint(Vector2 screen)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            Vector2 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            var origin = (Vector2)transform.position;
            Health best = null;
            float bestD = Tuning.Targeting.LockPickRadius;
            foreach (var enemy in EnemyRegistry.All)
            {
                var hp = Live(enemy);
                if (hp == null) continue;
                if (Vector2.Distance(origin, enemy.transform.position) > LockReach) continue;
                float d = Vector2.Distance(world, enemy.transform.position);
                if (d < bestD) { best = hp; bestD = d; }
            }
            return best;
        }

        static Health Live(EnemyController enemy)
        {
            if (enemy == null || enemy.FellIn) return null;
            var hp = enemy.GetComponent<Health>();
            return hp == null || hp.IsDead ? null : hp;
        }

        Health Nearest(Vector2 origin, float range, Health skip)
        {
            Health best = null;
            float bestD = range;
            foreach (var enemy in EnemyRegistry.All)
            {
                var hp = Live(enemy);
                if (hp == null || hp == skip) continue;
                float d = Vector2.Distance(origin, enemy.transform.position);
                if (d > bestD) continue;
                if (Core.Arena.WallBetween(origin, enemy.transform.position)) continue;
                best = hp; bestD = d;
            }
            return best;
        }

        /// <summary>
        /// The enemy within <paramref name="cone"/> degrees of <paramref name="dir"/> from
        /// <paramref name="from"/>, scored by distance stretched by its angle off the line - so one
        /// straight ahead beats a nearer one at the cone's edge. Range is measured from
        /// <paramref name="rangeFrom"/> (the character) when given.
        /// </summary>
        Health InCone(Vector2 from, Vector2 dir, float range, float cone, bool walls,
                      Health skip = null, Vector2? rangeFrom = null)
        {
            var origin = rangeFrom ?? from;
            Health best = null;
            float bestScore = float.MaxValue;
            foreach (var enemy in EnemyRegistry.All)
            {
                var hp = Live(enemy);
                if (hp == null || hp == skip) continue;
                Vector2 pos = enemy.transform.position;
                if (Vector2.Distance(origin, pos) > range) continue;
                Vector2 to = pos - from;
                float d = to.magnitude;
                if (d < 0.0001f) continue;
                float angle = Vector2.Angle(dir, to);
                if (angle > cone) continue;
                if (walls && Core.Arena.WallBetween(origin, pos)) continue;
                float score = d * (1f + angle / cone);
                if (score < bestScore) { best = hp; bestScore = score; }
            }
            return best;
        }

        // ---------------------------------------------------------------- the soft assist

        void AssistPick()
        {
            if (Locked != null) { Assist = null; return; }
            var origin = (Vector2)transform.position;
            Assist = InCone(origin, AimOrFacing(), Acquire, Tuning.Targeting.AssistConeDegrees, walls: true);
        }
    }
}
