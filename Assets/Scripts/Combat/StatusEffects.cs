using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Per-target status container. Fire applies Burn, Water applies Soak (slow +
    /// vulnerability), Earth applies Stagger. Attached lazily on first application.
    /// </summary>
    public class StatusEffects : MonoBehaviour
    {
        Health _health;

        // Burn - fire's damage-over-time. TWO independent tracks; see ApplyBurn.
        float _burnRemaining, _burnDps, _burnTick;
        GameObject _burnSource;

        // The WEAPON's burn, kept apart from the element's.
        float _wBurnRemaining, _wBurnDps, _wBurnTick;
        GameObject _wBurnSource;

        // Soak - water's debuff (and the Phlegm keystone's).
        float _soakRemaining, _soakVulnerability = 1f;

        // Stagger - earth's escalating slow (and the Melancholy keystone's). Antimony slows the
        // staggered body's attacks as well, for as long as the stagger lasts.
        float _staggerRemaining;
        int _staggerStacks;
        float _staggerAttackSlow;

        // A plain slow - Aqua Regia's, salted ground's.
        float _slowRemaining, _slowMul = 1f;

        // CONGEALED (the Congelation Opus): rooted through ApplyPetrify and taking more damage.
        float _congealRemaining, _congealVulnerability = 1f;

        public float MoveMultiplier
        {
            get
            {
                float m = 1f;
                if (_soakRemaining > 0f) m *= 0.55f;
                if (_staggerRemaining > 0f) m *= Mathf.Max(0.25f, 1f - 0.2f * _staggerStacks);
                if (_slowRemaining > 0f) m *= _slowMul;
                return m;
            }
        }

        /// <summary>
        /// How fast this body's attack cooldowns run - 1 normally, slower while an Antimony
        /// stagger holds it. Only the COOLDOWN between attacks, never a telegraph: what an attack
        /// looks like before it lands must stay the same length, or reading it stops working.
        /// </summary>
        public float AttackTempo => _staggerRemaining > 0f && _staggerAttackSlow > 0f
            ? 1f / (1f + _staggerAttackSlow) : 1f;

        public bool Burning => _burnRemaining > 0f || _wBurnRemaining > 0f;
        public bool Soaked => _soakRemaining > 0f;
        public bool Staggered => _staggerRemaining > 0f;
        public int StaggerStacks => _staggerRemaining > 0f ? _staggerStacks : 0;

        /// <summary>Slowed by anything - a soak, a stagger or a plain slow.</summary>
        public bool Slowed => _soakRemaining > 0f || _staggerRemaining > 0f || _slowRemaining > 0f;

        public bool Congealed => _congealRemaining > 0f;

        /// <summary>The strongest burn on this body right now, per second (0 when not burning) -
        /// what it passes on when Cineration spreads its burn.</summary>
        public float BurnDps => Mathf.Max(_burnRemaining > 0f ? _burnDps : 0f, _wBurnRemaining > 0f ? _wBurnDps : 0f);

        /// <summary>When this body last congealed - Congelation's per-body cooldown reads it.</summary>
        public float CongealedAt { get; private set; } = float.NegativeInfinity;

        // Petrify - an external ROOT, first applied by the Gargoyle family. MOVEMENT ONLY: it
        // does not stop attacking, aiming, or acting, so it is its own bool rather than folded
        // into MoveMultiplier above - that getter is multiplicative and (so far) enemy-only,
        // where this needs to compose as a hard "exactly zero" a PlayerController reads directly,
        // the same shape AttackLocked already uses ("we stopped you" rather than "you stopped").
        float _petrifyRemaining;
        public bool Petrified => _petrifyRemaining > 0f;

        /// <summary>Root the target in place for this long. Refreshes to the longer of the two
        /// durations, the same "longest wins" rule EnemyController.Flinch already uses, so a
        /// second root landing mid-root cannot shorten the first.</summary>
        public void ApplyPetrify(float duration) => _petrifyRemaining = Mathf.Max(_petrifyRemaining, duration);

        // STONE - Medusa's gaze. Not the Gargoyle's petrify, which roots and nothing else: stone is
        // the WHOLE body - no moving, attacking, releasing or defending - drawn as a statue
        // (ICharacterRig.SetStone), and it takes less damage while it lasts (STONE SKIN). The
        // multiplier is set by whoever turns the body to stone, because it is sized against
        // that fight's own adds (Tuning.Medusa.StoneSkinBase).
        float _stoneRemaining;
        float _stoneDamageTaken = 1f;

        public bool Stone => _stoneRemaining > 0f;

        /// <summary>Folded into the player's Vulnerability every physics step while stone.</summary>
        public float StoneDamageTaken => Stone ? _stoneDamageTaken : 1f;

        public void ApplyStone(float duration, float damageTaken)
        {
            _stoneRemaining = Mathf.Max(_stoneRemaining, duration);
            _stoneDamageTaken = Mathf.Clamp01(damageTaken);
        }

        /// <summary>Back to flesh now, rather than on the timer - the window it covered closed.</summary>
        public void EndStone() => _stoneRemaining = 0f;

        void Awake() => _health = GetComponent<Health>();

        /// <summary>
        /// Attaches the view alongside the sim on first use, so nothing has to remember to wire
        /// status visuals onto every enemy factory.
        /// </summary>
        public static StatusEffects Get(GameObject go)
        {
            var existing = go.GetComponent<StatusEffects>();
            if (existing != null) return existing;

            var added = go.AddComponent<StatusEffects>();
            go.AddComponent<StatusVisuals>();
            return added;
        }

        /// <summary>
        /// Bleed - a second damage-over-time, deliberately NOT burn.
        ///
        /// Mechanically identical, but burn is fire's signature and a water character opening a
        /// bleed should not set anything alight. Kept separate so the two also stack rather than
        /// refreshing each other.
        /// </summary>
        public void ApplyBleed(float dps, float duration, GameObject source, int maxStacks = 1)
        {
            // Stacks (Orpiment): each application while one is running adds a stack, up to the
            // cap; the per-stack dps is still merged by max, so stacks are the only way it climbs.
            _bleedStacks = _bleedRemaining > 0f ? Mathf.Min(Mathf.Max(1, maxStacks), _bleedStacks + 1) : 1;
            _bleedDps = Mathf.Max(_bleedDps, dps);
            _bleedRemaining = Mathf.Max(_bleedRemaining, duration);
            _bleedSource = source;
        }

        float _bleedRemaining, _bleedDps, _bleedTick;
        int _bleedStacks = 1;
        GameObject _bleedSource;

        public bool Bleeding => _bleedRemaining > 0f;

        /// <summary>
        /// The ELEMENT's burn. Refreshing takes the max of both fields rather than adding, so a
        /// crowd being re-lit every swing cannot stack itself into an execute.
        /// </summary>
        public void ApplyBurn(float dps, float duration, GameObject source)
        {
            _burnDps = Mathf.Max(_burnDps, dps);
            _burnRemaining = Mathf.Max(_burnRemaining, duration);
            _burnSource = source;
        }

        /// <summary>
        /// A WEAPON's burn - currently the fire blade's blue-stage finisher.
        ///
        /// Its own track, and this is the whole reason it exists: <see cref="ApplyBurn"/> merges
        /// by MAX, so a weaker burn applied on top of the Fire element's stronger one is swallowed
        /// whole and does nothing. A fire character swinging a burning sword would get exactly the
        /// burn they already had, and the sword's headline effect would be invisible precisely to
        /// the players most likely to notice.
        ///
        /// Two tracks, both ticking, so the two effects genuinely add up. It is still the same
        /// BURN - same fire damage type, same half-second cadence - because a second bespoke
        /// damage-over-time would be Bleed all over again, and this one has no reason to be a
        /// different thing.
        ///
        /// It merges by max WITHIN its own track, for the same anti-stacking reason as above.
        /// </summary>
        public void ApplyWeaponBurn(float dps, float duration, GameObject source)
        {
            _wBurnDps = Mathf.Max(_wBurnDps, dps);
            _wBurnRemaining = Mathf.Max(_wBurnRemaining, duration);
            _wBurnSource = source;
        }

        public void ApplySoak(float duration, float vulnerability)
        {
            // The stronger of a running soak and a new one - a weaker soak landing on a stronger
            // one must not water it down.
            _soakVulnerability = _soakRemaining > 0f ? Mathf.Max(_soakVulnerability, vulnerability) : vulnerability;
            _soakRemaining = Mathf.Max(_soakRemaining, duration);
            RefreshVulnerability();
        }

        /// <summary>
        /// Health.Vulnerability as the product of every status that raises it. Soak once WROTE the
        /// field and reset it to 1 on expiry, which was fine while it was the only writer; with a
        /// second (congealed) each would have erased the other.
        /// </summary>
        void RefreshVulnerability()
        {
            if (!_health) return;
            _health.Vulnerability = (_soakRemaining > 0f ? _soakVulnerability : 1f)
                                  * (_congealRemaining > 0f ? _congealVulnerability : 1f);
        }

        /// <summary>A plain slow: movement times <paramref name="multiplier"/>, the stronger slow
        /// winning while two overlap.</summary>
        public void ApplySlow(float duration, float multiplier)
        {
            _slowMul = _slowRemaining > 0f ? Mathf.Min(_slowMul, multiplier) : multiplier;
            _slowRemaining = Mathf.Max(_slowRemaining, duration);
        }

        /// <summary>CONGEAL (Congelation): rooted and taking more damage for a moment.</summary>
        public void ApplyCongeal(float duration, float vulnerability)
        {
            CongealedAt = Time.time;
            ApplyPetrify(duration);
            _congealVulnerability = Mathf.Max(_congealRemaining > 0f ? _congealVulnerability : 1f, vulnerability);
            _congealRemaining = Mathf.Max(_congealRemaining, duration);
            RefreshVulnerability();
        }

        /// <summary>
        /// Paint a target so the NEXT hit on it lands amplified. Falls off on a timer if nothing
        /// cashes it in, so a mark left unused is a wasted finisher rather than a stored one.
        /// </summary>
        public void ApplyMark(float duration, float multiplier)
        {
            if (_health == null) return;
            _health.MarkMultiplier = Mathf.Max(1f, multiplier);
            _markRemaining = Mathf.Max(_markRemaining, duration);
        }

        public bool Marked => _markRemaining > 0f && _health != null && _health.MarkMultiplier > 1f;

        float _markRemaining;

        /// <summary>Stagger, escalating to three stacks. <paramref name="attackSlow"/> (Antimony)
        /// also slows the body's attack cooldowns while it lasts. Returns the stacks now held.</summary>
        public int ApplyStagger(float duration, float attackSlow = 0f)
        {
            if (_staggerRemaining <= 0f) _staggerAttackSlow = 0f;
            _staggerRemaining = Mathf.Max(_staggerRemaining, duration);
            _staggerStacks = Mathf.Min(MaxStaggerStacks, _staggerStacks + 1);
            _staggerAttackSlow = Mathf.Max(_staggerAttackSlow, attackSlow);
            return _staggerStacks;
        }

        public const int MaxStaggerStacks = 3;

        /// <summary>Spends the stagger stacks (Congelation consumes them as it roots).</summary>
        public void ClearStaggerStacks() => _staggerStacks = 0;

        void Update()
        {
            float dt = Time.deltaTime;

            // The mark expires on a timer OR the moment a hit spends it, whichever comes first -
            // Health clears the multiplier itself, so this only has to notice that it is gone.
            if (_markRemaining > 0f)
            {
                _markRemaining -= dt;
                if (_markRemaining <= 0f || _health == null || _health.MarkMultiplier <= 1f)
                {
                    _markRemaining = 0f;
                    if (_health != null) _health.MarkMultiplier = 1f;
                }
            }

            if (_burnRemaining > 0f)
            {
                _burnRemaining -= dt;
                _burnTick += dt;
                if (_burnTick >= 0.5f && _health)
                {
                    _burnTick = 0f;
                    _health.Take(new DamageInfo(_burnDps * 0.5f, ElementType.Fire, _burnSource));
                }
                if (_burnRemaining <= 0f) _burnDps = 0f;
            }

            // The weapon's burn ticks on its OWN clock, not folded into the element's. Sharing a
            // timer would make one refresh the other and hand whichever landed last both durations.
            if (_wBurnRemaining > 0f)
            {
                _wBurnRemaining -= dt;
                _wBurnTick += dt;
                if (_wBurnTick >= 0.5f && _health)
                {
                    _wBurnTick = 0f;
                    _health.Take(new DamageInfo(_wBurnDps * 0.5f, ElementType.Fire, _wBurnSource));
                }
                if (_wBurnRemaining <= 0f) _wBurnDps = 0f;
            }

            if (_bleedRemaining > 0f)
            {
                _bleedRemaining -= dt;
                _bleedTick += dt;
                if (_bleedTick >= 0.5f && _health)
                {
                    _bleedTick = 0f;
                    // Earth rather than the dealer's element: a bleed is the wound, not the thing
                    // that made it, and tagging it Fire would feed fire's own reactions.
                    _health.Take(new DamageInfo(_bleedDps * _bleedStacks * 0.5f, ElementType.Earth, _bleedSource));
                }
                if (_bleedRemaining <= 0f) { _bleedDps = 0f; _bleedStacks = 1; }
            }

            if (_soakRemaining > 0f)
            {
                _soakRemaining -= dt;
                if (_soakRemaining <= 0f) { _soakVulnerability = 1f; RefreshVulnerability(); }
            }

            if (_congealRemaining > 0f)
            {
                _congealRemaining -= dt;
                if (_congealRemaining <= 0f) { _congealVulnerability = 1f; RefreshVulnerability(); }
            }

            if (_slowRemaining > 0f)
            {
                _slowRemaining -= dt;
                if (_slowRemaining <= 0f) _slowMul = 1f;
            }

            if (_staggerRemaining > 0f)
            {
                _staggerRemaining -= dt;
                if (_staggerRemaining <= 0f) { _staggerStacks = 0; _staggerAttackSlow = 0f; }
            }

            if (_petrifyRemaining > 0f) _petrifyRemaining -= dt;
            if (_stoneRemaining > 0f) _stoneRemaining -= dt;
        }
    }
}
