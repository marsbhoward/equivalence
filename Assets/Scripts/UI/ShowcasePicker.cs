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

        /// <summary>What the page was drawn from - redrawn when a load changes it, or a picture fails.</summary>
        (int Groups, int Items, bool More, bool Loading, int Failed) _drawn;

        /// <summary>Names learned from a policy's loaded pieces, for a policy the wallet read did not
        /// name - a policy id means nothing to a player (the user, 2026-10-31).</summary>
        readonly Dictionary<string, string> _learned = new();

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
        static bool Booth(ShowcaseGroup g) => g.Id != null && g.Id.StartsWith(BoothShowcase.GroupPrefix);

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
            _learned.Clear();
            // The booth's shots first (Hub.BoothShowcase), then the wallet's collections by name.
            _groups.Sort((a, b) => Booth(a) != Booth(b) ? (Booth(a) ? -1 : 1)
                                 : string.Compare(NameOf(a), NameOf(b), StringComparison.OrdinalIgnoreCase));
            // Opens on the policy of what the frame shows - the head of its key. An empty frame opens
            // on the list alone, nothing chosen (the user's call, 2026-10-31): no collection's pictures
            // are fetched until the player picks one.
            string policy = _current.StartsWith(Chain.BoothShots.KeyPrefix)
                ? BoothShowcase.GroupOf(_current)
                : _current.Length >= 56 ? _current.Substring(0, 56) : null;
            _group = policy == null ? -1 : _groups.FindIndex(g => g.Id == policy);
            _groupPage = Mathf.Max(0, _group) / GroupsPerPage;
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
                    ? "your booth shots and wallet's pieces    -    BACK or [BACKSPACE] to close"
                    : "nothing to show - keep a picture at the portrait booth, or connect a wallet holding art",
                18, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter);

            Rebuild();
        }

        static bool Readable(string name) => !string.IsNullOrEmpty(name) && !(name.Length == 56 && IsHex(name, 56));

        /// <summary>A group's name: its collection, else one learned from its pieces, else its policy id
        /// shortened.</summary>
        string NameOf(in ShowcaseGroup g)
        {
            if (Readable(g.Name)) return g.Name;
            if (g.Id != null && _learned.TryGetValue(g.Id, out var learned)) return learned;
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

            // BACK, on screen: Escape leaves the browser's fullscreen before the page ever sees it.
            var back = UiKit.Panel(_content, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(40, -112), new Vector2(220, -48), new Color(0.11f, 0.12f, 0.16f));
            UiKit.Label(back, "<  BACK", 20, new Color(0.8f, 0.83f, 0.9f), TextAnchor.MiddleCenter);
            _hits.Add((back, Close));

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

            _drawn = (_groups.Count, 0, false, false, 0);
            if (_groups.Count == 0) return;
            if (_group < 0 || _group >= _groups.Count)
            {
                UiKit.Label(UiKit.Rect(_content, "pick", mid, mid, new Vector2(-420f, -40f), new Vector2(474f, 40f)),
                    "pick a collection", 22, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter);
                return;
            }

            // ---- the chosen policy's pieces, paged - loaded a page at a time as they are reached ----
            var group = _groups[_group];
            _source.Loaded(group.Id, _items);
            bool more = _source.HasMore(group.Id), loading = _source.IsLoading(group.Id);
            _drawn = (_groups.Count, _items.Count, more, loading, FailedCount());
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
                // A picture that did not arrive: say so, and RETRY it - its hit before the card's,
                // so a tap on it fetches again rather than choosing a picture nobody can see.
                if (_source.Failed(key))
                {
                    img.color = Color.clear;   // the placeholder would be a blank white tile under the words
                    UiKit.Label(UiKit.Rect(card, "f", new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(6, 0), new Vector2(-6, -12)),
                        "couldn't load", 16, new Color(0.75f, 0.55f, 0.5f), TextAnchor.MiddleCenter);
                    var retry = UiKit.Panel(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(-70, 18), new Vector2(70, 72), new Color(0.22f, 0.2f, 0.14f));
                    UiKit.Label(retry, "RETRY", 19, new Color(1f, 0.86f, 0.4f), TextAnchor.MiddleCenter);
                    _hits.Add((retry, () => { _source.Retry(key); Rebuild(); }));
                }
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

        /// <summary>The visible pictures that failed - a change redraws the page with its RETRYs.</summary>
        int FailedCount()
        {
            int n = 0;
            if (_source == null) return 0;
            foreach (var (_, key) in _cells) if (_source.Failed(key)) n++;
            return n;
        }

        /// <summary>Names the source has now for policies this list drew unnamed. Whether any arrived.</summary>
        bool NamesArrived()
        {
            bool any = false;
            foreach (var s in _source.Groups)
            {
                if (!Readable(s.Name) || _learned.ContainsKey(s.Id)) continue;
                int i = _groups.FindIndex(g => g.Id == s.Id);
                if (i >= 0 && !Readable(_groups[i].Name)) { _learned[s.Id] = s.Name; any = true; }
            }
            return any;
        }

        /// <summary>A name for the open policy from its loaded pieces, when the wallet read gave none.
        /// Returns whether one was learned, so the list redraws with it.</summary>
        bool LearnName(string id)
        {
            if (string.IsNullOrEmpty(id) || _learned.ContainsKey(id)) return false;
            var g = _groups.Find(x => x.Id == id);
            if (Readable(g.Name)) return false;
            foreach (var it in _items)
                if (Readable(it.Collection)) { _learned[id] = it.Collection; return true; }
            return false;
        }

        void Update()
        {
            if (!IsOpen) return;

            // Names the wallet read gained since this opened (WalletInventory asks again for missing ones).
            if (_source != null && NamesArrived()) Rebuild();

            // A page arrived, or a load started or ended: redraw. A picture arriving: swap it in.
            if (_source != null && _group >= 0 && _group < _groups.Count)
            {
                var id = _groups[_group].Id;
                _source.Loaded(id, _items);
                bool named = LearnName(id);
                var now = (_groups.Count, _items.Count, _source.HasMore(id), _source.IsLoading(id), FailedCount());
                if (now != _drawn || named) Rebuild();
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
