using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.Enemies
{
    /// <summary>
    /// What each kind gains with the ENEMY BAND (Tuning.EnemyBands, the user's go-ahead 2026-10-25):
    /// band 1 they aim, band 2 a new move each, band 3 every basic carries its elite's move, band 4
    /// each kind's sharpest form. The checks live at each kind's own decision points in
    /// EnemyController; the state and the helpers they share are here.
    /// </summary>
    public partial class EnemyController
    {
        bool AtBand(int fromBand) => _band >= fromBand;

        /// <summary>A BASIC at the veterans' band: it carries its elite's move (Tuning.EnemyBands).
        /// Read wherever a behaviour (not a stat, never the art) asked the Elite bool.</summary>
        public bool Veteran => !Elite && _band >= Tuning.EnemyBands.VeteranFromBand;

        /// <summary>
        /// Set once by EnemyFactory, after the kind's stats and elite pattern: what the band changes
        /// at spawn (today, only the veterans' patterns). A VETERAN takes its kind's elite pattern at VeteranEveryMul times the interval
        /// (the Heart's cluster, a once-a-life pattern, at the elite's); the kinds whose elite move is
        /// read off the Elite bool ask <see cref="Veteran"/> where they act.
        /// </summary>
        public void ApplyBand()
        {
            if (!Veteran) return;
            switch (Kind)
            {
                case EnemyKind.Chaser:
                case EnemyKind.Ranged:
                case EnemyKind.Bomb:
                case EnemyKind.Turret:
                case EnemyKind.Dasher:
                case EnemyKind.Mortar:
                    var def = EnemyTypes.Of(Kind);
                    ElitePattern = def.Elite;
                    ElitePatternEvery = Kind == EnemyKind.Bomb
                        ? Mathf.Max(1, def.EliteEvery)
                        : Mathf.Max(1, def.EliteEvery) * Tuning.EnemyBands.VeteranEveryMul;
                    break;
            }
        }

        // ---------------------------------------------------------------- the Brute

        /// <summary>How far the Brute's swing reaches - its lunge (band 1) adds to it.</summary>
        float ChaserReach => AttackRange + (AtBand(Tuning.EnemyBands.BruteLungeFromBand) ? Tuning.EnemyBands.BruteLungeReach : 0f);

        float _lungeTimer;
        Vector2 _lungeDir;

        /// <summary>The swing STEPS IN when the player stands past its plain reach: the body is
        /// driven the gap over BruteLungeSeconds (FixedUpdate, <see cref="TickLunge"/>).</summary>
        void BeginLunge()
        {
            if (_target == null || !AtBand(Tuning.EnemyBands.BruteLungeFromBand)) return;
            Vector2 to = (Vector2)_target.position - (Vector2)transform.position;
            if (to.magnitude <= AttackRange * 0.8f) return;
            _lungeDir = to.normalized;
            _lungeTimer = Tuning.EnemyBands.BruteLungeSeconds;
        }

        /// <summary>One physics step of a lunge. True while it is driving the body.</summary>
        bool TickLunge()
        {
            if (_lungeTimer <= 0f || _rb == null) return false;
            _lungeTimer -= Time.fixedDeltaTime;
            _rb.linearDamping = 0f;
            _rb.linearVelocity = _lungeDir * (Tuning.EnemyBands.BruteLungeReach / Tuning.EnemyBands.BruteLungeSeconds);
            if (_lungeTimer <= 0f) _rb.linearVelocity = Vector2.zero;
            return true;
        }

        // The rubble throw (band 4): a kiting player out of reach is answered with a thrown stone.
        bool _rubbleTell;
        [SerializeField] float _rubbleCooldown;

        /// <summary>Whether the Brute should throw now: band 4, off cooldown, the player past its
        /// reach but within throwing range, in sight.</summary>
        bool WantsRubble(float dist)
        {
            if (!AtBand(Tuning.EnemyBands.BruteRubbleFromBand) || _rubbleCooldown > 0f) return false;
            if (dist < ChaserReach + Tuning.EnemyBands.RubbleMinBeyondReach || dist > Tuning.EnemyBands.RubbleMaxRange) return false;
            var sight = HazardQuery.Query(transform.position, _target.position, gameObject, _target.gameObject);
            return sight != SightResult.Blocked && sight != SightResult.Nulled;
        }

        /// <summary>The throw's tell: a grey line along a frozen aim, as the Stinger's.</summary>
        void BeginRubbleTell()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Growl, transform.position);
            _rubbleTell = true;
            _aimDir = ((Vector2)_target.position - (Vector2)transform.position).normalized;
            _telegraphTimer = Tuning.EnemyBands.RubbleTellSeconds;
            _telegraphVisual = new GameObject("chaser.rubble.tell");
            _tellLine = TellLine.On(_telegraphVisual, SortingOrders.Telegraph);
            _telegraphSr = null;
            UpdateRubbleTell();
        }

        void UpdateRubbleTell()
        {
            if (_telegraphVisual == null) return;
            float t = 1f - Mathf.Clamp01(_telegraphTimer / Tuning.EnemyBands.RubbleTellSeconds);
            if (_tellLine != null) _tellLine.Set(transform.position, (Vector2)transform.position + _aimDir * Tuning.EnemyBands.RubbleMaxRange,
                                                 t, Murk(new Color(0.78f, 0.74f, 0.70f, Mathf.Lerp(0.5f, 1f, t))));
        }

        void ThrowRubble()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.SwingHeavy, transform.position);
            CancelTelegraph();
            _rubbleTell = false;
            _rubbleCooldown = Tuning.EnemyBands.RubbleCooldown;
            _cooldown = Mathf.Max(_cooldown, AttackInterval);
            SwungAt = Time.time;
            BeginRecover(Tuning.Enemy.ChaserRecoverSeconds);
            ReleaseToken();
            if (_target == null || _targetHealth == null || _targetHealth.IsDead) return;
            float life = Tuning.EnemyBands.RubbleMaxRange * 1.2f / Tuning.EnemyBands.RubbleSpeed;
            EnemyProjectile.Launch(gameObject, _target, _targetHealth, transform.position, _aimDir,
                    Damage * Tuning.EnemyBands.RubbleDamageMul, AttackKnockback, Tuning.EnemyBands.RubbleSpeed, life)
                .Dress(SeekerArt.Rubble(), Tuning.EnemyBands.RubbleHitRadius, ElementType.Earth);
        }

        // ---------------------------------------------------------------- the Hound

        float _wakeAccum;

        /// <summary>The Hound's WAKE (band 2): small slowing patches along a dash, one every
        /// HoundWakeEvery units of it.</summary>
        void LeaveWake(Vector2 from, Vector2 to)
        {
            if (!AtBand(Tuning.EnemyBands.HoundWakeFromBand)) return;
            _wakeAccum += Vector2.Distance(from, to);
            while (_wakeAccum >= Tuning.EnemyBands.HoundWakeEvery)
            {
                _wakeAccum -= Tuning.EnemyBands.HoundWakeEvery;
                if (!Arena.OnFloor(to, 0.1f)) continue;
                MireField.Spawn(to, transform.parent, turned: false, staticRamp: null,
                    radius: Tuning.EnemyBands.HoundWakeRadius, seconds: Tuning.EnemyBands.HoundWakeSeconds, quiet: true);
            }
        }

        float HoundRushPast => Tuning.Enemy.DasherRushPast
            * (AtBand(Tuning.EnemyBands.HoundLongDashFromBand) ? Tuning.EnemyBands.HoundRushPastMul : 1f);

        // ---------------------------------------------------------------- the Watcher

        float RootSeconds => Tuning.Enemy.GargoylePetrifySeconds;

        /// <summary>Band 2: in the last WatcherCommitSeconds of its tell, losing sight no longer
        /// calls the root off.</summary>
        bool WatcherCommitted => AtBand(Tuning.EnemyBands.WatcherCommitFromBand)
                                 && _telegraphTimer <= Tuning.EnemyBands.WatcherCommitSeconds;

        /// <summary>The Watcher's root on the player - EXPOSING from band 4.</summary>
        void RootTarget()
        {
            var st = StatusEffects.Get(_target.gameObject);
            // Never back to back: refused while a root holds and for a grace after one (a slam's
            // damage still lands - only the root waits).
            if (!st.TryRoot(RootSeconds, Tuning.Enemy.GargoyleRootGraceSeconds)) return;
            if (AtBand(Tuning.EnemyBands.WatcherExposeFromBand))
                st.ApplyExposed(RootSeconds, Tuning.EnemyBands.WatcherExposeVulnerability);
        }

        // ---------------------------------------------------------------- the Eye, the Heart, the Bell, the Salamander

        float EyeChargeSeconds => Tuning.Enemy.TurretChargeDuration
            * (AtBand(Tuning.EnemyBands.EyeQuickFromBand) ? Tuning.EnemyBands.EyeChargeMul : 1f);

        float HeartRushFraction => AtBand(Tuning.EnemyBands.HeartRushFromBand)
            ? Tuning.EnemyBands.HeartRushFraction : Tuning.Enemy.BombCriticalFraction;

        float BellRecoverSeconds => Tuning.Enemy.BubblesRecoverSeconds
            * (AtBand(Tuning.EnemyBands.BellQuickFromBand) ? Tuning.EnemyBands.BellRecoverMul : 1f);

        float FlameReach => Tuning.Enemy.MortarFlameRange
            * (AtBand(Tuning.EnemyBands.FlameReachFromBand) ? Tuning.EnemyBands.FlameReachMul : 1f);

        // ---------------------------------------------------------------- the Lion's roar on an ally

        [SerializeField] float _lionSpeedMul = 1f;
    }
}
