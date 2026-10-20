using System;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// THE SHORT DESCENT's ENDING (SHORT-DESCENT.md, "Between floors" - the user's call: no deal or
    /// reward at the final floor; the avatar's words, then a skippable cut scene, then the run's
    /// summary). Shown by GameBootstrap once the final floor is cleared and written down; the run
    /// ends (an extraction) when it closes.
    ///
    ///     WORDS       the screen dims, the avatar's line(s) fade up one at a time
    ///                 (Core.Narrative - drafts, the user's to write); a press moves on
    ///     THE LIGHT   blinding white rises from the north - the sigil door's own light - and a
    ///                 card names how the run ended against the gate, with its run clock
    ///
    /// SKIPPABLE: Cancel / Menu (Esc, a pad's B, the touch BACK) ends it at once; any other press
    /// moves to the next beat. Unscaled time throughout - the game is held paused behind it.
    /// </summary>
    public class EndingScreen : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        const float LineIn = 0.8f, LineHold = 3.2f, LineOut = 0.6f;
        const float LightRise = 2.4f, CardIn = 0.8f, CardHold = 3.5f;

        enum Beat { Words, Light, Card, Done }

        GameObject _root;
        Image _veil, _light;
        Text _line, _card, _cardSub, _hint;
        string[] _words;
        string _cardText, _cardSubText;
        int _wordIndex;
        Beat _beat;
        float _beatAt;
        int _openedFrame;
        float _openedAt;
        Action _done;

        public void Show(Transform canvas, string[] words, float runClockSeconds, Action onDone)
        {
            Close();
            _words = words ?? Array.Empty<string>();
            _done = onDone;
            int s = Mathf.FloorToInt(runClockSeconds);
            _cardText = RunClock.InCollapse(runClockSeconds) ? Narrative.EndingCollapsed : Narrative.EndingInTime;
            _cardSubText = $"RUN CLOCK  {s / 60}:{s % 60:00}   -   {RunClock.TierName(RunClock.TierAt(runClockSeconds)).ToUpperInvariant()}";

            _root = new GameObject("Ending", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            _veil = UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0)).GetComponent<Image>();
            // The light: a white sheet that rises from the top edge down over the screen.
            _light = UiKit.Panel(full, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero, new Color(1, 1, 1, 0)).GetComponent<Image>();
            _light.rectTransform.pivot = new Vector2(0.5f, 1f);

            var mid = new Vector2(0.5f, 0.5f);
            _line = UiKit.Label(UiKit.Rect(full, "line", mid, mid, new Vector2(-700, -60), new Vector2(700, 60)),
                "", 40, new Color(0.92f, 0.9f, 0.86f, 0f), TextAnchor.MiddleCenter);
            _line.fontStyle = FontStyle.Italic;
            _card = UiKit.Label(UiKit.Rect(full, "card", mid, mid, new Vector2(-800, 10), new Vector2(800, 110)),
                _cardText, 64, new Color(0.12f, 0.1f, 0.08f, 0f), TextAnchor.MiddleCenter);
            _card.fontStyle = FontStyle.Bold;
            _cardSub = UiKit.Label(UiKit.Rect(full, "sub", mid, mid, new Vector2(-800, -60), new Vector2(800, 0)),
                _cardSubText, 26, new Color(0.3f, 0.24f, 0.18f, 0f), TextAnchor.MiddleCenter);
            _hint = UiKit.Hint(UiKit.Rect(full, "hint", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(-500, 30), new Vector2(500, 60)),
                "any key - continue      ESC - skip", "tap - continue      BACK - skip", 18,
                new Color(0.6f, 0.6f, 0.66f, 0.7f), TextAnchor.MiddleCenter,
                GamepadGlyphs.Interact + " continue      " + GamepadGlyphs.Menu + " skip");

            GamePause.Hold(this);
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _openedAt = Time.unscaledTime;
            _wordIndex = 0;
            Enter(_words.Length > 0 ? Beat.Words : Beat.Light);
        }

        public void Close()
        {
            if (_root) Destroy(_root);
            _root = null;
            if (IsOpen) GamePause.Release(this);
            IsOpen = false;
        }

        void Enter(Beat beat)
        {
            _beat = beat;
            _beatAt = Time.unscaledTime;
        }

        void Finish()
        {
            var done = _done;
            _done = null;
            Close();
            done?.Invoke();
        }

        void Update()
        {
            if (!IsOpen) return;
            float t = Time.unscaledTime - _beatAt;
            bool fresh = Time.frameCount != _openedFrame;
            if (fresh && (Controls.CancelTapped || Controls.MenuTapped)) { Finish(); return; }
            bool next = fresh && (Controls.ConfirmTapped || Controls.AttackTapped || Controls.InteractTapped);

            // The veil darkens over the first moments and stays.
            _veil.color = new Color(0, 0, 0, Mathf.Clamp01((Time.unscaledTime - _openedAt) / 1.2f) * 0.72f);

            switch (_beat)
            {
                case Beat.Words:
                {
                    UiKit.SetText(_line, _words[_wordIndex]);
                    float a = t < LineIn ? t / LineIn
                            : t < LineIn + LineHold ? 1f
                            : 1f - (t - LineIn - LineHold) / LineOut;
                    var c = _line.color; c.a = Mathf.Clamp01(a); _line.color = c;
                    if (next || t >= LineIn + LineHold + LineOut)
                    {
                        _wordIndex++;
                        if (_wordIndex >= _words.Length) { c.a = 0f; _line.color = c; Enter(Beat.Light); }
                        else Enter(Beat.Words);
                    }
                    break;
                }
                case Beat.Light:
                {
                    float k = Mathf.Clamp01(t / LightRise);
                    float eased = k * k * (3f - 2f * k);
                    var rt = _light.rectTransform;
                    rt.offsetMin = new Vector2(0, -Screen.height * 2f * eased);
                    rt.offsetMax = Vector2.zero;
                    _light.color = new Color(1f, 0.98f, 0.94f, eased);
                    if (next || k >= 1f)
                    {
                        _light.rectTransform.anchorMin = Vector2.zero;
                        _light.color = new Color(1f, 0.98f, 0.94f, 1f);
                        Enter(Beat.Card);
                    }
                    break;
                }
                case Beat.Card:
                {
                    float a = Mathf.Clamp01(t / CardIn);
                    _card.color = new Color(0.12f, 0.1f, 0.08f, a);
                    _cardSub.color = new Color(0.3f, 0.24f, 0.18f, a);
                    if ((next && t > 0.3f) || t >= CardIn + CardHold) Finish();
                    break;
                }
            }
        }

        void OnDisable() => Close();
    }
}
