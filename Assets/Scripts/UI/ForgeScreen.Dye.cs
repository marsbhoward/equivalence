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
    /// The Forge's DYE tab (MONETIZATION.md "Dyes"; the user's calls, 2026-10-04 to 2026-10-25):
    /// Diamond armour, piece by piece, a MAIN and an ACCENT channel each. Set -> piece -> channel ->
    /// colour, previewed freely; every change waits in a SESSION, priced live - one charge per piece
    /// that changes, one dye per channel set to a new colour - and CONFIRM spends it all at once
    /// (Chain.Recolour, the store's RecolourAsync). A Diamond box breaks into charges here.
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

        static readonly Color SwatchFrame = new(0.08f, 0.07f, 0.06f);

        void BuildDye()
        {
            var items = DyeItems;
            var session = Session();
            var price = Recolour.Cost(items, session);

            // ---- the standing line: what is held, and what the session will spend ----
            string held = $"charges {items.Charges}    dyes {items.TotalDyes()}";
            string spend = session.Count == 0 ? "no changes yet"
                : $"this session: {price.Charges} charge{(price.Charges == 1 ? "" : "s")}" +
                  (price.Dyes.Count > 0 ? ", " + string.Join(", ", price.Dyes.Select(kv => $"{kv.Value} {DyeCatalog.Get(kv.Key)?.Name ?? kv.Key}")) : "");
            Line(-4f, held + "      " + spend, 20, price.Affordable(items) || session.Count == 0 ? Ink : RefusedInk, 1700f);
            if (_dyeNote != null) Line(-36f, _dyeNote, 18, Dim, 1700f);

            if (_dyePiece != null) BuildDyePiece();
            else if (_dyeSet != null) BuildDyeSet();
            else BuildDyeSets();

            // ---- the session's buttons, along the foot ----
            float y = NavY;
            int breakable = _ctx.BreakableBoxes?.Invoke() ?? 0;
            if (_ctx.OnBreakBox != null)
                Button(-600f, y, 330f, 128f, breakable > 0
                        ? $"BREAK A DIAMOND BOX\n+{Tuning.Cosmetics.ChargesPerBox} charges  ({breakable} held)"
                        : "BREAK A DIAMOND BOX\nnone held",
                    breakable > 0 && !_dyeBusy ? ButtonBg : QuietBg, () =>
                    {
                        if (_dyeBusy || breakable <= 0) return;
                        RunDye(_ctx.OnBreakBox(), $"a Diamond box broken - {Tuning.Cosmetics.ChargesPerBox} charges");
                    });
            if (session.Count > 0)
            {
                bool ok = price.Refusal == null && price.Affordable(items);
                Button(-240f, y, 300f, 128f, ok ? "CONFIRM\nrecolour" : "CONFIRM\nnot enough", ok && !_dyeBusy ? ButtonBg : QuietBg, () =>
                {
                    if (_dyeBusy || !ok) return;
                    RunDye(_ctx.OnRecolour(Session()), "recoloured", clearSession: true);
                });
                Button(90f, y, 240f, 128f, "DISCARD", QuietBg, () => { _dyePending.Clear(); _dyeNote = null; Rebuild(); });
            }
            if (_ctx.OnWithdraw != null && _dyePiece == null && items.TotalDyes() > 0)
                Button(360f, y, 300f, 128f, "WITHDRAW\none of each dye", _dyeBusy ? QuietBg : ButtonBg, () =>
                {
                    if (_dyeBusy) return;
                    var first = items.Dyes.FirstOrDefault(d => d.Count > 0);
                    if (first != null) RunDye(_ctx.OnWithdraw(first.Swatch, first.Count), $"withdrawn to the wallet: {first.Count} {DyeCatalog.Get(first.Swatch)?.Name}");
                });
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

        // ------------------------------------------------------------------ step 1: the sets

        void BuildDyeSets()
        {
            var sets = ArmourSets.All().Where(id => ArmourSets.Pieces(id).Any(p => p.DyeChannels is { Length: > 0 })).ToList();
            var items = DyeItems;
            for (int i = 0; i < sets.Count; i++)
            {
                string id = sets[i];
                var pieces = ArmourSets.Pieces(id);
                int dyed = pieces.Count(p => items.Dyed.Any(d => d.Piece == p.ItemId));
                int pending = pieces.Count(p => _dyePending.ContainsKey(p.ItemId));
                var card = Card(i, 5, 330f, 96f, 16f, -70f);
                UiKit.Label(UiKit.Rect(card, "n", Vector2.zero, Vector2.one, new Vector2(12, 6), new Vector2(-12, -6)),
                    ArmourSets.Name(id) + "\n" + (dyed > 0 ? $"{dyed} dyed" : "as authored") + (pending > 0 ? $"  ·  {pending} changing" : ""),
                    18, pending > 0 ? PoweredInk : TierColor(LootTier.Diamond), TextAnchor.MiddleLeft);
                string chosen = id;
                _actions.Add((card, () => { _dyeSet = chosen; Rebuild(); }));
            }
        }

        // ------------------------------------------------------------------ step 2: a set's pieces

        void BuildDyeSet()
        {
            var pieces = ArmourSets.Pieces(_dyeSet).Where(p => p.DyeChannels is { Length: > 0 }).ToList();
            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                var card = Card(i, 4, ItemW, ItemH, ItemGap, GridTop - 20f);
                var wearing = Wearing(piece);
                if (_dyePending.ContainsKey(piece.ItemId)) card.GetComponent<Image>().color = new Color(0.36f, 0.25f, 0.1f);
                PiecePicture(card, piece, wearing, new Vector2(0f, 0.3f), new Vector2(0.45f, 1f));
                UiKit.Label(UiKit.Rect(card, "n", new Vector2(0.45f, 0f), Vector2.one, new Vector2(6, 8), new Vector2(-10, -8)),
                    piece.DisplayName + "\n" + ChannelText(piece, wearing), 17, Ink, TextAnchor.MiddleLeft);
                var chosen = piece;
                _actions.Add((card, () => { _dyePiece = chosen.ItemId; _dyeChannel = 0; Rebuild(); }));
            }
            Button(-840f, NavY, 200f, 128f, "BACK", QuietBg, () => { _dyeSet = null; Rebuild(); });
        }

        static string ChannelText(GearItem piece, string[] wearing)
        {
            var parts = new List<string>();
            for (int c = 0; c < piece.DyeChannels.Length; c++)
                parts.Add($"{piece.DyeChannels[c].Name}: {DyeCatalog.Get(c < wearing.Length ? wearing[c] : null)?.Name ?? "as authored"}");
            return string.Join("\n", parts);
        }

        /// <summary>The piece's menu art in the colours it would wear - the dyed copy, built once
        /// (Chain.Recolour.Registered), every layer side by side.</summary>
        void PiecePicture(RectTransform parent, GearItem piece, string[] swatches, Vector2 min, Vector2 max)
        {
            var drawn = Recolour.Registered(piece, swatches) ?? piece;
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

        // ------------------------------------------------------------------ step 3: one piece

        void BuildDyePiece()
        {
            var piece = GearCatalog.Get(_dyePiece);
            if (piece == null || piece.DyeChannels is not { Length: > 0 }) { _dyePiece = null; Rebuild(); return; }
            var wearing = (string[])Wearing(piece).Clone();
            var authored = DyeItems.SwatchesOn(piece.ItemId, piece.DyeChannels.Length);
            _dyeChannel = Mathf.Clamp(_dyeChannel, 0, piece.DyeChannels.Length - 1);

            // The preview, large, on the left.
            var frame = UiKit.Panel(_content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-900f, -525f), new Vector2(-420f, -70f), new Color(0.11f, 0.09f, 0.08f));
            PiecePicture(frame, piece, wearing, new Vector2(0f, 0.12f), Vector2.one);
            UiKit.Label(UiKit.Rect(frame, "n", Vector2.zero, new Vector2(1f, 0.12f), new Vector2(8, 4), new Vector2(-8, -4)),
                piece.DisplayName, 20, Ink, TextAnchor.MiddleCenter);

            // The channels.
            for (int c = 0; c < piece.DyeChannels.Length; c++)
            {
                int ch = c;
                var sw = DyeCatalog.Get(wearing[c]);
                var tab = UiKit.Panel(_content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-380f + c * 420f, -150f), new Vector2(-380f + c * 420f + 400f, -70f),
                    c == _dyeChannel ? ButtonBg : QuietBg);
                if (sw != null)
                    UiKit.Panel(tab, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10, 12), new Vector2(66, -12), sw.Color);
                UiKit.Label(UiKit.Rect(tab, "n", Vector2.zero, Vector2.one, new Vector2(78, 4), new Vector2(-8, -4)),
                    $"{(c == 0 ? "MAIN" : "ACCENT")} - {piece.DyeChannels[c].Name}\n{sw?.Name ?? "as authored"}",
                    18, Ink, TextAnchor.MiddleLeft);
                _actions.Add((tab, () => { _dyeChannel = ch; Rebuild(); }));
            }

            // The colours for the chosen channel: "as authored", then every swatch, with what is held.
            var items = DyeItems;
            var session = Session();
            var price = Recolour.Cost(items, session);
            var options = new List<DyeSwatch> { null };
            options.AddRange(DyeCatalog.All);
            const int cols = 5;
            const float w = 250f, h = 80f, gap = 12f, x0 = -380f, y0 = -170f;
            for (int i = 0; i < options.Count; i++)
            {
                var sw = options[i];
                float x = x0 + (i % cols) * (w + gap), y = y0 - (i / cols) * (h + gap);
                bool current = (wearing[_dyeChannel] ?? "") == (sw?.Id ?? "");
                int have = sw == null ? 0 : items.DyesOf(sw.Id);
                int need = sw != null && price.Dyes.TryGetValue(sw.Id, out int n) ? n : 0;
                // A colour the piece already wears costs no dye; one not held can still be previewed.
                bool reachable = sw == null || current || have > need || authored[_dyeChannel] == sw.Id;
                var card = UiKit.Panel(_content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(x, y - h), new Vector2(x + w, y), current ? new Color(0.36f, 0.25f, 0.1f) : SwatchFrame);
                if (sw != null)
                    UiKit.Panel(card, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8, 8), new Vector2(60, -8), sw.Color);
                string label = sw == null ? "as authored" : $"{sw.Name}\n{have} held · {(sw.Rarity == DyeRarity.VeryRare ? "Very Rare" : sw.Rarity.ToString())}";
                UiKit.Label(UiKit.Rect(card, "n", Vector2.zero, Vector2.one, new Vector2(sw == null ? 12 : 70, 4), new Vector2(-6, -4)),
                    label, 16, reachable ? Ink : Dim, TextAnchor.MiddleLeft);
                string id = sw?.Id;
                _actions.Add((card, () =>
                {
                    var next = (string[])Wearing(piece).Clone();
                    next[_dyeChannel] = id;
                    // Back to exactly what the piece wears already: no change left to pay for.
                    if (next.SequenceEqual(authored)) _dyePending.Remove(piece.ItemId);
                    else _dyePending[piece.ItemId] = next;
                    _dyeNote = null;
                    Rebuild();
                }));
            }
            Button(-840f, NavY, 200f, 128f, "BACK", QuietBg, () => { _dyePiece = null; Rebuild(); });
        }
    }
}
