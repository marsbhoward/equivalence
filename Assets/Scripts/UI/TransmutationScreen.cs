using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Art;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The transmutation circle's screen: the loadout, plus everything about how the character
    /// LOOKS.
    ///
    /// Organised around the tria prima, which is not decoration - the three alchemical principles
    /// happen to name the three things this screen actually does, and using them as the tabs
    /// means the circle on the floor and the screen it opens are describing the same idea:
    ///
    ///   SULFUR   soul, the active principle   -> gear that fights: stats, power, durability
    ///   MERCURY  spirit, transformation       -> PERCEPTION: what the gear is seen as, form
    ///                                            without substance (a perception grants nothing)
    ///   SALT     body, fixity                 -> the character itself: skin, hair, eyes, face
    ///
    /// A PAPERDOLL (the user's reference, 2026-10-09): the character in the middle, one icon
    /// frame per slot down either side and the weapon and relic under the feet. Nothing is
    /// written beside an icon until it is HIGHLIGHTED - pointer hover, pad focus, or a first tap
    /// on touch (the second tap opens the picker) - and then the piece's full stats stand beside
    /// it, over the figure. SALT is the same frame with the body's categories in the slots and
    /// the chosen category's swatches in a panel beside it.
    ///
    /// Sulfur and Mercury are deliberately separate pages. They are the same slots asked two
    /// different questions, and a player handed a stat upgrade they think is ugly needs to see
    /// that those are independent decisions. Every slot sits in the same place on both, so
    /// switching tabs moves nothing.
    ///
    /// The quick loadout on [C] still exists and is unchanged - it opens mid-run, where a
    /// cosmetics studio has no business being.
    /// </summary>
    public class TransmutationScreen : MonoBehaviour
    {
        public enum Tab { Sulfur, Mercury, Salt }

        public bool IsOpen { get; private set; }

        Func<CharacterProfile> _profile;
        Action<GearSlot, string> _equip;
        Action _toggleHelm;
        Action _changed;

        ElementType _element;
        Tab _tab = Tab.Sulfur;

        GameObject _root;
        RectTransform _full;
        RectTransform _frame;      // the preview's frame - moved, never rebuilt, between tabs
        RectTransform _page;       // everything the current tab built
        RectTransform _tooltip;
        readonly CharacterPreview _preview = new();

        readonly List<Hit> _hits = new();
        readonly List<Action> _refresh = new();
        readonly List<RectTransform> _focusRects = new();
        readonly List<(RectTransform Rect, Tab Tab)> _tabs = new();
        readonly List<SlotCell> _cells = new();

        /// <summary>The slot whose stats are showing, or null.</summary>
        GearSlot? _highlight;
        /// <summary>On touch the highlight is a first tap, held until another tap moves it.</summary>
        GearSlot? _touchPick;
        GearSlot? _shownTooltip;
        Tab _shownTooltipTab;

        /// <summary>SALT's chosen category, an index into BodyCategories.</summary>
        int _bodyCat;

        struct Hit
        {
            public RectTransform Rect;
            public Action OnClick;
        }

        class SlotCell
        {
            public GearSlot Slot;
            public RectTransform Rect;
            public Image Border;
            /// <summary>No perception for this slot (the relic on MERCURY) - hover still explains
            /// why, a tap does nothing.</summary>
            public bool Locked;
            /// <summary>Which side the stats stand on: +1 to the right of the icon, -1 to the
            /// left, 0 above it.</summary>
            public int Side;
        }

        /// <param name="changed">Repaint every live rig and checkpoint the profile. Called after
        /// any edit, because appearance is account-level and permanent - the same write point
        /// equipping already uses.</param>
        public void Init(Func<CharacterProfile> profile, Action<GearSlot, string> equip,
                         Action toggleHelm, Action changed)
        {
            _profile = profile;
            _equip = equip;
            _toggleHelm = toggleHelm;
            _changed = changed;
        }

        // ------------------------------------------------------------------ layout

        static Vector2 Centre => new(0.5f, 0.5f);

        const float Icon = UiKit.TouchTarget, IconGap = 6f, ColumnTop = 220f;
        const float PreviewW = 340f, PreviewH = 530f;
        const float TooltipW = 400f, FloorY = -460f;

        /// <summary>Where the paperdoll stands: centred on the gear pages, moved left on SALT to
        /// make room for the swatch panel.</summary>
        static float DollCentre(Tab tab) => tab == Tab.Salt ? -580f : 0f;

        /// <summary>The icon column's inner offset from the doll's centre.</summary>
        static float ColumnInner(Tab tab) => tab == Tab.Salt ? 192f : 432f;

        static float RowTop(int row) => ColumnTop - row * (Icon + IconGap);
        static float BottomRowTop => ColumnTop - PreviewH - IconGap;

        /// <summary>Five down the left, five down the right, weapon and relic under the feet -
        /// the twelve wearable slots, laid out the way the body wears them.</summary>
        static readonly GearSlot[] LeftSlots =
            { GearSlot.Head, GearSlot.Neck, GearSlot.Shoulders, GearSlot.Back, GearSlot.Torso };
        static readonly GearSlot[] RightSlots =
            { GearSlot.Gloves, GearSlot.Belt, GearSlot.Legs, GearSlot.Boots, GearSlot.Ring };
        static readonly GearSlot[] BottomSlots = { GearSlot.Weapon, GearSlot.Relic };

        // ------------------------------------------------------------------ open / close

        public void Toggle(Transform canvas, ElementType element)
        {
            if (IsOpen) Close(); else Open(canvas, element);
        }

        public void Open(Transform canvas, ElementType element)
        {
            if (IsOpen || _profile == null) return;
            IsOpen = true;
            _element = element;
            GamePause.Hold(this);

            _weaponHiddenInPreview = false;
            _highlight = _touchPick = _shownTooltip = null;
            _preview.Build(_element, (int)PreviewW, (int)PreviewH);
            BuildChrome(canvas);
            BuildPage();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);

            // Before _root goes: the grid is parented to it, so destroying the screen would take
            // the picker's GameObject with it while the component still believed it was open -
            // and an open picker refuses to open again, so the slots would go dead for the rest
            // of the session with nothing on screen to say why.
            _picker?.Close();

            if (_root) Destroy(_root);
            _preview.Dispose();
            _hits.Clear();
            _refresh.Clear();
            _cells.Clear();
            _tabs.Clear();
            _root = null;
            _page = null;
            _tooltip = null;
        }

        // ------------------------------------------------------------------ chrome

        void BuildChrome(Transform canvas)
        {
            _root = new GameObject("TransmutationScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            _full = (RectTransform)_root.transform;
            _full.anchorMin = Vector2.zero; _full.anchorMax = Vector2.one;
            _full.offsetMin = Vector2.zero; _full.offsetMax = Vector2.zero;

            UiKit.Panel(_full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.032f, 0.045f, 1f));

            var title = UiKit.Rect(_full, "title", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -104), new Vector2(0, -44));
            UiKit.Label(title, "TRANSMUTATION", 42, new Color(0.93f, 0.94f, 0.97f), TextAnchor.MiddleCenter);

            var sub = UiKit.Rect(_full, "sub", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -136), new Vector2(0, -104));
            UiKit.Label(sub, "what you fight with, how you are perceived, and what you are",
                17, new Color(0.45f, 0.48f, 0.56f), TextAnchor.MiddleCenter);

            string[] names = { "SULFUR", "MERCURY", "SALT" };
            string[] blurbs = { "gear", "perception", "body" };
            for (int i = 0; i < 3; i++)
            {
                float x = -560 + i * 380;
                var t = UiKit.Panel(_full, Centre, Centre,
                                    new Vector2(x, 246), new Vector2(x + 360, 246 + UiKit.TouchTarget),
                                    new Color(0.09f, 0.10f, 0.14f, 1f));
                UiKit.Label(UiKit.Rect(t, "n", Vector2.zero, Vector2.one, new Vector2(20, 52), new Vector2(-20, -20)),
                            names[i], 26, Color.white, TextAnchor.MiddleLeft);
                UiKit.Label(UiKit.Rect(t, "b", Vector2.zero, Vector2.one, new Vector2(20, 18), new Vector2(-20, -66)),
                            blurbs[i], 17, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleLeft);
                _tabs.Add((t, (Tab)i));
            }

            // The preview's frame is built ONCE and only moved between tabs - the render texture
            // is sized to it (CharacterPreview.MatchTo), and the size never changes.
            _frame = UiKit.Panel(_full, Centre, Centre, Vector2.zero, Vector2.zero,
                                 new Color(0.07f, 0.08f, 0.11f, 1f));
            PlaceFrame();
            var imgGo = new GameObject("preview", typeof(RawImage));
            imgGo.transform.SetParent(_frame, false);
            var irt = (RectTransform)imgGo.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(8, 8); irt.offsetMax = new Vector2(-8, -8);
            imgGo.GetComponent<RawImage>().raycastTarget = false;
            _preview.MatchTo(imgGo.GetComponent<RawImage>(), _full.GetComponentInParent<Canvas>());

            var foot = UiKit.Rect(_full, "foot", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 34), new Vector2(0, 74));
            UiKit.Hint(foot,
                "point at a slot to read it, click to change    -    [ESC] or [E] to leave the circle    -    changes are saved as you make them",
                "tap a slot to read it, tap again to change    -    BACK to leave the circle    -    changes are saved as you make them",
                17, new Color(0.42f, 0.45f, 0.53f), TextAnchor.MiddleCenter,
                UI.GamepadGlyphs.Navigate + " to read a slot, " + UI.GamepadGlyphs.Confirm + " to change    -    "
                + UI.GamepadGlyphs.Cancel + " to leave the circle    -    changes are saved as you make them");
        }

        void PlaceFrame()
        {
            float cx = DollCentre(_tab);
            _frame.offsetMin = new Vector2(cx - PreviewW * 0.5f, ColumnTop - PreviewH);
            _frame.offsetMax = new Vector2(cx + PreviewW * 0.5f, ColumnTop);
        }

        // ------------------------------------------------------------------ pages

        void BuildPage()
        {
            _hits.Clear();
            _refresh.Clear();
            _cells.Clear();
            _shownTooltip = null;

            // Detached and hidden BEFORE Destroy, which is deferred to end of frame: left in
            // place, the outgoing tab draws over the incoming one for a frame and every tab
            // switch flickers a double image.
            foreach (var old in new[] { _page, _tooltip })
            {
                if (old == null) continue;
                old.SetParent(null, false);
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            _tooltip = null;

            foreach (var (rect, tab) in _tabs)
                rect.GetComponent<Image>().color = tab == _tab
                    ? new Color(0.16f, 0.17f, 0.23f, 1f)
                    : new Color(0.075f, 0.08f, 0.11f, 1f);

            PlaceFrame();
            _page = UiKit.Rect(_full, "page", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // Behind the frame, so the backdrop never covers the figure.
            _page.SetSiblingIndex(_frame.GetSiblingIndex());

            switch (_tab)
            {
                case Tab.Sulfur:  BuildDoll(perception: false); break;
                case Tab.Mercury: BuildDoll(perception: true); break;
                default:          BuildBody(); break;
            }

            Refresh();
        }

        /// <summary>A dark panel behind the whole paperdoll, so the slots read as one object.</summary>
        void Backdrop(float left, float right)
        {
            UiKit.Panel(_page, Centre, Centre, new Vector2(left, FloorY + 4f), new Vector2(right, ColumnTop + 12f),
                new Color(0.055f, 0.06f, 0.08f, 1f));
        }

        /// <summary>
        /// SULFUR (what is worn) or MERCURY (how it is perceived): the twelve slots as icons
        /// around the figure. SULFUR shows twelve; on MERCURY the relic's frame is LOCKED - a
        /// relic's content is its weapon art, and a perception deciding a mechanic is the one
        /// thing "a skin grants nothing" exists to forbid (GearSlots.Transmoggable).
        /// </summary>
        void BuildDoll(bool perception)
        {
            float cx = DollCentre(_tab), inner = ColumnInner(_tab);
            Backdrop(cx - inner - Icon - 20f, cx + inner + Icon + 20f);

            for (int i = 0; i < LeftSlots.Length; i++)
                SlotIcon(LeftSlots[i], cx - inner - Icon, RowTop(i), +1, perception);
            for (int i = 0; i < RightSlots.Length; i++)
                SlotIcon(RightSlots[i], cx + inner, RowTop(i), -1, perception);
            SlotIcon(GearSlot.Weapon, cx - Icon - IconGap * 0.5f, BottomRowTop, 0, perception);
            SlotIcon(GearSlot.Relic, cx + IconGap * 0.5f, BottomRowTop, 0, perception);

            // The bottom row's flanks, BETWEEN the side columns (the bottom row lines up with
            // their last row): the power readout on SULFUR, the perception toggles on MERCURY.
            // A whole touch target tall, like the icons beside them.
            float left = cx - inner + 12f, right = cx + inner - 12f;
            float rowL = cx - Icon - IconGap * 0.5f - 16f, rowR = cx + Icon + IconGap * 0.5f + 16f;
            float top = BottomRowTop, bottom = top - Icon;

            if (!perception)
            {
                var power = UiKit.Label(UiKit.Rect(_page, "power", Centre, Centre,
                    new Vector2(left, bottom), new Vector2(rowL, top)),
                    "", 18, new Color(0.7f, 0.73f, 0.8f), TextAnchor.MiddleLeft);
                _refresh.Add(() =>
                {
                    var p = _profile();
                    power.text = $"GEAR POWER  {p.Gear.TotalPower():0}\n{p.Gear.WornCount()} / {GearSlots.Worn.Length} worn";
                });
                return;
            }

            // HELMET is a real, persisted preference (CharacterProfile.HelmHidden) applied to
            // every rig showing this character. WEAPON is deliberately NOT persisted - it only
            // reaches the preview rig this screen owns, so hiding a greatsword to look at a cloak
            // underneath never touches what the arena, hub, or any other screen draws.
            float mid = (left + rowL) * 0.5f;
            var helmText = Button(left, mid - 5f, top, bottom, () => { _toggleHelm?.Invoke(); Refresh(); });
            _refresh.Add(() => helmText.text = _profile().HelmHidden ? "HELMET\nhidden" : "HELMET\nshown");

            var weaponText = Button(mid + 5f, rowL, top, bottom, () =>
            {
                _weaponHiddenInPreview = !_weaponHiddenInPreview;
                _preview.Rig?.SetWeaponHidden(_weaponHiddenInPreview);
                Refresh();
            });
            _refresh.Add(() => weaponText.text = _weaponHiddenInPreview ? "WEAPON\nhidden here" : "WEAPON\nshown");

            var clearText = Button(rowR, right, top, bottom, () =>
            {
                foreach (var s in GearSlots.All) _profile().Look.Transmog.Clear(s);
                Commit();
            });
            clearText.text = "CLEAR ALL\nPERCEPTIONS";
            clearText.color = new Color(0.85f, 0.7f, 0.7f);
        }

        Text Button(float left, float right, float top, float bottom, Action onClick)
        {
            var b = UiKit.Panel(_page, Centre, Centre, new Vector2(left, bottom), new Vector2(right, top),
                new Color(0.09f, 0.10f, 0.14f, 1f));
            _hits.Add(new Hit { Rect = b, OnClick = onClick });
            return UiKit.Label(b, "", 17, new Color(0.85f, 0.87f, 0.92f), TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// One slot's frame: the piece's own art, its tier as the border (the read before the
        /// name), stars along the foot of a minted piece. Nothing is written beside it - the
        /// stats stand next to it only while it is highlighted (<see cref="ShowTooltip"/>).
        /// </summary>
        void SlotIcon(GearSlot slot, float x, float top, int side, bool perception)
        {
            bool locked = perception && Array.IndexOf(GearSlots.Transmoggable, slot) < 0;

            var frame = UiKit.Panel(_page, Centre, Centre, new Vector2(x, top - Icon), new Vector2(x + Icon, top),
                new Color(0.18f, 0.19f, 0.24f, 1f));
            var well = UiKit.Panel(frame, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4),
                new Color(0.085f, 0.095f, 0.13f, 1f));

            var art = UiKit.Rect(well, "art", Vector2.zero, Vector2.one, new Vector2(10, 14), new Vector2(-10, -10));
            var img = art.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;

            // An empty slot (or a minted piece with no art) names itself instead.
            var name = UiKit.Label(well, "", 14, new Color(0.38f, 0.40f, 0.47f), TextAnchor.MiddleCenter);

            var stars = UiKit.Rect(well, "stars", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2), new Vector2(0, 20));

            // A slot whose look is overridden is flagged on SULFUR too, or the stat page quietly
            // lies about what the character looks like.
            var mark = UiKit.Panel(well, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(-4, -4),
                MercuryInk);

            var border = frame.GetComponent<Image>();
            var cell = new SlotCell { Slot = slot, Rect = frame, Border = border, Locked = locked, Side = side };
            _cells.Add(cell);
            _hits.Add(new Hit { Rect = frame, OnClick = () => PressSlot(cell, perception) });

            GearItem shownItem = null;
            _refresh.Add(() =>
            {
                var p = _profile();
                var real = GearCatalog.Get(p.Gear.Get(slot));
                var over = perception && !locked ? GearCatalog.Get(p.Look.Transmog.Get(slot)) : null;
                if (Appearance.SameLook(over, real)) over = null;   // perceived as itself is no perception
                var item = perception ? (over ?? real) : real;

                // Rebuilt only when the piece changes - the Secret Fire and weapon extras hang
                // components off the image, and redoing them every refresh would stack them.
                if (item != shownItem)
                {
                    shownItem = item;
                    foreach (Transform c in art) Destroy(c.gameObject);
                    foreach (Transform c in stars) Destroy(c.gameObject);
                    var layer = item != null && !locked ? Hub.GearDisplay.Represent(item) : null;
                    img.sprite = layer?.Sprite;
                    img.enabled = layer?.Sprite != null;
                    if (layer?.Sprite != null)
                    {
                        WeaponExtras.AddHalo(img, item, layer.Sprite);
                        WeaponExtras.AddGemGlow(img, item, layer.Sprite);
                        KindledImage.On(img, item);
                    }
                    if (item != null && item.Stars >= 0)
                        UiKit.Stars(stars, item.Stars, GearRoller.MaxLevel, 16f, StarOn, StarOff);
                }

                var tint = shownItem != null ? (Hub.GearDisplay.Represent(shownItem)?.Tint ?? Color.white) : Color.white;
                // "As equipped" on MERCURY: the real piece, dimmed - unchanged, not empty.
                bool passThrough = perception && over == null;
                img.color = passThrough ? new Color(tint.r, tint.g, tint.b, 0.35f) : tint;

                name.text = locked ? "RELIC\nno perception"
                          : item == null ? slot.ToString().ToUpper()
                          : img.enabled ? "" : item.DisplayName.Replace(" ", "\n");
                name.color = item != null && !img.enabled && !locked ? CharacterScreen.TierColor(item.Tier)
                                                          : new Color(0.38f, 0.40f, 0.47f);

                mark.gameObject.SetActive(!perception && p.Look.IsTransmogged(slot, p.Gear));

                var edge = locked ? new Color(0.12f, 0.12f, 0.15f)
                         : item == null || passThrough ? new Color(0.18f, 0.19f, 0.24f)
                         : CharacterScreen.TierColor(item.Tier);
                border.color = _highlight == slot ? Color.Lerp(edge, Color.white, 0.45f) : edge;
            });
        }

        void PressSlot(SlotCell cell, bool perception)
        {
            // Touch has no hover: the first tap READS the slot, the second changes it.
            if (Core.Controls.TouchMode && _touchPick != cell.Slot)
            {
                _touchPick = cell.Slot;
                return;
            }
            if (cell.Locked) return;
            OpenPicker(cell.Slot, perception);
        }

        // ------------------------------------------------------------------ the tooltip

        static readonly Color Ink = new(0.85f, 0.87f, 0.92f);
        static readonly Color Dim = new(0.5f, 0.53f, 0.6f);
        static readonly Color MercuryInk = new(0.62f, 0.56f, 0.78f);
        static readonly Color StarOn = new(1f, 0.82f, 0.3f);
        static readonly Color StarOff = new(1f, 1f, 1f, 0.12f);

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        static string Pct(float v) => $"+{v:0.#}%";

        /// <summary>
        /// The highlighted slot's full read, standing beside its icon over the figure - where the
        /// reference puts an item's additions. Built when the highlight changes, not every frame.
        /// </summary>
        void ShowTooltip(SlotCell cell)
        {
            if (_tooltip != null) Destroy(_tooltip.gameObject);
            _tooltip = null;
            _shownTooltip = cell?.Slot;
            _shownTooltipTab = _tab;
            if (cell == null) return;

            var lines = _tab == Tab.Mercury ? PerceptionLines(cell) : GearLines(cell.Slot);
            int count = lines.Split('\n').Length;
            float h = 28f + count * 23f;

            var icon = cell.Rect;
            float iconLeft = icon.offsetMin.x, iconRight = icon.offsetMax.x, iconTop = icon.offsetMax.y;
            float left = cell.Side > 0 ? iconRight + 8f
                       : cell.Side < 0 ? iconLeft - 8f - TooltipW
                       : (iconLeft + iconRight) * 0.5f - TooltipW * 0.5f;
            float top = cell.Side == 0 ? iconTop + 8f + h : iconTop;
            top = Mathf.Max(top, FloorY + h);   // never past the foot of the screen

            // Edged in the piece's tier, like the slot it reads.
            _tooltip = UiKit.Panel(_full, Centre, Centre, new Vector2(left, top - h), new Vector2(left + TooltipW, top),
                cell.Border != null ? cell.Border.color : Dim);
            var body = UiKit.Panel(_tooltip, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2),
                new Color(0.035f, 0.04f, 0.06f, 0.97f));
            var t = UiKit.Label(UiKit.Rect(body, "t", Vector2.zero, Vector2.one, new Vector2(14, 12), new Vector2(-14, -12)),
                lines, 17, Ink, TextAnchor.UpperLeft);
            t.supportRichText = true;
            t.lineSpacing = 1.05f;
        }

        /// <summary>SULFUR: the worn piece - name, tier and stars, every stat, its ability or
        /// weapon art, its condition, and what it is perceived as.</summary>
        string GearLines(GearSlot slot)
        {
            var p = _profile();
            var item = GearCatalog.Get(p.Gear.Get(slot));
            var sb = new StringBuilder();
            if (item == null)
                return $"<color=#{Hex(Dim)}>{slot.ToString().ToUpper()}</color>\nnothing worn";

            sb.Append($"<size=21><color=#{Hex(CharacterScreen.TierColor(item.Tier))}>{item.DisplayName}</color></size>");
            string tier = item.Tier == LootTier.BlackDiamond ? "Black Diamond" : item.Tier.ToString();
            string stars = item.Stars > 0 ? $"  {item.Stars} star{(item.Stars == 1 ? "" : "s")}" : "";
            string kind = slot == GearSlot.Weapon ? WeaponClasses.Name(item.Class) : slot.ToString();
            sb.Append($"\n<color=#{Hex(Dim)}>{tier}{stars}  {kind}</color>");

            var record = p.MintedGear.Find(r => r.InstanceId == item.ItemId);
            if (record != null && record.PrimaryStat != StatKind.None)
            {
                float scale = GearRoller.UpgradeScale(record.UpgradeLevel);
                // The Forge's own read of a roll: primary, then each sub-stat, scaled by stars.
                sb.Append($"\n{GearForge.Label(record.PrimaryStat)} {Pct(GearRoller.PrimaryPoints(record.PrimaryStat, record.Tier, record.Slot) * scale)}");
                if (record.SubStats != null)
                    foreach (var sub in record.SubStats)
                        if (sub != null)
                            sb.Append($"\n<color=#{Hex(Dim)}>  {GearForge.Label(sub.Kind)} {Pct(sub.Value * scale)}</color>");
            }
            else
            {
                foreach (StatKind k in Enum.GetValues(typeof(StatKind)))
                {
                    if (k == StatKind.None) continue;
                    float v = GearRoller.PointsOf(item.Grants, k);
                    if (Mathf.Abs(v) > 0.01f) sb.Append($"\n{GearForge.Label(k)} {Pct(v)}");
                }
                if (item.Power > 0f) sb.Append($"\n<color=#{Hex(Dim)}>power {item.Power:0}</color>");
            }

            if (slot == GearSlot.Torso) sb.Append($"\n<color=#{Hex(MercuryInk)}>{item.DefensiveAbility.Label()}</color>");
            var art = item.GrantedFinisher;
            if (art != null)
                sb.Append($"\n<color=#{Hex(MercuryInk)}>weapon art: {art.DisplayName}{(item.Signature != null ? "  (locked)" : "")}</color>");

            if (GearSlots.Kind(slot) != SlotKind.Cosmetic && item.MaxDurability > 0f)
            {
                float cond = p.Wear.Get(item.ItemId) / item.MaxDurability;
                sb.Append($"\n<color=#{Hex(Dim)}>condition {Mathf.Clamp01(cond) * 100f:0}%</color>");
            }

            if (p.Look.IsTransmogged(slot, p.Gear))
            {
                var over = GearCatalog.Get(p.Look.Transmog.Get(slot));
                if (over != null) sb.Append($"\n<color=#{Hex(MercuryInk)}>perceived as {over.DisplayName}</color>");
            }
            return sb.ToString();
        }

        /// <summary>MERCURY: what the slot is perceived as, over what is really worn. A
        /// perception grants nothing, so there are no stats to read.</summary>
        string PerceptionLines(SlotCell cell)
        {
            var p = _profile();
            if (cell.Locked)
                return $"<color=#{Hex(Dim)}>RELIC</color>\na relic has no perception -\nits roll is its weapon art";

            var real = GearCatalog.Get(p.Gear.Get(cell.Slot));
            var over = GearCatalog.Get(p.Look.Transmog.Get(cell.Slot));
            string realName = real != null ? real.DisplayName : "nothing";
            if (over == null || Appearance.SameLook(over, real))
                return $"<color=#{Hex(Dim)}>{cell.Slot.ToString().ToUpper()}</color>\nas equipped  ({realName})";

            var sb = new StringBuilder();
            sb.Append($"<size=21><color=#{Hex(CharacterScreen.TierColor(over.Tier))}>{over.DisplayName}</color></size>");
            sb.Append($"\n<color=#{Hex(Dim)}>perceived over {realName}</color>");
            if (!p.Look.IsTransmogged(cell.Slot, p.Gear))
                sb.Append($"\n<color=#{Hex(MercuryInk)}>not shown - the held weapon is another class</color>");
            sb.Append($"\n<color=#{Hex(Dim)}>a perception grants nothing</color>");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ SALT

        /// <summary>
        /// Preview-only, never saved - see the toggle on MERCURY. Reset every time the circle is
        /// opened, so a hidden weapon does not silently carry over from a previous visit with
        /// nothing on screen to explain why the sword is missing.
        /// </summary>
        bool _weaponHiddenInPreview;

        /// <summary>One body category: a slot around the figure, and the options it opens.</summary>
        class BodyCategory
        {
            public string Label;
            public BodyLook.Swatch[] Swatches;   // a colour row, or
            public BodyLook.Style[] Styles;      // a named row
            public Func<Appearance, string> Get;
            public Action<Appearance, string> Set;
            /// <summary>Replaces the panel's heading when the row needs to say something about
            /// itself (three faces paint their own eyes; brows can follow the hair).</summary>
            public Func<Appearance, string> Note;
        }

        static readonly BodyCategory[] BodyCategories =
        {
            new() { Label = "SKIN", Swatches = BodyLook.Skins, Get = a => a.Skin, Set = (a, k) => a.Skin = k },
            // Three faces paint their own eyes, and a swatch that silently does nothing is worse
            // than one that says so.
            new() { Label = "EYES", Swatches = BodyLook.Eyes, Get = a => a.Eyes, Set = (a, k) => a.Eyes = k,
                    Note = a => BodyLook.OverridesEyeColour(a.Expression) ? "EYES    (this face sets its own)" : "EYES" },
            new() { Label = "EXPRESSION", Styles = BodyLook.Expressions, Get = a => a.Expression, Set = (a, k) => a.Expression = k },
            new() { Label = "EYEBROWS", Styles = BodyLook.Brows, Get = a => a.Brows, Set = (a, k) => a.Brows = k },
            // MATCH is the default and the one most characters want - brows following the hair
            // reads as deliberate rather than as an oversight.
            new() { Label = "BROW COLOUR", Swatches = BodyLook.BrowSwatches, Get = a => a.BrowColour, Set = (a, k) => a.BrowColour = k,
                    Note = a => string.IsNullOrEmpty(a.BrowColour) ? "BROW COLOUR    (matching hair)" : "BROW COLOUR" },
            new() { Label = "HAIR STYLE", Styles = BodyLook.HairStyles, Get = a => a.HairStyle, Set = (a, k) => a.HairStyle = k },
            new() { Label = "HAIR COLOUR", Swatches = BodyLook.Hairs, Get = a => a.Hair, Set = (a, k) => a.Hair = k },
            new() { Label = "BEARD", Styles = BodyLook.Beards, Get = a => a.Beard, Set = (a, k) => a.Beard = k },
        };

        /// <summary>Face and skin down the left, hair down the right.</summary>
        const int BodyLeftCount = 5;

        /// <summary>
        /// SALT: the character underneath the gear, in the same frame - each category a slot
        /// around the figure showing what it is set to, the chosen one's options in the panel
        /// to the right.
        /// </summary>
        void BuildBody()
        {
            float cx = DollCentre(Tab.Salt), inner = ColumnInner(Tab.Salt);
            Backdrop(cx - inner - Icon - 20f, cx + inner + Icon + 20f);

            for (int i = 0; i < BodyCategories.Length; i++)
            {
                bool leftSide = i < BodyLeftCount;
                int row = leftSide ? i : i - BodyLeftCount;
                float x = leftSide ? cx - inner - Icon : cx + inner;
                BodyIcon(i, x, RowTop(row));
            }

            BuildBodyPanel(cx + inner + Icon + 60f, 900f);
        }

        void BodyIcon(int index, float x, float top)
        {
            var cat = BodyCategories[index];
            var frame = UiKit.Panel(_page, Centre, Centre, new Vector2(x, top - Icon), new Vector2(x + Icon, top),
                new Color(0.18f, 0.19f, 0.24f, 1f));
            var well = UiKit.Panel(frame, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4),
                new Color(0.085f, 0.095f, 0.13f, 1f));
            UiKit.Label(UiKit.Rect(well, "l", new Vector2(0, 1), new Vector2(1, 1), new Vector2(4, -24), new Vector2(-4, -4)),
                cat.Label, 12, Dim, TextAnchor.MiddleCenter);

            Image chip = null;
            Text value = null;
            if (cat.Swatches != null)
                chip = UiKit.Panel(well, Vector2.zero, Vector2.one, new Vector2(18, 14), new Vector2(-18, -30),
                    Color.clear).GetComponent<Image>();
            else
                value = UiKit.Label(UiKit.Rect(well, "v", Vector2.zero, Vector2.one, new Vector2(4, 8), new Vector2(-4, -28)),
                    "", 16, Ink, TextAnchor.MiddleCenter);

            var border = frame.GetComponent<Image>();
            _hits.Add(new Hit { Rect = frame, OnClick = () => { _bodyCat = index; BuildPage(); } });
            _refresh.Add(() =>
            {
                var look = _profile().Look;
                string key = cat.Get(look);
                if (chip != null) chip.color = cat.Swatches[BodyLook.IndexOf(cat.Swatches, key)].Color;
                if (value != null) value.text = cat.Styles[BodyLook.IndexOf(cat.Styles, key)].Name.Replace(" ", "\n");
                border.color = _bodyCat == index ? new Color(1f, 0.92f, 0.6f) : new Color(0.18f, 0.19f, 0.24f);
            });
        }

        /// <summary>The chosen category's options, a finger wide, wrapping to the panel.</summary>
        void BuildBodyPanel(float left, float right)
        {
            var cat = BodyCategories[Mathf.Clamp(_bodyCat, 0, BodyCategories.Length - 1)];
            float width = right - left;

            var head = UiKit.Label(UiKit.Rect(_page, "head", Centre, Centre,
                new Vector2(left, ColumnTop - 30f), new Vector2(right, ColumnTop)),
                cat.Label, 18, Dim, TextAnchor.MiddleLeft);
            if (cat.Note != null) _refresh.Add(() => head.text = cat.Note(_profile().Look));

            const float gap = 10f, lineGap = 8f;
            float top = ColumnTop - 44f;
            int n = cat.Swatches?.Length ?? cat.Styles.Length;

            // Swatches are square; named chips share the line before they wrap, never narrower
            // than a finger.
            float w = cat.Swatches != null ? Icon
                    : Mathf.Clamp((width + gap) / n - gap, Icon, 200f);
            int perLine = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (w + gap)));

            for (int i = 0; i < n; i++)
            {
                float x = left + (i % perLine) * (w + gap);
                float y = top - (i / perLine) * (Icon + lineGap);
                var cell = UiKit.Panel(_page, Centre, Centre, new Vector2(x, y - Icon), new Vector2(x + w, y),
                    new Color(0.16f, 0.17f, 0.22f));
                var bg = cell.GetComponent<Image>();
                string key;

                if (cat.Swatches != null)
                {
                    var sw = cat.Swatches[i];
                    key = sw.Key;
                    UiKit.Panel(cell, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4), sw.Color);
                    _refresh.Add(() => bg.color = cat.Get(_profile().Look) == key
                        ? new Color(1f, 0.92f, 0.6f) : new Color(0.16f, 0.17f, 0.22f));
                }
                else
                {
                    var st = cat.Styles[i];
                    key = st.Key;
                    var text = UiKit.Label(cell, st.Name, 17, Color.white, TextAnchor.MiddleCenter);
                    _refresh.Add(() =>
                    {
                        bool on = cat.Get(_profile().Look) == key;
                        bg.color = on ? new Color(0.22f, 0.20f, 0.13f) : new Color(0.09f, 0.10f, 0.14f);
                        text.color = on ? new Color(1f, 0.92f, 0.6f) : new Color(0.65f, 0.68f, 0.75f);
                    });
                }

                _hits.Add(new Hit { Rect = cell, OnClick = () => { cat.Set(_profile().Look, key); Commit(); } });
            }
        }

        // ------------------------------------------------------------------ edits

        /// <summary>The per-slot grid, shared with the loadout screen. Created on demand and
        /// kept, so reopening a slot does not churn a component every press.</summary>
        GearPicker _picker;

        /// <summary>True while that grid is up. This screen stands down entirely then - nothing
        /// in this project consumes input, so without it a tap on a picker card would also land on
        /// the slot behind it, and Cancel would close both at once.</summary>
        public bool PickerOpen => _picker != null && _picker.IsOpen;

        /// <summary>
        /// Open the grid for one slot, on either question - what to WEAR (sulfur) or how to be
        /// PERCEIVED (mercury). The grid's EMPTY cell is "as equipped" on mercury: a player who
        /// changes a slot's perception has to be able to get back to showing what they really
        /// wear without hunting for the matching item.
        /// </summary>
        void OpenPicker(GearSlot slot, bool perception)
        {
            if (_root == null || PickerOpen) return;
            HideTooltip();

            // The relic socket has no items of its own: a candidate declares Slot.Weapon, because
            // the same sword has to stay wieldable. Its list is filtered by what is being HELD.
            var held = GearCatalog.Get(_profile().Gear.Get(GearSlot.Weapon));
            _picker ??= gameObject.AddComponent<GearPicker>();

            if (!perception)
            {
                _picker.Open(_root.transform, slot, held, _profile().Gear.Get(slot),
                             id => { _equip(slot, id); Refresh(); },
                             id => _profile().Wear.Get(id));
                return;
            }

            // A weapon may only be perceived as another weapon of its own class - the class is
            // how the thing is HELD, so the list is narrowed before the grid ever sees it rather
            // than offered and then refused at the point of use.
            // ...and never as ITSELF: the worn piece is the "as equipped" cell, not a perception
            // (the user's call, 2026-10-09) - choosing it would change nothing and still mark
            // the slot as perceived.
            var worn = GearCatalog.Get(_profile().Gear.Get(slot));
            // A piece with no art has no look to be perceived as (stat-rolled gear today).
            var options = SameClassOnly(slot, GearCatalog.ForSlot(slot))
                .FindAll(o => !Appearance.SameLook(o, worn) && o.Layers != null && o.Layers.Length > 0);

            _picker.Open(_root.transform, slot, held, _profile().Look.Transmog.Get(slot),
                         id => { _profile().Look.Transmog.Set(slot, id); Commit(); },
                         wear: null,                       // a perception never wears
                         stackBy: GearPicker.StackBy.Appearance,
                         options: options,
                         emptyLabel: "as equipped",
                         titleSuffix: "PERCEPTION",
                         wornLabel: "shown",
                         emptyItem: worn);
        }

        /// <summary>
        /// A WEAPON may only be perceived as another weapon of its own class.
        ///
        /// Not a taste rule - the class decides how the thing is HELD. `TwoHanded` brings the off
        /// hand across onto the same hilt and permutes the layer stack (TwoHandedOrder); a disc
        /// uses DualWieldOrder and a one-handed grip, with the fist showing through the ring. Cross
        /// the two and the character either two-hands a chakram or holds a greatsword by nothing,
        /// because the grip comes from one item and the picture from another.
        ///
        /// Every other slot is unrestricted: a helmet is a helmet.
        /// </summary>
        List<GearItem> SameClassOnly(GearSlot slot, List<GearItem> options)
        {
            if (slot != GearSlot.Weapon) return options;

            var held = GearCatalog.Get(_profile().Gear.Get(GearSlot.Weapon));
            if (held == null) return options;

            var same = new List<GearItem>();
            foreach (var o in options)
                if (o.Class == held.Class && o.TwoHanded == held.TwoHanded) same.Add(o);
            return same;
        }

        /// <summary>Persist and repaint everything showing this character.</summary>
        void Commit()
        {
            _changed?.Invoke();
            Refresh();
        }

        public void Refresh()
        {
            if (!IsOpen || _profile == null) return;
            var p = _profile();

            foreach (var r in _refresh) r();
            _preview.Refresh(p);

            // A change can alter what the highlighted slot says (equipped, perceived, worn).
            if (_shownTooltip != null)
            {
                var cell = _cells.Find(c => c.Slot == _shownTooltip);
                ShowTooltip(cell);
            }
        }

        void HideTooltip()
        {
            if (_tooltip != null) Destroy(_tooltip.gameObject);
            _tooltip = null;
            _shownTooltip = null;
        }

        // ------------------------------------------------------------------ input

        /// <summary>
        /// Which slot is highlighted this frame: the pad's focus, the mouse's hover, or touch's
        /// last tap. Asked every frame - the device can change mid-screen.
        /// </summary>
        GearSlot? CurrentHighlight()
        {
            if (Core.Controls.TouchMode) return _touchPick;

            if (Core.Controls.GamepadMode)
            {
                var focused = Core.Controls.Focused;
                foreach (var c in _cells) if (c.Rect == focused) return c.Slot;
                return null;
            }

            var at = Core.Controls.PointerPosition;
            foreach (var c in _cells)
                if (RectTransformUtility.RectangleContainsScreenPoint(c.Rect, at, null)) return c.Slot;
            return null;
        }

        void Update()
        {
            if (!IsOpen) return;

            // The slot grid covers this screen and handles its own taps and its own Cancel.
            // Nothing here consumes input, so without standing down a card press would also hit
            // the slot behind it and one Cancel would close the grid AND the screen under it.
            if (PickerOpen) return;

            if (Core.Controls.CancelTapped || Core.Controls.InteractTapped)
            {
                Close();
                return;
            }

            _focusRects.Clear();
            foreach (var (rect, _) in _tabs) _focusRects.Add(rect);
            foreach (var h in _hits)
                if (h.Rect != null) _focusRects.Add(h.Rect);
            Core.Controls.SetFocusCandidates(_focusRects);

            if (Core.Controls.Tapped(out var at))
            {
                foreach (var (rect, tab) in _tabs)
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) continue;
                    if (tab != _tab)
                    {
                        _tab = tab;
                        _touchPick = null;
                        BuildPage();
                    }
                    return;
                }

                bool hitSomething = false;
                foreach (var h in _hits)
                {
                    if (h.Rect == null || !RectTransformUtility.RectangleContainsScreenPoint(h.Rect, at, null))
                        continue;
                    h.OnClick?.Invoke();
                    hitSomething = true;
                    break;
                }
                // A tap on nothing puts a touch highlight away.
                if (!hitSomething) _touchPick = null;
                if (!IsOpen || PickerOpen) return;
            }

            var now = CurrentHighlight();
            if (now != _highlight)
            {
                _highlight = now;
                foreach (var r in _refresh) r();   // the borders follow the highlight
            }
            if (_highlight != _shownTooltip || (_tooltip != null && _shownTooltipTab != _tab))
                ShowTooltip(_highlight == null ? null : _cells.Find(c => c.Slot == _highlight));
        }
    }
}
