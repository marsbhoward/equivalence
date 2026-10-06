using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The hub's settings menu - opened with Escape (keyboard) or Start (gamepad), see
    /// <see cref="Controls.SettingsTapped"/>. One entry today: CONTROLLER CONFIGURATION, which
    /// hands off to <see cref="ControlsScreen"/> the same closes-this-opens-that way
    /// <see cref="PauseScreen"/> hands off to CHARACTER/INVENTORY/END RUN - it never nests,
    /// GameBootstrap owns the wiring back.
    /// </summary>
    public class SettingsScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        GameObject _root;

        // Opened by a key, and wasPressedThisFrame stays true for the whole frame - without
        // skipping the opening frame, the very Escape press that opened this would also read as
        // this screen's own Cancel and close it before it was ever drawn. Same guard as
        // ConfirmDialog and PauseScreen.
        int _openedFrame = -1;

        System.Action _onControls;
        RectTransform _controlsRect, _closeRect;
        readonly List<RectTransform> _focus = new();

        public void Open(Transform canvas, System.Action onControls)
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _onControls = onControls;

            GamePause.Hold(this);
            Build(canvas);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _controlsRect = _closeRect = null;
            _onControls = null;
        }

        // ------------------------------------------------------------------ ui

        void Build(Transform canvas)
        {
            _root = new GameObject("SettingsScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.02f, 0.035f, 0.88f));

            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-320, -150), new Vector2(320, -70)),
                "SETTINGS", 40, new Color(0.90f, 0.91f, 0.96f), TextAnchor.MiddleCenter);

            _controlsRect = Row(full, 0, "CONTROLLER CONFIGURATION",
                new Color(0.10f, 0.12f, 0.17f, 1f), new Color(0.86f, 0.89f, 0.96f));
            _closeRect = Row(full, 1, "CLOSE",
                new Color(0.10f, 0.12f, 0.17f, 1f), new Color(0.86f, 0.89f, 0.96f));

            UiKit.Hint(UiKit.Rect(full, "h", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 40), new Vector2(0, 82)),
                "[ESC] to close", "tap CLOSE to close", 17,
                new Color(0.5f, 0.53f, 0.62f), TextAnchor.MiddleCenter,
                GamepadGlyphs.Cancel + " to close");
        }

        static RectTransform Row(RectTransform parent, int index, string label, Color bg, Color fg)
        {
            const float w = 520f, h = UiKit.TouchTarget, gap = 24f;
            float top = -h * 0.5f;
            float y = top - index * (h + gap);
            var rt = UiKit.Panel(parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-w * 0.5f, y - h * 0.5f), new Vector2(w * 0.5f, y + h * 0.5f), bg);
            UiKit.Label(rt, label, 22, fg, TextAnchor.MiddleCenter);
            return rt;
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!IsOpen) return;
            if (Time.frameCount == _openedFrame) return;

            _focus.Clear();
            _focus.Add(_controlsRect); _focus.Add(_closeRect);
            Controls.SetFocusCandidates(_focus);

            // Escape closes this too, but that is GameBootstrap's call, not this Update's - see
            // its own settings block for why. Escape drives both SettingsTapped (open) and
            // CancelTapped (close), and this component's Update runs in an undefined order
            // against GameBootstrap's; reading CancelTapped here as well raced it and could
            // reopen this screen on the very frame it closed.
            if (!Controls.Tapped(out var p)) return;
            if (Hit(_closeRect, p)) { Close(); return; }
            if (Hit(_controlsRect, p)) { var cb = _onControls; cb?.Invoke(); return; }
        }

        static bool Hit(RectTransform rt, Vector2 point)
            => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, point, null);
    }
}
