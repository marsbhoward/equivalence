using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>
    /// The Gale Hound's tells point: a streak of wind thrown out of the body the way the coming
    /// dash will go, growing through the tell (the reference clip's flick of fire, the user's
    /// call, 2026-10-18). Three lanes - a hot core between two pale ones - painted on the texel
    /// grid into one axis-aligned texture, LightningArc's way, so it never smears beside the body.
    ///
    /// Lives on the enemy's ROOT, not under "visual": it is not body, so the hit flash and a
    /// chasm fall leave it alone. DasherVisual drives it and hides it outside a tell.
    /// </summary>
    public class DasherPointer : MonoBehaviour
    {
        const int Margin = 2;

        [SerializeField] SpriteRenderer _sr;
        Texture2D _tex;
        Color[] _px;
        Vector2 _dir;
        int _len = -1;

        public static DasherPointer Create(Transform root)
        {
            var go = new GameObject("dasher.pointer");
            go.transform.SetParent(root, false);
            var p = go.AddComponent<DasherPointer>();
            p._sr = go.AddComponent<SpriteRenderer>();
            p._sr.enabled = false;
            return p;
        }

        public void Hide() { if (_sr != null) _sr.enabled = false; }

        /// <summary>Shows the streak from <paramref name="from"/> (world) along
        /// <paramref name="dir"/>, <paramref name="k"/> of the way through the tell, sorted just
        /// in front of <paramref name="over"/>.</summary>
        public void Show(Vector2 from, Vector2 dir, float k, SpriteRenderer over)
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            int len = Mathf.RoundToInt(10f + 22f * Mathf.Min(1f, k * 1.6f));
            if (len != _len || Vector2.Dot(dir, _dir) < 0.999f) Paint(dir, len);
            float tpu = TexelRig.TexelsPerUnit;
            // the texture's origin sits Margin texels below-left of the streak's own box; snapped
            // to the texel grid so the streak's texels are the body's
            var o = new Vector2(Mathf.Round(from.x * tpu), Mathf.Round(from.y * tpu)) + _origin;
            transform.position = new Vector3(o.x / tpu, o.y / tpu, over.transform.position.z - 0.001f);
            _sr.sortingLayerID = over.sortingLayerID;
            _sr.sortingOrder = over.sortingOrder;
            _sr.enabled = over.enabled;
        }

        Vector2 _origin;

        void Paint(Vector2 dir, int len)
        {
            _dir = dir;
            _len = len;
            // the streak's texels, relative to its start
            var pts = new System.Collections.Generic.List<(int x, int y, Color c)>();
            var ramp = DasherArt.WindRamp;
            for (int i = 4; i < len; i++)
            for (int w = -1; w <= 1; w++)
            {
                if (w != 0 && (i + w * 3) % 4 == 0) continue;
                int x = Mathf.RoundToInt(dir.x * i - dir.y * w), y = Mathf.RoundToInt(dir.y * i + dir.x * w);
                var c = w == 0 ? (i > len - 4 ? ramp[2] : ramp[1]) : ramp[0];
                c.a = (w == 0 ? 0.95f : 0.5f) * (0.4f + 0.6f * i / len);
                pts.Add((x, y, c));
            }
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var (x, y, _) in pts) { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
            int tw = maxX - minX + 1 + Margin * 2, th = maxY - minY + 1 + Margin * 2;

            if (_tex == null || _tex.width != tw || _tex.height != th)
            {
                if (_tex != null) Destroy(_tex);
                _tex = new Texture2D(tw, th, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _px = new Color[tw * th];
            }
            else System.Array.Clear(_px, 0, _px.Length);

            foreach (var (x, y, c) in pts) _px[(y - minY + Margin) * tw + (x - minX + Margin)] = c;
            _tex.SetPixels(_px);
            _tex.Apply();
            _origin = new Vector2(minX - Margin, minY - Margin);
            if (_sr.sprite != null && _sr.sprite.texture != _tex) Destroy(_sr.sprite);
            if (_sr.sprite == null || _sr.sprite.texture != _tex)
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, tw, th), Vector2.zero, TexelRig.TexelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
