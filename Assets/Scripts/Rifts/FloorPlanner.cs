using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Convergence.Core;

namespace Convergence.Rifts
{
    public enum FloorCategory { Combat, Rift, Puzzle, Boss }

    /// <summary>Which kind of exit a floor carries.</summary>
    public enum RiftKind { None, Blue, Red, Collapsing }

    public readonly struct FloorPlan
    {
        public readonly int Floor;
        public readonly FloorCategory Category;
        public readonly RiftKind Rift;
        /// <summary>The dry-spell limit put this Rift here rather than the draw.</summary>
        public readonly bool Forced;

        public FloorPlan(int floor, FloorCategory category, RiftKind rift, bool forced)
        { Floor = floor; Category = category; Rift = rift; Forced = forced; }

        public override string ToString()
            => $"floor {Floor}: {Category}" + (Rift != RiftKind.None ? $" + {Rift} Rift" : "")
             + (Forced ? " (forced)" : "");
    }

    /// <summary>
    /// Decides what each floor IS. One per run; see Tuning.Floors for the weights and why they
    /// grow.
    ///
    ///     avatar floors 25/50/75   a boss, and a GUARANTEED Blue Rift once it falls
    ///     floor 100                the last avatar - clearing it ends the run, no Rift needed
    ///     every tenth floor        a boss; may still roll a Blue Rift, never Collapsing or Red
    ///     everything else          drawn by weight: combat, Rift (Blue / Collapsing / Red), puzzle
    ///                              (from PuzzleMinFloor)
    ///
    /// THE DRAW IS SEEDED, never live randomness: each floor's roll comes from (run seed, floor),
    /// so restarting cannot reroll a run's floors. The counters it reads DO depend on play (a
    /// Collapsing Rift only resets the Rift count if it was reached), which is fine - the same
    /// state on the same floor always draws the same answer. Seed is a local stand-in for the
    /// server's sticky run seed.
    ///
    /// [Serializable] with plain fields only, so a domain reload mid-run restores it (see "Domain
    /// reload traps" in CLAUDE.md).
    /// </summary>
    [Serializable]
    public class FloorPlanner
    {
        public int Seed;
        /// <summary>Rift-eligible floors since an exit was last REACHED.</summary>
        public int SinceRift;
        public int SincePuzzle;
        /// <summary>Floors left in the quiet stretch after a Collapsing Rift ran out - no Rift is
        /// drawn while this is above zero. See Tuning.Floors.QuietAfterCollapseMin.</summary>
        public int QuietFloors;
        /// <summary>The player's clear times this run, each as a ratio to that floor's estimate.</summary>
        public List<float> PaceRatios = new();

        public FloorPlanner(int seed) { Seed = seed; }

        /// <summary>
        /// PLAY-TESTING ONLY. Set either and the NEXT non-boss floor is that, once:
        ///
        ///     unity command eval 'Convergence.Rifts.FloorPlanner.DevForceCategory = Convergence.Rifts.FloorCategory.Puzzle; Convergence.Rifts.FloorPlanner.DevForcePuzzle = 2; return 0;'
        ///     unity command eval 'Convergence.Rifts.FloorPlanner.DevForceRift = Convergence.Rifts.RiftKind.Red; return 0;'
        ///
        /// DevForcePuzzle is a PuzzleKind as an int (0 Echo, 1 Elements, 2 Lights), -1 for the draw.
        /// Statics, so a domain reload clears them - which is the right answer for a one-shot.
        /// </summary>
        public static FloorCategory? DevForceCategory;
        public static RiftKind DevForceRift;
        public static int DevForcePuzzle = -1;

        public static bool IsAvatarFloor(int floor)
            => floor == 25 || floor == 50 || floor == 75 || floor == 100;

        public static bool IsBossFloor(int floor)
            => floor > 0 && (IsAvatarFloor(floor) || floor % Tuning.Boss.RiftInterval == 0);

        /// <summary>
        /// What <see cref="Plan"/> WILL say for <paramref name="floor"/>, without saying it: drawn
        /// on a copy, so no counter moves and no dev-force switch is spent. Plan is called once
        /// per floor and moves the counters it reads, so asking it ahead of time would plan the
        /// floor twice. Read once the current floor's Rift has settled - a Rift reached or
        /// collapsed after this changes the answer.
        /// </summary>
        public FloorPlan Peek(int floor)
        {
            var copy = new FloorPlanner(Seed)
            {
                SinceRift = SinceRift, SincePuzzle = SincePuzzle, QuietFloors = QuietFloors,
                LastPuzzle = LastPuzzle,
            };
            return copy.Plan(floor, consumeDevForce: false);
        }

        /// <summary>
        /// The next <paramref name="count"/> floors after <paramref name="from"/>, planned in order
        /// on ONE copy - so each sees the counters the floors before it left. Nothing on the real
        /// planner moves. What happens AT a Rift (reached, collapsed) is play, not plan: the copy
        /// assumes each one reached, so a plan past the first Rift is a forecast that the real
        /// floors may revise - fine for "where is the next Rift", which is all this is asked.
        /// </summary>
        public List<FloorPlan> PeekAhead(int from, int count)
        {
            var copy = new FloorPlanner(Seed)
            {
                SinceRift = SinceRift, SincePuzzle = SincePuzzle, QuietFloors = QuietFloors,
                LastPuzzle = LastPuzzle,
            };
            var plans = new List<FloorPlan>();
            for (int f = from + 1; f <= Math.Min(100, from + count); f++)
            {
                var plan = copy.Plan(f, consumeDevForce: false);
                if (plan.Rift != RiftKind.None) copy.MarkRiftReached();
                plans.Add(plan);
            }
            return plans;
        }

        public FloorPlan Plan(int floor) => Plan(floor, consumeDevForce: true);

        FloorPlan Plan(int floor, bool consumeDevForce)
        {
            // The avatar's exit is GUARANTEED, quiet stretch or not - beating one always earns its
            // way out. It still counts as a floor passed for the quiet stretch.
            if (IsAvatarFloor(floor))
            {
                if (QuietFloors > 0) QuietFloors--;
                return new FloorPlan(floor, FloorCategory.Boss,
                                     floor < 100 ? RiftKind.Blue : RiftKind.None, false);
            }

            bool boss = IsBossFloor(floor);

            // Play-testing: one floor of the asked-for kind, then back to the draw.
            if (!boss && (DevForceCategory != null || DevForceRift != RiftKind.None))
            {
                var cat = DevForceCategory ?? FloorCategory.Rift;
                var kind = DevForceRift;
                if (consumeDevForce)
                {
                    DevForceCategory = null;
                    DevForceRift = RiftKind.None;
                }
                return new FloorPlan(floor, cat, cat == FloorCategory.Puzzle ? RiftKind.None : kind, false);
            }

            var rng = new Random(unchecked(Seed * 486187739 + floor * 16777619));
            bool quiet = QuietFloors > 0;
            if (quiet) QuietFloors--;
            bool pastMin = floor >= Tuning.Floors.RiftMinFloor;
            bool riftEligible = pastMin && !quiet;

            // The dry-spell limit. Blue, always: a forced exit the player can fail is no limit.
            if (riftEligible && SinceRift >= Tuning.Floors.MaxRiftGap)
            {
                SinceRift++;
                if (!boss) SincePuzzle++;
                return new FloorPlan(floor, boss ? FloorCategory.Boss : FloorCategory.Combat,
                                     RiftKind.Blue, true);
            }

            float wCombat = Tuning.Floors.CombatWeight;
            float wRift = riftEligible ? Tuning.Floors.RiftBase + Tuning.Floors.RiftStep * SinceRift : 0f;
            float wPuzzle = boss || floor < Tuning.Floors.PuzzleMinFloor ? 0f
                          : Tuning.Floors.PuzzleBase + Tuning.Floors.PuzzleStep * SincePuzzle;
            double roll = rng.NextDouble() * (wCombat + wRift + wPuzzle);

            // Counted through a quiet stretch too, so the odds have built up by the time it ends.
            if (pastMin) SinceRift++;
            if (!boss) SincePuzzle++;

            if (roll < wRift)
                return new FloorPlan(floor, boss ? FloorCategory.Boss : FloorCategory.Rift,
                                     PickRift(floor, boss, rng), false);
            if (roll < wRift + wPuzzle)
            {
                SincePuzzle = 0;
                return new FloorPlan(floor, FloorCategory.Puzzle, RiftKind.None, false);
            }
            return new FloorPlan(floor, boss ? FloorCategory.Boss : FloorCategory.Combat, RiftKind.None, false);
        }

        static RiftKind PickRift(int floor, bool boss, Random rng)
        {
            float blue = Tuning.Floors.BlueShare;
            // Never on a boss floor: a guard pack between the player and the boss is exactly what
            // "a boss floor is the boss and nothing else" rules out.
            float red = boss ? 0f : Tuning.Floors.RedShare;
            // A boss fight has no "clear it in time" worth asking, and the first floors have no
            // pace yet to fit a clock to.
            float collapsing = !boss && floor >= Tuning.Floors.CollapsingMinFloor
                ? Tuning.Floors.CollapsingShare : 0f;
            double r = rng.NextDouble() * (blue + red + collapsing);
            if (r < collapsing) return RiftKind.Collapsing;
            if (r < collapsing + red) return RiftKind.Red;
            return RiftKind.Blue;
        }

        /// <summary>Which puzzle a puzzle floor holds, and the seed it builds from - both from the
        /// run seed, so a restart meets the same puzzle. Never the same kind twice running.</summary>
        public Puzzles.PuzzleKind PickPuzzle(int floor)
        {
            var rng = new Random(unchecked(Seed * 40503 + floor * 2654435 + 7));
            int n = Enum.GetValues(typeof(Puzzles.PuzzleKind)).Length;
            int k = rng.Next(n);
            if (k == LastPuzzle) k = (k + 1 + rng.Next(n - 1)) % n;
            if (DevForcePuzzle >= 0) { k = DevForcePuzzle; DevForcePuzzle = -1; }
            LastPuzzle = k;
            return (Puzzles.PuzzleKind)k;
        }
        public int LastPuzzle = -1;

        public int PuzzleSeed(int floor) => unchecked(Seed * 31337 + floor * 7919 + 11);

        /// <summary>Seeds Enemies.WaveComposer, so a restart on the sticky seed meets the same
        /// rooms - and so a boon can show the next floor's roster before the player gets there.</summary>
        public int WaveSeed(int floor) => unchecked(Seed * 7907 + floor * 104729 + 3);

        /// <summary>The seed of the deal offered after <paramref name="floor"/>'s clear - so a
        /// restart meets the same deals, and Oracle can show the next one truthfully.</summary>
        public int DealSeed(int floor) => unchecked(Seed * 3557 + floor * 69313 + 13);

        /// <summary>Seeds the floor's room shape (Hazards.RoomShape.For) - the same room on a
        /// restart, and a shape a preview can name without building it.</summary>
        public int ShapeSeed(int floor) => unchecked(Seed * 6151 + floor * 92821 + 5);

        /// <summary>The room shape <paramref name="floor"/> will have if it is a combat floor -
        /// null for the open rectangle. Pure: it moves no counter and spends no dev switch unless
        /// one is set.</summary>
        public Hazards.RoomShape ShapeFor(int floor)
            => Hazards.RoomShape.For(floor, ShapeSeed(floor), ShapeSeed(floor - 1));

        /// <summary>
        /// PLAY-TESTING ONLY. The next boss floor holds this boss, once - pair it with a jump to a
        /// boss floor:
        ///
        ///     unity command eval 'Convergence.Rifts.FloorPlanner.DevForceBoss = Convergence.Bosses.BossKind.Medusa; return 0;'
        /// </summary>
        public static Bosses.BossKind? DevForceBoss;

        /// <summary>
        /// The mini-bosses a run draws from. LARGER THAN IT LOOKS on purpose: the pool is meant to
        /// outgrow the eight mini-boss floors (10..90 less the avatars), so each run meets a
        /// different set. The Cantor stays in it until there are enough to replace it.
        /// </summary>
        static readonly Bosses.BossKind[] MiniBossPool = { Bosses.BossKind.Cantor, Bosses.BossKind.Medusa };

        /// <summary>
        /// Which boss holds <paramref name="floor"/>. The avatars (25/50/75/100) are still all the
        /// Cantor - they are meant to be four set-pieces of their own. Mini-boss floors draw from
        /// <see cref="MiniBossPool"/> as a SHUFFLE BAG seeded by the run: every boss comes up
        /// before any repeats, the order differs per run, and a restart meets the same bosses.
        /// </summary>
        public Bosses.BossKind BossFor(int floor)
        {
            if (DevForceBoss is Bosses.BossKind forced) { DevForceBoss = null; return forced; }
            if (IsAvatarFloor(floor)) return Bosses.BossKind.Cantor;

            // This floor's place among the mini-boss floors: 10 is the first, 20 the second...
            int slot = 0;
            for (int f = Tuning.Boss.RiftInterval; f < floor; f += Tuning.Boss.RiftInterval)
                if (!IsAvatarFloor(f)) slot++;

            var rng = new Random(unchecked(Seed * 92821 + 5));
            var bag = new List<Bosses.BossKind>();
            var drawn = new List<Bosses.BossKind>();
            while (drawn.Count <= slot)
            {
                bag.Clear();
                bag.AddRange(MiniBossPool);
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (bag[i], bag[j]) = (bag[j], bag[i]);
                }
                // No boss twice running across the seam between two bags.
                if (drawn.Count > 0 && bag.Count > 1 && bag[0] == drawn[^1])
                    (bag[0], bag[1]) = (bag[1], bag[0]);
                drawn.AddRange(bag);
            }
            return drawn[slot];
        }

        /// <summary>An exit was actually available: any Blue, a Collapsing Rift that stabilised,
        /// the avatar's guaranteed Rift. A Collapsing Rift that ran out does NOT call this.</summary>
        public void MarkRiftReached() => SinceRift = 0;

        /// <summary>A Collapsing Rift on <paramref name="floor"/> ran out. Opens the quiet
        /// stretch - its length drawn from the seed, so it can't be learned to the floor.</summary>
        public void MarkRiftCollapsed(int floor)
        {
            var rng = new Random(unchecked(Seed * 92821 + floor * 7349 + 3));
            QuietFloors = rng.Next(Tuning.Floors.QuietAfterCollapseMin, Tuning.Floors.QuietAfterCollapseMax + 1);
        }

        // ------------------------------------------------------------------ the Collapsing clock

        /// <summary>What a floor "should" take: the seconds of damage its wave was priced at
        /// (Enemies.WaveComposer), plus the walking-in and chasing around them. Only ever used as
        /// a yardstick - the clock is fitted to the player.</summary>
        public static float EstimateSeconds(int floor)
            => Enemies.WaveComposer.TargetSeconds(floor) * Tuning.CollapsingRift.ClearPerDamageSecond;

        /// <summary>An ordinary floor's clear, from first wave to last body. Kept as a ratio to the
        /// estimate so pace measured on small early floors carries over to bigger later ones.</summary>
        public void RecordClear(int floor, float seconds)
        {
            float est = EstimateSeconds(floor);
            if (est <= 0f || seconds <= 0f) return;
            PaceRatios.Add(seconds / est);
            if (PaceRatios.Count > 12) PaceRatios.RemoveAt(0);
        }

        /// <summary>The Collapsing Rift's countdown on <paramref name="floor"/>. See
        /// Tuning.CollapsingRift.</summary>
        public float CollapseSeconds(int floor)
        {
            float est = EstimateSeconds(floor);
            float k = 1f;
            int n = Tuning.CollapsingRift.PaceSamples;
            if (PaceRatios.Count >= n)
            {
                var recent = PaceRatios.Skip(PaceRatios.Count - n).OrderBy(x => x).ToList();
                float median = recent[recent.Count / 2];
                k = Math.Clamp(median * Tuning.CollapsingRift.Margin,
                               Tuning.CollapsingRift.BandLow, Tuning.CollapsingRift.BandHigh);
            }
            return Math.Max(Tuning.CollapsingRift.MinSeconds, est * k);
        }

        // ------------------------------------------------------------------ simulation

        /// <summary>
        /// Plays the planner over <paramref name="runs"/> full 100-floor runs and reports what the
        /// weights produce. <paramref name="collapsingSuccess"/> is the assumed share of
        /// Collapsing Rifts the player beats. From the CLI:
        ///
        ///     unity command eval 'return Convergence.Rifts.FloorPlanner.Simulate(20000, 0.7f);'
        /// </summary>
        public static string Simulate(int runs, float collapsingSuccess)
        {
            var counts = new Dictionary<string, int>();
            var gaps = new List<int>();
            int eligible = 0, baseStakeExits = 0, forced = 0, exitsTotal = 0, collapsesFailed = 0, quietBroken = 0;
            var rng = new Random(12345);

            for (int run = 0; run < runs; run++)
            {
                var p = new FloorPlanner(run * 7919 + 1);
                int lastExit = 0, lastCollapse = 0;
                bool baseStakePaid = false;
                for (int floor = 1; floor <= 100; floor++)
                {
                    var plan = p.Plan(floor);
                    if (!IsAvatarFloor(floor) && floor >= Tuning.Floors.RiftMinFloor)
                    {
                        eligible++;
                        string key = plan.Rift != RiftKind.None ? $"rift:{plan.Rift}" : plan.Category.ToString();
                        counts[key] = counts.TryGetValue(key, out var c) ? c + 1 : 1;
                    }
                    if (plan.Forced) forced++;

                    bool reached = plan.Rift == RiftKind.Blue || plan.Rift == RiftKind.Red
                                || (plan.Rift == RiftKind.Collapsing && rng.NextDouble() < collapsingSuccess);
                    if (plan.Rift == RiftKind.Collapsing && !reached)
                    {
                        p.MarkRiftCollapsed(floor);
                        collapsesFailed++;
                        lastCollapse = floor;
                    }
                    if (reached && !IsAvatarFloor(floor) && lastCollapse > 0
                        && floor - lastCollapse <= Tuning.Floors.QuietAfterCollapseMin)
                        quietBroken++;
                    if (!reached) continue;

                    p.MarkRiftReached();
                    exitsTotal++;
                    gaps.Add(floor - lastExit);
                    lastExit = floor;
                    if (floor >= Tuning.Stake.GateBase && floor < 25) baseStakePaid = true;
                }
                if (baseStakePaid) baseStakeExits++;
            }

            gaps.Sort();
            var sb = new StringBuilder();
            sb.Append($"{runs} runs, Collapsing beaten {collapsingSuccess:P0}\n");
            sb.Append($"floor mix (floors {Tuning.Floors.RiftMinFloor}-99, avatars excluded):\n");
            foreach (var kv in counts.OrderByDescending(kv => kv.Value))
                sb.Append($"  {kv.Key,-16} {kv.Value / (float)eligible:P1}\n");
            sb.Append($"exits reached per run   {exitsTotal / (float)runs:F1}  " +
                      $"(forced by the gap limit {forced / (float)runs:F2}/run)\n");
            sb.Append($"floors between exits    mean {gaps.Average():F1}  median {gaps[gaps.Count / 2]}  " +
                      $"p90 {gaps[(int)(gaps.Count * 0.9)]}  max {gaps[^1]}\n");
            sb.Append($"failed collapses per run {collapsesFailed / (float)runs:F2}, " +
                      $"followed by a non-avatar exit inside the quiet stretch: {quietBroken} (should be 0)\n");
            sb.Append($"base stake finds an exit on floors {Tuning.Stake.GateBase}-24   " +
                      $"{baseStakeExits / (float)runs:P1}");
            return sb.ToString();
        }
    }
}
