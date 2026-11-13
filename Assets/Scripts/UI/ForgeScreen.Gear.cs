using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Convergence.Core;
using Convergence.Art.Gear;
using Convergence.Chain;

namespace Convergence.UI
{
    /// <summary>
    /// The Forge's tabs that work on pieces: COMBINE, RE-ROLL, SALVAGE and FUSE. Each is a list of
    /// pieces on the left and the chosen one on the right; the first in the list is chosen until the
    /// player picks another, so the detail is never empty while there is something to show.
    /// </summary>
    public partial class ForgeScreen
    {
        // Combine.
        string _groupKey;
        MintedGearRecord _pickA, _pickB, _result;
        int _resultNewFrom;
        // Re-roll.
        enum RerollFilter { All, Worn, Weapons, Armour }
        RerollFilter _rerollFilter;
        string _rerollId, _rerollNote;
        // Salvage. Not readonly, and rebuilt if null - see the domain reload rules.
        HashSet<string> _salvagePick = new();
        bool _salvageConfirm;
        string _salvageNote;
        // Fuse.
        string _fusionId;
        MintedGearRecord _fused;

        void ResetGear()
        {
            _groupKey = null; _pickA = null; _pickB = null; _result = null;
            _rerollFilter = RerollFilter.All; _rerollId = null; _rerollNote = null;
            (_salvagePick ??= new HashSet<string>()).Clear();
            _salvageConfirm = false; _salvageNote = null;
            _fusionId = null; _fused = null;
        }

        List<MintedGearRecord> Gear => _ctx.MintedGear ?? new List<MintedGearRecord>();
        bool Worn(MintedGearRecord r) => _ctx.IsEquipped != null && _ctx.IsEquipped(r.InstanceId);

        // ------------------------------------------------------------------ Combine

        /// <summary>
        /// Groups of matching pieces on the left. The chosen group's first two are combined unless
        /// the player picks others - a group of three or more opens under its row so they can.
        /// </summary>
        void BuildCombine()
        {
            var groups = GearForge.Groups(_ctx.MintedGear, _ctx.IsEquipped);
            Columns(ListW, 44f);
            ListNote("Matching pairs - equipped pieces don't count");

            var group = groups.Find(g => GearForge.MatchKey(g[0]) == _groupKey) ?? (_result == null ? groups.FirstOrDefault() : null);
            if (group != null)
            {
                _groupKey = GearForge.MatchKey(group[0]);
                if (_pickA == null || !group.Contains(_pickA)) _pickA = group[0];
                if (_pickB == null || !group.Contains(_pickB) || _pickB == _pickA) _pickB = group.First(r => r != _pickA);
            }

            foreach (var g in groups)
            {
                var r = g[0];
                bool on = g == group;
                var row = PieceRow(r, on, PrimaryText(r, r.Tier, r.UpgradeLevel), $"×{g.Count}");
                var key = GearForge.MatchKey(r);
                Act(row, () => { _groupKey = key; _pickA = _pickB = null; _result = null; Rebuild(); });
                if (!on || g.Count <= 2) continue;

                // Three or more alike: the pieces differ in their sub-stats, so the player picks two.
                foreach (var piece in g)
                {
                    bool picked = piece == _pickA || piece == _pickB;
                    var sub = Framed(_listContent, new Vector2(0f, 1f), Vector2.one, new Vector2(48f, -_listH - 84f), new Vector2(0f, -_listH),
                        picked, picked ? OnBg : CardBg);
                    _listH += 84f + RowGap;
                    string subs = SubText(piece);
                    Txt(UiKit.Rect(sub, "n", Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f)),
                        string.IsNullOrEmpty(subs) ? "no sub-stats" : subs, SmallSize, picked ? Ink : SubInk, TextAnchor.MiddleLeft, wrap: true);
                    var chosen = piece;
                    Act(sub, () =>
                    {
                        if (chosen == _pickA || chosen == _pickB) return;
                        _pickA = _pickB;
                        _pickB = chosen;
                        Rebuild();
                    });
                }
            }

            if (_result != null) { BuildCombineResult(); return; }
            if (group == null)
            {
                Para("No two matching pieces yet.", HeadSize, Ink, 56f);
                Para("Two pieces of the same slot, tier, stars and primary stat (and class or ability) become one - " +
                     "no boxes needed. Equipped pieces don't count: take one off first.", BodySize, Dim, 200f);
                if (RunFormat.Showcase) Para("In the showcase, gear stops at Bronze 3 stars.", SmallSize, Dim, 40f);
                return;
            }
            BuildCombinePreview(GearForge.Plan(_pickA, _pickB));
        }

        /// <summary>
        /// What the combine WILL do, before it does it. Carried-over and powered-up rows show real
        /// numbers (they are certain); randomized rows show "???" and nothing else, because they
        /// have not been rolled yet and each stat has its own range.
        /// </summary>
        void BuildCombinePreview(GearForge.CombinePlan p)
        {
            string into = $"{Label(p.ToTier)} {(p.A.Slot == GearSlot.Weapon ? p.A.Class.ToString() : p.A.Slot.Label())}";
            Txt(Stack(52f, 6f), $"{Tint(ItemName(p.A) + " ×2", TierColor(p.FromTier))}  {Tint("becomes", Dim)}  {Tint(into, TierColor(p.ToTier))}",
                HeadSize, Ink, TextAnchor.MiddleLeft, wrap: true);
            var stars = Stack(34f, 20f);
            StarsAt(stars, 0f, p.FromLevel);
            Txt(UiKit.Rect(stars, "to", Vector2.zero, new Vector2(0f, 1f), new Vector2(100f, 0f), new Vector2(150f, 0f)), "→", BodySize, Dim, TextAnchor.MiddleCenter);
            StarsAt(stars, 150f, p.ToLevel);

            var cards = Rest();
            RectTransform Card(int i, string title, Color ink)
            {
                var c = UiKit.Panel(cards, new Vector2(i / 3f, 0f), new Vector2((i + 1) / 3f, 1f),
                    new Vector2(i == 0 ? 0f : 9f, 0f), new Vector2(i == 2 ? 0f : -9f, 0f), CardBg);
                Txt(UiKit.Rect(c, "t", new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -60f), new Vector2(-20f, -16f)),
                    title, SmallSize, ink, TextAnchor.MiddleLeft);
                return c;
            }
            // Lines down a card: a stat's name, then its numbers.
            void Lines(RectTransform card, IEnumerable<(string Name, string Value)> lines, Color valueInk, string foot)
            {
                float y = -70f;
                foreach (var (name, value) in lines)
                {
                    Txt(UiKit.Rect(card, "k", new Vector2(0f, 1f), Vector2.one, new Vector2(20f, y - 36f), new Vector2(-20f, y)), name, SmallSize, Ink);
                    Txt(UiKit.Rect(card, "v", new Vector2(0f, 1f), Vector2.one, new Vector2(20f, y - 74f), new Vector2(-20f, y - 38f)), value, BodySize, valueInk);
                    y -= 88f;
                }
                if (foot != null)
                    Txt(UiKit.Rect(card, "f", Vector2.zero, new Vector2(1f, 0f), new Vector2(20f, 16f), new Vector2(-20f, 110f)),
                        foot, SmallSize, Dim, TextAnchor.LowerLeft, wrap: true);
            }

            float before = Scaled(GearRoller.PrimaryPoints(p.A.PrimaryStat, p.FromTier, p.A.Slot), p.FromLevel);
            float after = Scaled(GearRoller.PrimaryPoints(p.A.PrimaryStat, p.ToTier, p.A.Slot), p.ToLevel);
            var powered = new List<(string, string)> { (GearForge.Label(p.A.PrimaryStat), $"{Pct(before)} → {Pct(after)}") };
            string poweredFoot;
            if (p.Promotes)
            {
                powered.Add(("Sub-stat slots", $"{GearRoller.SubStatCount(p.FromTier)} → {GearRoller.SubStatCount(p.ToTier)}"));
                poweredFoot = $"Takes {Plural(PromoteCost, "Rift Box")} - you have {RiftHeld}";
            }
            else poweredFoot = $"every stat ×{GearRoller.UpgradeScale(p.ToLevel) / GearRoller.UpgradeScale(p.FromLevel):0.00}";
            var pc = Card(0, "POWERED UP", Powered);
            Lines(pc, powered, Powered, poweredFoot);
            if (p.Promotes && !CanPromote)
                pc.GetComponentsInChildren<UnityEngine.UI.Text>().Last().color = Bad;

            var carried = p.Carried.Select(c =>
            {
                if (!p.Promotes) return (GearForge.Label(c.Kind), $"{Pct(Scaled(c.Value, p.FromLevel))} → {Pct(Scaled(c.Value, p.ToLevel))}");
                // The stat is certain; its value re-rolls in the new tier's range, which is known.
                var (min, max) = GearRoller.SubStatRange(c.Kind, p.ToTier);
                return (GearForge.Label(c.Kind), $"{Scaled(min, p.ToLevel):0.#} – {Scaled(max, p.ToLevel):0.#}%");
            }).ToList();
            Lines(Card(1, "CARRIED OVER", Good), carried, Good,
                carried.Count == 0 ? "nothing matched" : p.Promotes ? "a new roll in the new tier's range" : "the higher roll is kept");

            var random = Enumerable.Range(0, p.Randomized).Select(_ => ("???", "( ? – ? )")).ToList();
            Lines(Card(2, "RANDOMIZED", Rolled), random, Dim,
                p.Randomized == 0 ? "nothing - every sub-stat matched" : "rolled when you combine");

            bool can = !p.Promotes || CanPromote;
            Footer(RunFormat.Showcase ? "In the showcase, gear stops at Bronze 3 stars" : null, Dim,
                (can ? "COMBINE" : "NEEDS A RIFT BOX", true, can, () =>
                {
                    var result = _ctx.OnCombine?.Invoke(p.A.InstanceId, p.B.InstanceId);
                    _pickA = _pickB = null;
                    _groupKey = null;
                    if (result != null) { _result = result; _resultNewFrom = p.Carried.Count; }
                    Rebuild();
                }));
        }

        void BuildCombineResult()
        {
            var r = _result;
            Txt(Stack(30f, 4f), "COMBINED", SmallSize, Good);
            Txt(Stack(52f, 6f), ItemName(r), HeadSize, TierColor(r.Tier));
            StarsAt(Stack(34f, 20f), 0f, r.UpgradeLevel);
            Txt(Stack(44f, 12f), PrimaryText(r, r.Tier, r.UpgradeLevel), BodySize, Ink);
            for (int i = 0; i < r.SubStats.Count; i++)
            {
                var sub = r.SubStats[i];
                bool fresh = i >= _resultNewFrom;
                var line = Stack(44f, 6f);
                Txt(line, $"{GearForge.Label(sub.Kind)}  {Pct(Scaled(sub.Value, r.UpgradeLevel))}", BodySize, Ink);
                Txt(line, fresh ? "NEW" : "CARRIED", SmallSize, fresh ? Rolled : Good, TextAnchor.MiddleRight);
            }
            Footer(null, Dim, ("DONE", true, true, () => { _result = null; Rebuild(); }));
        }

        // ------------------------------------------------------------------ Re-roll

        static readonly (RerollFilter Filter, string Label)[] RerollFilters =
        {
            (RerollFilter.All, "ALL"), (RerollFilter.Worn, "WORN"), (RerollFilter.Weapons, "WEAPONS"), (RerollFilter.Armour, "ARMOUR"),
        };

        bool Passes(MintedGearRecord r) => _rerollFilter switch
        {
            RerollFilter.Worn => Worn(r),
            RerollFilter.Weapons => r.Slot == GearSlot.Weapon,
            RerollFilter.Armour => r.Slot != GearSlot.Weapon,
            _ => true,
        };

        void BuildReroll()
        {
            Columns(ListW, 64f);
            float x = 0f;
            foreach (var (filter, label) in RerollFilters)
            {
                float w = label.Length * 19f + 40f;
                bool on = _rerollFilter == filter;
                var chip = UiKit.Panel(_listHead, Vector2.zero, new Vector2(0f, 1f), new Vector2(x, 0f), new Vector2(x + w, 0f), on ? OnBg : ChipBg);
                Txt(chip, label, 24, on ? OnInk : Dim, TextAnchor.MiddleCenter);
                var chosen = filter;
                Act(chip, () => { _rerollFilter = chosen; _rerollId = null; _rerollNote = null; _scroll = 0f; Rebuild(); });
                x += w + 10f;
            }

            var eligible = Gear.Where(r => GearForge.CanReroll(r, 0)).ToList();
            var shown = eligible.Where(Passes).ToList();
            var item = shown.Find(r => r.InstanceId == _rerollId) ?? shown.FirstOrDefault();
            _rerollId = item?.InstanceId;

            foreach (var r in shown)
            {
                bool worn = Worn(r);
                var row = PieceRow(r, r == item, PrimaryText(r, r.Tier, r.UpgradeLevel), worn ? "WORN" : null, Good);
                var id = r.InstanceId;
                Act(row, () => { _rerollId = id; _rerollNote = null; Rebuild(); });
            }

            if (item == null)
            {
                Para(eligible.Count == 0 ? "Nothing to re-roll yet." : "Nothing here.", HeadSize, Ink, 56f);
                Para(eligible.Count == 0 ? "Bronze, Silver and Gold pieces carry sub-stats - find one on a floor or open a box."
                                         : "Try another filter.", BodySize, Dim, 120f);
                return;
            }
            BuildRerollItem(item);
        }

        void BuildRerollItem(MintedGearRecord r)
        {
            int owned = _ctx.Boxes.Get(r.Tier);
            var head = Stack(52f, 6f);
            Txt(head, ItemName(r), HeadSize, TierColor(r.Tier));
            Txt(head, $"{Label(r.Tier)} boxes: {owned}", SmallSize, Dim, TextAnchor.MiddleRight);
            StarsAt(Stack(34f, 16f), 0f, r.UpgradeLevel);

            var primary = Stack(44f, 16f);
            Txt(primary, PrimaryText(r, r.Tier, r.UpgradeLevel), BodySize, Dim);
            Txt(primary, "PRIMARY - FIXED", 24, Dim, TextAnchor.MiddleRight);

            Txt(Stack(32f, 10f), r.SubStats.Count == 1 ? "SUB-STAT" : "SUB-STATS", 24, Dim);
            int valueCost = Tuning.GearRoll.ForgeRerollValueBoxCost, statCost = Tuning.GearRoll.ForgeRerollStatBoxCost;
            for (int i = 0; i < r.SubStats.Count; i++)
            {
                var sub = r.SubStats[i];
                var (min, max) = GearRoller.SubStatRange(sub.Kind, r.Tier);
                var panel = UiKit.Panel(Stack(110f, 10f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, CardBg);
                const float bw = 250f;
                Txt(UiKit.Rect(panel, "n", new Vector2(0f, 0.5f), Vector2.one, new Vector2(20f, 0f), new Vector2(-bw * 2f - 50f, -10f)),
                    $"{GearForge.Label(sub.Kind)} {Pct(Scaled(sub.Value, r.UpgradeLevel))}", BodySize, Ink, TextAnchor.LowerLeft);
                Txt(UiKit.Rect(panel, "r", Vector2.zero, new Vector2(1f, 0.5f), new Vector2(20f, 10f), new Vector2(-bw * 2f - 50f, 0f)),
                    $"range {Scaled(min, r.UpgradeLevel):0.#} – {Scaled(max, r.UpgradeLevel):0.#}%", SmallSize, Dim, TextAnchor.UpperLeft);
                int index = i;
                Button(UiKit.Rect(panel, "v", new Vector2(1f, 0f), Vector2.one, new Vector2(-bw * 2f - 26f, 12f), new Vector2(-bw - 26f, -12f)),
                    $"NEW VALUE\n{Plural(valueCost, "BOX", "BOXES")}", false, owned >= valueCost, () => Reroll(r, index, false), 24);
                Button(UiKit.Rect(panel, "s", new Vector2(1f, 0f), Vector2.one, new Vector2(-bw - 12f, 12f), new Vector2(-12f, -12f)),
                    $"NEW STAT\n{Plural(statCost, "BOX", "BOXES")}", false, owned >= statCost, () => Reroll(r, index, true), 24);
            }

            if (!string.IsNullOrEmpty(_rerollNote))
            {
                var note = UiKit.Panel(Stack(64f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Hex(0x1d1729));
                Txt(UiKit.Rect(note, "n", Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f)), _rerollNote, BodySize, Rolled);
            }
            Footer($"Boxes of the piece's own tier. A re-roll can come out worse.", Dim);
        }

        void Reroll(MintedGearRecord r, int index, bool changeStat)
        {
            var old = r.SubStats[index];
            string before = $"{GearForge.Label(old.Kind)} {Pct(Scaled(old.Value, r.UpgradeLevel))}";
            var result = _ctx.OnReroll?.Invoke(r.InstanceId, index, changeStat);
            if (result == null) _rerollNote = $"Not enough {Label(r.Tier)} boxes";
            else
            {
                var now = result.SubStats[index];
                _rerollNote = $"Last re-roll: {before} → {GearForge.Label(now.Kind)} {Pct(Scaled(now.Value, result.UpgradeLevel))}";
                _rerollId = result.InstanceId;
            }
            Rebuild();
        }

        // ------------------------------------------------------------------ Salvage

        /// <summary>
        /// Unwanted Bronze/Silver/Gold pieces break into calx of their tier, CalxPerBox of it
        /// making a box. Tap pieces to pick them, or PICK LONERS for every base piece nothing else
        /// matches; SALVAGE asks once more before anything is destroyed. A piece that has a partner
        /// is marked, since it is one combine away from a star.
        /// </summary>
        void BuildSalvage()
        {
            _salvagePick ??= new HashSet<string>();
            var eligible = Gear
                .Where(r => GearForge.Salvageable(r) && !Worn(r))
                .OrderBy(r => Rank(r.Tier)).ThenBy(r => r.UpgradeLevel).ThenBy(r => r.Slot).ToList();
            _salvagePick.RemoveWhere(id => !eligible.Exists(r => r.InstanceId == id));
            var picked = eligible.Where(r => _salvagePick.Contains(r.InstanceId)).ToList();
            if (picked.Count == 0) _salvageConfirm = false;

            Columns(860f, 72f);
            Txt(UiKit.Rect(_listHead, "n", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-300f, 0f)),
                "Unequipped Bronze, Silver and Gold", SmallSize, Dim);
            // Every BASE piece with no partner. Starred pieces and relics are left to the player -
            // a lone two-star is still half a three-star, and a relic is a finisher.
            Button(UiKit.Rect(_listHead, "loners", new Vector2(1f, 0f), Vector2.one, new Vector2(-280f, 0f), Vector2.zero),
                "PICK LONERS", false, eligible.Count > 0 && !_salvageConfirm, () =>
                {
                    foreach (var r in eligible)
                        if (r.UpgradeLevel == 0 && GearForge.Combinable(r) && !GearForge.HasPartner(r, _ctx.MintedGear, _ctx.IsEquipped))
                            _salvagePick.Add(r.InstanceId);
                    _salvageNote = null;
                    Rebuild();
                }, 24);

            foreach (var r in eligible)
            {
                bool on = _salvagePick.Contains(r.InstanceId);
                string sub = PrimaryText(r, r.Tier, r.UpgradeLevel);
                if (GearForge.HasPartner(r, _ctx.MintedGear, _ctx.IsEquipped)) sub += " · " + Tint("has a partner", Bad);
                var row = PieceRow(r, on, sub, $"{GearForge.CalxFor(r)} calx", TierColor(r.Tier), ticked: on);
                var id = r.InstanceId;
                if (_salvageConfirm) continue;
                Act(row, () =>
                {
                    if (!_salvagePick.Remove(id)) _salvagePick.Add(id);
                    _salvageNote = null;
                    Rebuild();
                });
            }

            if (picked.Count == 0)
            {
                if (eligible.Count == 0) Para("Nothing to salvage.", HeadSize, Ink, 56f);
                Para(eligible.Count == 0
                        ? "Equipped pieces, Diamond and Black Diamond never are."
                        : $"Tap pieces to pick them. Each breaks into calx of its tier - 1 for a base piece, doubling with each star. " +
                          $"{Tuning.GearRoll.CalxPerBox} calx make a box.", BodySize, Dim, 200f);
                if (!string.IsNullOrEmpty(_salvageNote)) Para(_salvageNote, BodySize, Good, 120f);
                return;
            }

            Txt(Stack(36f, 12f), _salvageConfirm ? "THESE ARE DESTROYED" : $"{picked.Count} PICKED", 24, _salvageConfirm ? Bad : Dim);
            int per = Tuning.GearRoll.CalxPerBox;
            foreach (var tier in new[] { LootTier.Bronze, LootTier.Silver, LootTier.Gold })
            {
                var ofTier = picked.Where(r => r.Tier == tier).ToList();
                if (ofTier.Count == 0) continue;
                int calx = ofTier.Sum(GearForge.CalxFor);
                int heldCalx = _ctx.Calx?.Get(tier) ?? 0;
                int total = heldCalx + calx, boxes = total / per;
                var line = Stack(44f, 4f);
                Txt(line, $"{ofTier.Count} {Label(tier)} → {calx} calx", BodySize, TierColor(tier));
                Txt(line, boxes > 0 ? $"+ {heldCalx} held = {Tint(Plural(boxes, $"{Label(tier)} box"), Good)}" : $"+ {heldCalx} held",
                    BodySize, Ink, TextAnchor.MiddleRight);
                var left = Stack(40f, 12f);
                Txt(left, "calx left over", SmallSize, Dim);
                Txt(left, $"{total - boxes * per} / {per}", SmallSize, Dim, TextAnchor.MiddleRight);
            }

            int partnered = picked.Count(r => GearForge.HasPartner(r, _ctx.MintedGear, _ctx.IsEquipped));
            int starred = picked.Count(r => r.UpgradeLevel > 0);
            var warnings = new List<string>();
            if (partnered > 0) warnings.Add($"{partnered} of them could still combine with a piece you hold");
            if (starred > 0) warnings.Add($"{starred} of them carry stars");
            if (warnings.Count > 0)
            {
                var warn = UiKit.Panel(Stack(24f + warnings.Count * 40f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Hex(0x2a1513));
                Txt(UiKit.Rect(warn, "n", Vector2.zero, Vector2.one, new Vector2(20f, 12f), new Vector2(-20f, -12f)),
                    string.Join("\n", warnings), SmallSize, Bad, TextAnchor.MiddleLeft, wrap: true);
            }

            Para(_salvageConfirm ? "Check before you confirm - they cannot be got back." : "Asks once more before anything is destroyed.",
                 SmallSize, _salvageConfirm ? Bad : Dim, 40f);
            if (_salvageConfirm)
                Footer(null, Bad,
                    ("BACK", false, true, () => { _salvageConfirm = false; Rebuild(); }),
                    ($"SALVAGE {picked.Count}", true, true, () =>
                    {
                        var ids = picked.Select(r => r.InstanceId).ToList();
                        _salvageNote = _ctx.OnSalvage?.Invoke(ids) ?? "Nothing was salvaged";
                        _salvagePick.Clear();
                        _salvageConfirm = false;
                        Rebuild();
                    }));
            else
                Footer(null, Dim,
                    ("CLEAR", false, true, () => { _salvagePick.Clear(); Rebuild(); }),
                    ($"SALVAGE {picked.Count}", true, true, () => { _salvageConfirm = true; Rebuild(); }));
        }

        // ------------------------------------------------------------------ Fuse

        /// <summary>
        /// Fusions: specific designs burned into a Black Diamond weapon and its relic. The list holds
        /// the sets held in full (the rule that shows the tab at all); the detail what one burns, each
        /// piece marked held or missing, and what it makes. Nothing here is random, so unlike
        /// combining there is no plan/execute split to protect.
        /// </summary>
        void BuildFuse()
        {
            Columns(ListW, 44f);
            ListNote("Sets of Diamond weapons you hold in full");
            var ready = GearForge.Fusions
                .Where(f => GearForge.FusionInputs(f, _ctx.MintedGear, null).All(r => r != null)).ToList();
            var fusion = _fused != null ? null : ready.Find(f => f.Id == _fusionId) ?? ready.FirstOrDefault();
            _fusionId = fusion?.Id;

            foreach (var f in ready)
            {
                var held = GearForge.FusionInputs(f, _ctx.MintedGear, _ctx.IsEquipped);
                int have = held.Count(r => r != null);
                var row = ListItem(RowH, f == fusion);
                UiKit.Panel(row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, -36f), new Vector2(30f, 36f), TierColor(LootTier.BlackDiamond));
                Txt(UiKit.Rect(row, "n", new Vector2(0f, 0.5f), Vector2.one, new Vector2(46f, 0f), new Vector2(-16f, -8f)),
                    f.Name, BodySize, Ink, TextAnchor.LowerLeft);
                Txt(UiKit.Rect(row, "s", Vector2.zero, new Vector2(1f, 0.5f), new Vector2(46f, 8f), new Vector2(-16f, 0f)),
                    have == f.Inputs.Length ? "ready to fuse" : $"{have} / {f.Inputs.Length} free - unequip the rest", SmallSize,
                    have == f.Inputs.Length ? Good : SubInk, TextAnchor.UpperLeft);
                var id = f.Id;
                Act(row, () => { _fusionId = id; _fused = null; Rebuild(); });
            }

            if (_fused != null)
            {
                Txt(Stack(30f, 4f), "FUSED", SmallSize, Good);
                Txt(Stack(52f, 12f), _fused.DisplayName, HeadSize, TierColor(_fused.Tier));
                Para("and its seal - the relic carrying the weapon art - are yours. The pieces it burned are gone.", BodySize, Ink, 100f);
                Footer(null, Dim, ("DONE", true, true, () =>
                {
                    _fused = null;
                    // The set just burned - if it was the only one, FUSE goes with it.
                    if (!AnyFusionHeld()) _mode = Mode.Open;
                    Rebuild();
                }));
                return;
            }
            if (fusion == null)
            {
                Para("No set held in full.", HeadSize, Ink, 56f);
                return;
            }

            var inputs = GearForge.FusionInputs(fusion, _ctx.MintedGear, _ctx.IsEquipped);
            bool all = inputs.All(r => r != null);
            Txt(Stack(36f, 10f), "BURNS", 24, Dim);
            for (int i = 0; i < fusion.Inputs.Length; i++)
            {
                var design = GearCatalog.Get(fusion.Inputs[i]);
                var line = Stack(44f, 4f);
                Txt(line, design != null ? design.DisplayName : fusion.Inputs[i], BodySize, TierColor(LootTier.Diamond));
                Txt(line, inputs[i] != null ? "held" : "WORN - UNEQUIP IT", SmallSize, inputs[i] != null ? Good : Bad, TextAnchor.MiddleRight);
            }
            _dy += 16f;
            Txt(Stack(36f, 10f), "MAKES", 24, Dim);
            var weapon = GearCatalog.Get(fusion.Weapon);
            var w = Stack(44f, 4f);
            Txt(w, weapon != null ? weapon.DisplayName : fusion.Weapon, BodySize, TierColor(LootTier.BlackDiamond));
            Txt(w, "Black Diamond weapon", SmallSize, Dim, TextAnchor.MiddleRight);
            if (!string.IsNullOrEmpty(fusion.Relic))
            {
                var relic = GearCatalog.Get(fusion.Relic);
                var rl = Stack(44f, 4f);
                Txt(rl, relic != null ? relic.DisplayName : fusion.Relic, BodySize, TierColor(LootTier.BlackDiamond));
                Txt(rl, $"relic - {relic?.Signature?.DisplayName ?? "its weapon art"}", SmallSize, Dim, TextAnchor.MiddleRight);
            }

            var chosen = fusion;
            Footer(all ? "These are burned - check before you confirm" : "Equipped pieces don't count - unequip first", all ? Dim : Bad,
                ("FUSE", true, all, () =>
                {
                    var made = _ctx.OnFuse?.Invoke(chosen.Id);
                    if (made != null) _fused = made;
                    Rebuild();
                }));
        }
    }
}
