using UnityEngine;
using UnityEngine.InputSystem;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// The character you steer around the hub. Walking only - no attack, no targeting, no
    /// element, no health.
    ///
    /// Deliberately NOT a stripped-down <see cref="Player.PlayerController"/>. That component
    /// carries a combo chain, an auto-targeter, a resource and a damage pipeline, all of which
    /// would need switching off here and every one of which is a way for the menu to break the
    /// run that follows it. This is the whole of what a menu character has to do.
    ///
    /// The rig animates itself from the Rigidbody's velocity when there is no PlayerController on
    /// the object, so the walk cycle and the turn come for free - see PrimitiveCharacterRig.Body.
    /// </summary>
    public class HubAvatar : MonoBehaviour
    {
        public float MoveSpeed = Tuning.Hub.WalkSpeed;

        /// <summary>Cleared while a screen is up, so the character does not drift under a menu.</summary>
        public bool InputEnabled = true;

        Rigidbody2D _rb;

        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;
        public bool IsMoving => Velocity.sqrMagnitude > 0.05f;

        void Awake() => _rb = GetComponent<Rigidbody2D>();

        void FixedUpdate()
        {
            if (_rb == null) return;

            // Keyboard and the on-screen stick together - the hub is the first thing a player
            // steers, so it has to answer whichever one they reached for.
            Vector2 input = InputEnabled ? Core.Controls.Move : Vector2.zero;

            // Same blend as combat movement, so the character carries the same weight in the menu
            // as it does in the arena - the hub is the first thing a player steers, and it is
            // where they form an expectation of how heavy this character is.
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, input.normalized * MoveSpeed,
                                              input.sqrMagnitude > 0.01f ? 0.55f : 0.25f);
        }
    }
}
