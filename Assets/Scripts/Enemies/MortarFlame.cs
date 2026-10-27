using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// The Salamander's fire, drawn (the user's sign-off, 2026-10-19): a gout pouring from its jaws
    /// down the cone it burns - white-yellow at the jaws, crimson at the tips, flickering - in place
    /// of two soft cone sprites. Through the 0.4 s wind-up it is only a few licks of flame along the
    /// aim (<see cref="Tell"/>), so the way it will burn still reads from above.
    ///
    /// One axis-aligned texture on the texel grid round the jaws, redrawn each frame from the aim
    /// (never a rotated sprite - rotated texels shear). The damage is the controller's
    /// (EnemyController.InFlame, measured from the body); this is only the picture.
    /// </summary>
    public class MortarFlame : MonoBehaviour
    {
        const int Specks = 1000;

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
            float c = _size / 2f, baseAng = Mathf.Atan2(_dir.y, _dir.x);
            int frame = Mathf.FloorToInt(Time.time * 20f);
            int n = _tell ? Specks / 10 : Specks;
            float reach = _tell ? _reach * 0.35f : _reach;
            for (int i = 0; i < n; i++)
            {
                float u = TexelRig.Hash(i, frame), v = TexelRig.Hash(frame, i);
                float d = Mathf.Sqrt(u) * reach;                                 // denser out where the cone is wide
                float a = baseAng + (v - 0.5f) * 2f * TexelRig.Rad(_halfAngle) * (_tell ? 0.4f : 1f);
                float k = d / _reach;
                var col = k < 0.2f ? acc[3] : k < 0.55f ? acc[2] : acc[1];
                Put(Mathf.RoundToInt(c + Mathf.Cos(a) * d), Mathf.RoundToInt(c + Mathf.Sin(a) * d), col, (_tell ? 0.6f : 1f) * (1f - k * 0.7f));
            }
            _tex.SetPixels(_px);
            _tex.Apply();
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
