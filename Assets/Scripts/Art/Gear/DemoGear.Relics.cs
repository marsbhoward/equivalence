using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== the standard relics: looks a rolled relic wears
    //
    // The user's picks of 2026-10-31 (mock rounds 2-6, ART-NOTES "Relics on the hip"). A Bronze,
    // Silver or Gold relic is a rolled weapon art; these are the PICTURES it can wear, paired with
    // the art at random (the user's call: every look carries any class's rollable art). A minted
    // relic names one as its Design (Chain.MintedGearRecord), the way a Diamond piece wears its
    // design - so the look is the catalogue's and the art, class and tier are the record's.
    //
    // FORGE-ONLY, in GearItem's sense: never held bare. A player holds a look only through a relic
    // wearing it (GearOwnership), and no design box ever rolls one (DesignDrops skips ForgeOnly).
    //
    // All hung through HipRelic, at the pouch's density, around its size (8-13 wide, 12-14 tall).
    // The Wayfarer's Pouch (leather_pouch, DemoGear.cs) is a look as well; it stays the starter.
    //
    //   relic_hoop       Courier's Hoop      a bronze ring blade on a leather tie
    //   relic_book       Alchemist's Book    oxblood cover, gold corners and sign, the page edge
    //   relic_vial       Elixir Vial         water with light bubbles (transparent ones read as holes)
    //   relic_sprig      Bottled Sprig       a green sprig in soil, a corked jar
    //   relic_hourglass  Hourglass           bronze plates, glass, the sand falling
    //   relic_mortar     Mortar and Pestle   pale stone bowl, wooden pestle
    //   relic_fork       Tuning Fork         steel, a leather wrap on the stem
    //   relic_lodestone  Lodestone           dark stone, nails standing off it
    //   relic_salt       Salt Phial          glass, white crystals, cork
    //   relic_sulphur    Sulphur Tin         iron, the sulphur sign in yellow
    //   relic_mercury    Mercury Flask       a round flask, a silver pool
    //   relic_key        Iron Key            light iron
    public static partial class DemoGear
    {
        /// <summary>Every look a rolled relic may wear - the Pouch first, then this file's.</summary>
        public static readonly string[] StandardRelicIds =
        {
            "leather_pouch", "relic_hoop", "relic_book", "relic_vial", "relic_sprig", "relic_hourglass",
            "relic_mortar", "relic_fork", "relic_lodestone", "relic_salt", "relic_sulphur",
            "relic_mercury", "relic_key",
        };

        public static bool IsStandardRelic(string id)
            => !string.IsNullOrEmpty(id) && System.Array.IndexOf(StandardRelicIds, id) >= 0;

        /// <summary>The look for a seed - a roll's own seed (RunLoot) or a stable hash of an id, never a
        /// fresh draw, so a seeded floor's stream is not moved by the pick.</summary>
        public static string StandardRelicFor(int seed)
            => StandardRelicIds[(int)((uint)(seed * 0x9E3779B1) % (uint)StandardRelicIds.Length)];

        /// <summary>A stable hash of a string (FNV-1a) - string.GetHashCode is not promised to agree
        /// across sessions.</summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in s ?? "") { h ^= c; h *= 16777619; }
                return (int)h;
            }
        }

        static Palette.Ramp Tone(float r, float g, float b) => new(new Color(r, g, b));

        static void AddStandardRelics(List<GearItem> items)
        {
            var leather = Palette.Leather;
            var bronze = Palette.Bronze;
            var iron = Palette.Iron;
            var ironLt = Tone(0.60f, 0.60f, 0.64f);
            var glass = Tone(0.62f, 0.80f, 0.86f);
            var gold = Tone(0.90f, 0.72f, 0.30f);
            var cream = Tone(0.94f, 0.90f, 0.80f);

            void Look(string id, string name, string[] rows, Dictionary<char, Color> pal)
            {
                var item = Make(id, name, GearSlot.Relic, LootTier.Silver, 0f, HipRelic("gear.relic." + id, rows, pal));
                item.ForgeOnly = true;
                items.Add(item);
            }

            Look("relic_hoop", "Courier's Hoop", new[]
            {
                "....SBBS....",
                "....SBBS....",
                "...hllllb...",
                "..hlbbbbbd..",
                ".hlb....bdd.",
                "hlb......bds",
                "lb........ds",
                "lb........ds",
                "lb........ds",
                "bd.......dds",
                ".bd.....dds.",
                "..ddddddss..",
                "...ssssss...",
            }, Palette.Of(bronze, leather));

            Look("relic_book", "Alchemist's Book", new[]
            {
                "....dd....",
                "....dd....",
                "Hhllllllb5",
                "Lhbbbbbbd5",
                "lbbbLLbbd5",
                "lbbLBBLbd5",
                "lbLBBBBLd5",
                "lbbLBBLbd5",
                "lbbbLLbbd5",
                "lbbbbbbbd5",
                "Ldddddddd4",
                "Ssssssss44",
            }, Palette.Of(Tone(0.50f, 0.17f, 0.15f), gold, cream));

            Look("relic_vial", "Elixir Vial", new[]
            {
                "..4444..",
                "..3333..",
                "..lbbd..",
                ".hl..bd.",
                ".l....d.",
                ".lhhhhd.",
                ".lHLLLd.",
                ".lLhLBd.",
                ".lLLLBd.",
                ".lLLhBd.",
                ".lLLBBd.",
                "..LBBd..",
                "...dd...",
            }, Palette.Of(glass, Tone(0.30f, 0.58f, 0.90f), leather));

            Look("relic_sprig", "Bottled Sprig", new[]
            {
                "...4444...",
                "...3333...",
                "..lbbbbd..",
                ".l..H...d.",
                "l..HLB..bd",
                "l.HL.BS.bd",
                "l..LBS..bd",
                "l...BS..bd",
                "l...S...bd",
                "l1111111bd",
                ".b22222bd.",
                "..dddddd..",
            }, Palette.Of(glass, Tone(0.40f, 0.70f, 0.32f), Tone(0.38f, 0.26f, 0.18f)));

            Look("relic_hourglass", "Hourglass", new[]
            {
                "....ss....",
                "hllllllllb",
                "bddddddddd",
                ".BL5555LB.",
                ".BL5555LB.",
                "..BL55LB..",
                "...B55B...",
                "...B55B...",
                "..BL..LB..",
                ".BL.55.LB.",
                ".BL5555LB.",
                "hllllllllb",
                "bddddddddd",
            }, Palette.Of(bronze, glass, Tone(0.92f, 0.80f, 0.52f)));

            Look("relic_mortar", "Mortar and Pestle", new[]
            {
                ".......HH.",
                "......HLB.",
                ".....HLB..",
                "....HLB...",
                "...HLB....",
                "hllLBllllb",
                "bddSSddddd",
                "lbbbbbbbbd",
                ".lbbbbbbd.",
                "..lbbbbd..",
                "...dddd...",
                "..lbbbdd..",
            }, Palette.Of(Tone(0.72f, 0.70f, 0.66f), Tone(0.62f, 0.44f, 0.28f)));

            Look("relic_fork", "Tuning Fork", new[]
            {
                "..hl..hl..",
                "..hb..lb..",
                "..lb..lb..",
                "..lb..lb..",
                "..lb..bd..",
                "..lb..bd..",
                "..lbbbbd..",
                "...lbbd...",
                "....bd....",
                "...SBBS...",
                "....bd....",
                "....bd....",
                "...lbbd...",
                "....dd....",
            }, Palette.Of(Tone(0.78f, 0.80f, 0.86f), leather));

            Look("relic_lodestone", "Lodestone", new[]
            {
                "....HH....HL.",
                "....LB...HL..",
                "....LBhl.LB..",
                "...hllllbbd..",
                "..hlllllbbbd.",
                "HLhllllbbbbd.",
                "HBlllbbbbbdd.",
                "..lllbbbbddLH",
                "..lbbbbbdddBH",
                "...bbbbddds..",
                "....bdLBss...",
                "......LB.....",
                "......HH.....",
            }, Palette.Of(Tone(0.44f, 0.40f, 0.48f), Tone(0.72f, 0.72f, 0.76f)));

            Look("relic_salt", "Salt Phial", new[]
            {
                "...SBBS...",
                "...SBBS...",
                "...lbbd...",
                "..hlbbbd..",
                ".hl5555bd.",
                "hl566665bd",
                "hl566665bd",
                "l55555554d",
                "l44444443d",
                "b44444443s",
                ".bdddddds.",
                "..ssssss..",
            }, Palette.Of(glass, leather, Tone(0.93f, 0.92f, 0.88f)));

            Look("relic_sulphur", "Sulphur Tin", new[]
            {
                "....SS....",
                ".llllllll.",
                "hlllllllld",
                "dddddddddd",
                "bbbbLLbbbd",
                "bbbLBBLbbd",
                "bbLBBBBLbd",
                "bbbbBBbbbd",
                "sbbBBBBbbs",
                "sbbbBBbbbs",
                "sbbbbbbbbs",
                ".ssssssss.",
            }, Palette.Of(iron, Tone(0.92f, 0.78f, 0.20f)));

            Look("relic_mercury", "Mercury Flask", new[]
            {
                "....44....",
                "....33....",
                "....lb....",
                "...lbbd...",
                "..lb..bd..",
                ".lb....bd.",
                "lb......bd",
                "lHHLLLLLBd",
                "lLLBBBBBSd",
                ".bLBBBBSd.",
                "..bdSSdd..",
                "...dddd...",
            }, Palette.Of(glass, Tone(0.80f, 0.82f, 0.88f), leather));

            Look("relic_key", "Iron Key", new[]
            {
                "..hlllb..",
                ".hlbbbbd.",
                "hlb...bds",
                "lb.....ds",
                "hlb...bds",
                ".lbbbbdd.",
                "...lbd...",
                "...lbd...",
                "...lbdbb.",
                "...lbdds.",
                "...lbd...",
                "...lbdbb.",
                "...lbddd.",
                "....ss...",
            }, Palette.Of(ironLt));
        }
    }
}
