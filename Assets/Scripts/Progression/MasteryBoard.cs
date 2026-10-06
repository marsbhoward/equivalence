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
        /// <summary>One level, one unit of the branch's primary or secondary stat.</summary>
        Filler,
        /// <summary>The mouth of a branch and its decision: buying one locks its siblings.</summary>
        Keystone,
        /// <summary>Mid-branch, two levels: a rule that changes how something plays.</summary>
        Tincture,
        /// <summary>The branch's apex, four levels: its capstone.</summary>
        Opus,
        /// <summary>The second-ability node at the centre - see <see cref="Notable.Rebis"/>.</summary>
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

        public bool Keystone => Kind == NodeKind.Keystone;

        /// <summary>Principle points this node contributes once owned.</summary>
        public int PrincipleWeight => Kind switch
        {
            NodeKind.Filler   => 1,
            NodeKind.Keystone => MasteryBoard.KeystonePrincipleWeight,
            NodeKind.Tincture => MasteryBoard.TincturePrincipleWeight,
            NodeKind.Opus     => MasteryBoard.OpusPrincipleWeight,
            _                 => 0,
        };
    }

    /// <summary>
    /// ONE board design, instanced four times - once per element. Nodes are identical on every
    /// element's board; what is bought is stored per element (BoardState).
    ///
    /// THE REBUILD (2026-10-05, version 2). Every node now speaks gear's stat language (StatKind
    /// points, bent by the same character curve) or a board-only stat (lifesteal, status power,
    /// Rift capacity), so nothing on it is read by nobody, and every branch's name says what it
    /// gives. A branch runs keystone (4) - five fillers - TINCTURE (2) - six fillers - OPUS (4):
    /// 21 levels. One branch per forked domain is buyable (a keystone locks its siblings, and a
    /// branch's fillers are reached only through its own keystone), so a board holds 5 x 21 + the
    /// Rift's 13 + the Rebis = 119 buyable levels, and the cap of 50 covers 42% of it.
    ///
    /// SIZED AGAINST THE KNEES. A stat unit is a quarter of a Gold armour primary of the stat
    /// (Tuning.Board.StatUnitOfGoldPrimary), so a whole branch gives its primary nine units -
    /// about a third of the stat's character knee - and its secondary five.
    /// </summary>
    public static class MasteryBoard
    {
        /// <summary>
        /// The board's version, written into node ids ("v2.") and onto the profile
        /// (MasteryProfile.BoardVersion). Version 1 was the 2026-09 board of fifteen identical
        /// branches; its ids no longer resolve, so its purchases drop and their levels return.
        /// </summary>
        public const int Version = 2;

        public const int FillerCost = 1;
        public const int KeystoneCost = 4;
        public const int TinctureCost = 2;
        public const int OpusCost = 4;
        public const int RebisCost = 1;

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

        // ------------------------------------------------------------------ the branches

        /// <summary>One branch's whole content - the board is this table laid out.</summary>
        public readonly struct BranchSpec
        {
            public readonly MasteryDomain Domain;
            public readonly string Id, Name;
            public readonly Principle Principle;
            public readonly StatKind Primary, Secondary;
            public readonly BoardStat PrimaryExtra;
            public readonly Notable Keystone, Tincture, Opus;

            public BranchSpec(MasteryDomain domain, string id, string name, Principle principle,
                              StatKind primary, StatKind secondary, Notable tincture, Notable opus,
                              BoardStat primaryExtra = BoardStat.None, Notable keystone = Notable.None)
            {
                Domain = domain; Id = id; Name = name; Principle = principle;
                Primary = primary; Secondary = secondary; PrimaryExtra = primaryExtra;
                Keystone = keystone; Tincture = tincture; Opus = opus;
            }
        }

        /// <summary>
        /// Every forked branch. The primary takes the keystone and six fillers (nine units), the
        /// secondary five fillers. A status branch's primary is that status's POWER, and its
        /// keystone is the humour that lets any element apply it. Range's secondary reads as
        /// Pierce on a bow, Cleave on anything else (BoardState.Points).
        /// </summary>
        public static readonly BranchSpec[] Branches =
        {
            new(MasteryDomain.DamageSource, "strikes", "Strikes", Principle.Sulfur,
                StatKind.Damage, StatKind.Accuracy, Notable.Vitriol, Notable.Cohobation),
            new(MasteryDomain.DamageSource, "arts", "Weapon Arts", Principle.Sulfur,
                StatKind.FinisherPower, StatKind.FinisherKnockback, Notable.Cinnabar, Notable.Multiplication),
            new(MasteryDomain.DamageSource, "element", "Element", Principle.Mercury,
                StatKind.ElementalEffectiveness, StatKind.ElementGrowth, Notable.Alkahest, Notable.Exaltation),

            new(MasteryDomain.Status, "burn", "Burn", Principle.Sulfur,
                StatKind.None, StatKind.FinisherPower, Notable.Saltpetre, Notable.Cineration,
                BoardStat.BurnPower, Notable.Choler),
            new(MasteryDomain.Status, "soak", "Soak", Principle.Mercury,
                StatKind.None, StatKind.ElementGrowth, Notable.AquaFortis, Notable.Solution,
                BoardStat.SoakPower, Notable.Phlegm),
            new(MasteryDomain.Status, "bleed", "Bleed", Principle.Sulfur,
                StatKind.None, StatKind.CritChance, Notable.Orpiment, Notable.Mortification,
                BoardStat.BleedPower, Notable.Sanguine),
            new(MasteryDomain.Status, "stagger", "Stagger", Principle.Salt,
                StatKind.None, StatKind.FinisherKnockback, Notable.Antimony, Notable.Congelation,
                BoardStat.StaggerPower, Notable.Melancholy),

            new(MasteryDomain.Survival, "armour", "Armour", Principle.Salt,
                StatKind.Resilience, StatKind.MaxHp, Notable.Alum, Notable.Fixation),
            new(MasteryDomain.Survival, "evasion", "Evasion", Principle.Mercury,
                StatKind.Graze, StatKind.AbilityCooldownReduction, Notable.SalAmmoniac, Notable.Volatilization),
            new(MasteryDomain.Survival, "sustain", "Sustain", Principle.Salt,
                StatKind.None, StatKind.HealReceived, Notable.AquaVitae, Notable.Cibation,
                BoardStat.Lifesteal),

            new(MasteryDomain.Tempo, "speed", "Speed", Principle.Mercury,
                StatKind.AttackSpeed, StatKind.ComboTime, Notable.Realgar, Notable.Circulation),
            new(MasteryDomain.Tempo, "weight", "Weight", Principle.Sulfur,
                StatKind.CritDamage, StatKind.Brace, Notable.Tartar, Notable.Precipitation),

            new(MasteryDomain.Space, "range", "Range", Principle.Mercury,
                StatKind.Range, StatKind.Cleave, Notable.AquaRegia, Notable.Inceration),
            new(MasteryDomain.Space, "area", "Area", Principle.Salt,
                StatKind.AoeRadius, StatKind.Splash, Notable.Borax, Notable.Fermentation),
        };

        public static string[] BranchesOf(MasteryDomain d)
        {
            if (d == MasteryDomain.RiftCapacity) return new[] { RiftBranch };
            var list = new List<string>();
            foreach (var b in Branches) if (b.Domain == d) list.Add(b.Id);
            return list.ToArray();
        }

        public static BranchSpec? SpecOf(string branch)
        {
            foreach (var b in Branches) if (b.Id == branch) return b;
            return null;
        }

        /// <summary>The Rift ladder's branch id - one branch, nothing to lock.</summary>
        public const string RiftBranch = "rift";

        /// <summary>A branch's name as a player reads it.</summary>
        public static string BranchName(string branch)
            => branch == RiftBranch ? "Rift Capacity"
             : string.IsNullOrEmpty(branch) ? "The Rebis"
             : SpecOf(branch)?.Name ?? branch;

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

        const int FillersBeforeTincture = 5;
        const int FillersAfterTincture = 6;
        const int RiftFillers = 5;

        /// <summary>One step between adjacent nodes, along a branch and across branches alike.</summary>
        const float NodeStep = 0.8f;

        /// <summary>How far out each domain's keystones sit - clear of the Rebis at the centre and of
        /// the neighbouring domains' outer branches.</summary>
        const float TrunkRadius = 2.6f;

        static readonly MasteryDomain[] Ring =
        {
            MasteryDomain.DamageSource, MasteryDomain.Status, MasteryDomain.Survival,
            MasteryDomain.Tempo, MasteryDomain.Space, MasteryDomain.RiftCapacity,
        };

        static void Build()
        {
            if (_all != null) return;
            _all = new List<MasteryNode>();
            _byId.Clear();
            _branchOrdinal = 0;

            for (int d = 0; d < Ring.Length; d++)
            {
                float angle = d / (float)Ring.Length * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (Ring[d] == MasteryDomain.RiftCapacity) BuildRift(dir);
                else BuildDomain(Ring[d], dir);
            }

            // The Rebis sits at the centre, joined to nothing: any build reaches it, it locks
            // nothing, and nothing locks it.
            Add(new MasteryNode
            {
                Id = RebisId, Domain = MasteryDomain.DamageSource, Branch = "", Kind = NodeKind.Rebis,
                Principle = Principle.Mercury, Cost = RebisCost, Notable = Notable.Rebis, Position = Vector2.zero,
            });
        }

        /// <summary>
        /// Rotating offset so the fillers' principles do not land the same way on every branch -
        /// an unrotated remainder once put one principle far ahead of the others on the board.
        /// </summary>
        static int _branchOrdinal;

        static string Key(MasteryDomain d) => d switch
        {
            MasteryDomain.DamageSource => "source",
            MasteryDomain.RiftCapacity => "rift",
            _ => d.ToString().ToLowerInvariant(),
        };

        static void BuildDomain(MasteryDomain domain, Vector2 dir)
        {
            var branches = new List<BranchSpec>();
            foreach (var b in Branches) if (b.Domain == domain) branches.Add(b);

            // Branches run PARALLEL out of the trunk, offset sideways, so every gap on the board is
            // one step - fanned on an angle, a four-branch domain once crossed into its neighbour.
            var perp = new Vector2(-dir.y, dir.x);
            for (int i = 0; i < branches.Count; i++)
            {
                var spec = branches[i];
                float lateral = (i - (branches.Count - 1) * 0.5f) * NodeStep;
                Vector2 At(int step) => dir * (TrunkRadius + step * NodeStep) + perp * lateral;
                string prefix = $"v{Version}.{Key(domain)}.{spec.Id}";

                var key = new MasteryNode
                {
                    Id = $"{prefix}.key", Domain = domain, Branch = spec.Id, Kind = NodeKind.Keystone,
                    Principle = spec.Principle, Cost = KeystoneCost, Notable = spec.Keystone, Position = At(0),
                };
                SetUnits(key, spec, primary: true, units: 3);
                Add(key);
                _branchOrdinal++;

                string prev = key.Id;
                int step = 1, filler = 0;
                void Filler()
                {
                    var n = new MasteryNode
                    {
                        Id = $"{prefix}.{filler}", Domain = domain, Branch = spec.Id, Kind = NodeKind.Filler,
                        Principle = (Principle)((filler + _branchOrdinal) % 3), Cost = FillerCost,
                        Position = At(step),
                    };
                    // Primary first, then alternating: six of the primary, five of the secondary.
                    SetUnits(n, spec, primary: filler % 2 == 0, units: 1);
                    Add(n);
                    Link(prev, n.Id);
                    prev = n.Id;
                    filler++;
                    step++;
                }

                for (int f = 0; f < FillersBeforeTincture; f++) Filler();

                var tincture = new MasteryNode
                {
                    Id = $"{prefix}.tincture", Domain = domain, Branch = spec.Id, Kind = NodeKind.Tincture,
                    Principle = spec.Principle, Cost = TinctureCost, Notable = spec.Tincture, Position = At(step++),
                };
                Add(tincture);
                Link(prev, tincture.Id);
                prev = tincture.Id;

                for (int f = 0; f < FillersAfterTincture; f++) Filler();

                var opus = new MasteryNode
                {
                    Id = $"{prefix}.opus", Domain = domain, Branch = spec.Id, Kind = NodeKind.Opus,
                    Principle = spec.Principle, Cost = OpusCost, Notable = spec.Opus, Position = At(step),
                };
                Add(opus);
                Link(prev, opus.Id);
            }
        }

        /// <summary>
        /// The Rift ladder: the Cucurbit keystone (+1 capacity), five fillers that carry only their
        /// principle point - and say so - and the Aludel at its end (+1 capacity), so a Rift pushes
        /// one item to safety at base and up to three with the whole ladder.
        /// </summary>
        static void BuildRift(Vector2 dir)
        {
            Vector2 At(int step) => dir * (TrunkRadius + step * NodeStep);
            string prefix = $"v{Version}.rift";

            var key = new MasteryNode
            {
                Id = $"{prefix}.key", Domain = MasteryDomain.RiftCapacity, Branch = RiftBranch,
                Kind = NodeKind.Keystone, Principle = Principle.Salt, Cost = KeystoneCost,
                Extra = BoardStat.RiftCapacity, Value = 1f, Position = At(0),
            };
            Add(key);
            _branchOrdinal++;

            string prev = key.Id;
            for (int f = 0; f < RiftFillers; f++)
            {
                var n = new MasteryNode
                {
                    Id = $"{prefix}.{f}", Domain = MasteryDomain.RiftCapacity, Branch = RiftBranch,
                    Kind = NodeKind.Filler, Principle = (Principle)((f + _branchOrdinal) % 3),
                    Cost = FillerCost, Position = At(f + 1),
                };
                Add(n);
                Link(prev, n.Id);
                prev = n.Id;
            }

            var apex = new MasteryNode
            {
                Id = $"{prefix}.opus", Domain = MasteryDomain.RiftCapacity, Branch = RiftBranch,
                Kind = NodeKind.Opus, Principle = Principle.Salt, Cost = OpusCost,
                Extra = BoardStat.RiftCapacity, Value = 1f, Position = At(RiftFillers + 1),
            };
            Add(apex);
            Link(prev, apex.Id);
        }

        static void SetUnits(MasteryNode n, BranchSpec spec, bool primary, int units)
        {
            if (primary)
            {
                if (spec.Primary != StatKind.None) { n.Stat = spec.Primary; n.Value = Unit(spec.Primary) * units; }
                else if (spec.PrimaryExtra != BoardStat.None) { n.Extra = spec.PrimaryExtra; n.Value = Unit(spec.PrimaryExtra) * units; }
            }
            else if (spec.Secondary != StatKind.None)
            {
                n.Stat = spec.Secondary;
                n.Value = Unit(spec.Secondary) * units;
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
        /// the rule and its description cannot drift apart.
        /// </summary>
        public static string Describe(Notable n)
        {
            string P(float f) => $"{f * 100f:0.#}%";
            return n switch
            {
                Notable.Vitriol => $"Every hit on the same enemy as your last bites {P(Tuning.Board.VitriolPerHit)} deeper, up to {P(Tuning.Board.VitriolMax)}. A new target starts over.",
                Notable.Cohobation => $"Every basic that lands steeps your next weapon art: +{P(Tuning.Board.CohobationPerBasic)} each, up to +{P(Tuning.Board.CohobationMax)}. The weapon art spends it.",
                Notable.Cinnabar => "A PERFECT weapon art always crits.",
                Notable.Multiplication => "Your chain is one basic shorter (never fewer than one): weapon arts come sooner.",
                Notable.Alkahest => $"A release hands back {P(Tuning.Board.AlkahestRefund)} of your meter.",
                Notable.Exaltation => $"For {Tuning.Board.ExaltationSeconds:0.#} seconds after a release, +{Tuning.Board.ExaltationDamagePoints:0}% damage.",

                Notable.Choler => $"Your weapon arts set what they hit burning: {P(Tuning.Board.CholerBurnFraction)} of the hit each second for {Tuning.Board.CholerSeconds:0.#} seconds. Any element.",
                Notable.Saltpetre => $"Burning enemies take {P(Tuning.Board.SaltpetreBonus)} more from your hits.",
                Notable.Cineration => $"A burning enemy that dies bursts, setting everything within {Tuning.Board.CinerationRadius:0.#} units burning with the burn it carried.",

                Notable.Phlegm => $"Your weapon arts soak what they hit for {Tuning.Board.PhlegmSeconds:0.#} seconds: slowed, and taking {P(Tuning.Board.PhlegmVulnerability - 1f)} more damage. Any element.",
                Notable.AquaFortis => $"Soaking an enemy also soaks the nearest other enemy within {Tuning.Board.AquaFortisRadius:0.#} units.",
                Notable.Solution => $"Soaked enemies deal {P(1f - Tuning.Board.SolutionDamageMul)} less damage to you.",

                Notable.Sanguine => $"Your crits open a bleed: {P(Tuning.Board.SanguineBleedFraction)} of the hit each second for {Tuning.Board.SanguineSeconds:0.#} seconds. Any element.",
                Notable.Orpiment => $"Your bleeds stack, up to {Tuning.Board.OrpimentStacks} on one enemy.",
                Notable.Mortification => $"Your crits on a bleeding enemy deal {P(Tuning.Board.MortificationBonus)} more.",

                Notable.Melancholy => $"Your weapon arts stagger what they hit for {Tuning.Board.MelancholySeconds:0.#} seconds: slowed, more with each stagger. Any element.",
                Notable.Antimony => $"Staggered enemies attack {P(Tuning.Board.AntimonyAttackSlow)} slower.",
                Notable.Congelation => $"An enemy staggered a third time in a row congeals: rooted for {Tuning.Board.CongelationSeconds:0.#} seconds and taking {P(Tuning.Board.CongelationVulnerability - 1f)} more damage.",

                Notable.Alum => $"While at full health, you take {P(1f - Tuning.Board.AlumDamageMul)} less damage.",
                Notable.Fixation => $"No single hit can take more than {P(Tuning.Board.FixationMaxHitFraction)} of your max health.",
                Notable.SalAmmoniac => $"Your defensive ability's protection and parry window last {P(Tuning.Board.SalAmmoniacWindowMul - 1f)} longer.",
                Notable.Volatilization => "A parry refunds your defensive ability's cooldown.",
                Notable.AquaVitae => $"Every kill heals {P(Tuning.Board.AquaVitaeHealFraction)} of your max health.",
                Notable.Cibation => $"Lifesteal held back by the healing limit becomes a shield, up to {P(Tuning.Board.CibationShieldFraction)} of your max health, fading over {Tuning.Board.CibationFadeSeconds:0} seconds.",

                Notable.Realgar => $"After a weapon art lands, your next {Tuning.Board.RealgarBasics} basics swing {P(Tuning.Board.RealgarSpeedMul - 1f)} faster.",
                Notable.Circulation => $"Hits landed within {Tuning.Board.CirculationWindow:0.#} second of each other build flow: +{Tuning.Board.CirculationPointsPerHit:0}% attack speed each, up to +{Tuning.Board.CirculationMaxPoints:0}%. A second without a hit loses it.",
                Notable.Tartar => $"Standing still, +{Tuning.Board.TartarDamagePoints:0}% damage.",
                Notable.Precipitation => $"Your weapon arts land with a shockwave: {P(Tuning.Board.PrecipitationFraction)} of the hit to every other enemy within {Tuning.Board.PrecipitationRadius:0.#} units of the target.",

                Notable.AquaRegia => $"Hits on an enemy beyond {P(Tuning.Board.AquaRegiaReachFraction)} of your reach slow it by {P(1f - Tuning.Board.AquaRegiaSlow)} for {Tuning.Board.AquaRegiaSeconds:0.#} seconds.",
                Notable.Inceration => $"Enemies you have slowed take {P(Tuning.Board.IncerationBonus)} more from your hits.",
                Notable.Borax => $"Your releases and area weapon arts draw enemies {Tuning.Board.BoraxPullDistance:0.#} units toward their centre.",
                Notable.Fermentation => $"Your releases and area weapon arts salt the ground for {Tuning.Board.FermentationSeconds:0.#} seconds: enemies on it are slowed by {P(1f - Tuning.Board.FermentationSlow)} and take {P(Tuning.Board.FermentationHitFractionPerSecond)} of your hit each second.",

                Notable.Rebis => "Choose which ability your release fires. One is active at a time, on the same button - switch here, free.",
                _ => "",
            };
        }

        /// <summary>What a board-only stat is called.</summary>
        public static string Label(BoardStat s) => s switch
        {
            BoardStat.Lifesteal => "Lifesteal",
            BoardStat.RiftCapacity => "Rift Capacity",
            BoardStat.BurnPower => "Burn Power",
            BoardStat.SoakPower => "Soak Power",
            BoardStat.BleedPower => "Bleed Power",
            BoardStat.StaggerPower => "Stagger Power",
            _ => s.ToString(),
        };

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
