using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;
using Convergence.Art.Gear;
using Convergence.Chain;

namespace Convergence.UI
{
    /// <summary>
    /// The Forge, in full: the original free-voucher redemption, box-fuelled random and targeted
    /// redemption, COMBINING two matching pieces (the next star, or from three stars the next
    /// tier), and RE-ROLLING one sub-stat for boxes. One screen, five tabs.
    ///
    /// Combining never commits from a single tap. It walks group -> the two pieces -> a PREVIEW
    /// that states what carries over, what powers up, and how many slots will be randomized
    /// (those show only as "??? (?-?)" - nothing random is rolled until CONFIRM, or reopening the
    /// menu would be a free re-roll) -> the RESULT, so the player sees what actually rolled.
    /// </summary>
    public class ForgeScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        enum Mode { Vouchers, Redeem, Targeted, Combine, Reroll, Fuse }

        /// <summary>Every slot the Forge can roll into. Relic is excluded - GearRoller has no
        /// pool for it (cosmetic only, same rule a weapon transmog already follows), so redeeming
        /// into it would hand back a blank item. Trinket is excluded too: it merged into Relic and
        /// no item declares that slot any more, so redeeming into it would be a wasted voucher.</summary>
        static readonly GearSlot[] Slots =
        {
            GearSlot.Head, GearSlot.Shoulders, GearSlot.Torso, GearSlot.Back,
            GearSlot.Neck, GearSlot.Ring, GearSlot.Legs,
            GearSlot.Boots, GearSlot.Belt, GearSlot.Gloves, GearSlot.Weapon,
        };

        /// <summary>Same list, exposed for RedeemForgeBoxRandom's uniform-random pick.</summary>
        public static readonly GearSlot[] RedeemableSlots = Slots;

        static readonly LootTier[] BoxTiers =
        {
            LootTier.Bronze, LootTier.Silver, LootTier.Gold, LootTier.Diamond, LootTier.BlackDiamond,
        };

        static readonly WeaponClass[] Classes = (WeaponClass[])Enum.GetValues(typeof(WeaponClass));

        /// <summary>Everything the screen needs to know about the player's current Forge
        /// standing, and every action it can trigger. Bundled rather than a long parameter list,
        /// since five tabs each need a different slice of it.</summary>
        public struct Context
        {
            public int VoucherCount;
            public LootBoxes Boxes;
            /// <summary>Read live, not copied - a re-roll can spend Rift Boxes (as Silver) while
            /// the screen stays open.</summary>
            public Func<int> RiftBoxes;
            public List<MintedGearRecord> MintedGear;
            public Func<string, bool> IsEquipped;
            public Action<GearSlot> OnPickVoucher;
            public Action<LootTier> OnRedeemRandom;
            public Action<GearSlot, LootTier, WeaponClass> OnRedeemTargeted;
            /// <summary>Combines two pieces; returns the new one, or null if refused.</summary>
            public Func<string, string, MintedGearRecord> OnCombine;
            /// <summary>Re-rolls sub-stat (index) of a piece - true to change the stat itself;
            /// returns the piece, or null if refused (not enough boxes).</summary>
            public Func<string, int, bool, MintedGearRecord> OnReroll;
            /// <summary>Burns a fusion's pieces (by fusion id); returns the minted weapon, or null
            /// if refused.</summary>
            public Func<string, MintedGearRecord> OnFuse;
        }

        Context _ctx;
        GameObject _root;
        Transform _content;
        Mode _mode = Mode.Vouchers;

        // Multi-step picks within Redeem/Targeted, reset whenever the tab or a step changes.
        LootTier? _pickedTier;
        GearSlot? _pickedSlot;

        // Combine / Re-roll steps - see ResetSteps.
        string _groupKey;
        MintedGearRecord _pickA;
        GearForge.CombinePlan _plan;
        MintedGearRecord _result;
        int _resultNewFrom;
        MintedGearRecord _rerollItem;
        string _rerollNote;
        int _page;
        GearForge.Fusion _fusion;
        MintedGearRecord _fused;

        void ResetSteps()
        {
            _pickedTier = null; _pickedSlot = null;
            _groupKey = null; _pickA = null; _plan = null; _result = null;
            _rerollItem = null; _rerollNote = null; _page = 0;
            _fusion = null; _fused = null;
        }

        readonly List<RectTransform> _focusRects = new();
        readonly List<(RectTransform Rect, Action OnTap)> _actions = new();

        public void Open(Transform canvas, Context ctx)
        {
            if (IsOpen) return;
            IsOpen = true;
            _ctx = ctx;
            _mode = Mode.Vouchers;
            ResetSteps();
            GamePause.Hold(this);

            _root = new GameObject("ForgeScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.032f, 0.045f, 0.97f));

            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -108), new Vector2(0, -48)),
                "THE FORGE", 40, new Color(0.95f, 0.86f, 0.72f), TextAnchor.MiddleCenter);

            _full = full;
            _tabs = null;
            BuildTabs(full);

            _content = UiKit.Rect(full, "content", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(0, 60), new Vector2(0, -190));

            Rebuild();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
        }

        // ------------------------------------------------------------------ tabs

        static readonly (Mode Mode, string Label)[] AllTabs =
        {
            (Mode.Vouchers, "VOUCHERS"), (Mode.Redeem, "REDEEM"), (Mode.Targeted, "TARGETED"),
            (Mode.Combine, "COMBINE"), (Mode.Reroll, "RE-ROLL"), (Mode.Fuse, "FUSE"),
        };

        /// <summary>
        /// Whether the player holds every piece of at least one fusion - equipped pieces count
        /// here, so the tab does not vanish the moment one of them is worn (the preview says to
        /// unequip it). FUSE is only shown when this is true: a tab for a set nobody has yet would
        /// be a menu about something the player cannot do.
        /// </summary>
        bool AnyFusionHeld()
            => GearForge.Fusions.Any(f => GearForge.FusionInputs(f, _ctx.MintedGear, null).All(r => r != null));

        List<(Mode Mode, string Label)> Tabs()
            => AllTabs.Where(t => t.Mode != Mode.Fuse || AnyFusionHeld() || _fused != null).ToList();

        RectTransform _tabs;

        /// <summary>Built into their own container so they can be rebuilt when FUSE appears or
        /// goes (a fusion burns the set it was shown for).</summary>
        void BuildTabs(RectTransform full)
        {
            if (_tabs != null)
            {
                _actions.RemoveAll(a => a.Rect == null || a.Rect.IsChildOf(_tabs));
                Destroy(_tabs.gameObject);
            }
            _tabs = UiKit.Rect(full, "tabs", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            full = _tabs;

            var tabs = Tabs();
            const float tabW = 240f, tabH = 56f, gap = 12f;
            float gridW = tabs.Count * tabW + (tabs.Count - 1) * gap;

            for (int i = 0; i < tabs.Count; i++)
            {
                var (mode, label) = tabs[i];
                float x = -gridW * 0.5f + i * (tabW + gap);

                bool active = _mode == mode;
                var card = UiKit.Panel(full, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(x, -168 - tabH), new Vector2(x + tabW, -168),
                    active ? new Color(0.28f, 0.2f, 0.12f) : new Color(0.13f, 0.11f, 0.1f));

                UiKit.Label(UiKit.Rect(card, "n", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                    label, 20, active ? new Color(0.97f, 0.88f, 0.7f) : new Color(0.55f, 0.55f, 0.58f),
                    TextAnchor.MiddleCenter);

                var chosen = mode;
                _actions.Add((card, () => { _mode = chosen; ResetSteps(); BuildTabs(_full); Rebuild(); }));
            }
        }

        RectTransform _full;

        // ------------------------------------------------------------------ content

        void Rebuild()
        {
            foreach (Transform child in _content) Destroy(child.gameObject);
            // Tabs live outside _content and are built once at Open - only drop content-owned
            // actions (and any already-destroyed rect) here, so re-tapping a tab doesn't need
            // the whole screen torn down.
            _actions.RemoveAll(a => a.Rect == null || a.Rect.IsChildOf(_content));

            switch (_mode)
            {
                case Mode.Vouchers: BuildVouchers(); break;
                case Mode.Redeem: BuildRedeem(); break;
                case Mode.Targeted: BuildTargeted(); break;
                case Mode.Combine: BuildCombine(); break;
                case Mode.Reroll: BuildReroll(); break;
                case Mode.Fuse: BuildFuse(); break;
            }
        }

        void Subtitle(string text)
        {
            UiKit.Label(UiKit.Rect(_content, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -34), new Vector2(0, 0)),
                text, 18, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter);
        }

        /// <summary>A grid of tap cards, sized/positioned uniformly by every tab.</summary>
        RectTransform Card(int index, int cols, float cellW, float cellH, float gap, float top)
        {
            float gridW = cols * cellW + (cols - 1) * gap;
            int col = index % cols, row = index / cols;
            float x = -gridW * 0.5f + col * (cellW + gap);
            float y = top - row * (cellH + gap);
            return UiKit.Panel(_content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x, y - cellH), new Vector2(x + cellW, y),
                new Color(0.15f, 0.11f, 0.08f));
        }

        static Color TierColor(LootTier tier) => tier switch
        {
            LootTier.Bronze => new Color(0.62f, 0.42f, 0.28f),
            LootTier.Silver => new Color(0.75f, 0.77f, 0.8f),
            LootTier.Gold => new Color(0.86f, 0.72f, 0.32f),
            LootTier.Diamond => new Color(0.65f, 0.85f, 0.95f),
            LootTier.BlackDiamond => new Color(0.55f, 0.35f, 0.85f),
            _ => Color.white,
        };

        // ------------------------------------------------------------------ Vouchers (original flow)

        void BuildVouchers()
        {
            Subtitle($"{_ctx.VoucherCount} voucher{(_ctx.VoucherCount == 1 ? "" : "s")} - " +
                     "pick a slot to redeem a free random roll");

            for (int i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                var card = Card(i, 4, 220f, 130f, 22f, 60f);
                UiKit.Label(UiKit.Rect(card, "n", Vector2.zero, Vector2.one,
                    new Vector2(6, 6), new Vector2(-6, -6)),
                    slot.ToString(), 22, new Color(0.96f, 0.87f, 0.74f), TextAnchor.MiddleCenter);

                var chosen = slot;
                _actions.Add((card, () => { Close(); _ctx.OnPickVoucher?.Invoke(chosen); }));
            }
        }

        // ------------------------------------------------------------------ Redeem (random slot, one box)

        void BuildRedeem()
        {
            Subtitle($"spend {Tuning.GearRoll.ForgeRandomRedeemBoxCost} box - random slot at that box's tier");
            BuildTierPicker(tier =>
            {
                Close();
                _ctx.OnRedeemRandom?.Invoke(tier);
            });
        }

        // ------------------------------------------------------------------ Targeted (chosen slot [+ class])

        void BuildTargeted()
        {
            if (_pickedTier == null)
            {
                Subtitle($"spend {Tuning.GearRoll.ForgeTargetedRedeemBoxCost} boxes - pick a tier, then the exact slot");
                BuildTierPicker(tier => { _pickedTier = tier; Rebuild(); });
                return;
            }

            if (_pickedSlot == null)
            {
                Subtitle($"{_pickedTier} box - pick the slot");
                // Diamond and Black Diamond redeem into DESIGNS, so only slots holding one of that
                // tier are offered - and the relic slot is, since designs include relics.
                var slots = DesignDrops.IsDesignTier(_pickedTier.Value)
                    ? DesignDrops.Slots(_pickedTier.Value).ToArray()
                    : Slots;
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    var card = Card(i, 4, 220f, 130f, 22f, 60f);
                    UiKit.Label(UiKit.Rect(card, "n", Vector2.zero, Vector2.one,
                        new Vector2(6, 6), new Vector2(-6, -6)),
                        slot.ToString(), 22, new Color(0.96f, 0.87f, 0.74f), TextAnchor.MiddleCenter);

                    var chosen = slot;
                    _actions.Add((card, () =>
                    {
                        if (chosen == GearSlot.Weapon) { _pickedSlot = chosen; Rebuild(); return; }
                        Close();
                        _ctx.OnRedeemTargeted?.Invoke(chosen, _pickedTier.Value, WeaponClass.Greatsword);
                    }));
                }
                return;
            }

            // Weapon slot only reaches here - pick the class.
            Subtitle($"{_pickedTier} Weapon - pick the class");
            var classes = DesignDrops.IsDesignTier(_pickedTier.Value)
                ? DesignDrops.Classes(_pickedTier.Value).ToArray()
                : Classes;
            for (int i = 0; i < classes.Length; i++)
            {
                var wc = classes[i];
                var card = Card(i, 3, 260f, 130f, 22f, 60f);
                UiKit.Label(UiKit.Rect(card, "n", Vector2.zero, Vector2.one,
                    new Vector2(6, 6), new Vector2(-6, -6)),
                    wc.ToString(), 22, new Color(0.96f, 0.87f, 0.74f), TextAnchor.MiddleCenter);

                var chosen = wc;
                var slot = _pickedSlot.Value;
                var tier = _pickedTier.Value;
                _actions.Add((card, () => { Close(); _ctx.OnRedeemTargeted?.Invoke(slot, tier, chosen); }));
            }
        }

        /// <summary>Owned box tiers (Bronze-BlackDiamond) plus Rift Boxes shown as a Silver
        /// equivalent - the one currency the Forge also accepts, see GameBootstrap.TrySpendBoxes.</summary>
        void BuildTierPicker(Action<LootTier> onPick)
        {
            var owned = BoxTiers.Where(t => _ctx.Boxes.Get(t) > 0).ToList();
            int col = 0;

            foreach (var tier in owned)
            {
                var card = Card(col, 5, 200f, 120f, 18f, 60f);
                UiKit.Label(UiKit.Rect(card, "n", Vector2.zero, Vector2.one,
                    new Vector2(6, 6), new Vector2(-6, -6)),
                    $"{tier}\n x{_ctx.Boxes.Get(tier)}", 20, TierColor(tier), TextAnchor.MiddleCenter);
                var chosen = tier;
                _actions.Add((card, () => onPick(chosen)));
                col++;
            }

            int rift = _ctx.RiftBoxes?.Invoke() ?? 0;
            if (rift > 0)
            {
                var card = Card(col, 5, 200f, 120f, 18f, 60f);
                UiKit.Label(UiKit.Rect(card, "n", Vector2.zero, Vector2.one,
                    new Vector2(6, 6), new Vector2(-6, -6)),
                    $"Rift Box\n(as Silver)\n x{rift}", 18, new Color(0.55f, 0.8f, 0.95f),
                    TextAnchor.MiddleCenter);
                _actions.Add((card, () => onPick(LootTier.Silver)));
                col++;
            }

            if (col == 0)
                Subtitle("no boxes to spend - clear floors to find them");
        }

        // ------------------------------------------------------------------ shared pieces

        static readonly Color Ink = new(0.96f, 0.87f, 0.74f);
        static readonly Color Dim = new(0.5f, 0.53f, 0.6f);
        static readonly Color CarriedInk = new(0.55f, 0.9f, 0.6f);
        static readonly Color PoweredInk = new(0.97f, 0.8f, 0.4f);
        static readonly Color RandomInk = new(0.72f, 0.6f, 0.95f);
        static readonly Color StarOn = new(1f, 0.82f, 0.3f);
        static readonly Color StarOff = new(1f, 1f, 1f, 0.12f);
        static readonly Color ButtonBg = new(0.28f, 0.2f, 0.12f);
        static readonly Color QuietBg = new(0.13f, 0.11f, 0.1f);

        const int PageSize = 8;   // 4 x 2 item cards
        const float ItemW = 410f, ItemH = 220f, ItemGap = 20f, GridTop = -50f;
        static float NavY => GridTop - 2 * (ItemH + ItemGap) - 10f;

        static string Pct(float v) => $"+{v:0.#}%";
        static float Scaled(float unscaled, int level) => unscaled * GearRoller.UpgradeScale(level);

        static string ItemName(MintedGearRecord r)
        {
            string name = r.Slot == GearSlot.Weapon ? $"{r.Tier} {r.Class}" : $"{r.Tier} {r.Slot}";
            return r.Slot == GearSlot.Torso ? $"{name} ({r.DefensiveAbility})" : name;
        }

        static string PrimaryText(MintedGearRecord r, LootTier tier, int level)
            => $"{GearForge.Label(r.PrimaryStat)} {Pct(Scaled(GearRoller.PrimaryPoints(r.PrimaryStat, tier, r.Slot), level))}";

        static string ItemText(MintedGearRecord r)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(ItemName(r)).Append('\n').Append(PrimaryText(r, r.Tier, r.UpgradeLevel));
            if (r.SubStats != null)
                foreach (var sub in r.SubStats)
                    if (sub != null)
                        sb.Append("\n  ").Append(GearForge.Label(sub.Kind)).Append(' ')
                          .Append(Pct(Scaled(sub.Value, r.UpgradeLevel)));
            return sb.ToString();
        }

        /// <summary>One left-aligned line in a centred column, its top edge at y.</summary>
        void Line(float y, string text, int size, Color color, float width = 1000f)
        {
            UiKit.Label(UiKit.Rect(_content, "l", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-width * 0.5f, y - size - 10), new Vector2(width * 0.5f, y)),
                text, size, color, TextAnchor.MiddleLeft);
        }

        void StarRow(float x, float y, int level, float size = 30f)
        {
            var row = UiKit.Rect(_content, "stars", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x - 100f, y - size), new Vector2(x + 100f, y));
            UiKit.Stars(row, level, GearRoller.MaxLevel, size, StarOn, StarOff);
        }

        RectTransform Button(float x, float y, float w, float h, string text, Color bg, Action onTap)
        {
            var card = UiKit.Panel(_content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x, y - h), new Vector2(x + w, y), bg);
            UiKit.Label(UiKit.Rect(card, "n", Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6)),
                text, 20, Ink, TextAnchor.MiddleCenter);
            _actions.Add((card, onTap));
            return card;
        }

        RectTransform ItemCard(int index, string text, LootTier tier, int level, bool highlight)
        {
            var card = Card(index, 4, ItemW, ItemH, ItemGap, GridTop);
            if (highlight) card.GetComponent<Image>().color = new Color(0.36f, 0.25f, 0.1f);
            UiKit.Label(UiKit.Rect(card, "n", Vector2.zero, Vector2.one, new Vector2(14, 42), new Vector2(-14, -10)),
                text, 18, TierColor(tier), TextAnchor.UpperLeft);
            var row = UiKit.Rect(card, "stars", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 8), new Vector2(0, 36));
            UiKit.Stars(row, level, GearRoller.MaxLevel, 26f, StarOn, StarOff);
            return card;
        }

        List<T> Page<T>(List<T> all)
        {
            int pages = Mathf.Max(1, (all.Count + PageSize - 1) / PageSize);
            _page = Mathf.Clamp(_page, 0, pages - 1);
            return all.Skip(_page * PageSize).Take(PageSize).ToList();
        }

        /// <summary>BACK (if given) plus PREV/NEXT when the list is longer than one page.</summary>
        void NavRow(int total, Action back)
        {
            if (back != null) Button(-840f, NavY, 260f, 128f, "BACK", QuietBg, back);
            if (total <= PageSize) return;

            int pages = (total + PageSize - 1) / PageSize;
            Line(NavY - 44f, $"page {_page + 1} / {pages}", 20, Dim, 200f);
            if (_page > 0) Button(-360f, NavY, 240f, 128f, "PREV", QuietBg, () => { _page--; Rebuild(); });
            if (_page < pages - 1) Button(120f, NavY, 240f, 128f, "NEXT", QuietBg, () => { _page++; Rebuild(); });
        }

        // ------------------------------------------------------------------ Combine

        void BuildCombine()
        {
            if (_result != null) { BuildCombineResult(); return; }
            if (_plan != null) { BuildCombinePreview(); return; }

            var groups = GearForge.Groups(_ctx.MintedGear, _ctx.IsEquipped);
            var group = _groupKey == null ? null : groups.Find(g => GearForge.MatchKey(g[0]) == _groupKey);
            if (group == null)
            {
                _groupKey = null;
                _pickA = null;
                BuildCombineGroups(groups);
                return;
            }

            Subtitle(_pickA == null ? "pick the first piece" : "pick the second piece - tap the first again to un-pick it");
            var shown = Page(group);
            for (int i = 0; i < shown.Count; i++)
            {
                var r = shown[i];
                var card = ItemCard(i, ItemText(r), r.Tier, r.UpgradeLevel, r == _pickA);
                _actions.Add((card, () =>
                {
                    if (_pickA == null) _pickA = r;
                    else if (_pickA == r) _pickA = null;
                    else _plan = GearForge.Plan(_pickA, r);
                    Rebuild();
                }));
            }
            NavRow(group.Count, () => { _groupKey = null; _pickA = null; _page = 0; Rebuild(); });
        }

        void BuildCombineGroups(List<List<MintedGearRecord>> groups)
        {
            Subtitle("two matching pieces become one - same slot, tier, stars and primary stat" +
                     " (and class / ability). No boxes needed.");

            if (groups.Count == 0)
            {
                Line(-80f, "no two matching pieces yet (equipped pieces don't count - unequip first)", 20, Dim, 1100f);
                return;
            }

            var shown = Page(groups);
            for (int i = 0; i < shown.Count; i++)
            {
                var g = shown[i];
                var r = g[0];
                string next = r.UpgradeLevel >= GearRoller.MaxLevel ? "combine into the next tier" : "combine for a star";
                var card = ItemCard(i,
                    $"{ItemName(r)}\n{PrimaryText(r, r.Tier, r.UpgradeLevel)}\nx{g.Count} owned\n{next}",
                    r.Tier, r.UpgradeLevel, false);

                var key = GearForge.MatchKey(r);
                _actions.Add((card, () =>
                {
                    // A pair is the only possible choice - go straight to the preview.
                    if (g.Count == 2) _plan = GearForge.Plan(g[0], g[1]);
                    else { _groupKey = key; _pickA = null; _page = 0; }
                    Rebuild();
                }));
            }
            NavRow(groups.Count, null);
        }

        /// <summary>
        /// What the combine WILL do, before it does it. Carried-over and powered-up rows show
        /// real numbers (they are certain); randomized rows show "??? (?-?)" and nothing else,
        /// because they have not been rolled yet and each stat has its own range.
        /// </summary>
        void BuildCombinePreview()
        {
            var p = _plan;
            Subtitle("check the result before you confirm - randomized sub-stats roll on CONFIRM");

            float y = -40f;
            Line(y, $"{ItemName(p.A)}  x2", 28, TierColor(p.FromTier), 700f);
            StarRow(360f, y - 2f, p.FromLevel);
            y -= 50f;
            Line(y, $"becomes  {p.ToTier} {(p.A.Slot == GearSlot.Weapon ? p.A.Class.ToString() : p.A.Slot.ToString())}",
                 28, TierColor(p.ToTier), 700f);
            StarRow(360f, y - 2f, p.ToLevel);
            y -= 64f;

            Line(y, "POWERED UP", 22, PoweredInk); y -= 34f;
            float before = Scaled(GearRoller.PrimaryPoints(p.A.PrimaryStat, p.FromTier, p.A.Slot), p.FromLevel);
            float after = Scaled(GearRoller.PrimaryPoints(p.A.PrimaryStat, p.ToTier, p.A.Slot), p.ToLevel);
            Line(y, $"    {GearForge.Label(p.A.PrimaryStat)}  {Pct(before)}  ->  {Pct(after)}", 20, Ink); y -= 30f;
            if (p.Promotes)
                Line(y, $"    Tier {p.FromTier} -> {p.ToTier}, sub-stat slots " +
                        $"{GearRoller.SubStatCount(p.FromTier)} -> {GearRoller.SubStatCount(p.ToTier)}", 20, Ink);
            else
                Line(y, $"    every stat x{GearRoller.UpgradeScale(p.ToLevel) / GearRoller.UpgradeScale(p.FromLevel):0.00}", 20, Ink);
            y -= 44f;

            Line(y, "CARRIED OVER", 22, CarriedInk); y -= 34f;
            if (p.Carried.Count == 0) { Line(y, "    nothing matched", 20, Dim); y -= 30f; }
            foreach (var c in p.Carried)
            {
                if (p.Promotes)
                {
                    // The stat is certain; its value re-rolls in the new tier's range, which is known.
                    var (min, max) = GearRoller.SubStatRange(c.Kind, p.ToTier);
                    Line(y, $"    {GearForge.Label(c.Kind)}  ({Scaled(min, p.ToLevel):0.#}-{Scaled(max, p.ToLevel):0.#}%)" +
                            "   new roll in the new tier's range", 20, Ink);
                }
                else
                {
                    Line(y, $"    {GearForge.Label(c.Kind)}  {Pct(Scaled(c.Value, p.FromLevel))}  ->  " +
                            $"{Pct(Scaled(c.Value, p.ToLevel))}   higher roll kept", 20, Ink);
                }
                y -= 30f;
            }
            y -= 14f;

            Line(y, "RANDOMIZED", 22, RandomInk); y -= 34f;
            if (p.Randomized == 0) { Line(y, "    nothing - every sub-stat matched", 20, Dim); y -= 30f; }
            for (int i = 0; i < p.Randomized; i++) { Line(y, "    ??? (?-?)", 20, Ink); y -= 30f; }
            y -= 20f;

            Button(-280f, y, 260f, 128f, "CONFIRM", ButtonBg, () =>
            {
                var result = _ctx.OnCombine?.Invoke(p.A.InstanceId, p.B.InstanceId);
                _plan = null;
                _pickA = null;
                if (result != null) { _result = result; _resultNewFrom = p.Carried.Count; }
                Rebuild();
            });
            Button(20f, y, 260f, 128f, "BACK", QuietBg, () => { _plan = null; _pickA = null; Rebuild(); });
        }

        void BuildCombineResult()
        {
            var r = _result;
            Subtitle("combined");

            float y = -40f;
            Line(y, ItemName(r), 30, TierColor(r.Tier), 700f);
            StarRow(360f, y - 2f, r.UpgradeLevel);
            y -= 60f;
            Line(y, PrimaryText(r, r.Tier, r.UpgradeLevel), 22, Ink); y -= 40f;

            for (int i = 0; i < r.SubStats.Count; i++)
            {
                var sub = r.SubStats[i];
                bool fresh = i >= _resultNewFrom;
                Line(y, $"    {GearForge.Label(sub.Kind)}  {Pct(Scaled(sub.Value, r.UpgradeLevel))}" +
                        $"   {(fresh ? "NEW" : "CARRIED")}", 20, fresh ? RandomInk : CarriedInk);
                y -= 32f;
            }
            y -= 24f;

            Button(-130f, y, 260f, 128f, "DONE", ButtonBg, () => { ResetSteps(); Rebuild(); });
        }

        // ------------------------------------------------------------------ Re-roll

        void BuildReroll()
        {
            if (_rerollItem != null && (_ctx.MintedGear == null || !_ctx.MintedGear.Contains(_rerollItem)))
                _rerollItem = null;
            if (_rerollItem != null) { BuildRerollItem(); return; }

            Subtitle($"pick a piece - {Tuning.GearRoll.ForgeRerollValueBoxCost} box of its tier re-rolls one value, " +
                     $"{Tuning.GearRoll.ForgeRerollStatBoxCost} change the stat");

            var eligible = (_ctx.MintedGear ?? new List<MintedGearRecord>())
                .Where(r => GearForge.CanReroll(r, 0)).ToList();
            if (eligible.Count == 0)
            {
                Line(-80f, "nothing to re-roll yet - Bronze, Silver and Gold pieces carry sub-stats", 20, Dim, 1100f);
                return;
            }

            var shown = Page(eligible);
            for (int i = 0; i < shown.Count; i++)
            {
                var r = shown[i];
                var card = ItemCard(i, ItemText(r), r.Tier, r.UpgradeLevel, false);
                _actions.Add((card, () => { _rerollItem = r; _rerollNote = null; Rebuild(); }));
            }
            NavRow(eligible.Count, null);
        }

        void BuildRerollItem()
        {
            var r = _rerollItem;
            int rift = r.Tier == LootTier.Silver ? (_ctx.RiftBoxes?.Invoke() ?? 0) : 0;
            int owned = _ctx.Boxes.Get(r.Tier) + rift;
            Subtitle($"{r.Tier} boxes: {owned}{(rift > 0 ? " (incl. Rift Boxes)" : "")} - a re-roll can come out worse");

            float y = -40f;
            Line(y, ItemName(r), 30, TierColor(r.Tier), 700f);
            StarRow(360f, y - 2f, r.UpgradeLevel);
            y -= 56f;
            Line(y, $"{PrimaryText(r, r.Tier, r.UpgradeLevel)}   primary - fixed", 20, Dim); y -= 44f;

            int valueCost = Tuning.GearRoll.ForgeRerollValueBoxCost, statCost = Tuning.GearRoll.ForgeRerollStatBoxCost;
            for (int i = 0; i < r.SubStats.Count; i++)
            {
                var sub = r.SubStats[i];
                var (min, max) = GearRoller.SubStatRange(sub.Kind, r.Tier);
                UiKit.Label(UiKit.Rect(_content, "sub", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-840f, y - 128f), new Vector2(-120f, y)),
                    $"{GearForge.Label(sub.Kind)}  {Pct(Scaled(sub.Value, r.UpgradeLevel))}\n" +
                    $"range {Scaled(min, r.UpgradeLevel):0.#}-{Scaled(max, r.UpgradeLevel):0.#}%",
                    22, Ink, TextAnchor.MiddleLeft);

                int index = i;
                Button(-100f, y, 300f, 128f, $"NEW VALUE\n{valueCost} box{(valueCost == 1 ? "" : "es")}",
                       owned >= valueCost ? ButtonBg : QuietBg, () => Reroll(index, false));
                Button(220f, y, 300f, 128f, $"NEW STAT\n{statCost} box{(statCost == 1 ? "" : "es")}",
                       owned >= statCost ? ButtonBg : QuietBg, () => Reroll(index, true));
                y -= 140f;
            }

            if (!string.IsNullOrEmpty(_rerollNote)) Line(y, _rerollNote, 22, RandomInk);
            Button(-840f, NavY - 60f, 260f, 128f, "BACK", QuietBg, () => { _rerollItem = null; _rerollNote = null; Rebuild(); });
        }

        void Reroll(int index, bool changeStat)
        {
            var r = _rerollItem;
            var old = r.SubStats[index];
            string before = $"{GearForge.Label(old.Kind)} {Pct(Scaled(old.Value, r.UpgradeLevel))}";

            var result = _ctx.OnReroll?.Invoke(r.InstanceId, index, changeStat);
            if (result == null)
            {
                _rerollNote = $"not enough {r.Tier} boxes";
            }
            else
            {
                var now = result.SubStats[index];
                _rerollNote = $"{before}  ->  {GearForge.Label(now.Kind)} {Pct(Scaled(now.Value, result.UpgradeLevel))}";
            }
            Rebuild();
        }

        // ------------------------------------------------------------------ Fuse

        /// <summary>
        /// Fusions: specific designs burned into a Black Diamond weapon and its relic. Walks
        /// fusion -> what it burns (each piece marked held or missing) -> CONFIRM -> the result.
        /// Nothing here is random, so unlike combining there is no plan/execute split to protect.
        /// </summary>
        void BuildFuse()
        {
            if (_fused != null)
            {
                Subtitle("fused");
                Line(-40f, _fused.DisplayName, 34, TierColor(_fused.Tier), 700f);
                Line(-100f, "and its seal - the relic carrying the weapon art - are yours.", 22, Ink, 900f);
                Line(-140f, "The three swords are gone.", 22, Dim, 900f);
                Button(-130f, -220f, 260f, 128f, "DONE", ButtonBg, () =>
                {
                    ResetSteps();
                    // The set just burned - if it was the only one, FUSE goes with it.
                    if (!AnyFusionHeld()) _mode = Mode.Vouchers;
                    BuildTabs(_full);
                    Rebuild();
                });
                return;
            }
            if (_fusion != null) { BuildFusePreview(); return; }

            Subtitle("burn a set of Diamond weapons into the Black Diamond weapon they make - " +
                     "no box drops these");
            // Only sets the player holds in full - the same rule that shows the tab at all.
            var ready = GearForge.Fusions
                .Where(f => GearForge.FusionInputs(f, _ctx.MintedGear, null).All(r => r != null)).ToList();
            for (int i = 0; i < ready.Count; i++)
            {
                var f = ready[i];
                var held = GearForge.FusionInputs(f, _ctx.MintedGear, _ctx.IsEquipped);
                int have = held.Count(r => r != null);
                // A plain card, not ItemCard: that one always draws a star row, and nothing a
                // fusion makes or burns has stars.
                var card = Card(i, 4, ItemW, ItemH, ItemGap, GridTop);
                if (have == f.Inputs.Length) card.GetComponent<Image>().color = new Color(0.36f, 0.25f, 0.1f);
                UiKit.Label(UiKit.Rect(card, "n", Vector2.zero, Vector2.one, new Vector2(14, 10), new Vector2(-14, -10)),
                    $"{f.Name}\n{have} / {f.Inputs.Length} pieces held" +
                    (have == f.Inputs.Length ? "\nready to fuse" : ""),
                    22, TierColor(LootTier.BlackDiamond), TextAnchor.MiddleCenter);
                var chosen = f;
                _actions.Add((card, () => { _fusion = chosen; Rebuild(); }));
            }
        }

        void BuildFusePreview()
        {
            var f = _fusion;
            var held = GearForge.FusionInputs(f, _ctx.MintedGear, _ctx.IsEquipped);
            bool ready = held.All(r => r != null);
            Subtitle(ready ? "these are burned - check before you confirm"
                           : "missing pieces - equipped pieces don't count, unequip first");

            float y = -40f;
            Line(y, "BURNS", 22, Dim); y -= 36f;
            for (int i = 0; i < f.Inputs.Length; i++)
            {
                var design = GearCatalog.Get(f.Inputs[i]);
                string name = design != null ? design.DisplayName : f.Inputs[i];
                Line(y, $"    {name}   {(held[i] != null ? "held" : "MISSING")}", 24,
                     held[i] != null ? TierColor(LootTier.Diamond) : RandomInk, 900f);
                y -= 38f;
            }
            y -= 20f;

            Line(y, "MAKES", 22, Dim); y -= 36f;
            var weapon = GearCatalog.Get(f.Weapon);
            var relic = GearCatalog.Get(f.Relic);
            Line(y, $"    {(weapon != null ? weapon.DisplayName : f.Weapon)}   Black Diamond weapon",
                 24, TierColor(LootTier.BlackDiamond), 900f); y -= 38f;
            if (!string.IsNullOrEmpty(f.Relic))
            {
                Line(y, $"    {(relic != null ? relic.DisplayName : f.Relic)}   relic - " +
                        $"{relic?.Signature?.DisplayName ?? "its weapon art"}", 24, TierColor(LootTier.BlackDiamond), 900f);
                y -= 38f;
            }
            y -= 22f;

            if (ready)
                Button(-280f, y, 260f, 128f, "FUSE", ButtonBg, () =>
                {
                    var made = _ctx.OnFuse?.Invoke(f.Id);
                    if (made != null) _fused = made;
                    Rebuild();
                });
            Button(ready ? 20f : -130f, y, 260f, 128f, "BACK", QuietBg, () => { _fusion = null; Rebuild(); });
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!IsOpen) return;

            _focusRects.Clear();
            foreach (var (rect, _) in _actions)
                if (rect != null) _focusRects.Add(rect);
            Core.Controls.SetFocusCandidates(_focusRects);

            if (Core.Controls.CancelTapped)
            {
                Close();
                return;
            }

            if (!Core.Controls.Tapped(out var at)) return;

            foreach (var (rect, onTap) in _actions)
            {
                if (rect == null || !RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) continue;
                onTap?.Invoke();
                return;
            }
        }
    }
}
