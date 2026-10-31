using System.Collections.Generic;
using UnityEngine;
using Convergence.Chain;

namespace Convergence.Hub
{
    /// <summary>
    /// A DONADA card drawn by the game: the glass card spinning on its upright axis, its orbs circling
    /// with their trails and light pools - what the piece's own page draws, in a wall frame (the user's
    /// pick A, "smooth", 2026-10-31: https://claude.ai/artifact/D3knmPfHEea3Eap2TWAzP8).
    ///
    /// Drawn the way the page draws it, ported from its script (the projection, the depth curve, the
    /// trail and the orb's bloom, corona and disc), into a texture at SCREEN resolution like the other
    /// wallet art in frames - not the hub's texel grid, which leaves the card 20 texels wide. Only the
    /// spin and the orbits: touching it, flipping it and the effects are the live page's, in the
    /// web close-up (NftFrame). The colours come from the service (routes/nft.ts parseCard); the page
    /// itself never reaches the game.
    ///
    /// Each card is one Sprite whose texture is redrawn at <see cref="Fps"/> while something showing it
    /// touched it this last half second (a Frame, the picker's cell, the close-up), so a wallet of
    /// fifty cards costs nothing for the forty-seven nobody is looking at. Times come from the
    /// unscaled clock and a per-piece hash, never UnityEngine.Random, and nothing is saved.
    /// </summary>
    public sealed class NftCard
    {
        /// <summary>The wall card's height in pixels - a frame is about 110 screen px tall at 1080p.</summary>
        public const int WallHeight = 196;
        /// <summary>The native close-up's.</summary>
        public const int BigHeight = 560;
        const float Fps = 24f;
        const float TouchSeconds = 0.5f, EvictSeconds = 60f;

        // The page's own tunables (its script's header).
        const float OrbitDegPerSec = 24f, SpinDegPerSec = 12f;
        const float RFrac = 0.255f, CoreFrac = 0.0215f, FovMul = 2.6f, DepthMin = 0.40f;
        const float TrailArc = 132f;
        const int TrailLayers = 4;
        const float SpillR = 9f, SpillA = 0.19f;

        public Sprite Sprite { get; private set; }

        readonly int _w, _h;
        readonly float _seed;
        readonly Texture2D _tex;
        readonly Color32[] _out;
        readonly float[] _base, _face, _cov;   // premultiplied RGBA; a stroke's coverage
        readonly Orb[] _orbs;
        readonly Geo _front, _back;
        readonly int _trailN;                      // path samples - fewer at wall size, where they cannot be told apart
        readonly int[] _colX; readonly float[] _colT, _colGrow;   // the turn's per-column resampling (Squash)
        readonly float[] _lut = new float[LutN + 1];              // a glow's alpha by squared distance (Radial)
        const int LutN = 48;
        float _touched, _drawn = -1f;

        struct Orb { public Vector3 Core, Color, Bloom, Trail; }
        struct Geo { public float R, CosT, SinT, CosR, SinR, Fov, Rz, Rc, Phase; }
        struct P { public float X, Y, Z, S, D; }

        // ------------------------------------------------------------------ registry

        static Dictionary<string, NftCard> _cards = new();
        static Dictionary<Sprite, NftCard> _bySprite = new();
        static float _nextTick;

        /// <summary>The card for a piece at a height, made on first ask.</summary>
        public static NftCard Get(string key, NftCardSpec spec, int height)
        {
            if (string.IsNullOrEmpty(key) || spec == null) return null;
            _cards ??= new Dictionary<string, NftCard>();
            _bySprite ??= new Dictionary<Sprite, NftCard>();
            var id = key + "@" + height;
            if (_cards.TryGetValue(id, out var c) && c.Sprite != null) { c.Touch(); return c; }
            c = new NftCard(key, spec, height);
            _cards[id] = c;
            _bySprite[c.Sprite] = c;
            c.Touch();
            c.Draw(Time.unscaledTime);
            return c;
        }

        /// <summary>Keep a showing sprite moving - a no-op for any sprite that is not a card.</summary>
        public static void Touch(Sprite sprite)
        {
            if (sprite != null && _bySprite != null && _bySprite.TryGetValue(sprite, out var c)) c.Touch();
        }

        void Touch() => _touched = Time.unscaledTime;

        /// <summary>Redraw the cards being looked at; free the ones nobody has for a minute.
        /// Driven by WalletShowcaseSource.Update.</summary>
        public static void TickAll()
        {
            if (_cards == null || _cards.Count == 0) return;
            float now = Time.unscaledTime;
            if (now < _nextTick) return;
            _nextTick = now + 1f / Fps;

            List<string> gone = null;
            foreach (var kv in _cards)
            {
                var c = kv.Value;
                if (c.Sprite == null || now - c._touched > EvictSeconds) { (gone ??= new List<string>()).Add(kv.Key); continue; }
                if (now - c._touched <= TouchSeconds) c.Draw(now);
            }
            if (gone == null) return;
            foreach (var id in gone)
            {
                var c = _cards[id];
                _cards.Remove(id);
                if (c.Sprite != null) _bySprite.Remove(c.Sprite);
                c.Destroy();
            }
        }

        public static void DestroyAll()
        {
            if (_cards != null) foreach (var c in _cards.Values) c.Destroy();
            _cards = new Dictionary<string, NftCard>();
            _bySprite = new Dictionary<Sprite, NftCard>();
        }

        void Destroy()
        {
            if (Sprite != null) Object.Destroy(Sprite);
            if (_tex != null) Object.Destroy(_tex);
            Sprite = null;
        }

        // ------------------------------------------------------------------ build

        NftCard(string key, NftCardSpec spec, int height)
        {
            _h = height;
            _w = Mathf.RoundToInt(height * 5f / 7f) & ~1;   // the page's 5:7, even
            _seed = Hash01(key);

            var orbs = new List<Orb>();
            if (spec.Orbs != null)
                foreach (var o in spec.Orbs)
                {
                    var color = Rgb(o.Color, Vector3.one);
                    orbs.Add(new Orb { Core = Rgb(o.Core, color), Color = color, Bloom = Rgb(o.Bloom, color), Trail = Rgb(o.Trail, color) });
                }
            _orbs = orbs.ToArray();
            _front = GeoOf(spec.FrontOrbit, 44f, -14f, 0f);
            _back = GeoOf(spec.BackOrbit, 38f, 21f, 63f);

            _trailN = height >= 300 ? 40 : 24;
            _colX = new int[_w]; _colT = new float[_w]; _colGrow = new float[_w];
            _base = new float[_w * _h * 4];
            _face = new float[_w * _h * 4];
            _cov = new float[_w * _h];
            _out = new Color32[_w * _h];
            BuildGlass(Rgb(spec.Glass, Vector3.zero), spec.GlassAlpha > 0f ? spec.GlassAlpha : 0.8f);

            _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
            {
                // NOT Point: somebody's artwork in a frame, not our pixel grid (as WalletShowcaseSource).
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            // One unit tall, as the dev source's pictures - the Frame fits it either way.
            Sprite = Sprite.Create(_tex, new Rect(0, 0, _w, _h), new Vector2(0.5f, 0.5f), _h);
        }

        Geo GeoOf(NftOrbit o, float tilt, float roll, float phase)
        {
            // An absent orbit comes back from JsonUtility as all zeros: the template's own values then.
            if (o != null && (o.Tilt != 0f || o.Roll != 0f || o.Phase != 0f)) { tilt = o.Tilt; roll = o.Roll; phase = o.Phase; }
            float min = Mathf.Min(_w, _h), r = min * RFrac, t = tilt * Mathf.Deg2Rad, rl = roll * Mathf.Deg2Rad;
            return new Geo
            {
                R = r, CosT = Mathf.Cos(t), SinT = Mathf.Sin(t), CosR = Mathf.Cos(rl), SinR = Mathf.Sin(rl),
                Fov = r * FovMul, Rz = Mathf.Max(r * Mathf.Abs(Mathf.Sin(t)), 1e-3f), Rc = min * CoreFrac, Phase = phase,
            };
        }

        /// <summary>The glass, drawn once: its colour, the diagonal sheen, the rounded corners, a bright rim.</summary>
        void BuildGlass(Vector3 glass, float alpha)
        {
            var body = Vector3.Lerp(glass, new Vector3(30f, 30f, 34f) / 255f, 0.35f);
            float a = Mathf.Min(1f, alpha + 0.1f), radius = Mathf.Max(2f, _w * 0.06f);
            for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                float cover = RoundedCover(x + 0.5f, y + 0.5f, radius);
                if (cover <= 0f) continue;
                // The sheen runs from the top-left corner (canvas y down) to the bottom-right.
                float t = ((float)x / _w + (float)y / _h) * 0.5f;
                float sheen = t < 0.3f ? Mathf.Lerp(0.26f, 0.06f, t / 0.3f)
                            : t < 0.55f ? Mathf.Lerp(0.06f, 0.02f, (t - 0.3f) / 0.25f)
                            : Mathf.Lerp(0.02f, 0.12f, (t - 0.55f) / 0.45f);
                var c = body * a;
                float ca = a;
                c = c * (1f - sheen) + Vector3.one * sheen;   // white over, source-over
                ca = ca + sheen * (1f - ca);
                float rim = Mathf.Clamp01(1.5f - EdgeDistance(x + 0.5f, y + 0.5f, radius)) * 0.22f;
                c = c * (1f - rim) + Vector3.one * rim;
                ca = ca + rim * (1f - ca);
                // Over the page's own void (#07060a): the piece is designed against nothing but that
                // ("transparency has nothing to reveal"), so the card stays opaque on a brick wall too.
                c = c + new Vector3(7f, 6f, 10f) / 255f * (1f - ca);
                ca = 1f;
                int i = (y * _w + x) * 4;
                _base[i] = c.x * cover; _base[i + 1] = c.y * cover; _base[i + 2] = c.z * cover; _base[i + 3] = ca * cover;
            }
        }

        float RoundedCover(float x, float y, float r) => Mathf.Clamp01(EdgeDistance(x, y, r) + 0.5f);

        /// <summary>Distance inside the rounded rectangle's edge (negative outside).</summary>
        float EdgeDistance(float x, float y, float r)
        {
            float dx = Mathf.Max(r - x, x - (_w - r)), dy = Mathf.Max(r - y, y - (_h - r));
            if (dx > 0f && dy > 0f) return r - Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Min(Mathf.Min(x, _w - x), Mathf.Min(y, _h - y));
        }

        // ------------------------------------------------------------------ draw

        void Draw(float now)
        {
            if (Sprite == null || now == _drawn) return;
            _drawn = now;
            float t = now + _seed * 97f;
            float angle = (t * OrbitDegPerSec) % 360f;
            float spin = (t * SpinDegPerSec + _seed * 360f) % 360f;
            float cs = Mathf.Cos(spin * Mathf.Deg2Rad);
            bool front = cs >= 0f;

            System.Array.Copy(_base, _face, _base.Length);
            // The far side bleeds through the clear glass, mirrored; then the side facing us.
            DrawFace(front ? _back : _front, angle, 0.35f, mirror: true);
            DrawFace(front ? _front : _back, angle, 1f, mirror: false);
            Squash(Mathf.Abs(cs), _h >= 60 ? Mathf.Sin(spin * Mathf.Deg2Rad) * 0.04f * _h : 0f);

            _tex.SetPixelData(_out, 0);
            _tex.Apply(false);
        }

        P Project(in Geo g, float deg)
        {
            float a = deg * Mathf.Deg2Rad, px = Mathf.Cos(a) * g.R, py = Mathf.Sin(a) * g.R;
            float y = py * g.CosT, z = py * g.SinT;
            float rx = px * g.CosR - y * g.SinR, ry = px * g.SinR + y * g.CosR;
            float s = g.Fov / (g.Fov - z);
            return new P { X = rx * s, Y = ry * s, Z = z, S = s, D = (z / g.Rz + 1f) * 0.5f };
        }

        static float Lead(float d) => DepthMin + (1f - DepthMin) * Mathf.Pow(Mathf.Clamp01(d), 1.15f);

        readonly List<(float Deg, int I, float Z)> _order = new();

        void DrawFace(in Geo g, float angle, float k, bool mirror)
        {
            if (_orbs.Length == 0) return;
            float cx = _w * 0.5f, cy = _h * 0.5f, step = 360f / _orbs.Length, baseDeg = angle + g.Phase;
            _order.Clear();
            for (int i = 0; i < _orbs.Length; i++)
            {
                float deg = baseDeg + i * step;
                _order.Add((deg, i, Project(g, deg).Z));
            }
            _order.Sort((a, b) => a.Z.CompareTo(b.Z));   // far first
            float m = mirror ? -1f : 1f;

            // light pools on the glass, dropped below their orbs
            foreach (var (deg, i, _) in _order)
            {
                var p = Project(g, deg);
                float a = SpillA * Lead(p.D) * k;
                Radial(cx + m * p.X, cy + p.Y + g.Rc * p.S, g.Rc * SpillR * p.S, _orbs[i].Bloom,
                       a, 0.28f, a * 0.42f, 0.6f, a * 0.11f);
            }

            // The far side, seen through the glass, is its orbs and their light alone - its trails and
            // guide at a third of their strength did not read at wall size and cost as much as the rest.
            bool ghost = k < 1f;

            // the orbit's whisper of a guide, in the trails' average colour
            if (!ghost) {
            var guide = _orbs[0].Trail;
            for (int i = 1; i < _orbs.Length; i++) guide = Vector3.Lerp(guide, _orbs[i].Trail, 1f / (i + 1));
            Stroke(g, 180f, 360f, 24, Mathf.Max(0.6f, g.Rc * 0.44f), guide, 0.035f * k, cx, cy, m);
            Stroke(g, 0f, 180f, 24, Mathf.Max(0.6f, g.Rc * 0.6f), guide, 0.1f * k, cx, cy, m);
            }

            foreach (var (deg, i, _) in _order)
            {
                var o = _orbs[i];
                var head = Project(g, deg);
                float lead = Lead(head.D);
                // comet trail: stacked strokes, each shorter, fatter and nearer the core's colour
                for (int layer = 0; !ghost && layer < TrailLayers; layer++)
                {
                    float f = (float)layer / TrailLayers;
                    float width = Mathf.Max(0.5f, g.Rc * (0.35f + 1.4f * Mathf.Pow(f, 1.2f)) * head.S);
                    Stroke(g, deg - TrailArc * (1f - f), deg, Mathf.Max(8, Mathf.RoundToInt(_trailN * (1f - f))), width,
                           Vector3.Lerp(o.Trail, o.Core, Mathf.Pow(f, 1.8f)), 0.16f * lead * k, cx, cy, m);
                }
                // the orb: bloom, corona, then the solid disc over them
                float rc = g.Rc * head.S, x = cx + m * head.X, y = cy + head.Y, a = lead * k;
                Radial(x, y, rc * 8.7f, o.Bloom, 0.7f * a, 0.15f, 0.3f * a, 1f, 0f);
                Radial(x, y, rc * 3f, o.Color, 0.95f * a, 0.5f, 0.36f * a, 1f, 0f);
                Disc(x, y, Mathf.Max(0.7f, rc), o.Core, a);
            }
        }

        /// <summary>Additive radial light: alpha a0 at the centre, a1 at t1, a2 at t2, 0 at the rim.
        /// The curve is tabled by SQUARED distance once a call, so no pixel takes a square root.</summary>
        void Radial(float x, float y, float r, Vector3 c, float a0, float t1, float a1, float t2, float a2)
        {
            if (r < 0.3f || a0 <= 0.001f) return;
            for (int e = 0; e <= LutN; e++)
            {
                float t = Mathf.Sqrt((float)e / LutN);
                _lut[e] = t < t1 ? Mathf.Lerp(a0, a1, t / t1)
                        : t < t2 ? Mathf.Lerp(a1, a2, (t - t1) / Mathf.Max(1e-4f, t2 - t1))
                        : Mathf.Lerp(a2, 0f, (t - t2) / Mathf.Max(1e-4f, 1f - t2));
            }
            _lut[LutN] = 0f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(x - r)), x1 = Mathf.Min(_w - 1, Mathf.CeilToInt(x + r));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(y - r)), y1 = Mathf.Min(_h - 1, Mathf.CeilToInt(y + r));
            float scale = LutN / (r * r);
            float cr = c.x, cg = c.y, cb = c.z;
            var face = _face;
            for (int py = y0; py <= y1; py++)
            {
                float dy = py + 0.5f - y, dy2 = dy * dy;
                int row = py * _w;
                for (int px = x0; px <= x1; px++)
                {
                    float dx = px + 0.5f - x;
                    float u = (dx * dx + dy2) * scale;
                    if (u >= LutN) continue;
                    int e = (int)u;
                    float a = _lut[e] + (_lut[e + 1] - _lut[e]) * (u - e);
                    int i = (row + px) * 4;
                    face[i] += cr * a; face[i + 1] += cg * a; face[i + 2] += cb * a; face[i + 3] += a;
                }
            }
        }

        /// <summary>A solid disc over what is there, its edge antialiased.</summary>
        void Disc(float x, float y, float r, Vector3 c, float a)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(x - r - 1)), x1 = Mathf.Min(_w - 1, Mathf.CeilToInt(x + r + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(y - r - 1)), y1 = Mathf.Min(_h - 1, Mathf.CeilToInt(y + r + 1));
            for (int py = y0; py <= y1; py++)
            for (int px = x0; px <= x1; px++)
            {
                float dx = px + 0.5f - x, dy = py + 0.5f - y;
                float cover = Mathf.Clamp01(r + 0.5f - Mathf.Sqrt(dx * dx + dy * dy)) * a;
                if (cover <= 0f) continue;
                int i = (py * _w + px) * 4;
                _face[i] = _face[i] * (1f - cover) + c.x * cover;
                _face[i + 1] = _face[i + 1] * (1f - cover) + c.y * cover;
                _face[i + 2] = _face[i + 2] * (1f - cover) + c.z * cover;
                _face[i + 3] = _face[i + 3] * (1f - cover) + cover;
            }
        }

        /// <summary>
        /// A path along the orbit stroked ONCE: each pixel takes its greatest coverage from any segment
        /// and is lit a single time, as a canvas stroke() is - adding segment by segment would bead
        /// the line at every joint, the very thing the page's own comment warns about.
        /// </summary>
        void Stroke(in Geo g, float from, float to, int n, float width, Vector3 c, float a, float cx, float cy, float m)
        {
            if (a <= 0.002f) return;
            float half = width * 0.5f, reach2 = (half + 0.5f) * (half + 0.5f);
            int bx0 = _w, by0 = _h, bx1 = -1, by1 = -1;
            var prev = Project(g, from);
            float ax = cx + m * prev.X, ay = cy + prev.Y;
            for (int s = 1; s <= n; s++)
            {
                var p = Project(g, from + (to - from) * s / n);
                float bx = cx + m * p.X, by = cy + p.Y;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, bx) - half - 1)), x1 = Mathf.Min(_w - 1, Mathf.CeilToInt(Mathf.Max(ax, bx) + half + 1));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, by) - half - 1)), y1 = Mathf.Min(_h - 1, Mathf.CeilToInt(Mathf.Max(ay, by) + half + 1));
                float vx = bx - ax, vy = by - ay, len2 = Mathf.Max(1e-6f, vx * vx + vy * vy);
                for (int py = y0; py <= y1; py++)
                for (int px = x0; px <= x1; px++)
                {
                    float qx = px + 0.5f - ax, qy = py + 0.5f - ay;
                    float t = Mathf.Clamp01((qx * vx + qy * vy) / len2);
                    float dx = qx - vx * t, dy = qy - vy * t, d2 = dx * dx + dy * dy;
                    if (d2 >= reach2) continue;
                    float cover = Mathf.Clamp01(half + 0.5f - Mathf.Sqrt(d2));
                    int i = py * _w + px;
                    if (cover > _cov[i]) _cov[i] = cover;
                }
                if (x0 < bx0) bx0 = x0; if (y0 < by0) by0 = y0; if (x1 > bx1) bx1 = x1; if (y1 > by1) by1 = y1;
                ax = bx; ay = by;
            }
            for (int py = by0; py <= by1; py++)
            for (int px = bx0; px <= bx1; px++)
            {
                int i = py * _w + px;
                if (_cov[i] <= 0f) continue;
                Add(px, py, c, a * _cov[i]);
                _cov[i] = 0f;
            }
        }

        /// <summary>Light added, as the page's 'lighter' compositing does.</summary>
        void Add(int x, int y, Vector3 c, float a)
        {
            int i = (y * _w + x) * 4;
            _face[i] += c.x * a; _face[i + 1] += c.y * a; _face[i + 2] += c.z * a; _face[i + 3] += a;
        }

        /// <summary>
        /// The turn: the face narrowed about the middle by |cos(spin)|, the near edge a little taller
        /// than the far one - then into the texture. Canvas rows run down, texture rows up. Each
        /// column's source and height are worked out once, then every row only blends.
        /// </summary>
        void Squash(float sx, float taper)
        {
            float dw = _w * sx, x0 = (_w - dw) * 0.5f;
            for (int x = 0; x < _w; x++)
            {
                float t = (x + 0.5f - x0) / Mathf.Max(1e-3f, dw);
                if (dw < 0.5f || t < 0f || t >= 1f) { _colX[x] = -1; continue; }
                float fx = Mathf.Clamp(t * _w - 0.5f, 0f, _w - 1.001f);
                _colX[x] = (int)fx;
                _colT[x] = fx - (int)fx;
                _colGrow[x] = _h / (_h + (t - 0.5f) * taper * 2f);
            }
            var face = _face;
            float half = _h * 0.5f;
            for (int x = 0; x < _w; x++)
            {
                int sx0 = _colX[x];
                if (sx0 < 0) { for (int y = 0; y < _h; y++) _out[y * _w + x] = default; continue; }
                int sx1 = Mathf.Min(sx0 + 1, _w - 1);
                float tx = _colT[x], k = _colGrow[x];
                for (int y = 0; y < _h; y++)
                {
                    int o = (_h - 1 - y) * _w + x;
                    float sy = (y + 0.5f - half) * k + half - 0.5f;
                    if (sy < -0.5f || sy > _h - 0.5f) { _out[o] = default; continue; }
                    if (sy < 0f) sy = 0f; else if (sy > _h - 1.001f) sy = _h - 1.001f;
                    int sy0 = (int)sy;
                    float ty = sy - sy0;
                    int a0 = (sy0 * _w + sx0) * 4, a1 = (sy0 * _w + sx1) * 4, b0 = ((sy0 + 1) * _w + sx0) * 4, b1 = ((sy0 + 1) * _w + sx1) * 4;
                    float w00 = (1 - tx) * (1 - ty), w10 = tx * (1 - ty), w01 = (1 - tx) * ty, w11 = tx * ty;
                    float r = face[a0] * w00 + face[a1] * w10 + face[b0] * w01 + face[b1] * w11;
                    float g = face[a0 + 1] * w00 + face[a1 + 1] * w10 + face[b0 + 1] * w01 + face[b1 + 1] * w11;
                    float b = face[a0 + 2] * w00 + face[a1 + 2] * w10 + face[b0 + 2] * w01 + face[b1 + 2] * w11;
                    float aa = Min1(face[a0 + 3]) * w00 + Min1(face[a1 + 3]) * w10 + Min1(face[b0 + 3]) * w01 + Min1(face[b1 + 3]) * w11;
                    if (aa <= 0.001f) { _out[o] = default; continue; }
                    if (aa > 1f) aa = 1f;
                    // premultiplied -> straight, clamped: added light past white stays white
                    float inv = 255f / aa;
                    _out[o] = new Color32(To8(r * inv), To8(g * inv), To8(b * inv), (byte)(aa * 255f));
                }
            }
        }

        static float Min1(float v) => v < 1f ? v : 1f;
        static byte To8(float v) => v >= 255f ? (byte)255 : v <= 0f ? (byte)0 : (byte)v;

        // ------------------------------------------------------------------ helpers

        static Vector3 Rgb(string hex, Vector3 fallback)
        {
            if (string.IsNullOrEmpty(hex) || !ColorUtility.TryParseHtmlString(hex, out var c)) return fallback;
            return new Vector3(c.r, c.g, c.b);
        }

        /// <summary>A stable 0..1 per piece, so cards on one wall do not turn in lockstep.</summary>
        static float Hash01(string key)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in key) h = (h ^ ch) * 16777619;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
