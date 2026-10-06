using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// Gives every melee enemy near the player a SIDE to come in from, so a pack closes as a fan
    /// rather than a queue - all of them walking the same straight line arrived single file on one
    /// side, and one sidestep dealt with the lot.
    ///
    /// THE PACK KEEPS ITS ORDER. Slots are handed out in the order the enemies already stand
    /// around the player, centred on where the pack is, so nobody is sent across the circle
    /// through the others. Rebuilt every <see cref="Tuning.Steering.SurroundInterval"/>.
    ///
    /// AND IT NEVER CLOSES THE CIRCLE. The arc is capped at
    /// <see cref="Tuning.Steering.SurroundMaxArcDegrees"/>, so the side away from where the pack
    /// came in stays open. Surrounded with no way out is a death sentence, not a positioning
    /// problem.
    ///
    /// A slot only shapes the APPROACH - an enemy already in reach attacks from wherever it
    /// stands. Driven lazily from each enemy's FixedUpdate, like NavField.
    /// </summary>
    public static class EnemySurround
    {
        // Not readonly, null-guarded: see the domain-reload notes in CLAUDE.md.
        static List<EnemyController> _pack;
        static List<float> _offsets;
        static float _next = float.NegativeInfinity;

        public static void Tick(Vector2 player)
        {
            if (Time.time < _next) return;
            _next = Time.time + Tuning.Steering.SurroundInterval;

            _pack ??= new List<EnemyController>();
            _offsets ??= new List<float>();
            _pack.Clear();

            float r2 = Tuning.Steering.SurroundRadius * Tuning.Steering.SurroundRadius;
            Vector2 sum = Vector2.zero;
            var all = EnemyRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e == null) continue;
                var to = (Vector2)e.transform.position - player;
                if (!e.WantsSlot || to.sqrMagnitude > r2) { e.ClearSlot(); continue; }
                _pack.Add(e);
                if (to.sqrMagnitude > 1e-6f) sum += to.normalized;
            }
            int n = _pack.Count;
            if (n == 0) return;

            // The pack's centre bearing; a pack spread evenly all round has no centre, and then
            // any bearing will do.
            float centre = sum.sqrMagnitude > 1e-4f
                ? Mathf.Atan2(sum.y, sum.x)
                : Bearing(_pack[0], player);

            _offsets.Clear();
            for (int i = 0; i < n; i++)
                _offsets.Add(Mathf.DeltaAngle(centre * Mathf.Rad2Deg, Bearing(_pack[i], player) * Mathf.Rad2Deg));

            // Insertion sort by offset, the pack list carried along - a dozen entries at most.
            for (int i = 1; i < n; i++)
            {
                float o = _offsets[i];
                var e = _pack[i];
                int j = i - 1;
                while (j >= 0 && _offsets[j] > o)
                {
                    _offsets[j + 1] = _offsets[j];
                    _pack[j + 1] = _pack[j];
                    j--;
                }
                _offsets[j + 1] = o;
                _pack[j + 1] = e;
            }

            float spacing = n > 1
                ? Mathf.Min(Tuning.Steering.SlotSpacingMaxDegrees, Tuning.Steering.SurroundMaxArcDegrees / (n - 1))
                : 0f;
            for (int i = 0; i < n; i++)
            {
                float deg = centre * Mathf.Rad2Deg + (i - (n - 1) * 0.5f) * spacing;
                _pack[i].AssignSlot(deg * Mathf.Deg2Rad);
            }
        }

        static float Bearing(EnemyController e, Vector2 player)
        {
            var to = (Vector2)e.transform.position - player;
            return Mathf.Atan2(to.y, to.x);
        }
    }
}
