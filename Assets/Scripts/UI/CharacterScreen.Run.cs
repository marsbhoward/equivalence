using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Art.Gear;
using Convergence.Core;
using Convergence.Exchange;

namespace Convergence.UI
{
    /// <summary>
    /// THE RUN'S SHEET (the user's calls, 2026-11-18, on the Mid-Run Sheet mock
    /// https://claude.ai/artifact/ASE6YPKQPrUS9MAALCMVMA). Mid-run the loadout cannot change, so the
    /// sheet leads with what does: no figure, two tabs -
    ///
    ///   RUN      the weapon arts in rotation order, each with what is inscribed on it, then the
    ///            benefits and burdens as cards that say what they do. Scrolls on a long run.
    ///   LOADOUT  the twelve slots, locked, each pressable to read the piece.
    ///
    /// The stats panel beside them reads the character (run stats) until something is pressed. The
    /// REORDER WEAPON ARTS plate stands in the header on both tabs, a gold hexagon no other button
    /// shares: lit and breathing in the door window, a dark outline saying when it opens otherwise.
    /// Opened by C or the GEAR pill straight from the fight, or from the pause menu's CHARACTER ring.
    /// </summary>
    public partial class CharacterScreen
    {
        enum RunTab { Run, Loadout }
        RunTab _tab;

        /// <summary>The run's floor, clock and element, for the header - set by GameBootstrap.</summary>
        public System.Func<string> RunLine;

        RectTransform _runFull, _tabBody, _pane, _paneContent, _runTabRect, _loadoutTabRect;
        Image _reorderPlate;
        Texture2D _plateTex;

        /// <summary>Each art card by its socket.</summary>
        readonly List<(int Socket, RectTransform Rect)> _artCards = new();

        /// <summary>The art the stats panel is reading (its socket), or -1.</summary>
        int _inspectedArt = -1;

        float _scroll, _scrollMax, _scrollFrom;
        bool _scrolling, _pressed;
        Vector2 _scrollOrigin, _pressAt;
        const float DragSlop = 144f;

        // Centre-anchored, on the 1920 x 1080 reference: the list on the left, the panel on the right.
        const float ListL = -910f, ListR = 330f, ListTop = 340f, ListBottom = -480f, PanelL = 370f, PanelR = 910f;
        const float CardH = 112f, ArtChipH = 40f, Gap = 10f;

        static readonly Color ArtIdle = new(0.098f, 0.106f, 0.141f), ArtChipIdle = new(0.13f, 0.14f, 0.19f);
        static readonly Color TabOn = new(0.20f, 0.17f, 0.09f), TabOff = new(0.07f, 0.075f, 0.1f);

        void BuildRunUi(RectTransform full)
        {
            _runFull = full;
            _tab = RunTab.Run;

            UiKit.Label(At(full, ListL, 430, -300, 500), "THIS RUN", 40, new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleLeft)
                .fontStyle = FontStyle.Bold;
            var p = _profile();
            string line = RunLine?.Invoke() ?? "";
            UiKit.Label(At(full, ListL, 390, -250, 426),
                line + $"        GEAR POWER {p.Gear.TotalPower():0}", 17, StatDim, TextAnchor.MiddleLeft);

            _runTabRect = TabButton(full, -230f, "RUN");
            _loadoutTabRect = TabButton(full, 10f, "LOADOUT");
            UiKit.Hint(At(full, -230, 376, 230, 402), "Q / E", "", 14, StatFaint, TextAnchor.MiddleCenter, "LB / RB");

            BuildReorderPlate(full);

            _statsPanel = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(PanelL, ListBottom), new Vector2(PanelR, ListTop), new Color(0.07f, 0.075f, 0.1f, 1f));

            BuildRunTab();
        }

        static RectTransform At(RectTransform parent, float x0, float y0, float x1, float y1)
            => UiKit.Rect(parent, "r", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x0, y0), new Vector2(x1, y1));

        RectTransform TabButton(RectTransform full, float x, string label)
        {
            var r = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, 410), new Vector2(x + 220f, 490), TabOff);
            UiKit.Label(r, label, 20, StatInk, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;
            return r;
        }

        void PaintTabs()
        {
            void Paint(RectTransform r, bool on)
            {
                if (r == null) return;
                r.GetComponent<Image>().color = on ? TabOn : TabOff;
                var t = r.GetComponentInChildren<Text>();
                if (t != null) t.color = on ? StatGold : StatDim;
            }
            Paint(_runTabRect, _tab == RunTab.Run);
            Paint(_loadoutTabRect, _tab == RunTab.Loadout);
        }

        void SwitchTab(RunTab tab)
        {
            if (_tab == tab) return;
            _tab = tab;
            _inspected = null;
            _inspectedEntry = null;
            _inspectedArt = -1;
            _noteStat = null;
            BuildRunTab();
            Refresh();
        }

        // ------------------------------------------------------------------ the tab's body

        void BuildRunTab()
        {
            if (_tabBody != null) Destroy(_tabBody.gameObject);
            _rows.Clear();
            _ledgerChips.Clear();
            _chipIdle.Clear();
            _artCards.Clear();
            _scroll = 0f;
            _scrolling = _pressed = false;

            _tabBody = UiKit.Rect(_runFull, "tab", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _pane = At(_tabBody, ListL, ListBottom, ListR, ListTop);
            _pane.gameObject.AddComponent<RectMask2D>();   // a rectangle clip, no stencil
            _paneContent = UiKit.Rect(_pane, "rows", new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            _paneContent.pivot = new Vector2(0.5f, 1f);

            float h = _tab == RunTab.Run ? BuildLedgerList() : BuildSlotList();
            _paneContent.sizeDelta = new Vector2(0f, h);
            _scrollMax = Mathf.Max(0f, h - (ListTop - ListBottom));

            string what = _tab == RunTab.Run ? "an art, benefit or burden" : "a piece";
            UiKit.Hint(At(_tabBody, ListL, -532, ListR, -492),
                "click " + what + " to read it, again for yours    -    [Q] [E] tabs    -    [C] to close",
                "tap " + what + " to read it, again for yours    -    BACK to close",
                16, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleLeft,
                GamepadGlyphs.Navigate + " to " + what + ", " + GamepadGlyphs.Confirm + " to read it    -    LB / RB tabs    -    "
                + GamepadGlyphs.Loadout + " to close");
            PaintTabs();
        }

        /// <summary>A label across the list, <paramref name="y"/> down from its top. Returns the height used.</summary>
        float Heading(float y, float x0, float x1, string text, Color ink)
        {
            var r = UiKit.Rect(_paneContent, "h", new Vector2(0, 1), new Vector2(0, 1), new Vector2(x0, -y - 24f), new Vector2(x1, -y));
            UiKit.Label(r, text, 15, ink, TextAnchor.MiddleLeft).fontStyle = FontStyle.Bold;
            return 32f;
        }

        float BuildLedgerList()
        {
            float width = ListR - ListL, y = 0f;
            var mods = _mods?.Invoke();
            if (mods == null) return 0f;

            // ---- the weapon arts, in rotation order, each holding what is inscribed on it
            int sockets = mods.SocketCount?.Invoke() ?? 0;
            var onArt = new List<ExchangeEntry>[sockets];
            for (int i = 0; i < sockets; i++) onArt[i] = new List<ExchangeEntry>();
            var boons = new List<ExchangeEntry>();
            var costs = new List<ExchangeEntry>();
            foreach (var e in mods.Held)
            {
                if (mods.StacksOf(e) <= 0) continue;
                int at = mods.SocketOf(e.Id);
                if (at >= 0 && at < sockets) onArt[at].Add(e);
                else (e.Kind == ExchangeKind.Boon ? boons : costs).Add(e);
            }

            if (sockets > 0)
            {
                y += Heading(y, 0, width, "WEAPON ARTS  -  IN ROTATION ORDER", ExchangeInk.Violet);
                float cw = (width - (sockets - 1) * Gap) / sockets;
                // Every card as tall as the fullest, so the row reads as one.
                float tallest = UiKit.TouchTarget;
                for (int i = 0; i < sockets; i++) tallest = Mathf.Max(tallest, ArtCardHeight(onArt[i], cw));
                for (int i = 0; i < sockets; i++)
                    ArtCard(i, mods.SocketArt?.Invoke(i), onArt[i], i * (cw + Gap), y, cw, tallest);
                y += tallest + 24f;
            }

            // ---- the rest: benefits on the left, burdens on the right
            if (boons.Count == 0 && costs.Count == 0)
            {
                var r = UiKit.Rect(_paneContent, "empty", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -y - 40f), new Vector2(width, -y));
                UiKit.Label(r, "no benefits or burdens yet - each floor's deal adds them here", 17, StatFaint, TextAnchor.MiddleLeft);
                return y + 40f;
            }
            float colW = (width - 2f * Gap) / 2f, rx = colW + 2f * Gap;
            Heading(y, 0, colW, $"BENEFITS  -  {boons.Count}", LedgerStrip.BoonTint);
            y += Heading(y, rx, width, $"BURDENS  -  {costs.Count}", LedgerStrip.CostTint);
            for (int i = 0; i < boons.Count; i++) EntryCard(mods, boons[i], 0f, y + i * (CardH + Gap), colW);
            for (int i = 0; i < costs.Count; i++) EntryCard(mods, costs[i], rx, y + i * (CardH + Gap), colW);
            int rows = Mathf.Max(boons.Count, costs.Count);
            return y + rows * (CardH + Gap);
        }

        static float ChipWidth(ExchangeEntry e, float max) => Mathf.Min(max, e.Name.Length * 9.5f + 28f);

        static float ArtCardHeight(List<ExchangeEntry> on, float w)
        {
            float x = 0f, inner = w - 24f;
            int rows = on.Count > 0 ? 1 : 0;
            foreach (var e in on)
            {
                float cw = ChipWidth(e, inner);
                if (x > 0f && x + cw > inner) { rows++; x = 0f; }
                x += cw + 8f;
            }
            return 52f + Mathf.Max(1, rows) * (ArtChipH + 8f) + 6f;
        }

        void ArtCard(int socket, Combat.Moveset art, List<ExchangeEntry> on, float x, float y, float w, float h)
        {
            var card = UiKit.Panel(_paneContent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y - h), new Vector2(x + w, -y), ArtIdle);
            UiKit.Panel(card, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -4), Vector2.zero, ExchangeInk.Violet)
                .GetComponent<Image>().raycastTarget = false;
            _artCards.Add((socket, card));

            var head = UiKit.Rect(card, "name", new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -44), new Vector2(-12, -10));
            UiKit.Label(head, art != null ? art.DisplayName : "-", 20, ExchangeInk.Violet, TextAnchor.MiddleLeft)
                .fontStyle = FontStyle.Bold;
            UiKit.Label(head, ExchangeEntry.Roman(socket + 1), 15, StatDim, TextAnchor.MiddleRight);

            if (on.Count == 0)
            {
                UiKit.Label(UiKit.Rect(card, "none", new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -52 - ArtChipH), new Vector2(-12, -52)),
                    "nothing inscribed", 14, StatFaint, TextAnchor.MiddleLeft);
                return;
            }
            float cx = 0f, cy = 52f, inner = w - 24f;
            foreach (var e in on)
            {
                float cw = ChipWidth(e, inner);
                if (cx > 0f && cx + cw > inner) { cx = 0f; cy += ArtChipH + 8f; }
                var chip = UiKit.Panel(card, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(12 + cx, -cy - ArtChipH), new Vector2(12 + cx + cw, -cy), ArtChipIdle);
                var t = UiKit.Label(chip, e.Name, 15, ExchangeInk.Tint(e), TextAnchor.MiddleCenter);
                t.fontStyle = FontStyle.Bold;
                t.raycastTarget = false;
                _ledgerChips[e.Id] = chip;
                _chipIdle[e.Id] = ArtChipIdle;
                cx += cw + 8f;
            }
        }

        void EntryCard(RunModifiers mods, ExchangeEntry e, float x, float y, float w)
        {
            var card = UiKit.Panel(_paneContent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y - CardH), new Vector2(x + w, -y), ArtIdle);
            _ledgerChips[e.Id] = card;
            _chipIdle[e.Id] = ArtIdle;
            var tint = ExchangeInk.Tint(e);

            var well = UiKit.Panel(card, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, -36), new Vector2(84, 36), new Color(0.13f, 0.14f, 0.19f));
            well.GetComponent<Image>().raycastTarget = false;
            var img = UiKit.Rect(well, "i", Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -12)).gameObject.AddComponent<Image>();
            img.sprite = ExchangeGlyphs.Get(e);
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = tint;

            int n = mods.StacksOf(e);
            string right = e.CapName != null && n >= e.MaxStacks ? e.CapKind.ToUpperInvariant()
                         : e.Stackable ? $"{ExchangeEntry.Roman(n)} / {ExchangeEntry.Roman(e.MaxStacks)}" : "";
            var head = UiKit.Rect(card, "name", new Vector2(0, 1), new Vector2(1, 1), new Vector2(100, -42), new Vector2(-14, -10));
            UiKit.Label(head, e.Name, 20, tint, TextAnchor.MiddleLeft).fontStyle = FontStyle.Bold;
            if (right.Length > 0)
                UiKit.Label(head, right, 14, e.CapName != null && n >= e.MaxStacks
                    ? (e.Kind == ExchangeKind.Boon ? ExchangeScale.RubedoText : ExchangeScale.NigredoText) : StatDim, TextAnchor.MiddleRight);

            var fx = UiKit.Label(UiKit.Rect(card, "fx", Vector2.zero, new Vector2(1, 1), new Vector2(100, 10), new Vector2(-14, -46)),
                Glossary.Plain(e.Effect), 15, new Color(0.73f, 0.75f, 0.80f), TextAnchor.UpperLeft);
            fx.horizontalOverflow = HorizontalWrapMode.Wrap;
            fx.verticalOverflow = VerticalWrapMode.Truncate;
        }

        float BuildSlotList()
        {
            float width = ListR - ListL, y = Heading(0f, 0f, width, "LOADOUT  -  LOCKED FOR THIS RUN", StatDim);
            var slots = GearSlots.Selectable;
            const int cols = 3;
            float cw = (width - (cols - 1) * Gap) / cols, ch = UiKit.TouchTarget, step = ch + 6f;
            int perCol = Mathf.CeilToInt(slots.Length / (float)cols);
            for (int i = 0; i < slots.Length; i++)
            {
                int col = i / perCol, row = i % perCol;
                float x = col * (cw + Gap), top = y + row * step;
                _rows.Add(SlotCard(_paneContent, slots[i], new Vector2(0, 1), new Vector2(x, -top - ch), new Vector2(x + cw, -top)));
            }
            return y + perCol * step;
        }

        // ------------------------------------------------------------------ the reorder plate

        /// <summary>The REORDER WEAPON ARTS plate: a long hexagon with a diamond sigil, gold in the door
        /// window, a dark outline outside it. Drawn once per open - the window cannot change while the
        /// sheet is up.</summary>
        void BuildReorderPlate(RectTransform full)
        {
            bool lit = ReorderAvailable;
            const float x0 = PanelL, x1 = PanelR, y0 = 370f, y1 = 496f;
            _plateTex = PlateTexture(Mathf.RoundToInt((x1 - x0) / 2f), Mathf.RoundToInt((y1 - y0) / 2f), lit);
            var rect = At(full, x0, y0, x1, y1);
            _reorderPlate = rect.gameObject.AddComponent<Image>();
            _reorderPlate.sprite = Sprite.Create(_plateTex, new Rect(0, 0, _plateTex.width, _plateTex.height), new Vector2(0.5f, 0.5f));
            var ink = lit ? new Color(0.10f, 0.08f, 0.03f) : new Color(0.55f, 0.48f, 0.32f);
            float textL = (y1 - y0) * 0.5f * 0.9f + (y1 - y0) * 0.95f;
            var name = UiKit.Label(UiKit.Rect(rect, "t", new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(textL, -2), new Vector2(-40, -18)),
                "REORDER WEAPON ARTS", 22, ink, TextAnchor.LowerLeft);
            name.fontStyle = FontStyle.Bold;
            name.raycastTarget = false;
            UiKit.Label(UiKit.Rect(rect, "s", new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(textL, 18), new Vector2(-40, -4)),
                lit ? "open until the door" : "opens after a floor's reward", 15, ink, TextAnchor.UpperLeft).raycastTarget = false;
            // Only the lit plate presses; the dark one is there so a player learns where it is.
            _reorderButtonRect = lit ? rect : null;
        }

        static Texture2D PlateTexture(int w, int h, bool lit)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            Color rim = lit ? new Color(0.96f, 0.85f, 0.50f) : new Color(0.24f, 0.19f, 0.10f);
            Color top = lit ? new Color(0.93f, 0.79f, 0.43f) : new Color(0.075f, 0.062f, 0.035f);
            Color low = lit ? new Color(0.77f, 0.58f, 0.23f) : new Color(0.055f, 0.047f, 0.027f);
            Color sigil = lit ? new Color(0.10f, 0.08f, 0.03f) : new Color(0.55f, 0.48f, 0.32f);
            float tip = h * 0.5f * 0.9f, cx = tip + h * 0.5f, cy = h * 0.5f, r = h * 0.3f;
            const float rimW = 2f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float slope = tip * Mathf.Abs((y + 0.5f) * 2f / h - 1f);
                    float l = x + 0.5f - slope, rr = w - (x + 0.5f) - slope;
                    var c = new Color(0, 0, 0, 0);
                    if (l >= 0f && rr >= 0f)
                    {
                        bool edge = l < rimW * 1.4f || rr < rimW * 1.4f || y < rimW || y >= h - rimW;
                        c = edge ? rim : Color.Lerp(low, top, y / (float)(h - 1));
                        float d = Mathf.Abs(x + 0.5f - cx) + Mathf.Abs(y + 0.5f - cy);
                        if ((d <= r && d >= r - 2.5f) || d <= r * 0.32f) c = sigil;
                    }
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>The lit plate breathes, so the one window it works in is not missed.</summary>
        void BreathePlate()
        {
            if (_reorderPlate == null || !ReorderAvailable) return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.8f);
            _reorderPlate.color = Color.Lerp(new Color(0.82f, 0.82f, 0.82f), Color.white, k);
        }

        // ------------------------------------------------------------------ the art read

        /// <summary>The stats panel reading a weapon art: what is inscribed on it, each with its effect.
        /// False when no art is pressed.</summary>
        bool BuildArtStats()
        {
            if (_inspectedArt < 0) return false;
            var mods = _mods?.Invoke();
            int sockets = mods?.SocketCount?.Invoke() ?? 0;
            if (mods == null || _inspectedArt >= sockets) { _inspectedArt = -1; return false; }

            _noteDefault = null;
            _noteShown = null;
            _noteTitle = _noteText = null;
            var stack = EntryStack();
            var art = mods.SocketArt?.Invoke(_inspectedArt);
            EntryLine(stack, art != null ? art.DisplayName : "-", 24, ExchangeInk.Violet, FontStyle.Bold);
            EntryLine(stack, $"weapon art {ExchangeEntry.Roman(_inspectedArt + 1)} of {ExchangeEntry.Roman(sockets)}", 14, StatDim);

            bool any = false;
            foreach (var e in mods.Held)
            {
                if (mods.StacksOf(e) <= 0 || mods.SocketOf(e.Id) != _inspectedArt) continue;
                any = true;
                EntryLine(stack, (e.Kind == ExchangeKind.Boon ? "BENEFIT" : "BURDEN") + "  -  " + e.Name.ToUpperInvariant(), 14,
                          ExchangeInk.Tint(e), FontStyle.Bold);
                EntryLine(stack, Glossary.Plain(e.Effect), 17, StatInk);
            }
            if (!any)
                EntryLine(stack, "Nothing is inscribed on this art yet. A floor's deal can inscribe a weapon art benefit on it.", 16, StatDim);
            return true;
        }

        /// <summary>A pressed art card lets go of a pressed entry or slot, and the other way round.</summary>
        bool TapArt(Vector2 p)
        {
            foreach (var (socket, rect) in _artCards)
            {
                if (rect == null || !RectTransformUtility.RectangleContainsScreenPoint(rect, p, null)) continue;
                _inspectedArt = _inspectedArt == socket ? -1 : socket;
                _inspected = null;
                _inspectedEntry = null;
                _noteStat = null;
                Refresh();
                return true;
            }
            return false;
        }

        bool TapSlot(Vector2 p)
        {
            foreach (var row in _rows)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(row.Rect, p, null)) continue;
                _inspected = _inspected == row.Slot ? null : row.Slot;
                _inspectedEntry = null;
                _inspectedArt = -1;
                _noteStat = null;
                Refresh();
                return true;
            }
            return false;
        }

        void PaintArtCards()
        {
            foreach (var (socket, rect) in _artCards)
                if (rect != null) rect.GetComponent<Image>().color = socket == _inspectedArt ? ChipPressed : ArtIdle;
        }

        // ------------------------------------------------------------------ input

        void RunUpdate()
        {
            // Pad focus: the tabs, the lit plate, the list's art cards, chips and cards (or slots), the tiles.
            _focusRects.Add(_runTabRect);
            _focusRects.Add(_loadoutTabRect);
            if (_reorderButtonRect != null) _focusRects.Add(_reorderButtonRect);
            foreach (var (_, rect) in _artCards) _focusRects.Add(rect);
            RegisterLedgerFocus();
            foreach (var r in _rows) _focusRects.Add(r.Rect);
            foreach (var t in _statTiles) _focusRects.Add(t.Rect);
            Controls.SetFocusCandidates(_focusRects);

            UpdateFootnote();
            BreathePlate();

            if (Controls.TabPrevTapped || Controls.TabNextTapped)
            {
                SwitchTab(_tab == RunTab.Run ? RunTab.Loadout : RunTab.Run);
                return;
            }

            ScrollPane();
            var focused = Controls.Focused;
            if (focused != null && _paneContent != null && focused.IsChildOf(_paneContent))
            {
                float d = UiKit.ScrollToShow(_pane, focused);
                if (d != 0f) { _scroll += d; ApplyScroll(); }
            }

            if (Controls.Tapped(out var p))
            {
                if (Hit(_runTabRect, p)) { SwitchTab(RunTab.Run); return; }
                if (Hit(_loadoutTabRect, p)) { SwitchTab(RunTab.Loadout); return; }
                if (Hit(_reorderButtonRect, p)) { OpenReorderPanel(); return; }
                if (TapStats(p)) return;
                if (Hit(_pane, p)) { _pressAt = p; _pressed = true; }
            }

            // The list commits on RELEASE: the same press may be the start of a drag through it.
            if (!Controls.PointerReleased || !_pressed) return;
            _pressed = false;
            var at = Controls.PointerPosition;
            if ((at - _pressAt).sqrMagnitude > DragSlop || !Hit(_pane, at)) return;
            if (_tab == RunTab.Run) { if (!TapLedger(at)) TapArt(at); }
            else TapSlot(at);
        }


        void ScrollPane()
        {
            if (_pane == null) return;
            var at = Controls.PointerPosition;
            bool inside = Hit(_pane, at);
            if (inside && Mathf.Abs(Controls.Zoom) > 0.01f) { _scroll -= Controls.Zoom * 90f; ApplyScroll(); }

            if (Controls.Pinching) _scrolling = false;
            else if (Controls.Tapped(out var down) && inside && _scrollMax > 0f)
            {
                _scrolling = true;
                _scrollOrigin = down;
                _scrollFrom = _scroll;
            }
            if (!Controls.PointerHeld(out _)) _scrolling = false;
            if (!_scrolling) return;
            float dy = at.y - _scrollOrigin.y;
            if (dy * dy <= DragSlop) return;
            _scroll = _scrollFrom + dy / Mathf.Max(0.0001f, _pane.lossyScale.y);
            ApplyScroll();
        }

        void ApplyScroll()
        {
            _scroll = Mathf.Clamp(_scroll, 0f, _scrollMax);
            if (_paneContent != null) _paneContent.anchoredPosition = new Vector2(0f, _scroll);
        }

        void CloseRun()
        {
            if (_reorderPlate != null && _reorderPlate.sprite != null) Destroy(_reorderPlate.sprite);
            if (_plateTex != null) Destroy(_plateTex);
            _plateTex = null;
            _reorderPlate = null;
            _runFull = _tabBody = _pane = _paneContent = _runTabRect = _loadoutTabRect = null;
            _artCards.Clear();
            _chipIdle.Clear();
            _inspectedArt = -1;
        }
    }
}
