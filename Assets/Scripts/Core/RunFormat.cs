namespace Convergence.Core
{
    /// <summary>
    /// Which RUN FORMAT a run is played in (SHORT-DESCENT.md): Classic's 100 floors, or the Short
    /// Descent's 50, cleared in about an hour. A DEV-ONLY switch - players only ever see one format.
    ///
    ///                 floors   mini-bosses              avatars      depth of floor f
    ///     Classic     100      every 10th               25 50 75 100     f
    ///     Short       50       10 20 30 40              25 50            2f
    ///
    /// TWO KINDS OF FLOOR NUMBER, and every reader must pick the right one:
    ///
    ///   the FLOOR     STRUCTURE - which floors are bosses, the final floor, the deal and reward
    ///                 cadence, Rift planning, the guaranteed elite every third floor.
    ///   the DEPTH     DIFFICULTY and CONTENT - FloorDifficulty, enemy debuts and elite share,
    ///                 hazard unlocks, Medusa's escalations, scenery's hours, loot tiers. Short
    ///                 floor f is priced like Classic floor 2f, so floor 50 is as dangerous as
    ///                 Classic's 100 and every curve tuned for Classic keeps working.
    ///
    /// CONVENTION: functions take the FLOOR, and convert with <see cref="Depth"/> only where they
    /// compare against a depth or read a curve - never pass a depth on, or it is doubled twice.
    ///
    /// THE RUN CLOCK IS THE SERVICE's: it times each floor from the snapshot entering it to the one
    /// clearing it (web/service/src/runProgress.ts), counting the pause menu and a hidden tab during
    /// a fight (the user's call, 2026-10-20 - the pause screen shows the clock still running). The
    /// hourglass must count the same, or the two disagree at 60:00. The run-start save names the
    /// format (ServiceProfileStore), and the service judges the run's boxes by its rules
    /// (web/service/src/runFormat.ts).
    ///
    /// A static resets to the Short Descent on a domain reload - set it again before a run if testing Classic.
    /// </summary>
    public enum RunFormatId { Classic, ShortDescent }

    public static class RunFormat
    {
        // THE SHORT DESCENT IS THE PLAYERS' RUN (the user's call, 2026-10-25); Classic is internal only,
        // reached through the dev toggle (Tuning.Testing.FormatToggle).
        public static RunFormatId Current = RunFormatId.ShortDescent;

        public static bool Short => Current == RunFormatId.ShortDescent;

        /// <summary>The name the service knows the format by.</summary>
        public static string WireName => Short ? "short" : "classic";

        /// <summary>The run's last floor - its end, and the floor whose box carries the weekly guarantee.</summary>
        public static int FinalFloor => Short ? 50 : 100;

        /// <summary>Depth per floor ON AVERAGE: how many Classic floors one floor of this format
        /// stands for over the run. The ledger's per-floor counters step by it (RunModifiers.
        /// FloorCleared), so a card's "a floor" stays one number; difficulty reads <see cref="Depth"/>,
        /// which ramps in the Short Descent's opening floors.</summary>
        public static int DepthPerFloor => Short ? 2 : 1;

        /// <summary>
        /// The Classic floor whose difficulty and content <paramref name="floor"/> has. Classic: the
        /// floor. The Short Descent: Classic's own depth for its first floors, climbing to twice the
        /// floor by Tuning.RunClock.RampCatchUpFloor and twice the floor after (the OPENING RAMP).
        /// </summary>
        public static int Depth(int floor)
        {
            if (!Short) return floor;
            int easy = Tuning.RunClock.RampEasyFloors, caught = Tuning.RunClock.RampCatchUpFloor;
            if (floor <= easy) return floor;
            if (floor >= caught) return floor * 2;
            float t = (floor - easy) / (float)(caught - easy);
            return UnityEngine.Mathf.RoundToInt(easy + t * (caught * 2 - easy));
        }

        /// <summary>The depth reached by the floor BEFORE <paramref name="floor"/> - so a content
        /// threshold crossed between two floors is still met on the floor that crosses it.</summary>
        public static int DepthBefore(int floor) => floor <= 1 ? 0 : Depth(floor - 1);

        /// <summary>
        /// The band a MINI-BOSS fights at (the user's calls, 2026-10-28). Classic: the enemy band. The Short
        /// Descent: floor 10 keeps the enemy band (0, the plain first meeting); later mini-boss floors take
        /// the enemy band +1, or +2 at BossBandTwoChance - but +2 ONCE a run at most - capped at
        /// ShortBossCapBelowTop; from ShortBossTopFrom (floor 40) always the top band. So floor 20 is 2 or
        /// 3, floor 30 is 3. Seeded from the RUN's seed, so a restart meets the same. The AVATAR floors
        /// (25, 50) are a different kind of boss with their own banding, still to come: the enemy band.
        /// </summary>
        public static int BossBand(int floor, int runSeed)
        {
            int band = EnemyBand(floor);
            if (!Short || IsAvatarFloor(floor)) return band;
            int top = Tuning.EnemyBands.ShortEdges.Length;
            if (floor >= Tuning.EnemyBands.ShortBossTopFrom) return top;
            if (floor <= Tuning.EnemyBands.ShortBossPlainThrough) return band;
            // +2 once a run: an earlier mini-boss floor that rolled it spends it
            bool spent = false;
            for (int f = Tuning.EnemyBands.ShortBossPlainThrough + 1; f < floor; f++)
                if (f % Tuning.Boss.RiftInterval == 0 && !IsAvatarFloor(f) && RollsTwo(f, runSeed)) spent = true;
            int step = !spent && RollsTwo(floor, runSeed) ? 2 : 1;
            return System.Math.Min(Tuning.EnemyBands.ShortBossCapBelowTop, band + step);
        }

        static bool RollsTwo(int floor, int runSeed) => Combat.FireArt.Hash(runSeed, floor, 53) < Tuning.EnemyBands.BossBandTwoChance;

        /// <summary>
        /// The ENEMY BAND a floor is in, 0-4 (Tuning.EnemyBands): one more after every mini-boss of
        /// the Short Descent (10 / 20 / 30 / 40), Classic at the same depth (20 / 40 / 60 / 80).
        /// Structure, so it reads the FLOOR. Enemy behaviours that arrive with depth ask this, never
        /// the floor.
        /// </summary>
        public static int EnemyBand(int floor)
        {
            var edges = Short ? Tuning.EnemyBands.ShortEdges : Tuning.EnemyBands.ClassicEdges;
            int band = 0;
            foreach (int e in edges) if (floor > e) band++;
            return band;
        }

        /// <summary>An avatar's floor - its own boss, and (below the final floor) its own design boxes.</summary>
        public static bool IsAvatarFloor(int floor)
            => Short ? floor == 25 || floor == 50
                     : floor == 25 || floor == 50 || floor == 75 || floor == 100;
    }
}
