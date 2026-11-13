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
    /// The Forge, in full: the STARTING KIT a new character chooses, OPENING boxes (a random slot,
    /// or the chosen one for more), COMBINING two matching pieces (the next star, or from three stars
    /// the next tier), RE-ROLLING one sub-stat for boxes, SALVAGING unwanted pieces into calx, FUSING
    /// a complete Diamond set, DYEING Diamond armour and forging a better PHIAL.
    ///
    /// Laid out for a phone in landscape (the user's approved mock, 2026-11-12 - the old fixed-pixel
    /// grid put 18-unit text, about 1.3 mm, on an iPhone): what is held along the top, the tabs down a
    /// left rail, a scrolling LIST on the left and the chosen thing's DETAIL on the right, its main
    /// button always at the detail's bottom right. Sizes are canvas units (1920x1080 at match 0.5; a
    /// phone in landscape is about 2120x980) and stretch with the screen rather than sit at fixed
    /// offsets.
    ///
    /// Combining never commits from a single tap: the detail states what carries over, what powers
    /// up, and how many slots will be randomized (those show only as "???" - nothing random is rolled
    /// until COMBINE, or reopening the menu would be a free re-roll), then shows what actually rolled.
    /// </summary>
    public partial class ForgeScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        enum Mode { Kit, Open, Combine, Reroll, Salvage, Fuse, Dye, Phial }

        /// <summary>Every slot the Forge can roll into. Relic is excluded - GearRoller has no
        /// pool for it (cosmetic only, same rule a weapon transmog already follows), so redeeming
        /// into it would hand back a blank item. Trinket is excluded too: it merged into Relic and
        /// no item declares that slot any more, so redeeming into it would be wasted boxes.</summary>
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

        /// <summary>Everything the screen needs to know about the player's current Forge
        /// standing, and every action it can trigger. Bundled rather than a long parameter list,
        /// since every tab needs a different slice of it.</summary>
        public struct Context
        {
            public LootBoxes Boxes;
            /// <summary>The banked Rift Boxes, read live - a promotion spends one while the screen
            /// stays open.</summary>
            public Func<int> RiftBoxes;
            public List<MintedGearRecord> MintedGear;
            public Func<string, bool> IsEquipped;
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
            /// <summary>Salvages pieces (by instance id); returns what came of it, or null if
            /// nothing was salvaged.</summary>
            public Func<IReadOnlyList<string>, string> OnSalvage;
            /// <summary>The character's phial tier (Tuning.Phial: 0 Bronze, 1 Silver, 2 Gold), read live.</summary>
            public Func<int> PhialTier;
            /// <summary>Forges the phial of a tier from its boxes; true when it was forged.</summary>
            public Func<int, bool> OnCraftPhial;
            /// <summary>The calx short of a whole box, per tier - read live.</summary>
            public LootBoxes Calx;
            /// <summary>What the database holds for the character - dyes, charges, what each
            /// design is dyed (Chain.CharacterItems), read live.</summary>
            public Func<CharacterItems> Items;
            /// <summary>Applies a recolour session; null on success, else why not.</summary>
            public Func<List<PieceDye>, System.Threading.Tasks.Task<string>> OnRecolour;
            /// <summary>Breaks one Diamond box into charges; null on success, else why not.</summary>
            public Func<System.Threading.Tasks.Task<string>> OnBreakBox;
            /// <summary>How many Diamond boxes could be broken for charges right now.</summary>
            public Func<int> BreakableBoxes;
            /// <summary>Withdraws dyes to the wallet as tokens (a PFP holder only) - null when the
            /// player cannot, else returns null on success or why not.</summary>
            public Func<string, int, System.Threading.Tasks.Task<string>> OnWithdraw;
            /// <summary>A new character's kit waits to be chosen: the screen opens on STARTING KIT
            /// and every other tab waits for it. Read live.</summary>
            public Func<bool> KitWaiting;
            /// <summary>Wears the kit's weapon of a class and its chest of an ability.</summary>
            public Action<WeaponClass, DefensiveAbility> OnChooseKit;
        }

        Context _ctx;
        GameObject _root;
        RectTransform _frame, _body, _detail, _pane, _listContent, _listHead;
        Mode _mode = Mode.Open;
        int _openedFrame;

        // ------------------------------------------------------------------ layout (canvas units)

        const float HeaderH = 104f, RailW = 300f, ListW = 660f, Pad = 28f, RowH = 112f, RowGap = 10f;
        const float ButtonH = 92f, Gap = 16f;
        const int TitleSize = 36, HeadSize = 40, BodySize = 30, SmallSize = 27, ButtonSize = 30, RailSize = 28;

        static Color Hex(uint rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        static readonly Color Bg = Hex(0x08080b), Rule = Hex(0x221b15), ChipBg = Hex(0x17130f);
        static readonly Color RowBg = Hex(0x1c1510), OnBg = Hex(0x47331f), OnEdge = Hex(0xc9a46a), CardBg = Hex(0x14100c);
        static readonly Color Primary = Hex(0xc9a46a), PrimaryInk = Hex(0x1a120a), QuietBg = Hex(0x211c1a);
        static readonly Color Ink = Hex(0xf5debd), TitleInk = Hex(0xf2dbb8), OnInk = Hex(0xf7e0b3), SubInk = Hex(0xc9b597);
        static readonly Color Dim = Hex(0xa3a8b2), Off = Hex(0x7d7770);
        static readonly Color Good = Hex(0x8ce699), Bad = Hex(0xf2a08f), Rolled = Hex(0xc7adf5), Powered = Hex(0xf7cc66);
        static readonly Color StarOn = Hex(0xffd14d), StarOff = Hex(0x3a332c);

        internal static Color TierColor(LootTier tier) => tier switch
        {
            LootTier.Bronze => Hex(0xc08a63),
            LootTier.Silver => Hex(0xbfc4cc),
            LootTier.Gold => Hex(0xdbb852),
            LootTier.Diamond => Hex(0xa6d9f2),
            LootTier.BlackDiamond => Hex(0xb48ae8),
            _ => Color.white,
        };

        /// <summary>Bronze, Silver, Gold, Diamond, Black Diamond - LootTier's own order starts at Silver.</summary>
        static int Rank(LootTier tier) => Array.IndexOf(BoxTiers, tier);

        static string Html(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
        static string Tint(string text, Color c) => $"<color={Html(c)}>{text}</color>";

        internal static string Label(LootTier tier) => tier == LootTier.BlackDiamond ? "Black Diamond" : tier.ToString();
        static string Plural(int n, string one, string many = null) => n == 1 ? $"{n} {one}" : $"{n} {many ?? one + "es"}";

        // ------------------------------------------------------------------ open / close

        public void Open(Transform canvas, Context ctx)
        {
            if (IsOpen) return;
            IsOpen = true;
            _ctx = ctx;
            _mode = KitWaiting ? Mode.Kit : Mode.Open;
            ResetSteps();
            _scroll = 0f;
            GamePause.Hold(this);
            _openedFrame = Time.frameCount;
            _pressed = false;

            _root = new GameObject("ForgeScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;
            // Solid: at 0.98 the hub still read through it.
            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Bg);

            Rebuild();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _frame = null;
        }

        bool KitWaiting => _ctx.KitWaiting?.Invoke() ?? false;

        void Show(Mode mode)
        {
            if (_mode == mode) return;
            _mode = mode;
            ResetSteps();
            _scroll = 0f;
            Rebuild();
        }

        // ------------------------------------------------------------------ steps within the tabs

        // Open a box.
        bool _targeted;
        LootTier? _boxTier;
        GearSlot? _boxSlot;
        WeaponClass _boxClass;

        void ResetSteps()
        {
            _targeted = false; _boxTier = null; _boxSlot = null; _boxClass = WeaponClass.Greatsword;
            ResetGear();
            ResetDye();
            ResetKit();
        }

        // ------------------------------------------------------------------ the frame

        /// <summary>The whole frame is rebuilt on any change - a menu, not a HUD, and a box count or
        /// a calx tally in the top bar can move with any action. The list keeps its scroll, and the
        /// D-pad its place, across a rebuild.</summary>
        void Rebuild()
        {
            if (_root == null) return;
            int focusedAt = FocusedIndex();
            if (_frame != null) Destroy(_frame.gameObject);
            _actions.Clear();
            _detail = _pane = _listContent = _listHead = null;
            _listH = 0f;
            _dy = 0f;

            _frame = UiKit.Rect(_root.transform, "frame", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            if (_mode == Mode.Kit && !KitWaiting) _mode = Mode.Open;
            if (_mode == Mode.Fuse && !AnyFusionHeld() && _fused == null) _mode = Mode.Open;
            BuildHeader();
            BuildRail();
            _body = UiKit.Rect(_frame, "body", Vector2.zero, Vector2.one, new Vector2(RailW + 2f, 0f), new Vector2(0f, -HeaderH - 2f));

            switch (_mode)
            {
                case Mode.Kit: BuildKit(); break;
                case Mode.Open: BuildOpen(); break;
                case Mode.Combine: BuildCombine(); break;
                case Mode.Reroll: BuildReroll(); break;
                case Mode.Salvage: BuildSalvage(); break;
                case Mode.Fuse: BuildFuse(); break;
                case Mode.Dye: BuildDye(); break;
                case Mode.Phial: BuildPhial(); break;
            }

            if (_listContent != null)
            {
                _listContent.sizeDelta = new Vector2(0f, _listH);
                ApplyScroll();
            }
            RestoreFocus(focusedAt);
        }

        void BuildHeader()
        {
            var bar = UiKit.Rect(_frame, "header", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -HeaderH), Vector2.zero);
            UiKit.Panel(_frame, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -HeaderH - 2f), new Vector2(0f, -HeaderH), Rule);
            Txt(UiKit.Rect(bar, "t", Vector2.zero, new Vector2(0f, 1f), new Vector2(40f, 0f), new Vector2(520f, 0f)),
                "THE FORGE", TitleSize, TitleInk, TextAnchor.MiddleLeft);

            const float h = 64f;
            float x = -Pad;
            var close = UiKit.Panel(bar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(x - 72f, -36f), new Vector2(x, 36f), ChipBg);
            Txt(close, "X", BodySize, Ink, TextAnchor.MiddleCenter);
            Act(close, Close);
            x -= 72f + Gap;

            void Chip(string text, int plainLength, Color dot, bool diamond = false)
            {
                float w = 70f + plainLength * 15f;
                var chip = UiKit.Panel(bar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(x - w, -h * 0.5f), new Vector2(x, h * 0.5f), ChipBg);
                if (dot.a > 0f)
                {
                    var d = UiKit.Panel(chip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, -10f), new Vector2(40f, 10f), dot);
                    if (diamond) d.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }
                Txt(UiKit.Rect(chip, "n", Vector2.zero, Vector2.one, new Vector2(dot.a > 0f ? 52f : 20f, 0f), new Vector2(-14f, 0f)),
                    text, SmallSize, Ink, TextAnchor.MiddleLeft);
                x -= w + 10f;
            }

            int per = Tuning.GearRoll.CalxPerBox;
            if (_ctx.Calx != null)
            {
                string calx = $"Calx  {Tint($"{_ctx.Calx.Bronze}/{per}", TierColor(LootTier.Bronze))} " +
                              $"{Tint($"{_ctx.Calx.Silver}/{per}", TierColor(LootTier.Silver))} " +
                              $"{Tint($"{_ctx.Calx.Gold}/{per}", TierColor(LootTier.Gold))}";
                Chip(calx, 17, Color.clear);
            }
            Chip($"Rift {RiftHeld}", 7, Hex(0xe06b5a), diamond: true);
            for (int i = BoxTiers.Length - 1; i >= 0; i--)
            {
                var tier = BoxTiers[i];
                int n = _ctx.Boxes?.Get(tier) ?? 0;
                // Diamond and Black Diamond only when held: most characters never see either.
                if (n == 0 && DesignDrops.IsDesignTier(tier)) continue;
                string text = $"{Label(tier)} {n}";
                Chip(text, text.Length, TierColor(tier));
            }
        }

        static readonly (Mode Mode, string Label)[] AllTabs =
        {
            (Mode.Kit, "STARTING KIT"), (Mode.Open, "OPEN A BOX"), (Mode.Combine, "COMBINE"),
            (Mode.Reroll, "RE-ROLL"), (Mode.Salvage, "SALVAGE"), (Mode.Fuse, "FUSE"),
            (Mode.Dye, "DYE"), (Mode.Phial, "PHIAL"),
        };

        /// <summary>
        /// Whether the player holds every piece of at least one fusion - equipped pieces count
        /// here, so the tab does not vanish the moment one of them is worn (the preview says to
        /// unequip it). FUSE is only shown when this is true: a tab for a set nobody has yet would
        /// be a menu about something the player cannot do.
        /// </summary>
        bool AnyFusionHeld()
            => GearForge.Fusions.Any(f => GearForge.FusionInputs(f, _ctx.MintedGear, null).All(r => r != null));

        /// <summary>The tabs down the left. While the kit waits, STARTING KIT is the only one open.</summary>
        void BuildRail()
        {
            var rail = UiKit.Rect(_frame, "rail", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(RailW, -HeaderH - 2f));
            UiKit.Panel(_frame, Vector2.zero, new Vector2(0f, 1f), new Vector2(RailW, 0f), new Vector2(RailW + 2f, -HeaderH - 2f), Rule);

            bool kit = KitWaiting;
            bool fuse = AnyFusionHeld() || _fused != null;
            float y = -20f;
            foreach (var (mode, label) in AllTabs)
            {
                if (mode == Mode.Kit && !kit) continue;
                if (mode == Mode.Fuse && !fuse) continue;
                bool on = _mode == mode, open = !kit || mode == Mode.Kit;
                var tab = UiKit.Panel(rail, new Vector2(0f, 1f), Vector2.one, new Vector2(16f, y - 84f), new Vector2(-16f, y),
                    on ? OnBg : Color.clear);
                Txt(UiKit.Rect(tab, "n", Vector2.zero, Vector2.one, new Vector2(24f, 0f), new Vector2(-8f, 0f)),
                    label, RailSize, on ? OnInk : open ? Dim : Off, TextAnchor.MiddleLeft);
                var chosen = mode;
                if (open) Act(tab, () => Show(chosen));
                y -= 84f + 8f;
            }
        }

        // ------------------------------------------------------------------ building blocks

        static Text Txt(Transform parent, string text, int size, Color color,
                        TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
        {
            var t = UiKit.Label(parent, text, size, color, anchor);
            if (wrap) t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }

        /// <summary>A panel; with <paramref name="edge"/> it is ringed in gold (the chosen one).</summary>
        static RectTransform Framed(Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax,
                                    bool edge, Color fill)
        {
            if (!edge) return UiKit.Panel(parent, aMin, aMax, oMin, oMax, fill);
            var outer = UiKit.Panel(parent, aMin, aMax, oMin, oMax, OnEdge);
            UiKit.Panel(outer, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f), fill);
            return outer;
        }

        /// <summary>The whole body is the detail - a tab with no list (the kit, opening a box, the phial).</summary>
        void Whole()
        {
            _detail = UiKit.Rect(_body, "detail", Vector2.zero, Vector2.one, new Vector2(Pad * 1.4f, Pad), new Vector2(-Pad * 1.4f, -Pad));
        }

        /// <summary>A scrolling list <paramref name="listW"/> wide on the left (under an optional
        /// fixed head <paramref name="headH"/> tall) and the detail filling the rest.</summary>
        void Columns(float listW, float headH = 0f)
        {
            var col = UiKit.Rect(_body, "list", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(listW, 0f));
            UiKit.Panel(_body, Vector2.zero, new Vector2(0f, 1f), new Vector2(listW, 0f), new Vector2(listW + 2f, 0f), Rule);
            float top = 20f;
            if (headH > 0f)
            {
                _listHead = UiKit.Rect(col, "head", new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -top - headH), new Vector2(-20f, -top));
                top += headH + 12f;
            }
            _pane = UiKit.Rect(col, "pane", Vector2.zero, Vector2.one, new Vector2(20f, 20f), new Vector2(-20f, -top));
            // RectMask2D rather than Mask - a rectangle clip with no stencil buffer.
            _pane.gameObject.AddComponent<RectMask2D>();
            _listContent = UiKit.Rect(_pane, "rows", new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _detail = UiKit.Rect(_body, "detail", Vector2.zero, Vector2.one, new Vector2(listW + 2f + Pad, Pad), new Vector2(-Pad * 1.4f, -Pad));
        }

        /// <summary>One line of grey guidance across the list's head.</summary>
        void ListNote(string text)
        {
            if (_listHead == null) return;
            Txt(_listHead, text, SmallSize, Dim, TextAnchor.MiddleLeft, wrap: true);
        }

        float _listH;

        /// <summary>The next row of the list, <paramref name="h"/> tall.</summary>
        RectTransform ListItem(float h, bool on, float indent = 0f)
        {
            var r = Framed(_listContent, new Vector2(0f, 1f), Vector2.one, new Vector2(indent, -_listH - h), new Vector2(0f, -_listH),
                on, on ? OnBg : RowBg);
            _listH += h + RowGap;
            return r;
        }

        /// <summary>A list row for a piece: its tier's bar, name and a line under it, its stars and a
        /// tag on the right - and a tick box when <paramref name="ticked"/> is given.</summary>
        RectTransform PieceRow(MintedGearRecord r, bool on, string sub, string tag = null, Color? tagInk = null,
                               bool? ticked = null, float indent = 0f)
        {
            var row = ListItem(RowH, on, indent);
            float x = 18f;
            if (ticked != null)
            {
                var box = Framed(row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, -20f), new Vector2(x + 40f, 20f),
                    true, ticked.Value ? OnEdge : RowBg);
                if (ticked.Value) Txt(box, "X", 24, PrimaryInk, TextAnchor.MiddleCenter);
                x += 56f;
            }
            UiKit.Panel(row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, -36f), new Vector2(x + 12f, 36f), TierColor(r.Tier));
            x += 28f;
            bool stars = r.Tier is LootTier.Bronze or LootTier.Silver or LootTier.Gold;
            float right = stars || tag != null ? 170f : 16f;
            Txt(UiKit.Rect(row, "n", new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(x, 0f), new Vector2(-right, -8f)),
                ItemName(r), BodySize, Ink, TextAnchor.LowerLeft);
            Txt(UiKit.Rect(row, "s", Vector2.zero, new Vector2(1f, 0.5f), new Vector2(x, 8f), new Vector2(-right, 0f)),
                sub, SmallSize, SubInk, TextAnchor.UpperLeft);
            if (stars)
                UiKit.Stars(UiKit.Rect(row, "stars", new Vector2(1f, 0.5f), Vector2.one, new Vector2(-160f, 0f), new Vector2(-16f, -10f)),
                    r.UpgradeLevel, GearRoller.MaxLevel, 30f, StarOn, StarOff);
            if (tag != null)
                Txt(UiKit.Rect(row, "tag", new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-160f, 8f), new Vector2(-16f, 0f)),
                    tag, SmallSize, tagInk ?? Dim, TextAnchor.MiddleCenter);
            return row;
        }

        float _dy;

        /// <summary>The next block down the detail, <paramref name="h"/> tall.</summary>
        RectTransform Stack(float h, float gap = Gap)
        {
            var r = UiKit.Rect(_detail, "block", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -_dy - h), new Vector2(0f, -_dy));
            _dy += h + gap;
            return r;
        }

        /// <summary>The space left between the stack and the footer's buttons.</summary>
        RectTransform Rest(bool footer = true)
            => UiKit.Rect(_detail, "rest", Vector2.zero, Vector2.one, new Vector2(0f, footer ? ButtonH + 20f : 0f), new Vector2(0f, -_dy));

        Text Para(string text, int size, Color color, float h, float gap = Gap)
            => Txt(Stack(h, gap), text, size, color, TextAnchor.UpperLeft, wrap: true);

        static float ButtonW(string label)
        {
            int longest = label.Split('\n').Max(l => l.Length);
            return Mathf.Max(200f, longest * 21f + 80f);
        }

        /// <summary>A button filling <paramref name="r"/>: gold for the main action, dark for the
        /// rest, greyed when it cannot be pressed.</summary>
        RectTransform Button(RectTransform r, string label, bool primary, bool enabled, Action tap, int size = ButtonSize)
        {
            var img = r.gameObject.GetComponent<Image>() ?? r.gameObject.AddComponent<Image>();
            img.color = enabled && primary ? Primary : QuietBg;
            Txt(r, label, size, !enabled ? Off : primary ? PrimaryInk : Ink, TextAnchor.MiddleCenter);
            if (enabled && tap != null) Act(r, tap);
            return r;
        }

        /// <summary>The detail's foot: buttons from the right, in the order given, and a note to their left.</summary>
        void Footer(string note, Color noteInk, params (string Label, bool Primary, bool Enabled, Action Tap)[] buttons)
        {
            float x = 0f;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                var b = buttons[i];
                float w = ButtonW(b.Label);
                Button(UiKit.Rect(_detail, "button", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(x - w, 0f), new Vector2(x, ButtonH)),
                    b.Label, b.Primary, b.Enabled, b.Tap);
                x -= w + Gap;
            }
            if (!string.IsNullOrEmpty(note))
                Txt(UiKit.Rect(_detail, "note", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(x - 8f, ButtonH)),
                    note, SmallSize, noteInk, buttons.Length > 0 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, wrap: true);
        }

        /// <summary>A picture of the piece: its menu art, every layer side by side, in the colours
        /// <paramref name="swatches"/> dye it (the dyed copy, built once - Chain.Recolour.Registered).</summary>
        void PiecePicture(RectTransform parent, GearItem piece, string[] swatches, Vector2 min, Vector2 max)
        {
            var drawn = swatches != null ? Recolour.Registered(piece, swatches) ?? piece : piece;
            var layers = drawn.LayersFor(menu: true).Where(l => l?.Sprite != null && !RigLayers.IsBody(l.Layer)).ToList();
            if (layers.Count == 0) return;
            var box = UiKit.Rect(parent, "pic", min, max, new Vector2(8, 8), new Vector2(-8, -8));
            for (int i = 0; i < layers.Count; i++)
            {
                var slot = UiKit.Rect(box, "l" + i, new Vector2(i / (float)layers.Count, 0f), new Vector2((i + 1) / (float)layers.Count, 1f),
                    Vector2.zero, Vector2.zero);
                var img = slot.gameObject.AddComponent<Image>();
                img.sprite = layers[i].Sprite;
                img.color = layers[i].Tint;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
        }

        // ------------------------------------------------------------------ pieces in words

        static string Pct(float v) => $"+{v:0.#}%";
        static float Scaled(float unscaled, int level) => unscaled * GearRoller.UpgradeScale(level);

        internal static string ItemName(MintedGearRecord r)
        {
            string name = r.Slot == GearSlot.Weapon ? $"{Label(r.Tier)} {r.Class}" : $"{Label(r.Tier)} {r.Slot.Label()}";
            return r.Slot == GearSlot.Torso ? $"{name} ({r.DefensiveAbility.Label()})" : name;
        }

        static string PrimaryText(MintedGearRecord r, LootTier tier, int level)
            => $"{GearForge.Label(r.PrimaryStat)} {Pct(Scaled(GearRoller.PrimaryPoints(r.PrimaryStat, tier, r.Slot), level))}";

        static string SubText(MintedGearRecord r)
        {
            if (r.SubStats == null || r.SubStats.Count == 0) return "";
            return string.Join(" · ", r.SubStats.Where(s => s != null)
                .Select(s => $"{GearForge.Label(s.Kind)} {Pct(Scaled(s.Value, r.UpgradeLevel))}"));
        }

        /// <summary>A row of stars <paramref name="size"/> tall, starting at the left of <paramref name="parent"/>.</summary>
        static void StarsAt(RectTransform parent, float x, int level, float size = 30f)
        {
            float w = GearRoller.MaxLevel * size;
            UiKit.Stars(UiKit.Rect(parent, "stars", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(x, 0f), new Vector2(x + w, 0f)),
                level, GearRoller.MaxLevel, size, StarOn, StarOff);
        }

        /// <summary>Promoting (three stars into the next tier) takes a Rift Box from the banked
        /// reserve - GameBootstrap.CombineGear spends it and refuses without one.</summary>
        static int PromoteCost => Tuning.GearRoll.PromoteRiftBoxCost;
        int RiftHeld => _ctx.RiftBoxes?.Invoke() ?? 0;
        bool CanPromote => RiftHeld >= PromoteCost;

        // ------------------------------------------------------------------ Open a box

        static readonly (GearSlot Slot, WeaponClass Class, string Label)[] OpenChoices = BuildOpenChoices();

        /// <summary>The ten armour slots, then the weapon once per class - one tap, not a second step.</summary>
        static (GearSlot, WeaponClass, string)[] BuildOpenChoices()
        {
            var list = Slots.Where(s => s != GearSlot.Weapon).Select(s => (s, WeaponClass.Greatsword, s.Label())).ToList();
            list.Add((GearSlot.Weapon, WeaponClass.Greatsword, "Greatsword"));
            list.Add((GearSlot.Weapon, WeaponClass.Disc, "Discs"));
            list.Add((GearSlot.Weapon, WeaponClass.Bow, "Bow"));
            return list.ToArray();
        }

        /// <summary>
        /// Random and targeted redemption as one tab: how to open (a random slot for one box, or the
        /// slot chosen for more), which box, and - chosen - which slot. Diamond and Black Diamond
        /// open at random only. OPEN closes the screen for the reveal.
        /// </summary>
        void BuildOpen()
        {
            Whole();
            int randomCost = Tuning.GearRoll.ForgeRandomRedeemBoxCost, targetCost = Tuning.GearRoll.ForgeTargetedRedeemBoxCost;
            int cost = _targeted ? targetCost : randomCost;
            bool Opens(LootTier t) => (_ctx.Boxes?.Get(t) ?? 0) >= cost && (!_targeted || !DesignDrops.IsDesignTier(t));
            if (_boxTier == null || !Opens(_boxTier.Value)) _boxTier = BoxTiers.Cast<LootTier?>().FirstOrDefault(t => Opens(t.Value));

            // How to open.
            var how = Stack(88f, 24f);
            void Way(int i, bool targeted, string title, string sub)
            {
                bool on = _targeted == targeted;
                var b = UiKit.Panel(how, Vector2.zero, new Vector2(0f, 1f), new Vector2(i * 388f, 0f), new Vector2(i * 388f + 380f, 0f),
                    on ? OnBg : ChipBg);
                Txt(UiKit.Rect(b, "t", new Vector2(0f, 0.5f), Vector2.one, new Vector2(16f, 0f), new Vector2(-16f, -6f)),
                    title, 26, on ? OnInk : Dim, TextAnchor.LowerCenter);
                Txt(UiKit.Rect(b, "s", Vector2.zero, new Vector2(1f, 0.5f), new Vector2(16f, 6f), new Vector2(-16f, 0f)),
                    sub, SmallSize, on ? Ink : Dim, TextAnchor.UpperCenter);
                Act(b, () => { _targeted = targeted; _boxSlot = null; Rebuild(); });
            }
            Way(0, false, "RANDOM SLOT", Plural(randomCost, "box"));
            Way(1, true, "CHOOSE THE SLOT", Plural(targetCost, "box"));
            Txt(UiKit.Rect(how, "n", Vector2.zero, Vector2.one, new Vector2(800f, 0f), Vector2.zero),
                "Diamond and Black Diamond open at random only", SmallSize, Dim, TextAnchor.MiddleLeft, wrap: true);

            // Which box.
            var tiles = Stack(200f, 24f);
            for (int i = 0; i < BoxTiers.Length; i++)
            {
                var tier = BoxTiers[i];
                bool can = Opens(tier), on = can && _boxTier == tier;
                float a = i / (float)BoxTiers.Length, b = (i + 1) / (float)BoxTiers.Length;
                var tile = Framed(tiles, new Vector2(a, 0f), new Vector2(b, 1f), new Vector2(i == 0 ? 0f : 7f, 0f),
                    new Vector2(i == BoxTiers.Length - 1 ? 0f : -7f, 0f), on, on ? OnBg : RowBg);
                var icon = BoxIcon.Add(tile, BoxIcon.KindOf(tier), true, new Vector2(0.5f, 0f), new Vector2(0f, 60f));
                Txt(UiKit.Rect(tile, "n", Vector2.zero, new Vector2(1f, 0f), new Vector2(6f, 8f), new Vector2(-6f, 56f)),
                    $"{Label(tier)} ×{_ctx.Boxes?.Get(tier) ?? 0}", SmallSize, TierColor(tier), TextAnchor.MiddleCenter);
                if (!can) tile.gameObject.AddComponent<CanvasGroup>().alpha = 0.35f;
                else
                {
                    var chosen = tier;
                    Act(tile, () => { _boxTier = chosen; Rebuild(); });
                }
                _ = icon;
            }

            // Which slot, when chosen.
            if (_targeted && _boxTier != null)
            {
                Txt(Stack(40f, 12f), $"{Label(_boxTier.Value).ToUpperInvariant()} - PICK THE SLOT", SmallSize, Dim);
                const int cols = 7;
                int rows = (OpenChoices.Length + cols - 1) / cols;
                var grid = Stack(rows * 84f + (rows - 1) * 10f);
                for (int i = 0; i < OpenChoices.Length; i++)
                {
                    var (slot, cls, label) = OpenChoices[i];
                    bool on = _boxSlot == slot && (slot != GearSlot.Weapon || _boxClass == cls);
                    int c = i % cols, row = i / cols;
                    var cell = Framed(grid, new Vector2(c / (float)cols, 1f), new Vector2((c + 1) / (float)cols, 1f),
                        new Vector2(c == 0 ? 0f : 5f, -row * 94f - 84f), new Vector2(c == cols - 1 ? 0f : -5f, -row * 94f),
                        on, on ? OnBg : RowBg);
                    Txt(cell, label, SmallSize, Ink, TextAnchor.MiddleCenter);
                    Act(cell, () => { _boxSlot = slot; _boxClass = cls; Rebuild(); });
                }
            }

            // The verdict and OPEN.
            string note;
            bool ready = _boxTier != null && (!_targeted || _boxSlot != null);
            if (_boxTier == null)
                note = _targeted ? $"Choosing the slot takes {targetCost} Bronze, Silver or Gold boxes of one tier"
                                 : "No boxes yet - clear floors to find them";
            else
            {
                var tier = _boxTier.Value;
                int held = _ctx.Boxes.Get(tier);
                string what = !_targeted ? $"A random slot at {Label(tier)}"
                            : _boxSlot == null ? "Pick the slot"
                            : $"{Label(tier)} {(_boxSlot == GearSlot.Weapon ? WeaponName(_boxClass) : _boxSlot.Value.Label())}";
                note = ready ? $"{what} - spends {(cost == held ? $"all {cost}" : cost.ToString())} of your {held} {Label(tier)} boxes" : what;
                if (ready && cost == 1) note = $"{what} - spends 1 of your {held} {Label(tier)} boxes";
            }
            Footer(note, Dim, ("OPEN", true, ready, () =>
            {
                var tier = _boxTier.Value;
                var slot = _boxSlot;
                var cls = _boxClass;
                bool targeted = _targeted;
                Close();
                if (targeted) _ctx.OnRedeemTargeted?.Invoke(slot.Value, tier, cls);
                else _ctx.OnRedeemRandom?.Invoke(tier);
            }));
        }

        static string WeaponName(WeaponClass cls) => cls switch
        {
            WeaponClass.Disc => "Discs",
            _ => cls.ToString(),
        };

        // ------------------------------------------------------------------ Phial (the run's healing)

        /// <summary>The phial's three tiers side by side: what each holds, the one carried, and what
        /// the next costs - its own tier's boxes (Tuning.Phial).</summary>
        void BuildPhial()
        {
            Whole();
            int have = _ctx.PhialTier != null ? _ctx.PhialTier() : 0;
            Para("Full at a run's start - a drink back after each boss.", BodySize, Dim, 44f, 24f);
            var row = Stack(340f);
            int n = Tuning.Phial.Charges.Length;
            for (int t = 0; t < n; t++)
            {
                var tier = Tuning.Phial.TierOf(t);
                bool carried = t == have;
                var card = Framed(row, new Vector2(t / (float)n, 0f), new Vector2((t + 1) / (float)n, 1f),
                    new Vector2(t == 0 ? 0f : 12f, 0f), new Vector2(t == n - 1 ? 0f : -12f, 0f), carried, RowBg);
                Txt(UiKit.Rect(card, "t", new Vector2(0f, 1f), Vector2.one, new Vector2(16f, -90f), new Vector2(-16f, -24f)),
                    $"{Label(tier).ToUpperInvariant()} PHIAL", HeadSize, TierColor(tier), TextAnchor.MiddleCenter);
                Txt(UiKit.Rect(card, "h", new Vector2(0f, 1f), Vector2.one, new Vector2(16f, -190f), new Vector2(-16f, -100f)),
                    $"{Tuning.Phial.ChargesFor(t)} drinks\n{Mathf.RoundToInt(Tuning.Phial.HealFor(t) * 100f)}% health each",
                    BodySize, Ink, TextAnchor.MiddleCenter);
                var foot = UiKit.Rect(card, "f", Vector2.zero, new Vector2(1f, 0f), new Vector2(20f, 20f), new Vector2(-20f, 20f + ButtonH));
                if (carried) Txt(foot, "CARRIED", ButtonSize, Good, TextAnchor.MiddleCenter);
                else if (t < have) Txt(foot, "OUTGROWN", ButtonSize, Dim, TextAnchor.MiddleCenter);
                else
                {
                    int boxes = _ctx.Boxes.Get(tier);
                    int need = Tuning.Phial.CraftBoxes;
                    bool can = boxes >= need;
                    int chosen = t;
                    Button(foot, can ? $"FORGE - {need} BOXES" : $"{need} BOXES - HAVE {boxes}", true, can, () =>
                    {
                        if (_ctx.OnCraftPhial != null && _ctx.OnCraftPhial(chosen)) Rebuild();
                    }, 26);
                }
            }
            Para($"A phial is forged from {Tuning.Phial.CraftBoxes} boxes of its own tier.", SmallSize, Dim, 40f);
        }

        // ------------------------------------------------------------------ input

        readonly List<RectTransform> _focusRects = new();
        readonly List<(RectTransform Rect, Action OnTap)> _actions = new();

        void Act(RectTransform rect, Action tap) => _actions.Add((rect, tap));

        float _scroll;
        bool _scrolling, _pressed;
        Vector2 _scrollOrigin, _pressAt;
        float _scrollFrom;
        const float DragSlop = 64f, TapSlop = 400f;

        float ScrollMax => _pane == null ? 0f : Mathf.Max(0f, _listH - _pane.rect.height);

        void ApplyScroll()
        {
            _scroll = Mathf.Clamp(_scroll, 0f, ScrollMax);
            if (_listContent != null) _listContent.anchoredPosition = new Vector2(0f, _scroll);
        }

        int FocusedIndex()
        {
            var f = Controls.Focused;
            if (f == null) return -1;
            return _actions.FindIndex(a => a.Rect == f);
        }

        /// <summary>The D-pad keeps its place across a rebuild: the rect at the same index, which is
        /// the same button unless the rebuild changed the list.</summary>
        void RestoreFocus(int index)
        {
            if (index < 0 || _actions.Count == 0) return;
            _focusRects.Clear();
            foreach (var (rect, _) in _actions) _focusRects.Add(rect);
            Controls.SetFocusCandidates(_focusRects);
            Controls.Focus(_actions[Mathf.Min(index, _actions.Count - 1)].Rect);
        }

        /// <summary>Drag the list, or turn the wheel over it. A press only becomes a scroll once it
        /// has travelled far enough that it cannot have been meant as a tap (the same test the gear
        /// display screen and the mastery board use).</summary>
        void ScrollList()
        {
            if (_pane == null) return;
            var at = Controls.PointerPosition;
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(_pane, at, null);

            if (inside && Mathf.Abs(Controls.Zoom) > 0.01f) { _scroll -= Controls.Zoom * 90f; ApplyScroll(); }

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
            _scroll = _scrollFrom + dy / Mathf.Max(0.0001f, _pane.lossyScale.y);
            ApplyScroll();
        }

        void Update()
        {
            if (!IsOpen) return;

            _focusRects.Clear();
            foreach (var (rect, _) in _actions)
                if (rect != null) _focusRects.Add(rect);
            Controls.SetFocusCandidates(_focusRects);

            if (Time.frameCount == _openedFrame) return;   // the press that opened this
            if (Controls.CancelTapped) { Close(); return; }

            ScrollList();

            // A row the D-pad lands on below the fold scrolls up into the pane.
            var focused = Controls.Focused;
            if (focused != null && _listContent != null && focused.IsChildOf(_listContent))
            {
                float d = UiKit.ScrollToShow(_pane, focused);
                if (d != 0f) { _scroll += d; ApplyScroll(); }
            }

            // COMMITTED ON RELEASE, not on press - the same press may be the start of a drag of the
            // list, and a scroll's opening frame must not also pick the row the finger landed on.
            if (Controls.Tapped(out var press)) { _pressAt = press; _pressed = true; }
            if (!Controls.PointerReleased || !_pressed) return;
            _pressed = false;

            var at = Controls.PointerPosition;
            if ((at - _pressAt).sqrMagnitude > TapSlop) return;   // that was a drag

            foreach (var (rect, onTap) in _actions)
            {
                if (rect == null || !RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) continue;
                // A row scrolled out of the pane is clipped, not gone: only taps inside the pane reach it.
                if (_listContent != null && rect.IsChildOf(_listContent)
                    && !RectTransformUtility.RectangleContainsScreenPoint(_pane, at, null)) continue;
                onTap?.Invoke();
                return;
            }
        }
    }
}
