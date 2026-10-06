using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.Exchange
{
    /// <summary>One landed hit, as the ledger needs to see it.</summary>
    public struct HitContext
    {
        public bool IsFinisher;
        /// <summary>The swing's DESIGNATED body (its nearest), or a projectile's first.</summary>
        public bool First;
        /// <summary>How far out it landed, as a share of the reach (0 under the player).</summary>
        public float Reach01;
        public bool Thrown;
        /// <summary>This hit IS a repeat: it repeats nothing and counts toward nothing.</summary>
        public bool Repeat;
    }

    public partial class RunEffects
    {
        /// <summary>Told of every landed swing or throw - the transmutation circle's contacts. Set
        /// by the run; a delegate does not survive a domain reload, which only costs a contact.</summary>
        public System.Action Contact;

        // ---------------------------------------------------------------- state

        int _hitCount;
        bool _repeatNextHit;
        bool _felicityThisSwing, _felicityUsed;
        bool _wakeReady, _wakeThisSwing;
        int _swingChainIndex;
        bool _perfectArt;
        int _arts;
        float _eagleRemaining, _stoopRemaining, _huntRemaining;

        void TickOffense(float dt)
        {
            Countdown(ref _eagleRemaining, dt);
            Countdown(ref _stoopRemaining, dt);
            Countdown(ref _huntRemaining, dt);

            // Wake: a second at full speed readies the next basic.
            if (AtMax("fleetfoot") && _movingSeconds >= T.WakeAfterSeconds && !_wakeReady)
            {
                _wakeReady = true;
                if (_pc != null) Spr.Pulse(_pc.transform, 0.7f, new Color(0.7f, 0.95f, 1f, 0.6f), 0.22f, true, 0.8f);
            }
        }

        // ---------------------------------------------------------------- the swing

        /// <summary>
        /// A swing, throw or arrow has been committed - once per attack, before anything resolves.
        /// Pays what a swing costs (Blood Price) and returns false when it will connect with
        /// nothing (Fumbler). A fumbled swing still costs the swing - it plays, the chain
        /// advances, the cooldown runs - which is what makes it a cost rather than a pause.
        /// </summary>
        public bool OnSwing(bool isFinisher)
        {
            var hp = Hp;
            int price = N("blood_price");
            if (price > 0 && hp != null)
            {
                float cost = hp.Max * T.BloodPriceSwing * price;
                // Haemorrhage: below half, every swing costs twice - the spiral is the point.
                if (AtMax("blood_price") && hp.Current < hp.Max * T.HaemorrhageBelow) cost *= 2f;
                Pay(cost);
            }
            if (Has("pelican") && hp != null) _pc.HealWithinLimit(hp.Max * T.PelicanHeal);

            // Where this swing sits in its chain - read before the chain advances.
            _swingChainIndex = _pc != null ? Mathf.Min(_pc.ComboIndex, _pc.BasicsPerChain) : 0;
            _wakeThisSwing = !isFinisher && _wakeReady;
            if (_wakeThisSwing) _wakeReady = false;
            _felicityThisSwing = Has("felicity") && Random.value < T.FelicityChance;
            _felicityUsed = false;
            if (!isFinisher) _perfectArt = false;

            int fumble = N("fumbler");
            bool whiff = fumble > 0 && Random.value < T.FumblerChance * fumble;
            // Iron Rhythm: the swing that passed through makes the next hit repeat.
            if (whiff && Has("iron_rhythm") && Has("fumbler")) _repeatNextHit = true;
            return !whiff;
        }

        /// <summary>Lapsus: a swing that passes through leaves you stumbling.</summary>
        public bool StumblesOnWhiff => AtMax("fumbler");

        /// <summary>Expansion: area attacks deal full damage out to their edge.</summary>
        public bool FullEdge => AtMax("dilation");

        /// <summary>Mired: sand and mire slow the player twice as much.</summary>
        public bool Mired => AtMax("anchored");

        // ---------------------------------------------------------------- crits

        /// <summary>
        /// Whether this hit is a crit before any roll is made: Honed (the first hit on each enemy),
        /// Stoop (the hit after a kill), Cementation (every Nth hit on the same enemy).
        /// </summary>
        public bool ForcesCrit(Health target)
        {
            if (_mods == null || target == null) return false;
            if (_stoopRemaining > 0f) return true;
            if (Has("honed"))
            {
                var mk = LedgerMarks.Peek(target.gameObject);
                if (mk == null || !mk.Struck) return true;
            }
            if (Has("cementation"))
            {
                var mk = LedgerMarks.Peek(target.gameObject);
                if (((mk != null ? mk.Hits : 0) + 1) % T.CementationEvery == 0) return true;
            }
            return false;
        }

        /// <summary>
        /// What a crit is worth after Cold Iron: its BONUS cut by a third a stack - nothing left
        /// at III - and under Quenched less than an ordinary hit. A crit still IS a crit: everything
        /// that keys off critting (a bleed, Air's momentum) still fires.
        /// </summary>
        public float CritMultiplier(float multiplier)
        {
            int iron = N("cold_iron");
            if (iron <= 0) return multiplier;
            if (AtMax("cold_iron")) return T.QuenchedMul;
            return 1f + (multiplier - 1f) * Mathf.Max(0f, 1f - T.ColdIronShare * iron);
        }

        // ---------------------------------------------------------------- outgoing damage

        /// <summary>
        /// One hit's conditional damage against one target, before it lands - as run-layer POINTS
        /// joined to the ledger's own and bent together (Mods.Factor), so the conditional boons
        /// share the Vessel's ceiling and the conditional costs pass straight through.
        /// </summary>
        public float ModifyOutgoing(Health target, float damage, in HitContext h)
        {
            if (_mods == null || target == null) return damage;
            var m = M;
            float up = 0f, down = 0f;
            float frac = target.Max > 0f ? target.Current / target.Max : 1f;

            int exec = N("executioner");
            if (exec > 0 && (frac <= T.ExecutionerBelow || (_huntRemaining > 0f && Has("hunt"))))
                up += T.ExecutionerDamage * exec;

            if (Has("first_blood") && !h.Repeat && frac >= 0.999f)
            {
                var mk = LedgerMarks.Peek(target.gameObject);
                if (mk == null || !mk.Struck) up += T.FirstBloodDamage;
            }

            if (AtMax("long_reach") && h.Reach01 >= T.FarStrikeFrom) up += T.FarStrikeDamage;
            if (AtMax("short_arm") && h.Reach01 > T.CrampedFrom) down -= T.CrampedDamage;
            if (Has("close_quarters") && h.Reach01 <= T.CloseQuartersWithin) up += T.CloseQuartersDamage;

            if (_wakeThisSwing && !h.IsFinisher) up += T.WakeDamage;
            if (Has("deliberate")) up += T.DeliberateDamagePerBasic * _swingChainIndex;

            if (Has("damascene") && !h.IsFinisher)
            {
                var mk = LedgerMarks.Peek(target.gameObject);
                if (mk != null && mk.ScoredBasics > 0) up += T.DamasceneDamage;
            }

            var hp = Hp;
            float mine = hp != null && hp.Max > 0f ? hp.Current / hp.Max : 1f;
            if (Has("fury_of_the_frail") && mine < T.FuryBelow) up += T.FuryDamage;
            if (Has("bloodstone") && Has("blood_price"))
                up += Mathf.Min(T.BloodstoneCap, (1f - mine) / T.BloodstoneMissingPerPoint);

            float tens = Mathf.Floor(_floorSeconds / T.SouringEverySeconds);
            int sour = N("souring");
            if (sour > 0) down -= Mathf.Min(T.SouringCap, T.SouringDamage * tens) * sour;
            if (Has("maturation")) up += Mathf.Min(T.MaturationCap, T.MaturationDamage * tens);

            if (Has("mountain") && _stillSeconds > 0.25f) up += T.MountainDamage;
            if (_trapRemaining > 0f) up += T.TrapBonusDamage;

            damage *= m.Factor(StatKind.Damage, up, down);

            // Green Lion: a weapon art settled PERFECT, in Weapon Art points.
            if (h.IsFinisher && _perfectArt && Has("green_lion"))
                damage *= m.Factor(StatKind.FinisherPower, T.GreenLionArt * N("green_lion"));
            return damage;
        }

        /// <summary>Keen Edge: hits ignore enemy armour.</summary>
        public bool PiercesArmour => AtMax("whetstone");

        /// <summary>Cleaving Habit: basics lose nothing for each body they pass through.</summary>
        public bool NoChainFalloff => AtMax("wide_arc");

        /// <summary>Broadhead: pierced enemies take the full hit.</summary>
        public bool PierceFull => AtMax("fletching");

        /// <summary>Boomerang: ricochets lose nothing.</summary>
        public bool RicochetsKeepAll => AtMax("ricochet");

        // ---------------------------------------------------------------- landed hits

        /// <summary>A hit landed. Counters, scores, and the repeats (Reiteration, Iron Rhythm,
        /// Felicity) - from swings and throws alike.</summary>
        public void OnHitLanded(Health target, DamageInfo info, in HitContext h)
        {
            if (_mods == null || target == null || _pc == null) return;

            // A landed swing or throw is a combat contact - once per attack, on its first body.
            if (h.First && !h.Repeat) Contact?.Invoke();

            if (!h.Repeat)
            {
                bool tracks = Has("first_blood") || Has("honed") || Has("cementation") || Has("damascene");
                if (tracks)
                {
                    var marks = LedgerMarks.Of(target.gameObject);
                    marks.Struck = true;
                    marks.Hits++;
                    if (Has("damascene"))
                    {
                        if (h.IsFinisher) marks.ScoredBasics = T.DamasceneBasics;
                        else if (marks.ScoredBasics > 0) marks.ScoredBasics--;
                    }
                }
                _stoopRemaining = 0f;   // Stoop's crit and Hunt's mark are each one hit's
                _huntRemaining = 0f;
            }

            // Fulminate: a crit bursts onto the bodies round its target.
            if (info.Crit && AtMax("vein_finder") && !h.Repeat)
                CrowdHits.Splash(target, info.Amount, T.FulminateFraction, info.Element, _pc.gameObject, T.FulminateRadius);

            // Crushing Blow: every weapon art flinches, armoured or not - denial, not displacement.
            if (h.IsFinisher && AtMax("heavy_payoff") && !target.IsDead)
                target.GetComponent<Enemies.EnemyController>()?.Flinch(Tuning.Finisher.FlinchSeconds, true);

            // Coup de Grace: what the cut left this low, it finishes.
            if (AtMax("executioner") && !target.IsDead && target.Max > 0f
                && target.Current / target.Max <= T.CoupDeGraceBelow)
            {
                var enemy = target.GetComponent<Enemies.EnemyController>();
                if (enemy != null && !enemy.Elite)
                {
                    Spr.Flash(target.transform.position, 0.9f, new Color(1f, 0.25f, 0.2f), 0.25f, false);
                    target.Kill();
                }
            }

            if (h.Repeat || target.IsDead) return;

            bool repeat = false;
            int rei = N("reiteration");
            if (rei > 0)
            {
                _hitCount++;
                int every = Mathf.Max(2, T.ReiterationEvery - (rei - 1));
                if (_hitCount % every == 0) repeat = true;
            }
            if (_repeatNextHit) { _repeatNextHit = false; repeat = true; }
            if (repeat) Repeat(target, info, T.ReiterationFraction, AtMax("reiteration"));

            if (_felicityThisSwing && !_felicityUsed && h.First && !target.IsDead)
            {
                _felicityUsed = true;
                Repeat(target, info, 1f, false);
            }
        }

        /// <summary>
        /// The hit again, for a share of itself. Gemini makes every repeat a crit; Rota sends a
        /// repeat that kills on to the nearest enemy, to repeat again. A repeat is a plain hit - it
        /// repeats nothing and feeds no counter, or the boons would compound on themselves.
        /// </summary>
        void Repeat(Health target, DamageInfo info, float fraction, bool chainKills)
        {
            var at = target;
            for (int hops = 0; hops < 6 && at != null && !at.IsDead; hops++)
            {
                bool crit = info.Crit || Has("gemini");
                float dmg = info.Amount * fraction;
                if (crit && !info.Crit) dmg *= _pc.CritMultiplierNow;
                var where = (Vector2)at.transform.position;
                at.Take(new DamageInfo(dmg, info.Element, _pc.gameObject) { IsFinisher = info.IsFinisher, Crit = crit });
                Spr.Flash(where, 0.5f, new Color(1f, 0.95f, 0.7f), 0.18f, false);
                if (!chainKills || !at.IsDead) break;
                at = NearestEnemy(where, 4f, at);
            }
        }

        static Health NearestEnemy(Vector2 from, float within, Health exclude)
        {
            Health best = null;
            float bestD = within * within;
            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead || hp == exclude) continue;
                float d = ((Vector2)e.transform.position - from).sqrMagnitude;
                if (d >= bestD) continue;
                bestD = d;
                best = hp;
            }
            return best;
        }

        // ---------------------------------------------------------------- weapon arts

        /// <summary>A weapon art's timing bar has settled (StrikeJudge).</summary>
        public void OnArtSettled(StrikeVerdict verdict) => _perfectArt = verdict == StrikeVerdict.Perfect;

        /// <summary>Red Lion: the perfect streak survives a GOOD.</summary>
        public bool StreakSurvivesGood => AtMax("green_lion");

        /// <summary>
        /// A weapon art has completed and the chain reset. True when the NEXT art should be banked
        /// at once - Golden Chain's every third art.
        /// </summary>
        public bool OnArtCompleted()
        {
            if (!Has("golden_chain")) return false;
            _arts++;
            return _arts % T.GoldenChainEvery == T.GoldenChainEvery - 1;
        }

        // ---------------------------------------------------------------- kills

        /// <summary>An enemy died, whatever killed it - heard from GameBootstrap.HookDeath.</summary>
        public void OnEnemyDied(Health h)
        {
            if (_mods == null || h == null || _pc == null) return;
            var at = h.transform.position;
            bool byArt = h.LastDamage.IsFinisher && h.LastDamage.Source == _pc.gameObject;

            if (Has("eagle")) _eagleRemaining = AtMax("eagle") ? T.EagleSecondsII : T.EagleSeconds;
            if (AtMax("eagle")) _stoopRemaining = T.StoopSeconds;
            if (Has("hunt")) _huntRemaining = T.HuntSeconds;

            int ouro = N("ouroboros");
            if (ouro > 0 && (Random.value < T.OuroborosChance * ouro || (AtMax("ouroboros") && byArt)))
            {
                _pc.RefundFinisher();
                Spr.Pulse(_pc.transform, 0.9f, new Color(1f, 0.85f, 0.4f, 0.7f), 0.25f, true, 1.2f);
            }

            if (AtMax("rich_vein")) Resource?.Refund(T.MotherLodeRefill);

            int dead = N("dead_weight");
            if (dead > 0)
                Corpse.Spawn(at, T.DeadWeightSeconds + dead, _pc.gameObject,
                             AtMax("dead_weight") ? CorpseMode.StopsShots : CorpseMode.BlocksPlayer);
            else if (Has("ossuary"))
                Corpse.Spawn(at, T.DeadWeightSeconds + 3f, _pc.gameObject, CorpseMode.BlocksEnemies);
        }

        // ---------------------------------------------------------------- speed

        /// <summary>Move Speed points the ledger's conditions add right now (Eagle, Retrograde Motion).</summary>
        public float MoveSpeedUp
            => (_eagleRemaining > 0f ? T.EagleMove : 0f)
             + (Has("retrograde_motion") && Has("retrograde") && InputsInverted ? T.RetrogradeMotionMove : 0f);

        /// <summary>Lightfoot: Graze counts double at full speed - a second moving.</summary>
        public bool GrazeDoubled => Has("lightfoot") && _movingSeconds >= T.WakeAfterSeconds;

        /// <summary>Attack Speed points the ledger's conditions take right now (Quicksand).</summary>
        public float AttackSpeedDown => AtMax("restless") && _stillSeconds > 0.25f ? -T.QuicksandSpeed : 0f;
    }
}
