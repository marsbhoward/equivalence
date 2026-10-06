using UnityEngine;
using Convergence.Core;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// Chooses which rig to build. An authored PSD-imported prefab wins; otherwise the
    /// procedural placeholder keeps the game playable.
    /// </summary>
    public static class CharacterRigFactory
    {
        /// <summary>The one weapon that carries the Rift's drifting shards. A single id rather
        /// than a flag on GearItem while exactly one item wants it - promote it to a field the
        /// moment a second one does, not before.</summary>
        const string RiftTouchedWeaponId = "rift_blade";

        /// <summary>The one weapon that carries a halo. Same "a single id until there are two"
        /// reasoning as RiftTouchedWeaponId.</summary>
        const string SaintWeaponId = "saint_blade";

        /// <summary>The one weapon whose wrap carries a trailing ribbon. Same "a single id until
        /// there are two" reasoning as RiftTouchedWeaponId.</summary>
        const string LumenWeaponId = "lumen_blade";
        const string ShadowWeaponId = "shadow_blade";
        const string ShadowRelicId = "shadow_relic";

        public static ICharacterRig Build(GameObject root, ElementType element, int sortingOrder = SortingOrders.Character)
        {
            var art = GameArt.I.ForElement(element);

            if (art?.RigPrefab != null)
            {
                var go = Object.Instantiate(art.RigPrefab, root.transform);
                go.name = "visual";
                go.transform.localPosition = Vector3.zero;

                var rig = go.GetComponent<SpriteLibraryCharacterRig>()
                          ?? go.AddComponent<SpriteLibraryCharacterRig>();
                rig.Initialise(sortingOrder);
                return rig;
            }

            return PrimitiveCharacterRig.Build(root, element, sortingOrder);
        }

        /// <summary>
        /// Paint a whole profile onto a rig: body first, then the gear that is actually VISIBLE,
        /// then the helmet preference.
        ///
        /// One helper because there are four rigs in the project that all have to agree - the
        /// arena player, the hub character, the loadout preview and the transmutation preview -
        /// and the ordering is not obvious. The helmet toggle has to come last because Apply
        /// knows nothing about it and would otherwise put the helm straight back on.
        ///
        /// The loadout handed to Apply is the RESOLVED one, so a transmogged slot draws the art
        /// the player chose while every stat still comes from what is really equipped. Nothing
        /// downstream of here can tell the difference, which is the point: transmog must never be
        /// able to leak into power.
        ///
        /// Tolerates a destroyed rig. A run teardown can leave one of these behind for a frame,
        /// and "fake null" means ?. does not save you - Transform is a real UnityEngine.Object,
        /// so != null uses Unity's own check.
        /// </summary>
        public static void Paint(ICharacterRig rig, Chain.CharacterProfile profile)
        {
            if (rig == null || rig.Transform == null || profile == null) return;

            var visual = profile.Look.Resolve(profile.Gear);

            rig.SetAppearance(profile.Look);
            rig.Apply(visual);
            rig.SetHelmHidden(profile.HelmHidden);

            // A disc character holds BOTH discs anywhere they are simply being looked at - the
            // hub, the character sheet, the transmutation circle, the couch. Only the arena has a
            // reason to merge them, where PlayerController drives the split from range every
            // frame and so overrides this the moment a run starts.
            //
            // Read from the RESOLVED loadout, not the equipped one: the split is purely visual,
            // and transmog exists precisely so appearance and stats can disagree. Discs worn over
            // a greatsword's stats still show as a pair.
            //
            // Set AFTER Apply, which is what puts the sprite on the main-hand layer the off-hand
            // copies from.
            var weapon = GearCatalog.Get(visual.Get(GearSlot.Weapon));
            // A pair holding two DIFFERENT halves (the Armillary) hands the off hand its own
            // picture first; null for every other disc, whose off hand copies the main.
            rig.SetOffhandPicture(weapon?.OffhandLayer?.Sprite, weapon?.OffhandMenuLayer?.Sprite,
                                  weapon?.OffhandLayer != null ? weapon.IdleFrames : null,
                                  weapon?.OffhandIdleFrames);
            rig.SetWeaponSplit(weapon != null && weapon.Class == WeaponClass.Disc);

            // SHADOW, WHOLE: the Shadow's look drawn AND its relic socketed, and the character
            // casts no shadow. Both halves, deliberately - the relic alone is the mechanic and the
            // blade alone is a skin; the two together are the set, and this is its tell. The drawn
            // weapon (a skin counts) but the SOCKETED relic (relics carry no transmog).
            var relic = profile.Gear.Get(GearSlot.Relic);
            rig.SetContactShadow(!(weapon != null && weapon.ItemId == ShadowWeaponId && relic == ShadowRelicId));

            // Prism's lit gem, for the current attunement - on EVERY paint, so a repaint never
            // puts the base sprite's fire gem back (see Attunement).
            ApplyAttunement(rig, weapon, Attunement.Current);

            // The hub/preview has no run-scoped ledger to read Extra Sigil's stack from, so it
            // shows the base rack only - the same reason a preview never shows a banked finisher.
            rig.SetVialCount(weapon != null && weapon.Class == WeaponClass.Bow ? 2 : 0);

            // RIFT-TOUCHED WEAPONS CARRY THE RIFT'S OWN DEBRIS. Read from the RESOLVED loadout -
            // the blade being DRAWN - not from what is equipped, which is the same split the heat
            // cycle's repaint lives by: this is a picture, and a picture follows the picture. Skin
            // Rift Blade over another sword and the shards come with it, exactly as a skinned
            // Emberline still heats.
            var anchor = rig.WeaponAnchor;
            if (anchor != null)
            {
                bool riftTouched = weapon != null && weapon.ItemId == RiftTouchedWeaponId;
                var shards = anchor.GetComponentInChildren<Rifts.RiftShards>(true);
                if (riftTouched)
                {
                    // Slid up the blade before it orbits - the anchor is the FIST, and shards
                    // centred there drift around the character's legs instead of the sword. The
                    // blade's middle is counted off the Rift's own grid (DemoGear.RiftShardsAlong);
                    // the reach is then kept under the blade's half-width plus a little, so they
                    // hug it rather than forming a halo round the player.
                    (shards ??= Rifts.RiftShards.Attach(anchor, 0.34f, 4, SortingOrders.Fx - 44,
                                                        along: DemoGear.RiftShardsAlong)).SetShown(true);
                }
                else shards?.SetShown(false);

                // Saint's halo, on the same anchor and by the same rule - a picture follows the
                // picture, so a skinned Saint brings its ring with it.
                // Attach ALWAYS, never `??=`. Attach is what re-takes the weapon renderer the
                // halo sorts against, and a repaint is exactly when that renderer can have been
                // replaced - so short-circuiting on an existing halo skips the one thing the call
                // is for and leaves the ring sorting against a renderer the rig no longer uses.
                bool saint = weapon != null && weapon.ItemId == SaintWeaponId;
                // The blade's real midpoint above the fist, counted off Saint's own grid (see
                // DemoGear.SaintHaloAlong) - a literal here went stale the moment the hilt moved.
                if (saint) SaintHalo.Attach(anchor, DemoGear.SaintHaloSize, rig.WeaponRenderer, along: DemoGear.SaintHaloAlong).SetShown(true);
                else anchor.GetComponentInChildren<SaintHalo>(true)?.SetShown(false);

                // Lumen's wrap-tail, on the same anchor and the same "picture follows the
                // picture" rule as the two above - a skinned Lumen brings its ribbon with it.
                // Attach ALWAYS, never `??=`, for the identical reason SaintHalo states just
                // above: the weapon renderer it sorts against can be replaced by a repaint.
                bool lumen = weapon != null && weapon.ItemId == LumenWeaponId;
                if (lumen) LumenRibbon.Attach(anchor, DemoGear.LumenWireFrames, rig.WeaponRenderer).SetShown(true);
                else anchor.GetComponentInChildren<LumenRibbon>(true)?.SetShown(false);

                // The Rift Disc's light-cycle wall, on BOTH discs of the pair - the off-hand copy
                // hangs from the back arm, found by name as the rig itself finds it.
                bool lightCycle = weapon != null && weapon.LightCycleTrail;
                Combat.LightCycleTrail.SetOn(anchor, rig.WeaponRenderer, lightCycle);
                var offhand = (rig as Component) != null
                    ? FindDeep(((Component)rig).transform, "WeaponOffhand") : null;
                if (offhand != null)
                    Combat.LightCycleTrail.SetOn(offhand, offhand.GetComponent<SpriteRenderer>(),
                                                 lightCycle && weapon.Class == WeaponClass.Disc);

                // A weapon that casts light (Singularity) lights the character from BOTH discs.
                HeldGlow.SetOn(anchor, rig.WeaponRenderer, weapon);
                if (offhand != null)
                    HeldGlow.SetOn(offhand, offhand.GetComponent<SpriteRenderer>(),
                                   weapon != null && weapon.Class == WeaponClass.Disc ? weapon : null);

                // Rai's arcs, off BOTH discs of the pair by the same rule.
                bool arcs = weapon != null && weapon.LightningArcs;
                Combat.LightningArcs.SetOn(anchor, rig.WeaponRenderer, arcs);
                if (offhand != null)
                    Combat.LightningArcs.SetOn(offhand, offhand.GetComponent<SpriteRenderer>(),
                                               arcs && weapon.Class == WeaponClass.Disc);
            }
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var hit = FindDeep(c, name);
                if (hit != null) return hit;
            }
            return null;
        }

        /// <summary>
        /// Light <paramref name="shown"/>'s gem for <paramref name="element"/> and hang the glow
        /// at it - or hide the glow for a weapon with no gems. The one place this is done: the
        /// in-run player, the hub, the character screen and every repaint all come through here.
        /// SetWeaponSprite FIRST and the anchor read after: SetWeaponSprite ensures the layers,
        /// the anchor's getter does not, and after a reload the anchor can read empty otherwise.
        /// </summary>
        public static void ApplyAttunement(ICharacterRig rig, GearItem shown, ElementType element)
        {
            if (rig == null) return;
            // The Herald pauldron's sigil, whatever weapon is drawn.
            rig.SyncAttunement(element);
            if (shown != null && shown.HasElementGems)
            {
                rig.SetWeaponSprite(shown.BladeFor(element, rig.DetailArt));
                var anchor = rig.WeaponAnchor;
                if (anchor != null)
                    PrismGlow.Attach(anchor, shown.GemAlong(element), ElementInfo.Tint(element), rig.WeaponRenderer)
                             .SetShown(true);
            }
            else rig.WeaponAnchor?.GetComponentInChildren<PrismGlow>(true)?.SetShown(false);
        }
    }
}
