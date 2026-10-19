using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// The Tempest Eye's beam, drawn as LIGHTNING (the user's call, 2026-10-18): a jagged line
    /// from the eye to its target, re-rolled every <see cref="RerollSeconds"/>, a glow under a
    /// bright core under a white-hot centre, in the look's accent ramp. Overcharged, it thickens
    /// and throws two forks.
    ///
    /// On the texel grid like everything else: each re-roll paints one axis-aligned texture round
    /// the arc at <see cref="TexelRig.TexelsPerUnit"/>, point filtered, so it never smears into a
    /// soft line beside the pixel bodies. Purely the picture - the beam's damage and its line of
    /// sight stay EnemyController's, measured from the body's centre as they always were.
    /// </summary>
    public class LightningArc : MonoBehaviour
    {
        const float RerollSeconds = 0.05f;
        const int Margin = 6;

        SpriteRenderer _sr;
        Texture2D _tex;
        Color[] _px;
        float _next;
        int _seed;

        public static LightningArc Create(string name)
        {
            var go = new GameObject(name);
            var arc = go.AddComponent<LightningArc>();
            arc._sr = go.AddComponent<SpriteRenderer>();
            arc._sr.sortingOrder = SortingOrders.Telegraph;
            return arc;
        }

        /// <summary>Redraws the arc from <paramref name="from"/> to <paramref name="to"/> if it is
        /// time to (or <paramref name="force"/>); between re-rolls only its alpha breathes.</summary>
        public void Set(Vector2 from, Vector2 to, Color[] ramp, bool overcharged, bool force = false)
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (force || Time.time >= _next)
            {
                _next = Time.time + RerollSeconds;
                _seed++;
                Paint(from, to, ramp, overcharged);
            }
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 30f);
            _sr.color = new Color(1f, 1f, 1f, pulse);
        }

        void Paint(Vector2 from, Vector2 to, Color[] ramp, bool thick)
        {
            float tpu = TexelRig.TexelsPerUnit;
            Vector2 a = from * tpu, b = to * tpu;
            Vector2 min = Vector2.Min(a, b) - Vector2.one * Margin, max = Vector2.Max(a, b) + Vector2.one * Margin;
            int x0 = Mathf.FloorToInt(min.x), y0 = Mathf.FloorToInt(min.y);
            int w = Mathf.CeilToInt(max.x) - x0, h = Mathf.CeilToInt(max.y) - y0;
            w = Mathf.Clamp(w, 2, 2048); h = Mathf.Clamp(h, 2, 2048);

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

            // the jagged path: a kink every ~5 texels, its ends pinned
            var d = b - a;
            float len = d.magnitude;
            int n = Mathf.Max(4, Mathf.RoundToInt(len / 5f));
            var normal = len > 0.001f ? new Vector2(-d.y, d.x) / len : Vector2.up;
            var pts = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, j = i == 0 || i == n ? 0f : (TexelRig.Hash(i, _seed) - 0.5f) * 7f;
                pts[i] = a + d * t + normal * j - new Vector2(x0, y0);
            }

            var glow = ramp[2];
            glow.a = 0.45f;
            float extra = thick ? 1f : 0f;
            Stroke(pts, w, h, 1.6f + extra, glow);
            Stroke(pts, w, h, 0.9f + extra * 0.5f, ramp[2]);
            Stroke(pts, w, h, 0.4f + extra * 0.4f, ramp[3]);
            if (thick)
                for (int f = 0; f < 2; f++)
                {
                    int k = 2 + Mathf.FloorToInt(TexelRig.Hash(f, _seed) * Mathf.Max(1, pts.Length - 4));
                    var s = pts[Mathf.Min(k, pts.Length - 1)];
                    var e = s + new Vector2((TexelRig.Hash(_seed, f) - 0.5f) * 14f, (TexelRig.Hash(f + 3, _seed) - 0.5f) * 14f);
                    Stroke(new[] { s, e }, w, h, 0.5f, ramp[2]);
                }

            _tex.SetPixels(_px);
            _tex.Apply();
            if (_sr.sprite != null && _sr.sprite.texture != _tex) Destroy(_sr.sprite);
            if (_sr.sprite == null || _sr.sprite.texture != _tex)
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, w, h), Vector2.zero, tpu, 0, SpriteMeshType.FullRect);
            transform.position = new Vector3(x0 / tpu, y0 / tpu, 0f);
        }

        void Stroke(Vector2[] pts, int w, int h, float radius, Color c)
        {
            for (int i = 0; i + 1 < pts.Length; i++)
            {
                Vector2 p = pts[i], q = pts[i + 1];
                int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p.x, q.x) - radius - 1));
                int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(p.x, q.x) + radius + 1));
                int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p.y, q.y) - radius - 1));
                int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(p.y, q.y) + radius + 1));
                var seg = q - p;
                float l2 = Mathf.Max(1e-4f, seg.sqrMagnitude);
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var v = new Vector2(x + 0.5f, y + 0.5f) - p;
                    float t = Mathf.Clamp01(Vector2.Dot(v, seg) / l2);
                    if ((v - seg * t).sqrMagnitude <= radius * radius) _px[y * w + x] = c;
                }
            }
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
