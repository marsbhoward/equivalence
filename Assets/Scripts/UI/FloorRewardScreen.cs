using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Convergence.Combat;

namespace Convergence.UI
{
    /// <summary>
    /// Heal and Repair share one card slot - only one of them is offered on any given floor,
    /// chosen at random. They are both "patch yourself up" picks, so competing against each
    /// other wasted a slot that XP now occupies.
    /// </summary>
    public enum FloorReward { Heal, Repair, Moveset, Xp }

    /// <summary>What the player ended up doing with the floor's reward.</summary>
    public enum FloorOutcome
    {
        Heal,
        Repair,
        Xp,
        MovesetInto,   // SlotIndex says where
        Forfeit,       // backed out of the swap; the reward is spent either way
    }

    /// <summary>
    /// The between-floors screen, in three phases on one surface:
    ///
    ///   Choosing     - the three finisher slots sit above the three power-ups, purely to be read.
    ///                  The player can weigh what they already have before committing.
    ///   PickingSlot  - after taking the moveset, the slots light up as the choice.
    ///   Confirming   - a side-by-side of the outgoing finisher against the incoming one.
    ///
    /// Cancelling at the confirm step SPENDS the reward without replacing anything, so taking the
    /// moveset is a real commitment rather than a free look. That cost is spelled out on the
    /// button rather than left to be discovered.
    /// </summary>
    public class FloorRewardScreen : MonoBehaviour
    {
        enum Phase { Choosing, PickingSlot, Confirming }

        public bool IsOpen { get; private set; }

        Phase _phase;

        /// <summary>
        /// Whether a BACK press means anything here.
        ///
        /// It does NOT during Choosing or PickingSlot: the floor's reward has to be answered, and
        /// the only exit is cancelling at the confirm step, which spends it. The touch overlay
        /// asks so it can leave the button off rather than draw a dead one - a button that does
        /// nothing teaches the player that buttons here do nothing.
        /// </summary>
        public bool CanDismiss => _phase == Phase.Confirming;
        GameObject _root, _confirmPanel;
        Action<FloorOutcome, int> _done;

        Moveset _offered;
        IReadOnlyList<Moveset> _slots;

        /// <summary>
        /// Which slots refuse a replacement - a black-diamond weapon's signature finisher.
        /// A predicate rather than a second list so the screen cannot end up holding one that has
        /// drifted out of step with <see cref="_slots"/>.
        /// </summary>
        Func<int, bool> _slotLocked;
        int _rotationIndex;
        int _pendingSlot = -1;

        Text _prompt;
        readonly List<SlotCard> _slotCards = new();
        readonly List<PowerCard> _powerCards = new();
        readonly List<RectTransform> _focusRects = new();

        class SlotCard
        {
            public RectTransform Rect;
            public Image Bg, Accent, GlyphImg;
            public Text Name;
            public bool Locked;
        }

        class PowerCard
        {
            public RectTransform Rect;
            public Image Bg, Accent;
            public FloorReward Reward;
            public bool Enabled;
            public Color AccentColor;
        }

        // ------------------------------------------------------------------ open

        int _powerCount = 3;

        public void Show(Transform canvas, int floor, Moveset offered, IReadOnlyList<Moveset> slots,
                         Func<int, bool> slotLocked,
                         int rotationIndex, FloorReward restoreKind, string restoreText,
                         string xpText, string xpBlurb, Action<FloorOutcome, int> done,
                         string otherRestoreText = null)
        {
            // Curator (the exchange): one more card - the restore the roll did not pick.
            _powerCount = otherRestoreText != null ? 4 : 3;
            if (IsOpen) return;
            IsOpen = true;
            _phase = Phase.Choosing;
            _offered = offered;
            _slots = slots;
            _slotLocked = slotLocked ?? (_ => false);
            _rotationIndex = rotationIndex;
            _done = done;
            _pendingSlot = -1;
            _slotCards.Clear();
            _powerCards.Clear();

            Core.GamePause.Hold(this);

            _root = new GameObject("FloorReward", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.035f, 0.05f, 1f));

            var title = UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -108), new Vector2(0, -46));
            UiKit.Label(title, $"FLOOR {floor} CLEARED", 42,
                new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleCenter);

            var promptRow = UiKit.Rect(full, "p", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -146), new Vector2(0, -110));
            _prompt = UiKit.Label(promptRow, "", 19, new Color(0.55f, 0.58f, 0.66f), TextAnchor.MiddleCenter);

            // ---- finisher slots, above ----
            var slotsLabel = UiKit.Rect(full, "sl", new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                new Vector2(0, 268), new Vector2(0, 296));
            UiKit.Label(slotsLabel, "YOUR WEAPON ARTS", 16,
                new Color(0.42f, 0.45f, 0.52f), TextAnchor.MiddleCenter);

            for (int i = 0; i < slots.Count; i++) BuildSlotCard(full, i, slots[i] ?? MovesetLibrary.Default);

            // ---- power-ups, below ----
            var powerLabel = UiKit.Rect(full, "pl", new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                new Vector2(0, 26), new Vector2(0, 54));
            UiKit.Label(powerLabel, "FLOOR REWARD", 16,
                new Color(0.42f, 0.45f, 0.52f), TextAnchor.MiddleCenter);

            bool healing = restoreKind == FloorReward.Heal;
            BuildPowerCard(full, 0, restoreKind,
                healing ? "SMALL HEAL" : "REPAIR GEAR", restoreText,
                healing
                    ? "Patches you up. Does nothing for worn equipment."
                    : "Restores worn armour and weapons. Does nothing for your health.",
                healing ? new Color(0.45f, 0.85f, 0.55f) : new Color(0.55f, 0.75f, 1f), true);

            BuildPowerCard(full, 1, FloorReward.Moveset,
                offered != null ? "NEW WEAPON ART" : "NO WEAPON ART",
                offered != null ? offered.DisplayName : "-",
                offered != null
                    ? "Replaces one of your three weapon arts."
                    : "You already hold every weapon art.",
                new Color(0.95f, 0.78f, 0.32f), offered != null);

            BuildPowerCard(full, 2, FloorReward.Xp, "EXPERIENCE", xpText, xpBlurb,
                new Color(0.78f, 0.55f, 0.95f), true);

            if (otherRestoreText != null)
                BuildPowerCard(full, 3, healing ? FloorReward.Repair : FloorReward.Heal,
                    healing ? "REPAIR GEAR" : "SMALL HEAL", otherRestoreText,
                    healing
                        ? "Restores worn armour and weapons. Does nothing for your health."
                        : "Patches you up. Does nothing for worn equipment.",
                    healing ? new Color(0.55f, 0.75f, 1f) : new Color(0.45f, 0.85f, 0.55f), true);

            SetPhase(Phase.Choosing);
        }

        // ------------------------------------------------------------------ cards

        void BuildSlotCard(RectTransform parent, int index, Moveset current)
        {
            // Centred on the ACTUAL count, not on three. The row used to hardcode the three-card
            // centre, so a fourth slot pushed the whole row 154 units right of the screen's middle
            // and a fifth pushed it 308.
            int n = Mathf.Max(1, _slots != null ? _slots.Count : 3);
            float w = n >= 5 ? 218f : n == 4 ? 252f : 286f;
            const float gap = 22f;
            float x = -(n * w + (n - 1) * gap) / 2f + index * (w + gap);

            var card = UiKit.Panel(parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, 76), new Vector2(x + w, 258), new Color(0.09f, 0.10f, 0.14f, 1f));

            var accentRt = UiKit.Rect(card, "a", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -5), new Vector2(0, 0));
            var accent = accentRt.gameObject.AddComponent<Image>();

            var head = UiKit.Rect(card, "h", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(14, -32), new Vector2(-14, -8));
            bool locked = _slotLocked != null && _slotLocked(index);
            UiKit.Label(head, $"SLOT {index + 1}"
                            + (locked ? "   (locked)" : index == _rotationIndex ? "   (up next)" : ""),
                14, new Color(0.45f, 0.48f, 0.56f));

            var glyphRt = UiKit.Rect(card, "g", new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(16, 44), new Vector2(84, -40));
            var glyphImg = glyphRt.gameObject.AddComponent<Image>();
            glyphImg.sprite = Glyphs.Get(current.FinisherGlyph);
            glyphImg.preserveAspect = true;
            glyphImg.raycastTarget = false;

            var nameRt = UiKit.Rect(card, "n", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(96, -74), new Vector2(-12, -34));
            var name = UiKit.Label(nameRt, current.DisplayName, 21, new Color(0.9f, 0.92f, 0.96f));

            var statRt = UiKit.Rect(card, "s", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(96, 12), new Vector2(-12, -78));
            var stats = UiKit.Label(statRt, current.FinisherDescription, 14,
                new Color(0.55f, 0.58f, 0.66f));
            stats.horizontalOverflow = HorizontalWrapMode.Wrap;

            if (locked)
            {
                // Stated on the card, not left to be discovered by clicking and having nothing
                // happen - the same rule the reward cards follow when one is unavailable.
                name.color = new Color(0.62f, 0.55f, 0.78f);
                stats.text = "This weapon's signature art. It cannot be replaced.";
            }

            _slotCards.Add(new SlotCard
            {
                Rect = card, Bg = card.GetComponent<Image>(), Accent = accent,
                GlyphImg = glyphImg, Name = name, Locked = locked,
            });
        }

        void BuildPowerCard(RectTransform parent, int index, FloorReward reward, string heading,
                            string value, string blurb, Color accentColor, bool enabled)
        {
            // Centred on however many cards there are - three, or four under Curator.
            float w = _powerCount >= 4 ? 268f : 316f;
            const float gap = 24f;
            float x = -(_powerCount * w + (_powerCount - 1) * gap) / 2f + index * (w + gap);

            var card = UiKit.Panel(parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, -286), new Vector2(x + w, 6),
                enabled ? new Color(0.09f, 0.10f, 0.14f, 1f) : new Color(0.06f, 0.065f, 0.08f, 1f));

            var accentRt = UiKit.Rect(card, "a", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -5), new Vector2(0, 0));
            var accent = accentRt.gameObject.AddComponent<Image>();
            accent.color = enabled ? accentColor : new Color(0.2f, 0.21f, 0.26f);

            var head = UiKit.Rect(card, "h", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18, -56), new Vector2(-18, -20));
            UiKit.Label(head, heading, 20, enabled ? accentColor : new Color(0.35f, 0.37f, 0.43f),
                TextAnchor.MiddleCenter);

            var val = UiKit.Rect(card, "v", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18, -106), new Vector2(-18, -58));
            UiKit.Label(val, value, 26,
                enabled ? new Color(0.9f, 0.92f, 0.96f) : new Color(0.35f, 0.37f, 0.43f),
                TextAnchor.MiddleCenter);

            // For the finisher card, show what it actually does.
            if (reward == FloorReward.Moveset && _offered != null)
            {
                var gRt = UiKit.Rect(card, "g", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(-27, -160), new Vector2(27, -106));
                var gi = gRt.gameObject.AddComponent<Image>();
                gi.sprite = Glyphs.Get(_offered.FinisherGlyph);
                gi.color = accentColor;
                gi.preserveAspect = true;
                gi.raycastTarget = false;

                var sRt = UiKit.Rect(card, "s", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(18, -252), new Vector2(-18, -162));
                var sTxt = UiKit.Label(sRt, _offered.FinisherDescription, 15,
                    new Color(0.62f, 0.65f, 0.72f), TextAnchor.UpperCenter);
                sTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            var b = UiKit.Rect(card, "b", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(20, 46), new Vector2(-20, reward == FloorReward.Moveset ? -256 : -196));
            var blurbTxt = UiKit.Label(b, blurb, 15,
                enabled ? new Color(0.55f, 0.58f, 0.66f) : new Color(0.3f, 0.32f, 0.38f),
                TextAnchor.UpperCenter);
            blurbTxt.horizontalOverflow = HorizontalWrapMode.Wrap;

            var key = UiKit.Rect(card, "k", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(18, 12), new Vector2(-18, 42));
            UiKit.Label(key, enabled ? $"[ {index + 1} ]" : "unavailable", 19,
                enabled ? accentColor : new Color(0.3f, 0.32f, 0.38f), TextAnchor.MiddleCenter);

            _powerCards.Add(new PowerCard
            {
                Rect = card, Bg = card.GetComponent<Image>(), Accent = accent,
                Reward = reward, Enabled = enabled, AccentColor = accentColor,
            });
        }

        // ------------------------------------------------------------------ phases

        void SetPhase(Phase phase)
        {
            _phase = phase;

            bool picking = phase == Phase.PickingSlot;

            foreach (var s in _slotCards)
            {
                s.Accent.color = picking ? new Color(0.95f, 0.78f, 0.32f) : new Color(0.24f, 0.26f, 0.32f);
                s.Bg.color = picking ? new Color(0.14f, 0.13f, 0.09f, 1f) : new Color(0.09f, 0.10f, 0.14f, 1f);
                s.Name.color = picking ? new Color(1f, 0.93f, 0.75f) : new Color(0.9f, 0.92f, 0.96f);
                s.GlyphImg.color = picking ? new Color(0.95f, 0.78f, 0.32f) : new Color(0.6f, 0.63f, 0.7f);
            }

            // Dim the rewards once the choice has moved on to the slots.
            foreach (var p in _powerCards)
            {
                float k = picking ? 0.4f : 1f;
                p.Bg.color = p.Enabled
                    ? new Color(0.09f * k, 0.10f * k, 0.14f * k, 1f)
                    : new Color(0.06f, 0.065f, 0.08f, 1f);
                p.Accent.color = p.Enabled
                    ? (picking ? p.AccentColor * 0.35f : p.AccentColor)
                    : new Color(0.2f, 0.21f, 0.26f);
            }

            _prompt.text = phase switch
            {
                Phase.PickingSlot => "which weapon art does it replace?",
                Phase.Confirming  => "",
                // Say up front that the pick is binding, since it is - and that the ORDER isn't,
                // so a PickingSlot choice doesn't feel more final than it actually is.
                _                 => "inspect your weapon arts, then choose a reward    -    your pick is final    -    " +
                                     "you can reorder your weapon arts at the door ahead",
            };
        }

        // ------------------------------------------------------------------ confirm

        void ShowConfirm(int slotIndex)
        {
            _pendingSlot = slotIndex;
            SetPhase(Phase.Confirming);

            var outgoing = _slots[slotIndex] ?? MovesetLibrary.Default;

            _confirmPanel = new GameObject("Confirm", typeof(RectTransform));
            _confirmPanel.transform.SetParent(_root.transform, false);
            var full = (RectTransform)_confirmPanel.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.025f, 0.04f, 0.985f));

            // Deeper by what the buttons gained. The comparison above hangs off the box's TOP
            // edge, so growing downward leaves it exactly where it was.
            var box = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-470, -212), new Vector2(470, 250), new Color(0.08f, 0.09f, 0.12f, 1f));

            var t = UiKit.Rect(box, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -74), new Vector2(0, -24));
            UiKit.Label(t, $"REPLACE SLOT {slotIndex + 1}?", 34,
                new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleCenter);

            BuildComparison(box, outgoing, _offered);

            // Confirm / cancel. The cancel label states the cost outright.
            var confirm = UiKit.Panel(box, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-330, 26), new Vector2(-20, 26 + UiKit.TouchTarget),
                new Color(0.16f, 0.22f, 0.15f, 1f));
            UiKit.Label(UiKit.Rect(confirm, "c", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                "[ENTER]  CONFIRM SWAP", 20, new Color(0.6f, 0.92f, 0.65f), TextAnchor.MiddleCenter);
            _confirmRect = confirm;

            var cancel = UiKit.Panel(box, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(20, 26), new Vector2(330, 26 + UiKit.TouchTarget),
                new Color(0.22f, 0.15f, 0.15f, 1f));
            UiKit.Label(UiKit.Rect(cancel, "c", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                "CANCEL", 20, new Color(0.92f, 0.62f, 0.58f), TextAnchor.MiddleCenter);
            _cancelRect = cancel;

            var warn = UiKit.Rect(box, "w", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(20, 2), new Vector2(-20, 24));
            UiKit.Label(warn, "cancelling keeps your weapon arts but still spends this floor's reward",
                14, new Color(0.55f, 0.45f, 0.45f), TextAnchor.MiddleCenter);
        }

        RectTransform _confirmRect, _cancelRect;

        void BuildComparison(RectTransform box, Moveset outgoing, Moveset incoming)
        {
            Column(box, -450, "CURRENT", outgoing, new Color(0.62f, 0.65f, 0.72f));
            Column(box, 30, "INCOMING", incoming, new Color(0.95f, 0.78f, 0.32f));

            // A divider rather than a delta column: these are two different moves, not the same
            // move with different numbers, and reading them side by side is the actual comparison.
            var div = UiKit.Rect(box, "div", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-1, -300), new Vector2(1, -96));
            div.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.21f, 0.26f);
        }

        void Column(RectTransform box, float x, string heading, Moveset ms, Color tint)
        {
            var head = UiKit.Rect(box, "h", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(x, -114), new Vector2(x + 420, -86));
            UiKit.Label(head, heading, 15, new Color(0.42f, 0.45f, 0.52f));

            var gRt = UiKit.Rect(box, "g", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(x, -158), new Vector2(x + 48, -110));
            var gi = gRt.gameObject.AddComponent<Image>();
            gi.sprite = Glyphs.Get(ms.FinisherGlyph);
            gi.color = tint;
            gi.preserveAspect = true;

            var n = UiKit.Rect(box, "n", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(x + 60, -156), new Vector2(x + 420, -112));
            UiKit.Label(n, ms.DisplayName, 24, tint);

            var d = UiKit.Rect(box, "d", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(x, -286), new Vector2(x + 420, -170));
            var dTxt = UiKit.Label(d, ms.FinisherDescription, 17, new Color(0.8f, 0.83f, 0.9f));
            dTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!IsOpen) return;
            bool click = Core.Controls.Tapped(out var p);

            // D-pad navigation - a different candidate set per phase, since PickingSlot's cards
            // replace Choosing's entirely rather than sitting alongside them (see the phase's own
            // doc comment: there is no route back). Rebuilt every frame rather than cached, since
            // the locked-slot filter below can change as soon as a signature finisher locks one.
            _focusRects.Clear();
            switch (_phase)
            {
                case Phase.Choosing:
                    foreach (var c in _powerCards) _focusRects.Add(c.Rect);
                    break;
                case Phase.PickingSlot:
                    foreach (var s in _slotCards) if (!s.Locked) _focusRects.Add(s.Rect);
                    break;
                case Phase.Confirming:
                    if (_confirmRect != null) _focusRects.Add(_confirmRect);
                    if (_cancelRect != null) _focusRects.Add(_cancelRect);
                    break;
            }
            Core.Controls.SetFocusCandidates(_focusRects);

            switch (_phase)
            {
                case Phase.Choosing:
                    for (int i = 0; i < _powerCards.Count; i++)
                        if (Core.Controls.DigitTapped(i)) { TakeReward(i); return; }
                    if (click)
                        for (int i = 0; i < _powerCards.Count; i++)
                            if (Hit(_powerCards[i].Rect, p)) { TakeReward(i); return; }
                    break;

                case Phase.PickingSlot:
                    // No route back to the reward cards: picking a card is binding. The only way
                    // out from here is cancelling at the confirm step, which spends the reward.
                    for (int i = 0; i < _slotCards.Count; i++)
                        if (!_slotCards[i].Locked && Core.Controls.DigitTapped(i)) { ShowConfirm(i); return; }
                    if (click)
                        for (int i = 0; i < _slotCards.Count; i++)
                            if (!_slotCards[i].Locked && Hit(_slotCards[i].Rect, p)) { ShowConfirm(i); return; }
                    break;

                case Phase.Confirming:
                    if (Core.Controls.ConfirmTapped)
                    { Finish(FloorOutcome.MovesetInto, _pendingSlot); return; }
                    if (Core.Controls.CancelTapped)
                    { Finish(FloorOutcome.Forfeit, -1); return; }
                    if (click && _confirmRect != null && Hit(_confirmRect, p))
                    { Finish(FloorOutcome.MovesetInto, _pendingSlot); return; }
                    if (click && _cancelRect != null && Hit(_cancelRect, p))
                    { Finish(FloorOutcome.Forfeit, -1); return; }
                    break;
            }
        }

        static bool Hit(RectTransform rt, Vector2 screenPoint)
            => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, null);

        void TakeReward(int index)
        {
            if (index < 0 || index >= _powerCards.Count) return;
            var card = _powerCards[index];
            if (!card.Enabled) return;

            switch (card.Reward)
            {
                case FloorReward.Heal:    Finish(FloorOutcome.Heal, -1); break;
                case FloorReward.Repair:  Finish(FloorOutcome.Repair, -1); break;
                case FloorReward.Xp:      Finish(FloorOutcome.Xp, -1); break;
                case FloorReward.Moveset: SetPhase(Phase.PickingSlot); break;
            }
        }

        void Finish(FloorOutcome outcome, int slot)
        {
            var done = _done;
            Close();
            done?.Invoke(outcome, slot);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Core.GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null; _confirmPanel = null; _confirmRect = null; _cancelRect = null;
            _slotCards.Clear();
            _powerCards.Clear();
        }
    }
}
