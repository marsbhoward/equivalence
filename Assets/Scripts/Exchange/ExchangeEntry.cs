using System;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;

namespace Convergence.Exchange
{
    public enum ExchangeKind { Boon, Cost }

    /// <summary>
    /// The silhouette an entry's icon is drawn from. Three of the cost families are deliberately
    /// the damaged form of a boon family - Edge chips into Blunted, Hide cracks into Brittle,
    /// Azoth splits into Leaking - so the catalogue reads as one vocabulary rather than two.
    /// </summary>
    public enum ExchangeFamily
    {
        Edge, Anvil, Hide, Quicksilver, Azoth, Ledger,          // boons
        Blunted, Brittle, Tithe, Leaden, Leaking, Blindfold,    // costs
    }

    /// <summary>The mark drawn inside the family silhouette, which identifies the entry.</summary>
    public enum ExchangeMark { None, Dot1, Dot2, Dot3, Bar, Ring, Cross, Stroke, Drop }

    /// <summary>
    /// Where an entry comes from. Only BASE entries are in the pool from the start; the rest are
    /// earned: an Albedo by transmuting a Nigredo at a circle, a combination by holding its parts.
    /// The four colours of the Work, each meaning one thing - black a maxed cost (its Nigredo),
    /// white its transmutation (Albedo), yellow a boon and a cost combined (Citrinitas), red a
    /// maxed boon (its Rubedo) - with Conjunction (boon + boon) and Putrefaction (cost + cost).
    /// </summary>
    public enum EntryOrigin { Base, Albedo, Conjunction, Putrefaction, Citrinitas }

    /// <summary>How auto-target chooses, when a cost or an Albedo overrides it.</summary>
    public enum TargetMode { Normal, Random, Weakest }

    /// <summary>
    /// Everything the run's ledger does to the player, accumulated across every stack of every
    /// entry held - recomputed when the ledger changes, read every frame.
    ///
    /// RUN-LAYER POINTS IN, EFFECTIVE VALUES OUT. Entries write POINTS (<see cref="Add"/>): a
    /// boon's positive points and a cost's negative ones are kept apart, because the Vessel bends
    /// only the first (StatCurves.Run) and costs pass straight through - a player saturated in a
    /// stat must not lose less of it to a cost than anyone else does. RunModifiers.Vessel then
    /// writes the effective fields (BonusDamage, AttackSpeedMul ...) every reader uses.
    ///
    /// CONDITIONAL POINTS bend with the static ones: a hit that earns Executioner's bonus asks
    /// <see cref="Factor"/> for the stat with the extra points joined in, so a conditional boon
    /// shares the Vessel's ceiling instead of stepping past it. The Vessel's size travels with the
    /// block (<see cref="Level"/>) so any reader can bend without a reference to the ledger.
    /// </summary>
    public class Mods
    {
        static readonly int StatCount = Enum.GetValues(typeof(StatKind)).Length;

        /// <summary>Positive run-layer points per stat (boons), before the Vessel.</summary>
        public readonly float[] Up = new float[StatCount];

        /// <summary>Negative run-layer points per stat (costs) - never bent.</summary>
        public readonly float[] Down = new float[StatCount];

        /// <summary>A stat whose boon points count for nothing while set - a Nigredo that shuts
        /// the run's side of a stat (Leaden Limbs: attack speed from boons and spires).</summary>
        public readonly bool[] UpVoid = new bool[StatCount];

        /// <summary>The played element's mastery level, which sizes the Vessel.</summary>
        public int Level = Tuning.Mastery.LevelCap;

        /// <summary>Add run-layer points: positive to the boon side, negative to the cost side.</summary>
        public void Add(StatKind kind, float points)
        {
            if (points >= 0f) Up[(int)kind] += points;
            else Down[(int)kind] += points;
        }

        /// <summary>A stat's run-layer points after the Vessel - boons bent, costs straight on -
        /// optionally with conditional points joined in first.</summary>
        public float Points(StatKind kind, float extraUp = 0f, float extraDown = 0f)
            => (UpVoid[(int)kind] ? 0f : StatCurves.Run(kind, Up[(int)kind] + Mathf.Max(0f, extraUp), Level))
               + Down[(int)kind] + Mathf.Min(0f, extraDown);

        /// <summary>
        /// How much a conditional bonus (or penalty) changes a stat over its static value: the
        /// multiplier to put on a hit that has already been given the static value. 1 with no
        /// extra points.
        /// </summary>
        public float Factor(StatKind kind, float extraUp, float extraDown = 0f)
        {
            if (extraUp <= 0f && extraDown >= 0f) return 1f;
            float now = Mathf.Max(0.1f, 1f + Points(kind) / 100f);
            float with = Mathf.Max(0.1f, 1f + Points(kind, extraUp, extraDown) / 100f);
            return with / now;
        }

        // ---------------------------------------------------------------- effective (the Vessel's)

        /// <summary>Flat damage on the base hit - the Damage points as a share of it, so the
        /// existing "(BaseDamage + BonusDamage)" reads keep meaning what they always did.</summary>
        public float BonusDamage;
        public float AttackSpeedMul = 1f;
        public float MoveSpeedMul = 1f;
        public float FinisherDamageMul = 1f;

        /// <summary>Added straight to the crit multiplier.</summary>
        public float CritDamageAdd;

        /// <summary>On reach / throw / bow range, every class.</summary>
        public float RangeMul = 1f;

        /// <summary>On every area attack's radius, releases included.</summary>
        public float AreaMul = 1f;

        /// <summary>On everything a release does (with the character's Elemental Power).</summary>
        public float ReleaseMul = 1f;

        /// <summary>On how fast the element's meter builds (with the character's Element Growth).</summary>
        public float GainMul = 1f;

        /// <summary>On max health, the character's own included.</summary>
        public float MaxHpMul = 1f;

        /// <summary>Graze and Brace from the run, as damage factors (StatPercents.ReductionFactor).</summary>
        public float GrazeFactor = 1f, BraceFactor = 1f;

        /// <summary>Resilience from the run (Adamant), as a damage factor - inside the one floor.</summary>
        public float ResilienceFactor = 1f;

        /// <summary>Cleave the run adds to the character's (Cleaving Habit, Boomerang) - the same
        /// per-body falloff gear's Cleave shrinks.</summary>
        public float CleavePoints;

        /// <summary>Floors cleared while Senescence is held - read by the balance model; the game
        /// reads RunModifiers.Senescence.</summary>
        public int SenescenceFloors;

        /// <summary>Pierce the run adds - a share of an arrow's hit, bows only.</summary>
        public float PierceAdd;

        // ---------------------------------------------------------------- pools and plain values

        /// <summary>Crit chance into the one pool (StatCurves.Crit).</summary>
        public float BonusCrit;

        /// <summary>Lifesteal into the one pool (StatCurves.Lifesteal).</summary>
        public float BonusLifesteal;

        /// <summary>Stat mitigation the run grants - INSIDE the one floor (a spire's Tin).</summary>
        public float DamageTakenMul = 1f;

        /// <summary>Damage taken from costs - OUTSIDE the floor, like the water soak, so no amount
        /// of defence softens a cost.</summary>
        public float DamageTakenOutside = 1f;

        /// <summary>The mitigation floor when a COST moves it (Exposed); below zero keeps
        /// Tuning.Stats.IncomingFloor. No boon lowers it - it is the threshold every mitigation
        /// stat is tuned against.</summary>
        public float MitigationFloor = -1f;

        public float HealMul = 1f;

        /// <summary>The highest share of max health healing may reach (Hollow).</summary>
        public float HealCeiling = 1f;

        public float ArmourWearMul = 1f;
        public float RepairMul = 1f;
        public bool ArmourNeverWears;

        /// <summary>Worn armour no longer raises the damage taken (Patina).</summary>
        public bool ArmourWearIgnored;

        /// <summary>How fast the meter fades or decays (Leaky Vessel).</summary>
        public float DecayMul = 1f;

        /// <summary>Share of the meter every floor starts with (Attunement).</summary>
        public float StartMeterFraction;

        /// <summary>How long a weapon art locks the player.</summary>
        public float LockMul = 1f;

        /// <summary>Scales the swing's StrikeWidth - the capsule laid along the facing.</summary>
        public float StrikeWidthMul = 1f;

        public float DefenseCooldownMul = 1f;

        /// <summary>How eagerly auto-target leaves its target for a better one.</summary>
        public float SwitchAdvantageDelta;
        public TargetMode Targeting = TargetMode.Normal;

        /// <summary>Strength enemy attack telegraphs are drawn at (Murk).</summary>
        public float TelegraphStrength = 1f;

        /// <summary>Enemies farther than this are drawn as silhouettes; 0 for never (Sol Niger).</summary>
        public float SilhouetteBeyond;

        /// <summary>Fog I: the locked target loses its rim (Player.TargetHighlight).</summary>
        public bool HideTargetHighlight;
        /// <summary>Fog II: the chain and rotation display is hidden.</summary>
        public bool HideChainHud;
        /// <summary>Fog III: damage numbers and enemy armour rings are hidden - the readouts that
        /// say how a fight on an enemy is going.</summary>
        public bool HideEnemyReadouts;

        /// <summary>The floors a spire boon outlasts its own (Lodestone).</summary>
        public int SpireExtraFloors;

        /// <summary>Every enemy's health when it spawns, bosses' excepted (Induration,
        /// Mollification), and an elite's on top of that (Coagulation).</summary>
        public float EnemyHealthMul = 1f, EliteHealthMul = 1f;

        /// <summary>Slag (Dross III): weapon arts never crit.</summary>
        public bool ArtsCantCrit;

        public int BasicsPerChainDelta;

        /// <summary>How long a PARTIAL chain survives between swings (Fraying).</summary>
        public float ComboTimeMul = 1f;

        /// <summary>Extra weapon-art slots in the rotation - Extra Sigil, one stack.</summary>
        public int ExtraFinisherSlots;

        /// <summary>Extra cards on the floor reward (Curator).</summary>
        public int ExtraRewardOptions;

        /// <summary>Extra ricochets a thrown disc makes (Ricochet).</summary>
        public int ExtraRicochets;

        /// <summary>Rooted: no moving while ANY swing plays.</summary>
        public bool RootedWhileSwinging;

        /// <summary>Extra slide when you stop, 0 for none (Mired).</summary>
        public float ExtraSlide;
    }

    /// <summary>
    /// One-shot effects that change the SHAPE of the next deal rather than the character.
    ///
    /// Kept apart from <see cref="Mods"/> because they are events, not state: modelled as
    /// accumulated stats they would either apply to every remaining deal, or - patched to fire
    /// once - a second take would silently do nothing. Counters consumed at build time are simply
    /// what they are.
    /// </summary>
    public class PendingOffer
    {
        public int PairsDelta;
        public bool Forced;
        public bool NoBoon;

        /// <summary>Weight added to the next deal's drawn boons (Indenture: -1).</summary>
        public int BoonWeightDelta;

        /// <summary>Pairs taken off the deal AFTER the next (Caput Mortuum's second deal) - moved
        /// into PairsDelta when the next deal is built.</summary>
        public int PairsDeltaNext;

        /// <summary>The deal AFTER the next can't be refused either (Caput Mortuum's second).</summary>
        public bool ForcedNext;

        public void Clear()
        {
            PairsDelta = 0;
            Forced = false;
            NoBoon = false;
            BoonWeightDelta = 0;
            PairsDeltaNext = 0;
            ForcedNext = false;
        }

        public PendingOffer Copy() => new PendingOffer
        {
            PairsDelta = PairsDelta, Forced = Forced, NoBoon = NoBoon,
            BoonWeightDelta = BoonWeightDelta, PairsDeltaNext = PairsDeltaNext, ForcedNext = ForcedNext,
        };
    }

    /// <summary>
    /// One boon or cost - or an Albedo or a combination, which are entries like any other once
    /// earned. Copy and effect live together on purpose: an entry whose words and behaviour are
    /// declared in two files drifts apart the first time either is tuned.
    /// </summary>
    public class ExchangeEntry
    {
        public string Id;
        public string Name;

        /// <summary>Player-facing: what ONE stack does (a unique entry: what it does). Present
        /// tense, generated from the same Tuning.Exchange constant the effect reads.</summary>
        public string Effect;

        public ExchangeKind Kind;
        public ExchangeFamily Family;
        public ExchangeMark Mark;
        public EntryOrigin Origin = EntryOrigin.Base;

        /// <summary>1 a nudge, 2 changes a decision, 3 changes the build. Drives pairing.</summary>
        public int Weight = 1;

        /// <summary>How many of it one run may hold. At the cap it withdraws from the pool.</summary>
        public int MaxStacks = 1;

        /// <summary>Deal-shaping and stackless: rests Tuning.Exchange.RecurringRestDeals after
        /// it is taken, then may come again.</summary>
        public bool Recurring;

        /// <summary>
        /// The capstone at max stacks - a RUBEDO on a boon (it changes how the boon plays), a
        /// NIGREDO on a cost (a harsher twist on top). Null on a unique entry.
        /// </summary>
        public string CapName, CapText;

        /// <summary>A cost's Albedo: the boon a transmutation circle turns it into.</summary>
        public string AlbedoId;

        /// <summary>A combination's two parts (any stacks of each).</summary>
        public string[] Parts;

        /// <summary>Offered only to this element. Null for every element.</summary>
        public ElementType? Element;

        /// <summary>Offered only to these weapon classes. Null for every class.</summary>
        public WeaponClass[] Classes;

        /// <summary>
        /// Applies this entry to the accumulator, given how many stacks are held - the capstone
        /// included once <paramref name="n"/> reaches MaxStacks. Null for an entry that is wholly
        /// conditional (RunEffects) or deal-shaping.
        /// </summary>
        public Action<Mods, int> Apply;

        /// <summary>Fires once, the moment this entry is taken: entries that alter the next deal.</summary>
        public Action<PendingOffer> OnTaken;

        /// <summary>Behaviour that needs a clock, a counter or a memory lives in RunEffects, keyed
        /// by id. Flagged rather than inferred, because nothing here can see it.</summary>
        public bool Conditional;

        public bool Stackable => MaxStacks > 1;
        public bool IsCombination => Origin == EntryOrigin.Conjunction || Origin == EntryOrigin.Putrefaction
                                     || Origin == EntryOrigin.Citrinitas;
        public bool IsLive => Apply != null || OnTaken != null || Conditional;

        /// <summary>The Rubedo/Nigredo word for this entry's capstone.</summary>
        public string CapKind => Kind == ExchangeKind.Boon ? "Rubedo" : "Nigredo";

        /// <summary>
        /// The card's whole description: what a stack does, and - on a stackable entry - what
        /// max stacks adds.
        /// </summary>
        public string Describe()
        {
            if (CapName == null) return Effect;
            return $"{Effect} At {Roman(MaxStacks)}, {CapKind} - {CapName}: {CapText}";
        }

        public static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => n.ToString() };

        public bool FitsClass(WeaponClass weapon)
        {
            if (Classes == null) return true;
            foreach (var c in Classes) if (c == weapon) return true;
            return false;
        }
    }
}
