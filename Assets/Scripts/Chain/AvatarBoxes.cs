using System;

namespace Convergence.Chain
{
    /// <summary>
    /// AN AVATAR'S BOXES - the Diamond and Black Diamond boxes an avatar boss (25, 50, 75) drops on
    /// top of its floor's box, rolled separately at Tuning.GearRoll.AvatarDiamondBoxChance /
    /// AvatarBlackDiamondBoxChance.
    ///
    /// Like floor 100's (<see cref="FinalFloorReward"/>), a service player's are decided by the
    /// SERVICE (web/service/src/avatarBoxes.ts, the same numbers - keep the two in step) and
    /// recorded against the run, so asking again answers the same boxes and the run's end can only
    /// mint what was recorded; a local save rolls them here (<see cref="Rifts.RunLoot.RollAvatarLocal"/>).
    /// Asked through <see cref="IProfileStore.AvatarBoxesAsync"/>.
    /// </summary>
    [Serializable]
    public struct AvatarBoxes
    {
        public int Floor;
        public int Diamond;
        public int BlackDiamond;
    }
}
