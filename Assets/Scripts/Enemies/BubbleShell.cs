using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// The Drowned Bell's bubble, drawn (the user's sign-off, 2026-10-19): a wobbling rim with a
    /// highlight, faintly filled, round the shielded body - two walls for an elite's. It DRIFTS in
    /// from under the bell over <see cref="DriftSeconds"/> (the shield is on from the blow; this is
    /// only the picture), a Light or Medium art SHEDS the outer wall in droplets, and the last
    /// breaks BURSTS it. It replaced a soft blue circle and a thin ring. Coloured by the bell that
    /// blew it - water-blue, or Ironbound's purple.
    ///
    /// One texture on the texel grid round the body's own drawn bounds, redrawn each frame, on a
    /// child of the enemy's root so it rides the body. An overlay, as the circle was.
    /// </summary>
    public class BubbleShell : MonoBehaviour
    {
        const float DriftSeconds = 0.33f, ShedSeconds = 0.4f, BurstSeconds = 0.5f;
        const int Spill = 14;   // texels of room round the rim for the droplets

        [SerializeField] SpriteRenderer _sr;
        [SerializeField] Color[] _acc;
        [SerializeField] int _walls, _r, _size;
        [SerializeField] Vector2 _centre;    // local to the root, world units
        [SerializeField] Vector2 _from;      // local to the root, where the drift starts
        [SerializeField] bool _drifts;
        [SerializeField] Combat.Health _hostHp;
        [System.NonSerialized] float _born = -10f, _shedAt = -10f, _burstAt = -10f;
        Texture2D _tex;
        Color[] _px;

        public static BubbleShell Attach(EnemyController host, int walls, Color[] accent, Vector2? fromWorld)
        {
            if (host == null) return null;
            var old = host.GetComponentInChildren<BubbleShell>();
            if (old != null) Destroy(old.gameObject);

            // The body as drawn: the biggest renderer under the root that is not an overlay.
            Bounds body = new(host.transform.position, Vector3.one * 0.8f);
            float best = 0f;
            foreach (var r in host.GetComponentsInChildren<SpriteRenderer>())
            {
                if (r.sprite == null || r.sortingOrder >= SortingOrders.StatusOverlay) continue;
                float area = r.bounds.size.x * r.bounds.size.y;
                if (area > best) { best = area; body = r.bounds; }
            }

            float tpu = TexelRig.TexelsPerUnit;
            var go = new GameObject("bubble.shell");
            go.transform.SetParent(host.transform, false);
            var s = go.AddComponent<BubbleShell>();
            s._acc = accent;
            s._walls = Mathf.Max(1, walls);
            s._r = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(body.size.x, body.size.y) * tpu / 2f) + 3, 12, 44);
            s._size = (s._r + Spill) * 2;
            s._centre = (Vector2)(body.center - host.transform.position);
            s._drifts = fromWorld.HasValue;
            s._from = fromWorld.HasValue ? fromWorld.Value - (Vector2)host.transform.position : s._centre;
            s._born = Time.time;
            s._hostHp = host.GetComponent<Combat.Health>();
            s._sr = go.AddComponent<SpriteRenderer>();
            s._sr.sortingOrder = SortingOrders.StatusOverlay + 1;
            s.Place(0f);
            return s;
        }

        /// <summary>A break that left <paramref name="walls"/> standing: the outer wall sheds.</summary>
        public void SetWalls(int walls)
        {
            if (walls >= _walls || walls <= 0) return;
            _walls = walls;
            _shedAt = Time.time;
        }

        /// <summary>The last break: it bursts into droplets and is gone.</summary>
        public void Burst()
        {
            if (_burstAt >= 0f) return;
            _burstAt = Time.time;
        }

        void Place(float k)
        {
            // The drift: a small bubble from under the bell, arcing up and growing onto the body.
            var p = Vector2.Lerp(_from, _centre, k) + Vector2.up * (Mathf.Sin(Mathf.PI * k) * 0.35f);
            float tpu = TexelRig.TexelsPerUnit;
            transform.localPosition = new Vector3(Mathf.Round(p.x * tpu - _size / 2f) / tpu, Mathf.Round(p.y * tpu - _size / 2f) / tpu, 0f);
        }

        void LateUpdate()
        {
            if (_sr == null || _size <= 0 || _acc == null || _acc.Length < 4) return;
            // its wearer died some other way than through the bubble: it bursts with them
            if (_hostHp != null && _hostHp.IsDead) Burst();
            if (_burstAt > 0f && Time.time - _burstAt > BurstSeconds) { Destroy(gameObject); return; }
            if (_tex == null)
            {
                _tex = new Texture2D(_size, _size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _px = new Color[_size * _size];
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, _size, _size), Vector2.zero, TexelRig.TexelsPerUnit, 0, SpriteMeshType.FullRect);
            }

            float drift = _drifts ? Mathf.Clamp01((Time.time - _born) / DriftSeconds) : 1f;
            Place(drift);
            System.Array.Clear(_px, 0, _px.Length);
            float c = _size / 2f;

            float burst = _burstAt > 0f ? (Time.time - _burstAt) / BurstSeconds : -1f;
            if (burst < 0.15f)
            {
                // the shell itself; growing in as it drifts (no droplets of a formed wall yet)
                float R = _drifts ? Mathf.Lerp(4f, _r, drift * drift) : Mathf.Lerp(3f, _r, Mathf.Clamp01((Time.time - _born) / DriftSeconds));
                int walls = drift > 0.9f ? _walls : 1;
                for (int y = 0; y < _size; y++)
                for (int x = 0; x < _size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float a = Mathf.Atan2(dy, dx), wob = 1f + 0.03f * Mathf.Sin(a * 3f + Time.time * 4f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy * 1.1025f) / wob;
                    if (Mathf.Abs(d - R) < 0.55f) Put(x, y, dx < 0f && dy > 0f && a > 1.75f && a < 2.9f ? _acc[3] : _acc[2], 0.85f);
                    else if (walls > 1 && Mathf.Abs(d - R - 2.2f) < 0.5f) Put(x, y, _acc[1], 0.8f);
                    else if (d < R - 0.5f && (x + y) % 2 == 0) Put(x, y, _acc[2], 0.1f);
                }
            }

            // the outer wall shedding, and the burst
            float shed = (Time.time - _shedAt) / ShedSeconds;
            if (shed >= 0f && shed < 1f) Droplets(c, _r + 2f, shed, 18, _acc[1]);
            if (burst >= 0f) Droplets(c, _r, Mathf.Min(1f, burst), 26, _acc[2]);

            _tex.SetPixels(_px);
            _tex.Apply();
        }

        void Droplets(float c, float R, float k, int n, Color col)
        {
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + TexelRig.Hash(i, 5), r = R + k * 12f * (0.6f + TexelRig.Hash(i, 9) * 0.6f);
                Put(Mathf.RoundToInt(c + Mathf.Cos(a) * r), Mathf.RoundToInt(c + Mathf.Sin(a) * r - k * k * 6f), col, 1f - k);
            }
        }

        void Put(int x, int y, Color col, float alpha)
        {
            if (x < 0 || y < 0 || x >= _size || y >= _size) return;
            col.a = alpha;
            _px[y * _size + x] = col;
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
