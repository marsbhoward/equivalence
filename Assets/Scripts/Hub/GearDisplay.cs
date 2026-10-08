using UnityEngine;
using Convergence.Art;
using Convergence.Art.Gear;

namespace Convergence.Hub
{
    /// <summary>
    /// Shared by the armoury's two fixtures: how a piece of gear is drawn when it is being LOOKED
    /// AT rather than worn.
    ///
    /// Both stands are purely cosmetic - see WeaponRack and ArmourStand - so neither has any
    /// business knowing about loadouts, durability or stats. What they both need is the same two
    /// things: which sprite represents an item, and how to put it on screen at the size it really
    /// is.
    /// </summary>
    public static class GearDisplay
    {
        /// <summary>
        /// The layer that stands for an item.
        ///
        /// The TALLEST one, because a piece that paints a pair (both pauldrons, both boots) draws
        /// the same shape twice and either half is the whole idea - while a piece whose layers
        /// genuinely differ is one where the big one is what the player recognises.
        ///
        /// <paramref name="menu"/> is for a CLOSE-UP only (the rack's inspect page). The fixtures
        /// themselves stay on arena art: the hub camera has about 150 screen pixels per world unit
        /// at 1080p against menu art's 300 texels per unit, so hung in the room, half its texels
        /// would have no screen pixel to land on and the detail would shimmer instead of showing.
        /// </summary>
        public static LayerSprite Represent(GearItem item, bool menu = false)
        {
            if (item == null) return null;

            // A piece posed differently on display than in the hand (the Aether Dual Discs) hangs in
            // its display pose - see GearItem.DisplayLayer.
            var posed = item.DisplayFor(menu);
            if (posed != null) return posed;

            LayerSprite pick = null;
            foreach (var l in item.LayersFor(menu))
            {
                if (l == null || l.Sprite == null) continue;
                if (pick == null || l.Size.y > pick.Size.y) pick = l;
            }

            // A gemmed weapon shows the CURRENT element's gem lit, not the base sprite's fire one
            // (see Attunement). The variants share the layer's geometry, so only the sprite moves.
            var lit = Attunement.Blade(item, menu);
            if (pick != null && lit != null && pick.Layer == RigLayer.Weapon)
                pick = new LayerSprite { Layer = pick.Layer, Sprite = lit, Offset = pick.Offset,
                                         Size = pick.Size, Tint = pick.Tint };
            return pick;
        }

        /// <summary>The picture for a disc pair's SECOND disc: the item's own off-hand half where
        /// it has one (the Armillary), otherwise the same as <see cref="Represent"/>. A pair with a
        /// display pose (the Aether Dual Discs) hangs its second disc in that pose too.</summary>
        public static LayerSprite RepresentOffhand(GearItem item, bool menu = false)
            => item?.OffhandDisplayFor(menu) ?? item?.OffhandFor(menu) ?? Represent(item, menu);

        /// <summary>
        /// The scale that renders a layer at its declared world size - <c>Size / native</c>, which
        /// is exactly what PrimitiveCharacterRig applies and has to stay identical to it.
        ///
        /// ASSIGNING Size AS THE SCALE IS WRONG, and it is the trap this project already documents
        /// under "SetBody vs SetBodyPixel". It happens to work for a Spr sprite, because every one
        /// of those is 1x1 world units so scale and size coincide. Gear art is PPU-matched: a
        /// pixel sprite is ALREADY its own size, so multiplying by the size again shrinks it by
        /// roughly a factor of three. On the mannequin that came out as a giant wooden dummy
        /// wearing slivers of gold - the armour was a third of its real size while every number
        /// measured correct, because Size was right and only the way it was applied was not.
        /// </summary>
        public static Vector3 ScaleFor(LayerSprite layer, float fit = 1f)
        {
            if (layer == null || layer.Sprite == null) return Vector3.one;
            var native = layer.Sprite.bounds.size;
            return new Vector3(
                (native.x > 0.0001f ? layer.Size.x / native.x : layer.Size.x) * fit,
                (native.y > 0.0001f ? layer.Size.y / native.y : layer.Size.y) * fit,
                1f);
        }

        /// <summary>Draw one layer at its true world size.</summary>
        public static void Draw(SpriteRenderer target, LayerSprite layer, float fit = 1f)
        {
            if (target == null) return;
            if (layer == null || layer.Sprite == null)
            {
                target.sprite = null;
                return;
            }
            target.sprite = layer.Sprite;
            target.color = layer.Tint;
            target.transform.localScale = ScaleFor(layer, fit);
        }

        /// <summary>
        /// Turn on whatever effect a weapon carries of its own (or off, for anything else) on a
        /// renderer showing it. A display is where a weapon is LOOKED at - a Rift Blade that only
        /// shed light in the hand would read as inert on the stand, which is the one place the
        /// player is actually studying it. Shared by the rack and the armoury wall.
        /// </summary>
        public static void ApplyEffects(SpriteRenderer main, GearItem item)
        {
            if (main == null) return;

            var shards = main.transform.GetComponentInChildren<Rifts.RiftShards>(true);
            if (item != null && item.ItemId == "rift_blade")
                (shards ??= Rifts.RiftShards.Attach(main.transform, 0.5f, 4, main.sortingOrder + 1))
                    .SetShown(true);
            else shards?.SetShown(false);

            // Attached ALWAYS rather than `??=`, so the ring re-takes the renderer it sorts
            // against - see CharacterRigFactory for why skipping that call is the bug it looks
            // like a shortcut for.
            //
            // Both numbers are LOCAL, so both ride the display's own fit scale and any point-down
            // flip: the blade-midpoint distance runs up the blade whichever way it points, and the ring ends
            // up the same fraction of the blade wide here as it is in the hand.
            if (item != null && item.ItemId == "saint_blade")
                SaintHalo.Attach(main.transform, DemoGear.SaintHaloSize, main, along: DemoGear.SaintHaloAlong).SetShown(true);
            else main.transform.GetComponentInChildren<SaintHalo>(true)?.SetShown(false);

            // Prism's glow at its lit gem, as in the hand - the gem is the current attunement's.
            if (item != null && item.HasElementGems)
                PrismGlow.Attach(main.transform, item.GemAlong(Attunement.Current),
                                 Core.ElementInfo.Tint(Attunement.Current), main).SetShown(true);
            else main.transform.GetComponentInChildren<PrismGlow>(true)?.SetShown(false);

            if (item != null && item.ItemId == "lumen_blade")
                LumenRibbon.Attach(main.transform, DemoGear.LumenWireFrames, main).SetShown(true);
            else main.transform.GetComponentInChildren<LumenRibbon>(true)?.SetShown(false);

            // A weapon that casts light lights its display too.
            HeldGlow.SetOn(main.transform, main, item);

            // Rai's arcs, a greatsword's length off the disc on a display as in the hand.
            Combat.LightningArcs.SetOn(main.transform, main, item != null && item.LightningArcs);

            // Kindled marks (the Aether Greatsword) burn on a display as in the hand. Left on once there:
            // the overlay follows the renderer's sprite, so the next weapon hung with no marks - or
            // an unowned bay's silhouette - simply draws none.
            if (item != null && item.Kindled) KindledMarks.On(main);
        }

        /// <summary>Cap a piece to a display height, never magnifying a small one - a signet blown
        /// up to a greatsword's height is a picture of a ring rather than a ring.</summary>
        public static float FitScale(LayerSprite layer, float maxHeight)
            => layer == null ? 1f : Mathf.Min(maxHeight / Mathf.Max(0.001f, layer.Size.y), 1f);
    }
}
