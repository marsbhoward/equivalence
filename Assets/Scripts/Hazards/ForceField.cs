using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    public enum FieldColor { Blue, Red }

    /// <summary>
    /// A barrier strung between two columns. Bodies walk through it freely (a trigger collider),
    /// but it blocks or amplifies anything that tries to attack across it - Blue nulls a
    /// projectile/beam outright, Red doubles its damage (see HazardQuery). It can never be
    /// attacked itself - there is no Health component at all - and its own lifetime is entirely
    /// derived from its two anchors: destroy either one and the field drops.
    ///
    /// Anchors are POLLED, not subscribed to. A domain reload silently drops C# events/delegates
    /// (this project's own notes on Health.ModifyIncoming and PlayerController.ModsSource are the
    /// same lesson) - a plain Column reference survives that exactly the way any UnityEngine.Object
    /// reference does, and Unity's own overloaded null check already tells the truth about whether
    /// it has been destroyed. This is also what makes "a field between two non-cracked columns can
    /// never be destroyed" free: a non-cracked column has no Health and so can never become null.
    /// </summary>
    public class ForceField : MonoBehaviour
    {
        /// <summary>Named apart from UnityEngine.Color on purpose - a member called "Color" on this
        /// class would shadow the type of that name for every method below.</summary>
        public FieldColor Charge { get; private set; }

        Column _anchorA, _anchorB;
        bool _flipping;
        float _flipTimer;
        SpriteRenderer _glow, _spine;

        // The gameplay boundary stays the full nominal width (Tuning.Hazards.ForceFieldWidth) -
        // only the PAINT is split into a wide, faint atmospheric bleed and a thin bright spine
        // sitting well inside it, so what a raycast actually hits and what the eye reads as "the
        // barrier" stay in agreement even though the spine alone looks thinner than the collider.
        const float GlowWidthMul = 2.4f;
        const float SpineWidthFrac = 0.32f;
        const float SpineLengthFrac = 0.97f;

        public static ForceField Spawn(Column a, Column b, FieldColor charge, bool flipping, Transform parent)
        {
            var go = new GameObject("force-field");
            go.transform.SetParent(parent, false);

            var field = go.AddComponent<ForceField>();
            field._anchorA = a;
            field._anchorB = b;
            field.Charge = charge;
            field._flipping = flipping;
            field._flipTimer = Tuning.Hazards.ForceFieldFlipInterval;

            // Anchors never move once placed, so the span geometry is computed once here rather
            // than every frame - only the paint (color/flip) needs to keep updating.
            Vector2 from = a.transform.position;
            Vector2 to = b.transform.position;
            Vector2 delta = to - from;
            float length = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;

            go.transform.position = (from + to) * 0.5f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(Tuning.Hazards.ForceFieldWidth, length, 1f);

            // Glow: a soft, wide light-bleed the full length of the span - brightest at the
            // midpoint and fading toward each anchor, the same "the gradient IS the effect" shape
            // Spr.Glow was built for, rather than a hard-edged bar with a uniform fill.
            field._glow = Layer(go, Spr.Glow, new Vector2(GlowWidthMul, 1f));

            // Spine: the actual boundary, thin and bright - see it clearly without it reading as
            // a solid wall. Deliberately narrower than the collider it sits inside.
            field._spine = Layer(go, Spr.Capsule, new Vector2(SpineWidthFrac, SpineLengthFrac));

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;   // bodies pass through; HazardQuery's raycast still sees it (useTriggers = true)
            col.size = Vector2.one;

            field.Repaint();
            return field;
        }

        static SpriteRenderer Layer(GameObject parent, Sprite sprite, Vector2 localScale)
        {
            var go = new GameObject("layer");
            go.transform.SetParent(parent.transform, false);
            go.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = SortingOrders.Reticle;
            return sr;
        }

        void Update()
        {
            if (_anchorA == null || _anchorB == null) { Destroy(gameObject); return; }

            if (_flipping)
            {
                _flipTimer -= Time.deltaTime;
                if (_flipTimer <= 0f)
                {
                    _flipTimer = Tuning.Hazards.ForceFieldFlipInterval;
                    Charge = Charge == FieldColor.Blue ? FieldColor.Red : FieldColor.Blue;
                }
            }

            Repaint();
        }

        void Repaint()
        {
            var baseColor = Charge == FieldColor.Blue
                ? new Color(0.35f, 0.65f, 1f)
                : new Color(1f, 0.35f, 0.35f);

            // Flip-capable fields shimmer - a steady light reads as "this is what it is", a
            // pulsing one reads as "this is about to change", which is the whole point of asking
            // for a distinct tell on the flippable kind. Both layers breathe together so the
            // whole construct reads as one light source, not two independently-lit shapes.
            float pulse = _flipping ? 0.7f + 0.3f * Mathf.Sin(Time.time * 4f) : 1f;

            // Wide and faint - this is the see-through atmosphere, not the boundary itself.
            var glow = baseColor;
            glow.a = 0.30f * pulse;
            _glow.color = glow;

            // The spine carries the color you actually read Blue/Red off, lightened toward white
            // so it reads as glowing rather than as painted plastic - still translucent, just the
            // brightest part of the construct.
            var spine = Color.Lerp(baseColor, Color.white, 0.35f);
            spine.a = 0.5f * pulse;
            _spine.color = spine;
        }
    }
}
