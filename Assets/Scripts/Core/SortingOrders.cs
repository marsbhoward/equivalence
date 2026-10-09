namespace Convergence.Core
{
    /// <summary>
    /// Every sprite sorting order in one place.
    ///
    /// These were scattered as literals across the factories and the rig, which is how combat FX
    /// ended up at 20 - inside the character's own 10..30 band, so half the paper-doll drew on top
    /// of the ability bursts that were supposed to sell it. Anything that must read over the
    /// character has to clear <see cref="CharacterTop"/>, and that is only checkable if the
    /// numbers sit next to each other.
    /// </summary>
    public static class SortingOrders
    {
        public const int FloorBack   = -10;
        public const int Floor       = -9;
        public const int FloorDetail = -8;

        /// <summary>Rings and markers painted on the ground, under every body.</summary>
        public const int GroundDecal = -4;

        /// <summary>
        /// Bottom of the floor-pit band (fire, sand, spike), spanning PitBase..PitBase+5.
        ///
        /// ABOVE <see cref="GroundDecal"/> rather than below it, and that is a claim rather than a
        /// convenience: a decal is painted ON the floor, and a pit REPLACES the floor. A range
        /// ring drawn over the inside of a hole would say the hole is a surface.
        ///
        /// Still entirely below <see cref="Enemy"/>, so every body stands in front of whatever it
        /// is standing in - the same rule Spr.GroundBurn already lives by, for the same reason:
        /// a fight happening inside a hazard has to stay visible on top of it.
        /// </summary>
        public const int PitBase = -3;

        /// <summary>
        /// Starting order for an enemy, immediately replaced by its depth once DepthSorted runs.
        /// Only the relative gaps between an enemy's own renderers still matter.
        /// </summary>
        public const int Enemy       = 4;

        /// <summary>
        /// The rig adds its RigLayer index to this, so it spans Character..CharacterTop. This is
        /// only the value a rig is BUILT with - anything in a room re-bases onto its depth, and
        /// what survives is the RigLayer span, not the number.
        /// </summary>
        public const int Character    = 10;
        public const int CharacterTop = 43;   // Character + 33 RigLayers

        // These sit ABOVE the whole depth band below, not just above one character. A status
        // halo or a hit flash that could be hidden behind the enemy standing in front of it
        // would be feedback the player cannot rely on.
        public const int StatusOverlay = 24000;

        /// <summary>The finisher timing meter beside the player (Combat.StrikeBar). An overlay:
        /// a body standing in front must never hide it.</summary>
        public const int StrikeBar = StatusOverlay + 60;

        /// <summary>Riposte's ward in front of the player (Combat.RiposteWard). An overlay for the
        /// meter's reason, just under it.</summary>
        public const int RiposteWard = StatusOverlay + 55;
        /// <summary>Attack tells on the floor (enemy wind-ups, landing marks, the spire's dome rim).
        /// An overlay, so a body standing on one never hides it.</summary>
        public const int Telegraph     = 24100;
        public const int Fx            = 24200;


        // ---- depth band: things that stand on the floor and can occlude each other ----
        //
        // Used by the HUB and by COMBAT. A body the player can walk behind cannot live on a fixed
        // layer, which is what every other order in this file is. Anything in this band sorts by
        // its y instead: lower on the screen means nearer the camera means drawn in front.
        //
        // The known cost of doing this in combat is that a crowd can stand in front of the
        // player. Mitigated rather than avoided: DepthSorted.Bias lets the player sort as if
        // slightly nearer than it is, so it wins close calls without being permanently on top.
        // Every overlay that must never be hidden (status, telegraphs, hit FX) clears the band.

        /// <summary>Bottom of the depth band. Everything below this is fixed-layer scenery.</summary>
        public const int DepthBase = 100;

        /// <summary>
        /// Orders reserved per depth step. Must exceed the rig's 33 layers plus the bow's three
        /// hip vials stacked above them, or two characters at adjacent depths interleave their
        /// limbs into each other. What it leaves spare below each step is what the off-hand disc
        /// borrows; see SetWeaponSplit.
        ///
        /// 40, up from 32, when the elbow added four forearm layers; 44 when the wrist added four
        /// hands (37 layers and 3 vials filled 40 exactly, leaving the off-hand disc no spare order
        /// below the next step); 50 when the knee added six shins (43 layers and 3 vials, four
        /// spare - three since NeckOver made it 44). The band's TOP has to stay under <see cref="StatusOverlay"/>, so
        /// <see cref="DepthSteps"/> came down to pay for it each time.
        /// </summary>
        public const int DepthStride = 50;

        /// <summary>
        /// Depth steps across the band. 471 over 32 units is ~7cm of resolution, and
        /// DepthBase + DepthSteps * DepthStride (23650) stays clear of StatusOverlay.
        /// </summary>
        public const int DepthSteps = 471;

        /// <summary>
        /// World y mapped to the front and back of the band. Wide enough for the ARENA (half
        /// height 7, plus the floor margin the camera pans over), which also covers the hub.
        /// Anything outside clamps, so a body beyond the range sorts as if at the edge rather
        /// than wrapping around to the front.
        /// </summary>
        public const float DepthTop = 16f;
        public const float DepthBottom = -16f;

        /// <summary>Sorting base for something standing at this world y.</summary>
        public static int ForDepth(float y)
        {
            float t = UnityEngine.Mathf.InverseLerp(DepthTop, DepthBottom, y);
            int step = UnityEngine.Mathf.Clamp(
                UnityEngine.Mathf.RoundToInt(t * (DepthSteps - 1)), 0, DepthSteps - 1);
            return DepthBase + step * DepthStride;
        }
    }
}
