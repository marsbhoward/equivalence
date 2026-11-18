using System.Collections.Generic;
using Convergence.Art.Gear;

namespace Convergence.Chain
{
    /// <summary>
    /// What a QUARTZ box holds (GEAR.md "Quartz") - the game's copy of the service's rule
    /// (web/service/src/cosmetics.ts rollQuartz), for a LOCAL save; a service player's box is rolled
    /// by the service. One draw, every entry equally likely: the Quartz pieces (capes and accessories -
    /// a repeat still counts) and the EARNED poses and hub themes the character LACKS (a repeat of those
    /// would be empty, and the open poses are everyone's). Quartz is off chain: nothing here mints.
    /// </summary>
    public static class QuartzDrops
    {
        /// <summary>Every id a box may give a character holding <paramref name="items"/>.</summary>
        public static List<string> Entries(CharacterItems items)
        {
            var list = new List<string>();
            foreach (var piece in DesignDrops.Pool(LootTier.Quartz)) list.Add(piece.ItemId);
            foreach (var pose in PoseCatalog.All)
                if (System.Array.IndexOf(Core.Tuning.Cosmetics.OpenPoses, pose.Id) < 0 &&
                    (items?.Poses == null || !items.Poses.Contains(pose.Id))) list.Add(pose.Id);
            for (int i = 1; i < Hub.HubThemes.Count; i++)
            {
                string id = Hub.HubThemes.Id((Hub.HubTheme)i);
                if (items?.Themes == null || !items.Themes.Contains(id)) list.Add(id);
            }
            return list;
        }

        public static bool IsTheme(string id) => id != null && id.StartsWith("hub.", System.StringComparison.Ordinal);
        public static bool IsPose(string id) => id != null && id.StartsWith("pose.", System.StringComparison.Ordinal);

        /// <summary>Credits <paramref name="id"/> to <paramref name="items"/> in its own list.</summary>
        public static void Credit(CharacterItems items, string id)
        {
            if (IsTheme(id)) { if (!items.Themes.Contains(id)) items.Themes.Add(id); }
            else if (IsPose(id)) { if (!items.Poses.Contains(id)) items.Poses.Add(id); }
            else items.AddQuartz(id, 1);
        }
    }
}
