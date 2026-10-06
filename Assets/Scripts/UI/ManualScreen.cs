using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The book the Manual Shrine opens: a table of contents on the left, the selected section's
    /// text on the right - a reading screen, not a picker, so choosing a topic never closes it.
    /// Pure content, no gameplay hooked to anything drawn here.
    /// </summary>
    public class ManualScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        const float RowWidth = 420f, RowHeight = 76f, RowGap = 10f;
        const float ColumnTop = 150f;

        GameObject _root;
        RectTransform _detail;
        readonly List<(RectTransform Rect, Image Bg, Text Label)> _rows = new();

        /// <summary>Clickable rects handed to Controls each frame so the D-pad can walk them.
        /// Rebuilt rather than cached: these screens rebuild their contents on open.</summary>
        readonly List<RectTransform> _focusRects = new();
        int _selected;

        static readonly Color RowIdle = new(0.11f, 0.12f, 0.16f);
        static readonly Color RowSelected = new(0.20f, 0.18f, 0.10f);
        static readonly Color TitleIdle = new(0.75f, 0.78f, 0.85f);
        static readonly Color TitleSelected = new(0.95f, 0.85f, 0.55f);

        public void Open(Transform canvas)
        {
            if (IsOpen) return;
            IsOpen = true;
            _selected = 0;
            GamePause.Hold(this);

            _root = new GameObject("ManualScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.032f, 0.045f, 0.97f));

            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -108), new Vector2(0, -48)),
                "THE MANUAL", 40, new Color(0.95f, 0.85f, 0.55f), TextAnchor.MiddleCenter);

            UiKit.Label(UiKit.Rect(full, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -142), new Vector2(0, -108)),
                "pick a section to read    -    BACK or [ESC] to close",
                18, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter);

            var entries = ManualCatalog.Entries;
            float leftX = -960f + 60f;
            for (int i = 0; i < entries.Length; i++)
            {
                float y = ColumnTop - i * (RowHeight + RowGap);
                var row = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(leftX, y - RowHeight), new Vector2(leftX + RowWidth, y),
                    RowIdle);
                var bg = row.GetComponent<Image>();

                var label = UiKit.Label(UiKit.Rect(row, "n", Vector2.zero, Vector2.one,
                    new Vector2(20, 0), new Vector2(-16, 0)),
                    entries[i].Title, 19, TitleIdle, TextAnchor.MiddleLeft);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;

                _rows.Add((row, bg, label));
            }

            _detail = UiKit.Rect(full, "detail", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(leftX + RowWidth + 50f, -470f), new Vector2(960f - 60f, ColumnTop));

            ShowEntry(0);
        }

        void ShowEntry(int index)
        {
            _selected = index;
            var entries = ManualCatalog.Entries;

            for (int i = 0; i < _rows.Count; i++)
            {
                bool on = i == index;
                _rows[i].Bg.color = on ? RowSelected : RowIdle;
                _rows[i].Label.color = on ? TitleSelected : TitleIdle;
            }

            for (int i = _detail.childCount - 1; i >= 0; i--)
                Destroy(_detail.GetChild(i).gameObject);

            var entry = entries[index];
            UiKit.Label(UiKit.Rect(_detail, "title", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -56), new Vector2(0, 0)),
                entry.Title, 26, new Color(0.95f, 0.85f, 0.55f), TextAnchor.UpperLeft);

            var body = UiKit.Label(UiKit.Rect(_detail, "body", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(0, 0), new Vector2(0, -70)),
                entry.Body, 20, new Color(0.85f, 0.86f, 0.90f), TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _detail = null;
            _rows.Clear();
        }

        void Update()
        {
            if (!IsOpen) return;

            _focusRects.Clear();
            foreach (var r in _rows) _focusRects.Add(r.Rect);
            Core.Controls.SetFocusCandidates(_focusRects);

            if (Core.Controls.CancelTapped || Core.Controls.InteractTapped)
            {
                Close();
                return;
            }

            if (!Core.Controls.Tapped(out var at)) return;

            for (int i = 0; i < _rows.Count; i++)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(_rows[i].Rect, at, null)) continue;
                if (i != _selected) ShowEntry(i);
                return;
            }
        }
    }
}
