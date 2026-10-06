using System;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// What a piece of gear is worth, in PERCENTAGE POINTS on the character's baselines.
    ///
    /// Zero means "grants nothing", 10 means "+10%". 100% of any stat is the matching number in
    /// <see cref="Core.Tuning.Player"/>, which is where the game's actual units live.
    ///
    /// PERCENTAGE POINTS, SUMMED - not multipliers. Points compose predictably: a +10% helm and a
    /// +5% node are +15%, and the number printed on the item is true no matter what else is worn.
    /// Multipliers compound instead, so the same helm is worth more on a geared character than a
    /// bare one, and no single figure on any item means anything on its own. That is the whole
    /// reason this type exists rather than a float per stat scattered across GearItem.
    ///
    /// One type rather than six fields also means adding a stat is one line here and one line in
    /// the two places that consume it, instead of a field on every item and a sum at every site.
    /// </summary>
    [Serializable]
    public class StatPercents
    {
        [Tooltip("Percentage points of damage dealt. 10 = +10%.")]
        public float Damage;

        [Tooltip("Percentage points of attack speed - swing cadence AND the animation with it.")]
        public float AttackSpeed;

        [Tooltip("Percentage points of walk speed.")]
        public float MoveSpeed;

        [Tooltip("Percentage points of swing reach.")]
        public float Range;

        [Tooltip("Percentage points of maximum health.")]
        public float MaxHp;

        [Tooltip("Percentage points added to the size of the ARMOUR durability pool (every " +
                 "armour-kind item's own MaxDurability). A bigger buffer before wear starts " +
                 "costing you, not a direct damage reduction - see Resilience for that.")]
        public float Armor;

        [Tooltip("Percentage points that slow how fast the armour pool drains per hit. Protects " +
                 "the buffer itself, rather than growing it (Armor) or bypassing it (Resilience).")]
        public float DamageResistance;

        [Tooltip("Percentage points of direct incoming-damage mitigation, independent of armour " +
                 "condition entirely - it still helps at zero armour and does nothing extra at " +
                 "full armour.")]
        public float Resilience;

        [Tooltip("Percentage points added to whichever element is equipped's own continuous " +
                 "build rate (water's meter, earth's charge, air's momentum) - composed with " +
                 "that element's own mastery gain-rate node. Fire does not consume this: its " +
                 "heat is a discrete per-swing stack, not a rate.")]
        public float ElementGrowth;

        [Tooltip("Percentage points off the defensive ability's cooldown (Dash/Barrier/Bulwark/" +
                 "Parry Stance). Declared now so it can be rolled onto gear ahead of the ability " +
                 "system itself - consumed once that lands.")]
        public float AbilityCooldownReduction;

        [Tooltip("Percentage points of weapon art damage.")]
        public float FinisherPower;

        [Tooltip("Percentage points on how far a weapon art that already DISPLACES (Heavy) throws or pulls. Never touches flinch - a longer flinch would let a player stun-lock an enemy.")]
        public float FinisherKnockback;

        [Tooltip("Percentage points of crit CHANCE, added straight on (10 = +10% chance) over Tuning.Player.BaseCritChance and whatever the element grants. A playstyle for every element, not an Air exclusive.")]
        public float CritChance;

        [Tooltip("Percentage points added to the crit MULTIPLIER (30 = x1.8 becomes x2.1).")]
        public float CritDamage;

        [Tooltip("Percentage points of damage mitigation on hits taken while MOVING. Brace's mirror.")]
        public float Graze;

        [Tooltip("Percentage points of damage mitigation on hits taken while STANDING STILL. Graze's mirror.")]
        public float Brace;

        [Tooltip("Percentage points that shrink the damage lost per extra body: a blade's chain falloff through a crowd, a disc's falloff per ricochet.")]
        public float Cleave;

        [Tooltip("Percentage points on everything an elemental release does.")]
        public float ElementalEffectiveness;

        [Tooltip("Percentage points on how long a PARTIAL chain survives between swings. An armed weapon art never expires anyway.")]
        public float ComboTime;

        [Tooltip("Percentage points of radius on area-of-effect attacks.")]
        public float AoeRadius;

        [Tooltip("Percentage points on the floor reward's Small Heal - that card only.")]
        public float HealReceived;

        [Tooltip("Percentage points on the floor reward's Repair Gear - that card only.")]
        public float RepairReceived;

        [Tooltip("Percent of each worn piece's condition restored when a floor clears.")]
        public float Mend;

        [Tooltip("Percent of a hit's damage dealt to every OTHER enemy in a small radius around the target it landed on. The designated target only - the first body of a swing, the first of a throw - never the whole cleave, or it would compound.")]
        public float Splash;

        [Tooltip("BOW ONLY. Percent of an arrow's damage dealt to every enemy in a line behind the target it hits.")]
        public float Pierce;

        [Tooltip("Points that raise the BOTTOM of every hit's damage range toward its top (Tuning.Attack.DamageSpread). 100 = every hit at the top, worth +DamageSpread on average - more damage with a hard ceiling built in.")]
        public float Accuracy;

        public void Add(StatPercents other)
        {
            if (other == null) return;
            Damage += other.Damage;
            AttackSpeed += other.AttackSpeed;
            MoveSpeed += other.MoveSpeed;
            Range += other.Range;
            MaxHp += other.MaxHp;
            Armor += other.Armor;
            DamageResistance += other.DamageResistance;
            Resilience += other.Resilience;
            ElementGrowth += other.ElementGrowth;
            AbilityCooldownReduction += other.AbilityCooldownReduction;
            FinisherPower += other.FinisherPower;
            FinisherKnockback += other.FinisherKnockback;
            CritChance += other.CritChance;
            CritDamage += other.CritDamage;
            Graze += other.Graze;
            Brace += other.Brace;
            Cleave += other.Cleave;
            ElementalEffectiveness += other.ElementalEffectiveness;
            ComboTime += other.ComboTime;
            AoeRadius += other.AoeRadius;
            HealReceived += other.HealReceived;
            RepairReceived += other.RepairReceived;
            Mend += other.Mend;
            Splash += other.Splash;
            Pierce += other.Pierce;
            Accuracy += other.Accuracy;
        }

        /// <summary>
        /// Apply points to a baseline. Floored at 10% of it, so a pile of negative modifiers can
        /// slow or weaken a character without ever inverting the stat - a negative attack interval
        /// or a negative reach is not a debuff, it is a crash.
        /// </summary>
        public static float Apply(float baseline, float points)
            => baseline * Mathf.Max(0.1f, 1f + points / 100f);

        /// <summary>
        /// Points that REDUCE a rate rather than grow a baseline - Resilience on incoming damage,
        /// DamageResistance on armour wear. 1 at zero points, falling toward 0.1 as points climb
        /// (diminishing returns, same floor-not-invert shape as <see cref="Apply"/>), and rising
        /// above 1 for negative points so a debuff genuinely makes things worse rather than
        /// bottoming out at "no effect."
        /// </summary>
        public static float ReductionFactor(float points)
            => Mathf.Clamp(1f / (1f + points / 100f), 0.1f, 3f);

        /// <summary>
        /// Every field, as one stable string - what a gear STACK is keyed on, together with the
        /// rest of an item's attributes (see MintedGearRecord.StackSignature).
        ///
        /// It lists every field explicitly rather than calling ToString: that one is a HUMAN line and
        /// is free to be reworded, reordered or abbreviated, and a display string doubling as an
        /// identity is one rewrite away from silently merging two different rolls. Adding a stat
        /// means one more line here, which is the same one-line cost this type's own header
        /// already claims for Add and Apply.
        ///
        /// Two decimals, invariant: a roll lands on clean values and printing full float
        /// precision would let 6.0000001 and 6f read as different items.
        /// </summary>
        public string Signature()
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return Damage.ToString("0.##", c) + "/" + AttackSpeed.ToString("0.##", c)
                 + "/" + MoveSpeed.ToString("0.##", c) + "/" + Range.ToString("0.##", c)
                 + "/" + MaxHp.ToString("0.##", c) + "/" + Armor.ToString("0.##", c)
                 + "/" + DamageResistance.ToString("0.##", c)
                 + "/" + Resilience.ToString("0.##", c)
                 + "/" + ElementGrowth.ToString("0.##", c)
                 + "/" + AbilityCooldownReduction.ToString("0.##", c)
                 + "/" + FinisherPower.ToString("0.##", c)
                 + "/" + FinisherKnockback.ToString("0.##", c)
                 + "/" + CritChance.ToString("0.##", c)
                 + "/" + CritDamage.ToString("0.##", c)
                 + "/" + Graze.ToString("0.##", c)
                 + "/" + Brace.ToString("0.##", c)
                 + "/" + Cleave.ToString("0.##", c)
                 + "/" + ElementalEffectiveness.ToString("0.##", c)
                 + "/" + ComboTime.ToString("0.##", c)
                 + "/" + AoeRadius.ToString("0.##", c)
                 + "/" + HealReceived.ToString("0.##", c)
                 + "/" + RepairReceived.ToString("0.##", c)
                 + "/" + Mend.ToString("0.##", c)
                 + "/" + Splash.ToString("0.##", c)
                 + "/" + Pierce.ToString("0.##", c)
                 + "/" + Accuracy.ToString("0.##", c);
        }

        public override string ToString()
            => $"dmg {Damage:+0.#;-0.#;0}%  spd {AttackSpeed:+0.#;-0.#;0}%  "
             + $"move {MoveSpeed:+0.#;-0.#;0}%  range {Range:+0.#;-0.#;0}%  hp {MaxHp:+0.#;-0.#;0}%  "
             + $"armor {Armor:+0.#;-0.#;0}%  dmgres {DamageResistance:+0.#;-0.#;0}%  "
             + $"resil {Resilience:+0.#;-0.#;0}%  elemgrow {ElementGrowth:+0.#;-0.#;0}%  "
             + $"abilcd {AbilityCooldownReduction:+0.#;-0.#;0}%  "
             + $"finpow {FinisherPower:+0.#;-0.#;0}%  finkb {FinisherKnockback:+0.#;-0.#;0}%  "
             + $"crit {CritChance:+0.#;-0.#;0}%  critdmg {CritDamage:+0.#;-0.#;0}%  "
             + $"graze {Graze:+0.#;-0.#;0}%  brace {Brace:+0.#;-0.#;0}%  cleave {Cleave:+0.#;-0.#;0}%  "
             + $"elemfx {ElementalEffectiveness:+0.#;-0.#;0}%  combo {ComboTime:+0.#;-0.#;0}%  "
             + $"aoe {AoeRadius:+0.#;-0.#;0}%  heal {HealReceived:+0.#;-0.#;0}%  "
             + $"repair {RepairReceived:+0.#;-0.#;0}%  mend {Mend:+0.#;-0.#;0}%  "
             + $"splash {Splash:+0.#;-0.#;0}%  pierce {Pierce:+0.#;-0.#;0}%  accuracy {Accuracy:+0.#;-0.#;0}";
    }
}
