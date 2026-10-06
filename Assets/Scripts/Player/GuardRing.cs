using UnityEngine;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// The shared tell for the defensive abilities' parry window: a ring that closes onto the
    /// character and reaches it exactly as the window shuts.
    ///
    /// THE RADIUS IS THE CLOCK. It is not a flourish timed to look about right - it is
    /// <see cref="PlayerController.ParryWindowRemaining"/> drawn as a distance, so a player can
    /// learn to read how long is left rather than only that something fired. That is the whole
    /// reason it replaced the pale flash that used to sit in ActivateDefensiveAbility: one flash
    /// served all four abilities and so could only ever say "an ability happened", never which
    /// one and never how much of it was left.
    ///
    /// It draws for ALL FOUR, because the window IS all four - identical duration, identical
    /// effect, identical counter. Each ability then layers its own identity on top (Barrier's
    /// dome, Dash's after-images, Bulwark's aura). Parry Stance layers NOTHING, and that is the
    /// design rather than a gap: it has no fallback at all, which is what buys its short
    /// cooldown, so the ring alone is the complete and honest picture of what it does.
    ///
    /// Attached ONCE per run from BuildPlayer and left switched off between activations, the same
    /// shape RangeRings uses. Deliberately not spawned per press: a per-activation GameObject
    /// would have to carry its own copy of the clock, and the clock is exactly the thing that can
    /// end early (see ParryWindowRemaining).
    /// </summary>
    public class GuardRing : MonoBehaviour
    {
        // Plain UnityEngine.Object references - these survive a domain reload intact, unlike the
        // interface and dictionary fields documented in CLAUDE.md.
        PlayerController _pc;
        SpriteRenderer _sr;

        public static GuardRing Attach(PlayerController pc)
        {
            if (pc == null) return null;

            var ring = pc.GetComponentInChildren<GuardRing>(true);
            if (ring != null) { ring._pc = pc; return ring; }

            var go = new GameObject("guard-ring");
            go.transform.SetParent(pc.transform, false);
            go.transform.localPosition = Vector3.zero;

            ring = go.AddComponent<GuardRing>();
            ring._pc = pc;
            ring._sr = go.AddComponent<SpriteRenderer>();

            // The FAT soft band, not ThinRing. ThinRing's whole reason to exist is a hairline
            // that holds at a fixed size on the ground; this one travels from 1.7 units to 0.32,
            // and a hairline scaled down that far is a few pixels of nothing by the end. The same
            // "two uses, two sprites" call ThinRing itself documents, landing the other way.
            ring._sr.sprite = Spr.Ring;
            // Above the depth band with every other overlay, and below the dome so a Barrier
            // activation reads as the ring closing THROUGH the shell rather than over it.
            ring._sr.sortingOrder = SortingOrders.StatusOverlay + 2;
            ring._sr.enabled = false;
            return ring;
        }

        void LateUpdate()
        {
            if (_pc == null || _sr == null) return;

            float remaining = _pc.ParryWindowRemaining;
            if (remaining <= 0f)
            {
                // A hard cut, not a fade. The window did not taper off - it SHUT, or it was spent
                // on a parry, and in the second case the success beat is what should be drawing
                // the eye on that frame instead.
                _sr.enabled = false;
                return;
            }

            _sr.enabled = true;

            // 0 at the instant of activation, 1 as it closes. Linear, because the thing being
            // reported is linear: a window is not more urgent per second near its end, it simply
            // has less of itself left.
            float closed = 1f - Mathf.Clamp01(remaining / Tuning.Defense.ParryWindowSeconds);

            float radius = Mathf.Lerp(Tuning.Defense.GuardRingStartRadius,
                                      Tuning.Defense.GuardRingEndRadius, closed);
            transform.localScale = Vector3.one * radius * 2f;

            var c = Tuning.Defense.GuardRingColor;
            float a = Mathf.Lerp(Tuning.Defense.GuardRingWideAlpha,
                                 Tuning.Defense.GuardRingTightAlpha, closed);
            _sr.color = new Color(c.r, c.g, c.b, c.a * a);
        }
    }
}
