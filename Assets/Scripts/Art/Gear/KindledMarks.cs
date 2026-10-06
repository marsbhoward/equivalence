using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// The Secret Fire burning in the marks of whatever ONE renderer shows (see SecretFire): an
    /// overlay child drawing <see cref="SecretFire.Overlay(Sprite)"/> of the renderer's CURRENT
    /// sprite, at the one brightness every mark in the game shares.
    ///
    /// It never touches the renderer's own sprite - the rig swaps that for flashes, stone, cuts and
    /// mirrors, and an overlay just follows whatever is there (FinisherGlint's rule). A sprite with no
    /// marks (anything unkindled, a flash, a statue) gives no overlay, so this sits on a renderer
    /// harmlessly whatever it is showing: the rig puts one on every gear layer once anything kindled
    /// is worn, and the rack and the armoury wall keep theirs as the piece on show changes.
    ///
    /// Sorted at the renderer's OWN order, nudged toward the camera (FinisherGlint again: the rig's
    /// orders are a dense permutation, so order + 1 is some other layer, and a tie is settled by
    /// distance). The nudge is a thousandth of a unit, not the glint's hundredth: the hub camera is
    /// TILTED, and a z offset there moves the picture up the screen by its sine - at 0.01 that is the
    /// best part of a texel. Read every frame after DepthSorted, which re-sorts the rig as it walks.
    ///
    /// Everything it holds is a UnityEngine.Object reference, which a domain reload restores; the
    /// overlay cache it reads is static and simply rebuilds.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class KindledMarks : MonoBehaviour
    {
        const float Nudge = 0.001f;

        [SerializeField] SpriteRenderer _source;
        [SerializeField] SpriteRenderer _glow;

        /// <summary>Burn the marks of whatever <paramref name="source"/> shows. Idempotent.</summary>
        public static KindledMarks On(SpriteRenderer source)
        {
            if (source == null) return null;
            var marks = source.GetComponent<KindledMarks>();
            if (marks == null)
            {
                marks = source.gameObject.AddComponent<KindledMarks>();
                marks._source = source;
            }
            return marks;
        }

        void LateUpdate()
        {
            var src = _source;
            bool drawn = src != null && src.enabled && !src.forceRenderingOff && src.sprite != null;
            var overlay = drawn ? SecretFire.Overlay(src.sprite) : null;
            float alpha = overlay != null ? SecretFire.Alpha : 0f;

            if (overlay == null || alpha <= 0.001f)
            {
                if (_glow != null && _glow.enabled) _glow.enabled = false;
                return;
            }

            if (_glow == null)
            {
                var go = new GameObject("marks");
                go.transform.SetParent(src.transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -Nudge);
                _glow = go.AddComponent<SpriteRenderer>();
            }

            _glow.sprite = overlay;
            _glow.sharedMaterial = src.sharedMaterial;
            _glow.flipX = src.flipX;
            _glow.flipY = src.flipY;
            _glow.sortingLayerID = src.sortingLayerID;
            _glow.sortingOrder = src.sortingOrder;
            // The renderer's own colour carries through - pixel art draws at white, so this is a
            // no-op until something tints the figure on purpose (Separatio's ghost, an echo's fade).
            var c = src.color;
            _glow.color = new Color(c.r, c.g, c.b, c.a * alpha);
            _glow.enabled = true;
        }

        void OnDisable()
        {
            if (_glow != null) _glow.enabled = false;
        }
    }
}
