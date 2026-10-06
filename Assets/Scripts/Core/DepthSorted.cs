using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;

namespace Convergence.Core
{
    /// <summary>
    /// Sorts something by where it stands, so the character can walk behind it.
    ///
    /// Everything else in this project sorts on a fixed layer, which is correct right up until
    /// the room contains objects rather than scenery. A trophy on the floor is the first thing
    /// here a player will try to walk around, and a fixed layer can only ever be always-in-front
    /// or always-behind - both of which read as the object not really being there.
    ///
    /// Relative stacking inside the object is preserved: whatever offsets its own renderers were
    /// built with are captured once and re-applied on top of the new base, so a plinth's art
    /// stays in front of its plinth no matter where it is standing.
    /// </summary>
    public class DepthSorted : MonoBehaviour
    {
        /// <summary>
        /// Added to y before sorting. Sorting must happen at the FEET, not the origin - two
        /// objects whose art is anchored differently would otherwise disagree about which is
        /// nearer despite standing on the same line.
        /// </summary>
        public float FootOffset;

        /// <summary>
        /// Sorts as if this much NEARER the camera than it really is, without moving it.
        ///
        /// Exists for the player in combat. Honest y-sorting means a crowd closing from below
        /// legitimately covers you, which is correct and is also the moment you most need to see
        /// where you are. A small bias lets the player win near-ties while still going properly
        /// behind anything clearly in front - it buys readability without turning the sorting
        /// back into "player always on top".
        /// </summary>
        public float Bias;

        /// <summary>Set for anything that never moves, so it stops costing a LateUpdate.</summary>
        public bool Fixed;

        ICharacterRig _rig;
        SpriteRenderer[] _renderers;
        int[] _offsets;
        int _last = int.MinValue;

        public static DepthSorted Attach(GameObject go, ICharacterRig rig, float footOffset = 0f,
                                         bool isFixed = false, float bias = 0f)
        {
            var d = go.AddComponent<DepthSorted>();
            d._rig = rig;
            d.FootOffset = footOffset;
            d.Fixed = isFixed;
            d.Bias = bias;
            d.Apply();
            return d;
        }

        public static DepthSorted Attach(GameObject go, float footOffset, bool isFixed,
                                         params SpriteRenderer[] renderers)
        {
            var d = go.AddComponent<DepthSorted>();
            d.FootOffset = footOffset;
            d.Fixed = isFixed;

            // OVERLAYS ARE EXCLUDED. Anything already at or above StatusOverlay is deliberately
            // above the whole depth band - a status halo, a hit flash, a reticle - and pulling it
            // into a depth-relative offset both destroys that guarantee and overflows: a halo at
            // 24000 rebased onto a depth of 11300 lands at 35295, past the short that Unity
            // stores a sorting order in, and comes back as -30241. It vanishes behind the floor.
            //
            // A caller passing GetComponentsInChildren cannot reasonably know which renderers
            // those are, so the filter belongs here rather than at every call site.
            var kept = new List<SpriteRenderer>(renderers.Length);
            foreach (var sr in renderers)
                if (sr != null && sr.sortingOrder < SortingOrders.StatusOverlay) kept.Add(sr);
            d._renderers = kept.ToArray();

            // Captured relative to the LOWEST, not to zero: these were authored as absolute
            // orders on some other layer, and only the gaps between them carry meaning.
            int lowest = int.MaxValue;
            foreach (var sr in d._renderers) if (sr.sortingOrder < lowest) lowest = sr.sortingOrder;
            d._offsets = new int[d._renderers.Length];
            for (int i = 0; i < d._renderers.Length; i++)
                d._offsets[i] = d._renderers[i].sortingOrder - lowest;

            d.Apply();
            return d;
        }

        void LateUpdate()
        {
            if (Fixed) return;
            Apply();
        }

        public void Apply()
        {
            int order = SortingOrders.ForDepth(transform.position.y + FootOffset - Bias);
            if (order == _last) return;
            _last = order;

            if (_rig != null && _rig.Transform != null) { _rig.SetSortingBase(order); return; }
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                // Clamped: Unity stores a sorting order in a short, and a value past it wraps to
                // a large negative rather than saturating - the sprite does not draw slightly
                // wrong, it disappears behind the floor.
                _renderers[i].sortingOrder = Mathf.Clamp(order + _offsets[i], short.MinValue, short.MaxValue);
            }
        }
    }
}
