using UnityEngine;

namespace Convergence.Art.Gear
{
    // The living cape's state for Body8 (Body8.Cape.cs): its clock, and the one-shots the fight gives it.
    public partial class PrimitiveCharacterRig
    {
        Body8.LivingCape _cape8;
        float _cape8Clock, _cape8LashT = 99f, _cape8Wrap, _cape8Dash;
        /// <summary>The way the last dash went (world), held while the cape settles after it.</summary>
        Vector2 _cape8DashDir = Vector2.right;
        bool _cape8Swung;

        /// <summary>A swing's lash (seconds): out at once, then home.</summary>
        const float LashOut = 0.12f, LashHome = 0.6f;

        /// <summary>The living cape's state this frame and its key: "" when none is worn, null when a one-shot is
        /// playing (the picture is drawn for this frame alone).</summary>
        string LiveCape8(float dt, float size, float phase)
        {
            bool on = false;
            if (_worn8 != null) foreach (var p in _worn8) if (p.Living) { on = true; break; }
            if (!on) { _cape8 = default; return ""; }

            bool swinging = _attackTimer > 0f || _chargeTimer > 0f;
            if (swinging && !_cape8Swung) _cape8LashT = 0f;
            _cape8Swung = swinging;
            _cape8LashT += dt;
            float lash = _cape8LashT < LashOut ? _cape8LashT / LashOut : Mathf.Max(0f, 1f - (_cape8LashT - LashOut) / LashHome);
            bool guard = Player != null && Player.ParryWindowOpen;
            _cape8Wrap = Mathf.MoveTowards(_cape8Wrap, guard ? 1f : 0f, dt * (guard ? 6f : 2f));
            _cape8Dash = Mathf.MoveTowards(_cape8Dash, DashingNow ? 1f : 0f, dt * (DashingNow ? 6f : 2.5f));
            if (DashingNow)
            {
                var v = Body != null ? Body.linearVelocity : Vector2.zero;
                _cape8DashDir = v.sqrMagnitude > 0.01f ? v.normalized : _travelDir;
            }
            // the dash's opposite in the drawn picture: the root mirrors facing left, the sprite flips for the back views
            float sx = (_facingLeft ? -1f : 1f) * (_b8Flip ? -1f : 1f);
            var away = new Vector2(-_cape8DashDir.x * sx, -_cape8DashDir.y);

            bool moving = size > 0f;
            int k = 0;
            if (moving) _cape8Clock = 0f;
            else
            {
                _cape8Clock = Mathf.Repeat(_cape8Clock + dt, Body8.LoopSeconds);
                k = Mathf.FloorToInt(_cape8Clock / Body8.LoopSeconds * Body8.LoopFrames) % Body8.LoopFrames;
            }
            _cape8 = new Body8.LivingCape
            {
                On = true, Moving = moving, Lash = lash, Wrap = _cape8Wrap, Dash = _cape8Dash, DashAway = away,
                Angle = moving ? phase * 2f * Mathf.PI : k * 2f * Mathf.PI / Body8.LoopFrames,
            };
            if (lash > 0.001f || _cape8Wrap > 0.001f || _cape8Dash > 0.001f) return null;
            return moving ? ".lc" : ".lc" + k;
        }
    }
}
