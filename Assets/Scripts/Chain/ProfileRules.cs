using UnityEngine;
using Convergence.Core;

namespace Convergence.Chain
{
    /// <summary>
    /// What a checkpoint DOES to a profile - the one copy every store calls before it persists.
    ///
    /// The stores must agree about what a run does, or swapping backends silently changes
    /// progression. They used to each carry these lines (LocalJsonProfileStore, the chain store),
    /// kept in step by a comment; now there is nothing to keep in step.
    /// </summary>
    public static class ProfileRules
    {
        public static void RunStart(CharacterProfile profile, ElementType element)
        {
            profile.LastElement = element;
            // Every run starts in whole armour (the user's call, 2026-10-20): the hub has no way to
            // repair it, so wear carried between runs was a penalty with no answer.
            profile.Wear?.Entries?.Clear();
        }

        public static void RunEnd(CharacterProfile profile, RunSummary summary)
        {
            profile.TotalRuns += 1;
            profile.Wear?.Entries?.Clear();   // wear is the run's, never the character's
            profile.TotalKills += summary.Kills;
            profile.BestKills = Mathf.Max(profile.BestKills, summary.Kills);
            profile.BestFloor = Mathf.Max(profile.BestFloor, summary.Floors);
            profile.Xp += summary.XpEarned;
            profile.Level = 1 + profile.Xp / 100;
            if (summary.GuaranteeWeek > profile.GuaranteeWeek) profile.GuaranteeWeek = summary.GuaranteeWeek;
            profile.RecentRuns.Add(summary);
            if (profile.RecentRuns.Count > 10) profile.RecentRuns.RemoveAt(0);
        }
    }
}
