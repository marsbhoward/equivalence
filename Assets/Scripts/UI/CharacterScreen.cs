using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The loadout screen. Opens with C, pauses the game, and shows the character large
    /// alongside all twelve slots.
    ///
    /// INSPECT ONLY, in the hub and mid-run alike (2026-11-12): the stats panel reads the
    /// CHARACTER by default; pressing a slot reads that piece instead, and pressing it again goes
    /// back. Nothing here equips - gear changes at the transmutation circle (TransmutationScreen). Gear changes at the transmutation circle (TransmutationScreen).
    ///
    /// This exists because the arena camera can never show gear properly - even tightened, a
    /// character is ~12% of screen height, which is fine for combat readability but nowhere near
    /// enough to appreciate a cosmetic worth real money. The collectible tiers need somewhere
    /// they are actually looked at.
    ///
    /// The preview is a second rig built far from the arena and filmed by its own camera into a
    /// RenderTexture, so it can be posed and lit independently of gameplay.
    /// </summary>
    public partial class CharacterScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        /// <summary>
        /// True while a run is live. The sheet is inspect-only everywhere now; this only decides
        /// the hint's wording, the ledger and whether the reorder crack can exist. Decided once in
        /// Open(): a run cannot start or end while this screen is open.
        /// </summary>
        bool Locked;

        /// <summary>
        /// True for exactly one window: after a floor's reward is taken, until the player reaches
        /// the door that follows it. The one crack in Locked - reordering the wheel, never
        /// re-equipping gear - and like Locked it is decided once in Open() rather than read live,
        /// since the door cannot open or close while this screen is up.
        ///
        /// This only says the crack EXISTS - it no longer means the reorder window is open. It
        /// used to: opening the sheet during this window dropped the player straight into the
        /// full-screen swap overlay with no way to just look first. Now it gates whether the
        /// "REORDER WEAPON ARTS" button on the ordinary sheet is even there to press.
        /// </summary>
        bool ReorderAvailable;

        /// <summary>
        /// The reorder window is up (CharacterScreen.Reorder.cs). Reset on every Open()/Close().
        /// </summary>
        bool ReorderPanelOpen;

        /// <summary>
        /// Opened by <see cref="OpenReorder"/> (the floor reward's REORDER ARTS button): the
        /// screen exists only for the reorder window, so closing the window closes the screen and
        /// BACK does too - the reward screen behind it is what the player returns to.
        /// </summary>
        bool _reorderOnly;

        System.Func<CharacterProfile> _profile;

        /// <summary>
        /// The run's ledger, or null outside a run. The sheet is reachable from the hub as well
        /// as mid-run, and between runs there is genuinely nothing to show.
        /// </summary>
        System.Func<Exchange.RunModifiers> _mods;
        System.Action _toggleHelm;
        System.Func<Player.PlayerController> _player;
        System.Func<bool> _reorderOpen;
        System.Func<(StatPercents Stats, float MaxHp)> _character;
        ElementType _element;

        /// <summary>The slot the stats panel is reading, or null for the character.</summary>
        GearSlot? _inspected;

        /// <summary>Fired once when this screen closes, or null. Mid-run the pause menu opens this
        /// screen with a callback that reopens the menu, so backing out lands there rather than
        /// on the game. Null for the hub, where closing just returns to the room.</summary>
        System.Action _onClosed;

        GameObject _root;
        readonly CharacterPreview _preview = new();
        readonly List<SlotRow> _rows = new();
        Text _powerText;
        RectTransform _helmToggleRect;
        Text _helmToggleText;

        readonly List<RectTransform> _focusRects = new();

        // The button on the ordinary sheet that opens the reorder window, and the window's own
        // panel. Built once (only when ReorderAvailable), toggled by SetActive rather than
        // rebuilt.
        RectTransform _reorderButtonRect;
        Text _reorderButtonText;
        RectTransform _reorderPanel;

        class SlotRow
        {
            public GearSlot Slot;
            public RectTransform Rect;
            public Text Name, Item;
            public Image Swatch;
            public RectTransform Stars;
        }

        public void Init(System.Func<CharacterProfile> profile, System.Action toggleHelm, System.Func<Exchange.RunModifiers> mods = null,
                         System.Func<Player.PlayerController> player = null,
                         System.Func<bool> reorderOpen = null,
                         System.Func<(StatPercents Stats, float MaxHp)> character = null)
        {
            _character = character;
            _profile = profile;
            _toggleHelm = toggleHelm;
            _mods = mods;
            _player = player;
            _reorderOpen = reorderOpen;
        }

        // ------------------------------------------------------------------ open / close

        public void Toggle(Transform canvas, ElementType element)
        {
            if (IsOpen) Close();
            else Open(canvas, element);
        }

        public void Open(Transform canvas, ElementType element, System.Action onClosed = null)
        {
            if (IsOpen) return;
            _element = element;
            _onClosed = onClosed;
            IsOpen = true;
            Locked = _mods?.Invoke() != null;
            ReorderAvailable = Locked && (_reorderOpen?.Invoke() ?? false);
            ReorderPanelOpen = false;
            _reorderOnly = false;
            _picked = -1;
            _inspected = null;
            _noteStat = null;

            // Never capture a paused scale: if Open() is reached while time is already stopped,
            // saving 0 means Close() restores the freeze and the game soft-locks with no UI up.
            Core.GamePause.Hold(this);

            BuildPreview();
            BuildUi(canvas);
            Refresh();
        }

        /// <summary>
        /// Straight into the reorder window - the floor reward's REORDER ARTS button. Does nothing
        /// (and calls <paramref name="onClosed"/> at once) outside the door window.
        /// </summary>
        public void OpenReorder(Transform canvas, ElementType element, System.Action onClosed)
        {
            if (IsOpen) return;
            Open(canvas, element, onClosed);
            if (!ReorderAvailable) { Close(); return; }
            _reorderOnly = true;
            OpenReorderPanel();
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Core.GamePause.Release(this);

            if (_root) Destroy(_root);
            _preview.Dispose();
            _rows.Clear();
            _helmToggleRect = null;
            _helmToggleText = null;
            _statsPanel = null;
            _statTiles.Clear();
            _swap.Clear();
            _origin.Clear();
            _swapCards = _undoRect = _doneRect = null;
            _swapStatus = _undoText = _doneText = null;
            _picked = -1;
            _reorderButtonRect = null;
            _reorderButtonText = null;
            _reorderPanel = null;
            ReorderPanelOpen = false;
            _reorderOnly = false;

            var cb = _onClosed;
            _onClosed = null;
            cb?.Invoke();
        }

        // ------------------------------------------------------------------ preview

        void BuildPreview() => _preview.Build(_element);

        // ------------------------------------------------------------------ ui

        void BuildUi(Transform canvas)
        {
            _root = new GameObject("CharacterScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.035f, 0.05f, 1f));

            var title = UiKit.Rect(full, "title", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -110), new Vector2(0, -46));
            UiKit.Label(title, "LOADOUT", 44, new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleCenter);

            var sub = UiKit.Rect(full, "sub", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -146), new Vector2(0, -110));
            // Says where gear DOES change, so inspect-only reads as a rule rather than a fault.
            string why = Locked ? "loadout locked for this run" : "change gear at the circle";
            UiKit.Hint(sub,
                "click a slot for its stats, again for yours    -    " + why + "    -    [C] to close",
                "tap a slot for its stats, again for yours    -    " + why + "    -    BACK to close",
                18, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter,
                UI.GamepadGlyphs.Navigate + " to a slot, " + UI.GamepadGlyphs.Confirm + " for its stats    -    "
                + why + "    -    " + UI.GamepadGlyphs.Loadout + " to close");

            // Character preview, left.
            var frame = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                // Taller than the grid beside it. This panel is the showcase - it is where a
                // cosmetic worth real money is actually looked at, and it zooms - so it gets the
                // vertical space the cards do not need. It had been cut to 390 to make room for a
                // finger-sized helm toggle beneath it; the toggle kept its size and this took the
                // rest of the column instead.
                new Vector2(-900, -170), new Vector2(-540, 340), new Color(0.07f, 0.08f, 0.11f, 1f));
            var imgGo = new GameObject("preview", typeof(RawImage));
            imgGo.transform.SetParent(frame, false);
            var irt = (RectTransform)imgGo.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(8, 8); irt.offsetMax = new Vector2(-8, -8);
            // Sized to the pixels it is actually drawn at, NOT to a constant - see
            // CharacterPreview.MatchTo. Done here because only the layout knows the size.
            _canvas = full.GetComponentInParent<Canvas>();
            _previewRect = irt;
            _preview.MatchTo(imgGo.GetComponent<RawImage>(), _canvas);

            // Slot cards, right - THREE columns of five rather than two of seven.
            //
            // The cards were 288 x 66, and 66 units is about 23pt on a phone: half a finger. They
            // are UiKit.TouchTarget tall now, and thirteen of those in two columns would be 952
            // units deep against the 520 the screen has for them - so the grid turned a column
            // instead. There is room: the old note here said a third column at x+600 runs off the
            // panel, but the cards hang off `full` rather than off the preview panel, and column
            // three ends at x 658 against a 960 half-width.
            var slots = GearSlots.Selectable;
            const int cols = 3;
            const float cardW = 288f, cardH = UiKit.TouchTarget, stepY = cardH + 6f, top = 330f;
            int perCol = Mathf.CeilToInt(slots.Length / (float)cols);
            for (int i = 0; i < slots.Length; i++)
            {
                int col = i / perCol, row = i % perCol;
                float x = -510 + col * 300;
                float y = top - row * stepY;

                var cardColor = new Color(0.09f, 0.10f, 0.14f, 1f);
                var card = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, y - cardH), new Vector2(x + cardW, y), cardColor);

                var swatchRt = UiKit.Rect(card, "swatch", new Vector2(0, 0), new Vector2(0, 1),
                    new Vector2(0, 0), new Vector2(6, 0));
                var swatch = swatchRt.gameObject.AddComponent<Image>();

                var nameRt = UiKit.Rect(card, "slot", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(18, -46), new Vector2(-12, -14));
                var nameTxt = UiKit.Label(nameRt, slots[i].ToString().ToUpper(), 17,
                    new Color(0.5f, 0.53f, 0.6f));

                var itemRt = UiKit.Rect(card, "item", new Vector2(0, 0), new Vector2(1, 1),
                    new Vector2(18, 14), new Vector2(-12, -46));
                var itemTxt = UiKit.Label(itemRt, "-", 22, new Color(0.85f, 0.87f, 0.92f));

                // Top-right, beside the slot name - filled per item in Refresh.
                var starsRt = UiKit.Rect(card, "stars", new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-92, -42), new Vector2(-12, -16));

                _rows.Add(new SlotRow { Slot = slots[i], Rect = card, Name = nameTxt, Item = itemTxt,
                                        Swatch = swatch, Stars = starsRt });
            }

            // Above the grid now - the bottom row of cards took the space it used to sit in.
            var power = UiKit.Rect(full, "power", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-510, 342), new Vector2(378, 392));
            _powerText = UiKit.Label(power, "", 24, new Color(0.85f, 0.87f, 0.92f), TextAnchor.MiddleLeft);

            // A preference, not a slot, so it sits under the preview rather than in the grid -
            // the twelve cards are things you EQUIP, this is something you just LOOK AT.
            _helmToggleRect = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-900, -310), new Vector2(-540, -310 + UiKit.TouchTarget),
                new Color(0.09f, 0.10f, 0.14f, 1f));
            _helmToggleText = UiKit.Label(_helmToggleRect, "", 18,
                new Color(0.85f, 0.87f, 0.92f), TextAnchor.MiddleCenter);

            // Only exists at all while the door window is open - the crack in Locked is still
            // scoped to that window, this just stops the sheet from jumping straight into it.
            // A button rather than anything automatic: pressing it OPENS the reorder window,
            // which itself opens inspect-only - see BuildReorderOverlay.
            //
            // Top right, beside the title (2026-11-12: under the helmet toggle it was buried). Shown
            // all run long - dimmed and inert outside the door window, saying when it opens - so
            // a player learns it exists before the one moment it works.
            if (ReorderAvailable)
            {
                _reorderButtonRect = UiKit.Panel(full, new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-440, -46 - UiKit.TouchTarget), new Vector2(-60, -46),
                    new Color(0.22f, 0.18f, 0.08f, 1f));
                _reorderButtonText = UiKit.Label(_reorderButtonRect, "", 20,
                    new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleCenter);
                _reorderButtonText.fontStyle = FontStyle.Bold;
            }
            else if (Locked)
            {
                var idle = UiKit.Panel(full, new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-440, -46 - UiKit.TouchTarget), new Vector2(-60, -46),
                    new Color(0.07f, 0.075f, 0.09f, 1f));
                UiKit.Label(idle, "REORDER WEAPON ARTS\nafter a floor's reward", 15,
                    new Color(0.42f, 0.44f, 0.5f), TextAnchor.MiddleCenter);
            }

            // The stats panel, right of the slots: the character by default, a pressed slot's piece
            // while one is pressed (the user's call, 2026-11-12). Down to the helmet toggle's foot.
            // Filled by BuildStats (CharacterScreen.Stats.cs) on every Refresh.
            _statsPanel = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(408, -310), new Vector2(900, 330), new Color(0.07f, 0.075f, 0.1f, 1f));

            BuildLedger(full);

            // The reorder window (CharacterScreen.Reorder.cs) takes the whole screen over the sheet.
            // Built now but INACTIVE - toggled by the button above rather than rebuilt each time.
            if (ReorderAvailable)
            {
                BuildReorderOverlay(full);
                _reorderPanel.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// What the run is carrying, along the bottom. Same strip as the exchange row, with names
        /// - this is the screen you come to in order to actually read the ledger, rather than to
        /// glance at it mid-decision.
        /// </summary>
        void BuildLedger(RectTransform full)
        {
            var mods = _mods?.Invoke();
            if (mods == null || mods.Held.Count == 0) return;

            UiKit.Label(UiKit.Rect(full, "ledger-l", new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(60, 132), new Vector2(420, 158)),
                "EQUIVALENT EXCHANGE", 14, new Color(0.86f, 0.72f, 0.38f));

            var row = UiKit.Rect(full, "ledger", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(60, 44), new Vector2(-60, 124));
            LedgerStrip.Build(row, mods, withNames: true, size: 52f, gap: 12f);
        }

        // ------------------------------------------------------------------ state

        public void Refresh()
        {
            if (!IsOpen || _profile == null) return;
            var loadout = _profile().Gear;

            foreach (var row in _rows)
            {
                var item = GearCatalog.Get(loadout.Get(row.Slot));
                // TORSO carries the equipped ability too, or the sheet is the second place (after
                // the gear picker) that shows a cuirass's tier and power while staying silent
                // about the one thing that changes how the character plays.
                string abilitySuffix = item != null && row.Slot == GearSlot.Torso
                    ? "  — " + item.DefensiveAbility.Label() : "";
                row.Item.text = item != null ? item.DisplayName + abilitySuffix : "- empty -";
                row.Item.color = item != null
                    ? TierColor(item.Tier)
                    : new Color(0.35f, 0.37f, 0.43f);
                row.Swatch.color = item != null ? TierColor(item.Tier) : new Color(0.18f, 0.19f, 0.24f);

                // The pressed slot reads as pressed: the panel beside it is about this card.
                row.Rect.GetComponent<Image>().color = _inspected == row.Slot
                    ? new Color(0.16f, 0.15f, 0.10f, 1f) : new Color(0.09f, 0.10f, 0.14f, 1f);

                if (row.Stars != null)
                {
                    foreach (Transform child in row.Stars) Destroy(child.gameObject);
                    if (item != null && item.Stars >= 0)
                        UiKit.Stars(row.Stars, item.Stars, GearRoller.MaxLevel, 22f,
                                    new Color(1f, 0.82f, 0.3f), new Color(1f, 1f, 1f, 0.12f));
                }
            }

            _preview.Refresh(_profile());
            bool helmHidden = _profile().HelmHidden;
            if (_powerText)
                _powerText.text = $"GEAR POWER  {loadout.TotalPower():0}        "
                                + $"{loadout.WornCount()} / {GearSlots.Worn.Length} slots filled";
            if (_helmToggleText)
                _helmToggleText.text = helmHidden ? "HELMET: HIDDEN (click to show)" : "HELMET: SHOWN (click to hide)";

            if (_reorderButtonText) _reorderButtonText.text = "REORDER WEAPON ARTS";
            BuildStats();
        }

        public static Color TierColor(LootTier tier) => tier switch
        {
            LootTier.Bronze       => new Color(0.72f, 0.45f, 0.20f),
            LootTier.Silver       => new Color(0.78f, 0.80f, 0.85f),
            LootTier.Gold         => new Color(0.95f, 0.78f, 0.32f),
            LootTier.Diamond      => new Color(0.55f, 0.85f, 1.00f),
            _                     => new Color(0.75f, 0.45f, 1.00f),
        };

        /// <summary>
        /// The preview's own gesture: scroll or pinch to zoom, drag to pan, and a tap on it to
        /// go back to the whole character.
        ///
        /// Confined to the preview rect, so the loadout grid beside it still commits on press
        /// like every other screen in the game - only the panel that actually moves needs the
        /// tap-versus-drag test.
        /// </summary>
        bool PreviewGesture()
        {
            if (_previewRect == null) return false;

            var at = Core.Controls.PointerPosition;
            bool over = RectTransformUtility.RectangleContainsScreenPoint(_previewRect, at, null);

            float notches = Core.Controls.Zoom;
            if (over && Mathf.Abs(notches) > 0.001f) _preview.Zoom(notches, Normalised(at));

            if (Core.Controls.Pinching) _previewDrag = false;
            else if (over && Core.Controls.Tapped(out var down)) { _previewDrag = true; _dragFrom = down; _dragMoved = false; }
            if (!Core.Controls.PointerHeld(out _)) _previewDrag = false;

            if (_previewDrag)
            {
                var d = Core.Controls.PointerDelta;
                if (d.sqrMagnitude > 0.01f)
                {
                    _dragMoved |= (at - _dragFrom).sqrMagnitude > TapSlop;
                    float scale = _canvas != null ? _canvas.scaleFactor : 1f;
                    _preview.PanBy(d * scale);
                }
            }

            // A tap on the preview that did not turn into a drag resets the view. It is the only
            // way back out on a touch device, and it is where a player's thumb already is.
            if (over && Core.Controls.PointerReleased && !_dragMoved
                && (at - _dragFrom).sqrMagnitude <= TapSlop)
            {
                _preview.ResetView();
                return true;
            }
            return over;
        }

        Vector2 Normalised(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_previewRect, screenPoint, null, out var local);
            var r = _previewRect.rect;
            return new Vector2(Mathf.Clamp01((local.x - r.xMin) / Mathf.Max(1f, r.width)),
                               Mathf.Clamp01((local.y - r.yMin) / Mathf.Max(1f, r.height)));
        }

        RectTransform _previewRect;
        Canvas _canvas;
        bool _previewDrag, _dragMoved;
        Vector2 _dragFrom;
        const float TapSlop = 400f;

        void Update()
        {
            if (!IsOpen) return;

            // The reorder window covers the whole sheet and owns all input while it is up
            // (CharacterScreen.Reorder.cs) - BACK included, which the bootstrap leaves to it.
            _focusRects.Clear();
            if (ReorderPanelOpen)
            {
                RegisterReorderFocus();
                Core.Controls.SetFocusCandidates(_focusRects);
                HandleReorderInput();
                return;
            }

            // D-pad navigation - registered BEFORE the preview's gesture, which returns early.
            // Slots first: they are what a gamepad lands on as the screen opens.
            foreach (var r in _rows) _focusRects.Add(r.Rect);
            foreach (var t in _statTiles) _focusRects.Add(t.Rect);
            if (_helmToggleRect != null) _focusRects.Add(_helmToggleRect);
            if (_reorderButtonRect != null) _focusRects.Add(_reorderButtonRect);
            Core.Controls.SetFocusCandidates(_focusRects);

            // The footnote follows the pointer or the pad across the tiles.
            UpdateFootnote();

            // The preview swallows its own gestures, so a drag across it never also cycles a slot.
            if (PreviewGesture()) return;

            if (!Core.Controls.Tapped(out var p)) return;

            if (_helmToggleRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_helmToggleRect, p, null))
            {
                _toggleHelm?.Invoke();
                return;
            }

            if (_reorderButtonRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_reorderButtonRect, p, null))
            {
                OpenReorderPanel();
                return;
            }

            if (TapStats(p)) return;

            foreach (var row in _rows)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(row.Rect, p, null)) continue;
                _inspected = _inspected == row.Slot ? null : row.Slot;
                _noteStat = null;   // the footnote opens on the new read's own first stat
                Refresh();
                return;
            }
        }
    }
}
