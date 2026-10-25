using System;
using System.Collections.Generic;
using Convergence.Art.Gear;
using Convergence.Core;

namespace Convergence.Chain
{
    /// <summary>
    /// The cosmetic rolls for a LOCAL save (Tuning.Cosmetics) - a rare dye, a pose, a milestone. A
    /// service player's are the SERVICE's (web/service/src/cosmetics.ts, the same rules - keep the
    /// two in step) and are never rolled here. System.Random, never UnityEngine.Random: the run's
    /// seeded rolls share that one's state.
    /// </summary>
    public static class CosmeticRolls
    {
        static Random _rng;
        static Random Rng => _rng ??= new Random();

        static string Weighted(Random rng, Func<DyeSwatch, int> weight)
        {
            int total = 0;
            foreach (var s in DyeCatalog.All) total += weight(s);
            if (total <= 0) return null;
            int r = rng.Next(total);
            foreach (var s in DyeCatalog.All)
            {
                r -= weight(s);
                if (r < 0) return s.Id;
            }
            return null;
        }

        /// <summary>A Rare or Very Rare swatch, Rare <see cref="Tuning.Cosmetics.RareWeight"/> times as likely.</summary>
        public static string RareDye(Random rng = null)
            => Weighted(rng ?? Rng, s => s.Rarity == DyeRarity.Rare ? Tuning.Cosmetics.RareWeight
                                    : s.Rarity == DyeRarity.VeryRare ? Tuning.Cosmetics.VeryRareWeight : 0);

        public static string DyeOf(DyeRarity rarity, Random rng = null)
            => Weighted(rng ?? Rng, s => s.Rarity == rarity ? 1 : 0);

        /// <summary>A pose roll: a pose the character lacks at <see cref="Tuning.Cosmetics.PoseChance"/>,
        /// or Gold boxes when every pose is owned.</summary>
        public static (string pose, int gold) Pose(CharacterItems owned, Random rng = null)
        {
            rng ??= Rng;
            if (rng.NextDouble() >= Tuning.Cosmetics.PoseChance) return (null, 0);
            var lacking = new List<string>();
            foreach (var p in PoseCatalog.All) if (owned == null || !owned.HasPose(p.Id)) lacking.Add(p.Id);
            if (lacking.Count == 0) return (null, rng.Next(Tuning.Cosmetics.PoseGoldMin, Tuning.Cosmetics.PoseGoldMax + 1));
            return (lacking[rng.Next(lacking.Count)], 0);
        }

        /// <summary>The milestone the current format's <paramref name="floor"/> is, or null - named by
        /// its PLACE in the list (milestone.1 ...), so it is the same milestone in either format.</summary>
        public static MilestoneReward? MilestoneAt(int floor, Random rng = null)
        {
            var floors = Tuning.Cosmetics.MilestoneFloors;
            int i = Array.IndexOf(floors, floor);
            if (i < 0) return null;
            var rarity = i < Tuning.Cosmetics.MilestoneDyeRarity.Length ? Tuning.Cosmetics.MilestoneDyeRarity[i] : DyeRarity.Rare;
            return new MilestoneReward
            {
                Floor = floor,
                Milestone = $"milestone.{i + 1}",
                Diamond = i == Tuning.Cosmetics.MilestoneBoxIndex ? 1 : 0,
                Dye = DyeOf(rarity, rng),
            };
        }
    }
}
