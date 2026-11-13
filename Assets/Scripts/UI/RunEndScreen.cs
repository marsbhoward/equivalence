using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// THE RUN'S END (the user's approved mock, 2026-11-12): how it ended, the element's mastery XP
    /// filling from where the run began, what came home and what was lost, the stake and any hub
    /// theme, then the small stats - and one RETURN TO HUB button (tap, Enter/E, A on a pad).
    /// Replaces the old text panel's "[ R ] return to the hub", which a phone could not press.
    ///
    /// Every figure is read off the run as it settled (GameBootstrap.SettleRunAsync); this screen
    /// decides nothing.
    /// </summary>
    public class RunEndScreen : MonoBehaviour
    {
        public enum Outcome { Escaped, Fallen, Abandoned }

        /// <summary>The tiers whose boxes a run can bring home, in the Forge's order.</summary>
        public static readonly LootTier[] BoxTiers =
            { LootTier.Bronze, LootTier.Silver, LootTier.Gold, LootTier.Diamond, LootTier.BlackDiamond };

        public class Report
        {
            public Outcome How;
            public int Floor, Kills;
            public float Seconds;
            public bool NewBest;
            public ElementType Element;
            public WeaponClass Weapon;

            /// <summary>Mastery XP this run (all of it banked, whatever the outcome), the part from
            /// XP reward cards, and the element's level and XP before it was banked.</summary>
            public int Gained, FromCards, StartLevel, StartXp;

            public readonly List<MintedGearRecord> Home = new(), Lost = new();
            public readonly List<(LootTier Tier, int Count)> HomeBoxes = new(), LostBoxes = new();
            public int HomeRiftBoxes, LostRiftBoxes, HomeCalx, LostCalx;
            public string Stake;
            public bool StakeLost;
            public readonly List<string> Themes = new();
        }

        public static bool IsOpen { get; private set; }

        GameObject _root;
        Action _done;
        int _openedFrame;
        float _openedAt;
        RectTransform _button;
        readonly List<RectTransform> _focus = new();

        Report _r;
        Image _fill;
        Text _caption;
        readonly List<Image> _pips = new();
        int _shownLevel = -1;

        // The Forge's tokens (ForgeScreen), so the two screens read as one game.
        static readonly Color Bg = Hex(0x08080b), Rule = Hex(0x221b15), CardBg = Hex(0x14100c), RowBg = Hex(0x1c1510);
        static readonly Color ChipBg = Hex(0x17130f), ChipEdge = Hex(0x3a332c), LostBg = Hex(0x120e0b), QuietBg = Hex(0x211c1a);
        static readonly Color Primary = Hex(0xc9a46a), PrimaryInk = Hex(0x1a120a);
        static readonly Color Ink = Hex(0xf5debd), OnInk = Hex(0xf7e0b3), SubInk = Hex(0xc9b597), Dim = Hex(0xa3a8b2), Off = Hex(0x7d7770);
        static readonly Color Good = Hex(0x8ce699), GoodBg = Hex(0x1f3a24), Bad = Hex(0xf2a08f), Powered = Hex(0xf7cc66), Rolled = Hex(0xc7adf5);
        static readonly Color Rift = Hex(0x7fc4ff), StarOn = Hex(0xffd14d), StarOff = Hex(0x3a332c);

        const float Pad = 44f, HeadH = 96f, FootH = 92f, LeftW = 960f, Gap = 40f;
        const int TitleSize = 64, HeadSize = 30, BodySize = 28, SmallSize = 25;

        static Color Hex(uint rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
        static string Html(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
        static string Tint(string text, Color c) => $"<color={Html(c)}>{text}</color>";

        static Color ElementInk(ElementType e) => e switch
        {
            ElementType.Fire => Hex(0xf2a061),
            ElementType.Water => Hex(0x7fb8ec),
            ElementType.Earth => Hex(0xc9b27a),
            _ => Hex(0xa8e0cc),
        };

        public void Show(Transform canvas, Report report, Action onDone)
        {
            Close();
            _r = report ?? new Report();
            _done = onDone;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _openedAt = Time.unscaledTime;

            _root = new GameObject("RunEnd", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;
            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Bg);
            var frame = UiKit.Rect(full, "frame", Vector2.zero, Vector2.one, new Vector2(Pad, 34f), new Vector2(-Pad, -30f));

            BuildHeader(frame);
            BuildFooter(frame);
            var mid = UiKit.Rect(frame, "mid", Vector2.zero, Vector2.one, new Vector2(0f, FootH + 22f), new Vector2(0f, -(HeadH + 22f)));
            BuildMastery(UiKit.Rect(mid, "mastery", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(LeftW, 0f)));
            BuildLoot(UiKit.Rect(mid, "loot", Vector2.zero, Vector2.one, new Vector2(LeftW + Gap, 0f), Vector2.zero));
            Animate(0f);
        }

        // ------------------------------------------------------------------ header and footer

        void BuildHeader(RectTransform frame)
        {
            var head = UiKit.Rect(frame, "head", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -HeadH), Vector2.zero);
            UiKit.Panel(head, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f), Rule);

            var (title, ink, sub) = _r.How switch
            {
                Outcome.Escaped => ("ESCAPED", Rift, $"Through the Blue Rift on floor {_r.Floor} of {RunFormat.FinalFloor}"),
                Outcome.Fallen => ("FALLEN", Bad, $"On floor {_r.Floor} of {RunFormat.FinalFloor}"),
                _ => ("ABANDONED", Bad, $"Left on floor {_r.Floor} of {RunFormat.FinalFloor}"),
            };
            var t = UiKit.Label(UiKit.Rect(head, "t", Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 14f), new Vector2(700f, 0f)),
                title, TitleSize, ink, TextAnchor.LowerLeft);
            float x = t.preferredWidth + 30f;
            UiKit.Label(UiKit.Rect(head, "s", Vector2.zero, Vector2.one, new Vector2(x, 20f), new Vector2(-320f, 0f)),
                sub, BodySize, SubInk, TextAnchor.LowerLeft);

            if (_r.NewBest)
            {
                var chip = UiKit.Panel(head, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-300f, 18f), new Vector2(0f, 68f), Hex(0x2a2010));
                UiKit.Label(chip, "NEW BEST FLOOR", 24, Powered, TextAnchor.MiddleCenter);
            }
        }

        void BuildFooter(RectTransform frame)
        {
            var foot = UiKit.Rect(frame, "foot", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, FootH));
            UiKit.Panel(foot, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 20f), new Vector2(0f, 22f), Rule);

            int s = Mathf.FloorToInt(_r.Seconds);
            string time = s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
            string weapon = _r.Weapon == WeaponClass.Disc ? "Discs" : _r.Weapon.ToString();
            var stats = UiKit.Label(UiKit.Rect(foot, "stats", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-460f, 0f)),
                $"Kills  {Tint(_r.Kills.ToString(), Ink)}        Time  {Tint(time, Ink)}        {_r.Element}  {Tint(weapon, Ink)}",
                BodySize, Dim, TextAnchor.MiddleLeft);
            stats.supportRichText = true;

            _button = UiKit.Panel(foot, new Vector2(1f, 0f), Vector2.one, new Vector2(-420f, 0f), Vector2.zero, Primary);
            UiKit.Label(_button, "RETURN TO HUB", 34, PrimaryInk, TextAnchor.MiddleCenter);
        }

        // ------------------------------------------------------------------ mastery

        void BuildMastery(RectTransform col)
        {
            // The card is as tall as what it holds - a full-height card left a hole above the callout.
            var card = UiKit.Panel(col, new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero, CardBg);
            var c = UiKit.Rect(col, "in", Vector2.zero, Vector2.one, new Vector2(34f, 28f), new Vector2(-34f, -26f));
            Color el = ElementInk(_r.Element);

            float y = 0f;
            Row(c, ref y, 64f, out var top);
            UiKit.Label(top, $"{_r.Element.ToString().ToUpper()} MASTERY", HeadSize, el, TextAnchor.MiddleLeft);
            UiKit.Label(top, $"+{_r.Gained:N0} XP", 52, _r.Gained > 0 ? Good : Off, TextAnchor.MiddleRight);
            y -= 16f;

            int endLevel = LevelAt(_r.Gained).Level, gained = endLevel - _r.StartLevel;
            if (gained > 0)
            {
                Row(c, ref y, 30f, out var pips);
                UiKit.Label(UiKit.Rect(pips, "a", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(90f, 0f)),
                    $"LV {_r.StartLevel}", 24, Off, TextAnchor.MiddleLeft);
                UiKit.Label(UiKit.Rect(pips, "b", new Vector2(1f, 0f), Vector2.one, new Vector2(-90f, 0f), Vector2.zero),
                    $"LV {endLevel}", 24, OnInk, TextAnchor.MiddleRight);
                var row = UiKit.Rect(pips, "pips", Vector2.zero, Vector2.one, new Vector2(104f, 6f), new Vector2(-104f, -6f));
                int shown = Mathf.Min(gained, 12);
                for (int i = 0; i < shown; i++)
                {
                    float a = i / (float)shown, b = (i + 1) / (float)shown;
                    var pip = UiKit.Panel(row, new Vector2(a, 0f), new Vector2(b, 1f),
                        new Vector2(i == 0 ? 0f : 5f, 0f), new Vector2(i == shown - 1 ? 0f : -5f, 0f), QuietBg);
                    _pips.Add(pip.GetComponent<Image>());
                }
                y -= 18f;
            }
            else
            {
                Row(c, ref y, 30f, out var lv);
                UiKit.Label(lv, $"LV {_r.StartLevel}", 24, OnInk, TextAnchor.MiddleLeft);
                y -= 18f;
            }

            Row(c, ref y, 40f, out var bar);
            _fill = UiKit.Bar(bar, el, QuietBg);
            y -= 10f;
            Row(c, ref y, 34f, out var cap);
            _caption = UiKit.Label(cap, "", SmallSize, Dim, TextAnchor.MiddleLeft);
            if (gained > 0)
                UiKit.Label(cap, gained == 1 ? "1 level gained" : $"{gained} levels gained", SmallSize, Dim, TextAnchor.MiddleRight);
            y -= 26f;

            int fromFloors = _r.Gained - _r.FromCards;
            if (fromFloors > 0) Line(c, ref y, "Floors cleared", fromFloors);
            if (_r.FromCards > 0) Line(c, ref y, "XP reward cards", _r.FromCards);

            // The callout: what the XP buys, and on a fall the promise that it was kept.
            string kept = _r.How == Outcome.Escaped ? "" : " XP is never lost.";
            y -= 18f;
            var call = UiKit.Panel(c, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, y - 76f), new Vector2(0f, y), gained > 0 ? GoodBg : QuietBg);
            card.offsetMin = new Vector2(0f, y - 76f - 26f - 28f);
            if (gained > 0)
            {
                UiKit.Label(UiKit.Rect(call, "n", Vector2.zero, new Vector2(0f, 1f), new Vector2(20f, 0f), new Vector2(100f, 0f)),
                    $"+{gained}", 36, Good, TextAnchor.MiddleLeft);
                UiKit.Label(UiKit.Rect(call, "t", Vector2.zero, Vector2.one, new Vector2(100f, 0f), new Vector2(-16f, 0f)),
                    $"{(gained == 1 ? "point" : "points")} to spend on the {_r.Element} mastery board.{kept}", SmallSize, Hex(0xe6f5e8), TextAnchor.MiddleLeft);
            }
            else
            {
                var (_, xp, need) = LevelAt(_r.Gained);
                UiKit.Label(UiKit.Rect(call, "t", Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-16f, 0f)),
                    $"{need - xp:N0} XP to the next board point.{kept}", SmallSize, SubInk, TextAnchor.MiddleLeft);
            }
        }

        void Line(RectTransform c, ref float y, string label, int amount)
        {
            Row(c, ref y, 38f, out var row);
            UiKit.Label(row, label, BodySize, SubInk, TextAnchor.MiddleLeft);
            UiKit.Label(row, $"+{amount:N0}", BodySize, Ink, TextAnchor.MiddleRight);
            y -= 6f;
        }

        /// <summary>The element's level and XP once <paramref name="shown"/> of the run's XP is in
        /// (ElementMastery.Grant's own fold, without writing anything).</summary>
        (int Level, int Xp, int Need) LevelAt(int shown)
        {
            int level = _r.StartLevel, xp = _r.StartXp + shown;
            while (xp >= ElementMastery.XpForNext(level)) { xp -= ElementMastery.XpForNext(level); level++; }
            return (level, xp, ElementMastery.XpForNext(level));
        }

        /// <summary>The bar fills from where the run began: a pip lights as each level passes.</summary>
        void Animate(float t)
        {
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            var (level, xp, need) = LevelAt(Mathf.RoundToInt(_r.Gained * e));
            UiKit.SetFill(_fill, xp / (float)need);
            if (level == _shownLevel) { UiKit.SetText(_caption, $"{xp:N0} / {need:N0} to level {level + 1}"); return; }
            _shownLevel = level;
            UiKit.SetText(_caption, $"{xp:N0} / {need:N0} to level {level + 1}");
            Color el = ElementInk(_r.Element);
            int lit = level - _r.StartLevel, gained = LevelAt(_r.Gained).Level - _r.StartLevel;
            for (int i = 0; i < _pips.Count; i++)
            {
                // Over 12 levels the pips stand for an even share each.
                int at = Mathf.CeilToInt((i + 1) * gained / (float)_pips.Count);
                UiKit.SetColor(_pips[i], lit >= at ? el : QuietBg);
            }
        }

        // ------------------------------------------------------------------ loot

        void BuildLoot(RectTransform col)
        {
            float y = 0f;
            bool escaped = _r.How == Outcome.Escaped;

            Head(col, ref y, "CAME HOME", Good);
            if (!escaped && (_r.Home.Count > 0 || _r.HomeBoxes.Count > 0 || _r.HomeRiftBoxes > 0))
            {
                Row(col, ref y, 32f, out var sub);
                UiKit.Label(sub, "Secured at Rifts before the end", SmallSize, Dim, TextAnchor.MiddleLeft);
                y -= 8f;
            }
            var homeChips = new List<(string Text, Color Ink)>();
            foreach (var (tier, n) in _r.HomeBoxes) homeChips.Add(($"{Tint($"{ForgeScreen.Label(tier)} box", ForgeScreen.TierColor(tier))} x{n}", Ink));
            if (_r.HomeRiftBoxes > 0) homeChips.Add(($"{Tint("Rift Box", Hex(0xe06b5a))} x{_r.HomeRiftBoxes}", Ink));
            if (_r.HomeCalx > 0) homeChips.Add(($"{Tint("Calx", SubInk)} {_r.HomeCalx}", Ink));
            if (_r.Home.Count == 0 && homeChips.Count == 0)
            {
                Row(col, ref y, 40f, out var none);
                UiKit.Label(none, "Nothing this time.", BodySize, Off, TextAnchor.MiddleLeft);
                y -= 10f;
            }
            Pieces(col, ref y, _r.Home, _r.Lost.Count > 0 ? 4 : 6, lost: false);
            Chips(col, ref y, homeChips, lost: false);

            y -= 14f;
            var lostChips = new List<(string Text, Color Ink)>();
            foreach (var (tier, n) in _r.LostBoxes) lostChips.Add(($"{ForgeScreen.Label(tier)} box x{n}", Off));
            if (_r.LostRiftBoxes > 0) lostChips.Add(($"Rift Box x{_r.LostRiftBoxes}", Off));
            if (_r.LostCalx > 0) lostChips.Add(($"Calx {_r.LostCalx}", Off));
            bool anyLost = _r.Lost.Count > 0 || lostChips.Count > 0;
            Head(col, ref y, "LOST", anyLost ? Bad : Off);
            if (!anyLost)
            {
                Row(col, ref y, 40f, out var none);
                UiKit.Label(none, escaped ? "Nothing - everything you carried came home." : "Nothing - you carried nothing at risk.",
                    BodySize, Off, TextAnchor.MiddleLeft);
                y -= 10f;
            }
            Pieces(col, ref y, _r.Lost, 4, lost: true);
            Chips(col, ref y, lostChips, lost: true);

            if (_r.Stake == null && _r.Themes.Count == 0) return;
            y -= 6f;
            UiKit.Panel(col, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, y - 2f), new Vector2(0f, y), Rule);
            y -= 14f;
            if (_r.Stake != null)
            {
                Row(col, ref y, 38f, out var stake);
                var l = UiKit.Label(stake, $"{Tint("Stake", Powered)}   {(_r.StakeLost ? Tint(_r.Stake, Bad) : _r.Stake)}", BodySize, SubInk, TextAnchor.MiddleLeft);
                l.supportRichText = true;
            }
            if (_r.Themes.Count > 0)
            {
                Row(col, ref y, 38f, out var theme);
                var l = UiKit.Label(theme, $"{Tint("Hub theme", Rolled)}   {string.Join(", ", _r.Themes)} - a parcel waits on the crate", BodySize, SubInk, TextAnchor.MiddleLeft);
                l.supportRichText = true;
            }
        }

        void Head(RectTransform col, ref float y, string text, Color ink)
        {
            Row(col, ref y, 38f, out var row);
            UiKit.Label(row, text, 26, ink, TextAnchor.MiddleLeft);
            y -= 10f;
        }

        /// <summary>Pieces two to a row, at most <paramref name="cap"/> - the last cell counts the rest.</summary>
        void Pieces(RectTransform col, ref float y, List<MintedGearRecord> list, int cap, bool lost)
        {
            if (list.Count == 0) return;
            const float h = 60f, gap = 10f;
            int cells = Mathf.Min(list.Count, cap);
            for (int i = 0; i < cells; i++)
            {
                int c = i % 2, r = i / 2;
                float top = y - r * (h + gap);
                var cell = UiKit.Panel(col, new Vector2(c * 0.5f, 1f), new Vector2(c * 0.5f + 0.5f, 1f),
                    new Vector2(c == 0 ? 0f : 5f, top - h), new Vector2(c == 0 ? -5f : 0f, top), lost ? LostBg : RowBg);
                if (i == cap - 1 && list.Count > cap)
                {
                    UiKit.Label(cell, $"+{list.Count - cap + 1} more", BodySize, lost ? Off : SubInk, TextAnchor.MiddleCenter);
                    continue;
                }
                var p = list[i];
                UiKit.Panel(cell, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, -8f), new Vector2(32f, 8f),
                    lost ? Off : ForgeScreen.TierColor(p.Tier));
                bool stars = p.Tier is LootTier.Bronze or LootTier.Silver or LootTier.Gold;
                // One line, shrunk to fit: the rect is one line tall, so best fit cannot wrap it.
                var name = UiKit.Label(UiKit.Rect(cell, "n", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(46f, -17f), new Vector2(stars ? -106f : -12f, 17f)),
                    ForgeScreen.ItemName(p), SmallSize, lost ? Off : Ink, TextAnchor.MiddleLeft);
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 18;
                name.resizeTextMaxSize = SmallSize;
                if (stars)
                    UiKit.Stars(UiKit.Rect(cell, "stars", new Vector2(1f, 0f), Vector2.one, new Vector2(-98f, 0f), new Vector2(-12f, 0f)),
                        p.UpgradeLevel, GearRoller.MaxLevel, 24f, lost ? Off : StarOn, StarOff);
                if (lost)   // struck through
                    UiKit.Panel(cell, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(12f, -1f), new Vector2(-12f, 1f), Off);
            }
            int rows = (cells + 1) / 2;
            y -= rows * (h + gap) + 2f;
        }

        /// <summary>Boxes and calx as chips, wrapping onto a new line when the column runs out.</summary>
        void Chips(RectTransform col, ref float y, List<(string Text, Color Ink)> chips, bool lost)
        {
            if (chips.Count == 0) return;
            const float h = 50f, gap = 10f;
            float width = col.rect.width > 0f ? col.rect.width : 800f;
            float x = 0f;
            foreach (var (text, ink) in chips)
            {
                var probe = UiKit.Label(col, text, SmallSize, ink);
                probe.supportRichText = true;
                float w = probe.preferredWidth + 36f;
                Destroy(probe.gameObject);
                if (x > 0f && x + w > width) { x = 0f; y -= h + gap; }
                var chip = UiKit.Panel(col, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y - h), new Vector2(x + w, y), lost ? LostBg : ChipBg);
                var edge = UiKit.Panel(chip, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, lost ? Hex(0x2a221c) : ChipEdge);
                edge.SetAsFirstSibling();
                UiKit.Panel(chip, Vector2.zero, Vector2.one, new Vector2(1f, 1f), new Vector2(-1f, -1f), lost ? LostBg : ChipBg);
                var l = UiKit.Label(chip, text, SmallSize, ink, TextAnchor.MiddleCenter);
                l.supportRichText = true;
                if (lost)
                    UiKit.Panel(chip, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(10f, -1f), new Vector2(-10f, 1f), Off);
                x += w + gap;
            }
            y -= h + 12f;
        }

        static void Row(RectTransform parent, ref float y, float h, out RectTransform row)
        {
            row = UiKit.Rect(parent, "row", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, y - h), new Vector2(0f, y));
            y -= h;
        }

        // ------------------------------------------------------------------ input

        void Finish()
        {
            var done = _done;
            _done = null;
            Close();
            done?.Invoke();
        }

        public void Close()
        {
            if (_root) Destroy(_root);
            _root = null;
            _button = null;
            _fill = null;
            _caption = null;
            _pips.Clear();
            _shownLevel = -1;
            IsOpen = false;
        }

        void Update()
        {
            if (!IsOpen) return;
            float since = Time.unscaledTime - _openedAt;
            int levels = _r != null ? LevelAt(_r.Gained).Level - _r.StartLevel : 0;
            Animate((since - 0.3f) / Mathf.Min(2.4f, 0.9f + 0.35f * levels));

            _focus.Clear();
            if (_button) _focus.Add(_button);
            Controls.SetFocusCandidates(_focus);
            // Not on the frame it opens (the press that ended the run), nor in the moment after:
            // a swing or a tap still landing as the run ends must not skip the screen.
            if (Time.frameCount == _openedFrame || since < 0.35f) return;

            if (Controls.Tapped(out var at) && _button
                && RectTransformUtility.RectangleContainsScreenPoint(_button, at, null)) { Finish(); return; }
            if (Controls.ConfirmTapped || Controls.InteractTapped) Finish();
        }

        void OnDisable() => Close();
    }
}
