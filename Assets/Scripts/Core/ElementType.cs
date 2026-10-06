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
            ElementType.Fire  => "Stacking icons. Keep attacking or the heat bleeds off.",
            ElementType.Water => "Tiered meter. Cash out early for utility, or gamble for the burst.",
            ElementType.Earth => "Stand still to charge. Release an earthquake with aftershocks.",
            _                 => "Crits build momentum: faster, longer reach. Streaks raise your crit floor.",
        };

        /// <summary>The element's default release - what the ability button fires until the
        /// board has bought an alternative and the player has chosen it.</summary>
        public static string AbilityName(ElementType e) => e switch
        {
            ElementType.Fire  => "ERUPT",
            ElementType.Water => "SURGE",
            ElementType.Earth => "QUAKE",
            _                 => "GUST",
        };

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
