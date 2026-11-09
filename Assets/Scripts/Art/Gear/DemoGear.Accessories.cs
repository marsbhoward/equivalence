using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// THE ACCESSORIES (the user's eight picks from twenty, 2026-11-08): pieces worn in the ACCESSORY slot
    /// (GearSlot.Ring) over any armour, for individuality - not tied to one spot on the body the way the
    /// ring and the Wraith's Eye are. They paint no layers of their own: the rig draws each live from its
    /// <see cref="GearItem.Accessory"/> (<see cref="Accessories"/>, PrimitiveCharacterRig.Accessory.cs).
    /// Each keeps a still picture for cards and its NFT (<see cref="GearItem.DisplayLayer"/>), drawn the
    /// same way facing the camera. Diamond, power 0, like the Wraith's Eye: cosmetic collectibles.
    /// </summary>
    public static partial class DemoGear
    {
        static void AddAccessories(List<GearItem> items)
        {
            items.Add(Accessory("bandolier", "Bandolier", AccessoryKind.Bandolier));
            items.Add(Accessory("vial_harness", "Vial Harness", AccessoryKind.VialHarness));
            items.Add(Accessory("feather_collar", "Feather Collar", AccessoryKind.FeatherCollar));
            items.Add(Accessory("banner_pole", "Banner Pole", AccessoryKind.BannerPole));
            items.Add(Accessory("hourglass_pack", "Hourglass Pack", AccessoryKind.HourglassPack));
            items.Add(Accessory("chained_tome", "Chained Tome", AccessoryKind.ChainedTome));
            items.Add(Accessory("wrist_wraps", "Wrist Wraps", AccessoryKind.WristWraps));
            items.Add(Accessory("orbiting_stones", "Orbiting Stones", AccessoryKind.OrbitingStones));

            // The mock's variants, each its own design as the scarves are: the collar's three other birds
            // (the Four Birds' - see the bow set), the banner's and the wraps' other cloths, and wraps in
            // the wearer's own element.
            items.Add(Accessory("swan_feather_collar", "Swan Feather Collar", AccessoryKind.FeatherCollar, "Swan"));
            items.Add(Accessory("peacock_feather_collar", "Peacock Feather Collar", AccessoryKind.FeatherCollar, "Peacock"));
            items.Add(Accessory("phoenix_feather_collar", "Phoenix Feather Collar", AccessoryKind.FeatherCollar, "Phoenix"));
            items.Add(Accessory("linen_banner_pole", "Linen Banner Pole", AccessoryKind.BannerPole, "Linen"));
            items.Add(Accessory("ink_banner_pole", "Ink Banner Pole", AccessoryKind.BannerPole, "Ink"));
            items.Add(Accessory("navy_banner_pole", "Navy Banner Pole", AccessoryKind.BannerPole, "Navy"));
            items.Add(Accessory("linen_wrist_wraps", "Linen Wrist Wraps", AccessoryKind.WristWraps, "Linen"));
            items.Add(Accessory("ink_wrist_wraps", "Ink Wrist Wraps", AccessoryKind.WristWraps, "Ink"));
            items.Add(Accessory("navy_wrist_wraps", "Navy Wrist Wraps", AccessoryKind.WristWraps, "Navy"));
            items.Add(Accessory("element_wrist_wraps", "Elemental Wrist Wraps", AccessoryKind.WristWraps, "Element"));

            // The ARM PIECES (the user's eleven, 2026-11-09 - ArmPieces.cs): each replaces the whole weapon arm.
            items.Add(Accessory("salamander_arm", "Salamander Arm", AccessoryKind.SalamanderArm));
            items.Add(Accessory("quicksilver_arm", "Quicksilver Arm", AccessoryKind.QuicksilverArm));
            items.Add(Accessory("salt_arm", "Salt Arm", AccessoryKind.SaltArm));
            items.Add(Accessory("crucible_arm", "Crucible Arm", AccessoryKind.CrucibleArm));
            items.Add(Accessory("verdigris_arm", "Verdigris Arm", AccessoryKind.VerdigrisArm));
            items.Add(Accessory("rubedo_arm", "Rubedo Arm", AccessoryKind.RubedoArm));
            items.Add(Accessory("sanguis_arm", "Sanguis Arm", AccessoryKind.SanguisArm));
            items.Add(Accessory("fulmen_arm", "Fulmen Arm", AccessoryKind.FulmenArm));
            items.Add(Accessory("penumbra_arm", "Penumbra Arm", AccessoryKind.PenumbraArm));
            items.Add(Accessory("homunculus_plated_arm", "Homunculus Arm", AccessoryKind.HomunculusPlatedArm));
            items.Add(Accessory("homunculus_hydraulic_arm", "Hydraulic Homunculus Arm", AccessoryKind.HomunculusHydraulicArm));
        }

        static GearItem Accessory(string id, string name, AccessoryKind kind, string look = null)
        {
            var item = Make(id, name, GearSlot.Ring, LootTier.Diamond, 0f);
            item.Accessory = kind;
            item.AccessoryLook = look;
            var sprite = AccessoryPicture(kind, look, "gear.accessory." + id + ".display");
            if (sprite != null)
                item.DisplayLayer = new LayerSprite { Layer = RigLayer.Accessory, Sprite = sprite, Size = sprite.bounds.size };
            return item;
        }

        /// <summary>The piece facing the camera, still: full phials, the sand a third run, both pictures
        /// in one (the stones' far side under their near side, as on the body).</summary>
        static Sprite AccessoryPicture(AccessoryKind kind, string look, string key)
        {
            _accessoryPictures ??= new Dictionary<string, Sprite>();
            if (_accessoryPictures.TryGetValue(key, out var cached) && cached != null) return cached;
            List<TexelKit.Layer> a = new(), b = new();
            if (ArmPieces.Is(kind)) ArmPieces.Draw(kind, new ArmPieceState { T = 0.6f }, a);   // the whole arm, at rest
            else
            {
                var st = new AccessoryState { T = 0.6f, Run = 0.35f, Look = look };
                Accessories.Draw(kind, st, away: false, a, b);
                a.AddRange(b);
            }
            var grid = TexelKit.Compose(a);
            if (grid.Count == 0) return null;

            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (int k in grid.Keys)
            {
                int x = TexelKit.KX(k), y = TexelKit.KY(k);
                x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y);
            }
            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            w += w % 2; h += h % 2;
            var px = new Color32[w * h];
            foreach (var kv in grid)
                px[(y0 + h - 1 - TexelKit.KY(kv.Key)) * w + (TexelKit.KX(kv.Key) - x0)] = kv.Value;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply(false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), BodyPpu, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return _accessoryPictures[key] = sprite;
        }

        /// <summary>Built once each; non-readonly so a domain reload's empty copy rebuilds them.</summary>
        static Dictionary<string, Sprite> _accessoryPictures;
    }
}
