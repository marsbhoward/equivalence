using UnityEngine;
using Convergence.Combat;

namespace Convergence.Art.Gear
{
    // --------------------------------------------------------- the whole body in a swing
    //
    // The arms carry the blade; this is everything else. Before it, a swing was the arms alone:
    // the legs never left the walk cycle, and the motion's "body english" (bodySpin) turned the
    // whole figure about its FEET, so a strike tipped the character like a plank. Measured against
    // the user's reference (2026-10-14, an idle-game swordsman), every strike there is the whole
    // body: it sinks into a loaded stance, drives forward into a deep split stance with the front
    // foot stepping in, holds that pose through the follow-through, and rises out of it.
    //
    // So a swing now also poses the body, on the same clock and the same three beats as the arc:
    //
    //   LOAD    the cock: the hips sink a little, the feet open, weight back, chest back
    //   STRIKE  the snap: the hips drop into a deep split stance, the front foot steps in, the
    //           chest leans into it - all landing a beat after the blade starts, eased out
    //   SETTLE  the follow-through: the pose held, easing up a touch
    //
    // and the motion's own bodySpin turns the TORSO at the waist instead of the whole figure, so
    // the feet stay planted. Every arc, grip and hand-off is untouched: the arms ride the torso as
    // they always did, and these channels never write an arm.
    //
    // The step is DRAWING ONLY (the user's call): the hitbox, camera and shadow stay where the
    // player is, as with the hop and the reach shift. The back foot is held where it stood, so a
    // step reads as the front foot going rather than the figure sliding.
    public partial class PrimitiveCharacterRig
    {
        /// <summary>What the legs, hips and waist do on one frame of a swing.</summary>
        struct SwingBody
        {
            /// <summary>How far the hips - and the torso with them - drop, in rig units. The feet
            /// stay where they are, so the knees take it.</summary>
            public float Crouch;

            /// <summary>How far each foot moves off its rest spot along the facing, in rig units:
            /// the front foot forward, the back foot behind - the split stance.</summary>
            public float Stance;

            /// <summary>How far the whole figure travels along the facing, drawing only, in rig
            /// units. The back foot is held where it stood.</summary>
            public float Step;

            /// <summary>Degrees the torso leans at the waist on top of the motion's own turn.
            /// NEGATIVE leans into the facing (the rig's clockwise).</summary>
            public float Lean;

            public SwingBody(float crouch, float stance, float step, float lean)
            {
                Crouch = crouch; Stance = stance; Step = step; Lean = lean;
            }

            public static SwingBody Lerp(SwingBody a, SwingBody b, float t) => new(
                Mathf.LerpUnclamped(a.Crouch, b.Crouch, t),
                Mathf.LerpUnclamped(a.Stance, b.Stance, t),
                Mathf.LerpUnclamped(a.Step, b.Step, t),
                Mathf.LerpUnclamped(a.Lean, b.Lean, t));

            public static SwingBody operator *(SwingBody a, float s) =>
                new(a.Crouch * s, a.Stance * s, a.Step * s, a.Lean * s);

            public static SwingBody operator +(SwingBody a, SwingBody b) =>
                new(a.Crouch + b.Crouch, a.Stance + b.Stance, a.Step + b.Step, a.Lean + b.Lean);
        }

        /// <summary>The body this frame - written by the swing branch, eased home by the relax
        /// branch, exactly like the arm pose fields beside it.</summary>
        SwingBody _poseBody;

        /// <summary>The body when the current swing started; blended out of over the entry
        /// window, as the arms are.</summary>
        SwingBody _entryBody;

        /// <summary>Last frame's drawn step, for the cape's kick (see AnimateCape).</summary>
        float _lastBodyStep;

        /// <summary>The step's speed this frame, rig units a second. The cape is driven by the
        /// body's velocity, and a drawing-only step has none of its own.</summary>
        float _bodyStepVelocity;

        // ---- the beats, as whole-body poses ----
        //
        // Sized against the body, not by eye alone: the leg is 0.32 rig units (48 body texels), so
        // the strike's 0.068 crouch bends a planted knee about 70 degrees with the feet split, and
        // its stance puts the feet ~0.26 apart against shoulders ~0.17 - the reference's A. A
        // first pass at about three quarters of these read as barely there at play zoom.
        // The chop: weight back on the cock, then down and forward through the strike.
        static SwingBody ChopLoad => new(0.028f, 0.040f, -0.015f, 6f);
        static SwingBody ChopStrike => new(0.068f, 0.078f, 0.065f, -12f);
        static SwingBody ChopSettle => new(0.052f, 0.070f, 0.056f, -8f);

        // The rising cut: the hips DIP into the load (RiseCock's own "dip into the hips"), the
        // body comes UP through the cut, and it ends coiled back - weight on the rear foot, chest
        // open - which is the chop's own load, so the finisher after it drives straight out of it.
        static SwingBody RiseLoad => new(0.040f, 0.055f, 0.040f, -4f);
        static SwingBody RiseStrike => new(0.006f, 0.062f, 0.050f, 6f);
        static SwingBody RiseSettle => new(0.026f, 0.052f, 0.024f, 8f);

        // The rest, on each motion's own timing (2026-10-14, phase 3 - every motion has a body).
        // The low drawing cut: pressed in, then dragged back low through the target as the torso
        // turns away - ending wound up and LOW, which the spins, the whirl and the throw unwind from.
        static SwingBody WindLoad => new(0.020f, 0.040f, 0.020f, -4f);
        static SwingBody WindStrike => new(0.058f, 0.072f, -0.010f, 6f);
        static SwingBody WindSettle => new(0.052f, 0.068f, -0.006f, 7f);

        // The wide horizontal arc (and its reverse, the backhand): the shoulders carry it round,
        // the weight going in under it. No cock of its own - the arc is one smoothstep, fastest at
        // its middle - so the beats are cut on the arc's own speed (SweepCockK / SweepStrikeK).
        static SwingBody SweepLoad => new(0.020f, 0.035f, 0f, 3f);
        static SwingBody SweepStrike => new(0.052f, 0.066f, 0.046f, -7f);
        static SwingBody SweepSettle => new(0.040f, 0.060f, 0.040f, -4f);
        const float SweepCockK = 0.15f, SweepStrikeK = 0.70f;

        // The thrust: the arm extends to full at the swing's middle and comes home - the body
        // drives out in a long lunge with it and gives some back as the blade returns.
        static SwingBody ThrustLoad => new(0.015f, 0.030f, -0.010f, 3f);
        static SwingBody ThrustStrike => new(0.042f, 0.082f, 0.070f, -8f);
        static SwingBody ThrustSettle => new(0.025f, 0.052f, 0.022f, -2f);
        const float ThrustCockK = 0.10f, ThrustStrikeK = 0.50f;

        // The jab: the thrust's shape at a basic's size - a poke, not a lunge.
        static SwingBody JabLoad => new(0.010f, 0.022f, -0.006f, 2f);
        static SwingBody JabStrike => new(0.026f, 0.050f, 0.040f, -5f);
        static SwingBody JabSettle => new(0.014f, 0.032f, 0.012f, -1f);

        // The old Impale (a standing heavy thrust): coiled back into the chamber, then the
        // deepest lunge in the set, the skewer held.
        static SwingBody ImpaleLoad => new(0.030f, 0.050f, -0.020f, 5f);
        static SwingBody ImpaleStrike => new(0.062f, 0.090f, 0.080f, -10f);
        static SwingBody ImpaleSettle => new(0.046f, 0.072f, 0.050f, -5f);

        // The Impale art: the player really dashes (AttackStep.LungeDistance), so the drawn step
        // is small - the body arrives DOWN in the lunge, out of the held chamber (HoldLoad).
        static SwingBody LungeStrike => new(0.066f, 0.100f, 0.030f, -12f);
        static SwingBody LungeSettle => new(0.050f, 0.080f, 0.020f, -6f);

        // The full revolution: low through the whole turn - the root carries the spin, so no
        // lean (a lean would orbit with it).
        static SwingBody SpinLoad => new(0.030f, 0.050f, 0f, 0f);
        static SwingBody SpinStrike => new(0.050f, 0.070f, 0f, 0f);
        static SwingBody SpinSettle => new(0.034f, 0.056f, 0f, 0f);

        // The whirl: whip the blade out (a lunge with the release), stand braced while it circles,
        // ease up for the catch.
        static SwingBody WhirlLoad => new(0.020f, 0.040f, -0.010f, 4f);
        static SwingBody WhirlStrike => new(0.044f, 0.072f, 0.040f, -8f);
        static SwingBody WhirlSettle => new(0.022f, 0.056f, 0.010f, 0f);

        // The slam out of a charge: from the charge's own loaded stance (HoldLoad) the body drives
        // down after the blade - the deepest crouch here - and stays down.
        static SwingBody SlamStrike => new(0.080f, 0.086f, 0.060f, -14f);
        static SwingBody SlamSettle => new(0.070f, 0.080f, 0.054f, -10f);
        const float SlamStrikeK = 0.74f;   // Animate's drive reaches 1 at k = 1 / 1.35

        // The plant: up TALL on the raise (a negative crouch), then all the way down with the
        // drive, and held there - the blade stands in the ground.
        static SwingBody PlantLoad => new(-0.010f, 0.028f, 0f, 3f);
        static SwingBody PlantStrike => new(0.085f, 0.076f, 0.030f, -10f);
        static SwingBody PlantSettle => new(0.080f, 0.076f, 0.030f, -9f);
        const float PlantCockK = 0.62f, PlantStrikeK = 0.85f;   // Animate's RaiseEnd / DriveEnd

        // The throw: wound back behind the ear, then thrown - the body follows the arm through.
        static SwingBody ThrowLoad => new(0.024f, 0.040f, -0.016f, 5f);
        static SwingBody ThrowStrike => new(0.050f, 0.080f, 0.060f, -12f);
        static SwingBody ThrowSettle => new(0.034f, 0.064f, 0.044f, -6f);
        const float ThrowCockK = 0.08f, ThrowStrikeK = 0.62f;   // Animate's release at k = 1 / 1.6

        // The bow's draw: a dip for the vial, then the archer's stance - wide, a little low, the
        // chest back against the pull - held to the release.
        static SwingBody DrawLoad => new(0.012f, 0.040f, 0f, 0f);
        static SwingBody DrawStrike => new(0.026f, 0.066f, 0f, 4f);
        const float DrawCockK = 0.30f;   // Animate's ReachEnd

        /// <summary>The bow's KNEEL connector: hips right down, the feet split wide - the back knee
        /// nearly on the ground (BowKneelRoll ends here, KneelDraw holds it).</summary>
        static SwingBody BowKneel => new(0.125f, 0.105f, 0f, -3f);
        /// <summary>The CANTED shot's sidearm stance - lower, leaning in.</summary>
        static SwingBody CantLean => new(0.03f, 0.02f, 0f, -6f);

        /// <summary>The lean back under the HIGH pair's raised bow.</summary>
        static SwingBody LobLean => new(0f, 0f, 0f, 7f);

        /// <summary>The bow's RECOIL connector: weight on the back foot, a wide stance, leaning away
        /// from the target (BowRecoilHop lands here, RecoilDraw holds it).</summary>
        static SwingBody BowRecoil => new(0.05f, 0.085f, 0f, 9f);
        /// <summary>DISENGAGE's lift off the ground at <paramref name="k"/> of the art, drawing only:
        /// up through the flip, held at the top for the loose, then dropped (quadratic) to land.</summary>
        float _flipLooseK = -1f;   // where in the art the arrow was loosed; -1 not yet

        float FlipLift(float k)
        {
            if (k <= Core.Tuning.Disengage.TakeoffAt || _swingHop <= 0f) return 0f;
            if (k < Core.Tuning.Disengage.FlipEnd)
                return _swingHop * Mathf.Sin((k - Core.Tuning.Disengage.TakeoffAt) / (Core.Tuning.Disengage.FlipEnd - Core.Tuning.Disengage.TakeoffAt) * Mathf.PI * 0.5f);
            // Held at the top until the arrow is actually loosed (the gameplay's clock can run a
            // frame or two behind the drawing's - the fall began before the loose), then dropped.
            if (k < Core.Tuning.Disengage.LooseAt || (_flipLooseK < 0f && _awaitLoose)) return _swingHop;
            float from = Mathf.Max(Core.Tuning.Disengage.LooseAt, _flipLooseK);
            float u = (k - from) / (Core.Tuning.Disengage.LandAt - Core.Tuning.Disengage.LooseAt);
            return u >= 1f ? 0f : _swingHop * (1f - u * u);
        }

        /// <summary>DISENGAGE's backflip: one full turn of the figure, from takeoff to the top.</summary>
        static float FlipDegrees(float k)
        {
            return 360f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - Core.Tuning.Disengage.TakeoffAt) / (Core.Tuning.Disengage.FlipEnd - Core.Tuning.Disengage.TakeoffAt)));
        }

        /// <summary>The share of the recoil hop spent in the air.</summary>
        const float RecoilAirEnd = 0.45f;
        /// <summary>The bow arm's angle at the recoil - pulled in, the bow close and low.</summary>
        const float BowRecoilIn = 22f;

        /// <summary>The kneeling draw's lean into the pull, on top of <see cref="BowKneel"/>.</summary>
        static SwingBody DrawLean => new(0.008f, 0f, 0f, 4f);
        /// <summary>The share of the roll spent turning; the rest settles onto the knee.</summary>
        const float BowRollTurnEnd = 0.75f;
        /// <summary>The bow arm's angle at the kneel's ready - between the carry and the draw's 58.</summary>
        const float BowKneelReady = 48f;

        // The Flurry on its own clock (Tuning.Flurry): down on the first cut, up through the rising
        // one, coiled back as the hilt turns, then the hardest drive in the set behind the pommel.
        static readonly float[] FlurryBodyTimes = { 0f, 0.07f, 0.14f, 0.20f, 0.32f, 0.40f, 0.62f };
        static SwingBody FlurryBodyAt(int i) => i switch
        {
            0 => default,
            1 => new(0.050f, 0.060f, 0.030f, -8f),
            2 => new(0.032f, 0.056f, 0.030f, -2f),
            3 => new(0.014f, 0.060f, 0.040f, 4f),
            4 => new(0.030f, 0.062f, 0.020f, 6f),
            5 => new(0.066f, 0.086f, 0.070f, -12f),
            _ => new(0.050f, 0.076f, 0.060f, -8f),
        };

        /// <summary>The loaded stance a held wind-up sinks into as its bar fills - the timing
        /// bar's hold and a charge. Added to the motion's own frame-zero body.</summary>
        static SwingBody HoldLoad => DevArmsOnly ? default : new(0.030f, 0.045f, -0.012f, 5f);

        /// <summary>Share of the swing's lean the head gives back, so the face stays nearer
        /// level while the chest drives - the reference's head rides almost still.</summary>
        const float BodyLeanHeadShare = 0.5f;

        /// <summary>How far the legs' half of the body (stance, step) gives way to the walk while
        /// moving: the feet are busy striding, so the upper body carries the swing alone. 1 =
        /// none of the stance while walking at full stride.</summary>
        const float BodyStanceWalkGive = 1f;

        /// <summary>How hard the drawn step kicks the cape: the step's speed counts as this many
        /// times its share of the run speed the cape is tuned against.</summary>
        const float CapeStepGain = 4f;

        /// <summary>
        /// Testing: the swing as it was before the body joined it - arms only, the motion's turn
        /// on the whole figure about its feet. For comparing the two in play:
        /// <c>Convergence.Art.Gear.PrimitiveCharacterRig.DevArmsOnly = true</c>.
        /// </summary>
        public static bool DevArmsOnly;

        /// <summary>
        /// The body for <paramref name="motion"/> at <paramref name="k"/> (0..1 through the swing),
        /// on the motion's own phase timing - the same boundaries the facing lock reads
        /// (StrikePhases), so the body lands as the strike is drawn.
        /// </summary>
        static SwingBody BodyFor(AttackMotion motion, float k, float seconds)
        {
            if (DevArmsOnly) return default;
            float cockEnd, strikeEnd;
            switch (motion)
            {
                case AttackMotion.Chop:
                    ChopPhases(seconds, out cockEnd, out strikeEnd);
                    return ThreeBeats(k, cockEnd, strikeEnd, ChopLoad, ChopStrike, ChopSettle);
                case AttackMotion.Rise:
                    RisePhases(seconds, out cockEnd, out strikeEnd);
                    return ThreeBeats(k, cockEnd, strikeEnd, RiseLoad, RiseStrike, RiseSettle);
                case AttackMotion.Wind:
                    WindPhases(seconds, out cockEnd, out strikeEnd);
                    return ThreeBeats(k, cockEnd, strikeEnd, WindLoad, WindStrike, WindSettle);
                case AttackMotion.Sweep:
                    return ThreeBeats(k, SweepCockK, SweepStrikeK, SweepLoad, SweepStrike, SweepSettle);
                case AttackMotion.Thrust:
                    return ThreeBeats(k, ThrustCockK, ThrustStrikeK, ThrustLoad, ThrustStrike, ThrustSettle);
                case AttackMotion.Jab:
                    return ThreeBeats(k, ThrustCockK, ThrustStrikeK, JabLoad, JabStrike, JabSettle);
                case AttackMotion.Impale:
                    ImpalePhases(seconds, out cockEnd, out strikeEnd);
                    return ThreeBeats(k, cockEnd, strikeEnd, ImpaleLoad, ImpaleStrike, ImpaleSettle);
                case AttackMotion.Lunge:
                    // The draw before frame zero is the timing bar's hold, which sinks into
                    // HoldLoad on its own; the thrust starts loaded from there.
                    if (k < 0f) return default;
                    return ThreeBeats(k, 0f, LungeStrikeK(seconds), HoldLoad, LungeStrike, LungeSettle);
                case AttackMotion.Spin:
                    return ThreeBeats(k, 0.08f, 0.85f, SpinLoad, SpinStrike, SpinSettle);
                case AttackMotion.WhirlThrow:
                    return ThreeBeats(k, 0.05f, 0.22f, WhirlLoad, WhirlStrike, WhirlSettle);
                case AttackMotion.Slam:
                    return ThreeBeats(k, 0f, SlamStrikeK, HoldLoad, SlamStrike, SlamSettle);
                case AttackMotion.Plant:
                    return ThreeBeats(k, PlantCockK, PlantStrikeK, PlantLoad, PlantStrike, PlantSettle);
                case AttackMotion.Throw:
                    return ThreeBeats(k, ThrowCockK, ThrowStrikeK, ThrowLoad, ThrowStrike, ThrowSettle);
                case AttackMotion.Cast:
                {
                    // Wanderblade's cast starts where the Wind lead-in left the BODY (read off
                    // Wind, as its arm is): Throw's first frame stands upright, so the gather
                    // straightened out of Wind's crouch and sank again - the stutter. The hold sinks
                    // deeper from Wind's settle, and the throw drives out of that loaded stance.
                    var wound = BodyFor(AttackMotion.Wind, 1f, seconds);
                    if (k <= 0f) return wound;
                    return ThreeBeats(k, 0f, ThrowStrikeK, wound + HoldLoad, ThrowStrike, ThrowSettle);
                }
                case AttackMotion.Draw:
                case AttackMotion.HighDraw:
                case AttackMotion.TwinDraw:
                    return ThreeBeats(k, DrawCockK, 1f, DrawLoad, DrawStrike, DrawStrike);
                case AttackMotion.SnapDraw:
                    return BodyFor(AttackMotion.Draw, Mathf.Min(1f, k / Core.Tuning.Bow.SnapRelease), seconds);
                case AttackMotion.CantedDraw:
                    // Sidearm: a little lower, leaning into the shot.
                    return ThreeBeats(k, DrawCockK, 1f, DrawLoad + CantLean, DrawStrike + CantLean, DrawStrike + CantLean);
                case AttackMotion.LobDraw:
                    // Leaning back under a bow raised to the sky.
                    return ThreeBeats(k, DrawCockK, 1f, DrawLoad, DrawStrike + LobLean, DrawStrike + LobLean);
                case AttackMotion.KneelDraw:
                    // Down on the knee from the first frame (the roll left it there) to the release.
                    return BowKneel + ThreeBeats(k, DrawCockK, 1f, default, DrawLean, DrawLean);
                case AttackMotion.RecoilDraw:
                    // Weight on the back foot from the first frame (the hop left it there).
                    return BowRecoil + ThreeBeats(k, DrawCockK, 1f, default, DrawLean, DrawLean);
                case AttackMotion.FlipDraw:
                {
                    // Disengage: a deep gather, TUCKED through the flip (knees up - the crouch
                    // with the feet lifted under the lifted hips), stretched to stand at the top for
                    // the loose, and down into the recoil on the landing.
                    var gather = new SwingBody(0.07f, 0.03f, 0f, 4f);
                    var tuck = new SwingBody(0.06f, 0f, 0f, 0f);
                    var top = BowRecoil * 0.4f;
                    if (k < Core.Tuning.Disengage.TakeoffAt) return SwingBody.Lerp(default, gather, Mathf.SmoothStep(0f, 1f, k / Core.Tuning.Disengage.TakeoffAt));
                    if (k < Core.Tuning.Disengage.FlipEnd) return SwingBody.Lerp(gather, tuck, Mathf.SmoothStep(0f, 1f, (k - Core.Tuning.Disengage.TakeoffAt) / (Core.Tuning.Disengage.FlipEnd - Core.Tuning.Disengage.TakeoffAt)));
                    if (k < Core.Tuning.Disengage.LooseAt) return SwingBody.Lerp(tuck, top, Mathf.SmoothStep(0f, 1f, (k - Core.Tuning.Disengage.FlipEnd) / (Core.Tuning.Disengage.LooseAt - Core.Tuning.Disengage.FlipEnd)));
                    if (k < Core.Tuning.Disengage.LandAt) return top;
                    var sunk = BowRecoil + new SwingBody(0.03f, 0f, 0f, 2f);
                    float land = (k - Core.Tuning.Disengage.LandAt) / (1f - Core.Tuning.Disengage.LandAt);
                    return land < 0.4f ? SwingBody.Lerp(top, sunk, Mathf.SmoothStep(0f, 1f, land / 0.4f))
                                       : SwingBody.Lerp(sunk, BowRecoil, Mathf.SmoothStep(0f, 1f, (land - 0.4f) / 0.6f));
                }
                case AttackMotion.BowRecoilHop:
                {
                    // A small gather, up through the air, and down INTO the recoil - the landing
                    // sinks a little past it and settles.
                    var gather = new SwingBody(0.03f, 0.02f, 0f, 2f);
                    if (k < 0.08f) return SwingBody.Lerp(default, gather, k / 0.08f);
                    if (k < RecoilAirEnd) return SwingBody.Lerp(gather, BowRecoil * 0.5f, (k - 0.08f) / (RecoilAirEnd - 0.08f));
                    float land = (k - RecoilAirEnd) / (1f - RecoilAirEnd);
                    var sunk = BowRecoil + new SwingBody(0.025f, 0f, 0f, 2f);
                    return land < 0.35f ? SwingBody.Lerp(BowRecoil * 0.5f, sunk, Mathf.SmoothStep(0f, 1f, land / 0.35f))
                                        : SwingBody.Lerp(sunk, BowRecoil, Mathf.SmoothStep(0f, 1f, (land - 0.35f) / 0.65f));
                }
                case AttackMotion.BowKneelRoll:
                {
                    // Up out of the stance into the spin, low through it, down onto the knee as it
                    // comes round to the target - and held there for the next draw.
                    float spin01 = Mathf.Clamp01(k / BowRollTurnEnd);
                    var mid = new SwingBody(0.045f, 0.030f, 0f, 0f);
                    if (k < BowRollTurnEnd * 0.5f) return SwingBody.Lerp(default, mid, Mathf.SmoothStep(0f, 1f, spin01 * 2f));
                    float down = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - BowRollTurnEnd * 0.5f) / (1f - BowRollTurnEnd * 0.5f)));
                    return SwingBody.Lerp(mid, BowKneel, down);
                }
                case AttackMotion.Flurry:
                    return FlurryBody(Mathf.Clamp01(k) * Core.Tuning.Flurry.Seconds);
                default:
                {
                    var combo = ComboOf(motion);   // the dimensional basics carry their own
                    return combo != null ? ComboBody(combo, k, seconds) : default;
                }
            }
        }

        /// <summary>The Lunge's thrust as a fraction of its swing - Animate's own boundary.</summary>
        static float LungeStrikeK(float seconds) =>
            Mathf.Clamp(LungeThrustSeconds / Mathf.Max(0.12f, seconds), 0.1f, 0.35f);

        /// <summary>The Flurry's body at <paramref name="t"/> seconds into the sequence -
        /// smoothstepped between its keys.</summary>
        static SwingBody FlurryBody(float t)
        {
            var times = FlurryBodyTimes;
            if (times == null || times.Length == 0) return default;   // a reload's fresh array
            for (int i = 1; i < times.Length; i++)
            {
                if (t > times[i]) continue;
                float u = Mathf.SmoothStep(0f, 1f, (t - times[i - 1]) / Mathf.Max(0.0001f, times[i] - times[i - 1]));
                return SwingBody.Lerp(FlurryBodyAt(i - 1), FlurryBodyAt(i), u);
            }
            return FlurryBodyAt(times.Length - 1);
        }

        /// <summary>
        /// Load, strike, settle on a motion's own cock and strike boundaries. The load eases out
        /// as the cock does (quick, then held); the strike is eased OUT too - the hips launch with
        /// the blade and decelerate into the pose, so the body lands as the blade does rather than
        /// still arriving after it.
        /// </summary>
        static SwingBody ThreeBeats(float k, float cockEnd, float strikeEnd,
                                    SwingBody load, SwingBody strike, SwingBody settle)
        {
            // A cock of zero length starts LOADED - a motion that comes out of a held wind-up (the
            // charge's slam, the Impale art's thrust) begins where that hold left the body.
            k = Mathf.Clamp01(k);
            if (cockEnd > 0f && k < cockEnd)
                return load * Mathf.Sin(k / Mathf.Max(0.0001f, cockEnd) * Mathf.PI * 0.5f);
            if (k < strikeEnd)
            {
                float t = (k - cockEnd) / Mathf.Max(0.0001f, strikeEnd - cockEnd);
                t = 1f - (1f - t) * (1f - t);
                return SwingBody.Lerp(load, strike, t);
            }
            if (strikeEnd >= 1f) return strike;
            float u = Mathf.SmoothStep(0f, 1f, (k - strikeEnd) / Mathf.Max(0.0001f, 1f - strikeEnd));
            return SwingBody.Lerp(strike, settle, u);
        }

        /// <summary>
        /// Puts the body down for this frame: the crouch on the hips and torso, the legs posed to
        /// their feet with the stance and the step's back-foot hold. Called after the pose branch
        /// so it reads THIS frame's body (the walk's own hips height comes in as
        /// <paramref name="gaitHipsY"/>).
        /// </summary>
        void PlaceBody(float cycle, float gaitHipsY, float dt)
        {
            float legs = 1f - BodyStanceWalkGive * _gaitWeight;
            float stance = _poseBody.Stance * legs;
            float step = _poseBody.Step * legs;

            if (_torso) _torso.localPosition += Vector3.down * _poseBody.Crouch;
            float hipsY = gaitHipsY - _poseBody.Crouch;
            if (_hips) _hips.localPosition = new Vector3(HipsRest.x, hipsY, 0f);

            // The front foot carries the step with the figure; the back foot is held where it
            // stood, so it moves BACK in the rig's frame by exactly what the figure travelled -
            // along the facing, that is: a step turned up or down the screen with the aim (the
            // Facing partial's StepDirection) carries both feet with it for the rest.
            PoseLegs(cycle, hipsY, stance, -stance - step * StepDirection.x);

            _bodyStepVelocity = dt > 0f ? (step - _lastBodyStep) / dt : 0f;
            _lastBodyStep = step;
        }

        /// <summary>The drawn step this frame, in rig units along the facing (see PlaceBody).</summary>
        float BodyStepNow => _poseBody.Step * (1f - BodyStanceWalkGive * _gaitWeight);
    }
}
