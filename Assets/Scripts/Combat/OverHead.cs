using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// The rows over a creature's head, so marks stacked there never overlap (the user's call,
    /// 2026-10-26): Sulfur's PIPS just over the head, the STAGGER stars above them, the disc Mark's
    /// SIGIL on top. Each row is measured from the highest DRAWN texel of the body (a sprite's
    /// bounds are padded), in world units.
    /// </summary>
    public static class OverHead
    {
        /// <summary>The bottom of Sulfur's pips (Seasoning).</summary>
        public const float PipsAt = 0.05f;
        /// <summary>The middle of the stagger stars' orbit (StatusMarks).</summary>
        public const float StarsAt = 0.32f;
        /// <summary>The bottom of the Mark's sigil (MarkSigil).</summary>
        public const float SigilAt = 0.55f;

        /// <summary>The highest drawn texel of any of <paramref name="body"/>'s own layers, and the
        /// x of the layer it is on; false when nothing is drawn.</summary>
        public static bool Top(Transform body, out float top, out float x)
        {
            top = float.MinValue; x = body.position.x;
            foreach (var r in body.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (r == null || !r.enabled || r.sprite == null || r.sortingOrder >= SortingOrders.StatusOverlay) continue;
                if (r.name.StartsWith("status.") || r.name.StartsWith("ledger.") || r.name == "armor-ring"
                    || r.name == "seasoned" || r.name == HitFlash.ChildName) continue;
                var prof = BurnEmbers.ProfileOf(r.sprite);
                if (prof.Highest < 0) continue;
                float rowTop = r.bounds.min.y + (prof.Highest + 1) / r.sprite.pixelsPerUnit * Mathf.Abs(r.transform.lossyScale.y);
                if (rowTop > top) { top = rowTop; x = r.bounds.center.x; }
            }
            return top > float.MinValue;
        }
    }
}
