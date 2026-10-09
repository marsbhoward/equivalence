using System.Collections.Generic;
using UnityEngine;
using Convergence.Art;
using Convergence.Art.Gear;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// The other half of the armoury: a wooden mannequin wearing whatever armour the player has
    /// put on show.
    ///
    /// PURELY COSMETIC, exactly as WeaponRack is - it reads no loadout, grants nothing and wears
    /// down never. What it shows is what the player chose to show, including a piece they happen
    /// to be wearing.
    ///
    /// IT IS A STRIPPED RIG, NOT A PICTURE OF ONE, and that is the whole reason the armour sits
    /// correctly. Gear art is authored as LayerSprites whose Offsets are relative to a specific
    /// joint - a cuirass to the torso, a pauldron to a shoulder, a helm to the neck - so the only
    /// way to lay a full set out without re-deriving twelve offsets by eye is to rebuild the joints
    /// they were authored against. The pivot tree below is PrimitiveCharacterRig's own, at the same
    /// texel positions, and <see cref="PivotFor"/> is its PivotFor. A piece therefore hangs on the
    /// mannequin exactly where it hangs on the character, for free, and stays correct when a new
    /// piece is authored.
    ///
    /// WHAT IT IS NOT is the rig itself. A real rig carries a walk cycle, a cape spring, aim
    /// tracking, a face and twenty-two layers of paper doll - and a mannequin that blinked at you
    /// would be a second character standing in the room rather than furniture. The body here is
    /// six wooden blocks on the body layers, so the gear layers above them stack exactly as they
    /// do on a person while the thing underneath reads as a dummy: no face, no hands, one tone.
    ///
    /// THE HEAD IS A BLANK OVAL rather than absent. A headless post was the first instinct and it
    /// is wrong for a mechanical reason as well as a visual one - helms are authored at offset
    /// (0, 24) from the neck joint, so with nothing to cap they hang in mid-air above the
    /// shoulders. A featureless oval is also just what an armourer's dummy looks like.
    ///
    /// HOLDS NO PROFILE STATE - item ids and sprites, repointed by an explicit
    /// <c>HubRoom.RefreshArmoury()</c>, for the documented domain-reload reason.
    /// </summary>
    public class ArmourStand : MonoBehaviour
    {
        /// <summary>The layout key each slot saves under - "armourstand:Torso".</summary>
        public const string KeyPrefix = "armourstand:";
        public static string SlotKey(GearSlot slot) => KeyPrefix + slot;

        public Vector2 Anchor { get; private set; }
        public int Shown { get; private set; }

        /// <summary>What is on the stand, for the prompt - slot name to item name. NOT a
        /// Dictionary field on a MonoBehaviour by accident: it is rebuilt wholesale by
        /// <see cref="Set"/> and null-guarded, the documented shape for a collection here.</summary>
        readonly Dictionary<GearSlot, string> _names = new();

        Transform _hips, _torso, _armBack, _armFront, _legBack, _legFront, _head;
        readonly Dictionary<RigLayer, SpriteRenderer> _layers = new();
        SpriteRenderer _shadow, _base, _plinth, _glow;
        float _lit;
        bool _focused;

        /// <summary>Footprint for placement legality and for the walk-into collider.</summary>
        public const float Radius = 0.55f;

        /// <summary>Sorting base for the mannequin's own layers. Its own band, well clear of the
        /// character's, so a stand and the player standing in front of it never interleave limbs -
        /// the same reason DepthStride has to exceed the rig's layer count.</summary>
        const int LayerBase = 4;

        /// <summary>Where the dummy's soles are, in the stand's own space: hips at -18 texels, legs
        /// hanging 16 below them (DrawDummy). The scaled figure is pinned here.</summary>
        static readonly float FeetY = PixelSprite.Px(0f, -34f).y;

        static readonly Color Wood = new(0.44f, 0.33f, 0.22f);
        static readonly Color WoodDark = new(0.31f, 0.23f, 0.15f);
        static readonly Color WoodLit = new(0.53f, 0.40f, 0.27f);
        static readonly Color Stone = new(0.26f, 0.25f, 0.31f);

        public static ArmourStand Build(Transform parent, Vector2 centre)
        {
            var go = new GameObject("armour-stand");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            var s = go.AddComponent<ArmourStand>();
            s.Anchor = centre;

            s._shadow = Quad(go.transform, "shadow", 0.95f, 0.36f,
                             new Color(0f, 0f, 0f, 0.32f), 0, new Vector2(0f, -0.50f), Spr.Circle);

            // The mannequin is drawn at the same scale the hub avatar is (ArenaVisualScale), or a
            // set on the stand reads smaller than the same set on the player standing beside it.
            // One scaled child holds the joint tree, pinned at the FEET (a child at p scaled by k
            // maps its point f to p + k*f, so p = f*(1 - k) leaves f where it was) - the dummy
            // still stands on the plinth. Plinth, shadow and collider stay at 1, so the footprint
            // and placement rules do not move.
            float k = Tuning.Player.ArenaVisualScale;
            var figure = new GameObject("figure").transform;
            figure.SetParent(go.transform, false);
            figure.localPosition = new Vector3(0f, FeetY * (1f - k), 0f);
            figure.localScale = new Vector3(k, k, 1f);

            s._glow = Quad(figure, "glow", 1.5f, 1.8f,
                           new Color(1f, 0.92f, 0.72f, 0f), 1, new Vector2(0f, 0.05f), Spr.Glow);

            // The plinth it stands on, so it reads as a stand rather than as a person who is not
            // moving. Same stone the trophy plinths use, for the same job.
            s._plinth = Quad(go.transform, "plinth", 0.78f, 0.30f, Stone, 2, new Vector2(0f, -0.46f), Spr.Circle);
            s._base = Quad(go.transform, "base", 0.62f, 0.16f, Color.Lerp(Stone, Color.white, 0.14f),
                           3, new Vector2(0f, -0.41f), Spr.Circle);

            // PrimitiveCharacterRig's own joint tree, at its own texel positions - see the header.
            // (Doubled alongside the rig's own pivots when LayoutUnit moved 37.5 -> 75.)
            s._hips = Pivot("hips", PixelSprite.Px(0f, -18f), figure);
            s._legBack = Pivot("leg.back", PixelSprite.Px(-6f, 0f), s._hips);
            s._legFront = Pivot("leg.front", PixelSprite.Px(6f, 0f), s._hips);
            s._torso = Pivot("torso", PixelSprite.Px(0f, -18f), figure);
            s._armBack = Pivot("arm.back", PixelSprite.Px(12f, 18f), s._torso);
            s._armFront = Pivot("arm.front", PixelSprite.Px(-12f, 18f), s._torso);
            s._head = Pivot("head", PixelSprite.Px(0f, 20f), s._torso);

            foreach (var layer in RigLayers.All) s.MakeLayer(layer);
            s.DrawDummy();

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = Radius * 0.55f;
            col.offset = new Vector2(0f, -0.42f);

            // Sorted from the plinth, which is where the stand touches the floor. Everything under
            // the root goes in, so the whole mannequin re-bases as one piece.
            var all = new List<SpriteRenderer> { s._shadow, s._glow, s._plinth, s._base };
            foreach (var sr in s._layers.Values) all.Add(sr);
            DepthSorted.Attach(go, -0.42f, isFixed: false, all.ToArray());

            s.Apply(0f);
            return s;
        }

        public void SetFocus(bool on) => _focused = on;

        /// <summary>
        /// Dress one slot. A null or unknown id strips it.
        ///
        /// Every layer the item declares is painted, not just the tallest - a mannequin is the one
        /// place a piece is seen as it is actually WORN, so a pair of pauldrons must be a pair.
        /// That is the opposite of the weapon rack's rule, and deliberately: a peg shows one
        /// object, a mannequin shows a set.
        /// </summary>
        public void Set(GearSlot slot, string itemId)
        {
            var item = string.IsNullOrEmpty(itemId) ? null : GearCatalog.Get(itemId);

            // Clear this slot's own layers first, or swapping a piece for a smaller one leaves the
            // old sprite behind on any layer the new one does not happen to paint.
            foreach (var layer in LayersOf(slot)) Hide(layer);

            // Remembered so a new cape can re-dye the cloth already on the stand (GearItem.DyedByBack).
            // Non-readonly and null-guarded: an array of plain strings, the shape that survives a
            // domain reload intact.
            if (_worn == null || _worn.Length != GearSlots.All.Length) _worn = new string[GearSlots.All.Length];
            _worn[(int)slot] = item != null ? item.ItemId : null;
            bool dyed = ClothDye.TryMassOf(GearCatalog.Get(_worn[(int)GearSlot.Back]), out var dye);

            if (item != null)
            {
                foreach (var l in item.LayersFor(menu: false))
                {
                    if (l == null || l.Sprite == null) continue;
                    if (RigLayers.IsBody(l.Layer)) continue;   // gear may never paint a body layer
                    if (!_layers.TryGetValue(l.Layer, out var sr)) continue;
                    sr.enabled = true;
                    // Bare skin on the piece shows the mannequin's own wood (GearItem.ShowsSkin).
                    sr.sprite = item.ShowsSkin ? BodyLook.Reskin(l.Sprite, new Palette.Ramp(Wood), "wood") : l.Sprite;
                    if (item.DyedByBack && dyed) sr.sprite = ClothDye.Dye(sr.sprite, dye);
                    sr.color = l.Tint;
                    sr.transform.localPosition = l.Offset;
                    sr.transform.localScale = GearDisplay.ScaleFor(l);
                    // Kindled marks burn on the stand as on the wearer, in the attuned element -
                    // the overlay follows the layer, so it goes quiet when the piece comes off.
                    if (item.Kindled) KindledMarks.On(sr);
                }
                _names[slot] = item.DisplayName;
            }
            else _names.Remove(slot);

            Shown = _names.Count;

            // A hooded cloak encloses the helm, as on the character (PrimitiveCharacterRig.
            // EncloseHelmInHood). Either half changing re-cuts it, from the helm's own sprite.
            if (slot == GearSlot.Head || slot == GearSlot.Back) EncloseHelmInHood();

            // A cape changed: repaint whatever dyed cloth the stand already wears in its colour.
            if (slot == GearSlot.Back)
                foreach (var other in GearSlots.All)
                    if (other != GearSlot.Back && GearCatalog.Get(_worn[(int)other]) is { DyedByBack: true })
                        Set(other, _worn[(int)other]);
        }

        string[] _worn;

        void EncloseHelmInHood()
        {
            var helmItem = GearCatalog.Get(_worn[(int)GearSlot.Head]);
            if (helmItem == null || !_layers.TryGetValue(RigLayer.HeadArmor, out var helm) || helm == null) return;
            Sprite own = null;
            foreach (var l in helmItem.LayersFor(menu: false))
                if (l != null && l.Layer == RigLayer.HeadArmor && l.Sprite != null) own = l.Sprite;
            if (own == null) return;
            if (helmItem.ShowsSkin) own = BodyLook.Reskin(own, new Palette.Ramp(Wood), "wood");

            bool hooded = GearCatalog.Get(_worn[(int)GearSlot.Back]) is { HasHood: true }
                          && _layers.TryGetValue(RigLayer.Hood, out var hood) && hood != null
                          && hood.enabled && hood.sprite != null;
            helm.sprite = hooded
                ? PixelSprite.Enclosed(own, _layers[RigLayer.Hood].sprite,
                    _layers[RigLayer.Hood].transform.worldToLocalMatrix * helm.transform.localToWorldMatrix)
                : own;
        }

        /// <summary>What the prompt reads out - the pieces on the stand, longest-standing first.</summary>
        public string Summary()
        {
            if (_names == null || _names.Count == 0) return "bare";
            var sb = new System.Text.StringBuilder();
            foreach (var slot in GearSlots.Worn)
            {
                if (!_names.TryGetValue(slot, out var n)) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(n);
                if (sb.Length > 90) { sb.Append(" ..."); break; }
            }
            return sb.ToString();
        }

        /// <summary>Which layers an item in this slot could possibly paint. Derived from the
        /// catalogue rather than hardcoded, so a new piece that paints an unusual layer is still
        /// cleared properly when it comes off.</summary>
        static IEnumerable<RigLayer> LayersOf(GearSlot slot)
        {
            var seen = new HashSet<RigLayer>();
            foreach (var item in GearCatalog.ForSlot(slot))
            foreach (var l in item.LayersFor(menu: false))
                if (l != null && !RigLayers.IsBody(l.Layer)) seen.Add(l.Layer);
            return seen;
        }

        void Hide(RigLayer layer)
        {
            if (_layers.TryGetValue(layer, out var sr) && sr != null)
            {
                sr.enabled = false;
                sr.sprite = null;
            }
        }

        void Update()
        {
            if (_glow == null) return;   // half-built after a reload; wait for the room rebuild
            _lit = Mathf.MoveTowards(_lit, _focused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            Apply(_lit);
        }

        void Apply(float lit)
        {
            _glow.color = new Color(1f, 0.92f, 0.72f, 0.08f + lit * 0.24f);
            Tone(RigLayer.Torso, Color.Lerp(Wood, WoodLit, lit));
            Tone(RigLayer.Head, Color.Lerp(Wood, WoodLit, lit));
            Tone(RigLayer.ArmFront, Color.Lerp(Wood, WoodLit, lit));
            Tone(RigLayer.LegFront, Color.Lerp(Wood, WoodLit, lit));
            Tone(RigLayer.ArmBack, Color.Lerp(WoodDark, WoodLit, lit * 0.6f));
            Tone(RigLayer.LegBack, Color.Lerp(WoodDark, WoodLit, lit * 0.6f));
        }

        void Tone(RigLayer layer, Color c)
        {
            if (_layers.TryGetValue(layer, out var sr) && sr != null) sr.color = c;
        }

        /// <summary>
        /// The dummy under the armour: six wooden blocks on the BODY layers, at the same texel
        /// sizes and positions the character's own placeholder body uses, so a cuirass sized
        /// against a torso still frames that torso here.
        /// </summary>
        void DrawDummy()
        {
            // Both the position AND the size arguments are texel counts divided by the global
            // LayoutUnit (PixelSprite.Px does both jobs here - Body's own `size` becomes
            // localScale directly), so both had to double when LayoutUnit did.
            Body(RigLayer.Torso, PixelSprite.Px(0f, 10f), PixelSprite.Px(24f, 20f), Spr.Square);
            Body(RigLayer.Head, PixelSprite.Px(0f, 12f), PixelSprite.Px(26f, 30f), Spr.Circle);
            Body(RigLayer.ArmBack, PixelSprite.Px(0f, -8f), PixelSprite.Px(8f, 16f), Spr.Square);
            Body(RigLayer.ArmFront, PixelSprite.Px(0f, -8f), PixelSprite.Px(8f, 16f), Spr.Square);
            Body(RigLayer.LegBack, PixelSprite.Px(0f, -8f), PixelSprite.Px(12f, 16f), Spr.Square);
            Body(RigLayer.LegFront, PixelSprite.Px(0f, -8f), PixelSprite.Px(12f, 16f), Spr.Square);
        }

        void Body(RigLayer layer, Vector2 at, Vector2 size, Sprite sprite)
        {
            if (!_layers.TryGetValue(layer, out var sr)) return;
            sr.enabled = true;
            sr.sprite = sprite;
            sr.transform.localPosition = at;
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        void MakeLayer(RigLayer layer)
        {
            var go = new GameObject(layer.ToString());
            go.transform.SetParent(PivotFor(layer), false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = LayerBase + StandRank(layer);
            sr.enabled = false;
            _layers[layer] = sr;
        }

        /// <summary>
        /// Enum order, except <see cref="RigLayer.NeckOver"/> - appended to the enum, so at its
        /// raw value it would draw over the head - which goes directly above BackOver, where the
        /// rig's front-facing stacks put it.
        /// </summary>
        static int StandRank(RigLayer layer)
        {
            if (layer == RigLayer.NeckOver) return (int)RigLayer.BackOver + 1;
            int raw = (int)layer;
            return raw > (int)RigLayer.BackOver ? raw + 1 : raw;
        }

        /// <summary>The joint a layer hangs from, and the stand's draw rank for it - read by
        /// EditorTools.NftArt so an item image composes a piece exactly as the stand wears it,
        /// rather than from a third copy of the pivot tree.</summary>
        public Transform PivotOf(RigLayer layer) => PivotFor(layer);
        public static int RankOf(RigLayer layer) => StandRank(layer);
        /// <summary>The scaled child holding the joint tree (see Build).</summary>
        public Transform Figure => transform.Find("figure");

        /// <summary>PrimitiveCharacterRig.PivotFor, and it has to stay identical to it - a layer
        /// hung off the wrong joint is a pauldron on a hip.</summary>
        Transform PivotFor(RigLayer layer) => layer switch
        {
            // The rig hangs the lower-arm layers, the ring and the weapon from an ELBOW. A
            // mannequin's arms never bend, so its elbow is simply the shoulder - which is why the
            // same layers land on the same joint here with no offset conversion: nothing paints
            // a lower layer on the stand (the rig cuts those itself), and a ring or weapon offset
            // is authored shoulder-relative, which is exactly this pivot.
            RigLayer.ArmBack or RigLayer.GlovesBack
                or RigLayer.ArmBackLower or RigLayer.GlovesBackLower
                or RigLayer.ArmBackHand or RigLayer.GlovesBackHand         => _armBack,
            RigLayer.ArmFront or RigLayer.GlovesFront
                or RigLayer.ArmFrontLower or RigLayer.GlovesFrontLower
                or RigLayer.ArmFrontHand or RigLayer.GlovesFrontHand
                or RigLayer.Ring or RigLayer.Weapon                        => _armFront,
            // The same for the knee: the rig hangs the shins from it, a mannequin never bends one.
            RigLayer.LegBack or RigLayer.LegsBack or RigLayer.BootsBack
                or RigLayer.LegBackLower or RigLayer.LegsBackLower
                or RigLayer.BootsBackLower                                 => _legBack,
            RigLayer.LegFront or RigLayer.LegsFront or RigLayer.BootsFront
                or RigLayer.LegFrontLower or RigLayer.LegsFrontLower
                or RigLayer.BootsFrontLower                                => _legFront,
            RigLayer.Head or RigLayer.HeadArmor or RigLayer.HairBack
                or RigLayer.Hood                                           => _head,
            _                                                              => _torso,
        };

        static Transform Pivot(string name, Vector2 at, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            return go.transform;
        }

        static SpriteRenderer Quad(Transform parent, string name, float w, float h,
                                   Color c, int order, Vector2 at, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : Spr.Square;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
