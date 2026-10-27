using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.FireFx;

namespace Convergence.Combat
{
    /// <summary>
    /// Ignite's flame on the discs and the bow (the user's picks, 2026-10-25): a tail of flame
    /// behind whatever it follows - yellow at the thing, orange, then red and breaking up at the
    /// end - the greatsword's flame smear (SwingSmear) in the language of a moving point rather
    /// than a sweeping blade. A disc swung in the hand smears its path; a thrown disc trails a
    /// comet; an arrow's tinted streak becomes a flame tail.
    ///
    /// One texture round the head of the tail, on the floor's texel grid, rebuilt each frame from
    /// where the thing has been over the last <see cref="_window"/> seconds. A trail held in the
    /// hand records RELATIVE to the player (the anchor) and only while a swing is live, so walking
    /// leaves nothing and only the swing's own motion is drawn - SwingSmear's rule. A trail behind
    /// something thrown outlives it long enough for its tail to burn out, then goes.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class FlameTrail : MonoBehaviour
    {
        const float Ppu = 37.5f;
        const int Capacity = 48;

        struct Sample { public Vector2 Pos; public float Time; }

        [SerializeField] Transform _target;
        [SerializeField] SpriteRenderer _source;     // whose centre, visibility and sorting it follows
        [SerializeField] Transform _anchor;          // samples kept relative to this (a hand-held disc)
        [SerializeField] Vector2 _local;             // the point followed, in the target's local space
        [SerializeField] float _window, _radius, _recordUntil = float.MaxValue;
        [SerializeField] int _order;
        [SerializeField] bool _sortUnder;            // one under the source each frame, not _order
        [SerializeField] SpriteRenderer _sr;
        [SerializeField] int _half;
        int _cx, _cy;   // the canvas centre in world texels, this frame
        Texture2D _tex;
        Color32[] _px;
        Sample[] _samples;
        int _count;

        /// <summary>Behind a thrown thing - a disc, an arrow. Standalone, so the thing's own
        /// scale never touches the texels; it dies once the thing is gone and its tail is out.</summary>
        public static FlameTrail Behind(Transform target, SpriteRenderer source, Vector2 local, float window,
                                        float radiusTexels, int order)
        {
            var t = Make("flame.trail", null);
            t._target = target; t._source = source; t._local = local;
            t._window = window; t._radius = radiusTexels; t._order = order;
            return t;
        }

        /// <summary>A disc swung in the hand: records from now for <paramref name="seconds"/>,
        /// relative to <paramref name="player"/>, sorted just under the disc. One per hand, kept
        /// on the player and reused.</summary>
        public static void Swing(Transform player, SpriteRenderer disc, string name, float seconds)
        {
            if (player == null || disc == null || seconds <= 0f) return;
            var found = player.Find(name);
            var t = found != null ? found.GetComponent<FlameTrail>() : null;
            if (t == null) t = Make(name, player);
            t._target = disc.transform; t._source = disc; t._anchor = player; t._local = Vector2.zero;
            t._window = T.TrailDiscWindow; t._sortUnder = true;
            t._radius = Mathf.Max(1f, Mathf.Min(disc.bounds.extents.x, disc.bounds.extents.y) * Ppu * T.TrailDiscWidth);
            t._count = 0;   // never joined to the last swing's end - SwingSmear's reason
            t._recordUntil = Time.time + seconds;
            t.enabled = true;
        }

        static FlameTrail Make(string name, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var t = go.AddComponent<FlameTrail>();
            t._sr = go.AddComponent<SpriteRenderer>();
            t._sr.enabled = false;
            return t;
        }

        void LateUpdate()
        {
            EnsureCanvas();
            float now = Time.time;
            bool gone = _target == null;

            // A thrown thing sized off its picture the first frame it is seen.
            if (!gone && _radius <= 0f && _source != null)
                _radius = Mathf.Max(1f, Mathf.Min(_source.bounds.extents.x, _source.bounds.extents.y) * Ppu * T.TrailDiscWidth);

            bool visible = !gone && (_source == null || (_source.enabled && !_source.forceRenderingOff && _source.gameObject.activeInHierarchy));
            if (visible && now <= _recordUntil) Record(now);

            // Drop what has burned out, keeping the pair that spans the window's edge.
            int keepFrom = 0;
            for (int i = 0; i < _count - 1; i++)
                if (now - _samples[i + 1].Time > _window) keepFrom = i + 1;
            if (keepFrom > 0) { System.Array.Copy(_samples, keepFrom, _samples, 0, _count - keepFrom); _count -= keepFrom; }

            bool live = _count >= 2 && now - _samples[_count - 1].Time <= _window;
            if (!live)
            {
                _sr.enabled = false;
                if (gone && _anchor == null) { Destroy(gameObject); return; }
                if (now > _recordUntil) { _count = 0; enabled = false; }
                return;
            }

            // The canvas is centred on the newest sample, on the floor's grid.
            var origin = _anchor != null ? (Vector2)_anchor.position : Vector2.zero;
            var head = origin + _samples[_count - 1].Pos;
            var centre = new Vector2(Mathf.Round(head.x * Ppu), Mathf.Round(head.y * Ppu));
            System.Array.Clear(_px, 0, _px.Length);
            Draw(now, origin, centre);
            _tex.SetPixels32(_px);
            _tex.Apply();

            transform.position = new Vector3(centre.x / Ppu, centre.y / Ppu, 0f);
            transform.rotation = Quaternion.identity;
            if (_source != null)
            {
                _sr.sortingLayerID = _source.sortingLayerID;
                _sr.sharedMaterial = _source.sharedMaterial;
                if (_sortUnder) _sr.sortingOrder = _source.sortingOrder - 1;
            }
            if (!_sortUnder) _sr.sortingOrder = _order;
            _sr.enabled = true;
        }

        void Record(float now)
        {
            Vector2 p = _source != null && _local == Vector2.zero ? (Vector2)_source.bounds.center
                                                                  : (Vector2)_target.TransformPoint(_local);
            if (_anchor != null) p -= (Vector2)_anchor.position;
            if (_count == Capacity) { System.Array.Copy(_samples, 1, _samples, 0, Capacity - 1); _count--; }
            _samples[_count++] = new Sample { Pos = p, Time = now };
        }

        /// <summary>Oldest first, so the newest - hottest - overwrites where they overlap. Each
        /// segment is stepped a texel at a time, so a fast frame draws a band, not dots.</summary>
        void Draw(float now, Vector2 origin, Vector2 centreTexels)
        {
            int frame = Mathf.FloorToInt(now * 20f);
            _cx = Mathf.RoundToInt(centreTexels.x); _cy = Mathf.RoundToInt(centreTexels.y);
            for (int i = 0; i < _count - 1; i++)
            {
                var a = _samples[i]; var b = _samples[i + 1];
                var pa = (origin + a.Pos) * Ppu - centreTexels;
                var pb = (origin + b.Pos) * Ppu - centreTexels;
                int steps = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(pa, pb)), 1, 400);
                for (int k = 1; k <= steps; k++)
                {
                    float f = (float)k / steps;
                    var p = Vector2.Lerp(pa, pb, f);
                    float u = Mathf.Clamp01((now - Mathf.Lerp(a.Time, b.Time, f)) / _window);
                    Blob(p, u, frame);
                }
            }
        }

        /// <summary>One point of the tail: narrowing with age, the ramp by age, broken past the
        /// middle of the window.</summary>
        void Blob(Vector2 p, float u, int frame)
        {
            float r = _radius * Mathf.Sqrt(Mathf.Max(0f, 1.1f - u));
            int ir = Mathf.CeilToInt(r);
            int cx = Mathf.RoundToInt(p.x) + _half, cy = Mathf.RoundToInt(p.y) + _half;
            var colour = FireArt.At(0.18f + u * 0.82f);   // yellow at the thing, never white
            for (int dy = -ir; dy <= ir; dy++)
                for (int dx = -ir; dx <= ir; dx++)
                {
                    if (dx * dx + dy * dy > r * r + 0.25f) continue;
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= _half * 2 || y >= _half * 2) continue;
                    // Hashed in WORLD texels, so the breaks stay put on the floor as the canvas moves.
                    if (u > 0.45f && FireArt.Hash(x + _cx, y + _cy, frame) < (u - 0.45f) * 1.6f) continue;
                    _px[y * _half * 2 + x] = colour;
                }
        }

        void EnsureCanvas()
        {
            _samples ??= new Sample[Capacity];
            if (_tex != null && _px != null) return;
            _half = Mathf.CeilToInt(T.TrailHalfExtent * Ppu);
            int size = _half * 2;
            _tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            _px = new Color32[size * size];
            _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
