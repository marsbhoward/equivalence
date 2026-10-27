using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Convergence.Core;
using Convergence.Player;

namespace Convergence.Enemies
{
    /// <summary>One body in a composed wave.</summary>
    public readonly struct WaveSpawn
    {
        public readonly EnemyKind Kind;
        public readonly bool Elite;
        /// <summary>The designed group it arrives with (Tuning.WaveGroups), or None.</summary>
        public readonly WaveGroupId Group;
        /// <summary>An elite's modifier (Tuning.EliteAffixes), or None.</summary>
        public readonly EliteAffix Affix;
        public WaveSpawn(EnemyKind kind, bool elite, WaveGroupId group = WaveGroupId.None,
                         EliteAffix affix = EliteAffix.None)
        { Kind = kind; Elite = elite; Group = group; Affix = affix; }
    }

    /// <summary>A floor's whole wave, decided before anything spawns.</summary>
    public class Wave
    {
        public int Floor;

        /// <summary>Effective HP the floor was priced at.</summary>
        public float Pool;

        /// <summary>What the bodies COST against the pool (support kinds at their minimum).</summary>
        public float Spent;

        /// <summary>The bodies' real effective HP - what the player actually has to chew through.</summary>
        public float TotalEhp;

        /// <summary>In spawn order. The floor's guaranteed elite, if any, is last.</summary>
        public readonly List<WaveSpawn> Spawns = new();

        /// <summary>Whether any body of this kind is in the wave.</summary>
        public bool Fields(EnemyKind kind)
        {
            foreach (var s in Spawns) if (s.Kind == kind) return true;
            return false;
        }

        public int Elites => Spawns.Count(s => s.Elite);

        /// <summary>The designed group drawn, or None (code only - never shown).</summary>
        public WaveGroupId Group;

        /// <summary>"7 Chaser · 3 Mortar (1 elite) · 2 Turret" - what a preview boon shows.</summary>
        public string Roster()
            => string.Join(" · ", Spawns.GroupBy(s => s.Kind)
                .OrderByDescending(g => g.Count())
                .Select(g =>
                {
                    int e = g.Count(s => s.Elite);
                    string name = EnemyTypes.Of(g.Key).Name;
                    return e > 0 ? $"{g.Count()} {name} ({e} elite)" : $"{g.Count()} {name}";
                }));
    }

    /// <summary>
    /// A floor's enemies as a BUDGET rather than a count.
    ///
    /// The floor is a pool of effective HP (health plus armor): the seconds of damage it should
    /// take (Tuning.Waves.TargetSeconds) times the damage the player expected on it deals
    /// (Player.PlayerPower.ExpectedDps). Enemies are bought from that pool at their real effective
    /// HP on that floor, so 20 cheap bodies, 10 armoured ones or 5 elites can all be the same
    /// floor - WHICH is random, HOW MUCH killing it takes is not.
    ///
    /// It replaced a fixed count with each kind subtracted from it in order, which starved every
    /// kind introduced late: Gargoyle and Bubbles never spawned on an ordinary floor at all, Mortar
    /// on floor 4 only, and every floor from about 35 down was 13 Ranged and a Chaser.
    ///
    ///     COST       effective HP, from EnemyFactory's own numbers (never below
    ///                Tuning.Waves.MinCostFraction of a Chaser). How LONG a body takes.
    ///     PRESSURE   hand-set per kind. How DANGEROUS a body is while alive - what the
    ///                on-screen cap counts, so a swarm fits a dozen and elites come in threes.
    ///
    /// Pure and seeded (FloorPlanner.WaveSeed): a restart on the sticky seed meets the same rooms,
    /// and the next floor's roster can be known before the player gets there.
    ///
    /// From the CLI:
    ///
    ///     unity command eval 'return Convergence.Enemies.WaveComposer.Simulate(2000);'
    /// </summary>
    public static class WaveComposer
    {
        /// <summary>Every kind bought from the pool. Booster is not: it is gated on the account
        /// and added on top of the draw, at most one, as it always was.</summary>
        static readonly EnemyKind[] Pooled =
        {
            EnemyKind.Chaser, EnemyKind.Bomb, EnemyKind.Ranged, EnemyKind.Dasher, EnemyKind.Turret,
            EnemyKind.Mortar, EnemyKind.Gargoyle, EnemyKind.Bubbles,
        };

        /// <summary>
        /// Which archetypes may carry the elite tier. Booster is among them since 2026-10-19 (its
        /// elite roars two allies to power), but it is never DRAWN as one - it is not pooled; its
        /// own floor roll decides its tier (Tuning.Enemy.BoosterEliteChance).
        ///
        /// Turret was left out too, once, because the tier only doubled the HP of a thing that
        /// already holds an angle across the room - a chore rather than a threat. It is in now
        /// because the tier grants it a MOVE: alongside Overcharge it lobs a mire shell over any
        /// cover, landing as slowing ground that holds the player where the beam can reach
        /// (EnemyController.TickMire, Hazards.MireField).
        ///
        /// Gargoyle joins despite its BASIC form being stationary, because the elite tier is what
        /// makes it move at all (see ElitePattern.Descent).
        ///
        /// Bubbles joins too: its elite grants a more resilient bubble (BubblesEliteCharges) - a
        /// real difference, just read straight off the Elite bool rather than declared as an
        /// ElitePattern (see Tuning.Enemy's own note on why). The Turret's mire is read the same way.
        /// </summary>
        public static readonly EnemyKind[] EliteKinds =
        {
            EnemyKind.Chaser, EnemyKind.Bomb, EnemyKind.Ranged, EnemyKind.Dasher,
            EnemyKind.Turret, EnemyKind.Gargoyle, EnemyKind.Bubbles, EnemyKind.Mortar, EnemyKind.Booster,
        };

        public static int UnlockFloor(EnemyKind kind) => kind switch
        {
            EnemyKind.Chaser   => 1,
            EnemyKind.Bomb     => Tuning.Waves.BombFromFloor,
            EnemyKind.Ranged   => Tuning.Waves.RangedFromFloor,
            EnemyKind.Dasher   => Tuning.Waves.DasherFromFloor,
            EnemyKind.Turret   => Tuning.Waves.TurretFromFloor,
            EnemyKind.Mortar   => Tuning.Enemy.MortarFromFloor,
            EnemyKind.Gargoyle => Tuning.Waves.GargoyleFromFloor,
            EnemyKind.Bubbles  => Tuning.Enemy.BubblesFromFloor,
            _                  => int.MaxValue,
        };

        /// <summary>
        /// The floor a kind first appears on IN THE RUN BEING PLAYED: the Short Descent introduces
        /// by FLOOR (Tuning.Waves.Short*FromFloor - attackers in the first band, the kinds that help
        /// others in the second); Classic by depth (<see cref="UnlockFloor"/>).
        /// </summary>
        public static int UnlockAt(EnemyKind kind)
        {
            if (!Core.RunFormat.Short) return UnlockFloor(kind);
            return kind switch
            {
                EnemyKind.Chaser   => 1,
                EnemyKind.Ranged   => Tuning.Waves.ShortRangedFromFloor,
                EnemyKind.Bomb     => Tuning.Waves.ShortBombFromFloor,
                EnemyKind.Dasher   => Tuning.Waves.ShortDasherFromFloor,
                EnemyKind.Mortar   => Tuning.Waves.ShortMortarFromFloor,
                EnemyKind.Turret   => Tuning.Waves.ShortTurretFromFloor,
                EnemyKind.Gargoyle => Tuning.Waves.ShortGargoyleFromFloor,
                EnemyKind.Bubbles  => Tuning.Waves.ShortBubblesFromFloor,
                _                  => int.MaxValue,
            };
        }

        /// <summary>How far into its introductions the run is on <paramref name="floor"/>, in the
        /// units <see cref="UnlockAt"/> counts: the floor in the Short Descent, the depth in Classic.</summary>
        static int Progress(int floor) => Core.RunFormat.Short ? floor : Core.RunFormat.Depth(floor);
        static int ProgressBefore(int floor) => Core.RunFormat.Short ? floor - 1 : Core.RunFormat.DepthBefore(floor);

        /// <summary>Whether <paramref name="kind"/> has been introduced by <paramref name="floor"/> -
        /// for anything outside the wave that picks a kind (a Red Rift's guards). The Green Lion,
        /// never part of the pool, counts as introduced from its Short window.</summary>
        public static bool Unlocked(EnemyKind kind, int floor)
        {
            if (kind == EnemyKind.Booster)
                return !Core.RunFormat.Short || floor >= Tuning.Waves.ShortBoosterFromFloor;
            return UnlockAt(kind) <= Progress(floor);
        }

        static bool CanBeElite(EnemyKind kind) => Array.IndexOf(EliteKinds, kind) >= 0;

        // ------------------------------------------------------------------ prices

        /// <summary>Health plus armor, exactly as EnemyFactory would build this spawn.</summary>
        public static float Ehp(EnemyKind kind, bool elite, int floor)
        {
            var def = EnemyTypes.Of(kind);
            float hp = EnemyFactory.MaxHpFor(def, floor, elite);
            return hp + EnemyFactory.ArmorFor(def, hp, floor, elite);
        }

        public static float Cost(EnemyKind kind, bool elite, int floor)
            => Mathf.Max(Ehp(kind, elite, floor),
                         Tuning.Waves.MinCostFraction * Ehp(EnemyKind.Chaser, false, floor));

        /// <summary>A spawn's pressure, its elite modifier included.</summary>
        public static float Pressure(EnemyKind kind, bool elite, EliteAffix affix)
            => Pressure(kind, elite) * (affix != EliteAffix.None ? Tuning.EliteAffixes.PressureMul : 1f);

        public static float Pressure(EnemyKind kind, bool elite)
        {
            float p = kind switch
            {
                EnemyKind.Chaser   => Tuning.Waves.PressureChaser,
                EnemyKind.Bomb     => Tuning.Waves.PressureBomb,
                EnemyKind.Ranged   => Tuning.Waves.PressureRanged,
                EnemyKind.Turret   => Tuning.Waves.PressureTurret,
                EnemyKind.Dasher   => Tuning.Waves.PressureDasher,
                EnemyKind.Gargoyle => Tuning.Waves.PressureGargoyle,
                EnemyKind.Booster  => Tuning.Waves.PressureBooster,
                EnemyKind.Bubbles  => Tuning.Waves.PressureBubbles,
                EnemyKind.Mortar   => Tuning.Waves.PressureMortar,
                _                  => 1f,
            };
            return elite ? p * Tuning.Waves.ElitePressureMul : p;
        }

        public static float LivePressureCap(int floor)
            => Mathf.Min(Tuning.Waves.LivePressureBase + Core.RunFormat.Depth(floor) * Tuning.Waves.LivePressurePerFloor,
                         Tuning.Waves.LivePressureMax);

        /// <summary>
        /// Whether <paramref name="next"/> may come on screen now. The first body always may -
        /// an elite costing more than the whole cap still arrives, alone.
        /// </summary>
        public static bool Fits(WaveSpawn next, float livePressure, int liveBodies, int floor)
            => liveBodies == 0
            || (liveBodies < Tuning.Waves.MaxLiveBodies
                && livePressure + Pressure(next.Kind, next.Elite, next.Affix) <= LivePressureCap(floor) + 1e-4f);

        /// <summary>Seconds of damage the floor should take the expected player.</summary>
        public static float TargetSeconds(int floor)
        {
            // The format's own table, by FLOOR: a Short floor stays short while its depth doubles.
            var f = Core.RunFormat.Short ? Tuning.Waves.ShortTargetFloors : Tuning.Waves.TargetFloors;
            var s = Core.RunFormat.Short ? Tuning.Waves.ShortTargetSeconds : Tuning.Waves.TargetSeconds;
            if (floor <= f[0]) return s[0];
            for (int i = 1; i < f.Length; i++)
                if (floor <= f[i])
                    return Mathf.Lerp(s[i - 1], s[i], Mathf.InverseLerp(f[i - 1], f[i], floor));
            return s[^1];
        }

        /// <summary>The floor's effective-HP pool.</summary>
        /// <remarks>The expected player is the one at this floor's DEPTH (Core.RunFormat) - PlayerPower's
        /// curves are Classic floors.</remarks>
        public static float Pool(int floor) => TargetSeconds(floor) * PlayerPower.ExpectedDps(Core.RunFormat.Depth(floor));

        static float EliteShareCap(int floor)
            => Mathf.Min(Tuning.Waves.EliteShareBase + Core.RunFormat.Depth(floor) * Tuning.Waves.EliteSharePerFloor,
                         Tuning.Waves.EliteShareMax);

        static int EliteCap(int floor)
            => Core.RunFormat.Depth(floor) <= Tuning.Waves.EliteCapEarlyThrough ? Tuning.Waves.EliteCapEarly
             : Core.RunFormat.Depth(floor) <= Tuning.Waves.EliteCapMidThrough ? Tuning.Waves.EliteCapMid
             : int.MaxValue;

        // ------------------------------------------------------------------ composing

        /// <param name="eliteFloor">Every third floor guarantees one elite, paid from the pool.</param>
        /// <param name="boosterEligible">The account has earned the Booster curveball (see
        /// Tuning.Enemy.BoosterUnlockBestFloor) and the floor is shallow enough for it.</param>
        public static Wave Compose(int floor, int seed, bool eliteFloor, bool boosterEligible)
        {
            var rng = new System.Random(seed);
            var wave = new Wave { Floor = floor, Pool = Pool(floor) };

            // Priced once per floor - the draw below asks for these on every pick.
            int kindCount = Enum.GetValues(typeof(EnemyKind)).Length;
            var cost = new float[kindCount, 2];
            var ehp = new float[kindCount, 2];
            for (int k = 0; k < kindCount; k++)
                for (int e = 0; e < 2; e++)
                {
                    cost[k, e] = Cost((EnemyKind)k, e == 1, floor);
                    ehp[k, e] = Ehp((EnemyKind)k, e == 1, floor);
                }
            float CostOf(EnemyKind kind, bool elite) => cost[(int)kind, elite ? 1 : 0];
            var drawn = new List<WaveSpawn>();
            int elites = 0, eliteCap = EliteCap(floor);

            void Buy(List<WaveSpawn> into, EnemyKind kind, bool elite)
            {
                into.Add(new WaveSpawn(kind, elite));
                wave.Spent += CostOf(kind, elite);
                wave.TotalEhp += ehp[(int)kind, elite ? 1 : 0];
                if (elite) elites++;
            }

            // The Booster: a floor-level chance, never more than one. Its TIER comes from a draw of
            // its own seed, so adding the elite moved nothing else in any seeded wave.
            if (boosterEligible && rng.NextDouble() < Tuning.Enemy.BoosterFloorChance)
                Buy(drawn, EnemyKind.Booster,
                    new System.Random(seed ^ 0x6B00573).NextDouble() < Tuning.Enemy.BoosterEliteChance);

            // A kind's DEBUT is exactly one body, so the floor that introduces it teaches it.
            // Kinds there from the start (the Chaser) have nothing to be introduced against and are
            // drawn normally.
            // Unlocks are counted by UnlockAt/Progress: FLOORS in the Short Descent, DEPTHS in Classic
            // (a Classic-unlocked kind debuts on the floor whose depth first reaches its unlock).
            int depth = Progress(floor), depthBefore = ProgressBefore(floor);
            foreach (var kind in Pooled)
                if (UnlockAt(kind) > depthBefore && UnlockAt(kind) <= depth && UnlockAt(kind) > 1)
                    Buy(drawn, kind, false);

            // A DESIGNED GROUP (Tuning.WaveGroups): drawn from a stream of its own, so a floor that
            // draws none is exactly the wave it always was. Never on a floor that debuts a kind - that
            // floor teaches one thing - and only groups whose every kind the run has met.
            var group = new List<WaveSpawn>();
            bool debutFloor = Pooled.Any(k => UnlockAt(k) > depthBefore && UnlockAt(k) <= depth && UnlockAt(k) > 1);
            if (!debutFloor && Core.RunFormat.EnemyBand(floor) >= Tuning.WaveGroups.FromBand)
            {
                var grng = new System.Random(seed ^ 0x47524F55);
                if (grng.NextDouble() < Tuning.WaveGroups.Chance)
                {
                    var fits = new List<WaveGroupId>();
                    for (int g = 1; g < WaveGroups.Count; g++)
                    {
                        var id = (WaveGroupId)g;
                        var members = WaveGroups.Of(id);
                        if (members.Length == 0 || members.Any(m => UnlockAt(m.Kind) > depth)) continue;
                        float price = 0f;
                        int e = elites;
                        foreach (var m in members)
                        {
                            bool el = m.Elite && e < eliteCap;
                            if (el) e++;
                            price += CostOf(m.Kind, el);
                        }
                        if (price <= wave.Pool * Tuning.WaveGroups.MaxPoolShare && price <= wave.Pool - wave.Spent)
                            fits.Add(id);
                    }
                    if (fits.Count > 0)
                    {
                        var id = fits[grng.Next(fits.Count)];
                        wave.Group = id;
                        foreach (var m in WaveGroups.Of(id))
                        {
                            var bought = new List<WaveSpawn>();
                            Buy(bought, m.Kind, m.Elite && elites < eliteCap);
                            group.Add(new WaveSpawn(bought[0].Kind, bought[0].Elite, id));
                        }
                    }
                }
            }

            // A group's elite stands in for it (the group was drawn first).
            // The guaranteed elite is paid for FIRST, so the pool knows about it, but queued
            // LAST, so it trickles in late as it always has. Chosen among kinds that leave at
            // least half the pool for its company where any do - on an early floor a lone elite
            // Chaser would otherwise be the whole floor.
            WaveSpawn? guaranteed = null;
            if (eliteFloor && elites < eliteCap && !group.Any(g => g.Elite))
            {
                var open = EliteKinds.Where(k => UnlockAt(k) <= depth).ToList();
                var light = open.Where(k => CostOf(k, true) <= wave.Pool * 0.5f - wave.Spent).ToList();
                var from = light.Count > 0 ? light : open;
                var kind = from.Count > 0 ? from[rng.Next(from.Count)] : EnemyKind.Chaser;
                var tail = new List<WaveSpawn>();
                Buy(tail, kind, true);
                guaranteed = tail[0];
            }

            // This floor's lean: every eligible kind's weight scaled by a log-normal draw, a kind
            // fresh off its debut ramping up from DebutWeight.
            var weights = new Dictionary<EnemyKind, float>();
            foreach (var kind in Pooled)
            {
                int since = depth - UnlockAt(kind);
                // Not yet unlocked, or debuting on this floor (bought alone above).
                if (since < 0 || (UnlockAt(kind) > depthBefore && UnlockAt(kind) > 1)) continue;
                float ramp = floor == 1 || since >= Tuning.Waves.DebutRampFloors
                    ? 1f
                    : Mathf.Lerp(Tuning.Waves.DebutWeight, 1f, since / (float)Tuning.Waves.DebutRampFloors);
                weights[kind] = ramp * Mathf.Exp(Tuning.Waves.KindWeightSigma * Gaussian(rng));
            }

            double eliteShare = rng.NextDouble() * EliteShareCap(floor);
            var options = new List<(EnemyKind Kind, float Weight)>();

            while (drawn.Count + group.Count + (guaranteed.HasValue ? 1 : 0) < Tuning.Waves.MaxBodies)
            {
                float remaining = wave.Pool - wave.Spent;
                if (remaining <= 0f) break;

                // Running out of bodies before running out of pool: only what is dear enough to
                // spend the rest in the slots left. Otherwise a deep room of cheap kinds hits
                // MaxBodies with a fifth of its pool unspent - a floor that comes up short.
                int slotsLeft = Tuning.Waves.MaxBodies - drawn.Count - group.Count - (guaranteed.HasValue ? 1 : 0);
                float atLeast = remaining / slotsLeft;

                bool elite = elites < eliteCap && rng.NextDouble() < eliteShare;
                Collect(options, weights, cost, remaining, atLeast, elite);
                if (options.Count == 0 && elite)
                {
                    elite = false;
                    Collect(options, weights, cost, remaining, atLeast, false);
                }
                if (options.Count == 0) break;

                float total = 0f;
                foreach (var o in options) total += o.Weight;
                double r = rng.NextDouble() * total;
                var pick = options[^1].Kind;
                foreach (var o in options)
                {
                    r -= o.Weight;
                    if (r <= 0) { pick = o.Kind; break; }
                }
                Buy(drawn, pick, elite);
            }

            // Shuffled, so the debut and the Booster are not always the first thing through.
            for (int i = drawn.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (drawn[i], drawn[j]) = (drawn[j], drawn[i]);
            }
            // The group arrives FIRST, together, ahead of the shuffled rest.
            wave.Spawns.AddRange(group);
            wave.Spawns.AddRange(drawn);
            if (guaranteed.HasValue) wave.Spawns.Add(guaranteed.Value);
            RollAffixes(wave, seed);
            return wave;
        }

        /// <summary>
        /// Each ELITE may carry one modifier (Tuning.EliteAffixes), from a stream of its own so the
        /// draw above is untouched. In spawn order, so a seed always dresses the same bodies.
        /// </summary>
        static void RollAffixes(Wave wave, int seed)
        {
            float chance = WaveGroups.AffixChance(Core.RunFormat.EnemyBand(wave.Floor));
            if (chance <= 0f) return;
            var arng = new System.Random(seed ^ 0x41464658);
            var all = (EliteAffix[])Enum.GetValues(typeof(EliteAffix));
            var fit = new List<EliteAffix>();
            for (int i = 0; i < wave.Spawns.Count; i++)
            {
                var s = wave.Spawns[i];
                if (!s.Elite) continue;
                if (arng.NextDouble() >= chance) continue;
                fit.Clear();
                foreach (var a in all)
                    if (a != EliteAffix.None && WaveGroups.Suits(a, s.Kind)) fit.Add(a);
                if (fit.Count == 0) continue;
                wave.Spawns[i] = new WaveSpawn(s.Kind, s.Elite, s.Group, fit[arng.Next(fit.Count)]);
            }
        }

        /// <summary>
        /// The kinds that may be bought with what is left. A body may overshoot the pool by at
        /// most half its own cost - the last pick rounds to the nearest body rather than always
        /// down, so a floor lands ON its pool on average instead of a fraction short of it.
        /// Kinds cheaper than <paramref name="atLeast"/> are skipped while anything dearer fits.
        /// </summary>
        static void Collect(List<(EnemyKind, float)> into, Dictionary<EnemyKind, float> weights,
                            float[,] cost, float remaining, float atLeast, bool elite)
        {
            into.Clear();
            bool anyDear = false;
            foreach (var kv in weights)
            {
                if (elite && !CanBeElite(kv.Key)) continue;
                float c = cost[(int)kv.Key, elite ? 1 : 0];
                if (c > remaining * 2f) continue;
                if (c >= atLeast && !anyDear) { into.Clear(); anyDear = true; }
                if (c >= atLeast || !anyDear) into.Add((kv.Key, kv.Value));
            }
        }

        static float Gaussian(System.Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        // ------------------------------------------------------------------ simulation

        /// <summary>
        /// Composes <paramref name="runs"/> waves per floor and reports the spread: bodies,
        /// elites, what is on screen as the floor opens, how far the heaviest kind dominates, how
        /// close the bodies land to the pool, and seconds of damage for three players.
        /// </summary>
        public static string Simulate(int runs)
        {
            var sb = new StringBuilder();
            sb.Append($"{runs} waves per floor. pool in basic Chasers of that floor; " +
                      "damage-seconds are the bodies' real EHP over each player's DPS\n");
            sb.Append("floor  pool | bodies p10/50/90 max | elites p50/90 max | opening live p10/50/90 | " +
                      "top kind p90 | spent/pool min-avg | ref/avg  built/avg  built/stack\n");

            var seenOn = new Dictionary<EnemyKind, int>();
            var groupsDrawn = new Dictionary<WaveGroupId, int>();
            var affixes = new Dictionary<EliteAffix, int>();
            int wavesTotal = 0, elitesTotal = 0;
            var report = new HashSet<int> { 1, 2, 3, 4, 5, 6, 8, 10, 15, 20, 25, 30, 40, 50, 60, 75, 80, 100 };

            for (int floor = 1; floor <= Core.RunFormat.FinalFloor; floor++)
            {
                var bodies = new List<int>();
                var elites = new List<int>();
                var live = new List<int>();
                var top = new List<float>();
                var fill = new List<float>();
                float ehp = 0f;
                var kinds = new HashSet<EnemyKind>();

                for (int i = 0; i < runs; i++)
                {
                    var w = Compose(floor, unchecked(i * 7919 + floor * 104729), floor % 3 == 0, false);
                    bodies.Add(w.Spawns.Count);
                    elites.Add(w.Elites);
                    live.Add(OpeningLive(w));
                    top.Add(w.Spawns.GroupBy(s => s.Kind).Max(g => g.Count()) / (float)w.Spawns.Count);
                    fill.Add(w.Spent / w.Pool);
                    ehp += w.TotalEhp;
                    foreach (var s in w.Spawns) kinds.Add(s.Kind);
                    wavesTotal++;
                    if (w.Group != WaveGroupId.None)
                        groupsDrawn[w.Group] = groupsDrawn.TryGetValue(w.Group, out var gn) ? gn + 1 : 1;
                    foreach (var s in w.Spawns)
                    {
                        if (s.Elite) elitesTotal++;
                        if (s.Affix != EliteAffix.None)
                            affixes[s.Affix] = affixes.TryGetValue(s.Affix, out var an) ? an + 1 : 1;
                    }
                }
                foreach (var k in kinds) seenOn[k] = seenOn.TryGetValue(k, out var n) ? n + 1 : 1;
                if (!report.Contains(floor)) continue;

                ehp /= runs;
                float chaser = Ehp(EnemyKind.Chaser, false, floor);
                float r = ehp / PlayerPower.Dps(PowerBuild.Reference, LedgerPlay.Average, Core.RunFormat.Depth(floor));
                float ba = ehp / PlayerPower.Dps(PowerBuild.BuiltThree, LedgerPlay.Average, Core.RunFormat.Depth(floor));
                float bs = ehp / PlayerPower.Dps(PowerBuild.BuiltThree, LedgerPlay.Stacked, Core.RunFormat.Depth(floor));
                sb.Append($"{floor,5} {Pool(floor) / chaser,5:0.0} | " +
                          $"{P(bodies, .1f),3}/{P(bodies, .5f),3}/{P(bodies, .9f),3} {bodies.Max(),3} | " +
                          $"{P(elites, .5f)}/{P(elites, .9f)} {elites.Max(),2} | " +
                          $"{P(live, .1f),2}/{P(live, .5f),2}/{P(live, .9f),2} | " +
                          $"{P(top, .9f),4:0.00} | {fill.Min(),4:0.00}-{fill.Average(),4:0.00} | " +
                          $"{r,5:0}s  {ba,5:0}s  {bs,5:0}s\n");
            }

            sb.Append($"\nfloors (of {Core.RunFormat.FinalFloor}) each kind appears on: ");
            sb.Append(string.Join(", ", seenOn.OrderByDescending(kv => kv.Value)
                                              .Select(kv => $"{EnemyTypes.Of(kv.Key).Name} {kv.Value}")));
            sb.Append($"\ngroups: {groupsDrawn.Values.Sum()} of {wavesTotal} waves - ");
            sb.Append(string.Join(", ", groupsDrawn.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
            sb.Append($"\naffixes: {affixes.Values.Sum()} of {elitesTotal} elites - ");
            sb.Append(string.Join(", ", affixes.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
            return sb.ToString();
        }

        /// <summary>How many bodies are on screen once the floor's opening fill stops.</summary>
        static int OpeningLive(Wave w)
        {
            float pressure = 0f;
            int n = 0;
            foreach (var s in w.Spawns)
            {
                if (!Fits(s, pressure, n, w.Floor)) break;
                pressure += Pressure(s.Kind, s.Elite, s.Affix);
                n++;
            }
            return n;
        }

        static T P<T>(List<T> values, float q)
        {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted[(int)(q * (sorted.Count - 1))];
        }
    }
}
