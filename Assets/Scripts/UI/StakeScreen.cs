using System;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The question the gate asks before a run: put one equipped piece on the line, or go in with
    /// nothing staked. See <see cref="GearStake"/> for the rules this only shows.
    ///
    /// Opened only when the player HAS something stakeable - a character wearing no stat gear
    /// walks straight through the gate, and a screen with one answer is not a question.
    ///
    /// EVERY CARD STATES ITS TERMS: the floor to clear, what the payout is, and that the piece is
    /// destroyed on death. Same rule as RiftScreen - a cost learned by having it happen is learned
    /// too late to have been a decision.
    ///
    /// Dismissible: backing out stays in the hub and starts nothing.
    /// </summary>
    public class StakeScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        GameObject _root;
        int _openedFrame = -1;
        Action<MintedGearRecord> _onChoose;
        Action _onCancel;

        readonly List<(RectTransform Rect, MintedGearRecord Item)> _cards = new();
        readonly List<RectTransform> _focusRects = new();
        RectTransform _noStake;

        const int Columns = 3;
        const float CardW = 420f, CardH = UiKit.TouchTarget, Gap = 14f;

        public void Open(Transform canvas, IReadOnlyList<MintedGearRecord> candidates,
                         Action<MintedGearRecord> onChoose, Action onCancel)
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _onChoose = onChoose;
            _onCancel = onCancel;
            GamePause.Hold(this);

            _root = new GameObject("StakeScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.03f, 0.05f, 0.95f));

            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -104), new Vector2(0, -44)),
                "STAKE A PIECE?", 42, new Color(0.95f, 0.78f, 0.55f), TextAnchor.MiddleCenter);
            var sub = UiKit.Label(UiKit.Rect(full, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(160, -186), new Vector2(-160, -110)),
                "Clear the floor on the card, then extract, and you leave with a matching piece to " +
                "combine it with at the Forge. Extract sooner and it simply comes home. " +
                "Die - before or after that floor - and the staked piece is destroyed.",
                19, new Color(0.62f, 0.60f, 0.68f), TextAnchor.UpperCenter);
            sub.horizontalOverflow = HorizontalWrapMode.Wrap;

            int rows = Mathf.CeilToInt(candidates.Count / (float)Columns);
            int cols = Mathf.Min(Columns, candidates.Count);
            float gridW = cols * CardW + (cols - 1) * Gap;
            float top = -210f;
            for (int i = 0; i < candidates.Count; i++)
            {
                int row = i / Columns, col = i % Columns;
                // Each row centred on its own count, so a lone last card is not stranded left.
                int inRow = Mathf.Min(Columns, candidates.Count - row * Columns);
                float rowW = inRow * CardW + (inRow - 1) * Gap;
                float x = -rowW * 0.5f + col * (CardW + Gap);
                float y = top - row * (CardH + Gap);
                _cards.Add((Card(full, candidates[i], x, y), candidates[i]));
            }

            float btnTop = top - rows * (CardH + Gap) - 10f;
            _noStake = UiKit.Panel(full, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-gridW * 0.5f, btnTop - CardH), new Vector2(gridW * 0.5f, btnTop),
                new Color(0.12f, 0.14f, 0.16f, 1f));
            UiKit.Label(UiKit.Rect(_noStake, "l", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                "ENTER WITH NOTHING STAKED", 24, new Color(0.78f, 0.82f, 0.88f), TextAnchor.MiddleCenter);

            UiKit.Hint(UiKit.Rect(full, "h", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 24), new Vector2(0, 60)),
                "[ ESC ] stay in the hub", "BACK to stay in the hub", 18,
                new Color(0.45f, 0.47f, 0.54f));
        }

        static RectTransform Card(RectTransform parent, MintedGearRecord r, float x, float y)
        {
            var card = UiKit.Panel(parent, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(x, y - CardH), new Vector2(x + CardW, y), new Color(0.17f, 0.12f, 0.10f, 1f));

            var chip = UiKit.Rect(card, "c", new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(12, 12), new Vector2(24, -12));
            chip.gameObject.AddComponent<UnityEngine.UI.Image>().color = Art.Palette.Tier(r.Tier).Base;

            UiKit.Label(UiKit.Rect(card, "n", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(38, -46), new Vector2(-120, -10)),
                r.DisplayName, 22, new Color(0.96f, 0.90f, 0.82f), TextAnchor.MiddleLeft);
            UiKit.Stars(UiKit.Rect(card, "st", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-116, -44), new Vector2(-12, -12)),
                r.UpgradeLevel, Art.Gear.GearRoller.MaxLevel, 26f,
                new Color(1f, 0.84f, 0.40f), new Color(0.32f, 0.28f, 0.26f));

            UiKit.Label(UiKit.Rect(card, "p", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(38, 44), new Vector2(-12, -50)),
                GearForge.Label(r.PrimaryStat), 17, new Color(0.70f, 0.66f, 0.62f), TextAnchor.MiddleLeft);

            bool promotes = r.UpgradeLevel >= Art.Gear.GearRoller.MaxLevel;
            UiKit.Label(UiKit.Rect(card, "g", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(38, 10), new Vector2(-12, 42)),
                $"clear floor {GearStake.GateFloor(r.UpgradeLevel)}   ->   " +
                (promotes ? "a partner that PROMOTES it" : "a partner for +1 star"),
                17, new Color(0.95f, 0.70f, 0.45f), TextAnchor.MiddleLeft);
            return card;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _cards.Clear();
            _noStake = null;
            _onChoose = null;
            _onCancel = null;
        }

        void Update()
        {
            if (!IsOpen) return;

            _focusRects.Clear();
            foreach (var (rect, _) in _cards) _focusRects.Add(rect);
            if (_noStake) _focusRects.Add(_noStake);
            Controls.SetFocusCandidates(_focusRects);

            // The gate is used with the same press that could read as a tap on this frame.
            if (Time.frameCount == _openedFrame) return;

            if (Controls.CancelTapped)
            {
                var back = _onCancel;
                Close();
                back?.Invoke();
                return;
            }

            if (!Controls.Tapped(out var at)) return;

            foreach (var (rect, item) in _cards)
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) { Finish(item); return; }

            if (_noStake && RectTransformUtility.RectangleContainsScreenPoint(_noStake, at, null))
                Finish(null);
        }

        void Finish(MintedGearRecord chosen)
        {
            var go = _onChoose;
            Close();
            go?.Invoke(chosen);
        }
    }
}
