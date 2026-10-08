using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Convergence.Art;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The transmutation circle's screen: the loadout, plus everything about how the character
    /// LOOKS.
    ///
    /// Organised around the tria prima, which is not decoration - the three alchemical principles
    /// happen to name the three things this screen actually does, and using them as the tabs
    /// means the circle on the floor and the screen it opens are describing the same idea:
    ///
    ///   SULFUR   soul, the active principle   -> gear that fights: stats, power, durability
    ///   MERCURY  spirit, transformation       -> gear that is SEEN: transmog, form without substance
    ///   SALT     body, fixity                 -> the character itself: skin, hair, eyes, face
    ///
    /// Sulfur and Mercury are deliberately separate pages rather than two columns of one. They
    /// are the same twelve slots asked two different questions, and a player who has just been
    /// handed a stat upgrade they think is ugly needs to understand that those are now
    /// independent decisions. One merged page would teach the opposite.
    ///
    /// The quick loadout on [C] still exists and is unchanged - it opens mid-run, where a
    /// cosmetics studio has no business being.
    /// </summary>
    public class TransmutationScreen : MonoBehaviour
    {
        public enum Tab { Sulfur, Mercury, Salt }

        public bool IsOpen { get; private set; }

        Func<CharacterProfile> _profile;
        Action<GearSlot, string> _equip;
        Action _toggleHelm;
        Action _changed;

        ElementType _element;
        Tab _tab = Tab.Sulfur;

        GameObject _root;
        RectTransform _pane;

        /// <summary>
        /// The rows, inside the clipping pane. Dragged vertically; see <see cref="Scroll"/>.
        /// </summary>
        RectTransform _content;

        float _scroll, _contentHeight;
        bool _scrolling;
        Vector2 _scrollOrigin;
        float _scrollFrom;
        Vector2 _pressAt;
        bool _pressed;
        readonly CharacterPreview _preview = new();
        readonly List<Hit> _hits = new();

        /// <summary>
        /// Clickable rects handed to Controls each frame for D-pad navigation.
        ///
        /// Pane rows are filtered to what is actually VISIBLE, for the same reason the tap
        /// handler checks the pane before walking _hits: a row scrolled past the top is still a
        /// rectangle that contains points, so offering one as a focus target would move the
        /// cursor somewhere the player cannot see and then refuse the press.
        /// </summary>
        readonly List<RectTransform> _focusRects = new();
        readonly List<(RectTransform Rect, Tab Tab)> _tabs = new();
        Text _powerText;

        struct Hit
        {
            public RectTransform Rect;
            public Action OnClick;
        }

        /// <param name="changed">Repaint every live rig and checkpoint the profile. Called after
        /// any edit, because appearance is account-level and permanent - the same write point
        /// equipping already uses.</param>
        public void Init(Func<CharacterProfile> profile, Action<GearSlot, string> equip,
                         Action toggleHelm, Action changed)
        {
            _profile = profile;
            _equip = equip;
            _toggleHelm = toggleHelm;
            _changed = changed;
        }

        // ------------------------------------------------------------------ open / close

        public void Toggle(Transform canvas, ElementType element)
        {
            if (IsOpen) Close(); else Open(canvas, element);
        }

        public void Open(Transform canvas, ElementType element)
        {
            if (IsOpen || _profile == null) return;
            IsOpen = true;
            _element = element;
            GamePause.Hold(this);

            _weaponHiddenInPreview = false;
            _preview.Build(_element, 460, 760);
            BuildChrome(canvas);
            BuildPane();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);

            // Before _root goes: the grid is parented to it, so destroying the screen would take
            // the picker's GameObject with it while the component still believed it was open -
            // and an open picker refuses to open again, so the slot rows would go dead for the
            // rest of the session with nothing on screen to say why.
            _picker?.Close();

            if (_root) Destroy(_root);
            _preview.Dispose();
            _hits.Clear();
            _tabs.Clear();
            _root = null;
            _pane = null;
        }

        // ------------------------------------------------------------------ chrome

        void BuildChrome(Transform canvas)
        {
            _root = new GameObject("TransmutationScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.032f, 0.045f, 1f));

            var title = UiKit.Rect(full, "title", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -104), new Vector2(0, -44));
            UiKit.Label(title, "TRANSMUTATION", 42, new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleCenter);

            var sub = UiKit.Rect(full, "sub", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -136), new Vector2(0, -104));
            UiKit.Label(sub, "what you fight with, what you are seen as, and what you are",
                17, new Color(0.45f, 0.48f, 0.56f), TextAnchor.MiddleCenter);

            // Preview, left.
            var frame = UiKit.Panel(full, Centre, Centre,
                new Vector2(-900, -330), new Vector2(-500, 300), new Color(0.07f, 0.08f, 0.11f, 1f));
            var imgGo = new GameObject("preview", typeof(RawImage));
            imgGo.transform.SetParent(frame, false);
            var irt = (RectTransform)imgGo.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(8, 8); irt.offsetMax = new Vector2(-8, -8);
            // Sized to the pixels it is actually drawn at, NOT to a constant - see
            // CharacterPreview.MatchTo. Done here because only the layout knows the size.
            _preview.MatchTo(imgGo.GetComponent<RawImage>(), full.GetComponentInParent<Canvas>());

            _powerText = UiKit.Label(
                UiKit.Rect(full, "power", Centre, Centre, new Vector2(-900, -374), new Vector2(-500, -336)),
                "", 18, new Color(0.7f, 0.73f, 0.8f), TextAnchor.MiddleCenter);

            // Tabs.
            string[] names = { "SULFUR", "MERCURY", "SALT" };
            string[] blurbs = { "gear", "appearance", "body" };
            for (int i = 0; i < 3; i++)
            {
                float x = -450 + i * 380;
                // 128 tall rather than 64, and lifted to 246 so the taller tab still clears the
                // pane's top edge at 224.
                var t = UiKit.Panel(full, Centre, Centre,
                                    new Vector2(x, 246), new Vector2(x + 360, 246 + UiKit.TouchTarget),
                                    new Color(0.09f, 0.10f, 0.14f, 1f));
                UiKit.Label(UiKit.Rect(t, "n", Vector2.zero, Vector2.one, new Vector2(20, 52), new Vector2(-20, -20)),
                            names[i], 26, Color.white, TextAnchor.MiddleLeft);
                UiKit.Label(UiKit.Rect(t, "b", Vector2.zero, Vector2.one, new Vector2(20, 18), new Vector2(-20, -66)),
                            blurbs[i], 17, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleLeft);
                _tabs.Add((t, (Tab)i));
            }

            _pane = UiKit.Rect(full, "pane", Centre, Centre,
                new Vector2(-450, -330), new Vector2(-450 + PaneW, -330 + PaneH));

            // The pane CLIPS and its contents SCROLL. Both are new, and both fall out of the rows
            // becoming finger-sized: the body page is six rows plus a toggle, and at 128 per row
            // that is a thousand units of content against 554 of pane. It used to fit only
            // because a row was 34 units tall, which is 12pt on a phone.
            //
            // RectMask2D rather than Mask - it is a rectangle clip with no stencil buffer and no
            // extra draw call, which is all this needs.
            _pane.gameObject.AddComponent<RectMask2D>();

            _content = UiKit.Rect(_pane, "content", new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, Vector2.zero);
            _content.pivot = new Vector2(0.5f, 1f);

            var foot = UiKit.Rect(full, "foot", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 34), new Vector2(0, 74));
            UiKit.Hint(foot,
                "click to change    -    [ESC] or [E] to leave the circle    -    changes are saved as you make them",
                "tap to change    -    BACK to leave the circle    -    changes are saved as you make them",
                17, new Color(0.42f, 0.45f, 0.53f), TextAnchor.MiddleCenter,
                UI.GamepadGlyphs.Navigate + " to choose, " + UI.GamepadGlyphs.Confirm + " to change    -    "
                + UI.GamepadGlyphs.Cancel + " to leave the circle    -    changes are saved as you make them");
        }

        static Vector2 Centre => new(0.5f, 0.5f);

        // ------------------------------------------------------------------ panes

        void BuildPane()
        {
            _hits.Clear();
            _rowRefresh.Clear();

            // Detached and hidden BEFORE Destroy, which is deferred to end of frame: left
            // parented, the outgoing tab draws on top of the incoming one for a frame and every
            // tab switch flickers a double image of twelve overlapping cards.
            _scroll = 0f;
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i);
                child.SetParent(null, false);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            foreach (var (rect, tab) in _tabs)
            {
                bool on = tab == _tab;
                rect.GetComponent<Image>().color = on
                    ? new Color(0.16f, 0.17f, 0.23f, 1f)
                    : new Color(0.075f, 0.08f, 0.11f, 1f);
            }

            switch (_tab)
            {
                case Tab.Sulfur:  BuildSlots(transmog: false); break;
                case Tab.Mercury: BuildSlots(transmog: true); break;
                default:          BuildBody(); break;
            }

            // Measured off what was actually laid out rather than predicted, so a row added later
            // scrolls without anyone remembering to update a number.
            //
            // Direct children only, and their offsetMin: every row anchors to the content's TOP
            // corner and lays out downward, so offsetMin.y IS how far below the top it reaches.
            // Walking the whole tree instead would mix in grandchildren whose offsets are
            // relative to their own row, not to the page.
            float lowest = 0f;
            for (int i = 0; i < _content.childCount; i++)
                lowest = Mathf.Min(lowest, ((RectTransform)_content.GetChild(i)).offsetMin.y);
            _contentHeight = Mathf.Max(PaneH, -lowest + 24f);
            _content.sizeDelta = new Vector2(0f, _contentHeight);
            ApplyScroll();

            Refresh();
        }

        /// <summary>
        /// The slots, asked either "what is equipped" (sulfur) or "what does it look like"
        /// (mercury).
        ///
        /// SULFUR shows twelve, MERCURY eleven - the relic has an equip card and no disguise card,
        /// because a relic's content is its FINISHER and a disguise deciding a mechanic is the one
        /// thing "a skin grants nothing" exists to forbid. See GearSlots.Transmoggable.
        ///
        /// The row count is fixed rather than derived so the two tabs stay ALIGNED - the same slot
        /// has to sit in the same place when you switch, and deriving it from the count would
        /// shuffle every card the moment the lists differ in length. Which is exactly why the
        /// relic can be dropped from one tab for free: it is LAST in both lists, so mercury just
        /// ends a card early and nothing above it moves. Five deep also keeps twelve inside three
        /// columns; at four deep a thirteenth would open a fourth column past the pane, which is
        /// why this stayed five when Trinket's retirement dropped the count by one.
        /// </summary>
        void BuildSlots(bool transmog)
        {
            var slots = transmog ? GearSlots.Transmoggable : GearSlots.Selectable;
            const int PerCol = 5;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                int col = i / PerCol, row = i % PerCol;
                float x = col * 452;
                float y = -row * 78;

                // The relic's whole content is its finisher (see GearItem.GrantedFinisher's own
                // note: a relic below Black Diamond rolls one too, it just cannot LOCK it) and
                // nothing on this screen ever named it - a player had to remember which relic did
                // what. Its card grows a third line for that reason alone; every other slot keeps
                // the two-line layout.
                bool relic = !transmog && slot == GearSlot.Relic;
                float cardH = relic ? 96f : 68f;

                var card = UiKit.Panel(_content, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(x, y - cardH), new Vector2(x + 432, y), new Color(0.085f, 0.095f, 0.13f, 1f));

                var swatch = UiKit.Rect(card, "swatch", new Vector2(0, 0), new Vector2(0, 1),
                    Vector2.zero, new Vector2(6, 0)).gameObject.AddComponent<Image>();

                UiKit.Label(UiKit.Rect(card, "slot", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(18, -30), new Vector2(-14, -6)),
                    slot.ToString().ToUpper(), 14, new Color(0.5f, 0.53f, 0.6f));

                var value = UiKit.Label(UiKit.Rect(card, "item", new Vector2(0, relic ? 1 : 0), new Vector2(1, 1),
                    new Vector2(18, relic ? -58 : 8), new Vector2(-14, -34)),
                    "-", 19, new Color(0.85f, 0.87f, 0.92f));

                Text finisher = null;
                if (relic)
                    finisher = UiKit.Label(UiKit.Rect(card, "finisher", new Vector2(0, 1), new Vector2(1, 1),
                        new Vector2(18, -90), new Vector2(-14, -62)),
                        "", 15, new Color(0.62f, 0.56f, 0.78f));

                var s = slot;
                _hits.Add(new Hit { Rect = card, OnClick = () => OpenPicker(s, transmog) });
                _rowRefresh.Add(() =>
                {
                    var look = _profile().Look;
                    if (transmog)
                    {
                        var over = GearCatalog.Get(look.Transmog.Get(s));
                        var real = GearCatalog.Get(_profile().Gear.Get(s));
                        if (over != null)
                        {
                            value.text = over.DisplayName;
                            value.color = CharacterScreen.TierColor(over.Tier);
                            swatch.color = CharacterScreen.TierColor(over.Tier);
                        }
                        else
                        {
                            // Pass-through is the DEFAULT, and it has to look like a default
                            // rather than like an empty slot - naming what it is currently
                            // showing is the difference between "nothing here" and "unchanged".
                            value.text = real != null ? $"as equipped  ({real.DisplayName})" : "as equipped  (bare)";
                            value.color = new Color(0.42f, 0.45f, 0.52f);
                            swatch.color = new Color(0.16f, 0.17f, 0.22f);
                        }
                        return;
                    }

                    var item = GearCatalog.Get(_profile().Gear.Get(s));
                    value.text = item != null ? item.DisplayName : "- empty -";
                    value.color = item != null ? CharacterScreen.TierColor(item.Tier) : new Color(0.35f, 0.37f, 0.43f);
                    swatch.color = item != null ? CharacterScreen.TierColor(item.Tier) : new Color(0.18f, 0.19f, 0.24f);

                    // A slot whose look is overridden is flagged HERE too, or the stat page
                    // quietly lies about what the character is wearing.
                    // The honoured question, not the held one: a weapon skin refused for a class
                    // mismatch is not disguising anything, and saying so would be the stat page
                    // lying about the character again.
                    if (look.IsTransmogged(s, _profile().Gear)) value.text += "   (disguised)";

                    if (finisher != null)
                    {
                        var moveset = item?.GrantedFinisher;
                        finisher.text = moveset != null
                            ? (item.Signature != null ? $"weapon art: {moveset.DisplayName}  (locked)"
                                                       : $"weapon art: {moveset.DisplayName}")
                            : "";
                    }
                });
            }

            if (!transmog) return;

            // The three buttons sit in ONE row under the cards, one per card column. Stacked in
            // column 0 they started inside the fifth row and covered the Neck card.
            float top = -(PerCol - 1) * 78 - 68 - 20, bottom = top - 56;

            var clear = UiKit.Panel(_content, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, bottom), new Vector2(432, top), new Color(0.11f, 0.09f, 0.09f, 1f));
            UiKit.Label(clear, "REMOVE ALL DISGUISES", 18, new Color(0.85f, 0.7f, 0.7f), TextAnchor.MiddleCenter);
            _hits.Add(new Hit
            {
                Rect = clear,
                OnClick = () =>
                {
                    foreach (var s in GearSlots.All) _profile().Look.Transmog.Clear(s);
                    Commit();
                }
            });

            // Both toggles live here rather than on SALT - they are questions about what gear is
            // SEEN, the same question every other row on this tab asks.
            //
            // HELMET is a real, persisted preference (CharacterProfile.HelmHidden) applied to
            // every rig showing this character, same as the loadout screen's own copy of this
            // toggle. WEAPON is deliberately NOT persisted anywhere - it only ever reaches the
            // preview rig this screen owns (see CharacterPreview.Rig / PrimitiveCharacterRig.
            // SetWeaponHidden), so hiding a greatsword to look at a cloak underneath it never
            // touches what the arena, hub, or anyone else's screen actually draws.
            var helm = UiKit.Panel(_content, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(452, bottom), new Vector2(884, top), new Color(0.09f, 0.10f, 0.14f, 1f));
            var helmText = UiKit.Label(helm, "", 17, new Color(0.85f, 0.87f, 0.92f), TextAnchor.MiddleCenter);
            _hits.Add(new Hit { Rect = helm, OnClick = () => { _toggleHelm?.Invoke(); Refresh(); } });
            _rowRefresh.Add(() => helmText.text = _profile().HelmHidden
                ? "HELMET: HIDDEN (click to show)" : "HELMET: SHOWN (click to hide)");

            var weapon = UiKit.Panel(_content, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(904, bottom), new Vector2(1336, top), new Color(0.09f, 0.10f, 0.14f, 1f));
            var weaponText = UiKit.Label(weapon, "", 17, new Color(0.85f, 0.87f, 0.92f), TextAnchor.MiddleCenter);
            _hits.Add(new Hit
            {
                Rect = weapon,
                OnClick = () =>
                {
                    _weaponHiddenInPreview = !_weaponHiddenInPreview;
                    _preview.Rig?.SetWeaponHidden(_weaponHiddenInPreview);
                    Refresh();
                }
            });
            _rowRefresh.Add(() => weaponText.text = _weaponHiddenInPreview
                ? "WEAPON: HIDDEN HERE (click to show)" : "WEAPON: SHOWN (click to hide here)");
        }

        /// <summary>
        /// Preview-only, never saved - see the note above where this toggle is built. Reset every
        /// time the circle is opened, so a hidden weapon does not silently carry over from a
        /// previous visit with nothing on screen to explain why the sword is missing.
        /// </summary>
        bool _weaponHiddenInPreview;

        readonly List<Action> _rowRefresh = new();

        /// <summary>SALT: the character underneath the gear.</summary>
        void BuildBody()
        {
            float y = 0f;

            y = Swatches("SKIN", BodyLook.Skins, () => _profile().Look.Skin,
                         k => { _profile().Look.Skin = k; Commit(); }, y);
            y = Swatches("HAIR COLOUR", BodyLook.Hairs, () => _profile().Look.Hair,
                         k => { _profile().Look.Hair = k; Commit(); }, y);
            // The note is why this row is not just a row of colours: three faces paint their own
            // eyes, and a swatch that silently does nothing is worse than one that says so.
            y = Swatches("EYES", BodyLook.Eyes, () => _profile().Look.Eyes,
                         k => { _profile().Look.Eyes = k; Commit(); }, y,
                         () => BodyLook.OverridesEyeColour(_profile().Look.Expression)
                             ? "EYES    (this face sets its own)" : "EYES");
            y = Chips("HAIR STYLE", BodyLook.HairStyles, () => _profile().Look.HairStyle,
                      k => { _profile().Look.HairStyle = k; Commit(); }, y);
            y = Chips("BEARD", BodyLook.Beards, () => _profile().Look.Beard,
                      k => { _profile().Look.Beard = k; Commit(); }, y);
            y = Chips("EYEBROWS", BodyLook.Brows, () => _profile().Look.Brows,
                      k => { _profile().Look.Brows = k; Commit(); }, y);
            // The same swatches the hair row offers, plus a MATCH entry - which is the default and
            // the one most characters want, since brows following the hair is what reads as
            // deliberate rather than as an oversight.
            y = Swatches("BROW COLOUR", BodyLook.BrowSwatches, () => _profile().Look.BrowColour,
                         k => { _profile().Look.BrowColour = k; Commit(); }, y,
                         () => string.IsNullOrEmpty(_profile().Look.BrowColour)
                             ? "BROW COLOUR    (matching hair)" : "BROW COLOUR");
            y = Chips("EXPRESSION", BodyLook.Expressions, () => _profile().Look.Expression,
                      k => { _profile().Look.Expression = k; Commit(); }, y);
        }

        // Row metrics. These were squeezed to fit six rows plus the helmet toggle into 554pt of
        // pane, which left 40pt of headroom and a 34pt row - about 12pt on a phone, or a third of
        // a finger. The pane CLIPS AND SCROLLS now, so a row is a full touch target and the page
        // is 1084 units deep against 554 of pane. Adding a seventh row is free.
        const float RowLabel = 28f, RowGap = 14f, ChipH = UiKit.TouchTarget;

        // The pane's own size, so the rows below can fit themselves to it rather than trusting a
        // width nobody re-checks. Both rows used to lay out left-to-right off the end of the world
        // and the twelfth expression chip sat 430pt past the edge, where it is not clipped, not
        // scrolled and not reachable - it just is not there, which reads as the option not
        // existing rather than as a layout bug.
        const float PaneW = 1360f, PaneH = 554f;

        /// <summary>
        /// A labelled row of colour chips. Returns the y to continue from.
        ///
        /// <paramref name="note"/> replaces the label when the row needs to say something about
        /// itself - refreshed with everything else, so it can react to another row's choice.
        /// </summary>
        float Swatches(string label, BodyLook.Swatch[] set, Func<string> current, Action<string> pick,
                       float y, Func<string> note = null)
        {
            var head = UiKit.Label(UiKit.Rect(_content, "l", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, y - 24), new Vector2(400, y - 2)),
                label, 15, new Color(0.5f, 0.53f, 0.6f));
            if (note != null) _rowRefresh.Add(() => head.text = note());

            // WRAPS, like the chip rows, and for the reason the chip rows already learned the hard
            // way: a cell past the pane's right edge is not clipped-but-reachable, it simply is
            // not there, and now that the pane masks it is not even visibly missing. At a finger's
            // width fourteen skins need 1932 units against a 1360 pane, so nine fit a line and the
            // rest go below - which the page can afford since it scrolls.
            const float size = UiKit.TouchTarget, gap = 10f, lineGap = 6f;
            int perLine = Mathf.Max(1, Mathf.FloorToInt((PaneW + gap) / (size + gap)));

            for (int i = 0; i < set.Length; i++)
            {
                var sw = set[i];
                float x = (i % perLine) * (size + gap);
                float top = y - RowLabel - (i / perLine) * (size + lineGap);
                var cell = UiKit.Panel(_content, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(x, top - size), new Vector2(x + size, top),
                    new Color(0.2f, 0.2f, 0.25f));
                var inner = UiKit.Rect(cell, "c", Vector2.zero, Vector2.one,
                    new Vector2(3, 3), new Vector2(-3, -3)).gameObject.AddComponent<Image>();
                inner.color = sw.Color;

                string key = sw.Key;
                _hits.Add(new Hit { Rect = cell, OnClick = () => pick(key) });

                var border = cell.GetComponent<Image>();
                _rowRefresh.Add(() => border.color = current() == key
                    ? new Color(1f, 0.92f, 0.6f) : new Color(0.16f, 0.17f, 0.22f));
            }
            int lines = Mathf.CeilToInt(set.Length / (float)perLine);
            return y - RowLabel - lines * size - (lines - 1) * lineGap - RowGap;
        }

        /// <summary>A labelled row of named buttons, for things with no colour to show.</summary>
        float Chips(string label, BodyLook.Style[] set, Func<string> current, Action<string> pick, float y)
        {
            UiKit.Label(UiKit.Rect(_content, "l", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(0, y - 24), new Vector2(300, y - 2)),
                label, 15, new Color(0.5f, 0.53f, 0.6f));

            // FIT, then wrap. Chips shrink to share the pane before a second line is spent,
            // because vertical is the scarce axis here (40pt of headroom against a 34pt chip) and
            // horizontal is not - twelve expressions come out at 104 wide, which still holds the
            // longest name in the set. The wrap is the backstop for a set that cannot be shrunk
            // into one line without the text going unreadable, and it costs a row of budget the
            // page does not currently have spare.
            // minW is a FINGER now, not 96. Twelve expressions at 128 wide need 1656 units against
            // a 1360 pane, so the row wraps to two lines - which used to be the expensive answer,
            // because vertical was the scarce axis and there was 40pt of headroom. The page
            // scrolls now, so a second line costs nothing and a chip too small to hit costs the
            // option.
            const float gap = 10f, maxW = 180f, minW = UiKit.TouchTarget;
            const float h = ChipH, lineGap = 6f;

            float w = Mathf.Clamp((PaneW + gap) / set.Length - gap, minW, maxW);
            int perLine = Mathf.Max(1, Mathf.FloorToInt((PaneW + gap) / (w + gap)));

            for (int i = 0; i < set.Length; i++)
            {
                var st = set[i];
                float x = (i % perLine) * (w + gap);
                float top = y - RowLabel - (i / perLine) * (h + lineGap);
                var cell = UiKit.Panel(_content, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(x, top - h), new Vector2(x + w, top),
                    new Color(0.09f, 0.10f, 0.14f));
                var text = UiKit.Label(cell, st.Name, 17, Color.white, TextAnchor.MiddleCenter);

                string key = st.Key;
                _hits.Add(new Hit { Rect = cell, OnClick = () => pick(key) });

                var bg = cell.GetComponent<Image>();
                _rowRefresh.Add(() =>
                {
                    bool on = current() == key;
                    bg.color = on ? new Color(0.22f, 0.20f, 0.13f) : new Color(0.09f, 0.10f, 0.14f);
                    text.color = on ? new Color(1f, 0.92f, 0.6f) : new Color(0.65f, 0.68f, 0.75f);
                });
            }
            int lines = Mathf.CeilToInt(set.Length / (float)perLine);
            return y - RowLabel - lines * h - (lines - 1) * lineGap - RowGap;
        }

        // ------------------------------------------------------------------ edits

        /// <summary>The per-slot grid, shared with the loadout screen. Created on demand and
        /// kept, so reopening a slot does not churn a component every press.</summary>
        GearPicker _picker;

        /// <summary>True while that grid is up. This screen stands down entirely then - nothing
        /// in this project consumes input, so without it a tap on a picker card would also land on
        /// the slot row underneath, and Cancel would close both at once.</summary>
        public bool PickerOpen => _picker != null && _picker.IsOpen;

        /// <summary>
        /// Open the grid for one slot, on either question - what to WEAR (sulfur) or what to be
        /// SEEN as (mercury).
        ///
        /// THIS REPLACED A CYCLE on both tabs, the same cycle the loadout screen already gave up:
        /// press the row, advance to the next item, wrap round to nothing. Fine at three weapons,
        /// unusable past thirty pieces - no way to see what is coming, no way to step back, one
        /// press per item to reach the last. Both tabs going through the one grid is also what
        /// makes the two screens read as the same game rather than as two eras of one.
        ///
        /// The cycle's extra "as equipped" stop survives as the grid's own EMPTY cell, which is
        /// first and always present: a player who disguises a slot has to be able to get back to
        /// showing what they are really wearing without hunting for the matching item.
        /// </summary>
        void OpenPicker(GearSlot slot, bool transmog)
        {
            if (_root == null || PickerOpen) return;

            // The relic socket has no items of its own: a candidate declares Slot.Weapon, because
            // the same sword has to stay wieldable. Its list is filtered by what is being HELD.
            var held = GearCatalog.Get(_profile().Gear.Get(GearSlot.Weapon));
            _picker ??= gameObject.AddComponent<GearPicker>();

            if (!transmog)
            {
                _picker.Open(_root.transform, slot, held, _profile().Gear.Get(slot),
                             id => { _equip(slot, id); Refresh(); },
                             id => _profile().Wear.Get(id));
                return;
            }

            // A weapon may only be disguised as another weapon of its own class - the class is
            // how the thing is HELD, so the list is narrowed before the grid ever sees it rather
            // than offered and then refused at the point of use.
            var options = SameClassOnly(slot, GearCatalog.ForSlot(slot));

            _picker.Open(_root.transform, slot, held, _profile().Look.Transmog.Get(slot),
                         id => { _profile().Look.Transmog.Set(slot, id); Commit(); },
                         wear: null,                       // a disguise never wears
                         stackBy: GearPicker.StackBy.Appearance,
                         options: options,
                         emptyLabel: "as equipped",
                         titleSuffix: "DISGUISE",
                         wornLabel: "shown");
        }

        /// <summary>
        /// A WEAPON may only be disguised as another weapon of its own class.
        ///
        /// Not a taste rule - the class decides how the thing is HELD. `TwoHanded` brings the off
        /// hand across onto the same hilt and permutes the layer stack (TwoHandedOrder); a disc
        /// uses DualWieldOrder and a one-handed grip, with the fist showing through the ring. Cross
        /// the two and the character either two-hands a chakram or holds a greatsword by nothing,
        /// because the grip comes from one item and the picture from another.
        ///
        /// Every other slot is unrestricted: a helmet is a helmet.
        /// </summary>
        List<GearItem> SameClassOnly(GearSlot slot, List<GearItem> options)
        {
            if (slot != GearSlot.Weapon) return options;

            var held = GearCatalog.Get(_profile().Gear.Get(GearSlot.Weapon));
            if (held == null) return options;

            var same = new List<GearItem>();
            foreach (var o in options)
                if (o.Class == held.Class && o.TwoHanded == held.TwoHanded) same.Add(o);
            return same;
        }

        /// <summary>Persist and repaint everything showing this character.</summary>
        void Commit()
        {
            _changed?.Invoke();
            Refresh();
        }

        public void Refresh()
        {
            if (!IsOpen || _profile == null) return;
            var p = _profile();

            foreach (var r in _rowRefresh) r();
            _preview.Refresh(p);

            if (_powerText != null)
                _powerText.text = $"GEAR POWER  {p.Gear.TotalPower():0}    -    "
                                + $"{p.Gear.WornCount()} / {GearSlots.Worn.Length} worn";
        }

        // ------------------------------------------------------------------ input

        /// <summary>How far the content may be dragged, 0 when it already fits.</summary>
        float ScrollMax => Mathf.Max(0f, _contentHeight - PaneH);

        void ApplyScroll()
        {
            _scroll = Mathf.Clamp(_scroll, 0f, ScrollMax);
            if (_content != null) _content.anchoredPosition = new Vector2(0f, _scroll);
        }

        /// <summary>
        /// Drag the page. Same tap-versus-drag test the mastery board uses and for the same
        /// reason: one finger has to do both jobs, so a press only becomes a scroll once it has
        /// travelled far enough that it cannot have been meant as a tap.
        /// </summary>
        void Scroll()
        {
            var at = Core.Controls.PointerPosition;
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(_pane, at, null);

            if (Core.Controls.Pinching) _scrolling = false;
            else if (Core.Controls.Tapped(out var down) && inside && ScrollMax > 0f)
            {
                _scrolling = true;
                _scrollOrigin = down;
                _scrollFrom = _scroll;
            }
            if (!Core.Controls.PointerHeld(out _)) _scrolling = false;
            if (!_scrolling) return;

            float dy = at.y - _scrollOrigin.y;
            if (dy * dy <= DragSlop) return;

            // The page follows the FINGER: drag up and the content goes up with it, which is what
            // every touch surface does and the opposite of what a scrollbar does. _scroll is how
            // far the content has been lifted, so it moves with dy rather than against it.
            //
            // Screen pixels to canvas units, or the page slides at a different rate on every
            // device - the same trap the mastery board's hit test had.
            float scale = _pane.lossyScale.y;
            _scroll = _scrollFrom + dy / Mathf.Max(0.0001f, scale);
            ApplyScroll();
        }

        /// <summary>Squared screen pixels a press may travel and still be a tap. See MasteryScreen.</summary>
        const float DragSlop = 64f;
        const float TapSlop = 400f;

        void Update()
        {
            if (!IsOpen) return;

            // The slot grid covers this screen and handles its own taps and its own Cancel.
            // Nothing here consumes input, so without standing down a card press would also hit
            // the row behind it and one Cancel would close the grid AND the screen under it.
            if (PickerOpen) return;

            if (Core.Controls.CancelTapped || Core.Controls.InteractTapped)
            {
                Close();
                return;
            }

            Scroll();

            _focusRects.Clear();
            foreach (var (rect, _) in _tabs) _focusRects.Add(rect);
            // EVERY row, scrolled or not - a row below the fold is still a choice, and the D-pad
            // landing on it scrolls it into the pane below.
            foreach (var h in _hits)
                if (h.Rect != null) _focusRects.Add(h.Rect);
            Core.Controls.SetFocusCandidates(_focusRects);

            var focused = Core.Controls.Focused;
            if (focused != null && _content != null && focused.IsChildOf(_content))
            {
                float d = UiKit.ScrollToShow(_pane, focused);
                if (d != 0f) { _scroll += d; ApplyScroll(); }
            }

            // COMMITTED ON RELEASE, not on press, and that is forced by the page scrolling.
            //
            // Everywhere else in this project a tap fires the moment the button goes down, which
            // is right for a screen where nothing moves. Here the same press is also the start of
            // a drag - so on the frame a scroll began, the hit test ran and picked whatever the
            // finger happened to land on. Every attempt to scroll the body page also changed the
            // character's hair.
            //
            // So the press is only remembered, and the pick happens on release if the finger did
            // not travel far enough to have meant a drag.
            if (Core.Controls.Tapped(out var down)) { _pressAt = down; _pressed = true; }
            if (!Core.Controls.PointerReleased || !_pressed) return;
            _pressed = false;

            var at = Core.Controls.PointerPosition;
            if ((at - _pressAt).sqrMagnitude > TapSlop) return;   // that was a drag

            foreach (var (rect, tab) in _tabs)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) continue;
                if (tab == _tab) return;
                _tab = tab;
                BuildPane();
                return;
            }

            // Clipped away is not the same as gone: a row scrolled past the top of the pane is
            // invisible but its RectTransform still contains the point, so without this a tap on
            // the tab strip would pick whatever happened to be hiding behind it.
            if (!RectTransformUtility.RectangleContainsScreenPoint(_pane, at, null)) return;

            foreach (var h in _hits)
            {
                if (h.Rect == null || !RectTransformUtility.RectangleContainsScreenPoint(h.Rect, at, null))
                    continue;
                h.OnClick?.Invoke();
                return;
            }
        }
    }
}
