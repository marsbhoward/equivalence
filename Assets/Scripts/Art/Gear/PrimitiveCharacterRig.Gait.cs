using UnityEngine;

namespace Convergence.Art.Gear
{
    // --------------------------------------------------------------- the walk
    //
    // A WALK. A run was tried here and didn't pan out; the walk before it was one sine for
    // everything - legs swinging as straight stilts, straight-stick arms, every part at its
    // extreme on the same frame - and read as a metronome. This keeps what the run brought that
    // wasn't the running: the knees, feet planted on the floor, a stance that slides at a
    // constant rate, and parts that peak at different moments.
    //
    // ONE CLOCK, every part reading its own point on it. _stride is the phase, in radians, wrapped;
    // `cycle` below is the same thing as 0..1. Cycle 0 is the FRONT leg's footfall, 0.5 the back
    // leg's. Each curve is a function of the cycle alone, so the pose at a given moment of the
    // stride is the same whatever the speed, frame rate or history - nothing here integrates.
    //
    // What makes it a walk rather than a run: each foot is on the ground MORE than half its
    // cycle, so there is a moment in every step with BOTH feet down and never one with neither;
    // the body vaults OVER the planted leg, highest at mid-stance and lowest as the weight passes
    // from one foot to the other (a run is the reverse); the swinging foot only clears the floor.
    //
    // And a TACTICAL walk - a warrior walking into a battle, not a stroll. The arms barely sway
    // (a loose arm swing read as silly), held a little bent and ready; the knees stay soft; the
    // chest leans into the walk; the head rides steady on top.
    //
    // The body is drawn nearly FACE-ON, with the hips side by side. A leg swinging forward
    // therefore moves SIDEWAYS on screen, and a wide swing reads as a jumping-jack scissor (legs
    // splayed into an A, then crossed into an X) rather than a stride. That is why the stride here
    // is narrow: the walk reads from the KNEES and the body, not from how far the legs fan out.
    //
    // The legs are posed by their FEET. Each foot has a target on the clock - planted on the
    // floor and sliding back through the stance, lifted in an arc through the swing - and a
    // two-bone reach (SolveLeg) bends hip and knee to put it there. So the planted foot stays on
    // the floor however the body bobs, and the knee bends exactly as far as that takes; nothing
    // here keys a knee angle by hand. The knees are what keep it from reading as stilts: they
    // fold as the swinging foot lifts, and give a little as the weight lands on each footfall.
    public partial class PrimitiveCharacterRig
    {
        /// <summary>Stride cycles a second at the reference speed - two footfalls each.</summary>
        const float WalkStrideHz = 1.7f;

        /// <summary>
        /// How cadence grows with speed. Under 1 because going faster mostly lengthens the stride
        /// and only partly quickens it; at 1 a haste buff turned the legs into a blur, and a sand
        /// trap slowed them into a moonwalk.
        /// </summary>
        const float WalkCadenceExponent = 0.5f;

        /// <summary>
        /// Share of each leg's cycle spent on the ground. OVER half, so each footfall lands before
        /// the other foot leaves - the double support that makes it a walk.
        /// </summary>
        const float WalkStanceFraction = 0.6f;

        /// <summary>
        /// How far each foot travels either side of its hip through the stance, world units. The
        /// swing overshoots it a little. Narrow on purpose - see the note at the top.
        /// </summary>
        const float WalkStride = 0.06f;

        /// <summary>
        /// How high the swinging foot lifts, world units. Enough to clear the floor and fold the
        /// knee; a run's lift, kicking the heel up behind, is what this replaced.
        /// </summary>
        const float WalkFootLift = 0.035f;

        /// <summary>
        /// How far the foot sinks on screen as it reaches forward, world units, and rises as it
        /// goes back. The figure faces partly TOWARD the camera, so forward is partly nearer, and
        /// nearer on this floor is lower down the screen; drawn level, the feet read as sliding
        /// along a line instead of stepping through depth.
        /// </summary>
        const float WalkFootDepth = 0.01f;

        /// <summary>
        /// How far the hips sit lower for the whole walk, world units. Soft, ready knees - about 15
        /// degrees at mid-stance. Sensitive: the bend grows as the square root of this, and at
        /// 0.007 the stance knee sat at 25-45 degrees, a squat.
        /// </summary>
        const float WalkCrouch = 0.0015f;

        /// <summary>
        /// The overhead fallback's leg angle at the ends of the stance, degrees. TopDown has no
        /// knees to bend - its legs are capsules - so it keeps rotating the whole leg.
        /// </summary>
        const float WalkLegDegrees = 14f;

        /// <summary>
        /// How far the body drops as the weight passes between the feet, world units - small, so
        /// the walk reads grounded rather than bouncing. Highest at
        /// mid-stance, over the planted leg; lowest just after each footfall, where both feet are
        /// down and the knees take the landing.
        /// </summary>
        const float WalkBob = 0.002f;

        /// <summary>
        /// Shoulder swing either side, degrees. Barely any: a warrior's arms stay by the body,
        /// ready. At 16 the loose swing read as silly.
        /// </summary>
        const float WalkArmDegrees = 1f;

        /// <summary>
        /// How far the arms trail the legs, in cycles. Parts that peak on the same frame are what
        /// make a figure look driven rather than alive; what little the arms swing, they are
        /// carried by the body, so they arrive a beat late.
        /// </summary>
        const float WalkArmLagCycles = 0.06f;

        /// <summary>
        /// Share of the shoulder swing kept while both fists are on a two-handed hilt. Two hands
        /// on one sword cannot swing; the blade rocks a little with the stride instead. The carry
        /// lets go of the grip, and the free arm takes the whole swing again.
        /// </summary>
        const float WalkHiltSwingShare = 0.2f;

        /// <summary>
        /// The elbow's bend while walking, degrees - held a little bent and ready, not hanging
        /// slack and not a runner's pump. Positive brings the forearm FORWARD in the rig's frame.
        /// </summary>
        const float WalkElbowDegrees = 18f;

        /// <summary>
        /// Extra bend at the front of the arm's swing, less at the back. Small, like the swing.
        /// </summary>
        const float WalkElbowPumpDegrees = 3f;

        /// <summary>
        /// How much of the walk's elbow a hand holding a one-handed weapon takes.
        /// </summary>
        const float WalkWeaponHandShare = 0.5f;

        /// <summary>How far the head trails the torso's bob, in cycles.</summary>
        const float WalkHeadLagCycles = 0.06f;

        /// <summary>
        /// How much of the trailing bob the head takes. Low - the head rides steady, eyes on
        /// what's ahead. Kept well inside NeckOverlap (four body texels) either way - the head
        /// moving UP against the torso is a neck appearing.
        /// </summary>
        const float WalkHeadLagShare = 0.3f;

        /// <summary>
        /// Steady lean into a full-speed walk, degrees - chest forward, pressing in. The
        /// weight-shift spring only answers a CHANGE of speed; this is the lean that holds while
        /// the speed does.
        /// </summary>
        const float WalkLeanDegrees = 4f;

        /// <summary>Share of the walk lean kept, tipped BACK, while backpedalling.</summary>
        const float WalkBackpedalLeanShare = 0.4f;

        const float WalkLeanSharpness = 6f;

        /// <summary>
        /// Share of the reference speed at which the walk is at full size. A character easing to
        /// a stop or wading through sand takes shorter steps rather than full strides in slow
        /// motion.
        /// </summary>
        const float WalkFullSpeedFraction = 0.5f;

        // How quickly the walk grows and fades, and how quickly the stride clock changes pace.
        // The clock is eased rather than set so a stop lets the legs FINISH the step they are in
        // while it shrinks - set from the speed, they froze mid-stride and folded straight.
        const float WalkGrowSharpness = 10f;
        const float WalkFadeSharpness = 7f;
        const float WalkRateSharpness = 12f;

        /// <summary>
        /// Share of the speed that has to be AWAY from the facing before the stride runs
        /// backwards. Moving straight up or down the screen still walks forward.
        /// </summary>
        const float WalkBackpedalThreshold = 0.35f;

        // Plain floats, so a domain reload restores all of them.
        float _gaitWeight;   // 0..1, eased: how big the walk is
        float _strideRate;   // radians of _stride per second, SIGNED - negative backpedals
        float _walkLean;      // degrees, eased; the steady lean, kept apart from the lean spring

        /// <summary>The leg's length, hip to sole, in the rig's own units.</summary>
        static float LegLength => PixelSprite.Px(Proportions.Cells(Proportions.LegH));

        /// <summary>
        /// The size the walk is heading for at this speed, 0..1 - what <see cref="_gaitWeight"/>
        /// eases toward. The march carry reads this directly so it doesn't inherit the ease.
        /// </summary>
        float GaitWeightWanted(float speed)
        {
            float reference = Player != null ? Mathf.Max(0.1f, Player.MoveSpeed)
                                             : Convergence.Core.Tuning.Player.MoveSpeed;
            float relative = Mathf.Clamp(speed / reference, 0f, 2.5f);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(relative / WalkFullSpeedFraction));
        }

        /// <summary>
        /// Advance the stride clock and the walk's size. Called once a frame, before anything reads
        /// <see cref="_stride"/>.
        /// </summary>
        void AdvanceGait(float dt, float speed)
        {
            float reference = Player != null ? Mathf.Max(0.1f, Player.MoveSpeed)
                                             : Convergence.Core.Tuning.Player.MoveSpeed;
            float relative = Mathf.Clamp(speed / reference, 0f, 2.5f);

            float want = GaitWeightWanted(speed);
            float sharp = want > _gaitWeight ? WalkGrowSharpness : WalkFadeSharpness;
            _gaitWeight = Mathf.Lerp(_gaitWeight, want, 1f - Mathf.Exp(-sharp * dt));

            // Into the frame the rig is drawn in, as the lean and the cape measure it. Last
            // frame's facing - FaceAim runs later - which is a frame nobody can see.
            float forward = Body != null ? Body.linearVelocity.x : 0f;
            if (_facingLeft) forward = -forward;
            float direction = forward < -WalkBackpedalThreshold * speed ? -1f : 1f;

            float rate = direction * Mathf.PI * 2f * WalkStrideHz * Mathf.Pow(relative, WalkCadenceExponent);
            _strideRate = Mathf.Lerp(_strideRate, rate, 1f - Mathf.Exp(-WalkRateSharpness * dt));

            // Wrapped, so the phase never loses precision over a long session.
            _stride = Mathf.Repeat(_stride + _strideRate * dt, Mathf.PI * 2f);

            float lean = forward >= 0f
                ? -Mathf.Clamp01(forward / reference) * WalkLeanDegrees
                : Mathf.Clamp01(-forward / reference) * WalkLeanDegrees * WalkBackpedalLeanShare;
            _walkLean = Mathf.Lerp(_walkLean, lean * _gaitWeight, 1f - Mathf.Exp(-WalkLeanSharpness * dt));
        }

        /// <summary>
        /// One leg's travel through its own cycle, -1 (behind) .. +1 (ahead), overshooting both
        /// slightly. Cycle 0 is its footfall.
        ///
        /// STANCE runs straight from +1 to -1 at a constant rate: the foot is on the ground and
        /// the ground moves under it at a constant speed. The SWING is a Hermite curve whose ends
        /// leave and arrive at exactly that same rate, so there is no hitch at either end - the
        /// leg carries on back after toe-off (the follow-through), whips forward, reaches past
        /// the footfall and is pulled back into it. A sine has neither part: it spends as long
        /// bringing the leg forward as pushing it back, and stops dead at both ends.
        /// </summary>
        static float GaitLegTravel(float cycle)
        {
            float p = cycle - Mathf.Floor(cycle);
            const float s = WalkStanceFraction;
            if (p < s) return 1f - 2f * p / s;

            float u = (p - s) / (1f - s);
            float m = -2f * (1f - s) / s;   // the stance's rate, in swing time
            float u2 = u * u, u3 = u2 * u;
            return -(2f * u3 - 3f * u2 + 1f)
                   + (u3 - 2f * u2 + u) * m
                   + (-2f * u3 + 3f * u2)
                   + (u3 - u2) * m;
        }

        /// <summary>
        /// The torso's drop, world units, at most <see cref="WalkBob"/>. Twice a cycle - once per
        /// step - highest at mid-stance, vaulting over the planted leg, and lowest half a step
        /// later, in the double support just after the other foot lands.
        /// </summary>
        static float GaitBob(float cycle)
        {
            float high = WalkStanceFraction * 0.5f;
            return -WalkBob * 0.5f * (1f - Mathf.Cos(4f * Mathf.PI * (cycle - high)));
        }

        /// <summary>The head's offset against the torso: where the bob was a moment ago.</summary>
        static float GaitHeadLag(float cycle) =>
            (GaitBob(cycle - WalkHeadLagCycles) - GaitBob(cycle)) * WalkHeadLagShare;

        /// <summary>
        /// The front arm's swing, -1 (back) .. +1 (forward). Opposite the front leg, a beat late:
        /// fully back on the front leg's footfall, when that leg is furthest forward.
        /// </summary>
        static float GaitArmTravel(float cycle) =>
            -Mathf.Cos(2f * Mathf.PI * (cycle - WalkArmLagCycles));

        /// <summary>
        /// How high a foot is lifted at this point in its own cycle, world units: nothing through
        /// the stance, an arc through the swing that peaks a little early - the knee folds as the
        /// toe leaves the ground, then the shin swings through and the leg reaches out straight
        /// for the next footfall.
        /// </summary>
        static float GaitFootLift(float cycle)
        {
            float p = cycle - Mathf.Floor(cycle);
            const float s = WalkStanceFraction;
            if (p < s) return 0f;
            float u = (p - s) / (1f - s);
            return WalkFootLift * Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.8f));
        }

        /// <summary>
        /// Where the body sits this frame, against rest, world units (negative is down): the bob
        /// on top of the walk's soft knees. Torso and hips both take it, so the waist never stretches.
        /// </summary>
        float GaitBody(float cycle) => (GaitBob(cycle) - WalkCrouch) * _gaitWeight;

        /// <summary>
        /// Pose both legs for this frame, given where the hips' pivot now sits. Every other leg
        /// write is gone - the hips moving is what bends the knees.
        /// </summary>
        void PoseLegs(float cycle, float hipsY)
        {
            EnsureKnees();
            PoseLeg(_legFront, _kneeFront, cycle, hipsY);
            PoseLeg(_legBack, _kneeBack, cycle + 0.5f, hipsY);
        }

        void PoseLeg(Transform hip, Transform knee, float cycle, float hipsY)
        {
            if (hip == null) return;
            float travel = GaitLegTravel(cycle) * _gaitWeight;

            if (TopDown || knee == null)
            {
                hip.localRotation = Quaternion.Euler(0f, 0f, travel * WalkLegDegrees);
                if (knee != null) knee.localRotation = Quaternion.identity;
                return;
            }

            // The foot's target against the hip joint, in the rig's own frame. The joint sits on
            // the hips' pivot line, so its height is the hips'.
            float sole = HipsRest.y - LegLength;
            float footY = sole + GaitFootLift(cycle) * _gaitWeight - travel * WalkFootDepth;
            SolveLeg(travel * WalkStride, footY - hipsY, out float thigh, out float bend);

            hip.localRotation = Quaternion.Euler(0f, 0f, thigh);
            knee.localRotation = Quaternion.Euler(0f, 0f, bend);
        }

        /// <summary>
        /// Two-bone reach: the hip and knee angles, degrees, that put the foot at (dx, dy) from the
        /// hip joint. The knee always bends FORWARD (+X in the rig's frame, which the mirror turns
        /// to face the aim), so the thigh rotates past the line to the foot and the shin folds back.
        ///
        /// Out of reach the leg points straight at the target. That includes a character standing
        /// still: the hips at rest are exactly a leg's length above the sole, so the margin below
        /// is what makes a standing leg EXACTLY straight rather than a hair bent - a fraction of a
        /// degree on a point-filtered sprite is a texel that shimmers. Kept tiny: the bend grows
        /// as the square root of the shortfall, so a margin of half a millimetre already popped the
        /// knee seven degrees as the leg crossed it.
        /// </summary>
        static void SolveLeg(float dx, float dy, out float thighDegrees, out float kneeDegrees)
        {
            float thighLength = -KneeRest.y;
            float shinLength = LegLength - thighLength;
            float reach = thighLength + shinLength;

            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float toFoot = Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg;   // 0 straight down, + forward
            if (d >= reach - 0.000002f || d < 0.0001f)
            {
                thighDegrees = Mathf.Abs(dx) < 0.00001f ? 0f : toFoot;
                kneeDegrees = 0f;
                return;
            }

            float atHip = Mathf.Acos(Mathf.Clamp(
                (thighLength * thighLength + d * d - shinLength * shinLength) / (2f * thighLength * d),
                -1f, 1f)) * Mathf.Rad2Deg;
            float atKnee = Mathf.Acos(Mathf.Clamp(
                (thighLength * thighLength + shinLength * shinLength - d * d) / (2f * thighLength * shinLength),
                -1f, 1f)) * Mathf.Rad2Deg;

            thighDegrees = toFoot + atHip;
            kneeDegrees = -(180f - atKnee);
        }

        /// <summary>
        /// The walk's elbow for an arm whose swing is at <paramref name="travel"/> (-1 back .. +1
        /// forward), before the arm's own share.
        /// </summary>
        float GaitElbowDegrees(float travel) =>
            (WalkElbowDegrees + WalkElbowPumpDegrees * travel) * _gaitWeight;

        /// <summary>
        /// How much of the walk's elbow each arm takes: all of it for an empty hand, part of it
        /// for a hand holding a one-handed weapon, none for a hand on a two-handed grip, the
        /// carrying arm, or the bow arm. Zero outright while a swing or charge is live (the arcs
        /// are tuned for a straight arm), easing back in as the lean suppression lets go.
        /// </summary>
        void GaitArmShares(bool carrying, bool carryBack, out float front, out float back)
        {
            if (_attackTimer > 0f || _chargeTimer > 0f)
            {
                front = back = 0f;
                return;
            }

            float free = 1f - _leanSuppress;
            bool empty = HandsEmpty;

            if (carrying)
            {
                // Only the arm the carry leaves free, and only once the grip has opened.
                front = carryBack ? _gripBlend : 0f;
                back = carryBack ? 0f : _gripBlend;
            }
            else
            {
                front = HeldBow || empty ? 1f : WalkWeaponHandShare;
                back = HeldBow ? 0f : _split && !empty ? WalkWeaponHandShare : 1f;
            }

            front *= free;
            back *= free;
        }
    }
}
