using UnityEngine;

namespace Convergence.Art.Gear
{
    // THE DASH POSE (movement point 1; the user picked LOW BURST from three mocks, 2026-11-09). The
    // defensive dash covers 3.6 units in 0.25 s, about four times the walk, and the rig had no pose
    // for it: the walk sped up, its stride capped (WalkStrideMaxScale), and the legs splayed into an
    // A and skated. Now the dash takes the legs from the walk and holds a sprinter's drive - low, a
    // long split, the chest well over the front foot, the back foot up on its toe - then skids back
    // into the walk over DashOutSeconds, weight caught on the heels. Grit flicks off the feet at the
    // takeoff and again at the skid.
    //
    // A dash AWAY from the facing (the facing is the aim, the dash goes where the stick points) leans
    // back instead and pushes off the front foot - the backpedal's rule (WalkBackpedalThreshold), so a
    // dash straight up or down the screen still drives forward.
    public partial class PrimitiveCharacterRig
    {
        /// <summary>Testing: no dash pose - the walk sped up, as before. For a before-and-after.</summary>
        public static bool DevPlainDash;
        /// <summary>Testing: dash as if PlayerController.Dashing.</summary>
        [System.NonSerialized] public bool DevDashing;
        /// <summary>Testing: the body's speed, world units a second, in place of the Rigidbody's (-1 = read it).</summary>
        [System.NonSerialized] public float DevSpeed = -1f;

        const float DashInSeconds = 0.05f, DashOutSeconds = 0.2f;

        /// <summary>The drive held through the dash (the mock's "A").</summary>
        static SwingBody DashDrive => new(0.05f, 0.085f, 0f, -20f);
        /// <summary>How far the back foot is up on its toe through the drive, rig units.</summary>
        const float DashToeLift = 0.03f;
        /// <summary>Dashing away from the facing: the lean tipped back, this share of the drive's.</summary>
        const float DashBackLeanShare = 0.4f;
        /// <summary>The skid out of a dash: weight caught back on the heels.</summary>
        static SwingBody DashSkid => new(0.04f, 0.06f, 0f, 6f);

        float _dash;        // 0..1, how far the dash pose has taken the body
        bool _dashHeld;     // the dash itself still running (else the skid out of it)
        bool _dashAway;     // this dash goes away from the facing - fixed at the takeoff
        float _dashLiftFront, _dashLiftBack;

        bool DashingNow => DevDashing || (Player != null && Player.Dashing);

        /// <summary>After AdvanceGait: the dash weight, and the walk handing the legs over to it.</summary>
        void AdvanceDash(float dt)
        {
            if (DevPlainDash) { _dash = 0f; _dashHeld = false; return; }
            bool held = DashingNow;
            if (held && !_dashHeld)
            {
                Vector2 v = Body != null ? Body.linearVelocity : Vector2.zero;
                float forward = _facingLeft ? -v.x : v.x;
                _dashAway = forward < -WalkBackpedalThreshold * v.magnitude;
                DashGrit(v, false);
            }
            else if (!held && _dashHeld) DashGrit(Body != null ? Body.linearVelocity : Vector2.zero, true);
            _dashHeld = held;
            if (!held && _dash <= 0f) return;
            _dash = Mathf.MoveTowards(_dash, held ? 1f : 0f, dt / (held ? DashInSeconds : DashOutSeconds));
            _gaitWeight *= 1f - _dash;
        }

        /// <summary>Before PlaceBody: the dash's body over whatever the swing or the relax left.</summary>
        void PoseDash()
        {
            _dashLiftFront = _dashLiftBack = 0f;
            if (_dash <= 0f) return;
            var body = _dashHeld ? DashDrive : DashSkid;
            if (_dashHeld && _dashAway) body.Lean = -body.Lean * DashBackLeanShare;
            float w = Mathf.SmoothStep(0f, 1f, _dash);
            _poseBody = SwingBody.Lerp(_poseBody, body, w);
            if (_dashHeld)
            {
                if (_dashAway) _dashLiftFront = DashToeLift * w;
                else _dashLiftBack = DashToeLift * w;
            }
        }

        /// <summary>Grit off both feet: kicked back at the takeoff, thrown on ahead at the skid. On the
        /// foot line, as a footfall's (see Update).</summary>
        void DashGrit(Vector2 velocity, bool skid)
        {
            if (velocity.sqrMagnitude < 0.01f) return;
            Vector2 travel = velocity.normalized, flick = skid ? travel : -travel;
            Vector3 ground = _shadow != null ? _shadow.transform.position : transform.position;
            Vector2 side = new Vector2(-travel.y, travel.x) * (0.06f * _visualScale);
            DustPuff.Raise(ground + (Vector3)(flick * DustAhead * _visualScale + side), flick);
            DustPuff.Raise(ground + (Vector3)(flick * DustAhead * _visualScale - side), flick);
        }
    }
}
