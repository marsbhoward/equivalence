using System.Collections.Generic;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// A Diamond armour SET is ONE design and ONE token (the user's call, 2026-10-13;
    /// MONETIZATION.md "A Diamond armour set is one design"): a box pays out the whole set, owning
    /// it owns every piece, and its token pictures the set. The game still wears, mixes and dyes the
    /// pieces one by one - only the payout and the trade are per set.
    ///
    /// A set is every AUTHORED Diamond armour piece sharing an id prefix (the part before the first
    /// '_' - blacksteel_helm, blacksteel_plate ...), at least two of them. That is how every set is
    /// already authored (DemoGear's AddX per set, Dyeable by prefix), so naming a new set needs no
    /// list here. A piece alone under its prefix (the Hellspawn Cape, the Wraith's Eye) is its own
    /// design, as a weapon is.
    ///
    /// A set's DESIGN ID is <c>set_&lt;prefix&gt;</c> - also its art key, so it passes the service's
    /// art-key rule as it is.
    /// </summary>
    public static class ArmourSets
    {
        public const string Prefix = "set_";

        static IReadOnlyDictionary<string, GearItem> _builtFrom;
        static Dictionary<string, List<GearItem>> _pieces;
        static Dictionary<string, string> _setOf;

        /// <summary>Whether <paramref name="item"/> can belong to a set at all: authored, Diamond,
        /// worn armour (never a weapon, relic or piece of jewellery).</summary>
        static bool Candidate(GearItem item)
            => item != null && item.Tier == LootTier.Diamond &&
               string.IsNullOrEmpty(item.StackKey) && string.IsNullOrEmpty(item.DesignId) &&
               item.ItemId.IndexOf('@') < 0 && !item.ForgeOnly &&
               item.Slot is not (GearSlot.Weapon or GearSlot.Relic or GearSlot.Ring or GearSlot.Neck or GearSlot.Trinket);

        static void Build()
        {
            var all = GearCatalog.All;
            if (_pieces != null && ReferenceEquals(all, _builtFrom)) return;
            _builtFrom = all;
            var groups = new Dictionary<string, List<GearItem>>();
            foreach (var item in all.Values)
            {
                if (!Candidate(item)) continue;
                int cut = item.ItemId.IndexOf('_');
                if (cut <= 0) continue;
                string prefix = item.ItemId.Substring(0, cut);
                if (!groups.TryGetValue(prefix, out var list)) groups[prefix] = list = new List<GearItem>();
                list.Add(item);
            }
            _pieces = new Dictionary<string, List<GearItem>>();
            _setOf = new Dictionary<string, string>();
            foreach (var kv in groups)
            {
                if (kv.Value.Count < 2) continue;
                kv.Value.Sort((a, b) => a.Slot != b.Slot ? a.Slot.CompareTo(b.Slot) : string.CompareOrdinal(a.ItemId, b.ItemId));
                string id = Prefix + kv.Key;
                _pieces[id] = kv.Value;
                foreach (var piece in kv.Value) _setOf[piece.ItemId] = id;
            }
        }

        /// <summary>The set a piece belongs to (its design id), or null when it is not in one.</summary>
        public static string SetOf(GearItem item)
        {
            if (item == null) return null;
            Build();
            string id = !string.IsNullOrEmpty(item.DesignId) ? item.DesignId : item.ItemId;
            return _setOf.TryGetValue(id, out var set) ? set : null;
        }

        public static bool IsSet(string designId) => designId != null && designId.StartsWith(Prefix, System.StringComparison.Ordinal);

        /// <summary>The pieces of a set, in slot order - empty for an unknown id.</summary>
        public static IReadOnlyList<GearItem> Pieces(string setId)
        {
            Build();
            return setId != null && _pieces.TryGetValue(setId, out var list) ? list : System.Array.Empty<GearItem>();
        }

        /// <summary>Every set, sorted by id.</summary>
        public static List<string> All()
        {
            Build();
            var ids = new List<string>(_pieces.Keys);
            ids.Sort(string.CompareOrdinal);
            return ids;
        }

        /// <summary>
        /// A set's name: the words its pieces' names share ("Black Steel Helm", "Black Steel Cuirass"
        /// -> "Black Steel"), then "Set". Falls back to the prefix when the names share nothing.
        /// </summary>
        public static string Name(string setId)
        {
            var pieces = Pieces(setId);
            if (pieces.Count == 0) return setId;
            var words = pieces[0].DisplayName.Split(' ');
            int shared = words.Length;
            foreach (var p in pieces)
            {
                var other = p.DisplayName.Split(' ');
                int n = 0;
                while (n < shared && n < other.Length && other[n] == words[n]) n++;
                shared = n;
            }
            // Never the whole of a piece's name - "Diamond Plate" and "Diamond Plate" would leave nothing.
            if (shared >= words.Length) shared = words.Length - 1;
            string stem = shared > 0 ? string.Join(" ", words, 0, shared)
                                     : char.ToUpperInvariant(setId[Prefix.Length]) + setId.Substring(Prefix.Length + 1);
            return stem + " Set";
        }
    }
}
