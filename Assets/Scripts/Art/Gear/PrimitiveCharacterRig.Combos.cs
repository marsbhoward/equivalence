using UnityEngine;
using Convergence.Combat;

namespace Convergence.Art.Gear
{
    // ------------------------------------------------------------- the dimensional basics
    //
    // Five two-hit basic combos (the user's brief, 2026-10-14; the names are mine), each authored
    // as KEYFRAMES that use the rig's dimensions rather than the arms alone:
    //
    //   the BODY      crouch, split stance, step, lean (SwingBody - the Body partial)
    //   AWAY          degrees turned away from the camera - past ComboAwayOn the figure shows its
    //                 BACK (the back view), the pose itself staying as authored
    //   TURN          a full revolution through the views, the pose turning with it (SpinTurning)
    //   BEHIND        the blade drawn behind the body (the front view's WeaponBehindOrder)
    //
    // Each ends in a different CONNECTOR pose, so the weapon arts can each start from one:
    //
    //   Crescent  HIGH-BACK, turned away    Cyclone  LOW-FORWARD, crouched after a full turn
    //   Undertow  LOW-BACK, blade behind    Ascent   OVERHEAD (the charge's raised pose)
    //   Return    GUARD, blade level and thrust-ready
    //
    // A key's BLADE is the WORLD angle (0 tip up, -90 along the facing, +90 behind, +-180 down),
    // unwrapped within a motion; the arm takes it less the waist's turn. The FIST is the weapon
    // fist against the weapon shoulder, +X forward - the frame every other motion's fists use
    // (ThrustGuardFist, SweepHighFist...). Keys sit on a PHASE axis: 0..1 the cock, 1..2 the
    // strike, 2..3 the settle; each phase is eased as a whole (the cock out, the strike and the
    // settle in-out) and keys inside one are linear in it, so a strike drawn through several keys
    // never stops at them.
    public partial class PrimitiveCharacterRig
    {
        struct ComboKey
        {
            public float P, Blade, Waist, Back, Away, Turn;
            public Vector2 Fist;
            /// <summary>The OFF fist against the off shoulder, +X forward - a disc's second hand
            /// (OffFistFor). NaN: none, the off arm takes BACK as a shoulder angle.</summary>
            public Vector2 Off;
            public SwingBody Body;
            public bool Behind;
            /// <summary>The OFF arm and its disc drawn IN FRONT of the body, the main arm and its
            /// disc behind (an X whose second stroke crosses over the first).</summary>
            public bool OffFront;
        }

        sealed class ComboMotion
        {
            public float CockSeconds, StrikeSeconds;
            /// <summary>The longest share of the swing the strike may take (Carousel's two turns
            /// need most of it).</summary>
            public float MaxStrike = 0.55f;
            public ComboKey[] Keys;
        }

        static ComboKey K(float p, float blade, float fx, float fy, float waist, SwingBody body,
                          float away = 0f, float turn = 0f, bool behind = false, float back = 15f,
                          float ox = float.NaN, float oy = float.NaN, bool offFront = false)
            => new() { P = p, Blade = blade, Fist = new Vector2(fx, fy), Waist = waist, Body = body,
                       Away = away, Turn = turn, Behind = behind, Back = back, Off = new Vector2(ox, oy),
                       OffFront = offFront };

        static SwingBody B(float crouch, float stance, float step, float lean) => new(crouch, stance, step, lean);

        /// <summary>Turned away past this many degrees, the figure shows its back; it turns
        /// round again under ComboAwayOff.</summary>
        const float ComboAwayOn = 60f, ComboAwayOff = 40f;

        // Built on first use, never at static init (cross-file static order) - and rebuilt after
        // a domain reload empties it.
        static ComboMotion[] _combos;

        static ComboMotion ComboOf(AttackMotion m)
        {
            int i = (int)m - (int)AttackMotion.CrescentCut;
            if (i < 0) return null;   // a null slot (Cast) is not a combo either
            if (_combos == null || _combos.Length == 0) _combos = BuildCombos();
            return i < _combos.Length ? _combos[i] : null;
        }

        static bool IsCombo(AttackMotion m) => ComboOf(m) != null;

        static ComboMotion[] BuildCombos() => new[]
        {
            // ---- Crescent ----
            // A diagonal descending cut out of a coil behind the head...
            new ComboMotion { CockSeconds = 0.12f, StrikeSeconds = 0.11f, Keys = new[]
            {
                K(0.0f,   30f,  0.06f,  0.10f,   0f, B(0f, 0f, 0f, 0f)),
                K(1.0f,   70f, -0.02f,  0.17f,   8f, B(0.025f, 0.040f, -0.015f, 5f)),
                K(1.5f,  -40f,  0.12f,  0.10f,  -6f, B(0.050f, 0.065f, 0.040f, -8f)),
                K(2.0f, -150f,  0.16f, -0.09f, -12f, B(0.068f, 0.078f, 0.065f, -12f)),
                K(3.0f, -140f,  0.15f, -0.08f,  -8f, B(0.052f, 0.070f, 0.056f, -8f)),
            }},
            // ...then a rising backhand that turns the body AWAY as the blade comes over the top
            // and down behind the far shoulder. Ends HIGH-BACK, turned away.
            new ComboMotion { CockSeconds = 0.08f, StrikeSeconds = 0.14f, Keys = new[]
            {
                K(0.0f, -140f,  0.15f, -0.08f,  -8f, B(0.052f, 0.070f, 0.056f, -8f)),
                K(1.0f, -160f,  0.13f, -0.11f, -10f, B(0.065f, 0.075f, 0.050f, -6f)),
                K(1.4f,  -60f,  0.16f,  0.06f,   0f, B(0.030f, 0.065f, 0.050f, 2f), away: 30f),
                K(1.7f,   10f,  0.04f,  0.18f,  10f, B(0.010f, 0.060f, 0.035f, 6f), away: 75f),
                K(2.0f,   95f, -0.05f,  0.15f,  16f, B(0.020f, 0.055f, 0.020f, 8f), away: 80f),
                K(3.0f,  105f, -0.06f,  0.14f,  14f, B(0.026f, 0.052f, 0.015f, 8f), away: 70f),
            }},

            // ---- Cyclone ----
            // A flat cut from behind the hip: the blade starts BEHIND the body pointing back,
            // swings round toward the camera - in front of the body - and out along the facing.
            new ComboMotion { CockSeconds = 0.11f, StrikeSeconds = 0.12f, Keys = new[]
            {
                K(0.0f,   60f,  0.00f,  0.05f,   0f, B(0f, 0f, 0f, 0f)),
                K(1.0f,  115f, -0.07f, -0.02f,  12f, B(0.030f, 0.050f, -0.020f, 6f), behind: true),
                K(1.35f, 160f,  0.02f, -0.07f,   6f, B(0.045f, 0.062f, 0.005f, 2f), behind: true),
                K(1.5f,  180f,  0.08f, -0.08f,   0f, B(0.055f, 0.070f, 0.030f, -4f)),
                K(2.0f,  255f,  0.18f, -0.04f, -14f, B(0.060f, 0.075f, 0.060f, -10f)),
                K(3.0f,  265f,  0.17f, -0.05f, -10f, B(0.050f, 0.068f, 0.050f, -7f)),
            }},
            // ...then a full TURN through the views (face, side, back, face) with the blade held
            // out low, the pose turning with the body. Ends LOW-FORWARD, crouched.
            new ComboMotion { CockSeconds = 0.07f, StrikeSeconds = 0.24f, Keys = new[]
            {
                K(0.0f,  -95f,  0.17f, -0.05f, -10f, B(0.050f, 0.068f, 0.050f, -7f)),
                K(1.0f,  -95f,  0.15f, -0.06f,  -4f, B(0.060f, 0.070f, 0.020f, -2f)),
                K(2.0f, -100f,  0.16f, -0.07f,   0f, B(0.070f, 0.080f, 0.040f, -6f), turn: 360f),
                K(3.0f, -115f,  0.15f, -0.09f,   0f, B(0.072f, 0.082f, 0.050f, -8f), turn: 360f),
            }},

            // ---- Undertow ----
            // A stepping thrust out of a drawn-back chamber...
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.09f, Keys = new[]
            {
                K(0.0f,  -60f,  0.10f, -0.02f,   0f, B(0f, 0f, 0f, 0f)),
                K(1.0f,  -95f,  0.02f, -0.05f,  10f, B(0.025f, 0.045f, -0.020f, 5f)),
                K(2.0f,  -90f,  0.38f, -0.02f, -14f, B(0.050f, 0.090f, 0.080f, -10f)),
                K(3.0f,  -92f,  0.30f, -0.03f,  -8f, B(0.035f, 0.070f, 0.050f, -5f)),
            }},
            // ...then the blade dragged back down through the target, past the body and BEHIND it,
            // the shoulders coiling away. Ends LOW-BACK, the blade behind the rear hip.
            new ComboMotion { CockSeconds = 0.08f, StrikeSeconds = 0.13f, Keys = new[]
            {
                K(0.0f,  -92f,  0.30f, -0.03f,  -8f, B(0.035f, 0.070f, 0.050f, -5f)),
                K(1.0f, -110f,  0.26f, -0.05f, -10f, B(0.040f, 0.072f, 0.050f, -6f)),
                K(1.5f, -160f,  0.08f, -0.08f,   4f, B(0.060f, 0.075f, 0.010f, 2f)),
                K(1.75f,-200f, -0.04f, -0.07f,  12f, B(0.065f, 0.075f, -0.010f, 6f), behind: true),
                K(2.0f, -225f, -0.08f, -0.05f,  16f, B(0.062f, 0.072f, -0.012f, 7f), behind: true),
                K(3.0f, -230f, -0.08f, -0.05f,  15f, B(0.058f, 0.070f, -0.010f, 7f), behind: true),
            }},

            // ---- Ascent ----
            // An underhand rising cut: from low BEHIND the body, under and out in front, up along
            // the facing...
            new ComboMotion { CockSeconds = 0.11f, StrikeSeconds = 0.12f, Keys = new[]
            {
                K(0.0f,  120f, -0.03f,  0.02f,   0f, B(0f, 0f, 0f, 0f)),
                K(1.0f,  150f, -0.07f, -0.06f,  12f, B(0.045f, 0.060f, -0.015f, 6f), behind: true),
                K(1.4f,  200f,  0.06f, -0.10f,   0f, B(0.060f, 0.070f, 0.030f, -2f)),
                K(2.0f,  300f,  0.17f,  0.07f, -10f, B(0.020f, 0.065f, 0.055f, -6f)),
                K(3.0f,  310f,  0.16f,  0.08f,  -8f, B(0.025f, 0.060f, 0.050f, -4f)),
            }},
            // ...then a PIVOT: the figure turns its back as the blade comes up over the head, and
            // round again with it raised behind. Ends OVERHEAD - the charge's own hold
            // (ChargeFist, ChargeArmSwing), so a charged art starts where this leaves off.
            new ComboMotion { CockSeconds = 0.07f, StrikeSeconds = 0.16f, Keys = new[]
            {
                K(0.0f,  -50f,  0.16f,  0.08f,  -8f, B(0.025f, 0.060f, 0.050f, -4f)),
                K(1.0f,  -40f,  0.14f,  0.10f, -10f, B(0.035f, 0.065f, 0.050f, -5f)),
                K(1.5f,    0f,  0.03f,  0.19f,   0f, B(0.015f, 0.060f, 0.020f, 0f), away: 90f),
                K(2.0f,   35f, -0.03f,  0.18f,  10f, B(0.030f, 0.060f, -0.010f, 5f), away: 30f),
                K(3.0f,   40f, -0.03f,  0.17f,  10f, B(0.032f, 0.058f, -0.012f, 5f)),
            }},

            // ---- Return ----
            // The reference's own opening: the wind-up turns the BACK to the camera with the blade
            // cocked behind the head, then the body turns back into the chop...
            new ComboMotion { CockSeconds = 0.14f, StrikeSeconds = 0.11f, Keys = new[]
            {
                K(0.0f,   30f,  0.04f,  0.12f,   0f, B(0f, 0f, 0f, 0f)),
                K(0.6f,   60f, -0.02f,  0.17f,   8f, B(0.020f, 0.040f, -0.012f, 4f), away: 70f),
                K(1.0f,   85f, -0.04f,  0.16f,  12f, B(0.030f, 0.050f, -0.015f, 6f), away: 70f),
                K(1.4f,  -30f,  0.10f,  0.12f,  -4f, B(0.050f, 0.070f, 0.040f, -8f), away: 10f),
                K(2.0f, -155f,  0.15f, -0.10f, -14f, B(0.070f, 0.080f, 0.068f, -13f)),
                K(3.0f, -145f,  0.15f, -0.09f, -10f, B(0.055f, 0.072f, 0.058f, -9f)),
            }},
            // ...then a backhand up across the body and back behind the head, and the blade
            // brought back over and down to LEVEL. Ends in the GUARD, thrust-ready.
            new ComboMotion { CockSeconds = 0.08f, StrikeSeconds = 0.12f, Keys = new[]
            {
                K(0.0f, -145f,  0.15f, -0.09f, -10f, B(0.055f, 0.072f, 0.058f, -9f)),
                K(1.0f, -165f,  0.13f, -0.11f, -12f, B(0.060f, 0.074f, 0.050f, -8f)),
                K(1.5f,  -40f,  0.16f,  0.08f,   0f, B(0.040f, 0.065f, 0.040f, 0f)),
                K(2.0f,   60f,  0.00f,  0.15f,  12f, B(0.025f, 0.060f, 0.030f, 6f), behind: true),
                K(2.5f,  -20f,  0.08f,  0.06f,   4f, B(0.025f, 0.060f, 0.025f, 0f)),
                K(3.0f,  -97f,  0.12f, -0.06f,   0f, B(0.025f, 0.060f, 0.020f, -2f)),
            }},

            // ---- the weapon arts that start on a connector ----

            // SunderSlam (Sunder's Shatter): starts EXACTLY on Ascent's OVERHEAD end, so the
            // timing bar's held first frame is the pose the lead-in left. The cock rises onto the
            // toes with the blade drawn a little further back; the strike drives it over the top
            // and down into the ground in front - the tip planted, the fists low, the hips dropped
            // into the deepest crouch and widest stance of any swing, the chest leaning in. The
            // settle stays planted: the ground took the blow.
            new ComboMotion { CockSeconds = 0.06f, StrikeSeconds = 0.09f, Keys = new[]
            {
                K(0.0f,   40f, -0.03f,  0.17f,  10f, B(0.032f, 0.058f, -0.012f, 5f)),
                K(1.0f,   55f, -0.05f,  0.20f,  12f, B(-0.012f, 0.040f, -0.015f, 6f)),
                K(1.45f, -45f,  0.12f,  0.13f,  -2f, B(0.040f, 0.075f, 0.040f, -6f)),
                K(2.0f, -165f,  0.15f, -0.13f, -14f, B(0.090f, 0.092f, 0.072f, -16f)),
                K(3.0f, -162f,  0.15f, -0.12f, -12f, B(0.080f, 0.088f, 0.066f, -13f)),
            }},

            // Cast is a hand-posed motion (Throw's), not keyframed: its slot is empty.
            null,

            // StarLeap (Falling Star's takeoff): starts EXACTLY on Cyclone's crouched end. The
            // cock sinks deeper with the blade drawn back low; the strike is a full TURN through
            // the views as the legs drive off the ground (the leap lifts the drawing - see
            // PlayerController.LeapStrike), the blade swept round and up overhead, the legs
            // gathering under; the settle holds SunderSlam's first frame for the flight, so the
            // landing's slam starts where this leaves off.
            new ComboMotion { CockSeconds = 0.08f, StrikeSeconds = 0.36f, Keys = new[]
            {
                K(0.0f, -115f,  0.15f, -0.09f,   0f, B(0.072f, 0.082f, 0.050f, -8f)),
                K(1.0f, -105f,  0.12f, -0.11f,  -4f, B(0.095f, 0.080f, 0.025f, -4f)),
                K(1.5f,  -45f,  0.15f,  0.04f,   0f, B(0.010f, 0.035f, 0.000f, 0f), turn: 180f),
                K(2.0f,   40f, -0.03f,  0.17f,  10f, B(0.032f, 0.058f, -0.012f, 5f), turn: 360f),
                K(3.0f,   40f, -0.03f,  0.17f,  10f, B(0.032f, 0.058f, -0.012f, 5f), turn: 360f),
            }},

            // ---- Riposte (2026-10-14): both start on Crescent's HIGH-BACK, turned-away end -
            // the HANGING GUARD, held through the timing bar. ----

            // Turnabout: the BODY turns a full revolution counter-clockwise out of the guard -
            // through its back, its far side, its face, round to the attacker (TURN, the views
            // following, as a spin's do) - and leads: the blade TRAILS behind it through the turn,
            // then whips across the front of the body point-down with the fist held high (the tip
            // above the feet, never under them) and out level along the facing. The player steps
            // in with the turn (the move carries the body toward the parry - PlayerController).
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.26f, Keys = new[]
            {
                K(0.0f,  105f, -0.06f,  0.14f,  14f, B(0.026f, 0.052f, 0.015f, 8f), away: 70f),
                K(1.0f,  115f, -0.08f,  0.12f,  18f, B(0.045f, 0.058f, -0.005f, 8f), away: 60f, turn: -50f),
                K(1.35f, 130f, -0.07f,  0.16f,  12f, B(0.040f, 0.065f, 0.020f, 4f), turn: -150f),
                K(1.6f,  165f,  0.00f,  0.24f,   4f, B(0.045f, 0.070f, 0.040f, -2f), turn: -250f),
                K(1.8f,  220f,  0.12f,  0.18f,  -6f, B(0.055f, 0.078f, 0.055f, -8f), turn: -320f),
                K(2.0f,  270f,  0.20f,  0.06f, -14f, B(0.060f, 0.085f, 0.070f, -12f), turn: -360f),
                K(3.0f,  275f,  0.19f,  0.05f, -10f, B(0.050f, 0.075f, 0.060f, -8f), turn: -360f),
            }},
            // Return (nothing was parried): the figure turns back CLOCKWISE out of the guard -
            // its back coming round to the side - the blade coming round over with it and settling
            // high in front. No swing: the art was the guard.
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.22f, Keys = new[]
            {
                K(0.0f,  105f, -0.06f,  0.14f,  14f, B(0.026f, 0.052f, 0.015f, 8f), away: 70f),
                K(1.0f,  110f, -0.07f,  0.14f,  16f, B(0.030f, 0.050f, 0.010f, 8f), away: 75f),
                K(1.5f,   80f, -0.04f,  0.16f,   8f, B(0.020f, 0.045f, 0.005f, 4f), away: 45f),
                K(2.0f,   45f, -0.02f,  0.15f,   2f, B(0.015f, 0.035f, 0.000f, 2f)),
                K(3.0f,   40f, -0.02f,  0.15f,   0f, B(0.010f, 0.030f, 0.000f, 0f)),
            }},

            // ---- disc RANGED basics, MOCK (2026-10-15). For a disc the BLADE channel is the held
            // disc's turn and the FIST does the work; BACK is the OFF arm (its disc in that hand),
            // positive swinging it forward. ----

            // Pirouette 1: a full turn through the front, the throwing arm out behind and trailing,
            // whipped through as the body comes back round - released at the strike's end. The
            // off arm balances: out in front through the turn, pulled in to the chest on the throw.
            new ComboMotion { CockSeconds = 0.08f, StrikeSeconds = 0.30f, Keys = new[]
            {
                K(0.0f,    0f,  0.02f, -0.10f,   0f, B(0f, 0f, 0f, 0f), back: 15f, ox: 0.00f, oy: -0.12f),
                K(1.0f,   20f, -0.12f, -0.02f,  12f, B(0.045f, 0.050f, -0.010f, 4f), back: -20f, ox: 0.10f, oy: -0.04f),
                K(1.3f,   30f, -0.16f,  0.02f,  10f, B(0.030f, 0.040f, 0.000f, 0f), back: 30f, turn: 120f, ox: 0.15f, oy: 0.02f),
                K(1.65f,  40f, -0.14f,  0.05f,  14f, B(0.025f, 0.045f, 0.010f, 2f), back: 40f, turn: 260f, ox: 0.14f, oy: 0.04f),
                K(1.85f, -40f,  0.05f,  0.08f,  -6f, B(0.040f, 0.070f, 0.050f, -8f), back: -10f, turn: 340f, ox: 0.02f, oy: 0.00f),
                K(2.0f,  -80f,  0.22f,  0.04f, -14f, B(0.050f, 0.080f, 0.065f, -12f), back: -30f, turn: 360f, ox: -0.06f, oy: -0.04f),
                K(3.0f,  -75f,  0.20f,  0.02f, -10f, B(0.040f, 0.070f, 0.050f, -8f), back: -20f, turn: 360f, ox: -0.05f, oy: -0.06f),
            }},
            // Pirouette 2: the counter-turn, through the BACK the other way; the off hand drawn back
            // low with the elbow bent, then brought through past the hip and up - an UNDERARM throw,
            // snapping straight on the release - as the body comes round, the main hand drawn back
            // to the RELEASE (Throw's own first frame: arm at -105).
            new ComboMotion { CockSeconds = 0.08f, StrikeSeconds = 0.30f, Keys = new[]
            {
                K(0.0f,  -75f,  0.20f,  0.02f, -10f, B(0.040f, 0.070f, 0.050f, -8f), back: -20f, ox: -0.05f, oy: -0.06f),
                K(1.0f,  -30f,  0.06f, -0.04f,   8f, B(0.050f, 0.060f, -0.010f, 4f), back: -70f, turn: -30f, ox: -0.13f, oy: -0.05f),
                K(1.5f,  -60f, -0.05f,  0.00f,   6f, B(0.040f, 0.055f, 0.010f, 0f), back: 20f, turn: -200f, ox: -0.01f, oy: -0.13f),
                K(1.8f,  -85f, -0.12f,  0.03f,   3f, B(0.035f, 0.058f, 0.015f, 1f), back: 70f, turn: -310f, ox: 0.10f, oy: -0.08f),
                K(2.0f, -105f, -0.164f, 0.044f,  0f, B(0.030f, 0.060f, 0.020f, 2f), back: 100f, turn: -360f, ox: 0.18f, oy: 0.03f),
                K(3.0f, -105f, -0.164f, 0.044f,  0f, B(0.030f, 0.060f, 0.020f, 2f), back: 60f, turn: -360f, ox: 0.15f, oy: 0.01f),
            }},

            // Crow Hop 1: load sideways (torso turned away, throwing hand cocked back high, the off
            // arm stretched out at the target) through the hop, then the overarm release as it
            // lands - the off arm pulled in hard to the chest as the throwing arm comes over. The
            // hop is the swing hop (SetSwingHop).
            new ComboMotion { CockSeconds = 0.14f, StrikeSeconds = 0.14f, Keys = new[]
            {
                K(0.0f,    0f,  0.02f, -0.10f,   0f, B(0f, 0f, 0f, 0f), back: 15f, ox: 0.00f, oy: -0.12f),
                K(1.0f, -100f, -0.15f,  0.06f,  20f, B(0.050f, 0.075f, -0.020f, 6f), back: 70f, ox: 0.18f, oy: 0.06f),
                K(1.5f,  -60f, -0.05f,  0.14f,   4f, B(0.030f, 0.075f, 0.030f, -4f), back: 20f, ox: 0.08f, oy: 0.03f),
                K(2.0f,  -80f,  0.22f,  0.00f, -18f, B(0.055f, 0.090f, 0.070f, -14f), back: -40f, ox: -0.04f, oy: 0.02f),
                K(3.0f,  -78f,  0.20f, -0.01f, -12f, B(0.045f, 0.080f, 0.060f, -10f), back: -30f, ox: -0.05f, oy: -0.02f),
            }},
            // Crow Hop 2: a shorter hop, the off hand drawn back past the hip with the elbow bent,
            // then slung out LEVEL - a sidearm throw, the arm snapping straight - while the main
            // hand swings wide behind; ends LOW with both arms WIDE (ORBIT).
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.14f, Keys = new[]
            {
                K(0.0f,  -78f,  0.20f, -0.01f, -12f, B(0.045f, 0.080f, 0.060f, -10f), back: -30f, ox: -0.05f, oy: -0.02f),
                K(1.0f,  -40f,  0.10f, -0.03f,  12f, B(0.060f, 0.070f, -0.010f, 4f), back: -60f, ox: -0.13f, oy: -0.01f),
                K(1.5f,   20f, -0.08f, -0.03f,   4f, B(0.065f, 0.080f, 0.030f, -2f), back: 40f, ox: 0.04f, oy: -0.05f),
                K(2.0f,   70f, -0.17f, -0.04f,   0f, B(0.070f, 0.085f, 0.050f, -4f), back: 85f, ox: 0.19f, oy: -0.01f),
                K(3.0f,   70f, -0.17f, -0.04f,   0f, B(0.065f, 0.082f, 0.045f, -3f), back: 80f, ox: 0.17f, oy: -0.03f),
            }},

            // Charge 1: the cock is the RUN - the throwing arm pumping back and forward, the off
            // arm against it, leaning in (the player is carried forward - the mock's dash); then
            // cocked high and a long stretching throw at full stride.
            new ComboMotion { CockSeconds = 0.20f, StrikeSeconds = 0.12f, Keys = new[]
            {
                K(0.0f,    0f,  0.02f, -0.10f,   0f, B(0f, 0f, 0f, 0f), ox: 0.00f, oy: -0.12f),
                K(0.35f,  30f, -0.10f, -0.06f,   6f, B(0.030f, 0.050f, 0.020f, -6f), ox: 0.08f, oy: -0.02f),
                K(0.7f,  -20f,  0.06f, -0.02f,  -4f, B(0.030f, 0.050f, 0.030f, -8f), ox: -0.08f, oy: -0.06f),
                K(1.0f, -100f, -0.16f,  0.06f,  18f, B(0.040f, 0.070f, -0.010f, -4f), ox: 0.16f, oy: 0.04f),
                K(1.5f,  -60f, -0.03f,  0.13f,   2f, B(0.045f, 0.080f, 0.040f, -10f), ox: 0.06f, oy: 0.00f),
                K(2.0f,  -85f,  0.24f,  0.03f, -20f, B(0.060f, 0.095f, 0.080f, -16f), ox: -0.06f, oy: -0.02f),
                K(3.0f,  -80f,  0.22f,  0.01f, -14f, B(0.050f, 0.090f, 0.070f, -12f), ox: -0.05f, oy: -0.04f),
            }},
            // Charge 2: braking - sitting back onto the front foot, the off hand cocked back and
            // slung forward across the body as the momentum swings it; ends with both fists in
            // front of the chest, the arms CROSSED.
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.14f, Keys = new[]
            {
                K(0.0f,  -80f,  0.22f,  0.01f, -14f, B(0.050f, 0.090f, 0.070f, -12f), ox: -0.05f, oy: -0.04f),
                K(1.0f,  -40f,  0.08f,  0.00f,  14f, B(0.070f, 0.085f, 0.050f, 6f), ox: -0.12f, oy: 0.04f),
                K(1.5f,    0f,  0.05f,  0.03f,   0f, B(0.060f, 0.080f, 0.050f, 2f), ox: 0.04f, oy: 0.08f),
                K(2.0f,   20f,  0.02f,  0.06f, -10f, B(0.050f, 0.075f, 0.050f, -6f), ox: 0.19f, oy: 0.02f),
                K(3.0f,   40f,  0.07f,  0.05f,   0f, B(0.030f, 0.060f, 0.020f, 0f), ox: 0.11f, oy: 0.05f),
            }},

            // Leap 1: the cock is the JUMP (the swing hop - crouch, then up, both discs drawn
            // overhead, arched back at the top); the strike throws one DOWN at the target on the
            // way down (released mid-strike) and lands in a crouch.
            new ComboMotion { CockSeconds = 0.20f, StrikeSeconds = 0.16f, Keys = new[]
            {
                K(0.0f,    0f,  0.02f, -0.10f,   0f, B(0f, 0f, 0f, 0f), ox: 0.00f, oy: -0.12f),
                K(0.4f,   10f, -0.04f, -0.08f,   4f, B(0.090f, 0.060f, 0.000f, -4f), ox: -0.04f, oy: -0.08f),
                K(1.0f,   40f, -0.10f,  0.15f,   8f, B(-0.020f, 0.020f, 0.000f, 10f), ox: -0.03f, oy: 0.17f),
                K(1.5f, -130f,  0.20f, -0.06f, -16f, B(0.000f, 0.040f, 0.030f, -12f), ox: 0.02f, oy: 0.10f),
                K(2.0f, -125f,  0.18f, -0.07f, -12f, B(0.090f, 0.070f, 0.030f, -10f), ox: 0.00f, oy: 0.06f),
                K(3.0f, -120f,  0.17f, -0.07f, -10f, B(0.080f, 0.070f, 0.030f, -8f), ox: 0.00f, oy: 0.04f),
            }},
            // Leap 2: still low from the landing, the off hand drawn back by the hip and swept
            // forward along the ground - a skipping throw - then rising out of the crouch with
            // both arms up (HALO).
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.14f, Keys = new[]
            {
                K(0.0f, -120f,  0.17f, -0.07f, -10f, B(0.080f, 0.070f, 0.030f, -8f), ox: 0.00f, oy: 0.04f),
                K(1.0f,  -90f,  0.10f, -0.04f,   8f, B(0.100f, 0.080f, 0.030f, -8f), ox: -0.12f, oy: -0.08f),
                K(1.5f,  -90f,  0.08f, -0.02f,   2f, B(0.100f, 0.080f, 0.040f, -10f), ox: 0.06f, oy: -0.14f),
                K(2.0f,  -80f,  0.06f,  0.02f,  -4f, B(0.090f, 0.080f, 0.040f, -8f), ox: 0.19f, oy: -0.08f),
                K(3.0f,    0f, -0.02f,  0.17f,   0f, B(0.000f, 0.030f, 0.000f, 2f), ox: 0.00f, oy: 0.18f),
            }},

            // Windmill 1: the throwing arm wheels a full circle at the shoulder - forward, over the
            // top, back and down - the body rocking back as it goes up and forward into the release,
            // underarm at the bottom of the circle, rising. A round disc keeps its face (blade 0).
            new ComboMotion { CockSeconds = 0.08f, StrikeSeconds = 0.32f, Keys = new[]
            {
                K(0.0f,    0f,  0.02f, -0.10f,   0f, B(0f, 0f, 0f, 0f), ox: 0.00f, oy: -0.12f),
                K(1.0f,    0f,  0.04f, -0.15f,   6f, B(0.030f, 0.040f, -0.020f, 6f), ox: 0.06f, oy: -0.08f),
                K(1.25f,   0f,  0.16f,  0.00f,   4f, B(0.020f, 0.040f, -0.020f, 8f), ox: -0.04f, oy: -0.10f),
                K(1.5f,    0f,  0.02f,  0.17f,   2f, B(0.010f, 0.040f, -0.020f, 10f), ox: -0.08f, oy: -0.06f),
                K(1.75f,   0f, -0.16f,  0.02f,  10f, B(0.020f, 0.050f, 0.000f, 4f), ox: -0.06f, oy: -0.06f),
                K(1.9f,    0f, -0.02f, -0.16f,   2f, B(0.040f, 0.060f, 0.030f, -6f), ox: -0.04f, oy: -0.04f),
                K(2.0f,  -20f,  0.17f, -0.05f, -12f, B(0.050f, 0.080f, 0.060f, -12f), ox: -0.10f, oy: -0.04f),
                K(3.0f,  -20f,  0.16f, -0.03f,  -9f, B(0.045f, 0.075f, 0.050f, -9f), ox: -0.09f, oy: -0.05f),
            }},
            // Windmill 2: the OFF arm wheels the other way - down, back, over the top - and lets
            // go as it comes forward high, the body swaying back; then turning away with both
            // hands drawn back behind the hip (COIL).
            new ComboMotion { CockSeconds = 0.08f, StrikeSeconds = 0.30f, Keys = new[]
            {
                K(0.0f,  -20f,  0.16f, -0.03f,  -9f, B(0.045f, 0.075f, 0.050f, -9f), ox: -0.09f, oy: -0.05f),
                K(1.0f,  -10f,  0.06f, -0.06f,   4f, B(0.040f, 0.060f, 0.020f, -2f), ox: 0.00f, oy: -0.16f),
                K(1.3f,    0f,  0.04f, -0.08f,   4f, B(0.030f, 0.055f, 0.010f, 2f), ox: -0.16f, oy: 0.00f),
                K(1.6f,    0f,  0.02f, -0.08f,   2f, B(0.020f, 0.050f, 0.000f, 6f), ox: 0.00f, oy: 0.17f),
                K(1.85f,   0f,  0.00f, -0.08f,   0f, B(0.020f, 0.050f, -0.010f, 7f), ox: 0.14f, oy: 0.10f),
                K(2.0f,    0f, -0.02f, -0.08f,   0f, B(0.030f, 0.050f, -0.020f, 8f), ox: 0.18f, oy: 0.03f),
                K(3.0f,  120f, -0.14f, -0.10f,  14f, B(0.050f, 0.060f, -0.010f, 6f), away: 70f, ox: -0.13f, oy: -0.10f),
            }},

            // ---- disc MELEE basics, MOCK (2026-10-15), EXAGGERATED (the user's call): the keys between
            // each hit's start and end pushed (waist x1.7, crouch x1.6, stance x1.35, step x1.8,
            // lean x1.6, fists x1.3, off fist x1.25), the cock 30% longer and the strike 15% quicker -
            // the end pose untouched, so each strike overshoots it and settles back. Connectors as the ranged dance's:
            // CROSS main (0.07, 0.05) off (0.11, 0.05); ORBIT main (-0.17, -0.04) off (0.17, -0.03);
            // HALO main (-0.02, 0.17) off (0, 0.18); COIL turned away 70, both behind the hip;
            // RELEASE main at Throw's first frame (-0.164, 0.044, blade -105). ----

            // Twin Cut 1: a main-hand rising backhand, low across the body up and out.
            new ComboMotion { CockSeconds = 0.13f, StrikeSeconds = 0.10f, Keys = new[]
            {
                K(0.0f,    0f,  0.02f, -0.10f,   0f, B(0f, 0f, 0f, 0f), ox: 0.00f, oy: -0.12f),
                K(1.0f, 150f, 0.000f, -0.156f, 20f, B(0.064f, 0.068f, -0.018f, 6f), ox: 0.050f, oy: -0.075f),
                K(1.5f, -90f, 0.182f, -0.026f, 0f, B(0.064f, 0.095f, 0.054f, -10f), ox: 0.025f, oy: -0.100f),
                K(2.0f, -30f, 0.234f, 0.130f, -17f, B(0.080f, 0.101f, 0.090f, -16f), ox: -0.025f, oy: -0.075f),
                K(3.0f,  -25f,  0.16f,  0.10f,  -8f, B(0.045f, 0.070f, 0.045f, -8f), ox: -0.02f, oy: -0.07f),
            }},
            // Twin Cut 2: the off hand drawn back high and brought across forward and down, the
            // main hand coming in to the chest; ends CROSSED.
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.10f, Keys = new[]
            {
                K(0.0f,  -25f,  0.16f,  0.10f,  -8f, B(0.045f, 0.070f, 0.045f, -8f), ox: -0.02f, oy: -0.07f),
                K(1.0f, -25f, 0.130f, 0.078f, 17f, B(0.072f, 0.081f, 0.000f, 6f), ox: -0.138f, oy: 0.062f),
                K(1.5f, 0f, 0.104f, 0.065f, 0f, B(0.080f, 0.095f, 0.054f, -6f), ox: 0.050f, oy: 0.050f),
                K(2.0f, 20f, 0.091f, 0.065f, -17f, B(0.088f, 0.108f, 0.090f, -16f), ox: 0.213f, oy: -0.013f),
                K(3.0f,   40f,  0.07f,  0.05f,   0f, B(0.030f, 0.060f, 0.020f, 0f), ox: 0.11f, oy: 0.05f),
            }},

            // Step-turn 1: a stepping level slash from behind to in front and on across, the back
            // turning to the camera as it follows through.
            new ComboMotion { CockSeconds = 0.13f, StrikeSeconds = 0.12f, Keys = new[]
            {
                K(0.0f,    0f,  0.02f, -0.10f,   0f, B(0f, 0f, 0f, 0f), ox: 0.00f, oy: -0.12f),
                K(1.0f, 60f, -0.156f, 0.000f, 24f, B(0.064f, 0.081f, -0.018f, 6f), ox: 0.075f, oy: -0.075f),
                K(1.5f, -60f, 0.208f, 0.013f, 0f, B(0.080f, 0.108f, 0.090f, -10f), away: 30f, ox: -0.050f, oy: -0.075f),
                K(2.0f, -100f, 0.130f, 0.039f, -17f, B(0.080f, 0.108f, 0.108f, -10f), away: 80f, ox: -0.100f, oy: -0.050f),
                K(3.0f, -100f,  0.10f,  0.03f, -10f, B(0.050f, 0.080f, 0.060f, -6f), away: 80f, ox: -0.08f, oy: -0.04f),
            }},
            // Step-turn 2: the turn carried on round (TURN, counter-clockwise, out of the back
            // view) with the arms opening, flung WIDE - low, main hand behind, off hand in front.
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.17f, Keys = new[]
            {
                K(0.0f, -100f,  0.10f,  0.03f, -10f, B(0.050f, 0.080f, 0.060f, -6f), away: 80f, ox: -0.08f, oy: -0.04f),
                K(1.0f, -80f, 0.156f, 0.000f, 10f, B(0.096f, 0.095f, 0.036f, 0f), away: 60f, turn: -60f, ox: -0.125f, oy: -0.050f),
                K(1.5f, 0f, 0.065f, -0.065f, 0f, B(0.104f, 0.108f, 0.072f, -3f), turn: -200f, ox: 0.150f, oy: -0.050f),
                K(2.0f, 70f, -0.221f, -0.052f, 0f, B(0.112f, 0.115f, 0.090f, -6f), turn: -360f, ox: 0.237f, oy: -0.013f),
                K(3.0f,   70f, -0.17f, -0.04f,   0f, B(0.065f, 0.082f, 0.045f, -3f), turn: -360f, ox: 0.17f, oy: -0.03f),
            }},

            // Scissor 1: crouched, both discs together low, then driven up in front - an uppercut.
            new ComboMotion { CockSeconds = 0.13f, StrikeSeconds = 0.10f, Keys = new[]
            {
                K(0.0f,    0f,  0.02f, -0.10f,   0f, B(0f, 0f, 0f, 0f), ox: 0.00f, oy: -0.12f),
                K(1.0f, 160f, 0.026f, -0.169f, 14f, B(0.112f, 0.081f, 0.000f, 3f), ox: 0.050f, oy: -0.150f),
                K(1.5f, 60f, 0.182f, 0.000f, 0f, B(0.064f, 0.095f, 0.054f, -6f), ox: 0.175f, oy: 0.000f),
                K(2.0f, 10f, 0.156f, 0.169f, -10f, B(0.016f, 0.081f, 0.054f, -10f), ox: 0.150f, oy: 0.163f),
                K(3.0f,   10f,  0.12f,  0.13f,  -6f, B(0.010f, 0.060f, 0.030f, -6f), ox: 0.12f, oy: 0.13f),
            }},
            // Scissor 2: the halves part overhead - main back, off forward - and close across each
            // other like shears; ends both raised (HALO).
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.12f, Keys = new[]
            {
                K(0.0f,   10f,  0.12f,  0.13f,  -6f, B(0.010f, 0.060f, 0.030f, -6f), ox: 0.12f, oy: 0.13f),
                K(1.0f, 30f, -0.078f, 0.195f, 10f, B(0.032f, 0.068f, 0.000f, 6f), ox: 0.175f, oy: 0.150f),
                K(1.5f, 0f, 0.026f, 0.234f, 0f, B(0.000f, 0.074f, 0.036f, 0f), ox: 0.062f, oy: 0.225f),
                K(2.0f, -20f, 0.130f, 0.208f, -7f, B(0.000f, 0.081f, 0.054f, -3f), ox: -0.050f, oy: 0.213f),
                K(3.0f,    0f, -0.02f,  0.17f,   0f, B(0.000f, 0.030f, 0.000f, 2f), ox: 0.00f, oy: 0.18f),
            }},

            // Punch and draw 1: chambered at the hip, then a straight punch-cut, the disc leading
            // like a fist, the body stepping in behind it and the off hand pulled back.
            new ComboMotion { CockSeconds = 0.13f, StrikeSeconds = 0.08f, Keys = new[]
            {
                K(0.0f,    0f,  0.02f, -0.10f,   0f, B(0f, 0f, 0f, 0f), ox: 0.00f, oy: -0.12f),
                K(1.0f, -80f, -0.065f, -0.039f, 17f, B(0.064f, 0.081f, -0.018f, 6f), ox: 0.100f, oy: -0.050f),
                K(2.0f, -90f, 0.299f, 0.026f, -20f, B(0.080f, 0.115f, 0.135f, -19f), ox: -0.100f, oy: -0.075f),
                K(3.0f,  -88f,  0.21f,  0.01f,  -9f, B(0.045f, 0.080f, 0.065f, -9f), ox: -0.07f, oy: -0.07f),
            }},
            // Punch and draw 2: a backhand up across and back, the body turning AWAY with it; ends
            // with both hands drawn behind the hip (COIL).
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.12f, Keys = new[]
            {
                K(0.0f,  -88f,  0.21f,  0.01f,  -9f, B(0.045f, 0.080f, 0.065f, -9f), ox: -0.07f, oy: -0.07f),
                K(1.0f, -60f, 0.182f, 0.078f, -7f, B(0.080f, 0.095f, 0.072f, -10f), ox: -0.050f, oy: -0.075f),
                K(1.4f, 20f, 0.026f, 0.156f, 10f, B(0.064f, 0.081f, 0.018f, 3f), away: 30f, ox: -0.100f, oy: -0.025f),
                K(1.7f, 90f, -0.130f, 0.026f, 20f, B(0.072f, 0.081f, -0.018f, 8f), away: 60f, ox: -0.150f, oy: -0.100f),
                K(2.0f, 120f, -0.182f, -0.130f, 24f, B(0.080f, 0.081f, -0.018f, 10f), away: 70f, ox: -0.163f, oy: -0.125f),
                K(3.0f,  120f, -0.14f, -0.10f,  14f, B(0.050f, 0.060f, -0.010f, 6f), away: 70f, ox: -0.13f, oy: -0.10f),
            }},

            // Chop and wind 1: a downward cut from overhead, the off hand balancing.
            new ComboMotion { CockSeconds = 0.13f, StrikeSeconds = 0.09f, Keys = new[]
            {
                K(0.0f,    0f,  0.02f, -0.10f,   0f, B(0f, 0f, 0f, 0f), ox: 0.00f, oy: -0.12f),
                K(1.0f, 40f, -0.026f, 0.208f, 17f, B(0.048f, 0.068f, -0.018f, 8f), ox: 0.100f, oy: -0.025f),
                K(2.0f, -150f, 0.208f, -0.130f, -20f, B(0.096f, 0.108f, 0.117f, -19f), ox: -0.075f, oy: -0.075f),
                K(3.0f, -145f,  0.15f, -0.09f,  -9f, B(0.050f, 0.075f, 0.055f, -9f), ox: -0.05f, oy: -0.07f),
            }},
            // Chop and wind 2: a backhand up across the body that carries on past the shoulder
            // into the cocked throw (RELEASE - Throw's first frame), the off arm reaching out.
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.12f, Keys = new[]
            {
                K(0.0f, -145f,  0.15f, -0.09f,  -9f, B(0.050f, 0.075f, 0.055f, -9f), ox: -0.05f, oy: -0.07f),
                K(1.0f, -160f, 0.182f, -0.156f, -20f, B(0.088f, 0.101f, 0.090f, -16f), ox: -0.062f, oy: -0.088f),
                K(1.5f, -60f, 0.052f, 0.130f, 0f, B(0.064f, 0.088f, 0.054f, -3f), ox: 0.050f, oy: -0.050f),
                K(2.0f, -105f, -0.213f, 0.057f, 10f, B(0.048f, 0.081f, 0.036f, 3f), ox: 0.175f, oy: 0.025f),
                K(3.0f, -105f, -0.164f, 0.044f,  0f, B(0.030f, 0.060f, 0.020f, 2f), ox: 0.12f, oy: 0.00f),
            }},

            // Tether's cast: out of CROSS the arms coil in tighter, then snap OPEN - the main hand
            // slinging its disc out sidearm, the off hand flung back - and the settle draws the
            // main hand back to the hip, leaning away, as the line pulls the target in.
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.10f, Keys = new[]
            {
                K(0.0f,   40f,  0.07f,  0.05f,   0f, B(0.030f, 0.060f, 0.020f, 0f), ox: 0.11f, oy: 0.05f),
                K(1.0f,   60f,  0.03f,  0.06f,  14f, B(0.045f, 0.060f, -0.010f, 4f), ox: 0.14f, oy: 0.07f),
                K(1.5f,  -40f,  0.14f,  0.04f,   0f, B(0.045f, 0.075f, 0.040f, -6f), ox: -0.06f, oy: 0.02f),
                K(2.0f,  -85f,  0.23f,  0.02f, -16f, B(0.055f, 0.090f, 0.065f, -13f), ox: -0.12f, oy: -0.02f),
                K(3.0f,  -40f, -0.08f, -0.04f,  10f, B(0.055f, 0.075f, -0.010f, 9f), ox: 0.10f, oy: -0.02f),
            }},

            // Revolve's cast: out of ORBIT (low, arms wide) the arms sink lower and wider, then
            // sweep up and round together, the discs let go into the ring at the top, and the
            // settle brings the empty hands down to rest.
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.14f, Keys = new[]
            {
                K(0.0f,   70f, -0.17f, -0.04f,   0f, B(0.065f, 0.082f, 0.045f, -3f), ox: 0.17f, oy: -0.03f),
                K(1.0f,   80f, -0.17f, -0.09f,   6f, B(0.085f, 0.090f, 0.030f, -2f), ox: 0.17f, oy: -0.09f),
                K(1.5f,   20f, -0.10f,  0.08f,   0f, B(0.040f, 0.070f, 0.020f, 2f), ox: 0.12f, oy: 0.08f),
                K(2.0f,    0f, -0.02f,  0.17f,  -4f, B(0.010f, 0.050f, 0.010f, 4f), ox: 0.02f, oy: 0.18f),
                K(3.0f,    0f,  0.02f, -0.10f,   0f, B(0.010f, 0.030f, 0.000f, 0f), ox: 0.00f, oy: -0.12f),
            }},

            // Plow's cast: out of CROSS the figure drops into a deep crouch with both arms drawn
            // back low, then swings them down and forward together and lets the discs go along the
            // ground - a bowler's release, leaning out over the front foot - and holds it low.
            new ComboMotion { CockSeconds = 0.12f, StrikeSeconds = 0.12f, Keys = new[]
            {
                K(0.0f,   40f,  0.07f,  0.05f,   0f, B(0.030f, 0.060f, 0.020f, 0f), ox: 0.11f, oy: 0.05f),
                K(1.0f,  150f, -0.12f, -0.10f,  10f, B(0.100f, 0.080f, -0.010f, 6f), ox: -0.10f, oy: -0.10f),
                K(1.5f,  180f,  0.04f, -0.16f,   0f, B(0.110f, 0.085f, 0.040f, -6f), ox: 0.04f, oy: -0.16f),
                K(2.0f, -120f,  0.20f, -0.12f, -14f, B(0.115f, 0.095f, 0.075f, -15f), ox: 0.19f, oy: -0.11f),
                K(3.0f, -115f,  0.18f, -0.10f, -10f, B(0.095f, 0.090f, 0.065f, -11f), ox: 0.17f, oy: -0.10f),
            }},

            // Cross Cut: out of CROSS the arms open up high into a V - the main hand high behind
            // the head, the off hand high in front - rising onto the toes; then both cut down
            // ACROSS the body, each hand ending on the OTHER's side: an X. The main arm crosses
            // BEHIND the body, its fist out past the far shoulder along the facing; the off arm
            // crosses over the front (OffFront, from the crossing on) to the near side - on top
            // at the end, as an X's second stroke is - and the body drops into a deep lunge.
            new ComboMotion { CockSeconds = 0.13f, StrikeSeconds = 0.11f, Keys = new[]
            {
                K(0.0f,   40f,  0.07f,  0.05f,   0f, B(0.030f, 0.060f, 0.020f, 0f), ox: 0.11f, oy: 0.05f),
                K(1.0f,   60f, -0.06f,  0.17f,  12f, B(-0.010f, 0.050f, -0.020f, 8f), ox: 0.10f, oy: 0.18f),
                K(1.4f,  -40f,  0.08f,  0.05f,   0f, B(0.050f, 0.085f, 0.050f, -4f), ox: 0.06f, oy: 0.06f),
                K(1.5f,  -80f,  0.13f,  0.00f,  -4f, B(0.065f, 0.092f, 0.065f, -6f), ox: 0.00f, oy: 0.02f, offFront: true),
                K(2.0f, -150f,  0.21f, -0.07f,  -8f, B(0.115f, 0.115f, 0.100f, -6f), ox: -0.11f, oy: -0.18f, offFront: true),
                K(3.0f, -145f,  0.19f, -0.07f,  -5f, B(0.070f, 0.090f, 0.065f, -4f), ox: -0.10f, oy: -0.17f, offFront: true),
            }},

            // Ripple's throw: out of RELEASE (the main hand cocked back at the ear) the body coils,
            // the off hand drawn across to join it; then both whip forward and APART - the main
            // hand out high, the off hand out low - a fan, two discs leaving each hand at the
            // strike's end - and the body follows through over the front foot.
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.11f, Keys = new[]
            {
                K(0.0f, -105f, -0.164f, 0.044f,  0f, B(0.030f, 0.060f, 0.020f, 2f), ox: 0.13f, oy: 0.01f),
                K(1.0f, -115f, -0.19f,  0.06f,  12f, B(0.060f, 0.070f, -0.010f, 6f), ox: -0.08f, oy: 0.00f),
                K(1.5f,  -80f,  0.02f,  0.07f,   0f, B(0.055f, 0.080f, 0.040f, -3f), ox: 0.04f, oy: -0.03f),
                K(2.0f,  -55f,  0.24f,  0.11f,  -8f, B(0.050f, 0.090f, 0.070f, -6f), ox: 0.19f, oy: -0.10f),
                K(3.0f,  -60f,  0.20f,  0.08f,  -5f, B(0.040f, 0.075f, 0.050f, -4f), ox: 0.16f, oy: -0.09f),
            }},

            // Carom's hurl: out of RELEASE both hands rise and meet behind the head, the back
            // arched - the pair COMBINED into one - then come over the top together and hurl it
            // forward two-handed, the body driving down over the front foot.
            new ComboMotion { CockSeconds = 0.14f, StrikeSeconds = 0.12f, Keys = new[]
            {
                K(0.0f, -105f, -0.164f, 0.044f,  0f, B(0.030f, 0.060f, 0.020f, 2f), ox: 0.13f, oy: 0.01f),
                K(1.0f,   30f, -0.09f,  0.18f,   6f, B(-0.010f, 0.050f, -0.020f, 8f), ox: -0.06f, oy: 0.18f),
                K(1.5f,  -20f,  0.05f,  0.21f,   0f, B(0.030f, 0.075f, 0.030f, -2f), ox: 0.08f, oy: 0.21f),
                K(2.0f,  -95f,  0.25f,  0.05f,  -6f, B(0.075f, 0.100f, 0.085f, -8f), ox: 0.27f, oy: 0.04f),
                K(3.0f,  -90f,  0.21f,  0.03f,  -4f, B(0.055f, 0.085f, 0.060f, -5f), ox: 0.23f, oy: 0.02f),
            }},

            // Heavy Cleaver's chop: out of HALO (both hands raised) the main hand draws its disc
            // back behind the head and the off hand reaches up and forward, rising onto the toes;
            // then the disc comes down over the top - a cleaver's chop - let go in front at
            // shoulder height, the off arm driving down and back to counter, the body dropping
            // hard into the follow-through.
            new ComboMotion { CockSeconds = 0.15f, StrikeSeconds = 0.11f, Keys = new[]
            {
                K(0.0f,    0f, -0.02f,  0.17f,   0f, B(0.000f, 0.030f, 0.000f, 2f), ox: 0.00f, oy: 0.18f),
                K(1.0f,   70f, -0.10f,  0.19f,   6f, B(-0.010f, 0.050f, -0.020f, 6f), ox: 0.10f, oy: 0.21f),
                K(1.5f,  -30f,  0.08f,  0.19f,   0f, B(0.040f, 0.080f, 0.040f, -2f), ox: 0.06f, oy: 0.04f),
                K(2.0f, -120f,  0.24f,  0.00f,  -6f, B(0.100f, 0.105f, 0.090f, -8f), ox: -0.06f, oy: -0.14f),
                K(3.0f, -125f,  0.20f, -0.03f,  -4f, B(0.075f, 0.090f, 0.065f, -5f), ox: -0.05f, oy: -0.12f),
            }},

            // Carousel: out of ORBIT (low, arms wide) the arms lift to shoulder height, a disc held
            // out in each hand - main behind, off in front - winding a touch the other way; then
            // TWO full TURNS through the views (on Step-turn's counter-clockwise), sinking as it
            // goes, the arms held out all the way round, and they open out and down in the settle.
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.56f, MaxStrike = 0.8f, Keys = new[]
            {
                K(0.0f,   70f, -0.17f, -0.04f,   0f, B(0.065f, 0.082f, 0.045f, -3f), ox: 0.17f, oy: -0.03f),
                K(1.0f,   85f, -0.22f,  0.03f,   0f, B(0.075f, 0.090f, 0.020f, 0f), turn: 20f, ox: 0.22f, oy: 0.04f),
                K(1.25f,  85f, -0.22f,  0.04f,   0f, B(0.090f, 0.096f, 0.030f, 0f), turn: -170f, ox: 0.22f, oy: 0.05f),
                K(1.5f,   85f, -0.22f,  0.04f,   0f, B(0.100f, 0.099f, 0.035f, 0f), turn: -360f, ox: 0.22f, oy: 0.05f),
                K(1.75f,  85f, -0.22f,  0.04f,   0f, B(0.105f, 0.101f, 0.038f, 0f), turn: -540f, ox: 0.22f, oy: 0.05f),
                K(2.0f,   85f, -0.22f,  0.04f,   0f, B(0.110f, 0.102f, 0.040f, 0f), turn: -720f, ox: 0.22f, oy: 0.05f),
                K(3.0f,   70f, -0.18f, -0.02f,   0f, B(0.070f, 0.085f, 0.040f, -2f), turn: -720f, ox: 0.18f, oy: -0.01f),
            }},

            // Kickback's fling: out of COIL (turned away, both hands behind the hip) the coil winds
            // a touch deeper, then the body unwinds to the front and both arms sling forward from
            // the hip and fling the discs out in a fan - the main hand a little high, the off hand
            // a little low - and the push of it rocks the body back onto its heels for the hop.
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.10f, Keys = new[]
            {
                K(0.0f,  120f, -0.14f, -0.10f,  14f, B(0.050f, 0.060f, -0.010f, 6f), away: 70f, ox: -0.13f, oy: -0.10f),
                K(1.0f,  130f, -0.17f, -0.12f,  16f, B(0.070f, 0.070f, -0.020f, 7f), away: 80f, ox: -0.16f, oy: -0.12f),
                K(1.5f,    0f,  0.05f, -0.08f,   4f, B(0.050f, 0.080f, 0.020f, 0f), away: 30f, ox: 0.02f, oy: -0.08f),
                K(2.0f,  -90f,  0.24f,  0.02f,   0f, B(0.040f, 0.065f, -0.040f, 7f), ox: 0.23f, oy: -0.06f),
                K(3.0f,  -85f,  0.18f, -0.03f,   0f, B(0.035f, 0.060f, -0.030f, 5f), ox: 0.17f, oy: -0.08f),
            }},

            // Scattershot's burst: out of HALO (both raised) a crouch, then the JUMP (the swing hop
            // rises over the cock) with both discs gathered high over the head and the body arched;
            // at the top both arms are flung down and OUT - the main hand forward and low, the off
            // hand back and low - a star, the discs leaving as the arms open; it lands in a crouch.
            new ComboMotion { CockSeconds = 0.20f, StrikeSeconds = 0.16f, Keys = new[]
            {
                K(0.0f,    0f, -0.02f,  0.17f,   0f, B(0.000f, 0.030f, 0.000f, 2f), ox: 0.00f, oy: 0.18f),
                K(0.4f,   10f, -0.03f,  0.10f,   0f, B(0.090f, 0.060f, 0.000f, -3f), ox: 0.03f, oy: 0.10f),
                K(1.0f,   20f, -0.05f,  0.21f,   0f, B(-0.020f, 0.020f, 0.000f, 7f), ox: 0.04f, oy: 0.22f),
                K(1.5f,  -60f,  0.14f,  0.12f,   0f, B(0.000f, 0.050f, 0.000f, -2f), ox: -0.12f, oy: 0.12f),
                K(2.0f, -115f,  0.22f, -0.05f,   0f, B(0.090f, 0.085f, 0.020f, -4f), ox: -0.21f, oy: -0.05f),
                K(3.0f, -110f,  0.19f, -0.06f,   0f, B(0.075f, 0.075f, 0.020f, -3f), ox: -0.18f, oy: -0.06f),
            }},

            // Mark's flick: out of RELEASE the off arm comes up and POINTS at the target - held
            // there through the throw, the marking gesture - while the main hand draws back across
            // the body low, the torso winding into it; then the hand whips out sidearm, level, and
            // the wrist snaps the disc away, the body unwinding behind the pointing arm.
            new ComboMotion { CockSeconds = 0.12f, StrikeSeconds = 0.10f, Keys = new[]
            {
                K(0.0f, -105f, -0.164f, 0.044f,  0f, B(0.030f, 0.060f, 0.020f, 2f), ox: 0.13f, oy: 0.01f),
                K(1.0f,   40f, -0.10f, -0.03f,  10f, B(0.050f, 0.070f, 0.010f, 4f), ox: 0.25f, oy: 0.08f),
                K(1.5f,  -30f,  0.08f, -0.01f,   4f, B(0.045f, 0.075f, 0.030f, -2f), ox: 0.25f, oy: 0.08f),
                K(2.0f,  -95f,  0.25f,  0.02f,  -6f, B(0.050f, 0.080f, 0.050f, -6f), ox: 0.18f, oy: 0.05f),
                K(3.0f,  -92f,  0.21f,  0.00f,  -4f, B(0.040f, 0.070f, 0.040f, -4f), ox: 0.14f, oy: 0.01f),
            }},

            // Sublimate's opening: out of CROSS (arms crossed at the chest) the cross squeezes in
            // tighter as the body sinks; then both arms SNAP OPEN wide and a little up - the main
            // hand out in front, the off hand out behind - flinging the discs out to hang, the chest
            // opening and the body rising onto its toes; held open in the settle.
            new ComboMotion { CockSeconds = 0.12f, StrikeSeconds = 0.10f, Keys = new[]
            {
                K(0.0f,   40f,  0.07f,  0.05f,   0f, B(0.030f, 0.060f, 0.020f, 0f), ox: 0.11f, oy: 0.05f),
                K(1.0f,   55f,  0.03f,  0.07f,   0f, B(0.070f, 0.060f, 0.010f, 3f), ox: 0.14f, oy: 0.07f),
                K(1.5f,  -40f,  0.14f,  0.10f,   0f, B(0.020f, 0.060f, 0.020f, -2f), ox: -0.04f, oy: 0.10f),
                K(2.0f, -105f,  0.24f,  0.09f,   0f, B(-0.010f, 0.070f, 0.030f, -6f), ox: -0.22f, oy: 0.09f),
                K(3.0f, -100f,  0.21f,  0.06f,   0f, B(0.010f, 0.065f, 0.025f, -4f), ox: -0.19f, oy: 0.06f),
            }},

            // Orrery's cast: out of ORBIT (low, arms wide) the body sinks lower, the arms dipping
            // wider still; then both sweep UP and OUT - the main hand high in front, the off hand
            // high behind - rising tall, the discs leaving to spiral out onto their orbits; the
            // arms held up and open through the settle, as if setting the planets turning.
            new ComboMotion { CockSeconds = 0.12f, StrikeSeconds = 0.12f, Keys = new[]
            {
                K(0.0f,   70f, -0.17f, -0.04f,   0f, B(0.065f, 0.082f, 0.045f, -3f), ox: 0.17f, oy: -0.03f),
                K(1.0f,   85f, -0.19f, -0.07f,   0f, B(0.095f, 0.092f, 0.045f, -3f), ox: 0.19f, oy: -0.07f),
                K(1.5f,   20f, -0.12f,  0.10f,   0f, B(0.050f, 0.080f, 0.030f, 0f), ox: 0.14f, oy: 0.10f),
                K(2.0f,  -40f,  0.10f,  0.20f,   0f, B(0.005f, 0.070f, 0.020f, 3f), ox: -0.14f, oy: 0.20f),
                K(3.0f,  -30f,  0.08f,  0.16f,   0f, B(0.015f, 0.070f, 0.020f, 2f), ox: -0.12f, oy: 0.15f),
            }},

            // Gyre's lift: out of HALO (both raised) both arms come down and REACH OUT in front at
            // chest height, the body leaning into them - the discs leave the hands there to spin
            // as a rotor in front of the character.
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.10f, Keys = new[]
            {
                K(0.0f,    0f, -0.02f,  0.17f,   0f, B(0.000f, 0.030f, 0.000f, 2f), ox: 0.00f, oy: 0.18f),
                K(1.0f,  -30f,  0.08f,  0.15f,   0f, B(0.030f, 0.050f, 0.010f, 0f), ox: 0.08f, oy: 0.14f),
                K(2.0f,  -90f,  0.24f,  0.06f,   0f, B(0.045f, 0.075f, 0.040f, -4f), ox: 0.23f, oy: 0.05f),
                K(3.0f,  -90f,  0.24f,  0.06f,   0f, B(0.045f, 0.075f, 0.040f, -4f), ox: 0.23f, oy: 0.05f),
            }},

            // Gyre's cut: from Lift's end (held through the pull, the arms out under the rotor)
            // the hands draw back a little as the discs come home, then both slash down and across
            // in front, the body dropping into the cut.
            new ComboMotion { CockSeconds = 0.08f, StrikeSeconds = 0.10f, Keys = new[]
            {
                K(0.0f,  -90f,  0.24f,  0.06f,   0f, B(0.045f, 0.075f, 0.040f, -4f), ox: 0.23f, oy: 0.05f),
                K(1.0f,  -70f,  0.16f,  0.10f,   0f, B(0.035f, 0.065f, 0.020f, 1f), ox: 0.15f, oy: 0.09f),
                K(1.5f, -110f,  0.22f,  0.04f,   0f, B(0.060f, 0.085f, 0.045f, -4f), ox: 0.20f, oy: 0.01f),
                K(2.0f, -150f,  0.20f, -0.04f,   0f, B(0.085f, 0.095f, 0.060f, -7f), ox: 0.16f, oy: -0.06f),
                K(3.0f, -145f,  0.18f, -0.05f,   0f, B(0.065f, 0.085f, 0.050f, -5f), ox: 0.14f, oy: -0.06f),
            }},

            // King and Queen's pincer: out of RELEASE (the gather holds this first frame) the arms
            // cross in front of the chest, winding a touch; then they sweep APART - the main hand
            // out in front and high, the off hand out behind - each slinging its half round its
            // own side, the chest opening; held open as the pair goes out.
            new ComboMotion { CockSeconds = 0.10f, StrikeSeconds = 0.10f, Keys = new[]
            {
                K(0.0f, -105f, -0.164f, 0.044f,  0f, B(0.030f, 0.060f, 0.020f, 2f), ox: 0.13f, oy: 0.01f),
                K(1.0f,   30f,  0.02f,  0.04f,   6f, B(0.050f, 0.070f, 0.010f, 2f), ox: 0.18f, oy: 0.03f),
                K(1.5f,  -40f,  0.14f,  0.08f,   0f, B(0.040f, 0.075f, 0.030f, -2f), ox: 0.02f, oy: 0.06f),
                K(2.0f, -100f,  0.24f,  0.10f,  -4f, B(0.045f, 0.085f, 0.050f, -5f), ox: -0.22f, oy: 0.08f),
                K(3.0f,  -95f,  0.21f,  0.07f,  -3f, B(0.040f, 0.080f, 0.045f, -4f), ox: -0.19f, oy: 0.06f),
            }},

            // King and Queen's hurl: both arms raised overhead to RECEIVE the pair at the top of the
            // shadow step (this first frame, held through the catch), a touch further back as the
            // pair is gathered, then both drive DOWN in front, hurling it onto the target, the body
            // folding over the throw.
            new ComboMotion { CockSeconds = 0.06f, StrikeSeconds = 0.08f, Keys = new[]
            {
                K(0.0f,    0f, -0.02f,  0.22f,   0f, B(-0.020f, 0.020f, 0.000f, 5f), ox: 0.00f, oy: 0.23f),
                K(1.0f,   15f, -0.05f,  0.24f,   0f, B(-0.030f, 0.020f, 0.000f, 7f), ox: -0.02f, oy: 0.25f),
                K(1.5f,  -60f,  0.12f,  0.14f,   0f, B(0.040f, 0.070f, 0.030f, -3f), ox: 0.10f, oy: 0.13f),
                K(2.0f, -140f,  0.20f, -0.06f,   0f, B(0.110f, 0.110f, 0.080f, -9f), ox: 0.18f, oy: -0.07f),
                K(3.0f, -135f,  0.18f, -0.07f,   0f, B(0.080f, 0.100f, 0.060f, -7f), ox: 0.16f, oy: -0.07f),
            }},
        };

        /// <summary>The combo's cock and strike boundaries as fractions of the swing - Chop's
        /// rule (absolute seconds, clamped), so a basic snaps the same at any tempo.</summary>
        static void ComboPhases(ComboMotion c, float seconds, out float cockEnd, out float strikeEnd)
        {
            float dur = Mathf.Max(0.09f, seconds);
            cockEnd = Mathf.Clamp(c.CockSeconds / dur, 0.08f, 0.32f);
            strikeEnd = cockEnd + Mathf.Clamp(c.StrikeSeconds / dur, 0.15f, c.MaxStrike);
            strikeEnd = Mathf.Min(strikeEnd, c.MaxStrike > 0.55f ? 0.93f : 0.9f);
        }

        /// <summary>The phase axis (0..3) at <paramref name="k"/>: the cock eased out (quick, then
        /// held), the strike and the settle each eased in and out as a whole.</summary>
        static float ComboPhaseAt(ComboMotion c, float k, float seconds)
        {
            ComboPhases(c, seconds, out float cockEnd, out float strikeEnd);
            k = Mathf.Clamp01(k);
            if (k < cockEnd) return Mathf.Sin(k / cockEnd * Mathf.PI * 0.5f);
            if (k < strikeEnd) return 1f + Mathf.SmoothStep(0f, 1f, (k - cockEnd) / (strikeEnd - cockEnd));
            return 2f + Mathf.SmoothStep(0f, 1f, (k - strikeEnd) / Mathf.Max(0.0001f, 1f - strikeEnd));
        }

        /// <summary>The keys either side of phase <paramref name="p"/> and how far between.</summary>
        static void ComboBracket(ComboMotion c, float p, out ComboKey a, out ComboKey b, out float u)
        {
            var keys = c.Keys;
            a = keys[0]; b = keys[0]; u = 0f;
            if (p <= keys[0].P) return;
            for (int i = 1; i < keys.Length; i++)
            {
                if (p > keys[i].P) continue;
                a = keys[i - 1]; b = keys[i];
                u = (p - a.P) / Mathf.Max(0.0001f, b.P - a.P);
                return;
            }
            a = b = keys[keys.Length - 1];
        }

        /// <summary>Animate's half for a combo motion: the arms and the waist's turn.</summary>
        static void AnimateCombo(ComboMotion c, float k, float seconds,
                                 ref float armSwing, ref float backSwing, ref float bodySpin, ref Vector3 armReach)
        {
            ComboBracket(c, ComboPhaseAt(c, k, seconds), out var a, out var b, out float u);
            float blade = Mathf.LerpUnclamped(a.Blade, b.Blade, u);
            bodySpin = Mathf.Lerp(a.Waist, b.Waist, u);
            armSwing = blade - bodySpin;
            backSwing = Mathf.Lerp(a.Back, b.Back, u);
            armReach = ReachFor(Vector2.Lerp(a.Fist, b.Fist, u), armSwing);
        }

        /// <summary>The combo's OFF fist at <paramref name="k"/> (against the off shoulder, +X
        /// forward), or null when its keys set none - see ComboKey.Off.</summary>
        static Vector2? ComboOffFist(ComboMotion c, float k, float seconds)
        {
            ComboBracket(c, ComboPhaseAt(c, k, seconds), out var a, out var b, out float u);
            bool ha = !float.IsNaN(a.Off.x), hb = !float.IsNaN(b.Off.x);
            if (ha && hb) return Vector2.Lerp(a.Off, b.Off, u);
            if (ha) return a.Off;
            if (hb) return b.Off;
            return null;
        }

        static SwingBody ComboBody(ComboMotion c, float k, float seconds)
        {
            ComboBracket(c, ComboPhaseAt(c, k, seconds), out var a, out var b, out float u);
            return SwingBody.Lerp(a.Body, b.Body, u);
        }

        /// <summary>How far turned away, and turned round, the combo is at <paramref name="k"/>,
        /// and whether its blade is behind the body.</summary>
        static void ComboDimensions(ComboMotion c, float k, float seconds,
                                    out float away, out float turn, out bool behind)
            => ComboDimensions(c, k, seconds, out away, out turn, out behind, out _);

        static void ComboDimensions(ComboMotion c, float k, float seconds,
                                    out float away, out float turn, out bool behind, out bool offFront)
        {
            ComboBracket(c, ComboPhaseAt(c, k, seconds), out var a, out var b, out float u);
            away = Mathf.Lerp(a.Away, b.Away, u);
            turn = Mathf.Lerp(a.Turn, b.Turn, u);
            behind = u < 1f ? a.Behind : b.Behind;
            offFront = u < 1f ? a.OffFront : b.OffFront;
        }

        /// <summary>Whether this motion turns the whole figure round (its keys carry TURN) - it
        /// then turns like a Spin (SpinTurning), the pose turning with it.</summary>
        static bool ComboTurns(AttackMotion m)
        {
            var c = ComboOf(m);
            if (c == null) return false;
            foreach (var key in c.Keys) if (Mathf.Abs(key.Turn) > 0.01f) return true;
            return false;
        }

        /// <summary>The live combo's turned-away amount, held through the follow-through pause
        /// and eased back to facing after it - FaceAimTurning reads it for the view.</summary>
        float _comboAway;

        /// <summary>Whether the live combo has its blade behind the body - held through the
        /// follow-through pause like the rest of its pose.</summary>
        bool _comboBehind;

        /// <summary>Whether the live combo draws the off arm in front of the body (ComboKey.OffFront)
        /// - held through the follow-through pause, read by ApplyLayerOrder.</summary>
        bool _comboOffFront;

        /// <summary>Updated in the swing branch each frame - see _comboAway/_comboBehind.</summary>
        void TickComboDimensions(bool swinging, float k, float dt)
        {
            var c = ComboOf(_motion);
            bool holding = _sinceSwing < CarryReturnDelay && HeldTwoHanded;
            bool offFront = _comboOffFront;
            if (c != null && swinging)
            {
                ComboDimensions(c, k, _attackLength, out float away, out _, out bool behind, out offFront);
                _comboAway = away;
                _comboBehind = behind;
            }
            else if (c == null || !holding)
            {
                _comboAway = Mathf.Lerp(_comboAway, 0f, 1f - Mathf.Exp(-PoseRecoverySharpness * dt));
                _comboBehind = false;
                // The off hand stays in front through the follow-through pause, as its pose does.
                if (c == null || _sinceSwing >= CarryReturnDelay) offFront = false;
            }
            if (offFront != _comboOffFront)
            {
                _comboOffFront = offFront;
                ApplyLayerOrder();
            }
        }
    }
}
