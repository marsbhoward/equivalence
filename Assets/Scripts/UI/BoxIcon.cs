using UnityEngine;
using UnityEngine.UI;
using Convergence.Art;
using Convergence.Art.Gear;

namespace Convergence.UI
{
    /// <summary>
    /// A loot box drawn on flat UI - the same Art.BoxArt picture the pickup and the wallet token
    /// show, so a box looks like one thing everywhere it is counted.
    ///
    /// Drawn at its NATIVE size in canvas units (the canvas is 1920x1080 reference, so at 1080p a
    /// texel lands on one screen pixel): MENU density for a card big enough to look at, IN-GAME
    /// density for a row-sized badge. Never stretched to fit a rect - a box squeezed to an
    /// arbitrary height is off its grid.
    /// </summary>
    public static class BoxIcon
    {
        public static BoxArt.Kind KindOf(LootTier tier) => tier switch
        {
            LootTier.Bronze => BoxArt.Kind.Bronze,
            LootTier.Silver => BoxArt.Kind.Silver,
            LootTier.Gold => BoxArt.Kind.Gold,
            LootTier.Diamond => BoxArt.Kind.Diamond,
            _ => BoxArt.Kind.BlackDiamond,
        };

        /// <summary>A closed box with its FEET at <paramref name="anchor"/> + <paramref name="feet"/>
        /// (canvas units), centred horizontally there.</summary>
        public static Image Add(Transform parent, BoxArt.Kind kind, bool menu,
                                Vector2 anchor, Vector2 feet, bool open = false)
        {
            var sprite = open ? BoxArt.Open(kind, menu) : BoxArt.Closed(kind, menu);
            float w = sprite.rect.width, h = sprite.rect.height;
            var rt = UiKit.Rect(parent, $"box {kind}", anchor, anchor,
                new Vector2(feet.x - w * 0.5f, feet.y), new Vector2(feet.x + w * 0.5f, feet.y + h));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }
    }
}
