using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// The armour an enemy has left, as a ring standing UPRIGHT round the creature (the user's
    /// call, 2026-10-26 - it first lay on the floor, "ground segments", but a creature has no
    /// shadow child to find its feet by, so it floated at the waist): an ellipse round the body's
    /// drawn silhouette, split into one arc per bar remaining, so losing a bar reflows the ring
    /// into fewer, wider arcs - "how much is left" reads as arc width. One bar is a whole ring;
    /// none hides it. Texel-drawn at body density, sorted under the body (DepthSorted re-bases it
    /// with the creature's own layers), so only the ring's edge round the silhouette shows.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class ArmorRing : MonoBehaviour
    {
        const float Ppu = TexelFx.Body;
        const int Pad = 3;   // texels between the silhouette and the ring

        [SerializeField] EnemyArmor _armor;
        [SerializeField] SpriteRenderer _sr;
        [SerializeField] float _radius;
        [SerializeField] int _rx, _ry;   // measured once, off the first frame drawn
        int _shownBars = -1;
        Texture2D _tex;
        Color32[] _px;
        int _w, _h;

        static readonly Color32 Steel = new(196, 202, 211, 255), SteelDark = new(93, 101, 115, 255);

        public static ArmorRing Attach(GameObject go, EnemyArmor armor, float radius)
        {
            var ring = go.AddComponent<ArmorRing>();
            ring._armor = armor;
            ring._radius = radius;
            var ringGo = new GameObject("armor-ring");
            ringGo.transform.SetParent(go.transform, false);
            ring._sr = ringGo.AddComponent<SpriteRenderer>();
            // Behind the body (re-based with the creature's layers by DepthSorted).
            ring._sr.sortingOrder = SortingOrders.Enemy - 1;
            ring.Refresh();
            return ring;
        }

        void LateUpdate() => Refresh();

        /// <summary>Fog III: armour rings are hidden. Set by the run's ledger, reset at run end.</summary>
        public static bool Hidden;

        void Refresh()
        {
            if (_armor == null || _sr == null) return;
            if (Hidden) { _sr.enabled = false; _shownBars = -1; return; }
            int bars = _armor.BarsRemaining;
            var body = Body();
            _sr.enabled = bars > 0 && body != null;
            if (!_sr.enabled) return;

            var prof = BurnEmbers.ProfileOf(body.sprite);
            int minCol = int.MaxValue, maxCol = -1, low = int.MaxValue;
            for (int x = 0; x < prof.W; x++)
            {
                if (prof.Top[x] < 0) continue;
                minCol = Mathf.Min(minCol, x); maxCol = Mathf.Max(maxCol, x);
                low = Mathf.Min(low, prof.Bottom[x]);
            }
            if (maxCol < 0) { _sr.enabled = false; return; }
            if (_rx <= 0)
            {
                _rx = Mathf.CeilToInt((maxCol - minCol + 1) / 2f) + Pad;
                _ry = Mathf.CeilToInt((prof.Highest - low + 1) / 2f) + Pad;
                _shownBars = -1;
            }
            if (bars != _shownBars) { _shownBars = bars; Draw(bars); }

            // Centred on the silhouette's middle, on the texel grid.
            float ppu = body.sprite.pixelsPerUnit, sy = Mathf.Abs(body.transform.lossyScale.y);
            var at = new Vector2(body.bounds.center.x, body.bounds.min.y + (low + prof.Highest + 1) * 0.5f / ppu * sy);
            _sr.transform.SetPositionAndRotation(new Vector3(Mathf.Round(at.x * Ppu) / Ppu, Mathf.Round(at.y * Ppu) / Ppu, 0f), Quaternion.identity);
            var s = transform.lossyScale;
            _sr.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, Mathf.Abs(s.x)), 1f / Mathf.Max(1e-4f, Mathf.Abs(s.y)), 1f);
        }

        /// <summary>The creature's biggest drawn sprite.</summary>
        SpriteRenderer Body()
        {
            SpriteRenderer best = null;
            float area = 0f;
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (r == null || r == _sr || !r.enabled || r.sprite == null || r.sortingOrder >= SortingOrders.StatusOverlay) continue;
                if (r.name.StartsWith("status.") || r.name.StartsWith("ledger.") || r.name == HitFlash.ChildName) continue;
                float a = r.bounds.size.x * r.bounds.size.y;
                if (a > area) { area = a; best = r; }
            }
            return best;
        }

        void Draw(int bars)
        {
            int hw = _rx + 1, hh = _ry + 1;
            if (_tex == null || _w != hw * 2 + 1 || _h != hh * 2 + 1)
            {
                if (_sr.sprite != null) Destroy(_sr.sprite);
                if (_tex != null) Destroy(_tex);
                _w = hw * 2 + 1; _h = hh * 2 + 1;
                _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _px = new Color32[_w * _h];
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, _w, _h), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            }
            System.Array.Clear(_px, 0, _px.Length);
            float thick = 1.5f / Mathf.Min(_rx, _ry);
            for (int y = -hh; y <= hh; y++)
                for (int x = -hw; x <= hw; x++)
                {
                    float nx = x / (float)_rx, ny = y / (float)_ry;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    if (d > 1f || d < 1f - thick) continue;
                    // One arc per bar from the top, a small gap between (none when one bar is left).
                    float seg = Mathf.Repeat((Mathf.Atan2(nx, ny)) / (Mathf.PI * 2f), 1f) * bars;
                    float f = seg - Mathf.Floor(seg);
                    if (bars > 1 && (f < 0.06f || f > 0.94f)) continue;
                    _px[(y + hh) * _w + (x + hw)] = y > 0 ? Steel : SteelDark;   // lit from above
                }
            _tex.SetPixels32(_px);
            _tex.Apply();
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
