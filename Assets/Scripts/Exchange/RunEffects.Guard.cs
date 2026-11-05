using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Combat;
using Convergence.Core;
using T2 = Convergence.Core.Tuning.Overhaul;

namespace Convergence.Exchange
{
    /// <summary>
    /// THE GUARD KIND (the overhaul, 2026-11-04): forms by DEFENSIVE ABILITY - Dash, Barrier, Bulwark,
    /// Parry Stance - their coat skeletons, and the CONCORDS (weapon + defence, finished by the
    /// element's coat; offered only at the brand deal). None of these shortens a cooldown or widens
    /// a window: those are gear's thresholds.
    /// </summary>
    public partial class RunEffects
    {
        // ---------------------------------------------------------------- state

        Vector2 _defenceFrom;
        bool _wasDashing, _wasBarrier, _wasBulwark;
        float _landingUntil;
        float _auraTick, _concordTick, _embrasureTick;
        float _backlashBank;
        float _strainBank;
        bool _freeArrow, _woundBlade;
        int _parriesThisFloor;
        float _parryOrbitUntil;
        float _parryWindowCheckUntil = -1f;
        bool _parriedInWindow;
        readonly Dictionary<Health, int> _readMarks = new();
        readonly HashSet<Health> _stakeStruck = new();

        DefensiveAbility Ability => _pc != null ? _pc.EquippedDefensiveAbility : DefensiveAbility.Dash;

        /// <summary>A Concord's strength: its stacks (I-III).</summary>
        float ConcordPower(string id) => Has(id) ? Strength(id) : 0f;

        /// <summary>One strike a Concord makes: hit units, then the element's coat.</summary>
        void ConcordHit(Health h, string id, float hitUnits)
        {
            if (h == null || h.IsDead || _pc == null) return;
            float p = ConcordPower(id);
            h.Take(new DamageInfo(hitUnits * _pc.HitUnit * p, Element, _pc.gameObject) { Entry = id });
            if (!h.IsDead) Coat(h, p);
        }

        // ---------------------------------------------------------------- the ability used

        void OverhaulDefenceUsed()
        {
            if (_mods == null || _pc == null) return;
            _defenceFrom = _pc.transform.position;
            var hp = Hp;
            if (hp != null && Has("co_exertion")) Pay(hp.Max * T2.ExertionHealth);
            if (hp != null && (AtMax("co_strain") || AtMax("co_brittle_guard") || AtMax("co_exertion"))) Pay(hp.Max * 0.02f);
            if (hp != null && Has("al_exertion")) _pc.HealWithinLimit(hp.Max * T2.ExertionHeal * _mods.Potency("al_exertion"));
            if (Has("al_strain")) _strainBank = Mathf.Min(0.3f, _strainBank + T2.StrainBank * _mods.Potency("al_strain"));

            switch (Ability)
            {
                case DefensiveAbility.Dash:
                    if (Has("gd_afterimage")) Decoy(_defenceFrom, T2.DecoySeconds * Strength("gd_afterimage"));
                    if (Has("gd_landing")) _landingUntil = Time.time + Tuning.Defense.DashSeconds + T2.LandingWindow;
                    if (Has("cc_bow_dash_a"))
                    {
                        var near = NearestEnemy(_pc.transform.position, 8f, null);
                        if (near != null) { ConcordHit(near, "cc_bow_dash_a", 1.5f); LedgerFx.Echo(near.transform.position, _pc.transform.position); }
                    }
                    if (Has("cc_bow_dash_b")) StartCoroutine(StakedTrap(_defenceFrom));
                    if (Has("cc_disc_dash_a")) StartCoroutine(LeftBehind(_defenceFrom));
                    break;
            }

            // A parry stance press opens a window to catch "nothing to parry" (Off Balance, Feint).
            if (Ability == DefensiveAbility.ParryStance && (Has("co_off_balance") || Has("al_off_balance")))
            {
                _parryWindowCheckUntil = Time.time + Tuning.Defense.ParryWindowSeconds * M.DefenseWindowMul + 0.05f;
                _parriedInWindow = false;
            }
        }

        IEnumerator StakedTrap(Vector2 at)
        {
            TexelFx.Star(at, TexelFx.Steel, 3);
            for (float t = 0f; t < 4f; t += 0.1f)
            {
                yield return new WaitForSeconds(0.1f);
                if (_pc == null) yield break;
                if (EnemiesWithin(at, 1f).Count == 0) continue;
                foreach (var hp in EnemiesWithin(at, 1.5f)) ConcordHit(hp, "cc_bow_dash_b", 2f);
                LedgerFx.Stuff(at, CoatRamp, 1.5f, true);
                yield break;
            }
        }

        IEnumerator LeftBehind(Vector2 at)
        {
            TexelFx.Ring(at, TexelFx.Steel, 0.2f, 0.4f, T2.ConcordOrbitSeconds);
            yield return new WaitForSeconds(T2.ConcordOrbitSeconds);
            if (_pc == null) yield break;
            Vector2 to = _pc.transform.position;
            foreach (var hp in EnemiesNearSegment(at, to, 0.8f)) ConcordHit(hp, "cc_disc_dash_a", 1f);
            LedgerFx.Crest(at, (to - at).normalized, (to - at).magnitude, TexelFx.Steel);
        }

        /// <summary>The dash has ended - its path is known.</summary>
        void OnDashEnded()
        {
            Vector2 from = _defenceFrom, to = _pc.transform.position;
            if (Has("gd_cutting_dash"))
                foreach (var hp in EnemiesNearSegment(from, to, T2.CuttingDashHalfWidth))
                    hp.Take(new DamageInfo(T2.CuttingDashHitUnits * _pc.HitUnit * Strength("gd_cutting_dash"), ElementType.Air, _pc.gameObject) { Entry = "gd_cutting_dash" });
            if (Has("gd_wake_dash"))
                foreach (var hp in EnemiesNearSegment(from, to, T2.CuttingDashHalfWidth))
                    StatusEffects.Get(hp.gameObject).ApplySlow(T2.WakeDashSeconds * Strength("gd_wake_dash"), T2.WakeDashSlow);
            if (Has("gd_coat_trail"))
                foreach (var hp in EnemiesNearSegment(from, to, T2.CuttingDashHalfWidth)) Coat(hp, Strength("gd_coat_trail"));
            if (Has("cc_gs_dash_a"))
                foreach (var hp in EnemiesNearSegment(from, to, 1f)) ConcordHit(hp, "cc_gs_dash_a", 1.5f);
            if (Has("cc_gs_dash_b")) StartCoroutine(LeftArc(from, to));
            if (Has("cc_disc_dash_b"))
            {
                Vector2 dir = (to - from).sqrMagnitude > 0.01f ? (to - from).normalized : _pc.Facing;
                foreach (var hp in EnemiesInStrip(to, dir, 3f, 0.8f)) ConcordHit(hp, "cc_disc_dash_b", 1f);
                LedgerFx.Crest(to, dir, 3f, TexelFx.Steel);
            }
            if (Has("co_skid"))
            {
                Vector2 dir = (to - from).sqrMagnitude > 0.01f ? (to - from).normalized : _pc.Facing;
                float d = T2.SkidDistance * (AtMax("co_skid") ? 2f : 1f);
                _pc.Dash(dir, d / 0.2f, 0.2f);
            }
            if (Has("al_skid"))
            {
                foreach (var hp in EnemiesWithin(to, 1.5f))
                    hp.Take(new DamageInfo(T2.SkidCutHitUnits * _pc.HitUnit * _mods.Potency("al_skid"), ElementType.Air, _pc.gameObject) { Entry = "al_skid" });
                TexelFx.Ring(to, TexelFx.Steel, 0.4f, 1.5f, 0.25f);
            }
        }

        IEnumerator LeftArc(Vector2 from, Vector2 to)
        {
            LedgerFx.Crest(from, (to - from).normalized, (to - from).magnitude, TexelFx.Steel);
            yield return new WaitForSeconds(0.5f);
            if (_pc == null) yield break;
            foreach (var hp in EnemiesNearSegment(from, to, 1f)) ConcordHit(hp, "cc_gs_dash_b", 1.5f);
        }

        // ---------------------------------------------------------------- the dome

        /// <summary>The Barrier's dome turned a hit (PlayerController's BlocksHit). The shooter or the
        /// striker is the hit's source.</summary>
        public void OnBlocked(DamageInfo info)
        {
            if (_mods == null || _pc == null) return;
            var src = info.Source != null ? info.Source.GetComponent<Health>() : null;
            bool melee = src != null && Vector2.Distance(src.transform.position, _pc.transform.position) <= T2.MeleeBlowWithin;
            if (Has("gd_sap")) Resource?.Refund(T2.SapRefund * Strength("gd_sap"));
            if (src == null || src.IsDead) return;
            if (!melee && Has("gd_rebuke")) { src.Take(new DamageInfo(T2.RebukeHitUnits * _pc.HitUnit * Strength("gd_rebuke"), ElementType.Earth, _pc.gameObject) { Entry = "gd_rebuke" }); LedgerFx.Echo(src.transform.position, _pc.transform.position); }
            if (melee && Has("gd_plant")) Staggers(src, T2.PlantStagger * Strength("gd_plant"));
            if (Has("gd_coat_dome")) Coat(src, Strength("gd_coat_dome"));
            if (melee && Has("al_narrow")) Staggers(src, T2.NarrowEdgeStagger * _mods.Potency("al_narrow"));
            if (Has("cc_disc_barrier_a")) { ConcordHit(src, "cc_disc_barrier_a", 1f); LedgerFx.Echo(src.transform.position, _pc.transform.position); }
            if (!melee && Has("cc_bow_barrier_a")) { ConcordHit(src, "cc_bow_barrier_a", 1.2f); LedgerFx.Echo(src.transform.position, _pc.transform.position); }
            if (Has("cc_gs_barrier_a")) _woundBlade = true;
        }

        void OnBarrierEnded()
        {
            if (Has("gd_bulldoze"))
            {
                Vector2 me = _pc.transform.position, face = _pc.Facing.sqrMagnitude > 0.01f ? _pc.Facing.normalized : Vector2.down;
                foreach (var hp in EnemiesWithin(me, T2.BulldozeRadius))
                    if (Vector2.Dot(((Vector2)hp.transform.position - me).normalized, face) > 0.2f)
                        DragToward.Push(hp, me, T2.BulldozePush * Strength("gd_bulldoze") * PushMul);
                LedgerFx.Crest(me, face, T2.BulldozeRadius, LedgerFx.Ward);
            }
        }

        // ---------------------------------------------------------------- the bulwark, struck and ended

        void OverhaulStruckBy(DamageInfo info, Health attacker)
        {
            if (_pc == null || attacker == null) return;
            if (_pc.BulwarkActive)
            {
                if (Has("gd_backlash")) _backlashBank += info.Amount * T2.BacklashShare * Strength("gd_backlash");
                if (Has("gd_hold")) DragToward.Push(attacker, _pc.transform.position, T2.HoldPush * Strength("gd_hold") * PushMul);
                if (Has("cc_gs_bulwark_a")) ConcordHit(attacker, "cc_gs_bulwark_a", 1f);
                if (Has("cc_disc_bulwark_b")) { ConcordHit(attacker, "cc_disc_bulwark_b", 1f); LedgerFx.Echo(attacker.transform.position, _pc.transform.position); }
                if (Has("al_heavy_bulwark") && StillSeconds > 0.25f)
                    StatusEffects.Get(attacker.gameObject).ApplySlow(T2.HeavyStillSeconds, T2.HeavyStillSlow);
            }
            if (Has("al_sinking") && StillSeconds >= T2.SinkingAfter) Roots(attacker, T2.SinkingRoot * _mods.Potency("al_sinking"));
        }

        void OnBulwarkEnded()
        {
            Vector2 me = _pc.transform.position;
            if (_backlashBank > 0f && Has("gd_backlash"))
            {
                foreach (var hp in EnemiesWithin(me, T2.BacklashRadius)) hp.Take(new DamageInfo(_backlashBank, ElementType.Earth, _pc.gameObject) { Entry = "gd_backlash" });
                LedgerFx.ForceRound(_pc, TexelFx.Stone, T2.BacklashRadius);
            }
            _backlashBank = 0f;
            if (Has("cc_gs_bulwark_b"))
            {
                foreach (var hp in EnemiesWithin(me, 2.5f)) ConcordHit(hp, "cc_gs_bulwark_b", 2f);
                Visual(GroundCracks.Spawn(Arena.NearestFloor(me, 0.3f))?.gameObject);
            }
            if (Has("cc_bow_bulwark_b")) StartCoroutine(ClosingRain(me));
            _stakeStruck.Clear();
        }

        IEnumerator ClosingRain(Vector2 at)
        {
            for (int i = 0; i < 3; i++)
            {
                if (_pc == null) yield break;
                foreach (var hp in EnemiesWithin(at, 3f)) ConcordHit(hp, "cc_bow_bulwark_b", 0.6f);
                TexelFx.Ring(at, TexelFx.Steel, 2.6f, 3f, 0.2f, 1.2f, true);
                yield return new WaitForSeconds(0.3f);
            }
        }

        // ---------------------------------------------------------------- the parry

        /// <summary>A parry turned a blow (PlayerController.TryParry) - from any defensive ability.</summary>
        public void OnParried(Vector2 attackerAt)
        {
            if (_mods == null || _pc == null) return;
            _parriedInWindow = true;
            _parriesThisFloor++;
            var attacker = NearestEnemy(attackerAt, T2.ParryAttackerWithin, null);
            bool stance = Ability == DefensiveAbility.ParryStance;
            if (stance)
            {
                if (attacker != null && Has("gd_counterweight")) Staggers(attacker, T2.CounterweightStagger * Strength("gd_counterweight"));
                if (attacker != null && Has("gd_disarm")) HoldsOff(attacker, T2.DisarmSeconds * Strength("gd_disarm"));
                if (attacker != null && Has("gd_read")) _readMarks[attacker] = T2.ReadHits;
                if (Has("gd_echo_guard")) Hasten(T2.EchoGuardHaste * Mathf.Min(1f, Strength("gd_echo_guard")));
                if (Has("gd_coat_burst"))
                {
                    foreach (var hp in EnemiesWithin(_pc.transform.position, T2.CoatBurstRadius)) Coat(hp, Strength("gd_coat_burst"));
                    LedgerFx.Force(_pc.transform.position, CoatRamp, T2.CoatBurstRadius);
                }
                if (Has("al_brittle_guard"))
                    foreach (var hp in EnemiesWithin(_pc.transform.position, T2.BrittleGuardRadius))
                        Staggers(hp, T2.BrittleGuardStagger * _mods.Potency("al_brittle_guard"));
                // The Concords that answer a parry.
                if (attacker != null && Has("cc_gs_parry_a"))
                {
                    Vector2 me = _pc.transform.position, dir = ((Vector2)attacker.transform.position - me).normalized;
                    foreach (var hp in EnemiesInStrip(me, dir, 2.5f, 0.7f)) ConcordHit(hp, "cc_gs_parry_a", 1.5f);
                    LedgerFx.Crest(me, dir, 2.5f, TexelFx.Steel);
                }
                if (Has("cc_gs_parry_b") && _parriesThisFloor % 3 == 0)
                {
                    foreach (var hp in EnemiesWithin(_pc.transform.position, 2.5f)) ConcordHit(hp, "cc_gs_parry_b", 3f);
                    TexelFx.Ring(_pc.transform.position, TexelFx.Steel, 0.6f, 2.5f, 0.35f);
                }
                if (Has("cc_disc_parry_a")) _parryOrbitUntil = Time.time + T2.ConcordOrbitSeconds * ConcordPower("cc_disc_parry_a");
                if (attacker != null && Has("cc_disc_parry_b")) { ConcordHit(attacker, "cc_disc_parry_b", 1.5f); LedgerFx.Echo(attacker.transform.position, _pc.transform.position); }
                if (attacker != null && Has("cc_bow_parry_a")) { Roots(attacker, 1f * ConcordPower("cc_bow_parry_a")); Coat(attacker, ConcordPower("cc_bow_parry_a")); }
                if (Has("cc_bow_parry_b")) _freeArrow = true;
            }
        }

        // ---------------------------------------------------------------- the guard's tick

        void TickGuard(float dt)
        {
            if (_pc == null) return;
            bool dashing = _pc.Dashing, barrier = _pc.BarrierActive, bulwark = _pc.BulwarkActive;
            if (_wasDashing && !dashing) OnDashEnded();
            if (_wasBarrier && !barrier) OnBarrierEnded();
            if (_wasBulwark && !bulwark) OnBulwarkEnded();
            _wasDashing = dashing; _wasBarrier = barrier; _wasBulwark = bulwark;

            // A parry stance pressed at nothing (Off Balance; Feint, its Albedo).
            if (_parryWindowCheckUntil > 0f && Time.time > _parryWindowCheckUntil)
            {
                _parryWindowCheckUntil = -1f;
                if (!_parriedInWindow)
                {
                    if (Has("co_off_balance")) _pc.Stun(T2.OffBalanceStun * (AtMax("co_off_balance") ? 2f : 1f));
                    if (Has("al_off_balance"))
                    {
                        foreach (var hp in EnemiesWithin(_pc.transform.position, 2f)) DragToward.Push(hp, _pc.transform.position, T2.OffBalancePush);
                        LedgerFx.ForceRound(_pc, LedgerFx.Ward, 2f);
                    }
                }
            }

            Vector2 me = _pc.transform.position;
            _auraTick -= dt;
            if (bulwark && _auraTick <= 0f)
            {
                _auraTick = T2.AuraTick;
                if (Has("gd_mire_aura"))
                    foreach (var hp in EnemiesWithin(me, T2.AuraRadius)) StatusEffects.Get(hp.gameObject).ApplySlow(T2.AuraTick * 2f, T2.AuraSlow);
                if (Has("gd_taunt"))
                    foreach (var hp in EnemiesWithin(me, T2.TauntRadius)) DragToward.Pull(hp, me, T2.TauntPull * Strength("gd_taunt"));
                if (Has("gd_coat_aura"))
                    foreach (var hp in EnemiesWithin(me, T2.AuraRadius)) Coat(hp, Strength("gd_coat_aura") * 0.5f);
            }

            _concordTick -= dt;
            if (_concordTick <= 0f)
            {
                _concordTick = T2.ConcordTick;
                float tickUnits = 0.4f;
                bool orbit = (bulwark && Has("cc_disc_bulwark_a")) || Time.time < _parryOrbitUntil;
                if (orbit)
                {
                    string id = Time.time < _parryOrbitUntil ? "cc_disc_parry_a" : "cc_disc_bulwark_a";
                    foreach (var hp in EnemiesWithin(me, T2.ConcordOrbitRadius)) ConcordHit(hp, id, tickUnits);
                    TexelFx.Ring(me, TexelFx.Steel, T2.ConcordOrbitRadius - 0.1f, T2.ConcordOrbitRadius, T2.ConcordTick, 1f, true);
                }
                if (barrier && (Has("cc_gs_barrier_b") || Has("cc_disc_barrier_b")))
                {
                    Vector2 face = _pc.Facing.sqrMagnitude > 0.01f ? _pc.Facing.normalized : Vector2.down;
                    string id = Has("cc_gs_barrier_b") ? "cc_gs_barrier_b" : "cc_disc_barrier_b";
                    foreach (var hp in EnemiesWithin(me, 1.5f))
                        if (Vector2.Dot(((Vector2)hp.transform.position - me).normalized, face) > 0.3f) ConcordHit(hp, id, tickUnits);
                }
                if (bulwark && Has("cc_bow_bulwark_a"))
                    foreach (var hp in EnemiesWithin(me, 2f))
                        if (_stakeStruck.Add(hp)) ConcordHit(hp, "cc_bow_bulwark_a", 1f);
            }

            _embrasureTick -= dt;
            if (barrier && Has("cc_bow_barrier_b") && _embrasureTick <= 0f)
            {
                _embrasureTick = 0.5f;
                Vector2 face = _pc.Facing.sqrMagnitude > 0.01f ? _pc.Facing.normalized : Vector2.down;
                Health best = null; float bd = 64f;
                foreach (var hp in EnemiesWithin(me, 8f))
                {
                    Vector2 to = (Vector2)hp.transform.position - me;
                    if (Vector2.Dot(to.normalized, face) < 0.3f || to.sqrMagnitude >= bd) continue;
                    bd = to.sqrMagnitude; best = hp;
                }
                if (best != null) { ConcordHit(best, "cc_bow_barrier_b", 0.6f); LedgerFx.Echo(best.transform.position, me); }
            }
        }

        /// <summary>WOUND BLADE (a Concord): the basic after a blocked blow sweeps a full circle.</summary>
        void WoundBladeSwing()
        {
            if (!_woundBlade || _pc == null) return;
            _woundBlade = false;
            float reach = _pc.PendingRange;
            foreach (var hp in EnemiesWithin(_pc.transform.position, reach)) ConcordHit(hp, "cc_gs_barrier_a", 1f);
            TexelFx.Ring(_pc.transform.position, TexelFx.Steel, reach * 0.6f, reach, 0.25f);
        }
    }
}
