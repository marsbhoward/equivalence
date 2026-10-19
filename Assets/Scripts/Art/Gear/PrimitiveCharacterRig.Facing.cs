using UnityEngine;
using Convergence.Combat;

namespace Convergence.Art.Gear
{
    // ------------------------------------------------------------- turning toward the aim
    //
    // The arena's player only (SetAimViews). Everything else - the hub avatar, previews, shadow
    // echoes - keeps the plain FaceAim: a mirror that flips the instant the aim crosses x = 0 and
    // arms that tilt at most ArmAimRange.
    //
    // The user's calls (2026-10-14, from two reference clips - an idle-game swordsman and a
    // top-down action game):
    //
    //   FOUR VIEWS: the back when aiming up the screen, the face-on view otherwise (this body's
    //   front and side are the same picture - the face-on, mirrored one). The rig chooses from its
    //   own TURNED aim, so a view never changes ahead of the weapon swinging round to it. A
    //   scripted SetFacingAway (a door) still wins while it is held. Before this the back view
    //   was travel-only and combat forced the front, for legibility; that call is reversed.
    //
    //   TURNS, NOT FLIPS: the aim is an ANGLE eased toward its target, so the weapon swings round
    //   and the mirror flips only as the turn passes straight up or down - in the back or face-on
    //   view, where the figure is nearest symmetric. A reversal goes round through the FRONT (the
    //   face turning toward the camera) unless the aim is up there already.
    //
    //   FREE UNTIL THE STRIKE: an ordinary swing lands its damage on the PRESS, along the facing
    //   at that instant (PlayerController.Dispatch). So the drawn swing captures that direction
    //   (_strikeAim), turns to it through the cock, holds it EXACTLY from the cock's end until
    //   the strike is drawn, and is free again for the follow-through. It used to freeze both the
    //   mirror and the aim for the whole swing.
    //
    //   THE WEAPON CARRIES THE AIM: in a swing the WHOLE pose - blade and grips - turns about the
    //   near shoulder by up to SwingAimRange, so a swing aimed straight up lands straight up
    //   (the arms used to tilt 45 and no further, and only the arm, not the grip's orbit). Arcs
    //   are authored as before, at aim 0; this rotates them as a whole.
    public partial class PrimitiveCharacterRig
    {
        /// <summary>Whether this rig chooses its view and turns toward its aim - see the partial's
        /// note. Set by PlayerController for the arena's player.</summary>
        bool _aimViews;

        /// <summary>The aim as an ANGLE, degrees (0 = right, counter-clockwise), eased toward
        /// where the rig is told to look. The mirror, the elevation and the view all read it.</summary>
        float _aimAngle;
        bool _aimAngleKnown;

        /// <summary>Where the current swing's hit went - captured at PlayAttack, held until the
        /// strike is drawn (see "free until the strike").</summary>
        Vector2 _strikeAim;
        bool _strikeAimSet;

        /// <summary>A caller's SetFacingAway(true) - a scripted beat - holding the back view
        /// whatever the aim says.</summary>
        bool _scriptedAway;

        /// <summary>How far a swing's whole pose may turn toward the aim, degrees either side of
        /// level. 90 reaches straight up or down; with the mirror that is every direction.</summary>
        const float SwingAimRange = 90f;

        /// <summary>How fast a swing's cock turns onto the direction its hit went. Fast: the cock
        /// is ~0.07 s, and the turn must be done before the strike holds it exactly.</summary>
        const float StrikeTurnSharpness = 40f;

        /// <summary>A turn larger than this goes round through the FRONT rather than the short way
        /// - a reversal shows the face, not the back of the head.</summary>
        const float TurnThroughFrontFrom = 150f;

        /// <summary>The aim's height (sin of its angle) above which the figure turns its back -
        /// about 50 degrees up. Steeper than the hub's walk (Tuning.Player.FacingAwayDot, 30):
        /// the face-on view already carries a diagonal swing (its old 45-degree tilt), and at 30
        /// a target up and to the side showed the back with the blade drawn behind the body.</summary>
        const float AwayFromY = 0.77f;

        /// <summary>The aim's height below which the back view gives way to the face-on one again
        /// (about 37 degrees) - under <see cref="AwayFromY"/>, so an aim hovering near the line
        /// does not chatter between the two.</summary>
        const float FrontFromY = 0.6f;

        public void SetAimViews(bool on)
        {
            if (_aimViews == on) return;
            _aimViews = on;
            _aimAngleKnown = false;
            // Handing views back: whatever the aim chose is undone unless a script is holding it.
            if (!on && _facingAway && !_scriptedAway) ApplyFacingAway(false);
        }

        public bool PastStrike
        {
            get
            {
                if (_attackTimer <= 0f || _attackHold > 0f) return true;
                StrikePhases(_motion, _attackLength, out _, out float strikeEnd);
                return SwingKNow >= strikeEnd;
            }
        }

        public float StrikeDrawnSeconds(AttackMotion motion, float duration)
        {
            float length = AttackMotions.SwingSeconds(duration);
            StrikePhases(motion, length, out _, out float strikeEnd);
            return strikeEnd * length;
        }

        public float StrikeStartSeconds(AttackMotion motion, float duration)
        {
            float length = AttackMotions.SwingSeconds(duration);
            StrikePhases(motion, length, out float cockEnd, out _);
            return cockEnd * length;
        }

        /// <summary>0..1 through the live swing.</summary>
        float SwingKNow => 1f - Mathf.Clamp01(_attackTimer / Mathf.Max(0.0001f, _attackLength));

        /// <summary>
        /// Where the motion's cock ends and its strike is fully drawn, as fractions of the swing -
        /// the same boundaries its body uses (BodyFor). A spin and a bow's draw hold to the end
        /// (the spin hits all the way round, the arrow leaves at the end of the pull); anything
        /// unlisted turns onto the strike over the entry blend and holds it for the whole swing.
        /// </summary>
        static void StrikePhases(AttackMotion motion, float seconds, out float cockEnd, out float strikeEnd)
        {
            switch (motion)
            {
                case AttackMotion.Chop: ChopPhases(seconds, out cockEnd, out strikeEnd); return;
                case AttackMotion.Rise: RisePhases(seconds, out cockEnd, out strikeEnd); return;
                case AttackMotion.Wind: WindPhases(seconds, out cockEnd, out strikeEnd); return;
                case AttackMotion.Impale: ImpalePhases(seconds, out cockEnd, out strikeEnd); return;
                case AttackMotion.Sweep: cockEnd = SweepCockK; strikeEnd = SweepStrikeK; return;
                case AttackMotion.Thrust:
                case AttackMotion.Jab: cockEnd = ThrustCockK; strikeEnd = ThrustStrikeK; return;
                case AttackMotion.Lunge: cockEnd = 0f; strikeEnd = LungeStrikeK(seconds); return;
                case AttackMotion.Spin: cockEnd = 0.06f; strikeEnd = 1f; return;
                case AttackMotion.WhirlThrow: cockEnd = 0.05f; strikeEnd = 0.22f; return;
                case AttackMotion.Slam: cockEnd = 0f; strikeEnd = SlamStrikeK; return;
                case AttackMotion.Plant: cockEnd = PlantCockK; strikeEnd = PlantStrikeK; return;
                case AttackMotion.Throw:
                case AttackMotion.Cast: cockEnd = ThrowCockK; strikeEnd = ThrowStrikeK; return;
                case AttackMotion.Draw:
                case AttackMotion.KneelDraw:
                case AttackMotion.RecoilDraw:
                case AttackMotion.LobDraw:
                case AttackMotion.HighDraw:
                case AttackMotion.SnapDraw:
                case AttackMotion.CantedDraw:
                case AttackMotion.TwinDraw:
                case AttackMotion.FlipDraw: cockEnd = DrawCockK; strikeEnd = 1f; return;
                case AttackMotion.BowKneelRoll:
                case AttackMotion.BowRecoilHop: cockEnd = 0f; strikeEnd = 1f; return;
                case AttackMotion.Flurry:
                    cockEnd = Core.Tuning.Flurry.CutOneAt / Core.Tuning.Flurry.Seconds;
                    strikeEnd = Mathf.Min(1f, (Core.Tuning.Flurry.PommelAt + 0.03f) / Core.Tuning.Flurry.Seconds);
                    return;
                default:
                {
                    var combo = ComboOf(motion);
                    if (combo != null) { ComboPhases(combo, seconds, out cockEnd, out strikeEnd); return; }
                    cockEnd = Mathf.Clamp01(EntryBlendSeconds / Mathf.Max(0.05f, seconds));
                    strikeEnd = 1f;
                    return;
                }
            }
        }

        // ---- staging a swing aimed steeply up or down ----
        //
        // The side-on arcs are drawn in the plane of the facing; turned whole by the aim (above),
        // one aimed straight up tipped onto its side - a Chop became an uppercut climbing one side
        // of the body. Aimed past CentredFromDegrees, a swing is CENTRED instead: its whole pose is
        // turned so the middle of the blade's angular range points along the aim. A wide arc then
        // sweeps ACROSS the top (or the bottom) of the figure - the top-down read - and a narrow
        // one (a thrust, the Wind's draw) points straight at the aim, so the one rule serves both.
        // Decided once, at PlayAttack, off the direction the hit went, and kept for the swing.
        //
        // Spin (a revolution - see SpinTurning), Plant (aimed at the ground), Draw (the bow's two
        // hands do different jobs) and the throws (the release direction is the arm's, not the
        // blade's) keep the aim's own turn. So do the OVERHEAD cuts (Chop, Rise, Slam) aimed DOWN:
        // turned whole they come from over the head down past the feet - a chop toward the camera,
        // which reads - where centred they ended with the hands up by one shoulder. Aimed up, a
        // chop away from the camera has nothing to show turned (it tipped onto its side), so up
        // is always centred.

        /// <summary>The aim, in degrees above or below level, past which a swing is centred.
        /// Matches the back view's line (AwayFromY) going up.</summary>
        const float CentredFromDegrees = 50f;

        /// <summary>Whether the current swing is centred, and the turn its pose takes if so.</summary>
        bool _centred;
        float _centredTurn;

        /// <summary>The turn the swing pose took last frame, and when the current swing began -
        /// a swing blends its turn in over the entry window like the rest of its pose.</summary>
        float _poseTurn, _entryPoseTurn;

        static bool IsOverheadCut(AttackMotion m) =>
            m == AttackMotion.Chop || m == AttackMotion.Rise || m == AttackMotion.Slam
            || m == AttackMotion.CrescentCut || m == AttackMotion.ReturnChop
            || m == AttackMotion.SunderSlam;

        static bool CentresWhenSteep(AttackMotion m) =>
            m != AttackMotion.Spin && m != AttackMotion.Plant && m != AttackMotion.Draw
            && m != AttackMotion.KneelDraw && m != AttackMotion.BowKneelRoll
            && m != AttackMotion.RecoilDraw && m != AttackMotion.BowRecoilHop
            && m != AttackMotion.LobDraw && m != AttackMotion.HighDraw
            && m != AttackMotion.SnapDraw && m != AttackMotion.CantedDraw && m != AttackMotion.TwinDraw
            && m != AttackMotion.FlipDraw
            && m != AttackMotion.Throw && m != AttackMotion.Cast && m != AttackMotion.WhirlThrow
            && !ComboTurns(m);

        /// <summary>Called by PlayAttack, after the strike's direction is captured.</summary>
        void DecideStaging(AttackMotion motion, bool alt, float seconds)
        {
            _entryPoseTurn = _poseTurn;
            _centred = false;
            if (!_aimViews || !_strikeAimSet || HeldBow || !CentresWhenSteep(motion)) return;
            float elevation = Mathf.Asin(Mathf.Clamp(_strikeAim.y, -1f, 1f)) * Mathf.Rad2Deg;
            if (Mathf.Abs(elevation) < CentredFromDegrees) return;
            if (elevation < 0f && IsOverheadCut(motion)) return;

            // The blade's angular range, off the motion itself (blade = arm + the waist's turn).
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i <= 16; i++)
            {
                float arm = 0f, back = 0f, body = 0f;
                var reach = Vector3.zero;
                Animate(motion, i / 16f, alt, _facingLeft, seconds, ref arm, ref back, ref body, ref reach);
                float blade = arm + body;
                lo = Mathf.Min(lo, blade);
                hi = Mathf.Max(hi, blade);
            }
            float tipAlong = elevation > 0f ? 0f : 180f;   // 0 is tip up, 180 tip down
            _centredTurn = Mathf.DeltaAngle(0f, tipAlong - (lo + hi) * 0.5f);
            _centred = true;
        }

        /// <summary>
        /// The turn the swing's whole pose takes this frame: the aim's own (<paramref name="aim"/>)
        /// or a centred swing's, blended in over the entry window as a swing starts and eased back
        /// to the aim once the swing and its held follow-through are over.
        /// </summary>
        float PoseTurnNow(float aim, float dt)
        {
            if (!_aimViews) return _poseTurn = aim;
            bool held = _sinceSwing < CarryReturnDelay && HeldTwoHanded;
            if (_attackTimer > 0f)
            {
                float swingTurn = _centred ? _centredTurn : aim;
                float elapsed = _attackLength - _attackTimer + _holdElapsed;
                float e = Mathf.Clamp01(elapsed / EntryBlendSeconds);
                e = 1f - (1f - e) * (1f - e);
                _poseTurn = Mathf.LerpAngle(_entryPoseTurn, swingTurn, e);
            }
            else if (_centred && held)
                _poseTurn = _centredTurn;
            else
                _poseTurn = Mathf.LerpAngle(_poseTurn, aim, 1f - Mathf.Exp(-PoseRecoverySharpness * dt));
            return _poseTurn;
        }

        /// <summary>What the rig is told to look at: an explicit facing, the player's, else the
        /// body's travel.</summary>
        Vector2 AimSource() =>
            _facingOverride ?? (Player != null ? Player.Facing
                              : Body != null ? (Vector2)Body.linearVelocity
                              : Vector2.zero);

        /// <summary>Called by PlayAttack: the hit has just gone this way.</summary>
        void CaptureStrikeAim()
        {
            var a = AimSource();
            _strikeAimSet = a.sqrMagnitude >= 0.0025f;
            if (_strikeAimSet) _strikeAim = a.normalized;
        }

        /// <summary>
        /// The signed turn from <paramref name="from"/> to <paramref name="to"/>, degrees: the
        /// short way, except a reversal (over <see cref="TurnThroughFrontFrom"/>) goes whichever
        /// way passes LOWER on the screen - through the face-on view.
        /// </summary>
        static float TurnDelta(float from, float to)
        {
            float d = Mathf.DeltaAngle(from, to);
            if (Mathf.Abs(d) < TurnThroughFrontFrom) return d;
            float other = d - 360f * Mathf.Sign(d);
            float midShort = Mathf.Sin((from + d * 0.5f) * Mathf.Deg2Rad);
            float midLong = Mathf.Sin((from + other * 0.5f) * Mathf.Deg2Rad);
            return midShort <= midLong ? d : other;
        }

        /// <summary>
        /// FaceAim for a rig with <see cref="_aimViews"/>: the turned aim, the strike lock, the
        /// mirror, the elevation the arms carry, and the view. <paramref name="live"/> is this
        /// frame's normalised aim.
        /// </summary>
        void FaceAimTurning(Vector2 live)
        {
            bool swinging = _attackTimer > 0f && _attackHold <= 0f;
            float cockEnd = 0f, strikeEnd = 0f, k = 0f;
            if (swinging)
            {
                StrikePhases(_motion, _attackLength, out cockEnd, out strikeEnd);
                k = SwingKNow;
            }

            // A Spin TURNS the figure a full revolution about its own axis (SpinTurning), and so
            // does a dimensional basic that carries TURN (the Combos partial).
            if (swinging && _motion == AttackMotion.Spin && _strikeAimSet)
            {
                SpinTurning(k);
                return;
            }
            // The bow's roll to the knee: a full turn, ending back on the target.
            if (swinging && _motion == AttackMotion.BowKneelRoll && _strikeAimSet)
            {
                TurnBy(360f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / BowRollTurnEnd)));
                return;
            }
            if (swinging && _strikeAimSet && ComboTurns(_motion))
            {
                ComboDimensions(ComboOf(_motion), k, _attackLength, out _, out float turn, out _);
                TurnBy(turn);
                return;
            }

            // Until the drawn strike is over, it goes where the press already landed the hit:
            // turning onto it through the cock, exactly on it after.
            bool locked = swinging && _strikeAimSet && k < strikeEnd;
            bool exact = locked && k >= cockEnd;
            Vector2 target = locked ? _strikeAim : live;

            float want = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg;
            if (!_aimAngleKnown) { _aimAngle = want; _aimAngleKnown = true; }
            float sharpness = locked ? StrikeTurnSharpness : TurnSharpness;
            float ease = 1f - Mathf.Exp(-sharpness * Dt);
            _aimAngle = exact ? want : _aimAngle + TurnDelta(_aimAngle, want) * ease;
            _aimAngle = Mathf.Repeat(_aimAngle + 180f, 360f) - 180f;

            var dir = new Vector2(Mathf.Cos(_aimAngle * Mathf.Deg2Rad), Mathf.Sin(_aimAngle * Mathf.Deg2Rad));

            // The mirror follows the TURNED aim, so it flips as the turn passes straight up or
            // down rather than the instant the target crosses over.
            if (Mathf.Abs(dir.x) > MirrorDeadzone) _facingLeft = dir.x < 0f;
            transform.localScale = new Vector3((_facingLeft ? -1f : 1f) * _visualScale, _visualScale, 1f);

            // A swing turns its whole pose up to SwingAimRange - and keeps it through the held
            // follow-through between two swings of a chain (CarryReturnDelay), or a strike aimed
            // straight up sagged 45 degrees the moment its clock ran out. At rest the arms keep
            // the old tilt limit (the carry ignores aim anyway). Not a bow: its two hands do
            // different jobs, and the bow arm takes only a share of the aim.
            bool swingPose = swinging || (_sinceSwing < CarryReturnDelay && HeldTwoHanded);
            float elevation = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            float range = swingPose && !HeldBow ? Mathf.Max(SwingAimRange, AimRangeNow) : AimRangeNow;
            float clamped = Mathf.Clamp(elevation, -range, range);
            _aimResidual = exact ? clamped : Mathf.LerpAngle(_aimResidual, clamped, ease);

            // The view, from the same turned aim - with a band between the two thresholds so an
            // aim near the line holds what it has - or from a dimensional basic turning its back
            // to the camera (the Combos partial's AWAY).
            if (!_scriptedAway)
            {
                bool aimAway = dir.y > AwayFromY ? true
                             : dir.y < FrontFromY ? false
                             : _facingAway;
                bool turnedAway = _comboAway >= ComboAwayOn || (_facingAway && _comboAway > ComboAwayOff);
                bool away = aimAway || turnedAway;
                if (away != _facingAway) ApplyFacingAway(away);
            }
        }

        /// <summary>
        /// A Spin as a TURN, not a roll: the aim itself makes the full revolution, starting from
        /// the direction the hit went and going round through the FRONT first, and the mirror, the
        /// arms' elevation and the view follow it as they do any turn - so the figure stays upright
        /// and shows its face, its other side, its back, and its face again, while the blade swings
        /// right round it, in front of the body coming toward the camera and behind it going away.
        /// The old Spin rolled the whole figure about its feet in the picture plane, upside down
        /// halfway through (the motion's bodySpin, which a turning rig now ignores). The user's
        /// idle-game reference turns this way.
        /// </summary>
        void SpinTurning(float k) => TurnBy(360f * Mathf.SmoothStep(0f, 1f, k));

        /// <summary>The figure turned <paramref name="degrees"/> round from the strike's direction,
        /// through the FRONT first - SpinTurning's turn, for any amount.</summary>
        void TurnBy(float degrees)
        {
            float start = Mathf.Atan2(_strikeAim.y, _strikeAim.x) * Mathf.Rad2Deg;
            float sense = Mathf.Cos(start * Mathf.Deg2Rad) >= 0f ? -1f : 1f;   // through the front
            _aimAngle = start + sense * degrees;
            _aimAngle = Mathf.Repeat(_aimAngle + 180f, 360f) - 180f;
            _aimAngleKnown = true;

            var dir = new Vector2(Mathf.Cos(_aimAngle * Mathf.Deg2Rad), Mathf.Sin(_aimAngle * Mathf.Deg2Rad));
            if (Mathf.Abs(dir.x) > MirrorDeadzone) _facingLeft = dir.x < 0f;
            transform.localScale = new Vector3((_facingLeft ? -1f : 1f) * _visualScale, _visualScale, 1f);
            _aimResidual = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg,
                                       -SwingAimRange, SwingAimRange);
            if (!_scriptedAway)
            {
                // A turning combo may also hold the figure turned AWAY (Riposte's Turnabout leaves
                // the hanging guard with its back to the camera before the turn's aim gets there).
                bool aimAway = dir.y > AwayFromY ? true : dir.y < FrontFromY ? false : _facingAway;
                bool turnedAway = _comboAway >= ComboAwayOn || (_facingAway && _comboAway > ComboAwayOff);
                bool away = aimAway || turnedAway;
                if (away != _facingAway) ApplyFacingAway(away);
            }
        }

        /// <summary>Whether this frame's Spin is a turn (SpinTurning) - its bodySpin roll is then
        /// dropped, and nothing turns the root.</summary>
        bool SpinIsTurn => _aimViews && _motion == AttackMotion.Spin;

        /// <summary>How much of a swing's lean shows: all of it aimed level, none aimed straight
        /// up or down - a lean toward the camera or away from it has nothing to show in 2D.</summary>
        float LeanShare => _aimViews ? Mathf.Abs(Mathf.Cos(_aimResidual * (1f - _aimSuppress) * Mathf.Deg2Rad)) : 1f;

        /// <summary>The direction a swing's drawn step travels, in the mirrored frame: along the
        /// facing, turned up or down with the aim.</summary>
        Vector2 StepDirection
        {
            get
            {
                if (!_aimViews) return Vector2.right;
                float a = _aimResidual * (1f - _aimSuppress) * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
        }
    }
}
