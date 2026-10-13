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
            /// <summary>A Max character with no ledger, in multiples of the unbuffed player - its WORTH,
            /// sqrt(damage x survival), averaged over every build and element. Worth rather than
            /// damage because a Bulwark and an Earth character trade damage for survival by design:
            /// held to a damage band, every one of them read as a failure (2026-10-12).</summary>
            public const float Character = 4.0f;

            /// <summary>How far that average may sit from <see cref="Character"/>. How far one cell
            /// may sit from the rest is the spreads' job.</summary>
            public const float CharacterBand = 0.10f;

            /// <summary>Per build, the four elements' worth, sqrt(damage x survival): (max - min) /
            /// mean. Earth is heavy and slow and hard to kill, Air light and fast and fragile - each
            /// element's trade counts both halves.</summary>
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

            /// <summary>One release on one target, in seconds of the player's own damage, on a FRESH
            /// character - what every element's release gives everyone, before anything is invested
            /// in it.</summary>
            public const float ReleaseSecondsMin = 3f, ReleaseSecondsMax = 5f;

            /// <summary>The same on a Max Elementalist. Investing in releases pays as investing in
            /// anything else does (2026-10-12: the band used to be measured HERE, so it capped the one
            /// build whose plan is releases), up to this - past it a release is the whole game.</summary>
            public const float ReleaseSecondsInvestedMax = 10f;

            /// <summary>
            /// NO BUILD ESCAPES THE GEAR GRIND (the user's rule, 2026-10-12): what gear multiplies each
            /// build's worth by (Max over the same board with no gear, averaged over the elements) -
            /// (max - min) / mean across the builds.
            /// </summary>
            public const float GearMultiplierSpread = 0.15f;

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
            var sb = new StringBuilder("target                                   goal            now\n");

            // ---- the character, no ledger
            var cells = (from a in Archetypes from e in Elements
                         select (a, e, w: Evaluate(Make(a, e, Kit.Max), none, _ => 0, 1))).ToList();
            var reference = Evaluate(Reference(), none, _ => 0, 1);
            float WorthX(Worth w) => Mathf.Sqrt(w.Dps / reference.Dps * (w.Ehp / reference.Ehp));
            float mean = cells.Average(c => WorthX(c.w));
            Row(sb, "Max character, no ledger (x unbuffed)",
                $"{Targets.Character * (1 - Targets.CharacterBand):0.0}-{Targets.Character * (1 + Targets.CharacterBand):0.0}x",
                $"{mean:0.0}x ({cells.Min(c => WorthX(c.w)):0.0}-{cells.Max(c => WorthX(c.w)):0.0})",
                Mathf.Abs(mean - Targets.Character) <= Targets.Character * Targets.CharacterBand);

            float elementSpread = Archetypes.Max(a => Spread(cells.Where(c => c.a == a).Select(c => WorthX(c.w)).ToArray()));
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

            // Each stackable cost on the build it hurts most (an element's own costs on that
            // element) - checked on one Fire Striker, every other element's costs read as free.
            var bites = Exchange.ExchangeCatalog.All
                .Where(e => e.Kind == Exchange.ExchangeKind.Cost && e.Stackable && e.Origin == Exchange.EntryOrigin.Base)
                .Select(e => MaxBite(e, 50)).ToList();
            int severe = bites.Count(b => b >= Targets.CostAtMaxStacks);
            Row(sb, "stackable costs at max severe for Max", $"all >= {Targets.CostAtMaxStacks:P0}",
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
            var seconds = Elements.Select(e => ReleaseSeconds(Reference(e))).ToArray();
            Row(sb, "one release, fresh character", $"{Targets.ReleaseSecondsMin:0}-{Targets.ReleaseSecondsMax:0}s",
                $"{seconds.Min():0.0}-{seconds.Max():0.0}s",
                seconds.Min() >= Targets.ReleaseSecondsMin && seconds.Max() <= Targets.ReleaseSecondsMax);
            var invested = Elements.Select(e => ReleaseSeconds(Make(Archetype.Elementalist, e, Kit.Max))).ToArray();
            Row(sb, "one release, Max Elementalist", $"<= {Targets.ReleaseSecondsInvestedMax:0}s",
                $"{invested.Min():0.0}-{invested.Max():0.0}s", invested.Max() <= Targets.ReleaseSecondsInvestedMax);

            // ---- the gear grind
            var gear = Archetypes.Select(GearMultiplier).ToArray();
            Row(sb, "gear's multiplier, spread across builds", $"<= {Targets.GearMultiplierSpread:P0}",
                $"{Spread(gear):P0} (x{gear.Min():0.00}-{gear.Max():0.00})", Spread(gear) <= Targets.GearMultiplierSpread);

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
