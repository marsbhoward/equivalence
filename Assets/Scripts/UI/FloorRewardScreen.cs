using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>What a reward ring holds. Appended only - outcomes are logged by name.</summary>
    public enum FloorReward { Heal, Repair, Moveset, Xp, Calx }

    /// <summary>What the player ended up doing with the floor's reward.</summary>
    public enum FloorOutcome
    {
        Heal,
        Repair,
        Xp,
        MovesetInto,   // SlotIndex says where
        Forfeit,       // no longer reachable: backing out of a swap returns to the rings
        Calx,
    }

    /// <summary>
    /// The between-floors reward (mockup D on the Floor Reward canvas, 2026-10-12): FOUR RINGS in
    /// the sigil door's selector, at its compass points - two weapon arts (up, down), a restore
    /// (left) and Experience-or-Calx (right) - with the player's rotation in a column on the left
    /// and the highlighted ring's description in a matching column on the right.
    ///
    ///   Rewards   - a ring is highlighted (hover, pad focus, or a first tap on touch); taking it
    ///               (a click on the highlighted ring, its key again, Enter, or TAKE) is final.
    ///   Rotation  - a slot of the rotation is highlighted: its art's description opens beside the
    ///               column, the rings slide right and the reward description closes.
    ///   Compare   - a weapon art was taken: both descriptions come FORWARD side by side, the slot
    ///               being replaced against the art coming in, and any other slot can be picked to
    ///               compare instead. SWAP commits; BACK returns to the rings (the user's call -
    ///               comparing is a free look, the swap is the commitment).
    ///
    /// Curator's rerolls (<c>RunModifiers.ExtraRewardOptions</c>) replace all four rings.
    /// The rings are drawn texel by texel at <see cref="SigilRings.U"/> canvas units a texel;
    /// every layout number is the mockup's at 1.5x (1280x720 to the 1920x1080 canvas).
    /// </summary>
    public class FloorRewardScreen : MonoBehaviour
    {
        /// <summary>One ring.</summary>
        public class Option
        {
            public FloorReward Kind;
            public Moveset Art;        // the art a Moveset ring offers
            public bool Enabled;
            public string Value;       // the ring's line: an art's name, "+41 HP"
            public string Note;        // the description's state line: "96 → 137 / 137"
            public string Blurb;
        }

        enum Mode { Rewards, Rotation, Compare }

        public bool IsOpen { get; private set; }

        /// <summary>BACK means something only while comparing (it returns to the rings); the
        /// reward itself has to be answered, so the touch overlay leaves the button off otherwise.</summary>
        public bool CanDismiss => IsOpen && _mode == Mode.Compare;

        // ---- layout, canvas units, origin at the screen's centre
        const float RingsY = -60f;
        const float SideW = 285f, SideX = 679.5f, SideTop = 186f;
        const float WideW = 510f, Forward = 1.12f, ForwardTop = 246f;
        const float RowH = 84f, RowGap = 15f;

        // ---- the mockup's palette (the rotation wheel's bronze, the deal screen's cards)
        static readonly Color Veil = new(0.039f, 0.035f, 0.055f, 0.93f);
        static readonly Color FrameC = Hex(0x3b3746), FaceC = Hex(0x1c1a22), PanelFace = Hex(0x1f1c22);
        static readonly Color GoldC = Hex(0xffd95a), GiltC = Hex(0xc9a23a), BronzeLt = Hex(0xc8914f);
        static readonly Color NotchOn = Hex(0xf2cf8c), NotchOff = Hex(0x3d3040);
        static readonly Color Bright = Hex(0xf3ead8), Main = Hex(0xe6e8ef), Body = Hex(0xb8bfd1);
        static readonly Color Muted = Hex(0x80879a), Faint = Hex(0x6b7080), Prompt = Hex(0x9aa1b2);
        static readonly Color HealC = Hex(0xff8a9a), RepairC = Hex(0x9cc2ff), CalxC = Hex(0xe3a46a);
        static readonly Color OutC = Hex(0xd26b5e), OutText = Hex(0xf0a8a2), Divider = Hex(0x3b3746);

        // ---- state
        GameObject _root;
        List<Option> _options = new();
        IReadOnlyList<Moveset> _slots;
        Func<int, bool> _slotLocked;
        int _rotationIndex;
        ElementType _element;
        Func<List<Option>> _reroll;
        int _rerollsLeft;
        Action<FloorOutcome, int, Moveset> _done;
        Mode _mode;
        int _focus, _slot, _touchRing = -1, _openedFrame;
        Moveset _art;   // the art being compared in

        // ---- built pieces (not readonly - see CLAUDE.md's domain reload traps)
        RectTransform _rings, _rotPanel, _rewPanel, _rotFace, _rewFace, _compareBar, _arrow;
        CanvasGroup _ringsGroup, _rotGroup, _rewGroup, _compareGroup, _arrowGroup;
        Image _rotFrame, _rewFrame;
        Texture2D _ringTex;
        Color32[] _px;
        Text _prompt, _rerollLabel, _footer, _swapLabel;
        RectTransform _takeRect, _swapRect, _backRect, _rerollRect;
        List<RectTransform> _cells = new();
        List<SlotRow> _rows = new();
        List<RectTransform> _focusRects = new();
        string _builtRot, _builtRew, _drawnRings;

        class SlotRow
        {
            public RectTransform Rect;
            public Image Frame;
            public Text Sub;
            public bool Locked;
        }

        // ================================================================== open

        public void Show(Transform canvas, int floor, IReadOnlyList<Option> options, IReadOnlyList<Moveset> slots,
                         Func<int, bool> slotLocked, int rotationIndex, ElementType element,
                         Func<List<Option>> reroll, int rerolls, Action<FloorOutcome, int, Moveset> done)
        {
            if (IsOpen) return;
            IsOpen = true;
            _options = new List<Option>(options);
            _slots = slots;
            _slotLocked = slotLocked ?? (_ => false);
            _rotationIndex = rotationIndex;
            _element = element;
            _reroll = reroll;
            _rerollsLeft = reroll != null ? Mathf.Max(0, rerolls) : 0;
            _done = done;
            _mode = Mode.Rewards;
            _focus = FirstEnabled();
            _slot = FirstUnlocked();
            _touchRing = -1;
            _art = null;
            _openedFrame = Time.frameCount;
            _builtRot = _builtRew = _drawnRings = null;

            Core.GamePause.Hold(this);

            _root = new GameObject("FloorReward", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;
            // A veil, not a wall: the cleared room stays dimly behind the choice.
            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Veil);

            BuildHeader(full, floor);
            BuildRotation(full);
            BuildRings(full);
            BuildPanels(full);
            BuildCompareBar(full);
            BuildFooter(full);

            RefreshPanels();
            RefreshRows();
            DrawRings();
            Animate(1f);
        }

        int FirstEnabled()
        {
            for (int i = 0; i < _options.Count; i++) if (_options[i].Enabled) return i;
            return 0;
        }

        int FirstUnlocked()
        {
            int n = _slots?.Count ?? 0;
            for (int j = 0; j < n; j++) if (!_slotLocked(j)) return j;
            return 0;
        }

        // ================================================================== building

        void BuildHeader(RectTransform full, int floor)
        {
            var top = new Vector2(0, 1);
            var one = new Vector2(1, 1);
            var tag = UiKit.Label(UiKit.Rect(full, "floor", top, one, new Vector2(0, -56), new Vector2(0, -30)),
                $"F L O O R   {floor}", 20, BronzeLt, TextAnchor.MiddleCenter);
            tag.fontStyle = FontStyle.Bold;
            var title = UiKit.Label(UiKit.Rect(full, "title", top, one, new Vector2(0, -128), new Vector2(0, -58)),
                "CLEARED", 60, Bright, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            Shadow(title, 3f);
            _prompt = UiKit.Label(UiKit.Rect(full, "prompt", top, one, new Vector2(0, -164), new Vector2(0, -132)),
                "", 22, Prompt, TextAnchor.MiddleCenter);
        }

        void BuildRotation(RectTransform full)
        {
            var col = Centred(full, "rotation", new Vector2(-SideX, SideTop), new Vector2(SideW, 600f));
            var tag = UiKit.Label(UiKit.Rect(col, "tag", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -22), Vector2.zero),
                "YOUR ROTATION", 16, Muted);
            tag.fontStyle = FontStyle.Bold;

            _rows.Clear();
            int n = _slots?.Count ?? 0;
            for (int j = 0; j < n; j++)
            {
                var m = _slots[j] ?? MovesetLibrary.Default;
                float y = -34f - j * (RowH + RowGap);
                var rt = UiKit.Rect(col, $"slot{j}", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(0, y - RowH), new Vector2(0, y));
                var frame = Notched(rt, FrameC);
                var face = UiKit.Rect(rt, "face", Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
                Notched(face, FaceC);

                var g = Img(face, "glyph", Glyphs.Get(m.FinisherGlyph),
                    j == _rotationIndex ? GoldC : Hex(0xd8d2c4));
                Place(g.rectTransform, new Vector2(0, 0.5f), new Vector2(14, 6), new Vector2(36, 33));

                var name = UiKit.Label(UiKit.Rect(face, "name", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(60, -40), new Vector2(-48, -8)), m.DisplayName, 22, Main);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                Notches(face, RotationWheel.WeightOf(m), new Vector2(1, 1), new Vector2(-40, -24), 5f, 14f, 3f);
                var sub = UiKit.Label(UiKit.Rect(face, "sub", new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(60, 6), new Vector2(-10, 30)), "", 13, Faint);
                sub.fontStyle = FontStyle.Bold;

                _rows.Add(new SlotRow { Rect = rt, Frame = frame, Sub = sub, Locked = _slotLocked(j) });
            }

            float noteY = -34f - n * (RowH + RowGap) - 4f;
            UiKit.Label(UiKit.Rect(col, "note", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, noteY - 28), new Vector2(0, noteY)), "Reorder at the door ahead.", 18, Faint);
        }

        void BuildRings(RectTransform full)
        {
            _rings = UiKit.Rect(full, "rings", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _rings.sizeDelta = new Vector2(SigilRings.Size, SigilRings.Size);
            _rings.anchoredPosition = new Vector2(0, RingsY);
            _ringsGroup = _rings.gameObject.AddComponent<CanvasGroup>();
            _ringTex = SigilRings.Build(_rings, "reward.rings");

            BuildCells();

            if (_rerollsLeft > 0)
            {
                _rerollRect = Centred(full, "reroll", new Vector2(SideX, -330f), new Vector2(SideW, 66f));
                Notched(_rerollRect, GiltC);
                var face = UiKit.Rect(_rerollRect, "face", Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
                Notched(face, Hex(0x2a2414));
                _rerollLabel = UiKit.Label(face, "", 17, GoldC, TextAnchor.MiddleCenter);
                _rerollLabel.fontStyle = FontStyle.Bold;
            }
        }

        /// <summary>The four rings' contents - rebuilt whole by a reroll.</summary>
        void BuildCells()
        {
            foreach (var c in _cells) if (c != null) Destroy(c.gameObject);
            _cells.Clear();
            for (int i = 0; i < _options.Count && i < 4; i++)
            {
                var o = _options[i];
                var cell = SigilRings.Cell(_rings, i);
                var colour = Colour(o, i);
                var top = new Vector2(0, 1);
                var one = new Vector2(1, 1);

                var tag = UiKit.Label(UiKit.Rect(cell, "tag", top, one, new Vector2(0, -22), Vector2.zero),
                    TagOf(o), 15, colour, TextAnchor.MiddleCenter);
                tag.fontStyle = FontStyle.Bold;

                var icon = Img(cell, "icon", IconOf(o), IconTint(o, colour));
                Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(54, 48));

                var name = UiKit.Label(UiKit.Rect(cell, "name", top, one, new Vector2(-20, -114), new Vector2(20, -78)),
                    o.Value ?? "", 28, i == 0 ? Bright : Hex(0xc8ccd6), TextAnchor.MiddleCenter);
                name.fontStyle = FontStyle.Bold;

                if (o.Kind == FloorReward.Moveset && o.Art != null)
                {
                    int w = RotationWheel.WeightOf(o.Art);
                    Notches(cell, w, new Vector2(0.5f, 1), new Vector2(-44, -126), 6f, 15f, 4f);
                    var wt = UiKit.Label(UiKit.Rect(cell, "weight", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                        new Vector2(-10, -136), new Vector2(80, -116)), WeightName(w), 13, BronzeLt);
                    wt.fontStyle = FontStyle.Bold;
                }

                var arrow = Img(cell, "pad", SigilRings.PadArrow, colour);
                Place(arrow.rectTransform, new Vector2(0.5f, 0), new Vector2(-12, 16), new Vector2(15, 9));
                arrow.rectTransform.localEulerAngles = new Vector3(0, 0, SigilRings.ArrowTurn[i]);
                var key = UiKit.Label(UiKit.Rect(cell, "key", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(2, 4), new Vector2(40, 30)), (i + 1).ToString(), 18, colour);
                key.fontStyle = FontStyle.Bold;

                if (!o.Enabled)
                    foreach (var g in cell.GetComponentsInChildren<Graphic>()) g.color *= new Color(1, 1, 1, 0.35f);
                _cells.Add(cell);
            }
            _drawnRings = null;
        }

        void BuildPanels(RectTransform full)
        {
            (_rotPanel, _rotGroup, _rotFrame, _rotFace) = Panel(full, "rotation-detail");
            (_rewPanel, _rewGroup, _rewFrame, _rewFace) = Panel(full, "reward-detail");
        }

        (RectTransform, CanvasGroup, Image, RectTransform) Panel(RectTransform full, string name)
        {
            var rt = Centred(full, name, new Vector2(0, SideTop), new Vector2(SideW, 100f));
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            var frame = Notched(rt, FrameC);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(5, 5, 5, 5);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var face = UiKit.Rect(rt, "face", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Notched(face, PanelFace);
            var fv = face.gameObject.AddComponent<VerticalLayoutGroup>();
            fv.padding = new RectOffset(20, 20, 20, 18);
            fv.spacing = 10f;
            fv.childControlWidth = fv.childControlHeight = true;
            fv.childForceExpandWidth = true;
            fv.childForceExpandHeight = false;
            return (rt, group, frame, face);
        }

        void BuildCompareBar(RectTransform full)
        {
            _arrow = Centred(full, "arrow", new Vector2(60f, 90f), new Vector2(54f, 54f));
            _arrowGroup = _arrow.gameObject.AddComponent<CanvasGroup>();
            var a = UiKit.Label(_arrow, "→", 46, GoldC, TextAnchor.MiddleCenter);
            Shadow(a, 2f);

            _compareBar = Centred(full, "compare", new Vector2(60f, -390f), new Vector2(1000f, 70f));
            _compareGroup = _compareBar.gameObject.AddComponent<CanvasGroup>();
            (_swapRect, _swapLabel) = Button(_compareBar, "swap", new Vector2(-240, -35), 440f,
                Hex(0x6ea86a), Hex(0x16211a), Hex(0xa6e0a0));
            Text back;
            (_backRect, back) = Button(_compareBar, "back", new Vector2(230, -35), 400f,
                Hex(0x6b6577), Hex(0x1c1a22), Hex(0xc8ccd6));
            back.text = "BACK  ·  KEEP LOOKING";
        }

        void BuildFooter(RectTransform full)
        {
            var rt = UiKit.Rect(full, "footer", Vector2.zero, new Vector2(1, 0), new Vector2(0, 14), new Vector2(0, 44));
            _footer = UiKit.Label(rt, "", 16, Faint, TextAnchor.MiddleCenter);
            _footer.fontStyle = FontStyle.Bold;
        }

        static (RectTransform, Text) Button(RectTransform parent, string name, Vector2 at, float w,
                                            Color frame, Color face, Color text)
        {
            var rt = UiKit.Rect(parent, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                at - new Vector2(w * 0.5f, 33f), at + new Vector2(w * 0.5f, 33f));
            Notched(rt, frame);
            var f = UiKit.Rect(rt, "face", Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            Notched(f, face);
            var l = UiKit.Label(f, "", 18, text, TextAnchor.MiddleCenter);
            l.fontStyle = FontStyle.Bold;
            return (rt, l);
        }

        // ================================================================== panel contents

        void RefreshPanels()
        {
            bool compare = _mode == Mode.Compare;

            string rotKey = $"{compare}|{_slot}";
            if (rotKey != _builtRot && _slots != null && _slot < _slots.Count)
            {
                _builtRot = rotKey;
                Clear(_rotFace);
                var m = _slots[_slot] ?? MovesetLibrary.Default;
                bool locked = _slotLocked(_slot);
                string tag = compare ? $"OUT  ·  SLOT {_slot + 1}"
                           : $"SLOT {_slot + 1}" + (locked ? "  ·  SIGNATURE" : _slot == _rotationIndex ? "  ·  UP NEXT" : "");
                UiKit.SetColor(_rotFrame, compare ? OutC : BronzeLt);
                Tag(_rotFace, tag, compare ? OutText : BronzeLt);
                Heading(_rotFace, Glyphs.Get(m.FinisherGlyph), Hex(0xd8d2c4), m.DisplayName);
                WeightRow(_rotFace, RotationWheel.WeightOf(m));
                Para(_rotFace, m.FinisherDescription, 20, Body);
                Rule(_rotFace);
                Para(_rotFace, compare ? "Pick another slot on the left to compare it instead."
                                       : locked ? "This weapon's signature art. It cannot be replaced."
                                       : "Your art. Pick a reward ring to go back.", 17, Muted);
            }

            var o = compare ? null : (_focus < _options.Count ? _options[_focus] : null);
            string rewKey = compare ? $"in|{_art?.Id}" : $"{_focus}|{o?.Kind}|{o?.Value}";
            if (rewKey == _builtRew) return;
            _builtRew = rewKey;
            Clear(_rewFace);
            _takeRect = null;

            if (compare && _art != null)
            {
                UiKit.SetColor(_rewFrame, GoldC);
                Tag(_rewFace, "IN  ·  WEAPON ART", GoldC);
                Heading(_rewFace, Glyphs.Get(_art.FinisherGlyph), GoldC, _art.DisplayName);
                WeightRow(_rewFace, RotationWheel.WeightOf(_art));
                Para(_rewFace, _art.FinisherDescription, 20, Body);
                return;
            }
            if (o == null) return;

            var colour = Colour(o, _focus);
            UiKit.SetColor(_rewFrame, o.Enabled ? colour : FrameC);
            Tag(_rewFace, TagOf(o), colour);
            Heading(_rewFace, IconOf(o), IconTint(o, colour), o.Value ?? "");
            if (o.Kind == FloorReward.Moveset && o.Art != null) WeightRow(_rewFace, RotationWheel.WeightOf(o.Art));
            if (!string.IsNullOrEmpty(o.Note)) Para(_rewFace, o.Note, 15, Muted, bold: true);
            Para(_rewFace, o.Blurb ?? "", 20, Body);
            if (!o.Enabled) return;

            Rule(_rewFace);
            var holder = UiKit.Rect(_rewFace, "take", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            holder.gameObject.AddComponent<LayoutElement>().preferredHeight = 62f;
            Notched(holder, colour);
            var f = UiKit.Rect(holder, "face", Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3));
            Notched(f, Hex(0x2a2414));
            var l = UiKit.Label(f, o.Kind == FloorReward.Moveset ? "TAKE  ·  PICK A SLOT" : $"TAKE  [ {_focus + 1} ]",
                17, colour, TextAnchor.MiddleCenter);
            l.fontStyle = FontStyle.Bold;
            _takeRect = holder;
        }

        static void Clear(RectTransform face)
        {
            for (int i = face.childCount - 1; i >= 0; i--)
            {
                var c = face.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
        }

        static void Tag(RectTransform face, string text, Color c)
        {
            var t = UiKit.Label(face, text, 16, c);
            t.fontStyle = FontStyle.Bold;
        }

        static void Heading(RectTransform face, Sprite icon, Color iconTint, string name)
        {
            var row = UiKit.Rect(face, "head", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 14f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            var img = Img(row, "icon", icon, iconTint);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = 46f;
            le.preferredHeight = le.minHeight = 42f;
            var t = UiKit.Label(row, name, 32, Bright);
            t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        static void WeightRow(RectTransform face, int weight)
        {
            if (weight <= 0) return;
            var row = UiKit.Rect(face, "weight", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;
            Notches(row, weight, new Vector2(0, 0.5f), Vector2.zero, 7f, 18f, 5f);
            var t = UiKit.Label(UiKit.Rect(row, "name", Vector2.zero, Vector2.one, new Vector2(46, 0), Vector2.zero),
                WeightName(weight), 15, BronzeLt, TextAnchor.MiddleLeft);
            t.fontStyle = FontStyle.Bold;
        }

        static void Para(RectTransform face, string text, int size, Color c, bool bold = false)
        {
            var t = UiKit.Label(face, text, size, c);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.lineSpacing = 1.1f;
            if (bold) t.fontStyle = FontStyle.Bold;
        }

        static void Rule(RectTransform face)
        {
            var r = UiKit.Rect(face, "rule", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            r.gameObject.AddComponent<Image>().color = Divider;
            r.gameObject.AddComponent<LayoutElement>().preferredHeight = 3f;
        }

        // ================================================================== input

        void Update()
        {
            if (!IsOpen) return;

            RegisterFocus();
            if (Time.frameCount != _openedFrame && HandleInput()) return;
            if (!IsOpen) return;

            FollowHighlight();
            RefreshPanels();
            RefreshRows();
            DrawRings();
            Animate(1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));
        }

        void RegisterFocus()
        {
            _focusRects.Clear();
            if (_mode == Mode.Compare)
            {
                for (int j = 0; j < _rows.Count; j++) if (!_rows[j].Locked) _focusRects.Add(_rows[j].Rect);
                if (_swapRect != null) _focusRects.Add(_swapRect);
                if (_backRect != null) _focusRects.Add(_backRect);
            }
            else
            {
                // The rings first, so a pad opens on the top one.
                for (int i = 0; i < _cells.Count; i++) if (_options[i].Enabled) _focusRects.Add(_cells[i]);
                foreach (var r in _rows) _focusRects.Add(r.Rect);
                if (_mode == Mode.Rewards && _takeRect != null) _focusRects.Add(_takeRect);
                if (_rerollRect != null && _rerollsLeft > 0) _focusRects.Add(_rerollRect);
            }
            Core.Controls.SetFocusCandidates(_focusRects);
        }

        /// <summary>The pad's focus or the mouse's hover moves the highlight; touch has none
        /// (its first tap highlights). Only taps and keys commit.</summary>
        void FollowHighlight()
        {
            if (Core.Controls.TouchMode) return;
            int ring, row;
            if (Core.Controls.GamepadMode)
            {
                var f = Core.Controls.Focused;
                ring = _cells.IndexOf(f);
                row = _rows.FindIndex(r => r.Rect == f);
            }
            else
            {
                var at = Core.Controls.PointerPosition;
                ring = _cells.FindIndex(c => Hit(c, at));
                row = _rows.FindIndex(r => Hit(r.Rect, at));
            }

            if (_mode == Mode.Compare)
            {
                if (row >= 0 && !_rows[row].Locked) _slot = row;
                return;
            }
            if (ring >= 0 && _options[ring].Enabled) { _mode = Mode.Rewards; _focus = ring; }
            else if (row >= 0) { _mode = Mode.Rotation; _slot = row; }
        }

        /// <summary>True when the screen closed.</summary>
        bool HandleInput()
        {
            if (_mode == Mode.Compare)
            {
                for (int j = 0; j < _rows.Count; j++)
                    if (Core.Controls.DigitTapped(j) && !_rows[j].Locked) _slot = j;
                if (Core.Controls.ConfirmTapped) { Finish(FloorOutcome.MovesetInto, _slot, _art); return true; }
                if (Core.Controls.CancelTapped) { Core.Controls.ConsumeCancel(); Back(); return false; }

                if (!Core.Controls.Tapped(out var p)) return false;
                for (int j = 0; j < _rows.Count; j++)
                    if (Hit(_rows[j].Rect, p)) { if (!_rows[j].Locked) _slot = j; return false; }
                if (Hit(_swapRect, p)) { Finish(FloorOutcome.MovesetInto, _slot, _art); return true; }
                if (Hit(_backRect, p)) Back();
                return false;
            }

            for (int i = 0; i < _cells.Count; i++)
            {
                if (!Core.Controls.DigitTapped(i) || !_options[i].Enabled) continue;
                if (_mode == Mode.Rewards && _focus == i) return Take(i);
                _mode = Mode.Rewards; _focus = i; _touchRing = i;
            }
            if (Core.Controls.ConfirmTapped && _mode == Mode.Rewards) return Take(_focus);
            if (Core.Controls.AltTapped) Reroll();

            if (!Core.Controls.Tapped(out var at)) return false;
            for (int i = 0; i < _cells.Count; i++)
            {
                if (!Hit(_cells[i], at) || !_options[i].Enabled) continue;
                // Touch has no hover: the first tap READS a ring, the second takes it.
                bool armed = !Core.Controls.TouchMode || _touchRing == i;
                if (_mode == Mode.Rewards && _focus == i && armed) return Take(i);
                _mode = Mode.Rewards; _focus = i; _touchRing = i;
                return false;
            }
            for (int j = 0; j < _rows.Count; j++)
                if (Hit(_rows[j].Rect, at)) { _mode = Mode.Rotation; _slot = j; _touchRing = -1; return false; }
            if (_mode == Mode.Rewards && Hit(_takeRect, at)) return Take(_focus);
            if (Hit(_rerollRect, at)) Reroll();
            return false;
        }

        bool Take(int i)
        {
            if (i < 0 || i >= _options.Count) return false;
            var o = _options[i];
            if (!o.Enabled) return false;
            switch (o.Kind)
            {
                case FloorReward.Moveset:
                    _art = o.Art;
                    _mode = Mode.Compare;
                    _slot = FirstUnlocked();
                    return false;
                case FloorReward.Heal:   Finish(FloorOutcome.Heal, -1, null); return true;
                case FloorReward.Repair: Finish(FloorOutcome.Repair, -1, null); return true;
                case FloorReward.Calx:   Finish(FloorOutcome.Calx, -1, null); return true;
                default:                 Finish(FloorOutcome.Xp, -1, null); return true;
            }
        }

        void Back()
        {
            _mode = Mode.Rewards;
            _art = null;
            _touchRing = -1;
        }

        void Reroll()
        {
            if (_rerollsLeft <= 0 || _reroll == null || _mode != Mode.Rewards && _mode != Mode.Rotation) return;
            _rerollsLeft--;
            _options = _reroll() ?? _options;
            _focus = FirstEnabled();
            _mode = Mode.Rewards;
            _touchRing = -1;
            _builtRew = null;
            BuildCells();
        }

        void Finish(FloorOutcome outcome, int slot, Moveset art)
        {
            var done = _done;
            Close();
            done?.Invoke(outcome, slot, art);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Core.GamePause.Release(this);
            if (_root) Destroy(_root);
            if (_ringTex) Destroy(_ringTex);
            _root = null;
            _ringTex = null;
            _cells?.Clear();
            _rows?.Clear();
            _takeRect = _swapRect = _backRect = _rerollRect = null;
        }

        static bool Hit(RectTransform rt, Vector2 screenPoint)
            => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, null);

        // ================================================================== every frame

        void RefreshRows()
        {
            bool compare = _mode == Mode.Compare;
            for (int j = 0; j < _rows.Count; j++)
            {
                var r = _rows[j];
                bool sel = (compare || _mode == Mode.Rotation) && _slot == j;
                UiKit.SetColor(r.Frame, sel ? (compare ? OutC : BronzeLt) : FrameC);
                string sub = compare && sel ? $"REPLACED BY {_art?.DisplayName.ToUpperInvariant()}"
                           : r.Locked ? $"SLOT {j + 1}  ·  SIGNATURE"
                           : j == _rotationIndex ? $"SLOT {j + 1}  ·  UP NEXT" : $"SLOT {j + 1}";
                UiKit.SetText(r.Sub, sub);
                UiKit.SetColor(r.Sub, compare && sel ? OutText : j == _rotationIndex ? GoldC : Faint);
            }

            UiKit.SetText(_prompt, _mode == Mode.Compare ? $"Which art does {_art?.DisplayName} replace?"
                                 : _mode == Mode.Rotation ? "Your rotation, as it stands."
                                 : "Choose one. Your pick is final.");
            if (_swapLabel != null) UiKit.SetText(_swapLabel, $"[ENTER]  SWAP INTO SLOT {_slot + 1}");
            if (_rerollLabel != null)
            {
                UiKit.SetText(_rerollLabel, _rerollsLeft > 0 ? $"[Q]  REROLL  ·  {_rerollsLeft} LEFT" : "NO REROLLS LEFT");
                UiKit.SetColor(_rerollLabel, _rerollsLeft > 0 ? GoldC : Faint);
            }
            UiKit.SetText(_footer, _mode == Mode.Compare
                ? $"[ 1 - {_rows.Count} ]  PICK A SLOT    ·    [ ENTER ]  SWAP    ·    [ ESC ]  BACK"
                : "[ 1 - 4 ]  OR  D-PAD  CHOOSE    ·    ARTS CAN BE REORDERED AT THE DOOR");
        }

        /// <summary>Eases every moving piece toward where the mode puts it; k = 1 snaps.</summary>
        void Animate(float k)
        {
            bool compare = _mode == Mode.Compare, rotation = _mode == Mode.Rotation;

            Move(_rings, _ringsGroup, new Vector2(rotation ? 495f : 0f, RingsY), 0f,
                 compare ? 0.86f : 1f, compare ? 0.12f : 1f, k, keepWidth: true);
            Move(_rotPanel, _rotGroup,
                 compare ? new Vector2(-240f, ForwardTop) : new Vector2(rotation ? -252f : -297f, SideTop),
                 WideW, compare ? Forward : 1f, compare || rotation ? 1f : 0f, k);
            Move(_rewPanel, _rewGroup,
                 compare ? new Vector2(360f, ForwardTop) : new Vector2(rotation ? SideX + 45f : SideX, SideTop),
                 compare ? WideW : SideW, compare ? Forward : 1f, rotation ? 0f : 1f, k);
            Move(_arrow, _arrowGroup, new Vector2(60f, 90f), 0f, 1f, compare ? 1f : 0f, k, keepWidth: true);
            Move(_compareBar, _compareGroup, new Vector2(60f, -390f), 0f, 1f, compare ? 1f : 0f, k, keepWidth: true);
            if (_rerollRect != null) _rerollRect.gameObject.SetActive(_mode == Mode.Rewards && _rerollsLeft > 0);
        }

        static void Move(RectTransform rt, CanvasGroup g, Vector2 at, float width, float scale, float alpha,
                         float k, bool keepWidth = false)
        {
            if (rt == null) return;
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, at, k);
            if (!keepWidth) rt.sizeDelta = new Vector2(Mathf.Lerp(rt.sizeDelta.x, width, k), rt.sizeDelta.y);
            float s = Mathf.Lerp(rt.localScale.x, scale, k);
            rt.localScale = new Vector3(s, s, 1f);
            if (g != null) g.alpha = Mathf.Lerp(g.alpha, alpha, k);
        }

        // ================================================================== the rings

        void DrawRings()
        {
            string key = $"{_mode}|{_focus}|{_options.Count}|{_cells.Count}";
            if (key == _drawnRings || _ringTex == null) return;
            _drawnRings = key;
            SigilRings.Draw(_ringTex, ref _px, _mode == Mode.Rotation ? -1 : _focus, RingColour, _ => SigilRings.Lit);
        }

        Color32 RingColour(int i)
        {
            if (i >= _options.Count || !_options[i].Enabled) return SigilRings.Off;
            return _mode == Mode.Rotation ? SigilRings.Dim : SigilRings.Gold;
        }

        // ================================================================== what a ring shows

        static string TagOf(Option o) => o.Kind switch
        {
            FloorReward.Moveset => "WEAPON ART",
            FloorReward.Heal => "HEAL",
            FloorReward.Repair => "REPAIR",
            FloorReward.Calx => "CALX",
            _ => "EXPERIENCE",
        };

        Color Colour(Option o, int index) => o.Kind switch
        {
            FloorReward.Moveset => index == 0 ? GoldC : GiltC,
            FloorReward.Heal => HealC,
            FloorReward.Repair => RepairC,
            FloorReward.Calx => CalxC,
            _ => ElementInfo.Tint(_element),
        };

        Sprite IconOf(Option o) => o.Kind switch
        {
            FloorReward.Moveset => o.Art != null ? Glyphs.Get(o.Art.FinisherGlyph) : null,
            FloorReward.Heal => Heart,
            FloorReward.Repair => Shield,
            FloorReward.Calx => CalxPile,
            _ => Glyphs.Element(_element),
        };

        /// <summary>The pixel icons carry their own colours; glyphs take the ring's.</summary>
        static Color IconTint(Option o, Color colour)
            => o.Kind is FloorReward.Heal or FloorReward.Repair or FloorReward.Calx ? Color.white : colour;

        static string WeightName(int w) => w switch { 1 => "LIGHT", 2 => "MEDIUM", 3 => "HEAVY", _ => "" };

        // ================================================================== kit

        static Color Hex(int rgb) => new(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);

        /// <summary>A rect pivoted at its top middle, placed from the screen's centre.</summary>
        static RectTransform Centred(RectTransform parent, string name, Vector2 topMiddle, Vector2 size)
        {
            var rt = UiKit.Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = topMiddle;
            return rt;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 at, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(anchor.x == 0 ? 0f : 0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = at;
        }

        static Image Img(Transform parent, string name, Sprite sprite, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = sprite != null ? c : Color.clear;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>The mockups' pixel frame: a panel with one texel cut from each corner, sliced
        /// so the cut stays one mockup pixel (six canvas units) at any size.</summary>
        static Image Notched(RectTransform rt, Color c)
        {
            var img = rt.gameObject.GetComponent<Image>();
            if (img == null) img = rt.gameObject.AddComponent<Image>();
            img.sprite = NotchSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / 6f;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>1 Light, 2 Medium, 3 Heavy - the rotation wheel's notches.</summary>
        static void Notches(RectTransform parent, int weight, Vector2 anchor, Vector2 at, float w, float h, float gap)
        {
            if (weight <= 0) return;
            for (int n = 0; n < 3; n++)
            {
                var img = Img(parent, $"notch{n}", Pixel, n < weight ? NotchOn : NotchOff);
                img.preserveAspect = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = anchor;
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(w, h);
                rt.anchoredPosition = at + new Vector2(n * (w + gap), 0f);
            }
        }

        static void Shadow(Text t, float d)
        {
            var o = t.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            o.effectDistance = new Vector2(d, -d);
        }

        // ---- pixel sprites, made once

        static Sprite _notch, _pixel, _heart, _shield, _calx;
        static Dictionary<char, Color32> White => SigilRings.White;
        static Sprite Grid(string[] rows, Dictionary<char, Color32> palette, bool border = false)
            => SigilRings.Grid(rows, palette, border);

        static Sprite NotchSprite => _notch != null ? _notch : _notch = Grid(new[] { ".X.", "XXX", ".X." }, White, border: true);
        static Sprite Pixel => _pixel != null ? _pixel : _pixel = Grid(new[] { "X" }, White);

        static Sprite Heart => _heart != null ? _heart : _heart = Grid(new[]
        {
            ".ooo...ooo.",
            "orhro.orrro",
            "orhrrorrrro",
            "orrrrrrrrro",
            "orrrrrrrrro",
            ".orrrrrrro.",
            "..orrdrro..",
            "...orddo...",
            "....odo....",
            ".....o.....",
        }, new Dictionary<char, Color32>
        {
            ['o'] = new(0x0b, 0x0a, 0x0e, 255), ['r'] = new(0xd9, 0x40, 0x59, 255),
            ['d'] = new(0x8e, 0x23, 0x38, 255), ['h'] = new(0xff, 0x9a, 0xa8, 255),
        });

        static Sprite Shield => _shield != null ? _shield : _shield = Grid(new[]
        {
            "ooooooooo",
            "osllsssso",
            "osllsssdo",
            "osssssddo",
            "osssssddo",
            ".osssddo.",
            ".osssddo.",
            "..ossdo..",
            "...odo...",
            "....o....",
        }, new Dictionary<char, Color32>
        {
            ['o'] = new(0x0b, 0x0a, 0x0e, 255), ['s'] = new(0xa5, 0xaf, 0xbd, 255),
            ['l'] = new(0xd8, 0xdf, 0xe8, 255), ['d'] = new(0x59, 0x60, 0x6c, 255),
        });

        static Sprite CalxPile => _calx != null ? _calx : _calx = Grid(new[]
        {
            ".....11.....",
            "....2222....",
            "...2h2222...",
            "..31333333..",
            ".4444444444.",
            "553555555255",
            "555555555555",
        }, new Dictionary<char, Color32>
        {
            ['1'] = new(0xe3, 0xa4, 0x6a, 255), ['2'] = new(0xc8, 0x81, 0x4a, 255),
            ['3'] = new(0xa8, 0x68, 0x3a, 255), ['4'] = new(0x8f, 0x55, 0x30, 255),
            ['5'] = new(0x6b, 0x3d, 0x20, 255), ['h'] = new(0xf2, 0xcf, 0x8c, 255),
        });
    }
}
