using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.ReleaseFx;

namespace Convergence.Player
{
    /// <summary>
    /// SURGE's window (the user's pick, 2026-10-25 - "undertow"): two ribbons of water circle the
    /// body, one low and one at the waist, for as long as the surge runs, turning faster the more
    /// attack speed it gives. One texture on the floor's grid at the feet, sorted one under the
    /// rig's lowest layer: a ribbon passing in front is hidden exactly where it crosses the body,
    /// so it never covers the player. Opens and closes by easing its width.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class UndertowRibbons : MonoBehaviour
    {
        const float Ppu = Art.FloorArt.PixelsPerUnit;

        [SerializeField] WaterResource _water;
        [SerializeField] SpriteRenderer _sr;
        [SerializeField] float _open, _phase;
        Texture2D _tex;
        Color32[] _px;
        int _w, _h, _cx;

        public static UndertowRibbons Attach(WaterResource water)
        {
            var go = new GameObject("water.undertow");
            go.transform.SetParent(water.transform, false);
            var u = go.AddComponent<UndertowRibbons>();
            u._water = water;
            u._sr = go.AddComponent<SpriteRenderer>();
            u._sr.enabled = false;
            return u;
        }

        void LateUpdate()
        {
            if (_water == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            _open = Mathf.MoveTowards(_open, _water.Surging ? 1f : 0f, dt / T.UndertowInSeconds);
            if (_open <= 0f) { _sr.enabled = false; return; }
            _phase += dt * T.UndertowSpin * (1f + Mathf.Max(0f, _water.AttackSpeedPoints) / 100f);

            float bodyHeight = BodyHeight();
            if (_tex == null)
            {
                _cx = Mathf.CeilToInt(T.UndertowRadius * Ppu) + 2;
                _w = _cx * 2 + 1;
                _h = Mathf.CeilToInt(bodyHeight * T.UndertowHigh * Ppu + T.UndertowRadius * 0.25f * Ppu) + 4;
                _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _px = new Color32[_w * _h];
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, _w, _h), Vector2.zero, Ppu, 0, SpriteMeshType.FullRect);
            }
            System.Array.Clear(_px, 0, _px.Length);

            float rx = T.UndertowRadius * Ppu * _open, ry = rx * 0.25f;
            for (int r = 0; r < 2; r++)
            {
                float hy = bodyHeight * (r == 0 ? T.UndertowLow : T.UndertowHigh) * Ppu;
                for (int i = 0; i < T.UndertowTexels; i++)
                {
                    float a = _phase + r * Mathf.PI - i * 0.09f;
                    int x = _cx + Mathf.RoundToInt(Mathf.Cos(a) * rx);
                    int y = Mathf.RoundToInt(hy - Mathf.Sin(a) * ry);
                    float u = i / (float)T.UndertowTexels;
                    Put(x, y, ReleaseCanvas.At(T.Water, 0.05f + u * 0.5f));
                    Put(x, y - 1, ReleaseCanvas.At(T.Water, 0.3f + u * 0.5f));
                }
            }
            _tex.SetPixels32(_px);
            _tex.Apply();

            var feet = (Vector2)Shadow().position;
            transform.position = new Vector3((Mathf.Round(feet.x * Ppu) - _cx) / Ppu, Mathf.Round(feet.y * Ppu) / Ppu, 0f);
            _sr.sortingOrder = ReleaseCanvas.UnderBody(_water.transform);
            _sr.enabled = true;
        }

        /// <summary>The figure's drawn height, feet to crown - read off the rig's layers.</summary>
        float BodyHeight()
        {
            var rig = _water.GetComponentInChildren<Art.Gear.ICharacterRig>() as MonoBehaviour;
            if (rig == null) return 1f;
            float top = float.MinValue;
            foreach (var r in rig.GetComponentsInChildren<SpriteRenderer>(false))
                if (r != null && r.enabled && r.sprite != null && r.sortingOrder < SortingOrders.StatusOverlay)
                    top = Mathf.Max(top, r.bounds.max.y);
            float feet = Shadow().position.y;
            return top > feet ? Mathf.Min(top - feet, 2f) : 1f;
        }

        void Put(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;
            _px[y * _w + x] = c;
        }

        [SerializeField] Transform _shadow;
        Transform Shadow() => _shadow != null ? _shadow : (_shadow = ReleaseCanvas.ShadowOf(_water.transform));

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
