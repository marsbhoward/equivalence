using System;
using System.Collections.Generic;
using System.Linq;
using Convergence.Art.Gear;
using Convergence.Core;
using Convergence.Progression;
using UnityEngine;

namespace Convergence.Balance
{
    /// <summary>A playstyle, independent of element - the same build is assayed on all four.</summary>
    public enum Archetype { Striker, Tempo, Bulwark, Elementalist }

    /// <summary>How much of a character is levelled.</summary>
    public enum Kit
    {
        /// <summary>No gear and no board - the unbuffed player Tuning.Boss measures against.</summary>
        Reference,

        /// <summary>The player the deep floors are balanced against: a full board at the level
        /// cap, a Gold three-star weapon and three Gold three-star pieces, and every other slot
        /// FILLED at Gold one-star - all rolled toward the build where the slot allows.</summary>
        Max,

        /// <summary>Every slot Gold three-star and targeted - the stress test diminishing returns
        /// exist for.</summary>
        Ceiling,
    }

    /// <summary>
    /// One assayed character: its stat block and the board's contributions that do not live in
    /// it, folded the way GameBootstrap.BuildPlayer folds them.
    /// </summary>
    public sealed class Build
    {
        public string Name;

        /// <summary>One of Player.PlayerPower's builds, replayed by the regression check - no
        /// element, and PlayerPower's attack cadence.</summary>
        public bool Legacy;

        public Archetype Archetype;
        public ElementType? Element;
        public Kit Kit;

        /// <summary>Gear plus board, in percentage points.</summary>
        public StatPercents Stats = new();

        /// <summary>Flat max health on top of the percentage - none since the board went to Max HP
        /// points (2026-10-05); kept for legacy builds.</summary>
        public float GridHp;

        /// <summary>The board's lifesteal for this element, as a fraction.</summary>
        public float Lifesteal;

        /// <summary>The board's rules this build owns (BoardEffects), and its status power in
        /// points - what AssayBoard prices.</summary>
        public readonly HashSet<Notable> Notables = new();
        public float BurnPoints, SoakPoints, BleedPoints, StaggerPoints;

        /// <summary>The element meter's growth, in points: the board's meter nodes plus gear's
        /// Element Growth - the same sum BuildPlayer feeds ElementalResource.GainRateMultiplier.</summary>
        public float GainRatePoints;

        /// <summary>What the gear and the board were spent on, for the report.</summary>
        public readonly List<string> Picks = new();

        /// <summary>What the Assay caches this build's curves under - unique per build.</summary>
        public string Key => Legacy ? $"legacy/{Name}" : Name;
    }

    public static partial class Assay
    {
        /// <summary>Every slot that rolls stats, weapon aside. Trinket rolls nothing (it is slated to
        /// merge into Relic) and Relic carries a weapon art rather than stats.</summary>
        static readonly GearSlot[] ArmourSlots =
        {
            GearSlot.Head, GearSlot.Shoulders, GearSlot.Torso, GearSlot.Back, GearSlot.Neck,
            GearSlot.Ring, GearSlot.Legs, GearSlot.Boots, GearSlot.Belt, GearSlot.Gloves,
        };

        /// <summary>What each build rolls toward, best first. A slot takes the highest-ranked kind
        /// its pool offers; a kind outside the list is only taken when the pool offers nothing on it.
        /// Element-free on purpose: a build is a playstyle, played on any element.</summary>
        static readonly Dictionary<Archetype, StatKind[]> Priority = new()
        {
            { Archetype.Striker, new[] { StatKind.Damage, StatKind.CritDamage, StatKind.CritChance,
                                         StatKind.Accuracy, StatKind.FinisherPower, StatKind.AttackSpeed,
                                         StatKind.MaxHp, StatKind.Resilience, StatKind.Graze } },
            // No Move Speed: the model cannot price what it buys (positioning, kiting, Air's
            // momentum), so a Tempo spending three slots on it read as needing gear least and
            // weakest of the four (2026-10-12). Its real worth is unmeasured, not zero.
            { Archetype.Tempo, new[] { StatKind.AttackSpeed, StatKind.CritChance, StatKind.CritDamage,
                                       StatKind.Damage, StatKind.Accuracy, StatKind.Graze, StatKind.MaxHp,
                                       StatKind.Resilience } },
            { Archetype.Bulwark, new[] { StatKind.Resilience, StatKind.MaxHp, StatKind.Brace, StatKind.Graze,
                                         StatKind.Damage, StatKind.AttackSpeed, StatKind.CritChance } },
            { Archetype.Elementalist, new[] { StatKind.ElementalEffectiveness, StatKind.ElementGrowth,
                                              StatKind.Damage, StatKind.CritDamage, StatKind.AttackSpeed,
                                              StatKind.MaxHp, StatKind.Resilience, StatKind.Graze } },
        };

        /// <summary>
        /// Each build's spend of the 50 levels along the grid: whole routes in order ("path:lane1:lane2",
        /// or a diagonal's id), until the levels run out - three paths of 16 (48) and two more
        /// levels down a fourth. Lanes count from the path's left as MasteryBoard.Paths lists them.
        /// </summary>
        static readonly Dictionary<Archetype, string[]> Board = new()
        {
            { Archetype.Striker, new[] { "offense:2:1", "survival:0:0", "tempo:0:0", "element" } },
            { Archetype.Tempo, new[] { "tempo:0:0", "offense:2:2", "survival:2:2", "reach" } },
            { Archetype.Bulwark, new[] { "survival:0:3", "offense:1:1", "tempo:0:0", "area" } },
            { Archetype.Elementalist, new[] { "element", "area", "survival:3:3", "offense:2:1" } },
        };

        // ------------------------------------------------------------------ construction

        public static Build Reference(ElementType? element = null)
            => new() { Name = $"Reference/{(element?.ToString() ?? "none")}", Element = element, Kit = Kit.Reference };

        /// <summary>One of PlayerPower's builds, for the regression check.</summary>
        public static Build Legacy(Player.PowerBuild build)
            => new() { Name = build.ToString(), Legacy = true, Stats = Player.PlayerPower.StatsFor(build) };

        static readonly Dictionary<string, Build> _builds = new();

        public static Build Make(Archetype archetype, ElementType element, Kit kit)
        {
            string key = $"{archetype}/{element}/{kit}";
            if (_builds.TryGetValue(key, out var cached)) return cached;

            var b = new Build { Name = key, Archetype = archetype, Element = element, Kit = kit };
            if (kit != Kit.Reference)
            {
                Spend(b);   // the board first: its points count when the gear is chosen
                Gear(b);
            }
            _builds[key] = b;
            return b;
        }

        /// <summary>The Max board with NO gear - what gear is measured against
        /// (<see cref="GearMultiplier"/>).</summary>
        public static Build BoardOnly(Archetype archetype, ElementType element)
        {
            string key = $"{archetype}/{element}/BoardOnly";
            if (_builds.TryGetValue(key, out var cached)) return cached;
            var b = new Build { Name = key, Archetype = archetype, Element = element, Kit = Kit.Max };
            Spend(b);
            _builds[key] = b;
            return b;
        }

        /// <summary>What gear multiplies a build's worth, sqrt(damage x survival), by: its Max kit
        /// over the same board bare, averaged over the four elements.</summary>
        public static float GearMultiplier(Archetype archetype)
        {
            var none = new Exchange.Mods();
            return Elements.Average(e =>
            {
                var max = Evaluate(Make(archetype, e, Kit.Max), none, _ => 0, 1);
                var bare = Evaluate(BoardOnly(archetype, e), none, _ => 0, 1);
                return Mathf.Sqrt(max.Dps / bare.Dps * (max.Ehp / bare.Ehp));
            });
        }

        /// <summary>
        /// A build spending the board like <paramref name="boardOf"/> but rolling its gear toward
        /// <paramref name="priority"/> instead - for questions about the gear itself, such as
        /// whether a damage-first Striker and a crit-first one end up level. Cached under
        /// <paramref name="tag"/>.
        /// </summary>
        public static Build MakeRolledFor(string tag, Archetype boardOf, StatKind[] priority, ElementType element, Kit kit)
        {
            string key = $"{tag}/{element}/{kit}";
            if (_builds.TryGetValue(key, out var cached)) return cached;

            var b = new Build { Name = key, Archetype = boardOf, Element = element, Kit = kit };
            if (kit != Kit.Reference)
            {
                Spend(b);
                Gear(b, priority);
            }
            _builds[key] = b;
            return b;
        }

        /// <summary>
        /// The build's gear: every slot rolled toward it at mid-range sub-stats, the weapon and the
        /// three slots that serve it best at three stars, the rest at one (Max) - or every slot at
        /// three (Ceiling).
        ///
        /// Rolled the way a player rolls: toward the build, but never past a stat's knee once the
        /// board, the gear already chosen and the ELEMENT's head starts have got it there - an Air
        /// player whose momentum already covers attack speed re-rolls toward damage or crit. That is
        /// the head start doing its job (an element reaches a threshold sooner and frees the gear
        /// for something else), and without it the model wasted that gear and blamed the element.
        /// </summary>
        static void Gear(Build b, StatKind[] priority = null)
        {
            var prio = priority ?? Priority[b.Archetype];

            var have = new StatPercents();
            have.Add(b.Stats);                 // the board
            have.Add(HeadStarts(b.Element));   // the element, at its average state
            float dr = b.Element == null ? 0f : Profile(b.Element, 0f, _ => 0).Dr;

            var ranked = ArmourSlots.OrderByDescending(slot => Target(prio, slot, null, dr).Score).ToList();
            var maxed = b.Kit == Kit.Ceiling ? new HashSet<GearSlot>(ArmourSlots)
                                             : new HashSet<GearSlot>(ranked.Take(3));

            // The weapon first - every build has it - then the slots that serve the build best.
            have.Add(Wear(b, GearSlot.Weapon, Target(prio, GearSlot.Weapon, have, dr), GearRoller.MaxLevel));
            foreach (var slot in ranked)
                have.Add(Wear(b, slot, Target(prio, slot, have, dr), maxed.Contains(slot) ? GearRoller.MaxLevel : 1));

            // The meter grows by Element Growth from gear and board alike, as in BuildPlayer.
            b.GainRatePoints = b.Stats.ElementGrowth;
        }

        static StatPercents Wear(Build b, GearSlot slot, (StatKind Primary, StatKind[] Subs, float Score) plan, int level)
        {
            var subs = plan.Subs.Select(k =>
            {
                var (min, max) = GearRoller.SubStatRange(k, LootTier.Gold);
                return new SubStat(k, (min + max) * 0.5f);
            }).ToList();
            var grants = GearRoller.BuildGrants(slot, LootTier.Gold, plan.Primary, level, subs);
            b.Stats.Add(grants);
            b.Picks.Add($"{slot} {new string('*', level)}: {plan.Primary} + {string.Join("/", plan.Subs)}");
            return grants;
        }

        /// <summary>The element's head starts at its average state (Assume), as stat points - what
        /// the gear choice counts toward each knee. Zero with no element.</summary>
        static StatPercents HeadStarts(ElementType? element)
        {
            var h = new StatPercents();
            if (element == null) return h;
            var el = Profile(element, 0f, _ => 0);
            h.Damage = el.DamagePoints;
            h.AttackSpeed = el.AttackSpeedPoints;
            h.MoveSpeed = el.MoveSpeedPoints;
            h.CritDamage = el.CritDamagePoints;
            h.CritChance = el.CritAdd * 100f;
            return h;
        }

        /// <summary>
        /// True when a stat already sits at or past its knee (crit chance: at its pool cap;
        /// accuracy: every hit at the top; mitigation: at the floor, given the element's own
        /// damage reduction <paramref name="dr"/>) - more of it is mostly or wholly wasted, so a
        /// player rolling for the build looks to the next stat.
        /// </summary>
        static bool Saturated(StatKind k, StatPercents have, float dr = 0f)
        {
            if (have == null) return false;
            if (k == StatKind.CritChance)
                return Tuning.Player.BaseCritChance + have.CritChance / 100f >= Tuning.Stats.CritChanceCap;
            if (k == StatKind.Accuracy) return have.Accuracy >= 100f;
            if (k is StatKind.Resilience or StatKind.Graze or StatKind.Brace)
            {
                float incoming = StatPercents.ReductionFactor(have.Resilience)
                               * (Assume.MovingShare * StatPercents.ReductionFactor(have.Graze)
                                  + (1f - Assume.MovingShare) * StatPercents.ReductionFactor(have.Brace))
                               * (1f - dr);
                return incoming <= Tuning.Stats.IncomingFloor;
            }
            var (knee, _) = StatCurves.CharacterCurve(k);
            if (float.IsPositiveInfinity(knee)) return false;
            float points = k switch
            {
                StatKind.Damage => have.Damage,
                StatKind.AttackSpeed => have.AttackSpeed,
                StatKind.MoveSpeed => have.MoveSpeed,
                StatKind.CritDamage => have.CritDamage,
                StatKind.FinisherPower => have.FinisherPower,
                StatKind.Range => have.Range,
                StatKind.AoeRadius => have.AoeRadius,
                StatKind.ElementGrowth => have.ElementGrowth,
                StatKind.ElementalEffectiveness => have.ElementalEffectiveness,
                StatKind.MaxHp => have.MaxHp,
                _ => 0f,
            };
            return points >= knee;
        }

        /// <summary>What a targeted roll on this slot looks like: from the slot's one pool, the best
        /// stat as the primary, and three sub-stats - the best kind twice and the second once, the
        /// shape a player re-rolling toward a build ends up with. With <paramref name="have"/>, a
        /// stat already saturated is passed over for the next one the build wants.</summary>
        static (StatKind Primary, StatKind[] Subs, float Score) Target(StatKind[] prio, GearSlot slot,
                                                                      StatPercents have, float dr = 0f)
        {
            var pool = GearRoller.Pool(slot);
            var wanted = pool.Where(k => Array.IndexOf(prio, k) >= 0).Distinct()
                             .OrderBy(k => Array.IndexOf(prio, k)).ToList();
            var ranked = wanted.Where(k => !Saturated(k, have, dr)).ToList();
            if (ranked.Count == 0) ranked = wanted;            // all saturated - the build's favourites anyway
            if (ranked.Count == 0 && pool.Length > 0) ranked.Add(pool[0]);   // nothing it wants here

            var primary = ranked.Count > 0 ? ranked[0] : StatKind.None;
            StatKind[] subs = ranked.Count switch
            {
                0 => Array.Empty<StatKind>(),
                1 => new[] { ranked[0], ranked[0], ranked[0] },
                _ => new[] { ranked[0], ranked[0], ranked[1] },
            };

            float score = 2f * Weight(prio, primary) + subs.Sum(k => Weight(prio, k));
            return (primary, subs, score);
        }

        static float Weight(StatKind[] prio, StatKind k)
        {
            int r = Array.IndexOf(prio, k);
            return r < 0 ? 0f : prio.Length - r;
        }

        /// <summary>
        /// Spends the level cap along the build's routes the way BoardState allows - every route
        /// walks out from the centre - folded into the stat block exactly as BuildPlayer folds
        /// Mastery.Get. A lane-following rule takes its route's lane's.
        /// </summary>
        static void Spend(Build b)
        {
            int left = Tuning.Mastery.LevelCap;
            foreach (var pick in Board[b.Archetype])
            {
                var parts = pick.Split(':');
                int l1 = parts.Length > 1 ? int.Parse(parts[1]) : 0, l2 = parts.Length > 2 ? int.Parse(parts[2]) : 0;
                int bought = 0;
                foreach (var node in MasteryBoard.Route(parts[0], l1, l2))
                {
                    if (node.Cost > left) break;
                    left -= node.Cost;
                    bought++;
                    Fold(b, node, node.FollowsLane ? MasteryBoard.LaneRule(node, node.RuleSlot == 0 ? l1 : l2) : node.Notable);
                }
                if (bought > 0) b.Picks.Add($"board: {MasteryBoard.BranchName(parts[0])} {pick} x{bought}");
                if (left <= 0) break;
            }
        }

        /// <summary>A node folded into the build as BuildPlayer folds the board: its stat points
        /// into the stat block, its board-only stat and its rule beside it.</summary>
        static void Fold(Build b, MasteryNode node, Notable rule)
        {
            if (node.Stat != StatKind.None) GearRoller.Add(b.Stats, node.Stat, node.Value);
            switch (node.Extra)
            {
                case BoardStat.Lifesteal:    b.Lifesteal += node.Value; break;
                case BoardStat.BurnPower:    b.BurnPoints += node.Value; break;
                case BoardStat.SoakPower:    b.SoakPoints += node.Value; break;
                case BoardStat.BleedPower:   b.BleedPoints += node.Value; break;
                case BoardStat.StaggerPower: b.StaggerPoints += node.Value; break;
                case BoardStat.StatusPower:
                    b.BurnPoints += node.Value; b.SoakPoints += node.Value;
                    b.BleedPoints += node.Value; b.StaggerPoints += node.Value;
                    break;
            }
            if (rule != Notable.None) b.Notables.Add(rule);
        }
    }
}
