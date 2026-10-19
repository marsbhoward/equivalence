using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// A Gale Hound's dash leaves its wind lying on the floor along the line it took (the
    /// reference clip's trail of fire, the user's call, 2026-10-18): three lanes of broken pale
    /// streaks, a fleck of the acid green in the middle one, brighter toward where it went. Laid
    /// as the dash runs, faded out over <see cref="Tuning.Enemy.DasherTrailFadeSeconds"/> once it
    /// ends. Purely the picture.
    ///
    /// Its own object (ClusterShard's reason): a Dasher that dies mid-dash still leaves its line.
    /// One axis-aligned texture on the texel grid at the ground-decal layer, repainted only when
    /// the dash has carried it a texel further.
    /// </summary>
    public class DasherTrail : MonoBehaviour
    {
        const int Margin = 3;
        /// <summary>A dash nobody has extended for this long has ended (its Dasher died).</summary>
        const float OrphanSeconds = 0.1f;

        SpriteRenderer _sr;
        Texture2D _tex;
        Color[] _px;
        Vector2 _from, _to;
        int _seed;
        float _endedAt = -1f, _lastExtend;

        public static DasherTrail Begin(Transform parent, Vector2 from, int seed)
        {
            var go = new GameObject("dasher.trail");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<DasherTrail>();
            t._sr = go.AddComponent<SpriteRenderer>();
            t._sr.sortingOrder = SortingOrders.GroundDecal;
            t._from = t._to = from;
            t._seed = seed;
            t._lastExtend = Time.time;
            return t;
        }

        /// <summary>The dash has reached <paramref name="to"/>.</summary>
        public void Extend(Vector2 to)
        {
            _lastExtend = Time.time;
            if ((to - _to).sqrMagnitude * TexelRig.TexelsPerUnit * TexelRig.TexelsPerUnit < 1f) return;
            _to = to;
            Paint();
        }

        public void End() { if (_endedAt < 0f) _endedAt = Time.time; }

        void Update()
        {
            if (_endedAt < 0f && Time.time - _lastExtend > OrphanSeconds) End();
            if (_endedAt < 0f || _sr == null) return;
            float a = 1f - (Time.time - _endedAt) / Tuning.Enemy.DasherTrailFadeSeconds;
            if (a <= 0f) { Destroy(gameObject); return; }
            _sr.color = new Color(1f, 1f, 1f, a);
        }

        void Paint()
        {
            float tpu = TexelRig.TexelsPerUnit;
            Vector2 a = new(Mathf.Round(_from.x * tpu), Mathf.Round(_from.y * tpu)), b = new(Mathf.Round(_to.x * tpu), Mathf.Round(_to.y * tpu));
            var d = b - a;
            float len = d.magnitude;
            if (len < 1f) return;
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x)) - Margin, y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y)) - Margin;
            int w = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x)) - x0 + Margin + 1, 2, 2048);
            int h = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y)) - y0 + Margin + 1, 2, 2048);

            if (_tex == null || _tex.width != w || _tex.height != h)
            {
                if (_tex != null) Destroy(_tex);
                _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _px = new Color[w * h];
            }
            else System.Array.Clear(_px, 0, _px.Length);

            var ramp = DasherArt.WindRamp;
            Vector2 u = d / len, n = new(-u.y, u.x);
            for (int lane = -1; lane <= 1; lane++)
            for (int s = 0; s < len; s++)
            {
                if (TexelRig.Hash(Mathf.FloorToInt((s + lane * 5) / 4f), _seed + lane) < 0.3f) continue;
                float fade = 0.35f + 0.65f * s / len;
                var c = s % 9 < 2 && lane == 0 ? ramp[1] : ramp[0];
                c.a = fade * (lane != 0 ? 0.55f : 0.8f);
                int x = Mathf.RoundToInt(a.x + u.x * s + n.x * lane * 2) - x0, y = Mathf.RoundToInt(a.y + u.y * s + n.y * lane * 2) - y0;
                if (x >= 0 && y >= 0 && x < w && y < h) _px[y * w + x] = c;
            }
            _tex.SetPixels(_px);
            _tex.Apply();
            if (_sr.sprite != null && _sr.sprite.texture != _tex) Destroy(_sr.sprite);
            if (_sr.sprite == null || _sr.sprite.texture != _tex)
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, w, h), Vector2.zero, tpu, 0, SpriteMeshType.FullRect);
            transform.position = new Vector3(x0 / tpu, y0 / tpu, 0f);
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
