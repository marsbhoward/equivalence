using UnityEngine;
using Convergence.Core;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// A pulsing red glow over the character's own left eye - GearItem.HasGlowingEye's other
    /// half. BodyLook.Head's leftEyeGlow override already recolours the iris itself (see its own
    /// doc); baked pixel art cannot breathe, so this sits in front of it purely for the pulse.
    ///
    /// POSITIONED FROM MEASURED DATA, not eyeballed - the first version of this item placed a
    /// fixed local offset by guessing from a screenshot and it sat off the actual eye. The caller
    /// (PrimitiveCharacterRig.RepaintHead) finds the override's real texel bounding box with
    /// BodyLook.FindLeftEyeBox and converts it through the same PixelSprite.Px scale the head
    /// sprite's own offset uses, so this tracks wherever the eye actually lands - which moves a
    /// texel or two between expressions.
    /// </summary>
    public class GlowingEye : MonoBehaviour
    {
        static readonly Color Red = new(0.95f, 0.12f, 0.10f);

        SpriteRenderer _sr;
        float _t;

        public static GlowingEye Attach(Transform head)
        {
            var existing = head.GetComponentInChildren<GlowingEye>(true);
            if (existing != null) return existing;

            var go = new GameObject("glowing.eye");
            go.transform.SetParent(head, false);
            // Sized against the eye it sits on, which halved with the head when the head moved to
            // the body's density (0.065 was the eye at LayoutUnit).
            go.transform.localScale = Vector3.one * 0.0325f;

            var glow = go.AddComponent<GlowingEye>();
            glow._sr = go.AddComponent<SpriteRenderer>();
            glow._sr.sprite = Spr.Glow;
            glow._sr.color = Red;
            // An OVERLAY, never re-based - see SortingOrders' own note on StatusOverlay. This
            // effect only ever shows when the face itself is visible (RepaintHead hides it along
            // with the colour override whenever a helm is hidden - see SyncGlowEye), so it can
            // stay pinned above everything without needing to track a helmet layer's order.
            glow._sr.sortingOrder = SortingOrders.StatusOverlay;
            return glow;
        }

        public void SetShown(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        public void SetLocalPosition(Vector2 local) => transform.localPosition = local;

        void Update()
        {
            if (_sr == null) return;
            _t += Time.deltaTime;

            // The same breathing idiom PrismGlow and SaintHalo both use, so a lit eye reads as
            // alive rather than as a static decal.
            float pulse = 0.55f + 0.35f * Mathf.Sin(_t * 2.6f);
            _sr.color = new Color(Red.r, Red.g, Red.b, pulse);
        }
    }
}
