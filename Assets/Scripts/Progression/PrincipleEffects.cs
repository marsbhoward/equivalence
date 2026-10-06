using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Progression
{
    /// <summary>
    /// The three principle chains, running. See <see cref="Tuning.Principle"/> for the shape and
    /// every number, and <see cref="MasteryBoard"/> for where the links come from.
    ///
    /// ONE COMPONENT RATHER THAN THREE, and one binding rather than a subscription per hook - the
    /// same shape <see cref="Exchange.RunEffects"/> settled on for the exchange ledger's
    /// conditional half, and for the same reasons. Its public methods are the COMPLETE LIST of
    /// moments a chain can react to, which is the thing worth being able to read in one place; and
    /// nothing here is an event subscription, so there is no wiring to survive a domain reload on
    /// its own.
    ///
    /// LINK COUNTS ARE READ ONCE, AT RUN START. Mastery is spent in the main menu and never
    /// mid-run - that is a standing rule of the grid, not an implementation shortcut - so a chain
    /// cannot grow a link during a fight and there is nothing to re-read.
    ///
    /// SCOPED TO THE ELEMENT BEING PLAYED, because the four boards are funded independently and an
    /// always-on chain would stack four ways the moment a player filled every board. The caller
    /// passes the links for one element; this never sees the other three.
    /// </summary>
    public class PrincipleEffects : MonoBehaviour
    {
        /// <summary>
        /// What this needs from the player, and nothing more - the same narrow-facade discipline
        /// RunEffects uses, so a chain can read what it needs and cannot drive anything it should
        /// not.
        /// </summary>
        public class Facade
        {
            public System.Func<Health> Health;
            /// <summary>The character's Resilience as a fraction (points / 100) - gear and board.</summary>
            public System.Func<float> Resilience;
            public System.Func<Vector2> Position;
            /// <summary>The player's own basic hit (PlayerController.HitUnit) - what a detonation
            /// is counted in, so the chain grows with the character.</summary>
            public System.Func<float> HitUnit;
        }

        int _sulfur, _mercury, _salt;
        Facade _player;

        public void Bind(int sulfurLinks, int mercuryLinks, int saltLinks, Facade player)
        {
            _sulfur = sulfurLinks;
            _mercury = mercuryLinks;
            _salt = saltLinks;
            _player = player;
        }

        public int SulfurLinks => _sulfur;
        public int MercuryLinks => _mercury;
        public int SaltLinks => _salt;

        // ================================================================= Sulfur

        /// <summary>
        /// A basic landed. Link 1 seasons the target.
        ///
        /// Basics ONLY - the caller gates this. Sulfur's verb is sustained pressure, and letting
        /// finishers season too would make the chain build fastest off the swing that already pays
        /// best.
        /// </summary>
        public void OnBasicLanded(Health target)
        {
            if (_sulfur < 1 || target == null || target.IsDead) return;
            Seasoning.Get(target.gameObject).Add();
        }

        /// <summary>
        /// An elemental release fired. Link 2 detonates every mark on the field at once.
        ///
        /// EVERY MARK, not just the ones near the blast - the release is the trigger, not the
        /// delivery. That is what makes the chain a decision about WHEN rather than about where to
        /// stand, and it is why the marks are drawn: the player is reading the field to pick the
        /// moment.
        /// </summary>
        public void OnReleased()
        {
            if (_sulfur < 2) return;

            // Two passes. Every mark has to be counted BEFORE any damage lands, because link 4
            // scales the splash by the total and a single pass would give the first body detonated
            // a smaller number than the last for no reason the player could see.
            int total = 0;
            var marked = new System.Collections.Generic.List<(Health Hp, int Stacks)>();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var season = e.GetComponent<Seasoning>();
                if (season == null || season.Stacks <= 0) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                int n = season.Consume();
                total += n;
                marked.Add((hp, n));
            }
            if (marked.Count == 0) return;

            float splash = _sulfur >= 3
                ? (_sulfur >= 4
                    ? Mathf.Min(Tuning.Principle.DetonateSplashFraction
                                + Tuning.Principle.DetonateSplashPerMark * total,
                                Tuning.Principle.DetonateSplashFractionMax)
                    : Tuning.Principle.DetonateSplashFraction)
                : 0f;

            foreach (var (hp, stacks) in marked)
            {
                if (hp == null || hp.IsDead) continue;
                float dmg = stacks * Tuning.Principle.DetonateHitUnitsPerStack
                          * (_player?.HitUnit != null ? _player.HitUnit() : Tuning.Player.BaseDamage);
                var at = hp.transform.position;

                hp.Take(new DamageInfo(dmg, ElementType.Fire, gameObject));
                Spr.Flash(at, 0.8f, new Color(1f, 0.65f, 0.2f), 0.25f);

                if (splash <= 0f) continue;
                foreach (var other in Enemies.EnemyRegistry.All)
                {
                    if (other == null) continue;
                    var oh = other.GetComponent<Health>();
                    if (oh == null || oh == hp || oh.IsDead) continue;
                    if (Vector2.Distance(oh.transform.position, at) > Tuning.Principle.DetonateSplashRadius)
                        continue;
                    oh.Take(new DamageInfo(dmg * splash, ElementType.Fire, gameObject));
                }
            }
        }

        // ================================================================= Salt

        int _ward;
        float _wardExpires;
        float _guardUntil;

        public int WardStacks => Time.time < _wardExpires ? _ward : 0;

        /// <summary>
        /// Damage reduction the chain is currently supplying. Chained INTO Health.ModifyIncoming
        /// alongside the exchange ledger's own, rather than replacing it - that field is a single
        /// delegate and whoever assigns last would otherwise silently delete the other.
        /// </summary>
        public float ModifyIncoming(float amount)
        {
            if (_salt < 1) return amount;
            float reduction = WardStacks * Tuning.Principle.WardPerStack;
            if (_salt >= 3 && Time.time < _guardUntil)
                reduction += Tuning.Principle.ReturnGuardReduction;
            return amount * Mathf.Clamp01(1f - reduction);
        }

        /// <summary>
        /// The player took a hit. Link 1 stacks Ward; at full, link 2 spends it as a Return.
        ///
        /// Read AFTER the damage rather than before, so the hit that fills the Ward is itself
        /// reduced by the stacks already held - a stack that only counted from the next hit would
        /// make the fifth one free and the sixth expensive, which is the wrong way round for a
        /// principle whose claim is that wearing you down gets harder.
        /// </summary>
        public void OnDamaged(float amount)
        {
            if (_salt < 1) return;

            if (Time.time >= _wardExpires) _ward = 0;
            _ward = Mathf.Min(_ward + 1, Tuning.Principle.WardMaxStacks);
            _wardExpires = Time.time + Tuning.Principle.WardSeconds;

            if (_salt < 2 || _ward < Tuning.Principle.WardMaxStacks) return;

            // Full Ward: spend it.
            _ward = 0;
            if (_salt >= 3) _guardUntil = Time.time + Tuning.Principle.ReturnGuardSeconds;

            float reflect = amount * Tuning.Principle.ReturnFraction;
            if (_salt >= 4 && _player?.Resilience != null)
                reflect *= 1f + _player.Resilience() * Tuning.Principle.ReturnPerResilience;

            var at = _player?.Position != null ? _player.Position() : (Vector2)transform.position;
            Spr.Flash(at, Tuning.Principle.ReturnRadius, new Color(0.75f, 0.85f, 0.6f), 0.3f);

            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                if (Vector2.Distance(hp.transform.position, at) > Tuning.Principle.ReturnRadius) continue;
                hp.Take(new DamageInfo(reflect, ElementType.Earth, gameObject));
            }
        }

        // ================================================================= Mercury

        float _charge;
        bool _armed;
        float _armedDistance;
        float _slipUntil;
        float _slipBonus;
        Vector2 _lastPos;
        bool _hasLast;

        public float Quicksilver01 => Mathf.Clamp01(_charge / Tuning.Principle.QuicksilverDistance);
        public bool PhaseStrikeArmed => _armed;

        /// <summary>Move-speed multiplier the chain is currently supplying. 1 when nothing is
        /// running, so a caller can multiply unconditionally.</summary>
        public float MoveSpeedMultiplier
            => _mercury >= 3 && Time.time < _slipUntil ? 1f + _slipBonus : 1f;

        void Update()
        {
            if (_mercury < 1 || _player?.Position == null) return;

            var now = _player.Position();
            if (!_hasLast) { _lastPos = now; _hasLast = true; return; }

            // Distance TRAVELLED, not displacement from a start point - circling to stay alive is
            // movement, and Mercury is the mobility principle. Measuring net displacement would
            // reward running in one direction and nothing else.
            _charge += Vector2.Distance(now, _lastPos);
            _lastPos = now;

            if (_mercury >= 2 && !_armed && _charge >= Tuning.Principle.QuicksilverDistance)
            {
                _armed = true;
                _armedDistance = _charge;
                _charge = 0f;
            }
        }

        /// <summary>
        /// Asked once per hit, immediately before it is applied. Returns true if this hit should
        /// pass through armour, and spends the charge.
        ///
        /// Consumed by the CALLER at the point of use rather than by anything here, so the flag
        /// cannot be armed and then left set across a hit that never happened.
        /// </summary>
        public bool ConsumePhaseStrike()
        {
            if (!_armed) return false;
            _armed = false;

            if (_mercury >= 3)
            {
                _slipBonus = Tuning.Principle.SlipstreamBonus;
                float seconds = Tuning.Principle.SlipstreamSeconds;
                if (_mercury >= 4)
                {
                    float extra = Mathf.Max(0f, _armedDistance - Tuning.Principle.QuicksilverDistance);
                    seconds = Mathf.Min(seconds + extra * Tuning.Principle.SlipstreamPerExtraDistance,
                                        Tuning.Principle.SlipstreamSecondsMax);
                }
                _slipUntil = Time.time + seconds;
            }
            return true;
        }
    }
}
