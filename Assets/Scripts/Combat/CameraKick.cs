using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// A short directional shove of the camera when the player lands a finisher - the other half
    /// of the impact beat <see cref="Hitstop"/> already owns, and deliberately built to the same
    /// shape: a static with Attach/SetPlayer/Teardown, hooked onto a body's own Damaged event,
    /// leaving Health ignorant of either.
    ///
    /// It is gated on exactly what Hitstop is gated on (player-sourced, a finisher, not
    /// suppressed) rather than on a rule of its own. Two impact effects that disagree about which
    /// hits are impacts would read as a bug in whichever one fired alone, and the katana
    /// sequence's SuppressHitstop already exists to make one move produce one beat.
    ///
    /// THE CAMERA IS NOT MOVED HERE. GameBootstrap.TrackCamera stays the only thing that writes
    /// the camera's position - the same one-owner rule Controls.Screen follows - and simply adds
    /// <see cref="Offset"/> to what it was going to write anyway. Two things writing a transform
    /// independently is how a follow camera starts fighting itself.
    /// </summary>
    public static class CameraKick
    {
        static GameObject _player;

        /// <summary>
        /// Non-readonly plain fields, deliberately: a domain reload restores both, and a reload
        /// mid-kick just resumes the spring from where it was.
        /// </summary>
        static Vector2 _offset, _velocity;

        /// <summary>Where the camera should sit relative to its follow position, world units.</summary>
        public static Vector2 Offset => _offset;

        /// <summary>Set once from BuildPlayer, cleared by Teardown - see Hitstop.SetPlayer.</summary>
        public static void SetPlayer(GameObject player) => _player = player;

        /// <summary>
        /// Subscribe a body so the camera answers when the PLAYER lands a finisher on it. Called
        /// once per Health at creation, right beside <see cref="Hitstop.Attach"/>.
        /// </summary>
        public static void Attach(Health hp)
        {
            hp.Damaged += info => OnDamaged(info, hp);
        }

        static void OnDamaged(DamageInfo info, Health victim)
        {
            if (info.Amount <= 0f) return;
            if (_player == null || info.Source != _player) return;
            if (!info.IsFinisher) return;
            if (info.SuppressHitstop) return;
            if (victim == null) return;

            // Toward what was hit, not along the player's facing. The two are usually close, but
            // when they differ it is because the blow landed off to one side - which is the case
            // where a kick along the facing would shove the camera at nothing in particular.
            Vector2 dir = victim.transform.position - _player.transform.position;
            if (dir.sqrMagnitude < 0.0001f) return;

            Kick(dir.normalized, info.Crit ? Tuning.CameraKick.CritDistance
                                           : Tuning.CameraKick.Distance);
        }

        /// <summary>
        /// Shove the camera <paramref name="distance"/> world units along
        /// <paramref name="direction"/>. Public so anything that lands without a Damaged event of
        /// its own can ask - a parry is the case Hitstop already needed this for.
        /// </summary>
        public static void Kick(Vector2 direction, float distance)
        {
            // The spring is displaced rather than accelerated, so the kick is at full extent on
            // the frame of the hit. Feeding it as a velocity instead would spend the first few
            // frames travelling out, which puts the camera's answer AFTER the freeze that is
            // supposed to be the same moment.
            _offset += direction * distance;
        }

        /// <summary>
        /// Advanced once a frame from GameBootstrap.Update, on SCALED time - the opposite choice
        /// to Hitstop.Tick, and the reason is that they are opposite jobs. Hitstop is what sets
        /// the timescale, so it cannot measure itself with it. The kick is part of the fight, so
        /// it should hold still exactly when the fight does: the camera lands displaced, freezes
        /// there for the length of the hitstop, and only then recovers. Recovering THROUGH the
        /// freeze would spend the whole effect on frames where nothing else is moving.
        /// </summary>
        public static void Tick()
        {
            if (_offset.sqrMagnitude < 1e-8f && _velocity.sqrMagnitude < 1e-6f)
            {
                _offset = Vector2.zero;
                _velocity = Vector2.zero;
                return;
            }

            float dt = Time.deltaTime;
            _velocity += -_offset * Tuning.CameraKick.Stiffness * dt;
            _velocity *= Mathf.Exp(-Tuning.CameraKick.Damping * dt);
            _offset += _velocity * dt;
        }

        /// <summary>Torn down between runs, so a kick cannot outlive the run that caused it and
        /// leave the hub framed slightly off - the discipline Hitstop.Teardown follows.</summary>
        public static void Teardown()
        {
            _player = null;
            _offset = Vector2.zero;
            _velocity = Vector2.zero;
        }
    }
}
