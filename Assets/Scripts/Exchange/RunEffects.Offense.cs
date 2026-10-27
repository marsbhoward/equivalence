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
        /// <summary>THE PROC WEIGHT: how much of a chance-based on-hit status this hit earns - 1 for a
        /// swing, an arrow or a thrown blade, Tuning.Exchange.ProcDisc for a thrown disc, 0 for a repeat.</summary>
        public float Proc;
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
        bool _afterburnThisSwing;   // Afterburn: this basic follows the defensive ability
        float _afterburnUntil;
        int _swingChainIndex;
        bool _perfectArt;
        int _arts;
        /// <summary>The bodies this swing has struck - Scything staggers them all at the third.</summary>
        readonly System.Collections.Generic.List<Health> _swingStruck = new();
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
                if (_pc != null) LedgerFx.Spark(_pc);
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
            _afterburnThisSwing = !isFinisher && Time.time < _afterburnUntil;
            if (_afterburnThisSwing) _afterburnUntil = 0f;
            _felicityThisSwing = Has("felicity") && Random.value < T.FelicityChance;
            _swingStruck?.Clear();
            _felicityUsed = false;
            if (!isFinisher) _perfectArt = false;

            int fumble = N("fumbler");
            bool whiff = fumble > 0 && Random.value < T.FumblerChance * fumble;
            // Iron Rhythm: the swing that passed through makes the next hit repeat.
            if (whiff && Has("iron_rhythm") && Has("fumbler")) _repeatNextHit = true;
            if (!isFinisher && !whiff) Spindrift();
            return !whiff;
        }

        /// <summary>Lapsus: a swing that passes through leaves you stumbling.</summary>
        public bool StumblesOnWhiff => AtMax("fumbler");

        /// <summary>Expansion: the share of an area attack's edge loss it keeps - 1 without it.</summary>
        public float EdgeLossKept => AtMax("dilation") ? T.ExpansionEdgeLoss : 1f;

        /// <summary>Mired: sand and mire slow the player twice as much.</summary>
        public bool Mired => AtMax("anchored");

        // ---------------------------------------------------------------- crits

        /// <summary>
        /// Crit chance this one hit gains before it is rolled: Honed (a body above half health),
        /// Stoop (the hit after a kill), Cementation (every Nth hit on the same enemy). It joins
        /// the ONE pool (StatCurves.Crit) - a player at the cap gets it as crit damage instead.
        /// They once made the hit a crit outright, stepping past the cap every other source obeys.
        /// </summary>
        public float BonusCritChance(Health target, bool isFinisher = false)
        {
            if (_mods == null || target == null) return 0f;
            float add = isFinisher ? 0f : GildedCrit(target);   // Gilding: basics on a gilded body
            if (_stoopRemaining > 0f) add += T.ForcedCritChance;
            // Honed: every hit on a body above half health - the same share however fat it is.
            if (Has("honed") && target.Max > 0f && target.Current / target.Max > T.HonedAbove) add += T.HonedCrit * _mods.Potency("honed");
            if (Has("cementation"))
            {
                var mk = LedgerMarks.Peek(target.gameObject);
                if (((mk != null ? mk.Hits : 0) + 1) % T.CementationEvery == 0) add += T.ForcedCritChance;
            }
            return add;
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

            // First Blood: a share of the body's own health, on the attack's MAIN target only - on
            // every body a swing passed through it was worth more the bigger the crowd. A share
            // rather than Damage points, which fell to nothing as bodies grew: the same opener at
            // floor 5 and floor 95. Never a boss's.
            float opener = 0f;
            if (Has("first_blood") && h.First && !h.Repeat && frac >= 0.999f
                && target.GetComponent<Bosses.Boss>() == null)
            {
                var mk = LedgerMarks.Peek(target.gameObject);
                if (mk == null || !mk.Struck) opener = target.Max * T.FirstBloodShare;
            }

            if (AtMax("long_reach") && h.Reach01 >= T.FarStrikeFrom) up += T.FarStrikeDamage;
            if (AtMax("short_arm") && h.Reach01 > T.CrampedFrom) down -= T.CrampedDamage;
            if (Has("close_quarters") && h.Reach01 <= T.CloseQuartersWithin) up += T.CloseQuartersDamage * _mods.Potency("close_quarters");

            if (_wakeThisSwing && !h.IsFinisher) up += T.WakeDamage;
            if (_afterburnThisSwing && !h.IsFinisher) up += T.AfterburnDamage * Strength("afterburn");
            if (Has("deliberate")) up += T.DeliberateDamagePerBasic * _swingChainIndex * _mods.Potency("deliberate");

            if (Has("damascene"))
            {
                var mk = LedgerMarks.Peek(target.gameObject);
                if (mk != null && mk.ScoredHits > 0) up += T.DamasceneDamage;
            }

            var hp = Hp;
            float mine = hp != null && hp.Max > 0f ? hp.Current / hp.Max : 1f;
            if (Has("bloodstone") && Has("blood_price"))
                up += Mathf.Min(T.BloodstoneCap, (1f - mine) / T.BloodstoneMissingPerPoint);

            float tens = Mathf.Floor(_floorSeconds / T.SouringEverySeconds);
            int sour = N("souring");
            if (sour > 0) down -= Mathf.Min(T.SouringCap, T.SouringDamage * tens) * sour;

            if (_trapRemaining > 0f) up += T.TrapBonusDamage;

            damage = damage * m.Factor(StatKind.Damage, up, down) + opener;
            damage = AlbedosOutgoing(target, damage, h);   // Maturation, Committed
            // Dulled Script: the art on its socket deals less (a socket cost, paid for a form).
            if (h.IsFinisher && _pc != null) damage *= SocketDamageMul(_pc.ArtSocket);

            // Green Lion: a weapon art settled PERFECT, in Weapon Art points.
            if (h.IsFinisher && _perfectArt && Has("green_lion"))
                damage *= m.Factor(StatKind.FinisherPower, T.GreenLionArt * N("green_lion"));
            return damage;
        }

        /// <summary>Keen Edge: how much faster your hits spend enemy armour - 1 without it.</summary>
        public float ArmourShred => AtMax("whetstone") ? T.KeenEdgeShred : 1f;

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
                    // Damascene: an art scores the body for its next hits - spent one a hit, the
                    // scoring art's own hit spending the last score before it scores again.
                    if (Has("damascene"))
                    {
                        if (marks.ScoredHits > 0) marks.ScoredHits--;
                        if (h.IsFinisher) marks.ScoredHits = T.DamasceneHits;
                    }
                }
                _stoopRemaining = 0f;   // Stoop's crit is one hit's; Hunt's lasts its seconds
            }

            // Fulminate: a crit bursts onto the bodies round its target.
            if (info.Crit && AtMax("vein_finder") && !h.Repeat)
                CrowdHits.Splash(target, info.Amount, T.FulminateFraction, info.Element, _pc.gameObject, T.FulminateRadius);

            // The status capstones (2026-10-20 - each used to add only a stat), and the status layer:
            if (!h.Repeat && !target.IsDead) StatusCapstones(target, info, h);
            if (!h.Repeat && !target.IsDead) StatusLayer(target, info, h);
            if (!h.Repeat && !target.IsDead) FormsOnHit(target, info, h);
            if (!h.Repeat) AlbedosOnHitLanded(target, h);   // Gilding, Mollification

            // Crushing Blow: every weapon art staggers what it hits - its attacks come slower,
            // armoured or not. Not a flinch: armoured flinch is what makes an art Heavy.
            if (h.IsFinisher && AtMax("heavy_payoff") && !target.IsDead)
                _pc.Stagger(target, T.CrushingBlowStagger);

            // Coup de Grace: what the cut left this low, it finishes.
            if (AtMax("executioner") && !target.IsDead && target.Max > 0f
                && target.Current / target.Max <= T.CoupDeGraceBelow)
            {
                var enemy = target.GetComponent<Enemies.EnemyController>();
                if (enemy != null && !enemy.Elite)
                {
                    LedgerFx.CoupLine(target);
                    target.Kill();
                }
            }

            if (h.Repeat || target.IsDead) return;

            bool repeat = false;
            int rei = N("reiteration");
            // Counted per ATTACK (its main target), not per body: per body, a swing through a pack
            // or a disc landing five times as often as a sword repeated on nearly every press.
            if (rei > 0 && h.First)
            {
                _hitCount++;
                int every = Mathf.Max(2, T.ReiterationEvery - (rei - 1));
                if (_hitCount % every == 0) repeat = true;
            }
            if (_repeatNextHit) { _repeatNextHit = false; repeat = true; }
            // Gemini: the repeat is a whole twin of the hit, not half of it.
            if (repeat) Repeat(target, info, Has("gemini") ? 1f : T.ReiterationFraction, AtMax("reiteration"));

            // FELICITY (reworked): one swing in nine throws a crescent along the aim.
            if (_felicityThisSwing && !_felicityUsed && h.First)
            {
                _felicityUsed = true;
                Vector2 from = _pc.transform.position, dir = _pc.Facing.sqrMagnitude > 0.01f ? _pc.Facing.normalized : Vector2.right;
                float dmg = info.Amount * T.FelicityShare * _mods.Potency("felicity");
                foreach (var hp in EnemiesInStrip(from, dir, T.FelicityReach, T.FelicityHalfWidth))
                    if (hp != target) hp.Take(new DamageInfo(dmg, info.Element, _pc.gameObject));
                LedgerFx.Crest(ReleaseCanvas.Feet(_pc.transform), dir, T.FelicityReach, TexelFx.Gold);
            }
            // FURY OF THE FRAIL (reworked): low on health, basics bleed.
            if (!h.IsFinisher && Has("fury_of_the_frail") && Hp != null && Hp.Max > 0f && Hp.Current / Hp.Max < T.FuryBelow && !target.IsDead)
                _pc.Bleed(target, info.Amount * T.FuryBleedShare * _mods.Potency("fury_of_the_frail") / T.FuryBleedSeconds, T.FuryBleedSeconds);
        }

        /// <summary>
        /// Scything (Wide Arc II): a SWING that strikes enough bodies staggers every one. Eddy
        /// (Ricochet II): a thrown disc's bounce - any hit but its first - soaks. Broadhead
        /// (Fletching II): an arrow bleeds what it strikes. All through the player's own status
        /// calls, so the board's status rules apply.
        /// </summary>
        void StatusCapstones(Health target, DamageInfo info, in HitContext h)
        {
            var weapon = _mods.Weapon;
            // The bodies this swing has struck - Scything and Fault Line count them.
            if (!h.Thrown && _swingStruck != null && !_swingStruck.Contains(target)) _swingStruck.Add(target);
            if (!h.Thrown && AtMax("wide_arc") && _swingStruck != null)
            {
                if (_swingStruck.Count == T.ScythingBodies)
                    foreach (var hp in _swingStruck) { if (hp != null && !hp.IsDead) _pc.Stagger(hp, T.ScythingStagger); }
                else if (_swingStruck.Count > T.ScythingBodies) _pc.Stagger(target, T.ScythingStagger);
            }
            if (h.Thrown && !h.First && weapon == WeaponClass.Disc && AtMax("ricochet"))
                _pc.Soak(target, T.EddySoakSeconds, T.EddySoakVulnerability);
            if (h.Thrown && weapon == WeaponClass.Bow && AtMax("fletching"))
                _pc.Bleed(target, info.Amount * T.BroadheadBleedShare / T.BroadheadBleedSeconds, T.BroadheadBleedSeconds);
        }

        /// <summary>
        /// The status layer's on-hit entries: TINDER (a basic may set its target burning, by its proc
        /// weight), SERRATED (the chain's first basic bleeds all it strikes), PIN DOWN (a basic holds
        /// a staggered body down) and LANCET (a weapon art ends a bleed for half again what it had left).
        /// </summary>
        void StatusLayer(Health target, DamageInfo info, in HitContext h)
        {
            if (!h.IsFinisher)
            {
                if (Has("tinder") && Random.value < T.TinderChance * h.Proc)
                    _pc.Burn(target, info.Amount * T.TinderBurnShare, T.TinderSeconds);
                if (Has("serrated") && _swingChainIndex == 0)
                    _pc.Bleed(target, info.Amount * T.SerratedBleedShare, T.SerratedSeconds);
                if (Has("pin_down"))
                {
                    var st = target.GetComponent<StatusEffects>();
                    if (st != null && st.Staggered) st.ExtendStagger(T.PinDownSeconds);
                }
            }
            else if (Has("lancet"))
            {
                var st = target.GetComponent<StatusEffects>();
                float left = st != null ? st.ConsumeBleed() : 0f;
                if (left > 0f && !target.IsDead)
                {
                    target.Take(new DamageInfo(left * T.LancetShare, ElementType.Earth, _pc.gameObject));
                    LedgerFx.StatusBurst(target, LedgerFx.Blood);
                }
            }
        }

        /// <summary>
        /// The hit again, for a share of itself (Gemini: all of it); Rota sends a
        /// repeat that kills on to the nearest enemy, to repeat again. A repeat is a plain hit - it
        /// repeats nothing and feeds no counter, or the boons would compound on themselves.
        /// </summary>
        void Repeat(Health target, DamageInfo info, float fraction, bool chainKills)
        {
            var at = target;
            for (int hops = 0; hops < 6 && at != null && !at.IsDead; hops++)
            {
                bool crit = info.Crit;
                float dmg = info.Amount * fraction;
                var where = (Vector2)at.transform.position;
                at.Take(new DamageInfo(dmg, info.Element, _pc.gameObject) { IsFinisher = info.IsFinisher, Crit = crit });
                LedgerFx.Echo(where, _pc.transform.position);
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
        public void OnArtSettled(StrikeVerdict verdict)
        {
            _perfectArt = verdict == StrikeVerdict.Perfect;
            // KNELL: a perfect press rings out, staggering everything round you.
            if (_perfectArt && Has("knell") && _pc != null)
            {
                foreach (var hp in EnemiesWithin(_pc.transform.position, T.KnellRadius)) _pc.Stagger(hp, T.KnellStagger);
                LedgerFx.ForceRound(_pc, LedgerFx.Lilac, T.KnellRadius);
            }
        }

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

        /// <summary>
        /// An enemy died, whatever killed it - heard from GameBootstrap.HookDeath.
        /// <paramref name="worth"/> is the body's wave cost in Chaser-equivalents (the Rift Box's
        /// measure), so the per-kill entries pay for how much killing it took, not for bodies.
        /// </summary>
        public void OnEnemyDied(Health h, float worth = 1f)
        {
            if (_mods == null || h == null || _pc == null) return;
            var at = h.transform.position;
            bool byArt = h.LastDamage.IsFinisher && h.LastDamage.Source == _pc.gameObject;

            if (Has("eagle")) _eagleRemaining = AtMax("eagle") ? T.EagleSecondsII : T.EagleSeconds;
            if (AtMax("eagle")) _stoopRemaining = T.StoopSeconds;
            if (Has("hunt")) _huntRemaining = T.HuntSeconds;

            float counts = Mathf.Clamp(worth, 0f, T.KillWorthCap);
            int ouro = N("ouroboros");
            if (ouro > 0 && (Random.value < T.OuroborosChance * ouro * counts || (AtMax("ouroboros") && byArt)))
            {
                _pc.RefundFinisher();
                LedgerFx.Spark(_pc);
            }

            if (AtMax("rich_vein")) Resource?.Refund(T.MotherLodeRefill * counts);
            FormsOnKill(h);

            // BLOOD DEBT (an oath): paid in kills, by the body's worth.
            if (Has("blood_debt")) _mods.AddOathProgress("blood_debt", counts, T.BloodDebtWorth);
            // VITAL SPARK (reworked): a kill while low heals.
            var me = Hp;
            if (Has("vital_spark") && me != null && me.Max > 0f && me.Current / me.Max < T.VitalSparkBelow)
                _pc.HealWithinLimit(me.Max * T.VitalSparkKillHeal * _mods.Potency("vital_spark"));

            int dead = N("dead_weight");
            if (dead > 0)
                Corpse.Spawn(at, T.DeadWeightSeconds + dead, _pc.gameObject,
                             AtMax("dead_weight") ? CorpseMode.StopsShots : CorpseMode.BlocksPlayer);
            else if (Has("ossuary"))
                OssuaryCorpse(at);
        }

        // ---------------------------------------------------------------- speed

        /// <summary>Move Speed points the ledger's conditions add right now (Eagle, Retrograde Motion).</summary>
        public float MoveSpeedUp
            => (_eagleRemaining > 0f ? T.EagleMove : 0f)
             + (Has("retrograde_motion") && Has("retrograde") && InputsInverted ? T.RetrogradeMotionMove : 0f);

        /// <summary>Attack Speed points the ledger's conditions take right now (Quicksand).</summary>
        public float AttackSpeedDown => AtMax("restless") && _stillSeconds > 0.25f ? -T.QuicksandSpeed : 0f;
    }
}
