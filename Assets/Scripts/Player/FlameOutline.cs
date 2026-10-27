using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.ReleaseFx;

namespace Convergence.Player
{
    /// <summary>
    /// IGNITE's opening (the user's pick, 2026-10-25 - "flame outline"): for a moment the whole
    /// figure is outlined in flame, flames standing on the head, then it breaks up and burns
    /// down - the window itself is the flaming swings and trails.
    ///
    /// The outline is TargetHighlight's rim: a one-texel rim of every rig layer, laid on that
    /// layer's own transform and sorted ONE BELOW the rig's lowest layer, so the seams between
    /// parts are covered by the body and only the figure's outer edge shows. The head's flames
    /// stand on the highest layer's top texels, on its grid, sorted the same way. Nothing is ever
    /// drawn over the player.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class FlameOutline : MonoBehaviour
    {
        [SerializeField] float _startedAt = -99f;
        [SerializeField] SpriteRenderer _head;
        List<SpriteRenderer> _sources, _rims;
        Texture2D _tex;
        Color32[] _px;
        int _w, _h;
        Sprite _from;

        public static void Play(Transform player)
        {
            var o = player.GetComponent<FlameOutline>();
            if (o == null) o = player.gameObject.AddComponent<FlameOutline>();
            o._startedAt = Time.time;
        }

        void LateUpdate()
        {
            _sources ??= new List<SpriteRenderer>();
            _rims ??= new List<SpriteRenderer>();
            float age = Time.time - _startedAt;
            if (age < 0f || age > T.OutlineSeconds) { Hide(0); if (_head != null) _head.enabled = false; return; }

            var rig = GetComponentInChildren<Art.Gear.ICharacterRig>() as MonoBehaviour;
            if (rig == null) { Hide(0); return; }
            var weapon = ((Art.Gear.ICharacterRig)rig).WeaponRenderer;
            var off = ((Art.Gear.ICharacterRig)rig).OffhandRenderer;

            _sources.Clear();
            foreach (var sr in rig.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (sr == null || !sr.enabled || sr.sprite == null) continue;
                if (sr.sortingOrder >= SortingOrders.StatusOverlay || sr.name == HitFlash.ChildName) continue;
                _sources.Add(sr);
            }
            int lowest = int.MaxValue;
            SpriteRenderer top = null;
            foreach (var s in _sources)
            {
                lowest = Mathf.Min(lowest, s.sortingOrder);
                if (s == weapon || s == off || s.forceRenderingOff) continue;
                if (top == null || s.bounds.max.y > top.bounds.max.y) top = s;
            }
            if (_sources.Count == 0) { Hide(0); return; }

            // Solid, then breaking up: alternating with a dashed rim, then dashed alone.
            float k = age / T.OutlineSeconds;
            int frame = Mathf.FloorToInt(Time.time * 12f);
            bool dashed = age > T.OutlineSolidSeconds && (k > 0.7f || frame % 2 == 1);
            var colour = (Color)FireArt.At(0.2f + k * 0.5f + (FireArt.Hash(frame, 3) > 0.5f ? 0.15f : 0f));   // yellow first, never white
            colour.a = 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);

            int used = 0;
            foreach (var src in _sources)
            {
                var st = src.transform;
                var scale = st.lossyScale;
                float texelWorld = Mathf.Abs(scale.y) / Mathf.Max(1f, src.sprite.pixelsPerUnit);
                int pad = Mathf.Max(1, Mathf.RoundToInt(Tuning.Highlight.Thickness / Mathf.Max(1e-5f, texelWorld)));
                var rimSprite = Art.PixelSprite.Rim(src.sprite, pad, dashed);
                if (rimSprite == null) continue;
                var rim = RimAt(used++);
                rim.sprite = rimSprite;
                rim.flipX = src.flipX; rim.flipY = src.flipY;
                rim.color = colour;
                rim.sortingLayerID = src.sortingLayerID;
                rim.sortingOrder = lowest - 1;
                rim.transform.SetPositionAndRotation(st.position, st.rotation);
                rim.transform.localScale = scale;
                rim.enabled = true;
            }
            Hide(used);
            HeadFlames(top, lowest - 1, Mathf.RoundToInt(T.OutlineHeadFlame * (1f - k)));
        }

        /// <summary>Flames on the columns that top out near the highest texel of the highest
        /// layer (the head, a helm, a hood) - BurnEmbers' head flames, on the player. A canvas on
        /// that layer's own transform, pivot and flip, so it leans and turns with the head.</summary>
        void HeadFlames(SpriteRenderer top, int order, int height)
        {
            if (_head == null) _head = new GameObject("fire.ignite.head").AddComponent<SpriteRenderer>();
            if (top == null || height < 1) { _head.enabled = false; return; }

            var sprite = top.sprite;
            var p = BurnEmbers.ProfileOf(sprite);
            int rows = T.OutlineHeadRows;
            int baseRow = p.Highest - (rows - 1) + 1;   // the lowest column a flame may stand on, plus one
            int w = p.W, h = T.OutlineHeadFlame + rows;
            if (_tex == null || _from != sprite)
            {
                if (_head.sprite != null) Destroy(_head.sprite);
                if (_tex == null || _w != w || _h != h)
                {
                    if (_tex != null) Destroy(_tex);
                    _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                    };
                    _px = new Color32[w * h];
                    _w = w; _h = h;
                }
                var pivot = sprite.pivot;   // in texels, from the sprite rect's corner
                _head.sprite = Sprite.Create(_tex, new Rect(0, 0, w, h),
                                             new Vector2(pivot.x / w, (pivot.y - baseRow) / h), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                _from = sprite;
            }
            System.Array.Clear(_px, 0, _px.Length);
            for (int col = 0; col < w; col++)
            {
                int t = p.Top[col];
                if (t < 0 || t < baseRow - 1) continue;
                for (int i = 1; i <= height; i++)
                {
                    int y = t + i - baseRow;
                    if (y < 0 || y >= h) continue;
                    if (FireArt.Tongue(col, i, height, Time.time, 41, out var c)) _px[y * w + col] = c;
                }
            }
            _tex.SetPixels32(_px);
            _tex.Apply();

            var st = top.transform;
            _head.transform.SetPositionAndRotation(st.position, st.rotation);
            _head.transform.localScale = st.lossyScale;
            _head.flipX = top.flipX; _head.flipY = top.flipY;
            _head.sortingLayerID = top.sortingLayerID;
            _head.sortingOrder = order;
            _head.enabled = true;
        }

        SpriteRenderer RimAt(int i)
        {
            while (_rims.Count <= i) _rims.Add(null);
            if (_rims[i] != null) return _rims[i];
            var go = new GameObject("fire.ignite.rim");
            var sr = go.AddComponent<SpriteRenderer>();
            _rims[i] = sr;
            return sr;
        }

        void Hide(int from)
        {
            if (_rims == null) return;
            for (int i = from; i < _rims.Count; i++)
                if (_rims[i] != null && _rims[i].enabled) _rims[i].enabled = false;
        }

        void OnDestroy()
        {
            if (_rims != null) foreach (var r in _rims) if (r != null) Destroy(r.gameObject);
            if (_head != null) { if (_head.sprite != null) Destroy(_head.sprite); Destroy(_head.gameObject); }
            if (_tex != null) Destroy(_tex);
        }
    }
}
