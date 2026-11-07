using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;
using Convergence.Rifts;

namespace Convergence.UI
{
    /// <summary>
    /// The choice a Rift asks: leave with everything, or push on with what you take out.
    ///
    /// TWO GRIDS (the user's design, 2026-11-06). The top grid is everything carried, as an
    /// inventory; the bottom grid is the slots this tear gives - its free capacity, then one per
    /// Rift Box held - and a tap moves a piece from one to the other. What is in the bottom grid is
    /// kept if you die. The Rift Boxes left to spend sit by the title. Replaced a one-list-with-locks
    /// layout the same day, which spent most of the screen on empty row space; and before that two
    /// columns, "coming out / still at risk", which read as if only one side left with you when
    /// leaving keeps everything.
    ///
    /// STATS ON HOVER (the user's call): the gear cell under the pointer - or the pad's focus - shows
    /// its roll beside it: main stat, every sub-stat, the cuirass's defence, the relic's art. Which
    /// piece to take out turns on what it rolled. On a phone the pointer is the last touch, so the
    /// piece just moved shows its roll.
    ///
    /// ASKED FOR, AND DISMISSIBLE. It opens on [ E ] at the tear, never by walking into it, and
    /// KEEP GOING (or Escape) closes it with the tear still open. The Rift stands beside the
    /// floor's exit door, so going on is the door, not an answer here.
    ///
    /// AND TAKEN BACK. What this tear took out goes back on a tap, refunding what it cost (the
    /// user's call, 2026-11-05). What earlier Rifts secured is out of the way: a SECURED button
    /// under the Rift Box count, when there is any, opens it over the screen (the user's call,
    /// 2026-11-06) - the bottom grid is only this tear's slots.
    /// </summary>
    public class RiftScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        GameObject _root;
        RunLoot _loot;
        Rift _rift;
        int _openedFrame = -1;
        int _capacity, _floor, _scroll;
        Action _onExtract, _onPushOn;

        /// <summary>Tap targets: a cell (move).</summary>
        readonly List<(RectTransform Rect, Action Act)> _cells = new();
        readonly List<RectTransform> _focusRects = new();
        RectTransform _full, _extractBtn, _pushBtn, _upBtn, _downBtn;
        Text _subtitle, _boxCount, _topTitle, _bottomTitle, _extractSub, _pushSub;
        Image _boxIcon;

        // ---- what earlier Rifts secured, behind a button ----
        RectTransform _securedBtn, _securedClose, _securedPanel;
        Text _securedLabel;
        bool _showSecured;

        /// <summary>The gear in the order it was first listed - taking a piece out moves it
        /// between RunLoot's lists, and putting it back must return it to its own cell.</summary>
        readonly List<MintedGearRecord> _order = new();

        // ---- the hover card ----
        readonly List<(RectTransform Rect, MintedGearRecord Gear)> _gearCells = new();
        GameObject _tip;
        MintedGearRecord _tipGear;

        const float Gap = 14f, Head = 44f, Top = -132f;

        /// <summary>Cells to a row (the user's call, 2026-11-06): the bag is 5, 7 or 10
        /// (Tuning.Boss.CarrySlots), so its rows read as a full row and what follows.</summary>
        const int PerRow = 5;

        /// <summary>A cell's side, worked out each Refresh to fill the space the rows have.</summary>
        float Cell => _cell;
        float _cell = 168f;
        Vector2 _laidOutFor;
        const float MinCell = 96f, MaxCell = 210f;

        public void Open(Transform canvas, RunLoot loot, int capacity, int floor, Rift rift,
                         Action onExtract, Action onPushOn)
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _rift = rift;
            _loot = loot;
            _capacity = capacity;
            _floor = floor;
            _scroll = 0;
            _showSecured = false;
            _onExtract = onExtract;
            _onPushOn = onPushOn;
            _order.Clear();
            GamePause.Hold(this);

            _root = new GameObject("RiftScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            _full = (RectTransform)_root.transform;
            _full.anchorMin = Vector2.zero; _full.anchorMax = Vector2.one;
            _full.offsetMin = Vector2.zero; _full.offsetMax = Vector2.zero;

            Build(_full);
            Refresh();
        }

        static readonly Color RiskInk = new(0.95f, 0.62f, 0.55f);
        static readonly Color SafeInk = new(0.62f, 0.92f, 0.68f);
        static readonly Color Dim = new(0.55f, 0.52f, 0.65f);
        static readonly Color FreeInk = new(0.94f, 0.88f, 0.82f);
        static readonly Color PaidInk = new(0.80f, 0.72f, 1f);
        static readonly Color DeadInk = new(0.50f, 0.47f, 0.52f);

        void Build(RectTransform full)
        {
            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.04f, 0.035f, 0.06f, 0.97f));
            // A few rift flakes drifting up behind everything (the user's call, 2026-11-06).
            RiftMotes.Add(full, 14);

            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -88), new Vector2(0, -32)),
                "THE RIFT", 40, new Color(0.80f, 0.72f, 1f), TextAnchor.MiddleCenter);
            _subtitle = UiKit.Label(UiKit.Rect(full, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -118), new Vector2(0, -88)),
                "", 18, Dim, TextAnchor.MiddleCenter);

            // ---- the Rift Boxes left to spend, by the title
            _boxIcon = BoxIcon.Add(full, Art.BoxArt.Kind.Rift, false, new Vector2(1f, 1f), new Vector2(-190f, -96f));
            _boxCount = UiKit.Label(UiKit.Rect(full, "bc", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-150, -96), new Vector2(-40, -40)),
                "", 30, PaidInk, TextAnchor.MiddleLeft);
            _securedBtn = UiKit.Panel(full, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-250, -176), new Vector2(-40, -112), new Color(0.10f, 0.18f, 0.13f, 1f));
            _securedLabel = UiKit.Label(_securedBtn, "", 18, SafeInk, TextAnchor.MiddleCenter);

            // ---- the two grids: placed by Refresh, which knows how many cells each holds
            _topTitle = UiKit.Label(UiKit.Rect(full, "th", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                Vector2.zero, Vector2.zero), "", 21, RiskInk);
            _bottomTitle = UiKit.Label(UiKit.Rect(full, "bh", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                Vector2.zero, Vector2.zero), "", 21, SafeInk);
            _upBtn = UiKit.Panel(full, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero,
                new Color(0.14f, 0.13f, 0.19f, 1f));
            UiKit.Label(_upBtn, "UP", 18, Dim, TextAnchor.MiddleCenter);
            _downBtn = UiKit.Panel(full, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero,
                new Color(0.14f, 0.13f, 0.19f, 1f));
            UiKit.Label(_downBtn, "DOWN", 18, Dim, TextAnchor.MiddleCenter);

            // ---- the two answers ----
            _extractBtn = UiKit.Panel(full, new Vector2(0, 0), new Vector2(0.5f, 0),
                new Vector2(70, 32), new Vector2(-14, 132), new Color(0.14f, 0.26f, 0.17f, 1f));
            UiKit.Label(UiKit.Rect(_extractBtn, "l", Vector2.zero, Vector2.one, new Vector2(0, 40), new Vector2(0, -4)),
                "LEAVE THE RUN", 26, new Color(0.72f, 0.96f, 0.76f), TextAnchor.MiddleCenter);
            _extractSub = UiKit.Label(UiKit.Rect(_extractBtn, "s", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 8), new Vector2(0, 42)),
                "", 16, new Color(0.52f, 0.70f, 0.56f), TextAnchor.MiddleCenter);

            _pushBtn = UiKit.Panel(full, new Vector2(0.5f, 0), new Vector2(1, 0),
                new Vector2(14, 32), new Vector2(-70, 132), new Color(0.26f, 0.16f, 0.14f, 1f));
            UiKit.Label(UiKit.Rect(_pushBtn, "l", Vector2.zero, Vector2.one, new Vector2(0, 40), new Vector2(0, -4)),
                "KEEP GOING", 26, new Color(0.98f, 0.76f, 0.68f), TextAnchor.MiddleCenter);
            _pushSub = UiKit.Label(UiKit.Rect(_pushBtn, "s", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 8), new Vector2(0, 42)),
                "", 16, new Color(0.72f, 0.52f, 0.48f), TextAnchor.MiddleCenter);
        }

        RectTransform _cellsRoot;

        /// <summary>Redraws both grids and the counts. Rebuilt wholesale: the screen is open for
        /// one decision, and there is nothing here worth an incremental update.</summary>
        void Refresh()
        {
            _cells.Clear();
            _gearCells.Clear();
            HideTip();
            if (_cellsRoot) Destroy(_cellsRoot.gameObject);
            _cellsRoot = UiKit.Rect(_full, "cells", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            int free = Mathf.Max(0, _capacity - SecuredThisRift);
            int boxes = _loot.TotalBoxes;
            int safe = _loot.SecuredCount + SecuredBoxCount(), risk = _loot.CarriedCount + FoundBoxCount();

            _subtitle.text = $"floor {_floor}    -    tap to move between the grids    -    leaving keeps everything";
            _boxCount.text = $"x{boxes}";
            _boxIcon.color = boxes > 0 ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            string bag = _loot.CarryLimit < 1000 ? $"   -   gear {_loot.CarriedCount} of {_loot.CarryLimit}" : "";
            _topTitle.text = $"CARRIED   {risk}{bag}      <size=16><color=#B5736A>lost if you die before the next Rift</color></size>";
            _bottomTitle.text = $"TAKE OUT   {free} free"
                              + (boxes > 0 ? $" + {boxes} with Rift Box{(boxes == 1 ? "" : "es")}" : "")
                              + "      <size=16><color=#6FA27B>kept even if you die</color></size>";
            _extractSub.text = $"end here and keep all {safe + risk}";
            _pushSub.text = $"{safe} kept  -  {risk} at risk  -  take the door to go on";

            var (top, bottom) = Cells(free, boxes);

            // The carried boxes stand in a column at the top left, mirroring the Rift Box count at
            // the top right (the user's call, 2026-11-06), so the bag's grid is exactly its 5, 7 or
            // 10 places - boxes don't take bag space.
            var boxCol = top.FindAll(c => c.Box.HasValue);
            top.RemoveAll(c => c.Box.HasValue);
            DrawBoxColumn(boxCol);
            var earlier = bottom.FindAll(c => c.Lock == Lock.Earlier);
            bottom.RemoveAll(c => c.Lock == Lock.Earlier);
            int earlierCount = 0;
            foreach (var c in earlier) earlierCount += c.Box.HasValue ? c.Count : 1;
            _securedBtn.gameObject.SetActive(earlierCount > 0);
            _securedLabel.text = $"SECURED  {earlierCount}";
            if (earlierCount == 0) _showSecured = false;

            // ---- layout: rows of five, filled from the top row first (the user's call, 2026-11-06) -
            // boxes, then the bag's pieces and its empty places, with no gap between them; the cell
            // size is whatever fills the space between the title and the two answers, inside what
            // the width allows.
            const int cols = PerRow;
            _laidOutFor = _full.rect.size;
            var placed = new List<(Slot Slot, int Row, int Col)>();
            for (int i = 0; i < top.Count; i++) placed.Add((top[i], i / cols, i % cols));
            int rows = Mathf.Max(1, Mathf.CeilToInt(top.Count / (float)cols));
            int bottomRows = Mathf.Max(1, Mathf.CeilToInt(bottom.Count / (float)cols));

            float availH = _full.rect.height + Top - 2 * Head - 22f - (132f + 44f);
            float availW = _full.rect.width - 2 * 290f;   // the Rift Box count and SECURED stand to the right
            float byH = (availH - (rows + bottomRows - 2) * Gap) / (rows + bottomRows);
            float byW = (availW - (cols - 1) * Gap) / cols;
            _cell = Mathf.Clamp(Mathf.Min(byH, byW), MinCell, MaxCell);
            // At the smallest cell, rows that still don't fit page with UP/DOWN.
            float bottomH = bottomRows * (Cell + Gap) - Gap;
            int shownRows = Mathf.Clamp(Mathf.FloorToInt((availH - bottomH + Gap) / (Cell + Gap) + 0.01f), 1, rows);

            float blockW = cols * (Cell + Gap) - Gap;
            float left = -blockW * 0.5f;

            float y = Top;
            Place(_topTitle, left, y, Mathf.Max(blockW, 900f));
            y -= Head;
            _scroll = Mathf.Clamp(_scroll, 0, Mathf.Max(0, rows - shownRows));
            foreach (var (slot, row, col) in placed)
                if (row >= _scroll && row < _scroll + shownRows)
                    Draw(slot, new Vector2(left + col * (Cell + Gap), y - (row - _scroll) * (Cell + Gap)));
            if (top.Count == 0)
                UiKit.Label(UiKit.Rect(_cellsRoot, "empty", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(left, y - Cell), new Vector2(left + Mathf.Max(blockW, 400f), y)),
                    "nothing left at risk", 18, DeadInk, TextAnchor.MiddleLeft);
            float gridRight = left + blockW + Gap;
            PlaceButton(_upBtn, gridRight, y, rows > shownRows && _scroll > 0);
            PlaceButton(_downBtn, gridRight, y - shownRows * (Cell + Gap) + Cell + Gap, rows > shownRows && _scroll < rows - shownRows);
            y -= shownRows * (Cell + Gap) + 22f;

            Place(_bottomTitle, left, y, Mathf.Max(blockW, 900f));
            y -= Head;
            for (int i = 0; i < bottom.Count; i++)
                Draw(bottom[i], new Vector2(left + (i % cols) * (Cell + Gap), y - (i / cols) * (Cell + Gap)));

            if (_showSecured) DrawSecured(earlier, cols);
        }

        /// <summary>
        /// What earlier Rifts secured, over the screen: safe whatever happens, so nothing here
        /// moves - it is there to be looked at (hover for a piece's roll). Closed by its button, a
        /// tap off the panel, or Cancel.
        /// </summary>
        void DrawSecured(List<Slot> earlier, int maxCols)
        {
            var veil = UiKit.Panel(_cellsRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0f, 0f, 0f, 0.7f));
            veil.GetComponent<Image>().raycastTarget = false;

            int cols = Mathf.Clamp(earlier.Count, 1, Mathf.Min(maxCols, 6));
            int rows = Mathf.Max(1, Mathf.CeilToInt(earlier.Count / (float)cols));
            float gridW = cols * (Cell + Gap) - Gap, w = Mathf.Max(gridW, 3 * (Cell + Gap) - Gap) + 80f;
            float h = 70f + rows * (Cell + Gap) - Gap + 40f + 80f + 30f;
            float top = -150f;
            _securedPanel = UiKit.Panel(_cellsRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-w * 0.5f, top - h), new Vector2(w * 0.5f, top), new Color(0.07f, 0.10f, 0.08f, 1f));
            UiKit.Panel(_securedPanel, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -5), Vector2.zero, SafeInk);
            UiKit.Label(UiKit.Rect(_securedPanel, "h", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(40, -60), new Vector2(-40, -16)),
                "SECURED      <size=16><color=#6FA27B>from earlier Rifts - kept whatever happens</color></size>", 24, SafeInk);

            float left = -gridW * 0.5f, y = top - 70f;
            for (int i = 0; i < earlier.Count; i++)
            {
                earlier[i].Note = null;   // the panel's title says where they came from
                Draw(earlier[i], new Vector2(left + (i % cols) * (Cell + Gap), y - (i / cols) * (Cell + Gap)));
            }

            _securedClose = UiKit.Panel(_securedPanel, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-160, 30), new Vector2(160, 110), new Color(0.12f, 0.13f, 0.17f, 1f));
            UiKit.Label(_securedClose, "CLOSE", 22, new Color(0.82f, 0.85f, 0.92f), TextAnchor.MiddleCenter);
        }

        const float BoxCell = 118f;

        void DrawBoxColumn(List<Slot> boxCol)
        {
            UiKit.Label(UiKit.Rect(_cellsRoot, "bxh", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(50, -66), new Vector2(50 + BoxCell + 120, -36)),
                "BOXES - AT RISK", 15, new Color(0.72f, 0.45f, 0.42f), TextAnchor.MiddleLeft);
            if (boxCol.Count == 0)
                UiKit.Label(UiKit.Rect(_cellsRoot, "bxn", new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(50, -100), new Vector2(50 + BoxCell + 60, -70)), "none", 15, DeadInk, TextAnchor.MiddleLeft);
            for (int i = 0; i < boxCol.Count; i++)
                Draw(boxCol[i], new Vector2(50, -72 - i * (BoxCell + 10f)), BoxCell, corner: true);
        }

        static void Place(Text label, float left, float y, float width)
        {
            var rt = label.rectTransform;
            rt.offsetMin = new Vector2(left, y - Head + 6f);
            rt.offsetMax = new Vector2(left + width, y);
        }

        void PlaceButton(RectTransform b, float x, float y, bool on)
        {
            b.offsetMin = new Vector2(x, y - Cell);
            b.offsetMax = new Vector2(x + 96f, y);
            b.gameObject.SetActive(on);
        }

        // ------------------------------------------------------------------ cells

        enum Lock { Open, Mine, Earlier }

        /// <summary>One cell: a piece of gear, a stack of boxes of one kind, or an empty slot.</summary>
        class Slot
        {
            public MintedGearRecord Gear;  // inspectable
            public Art.BoxArt.Kind? Box;
            public int Count;
            public Lock Lock;
            public Action Toggle;          // null = cannot move
            public string Empty;           // an empty slot's label, or null
            public bool EmptyPaid;         // an empty slot a Rift Box pays for
            public string Note;            // a word under the cell's picture
            public Color NoteInk;
        }

        static readonly LootTier[] BoxTiers =
        {
            LootTier.BlackDiamond, LootTier.Diamond, LootTier.Gold, LootTier.Silver, LootTier.Bronze,
        };

        int FoundBoxCount()
        {
            int n = _loot.FoundBoxes;
            foreach (var t in BoxTiers) n += _loot.FoundTieredBoxes(t);
            return n;
        }

        int SecuredBoxCount()
        {
            int n = _loot.SecuredRiftBoxes;
            foreach (var t in BoxTiers) n += _loot.SecuredTieredBoxes(t);
            return n;
        }

        /// <summary>
        /// The two grids. Top: boxes rarest first - a Diamond box below a row of Bronze would be
        /// missed - then the gear in the order it was first listed. Bottom: what earlier Rifts
        /// secured (fixed), what this tear took out, then its empty slots - free ones, then one per
        /// Rift Box. A piece takes free capacity first, then a Rift Box - except a RIFT BOX itself,
        /// which only free capacity can take (paying one box to keep one box is no trade).
        /// </summary>
        (List<Slot> Top, List<Slot> Bottom) Cells(int free, int boxes)
        {
            var top = new List<Slot>();
            var bottom = new List<Slot>();
            bool can = free > 0 || boxes > 0;

            foreach (var t in BoxTiers)
            {
                var tier = t;
                var kind = BoxIcon.KindOf(t);
                int safe = _loot.SecuredTieredBoxes(t);
                if (safe > 0)
                {
                    // One cell per box, so each knows whether this tear can put it back.
                    int mine = Taken.FindAll(x => x.Tier == tier).Count;
                    if (safe - mine > 0) bottom.Add(Earlier(new Slot { Box = kind, Count = safe - mine }));
                    foreach (var take in Taken.FindAll(x => x.Tier == tier)) bottom.Add(Mine(new Slot { Box = kind, Count = 1 }, take));
                }
                if (_loot.FoundTieredBoxes(t) > 0)
                    top.Add(Open(new Slot { Box = kind, Count = _loot.FoundTieredBoxes(t) }, free, can,
                                 () => Take(() => _loot.SecureTieredBox(tier), new RiftTake { Tier = tier })));
            }
            if (_loot.SecuredRiftBoxes > 0)
            {
                int mine = Taken.FindAll(x => x.RiftBox).Count;
                if (_loot.SecuredRiftBoxes - mine > 0)
                    bottom.Add(Earlier(new Slot { Box = Art.BoxArt.Kind.Rift, Count = _loot.SecuredRiftBoxes - mine }));
                foreach (var take in Taken.FindAll(x => x.RiftBox))
                    bottom.Add(Mine(new Slot { Box = Art.BoxArt.Kind.Rift, Count = 1 }, take));
            }
            if (_loot.FoundBoxes > 0)
                top.Add(free > 0
                    ? Open(new Slot { Box = Art.BoxArt.Kind.Rift, Count = _loot.FoundBoxes }, free, true,
                           () => Take(() => _loot.SecureRiftBox(), new RiftTake { RiftBox = true }))
                    : new Slot { Box = Art.BoxArt.Kind.Rift, Count = _loot.FoundBoxes, Lock = Lock.Open,
                                 Note = "needs a free slot", NoteInk = DeadInk });

            foreach (var it in _loot.Secured) if (!_order.Contains(it)) _order.Add(it);
            foreach (var it in _loot.Carried) if (!_order.Contains(it)) _order.Add(it);
            _order.RemoveAll(x => !Contains(_loot.Secured, x) && !Contains(_loot.Carried, x));
            foreach (var it in _order)
            {
                var item = it;
                var cell = new Slot { Gear = it };
                if (!Contains(_loot.Secured, it))
                    top.Add(Open(cell, free, can, () => Take(() => _loot.Secure(item), new RiftTake { Gear = item })));
                else
                {
                    var take = Taken.Find(x => x.Gear == item);
                    bottom.Add(take != null ? Mine(cell, take) : Earlier(cell));
                    // Back into a full bag is no room at all (Tuning.Boss.CarrySlots).
                    if (take != null && _loot.BagFull) { cell.Toggle = null; cell.Note = "bag full"; cell.NoteInk = DeadInk; }
                }
            }

            // The bag's empty places, so its size reads at a glance.
            if (_loot.CarryLimit < 1000)
                for (int i = _loot.CarriedCount; i < _loot.CarryLimit; i++) top.Add(new Slot { Empty = "empty" });

            // Earlier Rifts' first, then this tear's in the order taken, then the empty slots.
            bottom.Sort((a, b) => (a.Lock == Lock.Earlier ? 0 : 1).CompareTo(b.Lock == Lock.Earlier ? 0 : 1));
            for (int i = 0; i < free; i++) bottom.Add(new Slot { Empty = "free slot" });
            for (int i = 0; i < boxes; i++) bottom.Add(new Slot { Empty = "Rift Box", EmptyPaid = true });
            return (top, bottom);
        }

        static bool Contains(IReadOnlyList<MintedGearRecord> list, MintedGearRecord it)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == it) return true;
            return false;
        }

        static Slot Earlier(Slot c)
        {
            c.Lock = Lock.Earlier;
            c.Note = "earlier Rift";
            c.NoteInk = new Color(0.45f, 0.62f, 0.50f);
            return c;
        }

        Slot Mine(Slot c, RiftTake take)
        {
            c.Lock = Lock.Mine;
            c.Note = take.PaidBox ? "Rift Box" : "free slot";
            c.NoteInk = take.PaidBox ? PaidInk : SafeInk;
            c.Toggle = () => Return(take);
            return c;
        }

        static Slot Open(Slot c, int free, bool can, Action take)
        {
            c.Lock = Lock.Open;
            if (!can) return c;
            c.Toggle = take;
            return c;
        }

        /// <param name="origin">The cell's top-left, from the screen's top centre.</param>
        /// <param name="size">The cell's side; the grids' fitted size unless given (the box column).</param>
        /// <param name="corner">Placed from the screen's top-left rather than its top centre.</param>
        void Draw(Slot c, Vector2 origin, float size = -1f, bool corner = false)
        {
            // On the shared root, so a Refresh clears both grids at once.
            var a = corner ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f);
            float side = size > 0f ? size : Cell;
            Color frame = c.Empty != null ? (c.EmptyPaid ? new Color(0.36f, 0.30f, 0.55f) : new Color(0.45f, 0.40f, 0.36f))
                        : c.Gear != null ? CharacterScreen.TierColor(c.Gear.Tier)
                        : c.Lock == Lock.Open ? new Color(0.40f, 0.36f, 0.42f) : new Color(0.32f, 0.50f, 0.38f);
            var cell = UiKit.Panel(_cellsRoot, a, a, origin + new Vector2(0, -side), origin + new Vector2(side, 0), frame);
            var inner = UiKit.Panel(cell, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3),
                c.Empty != null ? new Color(0.06f, 0.055f, 0.08f, 1f)
                : c.Lock == Lock.Open ? new Color(0.11f, 0.10f, 0.13f, 1f) : new Color(0.09f, 0.15f, 0.11f, 1f));

            if (c.Empty != null)
            {
                if (c.EmptyPaid)
                {
                    var ghost = BoxIcon.Add(inner, Art.BoxArt.Kind.Rift, false, new Vector2(0.5f, 0.5f), new Vector2(0f, -14f));
                    ghost.color = new Color(1f, 1f, 1f, 0.25f);
                }
                UiKit.Label(UiKit.Rect(inner, "e", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 6), new Vector2(0, 30)),
                    c.Empty, 15, c.EmptyPaid ? PaidInk : FreeInk, TextAnchor.MiddleCenter);
                return;
            }

            if (c.Box.HasValue)
            {
                BoxIcon.Add(inner, c.Box.Value, false, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f));
                if (c.Count > 1)
                    UiKit.Label(UiKit.Rect(inner, "n", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -34), new Vector2(-8, -4)),
                        $"x{c.Count}", 20, FreeInk, TextAnchor.MiddleRight);
            }
            else if (c.Gear != null)
            {
                GearCells.Picture(inner, c.Gear);
            }

            if (c.Note != null)
                UiKit.Label(UiKit.Rect(inner, "note", new Vector2(0, 1), new Vector2(1, 1), new Vector2(6, -24), new Vector2(-6, -2)),
                    c.Note, 13, c.NoteInk, TextAnchor.MiddleLeft);

            if (c.Gear != null) _gearCells.Add((cell, c.Gear));
            if (c.Toggle != null) _cells.Add((cell, c.Toggle));
        }

        // ------------------------------------------------------------------ the hover card

        /// <summary>The gear cell under the pointer, or the pad's focus; null over anything else.</summary>
        MintedGearRecord Hovered(out RectTransform over)
        {
            over = null;
            var focused = Core.Controls.GamepadMode ? Core.Controls.Focused : null;
            foreach (var (rect, gear) in _gearCells)
            {
                if (!rect) continue;
                // Under the SECURED overlay only its own cells answer.
                if (_showSecured && (!_securedPanel || !Inside(_securedPanel, rect))) continue;
                bool hit = focused != null
                    ? rect == focused
                    : RectTransformUtility.RectangleContainsScreenPoint(rect, Core.Controls.PointerPosition, null);
                if (hit) { over = rect; return gear; }
            }
            return null;
        }

        /// <summary>
        /// A piece's roll beside its cell: name in its tier's colour, tier and kind, stars, the main
        /// stat first, every sub-stat by name, the cuirass's defence and the relic's weapon art.
        /// </summary>
        void ShowTip(MintedGearRecord it, RectTransform over)
        {
            HideTip();
            _tipGear = it;
            _tip = GearCells.Tip(_full, it, over);
        }

        static readonly Vector3[] _corners = new Vector3[4];

        /// <summary>Whether <paramref name="cell"/>'s middle lies within <paramref name="panel"/>.</summary>
        static bool Inside(RectTransform panel, RectTransform cell)
        {
            cell.GetWorldCorners(_corners);
            var mid = (_corners[0] + _corners[2]) * 0.5f;
            return RectTransformUtility.RectangleContainsScreenPoint(panel, mid, null);
        }

        void HideTip()
        {
            if (_tip) Destroy(_tip);
            _tip = null;
            _tipGear = null;
        }

        // ------------------------------------------------------------------ the rules

        /// <summary>
        /// Secures one thing: free capacity first, then a Rift Box. Never the other way round: a
        /// player who had both and spent a consumable while a free slot sat unused would rightly
        /// call that a bug.
        ///
        /// Move FIRST, then pay. Spending first needed a refund path if the move failed, and the
        /// refund could not know which pool it had taken from - SpendBox drains found before
        /// banked, so refunding into found would quietly convert a safe box into one that dies
        /// with the run. Ordering it this way deletes the case instead of handling it.
        /// </summary>
        void Take(Func<bool> move, RiftTake take)
        {
            if (SecuredThisRift < _capacity)
            {
                if (!move()) return;
                SecuredThisRift++;
            }
            else if (_loot.TotalBoxes > 0 && move())
            {
                _loot.SpendBox(out take.PaidFromFound);
                take.PaidBox = true;
            }
            else return;
            Taken.Add(take);
        }

        /// <summary>
        /// Moves one thing this tear secured back to the carried side and pays back its cost: the
        /// Rift Box to the pool it came from, or the free slot. A freed slot goes first to the
        /// latest piece a box paid for, and that box comes back - the same never-spend-a-box-
        /// while-a-slot-sits-free rule as <see cref="Take"/>.
        ///
        /// Refund BEFORE moving back: a secured Rift Box can itself have paid for a later piece,
        /// and only the refund puts it back in the banked pool to be returned from.
        /// </summary>
        void Return(RiftTake take)
        {
            if (!Taken.Contains(take)) return;
            if (take.Gear != null && _loot.BagFull) return;   // no room back in the bag
            RiftTake shifted = null;
            if (take.PaidBox) _loot.RefundBox(take.PaidFromFound);
            else
            {
                shifted = Taken.FindLast(x => x.PaidBox && x != take);
                if (shifted != null) { _loot.RefundBox(shifted.PaidFromFound); shifted.PaidBox = false; }
                else SecuredThisRift--;
            }

            bool back = take.Gear != null ? _loot.Unsecure(take.Gear)
                      : take.Tier.HasValue ? _loot.UnsecureTieredBox(take.Tier.Value)
                      : _loot.UnsecureRiftBox();
            if (back) { Taken.Remove(take); return; }

            // Could not move it back: undo the refund, so nothing is paid back for nothing.
            if (take.PaidBox) _loot.SpendBox(out _);
            else if (shifted != null) { _loot.SpendBox(out shifted.PaidFromFound); shifted.PaidBox = true; }
            else SecuredThisRift++;
        }

        /// <summary>What this tear secured, held on the tear for the same reason
        /// <see cref="SecuredThisRift"/> is.</summary>
        List<RiftTake> Taken => _rift != null ? _rift.Taken ??= new List<RiftTake>() : _takenNoRift;
        readonly List<RiftTake> _takenNoRift = new();

        /// <summary>How many have been pushed through this Rift's own capacity. Counted rather than
        /// inferred from SecuredCount, which includes everything banked at earlier Rifts - and
        /// held on the Rift, so reopening the screen does not reset it.</summary>
        int SecuredThisRift
        {
            get => _rift != null ? _rift.Secured : _securedNoRift;
            set { if (_rift != null) _rift.Secured = value; else _securedNoRift = value; }
        }
        int _securedNoRift;

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            HideTip();
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _cells.Clear();
            _order.Clear();
            _rift = null;
            _securedNoRift = 0;
            _takenNoRift.Clear();
        }

        void Update()
        {
            if (!IsOpen) return;

            // The cell size is fitted to the screen, so a resize (a phone turned, a canvas scaled
            // after it opened) lays it out again.
            if (_full && (_full.rect.size - _laidOutFor).sqrMagnitude > 1f) Refresh();

            _focusRects.Clear();
            if (_showSecured)
            {
                // Only the overlay's own cells (for their roll) and its CLOSE.
                foreach (var (rect, _) in _gearCells)
                    if (rect && _securedPanel && rect.IsChildOf(_cellsRoot) && Inside(_securedPanel, rect)) _focusRects.Add(rect);
                if (_securedClose) _focusRects.Add(_securedClose);
            }
            else
            {
                foreach (var (rect, _) in _cells) _focusRects.Add(rect);
                foreach (var (rect, _) in _gearCells) if (!_focusRects.Contains(rect)) _focusRects.Add(rect);
                if (_upBtn && _upBtn.gameObject.activeSelf) _focusRects.Add(_upBtn);
                if (_downBtn && _downBtn.gameObject.activeSelf) _focusRects.Add(_downBtn);
                if (_securedBtn && _securedBtn.gameObject.activeSelf) _focusRects.Add(_securedBtn);
                if (_extractBtn) _focusRects.Add(_extractBtn);
                if (_pushBtn) _focusRects.Add(_pushBtn);
            }
            Core.Controls.SetFocusCandidates(_focusRects);

            var hovered = Hovered(out var over);
            if (hovered == null) { if (_tip) HideTip(); }
            else if (hovered != _tipGear) ShowTip(hovered, over);

            // The [ E ] that opened this is still down on its opening frame.
            if (Time.frameCount == _openedFrame) return;

            if (Core.Controls.CancelTapped && _showSecured) { _showSecured = false; Refresh(); return; }
            if (Core.Controls.CancelTapped)
            {
                // Backing out is KEEP GOING - see the class header.
                var stay = _onPushOn;
                Close();
                stay?.Invoke();
                return;
            }

            if (!Core.Controls.Tapped(out var at)) return;
            bool Hit(RectTransform r) => r && r.gameObject.activeInHierarchy
                                         && RectTransformUtility.RectangleContainsScreenPoint(r, at, null);

            if (_showSecured)
            {
                // Nothing behind it answers; its CLOSE or a tap off the panel closes it.
                if (Hit(_securedClose) || !Hit(_securedPanel)) { _showSecured = false; Refresh(); }
                return;
            }
            if (Hit(_securedBtn)) { _showSecured = true; Refresh(); return; }

            foreach (var (rect, act) in _cells)
            {
                if (!Hit(rect)) continue;
                act();
                if (IsOpen) Refresh();
                return;
            }

            if (Hit(_upBtn)) { _scroll--; Refresh(); return; }
            if (Hit(_downBtn)) { _scroll++; Refresh(); return; }

            if (Hit(_extractBtn))
            {
                var go = _onExtract;
                Close();
                go?.Invoke();
                return;
            }

            if (Hit(_pushBtn))
            {
                var go = _onPushOn;
                Close();
                go?.Invoke();
            }
        }
    }
}
