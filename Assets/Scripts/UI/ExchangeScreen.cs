using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Convergence.Core;
using Convergence.Exchange;

namespace Convergence.UI
{
    /// <summary>
    /// The equivalent-exchange row: paired offers with a refuse slate between them, shown BEFORE
    /// the floor reward and dissolving to reveal it.
    ///
    /// It comes first on purpose. The floor reward is a gift; this is a bargain, and a bargain
    /// read after you have already been handed something free is just a tax. Taking the debt while
    /// the reward is still hidden is what makes it a decision.
    /// </summary>
    public class ExchangeScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        /// <summary>An offer that cannot be refused has no back button - see FloorRewardScreen.</summary>
        public bool CanDismiss => _offer != null && _offer.CanRefuse;

        GameObject _root;
        ExchangeOffer _offer;
        RunModifiers _mods;
        Action<ExchangePair> _onChosen;

        readonly List<(RectTransform Rect, ExchangePair Pair)> _slates = new();
        RectTransform _refuseRect;
        readonly List<RectTransform> _focusRects = new();
        CanvasGroup _fade;
        float _dissolve = -1f;
        ExchangePair _taken;

        /// <summary>Transmuter's Eye: what the floor reward behind this row will be.</summary>
        string _preview;

        /// <summary>Scrying Glass: what the floor below holds.</summary>
        string _scry;

        /// <summary>Ids on offer right now, so the ledger strip can light what matches.</summary>
        readonly HashSet<string> _onOffer = new();

        /// <summary>Oracle: the deal that would follow each slate, then refusing (null where it
        /// cannot). Null without Oracle.</summary>
        List<ExchangeOffer> _oracle;

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
            _slates.Clear();
            _refuseRect = null;
            _fade = null;
        }

        // ------------------------------------------------------------------ build

        void Build(Transform canvas, int floor)
        {
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
                "EQUIVALENT EXCHANGE", 40, new Color(0.86f, 0.72f, 0.38f), TextAnchor.MiddleCenter);

            UiKit.Label(UiKit.Rect(full, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -166), new Vector2(0, -132)),
                _offer.CanRefuse
                    ? "nothing is given that is not also taken"
                    : _offer.Forced ? "INDENTURE - you must take one"
                    : "NO REFUSALS LEFT - you must take one",
                18, _offer.CanRefuse ? new Color(0.5f, 0.53f, 0.6f) : new Color(0.88f, 0.5f, 0.4f),
                TextAnchor.MiddleCenter);

            // Slates laid out with the refuse card in the MIDDLE of the row, same size as what it
            // refuses. Not a corner button: a player one bad cost from death should meet the way
            // out at the same moment they meet the trap.
            int n = _offer.Pairs.Count;
            int cards = n + (_offer.CanRefuse ? 1 : 0);
            const float w = 300f, gap = 22f;
            float totalW = cards * w + (cards - 1) * gap;
            float x0 = -totalW * 0.5f;

            int refuseAt = _offer.CanRefuse ? n / 2 : -1;
            int pairIndex = 0;

            for (int slot = 0; slot < cards; slot++)
            {
                float x = x0 + slot * (w + gap);
                if (slot == refuseAt)
                {
                    BuildRefuse(full, x, w);
                    if (_oracle != null && _oracle.Count > n) BuildOracle(full, x, w, _oracle[n]);
                    continue;
                }
                if (_oracle != null && pairIndex < _oracle.Count) BuildOracle(full, x, w, _oracle[pairIndex]);
                BuildSlate(full, x, w, _offer.Pairs[pairIndex++]);
            }

            // The reward this row is covering, for anyone carrying Transmuter's Eye. Sits directly
            // under the slates, because it is information about the choice above it.
            if (!string.IsNullOrEmpty(_preview))
                UiKit.Label(UiKit.Rect(full, "peek", new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                    new Vector2(0, -204), new Vector2(0, -168)),
                    _preview, 17, new Color(0.86f, 0.72f, 0.38f), TextAnchor.MiddleCenter);

            // Scrying Glass: the floor below, on the line under the reward peek - the same kind
            // of information (what is coming), read at the moment the next floor is being paid for.
            if (!string.IsNullOrEmpty(_scry))
                UiKit.Label(UiKit.Rect(full, "scry", new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                    new Vector2(0, -240), new Vector2(0, -204)),
                    _scry, 17, new Color(0.62f, 0.78f, 0.92f), TextAnchor.MiddleCenter);

            BuildStrip(full);

            UiKit.Label(UiKit.Rect(full, "f", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 34), new Vector2(0, 70)),
                _offer.CanRefuse ? "tap a slate    -    [1-3] choose    -    BACK or [ESC] refuse"
                                 : "click a slate    -    [1-3] choose",
                17, new Color(0.42f, 0.45f, 0.53f), TextAnchor.MiddleCenter);
        }

        /// <summary>Oracle: the deal that would follow this card, in a line under it.</summary>
        void BuildOracle(RectTransform full, float x, float w, ExchangeOffer next)
        {
            string text;
            if (next == null || next.Pairs.Count == 0) text = "then: nothing";
            else
            {
                var parts = new List<string>();
                foreach (var p in next.Pairs) parts.Add($"{p.Boon?.Name ?? "-"} / {p.Cost?.Name ?? "-"}");
                text = "then: " + string.Join("  |  ", parts);
            }
            var label = UiKit.Label(UiKit.Rect(full, "oracle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, -196), new Vector2(x + w, -154)), text, 13, new Color(0.62f, 0.78f, 0.92f),
                TextAnchor.UpperCenter);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        void BuildSlate(RectTransform full, float x, float w, ExchangePair pair)
        {
            var card = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, -150), new Vector2(x + w, 200), new Color(0.085f, 0.09f, 0.12f, 1f));

            // Why this slate is here, when it is not a plain draw - a combination just opened, or
            // a stack the run was owed (the mercy pull, the pity timer).
            string why = pair.Reason switch
            {
                SlateReason.NewCombination => "NEW",
                SlateReason.Mercy => "ONE STACK FROM ITS NIGREDO",
                SlateReason.Pity => "RETURNING",
                _ => null,
            };
            if (why != null)
                UiKit.Label(UiKit.Rect(card, "why", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(20, 2), new Vector2(-20, 22)), why, 13, new Color(0.95f, 0.82f, 0.42f),
                    TextAnchor.MiddleCenter);

            float y = -22f;
            if (pair.Boon != null) y = BuildHalf(card, pair.Boon, y, true);
            else
            {
                UiKit.Label(UiKit.Rect(card, "none", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(20, y - 46), new Vector2(-20, y)),
                    "no boon", 20, new Color(0.45f, 0.47f, 0.54f));
                y -= 62f;
            }

            // The rule between the halves is the exchange itself.
            var rule = UiKit.Rect(card, "rule", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(20, y - 9), new Vector2(-20, y - 8));
            rule.gameObject.AddComponent<Image>().color = new Color(0.28f, 0.3f, 0.36f);

            if (pair.Cost != null) BuildHalf(card, pair.Cost, y - 22f, false);

            _slates.Add((card, pair));
        }

        float BuildHalf(RectTransform card, ExchangeEntry e, float y, bool boon)
        {
            var tint = boon ? new Color(0.61f, 0.82f, 0.42f) : new Color(0.88f, 0.44f, 0.31f);

            var icon = UiKit.Rect(card, "icon", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, y - 46), new Vector2(66, y));
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = ExchangeGlyphs.Get(e);
            img.color = tint;
            img.preserveAspect = true;
            img.raycastTarget = false;

            // The stack this would make, so stacking reads off the card itself.
            int held = _mods != null ? _mods.StacksOf(e) : 0;
            string name = e.Stackable ? $"{e.Name}  {ExchangeEntry.Roman(held + 1)}/{ExchangeEntry.Roman(e.MaxStacks)}" : e.Name;
            if (e.IsCombination) name = $"{e.Name}  ({e.Origin})";
            UiKit.Label(UiKit.Rect(card, "n", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(78, y - 30), new Vector2(-18, y)), name, 19, tint);

            // The last stack completes the capstone - a Rubedo, or a Nigredo and the Albedo a
            // circle would make of it. Said on the card, because that is the decision.
            string capLine = null;
            if (e.CapName != null && held + 1 >= e.MaxStacks)
            {
                capLine = $"{e.CapKind}: {e.CapName} - {e.CapText}";
                var albedo = e.AlbedoId != null ? ExchangeCatalog.Get(e.AlbedoId) : null;
                if (albedo != null) capLine += $" A circle makes it {albedo.Name}.";
            }
            if (held > 0)
                UiKit.Label(UiKit.Rect(card, "held", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(78, y - 48), new Vector2(-18, y - 30)),
                    $"you carry {held}", 14, new Color(0.86f, 0.72f, 0.38f));

            var body = UiKit.Label(UiKit.Rect(card, "e", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(20, y - 130), new Vector2(-18, y - (held > 0 ? 52f : 34f))),
                capLine != null ? $"{e.Effect}\n{capLine}" : e.Effect, capLine != null ? 14 : 16,
                new Color(0.72f, 0.75f, 0.82f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            return y - 148f;
        }

        void BuildRefuse(RectTransform full, float x, float w)
        {
            _refuseRect = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, -150), new Vector2(x + w, 200), new Color(0.055f, 0.06f, 0.08f, 1f));

            UiKit.Label(UiKit.Rect(_refuseRect, "t", Vector2.zero, Vector2.one,
                new Vector2(16, 0), new Vector2(-16, -20)),
                "REFUSE", 28, new Color(0.62f, 0.65f, 0.72f), TextAnchor.MiddleCenter);

            UiKit.Label(UiKit.Rect(_refuseRect, "s", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(16, 70), new Vector2(-16, 124)),
                $"take nothing.\n{_offer.RefusalsLeft} refusal{(_offer.RefusalsLeft == 1 ? "" : "s")} left this run.",
                16, new Color(0.42f, 0.45f, 0.53f), TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// The ledger along the bottom. Anything on offer that the run already carries lights, so
        /// the player does not have to scan and count - the strip points at itself.
        /// </summary>
        void BuildStrip(RectTransform full)
        {
            if (_mods == null || _mods.Held.Count == 0) return;

            var row = UiKit.Rect(full, "strip", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-620, 92), new Vector2(620, 148));

            UiKit.Label(UiKit.Rect(row, "l", new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(96, 0)),
                "CARRYING", 13, new Color(0.42f, 0.45f, 0.53f), TextAnchor.MiddleLeft);

            var chips = UiKit.Rect(row, "chips", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(104, 0), new Vector2(0, 0));
            LedgerStrip.Build(chips, _mods, _onOffer);
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
            if (_offer == null) return;

            if (_offer.CanRefuse && Core.Controls.CancelTapped) { Choose(null); return; }
            for (int i = 0; i < _slates.Count && i < 3; i++)
                if (Core.Controls.DigitTapped(i)) { Choose(_slates[i].Pair); return; }

            // D-pad navigation between slates - see Controls.SetFocusCandidates. Rebuilt every
            // frame rather than only when the slates change: it's a handful of entries, and this
            // is the one place that already knows both the slates AND the refuse card together.
            _focusRects.Clear();
            foreach (var (rect, _) in _slates) _focusRects.Add(rect);
            if (_refuseRect != null) _focusRects.Add(_refuseRect);
            Core.Controls.SetFocusCandidates(_focusRects);

            if (!Core.Controls.Tapped(out var at)) return;

            if (_refuseRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_refuseRect, at, null))
            { Choose(null); return; }

            foreach (var (rect, pair) in _slates)
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, at, null))
                { Choose(pair); return; }
        }

        void Choose(ExchangePair pair)
        {
            _taken = pair;
            _dissolve = 0f;
        }
    }
}
