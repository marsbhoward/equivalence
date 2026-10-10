using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;
using Convergence.Exchange;

namespace Convergence.Balance
{
    /// <summary>How a simulated player picks from the exchange.</summary>
    public enum Policy
    {
        /// <summary>A random pair from every offer, never refusing - a player choosing for any
        /// reason at all. PlayerPower's "Average".</summary>
        Random,

        /// <summary>The pair that most improves damage and survival together (log DPS + log
        /// effective health), refusing when none does and never giving up more than
        /// <see cref="Assay.Assume.SensibleMaxSurvivalLoss"/> of its survival in one pick - the
        /// player the deep floors should be priced against.</summary>
        Sensible,

        /// <summary>Whichever pair raises damage most, blind to survival. PlayerPower's "Stacked",
        /// kept as an exploit detector: if it ever beats Sensible by much, a cost is too cheap.</summary>
        Greedy,
    }

    /// <summary>A build's worth floor by floor, averaged over simulated runs - the ledger held when
    /// each floor starts, so floor 1 has none.</summary>
    public sealed class Curve
    {
        public readonly float[] Dps = new float[Assay.LastFloor + 1];
        public readonly float[] ChainDps = new float[Assay.LastFloor + 1];
        public readonly float[] Ehp = new float[Assay.LastFloor + 1];
        public readonly float[] EffectiveHp = new float[Assay.LastFloor + 1];
        public readonly float[] MaxHp = new float[Assay.LastFloor + 1];
        public readonly float[] Sustain = new float[Assay.LastFloor + 1];

        /// <summary>Mean stacks of each entry held on the last floor.</summary>
        public readonly Dictionary<string, float> Held = new();

        /// <summary>Mean offers refused per run.</summary>
        public float Refused;

        /// <summary>Per run on average: costs transmuted at a circle, Rubedos reached (boons held
        /// at max stacks), combinations taken - the Phase 5 catalogue's per-run targets.</summary>
        public float Transmuted, Rubedos, Combinations;

        /// <summary>Each simulated run's ledger as it stood on the last floor - for attribution.</summary>
        public readonly List<RunModifiers> Finals = new();
    }

    public static partial class Assay
    {
        static readonly Dictionary<string, Curve> _curves = new();

        /// <summary>False runs the ledger with no transmutation circles - to see what they are
        /// worth. A setting for eval, never a model of the game.</summary>
        public static bool Circles = true;

        /// <summary>Forget every cached curve and build - after a tuning change, from eval.</summary>
        public static void Invalidate()
        {
            _curves.Clear();
            _builds.Clear();
            _attribution.Clear();
        }

        /// <summary>
        /// The build's curve under a pick policy, simulated through the real ExchangeOffers.
        /// Seeded, and Unity's random state restored, so it is the same every session and nothing
        /// else notices it ran.
        /// </summary>
        public static Curve Run(Build b, Policy policy, int runs = 100,
                                int refusalCap = int.MaxValue, int refusalStreak = int.MaxValue, int offerEvery = 0)
        {
            string key = $"{b.Key}|{policy}|{runs}|{refusalCap}|{refusalStreak}|{offerEvery}|{StatCurves.Enabled}|{Circles}";
            if (_curves.TryGetValue(key, out var cached)) return cached;

            var curve = new Curve();
            var held = new Dictionary<string, float>();
            int refused = 0;

            var saved = UnityEngine.Random.state;
            UnityEngine.Random.InitState(Seed);
            var pick = new System.Random(Seed);
            var deals = new System.Random(Seed + 1);
            try
            {
                for (int run = 0; run < runs; run++)
                {
                    var ledger = new RunModifiers();
                    // The Vessel is sized by mastery: an unlevelled character's is the smallest. A
                    // legacy build has no element (PlayerPower's), so no element entries either.
                    ledger.SetCharacter(b.Legacy ? null : b.Element, Art.Gear.WeaponClass.Greatsword,
                                        b.Kit == Kit.Reference && !b.Legacy ? 0 : Tuning.Mastery.LevelCap);
                    int refusedThisRun = 0, refusedInARow = 0;

                    for (int floor = 1; floor <= LastFloor; floor++)
                    {
                        var now = Evaluate(b, ledger.Current, ledger.StacksOf, floor);
                        curve.Dps[floor] += now.Dps;
                        curve.ChainDps[floor] += now.ChainDps;
                        curve.Ehp[floor] += now.Ehp;
                        curve.EffectiveHp[floor] += now.EffectiveHp;
                        curve.MaxHp[floor] += now.MaxHp;
                        curve.Sustain[floor] += now.Sustain;
                        if (floor == LastFloor) break;

                        // The floor clears before the deal: Withering takes its cut first.
                        ledger.FloorCleared();

                        // A transmutation circle, on the floors before each Rift: a held Nigredo
                        // leaves the ledger and its Albedo joins it. PlayerPower never had them, so
                        // a legacy build skips them and still replays it.
                        if (Circles && !b.Legacy && floor % Assume.CircleEveryFloors == 0 && ledger.HoldsNigredo
                            && ledger.CanTransmute)   // the run's cap, as GameBootstrap.CircleDue
                        {
                            ledger.Transmute(ChooseNigredo(b, policy, ledger, floor, pick));
                            curve.Transmuted++;
                        }

                        // The game's own cadence (ExchangeOffers.DealAfter), or every Nth floor
                        // when a report asks for another.
                        if (offerEvery <= 0 ? !ExchangeOffers.DealAfter(floor) : floor % offerEvery != 0) continue;

                        var offer = ExchangeOffers.Build(ledger, floor, deals);
                        if (offer.Pairs.Count == 0) continue;

                        // A refusal cap - per run, or in a row - takes the refuse slate away once it
                        // is spent, as Indenture does for one offer.
                        if (refusedThisRun >= refusalCap || refusedInARow >= refusalStreak) offer.CanRefuse = false;

                        var chosen = Choose(b, policy, ledger, offer, floor, now, pick);
                        if (chosen == null)
                        {
                            refused++; refusedThisRun++; refusedInARow++;
                            if (offer.CanRefuse) ledger.Refuse();
                            continue;
                        }
                        refusedInARow = 0;
                        if (chosen.Boon != null) ledger.Take(chosen.Boon);
                        if (chosen.Cost != null) ledger.Take(chosen.Cost);
                    }

                    curve.Finals.Add(ledger);
                    foreach (var e in ledger.Held)
                    {
                        held.TryGetValue(e.Id, out var count);
                        held[e.Id] = count + ledger.StacksOf(e.Id);
                        if (e.Kind == ExchangeKind.Boon && e.Stackable && ledger.AtCap(e)) curve.Rubedos++;
                        if (e.IsCombination) curve.Combinations++;
                    }
                }
            }
            finally
            {
                UnityEngine.Random.state = saved;
            }

            for (int f = 1; f <= LastFloor; f++)
            {
                curve.Dps[f] /= runs;
                curve.ChainDps[f] /= runs;
                curve.Ehp[f] /= runs;
                curve.EffectiveHp[f] /= runs;
                curve.MaxHp[f] /= runs;
                curve.Sustain[f] /= runs;
            }
            foreach (var kv in held) curve.Held[kv.Key] = kv.Value / runs;
            curve.Refused = refused / (float)runs;
            curve.Transmuted /= runs;
            curve.Rubedos /= runs;
            curve.Combinations /= runs;

            _curves[key] = curve;
            return curve;
        }

        static ExchangePair Choose(Build b, Policy policy, RunModifiers ledger, ExchangeOffer offer,
                                   int floor, Worth now, System.Random pick)
        {
            switch (policy)
            {
                case Policy.Random:
                    return offer.Pairs[pick.Next(offer.Pairs.Count)];

                case Policy.Greedy:
                {
                    // PlayerPower's exact loop - ties go to the later pair - so a legacy build replays it.
                    ExchangePair chosen = null;
                    float best = offer.CanRefuse ? now.Dps : float.MinValue;
                    foreach (var pair in offer.Pairs)
                    {
                        float trial = Trial(b, ledger, pair, floor, 0).Dps;
                        if (trial < best - Player.PlayerPower.TieTolerance * Mathf.Abs(best)) continue;
                        best = Mathf.Max(best, trial);
                        chosen = pair;
                    }
                    return chosen;
                }

                default:
                {
                    // Withering costs nothing the moment it is taken, so it is priced over the floors
                    // ahead - for each pair and for standing pat alike, so the comparison is like for like.
                    // Withering costs nothing the moment it is taken and everything later, so it is
                    // priced over the floors ahead - for each pair and for standing pat alike.
                    int wither = ledger.StacksOf("withering");
                    var stay = Evaluate(b, ledger.Current, ledger.StacksOf, floor);

                    ExchangePair chosen = null, fallback = null;
                    float best = offer.CanRefuse ? Utility(stay) : float.MinValue, fallbackBest = float.MinValue;
                    float floorHp = stay.EffectiveHp * (1f - Assume.SensibleMaxSurvivalLoss);
                    float minEhp = Mathf.Min(stay.Ehp, Assume.SensibleMinHits * ChaserHit(floor));
                    foreach (var pair in offer.Pairs)
                    {
                        int w = wither + (pair.Cost?.Id == "withering" ? 1 : 0);
                        var trial = Trial(b, ledger, pair, floor, w - wither);
                        float u = Utility(trial) + Lookahead(b, ledger, pair, floor);
                        if (u >= fallbackBest) { fallbackBest = u; fallback = pair; }
                        if (trial.EffectiveHp < floorHp || trial.Ehp < minEhp) continue;
                        if (u < best) continue;
                        best = u;
                        chosen = pair;
                    }
                    // Indenture: no refusing, so the best of a bad lot is taken.
                    return chosen ?? (offer.CanRefuse ? null : fallback);
                }
            }
        }

        /// <summary>Which held Nigredo a circle takes: the policy's own measure of the ledger after
        /// it - at random, by damage, or by the sensible utility.</summary>
        static ExchangeEntry ChooseNigredo(Build b, Policy policy, RunModifiers ledger, int floor, System.Random pick)
        {
            var nigredos = ledger.Nigredos();
            if (nigredos.Count == 1 || policy == Policy.Random) return nigredos[pick.Next(nigredos.Count)];
            ExchangeEntry best = nigredos[0];
            float bestScore = float.MinValue;
            foreach (var cost in nigredos)
            {
                var after = ledger.Clone();
                after.Transmute(cost);
                var w = Evaluate(b, after.Current, after.StacksOf, floor);
                float score = policy == Policy.Greedy ? w.Dps : Utility(w);
                if (score > bestScore) { bestScore = score; best = cost; }
            }
            return best;
        }

        /// <summary>
        /// A deal-shaping entry's price, which lands on the NEXT deal (Debt, Indenture, Caput
        /// Mortuum, Speculum): the next deal built with it taken and without, the same draw for
        /// both, and the difference in the best the careful player could do there. Without it the
        /// policy saw them as free - they change nothing about the character now - and took them in
        /// four runs of five.
        /// </summary>
        static float Lookahead(Build b, RunModifiers ledger, ExchangePair pair, int floor)
        {
            float u = 0f;
            if (pair.Cost != null && pair.Cost.Recurring) u += NextDealWith(b, ledger, pair.Cost, floor) - NextDealWith(b, ledger, null, floor);
            if (pair.Boon != null && pair.Boon.Recurring) u += NextDealWith(b, ledger, pair.Boon, floor) - NextDealWith(b, ledger, null, floor);
            return u;
        }

        /// <summary>The best utility the careful player could reach at the next deal, with
        /// <paramref name="shaper"/> taken now (or nothing) - averaged over a few draws of that
        /// deal (one draw is mostly noise), each the same with and without. A pair that is itself
        /// a deal shaper is left out: it would need a lookahead of its own.</summary>
        static float NextDealWith(Build b, RunModifiers ledger, ExchangeEntry shaper, int floor)
        {
            const int Draws = 4;
            int next = ExchangeOffers.NextDealFloor(floor);
            float sum = 0f;
            for (int d = 0; d < Draws; d++)
            {
                var c = ledger.Clone();
                if (shaper != null) c.Take(shaper);
                var offer = ExchangeOffers.Build(c, next, new System.Random(floor * 7919 + 17 + d * 104729));
                float stay = Utility(Evaluate(b, c.Current, c.StacksOf, next));
                float best = float.MinValue;
                foreach (var p in offer.Pairs)
                {
                    if ((p.Cost != null && p.Cost.Recurring) || (p.Boon != null && p.Boon.Recurring)) continue;
                    best = Mathf.Max(best, Utility(Trial(b, c, p, next, 0)));
                }
                if (offer.CanRefuse || best == float.MinValue) best = Mathf.Max(best, stay);
                sum += best;
            }
            return sum / Draws;
        }

        /// <summary>What the sensible player maximises: damage and survival weighted equally.</summary>
        static float Utility(Worth w) => Mathf.Log(Mathf.Max(0.001f, w.Dps)) + Mathf.Log(w.EffectiveHp);

        /// <summary>The worth if <paramref name="pair"/> were taken.</summary>
        static Worth Trial(Build b, RunModifiers ledger, ExchangePair pair, int floor, int newWither)
        {
            // Priced through the ledger's own Preview, so the pair goes through the Vessel like
            // any other stack - PlayerPower prices its trials the same way, so a legacy build
            // still replays it. A Withering stack taken now is charged its loss over the floors
            // ahead (Assume.WitheringHorizon), since it costs nothing the moment it is taken.
            var m = ledger.Preview(pair.Boon, pair.Cost);
            if (newWither > 0)
            {
                m.Add(StatKind.MaxHp, -Tuning.Exchange.WitheringPerFloor * 100f * newWither * Assume.WitheringHorizon);
                m.MaxHpMul = Mathf.Max(0.1f, 1f + m.Points(StatKind.MaxHp) / 100f);
            }
            string boon = pair.Boon?.Id, cost = pair.Cost?.Id;
            return Evaluate(b, m, id => ledger.StacksOf(id) + (id == boon ? 1 : 0) + (id == cost ? 1 : 0), floor);
        }
    }
}
