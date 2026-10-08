using UnityEngine;
using T = Convergence.Core.Tuning.StrikeTiming;

namespace Convergence.Combat
{
    /// <summary>What the first tap during a finisher's timing bar earned.</summary>
    public enum StrikeVerdict
    {
        /// <summary>The bar is still running and nothing has been pressed yet.</summary>
        Pending,
        /// <summary>Pressed in the lead segment, before any good band. Costs what no press costs.</summary>
        Early,
        /// <summary>Pressed in either yellow (good) band.</summary>
        Good,
        /// <summary>Pressed in the green (perfect) band.</summary>
        Perfect,
        /// <summary>The strike landed with nothing pressed.</summary>
        Missed,
    }

    /// <summary>
    /// The finisher timing bar's rules, kept apart from the controller that runs it and the
    /// component that draws it so all three read one definition. Numbers live in
    /// <see cref="Core.Tuning.StrikeTiming"/>.
    ///
    /// ONE ATTEMPT PER BAR. The first tap decides and every later one is ignored. An early tap
    /// costs exactly what no tap costs, so mashing spends its first press in the lead segment
    /// and can never reach the perfect band: strictly worse than waiting, not merely less efficient.
    /// </summary>
    public static class StrikeJudge
    {
        /// <summary>Where a press <paramref name="untilStrike"/> seconds before the strike lands.
        /// Measured from the STRIKE back, because the judged segments are the same on every bar
        /// and only the lead before them changes length. Past the strike is a miss - it has
        /// already happened.</summary>
        public static StrikeVerdict Judge(float untilStrike)
        {
            float seg = T.SegmentSeconds;
            if (untilStrike > seg * 3f) return StrikeVerdict.Early;
            if (untilStrike > seg * 2f) return StrikeVerdict.Good;
            if (untilStrike > seg) return StrikeVerdict.Perfect;
            if (untilStrike >= 0f) return StrikeVerdict.Good;
            return StrikeVerdict.Missed;
        }

        /// <summary>
        /// The damage multiplier a verdict pays. PENDING pays GOOD: it is only ever read by a hit
        /// that lands before the bar has closed (the katana's two sheathed cuts, which resolve
        /// before its draw), and GOOD is exactly today's damage once parity is applied - so a
        /// hit that could not be judged is neither rewarded nor punished.
        /// </summary>
        public static float Multiplier(StrikeVerdict v) => v switch
        {
            StrikeVerdict.Perfect => T.PerfectMultiplier,
            StrikeVerdict.Good => T.GoodMultiplier,
            StrikeVerdict.Pending => T.GoodMultiplier,
            _ => T.MissMultiplier,
        };

        /// <summary>
        /// The catch-up multiplier on a timed finisher, chosen so a chain landing GOOD deals
        /// exactly the damage per second it dealt before the bar existed.
        ///
        /// A finisher that had to GAIN a wind-up pays for it in tempo; this pays it back in
        /// damage. One that already delayed its damage gains no time, so its factor is
        /// 1 / GoodMultiplier - GOOD lands where it always did, PERFECT above, a miss below.
        /// That is "Good = today" applied evenly: otherwise the moves with long, easy-to-read
        /// wind-ups would get a free +10% for a press they were going to make anyway.
        ///
        /// Computed LIVE from the chain the player is actually swinging, never typed in: the
        /// wind-up is absolute seconds while the swings scale with attack speed, so a constant
        /// would drift the moment a speed buff landed. Single target - the same simplification
        /// PlayerPower makes.
        /// </summary>
        /// <param name="basicDamage">Damage of the chain's basics together.</param>
        /// <param name="finisherDamage">Damage of the finisher before any timing multiplier.</param>
        /// <param name="chainSeconds">The chain's length without any added wind-up.</param>
        /// <param name="addedSeconds">The wind-up this finisher had to gain.</param>
        public static float Parity(float basicDamage, float finisherDamage,
                                   float chainSeconds, float addedSeconds)
        {
            if (finisherDamage <= 0f || chainSeconds <= 0f) return 1f;
            float perSecond = (basicDamage + finisherDamage) / chainSeconds;
            float needed = perSecond * (chainSeconds + addedSeconds) - basicDamage;
            return Mathf.Max(0f, needed) / (finisherDamage * T.GoodMultiplier);
        }
    }
}
