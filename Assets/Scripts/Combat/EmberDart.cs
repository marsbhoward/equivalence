using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// The Stinger's ordinary bolt (the user's pick, 2026-10-26 - "ember dart"): a bright
    /// two-texel core with a tail of fire tapering back along its flight, flickering. It replaced
    /// an orange circle. Drawn on the bolt's own object at body density, redrawn each frame from
    /// the bolt's direction; a bolt dressed in other art (the missile, a fireball) hides it.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class EmberDart : MonoBehaviour
    {
        const int Half = 9, Tail = 8;
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
            for (int i = Tail - 1; i >= 0; i--)
            {
                if (i > 2 && FireArt.Hash(i, frame, _seed) > 0.7f) continue;
                Put(Mathf.RoundToInt(-dir.x * i), Mathf.RoundToInt(-dir.y * i), FireArt.At(0.1f + i / (float)(Tail - 1) * 0.75f));
            }
            Put(0, 0, FireArt.At(0f));
            Put(0, 1, FireArt.At(0.2f));
            _tex.SetPixels32(_px);
            _tex.Apply();

            // Unrotated, the root's scale undone, so a texel is a body texel.
            var s = _bolt.transform.lossyScale;
            _sr.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, s.x), 1f / Mathf.Max(1e-4f, s.y), 1f);
            _sr.transform.rotation = Quaternion.identity;
            _sr.enabled = true;
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
