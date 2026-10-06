using UnityEngine;
using UnityEngine.UI;

namespace Convergence.UI
{
    /// <summary>
    /// Keeps one hint line matching the device in use. See <see cref="UiKit.Hint"/>.
    ///
    /// A component rather than a coroutine or a static registry so it dies with the label it is
    /// attached to - these live on screens that are built and destroyed constantly, and a
    /// registry would need every one of them to remember to unregister.
    /// </summary>
    public class HintSwap : MonoBehaviour
    {
        public string Keyboard, Touch;

        /// <summary>
        /// Optional. Null falls back to <see cref="Touch"/> rather than to <see cref="Keyboard"/>,
        /// because the touch strings are already written device-neutrally ("BACK to close",
        /// "ATTACK") while the keyboard ones name keys a pad does not have. So an un-updated hint
        /// degrades to something merely generic instead of something actively wrong.
        /// </summary>
        public string Gamepad;

        Text _text;
        Mode _was;
        bool _primed;

        enum Mode { Keyboard, Touch, Gamepad }

        void Awake() => _text = GetComponent<Text>();

        void Update()
        {
            // Gamepad is checked FIRST: Controls.TouchMode and GamepadMode are mutually exclusive
            // by construction (last device wins, see Controls.DetectDevice), but the order makes
            // that independent of which one happens to be asked first.
            var mode = Core.Controls.GamepadMode ? Mode.Gamepad
                     : Core.Controls.TouchMode ? Mode.Touch
                     : Mode.Keyboard;

            if (_primed && mode == _was) return;
            _primed = true;
            _was = mode;
            if (_text == null) return;

            _text.text = mode switch
            {
                Mode.Gamepad => string.IsNullOrEmpty(Gamepad) ? Touch : Gamepad,
                Mode.Touch => Touch,
                _ => Keyboard,
            };
        }
    }
}
