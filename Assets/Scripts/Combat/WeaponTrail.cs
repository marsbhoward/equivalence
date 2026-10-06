using UnityEngine;
using Convergence.Art.Gear;

namespace Convergence.Combat
{
    /// <summary>
    /// An arc drawn behind the blade for the length of a FINISHER's swing, and only a finisher's.
    ///
    /// Basics deliberately draw nothing. A swing already shows its own reach through the blade and
    /// the crescent Spr.Slice throws, and a trail on every swing would be the same
    /// over-description that keeps the burst ring off ordinary hits - it stops meaning "this one
    /// is the big one" the moment every swing has it.
    ///
    /// Hung off <see cref="ICharacterRig.WeaponAnchor"/> and sorted against
    /// <see cref="ICharacterRig.WeaponRenderer"/>, which is what those two accessors exist for.
    /// The order has to be read EVERY FRAME rather than taken once: the rig is y-sorted, so
    /// SetSortingBase moves every layer whenever the character moves, and TwoHandedOrder permutes
    /// which offset the weapon itself holds.
    ///
    /// NOT parented for its shape, only for its position. A TrailRenderer emits into world space
    /// whatever it hangs under, which is the entire point - an arc that moved with the arm would
    /// be a shape stuck to the sword rather than the path the sword took.
    /// </summary>
    public class WeaponTrail : MonoBehaviour
    {
        /// <summary>
        /// How long a point on the arc survives. Short: this is a smear behind a blade travelling
        /// a fixed number of degrees, not a ribbon. Much past this and the tail is still hanging
        /// in the air after the arm has come back to rest, which reads as the sword having been
        /// somewhere it never was.
        /// </summary>
        const float Persistence = 0.11f;

        const float Width = 0.10f;

        /// <summary>
        /// Where along the blade the arc is drawn from, as a fraction of the weapon's own height.
        ///
        /// A weapon's pivot is its GRIP, so local +Y runs up the blade and the tip is very nearly
        /// the whole of its height above the fist - the same geometry RiftShards' `along` and
        /// SaintHalo's midpoint are counted off. Just short of the tip rather than exactly on it,
        /// because the outermost texel of a blade is usually its point tapering to nothing and an
        /// arc struck from there reads as detached from the weapon.
        /// </summary>
        const float AlongBlade = 0.92f;

        TrailRenderer _trail;
        SpriteRenderer _weapon;
        float _remaining;

        /// <summary>
        /// Draw the arc for <paramref name="seconds"/>. Safe to call on a rig that cannot say
        /// where its weapon is - the SpriteLibrary rig answers null to both accessors and simply
        /// does without, the same way every other weapon-hung effect treats it.
        /// </summary>
        public static void Play(ICharacterRig rig, float seconds)
        {
            if (rig == null || seconds <= 0f) return;

            var anchor = rig.WeaponAnchor;
            var weapon = rig.WeaponRenderer;
            if (anchor == null || weapon == null) return;
            if (!rig.TryGetWeaponVisual(out _, out var tint, out var size)) return;

            // Reused rather than doubled if a second swing starts before the first has faded -
            // the same trap RiftShards.Attach documents, and here it would also leave the older
            // renderer emitting from a stale position for the rest of the run.
            var t = anchor.GetComponentInChildren<WeaponTrail>(true);
            if (t == null)
            {
                var go = new GameObject("weapon.trail");
                go.transform.SetParent(anchor, false);
                t = go.AddComponent<WeaponTrail>();
                t._trail = go.AddComponent<TrailRenderer>();
                t._trail.autodestruct = false;

                // The weapon's own material, not a default one. A TrailRenderer left on Unity's
                // built-in material renders untinted in URP, which is the magenta-adjacent result
                // that makes an effect look broken rather than absent.
                t._trail.sharedMaterial = weapon.sharedMaterial;
            }

            t._weapon = weapon;
            // `size` is a WORLD size and the trail sits under the weapon anchor, inside the rig's
            // visual scale - measured in the anchor's own units or it rides past the blade.
            float anchorY = Mathf.Abs(anchor.lossyScale.y);
            float bladeLocal = anchorY > 0.0001f ? size.y / anchorY : size.y;
            t.transform.localPosition = new Vector3(0f, bladeLocal * AlongBlade, 0f);

            var trail = t._trail;
            trail.time = Persistence;
            trail.minVertexDistance = 0.01f;
            trail.widthCurve = AnimationCurve.EaseInOut(0f, Width, 1f, 0f);
            trail.numCapVertices = 2;

            // Struck in the weapon's own colour, so a skinned blade's arc is that blade's arc.
            // Fading to fully transparent at the tail rather than to a darker tone: the arc is
            // light coming off an edge, and light thins rather than going grey.
            var head = tint; head.a = 0.55f;
            var tail = tint; tail.a = 0f;
            trail.startColor = head;
            trail.endColor = tail;

            // Cleared before every swing, never merely re-enabled. Between finishers the arm
            // returns to rest, so a trail that kept its old points would join where the blade
            // finished the last swing to where it starts this one - a straight line drawn across
            // the character's own body.
            trail.Clear();
            trail.emitting = true;

            t._remaining = seconds;
            t.enabled = true;
        }

        void LateUpdate()
        {
            if (_trail == null) return;

            // Every frame, for the reason the class doc gives: the rig re-sorts as it moves.
            // Behind the blade, so the weapon is never drawn through its own arc.
            if (_weapon != null)
            {
                _trail.sortingLayerID = _weapon.sortingLayerID;
                _trail.sortingOrder = _weapon.sortingOrder - 1;
            }

            if (_remaining <= 0f) return;

            _remaining -= Time.deltaTime;
            if (_remaining > 0f) return;

            _remaining = 0f;

            // Stop ADDING points and let what is already drawn fade on its own. Clearing here
            // instead would cut the arc off mid-air on the frame the swing ends, which is the
            // pop the rig's own pose easing exists to avoid.
            _trail.emitting = false;
        }
    }
}
