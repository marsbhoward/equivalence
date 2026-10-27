using UnityEngine;
using Convergence.Core;

namespace Convergence.Art
{
    /// <summary>
    /// A footfall's grit: two specks flicked back off the heel (group 7, the user's pick "two
    /// flicks" - it replaced a soft puff), drawn by <see cref="Combat.TexelFx.Flicks"/>.
    ///
    /// Raised by PrimitiveCharacterRig on the STRIDE's own clock, not on a timer of its own - see
    /// the footfall block there. Nothing else about it is shared with the rig: once dropped it is
    /// a loose object in the world that outlives the step, and parenting it to a character that
    /// is still walking would drag it along by the foot that made it.
    ///
    /// Deliberately not a ParticleSystem. One system per character configured from script, warmed
    /// up and torn down per run, is a great deal of machinery for a sprite that lives a third of
    /// a second - and the sorting order has to be a fixed number under every body, which a
    /// particle system makes harder to state than a SpriteRenderer does.
    /// </summary>
    public static class DustPuff
    {
        /// <summary>
        /// Under every body, above the floor and above pit art.
        ///
        /// Sitting above <see cref="SortingOrders.PitBase"/> rather than at GroundDecal is the
        /// same claim the pit band itself makes: a decal is painted ON the floor and dust is
        /// kicked up OFF it, so dust crossing a pit's lip has to stay visible over the hole.
        /// Still entirely below <see cref="SortingOrders.Enemy"/>, so nothing standing in it is
        /// ever drawn behind it.
        /// </summary>
        public const int Order = SortingOrders.PitBase + 4;

        /// <param name="back">Against the travel: the way the specks are flicked.</param>
        public static void Raise(Vector3 at, Vector2 back)
            => Combat.TexelFx.Flicks(at, Combat.TexelFx.Grey, back, Order);
    }
}
