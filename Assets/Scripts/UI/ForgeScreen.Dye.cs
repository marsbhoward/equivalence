using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Convergence.Core;
using Convergence.Art.Gear;
using Convergence.Chain;

namespace Convergence.UI
{
    /// <summary>
    /// The Forge's DYE tab (MONETIZATION.md "Dyes"; the user's calls, 2026-10-04 to 2026-10-25):
    /// Diamond armour, piece by piece, a MAIN and an ACCENT channel each. The sets down the list (the
    /// chosen one open to its pieces), the piece on the right - its picture, its channels, the
    /// colours - previewed freely; every change waits in a SESSION, priced live - one charge per
    /// piece that changes, one dye per channel set to a new colour - and CONFIRM spends it all at
    /// once (Chain.Recolour, the store's RecolourAsync). A Diamond box breaks into charges here.
    /// </summary>
    public partial class ForgeScreen
    {
        string _dyeSet;
        string _dyePiece;
        int _dyeChannel;
        // Not readonly, and rebuilt if null - see the domain reload rules.
        Dictionary<string, string[]> _dyePending = new();
        string _dyeNote;
        bool _dyeBusy;

        void ResetDye()
        {
            _dyeSet = null; _dyePiece = null; _dyeChannel = 0;
            (_dyePending ??= new Dictionary<string, string[]>()).Clear();
            _dyeNote = null; _dyeBusy = false;
        }

        CharacterItems DyeItems => _ctx.Items?.Invoke() ?? new CharacterItems();

        /// <summary>What a piece wears in this session: its pending colours, else what it is dyed.</summary>
        string[] Wearing(GearItem piece)
        {
            _dyePending ??= new Dictionary<string, string[]>();
            if (_dyePending.TryGetValue(piece.ItemId, out var p)) return p;
            return DyeItems.SwatchesOn(piece.ItemId, piece.DyeChannels.Length);
        }

        List<PieceDye> Session()
        {
            var list = new List<PieceDye>();
            foreach (var kv in _dyePending ?? new Dictionary<string, string[]>())
            {
                var c = new PieceDye { Piece = kv.Key };
                foreach (var s in kv.Value) c.Swatches.Add(s ?? "");
                list.Add(c);
            }
            return list;
        }

        /// <summary>The DYE tab's own "set" of the character's statted capes and scarves - recoloured
        /// for free (DemoGear.FreeDye), so they sit apart from the Diamond sets.</summary>
        const string FreeSet = "@free";

        /// <summary>The free looks the character's statted gear wears, one per design - a dye is
        /// keyed by DESIGN, so two Hoplite Capes wear one colour.</summary>
        List<GearItem> FreePieces()
            => Gear
                .Where(r => r != null && string.IsNullOrEmpty(r.Design))
                .Select(r => r.Look).Where(DemoGear.FreeDye).Distinct()
                .Select(GearCatalog.Get).Where(p => p != null && p.DyeChannels is { Length: > 0 })
                .OrderBy(p => p.Slot).ThenBy(p => p.DisplayName).ToList();

        /// <summary>Diamond armour the character OWNS (a record wearing the design) that takes a dye -
        /// only while it holds a dye to put on it (the user's call, 2026-11-12: draw nothing it cannot
        /// recolour). Grouped by set; each set is built from what is held, never the whole catalogue.</summary>
        Dictionary<string, List<GearItem>> OwnedSets(CharacterItems items)
        {
            var sets = new Dictionary<string, List<GearItem>>();
            if (items.TotalDyes() <= 0) return sets;
            foreach (var design in Gear.Where(r => r != null && !string.IsNullOrEmpty(r.Design)).Select(r => r.Design).Distinct())
            {
                var piece = GearCatalog.Get(design);
                if (piece == null || piece.DyeChannels is not { Length: > 0 } || DemoGear.FreeDye(piece.ItemId)) continue;
                string set = ArmourSets.SetOf(piece);
                if (string.IsNullOrEmpty(set)) continue;
                if (!sets.TryGetValue(set, out var list)) sets[set] = list = new List<GearItem>();
                list.Add(piece);
            }
            foreach (var list in sets.Values) list.Sort((a, b) => a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return sets;
        }

        void BuildDye()
        {
            _dyePending ??= new Dictionary<string, string[]>();
            Columns(ListW, 44f);
            ListNote("Nothing is spent until you confirm");

            var items = DyeItems;
            var owned = OwnedSets(items);
            var free = FreePieces();
            var sets = owned.Keys.OrderBy(ArmourSets.Name).ToList();
            // The capes and scarves first: on a statted character (the showcase) they are all there is.
            if (free.Count > 0) sets.Insert(0, FreeSet);
            List<GearItem> PiecesOf(string set) => set == FreeSet ? free : owned[set];
            if (_dyeSet == null || !sets.Contains(_dyeSet)) _dyeSet = sets.FirstOrDefault();

            if (sets.Count == 0)
            {
                _dyePiece = null;
                Para("Nothing to recolour yet.", HeadSize, Ink, 56f);
                Para("Diamond armour you own can be dyed, channel by channel, once you hold a dye - dyes come from " +
                     "milestones: the first avatar on floor 25, and the first clears of floors 30, 40 and 50. " +
                     "Capes and scarves recolour for free.", BodySize, Dim, 220f);
                if (items.TotalDyes() <= 0 && Gear.Any(r => !string.IsNullOrEmpty(r.Design)))
                    Para("You own Diamond armour - it shows here once you hold a dye.", BodySize, Powered, 80f);
                DyeFooter(items);
                return;
            }

            foreach (var id in sets)
            {
                var pieces = PiecesOf(id);
                bool open = id == _dyeSet;
                int dyed = pieces.Count(p => items.Dyed.Any(d => d.Piece == p.ItemId));
                int pending = pieces.Count(p => _dyePending.ContainsKey(p.ItemId));
                bool isFree = id == FreeSet;
                var row = ListItem(96f, open && _dyePiece == null);
                Txt(UiKit.Rect(row, "n", new Vector2(0f, 0.5f), Vector2.one, new Vector2(24f, 0f), new Vector2(-60f, -6f)),
                    isFree ? "Capes & Scarves" : ArmourSets.Name(id), BodySize, isFree ? Powered : TierColor(LootTier.Diamond), TextAnchor.LowerLeft);
                Txt(UiKit.Rect(row, "s", Vector2.zero, new Vector2(1f, 0.5f), new Vector2(24f, 6f), new Vector2(-60f, 0f)),
                    (isFree ? dyed > 0 ? $"{dyed} recoloured" : "free to recolour" : dyed > 0 ? $"{dyed} dyed" : "as made") +
                    (pending > 0 ? "  ·  " + Tint($"{pending} changing", Powered) : ""),
                    SmallSize, SubInk, TextAnchor.UpperLeft);
                Txt(UiKit.Rect(row, "o", new Vector2(1f, 0f), Vector2.one, new Vector2(-56f, 0f), new Vector2(-16f, 0f)),
                    open ? "-" : "+", BodySize, Dim, TextAnchor.MiddleCenter);
                string chosen = id;
                Act(row, () => { _dyeSet = chosen; _dyePiece = null; _dyeChannel = 0; Rebuild(); });
                if (!open) continue;

                foreach (var piece in pieces)
                {
                    bool on = piece.ItemId == _dyePiece;
                    bool changing = _dyePending.ContainsKey(piece.ItemId);
                    var sub = Framed(_listContent, new Vector2(0f, 1f), Vector2.one, new Vector2(48f, -_listH - 84f), new Vector2(0f, -_listH),
                        on, on ? OnBg : CardBg);
                    _listH += 84f + RowGap;
                    Txt(UiKit.Rect(sub, "n", Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f)),
                        piece.DisplayName, SmallSize, Ink, TextAnchor.MiddleLeft);
                    if (changing) Txt(UiKit.Rect(sub, "c", Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f)),
                        "changing", 24, Powered, TextAnchor.MiddleRight);
                    string pid = piece.ItemId;
                    Act(sub, () => { _dyePiece = pid; _dyeChannel = 0; Rebuild(); });
                }
            }

            var piece0 = _dyePiece != null ? GearCatalog.Get(_dyePiece) : null;
            if (piece0 != null && piece0.DyeChannels is { Length: > 0 }) BuildDyePiece(piece0);
            else
            {
                _dyePiece = null;
                Para("Recolour a piece, channel by channel.", HeadSize, Ink, 56f);
                Para("Each piece that changes takes a charge, and each channel set to a new colour takes one dye of it. " +
                     "Capes and scarves recolour for free, in the Common and Uncommon colours.", BodySize, Dim, 160f);
            }
            DyeFooter(items);
        }

        /// <summary>One piece: its picture in the colours it would wear, its channels, and every
        /// colour the chosen channel can take, with what is held.</summary>
        void BuildDyePiece(GearItem piece)
        {
            var wearing = (string[])Wearing(piece).Clone();
            var authored = DyeItems.SwatchesOn(piece.ItemId, piece.DyeChannels.Length);
            _dyeChannel = Mathf.Clamp(_dyeChannel, 0, piece.DyeChannels.Length - 1);

            var top = Stack(176f, 18f);
            var frame = UiKit.Panel(top, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(176f, 0f), CardBg);
            PiecePicture(frame, piece, wearing, Vector2.zero, Vector2.one);
            Txt(UiKit.Rect(top, "n", new Vector2(0f, 1f), Vector2.one, new Vector2(196f, -52f), Vector2.zero),
                piece.DisplayName, HeadSize, Ink);
            int n = piece.DyeChannels.Length;
            for (int c = 0; c < n; c++)
            {
                int ch = c;
                var sw = DyeCatalog.Get(wearing[c]);
                bool on = c == _dyeChannel;
                var tab = Framed(top, new Vector2(c / (float)n, 0f), new Vector2((c + 1) / (float)n, 0f),
                    new Vector2(c == 0 ? 196f : 8f, 0f), new Vector2(c == n - 1 ? 0f : -8f, 104f), on, on ? OnBg : RowBg);
                if (sw != null)
                    UiKit.Panel(tab, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(14f, 16f), new Vector2(70f, -16f), sw.Color);
                Txt(UiKit.Rect(tab, "n", new Vector2(0f, 0.5f), Vector2.one, new Vector2(sw != null ? 84f : 20f, 0f), new Vector2(-10f, -8f)),
                    $"{(c == 0 ? "Main" : "Accent")} - {piece.DyeChannels[c].Name}", SmallSize, Dim, TextAnchor.LowerLeft);
                Txt(UiKit.Rect(tab, "s", Vector2.zero, new Vector2(1f, 0.5f), new Vector2(sw != null ? 84f : 20f, 8f), new Vector2(-10f, 0f)),
                    sw?.Name ?? "As made", BodySize, Ink, TextAnchor.UpperLeft);
                Act(tab, () => { _dyeChannel = ch; Rebuild(); });
            }

            // The colours for the chosen channel: "as made", then every swatch.
            var items = DyeItems;
            var price = Recolour.Cost(items, Session());
            bool free = DemoGear.FreeDye(piece.ItemId);
            var options = new List<DyeSwatch> { null };
            // A free piece takes the Common and Uncommon colours, for nothing (Recolour.FreeSwatch).
            options.AddRange(free ? DyeCatalog.All.Where(Recolour.FreeSwatch) : DyeCatalog.All);
            const int cols = 4;
            const float h = 72f, gap = 10f;
            int rows = (options.Count + cols - 1) / cols;
            var grid = UiKit.Rect(_detail, "swatches", Vector2.zero, Vector2.one, new Vector2(0f, ButtonH + 64f), new Vector2(0f, -_dy));
            // On a short screen the colours shrink to fit rather than run under the buttons.
            float avail = grid.rect.height > 0f ? grid.rect.height : rows * (h + gap);
            float cell = Mathf.Min(h, (avail - (rows - 1) * gap) / rows);
            for (int i = 0; i < options.Count; i++)
            {
                var sw = options[i];
                int c = i % cols, r = i / cols;
                bool current = (wearing[_dyeChannel] ?? "") == (sw?.Id ?? "");
                int have = sw == null ? 0 : items.DyesOf(sw.Id);
                int need = sw != null && price.Dyes.TryGetValue(sw.Id, out int k) ? k : 0;
                // A colour the piece already wears costs no dye; one not held can still be previewed.
                bool reachable = free || sw == null || current || have > need || authored[_dyeChannel] == sw.Id;
                var tile = Framed(grid, new Vector2(c / (float)cols, 1f), new Vector2((c + 1) / (float)cols, 1f),
                    new Vector2(c == 0 ? 0f : 5f, -r * (cell + gap) - cell), new Vector2(c == cols - 1 ? 0f : -5f, -r * (cell + gap)),
                    current, current ? OnBg : CardBg);
                if (sw != null)
                    UiKit.Panel(tile, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, 10f), new Vector2(10f + cell - 20f, -10f), sw.Color);
                float textX = sw == null ? 20f : cell + 4f;
                if (sw == null)
                    Txt(UiKit.Rect(tile, "n", Vector2.zero, Vector2.one, new Vector2(textX, 0f), new Vector2(-8f, 0f)),
                        "As made", SmallSize, Ink, TextAnchor.MiddleLeft);
                else
                {
                    // The name over what is held: side by side they ran out of the tile at four across.
                    Txt(UiKit.Rect(tile, "n", new Vector2(0f, 0.5f), Vector2.one, new Vector2(textX, -2f), new Vector2(-8f, 0f)),
                        sw.Name, SmallSize, reachable ? Ink : Dim, TextAnchor.LowerLeft);
                    Txt(UiKit.Rect(tile, "h", Vector2.zero, new Vector2(1f, 0.5f), new Vector2(textX, 0f), new Vector2(-8f, 2f)),
                        free ? "free" : $"{have} held", 22, free ? Good : reachable ? SubInk : Off, TextAnchor.UpperLeft);
                }
                string id = sw?.Id;
                Act(tile, () =>
                {
                    var next = (string[])Wearing(piece).Clone();
                    next[_dyeChannel] = id;
                    // Back to exactly what the piece wears already: no change left to pay for.
                    if (next.SequenceEqual(authored)) _dyePending.Remove(piece.ItemId);
                    else _dyePending[piece.ItemId] = next;
                    _dyeNote = null;
                    Rebuild();
                });
            }
        }

        /// <summary>The standing line - what is held, what the session spends - and the session's buttons.</summary>
        void DyeFooter(CharacterItems items)
        {
            var session = Session();
            var price = Recolour.Cost(items, session);
            bool ok = session.Count > 0 && price.Refusal == null && price.Affordable(items);
            string spend = session.Count == 0 ? "no changes yet"
                : $"this recolour: {Plural(price.Charges, "charge", "charges")}" +
                  (price.Dyes.Count > 0 ? ", " + string.Join(", ", price.Dyes.Select(kv => $"{kv.Value} {DyeCatalog.Get(kv.Key)?.Name ?? kv.Key}")) : "");
            var line = UiKit.Rect(_detail, "standing", Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, ButtonH + 12f), new Vector2(0f, ButtonH + 52f));
            Txt(line, $"Charges {items.Charges}  ·  dyes {items.TotalDyes()}  ·  " +
                      Tint(spend, session.Count == 0 || ok ? Dim : Bad), SmallSize, Ink);
            if (_dyeNote != null) Txt(line, _dyeNote, SmallSize, Rolled, TextAnchor.MiddleRight);

            var buttons = new List<(string, bool, bool, System.Action)>();
            int breakable = _ctx.BreakableBoxes?.Invoke() ?? 0;
            if (_ctx.OnBreakBox != null)
                buttons.Add(($"BREAK A BOX\n+{Tuning.Cosmetics.ChargesPerBox} CHARGES", false, breakable > 0 && !_dyeBusy, () =>
                    RunDye(_ctx.OnBreakBox(), $"A Diamond box broken - {Tuning.Cosmetics.ChargesPerBox} charges")));
            // Three buttons at most fit beside the list on a desktop: WITHDRAW only between sessions.
            if (_ctx.OnWithdraw != null && _dyePiece == null && session.Count == 0 && items.TotalDyes() > 0)
                buttons.Add(("WITHDRAW", false, !_dyeBusy, () =>
                {
                    var first = items.Dyes.FirstOrDefault(d => d.Count > 0);
                    if (first != null) RunDye(_ctx.OnWithdraw(first.Swatch, first.Count), $"Withdrawn to the wallet: {first.Count} {DyeCatalog.Get(first.Swatch)?.Name}");
                }));
            if (session.Count > 0)
                buttons.Add(("DISCARD", false, !_dyeBusy, () => { _dyePending.Clear(); _dyeNote = null; Rebuild(); }));
            buttons.Add((session.Count > 0 && !ok ? "NOT ENOUGH" : "RECOLOUR", true, ok && !_dyeBusy, () =>
                RunDye(_ctx.OnRecolour(Session()), "Recoloured", clearSession: true)));
            Footer(breakable > 0 ? $"Diamond boxes held: {breakable}" : null, Dim, buttons.ToArray());
        }

        async void RunDye(System.Threading.Tasks.Task<string> work, string done, bool clearSession = false)
        {
            _dyeBusy = true;
            _dyeNote = "working...";
            Rebuild();
            string refused;
            try { refused = await work; }
            catch (System.Exception e) { refused = e.Message; }
            _dyeBusy = false;
            if (!IsOpen) return;
            if (refused == null && clearSession) _dyePending.Clear();
            _dyeNote = refused ?? done;
            Rebuild();
        }
    }
}
