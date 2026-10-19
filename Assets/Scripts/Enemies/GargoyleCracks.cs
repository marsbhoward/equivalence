using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// An elite Gargoyle's slam edge, cracked into the floor (the user's call, 2026-10-18): violet
    /// cracks creep out from under it through the gaze and reach the slam's radius as it ends, a
    /// broken ring marking the edge; the slam lights every crack white-hot and the whole of it
    /// fades. Before this nothing showed how far the slam reached.
    ///
    /// Centred on the body's position, which is what the slam measures from, at exactly
    /// <see cref="Tuning.Enemy.GargoyleAoeRadius"/>. One texture on the texel grid at the
    /// ground-decal layer, repainted as the cracks grow. Its own object, so a Gargoyle killed in
    /// the gaze leaves its cracks to fade.
    /// </summary>
    public class GargoyleCracks : MonoBehaviour
    {
        const int Cracks = 18;
        const float BurstFadeSeconds = 0.5f, CancelFadeSeconds = 0.3f;

        SpriteRenderer _sr;
        Texture2D _tex;
        Color[] _px;
        int _size;
        float _radius, _shown = -1f, _fadeFrom = -1f, _fadeSeconds;

        public static GargoyleCracks Create(Transform parent, Vector2 centre)
        {
            var go = new GameObject("gargoyle.cracks");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<GargoyleCracks>();
            c._sr = go.AddComponent<SpriteRenderer>();
            c._sr.sortingOrder = SortingOrders.GroundDecal;
            float tpu = TexelRig.TexelsPerUnit;
            c._radius = Tuning.Enemy.GargoyleAoeRadius * tpu;
            c._size = Mathf.CeilToInt(c._radius) * 2 + 4;
            var o = new Vector2(Mathf.Round(centre.x * tpu), Mathf.Round(centre.y * tpu)) - Vector2.one * (c._size / 2);
            go.transform.position = new Vector3(o.x / tpu, o.y / tpu, 0f);
            return c;
        }

        /// <summary>The gaze is <paramref name="k"/> (0..1) of the way through.</summary>
        public void Grow(float k)
        {
            if (_fadeFrom >= 0f || Mathf.Abs(k - _shown) < 0.02f) return;
            _shown = k;
            Paint(k, false);
        }

        /// <summary>The slam: every crack white-hot, then gone.</summary>
        public void Burst()
        {
            if (_fadeFrom >= 0f) return;
            Paint(1f, true);
            Fade(BurstFadeSeconds);
        }

        /// <summary>The gaze was broken off: the cracks fade where they stopped.</summary>
        public void Cancel() => Fade(CancelFadeSeconds);

        void Fade(float seconds)
        {
            if (_fadeFrom >= 0f) return;
            _fadeFrom = Time.time;
            _fadeSeconds = seconds;
        }

        void Update()
        {
            if (_fadeFrom < 0f || _sr == null) return;
            float a = 1f - (Time.time - _fadeFrom) / _fadeSeconds;
            if (a <= 0f) { Destroy(gameObject); return; }
            _sr.color = new Color(1f, 1f, 1f, a);
        }

        void Paint(float k, bool burst)
        {
            if (_tex == null)
            {
                _tex = new Texture2D(_size, _size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _px = new Color[_size * _size];
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, _size, _size), Vector2.zero, TexelRig.TexelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            else System.Array.Clear(_px, 0, _px.Length);

            var acc = GargoyleArt.Accent;
            float c = _size / 2f, R = _radius;
            for (int i = 0; i < Cracks; i++)
            {
                // each crack wanders out from under it, a little further every frame of the gaze
                float a = i / (float)Cracks * Mathf.PI * 2f + TexelRig.Hash(i, 3) * 0.3f, r = 3f;
                float reach = R * Mathf.Min(1f, k * 1.15f) * (0.9f + TexelRig.Hash(i, 7) * 0.1f);
                while (r < reach)
                {
                    Put(c + Mathf.Cos(a) * r, c + Mathf.Sin(a) * r, burst ? acc[3] : r > reach - 3f ? acc[2] : acc[1], 1f);
                    a += (TexelRig.Hash(i, Mathf.FloorToInt(r)) - 0.5f) * 0.25f;
                    r += 1f;
                }
            }
            // the edge itself, broken, once the cracks reach it
            if (k >= 1f || burst)
                for (int d = 0; d < 360; d += 2)
                {
                    if (!burst && (d / 9) % 2 == 1) continue;
                    Put(c + Mathf.Cos(TexelRig.Rad(d)) * R, c + Mathf.Sin(TexelRig.Rad(d)) * R, burst ? acc[3] : acc[2], burst ? 0.9f : 0.6f);
                }
            _tex.SetPixels(_px);
            _tex.Apply();
        }

        void Put(float x, float y, Color col, float alpha)
        {
            int ix = Mathf.RoundToInt(x), iy = Mathf.RoundToInt(y);
            if (ix < 0 || iy < 0 || ix >= _size || iy >= _size) return;
            col.a = alpha;
            _px[iy * _size + ix] = col;
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
