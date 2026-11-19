using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// THE PARRY COUNTER (the user's calls, 2026-11-18, on the Parry Lab mock
    /// https://claude.ai/artifact/PXXThPf2xHsZ4sYRfvREUu). Every defensive ability parries in its opening
    /// window (TryParry); what answers it is the WEAPON's own move, after the parry's freeze:
    ///
    ///   GREATSWORD  a level Sweep that strikes EVERY enemy within the sword's reach, all the way round.
    ///   DISCS       a disc thrown at the attacker, ricocheting by the regular throw's rule (BaseRicochets +
    ///               bonuses, the bounce range, the falloff), then home - and no attack until it is caught.
    ///   BOW         a CANTED shot (the Canted connector's flat sidearm draw) at the attacker, full damage:
    ///               moving during the draw costs it nothing.
    ///   DASH        never cut short: an AFTERIMAGE stays at the parry point and strikes the attacker while
    ///               the dash carries on (DashTrail.Strike).
    ///
    /// Each counter is a basic's full damage (a step at DamageMultiplier 1) resolved through the weapon's
    /// ordinary path - so crits, elements, the board and the exchange all apply - and never advances the chain.
    /// Riposte's own guard answers with its art instead (TryParry), so a Riposte never counters twice.
    /// </summary>
    public partial class PlayerController
    {
        /// <summary>The PARRY GUARD each weapon raises for the window (the mock's guards): the greatsword
        /// level before the chest (the Sweep's first frame, so the counter goes straight on from it); the
        /// discs together before the chest; the bow raised square before the body (the rig's bow hold), the
        /// counter's canted draw then turning it flat. One frame of a motion each, held by the rig (RaiseGuard) - the counter's swing takes
        /// over from it, and a window that took nothing lets it drop.</summary>
        void RaiseParryGuard()
        {
            if (Rig == null || _parryWindowRemaining <= 0f) return;
            float seconds = _parryWindowRemaining + Tuning.Defense.GuardLingerSeconds;
            switch (Weapon)
            {
                case Art.Gear.WeaponClass.Disc: Rig.RaiseGuard(AttackMotion.DiscCrossCut, 0f, false, seconds); break;
                case Art.Gear.WeaponClass.Bow:  Rig.RaiseGuard(AttackMotion.Draw, 0f, false, seconds); break;
                default:                        Rig.RaiseGuard(AttackMotion.Sweep, 0f, false, seconds); break;   // the counter's own first frame
            }
        }

        /// <summary>The enemy the bow's counter shot is loosed at (ReleaseArrow reads it for a ParryCounter step).</summary>
        Health _counterTarget;

        /// <summary>The enemy that was parried: the one that attacked (an enemy, or a shot's shooter) while it
        /// lives, else the living enemy nearest the blow.</summary>
        Health CounterTargetFor(Vector2 at, GameObject attacker)
        {
            if (attacker != null)
            {
                var own = attacker.GetComponent<Health>();
                if (own != null && !own.IsDead) return own;
            }
            Enemies.EnemyRegistry.Prune();
            Health best = null;
            float bestD = Tuning.Defense.CounterFindRadius;
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                float d = Vector2.Distance(at, e.transform.position);
                if (d < bestD) { bestD = d; best = hp; }
            }
            return best;
        }

        void FireCounterStrike(Vector2 attackerPosition, GameObject attacker)
        {
            var target = CounterTargetFor(attackerPosition, attacker);
            if (EquippedDefensiveAbility == Art.Gear.DefensiveAbility.Dash) { AfterimageStrike(target, attackerPosition); return; }
            StartCoroutine(Counter(target, attackerPosition));
        }

        /// <summary>A basic at full damage, marked so the weapon's path knows it for a counter.</summary>
        static AttackStep CounterStep(AttackMotion motion, bool area = false) => new AttackStep
        {
            Name = "Counter", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = motion,
            AreaOfEffect = area, ParryCounter = true,
            // Every body in reach: one mid-strike clashes and takes nothing, but the sweep carries on.
            CleavesAll = true,
        };

        IEnumerator Counter(Health target, Vector2 attackerPosition)
        {
            // After the parry's freeze: a scaled wait does not pass while the hitstop holds time at zero.
            yield return new WaitForSeconds(0.01f);
            if (Health == null || Health.IsDead) yield break;
            Vector2 at = target != null ? (Vector2)target.transform.position : attackerPosition;
            var to = at - (Vector2)transform.position;
            if (to.sqrMagnitude > 1e-4f) Facing = to.normalized;
            _basicSwing = true;
            _artUnstruck = false;
            Rig?.SetArtElement(null, Weapon);

            switch (Weapon)
            {
                case Art.Gear.WeaponClass.Disc:
                    yield return CounterThrow(target);
                    break;
                case Art.Gear.WeaponClass.Bow:
                {
                    var step = CounterStep(AttackMotion.CantedDraw);
                    float window = IntervalFor(step);
                    _cooldown = Mathf.Max(_cooldown, AttackMotions.CooldownFor(window));
                    _counterTarget = target;
                    FireArrow(step, false, window);
                    break;
                }
                default:
                    yield return CounterSweep();
                    break;
            }
        }

        /// <summary>The greatsword's counter: a level Sweep, struck at its drawn moment on every enemy
        /// within the sword's reach, all the way round (an area step's circle).</summary>
        IEnumerator CounterSweep()
        {
            var step = CounterStep(AttackMotion.Sweep, area: true);
            float window = IntervalFor(step);
            _cooldown = Mathf.Max(_cooldown, AttackMotions.CooldownFor(window));
            _visual?.PlayAttack(step.Motion, window);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, window, false);
            Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(window));
            float strike = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, window) : AttackMotions.SwingSeconds(window) * 0.5f;
            if (strike > 0f) yield return new WaitForSeconds(strike);
            if (Health == null || Health.IsDead) yield break;
            ResolveArc(step, false, advanceCombo: false);
        }

        /// <summary>The discs' counter: one disc thrown at the attacker on the regular throw (its ricochets,
        /// bounce range and falloff), and no attack until it is back in the hand.</summary>
        IEnumerator CounterThrow(Health target)
        {
            var step = CounterStep(AttackMotion.Throw);
            float window = IntervalFor(step);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.ThrowMotion, window, false);
            float release = Rig != null ? Rig.StrikeDrawnSeconds(step.ThrowMotion, window) : 0.1f;
            _cooldown = Mathf.Max(_cooldown, release + 0.05f);
            if (release > 0f) yield return new WaitForSeconds(release);
            if (Health == null || Health.IsDead) yield break;
            if (target != null && !target.IsDead)
            {
                var to = (Vector2)target.transform.position - (Vector2)transform.position;
                if (to.sqrMagnitude > 1e-4f) Facing = to.normalized;
            }
            var disc = BasicDiscThrow();
            disc?.WinClashes();   // the parried enemy is likely mid-strike: the counter wins
            // Held until the catch (the user's call): the counter's disc out means no swing, though you move.
            float waited = 0f;
            while (disc != null && waited < Tuning.Defense.CounterCatchLimit)
            {
                _cooldown = Mathf.Max(_cooldown, 0.05f);
                waited += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>The dash's counter: the dash is never cut short - an afterimage stays at the parry point
        /// and strikes the attacker, if it stands within the weapon's reach of it.</summary>
        void AfterimageStrike(Health target, Vector2 attackerPosition)
        {
            Trail?.Strike();
            Vector2 at = target != null ? (Vector2)target.transform.position : attackerPosition;
            var dir = at - (Vector2)transform.position;
            if (dir.sqrMagnitude > 1e-4f) dir.Normalize(); else dir = Facing;
            TexelFx.Cut(at, TexelFx.Gold, dir, 0.55f);
            if (target == null || target.IsDead) return;
            _arcOnly = new HashSet<Health> { target };
            try { ResolveArc(CounterStep(AttackMotion.Thrust, area: true), false, advanceCombo: false); }
            finally { _arcOnly = null; }
        }
    }
}
