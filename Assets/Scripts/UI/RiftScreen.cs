using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Chain;
using Convergence.Core;
using Convergence.Rifts;

namespace Convergence.UI
{
    /// <summary>
    /// The choice a Rift asks: leave with everything, or push on and secure only what fits.
    ///
    /// FRAMED AROUND CONSEQUENCE, NOT AROUND INVENTORY. Two columns - what comes out with you, and
    /// what rides on what happens next - because those are the two futures the player is choosing
    /// between. A plain list with checkboxes would present the same information and ask a
    /// bookkeeping question instead of a nerve question.
    ///
    /// THE COST OF PUSHING ON IS STATED, NOT DISCOVERED. The screen says outright that anything
    /// left carried is lost on death, because a player learning that from having it happen has
    /// learned it too late to have made the decision. Same reasoning as FloorRewardScreen naming
    /// both of its costs up front.
    ///
    /// ASKED FOR, AND DISMISSIBLE. It opens on [ E ] at the tear, never by walking into it, and
    /// NOT YET (or Escape) closes it with the tear still open. The Rift stands beside the floor's
    /// exit door, so going on is the door, not an answer here - and a player who only wants to
    /// keep fighting must never be stopped by a screen to say so. It used to open on contact and
    /// could not be backed out of, which made brushing past a tear a forced decision.
    /// </summary>
    public class RiftScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        GameObject _root;
        RunLoot _loot;
        Rift _rift;
        int _openedFrame = -1;
        int _capacity;
        Action _onExtract, _onPushOn;

        readonly List<(RectTransform Rect, Action Secure)> _cells = new();
        readonly List<RectTransform> _focusRects = new();
        RectTransform _extractBtn, _pushBtn;
        Text _securedTitle, _carriedTitle, _pushLabel, _subtitle;
        int _floor;

        public void Open(Transform canvas, RunLoot loot, int capacity, int floor, Rift rift,
                         Action onExtract, Action onPushOn)
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _rift = rift;
            _loot = loot;
            _capacity = capacity;
            _onExtract = onExtract;
            _onPushOn = onPushOn;
            GamePause.Hold(this);

            _root = new GameObject("RiftScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.04f, 0.035f, 0.06f, 0.96f));

            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -104), new Vector2(0, -44)),
                "THE RIFT", 42, new Color(0.80f, 0.72f, 1f), TextAnchor.MiddleCenter);

            _subtitle = UiKit.Label(UiKit.Rect(full, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -140), new Vector2(0, -104)),
                "", 19, new Color(0.55f, 0.52f, 0.65f), TextAnchor.MiddleCenter);
            _floor = floor;

            Build(full);
            Refresh();
        }

        void Build(RectTransform full)
        {
            // ---- left: what comes out ----
            var left = UiKit.Panel(full, new Vector2(0, 0), new Vector2(0.5f, 1),
                new Vector2(60, 190), new Vector2(-20, -180), new Color(0.08f, 0.11f, 0.09f, 1f));
            _securedTitle = UiKit.Label(UiKit.Rect(left, "h", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(20, -52), new Vector2(-20, -12)),
                "", 24, new Color(0.62f, 0.92f, 0.68f));

            // ---- right: what rides on the next floors ----
            var right = UiKit.Panel(full, new Vector2(0.5f, 0), new Vector2(1, 1),
                new Vector2(20, 190), new Vector2(-60, -180), new Color(0.13f, 0.09f, 0.09f, 1f));
            _carriedTitle = UiKit.Label(UiKit.Rect(right, "h", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(20, -52), new Vector2(-20, -12)),
                "", 24, new Color(0.95f, 0.62f, 0.55f));

            UiKit.Label(UiKit.Rect(right, "w", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(20, 12), new Vector2(-20, 52)),
                "lost if you die before the next Rift", 16,
                new Color(0.72f, 0.45f, 0.42f), TextAnchor.MiddleCenter);

            _leftBody = UiKit.Rect(left, "b", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(14, 14), new Vector2(-14, -58));
            _rightBody = UiKit.Rect(right, "b", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(14, 58), new Vector2(-14, -58));

            // ---- the two answers ----
            _extractBtn = UiKit.Panel(full, new Vector2(0, 0), new Vector2(0.5f, 0),
                new Vector2(60, 40), new Vector2(-20, 168), new Color(0.14f, 0.26f, 0.17f, 1f));
            UiKit.Label(UiKit.Rect(_extractBtn, "l", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -34)),
                "LEAVE NOW", 26, new Color(0.72f, 0.96f, 0.76f), TextAnchor.MiddleCenter);
            UiKit.Label(UiKit.Rect(_extractBtn, "s", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 14), new Vector2(0, 48)),
                "the run ends and you keep everything", 16,
                new Color(0.52f, 0.70f, 0.56f), TextAnchor.MiddleCenter);

            _pushBtn = UiKit.Panel(full, new Vector2(0.5f, 0), new Vector2(1, 0),
                new Vector2(20, 40), new Vector2(-60, 168), new Color(0.26f, 0.16f, 0.14f, 1f));
            _pushLabel = UiKit.Label(UiKit.Rect(_pushBtn, "l", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -34)),
                "NOT YET", 26, new Color(0.98f, 0.76f, 0.68f), TextAnchor.MiddleCenter);
            UiKit.Label(UiKit.Rect(_pushBtn, "s", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 14), new Vector2(0, 48)),
                "the tear stays open until you take the door", 16,
                new Color(0.72f, 0.52f, 0.48f), TextAnchor.MiddleCenter);
        }

        RectTransform _leftBody, _rightBody;

        /// <summary>
        /// Redraws both columns.
        ///
        /// Rebuilt wholesale rather than diffed: securing a piece moves it between columns, and the
        /// screen is open for one decision - there is nothing here worth the bookkeeping of an
        /// incremental update.
        /// </summary>
        void Refresh()
        {
            foreach (var (rect, _) in _cells) if (rect) Destroy(rect.gameObject);
            _cells.Clear();

            _securedTitle.text = $"COMING OUT   {_loot.SecuredCount + SecuredBoxCount()}";
            _carriedTitle.text = $"STILL AT RISK   {_loot.CarriedCount + FoundBoxCount()}";

            // TWO WAYS TO SECURE, and the screen keeps them apart. The Rift's own capacity is
            // free; past it every piece costs a Rift Box. Rolling them into one "you may take N"
            // number would hide the fact that the second kind is being SPENT - and a consumable
            // the player did not notice spending is a consumable they will be angry about later.
            int free = Mathf.Max(0, _capacity - SecuredThisRift);
            int boxes = _loot.TotalBoxes;
            bool canSecure = free > 0 || boxes > 0;

            _subtitle.text = $"floor {_floor}    -    it holds {_capacity} free"
                           + (boxes > 0 ? $", and you have {boxes} Rift Box{(boxes == 1 ? "" : "es")}" : "");

            _pushLabel.text = free > 0
                ? $"NOT YET   ({free} free left)"
                : boxes > 0 ? $"NOT YET   ({boxes} box{(boxes == 1 ? "" : "es")} left)"
                            : "NOT YET";

            Fill(_leftBody, SecuredRows());
            Fill(_rightBody, CarriedRows(canSecure, free));
        }

        // ------------------------------------------------------------------ rows

        /// <summary>One line of a column: a carried piece, or a stack of boxes of one kind.</summary>
        struct Row
        {
            public string Label;
            public Color Ink, Chip;
            public Art.BoxArt.Kind? Box;   // drawn instead of the tier chip
            public bool CostsBox;          // shows the Rift Box it will spend
            public Action Secure;          // null = not clickable
        }

        static readonly Color SecuredInk = new(0.72f, 0.78f, 0.74f);
        static readonly Color FreeInk = new(0.94f, 0.88f, 0.82f);
        static readonly Color PaidInk = new(0.80f, 0.72f, 1f);
        static readonly Color DeadInk = new(0.55f, 0.5f, 0.52f);

        static readonly Art.Gear.LootTier[] BoxTiers =
        {
            Art.Gear.LootTier.BlackDiamond, Art.Gear.LootTier.Diamond, Art.Gear.LootTier.Gold,
            Art.Gear.LootTier.Silver, Art.Gear.LootTier.Bronze,
        };

        /// <summary>A carried piece's row name, with its star level - drops arrive at up to three
        /// stars, and which piece to secure turns on it. Text, not star sprites: a row is a line.</summary>
        static string Named(MintedGearRecord it)
            => it.UpgradeLevel > 0 ? $"{it.DisplayName}  +{it.UpgradeLevel}" : it.DisplayName;

        static string BoxName(Art.Gear.LootTier tier)
            => (tier == Art.Gear.LootTier.BlackDiamond ? "Black Diamond" : tier.ToString()) + " box";

        int FoundBoxCount()
        {
            int n = _loot.FoundBoxes;
            foreach (var t in BoxTiers) n += _loot.FoundTieredBoxes(t);
            return n;
        }

        int SecuredBoxCount()
        {
            int n = _loot.SecuredRiftBoxes;
            foreach (var t in BoxTiers) n += _loot.SecuredTieredBoxes(t);
            return n;
        }

        List<Row> SecuredRows()
        {
            var rows = new List<Row>();
            foreach (var t in BoxTiers)
                if (_loot.SecuredTieredBoxes(t) > 0)
                    rows.Add(new Row { Label = $"{BoxName(t)}  x{_loot.SecuredTieredBoxes(t)}", Ink = SecuredInk,
                                       Box = BoxIcon.KindOf(t) });
            if (_loot.SecuredRiftBoxes > 0)
                rows.Add(new Row { Label = $"Rift Box  x{_loot.SecuredRiftBoxes}", Ink = SecuredInk,
                                   Box = Art.BoxArt.Kind.Rift });
            foreach (var it in _loot.Secured)
                rows.Add(new Row { Label = Named(it), Ink = SecuredInk, Chip = Art.Palette.Tier(it.Tier).Base });
            return rows;
        }

        /// <summary>
        /// The carried column. Boxes first, rarest first: a found box is usually worth more than a
        /// piece of gear, and a Diamond box below nine rows of Bronze would be scrolled past.
        /// A box or piece costs free capacity first, then a Rift Box - except a RIFT BOX itself,
        /// which only free capacity can secure (paying one box to keep one box is no trade).
        /// </summary>
        List<Row> CarriedRows(bool canSecure, int free)
        {
            var rows = new List<Row>();
            bool paid = free == 0;
            string price = paid ? "[ spend a Rift Box ]" : "[ secure ]";
            foreach (var t in BoxTiers)
            {
                int n = _loot.FoundTieredBoxes(t);
                if (n <= 0) continue;
                var tier = t;
                rows.Add(new Row
                {
                    Label = $"{BoxName(t)}  x{n}      {(canSecure ? price : "")}",
                    Ink = canSecure ? (paid ? PaidInk : FreeInk) : SecuredInk,
                    Box = BoxIcon.KindOf(t),
                    CostsBox = canSecure && paid,
                    Secure = canSecure ? () => Take(() => _loot.SecureTieredBox(tier)) : null,
                });
            }
            if (_loot.FoundBoxes > 0)
            {
                bool can = free > 0;
                rows.Add(new Row
                {
                    Label = $"Rift Box  x{_loot.FoundBoxes}      {(can ? "[ secure ]" : "[ needs free space ]")}",
                    Ink = can ? FreeInk : DeadInk,
                    Box = Art.BoxArt.Kind.Rift,
                    Secure = can ? () => Take(() => _loot.SecureRiftBox()) : null,
                });
            }
            foreach (var it in _loot.Carried)
            {
                var item = it;
                rows.Add(new Row
                {
                    Label = canSecure ? $"{Named(it)}      {price}" : Named(it),
                    Ink = canSecure ? (paid ? PaidInk : FreeInk) : SecuredInk,
                    Chip = Art.Palette.Tier(it.Tier).Base,
                    CostsBox = canSecure && paid,
                    Secure = canSecure ? () => Take(() => _loot.Secure(item)) : null,
                });
            }
            return rows;
        }

        /// <summary>
        /// Secures one thing: free capacity first, then a Rift Box. Never the other way round: a
        /// player who had both and spent a consumable while a free slot sat unused would rightly
        /// call that a bug.
        ///
        /// Move FIRST, then pay. Spending first needed a refund path if the move failed, and the
        /// refund could not know which pool it had taken from - SpendBox drains found before
        /// banked, so refunding into found would quietly convert a safe box into one that dies
        /// with the run. Ordering it this way deletes the case instead of handling it.
        /// </summary>
        void Take(Func<bool> move)
        {
            if (SecuredThisRift < _capacity)
            {
                if (move()) SecuredThisRift++;
            }
            else if (_loot.TotalBoxes > 0 && move())
            {
                _loot.SpendBox();
            }
        }

        /// <summary>How many have been pushed through this Rift's own capacity. Counted rather than
        /// inferred from SecuredCount, which includes everything banked at earlier Rifts - and
        /// held on the Rift, so reopening the screen does not reset it.</summary>
        int SecuredThisRift
        {
            get => _rift != null ? _rift.Secured : _securedNoRift;
            set { if (_rift != null) _rift.Secured = value; else _securedNoRift = value; }
        }
        int _securedNoRift;

        void Fill(RectTransform parent, List<Row> rows)
        {
            const float h = 54f, gap = 8f;
            for (int i = 0; i < rows.Count && i < 9; i++)
            {
                var row = rows[i];
                float y = -i * (h + gap);
                var card = UiKit.Panel(parent, new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(0, y - h), new Vector2(0, y),
                    row.Secure != null ? new Color(0.20f, 0.14f, 0.13f, 1f) : new Color(0.11f, 0.13f, 0.12f, 1f));

                float textLeft = 32f;
                if (row.Box.HasValue)
                {
                    // The box itself at in-game density, standing on the row's floor.
                    BoxIcon.Add(card, row.Box.Value, false, new Vector2(0f, 0f), new Vector2(48f, 0f));
                    textLeft = 96f;
                }
                else
                {
                    var chip = UiKit.Rect(card, "c", new Vector2(0, 0), new Vector2(0, 1),
                        new Vector2(10, 10), new Vector2(20, -10));
                    chip.gameObject.AddComponent<Image>().color = row.Chip;
                }

                UiKit.Label(UiKit.Rect(card, "n", new Vector2(0, 0), new Vector2(1, 1),
                    new Vector2(textLeft, 0), new Vector2(-12, 0)),
                    row.Label, 19, row.Ink);

                // A row that costs a box shows the box it costs, at in-game density (the size it
                // was seen at, picked up mid-fight), standing on the row's floor at its right end.
                // It is a few texels taller than the row and peeks into the gap above - never
                // scaled down to fit, which would put it off its grid.
                if (row.CostsBox)
                    BoxIcon.Add(card, Art.BoxArt.Kind.Rift, false, new Vector2(1f, 0f), new Vector2(-52f, 0f));

                if (row.Secure != null) _cells.Add((card, row.Secure));
            }

            if (rows.Count > 9)
                UiKit.Label(UiKit.Rect(parent, "more", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(0, -9 * (h + gap) - 40), new Vector2(0, -9 * (h + gap))),
                    $"+{rows.Count - 9} more", 17, new Color(0.5f, 0.5f, 0.55f), TextAnchor.MiddleCenter);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _cells.Clear();
            _rift = null;
            _securedNoRift = 0;
        }

        void Update()
        {
            if (!IsOpen) return;

            _focusRects.Clear();
            foreach (var (rect, _) in _cells) _focusRects.Add(rect);
            if (_extractBtn) _focusRects.Add(_extractBtn);
            if (_pushBtn) _focusRects.Add(_pushBtn);
            Core.Controls.SetFocusCandidates(_focusRects);

            // The [ E ] that opened this is still down on its opening frame.
            if (Time.frameCount == _openedFrame) return;

            // Backing out is NOT YET - see the class header.
            if (Core.Controls.CancelTapped)
            {
                var stay = _onPushOn;
                Close();
                stay?.Invoke();
                return;
            }

            if (!Core.Controls.Tapped(out var at)) return;

            foreach (var (rect, secure) in _cells)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) continue;
                secure();
                Refresh();
                return;
            }

            if (_extractBtn != null
                && RectTransformUtility.RectangleContainsScreenPoint(_extractBtn, at, null))
            {
                var go = _onExtract;
                Close();
                go?.Invoke();
                return;
            }

            if (_pushBtn != null
                && RectTransformUtility.RectangleContainsScreenPoint(_pushBtn, at, null))
            {
                var go = _onPushOn;
                Close();
                go?.Invoke();
            }
        }
    }
}
