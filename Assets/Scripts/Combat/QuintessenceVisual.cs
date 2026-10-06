using System.Collections;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// The picture of the Armillary's Quintessence: the two halves rise out of the hands and meet
    /// OVERHEAD, become the whole four-element armillary there (its bands still turning), hold
    /// while the finisher strikes, then part and drop back to the hands.
    ///
    /// A picture only - PlayerController.QuintessenceStrike empties and refills the hands and
    /// resolves every hit. Hung off the player's root, not the rig, so the character's lean and
    /// swing cannot tip it; it follows the player as they move through the hold.
    /// </summary>
    public class QuintessenceVisual : MonoBehaviour
    {
        SpriteRenderer _a, _b, _whole;
        Sprite[] _frames;
        float _frameSeconds;

        public static QuintessenceVisual Play(Transform player, Art.Gear.ICharacterRig rig, Sprite[] wholeFrames,
                                              float frameSeconds, float holdSeconds)
        {
            var go = new GameObject("quintessence");
            go.transform.SetParent(player, false);
            var v = go.AddComponent<QuintessenceVisual>();
            v._frames = wholeFrames;
            v._frameSeconds = Mathf.Max(0.02f, frameSeconds);
            v.StartCoroutine(v.Run(rig, holdSeconds));
            return v;
        }

        static SpriteRenderer Piece(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Scale a renderer so its sprite draws at <paramref name="world"/> units tall.</summary>
        static void SizeTo(SpriteRenderer sr, float world)
        {
            if (sr.sprite == null) return;
            float native = Mathf.Max(0.0001f, sr.sprite.bounds.size.y);
            sr.transform.localScale = Vector3.one * (world / native);
        }

        IEnumerator Run(Art.Gear.ICharacterRig rig, float holdSeconds)
        {
            Sprite main = null, off = null;
            Vector2 size = Vector2.one * 0.4f;
            Color tint = Color.white;
            if (rig != null)
            {
                rig.TryGetDiscVisual(0, out main, out tint, out size);
                rig.TryGetDiscVisual(1, out off, out _, out _);
            }
            float handSize = Mathf.Max(size.x, size.y);
            var up = new Vector3(0f, Tuning.Quintessence.OverheadHeight, 0f);

            // Where each half starts: the main hand's disc, and its twin across the body.
            var hand = rig?.WeaponRenderer != null
                ? transform.InverseTransformPoint(rig.WeaponRenderer.transform.position)
                : new Vector3(-0.2f, 0.35f, 0f);
            var twin = new Vector3(-hand.x, hand.y, 0f);

            _a = Piece(transform, "half.main", SortingOrders.Fx - 1);
            _b = Piece(transform, "half.off", SortingOrders.Fx - 2);
            _a.sprite = main; _b.sprite = off != null ? off : main;
            _a.color = _b.color = tint;
            SizeTo(_a, handSize); SizeTo(_b, handSize);

            // ---- combine: both halves rise and meet overhead, growing into the whole
            for (float t = 0f; t < Tuning.Quintessence.MergeSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / Tuning.Quintessence.MergeSeconds);
                _a.transform.localPosition = Vector3.Lerp(hand, up, k);
                _b.transform.localPosition = Vector3.Lerp(twin, up, k);
                float s = Mathf.Lerp(1f, Tuning.Quintessence.OverheadScale, k);
                SizeTo(_a, handSize * s); SizeTo(_b, handSize * s);
                yield return null;
            }
            Destroy(_a.gameObject); Destroy(_b.gameObject);

            // ---- whole, overhead, its bands still turning
            _whole = Piece(transform, "whole", SortingOrders.Fx - 1);
            _whole.transform.localPosition = up;
            _whole.color = tint;
            Spr.Flash(transform.position + up, 0.55f, Color.white, 0.18f);
            float held = 0f;
            while (held < holdSeconds)
            {
                if (_frames != null && _frames.Length > 0)
                    _whole.sprite = _frames[Mathf.FloorToInt(held / _frameSeconds) % _frames.Length];
                else _whole.sprite = main;
                // the whole is drawn on its own (larger) grid - sized by the grid, so the frame
                // and the halves share one texel scale
                if (_whole.sprite != null && main != null)
                    _whole.transform.localScale = Vector3.one * (handSize / Mathf.Max(0.0001f, main.bounds.size.y))
                                                  * Tuning.Quintessence.OverheadScale;
                held += Time.deltaTime;
                yield return null;
            }

            // ---- part: the whole splits back into its halves and they drop to the hands
            Destroy(_whole.gameObject);
            _a = Piece(transform, "half.main", SortingOrders.Fx - 1);
            _b = Piece(transform, "half.off", SortingOrders.Fx - 2);
            _a.sprite = main; _b.sprite = off != null ? off : main;
            _a.color = _b.color = tint;
            for (float t = 0f; t < Tuning.Quintessence.SplitSeconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / Tuning.Quintessence.SplitSeconds);
                _a.transform.localPosition = Vector3.Lerp(up, hand, k);
                _b.transform.localPosition = Vector3.Lerp(up, twin, k);
                float s = Mathf.Lerp(Tuning.Quintessence.OverheadScale, 1f, k);
                SizeTo(_a, handSize * s); SizeTo(_b, handSize * s);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
