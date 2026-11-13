using UnityEngine;
using Convergence.Core;

namespace Convergence.Audio
{
    /// <summary>
    /// THE MENUS' SOUNDS (the user's call, 2026-11-13: quiet menu feedback), heard from one place rather than
    /// sixty screens: every screen already registers its clickable rects (Controls.SetFocusCandidates), so a
    /// press landing on one is a CONFIRM, the pad's selection moving is a TICK, and BACK out of a screen
    /// that can be dismissed is two notes down. Runs after the screens (LateUpdate) and only reads Controls.
    /// A screen's own bigger sound (a deal stamped, gear put on) plays over its confirm.
    /// </summary>
    public class UiSounds : MonoBehaviour
    {
        RectTransform _lastFocus;
        bool _wasModal;

        void LateUpdate()
        {
            if (Controls.TappedChoice) Sfx.Play(Sound.UiConfirm);

            var focus = Controls.Focused;
            if (focus != null && _lastFocus != null && focus != _lastFocus && !Controls.TappedChoice) Sfx.Play(Sound.UiMove);
            _lastFocus = focus;

            if (_wasModal && Controls.CancelTapped) Sfx.Play(Sound.UiBack);
            _wasModal = Controls.Screen == Controls.Context.Modal;
        }
    }
}
