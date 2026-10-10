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
        ///
        /// Sustain is HEALTH a second while Ehp is the ENEMY DAMAGE it takes to die, so it is turned
        /// into the same units first - divided by <see cref="Incoming"/>. Added raw, every heal and
        /// every price paid in health counted for a third to a half of what it is (2026-10-10).
        /// </summary>
        public float EffectiveHp
        {
            get
            {
                float sustain = Assay.Assume.SustainWindow * Sustain / Mathf.Max(0.01f, Incoming);
                return Mathf.Max(1f, Ehp + Mathf.Min(Ehp, Mathf.Max(0f, sustain)) + Mathf.Min(0f, sustain));
            }
        }
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

            /// <summary>Hits one Ghostwalk window turns: it opens on a hit, in a crowd that is
            /// often swinging again.</summary>
            public const float GhostwalkHits = 0.9f;

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

            // ---- the exchange ledger's conditional half (AssayExchange) ----

            /// <summary>Share of weapon arts the deep player lands PERFECT (Green Lion). The model
            /// prices the bar at GOOD everywhere else; this is the one place perfect play pays.</summary>
            public const float PerfectShare = 0.5f;

            /// <summary>Share of hits landing in the outer quarter of the reach (Far Strike).</summary>
            public const float OuterReachShare = 0.3f;

            /// <summary>Share of hits landing within half the reach (Close Quarters); the rest land
            /// beyond it (Cramped).</summary>
            public const float HalfReachShare = 0.5f;

            /// <summary>Share of basics thrown after a second at full speed (Wake).</summary>
            public const float WakeShare = 0.2f;

            /// <summary>Share of a fight spent under half health (Fury of the Frail, Glass Bones,
            /// Haemorrhage).</summary>
            public const float LowHealthShare = 0.25f;

            /// <summary>Share of hits taken under 30% health (Fortitude).</summary>
            public const float CriticalHealthShare = 0.12f;

            /// <summary>Health missing on average, as a share of max (Bloodstone).</summary>
            public const float MissingHealth = 0.3f;

            /// <summary>Share of a fight inside a trap boon's stack-II bonus: floors with the
            /// element's own hazard, times the time spent at it.</summary>
            public const float TrapUptime = 0.08f;

            /// <summary>Share of damage taken from the element's own hazard (a trap boon's stack I).</summary>
            public const float HazardShare = 0.05f;

            /// <summary>Other enemies close to the body being fought - what a burst, a splash or a
            /// cleave also reaches.</summary>
            public const float CrowdBodies = 1.5f;

            /// <summary>What damage on those other bodies is worth against damage on the one being
            /// fought. The model is single-target; crowd entries are credited at this rate.</summary>
            public const float CrowdCredit = 0.5f;

            /// <summary>Share of the hits aimed at the player that the defensive ability turns -
            /// what Encumbered and Shackled take a cut of.</summary>
            public const float DefenseShare = 0.2f;

            /// <summary>Share of hits taken after two seconds standing still (Restless, Quicksand),
            /// for every element but Earth, which stands more.</summary>
            public const float StillTwoSecondsShare = 0.2f;
            public const float EarthStillTwoSecondsShare = 0.4f;

            /// <summary>Share of moving time spent at full speed (Lightfoot).</summary>
            public const float FullSpeedShare = 0.6f;

            /// <summary>Share of floors with a captured spire boon running (Lodestone),
            /// and what one is worth while it runs.</summary>
            public const float SpireFloorShare = 0.15f;
            public const float SpireWorth = 0.2f;

            /// <summary>Fire's average heat when its ledger moves how long heat lasts: +0.5 stacks
            /// per 30 run Element Growth points, between 2 and 5.</summary>
            public const float FireStacksPerGrowth = 0.5f / 30f;

            /// <summary>Share of kills that are not elites (Coup de Grace).</summary>
            public const float NonEliteShare = 0.85f;

            /// <summary>Share of healing that lands above half health (Anaemia halves it).</summary>
            public const float HealAboveHalfShare = 0.6f;

            /// <summary>Floors between transmutation circles for a player holding a Nigredo - the
            /// mean gap between Rifts (FloorPlanner.Simulate).</summary>
            public const int CircleEveryFloors = 6;

            // ---- the Nigredos' exposures: share of hits taken inside each window

            /// <summary>Shackled: the defensive ability recharging (used near its cooldown, which
            /// Encumbered III has doubled).</summary>
            public const float DefenseRechargeShare = 0.8f;

            /// <summary>Barren: the meter under half (Stubborn Ore III fills it slowly).</summary>
            public const float MeterUnderHalfShare = 0.55f;

            /// <summary>Wet Ash: Fire under three heat, with Fuel gone.</summary>
            public const float FireLowHeatShare = 0.3f;

            /// <summary>Ebb: Water outside its surge.</summary>
            public const float WaterOffSurgeShare = 0.5f;

            /// <summary>Doldrums: Air under half momentum.</summary>
            public const float AirLowMomentumShare = 0.5f;

            /// <summary>Corrosion: armour condition under Rust III with repairs halved - the wear
            /// penalty it puts outside the floor is (1 - this) x Durability's +50%.</summary>
            public const float RustedCondition = 0.4f;

            /// <summary>Share of a wave's effective health that is elites (Coagulation).</summary>
            public const float EliteShare = 0.25f;
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
                                             Build b = null, Mods m = null)
        {
            var p = Profile(element, gainRatePoints, n, _releasesOff, b, m);
            return p;
        }

        /// <param name="m">The ledger, for its element entries and its Element Growth - null for a
        /// legacy build, which replays PlayerPower and so must not see them.</param>
        static ElementProfile Profile(ElementType? element, float gainRatePoints, Func<string, int> n, bool releasesOff,
                                      Build b = null, Mods m = null)
        {
            var p = new ElementProfile { BaseHp = Tuning.Player.HpFireWater, Vulnerability = 1f };
            if (element == null) return p;
            bool ledger = m != null;

            // The ledger's Element Growth (Rich Vein, Stubborn Ore) multiplies the character's.
            float gain = StatPercents.Apply(1f, gainRatePoints) * (ledger ? m.GainMul : 1f);
            float refund = 1f + Tuning.Exchange.OverflowRefund * Mathf.Min(1, n("overflow"));   // Overflow hands part of the meter back
            if (Owns(b, Progression.Notable.Alkahest)) refund /= 1f - Tuning.Board.AlkahestRefund;
            float every = Assume.ReleaseEvery / (element == ElementType.Fire ? 1f : gain) / refund;

            switch (element.Value)
            {
                case ElementType.Fire:
                {
                    float perStack = ElementNumbers.FireStackDamagePoints, stacks = Assume.FireStacks;
                    if (ledger)
                    {
                        // Smother takes from every stack, Phlogiston doubles it (RunEffects.HeatPoints).
                        perStack = Mathf.Max(0f, perStack - Tuning.Exchange.SmotherPoints * n("smother"));
                        if (n("phlogiston") > 0) perStack *= Tuning.Exchange.PhlogistonMul;
                        // Fire reads Element Growth as heat lasting longer.
                        stacks += Assume.FireStacksPerGrowth * m.Points(StatKind.ElementGrowth);
                        // Banked Embers: the rebuild after a release starts from the floor, not zero.
                        stacks += 0.3f * n("banked_embers") + (AtMax(n, "banked_embers") ? 0.4f : 0f);
                        stacks -= 0.25f * n("leaky_vessel");   // heat fades faster
                        if (AtMax(n, "smother")) stacks -= 1f;     // Wet Ash: no Fuel
                        stacks = Mathf.Clamp(stacks, 2f, 5f);
                    }
                    p.DamagePoints = perStack * stacks;
                    p.AttackSpeedPoints = ElementNumbers.FireHastePoints * Assume.FireHasteUptime;
                    // Erupt: area denial - one target stands in one pool for part of its life.
                    p.ReleaseHitUnits = ElementNumbers.FirePoolHitUnitsPerSecond * Tuning.Elements.Fire.PoolSeconds
                                      * Assume.FireTargetInPool;
                    break;
                }

                case ElementType.Water:
                {
                    float surge = Tuning.Elements.Water.SurgeSecondsTier3, surgePoints = ElementNumbers.WaterSurgePoints;
                    float soakAdd = 0f;
                    if (ledger)
                    {
                        surge += Tuning.Exchange.HighTideSeconds * n("high_tide");
                        surgePoints = Mathf.Max(0f, surgePoints - Tuning.Exchange.LowWaterSurge * n("low_water"));
                        if (n("flood") > 0) soakAdd = Tuning.Exchange.FloodVulnerability;
                    }
                    p.DamagePoints = ElementNumbers.WaterDamagePoints;
                    p.AttackSpeedPoints = surgePoints * Mathf.Min(1f, surge / every);
                    p.Vulnerability = 1f + (Tuning.Elements.Water.SoakVulnerability - 1f + soakAdd)
                                         * Mathf.Min(1f, Tuning.Elements.Water.SoakSeconds / every);
                    p.ReleaseHitUnits = ElementNumbers.WaterBurstHitUnits;   // one target takes the pool whole
                    break;
                }

                case ElementType.Earth:
                {
                    p.DamagePoints = ElementNumbers.EarthDamagePoints;
                    p.AttackSpeedPoints = ElementNumbers.EarthAttackSpeedPoints;
                    // Bedrock holds the planted reduction a second after moving.
                    float planted = Assume.EarthStillShare + (ledger && AtMax(n, "deep_roots") ? 0.1f : 0f);
                    p.Dr = Tuning.Elements.Earth.StillDamageReduction * planted
                         + Tuning.Elements.Earth.MovingDamageReduction * (1f - planted);
                    p.BaseHp = Tuning.Player.HpEarth;
                    // A full charge: the quake and floor(5 / 1.5) = 3 aftershocks at 0.55, 0.47, 0.39.
                    float shocks = 1f;
                    int after = Mathf.FloorToInt(Tuning.Elements.Earth.ChargeSeconds / Tuning.Elements.Earth.AftershockPerSeconds);
                    for (int i = 0; i < after; i++) shocks += 0.55f - i * 0.08f;
                    p.ReleaseHitUnits = (ElementNumbers.EarthShockHitUnits + ElementNumbers.EarthShockHitUnitsPerCharge) * shocks
                                      * (ledger ? 1f - 0.08f * n("leaky_vessel") : 1f);   // the charge bleeds faster
                    break;
                }

                default:   // Air
                {
                    float gust = Mathf.Min(1f, Tuning.Elements.Air.GustSeconds / every);
                    float momentum = Assume.AirMomentum, streak = Assume.AirStreak, perStreak = Tuning.Elements.Air.CritPerStreak;
                    if (ledger)
                    {
                        momentum = Mathf.Clamp01(momentum + 0.1f * n("tailwind") - 0.07f * n("leaky_vessel"));
                        if (AtMax(n, "becalmed")) streak = 1.5f;   // Doldrums: a hit taken resets it
                        perStreak = Mathf.Max(0f, perStreak - Tuning.Exchange.BecalmedCritPerHit * n("becalmed"));
                        if (n("gale") > 0) perStreak *= Tuning.Exchange.GaleMul;
                    }
                    p.DamagePoints = ElementNumbers.AirDamagePoints;
                    p.GustDamagePoints = ElementNumbers.AirGustDamagePoints * gust;
                    p.AttackSpeedPoints = ElementNumbers.AirAttackSpeedPoints
                                        + ElementNumbers.AirAttackSpeedPointsPerMomentum * momentum;
                    p.MoveSpeedPoints = ElementNumbers.AirMoveSpeedPointsPerMomentum * momentum
                                      + Tuning.Elements.Air.GustMoveSpeedPoints * gust;
                    p.CritDamagePoints = ElementNumbers.AirCritDamagePoints;
                    p.CritAdd = Mathf.Min(0.75f, Tuning.Elements.Air.BaseCrit
                        + perStreak * Mathf.Min(Tuning.Elements.Air.StreakCap, streak));
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
            // A legacy build replays PlayerPower, which knows nothing of the ledger's conditional
            // half - every term AssayExchange adds is off for it, so its DAMAGE stays identical.
            bool ledger = !b.Legacy;
            float gainPoints = StatCurves.Character(StatKind.ElementGrowth, b.GainRatePoints);
            var el = Profile(element, gainPoints, n, b, ledger ? m : null);

            // Elemental Power, bent on its own - releases and Gust's damage scale by it.
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

            float chain = ChainDps(b, s, m, n, el, b.Legacy, floor, out float hitUnit, out float hitsPerSecond,
                                   out var shape);
            float releaseHit = el.ReleaseHitUnits * hitUnit * scale;
            float releasesPerSecond = el.ReleasesPerSecond;
            float dps = chain;
            if (ledger)
            {
                var rel = LedgerReleases(b, m, n, el, shape, floor, chain);
                releasesPerSecond = rel.PerSecond;
                releaseHit = releaseHit * rel.HitMul + rel.HitUnitsAdd * hitUnit * scale;
                if (element == null) releaseHit = 0f;

                dps *= Mathf.Max(el.Vulnerability, BoardVulnerability(b, shape));
                dps += releaseHit * releasesPerSecond;
                dps *= BoardOffense(b, shape);
                dps += BoardDamageOverTime(b, shape);
                dps += LedgerFieldDps(n, hitUnit);
                dps *= LedgerTimeLost(n, element, shape, releasesPerSecond);
                dps *= LedgerCrowd(m, n, b, shape);
                dps *= Judged.Offense(n);
            }

            float maxHp = Mathf.Max(1f, (StatPercents.Apply(el.BaseHp, s.MaxHp) + b.GridHp) * m.MaxHpMul - witheringLost);
            float moving = b.Element == ElementType.Earth ? 1f - Assume.EarthStillShare : Assume.MovingShare;

            // Graze and Brace: the character's, then the run's as factors of their own (as
            // GameBootstrap's IncomingDamageMultiplier composes them). Lightfoot doubles the
            // character's Graze at full speed.
            float graze = StatPercents.ReductionFactor(s.Graze);
            if (n("lightfoot") > 0)
                graze = Mathf.Lerp(graze, StatPercents.ReductionFactor(s.Graze * Tuning.Exchange.LightfootGrazeMul),
                                   Assume.FullSpeedShare);
            graze *= m.GrazeFactor;
            float brace = StatPercents.ReductionFactor(s.Brace) * m.BraceFactor;

            // Stat mitigation - the ledger's conditional reductions included - floored as ONE, as
            // PlayerController floors Health.Vulnerability; the costs' multipliers land outside it.
            float inside = StatPercents.ReductionFactor(s.Resilience) * m.ResilienceFactor
                         * (moving * graze + (1f - moving) * brace)
                         * (1f - el.Dr)
                         * Mathf.Max(0.25f, m.DamageTakenMul)
                         * LedgerMitigation(n, b.Element, shape);
            float incoming = StatCurves.Incoming(inside, m.MitigationFloor)
                           * m.DamageTakenOutside
                           * LedgerOutside(m, n, b.Element, shape)
                           * (1f - NegatedShare(n, b.Element))
                           * DefensiveAbility(n)
                           * BoardDefense(b);

            float hit = ChaserHit(floor);
            // Fixation: no hit takes more than its share of max health, however hard it lands.
            if (Owns(b, Progression.Notable.Fixation) && hit * incoming > maxHp * Tuning.Board.FixationMaxHitFraction)
                incoming = maxHp * Tuning.Board.FixationMaxHitFraction / hit;
            float ehp = maxHp / Mathf.Max(0.01f, incoming);
            if (n("second_wind") > 0)
            {
                // One lethal hit survived per floor, then the heal - or Phoenix's rise.
                ehp += hit;
                float rise = n("phoenix") > 0 ? Tuning.Exchange.PhoenixRise : Tuning.Exchange.SecondWindHeal;
                ehp += maxHp * rise / Mathf.Max(0.01f, incoming);
            }
            // Open Stance: the floor's first hits land twice - a burst at the floor's start (a
            // quarter weight: it lands at full health, not at the edge), on top of its share of
            // the floor's damage.
            if (n("open_stance") > 0) ehp -= hit * Tuning.Exchange.OpenStanceHits * 0.25f;
            // The clear's own prices and gifts: what the NEXT floor starts with, at half weight
            // (a floor does not always open on a full bar). Toll takes from what is left - three
            // quarters on average - or, under Usury, from max health; Tribute heals.
            if (n("toll") > 0)
                ehp -= Mathf.Min(0.9f, Tuning.Exchange.TollFraction * n("toll")) * maxHp
                     * (AtMax(n, "toll") ? 1f : 0.75f) * 0.5f / Mathf.Max(0.01f, incoming);
            if (n("tribute") > 0) ehp += Tuning.Exchange.TributeHeal * maxHp * 0.5f / Mathf.Max(0.01f, incoming);
            if (m.HealCeiling < 1f) ehp *= m.HealCeiling;   // Hollow: fought from the ceiling
            ehp *= Judged.Survival(n);

            // Lifesteal as one capped pool, healing no faster than PlayerController.Drain allows -
            // the ledger's own heals inside that limit (Vital Spark, Pelican) share it.
            float lifesteal = StatCurves.Lifesteal(b.Lifesteal + m.BonusLifesteal + el.LifestealAdd);
            float withinLimit = lifesteal * dps + (ledger ? LedgerHealWithinLimit(n, maxHp, hitsPerSecond) : 0f);
            float heal = (Mathf.Min(withinLimit, StatCurves.LifestealPerSecond(maxHp))
                          + (ledger ? LedgerHeal(n, maxHp, releasesPerSecond) : 0f))
                       * m.HealMul * (AtMax(n, "thin_blood") ? AnaemiaFactor : 1f)
                       + BoardHealing(b, dps, maxHp, floor);
            // Cibation: lifesteal over the limit becomes a shield - health the next hits spend.
            ehp += BoardShield(b, lifesteal * dps, maxHp) / Mathf.Max(0.01f, incoming);
            float bleed = LedgerBleed(n, maxHp, hitsPerSecond, releasesPerSecond);

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
        /// priced 0.42 s. Kept only so the regression check can replay PlayerPower exactly; it also
        /// leaves out the ledger's conditional half, which PlayerPower never priced.
        /// </summary>
        static float ChainDps(Build b, StatPercents s, Mods m, Func<string, int> n, ElementProfile el, bool legacyTempo,
                              int floor, out float hitUnit, out float hitsPerSecond, out ChainShape shape)
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
            float lockSeconds = Tuning.Finisher.LockMedium * m.LockMul * interval;
            float seconds = basics * interval + lockSeconds;

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
            float chanceSum = Tuning.Player.BaseCritChance + el.CritAdd + s.CritChance / 100f + m.BonusCrit;
            Outgoing o = default;
            if (!legacyTempo)
            {
                // The ledger's conditional damage, as run-layer points bent with its own (Mods.Factor).
                o = LedgerOutgoing(m, n, b, floor, basics, basic, finisher, 1f, accuracy, seconds);
                chanceSum += o.ChanceAdd;   // Honed, on the share of hits it reaches
            }
            float mulSum = Tuning.Player.BaseCritMultiplier + s.CritDamage / 100f;
            var (chance, poolMul) = StatCurves.Crit(chanceSum, mulSum);
            float critMul = Player.PlayerPower.ColdIron(poolMul + m.CritDamageAdd, n("cold_iron"));
            float critFactor = 1f + chance * (critMul - 1f);

            if (!legacyTempo)
            {
                basic *= o.Basic;
                finisher *= o.Art;
                // Stoop, Cementation: a share of hits rolled with a crit bonus, in the pool.
                if (o.BonusShare > 0f)
                {
                    var (bc, bm) = StatCurves.Crit(chanceSum + Tuning.Exchange.ForcedCritChance, mulSum);
                    float bonused = 1f + bc * (Player.PlayerPower.ColdIron(bm + m.CritDamageAdd, n("cold_iron")) - 1f);
                    critFactor = Mathf.Lerp(critFactor, bonused, o.BonusShare);
                }
            }

            float dps = (basics * basic + finisher) / seconds * critFactor;

            if (!legacyTempo)
            {
                // Slag: the art never crits - its share of the chain loses the crit factor.
                float artCrit = m.ArtsCantCrit ? 1f / Mathf.Max(1e-3f, critFactor) : 1f;
                dps = (basics * basic + finisher * artCrit) / seconds * critFactor;
                // Arts banked without their basics: Ouroboros, The Serpent Eats, Golden Chain.
                float skip = RefundShare(n, dps, seconds, basics * basic, finisher, floor);
                if (skip > 0f)
                    dps = ((1f - skip) * basics * basic + finisher * artCrit) / (seconds - skip * basics * interval) * critFactor;
                // Induration, Coagulation, Mollification: the body has more (or less) health to cut.
                dps *= EnemyToughness(m, floor);
            }

            hitsPerSecond = (basics + 1) / seconds;
            shape = new ChainShape
            {
                Seconds = seconds, Basics = basics, BasicHit = basic * critFactor, ArtHit = finisher * critFactor,
                CritChance = chance, CritMul = critMul, CritFactor = critFactor, HitsPerSecond = hitsPerSecond,
                LockShare = lockSeconds / seconds,
            };
            float result = dps * Conditionals(n, legacyTempo);
            if (!legacyTempo) result *= LedgerRepeats(n, chanceSum, mulSum, m, critFactor) * KeenEdge(n, floor);
            return result;
        }

        /// <summary>
        /// PlayerPower's own estimates for the conditional entries that move damage, unchanged so a
        /// legacy build replays it exactly. Further offense the Assay judges lives in
        /// <see cref="Judged"/>.
        /// </summary>
        static float Conditionals(Func<string, int> n, bool legacy)
        {
            float mul = 1f;
            int ex = n("executioner");
            if (ex > 0) mul /= 0.7f + 0.3f / (1f + Tuning.Exchange.ExecutionerDamage / 100f * ex);
            int rei = n("reiteration");
            if (rei > 0) mul *= 1f + Tuning.Exchange.ReiterationFraction / Mathf.Max(2, Tuning.Exchange.ReiterationEvery - (rei - 1));
            mul *= Mathf.Max(0.1f, 1f - Tuning.Exchange.FumblerChance * n("fumbler"));
            if (legacy) mul *= Mathf.Max(0.1f, 1f - 0.036f * n("souring"));   // else AssayExchange prices it
            return mul;
        }

        /// <summary>
        /// Entries whose worth is a matter of judgement rather than arithmetic: costs that tax
        /// movement, reach or readability, and the boons that buy them back. Each factor is a
        /// stated guess at what the entry does to a fight - argue with the numbers here, nowhere
        /// else. Inert entries are deliberately absent: they do nothing in the game either.
        /// </summary>
        public static class Judged
        {
            static float Per(Func<string, int> n, string id, float each) => 1f + each * n(id);
            static float Has(Func<string, int> n, string id, float f) => n(id) > 0 ? f : 1f;
            static float Max(Func<string, int> n, string id, float f) => AtMax(n, id) ? f : 1f;

            public static float Offense(Func<string, int> n)
            {
                float f = 1f;
                // Reach, aim and the body: fewer swings land, or land later.
                f *= Per(n, "short_arm", -0.06f);
                f *= Per(n, "wandering_eye", -0.03f) * Max(n, "wandering_eye", 0.90f);   // Blind Rage: focus lost
                f *= Per(n, "overcommitted", -0.02f);
                f *= Per(n, "fog", -0.02f);
                f *= Has(n, "rooted", 0.95f);
                f *= Per(n, "dead_weight", -0.02f) * Max(n, "dead_weight", 0.99f);        // Charnel
                f *= Per(n, "retrograde", -0.02f);
                f *= Per(n, "projection", -0.02f) * Max(n, "projection", 0.98f);         // Lattice
                f *= Max(n, "long_chain", 0.98f);                                        // Fraying
                f *= Per(n, "long_reach", 0.02f);
                f *= Per(n, "wide_arc", 0.03f);
                // Crowds and fields the single-target model cannot see.
                f *= Per(n, "dilation", 0.03f) * Max(n, "dilation", 1.02f);               // Expansion
                f *= Max(n, "reiteration", 1.02f);                                       // Rota
                f *= Has(n, "residue", 1.02f);
                f *= Has(n, "cataclysm", 1.02f);
                f *= Has(n, "basilisk", 1.06f);
                f *= Has(n, "extra_sigil", 1.02f);
                f *= Per(n, "deep_roots", 0.04f);
                f *= Max(n, "high_tide", 1.02f);                                         // Spring Tide
                // A captured spire's floor boon, held a floor longer.
                f *= Has(n, "lodestone", 1f + Assume.SpireFloorShare * Assume.SpireWorth);
                return f;
            }

            public static float Survival(Func<string, int> n)
            {
                float f = 1f;
                f *= Mathf.Max(0.5f, 1f - 0.08f * n("anchored")) * Max(n, "anchored", 0.92f);   // Mired, and its slide
                f *= Has(n, "rooted", 0.88f);
                f *= Per(n, "retrograde", -0.10f) * Max(n, "retrograde", 0.90f);          // Contrary
                f *= Per(n, "projection", -0.10f) * Max(n, "projection", 0.90f);          // Lattice
                f *= Per(n, "dead_weight", -0.04f);
                f *= Per(n, "overcommitted", -0.03f);
                f *= Per(n, "fog", -0.03f) * Max(n, "fog", 0.88f);                        // Murk: telegraphs at half
                f *= Has(n, "sol_niger", 0.85f);
                f *= Per(n, "rust", -0.05f);                                             // Corrosion is computed
                f *= Per(n, "short_arm", -0.02f);                                        // closer to the pack
                f *= Per(n, "wandering_eye", -0.01f);
                f *= Per(n, "fleetfoot", 0.04f);
                f *= Per(n, "eagle", 0.02f);
                f *= Has(n, "kiln_fired", 1.05f);
                f *= Has(n, "patina", 1.12f);                                            // Rust III's mirror
                f *= Per(n, "long_reach", 0.02f);
                f *= Has(n, "lightfoot", 1.04f);
                f *= Has(n, "retrograde_motion", 1.02f);
                // Enemies thrown back, staggered or kept off you.
                f *= Max(n, "aegis_cycle", 1.03f);                                       // Tin Ward
                f *= Max(n, "heavy_payoff", 1.03f);                                      // Crushing Blow
                f *= Has(n, "cataclysm", 1.06f);
                f *= Has(n, "antipathy", 1.08f);
                f *= Has(n, "unshackled", 1.05f);
                f *= Has(n, "lucid", 1.10f);
                f *= Has(n, "ossuary", 1.06f);
                f *= Max(n, "tailwind", 1.02f);                                          // Updraft
                return f;
            }
        }
    }
}
