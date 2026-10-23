using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.Nigredo;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// THE NIGREDO (the user's calls, 2026-10-22/23; mock https://claude.ai/artifact/EvovSDqU3VH6VmLzAqcRhG):
    /// not a fifth biome but a LAYER laid over whatever biome the last quarter of a run is in. The
    /// alchemist already finished the work at the top of the tower - transmuted the Philosopher's
    /// Stone into the elixir and drank it - and the loop that cost them shows its seams here:
    ///
    ///   - the room darkens toward red-black,
    ///   - the north boundary TEARS - long jagged seams in the Rift portal's own colours (white-cyan
    ///     rim, cyan glow, violet halo, drifting shards, a cold blue light), never its standing
    ///     almond, so no tear reads as an exit,
    ///   - the STONE's red light shows through the largest tear, nearer each floor,
    ///   - deep red CRYSTALS (shards of the Stone) stand in the room, each a light,
    ///   - CINDERS: a back layer rising behind the bodies, a front layer falling like snow over them.
    ///
    /// All of it grows with <see cref="Strength"/>, 0 at <c>Tuning.Nigredo.StartShare</c> of the
    /// run's final floor, 1 on the final floor. Visual only, like the biomes. Set and built from
    /// DressArena; the room's pieces are a child of the arena root, rebuilt each floor.
    /// </summary>
    public static class Nigredo
    {
        public static float Strength { get; private set; }

        /// <summary>One-shot: the next floor's strength (0..1), whatever its number.</summary>
        public static float? DevForceNext;

        public static float For(int floor)
        {
            int final = RunFormat.FinalFloor;
            float start = final * T.StartShare;
            return floor <= start ? 0f : Mathf.Clamp01((floor - start) / (final - start));
        }

        public static void Set(int floor)
        {
            Strength = DevForceNext ?? For(floor);
            DevForceNext = null;
        }

        public static void Clear() => Strength = 0f;

        /// <summary>How large the Stone's crystals on a creature are: 0 none, 1-3.</summary>
        public static int Shards => Strength <= 0f ? 0 : Strength < 0.4f ? 1 : Strength < 0.8f ? 2 : 3;

        public static float Darkness(float place) =>
            Strength <= 0f ? place : Mathf.Max(place, Mathf.Lerp(T.DarknessStart, T.DarknessMax, Strength));

        public static Color Shade(Color place) =>
            Strength <= 0f ? place : Color.Lerp(place, T.Shade, 0.4f + 0.6f * Strength);

        /// <summary>Build this floor's tears, crystals and cinders under <paramref name="parent"/>
        /// (last floor's are destroyed first). Nothing below the layer's first floor.</summary>
        public static void Dress(Transform parent, Vector2 half, int seed)
        {
            if (parent == null) return;
            var old = parent.Find("nigredo");
            if (old != null) { old.name = "nigredo.old"; Object.Destroy(old.gameObject); }   // Destroy is deferred
            if (Strength <= 0f) return;
            var go = new GameObject("nigredo");
            go.transform.SetParent(parent, false);
            go.AddComponent<NigredoRoom>().Build(half, seed, Strength);
        }

        /// <summary>The room's crystals, once its walls are up (see NigredoRoom.PlaceCrystals).</summary>
        public static void PlaceCrystals(Transform parent, int seed)
        {
            var room = parent != null ? parent.Find("nigredo") : null;
            if (room != null && room.TryGetComponent<NigredoRoom>(out var r)) r.PlaceCrystals(seed);
        }
    }
}
