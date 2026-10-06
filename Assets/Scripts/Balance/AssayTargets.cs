using System;
using System.Linq;
using System.Text;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Balance
{
    public static partial class Assay
    {
        /// <summary>
        /// What the rebalance aims at, calculated from the 2026-10-05 baseline
        /// (docs/balance/2026-10-05-baseline.md) and the rules set for it:
        ///
        ///   PROGRESSION OUTWEIGHS A RUN. A maxed character with no boons (x4) must beat a fresh one
        ///   carrying a whole run's ledger (x1.5, the Vessel at mastery level 0), or boons would let
        ///   a new player skip gear and the board.
        ///
        ///   THE LEDGER STILL MATTERS. A sensible run takes a maxed character a further x2.5 -
        ///   below today's x3.7, because the deep floors are priced against it and the gap between
        ///   a lucky ledger and an ordinary one becomes the gap between a fair floor and a wall.
        ///
        ///   NO ELEMENT AHEAD, NO BUILD AHEAD, NO FREE COSTS. Spreads are bounded, and the
        ///   damage-only player may beat the sensible one by a tenth at most - past that, some cost
        ///   is too cheap.
        ///
        /// <see cref="Scorecard"/> checks every one. They are the phase gates.
        /// </summary>
        public static class Targets
        {
            /// <summary>A Max character with no ledger, in multiples of the unbuffed player.</summary>
            public const float Character = 4.0f;

            /// <summary>How far any build on any element may sit from <see cref="Character"/>.</summary>
            public const float CharacterBand = 0.10f;

            /// <summary>Per build, the four elements' damage: (max - min) / mean.</summary>
            public const float ElementSpread = 0.10f;

            /// <summary>Across builds, sqrt(damage x survival) - a bulwark trades damage for
            /// survival rather than simply losing: (max - min) / mean.</summary>
            public const float BuildSpread = 0.15f;

            /// <summary>The sensible ledger's damage at floor 100, x floor 1, on a Max character.</summary>
            public const float LedgerSensible = 2.5f;

            /// <summary>The same for random picks.</summary>
            public const float LedgerRandom = 1.6f;

            /// <summary>The most the damage-only player may out-damage the sensible one.</summary>
            public const float GreedyOverSensible = 1.10f;

            /// <summary>Every slot at three stars over the Max kit, no ledger.</summary>
            public const float CeilingDps = 1.25f, CeilingSurvival = 1.40f;

            /// <summary>One release on one target, in seconds of the player's own damage.</summary>
            public const float ReleaseSecondsMin = 3f, ReleaseSecondsMax = 5f;

            /// <summary>The most healing per second, as a share of max health.</summary>
            public const float SustainCap = 0.025f;

            /// <summary>Ordinary Chaser hits a Max character survives on floor 100, no ledger - met by
            /// pricing enemy damage against effective health.</summary>
            public const float HitsToDieDeep = 5f;

            /// <summary>A whole run's sensible ledger on an unlevelled character, x floor 1 - the
            /// Vessel at mastery level 0.</summary>
            public const float NewPlayerLedger = 1.5f;

            /// <summary>
            /// The least a cost held at max stacks may take off what it hits (damage or survival) on a
            /// Max character. Stacking the wrong cost must be a severe challenge to a skilled,
            /// maxed-out player - without challenge there is no fun. Its Nigredo takes more on top.
            /// </summary>
            public const float CostAtMaxStacks = 0.33f;
        }

        /// <summary>
        /// Every target against the model's current numbers. Heavy - its simulations are cached,
        /// so if the first call hits the 5s eval limit, call it again.
        /// </summary>
        public static string Scorecard()
        {
            var none = new Exchange.Mods();
            float unit = Unit;
            var sb = new StringBuilder("target                                   goal            now\n");

            // ---- the character, no ledger
            var cells = (from a in Archetypes from e in Elements
                         select (a, e, w: Evaluate(Make(a, e, Kit.Max), none, _ => 0, 1))).ToList();
            float lo = cells.Min(c => c.w.Dps) / unit, hi = cells.Max(c => c.w.Dps) / unit;
            Row(sb, "Max character, no ledger (x unbuffed)",
                $"{Targets.Character * (1 - Targets.CharacterBand):0.0}-{Targets.Character * (1 + Targets.CharacterBand):0.0}x",
                $"{lo:0.0}-{hi:0.0}x",
                lo >= Targets.Character * (1 - Targets.CharacterBand) && hi <= Targets.Character * (1 + Targets.CharacterBand));

            float elementSpread = Archetypes.Max(a => Spread(cells.Where(c => c.a == a).Select(c => c.w.Dps).ToArray()));
            Row(sb, "element spread, worst build", $"<= {Targets.ElementSpread:P0}", $"{elementSpread:P0}",
                elementSpread <= Targets.ElementSpread);

            var buildWorth = Archetypes.Select(a => cells.Where(c => c.a == a)
                .Average(c => Mathf.Sqrt(c.w.Dps * c.w.Ehp))).ToArray();
            float buildSpread = Spread(buildWorth);
            Row(sb, "build spread, sqrt(damage x survival)", $"<= {Targets.BuildSpread:P0}", $"{buildSpread:P0}",
                buildSpread <= Targets.BuildSpread);

            // ---- the ledger, on the Striker across the four elements
            float Mean(Policy p) => Elements.Average(e =>
            {
                var c = Run(Make(Archetype.Striker, e, Kit.Max), p, 60);
                return c.Dps[LastFloor] / c.Dps[1];
            });
            float sensible = Mean(Policy.Sensible), random = Mean(Policy.Random), greedy = Mean(Policy.Greedy);
            Row(sb, "sensible ledger @100 (x floor 1)", $"{Targets.LedgerSensible:0.0}x", $"{sensible:0.00}x",
                Mathf.Abs(sensible - Targets.LedgerSensible) <= 0.25f);
            Row(sb, "random ledger @100", $"{Targets.LedgerRandom:0.0}x", $"{random:0.00}x",
                Mathf.Abs(random - Targets.LedgerRandom) <= 0.2f);
            Row(sb, "damage-only over sensible", $"<= {Targets.GreedyOverSensible:0.00}x", $"{greedy / sensible:0.00}x",
                greedy / sensible <= Targets.GreedyOverSensible);

            var bites = CostBites(Make(Archetype.Striker, ElementType.Fire, Kit.Max), 50);
            int severe = bites.Count(r => r.Bite >= Targets.CostAtMaxStacks);
            Row(sb, "costs at max stacks severe for Max", $"all >= {Targets.CostAtMaxStacks:P0}",
                $"{severe} of {bites.Count}", severe == bites.Count);

            var fresh = Run(Reference(ElementType.Fire), Policy.Sensible, 60);
            float freshLedger = fresh.Dps[LastFloor] / fresh.Dps[1];
            Row(sb, "new player's whole-run ledger", $"<= {Targets.NewPlayerLedger:0.0}x", $"{freshLedger:0.00}x",
                freshLedger <= Targets.NewPlayerLedger);

            // ---- the ceiling
            float ceilDps = 0f, ceilHp = 0f;
            foreach (var (a, e, w) in cells)
            {
                var top = Evaluate(Make(a, e, Kit.Ceiling), none, _ => 0, 1);
                ceilDps = Mathf.Max(ceilDps, top.Dps / w.Dps);
                ceilHp = Mathf.Max(ceilHp, top.Ehp / w.Ehp);
            }
            Row(sb, "Ceiling over Max, worst cell", $"<= {Targets.CeilingDps:0.00} / {Targets.CeilingSurvival:0.00}",
                $"{ceilDps:0.00} / {ceilHp:0.00}", ceilDps <= Targets.CeilingDps && ceilHp <= Targets.CeilingSurvival);

            // ---- releases
            var seconds = Elements.Select(e => ReleaseSeconds(Make(Archetype.Elementalist, e, Kit.Max))).ToArray();
            Row(sb, "one release, seconds of own damage", $"{Targets.ReleaseSecondsMin:0}-{Targets.ReleaseSecondsMax:0}s",
                $"{seconds.Min():0.0}-{seconds.Max():0.0}s",
                seconds.Min() >= Targets.ReleaseSecondsMin && seconds.Max() <= Targets.ReleaseSecondsMax);

            // ---- survival
            float sustain = 0f;
            foreach (var a in Archetypes)
                foreach (var e in Elements)
                {
                    var c = Run(Make(a, e, Kit.Max), Policy.Sensible, 60);
                    for (int f = 1; f <= LastFloor; f++)
                        sustain = Mathf.Max(sustain, c.Sustain[f] / Mathf.Max(1f, c.MaxHp[f]));
                }
            Row(sb, "most healing per second (of max HP)", $"<= {Targets.SustainCap:P1}", $"{sustain:P1}",
                sustain <= Targets.SustainCap * 1.001f);   // a float's width of slack over the cap

            var deep = cells.Select(c => Evaluate(Make(c.a, c.e, Kit.Max), none, _ => 0, LastFloor).Ehp
                                         / ChaserHit(LastFloor)).ToArray();
            Row(sb, "Chaser hits to die @100, no ledger", $"~{Targets.HitsToDieDeep:0}",
                $"{deep.Min():0.0}-{deep.Max():0.0}",
                deep.Min() >= Targets.HitsToDieDeep * 0.8f && deep.Max() <= Targets.HitsToDieDeep * 1.25f);

            return sb.ToString();
        }

        static void Row(StringBuilder sb, string what, string goal, string now, bool pass)
            => sb.Append($"{(pass ? "PASS" : "    ")} {what,-36} {goal,-15} {now}\n");
    }
}
