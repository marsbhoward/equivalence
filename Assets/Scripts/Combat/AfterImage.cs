using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// A fading copy of a sprite left where it was (the user's pick, group 2 - "after-images"):
    /// Wanderblade's turning blade drops one every few frames as it flies. The sprite's flat
    /// silhouette (PixelSprite.Silhouette), tinted, at its pose that frame, fading quickly, one
    /// order under the original. A picture only.
    /// </summary>
    public class AfterImage : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _sr;
        [SerializeField] Color _tint;
        [SerializeField] float _life, _left;

        public static void Of(SpriteRenderer src, Color tint, float seconds)
        {
            if (src == null || src.sprite == null) return;
            var go = new GameObject("afterimage");
            var t = src.transform;
            go.transform.SetPositionAndRotation(t.position, t.rotation);
            go.transform.localScale = t.lossyScale;
            var a = go.AddComponent<AfterImage>();
            a._sr = go.AddComponent<SpriteRenderer>();
            a._sr.sprite = Art.PixelSprite.Silhouette(src.sprite);
            a._sr.flipX = src.flipX; a._sr.flipY = src.flipY;
            a._sr.sortingLayerID = src.sortingLayerID;
            a._sr.sortingOrder = src.sortingOrder - 1;
            a._tint = tint; a._life = a._left = Mathf.Max(0.01f, seconds);
            a._sr.color = tint;
        }

        void Update()
        {
            _left -= Time.deltaTime;
            if (_left <= 0f || _sr == null) { Destroy(gameObject); return; }
            float k = _left / _life;
            var c = _tint; c.a *= k * k;   // holds, then goes - DashTrail's fade
            _sr.color = c;
        }
    }
}
