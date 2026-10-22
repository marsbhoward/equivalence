using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The settings menu - from the hub with Escape (keyboard) or Start (gamepad), see
    /// <see cref="Controls.SettingsTapped"/>, and mid-run from the pause menu's top ring (closing
    /// it returns there). One entry today: CONTROLLER CONFIGURATION, which
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

        System.Action _onControls, _onClosed;
        RectTransform _controlsRect, _closeRect;
        // The three volume dials: a tap on a row's left half turns it down a step, the right half
        // up; a pad's A clicks the centre, which steps UP and wraps past full to silent.
        RectTransform _masterRect, _effectsRect, _ambienceRect;
        Text _masterText, _effectsText, _ambienceText;
        const float VolumeStep = 0.1f;
        readonly List<RectTransform> _focus = new();

        /// <summary><paramref name="onClosed"/> runs after CLOSE or <see cref="Dismiss"/> - mid-run
        /// it is the pause menu this was opened from; from the hub, nothing.</summary>
        public void Open(Transform canvas, System.Action onControls, System.Action onClosed = null)
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _onControls = onControls;
            _onClosed = onClosed;

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
            _masterRect = _effectsRect = _ambienceRect = null;
            _masterText = _effectsText = _ambienceText = null;
            _onControls = _onClosed = null;
        }

        /// <summary>Closes and hands back to whoever opened it (Escape, or CLOSE).</summary>
        public void Dismiss()
        {
            var cb = _onClosed;
            Close();
            cb?.Invoke();
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

            var bg = new Color(0.10f, 0.12f, 0.17f, 1f);
            var fg = new Color(0.86f, 0.89f, 0.96f);
            _masterRect = Row(full, -2, "", bg, fg);
            _effectsRect = Row(full, -1, "", bg, fg);
            _ambienceRect = Row(full, 0, "", bg, fg);
            _masterText = _masterRect.GetComponentInChildren<Text>();
            _effectsText = _effectsRect.GetComponentInChildren<Text>();
            _ambienceText = _ambienceRect.GetComponentInChildren<Text>();
            RefreshVolumes();

            _controlsRect = Row(full, 1, "CONTROLLER CONFIGURATION",
                new Color(0.10f, 0.12f, 0.17f, 1f), new Color(0.86f, 0.89f, 0.96f));
            _closeRect = Row(full, 2, "CLOSE",
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
            float top = -20f;   // five rows centred under the title
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
            _focus.Add(_masterRect); _focus.Add(_effectsRect); _focus.Add(_ambienceRect);
            _focus.Add(_controlsRect); _focus.Add(_closeRect);
            Controls.SetFocusCandidates(_focus);

            // Escape closes this too, but that is GameBootstrap's call, not this Update's - see
            // its own settings block for why. Escape drives both SettingsTapped (open) and
            // CancelTapped (close), and this component's Update runs in an undefined order
            // against GameBootstrap's; reading CancelTapped here as well raced it and could
            // reopen this screen on the very frame it closed.
            if (!Controls.Tapped(out var p)) return;
            if (Hit(_closeRect, p)) { Dismiss(); return; }
            if (Hit(_masterRect, p)) { Audio.Volume.Master = Step(Audio.Volume.Master, _masterRect, p); RefreshVolumes(); return; }
            if (Hit(_effectsRect, p)) { Audio.Volume.Effects = Step(Audio.Volume.Effects, _effectsRect, p); RefreshVolumes(); Audio.Sfx.Play(Audio.Sound.HitLight); return; }
            if (Hit(_ambienceRect, p)) { Audio.Volume.Ambience = Step(Audio.Volume.Ambience, _ambienceRect, p); RefreshVolumes(); return; }
            if (Hit(_controlsRect, p)) { var cb = _onControls; cb?.Invoke(); return; }
        }

        /// <summary>Left of the row's middle steps down; the middle and right step up, wrapping
        /// from full to silent (the pad clicks the exact centre).</summary>
        static float Step(float v, RectTransform rt, Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, null, out var local);
            float width = rt.rect.width;
            bool down = local.x < rt.rect.center.x - width * 0.1f;
            float next = Mathf.Round((v + (down ? -VolumeStep : VolumeStep)) / VolumeStep) * VolumeStep;
            if (!down && v >= 0.999f) next = 0f;
            return Mathf.Clamp01(next);
        }

        void RefreshVolumes()
        {
            if (_masterText != null) _masterText.text = Line("MASTER", Audio.Volume.Master);
            if (_effectsText != null) _effectsText.text = Line("EFFECTS", Audio.Volume.Effects);
            if (_ambienceText != null) _ambienceText.text = Line("AMBIENCE", Audio.Volume.Ambience);
        }

        static string Line(string name, float v) => $"<   {name}  {Mathf.RoundToInt(v * 100)}%   >";

        static bool Hit(RectTransform rt, Vector2 point)
            => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, point, null);
    }
}
