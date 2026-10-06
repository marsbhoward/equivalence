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
        /// <summary>One way to spend on one domain: a branch and how far along it.</summary>
        readonly struct DomainSpend
        {
            public readonly string Branch;
            public readonly int Cost;
            public readonly int Sulfur, Mercury, Salt;
            public DomainSpend(string branch, int cost, int sulfur, int mercury, int salt)
            { Branch = branch; Cost = cost; Sulfur = sulfur; Mercury = mercury; Salt = salt; }
            public int Of(Principle p) => p == Principle.Sulfur ? Sulfur : p == Principle.Mercury ? Mercury : Salt;
        }

        /// <summary>Every legal spend on a domain: nothing, or any prefix of any one of its branches
        /// (the keystone first, then along the path).</summary>
        static List<DomainSpend> Options(MasteryDomain domain)
        {
            var options = new List<DomainSpend> { new DomainSpend("", 0, 0, 0, 0) };
            foreach (var branch in MasteryBoard.BranchesOf(domain))
            {
                int cost = 0, su = 0, me = 0, sa = 0;
                foreach (var node in MasteryBoard.All.Where(n => n.Branch == branch))
                {
                    cost += node.Cost;
                    switch (node.Principle)
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

        static readonly MasteryDomain[] BoardDomains =
        {
            MasteryDomain.DamageSource, MasteryDomain.Status, MasteryDomain.Survival,
            MasteryDomain.Tempo, MasteryDomain.Space, MasteryDomain.RiftCapacity,
        };

        /// <summary>
        /// The most of principle <paramref name="a"/> a board of <paramref name="cap"/> levels can
        /// hold while holding at least <paramref name="floorB"/> of principle <paramref name="b"/>
        /// (-1 when that floor cannot be met). Exact: every domain's every legal spend, by DP over
        /// the budget.
        /// </summary>
        static int MaxPrinciple(Principle a, Principle b, int floorB, int cap)
        {
            // best[budget][pointsOfB capped at floorB] = most of A.
            var best = new Dictionary<(int Cost, int B), int> { [(0, 0)] = 0 };
            foreach (var domain in BoardDomains)
            {
                var next = new Dictionary<(int, int), int>();
                foreach (var kv in best)
                foreach (var o in Options(domain))
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
            int buyable = BoardDomains.Sum(d => Options(d).Max(o => o.Cost)) + MasteryBoard.RebisCost;
            sb.Append($"Mastery board v{MasteryBoard.Version}: {all.Count} nodes, {MasteryBoard.Branches.Length} forked branches + the Rift ladder + the Rebis.\n");
            sb.Append($"Buyable on one board (one branch a forked domain): {buyable} levels; the cap of {Tuning.Mastery.LevelCap} covers {(float)Tuning.Mastery.LevelCap / buyable:P0}.\n\n");

            sb.Append($"{"branch",-12} {"principle",-9} {"primary",-26} {"secondary",-26} {"tincture",-14} opus\n");
            foreach (var spec in MasteryBoard.Branches)
            {
                var nodes = all.Where(n => n.Branch == spec.Id).ToList();
                string P(Art.Gear.StatKind k, BoardStat x)
                {
                    float v = nodes.Where(n => (k != Art.Gear.StatKind.None && n.Stat == k) || (x != BoardStat.None && n.Extra == x)).Sum(n => n.Value);
                    string label = k != Art.Gear.StatKind.None ? Chain.GearForge.Label(k) : MasteryBoard.Label(x);
                    return x == BoardStat.Lifesteal ? $"{label} +{v * 100f:0.#}%" : $"{label} +{v:0.#}";
                }
                string primary = P(spec.Primary, spec.PrimaryExtra);
                string secondary = P(spec.Secondary, BoardStat.None);
                string key = spec.Keystone != Notable.None ? $" [{spec.Keystone}]" : "";
                sb.Append($"{spec.Name + key,-12} {spec.Principle,-9} {primary,-26} {secondary,-26} {MasteryBoard.NameOf(spec.Tincture),-14} {MasteryBoard.NameOf(spec.Opus)}\n");
            }

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
