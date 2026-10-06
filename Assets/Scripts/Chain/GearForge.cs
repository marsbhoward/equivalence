using System;
using System.Collections.Generic;
using System.Linq;
using Convergence.Art.Gear;

namespace Convergence.Chain
{
    /// <summary>
    /// The Forge's rules for combining and re-rolling minted gear, apart from any screen.
    ///
    /// Combining is split into a PLAN and an EXECUTE on purpose. The plan is everything that is
    /// certain - which sub-stats carry over and at what value, what the stars scale to, how many
    /// slots will be randomized - and is what the preview shows. The randomized sub-stats are
    /// rolled only in Execute, at the moment of commit, from fresh randomness. If the preview knew
    /// them, closing and reopening the menu would be a free re-roll until the stat came up right.
    /// </summary>
    public static class GearForge
    {
        /// <summary>One sub-stat that survives a combine. Value is the UNSCALED roll kept (the
        /// higher of the matched pair); on a promotion it is re-rolled into the new tier's range
        /// instead, so only the kind is certain.</summary>
        public readonly struct Carried
        {
            public readonly StatKind Kind;
            public readonly float Value;
            public Carried(StatKind kind, float value) { Kind = kind; Value = value; }
        }

        public class CombinePlan
        {
            public MintedGearRecord A, B;
            public LootTier FromTier, ToTier;
            public int FromLevel, ToLevel;
            public bool Promotes => ToTier != FromTier;
            public readonly List<Carried> Carried = new();
            /// <summary>Sub-stat slots rolled fresh at commit - shown only as "??? (?-?)".</summary>
            public int Randomized;
        }

        /// <summary>Whether a piece takes part in combining at all. Relics hold no stats and never
        /// combine; Diamond/Black Diamond are cosmetic; a pre-sub-stat record with no known primary
        /// can't be matched against anything.</summary>
        public static bool Combinable(MintedGearRecord r)
            => r != null && r.Slot != GearSlot.Relic && r.PrimaryStat != StatKind.None &&
               r.Tier is LootTier.Bronze or LootTier.Silver or LootTier.Gold &&
               !(r.Tier == LootTier.Gold && r.UpgradeLevel >= GearRoller.MaxLevel);

        /// <summary>
        /// Two pieces combine only if this matches: slot, tier, star level and primary stat - plus
        /// the weapon class on a weapon (it decides how the thing is held) and the defensive
        /// ability on a torso (otherwise one of the two abilities would silently vanish).
        /// </summary>
        public static string MatchKey(MintedGearRecord r)
        {
            string cls = r.Slot == GearSlot.Weapon ? r.Class.ToString() : "-";
            string ability = r.Slot == GearSlot.Torso ? r.DefensiveAbility.ToString() : "-";
            return $"{r.Slot}|{r.Tier}|{r.UpgradeLevel}|{r.PrimaryStat}|{cls}|{ability}";
        }

        /// <summary>Every group of two or more combinable, unequipped pieces sharing a MatchKey.</summary>
        public static List<List<MintedGearRecord>> Groups(IEnumerable<MintedGearRecord> gear,
                                                          Func<string, bool> isEquipped)
            => (gear ?? Enumerable.Empty<MintedGearRecord>())
                .Where(r => Combinable(r) && (isEquipped == null || !isEquipped(r.InstanceId)))
                .GroupBy(MatchKey)
                .Select(g => g.ToList())
                .Where(g => g.Count >= 2)
                .ToList();

        /// <summary>
        /// The certain half of combining <paramref name="a"/> with <paramref name="b"/>, or null
        /// if they may not combine.
        ///
        /// Matching is per OCCURRENCE: sub-stats may repeat, so two Crit rolls only both carry
        /// over if the other piece also has two. For each kind, the carried values are the
        /// highest of everything either piece rolled of it - "keep the higher," applied to
        /// every matched copy at once.
        /// </summary>
        public static CombinePlan Plan(MintedGearRecord a, MintedGearRecord b)
        {
            if (a == null || b == null || a == b || !Combinable(a) || !Combinable(b)) return null;
            if (MatchKey(a) != MatchKey(b)) return null;

            var plan = new CombinePlan { A = a, B = b, FromTier = a.Tier, FromLevel = a.UpgradeLevel };
            if (a.UpgradeLevel >= GearRoller.MaxLevel)
            {
                plan.ToTier = a.Tier == LootTier.Bronze ? LootTier.Silver : LootTier.Gold;
                plan.ToLevel = 0;
            }
            else
            {
                plan.ToTier = a.Tier;
                plan.ToLevel = a.UpgradeLevel + 1;
            }

            var subsA = (a.SubStats ?? new List<SubStat>()).Where(s => s != null).ToList();
            var subsB = (b.SubStats ?? new List<SubStat>()).Where(s => s != null).ToList();
            foreach (var kind in subsA.Select(s => s.Kind).Distinct())
            {
                int matched = Math.Min(subsA.Count(s => s.Kind == kind), subsB.Count(s => s.Kind == kind));
                if (matched == 0) continue;
                foreach (var value in subsA.Concat(subsB).Where(s => s.Kind == kind)
                                           .Select(s => s.Value).OrderByDescending(v => v).Take(matched))
                    plan.Carried.Add(new Carried(kind, value));
            }

            plan.Randomized = Math.Max(0, GearRoller.SubStatCount(plan.ToTier) - plan.Carried.Count);
            return plan;
        }

        /// <summary>
        /// Builds the combined piece. The randomized slots - and, on a promotion, the carried
        /// kinds' new values - are rolled HERE, never earlier. Does not touch the profile: the
        /// caller removes the two inputs and adds the result.
        /// </summary>
        public static MintedGearRecord Execute(CombinePlan plan, Random rng)
        {
            var a = plan.A;
            var subs = new List<SubStat>();
            foreach (var c in plan.Carried)
                subs.Add(new SubStat(c.Kind, plan.Promotes
                    ? GearRoller.RollSubValue(c.Kind, plan.ToTier, rng)
                    : c.Value));
            for (int i = 0; i < plan.Randomized; i++)
            {
                var sub = GearRoller.RollSubStat(a.Slot, plan.ToTier, rng, a.Class);
                if (sub != null) subs.Add(sub);
            }

            var record = new MintedGearRecord
            {
                InstanceId = $"COMBINE-{a.Slot}-{Guid.NewGuid():N}",
                DisplayName = $"{plan.ToTier} {a.Slot}",
                Slot = a.Slot,
                Tier = plan.ToTier,
                PrimaryStat = a.PrimaryStat,
                DefensiveAbility = a.DefensiveAbility,
                Finisher = a.Finisher,
                Class = a.Class,
                UpgradeLevel = plan.ToLevel,
                SubStats = subs,
                StatsVersion = GearRoller.TablesVersion,
            };
            record.RebuildGrants();
            return record;
        }

        /// <summary>Whether sub-stat <paramref name="index"/> of <paramref name="r"/> can be
        /// re-rolled - relics and the cosmetic tiers have nothing to re-roll.</summary>
        public static bool CanReroll(MintedGearRecord r, int index)
            => r != null && r.Tier is LootTier.Bronze or LootTier.Silver or LootTier.Gold &&
               r.SubStats != null && index >= 0 && index < r.SubStats.Count && r.SubStats[index] != null;

        /// <summary>
        /// Re-rolls one sub-stat in place. <paramref name="changeStat"/> false keeps the kind and
        /// draws a new value; true draws a new kind (never the one it was, when the pool has
        /// another - the player paid double to CHANGE it) and a value for it. Either can come
        /// out worse: the player makes the gamble.
        /// </summary>
        public static void Reroll(MintedGearRecord r, int index, bool changeStat, Random rng)
        {
            if (!CanReroll(r, index)) return;
            var current = r.SubStats[index];

            if (!changeStat)
            {
                current.Value = GearRoller.RollSubValue(current.Kind, r.Tier, rng);
            }
            else
            {
                SubStat next = null;
                for (int attempt = 0; attempt < 32; attempt++)
                {
                    next = GearRoller.RollSubStat(r.Slot, r.Tier, rng, r.Class);
                    if (next == null || next.Kind != current.Kind) break;
                }
                if (next != null) r.SubStats[index] = next;
            }
            r.RebuildGrants();
        }

        /// <summary>
        /// Carries a record rolled against older gear tables onto the current ones: each stored
        /// sub-stat keeps its PLACE in its range (a roll 70% of the way up its old range lands 70%
        /// of the way up its new one), then Grants is rebuilt, which re-values the primary and the
        /// floor from the current tables. Stamps the current version, so it runs once per record;
        /// deterministic, so a load that never saves migrates the same way next time.
        ///
        /// Run at load BEFORE <see cref="EnsureSubStats"/>: a backfilled sub-stat is rolled on the
        /// current tables and must not be remapped as if it were old.
        ///
        /// A sub-stat whose kind left its slot's pool stays - the stat still exists and the roll is
        /// the item's; the pool only governs new rolls. A record with no known primary keeps its
        /// stored Grants (nobody can say what its primary was, so rebuilding would erase it).
        /// </summary>
        public static bool Migrate(MintedGearRecord r)
        {
            if (r == null || r.StatsVersion >= GearRoller.TablesVersion) return false;

            if (r.SubStats != null)
                foreach (var sub in r.SubStats)
                {
                    if (sub == null || sub.Kind == StatKind.None) continue;
                    var (oldMin, oldMax) = GearRoller.SubStatRangeAt(r.StatsVersion, sub.Kind, r.Tier);
                    var (newMin, newMax) = GearRoller.SubStatRange(sub.Kind, r.Tier);
                    float t = oldMax > oldMin ? Math.Clamp((sub.Value - oldMin) / (oldMax - oldMin), 0f, 1f) : 0.5f;
                    sub.Value = (float)Math.Round(newMin + t * (newMax - newMin), 1);
                }

            r.StatsVersion = GearRoller.TablesVersion;
            if (r.PrimaryStat != StatKind.None && r.Slot != GearSlot.Relic) r.RebuildGrants();
            return true;
        }

        /// <summary>
        /// Tops up a record that has fewer sub-stats than its tier carries - everything minted
        /// before sub-stats existed. Seeded off the instance id, so the backfill is the same every
        /// load until the next save writes it down.
        /// </summary>
        public static bool EnsureSubStats(MintedGearRecord r)
        {
            if (r == null || r.Slot == GearSlot.Relic) return false;
            r.SubStats ??= new List<SubStat>();
            int want = GearRoller.SubStatCount(r.Tier);
            if (r.SubStats.Count >= want) return false;

            var rng = new Random((r.InstanceId ?? "").GetHashCode() ^ 0x5B57A7);
            while (r.SubStats.Count < want)
            {
                var sub = GearRoller.RollSubStat(r.Slot, r.Tier, rng, r.Class);
                if (sub == null) break;
                r.SubStats.Add(sub);
            }
            r.RebuildGrants();
            return true;
        }

        // ------------------------------------------------------------------ fusion

        /// <summary>
        /// A FUSION: specific designs burned together into a Black Diamond weapon and its relic.
        ///
        /// Not a combine. Combining matches pieces by what they ARE statistically and hands back a
        /// better one of the same; a fusion matches pieces by which DESIGN they wear, consumes all
        /// of them, and mints something no box drops at all. The weapon and relic come out as a
        /// pair because that is what every Black Diamond item is - the relic carries the finisher,
        /// the weapon the look (see "Black diamond: relic and cosmetic" in CLAUDE.md).
        /// </summary>
        public class Fusion
        {
            public string Id, Name;
            /// <summary>The weapon class the set is for. Every class gets its own set (Greatsword:
            /// Tria Prima; Disc and Bow sets still to be designed) - the parts are all one class,
            /// so the fused weapon is held the same way they are.</summary>
            public WeaponClass Class;
            public string[] Inputs;
            /// <summary>Relic may be NULL: a set whose signature finisher is not built yet fuses
            /// into the weapon alone until it is.</summary>
            public string Weapon, Relic;
        }

        /// <summary>Every fusion the Forge knows - one per weapon class once the Disc and Bow sets
        /// exist. Adding one is content only: three authored Diamond designs, a ForgeOnly Black
        /// Diamond weapon and relic sharing a SignatureFinisher, and an entry here.</summary>
        public static readonly Fusion[] Fusions =
        {
            new Fusion
            {
                Id = "tria_prima", Name = "Tria Prima", Class = WeaponClass.Greatsword,
                Inputs = new[] { DemoGear.SulfurRipsawId, DemoGear.SaltPacemakerId, DemoGear.MercuryReactorId },
                Weapon = DemoGear.TriaPrimaId, Relic = DemoGear.TriaPrimaRelicId,
            },
            new Fusion
            {
                // Four element discs into one pair holding two halves - Rising and Falling - and
                // the Seal carrying Quintessence.
                Id = "armillary", Name = "Armillary", Class = WeaponClass.Disc,
                Inputs = new[] { DemoGear.IgnisBandId, DemoGear.AerBandId, DemoGear.AquaBandId, DemoGear.TerraBandId },
                Weapon = DemoGear.ArmillaryId, Relic = DemoGear.ArmillaryRelicId,
            },
        };

        public static Fusion FusionById(string id) => Array.Find(Fusions, f => f.Id == id);

        /// <summary>
        /// The pieces a fusion would burn - one unequipped minted instance per input design, in
        /// input order, with null where the player holds none. Equipped pieces are skipped for
        /// the same reason combining skips them: burning what you are wearing would change a
        /// loadout from inside a menu about something else.
        /// </summary>
        public static MintedGearRecord[] FusionInputs(Fusion fusion, IEnumerable<MintedGearRecord> gear,
                                                     Func<string, bool> isEquipped)
        {
            var pool = (gear ?? Enumerable.Empty<MintedGearRecord>())
                .Where(r => r != null && (isEquipped == null || !isEquipped(r.InstanceId))).ToList();
            var picked = new MintedGearRecord[fusion.Inputs.Length];
            for (int i = 0; i < picked.Length; i++)
            {
                picked[i] = pool.Find(r => r.Design == fusion.Inputs[i]);
                if (picked[i] != null) pool.Remove(picked[i]);
            }
            return picked;
        }

        public static bool CanFuse(Fusion fusion, IEnumerable<MintedGearRecord> gear, Func<string, bool> isEquipped)
            => fusion != null && FusionInputs(fusion, gear, isEquipped).All(r => r != null);

        /// <summary>
        /// The weapon and relic a fusion mints. Pure - the caller removes the inputs and adds
        /// these. Both carry no stats: they are Black Diamond, where the tier buys a look and a
        /// locked finisher rather than numbers.
        /// </summary>
        public static (MintedGearRecord Weapon, MintedGearRecord Relic) ExecuteFusion(Fusion fusion)
        {
            string stamp = Guid.NewGuid().ToString("N");
            var weapon = DesignDrops.RecordFor(GearCatalog.Get(fusion.Weapon), $"FUSE-{fusion.Id}-{stamp}");
            var relicDesign = string.IsNullOrEmpty(fusion.Relic) ? null : GearCatalog.Get(fusion.Relic);
            var relic = relicDesign != null
                ? DesignDrops.RecordFor(relicDesign, $"FUSE-{fusion.Id}-relic-{stamp}") : null;
            return (weapon, relic);
        }

        /// <summary>A stat's name as a player reads it.</summary>
        public static string Label(StatKind kind) => kind switch
        {
            StatKind.AttackSpeed => "Attack Speed",
            StatKind.MoveSpeed => "Move Speed",
            StatKind.MaxHp => "Max HP",
            // Shown as Sturdiness: it slows the armour pool's wear, it has never resisted damage.
            StatKind.DamageResistance => "Sturdiness",
            StatKind.ElementGrowth => "Element Growth",
            StatKind.AbilityCooldownReduction => "Ability Cooldown",
            StatKind.FinisherPower => "Weapon Art Power",
            StatKind.FinisherKnockback => "Weapon Art Knockback",
            StatKind.CritChance => "Crit Chance",
            StatKind.CritDamage => "Crit Damage",
            StatKind.ElementalEffectiveness => "Elemental Power",
            StatKind.ComboTime => "Combo Time",
            StatKind.AoeRadius => "Area",
            StatKind.HealReceived => "Heal Received",
            StatKind.RepairReceived => "Repair Received",
            StatKind.Accuracy => "Accuracy",
            _ => kind.ToString(),
        };
    }
}
