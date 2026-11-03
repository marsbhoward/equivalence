using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// The Stinger's ordinary bolt (the user's pick, 2026-10-26 - "ember dart"): a round head with
    /// a white-hot heart and a tail of fire tapering back along its flight, flickering, rimmed in
    /// dark so it reads on any ground. It replaced an orange circle; grown 2026-11-02 (a two-texel
    /// core did not read in the arena). Drawn on the bolt's own object at body density, redrawn each frame from
    /// the bolt's direction; a bolt dressed in other art (the missile, a fireball) hides it.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class EmberDart : MonoBehaviour
    {
        const int Half = 18, Tail = 14;
        const float Head = 3.2f;
        static readonly Color32 RimColor = new(38, 10, 4, 255);
        const float Ppu = TexelFx.Body;

        [SerializeField] EnemyProjectile _bolt;
        [SerializeField] SpriteRenderer _sr;
        [SerializeField] int _seed;
        Texture2D _tex;
        Color32[] _px;

        public static void On(EnemyProjectile bolt, int order)
        {
            var d = bolt.gameObject.AddComponent<EmberDart>();
            d._bolt = bolt;
            d._seed = Mathf.Abs(bolt.GetEntityId().GetHashCode()) % 997;
            var go = new GameObject("bolt.ember");
            go.transform.SetParent(bolt.transform, false);
            d._sr = go.AddComponent<SpriteRenderer>();
            d._sr.sortingOrder = order;
        }

        void LateUpdate()
        {
            if (_bolt == null || _bolt.Dressed) { if (_sr != null) _sr.enabled = false; enabled = false; return; }
            int size = Half * 2 + 1;
            if (_tex == null)
            {
                _tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _px = new Color32[size * size];
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            }
            System.Array.Clear(_px, 0, _px.Length);
            var dir = _bolt.Direction;
            int frame = Mathf.FloorToInt(Time.time * 14f);
            // A round head with a white-hot heart, and a tail that tapers back along the flight,
            // its cool end flickering; then a dark rim round the lot so it reads on any ground.
            for (int y = -Half; y <= Half; y++)
            for (int x = -Half; x <= Half; x++)
            {
                float r = Mathf.Sqrt(x * x + y * y);
                float back = -(x * dir.x + y * dir.y);
                float side = Mathf.Abs(x * dir.y - y * dir.x);
                if (r <= Head)
                    Put(x, y, FireArt.At(r <= 1.3f ? 0f : r <= 2.3f ? 0.15f : 0.35f));
                else if (back > 0f && back <= Tail && side <= Mathf.Lerp(Head, 0.6f, back / Tail))
                {
                    float u = back / Tail;
                    if (u > 0.45f && FireArt.Hash(x + 40, y + 40, frame + _seed) > 1.15f - u) continue;
                    Put(x, y, FireArt.At(0.3f + u * 0.65f));
                }
            }
            Rim();
            _tex.SetPixels32(_px);
            _tex.Apply();

            // Unrotated, the root's scale undone, so a texel is a body texel.
            var s = _bolt.transform.lossyScale;
            _sr.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, s.x), 1f / Mathf.Max(1e-4f, s.y), 1f);
            _sr.transform.rotation = Quaternion.identity;
            _sr.enabled = true;
        }

        /// <summary>A one-texel dark edge round every lit texel.</summary>
        void Rim()
        {
            int size = Half * 2 + 1;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (_px[y * size + x].a != 0) continue;
                bool edge = (x > 0 && Lit(x - 1, y)) || (x < size - 1 && Lit(x + 1, y))
                         || (y > 0 && Lit(x, y - 1)) || (y < size - 1 && Lit(x, y + 1));
                if (edge) _px[y * size + x] = RimColor;
            }
        }

        bool Lit(int x, int y)
        {
            var c = _px[y * (Half * 2 + 1) + x];
            return c.a != 0 && !(c.r == RimColor.r && c.g == RimColor.g && c.b == RimColor.b);
        }

        void Put(int dx, int dy, Color32 c)
        {
            int x = Half + dx, y = Half + dy, size = Half * 2 + 1;
            if (x < 0 || y < 0 || x >= size || y >= size) return;
            _px[y * size + x] = c;
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
