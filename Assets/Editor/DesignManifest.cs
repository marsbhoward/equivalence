using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Chain;

namespace Convergence.EditorTools
{
    /// <summary>
    /// Writes the service's copy of the design pools - <c>web/service/data/designs.json</c> - from
    /// <see cref="DesignDrops"/> itself, so the server rolls a box from exactly the pools the game
    /// would. The server rolls because a client that named its own design would mint any Black
    /// Diamond it liked. Re-export (and redeploy) whenever a design is added or renamed:
    ///
    ///     unity command eval --code 'return Convergence.EditorTools.DesignManifest.Export();'
    ///
    /// Also lists every ART KEY those payouts need, so the operator can pre-pin them all
    /// (<see cref="ArtKeys"/>, rendered by <see cref="NftArt"/>) - a client never uploads art.
    /// </summary>
    public static class DesignManifest
    {
        public const string DefaultPath = "web/service/data/designs.json";

        static readonly LootTier[] Tiers = { LootTier.Diamond, LootTier.BlackDiamond };

        public static string Export(string path = DefaultPath)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"version\": 1,\n");
            sb.Append($"  \"exportedAt\": \"{System.DateTime.UtcNow:O}\",\n");
            sb.Append($"  \"costs\": {{ \"random\": {Core.Tuning.GearRoll.ForgeRandomRedeemBoxCost}, " +
                      $"\"targeted\": {Core.Tuning.GearRoll.ForgeTargetedRedeemBoxCost} }},\n");
            sb.Append("  \"tiers\": {\n");

            int total = 0;
            for (int t = 0; t < Tiers.Length; t++)
            {
                // One entry per DESIGN: a whole Diamond armour SET is one (ArmourSets), with its pieces.
                var pool = DesignDrops.Designs(Tiers[t]);
                sb.Append($"    \"{Tiers[t]}\": [\n");
                for (int i = 0; i < pool.Count; i++)
                {
                    var d = pool[i];
                    if (d.IsSet)
                    {
                        sb.Append($"      {{ \"id\": {Q(d.Id)}, \"name\": {Q(d.Name)}, \"slot\": \"Set\", \"pieces\": [");
                        for (int p = 0; p < d.Pieces.Count; p++)
                            sb.Append(p > 0 ? ", " : " ").Append("{ ").Append(Piece(d.Pieces[p])).Append(" }");
                        sb.Append(" ]");
                    }
                    else
                    {
                        var relic = DesignDrops.RelicFor(d.Item);
                        sb.Append("      { ").Append(Piece(d.Item));
                        if (relic != null) sb.Append(", \"relic\": { ").Append(Piece(relic)).Append(" }");
                    }
                    sb.Append(i < pool.Count - 1 ? " },\n" : " }\n");
                    total++;
                }
                sb.Append("    ],\n");
            }
            sb.Length -= 2;   // the last tier's comma
            sb.Append("\n  },\n");

            // The cosmetic economy's catalogues (web/service/src/cosmetics.ts reads them): every dye
            // colour, every dyeable design with its channel count, every rolled pose. Ids are permanent.
            sb.Append("  \"swatches\": [\n");
            for (int i = 0; i < DyeCatalog.All.Length; i++)
            {
                var sw = DyeCatalog.All[i];
                sb.Append($"    {{ \"id\": {Q(sw.Id)}, \"name\": {Q(sw.Name)}, \"rarity\": {Q(sw.Rarity.ToString())} }}")
                  .Append(i < DyeCatalog.All.Length - 1 ? ",\n" : "\n");
            }
            sb.Append("  ],\n  \"dyeable\": {\n");
            var dyeable = new List<GearItem>();
            foreach (var item in GearCatalog.All.Values)
                if (item.DyeChannels is { Length: > 0 } && string.IsNullOrEmpty(item.StackKey) &&
                    string.IsNullOrEmpty(item.DesignId) && !GearCatalog.IsDyedCopy(item)) dyeable.Add(item);
            dyeable.Sort((a, b) => string.CompareOrdinal(a.ItemId, b.ItemId));
            for (int i = 0; i < dyeable.Count; i++)
                sb.Append($"    {Q(dyeable[i].ItemId)}: {dyeable[i].DyeChannels.Length}").Append(i < dyeable.Count - 1 ? ",\n" : "\n");
            sb.Append("  },\n  \"poses\": [\n");
            for (int i = 0; i < PoseCatalog.All.Length; i++)
                sb.Append($"    {{ \"id\": {Q(PoseCatalog.All[i].Id)}, \"name\": {Q(PoseCatalog.All[i].Name)} }}")
                  .Append(i < PoseCatalog.All.Length - 1 ? ",\n" : "\n");
            // The hub themes, every one but the Cloister (everyone's) - on the poses' roll (group 7).
            sb.Append("  ],\n  \"themes\": [\n");
            for (int i = 1; i < Hub.HubThemes.Count; i++)
                sb.Append($"    {{ \"id\": {Q(Hub.HubThemes.Id((Hub.HubTheme)i))}, \"name\": {Q(Hub.HubThemes.NameOf((Hub.HubTheme)i))} }}")
                  .Append(i < Hub.HubThemes.Count - 1 ? ",\n" : "\n");
            sb.Append("  ]\n}\n");

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, sb.ToString());
            return $"{total} design(s) -> {path}";
        }

        /// <summary>Every art key a box payout can mint - designs and their relics - comma-separated
        /// for <see cref="NftArt.RenderMany"/>.</summary>
        public static string ArtKeys()
        {
            var keys = new List<string>();
            foreach (var tier in Tiers)
            foreach (var d in DesignDrops.Designs(tier))
            {
                keys.Add(d.Id);   // a set's key is its design id (set_<prefix>) - one picture per set
                if (d.IsSet) continue;
                var relic = DesignDrops.RelicFor(d.Item);
                if (relic != null) keys.Add(relic.ItemId);
            }
            return string.Join(",", keys);
        }

        static string Piece(GearItem d)
        {
            var sb = new StringBuilder();
            sb.Append($"\"id\": {Q(d.ItemId)}, \"name\": {Q(d.DisplayName)}, \"slot\": {Q(d.Slot.ToString())}");
            if (d.Slot == GearSlot.Weapon) sb.Append($", \"class\": {Q(d.Class.ToString())}");
            // A relic's finisher is the relic; on the weapon it only pairs the two (power 0).
            if (d.Slot == GearSlot.Relic && !string.IsNullOrEmpty(d.SignatureFinisher))
                sb.Append($", \"signature\": {Q(d.SignatureFinisher)}");
            return sb.ToString();
        }

        static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
