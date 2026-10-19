using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// What a disc in flight looks like when an attack sends BOTH discs out at once (the user's
    /// calls, 2026-10-16): every disc of it - Ripple, Kickback, Scattershot and any volley,
    /// Sublimate, Revolve, Orrery, Plow - and Carom's joined disc are drawn as the equipped
    /// weapon's COMBINED picture (GearItem.CombinedFrames, PlayerController.CombinedFrames - the
    /// whole Armillary, all four bands), animated on its own clock. A ONE-disc throw (the basics,
    /// Mark, Heavy Cleaver, Tether) keeps the half that left the hand, as do the swings with both
    /// in hand. A weapon with no combined picture (every ordinary disc) is unchanged, and so is
    /// one whose own RELIC is not socketed (PlayerController.CombinedInFlight - the Black Diamond
    /// rule: the bespoke treatment needs the cosmetic and its relic).
    ///
    /// Swapped in AFTER the disc is sized: the combined art is at the same texel density, so the
    /// scale set for the half is the right one for it too.
    /// </summary>
    public static class DiscPictures
    {
        public static bool HasCombined(Player.PlayerController p)
            => p != null && p.CombinedInFlight
               && p.CombinedFrames != null && p.CombinedFrames.Length > 0 && p.CombinedFrames[0] != null;

        /// <summary>Show the combined picture on <paramref name="sr"/>, if the weapon has one.</summary>
        public static void ShowCombined(SpriteRenderer sr, Player.PlayerController p)
        {
            if (sr == null || !HasCombined(p)) return;
            sr.sprite = p.CombinedFrames[0];
            if (p.CombinedFrames.Length > 1)
            {
                var cycle = sr.gameObject.GetComponent<SpriteCycle>();
                if (cycle == null) cycle = sr.gameObject.AddComponent<SpriteCycle>();   // never ??: Unity's null
                cycle.Frames = p.CombinedFrames;
                cycle.FrameSeconds = p.CombinedFrameSeconds;
            }
        }
    }
}
