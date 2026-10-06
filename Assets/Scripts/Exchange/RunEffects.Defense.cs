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
        float _wardTimer;
        float _reactiveRemaining, _reactiveCooldown;
        float _ghostRemaining, _ghostCooldown;
        int _vapourCount;
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

            var hp = Hp;

            // Aegis: the ward renews on its clock - twice as fast standing still under Athanor.
            if (Has("aegis_cycle") && !_wardReady)
            {
                _wardTimer -= dt * (Has("athanor") && !moving ? 2f : 1f);
                if (_wardTimer <= 0f)
                {
                    _wardReady = true;
                    if (_pc != null) Spr.Pulse(_pc.transform, 1.1f, new Color(0.7f, 0.85f, 1f, 0.7f), 0.35f, true, 1.3f);
                }
            }

            if (hp == null || hp.IsDead) return;

            // Vital Spark: a slow regeneration, inside the same healing limit lifesteal spends.
            if (Has("vital_spark")) _pc.HealWithinLimit(hp.Max * T.VitalSparkRegen * dt);

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
        /// Incoming damage after mitigation and before it is subtracted - OUTSIDE the floor, so
        /// these bite every build alike. The ward and the untouchable windows come first (nothing
        /// lands), then the multipliers, then whatever a hit taken sets going.
        /// </summary>
        public float ModifyIncoming(float amount)
        {
            if (_mods == null || amount <= 0f) return amount;

            if (_wardReady && Has("aegis_cycle"))
            {
                _wardReady = false;
                _wardTimer = AtMax("aegis_cycle") ? T.AegisRenewSecondsII : T.AegisRenewSeconds;
                if (_pc != null)
                {
                    Spr.Flash(_pc.transform.position, 1.2f, new Color(0.7f, 0.85f, 1f), 0.3f);
                    if (AtMax("aegis_cycle")) Push(_pc.transform.position, T.TinWardRadius, T.TinWardKnockback);
                }
                return 0f;
            }
            if (_ghostRemaining > 0f) return 0f;
            if (AtMax("evanescence") && _pc != null && _pc.IsMoving && ++_vapourCount % T.VapourEvery == 0)
            {
                Spr.Flash(_pc.transform.position, 0.8f, new Color(0.85f, 0.95f, 1f, 0.6f), 0.2f);
                return 0f;
            }

            float f = 1f;
            if (AtMax("stonestance") && _stillSeconds >= T.LapisStillSeconds)
            {
                f *= T.LapisMul;
                _stillSeconds = 0f;   // the next one wants another second planted
            }
            if (_reactiveRemaining > 0f)
                f *= 1f - (AtMax("reactive_plate") ? T.ReactivePlateReductionII : T.ReactivePlateReduction);
            if (Has("open_stance") && _openStanceHits < T.OpenStanceHits)
            {
                _openStanceHits++;
                f *= 2f;
            }

            var hp = Hp;
            float mine = hp != null && hp.Max > 0f ? hp.Current / hp.Max : 1f;
            if (AtMax("thickened_hide") && mine < T.FortitudeBelow) f *= T.FortitudeMul;
            if (Has("glass_bones") && mine < T.GlassBonesBelow) f *= 1f + T.GlassBonesTaken;
            if (_pc != null && _pc.AttackLocked)
            {
                if (AtMax("overcommitted")) f *= 1f + T.OverextendedTaken;
                if (Has("committed")) f *= 1f - T.CommittedTaken;
            }
            int restless = N("restless");
            if (restless > 0 && _stillSeconds >= T.RestlessStillSeconds) f *= 1f + T.RestlessTaken * restless;
            if (Has("retrograde_motion") && Has("retrograde") && InputsInverted) f *= T.RetrogradeMotionTakenMul;

            amount *= f;
            OnHitTaken(amount);
            return amount;
        }

        /// <summary>The windows and spills a hit taken sets going.</summary>
        void OnHitTaken(float amount)
        {
            if (Has("reactive_plate") && _reactiveCooldown <= 0f)
            {
                _reactiveRemaining = T.ReactivePlateSeconds;
                _reactiveCooldown = T.ReactivePlateCooldown;
                if (AtMax("reactive_plate")) _pc?.ShortenDefenseCooldown(T.TemperedCooldownCut);   // Tempered
            }
            if (Has("ghostwalk") && _ghostCooldown <= 0f)
            {
                _ghostRemaining = T.GhostwalkSeconds;
                _ghostCooldown = T.GhostwalkCooldown;
            }
            if (AtMax("retrograde")) _contraryRemaining = T.ContrarySeconds;   // Contrary
            if (AtMax("leaky_vessel")) Resource?.Spill(T.CrackedVesselSpill);   // Cracked Vessel
            if (Has("sealed_vessel")) Resource?.Refund(T.SealedVesselGain);
            if (AtMax("becalmed")) (Resource as Player.AirResource)?.BreakStreak();   // Doldrums
            if (Has("haemophilia") && amount > 0f)
            {
                _bleedOwed += amount * T.HaemophiliaFraction;
                _bleedRate = Mathf.Max(_bleedRate * 0.5f, _bleedOwed / T.HaemophiliaSeconds);
            }
        }

        // ---------------------------------------------------------------- heals

        public float ModifyHeal(float amount)
        {
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
            return amount;
        }

        /// <summary>True to survive a lethal hit at 1 health - Second Wind, once a floor.</summary>
        public bool SurviveLethal()
        {
            if (!Has("second_wind") || _secondWindSpent || Hp == null) return false;
            _secondWindSpent = true;
            if (Has("phoenix"))
            {
                // Phoenix: risen, and the blast throws the crowd off you.
                _riseTo = T.PhoenixRise;
                Push(_pc.transform.position, T.PhoenixRadius, T.TinWardKnockback);
                Spr.Flash(_pc.transform.position, T.PhoenixRadius, new Color(1f, 0.55f, 0.2f), 0.5f);
            }
            else _secondWindHealLeft = Hp.Max * T.SecondWindHeal;
            Spr.Flash(_pc.transform.position, 1.6f, new Color(1f, 0.95f, 0.6f), 0.6f);
            return true;
        }

        // ---------------------------------------------------------------- shared

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
