using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// A shot's aim tell as a texel line (the user's pick, 2026-10-26 - "narrowing line"): from
    /// the muzzle along the aim, three texels wide while the shot is far off and one as it
    /// nears, brightening, gone as it fires. It replaced a soft capsule. One canvas sized to the
    /// line's own box at body density; the caller sets it every frame.
    /// </summary>
    public class TellLine : MonoBehaviour
    {
        const float Ppu = TexelFx.Body;

        [SerializeField] SpriteRenderer _sr;
        Texture2D _tex;
        Color32[] _px;
        int _w, _h;

        public static TellLine On(GameObject go, int order)
        {
            var t = go.AddComponent<TellLine>();
            t._sr = go.AddComponent<SpriteRenderer>();
            t._sr.sortingOrder = order;
            return t;
        }

        /// <param name="progress">0 as the tell starts, 1 as it fires.</param>
        public void Set(Vector2 from, Vector2 to, float progress, Color colour)
        {
            int x0 = Mathf.RoundToInt(from.x * Ppu), y0 = Mathf.RoundToInt(from.y * Ppu);
            int x1 = Mathf.RoundToInt(to.x * Ppu), y1 = Mathf.RoundToInt(to.y * Ppu);
            int minX = Mathf.Min(x0, x1) - 2, minY = Mathf.Min(y0, y1) - 2;
            int w = Mathf.Abs(x1 - x0) + 5, h = Mathf.Abs(y1 - y0) + 5;
            if (_tex == null || w > _w || h > _h)
            {
                if (_sr.sprite != null) Destroy(_sr.sprite);
                if (_tex != null) Destroy(_tex);
                _w = Mathf.Max(w, _w); _h = Mathf.Max(h, _h);
                _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _px = new Color32[_w * _h];
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, _w, _h), Vector2.zero, Ppu, 0, SpriteMeshType.FullRect);
            }
            System.Array.Clear(_px, 0, _px.Length);

            progress = Mathf.Clamp01(progress);
            int half = progress < 0.45f ? 1 : 0;
            // Brighter as it nears: toward white from the tell's own colour.
            var c = (Color32)new Color(Mathf.Lerp(colour.r, 1f, progress * 0.35f), Mathf.Lerp(colour.g, 1f, progress * 0.35f),
                                       Mathf.Lerp(colour.b, 1f, progress * 0.35f), Mathf.Clamp01(colour.a));
            int n = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            bool steep = Mathf.Abs(y1 - y0) > Mathf.Abs(x1 - x0);
            for (int i = 0; i <= n; i++)
            {
                float f = n == 0 ? 0f : i / (float)n;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, f)) - minX, y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, f)) - minY;
                for (int s = -half; s <= half; s++)
                {
                    int px = steep ? x + s : x, py = steep ? y : y + s;
                    if (px >= 0 && py >= 0 && px < _w && py < _h) _px[py * _w + px] = c;
                }
            }
            _tex.SetPixels32(_px);
            _tex.Apply();
            transform.SetPositionAndRotation(new Vector3(minX / Ppu, minY / Ppu, 0f), Quaternion.identity);
            transform.localScale = Vector3.one;
            _sr.color = Color.white;
            _sr.enabled = true;
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
