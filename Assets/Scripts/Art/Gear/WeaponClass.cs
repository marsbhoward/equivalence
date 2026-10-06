namespace Convergence.Art.Gear
{
    /// <summary>
    /// How a weapon is FOUGHT WITH, as opposed to what it looks like or what it rolls.
    ///
    /// Until now this was implicit: every weapon in the game was a greatsword, so the chain, the
    /// swing animation, the reach and the whole moveset library could all assume one answer. The
    /// class is what makes that assumption explicit so a second one can exist beside it.
    ///
    /// A class owns three things: how a basic resolves, which finishers it may roll, and the grip
    /// the rig holds it in. Everything else - damage, tier, durability, transmog - is per ITEM and
    /// has nothing to do with class.
    /// </summary>
    public enum WeaponClass
    {
        /// <summary>Two-handed melee. Slow, wide, single reach band. The original.</summary>
        Greatsword,

        /// <summary>
        /// Hybrid. Slow heavy melee inside reach, faster ricocheting throws outside it, switched
        /// automatically by whether anything is close enough to hit.
        ///
        /// Neither range is the main one. Single-target damage per second is level between them
        /// by design; what differs is the shape - one big hit on one body versus a smaller hit
        /// that keeps going. See Tuning.Disc.
        /// </summary>
        Disc,

        /// <summary>
        /// Pure ranged, single target, no bounce - the third weapon class, and the only one with
        /// no melee mode at all to fall back on. The slowest cadence and the widest range circle
        /// in the game, and damage scales UP with distance: standing close is the weak case, not
        /// the safe one. See Tuning.Bow.
        /// </summary>
        Bow,
    }

    public static class WeaponClasses
    {
        public static string Name(WeaponClass c) => c switch
        {
            WeaponClass.Disc => "Discs",
            WeaponClass.Bow => "Bow",
            _ => "Greatsword",
        };

        /// <summary>
        /// What one connecting hit is worth to a per-hit resource.
        ///
        /// Discs land three to five times as often as a greatsword - every bounce is a hit, at a
        /// shorter interval - so an unscaled per-hit meter filled in under a second and water's
        /// entire gamble evaporated. Scaled, a disc fills slower than a sword when played safe
        /// and faster only when the player commits to a high-density finisher, which is the
        /// trade worth having.
        /// </summary>
        public static float HitMeterScale(WeaponClass c) => c switch
        {
            WeaponClass.Disc => 0.18f,
            _ => 1f,
        };

        /// <summary>
        /// What the class is FOR, in one line. Shown wherever a player is choosing between them.
        /// </summary>
        public static string Tagline(WeaponClass c) => c switch
        {
            WeaponClass.Disc => "Heavy up close, fast at range. Thrown discs ricochet between enemies.",
            WeaponClass.Bow => "The widest reach in the game. Slow and single-target - the farther the shot, the harder it hits.",
            _ => "Heavy two-handed swings. Everything happens inside your reach.",
        };
    }
}
