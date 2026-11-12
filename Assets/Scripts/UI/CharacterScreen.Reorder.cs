using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The reorder window (2026-11-12, the user's approved mock): one card per art in rotation
    /// order, each showing its weight and what is inscribed on it. Pick an art up and tap
    /// another slot to swap, or step it with the arrows beneath (the pad's way). Swapping is
    /// live from the moment the window opens, with UNDO ALL as the guard (it replaced the old
    /// REARRANGE step). Every inscription moves with its art (RunModifiers.SwapSockets).
    ///
    /// Arts are never dropped here: a slot only ever changes by a floor reward REPLACING its art
    /// (the user's call - the old DROP SLOT 3 toggle is gone).
    /// </summary>
    public partial class CharacterScreen
    {
        static readonly Color SwVeil = new(0.039f, 0.035f, 0.055f, 1f);
        static readonly Color SwFrame = FloorRewardScreen.Hex(0x3b3746), SwFace = FloorRewardScreen.Hex(0x1c1a22);
        static readonly Color SwGold = FloorRewardScreen.Hex(0xffd95a), SwGilt = FloorRewardScreen.Hex(0xc9a23a);
        static readonly Color SwBronze = FloorRewardScreen.Hex(0xc8914f), SwNotch = FloorRewardScreen.Hex(0xf2cf8c);
        static readonly Color SwBright = FloorRewardScreen.Hex(0xf3ead8), SwMuted = FloorRewardScreen.Hex(0x80879a);
        static readonly Color SwPrompt = FloorRewardScreen.Hex(0x9aa1b2), SwOff = FloorRewardScreen.Hex(0x3d3040);
        static readonly Color SwInscribed = FloorRewardScreen.Hex(0xbb96f2), SwGrey = FloorRewardScreen.Hex(0x6b6577);

        class SwapCard
        {
            public RectTransform Card, Left, Right;
        }

        // Not readonly - see CLAUDE.md's domain reload traps.
        List<SwapCard> _swap = new();
        List<int> _origin = new();      // the slot each art started this visit in, for UNDO ALL
        RectTransform _swapCards, _undoRect, _doneRect;
        Text _swapStatus, _undoText, _doneText;
        int _picked = -1;

        /// <summary>True while the reorder window covers the sheet - BACK then belongs to it.</summary>
        public bool ReorderPanelUp => IsOpen && ReorderPanelOpen;

        void BuildReorderOverlay(RectTransform full)
        {
            _reorderPanel = UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, SwVeil);
            var panel = _reorderPanel;
            var top = new Vector2(0, 1);
            var one = new Vector2(1, 1);

            var tag = UiKit.Label(UiKit.Rect(panel, "tag", top, one, new Vector2(0, -62), new Vector2(0, -36)),
                "W E A P O N   A R T S", 18, SwBronze, TextAnchor.MiddleCenter);
            tag.fontStyle = FontStyle.Bold;
            var title = UiKit.Label(UiKit.Rect(panel, "title", top, one, new Vector2(0, -132), new Vector2(0, -64)),
                "REORDER ARTS", 56, SwBright, TextAnchor.MiddleCenter);
            title.font = UiKit.Title;
            title.fontSize = 56;
            _swapStatus = UiKit.Label(UiKit.Rect(panel, "status", top, one, new Vector2(0, -172), new Vector2(0, -136)),
                "", 22, SwPrompt, TextAnchor.MiddleCenter);

            _swapCards = UiKit.Rect(panel, "cards", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            UiKit.Label(UiKit.Rect(panel, "loop", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-600, -352), new Vector2(600, -322)),
                "AFTER THE LAST SLOT THE ROTATION STARTS AGAIN AT SLOT 1", 15, SwMuted, TextAnchor.MiddleCenter);

            (_undoRect, _undoText) = SwapButton(panel, new Vector2(-230, 0), 300f, SwGrey, SwFace);
            _undoText.text = "UNDO ALL";
            (_doneRect, _doneText) = SwapButton(panel, new Vector2(170, 0), 440f, SwGilt, FloorRewardScreen.Hex(0x2a2414));
            _doneText.color = SwGold;
        }

        static (RectTransform, Text) SwapButton(RectTransform parent, Vector2 x, float w, Color frame, Color face)
        {
            var rt = UiKit.Rect(parent, "button", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(x.x - w * 0.5f, 40f), new Vector2(x.x + w * 0.5f, 136f));
            FloorRewardScreen.Notched(rt, frame);
            var f = UiKit.Rect(rt, "face", Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            FloorRewardScreen.Notched(f, face);
            var l = UiKit.Label(f, "", 20, FloorRewardScreen.Hex(0xc8ccd6), TextAnchor.MiddleCenter);
            l.fontStyle = FontStyle.Bold;
            return (rt, l);
        }

        void OpenReorderPanel()
        {
            var player = _player?.Invoke();
            ReorderPanelOpen = true;
            _picked = -1;
            _origin.Clear();
            for (int i = 0; i < (player != null ? player.Slots.Count : 0); i++) _origin.Add(i);
            if (_reorderPanel != null) _reorderPanel.gameObject.SetActive(true);
            RebuildSwapCards();
        }

        void CloseReorderPanel()
        {
            if (_reorderOnly) { Close(); return; }
            ReorderPanelOpen = false;
            _picked = -1;
            if (_reorderPanel != null) _reorderPanel.gameObject.SetActive(false);
            Refresh();
        }

        /// <summary>The cards are rebuilt whole after every change - the window is small and a
        /// swap moves names, weights and inscriptions all at once.</summary>
        void RebuildSwapCards(int focusArt = -1, bool focusRight = false)
        {
            var player = _player?.Invoke();
            if (_swapCards == null || player == null) return;
            for (int i = _swapCards.childCount - 1; i >= 0; i--)
            {
                var c = _swapCards.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
            _swap.Clear();

            var mods = Exchange.RunModifiers.Live;
            int n = player.Slots.Count;
            const float gap = 70f, cardH = 520f, topY = 330f;
            float cardW = Mathf.Min(440f, (1720f - (n - 1) * gap) / Mathf.Max(1, n));
            float startX = -(n * cardW + (n - 1) * gap) * 0.5f;

            for (int j = 0; j < n; j++)
            {
                var m = player.Slots[j] ?? MovesetLibrary.Default;
                bool picked = _picked == j, targeting = _picked >= 0 && !picked;
                bool moved = j < _origin.Count && _origin[j] != j;
                float x = startX + j * (cardW + gap);
                float y = topY + (picked ? 20f : 0f);

                var card = UiKit.Rect(_swapCards, $"art{j}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, y - cardH), new Vector2(x + cardW, y));
                FloorRewardScreen.Notched(card, picked ? SwGold : targeting ? SwBronze : SwFrame);
                var face = UiKit.Rect(card, "face", Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
                FloorRewardScreen.Notched(face, SwFace);

                var a = new Vector2(0, 1);
                var b = new Vector2(1, 1);
                UiKit.Label(UiKit.Rect(face, "slot", a, b, new Vector2(24, -48), new Vector2(-24, -18)),
                    $"SLOT {j + 1}", 16, SwMuted).fontStyle = FontStyle.Bold;
                string tag = picked ? "PICKED UP" : j == player.RotationIndex ? "UP NEXT"
                           : player.SlotLocked(j) ? "SIGNATURE" : moved ? "MOVED" : "";
                var tagText = UiKit.Label(UiKit.Rect(face, "tag", a, b, new Vector2(24, -48), new Vector2(-24, -18)),
                    tag, 16, picked || j == player.RotationIndex ? SwGold : SwBronze, TextAnchor.MiddleRight);
                tagText.fontStyle = FontStyle.Bold;

                var glyph = FloorRewardScreen.Img(face, "glyph", Glyphs.Get(m.FinisherGlyph),
                    j == player.RotationIndex ? SwGold : FloorRewardScreen.Hex(0xd8d2c4));
                var grt = glyph.rectTransform;
                grt.anchorMin = grt.anchorMax = a;
                grt.pivot = new Vector2(0, 1);
                grt.sizeDelta = new Vector2(60, 60);
                grt.anchoredPosition = new Vector2(24, -64);

                var name = UiKit.Label(UiKit.Rect(face, "name", a, b, new Vector2(100, -104), new Vector2(-16, -62)),
                    m.DisplayName, 30, SwBright);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 18;
                name.resizeTextMaxSize = 30;
                int weight = RotationWheel.WeightOf(m);
                FloorRewardScreen.Notches(face, weight, a, new Vector2(100, -118), 9f, 16f, 4f);
                UiKit.Label(UiKit.Rect(face, "weight", a, b, new Vector2(146, -130), new Vector2(-16, -106)),
                    FloorRewardScreen.WeightName(weight), 15, SwBronze).fontStyle = FontStyle.Bold;

                var rule = UiKit.Panel(face, a, b, new Vector2(24, -152), new Vector2(-24, -149), SwFrame);
                rule.GetComponent<Image>().raycastTarget = false;
                UiKit.Label(UiKit.Rect(face, "inscribed", a, b, new Vector2(24, -190), new Vector2(-24, -164)),
                    "INSCRIBED", 16, SwInscribed).fontStyle = FontStyle.Bold;

                var held = mods != null ? mods.SocketPreview(j, null) : null;
                if (held == null || held.Count == 0)
                    UiKit.Label(UiKit.Rect(face, "none", a, b, new Vector2(24, -230), new Vector2(-24, -198)),
                        "Nothing inscribed.", 20, SwMuted);
                else
                    for (int k = 0; k < held.Count; k++)
                    {
                        var e = held[k].Entry;
                        var ink = ExchangeInk.Tint(e);
                        float ey = -200f - k * 58f;
                        var row = UiKit.Panel(face, a, b, new Vector2(24, ey - 50), new Vector2(-24, ey),
                            Color.Lerp(SwFace, ink, 0.12f));
                        row.GetComponent<Image>().raycastTarget = false;
                        UiKit.Label(UiKit.Rect(row, "name", Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-8, 0)),
                            e.Name, 21, ink, TextAnchor.MiddleLeft);
                        UiKit.Label(UiKit.Rect(row, "kind", Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-14, 0)),
                            e.SocketCost ? "SOCKET COST" : "FORM", 13, SwMuted, TextAnchor.MiddleRight).fontStyle = FontStyle.Bold;
                    }

                var cta = UiKit.Label(UiKit.Rect(face, "cta", Vector2.zero, new Vector2(1, 0), new Vector2(0, 18), new Vector2(0, 52)),
                    picked ? "TAP AGAIN TO PUT BACK" : targeting ? "SWAP HERE" : "PICK UP", 17,
                    picked ? SwGold : targeting ? SwNotch : SwMuted, TextAnchor.MiddleCenter);
                cta.fontStyle = FontStyle.Bold;

                // The pad's (and anyone's) way: step this art one slot along.
                float mid = x + cardW * 0.5f;
                var left = StepButton(mid - 62f, j > 0, "<");
                var right = StepButton(mid + 62f, j < n - 1, ">");

                if (j < n - 1)
                {
                    var arrow = UiKit.Label(UiKit.Rect(_swapCards, "arrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(x + cardW, topY - cardH * 0.5f - 30f), new Vector2(x + cardW + gap, topY - cardH * 0.5f + 30f)),
                        "»", 46, SwGilt, TextAnchor.MiddleCenter);
                    arrow.raycastTarget = false;
                }

                _swap.Add(new SwapCard { Card = card, Left = left, Right = right });
            }

            if (focusArt >= 0 && focusArt < _swap.Count && Core.Controls.GamepadMode)
            {
                var s = _swap[focusArt];
                var to = focusRight ? s.Right : s.Left;
                if (to == null) to = focusRight ? s.Left : s.Right;
                if (to != null) Core.Controls.Focus(to);
            }

            bool changed = false;
            for (int i = 0; i < _origin.Count; i++) changed |= _origin[i] != i;
            if (_undoText) _undoText.color = changed ? FloorRewardScreen.Hex(0xc8ccd6) : FloorRewardScreen.Hex(0x4a4656);
            if (_doneText) _doneText.text = _reorderOnly ? "DONE  ·  BACK TO REWARD" : "DONE";

            if (_swapStatus)
            {
                string status = "Pick up an art, then choose its slot. What is inscribed on it moves too.";
                if (_picked >= 0 && _picked < n)
                {
                    var art = player.Slots[_picked];
                    var held = mods != null ? mods.SocketPreview(_picked, null) : null;
                    string who = art != null ? art.DisplayName : "The art";
                    status = held != null && held.Count > 0
                        ? $"{who} picked up. {Names(held)} {(held.Count == 1 ? "goes" : "go")} with it."
                        : $"{who} picked up. Choose where it goes.";
                }
                else if (changed) status = "Order changed. Every inscription stayed with its art.";
                _swapStatus.text = status;
            }
        }

        static string Names(List<(Exchange.ExchangeEntry Entry, Exchange.ExchangeEntry To)> held)
        {
            var parts = new List<string>();
            foreach (var h in held) parts.Add(h.Entry.Name);
            return parts.Count <= 1 ? string.Join("", parts)
                 : string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];
        }

        RectTransform StepButton(float cx, bool live, string glyph)
        {
            var rt = UiKit.Rect(_swapCards, "step", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(cx - 52f, -306f), new Vector2(cx + 52f, -210f));
            FloorRewardScreen.Notched(rt, SwFrame);
            var f = UiKit.Rect(rt, "face", Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            FloorRewardScreen.Notched(f, FloorRewardScreen.Hex(0x1f1c22));
            var l = UiKit.Label(f, glyph, 30, live ? SwNotch : SwOff, TextAnchor.MiddleCenter);
            l.fontStyle = FontStyle.Bold;
            return live ? rt : null;
        }

        void SwapArts(int a, int b, int focusArt = -1, bool focusRight = false)
        {
            var player = _player?.Invoke();
            if (player != null && player.SwapSlots(a, b))
            {
                // A reorder carries each art's inscriptions with it (RunModifiers.SwapSockets).
                Exchange.RunModifiers.Live?.SwapSockets(a, b);
                if (a < _origin.Count && b < _origin.Count) (_origin[a], _origin[b]) = (_origin[b], _origin[a]);
            }
            _picked = -1;
            RebuildSwapCards(focusArt, focusRight);
        }

        void PickArt(int j)
        {
            if (_picked < 0) _picked = j;
            else if (_picked == j) _picked = -1;
            else { SwapArts(_picked, j); return; }
            RebuildSwapCards();
        }

        void UndoAll()
        {
            var player = _player?.Invoke();
            if (player == null) return;
            for (int i = 0; i < _origin.Count; i++)
                while (_origin[i] != i)
                {
                    int j = _origin[i];
                    if (!player.SwapSlots(i, j)) { _origin.Clear(); break; }
                    Exchange.RunModifiers.Live?.SwapSockets(i, j);
                    (_origin[i], _origin[j]) = (_origin[j], _origin[i]);
                }
            _picked = -1;
            RebuildSwapCards();
        }

        void RegisterReorderFocus()
        {
            foreach (var s in _swap)
            {
                _focusRects.Add(s.Card);
                if (s.Left != null) _focusRects.Add(s.Left);
                if (s.Right != null) _focusRects.Add(s.Right);
            }
            if (_undoRect != null) _focusRects.Add(_undoRect);
            if (_doneRect != null) _focusRects.Add(_doneRect);
        }

        void HandleReorderInput()
        {
            if (Core.Controls.CancelTapped)
            {
                Core.Controls.ConsumeCancel();
                if (_picked >= 0) { _picked = -1; RebuildSwapCards(); }
                else CloseReorderPanel();
                return;
            }
            for (int j = 0; j < _swap.Count; j++)
                if (Core.Controls.DigitTapped(j)) { PickArt(j); return; }

            if (!Core.Controls.Tapped(out var p)) return;
            for (int j = 0; j < _swap.Count; j++)
            {
                var s = _swap[j];
                if (Hit(s.Left, p)) { SwapArts(j, j - 1, j - 1, false); return; }
                if (Hit(s.Right, p)) { SwapArts(j, j + 1, j + 1, true); return; }
                if (Hit(s.Card, p)) { PickArt(j); return; }
            }
            if (Hit(_undoRect, p)) { UndoAll(); return; }
            if (Hit(_doneRect, p)) CloseReorderPanel();
        }

        static bool Hit(RectTransform rt, Vector2 p)
            => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, p, null);
    }
}
