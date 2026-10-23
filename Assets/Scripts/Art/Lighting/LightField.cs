using UnityEngine;
using Convergence.Core;

namespace Convergence.Art.Lighting
{
    /// <summary>
    /// The room's DARKNESS, drawn as one texel picture laid over the world: darkest where no light
    /// reaches, cut away in banded, dithered pools round every <see cref="Glow"/>.
    ///
    /// WHY AN OVERLAY AND NOT URP's GLOBAL LIGHT: every sprite here is lit, so dimming the global
    /// light would darken the telegraphs, the hit flashes and the strike bar with the room. This
    /// sorts just UNDER the overlay band (<see cref="SortingOrders.StatusOverlay"/>) and over the
    /// whole depth band - every body and every stone is in shadow, and every readout stays at full
    /// brightness. The neutral global light is untouched.
    ///
    /// Two body pixels to a texel, snapped to the world's texel grid so it never swims; the camera's
    /// view plus a margin, recomputed <see cref="Tuning.Lighting.UpdatesPerSecond"/> times a second.
    /// A light's falloff is quantised into <see cref="Tuning.Lighting.Bands"/> steps with an ordered
    /// dither between them - a pool with stepped rings, the way the pixel art would draw one.
    ///
    /// The PLACE decides the darkness and its colour (<see cref="SetPlace"/>): today the depth band,
    /// later the biome. Darkness 0 switches it off (the hub).
    /// </summary>
    public class LightField : MonoBehaviour
    {
        const float Ppu = 18.75f;   // two body texels (37.5) to a light texel

        static LightField _field;
        SpriteRenderer _sr;
        Texture2D _tex;
        Color32[] _px;
        float[] _light;
        Color[] _tint;
        float[] _wash;
        int _w, _h;
        float _next;

        [SerializeField] float _darkness;
        [SerializeField] Color _shade = Color.black;

        static readonly float[] Bayer =
            { 0f, 8f, 2f, 10f, 12f, 4f, 14f, 6f, 3f, 11f, 1f, 9f, 15f, 7f, 13f, 5f };

        /// <summary>The place's darkness (0 off .. 1 black where no light reaches) and its colour.</summary>
        public static void SetPlace(float darkness, Color shade)
        {
            var f = Get();
            if (f == null) return;
            f._darkness = Mathf.Clamp01(darkness);
            f._shade = shade;
            f._next = 0f;
        }

        public static float Darkness => _field != null ? _field._darkness : 0f;

        static LightField Get()
        {
            if (_field != null) return _field;
            _field = FindAnyObjectByType<LightField>();
            if (_field != null) return _field;
            if (!Application.isPlaying) return null;
            var go = new GameObject("LightField");
            _field = go.AddComponent<LightField>();
            return _field;
        }

        void LateUpdate()
        {
            if (_sr == null)
            {
                _sr = GetComponent<SpriteRenderer>();
                if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
                _sr.sortingOrder = SortingOrders.StatusOverlay - 10;
            }
            var cam = Camera.main;
            if (_darkness <= 0.001f || cam == null || !cam.orthographic) { _sr.enabled = false; return; }
            _sr.enabled = true;

            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f / Tuning.Lighting.UpdatesPerSecond;

            // The view plus a margin, its corner on the world's light-texel grid.
            float halfH = cam.orthographicSize + 1f, halfW = cam.orthographicSize * cam.aspect + 1f;
            Vector2 c = cam.transform.position;
            float x0 = Mathf.Floor((c.x - halfW) * Ppu) / Ppu, y0 = Mathf.Floor((c.y - halfH) * Ppu) / Ppu;
            int w = Mathf.CeilToInt(halfW * 2f * Ppu) + 1, h = Mathf.CeilToInt(halfH * 2f * Ppu) + 1;
            Ensure(w, h);
            transform.position = new Vector3(x0, y0, 0f);

            System.Array.Clear(_light, 0, _light.Length);
            System.Array.Clear(_wash, 0, _wash.Length);
            var all = Glow.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var g = all[i];
                if (g == null) { all.RemoveAt(i); continue; }
                if (!g.isActiveAndEnabled || !g.Sample(out var at, out float r, out float s, out var col)) continue;
                Cast(at - new Vector2(x0, y0), r, s, col, g.Core >= 0f ? g.Core : Tuning.Lighting.CoreFraction, g.Wash);
            }

            float dark = _darkness, warm = Tuning.Lighting.LitTintAlpha;
            int bands = Tuning.Lighting.Bands;
            for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                int i = y * _w + x;
                // the world's own texel index, so the dither stays put as the view moves
                int wx = Mathf.FloorToInt(x0 * Ppu) + x, wy = Mathf.FloorToInt(y0 * Ppu) + y;
                // Hard steps, NOT dithered between: a dither's checkerboard lands on the bodies
                // too, and a character under it reads as see-through (the user's report).
                float d = Tuning.Lighting.Dither ? (Bayer[((wy & 3) << 2) | (wx & 3)] + 0.5f) / 16f : 0.5f;
                float q = Mathf.Clamp01(Mathf.Floor(_light[i] * bands + d) / bands);
                // The light's own colour only near the core of the pool: blended in sooner, the
                // half-lit rings were a coloured haze over the walls rather than light on them.
                float k = q * q * q * q;
                // A WASHED light (Glow.Wash - the Nigredo's reds) casts its colour over the whole
                // pool, a modest alpha even where fully lit; every other light clears to no tint.
                float wash = _wash[i];
                Color col = wash > 0f ? Color.Lerp(_shade, _tint[i], q) : k > 0f ? Color.Lerp(_shade, _tint[i], k) : _shade;
                float a = Mathf.Lerp(dark, Mathf.Max(warm, wash), q);
                _px[i] = new Color32((byte)(col.r * 255f), (byte)(col.g * 255f), (byte)(col.b * 255f), (byte)(a * 255f));
            }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        /// <summary>One light's pool, in view-local world units. The strongest light at a texel
        /// wins (max, not sum - two torches side by side do not burn a white hole) and lends it
        /// its colour.</summary>
        void Cast(Vector2 at, float radius, float strength, Color col, float core, float wash)
        {
            int cx = Mathf.FloorToInt(at.x * Ppu), cy = Mathf.FloorToInt(at.y * Ppu);
            int rr = Mathf.CeilToInt(radius * Ppu);
            int xa = Mathf.Max(0, cx - rr), xb = Mathf.Min(_w - 1, cx + rr);
            int ya = Mathf.Max(0, cy - rr), yb = Mathf.Min(_h - 1, cy + rr);
            float inv = 1f / (radius * Ppu);
            for (int y = ya; y <= yb; y++)
            for (int x = xa; x <= xb; x++)
            {
                float dx = (x + 0.5f - at.x * Ppu) * inv, dy = (y + 0.5f - at.y * Ppu) * inv;
                float dd = Mathf.Sqrt(dx * dx + dy * dy);
                if (dd >= 1f) continue;
                float f = dd <= core ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (dd - core) / (1f - core));
                float v = f * strength;
                int i = y * _w + x;
                if (v > _light[i]) { _light[i] = v; _tint[i] = col; _wash[i] = wash; }
            }
        }

        void Ensure(int w, int h)
        {
            if (_tex != null && w == _w && h == _h && _px != null && _light != null && _wash != null) return;
            if (_sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
            _w = w; _h = h;
            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "LightField" };
            _px = new Color32[w * h];
            _light = new float[w * h];
            _tint = new Color[w * h];
            _wash = new float[w * h];
            _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, w, h), Vector2.zero, Ppu);
        }

        void OnDestroy() { if (_tex != null) Destroy(_tex); }
    }
}
