using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using B = Convergence.Core.Tuning.Board;

namespace Convergence.Progression
{
    /// <summary>
    /// The mastery board's rules, running - every Tincture, Opus and status keystone the element's
    /// board owns (MasteryBoard.Describe says what each does; Tuning.Board holds the numbers).
    ///
    /// PrincipleEffects' shape, for its reasons: ONE component, bound once at run start through a
    /// narrow facade, and its public methods are the complete list of moments the board can react
    /// to. Mastery is spent in the main menu and never mid-run, so what is owned never changes
    /// during a fight.
    ///
    /// EVERY HIT PATH CALLS THE SAME HOOKS - the melee arc, a thrown disc, an arrow and the thrown
    /// blade - so a rule works for every weapon class: <see cref="ModifyOutgoing"/> before the hit,
    /// <see cref="OnHitLanded"/> after it, <see cref="OnBasicLanded"/> once per basic that lands.
    ///
    /// THE STATUSES GO THROUGH HERE TOO (<see cref="Burn"/>, <see cref="Soak"/>, <see cref="Bleed"/>,
    /// <see cref="Stagger"/>): the element's own (Fire's Ignite and pools, Water's soak, Earth's
    /// quake) and the humours' alike, so a status's POWER and its rules (Aqua Fortis, Orpiment,
    /// Antimony, Congelation) apply to every source the player has, whichever element it is.
    ///
    /// Owned rules are a bitmask, not a set: a HashSet on a MonoBehaviour comes back empty after a
    /// domain reload (see CLAUDE.md), a long survives it.
    /// </summary>
    public class BoardEffects : MonoBehaviour
    {
        /// <summary>What the rules need from the player, and nothing more.</summary>
        public class Facade
        {
            public System.Func<Health> Health;
            public System.Func<Vector2> Position;
            public System.Func<bool> IsMoving;
            public System.Func<Player.ElementalResource> Resource;
            /// <summary>The player's own basic hit right now (PlayerController.HitUnit).</summary>
            public System.Func<float> HitUnit;
            /// <summary>Area's multiplier on a radius (1 with none).</summary>
            public System.Func<float> AreaScale;
        }

        [SerializeField] long _owned;
        [SerializeField] float _burnPoints, _soakPoints, _bleedPoints, _staggerPoints;
        Facade _player;

        public void Bind(System.Collections.Generic.IEnumerable<Notable> owned, float burnPoints, float soakPoints,
                         float bleedPoints, float staggerPoints, Facade player)
        {
            _owned = 0;
            if (owned != null) foreach (var n in owned) _owned |= 1L << (int)n;
            _burnPoints = burnPoints;
            _soakPoints = soakPoints;
            _bleedPoints = bleedPoints;
            _staggerPoints = staggerPoints;
            _player = player;
        }

        public bool Has(Notable n) => (_owned & (1L << (int)n)) != 0;

        Health Self => _player?.Health?.Invoke();
        Vector2 Where => _player?.Position != null ? _player.Position() : (Vector2)transform.position;
        float AreaScale => _player?.AreaScale != null ? _player.AreaScale() : 1f;
        float HitUnit => _player?.HitUnit != null ? _player.HitUnit() : Tuning.Player.BaseDamage;

        // ================================================================= standing values

        public float BurnPower => 1f + _burnPoints / 100f;
        public float SoakPower => 1f + _soakPoints / 100f;
        public float BleedPower => 1f + _bleedPoints / 100f;
        public float StaggerPower => 1f + _staggerPoints / 100f;

        /// <summary>Basics added to the chain - Multiplication takes one away.</summary>
        public int ChainDelta => Has(Notable.Multiplication) ? -1 : 0;

        /// <summary>Damage points joined to the character's RAW points before the character curve,
        /// like an element's head start: Tartar while planted, Exaltation after a release.</summary>
        public float DamagePoints
            => (Has(Notable.Tartar) && _player?.IsMoving != null && !_player.IsMoving() ? B.TartarDamagePoints : 0f)
             + (Has(Notable.Exaltation) && Time.time < _exaltedUntil ? B.ExaltationDamagePoints : 0f);

        /// <summary>Attack speed points joined the same way: Circulation's flow.</summary>
        public float AttackSpeedPoints => Has(Notable.Circulation) ? _flow : 0f;

        /// <summary>Sal Ammoniac: what the defensive ability's protection and parry window are
        /// stretched by.</summary>
        public float DefenseWindowMul => Has(Notable.SalAmmoniac) ? B.SalAmmoniacWindowMul : 1f;

        /// <summary>Volatilization: a parry hands the defensive ability straight back.</summary>
        public bool RefundsOnParry => Has(Notable.Volatilization);

        // ================================================================= outgoing

        Health _vitriolTarget;
        int _vitriolStacks;

        /// <summary>
        /// A hit about to land on <paramref name="target"/>: the rules that depend on the TARGET -
        /// Vitriol's focus, Saltpetre on a burning body, Mortification on a bleeding one's crit,
        /// Inceration on a slowed one. Small multipliers, the ledger's conditional boons' shape.
        /// </summary>
        public float ModifyOutgoing(Health target, float damage, bool isFinisher, bool crit)
        {
            if (target == null || _owned == 0) return damage;
            float m = 1f;
            if (Has(Notable.Vitriol) && target == _vitriolTarget)
                m *= 1f + Mathf.Min(B.VitriolMax, B.VitriolPerHit * _vitriolStacks);

            var st = target.GetComponent<StatusEffects>();
            if (st != null)
            {
                if (Has(Notable.Saltpetre) && st.Burning) m *= 1f + B.SaltpetreBonus;
                if (Has(Notable.Mortification) && crit && st.Bleeding) m *= 1f + B.MortificationBonus;
                if (Has(Notable.Inceration) && st.Slowed) m *= 1f + B.IncerationBonus;
            }
            return damage * m;
        }

        float _lastHitAt = float.NegativeInfinity, _flow;
        int _quickBasics;

        /// <summary>
        /// A hit landed. <paramref name="reach01"/> is how far out it landed as a share of the
        /// attack's reach (0 at the player, 1 at the edge); <paramref name="designated"/> marks the
        /// FIRST body of a swing or throw, so a rule that should fire once per attack does.
        /// </summary>
        public void OnHitLanded(Health target, DamageInfo info, bool isFinisher, float reach01, bool designated)
        {
            if (target == null || _owned == 0) return;

            if (Has(Notable.Vitriol))
            {
                if (target == _vitriolTarget) _vitriolStacks = Mathf.Min(Mathf.RoundToInt(B.VitriolMax / B.VitriolPerHit), _vitriolStacks + 1);
                else { _vitriolTarget = target; _vitriolStacks = 1; }
            }

            if (Has(Notable.Circulation))
            {
                _flow = Time.time - _lastHitAt <= B.CirculationWindow
                    ? Mathf.Min(B.CirculationMaxPoints, _flow + B.CirculationPointsPerHit)
                    : B.CirculationPointsPerHit;
            }
            _lastHitAt = Time.time;

            if (!target.IsDead)
            {
                // The four humours: any element can leave these behind.
                if (isFinisher && Has(Notable.Choler)) Burn(target, info.Amount * B.CholerBurnFraction, B.CholerSeconds);
                if (isFinisher && Has(Notable.Phlegm)) Soak(target, B.PhlegmSeconds, B.PhlegmVulnerability);
                if (info.Crit && Has(Notable.Sanguine)) Bleed(target, info.Amount * B.SanguineBleedFraction, B.SanguineSeconds);
                if (isFinisher && Has(Notable.Melancholy)) Stagger(target, B.MelancholySeconds);

                if (Has(Notable.AquaRegia) && reach01 >= B.AquaRegiaReachFraction)
                    StatusEffects.Get(target.gameObject).ApplySlow(B.AquaRegiaSeconds, B.AquaRegiaSlow);
            }

            if (isFinisher && designated)
            {
                if (Has(Notable.Precipitation))
                    CrowdHits.Splash(target, info.Amount, B.PrecipitationFraction, info.Element, gameObject,
                                     B.PrecipitationRadius);
                if (Has(Notable.Realgar)) _quickBasics = B.RealgarBasics;
            }
        }

        int _steep;

        /// <summary>A basic landed (once per swing or throw, however many bodies it caught).</summary>
        public void OnBasicLanded()
        {
            if (Has(Notable.Cohobation))
                _steep = Mathf.Min(Mathf.RoundToInt(B.CohobationMax / B.CohobationPerBasic), _steep + 1);
        }

        /// <summary>
        /// A weapon art's timing bar settled. Returns what the art's hits are multiplied by
        /// (Cohobation's steep, spent here), and whether every hit of it crits (Cinnabar, on a
        /// PERFECT).
        /// </summary>
        public float OnStrikeSettled(StrikeVerdict verdict, out bool forceCrit)
        {
            forceCrit = Has(Notable.Cinnabar) && verdict == StrikeVerdict.Perfect;
            if (!Has(Notable.Cohobation) || _steep == 0) return 1f;
            float mul = 1f + Mathf.Min(B.CohobationMax, B.CohobationPerBasic * _steep);
            _steep = 0;
            return mul;
        }

        /// <summary>Realgar: true (and spent) if the basic about to swing is a quickened one.</summary>
        public bool ConsumeQuickBasic()
        {
            if (_quickBasics <= 0) return false;
            _quickBasics--;
            return true;
        }

        float _exaltedUntil = float.NegativeInfinity;

        /// <summary>The element's release fired (after the meter is spent).</summary>
        public void OnReleased(Player.ElementalResource resource)
        {
            if (Has(Notable.Alkahest)) resource?.Refund(B.AlkahestRefund);
            if (Has(Notable.Exaltation)) _exaltedUntil = Time.time + B.ExaltationSeconds;
            OnAreaAttack(Where, B.ReleaseRadius * AreaScale, HitUnit);
        }

        /// <summary>
        /// An area attack landed - a release round the player, or an area weapon art round its
        /// blast. Borax draws the crowd in; Fermentation salts the ground.
        /// </summary>
        public void OnAreaAttack(Vector2 centre, float radius, float hit)
        {
            if (Has(Notable.Borax))
            {
                Enemies.EnemyRegistry.Prune();
                foreach (var e in Enemies.EnemyRegistry.All)
                {
                    if (e == null) continue;
                    if (((Vector2)e.transform.position - centre).sqrMagnitude > radius * radius) continue;
                    DragToward.Pull(e.GetComponent<Health>(), centre, B.BoraxPullDistance);
                }
            }
            if (Has(Notable.Fermentation))
                SaltGround.Spawn(centre, B.FermentationRadius * AreaScale, hit * B.FermentationHitFractionPerSecond,
                                 B.FermentationSlow, B.FermentationSeconds, gameObject);
        }

        // ================================================================= the statuses

        /// <summary>A burn the player applies, strengthened by Burn Power.</summary>
        public void Burn(Health target, float dps, float seconds)
        {
            if (target == null || target.IsDead || dps <= 0f) return;
            StatusEffects.Get(target.gameObject).ApplyBurn(dps * BurnPower, seconds, gameObject);
        }

        /// <summary>A soak the player applies: Soak Power on its vulnerability, and Aqua Fortis
        /// carrying it to the nearest other enemy (once - a spread soak does not spread).</summary>
        public void Soak(Health target, float seconds, float vulnerability, bool spread = true)
        {
            if (target == null || target.IsDead) return;
            StatusEffects.Get(target.gameObject).ApplySoak(seconds, 1f + (vulnerability - 1f) * SoakPower);
            if (!spread || !Has(Notable.AquaFortis)) return;

            var near = Nearest(target, B.AquaFortisRadius);
            if (near == null) return;
            Soak(near, seconds, vulnerability, spread: false);
            Spr.Flash(near.transform.position, 0.6f, new Color(0.45f, 0.75f, 1f, 0.6f), 0.18f, false);
        }

        /// <summary>A bleed the player applies: Bleed Power on it, stacking under Orpiment.</summary>
        public void Bleed(Health target, float dps, float seconds)
        {
            if (target == null || target.IsDead || dps <= 0f) return;
            StatusEffects.Get(target.gameObject).ApplyBleed(dps * BleedPower, seconds, gameObject,
                                                            Has(Notable.Orpiment) ? B.OrpimentStacks : 1);
        }

        /// <summary>A stagger the player applies: Stagger Power on its length, Antimony's slower
        /// attacks, and Congelation rooting a body staggered a third time.</summary>
        public void Stagger(Health target, float seconds)
        {
            if (target == null || target.IsDead) return;
            var st = StatusEffects.Get(target.gameObject);
            int stacks = st.ApplyStagger(seconds * StaggerPower, Has(Notable.Antimony) ? B.AntimonyAttackSlow : 0f);
            if (!Has(Notable.Congelation) || stacks < StatusEffects.MaxStaggerStacks) return;
            if (Time.time - st.CongealedAt < B.CongelationCooldown) return;

            st.ApplyCongeal(B.CongelationSeconds, B.CongelationVulnerability);
            st.ClearStaggerStacks();
            Spr.Flash(target.transform.position, 0.9f, new Color(0.62f, 0.55f, 0.48f, 0.8f), 0.3f);
        }

        static Health Nearest(Health from, float radius)
        {
            Health best = null;
            float bestSq = radius * radius;
            Vector2 at = from.transform.position;
            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp == from || hp.IsDead) continue;
                float d = ((Vector2)e.transform.position - at).sqrMagnitude;
                if (d >= bestSq) continue;
                bestSq = d;
                best = hp;
            }
            return best;
        }

        // ================================================================= incoming

        /// <summary>Solution: a soaked enemy's blows land lighter. Asked with the hit's source.</summary>
        public float ScaleIncoming(DamageInfo info)
        {
            if (!Has(Notable.Solution) || info.Source == null) return 1f;
            var st = info.Source.GetComponent<StatusEffects>();
            return st != null && st.Soaked ? B.SolutionDamageMul : 1f;
        }

        float _shield;

        /// <summary>
        /// The player's incoming hit after mitigation: Alum at full health, Fixation's ceiling on
        /// one hit, then Cibation's shield soaking what is left. Chained after the ledger's and the
        /// principles' own (GameBootstrap.BuildPlayer) - the same order every reduction lands in.
        /// </summary>
        public float ModifyIncoming(float amount)
        {
            if (_owned == 0 || amount <= 0f) return amount;
            var hp = Self;
            if (hp != null)
            {
                if (Has(Notable.Alum) && hp.Current >= hp.Max - 0.5f) amount *= B.AlumDamageMul;
                if (Has(Notable.Fixation)) amount = Mathf.Min(amount, hp.Max * B.FixationMaxHitFraction);
            }
            if (_shield > 0f)
            {
                float absorbed = Mathf.Min(_shield, amount);
                _shield -= absorbed;
                amount -= absorbed;
            }
            return amount;
        }

        /// <summary>The shield Cibation is holding right now, in health.</summary>
        public float Shield => _shield;

        /// <summary>Cibation: lifesteal the healing limit (or full health) turned away.</summary>
        public void AddShield(float overflow)
        {
            if (!Has(Notable.Cibation) || overflow <= 0f) return;
            var hp = Self;
            if (hp == null) return;
            _shield = Mathf.Min(hp.Max * B.CibationShieldFraction, _shield + overflow);
        }

        // ================================================================= deaths

        /// <summary>Any wave enemy died, whatever killed it - a hit, a burn, a fall.</summary>
        public void OnEnemyDied(Health dead)
        {
            if (_owned == 0 || dead == null) return;

            if (Has(Notable.AquaVitae))
            {
                var hp = Self;
                if (hp != null && !hp.IsDead) hp.Heal(hp.Max * B.AquaVitaeHealFraction);
            }

            if (Has(Notable.Cineration))
            {
                var st = dead.GetComponent<StatusEffects>();
                if (st == null || !st.Burning) return;
                float dps = st.BurnDps;
                if (dps <= 0f) return;
                Vector2 at = dead.transform.position;
                Enemies.EnemyRegistry.Prune();
                foreach (var e in Enemies.EnemyRegistry.All)
                {
                    if (e == null) continue;
                    var hp = e.GetComponent<Health>();
                    if (hp == null || hp == dead || hp.IsDead) continue;
                    if (((Vector2)e.transform.position - at).sqrMagnitude > B.CinerationRadius * B.CinerationRadius) continue;
                    // Passed on as it was - already strengthened, so not through Burn() again.
                    StatusEffects.Get(hp.gameObject).ApplyBurn(dps, B.CholerSeconds, gameObject);
                }
                Spr.Flash(at, B.CinerationRadius, new Color(1f, 0.5f, 0.15f, 0.55f), 0.3f, true);
            }
        }

        // ================================================================= the clock

        float _shieldCue;

        void Update()
        {
            float dt = Time.deltaTime;

            if (_flow > 0f && Time.time - _lastHitAt > B.CirculationWindow) _flow = 0f;

            if (_shield > 0f)
            {
                var hp = Self;
                float fade = (hp != null ? hp.Max * B.CibationShieldFraction : _shield) / B.CibationFadeSeconds;
                _shield = Mathf.Max(0f, _shield - fade * dt);

                // The shield's readout: a pale gold ring shed at the feet while it holds.
                _shieldCue -= dt;
                if (_shieldCue <= 0f && _shield > 0f)
                {
                    _shieldCue = 0.45f;
                    Spr.Pulse(transform, 0.7f, new Color(1f, 0.88f, 0.5f, 0.45f), 0.4f, true, 1.3f);
                }
            }
        }
    }
}
