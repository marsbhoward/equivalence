using System;

namespace Convergence.Rifts
{
    /// <summary>
    /// EVERY REWARD A FLOOR CAN REPLAY IS ROLLED FROM THE RUN, not from live randomness - the half of
    /// resumable runs that stops them being a reroll button (CONTINUATION.md). A resumed floor is
    /// played again from its start; were its drops rolled fresh, a player who saw a poor one could
    /// close the tab and fight the floor again for another. So each roll comes from (run seed,
    /// floor, which roll): the Nth kill's drop on floor F is the same on every attempt, and so are
    /// the boss's box, the avatar's rolls and the reward rings.
    ///
    /// Done as a SCOPE over UnityEngine.Random rather than by threading an RNG through RunLoot,
    /// MovesetLibrary and WeaponClasses: inside it the global stream is seeded, and the state it
    /// had is put back on dispose - so nothing outside the roll notices (the same courtesy
    /// PlayerPower's simulation pays).
    ///
    ///     using (RunRolls.Seeded(_planner.Seed, _floor, RunRolls.Kind.KillGear, kill)) { ... }
    /// </summary>
    public static class RunRolls
    {
        public enum Kind
        {
            /// <summary>The Nth kill's Rift Box roll.</summary>
            KillBox = 1,
            /// <summary>The Nth kill's gear roll - the chance and the piece.</summary>
            KillGear = 2,
            /// <summary>What the clear pays: a boss floor's box, an avatar's design-box rolls.</summary>
            Clear = 3,
            /// <summary>The reward rings, index = the Curator reroll.</summary>
            Reward = 4,
        }

        /// <summary>A well-mixed seed for one roll (SplitMix-style), so neighbouring floors and
        /// indices don't land on neighbouring streams.</summary>
        public static int SeedFor(int runSeed, int floor, Kind kind, int index)
        {
            unchecked
            {
                ulong z = (ulong)(uint)runSeed;
                z = z * 0x9E3779B97F4A7C15UL + (ulong)(uint)floor;
                z = z * 0x9E3779B97F4A7C15UL + (ulong)kind;
                z = z * 0x9E3779B97F4A7C15UL + (ulong)(uint)index;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                return (int)(z ^ (z >> 32));
            }
        }

        public static Scope Seeded(int runSeed, int floor, Kind kind, int index)
            => new(SeedFor(runSeed, floor, kind, index));

        /// <summary>UnityEngine.Random seeded for its lifetime, then restored.</summary>
        public readonly struct Scope : IDisposable
        {
            readonly UnityEngine.Random.State _saved;

            public Scope(int seed)
            {
                _saved = UnityEngine.Random.state;
                UnityEngine.Random.InitState(seed);
            }

            public void Dispose() => UnityEngine.Random.state = _saved;
        }
    }
}
