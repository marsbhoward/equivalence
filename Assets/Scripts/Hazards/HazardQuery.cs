using System.Collections.Generic;
using UnityEngine;
using Convergence.Enemies;

namespace Convergence.Hazards
{
    /// <summary>
    /// Clear: nothing in the way. Blocked: a COLUMN (solid architecture) stopped it outright -
    /// there is nothing on the other side of that to negotiate with. Nulled: a Blue force field
    /// let it through as far as existing goes, but its effect is zero. Amplified: a Red force
    /// field let it through at double effect.
    ///
    /// Blocked and Nulled read the same to a caller that only cares about "did this reach the far
    /// end at all" (Turret's beam, which a Blue field switches off exactly like a wall). A caller
    /// that has to keep treating the far end as a valid target either way - melee, which must not
    /// let a force field make a target un-targetable, only make the HIT on it worth zero or
    /// double - is why the two are kept distinct rather than folded into one Blocked value.
    /// </summary>
    public enum SightResult { Clear, Blocked, Nulled, Amplified }

    /// <summary>
    /// The one obstruction check every sight/attack-blocking hazard interaction goes through -
    /// Turret's beam, melee's own candidate gathering, and every thrown weapon's flight all ask
    /// the same question: is there a column or force field between these two points, and if it's
    /// a field, which way does it cut?
    ///
    /// Generalized out of the raycast originally written for EnemyController.HasLineOfSight, which
    /// is now a thin wrapper over Query.
    /// </summary>
    public static class HazardQuery
    {
        /// <summary>Reusable buffer - this runs every frame from multiple call sites, so it must not allocate.</summary>
        static readonly List<RaycastHit2D> _hits = new(8);
        static readonly ContactFilter2D _filter = new() { useTriggers = true };

        /// <summary>
        /// Is the straight line from <paramref name="from"/> to <paramref name="to"/> clear?
        /// Enemy bodies never count as an obstruction here (see Column's own doc comment on why),
        /// and neither does <paramref name="ignoreA"/>/<paramref name="ignoreB"/> - normally the
        /// querier itself and whatever it's asking about the other end of the line.
        /// </summary>
        public static SightResult Query(Vector2 from, Vector2 to, GameObject ignoreA, GameObject ignoreB)
        {
            var offset = to - from;
            float dist = offset.magnitude;
            if (dist < 0.0001f) return SightResult.Clear;

            int count = Physics2D.Raycast(from, offset / dist, _filter, _hits, dist);
            bool nulled = false, amplified = false;

            for (int i = 0; i < count; i++)
            {
                var col = _hits[i].collider;
                if (col == null) continue;
                var go = col.gameObject;
                if (go == ignoreA || go == ignoreB) continue;
                if (go.GetComponent<EnemyController>() != null) continue;

                var field = go.GetComponent<ForceField>();
                if (field != null)
                {
                    // Keep scanning either way - a column further along still fully blocks, and
                    // that has to win over a field's own, gentler verdict.
                    if (field.Charge == FieldColor.Blue) nulled = true;
                    else amplified = true;
                    continue;
                }

                // Anything else solid (a Column; the arena's own boundary walls, though those can
                // never actually sit between two interior points) blocks outright.
                return SightResult.Blocked;
            }

            // A spire's bubble is a Blue field bent round the ring: a line with one end inside and
            // one outside is nulled. Checked after the raycast so a column still blocks outright.
            if (Spire.BubbleSeparates(from, to)) nulled = true;

            if (nulled) return SightResult.Nulled;   // Blue dominates Red if a line somehow crosses both
            return amplified ? SightResult.Amplified : SightResult.Clear;
        }

        /// <summary>
        /// For something that MOVES through space rather than querying a static line - the leg a
        /// projectile just traveled this frame. Force fields matter here, and a room's interior
        /// WALLS (Arena.WallBetween) - those return Blocked. Columns deliberately do not stop a
        /// projectile's flight (see the Hazards plan's explicit non-goal on this): a pillar is
        /// cover from sight, a wall is cover from shots. What Blocked does is each mover's call
        /// - a bolt dies, a disc drops home, the thrown blade rebounds as off the outer wall -
        /// and a weapon RETURNING to the hand ignores it, or a wall would keep it from the player.
        /// </summary>
        public static SightResult CheckCrossing(Vector2 prevPos, Vector2 newPos)
        {
            var offset = newPos - prevPos;
            float dist = offset.magnitude;
            if (dist < 0.0001f) return SightResult.Clear;

            // A projectile crossing a spire bubble's wall, either way, is spent on it.
            if (Spire.BubbleSeparates(prevPos, newPos)) return SightResult.Nulled;
            if (Core.Arena.WallBetween(prevPos, newPos)) return SightResult.Blocked;

            int count = Physics2D.Raycast(prevPos, offset / dist, _filter, _hits, dist);
            bool amplified = false;

            for (int i = 0; i < count; i++)
            {
                var field = _hits[i].collider != null ? _hits[i].collider.GetComponent<ForceField>() : null;
                if (field == null) continue;
                if (field.Charge == FieldColor.Blue) return SightResult.Nulled;
                amplified = true;
            }

            return amplified ? SightResult.Amplified : SightResult.Clear;
        }
    }
}
