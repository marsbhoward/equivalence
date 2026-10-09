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
                var pool = DesignDrops.Pool(Tiers[t]);
                sb.Append($"    \"{Tiers[t]}\": [\n");
                for (int i = 0; i < pool.Count; i++)
                {
                    var d = pool[i];
                    var relic = DesignDrops.RelicFor(d);
                    sb.Append("      { ").Append(Piece(d));
                    if (relic != null) sb.Append(", \"relic\": { ").Append(Piece(relic)).Append(" }");
                    sb.Append(i < pool.Count - 1 ? " },\n" : " }\n");
                    total++;
                }
                sb.Append(t < Tiers.Length - 1 ? "    ],\n" : "    ]\n");
            }
            sb.Append("  }\n}\n");

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
            foreach (var d in DesignDrops.Pool(tier))
            {
                keys.Add(d.ItemId);
                var relic = DesignDrops.RelicFor(d);
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
