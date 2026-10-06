using UnityEngine;
using UnityEngine.UI;
using Convergence.Art.Gear;

namespace Convergence.UI
{
    /// <summary>
    /// The Secret Fire on a flat card: KindledMarks' overlay, for a UI Image instead of a renderer
    /// (see Art.Gear.SecretFire). A child image stretched over the card's own, the same
    /// preserveAspect, showing <see cref="SecretFire.Overlay(Sprite)"/> of whatever the card shows -
    /// the overlay is the source's size, so the two draw into the same rect texel for texel. Faded on
    /// the shared beat, on unscaled time like everything else here: these screens pause the game.
    /// </summary>
    public class KindledImage : MonoBehaviour
    {
        [SerializeField] Image _source;
        [SerializeField] Image _glow;

        /// <summary>Burn the marks of <paramref name="source"/>'s picture if
        /// <paramref name="item"/> carries any. Call after the image is laid out.</summary>
        public static void On(Image source, GearItem item)
        {
            if (source == null || item == null || !item.Kindled) return;
            // Not `??`: in the Editor a missing component comes back as Unity's fake null.
            var k = source.GetComponent<KindledImage>();
            if (k == null) k = source.gameObject.AddComponent<KindledImage>();
            k._source = source;
        }

        void Update()
        {
            if (_source == null) return;
            var overlay = SecretFire.Overlay(_source.sprite);
            float alpha = overlay != null && _source.enabled ? SecretFire.Alpha : 0f;

            if (_glow == null)
            {
                var go = new GameObject("marks", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_source.transform, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                _glow = go.AddComponent<Image>();
                _glow.raycastTarget = false;
            }

            _glow.enabled = alpha > 0.001f;
            if (!_glow.enabled) return;
            if (_glow.sprite != overlay) _glow.sprite = overlay;
            _glow.preserveAspect = _source.preserveAspect;
            var c = _source.color;
            _glow.color = new Color(c.r, c.g, c.b, c.a * alpha);
        }
    }
}
