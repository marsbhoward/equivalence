using System;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// One rolled secondary stat on a piece of gear - a kind and where in its range it landed.
    ///
    /// Value is the UNSCALED roll: the number drawn from the tier's range, before the item's star
    /// level multiplies it (GearRoller.BuildGrants applies that). Stored unscaled so combining can
    /// compare two rolls at the same level directly and "keep the higher" means the better ROLL,
    /// and so a promotion's re-roll into the next tier's range starts from a clean number.
    ///
    /// A plain class rather than a struct so JsonUtility serialises a List of them the way it
    /// already does everything else on MintedGearRecord.
    /// </summary>
    [Serializable]
    public class SubStat
    {
        public StatKind Kind;
        public float Value;

        public SubStat() { }
        public SubStat(StatKind kind, float value) { Kind = kind; Value = value; }
    }
}
