namespace Convergence.Art.Gear
{
    /// <summary>
    /// The chest's active defensive tech. Every torso item rolls one of these four - never
    /// "none" - because the mechanic is meant to be available from a player's very first chest
    /// piece, not gated behind tier or grind. All four share the same instant, omnidirectional
    /// parry check at the moment of activation; what differs is the fallback if that timing is
    /// missed - see PlayerController's defensive-ability block for the full shape.
    /// </summary>
    /// <remarks>
    /// Renamed once already: what is now <c>Barrier</c> was <c>Shield</c>, and what is now
    /// <c>Bulwark</c> was <c>Barrier</c>. Both kept their ORDINAL - the enum serialises as an int
    /// (see GearSlot's own note), so a rename is free and a reorder would silently repoint every
    /// saved torso at a different ability.
    /// </remarks>
    public enum DefensiveAbility { Dash, Barrier, Bulwark, ParryStance }

    public static class DefensiveAbilityInfo
    {
        /// <summary>
        /// The player-facing name. Everywhere ELSE that a torso's ability needs to be legible -
        /// the gear picker's card and the loadout screen's equipped-slot row - reads this, because
        /// it did not exist before: a torso card showed art, tier, power and a name, and the
        /// equipped-slot row showed only the name, so nothing anywhere ever told a player which of
        /// the four abilities a given chest piece actually grants. `ParryStance` needed a space
        /// added; everything else already reads correctly off `ToString()`.
        /// </summary>
        public static string Label(this DefensiveAbility a) =>
            a == DefensiveAbility.ParryStance ? "Parry Stance" : a.ToString();
    }
}
