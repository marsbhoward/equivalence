using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Art.Gear;
using Convergence.Chain;

namespace Convergence.UI
{
    /// <summary>
    /// The run's inventory as two grids, the Rift screen's style (the user's design, 2026-11-06):
    /// CARRIED on top - the bag's pieces and its empty places, exactly its 5, 7 or 10, five to a row
    /// - and SECURED below. Boxes don't take bag space, so they stand in a column at the top left,
    /// mirroring the Rift Box count at the top right: the carried ones, then the secured. Cells fit the space as the Rift screen's do, a piece's roll
    /// shows on hover (GearCells.Tip), and the Rift Boxes held are counted by the title. No rift
    /// flakes here - this is the bag, not the tear.
    ///
    /// SELECT, THEN DROP. On the Rift screen a tap moves a cell and a tap moves it back; a drop here
    /// can't be taken back in the menu, so a tap selects and the line under the grid offers DROP and
    /// says what it does. A gear piece goes down beside the player; a carried Rift Box asks first
    /// (the bootstrap's confirm). Tier boxes open at the run's end and aren't dropped.
    /// </summary>
    public partial class InventoryScreen
    {
        const int PerRow = 5;
        const float Gap = 14f, Head = 44f, Top = -132f, MinCell = 96f, MaxCell = 210f;

        static readonly Color RiskInk = new(0.95f, 0.62f, 0.55f);
        static readonly Color SafeInk = new(0.62f, 0.92f, 0.68f);
        static readonly Color FreeInk = new(0.94f, 0.88f, 0.82f);
        static readonly Color DeadInk = new(0.50f, 0.47f, 0.52f);
        static readonly Color Pick = new(0.80f, 0.72f, 1f);

        /// <summary>One cell: a piece, a stack of one kind of box, or an empty place in the bag.</summary>
        class Cell
        {
            public MintedGearRecord Gear;
            public Art.BoxArt.Kind? Box;
            public LootTier? Tier;        // a tier box's stack
            public bool Rift;             // the carried Rift Boxes
            public int Count;
            public bool Empty, Safe;
        }

        RectTransform _full, _cellsRoot, _dropBtn;
        Text _boxCount, _carriedTitle, _securedTitle, _action;
        Image _boxIcon;
        float _cell = 168f;
        Vector2 _laidOutFor;

        // ---- what is selected: a carried piece, the carried Rift Boxes, or a tier box's stack
        MintedGearRecord _selGear;
        bool _selRift;
        LootTier? _selTier;

        readonly List<(RectTransform Rect, Cell Cell)> _runCells = new();
        GameObject _tip;
        MintedGearRecord _tipGear;

        void BuildRun(Transform canvas)
        {
            _root = new GameObject("InventoryScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            _full = (RectTransform)_root.transform;
            _full.anchorMin = Vector2.zero; _full.anchorMax = Vector2.one;
            _full.offsetMin = Vector2.zero; _full.offsetMax = Vector2.zero;
            _selGear = null; _selRift = false; _selTier = null;

            UiKit.Panel(_full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.04f, 0.04f, 0.055f, 0.97f));

            UiKit.Label(UiKit.Rect(_full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -88), new Vector2(0, -32)),
                "INVENTORY", 40, new Color(0.90f, 0.91f, 0.96f), TextAnchor.MiddleCenter);
            UiKit.Label(UiKit.Rect(_full, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -118), new Vector2(0, -88)),
                "what this run is carrying", 18, Dim, TextAnchor.MiddleCenter);

            // ---- the Rift Boxes held, by the title as on the Rift screen
            _boxIcon = BoxIcon.Add(_full, Art.BoxArt.Kind.Rift, false, new Vector2(1f, 1f), new Vector2(-190f, -96f));
            _boxCount = UiKit.Label(UiKit.Rect(_full, "bc", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-150, -96), new Vector2(-40, -40)), "", 30, Pick, TextAnchor.MiddleLeft);

            _carriedTitle = UiKit.Label(UiKit.Rect(_full, "ch", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                Vector2.zero, Vector2.zero), "", 21, RiskInk);
            _securedTitle = UiKit.Label(UiKit.Rect(_full, "sh", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                Vector2.zero, Vector2.zero), "", 21, SafeInk);
            _action = UiKit.Label(UiKit.Rect(_full, "act", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                Vector2.zero, Vector2.zero), "", 17, Dim, TextAnchor.MiddleLeft);
            _dropBtn = UiKit.Panel(_full, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero,
                new Color(0.26f, 0.15f, 0.14f, 1f));
            UiKit.Label(_dropBtn, "DROP", 22, new Color(0.98f, 0.76f, 0.68f), TextAnchor.MiddleCenter);

            // ---- back ----
            _backRect = UiKit.Panel(_full, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-220, 32), new Vector2(220, 132), new Color(0.12f, 0.13f, 0.17f, 1f));
            UiKit.Label(_backRect, "BACK", 24, new Color(0.82f, 0.85f, 0.92f), TextAnchor.MiddleCenter);

            RefreshRun();
        }

        void CloseRun()
        {
            HideRunTip();
            _runCells.Clear();
            _full = _cellsRoot = _dropBtn = null;
            _selGear = null; _selRift = false; _selTier = null;
        }

        static readonly LootTier[] BoxTiers =
        {
            LootTier.BlackDiamond, LootTier.Diamond, LootTier.Gold, LootTier.Silver, LootTier.Bronze,
        };

        void RefreshRun()
        {
            if (_full == null || _loot == null) return;
            _runCells.Clear();
            HideRunTip();
            if (_cellsRoot) Destroy(_cellsRoot.gameObject);
            _cellsRoot = UiKit.Rect(_full, "cells", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // ---- the cells: carried boxes rarest first, the Rift Boxes, the pieces, the bag's empty places
            var boxRisk = new List<Cell>();
            foreach (var t in BoxTiers)
                if (_loot.FoundTieredBoxes(t) > 0)
                    boxRisk.Add(new Cell { Box = BoxIcon.KindOf(t), Tier = t, Count = _loot.FoundTieredBoxes(t) });
            if (_loot.FoundBoxes > 0) boxRisk.Add(new Cell { Box = Art.BoxArt.Kind.Rift, Rift = true, Count = _loot.FoundBoxes });
            var carried = new List<Cell>();
            foreach (var it in _loot.Carried) carried.Add(new Cell { Gear = it });
            if (_loot.CarryLimit < 1000)
                for (int i = _loot.CarriedCount; i < _loot.CarryLimit; i++) carried.Add(new Cell { Empty = true });

            var boxSafe = new List<Cell>();
            foreach (var t in BoxTiers)
                if (_loot.SecuredTieredBoxes(t) > 0)
                    boxSafe.Add(new Cell { Box = BoxIcon.KindOf(t), Count = _loot.SecuredTieredBoxes(t), Safe = true });
            if (_loot.BankedBoxes > 0) boxSafe.Add(new Cell { Box = Art.BoxArt.Kind.Rift, Count = _loot.BankedBoxes, Safe = true });
            var secured = new List<Cell>();
            foreach (var it in _loot.Secured) secured.Add(new Cell { Gear = it, Safe = true });

            // A selection that has gone (dropped, or the last of a stack) is let go.
            if (_selGear != null && !Contains(_loot.Carried, _selGear)) _selGear = null;
            if (_selRift && _loot.FoundBoxes <= 0) _selRift = false;
            if (_selTier.HasValue && _loot.FoundTieredBoxes(_selTier.Value) <= 0) _selTier = null;

            int boxes = _loot.TotalBoxes;
            _boxCount.text = $"x{boxes}";
            _boxIcon.color = boxes > 0 ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            string bag = _loot.CarryLimit < 1000 ? $"   -   gear {_loot.CarriedCount} of {_loot.CarryLimit}" : "";
            _carriedTitle.text = $"CARRIED{bag}      <size=16><color=#B5736A>lost if you die before the next Rift</color></size>";
            int home = _loot.HomeBoxes;
            _securedTitle.text = "SECURED      <size=16><color=#6FA27B>kept whatever happens"
                               + (home > 0 ? $"  -  {home} more Rift Box{(home == 1 ? "" : "es")} left at home" : "")
                               + "</color></size>";

            DrawBoxColumn(boxRisk, boxSafe);

            // ---- layout: rows of five; the cell size fills the space between the title and BACK
            _laidOutFor = _full.rect.size;
            int carriedRows = Mathf.Max(1, Mathf.CeilToInt(carried.Count / (float)PerRow));
            int securedRows = Mathf.Max(1, Mathf.CeilToInt(secured.Count / (float)PerRow));
            const float actionH = 76f;
            float availH = _full.rect.height + Top - 2 * Head - actionH - 22f - (132f + 36f);
            float availW = _full.rect.width - 2 * 290f;
            float byH = (availH - (carriedRows + securedRows - 2) * Gap) / (carriedRows + securedRows);
            float byW = (availW - (PerRow - 1) * Gap) / PerRow;
            _cell = Mathf.Clamp(Mathf.Min(byH, byW), MinCell, MaxCell);

            float blockW = PerRow * (_cell + Gap) - Gap, left = -blockW * 0.5f;
            float y = Top;
            Place(_carriedTitle, left, y, Mathf.Max(blockW, 900f), Head);
            y -= Head;
            for (int i = 0; i < carried.Count; i++)
                Draw(carried[i], new Vector2(left + (i % PerRow) * (_cell + Gap), y - (i / PerRow) * (_cell + Gap)));
            y -= carriedRows * (_cell + Gap) - Gap + 10f;

            // ---- the line under the bag: what the selection is, and DROP when it can go
            bool canDrop = (_selGear != null && _onDropGear != null) || (_selRift && _onDropBox != null);
            _action.text = _selGear != null ? $"{_selGear.DisplayName}  -  lies on the floor beside you until you leave the floor"
                         : _selRift ? "Rift Box  -  a dropped Rift Box is gone"
                         : _selTier.HasValue ? $"{(_selTier == LootTier.BlackDiamond ? "Black Diamond" : _selTier.ToString())} box  -  boxes open at the end of the run; they aren't dropped"
                         : "tap a carried piece to drop it";
            _dropBtn.gameObject.SetActive(canDrop);
            _dropBtn.offsetMin = new Vector2(left, y - actionH + 10f);
            _dropBtn.offsetMax = new Vector2(left + 170f, y);
            Place(_action, canDrop ? left + 190f : left, y, blockW + 200f, actionH - 10f);
            y -= actionH + 12f;

            Place(_securedTitle, left, y, Mathf.Max(blockW, 900f), Head);
            y -= Head;
            if (secured.Count == 0)
                UiKit.Label(UiKit.Rect(_cellsRoot, "none", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(left, y - 40f), new Vector2(left + 400f, y)), "nothing yet", 18, DeadInk, TextAnchor.MiddleLeft);
            for (int i = 0; i < secured.Count; i++)
                Draw(secured[i], new Vector2(left + (i % PerRow) * (_cell + Gap), y - (i / PerRow) * (_cell + Gap)));
        }

        static bool Contains(IReadOnlyList<MintedGearRecord> list, MintedGearRecord it)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == it) return true;
            return false;
        }

        static void Place(Text label, float left, float y, float width, float height)
        {
            var rt = label.rectTransform;
            rt.offsetMin = new Vector2(left, y - height + 6f);
            rt.offsetMax = new Vector2(left + width, y);
        }

        bool Selected(Cell c)
            => (c.Gear != null && !c.Safe && c.Gear == _selGear) || (c.Rift && _selRift)
            || (c.Tier.HasValue && _selTier == c.Tier);

        const float BoxCell = 118f;

        /// <summary>The boxes, top left: the carried ones under BOXES - AT RISK, the secured under
        /// SECURED. Fixed size - the column isn't part of the fitted grid.</summary>
        void DrawBoxColumn(List<Cell> risk, List<Cell> safe)
        {
            float y = -36f;
            UiKit.Label(UiKit.Rect(_cellsRoot, "bxh", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(50, y - 30), new Vector2(50 + BoxCell + 120, y)),
                "BOXES - AT RISK", 15, new Color(0.72f, 0.45f, 0.42f), TextAnchor.MiddleLeft);
            y -= 36f;
            if (risk.Count == 0)
            {
                UiKit.Label(UiKit.Rect(_cellsRoot, "bxn", new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(50, y - 28), new Vector2(50 + BoxCell + 60, y)), "none", 15, DeadInk, TextAnchor.MiddleLeft);
                y -= 34f;
            }
            foreach (var c in risk) { Draw(c, new Vector2(50, y), BoxCell, corner: true); y -= BoxCell + 10f; }
            if (safe.Count == 0) return;
            y -= 10f;
            UiKit.Label(UiKit.Rect(_cellsRoot, "bxs", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(50, y - 30), new Vector2(50 + BoxCell + 120, y)),
                "SECURED", 15, new Color(0.45f, 0.66f, 0.50f), TextAnchor.MiddleLeft);
            y -= 36f;
            foreach (var c in safe) { Draw(c, new Vector2(50, y), BoxCell, corner: true); y -= BoxCell + 10f; }
        }

        void Draw(Cell c, Vector2 origin, float size = -1f, bool corner = false)
        {
            var a = corner ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f);
            float side = size > 0f ? size : _cell;
            Color frame = c.Empty ? new Color(0.45f, 0.40f, 0.36f)
                        : c.Gear != null ? CharacterScreen.TierColor(c.Gear.Tier)
                        : c.Safe ? new Color(0.32f, 0.50f, 0.38f) : new Color(0.40f, 0.36f, 0.42f);
            if (Selected(c)) frame = Pick;
            var cell = UiKit.Panel(_cellsRoot, a, a, origin + new Vector2(0, -side), origin + new Vector2(side, 0), frame);
            float edge = Selected(c) ? 6f : 3f;
            var inner = UiKit.Panel(cell, Vector2.zero, Vector2.one, new Vector2(edge, edge), new Vector2(-edge, -edge),
                c.Empty ? new Color(0.06f, 0.055f, 0.08f, 1f)
                : c.Safe ? new Color(0.09f, 0.15f, 0.11f, 1f) : new Color(0.11f, 0.10f, 0.13f, 1f));

            if (c.Empty)
                UiKit.Label(UiKit.Rect(inner, "e", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 6), new Vector2(0, 30)),
                    "empty", 15, FreeInk, TextAnchor.MiddleCenter);
            else if (c.Box.HasValue)
            {
                BoxIcon.Add(inner, c.Box.Value, false, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f));
                if (c.Count > 1)
                    UiKit.Label(UiKit.Rect(inner, "n", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -34), new Vector2(-8, -4)),
                        $"x{c.Count}", 20, FreeInk, TextAnchor.MiddleRight);
            }
            else if (c.Gear != null) GearCells.Picture(inner, c.Gear);

            if (!c.Empty) _runCells.Add((cell, c));
        }

        // ------------------------------------------------------------------ input

        void RunFocus(List<RectTransform> focus)
        {
            if (_full && (_full.rect.size - _laidOutFor).sqrMagnitude > 1f) RefreshRun();
            foreach (var (rect, c) in _runCells) if (rect && !c.Safe) focus.Add(rect);
            foreach (var (rect, c) in _runCells) if (rect && c.Safe && c.Gear != null) focus.Add(rect);
            if (_dropBtn && _dropBtn.gameObject.activeSelf) focus.Add(_dropBtn);
        }

        /// <summary>The piece under the pointer (or the pad's focus) shows its roll.</summary>
        void RunHover()
        {
            var focused = Core.Controls.GamepadMode ? Core.Controls.Focused : null;
            RectTransform over = null;
            MintedGearRecord gear = null;
            foreach (var (rect, c) in _runCells)
            {
                if (!rect || c.Gear == null) continue;
                bool hit = focused != null ? rect == focused
                    : RectTransformUtility.RectangleContainsScreenPoint(rect, Core.Controls.PointerPosition, null);
                if (hit) { over = rect; gear = c.Gear; break; }
            }
            if (gear == null) { if (_tip) HideRunTip(); }
            else if (gear != _tipGear)
            {
                HideRunTip();
                _tipGear = gear;
                _tip = GearCells.Tip(_full, gear, over);
            }
        }

        void HideRunTip()
        {
            if (_tip) Destroy(_tip);
            _tip = null;
            _tipGear = null;
        }

        void RunTap(Vector2 p)
        {
            bool Hit(RectTransform r) => r && r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, p, null);

            if (Hit(_dropBtn)) { Drop(); return; }
            foreach (var (rect, c) in _runCells)
            {
                if (!Hit(rect) || c.Safe) continue;
                // A second tap on the same cell lets it go.
                bool same = Selected(c);
                _selGear = null; _selRift = false; _selTier = null;
                if (!same)
                {
                    if (c.Gear != null) _selGear = c.Gear;
                    else if (c.Rift) _selRift = true;
                    else if (c.Tier.HasValue) _selTier = c.Tier;
                }
                RefreshRun();
                return;
            }
        }

        void Drop()
        {
            if (_selGear != null)
            {
                var gear = _selGear;
                _selGear = null;
                if (_loot.DropCarried(gear)) _onDropGear?.Invoke(gear);
                RefreshRun();
                return;
            }
            if (_selRift)
            {
                // Closed first, quietly - the confirm is the bootstrap's, and a "no" reopens this.
                var ask = _onDropBox;
                var closed = _onClosed;
                _onClosed = null;
                Close();
                ask?.Invoke(closed);
            }
        }
    }
}
