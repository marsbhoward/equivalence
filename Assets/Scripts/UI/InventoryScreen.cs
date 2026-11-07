using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;
using Convergence.Rifts;

namespace Convergence.UI
{
    /// <summary>
    /// A read-only look at what the run is carrying, reached from the pause menu.
    ///
    /// CARRIED loot is the run's stake - lost on death, or on leaving without reaching a Rift -
    /// and SECURED loot is already banked and cannot be lost. This screen only SHOWS that split;
    /// the decision that moves a piece across it is the Rift's (see RiftScreen). Framed the same
    /// way it is: at-risk on the left, safe on the right.
    ///
    /// Dismissible, so it carries a BACK button and closes on Cancel, and its close hands control
    /// straight back to the pause menu.
    ///
    /// IN A RUN it is two grids, the Rift screen's style (InventoryScreen.Run.cs, the user's
    /// design, 2026-11-06): what is carried, the bag's empty places drawn, over what is secured. A
    /// carried piece is selected, then DROPPED to the floor beside the player, where it lies until
    /// the floor is left - the bag has a size (Tuning.Boss.CarrySlots), so making room has to be
    /// possible. A carried Rift Box drops too, behind a confirm, because a dropped one is gone
    /// (RiftBoxPickup.DropFleeting).
    ///
    /// IN THE HUB (<see cref="OpenHub"/>, the user's call 2026-11-02) it shows what the character
    /// holds - its Rift Boxes and design boxes - and chooses how many Rift Boxes come into the next
    /// run. Those come in AT RISK, carried like a box found on the run; the rest stay home, safe
    /// and out of the run's reach (RunLoot.StartBoxes). Reached from the crate's second button and
    /// the inventory key; the hub's themes, which that button used to open, are a button here.
    /// </summary>
    public partial class InventoryScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        GameObject _root;
        int _openedFrame = -1;
        RunLoot _loot;
        System.Action _onClosed;
        RectTransform _backRect;
        readonly List<RectTransform> _focus = new();

        // ---- dropping, in a run ----
        Transform _canvas;
        System.Action<MintedGearRecord> _onDropGear;
        System.Action<System.Action> _onDropBox;

        // ---- the hub's version ----
        bool _hub;
        CharacterProfile _profile;
        System.Func<int> _getBring;
        System.Action<int> _setBring;
        System.Action _onThemes;
        RectTransform _lessRect, _moreRect, _themesRect;
        Text _bringText, _bringNote;

        /// <param name="onDropGear">Lays a carried piece at the player's feet (already out of the
        /// bag), or null for a look-only bag.</param>
        /// <param name="onDropBox">Asks to throw a carried Rift Box away; this screen has closed
        /// first, and the argument is its own onClosed, for reopening it if the answer is no.</param>
        public void Open(Transform canvas, RunLoot loot, System.Action onClosed = null,
                         System.Action<MintedGearRecord> onDropGear = null,
                         System.Action<System.Action> onDropBox = null)
        {
            if (IsOpen) return;
            IsOpen = true;
            _hub = false;
            _openedFrame = Time.frameCount;
            _loot = loot;
            _onClosed = onClosed;
            _canvas = canvas;
            _onDropGear = onDropGear;
            _onDropBox = onDropBox;
            GamePause.Hold(this);
            BuildRun(canvas);
        }

        /// <summary>The hub's inventory: what <paramref name="profile"/> holds, and the Rift Boxes
        /// it will bring into the next run (<paramref name="getBring"/>/<paramref name="setBring"/>,
        /// held by GameBootstrap and spent at the run's start).</summary>
        public void OpenHub(Transform canvas, CharacterProfile profile, System.Func<int> getBring,
                            System.Action<int> setBring, System.Action onThemes, System.Action onClosed = null)
        {
            if (IsOpen || profile == null) return;
            IsOpen = true;
            _hub = true;
            _openedFrame = Time.frameCount;
            _loot = null;
            _profile = profile;
            _getBring = getBring;
            _setBring = setBring;
            _onThemes = onThemes;
            _onClosed = onClosed;
            GamePause.Hold(this);
            BuildHub(canvas);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _backRect = _lessRect = _moreRect = _themesRect = null;
            CloseRun();
            _onDropGear = null;
            _onDropBox = null;
            _bringText = _bringNote = null;
            _profile = null;
            _getBring = null;
            _setBring = null;

            var cb = _onClosed;
            _onClosed = null;
            cb?.Invoke();
        }

        // ------------------------------------------------------------------ ui

        // ------------------------------------------------------------------ the hub

        static readonly Color Ink = new(0.82f, 0.85f, 0.92f);
        static readonly Color Dim = new(0.55f, 0.57f, 0.66f);
        static readonly Color Risk = new(0.95f, 0.62f, 0.55f);
        static readonly Color Button = new(0.12f, 0.13f, 0.17f, 1f);

        static readonly Art.Gear.LootTier[] DesignTiers =
        {
            Art.Gear.LootTier.Bronze, Art.Gear.LootTier.Silver, Art.Gear.LootTier.Gold,
            Art.Gear.LootTier.Diamond, Art.Gear.LootTier.BlackDiamond,
        };

        int Held => Mathf.Max(0, _profile?.RiftBoxes ?? 0);
        int Bring => Mathf.Clamp(_getBring?.Invoke() ?? 0, 0, Held);

        void BuildHub(Transform canvas)
        {
            _root = new GameObject("InventoryScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.04f, 0.04f, 0.055f, 0.97f));

            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -102), new Vector2(0, -44)),
                "INVENTORY", 42, new Color(0.90f, 0.91f, 0.96f), TextAnchor.MiddleCenter);
            UiKit.Label(UiKit.Rect(full, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -140), new Vector2(0, -102)),
                $"what {_profile.DisplayName} holds", 18, Dim, TextAnchor.MiddleCenter);

            // ---- the Rift Boxes, and how many come along ----
            var mid = new Vector2(0.5f, 0.5f);
            var panel = UiKit.Panel(full, mid, mid, new Vector2(-520, -60), new Vector2(520, 360),
                new Color(0.09f, 0.09f, 0.12f, 1f));
            var icon = BoxIcon.Add(panel, Art.BoxArt.Kind.Rift, true, new Vector2(0f, 1f), new Vector2(120f, -190f));
            if (Held == 0) icon.color = new Color(1f, 1f, 1f, 0.3f);
            UiKit.Label(UiKit.Rect(panel, "held", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(230, -150), new Vector2(-30, -24)),
                $"RIFT BOXES   x{Held}\n<size=16>Secure past a Rift's capacity, promote a three-star " +
                "piece, or insure a stake.</size>", 26, Held > 0 ? Ink : Dim, TextAnchor.MiddleLeft)
                .horizontalOverflow = HorizontalWrapMode.Wrap;

            UiKit.Label(UiKit.Rect(panel, "bh", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(230, -260), new Vector2(560, -190)),
                "BRING INTO THE NEXT RUN", 20, Ink, TextAnchor.MiddleLeft);
            _lessRect = UiKit.Panel(panel, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(580, -290), new Vector2(580 + UiKit.TouchTarget, -290 + UiKit.TouchTarget), Button);
            UiKit.Label(_lessRect, "-", 30, Ink, TextAnchor.MiddleCenter);
            _bringText = UiKit.Label(UiKit.Rect(panel, "n", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(580 + UiKit.TouchTarget, -290), new Vector2(700 + UiKit.TouchTarget, -290 + UiKit.TouchTarget)),
                "", 30, Risk, TextAnchor.MiddleCenter);
            _moreRect = UiKit.Panel(panel, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(700 + UiKit.TouchTarget, -290), new Vector2(700 + 2 * UiKit.TouchTarget, -290 + UiKit.TouchTarget), Button);
            UiKit.Label(_moreRect, "+", 30, Ink, TextAnchor.MiddleCenter);
            _bringNote = UiKit.Label(UiKit.Rect(panel, "bn", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(30, 14), new Vector2(-30, 116)), "", 16, Dim, TextAnchor.MiddleCenter);
            _bringNote.horizontalOverflow = HorizontalWrapMode.Wrap;
            RefreshBring();

            // ---- the design boxes held, a row under it ----
            UiKit.Label(UiKit.Rect(full, "dh", mid, mid, new Vector2(-520, -110), new Vector2(520, -70)),
                "BOXES", 20, Dim, TextAnchor.MiddleCenter);
            for (int i = 0; i < DesignTiers.Length; i++)
            {
                var tier = DesignTiers[i];
                int n = _profile.Boxes?.Get(tier) ?? 0;
                float x = (i - (DesignTiers.Length - 1) * 0.5f) * 200f;
                var img = BoxIcon.Add(full, BoxIcon.KindOf(tier), true, mid, new Vector2(x, -270f));
                if (n == 0) img.color = new Color(1f, 1f, 1f, 0.3f);
                UiKit.Label(UiKit.Rect(full, "dn", mid, mid, new Vector2(x - 90, -315), new Vector2(x + 90, -275)),
                    $"x{n}", 22, n > 0 ? CharacterScreen.TierColor(tier) : Dim, TextAnchor.MiddleCenter);
            }

            // ---- hub themes and back ----
            if (_onThemes != null)
            {
                _themesRect = UiKit.Panel(full, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                    new Vector2(-420, 36), new Vector2(-20, 36 + UiKit.TouchTarget), Button);
                UiKit.Label(_themesRect, "HUB THEMES", 22, Ink, TextAnchor.MiddleCenter);
            }
            float bx = _onThemes != null ? 220f : 0f;
            _backRect = UiKit.Panel(full, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(bx - 200, 36), new Vector2(bx + 200, 36 + UiKit.TouchTarget), Button);
            UiKit.Label(_backRect, "BACK", 22, Ink, TextAnchor.MiddleCenter);
        }

        void RefreshBring()
        {
            int n = Bring;
            if (_bringText) _bringText.text = n.ToString();
            if (_bringNote)
                _bringNote.text = Held == 0
                    ? "None held. They drop from enemies on a run."
                    : n == 0
                        ? "All stay home, safe. The run can't spend them."
                        : $"{n} come in carried, as if found: lost on a death unless secured or extracted. " +
                          $"{Held - n} stay home, safe.";
        }

        void Step(int by)
        {
            int n = Mathf.Clamp(Bring + by, 0, Held);
            _setBring?.Invoke(n);
            RefreshBring();
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!IsOpen) return;
            if (Time.frameCount == _openedFrame) return;

            _focus.Clear();
            if (_lessRect) _focus.Add(_lessRect);
            if (_moreRect) _focus.Add(_moreRect);
            if (_themesRect) _focus.Add(_themesRect);
            if (!_hub) RunFocus(_focus);
            if (_backRect) _focus.Add(_backRect);
            Core.Controls.SetFocusCandidates(_focus);
            if (!_hub) RunHover();

            if (Core.Controls.CancelTapped || Core.Controls.InventoryTapped) { Close(); return; }

            if (!Core.Controls.Tapped(out var p)) return;
            bool Hit(RectTransform r) => r && RectTransformUtility.RectangleContainsScreenPoint(r, p, null);
            if (Hit(_backRect)) { Close(); return; }
            if (!_hub) { RunTap(p); return; }
            if (Hit(_lessRect)) Step(-1);
            else if (Hit(_moreRect)) Step(1);
            else if (Hit(_themesRect))
            {
                // The themes open over the room, as they did from the crate - this screen closes first.
                var themes = _onThemes;
                _onClosed = null;
                Close();
                themes?.Invoke();
            }
        }
    }
}
