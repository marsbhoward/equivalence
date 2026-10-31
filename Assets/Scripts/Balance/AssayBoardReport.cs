using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Convergence.Core;
using Convergence.Progression;

namespace Convergence.Balance
{
    /// <summary>
    /// The mastery board read through its own rules: what a capped board can hold, and what the
    /// principle chains' thresholds allow. Proves the claims MasteryBoard and Tuning.Mastery make.
    ///
    ///     unity command eval 'return Convergence.Balance.Assay.BoardReport();'
    /// </summary>
    public static partial class Assay
    {
        /// <summary>One way to spend on one path or diagonal: a route and how far along it.</summary>
        readonly struct DomainSpend
        {
            public readonly string Branch;
            public readonly int Cost;
            public readonly int Sulfur, Mercury, Salt;
            public DomainSpend(string branch, int cost, int sulfur, int mercury, int salt)
            { Branch = branch; Cost = cost; Sulfur = sulfur; Mercury = mercury; Salt = salt; }
            public int Of(Principle p) => p == Principle.Sulfur ? Sulfur : p == Principle.Mercury ? Mercury : Salt;
        }

        /// <summary>A node's principle on a route whose forks took <paramref name="lane1"/> and
        /// <paramref name="lane2"/> - what BoardState.PrincipleOf reads once those lanes are owned.</summary>
        static Principle RoutePrinciple(MasteryNode n, int lane1, int lane2)
        {
            if (!n.FollowsLane) return n.Principle;
            var rule = MasteryBoard.LaneRule(n, n.RuleSlot == 0 ? lane1 : lane2);
            return rule != Notable.None ? MasteryBoard.PrincipleOf(rule) : n.Principle;
        }

        /// <summary>Every legal spend on one path (any prefix of any lane pair's route) or diagonal
        /// (any prefix). The bridges are left out - they only add ways in, not points.</summary>
        static List<DomainSpend> Options(string branch)
        {
            var options = new List<DomainSpend> { new DomainSpend("", 0, 0, 0, 0) };
            int lanes = MasteryBoard.PathOf(branch)?.Lanes.Length ?? 1;
            for (int l1 = 0; l1 < lanes; l1++)
            for (int l2 = 0; l2 < lanes; l2++)
            {
                int cost = 0, su = 0, me = 0, sa = 0;
                foreach (var node in MasteryBoard.Route(branch, l1, l2))
                {
                    cost += node.Cost;
                    switch (RoutePrinciple(node, l1, l2))
                    {
                        case Principle.Sulfur: su += node.PrincipleWeight; break;
                        case Principle.Mercury: me += node.PrincipleWeight; break;
                        default: sa += node.PrincipleWeight; break;
                    }
                    options.Add(new DomainSpend(branch, cost, su, me, sa));
                }
            }
            return options;
        }

        static IEnumerable<string> BoardBranches
            => MasteryBoard.Paths.Select(p => p.Id).Concat(MasteryBoard.Diagonals.Select(d => d.Id));

        /// <summary>
        /// The most of principle <paramref name="a"/> a board of <paramref name="cap"/> levels can
        /// hold while holding at least <paramref name="floorB"/> of principle <paramref name="b"/>
        /// (-1 when that floor cannot be met). Exact over every path's and diagonal's legal spends
        /// (bridges aside), by DP over the budget.
        /// </summary>
        static int MaxPrinciple(Principle a, Principle b, int floorB, int cap)
        {
            // best[budget][pointsOfB capped at floorB] = most of A.
            var best = new Dictionary<(int Cost, int B), int> { [(0, 0)] = 0 };
            foreach (var branch in BoardBranches)
            {
                var next = new Dictionary<(int, int), int>();
                var options = Options(branch);
                foreach (var kv in best)
                foreach (var o in options)
                {
                    int cost = kv.Key.Cost + o.Cost;
                    if (cost > cap) continue;
                    int pb = Mathf.Min(floorB, kv.Key.B + o.Of(b));
                    int pa = kv.Value + o.Of(a);
                    var key = (cost, pb);
                    if (!next.TryGetValue(key, out var have) || pa > have) next[key] = pa;
                }
                best = next;
            }
            int answer = -1;
            foreach (var kv in best) if (kv.Key.B >= floorB && kv.Value > answer) answer = kv.Value;
            return answer;
        }

        public static string BoardReport()
        {
            var sb = new StringBuilder();
            var all = MasteryBoard.All;
            int pathLevels = MasteryBoard.Route(MasteryBoard.Paths[0].Id).Sum(n => n.Cost);
            int buyable = BoardBranches.Sum(b => Options(b).Max(o => o.Cost))
                        + all.Where(n => n.Branch.StartsWith("bridge.")).Sum(n => n.Cost);
            sb.Append($"Mastery grid v{MasteryBoard.Version}: {all.Count} nodes - {MasteryBoard.Paths.Length} paths, {MasteryBoard.Diagonals.Length} diagonals, their bridges and the centre.\n");
            sb.Append($"One path end to end: {pathLevels} levels. Buyable on one board (one lane a fork): {buyable}; the cap of {Tuning.Mastery.LevelCap} covers {(float)Tuning.Mastery.LevelCap / buyable:P0}.\n\n");

            sb.Append($"{"path",-11} {"trunk",-22} {"lanes",-62} {"convergence",-14} capstone\n");
            foreach (var p in MasteryBoard.Paths)
            {
                string lanes = string.Join(" / ", p.Lanes.Select(l => l.Name));
                string conv = p.FollowsLane ? "(the lane's)" : MasteryBoard.NameOf(p.Convergence);
                string cap2 = p.FollowsLane ? "(the lane's)" : MasteryBoard.NameOf(p.Capstone);
                sb.Append($"{p.Name,-11} {p.Trunk.Name,-22} {lanes,-62} {conv,-14} {cap2}\n");
            }
            foreach (var d in MasteryBoard.Diagonals)
                sb.Append($"{d.Name,-11} {(d.A.Name.Length > 0 ? d.A.Name + " / " + d.B.Name : "Rift Capacity x2"),-85} {(d.End != Notable.None ? MasteryBoard.NameOf(d.End) : "-")}\n");

            int cap = Tuning.Mastery.LevelCap;
            var t = MasteryBoard.Thresholds;
            int last = t[t.Length - 1];
            sb.Append($"\nPrinciple chains, thresholds {string.Join("/", t)} - at the cap of {cap}:\n");
            var ps = new[] { Principle.Sulfur, Principle.Mercury, Principle.Salt };
            foreach (var p in ps)
            {
                int alone = MaxPrinciple(p, p, 0, cap);
                sb.Append($"  {p,-8} most held alone {alone,3}  (chain complete: {(alone >= last ? "yes" : "NO")})");
                foreach (var q in ps.Where(q => q != p))
                {
                    int withQ = MaxPrinciple(p, q, last, cap);
                    sb.Append($"   with {q} complete: {(withQ < 0 ? "-" : withQ.ToString())}");
                }
                sb.Append('\n');
            }
            bool twoComplete = ps.Any(p => ps.Any(q => q != p && MaxPrinciple(p, q, last, cap) >= last));
            sb.Append(twoComplete
                ? "  !! some spend of the cap completes TWO chains - raise the last threshold.\n"
                : "  no spend of the cap completes two chains.\n");
            return sb.ToString();
        }
    }
}
