using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// How a swing should look. Deliberately describes SHAPE, not a specific move, so the rig
    /// never has to know what a moveset is - a new finisher picks an existing motion and animates
    /// correctly with no rig change.
    ///
    /// Before this existed every attack in the game played one fixed 0.24s overhand, so Flurry,
    /// Whirlwind and Impale were visually identical.
    /// </summary>
    public enum AttackMotion
    {
        /// <summary>Overhand arc, brought down. The default weight.</summary>
        Chop,
        /// <summary>Wide horizontal arc across the body.</summary>
        Sweep,
        /// <summary>Straight extension along the facing.</summary>
        Thrust,
        /// <summary>Full-body rotation - the cleaving finishers.</summary>
        Spin,
        /// <summary>Several quick shallow strikes inside one swing window.</summary>
        Jab,
        /// <summary>Overhead whip that releases the weapon.</summary>
        Throw,
        /// <summary>Blade driven down from overhead with the whole body behind it.</summary>
        Slam,
        /// <summary>
        /// Three complete swings inside one window, alternating direction.
        ///
        /// Separate from <see cref="Jab"/> because the count belongs to the MOVE, not to the
        /// shape: Flurry's basics are single quick pokes and its finisher is a triple, and both
        /// used to be Jab - so making Jab a triple would have turned every basic in that moveset
        /// into three swings too.
        ///
        /// Appended rather than inserted. AttackMotion is an enum on serialisable data and this
        /// project has already had one save corrupted by renumbering an enum in place.
        /// </summary>
        Flurry,

        /// <summary>
        /// The blade driven point-down into the ground and HELD there.
        ///
        /// Unlike every other motion this one does not return: the arm comes down, the sword ends
        /// vertical at the character's feet, and it stays planted for the rest of the window. Slam
        /// is the near miss - it also comes down hard, but it swings THROUGH and recovers, which
        /// reads as a strike at something rather than as setting the sword into the earth and
        /// letting what follows come out of the ground.
        ///
        /// Appended, never inserted. AttackMotion is an enum on serialisable data and this project
        /// has already had one save corrupted by renumbering an enum in place.
        /// </summary>
        Plant,

        /// <summary>
        /// Two beats, no charge: reach down to the hip for a vial, then draw the string back.
        /// The bow's whole basic - unlike Plant or a real charge, this always completes on a
        /// fixed clock, because the class's cost is cadence and range, never a held state.
        /// </summary>
        Draw,

        /// <summary>
        /// Whip the weapon out of the hand rather than swing it: the arm releases in the same
        /// whip <see cref="Throw"/> uses, then holds empty while a separate cosmetic object
        /// (<c>Combat.WhirlingBlade</c>) carries the weapon in a circle around the player and
        /// back, spinning on its own axis the whole way.
        ///
        /// Only <see cref="AttackStep.OrbitsWeapon"/> steps ever set this - today, Whirlwind
        /// alone. It replaces the old whole-body <see cref="Spin"/> read for that one finisher;
        /// Carousel, Echo, Exsanguinate and Vortex still spin the body holding the weapon, which
        /// is correct for them - a disc carousel and a vial emptying in every direction are not
        /// "throw the sword and watch it circle you".
        ///
        /// Appended, never inserted. AttackMotion is an enum on serialisable data and this
        /// project has already had one save corrupted by renumbering an enum in place.
        /// </summary>
        WhirlThrow,

        /// <summary>
        /// An upward, rising inverted cut from low lead hip to high rear shoulder, finishing in
        /// a coiled, loaded ready stance. The lead-in for finishers that start HIGH (the chops,
        /// the overhand) - it ends within a few degrees of where a Chop cocks. NOT for the ones
        /// that start low: watched frame by frame, Rise into Whirlwind threw a whole downstroke
        /// into the 0.06s entry blend and then reversed it. Those take <see cref="Wind"/>.
        ///
        /// Appended, never inserted. AttackMotion is an enum on serialisable data and this
        /// project has already had one save corrupted by renumbering an enum in place.
        /// </summary>
        Rise,

        /// <summary>
        /// Holds the blade up horizontally at shoulder height, drawing it back to the shoulder
        /// before lunging violently forward in a deep, penetrating skewer. The signature motion
        /// of the heavy Impale finisher.
        ///
        /// Appended, never inserted. AttackMotion is an enum on serialisable data and this
        /// project has already had one save corrupted by renumbering an enum in place.
        /// </summary>
        Impale,

        /// <summary>
        /// A low drawing cut that COILS the body for a release. Starts where a Chop ends (blade
        /// low in front), drags the blade back and down through the target as the hands pull to
        /// the rear hip and the torso turns away - ending wound up, blade still low, which is
        /// the pose the spins, the whirl and the throw unwind from. The lead-in for those
        /// finishers, the way Rise is the lead-in for the ones that start high.
        ///
        /// Appended, never inserted. AttackMotion is an enum on serialisable data and this
        /// project has already had one save corrupted by renumbering an enum in place.
        /// </summary>
        Wind,
    }

    /// <summary>Timing shared by the rig and the gameplay that depends on it.</summary>
    public static class AttackMotions
    {
        // Central values: Tuning.Attack. Aliased here so the timing helpers below read cleanly.
        public const float FillFraction = Tuning.Attack.SwingFillFraction;
        public const float MinSeconds = Tuning.Attack.SwingMinSeconds;
        public const float MaxSeconds = Tuning.Attack.SwingMaxSeconds;

        /// <summary>
        /// How long a swing actually animates, given the interval until the next one.
        ///
        /// Shared deliberately: the finisher lock has to expire exactly when the animation ends.
        /// When the controller assumed the raw interval and the rig quietly clamped to
        /// <see cref="MaxSeconds"/>, a heavy finisher held the player still for 0.22s after the
        /// move had visibly finished, with nothing on screen explaining why.
        /// </summary>
        public static float SwingSeconds(float interval)
            => Mathf.Clamp(interval * FillFraction, MinSeconds, MaxSeconds);

        /// <summary>
        /// How long the controller must gate the NEXT attack, given the raw interval.
        ///
        /// Normally identical to <paramref name="interval"/> itself - <see cref="FillFraction"/>
        /// keeps the animation shorter than the interval by design, leaving a little recovery.
        /// But <see cref="SwingSeconds"/> also clamps to <see cref="MinSeconds"/>, and at a large
        /// enough attack-speed buff the raw interval can fall BELOW that floor - at which point
        /// the animation is longer than the interval that was about to gate it, and a new swing
        /// could start while the old one was still visibly playing. Taking the max of the two
        /// guarantees the gate is never shorter than the thing it is gating.
        /// </summary>
        public static float CooldownFor(float interval) => Mathf.Max(interval, SwingSeconds(interval));
    }
}
