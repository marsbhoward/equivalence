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

        // ------------------------------------------------------------------ the edge

        // EDGES FALL INTO THE DARK (the user's call, 2026-11-01): on a floor with open edges the
        // darkness thickens with distance from the ground - or, in the bright places, turns to
        // haze - so the room fades out at its lip instead of stopping. A coarse map of 0..1 laid
        // by Art.Biomes.Ground once per floor; 1 everywhere off the map. Plain statics: a domain
        // reload drops it and the room is simply lit as before until the next floor.
        static float[] _edge;
        static int _edgeW, _edgeH;
        static Vector2 _edgeOrigin;
        static float _edgeCell, _edgeAlpha;
        static Color _edgeShade;

        /// <summary>Lay the floor's edge map: <paramref name="map"/> (w x h, bottom row first,
        /// cells of <paramref name="cell"/> units from <paramref name="origin"/>), how dark the
        /// edge goes at 1 (<paramref name="alpha"/>) and in what colour.</summary>
        public static void SetEdge(float[] map, int w, int h, Vector2 origin, float cell, float alpha, Color shade)
        {
            _edge = map; _edgeW = w; _edgeH = h; _edgeOrigin = origin; _edgeCell = cell;
            _edgeAlpha = alpha; _edgeShade = shade;
            if (_field != null) _field._next = 0f;
        }

        public static void ClearEdge() => _edge = null;

        /// <summary>The place's shade colour, as last set - so a fight can lift the dark without changing its colour.</summary>
        public static Color Shade => _field != null ? _field._shade : Color.black;

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
            // Most of the view is unlit (q 0): such a texel is the shade, darkened toward the edge by
            // one of bands + 1 steps, so those are worked out once here and looked up below. The
            // edge cell under each column and row is found once too. Lit texels are worked out as before.
            int wx0 = Mathf.FloorToInt(x0 * Ppu), wy0 = Mathf.FloorToInt(y0 * Ppu);
            EnsureEdgeCells(wx0, wy0, bands);
            if (_unlit == null || _unlit.Length != bands + 1) _unlit = new Color32[bands + 1];
            for (int L = 0; L <= bands; L++) _unlit[L] = Shaded(_shade, dark, L <= 0 ? 0f : L / (float)bands, 0f);
            for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                int i = y * _w + x;
                // the world's own texel index, so the dither stays put as the view moves
                int wx = wx0 + x, wy = wy0 + y;
                // Hard steps, NOT dithered between: a dither's checkerboard lands on the bodies
                // too, and a character under it reads as see-through (the user's report).
                float d = Tuning.Lighting.Dither ? (Bayer[((wy & 3) << 2) | (wx & 3)] + 0.5f) / 16f : 0.5f;
                float q = Mathf.Clamp01(Mathf.Floor(_light[i] * bands + d) / bands);
                if (q <= 0f) { _px[i] = _unlit[EdgeLevel(x, y)]; continue; }
                // The light's own colour only near the core of the pool: blended in sooner, the
                // half-lit rings were a coloured haze over the walls rather than light on them.
                float k = q * q * q * q;
                // A WASHED light (Glow.Wash - the Nigredo's reds) casts its colour over the whole
                // pool, a modest alpha even where fully lit; every other light clears to no tint.
                float wash = _wash[i];
                Color col = wash > 0f ? Color.Lerp(_shade, _tint[i], q) : k > 0f ? Color.Lerp(_shade, _tint[i], k) : _shade;
                float a = Mathf.Lerp(dark, Mathf.Max(warm, wash), q);
                int lv = EdgeLevel(x, y);
                _px[i] = Shaded(col, a, lv <= 0 ? 0f : lv / (float)bands, q);
            }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        Color32[] _unlit;
        int[] _edgeCol, _edgeRow;
        int[] _edgeLv;
        float[] _edgeLvOf;

        /// <summary>A texel of colour <paramref name="col"/> at alpha <paramref name="a"/>, lit <paramref name="q"/>, faded
        /// toward the edge by <paramref name="e"/> (already stepped, like the light, so the fade is bands rather than a
        /// smooth haze; a light still cuts into it - a torch at the lip lights the drop below it).</summary>
        static Color32 Shaded(Color col, float a, float e, float q)
        {
            if (_edge != null && e > 0f)
            {
                float lit = 1f - q;
                col = Color.Lerp(col, _edgeShade, e * lit);
                a = Mathf.Lerp(a, Mathf.Max(a, _edgeAlpha), e * lit);
            }
            return new Color32((byte)(col.r * 255f), (byte)(col.g * 255f), (byte)(col.b * 255f), (byte)(a * 255f));
        }

        /// <summary>The edge map's cell under each light column and row (-1 off the map), and each cell's step.</summary>
        void EnsureEdgeCells(int wx0, int wy0, int bands)
        {
            if (_edgeCol == null || _edgeCol.Length != _w) _edgeCol = new int[_w];
            if (_edgeRow == null || _edgeRow.Length != _h) _edgeRow = new int[_h];
            if (_edge == null) return;
            for (int x = 0; x < _w; x++)
            {
                int cx = EdgeCell((wx0 + x + 0.5f) / Ppu, _edgeOrigin.x);
                _edgeCol[x] = cx < 0 || cx >= _edgeW ? -1 : cx;
            }
            for (int y = 0; y < _h; y++)
            {
                int cy = EdgeCell((wy0 + y + 0.5f) / Ppu, _edgeOrigin.y);
                _edgeRow[y] = cy < 0 || cy >= _edgeH ? -1 : cy;
            }
            // the map is laid once a floor: its steps are worked out again only when it changes
            if (_edgeLvOf != _edge || _edgeLv == null || _edgeLv.Length != _edge.Length)
            {
                _edgeLv = new int[_edge.Length];
                for (int k = 0; k < _edge.Length; k++) _edgeLv[k] = Mathf.FloorToInt(_edge[k] * bands + 0.5f);
                _edgeLvOf = _edge;
            }
        }

        // A texel centre can land exactly on a cell's side: the float rounding there is the old per-texel EdgeAt's,
        // so the same call shape (a float argument, then the cell).
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static int EdgeCell(float w, float origin) => Mathf.FloorToInt((w - origin) / _edgeCell);

        /// <summary>The edge's step at light texel (x, y): 0 none .. bands full (off the map is 1, full).</summary>
        int EdgeLevel(int x, int y)
        {
            if (_edge == null) return 0;
            int cx = _edgeCol[x], cy = _edgeRow[y];
            if (cx < 0 || cy < 0) return Tuning.Lighting.Bands;
            return _edgeLv[cy * _edgeW + cx];
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
            float inv = 1f / (radius * Ppu), ax = at.x * Ppu, ay = at.y * Ppu;
            for (int y = ya; y <= yb; y++)
            {
                float dy = (y + 0.5f - ay) * inv, dy2 = dy * dy;
                if (dy2 > 1f) continue;
                for (int x = xa; x <= xb; x++)
                {
                    float dx = (x + 0.5f - ax) * inv, d2 = dx * dx + dy2;
                    if (d2 > 1f) continue;
                    float dd = Mathf.Sqrt(d2);
                    if (dd >= 1f) continue;
                    float f = dd <= core ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (dd - core) / (1f - core));
                    float v = f * strength;
                    int i = y * _w + x;
                    if (v > _light[i]) { _light[i] = v; _tint[i] = col; _wash[i] = wash; }
                }
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
