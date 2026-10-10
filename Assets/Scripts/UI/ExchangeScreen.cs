using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;
using Convergence.Exchange;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.UI
{
    /// <summary>
    /// The equivalent-exchange row: paired offers with a refuse slate between them, shown BEFORE
    /// the floor reward and dissolving to reveal it.
    ///
    /// It comes first on purpose. The floor reward is a gift; this is a bargain, and a bargain
    /// read after you have already been handed something free is just a tax. Taking the debt while
    /// the reward is still hidden is what makes it a decision.
    ///
    /// Every slate is a SCALE (<see cref="ExchangeScale"/>): the boon in the left pan, the cost in
    /// the right, the beam tipped by their weights - so how good a trade is reads before a word
    /// of it is. Under the scale each half is a column: name, effect, stack, capstone, and the
    /// icon again at the foot, at the same height on every card (pattern recognition - the
    /// user's call). Designed on the mockup canvas, 2026-10-10.
    ///
    /// Taking a slate is played out rather than cut: the beam holds, the two icons lift out of
    /// the pans and fly into the ledger strip, the other slates dim, then the row dissolves.
    /// </summary>
    public class ExchangeScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        /// <summary>An offer that cannot be refused has no back button - see FloorRewardScreen.</summary>
        public bool CanDismiss => _offer != null && _offer.CanRefuse && _taking == null;

        // Card layout, in canvas units at the 1920 x 1080 reference.
        const float CardW = 420f, CardH = 640f, CardGap = 24f, CardTop = 360f;
        const float ColumnsTop = 296f, IconSize = ExchangeScale.IconTexels * 3f;

        static readonly Color Gold = new(0.86f, 0.72f, 0.38f);
        static readonly Color Body = new(0.72f, 0.75f, 0.82f);
        static readonly Color Muted = new(0.50f, 0.53f, 0.60f);
        static readonly Color Faint = new(0.36f, 0.38f, 0.45f);
        static readonly Color CardColor = new(0.085f, 0.09f, 0.12f, 1f);
        static readonly Color RefuseColor = new(0.055f, 0.06f, 0.08f, 1f);
        static readonly Color BronzeLit = new(0.78f, 0.57f, 0.31f);
        static readonly Color BronzeDark = new(0.56f, 0.36f, 0.18f);
        static readonly Color CitrineText = new(0.91f, 0.78f, 0.29f);

        GameObject _root;
        ExchangeOffer _offer;
        RunModifiers _mods;
        Action<ExchangePair> _onChosen;

        // Not readonly: a domain reload hands back readonly collections freshly initialised but
        // can leave these null - every use is guarded.
        List<Slate> _slates = new();
        RectTransform _refuseRect;
        Image _refuseVeil;
        ExchangeScale _refuseScale;
        List<RectTransform> _focusRects = new();
        CanvasGroup _fade;
        float _dissolve = -1f;
        ExchangePair _taken;

        /// <summary>The slate being taken, while its icons are in the air. Null otherwise.</summary>
        Slate _taking;
        float _takenAt;
        List<Flyer> _flyers = new();

        RectTransform _stripChips;
        Dictionary<string, RectTransform> _chips = new();
        float _stripEnd;

        /// <summary>Transmuter's Eye: what the floor reward behind this row will be.</summary>
        string _preview;

        /// <summary>Scrying Glass: what the floor below holds.</summary>
        string _scry;

        /// <summary>Ids on offer right now, so the ledger strip can light what matches.</summary>
        readonly HashSet<string> _onOffer = new();

        /// <summary>Oracle: the deal that would follow each slate, then refusing (null where it
        /// cannot). Null without Oracle.</summary>
        List<ExchangeOffer> _oracle;

        class Slate
        {
            public RectTransform Rect;
            public ExchangePair Pair;
            public ExchangeScale Scale;
            public Image Veil;
        }

        class Flyer
        {
            public RectTransform Rect;
            public RawImage Image;
            public Vector3 From, To;
            public float FromSize, ToSize;
            public RectTransform Chip;      // the chip it lands on; null when it starts a new one
            public bool Landed;
        }

        public void Show(Transform canvas, ExchangeOffer offer, RunModifiers mods, int floor,
                         string preview, Action<ExchangePair> onChosen, string scry = null,
                         List<ExchangeOffer> oracle = null)
        {
            if (IsOpen || offer == null || offer.Pairs.Count == 0) { onChosen?.Invoke(null); return; }

            IsOpen = true;
            _offer = offer;
            _mods = mods;
            _onChosen = onChosen;
            _preview = preview;
            _scry = scry;
            _oracle = oracle;
            _dissolve = -1f;
            _taken = null;
            _taking = null;
            GamePause.Hold(this);

            _onOffer.Clear();
            foreach (var p in offer.Pairs)
            {
                if (p.Boon != null) _onOffer.Add(p.Boon.Id);
                if (p.Cost != null) _onOffer.Add(p.Cost.Id);
            }

            Build(canvas, floor);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _slates?.Clear();
            _flyers?.Clear();
            _chips?.Clear();
            _refuseRect = null;
            _refuseVeil = null;
            _refuseScale = null;
            _taking = null;
            _fade = null;
        }

        // ------------------------------------------------------------------ build

        void Build(Transform canvas, int floor)
        {
            _slates ??= new List<Slate>();
            _flyers ??= new List<Flyer>();
            _chips ??= new Dictionary<string, RectTransform>();

            _root = new GameObject("ExchangeScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;
            _fade = _root.AddComponent<CanvasGroup>();

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.032f, 0.045f, 0.96f));

            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -132), new Vector2(0, -68)),
                "EQUIVALENT EXCHANGE", 40, Gold, TextAnchor.MiddleCenter);

            UiKit.Label(UiKit.Rect(full, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -166), new Vector2(0, -132)),
                _offer.CanRefuse
                    ? "nothing is given that is not also taken"
                    : _offer.Forced ? "INDENTURE - you must take one"
                    : "NO REFUSALS LEFT - you must take one",
                18, _offer.CanRefuse ? Muted : new Color(0.88f, 0.5f, 0.4f), TextAnchor.MiddleCenter);

            // Slates laid out with the refuse card in the MIDDLE of the row, same size as what it
            // refuses. Not a corner button: a player one bad cost from death should meet the way
            // out at the same moment they meet the trap.
            int n = _offer.Pairs.Count;
            int cards = n + (_offer.CanRefuse ? 1 : 0);
            float totalW = cards * CardW + (cards - 1) * CardGap;
            float x0 = -totalW * 0.5f;

            int refuseAt = _offer.CanRefuse ? n / 2 : -1;
            int pairIndex = 0;

            for (int slot = 0; slot < cards; slot++)
            {
                float x = x0 + slot * (CardW + CardGap);
                if (slot == refuseAt)
                {
                    BuildRefuse(full, x, slot);
                    if (_oracle != null && _oracle.Count > n) BuildOracle(full, x, _oracle[n]);
                    continue;
                }
                if (_oracle != null && pairIndex < _oracle.Count) BuildOracle(full, x, _oracle[pairIndex]);
                BuildSlate(full, x, slot, _offer.Pairs[pairIndex++]);
            }

            float below = CardTop - CardH;

            // The reward this row is covering, for anyone carrying Transmuter's Eye. Sits directly
            // under the slates, because it is information about the choice above it.
            if (!string.IsNullOrEmpty(_preview))
                UiKit.Label(UiKit.Rect(full, "peek", new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                    new Vector2(0, below - 72), new Vector2(0, below - 44)),
                    _preview, 17, Gold, TextAnchor.MiddleCenter);

            // Scrying Glass: the floor below, on the line under the reward peek - the same kind
            // of information (what is coming), read at the moment the next floor is being paid for.
            if (!string.IsNullOrEmpty(_scry))
                UiKit.Label(UiKit.Rect(full, "scry", new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                    new Vector2(0, below - 100), new Vector2(0, below - 72)),
                    _scry, 17, new Color(0.62f, 0.78f, 0.92f), TextAnchor.MiddleCenter);

            BuildStrip(full);

            UiKit.Label(UiKit.Rect(full, "f", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 34), new Vector2(0, 70)),
                _offer.CanRefuse ? "tap a slate    -    [1-3] choose    -    BACK or [ESC] refuse"
                                 : "click a slate    -    [1-3] choose",
                17, new Color(0.42f, 0.45f, 0.53f), TextAnchor.MiddleCenter);
        }

        RectTransform Card(RectTransform full, float x, Color color)
            => UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, CardTop - CardH), new Vector2(x + CardW, CardTop), color);

        /// <summary>The scale's top-left inside a card - centred, just under the top edge.</summary>
        static Vector2 ScaleAt => new((CardW - ExchangeScale.W * T.ScaleUnitsPerTexel) * 0.5f, -8f);

        /// <summary>Oracle: the deal that would follow this card, in a line under it.</summary>
        void BuildOracle(RectTransform full, float x, ExchangeOffer next)
        {
            string text;
            if (next == null || next.Pairs.Count == 0) text = "then: nothing";
            else
            {
                var parts = new List<string>();
                foreach (var p in next.Pairs) parts.Add($"{p.Boon?.Name ?? "-"} / {p.Cost?.Name ?? "-"}");
                text = "then: " + string.Join("  |  ", parts);
            }
            float below = CardTop - CardH;
            var label = UiKit.Label(UiKit.Rect(full, "oracle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, below - 44), new Vector2(x + CardW, below - 4)), text, 14,
                new Color(0.62f, 0.78f, 0.92f), TextAnchor.UpperCenter);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        void BuildSlate(RectTransform full, float x, int slot, ExchangePair pair)
        {
            var card = Card(full, x, CardColor);

            UiKit.Label(UiKit.Rect(card, "num", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(14, -34), new Vector2(60, -10)),
                (_slates.Count + 1).ToString(), 15, Faint);

            int boonHeld = pair.Boon != null && _mods != null ? _mods.StacksOf(pair.Boon) : 0;
            int costHeld = pair.Cost != null && _mods != null ? _mods.StacksOf(pair.Cost) : 0;

            var scale = ExchangeScale.Create(card, ScaleAt);
            scale.Show(pair.Boon, boonHeld, pair.Cost, costHeld, slot);

            // Why this slate is here, when it is not a plain draw - hung from the pan it is about:
            // a combination that just opened and the pity timer's return are the BOON's story, the
            // mercy pull (one stack from a Nigredo) is the COST's.
            switch (pair.Reason)
            {
                case SlateReason.NewCombination: scale.Hang(Tag(card, "NEW"), 0); break;
                case SlateReason.Pity:           scale.Hang(Tag(card, "RETURNING"), 0); break;
                case SlateReason.Mercy:          scale.Hang(Tag(card, "MERCY"), 1); break;
            }

            // The two columns, a rule between them.
            var rule = UiKit.Rect(card, "rule", new Vector2(0.5f, 0), new Vector2(0.5f, 1),
                new Vector2(0, 22), new Vector2(1, -ColumnsTop));
            rule.gameObject.AddComponent<Image>().color = new Color(0.17f, 0.18f, 0.23f);

            BuildHalf(card, pair.Boon, boonHeld, 0f, true);
            BuildHalf(card, pair.Cost, costHeld, 0.5f, false);

            _slates.Add(new Slate { Rect = card, Pair = pair, Scale = scale, Veil = Veil(card, CardColor) });
        }

        /// <summary>
        /// A cover in the card's own colour, clear until another slate is taken. The row steps
        /// back by being veiled rather than faded: a faded card is see-through, and the room
        /// behind the screen showed through it.
        /// </summary>
        static Image Veil(RectTransform card, Color color)
        {
            var veil = UiKit.Panel(card, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(color.r, color.g, color.b, 0f)).GetComponent<Image>();
            veil.raycastTarget = false;
            return veil;
        }

        /// <summary>A small bronze-framed label on a short stem, hung under a pan.</summary>
        static RectTransform Tag(RectTransform card, string text)
        {
            float w = 18f + text.Length * 11f;
            var tag = UiKit.Rect(card, "tag", new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero);
            tag.sizeDelta = new Vector2(w, 32);

            var stem = UiKit.Rect(tag, "stem", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-1, -8), new Vector2(1, 0));
            stem.gameObject.AddComponent<Image>().color = BronzeDark;

            var frame = UiKit.Panel(tag, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(0, -8), BronzeDark);
            UiKit.Panel(frame, Vector2.zero, Vector2.one, new Vector2(1, 1), new Vector2(-1, -1),
                new Color(0.11f, 0.10f, 0.08f));
            UiKit.Label(frame, text, 13, new Color(0.94f, 0.81f, 0.42f), TextAnchor.MiddleCenter);
            return tag;
        }

        void BuildHalf(RectTransform card, ExchangeEntry e, int held, float left, bool boon)
        {
            var col = UiKit.Rect(card, boon ? "boon" : "cost", new Vector2(left, 0), new Vector2(left + 0.5f, 1),
                new Vector2(12, 22), new Vector2(-12, -ColumnsTop));

            // Text stacks from the top; the icon is pinned to the column's foot, so it sits at
            // the same height on every card whatever the text above it runs to.
            var stack = UiKit.Rect(col, "text", new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            stack.pivot = new Vector2(0.5f, 1f);
            var layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 8f;
            stack.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (e == null)
            {
                Line(stack, "no boon", 26, Faint, FontStyle.Bold);
                Line(stack, "This side of the scale is empty.", 20, Muted);
                return;
            }

            var tint = boon ? LedgerStrip.BoonTint : LedgerStrip.CostTint;
            Line(stack, e.Name, 26, tint, FontStyle.Bold);

            if (e.IsCombination && e.Parts != null)
            {
                var names = new List<string>();
                foreach (var id in e.Parts) names.Add(ExchangeCatalog.Get(id)?.Name ?? id);
                Line(stack, $"{e.Origin.ToString().ToUpperInvariant()} - {string.Join(" + ", names)}", 15,
                    e.Origin == EntryOrigin.Citrinitas ? CitrineText : Gold);
            }

            Line(stack, e.Effect, 20, Body);

            if (e.Stackable)
                Line(stack, $"stack {ExchangeEntry.Roman(held + 1)} of {ExchangeEntry.Roman(e.MaxStacks)}", 16, Muted);

            // The last stack completes the capstone - a Rubedo, or a Nigredo and the Albedo a
            // circle would make of it. Said on the card, because that is the decision.
            if (ExchangeScale.Completes(e, held))
            {
                string cap = $"completes its {e.CapKind.ToUpperInvariant()} - {e.CapName}: {e.CapText}";
                var albedo = e.AlbedoId != null ? ExchangeCatalog.Get(e.AlbedoId) : null;
                if (albedo != null) cap += $" A circle makes it {albedo.Name}.";
                Line(stack, cap, 17, boon ? ExchangeScale.RubedoText : ExchangeScale.NigredoText);
            }

            var icon = UiKit.Rect(col, "icon", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-IconSize * 0.5f, 0), new Vector2(IconSize * 0.5f, IconSize));
            var img = icon.gameObject.AddComponent<RawImage>();
            img.texture = ExchangeScale.IconTexture(e);
            img.raycastTarget = false;
        }

        static Text Line(RectTransform stack, string text, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var label = UiKit.Label(stack, text, size, color, TextAnchor.UpperCenter);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.fontStyle = style;
            label.lineSpacing = 1.05f;
            return label;
        }

        void BuildRefuse(RectTransform full, float x, int slot)
        {
            _refuseRect = Card(full, x, RefuseColor);

            _refuseScale = ExchangeScale.Create(_refuseRect, ScaleAt);
            _refuseScale.ShowRefused(slot);

            UiKit.Label(UiKit.Rect(_refuseRect, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(16, -364), new Vector2(-16, -316)),
                "REFUSE", 34, new Color(0.62f, 0.65f, 0.72f), TextAnchor.MiddleCenter);

            UiKit.Label(UiKit.Rect(_refuseRect, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(16, -404), new Vector2(-16, -372)),
                "take nothing.", 20, Muted, TextAnchor.MiddleCenter);

            // Refusals left as pips, spent ones hollow - and said in words under them.
            int left = _offer.RefusalsLeft, total = Mathf.Max(left, T.RefusalsPerRun);
            int shown = Mathf.Min(total, 12);
            const float pip = 16f, pipGap = 8f;
            float rowW = shown * pip + (shown - 1) * pipGap;
            for (int i = 0; i < shown; i++)
            {
                float px = -rowW * 0.5f + i * (pip + pipGap);
                var ring = UiKit.Rect(_refuseRect, "pip", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(px, -440), new Vector2(px + pip, -440 + pip));
                var outer = ring.gameObject.AddComponent<Image>();
                outer.sprite = Spr.Circle; outer.color = BronzeDark; outer.raycastTarget = false;
                var inner = UiKit.Rect(ring, "in", Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3))
                    .gameObject.AddComponent<Image>();
                inner.sprite = Spr.Circle; inner.raycastTarget = false;
                inner.color = i < left ? BronzeLit : RefuseColor;
            }

            UiKit.Label(UiKit.Rect(_refuseRect, "n", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(16, -484), new Vector2(-16, -452)),
                $"{left} refusal{(left == 1 ? "" : "s")} left this run", 16, Faint, TextAnchor.MiddleCenter);

            _refuseVeil = Veil(_refuseRect, RefuseColor);
        }

        /// <summary>
        /// The ledger along the bottom. Anything on offer that the run already carries lights, so
        /// the player does not have to scan and count - the strip points at itself. It is also
        /// where a taken slate's icons land, so the row exists even before anything is carried.
        /// </summary>
        void BuildStrip(RectTransform full)
        {
            var row = UiKit.Rect(full, "strip", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-620, 92), new Vector2(620, 148));

            UiKit.Label(UiKit.Rect(row, "l", new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(96, 0)),
                "CARRYING", 13, new Color(0.42f, 0.45f, 0.53f), TextAnchor.MiddleLeft);

            _stripChips = UiKit.Rect(row, "chips", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(104, 0), new Vector2(0, 0));
            _chips.Clear();
            _stripEnd = _mods != null && _mods.Held.Count > 0
                ? LedgerStrip.Build(_stripChips, _mods, _onOffer, chips: _chips)
                : 0f;
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!IsOpen) return;

            // Dissolving: the row fades out and hands over to whatever was underneath it.
            if (_dissolve >= 0f)
            {
                _dissolve += Time.unscaledDeltaTime * 3.2f;
                if (_fade) _fade.alpha = Mathf.Clamp01(1f - _dissolve);
                if (_dissolve < 1f) return;

                var taken = _taken;
                var cb = _onChosen;
                Close();
                cb?.Invoke(taken);
                return;
            }

            // The offer is a plain C# object, so a domain reload leaves IsOpen true and this
            // null - and the screen then threw a NullReference every frame for the rest of the
            // session. Same shape as the interface and dictionary fields in CLAUDE.md; the only
            // difference is that this one is cheap to guard.
            if (_offer == null || _slates == null) return;

            if (_taking != null) { TickTaking(); return; }

            // Hover and pad focus both sway a scale - the focus cue on a pad, a "this one" on a
            // mouse. Touch has no hover, so it keeps still until tapped.
            var focused = Controls.Focused;
            var pointer = Controls.PointerPosition;
            bool canHover = !Controls.TouchMode && !Controls.GamepadMode;
            foreach (var s in _slates)
                if (s.Scale) s.Scale.Focused = focused == s.Rect
                    || canHover && RectTransformUtility.RectangleContainsScreenPoint(s.Rect, pointer, null);

            if (_offer.CanRefuse && Controls.CancelTapped) { Choose(null); return; }
            for (int i = 0; i < _slates.Count && i < 3; i++)
                if (Controls.DigitTapped(i)) { Choose(_slates[i]); return; }

            // D-pad navigation between slates - see Controls.SetFocusCandidates. Rebuilt every
            // frame rather than only when the slates change: it's a handful of entries, and this
            // is the one place that already knows both the slates AND the refuse card together.
            _focusRects ??= new List<RectTransform>();
            _focusRects.Clear();
            foreach (var s in _slates) _focusRects.Add(s.Rect);
            if (_refuseRect != null) _focusRects.Add(_refuseRect);
            Controls.SetFocusCandidates(_focusRects);

            if (!Controls.Tapped(out var at)) return;

            if (_refuseRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_refuseRect, at, null))
            { Choose(null); return; }

            foreach (var s in _slates)
                if (RectTransformUtility.RectangleContainsScreenPoint(s.Rect, at, null))
                { Choose(s); return; }
        }

        /// <summary>Refusing dissolves at once; taking plays out first (see TickTaking).</summary>
        void Choose(Slate slate)
        {
            if (slate == null) { _taken = null; _dissolve = 0f; return; }

            _taken = slate.Pair;
            _taking = slate;
            _takenAt = Time.unscaledTime;
            slate.Scale.Take();

            _flyers.Clear();
            int fresh = 0;
            var entries = new[] { slate.Pair.Boon, slate.Pair.Cost };
            for (int side = 0; side < 2; side++)
            {
                var e = entries[side];
                if (e == null) continue;
                _chips.TryGetValue(e.Id, out var chip);
                Vector3 to;
                if (chip) to = chip.TransformPoint(chip.rect.center);
                else
                {
                    // Not carried yet: it lands where its chip will be, after the strip's last.
                    float cx = _stripEnd + fresh++ * 52f + 22f;
                    to = _stripChips.TransformPoint(new Vector3(_stripChips.rect.xMin + cx, _stripChips.rect.center.y, 0));
                }
                _flyers.Add(Fly(e, slate.Scale.IconWorldCentre(side), to, chip));
            }
        }

        Flyer Fly(ExchangeEntry e, Vector3 from, Vector3 to, RectTransform chip)
        {
            var rt = UiKit.Rect(_root.transform, "flyer", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            float size = ExchangeScale.IconTexels * T.ScaleUnitsPerTexel;
            rt.sizeDelta = new Vector2(size, size);
            rt.position = from;
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = ExchangeScale.IconTexture(e);
            img.raycastTarget = false;
            return new Flyer { Rect = rt, Image = img, From = from, To = to, FromSize = size, ToSize = 30f, Chip = chip };
        }

        void TickTaking()
        {
            float tp = Time.unscaledTime - _takenAt;

            // The rest of the row steps back while the taken slate plays out.
            float veil = 0.75f * Ease(tp / 0.3f);
            foreach (var s in _slates)
                if (s != _taking && s.Veil) s.Veil.color = new Color(s.Veil.color.r, s.Veil.color.g, s.Veil.color.b, veil);
            if (_refuseVeil) _refuseVeil.color = new Color(_refuseVeil.color.r, _refuseVeil.color.g, _refuseVeil.color.b, veil);

            float lift = T.TakeLiftSeconds, fly = T.TakeFlySeconds;
            float liftUnits = 24f * _root.transform.lossyScale.y;
            foreach (var f in _flyers)
            {
                if (!f.Rect) continue;
                if (tp < lift)
                {
                    f.Rect.position = f.From + Vector3.up * (liftUnits * tp / lift);
                    continue;
                }
                float u = Ease((tp - lift) / fly);
                var start = f.From + Vector3.up * liftUnits;
                var arc = Vector3.up * (Mathf.Sin(Mathf.PI * u) * 70f * _root.transform.lossyScale.y);
                f.Rect.position = Vector3.Lerp(start, f.To, u) + arc;
                float size = Mathf.Lerp(f.FromSize, f.ToSize, u);
                f.Rect.sizeDelta = new Vector2(size, size);

                if (u >= 1f && !f.Landed)
                {
                    f.Landed = true;
                    // Landing on a chip already carried: ring it. A new one gets a chip of its own.
                    var host = f.Chip ? f.Chip : NewChip(f.To);
                    var ring = UiKit.Rect(host, "landed", Vector2.zero, Vector2.one,
                        new Vector2(-3, -3), new Vector2(3, 3)).gameObject.AddComponent<Image>();
                    ring.color = new Color(Gold.r, Gold.g, Gold.b, 0.55f);
                    ring.raycastTarget = false;
                    ring.transform.SetAsFirstSibling();
                    if (f.Chip) f.Image.enabled = false;
                }
            }

            if (tp >= T.TakeHoldSeconds) _dissolve = 0f;
        }

        RectTransform NewChip(Vector3 at)
        {
            var chip = UiKit.Panel(_stripChips, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(-22, -22), new Vector2(22, 22), new Color(0.10f, 0.11f, 0.14f));
            chip.position = at;
            // Behind the flyer, which stays as its icon.
            chip.SetAsFirstSibling();
            return chip;
        }

        static float Ease(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }
    }
}
