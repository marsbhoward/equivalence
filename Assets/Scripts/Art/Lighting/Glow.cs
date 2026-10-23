using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Lighting
{
    /// <summary>
    /// A LIGHT in the room: a pool <see cref="LightField"/> cuts out of the darkness, banded and
    /// dithered like the art. A component on whatever gives off light - the player, a fire pit, a
    /// sconce, the spire's core, a Rift, a window's patch on the floor.
    ///
    /// It can BORROW its colour and strength from a sprite the owner already animates
    /// (<see cref="Follow"/>): the spire's core fading in as it rises, the Rift's colour, a window
    /// patch's alpha - so the owner's logic is untouched and the light can never disagree with
    /// the picture. Otherwise the owner sets <see cref="Intensity"/> itself (a fire pit by its heat).
    /// </summary>
    public class Glow : MonoBehaviour
    {
        public float Radius = 3f;
        [Range(0f, 1f)] public float Intensity = 1f;
        public Color Color = new(1f, 0.75f, 0.45f);
        /// <summary>0..1 - how much the strength wavers, on its own clock (a flame).</summary>
        public float Flicker;
        /// <summary>A sprite to take colour, alpha (as strength) and position from - its bounds'
        /// centre - when set. A hidden or disabled one gives no light.</summary>
        public SpriteRenderer Follow;
        /// <summary>Radius from the followed sprite's own size: max extent x this (0 keeps Radius).</summary>
        public float RadiusFromBounds;
        /// <summary>Share of the radius at full strength; negative uses Tuning.Lighting.CoreFraction.</summary>
        public float Core = -1f;
        /// <summary>0..1 - this light's colour cast over its WHOLE pool, even where fully lit (most
        /// lights clear the dark to no tint - a tint everywhere read as haze). Only lights whose
        /// colour is the point use it: the Nigredo's reds.</summary>
        public float Wash;
        /// <summary>Offset from the transform, world units (the player's light sits on the body, not the feet).</summary>
        public Vector2 Offset;

        float _phase;

        static List<Glow> _all = new();

        /// <summary>Every live glow. Rebuilt from the scene if a domain reload emptied it.</summary>
        public static List<Glow> All
        {
            get
            {
                _all ??= new List<Glow>();
                if (_all.Count == 0)
                    foreach (var g in FindObjectsByType<Glow>(FindObjectsInactive.Exclude)) _all.Add(g);
                return _all;
            }
        }

        void OnEnable() { _all ??= new List<Glow>(); if (!_all.Contains(this)) _all.Add(this); _phase = Mathf.Abs(GetEntityId().GetHashCode() * 0.6180339f) % 10f; }   // never UnityEngine.Random - seeded rolls share it
        void OnDisable() => _all?.Remove(this);

        /// <summary>What it casts this moment: where, how far, how strongly, in what colour.</summary>
        public bool Sample(out Vector2 at, out float radius, out float strength, out Color color)
        {
            at = (Vector2)transform.position + Offset; radius = Radius; strength = Intensity; color = Color;
            if (Follow != null)
            {
                if (!Follow.enabled || !Follow.gameObject.activeInHierarchy) return false;
                var b = Follow.bounds;
                at = b.center;
                if (RadiusFromBounds > 0f) radius = Mathf.Max(b.extents.x, b.extents.y) * RadiusFromBounds;
                var c = Follow.color;
                strength *= c.a;
                color = new Color(c.r, c.g, c.b, 1f);
            }
            if (Flicker > 0f)
            {
                float t = Time.time * 9f + _phase;
                float w = Mathf.Sin(t) * 0.5f + Mathf.Sin(t * 2.3f + 1.7f) * 0.3f + Mathf.Sin(t * 5.1f) * 0.2f;
                strength *= 1f - Flicker * (0.5f + 0.5f * w);
            }
            return strength > 0.01f && radius > 0.05f;
        }

        /// <summary>Adds (or finds) a glow on <paramref name="go"/>.</summary>
        public static Glow Attach(GameObject go, float radius, Color color, float intensity = 1f, float flicker = 0f)
        {
            if (go == null) return null;
            var g = go.GetComponent<Glow>();
            if (g == null) g = go.AddComponent<Glow>();
            g.Radius = radius; g.Color = color; g.Intensity = intensity; g.Flicker = flicker;
            return g;
        }

        /// <summary>A brief light that fades out and removes itself - an element release, a blast.</summary>
        public static void Flash(Vector2 at, float radius, Color color, float seconds, float intensity = 1f)
        {
            var go = new GameObject("glow.flash");
            go.transform.position = at;
            var g = Attach(go, radius, color, intensity);
            go.AddComponent<FadeOut>().Begin(g, seconds);
        }

        class FadeOut : MonoBehaviour
        {
            Glow _g; float _t, _len, _from;
            public void Begin(Glow g, float seconds) { _g = g; _len = Mathf.Max(0.05f, seconds); _from = g.Intensity; }
            void Update()
            {
                _t += Time.deltaTime;
                if (_g != null) _g.Intensity = _from * (1f - _t / _len);
                if (_t >= _len) Destroy(gameObject);
            }
        }
    }
}
