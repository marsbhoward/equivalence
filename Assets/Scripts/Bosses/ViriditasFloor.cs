using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// The stomp's CONE on the floor (the user's pick): aimed where the player stands, a hard red edge
    /// from the first frame FILLING INWARD toward its feet (group 4's danger rule). On the slam, ROOTS
    /// crack out along it (the user's yes) and fade. One per stomp; the fight steers it.
    /// </summary>
    public class StompCone : TexelPicture
    {
        [SerializeField] public float Aim;       // radians, 0 east
        [SerializeField] public float Fill = -1f;
        [SerializeField] float _slamAt = -9f;
        [SerializeField] int _seed;

        public static StompCone At(Transform parent, Vector2 at, int seed)
        {
            var go = new GameObject("viriditas.cone");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            int r = Mathf.CeilToInt(Tuning.Viriditas.ConeLength * TexelFx.Body) + 3;
            var p = Attach<StompCone>(go.transform, "cone", Vector2.zero, r, r, r, SortingOrders.GroundDanger, clip: true);
            p._seed = seed;
            return p;
        }

        public void Slam() { _slamAt = Age; Fill = -1f; Destroy(gameObject, 1.0f); }

        bool Inside(float x, float y, float len, float half)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            if (d > len) return false;
            if (d < 0.6f * TexelFx.Body) return true;
            float a = Mathf.Abs(Mathf.DeltaAngle(Aim * Mathf.Rad2Deg, Mathf.Atan2(y, x) * Mathf.Rad2Deg));
            return a <= half;
        }

        protected override bool Draw()
        {
            float len = Tuning.Viriditas.ConeLength * TexelFx.Body, half = Tuning.Viriditas.ConeHalfAngleDegrees;
            int r = Mathf.CeilToInt(len) + 1;
            var red = MedusaCanvas.Red;
            if (Fill >= 0f)
            {
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                    {
                        if (!Inside(x, y, len, half)) continue;
                        bool edge = !Inside(x + 1, y, len, half) || !Inside(x - 1, y, len, half) || !Inside(x, y + 1, len, half) || !Inside(x, y - 1, len, half);
                        if (edge) { Over(x, y, red[2], 1f); continue; }
                        float d = Mathf.Sqrt(x * x + y * y);
                        if (d >= len * (1f - Fill)) Over(x, y, red[3], 0.42f);
                    }
                return true;
            }
            float since = Age - _slamAt;
            if (since > 0.95f) return false;
            // the roots, cracking out from its feet along the cone, then fading
            float reach = Mathf.Clamp01(since / 0.18f) * len, fade = 1f - Mathf.Clamp01((since - 0.4f) / 0.55f);
            for (int k = 0; k < 7; k++)
            {
                float a = Aim + (k / 6f - 0.5f) * 2f * half * Mathf.Deg2Rad * 0.9f + (FireArt.Hash(k, _seed) - 0.5f) * 0.15f;
                float x = 0f, y = 0f;
                for (int i = 0; i < reach; i++)
                {
                    x += Mathf.Cos(a); y += Mathf.Sin(a);
                    if (FireArt.Hash(k, i, _seed) > 0.72f) a += (FireArt.Hash(k, i, _seed + 1) - 0.5f) * 0.5f;
                    var c = i > reach - 3 ? GroveCanvas.Moss[1] : GroveCanvas.Bark[3];
                    Over(Mathf.RoundToInt(x), Mathf.RoundToInt(y), c, fade);
                    Over(Mathf.RoundToInt(x), Mathf.RoundToInt(y) - 1, GroveCanvas.Bark[4], fade * 0.6f);
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Sap where the roll broke a sapling or a tree (Glaciron's pools; the user's call): amber,
    /// spreading to SapGrowth times its first size, bubbling, ticking damage until the grove regrows.
    /// The ticking is the fight's (<see cref="Viriditas"/>), read from <see cref="Radius"/>.
    /// </summary>
    public class SapPool : TexelPicture
    {
        [SerializeField] float _growth;

        public static SapPool At(Transform parent, Vector2 at, float growth)
        {
            var go = new GameObject("viriditas.sap");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            int r = Mathf.CeilToInt(Tuning.Viriditas.SapRadius * growth * TexelFx.Body) + 4;
            var p = Attach<SapPool>(go.transform, "sap", Vector2.zero, r, r, r, SortingOrders.GroundLasting, clip: true);
            p._growth = growth;
            return p;
        }

        /// <summary>Its reach now, in world units.</summary>
        public float Radius => Tuning.Viriditas.SapRadius * Mathf.Lerp(1f, _growth, Mathf.Clamp01(Age / Tuning.Viriditas.SapSpreadSeconds));
        public Vector2 Centre => transform.parent != null ? (Vector2)transform.parent.position : (Vector2)transform.position;

        protected override bool Draw()
        {
            float r = Radius * TexelFx.Body;
            int e = Mathf.CeilToInt(r) + 2, frame = Mathf.FloorToInt(Age * 3f);
            var sap = GroveCanvas.Sap;
            for (int y = -e; y <= e; y++)
                for (int x = -e; x <= e; x++)
                {
                    float d = Mathf.Sqrt(x * x + y * y) / r + (FireArt.Noise(x * 0.12f, y * 0.12f) - 0.5f) * 0.18f;
                    if (d > 1f) continue;
                    if (d > 0.88f) { Over(x, y, sap[2], 0.9f); continue; }
                    Over(x, y, FireArt.Hash(x, y, frame) > 0.96f ? sap[0] : sap[1], 0.62f);
                }
            return true;
        }
    }

    /// <summary>
    /// Full Bloom on the floor (Mechanar's nuke, the user's pick): POLLEN filling outward from the
    /// lotus everywhere it would reach, the trees' shadows left clear with a hard red border (group 4's
    /// danger rule), then a bright wash as it blooms, fading. Painted from <see cref="Grove.Shadowed"/> -
    /// the call the Bloom tests - like Medusa's red (<see cref="MedusaGaze"/>, whose shape this is).
    /// </summary>
    public class BloomFill : MonoBehaviour
    {
        const float Fps = 15f, WaveSeconds = 0.35f, Hold = 0.3f, FadeSeconds = 0.6f;

        [SerializeField] SpriteRenderer _sr;
        [SerializeField] Grove _grove;
        [SerializeField] Vector2 _eye;
        [SerializeField] public float Fill = -1f;
        [SerializeField] float _bloomAt = -9f;

        int _w, _h, _key = int.MinValue;
        Vector2 _keyEye;
        Texture2D _tex;
        Color32[] _px;
        bool[] _shadow, _edge;
        float[] _dist;
        float _maxDist, _drawn = -9f;
        bool _on;

        public static BloomFill Build(Transform parent, Grove grove)
        {
            var go = new GameObject("viriditas.bloom");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<BloomFill>();
            g._sr = go.AddComponent<SpriteRenderer>();
            g._sr.sortingOrder = SortingOrders.GroundDanger;
            g._grove = grove;
            return g;
        }

        public void Begin(Vector2 eye) { _eye = eye; Fill = 0f; }
        public void Bloom() { _bloomAt = Time.time; Fill = -1f; }

        Vector2 World(int x, int y) => new((x - _w / 2 + 0.5f) / TexelFx.Body, (y - _h / 2 + 0.5f) / TexelFx.Body);

        void Ensure()
        {
            var inner = Arena.HalfExtents - Vector2.one * Tuning.Scenery.WallInset;
            int w = 2 * Mathf.CeilToInt(inner.x * TexelFx.Body), h = 2 * Mathf.CeilToInt(inner.y * TexelFx.Body);
            if (_tex == null || _w != w || _h != h || _px == null)
            {
                _w = w; _h = h;
                _tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                _px = new Color32[w * h]; _shadow = new bool[w * h]; _edge = new bool[w * h]; _dist = new float[w * h];
                if (_sr.sprite != null) Destroy(_sr.sprite);
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), TexelFx.Body, 0, SpriteMeshType.FullRect);
                _sr.transform.position = Vector3.zero;
                // A new Texture2D starts OPAQUE GREY: clear it before it is ever shown.
                _tex.SetPixels32(_px); _tex.Apply(false);
                _key = int.MinValue; _on = false;
            }
            int key = _grove != null ? _grove.TreeMask : 0;
            if (key == _key && _keyEye == _eye) return;
            _key = key; _keyEye = _eye;
            _maxDist = 0f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var p = World(x, y);
                    _shadow[i] = _grove != null && _grove.Shadowed(_eye, p);
                    _dist[i] = Vector2.Distance(p, _eye);
                    _maxDist = Mathf.Max(_maxDist, _dist[i]);
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    _edge[i] = !_shadow[i] && ((x > 0 && _shadow[i - 1]) || (x < w - 1 && _shadow[i + 1])
                                               || (y > 0 && _shadow[i - w]) || (y < h - 1 && _shadow[i + w]));
                }
            _drawn = -9f;
        }

        static Color32 A(Color32 c, float a) { c.a = (byte)(Mathf.Clamp01(a) * 255f); return c; }

        void LateUpdate()
        {
            if (_sr == null) return;
            float now = Time.time, since = now - _bloomAt;
            bool bloom = since < WaveSeconds + Hold + FadeSeconds;
            if (Fill < 0f && !bloom)
            {
                if (_on) { System.Array.Clear(_px, 0, _px.Length); _tex.SetPixels32(_px); _tex.Apply(false); _on = false; }
                return;
            }
            Ensure();
            if (now - _drawn < 1f / Fps) return;
            _drawn = now; _on = true;

            var pollen = GroveCanvas.Pollen;
            var red = MedusaCanvas.Red;
            int frame = Mathf.FloorToInt(now * 10f);
            float front = Mathf.Clamp01(Fill) * _maxDist, band = 2f / TexelFx.Body;
            float wave = Mathf.Clamp01(since / WaveSeconds) * _maxDist;
            float fade = 1f - Mathf.Clamp01((since - WaveSeconds - Hold) / FadeSeconds);
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    int i = y * _w + x;
                    if (_shadow[i]) { _px[i] = default; continue; }
                    float d = _dist[i];
                    if (bloom)
                    {
                        _px[i] = d > wave ? default : A(FireArt.Hash(x, y, frame) > 0.9f ? pollen[0] : pollen[1], fade * 0.85f);
                        continue;
                    }
                    if (_edge[i]) { _px[i] = A(red[2], 0.4f + Fill); continue; }
                    if (d > front) { _px[i] = default; continue; }
                    _px[i] = d > front - band ? A(pollen[2], 0.95f)
                           : A(FireArt.Hash(x, y, frame) > 0.93f ? pollen[0] : pollen[2], 0.22f + Fill * 0.3f);
                }
            _tex.SetPixels32(_px); _tex.Apply(false);
        }
    }

    /// <summary>
    /// The grove regrowing (the user's pick, "pollen settles"): a cloud of pollen drifts out from the
    /// lotus over the room and settles; the saplings come up where it lands (the fight times them).
    /// </summary>
    public class PollenDrift : TexelPicture
    {
        const float Reach = 9f;
        [SerializeField] float _seconds;

        public static void At(Transform parent, Vector2 at, float seconds)
        {
            var go = new GameObject("viriditas.pollen");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            int r = Mathf.CeilToInt(Reach * TexelFx.Body);
            var p = Attach<PollenDrift>(go.transform, "pollen", Vector2.zero, r, r, r, TexelFx.Air, clip: true);
            p._seconds = seconds;
            Destroy(go, seconds + 0.1f);
        }

        protected override bool Draw()
        {
            float k = Age / _seconds;
            if (k >= 1f) return false;
            var pollen = GroveCanvas.Pollen;
            float max = Reach * TexelFx.Body;
            for (int i = 0; i < 260; i++)
            {
                float a = FireArt.Hash(i, 1) * Mathf.PI * 2f, far = FireArt.Hash(i, 2);
                float d = Mathf.Min(1f, k * 1.6f) * far * max;
                float x = Mathf.Cos(a) * d + Mathf.Sin(Age * 2f + i) * 2f, y = Mathf.Sin(a) * d * 0.8f + 40f * (1f - Mathf.Min(1f, k * 1.6f)) * (1f - far);
                float alpha = 0.8f * (1f - Mathf.Clamp01((k - 0.7f) / 0.3f));
                Over(Mathf.RoundToInt(x), Mathf.RoundToInt(y), pollen[1 + (i % 2)], alpha);
            }
            return true;
        }
    }
}
