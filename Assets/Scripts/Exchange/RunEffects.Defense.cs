using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.Exchange
{
    public partial class RunEffects
    {
        // ---------------------------------------------------------------- state

        bool _wardReady;
        bool _hitThisFloor;
        float _wardTimer;
        float _reactiveRemaining, _reactiveCooldown;
        float _ghostRemaining, _ghostCooldown;
        int _vapourCount;

        /// <summary>The one clock every ledger negation shares (Tuning.Exchange.NegationGapSeconds):
        /// while it runs, the ward holds, Vapour lets the hit land and Ghostwalk does not start.</summary>
        float _negationLock;
        float _secondWindHealLeft;
        float _riseTo;

        /// <summary>Haemophilia's bleed still owed, and how fast it is paid.</summary>
        float _bleedOwed, _bleedRate;

        void TickDefense(float dt, bool moving)
        {
            Countdown(ref _reactiveRemaining, dt);
            Countdown(ref _reactiveCooldown, dt);
            Countdown(ref _ghostRemaining, dt);
            Countdown(ref _ghostCooldown, dt);
            Countdown(ref _negationLock, dt);

            var hp = Hp;

            // Aegis: the ward renews on its clock - twice as fast standing still under Athanor.
            if (Has("aegis_cycle") && !_wardReady)
            {
                _wardTimer -= dt * (Has("athanor") && !moving ? 2f : 1f);
                if (_wardTimer <= 0f)
                {
                    _wardReady = true;
                    if (_pc != null) Player.RigRim.Glint(_pc.transform, LedgerFx.WardColour);
                }
            }

            if (hp == null || hp.IsDead) return;

            // Second Wind's heal, over its seconds; Phoenix's rise, at once.
            if (_secondWindHealLeft > 0f)
            {
                float step = Mathf.Min(_secondWindHealLeft, hp.Max * T.SecondWindHeal / T.SecondWindHealSeconds * dt);
                _secondWindHealLeft -= step;
                hp.Heal(step);
            }
            if (_riseTo > 0f)
            {
                hp.Heal(Mathf.Max(0f, hp.Max * _riseTo - hp.Current));
                _riseTo = 0f;
            }

            // Haemophilia: the bleed is a price, paid over its seconds.
            if (_bleedOwed > 0f)
            {
                float step = Mathf.Min(_bleedOwed, _bleedRate * dt);
                _bleedOwed -= step;
                Pay(step);
            }
        }

        // ---------------------------------------------------------------- incoming

        /// <summary>
        /// A multiplier on a hit from an ENEMY, asked with the hit (Health.ScaleIncoming):
        /// Senescence's floors and Acetum's seconds make enemies hit harder - never a hazard,
        /// never a price the run charges.
        /// </summary>
        public float ScaleIncoming(DamageInfo info)
        {
            if (_mods == null || !FromEnemy(info)) return 1f;
            float f = 1f;
            if (Has("senescence") && _mods.Senescence > 0) f *= 1f + T.SenescenceTaken * _mods.Senescence;
            if (AtMax("souring")) f *= 1f + T.AcetumTaken * Mathf.Floor(_floorSeconds / T.SouringEverySeconds);
            return f;
        }

        bool FromEnemy(DamageInfo info) => FromEnemy(info, _pc != null ? _pc.gameObject : null);

        /// <summary>Whether a hit on the player came from an ENEMY - a body, its bolt or its shell -
        /// rather than a hazard on the floor or a price the run charges.</summary>
        public static bool FromEnemy(DamageInfo info, GameObject player)
        {
            var s = info.Source;
            if (s == null || info.Price || s == player) return false;
            return s.GetComponent<Hazards.FloorPit>() == null && s.GetComponent<Hazards.Tornado>() == null
                && s.GetComponent<Hazards.Spire>() == null && s.GetComponent<Hazards.ProjectionLines>() == null;
        }

        /// <summary>
        /// The ledger's conditional REDUCTIONS right now - Lapis, Reactive Plate, Fortitude,
        /// Adamant, Retrograde Motion. Mitigation like any other, so PlayerController folds it
        /// INSIDE the one floor (StatCurves.Incoming): they used to land after it, so a build at
        /// the floor could still halve and halve again.
        /// </summary>
        public float MitigationNow
        {
            get
            {
                if (_mods == null) return 1f;
                float f = 1f;
                if (LapisReady) f *= T.LapisMul;
                if (PoiseReady) f *= T.PoiseMul;
                if (_reactiveRemaining > 0f)
                    f *= 1f - (AtMax("reactive_plate") ? T.ReactivePlateReductionII : T.ReactivePlateReduction);
                var hp = Hp;
                float mine = hp != null && hp.Max > 0f ? hp.Current / hp.Max : 1f;
                if (AtMax("thickened_hide") && mine < T.FortitudeBelow) f *= T.FortitudeMul;
                f *= TemperedNow;   // Adamant
                if (Has("retrograde_motion") && Has("retrograde") && InputsInverted) f *= T.RetrogradeMotionTakenMul;
                return f;
            }
        }

        /// <summary>
        /// The Nigredos' exposures right now - each cost's own weak moment, a window of damage taken
        /// OUTSIDE the mitigation floor: Shackled (the defensive ability recharging), Barren (the
        /// meter under half), Wet Ash (Fire under three heat), Ebb (Water out of its surge),
        /// Doldrums (Air under half momentum).
        /// </summary>
        public float ExposureNow
        {
            get
            {
                // Phase 4 (2026-10-20) re-cut Shackled, Barren, Wet Ash, Ebb and Doldrums from
                // exposures into rules of their own (OnHitTaken, OnFloorStarted, DefenceShut) - 14
                // costs ended in "you take more damage while X". None is left here.
                return 1f;
            }
        }

        /// <summary>SHACKLED (Encumbered III): the defensive ability is shut for a moment after an art.</summary>
        public bool DefenceShut(float secondsSinceArt) => AtMax("encumbered") && secondsSinceArt < T.ShackledSeconds;

        /// <summary>Corrosion: worn armour's penalty lands outside the floor.</summary>
        public bool WearOutsideFloor => AtMax("rust");

        /// <summary>Lapis: a second planted halves the next hit.</summary>
        bool LapisReady => AtMax("stonestance") && _stillSeconds >= T.LapisStillSeconds;

        /// <summary>Poise: a change of pace halves the next hit (armed in Tick).</summary>
        bool PoiseReady => _poiseArmed && AtMax("equipoise");

        /// <summary>
        /// Incoming damage after mitigation and before it is subtracted. The ward and the
        /// untouchable windows come first (nothing lands - and they share ONE clock,
        /// NegationGapSeconds), then the costs' multipliers, which sit OUTSIDE the floor so they
        /// bite every build alike, then whatever a hit taken sets going.
        /// </summary>
        public float ModifyIncoming(float amount)
        {
            if (_mods == null || amount <= 0f) return amount;

            if (_ghostRemaining > 0f) return 0f;   // the window already spent the clock
            if (_negationLock <= 0f)
            {
                if (_wardReady && Has("aegis_cycle"))
                {
                    _wardReady = false;
                    _wardTimer = AtMax("aegis_cycle") ? T.AegisRenewSecondsII : T.AegisRenewSeconds;
                    _negationLock = T.NegationGapSeconds;
                    if (_pc != null)
                    {
                        Player.RigRim.Flare(_pc.transform);
                        if (AtMax("aegis_cycle")) Burst(_pc.transform.position, T.TinWardRadius, T.TinWardHitUnits);
                    }
                    return 0f;
                }
                if (AtMax("evanescence") && _pc != null && _pc.IsMoving && ++_vapourCount % T.VapourEvery == 0)
                {
                    _negationLock = T.NegationGapSeconds;
                    Player.RigRim.Flare(_pc.transform);
                    return 0f;
                }
            }

            // Lapis and Poise are spent by the hit they softened (MitigationNow already halved it).
            if (LapisReady) _stillSeconds = 0f;
            if (PoiseReady) _poiseArmed = false;

            float f = 1f;
            if (Has("unbloodied")) f *= 1f + T.UnbloodiedTaken;   // an oath, until a floor is cleared unhit
            if (Has("open_stance") && _openStanceHits < T.OpenStanceHits)
            {
                _openStanceHits++;
                f *= 2f;
            }

            var hp = Hp;
            float mine = hp != null && hp.Max > 0f ? hp.Current / hp.Max : 1f;
            if (Has("glass_bones") && mine < T.GlassBonesBelow) f *= 1f + T.GlassBonesTaken;
            if (_pc != null && _pc.AttackLocked && AtMax("overcommitted")) f *= 1f + T.OverextendedTaken;
            int restless = N("restless");
            if (restless > 0 && _stillSeconds >= T.RestlessStillSeconds) f *= 1f + T.RestlessTaken * restless;

            amount *= f;
            amount = AbsorbShield(amount);   // Viriditas
            OnHitTaken(amount);
            return amount;
        }

        /// <summary>
        /// An ENEMY's hit landed on the player (Health.Damaged, a price or a hazard never): TIDEWATER
        /// soaks the attacker, BACKDRAFT burns out a burning one at once. The attacker is the hit's
        /// source - a body, or a bolt's shooter; a Mortar shell is its own source and counts for neither.
        /// </summary>
        public void OnStruckBy(DamageInfo info)
        {
            if (_mods == null || _pc == null || info.Source == null) return;
            var attacker = info.Source.GetComponent<Health>();
            if (attacker == null || attacker.IsDead || attacker.gameObject == _pc.gameObject) return;
            if (Has("backdraft"))
            {
                var st = attacker.GetComponent<StatusEffects>();
                float left = st != null && st.Burning ? st.ConsumeBurn() : 0f;
                if (left > 0f)
                {
                    attacker.Take(new DamageInfo(left * T.BackdraftShare, ElementType.Fire, _pc.gameObject));
                    LedgerFx.StatusBurst(attacker, TexelFx.Fire);
                }
            }
            if (Has("tidewater") && !attacker.IsDead)
                _pc.Soak(attacker, T.TidewaterSeconds, T.TidewaterVulnerability);
            AlbedosOnStruckBy(attacker);   // Mountain, Lucid
        }

        /// <summary>SATURATE: soaking an enemy already soaked staggers it (PlayerController.Soak asks).</summary>
        public bool Saturates => Has("saturate");

        /// <summary>The windows and spills a hit taken sets going.</summary>
        void OnHitTaken(float amount)
        {
            _hitThisFloor = true;   // Unbloodied
            AlbedosOnHitTaken(amount);   // Adamant, Committed
            // The re-cut Nigredos: each a rule on its own mechanic.
            if (AtMax("smother") && Resource is Player.FireResource fire) fire.Spill(1f / Player.FireResource.MaxStacks);   // Wet Ash
            if (AtMax("low_water") && Resource is Player.WaterResource water) water.EndSurge();                              // Ebb
            if (Has("reactive_plate") && _reactiveCooldown <= 0f)
            {
                _reactiveRemaining = T.ReactivePlateSeconds;
                _reactiveCooldown = AtMax("reactive_plate") ? T.ReactivePlateCooldownTempered   // Tempered
                                                            : T.ReactivePlateCooldown;
            }
            if (Has("ghostwalk") && _ghostCooldown <= 0f && _negationLock <= 0f)
            {
                _ghostRemaining = T.GhostwalkSeconds;
                _ghostCooldown = T.GhostwalkCooldown;
                _negationLock = T.GhostwalkSeconds + T.NegationGapSeconds;
            }
            if (AtMax("retrograde")) _contraryRemaining = T.ContrarySeconds;   // Contrary
            if (AtMax("leaky_vessel")) Resource?.Spill(T.CrackedVesselSpill);   // Cracked Vessel
            if (AtMax("becalmed") && Resource is Player.AirResource air) { air.BreakStreak(); air.Spill(1f); }   // Doldrums
            if (Has("haemophilia") && amount > 0f)
            {
                _bleedOwed += amount * T.HaemophiliaFraction;
                _bleedRate = Mathf.Max(_bleedRate * 0.5f, _bleedOwed / T.HaemophiliaSeconds);
            }
        }

        // ---------------------------------------------------------------- heals

        public float ModifyHeal(float amount)
        {
            if (Has("blood_debt")) return 0f;   // an oath: no healing until it is paid
            var m = M;
            amount *= m.HealMul;
            var hp = Hp;
            if (hp == null || amount <= 0f) return amount;

            // Anaemia: whatever of the heal would land above half health is halved.
            if (AtMax("thin_blood"))
            {
                float line = hp.Max * T.AnaemiaAbove;
                float below = Mathf.Clamp(line - hp.Current, 0f, amount);
                amount = below + (amount - below) * T.AnaemiaMul;
            }

            // Hollow: healing cannot carry you past the ceiling.
            if (m.HealCeiling < 1f)
                amount = Mathf.Min(amount, Mathf.Max(0f, hp.Max * m.HealCeiling - hp.Current));

            // PELICAN (reworked): the heal is also dealt, as damage, to the nearest enemy.
            if (Has("pelican") && amount > 0f && _pc != null)
            {
                var near = NearestEnemy(_pc.transform.position, T.PelicanReach, null);
                if (near != null) near.Take(new DamageInfo(amount * _mods.Potency("pelican"), ElementType.Water, _pc.gameObject));
            }
            return amount;
        }

        /// <summary>True to survive a lethal hit at 1 health - Second Wind, once a floor.</summary>
        public bool SurviveLethal()
        {
            if (!Has("second_wind") || _secondWindSpent || Hp == null) return false;
            _secondWindSpent = true;
            if (Has("phoenix"))
            {
                // Phoenix: risen, and the blast sets the crowd round you burning.
                _riseTo = T.PhoenixRise;
                Ignite(_pc.transform.position, T.PhoenixRadius, T.PhoenixBurnHitUnits, T.PhoenixBurnSeconds);
                LedgerFx.Wings(_pc, phoenix: true);
            }
            else _secondWindHealLeft = Hp.Max * T.SecondWindHeal;
            if (!Has("phoenix")) LedgerFx.Wings(_pc, phoenix: false);
            return true;
        }

        // ---------------------------------------------------------------- shared

        /// <summary>Tin Ward: every live enemy within <paramref name="radius"/> struck for
        /// <paramref name="hitUnits"/> of the player's own hits.</summary>
        void Burst(Vector2 from, float radius, float hitUnits)
        {
            if (_pc == null) return;
            float dmg = hitUnits * _pc.HitUnit;
            foreach (var hp in EnemiesWithin(from, radius))
                hp.Take(new DamageInfo(dmg, Element, _pc.gameObject));
            LedgerFx.Force(from, LedgerFx.Ward, radius);
        }

        /// <summary>Phoenix: every live enemy within <paramref name="radius"/> set burning for
        /// <paramref name="hitUnits"/> of the player's hits over <paramref name="seconds"/> -
        /// through the player's own Burn, so the board's burn rules apply.</summary>
        void Ignite(Vector2 from, float radius, float hitUnits, float seconds)
        {
            if (_pc == null) return;
            float dps = hitUnits * _pc.HitUnit / seconds;
            foreach (var hp in EnemiesWithin(from, radius)) _pc.Burn(hp, dps, seconds);
        }

        static System.Collections.Generic.List<Health> EnemiesWithin(Vector2 from, float radius)
        {
            var list = new System.Collections.Generic.List<Health>();
            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                if (((Vector2)e.transform.position - from).sqrMagnitude <= radius * radius) list.Add(hp);
            }
            return list;
        }

        /// <summary>Every live enemy within <paramref name="radius"/> thrown away from a point.</summary>
        static void Push(Vector2 from, float radius, float distance)
        {
            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                if (((Vector2)e.transform.position - from).sqrMagnitude > radius * radius) continue;
                DragToward.Push(hp, from, distance);
            }
        }
    }
}
