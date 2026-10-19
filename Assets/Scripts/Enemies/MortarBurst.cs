using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// A Salamander's shell going off (the user's sign-off, 2026-10-19): a flash of the blast's
    /// ring and a spray of magma droplets out to its edge, white-yellow cooling to crimson, in
    /// place of a soft flash. Blue for a shell the player turned. A picture only - the blast's
    /// damage is MortarShell's. One texture on the texel grid, redrawn for its short life.
    /// </summary>
    public class MortarBurst : MonoBehaviour
    {
        const float Seconds = 0.4f;
        const int Drops = 44;

        SpriteRenderer _sr;
        Texture2D _tex;
        Color[] _px, _ramp;
        int _size;
        float _r, _born;

        public static void Spawn(Vector2 at, float radius, Color[] ramp, Transform parent)
        {
            var go = new GameObject("mortar.burst");
            if (parent != null) go.transform.SetParent(parent, false);
            var b = go.AddComponent<MortarBurst>();
            float tpu = TexelRig.TexelsPerUnit;
            b._ramp = ramp;
            b._r = radius * tpu;
            b._size = Mathf.CeilToInt(b._r) * 2 + 16;
            b._born = Time.time;
            go.transform.position = new Vector3(Mathf.Round(at.x * tpu - b._size / 2f) / tpu, Mathf.Round(at.y * tpu - b._size / 2f) / tpu, 0f);
            b._sr = go.AddComponent<SpriteRenderer>();
            b._sr.sortingOrder = SortingOrders.Telegraph + 2;
            b._tex = new Texture2D(b._size, b._size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            b._px = new Color[b._size * b._size];
            b._sr.sprite = Sprite.Create(b._tex, new Rect(0, 0, b._size, b._size), Vector2.zero, tpu, 0, SpriteMeshType.FullRect);
            b.Paint();
        }

        void Update()
        {
            if (Time.time - _born > Seconds) { Destroy(gameObject); return; }
            Paint();
        }

        void Paint()
        {
            float k = Mathf.Clamp01((Time.time - _born) / Seconds), c = _size / 2f;
            System.Array.Clear(_px, 0, _px.Length);
            // the edge flashes first
            if (k < 0.3f)
                for (int deg = 0; deg < 360; deg += 3)
                    Put(c + Mathf.Cos(TexelRig.Rad(deg)) * _r, c + Mathf.Sin(TexelRig.Rad(deg)) * _r, _ramp[3], 1f - k / 0.3f);
            for (int i = 0; i < Drops; i++)
            {
                float a = i / (float)Drops * Mathf.PI * 2f + TexelRig.Hash(i, 4), r = _r * (0.15f + k * (0.6f + TexelRig.Hash(i, 8) * 0.5f));
                Put(c + Mathf.Cos(a) * r, c + Mathf.Sin(a) * r, k < 0.3f ? _ramp[3] : k < 0.6f ? _ramp[2] : _ramp[1], 1f - k);
            }
            _tex.SetPixels(_px);
            _tex.Apply();
        }

        void Put(float x, float y, Color col, float alpha)
        {
            int ix = Mathf.RoundToInt(x), iy = Mathf.RoundToInt(y);
            if (ix < 0 || iy < 0 || ix >= _size || iy >= _size) return;
            col.a = alpha;
            _px[iy * _size + ix] = col;
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
