using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;
using Convergence.Exchange;

namespace Convergence.Balance
{
    /// <summary>
    /// WHERE PLAYER POWER GOES - the whole Assay as one data set, for a page to draw: every build's
    /// damage and survival floor by floor under each pick policy, the floors' own demand beside it
    /// (a Chaser's effective health and hit), the layers a character's power comes from (element,
    /// board, gear, ledger), and which ledger entries carried a finished run - each taken back out
    /// of the run's final ledger to see what it was worth.
    ///
    /// Heavy: dozens of simulated ledgers. Build it in pieces (<see cref="PowerMapWarm"/> one build
    /// a call) before asking for <see cref="PowerMap"/>, or call again after an eval timeout - the
    /// simulations finish and are cached.
    /// </summary>
    public static partial class Assay
    {
        public static readonly int[] MapFloors = { 1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100 };
        const int MapRuns = 60;

        static readonly Dictionary<string, string> _attribution = new();

        /// <summary>Simulates one build under every policy and attributes its ledger - so the map
        /// itself only reads caches. Returns what it warmed.</summary>
        public static string PowerMapWarm(int index)
        {
            var all = MapBuilds();
            if (index < 0 || index >= all.Count) return "done";
            var b = all[index];
            foreach (Policy p in Enum.GetValues(typeof(Policy))) Run(b, p, MapRuns);
            Attribution(b);
            return $"{index}: {b.Key}";
        }

        static List<Build> MapBuilds()
        {
            var list = new List<Build>();
            foreach (var a in Archetypes) foreach (var e in Elements) list.Add(Make(a, e, Kit.Max));
            foreach (var e in Elements) list.Add(Reference(e));   // the new player
            return list;
        }

        /// <summary>The whole map as JSON.</summary>
        public static string PowerMap()
        {
            var sb = new StringBuilder("{");
            sb.Append($"\"floors\":{Arr(MapFloors.Select(f => (float)f))},");
            sb.Append($"\"unit\":{F(Unit)},");
            sb.Append("\"enemy\":{");
            sb.Append($"\"chaserEhp\":{Arr(MapFloors.Select(f => ChaserEhp(f)))},");
            sb.Append($"\"chaserHit\":{Arr(MapFloors.Select(f => ChaserHit(f)))},");
            sb.Append($"\"targetSeconds\":{Arr(MapFloors.Select(f => Enemies.WaveComposer.TargetSeconds(f)))},");
            sb.Append($"\"pricedDps\":{Arr(MapFloors.Select(f => Player.PlayerPower.ExpectedDps(f)))}");
            sb.Append("},");

            sb.Append("\"runs\":[");
            bool first = true;
            foreach (var b in MapBuilds())
                foreach (Policy p in Enum.GetValues(typeof(Policy)))
                {
                    var c = Run(b, p, MapRuns);
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('{');
                    sb.Append($"\"build\":\"{b.Key}\",\"archetype\":\"{(b.Kit == Kit.Reference ? "NewPlayer" : b.Archetype.ToString())}\",");
                    sb.Append($"\"element\":\"{b.Element}\",\"policy\":\"{p}\",");
                    sb.Append($"\"dps\":{Arr(MapFloors.Select(f => c.Dps[f]))},");
                    sb.Append($"\"effHp\":{Arr(MapFloors.Select(f => c.EffectiveHp[f]))},");
                    sb.Append($"\"ehp\":{Arr(MapFloors.Select(f => c.Ehp[f]))},");
                    sb.Append($"\"maxHp\":{Arr(MapFloors.Select(f => c.MaxHp[f]))},");
                    sb.Append($"\"transmuted\":{F(c.Transmuted)},\"rubedos\":{F(c.Rubedos)},\"combos\":{F(c.Combinations)},\"refused\":{F(c.Refused)}");
                    sb.Append('}');
                }
            sb.Append("],");

            sb.Append("\"layers\":[");
            first = true;
            foreach (var a in Archetypes)
                foreach (var e in Elements)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(Layers(a, e));
                }
            sb.Append("],");

            sb.Append("\"attribution\":[");
            sb.Append(string.Join(",", MapBuilds().Select(Attribution)));
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// One build's power at floor 1, layer by layer: the unbuffed player, its element, the
        /// board, the gear (Max) - then the sensible ledger's multiplier at floor 100 on top.
        /// </summary>
        static string Layers(Archetype a, ElementType e)
        {
            var none = new Mods();
            Worth W(Build b) => Evaluate(b, none, _ => 0, 1);
            var bare = W(Reference());
            var element = W(Reference(e));
            var boardOnly = new Build { Name = $"{a}/{e}/board", Archetype = a, Element = e, Kit = Kit.Max };
            Spend(boardOnly);
            boardOnly.GainRatePoints = boardOnly.Stats.ElementGrowth;
            var board = W(boardOnly);
            var max = Make(a, e, Kit.Max);
            var full = W(max);
            var c = Run(max, Policy.Sensible, MapRuns);
            string L(string name, Worth w) => $"{{\"layer\":\"{name}\",\"dps\":{F(w.Dps)},\"effHp\":{F(w.EffectiveHp)}}}";
            return $"{{\"archetype\":\"{a}\",\"element\":\"{e}\",\"steps\":[{L("unbuffed", bare)},{L("element", element)}," +
                   $"{L("board", board)},{L("gear", full)}," +
                   $"{{\"layer\":\"ledger@100\",\"dps\":{F(full.Dps * c.Dps[LastFloor] / c.Dps[1])},\"effHp\":{F(full.EffectiveHp * c.EffectiveHp[LastFloor] / c.EffectiveHp[1])}}}]}}";
        }

        /// <summary>
        /// What each entry was worth in the sensible player's finished runs: taken back out of every
        /// final ledger that held it, the log change in damage and in survival, averaged over the
        /// runs that held it; with how often it was held.
        /// </summary>
        static string Attribution(Build b)
        {
            if (_attribution.TryGetValue(b.Key, out var cached)) return cached;
            var c = Run(b, Policy.Sensible, MapRuns);
            var sums = new Dictionary<string, (float Dps, float Hp, int Held, float Stacks)>();
            foreach (var ledger in c.Finals)
            {
                var whole = Evaluate(b, ledger.Current, ledger.StacksOf, LastFloor);
                foreach (var e in ledger.Held)
                {
                    var less = ledger.Without(e);
                    var w = Evaluate(b, less.Current, less.StacksOf, LastFloor);
                    sums.TryGetValue(e.Id, out var s);
                    s.Dps += Mathf.Log(Mathf.Max(1e-3f, whole.Dps) / Mathf.Max(1e-3f, w.Dps));
                    s.Hp += Mathf.Log(Mathf.Max(1e-3f, whole.EffectiveHp) / Mathf.Max(1e-3f, w.EffectiveHp));
                    s.Held++;
                    s.Stacks += ledger.StacksOf(e);
                    sums[e.Id] = s;
                }
            }
            var rows = sums.Select(kv =>
            {
                var e = ExchangeCatalog.Get(kv.Key);
                int n = Mathf.Max(1, kv.Value.Held);
                return $"{{\"id\":\"{kv.Key}\",\"name\":\"{e?.Name}\",\"kind\":\"{e?.Kind}\",\"origin\":\"{e?.Origin}\"," +
                       $"\"dps\":{F(kv.Value.Dps / n)},\"hp\":{F(kv.Value.Hp / n)}," +
                       $"\"heldShare\":{F(kv.Value.Held / (float)c.Finals.Count)},\"stacks\":{F(kv.Value.Stacks / n)}}}";
            });
            string json = $"{{\"build\":\"{b.Key}\",\"entries\":[{string.Join(",", rows)}]}}";
            _attribution[b.Key] = json;
            return json;
        }

        static string F(float v) => float.IsNaN(v) || float.IsInfinity(v) ? "0" : v.ToString("0.####", CultureInfo.InvariantCulture);
        static string Arr(IEnumerable<float> xs) => "[" + string.Join(",", xs.Select(F)) + "]";
    }
}
