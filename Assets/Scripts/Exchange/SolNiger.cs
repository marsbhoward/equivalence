using UnityEngine;
using Convergence.Core;

namespace Convergence.Exchange
{
    /// <summary>
    /// SOL NIGER, the black sun (a Putrefaction of Fog and Wandering Eye): past a few units from
    /// the player the floor goes dark, and everything out there - enemies, their telegraphs, the
    /// ground - is a silhouette against it.
    ///
    /// One sprite following the player: black, with a soft clear circle at its centre whose world
    /// radius is the ledger's SilhouetteBeyond, big enough to cover the arena wherever the player
    /// stands. Drawn above everything in the world and below the HUD, so the player (always inside
    /// the circle) is never covered. Built once and cached; a domain reload rebuilds it on demand.
    /// </summary>
    public class SolNiger : MonoBehaviour
    {
        const int Size = 256;
        const float WorldSize = 64f;
        const float Darkness = 0.86f;
        const float SoftEdge = 0.8f;   // world units the dark fades in over

        [SerializeField] Transform _follow;
        static Sprite _sprite;
        static float _spriteRadius;
        static SolNiger _live;

        /// <summary>The dark on (beyond <paramref name="radius"/> units of the player) or off (0).</summary>
        public static void Set(Transform player, float radius)
        {
            if (radius <= 0f || player == null) { Clear(); return; }
            if (_live == null)
            {
                var go = new GameObject("sol-niger");
                _live = go.AddComponent<SolNiger>();
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = SortingOrders.StatusOverlay - 20;
                sr.color = new Color(0f, 0f, 0f, Darkness);
            }
            _live._follow = player;
            var r = _live.GetComponent<SpriteRenderer>();
            r.sprite = SpriteFor(radius);
            _live.transform.localScale = Vector3.one;
        }

        public static void Clear()
        {
            if (_live != null) Destroy(_live.gameObject);
            _live = null;
        }

        static Sprite SpriteFor(float radius)
        {
            if (_sprite != null && Mathf.Approximately(_spriteRadius, radius)) return _sprite;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float ppu = Size / WorldSize;
            float c = Size * 0.5f;
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) / ppu;
                float a = Mathf.Clamp01((d - radius) / SoftEdge);
                px[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), ppu);
            _spriteRadius = radius;
            return _sprite;
        }

        void LateUpdate()
        {
            if (_follow == null) { Clear(); return; }
            transform.position = _follow.position;
        }
    }
}
