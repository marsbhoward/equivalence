using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The terminal's screen: every checkpoint written this session, newest first.
    ///
    /// This exists because the game's writes are otherwise invisible. Three checkpoints per run
    /// and an account record behind them all happen silently, and on a chain-backed game "did
    /// that save" is a question the player is entitled to an answer to. Rebuilt on every TxLog
    /// change so a write that settles while the screen is open updates in place.
    /// </summary>
    public class TxScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        GameObject _root;
        RectTransform _list;
        Transform _canvas;

        static readonly Color Ink = new(0.36f, 0.92f, 0.66f);
        static readonly Color Dim = new(0.45f, 0.50f, 0.52f);
        static readonly Color Warn = new(0.95f, 0.72f, 0.30f);
        static readonly Color Bad = new(0.90f, 0.42f, 0.35f);

        public void Toggle(Transform canvas) { if (IsOpen) Close(); else Open(canvas); }

        public void Open(Transform canvas)
        {
            if (IsOpen) return;
            IsOpen = true;
            _canvas = canvas;
            GamePause.Hold(this);
            TxLog.Changed += Refresh;
            Build();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            TxLog.Changed -= Refresh;
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _list = null;
        }

        void Refresh()
        {
            if (!IsOpen) return;
            if (_root) Destroy(_root);
            Build();
        }

        void Build()
        {
            _root = new GameObject("TxScreen", typeof(RectTransform));
            _root.transform.SetParent(_canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.05f, 0.045f, 0.97f));

            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(64, -112), new Vector2(-64, -56)),
                "SESSION LEDGER", 34, Ink);

            int pending = TxLog.PendingCount;
            int total = TxLog.All.Count;
            UiKit.Label(UiKit.Rect(full, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(66, -142), new Vector2(-64, -112)),
                total == 0
                    ? "no checkpoints written yet this session"
                    : $"{total} checkpoint{(total == 1 ? "" : "s")}" +
                      (pending > 0 ? $"   -   {pending} settling" : "   -   all confirmed"),
                17, pending > 0 ? Warn : Dim);

            _list = UiKit.Rect(full, "list", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(64, 88), new Vector2(-64, -160));

            // Newest first: the thing you came to check is almost always the last thing that
            // happened.
            float y = 0f;
            for (int i = TxLog.All.Count - 1; i >= 0; i--)
            {
                y = Row(_list, TxLog.All[i], y);
                if (y < -760f) break;   // beyond the panel; no scrolling yet
            }

            UiKit.Hint(UiKit.Rect(full, "f", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(64, 36), new Vector2(-64, 72)),
                "[E] or [ESC] to step away    -    this log is not saved; the datum on disk is the record",
                "BACK to step away    -    this log is not saved; the datum on disk is the record",
                16, Dim, TextAnchor.MiddleCenter,
                UI.GamepadGlyphs.Cancel + " to step away    -    this log is not saved; the datum on disk is the record");
        }

        float Row(RectTransform parent, TxRecord tx, float y)
        {
            var card = UiKit.Panel(parent, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, y - 92), new Vector2(0, y - 6), new Color(0.05f, 0.10f, 0.09f));

            var state = tx.State switch
            {
                TxState.Confirmed => (text: "CONFIRMED", tint: Ink),
                TxState.Failed => (text: "FAILED", tint: Bad),
                _ => (text: "PENDING", tint: Warn),
            };

            var stripe = UiKit.Rect(card, "stripe", new Vector2(0, 0), new Vector2(0, 1),
                Vector2.zero, new Vector2(4, 0)).gameObject.AddComponent<Image>();
            stripe.color = state.tint;

            UiKit.Label(UiKit.Rect(card, "k", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(18, -34), new Vector2(190, -8)),
                TxLog.Label(tx.Kind), 15, state.tint);

            UiKit.Label(UiKit.Rect(card, "st", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-160, -34), new Vector2(-18, -8)),
                state.text, 14, state.tint, TextAnchor.MiddleRight);

            UiKit.Label(UiKit.Rect(card, "sm", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18, -60), new Vector2(-18, -34)),
                tx.Summary, 19, new Color(0.85f, 0.90f, 0.88f));

            // The detail is the datum: multi-line in the record, one line here, because the point
            // is that you can see WHAT was written, not read it as a document.
            string detail = (tx.Detail ?? "").Replace("\n", "   ");
            UiKit.Label(UiKit.Rect(card, "d", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18, -84), new Vector2(-18, -60)),
                detail, 14, Dim);

            if (!string.IsNullOrEmpty(tx.Hash))
                UiKit.Label(UiKit.Rect(card, "h", new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-320, -60), new Vector2(-18, -34)),
                    tx.Hash, 14, new Color(0.30f, 0.55f, 0.45f), TextAnchor.MiddleRight);

            return y - 98f;
        }

        void Update()
        {
            if (!IsOpen) return;
            if (Core.Controls.CancelTapped || Core.Controls.InteractTapped)
                Close();
        }
    }
}
