using System;
using System.Collections.Generic;
using System.Text;
using Convergence.Art.Gear;
using Convergence.Core;
using Convergence.Exchange;

namespace Convergence.Balance
{
    public static partial class Assay
    {
        /// <summary>
        /// PAIR PRICING (2026-10-20, docs/balance/2026-10-20-exchange-review.md): every boon and cost
        /// that can share a slate, priced TOGETHER on the four Max builds. <see cref="Entries"/>
        /// prices each entry alone, so a boon that makes a cost free (Kiln-Fired against Rust) was
        /// invisible by construction.
        ///
        /// For each pair: the cost's BITE alone (1 - worth with it / worth bare), and the bite it
        /// still takes once the boon is held (1 - worth with both / worth with the boon). The share
        /// of the bite the boon CANCELS is the flag - half or more and the slate is close to free.
        /// The other way, a pair worth much less than its parts (the boon is wasted under the cost)
        /// is a trap. Worth is damage x survival, as everywhere in the Assay.
        ///
        /// A slate's boon is its cost's weight or one heavier; same-axis pairs never meet
        /// (ExchangeEntry.Axis) and are skipped. Deal shapers and class-only entries are skipped as
        /// <see cref="Entries"/> skips them. It runs in steps - more than one eval's 5 s:
        /// <c>Assay.PairsStart()</c>, then <c>Assay.PairsStep()</c> until it returns the report.
        /// </summary>
        public static string PairsStart(int floor = 50)
        {
            _pairFloor = floor;
            _pairIndex = 0;
            _pairQueue = new List<(ExchangeEntry Boon, ExchangeEntry Cost)>();
            _pairResults = new List<PairResult>();
            _pairSingles = new Dictionary<string, float>();

            bool Priced(ExchangeEntry e) => e.Origin == EntryOrigin.Base && Array.IndexOf(Unpriced, e.Id) < 0
                && (e.Classes == null || Array.IndexOf(e.Classes, WeaponClass.Greatsword) >= 0);
            foreach (var cost in ExchangeCatalog.All)
            {
                if (cost.Kind != ExchangeKind.Cost || !Priced(cost)) continue;
                foreach (var boon in ExchangeCatalog.All)
                {
                    if (boon.Kind != ExchangeKind.Boon || !Priced(boon)) continue;
                    if (boon.Weight != cost.Weight && boon.Weight != cost.Weight + 1) continue;
                    if (boon.Axis != null && boon.Axis == cost.Axis) continue;
                    if (boon.Element != null && cost.Element != null && boon.Element != cost.Element) continue;
                    _pairQueue.Add((boon, cost));
                }
            }
            return $"{_pairQueue.Count} pairs queued at floor {floor} - call Assay.PairsStep() until it returns the report.";
        }

        public static string PairsStep(float seconds = 3.5f)
        {
            if (_pairQueue == null) return "Call Assay.PairsStart() first.";
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (_pairIndex < _pairQueue.Count && clock.Elapsed.TotalSeconds < seconds)
            {
                var (boon, cost) = _pairQueue[_pairIndex++];
                var only = boon.Element ?? cost.Element;
                PairResult worst = default;
                bool any = false;
                foreach (var b in PriceBuilds(only))
                {
                    float bare = Single(b, null), withBoon = Single(b, boon), withCost = Single(b, cost);
                    float both = Product(b, boon, cost);
                    float bite = 1f - withCost / bare;
                    float biteWithBoon = 1f - both / withBoon;
                    var r = new PairResult
                    {
                        Boon = boon.Id, Cost = cost.Id, Build = b.Key,
                        Gain = withBoon / bare - 1f, Bite = bite, BiteWithBoon = biteWithBoon,
                        Cancelled = bite > 0.005f ? 1f - biteWithBoon / bite : 0f,
                        Synergy = both * bare / Math.Max(1e-6f, withBoon * withCost),
                    };
                    // Keep the build where the boon cancels the most of the cost.
                    if (!any || r.Cancelled > worst.Cancelled) { worst = r; any = true; }
                }
                if (any) _pairResults.Add(worst);
            }
            if (_pairIndex < _pairQueue.Count)
                return $"{_pairIndex} / {_pairQueue.Count} pairs priced - call Assay.PairsStep() again.";
            return PairsReport();
        }

        struct PairResult
        {
            public string Boon, Cost, Build;
            public float Gain, Bite, BiteWithBoon, Cancelled, Synergy;
        }

        static List<(ExchangeEntry Boon, ExchangeEntry Cost)> _pairQueue;
        static List<PairResult> _pairResults;
        static Dictionary<string, float> _pairSingles;
        static int _pairFloor, _pairIndex;

        /// <summary>Damage x survival for a build holding one entry (or none) - cached per build.</summary>
        static float Single(Build b, ExchangeEntry e)
        {
            string key = b.Key + "|" + (e?.Id ?? "-");
            if (_pairSingles.TryGetValue(key, out var v)) return v;
            v = Product(b, e, null);
            _pairSingles[key] = v;
            return v;
        }

        static float Product(Build b, ExchangeEntry a, ExchangeEntry c)
        {
            var l = new RunModifiers();
            l.SetCharacter(b.Element, WeaponClass.Greatsword, Tuning.Mastery.LevelCap);
            if (a != null) l.Take(a);
            if (c != null) l.Take(c);
            // Entries that grow by the floor are priced over the horizon, as Entries prices them.
            if (Grows(a) || Grows(c))
                for (int f = 0; f < Assume.WitheringHorizon; f++) l.FloorCleared();
            var w = WorthOf(b, l, _pairFloor);
            return Math.Max(1e-6f, w.Dps * w.Hp);
        }

        static bool Grows(ExchangeEntry e)
            => e != null && (e.Id == "withering" || e.Id == "viriditas" || e.Id == "senescence");

        static string PairsReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{_pairResults.Count} slate pairs at floor {_pairFloor} (Max builds, each pair on the build where the boon cancels most of its cost).");
            sb.AppendLine("cancelled = share of the cost's bite the boon takes away; synergy = together over the parts (1 = independent).");

            var free = _pairResults.FindAll(r => r.Cancelled >= 0.5f);
            free.Sort((x, y) => y.Cancelled.CompareTo(x.Cancelled));
            sb.AppendLine($"\nNEAR-FREE - the boon cancels half the cost or more ({free.Count}):");
            foreach (var r in free) sb.AppendLine(Line(r));

            var helped = _pairResults.FindAll(r => r.Cancelled >= 0.2f && r.Cancelled < 0.5f);
            helped.Sort((x, y) => y.Cancelled.CompareTo(x.Cancelled));
            sb.AppendLine($"\nSOFTENED - the boon cancels 20-50% of the cost ({helped.Count}):");
            foreach (var r in helped) sb.AppendLine(Line(r));

            var traps = _pairResults.FindAll(r => r.Synergy <= 0.92f);
            traps.Sort((x, y) => x.Synergy.CompareTo(y.Synergy));
            sb.AppendLine($"\nTRAPS - together worth 8% or more under the parts: the boon wasted under the cost ({traps.Count}):");
            foreach (var r in traps) sb.AppendLine(Line(r));
            return sb.ToString();
        }

        static string Line(PairResult r)
            => $"  {r.Boon,-18} + {r.Cost,-15} | gain {r.Gain,5:P0}  bite {r.Bite,5:P0} -> {r.BiteWithBoon,5:P0}" +
               $"  cancelled {r.Cancelled,5:P0}  synergy {r.Synergy:0.00} | {r.Build}";
    }
}
