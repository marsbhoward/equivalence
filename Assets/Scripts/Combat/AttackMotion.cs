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

        /// <summary>
        /// The Impale art: the blade pulled back into the CHAMBER during the timing bar's
        /// wind-up (the draw is animated over the hold, not frozen on a frame), then a fast
        /// level thrust as the whole body lunges forward with it. Frame zero is the chamber;
        /// the guard-to-chamber draw lives at negative k, which only the held wind-up plays
        /// (see PrimitiveCharacterRig's Lunge case). The player's own displacement is
        /// AttackStep.LungeDistance - this is only the picture.
        ///
        /// Appended, never inserted. AttackMotion is an enum on serialisable data and this
        /// project has already had one save corrupted by renumbering an enum in place.
        /// </summary>
        Lunge,

        // ---- the dimensional basics (2026-10-14) ----
        //
        // Five two-hit basic combos authored as keyframes with the rig's dimensions - the body
        // (crouch, stance, step, lean), turning AWAY from the camera (the back view), a full TURN
        // through the views, and the blade passing BEHIND the body - each ending in a different
        // CONNECTOR pose for the weapon arts to start from (PrimitiveCharacterRig's Combos
        // partial). The user's brief; the names are mine. Appended, never inserted (saved data).

        /// <summary>Crescent, hit 1: a diagonal descending cut out of a coil behind the head.</summary>
        CrescentCut,
        /// <summary>Crescent, hit 2: a rising backhand that turns the body AWAY, the blade passing
        /// behind the head - ends HIGH-BACK, turned away.</summary>
        CrescentRise,
        /// <summary>Cyclone, hit 1: a flat cut from behind the hip, the blade crossing from behind
        /// the body to in front of it.</summary>
        CycloneCut,
        /// <summary>Cyclone, hit 2: a full turn through the views with the blade held out low -
        /// ends LOW-FORWARD, crouched.</summary>
        CycloneSpin,
        /// <summary>Undertow, hit 1: a stepping thrust out of a drawn-back chamber.</summary>
        UndertowThrust,
        /// <summary>Undertow, hit 2: the blade dragged back down past the body and behind it -
        /// ends LOW-BACK, the blade behind the rear hip.</summary>
        UndertowDraw,
        /// <summary>Ascent, hit 1: an underhand rising cut from low behind the body.</summary>
        AscentRise,
        /// <summary>Ascent, hit 2: a pivot that shows the back as the blade circles overhead -
        /// ends OVERHEAD, the charge's raised pose.</summary>
        AscentPivot,
        /// <summary>Return, hit 1: a wind-up that turns the back to the camera, then the chop down
        /// as the body turns back into it.</summary>
        ReturnChop,
        /// <summary>Return, hit 2: a backhand up across the body and behind the head, then the
        /// blade brought back down level - ends in the GUARD, thrust-ready.</summary>
        ReturnSweep,

        /// <summary>Sunder's art (2026-10-14): out of the Ascent pair's OVERHEAD end, a rise onto
        /// the toes, then the blade driven over the top and planted in the ground in front, deep
        /// in a crouch. Keyframed like the dimensional basics. Appended (saved data).</summary>
        SunderSlam,

        /// <summary>Wanderblade's throw (2026-10-14): Throw's whip, but starting EXACTLY where the
        /// Wind lead-in leaves the arm - its swung-back end - so the gather holds that pose and
        /// nothing snaps forward into it (Throw's own start sat 30 degrees ahead of Wind's end, the
        /// stutter the user saw). Throw itself is shared with the discs and the bow arts, whose
        /// gathers hold its first frame, so it is left alone. Appended (saved data).</summary>
        Cast,

        /// <summary>Falling Star's takeoff (2026-10-14): out of the Cyclone pair's crouched,
        /// LOW-FORWARD end, a deeper gather, then a full TURN through the views as the figure
        /// leaves the ground, the blade swept round and up to end OVERHEAD - SunderSlam's first
        /// frame, which the landing plays. Keyframed like the dimensional basics. Appended.</summary>
        StarLeap,

        /// <summary>Riposte's COUNTER (2026-10-14), when the guard turned something aside: out of
        /// the Crescent pair's HIGH-BACK turned-away end (the hanging guard), the body turns a
        /// full revolution counter-clockwise and leads, the blade trailing, then sweeping across
        /// the front of the body and out level. The player steps in with it. Appended.</summary>
        RiposteTurnabout,

        /// <summary>Riposte when the guard turned NOTHING aside (2026-10-14): out of the hanging
        /// guard the figure turns back to the attacker clockwise, the blade coming round with
        /// it - no swing, no hit. Appended (the slot of the Pivot mock, which never shipped).</summary>
        RiposteReturn,

        // ---- disc RANGED basics, MOCK (2026-10-15): a dance - each pair's first throw BUILDS
        // momentum, its second (the OFF hand's) spends it and ends on a connector. ----

        /// <summary>Pirouette, throw 1: a full body turn through the views, the throwing arm
        /// trailing, the disc whipped out as the body comes round to the target.</summary>
        DiscPirouette,

        /// <summary>Pirouette, throw 2: a counter-turn the other way, the OFF hand throwing
        /// underarm on the way back - ends cocked behind the ear (RELEASE, Throw's first frame).</summary>
        DiscCounterTurn,

        /// <summary>Crow Hop, throw 1: a skip forward onto the back foot, the body loading
        /// sideways, an overarm release as the front foot lands.</summary>
        DiscCrowHop,

        /// <summary>Crow Hop, throw 2: a shorter hop and a sidearm throw from the OFF hand - ends
        /// low with both arms wide (ORBIT).</summary>
        DiscCrowSide,

        /// <summary>Charge, throw 1: quick strides forward with the arm pumping, then a long
        /// stretching throw at full stride.</summary>
        DiscCharge,

        /// <summary>Charge, throw 2: stopping hard on the front foot, the OFF hand throwing as the
        /// momentum swings it round - ends with the arms CROSSED at the chest (CROSS).</summary>
        DiscBrake,

        /// <summary>Leap, throw 1: a crouch and a jump, arched at the top with both discs drawn
        /// overhead, one thrown DOWN at the target as it comes down.</summary>
        DiscLeap,

        /// <summary>Leap, throw 2: landing low, the OFF hand skipping its disc low along the
        /// ground, then rising with both arms up (HALO).</summary>
        DiscSkip,

        /// <summary>Windmill, throw 1: the throwing arm wheels a full circle at the shoulder, the
        /// body rocking back then forward, released underarm at the bottom, rising.</summary>
        DiscWindmill,

        /// <summary>Windmill, throw 2: the OFF arm wheels the other way and throws as the body
        /// sways back, turning away with both hands drawn behind the hip (COIL).</summary>
        DiscCounterWheel,

        // ---- disc MELEE basics, MOCK (2026-10-15): both hands strike (the pair split), each
        // pair ending on the same five connectors as the ranged dance. ----

        /// <summary>Twin Cut 1: a main-hand rising backhand.</summary>
        DiscTwinCut,
        /// <summary>Twin Cut 2: the off hand's forehand following through; ends arms CROSSED.</summary>
        DiscTwinCross,
        /// <summary>Step-turn 1: a stepping level slash that turns the back to the camera.</summary>
        DiscStepCut,
        /// <summary>Step-turn 2: the turn completed, both arms flung wide; ends LOW and WIDE (ORBIT).</summary>
        DiscFlingTurn,
        /// <summary>Scissor 1: a low uppercut with both discs together.</summary>
        DiscUppercut,
        /// <summary>Scissor 2: the halves part overhead and cross like shears; ends both raised (HALO).</summary>
        DiscScissor,
        /// <summary>Punch and draw 1: a straight punch-cut, the disc leading like a fist.</summary>
        DiscPunch,
        /// <summary>Punch and draw 2: a backhand that turns away; ends both drawn behind the hip (COIL).</summary>
        DiscBackhandCoil,
        /// <summary>Chop and wind 1: a downward cut.</summary>
        DiscChop,
        /// <summary>Chop and wind 2: a backhand carried on past the shoulder into the cocked throw (RELEASE).</summary>
        DiscWindUp,

        /// <summary>Tether's cast (2026-10-15): out of CROSS, the arms coil tighter, then snap
        /// open - the main hand flinging its disc sidearm - and the hand is drawn back to the hip
        /// as the line pulls.</summary>
        DiscTetherCast,

        /// <summary>Revolve's cast (2026-10-15): out of ORBIT, the arms sink lower and wider, then
        /// sweep up and round, letting both discs go into the ring, and settle empty-handed.</summary>
        DiscOrbitCast,

        /// <summary>Plow's cast (2026-10-15): out of CROSS, a deep crouch with both arms drawn back
        /// low, then swung down and forward together - both discs bowled along the ground.</summary>
        DiscPlowCast,

        /// <summary>Cross Cut (2026-10-15): out of CROSS the crossed arms coil up high, then slash
        /// out and down, flinging apart - the main hand forward, the off hand back - an X across
        /// the enemy in front.</summary>
        DiscCrossCut,

        /// <summary>Ripple (2026-10-15): out of RELEASE both arms coil back together, then whip
        /// forward and apart - the main hand up, the off hand down - each letting two discs go.</summary>
        DiscFanThrow,

        /// <summary>Carom (2026-10-15): out of RELEASE both hands come together over the head -
        /// the pair combined - and hurl it forward two-handed.</summary>
        DiscCaromHurl,

        /// <summary>Heavy Cleaver (2026-10-15): out of HALO the main hand draws its disc back
        /// behind the head and brings it down over the top - a cleaver's chop - and lets it go,
        /// the off arm driving down to counter.</summary>
        DiscCleaverChop,

        /// <summary>Carousel (2026-10-15): out of ORBIT the arms lift wide to shoulder height, a
        /// disc out in each hand, and the body turns a full circle, sinking as it goes.</summary>
        DiscCarousel,

        /// <summary>Kickback (2026-10-15): out of COIL the body unwinds and both arms sling forward
        /// from the hip, flinging a fan of discs, the push of it carrying the player back.</summary>
        DiscKickFling,

        /// <summary>Scattershot (2026-10-16): out of HALO a crouch and a jump, both discs gathered
        /// high, then both arms flung down and out at the top - a ring of discs from the air.</summary>
        DiscScatterBurst,

        /// <summary>Mark (2026-10-16): out of RELEASE the off arm points at the target while the
        /// main hand draws back across the body and flicks the disc out sidearm.</summary>
        DiscMarkFlick,

        /// <summary>Sublimate (2026-10-16): out of CROSS the crossed arms squeeze in, then snap
        /// open wide and fling the discs out to hang.</summary>
        DiscSublimateOpen,

        /// <summary>Orrery (2026-10-16): out of ORBIT the body sinks with the arms low and wide,
        /// then both arms sweep up and out, flinging the discs onto their orbits.</summary>
        DiscOrreryCast,

        /// <summary>Gyre (2026-10-16): out of HALO both arms reach out in front and the discs leave
        /// them to spin there as a rotor.</summary>
        DiscGyreLift,

        /// <summary>Gyre's cut: from the arms out in front (Lift's end, held while the pull runs)
        /// both slash down and across as the discs come back to the hands.</summary>
        DiscGyreCut,

        /// <summary>King and Queen's pincer throw (2026-10-16): out of RELEASE the arms cross in
        /// front, then sweep apart, each hand slinging its half round its own side.</summary>
        DiscPincerThrow,

        /// <summary>King and Queen's hurl: from both arms raised overhead to receive the pair (its
        /// first frame, held through the catch) both drive down, hurling the discs onto the target.</summary>
        DiscPincerHurl,

        /// <summary>
        /// The bow's KNEEL connector, reached by MOVING (the user's idea, 2026-10-17 - a basic may be
        /// a move instead of a shot): a full turn through the views while the body travels, ending
        /// down on one knee with the bow up and ready. No arrow; PlayerController.BowRollStrike moves
        /// the body (a real dash, by the move input, else away from the target).
        /// </summary>
        BowKneelRoll,

        /// <summary>The bow's <see cref="Draw"/> from the KNEEL - the same two hands, the body held
        /// down on one knee throughout. What an art starting from the Kneel connector looses with.</summary>
        KneelDraw,

        /// <summary>
        /// The bow's RECOIL connector, reached by MOVING (2026-10-17): a hop straight back from the
        /// target, landing with the weight on the back foot and the bow pulled in.
        /// PlayerController.BowRollStrike moves the body (a real dash, always away from the target).
        /// </summary>
        BowRecoilHop,

        /// <summary>The bow's <see cref="Draw"/> from the RECOIL - weight back, leaning away.</summary>
        RecoilDraw,

        /// <summary>
        /// The bow's HIGH connector's lob (2026-10-17): the draw with the bow RAISED toward the sky
        /// above the target - the arrow arcs up and down onto it (ThrownArrow.Lob) - and the bow held
        /// up there after the loose.
        /// </summary>
        LobDraw,

        /// <summary>The bow's <see cref="Draw"/> from the HIGH connector: it starts raised where the
        /// lob left it and comes down onto the target as the arrow is nocked.</summary>
        HighDraw,

        /// <summary>The bow's SNAP shot (the Canted pair's first, 2026-10-17): the draw on a faster
        /// clock, loosed at Tuning.Bow.SnapRelease of the swing, the rest follow-through.</summary>
        SnapDraw,

        /// <summary>
        /// The bow's CANTED connector (2026-10-17): drawn with the bow turned FLAT - foreshortened in
        /// this view, the stave toward the camera - sidearm, low. The bow is held canted after the
        /// loose, and the art from the connector is drawn canted too.
        /// </summary>
        CantedDraw,

        /// <summary>The bow's TWIN NOCK (2026-10-17): the draw with TWO arrows on the string, loosed
        /// together in a narrow fan; the bow held drawn-ready after, for the art.</summary>
        TwinDraw,

        /// <summary>
        /// DISENGAGE (2026-10-18): out of the RECOIL, a gather and a BACKFLIP off the ground away
        /// from the target, drawing through the turn; upright at the top, the arrow is loosed down
        /// at the target (Tuning.Disengage.LooseAt), then the fall into the recoil stance.
        /// PlayerController.DisengageStrike moves the body (a real dash).
        /// </summary>
        FlipDraw,
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
