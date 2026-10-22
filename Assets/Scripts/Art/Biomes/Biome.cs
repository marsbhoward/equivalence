using UnityEngine;
using Convergence.Core;

namespace Convergence.Art.Biomes
{
    /// <summary>A place the tower has bled into. Append only.</summary>
    public enum BiomeKind { Tower, Forest, SkyRuin, SaltFlats }

    /// <summary>
    /// BIOMES (the user's calls, 2026-10-22; design in memory and the mock
    /// https://claude.ai/artifact/2SXMAiiX7eWyDjBsgi7LLm): space-time is distorted, so a floor may be
    /// any place that bled into the tower. A biome is VISUAL ONLY - it re-draws the systems already
    /// there (floor, boundary, columns, walls, force fields, chasms, pits) and sets the light and the
    /// room tone. It never changes a rule, a collider or a number, and it is chosen apart from the
    /// boss bag, so seeing one says nothing about what is coming. Anything a boss brings into the
    /// room takes the current biome's look.
    ///
    /// Biomes come in STRETCHES of a few floors (Tuning.Biomes), from the run seed on their own
    /// stream - a restart meets the same places, and nothing else's rolls move. The first stretch is
    /// always the Tower: the basics are learned in the room they were built for.
    ///
    /// <see cref="Current"/> is set at the top of each floor (GameBootstrap.DressArena), before
    /// anything is built, and every drawer asks it.
    /// </summary>
    public static class Biome
    {
        public static BiomeKind Current { get; private set; } = BiomeKind.Tower;

        /// <summary>One-shot: the next floor dressed is this biome, whatever its stretch says.</summary>
        public static BiomeKind? DevForceNext;

        public static void Set(BiomeKind kind) => Current = kind;

        public static bool Is(BiomeKind kind) => Current == kind;

        /// <summary>The biome for <paramref name="floor"/> of the run with <paramref name="runSeed"/>.
        /// Pure - walks the run's stretches from floor 1, which is cheap.</summary>
        public static BiomeKind For(int runSeed, int floor)
        {
            if (DevForceNext is BiomeKind forced) { DevForceNext = null; return forced; }
            var rng = new System.Random(unchecked(runSeed * 7121 + 977));
            int at = 1 + rng.Next(Tuning.Biomes.FirstStretchMin, Tuning.Biomes.FirstStretchMax + 1);
            var kind = BiomeKind.Tower;
            if (floor < at) return kind;
            int kinds = System.Enum.GetValues(typeof(BiomeKind)).Length;
            while (true)
            {
                // never the same place twice running
                kind = (BiomeKind)(((int)kind + 1 + rng.Next(kinds - 1)) % kinds);
                int len = rng.Next(Tuning.Biomes.StretchMin, Tuning.Biomes.StretchMax + 1);
                if (floor < at + len) return kind;
                at += len;
            }
        }

        // ------------------------------------------------------------------ the profile

        /// <summary>The darkness where no light reaches, for this biome at this depth.</summary>
        public static float Darkness(int depth, bool boss)
        {
            float d = Tuning.Lighting.DarknessFor(depth, boss);
            if (Current == BiomeKind.Forest) d += Tuning.Biomes.ForestDarker;
            if (Current == BiomeKind.SkyRuin) d *= Tuning.Biomes.SkyDarkness;   // open to the sky
            if (Current == BiomeKind.SaltFlats) d *= Tuning.Biomes.SaltDarkness; // white crust, glaring
            return Mathf.Clamp01(d);
        }

        public static Color Shade => Current switch
        {
            BiomeKind.Forest => Tuning.Biomes.ForestShade,
            BiomeKind.SkyRuin => Tuning.Biomes.SkyShade,
            BiomeKind.SaltFlats => Tuning.Biomes.SaltShade,
            _ => Tuning.Lighting.ShadeColor,
        };

        /// <summary>The room tone: the forest has its own; the tower's follows the depth.</summary>
        public static Audio.Bed Bed(int floor) => Current switch
        {
            BiomeKind.Forest => Audio.Bed.Forest,
            BiomeKind.SkyRuin => Audio.Bed.Sky,
            BiomeKind.SaltFlats => Audio.Bed.Salt,
            _ => Audio.Ambience.ForFloor(floor),
        };

        /// <summary>A forest's chasms are always its lake; the salt's, a brine sinkhole.</summary>
        public static bool ForcesLake => Current == BiomeKind.Forest || Current == BiomeKind.SaltFlats;

        /// <summary>A sky ruin's are always a drop - into the sky.</summary>
        public static bool ForcesDrop => Current == BiomeKind.SkyRuin;

        // ------------------------------------------------------------------ rooms (RoomShape.Compose)
        //
        // A biome changes the ODDS of a composed room's pieces - never the rules, never a check.
        // The forest grows organic outlines and clumps of cover; the sky ruin breaks into notched
        // outlines, ruin and great chasms; the salt stays open, few pieces. The Tower is the
        // composer as built.

        /// <summary>Multiplier on a boundary's weight.</summary>
        public static float BoundaryWeight(string name) => Current switch
        {
            BiomeKind.Forest => name switch { "cave" => 2.5f, "octagon" => 1.5f, "rect" => 0.6f, _ => 1f },
            BiomeKind.SkyRuin => name switch { "notched" => 2f, "corner" => 2f, "waist" => 1.5f, "rect" => 0.7f, _ => 1f },
            BiomeKind.SaltFlats => name switch { "rect" => 1.6f, "waist" => 1.5f, "cave" => 0.4f, "notched" => 0.6f, _ => 1f },
            _ => 1f,
        };

        /// <summary>Multiplier on a stamp's weight, from its tags.</summary>
        public static float StampWeight(string tags)
        {
            float w = 1f;
            switch (Current)
            {
                case BiomeKind.Forest:
                    if (tags.Contains("cover")) w *= 1.6f;
                    if (tags.Contains("ruin")) w *= 0.6f;
                    break;
                case BiomeKind.SkyRuin:
                    if (tags.Contains("ruin")) w *= 1.8f;
                    if (tags.Contains("wall")) w *= 0.7f;
                    break;
                case BiomeKind.SaltFlats:
                    if (tags.Contains("wall")) w *= 0.5f;
                    if (tags.Contains("cover")) w *= 1.2f;
                    break;
            }
            return w;
        }

        /// <summary>Multiplier on a composed room's chasm chance, and the large chasms' share.</summary>
        public static float ChasmChanceMul => Current == BiomeKind.SkyRuin ? 1.25f : 1f;
        public static float LargeChasmShare(float baseShare) => Current == BiomeKind.SkyRuin ? Mathf.Max(baseShare, 0.75f) : baseShare;

        /// <summary>Fewer pieces on the salt - the flats are open.</summary>
        public static int StampsLess => Current == BiomeKind.SaltFlats ? 1 : 0;

        /// <summary>Whether the tower's windows can appear (a forest has a canopy, not a wall).</summary>
        public static bool HasWindows => Current == BiomeKind.Tower;
    }
}
