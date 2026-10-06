using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;
using Convergence.Exchange;
using Convergence.Progression;

namespace Convergence.Player
{
    /// <summary>Whose gear and board a damage estimate assumes.</summary>
    public enum PowerBuild
    {
        /// <summary>The reference player: an unbuffed greatsword on the Medium chain, no gear,
        /// no board. What Tuning.Boss measures against.</summary>
        Reference,

        /// <summary>A full board and two Gold three-star pieces (ring, gloves) plus a Gold
        /// three-star weapon.</summary>
        BuiltTwo,

        /// <summary>As BuiltTwo, plus a Gold three-star neck.</summary>
        BuiltThree,
    }

    /// <summary>How the boon ledger is played.</summary>
    public enum LedgerPlay
    {
        /// <summary>Takes a random pair from every offer - a player choosing for any reason.</summary>
        Average,

        /// <summary>Takes whichever pair raises damage most, refusing when none does.</summary>
        Stacked,
    }

    /// <summary>
    /// How much damage a player deals per second, as a MODEL - so a floor can be priced against
    /// the player expected to be standing on it (Enemies.WaveComposer).
    ///
    /// Built from the game's own pieces rather than a spreadsheet of them: gear is real
    /// GearRoller.BuildGrants output, the board is read off MasteryBoard, and the ledger is the
    /// real ExchangeOffers drawing from the real catalogue, caps and weights. Retune any of them
    /// and this follows.
    ///
    /// SINGLE TARGET, MELEE, NO ELEMENT. Releases, the principle chains, Cleave, Splash and
    /// Cleaving Habit all make a real clear faster than this says, so a time derived from it is
    /// the SLOW end. Area damage especially: a swarm room can go 2-4x faster for a cleave build.
    ///
    /// From the CLI:
    ///
    ///     unity command eval 'return Convergence.Player.PlayerPower.Report();'
    /// </summary>
    public static class PlayerPower
    {
        /// <summary>Runs end at floor 100.</summary>
        public const int LastFloor = 100;

        /// <summary>Runs simulated per ledger curve. The ledger is noisy run to run; 200 is what
        /// the original ledger measurement used.</summary>
        const int Runs = 200;

        const int Seed = 20261002;

        /// <summary>Two trials closer than this share of each other are a tie - see Simulate.</summary>
        public const float TieTolerance = 1e-4f;

        /// <summary>The element's crit multiplier when it declares none of its own -
        /// ElementalResource.CritMultiplier's default.</summary>
        const float BaseCritMultiplier = 1.8f;

        // ------------------------------------------------------------------ the build

        /// <summary>
        /// Gear plus board for a build, in the same summed percentage points BuildPlayer folds
        /// into the stat block.
        ///
        /// The built board spends 42 of its 50 points on two whole branches - Strikes (Damage)
        /// and Speed (Attack Speed) - and counts only those two stats from them; the rest of the
        /// board, and every rule on it, is beyond this model (the Assay prices them). Sub-stats sit
        /// at the middle of their range, on the kind a player re-rolls toward.
        /// </summary>
        public static StatPercents StatsFor(PowerBuild build)
        {
            var s = new StatPercents();
            if (build == PowerBuild.Reference) return s;

            foreach (var node in MasteryBoard.All)
            {
                if (node.Branch == "strikes" && node.Stat == StatKind.Damage) s.Damage += node.Value;
                if (node.Branch == "speed" && node.Stat == StatKind.AttackSpeed) s.AttackSpeed += node.Value;
            }

            s.Add(Gold3(GearSlot.Weapon, StatKind.Damage,
                        StatKind.Damage, StatKind.FinisherPower, StatKind.CritDamage));
            s.Add(Gold3(GearSlot.Ring, StatKind.Damage,
                        StatKind.Damage, StatKind.Damage, StatKind.CritDamage));
            s.Add(Gold3(GearSlot.Gloves, StatKind.AttackSpeed,
                        StatKind.AttackSpeed, StatKind.AttackSpeed, StatKind.CritChance));
            if (build == PowerBuild.BuiltThree)
                s.Add(Gold3(GearSlot.Neck, StatKind.ElementalEffectiveness,
                            StatKind.Damage, StatKind.Damage, StatKind.Damage));
            return s;
        }

        static StatPercents Gold3(GearSlot slot, StatKind primary, params StatKind[] subs)
        {
            var rolled = new List<SubStat>();
            foreach (var kind in subs)
            {
                var (min, max) = GearRoller.SubStatRange(kind, LootTier.Gold);
                rolled.Add(new SubStat(kind, (min + max) * 0.5f));
            }
            return GearRoller.BuildGrants(slot, LootTier.Gold, primary, GearRoller.MaxLevel, rolled);
        }

        // ------------------------------------------------------------------ damage per second

        /// <summary>
        /// Sustained single-target damage per second: the Medium chain (two basics and a 5x
        /// finisher over 3.5 basic intervals plus the timing bar's wind-up, landed GOOD - 47.6
        /// for the reference player before crits),
        /// with the stat block and the ledger folded in exactly as PlayerController folds them.
        /// <paramref name="stacks"/> answers for the conditional entries, which have no Apply.
        /// </summary>
        public static float Dps(StatPercents s, Mods m, Func<string, int> stacks)
        {
            int basics = Mathf.Max(1, 2 + m.BasicsPerChainDelta);
            float speed = StatPercents.Apply(1f, s.AttackSpeed) * Mathf.Max(0.35f, m.AttackSpeedMul);
            float interval = Tuning.Attack.BaseInterval / speed;

            float basic = (Tuning.Player.BaseDamage + m.BonusDamage) * StatPercents.Apply(1f, s.Damage);
            float finisher = basic * Tuning.Finisher.DamageMedium
                           * Mathf.Max(0.25f, m.FinisherDamageMul) * StatPercents.Apply(1f, s.FinisherPower);
            float seconds = (basics + Tuning.Finisher.LockMedium * m.LockMul) * interval;

            // The finisher timing bar (Tuning.StrikeTiming): an ordinary swing gains a wind-up and
            // is paid back through the parity factor, priced at a GOOD press - never at perfect
            // play, which would price every wave against a skill the player may not have. By
            // construction the two cancel and the number is unchanged; it is written out so the
            // model stays honest if either half ever moves.
            float windUp = Tuning.StrikeTiming.BarSeconds;
            finisher *= Combat.StrikeJudge.Parity(basics * basic, finisher, seconds, windUp)
                        * Tuning.StrikeTiming.GoodMultiplier;
            seconds += windUp;

            float chance = Mathf.Clamp01(Tuning.Player.BaseCritChance + s.CritChance / 100f + m.BonusCrit);
            float critMul = ColdIron(BaseCritMultiplier + s.CritDamage / 100f + m.CritDamageAdd, stacks("cold_iron"));

            float dps = (basics * basic + finisher) / seconds * (1f + chance * (critMul - 1f));
            return dps * Conditionals(stacks);
        }

        /// <summary>A crit's multiplier after Cold Iron: its bonus cut by a third a stack, and
        /// under Quenched (III) less than an ordinary hit - RunEffects.CritMultiplier's rule.</summary>
        public static float ColdIron(float multiplier, int stacks)
        {
            if (stacks <= 0) return multiplier;
            if (stacks >= 3) return Tuning.Exchange.QuenchedMul;
            return 1f + (multiplier - 1f) * Mathf.Max(0f, 1f - Tuning.Exchange.ColdIronShare * stacks);
        }

        /// <summary>
        /// The conditional entries that move damage, as the share of a fight they bite in. Rough
        /// on purpose - they are the minority of the ledger's worth, and each guess is stated.
        /// </summary>
        static float Conditionals(Func<string, int> stacks)
        {
            float mul = 1f;

            // Executioner: its points on the last 30% of every body, so that 30% goes faster.
            int ex = stacks("executioner");
            if (ex > 0) mul /= 0.7f + 0.3f / (1f + Tuning.Exchange.ExecutionerDamage / 100f * ex);

            // Reiteration: every fifth landed hit (fourth at two) repeats for half.
            int rei = stacks("reiteration");
            if (rei > 0) mul *= 1f + Tuning.Exchange.ReiterationFraction / Mathf.Max(2, Tuning.Exchange.ReiterationEvery - (rei - 1));

            // Fumbler: one swing in nine passes through, per stack.
            mul *= Mathf.Max(0.1f, 1f - Tuning.Exchange.FumblerChance * stacks("fumbler"));

            // Souring: points lost for every ten seconds on a floor; across a ~45s floor that
            // averages about a third of its cap a stack.
            mul *= Mathf.Max(0.1f, 1f - 0.036f * stacks("souring"));

            return mul;
        }

        // ------------------------------------------------------------------ the ledger, simulated

        static Dictionary<(PowerBuild, LedgerPlay), float[]> _curves;

        /// <summary>Damage per second on <paramref name="floor"/>, averaged over simulated runs -
        /// the ledger held when that floor starts, so floor 1 has none.</summary>
        public static float Dps(PowerBuild build, LedgerPlay play, int floor)
        {
            _curves ??= new Dictionary<(PowerBuild, LedgerPlay), float[]>();
            if (!_curves.TryGetValue((build, play), out var curve))
            {
                curve = Simulate(StatsFor(build), play);
                _curves[(build, play)] = curve;
            }
            return curve[Mathf.Clamp(floor, 1, LastFloor)];
        }

        /// <summary>
        /// The player a floor is priced against: the reference player on the average ledger
        /// early, turning into the built player stacking boons across
        /// Tuning.Waves.BuiltFromFloor..BuiltByFloor.
        /// </summary>
        public static float ExpectedDps(int floor)
        {
            float t = Mathf.InverseLerp(Tuning.Waves.BuiltFromFloor, Tuning.Waves.BuiltByFloor, floor);
            return Mathf.Lerp(Dps(PowerBuild.Reference, LedgerPlay.Average, floor),
                              Dps(PowerBuild.BuiltThree, LedgerPlay.Stacked, floor), t);
        }

        static float[] Simulate(StatPercents stats, LedgerPlay play)
        {
            var curve = new float[LastFloor + 1];

            // The deals draw from their own seeded generator, and the pick from another, so the
            // curve - and every pool priced from it - is the same every session. UnityEngine.Random
            // is seeded and restored too, for anything that still reads it.
            var saved = UnityEngine.Random.state;
            UnityEngine.Random.InitState(Seed);
            var pick = new System.Random(Seed);
            var deals = new System.Random(Seed + 1);
            try
            {
                for (int run = 0; run < Runs; run++)
                {
                    var ledger = new RunModifiers();
                    for (int floor = 1; floor <= LastFloor; floor++)
                    {
                        float now = Dps(stats, ledger.Current, ledger.StacksOf);
                        curve[floor] += now;
                        if (floor == LastFloor) break;

                        ledger.FloorCleared();
                        if (!ExchangeOffers.DealAfter(floor)) continue;
                        var offer = ExchangeOffers.Build(ledger, floor, deals);
                        if (offer.Pairs.Count == 0) continue;

                        ExchangePair chosen = null;
                        if (play == LedgerPlay.Average)
                            chosen = offer.Pairs[pick.Next(offer.Pairs.Count)];
                        else
                        {
                            float best = offer.CanRefuse ? now : float.MinValue;
                            foreach (var pair in offer.Pairs)
                            {
                                // Ties go to the later pair, within a hair: many pairs leave damage
                                // untouched, and a last-bit difference must not decide between them
                                // (the Assay replays this loop and must make the same choices).
                                float trial = Trial(stats, ledger, pair);
                                if (trial < best - TieTolerance * Mathf.Abs(best)) continue;
                                best = Mathf.Max(best, trial);
                                chosen = pair;
                            }
                        }

                        if (chosen == null) { if (offer.CanRefuse) ledger.Refuse(); continue; }
                        if (chosen.Boon != null) ledger.Take(chosen.Boon);
                        if (chosen.Cost != null) ledger.Take(chosen.Cost);
                    }
                }
            }
            finally
            {
                UnityEngine.Random.state = saved;
            }

            for (int f = 1; f <= LastFloor; f++) curve[f] /= Runs;
            return curve;
        }

        /// <summary>Damage per second if <paramref name="pair"/> were taken - priced through the
        /// ledger's own Preview, so the pair goes through the Vessel like any stack.</summary>
        static float Trial(StatPercents stats, RunModifiers ledger, ExchangePair pair)
        {
            var m = ledger.Preview(pair.Boon, pair.Cost);
            string boon = pair.Boon?.Id, cost = pair.Cost?.Id;
            return Dps(stats, m, id => ledger.StacksOf(id) + (id == boon ? 1 : 0) + (id == cost ? 1 : 0));
        }

        // ------------------------------------------------------------------ report

        public static string Report()
        {
            var sb = new StringBuilder();
            foreach (var build in new[] { PowerBuild.BuiltTwo, PowerBuild.BuiltThree })
            {
                var s = StatsFor(build);
                sb.Append($"{build}: damage +{s.Damage:0}  attack speed +{s.AttackSpeed:0}  " +
                          $"crit +{s.CritChance:0.0}  crit damage +{s.CritDamage:0}  finisher +{s.FinisherPower:0.0}\n");
            }

            float refOne = Dps(PowerBuild.Reference, LedgerPlay.Average, 1);
            sb.Append($"\nDPS (x the reference player on floor 1, {refOne:0.0})\n");
            sb.Append("floor   ref/avg  built2/avg  built3/avg  built3/stack | ledger avg  stack | expected\n");
            foreach (int f in new[] { 1, 5, 10, 20, 30, 40, 50, 60, 75, 100 })
            {
                float r = Dps(PowerBuild.Reference, LedgerPlay.Average, f);
                float b2 = Dps(PowerBuild.BuiltTwo, LedgerPlay.Average, f);
                float b3 = Dps(PowerBuild.BuiltThree, LedgerPlay.Average, f);
                float b3s = Dps(PowerBuild.BuiltThree, LedgerPlay.Stacked, f);
                float la = b3 / Dps(PowerBuild.BuiltThree, LedgerPlay.Average, 1);
                float ls = b3s / Dps(PowerBuild.BuiltThree, LedgerPlay.Stacked, 1);
                sb.Append($"{f,5}   {r / refOne,6:0.00}  {b2 / refOne,9:0.00}  {b3 / refOne,9:0.00}  " +
                          $"{b3s / refOne,11:0.00} |   x{la,5:0.00}  x{ls,5:0.00} | {ExpectedDps(f),6:0}\n");
            }
            return sb.ToString();
        }
    }
}
