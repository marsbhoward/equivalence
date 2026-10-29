using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// HUD &amp; TOUCH - the on-screen controls and where the run HUD sits (<see cref="HudPrefs"/>).
    /// Reached from SETTINGS the way CONTROLLER CONFIGURATION is: settings closes, this opens, and
    /// BACK (or Escape) closes this and reopens settings - GameBootstrap owns the hand-off, it
    /// never nests.
    ///
    /// Every row is a dial like the volumes: a tap on its left half steps back, on its right half
    /// forward, and a pad's A (which clicks the exact centre) steps forward and wraps. Every change
    /// lands at once - the overlay and the HUD read the prefs every frame - so the player sees
    /// the result through the dimmed panel before they leave.
    /// </summary>
    public class HudSettingsScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        GameObject _root;
        int _openedFrame = -1;
        System.Action _onBack;

        RectTransform _overlayRect, _touchRect, _deskRect, _sizeRect, _handRect, _keysRect, _backRect;
        Text _overlayText, _touchText, _deskText, _sizeText, _handText, _keysText, _about;
        readonly List<RectTransform> _focus = new();

        public void Open(Transform canvas, System.Action onBack)
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _onBack = onBack;
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
            _overlayRect = _touchRect = _deskRect = _sizeRect = _handRect = _keysRect = _backRect = null;
            _overlayText = _touchText = _deskText = _sizeText = _handText = _keysText = _about = null;
            _onBack = null;
        }

        // ------------------------------------------------------------------ ui

        void Build(Transform canvas)
        {
            _root = new GameObject("HudSettingsScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.02f, 0.035f, 0.88f));

            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-320, -120), new Vector2(320, -50)),
                "HUD & TOUCH", 40, new Color(0.90f, 0.91f, 0.96f), TextAnchor.MiddleCenter);

            var bg = new Color(0.10f, 0.12f, 0.17f, 1f);
            var fg = new Color(0.86f, 0.89f, 0.96f);
            _overlayRect = Row(full, -2, bg, fg, out _overlayText);
            _touchRect = Row(full, -1, bg, fg, out _touchText);
            _deskRect = Row(full, 0, bg, fg, out _deskText);
            _sizeRect = Row(full, 1, bg, fg, out _sizeText, half: -1);
            _handRect = Row(full, 1, bg, fg, out _handText, half: 1);
            _keysRect = Row(full, 2, bg, fg, out _keysText);
            _backRect = Row(full, 3, bg, fg, out var back);
            back.text = "BACK";

            // What the touch layout means, between the title and the rows - a dial's one word
            // cannot say that the wheel BECOMES the stick.
            _about = UiKit.Label(UiKit.Rect(full, "about", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-560, -146), new Vector2(560, -120)),
                "", 17, new Color(0.62f, 0.65f, 0.74f), TextAnchor.MiddleCenter);

            UiKit.Hint(UiKit.Rect(full, "h", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 40), new Vector2(0, 82)),
                "[ESC] to go back", "tap BACK to go back", 17,
                new Color(0.5f, 0.53f, 0.62f), TextAnchor.MiddleCenter,
                GamepadGlyphs.Cancel + " to go back");

            Refresh();
        }

        /// <summary>SettingsScreen's row geometry, so the two screens line up row for row.</summary>
        static RectTransform Row(RectTransform parent, int index, Color bg, Color fg, out Text text, int half = 0)
        {
            const float w = 520f, h = UiKit.TouchTarget, gap = 12f;
            float top = 51f;
            float y = top - index * (h + gap);
            float x0 = half > 0 ? gap * 0.5f : -w * 0.5f, x1 = half < 0 ? -gap * 0.5f : w * 0.5f;
            var rt = UiKit.Panel(parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x0, y - h * 0.5f), new Vector2(x1, y + h * 0.5f), bg);
            text = UiKit.Label(rt, "", half == 0 ? 22 : 20, fg, TextAnchor.MiddleCenter);
            return rt;
        }

        void Refresh()
        {
            if (_overlayText == null) return;
            _overlayText.text = Dial("ON-SCREEN CONTROLS", HudPrefs.OnScreen switch
            {
                HudPrefs.Overlay.Always => "ALWAYS",
                HudPrefs.Overlay.Off => "OFF",
                _ => "AUTO",
            });
            _touchText.text = Dial("HUD ON TOUCH", Name(HudPrefs.TouchLayout, touch: true));
            _deskText.text = Dial("HUD ON KEYS & PAD", Name(HudPrefs.DeskLayout, touch: false));
            _sizeText.text = $"SIZE  {HudPrefs.SizeNames[HudPrefs.Size]}";
            _handText.text = HudPrefs.LeftHanded ? "LEFT-HANDED" : "RIGHT-HANDED";
            _keysText.text = Dial("KEY HINTS", HudPrefs.KeyHints ? "ON" : "OFF");

            _about.text = HudPrefs.OnScreen == HudPrefs.Overlay.Off
                ? "With the on-screen controls off, a phone has no stick or buttons at all."
                : HudPrefs.TouchLayout == HudPrefs.Layout.Wheel
                    ? "On touch the rotation wheel is the movement stick: drag its hub to move."
                    : "On touch the plain stick moves you, and the wheel sits beside the seal.";
        }

        static string Name(HudPrefs.Layout l, bool touch)
            => l == HudPrefs.Layout.Centred ? "CENTRED" : touch ? "WHEEL STICK" : "WHEEL LEFT";

        static string Dial(string name, string value) => $"<   {name}  {value}   >";

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!IsOpen) return;
            if (Time.frameCount == _openedFrame) return;

            _focus.Clear();
            _focus.Add(_overlayRect); _focus.Add(_touchRect); _focus.Add(_deskRect);
            _focus.Add(_sizeRect); _focus.Add(_handRect); _focus.Add(_keysRect); _focus.Add(_backRect);
            Controls.SetFocusCandidates(_focus);

            // Spent here, so SETTINGS - reopened by the hand-off this same frame - does not read
            // the same Escape as its own and close as well.
            if (Controls.CancelTapped) { Controls.ConsumeCancel(); Back(); return; }

            if (!Controls.Tapped(out var p)) return;
            if (Hit(_backRect, p)) { Back(); return; }
            if (Hit(_overlayRect, p)) HudPrefs.OnScreen = (HudPrefs.Overlay)Step((int)HudPrefs.OnScreen, 3, _overlayRect, p);
            else if (Hit(_touchRect, p)) HudPrefs.TouchLayout = (HudPrefs.Layout)Step((int)HudPrefs.TouchLayout, 2, _touchRect, p);
            else if (Hit(_deskRect, p)) HudPrefs.DeskLayout = (HudPrefs.Layout)Step((int)HudPrefs.DeskLayout, 2, _deskRect, p);
            else if (Hit(_sizeRect, p)) HudPrefs.Size = Step(HudPrefs.Size, 3, _sizeRect, p);
            else if (Hit(_handRect, p)) HudPrefs.LeftHanded = !HudPrefs.LeftHanded;
            else if (Hit(_keysRect, p)) HudPrefs.KeyHints = !HudPrefs.KeyHints;
            else return;
            Audio.Sfx.Play(Audio.Sound.HitLight);
            Refresh();
        }

        void Back()
        {
            var cb = _onBack;
            cb?.Invoke();
        }

        /// <summary>Left of the row's middle steps back, the middle and right forward, wrapping
        /// either way - SettingsScreen.Step's rule, for a dial of <paramref name="count"/> stops.</summary>
        static int Step(int v, int count, RectTransform rt, Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screen, null, out var local);
            bool down = local.x < rt.rect.center.x - rt.rect.width * 0.1f;
            return ((v + (down ? -1 : 1)) % count + count) % count;
        }

        static bool Hit(RectTransform rt, Vector2 point)
            => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, point, null);
    }
}
