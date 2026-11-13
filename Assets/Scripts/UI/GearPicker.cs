using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Art.Gear;
using Convergence.Core;
using Convergence.Hub;

namespace Convergence.UI
{
    /// <summary>
    /// Choose what goes in ONE loadout slot: a grid of every valid piece, opened by pressing that
    /// slot's card on the character screen.
    ///
    /// IT REPLACES A CYCLE, and the cycle is what made it necessary. Pressing a slot used to
    /// advance to the next valid item and wrap round to nothing, which is fine at three weapons
    /// and useless at ten - the player cannot see what is coming, cannot go back a step, and pays
    /// one press per item to reach the last one. With the catalogue past thirty pieces that is the
    /// single worst interaction in the game.
    ///
    /// A GRID OF PICTURES, not a list of names, for the reason ShowcasePicker and GearDisplayScreen
    /// both already settled on: the thing being chosen IS a picture, and a player hunting for "the
    /// gold one" should not have to read. The name, tier swatch and power sit under the art for
    /// when they do.
    ///
    /// THE WEAPON SLOT IS SPLIT BY CLASS, and that is not just tidying. A weapon class decides how
    /// the thing is FOUGHT WITH - the grip, the finisher pool, whether basics throw - so swords,
    /// discs and bows are not eighteen interchangeable pictures, they are three different ways to
    /// play with a handful of options each. Showing all of them at once said the opposite. The tab
    /// opens on the class already being held, because that is the one the player is asking about.
    ///
    /// THE EMPTY CELL IS FIRST AND PRESENT on every tab, unless the caller says the slot may not be emptied
    /// (allowEmpty: a worn slot is swapped, never taken off - the user's call, 2026-11-12). Unequipping was the wrap-around
    /// step of the old cycle - you got there by passing every item - so it has to be somewhere
    /// explicit now. First rather than last because it is the one entry whose position never
    /// changes as the catalogue grows.
    ///
    /// IDENTICAL PIECES STACK, AND NOTHING ELSE DOES. Loot and the Forge mint one instance per
    /// drop, so a player who rolls six cuirasses gets six cards that all read "Gold Torso" - and
    /// they are not interchangeable, because each one carries its own stats and, on a torso, its
    /// own defensive ability. Collapsing them by NAME would hide a real choice behind a count
    /// badge. So a stack is keyed on every attribute an item actually has
    /// (MintedGearRecord.StackSignature): same slot, tier, name, all ten stat points, defensive
    /// ability and rolled finisher, or it is a separate card. A hand-authored catalogue piece
    /// carries no key at all and can never stack with anything - see GearItem.StackKey.
    ///
    /// THE TRANSMOG TAB ASKS A LOOSER VERSION OF THE SAME QUESTION (StackBy.Appearance): slot,
    /// tier and name, because a skin grants nothing and the stats, ability and finisher it would
    /// otherwise split on cannot reach the character. Same grid, same count badge - the only thing
    /// that changes is how much has to match, which is the honest difference between the two
    /// screens rather than two pickers with different rules bolted on.
    ///
    /// WHICH COPY A STACK HANDS YOU is decided here rather than left to list order: the one
    /// already worn if the stack holds it (so re-picking the worn card is a no-op instead of
    /// quietly swapping to a different instance with different wear), otherwise the LEAST WORN.
    /// Durability is deliberately not part of the key - it moves every fight, so keying on it
    /// would split a stack into one card per hit taken - which is exactly why the stack has to
    /// answer the question itself.
    ///
    /// It does NOT hold the pause itself: CharacterScreen is already holding it and is still open
    /// underneath. It does have to stop that screen acting while it is up, which CharacterScreen
    /// does by asking IsOpen - nothing here consumes input, the same as every other screen in this
    /// project (see the modal-input rules).
    /// </summary>
    public class GearPicker : MonoBehaviour
    {
        /// <summary>
        /// How much has to match before two pieces share a card.
        ///
        /// <see cref="Attributes"/> is the equip question: everything the item carries, because
        /// every one of those differences is something the player will feel. <see cref="Appearance"/>
        /// is the disguise question: slot, tier and name only, because a skin grants nothing and
        /// the rest of it cannot reach the character.
        /// </summary>
        public enum StackBy { Attributes, Appearance }

        public bool IsOpen { get; private set; }

        GameObject _root, _gridRoot;
        RectTransform _full;
        Text _countText;
        Action<string> _onPick;

        GearSlot _slot;
        string _equippedId;
        List<GearItem> _all = new();

        /// <summary>Current durability of one item id, or null when the caller has no wear to
        /// offer. Only ever used to choose BETWEEN members of one stack, never to build it.</summary>
        Func<string, float> _wear;

        StackBy _stackBy = StackBy.Attributes;

        /// <summary>What the first cell says. "empty" when the question is what to WEAR; the
        /// transmog tab passes "as equipped", because clearing a disguise is not emptying a slot -
        /// it is showing what is really there, and the two read completely differently.</summary>
        string _emptyLabel = "empty";

        /// <summary>Appended to the slot name in the header, or null. The disguise list has to
        /// say it is a disguise list: the same slot, asked a different question.</summary>
        string _titleSuffix;

        /// <summary>What the current entry is called on its card. "worn" for gear; the transmog
        /// tab says "shown", because a disguise is not being worn - the piece underneath it is.</summary>
        string _wornLabel = "worn";

        /// <summary>The piece the EMPTY cell stands for, drawn on it, or null. The perception
        /// tab hands over the worn piece: "as equipped" shows it, and it is not offered again as a
        /// perception of itself.</summary>
        GearItem _emptyItem;

        /// <summary>A card's full read for the details panel, or null for the name and tier alone.</summary>
        Func<GearItem, string> _describe;

        /// <summary>What the details panel's button says for a piece not yet chosen: "EQUIP" for gear,
        /// "SHOW" on the perception tab.</summary>
        string _pickLabel = "EQUIP";

        // ---- inspecting: a press SELECTS a card and reads it; only the panel's button chooses it
        // (the user's call, 2026-11-06 - a press that equipped gave no chance to read the piece first).
        int _selected = -1;
        RectTransform _detail, _actionRect;
        readonly List<GearItem> _cellItems = new();
        /// <summary>1 with the empty cell first, 0 without (allowEmpty).</summary>
        int _emptyCells = 1;

        /// <summary>
        /// One card: a single item, or several that are identical in every attribute.
        /// <see cref="PickId"/> is the instance actually equipped when the card is pressed.
        /// </summary>
        class Stack
        {
            public GearItem Item;
            public int Count;
            public string PickId;
            public bool Worn;
            public float BestDurability;
        }

        /// <summary>Which weapon class the grid is filtered to, or null when the slot is not the
        /// weapon slot and there is nothing to filter by.</summary>
        WeaponClass? _activeClass;

        readonly List<(RectTransform Rect, string ItemId)> _cells = new();
        readonly List<(RectTransform Rect, WeaponClass Class)> _tabs = new();
        readonly List<RectTransform> _focusRects = new();

        // ---- pages. Every card is built; only the current page's are active. A grid hung from the
        // header ran off the foot of the screen once a slot held more than three rows (2026-11-12).
        readonly List<int> _cardPage = new();
        int _pageIndex, _pageCount = 1;
        RectTransform _prevRect, _nextRect;
        Text _pageText;

        /// <summary>
        /// The frame this opened on. A modal opened by a press must ignore that press: Tapped
        /// stays true for the whole frame, so without this the click that opened the grid is also
        /// read by the grid and picks whatever happens to sit under the cursor. Same guard, same
        /// reason, as ConfirmDialog's own.
        /// </summary>
        int _openedFrame = -1;

        // ---- layout. 132 is the cell floor: UiKit.TouchTarget is 128 and a card may not go under
        // it whatever the item count does next. Tabs are 128 tall for the same rule - it is the
        // SMALLEST dimension of a target that counts.
        const int Cols = 8;
        // LabelH holds TWO lines of name: the names are wrapped to their own card, since a name
        // left to overflow ran into the next card's (2026-11-06).
        const float Cell = 132f, Gap = 20f, LabelH = 44f;
        // The details panel, to the right of the grid.
        const float DetailW = 440f, DetailGap = 32f;
        const float TabW = 260f, TabH = 128f, TabGap = 18f;

        /// <param name="options">The list to show, or null to ask the catalogue for the slot.
        /// Supplied by a caller that has already narrowed it - the transmog tab hands over a
        /// weapon list filtered to the held weapon's own class, since a greatsword may not be
        /// disguised as a bow and offering the choice at all would be the screen lying.</param>
        public void Open(Transform canvas, GearSlot slot, GearItem heldWeapon,
                         string equippedId, Action<string> onPick, Func<string, float> wear = null,
                         StackBy stackBy = StackBy.Attributes, List<GearItem> options = null,
                         string emptyLabel = "empty", string titleSuffix = null,
                         string wornLabel = "worn", GearItem emptyItem = null,
                         Func<GearItem, string> describe = null, string pickLabel = "EQUIP", bool allowEmpty = true)
        {
            if (IsOpen) return;
            _emptyCells = allowEmpty ? 1 : 0;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _onPick = onPick;
            _slot = slot;
            _equippedId = equippedId;
            _wear = wear;
            _stackBy = stackBy;
            _emptyLabel = string.IsNullOrEmpty(emptyLabel) ? "empty" : emptyLabel;
            _titleSuffix = titleSuffix;
            _wornLabel = string.IsNullOrEmpty(wornLabel) ? "worn" : wornLabel;
            _emptyItem = emptyItem;
            _describe = describe;
            _pickLabel = string.IsNullOrEmpty(pickLabel) ? "EQUIP" : pickLabel;
            _selected = -1;

            // The relic socket lists by what is HELD rather than by slot - a signature relic only
            // fits beside a weapon of its own class. Everything else is a plain slot lookup.
            _all = options ?? (slot == GearSlot.Relic
                ? GearOwnership.Relics(heldWeapon)
                : GearOwnership.Owned(slot));

            _root = new GameObject("GearPicker", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            _full = (RectTransform)_root.transform;
            _full.anchorMin = Vector2.zero; _full.anchorMax = Vector2.one;
            _full.offsetMin = Vector2.zero; _full.offsetMax = Vector2.zero;

            // FULLY opaque. The loadout screen is still live underneath and its own labels read
            // straight through even a 0.97 panel, which looks like a rendering fault.
            UiKit.Panel(_full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.032f, 0.045f, 1f));

            UiKit.Label(UiKit.Rect(_full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -104), new Vector2(0, -46)),
                slot.Label().ToUpperInvariant()
                    + (string.IsNullOrEmpty(_titleSuffix) ? "" : "   -   " + _titleSuffix), 40,
                new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleCenter);

            // The count is a PLAIN label, updated as tabs change. The back instruction beside it
            // is a UiKit.Hint and is left alone: Hint installs a HintSwap that rewrites its own
            // text whenever the input device changes, so anything overwriting that string per
            // frame would be fighting it.
            _countText = UiKit.Label(UiKit.Rect(_full, "c", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -134), new Vector2(0, -104)),
                "", 18, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter);

            UiKit.Hint(UiKit.Rect(_full, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -166), new Vector2(0, -136)),
                "[ESC] to go back", "BACK to go back",
                17, new Color(0.42f, 0.45f, 0.52f), TextAnchor.MiddleCenter,
                GamepadGlyphs.Cancel + " to go back");

            // Only the weapon slot splits. Every other slot is one kind of thing with few enough
            // entries that a filter would be furniture. The picker is reused, so the class a weapon
            // grid was left on has to be cleared here or it filters the next slot's pieces away.
            _activeClass = null;
            if (slot == GearSlot.Weapon)
            {
                // Open on the class already in hand - that is the one being asked about.
                var held = GearCatalog.Get(equippedId);
                _activeClass = held != null ? held.Class : FirstClassWithItems();
                BuildTabs();
            }

            BuildGrid();
        }

        WeaponClass FirstClassWithItems()
        {
            foreach (WeaponClass c in Enum.GetValues(typeof(WeaponClass)))
                foreach (var i in _all)
                    if (i.Class == c) return c;
            return WeaponClass.Greatsword;
        }

        static string ClassLabel(WeaponClass c) => c switch
        {
            WeaponClass.Disc => "DUAL DISCS",
            WeaponClass.Bow  => "BOW",
            _                => "SWORDS",
        };

        void BuildTabs()
        {
            var present = new List<WeaponClass>();
            foreach (WeaponClass c in Enum.GetValues(typeof(WeaponClass)))
                foreach (var i in _all)
                    if (i.Class == c) { present.Add(c); break; }

            float stripW = present.Count * TabW + (present.Count - 1) * TabGap;
            for (int i = 0; i < present.Count; i++)
            {
                var c = present[i];
                bool on = _activeClass == c;
                float x = -stripW * 0.5f + i * (TabW + TabGap);

                var tab = UiKit.Panel(_full, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(x, -320), new Vector2(x + TabW, -320 + TabH),
                    on ? new Color(0.16f, 0.15f, 0.10f) : new Color(0.10f, 0.11f, 0.15f));

                // CARDS, not pieces - the tab has to agree with the grid it opens, or it promises
                // twelve swords and shows nine.
                int n = 0;
                var counted = new HashSet<string>();
                foreach (var it in _all)
                {
                    if (it.Class != c) continue;
                    string key = KeyOf(it);
                    if (string.IsNullOrEmpty(key)) { n++; continue; }
                    if (counted.Add(key)) n++;
                }

                UiKit.Label(UiKit.Rect(tab, "l", Vector2.zero, Vector2.one,
                        Vector2.zero, Vector2.zero),
                    ClassLabel(c) + "   " + n, 22,
                    on ? new Color(0.94f, 0.82f, 0.46f) : new Color(0.62f, 0.66f, 0.74f),
                    TextAnchor.MiddleCenter);

                _tabs.Add((tab, c));
            }
        }

        /// <summary>
        /// The tab's items, with identical pieces collapsed into one card. Order is FIRST
        /// APPEARANCE, so a stack sits where its earliest member would have, and the grid does not
        /// reshuffle itself when a piece is equipped or worn down.
        /// </summary>
        List<Stack> BuildStacks()
        {
            var stacks = new List<Stack>();
            var byKey = new Dictionary<string, Stack>();

            foreach (var i in _all)
            {
                if (_activeClass != null && i.Class != _activeClass.Value) continue;

                string key = KeyOf(i);

                // No key means an authored, one-of-a-kind catalogue entry: its own card, always.
                if (string.IsNullOrEmpty(key))
                {
                    stacks.Add(new Stack
                    {
                        Item = i, Count = 1, PickId = i.ItemId, Worn = i.ItemId == _equippedId,
                    });
                    continue;
                }

                if (!byKey.TryGetValue(key, out var st))
                {
                    st = new Stack { Item = i, BestDurability = float.NegativeInfinity };
                    byKey[key] = st;
                    stacks.Add(st);
                }
                st.Count++;
                Consider(st, i);
            }

            return stacks;
        }

        /// <summary>What this list stacks on. Empty means never - see GearItem.StackKey.</summary>
        string KeyOf(GearItem item)
            => _stackBy == StackBy.Appearance ? item.AppearanceKey : item.StackKey;

        /// <summary>Which member of a stack the card hands over. The worn one wins outright;
        /// otherwise the least worn, so picking from a stack spends the freshest copy rather than
        /// whichever the catalogue happened to list first.</summary>
        void Consider(Stack st, GearItem candidate)
        {
            if (candidate.ItemId == _equippedId)
            {
                st.Worn = true;
                st.PickId = candidate.ItemId;
                return;
            }
            if (st.Worn) return;

            float d = _wear != null ? _wear(candidate.ItemId) : 0f;
            if (st.PickId != null && d <= st.BestDurability) return;
            st.PickId = candidate.ItemId;
            st.BestDurability = d;
        }

        /// <summary>The card for what is equipped now, if this grid shows it.</summary>
        RectTransform _wornRect;

        void BuildGrid()
        {
            if (_gridRoot) Destroy(_gridRoot);
            _cells.Clear();
            _cellItems.Clear();
            _cardPage.Clear();
            _wornRect = null;
            _prevRect = _nextRect = null;
            _pageText = null;

            _gridRoot = new GameObject("grid", typeof(RectTransform));
            _gridRoot.transform.SetParent(_full, false);
            var gr = (RectTransform)_gridRoot.transform;
            gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one;
            gr.offsetMin = Vector2.zero; gr.offsetMax = Vector2.zero;

            var shown = BuildStacks();

            int pieces = 0;
            foreach (var s in shown) pieces += s.Count;
            if (_countText != null)
                _countText.text = pieces == shown.Count
                    ? shown.Count + " to choose from"
                    : $"{shown.Count} to choose from   -   {pieces} pieces";

            int count = shown.Count + _emptyCells;          // cell 0 is EMPTY, when there is one

            // Centre on the cells that actually exist, not on the full eight columns. The whole
            // point of the class split is that BOW is one item - measured against a fixed
            // eight-wide grid it sat in the far left corner with two thirds of the screen empty
            // beside it, which reads as a layout fault rather than as a short list.
            int useCols = Mathf.Min(count, Cols);
            float gridW = useCols * Cell + (useCols - 1) * Gap;
            float pitch = Cell + Gap + LabelH;

            // Hung from below the header (and the tabs, when there are any) rather than centred on
            // the screen: with a filter in place the row count changes as tabs are switched, and a
            // grid that re-centres itself on every tab press makes the cards jump under the cursor.
            float top = _activeClass != null ? 190f : 292f;

            // As many rows as fit above the foot of the screen; the rest go to further pages, with
            // the page bar taking the foot when there is more than one.
            float half = _full.rect.height > 1f ? _full.rect.height * 0.5f : 540f;
            float floor = -half + PageMargin;
            float barTop = floor + PageBarH;
            int RowsAbove(float bottom) =>
                Mathf.Max(1, Mathf.FloorToInt((top - bottom - (Cell + LabelH)) / pitch) + 1);
            int totalRows = (count + Cols - 1) / Cols;
            int rowsPerPage = totalRows <= RowsAbove(floor) ? totalRows : RowsAbove(barTop + PageGap);
            int perPage = rowsPerPage * Cols;
            _pageCount = Mathf.Max(1, (count + perPage - 1) / perPage);

            // The grid stands left of centre so the grid and the details panel are centred as one.
            float shift = -(DetailW + DetailGap) * 0.5f;
            _gridRight = shift + gridW * 0.5f;
            _gridTop = top;
            int select = 0;

            for (int i = 0; i < count; i++)
            {
                var stack = i < _emptyCells ? null : shown[i - _emptyCells];
                var item = stack?.Item;
                bool isEmpty = item == null;
                bool worn = stack != null && stack.Worn;
                bool nothingWorn = string.IsNullOrEmpty(_equippedId);

                int page = i / perPage, onPage = i % perPage;
                int col = onPage % Cols, row = onPage / Cols;
                float x = shift - gridW * 0.5f + col * (Cell + Gap);
                float y = top - row * pitch;

                var card = UiKit.Panel(gr, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, y - Cell), new Vector2(x + Cell, y),
                    worn || (isEmpty && nothingWorn) ? new Color(0.14f, 0.13f, 0.09f)
                                                     : new Color(0.11f, 0.12f, 0.16f));

                if (isEmpty && _emptyItem != null)
                {
                    var emptyLayer = GearDisplay.Represent(_emptyItem);
                    if (emptyLayer?.Sprite != null)
                    {
                        var eart = UiKit.Rect(card, "art", Vector2.zero, Vector2.one,
                                              new Vector2(16, 22), new Vector2(-16, -16));
                        var eimg = eart.gameObject.AddComponent<Image>();
                        eimg.sprite = emptyLayer.Sprite;
                        eimg.preserveAspect = true;
                        eimg.raycastTarget = false;
                        eimg.color = emptyLayer.Tint;
                        KindledImage.On(eimg, _emptyItem);
                    }
                }

                if (!isEmpty)
                {
                    var layer = GearDisplay.Represent(item);
                    var art = UiKit.Rect(card, "art", Vector2.zero, Vector2.one,
                                         new Vector2(16, 22), new Vector2(-16, -16));
                    // A weapon with a hanging part gets its own rect inside the art box, so the
                    // fit below can move it without moving the box.
                    var holder = WeaponExtras.Any(item)
                        ? UiKit.Rect(art, "blade", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero)
                        : art;
                    var img = holder.gameObject.AddComponent<Image>();
                    img.sprite = layer?.Sprite;
                    // A minted piece with no art draws nothing, not a white square.
                    img.enabled = layer?.Sprite != null;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    img.color = layer != null ? layer.Tint : Color.white;
                    // A part that hangs off the weapon (Lumen's live wire) is part of how it
                    // reads - fitted into the card with the sword rather than left off it.
                    if (WeaponExtras.Hangs(item) && layer?.Sprite != null)
                        WeaponExtras.FitWithHanging(art, img, item, layer.Sprite,
                                                    new Vector2(Cell - 32f, Cell - 38f));
                    // ...and Saint's halo, behind and in front of the blade as in the hand.
                    if (layer?.Sprite != null)
                    {
                        WeaponExtras.AddHalo(img, item, layer.Sprite);
                        WeaponExtras.AddGemGlow(img, item, layer.Sprite);     // Prism's lit gem
                        KindledImage.On(img, item);                           // the Secret Fire's marks
                    }

                    // Tier swatch, so rarity reads before the name does - the same swatch the
                    // loadout rows already use, so the two screens agree at a glance.
                    var sw = UiKit.Panel(card, new Vector2(0, 1), new Vector2(0, 1),
                        new Vector2(8, -20), new Vector2(24, -6),
                        CharacterScreen.TierColor(item.Tier));
                    sw.GetComponent<Image>().raycastTarget = false;

                    // Stars on a minted piece, in the corner the power figure uses on an authored
                    // one - the two never share a card (minted pieces carry no Power).
                    if (item.Stars >= 0)
                        UiKit.Stars(UiKit.Rect(card, "stars", new Vector2(1, 1), new Vector2(1, 1),
                                        new Vector2(-60, -22), new Vector2(-6, -4)),
                                    item.Stars, Art.Gear.GearRoller.MaxLevel, 17f,
                                    new Color(1f, 0.82f, 0.3f), new Color(1f, 1f, 1f, 0.12f));
                    else if (item.Power > 0f)
                        UiKit.Label(UiKit.Rect(card, "p", new Vector2(1, 1), new Vector2(1, 1),
                            new Vector2(-52, -22), new Vector2(-6, -4)),
                            item.Power.ToString("0"), 16,
                            new Color(0.72f, 0.76f, 0.85f), TextAnchor.MiddleRight);

                    // TORSO ONLY - every torso rolls one of the four defensive abilities and
                    // nothing anywhere used to say which. A player choosing between two cuirasses
                    // could see their tier and power differ and had no way to see the one thing
                    // that actually changes how the character plays. Along the card's foot, under
                    // the art: between the swatch and the stars it ran into the stars (2026-11-06).
                    if (item.Slot == GearSlot.Torso)
                        UiKit.Label(UiKit.Rect(card, "ability", new Vector2(0, 0), new Vector2(1, 0),
                            new Vector2(4, 3), new Vector2(-4, 19)),
                            item.DefensiveAbility.Label(), 11,
                            new Color(0.58f, 0.74f, 0.86f), TextAnchor.MiddleCenter);
                }

                // The count rides the NAME rather than sitting in the card's top-right corner,
                // which the power figure already owns - a stacking piece is minted and carries no
                // power today, but "today" is not a layout rule and two labels in one rect is a
                // collision waiting for the first minted item that rolls one.
                string name = isEmpty ? (nothingWorn ? _emptyLabel + (_emptyItem != null ? "  (" + _wornLabel + ")" : "  (current)")
                                                     : _emptyLabel)
                                      : item.DisplayName
                                        + (stack.Count > 1 ? "  x" + stack.Count : "")
                                        + (worn ? "  (" + _wornLabel + ")" : "");

                // Wrapped to the card plus half a gap each side, so it never reaches the next one.
                var label = UiKit.Label(UiKit.Rect(card, "n", new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(-Gap * 0.5f + 2f, -LabelH), new Vector2(Gap * 0.5f - 2f, -4)),
                    name,
                    14,
                    worn || (isEmpty && nothingWorn) ? new Color(0.92f, 0.78f, 0.42f)
                                                     : new Color(0.8f, 0.83f, 0.9f),
                    TextAnchor.UpperCenter);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.lineSpacing = 0.9f;

                // The selection's outline, off until the card is selected.
                var outline = card.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.94f, 0.82f, 0.46f);
                outline.effectDistance = new Vector2(3f, -3f);
                outline.enabled = false;

                _cells.Add((card, stack?.PickId));
                _cellItems.Add(isEmpty ? null : item);
                _cardPage.Add(page);
                if (worn || (isEmpty && nothingWorn)) { _wornRect = card; select = i; }
            }

            if (_pageCount > 1) BuildPageBar(gr, shift, barTop);

            // Opens reading what is worn - the piece every other card is being compared with -
            // on the page that card is on.
            int open = Mathf.Clamp(_selected >= 0 && _selected < count ? _selected : select, 0, count - 1);
            ShowPage(_cardPage[open]);
            Select(open);
        }

        const float PageMargin = 16f, PageGap = 12f, PageBarH = 128f, PageButtonW = 220f;

        /// <summary>PREV, "page n / m", NEXT - centred under the grid, at the foot of the screen.</summary>
        void BuildPageBar(RectTransform gr, float centre, float barTop)
        {
            var centreAnchor = new Vector2(0.5f, 0.5f);
            float bottom = barTop - PageBarH;
            float span = 2f * PageButtonW + 240f;
            float left = centre - span * 0.5f, right = centre + span * 0.5f;

            _prevRect = UiKit.Panel(gr, centreAnchor, centreAnchor,
                new Vector2(left, bottom), new Vector2(left + PageButtonW, barTop),
                new Color(0.11f, 0.12f, 0.16f));
            UiKit.Label(_prevRect, "<   PREV", 22, new Color(0.8f, 0.83f, 0.9f), TextAnchor.MiddleCenter);

            _nextRect = UiKit.Panel(gr, centreAnchor, centreAnchor,
                new Vector2(right - PageButtonW, bottom), new Vector2(right, barTop),
                new Color(0.11f, 0.12f, 0.16f));
            UiKit.Label(_nextRect, "NEXT   >", 22, new Color(0.8f, 0.83f, 0.9f), TextAnchor.MiddleCenter);

            _pageText = UiKit.Label(UiKit.Rect(gr, "page", centreAnchor, centreAnchor,
                new Vector2(left + PageButtonW, bottom), new Vector2(right - PageButtonW, barTop)),
                "", 20, new Color(0.62f, 0.66f, 0.74f), TextAnchor.MiddleCenter);
        }

        /// <summary>Show one page of cards. Wraps, so NEXT on the last page goes back to the first.</summary>
        void ShowPage(int page)
        {
            _pageIndex = ((page % _pageCount) + _pageCount) % _pageCount;
            for (int c = 0; c < _cells.Count; c++)
                _cells[c].Rect.gameObject.SetActive(_cardPage[c] == _pageIndex);
            if (_pageText != null)
                _pageText.text = $"page {_pageIndex + 1} / {_pageCount}";
        }

        /// <summary>Turn the page. The pad's focus moves to the new page's first card, since the
        /// card it was on is no longer there to be on.</summary>
        void TurnPage(int step)
        {
            if (_pageCount <= 1) return;
            ShowPage(_pageIndex + step);
            if (Core.Controls.GamepadMode)
                for (int c = 0; c < _cells.Count; c++)
                    if (_cardPage[c] == _pageIndex) { Core.Controls.Focus(_cells[c].Rect); break; }
        }

        bool OnPage(int i) => i >= 0 && i < _cardPage.Count && _cardPage[i] == _pageIndex;

        float _gridRight, _gridTop;

        /// <summary>Whether cell <paramref name="i"/> is what the slot holds now.</summary>
        bool IsCurrent(int i) => i < _emptyCells ? string.IsNullOrEmpty(_equippedId)
                                        : _cells[i].ItemId == _equippedId ||
                                          (_wornRect != null && _cells[i].Rect == _wornRect);

        /// <summary>Select a card: outline it and read it in the details panel.</summary>
        void Select(int i)
        {
            _selected = i;
            for (int c = 0; c < _cells.Count; c++)
            {
                var o = _cells[c].Rect.GetComponent<Outline>();
                if (o != null) o.enabled = c == i;
            }
            BuildDetail();
        }

        void BuildDetail()
        {
            if (_detail) Destroy(_detail.gameObject);
            _detail = null;
            _actionRect = null;
            if (_gridRoot == null || _selected < 0 || _selected >= _cells.Count) return;

            var item = _cellItems[_selected];
            bool current = IsCurrent(_selected);
            var ink = new Color(0.85f, 0.87f, 0.92f);
            var dim = new Color(0.5f, 0.53f, 0.6f);

            string text;
            if (item != null)
                text = _describe != null ? _describe(item)
                     : $"<size=21><color=#{ColorUtility.ToHtmlStringRGB(CharacterScreen.TierColor(item.Tier))}>{item.DisplayName}</color></size>";
            else if (_emptyItem != null)
                text = $"<size=21>{_emptyLabel.ToUpperInvariant()}</size>\n<color=#{ColorUtility.ToHtmlStringRGB(dim)}>shows {_emptyItem.DisplayName}, as it is</color>";
            else
                text = $"<size=21>{_emptyLabel.ToUpperInvariant()}</size>\n<color=#{ColorUtility.ToHtmlStringRGB(dim)}>nothing in this slot</color>";

            const float ButtonH = 128f, Pad = 20f;
            float left = _gridRight + DetailGap;
            var gr = (RectTransform)_gridRoot.transform;
            _detail = UiKit.Panel(gr, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(left, _gridTop - 200f), new Vector2(left + DetailW, _gridTop),
                new Color(0.07f, 0.075f, 0.1f, 1f));

            var t = UiKit.Label(UiKit.Rect(_detail, "t", Vector2.zero, Vector2.one,
                    new Vector2(Pad, Pad + ButtonH + Pad), new Vector2(-Pad, -Pad)),
                text, 17, ink, TextAnchor.UpperLeft);
            t.supportRichText = true;
            t.lineSpacing = 1.05f;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Sized to the read as laid out at the panel's width.
            float h = Mathf.Max(200f, t.preferredHeight + Pad * 3f + ButtonH);
            _detail.offsetMin = new Vector2(left, _gridTop - h);

            // The button: what choosing this card would do. On what is already worn it says so,
            // and pressing it only closes.
            string action = current ? _wornLabel.ToUpperInvariant()
                          : item == null ? (_emptyItem != null ? _emptyLabel.ToUpperInvariant() : "TAKE OFF")
                          : _pickLabel;
            _actionRect = UiKit.Panel(_detail, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(Pad, Pad), new Vector2(-Pad, Pad + ButtonH),
                current ? new Color(0.12f, 0.13f, 0.17f) : new Color(0.20f, 0.17f, 0.09f));
            UiKit.Label(_actionRect, action, 24,
                current ? dim : new Color(0.94f, 0.82f, 0.46f), TextAnchor.MiddleCenter);
        }

        /// <summary>Choose the selected card: equip it (or show it), then close.</summary>
        void PickSelected()
        {
            if (_selected < 0 || _selected >= _cells.Count) return;
            bool current = IsCurrent(_selected);
            var pick = _cells[_selected].ItemId;
            var cb = _onPick;
            Close();
            if (!current) cb?.Invoke(pick);
        }

        /// <summary>Repaint the tab strip so the active one reads as active.</summary>
        void RefreshTabs()
        {
            foreach (var (rect, c) in _tabs)
            {
                bool on = _activeClass == c;
                var bg = rect.GetComponent<Image>();
                if (bg != null)
                    bg.color = on ? new Color(0.16f, 0.15f, 0.10f) : new Color(0.10f, 0.11f, 0.15f);
                var txt = rect.GetComponentInChildren<Text>();
                if (txt != null)
                    txt.color = on ? new Color(0.94f, 0.82f, 0.46f) : new Color(0.62f, 0.66f, 0.74f);
            }
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (_root) Destroy(_root);
            _root = null;
            _gridRoot = null;
            _full = null;
            _countText = null;
            _cells.Clear();
            _cellItems.Clear();
            _cardPage.Clear();
            _prevRect = _nextRect = null;
            _pageText = null;
            _pageIndex = 0;
            _pageCount = 1;
            _tabs.Clear();
            _detail = _actionRect = null;
            _selected = -1;
            _describe = null;
            _pickLabel = "EQUIP";
            _onPick = null;
            _wear = null;
            _stackBy = StackBy.Attributes;
            _emptyLabel = "empty";
            _titleSuffix = null;
            _wornLabel = "worn";
            _emptyItem = null;
        }

        void Update()
        {
            if (!IsOpen) return;

            // The worn card first, so a gamepad opens on what is equipped; then the rest of the
            // grid, then the tabs above it.
            _focusRects.Clear();
            if (_wornRect != null && _wornRect.gameObject.activeSelf) _focusRects.Add(_wornRect);
            for (int c = 0; c < _cells.Count; c++)
                if (OnPage(c)) _focusRects.Add(_cells[c].Rect);
            if (_actionRect != null) _focusRects.Add(_actionRect);
            if (_prevRect != null) _focusRects.Add(_prevRect);
            if (_nextRect != null) _focusRects.Add(_nextRect);
            foreach (var (rect, _) in _tabs) _focusRects.Add(rect);
            Core.Controls.SetFocusCandidates(_focusRects);

            // Back only. Deliberately NOT InteractTapped: on a keyboard that shares E with the
            // ability button, and this screen is reachable mid-run.
            if (Core.Controls.CancelTapped) { Core.Controls.ConsumeCancel(); Close(); return; }

            if (Time.frameCount == _openedFrame) return;   // the press that opened this

            // The bumpers turn the grid's page; the circle behind stands down while this is open.
            if (Core.Controls.PagePrevTapped) { TurnPage(-1); return; }
            if (Core.Controls.PageNextTapped) { TurnPage(1); return; }

            if (!Core.Controls.Tapped(out var at)) return;

            // Tabs first: they sit above the grid and a press on one must never fall through to
            // whatever card happens to be behind it.
            foreach (var (rect, c) in _tabs)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) continue;
                if (_activeClass == c) return;
                _activeClass = c;
                _selected = -1;
                RefreshTabs();
                BuildGrid();
                return;
            }

            if (_actionRect != null && RectTransformUtility.RectangleContainsScreenPoint(_actionRect, at, null))
            {
                PickSelected();
                return;
            }

            if (_prevRect != null && RectTransformUtility.RectangleContainsScreenPoint(_prevRect, at, null))
            {
                TurnPage(-1);
                return;
            }
            if (_nextRect != null && RectTransformUtility.RectangleContainsScreenPoint(_nextRect, at, null))
            {
                TurnPage(1);
                return;
            }

            // Only this page's cards: a hidden card still has its rect, and would take the press.
            for (int i = 0; i < _cells.Count; i++)
            {
                if (!OnPage(i)) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(_cells[i].Rect, at, null)) continue;
                if (i != _selected) Select(i);
                return;
            }
        }
    }
}
