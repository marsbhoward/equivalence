using System;
using Convergence.Art.Gear;
using Convergence.Core;

namespace Convergence.Chain
{
    /// <summary>
    /// FLOOR 100's BOX - the one drop the game does not roll on its own (the user's calls,
    /// 2026-10-13; MONETIZATION.md "Drops"):
    ///
    ///     the FIRST floor-100 clear of the week, per CHARACTER    a Diamond box
    ///                                                             (Black Diamond 1% instead)
    ///     every later clear that week                             Diamond 2%, Black Diamond 1%,
    ///                                                             otherwise 2-3 Gold boxes
    ///
    /// The week turns at SUNDAY 00:00 UTC. It carries a weekly GUARANTEE, and a guarantee the
    /// client decided would be claimed every clear - so for a service player the SERVICE decides it
    /// (web/service/src/finalFloor.ts, the same numbers - keep the two in step) and records it
    /// against the run; a local save decides it here, from <see cref="CharacterProfile.GuaranteeWeek"/>.
    /// Asked through <see cref="IProfileStore.FinalFloorRewardAsync"/>.
    /// </summary>
    [Serializable]
    public struct FinalFloorReward
    {
        public LootTier Tier;
        public int Count;
        /// <summary>This was the week's guaranteed box.</summary>
        public bool Guaranteed;
        /// <summary>The week it was decided in (<see cref="WeekOf"/>).</summary>
        public int Week;

        /// <summary>1970-01-04, the first Sunday after the epoch: week 0 began here.</summary>
        static readonly DateTime FirstSunday = new(1970, 1, 4, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>The guarantee's week for a UTC moment - each turns at Sunday 00:00 UTC.</summary>
        public static int WeekOf(DateTime utc) => (int)Math.Floor((utc - FirstSunday).TotalDays / 7.0);

        public static int ThisWeek => WeekOf(DateTime.UtcNow);

        /// <summary>When <paramref name="week"/> begins - the moment the guarantee before it resets.</summary>
        public static DateTime WeekStarts(int week) => FirstSunday.AddDays(week * 7.0);

        /// <summary>The rule, for a store that decides it itself.</summary>
        public static FinalFloorReward Roll(bool guaranteeUnused, int week, Random rng)
        {
            double r = rng.NextDouble();
            if (guaranteeUnused)
                return new FinalFloorReward
                {
                    Tier = r < Tuning.GearRoll.FinalGuaranteedBlackDiamondChance ? LootTier.BlackDiamond : LootTier.Diamond,
                    Count = 1, Guaranteed = true, Week = week,
                };
            if (r < Tuning.GearRoll.FinalLaterBlackDiamondChance)
                return new FinalFloorReward { Tier = LootTier.BlackDiamond, Count = 1, Week = week };
            if (r < Tuning.GearRoll.FinalLaterBlackDiamondChance + Tuning.GearRoll.FinalLaterDiamondChance)
                return new FinalFloorReward { Tier = LootTier.Diamond, Count = 1, Week = week };
            return new FinalFloorReward
            {
                Tier = LootTier.Gold,
                Count = rng.Next(Tuning.GearRoll.FinalGoldBoxesMin, Tuning.GearRoll.FinalGoldBoxesMax + 1),
                Week = week,
            };
        }

        /// <summary>
        /// What floor 100 pays when no decision could be had (the service never answered): the
        /// least Gold it could have paid, and NOT the guarantee - nothing was recorded, so the
        /// week's guaranteed box is still waiting for the next clear.
        /// </summary>
        public static FinalFloorReward Unanswered => new()
            { Tier = LootTier.Gold, Count = Tuning.GearRoll.FinalGoldBoxesMin, Week = ThisWeek };
    }
}
