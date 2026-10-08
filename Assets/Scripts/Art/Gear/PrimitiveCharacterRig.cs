using System.Collections.Generic;
using UnityEngine;
using Convergence.Chain;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Player;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// The humanoid paper-doll. One SpriteRenderer per <see cref="RigLayer"/>, painted back to
    /// front, with the bare body underneath and equipped gear drawn over it.
    ///
    /// Re-equipping is just <see cref="Apply"/> — no rebuilding, no prefab swapping — because
    /// gear needs to change constantly as loot drops.
    ///
    /// View: drawn 3/4 (seen slightly from above and in front, like Zelda or Hades) rather than
    /// true top-down. A pure overhead view shows only scalp and shoulders, which would make the
    /// cosmetic tiers that the NFT economy rests on almost invisible.
    /// </summary>
    public partial class PrimitiveCharacterRig : MonoBehaviour, ICharacterRig
    {
        readonly Dictionary<RigLayer, SpriteRenderer> _layers = new();
        readonly Dictionary<RigLayer, Transform> _pivots = new();

        // Limb pivots that animate.
        Transform _armFront, _armBack, _legFront, _legBack, _torso, _hips, _head;

        /// <summary>
        /// The elbows - children of the two shoulder pivots, at <see cref="ElbowRest"/>. The
        /// forearm, the lower half of the glove, the ring and the weapon all hang here, so the
        /// hand follows the forearm when the arm bends. At 0 degrees (every pose but the rest
        /// carry) the arm is exactly the straight limb it always was.
        ///
        /// Re-found by name in <see cref="EnsureElbows"/> rather than trusted, the same discipline
        /// as the offhand disc.
        /// </summary>
        Transform _elbowFront, _elbowBack;

        /// <summary>
        /// The knees - children of the two hip pivots, at <see cref="KneeRest"/>. The shin, the
        /// lower greave and the boot hang here. Straight (0 degrees) whenever the hips sit at
        /// their rest height, so a standing character is exactly the unbroken leg it always was;
        /// the run and a landing bend them. Re-found by name in <see cref="EnsureKnees"/>, the
        /// elbows' discipline.
        /// </summary>
        Transform _kneeFront, _kneeBack;
        PlayerController _playerCache;
        Rigidbody2D _bodyCache;

        /// <summary>
        /// Resolved lazily, NOT in Construct.
        ///
        /// The rig is built before the Rigidbody2D and PlayerController are added to the same
        /// GameObject, so capturing these up front left both permanently null. The character then
        /// had no aim to face and no velocity to walk to - it never turned and never took a step,
        /// and every symptom looked like an animation problem rather than a wiring one.
        /// </summary>
        PlayerController Player => _playerCache != null
            ? _playerCache
            : _playerCache = GetComponentInParent<PlayerController>();

        Rigidbody2D Body => _bodyCache != null
            ? _bodyCache
            : _bodyCache = GetComponentInParent<Rigidbody2D>();

        SpriteRenderer _shadow;

        /// <summary>The contact shadow switched off (Shadow's appearance with its relic - a
        /// character that casts none). A plain bool, so a domain reload keeps it.</summary>
        bool _shadowHidden;

        /// <summary>
        /// See <see cref="ICharacterRig.SetVisualScale"/>. A plain float, so a domain reload
        /// restores it. Applied everywhere this rig writes its own root transform or the contact
        /// shadow (a sibling, so it does not inherit the root's scale), and to the offsets that
        /// move the drawing in its parent's space - a hop or a leap is a fraction of the body.
        /// </summary>
        [SerializeField] float _visualScale = 1f;

        public void SetVisualScale(float scale)
        {
            _visualScale = scale > 0f ? scale : 1f;
            var s = transform.localScale;
            transform.localScale = new Vector3(Mathf.Sign(s.x == 0f ? 1f : s.x) * _visualScale, _visualScale, 1f);
            if (_shadow != null && !TopDown)
                _shadow.transform.localPosition = new Vector3(0f, PixelSprite.Px(Proportions.SoleYCells) * _visualScale, 0f);
        }
        /// <summary>
        /// Joint rest positions, snapped to the pixel grid so limb seams land on texel boundaries
        /// rather than halfway across one.
        ///
        /// These are constants because the walk cycle REWRITES both every frame. When they were
        /// literals duplicated between Construct and Update, moving one and not the other put the
        /// rig back where it started on frame 1 - a bug that looks like the snap silently failing.
        /// bob and dip stay continuous on purpose: quantising them would jump the whole upper body
        /// a full texel per step, which is far worse than a smooth sub-texel slide.
        /// </summary>
        static readonly Vector2 HipsRest = PixelSprite.Px(0f, Proportions.HipYCells);
        static readonly Vector2 TorsoRest = PixelSprite.Px(0f, Proportions.HipYCells);

        /// <summary>
        /// THE ONE PLACE THE BODY'S PROPORTIONS ARE DECLARED, in texels at
        /// <see cref="PixelSprite.LayoutUnit"/>, y up from the rig root.
        ///
        /// Everything downstream - joint rest positions, the two-handed grip's shoulder
        /// relocation, the contact shadow, the outline contract that gear is placed against - is
        /// DERIVED from these rather than restating them. The numbers here are exactly what the
        /// rig was already built from, so introducing this table changed no pixel; what it changes
        /// is that a proportion can now be moved in one place instead of swept by hand through a
        /// dozen literals that had no way of knowing about each other.
        ///
        /// That mattered immediately: ArmBackGripRest's own doc reasons from "the shoulders are 12
        /// texels apart and the arm is 8 long", and both of those were separate literals hundreds
        /// of lines apart from the constant that depended on them. The same trap BladeRows and
        /// GreatswordGrip already solved for weapons - a pivot that disagrees with the grid moves
        /// the whole piece out of the character's hands.
        ///
        /// Chibi by deliberate choice, NOT by accident - see CLAUDE.md's "Chibi proportions". The
        /// head is bigger than the torso and there is no neck; head bottom overlaps torso top by
        /// <see cref="NeckOverlap"/> texels so the two silhouettes fuse rather than meeting at a
        /// seam. Anything that reproportions this body has to keep that overlap, or a neck appears.
        /// </summary>
        internal static class Proportions
        {
            // ================== TWO UNITS. THEY ARE NOT THE SAME UNIT. ==================
            //
            // BODY TEXELS  - cells of the body's own art grids, at BodyPpu.
            // LAYOUT CELLS - the project-wide offset coordinate system, PixelSprite.LayoutUnit.
            //
            // They used to be interchangeable, purely because the body happened to be drawn AT
            // LayoutUnit. That coincidence ends here: the body moves to 150 while every offX/offY
            // literal in the project stays in 75-cells (PixelSprite.LayoutUnit's own doc explains
            // why moving those is not an option - it is 106 gear offsets). So a grid dimension and
            // a joint position are now different units that differ by a factor of two, and mixing
            // them silently halves or doubles a limb.
            //
            // Every name below says which it is. Nothing here is unitless.

            /// <summary>
            /// The density the BODY's own art is drawn at - the one lever that moves the whole
            /// character's resolution.
            ///
            /// 150 rather than LayoutUnit's 75, which is what buys the reference proportions
            /// without redrawing a single face. The head's grids are left exactly as authored at
            /// 32x28 and simply land at half their old world size, taking the head from 45% of the
            /// figure's height to 23% - where the reference sits. Only the limbs and torso are
            /// redrawn, at roughly doubled texel counts, to hold the world size they already had.
            ///
            /// Detail doubles in the same screen area: the character still occupies 124 screen
            /// pixels at 1080p, but with 124 real texels instead of 62 block-doubled ones. The
            /// arena view does not move, because the character's WORLD size does not move.
            /// </summary>
            public const float BodyPpu = PixelSprite.FinestUnit;

            /// <summary>Body texels per layout cell. 2 - and the whole reason for the two units.</summary>
            public const float TexelsPerCell = BodyPpu / PixelSprite.LayoutUnit;

            /// <summary>Body texels to layout cells, for turning a grid size into a joint offset.</summary>
            public static float Cells(float bodyTexels) => bodyTexels / TexelsPerCell;

            // ---- grids, in BODY TEXELS. These are the sizes the art is drawn at. ----

            /// <summary>
            /// Head grid - composed in BodyLook, NOT redrawn for the reproportion. Unchanged at
            /// 32x28, which at BodyPpu is half the world size it used to be. This is the whole
            /// trick: the head shrinks by being measured against a finer ruler, so every skull,
            /// expression, brow, beard and hairstyle keeps its authored art exactly.
            /// </summary>
            public const int HeadW = 32, HeadH = 28;

            /// <summary>Torso. Narrower relative to the figure than the chibi build - the head no
            /// longer has to be the widest thing on screen for the proportions to read.</summary>
            public const int TorsoW = 26, TorsoH = 44;

            /// <summary>One arm, shoulder to fingertips.</summary>
            public const int ArmW = 10, ArmH = 32;

            /// <summary>
            /// Where the elbow falls IN THE ART, in BODY TEXELS down from the shoulder - the middle
            /// of the pinch Limb has carried at rows 12-15 since before there was a joint to put
            /// there. The glove harness already lines up with it: its elbow groove sits within a
            /// texel of this line. Every arm sprite is cut here.
            /// </summary>
            public const int ElbowArtTexels = 14;

            /// <summary>
            /// The WRIST, in arm-local art texels below the shoulder - where every glove's cuff meets
            /// its hand (GloveRows' wrist groove, Sovereign's "wrist at -22"). The rig cuts each
            /// forearm again here (PaintWristSplit) so a grip can raise the hand alone.
            /// </summary>
            public const int WristArtTexels = 22;

            /// <summary>
            /// How much LONGER the upper arm is drawn than the art authors it, in BODY TEXELS.
            ///
            /// At 14 the upper arm was barely longer than it is wide, and a pauldron's half-width
            /// alone is 9-10 texels - so in the shouldered carry, where the elbow is the point of
            /// the pose, the plate covered the whole segment and there was no elbow to see. 20
            /// puts the upper arm level with the forearm, the proportion the carry reference has.
            ///
            /// Added at PAINT time rather than authored (see PaintElbowSplit): the upper half of
            /// every arm sprite has the row just above its elbow band repeated this many times,
            /// and everything below the elbow keeps its own art and simply hangs lower. Every
            /// glove, ring and weapon offset stays exactly as authored.
            /// </summary>
            public const int UpperArmExtraTexels = 6;

            /// <summary>The elbow JOINT, in body texels down from the shoulder.</summary>
            public const int ElbowTexels = ElbowArtTexels + UpperArmExtraTexels;

            /// <summary>The elbow joint, in LAYOUT CELLS from the shoulder, y up.</summary>
            public static float ElbowYCells => -Cells(ElbowTexels);

            /// <summary>The elbow's line in the art, in LAYOUT CELLS from the shoulder, y up.</summary>
            public static float ElbowArtYCells => -Cells(ElbowArtTexels);

            /// <summary>One leg, hip to sole.</summary>
            public const int LegW = 8, LegH = 48;

            /// <summary>
            /// Where the knee falls IN THE ART, in BODY TEXELS down from the hip - the middle of the
            /// pinch the leg grid has carried at rows 22-25 since before there was a joint to put
            /// there ("the pinch marks where the joint goes before there is a joint"). Half the
            /// leg, so thigh and shin are the same length. Every leg, greave and boot is cut here.
            /// </summary>
            public const int KneeTexels = 24;

            /// <summary>The knee joint, in LAYOUT CELLS from the hip, y up.</summary>
            public static float KneeYCells => -Cells(KneeTexels);

            /// <summary>
            /// How far head bottom and torso top OVERLAP, in BODY TEXELS. This is what closes the
            /// neck. Never close it by moving the joints closer instead - the overlap is what
            /// fuses the two silhouettes, and offsets alone leave a visible waist between them.
            /// </summary>
            public const int NeckOverlap = 4;

            // ---- joints, in LAYOUT CELLS from the rig root, y up. ----

            /// <summary>
            /// The waist. Hips and torso share it: the legs hang from here and stay PLANTED, while
            /// the torso takes the aim lean, so turning pivots at the waist instead of swinging the
            /// feet off the ground.
            /// </summary>
            public const float HipYCells = -8f;

            /// <summary>
            /// Half the distance between the two hip joints - one leg either side.
            ///
            /// 5. At 3 the two legs ABUT: each is LegW wide centred half a span out, so
            /// they met at the centre line with no gap at all and the stance read as one column
            /// with a seam down it. Legs now sit slightly wider than the waist, which is what
            /// hips are. Raised again from 4 once ARMOUR went on: a greave has to fit between the
            /// legs as well as on them, and at 4 the plates met even after the bare legs did not.
            /// </summary>
            public const float HipHalfSpanCells = 4f;

            /// <summary>
            /// Half the distance between the shoulders. ArmBackGripRest reasons directly from this,
            /// so the two-handed grip follows a change here instead of drifting.
            ///
            /// 7, not 6. Measured against the torso rather than guessed: at 6 the arm sat 12 body
            /// texels out with a half-width of 5, against a torso half-width of 13 - so only FOUR
            /// texels of arm ever cleared the body outline and at arm's length the character read
            /// as having none. At 7 that clearance is six texels, and the joint sits a texel past
            /// the torso edge, which is where a shoulder is.
            /// </summary>
            public const float ShoulderHalfSpanCells = 7f;

            /// <summary>Shoulder height above the waist - just under the torso's top edge.</summary>
            public static float ShoulderYCells => Cells(TorsoH) - 1f;

            /// <summary>
            /// The neck joint, above the waist. The head rotates about where it MEETS the body
            /// rather than about its own middle, which is why this is the torso's top edge and not
            /// the head's centre.
            /// </summary>
            public static float NeckYCells => Cells(TorsoH);

            // ---- the outline contract, DERIVED, in LAYOUT CELLS. ----
            //
            // CLAUDE.md: "Gear offsets and the contact shadow are placed against these." Computed
            // rather than quoted so they cannot fall out of step with the grids above - which, as
            // literals in a comment, they previously could and silently would.
            //
            // CROWN AND SOLE are derived, so trimming a limb moves them - see BuildShadow,
            // which reads SoleYCells rather than the literal -34 it used to carry. That literal
            // was already stale the moment the legs were trimmed: it put the contact patch two
            // cells below the feet, and nothing would have said so.
            // rather than lucky: the reproportion redistributes the figure INSIDE its own outline
            // instead of resizing it. Anything measured against the extremes - the contact shadow,
            // the collider, the camera framing - is untouched. Only the internals move.

            /// <summary>Topmost drawn cell: the head's crown.</summary>
            public static float CrownYCells => HipYCells + NeckYCells - Cells(NeckOverlap) + Cells(HeadH);

            /// <summary>Bottommost drawn cell: the soles.</summary>
            public static float SoleYCells => HipYCells - Cells(LegH);

            /// <summary>Total drawn height in layout cells, crown to sole.</summary>
            public static float HeightCells => CrownYCells - SoleYCells;

            /// <summary>Half the widest point of the bare body.</summary>
            public static float HalfWidestCells => Cells(Mathf.Max(HeadW, TorsoW)) * 0.5f;
        }

        /// <summary>
        /// The neck joint, at the torso's top edge. A constant for the same reason the two above
        /// are: the idle breath rewrites it every frame, so a literal here and another in Construct
        /// would be the same trap - move one and the rig snaps back to the other on frame 1.
        /// </summary>
        static readonly Vector2 HeadRest = PixelSprite.Px(0f, Proportions.NeckYCells);

        /// <summary>The knee, in its hip pivot's frame. Identical on both legs.</summary>
        static readonly Vector2 KneeRest = PixelSprite.Px(0f, Proportions.KneeYCells);

        /// <summary>The elbow, in its shoulder pivot's frame. Identical on both arms.</summary>
        static readonly Vector2 ElbowRest = PixelSprite.Px(0f, Proportions.ElbowYCells);

        /// <summary>
        /// The elbow's line in shoulder-AUTHORED coordinates - where the art is cut, and what an
        /// authored offset is measured against. Differs from <see cref="ElbowRest"/> by exactly
        /// the upper arm's extra length.
        /// </summary>
        static readonly Vector2 ElbowArtRest = PixelSprite.Px(0f, Proportions.ElbowArtYCells);
        static readonly Vector2 WristArtRest = PixelSprite.Px(0f, -Proportions.Cells(Proportions.WristArtTexels));

        /// <summary>
        /// Where the off-hand shoulder sits when both hands are on one hilt.
        ///
        /// The arm cannot reach across by rotating: the shoulders are 12 texels apart and the arm
        /// is 8 long, so no angle brings the far fist to the near one. The joint itself has to
        /// move. Planted just past the body's centre line, the off fist lands about four texels
        /// below the main one and on the same axis - the lower grip, with the main fist under the
        /// guard, which is how a greatsword is actually held. Tuned by measuring the fist in the
        /// WEAPON's own frame rather than by eye; a fist that is only nearly on the hilt reads as
        /// a second hand grabbing at air.
        ///
        /// Negative X because the weapon arm now rests on the far side of the aim: the hilt is over
        /// on -X, so the off hand reaches just PAST centre in that direction to meet it.
        /// </summary>
        /// <remarks>
        /// Derived from <see cref="Proportions"/> rather than restated, because the reasoning above
        /// is ENTIRELY about numbers that live there: "the shoulders are 12 texels apart and the
        /// arm is 8 long" is ShoulderHalfSpan and ArmW. Left as literals, reproportioning the body
        /// would move the shoulders while this constant went on describing the old ones, and the
        /// off fist would grab at air - which is the exact failure its own note warns about.
        ///
        /// Half a shoulder-span past centre, and four texels down from the shoulder line.
        /// </remarks>
        static readonly Vector2 ArmBackGripRest = PixelSprite.Px(
            -Proportions.ShoulderHalfSpanCells * 0.5f,
            Proportions.ShoulderYCells - Proportions.Cells(Proportions.ArmW) * 0.75f);

        /// <summary>How far the off hand trails the main one round the hilt, in degrees.</summary>
        const float GripTrail = 20f;

        /// <summary>
        /// How far the shoulders drive forward at the peak of a swing, in world units.
        ///
        /// Deliberately small. This is what closes the last stretch between the blade tip and the
        /// reach ring. armReach no longer slides the shoulder joint itself - the arm REACHES for
        /// the grip it describes (SolveArmReach), and only what a straight arm can't cover moves
        /// the shoulder (MaxShoulderShove) or the whole figure - but a bigger lunge is still a
        /// bigger body slide. The blade's own length does the heavy lifting.
        /// </summary>
        const float Lunge = 0.26f;

        /// <summary>
        /// How far the hand lifts on Plant's wind-up, world units from the shoulder.
        ///
        /// Sized against Lunge for the same reason it exists: armReach moves the arm JOINT, and
        /// past about a third of a unit the shoulder visibly detaches from the torso. The head
        /// sits at +0.373, so this puts the hilt at roughly eye level without tearing the rig.
        /// </summary>
        const float HiltRaise = 0.20f;

        /// <summary>
        /// How far ACROSS the body the plant carries the hand, toward the facing.
        ///
        /// The weapon hangs off the front arm, whose rest is x -0.16 - the far side of a character
        /// facing +x - so a plant that only moved vertically drove the blade into the ground
        /// BEHIND them. This carries the hand past the body's centre so the sword goes down in
        /// front, where the fire it starts is between the player and whatever they are fighting.
        ///
        /// armReach is in the FACING frame - the root mirrors, so +x is always forward, the same
        /// convention Lunge uses.
        ///
        /// This is far enough to put the blade BESIDE the body rather than over it. At a smaller
        /// push the sword landed on the character's centre line and, being 0.43 wide against a
        /// 0.6-wide character, buried the torso and most of the gear - which is the one thing this
        /// rig exists to show. The pair (0.30, 0.20) is 0.36 from the joint's rest, past Lunge's
        /// third-of-a-unit rule of thumb; checked on screen rather than assumed, and the shoulder
        /// holds because the pauldron and the raised arm cover the seam in this pose.
        /// </summary>
        const float PlantForward = 0.30f;

        // ---- idle breath ----
        //
        // Every term in the walk cycle is multiplied by the stride's own speed factor, so a
        // character standing still was drawn with every joint frozen at its rest position - and
        // standing still is most of what the figure does in the hub, where it is looked at
        // head-on for minutes at a time. This is the one motion that runs AT A STANDSTILL and
        // fades out as the walk fades in, so the two never both drive the torso at once.

        /// <summary>Breaths per second.</summary>
        const float BreathRate = 0.26f;

        /// <summary>
        /// How far the chest rises at rest, world units. Deliberately well under the walk's own
        /// bob (0.03): a breath the size of a footfall reads as panting, not as standing.
        /// </summary>
        const float BreathRise = 0.006f;

        /// <summary>
        /// Speed at which the breath has faded out entirely. Well under the stride's own 3.5, so
        /// the two overlap for a moment rather than handing over on one exact frame.
        /// </summary>
        const float BreathStillSpeed = 1.2f;

        /// <summary>
        /// How much of the chest's rise the head gives back, as a fraction.
        ///
        /// The head hangs off the torso, so it inherits the whole rise for free and the figure
        /// would simply translate upward - which reads as bobbing, not breathing. Taking a share
        /// back at the neck compresses that gap instead, which is where a breath actually shows.
        /// </summary>
        const float BreathNeckShare = 0.35f;

        // ---- weight shift: starting, stopping and turning ----
        //
        // ONE spring with two drivers, because these are the same event: a start, a stop and a
        // direction reversal are all a change in horizontal velocity, and a body answers all
        // three the same way - the legs go first and the chest arrives late. Built as two
        // mechanisms it would mean tuning the same material response twice, and then getting two
        // different answers for a turn that is also a stop.
        //
        // The TORSO carries it. Nothing else writes that rotation (FaceAim only ever clears it),
        // and the head counter-rotates against it for the reason Construct already gives: a turn
        // should read as the shoulders coming round, not as the whole character keeling over.

        /// <summary>Lean at a full-speed start or stop, in degrees. Positive tips the chest BACK.</summary>
        const float LeanAngle = 7f;

        /// <summary>
        /// How fast the reference speed chases the real one, per second.
        ///
        /// The lean is driven by the GAP between the two, never by a dt-derivative of velocity:
        /// Rigidbody2D.linearVelocity only changes on a physics step, so differentiating it in
        /// Update reads exactly zero on the frames between steps and a spike on the frame after -
        /// garbage at any framerate not locked to the physics rate. The gap is bounded by speed
        /// instead of by dt, and it already has the shape wanted: positive while pulling away from
        /// rest, zero at a constant speed, negative while stopping.
        /// </summary>
        const float LeanFollow = 7f;

        const float LeanStiffness = 190f;
        const float LeanDamping = 13f;

        /// <summary>
        /// Kick given to the lean spring when the mirror flips, in degrees per second.
        ///
        /// Positive, so the chest is left BEHIND the turn and has to catch up. A reversal the body
        /// answered instantly is what made fast aim-flipping read as the character sliding round
        /// rather than turning - the mirror swapped and nothing else in the figure acknowledged it.
        /// </summary>
        const float TurnPlantKick = 210f;

        /// <summary>How far the hips drop per degree of lean, world units - the plant under it.</summary>
        const float LeanHipDip = 0.0016f;

        /// <summary>How much of the torso's rotation the head cancels.</summary>
        const float LeanHeadShare = 0.45f;

        /// <summary>
        /// How much of the lean is taken away while a swing or charge is live.
        ///
        /// The arms hang off the torso, so a leaning chest rotates every arm angle with it - and
        /// the swing arcs in Animate are tuned as ABSOLUTE angles, the same trap ArmAimRange's own
        /// clamp exists for. Reduced rather than cancelled, and eased in and out (see
        /// <see cref="_leanSuppress"/>) rather than switched, because snapping back to the full
        /// lean on the frame a swing ends is exactly the pop "a swing eases out, not just in" is
        /// about.
        /// </summary>
        const float LeanSwingSuppression = 0.7f;

        // ---- landing ----
        //
        // WRITTEN AS JOINTS MOVING, NEVER AS localScale, and that is not a preference. This art is
        // point-filtered and held on a whole number of screen pixels per texel on purpose (see
        // PixelPerfectZoom, and why the densities are 37.5/75/150); scaling a point-sampled sprite
        // resamples it, so a squash expressed as a scale would shimmer in exactly the way all of
        // that exists to prevent - and it would do it on the one frame the player is looking
        // hardest. Legs bending and a chest sinking is the same read and costs nothing.
        //
        // Fed by BOTH lifts the rig knows about: SetAirborne's gameplay leap and the swing hop.
        // They already stack as a drawing offset, so a landing is simply their sum returning to
        // the floor, whichever of them put the figure in the air.

        /// <summary>
        /// Scale of the hip drop on a full-height landing, world units.
        ///
        /// NOT the depth actually reached - the kick is fed in as a velocity (see AdvanceLanding)
        /// and the damping eats part of it on the way out, so the real peak is about 0.64 of this
        /// at the current stiffness/damping pair. Left expressed this way because a velocity is
        /// unreadable as a number and a distance is not, but the two are only proportional:
        /// retuning LandDamping moves the peak without this constant changing.
        /// </summary>
        const float LandSquash = 0.085f;

        /// <summary>
        /// Fall height at which the squash is full, world units. A leap finisher clears several
        /// units and a swing hop is a fraction of one, so without a reference height the hop
        /// would either be invisible or the leap would fold the character in half.
        /// </summary>
        const float LandFullHeight = 3f;

        const float LandStiffness = 260f;
        const float LandDamping = 15f;

        /// <summary>
        /// How much further the chest sinks than the hips. Above 1 so the body COMPRESSES: at
        /// exactly 1 the torso and hips drop together and the whole figure just moves down, which
        /// reads as the ground giving way rather than as the character absorbing a landing.
        /// </summary>
        const float LandTorsoShare = 1.35f;

        /// <summary>How much of the drop the neck takes on top of the torso's - see BreathNeckShare.</summary>
        const float LandNeckShare = 0.3f;

        /// <summary>Lift below which the figure counts as standing on the floor, world units.</summary>
        const float LandGroundedLift = 0.002f;

        // ---- footfalls ----

        /// <summary>
        /// Speed below which a step raises nothing. A character easing into a walk should not be
        /// kicking up dust, and the stride clock runs at any speed above zero.
        /// </summary>
        const float DustSpeed = 1.8f;

        /// <summary>How far ahead of the hips a puff is dropped, world units.</summary>
        const float DustAhead = 0.10f;

        // ---- hit reaction ----

        /// <summary>
        /// How long the figure is shoved off its own origin after taking a hit.
        ///
        /// DRAWING ONLY, the same contract SetAirborne and SetPhantomOffset keep - the player's
        /// transform never moves, so this is not knockback and does not break the rule that
        /// enemies never displace the player (Tuning.Enemy.AttackKnockback is 0 and stays 0).
        /// What it buys is that a hit landing on you is legible as an EVENT rather than as the
        /// white flash being the only thing that ever happens.
        /// </summary>
        const float HitReactionSeconds = 0.09f;

        /// <summary>How far that shove travels at its peak, world units.</summary>
        const float HitReactionDistance = 0.055f;

        // ---- cape ----
        //
        // A spring rather than a curve driven off speed. The difference is the whole point: a
        // curve puts the cape at exactly the angle the current velocity implies and freezes it
        // there, so stopping snaps it straight and it never behaves like cloth. A spring carries
        // momentum, so it lags on the way out, overshoots when you stop, and settles - which is
        // the entire read of "it trails, then falls back".

        /// <summary>Swing at a full run, in degrees.</summary>
        const float CapeSwing = 34f;
        const float CapeStiffness = 110f;
        const float CapeDamping = 9f;

        /// <summary>
        /// The HEM's own spring, chasing the upper cloth rather than the run - so it lags as a run
        /// starts and swings on past when it stops, which is what makes a cape read as cloth and
        /// not as a board. Softer and less damped than the upper spring on purpose: the further
        /// cloth hangs from where it is fastened, the longer it takes to settle.
        /// </summary>
        const float CapeHemStiffness = 45f;
        const float CapeHemDamping = 4.5f;

        /// <summary>
        /// A small ripple on top of the swing, scaled by speed - its reach at the hem, as an angle.
        /// It runs DOWN the cloth (ClothBend.RipplePhase), not as one wobble of the whole sheet.
        ///
        /// It is what covers running straight up or down the screen. A cape trailing directly away
        /// from the camera would foreshorten, and there is no art for that, so the directional
        /// swing has nothing to work with on a vertical run - without the ripple the cloth would
        /// sit dead still while the character sprints.
        /// </summary>
        const float CapeFlutter = 5f;

        /// <summary>Fallback run speed when the rig is previewed with no player attached.</summary>
        const float CapeReferenceSpeed = 6.5f;

        Vector2 _capeRest;      // where Apply parked the cape, its own centre
        float _capeHalfHeight;  // the hinge sits this far ABOVE the rest position
        float _capeLength;      // hinge to hem
        float _capeSwingScale = 1f;   // GearItem.CapeSwingScale - a long cape swings less
        bool _hasCape;
        float _capeAngle, _capeAngularVelocity;       // the cloth just under the hinge
        float _capeHemAngle, _capeHemVelocity;        // the hem, chasing it

        /// <summary>The bends drawing the cape and a drape that swings with it (see ClothBend).
        /// Component references, so a domain reload keeps them.</summary>
        ClothBend _capeCloth, _drapeCloth;

        /// <summary>Whether the Back item's drape swings with the cape - see
        /// <see cref="GearItem.DrapeSwingsWithCape"/>. A plain bool, so a domain reload keeps it.</summary>
        bool _drapeSwings;

        /// <summary>
        /// The scarf's own spring - same shape as the cape's (see AnimateScarf/AnimateCape), just
        /// lighter: a scarf is a strip of cloth rather than a full sheet, so it swings further at
        /// a full run and settles faster once you stop. Kept as its own state rather than reusing
        /// the cape's, because a character can wear a Back-slot cape AND a Neck-slot scarf at
        /// once and each has to trail independently.
        /// </summary>
        const float ScarfSwing = 46f;
        const float ScarfStiffness = 150f;
        const float ScarfDamping = 7f;
        const float ScarfFlutter = 9f;

        /// <summary>The tail's END, chasing the cloth at the knot - the cape's hem spring, quicker:
        /// a strip has less cloth to bring round than a sheet.</summary>
        const float ScarfHemStiffness = 70f;
        const float ScarfHemDamping = 5f;

        Vector2 _scarfRest;
        float _scarfHalfHeight;
        bool _hasScarf;
        float _scarfAngle, _scarfAngularVelocity;     // the tail just under the knot
        float _scarfHemAngle, _scarfHemVelocity;      // its end, chasing it
        ClothBend _scarfCloth;

        // ---- cloth over the legs (Tasset and Belt) ----
        //
        // A skirt, a coat's tails, faulds: worn at the waist and over the legs. See ClothBend's
        // OverLegs for the rules (hang from the hips, not the leaning chest; pushed by the legs).
        // What lives here is the TIMING - the cloth answers the legs a beat late and swings a
        // little past them, which is the difference between cloth and a picture glued to a leg.

        /// <summary>The cloth's own copy of each leg's thigh and shin angle, chasing the real
        /// ones. Stiff enough that a leg never gets clear of the cloth over it, soft enough to
        /// lag and overshoot.</summary>
        const float LegClothStiffness = 260f;
        const float LegClothDamping = 15f;

        /// <summary>Trailing the walk, at full speed, degrees - small, or the hem trails off the
        /// front leg and shows it. The legs' push is most of what this cloth does.</summary>
        const float LegClothSwing = 5f;
        const float LegClothSwingStiffness = 70f;
        const float LegClothSwingDamping = 7f;
        const float LegClothHemStiffness = 40f;
        const float LegClothHemDamping = 4f;
        const float LegClothFlutter = 1.5f;

        /// <summary>How much of the legs' push reaches cloth far out past them, and how quickly
        /// cloth below a knee falls back from it (see ClothBend.DrapeFall).</summary>
        const float LegClothFarFollow = 0.5f;
        const float LegClothDrapeFall = 0.35f;

        float _lcThighF, _lcThighFV, _lcShinF, _lcShinFV;
        float _lcThighB, _lcThighBV, _lcShinB, _lcShinBV;
        float _lcAngle, _lcAngleV, _lcHem, _lcHemV;
        bool _lcPrimed;
        ClothBend _tassetCloth, _beltCloth;

        /// <summary>The COMBINED, currently-applied state - GearItem.HasGlowingEye asked for by
        /// <see cref="_glowEyeItem"/> AND not suppressed by <see cref="_helmHidden"/>. See
        /// RepaintHead and SyncGlowEye. Tracked separately from Apply's other per-item flags
        /// because it is the only one that has to reach back into SetAppearance's own territory
        /// (the face itself) rather than staying inside the gear layers Apply otherwise owns.
        /// </summary>
        bool _glowLeftEye;

        /// <summary>
        /// Same spring as the cape (see AnimateCape), scaled down for a small glass vial rather
        /// than a full sheet of cloth: less swing, and it settles faster. Unlike the cape this
        /// needs no separate hinge-offset math - VialPivot already sits at the cork, which is the
        /// transform's own local origin, so rotating localRotation in place already orbits the
        /// body around that point instead of spinning it about its own centre.
        /// </summary>
        const float VialSwing = 14f;
        const float VialStiffness = 150f;
        const float VialDamping = 11f;
        float _vialAngle, _vialAngularVelocity;

        /// <summary>True while a two-handed weapon is equipped. Set by <see cref="Apply"/>.</summary>
        bool _twoHanded;

        /// <summary>
        /// True while a bow is equipped. Unlike every other two-handed weapon, a bow's two hands
        /// do genuinely different things - one holds the bow steady, the other draws the string -
        /// so it cannot use the shared-hilt "off arm copies the main arm" rule TwoHanded is built
        /// on. Set by <see cref="Apply"/>, alongside re-parenting the Weapon layer onto the BACK
        /// arm instead of the front one - see the note by that re-parenting for why.
        /// </summary>
        bool _bowGrip;

        /// <summary>True while the weapon is drawn on the far side of the body - the alt-swing.</summary>
        bool _weaponBehind;

        /// <summary>
        /// Whether the shouldered rest carry's own layer stack is on - see
        /// <see cref="ShoulderCarryOrder"/>. Separate state from <see cref="_weaponBehind"/>
        /// because the two want DIFFERENT stacks and only one of them can be applied: the
        /// alt-swing's crossover puts the blade behind the torso while leaving it in front of the
        /// head and the arms, which is right for a swing travelling across the body and wrong for
        /// a sword resting on a shoulder.
        /// </summary>
        [SerializeField] bool _shoulderCarry;

        /// <summary>True while showing the character's back - see <see cref="SetFacingAway"/>.</summary>
        bool _facingAway;

        bool _facingLeft;
        float _bodyYaw;       // top-down: the body's heading, eased toward the aim
        float _facingYaw;     // what the root should sit at when no spin is overriding it
        float _aimResidual;   // elevation the body could not cover; the arm adds it

        /// <summary>
        /// How much of <see cref="_aimResidual"/> is currently suppressed, 0..1.
        ///
        /// Some attacks are not aimed AT anything. Plant drives the blade into the ground at the
        /// character's own feet, so letting the aim residual ride on top of it tilted the sword by
        /// up to ArmAimRange - a blade that should be dead vertical leaned by however high the
        /// auto-targeter happened to be pointing.
        ///
        /// Eased rather than switched, for the reason the pose values are: snapping 45 degrees of
        /// aim back in on the frame the swing ends is the same pop that "a swing eases out, not
        /// just in" exists to prevent.
        /// </summary>
        float _aimSuppress;
        float _stride;        // walk cycle phase, radians, wrapped - see the Gait partial

        // Plain floats and a bool, so a domain reload restores all of them. Nothing here may
        // become a collection or an interface without re-reading the traps in CLAUDE.md.
        float _breath;                     // idle breath phase, wrapped every cycle
        float _leanAngle, _leanVelocity;   // the weight-shift spring - see the block above
        float _forwardReference;           // the speed the lean measures the real one against
        float _leanSuppress;               // 0..1, eased; how far a live swing holds the lean down

        /// <summary>
        /// Whether <see cref="_facingLeft"/> has ever been set from a real aim.
        ///
        /// A rig constructed facing right and first aimed left would otherwise read its opening
        /// frame as a reversal and plant a turn nobody performed - most visible on the shadow
        /// echoes, which are placed already pointing outward.
        /// </summary>
        bool _facingKnown;

        float _landDepth, _landVelocity;   // the landing spring, world units of compression
        float _lastLift;                   // airborne + hop last frame, for the landing edge
        Vector2 _hitPush;                  // direction and size of the hit shove, world units
        float _hitTimer;                   // seconds left on it

        /// <summary>
        /// Which footfall of the stride the last frame was in, so a puff is dropped on the
        /// crossing rather than every frame the dip is near its peak. Sentinel because the FIRST
        /// crossing a rig ever sees is not a step it took - it is wherever the phase happened to
        /// start.
        /// </summary>
        int _lastFootfall = int.MinValue;
        float _attackTimer;
        float _attackLength = 0.24f;
        bool _alt;            // this swing is the chain's ALT variant - see ICharacterRig.PlayAttack
        AttackMotion _motion = AttackMotion.Chop;
        Vector3 _armFrontRest, _armBackRest;

        // Carried between frames so the swing pose can EASE back to idle instead of popping.
        // Animate() writes exact keyframed values while a swing is live, which is correct - the
        // arc has to hit its real numbers, not a smoothed approximation of them. The pop was in
        // what happened the instant _attackTimer crossed zero: armSwing/backSwing/bodySpin/
        // armReach fell straight through to the walk-cycle default with no transition at all, so
        // a Chop ending its arc at +74 degrees was AT +74 one frame and AT 0 the next. Reversed
        // swings end at a different angle than forward ones (Chop is -100..+75, not symmetric),
        // so the size of that pop differed by direction too - which is likely what read as
        // swings not feeling consistent, on top of just looking broken on every single swing.
        float _poseArmSwing, _poseBackSwing, _poseBodySpin;
        Vector3 _poseArmReach;

        /// <summary>How quickly the pose relaxes to idle once a swing or charge ends.</summary>
        const float PoseRecoverySharpness = 18f;

        // ---- the shouldered rest carry ----
        //
        // The pose is copied from the reference, and every number here is measured off it rather
        // than chosen by eye. Seen from the front, with the character's RIGHT arm on screen-left:
        //
        //   the UPPER ARM rises up and out from the shoulder, 30 degrees above horizontal, so the
        //   elbow sits outside the body at about chin height;
        //   the FOREARM folds back up from the elbow, 20 degrees off vertical and leaning in, so
        //   the fist lands beside the head at forehead height;
        //   the BLADE runs from that fist down across the back, 50 degrees off vertical, its tip
        //   clearing the far hip, with the handle and pommel standing up and out past the head.
        //
        // Before the elbow existed the bend could only be IMPLIED - the shoulder swung up and the
        // weapon counter-rotated against it. With a real joint each segment takes its own angle.
        //
        // All three are written for arm.back (on rig +X) and flipped for arm.front - see
        // CarryInBackArm for why the carrying arm changes with facing.

        /// <summary>Shoulder angle for the carry, degrees: the upper arm 30 above horizontal.</summary>
        const float CarryShoulderDegrees = 120f;

        /// <summary>Elbow angle for the carry, degrees: the forearm 20 off vertical, leaning in.</summary>
        const float CarryElbowDegrees = 80f;

        /// <summary>
        /// The weapon's own angle against the forearm, degrees. Their sum with the two above is
        /// the blade's angle: 120 + 80 - 70 = 130, i.e. 50 degrees off vertical, tip down and
        /// toward the far side of the body.
        /// </summary>
        const float CarryWeaponDegrees = 70f;

        // ------------------------------------------------------------ the march carry
        //
        // Walking LEFT or RIGHT while facing the camera, the blade comes off the back and is
        // SHOULDERED: laid back over the trailing shoulder, resting on top of it beside the neck,
        // ready to come up into a strike. The rest carry (blade down across the back) is a pose
        // for standing; walking into a fight with it read as wrong (the user's call). Standing
        // still, walking straight up or down the screen, or turned away, the rest carry holds.
        // Blended by _march.
        //
        // From the power stance it is the ELBOW DROPPING and the WRIST TURNING: the forearm keeps
        // nearly the rest carry's angle (200 -> 210, upright, leaning in), the upper arm swings
        // down so the elbow comes in front of the body below the fist, and the fist ends on the
        // upper chest. That is forced, not chosen: for the blade to rest on the shoulder by the
        // neck, the fist has to be INWARD of that shoulder (measured in the rig: the shoulder
        // joint is 3.5 body units from the neck, the arm 5.0 + 3.45 to the grip) - and the only
        // elbow that reaches there is down in front. Two wrong turns first: the rest arm with
        // only the blade turned put the fist overhead (brandishing); an arm folded out beside
        // the shoulder moved the fist away from the head (holding the sword OUT); and the power
        // stance with the elbow dropped 15 degrees had the blade floating above the shoulder.
        //
        // Then (the user's call) the elbow came further DOWN, the blade turned a little FLATTER
        // and the wrist ROLLED it: shoulder -33 -> -20, blade 25 -> 17 (clockwise walking left,
        // tried steeper at 37 first and sent back), forearm kept at 210. The wrist turns as it
        // comes, putting the OTHER edge up (_marchRolled) - the blade moved down the shoulder into
        // a power stance it can strike from. Judged by eye in the hub: the guard sits on the
        // pauldron; at -30 the fist rose and the guard climbed toward the head.
        //
        // Third pass (the user's call, against two shouldered-greatsword references): arm and
        // blade moved DIAGONALLY DOWN, ~5 body texels down and 5 inward, the blade's angle
        // unchanged - it had floated above the pauldron with the guard by the neck; now it rests
        // ON the shoulder with the fist lower on the chest. Solved as IK on the real rig: the
        // forearm 210 -> 240, the shoulder stays at -20 (it came out -19). Down and OUTWARD was
        // tried beside it and buried the arm under the pauldron.
        //
        // Same convention as the rest carry: written for arm.back and flipped for arm.front, so
        // "outward on the carrying side" is BEHIND - the carrying arm is the trailing one.

        /// <summary>
        /// Shoulder angle for the march carry, degrees: the upper arm down and a little INWARD,
        /// so the elbow sits in front of the body under the fist. Dropped from -33 by the user's
        /// call, together with the flatter <see cref="MarchBladeDegrees"/> (forearm held at
        /// MarchForearmDegrees).
        /// </summary>
        const float MarchShoulderDegrees = -20f;

        /// <summary>
        /// The forearm's ABSOLUTE angle in the march carry (shoulder + elbow), degrees: leaning
        /// in across the chest. Was 210; 240 moves the fist and blade diagonally down onto the
        /// shoulder (the user's call). Kept as an absolute angle so the blend moves the forearm
        /// 40 degrees while the elbow drops, rather than spinning it a whole turn the other way
        /// (-120 and 240 are the same angle; only one is a short blend).
        /// </summary>
        const float MarchForearmDegrees = 240f;

        /// <summary>Elbow angle for the march carry, degrees - derived from the two above.</summary>
        const float MarchElbowDegrees = MarchForearmDegrees - MarchShoulderDegrees;

        /// <summary>
        /// The blade's angle above horizontal in the march carry, pointing back (away from the
        /// walk) over the shoulder. Was 25 (set by eye by the user); 17 since the user asked for
        /// it a little clockwise (walking left) with the elbow dropped. Not 37 - tried, sent back.
        /// </summary>
        const float MarchBladeDegrees = 17f;

        /// <summary>
        /// The weapon's own angle against the forearm in the march carry, DERIVED so the blade
        /// lands on <see cref="MarchBladeDegrees"/>. The rig's angles are 0 tip-up, positive
        /// toward -X, and total = shoulder + elbow - weapon; a blade leaning OUT (+X for
        /// arm.back) at that elevation is a total of 270 + blade, taken the long way round from
        /// the rest carry's 130 so the blend swings the tip DOWN and out behind the body, never
        /// up across the head.
        /// </summary>
        const float MarchWeaponDegrees = MarchForearmDegrees - (270f + MarchBladeDegrees);

        /// <summary>
        /// How far into the blend the march's own layer order takes over (see MarchCarryOrder).
        /// Late, because on the way the blade swings down behind the body, where the rest order
        /// is the right one.
        /// </summary>
        const float MarchOrderFrom = 0.85f;

        /// <summary>
        /// Share of the speed that must be sideways (screen X) for the march carry to be full.
        /// Below MarchSidewaysFrom none of it - walking straight up or down keeps the rest carry.
        /// </summary>
        const float MarchSidewaysFrom = 0.35f;
        const float MarchSidewaysFull = 0.75f;

        /// <summary>
        /// Where in the blend the wrist rolls the blade onto its OTHER edge (see _marchRolled),
        /// either side of halfway so a blend hovering there can't flicker. Mid-blend because the
        /// blade is swinging down behind the body then, where the swap is least seen.
        /// </summary>
        const float MarchRollOn = 0.55f;
        const float MarchRollOff = 0.45f;

        /// <summary>
        /// How quickly the carry moves INTO the march, per second - a quick flick up onto the
        /// shoulder (95% in about 0.17s), not a lazy swing. Was 6, scaled by the walk's own eased
        /// size on top: the blade took two-thirds of a second to arrive (the user's call: too slow).
        /// </summary>
        const float MarchInSharpness = 18f;

        /// <summary>How quickly the carry settles back from the march to rest, per second.</summary>
        const float MarchOutSharpness = 8f;

        /// <summary>
        /// Where the weapon's grip sits in the carry, in the ELBOW's frame (x flips with the arm).
        /// The fist: the gauntlet's middle, 13.5 body texels down the forearm. The authored grip
        /// sits at the wrist, which is right while the handle runs along the forearm and wrong
        /// once it crosses it - held there the handle would pass beside the hand instead of
        /// through it.
        /// </summary>
        static readonly Vector2 CarryGrip = PixelSprite.Px(0f, -Proportions.Cells(13.5f));

        // ---- reaching the grip (see "the swing's arms REACH the grip" in Update) ----

        /// <summary>
        /// How far a swing may still shove the shoulder JOINT, rig units - about a texel and a
        /// half. Enough for the strike to carry the shoulder into it; past this it reads as the
        /// arm coming away from the body (the old armReach slid it 0.17-0.38). Anything a fully
        /// extended arm still can't reach moves the whole figure instead (_reachShift).
        /// </summary>
        const float MaxShoulderShove = 0.04f;

        /// <summary>
        /// Which way the elbows bend when a swing pulls the hand in toward the shoulder: +1 turns
        /// the forearm toward the facing, the way the walk's own elbow bends - the elbow drops
        /// under a forward hand and points forward under a raised one. Fixed rather than chosen
        /// per frame, because an elbow that picks the nearer solution flips sides mid-swing.
        /// </summary>
        const float ArmBendSign = 1f;

        /// <summary>
        /// The figure's own drawing-only shift this frame, in the TORSO's frame: what the near arm
        /// could not reach even fully extended and shoved (an Impale's lunge). Recomputed every
        /// frame, zero at rest. A plain Vector2, so a domain reload keeps it.
        /// </summary>
        Vector2 _reachShift;

        /// <summary>
        /// The near hand's grip in the ELBOW's frame: where the weapon pivot hangs (so the blade
        /// lands where it was authored), or the fist when the hand is empty.
        /// </summary>
        Vector2 FrontGripPoint => ((Vector2)_weaponGripRest).sqrMagnitude > 1e-6f
            ? (Vector2)_weaponGripRest : CarryGrip;

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>
        /// Two-bone reach: the shoulder and elbow angles that put the hand point at
        /// <paramref name="target"/> (measured from the shoulder's REST position, in its parent's
        /// frame). <paramref name="upper"/> is the elbow in the arm's frame and
        /// <paramref name="hand"/> the hand point in the elbow's, both at zero rotation, so any
        /// offset off the arm's axis is honoured. Out of reach, the shoulder is shoved toward the
        /// target up to MaxShoulderShove and the rest's HORIZONTAL part comes back as
        /// <paramref name="figure"/>, for the whole figure to travel; too close, the elbow folds
        /// as far as it goes.
        /// </summary>
        static void SolveArmReach(Vector2 upper, Vector2 hand, Vector2 target, float bendSign,
                                  out float shoulder, out float elbow, out Vector2 shove, out Vector2 figure)
        {
            float lu = upper.magnitude, lh = hand.magnitude, reach = lu + lh;
            float dist = target.magnitude;
            Vector2 dir = dist > 1e-6f ? target / dist : Vector2.down;

            shove = Vector2.zero;
            figure = Vector2.zero;
            if (dist > reach)
            {
                // The shoulder first, a little; then the figure, but only ALONG THE GROUND - a
                // body lunges forward, it does not sink into the floor (a low grip once dropped
                // the feet through it). Whatever is still out of reach after that is left short:
                // the arm points straight at it and the blade keeps its angle.
                float over = dist - reach;
                shove = dir * Mathf.Min(over, MaxShoulderShove);
                figure = new Vector2((dir * (over - shove.magnitude)).x, 0f);
                target -= shove + figure;
                dist = Mathf.Min(target.magnitude, reach);
                dir = target.magnitude > 1e-6f ? target.normalized : dir;
            }

            float AngleOf(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            if (lu < 1e-6f || lh < 1e-6f)
            {
                elbow = 0f;
                shoulder = AngleOf(dir) - AngleOf(upper + hand);
                return;
            }

            float c = Mathf.Clamp((dist * dist - lu * lu - lh * lh) / (2f * lu * lh), -1f, 1f);
            elbow = Mathf.DeltaAngle(0f, AngleOf(upper) - AngleOf(hand) + bendSign * Mathf.Acos(c) * Mathf.Rad2Deg);
            shoulder = AngleOf(dir) - AngleOf(upper + Rotate(hand, elbow));
        }

        /// <summary>
        /// Whether the carry is in arm.back this frame.
        ///
        /// FOLLOWS THE MIRROR: facing left is facing right reflected, carry, drape and saya
        /// included. It used to read `_facingLeft != _facingAway` to keep the carry in the
        /// character's anatomical RIGHT hand - which swapped arms on every turn so the sword,
        /// cape drape and saya landed exactly where they were on screen, and with a symmetric
        /// torso the only thing that visibly turned was the head. Turning AWAY still swaps it.
        /// </summary>
        bool CarryInBackArm => _facingAway;

        /// <summary>Last value of <see cref="CarryInBackArm"/> the layer order was built for.</summary>
        bool _carryInBack;

        /// <summary>
        /// How much of the carrying shoulder's raise the pauldron over it follows, 0..1.
        ///
        /// A pauldron is strapped to the top of the upper arm, so it rides up when the arm does -
        /// but it is also a plate over the joint, so it turns LESS than the limb under it rather
        /// than swinging with it bone for bone. Left on the torso, the plate stayed flat across
        /// the shoulder while the arm rose out from under it, and at these proportions it
        /// covered the whole upper arm: the elbow the pose is built around disappeared.
        ///
        /// 0.25 because 0.25 x CarryShoulderDegrees is 30 - exactly the upper arm's own rise above
        /// horizontal - so the plate comes to lie ALONG the upper arm, a cap on the segment it is
        /// strapped to, and the elbow and forearm stand clear past its end. Compared on screen
        /// against 0, 0.4, 0.6 and 1: flat, the arm slides out from under a plate that ignores
        /// it; much past 0.25 the plate stands up beside the head like a slab.
        ///
        /// That is a CAP's answer. A plate that already hangs DOWN the upper arm - the Shogun's
        /// sode, chin to elbow - lies along the arm at rest, so it has to turn with the whole
        /// raise to stay there; at a quarter it hung off the shoulder while the arm lifted away.
        /// The item says which it is (GearItem.HangsAlongArm), since the rig cannot tell a cap
        /// from a hanging plate by its sprite. See _pauldronFollow.
        /// </summary>
        const float PauldronFollow = 0.25f;

        /// <summary>The follow the equipped pauldrons take - PauldronFollow for a cap, the whole
        /// raise for a plate that hangs along the arm. Set by Apply.</summary>
        float _pauldronFollow = PauldronFollow;

        /// <summary>
        /// Where Apply placed each pauldron layer, torso-local - what the carry orbits from. Kept
        /// per LAYER, and matched to a shoulder by the SIGN of x rather than by layer name:
        /// the catalogue is not consistent about which of Shoulders/ShouldersBack sits on which
        /// side (gilded puts Shoulders on -X, the others on +X).
        /// </summary>
        Vector3 _shouldersRest, _shouldersBackRest;

        /// <summary>The worn cape's mass colour, which DyedByBack cloth takes - set by Apply. A
        /// Color rather than the ramp built from it: a plain Color survives a domain reload, the
        /// ramp (an unserialisable struct) would come back black.</summary>
        Color _clothDye;
        bool _clothDyed;

        /// <summary>
        /// Where Apply placed <see cref="RigLayer.BackOver"/>, torso-local, as AUTHORED - on the
        /// character's right in the default facing. <see cref="SyncDrapeSide"/> mirrors it from
        /// here whenever the right shoulder is on the other side of the rig.
        /// </summary>
        Vector3 _backOverRest;

        /// <summary>Where Apply placed <see cref="RigLayer.NeckOver"/>, as authored - mirrored by
        /// <see cref="SyncDrapeSide"/> exactly as <see cref="_backOverRest"/> is.</summary>
        Vector3 _neckOverRest;

        /// <summary>The worn neck item's <see cref="GearItem.ShroudOverCloak"/> - set by Apply,
        /// read by ApplyLayerOrder. A plain bool, so it survives a reload.</summary>
        bool _shroudOverCloak;

        /// <summary>
        /// Where Apply placed <see cref="RigLayer.Trinket"/>, torso-local, as authored - on the
        /// character's LEFT hip in the default facing - and whether the relic there asks to STAY
        /// on the left hip (<see cref="GearItem.WornOnLeftHip"/>). <see cref="SyncTrinketSide"/>
        /// mirrors it from here, by the same rule SyncDrapeSide uses.
        /// </summary>
        Vector3 _trinketRest;
        bool _trinketLeftHip;

        /// <summary>
        /// The equipped Back item's drape seen from behind (<see cref="GearItem.DrapeBack"/>),
        /// and the front sprite it stands in for while turned away - the same pair, for the
        /// same reason, as <see cref="_hoodBack"/>/<see cref="_hoodFront"/>. Set fresh by Apply.
        /// </summary>
        Sprite _drapeBack, _drapeFront;

        /// <summary>
        /// The grip position Apply placed the weapon at, in its elbow's frame - what the carry
        /// eases back to. Written every Apply, read every frame.
        /// </summary>
        Vector3 _weaponGripRest;

        /// <summary>
        /// How much of the aim residual the front arm still carries at full rest, as a fraction.
        ///
        /// The arm normally takes the WHOLE aim angle (the torso does not rotate - see FaceAim),
        /// and at ArmAimRange 45 degrees that is comparable to the carry angle itself: aiming
        /// down-right cancelled almost all of the 32 and stood the blade back up vertical, which
        /// is the exact pose the carry exists to replace. Damped, not zeroed - an arm that
        /// ignores aim completely reads as detached from where the character is looking.
        /// </summary>
        const float ShoulderAimFraction = 0.3f;

        /// <summary>
        /// How far the free arm splays AWAY from the body at full rest, degrees.
        ///
        /// Not zero, and that is the whole point. The torso is 24 texels wide and the arm is 8,
        /// hanging from a shoulder 12 out - so an arm at a true 0 hangs flush against the torso's
        /// own edge and disappears into it. Nothing is wrong with the pose at that angle; there is
        /// simply no silhouette, which is indistinguishable from the arm not hanging at all.
        ///
        /// Positive swings the hand outward, away from the centre line: the rig's Z rotation is
        /// counter-clockwise and this arm sits on +X, so a NEGATIVE angle here tucks it across the
        /// belly instead, which is exactly the failure this constant exists to correct.
        /// </summary>
        const float ShoulderOffArmDegrees = 14f;

        /// <summary>
        /// How far the back arm swings OUT while it holds the pair's second disc, degrees - on top
        /// of the walk's own swing. Positive is outward for this arm (see ShoulderOffArmDegrees).
        /// Enough to carry the disc's middle clear of the torso's edge.
        /// </summary>
        const float SplitOffArmDegrees = 22f;
        const float SplitOffArmDegreesPerSecond = 160f;
        /// <summary>The splay the back arm has NOW - zero in every pose but the disc stance, since
        /// the pauldron over that arm follows it (FollowPauldron).</summary>
        float _offSplay;



        /// <summary>
        /// How quickly the grip opens and closes between the rest carry and the combat grip.
        ///
        /// Slower than PoseRecoverySharpness deliberately. The off hand does not rotate onto the
        /// hilt, it RELOCATES across the body's centre line (see ArmBackGripRest) - a travel long
        /// enough to read as an arm moving rather than an arm teleporting.
        /// </summary>
        const float GripBlendSharpness = 11f;

        /// <summary>
        /// How quickly the grip leaves the carry once a swing or charge starts. Much faster than
        /// <see cref="GripBlendSharpness"/>, which is the RETURN: at 11 a third of the carry was
        /// still blended into the arm when a Chop's strike began, so the arc never hit its real
        /// angles. At 30 the grip is ~98% closed by the end of the Chop's cock (ChopCockSeconds),
        /// which is where the hand-off out of the carry belongs.
        /// </summary>
        const float GripToSwingSharpness = 30f;

        /// <summary>
        /// Seconds after a swing or charge ends before the grip starts back for the carry. A chain
        /// leaves a gap between swings (the animation fills 85% of each interval, and a HELD attack
        /// waits HoldExtraDelay on top), and starting the return in that gap swung the arm partway
        /// to the shoulder and snapped it back on the next swing - the near pauldron flickered out
        /// from behind the arm for a frame between every basic. Longer than any gap in a chain,
        /// short enough that stopping still reads as letting go.
        /// </summary>
        const float CarryReturnDelay = 0.2f;

        /// <summary>Seconds since the last swing or charge ended - see CarryReturnDelay. A plain
        /// float, so a domain reload keeps it. Starts past the delay so a new rig rests carried.</summary>
        float _sinceSwing = CarryReturnDelay;

        /// <summary>
        /// Whether the character is ASKING for the rest carry - see SetGripShouldered. Not the
        /// same as what is drawn: a swing overrides it outright, and _gripBlend is what the pose
        /// actually rides on.
        ///
        /// Defaults to TRUE - every sword rests on the shoulder. It used to default false, and
        /// every rig nobody explicitly asked (the hub avatar, the couch) stood in the both-hands
        /// grip, which from behind put the fists and hilt over the cape.
        /// </summary>
        [SerializeField] bool _gripShouldered = true;

        /// <summary>
        /// 0 = both hands on the hilt, 1 = the shouldered rest carry, eased between.
        ///
        /// [SerializeField] and non-readonly like every other cached field here: a domain reload
        /// that lost this would drop the character into the combat grip with no swing running and
        /// nothing to ease it back. See CLAUDE.md's domain reload traps.
        ///
        /// Starts AT the carry, which is where every rig begins (_gripShouldered defaults on).
        /// From 0 a new rig spent its first third of a second swinging the blade up onto the
        /// shoulder - and the character preview measured its frame off that moving blade on the
        /// frame it opened, then re-measured on the next refresh and jumped.
        /// </summary>
        [SerializeField] float _gripBlend = 1f;

        /// <summary>0 = rest carry, 1 = march carry (blade shouldered behind). Eased.</summary>
        float _march;

        /// <summary>Whether the layer order last applied was the march's.</summary>
        bool _marchOrdered;

        /// <summary>
        /// Whether the wrist has rolled the blade over for the march carry: shouldered, the OTHER
        /// edge faces up (the user's call). A 2D rotation can't show a turn about the blade's own
        /// long axis, so it is the weapon mirrored across that axis - an exact -1, never a scale
        /// through 0, which would resample point-filtered art (see "landing" on why the rig never
        /// squashes). Latched with hysteresis in AdvanceMarch.
        /// </summary>
        bool _marchRolled;

        float CarryShoulderNow => Mathf.Lerp(CarryShoulderDegrees, MarchShoulderDegrees, _march);
        float CarryElbowNow => Mathf.Lerp(CarryElbowDegrees, MarchElbowDegrees, _march);
        float CarryWeaponNow => Mathf.Lerp(CarryWeaponDegrees, MarchWeaponDegrees, _march);

        /// <summary>
        /// The carry's elbow as the GRIP blend scales it toward the swing's straight arm: the
        /// SHORT way round. The march's elbow is 260 degrees, and scaled from there to 0 it folded
        /// back through 180 - the forearm swept a whole half-turn and the blade spun round past
        /// the hip and back up before the swing could start. Taken as -100 instead, the forearm
        /// lifts and the blade stays laid back over the shoulder, which is within a few degrees of
        /// where a Chop cocks (blade 74 against the cock's 75-87): the march flows into the opener.
        ///
        /// Latched (_carryElbowUnwound) rather than wrapped every frame: the march can cross 180
        /// while the grip is part-open, and a fresh wrap there would jump the elbow by
        /// _gripBlend x 360. It is re-decided only while the carry is fully held, where both
        /// readings are the same angle.
        /// </summary>
        float CarryElbowHeld => CarryElbowNow - (_carryElbowUnwound ? 360f : 0f);

        /// <summary>See <see cref="CarryElbowHeld"/>. A plain bool, so a domain reload keeps it.</summary>
        bool _carryElbowUnwound;


        /// <summary>
        /// Ease <see cref="_march"/> toward the march carry while walking sideways facing the
        /// camera, scaled by the size the walk is HEADING for so a stop settles back to rest -
        /// not by _gaitWeight itself, which eases on its own clock and doubled the lag.
        /// </summary>
        void AdvanceMarch(float dt, float speed)
        {
            float sideways = speed > 0.01f && Body != null ? Mathf.Abs(Body.linearVelocity.x) / speed : 0f;
            float want = _facingAway ? 0f
                : GaitWeightWanted(speed) * Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(MarchSidewaysFrom, MarchSidewaysFull, sideways));
            float sharp = want > _march ? MarchInSharpness : MarchOutSharpness;
            _march = Mathf.Lerp(_march, want, 1f - Mathf.Exp(-sharp * dt));

            if (_march > MarchRollOn) _marchRolled = true;
            else if (_march < MarchRollOff) _marchRolled = false;

            bool ordered = _march > MarchOrderFrom;
            if (ordered != _marchOrdered)
            {
                _marchOrdered = ordered;
                if (_shoulderCarry) ApplyLayerOrder();
            }
        }

        /// <summary>
        /// The weapon's pivot, cached as a TRANSFORM rather than re-read from _pivots each frame.
        ///
        /// WeaponAnchor reads the _pivots Dictionary, and a domain reload empties that while every
        /// transform under the rig survives intact - the exact trap CLAUDE.md lists. A rest carry
        /// that read it directly would silently stop rotating the weapon after any script edit in
        /// Play mode, with the character still drawing perfectly and nothing pointing at why. A
        /// Transform field is a UnityEngine.Object reference, so it comes back.
        /// </summary>
        [SerializeField] Transform _weaponPivot;
        float _chargeTimer, _chargeLength = 1f;
        float _airHeight;     // world units the figure is lifted off the ground; 0 = standing
        Vector2 _phantomOffset;   // Phantom's poof - drawing-only, see SetPhantomOffset
        Color _accent = Color.white;
        ElementType _element = ElementType.Fire;

        public int BaseSortingOrder = SortingOrders.Character;

        /// <summary>Lift at which the airborne shadow reaches its tightest, darkest point.</summary>
        const float AirborneShadowFull = 6f;

        [Tooltip("Rotate the whole body freely to the aim, as a top-down game does. Off gives the " +
                 "3/4 read: mirror left/right with a clamped lean, which is what keeps gear legible.")]
        public bool TopDown = false;

        [Tooltip("3/4 mode only. How far the arms swing to cover a high or low aim.")]
        public float ArmAimRange = 45f;

        [Tooltip("How quickly the body settles into a new facing. Higher snaps.")]
        public float TurnSharpness = 16f;

        [Tooltip("Horizontal aim below this keeps the previous left/right mirror, so aiming " +
                 "straight up or down does not flip the character back and forth.")]
        public float MirrorDeadzone = 0.12f;

        [Tooltip("A relaxed at-ease idle for the loadout preview: while standing still the arms " +
                 "ease a few degrees off the centre line so the belt and other waist gear are not " +
                 "hidden behind hanging fists. Never set in combat - the fighting stance is unchanged.")]
        public bool ShowcasePose;

        /// <summary>
        /// This frame's step. A showcase rig lives inside a screen that holds the pause, so on
        /// scaled time it never animated at all: it stood in whatever the pose fields defaulted
        /// to, arms straight down inside the torso, and a toggle like hiding the weapon changed
        /// nothing until the screen closed.
        /// </summary>
        float Dt => ShowcasePose ? Time.unscaledDeltaTime : Time.deltaTime;

        /// <summary>
        /// The hands-free showcase pose: each shoulder swings out this far, degrees. Chosen from
        /// three tried side by side - at 28 the pose read as staged, and a fists-on-hips version
        /// (45 out, 90 at the elbow) put the fists on the chest, because these arms are too short
        /// to reach the hips that way.
        /// </summary>
        const float ShowcaseShoulderDegrees = 20f;

        /// <summary>How far each forearm folds back in toward the hip in that pose, degrees.</summary>
        const float ShowcaseElbowDegrees = 12f;

        /// <summary>
        /// 0..1: how far the arms are into the hands-free showcase pose. Eased, because it turns
        /// on and off with the weapon toggle and the elbow has no other pose field to ease it.
        /// </summary>
        float _handsFree;

        /// <summary>Nothing drawn in either hand: the weapon is hidden, or there is none.</summary>
        bool HandsEmpty => _weaponHiddenPref
            || !_layers.TryGetValue(RigLayer.Weapon, out var w) || w == null || w.sprite == null;


        public Transform Transform => transform;

        public static PrimitiveCharacterRig Build(GameObject root, ElementType element, int sortingOrder = SortingOrders.Character)
        {
            var go = new GameObject("visual");
            go.transform.SetParent(root.transform, false);
            var rig = go.AddComponent<PrimitiveCharacterRig>();
            rig.BaseSortingOrder = sortingOrder;
            rig.Construct(element);
            return rig;
        }

        void Construct(ElementType element)
        {
            _element = element;
            _accent = ElementInfo.Tint(element);

            // Pivot hierarchy: limbs rotate about a shoulder/hip, so the sprite hangs below it.
            //
            // HIPS ARE SEPARATE FROM THE TORSO ON PURPOSE. The legs used to hang off the torso,
            // and the aim lean was applied to the whole rig - so turning tipped the entire body
            // about a waist-height pivot and swung the feet off the ground. It read as floating,
            // not turning. Now the hips stay planted and only the upper body rotates, which is
            // what a person actually does.
            // Legs hang from the hip joint at the waist; arms from the shoulder at the TOP of the
            // torso; the head from the neck joint, which is the torso's top edge - so the head
            // rotates about where it meets the body rather than about its own middle.
            _hips = MakePivot("hips", HipsRest, transform);
            _legBack = MakePivot("leg.back", PixelSprite.Px(-Proportions.HipHalfSpanCells, 0f), _hips);
            _legFront = MakePivot("leg.front", PixelSprite.Px(Proportions.HipHalfSpanCells, 0f), _hips);

            _torso = MakePivot("torso", TorsoRest, transform);
            // The weapon arm sits on the side AWAY from the aim (rig-local +X is forward, toward
            // the target), so every swing winds up on the far shoulder and travels ACROSS the body
            // into the target instead of starting already pointed at it. A near-side sword only has
            // room for a short jab; a cross-body arc is what reads as a real swing with weight.
            _armBack = MakePivot("arm.back",
                PixelSprite.Px(Proportions.ShoulderHalfSpanCells, Proportions.ShoulderYCells), _torso);
            _armFront = MakePivot("arm.front",
                PixelSprite.Px(-Proportions.ShoulderHalfSpanCells, Proportions.ShoulderYCells), _torso);

            // The head rides the torso but counter-rotates, so a turn reads as shoulders coming
            // round rather than the whole character keeling over.
            _head = MakePivot("head", HeadRest, _torso);

            _armFrontRest = _armFront.localPosition;
            _armBackRest = _armBack.localPosition;
            EnsureElbows();

            CheckLayerOrders();

            foreach (var layer in RigLayers.All)
                _layers[layer] = MakeLayer(layer);

            BuildShadow();
            DrawPlaceholderBody();
        }

        /// <summary>
        /// A contact patch under the feet. Nothing else in the scene says where the character is
        /// standing - without it a humanoid drawn in 3/4 has no anchor to the floor at all, and
        /// every bit of secondary motion reads as drift.
        /// </summary>
        void BuildShadow()
        {
            var go = new GameObject("shadow");

            // Parented OUTSIDE the rig root, next to it. As a child it inherited the root's spin,
            // and countering the rotation was not enough - the child's POSITION swung round the
            // pivot too, so during a spin finisher the shadow orbited the character instead of
            // staying under the feet. Ground does not move.
            go.transform.SetParent(transform.parent != null ? transform.parent : transform, false);
            // From above the character stands ON its own footprint, so the patch sits under the
            // body rather than below the feet.
            // The foot line, READ from the proportions rather than restated. It was the literal
            // -34 for a long time, which is what SoleYCells still evaluates to for the build that
            // number was written against - but the moment the legs were trimmed it put the contact
            // patch below the feet, with nothing anywhere to say the two had parted company.
            go.transform.localPosition = TopDown ? Vector3.zero
                                                 : new Vector3(0f, PixelSprite.Px(Proportions.SoleYCells), 0f);
            go.transform.localScale = TopDown ? new Vector3(0.52f, 0.44f, 1f)
                                              : new Vector3(0.46f, 0.17f, 1f);

            _shadow = go.AddComponent<SpriteRenderer>();
            _shadow.sprite = Spr.Circle;
            _shadow.color = new Color(0f, 0f, 0f, 0.32f);
            _shadow.sortingOrder = SortingOrders.GroundDecal + 2;
        }

        Transform MakePivot(string name, Vector2 local, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            return go.transform;
        }

        /// <summary>Each layer hangs off whichever pivot animates it.</summary>
        Transform PivotFor(RigLayer layer) => layer switch
        {
            RigLayer.ArmBack or RigLayer.GlovesBack                        => _armBack,
            RigLayer.ArmFront or RigLayer.GlovesFront                      => _armFront,
            // Everything from the elbow down. The ring and the weapon are in the HAND, so they
            // ride the forearm; their authored offsets are still shoulder-relative - see
            // HungFromElbow, which is what converts them.
            RigLayer.ArmBackLower or RigLayer.GlovesBackLower
                or RigLayer.ArmBackHand or RigLayer.GlovesBackHand         => _elbowBack,
            RigLayer.ArmFrontLower or RigLayer.GlovesFrontLower
                or RigLayer.ArmFrontHand or RigLayer.GlovesFrontHand
                or RigLayer.Ring or RigLayer.Weapon                        => _elbowFront,
            RigLayer.LegBack or RigLayer.LegsBack or RigLayer.BootsBack    => _legBack,
            RigLayer.LegFront or RigLayer.LegsFront or RigLayer.BootsFront => _legFront,
            // Everything from the knee down - cut there at paint time, see PaintKneeSplit.
            RigLayer.LegBackLower or RigLayer.LegsBackLower
                or RigLayer.BootsBackLower                                 => _kneeBack,
            RigLayer.LegFrontLower or RigLayer.LegsFrontLower
                or RigLayer.BootsFrontLower                                => _kneeFront,
            // Hair hangs off the NECK JOINT with the head, not off the torso, so a head turn
            // carries the length with it instead of leaving it pinned to the shoulders.
            RigLayer.Head or RigLayer.HeadBack or RigLayer.HeadArmor or RigLayer.HairBack
                or RigLayer.Hood                                           => _head,
            _                                                              => _torso,
        };

        /// <summary>
        /// Each sorting table must be an exact permutation of 0..N-1, and nothing about a broken
        /// one is visible on screen: a duplicated order silently drops whichever layer loses the
        /// tie, and a missing one leaves a gap nobody notices. Checked once per rig, in the editor
        /// only - it is pure arithmetic over three static arrays, so a build gains nothing by
        /// re-running it.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        static void CheckLayerOrders()
        {
            int n = RigLayers.All.Length;
            Check(OneHandedOrder, nameof(OneHandedOrder));
            Check(DualWieldOrder, nameof(DualWieldOrder));
            Check(TwoHandedOrder, nameof(TwoHandedOrder));
            Check(WeaponBehindOrder, nameof(WeaponBehindOrder));
            Check(ShoulderCarryOrder, nameof(ShoulderCarryOrder));
            Check(FacingAwayOrder, nameof(FacingAwayOrder));
            Check(FacingAwayHeldOrder, nameof(FacingAwayHeldOrder));
            Check(FacingAwayFrontCarryOrder, nameof(FacingAwayFrontCarryOrder));
            Check(HandsUpOrder, nameof(HandsUpOrder));
            Check(ArmsRaisedOrder, nameof(ArmsRaisedOrder));

            void Check(int[] table, string name)
            {
                if (table.Length != n)
                {
                    Debug.LogError($"[Rig] {name} has {table.Length} entries for {n} RigLayers.");
                    return;
                }
                var seen = new bool[n];
                foreach (int o in table)
                {
                    if (o < 0 || o >= n) { Debug.LogError($"[Rig] {name} has order {o}, outside 0..{n - 1}."); return; }
                    if (seen[o]) { Debug.LogError($"[Rig] {name} uses order {o} twice - a layer will vanish."); return; }
                    seen[o] = true;
                }
            }
        }

        SpriteRenderer MakeLayer(RigLayer layer)
        {
            var go = new GameObject(layer.ToString());
            var pivot = PivotFor(layer);
            go.transform.SetParent(pivot, false);
            _pivots[layer] = go.transform;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = BaseSortingOrder + (int)layer;
            sr.enabled = false;
            return sr;
        }

        // ---------------------------------------------------------------- placeholder body

        /// <summary>
        /// A featureless humanoid drawn from primitives, so the rig is visible and animating
        /// before any art exists. Replaced wholesale the moment body art is authored.
        /// </summary>
        void DrawPlaceholderBody()
        {
            var skin = new Color(0.82f, 0.72f, 0.62f);
            var cloth = Color.Lerp(_accent, new Color(0.2f, 0.2f, 0.25f), 0.45f);
            var clothDark = Color.Lerp(cloth, Color.black, 0.35f);

            // The overhead read keeps the old capsules - it is a fallback mode and its gear
            // offsets were never authored, so there is nothing for pixel art to sit on yet.
            if (TopDown)
            {
                // Seen from above: shoulders across, head on top of them, arms reaching FORWARD
                // (+Y) so the weapon leads the turn. Read from overhead a character is mostly
                // shoulders and whatever they are holding, which is also what makes gear legible
                // from this angle.
                SetBody(RigLayer.Torso,    Spr.Capsule, cloth,
                        new Vector2(0f, -0.02f), new Vector2(0.40f, 0.30f));
                SetBody(RigLayer.LegBack,  Spr.Capsule, Color.Lerp(clothDark, Color.black, 0.3f),
                        new Vector2(-0.09f, -0.20f), new Vector2(0.13f, 0.22f));
                SetBody(RigLayer.LegFront, Spr.Capsule, clothDark,
                        new Vector2(0.09f, -0.20f), new Vector2(0.13f, 0.22f));
                SetBody(RigLayer.ArmBack,  Spr.Capsule, Color.Lerp(skin, Color.black, 0.25f),
                        new Vector2(-0.02f, 0.18f), new Vector2(0.11f, 0.26f));
                SetBody(RigLayer.ArmFront, Spr.Capsule, skin,
                        new Vector2(0.02f, 0.18f), new Vector2(0.11f, 0.26f));
                SetBody(RigLayer.Head,     Spr.Circle,  skin,
                        new Vector2(0f, 0.04f), new Vector2(0.26f, 0.26f));
                return;
            }

            DrawPixelBody();
        }

        /// <summary>
        /// Place a pixel sprite at its NATIVE size.
        ///
        /// Deliberately not an overload of <see cref="SetBody"/>: that one assigns
        /// <c>localScale = size</c> directly, which is only correct because every Spr sprite is
        /// exactly 1x1 world units so scale and size coincide. A PPU-matched pixel sprite is
        /// already its own size, so scale must stay (1,1) - passing a size here would shrink the
        /// body by a factor of three and read as "the character vanished". Two names, two rules.
        /// </summary>
        /// <summary>
        /// The character, drawn as pixel art. Grids read top-row-first, exactly as they look.
        ///
        /// Every part is sized to land within a screen pixel of the capsule body it replaces, so
        /// the outline contract the rest of the game is tuned against - head top ~0.53, feet
        /// ~-0.39, arms to ~+/-0.20 - survives unchanged. That is what lets the gear offsets, the
        /// contact shadow and the collider stay exactly as they were.
        ///
        /// Widths are even on purpose; see the mirror note in PixelSprite.
        /// </summary>
        /// <summary>
        /// The player's chosen body. Never null - a character with no saved appearance still has
        /// to render, and defaulting here rather than at every read is what keeps the preview rig
        /// and the arena rig from disagreeing about what "unset" looks like.
        /// </summary>
        Chain.Appearance _look = new();

        /// <summary>
        /// Repaint the body for a new appearance. Gear is untouched - <see cref="Apply"/> owns
        /// that, and the two are deliberately independent so a transmog never has to know what
        /// colour someone's eyes are.
        /// </summary>
        public void SetAppearance(Chain.Appearance look)
        {
            string skinWas = _look.Skin;
            _look = look ?? new Chain.Appearance();

            // EnsureLayers FIRST, not a bare _pivots.Count check - the same trap _layers already
            // has a documented fix for. _pivots is a readonly Dictionary, so a domain reload
            // during play (editing any script while the character or transmutation screen is
            // open) empties it while every layer GameObject survives untouched - the count check
            // alone then reads as "the rig isn't built yet" and silently skips the repaint,
            // exactly the "looks fine, refuses to change" symptom EnsureLayers exists to catch.
            EnsureLayers();
            if (_pivots.Count > 0) DrawPixelBody();

            // The one place the two DO meet: gear that leaves skin bare is painted in this tone
            // (GearItem.ShowsSkin), so a new tone repaints it.
            if (_lastLoadout != null && skinWas != _look.Skin && WearsSkin(_lastLoadout)) Apply(_lastLoadout);
        }

        static bool WearsSkin(Loadout loadout)
        {
            foreach (var entry in loadout.Equipped)
                if (GearCatalog.Get(entry.ItemId) is { ShowsSkin: true }) return true;
            return false;
        }

        /// <summary>
        /// Paint just the Head layer - the one piece of the body Apply also has reason to touch,
        /// via <see cref="_glowLeftEye"/>. Pulled out of <see cref="DrawPixelBody"/> rather than
        /// left inline so a gear change can repaint the face without repainting the other eleven
        /// body parts, which SetAppearance's own doc is explicit must stay independent of gear.
        /// </summary>
        void RepaintHead(Palette.Ramp skin, Palette.Ramp hair, Palette.Ramp eyes, Palette.Ramp brow)
        {
            // THE HEAD IS THE ONE PART WITH A SECOND DENSITY, and only on the character screen.
            //
            // BOTH go through the same detail pass (BodyLook.HeadDetail) - the arena at 1x, the
            // character screen at DetailScale. The pass was built when the menu drew at 150,
            // which is now the body's own density, so the arena spends its grid's cells on the
            // same rules (smoothed outline, lit skull, hair highlight, a real eye) instead of on
            // a block-double. It adds no FEATURE - see the Expressions header - so the old
            // arena objection to a busier face does not re-apply.
            //
            // The character screen still goes 2x past that: it magnifies a body texel to around
            // ten screen pixels, the case the whole `MenuLayers` mechanism exists for.
            //
            // Measured cost: at CameraSize 4.8 a 150-ppu texel is 0.75 screen px at 1080p and 1.5
            // at 4K - fractional both ways - so the eye's lash and glint shimmer as the character
            // moves. Only 1440p lands whole (1:1). See PixelPerfectZoom for why the camera cannot
            // simply snap to fix it.
            bool fine = _detailArt;
            // Covered by a hood or helm, the hair that rises off the skull is clipped - see
            // BodyLook.ClipOverhead. In the key, or a covered head would be served bare.
            string cov = _headCovered ? ".cov" : "";
            var headSprite = fine
                    // TURNED, like the arena's. The body is turned in the character screen too, and
                    // a face-on head on it was the one place the old camera-facing look survived.
                    ? PixelSprite.From(BodyLook.DetailKey(_look, _glowLeftEye) + ".g" + GazeShift + cov,
                        Covered(BodyLook.HeadDetail(_look.Expression, _look.HairStyle, _look.Beard,
                                                    _look.Brows, _look.Hair, _glowLeftEye,
                                                    gaze: GazeShift),
                                BodyLook.DetailScale),
                        BodyLook.HeadPalette(skin, hair, eyes, brow),
                        pixelsPerUnit: bodyPpu * BodyLook.DetailScale)
                    // The ARENA runs the same detail pass at 1x rather than drawing the flat grid:
                    // the composed head is a block-double of what the features were authored on,
                    // so the pass has two texels per authored cell to spend - what the menu face
                    // was first built with, back when 150 was the menu's density rather than the
                    // body's. Same grid size and world size as the flat head it replaces.
                    : PixelSprite.From("arena." + BodyLook.HeadKey(_look, _glowLeftEye, GazeShift) + cov,
                        Covered(BodyLook.HeadDetail(_look.Expression, _look.HairStyle, _look.Beard,
                                                    _look.Brows, _look.Hair, _glowLeftEye,
                                                    scale: 1, gaze: GazeShift), 1),
                        BodyLook.HeadPalette(skin, hair, eyes, brow),
                        pixelsPerUnit: bodyPpu);

            SetBodyPixel(RigLayer.Head, headSprite, new Vector2(0, ChinOnNeckline(headSprite)));
            // SetBodyPixel switches the layer on; turned away the face stays off (SetFacingAway) -
            // or hiding the helm from behind put the face on the back of the head.
            if (_facingAway) SetLayerEnabled(RigLayer.Head, false);
            EncloseHeadInHelm();

            SyncGlowEyeSprite();
        }

        /// <summary>The head as painted, and the copy cut to a sealed helm that stands in for it -
        /// see <see cref="EncloseHeadInHelm"/>. Sprites, so a domain reload keeps them.</summary>
        Sprite _headOpen, _headClipped;

        /// <summary>
        /// Under a SEALED helm (<see cref="GearItem.SealsHead"/>), the head is cut to the helm's
        /// outline (<see cref="PixelSprite.Enclosed"/>, the hood's rule for a helm): ClipOverhead
        /// only blanks the rows above the skull, and the hair at the skull's top corners still
        /// poked out where a round dome narrows. The face opening is inside the outline, so the
        /// face is kept. Swaps the sprite only - never the renderer's enabled state, which the
        /// facing owns - and gives the uncut head back when the helm is hidden or taken off.
        /// Measured against the FRONT helm (the head is off while turned away).
        /// </summary>
        void EncloseHeadInHelm()
        {
            if (!_layers.TryGetValue(RigLayer.Head, out var head) || head == null || head.sprite == null) return;
            if (_headClipped != null && head.sprite == _headClipped && _headOpen != null) head.sprite = _headOpen;
            _headOpen = head.sprite;
            _headClipped = null;

            if (!_headSealed || _helmHidden) return;
            if (!_layers.TryGetValue(RigLayer.HeadArmor, out var helm) || helm == null) return;
            var frame = helm.sprite == _helmBack && _helmBack != null ? _helmFront : helm.sprite;
            if (frame == null) return;

            var cut = PixelSprite.Enclosed(head.sprite, frame,
                helm.transform.worldToLocalMatrix * head.transform.localToWorldMatrix);
            if (cut == null || cut == head.sprite) return;
            _headClipped = cut;
            head.sprite = cut;
        }

        string[] Covered(string[] head, int scale)
            => _headCovered ? BodyLook.ClipOverhead(head, scale) : head;

        /// <summary>
        /// Show/hide/reposition the pulsing overlay GlowingEye needs on top of the baked colour
        /// override - see that class's own doc for why the two are split. Always measured off the
        /// COARSE head grid, even in detail mode: the physical head is the same size at either
        /// density (see PixelSprite.cs's own "more pixels is free" note), so the coarse grid's
        /// texel coordinates convert to the same world position either way, and building it is
        /// cheap next to the sprite it feeds RepaintHead's own PixelSprite.From either way.
        /// </summary>
        void SyncGlowEyeSprite()
        {
            if (!_glowLeftEye)
            {
                _head.GetComponentInChildren<GlowingEye>(true)?.SetShown(false);
                return;
            }

            // With the gaze, because the head is drawn turned (arena and menu alike) and the eye
            // is not where an unturned grid would say.
            var coarse = BodyLook.Head(_look.Expression, _look.Beard, _look.Brows,
                                       leftEyeGlow: true, gaze: GazeShift);
            var box = BodyLook.FindLeftEyeBox(coarse);
            var glow = GlowingEye.Attach(_head);
            if (box == null) { glow.SetShown(false); return; }   // shouldn't happen; fail visible-off

            float w = coarse[0].Length, h = coarse.Length;
            float boxCx = (box.Value.x0 + box.Value.x1 + 1) * 0.5f;
            float boxCy = (box.Value.y0 + box.Value.y1 + 1) * 0.5f;
            // Offset from the HEAD SPRITE'S OWN CENTRE (its pivot, PixelSprite's default) in
            // texels - x grows right same as the grid; y is FLIPPED, since row 0 is the grid's
            // top row but a larger world Y is up.
            float offX = boxCx - w * 0.5f;
            float offY = h * 0.5f - boxCy;
            // The eye's offset is relative to the sprite's centre, so it adds directly to the
            // head-sprite offset RepaintHead itself passes to SetBodyPixel - ASKED of the sprite
            // it placed, not restated. This read a literal 14, which was that offset while the
            // head was drawn at LayoutUnit; the move to BodyPpu halved it and left the glow a
            // head-height up in the hair. The eye box is in HEAD texels, so it converts through
            // Cells for the same reason.
            _layers.TryGetValue(RigLayer.Head, out var headSr);
            float headCentre = ChinOnNeckline(headSr != null ? headSr.sprite : null);
            glow.SetLocalPosition(PixelSprite.Px(Proportions.Cells(offX),
                                                 headCentre + Proportions.Cells(offY)));
            // A repaint while turned away (an equip, hide-helm) must not bring it back on.
            glow.SetShown(!_facingAway);
        }

        /// <summary>
        /// Paint the body - the black bodysuit below the neck, plus the head.
        ///
        /// EVERYTHING BELOW THE NECK IS ONE MATERIAL (<see cref="Palette.Undersuit"/>, which
        /// carries the full reasoning). Torso, both arms and both legs used to take three
        /// unrelated ramps - the element's cloth, the player's skin tone, and a darkened cloth -
        /// and armour then had to sit convincingly on all three at once. It is one surface now,
        /// so the plates are the only thing the eye resolves and the black between them is the
        /// same black wherever it shows.
        ///
        /// THE SKIN TONE STILL MATTERS, just not here: the face is the only bare part of the
        /// figure, so <see cref="BodyLook.Skins"/> reads on the head alone. Nothing about the
        /// character picker changes; what changed is that a limb no longer competes with it.
        ///
        /// THE ELEMENT NO LONGER SHOWS ON THE BODY. <see cref="Palette.Cloth"/> was the only
        /// thing making a Fire character look unlike a Water one at a glance, and a black suit
        /// takes that away by construction. The accent has to come back somewhere - armour trim
        /// is the natural home now that armour is what the silhouette is made of - but it is a
        /// design decision rather than a consequence of this change, so it is NOT quietly
        /// reintroduced here as a tint on the suit. A "mostly black" suit is not a black suit.
        /// </summary>
        void DrawPixelBody()
        {
            var suit = Palette.Undersuit;
            var suitFar = Palette.UndersuitFar;
            var skin = BodyLook.SkinRamp(_look.Skin);
            var hair = BodyLook.HairRamp(_look.Hair);
            var eyes = BodyLook.EyeRamp(_look.Eyes);
            var brow = BodyLook.BrowRamp(_look);

            // Sprite keys no longer carry the element or the skin tone, because neither is drawn
            // below the neck any more. That is not just tidiness: a key naming a variable nothing
            // reads is a cache entry per element per skin for art that is byte-identical across
            // all of them, and the next person to read it would reasonably assume the body still
            // varies. The head's own keys still carry everything the head actually draws.
            SetBodyPixel(RigLayer.Torso,
                PixelSprite.From("body.torso.suit", Torso, Palette.Of(suit), pixelsPerUnit: bodyPpu),
                new Vector2(0, Proportions.Cells(Proportions.TorsoH) * 0.5f));

            RepaintHead(skin, hair, eyes, brow);
            DrawHeadBack(skin, hair);
            DrawMane(hair);

            SetBodyPixel(RigLayer.ArmFront,
                PixelSprite.From("body.arm.suit", Limb, Palette.Of(suit), pixelsPerUnit: bodyPpu),
                new Vector2(0, -Proportions.Cells(Proportions.ArmH) * 0.5f));
            SetBodyPixel(RigLayer.ArmBack,
                PixelSprite.From("body.arm.back.suit", Limb, Palette.Of(suitFar), pixelsPerUnit: bodyPpu),
                new Vector2(0, -Proportions.Cells(Proportions.ArmH) * 0.5f));
            PaintElbowSplit(RigLayer.ArmFront);
            PaintElbowSplit(RigLayer.ArmBack);

            SetBodyPixel(RigLayer.LegFront,
                PixelSprite.From("body.leg.suit", Leg, Palette.Of(suit), pixelsPerUnit: bodyPpu),
                new Vector2(0, -Proportions.Cells(Proportions.LegH) * 0.5f));
            SetBodyPixel(RigLayer.LegBack,
                PixelSprite.From("body.leg.back.suit", Leg, Palette.Of(suitFar), pixelsPerUnit: bodyPpu),
                new Vector2(0, -Proportions.Cells(Proportions.LegH) * 0.5f));
            PaintKneeSplit(RigLayer.LegFront);
            PaintKneeSplit(RigLayer.LegBack);
        }

        // The BODY's density - no longer the same as the gear's. See Proportions.BodyPpu for why
        // the two parted company, and why that is what reproportioned the figure without redrawing
        // a face. Gear stays at LayoutUnit until it is re-placed against the new outline.
        const float bodyPpu = Proportions.BodyPpu;

        /// <summary>
        /// Where to place a head sprite so its CHIN lands on the neckline, in layout cells from
        /// the head pivot.
        ///
        /// Derived from the sprite rather than typed, because the literal this replaces had
        /// already been wrong once: it carried the note "14, not 12 - the grid gained four rows of
        /// lower face and grows from its CENTRE, so half of that would have pushed the chin down
        /// into the torso." That is a correction anyone would have to redo by hand every time the
        /// head's composed row count changes - and the head is composed from four grid families
        /// whose heights are not obvious at the call site.
        ///
        /// The composed head is taller than <see cref="Proportions.HeadH"/>: BodyLook adds rows of
        /// lower face and overhead hair. So asking the sprite how tall it ACTUALLY came out is the
        /// only way this stays right, and it is what makes the density move safe - at BodyPpu the
        /// head is half the world size it was, and the chin still lands on the neckline with no
        /// second magic number.
        /// </summary>
        /// <summary>
        /// How far the face turns toward where the character is facing, in composed texels - see
        /// BodyLook.Gaze: the FAR half of the face moves this much, the near half this plus
        /// BodyLook.NearEyeLead, and the far eye is narrowed.
        ///
        /// POSITIVE IS TOWARD THE CHARACTER'S OWN FORWARD, always. The head sprite is never
        /// mirrored by itself; the whole rig root is, so a single constant here comes out facing
        /// correctly in both mirrored directions without a sign test.
        /// </summary>
        const int GazeShift = 2;

        static float ChinOnNeckline(Sprite head)
        {
            if (head == null) return 0f;

            // Sprite bounds are world units; the offset SetBodyPixel wants is layout cells.
            float halfHeightCells = head.bounds.size.y * PixelSprite.LayoutUnit * 0.5f;

            // The chin sits one overlap BELOW the head pivot, which is the torso's top edge - that
            // overlap is what fuses head and torso into one silhouette instead of leaving a neck.
            return halfHeightCells - Proportions.Cells(Proportions.NeckOverlap);
        }

        /// <summary>
        /// Paint <see cref="RigLayer.HeadBack"/> - see the field's own doc for why this exists at
        /// all. Called from <see cref="DrawPixelBody"/> and again from <see cref="SyncHeadCoverage"/>,
        /// since a hood can be equipped or removed well after the body was last painted and
        /// nothing else re-touches this layer.
        ///
        /// HOODED, THIS IS HOOD-COLOURED, NOT HAIR-COLOURED. A hood's own art (RigLayer.Hood) is
        /// a front-view opening into the face and is disabled outright while facing away - see
        /// SetFacingAway - so this stand-in is the only thing a hooded character shows from
        /// behind, and it used to paint the character's own hair regardless: a player wearing a
        /// cloak with a cowl up would turn around and see their hair rather than the hood they
        /// equipped. One ramp for both halves of the grid, because a hood is opaque fabric front
        /// to back - there is no skin tone to show through it the way there is on a bare head.
        /// </summary>
        void DrawHeadBack(Palette.Ramp skin, Palette.Ramp hair)
        {
            // Hood beats tie: a hood covers the whole head, and its 2-material palette leaves
            // the tie's digit characters unmapped (transparent) even if HasTieBack is also true
            // on whatever's under it - no separate check needed to hide the tie under a hood.
            bool tieVisible = _tieBackOn && !_hoodOn;

            // The STYLE's own back view where it has one - see BodyLook.HairViews. The hood keeps
            // the flat stand-in below: a cowl covers the whole head and has no hair to show.
            if (!_hoodOn && _look != null && BodyLook.View(BodyLook.HairStyle(_look.HairStyle).Key, back: true) != null)
            {
                int scale = _detailArt ? BodyLook.DetailScale : 1;
                var tie = new Palette.Ramp(_tieColor);
                string styledKey = $"body.headback.{_look.HairStyle}.{_look.Skin}.{_look.Hair}.x{scale}" +
                                   (tieVisible ? "." + ColorUtility.ToHtmlStringRGB(_tieColor) : "");
                var styled = PixelSprite.Cached(styledKey) ?? PixelSprite.From(styledKey,
                    BodyLook.HeadBackDetail(BodyLook.HairStyle(_look.HairStyle).Key, _look.Hair, scale, tieVisible),
                    Palette.Of(skin, hair, tie), pixelsPerUnit: bodyPpu * scale);
                SetBodyPixel(RigLayer.HeadBack, styled, new Vector2(0, ChinOnNeckline(styled)));
                if (_layers.TryGetValue(RigLayer.HeadBack, out var styledSr) && styledSr != null)
                    styledSr.enabled = _facingAway && !SealedHelmBackShown;
                return;
            }

            // Hooded: the plain skull outline in the cowl's colour. (The un-hooded arm is only a
            // guard - every style has a back view, so it is not reached in practice.)
            var pal = _hoodOn ? Palette.Of(new Palette.Ramp(_hoodColor), new Palette.Ramp(_hoodColor))
                              : Palette.Of(skin, hair);
            string[] rows = HeadBackSkull;
            string key = _hoodOn
                ? $"body.headback.hood.{ColorUtility.ToHtmlStringRGB(_hoodColor)}"
                : $"body.headback.{_look.Skin}.{_look.Hair}";

            // THE SAME ANCHOR AS THE FRONT HEAD, derived the same way - not a literal that happens
            // to agree with it. This line read `new Vector2(0, 14)` and carried a note admitting it
            // was "close enough to line up at a glance, worth re-measuring live once this state is
            // actually used". It was, and it did not: rendered facing away, the back of the head
            // floated a clear gap above the shoulders. 14 was correct while both head grids were
            // drawn at LayoutUnit; the body's move to BodyPpu halved the sprite's world height and
            // the literal stayed put, so the chin ended up nine cells above the neckline.
            //
            // That is the third time this project has been caught by one number restated in a
            // second place (the stale NeckYCells, the shadow's hardcoded sole), and the fix is the
            // one already written for the other two: ask the sprite. ChinOnNeckline works off
            // bounds, so it is right for this grid and the front's taller composed one alike, and
            // neither needs re-tuning if the head's proportions move again.
            var backSprite = PixelSprite.From(key, rows, pal, pixelsPerUnit: bodyPpu);
            SetBodyPixel(RigLayer.HeadBack, backSprite,
                new Vector2(0, ChinOnNeckline(backSprite)));
            if (_layers.TryGetValue(RigLayer.HeadBack, out var sr) && sr != null)
                sr.enabled = _facingAway && !SealedHelmBackShown;   // painted every time, shown only while turned away
        }

        /// <summary>
        /// Paint the hair that falls past the skull, on its own layer behind the whole body.
        ///
        /// ANCHORED BY ITS TOP EDGE, never by its centre. The grid is placed so its first row sits
        /// at the style's own <c>ManeTop</c>, which is stated in the same texels the head sprite's
        /// own top edge is - so a mane lines up with the head it belongs to whatever size it is,
        /// and a style can make its mane longer without re-deriving an offset. Centring it instead
        /// would move the hair up the head every time the length changed.
        /// </summary>
        void DrawMane(Palette.Ramp hair)
        {
            if (!_layers.TryGetValue(RigLayer.HairBack, out var sr) || sr == null) return;

            // A SEALED helm has no mane at all, not a clipped one. Clipping only blanks the rows
            // above the hairline, which is correct for a helmet hair can fall out of and wrong for
            // one that wraps the jaw - there, the length below the line reads as hair growing
            // through steel. Guarded on _helmOn too, so hiding the helm gives the hair back.
            bool back = _facingAway;
            var grid = _headSealed && _helmOn ? null : BodyLook.Mane(_look.HairStyle, _headCovered, back);
            if (grid == null)
            {
                sr.enabled = false;
                sr.sprite = null;
                return;
            }

            // ONE RAMP, TWICE. The mane draws in the hair's uppercase set and nothing else - no
            // skin, no eyes - so handing it the head's full palette would make the sprite depend on
            // fields ManeKey deliberately does not name, and the cache would then serve one
            // character's mane to the next one with the same hair.
            var pal = Palette.Of(hair, hair);
            // The HEAD's density, not LayoutUnit. The mane is stated row for row against the head
            // sprite (ManeTop is in the head's own texels), so it has to be drawn at whatever the
            // head is drawn at - left at LayoutUnit when the head moved to BodyPpu, every mane came
            // out twice the head's size and floating a full head-height above it.
            float ppu = bodyPpu * (_detailArt ? BodyLook.DetailScale : 1);
            string key = BodyLook.ManeKey(_look, _headCovered, back) + (_detailArt ? ".menu" : ".arena");

            // Through the SAME hair pass as the head at either density - see ManeDetail. A
            // rendered fringe over a flat mane is one head of hair joined down the middle.
            // Cached first: the mane swaps with every turn away and back, and the hair pass is not
            // something to rerun each time the player changes direction.
            var sprite = PixelSprite.Cached(key) ?? PixelSprite.From(key,
                BodyLook.ManeDetail(_look.HairStyle, _headCovered, _look.Hair,
                                    _detailArt ? BodyLook.DetailScale : 1, back),
                pal, pixelsPerUnit: ppu);
            int top = BodyLook.ManeTopFor(_look.HairStyle, back);

            SetBodyPixel(RigLayer.HairBack,
                sprite,
                // Rows are counted down from the top edge, so the centre is half the grid below it.
                // Worked in head texels, then converted: SetBodyPixel wants layout cells.
                new Vector2(0, Proportions.Cells(top - grid.Length * 0.5f)));
        }

        /// <summary>
        /// Whether a helmet is actually ON: something equipped in the head-armour layer AND not
        /// hidden by the player's own preference.
        /// </summary>
        bool _helmOn;

        /// <summary>
        /// The equipped Head item is a MASK, not a helm - see <see cref="GearItem.CoversFaceOnly"/>.
        /// Kept as its own field beside <see cref="_helmOn"/> rather than folded into it: _helmOn
        /// still has to be true (the face IS covered, which is what HeadBack reads it for), and
        /// only the MANE's hairline clip has any business asking the difference.
        /// </summary>
        bool _faceMaskOnly;

        /// <summary>The equipped Head item seals the skull - see <see cref="GearItem.SealsHead"/>.</summary>
        bool _headSealed;

        /// <summary>
        /// Whether the equipped Back item is a hooded cloak (<see cref="GearItem.HasHood"/>),
        /// set fresh by <see cref="Apply"/> every time the loadout changes.
        /// </summary>
        bool _hoodOn;

        /// <summary>The hooded item's own colour, read alongside <see cref="_hoodOn"/> - see its
        /// use painting <see cref="RigLayer.HeadBack"/>.</summary>
        Color _hoodColor;

        /// <summary>
        /// The equipped hood's BACK view (<see cref="GearItem.HoodBack"/>) and the front sprite it
        /// stands in for while turned away - see <see cref="SyncHoodFacing"/>. Sprites, not a
        /// collection or an interface, so a domain reload restores them like any other
        /// UnityEngine.Object reference.
        /// </summary>
        Sprite _hoodBack, _hoodFront;

        /// <summary>The equipped helm's BACK view (<see cref="GearItem.HelmBack"/>) and the front
        /// sprite it stands in for while turned away - see <see cref="SyncHelmFacing"/>.</summary>
        Sprite _helmBack, _helmFront;

        /// <summary>A sealed helm's back view is on: it encloses the head, so the back of the
        /// skull (HeadBack) is hidden under it - drawn, its hair pokes out round the dome.</summary>
        bool SealedHelmBackShown => _helmBack != null && _headSealed && !_helmHidden;

        /// <summary>
        /// Whether the equipped Head item is tied on with cloth (<see cref="GearItem.HasTieBack"/>),
        /// set fresh by <see cref="Apply"/> every time the loadout changes. Same shape as
        /// <see cref="_hoodOn"/>, one slot over: the front sprite (RigLayer.HeadArmor) shows the
        /// plate, RigLayer.HeadBack shows the knot and tails.
        /// </summary>
        bool _tieBackOn;

        /// <summary>The tie's own cloth colour, read alongside <see cref="_tieBackOn"/>.</summary>
        Color _tieColor;

        /// <summary>
        /// Whether the crown is covered by ANYTHING - a helmet or a hood - which is what decides
        /// how much of the mane is drawn, so it has to follow the wider question rather than just
        /// the helm's own visibility. A hood clips the mane the same way a helmet already does
        /// (<see cref="BodyLook.Mane"/> takes a plain bool and does not care which); nothing about
        /// the clip itself needed to change; only what could set it.
        /// </summary>
        bool _headCovered;

        /// <summary>
        /// Re-read the helm/hood state and repaint the mane if either changed.
        ///
        /// HeadBack is repainted UNCONDITIONALLY, not gated behind the same check - a helm coming
        /// on or off while a hood is already worn never changes <c>covered</c> (it is true either
        /// way), but the hood itself could have just been equipped or removed this same Apply, and
        /// HeadBack is the one thing that has to notice. PixelSprite caches by key, so repainting
        /// with nothing actually changed costs a dictionary lookup and nothing more.
        /// </summary>
        void SyncHeadCoverage()
        {
            _helmOn = _layers.TryGetValue(RigLayer.HeadArmor, out var sr)
                      && sr != null && sr.sprite != null && sr.enabled;

            // A MASK does not clip the mane. "covered" here means "the hairline is under
            // something", which is what DrawMane blanks against - and a mask that stops below the
            // fringe leaves the hairline in plain view, so clipping for it deletes hair the player
            // can still see. A hood always covers, mask or not, so it stays in the test.
            bool covered = (_helmOn && !_faceMaskOnly) || _hoodOn;

            DrawHeadBack(BodyLook.SkinRamp(_look.Skin), BodyLook.HairRamp(_look.Hair));

            if (covered == _headCovered) return;
            _headCovered = covered;
            // The head as well as the mane: covered, it loses the hair above the skull.
            RepaintHead(BodyLook.SkinRamp(_look.Skin), BodyLook.HairRamp(_look.Hair),
                        BodyLook.EyeRamp(_look.Eyes), BodyLook.BrowRamp(_look));
            DrawMane(BodyLook.HairRamp(_look.Hair));
        }

        // ---- CHIBI PROPORTIONS ----
        //
        // Layout in texels from the rig root, y up. The head is deliberately BIGGER than the
        // torso and there is NO neck: head bottom (0) overlaps torso top (+2) by two texels, so the
        // two silhouettes fuse. Previously the torso was 12x16 against a 10x10 head - taller body
        // than head, i.e. realistic adult proportions, which is what read as "anatomically correct
        // but shouldn't be".
        //
        // Every count below is block-doubled from the original 37.5 ppu authoring (LayoutUnit is
        // now 75 - see PixelSprite.LayoutUnit) - twice the texels, same world size, same picture.
        //
        //   part     grid      centre y     spans
        //   Head     32 x 28     +14        0 .. +28
        //   Torso    24 x 20      -8      -18 ..  +2
        //   Arms      8 x 16      -8      -16 ..   0
        //   Legs     12 x 16     -26      -34 .. -18
        //
        // Outline contract this establishes: head top +0.373u, feet -0.453u, widest point is the
        // HEAD at +/-0.213u. Gear offsets and the contact shadow are placed against these.
        //
        // EVERY NUMBER IN THE TWO PARAGRAPHS ABOVE NOW LIVES IN Proportions, and the outline
        // contract is derived there (CrownY/SoleY/HalfWidest) rather than quoted. The table is
        // authoritative; this table is the same values written out for reading. If they ever
        // disagree, the table is right and this comment is stale - which is precisely why the
        // contract stopped being a comment: as prose it could drift from the grids it described,
        // with nothing to catch it, while 106 gear offsets went on trusting it.

        // The HEAD grid lives in BodyLook now: it is composed from a bare skull, an expression
        // and a hairstyle rather than baked as one picture, so hair colour and hairstyle stop
        // being the same decision as the face. See BodyLook.Head.

        /// <summary>
        /// The back of the head under a HOOD, and nothing else now: every hairstyle has its own
        /// back view (BodyLook.HeadBackDetail, see DrawHeadBack). A cowl covers the whole head, so
        /// this plain skull outline painted in the hood's colour is all that shows. 32 x 28.
        /// </summary>
        static readonly string[] HeadBackSkull =
        {
                "......KKKKKKKKKKKKKKKKKKKK......",
                "......KKKKKKKKKKKKKKKKKKKK......",
                "..KKKKBBBBBBBBBBBBBBBBBBBBKKKK..",
                "..KKKKBBBBBBBBBBBBBBBBBBBBKKKK..",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKBBBBBBBBBBBBBBBBBBBBBBBBBBBBKK",
                "KKDDDDDDDDDDDDDDDDDDDDDDDDDDDDKK",
                "KKDDDDDDDDDDDDDDDDDDDDDDDDDDDDKK",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "kkbbbbbbbbbbbbbbbbbbbbbbbbbbbbkk",
                "..kkbbbbbbbbbbbbbbbbbbbbbbbbkk..",
                "..kkbbbbbbbbbbbbbbbbbbbbbbbbkk..",
                "....kkddddddddddddddddddddkk....",
                "....kkddddddddddddddddddddkk....",
                "........kkkkkkkkkkkkkkkk........",
                "........kkkkkkkkkkkkkkkk........",
        };

        // 26 x 48 at BodyPpu - shoulders to waist, 39% of the figure's height.
        //
        // The chest is no longer "small because the head is the read". The head is now 23% of the
        // figure instead of 45%, so the torso has to carry the body on its own: it is longer than
        // it is wide by nearly two to one, where the old one was roughly square.
        //
        // ASYMMETRIC, and that is the point. The rig has three directions - facing forward, its
        // mirror, and facing away - but every body grid was drawn symmetric, so the two mirrored
        // directions rendered identically and the character read as permanently facing the player.
        // Shifting interior features (see BodyLook.Gaze) was not enough: at this size the
        // SILHOUETTE carries the read and interior detail does not.
        //
        // So the outline itself is turned. Rig-local +X is forward, which is the RIGHT of these
        // rows, and the mirror handles the other direction - this is authored once.
        //
        //   BACK  (left)  a straight vertical line, column 2 on every single row.
        //   CHEST (right) pushes furthest out across the chest rows and draws back into the waist.
        //
        // The light follows the same logic rather than a fixed key: shadow banked against the
        // straight back, base through the middle, lit tone on the leading edge, so the volume
        // reads as turned toward where the character is looking.
        static readonly string[] Torso =
        {
                "..kddddbbbbbbbbbbbllllk...",
                "..kddddbbbbbbbbbbbllllk...",
                "..kdddddbbbbbbbbbblllllk..",
                "..kdddddbbbbbbbbbblllllk..",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbbblllllk",
                "..kdddddbbbbbbbbbbblllllk.",
                "..kdddddbbbbbbbbbbblllllk.",
                "..kdddddbbbbbbbbbbblllllk.",
                "..kdddddbbbbbbbbbbblllllk.",
                "..kdddddbbbbbbbbbbblllllk.",
                "..kdddddbbbbbbbbbbblllllk.",
                "..kdddddbbbbbbbbbblllllk..",
                "..kdddddbbbbbbbbbblllllk..",
                "..kdddddbbbbbbbbbblllllk..",
                "..kdddddbbbbbbbbbblllllk..",
                "..kdddddbbbbbbbbbblllllk..",
                "..kdddddbbbbbbbbbblllllk..",
                "..kdddddbbbbbbbbbblllllk..",
                "..kdddddbbbbbbbbbblllllk..",
                "..kddddbbbbbbbbbbllllk....",
                "..kddddbbbbbbbbbbllllk....",
                "..kddddbbbbbbbbbbllllk....",
                "..kddddbbbbbbbbbbllllk....",
                "..kddddbbbbbbbbbbllllk....",
                "..kddddbbbbbbbbbbllllk....",
                "..kddddbbbbbbbbbllllk.....",
                "..kddddbbbbbbbbbllllk.....",
        };

        // 10 x 34 at BodyPpu - shoulder to fingertips, 27% of the figure's height.
        //
        // NOT the old 8x16 block-doubled. The arm was ALREADY close to the reference's fraction of
        // body height; what made it read as stubby was the head beside it, and the head is what
        // moved. The extra texels went into shape rather than size: a narrowing toward the wrist,
        // a darker cuff where the hand begins, and a lit outer edge that runs the whole length so
        // the limb has a direction instead of being a bar.
        //
        // Waisted at the elbow (rows 12-15) on purpose: the pinch is where the joint is. The rig
        // cuts this sprite at Proportions.ElbowTexels when it paints it (see PaintElbowSplit), so
        // the bend falls along a line the art already has rather than a seam mid-forearm.
        static readonly string[] Limb =
        {
                "..kkkkkk..",
                "..kkkkkk..",
                ".kkllllkk.",
                "kkllllllkk",
                "kkllllllkk",
                "kkllllllkk",
                "kkllllllkk",
                "kkllllllkk",
                "kkllbbbbkk",
                "kkllbbbbkk",
                "kkllbbbbkk",
                "kkllbbbbkk",
                ".kllbbbbk.",
                ".kllbbbbk.",
                ".kllbbbbk.",
                ".kllbbbbk.",
                "kkllbbbbkk",
                "kkllbbbbkk",
                "kkllbbbbkk",
                "kkllbbbbkk",
                "kkllbbbbkk",
                "kkllbbbbkk",
                "kkllbbddkk",
                "kkllbbddkk",
                "kkllbbddkk",
                "kkllbbddkk",
                "kkddddddkk",
                "kkddddddkk",
                ".kkddddkk.",
                ".kkddddkk.",
                "..kkkkkk..",
                "..kkkkkk..",
        };

        // 8 x 48 at BodyPpu - hip to sole, 42% of the figure's height. Still wider than the arm
        // so the stance reads.
        //
        // This is the part that carries the reproportion. The legs were 26% of the figure and are
        // now 42%; that single change is most of what separates the chibi build from the
        // reference, because legs are what a standing pose is read from.
        //
        // EIGHT WIDE, not twelve. Measured against the reference rather than judged: at 12 the
        // leg was 12.7% of the figure's height where the reference is about 7.5%, and that ratio
        // - right length, half again too thick - is the whole of what read as "stocky". Length
        // was already correct at ~38% and did not move.
        //
        // Three zones, so the length reads as a leg rather than a column: thigh (lit, widest), a
        // knee pinch at rows 22-25, and a shin that tapers into a darker boot mass at the ankle.
        // Same rule as the arm - the pinch marks where the joint goes before there is a joint.
        //
        // EVERY ROW CARRIES A LIT LEADING COLUMN, which it did not until the body became one black
        // material. The thigh used to be a flat run of base and the shin a flat run of dark, and
        // that was survivable while the legs were element-tinted cloth: they separated from the
        // floor by HUE, and a saturated rust against a blue-grey ground reads even at the same
        // value. The undersuit is neutral, so value is the only channel left - and measured
        // against the arena floor (0.100, 0.110, 0.140) the suit's own Dark tone lands within a
        // hundredth of it. Rendered, the shins simply were not there.
        //
        // The fix is the one the other two body grids already made rather than a new idea: Torso
        // banks shadow against its straight back and runs a lit edge down the leading side, and
        // Limb carries `ll` down the whole outer edge - which is exactly why the ARMS read
        // correctly in the same render the legs failed. Four interior texels is all an 8-wide leg
        // has to spend, so it is one column each: shadow, base, base, light. The silhouette is
        // untouched, texel for texel - only the tones inside it moved, so nothing about
        // proportion, placement or the greave that sits on top of this had to be re-derived.
        static readonly string[] Leg =
        {
                "..kkkk..",
                "..kkkk..",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                "kkdbblkk",
                ".kdbblk.",
                ".kdbblk.",
                ".kdbblk.",
                ".kdbblk.",
                "kkdbblkk",
                "kkdbblkk",
                "kksdblkk",
                "kksdblkk",
                "kksdblkk",
                "kksdblkk",
                "kksdblkk",
                "kksdblkk",
                "kksdblkk",
                "kksdblkk",
                "kksdblkk",
                "kksdblkk",
                "kksdblkk",
                "kksdblkk",
                ".ksdblk.",
                ".ksdblk.",
                ".ksdblk.",
                ".ksdblk.",
                "kksdblkk",
                "kksdblkk",
                "kksdblkk",
                "kksdblkk",
        };

        bool _helmHidden;

        /// <summary>
        /// Applied on top of whatever Apply just equipped - re-running it after every Apply is
        /// what keeps the preference sticky across re-equips, since Apply has no idea this exists.
        /// </summary>
        public void SetHelmHidden(bool hidden)
        {
            _helmHidden = hidden;
            // Through the facing, so showing the helm while turned away shows its BACK (or
            // nothing), never the front face.
            if (_layers.TryGetValue(RigLayer.HeadArmor, out var sr) && sr != null && sr.sprite != null)
                SyncHelmFacing(_facingAway);
            EncloseHeadInHelm();

            // wraith_eye paints no HeadArmor sprite at all - the recoloured eye and its glow ARE
            // its whole presence on the head slot, so "hide helm" has to reach into SyncGlowEye
            // too or the item has nothing the toggle actually does anything to.
            SyncGlowEye();

            // Taking the helmet off gives the hair its crown back, so the mane has to be rebuilt
            // unclipped - otherwise hiding a helm leaves a bald gap where its dome used to be.
            // (Unless a hood is also worn - SyncHeadCoverage checks that too.)
            SyncHeadCoverage();
        }

        /// <summary>
        /// See <see cref="ICharacterRig.SetFacingAway"/>. Applied on top of whatever Apply just
        /// equipped, the same way <see cref="SetHelmHidden"/> is - re-running it at the end of
        /// every Apply is what keeps it sticky across a loadout change made mid-sequence (a door
        /// swaps nothing, but nothing stops another system from calling Apply while one is open),
        /// since Apply has no idea this state exists and would otherwise silently restore the
        /// front face and the held-weapon stack on the very next equip.
        /// </summary>
        public void SetFacingAway(bool away)
        {
            bool turned = away != _facingAway;
            _facingAway = away;
            ApplyLayerOrder();
            SyncLopsided();

            // The mane is drawn per VIEW (see BodyLook.HairViews): hair seen from behind is a
            // different picture from hair seen turned, not the same one in another order.
            if (turned) DrawMane(BodyLook.HairRamp(_look.Hair));

            SetLayerEnabled(RigLayer.Head,     !away);
            // The glowing eye is an OVERLAY child of the head joint, not the Head layer, so
            // switching the face off above leaves it drawn through the back of the skull.
            var glowEye = _head != null ? _head.GetComponentInChildren<GlowingEye>(true) : null;
            if (glowEye != null) glowEye.SetShown(!away && _glowLeftEye);
            // Turning back round must not override "hide helm" - the helmet is shown again only
            // if the player has not hidden it (see SetHelmHidden).
            SyncHelmFacing(away);
            SyncHoodFacing(away);
            SetLayerEnabled(RigLayer.Ring,      !away);
            SetLayerEnabled(RigLayer.Trinket,   !away);
            SetLayerEnabled(RigLayer.Neck,      !away);
            SyncDrapeFacing(away);
            SyncDrapeSide();

            // HeadBack is a body layer SetAppearance always keeps painted (see DrawPixelBody), so
            // showing it again is just re-enabling the renderer - there is no equip step to redo.
            if (_layers.TryGetValue(RigLayer.HeadBack, out var headBack) && headBack != null)
                headBack.enabled = away && headBack.sprite != null && !SealedHelmBackShown;
        }

        /// <summary>
        /// Turned away, a helm with back art (<see cref="GearItem.HelmBack"/>) swaps it onto
        /// HeadArmor; one without is switched off and the back of the head shows. "Hide helm"
        /// wins either way. The front sprite is remembered for the way back, as the hood's is.
        /// </summary>
        void SyncHelmFacing(bool away)
        {
            if (!_layers.TryGetValue(RigLayer.HeadArmor, out var sr) || sr == null) return;

            if (away && _helmBack != null)
            {
                if (sr.sprite != _helmBack) _helmFront = sr.sprite;
                sr.sprite = _helmBack;
                sr.enabled = !_helmHidden;
                return;
            }

            if (sr.sprite == _helmBack && _helmFront != null) sr.sprite = _helmFront;
            SetLayerEnabled(RigLayer.HeadArmor, !away && !_helmHidden);
        }

        /// <summary>
        /// Turned away, a hood with back art SHOWS it on its own layer instead of disappearing.
        ///
        /// The front cowl is an opening into a face and has nothing true to say from behind, which
        /// is why it used to be switched off here and the back of the head painted in the hood's
        /// colour instead. That stand-in is the SKULL's shape - smaller than the cowl, with the
        /// skull's own hairline band showing through - so the hood visibly shrank and changed
        /// shape every time the character turned round. The back view is the cowl's own
        /// silhouette closed up (see DemoGear.HoodBackRows), so turning changes only what is
        /// inside the outline. Same layer, so it keeps the cowl's sort position over the cape.
        ///
        /// The front sprite is remembered rather than repainted on the way back, because Apply is
        /// what paints it and nothing about turning round calls Apply.
        /// </summary>
        void SyncHoodFacing(bool away)
        {
            if (!_layers.TryGetValue(RigLayer.Hood, out var sr) || sr == null) return;

            if (away && _hoodOn && _hoodBack != null)
            {
                if (sr.sprite != _hoodBack) _hoodFront = sr.sprite;
                sr.sprite = _hoodBack;
                sr.enabled = true;
                return;
            }

            if (sr.sprite == _hoodBack && _hoodFront != null) sr.sprite = _hoodFront;
            SetLayerEnabled(RigLayer.Hood, !away);
        }

        /// <summary>
        /// Turned away, a one-shoulder drape swaps to its BACK view instead of disappearing - the
        /// right shoulder is under the cloak from either side. The back view is its own sprite
        /// (DemoGear.WraithDrapeBackRows) because the front art, laid over the cape, drew a
        /// seam across the middle of what is meant to be one cloth. A drape without one (the
        /// poncho's yoke) has nothing true to say from behind and is switched off, its own back
        /// panel covering both shoulders there. Same shape as <see cref="SyncHoodFacing"/>.
        /// </summary>
        void SyncDrapeFacing(bool away)
        {
            if (!_layers.TryGetValue(RigLayer.BackOver, out var sr) || sr == null) return;

            if (away && _drapeBack != null)
            {
                if (sr.sprite != _drapeBack) _drapeFront = sr.sprite;
                sr.sprite = _drapeBack;
                sr.enabled = true;
                return;
            }

            if (sr.sprite == _drapeBack && _drapeFront != null) sr.sprite = _drapeFront;
            SetLayerEnabled(RigLayer.BackOver, !away);
        }

        /// <summary>
        /// Keep the Back item's drape on the shoulder OPPOSITE the carrying arm.
        ///
        /// The drape is authored on rig -X, the carrying shoulder in the default facing, so it is
        /// mirrored to +X whenever the carry is in arm.front - i.e. the inverse of
        /// <see cref="CarryInBackArm"/>. Keyed off the carry rather than a fixed rig side so
        /// turning away (which moves the carry) moves the drape with it.
        ///
        /// Mirrored about the TORSO's centre line (position negated, sprite flipped about its
        /// own centre pivot), so a symmetric piece like the poncho's yoke is unchanged by it.
        /// Re-derived from the rest pose every call rather than latched.
        ///
        /// A drape that swings with the cape (<see cref="GearItem.DrapeSwingsWithCape"/>) is not
        /// moved here at all: AnimateCape bends it through its own ClothBend with the cape's hinge,
        /// length and angles, so at any height it bends exactly as the cape behind it does.
        /// </summary>
        void SyncDrapeSide()
        {
            SyncTrinketSide();
            SyncShroudSide();
            if (!_layers.TryGetValue(RigLayer.BackOver, out var sr) || sr == null) return;
            bool mirrored = !CarryInBackArm;
            var p = _backOverRest;
            if (mirrored) p.x = -p.x;

            // A drape that swings with the cape is BENT with it rather than moved (see AnimateCape).
            sr.transform.localPosition = p;
            sr.transform.localRotation = Quaternion.identity;
            sr.flipX = mirrored;
        }

        /// <summary>
        /// A neck item's shroud (RigLayer.NeckOver) takes the drape's side by the drape's rule:
        /// authored on -X, mirrored whenever the carry is in arm.front. Called from SyncDrapeSide
        /// (every frame, after the pauldrons have followed their arms) for the trinket's reason.
        ///
        /// It HIDES THE PAULDRONS, the hood's rule for the mane (the user's call): a shroud lies
        /// over both shoulders, so what is under it is covered anyway and all hiding changes is a
        /// plate too big for the cloth poking out past it. By forceRenderingOff, not enabled -
        /// enabled is what Apply, HideGearLayer and FollowPauldron read as "is a pauldron worn",
        /// so the plates keep following their arms and come back the moment the shroud comes off.
        /// Keyed off the LAYER, so any shroud does it. A one-shoulder cloak's drape hides the one
        /// pauldron under it by the same rule.
        /// </summary>
        void SyncShroudSide()
        {
            bool mirrored = !CarryInBackArm;
            bool shroud = _layers.TryGetValue(RigLayer.NeckOver, out var sr) && sr != null
                          && sr.enabled && sr.sprite != null;
            if (sr != null)
            {
                var p = _neckOverRest;
                if (mirrored) p.x = -p.x;
                sr.transform.localPosition = p;
                sr.flipX = mirrored;
            }

            // A one-shoulder drape (a Back item with a DrapeBack - the Wraithguard's, Tepes' fur)
            // covers the plate on ITS shoulder the same way: that pauldron is under the cloth, so
            // hiding it only stops one too big for the drape poking out past it. The drape sits
            // opposite the carry, at +X exactly when the carry is in arm.front (SyncDrapeSide).
            // Asked of the layer's ENABLED, not its rendering - a drape swinging with the cape is
            // drawn by its ClothBend with the layer's own renderer switched off.
            bool drape = _drapeBack != null
                         && _layers.TryGetValue(RigLayer.BackOver, out var drapeSr) && drapeSr != null
                         && drapeSr.enabled && drapeSr.sprite != null;
            bool drapeOnPlus = !CarryInBackArm;

            ShroudCovers(RigLayer.Shoulders, _shouldersRest);
            ShroudCovers(RigLayer.ShouldersBack, _shouldersBackRest);

            void ShroudCovers(RigLayer layer, Vector3 rest)
            {
                if (!_layers.TryGetValue(layer, out var plate) || plate == null) return;
                plate.forceRenderingOff = shroud || (drape && (rest.x > 0f) == drapeOnPlus);
            }
        }

        /// <summary>Keep a left-hip relic on the left hip - see _trinketRest. Called from
        /// SyncDrapeSide, which already runs everywhere the carrying side can change.</summary>
        void SyncTrinketSide()
        {
            if (!_trinketLeftHip) return;
            if (!_layers.TryGetValue(RigLayer.Trinket, out var sr) || sr == null) return;
            bool mirrored = CarryInBackArm;
            var p = _trinketRest;
            if (mirrored) p.x = -p.x;
            sr.transform.localPosition = p;
            sr.flipX = mirrored;
        }

        /// <summary>
        /// Disable a layer without touching its sprite, so the front-facing state Apply/equip
        /// left it in is exactly what comes back when facing-away turns off. Skips a gear layer
        /// that never had anything painted into it - forcing it "on" with a null sprite would
        /// draw nothing but leave SyncHeadCoverage and similar reads confused about whether the
        /// slot is actually occupied.
        /// </summary>
        void SetLayerEnabled(RigLayer layer, bool enabled)
        {
            if (!_layers.TryGetValue(layer, out var sr) || sr == null) return;
            if (enabled && sr.sprite == null) return;
            sr.enabled = enabled;
        }

        void SetBodyPixel(RigLayer layer, Sprite sprite, Vector2 offsetPx)
        {
            var sr = _layers[layer];
            sr.sprite = sprite;
            sr.color = Color.white;         // the art carries its own colour; tint must not multiply it
            sr.enabled = true;
            sr.drawMode = SpriteDrawMode.Simple;
            sr.transform.localPosition = PixelSprite.Px(offsetPx.x, offsetPx.y);
            sr.transform.localScale = Vector3.one;
        }

        // ---------------------------------------------------------------- the elbow

        /// <summary>
        /// Find the two elbow pivots by name, or build them. Cheap when both are already held.
        /// A rig built before the joint existed (or a field lost to a reload) is repaired here
        /// rather than rebuilt - see CLAUDE.md's domain reload traps.
        /// </summary>
        void EnsureElbows()
        {
            if (_elbowFront == null && _armFront != null) _elbowFront = ElbowUnder(_armFront, "elbow.front");
            if (_elbowBack == null && _armBack != null) _elbowBack = ElbowUnder(_armBack, "elbow.back");
            EnsureKnees();

            Transform ElbowUnder(Transform shoulder, string name)
            {
                var found = shoulder.Find(name);
                return found != null ? found : MakePivot(name, ElbowRest, shoulder);
            }
        }

        /// <summary>
        /// True for a layer hung from an elbow: both forearms, both lower gloves, and the two
        /// things held in the hand.
        /// </summary>
        static bool HungFromElbow(RigLayer layer) =>
            layer is RigLayer.Ring or RigLayer.Weapon
                  or RigLayer.ArmBackLower or RigLayer.GlovesBackLower
                  or RigLayer.ArmFrontLower or RigLayer.GlovesFrontLower
                  or RigLayer.ArmBackHand or RigLayer.GlovesBackHand
                  or RigLayer.ArmFrontHand or RigLayer.GlovesFrontHand;

        /// <summary>
        /// What to subtract from a SHOULDER-relative offset to place a layer under its real
        /// pivot. Every gear offset in the project was authored before the elbow, against the
        /// shoulder, and stays that way - converting at the point of placement is one line, where
        /// re-authoring would be every weapon and ring in the catalogue.
        /// </summary>
        static Vector2 PivotShift(RigLayer layer) => HungFromElbow(layer) ? ElbowArtRest : Vector2.zero;

        /// <summary>
        /// How far each half of a cut arm runs PAST the elbow line, world units - two body
        /// texels. The two halves overlap by twice this, with identical pixels, so a straight arm
        /// composites to exactly the unbroken sprite; bent, the overlap is what fills the joint
        /// instead of a wedge of daylight opening on the outside of the bend.
        /// </summary>
        const float ElbowOverlap = 2f / Proportions.BodyPpu;

        /// <summary>Cut sprites by (source instance, cut row). Non-readonly and rebuilt on
        /// demand - a reload emptying it costs one re-cut, nothing more.</summary>
        static Dictionary<(Sprite, int), (Sprite upper, Sprite lower)> _elbowCuts;

        /// <summary>
        /// Cut whatever <paramref name="upper"/> currently shows at the elbow line: the part above
        /// stays where it is, on the shoulder; the part below moves onto the matching lower layer
        /// under the elbow pivot, at the same world position.
        ///
        /// Must be handed the WHOLE sprite - call it straight after painting the layer, never on
        /// a layer it has already cut.
        /// </summary>
        void PaintElbowSplit(RigLayer upper)
        {
            if (!RigLayers.LowerOf(upper, out var lowerLayer)) return;
            if (!_layers.TryGetValue(upper, out var up) || up == null) return;
            if (!_layers.TryGetValue(lowerLayer, out var lo) || lo == null) return;

            lo.sprite = null;
            lo.enabled = false;
            if (RigLayers.HandOf(lowerLayer, out var handLayer)
                && _layers.TryGetValue(handLayer, out var hand) && hand != null)
            {
                hand.sprite = null;
                hand.enabled = false;
            }

            var full = up.sprite;
            var t = up.transform;
            if (full == null || !up.enabled || Mathf.Abs(t.localScale.y) < 0.0001f) return;

            // The elbow line in the sprite's own units, measured from its pivot, and the upper
            // arm's extra length in the same units.
            float cut = (ElbowArtRest.y - t.localPosition.y) / t.localScale.y;
            float extra = (ElbowArtRest.y - ElbowRest.y) / Mathf.Abs(t.localScale.y);
            var (above, below) = CutAtElbow(full, cut, extra);

            up.sprite = above;
            up.enabled = above != null;
            if (below == null) return;

            lo.sprite = below;
            lo.color = up.color;
            lo.drawMode = SpriteDrawMode.Simple;
            lo.enabled = true;
            lo.transform.localPosition = t.localPosition - (Vector3)ElbowArtRest;
            lo.transform.localRotation = t.localRotation;
            lo.transform.localScale = t.localScale;

            PaintWristSplit(lowerLayer);
        }

        /// <summary>
        /// Cut a forearm layer again at the WRIST: the forearm stays, the hand moves onto the
        /// matching hand layer, on the same elbow pivot at the same place, so a straight arm still
        /// composites to exactly the picture it was. The same cut as the elbow's (CutAtElbow,
        /// two texels of overlap) with no lengthening. Only ever called by PaintElbowSplit, on
        /// the forearm it has just made.
        /// </summary>
        void PaintWristSplit(RigLayer lower)
        {
            if (!RigLayers.HandOf(lower, out var handLayer)) return;
            if (!_layers.TryGetValue(lower, out var lo) || lo == null) return;
            if (!_layers.TryGetValue(handLayer, out var hand) || hand == null) return;

            hand.sprite = null;
            hand.enabled = false;

            var full = lo.sprite;
            var t = lo.transform;
            if (full == null || !lo.enabled || Mathf.Abs(t.localScale.y) < 0.0001f) return;

            // The wrist in the forearm's own frame - the elbow pivot's, where the art's elbow line
            // sits at 0 - then in the sprite's units from its pivot.
            float wrist = WristArtRest.y - ElbowArtRest.y;
            float cut = (wrist - t.localPosition.y) / t.localScale.y;
            var (forearm, below) = CutAtElbow(full, cut, 0f);

            lo.sprite = forearm;
            lo.enabled = forearm != null;
            if (below == null) return;

            hand.sprite = below;
            hand.color = lo.color;
            hand.drawMode = SpriteDrawMode.Simple;
            hand.enabled = true;
            hand.transform.localPosition = t.localPosition;
            hand.transform.localRotation = t.localRotation;
            hand.transform.localScale = t.localScale;
        }

        // ---------------------------------------------------------------- the knee

        /// <summary>Find the two knee pivots by name, or build them - see EnsureElbows.</summary>
        void EnsureKnees()
        {
            if (_kneeFront == null && _legFront != null) _kneeFront = KneeUnder(_legFront, "knee.front");
            if (_kneeBack == null && _legBack != null) _kneeBack = KneeUnder(_legBack, "knee.back");

            Transform KneeUnder(Transform hip, string name)
            {
                var found = hip.Find(name);
                return found != null ? found : MakePivot(name, KneeRest, hip);
            }
        }

        /// <summary>
        /// Cut whatever leg layer <paramref name="upper"/> currently shows at the knee: the thigh
        /// stays on the hip, the shin moves onto the matching lower layer under the knee pivot,
        /// at the same world position. The elbow's cut (CutAtElbow, two texels of overlap) with
        /// no lengthening - the leg art already has its joint at half its height.
        ///
        /// A boot that sits wholly below the knee goes to the shin whole. Must be handed the WHOLE
        /// sprite - call it straight after painting the layer, never on one it has already cut.
        /// </summary>
        void PaintKneeSplit(RigLayer upper)
        {
            if (!RigLayers.ShinOf(upper, out var lowerLayer)) return;
            if (!_layers.TryGetValue(upper, out var up) || up == null) return;
            if (!_layers.TryGetValue(lowerLayer, out var lo) || lo == null) return;

            lo.sprite = null;
            lo.enabled = false;

            var full = up.sprite;
            var t = up.transform;
            if (full == null || !up.enabled || Mathf.Abs(t.localScale.y) < 0.0001f) return;

            float cut = (KneeRest.y - t.localPosition.y) / t.localScale.y;
            var (above, below) = CutAtElbow(full, cut, 0f);

            up.sprite = above;
            up.enabled = above != null;
            if (below == null) return;

            lo.sprite = below;
            lo.color = up.color;
            lo.drawMode = SpriteDrawMode.Simple;
            lo.enabled = true;
            lo.transform.localPosition = t.localPosition - (Vector3)KneeRest;
            lo.transform.localRotation = t.localRotation;
            lo.transform.localScale = t.localScale;
        }

        static (Sprite upper, Sprite lower) CutAtElbow(Sprite s, float cutUnits, float extraUnits)
        {
            var r = s.rect;
            float ppu = s.pixelsPerUnit;

            // Rows from the bottom of the sprite's rect to the elbow line.
            int c = Mathf.RoundToInt(s.pivot.y + cutUnits * ppu);
            int extra = Mathf.Max(0, Mathf.RoundToInt(extraUnits * ppu));
            if (c <= 0) return (s, null);                  // entirely above the elbow
            if (c >= (int)r.height) return (null, s);      // entirely below it

            // A tightly packed atlas sprite has no rectangle to cut, and an unreadable texture
            // has no rows to repeat. Nothing in the project is either today; if something ever
            // is, it keeps the old rigid arm rather than tearing at the joint.
            if (s.packed && s.packingMode == SpritePackingMode.Tight) return (s, null);
            if (extra > 0 && !s.texture.isReadable)
            {
                Debug.LogWarning($"[Rig] '{s.name}' is not readable, so it cannot bend at the elbow.");
                return (s, null);
            }

            _elbowCuts ??= new Dictionary<(Sprite, int), (Sprite, Sprite)>();
            var key = (s, c * 1000 + extra);
            if (_elbowCuts.TryGetValue(key, out var hit) && hit.upper != null && hit.lower != null)
                return hit;

            int o = Mathf.Max(1, Mathf.RoundToInt(ElbowOverlap * ppu));
            int upFrom = Mathf.Max(0, c - o);
            int lowTo = Mathf.Min((int)r.height, c + o);

            Sprite upper;
            if (extra == 0)
            {
                // Pivots are carried over in PIXELS, so each half lands exactly where that part of
                // the whole sprite did - a pivot outside its own half's rect is fine.
                upper = Sprite.Create(s.texture,
                    new Rect(r.x, r.y + upFrom, r.width, r.height - upFrom),
                    new Vector2(s.pivot.x / r.width, (s.pivot.y - upFrom) / (r.height - upFrom)),
                    ppu, 0, SpriteMeshType.FullRect);
            }
            else
            {
                // The upper half, LENGTHENED: the elbow band at the bottom, then `extra` copies of
                // the first row above the band, then the rest of the arm as authored. The top stays
                // anchored at the shoulder, so the pivot keeps its distance from the TOP edge.
                int w = (int)r.width;
                int keep = (int)r.height - upFrom;
                int h = keep + extra;
                int band = lowTo - upFrom;
                int dupRow = Mathf.Min(lowTo, (int)r.height - 1);   // first row above the band

                var src = s.texture.GetPixels((int)r.x, (int)r.y + upFrom, w, keep);
                var dup = s.texture.GetPixels((int)r.x, (int)r.y + dupRow, w, 1);
                var px = new Color[w * h];
                for (int y = 0; y < h; y++)
                {
                    int from = y < band ? y : y < band + extra ? -1 : y - extra;
                    for (int x = 0; x < w; x++)
                        px[y * w + x] = from < 0 ? dup[x] : src[from * w + x];
                }

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = s.texture.filterMode,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                tex.SetPixels(px);
                tex.Apply();

                // Measured from the bottom: the old pivot's height above upFrom, plus the rows
                // inserted beneath it.
                upper = Sprite.Create(tex, new Rect(0, 0, w, h),
                    new Vector2(s.pivot.x / w, (s.pivot.y - upFrom + extra) / h),
                    ppu, 0, SpriteMeshType.FullRect);
            }
            var lower = Sprite.Create(s.texture,
                new Rect(r.x, r.y, r.width, lowTo),
                new Vector2(s.pivot.x / r.width, s.pivot.y / lowTo),
                ppu, 0, SpriteMeshType.FullRect);
            upper.name = s.name + ".upper";
            lower.name = s.name + ".lower";

            var pair = (upper, lower);
            _elbowCuts[key] = pair;
            return pair;
        }

        void SetBody(RigLayer layer, Sprite sprite, Color color, Vector2 offset, Vector2 size)
        {
            var sr = _layers[layer];
            sr.sprite = sprite;
            sr.color = color;
            sr.enabled = true;
            sr.drawMode = SpriteDrawMode.Simple;
            sr.transform.localPosition = offset;
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        // ---------------------------------------------------------------- equipping

        /// <summary>
        /// Paint a loadout onto the rig. Clears every gear layer first so unequipping works,
        /// then draws each equipped item into the layers it declares.
        /// </summary>
        /// <summary>
        /// Re-attach <see cref="_layers"/> to the renderers that are already in the hierarchy.
        ///
        /// A Dictionary is not serializable, so a domain reload - i.e. editing ANY script while
        /// play mode is running - empties it while every layer GameObject survives untouched. The
        /// character therefore carries on drawing perfectly, which is what makes this so hard to
        /// spot, but Apply's indexer throws "key 'Back' was not present" on the first layer and
        /// every subsequent equip silently does nothing.
        ///
        /// Rebuilt by NAME rather than by calling MakeLayer, which would weld a second set of
        /// renderers onto the rig on top of the ones already there.
        /// </summary>
        void EnsureLayers()
        {
            EnsureElbows();
            if (_layers.Count == RigLayers.All.Length) return;

            foreach (var layer in RigLayers.All)
            {
                if (_layers.TryGetValue(layer, out var have) && have != null) continue;

                var pivot = PivotFor(layer);
                var found = pivot != null ? pivot.Find(layer.ToString()) : null;

                // NOT ONLY UNDER ITS DEFAULT PIVOT. A layer can legitimately be living on a
                // different joint than PivotFor names: the bow moves the weapon to the off arm,
                // and the shouldered rest carry moves it again. Looking only where it is
                // SUPPOSED to be and building a fresh one when it is not there is precisely the
                // "never rebuild - that welds a second set on top" failure CLAUDE.md records.
                //
                // Caught with two live Weapon layers at different sorting orders after a domain
                // reload while the carry was open: the real one kept animating, the duplicate sat
                // behind the torso, and every later Apply painted whichever the dictionary
                // happened to hold.
                if (found == null) found = FindLayerAnywhere(layer);

                _layers[layer] = found != null ? found.GetComponent<SpriteRenderer>()
                                               : MakeLayer(layer);
                if (found != null) _pivots[layer] = found;
            }
        }

        /// <summary>
        /// Hunt for a layer's GameObject anywhere under the rig, by name.
        ///
        /// The fallback for <see cref="EnsureLayers"/> when a layer is not on the joint
        /// <see cref="PivotFor"/> names - see the note there. Depth-first over the whole rig,
        /// which is a few dozen transforms and only ever walked when the dictionary is already
        /// empty, so this is a domain-reload repair path rather than anything hot.
        /// </summary>
        Transform FindLayerAnywhere(RigLayer layer)
        {
            string want = layer.ToString();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == want) return t;
            return null;
        }

        /// <summary>
        /// Which art set Apply paints from. A plain bool, so it survives a domain reload where an
        /// interface-typed field would not.
        /// </summary>
        [SerializeField] bool _detailArt;

        /// <summary>The weapon sprite the LOADOUT asked for, so a heat swap can be undone.</summary>
        Sprite _weaponBase;

        public void SetWeaponSprite(Sprite sprite)
        {
            EnsureLayers();
            if (!_layers.TryGetValue(RigLayer.Weapon, out var sr) || sr == null) return;
            if (_weaponBase == null) _weaponBase = sr.sprite;
            sr.sprite = sprite != null ? sprite : _weaponBase;

            // The off-hand disc is a COPY taken once by SetWeaponSplit, so a flipbook ticking the
            // main hand (Deadlights' turning lights) left the pair's other disc frozen on frame 0.
            // A pair with its own off-hand half steps that half's flipbook to the SAME frame.
            if (_split)
            {
                var off = EnsureOffhand();
                if (off != null) off.sprite = OffhandSpriteFor(sr.sprite);
            }
        }

        public bool DetailArt => _detailArt;

        public void SetDetailArt(bool on)
        {
            if (_detailArt == on) return;
            _detailArt = on;

            // The BODY has a detail set now as well as the gear, so the switch has to redraw both.
            // Repainting only the loadout left the finer head unreachable: nothing else calls
            // DrawPixelBody once the rig is built except a change of appearance.
            //
            // EnsureLayers first, same reason SetAppearance needs it - see that method's own note.
            EnsureLayers();
            if (_pivots.Count > 0) DrawPixelBody();
            if (_lastLoadout != null) Apply(_lastLoadout);
        }

        /// <summary>
        /// The last loadout painted, so a detail-art switch can repaint without the caller having
        /// to re-supply it. Not serialized: after a reload the rig is repainted by whoever owns it.
        /// </summary>
        Loadout _lastLoadout;

        public void Apply(Loadout loadout)
        {
            EnsureLayers();
            _lastLoadout = loadout;
            _weaponBase = null;     // a new loadout is a new baseline for the heat swap
            _lopsidedSwapped = CarryInBackArm;
            _sigilElement = Attunement.Current;

            // A bow's weapon sprite is re-parented onto the BACK arm instead of the front one
            // PivotFor gives every other weapon. The vials sit on the front arm's own side (see
            // VialSlots' mirror of the relic), and the front arm is also the one that reaches for
            // them mid-draw - so the bow itself has to live on the other arm, or "held opposite
            // the vials" and "the reaching hand is the vial-side hand" cannot both be true at
            // once. Re-derived every Apply, so it self-heals if EnsureLayers ever reattaches the
            // layer to its PivotFor default after a domain reload.
            _bowGrip = WeaponIsBow(loadout);
            _singleEdged = WeaponIsSingleEdged(loadout);
            if (_layers.TryGetValue(RigLayer.Weapon, out var weaponSr) && weaponSr != null)
            {
                var wantParent = _bowGrip ? _elbowBack : _elbowFront;
                if (weaponSr.transform.parent != wantParent)
                    weaponSr.transform.SetParent(wantParent, false);
            }

            foreach (var layer in RigLayers.All)
            {
                if (RigLayers.IsBody(layer)) continue;
                var sr = _layers[layer];
                if (sr == null) continue;      // rig torn down mid-call
                sr.enabled = false;
                sr.sprite = null;
            }

            _hasCape = false;
            _pauldronFollow = PauldronFollow;
            _hasScarf = false;
            _shroudOverCloak = false;
            if (_scarfCloth != null) _scarfCloth.Active = false;
            _hoodOn = false;
            _drapeBack = null;
            _drapeSwings = false;
            if (_capeCloth != null) _capeCloth.Active = false;
            if (_drapeCloth != null) _drapeCloth.Active = false;
            _drapeFront = null;
            _hoodBack = null;
            _hoodFront = null;
            _helmBack = null;
            _helmFront = null;
            _tieBackOn = false;
            _faceMaskOnly = false;
            _headSealed = false;
            _trinketLeftHip = false;
            _clothDyed = false;
            bool wantGlowEye = false;

            if (loadout == null) { _glowEyeItem = false; SyncGlowEye(); return; }

            // Before the loop, not inside it: the cape may come after the tabard it dyes in
            // Equipped (GearItem.DyedByBack).
            _clothDyed = ClothDye.TryMassOf(GearCatalog.Get(loadout.Get(GearSlot.Back)), out _clothDye);

            foreach (var entry in loadout.Equipped)
            {
                var item = GearCatalog.Get(entry.ItemId);
                if (item == null) continue;

                // Read off the ITEM, not a layer - a hood is a fact about the whole piece, not
                // about any one sprite it paints, and it has to be known before AnimateScarf's
                // next frame runs whether or not this item happens to paint anything at all.
                if (item.Slot == GearSlot.Back && item.DrapeBack?.Sprite != null)
                    _drapeBack = item.DrapeBack.Sprite;
                if (item.Slot == GearSlot.Back && item.DrapeSwingsWithCape)
                    _drapeSwings = true;
                if (item.Slot == GearSlot.Neck && item.ShroudOverCloak)
                    _shroudOverCloak = true;

                if (item.Slot == GearSlot.Back && item.HasHood)
                {
                    _hoodOn = true;
                    _hoodColor = item.HoodColor;
                    _hoodBack = item.HoodBack?.Sprite;
                }

                // Same reasoning as the hood above: a fact about the whole Head item, read once
                // per Apply rather than per layer it happens to paint (wraith_eye paints no
                // layers at all - see DemoGear's own note).
                if (item.Slot == GearSlot.Head && item.HasGlowingEye) wantGlowEye = true;

                // Same reasoning again: a mask is a fact about the ITEM, not something the rig
                // could work out from the sprite it paints. SyncHeadCoverage derives _helmOn from
                // "HeadArmor has art", which is right for every helm in the catalogue and wrong
                // for a face mask - see GearItem.CoversFaceOnly.
                if (item.Slot == GearSlot.Head && item.CoversFaceOnly) _faceMaskOnly = true;
                if (item.Slot == GearSlot.Head && item.SealsHead) _headSealed = true;
                if (item.Slot == GearSlot.Head) _helmBack = item.HelmBack?.Sprite;

                if (item.Slot == GearSlot.Head && item.HasTieBack)
                {
                    _tieBackOn = true;
                    _tieColor = item.TieColor;
                }

                foreach (var ls in item.LayersFor(_detailArt))
                {
                    if (ls.Sprite == null) continue;

                    // A relic MAY hold a black-diamond WEAPON, and a weapon paints
                    // RigLayer.Weapon - drawing that layer from the relic slot too would put a
                    // second sword over the one actually being held. Everything else a relic
                    // paints (a pouch on RigLayer.Trinket, say, since the Trinket slot merged
                    // into Relic) is real, visible art and belongs on screen - only the one layer
                    // that would collide is skipped, not the whole slot.
                    if (entry.Slot == GearSlot.Relic && ls.Layer == RigLayer.Weapon) continue;

                    // Apply repaints every layer this loadout touches, which would silently
                    // re-show a hidden helmet the moment ANY slot changes. Re-apply the preference
                    // after the loop below instead of trying to special-case it here.
                    // A body layer is exempt from the clear loop above, so anything painted there
                    // sticks forever and buries the body part underneath it.
                    if (RigLayers.IsBody(ls.Layer))
                    {
                        Debug.LogWarning($"[Rig] '{item.ItemId}' paints body layer {ls.Layer}, " +
                                         "which is never cleared - it cannot be unequipped.");
                        continue;
                    }

                    PaintGearLayer(item, ls);
                }
            }

            // AFTER every item has painted, so each glove is cut from the whole sprite it declared.
            PaintElbowSplit(RigLayer.GlovesFront);
            PaintElbowSplit(RigLayer.GlovesBack);
            // ...and each greave and boot likewise at the knee.
            PaintKneeSplit(RigLayer.LegsFront);
            PaintKneeSplit(RigLayer.LegsBack);
            PaintKneeSplit(RigLayer.BootsFront);
            PaintKneeSplit(RigLayer.BootsBack);
            // AFTER every item has painted too: the helm and the hood come from two slots, in
            // whatever order Equipped lists them.
            EncloseHelmInHood();
            // And the head inside a sealed helm - or uncut again, if the helm came off.
            EncloseHeadInHelm();

            _glowEyeItem = wantGlowEye;
            SyncGlowEye();

            _dualWield = WeaponIsDualWield(loadout);
            SetTwoHanded(WeaponIsTwoHanded(loadout));   // recomputes the whole stack

            if (_helmHidden) SetHelmHidden(true);
            if (_facingAway) SetFacingAway(true);
            SyncDrapeSide();
            ApplyWeaponVisibility();

            // AFTER the hidden-helmet preference, not before: how much of the mane is drawn
            // depends on whether a helm is actually visible or a hood is worn, and Apply has just
            // repainted the head-armour layer (and set _hoodOn) with no idea either preference
            // exists.
            SyncHeadCoverage();

            SyncKindled(loadout);
        }

        /// <summary>
        /// The Secret Fire's overlay (KindledMarks) on EVERY gear layer once anything kindled is worn
        /// - not only the layers a kindled item paints: the cuts move its art onto the forearm, hand
        /// and shin layers, a lopsided turn trades sides, and each overlay follows whatever its layer
        /// shows, drawing nothing over a sprite with no marks. They are never taken off again; with
        /// nothing kindled on, each costs one cache lookup a frame.
        /// </summary>
        void SyncKindled(Loadout loadout)
        {
            bool any = false;
            foreach (var entry in loadout.Equipped)
                if (GearCatalog.Get(entry.ItemId) is { Kindled: true }) { any = true; break; }
            if (!any) return;

            foreach (var kv in _layers)
                if (kv.Value != null && !RigLayers.IsBody(kv.Key)) KindledMarks.On(kv.Value);
        }

        /// <summary>
        /// A helmet under a hood is cut to the cowl's outline - the hood ENCLOSES it the way it
        /// already encloses the hair (BodyLook.Mane: nothing pokes through the crown or the cowl).
        /// The cowl draws over the helm (RigLayer.Hood sits above HeadArmor), so only what showed
        /// through the face opening or stuck out past the cloth was ever visible; the opening is
        /// kept and the rest goes. Whatever reaches below the hem still shows, as hair does. See
        /// <see cref="PixelSprite.Enclosed"/>.
        ///
        /// Measured from the FRONT cowl: turned away the helm is off anyway (SetFacingAway).
        /// </summary>
        void EncloseHelmInHood()
        {
            if (!_hoodOn) return;
            if (!_layers.TryGetValue(RigLayer.HeadArmor, out var helm) || helm == null || helm.sprite == null) return;
            if (!_layers.TryGetValue(RigLayer.Hood, out var hood) || hood == null || hood.sprite == null) return;
            helm.sprite = PixelSprite.Enclosed(helm.sprite, hood.sprite,
                hood.transform.worldToLocalMatrix * helm.transform.localToWorldMatrix);
        }

        /// <summary>
        /// Paint one of an item's layers onto the rig - its sprite, place, size, and whatever rest
        /// pose or hinge that layer carries. Apply's per-layer work, and SyncLopsided's.
        /// </summary>
        void PaintGearLayer(GearItem item, LayerSprite ls)
        {
            var layer = ls.Layer;
            var offset = ls.Offset;
            // The wearer's element sigil (GearItem.BearsSigil) in place of the combined picture.
            var sprite = item.SigilFor(layer, _sigilElement, _detailArt) ?? ls.Sprite;
            // Bare skin in the wearer's own tone (GearItem.ShowsSkin). Before the elbow split,
            // which cuts whatever sprite this leaves on the layer.
            if (item.ShowsSkin) sprite = BodyLook.Reskin(sprite, _look.Skin);
            // Cloth in the worn cape's colour (GearItem.DyedByBack), for the same reason.
            if (item.DyedByBack && _clothDyed) sprite = ClothDye.Dye(sprite, _clothDye);
            // A lopsided piece's two sides trade places with the carry (GearItem.Lopsided); a
            // lopsided cape has no pair, and turns over in place.
            if (item.Lopsided && _lopsidedSwapped && Sided(layer))
            {
                if (OtherSide(layer, out var other)) layer = other;
                offset.x = -offset.x;
                sprite = PixelSprite.Mirror(sprite);
            }

            var sr = _layers[layer];
            if (sr == null) return;
            sr.sprite = sprite;
            sr.color = ls.Tint;
            sr.enabled = true;
            // Authored against the SHOULDER; a hand layer now hangs from the elbow.
            sr.transform.localPosition = offset - PivotShift(layer);
            if (layer == RigLayer.Weapon) _weaponGripRest = sr.transform.localPosition;
            if (layer == RigLayer.Shoulders) _shouldersRest = sr.transform.localPosition;
            if (layer == RigLayer.ShouldersBack) _shouldersBackRest = sr.transform.localPosition;
            if (layer == RigLayer.Shoulders || layer == RigLayer.ShouldersBack)
                _pauldronFollow = item.HangsAlongArm ? 1f : PauldronFollow;
            if (layer == RigLayer.BackOver) _backOverRest = sr.transform.localPosition;
            if (layer == RigLayer.NeckOver) _neckOverRest = sr.transform.localPosition;
            if (layer == RigLayer.Trinket)
            {
                _trinketRest = sr.transform.localPosition;
                _trinketLeftHip = item.WornOnLeftHip;
                sr.flipX = false;
            }
            sr.transform.localRotation = Quaternion.identity;
            sr.drawMode = SpriteDrawMode.Simple;

            // The cape bends from its neckline (ClothBend), measured against the rest position,
            // the hinge's height above it and the length below it recorded here. Its sprite
            // pivot stays its centre: a texel-indexed pivot can only land on a texel CENTRE, so
            // one on the top edge of an even-width grid would sit half a texel off the middle.
            if (layer == RigLayer.Back)
            {
                _capeRest = offset;
                // Hinged at the top edge, unless the item says its neckline is lower -
                // a collar standing up past the neck would otherwise put the hinge over
                // the head (GearItem.CapeHingeCells).
                _capeHalfHeight = ls.Size.y * 0.5f - PixelSprite.Px(0f, item.CapeHingeCells).y;
                _capeLength = _capeHalfHeight + ls.Size.y * 0.5f;
                _capeSwingScale = item.CapeSwingScale;
                _hasCape = true;
            }

            // Same hinge trick, on the scarf's own tail layer - RigLayer.NeckBack is
            // nothing but that tail, so this can key on the layer alone, the same way the
            // cape's own check does. The collar itself paints RigLayer.Neck, in front,
            // and is never gated here - it is meant to sit still like any other pendant.
            if (layer == RigLayer.NeckBack)
            {
                _scarfRest = offset;
                _scarfHalfHeight = ls.Size.y * 0.5f;
                _hasScarf = true;
            }

            // Scale to the declared rig-unit size, whatever the source PPU.
            var native = sprite.bounds.size;
            sr.transform.localScale = new Vector3(
                native.x > 0.0001f ? ls.Size.x / native.x : ls.Size.x,
                native.y > 0.0001f ? ls.Size.y / native.y : ls.Size.y,
                1f);
        }

        /// <summary>Whether a lopsided piece's layer changes side with the carry: the arm and
        /// shoulder pairs, and the cape (the Vermilion Cape's lining is turned back on one edge).</summary>
        static bool Sided(RigLayer layer) => layer == RigLayer.Back || OtherSide(layer, out _);

        /// <summary>The same layer on the other arm or shoulder, for a lopsided piece.</summary>
        static bool OtherSide(RigLayer layer, out RigLayer other)
        {
            other = layer switch
            {
                RigLayer.GlovesFront => RigLayer.GlovesBack,
                RigLayer.GlovesBack => RigLayer.GlovesFront,
                RigLayer.Shoulders => RigLayer.ShouldersBack,
                RigLayer.ShouldersBack => RigLayer.Shoulders,
                _ => layer,
            };
            return other != layer;
        }

        /// <summary>
        /// Whether a lopsided piece's sides are painted traded - true while the carry is in
        /// arm.back. Recorded so a turn repaints only when the side actually changes, and so it
        /// always says what is ON the rig: set by Apply, and by SyncLopsided only when it repaints.
        /// </summary>
        bool _lopsidedSwapped;

        /// <summary>
        /// Turned away, the rig keeps the carry in the anatomical RIGHT hand by moving it to
        /// arm.back, so a lopsided piece (GearItem.Lopsided) trades sides with it - or turned away
        /// the Talon plate would sit on the right arm and raise the sword. Repaints only those
        /// pieces' layers and re-cuts only gloves it repainted: a full Apply would also reset the
        /// cape's spring and put the base gem back on a Prism.
        /// </summary>
        void SyncLopsided()
        {
            bool want = CarryInBackArm;
            if (want == _lopsidedSwapped || _lastLoadout == null) return;

            bool started = false, gloves = false, wasStone = _stone;
            foreach (var entry in _lastLoadout.Equipped)
            {
                var item = GearCatalog.Get(entry.ItemId);
                if (item == null || !item.Lopsided) continue;
                if (!started)
                {
                    started = true;
                    _lopsidedSwapped = want;
                    // Both remember each layer's sprite and put THAT back, which would be the
                    // other side's art - ended around the repaint, and the stone re-laid after.
                    if (wasStone) SetStone(false);
                    if (_flashing) SetFlash(0f);
                }
                var layers = item.LayersFor(_detailArt);
                foreach (var ls in layers)
                {
                    if (ls.Sprite == null || !Sided(ls.Layer)) continue;
                    HideGearLayer(ls.Layer);
                    if (OtherSide(ls.Layer, out var other)) HideGearLayer(other);
                    gloves |= ls.Layer is RigLayer.GlovesFront or RigLayer.GlovesBack;
                }
                foreach (var ls in layers)
                    if (ls.Sprite != null && Sided(ls.Layer)) PaintGearLayer(item, ls);
            }
            if (!started) { _lopsidedSwapped = want; return; }

            if (gloves)
            {
                PaintElbowSplit(RigLayer.GlovesFront);
                PaintElbowSplit(RigLayer.GlovesBack);
            }
            if (wasStone) SetStone(true);
        }

        /// <summary>The element whose sigil a BearsSigil piece is painted with - set by Apply
        /// from Attunement, and by SyncAttunement when it repaints.</summary>
        ElementType _sigilElement = ElementType.Fire;

        /// <summary>
        /// Repaint a sigil-bearing piece (GearItem.BearsSigil) for <paramref name="element"/> -
        /// the hub's selector turning, a run starting. Only that layer, SyncLopsided's way: a full
        /// Apply would also reset the cape's spring and put the base gem back on a Prism.
        /// </summary>
        public void SyncAttunement(ElementType element)
        {
            if (element == _sigilElement || _lastLoadout == null) return;
            _sigilElement = element;

            bool started = false, gloves = false, wasStone = _stone;
            foreach (var entry in _lastLoadout.Equipped)
            {
                var item = GearCatalog.Get(entry.ItemId);
                if (item == null || !item.BearsSigil) continue;
                if (!started)
                {
                    started = true;
                    // Both remember each layer's sprite and put THAT back - the old sigil.
                    if (wasStone) SetStone(false);
                    if (_flashing) SetFlash(0f);
                }
                foreach (var ls in item.LayersFor(_detailArt))
                {
                    if (ls.Sprite == null || ls.Layer != item.SigilLayer) continue;
                    PaintGearLayer(item, ls);
                    gloves |= ls.Layer is RigLayer.GlovesFront or RigLayer.GlovesBack;
                }
            }
            if (gloves)
            {
                PaintElbowSplit(RigLayer.GlovesFront);
                PaintElbowSplit(RigLayer.GlovesBack);
            }
            if (wasStone) SetStone(true);
        }

        void HideGearLayer(RigLayer layer)
        {
            if (!_layers.TryGetValue(layer, out var sr) || sr == null) return;
            sr.sprite = null;
            sr.enabled = false;
        }

        /// <summary>
        /// Whether the equipped Head item asks for GearItem.HasGlowingEye, independent of
        /// <see cref="_helmHidden"/> - <see cref="_glowLeftEye"/> is the two combined. Apply sets
        /// this; SetHelmHidden re-derives <see cref="_glowLeftEye"/> from it without needing to
        /// know anything about gear.
        /// </summary>
        bool _glowEyeItem;

        /// <summary>
        /// Repaint the Head layer only when the combined state actually CHANGES - Apply runs on
        /// every equip, and re-baking the face pixel art (a real cost; see RepaintHead's own note
        /// on why it exists apart from DrawPixelBody) on every unrelated slot change would be
        /// paid for nothing most of the time.
        ///
        /// Gated on <see cref="_helmHidden"/> the same way a real helmet's own sprite already is
        /// (see SetHelmHidden) - wraith_eye's whole visible presence IS this effect, so "hide
        /// helm" has nothing else on the item to hide unless this counts too.
        /// </summary>
        void SyncGlowEye()
        {
            bool want = _glowEyeItem && !_helmHidden;
            if (want == _glowLeftEye) return;
            _glowLeftEye = want;

            var skin = BodyLook.SkinRamp(_look.Skin);
            var hair = BodyLook.HairRamp(_look.Hair);
            var eyes = BodyLook.EyeRamp(_look.Eyes);
            var brow = BodyLook.BrowRamp(_look);
            RepaintHead(skin, hair, eyes, brow);
        }

        /// <summary>
        /// Extend a table written for the layers BEFORE the elbow existed with the four forearm
        /// layers, each ranked directly above its own upper half.
        ///
        /// Derived rather than typed into every table, for the reason ShoulderCarryOrder's note
        /// gives: re-ranking a valid permutation cannot produce a duplicate or a hole, where
        /// hand-inserting four entries into seven tables is twenty-eight chances to. And
        /// "directly above its upper half" is the only answer that is right in EVERY stack: a
        /// straight arm then composites exactly as the unbroken sprite did, so no decision any
        /// table documents about the arms changes meaning.
        /// </summary>
        static int[] WithForearms(int[] upperOnly)
        {
            var ranked = new List<RigLayer>();
            for (int i = 0; i < upperOnly.Length; i++) ranked.Add((RigLayer)i);
            ranked.Sort((a, b) => upperOnly[(int)a].CompareTo(upperOnly[(int)b]));

            foreach (var upper in new[] { RigLayer.ArmBack, RigLayer.GlovesBack,
                                          RigLayer.ArmFront, RigLayer.GlovesFront })
                if (RigLayers.LowerOf(upper, out var lower))
                {
                    ranked.Insert(ranked.IndexOf(upper) + 1, lower);
                    // and the hand directly above its forearm, for the same reason
                    if (RigLayers.HandOf(lower, out var hand))
                        ranked.Insert(ranked.IndexOf(lower) + 1, hand);
                }

            var order = new int[ranked.Count];
            for (int i = 0; i < ranked.Count; i++) order[(int)ranked[i]] = i;
            return WithShins(order);
        }

        /// <summary>
        /// Extend a table with the six shin layers, each ranked directly above its own upper half -
        /// <see cref="WithForearms"/>' rule for the same reason: a straight leg then composites
        /// exactly as the unbroken sprite did. Called by WithForearms, so every table that has the
        /// forearms has the shins too.
        /// </summary>
        static int[] WithShins(int[] table)
        {
            var ranked = new List<RigLayer>();
            for (int i = 0; i < table.Length; i++) ranked.Add((RigLayer)i);
            ranked.Sort((a, b) => table[(int)a].CompareTo(table[(int)b]));

            foreach (var upper in new[] { RigLayer.LegBack, RigLayer.LegsBack, RigLayer.BootsBack,
                                          RigLayer.LegFront, RigLayer.LegsFront, RigLayer.BootsFront })
                if (RigLayers.ShinOf(upper, out var lower))
                    ranked.Insert(ranked.IndexOf(upper) + 1, lower);

            var order = new int[ranked.Count];
            for (int i = 0; i < ranked.Count; i++) order[(int)ranked[i]] = i;
            return order;
        }

        /// <summary>
        /// Move <see cref="RigLayer.BackOver"/> to directly above <see cref="RigLayer.TorsoOver"/>
        /// - over both pauldrons and anything capping them, under the head and hood.
        ///
        /// BackOver is the Back slot's FRONT piece (the poncho's yoke, the Wraithguard cloak's
        /// shoulder drape), and a cloak worn over armour covers the shoulder plates. It used to sit
        /// just above TorsoArmor, which put every pauldron on top of the garment meant to be
        /// covering it. Derived, not retyped in each table, for the reason Reranked exists.
        ///
        /// FRONT-FACING TABLES ONLY. Turned away the cape is the outermost layer, and the
        /// facing-away stacks rank BackOver above it instead - see DrapeOverCape.
        /// In the two-handed stack the near arm still draws over the drape, as it already draws
        /// over the pauldron it crosses - see that table's TorsoOver note.
        /// </summary>
        static int[] DrapeOverShoulders(int[] table)
            => Reranked(table, RigLayer.BackOver, after: RigLayer.TorsoOver);

        /// <summary>
        /// Add <see cref="RigLayer.NeckOver"/> directly above <see cref="RigLayer.BackOver"/>: from
        /// the front a neck item's shroud is over the cloak's drape (the user's rule). Applied
        /// after <see cref="DrapeOverShoulders"/>, so it is over the pauldrons too.
        /// </summary>
        static int[] NeckOverDrape(int[] table)
            => Inserted(table, RigLayer.NeckOver, RigLayer.BackOver, above: true);

        /// <summary>
        /// The turned-away counterpart of <see cref="NeckOverDrape"/>: from behind the cape is the
        /// outermost garment, so the shroud goes directly UNDER it - and so under the drape's
        /// back view, which <see cref="DrapeOverCape"/> ranks over the cape.
        /// </summary>
        static int[] NeckUnderCape(int[] table)
            => Inserted(table, RigLayer.NeckOver, RigLayer.Back, above: false);

        /// <summary>
        /// Put <paramref name="layer"/> directly above or below <paramref name="anchor"/>, adding
        /// it if the table does not have it yet (a layer appended to the enum after the table was
        /// written - see <see cref="RigLayer.NeckOver"/>). A permutation in, a permutation out.
        /// </summary>
        static int[] Inserted(int[] table, RigLayer layer, RigLayer anchor, bool above)
        {
            var ranked = new List<RigLayer>();
            for (int i = 0; i < table.Length; i++) ranked.Add((RigLayer)i);
            ranked.Sort((a, b) => table[(int)a].CompareTo(table[(int)b]));
            ranked.Remove(layer);
            ranked.Insert(ranked.IndexOf(anchor) + (above ? 1 : 0), layer);

            var order = new int[ranked.Count];
            for (int i = 0; i < ranked.Count; i++) order[(int)ranked[i]] = i;
            return order;
        }

        /// <summary>
        /// The pauldrons CAP THE ARMS in every stack: each arm down to the WRIST - the arm and the
        /// glove, upper arm and forearm - goes directly under whichever pauldron ranks lower, if
        /// its table had it higher. Only the HANDS and the ring keep a rank above them, which is
        /// all a grip ever needed. Forearms were left above at first, and a hanging pauldron (the
        /// sode reaches past the elbow) had the forearm's harness drawn over its lower lames.
        ///
        /// Before the elbow existed the fist could only rise with its whole arm, so the disc, the
        /// greatsword and the turned-away carry each promoted a WHOLE glove over the pauldrons to
        /// grip their weapon. That was harmless while every glove was a hand. Every Gloves piece is
        /// a full arm harness now (shoulder to fingertips - see DemoGear.GloveRows), and a promoted
        /// harness drew its rerebrace straight over the plate meant to cap it.
        ///
        /// Both pauldrons, not just the one on the same side: SwapArms exchanges the two arms'
        /// ranks and leaves the pauldrons alone, so an arm under only its own would land over the
        /// other one's after a swap. Applied after WithForearms, so a forearm stays where its table
        /// put it; a straight arm still composites exactly, forearm over upper half, just not
        /// always adjacent any more.
        /// </summary>
        static int[] PauldronsCapArms(int[] table)
        {
            var ranked = new List<RigLayer>();
            for (int i = 0; i < table.Length; i++) ranked.Add((RigLayer)i);
            ranked.Sort((a, b) => table[(int)a].CompareTo(table[(int)b]));

            var cap = table[(int)RigLayer.ShouldersBack] < table[(int)RigLayer.Shoulders]
                ? RigLayer.ShouldersBack : RigLayer.Shoulders;
            // arm before glove, upper before forearm, so each still draws over what it covers
            foreach (var part in new[] { RigLayer.ArmBack, RigLayer.ArmBackLower,
                                         RigLayer.GlovesBack, RigLayer.GlovesBackLower,
                                         RigLayer.ArmFront, RigLayer.ArmFrontLower,
                                         RigLayer.GlovesFront, RigLayer.GlovesFrontLower })
            {
                if (ranked.IndexOf(part) < ranked.IndexOf(cap)) continue;
                ranked.Remove(part);
                ranked.Insert(ranked.IndexOf(cap), part);
            }

            var order = new int[ranked.Count];
            for (int i = 0; i < ranked.Count; i++) order[(int)ranked[i]] = i;
            return order;
        }

        /// <summary>
        /// The held disc over the HOOD, and the near hand and its ring back over the disc: a disc
        /// carried at the chest is in front of a cowl hanging past the shoulders (the user's call -
        /// under it, the cowl's side panel cut across the disc). The price is the rule that kept
        /// the face above the disc: the cowl draws over the head, so a disc over the cowl is over
        /// the head too - it only meets the chin at the disc's top edge.
        /// </summary>
        static int[] DiscOverHood(int[] table)
        {
            table = Reranked(table, RigLayer.Weapon, after: RigLayer.Hood);
            table = Reranked(table, RigLayer.ArmFrontHand, after: RigLayer.Weapon);
            table = Reranked(table, RigLayer.GlovesFrontHand, after: RigLayer.ArmFrontHand);
            return Reranked(table, RigLayer.Ring, after: RigLayer.GlovesFrontHand);
        }

        /// <summary>
        /// The turned-away counterpart of <see cref="DrapeOverShoulders"/>: from behind the cape
        /// is the outermost garment and already sits over both pauldrons, so a one-shoulder drape
        /// (the only kind drawn back here - see GearItem.DrapeBack) goes directly above IT,
        /// still under the head and hood. The cape is narrower than a pauldron's overhang; the
        /// drape is what covers the right shoulder's.
        /// </summary>
        static int[] DrapeOverCape(int[] table)
            => Reranked(table, RigLayer.BackOver, after: RigLayer.Back);

        /// <summary>
        /// From behind, the MANE is on the near side of everything the character wears on their
        /// back: long hair falls down over the cloak and the torso, under the head it grows from.
        /// Every front-facing stack keeps it at the very bottom (behind the body, under the cape),
        /// which from behind hid all the length a long style has. Applied after DrapeOverCape, so
        /// it lands over the cloak's drape as well.
        /// </summary>
        static int[] ManeOverCape(int[] table)
            => Reranked(table, RigLayer.HairBack, after: RigLayer.BackOver);

        /// <summary>
        /// Back-to-front order for the plain one-handed stack - the DEFAULT, used whenever none of
        /// <see cref="_facingAway"/>/<see cref="_weaponBehind"/>/<see cref="_twoHanded"/>/
        /// <see cref="_dualWield"/> apply. Everything sits at its own <see cref="RigLayer"/> value
        /// except one swap.
        ///
        /// BACK AND HAIRBACK ARE TRANSPOSED. The raw enum order (used here until this fix) drew
        /// <see cref="RigLayer.HairBack"/> in front of <see cref="RigLayer.Back"/> - reasoned at
        /// the time as protecting the mane from a cape wide enough to swallow it. In play that
        /// reads backwards: a cloak worn over the shoulders and back is the OUTER garment, so
        /// where the two overlap the cloak has to cover the hair hanging under it, not the other
        /// way round - the same "back items and any helmet sit over the hair" rule
        /// <see cref="TwoHandedOrder"/>, <see cref="DualWieldOrder"/> and
        /// <see cref="WeaponBehindOrder"/> now all keep. HeadArmor and Hood already sat well above
        /// HairBack in every stack and needed no change.
        /// </summary>
        static readonly int[] OneHandedOrder = PauldronsCapArms(NeckOverDrape(DrapeOverShoulders(WithForearms(new[]
        {
            1,   // Back           - over the mane; see the note above
            0,   // HairBack       - under the cape now; nothing else moves
            2,   // ArmBack
            3,   // GlovesBack
            4,   // LegBack
            5,   // LegsBack
            6,   // BootsBack
            7,   // NeckBack
            8,   // Torso
            9,   // LegFront
            10,  // LegsFront
            11,  // BootsFront
            12,  // TorsoArmor
            13,  // BackOver       - re-ranked over the pauldrons; see DrapeOverShoulders
            14,  // Tasset         - hangs off the belt, over the cuirass fauld
            15,  // Belt
            16,  // Trinket
            17,  // Neck
            18,  // ArmFront
            19,  // GlovesFront
            20,  // Ring
            21,  // ShouldersBack
            22,  // Shoulders
            23,  // TorsoOver      - the mantle caps the pauldron; see the layer's own doc
            24,  // Head
            25,  // HeadBack
            26,  // HeadArmor
            27,  // Hood
            28,  // Weapon
        }))));

        /// <summary>
        /// Back-to-front order while both hands are on one hilt, indexed by <see cref="RigLayer"/>.
        ///
        /// A one-handed weapon hangs beside the body, so the default stack - sword last, over
        /// everything - is right. A greatsword does not: it is 14 texels wide across the guard and
        /// it sits directly in front of both fists, so drawing it last erases the arms completely.
        /// Measured, not guessed: the weapon's bounds enclosed every arm and glove layer, and the
        /// character came out holding a sword with no visible hands on it.
        ///
        /// So the near arm, its gauntlet and its ring move ABOVE the weapon, while the far arm
        /// comes forward over the torso but stays behind the blade - one fist in front of the
        /// grip, one behind it, which is what reads as a hand wrapped round a hilt rather than a
        /// sword pasted over a body. The blade still passes over the head and shoulders.
        ///
        /// This is a PERMUTATION of 0..24, not a set of nudges. The band is exactly as wide as the
        /// layer count, so there is no spare order to promote anything into; every layer has to
        /// move down to make the room. Anything else silently collides two layers on one order.
        /// </summary>
        /// <summary>
        /// Back-to-front order for a DUAL-WIELD grip (the disc class), indexed by RigLayer.
        ///
        /// Same problem the greatsword has, for the opposite reason. A chakram is mostly hole,
        /// and the hand is meant to show through the middle of the ring - but the grip bar is
        /// opaque and runs straight across that hole, so with the weapon drawn last it covered
        /// the fist and the disc read as pasted onto the character rather than held. Measured:
        /// GlovesFront spans y -17..-7 and sits entirely inside the ring's hollow, so the hand is
        /// in exactly the right place and was simply being painted over.
        ///
        /// So the GAUNTLET and ring rise above the weapon, and the fist closes over the grip bar.
        ///
        /// The disc in turn rises above the pauldron. A disc this size held at the hand genuinely
        /// overlaps the shoulder, and painting the pauldron on top of it read as a shoulder
        /// hovering in front of the weapon. Only the glove is promoted, not the whole arm: the
        /// pauldron must still cap ArmFront, and arm-under-pauldron-under-disc-under-fist is
        /// circular otherwise. It resolves because the glove sits at -17..-7 and the pauldron at
        /// -5..+5, so the two barely meet and their relative order is nearly free.
        ///
        /// A PERMUTATION of 0..24, same contract as the other two - verified in Construct.
        /// </summary>
        static readonly int[] DualWieldOrder = DiscOverHood(PauldronsCapArms(NeckOverDrape(DrapeOverShoulders(WithForearms(new[]
        {
            1,   // Back           - over the mane; see OneHandedOrder's own note
            0,   // HairBack       - under the cape, under the body, on every permutation
            2,   // ArmBack
            3,   // GlovesBack
            4,   // LegBack
            5,   // LegsBack
            6,   // BootsBack
            7,   // NeckBack       - the scarf's tail, behind the body on every permutation
            8,   // Torso
            9,   // LegFront
            10,  // LegsFront
            11,  // BootsFront
            12,  // TorsoArmor
            13,  // BackOver       - re-ranked over the pauldrons; see DrapeOverShoulders
            14,  // Tasset
            15,  // Belt
            16,  // Trinket
            17,  // Neck
            18,  // ArmFront       - still under the pauldron, which caps it as usual
            23,  // GlovesFront    - but the FIST rises above the disc, to grip it
            24,  // Ring
            19,  // ShouldersBack
            20,  // Shoulders
            21,  // TorsoOver      - over the arm and the pauldron, UNDER the held disc: the disc
                 // is carried in front of the body. Their overlap is nearly free anyway - the
                 // mantle stops at y -14 and the fist sits at -17..-7, so only the grip bar's
                 // top edge ever meets it.
            25,  // Head           - stays above the disc, so the face is never covered
            26,  // HeadBack       - unused here (front-facing); kept in step with Head
            27,  // HeadArmor
            28,  // Hood           - a cowl frames whatever the head is doing, helmed or bare
            22,  // Weapon         - in front of the pauldron, behind the fist and the head
        })))));

        static readonly int[] TwoHandedOrder = PauldronsCapArms(NeckOverDrape(DrapeOverShoulders(WithForearms(new[]
        {
            1,   // Back           - over the mane; see OneHandedOrder's own note
            0,   // HairBack       - under the cape, under the body, on every permutation
            16,  // ArmBack        - forward of the torso: it crosses the chest to reach the hilt
            17,  // GlovesBack
            2,   // LegBack
            3,   // LegsBack
            4,   // BootsBack
            5,   // NeckBack       - the scarf's tail, behind the body on every permutation
            6,   // Torso
            7,   // LegFront
            8,   // LegsFront
            9,   // BootsFront
            10,  // TorsoArmor
            11,  // BackOver       - re-ranked over the pauldrons; see DrapeOverShoulders
            12,  // Tasset
            13,  // Belt           - on top of the cuirass, same as the one-handed stack
            14,  // Trinket
            15,  // Neck
            26,  // ArmFront       - above the weapon, so the near fist grips it
            27,  // GlovesFront
            28,  // Ring
            18,  // ShouldersBack
            19,  // Shoulders
            20,  // TorsoOver      - UNDER the blade and under the near arm here, which is the one
                 // stack where the mantle does not cap the arm. The cycle is unbreakable without
                 // restructuring this permutation: the blade has to cross in front of the chest
                 // (it stands a head taller than the collar and would otherwise be sliced in
                 // half by it), and the near arm has to stay in front of the blade to grip it.
                 // Of the two artefacts, arm-over-mantle is the cheaper: this stack already
                 // draws the near arm over Shoulders, so shoulder armour under the greatsword
                 // arm is established behaviour rather than something new this layer introduces.
            21,  // Head
            22,  // HeadBack       - unused here (front-facing); kept in step with Head
            23,  // HeadArmor
            24,  // Hood           - a cowl frames whatever the head is doing, helmed or bare
            25,  // Weapon         - over the body, under the near hand
        }))));

        /// <summary>
        /// Back-to-front order while the weapon is on the FAR side of the body, indexed by
        /// <see cref="RigLayer"/>.
        ///
        /// This is how the alt-swing says "other shoulder". A 3/4 figure this small has no room
        /// to express which side of the body a two-handed sword is on by POSITION - the offsets
        /// large enough to read tear the fist off the shoulder - but depth says it instantly.
        /// The weapon and the whole near arm drop behind the torso and head, so the blade passes
        /// behind the character rather than across their chest, and the two basics of a chain
        /// become unmistakably different swings.
        ///
        /// A PERMUTATION of 0..24, same contract as <see cref="TwoHandedOrder"/>: the band is
        /// exactly as wide as the layer count, so everything shifts up to make room rather than
        /// anything being promoted into a spare slot.
        /// </summary>
        /// <summary>
        /// The stack for the shouldered rest carry: the weapon drops to just above
        /// <see cref="RigLayer.Torso"/>, which puts it behind the head, behind both arms, and
        /// behind whatever is worn on the chest.
        ///
        /// WHY NOT REUSE <see cref="WeaponBehindOrder"/>, which sounds like it already does this.
        /// Measured, it does not: it ranks the weapon ABOVE the head and above the carrying arm.
        /// That is correct for what it exists for - an alt-swing crossing the body, where the
        /// blade is behind the TORSO but still in front of the face and the fist - and it is
        /// exactly wrong here. Seen on screen it reads as the sword floating beside the character
        /// rather than held, because the grip is painted over the hand holding it.
        ///
        /// DERIVED, not typed. <see cref="CheckLayerOrders"/> validates these as exact
        /// permutations of 0..n-1, so a hand-written twenty-eight-entry table is a transcription
        /// error waiting to be a vanished layer. Re-ranking an existing stack cannot produce a
        /// duplicate or a hole, and it means this one inherits every ordering decision the
        /// two-handed stack already documents instead of restating them and drifting.
        /// </summary>
        /// <summary>
        /// Back-to-front order while the weapon RESTS ON THE SHOULDER and the character faces the
        /// camera: the default stack with the weapon sunk to just above the cape and the mane.
        ///
        /// The blade lies across the character's BACK - from the carrying fist beside the head,
        /// behind the collar, down to past the far hip - so from the front everything the body is
        /// made of sits between the camera and it. Only what clears the silhouette shows: the
        /// handle and pommel standing up past the head, and the blade's lower run beside the
        /// hip, exactly as in the reference.
        ///
        /// ONE TABLE SERVES BOTH FACINGS, which is what the right-hand carry buys. Whichever rig
        /// arm is carrying keeps its ordinary rank: arm.front (the near arm, the carrier while
        /// facing right) stays in front of the torso, arm.back (the far arm, the carrier while
        /// facing left) stays behind it - and both are above the sunken blade, so the carrying
        /// fist closes over the handle either way.
        ///
        /// Above the cape and the mane, which is a readability call rather than the physical
        /// answer: a sword laid over a cloak would sit under it from the front, and the cape is
        /// wide enough to hide most of the blade's run. WeaponBehindOrder already made the same
        /// call for the same reason.
        /// </summary>
        static readonly int[] ShoulderCarryOrder = Reranked(OneHandedOrder,
            RigLayer.Weapon, after: RigLayer.Back);

        /// <summary>
        /// Back-to-front order for the MARCH carry (blade shouldered, see MarchBladeDegrees): the
        /// weapon over the torso, belt and amulet, under the carrying arm, the pauldrons and the
        /// head. The hilt is in front of the chest, so it shows with the fist closed over it; the
        /// blade runs back over the shoulder, so the pauldron and head cover where it crosses and
        /// it reads as resting ON the shoulder. Under ShoulderCarryOrder the hilt was hidden
        /// behind the chest and the fist held nothing.
        /// </summary>
        static readonly int[] MarchCarryOrder = ForearmOverShoulders(Reranked(OneHandedOrder,
            RigLayer.Weapon, after: RigLayer.Neck));

        /// <summary>
        /// The carrying arm's forearm, glove and hand over the shoulder pieces (pauldrons, mantle,
        /// drape, shroud), the user's call: in the march carry the forearm comes up IN FRONT of
        /// the chest, so the hand armour is nearer the camera than any plate on the shoulder, and
        /// capped under them (PauldronsCapArms) the fist holding the hilt was lost under the
        /// pauldron and cloak. Only the march's carrying arm (arm.front - the march is never
        /// turned away): a hanging forearm sits beside the body, where the cap is still right.
        /// The upper arm stays capped - the pauldron still sits over the shoulder it rises from.
        /// </summary>
        static int[] ForearmOverShoulders(int[] table)
        {
            var top = RigLayer.Shoulders;
            foreach (var cap in new[] { RigLayer.ShouldersBack, RigLayer.TorsoOver,
                                        RigLayer.BackOver, RigLayer.NeckOver })
                if (table[(int)cap] > table[(int)top]) top = cap;

            // forearm before glove before hand, so each still draws over what it covers
            foreach (var part in new[] { RigLayer.ArmFrontLower, RigLayer.GlovesFrontLower,
                                         RigLayer.ArmFrontHand, RigLayer.GlovesFrontHand,
                                         RigLayer.Ring })
            {
                table = Reranked(table, part, after: top);
                top = part;
            }
            return table;
        }

        /// <summary>
        /// <see cref="FacingAwayOrder"/> for when arm.FRONT is the carrying arm (turned away and
        /// mirrored) - the same stack with the two arms' ranks exchanged, so the fist promoted
        /// over the hilt is the one actually holding it.
        ///
        /// Lazy, not a field initialiser: FacingAwayOrder is declared further down, and static
        /// fields initialise in textual order - read here it would still be null.
        /// </summary>
        static int[] FacingAwayFrontCarryOrder => _facingAwayFrontCarry ??= SwapArms(FacingAwayOrder);
        static int[] _facingAwayFrontCarry;

        /// <summary>
        /// <see cref="TwoHandedOrder"/> while a two-handed swing has the hands UP - the near fist
        /// above the shoulder line. TwoHandedOrder caps both arms under the pauldrons
        /// (PauldronsCapArms), which puts them under the head too: right while they hang at the
        /// hips, wrong the moment the hilt comes up in front of the face - the near forearm hid
        /// behind the chin while its fist floated on the hilt, and the far fist vanished so the
        /// sword read as held in one hand.
        ///
        /// Both forearms, gloves and fists come over the head: the far arm's under the weapon (the
        /// far hand on a hilt), the near arm's over it. The upper arms stay capped - they still
        /// rise from under the pauldrons. Switched by HandsUp. Lazy for the same textual-order
        /// reason as FacingAwayFrontCarryOrder.
        /// </summary>
        static int[] HandsUpOrder => _handsUpOrder ??= LiftedArms(TwoHandedOrder, nearUpper: false);
        static int[] _handsUpOrder;

        /// <summary>
        /// <see cref="HandsUpOrder"/> once the near ELBOW is up past the neck (a Chop's cock, the
        /// charge's hold): the near upper arm comes over the head too, rising from its shoulder in
        /// front of the face. The far upper arm stays behind the head - the ordinary 3/4 depth for
        /// raised arms, its forearm going back behind the head with it and only its fist over it.
        /// Both whole arms over the head (tried) hid the face at the top of the cock, and so did
        /// the far forearm left over it. Switched by ArmsAboveNeck.
        /// </summary>
        static int[] ArmsRaisedOrder => _armsRaisedOrder ??= LiftedArms(TwoHandedOrder, nearUpper: true);
        static int[] _armsRaisedOrder;

        static int[] LiftedArms(int[] table, bool nearUpper)
        {
            // The far fist (and, hands up, its forearm), just over whatever the head is wearing -
            // which keeps them under the weapon, ranked above all of it in TwoHandedOrder. Raised,
            // the far forearm goes back behind the head with its upper arm.
            var head = RigLayer.Head;
            foreach (var part in new[] { RigLayer.HeadBack, RigLayer.HeadArmor, RigLayer.Hood })
                if (table[(int)part] > table[(int)head]) head = part;
            var after = head;
            var far = nearUpper
                ? new[] { RigLayer.ArmBackHand, RigLayer.GlovesBackHand }
                : new[] { RigLayer.ArmBackLower, RigLayer.GlovesBackLower,
                          RigLayer.ArmBackHand, RigLayer.GlovesBackHand };
            foreach (var part in far)
            {
                table = Reranked(table, part, after: after);
                after = part;
            }

            // The near arm over everything else - upper arm first when it is raised. Measured from
            // what is NOT being moved: the near fist and ring are already at the very top of
            // TwoHandedOrder, and ranking a layer after itself is undefined.
            var near = new List<RigLayer>();
            if (nearUpper) { near.Add(RigLayer.ArmFront); near.Add(RigLayer.GlovesFront); }
            near.AddRange(new[] { RigLayer.ArmFrontLower, RigLayer.GlovesFrontLower,
                                  RigLayer.ArmFrontHand, RigLayer.GlovesFrontHand, RigLayer.Ring });
            int topRank = -1;
            var top = RigLayer.Head;
            for (int i = 0; i < table.Length; i++)
                if (!near.Contains((RigLayer)i) && table[i] > topRank)
                { topRank = table[i]; top = (RigLayer)i; }
            foreach (var part in near)
            {
                table = Reranked(table, part, after: top);
                top = part;
            }
            return table;
        }

        /// <summary>Move one layer to rank directly above another, everything else keeping its
        /// relative order. A permutation in, a permutation out.</summary>
        static int[] Reranked(int[] table, RigLayer layer, RigLayer after)
        {
            var ranked = new List<RigLayer>();
            for (int i = 0; i < table.Length; i++) ranked.Add((RigLayer)i);
            ranked.Sort((a, b) => table[(int)a].CompareTo(table[(int)b]));
            ranked.Remove(layer);
            ranked.Insert(ranked.IndexOf(after) + 1, layer);

            var order = new int[table.Length];
            for (int i = 0; i < ranked.Count; i++) order[(int)ranked[i]] = i;
            return order;
        }

        /// <summary>Exchange the ranks of every front-arm layer with its back-arm twin.</summary>
        static int[] SwapArms(int[] table)
        {
            var order = (int[])table.Clone();
            void Swap(RigLayer a, RigLayer b) => (order[(int)a], order[(int)b]) = (order[(int)b], order[(int)a]);
            Swap(RigLayer.ArmFront, RigLayer.ArmBack);
            Swap(RigLayer.GlovesFront, RigLayer.GlovesBack);
            Swap(RigLayer.ArmFrontLower, RigLayer.ArmBackLower);
            Swap(RigLayer.GlovesFrontLower, RigLayer.GlovesBackLower);
            Swap(RigLayer.ArmFrontHand, RigLayer.ArmBackHand);
            Swap(RigLayer.GlovesFrontHand, RigLayer.GlovesBackHand);
            return order;
        }

        static readonly int[] WeaponBehindOrder = PauldronsCapArms(NeckOverDrape(DrapeOverShoulders(WithForearms(new[]
        {
            1,   // Back           - still hangs behind the body; over the mane now (see
                 // OneHandedOrder's own note), same swap as every other stack
            0,   // HairBack       - under the cape, under the body, on every permutation
            6,   // ArmBack
            7,   // GlovesBack
            8,   // LegBack
            9,   // LegsBack
            10,  // BootsBack
            11,  // NeckBack       - the scarf's tail, behind the body on every permutation
            12,  // Torso          - body now covers the blade
            13,  // LegFront
            14,  // LegsFront
            15,  // BootsFront
            16,  // TorsoArmor
            17,  // BackOver       - re-ranked over the pauldrons; see DrapeOverShoulders
            18,  // Tasset
            19,  // Belt
            20,  // Trinket
            21,  // Neck
            3,   // ArmFront       - the whole weapon arm goes behind with it
            4,   // GlovesFront
            5,   // Ring
            22,  // ShouldersBack
            23,  // Shoulders
            24,  // TorsoOver      - the weapon and the whole near arm are behind the body here,
                 // so the mantle caps everything below the head with no cycle to resolve
            25,  // Head
            26,  // HeadBack       - unused here (front-facing); kept in step with Head
            27,  // HeadArmor
            28,  // Hood           - a cowl frames whatever the head is doing, helmed or bare
            2,   // Weapon         - behind the body, in front of the hair and the cape only
        }))));

        /// <summary>
        /// Back-to-front order while <see cref="_facingAway"/> is on - see
        /// <see cref="ICharacterRig.SetFacingAway"/> for what this is for and why it is never
        /// driven by aim.
        ///
        /// Three moves from the default stack, each doing a different job:
        ///
        /// BACK PROMOTES ABOVE THE WHOLE BODY. A cape at its declared order (0, behind
        /// everything) is drawn almost entirely under the character's own silhouette and barely
        /// shows past the edges at rest - measured live: fully invisible at idle. From behind, a
        /// cape is the outermost surface between the camera and the character's back, so it has
        /// to draw OVER Torso/TorsoArmor/Shoulders/arms to say that - checked live, and it reads
        /// as a worn garment rather than a glitch. It still sits BELOW Head/HeadBack: a cloak
        /// covers the shoulders, not the skull, unless it HasHood, which Hood already handles
        /// separately and is left out of this pass for now (see the class doc's open items).
        ///
        /// THE WEAPON RISES TO THE TOP, above even the promoted cape. This is the one entry here
        /// that is a statement about DEPTH rather than about costume, and it reverses what this
        /// table did for a long time.
        ///
        /// The old rule sank the weapon to 0, behind body and cloak alike, on the reasoning that a
        /// blade bigger than the body would still show its tip and pommel past the silhouette and
        /// so read as "slung across the back" with no new art. That was written before the
        /// shouldered carry existed, when nothing was HELD and the weapon really was strapped
        /// behind the character.
        ///
        /// It is now held on the shoulder, and the carry does not change when the character turns
        /// round - only which side of them the camera is on does. Facing the camera, the head sits
        /// between the viewer and the blade, so the blade draws behind it; that is ShoulderCarryOrder
        /// and it was always right. Turn the character away and the same blade becomes the NEAR
        /// surface: it should cover the back of the head, not hide behind it. Left at 0 the result
        /// read as a character walking along with a sword in front of their face, because the one
        /// thing a viewer can be certain of - that the sword is between them and the character - was
        /// the one thing the sorting denied.
        ///
        /// IT DOES NOT GO ALL THE WAY TO THE TOP, and that was the correction after it did. With the
        /// whole sword in front of everything, nothing occluded any part of it and it read as a
        /// decal pasted on the character's back rather than an object resting on them. The CARRYING
        /// FIST (GlovesBack here; FacingAwayFrontCarryOrder swaps it for GlovesFront when the
        /// carry is in arm.front) is promoted above it so it cuts across the grip end - one overlap is all
        /// it takes to put the sword in the scene instead of on top of it. The blade still crosses the
        /// head freely, which is the part that had to be true.
        ///
        /// THE PAULDRON IS NOT PROMOTED, though it once was alongside the fist. It sits on the far
        /// shoulder from the grip and never touches the blade, so promoting it bought nothing for the
        /// sword - and left a plate drawn OVER the cape, which from behind is the outer garment. It
        /// sits directly under the cape instead.
        ///
        /// HEAD SWAPS FOR HEADBACK; HEADARMOR, HOOD, RING, TRINKET AND NECK DROP OUT ENTIRELY.
        /// SetFacingAway does the actual hiding (a sorting order can't disable a renderer); their
        /// slots here only need to be valid, unique numbers to keep this an honest permutation -
        /// see the class doc's own warning about what an accidental collision costs.
        ///
        /// THE BELT AND THE TASSET ARE PROMOTED ABOVE TORSO AND TORSOARMOR, which the rest of the
        /// gear is not. Everything else worn on the chest is a FRONT surface and is correctly
        /// buried or hidden back here; a belt is a closed loop and hip armour wraps the hips, so
        /// from behind you are looking at the back of the same strap. Left at their default order
        /// the belt was drawn entirely under the torso - measured, not assumed: the strap spans
        /// rig-local -2.5 to -7.5 and the torso -8 to +14, so it is wholly inside the body's own
        /// footprint and nothing of it survived. The tasset was getting away with the same mistake
        /// only because it hangs BELOW the torso's extent, where sorting barely reaches it.
        ///
        /// They keep the front stack's own relationship (TorsoArmor under BackOver under Belt) and
        /// stay UNDER the promoted cape, which is still the outermost garment back here. Every
        /// other layer's relative order is untouched.
        /// </summary>
        static readonly int[] FacingAwayOrder = PauldronsCapArms(ManeOverCape(DrapeOverCape(NeckUnderCape(WithForearms(new[]
        {
            22, // Back
            0,  // HairBack
            1,  // ArmBack
            28, // GlovesBack   - the carrying fist, over the hilt - the anchor that matters
            2,  // LegBack
            3,  // LegsBack
            4,  // BootsBack
            5,  // NeckBack
            15, // Torso
            6,  // LegFront
            7,  // LegsFront
            8,  // BootsFront
            16, // TorsoArmor
            17, // BackOver
            18, // Tasset
            19, // Belt
            9,  // Trinket
            10, // Neck
            12, // ArmFront
            13, // GlovesFront
            11, // Ring
            14, // ShouldersBack
            21, // Shoulders   - the far-side pauldron, UNDER the cape - nothing rests on it
            20, // TorsoOver
            23, // Head
            24, // HeadBack   - the back of the head, under the blade crossing it
            25, // HeadArmor  - over HeadBack: a helm's back view (GearItem.HelmBack) covers the skull
            26, // Hood       - over HeadBack: the cowl's back view covers the skull stand-in
            27, // Weapon   - over the head, UNDER the fist holding it
        })))));

        /// <summary>
        /// Back-to-front order while <see cref="_facingAway"/> is on and the weapon is HELD rather
        /// than shouldered - the upright grip, a one-hander, a bow, or a two-hander mid-chain.
        ///
        /// <see cref="FacingAwayOrder"/> is written for the rest carry, where the blade lies over
        /// the far shoulder and so really is the near surface from behind. Applied to a held grip,
        /// its promotions put the carrying fist and the hilt ON TOP OF THE CAPE, in the middle of
        /// the character's back - the grip is in front of the chest, which from behind is the far
        /// side of the body. So here the weapon and both arms SINK below everything instead, the
        /// old "behind body and cloak alike" rule the shoulder carry replaced: the blade still shows
        /// wherever it clears the silhouette (above the head, past the hip), and nothing that is
        /// physically in front of the character is drawn over their back.
        ///
        /// The pauldron sinks back beside its far twin, since nothing rests on it here. Every other
        /// layer keeps its relative order from FacingAwayOrder.
        /// </summary>
        static readonly int[] FacingAwayHeldOrder = PauldronsCapArms(ManeOverCape(DrapeOverCape(NeckUnderCape(WithForearms(new[]
        {
            24, // Back         - still the outermost garment
            6,  // HairBack
            1,  // ArmBack      - both arms reach forward to the grip, on the far side of the body
            2,  // GlovesBack
            7,  // LegBack
            8,  // LegsBack
            9,  // BootsBack
            10, // NeckBack
            18, // Torso
            11, // LegFront
            12, // LegsFront
            13, // BootsFront
            19, // TorsoArmor
            20, // BackOver
            21, // Tasset
            22, // Belt
            14, // Trinket
            15, // Neck
            3,  // ArmFront
            4,  // GlovesFront
            5,  // Ring
            16, // ShouldersBack
            17, // Shoulders
            23, // TorsoOver
            25, // Head
            26, // HeadBack
            27, // HeadArmor    - over HeadBack, as in FacingAwayOrder
            28, // Hood         - over HeadBack: the cowl's back view covers the skull stand-in
            0,  // Weapon       - under everything; only what clears the body shows
        })))));

        static bool WeaponIsTwoHanded(Loadout loadout)
        {
            foreach (var entry in loadout.Equipped)
            {
                var item = GearCatalog.Get(entry.ItemId);
                if (item != null && item.Slot == GearSlot.Weapon) return item.TwoHanded;
            }
            return false;   // bare hands counter-swing like any other walk
        }

        /// <summary>
        /// Drop the weapon and its arm behind the body, or bring them back to the front.
        ///
        /// Overrides the two-handed restack while it is on: that one exists to keep both fists
        /// visible ON the hilt, and it still holds here because the arm travels behind WITH the
        /// weapon rather than being separated from it.
        /// </summary>
        /// <summary>
        /// Motions aimed at the GROUND rather than at a target, which therefore ignore the aim
        /// residual. Plant is the only one: it goes straight down at the character's own feet, so
        /// there is nothing for it to be aimed at and the blade must read as vertical whatever the
        /// auto-targeter is pointing at.
        /// </summary>
        static bool IgnoresAim(AttackMotion m) => m == AttackMotion.Plant;

        void SetWeaponBehind(bool behind)
        {
            if (_weaponBehind == behind) return;
            _weaponBehind = behind;
            ApplyLayerOrder();
        }

        /// <summary>Switch the lifted-arms stacks - see HandsUpOrder and ArmsRaisedOrder.</summary>
        void SetArmsLifted(bool handsUp, bool raised)
        {
            if (_handsUp == handsUp && _armsRaised == raised) return;
            _handsUp = handsUp;
            _armsRaised = raised;
            ApplyLayerOrder();
        }

        /// <summary>Whether HandsUpOrder is applied. A plain bool, so a domain reload keeps it.</summary>
        bool _handsUp;

        /// <summary>
        /// How far above / below the shoulder line the near fist must be to lift / drop the
        /// forearms over the head, rig units in the torso's frame. Either side so a hand hovering
        /// there can't flicker the stack.
        /// </summary>
        const float HandsUpOn = 0.01f;
        const float HandsUpOff = -0.02f;

        /// <summary>
        /// Whether a two-handed swing has the near fist above the shoulder line this frame - see
        /// HandsUpOrder. The same gating as ArmsAboveNeck.
        /// </summary>
        bool HandsAboveShoulders()
        {
            if (!LiftsArms()) return false;
            float shoulder = _armFrontRest.y;
            // The FIST, not the weapon's grip point (which sits at the wrist): the fist is what
            // floated over the face with its forearm hidden behind the chin.
            float fist = _torso.InverseTransformPoint(_elbowFront.TransformPoint(CarryGrip)).y;
            return fist > shoulder + (_handsUp ? HandsUpOff : HandsUpOn);
        }

        /// <summary>The poses the lifted-arms stacks amend: a live two-handed swing or charge
        /// facing the camera, on the near side - see ArmsAboveNeck.</summary>
        bool LiftsArms()
        {
            if (_attackTimer <= 0f && _chargeTimer <= 0f) return false;
            if (!HeldTwoHanded || HeldBow || _facingAway || _weaponBehind || _shoulderCarry) return false;
            return _torso != null && _head != null && _elbowFront != null;
        }

        /// <summary>Whether ArmsRaisedOrder is applied. A plain bool, so a domain reload keeps it.</summary>
        bool _armsRaised;

        /// <summary>
        /// How far above / below the neck the near elbow must be to raise / lower the arms'
        /// stack, rig units in the torso's frame. Either side of the neck so a pose hovering at
        /// it can't flicker the stack every frame. Measured on a Chop out of the march: the elbow
        /// sits at 0.16 shouldered, 0.35-0.44 through the cock, back under 0.19 by the strike's
        /// end; the neck is at 0.29.
        /// </summary>
        const float ArmsRaisedOn = -0.03f;
        const float ArmsRaisedOff = -0.06f;

        /// <summary>
        /// ...and how far above the neck the near FIST must be as well - the hands genuinely
        /// overhead (about eye level). On the elbow alone, a high elbow with the hands still down
        /// raised the stack: a frame of Impale's draw, and the rest carry's lifted elbow as a
        /// swing opened, flicked the near upper arm over its pauldron and back. A Chop's cock
        /// holds the fist at 0.45 against a neck at 0.29.
        /// </summary>
        const float FistOverheadOn = 0.08f;
        const float FistOverheadOff = 0.05f;

        /// <summary>
        /// Whether a two-handed swing has the hands up past the head this frame - see
        /// ArmsRaisedOrder. Read off the POSE (the near elbow against the neck), not off which
        /// motion is playing, so every overhead frame of every swing and the charge's hold get it
        /// and nothing else does. Only for the stack it amends: the alt-swing's far side
        /// (WeaponBehindOrder) puts the arm behind the head on purpose, and the carries and the
        /// facing-away stacks are poses of their own.
        /// </summary>
        bool ArmsAboveNeck()
        {
            if (!LiftsArms()) return false;
            float neck = _torso.InverseTransformPoint(_head.position).y;
            float elbow = _torso.InverseTransformPoint(_elbowFront.position).y;
            float fist = _torso.InverseTransformPoint(_elbowFront.TransformPoint(CarryGrip)).y;
            return elbow > neck + (_armsRaised ? ArmsRaisedOff : ArmsRaisedOn)
                && fist > neck + (_armsRaised ? FistOverheadOff : FistOverheadOn);
        }

        /// <summary>Switch the rest carry's stack on or off - see ShoulderCarryOrder.</summary>
        void SetShoulderCarry(bool carrying)
        {
            if (_shoulderCarry == carrying) return;
            _shoulderCarry = carrying;
            ApplyLayerOrder();
        }

        public void SetSortingBase(int order)
        {
            if (BaseSortingOrder == order) return;
            BaseSortingOrder = order;
            ApplyLayerOrder();
        }

        void ApplyLayerOrder()
        {
            // Facing-away wins over every combat stack. It is a scripted, non-combat state -
            // nothing plays a swing while it is on - so there is no case where it needs to
            // agree with which shoulder a two-hander is drawn from.
            int[] table = _facingAway    ? (!_shoulderCarry ? FacingAwayHeldOrder
                                           : _carryInBack  ? FacingAwayOrder
                                                           : FacingAwayFrontCarryOrder)
                        : _weaponBehind   ? WeaponBehindOrder
                        : _shoulderCarry  ? (_marchOrdered ? MarchCarryOrder : ShoulderCarryOrder)
                        : HeldTwoHanded   ? TwoHandedOrder
                        : _dualWield      ? DualWieldOrder
                                          : OneHandedOrder;

            // Hands up in a two-handed swing: the forearms come over the head, then the near arm.
            if (table == TwoHandedOrder)
                table = _armsRaised ? ArmsRaisedOrder : _handsUp ? HandsUpOrder : table;

            // A REACHING arm.back crosses in front of the body (the right hand going to the left
            // hip, facing left), so it takes arm.front's rank for the duration.
            if (_reaching && CarryInBackArm && !_facingAway) table = SwapArms(table);

            // A shroud worn as the outermost garment (GearItem.ShroudOverCloak). Under a hood it
            // goes directly over the HOOD in either facing - and so over the chin, or the back of
            // the head, where the cowl would cover them; the weapon and the gripping hands stay
            // above it. Without one it keeps its place under the head from the front, and from
            // behind it goes over the cape and the drape's back view, under the mane.
            if (_shroudOverCloak && _hoodOn)
                table = Inserted(table, RigLayer.NeckOver, RigLayer.Hood, above: true);
            else if (_shroudOverCloak && _facingAway)
                table = Inserted(table, RigLayer.NeckOver, RigLayer.BackOver, above: true);

            foreach (var layer in RigLayers.All)
            {
                if (!_layers.TryGetValue(layer, out var sr) || sr == null) continue;
                sr.sortingOrder = BaseSortingOrder + table[(int)layer];
            }

            if (_split) SetWeaponSplit(true);   // re-derives the off-hand's order from the new base
        }

        bool _dualWield;

        /// <summary>A one-handed weapon whose class wants the hand painted over it.</summary>
        static bool WeaponIsDualWield(Loadout loadout)
        {
            if (loadout == null) return false;
            var item = GearCatalog.Get(loadout.Get(GearSlot.Weapon));
            return item != null && !item.TwoHanded && item.Class == WeaponClass.Disc;
        }

        static bool WeaponIsSingleEdged(Loadout loadout)
        {
            if (loadout == null) return false;
            var item = GearCatalog.Get(loadout.Get(GearSlot.Weapon));
            return item != null && item.SingleEdged;
        }

        /// <summary>The held blade has one edge (GearItem.SingleEdged), re-read every Apply.</summary>
        bool _singleEdged;

        /// <summary>
        /// Whether the current swing turns a single-edged blade OVER (the sprite mirrored across
        /// its own long axis, the march roll's mechanism) so its edge leads. Set at PlayAttack from
        /// the whole swing's sweep (<see cref="SweepSign"/>), then re-decided every frame from the
        /// sweep just ahead (<see cref="SweepSignBetween"/>), so it follows a swing that reverses
        /// - a thrust or a hold, with no sweep, keeps the PlayAttack answer. A clockwise cut
        /// leads with the sprite's right side, so the left-authored edge is turned there; a
        /// counter-clockwise cut already leads with it; a thrust - no sweep - turns it DOWN, which
        /// is the right side again for a blade along the facing. Applied only in the combat grip:
        /// the carry keeps the authored side.
        /// </summary>
        bool _edgeTurned;

        /// <summary>
        /// Which way a motion's blade SWEEPS: the sign of its angular travel weighted by speed, so
        /// the strike (the fastest part) decides and a cock or a recovery doesn't. 0 for a motion
        /// with no real sweep (the thrust family).
        /// </summary>
        static float SweepSign(AttackMotion motion, bool alt, float seconds)
        {
            const int Steps = 32;
            float sum = 0f, total = 0f, prev = 0f;
            for (int i = 0; i <= Steps; i++)
            {
                float arm = 0f, back = 0f, body = 0f;
                var reach = Vector3.zero;
                Animate(motion, i / (float)Steps, alt, false, seconds, ref arm, ref back, ref body, ref reach);
                float blade = arm + body;
                if (i > 0)
                {
                    float d = Mathf.DeltaAngle(prev, blade);
                    sum += d * Mathf.Abs(d);
                    total += d * d;
                }
                prev = blade;
            }
            // A sweep is one-directional; a thrust's small wobble cancels out.
            return total > 0f && Mathf.Abs(sum) > 0.5f * total && total > 400f ? Mathf.Sign(sum) : 0f;
        }

        /// <summary>Seconds ahead of the current frame the edge looks to see which way the blade
        /// is about to sweep - long enough to catch the next stroke while the blade is still
        /// stopped before it, so the edge turns over in the stop, not a frame into the cut.</summary>
        const float EdgeLookAheadSeconds = 0.1f;

        /// <summary>Degrees the blade must travel inside the look-ahead before it counts as a
        /// STROKE. Below it - a cock, an overshoot settling back, a hold, a thrust's wobble - the
        /// edge stays as it was. At 12 a Chop's cock flipped the edge for two frames at the start
        /// and its settle flipped it again in the follow-through; real strokes cover 100+.</summary>
        const float EdgeMinTravel = 40f;

        /// <summary>
        /// <see cref="SweepSign"/> over part of a motion, <paramref name="k0"/> to
        /// <paramref name="k1"/>: the sign of the blade's speed-weighted travel there, 0 when it
        /// travels less than <see cref="EdgeMinTravel"/> or doesn't go one way.
        /// </summary>
        static float SweepSignBetween(AttackMotion motion, bool alt, float seconds, float k0, float k1)
        {
            const int Steps = 8;
            float sum = 0f, total = 0f, travel = 0f, prev = 0f;
            for (int i = 0; i <= Steps; i++)
            {
                float arm = 0f, back = 0f, body = 0f;
                var reach = Vector3.zero;
                Animate(motion, Mathf.Lerp(k0, k1, i / (float)Steps), alt, false, seconds,
                        ref arm, ref back, ref body, ref reach);
                float blade = arm + body;
                if (i > 0)
                {
                    float d = Mathf.DeltaAngle(prev, blade);
                    sum += d * Mathf.Abs(d);
                    total += d * d;
                    travel += Mathf.Abs(d);
                }
                prev = blade;
            }
            return travel >= EdgeMinTravel && Mathf.Abs(sum) > 0.5f * total ? Mathf.Sign(sum) : 0f;
        }

        static bool WeaponIsBow(Loadout loadout)
        {
            if (loadout == null) return false;
            var item = GearCatalog.Get(loadout.Get(GearSlot.Weapon));
            return item != null && item.Class == WeaponClass.Bow;
        }

        /// <summary>
        /// Ask for the shouldered rest carry - see ICharacterRig.SetGripShouldered.
        ///
        /// Deliberately does NOT restack the layers. SetTwoHanded's permutation exists so both
        /// fists draw above the weapon sprite, and a fist hanging at the hip is nowhere near the
        /// blade to be drawn wrongly against it - so the rest carry reads correctly under
        /// TwoHandedOrder unchanged. That matters: ApplyLayerOrder rebuilds every layer's sorting
        /// order, and this flips on and off around every chain.
        /// </summary>
        public void SetGripShouldered(bool shouldered) => _gripShouldered = shouldered;

        // ---------------------------------------------------------------- the reach

        /// <summary>Where the right fist has been sent, in the root's local frame - see
        /// ICharacterRig.SetHandTarget. NonSerialized so a domain reload DROPS the reach: the
        /// coroutine that would have handed the arm back does not survive one, and a restored
        /// flag would hold the arm out for the rest of the session.</summary>
        [System.NonSerialized] bool _reaching;
        [System.NonSerialized] Vector2 _reachTarget;
        [System.NonSerialized] float _reachOffHand;

        public void SetHandTarget(Vector2? rootLocal, float offHand = 1f)
        {
            if (rootLocal.HasValue) _reachTarget = rootLocal.Value;
            _reachOffHand = Mathf.Clamp01(offHand);
            if (rootLocal.HasValue == _reaching) return;
            _reaching = rootLocal.HasValue;
            ApplyLayerOrder();   // the reaching arm crosses the body, so it is drawn in front of it
        }

        /// <summary>
        /// The reach is an OVERRIDE laid on top of Update's pose, not a branch inside it: Update
        /// runs exactly as it would have (the grip stays whatever it was), and this rewrites the
        /// two arms afterwards. THE GRIP IS NEVER OPENED FOR A REACH. It was, once - the carry was
        /// the easy way to make the other hand let go - and the draw-cut that follows Crosscut's
        /// sheathe then started from the carry: the sword still turned 70 degrees in the fist and
        /// sunk behind the body in the carry's stack, with the arm swinging it in front. Posing
        /// the free arm here instead hands the rig back the very grip it had. In LateUpdate because the
        /// thing being reached for is moved by a coroutine, which resumes AFTER Update - solved
        /// there, the fist would trail the handle by a frame on a slide only seven frames long.
        /// </summary>
        void LateUpdate()
        {
            if (_stone) return;     // a statue holds the pose it was caught in
            if (_reaching) ApplyReach();
        }

        /// <summary>
        /// The point on the segment from <paramref name="rest"/> to <paramref name="relocated"/>
        /// nearest to rest that puts <paramref name="target"/> within <paramref name="reach"/> -
        /// the relocated end if nothing on the segment does.
        /// </summary>
        static Vector2 ShoulderToReach(Vector2 rest, Vector2 relocated, Vector2 target, float reach)
        {
            Vector2 d = target - rest, e = relocated - rest;
            if (d.sqrMagnitude <= reach * reach || e.sqrMagnitude < 1e-8f) return rest;
            // |d - u e| = reach, smallest root: u^2 |e|^2 - 2u (d.e) + |d|^2 - reach^2 = 0
            float ee = e.sqrMagnitude, de = Vector2.Dot(d, e);
            float disc = de * de - ee * (d.sqrMagnitude - reach * reach);
            float u = disc >= 0f ? (de - Mathf.Sqrt(disc)) / ee : de / ee;
            return rest + e * Mathf.Clamp01(u);
        }

        public bool TryGetSwordHand(out Vector2 rootLocal)
        {
            var elbow = CarryInBackArm ? _elbowBack : _elbowFront;
            rootLocal = elbow != null ? (Vector2)transform.InverseTransformPoint(elbow.TransformPoint(CarryGrip))
                                      : Vector2.zero;
            return elbow != null;
        }

        /// <summary>
        /// Two-bone reach for the RIGHT arm (the carrying one - see CarryInBackArm), putting its
        /// FIST on <see cref="_reachTarget"/>. The fist is where the carry closes the hand on the
        /// grip (<see cref="CarryGrip"/>), so a reach to where the carry's own fist already is
        /// lands on exactly the carry's own angles.
        ///
        /// Solved in the TORSO's frame, where both segments hang straight down at zero rotation.
        /// The elbow bends to the side the carry bends it (outward, over the shoulder), which keeps
        /// the solution continuous from the carry into the reach: picking the "lower" elbow instead
        /// flips branch half way through the motion and the forearm pops. The combat grip - where
        /// the finisher actually starts - holds the arm straight, where both branches meet, so it
        /// is continuous from there too. Out of reach, the arm points straight at the target and
        /// the fist falls short rather than detaching.
        ///
        /// No blend weight, deliberately: a caller starting from TryGetSwordHand has no snap to
        /// hide, and a blend between the carry's pose and the reach's put the fist off the handle
        /// the whole way across (the two arms are nowhere near each other).
        /// </summary>
        void ApplyReach()
        {
            bool back = CarryInBackArm;
            var arm = back ? _armBack : _armFront;
            var elbow = back ? _elbowBack : _elbowFront;
            if (arm == null || elbow == null || arm.parent == null) return;

            Vector2 s = arm.localPosition;
            Vector2 t = arm.parent.InverseTransformPoint(transform.TransformPoint(_reachTarget));
            float upper = Mathf.Abs(elbow.localPosition.y);
            float fore = Mathf.Abs(CarryGrip.y);

            // arm.back only reaches the hilt at all by RELOCATING its shoulder (ArmBackGripRest -
            // see its note), and opening the grip sends that shoulder home. So a reaching
            // arm.back slides from its rest toward the grip's shoulder by exactly as much as the
            // fist needs to arrive, and no more: all the way at the start, where the sword is still
            // in the combat grip, and home again by the time it is across at the saya. Measured
            // off distance alone, so it moves as continuously as the target does.
            if (back)
            {
                s = ShoulderToReach(_armBackRest, ArmBackGripRest, t, upper + fore - 1e-3f);
                arm.localPosition = new Vector3(s.x, s.y, arm.localPosition.z);
            }

            ReleaseOffHand(back);

            var toTarget = t - s;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upper - fore) + 1e-4f, upper + fore - 1e-4f);
            float cos = (upper * upper + d * d - fore * fore) / (2f * upper * d);
            float bend = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Rad2Deg;

            // The carry turns arm.front clockwise and arm.back anticlockwise (side), and its elbow
            // folds the same way - which puts the elbow ANTICLOCKWISE of shoulder-to-fist on the
            // front arm and clockwise on the back one.
            float upperDeg = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg + (back ? -bend : bend);
            var elbowAt = s + upper * new Vector2(Mathf.Cos(upperDeg * Mathf.Deg2Rad),
                                                  Mathf.Sin(upperDeg * Mathf.Deg2Rad));
            var toFist = t - elbowAt;
            float foreDeg = Mathf.Atan2(toFist.y, toFist.x) * Mathf.Rad2Deg;

            // Both segments point straight DOWN (-90) at zero rotation.
            arm.localRotation = Quaternion.Euler(0f, 0f, upperDeg + 90f);
            elbow.localRotation = Quaternion.Euler(0f, 0f, Mathf.DeltaAngle(upperDeg, foreDeg));
        }

        /// <summary>
        /// The OTHER hand lets go of the grip by <see cref="_reachOffHand"/>: from wherever Update
        /// posed it on the hilt, to hanging at its own shoulder the way the carry hangs it. Blended
        /// by joint, not solved - it holds nothing, so there is no point it has to land on.
        /// </summary>
        void ReleaseOffHand(bool reachingBack)
        {
            var arm = reachingBack ? _armFront : _armBack;
            var elbow = reachingBack ? _elbowFront : _elbowBack;
            if (arm == null) return;
            float w = _reachOffHand;
            Vector3 rest = reachingBack ? _armFrontRest : _armBackRest;
            // Splayed off the body - the sign that swings a hand away from the belly is mirrored
            // between the two shoulders (see ShoulderOffArmDegrees).
            float hang = reachingBack ? -ShoulderOffArmDegrees : ShoulderOffArmDegrees;
            arm.localPosition = Vector3.Lerp(arm.localPosition, rest, w);
            arm.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(arm.localEulerAngles.z, hang, w));
            if (elbow != null)
                elbow.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(elbow.localEulerAngles.z, 0f, w));
        }

        // ---------------------------------------------------------------- the present

        // Held out at arm's length, barrel forward: the upper arm a little under horizontal, the
        // forearm level, the weapon canted 10 degrees up. Angles are in the ROOT's frame (the
        // torso's lean is taken back off), because the picture is a gun pointed at the horizon
        // whichever way the body happens to be leaning. The weapon's total of -80 is the blade
        // pointing forward: the grids are drawn blade-up, so a clockwise quarter turn puts the
        // Sniper's rib and hammer on TOP and its underlug and trigger guard below - a revolver
        // held the right way up.

        const float PresentShoulderDegrees = 75f;
        const float PresentElbowDegrees = 12f;
        const float PresentWeaponTotalDegrees = -80f;

        /// <summary>See ICharacterRig.SetPresent. NonSerialized so a domain reload drops it, like
        /// the reach: whatever was easing it does not survive one.</summary>
        [System.NonSerialized] float _present;

        public void SetPresent(float weight) => _present = Mathf.Clamp01(weight);

        /// <summary>
        /// Lays the present over Update's pose, blended by its weight. Only while the weapon is in
        /// the COMBAT grip (arm.front's elbow): the request keeps the carry from forming (see
        /// wantShouldered), and the weight is held back until the carry has let go past its
        /// halfway point, which is where the weapon changes arms - so the arm never lifts a sword
        /// it is not holding. The off hand lets go and hangs, as the reach's does.
        /// </summary>
        void ApplyPresent()
        {
            float w = _facingAway ? 0f : _present * Mathf.Clamp01(1f - 2f * _gripBlend);
            if (w <= 0f || _armFront == null || _elbowFront == null) return;

            float lean = _armFront.parent != null
                ? Mathf.DeltaAngle(0f, _armFront.parent.localEulerAngles.z) : 0f;
            float shoulder = PresentShoulderDegrees - lean;

            _armFront.localRotation = Quaternion.Euler(0, 0,
                Mathf.LerpAngle(_armFront.localEulerAngles.z, shoulder, w));
            _armFront.localPosition = Vector3.Lerp(_armFront.localPosition, _armFrontRest, w);
            _elbowFront.localRotation = Quaternion.Euler(0, 0,
                Mathf.LerpAngle(_elbowFront.localEulerAngles.z, PresentElbowDegrees, w));

            if (_weaponPivot != null && _weaponPivot.parent == _elbowFront)
            {
                float weapon = PresentWeaponTotalDegrees - PresentShoulderDegrees - PresentElbowDegrees;
                _weaponPivot.localRotation = Quaternion.Euler(0, 0,
                    Mathf.LerpAngle(_weaponPivot.localEulerAngles.z, weapon, w));
                _weaponPivot.localPosition = Vector3.Lerp(_weaponPivot.localPosition, _weaponGripRest, w);
            }

            if (_armBack != null && HeldTwoHanded && !HeldBow)
            {
                _armBack.localPosition = Vector3.Lerp(_armBack.localPosition, _armBackRest, w);
                _armBack.localRotation = Quaternion.Euler(0, 0,
                    Mathf.LerpAngle(_armBack.localEulerAngles.z, ShoulderOffArmDegrees, w));
                if (_elbowBack != null)
                    _elbowBack.localRotation = Quaternion.Euler(0, 0,
                        Mathf.LerpAngle(_elbowBack.localEulerAngles.z, 0f, w));
            }
        }

        /// <summary>Restack for a two-handed grip, or put every layer back where it belongs.</summary>
        void SetTwoHanded(bool twoHanded)
        {
            _twoHanded = twoHanded;
            ApplyLayerOrder();
        }

        // ---------------------------------------------------------------- animation

        /// <summary>The weapon layer's own transform - see ICharacterRig.WeaponAnchor. Read from
        /// _pivots, which EnsureLayers re-attaches after a domain reload, so this survives a script
        /// edit during play the way a captured reference would not.</summary>
        public Transform WeaponAnchor
            => _pivots.TryGetValue(RigLayer.Weapon, out var t) ? t : null;

        /// <summary>The weapon layer's renderer - see ICharacterRig.WeaponRenderer. Read from
        /// _layers for the same reason WeaponAnchor reads _pivots: EnsureLayers re-attaches that
        /// dictionary after a domain reload, and a captured reference would not survive one.</summary>
        public SpriteRenderer WeaponRenderer
            => _layers.TryGetValue(RigLayer.Weapon, out var sr) ? sr : null;

        /// <summary>The worn Trinket layer's renderer - see ICharacterRig.TrinketRenderer. Read
        /// from _layers for the same reason WeaponRenderer is.</summary>
        /// <summary>The off hand's disc while the pair is split - see ICharacterRig.OffhandRenderer.</summary>
        public SpriteRenderer OffhandRenderer => _split ? EnsureOffhand() : null;

        public SpriteRenderer TrinketRenderer
            => _layers.TryGetValue(RigLayer.Trinket, out var sr) ? sr : null;

        public bool TryGetWeaponVisual(out Sprite sprite, out Color tint, out Vector2 size)
        {
            sprite = null; tint = Color.white; size = Vector2.one;
            if (!_layers.TryGetValue(RigLayer.Weapon, out var sr) || sr == null || sr.sprite == null)
                return false;

            sprite = sr.sprite;
            tint = sr.color;

            // True world size = the sprite's own bounds times whatever scale the layer carries.
            // Reading localScale alone was only ever right because every Spr sprite is exactly
            // 1x1 units, so scale happened to equal size. A pixel sprite is drawn at its native
            // size with scale (1,1), and the old reading would have sent a 1x1-unit slab flying.
            var world = Vector2.Scale(sr.sprite.bounds.size, sr.transform.localScale) * _visualScale;
            size = new Vector2(Mathf.Abs(world.x), Mathf.Abs(world.y));
            return true;
        }

        public bool TryGetDiscVisual(int index, out Sprite sprite, out Color tint, out Vector2 size)
        {
            if (!TryGetWeaponVisual(out sprite, out tint, out size)) return false;
            // The order the hands empty (ApplyWeaponVisibility): the main hand's disc goes first,
            // the off hand's second, the rest alternate.
            int which = index >= 0 ? index : _weaponHides;
            if (which % 2 == 1) sprite = OffhandSpriteFor(sprite);
            return true;
        }

        readonly Dictionary<RigLayer, Sprite> _preFlash = new();
        bool _flashing;

        /// <summary>
        /// Swap every visible layer to a white silhouette while the flash is hot, then swap back.
        ///
        /// Binary rather than a fade: at Health's decay rate this is about 0.13s of white, and a
        /// gradient is not reachable by tint on baked art anyway. Gear flashes too, which the old
        /// single-renderer version never managed.
        /// </summary>
        public void SetFlash(float t)
        {
            bool want = t > 0.35f;
            if (want == _flashing) return;
            _flashing = want;

            if (want)
            {
                _preFlash.Clear();
                foreach (var layer in RigLayers.All)
                {
                    if (!_layers.TryGetValue(layer, out var sr) || sr == null) continue;
                    if (!sr.enabled || sr.sprite == null) continue;
                    _preFlash[layer] = sr.sprite;
                    sr.sprite = PixelSprite.Silhouette(sr.sprite);
                }
                return;
            }

            foreach (var kv in _preFlash)
                if (_layers.TryGetValue(kv.Key, out var sr) && sr != null) sr.sprite = kv.Value;
            _preFlash.Clear();
        }

        // NON-READONLY and rebuilt if null: a domain reload empties a Dictionary while the
        // renderers it points at survive (see CLAUDE.md, Domain reload traps).
        Dictionary<RigLayer, Sprite> _preStone;
        bool _stone;

        public void SetStone(bool on)
        {
            if (on == _stone) return;

            // The flash remembers whatever sprite each layer had when it began, and puts THAT back.
            // Ended first either way, so it cannot restore flesh over stone, or stone over flesh.
            if (_flashing) SetFlash(0f);
            _stone = on;
            _preStone ??= new Dictionary<RigLayer, Sprite>();
            EnsureLayers();

            if (on)
            {
                _preStone.Clear();
                foreach (var layer in RigLayers.All)
                {
                    if (!_layers.TryGetValue(layer, out var sr) || sr == null) continue;
                    if (sr.sprite == null) continue;
                    _preStone[layer] = sr.sprite;
                    sr.sprite = PixelSprite.Stone(sr.sprite);
                }
                return;
            }

            foreach (var kv in _preStone)
                if (_layers.TryGetValue(kv.Key, out var sr) && sr != null) sr.sprite = kv.Value;
            _preStone.Clear();
        }

        /// <summary>
        /// Hide the main-hand layer while the weapon is airborne. Remembers what was equipped so
        /// showing it again does not need the loadout re-applied.
        /// </summary>
        public void SetWeaponVisible(bool visible)
        {
            // REFERENCE COUNTED, not a flag.
            //
            // As a flag this broke the moment two things hid the weapon at once: the second hide
            // recorded "it was already invisible", so neither of them could restore it and the
            // character stayed empty-handed for the rest of the run. A disc volley throws five at
            // once, so that is not an edge case - it is the common path.
            if (!visible) _weaponHides++;
            else _weaponHides = Mathf.Max(0, _weaponHides - 1);

            ApplyWeaponVisibility();
        }

        int _weaponHides;

        /// <summary>A standing preference, orthogonal to the transient hides above - see
        /// ICharacterRig.SetWeaponHidden.</summary>
        bool _weaponHiddenPref;

        public void SetWeaponHidden(bool hidden)
        {
            _weaponHiddenPref = hidden;
            ApplyWeaponVisibility();
            ApplyLayerOrder();   // the grip's stack goes with the grip - see HeldTwoHanded
        }

        /// <summary>
        /// The grip the ARMS take: the equipped one, unless the weapon is hidden by preference.
        /// A hidden weapon leaves nothing in the hands, so they hang at the sides like a bare
        /// character's rather than closing on an invisible hilt in front of the belt and chest,
        /// which is the gear the hide exists to show. The transient hides (a thrown blade) leave
        /// the grip alone: that weapon is coming back to the hand it left.
        /// </summary>
        bool HeldTwoHanded => _twoHanded && !_weaponHiddenPref;

        /// <summary>The bow's grip, under the same rule as <see cref="HeldTwoHanded"/>.</summary>
        bool HeldBow => _bowGrip && !_weaponHiddenPref;

        void ApplyWeaponVisibility()
        {
            if (!_layers.TryGetValue(RigLayer.Weapon, out var sr) || sr == null) return;
            bool shown = !_weaponHiddenPref && _weaponHides == 0;
            sr.enabled = shown;
            if (_split && _offhand != null) _offhand.enabled = !_weaponHiddenPref && _weaponHides <= 1;
        }

        // ---------------------------------------------------------------- off-hand (dual wield)
        //
        // Deliberately NOT a RigLayer. The enum's declaration order IS paint order, and both
        // sorting permutations are exact permutations of 0..21 - adding a member to show a
        // mirrored copy of a sprite the rig already owns would renumber every entry in both
        // arrays and put a whole-character sorting bug one typo away. This hangs off the back
        // arm instead and borrows GlovesBack's order, so it animates and stacks correctly with
        // no change to the layer contract.

        SpriteRenderer _offhand;
        bool _split;

        // A pair whose off hand holds a DIFFERENT half (SetOffhandPicture). All null for an
        // ordinary disc, whose off hand copies the main.
        Sprite _offArena, _offMenu;
        Sprite[] _offMainFrames, _offFrames;

        public void SetOffhandPicture(Sprite arena, Sprite menu, Sprite[] mainFrames, Sprite[] offFrames)
        {
            _offArena = arena;
            _offMenu = menu;
            _offMainFrames = mainFrames;
            _offFrames = offFrames;
            if (_split) SetWeaponSplit(true);
        }

        /// <summary>What the off hand draws while the main hand draws <paramref name="main"/>:
        /// the matching frame of its own flipbook, its own still at this density, or - for an
        /// ordinary disc - the main sprite itself.</summary>
        Sprite OffhandSpriteFor(Sprite main)
        {
            if (_offArena == null && _offMenu == null) return main;
            if (_offMainFrames != null && _offFrames != null && main != null)
            {
                int i = System.Array.IndexOf(_offMainFrames, main);
                if (i >= 0 && i < _offFrames.Length) return _offFrames[i];
            }
            return _detailArt && _offMenu != null ? _offMenu : (_offArena != null ? _offArena : main);
        }

        SpriteRenderer EnsureOffhand()
        {
            // Found by NAME before being created: this is a private field, so a domain reload
            // clears it while the GameObject itself survives, and a blind create would leave a
            // second disc welded to the arm every time a script changed during play.
            if (_offhand != null) return _offhand;
            EnsureElbows();
            if (_elbowBack == null) return null;

            // In the HAND, so on the forearm - the same frame the main weapon's offset is in,
            // which SetWeaponSplit copies straight across.
            var existing = _elbowBack.Find("WeaponOffhand");
            if (existing != null)
            {
                _offhand = existing.GetComponent<SpriteRenderer>();
                if (_offhand != null) return _offhand;
                Object.Destroy(existing.gameObject);
            }

            var go = new GameObject("WeaponOffhand");
            go.transform.SetParent(_elbowBack, false);
            _offhand = go.AddComponent<SpriteRenderer>();
            // A kindled pair's second disc burns too (the Aether Dual Discs' lioness). SyncKindled
            // only reaches _layers; the overlay draws nothing over a disc with no marks.
            KindledMarks.On(_offhand);
            return _offhand;
        }

        /// <summary>
        /// Show a second copy of the weapon in the off hand. The disc class is the only caller:
        /// a chakram in one hand is just a chakram, and the pair is what makes it a DUAL disc.
        /// </summary>
        public void SetContactShadow(bool on) => _shadowHidden = !on;

        public void SetWeaponSplit(bool split)
        {
            _split = split;

            var off = EnsureOffhand();
            if (off == null) return;

            if (!split || !_layers.TryGetValue(RigLayer.Weapon, out var main) || main == null
                || main.sprite == null)
            {
                off.enabled = false;
                return;
            }

            off.sprite = OffhandSpriteFor(main.sprite);
            off.color  = main.color;

            // IN THE BACK HAND: the same hand-relative position the main disc has in the front
            // one, so the far fist closes on its grip exactly as the near fist does (the far arm
            // draws over the disc - see the sorting note below). It used to be shoved 0.12 units
            // further OUT to clear the near disc's silhouette, which left the grip
            // floating a hand's width beside the fist - two hands holding their discs two
            // different ways. The discs overlap more now; the far one is behind the body anyway.
            var t = main.transform;
            off.transform.localPosition = t.localPosition;
            off.transform.localRotation = t.localRotation;
            off.transform.localScale    = t.localScale * 0.92f;

            // BEHIND the whole character, so the far arm passes over its disc.
            //
            // The two hands deliberately read differently: the near disc is gripped through its
            // hollow with the fist painted on top of the bar, while the far one is simply carried
            // behind the body. Trying to give both the same head-on grip put a second full ring
            // across the torso and buried the armour.
            //
            // -1 rather than a layer's order because every one of the 22 is spoken for. The depth
            // band is 32 wide, so orders base+0..base+21 leave ten spare below; base-1 sits in
            // that gap, still clear of the previous depth step, whose top is base-11.
            off.sortingOrder = BaseSortingOrder - 1;

            // One hide is a single disc in flight - the off hand still has its own, which is
            // exactly what the player just did. Only a volley empties both hands.
            off.enabled = _weaponHides <= 1;
        }

        // ---------------------------------------------------------------- bow's hip vials

        /// <summary>
        /// A corked glass reagent vial - the bow class's own hip decoration, on the side
        /// opposite the relic's Trinket medallion. Tall and slender with a rounded base, per the
        /// reference sketch (a slung bandolier vial, not a squat jar), and worn crossed rather
        /// than stacked - see the per-slot offset/rotation table below.
        ///
        /// Six wide (even, for the mirror). The cork is a heavy, deliberately oversized cap: the
        /// sketch reads as "a stopper with a sliver of glass hanging off it" rather than a bottle
        /// with a small lid, and a thin cork band drawn to scale disappeared entirely once shrunk
        /// to this size.
        /// </summary>
        static readonly string[] VialRows =
        {
            ".KKKK.",
            "KKKKKK",
            "KKKKKK",
            ".llll.",
            ".llll.",
            "lbbbbl",
            "lbbbbl",
            "lbbbbl",
            "lbbbbl",
            "lddddl",
            "lddddl",
            "lddddl",
            "lssssl",
            "lssssl",
            ".ssss.",
            ".ssss.",
            "..ss..",
        };

        static Dictionary<char, Color> _vialPal;
        static Dictionary<char, Color> VialPal => _vialPal ??=
            // Saturated teal glass against a warm gold cork - the two ramps sit on opposite
            // sides of the colour wheel on purpose, so the block of colour reads as "two
            // materials" even with none of the internal shading resolving at this size.
            Palette.Of(new Palette.Ramp(new Color(0.20f, 0.62f, 0.62f)),
                       new Palette.Ramp(new Color(0.80f, 0.60f, 0.20f)));

        /// <summary>
        /// Pivoted near the TOP of the cork rather than the sprite's own centre, so a rotation
        /// swings the body like something hanging from a strap up there - which is what makes two
        /// oppositely-rotated copies read as crossed rather than as two vials that both happen to
        /// be tilted the same way.
        /// </summary>
        static readonly Vector2Int VialPivot = new(3, 1);

        /// <summary>
        /// One (offset, rotation) pair per worn vial, in DRAW order. Upright, at the buckle - a
        /// utility-belt cluster, not the earlier crossed-sling sketch.
        ///
        /// X started at the EXACT mirror of the relic's own Trinket offset (Px(5, 1)) rather than a
        /// nearby approximation, then pulled in a texel further to actually hug the hip instead of
        /// standing off it. Y sits above the relic's own +1: that offset is where the relic's
        /// CENTRE sits, since it uses a plain centred pivot, but the vial uses a pivot near the
        /// cork (see VialPivot) so the same anchor Y left almost the whole sprite hanging below it
        /// - measured at about 0.13 world units lower than the relic's own visual centre. Y=6 first
        /// overcorrected past level; 4 is what actually reads as matched.
        ///
        /// The base pair (slots 0-1) sit close together, at the front, by the buckle - the primary
        /// placement. Slot 2 (Extra Sigil) steps out further to the side rather than slotting into
        /// that same cluster, on the reasoning that the front spot is only sized for two; a third
        /// squeezed in there would crowd the pair the belt is built around instead of reading as a
        /// deliberate addition.
        /// </summary>
        static readonly (Vector2 offset, float rotation)[] VialSlots =
        {
            (new Vector2(-4f, 4f),  0f),
            (new Vector2(-7f, 4f),  0f),
            (new Vector2(-11f, 4f), 0f),
        };

        const int MaxVials = 3;
        const float VialPpu = 75f;
        SpriteRenderer[] _vials;

        /// <summary>
        /// Found by NAME before being created, the same defence <see cref="EnsureOffhand"/> uses:
        /// a private array is not restored across a domain reload while the GameObjects it points
        /// at survive, and a blind create would weld a second rack onto the hip on every edit.
        /// </summary>
        SpriteRenderer[] EnsureVials()
        {
            if (_vials != null) return _vials;
            if (_torso == null) return null;

            _vials = new SpriteRenderer[MaxVials];
            var sprite = PixelSprite.From("gear.trinket.vial", VialRows, VialPal,
                                          outline: true, pivotTexel: VialPivot, pixelsPerUnit: VialPpu);
            for (int i = 0; i < MaxVials; i++)
            {
                string name = "Vial" + i;
                var existing = _torso.Find(name);
                if (existing != null)
                {
                    _vials[i] = existing.GetComponent<SpriteRenderer>();
                    if (_vials[i] != null) continue;
                    Object.Destroy(existing.gameObject);
                    existing = null;
                }

                var go = new GameObject(name);
                go.transform.SetParent(_torso, false);
                var (offset, rotation) = VialSlots[i];
                go.transform.localPosition = PixelSprite.Px(offset.x, offset.y);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                // Deliberately NOT BaseSortingOrder + (int)RigLayer.Trinket: that raw enum value
                // only lines up with where Trinket actually ends up on screen when every layer is
                // in its DEFAULT order. TwoHanded (which the bow always is) PERMUTES that whole
                // band - ArmBack alone jumps to order 12, ahead of Trinket's own permuted slot at
                // 10 - to bring the off arm across the chest for the grip, and my vials never went
                // through that permutation. The result was vials sorted as if nothing had moved,
                // landing behind the very arm the grip brings forward. There is no grip state
                // where a hip accessory should read as tucked behind the body, so this sits one
                // past the whole 22-layer band (0..21, on every permutation table) rather than
                // trying to track whichever permutation happens to be active.
                sr.sortingOrder = BaseSortingOrder + RigLayers.All.Length + i;
                sr.enabled = false;
                _vials[i] = sr;
            }
            return _vials;
        }

        /// <summary>
        /// Show this many vials, 0 hides the rack entirely. Called every frame the same way
        /// SetWeaponSplit is - cheap, since it is just enabling up to three already-built
        /// renderers - so a stack gained or lost mid-run shows up immediately.
        /// </summary>
        public void SetVialCount(int count)
        {
            var vials = EnsureVials();
            if (vials == null) return;
            for (int i = 0; i < vials.Length; i++)
                if (vials[i] != null) vials[i].enabled = i < count;
        }

        /// <summary>
        /// Raise the drawing off the ground. Applied in Update alongside the root's spin, because
        /// the root transform is rewritten there every frame and a one-shot write here would be
        /// overwritten before it was ever seen.
        /// </summary>
        public void SetAirborne(float height) => _airHeight = Mathf.Max(0f, height);

        public void SetPhantomOffset(Vector2 offset) => _phantomOffset = offset;

        /// <summary>Switch to the relaxed preview idle - see <see cref="ShowcasePose"/>.</summary>
        public void SetShowcasePose(bool on) => ShowcasePose = on;

        /// <summary>Height the next swing hops; see <see cref="ICharacterRig.SetSwingHop"/>.</summary>
        public void SetSwingHop(float height) => _swingHop = Mathf.Max(0f, height);

        /// <summary>
        /// The hop's height at this point through a swing.
        ///
        /// Rises across the cock and falls across the strike, both timed off the SAME phase
        /// boundaries the arc uses. The fall is quadratic rather than eased: a hop that floated
        /// down reads as the character drifting, whereas one that accelerates into the ground
        /// reads as weight being dropped into the blow. Flat zero once the strike has landed -
        /// the follow-through happens on the ground, with the feet planted.
        /// </summary>
        float HopAt(float k, float seconds)
        {
            if (_swingHop <= 0f) return 0f;
            ChopPhases(seconds, out float cockEnd, out float strikeEnd);
            if (k >= strikeEnd) return 0f;

            if (k < cockEnd)
                return _swingHop * Mathf.Sin(Mathf.Clamp01(k / cockEnd) * Mathf.PI * 0.5f);

            float u = (k - cockEnd) / Mathf.Max(0.0001f, strikeEnd - cockEnd);
            return _swingHop * (1f - u * u);
        }

        /// <summary>
        /// Duration is the real interval to the next swing, clamped so the motion always resolves
        /// slightly early. A fixed length made Flurry's 0.21s jab restart at 88% of its arc - the
        /// arm never finished, so the fastest moveset in the game looked like every other swing.
        /// </summary>
        /// <summary>Blade up and held, trembling harder as the release approaches.</summary>
        public void PlayCharge(float seconds)
        {
            _chargeLength = Mathf.Max(0.05f, seconds);
            _chargeTimer = _chargeLength;
            _attackTimer = 0f;   // a charge supersedes any swing still playing

            // Blended into over the swings' own entry window, from wherever the arm is - it once
            // SNAPPED, which no lead-in can land exactly, so every charge opened on a one-frame jump.
            _entryArmSwing = _poseArmSwing;
            _entryBackSwing = _poseBackSwing;
            _entryBodySpin = _poseBodySpin;
            _entryArmReach = _poseArmReach;
        }

        public void PlayAttack(AttackMotion motion, float duration, bool alt)
        {
            _chargeTimer = 0f;
            _attackHold = 0f;
            _holdElapsed = 0f;
            _motion = motion;
            _alt = alt;
            _attackLength = AttackMotions.SwingSeconds(duration);
            _attackTimer = _attackLength;
            // Counter-clockwise already leads with the authored edge; anything else turns it over.
            if (_singleEdged) _edgeTurned = SweepSign(motion, alt, _attackLength) <= 0f;

            // Where the arm actually IS right now - the previous swing's end pose, a charge, or
            // the walk cycle. The new swing's opening frames blend from here to its own wind-up,
            // because a swing whose canonical start disagrees with the current pose otherwise
            // TELEPORTS to it on frame one: chop 1 snapped 100 degrees out of idle, and the
            // finisher snapped 175 degrees out of the previous chop's wrapped follow-through.
            // (Chop 3 was the only swing that ever looked right, purely because the reversed
            // chop before it happens to END where a chop begins.)
            _entryArmSwing = _poseArmSwing;
            _entryBackSwing = _poseBackSwing;
            _entryBodySpin = _poseBodySpin;
            _entryArmReach = _poseArmReach;

            // Out of the CARRY, though, the arm is not where _poseArmSwing says - the grip blend
            // owns it, and that blend IS the travel into the swing. Blending in from the walk's
            // hanging arm as well made the carry-to-swing LerpAngle pick its short way against a
            // target still on the move: from the rest carry the first frame went the wrong way
            // round (the blade flicked up in front) and the next snapped back over the top. So
            // the swing starts on its own first frame and only the grip blend travels.
            if (_gripBlend > 0.5f && HeldTwoHanded && !HeldBow)
            {
                float arm = 0f, back = 0f, spin = 0f;
                var reach = Vector3.zero;
                Animate(motion, 0f, alt, _facingLeft, _attackLength, ref arm, ref back, ref spin, ref reach);
                _entryArmSwing = arm;
            }
        }

        /// <summary>
        /// The finisher timing bar's wind-up: travel into this swing's first frame and hold it.
        /// Implemented as the swing itself with its clock stopped (<see cref="_attackHold"/>), so
        /// the pose IS the motion's frame zero - including the alt-swing's far-side blade - and
        /// the entry blend runs exactly as it does for a swing. The strike's own PlayAttack then
        /// starts from this pose, which is its own first frame, so there is nothing to blend.
        ///
        /// Held a little LONGER than asked: the strike's PlayAttack ends the hold, and a hold that
        /// ran out a frame early would let the swing start on its own clock and then restart.
        /// </summary>
        public void HoldSwingStart(AttackMotion motion, bool alt, float seconds)
        {
            PlayAttack(motion, 1f, alt);
            _attackHold = Mathf.Max(0f, seconds) + HoldOvershoot;
        }

        const float HoldOvershoot = 0.1f;

        /// <summary>Seconds the current swing is still frozen on its first frame, and how long it
        /// has been frozen (which the entry blend counts as swing time). See HoldSwingStart.</summary>
        float _attackHold, _holdElapsed;

        /// <summary>Pose captured at the moment the current swing started; see PlayAttack.</summary>
        float _entryArmSwing, _entryBackSwing, _entryBodySpin;
        Vector3 _entryArmReach;

        /// <summary>
        /// Seconds the start of every swing spends travelling from the entry pose onto the
        /// animated one.
        ///
        /// Deliberately SHORTER than the cock window (see ChopCockSeconds), and that gap is the
        /// whole point: the arm whips up into the cocked pose in this time, then HOLDS there for
        /// what remains of the cock. When the two were equal the arm was still arriving as the
        /// strike began, so a swing entered from idle became one continuous smear with no
        /// anticipation - which is why chop 3 still looked like the only right one. It enters
        /// already at the cock angle (the reversed chop before it ends there), so its blend was
        /// a no-op and it got the full hold for free.
        /// </summary>
        const float EntryBlendSeconds = 0.06f;

        void Update()
        {
            // A STATUE does not move: no walk cycle, no breath, no cape, no flipbook. Everything
            // it would have animated waits, and resumes from the same pose when the stone lets go.
            if (_stone) return;

            float dt = Dt;
            float speed = Body != null ? Body.linearVelocity.magnitude : 0f;

            // Advanced before anything reads it, and before FaceAim can change the frame it is
            // measured in - the flip is handled in ApplyWeightShift, which is the only place that
            // knows a flip happened.
            AdvanceWeightShift(dt);
            bool wasFacingLeft = _facingLeft;

            // Read off LAST frame's lift, because this frame's hop is not computed until the pose
            // branches below and the positional writes come before them. One frame of latency on
            // a landing is not a thing anyone can see; reordering the whole of Update around it
            // would be.
            AdvanceLanding(dt);
            AdvanceHitReaction(dt);

            // Breath fades in exactly as the stride fades out, so only one of them is ever
            // driving the torso. Wrapped rather than left to grow: Sin loses precision on a
            // float that has been accumulating for an hour.
            _breath += dt * BreathRate * Mathf.PI * 2f;
            if (_breath > Mathf.PI * 2f) _breath -= Mathf.PI * 2f;
            float breath = Mathf.Sin(_breath) * BreathRise
                         * (1f - Mathf.Clamp01(speed / BreathStillSpeed));

            // The walk: one clock, every part reading its own point on it - see the Gait partial.
            AdvanceGait(dt, speed);
            AdvanceMarch(dt, speed);
            float cycle = _stride / (Mathf.PI * 2f);

            // A puff on the stride's own clock rather than on a timer of its own. Sharing the
            // clock is the entire point: dust raised a few hundredths of a second off the
            // footfall reads as an effect playing NEAR a footstep instead of as one. Cycle 0 is
            // the front leg's footfall and 0.5 the back leg's, so the half the cycle is in
            // changes exactly on each.
            int footfall = Mathf.FloorToInt(cycle * 2f);
            if (footfall != _lastFootfall)
            {
                if (_lastFootfall != int.MinValue && speed >= DustSpeed)
                {
                    // Dropped BEHIND the direction of travel, taken from the velocity rather than
                    // from the facing: the facing is the aim, and a player strafing away from
                    // what they are pointing at would otherwise kick dust out in front of
                    // themselves. Velocity is also the only one of the two that means anything
                    // on a straight vertical run.
                    Vector2 back = Body != null ? -Body.linearVelocity.normalized : Vector2.zero;
                    DustPuff.Raise(transform.position + (Vector3)(back * DustAhead),
                                   Mathf.Clamp01(speed / 6f));
                }
                _lastFootfall = footfall;
            }

            // The body sinks as the planted leg takes the load and rises through the flight
            // between footfalls - the weight of the run lives here. Torso and hips both carry it.
            float body = GaitBody(cycle);
            if (_torso)
            {
                _torso.localPosition = new Vector3(
                    TorsoRest.x,
                    TorsoRest.y + body + breath - _landDepth * LandTorsoShare, 0f);
            }

            // The neck gives part of the breath back so the chest expands INTO it, rather than
            // the whole figure translating up - and takes a further share of a landing on top,
            // the same joint doing the same thing in both directions. See BreathNeckShare. The
            // head also trails the walk's bob a beat late, carried rather than bolted on.
            if (_head)
                _head.localPosition = new Vector3(
                    HeadRest.x,
                    HeadRest.y - breath * BreathNeckShare - _landDepth * LandNeckShare
                    + GaitHeadLag(cycle) * _gaitWeight, 0f);

            // The hips ride the run, and drop further for a plant or a landing. Nothing moves
            // the feet: they stay where the stride puts them and the KNEES take up whatever the
            // hips did (PoseLegs) - which is what makes a dip read as weight rather than as the
            // whole figure sinking into the floor.
            {
                // The legs take the load whichever way the chest is thrown, so the plant reads off
                // the lean's MAGNITUDE - a hard stop and a hard start compress the same way.
                float dip = Mathf.Abs(_leanAngle) * LeanHipDip;

                // A landing bends the knees, so the hips drop with the rest of it.
                dip += _landDepth;

                float hipsY = HipsRest.y + body - dip;
                if (_hips) _hips.localPosition = new Vector3(HipsRest.x, hipsY, 0f);
                PoseLegs(cycle, hipsY);
            }

            // The arms swing against the legs, a beat behind them. Attack overrides both.
            float armWalk = GaitArmTravel(cycle) * WalkArmDegrees * _gaitWeight;
            if (HeldTwoHanded && !HeldBow) armWalk *= Mathf.Lerp(WalkHiltSwingShare, 1f, _gripBlend);
            float idleArmSwing = armWalk;
            float idleBackSwing = -armWalk;

            // Loadout preview: square-on and stock still, both fists hang straight down the
            // centre line and cover the belt. Ease each arm a few degrees toward its own side so
            // the middle of the belt reads, without the pose looking staged. Combat never sets
            // this, and it fades out the moment the character starts moving.
            bool handsFree = ShowcasePose && speed < 0.05f && HandsEmpty
                             && _attackTimer <= 0f && _chargeTimer <= 0f;
            _handsFree = Mathf.Lerp(_handsFree, handsFree ? 1f : 0f,
                                    1f - Mathf.Exp(-PoseRecoverySharpness * dt));
            if (ShowcasePose && speed < 0.05f)
            {
                idleArmSwing -= Mathf.Lerp(16f, ShowcaseShoulderDegrees, _handsFree);
                idleBackSwing += Mathf.Lerp(12f, ShowcaseShoulderDegrees, _handsFree);
            }

            bool posed = false;   // this frame set an exact keyframed pose - don't ease it

            // Cleared before the pose branches, not after them: only a live swing lifts the
            // figure, so anything that interrupts one - a charge starting, the run ending -
            // puts the feet back on the ground rather than stranding it in the air.
            _hopHeight = 0f;
            bool wantWeaponBehind = false;

            if (_chargeTimer > 0f)
            {
                _chargeTimer -= dt;
                // Both arms locked overhead. The tremble grows as the release nears, which is the
                // only tell an enemy-side reader gets that the slam is about to land.
                float t = 1f - Mathf.Clamp01(_chargeTimer / _chargeLength);
                float shake = Mathf.Sin(Time.time * 34f) * (1.5f + t * 4.5f);
                _poseArmSwing = ChargeArmSwing + shake;
                _poseBackSwing = ChargeArmSwing - shake;
                _poseBodySpin = Mathf.Sin(Time.time * 26f) * t * 2.5f;
                _poseArmReach = ReachFor(ChargeFist, ChargeArmSwing + shake);

                float into = Mathf.Clamp01((_chargeLength - _chargeTimer) / EntryBlendSeconds);
                if (into < 1f)
                {
                    float e = 1f - (1f - into) * (1f - into);   // ease out, as a swing's entry
                    _poseArmSwing = Mathf.LerpAngle(_entryArmSwing, _poseArmSwing, e);
                    _poseBackSwing = Mathf.LerpAngle(_entryBackSwing, _poseBackSwing, e);
                    _poseBodySpin = Mathf.Lerp(_entryBodySpin, _poseBodySpin, e);
                    _poseArmReach = Vector3.Lerp(_entryArmReach, _poseArmReach, e);
                }
                posed = true;
            }
            else if (_attackTimer > 0f)
            {
                // A held wind-up stops the swing's clock on frame zero; see HoldSwingStart.
                if (_attackHold > 0f) { _attackHold -= dt; _holdElapsed += dt; }
                else _attackTimer -= dt;
                float k = 1f - Mathf.Clamp01(_attackTimer / _attackLength);
                // The Lunge art's pull-back is ANIMATED over the hold (k -1 to 0), not frozen on
                // its first frame - the draw is the wind-up the timing bar times.
                if (_attackHold > 0f && _motion == AttackMotion.Lunge)
                {
                    float held = _holdElapsed + _attackHold - HoldOvershoot;
                    k = Mathf.Clamp01(_holdElapsed / Mathf.Max(0.05f, held)) - 1f;
                }
                float swingArm = 0f, swingBack = 0f, swingBody = 0f;
                var swingReach = Vector3.zero;
                Animate(_motion, k, _alt, _facingLeft, _attackLength, ref swingArm, ref swingBack, ref swingBody, ref swingReach);

                // A single-edged blade's edge FOLLOWS the motion: whichever way the blade is about
                // to sweep decides it, so a swing that reverses (the Flurry's cut down and cut up)
                // turns the edge over in the still moment between strokes - the wrist turning -
                // rather than leading the second stroke with the spine. A window with no real sweep
                // (a thrust, a hold) keeps what it had.
                if (_singleEdged && _attackHold <= 0f)
                {
                    float ahead = EdgeLookAheadSeconds / Mathf.Max(0.05f, _attackLength);
                    float sign = SweepSignBetween(_motion, _alt, _attackLength, k, Mathf.Min(1f, k + ahead));
                    if (sign != 0f) _edgeTurned = sign < 0f;
                }

                // Blend out of wherever the arm actually was when this swing started, instead of
                // snapping onto the animation's first frame. The window is short and eased, so
                // what the player sees is the arm TRAVELLING into the wind-up - which is the
                // anticipation every swing except chop 3 was missing, because only that one
                // happened to begin where the previous swing ended.
                float elapsed = _attackLength - _attackTimer + _holdElapsed;
                float entry = Mathf.Clamp01(elapsed / EntryBlendSeconds);
                if (entry < 1f)
                {
                    // Ease OUT, not in-out: the arm leaves fast and DECELERATES into the cock,
                    // which is how a real wind-up settles. SmoothStep started slow, so the first
                    // frames of a swing from idle barely moved and the whole travel got shoved
                    // into the moment the strike was due to fire.
                    float e = 1f - (1f - entry) * (1f - entry);
                    swingArm = Mathf.LerpAngle(_entryArmSwing, swingArm, e);
                    swingBack = Mathf.LerpAngle(_entryBackSwing, swingBack, e);
                    swingBody = Mathf.Lerp(_entryBodySpin, swingBody, e);
                    swingReach = Vector3.Lerp(_entryArmReach, swingReach, e);
                }

                // A held wind-up trembles, harder as the strike nears - the charge's own tell at a
                // fraction of its size, so a still first frame reads as "about to strike" rather
                // than as standing around. A few degrees, so the hand-off to the swing stays clean.
                if (_attackHold > 0f)
                {
                    float held = _holdElapsed + _attackHold - HoldOvershoot;
                    float t = Mathf.Clamp01(_holdElapsed / Mathf.Max(0.05f, held));
                    float shake = Mathf.Sin(Time.time * 34f) * (0.8f + t * 2.6f);
                    swingArm += shake;
                    swingBack -= shake;
                }

                _poseArmSwing = swingArm;
                _poseBackSwing = swingBack;
                _poseBodySpin = swingBody;
                _poseArmReach = swingReach;
                _hopHeight = HopAt(k, _attackLength);

                // The alt-swing starts with the blade on the far side of the body and CROSSES to
                // the near side as it comes through. Swapping the depth at the crossover is what
                // a 2D animator would draw; holding it behind for the whole swing would put the
                // blade behind the torso while it is visibly out in front by the end of the arc.
                //
                // Set ChopCrossToFront above 1 to keep it behind for the entire swing instead.
                wantWeaponBehind = _alt && _motion == AttackMotion.Chop &&
                                   ChopProgress(k, _attackLength) < ChopCrossToFront;
                posed = true;
            }

            // The rest carry lays the blade across the character's BACK, so it passes behind the
            // head rather than across the face - see ShoulderCarryOrder. Resolved here, alongside the alt-swing's own crossover,
            // because both are answering the same question and only one of them can win.
            //
            // Flipped at the halfway point of the grip blend rather than at either end: the
            // restack is instantaneous, so it wants to land while the blade is travelling fastest
            // and has the most screen distance to hide the swap in. At the ends it would pop
            // against a stationary sword.
            //
            // A live swing always wins - the arc is drawn in front, and a blade that stayed behind
            // the head through a chop would disappear into the body at exactly the moment the
            // player is reading where it lands.
            bool shoulderedBehind = _gripBlend > 0.5f && _attackTimer <= 0f && _chargeTimer <= 0f;

            // Applied once, after the branches: only a live alt-swing puts the blade behind, so
            // idling, walking and charging all fall through to the front. Resolved here rather
            // than inside a branch so an interrupted swing cannot leave the sword stuck behind
            // the body - the same trap the hop had.
            SetWeaponBehind(wantWeaponBehind);
            SetShoulderCarry(shoulderedBehind && !wantWeaponBehind);

            // Between two swings of a chain the follow-through HOLDS (see CarryReturnDelay):
            // relaxing toward the walk pose in the gap swung the blade most of the way to upright
            // and the next swing dragged it straight back down - a wobble between every basic.
            if (!posed && _sinceSwing < CarryReturnDelay && HeldTwoHanded) posed = true;

            if (!posed)
            {
                // Relax toward the walk cycle instead of snapping to it. LerpAngle on the swing
                // values, plain Lerp on the reach - a positional offset has no wraparound to get
                // wrong the way a Z-angle does.
                float ease = 1f - Mathf.Exp(-PoseRecoverySharpness * dt);
                _poseArmSwing = Mathf.LerpAngle(_poseArmSwing, idleArmSwing, ease);
                _poseBackSwing = Mathf.LerpAngle(_poseBackSwing, idleBackSwing, ease);
                _poseBodySpin = Mathf.Lerp(_poseBodySpin, 0f, ease);
                _poseArmReach = Vector3.Lerp(_poseArmReach, Vector3.zero, ease);
            }

            // Grounded motions ignore the aim entirely; everything else rides on it as before.
            float suppressTarget = _attackTimer > 0f && IgnoresAim(_motion) ? 1f : 0f;
            _aimSuppress = Mathf.Lerp(_aimSuppress, suppressTarget,
                                      1f - Mathf.Exp(-PoseRecoverySharpness * dt));
            float aim = _aimResidual * (1f - _aimSuppress);

            float armSwing = _poseArmSwing;
            float backSwing = _poseBackSwing;
            float bodySpin = _poseBodySpin;
            var armReach = _poseArmReach;

            // FaceAim first: the arms need this frame's residual, not last frame's, and the cape
            // needs this frame's mirror to know which way "behind" is.
            FaceAim();
            ApplyWeightShift(wasFacingLeft);
            AnimateCape(dt, speed);
            AnimateScarf(dt, speed);
            AnimateVials(dt);
            AnimateLegCloth(dt, speed);

            // The rest carry is only ever a TWO-HANDED weapon's idle: a one-handed weapon already
            // hangs from one hand, and the bow's two hands do genuinely different jobs. A live
            // swing or charge overrides it outright rather than blending against it - the arc has
            // to hit its real angles (see the pose fields' own note), so the grip must already be
            // closed by the time the blade moves.
            bool swinging = _attackTimer > 0f || _chargeTimer > 0f;
            _sinceSwing = swinging ? 0f : _sinceSwing + dt;
            bool wantShouldered = _gripShouldered && HeldTwoHanded && !HeldBow
                                  && !swinging && _sinceSwing >= CarryReturnDelay && _present <= 0f;
            float gripSharpness = _attackTimer > 0f || _chargeTimer > 0f
                ? GripToSwingSharpness : GripBlendSharpness;
            _gripBlend = Mathf.Lerp(_gripBlend, wantShouldered ? 1f : 0f,
                                    1f - Mathf.Exp(-gripSharpness * dt));

            // Re-decided only while the carry is fully held, where both readings are the same
            // angle - see CarryElbowHeld.
            if (_gripBlend > 0.999f) _carryElbowUnwound = CarryElbowNow > 180f;


            // Which rig arm carries - see CarryInBackArm (it follows the mirror, so a turn flips
            // the whole picture rather than just the head).
            //
            // Re-derived every frame against the CURRENT parent rather than latched: that
            // self-heals after an Apply (which re-parents by _bowGrip and knows nothing about the
            // carry) and after a domain reload reattaches the layer to its PivotFor default. A
            // reference compare costs nothing; a latched flag that disagreed with the scene would
            // hang the sword off an arm it is not attached to.
            var fromDict = WeaponAnchor;
            if (fromDict != null) _weaponPivot = fromDict;

            bool carryBack = CarryInBackArm;
            if (carryBack != _carryInBack)
            {
                _carryInBack = carryBack;
                if (_shoulderCarry) ApplyLayerOrder();   // facing away promotes the carrying fist
            }

            bool carrying = HeldTwoHanded && !HeldBow;
            bool carryHandOpen = _gripBlend > 0.5f && carrying;
            if (_weaponPivot != null && !_bowGrip)
            {
                var wantParent = carryHandOpen && carryBack ? _elbowBack : _elbowFront;
                if (wantParent != null && _weaponPivot.parent != wantParent)
                    _weaponPivot.SetParent(wantParent, false);
            }

            // +1 when the carrying arm is arm.back, -1 when it is arm.front. Every carry angle is
            // written for arm.back and flipped by this, because the two arms are mirror images in
            // the rig's own frame: that flip is what keeps the pose the SAME PICTURE on screen when
            // the carry changes arms on a turn.
            float side = carryBack ? 1f : -1f;
            float carryBlend = carrying ? _gripBlend : 0f;

            // The walk carries the arms a little bent, swinging loosely - on whichever arms are free.
            float armTravel = GaitArmTravel(cycle);
            GaitArmShares(carrying, carryBack, out float walkShareFront, out float walkShareBack);

            // ---- the swing's arms REACH the grip; the shoulders stay on the body ----
            //
            // Every motion is authored as a straight arm turned about a shoulder that armReach
            // SLIDES: that is what carries the grip through the arc. But the arm is 0.17 to the
            // grip and the slides run to 0.38, so the joint left the body - a Chop's cock put the
            // shoulder in the middle of the face (0.17 up), hidden only while the head drew over
            // the arms. Now the same grip position and blade angle are kept EXACTLY, and the arm
            // reaches them: the shoulder stays put (a small shove allowed), the elbow bends (IK,
            // like the legs' SolveLeg) and the wrist turns the weapon back onto the authored blade
            // angle. Every arc, hand-off and lead-in still lands where it was tuned to; only the
            // arms stop tearing off. What even a full arm cannot reach moves the whole figure
            // (_reachShift, drawing-only like the hop) - a lunge is the body going with the blade.
            float swingFrontA = armSwing + aim, swingFrontE = 0f, swingWrist = 0f;
            Vector2 frontShove = armReach;
            _reachShift = Vector2.zero;
            if (_armFront && _elbowFront && !HeldBow)
            {
                Vector2 upper = _elbowFront.localPosition;
                Vector2 grip = FrontGripPoint;
                Vector2 target = (Vector2)armReach + Rotate(upper + grip, swingFrontA);   // from rest
                SolveArmReach(upper, grip, target, ArmBendSign, out swingFrontA, out swingFrontE,
                              out frontShove, out _reachShift);
                swingWrist = Mathf.DeltaAngle(swingFrontA + swingFrontE, armSwing + aim);
            }

            if (_armFront)
            {
                float angle, elbow = 0f;
                if (carrying && !carryBack)
                {
                    // The carrying arm ignores aim: the pose is a fixed picture, and aim riding on
                    // it would lift and drop the blade every time the character looks round.
                    angle = Mathf.LerpAngle(swingFrontA, side * CarryShoulderNow, _gripBlend);
                    elbow = Mathf.Lerp(swingFrontE, side * CarryElbowHeld, _gripBlend);
                }
                else
                {
                    // The free arm hangs once the carry is open. Negative splay because this arm
                    // sits on -X: the sign that swings the hand away from the belly is mirrored
                    // between the two shoulders - see ShoulderOffArmDegrees.
                    float hang = armSwing - ShoulderOffArmDegrees;
                    float hangAim = aim * Mathf.Lerp(1f, ShoulderAimFraction, carryBlend);
                    angle = Mathf.LerpAngle(swingFrontA,
                                            Mathf.LerpAngle(armSwing, hang, carryBlend) + hangAim, carryBlend);
                    elbow = swingFrontE * (1f - carryBlend);
                }
                _armFront.localRotation = Quaternion.Euler(0, 0, angle);

                // The shove is a SWING's; a resting arm has no business carrying it.
                _armFront.localPosition = _armFrontRest + (Vector3)frontShove * (1f - carryBlend);
                elbow += GaitElbowDegrees(armTravel) * walkShareFront;
                elbow += ShowcaseElbowDegrees * _handsFree;   // forearm folds back in toward the hip
                if (_elbowFront) _elbowFront.localRotation = Quaternion.Euler(0, 0, elbow);
            }

            // The weapon's own pivot - the only writes to it anywhere in the rig. At blend 0 this
            // is exactly the grip Apply placed it at, so nothing changes for any weapon or pose
            // that never asks for the carry. Carried, the grip moves into the FIST: the handle
            // crosses the forearm there instead of running along it, the way it does in the hand
            // of anyone holding a sword over their shoulder.
            if (_weaponPivot != null)
            {
                // The BLADE turns the short way from the swing's angle to the carry's, and the wrist
                // is whatever puts it there on top of the arm as drawn. Blending the wrist on its
                // own let the blade take the long way round between two poses far apart - on the
                // alt chop's first frame it pointed up where both ends had it pointing down.
                float blade = Mathf.LerpAngle(armSwing + aim,
                    side * (CarryShoulderNow + CarryElbowHeld - CarryWeaponNow), carryBlend);
                float wrist = carryBlend > 0f && !carryBack && _armFront && _elbowFront && !_bowGrip
                              && _weaponPivot.parent == _elbowFront
                    ? Mathf.DeltaAngle(_armFront.localEulerAngles.z + _elbowFront.localEulerAngles.z, blade)
                    : Mathf.LerpAngle(swingWrist, -side * CarryWeaponNow, carryBlend);
                _weaponPivot.localRotation = Quaternion.Euler(0, 0, wrist);
                // Rolled only while the carrying fist holds it: the combat grip takes the blade
                // back at the same moment the weapon changes arms (carryHandOpen).
                // A single-edged blade turned so its edge leads the swing (_edgeTurned) - in the
                // combat grip only, the same handover point as the roll.
                bool rolled = carryBlend > 0.5f ? _marchRolled : _singleEdged && _edgeTurned;
                _weaponPivot.localScale = new Vector3(rolled ? -1f : 1f, 1f, 1f);
                _weaponPivot.localPosition = Vector3.Lerp(_weaponGripRest,
                    new Vector3(side * CarryGrip.x, CarryGrip.y, _weaponGripRest.z), carryBlend);
            }

            if (_armBack)
            {
                float elbow = 0f;
                if (HeldBow)
                {
                    // A bow is the one two-handed grip where the two hands genuinely do
                    // different things - this arm (which now holds the bow, see Apply's
                    // re-parenting) stays on its OWN backSwing rather than copying the front
                    // arm's armSwing/armReach, which is the reaching-and-drawing hand's motion.
                    // Copying it here is exactly what made the whole weapon read as flung
                    // through space together, like a throw, instead of one hand holding steady
                    // while the other draws.
                    _armBack.localRotation = Quaternion.Euler(0, 0, backSwing + aim * 0.4f);
                    _armBack.localPosition = _armBackRest;
                    _offSplay = 0f;                    // no disc in this hand; its plate reads this
                }
                else if (HeldTwoHanded)
                {
                    _offSplay = 0f;
                    // Both fists are on the same hilt, so the off arm cannot have an opinion of
                    // its own: it copies the main arm's angle and its reach, trailing slightly
                    // round the grip. This also, correctly, kills the walk's arm swing while a
                    // greatsword is carried - you do not swing your arms holding one in both
                    // hands.
                    //
                    // Unless the grip is open, in which case this arm either CARRIES (it is the
                    // right arm this way round) or hangs free. Note this is a POSITION blend as
                    // much as an angle one: ArmBackGripRest relocates the shoulder past the body's
                    // centre line, because the arm is 8 long and the shoulders are 12 apart (see
                    // its own note), so letting go is the joint travelling back rather than the
                    // hand rotating off.
                    float gripAngle = armSwing + GripTrail + aim;
                    float rest = carryBack ? side * CarryShoulderNow
                                           : backSwing + ShoulderOffArmDegrees + aim * ShoulderAimFraction;

                    if (_elbowBack && !carryBack)
                    {
                        // The off fist reaches where the old straight arm put it, from a shoulder
                        // that stays on the body - see "the swing's arms REACH the grip" above -
                        // less the figure's own shift, which carries this shoulder along too.
                        //
                        // Letting go of the hilt, it is the HAND that travels, from the hilt to
                        // where the hanging arm holds it, and the arm reaches it every frame.
                        // Blending the two arm ANGLES instead (the old way) pointed the arm,
                        // halfway between up on the hilt and down by the hip, out sideways at
                        // nothing for the frames a swing takes to close the grip.
                        Vector2 upper = _elbowBack.localPosition;
                        Vector2 reachTo = upper + CarryGrip;
                        Vector2 onHilt = ArmBackGripRest + (Vector2)armReach
                                         + Rotate(reachTo, gripAngle) - _reachShift;
                        Vector2 hanging = (Vector2)_armBackRest + Rotate(reachTo, rest);
                        // Squared, so the hand is on the hilt within a couple of frames of a swing
                        // starting - on the way it crosses in front of the face.
                        float letGo = _gripBlend * _gripBlend;
                        Vector2 shoulderAt = Vector2.Lerp(ArmBackGripRest, _armBackRest, letGo);
                        SolveArmReach(upper, CarryGrip, Vector2.Lerp(onHilt, hanging, letGo) - shoulderAt,
                                      ArmBendSign, out float reachAngle, out elbow, out var shove, out _);
                        _armBack.localRotation = Quaternion.Euler(0, 0, reachAngle);
                        _armBack.localPosition = shoulderAt + shove * (1f - letGo);
                    }
                    else
                    {
                        // Turned away the far arm CARRIES, and the carry's bent elbow is a pose
                        // of its own - reached for, it could come back as the mirrored solution.
                        if (carryBack) elbow = side * CarryElbowHeld * _gripBlend;
                        _armBack.localRotation = Quaternion.Euler(
                            0, 0, Mathf.LerpAngle(gripAngle, rest, _gripBlend));
                        _armBack.localPosition = Vector3.Lerp(
                            (Vector3)ArmBackGripRest + armReach, _armBackRest, _gripBlend);
                    }
                }
                else
                {
                    // Holding the pair's second disc, this arm swings OUT so the fist carries it
                    // clear of the torso - the disc sits in the hand now (SetWeaponSplit), and a
                    // hand hanging at the body's edge put its disc straight behind the body.
                    // Eased on UNSCALED time: the character screen pauses the game, and a swing
                    // eased on game time would never arrive there.
                    _offSplay = Mathf.MoveTowards(_offSplay, _split ? SplitOffArmDegrees : 0f,
                                                  SplitOffArmDegreesPerSecond * Time.unscaledDeltaTime);
                    _armBack.localRotation = Quaternion.Euler(0, 0, backSwing + aim * 0.4f + _offSplay);
                    _armBack.localPosition = _armBackRest;

                    // ...and the disc turned back by the same amount, so it stands upright like
                    // the near one rather than tilting with the arm.
                    if (_split && _offhand != null
                        && _layers.TryGetValue(RigLayer.Weapon, out var mainWeapon) && mainWeapon != null)
                        _offhand.transform.localRotation =
                            Quaternion.Euler(0, 0, -_offSplay) * mainWeapon.transform.localRotation;
                }
                elbow += GaitElbowDegrees(-armTravel) * walkShareBack;
                elbow -= ShowcaseElbowDegrees * _handsFree;   // mirror of the front arm's fold
                if (_elbowBack) _elbowBack.localRotation = Quaternion.Euler(0, 0, elbow);
            }

            ApplyPresent();

            FollowPauldron(RigLayer.Shoulders, _shouldersRest, carryBack, side, carryBlend);
            FollowPauldron(RigLayer.ShouldersBack, _shouldersBackRest, carryBack, side, carryBlend);
            SyncDrapeSide();

            // In top-down the root carries the facing; in 3/4 it only carries a spin finisher.
            float rootZ = Mathf.Abs(bodySpin) > 0.01f
                ? (TopDown ? _facingYaw + bodySpin : bodySpin)
                : (TopDown ? _facingYaw : 0f);
            transform.localRotation = Quaternion.Euler(0, 0, rootZ);

            // The rig root sits at the player's origin, so these offsets are the only thing
            // ever written here - no rest position to remember. All three are drawing-only and
            // stack: _airHeight is the leap's gameplay state, _hopHeight the swing's cosmetic
            // weight shift, _phantomOffset Phantom's poof. None of them move the player's own
            // transform, so the hitbox, the camera and the shadow all stay where the character
            // actually is.
            bool raised = ArmsAboveNeck();
            SetArmsLifted(raised || HandsAboveShoulders(), raised);

            var hit = _hitTimer > 0f ? _hitPush * (_hitTimer / HitReactionSeconds) : Vector2.zero;
            transform.localPosition = new Vector3(
                _phantomOffset.x + hit.x,
                _airHeight + _hopHeight + _phantomOffset.y + hit.y, 0f) * _visualScale;

            // What the arms could not reach, the figure travels (see SolveArmReach): along the
            // ground only, forward in the facing - mirrored with the root, never turned by a
            // spinning finisher's own rotation.
            if (Mathf.Abs(_reachShift.x) > 1e-5f && carryBlend < 1f)
                transform.localPosition += new Vector3(
                    Mathf.Sign(transform.localScale.x) * _reachShift.x * (1f - carryBlend) * _visualScale,
                    0f, 0f);

            if (_shadow != null)
            {
                // Tightening as the body rises off the ground is the cue that reads as weight.
                var st = _shadow.transform;
                float lift = (1f + GaitBob(cycle) / WalkBob) * _gaitWeight;
                Vector2 patch = TopDown
                    ? new Vector2(0.52f - lift * 0.04f, 0.44f - lift * 0.035f)
                    : new Vector2(0.46f - lift * 0.05f, 0.17f - lift * 0.02f);
                st.localScale = new Vector3(patch.x * _visualScale, patch.y * _visualScale, 1f);
                st.localRotation = Quaternion.identity;
                var sc = _shadow.color;
                sc.a = 0.32f - lift * 0.07f;

                // Airborne, the shadow is all that is left on screen - the figure itself is above
                // the camera. It tightens and darkens rather than fading out, because it is the
                // only thing telling the player and the enemies where the slam is going to land.
                if (_airHeight > 0.001f)
                {
                    float air = Mathf.Clamp01(_airHeight / AirborneShadowFull);
                    float tight = Mathf.Lerp(1f, 0.42f, air);
                    st.localScale = new Vector3(st.localScale.x * tight, st.localScale.y * tight, 1f);
                    sc.a = Mathf.Lerp(sc.a, 0.55f, air);
                }
                _shadow.color = sc;

                // Hidden, except in the air: there the shadow is where the slam will land, and a
                // character who casts none still owes the player that.
                _shadow.enabled = !_shadowHidden || _airHeight > 0.001f;
            }
        }

        /// <summary>
        /// One shape per <see cref="AttackMotion"/>, evaluated on k = 0..1 through the swing.
        /// The point is that the shapes are DISTINGUISHABLE at a glance - a player should read
        /// which finisher fired from the body, not from the icon in the corner.
        /// </summary>
        // ---- Chop's three-phase swing (prototype for the rest of the motions) ----
        //
        // One SmoothStep across a 175-degree arc read as a windshield wiper: the sword rotated
        // about a fixed fist, half the duration was spent barely moving near the wind-up, and the
        // strike crossed the whole readable arc between two frames. The fix is structural, not a
        // tuning pass, and its three parts only work together: phase timing makes the strike the
        // legible part, the grip ORBIT turns rotation into a swing (the hands carry the sword
        // through space instead of spinning it in place), and the body english gives the strike
        // a beat to land on.

        /// <summary>
        /// Seconds the cock aims to occupy. ABSOLUTE time, not a fraction: phases as fixed
        /// fractions made a fast basic's strike 4 frames long - a blink between two static holds
        /// - while the slower finisher read perfectly. The eye needs the same real time to
        /// register a phase regardless of how long the whole swing lasts.
        /// </summary>
        const float ChopCockSeconds = 0.13f;

        /// <summary>Seconds the strike aims to occupy - the entire arc crosses inside this.</summary>
        const float ChopStrikeSeconds = 0.12f;

        /// <summary>How far past the start the cock pulls, as a fraction of the full arc.</summary>
        const float ChopCockAmount = 0.07f;

        /// <summary>How far past the end the strike overshoots before settling back.</summary>
        const float ChopOvershoot = 0.09f;

        /// <summary>World units the grip travels forward through the strike - the orbit radius.</summary>
        const float ChopOrbit = 0.34f;

        // Where the chop's arc begins and ends, in arm-pivot degrees.
        //
        // These were the other way round until it was watched frame by frame: the canonical
        // direction swept the blade UP and away from the target, so an "Overhand" was anything
        // but. Fixed HERE rather than by tagging each step with ReverseArc, because the direction
        // was wrong for the MOTION - every move built on Chop (the basic swings, Sunder's
        // Smash/Heave/Crush, Falling Star's Ascend, Wanderblade's Wind) had the same problem.
        // ReverseArc stays for genuine per-step exceptions, and the chain's alternation still
        // flips consecutive basics either side of this.

        /// <summary>Blade cocked back over the shoulder.</summary>
        const float ChopFrom = 75f;

        /// <summary>Driven down and through, past the hip.</summary>
        const float ChopTo = -100f;

        // ---- the ALT swing: the same downward chop, staged from the other shoulder ----
        //
        // What separates it from the default swing is DEPTH, not pose: the blade is drawn behind
        // the body, so it reads as being on the far side of the character. See WeaponBehindOrder.
        // Moving the blade in SPACE was tried and abandoned - the offsets big enough to be
        // legible tore the fist off the shoulder, because they move the arm joint and the arm is
        // only ~0.21 units long.

        /// <summary>
        /// Extra degrees the alt chop cocks through, so the far-shoulder wind-up sits a little
        /// higher and further round than the near-shoulder one. Deliberately small: the layer
        /// swap carries the read, and this only keeps the two poses from being identical.
        /// </summary>
        const float ChopAltCockBias = 26f;

        /// <summary>
        /// World units the alt chop carries the grip across the body at full cock, easing back
        /// out as the strike lands. Kept well under the arm's own length so the shoulder holds.
        /// </summary>
        const float ChopAltBehind = 0.18f;

        /// <summary>
        /// Point in the alt chop's arc where the blade crosses from the far side of the body to
        /// the near one, and the weapon layer comes back to the front. Just past the middle of
        /// the strike, which is where the blade visibly passes the character.
        ///
        /// Above 1 keeps the weapon behind for the whole swing.
        /// </summary>
        const float ChopCrossToFront = 0.55f;

        // ---- the RISE swing: upward cut from lead hip to rear shoulder, winding the body ----
        //
        // Hit 2 of the basic combo. Starts low where Chop concludes (-100°), whips upward across
        // the target to high-shoulder (+85°), and leaves the torso coiled (+18°) with arms elevated.
        // This coiled end-pose is the launchpad for all finishers: downward slams drop into it,
        // spins release its torque, thrusts drive forward from it, and leaps carry its upward lift.

        /// <summary>Blade starting angle at low lead hip.</summary>
        const float RiseFrom = -100f;

        /// <summary>Blade ending angle cocked high over the rear shoulder.</summary>
        const float RiseTo = 85f;

        /// <summary>
        /// Where the grip starts, world units forward and up - exactly where a Chop's arc ends
        /// (its ChopOrbit at the end of the strike), so the cut begins in the hands the Chop left.
        /// It was (0, -0.06): only the BLADE angle matched the Chop's end, the grip did not - the
        /// fist jumped from in front of the hip to behind the shoulder in the entry blend while
        /// the blade kept pointing forward, and the sword slid back through the torso for three
        /// frames at the start of every Rise. Hidden while the arm was a straight stick; obvious
        /// once the arm reaches its grip (SolveArmReach).
        /// </summary>
        const float RiseReachFrom = 0.32f;
        const float RiseLiftFrom = -0.12f;

        /// <summary>Seconds spent dipping into the knees before the upward strike.</summary>
        const float RiseCockSeconds = 0.08f;

        /// <summary>Seconds the upward arc occupies to snap from hip to shoulder.</summary>
        const float RiseStrikeSeconds = 0.11f;

        /// <summary>How far the blade dips past the start during the wind-up (fraction of arc).</summary>
        const float RiseCockAmount = 0.06f;

        /// <summary>How far past the high guard the blade overshoots before settling.</summary>
        const float RiseOvershoot = 0.08f;

        // ---- the WIND swing: a low drawing cut that coils the body for a release ----
        //
        // The lead-in for the finishers that start LOW - Spin, WhirlThrow, Throw all begin with
        // the blade where a Chop ends. Starts there too, so it needs no entry travel; drags the
        // blade back and down through the target while the hands pull to the rear hip and the
        // torso turns AWAY. Ends wound with the blade still low, so the finisher's own motion is
        // the coil letting go rather than a second swing starting from somewhere else.

        /// <summary>Blade angle at the start - exactly where a Chop finishes.</summary>
        const float WindFrom = -100f;

        /// <summary>Blade dropped lower as the hands draw it back past the hip.</summary>
        const float WindTo = -132f;

        /// <summary>Seconds spent lifting the blade a touch before it is drawn through.</summary>
        const float WindCockSeconds = 0.07f;

        /// <summary>Seconds the draw itself occupies.</summary>
        const float WindStrikeSeconds = 0.12f;

        /// <summary>Degrees the blade lifts during the load, before it drops through the cut.</summary>
        const float WindLift = 12f;

        /// <summary>World units the grip starts forward of the body - matches a Chop's end.</summary>
        const float WindReachFrom = 0.30f;

        /// <summary>
        /// World units the grip ends BEHIND the body, drawn back to the rear hip - where Throw,
        /// Whirlwind and Spin take it (their grips start at the rear hip). Was 0.16, which ended
        /// the draw 0.12 past them and slid the blade forward again through each hand-off.
        /// </summary>
        const float WindDrawBack = 0.04f;

        /// <summary>The grip's height at the end of the draw, with <see cref="WindDrawBack"/>.</summary>
        const float WindLiftTo = -0.076f;

        /// <summary>
        /// Degrees the torso turns away by the end - the coil. Positive, the same way Rise winds,
        /// because every finisher that unwinds from it (Spin's -360) turns the other way.
        /// </summary>
        const float WindCoil = 22f;

        /// <summary>Degrees the torso leans INTO the cut during the load, before it turns away.</summary>
        const float WindLoadLean = 5f;

        /// <summary>How far past the coil the draw overshoots before settling, as a fraction.</summary>
        const float WindOvershoot = 0.12f;

        // ---- the IMPALE finisher: horizontal chamber to shoulder, explosive forward lunge ----
        //
        // Holds the blade horizontally at chest level and draws the fists back past the near hip,
        // coiling the body back (-16°), before explosively thrusting forward in a full extension
        // lunge - the figure travelling with it.

        /// <summary>
        /// Dead level horizontal sword angle for the forward thrust: -90, the blade pointing along
        /// the facing. It was -6, written as if 0 were horizontal - in this rig 0 is tip UP, so
        /// the "level skewer" was a raised vertical blade, and Thrust and Jab had the same mistake
        /// (the sword held upright while the fist punched). See ThrustGuardFist.
        /// </summary>
        const float ImpaleAngle = -90f;

        // ---- the thrust family: Thrust, Jab, Impale, posed as FIST and BLADE ----
        //
        // Every thrust starts and ends in the same GUARD - fists low in front, where a Chop leaves
        // them, blade level and a touch down - so a Chop leads into a Thrust, a Thrust into an
        // Impale, and an Impale back into the opener with nothing to slide across.

        /// <summary>The guard every thrust starts and ends in: fists low in front (relative to the
        /// near shoulder at rest), where a Chop's arc leaves them.</summary>
        static readonly Vector2 ThrustGuardFist = new Vector2(0.12f, -0.07f);

        /// <summary>The blade in the guard: forward, a few degrees below level.</summary>
        const float ThrustGuardBlade = -97f;

        /// <summary>Where a Thrust drives the fists: out along the facing, rising to chest height.
        /// Past a straight arm's reach - the shoulder and the figure go with it.</summary>
        static readonly Vector2 ThrustExtendFist = new Vector2(0.36f, -0.03f);

        /// <summary>Where a Jab pokes the fists: a shorter Thrust.</summary>
        static readonly Vector2 JabExtendFist = new Vector2(0.26f, -0.04f);

        /// <summary>Impale's chamber: the fists drawn back past the near hip at chest height, the
        /// blade level along the body, coiled to drive.</summary>
        static readonly Vector2 ImpaleChamberFist = new Vector2(-0.07f, -0.02f);

        /// <summary>Impale at full extension - the deep lunge, the figure travelling with it.</summary>
        static readonly Vector2 ImpaleLungeFist = new Vector2(0.42f, 0f);

        /// <summary>Share of Impale's follow-through spent HOLDING the skewer before the fists
        /// come back to the guard, so whatever follows starts where it expects the hands.</summary>
        const float ImpaleHoldShare = 0.45f;

        /// <summary>Seconds spent drawing the blade back to the shoulder and aiming.</summary>
        const float ImpaleDrawSeconds = 0.26f;

        /// <summary>Seconds the violent forward thrust occupies to reach full extension.</summary>
        const float ImpaleLungeSeconds = 0.12f;


        // ---- the FLURRY art: cut down, cut up, the pommel ----
        //
        // Starts where a Rise leaves the hands (blade cocked high over the shoulder, fists high in
        // front), ends where a Chop starts (the opener's cock), so the chain runs into it and
        // straight back out of it. Each cut is timed against the moment Tuning.Flurry says it lands,
        // placed so the blade is crossing in front of the body as the hit resolves. The pommel turns the
        // blade back over the shoulder - the pommel then points AT the target - and drives the
        // fists out past a straight arm's reach (the figure goes with them, as a Thrust's does).

        /// <summary>Where a Rise leaves the grip, and the Flurry starts it.</summary>
        static readonly Vector2 FlurryHighFist = new Vector2(0.172f, 0.145f);
        const float FlurryHighBlade = 85f;

        /// <summary>The first cut's end: forward and down, the hands low in front (a Chop's end).</summary>
        static readonly Vector2 FlurryLowFist = new Vector2(0.145f, -0.08f);
        const float FlurryLowBlade = -118f;

        /// <summary>The rising cut's end: blade back up over the shoulder, hands high.</summary>
        static readonly Vector2 FlurryRiseFist = new Vector2(0.15f, 0.13f);
        const float FlurryRiseBlade = 80f;

        // The two pommel blade angles are the ARM's; what is drawn is arm + body, and the body is
        // coiled +22 at the chamber and leaning -18 into the blow. Written as the drawn angle it
        // landed tipped 20 degrees UP behind the head - the pommel pointed at the ground and the
        // blow read as one more cock over the shoulder.

        /// <summary>The pommel's chamber: fists drawn back level with the face, the blade laid
        /// back along the forearms (drawn ~105, a touch below level).</summary>
        static readonly Vector2 FlurryChamberFist = new Vector2(-0.04f, 0.10f);
        const float FlurryChamberBlade = 83f;

        /// <summary>The pommel landing: fists driven out at head height, past a straight arm (as
        /// Impale's lunge), the blade level behind them (drawn ~94) so the pommel leads.</summary>
        static readonly Vector2 FlurryPommelFist = new Vector2(0.42f, 0.06f);
        const float FlurryPommelBlade = 112f;

        /// <summary>The end: the opener's cock (a Chop's first frame), where the next chain starts.</summary>
        static readonly Vector2 FlurryEndFist = new Vector2(0.167f, 0.074f);
        const float FlurryEndBlade = ChopFrom;

        /// <summary>Seconds the first cut runs on past its hit. Its arc starts on the first frame,
        /// so the hit (CutOneAt) falls ~60% through it - the blade some 50 degrees down from
        /// upright, out in front, as the hit resolves. Centred, it hit with the blade upright.</summary>
        const float FlurryCutOneAfter = 0.045f;

        /// <summary>Seconds the rising cut starts before its hit, and runs on after it: the hit
        /// falls 40% through, the blade again ~50 degrees from upright, rising.</summary>
        const float FlurryCutTwoBefore = 0.048f;
        const float FlurryCutTwoAfter = 0.072f;

        /// <summary>World units the fists bow forward through the middle of each cut (an orbit,
        /// not a blade turning about a fixed point - see ChopOrbit).</summary>
        const float FlurryCutBow = 0.10f;

        /// <summary>Seconds the pommel holds at full extension before the recovery.</summary>
        const float FlurryPommelHold = 0.06f;

        /// <summary>
        /// The Flurry's pose at <paramref name="t"/> seconds into the sequence. Phases, in order:
        /// cut one (high to low, CutOneAt ~60% through it), cut two (low to high, CutTwoAt 40% through),
        /// the chamber, the drive (accelerating INTO the pommel's hit at PommelAt), a hold, and
        /// the recovery to the opener's cock.
        /// </summary>
        static void FlurryPose(float t, out Vector2 fist, out float blade, out float body, out float back)
        {
            var T = Core.Tuning.Flurry.Seconds;
            float cut1End = Core.Tuning.Flurry.CutOneAt + FlurryCutOneAfter;
            float cut2Start = Core.Tuning.Flurry.CutTwoAt - FlurryCutTwoBefore;
            float cut2End = Core.Tuning.Flurry.CutTwoAt + FlurryCutTwoAfter;
            float hit = Core.Tuning.Flurry.PommelAt;
            float drive = hit - 0.07f;
            float holdEnd = hit + FlurryPommelHold;

            float Smooth(float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

            if (t < cut1End)
            {
                float u = Smooth(0f, cut1End);
                blade = Mathf.Lerp(FlurryHighBlade, FlurryLowBlade, u);
                fist = Vector2.Lerp(FlurryHighFist, FlurryLowFist, u)
                     + new Vector2(Mathf.Sin(u * Mathf.PI) * FlurryCutBow, 0f);
                body = Mathf.Lerp(18f, -12f, u);
                back = Mathf.Lerp(-20f, 25f, u);
            }
            else if (t < cut2Start)
            {
                blade = FlurryLowBlade;
                fist = FlurryLowFist;
                body = -12f;
                back = 25f;
            }
            else if (t < cut2End)
            {
                float u = Smooth(cut2Start, cut2End);
                blade = Mathf.Lerp(FlurryLowBlade, FlurryRiseBlade, u);
                fist = Vector2.Lerp(FlurryLowFist, FlurryRiseFist, u)
                     + new Vector2(Mathf.Sin(u * Mathf.PI) * FlurryCutBow, 0f);
                body = Mathf.Lerp(-12f, 14f, u);
                back = Mathf.Lerp(25f, -30f, u);
            }
            else if (t < drive)
            {
                // The chamber: the wrist turns the blade back over the shoulder, the fists come
                // back to the face, the shoulders coil away.
                float u = Smooth(cut2End, drive);
                blade = Mathf.Lerp(FlurryRiseBlade, FlurryChamberBlade, u);
                fist = Vector2.Lerp(FlurryRiseFist, FlurryChamberFist, u);
                body = Mathf.Lerp(14f, 22f, u);
                back = Mathf.Lerp(-30f, -36f, u);
            }
            else if (t < hit)
            {
                // The drive ACCELERATES into the hit - a blow arrives fast, it does not ease in.
                float u = Mathf.InverseLerp(drive, hit, t);
                u *= u;
                blade = Mathf.Lerp(FlurryChamberBlade, FlurryPommelBlade, u);
                fist = Vector2.Lerp(FlurryChamberFist, FlurryPommelFist, u);
                body = Mathf.Lerp(22f, -18f, u);
                back = Mathf.Lerp(-36f, 10f, u);
            }
            else if (t < holdEnd)
            {
                blade = FlurryPommelBlade;
                fist = FlurryPommelFist;
                body = -18f;
                back = 10f;
            }
            else
            {
                float u = Smooth(holdEnd, T);
                blade = Mathf.Lerp(FlurryPommelBlade, FlurryEndBlade, u);
                fist = Vector2.Lerp(FlurryPommelFist, FlurryEndFist, u);
                body = Mathf.Lerp(-18f, 0f, u);
                back = Mathf.Lerp(10f, -20f, u);
            }
        }

        /// <summary>
        /// How high a hopping swing lifts the figure, as a fraction of the step's HopHeight.
        /// The rise fills the cock and the drop fills the strike, so the body's weight is coming
        /// DOWN exactly while the blade is - which is the whole reason to hop at all.
        /// </summary>
        float _swingHop;

        /// <summary>This frame's hop lift, recomputed every frame while a swing is playing.</summary>
        float _hopHeight;

        /// <summary>
        /// Progress through the chop's arc as a function of time: 0 at the start angle, 1 at the
        /// end angle, briefly NEGATIVE during the cock and briefly ABOVE 1 at the overshoot.
        /// Piecewise so each phase has its own speed - which is the whole point.
        ///
        /// Phase boundaries derive from the swing's real duration so the cock and strike hold
        /// their ABSOLUTE lengths (see ChopCockSeconds): on a 0.45s basic the strike now spans
        /// ~27% of the swing instead of a fixed 15%, and on the 0.55s finisher it tightens back.
        /// Only the follow-through stretches and shrinks with the tempo.
        /// </summary>
        /// <summary>
        /// Where the cock ends and the strike ends, as fractions of this swing's duration.
        /// Shared so the hop can land on the same beats the arc does - a hop that peaked at a
        /// different moment than the strike would fight it rather than power it.
        /// </summary>
        /// <summary>
        /// Turn one pauldron about the shoulder it caps by <see cref="PauldronFollow"/> of the
        /// carry's raise - only the plate over the CARRYING arm, and only in the carry. Every
        /// other pose writes the plate back to exactly where Apply put it, so combat, where the
        /// arms swing through half a circle, never flaps the armour.
        ///
        /// The one exception is the disc stance's splay (_offSplay), which the plate over the far
        /// arm follows by the same share: that arm is held OUT for as long as the discs are, so it
        /// is a pose, not a swing.
        ///
        /// About the joint's REST position, not its live one: the carrying shoulder is at rest
        /// whenever the carry is fully open, and the two-handed grip's relocated off-hand
        /// shoulder (ArmBackGripRest) would drag a plate across the chest mid-blend.
        /// </summary>
        void FollowPauldron(RigLayer layer, Vector3 rest, bool carryBack, float side, float carryBlend)
        {
            if (!_layers.TryGetValue(layer, out var sr) || sr == null || !sr.enabled) return;

            bool onBack = rest.x > 0f;
            Vector3 joint = onBack ? _armBackRest : _armFrontRest;
            float a = onBack == carryBack
                ? side * CarryShoulderNow * _pauldronFollow * carryBlend
                : 0f;
            // The disc stance's far arm swings OUT to carry its disc clear of the body, and the
            // plate over it goes with it by the same share as a raise - left flat, the arm slid
            // out from under it while the near plate sat on its own arm (the user's report).
            if (onBack) a += _offSplay * _pauldronFollow;

            var turn = Quaternion.Euler(0f, 0f, a);
            sr.transform.localPosition = joint + turn * (rest - joint);
            sr.transform.localRotation = turn;
        }

        static void ChopPhases(float seconds, out float cockEnd, out float strikeEnd)
        {
            float dur = Mathf.Max(0.09f, seconds);
            cockEnd = Mathf.Clamp(ChopCockSeconds / dur, 0.10f, 0.30f);
            strikeEnd = cockEnd + Mathf.Clamp(ChopStrikeSeconds / dur, 0.15f, 0.45f);
        }

        static float ChopProgress(float k, float seconds)
        {
            ChopPhases(seconds, out float cockEnd, out float strikeEnd);

            if (k < cockEnd)
            {
                // Ease OUT into the cock: quick pull, then held at full draw until the strike.
                float t = Mathf.Sin(k / cockEnd * Mathf.PI * 0.5f);
                return -ChopCockAmount * t;
            }
            if (k < strikeEnd)
            {
                // The entire arc, plus overshoot, inside this short window. Smoothstepped so it
                // launches from the cock and lands on the overshoot without a velocity pop; the
                // window is short enough that it still reads as a snap.
                float t = (k - cockEnd) / (strikeEnd - cockEnd);
                t = t * t * (3f - 2f * t);
                return Mathf.Lerp(-ChopCockAmount, 1f + ChopOvershoot, t);
            }
            // Follow-through: settle from the overshoot back onto the true end pose.
            float u = Mathf.SmoothStep(0f, 1f, (k - strikeEnd) / (1f - strikeEnd));
            return Mathf.Lerp(1f + ChopOvershoot, 1f, u);
        }

        static void RisePhases(float seconds, out float cockEnd, out float strikeEnd)
        {
            float dur = Mathf.Max(0.09f, seconds);
            cockEnd = Mathf.Clamp(RiseCockSeconds / dur, 0.08f, 0.25f);
            strikeEnd = cockEnd + Mathf.Clamp(RiseStrikeSeconds / dur, 0.15f, 0.45f);
        }

        static float RiseProgress(float k, float seconds)
        {
            RisePhases(seconds, out float cockEnd, out float strikeEnd);

            if (k < cockEnd)
            {
                // Quick downward dip/load into the hips
                float t = Mathf.Sin(k / cockEnd * Mathf.PI * 0.5f);
                return -RiseCockAmount * t;
            }
            if (k < strikeEnd)
            {
                // Explosive upward snap with smoothstep acceleration and overshoot
                float t = (k - cockEnd) / (strikeEnd - cockEnd);
                t = t * t * (3f - 2f * t);
                return Mathf.Lerp(-RiseCockAmount, 1f + RiseOvershoot, t);
            }
            // Settle out of the overshoot into the high coiled guard
            float u = Mathf.SmoothStep(0f, 1f, (k - strikeEnd) / (1f - strikeEnd));
            return Mathf.Lerp(1f + RiseOvershoot, 1f, u);
        }

        static void WindPhases(float seconds, out float cockEnd, out float strikeEnd)
        {
            float dur = Mathf.Max(0.09f, seconds);
            cockEnd = Mathf.Clamp(WindCockSeconds / dur, 0.08f, 0.25f);
            strikeEnd = cockEnd + Mathf.Clamp(WindStrikeSeconds / dur, 0.15f, 0.45f);
        }

        /// <summary>
        /// 0 at the start, 1 at the end of the draw; negative through the load (the lift), past 1
        /// through the overshoot. Same three beats as Rise, so the two lead-ins share a rhythm.
        /// </summary>
        static float WindProgress(float k, float seconds)
        {
            WindPhases(seconds, out float cockEnd, out float strikeEnd);
            float lift = -WindLift / (WindFrom - WindTo);

            if (k < cockEnd)
                return lift * Mathf.Sin(k / cockEnd * Mathf.PI * 0.5f);
            if (k < strikeEnd)
            {
                float t = (k - cockEnd) / (strikeEnd - cockEnd);
                t = t * t * (3f - 2f * t);
                return Mathf.Lerp(lift, 1f + WindOvershoot, t);
            }
            float u = Mathf.SmoothStep(0f, 1f, (k - strikeEnd) / (1f - strikeEnd));
            return Mathf.Lerp(1f + WindOvershoot, 1f, u);
        }

        /// <summary>Impale's draw, 0 to 1: from the guard, level the blade and pull the fists
        /// back past the near hip into the chamber, coiling the torso away.</summary>
        static void ImpaleDraw(float t, out float blade, out Vector2 fist, out float bodySpin, out float backSwing)
        {
            float td = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            blade = Mathf.Lerp(ThrustGuardBlade, ImpaleAngle, td);
            fist = Vector2.Lerp(ThrustGuardFist, ImpaleChamberFist, td);
            bodySpin = Mathf.Lerp(0f, -16f, td);
            backSwing = Mathf.Lerp(10f, 25f, td);
        }

        /// <summary>Impale's thrust, 0 to 1: explosive, from the chamber to full extension.</summary>
        static void ImpaleThrust(float t, out float blade, out Vector2 fist, out float bodySpin, out float backSwing)
        {
            float tl = Mathf.Clamp01(t);
            tl = tl * tl * (3f - 2f * tl);
            blade = ImpaleAngle;
            fist = Vector2.Lerp(ImpaleChamberFist, ImpaleLungeFist, tl);
            bodySpin = Mathf.Lerp(-16f, 18f, tl);
            backSwing = Mathf.Lerp(25f, -15f, tl);
        }

        /// <summary>Impale's follow-through, 0 to 1: hold the skewer, then bring the fists home
        /// to the guard, the body untwisting with them.</summary>
        static void ImpaleRecover(float t, out float blade, out Vector2 fist, out float bodySpin, out float backSwing)
        {
            float raw = Mathf.Clamp01(t);
            float u = Mathf.SmoothStep(0f, 1f, raw);
            float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ImpaleHoldShare, 1f, raw));
            blade = Mathf.Lerp(ImpaleAngle, ThrustGuardBlade, back);
            fist = Vector2.Lerp(ImpaleLungeFist * 0.94f, ThrustGuardFist, back);
            bodySpin = Mathf.Lerp(Mathf.Lerp(18f, 12f, u), 0f, back);
            backSwing = Mathf.Lerp(Mathf.Lerp(-15f, -8f, u), 10f, back);
        }

        /// <summary>Seconds the Lunge art's thrust takes to full extension - the dash's length
        /// (Tuning.Impale.LungeSeconds), so the blade is out as the body arrives.</summary>
        const float LungeThrustSeconds = Core.Tuning.Impale.LungeSeconds;

        static void ImpalePhases(float seconds, out float drawEnd, out float strikeEnd)
        {
            float dur = Mathf.Max(0.12f, seconds);
            drawEnd = Mathf.Clamp(ImpaleDrawSeconds / dur, 0.20f, 0.45f);
            strikeEnd = drawEnd + Mathf.Clamp(ImpaleLungeSeconds / dur, 0.12f, 0.28f);
        }

        /// <summary>
        /// Blade angle a charged strike holds while it winds up: RAISED, up and back over the
        /// head. Set outright, with no blend from whatever pose came before - so it is the pose a
        /// lead-in hands a charge to.
        ///
        /// It was -150, written as if the blade ran along the arm: the arm went up overhead, but
        /// in this rig the blade points AWAY from the arm, so the held sword hung point-down across
        /// the whole body from the raised fists, and Slam (which starts here) swept it UP - a
        /// rising cut, the reverse of the slam both comments describe. Hidden while the arm was a
        /// straight stick on a sliding shoulder; now the fists and the blade are posed apart
        /// (ChargeFist), the arm reaching the fists (SolveArmReach).
        /// </summary>
        public const float ChargeArmSwing = 40f;

        /// <summary>
        /// Where a charge holds the fists, relative to the near shoulder at rest: straight up
        /// above it, a touch behind - both arms locked overhead.
        /// </summary>
        static readonly Vector2 ChargeFist = new Vector2(-0.03f, 0.17f);

        /// <summary>
        /// Where a Slam leaves the fists: low and forward, the blade driven down past the knee
        /// (<see cref="SlamTo"/>).
        /// </summary>
        static readonly Vector2 SlamFist = new Vector2(0.12f, -0.10f);

        /// <summary>The blade at the end of a Slam: forward and steeply down, past the knee.</summary>
        const float SlamTo = -140f;

        /// <summary>
        /// A Sweep's grip at its HIGH end (blade back and down, +115): in front, level with the
        /// shoulder - exactly where the old zero-reach arc put it, so a Sweep opening from the
        /// carry and a Chop following a backhand are unchanged.
        /// </summary>
        static readonly Vector2 SweepHighFist = new Vector2(0.157f, 0.073f);

        /// <summary>
        /// A Sweep's grip at its LOW end (blade forward and down, -115): low and in front, where
        /// a Chop leaves the hands and a Wind or a Rise takes them.
        /// </summary>
        static readonly Vector2 SweepLowFist = new Vector2(0.145f, -0.08f);

        /// <summary>
        /// How much of <see cref="Lunge"/> a Sweep pushes through the middle of its arc. Half,
        /// because the fists are now in front through the middle rather than under the shoulder,
        /// and the full push put them so far out that the whole figure slid twice as far as the
        /// shoulder shove it replaced.
        /// </summary>
        const float SweepLungeShare = 0.5f;


        /// <summary>
        /// The straight arm a motion is authored against, shoulder to grip, rig units - the
        /// elbow at 0.133 plus the grip 0.04 below it. armReach for a fist-and-blade pose is
        /// worked out against this; the rig reaches with the real arm, so a weapon whose grip
        /// sits a little elsewhere moves its fist by the difference, never the blade's angle.
        /// </summary>
        const float AuthoredArmToGrip = 0.173f;

        /// <summary>The armReach that puts the fist at <paramref name="fist"/> with the blade at
        /// <paramref name="blade"/> - the inverse of how the rig reads a pose.</summary>
        static Vector3 ReachFor(Vector2 fist, float blade)
        {
            float r = blade * Mathf.Deg2Rad;
            return new Vector3(fist.x - AuthoredArmToGrip * Mathf.Sin(r),
                               fist.y + AuthoredArmToGrip * Mathf.Cos(r), 0f);
        }

        /// <summary>
        /// How far through <paramref name="motion"/> (0..1 of its swing) the arm first passes
        /// <paramref name="angle"/> - before the aim is added, so -90 is the moment the blade points
        /// straight down the aim, whatever the aim is. 0 if it never does.
        /// </summary>
        public static float CrossingK(AttackMotion motion, float angle, bool alt)
        {
            float prev = Mathf.DeltaAngle(angle, ArmAngleAt(motion, 0f, alt));
            for (int i = 1; i <= 200; i++)
            {
                float k = i / 200f;
                float now = Mathf.DeltaAngle(angle, ArmAngleAt(motion, k, alt));
                if (Mathf.Sign(now) != Mathf.Sign(prev) && Mathf.Abs(now - prev) < 180f)
                    return Mathf.Lerp((i - 1) / 200f, k, Mathf.Abs(prev) / Mathf.Max(0.0001f, Mathf.Abs(prev) + Mathf.Abs(now)));
                prev = now;
            }
            return 0f;
        }

        /// <summary>The arm angle on a motion's first frame - where it wants the swing before it to end.</summary>
        public static float StartArmAngle(AttackMotion motion, bool alt) => ArmAngleAt(motion, 0f, alt);

        /// <summary>The arm angle a motion settles on - what it hands the swing after it.</summary>
        public static float EndArmAngle(AttackMotion motion, bool alt) => ArmAngleAt(motion, 1f, alt);

        static float ArmAngleAt(AttackMotion motion, float k, bool alt)
        {
            // Any real swing length: every motion's phases have settled by both ends.
            float arm = 0f, back = 0f, body = 0f;
            var reach = Vector3.zero;
            Animate(motion, k, alt, false, 0.4f, ref arm, ref back, ref body, ref reach);
            return arm;
        }

        /// <summary>
        /// Where a motion's first frame puts the near hand's grip, relative to the near shoulder at
        /// rest, rig units - the point the arm REACHES (SolveArmReach), worked out the way the rig
        /// reads a pose: armReach plus the straight arm at the blade's angle. What a hand-off has
        /// to match as well as the blade angle - see Moveset.CheckGripHandoffs.
        /// </summary>
        public static Vector2 StartGrip(AttackMotion motion, bool alt)
            => GripAt(motion, motion == AttackMotion.Lunge ? -1f : 0f, alt);   // the lunge starts with its draw

        /// <summary>Where a motion leaves the near hand's grip - see <see cref="StartGrip"/>.</summary>
        public static Vector2 EndGrip(AttackMotion motion, bool alt) => GripAt(motion, 1f, alt);

        /// <summary>Where a charge holds the near hand's grip - see <see cref="StartGrip"/>.</summary>
        public static Vector2 ChargeGrip => ChargeFist;

        static Vector2 GripAt(AttackMotion motion, float k, bool alt)
        {
            float arm = 0f, back = 0f, body = 0f;
            var reach = Vector3.zero;
            Animate(motion, k, alt, false, 0.4f, ref arm, ref back, ref body, ref reach);
            return (Vector2)reach + Rotate(new Vector2(0f, -AuthoredArmToGrip), arm);
        }

        // `mirrored` is only read by motions that pin a limb to a WORLD angle rather than swinging
        // through a relative arc - Plant, so far. Everything else is authored in rig-local degrees
        // and mirrors correctly for free, which is why this stayed static and keyframe-pure.
        static void Animate(AttackMotion motion, float k, bool alt, bool mirrored, float seconds,
                            ref float armSwing, ref float backSwing,
                            ref float bodySpin, ref Vector3 armReach)
        {
            float ease = Mathf.SmoothStep(0f, 1f, k);

            // Swap the ends of an arc for the return stroke. Only Chop and Sweep use it: those are
            // the two that travel from somewhere to somewhere else, so running one backwards is a
            // real upstroke or backhand. The others have a fixed sense - a thrust extends, a spin
            // revolves, a slam comes DOWN out of the charge that is still holding the blade up -
            // and reversing them would play a different move, not a mirrored one.
            float Arc(float from, float to) => Mathf.Lerp(alt ? to : from, alt ? from : to, ease);
            float Sense(float v) => alt ? -v : v;

            switch (motion)
            {
                case AttackMotion.Chop:
                {
                    // Cock over the shoulder, snap through the arc, settle out of the overshoot.
                    // LerpUnclamped because ChopProgress deliberately leaves [0,1] at both ends.
                    //
                    // `alt` does NOT swap the ends. Swapping them turned the second swing into a
                    // rising sweep, which reads as putting the sword back rather than attacking.
                    // Both variants travel the same way, DOWNWARD; what alternates is where the
                    // blade is cocked - on the shoulder, or up behind the head.
                    float p = ChopProgress(Mathf.Clamp01(k), seconds);
                    float mirror = alt ? -1f : 1f;

                    armSwing = Mathf.LerpUnclamped(
                        ChopFrom + (alt ? ChopAltCockBias : 0f), ChopTo, p);
                    backSwing = Mathf.LerpUnclamped(-20f, 25f, Mathf.Clamp(p, -0.3f, 1.3f));

                    // The grip ORBITS the body rather than spinning in place: back-and-up at
                    // full draw, driven forward-and-down through the strike, relaxing a touch
                    // in the follow-through. This translation is what makes the blade sweep
                    // through space instead of about a fixed point.
                    float c = Mathf.Clamp01(p);
                    float pull = Mathf.Clamp01(-p / ChopCockAmount);   // 1 at full cock
                    float fwd = Mathf.Sin(c * Mathf.PI * 0.62f) * ChopOrbit
                                - pull * ChopOrbit * 0.4f;
                    float lift = Mathf.Cos(c * Mathf.PI) * ChopOrbit * 0.35f
                                 + pull * ChopOrbit * 0.18f;

                    // The alt variant carries the grip across the body at full cock and pulls it
                    // back out through the strike, fading as the arc completes so both swings
                    // converge on the same follow-through. Small on purpose - what actually says
                    // "other side" is the weapon being drawn BEHIND the body, not this.
                    float behind = alt ? -ChopAltBehind * (1f - c) : 0f;
                    armReach = new Vector3(fwd + behind, lift, 0f);

                    // Body english: a small counter-rotation while cocked, then the shoulders
                    // punch through with the strike. At this sprite size the body carries more
                    // of the read than the blade angle does. Mirrored for the other shoulder,
                    // so the two swings lean opposite ways and read as a genuine back-and-forth.
                    bodySpin = mirror * (-14f * Mathf.Sin(c * Mathf.PI) + pull * 4f);
                    break;
                }

                case AttackMotion.Rise:
                {
                    // Upward cut: whips from low lead hip (-100°) up to the rear shoulder (+85°).
                    // As it reaches the top, the torso counter-twists into a loaded coil (+18°)
                    // and hands elevate (+0.16), leaving the body in the universal launchpad pose.
                    float p = RiseProgress(Mathf.Clamp01(k), seconds);
                    float c = Mathf.Clamp01(p);
                    float pull = Mathf.Clamp01(-p / RiseCockAmount);

                    armSwing = Mathf.LerpUnclamped(RiseFrom - pull * 12f, RiseTo, p);
                    backSwing = Mathf.LerpUnclamped(20f, -30f, Mathf.Clamp(p, -0.2f, 1.2f));

                    // Body coils clockwise as the blade rises, winding up torque for the finisher
                    bodySpin = Mathf.LerpUnclamped(-6f, 18f, c) - pull * 4f;

                    // The hands start where a Chop LEFT them - forward and low by the lead hip - and
                    // rise to the rear shoulder, lunging forward through the middle of the cut.
                    float fwd = Mathf.Lerp(RiseReachFrom, 0f, c) + Mathf.Sin(c * Mathf.PI) * Lunge * 0.75f;
                    float lift = Mathf.Lerp(RiseLiftFrom, 0.16f, c);
                    armReach = new Vector3(fwd, lift, 0f);
                    break;
                }

                case AttackMotion.Wind:
                {
                    // Lift a touch, then draw the blade back and down through the target: hands
                    // from in front of the body to the rear hip, torso turning away into the coil.
                    // `alt` is ignored - the coil has one sense, the one the finishers unwind.
                    float p = WindProgress(Mathf.Clamp01(k), seconds);
                    float c = Mathf.Clamp01(p);

                    armSwing = Mathf.LerpUnclamped(WindFrom, WindTo, p);
                    backSwing = Mathf.LerpUnclamped(25f, 10f, Mathf.Clamp(p, -0.2f, 1.2f));

                    // The load leans IN a few degrees (pressing the edge into the target) before
                    // the draw turns the torso away into the coil.
                    float load = Mathf.Clamp01(-p * (WindFrom - WindTo) / WindLift);   // 1 at full lift
                    bodySpin = p < 0f ? -WindLoadLean * load : Mathf.LerpUnclamped(0f, WindCoil, p);

                    // Low throughout - this is a cut at the legs, not a swing at the head - and
                    // the grip travels BACK, which is the whole difference from a thrust. The
                    // load lifts the hands a touch with the blade.
                    float fwd = Mathf.Lerp(WindReachFrom, -WindDrawBack, c);
                    float lift = Mathf.Lerp(-0.12f, WindLiftTo, c) + load * 0.04f;
                    armReach = new Vector3(fwd, lift, 0f);
                    break;
                }

                case AttackMotion.Sweep:
                {
                    // Wide lateral arc, the shoulders carrying it round; reversed is the backhand.
                    //
                    // The blade's arc is unchanged; the FISTS arc round the shoulder between
                    // SweepHighFist and SweepLowFist. With no reach of its own, the low end once
                    // left them level with the shoulder and BEHIND it - while the Chop before it
                    // and the Wind and Rise after it hand over with the hands low and in front, so
                    // every one of those hand-offs slid the sword through the body.
                    armSwing = Arc(115f, -115f);
                    backSwing = Arc(-50f, 50f);
                    bodySpin = Sense(Mathf.Sin(k * Mathf.PI) * 22f);
                    float from = Mathf.Atan2(SweepHighFist.y, SweepHighFist.x);
                    float to = Mathf.Atan2(SweepLowFist.y, SweepLowFist.x);
                    float a = Mathf.Lerp(alt ? to : from, alt ? from : to, ease);
                    float r = Mathf.Lerp(alt ? SweepLowFist.magnitude : SweepHighFist.magnitude,
                                         alt ? SweepHighFist.magnitude : SweepLowFist.magnitude, ease);
                    var fist = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r
                             + new Vector2(Lunge * SweepLungeShare * Mathf.Sin(k * Mathf.PI), 0f);
                    armReach = ReachFor(fist, armSwing);
                    break;
                }

                case AttackMotion.Thrust:
                    // Little rotation - the read is the arm extending and snapping back.
                {
                    float punch = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                    armSwing = Mathf.Lerp(ThrustGuardBlade, ImpaleAngle, punch);
                    armReach = ReachFor(Vector2.Lerp(ThrustGuardFist, ThrustExtendFist, punch), armSwing);
                    backSwing = Mathf.Lerp(15f, 35f, ease);
                    break;
                }

                case AttackMotion.Impale:
                {
                    // Two clear beats:
                    // 1. Chamber: from the guard, level the blade and draw the fists back past the
                    //    near hip (ImpaleChamberFist), coiling the torso back (-16°).
                    // 2. Lunge: explosive forward thrust to ImpaleLungeFist through the target,
                    //    then a held skewer and the fists home to the guard.
                    ImpalePhases(seconds, out float drawEnd, out float strikeEnd);

                    // The body twists (bodySpin turns the whole rig) but the BLADE stays level:
                    // each phase takes the twist back out of the blade angle, or the coil tipped it
                    // down and the lunge drove it up into a rising stab.
                    float blade;
                    Vector2 fist;
                    if (k < drawEnd)
                        ImpaleDraw(k / drawEnd, out blade, out fist, out bodySpin, out backSwing);
                    else if (k < strikeEnd)
                        ImpaleThrust((k - drawEnd) / (strikeEnd - drawEnd),
                                     out blade, out fist, out bodySpin, out backSwing);
                    else
                        ImpaleRecover((k - strikeEnd) / (1f - strikeEnd),
                                      out blade, out fist, out bodySpin, out backSwing);
                    armSwing = blade - bodySpin;
                    armReach = ReachFor(fist, armSwing);
                    break;
                }

                case AttackMotion.Lunge:
                {
                    // Impale's three beats with the DRAW moved out in front of frame zero: k in
                    // [-1, 0) is the pull-back from the guard into the chamber, played only by the
                    // timing bar's held wind-up (the rig maps the hold onto it); frame zero is the
                    // chamber, and the swing proper is the thrust - quick, so the blade is out as
                    // the body arrives - then the held skewer and the way home to the guard.
                    float blade;
                    Vector2 fist;
                    float strikeEnd = Mathf.Clamp(LungeThrustSeconds / Mathf.Max(0.12f, seconds), 0.1f, 0.35f);
                    if (k < 0f)
                        ImpaleDraw(k + 1f, out blade, out fist, out bodySpin, out backSwing);
                    else if (k < strikeEnd)
                        ImpaleThrust(k / strikeEnd, out blade, out fist, out bodySpin, out backSwing);
                    else
                        ImpaleRecover((k - strikeEnd) / (1f - strikeEnd),
                                      out blade, out fist, out bodySpin, out backSwing);
                    armSwing = blade - bodySpin;
                    armReach = ReachFor(fist, armSwing);
                    break;
                }

                case AttackMotion.Spin:
                    // A full revolution of the whole body: unmistakable, and honest about the
                    // fact that these are the finishers that hit everything around you.
                    bodySpin = -360f * ease;
                    armSwing = -70f;
                    // The fists low in FRONT for the whole turn: a Wind's coil unwinds INTO it (the
                    // fists whip across from the rear hip as the blade turns 62 degrees), a
                    // Thrust's guard is already there, and whatever follows starts from in front.
                    armReach = ReachFor(SweepLowFist, armSwing);
                    backSwing = 70f;
                    break;

                case AttackMotion.WhirlThrow:
                {
                    // Three beats: whip the blade out of the hand exactly as Throw's own release
                    // does, hold empty-handed while the orbiting copy (Combat.WhirlingBlade)
                    // actually carries it round the player, then reach back in to take it as it
                    // completes its lap.
                    //
                    // The BODY DOES NOT TURN - bodySpin is left at its default 0 for the whole
                    // window. An earlier version carried it through a half revolution to "help"
                    // sell the circling, and it read as exactly the old whole-body Spin this
                    // motion exists to replace: the WEAPON is what circles now, and a player has
                    // to be able to see that against a planted, stationary thrower, not read the
                    // body still turning underneath it.
                    const float ReleaseEnd = 0.22f, CatchStart = 0.78f;
                    if (k < ReleaseEnd)
                    {
                        float u = Mathf.SmoothStep(0f, 1f, k / ReleaseEnd);
                        armSwing = Mathf.Lerp(-105f, 15f, u);
                        armReach = new Vector3(0.24f * u, 0f, 0f);
                        backSwing = Mathf.Lerp(30f, -15f, u);
                    }
                    else if (k < CatchStart)
                    {
                        float u = (k - ReleaseEnd) / (CatchStart - ReleaseEnd);
                        armSwing = Mathf.Lerp(15f, -20f, u);
                        armReach = new Vector3(Mathf.Lerp(0.24f, 0.10f, u), 0f, 0f);
                        backSwing = Mathf.Lerp(-15f, 20f, u);
                    }
                    else
                    {
                        float u = Mathf.SmoothStep(0f, 1f, (k - CatchStart) / (1f - CatchStart));
                        armSwing = Mathf.Lerp(-20f, -70f, u);
                        armReach = new Vector3(Mathf.Lerp(0.10f, 0f, u), 0f, 0f);
                        backSwing = Mathf.Lerp(20f, 70f, u);
                    }
                    break;
                }

                case AttackMotion.Slam:
                {
                    // Comes down from where the charge left it, past the knee, with the body
                    // pitching after it. Front-loaded so the impact reads on the first frames.
                    //
                    // Posed as the FISTS and the BLADE: the fists arc forward over the top, from
                    // overhead (ChargeFist) to low in front (SlamFist), while the blade turns
                    // forward through the front from raised to driven down. It once swept the
                    // other way (-150 to 70) - see ChargeArmSwing.
                    float drive = Mathf.Pow(Mathf.Clamp01(k * 1.35f), 0.55f);
                    float fromAngle = Mathf.Atan2(ChargeFist.y, ChargeFist.x) * Mathf.Rad2Deg;
                    float toAngle = Mathf.Atan2(SlamFist.y, SlamFist.x) * Mathf.Rad2Deg;
                    float a = Mathf.Lerp(fromAngle, toAngle, drive) * Mathf.Deg2Rad;
                    float radius = Mathf.Lerp(ChargeFist.magnitude, SlamFist.magnitude, drive);
                    armSwing = Mathf.Lerp(ChargeArmSwing, SlamTo, drive);
                    armReach = ReachFor(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, armSwing);
                    backSwing = Mathf.Lerp(-150f, 55f, drive);
                    bodySpin = Mathf.Sin(Mathf.Clamp01(k * 1.35f) * Mathf.PI) * -14f;
                    break;
                }

                case AttackMotion.Plant:
                    // THREE BEATS, not one sweep: spin the blade point-down in the hand, raise the
                    // hilt to head height, then drive it into the ground and hold.
                    //
                    // A single lerp read as the character simply lowering the sword. The pause at
                    // the top is what makes the plant land - the raise is a wind-up, and without
                    // one there is nothing for the drive to be the release of.
                    //
                    // The ROTATION happens once, in beat one, and then holds: the blade is already
                    // point-down for the rest of the move, so beats two and three are pure vertical
                    // travel of the hand. That is also why the hilt is what rises - the sprite
                    // pivots at the grip, so raising the hand raises the hilt and the blade hangs
                    // from it.
                    const float SpinEnd = 0.30f, RaiseEnd = 0.62f, DriveEnd = 0.85f;

                    // 168 is measured, not chosen: the tip sits 0.04 below the hips at 96 degrees
                    // and 0.38 below at 168, with the feet at 0.453. Below about 150 the sword is
                    // merely lowered, not planted, and the fire looks like it comes out of the
                    // character's knees.
                    // Beat two lifts, beat three drives. Held apart so the drive can be FAST while
                    // the raise is not - the same distance travelled twice at the same speed reads
                    // as the sword bobbing rather than as a strike.
                    float hilt, across;
                    if (k < RaiseEnd)
                    {
                        float rise = Mathf.SmoothStep(0f, 1f,
                            Mathf.Clamp01((k - SpinEnd * 0.5f) / (RaiseEnd - SpinEnd * 0.5f)));
                        hilt = Mathf.Lerp(0f, HiltRaise, rise);
                        across = Mathf.Lerp(0f, PlantForward, rise);   // brought across on the raise
                        bodySpin = Mathf.Lerp(0f, -6f, rise);          // shoulders open on the wind-up
                    }
                    else
                    {
                        // Eased OUT, not in: the hand is already moving when the drive starts and
                        // arrives hard, which is where the weight of the blow lives.
                        float slam = 1f - Mathf.Pow(1f - Mathf.Clamp01((k - RaiseEnd) / (DriveEnd - RaiseEnd)), 3f);
                        hilt = Mathf.Lerp(HiltRaise, -0.09f, slam);
                        across = PlantForward;                         // and stays there
                        bodySpin = Mathf.Lerp(-6f, 10f, slam);         // close hard over the plant
                    }
                    armReach = new Vector3(across, hilt, 0f);

                    // STRAIGHT DOWN, and the compensation is the point. The arm hangs under the
                    // root, so the blade's world angle carries bodySpin as well - and bodySpin is
                    // doing real work here, opening on the wind-up and closing over the plant. A
                    // fixed arm angle therefore leaned the blade by however much the body happened
                    // to be turned: about 20 degrees off vertical through the whole raise.
                    //
                    // The sign flips with the MIRROR, and that is not symmetry for its own sake.
                    // Unity's TRS puts a transform's rotation OUTSIDE its own scale, so the root's
                    // -1 x scale mirrors every child's angle beneath it but not the root's own
                    // bodySpin: the world angle is bodySpin + armSwing facing right and bodySpin -
                    // armSwing facing left. One formula therefore cancels the lean in one direction
                    // and DOUBLES it in the other - measured at exactly 0.0 and 20.0 degrees off
                    // vertical, 20 being twice the 10-degree close on the plant.
                    float straight = mirrored ? bodySpin - 180f : 180f - bodySpin;
                    float turn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / SpinEnd));
                    armSwing = Mathf.Lerp(0f, straight, turn);
                    backSwing = Mathf.Lerp(0f, straight - 13f, turn);   // off hand trails on the hilt
                    break;

                case AttackMotion.Throw:
                    // Whip from behind the ear and snap straight, ending with the arm extended:
                    // the release has to land at the moment the blade appears in the world.
                    armSwing = Mathf.Lerp(-105f, 10f, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 1.6f)));
                    armReach = new Vector3(0.26f * Mathf.Min(1f, k * 1.6f), 0f, 0f);
                    backSwing = Mathf.Lerp(30f, -20f, ease);
                    bodySpin = Mathf.Sin(k * Mathf.PI) * -10f;
                    break;

                case AttackMotion.Draw:
                {
                    // Beat one: a dip down to the belt for a vial - a flourish, not the point of
                    // the shot, so it stays brief, but it has to actually reach where the vials
                    // now sit (belt centre, roughly at torso height) rather than a token nod
                    // downward. The hand rests well above that (the arm pivot alone sits ~0.24
                    // units above torso centre), so this is a real reach, not a twitch.
                    const float ReachEnd = 0.30f;
                    const float ReachX = -0.06f, ReachY = -0.22f;

                    // The bow hand - now the BACK arm, see Apply's re-parenting - settles into
                    // its aim early and HOLDS there through both beats, independent of whatever
                    // the string hand is doing. It is the anchor the draw works against, not a
                    // participant in the reach or the pull; a bow that also moved through the
                    // reach-and-draw would read as two hands throwing the same object back and
                    // forth rather than one steady grip and one working hand.
                    backSwing = Mathf.Lerp(0f, 58f, Mathf.Clamp01(k / 0.25f));

                    if (k < ReachEnd)
                    {
                        float reach01 = Mathf.SmoothStep(0f, 1f, k / ReachEnd);
                        armSwing = Mathf.Lerp(0f, -35f, reach01);
                        armReach = new Vector3(ReachX * reach01, ReachY * reach01, 0f);
                    }
                    else
                    {
                        // Beat two: the string hand pulls back toward the ear. ROTATION is what
                        // should carry this, not translation - the hand travels through an arc
                        // pivoting at the shoulder, the same as every other motion in this rig,
                        // rather than sliding away from it in a straight line. Translation was
                        // tried twice: -0.38 fully detached the hand from the shoulder (a
                        // floating glove with a visible gap), and even the reduced -0.15 still
                        // left a seam, because ANY armReach on top of a held rotation moves the
                        // whole sprite bodily off its pivot - there is no amount of translation
                        // that reads as "the same arm, further back" rather than "the arm
                        // slid".
                        //
                        // The first version of this motion also had the SIGN backwards: it moved
                        // armSwing from -35 toward -12, which is toward neutral/forward - exactly
                        // the direction Throw's own release whip travels (-105 cocked -> +10
                        // extended). That is precisely why it read as a throw. A draw needs the
                        // opposite: further NEGATIVE, deeper behind the ear, the same direction
                        // Throw's own wind-up sits in but arrived at slowly instead of whipped.
                        float draw01 = 1f - Mathf.Pow(1f -
                            Mathf.Clamp01((k - ReachEnd) / (1f - ReachEnd)), 2f);
                        armSwing = Mathf.Lerp(-35f, -95f, draw01);
                        armReach = new Vector3(Mathf.Lerp(ReachX, 0f, draw01),
                                               Mathf.Lerp(ReachY, 0f, draw01), 0f);
                        bodySpin = Mathf.Sin(draw01 * Mathf.PI * 0.5f) * -6f;
                    }
                    break;
                }

                case AttackMotion.Jab:
                    // ONE quick shallow strike. This is a basic, so it stays a poke.
                {
                    float extend = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                    armSwing = Mathf.Lerp(ThrustGuardBlade, ImpaleAngle, extend);
                    armReach = ReachFor(Vector2.Lerp(ThrustGuardFist, JabExtendFist, extend), armSwing);
                    backSwing = Mathf.Lerp(10f, 24f, extend);
                    break;
                }

                case AttackMotion.Flurry:
                {
                    // Cut down, cut back up, turn the hilt, drive the pommel. Keyed off ABSOLUTE
                    // time on the sequence's own clock (Tuning.Flurry), never `seconds`: the hits
                    // land on these beats, and every caller (the stills, the hand-off checks, the
                    // sweep test) must see the same picture. Every phase is a FIST and a BLADE
                    // (ReachFor), so the hands are always where the grip is.
                    FlurryPose(Mathf.Clamp01(k) * Core.Tuning.Flurry.Seconds,
                               out var fist, out armSwing, out bodySpin, out backSwing);
                    armReach = ReachFor(fist, armSwing);
                    break;
                }
            }
        }

        /// <summary>
        /// Watch the figure's total lift for the frame it reaches the floor, and step the
        /// compression that follows - see the "landing" constants.
        /// </summary>
        void AdvanceLanding(float dt)
        {
            // Both lifts together. Which one put the character in the air is not something the
            // landing has an opinion about: a leap finisher and a swing hop both end with weight
            // arriving back on the legs, and the height is what decides how much of it there is.
            float lift = _airHeight + _hopHeight;

            if (_lastLift > LandGroundedLift && lift <= LandGroundedLift)
            {
                // Fed as a VELOCITY so the compression travels in over a few frames rather than
                // appearing at full depth on the landing frame - a squash that is simply present
                // on frame one is a pop, which is the whole thing this is meant to smooth. The
                // sqrt converts the depth wanted into the velocity that reaches it, so the
                // constant stays readable as a distance.
                float fall = Mathf.Clamp01(_lastLift / LandFullHeight);
                _landVelocity += fall * LandSquash * Mathf.Sqrt(LandStiffness);
            }
            _lastLift = lift;

            _landVelocity += -_landDepth * LandStiffness * dt;
            _landVelocity *= Mathf.Exp(-LandDamping * dt);
            _landDepth += _landVelocity * dt;
        }

        /// <summary>Fade the hit shove out linearly over its own window.</summary>
        void AdvanceHitReaction(float dt)
        {
            if (_hitTimer <= 0f) return;
            _hitTimer -= dt;
            if (_hitTimer <= 0f)
            {
                _hitTimer = 0f;
                _hitPush = Vector2.zero;
            }
        }

        /// <summary>
        /// Shove the DRAWING away from a hit for a moment. See the "hit reaction" constants for
        /// why this is not knockback and cannot become it.
        ///
        /// Takes the direction the blow pushes, in world units - the caller knows where the
        /// attacker was and the rig does not. A zero vector is ignored rather than defaulted,
        /// since a hit with no direction (a hazard standing under the player) has no answer that
        /// is better than not moving.
        /// </summary>
        public void PlayHitReaction(Vector2 push)
        {
            if (push.sqrMagnitude < 0.0001f) return;

            // Overwritten rather than accumulated: two hits in the same handful of frames is one
            // event as far as the drawing is concerned, and adding them would walk the figure a
            // visible distance off its own feet.
            _hitPush = push.normalized * HitReactionDistance;
            _hitTimer = HitReactionSeconds;
        }

        /// <summary>
        /// Step the weight-shift spring. Called at the TOP of Update, before FaceAim can change
        /// which way the rig is drawn - see the "weight shift" constants.
        /// </summary>
        void AdvanceWeightShift(float dt)
        {
            // Into the frame the rig is drawn in, the same convention the cape uses. A lean is a
            // statement about forward and back, so it has to be measured against the facing and
            // not against the world.
            float forward = Body != null ? Body.linearVelocity.x : 0f;
            if (_facingLeft) forward = -forward;

            float reference = Player != null ? Mathf.Max(0.1f, Player.MoveSpeed) : CapeReferenceSpeed;
            _forwardReference = Mathf.Lerp(_forwardReference, forward,
                                           1f - Mathf.Exp(-LeanFollow * dt));

            // Negative tips the chest forward, so pulling AHEAD of the reference (a start) leans
            // into it and falling behind it (a stop) rocks back.
            float target = -Mathf.Clamp((forward - _forwardReference) / reference, -1f, 1f) * LeanAngle;

            _leanVelocity += (target - _leanAngle) * LeanStiffness * dt;
            _leanVelocity *= Mathf.Exp(-LeanDamping * dt);
            _leanAngle += _leanVelocity * dt;

            _leanSuppress = Mathf.Lerp(_leanSuppress,
                                       _attackTimer > 0f || _chargeTimer > 0f ? 1f : 0f,
                                       1f - Mathf.Exp(-PoseRecoverySharpness * dt));
        }

        /// <summary>
        /// Plant a turn if the mirror flipped this frame, then write the lean onto the body.
        ///
        /// Runs immediately after FaceAim because that is what flips the mirror, and because
        /// FaceAim clears the torso and head rotations - anything written before it is discarded.
        /// </summary>
        void ApplyWeightShift(bool wasFacingLeft)
        {
            if (_facingKnown && _facingLeft != wasFacingLeft)
            {
                // Re-express the spring in the frame the rig is NOW drawn in, then kick it. The
                // velocity has not changed - only the frame measuring it has - so each carried
                // value negates rather than resetting: a chest still travelling the old way is a
                // chest now leaning BACKWARD, which is the read a reversal is supposed to have.
                // Reset them instead and the lean pops to zero on the frame of every turn, which
                // is the opposite of what this is for.
                _forwardReference = -_forwardReference;
                _leanAngle = -_leanAngle;
                _leanVelocity = -_leanVelocity + TurnPlantKick;
                _walkLean = -_walkLean;   // the walk's lean, too: same chest, new frame
            }
            _facingKnown = true;

            // Top-down puts the whole facing on the root yaw and has no front to lean over, so it
            // keeps the identity rotations FaceAim just wrote.
            if (TopDown) return;

            // The spring's lean answers a change of speed, the walk's lean holds while the speed
            // does (see WalkLeanDegrees); the hips' plant reads only the spring's.
            float lean = (_leanAngle + _walkLean) * (1f - _leanSuppress * LeanSwingSuppression);
            if (_torso) _torso.localRotation = Quaternion.Euler(0f, 0f, lean);
            if (_head) _head.localRotation = Quaternion.Euler(0f, 0f, -lean * LeanHeadShare);
        }

        /// <summary>
        /// Swing the cape so it trails the run and settles when you stop.
        ///
        /// Driven by the FORWARD component of velocity in the mirrored frame, not by world x: the
        /// rig flips its own root scale to face left, so a raw world velocity would swing the
        /// cloth the wrong way round for half the compass.
        ///
        /// BENT, not turned (ClothBend). It used to orbit the neckline as one rigid sheet, and a
        /// sheet turned about the middle of its top edge tips that edge: the shoulder corners rose
        /// and fell about eight cells at a full swing while the hem moved as one plank with them.
        /// Two springs now - the cloth under the hinge chasing the run, the hem chasing THAT -
        /// and the bend blends between them down the cloth, pinned at the hinge.
        /// </summary>
        void AnimateCape(float dt, float speed)
        {
            if (!_hasCape || !_pivots.TryGetValue(RigLayer.Back, out var cape) || cape == null) return;

            float forward = Body != null ? Body.linearVelocity.x : 0f;
            if (_facingLeft) forward = -forward;   // into the frame the rig is drawn in

            float reference = Player != null ? Mathf.Max(0.1f, Player.MoveSpeed) : CapeReferenceSpeed;
            float target = -Mathf.Clamp(forward / reference, -1f, 1f) * CapeSwing * _capeSwingScale;

            _capeAngularVelocity += (target - _capeAngle) * CapeStiffness * dt;
            _capeAngularVelocity *= Mathf.Exp(-CapeDamping * dt);
            _capeAngle += _capeAngularVelocity * dt;

            _capeHemVelocity += (_capeAngle - _capeHemAngle) * CapeHemStiffness * dt;
            _capeHemVelocity *= Mathf.Exp(-CapeHemDamping * dt);
            _capeHemAngle += _capeHemVelocity * dt;

            // BENT, not turned - see ClothBend. The sprite stays where Apply parked it.
            cape.localPosition = _capeRest;
            cape.localRotation = Quaternion.identity;

            float ripple = _capeLength * Mathf.Sin(CapeFlutter * Mathf.Deg2Rad)
                           * Mathf.Clamp01(speed / reference) * _capeSwingScale;
            float hingeY = _capeRest.y + _capeHalfHeight;
            if (_layers.TryGetValue(RigLayer.Back, out var capeSr) && capeSr != null)
                Bend(_capeCloth = _capeCloth != null ? _capeCloth : ClothBend.On(capeSr),
                     hingeY, _capeLength, _capeAngle, _capeHemAngle, ripple, Time.time * 17f);

            // The drape that swings with it is the same cloth: the same hinge, the same length,
            // so at any height it bends exactly as the cape behind it does.
            bool drape = _drapeSwings && _layers.TryGetValue(RigLayer.BackOver, out var drapeSr) && drapeSr != null;
            if (drape) Bend(_drapeCloth = _drapeCloth != null ? _drapeCloth : ClothBend.On(_layers[RigLayer.BackOver]),
                            hingeY, _capeLength, _capeAngle, _capeHemAngle, ripple, Time.time * 17f);
            else if (_drapeCloth != null) _drapeCloth.Active = false;
        }

        /// <summary>Hand a cloth this frame's bend: its hinge and length in torso space, the
        /// angle under the hinge and at the end (degrees), and the ripple running down it.</summary>
        static void Bend(ClothBend cloth, float hingeY, float length, float upperDeg, float hemDeg,
                         float ripple, float phase)
        {
            cloth.Active = true;
            cloth.HingeY = hingeY;
            cloth.Length = length;
            cloth.UpperRad = upperDeg * Mathf.Deg2Rad;
            cloth.LowerRad = hemDeg * Mathf.Deg2Rad;
            cloth.RippleAmp = ripple;
            cloth.RipplePhase = phase;
        }

        /// <summary>
        /// Same springs and the same bend as <see cref="AnimateCape"/>, hinged at the knot instead
        /// of the shoulders, and bending <see cref="RigLayer.NeckBack"/> - the tail alone, tucked
        /// behind the torso - rather than the front-facing collar on <see cref="RigLayer.Neck"/>.
        /// Turned rigidly, the whole tail swung as one stick from the knot.
        /// </summary>
        void AnimateScarf(float dt, float speed)
        {
            if (!_hasScarf || !_pivots.TryGetValue(RigLayer.NeckBack, out var scarf) || scarf == null) return;

            // A hooded cloak covers the collar entirely, so there is nothing for the tail to
            // swing out from under - parked at rest rather than left running behind the hood,
            // where the spring would keep accumulating a swing nobody can see. Reset the spring
            // itself, not just the transform, or taking the hood back off would resume the swing
            // from whatever velocity it had quietly built up while hidden.
            scarf.localPosition = (Vector3)_scarfRest;
            scarf.localRotation = Quaternion.identity;
            if (_hoodOn)
            {
                _scarfAngle = 0f;
                _scarfAngularVelocity = 0f;
                _scarfHemAngle = 0f;
                _scarfHemVelocity = 0f;
                if (_scarfCloth != null) _scarfCloth.Active = false;
                return;
            }

            float forward = Body != null ? Body.linearVelocity.x : 0f;
            if (_facingLeft) forward = -forward;   // into the frame the rig is drawn in

            float reference = Player != null ? Mathf.Max(0.1f, Player.MoveSpeed) : CapeReferenceSpeed;
            float target = -Mathf.Clamp(forward / reference, -1f, 1f) * ScarfSwing;

            _scarfAngularVelocity += (target - _scarfAngle) * ScarfStiffness * dt;
            _scarfAngularVelocity *= Mathf.Exp(-ScarfDamping * dt);
            _scarfAngle += _scarfAngularVelocity * dt;

            _scarfHemVelocity += (_scarfAngle - _scarfHemAngle) * ScarfHemStiffness * dt;
            _scarfHemVelocity *= Mathf.Exp(-ScarfHemDamping * dt);
            _scarfHemAngle += _scarfHemVelocity * dt;

            float length = _scarfHalfHeight * 2f;   // hinged at the tail's top edge
            float ripple = length * Mathf.Sin(ScarfFlutter * Mathf.Deg2Rad) * Mathf.Clamp01(speed / reference);
            if (_layers.TryGetValue(RigLayer.NeckBack, out var tailSr) && tailSr != null)
                Bend(_scarfCloth = _scarfCloth != null ? _scarfCloth : ClothBend.On(tailSr),
                     _scarfRest.y + _scarfHalfHeight, length, _scarfAngle, _scarfHemAngle, ripple,
                     Time.time * 21f);
        }

        /// <summary>
        /// Hang the cloth worn over the legs (the Tasset and Belt layers) from the HIPS and let
        /// the legs push it - see ClothBend's OverLegs. Both layers get the same numbers, so
        /// faulds and the skirt under them move together at every height.
        ///
        /// Before this, both rode the torso as boards: the walk's lean swung a floor-length hem
        /// back several texels while the legs stayed planted, and the front foot came out
        /// through the cloth; a knee coming forward in the swing passed through it the same way.
        /// </summary>
        void AnimateLegCloth(float dt, float speed)
        {
            bool tasset = _layers.TryGetValue(RigLayer.Tasset, out var tassetSr) && tassetSr != null
                          && tassetSr.sprite != null;
            bool belt = _layers.TryGetValue(RigLayer.Belt, out var beltSr) && beltSr != null
                        && beltSr.sprite != null;
            if (TopDown || _torso == null || _hips == null || _legFront == null || _legBack == null
                || (!tasset && !belt))
            {
                if (_tassetCloth != null) _tassetCloth.Active = false;
                if (_beltCloth != null) _beltCloth.Active = false;
                return;
            }

            float Angle(Transform t) => t != null ? Mathf.DeltaAngle(0f, t.localEulerAngles.z) : 0f;
            float thighF = Angle(_legFront), shinF = thighF + Angle(_kneeFront);
            float thighB = Angle(_legBack), shinB = thighB + Angle(_kneeBack);

            if (!_lcPrimed)
            {
                _lcThighF = thighF; _lcShinF = shinF; _lcThighB = thighB; _lcShinB = shinB;
                _lcThighFV = _lcShinFV = _lcThighBV = _lcShinBV = 0f;
                _lcPrimed = true;
            }
            Spring(ref _lcThighF, ref _lcThighFV, thighF, LegClothStiffness, LegClothDamping, dt);
            Spring(ref _lcShinF, ref _lcShinFV, shinF, LegClothStiffness, LegClothDamping, dt);
            Spring(ref _lcThighB, ref _lcThighBV, thighB, LegClothStiffness, LegClothDamping, dt);
            Spring(ref _lcShinB, ref _lcShinBV, shinB, LegClothStiffness, LegClothDamping, dt);

            float forward = Body != null ? Body.linearVelocity.x : 0f;
            if (_facingLeft) forward = -forward;   // into the frame the rig is drawn in
            float reference = Player != null ? Mathf.Max(0.1f, Player.MoveSpeed) : CapeReferenceSpeed;
            float target = -Mathf.Clamp(forward / reference, -1f, 1f) * LegClothSwing;
            Spring(ref _lcAngle, ref _lcAngleV, target, LegClothSwingStiffness, LegClothSwingDamping, dt);
            Spring(ref _lcHem, ref _lcHemV, _lcAngle, LegClothHemStiffness, LegClothHemDamping, dt);

            // Hips and torso are siblings under the rig root; the legs' joints in the torso's
            // own coordinates as the HIPS hold them (the cloth is put back into the hips' frame).
            Vector2 hipsOff = (Vector2)(_hips.localPosition - _torso.localPosition);
            Vector2 hipF = hipsOff + (Vector2)_legFront.localPosition;
            Vector2 hipB = hipsOff + (Vector2)_legBack.localPosition;
            float thighLength = -KneeRest.y, shinLength = LegLength - thighLength;
            float legWidth = PixelSprite.Px(Proportions.Cells(Proportions.LegW));

            // One length for both layers - the longer cloth's - so they bend alike at any depth.
            float Bottom(SpriteRenderer sr) =>
                sr.transform.localPosition.y + sr.sprite.bounds.min.y * Mathf.Abs(sr.transform.localScale.y);
            float bottom = Mathf.Min(tasset ? Bottom(tassetSr) : 0f, belt ? Bottom(beltSr) : 0f);
            float length = Mathf.Max(0.05f, -bottom);

            float ripple = length * Mathf.Sin(LegClothFlutter * Mathf.Deg2Rad) * Mathf.Clamp01(speed / reference);
            float plumb = Mathf.DeltaAngle(0f, _torso.localEulerAngles.z) * Mathf.Deg2Rad;

            void Drive(ClothBend cloth)
            {
                Bend(cloth, 0f, length, _lcAngle, _lcHem, ripple, Time.time * 13f);
                cloth.OverLegs = true;
                cloth.Plumb = plumb;
                cloth.HipFront = hipF;
                cloth.HipBack = hipB;
                cloth.ThighFront = _lcThighF * Mathf.Deg2Rad;
                cloth.ShinFront = _lcShinF * Mathf.Deg2Rad;
                cloth.ThighBack = _lcThighB * Mathf.Deg2Rad;
                cloth.ShinBack = _lcShinB * Mathf.Deg2Rad;
                cloth.ThighLength = thighLength;
                cloth.ShinLength = shinLength;
                cloth.LegHalfWidth = legWidth * 0.5f;
                cloth.FarFollow = LegClothFarFollow;
                cloth.DrapeFall = LegClothDrapeFall;
            }

            if (tasset) Drive(_tassetCloth = _tassetCloth != null ? _tassetCloth : ClothBend.On(tassetSr));
            else if (_tassetCloth != null) _tassetCloth.Active = false;
            if (belt) Drive(_beltCloth = _beltCloth != null ? _beltCloth : ClothBend.On(beltSr));
            else if (_beltCloth != null) _beltCloth.Active = false;
        }

        /// <summary>A damped spring step: <paramref name="value"/> chasing
        /// <paramref name="target"/>, the cape's integration.</summary>
        static void Spring(ref float value, ref float velocity, float target, float stiffness,
                           float damping, float dt)
        {
            velocity += (target - value) * stiffness * dt;
            velocity *= Mathf.Exp(-damping * dt);
            value += velocity * dt;
        }

        /// <summary>
        /// Swing the belt vials the same way the cape trails a run - strung to the belt rather
        /// than a stiff board bolted across it. One shared spring rather than one per vial: a
        /// belt's own canisters are strung close together and swing as a row, not independently.
        /// </summary>
        void AnimateVials(float dt)
        {
            if (_vials == null) return;

            float forward = Body != null ? Body.linearVelocity.x : 0f;
            if (_facingLeft) forward = -forward;

            float reference = Player != null ? Mathf.Max(0.1f, Player.MoveSpeed) : CapeReferenceSpeed;
            float target = -Mathf.Clamp(forward / reference, -1f, 1f) * VialSwing;

            _vialAngularVelocity += (target - _vialAngle) * VialStiffness * dt;
            _vialAngularVelocity *= Mathf.Exp(-VialDamping * dt);
            _vialAngle += _vialAngularVelocity * dt;

            var rot = Quaternion.Euler(0f, 0f, _vialAngle);
            foreach (var v in _vials)
                if (v != null) v.transform.localRotation = rot;
        }

        /// <summary>
        /// Flip the whole rig rather than each sprite: flipping renderers individually would
        /// mirror them in place and break the front/back limb relationship.
        /// </summary>
        /// <summary>
        /// Turn toward the aim so a swing always reads as happening IN FRONT.
        ///
        /// It used to only mirror on X, so the character faced left or right and nothing else - a
        /// swing aimed north played out sideways while the damage arc pointed north, and the two
        /// disagreed visibly.
        ///
        /// The TORSO NEVER ROTATES. It used to tip up to 42 degrees toward a high or low aim, with
        /// the arm covering whatever was left over. That put the whole paper-doll on a slant -
        /// pauldrons, belt and cuirass all tilting as one slab - which is not what a person does
        /// when they raise a weapon, and it is exactly what the twelve-slot silhouette exists to
        /// keep legible. Now the body stays upright and the ARMS carry the entire angle, so a high
        /// aim lifts the hands and the sword with them and nothing else moves.
        /// </summary>
        /// <summary>Explicit aim for a rig with no controller and no body. See SetFacing.</summary>
        Vector2? _facingOverride;

        /// <summary>The arms' tilt limit while a move asks for more than ArmAimRange - see
        /// ICharacterRig.SetAimRange. 0 = the rig's own.</summary>
        float _aimRangeOverride;

        public void SetAimRange(float degrees) => _aimRangeOverride = Mathf.Max(0f, degrees);

        public void SetFacing(Vector2 aim)
        {
            if (aim.sqrMagnitude < 0.0001f) { _facingOverride = null; return; }
            _facingOverride = aim.normalized;

            // Set the MIRROR here rather than leaving it to FaceAim. FaceAim freezes the mirror
            // for the duration of a swing, and an echo is placed and told to swing in the same
            // frame - so by the time FaceAim next runs the swing is already live and the figure
            // would hold whichever way it happened to be facing before.
            if (Mathf.Abs(_facingOverride.Value.x) > MirrorDeadzone)
                _facingLeft = _facingOverride.Value.x < 0f;
        }

        float AimRangeNow => _aimRangeOverride > 0f ? _aimRangeOverride : ArmAimRange;

        void FaceAim()
        {
            var aim = _facingOverride ??
                    ( Player != null ? Player.Facing
                    : Body != null ? (Vector2)Body.linearVelocity
                    : Vector2.zero );
            if (aim.sqrMagnitude < 0.0025f) return;
            aim.Normalize();

            if (TopDown)
            {
                // The body IS the facing. Seen from above there is no upside down, so it turns the
                // full 360 and the swing genuinely happens where the arc is - which is the whole
                // point: the damage cone and the animation stop disagreeing.
                //
                // -90 because the placeholder is drawn facing +Y (up the screen), and rig-local +X
                // is what the attack code treats as forward.
                float want = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg - 90f;
                _bodyYaw = Mathf.LerpAngle(_bodyYaw, want, 1f - Mathf.Exp(-TurnSharpness * Dt));

                transform.localScale = Vector3.one * _visualScale;
                if (_torso) _torso.localRotation = Quaternion.identity;
                if (_head) _head.localRotation = Quaternion.identity;
                _aimResidual = 0f;
                _facingYaw = _bodyYaw;
                return;
            }

            // A swing, once started, plays out exactly as it started.
            //
            // Both of the things below feed the arm's absolute angle, and both used to keep moving
            // mid-swing: the mirror could flip the character round halfway through a strike, and
            // the aim offset slid the whole arc up or down as the auto-targeter drifted. The
            // motion is a fixed number of degrees - Chop is 175, Sweep is 230 - but neither the
            // start angle nor the direction was fixed, so the same move came out differently every
            // time depending on what happened during it.
            //
            // Frozen rather than latched into a copy: the residual resumes easing from where the
            // swing left it, so the arm settles into the new aim instead of snapping to it on the
            // frame the animation ends. A CHARGE is deliberately not covered - that one is
            // supposed to keep tracking, so the slam lands where the player is facing when it goes.
            // The timing bar's held wind-up tracks too, for the same reason - see HoldSwingStart.
            bool swinging = _attackTimer > 0f && _attackHold <= 0f;

            if (!swinging && Mathf.Abs(aim.x) > MirrorDeadzone) _facingLeft = aim.x < 0f;
            bool left = _facingLeft;
            transform.localScale = new Vector3((left ? -1f : 1f) * _visualScale, _visualScale, 1f);

            // Elevation measured in the mirrored frame, so left and right read the same way.
            // The arms take all of it. Smoothed HERE rather than left instantaneous: it used to be
            // the remainder of an already-eased lean, so the easing came for free; carrying the
            // whole angle, it has to do its own or the hands snap between aims.
            //
            // Clamped well short of 90 for two reasons. Visually, a right-angled arm lays the
            // greatsword flat across the chest and it reads as a permanently held wind-up. More
            // importantly the swing arcs in Animate are tuned as absolute angles - Chop runs -80
            // to +55 - and this offset rides on top of every one of them, so a large aim tilt
            // quietly rotates every attack in the game out of the range it was built for.
            float elevation = Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (!swinging)
                _aimResidual = Mathf.LerpAngle(_aimResidual,
                                               Mathf.Clamp(elevation, -AimRangeNow, AimRangeNow),
                                               1f - Mathf.Exp(-TurnSharpness * Dt));

            // Explicitly cleared, not merely left alone: both carried a lean until now, and a
            // stale rotation would sit on the torso forever once nothing wrote it again.
            if (_torso) _torso.localRotation = Quaternion.identity;
            if (_head) _head.localRotation = Quaternion.identity;
        }
    }
}
