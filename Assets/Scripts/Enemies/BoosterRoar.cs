using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// The Green Lion's roar, seen (the user's sign-off, 2026-10-19): three arcs of green sound
    /// rolling out of the jaws toward each ally it reached, fading as they go, and a ring
    /// flashing round the ally as the front arrives. The buff itself landed at the roar - this is
    /// only the picture of it, so the waves never warn of anything.
    ///
    /// One texture on the texel grid, sized at the roar to the jaws and every ally plus a margin,
    /// redrawn each frame; its own object, so a lion killed as it roars still shows it.
    /// </summary>
    public class BoosterRoar : MonoBehaviour
    {
        const float WaveSeconds = 0.6f, RingSeconds = 0.3f;
        const float HalfSpanDeg = 26f, Pad = 22f;   // texels round the jaws and each ally

        SpriteRenderer _sr;
        Texture2D _tex;
        Color[] _px;
        int _w, _h;
        Vector2 _mouth;                       // texels, in the canvas
        Vector2[] _to = new Vector2[2];       // texels, in the canvas
        int _count;
        float _born;

        public static void Create(Transform parent, Vector2 mouth, EnemyController a, EnemyController b)
        {
            if (a == null) return;
            var go = new GameObject("booster.roar");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<BoosterRoar>();
            r.Begin(mouth, a, b);
        }

        void Begin(Vector2 mouth, EnemyController a, EnemyController b)
        {
            float tpu = TexelRig.TexelsPerUnit;
            Vector2 T(Vector2 w) => new(Mathf.Round(w.x * tpu), Mathf.Round(w.y * tpu));
            var m = T(mouth);
            var ends = new System.Collections.Generic.List<Vector2>();
            foreach (var e in new[] { a, b }) if (e != null) ends.Add(T(e.transform.position));

            Vector2 lo = m, hi = m;
            foreach (var e in ends) { lo = Vector2.Min(lo, e); hi = Vector2.Max(hi, e); }
            lo -= Vector2.one * Pad;
            hi += Vector2.one * Pad;
            _w = Mathf.CeilToInt(hi.x - lo.x);
            _h = Mathf.CeilToInt(hi.y - lo.y);
            _mouth = m - lo;
            _count = ends.Count;
            for (int i = 0; i < _count; i++) _to[i] = ends[i] - lo;

            transform.position = new Vector3(lo.x / tpu, lo.y / tpu, 0f);
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = SortingOrders.Telegraph;
            _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            _px = new Color[_w * _h];
            _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, _w, _h), Vector2.zero, tpu, 0, SpriteMeshType.FullRect);
            _born = Time.time;
            Paint();
        }

        void Update()
        {
            if (_tex == null) return;
            if (Time.time - _born > WaveSeconds + RingSeconds) { Destroy(gameObject); return; }
            Paint();
        }

        void Paint()
        {
            System.Array.Clear(_px, 0, _px.Length);
            float k = (Time.time - _born) / WaveSeconds;
            var acc = BoosterArt.Accent;
            for (int e = 0; e < _count; e++)
            {
                var d = _to[e] - _mouth;
                float reach = Mathf.Max(20f, d.magnitude), dir = Mathf.Atan2(d.y, d.x);
                for (int i = 0; i < 3; i++)
                {
                    float ph = k * 1.3f - i * 0.18f;
                    if (ph <= 0f || ph >= 1f) continue;
                    float R = 4f + ph * reach, al = (1f - ph) * 0.9f, step = 1.2f / R;
                    float span = TexelRig.Rad(HalfSpanDeg);
                    int n = 0;
                    for (float a = -span; a <= span; a += step, n++)
                    {
                        if (i == 2 && n % 3 == 0) continue;   // the last wave broken up
                        float edge = Mathf.Abs(a) > span * 0.77f ? 0.5f : 1f;
                        Put(_mouth.x + Mathf.Cos(dir + a) * R, _mouth.y + Mathf.Sin(dir + a) * R, i == 0 ? acc[3] : acc[2], al * edge);
                    }
                }
                // the ring round the ally as the front reaches it
                float rk = (Time.time - _born - WaveSeconds * 0.75f) / RingSeconds;
                if (rk > 0f && rk < 1f)
                {
                    float R = 4f + rk * 14f;
                    for (int deg = 0; deg < 360; deg += 8)
                        Put(_to[e].x + Mathf.Cos(TexelRig.Rad(deg)) * R, _to[e].y + Mathf.Sin(TexelRig.Rad(deg)) * R,
                            rk < 0.5f ? acc[3] : acc[2], 1f - rk);
                }
            }
            _tex.SetPixels(_px);
            _tex.Apply();
        }

        void Put(float x, float y, Color c, float alpha)
        {
            int ix = Mathf.RoundToInt(x), iy = Mathf.RoundToInt(y);
            if (ix < 0 || iy < 0 || ix >= _w || iy >= _h) return;
            c.a = alpha;
            _px[iy * _w + ix] = c;
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
