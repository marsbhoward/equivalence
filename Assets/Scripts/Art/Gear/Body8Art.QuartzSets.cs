using System.Collections.Generic;

namespace Convergence.Art.Gear
{
    // The QUARTZ back pieces' eight-direction pictures (GEAR.md "Quartz", stage 4): which baked set each
    // design wears. Baked by Tools/tier-art/regen8q.sh from quartz8.js into Body8Art.Quartz.cs (generated -
    // "q_" + the mock's id). A design not listed draws bare in eight directions until it is baked.
    public static partial class Body8Art
    {
        static readonly Dictionary<string, string> QuartzSets = new()
        {
            ["nocturne_cape"] = "q_nocturne",
            ["tepes_cape"] = "q_tepes",
            ["hellspawn_cape"] = "q_hellspawn",
            ["orichalc_coat"] = "q_orichalc",
            ["talon_coat"] = "q_talon",
            ["pilgrim_mantle"] = "q_pilgrim",
            ["war_panels"] = "q_panels",
            ["regent_mantle"] = "q_regent",
            ["slung_shield"] = "q_shield",
            ["bone_wings"] = "q_wings",
            ["crossed_katanas"] = "q_katanas",
            ["peaked_hood"] = "q_peaked",
            ["dagged_hood"] = "q_dagged",
            // the older hooded pieces (their four-view art unchanged; these are their eight-direction bakes)
            ["black_hood"] = "q_blackhood",
            ["wanderers_hood"] = "q_wanderer",
            ["wraith_cloak"] = "q_wraith",
        };

        /// <summary>The baked Quartz set a design (or a dyed copy's design) wears, or null.</summary>
        static string QuartzSetOf(string designId)
            => designId != null && QuartzSets.TryGetValue(designId, out var set) ? set : null;
    }
}
