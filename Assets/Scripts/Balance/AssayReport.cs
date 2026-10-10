using System;
using System.Linq;
using System.Text;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;
using Convergence.Player;

namespace Convergence.Balance
{
    /// <summary>
    /// The Assay's readouts. Each is one eval call; the simulations are cached, so a report that
    /// times out the first time (the 5s eval limit) finishes on the second call.
    ///
    ///     unity command eval 'return Convergence.Balance.Assay.Check(Convergence.Player.PowerBuild.BuiltThree);'
    ///     unity command eval 'return Convergence.Balance.Assay.Matrix();'
    ///     unity command eval 'return Convergence.Balance.Assay.Ledger(Convergence.Balance.Archetype.Striker, Convergence.Core.ElementType.Fire);'
    ///     unity command eval 'return Convergence.Balance.Assay.Releases();'
    ///     unity command eval 'return Convergence.Balance.Assay.Builds();'
    ///     unity command eval 'return Convergence.Balance.Assay.Pricing();'
    /// </summary>
    public static partial class Assay
    {
        static readonly ElementType[] Elements = { ElementType.Fire, ElementType.Water, ElementType.Earth, ElementType.Air };
        static readonly Archetype[] Archetypes = (Archetype[])Enum.GetValues(typeof(Archetype));

        /// <summary>The unit every DPS figure is reported in: the unbuffed, element-less player on
        /// floor 1 at the game's real swing cadence.</summary>
        public static float Unit => Evaluate(Reference(), new Exchange.Mods(), _ => 0, 1).Dps;

        // ------------------------------------------------------------------ regression

        /// <summary>
        /// Replays one of PlayerPower's builds through the Assay with PlayerPower's cadence and
        /// pick rules, and compares every floor of both ledger curves. Anything but OK means the
        /// port drifted. One build per call - six full simulations will not fit one eval.
        /// </summary>
        public static string Check(PowerBuild build)
        {
            // PlayerPower predates diminishing returns, so the replay runs with every curve off -
            // which is also the proof that the stat core's plumbing changed nothing on its own.
            bool curves = StatCurves.Enabled;
            StatCurves.Enabled = false;
            try { return CheckReplay(build); }
            finally { StatCurves.Enabled = curves; }
        }

        static string CheckReplay(PowerBuild build)
        {
            var b = Legacy(build);
            float worst = 0f;
            string where = "";
            foreach (var (play, policy) in new[] { (LedgerPlay.Average, Policy.Random), (LedgerPlay.Stacked, Policy.Greedy) })
            {
                var curve = Run(b, policy, 200);
                for (int f = 1; f <= LastFloor; f++)
                {
                    float theirs = PlayerPower.Dps(build, play, f);
                    float diff = Mathf.Abs(curve.Dps[f] - theirs) / Mathf.Max(1e-4f, theirs);
                    if (diff <= worst) continue;
                    worst = diff;
                    where = $"{play} floor {f}: assay {curve.Dps[f]:0.###} vs PlayerPower {theirs:0.###}";
                }
            }
            return worst < 1e-5f
                ? $"OK - {build} replays PlayerPower on both ledgers, every floor (worst {worst:0.#######})"
                : $"MISMATCH - {build}: worst {worst:P3} at {where}";
        }

        // ------------------------------------------------------------------ the matrix

        /// <summary>
        /// Every build on every element: damage as a multiple of <see cref="Unit"/>, and survival
        /// as the number of ordinary Chaser hits on that floor it takes to die (effective health,
        /// no sustain). The spread column is how far apart the four elements are for one build.
        /// </summary>
        public static string Matrix(Kit kit = Kit.Max, Policy policy = Policy.Sensible, int runs = 60)
        {
            var sb = new StringBuilder();
            float unit = Unit;
            sb.Append($"{kit} kit, {policy} ledger. DPS in multiples of the unbuffed player ({unit:0.0}); " +
                      "SURVIVAL in ordinary Chaser hits to die; SUSTAIN in % of max HP healed per second.\n");

            foreach (int floor in new[] { 1, 25, 50, 75, 100 })
            {
                sb.Append($"\nfloor {floor}  (Chaser hit {ChaserHit(floor):0})\n");
                sb.Append("                 DPS: Fire  Water  Earth    Air  spread |  HITS: Fire  Water  Earth    Air | SUSTAIN: Fire Water Earth  Air\n");
                foreach (var a in Archetypes)
                {
                    var dps = new float[4];
                    var hits = new float[4];
                    var sus = new float[4];
                    for (int i = 0; i < 4; i++)
                    {
                        var c = Run(Make(a, Elements[i], kit), policy, runs);
                        dps[i] = c.Dps[floor] / unit;
                        hits[i] = c.Ehp[floor] / ChaserHit(floor);
                        sus[i] = 100f * c.Sustain[floor] / Mathf.Max(1f, c.MaxHp[floor]);
                    }
                    sb.Append($"{a,-13}  {dps[0],9:0.00} {dps[1],6:0.00} {dps[2],6:0.00} {dps[3],6:0.00} {Spread(dps),6:P0} | " +
                              $"{hits[0],9:0.0} {hits[1],6:0.0} {hits[2],6:0.0} {hits[3],6:0.0} | " +
                              $"{sus[0],11:0.0} {sus[1],5:0.0} {sus[2],5:0.0} {sus[3],4:0.0}\n");
                }
            }
            return sb.ToString();
        }

        /// <summary>(max - min) / mean.</summary>
        static float Spread(float[] v) => (v.Max() - v.Min()) / Mathf.Max(1e-4f, v.Average());

        /// <summary>Ceiling against Max, per build and element, with no ledger - what filling every
        /// slot at three stars buys over the balance target.</summary>
        public static string Ceiling()
        {
            var sb = new StringBuilder("Ceiling over Max, no ledger (DPS x / survival x)\n");
            sb.Append("                 Fire          Water         Earth         Air\n");
            var none = new Exchange.Mods();
            foreach (var a in Archetypes)
            {
                sb.Append($"{a,-13}");
                foreach (var e in Elements)
                {
                    var max = Evaluate(Make(a, e, Kit.Max), none, _ => 0, 50);
                    var top = Evaluate(Make(a, e, Kit.Ceiling), none, _ => 0, 50);
                    sb.Append($"  {top.Dps / max.Dps,5:0.00} / {top.Ehp / max.Ehp,4:0.00}");
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the ledger

        /// <summary>One build under all three pick policies: what the ledger multiplies damage and
        /// survival by as the run deepens, how often it refuses, and what it ends up holding.</summary>
        public static string Ledger(Archetype a, ElementType e, Kit kit = Kit.Max, int runs = 100)
        {
            var b = Make(a, e, kit);
            var sb = new StringBuilder($"{b.Key}: the ledger's effect (x floor 1)\n");
            sb.Append("policy      |  DPS @25    @50    @75   @100 | SURVIVAL @25  @50   @75  @100 | refused | transmuted rubedos combos\n");
            foreach (Policy p in Enum.GetValues(typeof(Policy)))
            {
                var c = Run(b, p, runs);
                sb.Append($"{p,-11} | {Ratio(c.Dps, 25),7:0.00} {Ratio(c.Dps, 50),6:0.00} {Ratio(c.Dps, 75),6:0.00} {Ratio(c.Dps, 100),6:0.00} |" +
                          $" {Ratio(c.EffectiveHp, 25),11:0.00} {Ratio(c.EffectiveHp, 50),5:0.00} {Ratio(c.EffectiveHp, 75),5:0.00} {Ratio(c.EffectiveHp, 100),5:0.00} |" +
                          $" {c.Refused,5:0.0}   | {c.Transmuted,10:0.0} {c.Rubedos,7:0.0} {c.Combinations,6:0.0}\n");
            }
            foreach (Policy p in new[] { Policy.Sensible, Policy.Greedy })
            {
                var c = Run(b, p, runs);
                sb.Append($"\n{p} holds by floor 100 (mean stacks / cap):\n");
                foreach (var kv in c.Held.OrderByDescending(kv => kv.Value).Take(24))
                {
                    var entry = Exchange.ExchangeCatalog.Get(kv.Key);
                    sb.Append($"  {(entry?.Kind == Exchange.ExchangeKind.Cost ? "-" : "+")} {kv.Key,-18} {kv.Value,4:0.0} / {entry?.MaxStacks}\n");
                }
            }
            return sb.ToString();
        }

        static float Ratio(float[] curve, int floor) => curve[floor] / Mathf.Max(1e-4f, curve[1]);

        /// <summary>
        /// Every cost held at its maximum stacks, alone, on one Max character: what it takes off
        /// damage and off survival (effective health, healing included). A cost that barely
        /// touches a maxed player is no challenge to the player the deep floors are built for.
        /// Withering is priced over <see cref="Assume.WitheringHorizon"/> floors held.
        /// </summary>
        public static string Costs(Archetype a = Archetype.Striker, ElementType e = ElementType.Fire, int floor = 50)
        {
            var b = Make(a, e, Kit.Max);
            var sb = new StringBuilder($"Each cost at max stacks, alone - {b.Key}, floor {floor}. What it takes off:\n");
            sb.Append("cost               stacks | damage | survival\n");
            foreach (var r in CostBites(b, floor).OrderByDescending(r => r.Bite))
                sb.Append($"{r.Id,-18} {r.Stacks,6} | {r.Dps,6:P0} | {r.Hp,8:P0}\n");
            return sb.ToString();
        }

        /// <summary>The offer-shapers change the next offer, not the character - they are judged by
        /// how they reshape the exchange, never by this.</summary>
        static readonly string[] OfferShapers = { "caput_mortuum", "indenture", "debt" };

        static System.Collections.Generic.List<(string Id, int Stacks, float Dps, float Hp, float Bite)> CostBites(Build b, int floor)
        {
            var bare = Evaluate(b, new Exchange.Mods(), _ => 0, floor);
            var rows = new System.Collections.Generic.List<(string, int, float, float, float)>();
            foreach (var entry in Exchange.ExchangeCatalog.All)
            {
                if (entry.Kind != Exchange.ExchangeKind.Cost || Array.IndexOf(OfferShapers, entry.Id) >= 0) continue;
                var ledger = new Exchange.RunModifiers();
                ledger.SetCharacter(b.Element, Art.Gear.WeaponClass.Greatsword, Tuning.Mastery.LevelCap);
                for (int i = 0; i < entry.MaxStacks; i++) ledger.Take(entry);
                // Withering bites over the floors it is carried, not the moment it is taken.
                if (entry.Id == "withering")
                    for (int f = 0; f < Assume.WitheringHorizon; f++) ledger.FloorCleared();
                var w = Evaluate(b, ledger.Current, ledger.StacksOf, floor);
                float dps = 1f - w.Dps / bare.Dps, hp = 1f - w.EffectiveHp / bare.EffectiveHp;
                rows.Add((entry.Id, entry.MaxStacks, dps, hp, 1f - (1f - dps) * (1f - hp)));   // both, as Entries
            }
            return rows;
        }

        /// <summary>
        /// What capping refusals does to the sensible player: per run, or in a row. Refusing is free
        /// today, so the sensible player skips most offers and only ever takes the good ones.
        /// </summary>
        public static string Refusals(Archetype a = Archetype.Striker, ElementType e = ElementType.Fire, int runs = 60)
        {
            var b = Make(a, e, Kit.Max);
            var sb = new StringBuilder($"{b.Key}, sensible ledger, refusals capped\n");
            sb.Append("deals            cap         refused a run | DPS @100 (x floor 1) | survival @100 (x floor 1) | Chaser hits to die @100\n");
            var caps = new (string Label, int Cap, int Streak, int Every)[]
            {
                ("none", int.MaxValue, int.MaxValue, 1), ("30 a run", 30, int.MaxValue, 1), ("20 a run", 20, int.MaxValue, 1),
                ("10 a run", 10, int.MaxValue, 1), ("2 in a row", int.MaxValue, 2, 1), ("1 in a row", int.MaxValue, 1, 1),
                ("none", int.MaxValue, int.MaxValue, 2), ("15 a run", 15, int.MaxValue, 2), ("10 a run", 10, int.MaxValue, 2),
                ("5 a run", 5, int.MaxValue, 2), ("1 in a row", int.MaxValue, 1, 2),
            };
            foreach (var (label, cap, streak, every) in caps)
            {
                var c = Run(b, Policy.Sensible, runs, cap, streak, every);
                string deals = every == 1 ? "every floor" : $"every {every} floors";
                sb.Append($"{deals,-16} {label,-11} {c.Refused,13:0.0} | {Ratio(c.Dps, LastFloor),20:0.00} | " +
                          $"{Ratio(c.EffectiveHp, LastFloor),25:0.00} | {c.Ehp[LastFloor] / ChaserHit(LastFloor),23:0.0}\n");
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ releases

        /// <summary>
        /// One release on one target, for each element on an Elementalist (who invests in it most):
        /// as seconds of that player's own sustained damage, and as a share of an ordinary
        /// Chaser's effective health as the floors deepen. A release that stays the same size while
        /// the enemies grow is the problem this shows.
        /// </summary>
        public static string Releases(Kit kit = Kit.Max)
        {
            var sb = new StringBuilder($"Releases, {kit} Elementalist, no ledger. Worth = seconds of the player's own damage one release adds (hits, surge, soak, gust).\n");
            sb.Append("element   worth | one direct hit | % of a Chaser @1   @25   @50   @100\n");
            var none = new Exchange.Mods();
            foreach (var e in Elements)
            {
                var b = Make(Archetype.Elementalist, e, kit);
                var w = Evaluate(b, none, _ => 0, 1);
                sb.Append($"{e,-8} {ReleaseSeconds(b),5:0.0}s | {w.ReleaseHit,14:0} |" +
                          $" {100f * w.ReleaseHit / ChaserEhp(1),12:0}% {100f * w.ReleaseHit / ChaserEhp(25),5:0}% " +
                          $"{100f * w.ReleaseHit / ChaserEhp(50),5:0}% {100f * w.ReleaseHit / ChaserEhp(100),5:0}%\n");
            }
            return sb.ToString();
        }

        /// <summary>
        /// The element calibration view: every build's damage on every element (Max, no ledger,
        /// floor 1, x the unbuffed player) with the spread across elements, and each element's
        /// release worth - the two numbers the element parity rule is judged on.
        /// </summary>
        public static string ElementTable(Kit kit = Kit.Max)
        {
            float unit = Unit;
            var none = new Exchange.Mods();
            var sb = new StringBuilder($"{kit}, no ledger, floor 1 - damage x unbuffed ({unit:0.0})\n");
            sb.Append("build            Fire  Water  Earth    Air  spread | release worth: Fire Water Earth  Air\n");
            foreach (var a in Archetypes)
            {
                var d = Elements.Select(e => Evaluate(Make(a, e, kit), none, _ => 0, 1).Dps / unit).ToArray();
                var r = Elements.Select(e => ReleaseSeconds(Make(a, e, kit))).ToArray();
                sb.Append($"{a,-13} {d[0],6:0.00} {d[1],6:0.00} {d[2],6:0.00} {d[3],6:0.00} {Spread(d),6:P0} |" +
                          $" {r[0],19:0.0}s {r[1],4:0.0}s {r[2],4:0.0}s {r[3],3:0.0}s\n");
            }
            sb.Append($"model numbers differing from Tuning: {ElementNumbers.Drift()}\n");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ builds

        /// <summary>What each build's Max kit is made of, and the stat block it adds up to.</summary>
        public static string Builds(ElementType e = ElementType.Fire)
        {
            var sb = new StringBuilder();
            foreach (var a in Archetypes)
            {
                var b = Make(a, e, Kit.Max);
                var s = b.Stats;
                sb.Append($"{a} ({e}, Max)\n  {string.Join("\n  ", b.Picks)}\n");
                sb.Append($"  = dmg {s.Damage:+0} spd {s.AttackSpeed:+0} crit {s.CritChance:+0.0} critdmg {s.CritDamage:+0} " +
                          $"wpnart {s.FinisherPower:+0} hp {s.MaxHp:+0}% resil {s.Resilience:+0} graze {s.Graze:+0} " +
                          $"brace {s.Brace:+0} move {s.MoveSpeed:+0} elempow {s.ElementalEffectiveness:+0} " +
                          $"meter {b.GainRatePoints:+0} lifesteal {b.Lifesteal:P0}\n\n");
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ pricing

        /// <summary>
        /// What WaveComposer prices floors against today (PlayerPower.ExpectedDps) next to the same
        /// blend at the game's real swing cadence: the ratio is how much longer today's floors take
        /// than their TargetSeconds, from the cadence alone.
        /// </summary>
        public static string Pricing()
        {
            var sb = new StringBuilder("floor | priced today | same players at real cadence | floors run longer by\n");
            foreach (int f in new[] { 1, 10, 25, 40, 50, 75, 100 })
            {
                float t = Mathf.InverseLerp(Tuning.Waves.BuiltFromFloor, Tuning.Waves.BuiltByFloor, f);
                var reference = Run(Reference(), Policy.Random, 200);
                var built = Run(Real(PowerBuild.BuiltThree), Policy.Greedy, 200);
                float real = Mathf.Lerp(reference.Dps[f], built.Dps[f], t);
                float priced = PlayerPower.ExpectedDps(f);
                sb.Append($"{f,5} | {priced,12:0.0} | {real,28:0.0} | {priced / real - 1f,10:P0}\n");
            }
            return sb.ToString();
        }

        /// <summary>A PlayerPower build at the game's real cadence (no element) - for Pricing.</summary>
        static Build Real(PowerBuild build)
            => new() { Name = $"{build}-real", Stats = PlayerPower.StatsFor(build) };
    }
}
