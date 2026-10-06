using System;
using System.Linq;
using System.Text;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.Balance
{
    /// <summary>
    /// The gear tables, read through the model: what each stat rolls, where, how far a targeted piece
    /// carries it toward its knee, and what one roll of it is worth on the Max player.
    ///
    ///     unity command eval 'return Convergence.Balance.Assay.GearTable();'
    ///     unity command eval 'return Convergence.Balance.Assay.GearWorth();'
    /// </summary>
    public static partial class Assay
    {
        /// <summary>Every rollable stat, in the order the reports list them.</summary>
        static readonly StatKind[] Rollable =
        {
            StatKind.Damage, StatKind.AttackSpeed, StatKind.CritChance, StatKind.CritDamage, StatKind.Accuracy,
            StatKind.FinisherPower, StatKind.FinisherKnockback, StatKind.Range, StatKind.AoeRadius,
            StatKind.Cleave, StatKind.Splash, StatKind.Pierce, StatKind.ComboTime,
            StatKind.ElementalEffectiveness, StatKind.ElementGrowth,
            StatKind.MaxHp, StatKind.Resilience, StatKind.Graze, StatKind.Brace, StatKind.MoveSpeed,
            StatKind.AbilityCooldownReduction, StatKind.Armor, StatKind.DamageResistance,
            StatKind.Mend, StatKind.HealReceived, StatKind.RepairReceived,
        };

        /// <summary>The stats the model prices in damage, and in effective health.</summary>
        static readonly StatKind[] DamageStats =
        {
            StatKind.Damage, StatKind.AttackSpeed, StatKind.CritChance, StatKind.CritDamage, StatKind.Accuracy,
            StatKind.FinisherPower, StatKind.ElementalEffectiveness, StatKind.ElementGrowth,
        };
        static readonly StatKind[] SurvivalStats = { StatKind.MaxHp, StatKind.Resilience, StatKind.Graze, StatKind.Brace };

        /// <summary>
        /// Every rollable stat: the slots that roll it, its scale, a Gold primary on armour and on a
        /// weapon, a fully targeted Gold three-star piece (primary and three sub-stats of it at mid
        /// range - on the weapon for a weapon-only stat), and that piece against the stat's knee.
        /// </summary>
        public static string GearTable()
        {
            var sb = new StringBuilder();
            sb.Append("Gear tables (GearRoller, tables v").Append(GearRoller.TablesVersion).Append("). Gold values, percentage points.\n");
            sb.Append($"{"stat",-20} {"slots",-36} {"scale",5} {"primary",8} {"weapon",7} {"3* targeted",11} {"knee",5} {"of knee",7}\n");
            float mid = (Tuning.GearRoll.SubStatMinFraction + Tuning.GearRoll.SubStatMaxFraction) * 0.5f;
            foreach (var k in Rollable)
            {
                var slots = GearRoller.SlotsOf(k);
                bool weaponOnly = slots.Count == 1 && slots[0] == GearSlot.Weapon;
                float armour = GearRoller.PrimaryPoints(k, LootTier.Gold, GearSlot.Head);
                float weapon = GearRoller.PrimaryPoints(k, LootTier.Gold, GearSlot.Weapon);
                float lead = weaponOnly ? weapon : armour;
                float targeted = (lead + 3f * armour * mid) * GearRoller.UpgradeScale(GearRoller.MaxLevel);
                var (knee, _) = StatCurves.CharacterCurve(k);
                string kneeText = float.IsPositiveInfinity(knee) ? "-" : knee.ToString("0");
                string ofKnee = float.IsPositiveInfinity(knee) ? "" : $"{targeted / knee:P0}";
                bool weaponRolls = slots.Contains(GearSlot.Weapon);
                sb.Append($"{GearForge.Label(k),-20} {string.Join(" ", slots),-36} {GearRoller.KindScale(k),5:0.###} " +
                          $"{(weaponOnly ? "-" : armour.ToString("0.#")),8} {(weaponRolls ? weapon.ToString("0.#") : "-"),7} " +
                          $"{targeted,11:0.#} {kneeText,5} {ofKnee,7}\n");
            }
            sb.Append("\nPool sizes: ");
            sb.Append(string.Join("  ", GearSlots.Worn.Select(s => $"{s} {GearRoller.Pool(s).Length}")));
            sb.Append($"  (bow weapon {GearRoller.Pool(GearSlot.Weapon, WeaponClass.Bow).Length})\n");
            return sb.ToString();
        }

        static Build Copy(Build b)
        {
            var c = new Build
            {
                Name = b.Name, Legacy = b.Legacy, Archetype = b.Archetype, Element = b.Element, Kit = b.Kit,
                GridHp = b.GridHp, Lifesteal = b.Lifesteal, GainRatePoints = b.GainRatePoints,
                BurnPoints = b.BurnPoints, SoakPoints = b.SoakPoints, BleedPoints = b.BleedPoints,
                StaggerPoints = b.StaggerPoints,
            };
            c.Stats.Add(b.Stats);
            c.Notables.UnionWith(b.Notables);
            return c;
        }

        /// <summary>One Gold roll of <paramref name="k"/> (an armour primary) on top of the build.</summary>
        static Build WithRoll(Build b, StatKind k)
        {
            var c = Copy(b);
            float points = GearRoller.PrimaryPoints(k, LootTier.Gold, GearSlot.Head);
            GearRoller.Add(c.Stats, k, points);
            if (k == StatKind.ElementGrowth) c.GainRatePoints += points;   // what the meter reads, as BuildPlayer folds it
            return c;
        }

        /// <summary>What one Gold roll of <paramref name="k"/> adds to the build: damage per second,
        /// or effective health for a survival stat, as a fraction.</summary>
        static float RollGain(Build b, StatKind k)
        {
            var none = new Exchange.Mods();
            var before = Evaluate(b, none, _ => 0, 1);
            var after = Evaluate(WithRoll(b, k), none, _ => 0, 1);
            bool survival = Array.IndexOf(SurvivalStats, k) >= 0;
            return survival ? after.Ehp / Mathf.Max(1e-4f, before.Ehp) - 1f
                            : after.Dps / Mathf.Max(1e-4f, before.Dps) - 1f;
        }

        /// <summary>A roll of <paramref name="k"/> against a roll of the family's reference stat (Damage,
        /// or Max HP for survival), on one archetype, averaged over the four elements.</summary>
        static float RollWorth(Archetype a, StatKind k)
        {
            bool survival = Array.IndexOf(SurvivalStats, k) >= 0;
            var reference = survival ? StatKind.MaxHp : StatKind.Damage;
            float sum = 0f;
            foreach (var e in Elements)
            {
                var b = Make(a, e, Kit.Max);
                sum += RollGain(b, k) / Mathf.Max(1e-5f, RollGain(b, reference));
            }
            return sum / Elements.Length;
        }

        /// <summary>The Striker rolling damage first (Damage, then Accuracy) and crit first (Crit
        /// Chance, then Crit Damage), each falling back to the other once saturated.</summary>
        static readonly StatKind[] DamageFirst =
        {
            StatKind.Damage, StatKind.Accuracy, StatKind.FinisherPower, StatKind.AttackSpeed,
            StatKind.CritDamage, StatKind.CritChance, StatKind.MaxHp, StatKind.Resilience, StatKind.Graze,
        };
        static readonly StatKind[] CritFirst =
        {
            StatKind.CritChance, StatKind.CritDamage, StatKind.FinisherPower, StatKind.AttackSpeed,
            StatKind.Damage, StatKind.Accuracy, StatKind.MaxHp, StatKind.Resilience, StatKind.Graze,
        };

        /// <summary>
        /// The user's rule, whole-build: a Max Striker who rolls damage first and one who rolls crit
        /// first, against the ordinary (mixed) Striker - damage per second with no ledger, per
        /// element. Within about 10% means neither is mandatory.
        /// </summary>
        public static string CritParity()
        {
            var sb = new StringBuilder("Crit never mandatory - Max Striker damage, no ledger, against the mixed Striker:\n");
            sb.Append($"{"",-14}" + string.Concat(Elements.Select(e => $" {e,7}")) + "\n");
            var none = new Exchange.Mods();
            foreach (var (name, prio) in new[] { ("damage-first", DamageFirst), ("crit-first", CritFirst) })
            {
                sb.Append($"{name,-14}");
                foreach (var e in Elements)
                {
                    float mixed = Evaluate(Make(Archetype.Striker, e, Kit.Max), none, _ => 0, 1).Dps;
                    float variant = Evaluate(MakeRolledFor(name, Archetype.Striker, prio, e, Kit.Max), none, _ => 0, 1).Dps;
                    sb.Append($" {variant / mixed,7:0.00}");
                }
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// What one Gold roll of each stat is worth on the Max player (no ledger), as a share of a
        /// roll of Damage (damage stats) or of Max HP (survival stats), per build, averaged over the
        /// four elements - the user's rule that crit is never mandatory reads off the crit family's
        /// row: a roll of Crit Chance, Crit Damage or Accuracy should be worth a roll of Damage. The
        /// last lines are the scales that would make them so on the Striker and the Tempo, the two
        /// builds that roll them; write those into GearRoller.KindScale and run again (the Max
        /// player's own gear moves with them, so it takes two or three passes to settle).
        /// </summary>
        public static string GearWorth()
        {
            var sb = new StringBuilder();
            sb.Append("One Gold roll on the Max player (no ledger), against a Gold roll of Damage - or of Max HP for survival.\n");
            sb.Append("Average over the four elements. 1.00 = worth the same.\n\n");
            sb.Append($"{"",-20} {"scale",6}" + string.Concat(Archetypes.Select(a => $" {a,12}")) + "\n");
            foreach (var k in DamageStats.Concat(SurvivalStats))
            {
                if (k == StatKind.MaxHp) sb.Append("survival\n");
                sb.Append($"{GearForge.Label(k),-20} {GearRoller.KindScale(k),6:0.###}");
                foreach (var a in Archetypes) sb.Append($" {RollWorth(a, k),12:0.00}");
                sb.Append('\n');
            }

            sb.Append("\nThe crit family on the Striker and the Tempo:");
            foreach (var k in new[] { StatKind.CritChance, StatKind.CritDamage, StatKind.Accuracy })
            {
                float w = (RollWorth(Archetype.Striker, k) + RollWorth(Archetype.Tempo, k)) * 0.5f;
                sb.Append($"\n  {GearForge.Label(k),-12} worth {w:0.00} of a Damage roll -> parity scale {GearRoller.KindScale(k) / Mathf.Max(1e-3f, w):0.##}");
            }
            sb.Append("\n\n").Append(CritParity());

            sb.Append("\n\nNot priced by the model (single target, no movement or wear): ");
            sb.Append(string.Join(", ", Rollable.Where(k => Array.IndexOf(DamageStats, k) < 0 && Array.IndexOf(SurvivalStats, k) < 0)
                                                .Select(GearForge.Label)));
            sb.Append('\n');
            return sb.ToString();
        }
    }
}
