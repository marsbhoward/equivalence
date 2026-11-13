using System.Collections.Generic;
using Convergence.Chain;

namespace Convergence.Hub
{
    /// <summary>
    /// The frames' art with the BOOTH SHOTS put first (Chain.BoothShots; the user's call, 2026-11-13): one
    /// group per character holding any, ahead of the wallet's collections, then whatever the wallet source
    /// gives. What <see cref="Showcase.Source"/> hands out - the wallet source itself is
    /// <see cref="Showcase.Wallet"/>, and may be null; the shots are there either way.
    /// </summary>
    public class BoothShowcase : IShowcaseSource
    {
        public IShowcaseSource Wallet;

        public const string GroupPrefix = "booth.";

        /// <summary>The group a booth shot's key belongs to - its character's.</summary>
        public static string GroupOf(string key)
        {
            var rest = key.Substring(BoothShots.KeyPrefix.Length);
            int colon = rest.LastIndexOf(':');
            return GroupPrefix + (colon < 0 ? rest : rest.Substring(0, colon));
        }

        public int Count => BoothShots.All.Count + (Wallet?.Count ?? 0);

        public IReadOnlyList<ShowcaseGroup> Groups
        {
            get
            {
                var groups = new List<ShowcaseGroup>();
                var seen = new List<string>();
                foreach (var s in BoothShots.All)
                {
                    if (seen.Contains(s.ProfileId)) continue;
                    seen.Add(s.ProfileId);
                    groups.Add(new ShowcaseGroup
                    {
                        Id = GroupPrefix + s.ProfileId,
                        Name = string.IsNullOrEmpty(s.Name) ? "Booth" : $"Booth · {s.Name}",
                        Count = BoothShots.CountFor(s.ProfileId),
                    });
                }
                if (Wallet != null) groups.AddRange(Wallet.Groups);
                return groups;
            }
        }

        public void Loaded(string group, List<ShowcaseItem> into)
        {
            if (group != null && group.StartsWith(GroupPrefix))
            {
                into.Clear();
                foreach (var s in BoothShots.Of(group.Substring(GroupPrefix.Length))) into.Add(Item(s));
                return;
            }
            if (Wallet != null) Wallet.Loaded(group, into);
            else into.Clear();
        }

        static ShowcaseItem Item(BoothShots.Shot s) => new()
        {
            Key = s.Key,
            Art = s.Art,
            Title = string.IsNullOrEmpty(s.Name) ? "A portrait" : s.Name,
            Collection = "Portrait booth",
        };

        static bool Ours(string key) => key != null && key.StartsWith(BoothShots.KeyPrefix);
        static bool OurGroup(string group) => group != null && group.StartsWith(GroupPrefix);

        public bool HasMore(string group) => !OurGroup(group) && (Wallet?.HasMore(group) ?? false);
        public bool IsLoading(string group) => !OurGroup(group) && (Wallet?.IsLoading(group) ?? false);
        public void LoadMore(string group) { if (!OurGroup(group)) Wallet?.LoadMore(group); }

        public bool TryGetByKey(string key, out ShowcaseItem item)
        {
            if (Ours(key))
            {
                bool found = BoothShots.TryGet(key, out var s);
                item = found ? Item(s) : default;
                return found;
            }
            item = default;
            return Wallet != null && Wallet.TryGetByKey(key, out item);
        }

        public void Want(IEnumerable<string> keys)
        {
            if (Wallet == null) return;
            var theirs = new List<string>();
            foreach (var k in keys) if (!Ours(k)) theirs.Add(k);
            if (theirs.Count > 0) Wallet.Want(theirs);
        }

        public bool Failed(string key) => !Ours(key) && (Wallet?.Failed(key) ?? false);
        public void Retry(string key) { if (!Ours(key)) Wallet?.Retry(key); }
    }
}
