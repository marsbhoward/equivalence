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
    /// The loadout screen. Opens with C or Tab, pauses the game, and shows the character large
    /// alongside all twelve slots.
    ///
    /// This exists because the arena camera can never show gear properly - even tightened, a
    /// character is ~12% of screen height, which is fine for combat readability but nowhere near
    /// enough to appreciate a cosmetic worth real money. The collectible tiers need somewhere
    /// they are actually looked at.
    ///
    /// The preview is a second rig built far from the arena and filmed by its own camera into a
    /// RenderTexture, so it can be posed and lit independently of gameplay.
    /// </summary>
    public class CharacterScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        /// <summary>
        /// True while a run is live - every piece of gear is locked for its duration, so this
        /// screen becomes inspection only. Decided once in Open() rather than read live: a run
        /// cannot start or end while this screen is open, so it cannot change mid-view, and
        /// deciding it once means the hint text and the cards' own dimming can be built without
        /// re-deriving it everywhere they are touched.
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
        /// The reorder window is open, but that is still inspect-only - see
        /// <see cref="Rearranging"/>. Reset on every Open()/Close(), never read outside this
        /// screen's own session.
        /// </summary>
        bool ReorderPanelOpen;

        /// <summary>
        /// True once the player has pressed the panel's own "REARRANGE" button. Below this, the
        /// panel shows the wheel but taps on its cards and the slot-3 toggle do nothing - an
        /// inspect mode for a screen whose only other state used to be "already mutating your
        /// wheel the instant it opened." Reset alongside ReorderPanelOpen.
        /// </summary>
        bool Rearranging;

        System.Func<CharacterProfile> _profile;

        /// <summary>
        /// The run's ledger, or null outside a run. The sheet is reachable from the hub as well
        /// as mid-run, and between runs there is genuinely nothing to show.
        /// </summary>
        System.Func<Exchange.RunModifiers> _mods;
        System.Action<GearSlot, string> _equip;
        System.Action _toggleHelm;
        System.Func<Player.PlayerController> _player;
        System.Func<bool> _reorderOpen;
        ElementType _element;

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

        readonly List<FinisherCard> _finisherCards = new();
        int _selectedFinisher = -1;
        RectTransform _slot3ToggleRect;
        Text _slot3ToggleText;
        readonly List<RectTransform> _focusRects = new();

        // The button on the ordinary sheet that opens the reorder window, and the window's own
        // panel plus its "start rearranging" toggle. Built once (only when ReorderAvailable),
        // toggled by SetActive rather than rebuilt - the same lazy-but-kept shape _picker uses.
        RectTransform _reorderButtonRect;
        Text _reorderButtonText;
        RectTransform _reorderPanel;
        RectTransform _rearrangeButtonRect;
        Text _rearrangeButtonText;

        class SlotRow
        {
            public GearSlot Slot;
            public RectTransform Rect;
            public Text Name, Item;
            public Image Swatch;
            public RectTransform Stars;
        }

        class FinisherCard
        {
            public int Index;
            public RectTransform Rect;
            public Image Bg;
            public Text Name;
        }

        public void Init(System.Func<CharacterProfile> profile, System.Action<GearSlot, string> equip,
                         System.Action toggleHelm, System.Func<Exchange.RunModifiers> mods = null,
                         System.Func<Player.PlayerController> player = null,
                         System.Func<bool> reorderOpen = null)
        {
            _profile = profile;
            _equip = equip;
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
            Rearranging = false;
            _selectedFinisher = -1;

            // Never capture a paused scale: if Open() is reached while time is already stopped,
            // saving 0 means Close() restores the freeze and the game soft-locks with no UI up.
            Core.GamePause.Hold(this);

            BuildPreview();
            BuildUi(canvas);
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            // The grid is a child of this screen, not a peer: closing the loadout while it is up
            // would otherwise leave it orphaned over the game with nothing owning its input.
            if (_picker != null) _picker.Close();
            Core.GamePause.Release(this);

            if (_root) Destroy(_root);
            _preview.Dispose();
            _rows.Clear();
            _helmToggleRect = null;
            _helmToggleText = null;
            _finisherCards.Clear();
            _slot3ToggleRect = null;
            _slot3ToggleText = null;
            _reorderButtonRect = null;
            _reorderButtonText = null;
            _reorderPanel = null;
            _rearrangeButtonRect = null;
            _rearrangeButtonText = null;
            ReorderPanelOpen = false;
            Rearranging = false;

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
            if (Locked)
            {
                UiKit.Hint(sub,
                    "loadout locked for this run - inspect only    -    [C] or [TAB] to close",
                    "loadout locked for this run - inspect only    -    BACK to close",
                    18, new Color(0.6f, 0.55f, 0.4f), TextAnchor.MiddleCenter,
                    "loadout locked for this run - inspect only    -    " + UI.GamepadGlyphs.Loadout + " to close");
            }
            else
            {
                UiKit.Hint(sub,
                    "click a slot to choose    -    [C] or [TAB] to close    -    game is paused",
                    "tap a slot to choose    -    BACK to close    -    game is paused",
                    18, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter,
                    UI.GamepadGlyphs.Navigate + " to a slot, " + UI.GamepadGlyphs.Confirm + " to choose    -    "
                    + UI.GamepadGlyphs.Loadout + " to close    -    game is paused");
            }

            // Character preview, left.
            var frame = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                // Taller than the grid beside it. This panel is the showcase - it is where a
                // cosmetic worth real money is actually looked at, and it zooms - so it gets the
                // vertical space the cards do not need. It had been cut to 390 to make room for a
                // finger-sized helm toggle beneath it; the toggle kept its size and this took the
                // rest of the column instead.
                new Vector2(-620, -170), new Vector2(-260, 340), new Color(0.07f, 0.08f, 0.11f, 1f));
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
                float x = -230 + col * 300;
                float y = top - row * stepY;

                // Flattened toward the panel's own background when locked, so a card that no
                // longer responds to a tap does not still look like a button inviting one.
                var cardColor = Locked ? new Color(0.055f, 0.06f, 0.08f, 1f) : new Color(0.09f, 0.10f, 0.14f, 1f);
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
                new Vector2(-230, 342), new Vector2(358, 392));
            _powerText = UiKit.Label(power, "", 24, new Color(0.85f, 0.87f, 0.92f), TextAnchor.MiddleLeft);

            // A preference, not a slot, so it sits under the preview rather than in the grid -
            // the twelve cards are things you EQUIP, this is something you just LOOK AT.
            _helmToggleRect = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-620, -310), new Vector2(-260, -310 + UiKit.TouchTarget),
                new Color(0.09f, 0.10f, 0.14f, 1f));
            _helmToggleText = UiKit.Label(_helmToggleRect, "", 18,
                new Color(0.85f, 0.87f, 0.92f), TextAnchor.MiddleCenter);

            // Only exists at all while the door window is open - the crack in Locked is still
            // scoped to that window, this just stops the sheet from jumping straight into it.
            // A button rather than anything automatic: pressing it OPENS the reorder window,
            // which itself opens inspect-only - see BuildReorderOverlay.
            if (ReorderAvailable)
            {
                _reorderButtonRect = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-620, -310 - 8 - UiKit.TouchTarget), new Vector2(-260, -310 - 8),
                    new Color(0.14f, 0.13f, 0.09f, 1f));
                _reorderButtonText = UiKit.Label(_reorderButtonRect, "", 18,
                    new Color(0.95f, 0.78f, 0.32f), TextAnchor.MiddleCenter);
            }

            BuildLedger(full);

            // Covers the (already non-interactive) gear grid entirely rather than sharing the
            // screen with it - the two coordinate systems the grid and the preview panel already
            // use leave no clean gap to fit a new row into, and reordering is rare and temporary
            // enough that a full-screen takeover for its one window is the honest shape for it.
            //
            // Built now but INACTIVE - toggled by the button above rather than rebuilt each time,
            // the same lazy-but-kept shape _picker uses for the gear grid.
            if (ReorderAvailable)
            {
                BuildReorderOverlay(full);
                _reorderPanel.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// The one exception to Locked: reordering the finisher wheel, open only for the walk to
        /// the door after a floor's reward. Opens INSPECT ONLY - the wheel is shown, but the cards
        /// and the slot 3 toggle do nothing until the player presses REARRANGE. Once rearranging,
        /// tap one card then another to swap them; a locked slot (a signature, weapon or relic)
        /// cannot be picked at all. The slot 3 toggle is the only other lever here - dropping it
        /// takes the wheel to its two-finisher minimum, restoring it puts the default back.
        /// </summary>
        void BuildReorderOverlay(RectTransform full)
        {
            _reorderPanel = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-520, -300), new Vector2(520, 300), new Color(0.05f, 0.055f, 0.075f, 0.97f));
            var panel = _reorderPanel;

            UiKit.Label(UiKit.Rect(panel, "reorder-title", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -56), new Vector2(0, -12)),
                "WEAPON ART WHEEL", 30, new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleCenter);

            // The button that flips Rearranging on and off - the whole point of this pass. It
            // reads TEXT from RefreshReorderOverlay rather than here, so it can say which state
            // it is currently in and what pressing it does next.
            _rearrangeButtonRect = UiKit.Panel(panel, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-232, -58), new Vector2(-12, -14), new Color(0.14f, 0.13f, 0.09f, 1f));
            _rearrangeButtonText = UiKit.Label(_rearrangeButtonRect, "", 18,
                new Color(0.95f, 0.78f, 0.32f), TextAnchor.MiddleCenter);

            UiKit.Hint(UiKit.Rect(panel, "reorder-hint", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -88), new Vector2(0, -60)),
                "REARRANGE to enable swapping    -    inspecting only until then    -    " +
                "a locked slot cannot be moved",
                "REARRANGE to enable swapping    -    inspecting only until then    -    " +
                "a locked slot cannot be moved",
                16, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter,
                UI.GamepadGlyphs.Confirm + " REARRANGE to enable swapping    -    inspecting only " +
                "until then    -    a locked slot cannot be moved");

            var player = _player?.Invoke();
            int count = player != null ? player.Slots.Count : 0;
            const float cardW = 200f, cardH = UiKit.TouchTarget, gap = 20f;
            float totalW = count * cardW + Mathf.Max(0, count - 1) * gap;
            float startX = -totalW * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float x = startX + i * (cardW + gap);
                var cardRect = UiKit.Panel(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, -20 - cardH), new Vector2(x + cardW, -20), new Color(0.1f, 0.11f, 0.15f, 1f));
                var bg = cardRect.GetComponent<Image>();

                var nameTxt = UiKit.Label(UiKit.Rect(cardRect, "name", Vector2.zero, Vector2.one,
                    new Vector2(8, 8), new Vector2(-8, -8)),
                    "", 17, new Color(0.9f, 0.92f, 0.96f), TextAnchor.MiddleCenter);

                _finisherCards.Add(new FinisherCard { Index = i, Rect = cardRect, Bg = bg, Name = nameTxt });
            }

            const int thirdSlot = Player.PlayerController.BaseSlots - 1;
            bool thirdLocked = player == null || player.SlotLocked(thirdSlot);
            _slot3ToggleRect = UiKit.Panel(panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-160, 24), new Vector2(160, 24 + UiKit.TouchTarget),
                thirdLocked ? new Color(0.07f, 0.075f, 0.09f, 1f) : new Color(0.14f, 0.13f, 0.09f, 1f));
            _slot3ToggleText = UiKit.Label(_slot3ToggleRect, "", 18,
                thirdLocked ? new Color(0.4f, 0.42f, 0.48f) : new Color(0.95f, 0.78f, 0.32f),
                TextAnchor.MiddleCenter);
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

            if (_reorderButtonText)
                _reorderButtonText.text = ReorderPanelOpen ? "CLOSE REORDER WINDOW" : "REORDER WEAPON ARTS";

            if (ReorderAvailable) RefreshReorderOverlay();
        }

        void RefreshReorderOverlay()
        {
            var player = _player?.Invoke();
            if (player == null) return;

            if (_rearrangeButtonText)
                _rearrangeButtonText.text = Rearranging ? "DONE REARRANGING" : "REARRANGE";

            foreach (var card in _finisherCards)
            {
                bool inRange = card.Index < player.Slots.Count;
                card.Rect.gameObject.SetActive(inRange);
                if (!inRange) continue;

                // "Locked" here only ever meant "a floor reward cannot overwrite this" - see
                // PlayerController.SlotLocked. It used to also mean "cannot be tapped in this
                // screen", which was a second, stricter rule stacked on top for no real reason:
                // moving a signature to a different spot in the wheel doesn't touch what it's
                // protected from, so a reward-locked slot is a full participant in a swap now.
                // SwapSlots carries the lock flag along with the moveset, so the protection
                // follows whichever slot ends up holding the signature.
                bool locked = player.SlotLocked(card.Index);
                bool selected = card.Index == _selectedFinisher;
                card.Name.text = (player.Slots[card.Index]?.DisplayName ?? "-")
                                + (locked ? "\n(reward-locked)" : "");
                card.Name.color = new Color(0.9f, 0.92f, 0.96f);
                card.Bg.color = selected ? new Color(0.22f, 0.28f, 0.34f, 1f)
                              : locked   ? new Color(0.16f, 0.14f, 0.08f, 1f)
                                         : new Color(0.1f, 0.11f, 0.15f, 1f);
            }

            const int thirdSlot = Player.PlayerController.BaseSlots - 1;
            bool removed = thirdSlot >= player.Slots.Count;
            bool thirdLocked = !removed && player.SlotLocked(thirdSlot);
            if (_slot3ToggleText)
            {
                _slot3ToggleText.text = thirdLocked ? "SLOT 3 IS LOCKED"
                                      : removed      ? "RESTORE SLOT 3"
                                                     : "DROP SLOT 3 (2 weapon arts)";
            }
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

            // The slot grid covers this screen and handles its own taps. Nothing in this project
            // consumes input - every screen hit-tests the pointer itself - so without standing
            // down here a tap on a picker card would ALSO hit the slot row behind it.
            if (PickerOpen) return;

            // D-pad navigation - registered BEFORE the preview's gesture, which returns early. The
            // reorder panel's cards replace the gear grid entirely while it's open, the same
            // "covers the grid, handled completely separately" split the tap handling below
            // already uses. Its own cards only become real targets once Rearranging is on; before
            // that the panel has the REARRANGE button and the button that closes it again.
            _focusRects.Clear();
            if (ReorderPanelOpen)
            {
                if (_rearrangeButtonRect != null) _focusRects.Add(_rearrangeButtonRect);
                if (Rearranging)
                {
                    if (_slot3ToggleRect != null) _focusRects.Add(_slot3ToggleRect);
                    foreach (var c in _finisherCards) _focusRects.Add(c.Rect);
                }
                if (_reorderButtonRect != null) _focusRects.Add(_reorderButtonRect);
            }
            else
            {
                // Slots first: they are what a gamepad lands on as the screen opens.
                if (!Locked) foreach (var r in _rows) _focusRects.Add(r.Rect);
                if (_helmToggleRect != null) _focusRects.Add(_helmToggleRect);
                if (_reorderButtonRect != null) _focusRects.Add(_reorderButtonRect);
            }
            Core.Controls.SetFocusCandidates(_focusRects);

            // The preview swallows its own gestures, so a drag across it never also cycles a slot.
            if (PreviewGesture()) return;


            if (!Core.Controls.Tapped(out var p)) return;

            // The panel covers the grid entirely, so its own taps are handled completely
            // separately rather than falling through to the (locked, inert) cards underneath -
            // including the reorder button itself, which stays live behind the panel only so a
            // second press can close it again.
            if (ReorderPanelOpen) { HandleReorderTap(p); return; }

            if (_helmToggleRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_helmToggleRect, p, null))
            {
                _toggleHelm?.Invoke();
                return;
            }

            if (_reorderButtonRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_reorderButtonRect, p, null))
            {
                ReorderPanelOpen = true;
                if (_reorderPanel != null) _reorderPanel.gameObject.SetActive(true);
                Refresh();
                return;
            }

            // Every other tap target above this stays live (the helmet preference is not gear -
            // it changes nothing about how the character performs, only whether the sprite
            // renders) - only the slots themselves stop responding.
            if (Locked) return;

            foreach (var row in _rows)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(row.Rect, p, null)) continue;
                OpenPicker(row.Slot);
                return;
            }
        }

        void HandleReorderTap(Vector2 p)
        {
            var player = _player?.Invoke();
            if (player == null) return;

            if (_rearrangeButtonRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_rearrangeButtonRect, p, null))
            {
                Rearranging = !Rearranging;
                _selectedFinisher = -1;
                Refresh();
                return;
            }

            // Closing from inside the panel - tapping the (now-relabelled) button that opened it,
            // still visible below the panel's own bottom edge.
            if (_reorderButtonRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_reorderButtonRect, p, null))
            {
                ReorderPanelOpen = false;
                Rearranging = false;
                _selectedFinisher = -1;
                if (_reorderPanel != null) _reorderPanel.gameObject.SetActive(false);
                Refresh();
                return;
            }

            // Everything below here MUTATES the wheel, so none of it responds until the player
            // has explicitly pressed REARRANGE - opening the panel is inspect-only by default.
            if (!Rearranging) return;

            if (_slot3ToggleRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_slot3ToggleRect, p, null))
            {
                const int thirdSlot = Player.PlayerController.BaseSlots - 1;
                if (thirdSlot >= player.Slots.Count) player.RestoreThirdSlot();
                else if (!player.SlotLocked(thirdSlot)) player.RemoveThirdSlot();
                _selectedFinisher = -1;
                Refresh();
                return;
            }

            foreach (var card in _finisherCards)
            {
                if (card.Index >= player.Slots.Count) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(card.Rect, p, null)) continue;

                // A reward-locked slot can be picked up and swapped like any other now - see
                // RefreshReorderOverlay's own note. SwapSlots carries the lock flag with the
                // moveset, so the protection follows it to wherever it ends up.
                if (_selectedFinisher < 0) _selectedFinisher = card.Index;
                else if (_selectedFinisher == card.Index) _selectedFinisher = -1;   // tapped itself - deselect
                else
                {
                    player.SwapSlots(_selectedFinisher, card.Index);
                    _selectedFinisher = -1;
                }
                Refresh();
                return;
            }
        }

        /// <summary>Step through everything that fits this slot, plus an empty state.</summary>
        /// <summary>The per-slot grid. Created on demand and kept, so reopening a slot does not
        /// churn a component every press.</summary>
        GearPicker _picker;

        /// <summary>True while the slot grid is up. CharacterScreen stands down entirely then -
        /// nothing in this project consumes input, so without this a tap on a picker card would
        /// also land on the slot row underneath it.</summary>
        public bool PickerOpen => _picker != null && _picker.IsOpen;

        /// <summary>
        /// Open the grid for one slot.
        ///
        /// This replaced a cycle - press the card, advance to the next valid item, wrap round to
        /// nothing - which was fine at three weapons and unusable past thirty pieces: no way to
        /// see what was coming, no way to step back, and one press per item to reach the last.
        /// See GearPicker.
        /// </summary>
        void OpenPicker(GearSlot slot)
        {
            if (_canvas == null || PickerOpen) return;

            // The relic socket lists by what is HELD, not by slot: a signature relic only fits
            // beside a weapon of its own class. GearPicker owns that split.
            var held = GearCatalog.Get(_profile().Gear.Get(GearSlot.Weapon));

            _picker ??= gameObject.AddComponent<GearPicker>();

            // The wear lookup is only ever used to choose WITHIN a stack of identical pieces -
            // the freshest copy, rather than whichever the catalogue listed first. It is not part
            // of what makes two pieces stack; see GearPicker's own note.
            _picker.Open(_canvas.transform, slot, held, _profile().Gear.Get(slot),
                         id => _equip(slot, id),
                         id => _profile().Wear.Get(id));
        }
    }
}
