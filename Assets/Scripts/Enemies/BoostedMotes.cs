using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// The mark of an ally the Green Lion has roared to power (the user's sign-off, 2026-10-19):
    /// green motes, the roar's colour, rising off it from its feet to over its head, for the rest
    /// of its life. It replaced a soft pale circle over the body.
    ///
    /// One small texture on the texel grid over the body's own drawn bounds, redrawn each frame on
    /// a child of the enemy's root (so it rides the body and dies with it). An overlay, above the
    /// depth band, as the circle was.
    /// </summary>
    public class BoostedMotes : MonoBehaviour
    {
        const int Motes = 9;
        const float RiseSeconds = 1.4f;

        [SerializeField] SpriteRenderer _sr;
        Texture2D _tex;
        Color[] _px;
        [SerializeField] int _w, _h;

        public static void Attach(EnemyController enemy)
        {
            if (enemy == null || enemy.GetComponentInChildren<BoostedMotes>() != null) return;

            // The body as drawn: the biggest renderer under the root that is not an overlay.
            Bounds body = new(enemy.transform.position, Vector3.one * 0.8f);
            float best = 0f;
            foreach (var r in enemy.GetComponentsInChildren<SpriteRenderer>())
            {
                if (r.sprite == null || r.sortingOrder >= SortingOrders.StatusOverlay) continue;
                float area = r.bounds.size.x * r.bounds.size.y;
                if (area > best) { best = area; body = r.bounds; }
            }

            float tpu = TexelRig.TexelsPerUnit;
            var go = new GameObject("boosted");
            go.transform.SetParent(enemy.transform, false);
            var m = go.AddComponent<BoostedMotes>();
            m._w = Mathf.Clamp(Mathf.CeilToInt(body.size.x * tpu), 16, 80);
            m._h = Mathf.Clamp(Mathf.CeilToInt(body.size.y * tpu) + 12, 24, 96);
            var foot = new Vector2(body.center.x, body.min.y) - (Vector2)enemy.transform.position;
            go.transform.localPosition = new Vector3(Mathf.Round(foot.x * tpu - m._w / 2f) / tpu, Mathf.Round(foot.y * tpu) / tpu, 0f);
            m._sr = go.AddComponent<SpriteRenderer>();
            m._sr.sortingOrder = SortingOrders.StatusOverlay;
        }

        void LateUpdate()
        {
            if (_sr == null || _w <= 0) return;
            if (_tex == null)
            {
                _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _px = new Color[_w * _h];
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, _w, _h), Vector2.zero, TexelRig.TexelsPerUnit, 0, SpriteMeshType.FullRect);
            }

            System.Array.Clear(_px, 0, _px.Length);
            var acc = BoosterArt.Accent;
            for (int i = 0; i < Motes; i++)
            {
                float ph = ((Time.time / RiseSeconds + i / (float)Motes) % 1f + 1f) % 1f;
                int x = Mathf.RoundToInt(2f + TexelRig.Hash(i, 1) * (_w - 4) + Mathf.Sin(ph * 6f + i) * 1.5f);
                int y = Mathf.RoundToInt(2f + ph * (_h - 4));
                var c = ph < 0.5f ? acc[2] : acc[1];
                Put(x, y, c, 1f - ph);
                if (ph < 0.3f) Put(x, y - 1, c, 1f - ph);
            }
            _tex.SetPixels(_px);
            _tex.Apply();
        }

        void Put(int x, int y, Color c, float alpha)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;
            c.a = alpha;
            _px[y * _w + x] = c;
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
