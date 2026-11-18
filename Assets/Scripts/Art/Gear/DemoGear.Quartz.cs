using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Quartz: single worn cosmetics
    //
    // The user's calls, 2026-11-17 (GEAR.md "Quartz"): every CAPE and back piece and every ACCESSORY
    // is Quartz - account-only, never minted, never traded. Diamond keeps the full armour sets (now
    // without their capes) and the weapons; Black Diamond is untouched.
    //
    // Done as ONE pass over the finished catalogue rather than in each file's Make call, so a set's
    // cape is written where its set is drawn and still lands here. A cape leaving Diamond leaves its
    // set too: ArmourSets only gathers Diamond pieces.
    //
    // NOT moved: the tier backs (tier_*_back, the looks rolled Bronze/Silver/Gold backs wear) - they
    // are stat drops, not cosmetics.
    public static partial class DemoGear
    {
        /// <summary>
        /// Capes folded into another by the back-piece review (2026-11-16): the Vermilion Cape into
        /// the Nocturne Cape, the Prismatic Cloak into the Sentinel Cape. Kept in the catalogue, so a
        /// save that names one still draws it, but ForgeOnly - never held bare, never in a box.
        /// </summary>
        static readonly string[] MergedCapes = { "vermilion_cape", "diamond_cloak" };

        /// <summary>
        /// The QUARTZ DRAFTS kept by the back-piece review (2026-11-16/17) that exist only as eight-direction art
        /// (Tools/tier-art/quartz8.js, baked "q_" sets): each gets its item here, its card and four-view picture
        /// derived from that same bake (the back view, every piece merged by depth) so the two cannot drift, and
        /// one dye channel per material on the exact tones it was drawn in (GearDye matches them tone for tone).
        /// </summary>
        static void AddQuartzDrafts(List<GearItem> items)
        {
            GearItem Draft(string id, string name, string set, params DyeChannel[] channels)
            {
                var layer = BakedBackLayer(set, "gear.back.quartz." + id);
                if (layer == null) return null;
                var item = Make(id, name, GearSlot.Back, LootTier.Quartz, 0f, layer);
                item.DyeChannels = channels;
                item.CapeSwingScale = 0.45f;
                items.Add(item);
                return item;
            }
            static DyeChannel Ch(string name, DyeMaterial m, string hex)
                => DyeChannel.Of(name, m, new Palette.Ramp(ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta));
            Draft("pilgrim_mantle", "Pilgrim's Mantle", "q_pilgrim", Ch("Cloth", DyeMaterial.Cloth, "#5C6C80"));
            Draft("war_panels", "War Panels", "q_panels", Ch("Leather", DyeMaterial.Leather, "#703A2C"), Ch("Iron", DyeMaterial.Metal, "#96969E"));
            Draft("regent_mantle", "Regent's Mantle", "q_regent", Ch("Cloth", DyeMaterial.Cloth, "#4E2E70"), Ch("Lining", DyeMaterial.Cloth, "#C4B296"));
            // The rigid ones hang still: no swing on the card or in the four views.
            foreach (var rigid in new[]
            {
                Draft("slung_shield", "Slung Shield", "q_shield", Ch("Wood", DyeMaterial.Leather, "#7A5432"),
                    Ch("Iron", DyeMaterial.Metal, "#80828A"), Ch("Strap", DyeMaterial.Leather, "#563824")),
                Draft("bone_wings", "Bone Wings", "q_wings", Ch("Bone", DyeMaterial.Leather, "#D6CCB4")),
                Draft("crossed_katanas", "Crossed Katanas", "q_katanas", Ch("Lacquer", DyeMaterial.Leather, "#2C2830"),
                    Ch("Wrap", DyeMaterial.Cloth, "#D6D0C4"), Ch("Guard", DyeMaterial.Metal, "#C4A052"), Ch("Cord", DyeMaterial.Cloth, "#AA2C28")),
            })
                if (rigid != null) rigid.CapeSwingScale = 0f;
            Draft("peaked_hood", "Peaked Hood", "q_peaked", Ch("Cloth", DyeMaterial.Cloth, "#486321"));
            Draft("dagged_hood", "Dagged Hood", "q_dagged", Ch("Cloth", DyeMaterial.Cloth, "#B08434"));
        }

        /// <summary>A baked set's BACK view as one Back layer: every piece's back view, the nearest texel winning,
        /// at body density - placed so its shoulders sit where a hand-drawn cape's do (the eight-direction figure is
        /// ~55 texels taller below the shoulders than the torso frame's cells).</summary>
        static LayerSprite BakedBackLayer(string set, string key)
        {
            const int back = 4;
            var colour = new Dictionary<Vector2Int, (Color c, int z)>();
            // the back pieces, then a hood (its set's Head piece, baked without depth) over them, as the rig draws it
            var pieces = new List<Body8Art.Piece>(Body8Art.Of(set, GearSlot.Back));
            pieces.AddRange(Body8Art.Of(set, GearSlot.Head));
            foreach (var piece in pieces)
            {
                var v = piece.Views != null && piece.Views.Length > back ? piece.Views[back] : null;
                if (v == null || piece.Palette == null) continue;
                for (int r = 0; r < v.H; r++)
                for (int x = 0; x < v.W; x++)
                {
                    char ch = v.Cols[r * v.W + x];
                    if (ch == '.' || !piece.Palette.TryGetValue(ch, out var c)) continue;
                    int z = v.Depth != null ? v.Depth[r * v.W + x] : 9999;
                    var at = new Vector2Int(v.X0 + x, Body8Art.Top - v.Y0 - r);
                    if (!colour.TryGetValue(at, out var was) || z > was.z) colour[at] = (c, z);
                }
            }
            if (colour.Count == 0) return null;
            int x0 = int.MaxValue, x1 = int.MinValue, y0 = int.MaxValue, y1 = int.MinValue;
            foreach (var k in colour.Keys) { x0 = Mathf.Min(x0, k.x); x1 = Mathf.Max(x1, k.x); y0 = Mathf.Min(y0, k.y); y1 = Mathf.Max(y1, k.y); }
            // Mirrored: the back view is seen from behind, the four-view Back layer from the front.
            const string chars = "abcdefghijmnopqrtuvwxyzABCDEFGIJMNOPQRTUVWXYZ";
            var pal = new Dictionary<char, Color>();
            var of = new Dictionary<Color, char>();
            var rows = new string[y1 - y0 + 1];
            for (int y = y1; y >= y0; y--)
            {
                var sb = new System.Text.StringBuilder();
                for (int x = x1; x >= x0; x--)
                {
                    if (!colour.TryGetValue(new Vector2Int(x, y), out var e)) { sb.Append('.'); continue; }
                    if (!of.TryGetValue(e.c, out char ch)) { ch = chars[Mathf.Min(of.Count, chars.Length - 1)]; of[e.c] = ch; pal[ch] = e.c; }
                    sb.Append(ch);
                }
                rows[y1 - y] = sb.ToString();
            }
            float centre = (y0 + y1) / 2f - 55f;
            return Pixels(RigLayer.Back, key, rows, pal, 0f, centre, ppu: BodyPpu);
        }

        static void QuartzRetier(List<GearItem> items)
        {
            foreach (var item in items)
            {
                if (item == null || !string.IsNullOrEmpty(item.StackKey) || !string.IsNullOrEmpty(item.DesignId)) continue;
                if (IsTierLook(item.ItemId)) continue;
                bool cape = item.Slot == GearSlot.Back;
                bool accessory = item.Slot == GearSlot.Ring && item.Tier == LootTier.Diamond;
                if (!cape && !accessory) continue;
                item.Tier = LootTier.Quartz;
                if (System.Array.IndexOf(MergedCapes, item.ItemId) >= 0) item.ForgeOnly = true;
                // Quartz capes recolour FREE, without dyes (the user's call) - FreeDye's own rule.
                if (cape) _freeBacks.Add(item.ItemId);
            }
        }
    }
}
