using System.Collections.Generic;

namespace Convergence.Enemies
{
    /// <summary>
    /// Every live enemy, maintained by the enemies themselves.
    ///
    /// Targeting runs every frame, so it must not allocate. A physics overlap query would
    /// return a fresh array each call; this is a plain list the enemies add and remove
    /// themselves from as they spawn and die.
    /// </summary>
    public static class EnemyRegistry
    {
        static readonly List<EnemyController> _all = new();

        public static IReadOnlyList<EnemyController> All => _all;

        public static void Register(EnemyController e)
        {
            if (e != null && !_all.Contains(e)) _all.Add(e);
        }

        public static void Unregister(EnemyController e) => _all.Remove(e);

        /// <summary>Drop any entries destroyed without OnDisable running (domain-reload safety).</summary>
        public static void Prune() => _all.RemoveAll(e => e == null);
    }
}
