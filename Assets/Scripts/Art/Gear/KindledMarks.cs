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

        /// <summary>White-hot marks over the ordinary ones, while a Magnum Opus runs them past full.</summary>
        [SerializeField] SpriteRenderer _hot;

        /// <summary>The same marks near white, over the hot ones, as the heat nears its top.</summary>
        [SerializeField] SpriteRenderer _sear;

        /// <summary>The Magnum Opus on this renderer's character, found only while one is running
        /// somewhere (<see cref="MagnumOpusGlow.Live"/>).</summary>
        [SerializeField] MagnumOpusGlow _opus;

        /// <summary>
        /// Hold these marks lit at this alpha and heat whatever the beat says (negative = off) - a
        /// weapon of light in flight (the Prima Materia art's thrown bow is FULLY lit, not on the beat).
        /// </summary>
        public float ForceAlpha = -1f, ForceHeat;

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
            // A liquid only ebbs: it never burns below its floor (SecretFire.LiquidFloor).
            float alpha = overlay != null ? Mathf.Max(SecretFire.Alpha, SecretFire.FloorOf(src.sprite)) : 0f;
            float heat = 0f;

            // The character performing the Magnum Opus answers for its own marks while it does.
            if (overlay != null && ForceAlpha >= 0f) { alpha = ForceAlpha; heat = ForceHeat; }
            else if (overlay != null && MagnumOpusGlow.Live > 0)
            {
                if (_opus == null) _opus = src.GetComponentInParent<MagnumOpusGlow>();
                if (_opus != null && _opus.Running) _opus.Sample(src, alpha, out alpha, out heat);
            }

            if (overlay == null || alpha <= 0.001f)
            {
                if (_glow != null && _glow.enabled) _glow.enabled = false;
                if (_hot != null && _hot.enabled) _hot.enabled = false;
                if (_sear != null && _sear.enabled) _sear.enabled = false;
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

            // Past full the marks burn HOT, and near the top of the heat they SEAR toward white -
            // the blade's inner light at its most luminous (the Magnum Opus, gathered to strike).
            float sear = Mathf.Clamp01((heat - 0.45f) / 0.55f);
            _hot = Layer(_hot, "marks.hot", 2f, heat > 0.001f ? SecretFire.HotOverlay(src.sprite) : null, alpha * heat);
            _sear = Layer(_sear, "marks.sear", 3f, sear > 0.001f ? SecretFire.SearOverlay(src.sprite) : null, alpha * sear);
        }

        /// <summary>One more picture of the marks over the overlay, <paramref name="depth"/> nudges
        /// toward the camera. Off when there is nothing to draw.</summary>
        SpriteRenderer Layer(SpriteRenderer sr, string name, float depth, Sprite sprite, float a)
        {
            var src = _source;
            if (sprite == null || a <= 0.001f)
            {
                if (sr != null && sr.enabled) sr.enabled = false;
                return sr;
            }
            if (sr == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(src.transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -Nudge * depth);
                sr = go.AddComponent<SpriteRenderer>();
            }
            sr.sprite = sprite;
            sr.sharedMaterial = src.sharedMaterial;
            sr.flipX = src.flipX;
            sr.flipY = src.flipY;
            sr.sortingLayerID = src.sortingLayerID;
            sr.sortingOrder = src.sortingOrder;
            var c = src.color;
            sr.color = new Color(c.r, c.g, c.b, c.a * a);
            sr.enabled = true;
            return sr;
        }

        void OnDisable()
        {
            if (_glow != null) _glow.enabled = false;
            if (_hot != null) _hot.enabled = false;
            if (_sear != null) _sear.enabled = false;
        }
    }
}
