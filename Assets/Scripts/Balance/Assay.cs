using System;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;
using Convergence.Exchange;

namespace Convergence.Balance
{
    /// <summary>
    /// What one moment of a run is worth: damage going out and survival coming in, for one build
    /// carrying one ledger on one floor.
    /// </summary>
    public struct Worth
    {
        /// <summary>Sustained single-target damage per second: the weapon's chain, the element's
        /// passives and burn, and its release spread over the release cadence.</summary>
        public float Dps;

        /// <summary>The weapon's chain alone - basics, the weapon art, crits. With a legacy build
        /// this is exactly what Player.PlayerPower measures.</summary>
        public float ChainDps;

        /// <summary>One release landing on one target.</summary>
        public float ReleaseHit;

        public float MaxHp;

        /// <summary>The fraction of an enemy hit that reaches health. 1 takes all of it.</summary>
        public float Incoming;

        /// <summary>Max health over Incoming, adjusted for the ledger's survival tricks and costs.</summary>
        public float Ehp;

        /// <summary>Health per second: lifesteal at this DPS, minus what the ledger bleeds you for.</summary>
        public float Sustain;

        /// <summary>
        /// Ehp plus <see cref="Assay.Assume.SustainWindow"/> seconds of Sustain - the survival
        /// figure the sensible pick policy weighs against damage.
        ///
        /// Healing counts for at most another Ehp: it cannot heal through a burst bigger than the
        /// pool it refills. Uncapped, lifesteal at a deep floor's damage outweighed any health cost
        /// and the policy took every one. Self-bleed counts in full.
        /// </summary>
        public float EffectiveHp
            => Mathf.Max(1f, Ehp + Mathf.Min(Ehp, Assay.Assume.SustainWindow * Mathf.Max(0f, Sustain))
                             + Assay.Assume.SustainWindow * Mathf.Min(0f, Sustain));
    }

    /// <summary>
    /// THE ASSAY - the balance model: what a build is worth on a floor, damage out and survival in.
    ///
    /// The successor to Player.PlayerPower, which measured damage only. Pricing waves on damage
    /// alone let its damage-greedy player take every survival cost for free (it held max Thin
    /// Blood, Paper Guard and Withering), so the deep floors were priced against a player who
    /// could not have lived through their own ledger. The Assay prices both axes.
    ///
    /// Built from the game's own pieces like PlayerPower was: gear is real GearRoller output from
    /// the real slot pools, the board is read off MasteryBoard, and the ledger is the real
    /// ExchangeOffers drawing from the real catalogue. What the game's code cannot answer - how
    /// often a player stands still, how full Air's momentum runs - is stated once, in
    /// <see cref="Assume"/>, and nowhere else.
    ///
    /// SINGLE TARGET. Cleave, Splash, Fire's splash and every area release make a real crowd die
    /// faster than this says, so a clear time derived from it is the slow end.
    ///
    /// Nothing in the game reads it yet. It replaces PlayerPower in WaveComposer once the stat
    /// core lands; until then it is measurement only.
    ///
    ///     unity command eval 'return Convergence.Balance.Assay.Check();'
    ///     unity command eval 'return Convergence.Balance.Assay.Matrix();'
    /// </summary>
    public static partial class Assay
    {
        /// <summary>Runs end at floor 100.</summary>
        public const int LastFloor = Player.PlayerPower.LastFloor;

        /// <summary>The same seed PlayerPower uses, so a legacy build replays its exact runs.</summary>
        const int Seed = 20261002;

        /// <summary>
        /// Every guess the model makes. Nothing here can be read off the game's code: each is a
        /// statement about how the game is PLAYED, and each is meant to be argued with and replaced
        /// by measured play once there is some.
        /// </summary>
        public static class Assume
        {
            /// <summary>Share of hits taken while moving (Graze) rather than standing (Brace).</summary>
            public const float MovingShare = 0.6f;

            /// <summary>Earth's share of a fight spent planted - its charge and its damage reduction
            /// both want it, and it is the element built around paying that price.</summary>
            public const float EarthStillShare = 0.5f;

            /// <summary>Fire's average heat in a fight: five held between releases, rebuilt after.</summary>
            public const float FireStacks = 4.5f;

            /// <summary>Share of a fight Fire spends at two or more stacks (its attack-speed step).</summary>
            public const float FireHasteUptime = 0.95f;

            /// <summary>Air's average momentum: built by running, spent by every Gust.</summary>
            public const float AirMomentum = 0.5f;

            /// <summary>Air's average consecutive-hit streak (its crit floor climbs 4% a hit, to 8).</summary>
            public const float AirStreak = 6f;

            /// <summary>Seconds of fighting between full releases at base meter growth. Element
            /// Growth shortens it for the three meters that fill at a rate; Fire's ladder fills
            /// per swing and is not sped up by it.</summary>
            public const float ReleaseEvery = 15f;

            /// <summary>Share of an Erupt pool's life one target spends standing in it.</summary>
            public const float FireTargetInPool = 0.4f;

            /// <summary>Seconds of sustain counted into effective health. Lifesteal and self-bleed are
            /// rates; ten seconds is about how long a bad exchange in a crowd lasts.</summary>
            public const float SustainWindow = 10f;

            /// <summary>Share of hits that land inside Reactive Plate's window from the last one.</summary>
            public const float ReactiveUptime = 0.5f;

            /// <summary>Share of hits that land inside Ghostwalk's window after the last one.</summary>
            public const float GhostwalkShare = 0.2f;

            /// <summary>Hits a second the player takes in a fight - what a ward renewing on a clock
            /// (Aegis) is measured against.</summary>
            public const float HitsTakenPerSecond = 0.6f;

            /// <summary>Share of a fight spent having stood still for a second (Stonestance), for
            /// every element but Earth, which uses <see cref="EarthStillShare"/>.</summary>
            public const float StillAfterSecondShare = 0.35f;

            /// <summary>Share of a fight spent with the meter completely full (Burning Wick).</summary>
            public const float MeterFullShare = 0.3f;

            /// <summary>A floor's length in seconds, for costs charged per floor (Toll).</summary>
            public const float FloorSeconds = 45f;

            /// <summary>The most a careful player gives up in effective health for one pair.</summary>
            public const float SensibleMaxSurvivalLoss = 0.2f;

            /// <summary>A careful player never takes a pair that leaves them dying to fewer ordinary
            /// hits than this, unless they are already there.</summary>
            public const float SensibleMinHits = 3f;

            /// <summary>Floors ahead a careful player prices Withering over - it costs nothing the
            /// moment it is taken and everything later.</summary>
            public const int WitheringHorizon = 10;

            // ---- the mastery board's rules (AssayBoard) ----

            /// <summary>Share of hits landing on a target already hit five times running (Vitriol's
            /// full bite) - the single target this model fights is struck over and over.</summary>
            public const float SameTargetShare = 0.8f;

            /// <summary>Share of a fight spent at full flow (Circulation) - hits rarely stop a second.</summary>
            public const float FlowUptime = 0.8f;

            /// <summary>Share of hits taken at full health (Alum).</summary>
            public const float FullHealthShare = 0.2f;

            /// <summary>Share of the hits taken that come from the body being fought (soaked by Phlegm,
            /// staggered by Melancholy) rather than another.</summary>
            public const float FromTargetShare = 0.5f;

            /// <summary>Share of a fight a target spends slowed by Aqua Regia alone (hits at the edge
            /// of reach) - the floor of Inceration's uptime.</summary>
            public const float EdgeSlowUptime = 0.3f;
        }

        // ------------------------------------------------------------------ the element

        /// <summary>
        /// The element numbers the model reads - a COPY of Tuning.Elements, so a calibration can try
        /// values from eval without a recompile per guess. Every field starts as Tuning's value; a
        /// calibrated number is written back into Tuning, and <see cref="ElementNumbers.Reset"/>
        /// (or any domain reload) puts the copy back in step. <see cref="ElementNumbers.Drift"/>
        /// names any field still differing from Tuning.
        /// </summary>
        public static class ElementNumbers
        {
            public static float FireStackDamagePoints, FireHastePoints, FirePoolHitUnitsPerSecond;
            public static float WaterDamagePoints, WaterSurgePoints, WaterBurstHitUnits;
            public static float EarthDamagePoints, EarthAttackSpeedPoints, EarthShockHitUnits, EarthShockHitUnitsPerCharge;
            public static float AirDamagePoints, AirAttackSpeedPoints, AirAttackSpeedPointsPerMomentum,
                                AirCritDamagePoints, AirGustDamagePoints, AirMoveSpeedPointsPerMomentum;

            static ElementNumbers() => Reset();

            public static void Reset()
            {
                FireStackDamagePoints = Tuning.Elements.Fire.StackDamagePoints;
                FireHastePoints = Tuning.Elements.Fire.HastePoints;
                FirePoolHitUnitsPerSecond = Tuning.Elements.Fire.PoolHitUnitsPerSecond;
                WaterDamagePoints = Tuning.Elements.Water.DamagePoints;
                WaterSurgePoints = Tuning.Elements.Water.SurgePoints;
                WaterBurstHitUnits = Tuning.Elements.Water.BurstHitUnits;
                EarthDamagePoints = Tuning.Elements.Earth.DamagePoints;
                EarthAttackSpeedPoints = Tuning.Elements.Earth.AttackSpeedPoints;
                EarthShockHitUnits = Tuning.Elements.Earth.ShockHitUnits;
                EarthShockHitUnitsPerCharge = Tuning.Elements.Earth.ShockHitUnitsPerCharge;
                AirDamagePoints = Tuning.Elements.Air.DamagePoints;
                AirAttackSpeedPoints = Tuning.Elements.Air.AttackSpeedPoints;
                AirAttackSpeedPointsPerMomentum = Tuning.Elements.Air.AttackSpeedPointsPerMomentum;
                AirCritDamagePoints = Tuning.Elements.Air.CritDamagePoints;
                AirGustDamagePoints = Tuning.Elements.Air.GustDamagePoints;
                AirMoveSpeedPointsPerMomentum = Tuning.Elements.Air.MoveSpeedPointsPerMomentum;
                Invalidate();
            }

            /// <summary>Every field that no longer matches Tuning, or "none".</summary>
            public static string Drift()
            {
                var off = new System.Collections.Generic.List<string>();
                void Check(string name, float mine, float tuning) { if (!Mathf.Approximately(mine, tuning)) off.Add($"{name} {mine} (Tuning {tuning})"); }
                Check("FireStackDamagePoints", FireStackDamagePoints, Tuning.Elements.Fire.StackDamagePoints);
                Check("FireHastePoints", FireHastePoints, Tuning.Elements.Fire.HastePoints);
                Check("FirePoolHitUnitsPerSecond", FirePoolHitUnitsPerSecond, Tuning.Elements.Fire.PoolHitUnitsPerSecond);
                Check("WaterDamagePoints", WaterDamagePoints, Tuning.Elements.Water.DamagePoints);
                Check("WaterSurgePoints", WaterSurgePoints, Tuning.Elements.Water.SurgePoints);
                Check("WaterBurstHitUnits", WaterBurstHitUnits, Tuning.Elements.Water.BurstHitUnits);
                Check("EarthDamagePoints", EarthDamagePoints, Tuning.Elements.Earth.DamagePoints);
                Check("EarthAttackSpeedPoints", EarthAttackSpeedPoints, Tuning.Elements.Earth.AttackSpeedPoints);
                Check("EarthShockHitUnits", EarthShockHitUnits, Tuning.Elements.Earth.ShockHitUnits);
                Check("EarthShockHitUnitsPerCharge", EarthShockHitUnitsPerCharge, Tuning.Elements.Earth.ShockHitUnitsPerCharge);
                Check("AirDamagePoints", AirDamagePoints, Tuning.Elements.Air.DamagePoints);
                Check("AirAttackSpeedPoints", AirAttackSpeedPoints, Tuning.Elements.Air.AttackSpeedPoints);
                Check("AirAttackSpeedPointsPerMomentum", AirAttackSpeedPointsPerMomentum, Tuning.Elements.Air.AttackSpeedPointsPerMomentum);
                Check("AirCritDamagePoints", AirCritDamagePoints, Tuning.Elements.Air.CritDamagePoints);
                Check("AirGustDamagePoints", AirGustDamagePoints, Tuning.Elements.Air.GustDamagePoints);
                Check("AirMoveSpeedPointsPerMomentum", AirMoveSpeedPointsPerMomentum, Tuning.Elements.Air.MoveSpeedPointsPerMomentum);
                return off.Count == 0 ? "none" : string.Join("; ", off);
            }
        }

        /// <summary>
        /// An element's standing effect at the states Assume describes. Its passives are HEAD
        /// STARTS in stat points (Tuning.Elements), joined to the character's own before the curve
        /// bends them, exactly as PlayerController's *PointsNow do. Releases are in HIT UNITS.
        /// </summary>
        public struct ElementProfile
        {
            public float DamagePoints, AttackSpeedPoints, MoveSpeedPoints, CritDamagePoints;
            /// <summary>Air's Gust damage points at their uptime - before Elemental Power.</summary>
            public float GustDamagePoints;
            public float CritAdd, Dr, BaseHp;
            /// <summary>Multiplier on everything the player deals, from Water's soak.</summary>
            public float Vulnerability;
            public float LifestealAdd;
            /// <summary>One release on one target, in hit units, before Elemental Power.</summary>
            public float ReleaseHitUnits;
            public float ReleasesPerSecond;
        }

        /// <summary>True while <see cref="ReleaseSeconds"/> measures a build WITHOUT what its
        /// releases bring - burst, pools, shocks, surge, soak, gust.</summary>
        static bool _releasesOff;

        public static ElementProfile Profile(ElementType? element, float gainRatePoints, Func<string, int> n,
                                             Build b = null)
        {
            var p = Profile(element, gainRatePoints, n, _releasesOff, b);
            return p;
        }

        static ElementProfile Profile(ElementType? element, float gainRatePoints, Func<string, int> n, bool releasesOff,
                                      Build b = null)
        {
            var p = new ElementProfile { BaseHp = Tuning.Player.HpFireWater, Vulnerability = 1f };
            if (element == null) return p;

            float gain = StatPercents.Apply(1f, gainRatePoints);
            float refund = 1f + Tuning.Exchange.OverflowRefund * Mathf.Min(1, n("overflow"));   // Overflow hands part of the meter back
            if (Owns(b, Progression.Notable.Alkahest)) refund /= 1f - Tuning.Board.AlkahestRefund;
            float every = Assume.ReleaseEvery / (element == ElementType.Fire ? 1f : gain) / refund;

            switch (element.Value)
            {
                case ElementType.Fire:
                {
                    p.DamagePoints = ElementNumbers.FireStackDamagePoints * Assume.FireStacks;
                    p.AttackSpeedPoints = ElementNumbers.FireHastePoints * Assume.FireHasteUptime;
                    // Erupt: area denial - one target stands in one pool for part of its life.
                    p.ReleaseHitUnits = ElementNumbers.FirePoolHitUnitsPerSecond * Tuning.Elements.Fire.PoolSeconds
                                      * Assume.FireTargetInPool;
                    break;
                }

                case ElementType.Water:
                    p.DamagePoints = ElementNumbers.WaterDamagePoints;
                    p.AttackSpeedPoints = ElementNumbers.WaterSurgePoints
                                        * Mathf.Min(1f, Tuning.Elements.Water.SurgeSecondsTier3 / every);
                    p.Vulnerability = 1f + (Tuning.Elements.Water.SoakVulnerability - 1f)
                                         * Mathf.Min(1f, Tuning.Elements.Water.SoakSeconds / every);
                    p.ReleaseHitUnits = ElementNumbers.WaterBurstHitUnits;   // one target takes the pool whole
                    break;

                case ElementType.Earth:
                {
                    p.DamagePoints = ElementNumbers.EarthDamagePoints;
                    p.AttackSpeedPoints = ElementNumbers.EarthAttackSpeedPoints;
                    p.Dr = Tuning.Elements.Earth.StillDamageReduction * Assume.EarthStillShare
                         + Tuning.Elements.Earth.MovingDamageReduction * (1f - Assume.EarthStillShare);
                    p.BaseHp = Tuning.Player.HpEarth;
                    // A full charge: the quake and floor(5 / 1.5) = 3 aftershocks at 0.55, 0.47, 0.39.
                    float shocks = 1f;
                    int after = Mathf.FloorToInt(Tuning.Elements.Earth.ChargeSeconds / Tuning.Elements.Earth.AftershockPerSeconds);
                    for (int i = 0; i < after; i++) shocks += 0.55f - i * 0.08f;
                    p.ReleaseHitUnits = (ElementNumbers.EarthShockHitUnits + ElementNumbers.EarthShockHitUnitsPerCharge) * shocks;
                    break;
                }

                default:   // Air
                {
                    float gust = Mathf.Min(1f, Tuning.Elements.Air.GustSeconds / every);
                    p.DamagePoints = ElementNumbers.AirDamagePoints;
                    p.GustDamagePoints = ElementNumbers.AirGustDamagePoints * gust;
                    p.AttackSpeedPoints = ElementNumbers.AirAttackSpeedPoints
                                        + ElementNumbers.AirAttackSpeedPointsPerMomentum * Assume.AirMomentum;
                    p.MoveSpeedPoints = ElementNumbers.AirMoveSpeedPointsPerMomentum * Assume.AirMomentum
                                      + Tuning.Elements.Air.GustMoveSpeedPoints * gust;
                    p.CritDamagePoints = ElementNumbers.AirCritDamagePoints;
                    p.CritAdd = Mathf.Min(0.75f, Tuning.Elements.Air.BaseCrit
                        + Tuning.Elements.Air.CritPerStreak * Mathf.Min(Tuning.Elements.Air.StreakCap, Assume.AirStreak));
                    p.BaseHp = Tuning.Player.HpAir;
                    // Gust deals nothing itself - it is damage, range, legs and lifesteal for six seconds.
                    p.LifestealAdd = Tuning.Elements.Air.GustLifesteal * gust;
                    break;
                }
            }

            p.ReleasesPerSecond = 1f / every;
            if (releasesOff)
            {
                // Everything a release brings, gone - the passives stay.
                p.ReleaseHitUnits = 0f;
                p.GustDamagePoints = 0f;
                p.LifestealAdd = 0f;
                p.Vulnerability = 1f;
                if (element == ElementType.Water) p.AttackSpeedPoints = 0f;   // the surge
                if (element == ElementType.Air)
                    p.MoveSpeedPoints = ElementNumbers.AirMoveSpeedPointsPerMomentum * Assume.AirMomentum;
            }
            return p;
        }

        /// <summary>
        /// What one release is worth, in seconds of the player's own damage: the damage a release
        /// cycle adds - its hits, and Water's surge and soak, and Air's Gust - over the damage the
        /// player deals without it. The same measure for every element, however its release works.
        /// </summary>
        public static float ReleaseSeconds(Build b)
        {
            if (b.Element == null) return 0f;
            var none = new Mods();
            var on = Evaluate(b, none, _ => 0, 1);
            _releasesOff = true;
            Worth off;
            try { off = Evaluate(b, none, _ => 0, 1); }
            finally { _releasesOff = false; }
            float gain = StatCurves.Character(StatKind.ElementGrowth, b.GainRatePoints);
            float every = 1f / Mathf.Max(1e-4f, Profile(b.Element, gain, _ => 0, false, b).ReleasesPerSecond);
            return (on.Dps - off.Dps) / Mathf.Max(1e-3f, off.Dps) * every;
        }

        // ------------------------------------------------------------------ the floor

        /// <summary>An ordinary Chaser's hit on this floor - the unit survival is counted in.</summary>
        public static float ChaserHit(int floor)
            => Enemies.EnemyTypes.Of(Enemies.EnemyKind.Chaser).Damage * Enemies.FloorDifficulty.Damage(floor);

        /// <summary>An ordinary Chaser's effective health (health + armour) on this floor.</summary>
        public static float ChaserEhp(int floor) => Enemies.WaveComposer.Ehp(Enemies.EnemyKind.Chaser, false, floor);

        // ------------------------------------------------------------------ evaluation

        public static Worth Evaluate(Build b, Mods m, Func<string, int> n, int floor, float witheringLost = 0f)
        {
            ElementType? element = b.Legacy ? null : b.Element;
            float gainPoints = StatCurves.Character(StatKind.ElementGrowth, b.GainRatePoints);
            var el = Profile(element, gainPoints, n, b);

            // Elemental Power, bent on its own - releases and Gust's damage scale by it. Twin Spark's
            // repeat never fires (every release empties the meter the repeat needs), so what it
            // actually does is scale the one release to 60%.
            float scale = StatPercents.Apply(1f, StatCurves.Character(StatKind.ElementalEffectiveness, b.Stats.ElementalEffectiveness))
                        * m.ReleaseMul;

            // The element's head starts join the character's own points BEFORE the curve.
            var raw = new StatPercents();
            raw.Add(b.Stats);
            raw.Damage += el.DamagePoints + el.GustDamagePoints * scale;
            raw.AttackSpeed += el.AttackSpeedPoints;
            // The board's conditional points join the same way (Tartar, Exaltation, Circulation).
            var (boardDamage, boardSpeed) = BoardPoints(b, el.ReleasesPerSecond);
            raw.Damage += boardDamage;
            raw.AttackSpeed += boardSpeed;
            raw.MoveSpeed += el.MoveSpeedPoints;
            raw.CritDamage += el.CritDamagePoints;
            var s = StatCurves.Character(raw);

            float chain = ChainDps(b, s, m, n, el, b.Legacy, out float hitUnit, out float hitsPerSecond,
                                   out var shape);
            float releaseHit = el.ReleaseHitUnits * hitUnit * scale;
            float dps = chain;
            if (!b.Legacy)
            {
                dps *= Mathf.Max(el.Vulnerability, BoardVulnerability(b, shape));
                dps += releaseHit * el.ReleasesPerSecond;
                dps *= BoardOffense(b, shape);
                dps += BoardDamageOverTime(b, shape);
                dps *= Judged.Offense(n);
            }

            float maxHp = Mathf.Max(1f, (StatPercents.Apply(el.BaseHp, s.MaxHp) + b.GridHp) * m.MaxHpMul - witheringLost);
            float moving = b.Element == ElementType.Earth ? 1f - Assume.EarthStillShare : Assume.MovingShare;
            // Stat mitigation floored as one, as PlayerController floors Health.Vulnerability; the
            // ledger's conditional reductions (RunEffects.ModifyIncoming) land after the floor.
            float incoming = StatCurves.Incoming(StatPercents.ReductionFactor(s.Resilience)
                                                 * (moving * StatPercents.ReductionFactor(s.Graze)
                                                    + (1f - moving) * StatPercents.ReductionFactor(s.Brace))
                                                 * (1f - el.Dr)
                                                 * Mathf.Max(0.25f, m.DamageTakenMul))
                           * LedgerDefense(n, b.Element) * BoardDefense(b);

            float hit = ChaserHit(floor);
            // Fixation: no hit takes more than its share of max health, however hard it lands.
            if (Owns(b, Progression.Notable.Fixation) && hit * incoming > maxHp * Tuning.Board.FixationMaxHitFraction)
                incoming = maxHp * Tuning.Board.FixationMaxHitFraction / hit;
            float ehp = maxHp / Mathf.Max(0.01f, incoming);
            if (n("second_wind") > 0) ehp += hit;      // one lethal hit survived per floor
            if (n("open_stance") > 0) ehp -= hit * Tuning.Exchange.OpenStanceHits;   // the floor's first hits land twice
            if (n("slow_knit") >= 3) ehp *= Tuning.Exchange.HollowCeiling;   // Hollow: fought from the ceiling
            ehp *= Judged.Survival(n);

            // Lifesteal as one capped pool, healing no faster than PlayerController.Drain allows.
            float lifesteal = StatCurves.Lifesteal(b.Lifesteal + m.BonusLifesteal + el.LifestealAdd);
            float heal = Mathf.Min(lifesteal * dps, StatCurves.LifestealPerSecond(maxHp))
                       * m.HealMul
                       + BoardHealing(b, dps, maxHp, floor);
            // Cibation: lifesteal over the limit becomes a shield - health the next hits spend.
            ehp += BoardShield(b, lifesteal * dps, maxHp) / Mathf.Max(0.01f, incoming);
            float bleed = maxHp * Tuning.Exchange.BloodPriceSwing * n("blood_price") * hitsPerSecond
                        + maxHp * Tuning.Exchange.BackfireFraction * n("backfire") * el.ReleasesPerSecond
                        + Mathf.Min(0.9f, Tuning.Exchange.TollFraction * n("toll")) * maxHp / Assume.FloorSeconds;

            return new Worth
            {
                Dps = dps, ChainDps = chain, ReleaseHit = releaseHit,
                MaxHp = maxHp, Incoming = incoming, Ehp = Mathf.Max(1f, ehp), Sustain = heal - bleed,
            };
        }

        /// <summary>
        /// The weapon's chain: the Medium chain (two basics and a 5x weapon art over 3.5 basic
        /// intervals plus the timing bar's wind-up, landed GOOD), with the stat block (the
        /// element's head starts already bent into it) and the ledger folded in as PlayerController
        /// folds them.
        ///
        /// <paramref name="legacyTempo"/> leaves out Tuning.Attack.GlobalTempo, which PlayerPower
        /// never applied - the game swings at 0.42 / 0.8 = 0.525 s between basics, PlayerPower
        /// priced 0.42 s. Kept only so the regression check can replay PlayerPower exactly.
        /// </summary>
        static float ChainDps(Build b, StatPercents s, Mods m, Func<string, int> n, ElementProfile el, bool legacyTempo,
                              out float hitUnit, out float hitsPerSecond, out ChainShape shape)
        {
            int basics = Mathf.Max(1, 2 + m.BasicsPerChainDelta
                                      + (Owns(b, Progression.Notable.Multiplication) ? -1 : 0));
            float tempo = legacyTempo ? 1f : Tuning.Attack.GlobalTempo;
            float speed = StatPercents.Apply(1f, s.AttackSpeed) * Mathf.Max(0.35f, m.AttackSpeedMul) * tempo;
            float interval = Tuning.Attack.BaseInterval / speed;

            // PlayerController.HitUnit: the basic hit before crit and the damage roll.
            hitUnit = (Tuning.Player.BaseDamage + m.BonusDamage) * StatPercents.Apply(1f, s.Damage);

            // The damage range's average: Accuracy lifts its bottom, worth up to +DamageSpread.
            float accuracy = 1f + Tuning.Attack.DamageSpread * Mathf.Clamp01(s.Accuracy / 100f);
            float basic = hitUnit * accuracy;
            float finisher = basic * Tuning.Finisher.DamageMedium
                           * Mathf.Max(0.25f, m.FinisherDamageMul) * StatPercents.Apply(1f, s.FinisherPower);
            float seconds = (basics + Tuning.Finisher.LockMedium * m.LockMul) * interval;

            float windUp = Tuning.StrikeTiming.BarSecondsFor(Combat.FinisherWeight.Medium);   // the finisher priced
            finisher *= Combat.StrikeJudge.Parity(basics * basic, finisher, seconds, windUp)
                        * Tuning.StrikeTiming.GoodMultiplier;
            seconds += windUp;

            // The board: Cohobation steeps the art with every basic before it; Realgar quickens the
            // basics after it.
            if (Owns(b, Progression.Notable.Cohobation))
                finisher *= 1f + Mathf.Min(Tuning.Board.CohobationMax, Tuning.Board.CohobationPerBasic * basics);
            if (Owns(b, Progression.Notable.Realgar))
                seconds -= Mathf.Min(basics, Tuning.Board.RealgarBasics) * interval * (1f - 1f / Tuning.Board.RealgarSpeedMul);

            // One pool, one cap, overflow into the multiplier - PlayerController.RollCrit's rule.
            var (chance, poolMul) = StatCurves.Crit(Tuning.Player.BaseCritChance + el.CritAdd + s.CritChance / 100f + m.BonusCrit,
                                                    Tuning.Player.BaseCritMultiplier + s.CritDamage / 100f);
            float critMul = Player.PlayerPower.ColdIron(poolMul + m.CritDamageAdd, n("cold_iron"));
            float critFactor = 1f + chance * (critMul - 1f);

            float dps = (basics * basic + finisher) / seconds * critFactor;

            hitsPerSecond = (basics + 1) / seconds;
            shape = new ChainShape
            {
                Seconds = seconds, Basics = basics, BasicHit = basic * critFactor, ArtHit = finisher * critFactor,
                CritChance = chance, CritMul = critMul, CritFactor = critFactor, HitsPerSecond = hitsPerSecond,
            };
            return dps * Conditionals(n);
        }

        /// <summary>
        /// PlayerPower's own estimates for the conditional entries that move damage, unchanged so a
        /// legacy build replays it exactly. Further offense the Assay judges lives in
        /// <see cref="Judged"/>.
        /// </summary>
        static float Conditionals(Func<string, int> n)
        {
            float mul = 1f;
            int ex = n("executioner");
            if (ex > 0) mul /= 0.7f + 0.3f / (1f + Tuning.Exchange.ExecutionerDamage / 100f * ex);
            int rei = n("reiteration");
            if (rei > 0) mul *= 1f + Tuning.Exchange.ReiterationFraction / Mathf.Max(2, Tuning.Exchange.ReiterationEvery - (rei - 1));
            mul *= Mathf.Max(0.1f, 1f - Tuning.Exchange.FumblerChance * n("fumbler"));
            mul *= Mathf.Max(0.1f, 1f - 0.036f * n("souring"));
            return mul;
        }

        /// <summary>Incoming-damage factors from the conditional half of the ledger (RunEffects),
        /// at the uptimes Assume states.</summary>
        static float LedgerDefense(Func<string, int> n, ElementType? element)
        {
            float f = 1f;
            int reactive = n("reactive_plate");
            if (reactive > 0)
                f *= 1f - (reactive >= 2 ? Tuning.Exchange.ReactivePlateReductionII : Tuning.Exchange.ReactivePlateReduction)
                        * Assume.ReactiveUptime;

            // Lapis: the hit after a second planted deals half.
            if (n("stonestance") >= 3)
            {
                float still = element == ElementType.Earth ? Assume.EarthStillShare : Assume.StillAfterSecondShare;
                f *= 1f - (1f - Tuning.Exchange.LapisMul) * still * 0.5f;
            }

            // Aegis: a ward that cancels one hit every renewal.
            int aegis = n("aegis_cycle");
            if (aegis > 0)
            {
                float renew = aegis >= 2 ? Tuning.Exchange.AegisRenewSecondsII : Tuning.Exchange.AegisRenewSeconds;
                f *= 1f - Mathf.Min(0.5f, 1f / (renew * Assume.HitsTakenPerSecond));
            }

            if (n("ghostwalk") > 0) f *= 1f - Assume.GhostwalkShare;
            return f;
        }

        /// <summary>
        /// Entries whose worth is a matter of judgement rather than arithmetic: costs that tax
        /// movement, reach or readability, and the boons that buy them back. Each factor is a
        /// stated guess at what the entry does to a fight - argue with the numbers here, nowhere
        /// else. Inert entries are deliberately absent: they do nothing in the game either.
        /// </summary>
        public static class Judged
        {
            public static float Offense(Func<string, int> n)
            {
                float f = 1f;
                f *= 1f - 0.04f * n("short_arm");
                f *= 1f - 0.03f * n("wandering_eye");
                f *= 1f - 0.02f * n("overcommitted");
                f *= n("fog") > 0 ? 0.98f : 1f;
                f *= n("rooted") > 0 ? 0.95f : 1f;
                f *= n("dead_weight") > 0 ? 0.98f : 1f;
                f *= 1f + 0.02f * n("long_reach");
                f *= 1f + 0.02f * n("wide_arc");
                f *= n("first_blood") > 0 ? 1.03f : 1f;
                f *= 1f + 0.02f * n("ouroboros");
                return f;
            }

            public static float Survival(Func<string, int> n)
            {
                float f = 1f;
                f *= Mathf.Max(0.5f, 1f - 0.06f * n("anchored"));
                f *= n("rooted") > 0 ? 0.88f : 1f;
                f *= n("drag") > 0 ? 0.96f : 1f;
                f *= 1f - 0.08f * n("retrograde");
                f *= 1f - 0.02f * n("dead_weight");
                f *= 1f - 0.03f * n("overcommitted");
                f *= n("fog") > 0 ? 0.97f : 1f;
                f *= 1f - 0.04f * n("rust");
                f *= 1f - 0.01f * n("wandering_eye");
                f *= 1f + 0.04f * n("fleetfoot");
                f *= 1f + 0.02f * n("eagle");
                f *= n("kiln_fired") > 0 ? 1.05f : 1f;
                f *= 1f + 0.02f * n("long_reach");
                return f;
            }
        }
    }
}
