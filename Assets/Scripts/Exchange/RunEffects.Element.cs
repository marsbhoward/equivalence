using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Hazards;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.Exchange
{
    public partial class RunEffects
    {
        // ---------------------------------------------------------------- state

        float _barrenRemaining;
        float _fillSeconds;
        float _trapRemaining;

        /// <summary>Releases owed a repeat: when, and at what strength. Two at most (Twin Spark
        /// and Wellspring on the same release).</summary>
        float _repeatInA = -1f, _repeatScaleA, _repeatInB = -1f, _repeatScaleB;

        /// <summary>The strength a repeat is firing at, while it fires - read by ReleaseScale.</summary>
        float _repeatNow = 1f;

        /// <summary>1 for an ordinary release; a repeat's own share while it fires.</summary>
        public float RepeatStrength => _repeatNow;

        void TickElement(float dt, float meter01)
        {
            Countdown(ref _barrenRemaining, dt);
            if (meter01 < 0.999f) _fillSeconds += dt;

            // The trap boons' stack II: the bonus is held while touching the element's own
            // ground, and for its seconds after.
            if (TrapBonusLive && TouchingOwnGround()) _trapRemaining = T.TrapBonusSeconds;
            else Countdown(ref _trapRemaining, dt);

            TickRepeat(ref _repeatInA, ref _repeatScaleA, dt);
            TickRepeat(ref _repeatInB, ref _repeatScaleB, dt);
        }

        void TickRepeat(ref float until, ref float scale, float dt)
        {
            if (until < 0f) return;
            until -= dt;
            if (until > 0f) return;
            until = -1f;
            var r = Resource;
            if (r == null || _pc == null || _pc.Statue) return;
            _repeatNow = scale;
            try { r.ReleaseAgain(); }
            finally { _repeatNow = 1f; }
        }

        void ScheduleRepeat(float seconds, float scale)
        {
            if (_repeatInA < 0f) { _repeatInA = seconds; _repeatScaleA = scale; }
            else if (_repeatInB < 0f) { _repeatInB = seconds; _repeatScaleB = scale; }
        }

        // ---------------------------------------------------------------- the meter

        /// <summary>The run's side of how fast the meter builds: the ledger's Element Growth,
        /// nothing at all through Barren's seconds.</summary>
        public float GainMul => _barrenRemaining > 0f ? 0f : M.GainMul;

        /// <summary>How fast the meter fades or decays (Leaky Vessel; Sealed Vessel stops it).</summary>
        public float DecayMul => M.DecayMul;

        // ---------------------------------------------------------------- releases

        /// <summary>
        /// Everything a release does, from the run: the ledger's Elemental Power, Slow Fire's
        /// patience, and a repeat's own strength while it fires.
        /// </summary>
        public float ReleaseScale
        {
            get
            {
                var m = M;
                float scale = m.ReleaseMul * _repeatNow;
                if (Has("slow_fire") && Has("stubborn_ore"))
                    scale *= m.Factor(StatKind.ElementalEffectiveness,
                                      Mathf.Min(T.SlowFireCap, T.SlowFirePowerPerSecond * _fillSeconds));
                return scale;
            }
        }

        /// <summary>Grand Elixir: release hits can crit.</summary>
        public bool ReleasesCrit => AtMax("elixir");

        /// <summary>Cataclysm: release hits stagger.</summary>
        public bool ReleasesStagger => Has("cataclysm");

        /// <summary>Transfusion: lifesteal drinks from releases, burns and bleeds.</summary>
        public bool LifestealFromAll => AtMax("bloodletters_pact");

        /// <summary>A release has fired (not a repeat): refunds, prices, fields, repeats.</summary>
        public void OnReleased(Player.ElementalResource r)
        {
            if (_mods == null || r == null || _pc == null) return;
            var hp = Hp;

            if (Has("overflow")) r.Refund(T.OverflowRefund);

            int back = N("backfire");
            if (back > 0 && hp != null)
            {
                Pay(hp.Max * T.BackfireFraction * back);
                if (AtMax("backfire")) _pc.Stun(T.RecoilSeconds);   // Recoil
            }
            if (Has("rebound") && hp != null) hp.Heal(hp.Max * T.ReboundHeal);

            if (Has("residue"))
                ResidueField.Spawn(_pc.transform.position, T.ResidueRadius * _pc.AreaScaleNow, r.Element,
                                   T.ResidueSeconds, _pc);

            if (Has("twin_spark")) ScheduleRepeat(T.TwinSparkDelay, T.TwinSparkScale);
            if (Has("wellspring") && !_wellspringSpent)
            {
                _wellspringSpent = true;
                ScheduleRepeat(T.TwinSparkDelay * 0.6f, 1f);
            }
            if (AtMax("stubborn_ore")) _barrenRemaining = T.BarrenSeconds;   // Barren
            _fillSeconds = 0f;

            // The elements' own capstones.
            if (r is Player.FireResource fire && AtMax("banked_embers")) fire.HoldAtLeast(T.HearthStacks);   // Hearth
            if (r is Player.WaterResource water && AtMax("high_tide") && water.LastTier >= 2)                 // Spring Tide
                water.SoakAround();
            if (r is Player.AirResource air && AtMax("tailwind") && !air.UseSecondAbility)                     // Updraft
                Push(_pc.transform.position, T.UpdraftRadius, T.UpdraftKnockback);
        }

        // ---------------------------------------------------------------- the element entries

        /// <summary>Fire: heat stacks never fade below this (Banked Embers).</summary>
        public int HeatFloor => Element == ElementType.Fire ? N("banked_embers") : 0;

        /// <summary>Fire: whether Fuel still works (Wet Ash stops it).</summary>
        public bool FuelWorks => !AtMax("smother");

        /// <summary>Fire: Damage points one heat stack gives, from its own base - Smother takes,
        /// Phlogiston doubles.</summary>
        public float HeatPoints(float basePoints)
        {
            float p = basePoints - T.SmotherPoints * N("smother");
            if (Has("phlogiston")) p *= T.PhlogistonMul;
            return Mathf.Max(0f, p);
        }

        /// <summary>Water: seconds a surge gains (High Tide).</summary>
        public float SurgeExtraSeconds => T.HighTideSeconds * N("high_tide");

        /// <summary>Water: Attack Speed points a surge loses (Low Water).</summary>
        public float SurgePointsLost => T.LowWaterSurge * N("low_water");

        /// <summary>Water: whether soaked enemies still fill the meter double (Ebb stops it).</summary>
        public bool SoakedDoubleWorks => !AtMax("low_water");

        /// <summary>Water: vulnerability a soak adds over its own (Flood).</summary>
        public float SoakVulnerabilityAdd => Has("flood") ? T.FloodVulnerability : 0f;

        /// <summary>Earth: seconds the charge stops bleeding after a move (Deep Roots).</summary>
        public float ChargeHoldSeconds => T.DeepRootsHold * N("deep_roots");

        /// <summary>Earth: seconds the planted reduction holds after a move (Bedrock).</summary>
        public float PlantedHoldSeconds => AtMax("deep_roots") ? T.BedrockHold : 0f;

        /// <summary>Air: how fast momentum fades when stopped (Tailwind), and how long it holds
        /// first at II.</summary>
        public float MomentumFadeMul => Has("tailwind") ? T.TailwindFadeMul : 1f;
        public float MomentumHoldSeconds => AtMax("tailwind") ? T.TailwindHoldII : 0f;

        /// <summary>Air: the crit floor a hit adds, from its own base - Becalmed takes, Gale doubles.</summary>
        public float CritPerHit(float basePerHit)
        {
            float p = basePerHit - T.BecalmedCritPerHit * N("becalmed");
            if (Has("gale")) p *= T.GaleMul;
            return Mathf.Max(0f, p);
        }

        // ---------------------------------------------------------------- the trap boons

        string TrapId => Element switch
        {
            ElementType.Fire => "salamander",
            ElementType.Earth => "gnome",
            ElementType.Water => "undine",
            _ => "sylph",
        };

        bool TrapBonusLive => _mods != null && AtMax(TrapId);

        /// <summary>The element's own trap does nothing to its holder (stack I).</summary>
        public bool ImmuneTo(PitKind kind) => kind switch
        {
            PitKind.Fire => Has("salamander"),
            PitKind.Sand => Has("gnome"),
            PitKind.Water => Has("undine"),
            _ => false,
        };

        public bool ImmuneToTornadoes => Has("sylph");

        bool TouchingOwnGround()
        {
            if (_pc == null) return false;
            Vector2 p = _pc.transform.position;
            if (Element == ElementType.Air) return Tornado.Touching(p);
            var pit = FloorPits.At(p);
            if (pit == null) return false;
            return Element switch
            {
                ElementType.Fire => pit.Kind == PitKind.Fire && pit.HurtsNow,
                ElementType.Earth => pit.Kind == PitKind.Sand,
                ElementType.Water => pit.Kind == PitKind.Water,
                _ => false,
            };
        }

        /// <summary>Seconds of the trap bonus left - for the HUD.</summary>
        public float TrapBonusRemaining => _trapRemaining;
    }
}
