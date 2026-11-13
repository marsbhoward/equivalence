using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The loadout's stats panel - mock E, the user's pick (2026-11-12): a strip, the stats as tiles
    /// three to a row in four groups, and a footnote at the foot.
    ///
    /// It reads the CHARACTER by default - effective values, after the curves, what the fight uses.
    /// A pressed slot reads that piece instead: the same tiles, the ones it feeds lit with how much
    /// it adds, the rest faded, so the piece is read against the whole. Pressing it again goes back.
    ///
    /// The number is the read; a stat's meaning is in the footnote, for the tile under the pointer
    /// or the pad, else the last one tapped, else the first (a piece's: its primary). Meanings under
    /// every stat outran the panel on a full kit - this is Glossary's Highlight + Footnotes.
    /// </summary>
    public partial class CharacterScreen
    {
        RectTransform _statsPanel, _statsBody;
        Text _noteTitle, _noteText;

        /// <summary>The footnote's stat when nothing is under the pointer or pad: the last tile
        /// tapped, or null for the read's own default.</summary>
        StatKind? _noteStat;
        StatKind? _noteDefault;

        struct StatTile { public RectTransform Rect; public StatKind Kind; public Outline Outline; }
        readonly List<StatTile> _statTiles = new();

        /// <summary>The groups, in order: what a stat is FOR. Every StatKind is in exactly one.</summary>
        static readonly (string Name, StatKind[] Kinds)[] StatGroups =
        {
            ("STRIKE", new[] { StatKind.Damage, StatKind.AttackSpeed, StatKind.CritChance, StatKind.CritDamage,
                               StatKind.Accuracy, StatKind.ComboTime, StatKind.FinisherPower,
                               StatKind.FinisherKnockback, StatKind.Cleave, StatKind.Splash, StatKind.Pierce,
                               StatKind.Range, StatKind.MoveSpeed }),
            ("ELEMENT", new[] { StatKind.ElementalEffectiveness, StatKind.ElementGrowth, StatKind.AoeRadius,
                                StatKind.AbilityCooldownReduction }),
            ("BODY",    new[] { StatKind.MaxHp, StatKind.Armor, StatKind.DamageResistance, StatKind.Resilience,
                                StatKind.Graze, StatKind.Brace }),
            ("UPKEEP",  new[] { StatKind.HealReceived, StatKind.RepairReceived, StatKind.Mend }),
        };

        static readonly Color StatInk = new(0.85f, 0.87f, 0.92f), StatDim = new(0.5f, 0.53f, 0.6f),
                              StatFaint = new(0.33f, 0.35f, 0.42f), StatGold = new(0.94f, 0.82f, 0.46f),
                              StatWell = new(0.098f, 0.106f, 0.141f), StatHit = new(0.133f, 0.122f, 0.086f),
                              StatHp = new(0.94f, 0.65f, 0.64f), StatHpBar = new(0.85f, 0.33f, 0.31f),
                              StatViolet = new(0.73f, 0.55f, 0.94f);

        const float SPad = 20f, StripTop = 86f, StripH = 50f, GridTop = 150f, NoteH = 104f,
                    TileGap = 6f, GroupH = 22f, Cols = 3f;

        void BuildStats()
        {
            if (_statsPanel == null) return;
            if (_statsBody != null) Destroy(_statsBody.gameObject);
            _statTiles.Clear();
            _statsBody = UiKit.Rect(_statsPanel, "stats", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var p = _profile();
            GearItem item = _inspected is GearSlot slot ? GearCatalog.Get(p.Gear.Get(slot)) : null;
            bool piece = _inspected != null;

            // Totals: the character's effective block. A piece's own points are its raw grants.
            StatPercents totals;
            float maxHp = 0f;
            if (_character != null) (totals, maxHp) = _character();
            else totals = p.Gear.TotalStats();

            // ---- header
            string title, sub;
            Color titleInk = StatInk;
            if (!piece)
            {
                title = string.IsNullOrWhiteSpace(p.Name) ? "CHARACTER" : p.Name.ToUpperInvariant();
                var weapon = GearCatalog.Get(p.Gear.Get(GearSlot.Weapon));
                sub = $"{_element}  -  {WeaponClasses.Name(weapon?.Class ?? WeaponClass.Greatsword)}"
                    + $"  -  gear power {p.Gear.TotalPower():0}";
            }
            else if (item == null)
            {
                title = _inspected.Value.Label().ToUpperInvariant();
                sub = "nothing worn";
            }
            else
            {
                title = item.DisplayName;
                titleInk = TierColor(item.Tier);
                string tier = item.Tier == LootTier.BlackDiamond ? "Black Diamond" : item.Tier.ToString();
                string kind = item.Slot == GearSlot.Weapon ? WeaponClasses.Name(item.Class) : item.Slot.Label();
                sub = $"{tier} {kind}";
                if (GearSlots.Kind(item.Slot) != SlotKind.Cosmetic && item.MaxDurability > 0f)
                    sub += $"  -  condition {Mathf.Clamp01(p.Wear.Get(item.ItemId) / item.MaxDurability) * 100f:0}%";
            }
            var titleRt = Top(SPad, SPad + 30f);
            UiKit.Label(titleRt, title, 21, titleInk, TextAnchor.MiddleLeft);
            UiKit.Label(Top(SPad + 32f, SPad + 54f), sub, 14, StatDim, TextAnchor.MiddleLeft);
            if (item != null && item.Stars >= 0)
                UiKit.Stars(UiKit.Rect(titleRt, "stars", new Vector2(1, 0), new Vector2(1, 1),
                                new Vector2(-66, 4), new Vector2(0, -4)),
                            item.Stars, GearRoller.MaxLevel, 20f, new Color(1f, 0.82f, 0.3f), new Color(1f, 1f, 1f, 0.12f));

            // ---- the strip: health, or what the piece does beyond its numbers
            StatKind? primary = null;
            if (item != null)
            {
                var record = p.MintedGear.Find(r => r.InstanceId == item.ItemId);
                if (record != null && record.PrimaryStat != StatKind.None) primary = record.PrimaryStat;
                else
                {
                    float best = 0.01f;
                    foreach (var (_, kinds) in StatGroups)
                        foreach (var k in kinds)
                        {
                            float v = GearRoller.PointsOf(item.Grants, k);
                            if (v > best) { best = v; primary = k; }
                        }
                }
            }

            var strip = UiKit.Panel(_statsBody, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(SPad, -StripTop - StripH), new Vector2(-SPad, -StripTop), StatWell);
            var bar = UiKit.Panel(strip, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(4, 0),
                piece ? StatViolet : StatHpBar);
            bar.GetComponent<Image>().raycastTarget = false;
            var left = UiKit.Rect(strip, "l", Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-12, 0));
            if (!piece)
            {
                UiKit.Label(left, "HEALTH", 15, StatDim, TextAnchor.MiddleLeft);
                UiKit.Label(left, maxHp > 0f ? maxHp.ToString("0") : "-", 28, StatHp, TextAnchor.MiddleRight);
            }
            else
            {
                string what = item == null ? "" :
                              item.Slot == GearSlot.Torso ? item.DefensiveAbility.Label() :
                              item.GrantedFinisher != null ? "weapon art: " + item.GrantedFinisher.DisplayName :
                              item.Slot.Label();
                UiKit.Label(left, what, 16, StatViolet, TextAnchor.MiddleLeft);
                if (primary != null)
                    UiKit.Label(left, "primary  -  " + GearForge.Label(primary.Value), 14, StatDim, TextAnchor.MiddleRight);
            }

            // ---- the tiles: every stat the character has, or the piece gives
            var shown = new List<(string Group, List<(StatKind K, float Total, float Mine)> Stats)>();
            int rows = 0;
            foreach (var (name, kinds) in StatGroups)
            {
                var list = new List<(StatKind, float, float)>();
                foreach (var k in kinds)
                {
                    float total = GearRoller.PointsOf(totals, k);
                    float mine = item != null ? GearRoller.PointsOf(item.Grants, k) : 0f;
                    if (Mathf.Abs(total) > 0.01f || Mathf.Abs(mine) > 0.01f) list.Add((k, total, mine));
                }
                if (list.Count == 0) continue;
                shown.Add((name, list));
                rows += Mathf.CeilToInt(list.Count / Cols);
            }

            float panelH = _statsPanel.rect.height, panelW = _statsPanel.rect.width;
            float avail = panelH - GridTop - NoteH - 12f;
            float tileW = (panelW - SPad * 2f - TileGap * (Cols - 1f)) / Cols;
            // Group labels give way before the tiles shrink past reading.
            bool labels = true;
            float tileH = rows > 0 ? (avail - shown.Count * GroupH) / rows - TileGap : 60f;
            if (tileH < 46f)
            {
                labels = false;
                tileH = rows > 0 ? (avail - shown.Count * 8f) / rows - TileGap : 60f;
            }
            tileH = Mathf.Clamp(tileH, 40f, 62f);

            float y = GridTop;
            _noteDefault = primary;
            foreach (var (group, stats) in shown)
            {
                if (labels)
                {
                    UiKit.Label(Top(y + 2f, y + GroupH), group, 11, StatFaint, TextAnchor.LowerLeft);
                    y += GroupH;
                }
                else y += 8f;

                for (int i = 0; i < stats.Count; i++)
                {
                    var (k, total, mine) = stats[i];
                    _noteDefault ??= k;
                    int col = i % (int)Cols;
                    if (col == 0 && i > 0) y += tileH + TileGap;
                    float x = SPad + col * (tileW + TileGap);
                    StatTileAt(k, total, mine, piece, x, y, tileW, tileH);
                }
                y += tileH + TileGap;
            }
            if (shown.Count == 0)
                UiKit.Label(Top(GridTop, GridTop + 40f), "nothing from gear or the board yet", 15, StatDim, TextAnchor.MiddleLeft);

            // ---- the footnote
            var foot = UiKit.Rect(_statsBody, "foot", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(SPad, 16f), new Vector2(-SPad, NoteH));
            var rule = UiKit.Panel(foot, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), Vector2.zero,
                new Color(0.15f, 0.16f, 0.22f, 1f));
            rule.GetComponent<Image>().raycastTarget = false;
            _noteTitle = UiKit.Label(UiKit.Rect(foot, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -32f), new Vector2(0, -10f)), "", 14, StatGold, TextAnchor.MiddleLeft);
            _noteText = UiKit.Label(UiKit.Rect(foot, "m", Vector2.zero, Vector2.one,
                new Vector2(0, 0), new Vector2(0, -36f)), "", 14, StatDim, TextAnchor.UpperLeft);
            _noteText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _noteText.lineSpacing = 1.1f;
            _noteShown = _noteStat ?? _noteDefault;
            ShowNote(_noteShown);
        }

        /// <summary>A rect hung from the panel's top, <paramref name="top"/> to <paramref name="bottom"/>
        /// units down, between the side pads.</summary>
        RectTransform Top(float top, float bottom)
            => UiKit.Rect(_statsBody, "r", new Vector2(0, 1), new Vector2(1, 1),
                          new Vector2(SPad, -bottom), new Vector2(-SPad, -top));

        void StatTileAt(StatKind k, float total, float mine, bool piece, float x, float y, float w, float h)
        {
            bool hit = Mathf.Abs(mine) > 0.01f;
            var tile = UiKit.Panel(_statsBody, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(x, -y - h), new Vector2(x + w, -y), hit ? StatHit : StatWell);
            UiKit.Panel(tile, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(3, 0),
                hit ? StatGold : new Color(0.15f, 0.16f, 0.22f, 1f)).GetComponent<Image>().raycastTarget = false;

            var inner = UiKit.Rect(tile, "in", Vector2.zero, Vector2.one, new Vector2(11, 4), new Vector2(-8, -5));
            var label = UiKit.Label(inner, GearForge.Label(k), 12, StatDim, TextAnchor.UpperLeft);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Label(inner, Signed(total), 20, StatInk, TextAnchor.LowerLeft);
            if (hit) UiKit.Label(inner, Signed(mine), 13, StatGold, TextAnchor.LowerRight);

            // A piece's read fades what it does not touch, so what it does stands out.
            if (piece && !hit) tile.gameObject.AddComponent<CanvasGroup>().alpha = 0.3f;

            var outline = tile.gameObject.AddComponent<Outline>();
            outline.effectColor = StatGold;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.enabled = false;
            _statTiles.Add(new StatTile { Rect = tile, Kind = k, Outline = outline });
        }

        static string Signed(float v) => $"{v:+0.#;-0.#;0}%";

        void ShowNote(StatKind? k)
        {
            foreach (var t in _statTiles) t.Outline.enabled = k != null && t.Kind == k.Value;
            if (_noteTitle == null) return;
            if (k == null) { _noteTitle.text = ""; _noteText.text = ""; return; }
            _noteTitle.text = GearForge.Label(k.Value).ToUpperInvariant();
            _noteText.text = Glossary.Plain(Glossary.Stat(k.Value));
        }

        StatKind? _noteShown;

        /// <summary>Per frame: the tile under the pad or pointer, else the tapped one, else the default.</summary>
        void UpdateFootnote()
        {
            if (_statTiles.Count == 0) return;
            StatKind? want = null;
            var focused = Controls.Focused;
            var at = Controls.PointerPosition;
            foreach (var t in _statTiles)
            {
                if (t.Rect == focused ||
                    (focused == null && RectTransformUtility.RectangleContainsScreenPoint(t.Rect, at, null)))
                { want = t.Kind; break; }
            }
            want ??= _noteStat ?? _noteDefault;
            if (want == _noteShown) return;
            _noteShown = want;
            ShowNote(want);
        }

        /// <summary>A tap on a tile pins the footnote to it. True when the tap was a tile's.</summary>
        bool TapStats(Vector2 p)
        {
            foreach (var t in _statTiles)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(t.Rect, p, null)) continue;
                _noteStat = t.Kind;
                _noteShown = null;
                return true;
            }
            return false;
        }
    }
}
