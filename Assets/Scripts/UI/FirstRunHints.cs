using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// FIRST-RUN HINTS - the onboarding (SHOWCASE.md step 6; the user's picks, 2026-10-31, mock
    /// "Onboarding Hints"): a CALLOUT that PAUSES. The game holds, a light dim falls, and a small
    /// gold-lined tag names what to do, with a fine gold line to the thing it is about. Any press
    /// goes on - and the game resumes when that press is LET GO, so a tap on ATTACK over the
    /// timing bar is never also the strike or a swing (<see cref="Controls.WorldBlocked"/>).
    ///
    /// Six moments, each shown once per CHARACTER (<see cref="Chain.CharacterProfile.HintsSeen"/>),
    /// asked for by GameBootstrap where they happen: move, bar, aim, guard, exchange, rift. Never
    /// two at once, none in a floor's first seconds, a gap between two (<see cref="Tuning.Hints"/>).
    /// HUD &amp; TOUCH turns them off; turning them back on shows them all again.
    ///
    /// Its own canvas at 16: over the screens (10) - the exchange hint sits on the deal screen -
    /// and the touch canvas's modal BACK (15), under the gamepad cursor (20). A modal in
    /// GameBootstrap.Update's guard, with its tag as the pad's one focus rect, so A goes on.
    /// </summary>
    public class FirstRunHints : MonoBehaviour
    {
        /// <summary>Open, or closed but a button still down: the world and the screens under it
        /// keep their hands off until the frame after it is let go.</summary>
        public static bool Blocking => _instance != null && (_instance._open || Time.frameCount <= _instance._clearedFrame);
        public static bool IsOpen => _instance != null && _instance._open;

        /// <summary>The character whose hints these are - set by GameBootstrap.</summary>
        public static System.Func<Chain.CharacterProfile> Profile;

        static FirstRunHints _instance;

        RectTransform _root, _box, _line, _dot;
        Text _text, _goOn;
        bool _open, _closing, _worldPrev;
        float _openedAt, _closedAt = float.NegativeInfinity;
        int _clearedFrame = -1;
        int _floor = int.MinValue;
        float _floorSince;
        readonly List<RectTransform> _focus = new();

        static readonly Color Gold = new(0.85f, 0.71f, 0.35f, 0.95f);

        public static FirstRunHints Build(Transform parent)
        {
            var canvas = UiKit.CreateCanvas("hints", 16);
            canvas.transform.SetParent(parent, false);
            var h = canvas.gameObject.AddComponent<FirstRunHints>();
            h.Construct((RectTransform)canvas.transform);
            _instance = h;
            return h;
        }

        void Construct(RectTransform canvas)
        {
            _root = UiKit.Rect(canvas, "hint", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UiKit.Panel(_root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.03f, 0.02f, 0.05f, 0.35f));

            _line = UiKit.Panel(_root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Gold);
            _line.pivot = new Vector2(0f, 0.5f);
            _dot = UiKit.Panel(_root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-5f, -5f), new Vector2(5f, 5f), Gold);

            // The tag: a gold edge, a dark glass face inside it, the line and the "go on" under it.
            _box = UiKit.Panel(_root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Gold);
            UiKit.Panel(_box, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f), new Color(0.07f, 0.06f, 0.09f, 0.92f));
            _text = UiKit.Label(UiKit.Rect(_box, "t", new Vector2(0f, 0.42f), Vector2.one, new Vector2(24f, 0f), new Vector2(-24f, -8f)),
                                "HINT", 26, Gold, TextAnchor.MiddleCenter);
            _goOn = UiKit.Label(UiKit.Rect(_box, "g", Vector2.zero, new Vector2(1f, 0.42f), new Vector2(24f, 8f), new Vector2(-24f, 0f)),
                                "TAP TO GO ON", 16, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter);
            _root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ asking

        /// <summary>
        /// Whether a hint could open now: on, none open or draining, past the floor's quiet
        /// seconds and the gap since the last. <paramref name="floor"/> is the run's floor, so a
        /// new floor restarts the quiet; a screen's hint (the exchange) passes ignoreQuiet.
        /// </summary>
        public static bool Ready(int floor, bool ignoreQuiet = false)
        {
            var h = _instance;
            if (h == null || !HudPrefs.FirstRunHints || Profile?.Invoke() == null) return false;
            if (floor != h._floor) { h._floor = floor; h._floorSince = Time.unscaledTime; }
            if (Blocking) return false;
            if (Time.unscaledTime - h._closedAt < Tuning.Hints.GapSeconds) return false;
            return ignoreQuiet || Time.unscaledTime - h._floorSince >= Tuning.Hints.FloorQuietSeconds;
        }

        public static bool Seen(string key)
        {
            var p = Profile?.Invoke();
            return p == null || (p.HintsSeen != null && p.HintsSeen.Contains(key));
        }

        /// <summary>
        /// Show <paramref name="key"/>'s hint unless this character has seen it. Call only when
        /// <see cref="Ready"/>. <paramref name="screen"/> is what it is about, in screen pixels,
        /// or null for a tag with no line; <paramref name="avoid"/> a point the tag must not cover
        /// (the player, when the hint is about something else). True when it opened.
        /// </summary>
        public static bool Offer(string key, string text, Vector2? screen, Vector2? avoid = null)
        {
            var h = _instance;
            if (h == null || Seen(key)) return false;
            var p = Profile();
            p.HintsSeen ??= new List<string>();
            p.HintsSeen.Add(key);   // saved with the character's next save
            h.Show(text, screen, avoid);
            return true;
        }

        /// <summary>Every hint again - HUD &amp; TOUCH turning them back on.</summary>
        public static void ClearSeen() => Profile?.Invoke()?.HintsSeen?.Clear();

        /// <summary>A world point in screen pixels, for <see cref="Offer"/>.</summary>
        public static Vector2? OnScreen(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var s = cam.WorldToScreenPoint(world);
            return s.z < 0f ? null : (Vector2)s;
        }

        // ------------------------------------------------------------ showing

        void Show(string text, Vector2? screen, Vector2? avoid)
        {
            _text.text = text;
            _goOn.text = Controls.TouchMode ? "TAP TO GO ON"
                       : Controls.GamepadMode ? GamepadGlyphs.Confirm + " TO GO ON" : "CLICK OR ENTER TO GO ON";
            _root.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Place(screen, avoid);

            _open = true;
            _closing = false;
            _openedAt = Time.unscaledTime;
            _worldPrev = true;   // a button already down is not a press - it must be let go first
            Controls.WorldBlocked = true;
            GamePause.Hold(this);
        }

        /// <summary>The tag beside the target - the side nearer the middle first, then the other,
        /// then above and below - at the first spot that stays on screen and clear of
        /// <paramref name="avoid"/>, with the line from its edge to a dot on the target. With no
        /// target, the tag sits in the upper middle of the screen on its own.</summary>
        void Place(Vector2? screen, Vector2? avoid)
        {
            var size = _root.rect.size;
            float w = Mathf.Clamp(_text.preferredWidth + 48f, 360f, size.x - 80f);
            const float h = 104f;
            _box.sizeDelta = new Vector2(w, h);
            _box.offsetMin = new Vector2(-w * 0.5f, -h * 0.5f);
            _box.offsetMax = new Vector2(w * 0.5f, h * 0.5f);

            Vector2 t = default;
            bool aimed = screen.HasValue
                         && RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen.Value, null, out t);
            _line.gameObject.SetActive(aimed);
            _dot.gameObject.SetActive(aimed);
            if (!aimed) { _box.anchoredPosition = new Vector2(0f, size.y * 0.22f); return; }

            Vector2 a = default;
            bool avoiding = avoid.HasValue
                            && RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, avoid.Value, null, out a);
            const float gap = 90f, clear = 70f;
            float near = t.x > 0f ? -1f : 1f;
            var spots = new[]
            {
                new Vector2(t.x + near * (gap + w * 0.5f), t.y + 30f),
                new Vector2(t.x - near * (gap + w * 0.5f), t.y + 30f),
                new Vector2(t.x, t.y + gap + h * 0.5f),
                new Vector2(t.x, t.y - gap - h * 0.5f),
            };
            Vector2 c = spots[0];
            foreach (var s in spots)
            {
                var k = new Vector2(Mathf.Clamp(s.x, -size.x * 0.5f + w * 0.5f + 24f, size.x * 0.5f - w * 0.5f - 24f),
                                    Mathf.Clamp(s.y, -size.y * 0.5f + h * 0.5f + 24f, size.y * 0.5f - h * 0.5f - 24f));
                bool covers = avoiding && Mathf.Abs(a.x - k.x) < w * 0.5f + clear && Mathf.Abs(a.y - k.y) < h * 0.5f + clear;
                bool coversTarget = Mathf.Abs(t.x - k.x) < w * 0.5f + 20f && Mathf.Abs(t.y - k.y) < h * 0.5f + 20f;
                if (covers || coversTarget) continue;
                c = k;
                break;
            }
            _box.anchoredPosition = c;

            // The line leaves the tag's nearest edge: a side for a tag beside, top or bottom for one above or below.
            bool beside = Mathf.Abs(c.y - t.y) < h * 0.5f + 40f;
            var from = beside ? new Vector2(c.x + Mathf.Sign(t.x - c.x) * w * 0.5f, c.y)
                              : new Vector2(c.x, c.y + Mathf.Sign(t.y - c.y) * h * 0.5f);
            _dot.anchoredPosition = t;
            var to = t - (t - from).normalized * 10f;
            var d = to - from;
            _line.anchoredPosition = from;
            _line.sizeDelta = new Vector2(Mathf.Max(0f, d.magnitude), 3f);
            _line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }

        void Update()
        {
            if (!_open) return;

            bool held = Controls.WorldPressed || Controls.PointerHeld(out _);
            if (_closing)
            {
                if (held) return;
                _open = _closing = false;
                _clearedFrame = Time.frameCount + 1;
                _closedAt = Time.unscaledTime;
                Controls.WorldBlocked = false;
                GamePause.Release(this);
                return;
            }

            _focus.Clear();
            _focus.Add(_box);
            Controls.SetFocusCandidates(_focus);

            bool world = Controls.WorldPressed;
            bool worldEdge = world && !_worldPrev;
            _worldPrev = world;
            bool tapped = Controls.Tapped(out _), confirm = Controls.ConfirmTapped, cancel = Controls.CancelTapped;
            if (cancel) Controls.ConsumeCancel();
            if (Time.unscaledTime - _openedAt < Tuning.Hints.ReadSeconds) return;
            if (!(worldEdge || tapped || confirm || cancel)) return;

            // Gone from view on the press; the game waits for the let-go (above).
            _root.gameObject.SetActive(false);
            _closing = true;
        }

        void OnDestroy()
        {
            if (_instance != this) return;
            if (_open) { Controls.WorldBlocked = false; GamePause.Release(this); }
            _instance = null;
        }
    }
}
