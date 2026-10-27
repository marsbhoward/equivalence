using UnityEngine;

namespace Convergence.Hub
{
    /// <summary>
    /// The hub's THEMES (the user's group 8 picks, 2026-10-27, https://claude.ai/artifact/5w93SpEa9d7uSNoP8ZvK7U and its
    /// two later rounds): a theme is a room's floor and north wall plus every hub item drawn for it, and a hub
    /// DROP is a whole theme. Ids are PERMANENT and the list APPEND-ONLY - a character's
    /// <see cref="Chain.HubLook"/> saves the numbers, its owned themes are <see cref="Id"/> strings
    /// (<see cref="Chain.CharacterItems.Themes"/>). The Cloister is everyone's; the rest are earned.
    /// </summary>
    public enum HubTheme
    {
        Cloister = 0, Study = 1, Observatory = 2, Crypt = 3, Atrium = 4, Salt = 5,
        Terrace = 6, Sanctum = 7, Smithy = 8, Chapel = 9, Shore = 10, Volcano = 11, Dojo = 12, TechLab = 13,
    }

    /// <summary>What a theme dresses, each chosen apart (the crate's HUB tab picks a theme per item).
    /// PERMANENT ids - <see cref="Chain.HubLook.Items"/> is indexed by them. The sigil door is not
    /// themed (the user's call): it stays stone slabs in every room.</summary>
    public enum HubItem
    {
        Room = 0, Forge = 1, Terminal = 2, Booth = 3, Frames = 4, Shrine = 5, Table = 6, Couch = 7,
        Crate = 8, Rack = 9, Stand = 10, Selector = 11, Doorway = 12, Circle = 13,
    }

    public static class HubThemes
    {
        public const int Count = 14;
        public const int ItemCount = 14;

        /// <summary>The permanent string id a theme is owned by (designs.json, the service's item table).</summary>
        public static string Id(HubTheme t) => "hub." + t.ToString().ToLowerInvariant();

        public static bool TryParse(string id, out HubTheme t)
        {
            t = HubTheme.Cloister;
            if (string.IsNullOrEmpty(id) || !id.StartsWith("hub.")) return false;
            for (int i = 0; i < Count; i++) if (Id((HubTheme)i) == id) { t = (HubTheme)i; return true; }
            return false;
        }

        public static string NameOf(HubTheme t) => t switch
        {
            HubTheme.Study => "Alchemist's study",
            HubTheme.Observatory => "Observatory",
            HubTheme.Crypt => "Crypt",
            HubTheme.Atrium => "Garden atrium",
            HubTheme.Salt => "Salt chamber",
            HubTheme.Terrace => "Sky terrace",
            HubTheme.Sanctum => "Nigredo sanctum",
            HubTheme.Smithy => "Smithy",
            HubTheme.Chapel => "Rubedo chapel",
            HubTheme.Shore => "Seashore",
            HubTheme.Volcano => "Volcano throne",
            HubTheme.Dojo => "Dojo",
            HubTheme.TechLab => "Tech lab",
            _ => "Cloister",
        };

        public static string NameOf(HubItem i) => i switch
        {
            HubItem.Room => "The room",
            HubItem.Forge => "The forge",
            HubItem.Terminal => "The terminal",
            HubItem.Booth => "The photo booth",
            HubItem.Frames => "Trophy frames",
            HubItem.Shrine => "The manual shrine",
            HubItem.Table => "The mastery table",
            HubItem.Couch => "The couch",
            HubItem.Crate => "The crate",
            HubItem.Rack => "The weapon rack",
            HubItem.Stand => "The armour stand",
            HubItem.Selector => "The sigil selector",
            HubItem.Doorway => "The armoury doorway",
            HubItem.Circle => "The transmutation circle",
            _ => i.ToString(),
        };
    }

    /// <summary>The themes' materials, six steps light to dark, shared by the room and every item.</summary>
    public static class Pal
    {
        static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static Color32[] R(params string[] h) { var r = new Color32[h.Length]; for (int i = 0; i < h.Length; i++) r[i] = C(h[i]); return r; }
        public static Color32 Hex(string h) => C(h);

        public static readonly Color32 K = TexelCanvas.K;
        public static readonly Color32[] Oak = HubArt.Oak, DarkWood = HubArt.DarkWood, Stone = HubArt.Stone, Brass = HubArt.Brass,
                                         Iron = HubArt.Iron, Parch = HubArt.Parch, Gold = HubArt.Gold, Teal = HubArt.Teal;
        public static readonly Color32[] Velvet = R("#e07080", "#b83850", "#8a2238", "#601428", "#3c0a18", "#20040c");
        public static readonly Color32[] Obsid = R("#8a86a8", "#5a5674", "#3a3650", "#26223a", "#161426", "#0a0814");
        public static readonly Color32[] Navy = R("#7a90d0", "#4a5ea0", "#2e3c78", "#1e2856", "#121a38", "#080c1e");
        public static readonly Color32[] Terra = R("#f0a070", "#c87850", "#a05a3a", "#7a4028", "#502818", "#2a140a");
        public static readonly Color32[] Leaf = R("#c8f0a0", "#8cc75c", "#5a9a3c", "#3a6a28", "#22441a", "#10240c");
        public static readonly Color32[] Wicker = R("#f0d8a0", "#d0b070", "#a88a50", "#806638", "#584424", "#2c2212");
        public static readonly Color32[] SaltW = R("#ffffff", "#f0eee6", "#dcd8cc", "#b8b2a4", "#8a8476", "#4c4840");
        public static readonly Color32[] Pink = R("#fff0f0", "#f8c8c8", "#e09898", "#b86a70", "#7a4048", "#3a1c20");
        public static readonly Color32[] Bone = R("#f4efe2", "#d8cfb8", "#b0a68c", "#857c66", "#5a5344", "#2e2a22");
        public static readonly Color32[] CryptS = R("#8a8494", "#625c6c", "#46404e", "#322e3a", "#201e26", "#100e14");
        public static readonly Color32[] Rust = R("#9a7a6a", "#7a5a4a", "#5a4034", "#3e2c24", "#2a1c16", "#140c0a");
        public static readonly Color32[] Marble = R("#ffffff", "#eeeae2", "#d8d2c6", "#b4ac9e", "#888072", "#4c463c");
        public static readonly Color32[] SkyB = R("#e8f4ff", "#b8dcf8", "#7ab8e8", "#4a88c0", "#2a5888", "#14304c");
        public static readonly Color32[] Crim = R("#ff9aa0", "#d83a48", "#a81828", "#78101c", "#4c0810", "#260408");
        public static readonly Color32[] Brick = R("#e09070", "#b86448", "#904a34", "#683424", "#442216", "#22100a");
        public static readonly Color32[] RedX = R("#ffd0c8", "#ff6a5a", "#d02020", "#901010", "#500808", "#280404");
        public static readonly Color32[] Blacks = R("#5a5060", "#3a3240", "#2a2430", "#1c1820", "#121016", "#08060a");
        public static readonly Color32[] NB = R("#3a3036", "#2c2428", "#221c20", "#1a1418", "#120e10", "#080608");
        public static readonly Color32[] Sand = R("#fff4d8", "#f0dca8", "#d8bc80", "#b0945c", "#806a40", "#4a3c24");
        public static readonly Color32[] Sea = R("#e0fbff", "#8ee8f0", "#3cc0d8", "#1e88b0", "#105878", "#082c40");
        public static readonly Color32[] Drift = R("#e8e2d8", "#c8beb0", "#a0968a", "#787068", "#504a44", "#2a2622");
        public static readonly Color32[] Coral = R("#ffd8d0", "#ff9a88", "#e06a5a", "#b04a40", "#7a2a24", "#401410");
        public static readonly Color32[] Tatami = R("#f0ecc0", "#d8d098", "#b8b070", "#8a8450", "#5c5834", "#2e2c1a");
        public static readonly Color32[] Lacq = R("#ff8a70", "#d83a28", "#a8201a", "#781410", "#4c0a08", "#260404");
        public static readonly Color32[] BlackLacq = R("#4a3a38", "#3a2a28", "#2a1c1a", "#1e1412", "#140c0a", "#0a0606");
        public static readonly Color32[] Beige = R("#fffcec", "#ece6cc", "#d4ccac", "#aaa286", "#7c7660", "#46423a");
        public static readonly Color32[] Crt = R("#e8ffe8", "#8cff8c", "#30d040", "#18902a", "#0c5018", "#062a0c");
        public static readonly Color32[] Plast = R("#d8dce4", "#b0b6c0", "#888e9a", "#626874", "#40444e", "#22242a");
        public static readonly Color32[] Verd = R("#c8f0e0", "#80c8b0", "#4a9a88", "#2e6a5e", "#1a4038", "#0c201c");
        public static readonly Color32[] Steel = R("#f0f3f7", "#c4cad3", "#8e97a6", "#5d6573", "#373c46", "#1c1f25");
        public static readonly Color32[] Yellow = R("#fffbe0", "#ffe680", "#f2c230", "#c08a10", "#7a5208", "#3a2604");
        public static readonly Color32[] Violet = R("#f6eeff", "#d2b8ff", "#a07ff2", "#6c50d0", "#3e2b8a", "#1e1450");
        public static readonly Color32[] ColdViolet = R("#e8d8ff", "#b89cf0", "#7a5ad0", "#4e3a98", "#2a1e5a", "#140e2e");
        public static readonly Color32[] Straw = R("#e8d498", "#c8b070", "#a89058", "#806a3a", "#5a4826", "#2e2412");
        public static readonly Color32[] Paper = R("#fcfaf2", "#f4f0e4", "#e0dac8", "#bcb49c", "#8a846e", "#4a4638");
        public static readonly Color32 White = new(255, 255, 255, 255);
        public static readonly Color32 Void = new(5, 4, 6, 255);
    }
}
