using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;
using Convergence.Hub;

namespace Convergence.UI
{
    /// <summary>
    /// Choose what a placed frame shows. The crate holds FRAMES, not the wallet (the user's call,
    /// 2026-10-31): a frame comes out empty, is set down, and this is where its art is picked.
    ///
    /// GROUPED BY POLICY - a collection on Cardano is a policy id - down the left, that policy's
    /// pieces in a paged grid on the right. A wallet holding a few hundred pieces across a dozen
    /// collections reads as a dozen choices first, rather than one long wall of pictures.
    ///
    /// A grid of pictures rather than a list of names, because the thing being chosen IS a
    /// picture - a player looking for "the blue one" should not have to read.
    ///
    /// Pieces already displayed elsewhere are shown dimmed and marked, but remain pickable - a
    /// frame is its own spot in the room, independent of what it shows (see
    /// RoomLayout.TrophyPlacement.Id), so the same piece can legitimately hang in two places at
    /// once. Hiding or refusing them would make a small collection feel like it ran out.
    /// </summary>
    public class ShowcasePicker : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        const int GroupsPerPage = 7, Cols = 4, Rows = 2;

        GameObject _root;
        RectTransform _full, _content;
        IShowcaseSource _source;
        readonly List<ShowcaseGroup> _groups = new();
        readonly List<ShowcaseItem> _items = new();
        int _group, _groupPage, _page;
        string _current;

        /// <summary>What the page was drawn from - redrawn when a load changes it.</summary>
        (int Groups, int Items, bool More, bool Loading) _drawn;

        /// <summary>The visible pictures, so a picture arriving swaps in without a redraw (which
        /// would lose the pad's focus).</summary>
        readonly List<(Image Img, string Key)> _cells = new();

        /// <summary>Everything clickable on the current page, in focus order. Rebuilt with the
        /// page - the pad walks these (Controls.SetFocusCandidates).</summary>
        readonly List<(RectTransform Rect, Action Act)> _hits = new();
        readonly List<RectTransform> _focusRects = new();

        Func<string, bool> _isPlaced;
        Action<string> _onPick;

        /// <param name="current">The key the frame shows now (empty for an empty frame) - the
        /// picker opens on its policy and marks it.</param>
        /// <param name="onPick">The chosen key; EMPTY when the player empties the frame.</param>
        public void Open(Transform canvas, IShowcaseSource source, Func<string, bool> isPlaced,
                         string current, Action<string> onPick)
        {
            if (IsOpen) return;
            IsOpen = true;
            _onPick = onPick;
            _isPlaced = isPlaced;
            _current = current ?? "";
            GamePause.Hold(this);

            _source = source;
            _groups.Clear();
            if (source != null) _groups.AddRange(source.Groups);
            _groups.Sort((a, b) => string.Compare(NameOf(a), NameOf(b), StringComparison.OrdinalIgnoreCase));
            // Opens on the policy of what the frame shows - the head of its key.
            string policy = _current.Length >= 56 ? _current.Substring(0, 56) : null;
            _group = Mathf.Max(0, _groups.FindIndex(g => g.Id == policy));
            _groupPage = _group / GroupsPerPage;
            _page = 0;

            _root = new GameObject("ShowcasePicker", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            _full = (RectTransform)_root.transform;
            _full.anchorMin = Vector2.zero; _full.anchorMax = Vector2.one;
            _full.offsetMin = Vector2.zero; _full.offsetMax = Vector2.zero;

            UiKit.Panel(_full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.032f, 0.045f, 0.97f));

            UiKit.Label(UiKit.Rect(_full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -108), new Vector2(0, -48)),
                "CHOOSE ITS ART", 40, new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleCenter);

            UiKit.Label(UiKit.Rect(_full, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -142), new Vector2(0, -108)),
                _groups.Count > 0
                    ? "your wallet's pieces, by collection    -    BACK or [ESC] to close"
                    : "nothing to show - connect a wallet holding art",
                18, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter);

            Rebuild();
        }

        /// <summary>A group's name: its collection, else its policy id shortened.</summary>
        static string NameOf(in ShowcaseGroup g)
        {
            if (!string.IsNullOrEmpty(g.Name) && !(g.Name.Length == 56 && IsHex(g.Name, 56))) return g.Name;
            var id = g.Id ?? "";
            return id.Length == 56 ? $"policy {id.Substring(0, 6)}...{id.Substring(52)}" : "untitled";
        }

        static bool IsHex(string s, int n)
        {
            for (int i = 0; i < n; i++)
            {
                char ch = s[i];
                if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F'))) return false;
            }
            return true;
        }

        /// <summary>Redraw the policy list and the grid for the current selection and pages.</summary>
        void Rebuild()
        {
            if (_content != null) Destroy(_content.gameObject);
            _hits.Clear();
            _cells.Clear();
            _content = UiKit.Rect(_full, "content", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var mid = new Vector2(0.5f, 0.5f);

            // ---- the policies, down the left ----
            const float lx0 = -880f, lx1 = -500f, rowH = 64f, rowGap = 10f, top = 340f;
            int gFirst = _groupPage * GroupsPerPage;
            for (int i = gFirst; i < Mathf.Min(_groups.Count, gFirst + GroupsPerPage); i++)
            {
                var g = _groups[i];
                float y = top - (i - gFirst) * (rowH + rowGap);
                bool on = i == _group;
                var row = UiKit.Panel(_content, mid, mid, new Vector2(lx0, y - rowH), new Vector2(lx1, y),
                    on ? new Color(0.22f, 0.2f, 0.14f) : new Color(0.11f, 0.12f, 0.16f));
                UiKit.Label(UiKit.Rect(row, "n", Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-70, 0)),
                    NameOf(g), 19, on ? new Color(1f, 0.86f, 0.4f) : new Color(0.8f, 0.83f, 0.9f), TextAnchor.MiddleLeft);
                UiKit.Label(UiKit.Rect(row, "c", Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-18, 0)),
                    g.Count.ToString(), 18, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleRight);
                int pick = i;
                _hits.Add((row, () => { _group = pick; _page = 0; Rebuild(); }));
            }

            int groupPages = (_groups.Count + GroupsPerPage - 1) / GroupsPerPage;
            float listBottom = top - GroupsPerPage * (rowH + rowGap);
            if (groupPages > 1)
                Pager(mid, lx0, lx1, listBottom - 6f, _groupPage, groupPages, p => { _groupPage = p; Rebuild(); });

            // Leave the frame standing empty - it is the player's furniture now, art or none.
            if (!string.IsNullOrEmpty(_current))
            {
                var empty = UiKit.Panel(_content, mid, mid, new Vector2(lx0, -440f), new Vector2(lx1, -380f),
                    new Color(0.16f, 0.1f, 0.1f));
                UiKit.Label(empty, "EMPTY THE FRAME", 20, new Color(0.9f, 0.7f, 0.65f), TextAnchor.MiddleCenter);
                _hits.Add((empty, () => Pick("")));
            }

            _drawn = (_groups.Count, 0, false, false);
            if (_groups.Count == 0) return;

            // ---- the chosen policy's pieces, paged - loaded a page at a time as they are reached ----
            var group = _groups[_group];
            _source.Loaded(group.Id, _items);
            bool more = _source.HasMore(group.Id), loading = _source.IsLoading(group.Id);
            _drawn = (_groups.Count, _items.Count, more, loading);
            const float cell = 210f, gap = 24f, label = 34f, gx0 = -420f;
            float gx1 = gx0 + Cols * cell + (Cols - 1) * gap;

            UiKit.Label(UiKit.Rect(_content, "h", mid, mid, new Vector2(gx0, top + 8f), new Vector2(gx1, top + 48f)),
                $"{NameOf(group).ToUpper()}    {group.Count} piece{(group.Count == 1 ? "" : "s")}",
                22, new Color(0.86f, 0.78f, 0.55f), TextAnchor.MiddleLeft);

            int per = Cols * Rows;
            int n = _items.Count;
            // A further page while more are to come and every loaded page is full.
            int pages = Mathf.Max(1, (n + per - 1) / per + (more && n % per == 0 && n > 0 ? 1 : 0));
            _page = Mathf.Clamp(_page, 0, pages - 1);
            int first = _page * per;
            // This page not full and more to come: ask. The redraw follows the answer (Update).
            if (first + per > n && more && !loading) { _source.LoadMore(group.Id); loading = _source.IsLoading(group.Id); _drawn.Loading = loading; }

            if (first >= n)
                UiKit.Label(UiKit.Rect(_content, "e", mid, mid, new Vector2(gx0, top - cell), new Vector2(gx1, top)),
                    loading || more ? "loading..." : "no pictures in this collection", 20,
                    new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter);

            for (int i = first; i < Mathf.Min(n, first + per); i++)
            {
                var item = _items[i];
                int col = (i - first) % Cols, row = (i - first) / Cols;
                float x = gx0 + col * (cell + gap);
                float y = top - row * (cell + gap + label);

                bool current = item.Key == _current;
                bool placed = !current && _isPlaced != null && _isPlaced(item.Key);

                var card = UiKit.Panel(_content, mid, mid, new Vector2(x, y - cell), new Vector2(x + cell, y),
                    current ? new Color(0.22f, 0.2f, 0.14f)
                    : placed ? new Color(0.07f, 0.075f, 0.10f) : new Color(0.11f, 0.12f, 0.16f));

                var art = UiKit.Rect(card, "art", Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));
                var img = art.gameObject.AddComponent<Image>();
                img.sprite = _source.TryGetByKey(item.Key, out var shown) ? shown.Art : item.Art;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = placed ? new Color(1f, 1f, 1f, 0.32f) : Color.white;

                string title = string.IsNullOrEmpty(item.Title) ? "untitled" : item.Title;
                UiKit.Label(UiKit.Rect(card, "n", new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(4, -30), new Vector2(-4, -2)),
                    current ? title + "  (here)" : placed ? title + "  (placed)" : title, 15,
                    current ? new Color(1f, 0.86f, 0.4f)
                    : placed ? new Color(0.45f, 0.48f, 0.55f) : new Color(0.8f, 0.83f, 0.9f),
                    TextAnchor.MiddleCenter);

                var key = item.Key;
                _hits.Add((card, () => Pick(key)));
                _cells.Add((img, key));
            }

            if (pages > 1)
                Pager(mid, gx0, gx1, top - Rows * (cell + gap + label) - 6f, _page, pages, p => { _page = p; Rebuild(); });
        }

        /// <summary>PREV / page n of m / NEXT under a list, the arrows only where they lead somewhere.</summary>
        void Pager(Vector2 mid, float x0, float x1, float y, int page, int pages, Action<int> go)
        {
            const float h = 52f, w = 120f;
            UiKit.Label(UiKit.Rect(_content, "pg", mid, mid, new Vector2(x0, y - h), new Vector2(x1, y)),
                $"{page + 1} / {pages}", 18, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter);
            if (page > 0)
            {
                var prev = UiKit.Panel(_content, mid, mid, new Vector2(x0, y - h), new Vector2(x0 + w, y),
                    new Color(0.11f, 0.12f, 0.16f));
                UiKit.Label(prev, "<  PREV", 18, new Color(0.8f, 0.83f, 0.9f), TextAnchor.MiddleCenter);
                _hits.Add((prev, () => go(page - 1)));
            }
            if (page < pages - 1)
            {
                var next = UiKit.Panel(_content, mid, mid, new Vector2(x1 - w, y - h), new Vector2(x1, y),
                    new Color(0.11f, 0.12f, 0.16f));
                UiKit.Label(next, "NEXT  >", 18, new Color(0.8f, 0.83f, 0.9f), TextAnchor.MiddleCenter);
                _hits.Add((next, () => go(page + 1)));
            }
        }

        void Pick(string key)
        {
            var pick = _onPick;
            Close();
            pick?.Invoke(key);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _full = _content = null;
            _source = null;
            _hits.Clear();
            _cells.Clear();
            _groups.Clear();
            _items.Clear();
        }

        void Update()
        {
            if (!IsOpen) return;

            // A page arrived, or a load started or ended: redraw. A picture arriving: swap it in.
            if (_source != null && _groups.Count > 0)
            {
                var id = _groups[_group].Id;
                _source.Loaded(id, _items);
                var now = (_groups.Count, _items.Count, _source.HasMore(id), _source.IsLoading(id));
                if (now != _drawn) Rebuild();
                foreach (var (img, key) in _cells)
                    if (img != null && _source.TryGetByKey(key, out var it) && img.sprite != it.Art) img.sprite = it.Art;
            }

            _focusRects.Clear();
            foreach (var (rect, _) in _hits) _focusRects.Add(rect);
            Core.Controls.SetFocusCandidates(_focusRects);

            // Taps first: a page turn rebuilds the list, so the loop stops at the first hit.
            if (Core.Controls.Tapped(out var at))
            {
                foreach (var (rect, act) in _hits)
                {
                    if (rect == null || !RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) continue;
                    act();
                    return;
                }
            }

            if (Core.Controls.CancelTapped || Core.Controls.InteractTapped) Close();
        }
    }
}
