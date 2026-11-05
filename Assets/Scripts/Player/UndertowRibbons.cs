using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.ReleaseFx;

namespace Convergence.Player
{
    /// <summary>
    /// SURGE's window - the UNDERTOW (the user's pick, 2026-10-25; redrawn 2026-11-05 from the
    /// mock sheet's "Riptide" - a name the exchange already uses): two ribbons of water circle the body, one low and one at the waist, for as long
    /// as the surge runs, turning faster the more attack speed it gives - wide enough to clear the
    /// body, foam riding on them; moving leaves a short wake, and every swing throws a splash off
    /// the ribbons. One texture on the floor's grid at the feet, sorted one under the
    /// rig's lowest layer: a ribbon passing in front is hidden exactly where it crosses the body,
    /// so it never covers the player. Opens and closes by easing its width.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class UndertowRibbons : MonoBehaviour
    {
        const float Ppu = Art.FloorArt.PixelsPerUnit;

        [SerializeField] WaterResource _water;
        [SerializeField] SpriteRenderer _sr;
        [SerializeField] float _open, _phase, _splash = -1f, _wakeIn;
        Vector2 _lastFeet;
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

        /// <summary>A swing during the surge: the ribbons throw a splash.</summary>
        public void Splash() { if (_open > 0.5f) _splash = 0f; }

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
                _cx = Mathf.CeilToInt(T.UndertowRadius * Ppu) + T.UndertowSplashTexels + 2;
                _w = _cx * 2 + 1;
                _h = Mathf.CeilToInt(bodyHeight * T.UndertowHigh * Ppu + T.UndertowRadius * 0.25f * Ppu) + T.UndertowSplashTexels + 4;
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
                    float a = _phase + r * Mathf.PI - i * (3.6f / T.UndertowTexels);
                    int x = _cx + Mathf.RoundToInt(Mathf.Cos(a) * rx);
                    int y = Mathf.RoundToInt(hy - Mathf.Sin(a) * ry);
                    float u = i / (float)T.UndertowTexels;
                    // Foam on the crest every few texels, the body of the water under it.
                    bool foam = i % 6 == 0 && u < 0.7f;
                    Put(x, y + 1, ReleaseCanvas.At(T.Water, foam ? 0f : 0.15f + u * 0.4f));
                    Put(x, y, ReleaseCanvas.At(T.Water, 0.2f + u * 0.45f));
                    Put(x, y - 1, ReleaseCanvas.At(T.Water, 0.45f + u * 0.4f));
                }

                // A swing's splash: drops thrown out off the ribbon, falling back.
                if (_splash >= 0f && _splash < T.UndertowSplashSeconds)
                {
                    float k = _splash / T.UndertowSplashSeconds;
                    for (int d = 0; d < 6; d++)
                    {
                        float a = _phase * 0.3f + d * 1.047f + r;
                        float out_ = rx + k * T.UndertowSplashTexels;
                        int x = _cx + Mathf.RoundToInt(Mathf.Cos(a) * out_);
                        int y = Mathf.RoundToInt(hy - Mathf.Sin(a) * ry + T.UndertowSplashTexels * 0.6f * (k * 2f - k * k * 2.2f));
                        Put(x, y, ReleaseCanvas.At(T.Water, 0.05f + k * 0.4f));
                    }
                }
            }
            if (_splash >= 0f) { _splash += dt; if (_splash > T.UndertowSplashSeconds) _splash = -1f; }
            _tex.SetPixels32(_px);
            _tex.Apply();

            var feet = (Vector2)Shadow().position;

            // The wake: moving under a surge drops a little ripple at the feet every so often.
            _wakeIn -= dt;
            if (_open >= 1f && (feet - _lastFeet).sqrMagnitude > T.UndertowWakeMinStep * T.UndertowWakeMinStep * dt * dt && _wakeIn <= 0f)
            {
                TexelFx.Ring(feet, T.Water, 0.08f, 0.3f, 0.35f);
                _wakeIn = T.UndertowWakeEvery;
            }
            _lastFeet = feet;
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
