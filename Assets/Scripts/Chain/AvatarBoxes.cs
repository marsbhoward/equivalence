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
        /// <summary>A RARE dye (swatch id) on top of the boxes, or empty - already the character's
        /// (CharacterItems: database first, credited at the decision).</summary>
        public string Dye;
        /// <summary>A portrait pose the character lacked, or empty - already the character's.</summary>
        public string Pose;
        /// <summary>Gold boxes the pose roll paid instead, the character owning every pose.</summary>
        public int PoseGold;
    }

    /// <summary>
    /// A MILESTONE (Tuning.Cosmetics): the first clear of one of the run format's milestone floors,
    /// once per character - a dye (already the character's) and, for the first, a Diamond box that
    /// joins the run's loot like an avatar's and is minted at the run's end. The service decides it
    /// for a service player (web/service/src/routes/items.ts); a local save decides it itself.
    /// </summary>
    [Serializable]
    public struct MilestoneReward
    {
        public int Floor;
        public string Milestone;
        public int Diamond;
        public string Dye;
    }
}
