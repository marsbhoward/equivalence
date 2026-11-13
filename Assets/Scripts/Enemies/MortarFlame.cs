using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// The Salamander's fire, drawn (the user's sign-off, 2026-10-19; redrawn 2026-11-12 - the
    /// specks read as static, not fire): a GOUT that pours out of its jaws and down the cone it
    /// burns. A white-hot core at the jaws, then puffs of flame flowing outward, each swelling and
    /// cooling as it goes - yellow, crimson, then dark smoke at the tips - so the fire visibly
    /// travels. It runs out to its full reach over its first moment rather than appearing whole.
    /// Through the wind-up it is only a glow gathering in the jaws (<see cref="Tell"/>,
    /// <see cref="Progress"/>); the cone it will burn is on the floor (<see cref="BlastTell"/>).
    ///
    /// One axis-aligned texture on the texel grid round the jaws, redrawn each frame from the aim
    /// (never a rotated sprite - rotated texels shear). The damage is the controller's
    /// (EnemyController.InFlame, measured from the body); this is only the picture.
    /// </summary>
    public class MortarFlame : MonoBehaviour
    {
        SpriteRenderer _sr;
        Texture2D _tex;
        Color[] _px;
        int _size;
        float _reach, _halfAngle;
        Vector2 _dir = Vector2.right;
        bool _tell;

        /// <summary>The licks of the wind-up (true) or the gout itself (false).</summary>
        public bool Tell { get => _tell; set => _tell = value; }

        /// <param name="range">World reach; 0 = Tuning.Enemy.MortarFlameRange (band 4 reaches further).</param>
        public static MortarFlame Create(float range = 0f)
        {
            var go = new GameObject("mortar.flame");
            var f = go.AddComponent<MortarFlame>();
            f._sr = go.AddComponent<SpriteRenderer>();
            f._sr.sortingOrder = SortingOrders.Fx - 1;
            f._reach = (range > 0f ? range : Tuning.Enemy.MortarFlameRange) * TexelRig.TexelsPerUnit;
            f._halfAngle = Tuning.Enemy.MortarFlameHalfAngle;
            f._size = Mathf.CeilToInt(f._reach) * 2 + 6;
            return f;
        }

        /// <summary>From the jaws (world) along the aim.</summary>
        public void Place(Vector2 jaws, Vector2 dir)
        {
            if (dir.sqrMagnitude > 0.0001f) _dir = dir.normalized;
            float tpu = TexelRig.TexelsPerUnit;
            transform.position = new Vector3(Mathf.Round(jaws.x * tpu - _size / 2f) / tpu, Mathf.Round(jaws.y * tpu - _size / 2f) / tpu, 0f);
        }

        /// <summary>Through the wind-up: 0 as it starts, 1 as it breathes.</summary>
        public float Progress;

        const int Puffs = 96;
        const float TravelSeconds = 0.42f, RunOutSeconds = 0.18f;
        float _pouring;
        readonly float[] _p = new float[Puffs];
        readonly int[] _order = new int[Puffs];

        void LateUpdate()
        {
            if (_sr == null) return;
            if (_tex == null)
            {
                _tex = new Texture2D(_size, _size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _px = new Color[_size * _size];
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, _size, _size), Vector2.zero, TexelRig.TexelsPerUnit, 0, SpriteMeshType.FullRect);
            }

            System.Array.Clear(_px, 0, _px.Length);
            var acc = MortarArt.Accent;
            float c = _size / 2f, baseAng = Mathf.Atan2(_dir.y, _dir.x), half = TexelRig.Rad(_halfAngle);
            int frame = Mathf.FloorToInt(Time.time * 20f);
            float ca = Mathf.Cos(baseAng), sa = Mathf.Sin(baseAng);

            if (_tell)
            {
                // Breath drawn in: a glow swelling in the jaws, flickering.
                _pouring = 0f;
                float k = Mathf.Clamp01(Progress);
                float r = Mathf.Lerp(1f, 3.2f, k) + (TexelRig.Hash(frame, 3) - 0.5f) * 0.8f;
                Disc(c + ca * 1.5f, c + sa * 1.5f, r + 1.2f, acc[1], 0.5f * k, frame);
                Disc(c + ca * 1.5f, c + sa * 1.5f, r, acc[2], 0.4f + 0.6f * k, frame);
                Disc(c + ca * 1.5f, c + sa * 1.5f, r * 0.5f, acc[3], k, frame);
            }
            else
            {
                _pouring += Time.deltaTime;
                float extent = _reach * Mathf.Clamp01(_pouring / RunOutSeconds);

                // The puffs, far (cool) ones first so the hot ones near the jaws lie over them.
                for (int i = 0; i < Puffs; i++)
                {
                    float rate = 0.85f + 0.3f * TexelRig.Hash(i, 11);
                    float p = _pouring / TravelSeconds * rate + TexelRig.Hash(i, 12);
                    _p[i] = p - Mathf.Floor(p);
                    _order[i] = i;
                }
                System.Array.Sort(_order, (x, y) => _p[y].CompareTo(_p[x]));
                foreach (int i in _order)
                {
                    float p = _p[i], d = p * _reach;
                    if (d > extent) continue;
                    // Spread across the cone, wavering as it flows.
                    float lane = (TexelRig.Hash(i, 13) - 0.5f) * 2f * 0.8f
                               + Mathf.Sin(_pouring * 9f + i * 1.7f) * 0.12f;
                    float a = baseAng + lane * half * Mathf.Clamp01(0.6f + p);
                    float x = c + Mathf.Cos(a) * d, y = c + Mathf.Sin(a) * d;
                    float r = 2f + p * 6.5f;
                    var col = p < 0.22f ? acc[3] : p < 0.55f ? acc[2] : p < 0.86f ? acc[1] : acc[0];
                    float alpha = p < 0.86f ? 1f : Mathf.Lerp(0.6f, 0.1f, (p - 0.86f) / 0.14f);
                    Disc(x, y, r, col, alpha, frame + i);
                    // A hot heart in the puffs still near the jaws.
                    if (p < 0.55f) Disc(x, y, r * 0.45f, p < 0.3f ? acc[3] : acc[2], 1f, frame + i * 3);
                }

                // The core: a white-hot tongue out of the jaws, tapering.
                float core = Mathf.Min(extent, _reach * 0.45f);
                for (float t = 0f; t <= core; t += 0.5f)
                {
                    float w = Mathf.Lerp(3f, 1.4f, t / Mathf.Max(1f, _reach * 0.45f));
                    float wob = Mathf.Sin(t * 0.6f - _pouring * 30f) * 0.6f;
                    float x = c + ca * t - sa * wob, y = c + sa * t + ca * wob;
                    Disc(x, y, w, acc[3], 1f, frame);
                }
            }
            _tex.SetPixels(_px);
            _tex.Apply();
        }

        /// <summary>A rough disc - its edge nibbled differently every frame, so it licks.</summary>
        void Disc(float cx, float cy, float r, Color col, float alpha, int seed)
        {
            int e = Mathf.CeilToInt(r) + 1, x0 = Mathf.RoundToInt(cx), y0 = Mathf.RoundToInt(cy);
            for (int dy = -e; dy <= e; dy++)
                for (int dx = -e; dx <= e; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > r - TexelRig.Hash(x0 + dx + seed * 7, y0 + dy) * Mathf.Min(1.5f, r * 0.4f)) continue;
                    Put(x0 + dx, y0 + dy, col, alpha);
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
