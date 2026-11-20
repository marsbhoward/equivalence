using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.Enemies
{
    /// <summary>What a Maelstrom is doing (MaelstromVisual draws it from this).</summary>
    public enum MaelstromPhase { Surface, Dive, Under, Tell, Rise }

    /// <summary>
    /// THE MAELSTROM (the user's design 2026-11-20, mock https://claude.ai/artifact/AeFpbH6KKevKh6F9F6bURn;
    /// Tuning.Enemy.Maelstrom*, Tuning.EnemyBands.Maelstrom*). A figure of water that DRAINS into the
    /// floor, follows the player beneath it as a ripple, marks a ring UNDER them (its attack turn, its
    /// tell) and bursts up in a whirlpool there - then stands surfaced, open to hits, until it dives again.
    ///
    /// Beneath the floor it is SUBMERGED: immune, its collider off, skipped by targeting - but its ripple
    /// is always drawn (MaelstromWater), so the player never loses it. The whirlpool is drawn, never a pull:
    /// enemies never move the player, so the ring is the whole danger.
    /// </summary>
    public partial class EnemyController
    {
        [SerializeField] MaelstromPhase _maelPhase;
        [SerializeField] float _maelTimer;
        [SerializeField] Vector2 _maelRing;
        [SerializeField] bool _submerged;
        /// <summary>The second eruption (band 3, elites): seconds until it, and where.</summary>
        [SerializeField] float _maelSecondIn;
        [SerializeField] Vector2 _maelSecondAt;
        [SerializeField] bool _maelFirstDive;
        BlastTell _maelTell, _maelSecondTell;
        MaelstromWater _maelWater;

        /// <summary>Beneath the floor: nothing can hit it and nothing locks on to it.</summary>
        public bool Submerged => _submerged;
        public MaelstromPhase MaelPhase => _maelPhase;
        /// <summary>0 as it starts draining away, 1 gone (Dive); 0 as it starts pouring up, 1 risen (Rise).</summary>
        public float MaelProgress01 => _maelPhase switch
        {
            MaelstromPhase.Dive => 1f - Mathf.Clamp01(_maelTimer / Tuning.Enemy.MaelstromDiveSeconds),
            MaelstromPhase.Rise => 1f - Mathf.Clamp01(_maelTimer / Tuning.Enemy.MaelstromRiseSeconds),
            _ => 0f,
        };

        float MaelstromUpSeconds => AtBand(Tuning.EnemyBands.MaelstromFastFromBand)
            ? Tuning.Enemy.MaelstromUpSecondsBand4 : Tuning.Enemy.MaelstromUpSeconds;
        float MaelstromRadius => Elite ? Tuning.Enemy.MaelstromEliteRadius : Tuning.Enemy.MaelstromRadius;
        float MaelstromTravel => AtBand(Tuning.EnemyBands.MaelstromFastFromBand)
            ? Tuning.Enemy.MaelstromTravelSpeedBand4 : Tuning.Enemy.MaelstromTravelSpeed;
        bool MaelstromSeconds => Elite || AtBand(Tuning.EnemyBands.MaelstromSecondFromBand);
        static Color MaelstromTellColour => MaelstromArt.Reserved[2];

        void UpdateMaelstrom()
        {
            float dt = Time.deltaTime;
            if (_maelWater == null) _maelWater = MaelstromWater.Attach(transform);
            TickMaelstromSecond(dt);
            bool live = _targetHealth != null && !_targetHealth.IsDead;

            switch (_maelPhase)
            {
                case MaelstromPhase.Surface:
                    if (Recovering) return;
                    // It arrives standing and dives soon after; then once a cycle, on its own clock.
                    if (!_maelFirstDive) { _maelFirstDive = true; _cooldown = Mathf.Min(_cooldown, 0.6f); }
                    _cooldown -= dt * AttackTempo;
                    if (_cooldown <= 0f && live) BeginMaelstromDive();
                    break;

                case MaelstromPhase.Dive:
                    _maelTimer -= dt;
                    if (_maelTimer <= 0f)
                    {
                        _maelPhase = MaelstromPhase.Under;
                        _maelTimer = Tuning.Enemy.MaelstromChaseSeconds;
                    }
                    break;

                case MaelstromPhase.Under:
                {
                    _maelTimer -= dt;
                    if (!live) { BeginMaelstromRise(false); break; }
                    float dist = Vector2.Distance(transform.position, _target.position);
                    // Its attack turn is the RING: taken once it is near enough (or has chased long enough).
                    if ((dist <= Tuning.Enemy.MaelstromWaitRange || _maelTimer <= 0f) && TryTakeToken())
                        BeginMaelstromRing();
                    break;
                }

                case MaelstromPhase.Tell:
                    if (!live) { CancelMaelstromTell(); ReleaseToken(0f); BeginMaelstromRise(false); break; }
                    _telegraphTimer -= dt;
                    // Band 1: the ring follows the player until a moment before it erupts, then locks.
                    if (AtBand(Tuning.EnemyBands.MaelstromFollowFromBand) && _telegraphTimer > Tuning.Enemy.MaelstromRingLockSeconds)
                        _maelRing = RingPoint(_target.position);
                    if (_maelTell != null)
                    {
                        _maelTell.Anchor = _maelRing;
                        _maelTell.Progress = MaelTellProgress01;
                    }
                    if (_telegraphTimer <= 0f) EruptMaelstrom();
                    break;

                case MaelstromPhase.Rise:
                    _maelTimer -= dt;
                    if (_maelTimer <= 0f)
                    {
                        _maelPhase = MaelstromPhase.Surface;
                        _cooldown = AttackInterval;
                        BeginRecover(MaelstromUpSeconds);   // surfaced: the window to hit it
                    }
                    break;
            }
            if (_maelWater != null) _maelWater.Show(_maelPhase, MaelProgress01, MaelstromRadius);
        }

        /// <summary>Where a ring may lie: on the floor nearest the player.</summary>
        static Vector2 RingPoint(Vector2 at) => Arena.OnFloor(at) ? at : Arena.NearestFloor(at, 0.3f);

        void BeginMaelstromDive()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Bubbling, transform.position);
            _maelPhase = MaelstromPhase.Dive;
            _maelTimer = Tuning.Enemy.MaelstromDiveSeconds;
            SetSubmerged(true);
        }

        void BeginMaelstromRing()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Hiss, _target.position);
            _maelPhase = MaelstromPhase.Tell;
            _maelRing = RingPoint(_target.position);
            // The floor-scaled wind-up, longer in band 0 (and for Dross, which fights as band 0).
            _telegraphTimer = TelegraphDuration * (AtBand(1) ? 1f : Tuning.Enemy.MaelstromTellBandZeroMul);
            _maelTellLength = _telegraphTimer;
            _maelTell = BlastTell.Circle(null, _maelRing, MaelstromRadius, MaelstromTellColour);
            _maelTell.name = "tell.maelstrom";
        }

        /// <summary>This ring's whole wind-up (TelegraphProgress01 divides by TelegraphDuration, which band 0 stretches past).</summary>
        [SerializeField] float _maelTellLength;
        float MaelTellProgress01 => _maelTellLength > 0f ? 1f - Mathf.Clamp01(_telegraphTimer / _maelTellLength) : 0f;

        void EruptMaelstrom()
        {
            _telegraphTimer = 0f;
            if (_maelTell != null) { _maelTell.Spend(0.2f); _maelTell = null; }
            transform.position = _maelRing;
            if (_rb != null) { _rb.position = _maelRing; _rb.linearVelocity = Vector2.zero; }
            WhirlpoolAt(_maelRing, MaelstromRadius, 1f);
            if (AtBand(Tuning.EnemyBands.MaelstromPoolFromBand))
            {
                var size = Vector2.one * Tuning.Enemy.MaelstromPoolSize;
                var pit = WaterPit.Spawn(new Rect(_maelRing - size * 0.5f, size), _target, GameBootstrap.FloorRoot);
                Destroy(pit.gameObject, Tuning.Enemy.MaelstromPoolSeconds);
                NavField.MarkDirty();
            }
            if (MaelstromSeconds && _target != null)
            {
                _maelSecondIn = Tuning.Enemy.MaelstromSecondDelay;
                _maelSecondAt = RingPoint(_target.position);
                _maelSecondTell = BlastTell.Circle(null, _maelSecondAt, Tuning.Enemy.MaelstromSecondRadius, MaelstromTellColour);
                ReleaseToken(Tuning.Enemy.MaelstromSecondDelay + Tuning.AttackTokens.HoldAfterSeconds);
            }
            else ReleaseToken();
            BeginMaelstromRise(true);
        }

        void BeginMaelstromRise(bool erupted)
        {
            if (!erupted) Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Bubbling, transform.position);
            SetSubmerged(false);
            _maelPhase = MaelstromPhase.Rise;
            _maelTimer = Tuning.Enemy.MaelstromRiseSeconds;
        }

        void TickMaelstromSecond(float dt)
        {
            if (_maelSecondIn <= 0f) return;
            _maelSecondIn -= dt;
            if (_maelSecondTell != null) _maelSecondTell.Progress = 1f - Mathf.Clamp01(_maelSecondIn / Tuning.Enemy.MaelstromSecondDelay);
            if (_maelSecondIn > 0f) return;
            if (_maelSecondTell != null) { _maelSecondTell.Spend(0.2f); _maelSecondTell = null; }
            WhirlpoolAt(_maelSecondAt, Tuning.Enemy.MaelstromSecondRadius, Tuning.Enemy.MaelstromSecondDamageFraction);
        }

        /// <summary>One whirlpool's blow: everything inside the ring (the player - the blast's own checks).</summary>
        void WhirlpoolAt(Vector2 at, float radius, float damageMul)
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Erupt, at);
            TexelFx.Shock(at, TexelFx.Rift, TexelFx.Grey, radius);
            LakeSurface.Splash(at);
            if (_targetHealth == null || _targetHealth.IsDead || _target == null) return;
            if (Vector2.Distance(at, _target.position) > radius) return;
            if (Spire.BubbleSeparates(at, _target.position)) return;
            if (_targetController != null && _targetController.TryParry(at, gameObject)) return;
            var dir = ((Vector2)_target.position - at);
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
            _targetHealth.Take(new DamageInfo(Damage * damageMul, ElementType.Water, gameObject)
            {
                Knockback = dir * Tuning.Enemy.MaelstromKnockback,
            });
        }

        void SetSubmerged(bool on)
        {
            _submerged = on;
            if (_health != null) _health.Immune = on;
            foreach (var col in GetComponentsInChildren<Collider2D>()) col.enabled = !on;
            if (on && _rb != null) _rb.linearVelocity = Vector2.zero;
        }

        void CancelMaelstromTell()
        {
            if (_maelTell != null) Destroy(_maelTell.gameObject);
            _maelTell = null;
            _telegraphTimer = 0f;
        }

        /// <summary>Gone or disabled mid-cycle: nothing left on the floor, and never left immune.</summary>
        void EndMaelstrom()
        {
            CancelMaelstromTell();
            if (_maelSecondTell != null) Destroy(_maelSecondTell.gameObject);
            _maelSecondTell = null;
            _maelSecondIn = 0f;
            if (_submerged) SetSubmerged(false);
        }

        /// <summary>Its physics step: beneath the floor straight at the player (or to its ring), over
        /// walls and columns but never off the floor; surfaced, still.</summary>
        void MaelstromMove(Vector2 pos, Vector2 to, float dist)
        {
            if (!_submerged || _maelPhase == MaelstromPhase.Dive)
            {
                _rb.linearVelocity = Vector2.zero;
                return;
            }
            Vector2 goal = _maelPhase == MaelstromPhase.Tell ? _maelRing : (Vector2)_target.position;
            Vector2 d = goal - pos;
            float stopAt = _maelPhase == MaelstromPhase.Tell ? 0.05f : Tuning.Enemy.MaelstromMarkRange;
            if (d.magnitude <= stopAt) { _rb.linearVelocity = Vector2.zero; return; }
            float speed = MaelstromTravel * (_status ? _status.MoveMultiplier : 1f);
            Vector2 v = d.normalized * speed;
            Vector2 next = pos + v * Time.fixedDeltaTime;
            if (!Arena.OnFloor(next))
            {
                // slide along the floor's edge rather than travel under the void
                var vx = new Vector2(v.x, 0f); var vy = new Vector2(0f, v.y);
                v = Arena.OnFloor(pos + vx * Time.fixedDeltaTime) ? vx : Arena.OnFloor(pos + vy * Time.fixedDeltaTime) ? vy : Vector2.zero;
            }
            _rb.linearVelocity = v;
        }
    }
}
