using UnityEngine;
using Convergence.Player;

namespace Convergence.Art
{
    /// <summary>
    /// Drives an authored actor prefab from gameplay state, without gameplay code needing to
    /// know anything about the art. Added automatically to every visual child.
    ///
    /// If the prefab has an Animator, these parameters are driven when they exist - all optional:
    ///   float "Speed"   - current movement speed in units/sec
    ///   trigger "Attack" - fired on each attack swing
///   int   "Motion"   - which AttackMotion this swing is (Chop/Sweep/Thrust/Spin/Jab)
///   float "AttackSpeed" - scales the clip so it fills the real attack interval
    /// Sprites are flipped horizontally to match facing.
    /// </summary>
    public class ActorVisual : MonoBehaviour
    {
        public string SpeedParam = "Speed";
        public string AttackTrigger = "Attack";
        public string MotionParam = "Motion";
        public string ChargeTrigger = "Charge";
        public string AttackSpeedParam = "AttackSpeed";
        [Tooltip("Turn off if the art is drawn facing both ways, or is symmetrical.")]
        public bool FlipToFacing = true;

        /// <summary>The length an authored attack clip is assumed to be authored at.</summary>
        const float BaseSwingSeconds = 0.24f;

        Animator _animator;
        Rigidbody2D _body;
        PlayerController _player;
        SpriteRenderer[] _renderers;
        bool _hasSpeed, _hasAttack, _hasMotion, _hasAttackSpeed, _hasCharge;

        void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _body = GetComponentInParent<Rigidbody2D>();
            _player = GetComponentInParent<PlayerController>();
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);

            if (_animator != null)
            {
                foreach (var p in _animator.parameters)
                {
                    if (p.name == SpeedParam) _hasSpeed = true;
                    if (p.name == AttackTrigger) _hasAttack = true;
                    if (p.name == MotionParam) _hasMotion = true;
                    if (p.name == ChargeTrigger) _hasCharge = true;
                    if (p.name == AttackSpeedParam) _hasAttackSpeed = true;
                }
            }
        }

        void Update()
        {
            if (_animator != null && _hasSpeed && _body != null)
                _animator.SetFloat(SpeedParam, _body.linearVelocity.magnitude);

            if (!FlipToFacing) return;

            // Player aims with the mouse; everything else faces where it is moving.
            float x = _player != null ? _player.Facing.x
                    : _body != null ? _body.linearVelocity.x
                    : 0f;
            if (Mathf.Abs(x) < 0.05f) return;

            bool flip = x < 0f;
            foreach (var sr in _renderers)
                if (sr != null) sr.flipX = flip;
        }

        public void PlayCharge(float seconds)
        {
            if (_animator == null || !_hasCharge) return;
            if (_hasAttackSpeed && seconds > 0.01f)
                _animator.SetFloat(AttackSpeedParam, BaseSwingSeconds / seconds);
            _animator.SetTrigger(ChargeTrigger);
        }

        public void PlayAttack(Combat.AttackMotion motion, float duration)
        {
            if (_animator == null || !_hasAttack) return;
            // Authored controllers select the clip from Motion and time it with AttackSpeed.
            // Both are optional, so a controller carrying only the trigger still works.
            if (_hasMotion) _animator.SetInteger(MotionParam, (int)motion);
            if (_hasAttackSpeed && duration > 0.01f)
                _animator.SetFloat(AttackSpeedParam, BaseSwingSeconds / duration);
            _animator.SetTrigger(AttackTrigger);
        }
    }
}
