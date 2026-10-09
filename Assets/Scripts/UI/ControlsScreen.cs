using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// Rebinds which physical gamepad button each action reads - see <see cref="GamepadBindings"/>
    /// for the mapping this writes to. Reached from SettingsScreen's CONTROLLER CONFIGURATION row;
    /// backing out returns there, the same closes-this-opens-that shape every other hand-off in
    /// this project already uses rather than nesting.
    ///
    /// Clicking a row starts LISTENING: the next gamepad button pressed becomes that action's new
    /// binding. If another action already owned it the two SWAP - <see cref="GamepadBindings.Set"/>
    /// does the swap itself; this screen only reflects the result.
    ///
    /// NOT TOUCH-TARGET SIZED, DELIBERATELY. Every other screen in this project holds to
    /// UiKit.TouchTarget because a phone player might reach any of them - this one cannot be
    /// reached by one. Opening it starts at SettingsScreen, which itself only opens on Escape or a
    /// gamepad's Select button (Controls.SettingsTapped), and there is no on-screen/virtual
    /// equivalent for either - the same "keyboard only, deliberately" reasoning Controls.DigitTapped
    /// already states for a shortcut nothing on a touch device can reach. A screen about
    /// configuring a controller is also meaningless without one already connected.
    /// </summary>
    public class ControlsScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        GameObject _root;
        int _openedFrame = -1;
        System.Action _onBack;

        readonly List<(RectTransform Rect, Image Bg, Text Binding, GamepadAction Action)> _rows = new();
        readonly List<RectTransform> _focus = new();
        RectTransform _resetRect, _backRect;
        Text _prompt;
        GameObject _listenOverlay;

        bool _listening;
        GamepadAction _listeningAction;

        static readonly Color RowIdle = new(0.10f, 0.12f, 0.17f, 1f);
        static readonly Color RowListening = new(0.30f, 0.24f, 0.10f, 1f);
        static readonly Color NameColor = new(0.86f, 0.89f, 0.96f);
        static readonly Color BindingColor = new(0.95f, 0.85f, 0.55f);

        public void Open(Transform canvas, System.Action onBack)
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _onBack = onBack;
            _listening = false;

            GamePause.Hold(this);
            Build(canvas);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _listening = false;
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _rows.Clear();
            _resetRect = _backRect = null;
            _prompt = null;
            _listenOverlay = null;
            _onBack = null;
        }

        // ------------------------------------------------------------------ ui

        const float RowW = 640f, RowH = 62f, RowGap = 12f, FirstRowTop = -200f;

        void Build(Transform canvas)
        {
            _root = new GameObject("ControlsScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.02f, 0.035f, 0.92f));

            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-360, -150), new Vector2(360, -70)),
                "CONTROLLER CONFIGURATION", 32, new Color(0.90f, 0.91f, 0.96f), TextAnchor.MiddleCenter);

            var actions = GamepadBindings.AllActions;
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                float rowTop = FirstRowTop - i * (RowH + RowGap);
                var rect = UiKit.Panel(full, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(-RowW * 0.5f, rowTop - RowH), new Vector2(RowW * 0.5f, rowTop), RowIdle);
                var bg = rect.GetComponent<Image>();

                UiKit.Label(UiKit.Rect(rect, "n", Vector2.zero, Vector2.one,
                    new Vector2(24, 0), new Vector2(-260, 0)),
                    GamepadBindings.ActionName(action), 18, NameColor, TextAnchor.MiddleLeft);

                var bindLabel = UiKit.Label(UiKit.Rect(rect, "b", Vector2.zero, Vector2.one,
                    new Vector2(0, 0), new Vector2(-24, 0)),
                    GamepadBindings.Name(GamepadBindings.Get(action)), 18, BindingColor, TextAnchor.MiddleRight);

                _rows.Add((rect, bg, bindLabel, action));
            }

            float buttonTop = FirstRowTop - actions.Count * (RowH + RowGap) - 24f;
            const float buttonH = 70f;
            _resetRect = SmallButton(full, new Vector2(-170f, buttonTop), buttonH, "RESET TO DEFAULT");
            _backRect = SmallButton(full, new Vector2(170f, buttonTop), buttonH, "BACK");

            _listenOverlay = UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.01f, 0.01f, 0.02f, 0.75f)).gameObject;
            _prompt = UiKit.Label(UiKit.Rect((RectTransform)_listenOverlay.transform,
                "p", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-460, -40), new Vector2(460, 40)),
                "", 26, new Color(0.98f, 0.86f, 0.40f), TextAnchor.MiddleCenter);
            _listenOverlay.SetActive(false);

            UiKit.Hint(UiKit.Rect(full, "h", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 40), new Vector2(0, 82)),
                "click a row to rebind it    -    [ESC] to go back",
                "click a row to rebind it    -    tap BACK to go back", 17,
                new Color(0.5f, 0.53f, 0.62f), TextAnchor.MiddleCenter,
                "press a row, then the new button    -    " + GamepadGlyphs.Cancel + " or " + GamepadGlyphs.Settings + " to go back");
        }

        static RectTransform SmallButton(RectTransform parent, Vector2 center, float h, string label)
        {
            const float w = 280f;
            var rt = UiKit.Panel(parent, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(center.x - w * 0.5f, center.y - h),
                new Vector2(center.x + w * 0.5f, center.y),
                new Color(0.10f, 0.12f, 0.17f, 1f));
            UiKit.Label(rt, label, 19, NameColor, TextAnchor.MiddleCenter);
            return rt;
        }

        void RefreshBindingLabels()
        {
            foreach (var r in _rows)
                r.Binding.text = GamepadBindings.Name(GamepadBindings.Get(r.Action));
        }

        void RefreshRowColors()
        {
            foreach (var r in _rows)
                r.Bg.color = _listening && r.Action == _listeningAction ? RowListening : RowIdle;
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!IsOpen) return;
            if (Time.frameCount == _openedFrame) return;

            if (_listening)
            {
                _prompt.text = "press a button for\n" + GamepadBindings.ActionName(_listeningAction);

                // Settings, not Cancel: Cancel is one of the very actions a player might be
                // mid-rebind of, and escaping a capture must not depend on whichever button
                // currently happens to mean Cancel.
                if (Controls.SettingsTapped) { StopListening(); return; }

                if (Controls.TryCaptureGamepadButton(out var button))
                {
                    GamepadBindings.Set(_listeningAction, button);
                    StopListening();
                    RefreshBindingLabels();
                }
                return;
            }

            _focus.Clear();
            foreach (var r in _rows) _focus.Add(r.Rect);
            _focus.Add(_resetRect); _focus.Add(_backRect);
            Controls.SetFocusCandidates(_focus);

            if (Controls.CancelTapped) { var cb = _onBack; cb?.Invoke(); return; }

            if (!Controls.Tapped(out var p)) return;

            if (Hit(_backRect, p)) { var cb = _onBack; cb?.Invoke(); return; }
            if (Hit(_resetRect, p)) { GamepadBindings.ResetToDefault(); RefreshBindingLabels(); return; }

            for (int i = 0; i < _rows.Count; i++)
            {
                if (!Hit(_rows[i].Rect, p)) continue;
                _listening = true;
                _listeningAction = _rows[i].Action;
                RefreshRowColors();
                _listenOverlay.SetActive(true);
                return;
            }
        }

        void StopListening()
        {
            _listening = false;
            RefreshRowColors();
            if (_listenOverlay) _listenOverlay.SetActive(false);
        }

        static bool Hit(RectTransform rt, Vector2 point)
            => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, point, null);
    }
}
