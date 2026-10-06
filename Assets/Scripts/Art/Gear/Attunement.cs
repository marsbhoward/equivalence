using Convergence.Core;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// Which element a GEMMED weapon (Prism) shows lit, everywhere it is drawn.
    ///
    /// In a run it is the run's element; in the hub, the element the sigil selector is set to -
    /// the one the player would walk into the gate with. Before this, only the in-run player lit a
    /// gem: the hub, the character screen, the rack, the armoury wall and every UI card showed the
    /// FIRE gem lit whatever was chosen, because that is the base sprite's palette - and a repaint
    /// mid-run (an appearance edit, the helm toggle) put the fire gem back under the glow of
    /// another element's.
    ///
    /// Plain static state, set by its two owners (GameBootstrap for a run, HubRoom for the
    /// selector) and read by the places that draw. A domain reload resets it to Fire until the
    /// next set; the hub and the run both set it on entry, so that lasts only until then.
    /// </summary>
    public static class Attunement
    {
        public static ElementType Current { get; private set; } = ElementType.Fire;

        /// <summary>The element before the last change, and when (unscaled) it changed - what the
        /// Secret Fire's marks fade out of before coming back in <see cref="Current"/>.</summary>
        public static ElementType Previous { get; private set; } = ElementType.Fire;
        public static float ChangedAt { get; private set; } = float.NegativeInfinity;

        public static void Set(ElementType element)
        {
            if (element == Current) return;
            Previous = Current;
            Current = element;
            ChangedAt = Time.unscaledTime;
        }

        /// <summary>The lit variant of <paramref name="item"/>'s blade at the density asked for,
        /// or null for a weapon with no gems.</summary>
        public static Sprite Blade(GearItem item, bool menu)
            => item != null && item.HasElementGems ? item.BladeFor(Current, menu) : null;
    }
}
