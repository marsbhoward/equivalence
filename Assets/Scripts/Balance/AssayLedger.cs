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
    }

    public static partial class Assay
    {
        static readonly Dictionary<string, Curve> _curves = new();

        /// <summary>Forget every cached curve and build - after a tuning change, from eval.</summary>
        public static void Invalidate()
        {
            _curves.Clear();
            _builds.Clear();
        }

        /// <summary>
        /// The build's curve under a pick policy, simulated through the real ExchangeOffers.
        /// Seeded, and Unity's random state restored, so it is the same every session and nothing
        /// else notices it ran.
        /// </summary>
        public static Curve Run(Build b, Policy policy, int runs = 100,
                                int refusalCap = int.MaxValue, int refusalStreak = int.MaxValue, int offerEvery = 0)
        {
            string key = $"{b.Key}|{policy}|{runs}|{refusalCap}|{refusalStreak}|{offerEvery}|{StatCurves.Enabled}";
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

                    foreach (var e in ledger.Held)
                    {
                        held.TryGetValue(e.Id, out var count);
                        held[e.Id] = count + ledger.StacksOf(e.Id);
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
                        float u = Utility(trial);
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
