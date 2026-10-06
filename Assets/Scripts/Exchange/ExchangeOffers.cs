using System.Collections.Generic;
using UnityEngine;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.Exchange
{
    /// <summary>Why a slate is on the deal when it is not a plain draw - what its card says.</summary>
    public enum SlateReason { Drawn, NewCombination, Mercy, Pity }

    /// <summary>One slate: a boon against a cost. The boon is absent on a Debt deal.</summary>
    public class ExchangePair
    {
        public ExchangeEntry Boon;
        public ExchangeEntry Cost;
        public SlateReason Reason = SlateReason.Drawn;
    }

    public class ExchangeOffer
    {
        public readonly List<ExchangePair> Pairs = new();

        /// <summary>False under Indenture, and once the run's refusals are spent.</summary>
        public bool CanRefuse = true;

        /// <summary>Indenture made this deal: refusing is not possible and costs nothing.</summary>
        public bool Forced;

        /// <summary>Refusals the run has left, for the refuse slate to say.</summary>
        public int RefusalsLeft;
    }

    /// <summary>
    /// Builds the deal shown after a cleared floor - every DealEvery floors (DealAfter).
    ///
    /// THE PAIRING FORMULA
    ///
    ///   costWeight  drawn per slate, climbing with the floor
    ///   boonWeight  clamp(costWeight + bonus, 1, 3), bonus 1 or 0
    ///
    /// The bonus starts almost always 1 and drifts toward 0 as the run deepens, so early deals are
    /// bargains and late ones level trades - the exchange becomes genuinely equivalent exactly when
    /// the costs on offer get heavy. (boon = cost + 1 with no clamp orphaned a third of the
    /// catalogue: weight-1 boons and weight-3 costs could never be drawn.)
    ///
    /// ONE GUARANTEED SLATE a deal - two when it shows three pairs - in priority order: a
    /// combination that just opened, the MERCY PULL (a cost one stack short of its Nigredo, unseen
    /// for MercyDeals), the PITY TIMER (a stackable boon held below max, unseen for PityDeals).
    /// Ties go to whatever has waited longest. So stacking is achievable, and chasing an Albedo is
    /// a strategy rather than a lottery.
    ///
    /// SEEDED: every draw comes from the rng handed in (the run's seed for the floor), so a restart
    /// meets the same deals - and Oracle can show the deal that follows a choice truthfully, by
    /// building it on a copy of the ledger with the same seed.
    /// </summary>
    public static class ExchangeOffers
    {
        /// <summary>A deal follows this floor's clear: the first, and every DealEvery-th after it.</summary>
        public static bool DealAfter(int floor) => floor >= 1 && (floor - 1) % T.DealEvery == 0;

        /// <summary>The floor whose clear brings the deal after the one following <paramref name="floor"/>.</summary>
        public static int NextDealFloor(int floor) => floor + T.DealEvery;

        public static ExchangeOffer Build(RunModifiers mods, int floor, System.Random rng)
        {
            var offer = new ExchangeOffer();
            if (mods == null) return offer;
            mods.BeginDeal();

            var pending = mods.Pending;
            int pairs = Mathf.Clamp(T.BasePairs + pending.PairsDelta, 1, T.MaxPairs);
            offer.Forced = pending.Forced;
            bool noBoon = pending.NoBoon;
            pending.Clear();
            offer.RefusalsLeft = mods.RefusalsLeft;
            offer.CanRefuse = !offer.Forced && mods.RefusalsLeft > 0;

            float t = Mathf.Clamp01((floor - 1) / (T.BargainUntilFloor - 1f));
            int target = Mathf.Clamp(1 + Mathf.FloorToInt(t * 3f), 1, 3);
            float bargain = Mathf.Lerp(T.BargainEarly, T.BargainDeep, t);

            var used = new HashSet<string>();

            int guaranteed = pairs >= 3 ? 2 : 1;
            for (int g = 0; g < guaranteed && offer.Pairs.Count < pairs; g++)
            {
                var pair = Guaranteed(mods, rng, used, bargain);
                if (pair == null) break;
                offer.Pairs.Add(pair);
            }

            // Slates differ in cost weight on purpose: one at the floor's target and one a step
            // lighter makes the choice "safe or spicy" rather than "left or right". Past the first
            // floors a slate sometimes draws light instead, so a nudge still turns up late.
            for (int i = offer.Pairs.Count; i < pairs; i++)
            {
                int costWeight = Mathf.Max(1, target - (i % 2));
                if (costWeight > 1 && rng.NextDouble() < T.LightShareDeep) costWeight = 1;
                int bonus = rng.NextDouble() < bargain ? 1 : 0;
                int boonWeight = Mathf.Clamp(costWeight + bonus, 1, 3);

                var cost = Draw(mods, ExchangeKind.Cost, costWeight, used, rng);
                var boon = Draw(mods, ExchangeKind.Boon, boonWeight, used, rng);

                // A slate with neither half is not an offer. If the pool is that exhausted there
                // is nothing to show, and an empty card would be worse than one slate fewer.
                if (cost == null && boon == null) continue;
                offer.Pairs.Add(new ExchangePair { Boon = boon, Cost = cost });
            }

            // DEBT: this deal gives its costs and no boons - take one for nothing, or spend a refusal.
            if (noBoon)
                foreach (var p in offer.Pairs) p.Boon = null;

            foreach (var p in offer.Pairs)
            {
                mods.MarkOffered(p.Boon);
                mods.MarkOffered(p.Cost);
            }
            return offer;
        }

        /// <summary>The deal's guaranteed slate, or null when nothing is owed one.</summary>
        static ExchangePair Guaranteed(RunModifiers mods, System.Random rng, HashSet<string> used, float bargain)
        {
            var combo = mods.TakeNewCombination();
            while (combo != null && used.Contains(combo.Id)) combo = mods.TakeNewCombination();
            if (combo != null)
            {
                used.Add(combo.Id);
                // A Putrefaction always comes against the heaviest boons; a Conjunction or a
                // Citrinitas against a cost of its own weight.
                if (combo.Kind == ExchangeKind.Cost)
                    return new ExchangePair { Cost = combo, Boon = Draw(mods, ExchangeKind.Boon, 3, used, rng),
                                              Reason = SlateReason.NewCombination };
                return new ExchangePair { Boon = combo, Cost = Draw(mods, ExchangeKind.Cost, combo.Weight, used, rng),
                                          Reason = SlateReason.NewCombination };
            }

            // The mercy pull, then the pity timer: whatever has waited longest of each.
            ExchangeEntry mercy = null, pity = null;
            int mercyWait = -1, pityWait = -1;
            foreach (var e in mods.Held)
            {
                if (!e.Stackable || used.Contains(e.Id) || !mods.Offerable(e)) continue;
                int held = mods.StacksOf(e);
                int wait = mods.DealsSince(e);
                if (e.Kind == ExchangeKind.Cost)
                {
                    if (e.AlbedoId == null || held != e.MaxStacks - 1 || wait < T.MercyDeals) continue;
                    if (wait > mercyWait) { mercy = e; mercyWait = wait; }
                }
                else
                {
                    if (held <= 0 || held >= e.MaxStacks || wait < T.PityDeals) continue;
                    if (wait > pityWait) { pity = e; pityWait = wait; }
                }
            }

            int bonus = rng.NextDouble() < bargain ? 1 : 0;
            if (mercy != null)
            {
                used.Add(mercy.Id);
                var boon = Draw(mods, ExchangeKind.Boon, Mathf.Clamp(mercy.Weight + bonus, 1, 3), used, rng);
                return new ExchangePair { Cost = mercy, Boon = boon, Reason = SlateReason.Mercy };
            }
            if (pity != null)
            {
                used.Add(pity.Id);
                var cost = Draw(mods, ExchangeKind.Cost, Mathf.Max(1, pity.Weight - bonus), used, rng);
                return new ExchangePair { Boon = pity, Cost = cost, Reason = SlateReason.Pity };
            }
            return null;
        }

        /// <summary>
        /// One entry of the requested kind and weight, not already on this deal.
        ///
        /// Falls outward through neighbouring weights when the exact one is exhausted - late in a
        /// run the heavy tiers fill up, and offering nothing because weight 3 is spent would
        /// quietly switch the whole mechanic off just as it should be biting hardest.
        /// </summary>
        static ExchangeEntry Draw(RunModifiers mods, ExchangeKind kind, int weight, HashSet<string> used, System.Random rng)
        {
            foreach (int w in Fallback(weight))
            {
                var pool = mods.Offerable(kind, w);
                pool.RemoveAll(e => used.Contains(e.Id));
                if (pool.Count == 0) continue;

                var pick = pool[rng.Next(pool.Count)];
                used.Add(pick.Id);
                return pick;
            }
            return null;
        }

        static IEnumerable<int> Fallback(int weight)
        {
            yield return weight;
            for (int d = 1; d <= 2; d++)
            {
                if (weight - d >= 1) yield return weight - d;
                if (weight + d <= 3) yield return weight + d;
            }
        }

        /// <summary>
        /// ORACLE: the deal that would follow each choice on this one - one per slate, then one for
        /// refusing (null where refusing is not possible) - built on a copy of the ledger with the
        /// seed the real one will use. A transmutation before then can still change it.
        /// </summary>
        public static List<ExchangeOffer> PreviewNext(RunModifiers mods, ExchangeOffer offer, int floor,
                                                      System.Func<int, int> seedFor)
        {
            var list = new List<ExchangeOffer>();
            if (mods == null || offer == null) return list;
            int next = NextDealFloor(floor);

            foreach (var pair in offer.Pairs)
            {
                var copy = mods.Clone();
                if (pair.Boon != null) copy.Take(pair.Boon);
                if (pair.Cost != null) copy.Take(pair.Cost);
                list.Add(Build(copy, next, new System.Random(seedFor(next))));
            }
            if (offer.CanRefuse)
            {
                var copy = mods.Clone();
                copy.Refuse();
                list.Add(Build(copy, next, new System.Random(seedFor(next))));
            }
            else list.Add(null);
            return list;
        }
    }
}
