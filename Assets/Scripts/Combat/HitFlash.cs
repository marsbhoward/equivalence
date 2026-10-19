using UnityEngine;
using Convergence.Art;

namespace Convergence.Combat
{
    /// <summary>
    /// A hit flash for BAKED-COLOUR sprites: a white silhouette of whatever the renderer shows,
    /// laid over it and faded by <see cref="Amount"/>.
    ///
    /// Health's old fallback lerped the renderer's colour toward white, which works on a tinted
    /// placeholder circle and does nothing on pixel art (the colour is a MULTIPLY and the art is
    /// drawn at white - see PixelSprite). Swapping the sprite itself, the rig's way, would fight
    /// every stage driver that sets the sprite each frame (EnemyStageCycle, ChaserVisual), so the
    /// flash is a separate picture over the top instead: it follows whatever frame, mirror and
    /// order the body has this frame, and the body's own drivers never know it is there.
    ///
    /// Sorted at the source's OWN order, nudged a thousandth toward the camera - KindledMarks'
    /// rule, for the same reason (source + 1 can be some other layer). Runs late so it copies the
    /// order DepthSorted has just written.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class HitFlash : MonoBehaviour
    {
        public const string ChildName = "hit.flash";
        const float Nudge = 0.001f;

        [SerializeField] SpriteRenderer _src;
        [SerializeField] SpriteRenderer _over;

        /// <summary>0 = no flash, 1 = solid white. Written by Health each frame it flashes.</summary>
        [System.NonSerialized] public float Amount;

        public static HitFlash For(SpriteRenderer src)
        {
            var f = src.GetComponent<HitFlash>();
            if (f != null) return f;

            f = src.gameObject.AddComponent<HitFlash>();
            f._src = src;
            var go = new GameObject(ChildName);
            go.transform.SetParent(src.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -Nudge);
            f._over = go.AddComponent<SpriteRenderer>();
            f._over.enabled = false;
            return f;
        }

        void LateUpdate()
        {
            if (_src == null || _over == null) return;

            bool show = Amount > 0.001f && _src.enabled && !_src.forceRenderingOff && _src.sprite != null
                        && _src.gameObject.activeInHierarchy;
            _over.enabled = show;
            if (!show) return;

            _over.sprite = PixelSprite.Silhouette(_src.sprite);
            _over.flipX = _src.flipX;
            _over.flipY = _src.flipY;
            _over.sortingLayerID = _src.sortingLayerID;
            _over.sortingOrder = _src.sortingOrder;
            _over.color = new Color(1f, 1f, 1f, Mathf.Clamp01(Amount) * _src.color.a);
        }
    }
}
