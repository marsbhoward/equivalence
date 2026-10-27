using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;
using Convergence.Hub;

namespace Convergence.UI
{
    /// <summary>
    /// The crate collection's HUB tab (the user's group 7 pick: chosen in the collection): pick a hub
    /// item down the left, then the theme it wears from the themes this character OWNS on the right -
    /// each card is that item drawn in that theme. The top row dresses every item at once. Only owned
    /// themes are offered; the Cloister always is.
    /// </summary>
    public class HubThemeScreen : MonoBehaviour
    {
        public static bool IsOpen => _open != null && _open._root != null;
        static HubThemeScreen _open;

        GameObject _root;
        RectTransform _full, _grid;
        readonly List<(RectTransform Rect, int Item)> _rows = new();
        readonly List<(RectTransform Rect, HubTheme Theme)> _cards = new();
        readonly List<RectTransform> _focus = new();
        Chain.HubLook _look;
        List<HubTheme> _owned;
        int _item = -1;   // -1: every item
        Action<int, HubTheme> _onPick;
        Action _onBack;
        int _openedFrame;

        /// <summary>Open over <paramref name="canvas"/>. <paramref name="onPick"/> gets (item, theme) -
        /// item -1 for every item; <paramref name="onBack"/> returns to the collection.</summary>
        public static void Open(Transform canvas, Chain.HubLook look, List<HubTheme> owned, Action<int, HubTheme> onPick, Action onBack)
        {
            if (IsOpen) return;
            var go = new GameObject("HubThemeScreen", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            _open = go.AddComponent<HubThemeScreen>();
            _open.Build(look, owned, onPick, onBack);
        }

        public static void Close()
        {
            if (_open == null) return;
            GamePause.Release(_open);
            Destroy(_open.gameObject);
            _open = null;
        }

        void Build(Chain.HubLook look, List<HubTheme> owned, Action<int, HubTheme> onPick, Action onBack)
        {
            _look = look; _owned = owned; _onPick = onPick; _onBack = onBack; _openedFrame = Time.frameCount;
            GamePause.Hold(this);
            _root = new GameObject("root", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            _full = (RectTransform)_root.transform;
            _full.anchorMin = Vector2.zero; _full.anchorMax = Vector2.one; _full.offsetMin = _full.offsetMax = Vector2.zero;
            ((RectTransform)transform).anchorMin = Vector2.zero; ((RectTransform)transform).anchorMax = Vector2.one;
            Redraw();
        }

        void Redraw()
        {
            foreach (Transform c in _full) Destroy(c.gameObject);
            _rows.Clear(); _cards.Clear();
            UiKit.Panel(_full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.03f, 0.032f, 0.045f, 0.97f));
            UiKit.Label(UiKit.Rect(_full, "t", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -108), new Vector2(0, -48)),
                "THE COLLECTION  -  HUB", 40, new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleCenter);
            UiKit.Label(UiKit.Rect(_full, "s", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -142), new Vector2(0, -108)),
                $"{_owned.Count - 1} of {HubThemes.Count - 1} themes earned    -    BACK or [ESC] to the collection",
                18, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter);

            // The items, down the left.
            float y = 300f;
            for (int i = -1; i < HubThemes.ItemCount; i++)
            {
                bool sel = i == _item;
                var row = UiKit.Panel(_full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-760, y - 36), new Vector2(-380, y), sel ? new Color(0.22f, 0.2f, 0.14f) : new Color(0.1f, 0.11f, 0.15f));
                string name = i < 0 ? "Every item" : HubThemes.NameOf((HubItem)i);
                string now = i < 0 ? "" : HubThemes.NameOf(Current(i));
                UiKit.Label(UiKit.Rect(row, "n", Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0)),
                    i < 0 ? name : $"{name}   <color=#8a8f9c>{now}</color>", 17, sel ? new Color(1f, 0.86f, 0.4f) : new Color(0.82f, 0.84f, 0.9f), TextAnchor.MiddleLeft);
                _rows.Add((row, i));
                y -= 40f;
            }

            // The owned themes, each shown as the chosen item drawn in it.
            const int cols = 4; const float cell = 210f, gap = 18f;
            for (int k = 0; k < _owned.Count; k++)
            {
                var th = _owned[k];
                int col = k % cols, rowN = k / cols;
                float x = -330f + col * (cell + gap), top = 300f - rowN * (cell + gap + 30f);
                bool wearing = _item >= 0 && Current(_item) == th;
                var card = UiKit.Panel(_full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, top - cell), new Vector2(x + cell, top), wearing ? new Color(0.22f, 0.2f, 0.14f) : new Color(0.11f, 0.12f, 0.16f));
                var art = UiKit.Rect(card, "art", Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -12));
                var img = art.gameObject.AddComponent<Image>();
                img.sprite = HubThemeArt.Preview(_item < 0 ? HubItem.Room : (HubItem)_item, th);
                img.preserveAspect = true; img.raycastTarget = false;
                UiKit.Label(UiKit.Rect(card, "n", new Vector2(0, 0), new Vector2(1, 0), new Vector2(4, -28), new Vector2(-4, -2)),
                    HubThemes.NameOf(th) + (wearing ? "  (wearing)" : ""), 15, new Color(0.8f, 0.83f, 0.9f), TextAnchor.MiddleCenter);
                _cards.Add((card, th));
            }
        }

        HubTheme Current(int item)
        {
            int t = _look?.Get(item) ?? 0;
            var theme = t > 0 && t < HubThemes.Count ? (HubTheme)t : HubTheme.Cloister;
            return _owned.Contains(theme) ? theme : HubTheme.Cloister;
        }

        void Update()
        {
            if (_root == null) return;
            _focus.Clear();
            foreach (var (r, _) in _rows) _focus.Add(r);
            foreach (var (r, _) in _cards) _focus.Add(r);
            Controls.SetFocusCandidates(_focus);
            if (Time.frameCount == _openedFrame) return;

            if (Controls.CancelTapped)
            {
                Controls.ConsumeCancel();
                var back = _onBack; Close(); back?.Invoke();
                return;
            }
            if (!Controls.Tapped(out var at)) return;
            foreach (var (r, item) in _rows)
                if (RectTransformUtility.RectangleContainsScreenPoint(r, at, null)) { _item = item; Redraw(); return; }
            foreach (var (r, th) in _cards)
                if (RectTransformUtility.RectangleContainsScreenPoint(r, at, null))
                {
                    if (_item < 0) _look?.SetAll((int)th); else _look?.Set(_item, (int)th);
                    _onPick?.Invoke(_item, th);
                    Redraw();
                    return;
                }
        }
    }
}
