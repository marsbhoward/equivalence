using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;
using Convergence.Progression;
using B = Convergence.Core.Tuning.Board;

namespace Convergence.Balance
{
    /// <summary>
    /// The shape of one weapon chain as ChainDps measured it - what the board's rules are priced
    /// against. Hits include the crit factor.
    /// </summary>
    public struct ChainShape
    {
        public float Seconds;          // one chain, wind-up included
        public int Basics;
        public float BasicHit, ArtHit;
        public float CritChance, CritMul, CritFactor;
        public float HitsPerSecond;

        /// <summary>The average hit of the chain on a CRIT - what a crit-triggered status is a share of.</summary>
        public float CritHit => (Basics * BasicHit + ArtHit) / Mathf.Max(1, Basics + 1) / Mathf.Max(1e-4f, CritFactor) * CritMul;
    }

    /// <summary>
    /// The mastery board's rules (Progression.BoardEffects), priced. Everything the single-target
    /// model can see has a term here; what it cannot - a crowd (Aqua Fortis, Cineration,
    /// Precipitation, Borax, Fermentation), a press timed PERFECT (Cinnabar; the model never prices
    /// perfect play), a parry or a dodge (Sal Ammoniac, Volatilization) - is left out and says so
    /// in GearWorth's note and the phase report. Every number is Tuning.Board's or a stated Assume.
    /// </summary>
    public static partial class Assay
    {
        static bool Owns(Build b, Notable n) => b != null && !b.Legacy && b.Notables.Contains(n);

        static float Power(float points) => 1f + points / 100f;

        static float StillShare(Build b)
            => b?.Element == ElementType.Earth ? Assume.EarthStillShare : 1f - Assume.MovingShare;

        /// <summary>Damage and attack speed points the board's conditional rules add on average,
        /// joined to the raw points before the character curve, as BoardEffects does in play.</summary>
        static (float Damage, float AttackSpeed) BoardPoints(Build b, float releasesPerSecond)
        {
            float damage = 0f, speed = 0f;
            if (Owns(b, Notable.Tartar)) damage += B.TartarDamagePoints * StillShare(b);
            if (Owns(b, Notable.Exaltation))
                damage += B.ExaltationDamagePoints * Mathf.Min(1f, B.ExaltationSeconds * releasesPerSecond);
            if (Owns(b, Notable.Circulation)) speed += B.CirculationMaxPoints * Assume.FlowUptime;
            return (damage, speed);
        }

        /// <summary>Phlegm's soak on the body being fought: the vulnerability it holds on average.
        /// Max'd with the element's own soak (Water), as two soaks on one body are.</summary>
        static float BoardVulnerability(Build b, ChainShape c)
        {
            if (!Owns(b, Notable.Phlegm)) return 1f;
            float uptime = Mathf.Min(1f, B.PhlegmSeconds / Mathf.Max(0.1f, c.Seconds));
            return 1f + (B.PhlegmVulnerability - 1f) * Power(b.SoakPoints) * uptime;
        }

        /// <summary>The share of a fight the body being fought spends slowed by the build's own rules.</summary>
        static float SlowedUptime(Build b, ChainShape c)
        {
            float up = 0f;
            if (Owns(b, Notable.AquaRegia)) up = Mathf.Max(up, Assume.EdgeSlowUptime);
            if (Owns(b, Notable.Phlegm)) up = Mathf.Max(up, Mathf.Min(1f, B.PhlegmSeconds / Mathf.Max(0.1f, c.Seconds)));
            if (Owns(b, Notable.Melancholy))
                up = Mathf.Max(up, Mathf.Min(1f, B.MelancholySeconds * Power(b.StaggerPoints) / Mathf.Max(0.1f, c.Seconds)));
            return up;
        }

        /// <summary>Multipliers on the build's damage from rules that ask about the target.</summary>
        static float BoardOffense(Build b, ChainShape c)
        {
            if (b == null || b.Legacy || b.Notables.Count == 0) return 1f;
            float f = 1f;
            if (Owns(b, Notable.Vitriol)) f *= 1f + B.VitriolMax * Assume.SameTargetShare;

            if (Owns(b, Notable.Saltpetre) && Owns(b, Notable.Choler))
                f *= 1f + B.SaltpetreBonus * Mathf.Min(1f, B.CholerSeconds / Mathf.Max(0.1f, c.Seconds));

            if (Owns(b, Notable.Mortification) && Owns(b, Notable.Sanguine))
            {
                float critsPerSecond = c.CritChance * c.HitsPerSecond;
                float bleeding = Mathf.Min(1f, critsPerSecond * B.SanguineSeconds);
                float critShare = c.CritChance * c.CritMul / Mathf.Max(1e-4f, c.CritFactor);
                f *= 1f + B.MortificationBonus * critShare * bleeding;
            }

            if (Owns(b, Notable.Inceration)) f *= 1f + B.IncerationBonus * SlowedUptime(b, c);

            if (Owns(b, Notable.Congelation) && Owns(b, Notable.Melancholy))
            {
                // Three staggers - one a weapon art - and the per-body cooldown between congealings.
                float every = Mathf.Max(3f * c.Seconds, B.CongelationCooldown + B.CongelationSeconds);
                f *= 1f + (B.CongelationVulnerability - 1f) * B.CongelationSeconds / every;
            }
            return f;
        }

        /// <summary>Damage per second the build's status keystones add as burns and bleeds.</summary>
        static float BoardDamageOverTime(Build b, ChainShape c)
        {
            if (b == null || b.Legacy) return 0f;
            float dot = 0f;
            if (Owns(b, Notable.Choler))
            {
                // A burn merges by max: the last weapon art's share, held while arts keep coming.
                float uptime = Mathf.Min(1f, B.CholerSeconds / Mathf.Max(0.1f, c.Seconds));
                dot += B.CholerBurnFraction * Power(b.BurnPoints) * c.ArtHit * uptime;
            }
            if (Owns(b, Notable.Sanguine))
            {
                float critsPerSecond = c.CritChance * c.HitsPerSecond;
                float uptime = Mathf.Min(1f, critsPerSecond * B.SanguineSeconds);
                float stacks = Owns(b, Notable.Orpiment)
                    ? Mathf.Clamp(critsPerSecond * B.SanguineSeconds, 1f, B.OrpimentStacks) : 1f;
                dot += B.SanguineBleedFraction * Power(b.BleedPoints) * c.CritHit * stacks * uptime;
            }
            return dot;
        }

        /// <summary>A multiplier on incoming damage from the board's conditional defences - landing
        /// after the mitigation floor, as BoardEffects.ModifyIncoming and Health.ScaleIncoming do.</summary>
        static float BoardDefense(Build b)
        {
            if (b == null || b.Legacy || b.Notables.Count == 0) return 1f;
            float f = 1f;
            if (Owns(b, Notable.Alum)) f *= 1f - (1f - B.AlumDamageMul) * Assume.FullHealthShare;
            if (Owns(b, Notable.Antimony) && Owns(b, Notable.Melancholy))
                f *= 1f - Assume.FromTargetShare * (1f - 1f / (1f + B.AntimonyAttackSlow));
            if (Owns(b, Notable.Solution) && Owns(b, Notable.Phlegm))
                f *= 1f - Assume.FromTargetShare * (1f - B.SolutionDamageMul);
            return f;
        }

        /// <summary>Health per second the board adds beyond lifesteal: Aqua Vitae's kills.</summary>
        static float BoardHealing(Build b, float dps, float maxHp, int floor)
        {
            if (!Owns(b, Notable.AquaVitae)) return 0f;
            float killsPerSecond = dps / Mathf.Max(1f, ChaserEhp(floor));
            return killsPerSecond * maxHp * B.AquaVitaeHealFraction;
        }

        /// <summary>Cibation's standing shield, in health: lifesteal over the healing limit banked,
        /// fading over its seconds, never past its share of max health.</summary>
        static float BoardShield(Build b, float lifestealPerSecond, float maxHp)
        {
            if (!Owns(b, Notable.Cibation)) return 0f;
            float over = Mathf.Max(0f, lifestealPerSecond - StatCurves.LifestealPerSecond(maxHp));
            return Mathf.Min(maxHp * B.CibationShieldFraction, over * B.CibationFadeSeconds);
        }
    }
}
