using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;

namespace Convergence.Progression
{
    /// <summary>
    /// The three principles every node feeds. See CLAUDE.md for the full design; the short version
    /// is that this is the SECOND layer of the board - domains below are the local, tactile choice,
    /// and these are the global identity, assembled by commitment rather than bought outright.
    ///
    /// Reused from the transmutation circle deliberately: the game already has a vocabulary for
    /// this trio, and using it twice is a smaller cost than inventing a second one.
    /// </summary>
    public enum Principle
    {
        /// <summary>The volatile principle. Offense, aggression, damage.</summary>
        Sulfur,
        /// <summary>The fluid principle. Mobility, adaptation, penetration.</summary>
        Mercury,
        /// <summary>The fixed principle. Defense, endurance, preservation.</summary>
        Salt,
    }

    /// <summary>The six systems the board forks on. Not saved anywhere - only node ids are.</summary>
    public enum MasteryDomain
    {
        DamageSource,   // Strikes / Arts / Element
        Status,         // Burn / Soak / Bleed / Stagger
        Survival,       // Armour / Evasion / Sustain
        Tempo,          // Speed / Weight
        Space,          // Range / Area
        RiftCapacity,   // no fork - a short ladder
    }

    public enum NodeKind
    {
        /// <summary>One level, one unit of its stat.</summary>
        Filler,
        /// <summary>Version 2's branch mouths. Not on the grid (version 3); kept so nothing renumbers.</summary>
        Keystone,
        /// <summary>A CONVERGENCE, where a fork's lanes meet, or a diagonal's end: a rule, two levels.</summary>
        Tincture,
        /// <summary>A path's CAPSTONE, where its second fork meets: a rule, three levels.</summary>
        Opus,
        /// <summary>The centre: the element's release, owned from the start - see <see cref="Notable.Rebis"/>.</summary>
        Rebis,
    }

    /// <summary>What a node can add that gear has no StatKind for.</summary>
    public enum BoardStat
    {
        None,
        /// <summary>A fraction of damage dealt returned as health (one pool with every source).</summary>
        Lifesteal,
        /// <summary>Items a Rift pushes to safety beyond the base one.</summary>
        RiftCapacity,
        /// <summary>Percentage points on every burn the player applies.</summary>
        BurnPower,
        /// <summary>Percentage points on every soak the player applies (its vulnerability).</summary>
        SoakPower,
        /// <summary>Percentage points on every bleed the player applies.</summary>
        BleedPower,
        /// <summary>Percentage points on every stagger the player applies (its duration).</summary>
        StaggerPower,
        /// <summary>Percentage points on ALL FOUR statuses at once - read as each one's power
        /// (BoardState.Recompute). Appended 2026-10-30 (the grid's Affliction trunk).</summary>
        StatusPower,
    }

    /// <summary>
    /// Every node that DOES something rather than adding points: the status keystones, each
    /// branch's Tincture and Opus, the Rift's two vessels' worth of capacity aside, and the Rebis.
    /// The rules live in <see cref="BoardEffects"/>, the numbers in Tuning.Board, the words in
    /// <see cref="MasteryBoard.Describe"/>.
    /// </summary>
    public enum Notable
    {
        None,
        // Damage Source
        Vitriol, Cohobation,            // Strikes
        Cinnabar, Multiplication,       // Arts
        Alkahest, Exaltation,           // Element
        // Status - the keystones are the four humours
        Choler, Saltpetre, Cineration,  // Burn
        Phlegm, AquaFortis, Solution,   // Soak
        Sanguine, Orpiment, Mortification,      // Bleed
        Melancholy, Antimony, Congelation,      // Stagger
        // Survival
        Alum, Fixation,                 // Armour
        SalAmmoniac, Volatilization,    // Evasion
        AquaVitae, Cibation,            // Sustain
        // Tempo
        Realgar, Circulation,           // Speed
        Tartar, Precipitation,          // Weight
        // Space
        AquaRegia, Inceration,          // Range
        Borax, Fermentation,            // Area
        // The centre
        Rebis,
    }

    public class MasteryNode
    {
        public string Id;
        public MasteryDomain Domain;
        /// <summary>Which fork branch this belongs to. Empty for the Rebis.</summary>
        public string Branch = "";
        public NodeKind Kind;
        public Principle Principle;
        public int Cost;

        /// <summary>The gear stat this node adds, in percentage points, or None.</summary>
        public StatKind Stat;
        /// <summary>The board-only stat this node adds, or None.</summary>
        public BoardStat Extra;
        /// <summary>How much of <see cref="Stat"/> (points) or <see cref="Extra"/> it adds.</summary>
        public float Value;

        /// <summary>The rule this node switches on, or None.</summary>
        public Notable Notable;

        public Vector2 Position;
        public readonly List<string> Neighbours = new();

        /// <summary>The fork a lane node belongs to ("offense.f1"), and which of its lanes. Taking
        /// any node of a lane locks the fork's other lanes on that board. Empty off the forks.</summary>
        public string Fork = "";
        public int Lane = -1;

        /// <summary>A rule node whose rule is the one the lane chosen at <see cref="RuleFork"/> carries
        /// (MasteryBoard.LaneRules) - <see cref="RuleSlot"/> 0 at the convergence, 1 at the capstone.</summary>
        public bool FollowsLane;
        public string RuleFork = "";
        public int RuleSlot;

        public bool Keystone => Kind == NodeKind.Keystone;

        /// <summary>Principle points this node contributes once owned.</summary>
        public int PrincipleWeight => Kind switch
        {
            NodeKind.Filler   => 1,
            NodeKind.Keystone => MasteryBoard.KeystonePrincipleWeight,
            NodeKind.Tincture => MasteryBoard.TincturePrincipleWeight,
            NodeKind.Opus     => MasteryBoard.OpusPrincipleWeight,
            _                 => 0,   // the centre
        };
    }

    /// <summary>
    /// ONE board design, instanced four times - once per element. Nodes are identical on every
    /// element's board; what is bought is stored per element (BoardState).
    ///
    /// THE GRID (2026-10-30, version 3 - the user's design, mock
    /// https://claude.ai/artifact/2V5HV3GA2QcNf9uKisGt2k). Version 2 asked four levels for a
    /// keystone before a branch's first stat, and a keystone closed its domain's other branches
    /// for good: at level 24 a player held one node in four columns. Now every node costs a
    /// level from level 1 and the player chooses freely where the stats go:
    ///
    ///   - the CENTRE is the sigil, its knot the element's release, owned from the start;
    ///   - four PATHS leave its four rings: three trunk stats, a FORK of four lanes (three nodes
    ///     each) meeting at a CONVERGENCE rule (2), two more trunk, the same fork AGAIN - free to
    ///     go another way - and a CAPSTONE rule (3). One path end to end is 16 levels;
    ///   - taking a lane locks only that fork's other lanes (BoardState.ForkLocked);
    ///   - four DIAGONALS between the paths: seven nodes and an end rule, with BRIDGES from the
    ///     end to both neighbouring paths' convergences, so the board is a grid, entered anywhere
    ///     from what is owned.
    ///
    /// Every node speaks gear's stat language (StatKind points, bent by the same character curve)
    /// or a board-only stat (lifesteal, status power, Rift capacity). One stat node is one unit:
    /// a 27th of the stat's character knee, or a quarter of a Gold armour primary for a stat
    /// with none (<see cref="Unit(StatKind)"/>). Balance is a pass of its own (the user's call).
    /// </summary>
    public static class MasteryBoard
    {
        /// <summary>
        /// The board's version, written into node ids ("v3.") and onto the profile
        /// (MasteryProfile.BoardVersion). Version 1 was the 2026-09 board of fifteen identical
        /// branches, version 2 the 2026-10-05 keystone branches; their ids no longer resolve, so
        /// their purchases drop and their levels return (BoardState.Migrate).
        /// </summary>
        public const int Version = 3;

        public const int FillerCost = 1;
        public const int KeystoneCost = 4;
        public const int TinctureCost = 2;
        public const int OpusCost = 3;
        /// <summary>The centre is owned from the start (BoardState), so it costs nothing.</summary>
        public const int RebisCost = 0;

        /// <summary>Principle points a keystone contributes, as a multiple of a filler's.</summary>
        public const int KeystonePrincipleWeight = 3;
        public const int TincturePrincipleWeight = 2;
        public const int OpusPrincipleWeight = 3;

        /// <summary>
        /// Points into one principle that unlock each link of its chain.
        ///
        /// Sized against the cap rather than the board (Balance.Assay.BoardReport proves it): 50
        /// levels spent toward ONE principle reach the last link, and no spend of 50 completes two
        /// chains - one completed, one tasted, one untouched.
        /// </summary>
        public static readonly int[] Thresholds = { 8, 16, 22, 28 };

        static List<MasteryNode> _all;
        static readonly Dictionary<string, MasteryNode> _byId = new();

        public static IReadOnlyList<MasteryNode> All { get { Build(); return _all; } }

        /// <summary>Null id returns null rather than throwing.</summary>
        public static MasteryNode Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Build();
            return _byId.TryGetValue(id, out var n) ? n : null;
        }

        /// <summary>The Rebis's id - the one node no branch owns.</summary>
        public static string RebisId => $"v{Version}.rebis";

        // ------------------------------------------------------------------ the principle chains

        /// <summary>
        /// What each link of a principle's chain actually does, in the player's words. One entry
        /// per threshold, so the array length IS the chain length.
        /// </summary>
        public static readonly (string Name, string What)[] SulfurChain =
        {
            ("Season",   "Basics mark their target, stacking."),
            ("Detonate", "An elemental release sets off every mark on the field, each mark a share of your own hit."),
            ("Splash",   "Each detonation also catches what stands near it."),
            ("Swell",    "The splash grows with how many marks went off at once."),
        };

        public static readonly (string Name, string What)[] MercuryChain =
        {
            ("Quicksilver",  "Moving builds a charge."),
            ("Phase Strike", "At full charge, the next hit passes through armour."),
            ("Slipstream",   "A Phase Strike refunds a burst of speed."),
            ("Flow",         "The refund lasts longer the further you travelled for it."),
        };

        public static readonly (string Name, string What)[] SaltChain =
        {
            ("Ward",   "Every hit taken stacks temporary damage reduction."),
            ("Return", "At full Ward, the next hit taken reflects onto what is near."),
            ("Guard",  "Return also buys a moment of real protection."),
            ("Temper", "Return scales with your own Resilience."),
        };

        public static (string Name, string What)[] ChainOf(Principle p) => p switch
        {
            Principle.Sulfur  => SulfurChain,
            Principle.Mercury => MercuryChain,
            _                 => SaltChain,
        };

        // ------------------------------------------------------------------ the paths

        /// <summary>What one node gives: a gear stat, or a board-only stat, or (both None) only its
        /// principle point.</summary>
        public readonly struct Gives
        {
            public readonly StatKind Stat;
            public readonly BoardStat Extra;
            public Gives(StatKind stat, BoardStat extra = BoardStat.None) { Stat = stat; Extra = extra; }
            public static implicit operator Gives(StatKind s) => new(s);
            public static implicit operator Gives(BoardStat x) => new(StatKind.None, x);
            public string Name => Stat != StatKind.None ? Chain.GearForge.Label(Stat) : Extra != BoardStat.None ? Label(Extra) : "";
        }

        /// <summary>
        /// One of the four paths out of the sigil: a trunk of its own stat, a fork of four lanes
        /// that meet at the CONVERGENCE, more trunk, the SAME fork again (a second choice, free to
        /// differ), and the CAPSTONE where its lanes meet. A rule of <see cref="Notable.None"/> on a
        /// path whose <see cref="FollowsLane"/> is set means the lane chosen at that fork decides it
        /// (<see cref="LaneRules"/>).
        /// </summary>
        public readonly struct PathSpec
        {
            public readonly string Id, Name;
            public readonly MasteryDomain Domain;
            public readonly Vector2 Along;
            public readonly Gives Trunk;
            public readonly Gives[] Lanes;
            public readonly Notable Convergence, Capstone;
            public readonly bool FollowsLane;
            public PathSpec(string id, string name, MasteryDomain domain, Vector2 along, Gives trunk, Gives[] lanes,
                            Notable convergence, Notable capstone, bool followsLane = false)
            {
                Id = id; Name = name; Domain = domain; Along = along; Trunk = trunk; Lanes = lanes;
                Convergence = convergence; Capstone = capstone; FollowsLane = followsLane;
            }
        }

        /// <summary>
        /// The four paths, one from each ring of the sigil (the user's grid, 2026-10-30; mock
        /// https://claude.ai/artifact/2V5HV3GA2QcNf9uKisGt2k). Up Offense, right Tempo, down
        /// Survival, left Affliction - the rings themselves are Air up, Earth down, Fire left,
        /// Water right, as the game's sigil has them.
        /// </summary>
        public static readonly PathSpec[] Paths =
        {
            new("offense", "Offense", MasteryDomain.DamageSource, Vector2.up, StatKind.Damage,
                new Gives[] { StatKind.CritChance, StatKind.CritDamage, StatKind.Damage, StatKind.FinisherPower },
                Notable.Vitriol, Notable.Cohobation),
            new("tempo", "Tempo", MasteryDomain.Tempo, Vector2.right, StatKind.AttackSpeed,
                new Gives[] { StatKind.AttackSpeed, StatKind.MoveSpeed, StatKind.ComboTime, StatKind.AbilityCooldownReduction },
                Notable.Realgar, Notable.Circulation),
            new("survival", "Survival", MasteryDomain.Survival, Vector2.down, StatKind.MaxHp,
                new Gives[] { StatKind.Resilience, StatKind.MaxHp, StatKind.Graze, BoardStat.Lifesteal },
                Notable.Alum, Notable.Fixation),
            new("affliction", "Affliction", MasteryDomain.Status, Vector2.left, BoardStat.StatusPower,
                new Gives[] { BoardStat.BurnPower, BoardStat.SoakPower, BoardStat.BleedPower, BoardStat.StaggerPower },
                Notable.None, Notable.None, followsLane: true),
        };

        /// <summary>A shorter spoke between two paths: its two stats alternating, a rule at its end,
        /// and (<see cref="Bridges"/>) a link from its end to both neighbours' convergences.</summary>
        public readonly struct DiagonalSpec
        {
            public readonly string Id, Name;
            public readonly MasteryDomain Domain;
            public readonly Vector2 Along;
            public readonly Gives A, B;
            public readonly Notable End;
            public readonly string Left, Right;
            public DiagonalSpec(string id, string name, MasteryDomain domain, Vector2 along, Gives a, Gives b,
                                Notable end, string left, string right)
            {
                Id = id; Name = name; Domain = domain; Along = along; A = a; B = b; End = end; Left = left; Right = right;
            }
        }

        public static readonly DiagonalSpec[] Diagonals =
        {
            new("element", "Element", MasteryDomain.DamageSource, new Vector2(1, 1), StatKind.ElementalEffectiveness,
                StatKind.ElementGrowth, Notable.Alkahest, "offense", "tempo"),
            new("area", "Area", MasteryDomain.Space, new Vector2(1, -1), StatKind.AoeRadius, StatKind.Splash,
                Notable.Borax, "tempo", "survival"),
            new("reach", "Reach", MasteryDomain.Space, new Vector2(-1, -1), StatKind.Range, StatKind.Cleave,
                Notable.AquaRegia, "survival", "affliction"),
            new("rift", "Rift", MasteryDomain.RiftCapacity, new Vector2(-1, 1), default, default,
                Notable.None, "affliction", "offense"),
        };

        /// <summary>A lane's own pair of rules - convergence, capstone - for a path that follows
        /// its lanes. None where the lane has no rule of its own.</summary>
        public static (Notable Convergence, Notable Capstone) LaneRules(Gives g) => g.Extra switch
        {
            BoardStat.BurnPower => (Notable.Saltpetre, Notable.Cineration),
            BoardStat.SoakPower => (Notable.AquaFortis, Notable.Solution),
            BoardStat.BleedPower => (Notable.Orpiment, Notable.Mortification),
            BoardStat.StaggerPower => (Notable.Antimony, Notable.Congelation),
            BoardStat.Lifesteal => (Notable.AquaVitae, Notable.Cibation),
            _ => g.Stat switch
            {
                StatKind.Damage => (Notable.Vitriol, Notable.Cohobation),
                StatKind.FinisherPower => (Notable.Cinnabar, Notable.Multiplication),
                StatKind.CritDamage => (Notable.Tartar, Notable.Precipitation),
                StatKind.ElementalEffectiveness => (Notable.Alkahest, Notable.Exaltation),
                StatKind.Resilience => (Notable.Alum, Notable.Fixation),
                StatKind.Graze => (Notable.SalAmmoniac, Notable.Volatilization),
                StatKind.AttackSpeed => (Notable.Realgar, Notable.Circulation),
                StatKind.Range => (Notable.AquaRegia, Notable.Inceration),
                StatKind.AoeRadius => (Notable.Borax, Notable.Fermentation),
                _ => (Notable.None, Notable.None),
            },
        };

        /// <summary>The rule a lane-following node takes when <paramref name="lane"/> is the lane
        /// chosen at its fork.</summary>
        public static Notable LaneRule(MasteryNode node, int lane)
        {
            var spec = PathOf(node.Branch);
            if (spec == null || lane < 0 || lane >= spec.Value.Lanes.Length) return Notable.None;
            var rules = LaneRules(spec.Value.Lanes[lane]);
            return node.RuleSlot == 0 ? rules.Convergence : rules.Capstone;
        }

        /// <summary>The humour a status lane's first node opens - any element may then apply it.</summary>
        public static Notable HumourOf(Gives g) => g.Extra switch
        {
            BoardStat.BurnPower => Notable.Choler,
            BoardStat.SoakPower => Notable.Phlegm,
            BoardStat.BleedPower => Notable.Sanguine,
            BoardStat.StaggerPower => Notable.Melancholy,
            _ => Notable.None,
        };

        /// <summary>Every stat leans one way: offense Sulfur, movement and the element Mercury,
        /// holding on Salt. A node's principle is its stat's, a rule's its own.</summary>
        public static Principle PrincipleOf(Gives g) => g.Extra switch
        {
            BoardStat.BurnPower or BoardStat.BleedPower => Principle.Sulfur,
            BoardStat.SoakPower or BoardStat.StatusPower => Principle.Mercury,
            BoardStat.StaggerPower or BoardStat.Lifesteal or BoardStat.RiftCapacity => Principle.Salt,
            _ => g.Stat switch
            {
                StatKind.Damage or StatKind.CritChance or StatKind.CritDamage or StatKind.FinisherPower
                    or StatKind.Accuracy => Principle.Sulfur,
                StatKind.AttackSpeed or StatKind.MoveSpeed or StatKind.ComboTime or StatKind.AbilityCooldownReduction
                    or StatKind.Graze or StatKind.Range or StatKind.Cleave or StatKind.Pierce
                    or StatKind.ElementalEffectiveness or StatKind.ElementGrowth => Principle.Mercury,
                _ => Principle.Salt,
            },
        };

        public static Principle PrincipleOf(Notable n) => n switch
        {
            Notable.Vitriol or Notable.Cohobation or Notable.Cinnabar or Notable.Multiplication or Notable.Tartar
                or Notable.Precipitation or Notable.Saltpetre or Notable.Cineration or Notable.Orpiment
                or Notable.Mortification or Notable.Choler or Notable.Sanguine => Principle.Sulfur,
            Notable.Alkahest or Notable.Exaltation or Notable.AquaFortis or Notable.Solution or Notable.Phlegm
                or Notable.SalAmmoniac or Notable.Volatilization or Notable.Realgar or Notable.Circulation
                or Notable.AquaRegia or Notable.Inceration or Notable.Rebis => Principle.Mercury,
            _ => Principle.Salt,
        };

        public static PathSpec? PathOf(string id)
        {
            foreach (var p in Paths) if (p.Id == id) return p;
            return null;
        }

        public static DiagonalSpec? DiagonalOf(string id)
        {
            foreach (var d in Diagonals) if (d.Id == id) return d;
            return null;
        }

        /// <summary>A path's or diagonal's name as a player reads it.</summary>
        public static string BranchName(string branch)
            => string.IsNullOrEmpty(branch) ? "The Sigil"
             : branch.StartsWith("bridge.") ? "Bridge"
             : PathOf(branch)?.Name ?? DiagonalOf(branch)?.Name ?? branch;

        /// <summary>
        /// One whole path bought in order - trunk, the chosen lane of the first fork, convergence,
        /// trunk, the chosen lane of the second fork, capstone - or a diagonal to its end when
        /// <paramref name="branch"/> names one. What the Assay's builds spend along.
        /// </summary>
        public static List<MasteryNode> Route(string branch, int lane1 = 0, int lane2 = 0)
        {
            Build();
            var route = new List<MasteryNode>();
            string p = $"v{Version}.{branch}.";
            foreach (var n in _all)
            {
                if (!n.Id.StartsWith(p)) continue;
                if (n.Fork.Length > 0 && n.Lane != (n.Fork.EndsWith("f1") ? lane1 : lane2)) continue;
                route.Add(n);
            }
            return route;   // _all holds each path in walking order
        }

        // ------------------------------------------------------------------ sizes

        /// <summary>
        /// One unit of a gear stat on the board, rounded to a tenth so a node reads cleanly. For a
        /// stat with a character knee, a ninth of a third of it (Tuning.Board.UnitsPerKnee) - a
        /// branch's nine units of its primary are a third of the knee whatever the stat's gear roll
        /// size, which for the crit family is set for per-roll worth rather than the knee. For one
        /// without (crit chance, accuracy, the mitigations, the utilities), a quarter of a Gold
        /// armour primary of it.
        /// </summary>
        public static float Unit(StatKind kind)
        {
            var (knee, _) = StatCurves.CharacterCurve(kind);
            float unit = float.IsPositiveInfinity(knee)
                ? GearRoller.PrimaryPoints(kind, LootTier.Gold, GearSlot.Head) * Tuning.Board.StatUnitOfGoldPrimary
                : knee / Tuning.Board.UnitsPerKnee;
            return Mathf.Round(unit * 10f) / 10f;
        }

        /// <summary>One unit of a board-only stat.</summary>
        public static float Unit(BoardStat stat) => stat switch
        {
            BoardStat.Lifesteal => Tuning.Board.LifestealUnit,
            BoardStat.RiftCapacity => 1f,
            _ => Tuning.Board.StatusPowerUnit,
        };

        // ------------------------------------------------------------------ construction

        /// <summary>Grid steps of each part of a path (the mock's defaults).</summary>
        const int TrunkNodes = 3, LaneNodes = 3, BetweenForks = 2;
        const int BridgeNodes = 3;

        /// <summary>Lanes sit this many steps either side of their path: 1 and 3, so every lane is
        /// two steps from its neighbour.</summary>
        static readonly int[] LaneOffsets = { -3, -1, 1, 3 };

        /// <summary>The first node of every path sits this many steps out, clear of the sigil's
        /// four rings drawn round the centre.</summary>
        const int FirstStep = 4;

        /// <summary>Where a path's lanes meet the first time - also how far out each diagonal ends.</summary>
        static int ConvergenceStep => FirstStep + TrunkNodes + LaneNodes;

        /// <summary>One step between adjacent nodes, along a path and across lanes alike.</summary>
        public const float NodeStep = 0.8f;

        static void Build()
        {
            if (_all != null) return;
            _all = new List<MasteryNode>();
            _byId.Clear();

            // The centre: the element's release, owned from the start (BoardState). Every path and
            // diagonal starts from it.
            Add(new MasteryNode
            {
                Id = RebisId, Domain = MasteryDomain.DamageSource, Branch = "", Kind = NodeKind.Rebis,
                Principle = Principle.Mercury, Cost = RebisCost, Notable = Notable.Rebis, Position = Vector2.zero,
            });

            foreach (var p in Paths) BuildPath(p);
            foreach (var d in Diagonals) BuildDiagonal(d);
        }

        static Vector2 Across(Vector2 along) => new(along.y, -along.x);

        static MasteryNode StatNode(string id, string branch, MasteryDomain domain, Gives g, Vector2 at)
        {
            var n = new MasteryNode
            {
                Id = id, Domain = domain, Branch = branch, Kind = NodeKind.Filler, Cost = FillerCost,
                Principle = PrincipleOf(g), Stat = g.Stat, Extra = g.Extra, Position = at,
            };
            n.Value = g.Stat != StatKind.None ? Unit(g.Stat) : g.Extra != BoardStat.None ? Unit(g.Extra) : 0f;
            Add(n);
            return n;
        }

        static void BuildPath(PathSpec p)
        {
            string prefix = $"v{Version}.{p.Id}";
            var across = Across(p.Along);
            Vector2 At(int step, int lateral = 0) => (p.Along * step + across * lateral) * NodeStep;

            string prev = RebisId;
            int step = FirstStep;
            for (int i = 0; i < TrunkNodes; i++, step++)
            {
                var n = StatNode($"{prefix}.t{i}", p.Id, p.Domain, p.Trunk, At(step));
                Link(prev, n.Id);
                prev = n.Id;
            }

            string conv = $"{prefix}.conv";
            Fork(p, prefix, 1, prev, step, At);
            step += LaneNodes;
            Add(RuleNode(conv, p, NodeKind.Tincture, TinctureCost, p.Convergence, 0, At(step)));
            ForkEnds(p, prefix, 1, conv);
            prev = conv;
            step++;

            for (int i = 0; i < BetweenForks; i++, step++)
            {
                var n = StatNode($"{prefix}.m{i}", p.Id, p.Domain, p.Trunk, At(step));
                Link(prev, n.Id);
                prev = n.Id;
            }

            string cap = $"{prefix}.opus";
            Fork(p, prefix, 2, prev, step, At);
            step += LaneNodes;
            Add(RuleNode(cap, p, NodeKind.Opus, OpusCost, p.Capstone, 1, At(step)));
            ForkEnds(p, prefix, 2, cap);
        }

        static MasteryNode RuleNode(string id, PathSpec p, NodeKind kind, int cost, Notable rule, int slot, Vector2 at)
            => new()
            {
                Id = id, Domain = p.Domain, Branch = p.Id, Kind = kind, Cost = cost, Notable = rule, Position = at,
                // A lane-following rule's principle is read off the lane chosen (BoardState.PrincipleOf);
                // until then it counts toward the path's own.
                Principle = rule != Notable.None ? PrincipleOf(rule) : PrincipleOf(p.Trunk),
                FollowsLane = p.FollowsLane, RuleFork = p.FollowsLane ? $"{p.Id}.f{slot + 1}" : "", RuleSlot = slot,
            };

        /// <summary>A fork's four lanes, each <see cref="LaneNodes"/> long, out of <paramref name="from"/>.
        /// A status lane's first node also opens its humour.</summary>
        static void Fork(PathSpec p, string prefix, int fork, string from, int startStep, System.Func<int, int, Vector2> at)
        {
            for (int lane = 0; lane < p.Lanes.Length; lane++)
            {
                string prev = from;
                for (int i = 0; i < LaneNodes; i++)
                {
                    var n = StatNode($"{prefix}.f{fork}.{lane}.{i}", p.Id, p.Domain, p.Lanes[lane],
                                     at(startStep + i, LaneOffsets[lane]));
                    n.Fork = $"{p.Id}.f{fork}";
                    n.Lane = lane;
                    if (i == 0) n.Notable = HumourOf(p.Lanes[lane]);
                    Link(prev, n.Id);
                    prev = n.Id;
                }
            }
        }

        /// <summary>Joins each lane's last node to the rule node its lanes meet at.</summary>
        static void ForkEnds(PathSpec p, string prefix, int fork, string to)
        {
            for (int lane = 0; lane < p.Lanes.Length; lane++) Link($"{prefix}.f{fork}.{lane}.{LaneNodes - 1}", to);
        }

        /// <summary>
        /// A diagonal: its two stats alternating out to the convergence's distance, a rule at the
        /// end, and a bridge of <see cref="BridgeNodes"/> from the end to each neighbouring path's
        /// convergence - the grid's cross-links, so a path can be entered from the side. The Rift
        /// diagonal carries Rift Capacity on its fourth node and its end, principle points between.
        /// </summary>
        static void BuildDiagonal(DiagonalSpec d)
        {
            string prefix = $"v{Version}.{d.Id}";
            bool rift = d.Domain == MasteryDomain.RiftCapacity;
            string prev = RebisId;
            int ends = ConvergenceStep;
            for (int k = 3, i = 0; k < ends; k++, i++)
            {
                Gives g = rift ? (i == 3 ? (Gives)BoardStat.RiftCapacity : new Gives(StatKind.None))
                               : (i % 2 == 0 ? d.A : d.B);
                var n = StatNode($"{prefix}.{i}", d.Id, d.Domain, g, d.Along * k * NodeStep);
                if (rift) n.Principle = Principle.Salt;
                Link(prev, n.Id);
                prev = n.Id;
            }

            string end = $"{prefix}.end";
            if (d.End != Notable.None)
                Add(new MasteryNode
                {
                    Id = end, Domain = d.Domain, Branch = d.Id, Kind = NodeKind.Tincture, Cost = TinctureCost,
                    Notable = d.End, Principle = PrincipleOf(d.End), Position = d.Along * ends * NodeStep,
                });
            else
            {
                var n = StatNode(end, d.Id, d.Domain, rift ? BoardStat.RiftCapacity : d.A, d.Along * ends * NodeStep);
                if (rift) n.Principle = Principle.Salt;
            }
            Link(prev, end);

            foreach (var pathId in new[] { d.Left, d.Right })
            {
                var path = PathOf(pathId).Value;
                var other = PathOf(pathId == d.Left ? d.Right : d.Left).Value;
                Vector2 from = d.Along * ends * NodeStep, to = path.Along * ends * NodeStep;
                string link = end;
                for (int j = 1; j <= BridgeNodes; j++)
                {
                    // Out of the diagonal on the far path's stat, the diagonal's own in the middle,
                    // into the near path on its trunk's.
                    Gives g = j == 1 ? other.Trunk : j == BridgeNodes ? path.Trunk : (rift ? path.Trunk : d.A);
                    var n = StatNode($"v{Version}.bridge.{d.Id}.{pathId}.{j - 1}", $"bridge.{d.Id}", d.Domain, g,
                                     Vector2.Lerp(from, to, j / (float)(BridgeNodes + 1)));
                    Link(link, n.Id);
                    link = n.Id;
                }
                Link(link, $"v{Version}.{pathId}.conv");
            }
        }

        static void Add(MasteryNode n)
        {
            _all.Add(n);
            _byId[n.Id] = n;
        }

        static void Link(string a, string b)
        {
            if (!_byId.TryGetValue(a, out var na) || !_byId.TryGetValue(b, out var nb)) return;
            if (!na.Neighbours.Contains(b)) na.Neighbours.Add(b);
            if (!nb.Neighbours.Contains(a)) nb.Neighbours.Add(a);
        }

        // ------------------------------------------------------------------ words

        /// <summary>A notable's name as a player reads it.</summary>
        public static string NameOf(Notable n) => n switch
        {
            Notable.AquaFortis => "Aqua Fortis",
            Notable.SalAmmoniac => "Sal Ammoniac",
            Notable.AquaVitae => "Aqua Vitae",
            Notable.AquaRegia => "Aqua Regia",
            _ => n.ToString(),
        };

        /// <summary>
        /// What a notable does, in the player's words, with its numbers read from Tuning.Board -
        /// the rule and its description cannot drift apart. Defined words are MARKED for
        /// Core.Glossary ("[[steep]]"): show it through Glossary.Highlight + Footnotes, or
        /// Glossary.Plain where there is no room for definitions.
        /// </summary>
        public static string Describe(Notable n)
        {
            string P(float f) => $"{f * 100f:0.#}%";
            return n switch
            {
                Notable.Vitriol => $"Each hit in a row on the same enemy deals {P(Tuning.Board.VitriolPerHit)} more than the last, up to +{P(VitriolTop)}. Hitting a different enemy starts it over.",
                Notable.Cohobation => $"Each [[basic]] that lands adds {P(Tuning.Board.CohobationPerBasic)} to your [[steep]], up to +{P(Tuning.Board.CohobationMax)}. Your next [[weapon art]] spends the whole steep as extra damage.",
                Notable.Cinnabar => "A [[perfect]] weapon art always [[crit|crits]].",
                Notable.Multiplication => "Your [[chain]] is one [[basic]] shorter (never fewer than one), so weapon arts come sooner.",
                Notable.Alkahest => $"Each [[release]] refunds {P(Tuning.Board.AlkahestRefund)} of your [[element meter]].",
                Notable.Exaltation => $"For {Tuning.Board.ExaltationSeconds:0.#} seconds after a [[release]], +{Tuning.Board.ExaltationDamagePoints:0} [[damage points|Damage]].",

                Notable.Choler => $"Your [[weapon art|weapon arts]] set what they hit [[burning]]: {P(Tuning.Board.CholerBurnFraction)} of the hit each second for {Tuning.Board.CholerSeconds:0.#} seconds. Works with any element.",
                Notable.Saltpetre => $"Your hits deal {P(Tuning.Board.SaltpetreBonus)} more to [[burning]] enemies.",
                Notable.Cineration => $"When a [[burning]] enemy dies, it bursts: every enemy within {Tuning.Board.CinerationRadius:0.#} [[units]] starts burning with the burn it carried.",

                Notable.Phlegm => $"Your [[weapon art|weapon arts]] [[soaked|soak]] what they hit for {Tuning.Board.PhlegmSeconds:0.#} seconds: slowed, and taking {P(Tuning.Board.PhlegmVulnerability - 1f)} more damage. Works with any element.",
                Notable.AquaFortis => $"When you [[soaked|soak]] an enemy, the nearest other enemy within {Tuning.Board.AquaFortisRadius:0.#} [[units]] is soaked too.",
                Notable.Solution => $"[[soaked|Soaked]] enemies deal {P(1f - Tuning.Board.SolutionDamageMul)} less damage to you.",

                Notable.Sanguine => $"Your [[crit|crits]] open a [[bleeding|bleed]]: {P(Tuning.Board.SanguineBleedFraction)} of the hit each second for {Tuning.Board.SanguineSeconds:0.#} seconds. Works with any element.",
                Notable.Orpiment => $"Your [[bleeding|bleeds]] stack: up to {Tuning.Board.OrpimentStacks} on one enemy, each dealing its own damage.",
                Notable.Mortification => $"Your [[crit|crits]] on a [[bleeding]] enemy deal {P(Tuning.Board.MortificationBonus)} more.",

                Notable.Melancholy => $"Your [[weapon art|weapon arts]] [[staggered|stagger]] what they hit for {Tuning.Board.MelancholySeconds:0.#} seconds. Works with any element.",
                Notable.Antimony => $"[[staggered|Staggered]] enemies wait {P(Tuning.Board.AntimonyAttackSlow)} longer between attacks. Their [[tells]] stay the same length.",
                Notable.Congelation => $"An enemy [[staggered]] three times in a row [[congealed|congeals]]: rooted for {Tuning.Board.CongelationSeconds:0.#} seconds and taking {P(Tuning.Board.CongelationVulnerability - 1f)} more damage. At most once every {Tuning.Board.CongelationCooldown:0.#} seconds per enemy.",

                Notable.Alum => $"While your health is full, you take {P(1f - Tuning.Board.AlumDamageMul)} less damage.",
                Notable.Fixation => $"No single hit can take more than {P(Tuning.Board.FixationMaxHitFraction)} of your max health.",
                Notable.SalAmmoniac => $"Your [[defensive ability]] protects you {P(Tuning.Board.SalAmmoniacWindowMul - 1f)} longer, and its [[parry]] window is {P(Tuning.Board.SalAmmoniacWindowMul - 1f)} longer.",
                Notable.Volatilization => "A [[parry]] resets your [[defensive ability|defensive ability's]] cooldown.",
                Notable.AquaVitae => $"Every kill heals {P(Tuning.Board.AquaVitaeHealFraction)} of your max health.",
                Notable.Cibation => $"[[lifesteal|Lifesteal]] that cannot heal you (at full health, or over the [[healing limit]]) becomes a [[shield]] instead, up to {P(Tuning.Board.CibationShieldFraction)} of your max health. The shield fades over {Tuning.Board.CibationFadeSeconds:0} seconds.",

                Notable.Realgar => $"After a [[weapon art]] lands, your next {Tuning.Board.RealgarBasics} [[basic|basics]] swing {P(Tuning.Board.RealgarSpeedMul - 1f)} faster.",
                Notable.Circulation => $"Each hit that lands within {Tuning.Board.CirculationWindow:0.#} second of your last adds {Tuning.Board.CirculationPointsPerHit:0} [[attack speed points|Attack Speed]], up to +{Tuning.Board.CirculationMaxPoints:0}. A second without landing a hit loses it all.",
                Notable.Tartar => $"While you stand still, +{Tuning.Board.TartarDamagePoints:0} [[damage points|Damage]].",
                Notable.Precipitation => $"Each [[weapon art]] hit sends out a shockwave: every other enemy within {Tuning.Board.PrecipitationRadius:0.#} [[units]] of the target takes {P(Tuning.Board.PrecipitationFraction)} of that hit.",

                Notable.AquaRegia => $"Hits on an enemy past {P(Tuning.Board.AquaRegiaReachFraction)} of your [[reach]] slow it by {P(1f - Tuning.Board.AquaRegiaSlow)} for {Tuning.Board.AquaRegiaSeconds:0.#} seconds.",
                Notable.Inceration => $"Your hits deal {P(Tuning.Board.IncerationBonus)} more to [[slowed]] enemies.",
                Notable.Borax => $"Your [[release|releases]] and [[area weapon arts]] pull enemies {Tuning.Board.BoraxPullDistance:0.#} [[units]] toward the centre of the blast.",
                Notable.Fermentation => $"Your [[release|releases]] and [[area weapon arts]] leave salted ground {Tuning.Board.FermentationRadius:0.#} [[units]] around the centre for {Tuning.Board.FermentationSeconds:0.#} seconds. Enemies on it are [[slowed]] by {P(1f - Tuning.Board.FermentationSlow)} and take {P(Tuning.Board.FermentationHitFractionPerSecond)} of your hit each second.",

                Notable.Rebis => "Choose which ability your [[release]] fires. One is active at a time, on the same button - switch here, free.",
                _ => "",
            };
        }

        /// <summary>
        /// Vitriol's real ceiling: its stacks stop at RoundToInt(Max / PerHit) (BoardEffects), which
        /// can fall short of VitriolMax (0.05 / 0.02 rounds to 2 stacks, +4%). The words say what
        /// the rule does, not what the constant hopes.
        /// </summary>
        static float VitriolTop => UnityEngine.Mathf.Min(Tuning.Board.VitriolMax,
            Tuning.Board.VitriolPerHit * UnityEngine.Mathf.RoundToInt(Tuning.Board.VitriolMax / Tuning.Board.VitriolPerHit));

        /// <summary>What a board-only stat is called.</summary>
        public static string Label(BoardStat s) => s switch
        {
            BoardStat.Lifesteal => "Lifesteal",
            BoardStat.RiftCapacity => "Rift Capacity",
            BoardStat.BurnPower => "Burn Power",
            BoardStat.SoakPower => "Soak Power",
            BoardStat.BleedPower => "Bleed Power",
            BoardStat.StaggerPower => "Stagger Power",
            BoardStat.StatusPower => "Status Power",
            _ => s.ToString(),
        };

        /// <summary>What the node's stat means (Core.Glossary for gear stats; the board-only stats
        /// here), marked for the glossary like <see cref="Describe"/>. Empty for a node with none.</summary>
        public static string StatMeaning(MasteryNode n, WeaponClass weapon = WeaponClass.Greatsword)
        {
            if (n.Stat != StatKind.None)
                return Glossary.Stat(n.Stat == StatKind.Cleave && weapon == WeaponClass.Bow ? StatKind.Pierce : n.Stat);
            return n.Extra switch
            {
                BoardStat.Lifesteal => "A share of the damage you deal comes back as health, within the [[healing limit]].",
                BoardStat.RiftCapacity => "A Rift carries more of your items to safety when you leave through it.",
                BoardStat.BurnPower => "Strengthens every burn you apply, and Fire's own Ignite and pools.",
                BoardStat.SoakPower => "Strengthens every soak you apply: soaked enemies take more damage. Also strengthens Water's soak.",
                BoardStat.BleedPower => "Strengthens every bleed you apply.",
                BoardStat.StaggerPower => "Staggers you apply last longer. Also strengthens Earth's Quake.",
                BoardStat.StatusPower => "Strengthens all four statuses you apply at once: burn, soak, bleed and stagger.",
                _ => "",
            };
        }

        /// <summary>A node's stat line - "Damage +3%", "Lifesteal +0.8%", "Rift Capacity +1".</summary>
        public static string StatLine(MasteryNode n, WeaponClass weapon = WeaponClass.Greatsword)
        {
            if (n.Stat != StatKind.None)
            {
                var kind = n.Stat == StatKind.Cleave && weapon == WeaponClass.Bow ? StatKind.Pierce : n.Stat;
                string label = n.Stat == StatKind.Cleave ? "Cleave (Pierce on a bow)" : Chain.GearForge.Label(kind);
                return $"{label} +{n.Value:0.##}%";
            }
            return n.Extra switch
            {
                BoardStat.None => "",
                BoardStat.RiftCapacity => $"{Label(n.Extra)} +{n.Value:0}",
                BoardStat.Lifesteal => $"{Label(n.Extra)} +{n.Value * 100f:0.#}%",
                _ => $"{Label(n.Extra)} +{n.Value:0.#}%",
            };
        }
    }
}
