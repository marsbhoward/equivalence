using Convergence.Combat;

namespace Convergence.Art.Gear
{
    // THE PARRY GUARD (the Parry Lab mock, 2026-11-18). Barrier, Bulwark and Parry Stance open the
    // parry window at their start; for that window the weapon comes up into a guard - one frame of a
    // motion held, as a portrait pose is (HoldPose), but travelled into over the swings' entry blend,
    // with no tremble (a guard waits, it does not wind up), and let go on its own when the window
    // closes. A landed parry's counter is a real swing, whose PlayAttack takes over from the guard.
    //
    // The guard is never a strike: InStrike, SinceStrike and ReleaseSettle ignore it, so it neither
    // roots the walk nor reads as an art in motion, and a step does not settle it early.
    public partial class PrimitiveCharacterRig
    {
        /// <summary>Seconds the guard is still up (0 = none) and how long it has been.</summary>
        [System.NonSerialized] float _guardLeft, _guardElapsed;

        bool GuardUp => _guardLeft > 0f;

        public void RaiseGuard(AttackMotion motion, float k, bool alt, float seconds)
        {
            if (seconds <= 0f || _chargeTimer > 0f) return;
            // A swing still before its strike keeps its arm: the hit is timed off it.
            if (_attackTimer > 0f && _frozenK < 0f && !GuardUp && !PastStrike) return;
            bool sound = MakesSound;
            MakesSound = false;
            PlayAttack(motion, PoseSeconds, alt);
            MakesSound = sound;
            _frozenK = UnityEngine.Mathf.Clamp01(k);
            _awaitLoose = false;
            _guardLeft = seconds;
            _guardElapsed = 0f;
        }

        /// <summary>The guard's clock, run before the pose branches: a guard whose window closed is let
        /// go (the arm eases back to the carry as after any swing).</summary>
        void TickGuard(float dt)
        {
            if (!GuardUp) return;
            _guardElapsed += dt;
            _guardLeft -= dt;
            if (_guardLeft > 0f) return;
            _guardLeft = 0f;
            _frozenK = -1f;
            _attackTimer = 0f;
        }
    }
}
