using UnityEngine;
using Convergence.Core;
using S = Convergence.Core.Tuning.Stats;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// DIMINISHING RETURNS, in one place, for the game and the balance model (Balance.Assay) alike -
    /// a rule written twice drifts the first time one copy is tuned.
    ///
    /// TWO LAYERS, each bent by its own curve and then multiplied: the CHARACTER (gear and the
    /// board, bent once when the player is built - GameBootstrap.BuildPlayer) and the RUN (the
    /// ledger and a spire's floor boon, bent inside RunModifiers.Recompute). A boon is then worth
    /// the same to a fresh character and a maxed one, and the ledger has its own ceiling - the
    /// VESSEL - that gear can neither erode nor inflate.
    ///
    /// THE CURVE: linear up to a knee, so ordinary play reads exactly the printed number, then a
    /// smooth tail toward a cap - at knee + (cap - knee) points it sits halfway between them.
    /// Negative points pass through untouched: stacking costs never gets cheaper.
    ///
    /// POOLS: crit chance, lifesteal and mitigation are summed from every source and capped as
    /// one - see <see cref="Crit"/>, <see cref="Lifesteal"/>, <see cref="Incoming"/>.
    ///
    /// <see cref="Enabled"/> false turns every rule back into the plain sum it replaced: the
    /// balance model's regression check runs that way to prove the plumbing changed nothing.
    /// </summary>
    public static class StatCurves
    {
        /// <summary>False: every curve and cap is the identity - the game as it was before them.</summary>
        public static bool Enabled = true;

        /// <summary>Linear to <paramref name="knee"/>, a hyperbolic tail toward <paramref name="cap"/>
        /// above it, its slope 1 at the knee so the bend has no corner.</summary>
        public static float Bend(float points, float knee, float cap)
        {
            if (!Enabled || points <= knee || cap <= knee) return points;
            float room = cap - knee, over = points - knee;
            return knee + room * over / (over + room);
        }

        // ------------------------------------------------------------------ the character

        /// <summary>The character layer's knee and cap for a stat. Stats without one - the
        /// mitigation family, which already falls off through StatPercents.ReductionFactor, and
        /// the small utility stats - return an infinite knee.</summary>
        public static (float Knee, float Cap) CharacterCurve(StatKind kind) => kind switch
        {
            StatKind.Damage                 => (S.DamageKnee, S.DamageCap),
            StatKind.AttackSpeed            => (S.AttackSpeedKnee, S.AttackSpeedCap),
            StatKind.MoveSpeed              => (S.MoveSpeedKnee, S.MoveSpeedCap),
            StatKind.CritDamage             => (S.CritDamageKnee, S.CritDamageCap),
            StatKind.FinisherPower          => (S.WeaponArtKnee, S.WeaponArtCap),
            StatKind.Range                  => (S.RangeKnee, S.RangeCap),
            StatKind.AoeRadius              => (S.AreaKnee, S.AreaCap),
            StatKind.ElementGrowth          => (S.ElementGrowthKnee, S.ElementGrowthCap),
            StatKind.ElementalEffectiveness => (S.ElementalPowerKnee, S.ElementalPowerCap),
            StatKind.MaxHp                  => (S.MaxHpKnee, S.MaxHpCap),
            _                               => (float.PositiveInfinity, float.PositiveInfinity),
        };

        public static float Character(StatKind kind, float points)
        {
            var (knee, cap) = CharacterCurve(kind);
            return Bend(points, knee, cap);
        }

        /// <summary>A whole character stat block, every field through its own curve.</summary>
        public static StatPercents Character(StatPercents raw)
        {
            var s = new StatPercents();
            s.Add(raw);
            s.Damage = Character(StatKind.Damage, raw.Damage);
            s.AttackSpeed = Character(StatKind.AttackSpeed, raw.AttackSpeed);
            s.MoveSpeed = Character(StatKind.MoveSpeed, raw.MoveSpeed);
            s.CritDamage = Character(StatKind.CritDamage, raw.CritDamage);
            s.FinisherPower = Character(StatKind.FinisherPower, raw.FinisherPower);
            s.Range = Character(StatKind.Range, raw.Range);
            s.AoeRadius = Character(StatKind.AoeRadius, raw.AoeRadius);
            s.ElementGrowth = Character(StatKind.ElementGrowth, raw.ElementGrowth);
            s.ElementalEffectiveness = Character(StatKind.ElementalEffectiveness, raw.ElementalEffectiveness);
            s.MaxHp = Character(StatKind.MaxHp, raw.MaxHp);
            return s;
        }

        // ------------------------------------------------------------------ the run: the Vessel

        /// <summary>The run layer's knee and cap at FULL mastery.</summary>
        public static (float Knee, float Cap) RunCurve(StatKind kind) => kind switch
        {
            StatKind.Damage                 => (S.RunDamageKnee, S.RunDamageCap),
            StatKind.AttackSpeed            => (S.RunAttackSpeedKnee, S.RunAttackSpeedCap),
            StatKind.MoveSpeed              => (S.RunMoveSpeedKnee, S.RunMoveSpeedCap),
            StatKind.FinisherPower          => (S.RunWeaponArtKnee, S.RunWeaponArtCap),
            StatKind.CritDamage             => (S.RunCritDamageKnee, S.RunCritDamageCap),
            StatKind.ElementalEffectiveness => (S.RunElementalPowerKnee, S.RunElementalPowerCap),
            StatKind.ElementGrowth          => (S.RunElementGrowthKnee, S.RunElementGrowthCap),
            StatKind.AoeRadius              => (S.RunAreaKnee, S.RunAreaCap),
            StatKind.Range                  => (S.RunRangeKnee, S.RunRangeCap),
            StatKind.MaxHp                  => (S.RunMaxHpKnee, S.RunMaxHpCap),
            _                               => (float.PositiveInfinity, float.PositiveInfinity),
        };

        /// <summary>How big the Vessel is at a mastery level, as a share of its full size.</summary>
        public static float Vessel(int masteryLevel)
            => Mathf.Lerp(S.VesselAtLevelZero, 1f, Mathf.Clamp01(masteryLevel / (float)Tuning.Mastery.LevelCap));

        /// <summary>A run-layer stat through the Vessel - its thresholds scaled by mastery level.</summary>
        public static float Run(StatKind kind, float points, int masteryLevel)
        {
            var (knee, cap) = RunCurve(kind);
            if (float.IsPositiveInfinity(knee)) return points;
            float size = Vessel(masteryLevel);
            return Bend(points, knee * size, cap * size);
        }

        // ------------------------------------------------------------------ the pools

        /// <summary>
        /// Crit as one pool: every source's chance summed, held to one cap for every element, and
        /// what lies past it turned into crit damage - <see cref="Tuning.Stats.CritOverflowToDamage"/>
        /// on the multiplier per whole 1.0 of chance over.
        /// </summary>
        public static (float Chance, float Multiplier) Crit(float chance, float multiplier)
        {
            if (!Enabled) return (Mathf.Clamp01(chance), multiplier);
            float over = Mathf.Max(0f, chance - S.CritChanceCap);
            return (Mathf.Clamp(chance, 0f, S.CritChanceCap), multiplier + over * S.CritOverflowToDamage);
        }

        /// <summary>Lifesteal from every source as one pool, held under its cap.</summary>
        public static float Lifesteal(float fraction) => Enabled ? Mathf.Min(fraction, S.LifestealCap) : fraction;

        /// <summary>The most lifesteal may heal per second, given max health. Infinite when off.</summary>
        public static float LifestealPerSecond(float maxHp) => Enabled ? S.LifestealHealPerSecond * maxHp : float.PositiveInfinity;

        /// <summary>The share of a hit that gets through stat-based mitigation, never below the floor.</summary>
        public static float Incoming(float multiplier) => Enabled ? Mathf.Max(S.IncomingFloor, multiplier) : multiplier;

        /// <summary><see cref="Incoming(float)"/> against a floor the run's ledger has moved
        /// (Exposed raises it); below zero keeps the usual one.</summary>
        public static float Incoming(float multiplier, float floor)
            => !Enabled ? multiplier : Mathf.Max(floor < 0f ? S.IncomingFloor : floor, multiplier);
    }
}
