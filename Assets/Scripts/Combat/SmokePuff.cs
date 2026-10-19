using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// A small puff of smoke - a projectile the orbiting discs stopped (DiscOrbit). A few grey
    /// blobs that swell, drift up and fade, on the texel grid. Scaled time.
    /// </summary>
    public class SmokePuff : MonoBehaviour
    {
        const float Seconds = 0.38f;
        const int Blobs = 5;

        SpriteRenderer[] _srs;
        Vector2[] _drift;
        float[] _size;
        float _age;

        public static void At(Vector2 at)
        {
            var go = new GameObject("smoke.puff");
            go.transform.position = at;
            var p = go.AddComponent<SmokePuff>();
            p._srs = new SpriteRenderer[Blobs];
            p._drift = new Vector2[Blobs];
            p._size = new float[Blobs];
            for (int i = 0; i < Blobs; i++)
            {
                var b = new GameObject("blob");
                b.transform.SetParent(go.transform, false);
                var sr = b.AddComponent<SpriteRenderer>();
                sr.sprite = Spr.Circle;
                float g = Random.Range(0.55f, 0.78f);
                sr.color = new Color(g, g, g * 1.02f, 0.85f);
                sr.sortingOrder = SortingOrders.Fx + 2;
                p._srs[i] = sr;
                float a = i * Mathf.PI * 2f / Blobs + Random.Range(-0.4f, 0.4f);
                p._drift[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * Random.Range(0.10f, 0.18f) + Vector2.up * 0.18f;
                p._size[i] = Random.Range(0.10f, 0.16f);
            }
            p.Lay(0f);
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= Seconds) { Destroy(gameObject); return; }
            Lay(_age / Seconds);
        }

        void Lay(float t)
        {
            float ease = 1f - (1f - t) * (1f - t);
            for (int i = 0; i < _srs.Length; i++)
            {
                var sr = _srs[i];
                if (sr == null) continue;
                var p = _drift[i] * ease;
                sr.transform.localPosition = new Vector3(Mathf.Round(p.x * 37.5f) / 37.5f, Mathf.Round(p.y * 37.5f) / 37.5f, 0f);
                sr.transform.localScale = Vector3.one * _size[i] * Mathf.Lerp(0.8f, 1.7f, ease);
                var c = sr.color; c.a = 0.85f * (1f - t); sr.color = c;
            }
        }
    }
}
