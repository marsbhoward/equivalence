using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// The blade travelling between the fist and the scabbard, for Zanmato's sheathe-and-draw
    /// finisher. Without it the sword simply blinked out of the hand and a stub appeared at the
    /// hip, which is not a sheathe - it is a cut.
    ///
    /// IT DRAWS THE REAL WEAPON SPRITE, captured from the rig before the rig's own copy is
    /// hidden - the same thing ThrownBlade does, and for the same reason: a generic stand-in
    /// reads as a second object rather than as YOUR sword leaving your hand.
    ///
    /// THE BLADE IS LONGER THAN THE SCABBARD AND IS CLIPPED AWAY AS IT GOES IN. It has to be: the
    /// saya is scaled to a BODY - belt to knee on a 0.77-tall character - while the blade runs 0.87
    /// units above its grip, three and a half times that. A scabbard sized to the real blade would
    /// hang past the character's feet. So the sprite is re-cut each step to only the part still
    /// OUTSIDE the mouth (Sprite.Create on a sub-rect, the same slice SigilDoor's door halves and
    /// Bisection's body halves already use), trimmed from the TOP so the bottom-left origin never
    /// moves and the grip stays exactly where it was put. Squashing the blade instead was tried
    /// and shrinks the protruding tsuka with it.
    ///
    /// It sorts IN FRONT of the saya, which is the opposite of the obvious answer and took a
    /// measurement to settle. Drawing it behind seemed right - "it goes inside" - but the CLIP
    /// has already removed everything past the mouth, so there is nothing left needing to be
    /// occluded; all the behind-sorting did was bury the handle's base under the mouth fitting,
    /// leaving the tsuka apparently starting above the opening and off to one side of it. In
    /// front, the handle sits ON the mouth, concentric with it, which is where a sheathed sword's
    /// handle actually is from the viewer's side.
    ///
    /// Precisely, it sorts AS THE RIG'S OWN WEAPON LAYER - which in the combat grip is above the
    /// saya, the body and the head and just under the sword hand. It was "one above the saya",
    /// which is right at the mouth and wrong everywhere else: out of the scabbard and swinging up,
    /// the blade went behind the pauldron and then behind the head while the fist holding it stayed
    /// in front. At the weapon's own depth the copy also leaves and rejoins the rig's sword without
    /// a change of layer.
    ///
    /// EVERYTHING IS COMPUTED IN THE RIG ROOT'S LOCAL SPACE, and that is not incidental. The root
    /// carries the -1 x mirror, so a local pose mirrors with the character for free - where world
    /// coordinates would need the whole "rotation is outside scale" correction the rig's own
    /// Plant motion documents the hard way.
    /// </summary>
    public class KatanaSheathe : MonoBehaviour
    {
        /// <summary>Blade travel between re-cuts, in texels. Quantised so a slide creates a
        /// handful of sprites rather than one per frame; 4 is under two screen pixels at the
        /// arena's own scale, so the step is invisible.</summary>
        const int ClipStep = 4;

        /// <summary>
        /// Where the fist holds the handle once the blade is home: this far above the MOUTH, in
        /// world units (10 body texels). On the mouth itself it was out of the arm's reach from the
        /// far shoulder, and covered the fitting it is meant to be pushing the blade into.
        /// </summary>
        const float GripHold = 10f / PixelSprite.FinestUnit;

        /// <summary>
        /// Where the fist holds the handle on the way in: this far from the grip's centre toward
        /// the GUARD (12 body texels of the grip's 17), which is where a hand sheathing a sword
        /// actually is. Held toward the pommel instead, a 34-texel grip put the fist at the chin
        /// with the guard still above the mouth, and the head drew over it.
        /// </summary>
        const float GripNearGuard = 12f / PixelSprite.FinestUnit;

        Transform _root;
        SpriteRenderer _saya, _sr, _weapon;
        Sprite _full;
        Vector2 _baseScale = Vector2.one;
        float _slide;

        /// <summary>Where the sword was in the fist when this started, root-local. CAPTURED, not
        /// re-read: the fist now follows the blade (see the rig's reach), so a start pose read off
        /// the hand every frame would chase itself.</summary>
        Vector2 _startPos;
        float _startDeg;

        /// <summary>Where the right fist was when this started, in the BLADE's own frame (x across,
        /// y toward the tip, from the grip centre): nothing in the rest carry (the grip is IN the
        /// fist), off down the handle - and, pose depending, beside it - in the combat grip. The
        /// hold eases from here onto the handle, so it starts exactly on the fist.</summary>
        Vector2 _startGrip;

        /// <summary>The rig, as the component it is - an interface field comes back null after a
        /// reload (CLAUDE.md), and this one is what hands the arm back.</summary>
        MonoBehaviour _rigHost;
        ICharacterRig Rig => _rigHost as ICharacterRig;

        /// <summary>Cut sprites by hidden-texel count. A Dictionary on a MonoBehaviour normally
        /// needs the documented reload guard, but this object lives under a second and is
        /// destroyed - an emptied cache just re-cuts. Null-checked on read regardless.</summary>
        readonly Dictionary<int, Sprite> _clips = new();

        /// <summary>
        /// Start the blade from where the rig draws it. The rig's ROOT carries the mirror; its
        /// weapon anchor is the weapon layer's own transform, so its position IS the grip the
        /// drawn blade hangs from and the copy starts exactly on it; its Trinket renderer is the
        /// worn scabbard, whose transform is the sprite centre.
        /// </summary>
        public static KatanaSheathe Attach(ICharacterRig rig, Sprite blade, Color tint, Vector2 size,
                                           float slideDistance)
        {
            var root = rig?.Transform;
            var hand = rig?.WeaponAnchor;
            var saya = rig?.TrinketRenderer;
            if (root == null || hand == null || blade == null) return null;

            var go = new GameObject("katana.sheathe");
            go.transform.SetParent(root, false);

            var e = go.AddComponent<KatanaSheathe>();
            e._root = root;
            e._rigHost = rig as MonoBehaviour;
            e._saya = saya;
            e._full = blade;
            e._slide = slideDistance;

            e._sr = go.AddComponent<SpriteRenderer>();
            e._sr.sprite = blade;
            e._sr.color = tint;
            // At the rig's own weapon depth - see the class header.
            e._weapon = rig.WeaponRenderer;
            e.SyncSorting();

            // `size` is a WORLD size (ICharacterRig.TryGetWeaponVisual), and this sits under the
            // rig root, which carries the character's visual scale - so take that back out, or
            // the drawn scale lands on the blade twice.
            var rootScale = root.lossyScale;
            if (Mathf.Abs(rootScale.x) > 0.0001f && Mathf.Abs(rootScale.y) > 0.0001f)
                size = new Vector2(size.x / Mathf.Abs(rootScale.x), size.y / Mathf.Abs(rootScale.y));

            var native = blade.bounds.size;
            e._baseScale = new Vector2(
                native.x > 0.0001f ? size.x / native.x : size.x,
                native.y > 0.0001f ? size.y / native.y : size.y);
            go.transform.localScale = new Vector3(e._baseScale.x, e._baseScale.y, 1f);

            e._startPos = root.InverseTransformPoint(hand.position);
            e._startDeg = e.LocalAngle(hand.up);
            if (rig.TryGetSwordHand(out var fist))
                e._startGrip = Rotate(fist - e._startPos, -e._startDeg);

            e.SetProgress(0f);
            return e;
        }

        /// <summary>0 = sitting in the fist exactly where the rig drew it, 1 = home in the saya.</summary>
        public void SetProgress(float t)
        {
            if (_root == null || _sr == null) return;
            t = Mathf.Clamp01(t);

            // ---- the two poses, both in root-local space so the mirror comes for free ----
            var handPos = _startPos;
            float handDeg = _startDeg;

            // Toward the MOUTH, signed by which way the saya leans (DemoGear.SayaMouthSide) - the
            // blade goes in pointing down the axis, the other way. A saya the rig has mirrored to
            // keep it on the left hip (GearItem.WornOnLeftHip) leans the other way too.
            float axisDeg = DemoGear.SayaAxisDegrees * (_saya != null && _saya.flipX ? -1f : 1f);
            var axis = new Vector2(Mathf.Sin(axisDeg * Mathf.Deg2Rad),
                                   Mathf.Cos(axisDeg * Mathf.Deg2Rad));

            Vector2 mouth;
            if (_saya != null)
            {
                var centre = (Vector2)_root.InverseTransformPoint(_saya.transform.position);
                mouth = centre + axis * DemoGear.SayaHalfLength;
            }
            else
            {
                mouth = new Vector2(0.17f, -0.13f);   // no scabbard worn: a plausible hip
            }

            // Point-first, so the blade runs down the axis and the handle stands out of the mouth.
            // A sprite at rotation d has its up-vector at (-sin d, cos d), so pointing ALONG the
            // axis is -axisDeg and pointing INTO the scabbard is that turned around.
            float homeDeg = 180f - axisDeg;

            // ---- two beats, because a sheathe is a carry ACROSS and then a slide IN ----
            //
            // One straight lerp from the fist to the mouth pushes the blade sideways through the
            // scabbard's wall, which reads as clipping rather than as sheathing. Aligning first
            // and only then travelling down the axis is what a person actually does with a sword.
            const float Align = 0.55f;

            // WHERE THE BLADE IS WHEN IT ARRIVES, as a fraction of its length above the mouth.
            //
            // Not 1. A blade this long touching the mouth tip-first would put its grip 0.9 units
            // up the axis - above the character's own head - and the carry became a windmill.
            // Arriving already part-way in is both what a fast sheathe actually looks like and
            // the only pose that keeps the hand at the hip. The visible length is kept exactly
            // equal to the gap it spans, so the tip sits in the mouth rather than short of it.
            //
            // 0.2, not 0.35, since the fist has followed the blade: at 0.35 the grip arrived at
            // the character's chin, and the hand holding it went behind the head for half the
            // sheathe and half the draw.
            const float Poise = 0.2f;
            var poised = mouth + axis * (_slide * Poise);

            Vector2 pos;
            float deg, inside, offHand;
            Vector2 grip;   // where the fist holds, in the blade's frame - see _startGrip
            if (t <= Align)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / Align);
                pos = Vector2.Lerp(handPos, poised, k);
                deg = Mathf.LerpAngle(handDeg, homeDeg, k);
                grip = Vector2.Lerp(_startGrip, new Vector2(0f, GripNearGuard), k);
                // The other hand comes off the hilt as the blade crosses, and is back on it by the
                // time the draw returns the blade here - the swing that follows starts from the
                // grip it expects.
                offHand = k;
                // Squared, so it stays whole while it is plainly still in the air and only
                // starts entering as it arrives - the shrink reads as the mouth taking it.
                inside = (1f - Poise) * k * k;
            }
            else
            {
                float k = Mathf.SmoothStep(0f, 1f, (t - Align) / (1f - Align));
                pos = Vector2.Lerp(poised, mouth, k);
                deg = homeDeg;
                grip = new Vector2(0f, -Mathf.Max(-GripNearGuard, GripHold - Vector2.Dot(pos - mouth, axis)));
                offHand = 1f;
                inside = Mathf.Lerp(1f - Poise, 1f, k);
            }

            transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, deg);

            // THE RIGHT HAND STAYS ON IT. The rig's own sword is hidden for the whole sequence and
            // this copy is a separate sprite, so the arm has to be sent after it - without this it
            // stayed up in the rest carry, empty.
            //
            // Behind the guard while it is still above the mouth, then held just above the mouth
            // while the last of the grip goes in - the hand pushes it home rather than riding it
            // into the scabbard. On the way across it eases there from wherever the fist already
            // was (_startGrip), and back again on the way out, so the draw ends with the hand
            // where the swing that follows expects it.
            Rig?.SetHandTarget(pos + Rotate(grip, deg), offHand);

            // Show only what is still outside the mouth.
            //
            // CLAMPED TO THE GRIP, and that clamp is not defensive - without it the quantised
            // step rounds UP past the pivot (70 texels of blade rounds to 72), the cut rect ends
            // up shorter than the pivot's own offset from the bottom, and the whole handle draws
            // a couple of texels along its axis from where it belongs. On screen that reads as
            // the hilt sitting beside the mouth rather than in it - which looks like a mistake,
            // because it is one. Nothing below the grip is ever hidden: the handle is the part
            // that must stay proud of the scabbard.
            int above = Mathf.FloorToInt(_full.rect.height - _full.pivot.y);
            int hidden = Mathf.Clamp(Mathf.RoundToInt(inside * above / ClipStep) * ClipStep, 0, above);
            var cut = ClippedTo(hidden);
            _sr.sprite = cut;
            _sr.enabled = cut != null;

            SyncSorting();
        }

        /// <summary>
        /// The rig's weapon layer's order, re-read every frame rather than cached: the rig is
        /// y-sorted, so SetSortingBase moves every layer whenever the character does. Never below
        /// the saya (the handle must stand ON the mouth), and the character band if there is
        /// neither to go by, so the animation still plays on a rig that cannot say.
        /// </summary>
        void SyncSorting()
        {
            int order = _weapon != null ? _weapon.sortingOrder : Core.SortingOrders.Character;
            if (_saya != null) order = Mathf.Max(order, _saya.sortingOrder + 1);
            _sr.sortingOrder = order;
        }

        /// <summary>
        /// The blade with its top <paramref name="hidden"/> texels cut off - what is left standing
        /// out of the scabbard. Trimmed from the TOP only: the sub-rect keeps its bottom-left
        /// origin, so the grip pivot stays at the same texel and the handle never shifts.
        /// </summary>
        Sprite ClippedTo(int hidden)
        {
            if (hidden <= 0) return _full;
            if (_clips.TryGetValue(hidden, out var cached) && cached != null) return cached;

            // RECT, NOT textureRect. Sprite.pivot is measured against the rect the sprite was
            // CREATED with; textureRect is Unity's tightly-trimmed region, which for this blade
            // starts 4 texels in. Cutting from the trimmed rect while normalising the pivot
            // against it mixes two coordinate systems and slides the handle four texels off the
            // scabbard's centreline - which is exactly the "off-centre, looks like a mistake"
            // this fixes. Measured: via rect the grip's centre and the pivot are both 15.5.
            var r = _full.rect;
            float h = r.height - hidden;
            if (h <= 1f) return null;   // entirely inside

            var made = Sprite.Create(_full.texture,
                                     new Rect(r.x, r.y, r.width, h),
                                     new Vector2(_full.pivot.x / r.width, _full.pivot.y / h),
                                     _full.pixelsPerUnit);
            made.name = _full.name + ".cut" + hidden;
            _clips[hidden] = made;
            return made;
        }

        /// <summary>
        /// A world direction's angle in the root's own frame. InverseTransformVector, NOT
        /// InverseTransformDirection: the latter ignores scale, and the root's -1 x IS the mirror,
        /// so facing left it handed back the un-mirrored angle and the blade started the sheathe
        /// turned ~100 degrees away from the sword in the hand.
        /// </summary>
        static Vector2 Rotate(Vector2 v, float deg)
        {
            float c = Mathf.Cos(deg * Mathf.Deg2Rad), s = Mathf.Sin(deg * Mathf.Deg2Rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        float LocalAngle(Vector3 worldUp)
        {
            var d = (Vector2)_root.InverseTransformVector(worldUp);
            return d.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
        }

        public void Release()
        {
            if (this == null) return;
            Rig?.SetHandTarget(null);    // now, not at the deferred Destroy - the swing starts this frame
            Destroy(gameObject);
        }

        /// <summary>
        /// Sprite.Create allocates, and a runtime Sprite is NOT collected with the GameObject that
        /// referenced it - so every cast of the finisher would otherwise leave its cut blades
        /// behind for the rest of the run. Destroyed with the object that made them.
        /// </summary>
        void OnDestroy()
        {
            // Hand the arm back however this ends - Release, or the rig torn down around it.
            Rig?.SetHandTarget(null);

            foreach (var s in _clips.Values) if (s != null) Destroy(s);
            _clips.Clear();
        }
    }
}
