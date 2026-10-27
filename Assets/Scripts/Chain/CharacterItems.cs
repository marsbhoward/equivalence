using System;
using System.Collections.Generic;
using Convergence.Art.Gear;

namespace Convergence.Chain
{
    /// <summary>One colour of earned dye and how many the character holds.</summary>
    [Serializable]
    public class DyeCount
    {
        public string Swatch;
        public int Count;
    }

    /// <summary>The swatch each dye channel of one DESIGN wears on this character ([0] main,
    /// [1] accent; empty = as authored). Keyed by the design, so a minted copy and the authored
    /// piece it wears look the same.</summary>
    [Serializable]
    public class PieceDye
    {
        public string Piece;
        public List<string> Swatches = new();
    }

    /// <summary>
    /// WHAT THE DATABASE HOLDS FOR ONE CHARACTER (the user's calls, 2026-10-25; MONETIZATION.md
    /// "The plan as of 2026-10-25"): earned DYES (a count per colour, never a token until withdrawn),
    /// dye CHARGES (a Diamond box breaks into seven), what each Diamond armour design is DYED, the
    /// POSES it has earned (untradeable) and the MILESTONES it has passed. Per CHARACTER - it travels
    /// with the character NFT - never per account.
    ///
    /// For a service player THE SERVICE IS THE AUTHORITY (web/service/src/routes/items.ts): this copy
    /// on the profile is overwritten by the service's answer on every load and after every change,
    /// and anything the client writes here in a save is ignored. A local save keeps it itself.
    /// </summary>
    [Serializable]
    public class CharacterItems
    {
        public List<DyeCount> Dyes = new();
        public int Charges;
        public List<PieceDye> Dyed = new();
        public List<string> Poses = new();
        public List<string> Milestones = new();
        /// <summary>The HUB THEMES the character has earned (Hub.HubThemes.Id - "hub.study"...), each a
        /// whole room: floor, wall and every hub item (the user's group 7/8 calls). Untradeable, on the
        /// avatar kill's roll shared with the poses. The Cloister is everyone's and never listed.</summary>
        public List<string> Themes = new();
        /// <summary>Diamond boxes the SERVICE holds for the character (earned without a PFP) - a
        /// service answer only, breakable for charges like any box.</summary>
        public int HeldDiamond;

        public int DyesOf(string swatch)
        {
            foreach (var d in Dyes) if (d != null && d.Swatch == swatch) return d.Count;
            return 0;
        }

        public int TotalDyes()
        {
            int n = 0;
            foreach (var d in Dyes) if (d != null) n += d.Count;
            return n;
        }

        public void AddDye(string swatch, int n)
        {
            if (string.IsNullOrEmpty(swatch) || n == 0) return;
            Dyes ??= new List<DyeCount>();
            var d = Dyes.Find(x => x != null && x.Swatch == swatch);
            if (d == null) Dyes.Add(d = new DyeCount { Swatch = swatch });
            d.Count = Math.Max(0, d.Count + n);
            if (d.Count == 0) Dyes.Remove(d);
            Dyes.Sort((a, b) => string.CompareOrdinal(a.Swatch, b.Swatch));
        }

        /// <summary>The swatches one design wears, padded to its channel count (null = as authored).</summary>
        public string[] SwatchesOn(string piece, int channels)
        {
            var result = new string[Math.Max(0, channels)];
            var d = Dyed?.Find(x => x != null && x.Piece == piece);
            if (d?.Swatches == null) return result;
            for (int i = 0; i < result.Length && i < d.Swatches.Count; i++)
                result[i] = string.IsNullOrEmpty(d.Swatches[i]) ? null : d.Swatches[i];
            return result;
        }

        public void SetDyed(string piece, string[] swatches)
        {
            Dyed ??= new List<PieceDye>();
            Dyed.RemoveAll(x => x == null || x.Piece == piece);
            bool any = false;
            foreach (var s in swatches) any |= !string.IsNullOrEmpty(s);
            if (!any) return;
            var entry = new PieceDye { Piece = piece };
            foreach (var s in swatches) entry.Swatches.Add(s ?? "");
            Dyed.Add(entry);
            Dyed.Sort((a, b) => string.CompareOrdinal(a.Piece, b.Piece));
        }

        public bool HasPose(string pose) => Poses != null && Poses.Contains(pose);
        public bool HasTheme(string theme) => Themes != null && Themes.Contains(theme);

        /// <summary>A collectible won on the shared roll - a hub theme ("hub.*") or a pose - added to its list.</summary>
        public void Grant(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var list = id.StartsWith("hub.") ? (Themes ??= new List<string>()) : (Poses ??= new List<string>());
            if (!list.Contains(id)) list.Add(id);
        }
        public bool Passed(string milestone) => Milestones != null && Milestones.Contains(milestone);

        public CharacterItems Copy() => UnityEngine.JsonUtility.FromJson<CharacterItems>(UnityEngine.JsonUtility.ToJson(this));
    }

    /// <summary>
    /// How a character's hub is dressed: a THEME for the room (floor and wall) and for each hub item
    /// (Hub.HubItem - the index), chosen apart in the crate's HUB tab. Only an OWNED theme counts:
    /// anything else, or a slot an older save never wrote, shows the Cloister. Part of the save (the
    /// client's to write) - ownership is what the service decides, and the hub resolves against it.
    /// </summary>
    [Serializable]
    public class HubLook
    {
        public List<int> Items = new();
        /// <summary>Themes won but not yet opened - the PARCEL waiting on the crate (the user's pick:
        /// named at the run's end, and the parcel waits at home). Ids ("hub.study").</summary>
        public List<string> Unopened = new();

        /// <summary>Dress every item, the room included, in one theme.</summary>
        public void SetAll(int theme) { for (int i = 0; i < 14; i++) Set(i, theme); }

        public int Get(int item) => Items != null && item >= 0 && item < Items.Count ? Items[item] : 0;

        public void Set(int item, int theme)
        {
            Items ??= new List<int>();
            while (Items.Count <= item) Items.Add(0);
            Items[item] = theme;
        }
    }

    /// <summary>
    /// RECOLOURING at the Forge (MONETIZATION.md "Dyes"): ONE CHARGE per piece whose colours change,
    /// plus ONE DYE per channel set to a colour it was not already wearing. Putting a channel back
    /// as authored costs no dye (the piece's charge still pays for the rewrite). The service applies
    /// the same rule (web/service/src/cosmetics.ts `recolourCost`) - keep the two in step.
    /// </summary>
    public static class Recolour
    {
        public sealed class Price
        {
            public int Charges;
            public readonly Dictionary<string, int> Dyes = new();
            /// <summary>Why the change cannot be made at all (an unknown piece or colour), or null.</summary>
            public string Refusal;

            public bool Affordable(CharacterItems items)
            {
                if (Refusal != null || items == null || items.Charges < Charges) return false;
                foreach (var kv in Dyes) if (items.DyesOf(kv.Key) < kv.Value) return false;
                return true;
            }
        }

        /// <summary>A design that can be dyed: authored Diamond armour with channels.</summary>
        public static GearItem Dyeable(string piece)
        {
            var item = GearCatalog.Get(piece);
            return item != null && item.DyeChannels is { Length: > 0 } && string.IsNullOrEmpty(item.StackKey) ? item : null;
        }

        /// <summary>What <paramref name="changes"/> would cost on top of what <paramref name="items"/>
        /// already wears. Several changes to one piece are priced as its final colours.</summary>
        public static Price Cost(CharacterItems items, IReadOnlyList<PieceDye> changes)
        {
            var price = new Price();
            var seen = new HashSet<string>();
            for (int i = changes.Count - 1; i >= 0; i--)   // the LAST change to a piece is what it ends as
            {
                var c = changes[i];
                if (c == null || !seen.Add(c.Piece)) continue;
                var design = Dyeable(c.Piece);
                if (design == null) { price.Refusal = $"{c.Piece} cannot be dyed"; return price; }
                int channels = design.DyeChannels.Length;
                var was = items.SwatchesOn(c.Piece, channels);
                bool changed = false;
                for (int ch = 0; ch < channels; ch++)
                {
                    string want = ch < c.Swatches.Count && !string.IsNullOrEmpty(c.Swatches[ch]) ? c.Swatches[ch] : null;
                    if (want != null && DyeCatalog.Get(want) == null) { price.Refusal = $"no dye '{want}'"; return price; }
                    if (want == was[ch]) continue;
                    changed = true;
                    if (want != null) price.Dyes[want] = (price.Dyes.TryGetValue(want, out int n) ? n : 0) + 1;
                }
                if (changed) price.Charges++;
            }
            return price;
        }

        /// <summary>Applies <paramref name="changes"/> to <paramref name="items"/>, spending what they
        /// cost - the LOCAL store's half. Null on success, else why nothing was changed.</summary>
        public static string Apply(CharacterItems items, IReadOnlyList<PieceDye> changes)
        {
            var price = Cost(items, changes);
            if (price.Refusal != null) return price.Refusal;
            if (!price.Affordable(items)) return "not enough charges or dyes";
            items.Charges -= price.Charges;
            foreach (var kv in price.Dyes) items.AddDye(kv.Key, -kv.Value);
            foreach (var c in changes)
            {
                var design = Dyeable(c.Piece);
                var want = new string[design.DyeChannels.Length];
                for (int ch = 0; ch < want.Length; ch++)
                    want[ch] = ch < c.Swatches.Count && !string.IsNullOrEmpty(c.Swatches[ch]) ? c.Swatches[ch] : null;
                items.SetDyed(c.Piece, want);
            }
            return null;
        }

        /// <summary>
        /// The loadout to DRAW with this character's dyes on it: every slot whose design is dyed
        /// becomes its dyed copy (GearDye.DyedCopy, registered once under <c>design@dye...</c>). The
        /// drawing loadout only - stats never pass through here (Appearance.Resolve's rule).
        /// </summary>
        public static Loadout Dress(Loadout visual, CharacterItems items)
        {
            if (visual == null || items?.Dyed == null || items.Dyed.Count == 0) return visual;
            var dressed = new Loadout();
            foreach (var slot in GearSlots.Selectable)
            {
                string id = visual.Get(slot);
                var item = GearCatalog.Get(id);
                if (item != null && item.DyeChannels is { Length: > 0 })
                {
                    var swatches = items.SwatchesOn(DesignOf(item), item.DyeChannels.Length);
                    var copy = Registered(item, swatches);
                    if (copy != null) id = copy.ItemId;
                }
                dressed.Set(slot, id);
            }
            return dressed;
        }

        /// <summary>The design a worn item IS - a minted copy's design, else the item itself.</summary>
        public static string DesignOf(GearItem item)
            => item == null ? null : !string.IsNullOrEmpty(item.DesignId) ? item.DesignId : item.ItemId;

        /// <summary>The dyed copy of <paramref name="item"/>, built and registered once; null when
        /// nothing is dyed.</summary>
        public static GearItem Registered(GearItem item, string[] swatches)
        {
            bool any = false;
            foreach (var s in swatches) any |= !string.IsNullOrEmpty(s);
            if (!any) return null;
            // The id the copy will have, so a copy already built is found without rebuilding it.
            var sb = new System.Text.StringBuilder(item.ItemId).Append("@dye");
            for (int c = 0; c < item.DyeChannels.Length; c++)
            {
                var sw = c < swatches.Length ? DyeCatalog.Get(swatches[c]) : null;
                sb.Append('.').Append(sw != null ? sw.Id.Substring(4) : "-");
            }
            var known = GearCatalog.Get(sb.ToString());
            if (known != null) return known;
            var copy = GearDye.DyedCopy(item, swatches);
            if (copy == null || copy == item) return null;
            GearCatalog.Register(copy);
            return copy;
        }
    }
}
