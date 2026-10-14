using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Convergence.UI
{
    /// <summary>
    /// A small yes/no prompt for decisions that throw work away. Pauses like the other screens.
    ///
    /// Deliberately generic: abandoning a run is the first user, but false starts and profile
    /// transfers are the same shape of question and should not each grow their own dialog.
    /// </summary>
    public class ConfirmDialog : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        GameObject _root;
        RectTransform _confirmRect, _cancelRect, _altRect;
        int _openedFrame = -1;
        Action _onConfirm, _onCancel, _onAlt;

        public void Show(Transform canvas, string title, string body,
                         string confirmLabel, string cancelLabel,
                         Action onConfirm, Action onCancel = null)
            => Open(canvas, title, body, confirmLabel, null, cancelLabel, onConfirm, null, onCancel);

        /// <summary>
        /// Three answers: the destructive one (left, red), a middle one, and backing out (right).
        /// Here ENTER takes the MIDDLE answer, not the destructive one - the middle is what the
        /// question is usually for (suspend the run, resume it), and a keystroke should never be
        /// the way a run is thrown away. The destructive answer is a click or the pad.
        /// </summary>
        public void ShowChoice(Transform canvas, string title, string body,
                               string confirmLabel, string altLabel, string cancelLabel,
                               Action onConfirm, Action onAlt, Action onCancel = null)
            => Open(canvas, title, body, confirmLabel, altLabel, cancelLabel, onConfirm, onAlt, onCancel);

        /// <summary>One answer - something the player is told rather than asked.</summary>
        public void ShowNotice(Transform canvas, string title, string body, string okLabel, Action onClose = null)
            => Open(canvas, title, body, null, null, okLabel, null, null, onClose);

        void Open(Transform canvas, string title, string body,
                  string confirmLabel, string altLabel, string cancelLabel,
                  Action onConfirm, Action onAlt, Action onCancel)
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _onConfirm = onConfirm;
            _onAlt = altLabel != null ? onAlt : null;
            _onCancel = onCancel;
            bool three = altLabel != null;

            Core.GamePause.Hold(this);

            _root = new GameObject("ConfirmDialog", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.025f, 0.04f, 0.92f));

            // 72 units deeper than it was, which is exactly what the buttons below gained when
            // they went from 56 tall to a finger. The dialog grows DOWNWARD so the title and body
            // stay where they were.
            var box = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-380, -242), new Vector2(380, 150), new Color(0.08f, 0.09f, 0.12f, 1f));

            var t = UiKit.Rect(box, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -84), new Vector2(0, -26));
            UiKit.Label(t, title, 32, new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleCenter);

            var b = UiKit.Rect(box, "b", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(40, 176), new Vector2(-40, -92));
            var bodyTxt = UiKit.Label(b, body, 18, new Color(0.68f, 0.71f, 0.78f), TextAnchor.UpperCenter);
            bodyTxt.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 128 tall, not 56 - a yes/no on a destructive action is the last place to make the
            // player aim. See UiKit.TouchTarget. Three answers share the same row, narrower.
            float l0 = three ? -350 : -330, l1 = three ? -125 : -20;
            _confirmRect = null;
            if (confirmLabel != null)
            {
                var confirm = UiKit.Panel(box, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(l0, 28), new Vector2(l1, 28 + UiKit.TouchTarget),
                    new Color(0.24f, 0.15f, 0.15f, 1f));
                UiKit.Label(UiKit.Rect(confirm, "c", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                    confirmLabel, 19, new Color(0.94f, 0.62f, 0.58f), TextAnchor.MiddleCenter);
                _confirmRect = confirm;
            }

            _altRect = null;
            if (three)
            {
                var alt = UiKit.Panel(box, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(-105, 28), new Vector2(105, 28 + UiKit.TouchTarget),
                    new Color(0.20f, 0.18f, 0.12f, 1f));
                UiKit.Label(UiKit.Rect(alt, "c", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                    altLabel, 19, new Color(0.96f, 0.84f, 0.52f), TextAnchor.MiddleCenter);
                _altRect = alt;
            }

            float r0 = three ? 125 : confirmLabel == null ? -155 : 20, r1 = three ? 350 : confirmLabel == null ? 155 : 330;
            var cancel = UiKit.Panel(box, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(r0, 28), new Vector2(r1, 28 + UiKit.TouchTarget),
                new Color(0.14f, 0.18f, 0.15f, 1f));
            UiKit.Label(UiKit.Rect(cancel, "c", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                cancelLabel, 19, new Color(0.6f, 0.9f, 0.65f), TextAnchor.MiddleCenter);
            _cancelRect = cancel;
        }

        readonly System.Collections.Generic.List<RectTransform> _focus = new();

        void Update()
        {
            if (!IsOpen) return;

            // The SAFE answer first - it's what a gamepad lands on as the dialog opens, for the
            // same reason Esc backs out: this dialog guards decisions that throw work away.
            _focus.Clear();
            if (_cancelRect) _focus.Add(_cancelRect);
            if (_altRect) _focus.Add(_altRect);
            if (_confirmRect) _focus.Add(_confirmRect);
            Core.Controls.SetFocusCandidates(_focus);

            // Esc opens this dialog, and wasPressedThisFrame stays true for the whole frame - so
            // without skipping the opening frame the dialog read its own opening keystroke as a
            // cancel and vanished before it was ever drawn. Escape looked like it did nothing.
            if (Time.frameCount == _openedFrame) return;

            // Enter confirms, Esc backs out - and Esc is what opened this, so backing out
            // must be the safe default. Both buttons are on screen, so a touch player has the
            // same two answers without either one, and a gamepad's A clicks whichever the D-pad
            // has selected.
            if (Core.Controls.ConfirmTapped)
            {
                Finish(_altRect != null ? Answer.Alt : _confirmRect != null ? Answer.Confirm : Answer.Cancel);
                return;
            }
            if (Core.Controls.CancelTapped) { Finish(Answer.Cancel); return; }

            if (!Core.Controls.Tapped(out var p)) return;
            if (Hit(_confirmRect, p)) Finish(Answer.Confirm);
            else if (Hit(_altRect, p)) Finish(Answer.Alt);
            else if (Hit(_cancelRect, p)) Finish(Answer.Cancel);
        }

        static bool Hit(RectTransform rt, Vector2 point)
            => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, point, null);

        enum Answer { Confirm, Alt, Cancel }

        void Finish(Answer answer)
        {
            var yes = _onConfirm;
            var alt = _onAlt;
            var no = _onCancel;
            Close();
            switch (answer)
            {
                case Answer.Confirm: yes?.Invoke(); break;
                case Answer.Alt: alt?.Invoke(); break;
                default: no?.Invoke(); break;
            }
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Core.GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null; _confirmRect = null; _cancelRect = null; _altRect = null;
            _onConfirm = null; _onCancel = null; _onAlt = null;
        }
    }
}
