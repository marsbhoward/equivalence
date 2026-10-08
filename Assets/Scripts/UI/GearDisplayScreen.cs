using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Art.Gear;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// Choose what stands in the armoury - the weapon rack's one peg and the armour stand's ten
    /// slots.
    ///
    /// USES THE SAME GRID AS THE EQUIP SCREEN, AND FOR THE SAME REASON IT WAS BUILT THERE. This
    /// used to dump every candidate item into one flat, unbounded grid at once - fourteen-odd
    /// weapons for the rack, or every armour item across all ten slots at once for the stand - with
    /// no tabs and no scrolling, so a catalogue of any real size ran the grid off the bottom of the
    /// screen with nothing to bring the rest of it back into view. <see cref="GearPicker"/> already
    /// solved exactly this for the loadout screen (see its own header), so the fix is to hand this
    /// screen's item choice to it rather than to keep a second, worse grid beside it.
    ///
    /// THE ARMOUR STAND IS THEREFORE A SLOT LIST FIRST, A PICKER SECOND - the same two-level shape
    /// TransmutationScreen already uses for the loadout tab: a small, always-visible list of the
    /// ten slots (segmented by slot, and SCROLLABLE the same way that screen's own body page is,
    /// since ten touch-sized rows do not fit one screen), and pressing a slot opens the identical
    /// per-slot grid the equip screen opens. The weapon rack has only one slot, so there is nothing
    /// for a list to be a list OF - its screen opens straight into the picker.
    ///
    /// THERE IS NO SLOT PICKER for WHERE a chosen piece goes, deliberately, exactly as before: an
    /// item already knows its own slot and a peg does not care which one it is. What changed is how
    /// a slot is EMPTIED - GearPicker's own EMPTY cell does that now, the same "first and always
    /// present" cell the loadout screen already trained the player on, rather than a second press
    /// on the same card. That is a real change of interaction from the old screen's "pick it again
    /// to take it down", made deliberately: the two screens now agree on what picking nothing means.
    /// </summary>
    public class GearDisplayScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        enum Kind { Weapons, Armour }
        Kind _kind;

        GameObject _root;
        RectTransform _full, _pane, _content;

        // ---- armour: one callback pair, keyed by slot ----
        Func<GearSlot, string> _slotShown;
        Action<GearSlot, string> _setSlotShown;

        // ---- weapons: one callback pair, there being only one slot ----
        Func<string> _weaponShown;
        Action<string> _setWeaponShown;

        /// <summary>The per-slot grid, shared with the loadout and transmutation screens. Created
        /// on demand and kept, so opening a second slot does not churn a component every press.</summary>
        GearPicker _picker;

        /// <summary>True while that grid is up. This screen stands down entirely then - nothing in
        /// this project consumes input, so without it a tap on a picker card would also land on the
        /// slot row underneath, and Cancel would close both at once.</summary>
        public bool PickerOpen => _picker != null && _picker.IsOpen;

        readonly List<(RectTransform Rect, GearSlot Slot)> _rows = new();
        readonly List<RectTransform> _focusRects = new();

        /// <summary>The frame this opened on. A modal opened by a press must ignore that press -
        /// see ConfirmDialog's own note - or the tap that opened the armoury also lands on whatever
        /// row or cell happens to sit under it.</summary>
        int _openedFrame = -1;

        // ---- scrolling, the identical drag-vs-tap idiom TransmutationScreen's body page uses ----
        float _scroll, _contentHeight;
        bool _scrolling;
        Vector2 _scrollOrigin;
        float _scrollFrom;
        Vector2 _pressAt;
        bool _pressed;

        const float PaneW = 1360f, PaneH = 620f;
        const float RowH = 96f, RowGap = 18f, ColGap = 24f;
        const int Cols = 2;
        const float DragSlop = 64f, TapSlop = 400f;

        /// <summary>The ten armour slots the mannequin wears - GearSlots.Worn minus the weapon,
        /// which the rack shows instead.</summary>
        static readonly GearSlot[] ArmourSlots =
            Array.FindAll(GearSlots.Worn, s => s != GearSlot.Weapon);

        // ------------------------------------------------------------------ open / close

        /// <summary>
        /// Open the rack. With a weapon on show this is a CLOSE-UP first - the one place in the
        /// hub the menu-density art can actually be seen (see <see cref="Inspect"/>) - and the
        /// grid is one press further in. An empty rack has nothing to look at, so it opens
        /// straight into the grid.
        /// </summary>
        public void OpenWeapons(Transform canvas, Func<string> shown, Action<string> setShown)
        {
            if (IsOpen) return;
            _kind = Kind.Weapons;
            _weaponShown = shown;
            _setWeaponShown = setShown;
            BeginOpen(canvas);

            if (ShownWeapon() != null) BuildInspect();
            else OpenWeaponPicker();
        }

        GearItem ShownWeapon()
        {
            var id = _weaponShown?.Invoke();
            return string.IsNullOrEmpty(id) ? null : GearCatalog.Get(id);
        }

        void OpenWeaponPicker()
        {
            ClearInspect();
            _picker ??= gameObject.AddComponent<GearPicker>();
            _picker.Open(_full, GearSlot.Weapon, null, _weaponShown?.Invoke(),
                         id => _setWeaponShown?.Invoke(id),
                         wornLabel: "on show");
        }

        // ------------------------------------------------------------------ inspect

        /// <summary>
        /// The rack's close-up: the weapon on show, drawn from its MENU art.
        ///
        /// The rack itself can't show that art - the hub gives a world unit about 150 screen
        /// pixels at 1080p, and menu art is 300 texels to the unit, so half of it would have
        /// nowhere to land. Here the art isn't tied to the world at all, so it is drawn at a
        /// WHOLE number of screen pixels per texel, as large as the page allows: at a fractional
        /// scale, point-sampled columns come out alternately k and k+1 pixels wide and a fuller
        /// or a bevel one texel across is exactly what goes uneven first.
        ///
        /// Upright, not point-down the way the rack hangs it - the flip is about hanging a blade
        /// on a hook, and a close-up is the weapon as it's held.
        /// </summary>
        RectTransform _inspect, _change;

        /// <summary>Canvas units kept clear above and below the art - the title block, and the CHANGE
        /// button with a margin (it tops out at 56 + TouchTarget = 184).</summary>
        const float InspectTop = 150f, InspectBottom = 220f;

        void BuildInspect()
        {
            ClearInspect();
            var item = ShownWeapon();
            if (item == null) return;
            var layer = Hub.GearDisplay.Represent(item, menu: true);

            _inspect = UiKit.Rect(_full, "inspect", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _openedFrame = Time.frameCount;

            var tint = CharacterScreen.TierColor(item.Tier);
            UiKit.Label(UiKit.Rect(_inspect, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -96), new Vector2(0, -40)),
                item.DisplayName.ToUpperInvariant(), 40, tint, TextAnchor.MiddleCenter);
            UiKit.Label(UiKit.Rect(_inspect, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -130), new Vector2(0, -96)),
                "THE WEAPON RACK   -   "
                + System.Text.RegularExpressions.Regex.Replace(item.Tier.ToString(), "(?<=[a-z])(?=[A-Z])", " ")
                    .ToUpperInvariant() + "   -   "
                + WeaponClasses.Name(item.Class).ToUpperInvariant(),
                16, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter);

            if (layer != null && layer.Sprite != null)
            {
                // A disc is a PAIR, for the reason WeaponRack gives - the twin behind and darker,
                // overlapping rather than beside it.
                bool pair = item.Class == WeaponClass.Disc;
                var r = layer.Sprite.rect;
                float scale = Mathf.Max(0.0001f, _full.lossyScale.y);
                float screenW = _full.rect.width * scale, screenH = _full.rect.height * scale;
                float roomW = screenW * 0.8f;
                float roomH = (_full.rect.height - InspectTop - InspectBottom) * scale;
                float hang = WeaponExtras.HangTexels(item, layer.Sprite);   // Lumen's live wire
                float spanW = pair ? r.width * 1.35f : r.width;
                float spanH = (pair ? r.height * 1.25f : r.height) + hang;
                int k = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(roomW / spanW, roomH / spanH)));

                // Placed in SCREEN pixels and rounded, then converted back to canvas units, so the
                // sprite's own corner sits on a pixel boundary - otherwise every texel edge lands
                // mid-pixel and the whole-number scale above buys nothing.
                float w = r.width * k, h = r.height * k;
                // Centred on the sword AND what hangs from it, so the wire has its room below.
                float midY = (InspectBottom * scale) + roomH * 0.5f + hang * k * 0.5f;
                Vector2 Corner(Vector2 shift) => new(
                    Mathf.Round(screenW * 0.5f - w * 0.5f + shift.x * k),
                    Mathf.Round(midY - h * 0.5f + shift.y * k));

                if (pair)
                {
                    var twin = Piece(Corner(new Vector2(r.width * 0.175f, -r.height * 0.125f)), w, h, scale,
                          (Hub.GearDisplay.RepresentOffhand(item, menu: true) ?? layer).Sprite,
                          new Color(0.72f, 0.72f, 0.72f, 1f));
                    KindledImage.On(twin, item);    // a kindled pair burns in BOTH halves (the Aether Dual Discs)
                }
                var at = pair ? new Vector2(-r.width * 0.175f, r.height * 0.125f) : Vector2.zero;
                var main = Piece(Corner(at), w, h, scale, layer.Sprite, layer.Tint);
                WeaponExtras.AddHalo(main, item, layer.Sprite);             // Saint's halo
                WeaponExtras.AddGemGlow(main, item, layer.Sprite);          // Prism's lit gem
                KindledImage.On(main, item);                                // the Secret Fire's marks
                if (hang > 0f)
                {
                    // The pommel's end, from the canvas centre, in canvas units.
                    var c = Corner(at);
                    var pommel = new Vector2(c.x + w * 0.5f - screenW * 0.5f, c.y - screenH * 0.5f) / scale;
                    WeaponExtras.AddHanging(_inspect, item, layer.Sprite, pommel, k / scale);
                }
            }

            _change = UiKit.Panel(_inspect, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-170, 56), new Vector2(170, 56 + UiKit.TouchTarget),
                new Color(0.12f, 0.13f, 0.18f, 1f));
            UiKit.Label(UiKit.Rect(_change, "c", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                "CHANGE", 20, new Color(0.93f, 0.82f, 0.52f), TextAnchor.MiddleCenter);

            UiKit.Hint(UiKit.Rect(_inspect, "h", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 16), new Vector2(0, 48)),
                "[E] change what hangs here    -    [ESC] to close",
                "tap CHANGE to swap it    -    BACK to close",
                18, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter);
        }

        Image Piece(Vector2 cornerPx, float wPx, float hPx, float scale, Sprite sprite, Color tint)
        {
            var rt = UiKit.Rect(_inspect, "piece", Vector2.zero, Vector2.zero,
                cornerPx / scale, (cornerPx + new Vector2(wPx, hPx)) / scale);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = tint;
            img.raycastTarget = false;
            return img;
        }

        void ClearInspect()
        {
            if (_inspect != null)
            {
                // Detached before the deferred Destroy, or it draws under the grid for a frame.
                _inspect.SetParent(null, false);
                _inspect.gameObject.SetActive(false);
                Destroy(_inspect.gameObject);
            }
            _inspect = _change = null;
        }

        void UpdateInspect()
        {
            // Whenever the grid closes - a pick or a cancel - come back to the close-up, now
            // showing whatever hangs there. Emptied, there is nothing to look at, so close.
            if (_inspect == null)
            {
                if (ShownWeapon() != null) BuildInspect();
                else Close();
                return;
            }

            if (Time.frameCount == _openedFrame) return;   // the press that opened this

            if (Controls.CancelTapped) { Close(); return; }

            _focusRects.Clear();
            _focusRects.Add(_change);
            Controls.SetFocusCandidates(_focusRects);

            if (Controls.InteractTapped || Controls.ConfirmTapped) { OpenWeaponPicker(); return; }
            if (Controls.Tapped(out var p)
                && RectTransformUtility.RectangleContainsScreenPoint(_change, p, null))
                OpenWeaponPicker();
        }

        /// <summary>Open the stand: the scrollable slot list, each row opening the shared picker.</summary>
        public void OpenArmour(Transform canvas, Func<GearSlot, string> shown,
                               Action<GearSlot, string> setShown)
        {
            if (IsOpen) return;
            _kind = Kind.Armour;
            _slotShown = shown;
            _setSlotShown = setShown;
            BeginOpen(canvas);

            UiKit.Label(UiKit.Rect(_full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -108), new Vector2(0, -48)),
                "THE ARMOUR STAND", 40, new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleCenter);

            UiKit.Hint(UiKit.Rect(_full, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -142), new Vector2(0, -108)),
                "pick a slot to dress it, pick EMPTY to strip it    -    [ESC] to close",
                "tap a slot to dress it, tap EMPTY to strip it    -    BACK to close",
                18, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter);

            BuildRows();
        }

        void BeginOpen(Transform canvas)
        {
            IsOpen = true;
            _openedFrame = Time.frameCount;
            GamePause.Hold(this);

            _root = new GameObject("GearDisplayScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            _full = (RectTransform)_root.transform;
            _full.anchorMin = Vector2.zero; _full.anchorMax = Vector2.one;
            _full.offsetMin = Vector2.zero; _full.offsetMax = Vector2.zero;

            // FULLY opaque - the hub is still live underneath, and its own room reads straight
            // through even a near-opaque panel, which looks like a rendering fault.
            UiKit.Panel(_full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.032f, 0.045f, 1f));
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (_picker != null && _picker.IsOpen) _picker.Close();
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _full = _pane = _content = null;
            _inspect = _change = null;
            _rows.Clear();
            _slotShown = null; _setSlotShown = null;
            _weaponShown = null; _setWeaponShown = null;
        }

        // ------------------------------------------------------------------ armour rows

        /// <summary>
        /// The ten-row list, two columns of five - the pane clips it and the content scrolls,
        /// exactly TransmutationScreen's RectMask2D-plus-drag idiom, because ten touch-sized rows
        /// (about 1150 units across two rows of five) do not fit inside one screen any more than
        /// that screen's own six body rows did.
        /// </summary>
        void BuildRows()
        {
            _rows.Clear();

            // Detached and hidden BEFORE Destroy, which is deferred to end of frame - left
            // parented, the outgoing list draws on top of the incoming one for a frame, the same
            // flicker TransmutationScreen's own BuildPane already documents and avoids this way.
            if (_content != null)
            {
                _content.SetParent(null, false);
                _content.gameObject.SetActive(false);
                Destroy(_content.gameObject);
            }
            if (_pane != null) Destroy(_pane.gameObject);

            _pane = UiKit.Rect(_full, "pane", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-PaneW * 0.5f, -PaneH * 0.5f - 40f),
                new Vector2(PaneW * 0.5f, PaneH * 0.5f - 40f));

            // RectMask2D rather than Mask - a rectangle clip with no stencil buffer and no extra
            // draw call, which is all a scroll pane here needs.
            _pane.gameObject.AddComponent<RectMask2D>();

            _content = UiKit.Rect(_pane, "content", new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, Vector2.zero);
            _content.pivot = new Vector2(0.5f, 1f);

            float colW = (PaneW - ColGap * (Cols - 1)) / Cols;

            for (int i = 0; i < ArmourSlots.Length; i++)
            {
                var slot = ArmourSlots[i];
                int col = i % Cols, row = i / Cols;
                float x = col * (colW + ColGap);
                float y = -row * (RowH + RowGap);

                var card = UiKit.Panel(_content, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(x, y - RowH), new Vector2(x + colW, y),
                    new Color(0.10f, 0.11f, 0.15f, 1f));

                var itemId = _slotShown?.Invoke(slot);
                var item = string.IsNullOrEmpty(itemId) ? null : GearCatalog.Get(itemId);
                var tint = item != null ? CharacterScreen.TierColor(item.Tier)
                                        : new Color(0.18f, 0.19f, 0.24f);

                var swatch = UiKit.Panel(card, new Vector2(0, 0), new Vector2(0, 1),
                    Vector2.zero, new Vector2(8, 0), tint);
                swatch.GetComponent<Image>().raycastTarget = false;

                UiKit.Label(UiKit.Rect(card, "s", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(22, -32), new Vector2(-12, -8)),
                    slot.ToString().ToUpperInvariant(), 14,
                    new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleLeft);

                UiKit.Label(UiKit.Rect(card, "n", new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(22, 8), new Vector2(-12, 38)),
                    item != null ? item.DisplayName + "  (on show)" : "- empty -",
                    18,
                    item != null ? tint : new Color(0.42f, 0.45f, 0.52f),
                    TextAnchor.MiddleLeft);

                _rows.Add((card, slot));
            }

            // Measured off what was actually laid out rather than predicted, so a slot added to
            // the game later scrolls without anyone remembering to update a row count here.
            float lowest = 0f;
            for (int i = 0; i < _content.childCount; i++)
                lowest = Mathf.Min(lowest, ((RectTransform)_content.GetChild(i)).offsetMin.y);
            _contentHeight = Mathf.Max(PaneH, -lowest + 24f);
            _content.sizeDelta = new Vector2(0f, _contentHeight);
            _scroll = 0f;
            ApplyScroll();
        }

        /// <summary>
        /// Open the shared grid for one slot. An item already knows its own slot, so - as before -
        /// there is nothing to ask the player about beyond WHICH item; picking GearPicker's own
        /// empty cell strips the slot instead of a second press on an already-shown card.
        /// </summary>
        void OpenSlotPicker(GearSlot slot)
        {
            if (PickerOpen) return;
            _picker ??= gameObject.AddComponent<GearPicker>();
            var currentId = _slotShown?.Invoke(slot);
            _picker.Open(_full, slot, null, currentId,
                         id => { _setSlotShown?.Invoke(slot, id); BuildRows(); },
                         wornLabel: "on show");
        }

        // ------------------------------------------------------------------ input

        float ScrollMax => Mathf.Max(0f, _contentHeight - PaneH);

        void ApplyScroll()
        {
            _scroll = Mathf.Clamp(_scroll, 0f, ScrollMax);
            if (_content != null) _content.anchoredPosition = new Vector2(0f, _scroll);
        }

        /// <summary>Drag the row list. Same tap-versus-drag test the mastery board and the
        /// transmutation screen's body page both use, for the same reason: one finger has to do
        /// both jobs, so a press only becomes a scroll once it has travelled far enough that it
        /// cannot have been meant as a tap.</summary>
        void Scroll()
        {
            var at = Controls.PointerPosition;
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(_pane, at, null);

            if (Controls.Pinching) _scrolling = false;
            else if (Controls.Tapped(out var down) && inside && ScrollMax > 0f)
            {
                _scrolling = true;
                _scrollOrigin = down;
                _scrollFrom = _scroll;
            }
            if (!Controls.PointerHeld(out _)) _scrolling = false;
            if (!_scrolling) return;

            float dy = at.y - _scrollOrigin.y;
            if (dy * dy <= DragSlop) return;

            float scale = _pane.lossyScale.y;
            _scroll = _scrollFrom + dy / Mathf.Max(0.0001f, scale);
            ApplyScroll();
        }

        void Update()
        {
            if (!IsOpen) return;

            if (_kind == Kind.Weapons)
            {
                if (!PickerOpen) UpdateInspect();
                return;
            }

            // The slot grid covers this screen and handles its own taps and its own Cancel.
            // Nothing here consumes input, so without standing down a card press would also hit
            // the row behind it and one Cancel would close the grid AND the screen under it.
            if (PickerOpen) return;

            if (Time.frameCount == _openedFrame) return;   // the press that opened this

            if (Controls.CancelTapped) { Close(); return; }

            Scroll();

            _focusRects.Clear();
            foreach (var (rect, _) in _rows) _focusRects.Add(rect);
            Controls.SetFocusCandidates(_focusRects);

            // A row the D-pad lands on below the fold scrolls up into the pane.
            var focused = Controls.Focused;
            if (focused != null && _content != null && focused.IsChildOf(_content))
            {
                float d = UiKit.ScrollToShow(_pane, focused);
                if (d != 0f) { _scroll += d; ApplyScroll(); }
            }

            // COMMITTED ON RELEASE, not on press - forced by the list scrolling. Everywhere else
            // in this project a tap fires the moment the button goes down, which is right for a
            // screen where nothing moves; here the same press is also the start of a drag, so a
            // scroll's opening frame would otherwise also pick whatever row the finger landed on.
            if (Controls.Tapped(out var down)) { _pressAt = down; _pressed = true; }
            if (!Controls.PointerReleased || !_pressed) return;
            _pressed = false;

            var at = Controls.PointerPosition;
            if ((at - _pressAt).sqrMagnitude > TapSlop) return;   // that was a drag
            if (!RectTransformUtility.RectangleContainsScreenPoint(_pane, at, null)) return;

            foreach (var (rect, slot) in _rows)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) continue;
                OpenSlotPicker(slot);
                return;
            }
        }
    }
}
