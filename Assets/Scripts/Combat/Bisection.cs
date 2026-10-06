using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// A body cut cleanly in two: the death a non-boss enemy dies when the katana signature's
    /// draw-cut (<see cref="DamageInfo.Bisects"/>) finishes it.
    ///
    /// The cut is VERTICAL and the two halves SHEAR past each other - the left slides a little
    /// down, the right a little up - which is what "cut in half" reads as at a glance, and matches
    /// how a real draw-cut lands. Deliberately small travel and a short life: a shear, not an
    /// explosion. The halves are real sub-sprites of the body's own art (SigilDoor's slice trick -
    /// Sprite.Create on a half-width rect pivoted on the seam), so a chaser splits like a chaser
    /// and a turret like a turret, for free.
    ///
    /// Self-contained like RiftImplosion: a static factory captures everything it needs from the
    /// dying body, then it animates itself and Destroys itself. It never touches the enemy, which
    /// HookDeath is about to destroy anyway.
    /// </summary>
    public class Bisection : MonoBehaviour
    {
        Transform _left, _right;
        SpriteRenderer _leftSr, _rightSr, _seam;
        Color _bodyColor;
        Color _tint;
        float _t;
        float _life;
        float _slide;

        /// <param name="body">The dying enemy's root. Its "visual" child supplies the sprite,
        /// colour, world scale and rotation the halves are built from.</param>
        /// <param name="tint">The katana's own colour, for the cut-line flash along the seam.</param>
        public static Bisection At(Transform body, Color tint)
        {
            if (body == null) return null;

            var vt = body.Find("visual");
            var src = vt != null ? vt.GetComponentInChildren<SpriteRenderer>()
                                 : body.GetComponentInChildren<SpriteRenderer>();

            var go = new GameObject("bisection");
            go.transform.position = src != null ? src.transform.position : body.position;
            go.transform.rotation = src != null ? src.transform.rotation : body.rotation;
            // The visual child carries the body's size (ArtBinder normalises height onto it), so
            // the halves inherit it by sitting under a root scaled to match.
            go.transform.localScale = src != null ? src.transform.lossyScale : Vector3.one;

            var e = go.AddComponent<Bisection>();
            e._tint = tint;
            e._life = Mathf.Max(0.1f, Tuning.Katana.BisectSeconds);
            e._slide = Tuning.Katana.BisectSlide;
            e._bodyColor = src != null ? src.color : new Color(0.8f, 0.8f, 0.85f, 1f);

            Sprite sprite = src != null ? src.sprite : null;
            int order = src != null ? src.sortingOrder : SortingOrders.Enemy;

            e._left = e.MakeHalf("half.left", sprite, left: true, order);
            e._right = e.MakeHalf("half.right", sprite, left: false, order);
            e._leftSr = e._left.GetComponent<SpriteRenderer>();
            e._rightSr = e._right.GetComponent<SpriteRenderer>();

            // The seam: a thin bright line down the cut, gone almost at once.
            var seamGo = new GameObject("seam");
            seamGo.transform.SetParent(go.transform, false);
            e._seam = seamGo.AddComponent<SpriteRenderer>();
            e._seam.sprite = Spr.Capsule;
            e._seam.sortingOrder = order + 1;
            float h = HalfSpanY(sprite);
            seamGo.transform.localScale = new Vector3(0.06f, h * 2.1f, 1f);

            return e;
        }

        Transform MakeHalf(string name, Sprite whole, bool left, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.color = _bodyColor;
            sr.sortingOrder = order;

            if (whole != null && whole.texture != null)
            {
                var tex = whole.texture;
                var r = whole.textureRect;   // this sprite's own island in its atlas/texture
                float halfW = r.width * 0.5f;
                var rect = left ? new Rect(r.x, r.y, halfW, r.height)
                                : new Rect(r.x + halfW, r.y, halfW, r.height);
                // Pivot on the CUT edge, so each half hangs off the centre line and the pair
                // reconstructs the body exactly - the same reason SigilDoor's leaves pivot on
                // their seam.
                var pivot = left ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
                sr.sprite = Sprite.Create(tex, rect, pivot, whole.pixelsPerUnit);
            }
            else
            {
                // No readable body art: a plain half-disc still reads as "half of something".
                sr.sprite = Spr.HalfDisc;
                go.transform.localRotation = Quaternion.Euler(0f, 0f, left ? 90f : -90f);
            }
            return go.transform;
        }

        static float HalfSpanY(Sprite s)
            => s != null ? s.bounds.extents.y : 0.5f;

        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _life);
            if (k >= 1f) { Destroy(gameObject); return; }

            // Eased out - the halves are fastest at the moment of the cut and settle as they part,
            // which is what a slice looks like versus a shove.
            float e = 1f - (1f - k) * (1f - k);

            _left.localPosition = new Vector3(-_slide * 0.35f * e, -_slide * e, 0f);
            _left.localRotation = Quaternion.Euler(0f, 0f, 4f * e);
            _right.localPosition = new Vector3(_slide * 0.35f * e, _slide * e, 0f);
            _right.localRotation = Quaternion.Euler(0f, 0f, -4f * e);

            // Hold opaque, then fade over the last third - the halves are the point, the
            // disappearance is just cleanup.
            float a = 1f - Mathf.Clamp01((k - 0.66f) / 0.34f);
            var lc = _bodyColor; lc.a *= a; _leftSr.color = lc; _rightSr.color = lc;

            // The seam is bright for a blink and then gone.
            float seam = Mathf.Clamp01(1f - k / 0.28f);
            var sc = _tint; sc.a = 0.9f * seam;
            _seam.color = sc;
        }
    }
}
