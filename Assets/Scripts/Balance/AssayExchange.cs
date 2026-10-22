using System;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;
using Convergence.Exchange;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.Balance
{
    /// <summary>
    /// The exchange ledger's CONDITIONAL half priced - every entry that RunEffects runs on an
    /// event, a clock or a target, at the uptimes <see cref="Assay.Assume"/> states. What is just a
    /// number already reaches the model through <see cref="Mods"/>; this is the rest, so every boon,
    /// cost, Albedo and combination has a term (the guard <see cref="Assay.Entries"/> reports on).
    ///
    /// Each term reads the same Tuning.Exchange constant its effect reads, and follows the effect's
    /// own rule - conditional damage as run-layer points bent with the ledger's own (Mods.Factor),
    /// conditional reductions INSIDE the mitigation floor, costs outside it, negations on one clock.
    /// Off for a legacy build, which replays PlayerPower.
    /// </summary>
    public static partial class Assay
    {
        /// <summary>AttackStep.ChainFalloff's default - what a body a swing passes through keeps.</summary>
        const float SwingFalloff = 0.78f;

        static float AnaemiaFactor => 1f - (1f - T.AnaemiaMul) * Assume.HealAboveHalfShare;

        /// <summary>The stackable entry is at max stacks - its Rubedo or Nigredo is live.</summary>
        static bool AtMax(Func<string, int> n, string id)
        {
            var e = ExchangeCatalog.Get(id);
            return e != null && e.Stackable && n(id) >= e.MaxStacks;
        }

        static string TrapIdFor(ElementType? element) => element switch
        {
            ElementType.Fire => "salamander",
            ElementType.Earth => "gnome",
            ElementType.Water => "undine",
            ElementType.Air => "sylph",
            _ => null,
        };

        static float StillTwoSeconds(ElementType? element)
            => element == ElementType.Earth ? Assume.EarthStillTwoSecondsShare : Assume.StillTwoSecondsShare;

        /// <summary>A conditional bonus held for a share of hits, as an expected multiplier.</summary>
        static float Sometimes(float share, float factor) => 1f + Mathf.Clamp01(share) * (factor - 1f);

        /// <summary>The average of a per-ten-seconds ramp over one floor (Souring, Maturation, Acetum).</summary>
        static float FloorAverage(Func<float, float> atTens)
        {
            float sum = 0f;
            int steps = Mathf.Max(1, Mathf.RoundToInt(Assume.FloorSeconds));
            for (int i = 0; i < steps; i++) sum += atTens(Mathf.Floor(i / T.SouringEverySeconds));
            return sum / steps;
        }

        // ------------------------------------------------------------------ outgoing

        public struct Outgoing { public float Basic, Art, BonusShare, ChanceAdd; }

        /// <summary>
        /// The ledger's conditional damage on the chain's basics and its art (RunEffects.ModifyOutgoing),
        /// and the share of hits rolled with a crit bonus (RunEffects.BonusCritChance).
        /// </summary>
        static Outgoing LedgerOutgoing(Mods m, Func<string, int> n, Build b, int floor, int basics,
                                       float basic, float finisher, float critFactor, float accuracy, float chainSeconds)
        {
            float avgHit = (basics * basic + finisher) / (basics + 1) * critFactor;
            // The share of hits that are an enemy's first - or the first after a kill.
            float firstShare = 1f / Mathf.Max(1f, ChaserEhp(floor) / Mathf.Max(1e-3f, avgHit));
            float hitsPerSecond = (basics + 1) / Mathf.Max(0.1f, chainSeconds);

            // The body being fought: its health, and its armour standing in front of it.
            var chaser = Enemies.EnemyTypes.Of(Enemies.EnemyKind.Chaser);
            float bodyHp = Enemies.EnemyFactory.MaxHpFor(chaser, floor, false);
            float bodyEhp = Mathf.Max(1f, ChaserEhp(floor));

            float all = 1f;
            float D(float points) => m.Factor(StatKind.Damage, points);
            // First Blood: a share of each body's health taken at the opener - that much less to cut.
            if (n("first_blood") > 0) all /= 1f - T.FirstBloodShare * bodyHp / bodyEhp;
            // Souring: points lost for every ten seconds on the floor, up to its cap a stack.
            int sour = n("souring");
            if (sour > 0)
                all *= m.Factor(StatKind.Damage, 0f, -FloorAverage(t => Mathf.Min(T.SouringCap, T.SouringDamage * t)) * sour);
            if (AtMax(n, "long_reach")) all *= Sometimes(Assume.OuterReachShare, D(T.FarStrikeDamage));
            if (n("close_quarters") > 0) all *= Sometimes(Assume.HalfReachShare, D(T.CloseQuartersDamage));
            if (AtMax(n, "short_arm"))
                all *= Sometimes(1f - Assume.HalfReachShare, m.Factor(StatKind.Damage, 0f, -T.CrampedDamage));
            if (n("bloodstone") > 0 && n("blood_price") > 0)
                all *= D(Mathf.Min(T.BloodstoneCap, Assume.MissingHealth / T.BloodstoneMissingPerPoint));
            // Maturation: one art in every few seconds strips the armour off what it hits - that much
            // less to cut, as First Blood's share is.
            if (n("maturation") > 0) all /= 1f - Assume.MaturationStripShare * (bodyEhp - bodyHp) / bodyEhp;
            string trap = TrapIdFor(b.Element);
            if (trap != null && AtMax(n, trap)) all *= Sometimes(Assume.TrapUptime, D(T.TrapBonusDamage));
            int ex = n("executioner");
            if (n("hunt") > 0 && ex > 0)   // the seconds after each kill
                all *= Sometimes(firstShare * hitsPerSecond * T.HuntSeconds * (1f - T.ExecutionerBelow),
                                 D(T.ExecutionerDamage * ex));
            // Coup de Grace: a non-elite cut below the line dies - that much less health to take.
            if (AtMax(n, "executioner")) all /= 1f - T.CoupDeGraceBelow * Assume.NonEliteShare;
            // Rebated: every hit at the bottom of its range.
            if (AtMax(n, "dulled")) all *= (1f - Tuning.Attack.DamageSpread) / Mathf.Max(1e-3f, accuracy);
            // Quicksand: standing still costs attack speed - as a share of the chain's pace.
            if (AtMax(n, "restless")) all *= 1f - T.QuicksandSpeed / 100f * StillTwoSeconds(b.Element) * 0.8f;

            float basicMul = all, artMul = all;
            if (AtMax(n, "fleetfoot")) basicMul *= Sometimes(Assume.WakeShare, D(T.WakeDamage));
            if (n("damascene") > 0)   // the art scores the next hits - the following chain's
            {
                float scored = Sometimes(Mathf.Min(1f, T.DamasceneHits / (float)(basics + 1)), D(T.DamasceneDamage));
                basicMul *= scored;
                artMul *= scored;
            }
            if (n("dulled_script") > 0) artMul *= 1f - T.DulledScriptDamage / Assume.RotationSockets;
            if (n("deliberate") > 0)
            {
                basicMul *= D(T.DeliberateDamagePerBasic * (basics - 1) * 0.5f);
                artMul *= D(T.DeliberateDamagePerBasic * basics);
            }
            if (n("green_lion") > 0)
                artMul *= Sometimes(Assume.PerfectShare,
                                    m.Factor(StatKind.FinisherPower, T.GreenLionArt * n("green_lion")));

            // Crit bonuses, in the one pool: the share of hits that roll one.
            float keep = 1f;
            // Honed: a body is above half health while its armour lasts and for half its health.
            float chanceAdd = n("honed") > 0
                ? T.HonedCrit * Mathf.Clamp01((bodyEhp - bodyHp * T.HonedAbove) / bodyEhp) : 0f;
            // Gilding: the basics after an art, on what it gilded, roll with more crit chance.
            if (n("gilding") > 0) chanceAdd += T.GildingCrit * Assume.GildedBasicShare * basics / (basics + 1f);
            if (n("cementation") > 0) keep *= 1f - 1f / T.CementationEvery;
            if (AtMax(n, "eagle")) keep *= 1f - firstShare;   // Stoop: the hit after a kill

            return new Outgoing { Basic = basicMul, Art = artMul, BonusShare = 1f - keep, ChanceAdd = chanceAdd };
        }

        /// <summary>Share of chains whose art comes banked, its basics skipped: Ouroboros (a kill's
        /// chance, a Chaser-equivalent each), The Serpent Eats (an art that kills), Golden Chain.</summary>
        static float RefundShare(Func<string, int> n, float dps, float seconds, float basicsDamage, float artDamage, int floor)
        {
            float killsPerChain = dps * seconds / Mathf.Max(1f, ChaserEhp(floor));
            float keep = 1f;
            int ouro = n("ouroboros");
            if (ouro > 0) keep *= 1f - Mathf.Min(1f, killsPerChain * T.OuroborosChance * ouro);
            if (AtMax(n, "ouroboros"))
                keep *= 1f - Mathf.Min(1f, killsPerChain * artDamage / Mathf.Max(1e-3f, basicsDamage + artDamage));
            if (n("golden_chain") > 0) keep *= 1f - 1f / T.GoldenChainEvery;
            // Unshackled: each use of the defensive ability skips one of the chain's basics.
            if (n("unshackled") > 0)
                keep *= 1f - Mathf.Min(1f, seconds / Assume.DefenseUseSeconds) / Convergence.Player.PlayerController.BaseBasicsPerChain;
            return 1f - keep;
        }

        /// <summary>The ledger's repeats beyond Reiteration's own (which Conditionals prices):
        /// Gemini making them whole, Felicity, Iron Rhythm.</summary>
        static float LedgerRepeats(Func<string, int> n, float chanceSum, float mulSum, Mods m, float critFactor)
        {
            float mul = 1f;
            int rei = n("reiteration");
            if (rei > 0 && n("gemini") > 0)
            {
                float every = Mathf.Max(2, T.ReiterationEvery - (rei - 1));
                mul *= (1f + 1f / every) / (1f + T.ReiterationFraction / every);
            }
            if (n("iron_rhythm") > 0 && n("fumbler") > 0)
                mul *= 1f + T.FumblerChance * n("fumbler") * T.ReiterationFraction;
            return mul;
        }

        /// <summary>Keen Edge: the body's armour spent faster - its effective health over what is
        /// left of it, on this floor's Chaser.</summary>
        static float KeenEdge(Func<string, int> n, int floor)
        {
            if (!AtMax(n, "whetstone")) return 1f;
            var def = Enemies.EnemyTypes.Of(Enemies.EnemyKind.Chaser);
            float hp = Enemies.EnemyFactory.MaxHpFor(def, floor, false);
            float armour = Enemies.EnemyFactory.ArmorFor(def, hp, floor, false);
            return (hp + armour) / Mathf.Max(1f, hp + armour / T.KeenEdgeShred);
        }

        /// <summary>Damage the ledger puts on the floor itself - Ley Lines through the target.</summary>
        static float LedgerFieldDps(Func<string, int> n, float hitUnit)
            => n("ley_lines") > 0 ? T.LeyLinesHitUnits * hitUnit / T.LeyLinesEvery : 0f;

        /// <summary>Fighting time the ledger takes away: Lapsus's stumble, Recoil's root.</summary>
        static float LedgerTimeLost(Func<string, int> n, ElementType? element, ChainShape c, float releasesPerSecond)
        {
            float f = 1f;
            if (AtMax(n, "fumbler"))
                f *= Mathf.Max(0.5f, 1f - c.HitsPerSecond * T.FumblerChance * n("fumbler") * T.LapsusStumble);
            if (AtMax(n, "backfire") && element != null)
                f *= Mathf.Max(0.5f, 1f - T.RecoilSeconds * releasesPerSecond);
            return f;
        }

        /// <summary>
        /// What the ledger adds on bodies OTHER than the one being fought, credited at
        /// <see cref="Assume.CrowdCredit"/>: Fulminate's bursts. The run's Cleave and Area are in
        /// AssayCrowd with the character's own.
        /// </summary>
        static float LedgerCrowd(Mods m, Func<string, int> n, Build b, ChainShape c)
        {
            float extra = 0f;
            if (AtMax(n, "vein_finder"))
                extra += c.CritChance * T.FulminateFraction * Assume.CrowdBodies;
            return 1f + Assume.CrowdCredit * extra;
        }

        // ------------------------------------------------------------------ releases

        public struct ReleaseTerms { public float PerSecond, HitMul, HitUnitsAdd; }

        /// <summary>
        /// How often the element releases and what each release is worth under the ledger: meter
        /// refilled by kills, hits taken and the floor's start, spilled by hits, held shut by
        /// Barren; and the release itself repeated, critting, steeped or leaving a field.
        /// </summary>
        static ReleaseTerms LedgerReleases(Build b, Mods m, Func<string, int> n, ElementProfile el, ChainShape c,
                                           int floor, float chainDps)
        {
            var r = new ReleaseTerms { PerSecond = el.ReleasesPerSecond, HitMul = 1f };
            if (b.Element == null || r.PerSecond <= 0f) return r;

            float keep = 1f + T.OverflowRefund * Mathf.Min(1, n("overflow"));   // Profile's refund rule
            float fill = 0f;   // meters a second
            if (AtMax(n, "rich_vein")) fill += T.MotherLodeRefill * chainDps / Mathf.Max(1f, ChaserEhp(floor));
            if (AtMax(n, "leaky_vessel")) fill -= T.CrackedVesselSpill * Assume.HitsTakenPerSecond;
            int attune = n("attunement");
            if (attune > 0) fill += (AtMax(n, "attunement") ? 1f : T.AttunementFill * attune) / Assume.FloorSeconds;

            float rate = r.PerSecond * (1f - 0.12f * n("leaky_vessel"))       // the meter fading faster
                                     * (AtMax(n, "low_water") ? 0.75f : 1f);    // Ebb: soaked bodies fill single
            rate = Mathf.Max(0.005f, rate + fill * keep);
            r.PerSecond = rate;

            if (n("twin_spark") > 0) r.HitMul += T.TwinSparkScale;
            if (n("wellspring") > 0) r.HitMul += 1f / T.WellspringEvery;            // every Nth fires twice
            if (AtMax(n, "elixir")) r.HitMul *= c.CritFactor;                      // Grand Elixir
            if (n("slow_fire") > 0 && n("stubborn_ore") > 0)
                r.HitMul *= m.Factor(StatKind.ElementalEffectiveness, Mathf.Min(T.SlowFireCap, T.SlowFirePowerPerSecond / rate));
            if (n("residue") > 0 && b.Element == ElementType.Fire)
                r.HitUnitsAdd += T.ResidueBurnHitUnits * T.ResidueSeconds * Assume.FireTargetInPool;
            return r;
        }

        // ------------------------------------------------------------------ incoming

        /// <summary>The ledger's conditional reductions - INSIDE the mitigation floor, as
        /// RunEffects.MitigationNow is.</summary>
        static float LedgerMitigation(Func<string, int> n, ElementType? element, ChainShape c)
        {
            float f = 1f;
            int reactive = n("reactive_plate");
            if (reactive > 0)
            {
                float uptime = Assume.ReactiveUptime;
                if (AtMax(n, "reactive_plate"))   // Tempered: the window comes back sooner
                    uptime = Mathf.Min(0.85f, uptime * T.ReactivePlateCooldown / T.ReactivePlateCooldownTempered);
                f *= 1f - (AtMax(n, "reactive_plate") ? T.ReactivePlateReductionII : T.ReactivePlateReduction) * uptime;
            }
            if (AtMax(n, "stonestance"))   // Lapis: the hit after a second planted deals half
            {
                float still = element == ElementType.Earth ? Assume.EarthStillShare : Assume.StillAfterSecondShare;
                f *= 1f - (1f - T.LapisMul) * still * 0.5f;
            }
            if (AtMax(n, "equipoise"))   // Poise: the hit after a change of pace deals half
                f *= 1f - (1f - T.PoiseMul) * Assume.PaceChangeShare;
            if (AtMax(n, "thickened_hide")) f *= 1f - (1f - T.FortitudeMul) * Assume.CriticalHealthShare;
            // Adamant: a heavy blow tempers you - Resilience for a few seconds.
            if (n("adamant") > 0)
                f *= 1f - (1f - StatPercents.ReductionFactor(T.AdamantResilience)) * Assume.TemperedShare;
            if (n("retrograde_motion") > 0 && n("retrograde") > 0)
                f *= 1f - (1f - T.RetrogradeMotionTakenMul) * InvertedShare(n);
            return f;
        }

        static float InvertedShare(Func<string, int> n)
            => T.RetrogradeSeconds / (AtMax(n, "retrograde") ? T.RetrogradeEveryII : T.RetrogradeEvery);

        /// <summary>The costs' multipliers on damage taken - OUTSIDE the floor, as RunEffects and
        /// Health.ScaleIncoming apply them - and a trap boon's immunity to its own hazard.</summary>
        static float LedgerOutside(Mods m, Func<string, int> n, ElementType? element, ChainShape c)
        {
            float f = 1f;
            if (n("open_stance") > 0)
                f *= 1f + T.OpenStanceHits / (Assume.FloorSeconds * Assume.HitsTakenPerSecond);
            if (n("glass_bones") > 0) f *= 1f + T.GlassBonesTaken * Assume.LowHealthShare;
            if (AtMax(n, "overcommitted")) f *= 1f + T.OverextendedTaken * c.LockShare;
            int restless = n("restless");
            if (restless > 0) f *= 1f + T.RestlessTaken * restless * StillTwoSeconds(element);
            if (n("haemophilia") > 0) f *= 1f + T.HaemophiliaFraction;
            if (m.SenescenceFloors > 0) f *= 1f + T.SenescenceTaken * m.SenescenceFloors;
            if (AtMax(n, "souring")) f *= 1f + T.AcetumTaken * FloorAverage(t => t);
            string trap = TrapIdFor(element);
            if (trap != null && n(trap) > 0) f *= 1f - Assume.HazardShare;

            // The Nigredos' exposures: the cost's own weak moment, damage taken outside the floor.
            // (Shackled, Barren, Wet Ash, Ebb and Doldrums were re-cut from exposures - Assay.Judged.)
            if (n("unbloodied") > 0) f *= 1f + T.UnbloodiedTaken * Assume.OathHeldShare;   // an oath
            if (n("sealed_sands") > 0) f *= Tuning.RunClock.TierStep;                       // a heat tier ahead
            return f;
        }

        // ------------------------------------------------------------------ the status layer

        /// <summary>
        /// The status layer's damage (Phase 1, 2026-10-20): Tinder's burns and Serrated's bleeds as
        /// damage over time on the body being fought, Lancet cashing a bleed in early, Backdraft
        /// burning out an attacker. A burn or a bleed MERGES BY MAX (StatusEffects), so beside the
        /// board's own (Choler, Sanguine - bigger, off arts and crits) these add mostly uptime: a
        /// share, not the whole. Tidewater's soak is a vulnerability while the attacker is the body
        /// being fought.
        /// </summary>
        static float LedgerDamageOverTime(Build b, Func<string, int> n, ChainShape c)
        {
            float dot = 0f;
            float basicsPerSecond = c.Basics / Mathf.Max(0.1f, c.Seconds);
            float burnDps = 0f;
            if (n("tinder") > 0)
            {
                float uptime = Mathf.Min(1f, T.TinderChance * basicsPerSecond * T.TinderSeconds);
                burnDps = T.TinderBurnShare * c.BasicHit * uptime * (Owns(b, Progression.Notable.Choler) ? 0.4f : 1f);
                dot += burnDps;
            }
            float bleedDps = 0f;
            if (n("serrated") > 0)
            {
                float uptime = Mathf.Min(1f, T.SerratedSeconds / Mathf.Max(0.1f, c.Seconds));
                bleedDps = T.SerratedBleedShare * c.BasicHit * uptime * (Owns(b, Progression.Notable.Sanguine) ? 0.5f : 1f);
                dot += bleedDps;
            }
            // Lancet: each art cashes the running bleed in at half again - about half its seconds left.
            if (n("lancet") > 0)
            {
                float running = bleedDps + (Owns(b, Progression.Notable.Sanguine) ? Tuning.Board.SanguineBleedFraction * c.CritHit * c.CritChance : 0f);
                dot += (T.LancetShare - 1f) * running * Mathf.Min(c.Seconds, T.SerratedSeconds) * 0.5f / Mathf.Max(0.1f, c.Seconds);
            }
            // Backdraft: a burning attacker that is the body being fought burns out at once.
            if (n("backdraft") > 0 && burnDps > 0f)
                dot += Assume.HitsTakenPerSecond * Assume.FromTargetShare * burnDps * T.TinderSeconds * 0.5f;
            return dot;
        }

        /// <summary>Tidewater: the attacker soaked - a vulnerability on the body being fought while it
        /// is the one hitting you. Riptide: one art in three soaks a strip along the aim.</summary>
        static float LedgerVulnerability(Build b, Func<string, int> n, ChainShape c)
        {
            float f = 1f;
            // Lucid: a body whose wind-up was dodged takes more for a few seconds.
            if (n("lucid") > 0) f *= 1f + T.LucidExposed * Assume.LucidExposedShare;
            // Basilisk: the body held under the lock is stoned.
            if (n("basilisk") > 0) f *= 1f + T.BasiliskVulnerability * Assume.GazeHeldShare;
            // Sealed Vessel on Water: the lost meter soaks what is round you.
            if (n("sealed_vessel") > 0 && b.Element == ElementType.Water)
            {
                float uptime = Mathf.Min(1f, SealedSplashesPerSecond * T.SealedVesselSeconds);
                f *= 1f + (Tuning.Elements.Water.SoakVulnerability - 1f) * uptime;
            }
            if (n("tidewater") > 0)
            {
                float uptime = Mathf.Min(1f, Assume.HitsTakenPerSecond * T.TidewaterSeconds) * Assume.FromTargetShare;
                f *= 1f + (T.TidewaterVulnerability - 1f) * uptime;
            }
            // Spindrift: the body being fought walked in through the spray - soaked as it arrives.
            if (n("spindrift") > 0)
                f *= 1f + (T.SpindriftSoakVulnerability - 1f) * Assume.SpindriftSoakedShare;
            if (n("riptide") > 0 || n("maelstrom") > 0)
            {
                // Maelstrom's pool holds the soak on longer.
                float seconds = T.RiptideSoakSeconds + (n("maelstrom") > 0 ? T.MaelstromPoolSeconds * 0.5f : 0f);
                float uptime = Mathf.Min(1f, seconds / Mathf.Max(0.1f, c.Seconds * Assume.RotationSockets));
                f *= 1f + (T.RiptideVulnerability - 1f) * uptime;
            }
            return f;
        }

        /// <summary>
        /// The forms' damage on the body being fought (Phase 2): Shockwave's ring and its burn from
        /// one art in three, Fault Line's crack under a greatsword's crowded swings. A form held
        /// alone is at full strength - the model builds no rotation, so a shared slot is not seen.
        /// Dulled Script's art damage is in LedgerOutgoing.
        /// </summary>
        static float LedgerForms(Func<string, int> n, ChainShape c, float hitUnit)
        {
            float dps = 0f, artsPerSecond = 1f / Mathf.Max(0.1f, c.Seconds * Assume.RotationSockets);
            if (n("shockwave") > 0)
                dps += artsPerSecond * hitUnit * (T.ShockwaveHitUnits + T.ShockwaveBurnShare * T.ShockwaveBurnSeconds);
            if (n("nova") > 0)
                dps += artsPerSecond * hitUnit * (T.NovaHitUnits + T.NovaBurnShare * T.NovaBurnSeconds);
            if (n("hemorrhage") > 0)
                dps += T.HemorrhageBleedHitUnits * hitUnit * T.LacerateSeconds / Assume.DefenseUseSeconds;
            if (n("fault_line") > 0)
                dps += c.Basics / Mathf.Max(0.1f, c.Seconds) * Assume.ThreeBodySwingShare * T.FaultLineHitUnits * hitUnit;
            if (n("lacerate") > 0)
                dps += T.LacerateBleedHitUnits * hitUnit * T.LacerateSeconds / Assume.DefenseUseSeconds;
            // Lone Cut: a basic that strikes one enemy bleeds it.
            if (n("lone_cut") > 0)
                dps += c.Basics / Mathf.Max(0.1f, c.Seconds) * Assume.LoneSwingShare
                       * T.LoneCutBleedShare * T.LoneCutBleedSeconds * c.BasicHit;
            // Afterburn: once a defensive ability, one basic with Damage points and a burn.
            if (n("afterburn") > 0)
                dps += (T.AfterburnDamage / 100f + T.AfterburnBurnShare * T.AfterburnBurnSeconds)
                       * c.BasicHit / Assume.DefenseUseSeconds;
            // Drawdown gathers the crowd and deals nothing itself - unpriced, as Residue's shapes are.
            // Fury of the Frail (reworked): low on health, basics bleed.
            if (n("fury_of_the_frail") > 0)
                dps += Assume.LowHealthShare * T.FuryBleedShare * c.BasicHit;
            return dps;
        }

        // ------------------------------------------------------------------ the transformed Albedos

        static float SealedSplashesPerSecond => Assume.MeterLostPerSecond / T.SealedVesselSplashEvery;

        /// <summary>
        /// Batch 3 of the transformed Albedos on the body being fought: Sealed Vessel's burns and
        /// bleeds (Fire, Air - Water's soak is a vulnerability, Earth's stagger a survival
        /// factor, neither here), Lightfoot's cut, Ossuary's bursts. Lucid is a vulnerability,
        /// Unshackled a refund, Concentrate a crowd term, Mountain a survival factor.
        /// </summary>
        static float LedgerAlbedos(Build b, Func<string, int> n, ChainShape c, float hitUnit)
        {
            float dps = 0f;
            // Patina: a shard at the striker for each plate broken - the body being fought, half the time.
            if (n("patina") > 0)
                dps += Assume.PlatesPerSecond * T.PatinaShardHitUnits * hitUnit * Assume.FromTargetShare;
            // Phlogiston: heat lost flares round you, burning what is near.
            if (n("phlogiston") > 0 && b.Element == ElementType.Fire)
                dps += Assume.HeatLostPerSecond * T.PhlogistonBurnHitUnits * T.PhlogistonBurnSeconds * hitUnit;
            // Committed: blows taken in an art's lock, half added to the next art.
            if (n("committed") > 0)
                dps += Assume.HitsTakenPerSecond * c.LockShare * T.CommittedShare * Assume.BlowInHitUnits * hitUnit;
            if (n("sealed_vessel") > 0 && (b.Element == ElementType.Fire || b.Element == ElementType.Air))
                dps += SealedSplashesPerSecond * T.SealedVesselDotHitUnits * T.SealedVesselSeconds * hitUnit;
            if (n("lightfoot") > 0)
                dps += Assume.AnchorSnapShare / Assume.DefenseUseSeconds * Assume.AnchorCutsTarget
                       * T.LightfootCutHitUnits * hitUnit;
            if (n("ossuary") > 0)
                dps += Assume.OssuaryBurstsPerSecond * T.OssuaryBurstHitUnits * hitUnit
                       * Mathf.Min(1f, BodiesWithin(T.OssuaryBurstRadius));
            return dps;
        }

        /// <summary>The same on the OTHER bodies, credited as crowd.</summary>
        static float LedgerAlbedosCrowd(Build b, Func<string, int> n, float hitUnit)
        {
            float dps = 0f;
            if (n("phlogiston") > 0 && b.Element == ElementType.Fire)
                dps += Assume.HeatLostPerSecond * T.PhlogistonBurnHitUnits * T.PhlogistonBurnSeconds * hitUnit
                       * Mathf.Max(0f, BodiesWithin(T.PhlogistonRadius) - 1f);
            // Mollification: a broken shell's shards on the bodies round it.
            if (n("mollification") > 0)
                dps += Assume.ArmourBreaksPerSecond * T.MollificationShardHitUnits * hitUnit
                       * BodiesWithin(T.MollificationRadius);
            if (n("sealed_vessel") > 0 && (b.Element == ElementType.Fire || b.Element == ElementType.Air))
                dps += SealedSplashesPerSecond * T.SealedVesselDotHitUnits * T.SealedVesselSeconds * hitUnit
                       * Mathf.Max(0f, BodiesWithin(T.SealedVesselRadius) - 1f);
            if (n("lightfoot") > 0)
                dps += Assume.AnchorSnapShare / Assume.DefenseUseSeconds * T.LightfootCutHitUnits * hitUnit
                       * BodiesWithin(T.LightfootCutWidth * 2f);
            if (n("ossuary") > 0)
                dps += Assume.OssuaryBurstsPerSecond * T.OssuaryBurstHitUnits * hitUnit
                       * Mathf.Max(0f, BodiesWithin(T.OssuaryBurstRadius) - 1f);
            return dps;
        }

        /// <summary>The forms' damage on the OTHER bodies round the fight - credited as crowd:
        /// Shockwave's ring, Fault Line's crack, Lacerate's bleeds.</summary>
        static float LedgerFormsCrowd(Func<string, int> n, ChainShape c, float hitUnit)
        {
            float dps = 0f, artsPerSecond = 1f / Mathf.Max(0.1f, c.Seconds * Assume.RotationSockets);
            if (n("shockwave") > 0)
                dps += artsPerSecond * hitUnit * (T.ShockwaveHitUnits + T.ShockwaveBurnShare * T.ShockwaveBurnSeconds)
                     * Mathf.Max(0f, BodiesWithin(T.ShockwaveRadius) - 1f);
            if (n("nova") > 0)
                dps += artsPerSecond * hitUnit * (T.NovaHitUnits + T.NovaBurnShare * T.NovaBurnSeconds)
                     * Mathf.Max(0f, BodiesWithin(T.NovaRadius) - 1f);
            if (n("hemorrhage") > 0)
                dps += T.HemorrhageBleedHitUnits * hitUnit * T.LacerateSeconds / Assume.DefenseUseSeconds
                     * Mathf.Max(0f, BodiesWithin(T.HemorrhageRadius) - 1f);
            if (n("fault_line") > 0)
                dps += c.Basics / Mathf.Max(0.1f, c.Seconds) * Assume.ThreeBodySwingShare * T.FaultLineHitUnits * hitUnit * 2f;
            if (n("lacerate") > 0)
                dps += T.LacerateBleedHitUnits * hitUnit * T.LacerateSeconds / Assume.DefenseUseSeconds
                     * Mathf.Max(0f, BodiesWithin(T.LacerateRadius) - 1f);
            // Felicity (reworked): one swing in nine throws a crescent through the bodies ahead.
            if (n("felicity") > 0)
                dps += c.HitsPerSecond * T.FelicityChance * T.FelicityShare * c.BasicHit
                     * Mathf.Max(0f, BodiesWithin(T.FelicityReach * 0.5f) - 1f);
            return dps;
        }

        // ------------------------------------------------------------------ armour wear

        /// <summary>
        /// Armour condition while floor <paramref name="floor"/> is fought (the mean of its start and
        /// end) - whole at the run's
        /// start (ProfileRules empties wear at both checkpoints). Each floor: the fight's blows wear
        /// every armour piece (GameBootstrap's Damaged hook - a Chaser-sized blow each, slowed by
        /// Sturdiness, spread over a pool Armour grows), then the clear mends (Mend) and a share of
        /// floors repair (the restore ring, a Lead spire), every repair scaled by the ledger's.
        /// </summary>
        public static float ArmourCondition(StatPercents s, float wearMul, float repairMul, int floor, float platingLost = 0f)
        {
            float pool = StatPercents.Apply(100f, s.Armor);
            // PLATING absorbs the floor's first blows (Tuning.Defense.ArmorPerPlate), less what Rust
            // takes; the ledger's wear multiplier acts on the rest.
            float plating = Mathf.Max(0f, s.Armor / Tuning.Defense.ArmorPerPlate - platingLost);
            float blows = Mathf.Max(0f, Assume.HitsTakenPerSecond * Assume.FloorSeconds - plating);
            float wear = blows * Assume.ArmorWearPerHit
                       * wearMul * StatPercents.ReductionFactor(s.DamageResistance) / Mathf.Max(1f, pool);
            float repair = (s.Mend / 100f
                            + Assume.RepairRingShare * Assume.RepairRingFraction * StatPercents.Apply(1f, s.RepairReceived)
                            + Assume.LeadSpireShare * Tuning.Spire.RepairFraction) * repairMul;
            float c = 1f;
            for (int f = 1; f < Mathf.Max(1, floor); f++)
                c = Mathf.Min(1f, Mathf.Max(0f, c - wear) + repair);
            return (c + Mathf.Max(0f, c - wear)) * 0.5f;   // the floor is fought from c down
        }

        /// <summary>
        /// What the ledger does to worn armour's penalty (+50% damage taken at zero condition), as
        /// factors against the run without it: Rust takes plating, Corrosion halves the repairs,
        /// Patina the penalty. Under Corrosion the penalty leaves the inside of
        /// the mitigation floor for the outside - so (inside, outside).
        /// </summary>
        static (float Inside, float Outside) LedgerWear(StatPercents s, Mods m, Func<string, int> n, int floor)
        {
            float Penalty(float condition, float share) => 1f + (1f - condition) * Chain.Durability.MaxDamageTakenPenalty * share;
            float baseline = Penalty(ArmourCondition(s, 1f, 1f, floor), 1f);
            float with = Penalty(ArmourCondition(s, m.ArmourWearMul, m.RepairMul, floor, m.PlatingLost), m.ArmourWearPenaltyMul);
            if (AtMax(n, "rust")) return (1f / baseline, with);   // Corrosion: the wear lands outside
            return (with / baseline, 1f);
        }

        /// <summary>What the ledger's hand in the enemy's toughness does to damage: the body's
        /// effective health over what it now is - health and armour scaled (GameBootstrap.Harden).</summary>
        static float EnemyToughness(Mods m, int floor)
        {
            if (Mathf.Approximately(m.EnemyHealthMul, 1f) && Mathf.Approximately(m.EliteHealthMul, 1f)) return 1f;
            var def = Enemies.EnemyTypes.Of(Enemies.EnemyKind.Chaser);
            float hp = Enemies.EnemyFactory.MaxHpFor(def, floor, false);
            float armour = Enemies.EnemyFactory.ArmorFor(def, hp, floor, false);
            float mul = m.EnemyHealthMul * (1f + Assume.EliteShare * (m.EliteHealthMul - 1f));
            return 1f / Mathf.Max(0.01f, mul);
        }

        /// <summary>
        /// The share of hits the ledger makes land as nothing - Aegis's ward, Ghostwalk's window,
        /// Vapour's pass - on ONE clock (Tuning.Exchange.NegationGapSeconds): each negation shuts
        /// the others out for the gap, so their rates add and then saturate, rather than multiply.
        /// </summary>
        static float NegatedShare(Func<string, int> n, ElementType? element)
        {
            float hits = Assume.HitsTakenPerSecond;
            float moving = element == ElementType.Earth ? 1f - Assume.EarthStillShare : Assume.MovingShare;
            float events = 0f, turned = 0f, gap = T.NegationGapSeconds;

            int aegis = n("aegis_cycle");
            if (aegis > 0)
            {
                float renew = aegis >= 2 ? T.AegisRenewSecondsII : T.AegisRenewSeconds;
                float rate = (moving + (1f - moving) * (n("athanor") > 0 ? 2f : 1f)) / renew;
                events += rate;
                turned += rate;
            }
            if (AtMax(n, "evanescence"))
            {
                float rate = moving * hits / T.VapourEvery;
                events += rate;
                turned += rate;
            }
            if (n("ghostwalk") > 0)
            {
                float rate = 1f / (T.GhostwalkCooldown + 1f / hits);
                events += rate;
                turned += rate * Assume.GhostwalkHits;
                gap += T.GhostwalkSeconds * rate / Mathf.Max(1e-4f, events);
            }
            if (events <= 0f) return 0f;
            float landed = events / (1f + events * gap);   // the clock shuts each one out for the gap
            return Mathf.Min(0.9f, landed * (turned / events) / hits);
        }

        /// <summary>What Encumbered and Shackled cost the defensive ability, as a factor on damage
        /// taken - 1 with neither.</summary>
        static float DefensiveAbility(Func<string, int> n)
        {
            int enc = n("encumbered");
            if (enc <= 0) return 1f;
            float k = 1f / (1f + T.EncumberedCooldown * enc);
            return (1f - Assume.DefenseShare * k) / (1f - Assume.DefenseShare);
        }

        // ------------------------------------------------------------------ health

        /// <summary>The ledger's heals that share lifesteal's limit: Vital Spark, Pelican.</summary>
        static float LedgerHealWithinLimit(Func<string, int> n, float maxHp, float swingsPerSecond)
        {
            float h = 0f;
            return h;
        }

        /// <summary>The ledger's other heals: Rebound on a release. (Tribute heals at the clear, so
        /// it is priced as the health a floor starts with - in Evaluate, beside Toll.)</summary>
        static float LedgerHeal(Func<string, int> n, float maxHp, float releasesPerSecond)
        {
            float h = 0f;
            return h;
        }

        /// <summary>What the ledger charges in health a second: Blood Price (twice below half under
        /// Haemorrhage), Backfire. (Toll is taken at the clear - priced in Evaluate.)</summary>
        static float LedgerBleed(Func<string, int> n, float maxHp, float swingsPerSecond, float releasesPerSecond)
        {
            float price = maxHp * T.BloodPriceSwing * n("blood_price") * swingsPerSecond
                        * (AtMax(n, "blood_price") ? 1f + Assume.LowHealthShare : 1f);
            float backfire = maxHp * T.BackfireFraction * n("backfire") * releasesPerSecond;
            return price + backfire;
        }

        // ------------------------------------------------------------------ the price list

        /// <summary>Entries that change the deal or the floor's rewards, not the character - judged
        /// by what they do to the exchange, never by this.</summary>
        static readonly string[] Unpriced =
            { "caput_mortuum", "indenture", "debt", "prima_materia", "curator", "transmuters_eye", "scrying_glass", "oracle", "tribute" };

        /// <summary>The four builds an entry is priced on: every archetype, on the entry's element
        /// when it has one, else each on its own (so all four elements are seen).</summary>
        static Build[] PriceBuilds(ElementType? only)
        {
            if (only != null)
                return new[] { Make(Archetype.Striker, only.Value, Kit.Max), Make(Archetype.Tempo, only.Value, Kit.Max),
                               Make(Archetype.Bulwark, only.Value, Kit.Max), Make(Archetype.Elementalist, only.Value, Kit.Max) };
            return new[] { Make(Archetype.Striker, ElementType.Fire, Kit.Max), Make(Archetype.Tempo, ElementType.Air, Kit.Max),
                           Make(Archetype.Bulwark, ElementType.Earth, Kit.Max), Make(Archetype.Elementalist, ElementType.Water, Kit.Max) };
        }

        /// <summary>What a ledger is worth to one build: damage and effective health, log-summed -
        /// the sensible player's own utility.</summary>
        static (float Dps, float Hp) WorthOf(Build b, RunModifiers ledger, int floor)
        {
            var w = Evaluate(b, ledger.Current, ledger.StacksOf, floor);
            return (w.Dps, w.EffectiveHp);
        }

        static RunModifiers LedgerFor(Build b, ExchangeEntry e, int stacks, bool withParts)
        {
            var l = new RunModifiers();
            l.SetCharacter(b.Element, WeaponClass.Greatsword, Tuning.Mastery.LevelCap);
            if (withParts && e.Parts != null)
                foreach (var part in e.Parts) l.Take(ExchangeCatalog.Get(part));
            for (int i = 0; i < stacks; i++) l.Take(e);
            // Entries that grow by the floor are priced over the horizon a careful player looks ahead.
            if (e.Id == "withering" || e.Id == "senescence" || e.Id == "desiccation")
                for (int f = 0; f < Assume.WitheringHorizon; f++) l.FloorCleared();
            return l;
        }

        /// <summary>The most a cost held at max stacks takes off one axis, on the build it hurts most.</summary>
        static float MaxBite(ExchangeEntry e, int floor)
        {
            float worst = 0f;
            foreach (var b in PriceBuilds(e.Element))
            {
                var bare = WorthOf(b, LedgerFor(b, e, 0, true), floor);
                var all = WorthOf(b, LedgerFor(b, e, e.MaxStacks, true), floor);
                worst = Mathf.Max(worst, 1f - all.Dps / bare.Dps * all.Hp / bare.Hp);
            }
            return worst;
        }

        /// <summary>The scale signed off with the Phase 5 catalogue: what one stack of a boon is worth
        /// on its axis by weight (an Albedo or a combination is sized like a weight-3 boon), and what
        /// one stack of a cost takes.</summary>
        public static float StackTarget(ExchangeEntry e)
        {
            bool earned = e.Origin != EntryOrigin.Base;
            if (e.Kind == ExchangeKind.Boon)
                return earned ? 0.15f : e.Weight switch { 1 => 0.05f, 2 => 0.10f, _ => 0.15f };
            if (earned) return 0.18f;
            return e.Weight switch { 1 => 0.11f, 2 => 0.12f, _ => 0.18f };
        }

        /// <summary>
        /// EVERY ENTRY PRICED (the M4 guard): each boon, cost, Albedo and combination held alone
        /// on the four Max builds at <paramref name="floor"/> - a combination with its parts held,
        /// an Albedo with its cost gone. "first" is one stack, "max" all of them, capstone included;
        /// a boon reads the build it suits best (a boon that suits nobody is LOW), a cost the build
        /// it hurts most - both as damage times survival. The target is the signed-off scale
        /// (<see cref="StackTarget"/>); a capstone counts as one more stack. Flags: LOW under half
        /// the target, HIGH over 1.75x - and for a stackable cost, SOFT when max stacks takes less
        /// than <see cref="Targets.CostAtMaxStacks"/>.
        /// </summary>
        public static string Entries(int floor = 50, string only = null)
        {
            var sb = new System.Text.StringBuilder(
                $"Every entry, alone, on the four Max builds at floor {floor} - worth as damage x survival (boons) " +
                "or the most it takes off one axis (costs).\n" +
                "id                  kind  W st | first  per-stack target | max    max-target | best build        | flag\n");
            foreach (var e in ExchangeCatalog.All)
            {
                if (Array.IndexOf(Unpriced, e.Id) >= 0) continue;
                if (only != null && !e.Id.Contains(only)) continue;
                if (e.Classes != null && Array.IndexOf(e.Classes, WeaponClass.Greatsword) < 0) continue;   // the model swings a sword

                float bestFirst = float.MinValue, bestMax = float.MinValue;
                string bestBuild = "";
                foreach (var b in PriceBuilds(e.Element))
                {
                    var bare = WorthOf(b, LedgerFor(b, e, 0, true), floor);
                    var one = WorthOf(b, LedgerFor(b, e, 1, true), floor);
                    var all = WorthOf(b, LedgerFor(b, e, e.MaxStacks, true), floor);
                    float first, max;
                    if (e.Kind == ExchangeKind.Boon)
                    {
                        first = one.Dps / bare.Dps * one.Hp / bare.Hp - 1f;
                        max = all.Dps / bare.Dps * all.Hp / bare.Hp - 1f;
                    }
                    else
                    {
                        // Damage and survival together, as a boon is measured - a Nigredo that
                        // exposes you on top of a stack that takes damage costs both.
                        first = 1f - one.Dps / bare.Dps * one.Hp / bare.Hp;
                        max = 1f - all.Dps / bare.Dps * all.Hp / bare.Hp;
                    }
                    if (max > bestMax) { bestMax = max; bestFirst = first; bestBuild = b.Key; }
                }

                float target = StackTarget(e);
                int units = e.MaxStacks + (e.Stackable ? 1 : 0);   // a capstone counts as one more stack
                string flag = "";
                if (bestFirst < target * 0.5f) flag = "LOW";
                else if (bestFirst > target * 1.75f || bestMax > target * units * 1.75f) flag = "HIGH";
                if (e.Kind == ExchangeKind.Cost && e.Stackable && bestMax < Targets.CostAtMaxStacks)
                    flag = (flag.Length > 0 ? flag + " " : "") + "SOFT";
                string kind = e.Origin switch
                {
                    EntryOrigin.Albedo => "Alb", EntryOrigin.Conjunction => "Conj",
                    EntryOrigin.Putrefaction => "Putr", EntryOrigin.Citrinitas => "Citr",
                    _ => e.Kind == ExchangeKind.Boon ? "boon" : "cost",
                };
                sb.Append($"{e.Id,-19} {kind,-5} {e.Weight} {e.MaxStacks,2} | {bestFirst,5:P0} {target,8:P0}         |" +
                          $" {bestMax,5:P0} {target * units,7:P0}    | {bestBuild,-17} | {flag}\n");
            }
            return sb.ToString();
        }
    }
}
