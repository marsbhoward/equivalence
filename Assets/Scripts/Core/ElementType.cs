namespace Convergence.Core
{
    /// <summary>The four playable elements. Each has a mechanically distinct resource rhythm.</summary>
    public enum ElementType
    {
        Fire,   // Stacking icons  - attrition, sustained aggression
        Water,  // Tiered meter    - pressure / payoff, player chooses cash-out tier
        Earth,  // Stillness meter - slow, durable, charge-and-release
        Air,    // Crit scaling    - speed / finesse, momentum snowball
    }

    public static class ElementInfo
    {
        public static UnityEngine.Color Tint(ElementType e) => e switch
        {
            ElementType.Fire  => new UnityEngine.Color(1.00f, 0.42f, 0.18f),
            ElementType.Water => new UnityEngine.Color(0.25f, 0.62f, 1.00f),
            ElementType.Earth => new UnityEngine.Color(0.55f, 0.78f, 0.36f),
            _                 => new UnityEngine.Color(0.85f, 0.85f, 0.95f),
        };

        public static string ResourceName(ElementType e) => e switch
        {
            ElementType.Fire  => "HEAT",
            ElementType.Water => "PRESSURE",
            ElementType.Earth => "STABILITY",
            _                 => "MOMENTUM",
        };

        public static string Tagline(ElementType e) => e switch
        {
            ElementType.Fire  => "Heat from every blow you land or take. Stop fighting and it drains.",
            ElementType.Water => "Every hit fills it, double on soaked foes; a miss costs it. Choose your shots.",
            ElementType.Earth => "Stand still to charge. Release an earthquake with aftershocks.",
            _                 => "Moving builds momentum: faster swings and legs. A streak of hits raises Accuracy.",
        };

        /// <summary>The element's starting release - its Blast (RELEASES.md).</summary>
        public static string AbilityName(ElementType e) => ReleaseInfo.Name(e, ReleaseKind.Blast);

        /// <summary>
        /// The alternative release the DamageSource/elemental keystone offers, or null for an
        /// element that has not built one yet. The mastery screen reads this to decide whether
        /// there is a choice to offer at all - a toggle between one option and nothing is not a
        /// choice.
        /// </summary>
        public static string SecondAbilityName(ElementType e) => e switch
        {
            ElementType.Air  => "SQUALL",
            ElementType.Fire => "IGNITE",
            _                => null,
        };
    }
}
