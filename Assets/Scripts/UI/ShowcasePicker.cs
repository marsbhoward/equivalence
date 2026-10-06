using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Convergence.Core;
using Convergence.Hub;

namespace Convergence.UI
{
    /// <summary>
    /// Choose a piece to carry out and stand somewhere in the room, to hang on a wall frame, or
    /// to swap into a frame that already holds something else.
    ///
    /// A grid of pictures rather than a list of names, because the thing being chosen IS a
    /// picture - a player looking for "the blue one" should not have to read.
    ///
    /// Pieces already displayed elsewhere are shown dimmed and marked, but remain pickable - a
    /// frame is its own spot in the room now, independent of what it shows (see
    /// RoomLayout.TrophyPlacement.Id), so the same piece can legitimately hang in two places at
    /// once. Hiding or refusing them would make a small collection feel like it ran out.
    /// </summary>
    public class ShowcasePicker : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        GameObject _root;
        readonly List<(RectTransform Rect, ShowcaseItem Item, bool Placed)> _cells = new();

        /// <summary>Clickable rects handed to Controls each frame so the D-pad can walk them.
        /// Rebuilt rather than cached: these screens rebuild their contents on open.</summary>
        readonly List<RectTransform> _focusRects = new();
        Action<ShowcaseItem> _onPick;
        Func<string, bool> _isPlaced;

        public void Open(Transform canvas, IShowcaseSource source, Func<string, bool> isPlaced,
                         Action<ShowcaseItem> onPick)
        {
            if (IsOpen) return;
            IsOpen = true;
            _onPick = onPick;
            _isPlaced = isPlaced;
            GamePause.Hold(this);

            _root = new GameObject("ShowcasePicker", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.032f, 0.045f, 0.97f));

            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -108), new Vector2(0, -48)),
                "THE COLLECTION", 40, new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleCenter);

            int count = source?.Count ?? 0;
            UiKit.Label(UiKit.Rect(full, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -142), new Vector2(0, -108)),
                count > 0
                    ? "pick a piece to carry out and place    -    BACK or [ESC] to close"
                    : "nothing to show - no collection is connected",
                18, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter);

            const int cols = 5;
            const float cell = 200f, gap = 22f;
            float gridW = cols * cell + (cols - 1) * gap;

            for (int i = 0; i < count; i++)
            {
                if (!source.TryGet(i, out var item)) continue;
                int col = i % cols, row = i / cols;
                float x = -gridW * 0.5f + col * (cell + gap);
                float y = 170f - row * (cell + gap + 34f);

                bool placed = isPlaced != null && isPlaced(item.Key);

                var card = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, y - cell), new Vector2(x + cell, y),
                    placed ? new Color(0.07f, 0.075f, 0.10f) : new Color(0.11f, 0.12f, 0.16f));

                var art = UiKit.Rect(card, "art", Vector2.zero, Vector2.one,
                                     new Vector2(10, 10), new Vector2(-10, -10));
                var img = art.gameObject.AddComponent<Image>();
                img.sprite = item.Art;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = placed ? new Color(1f, 1f, 1f, 0.32f) : Color.white;

                UiKit.Label(UiKit.Rect(card, "n", new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(4, -30), new Vector2(-4, -2)),
                    placed ? item.Title + "  (placed)" : item.Title, 15,
                    placed ? new Color(0.45f, 0.48f, 0.55f) : new Color(0.8f, 0.83f, 0.9f),
                    TextAnchor.MiddleCenter);

                _cells.Add((card, item, placed));
            }

            UiKit.Label(UiKit.Rect(full, "f", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 34), new Vector2(0, 74)),
                "a placed piece can be picked up again by walking to it and pressing [E]",
                17, new Color(0.42f, 0.45f, 0.53f), TextAnchor.MiddleCenter);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _cells.Clear();
        }

        void Update()
        {
            if (!IsOpen) return;

            _focusRects.Clear();
            foreach (var (rect, _, _) in _cells) _focusRects.Add(rect);
            Core.Controls.SetFocusCandidates(_focusRects);

            if (Core.Controls.CancelTapped || Core.Controls.InteractTapped)
            {
                Close();
                return;
            }

            if (!Core.Controls.Tapped(out var at)) return;

            foreach (var (rect, item, _) in _cells)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) continue;
                var chosen = item;
                Close();
                _onPick?.Invoke(chosen);
                return;
            }
        }
    }
}
