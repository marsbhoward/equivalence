using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Convergence.UI
{
    /// <summary>
    /// A question with a LIST of answers - ConfirmDialog's look and input rules, for more than three:
    /// which rotation socket an Art form is inscribed on, which form to take out of a stacked slot
    /// (the exchange's Phase 2). Each answer is one wide button with a line of detail under it.
    ///
    /// A null <c>cancelLabel</c> makes it a question that must be answered (no Esc, no back).
    /// Every answer registers as a focus candidate, the first focused on a pad.
    /// </summary>
    public class ChoiceList : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        GameObject _root;
        readonly List<RectTransform> _answers = new();
        RectTransform _cancelRect;
        Action<int> _onPick;
        Action _onCancel;
        int _openedFrame = -1;

        public void Show(Transform canvas, string title, string body, IList<(string Label, string Detail)> answers,
                         Action<int> onPick, string cancelLabel = null, Action onCancel = null)
        {
            if (IsOpen) Close();
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _onPick = onPick;
            _onCancel = onCancel;
            Core.GamePause.Hold(this);

            _root = new GameObject("ChoiceList", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;
            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.025f, 0.04f, 0.92f));

            int rows = answers.Count + (cancelLabel != null ? 1 : 0);
            float rowH = 104f, gap = 12f, head = 150f;
            float h = head + rows * (rowH + gap) + 24f;
            var box = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-460, -h * 0.5f), new Vector2(460, h * 0.5f), new Color(0.08f, 0.09f, 0.12f, 1f));

            UiKit.Label(UiKit.Rect(box, "t", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(0, -18)),
                title, 30, new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleCenter);
            var bodyTxt = UiKit.Label(UiKit.Rect(box, "b", new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -head + 6), new Vector2(-40, -72)),
                body, 17, new Color(0.68f, 0.71f, 0.78f), TextAnchor.UpperCenter);
            bodyTxt.horizontalOverflow = HorizontalWrapMode.Wrap;

            _answers.Clear();
            float y = -head;
            for (int i = 0; i < answers.Count; i++)
            {
                var b = UiKit.Panel(box, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, y - rowH), new Vector2(-40, y),
                    new Color(0.13f, 0.15f, 0.19f, 1f));
                UiKit.Label(UiKit.Rect(b, "l", new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(20, 0), new Vector2(-20, -8)),
                    answers[i].Label, 21, new Color(0.94f, 0.88f, 0.70f), TextAnchor.MiddleLeft);
                var d = UiKit.Label(UiKit.Rect(b, "d", new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(20, 8), new Vector2(-20, 0)),
                    answers[i].Detail ?? "", 15, new Color(0.62f, 0.66f, 0.74f), TextAnchor.MiddleLeft);
                d.horizontalOverflow = HorizontalWrapMode.Wrap;
                _answers.Add(b);
                y -= rowH + gap;
            }
            _cancelRect = null;
            if (cancelLabel != null)
            {
                var c = UiKit.Panel(box, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, y - rowH), new Vector2(-40, y),
                    new Color(0.14f, 0.18f, 0.15f, 1f));
                UiKit.Label(UiKit.Rect(c, "c", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                    cancelLabel, 20, new Color(0.6f, 0.9f, 0.65f), TextAnchor.MiddleCenter);
                _cancelRect = c;
            }
        }

        readonly List<RectTransform> _focus = new();

        void Update()
        {
            if (!IsOpen) return;
            _focus.Clear();
            _focus.AddRange(_answers);
            if (_cancelRect) _focus.Add(_cancelRect);
            Core.Controls.SetFocusCandidates(_focus);
            if (Time.frameCount == _openedFrame) return;

            if (_cancelRect != null && Core.Controls.CancelTapped) { Cancel(); return; }
            if (!Core.Controls.Tapped(out var p)) return;
            for (int i = 0; i < _answers.Count; i++)
                if (_answers[i] && RectTransformUtility.RectangleContainsScreenPoint(_answers[i], p, null)) { Pick(i); return; }
            if (_cancelRect && RectTransformUtility.RectangleContainsScreenPoint(_cancelRect, p, null)) Cancel();
        }

        void Pick(int i)
        {
            var pick = _onPick;
            Close();
            pick?.Invoke(i);
        }

        void Cancel()
        {
            var cancel = _onCancel;
            Close();
            cancel?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Core.GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _answers.Clear();
            _cancelRect = null;
        }

        void OnDisable() => Close();
    }
}
