using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== the tier looks: what rolled armour wears
    //
    // A Bronze, Silver or Gold piece is a stat roll with no art of its own (Chain.MintedGearRecord);
    // these are the PICTURES it wears. The user's approved sets (2026-10-31): three per tier, eight
    // pieces each, and a greatsword, a disc pair and a bow per set (DemoGear.Tier<Tier>.cs, generated
    // by Tools/tier-art/regen.sh).
    //
    // PER PIECE, HASHED (the user's call, 2026-11-02): each rolled piece wears the set its own
    // instance id hashes to, so a drop is any of its tier's sets and the same piece always looks the
    // same. Derived, never stored - a record written before the looks existed wears one too, and
    // nothing new is saved or written to the chain.
    //
    // FORGE-ONLY, the standard relics' rule: never held bare, never rolled by a design box.
    public static partial class DemoGear
    {
        /// <summary>The sets of each tier, in the mock's order. A tier with none wears nothing.</summary>
        static readonly Dictionary<LootTier, string[]> TierSets = new()
        {
            [LootTier.Bronze] = new[] { "hoplite", "wayfarer", "athanor" },
            [LootTier.Silver] = new[] { "hauberk", "kettle", "sentinel" },
            [LootTier.Gold] = new[] { "sol", "cataphract", "wingward" },
        };

        /// <summary>
        /// The look a rolled piece of <paramref name="slot"/> and <paramref name="tier"/> wears, for a
        /// stable <paramref name="seed"/> (StableHash of its instance id) - a weapon's of its own
        /// <paramref name="weaponClass"/>. Null when its tier has no looks yet, or for a relic or a
        /// piece of jewellery (relics wear the standard looks, DemoGear.Relics).
        /// </summary>
        public static string TierLookFor(GearSlot slot, LootTier tier, WeaponClass weaponClass, int seed)
        {
            if (!TierSets.TryGetValue(tier, out var sets) || sets.Length == 0) return null;
            string piece = slot switch
            {
                GearSlot.Head or GearSlot.Shoulders or GearSlot.Torso or GearSlot.Gloves or
                GearSlot.Belt or GearSlot.Legs or GearSlot.Boots or GearSlot.Back => slot.ToString().ToLowerInvariant(),
                GearSlot.Weapon => weaponClass switch
                {
                    WeaponClass.Disc => "disc",
                    WeaponClass.Bow => "bow",
                    _ => "greatsword",
                },
                _ => null,
            };
            if (piece == null) return null;
            string set = sets[(int)((uint)(seed * 0x9E3779B1) % (uint)sets.Length)];
            string id = $"tier_{set}_{piece}";
            return GearCatalog.Get(id) != null ? id : null;
        }

        public static bool IsTierLook(string id) => !string.IsNullOrEmpty(id) && id.StartsWith("tier_");

        static void AddTierLooks(List<GearItem> items)
        {
            AddTierBronze(items);
            AddTierSilver(items);
            AddTierGold(items);
        }

        /// <summary>One look: an authored piece only a rolled record wears.</summary>
        static GearItem Look(string id, string name, GearSlot slot, LootTier tier, params LayerSprite[] layers)
        {
            var item = Make(id, name, slot, tier, 0f, layers);
            item.ForgeOnly = true;
            return item;
        }

        /// <summary>
        /// A helm with its view from BEHIND (GearItem.HelmBack): the same silhouette with the face
        /// opening closed, baked from the front (bake.js backOf) - without one the rig switched the
        /// helm off whenever the wearer turned away. <paramref name="seals"/> for one that wraps the
        /// jaw (a coif, a hood): no hair escapes it.
        /// </summary>
        static GearItem Helm(GearItem item, LayerSprite back, bool seals)
        {
            item.HelmBack = back;
            item.SealsHead = seals;
            return item;
        }

        /// <summary>A back piece that hangs still - a bedroll, a pack - rather than swinging as a cape.</summary>
        static GearItem Still(GearItem item)
        {
            item.CapeSwingScale = 0f;
            return item;
        }

        /// <summary>A baked grid: <paramref name="pal"/> is "aRRGGBB bRRGGBB ...", the offset in
        /// layout cells from the layer's joint, the grid at the body's density.</summary>
        static LayerSprite Grid(RigLayer layer, string key, float offX, float offY, string pal, params string[] rows)
            => Pixels(layer, key, rows, ParseBakedPalette(pal), offX, offY, ppu: BodyPpu);

        /// <summary>
        /// <see cref="Grid"/> with no outline: for straps on <see cref="RigLayer.BackOver"/>, which
        /// sorts OVER the pauldrons (it is the drape's layer) - a strap's 4-texel ring ate the inner
        /// side of every pauldron, where the mock lays the strap under it.
        /// </summary>
        static LayerSprite GridBare(RigLayer layer, string key, float offX, float offY, string pal, params string[] rows)
            => Pixels(layer, key, rows, ParseBakedPalette(pal), offX, offY, ppu: BodyPpu, outline: false);

        // ---- the weapons: a greatsword, a disc pair and a bow per set (the user's approval of
        // 2026-10-31, tier-weapons round 2). Grids at weapon density, doubled to 150 as every
        // weapon's are; the pivot is the grip cell in the authored grid (x across, y down).

        static GearItem TierSword(string id, string name, LootTier tier, int gripX, int gripY, string pal, params string[] rows)
            => Look(id, name, GearSlot.Weapon, tier,
                Pixels(RigLayer.Weapon, id, rows, ParseBakedPalette(pal), 0f, -10f,
                       pivotTexel: new Vector2Int(gripX, gripY), ppu: FinePpu, upscale2x: true));

        static GearItem TierDisc(string id, string name, LootTier tier, int gripX, int gripY, string pal, params string[] rows)
            => Disc(Look(id, name, GearSlot.Weapon, tier,
                Pixels(RigLayer.Weapon, id, rows, ParseBakedPalette(pal), DiscHandX, DiscHandY,
                       pivotTexel: new Vector2Int(gripX, gripY), ppu: FinePpu, upscale2x: true)));

        /// <summary>A bow, strung and unstrung on one grid and palette; the nock is the string's top
        /// cell (x across, y down), its bottom mirrored (Strung).</summary>
        static GearItem TierBow(string id, string name, LootTier tier, int gripX, int gripY, float nockX, float nockY,
                                string stringHex, string pal, string[] strung, string[] unstrung)
        {
            var colours = ParseBakedPalette(pal);
            var grip = new Vector2Int(gripX, gripY);
            var bow = Bow(Look(id, name, GearSlot.Weapon, tier,
                Pixels(RigLayer.Weapon, id, strung, colours, 0f, 0f, pivotTexel: grip, ppu: FinePpu, upscale2x: true)));
            ColorUtility.TryParseHtmlString(stringHex, out var stringColour);
            Strung(bow, Pixels(RigLayer.Weapon, id + ".unstrung", unstrung, colours, 0f, 0f,
                               pivotTexel: grip, ppu: FinePpu, upscale2x: true),
                   grip, new Vector2(nockX, nockY), stringColour);
            return bow;
        }

        static Dictionary<char, Color> ParseBakedPalette(string pal)
        {
            var map = new Dictionary<char, Color>();
            foreach (var entry in pal.Split(' '))
                if (entry.Length == 7 && ColorUtility.TryParseHtmlString("#" + entry.Substring(1), out var c))
                    map[entry[0]] = c;
            return map;
        }
    }
}
