using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Hub
{
    /// <summary>
    /// The mock sheets' drawing tools in C#, so a hub piece is carried over from its mock line for line
    /// (the group 8 sheets). Coordinates are the MOCK's: x right, y DOWN, (0, 0) the fixture's FOOT - the
    /// point its sprite is pivoted on. It grows to fit whatever is drawn, so a piece never has to say
    /// how big it is. Box3 / Cyl3 / Ring / Disc / Line behave exactly as the mock's box3 / cyl3 / ring /
    /// ell / line did.
    /// </summary>
    public sealed class HubSketch
    {
        readonly Dictionary<long, Color32> _px = new();
        int _minX = int.MaxValue, _minY = int.MaxValue, _maxX = int.MinValue, _maxY = int.MinValue;
        public static readonly Color32 K = TexelCanvas.K;
        /// <summary>The mock's animation clock for this frame, in seconds.</summary>
        public float T;

        static long Key(int x, int y) => ((long)(x + 100000) << 32) | (uint)(y + 100000);

        public bool Has(float x, float y) => _px.ContainsKey(Key(Mathf.RoundToInt(x), Mathf.RoundToInt(y)));

        public void Set(float fx, float fy, Color32 c, float a = 1f)
        {
            int x = Mathf.RoundToInt(fx), y = Mathf.RoundToInt(fy);
            if (a <= 0f) return;
            long k = Key(x, y);
            if (_px.TryGetValue(k, out var o) && a < 1f) c = Color32.Lerp(o, new Color32(c.r, c.g, c.b, 255), a);
            else if (a < 1f && !_px.ContainsKey(k)) c.a = (byte)(255 * Mathf.Clamp01(a));
            _px[k] = c;
            if (x < _minX) _minX = x; if (x > _maxX) _maxX = x;
            if (y < _minY) _minY = y; if (y > _maxY) _maxY = y;
        }

        /// <summary>The highest drawn row (least y) in column <paramref name="x"/>, or 0 if none.</summary>
        public int TopOfColumn(int x)
        {
            int best = int.MaxValue;
            foreach (var k in _px.Keys) { int kx = (int)((k >> 32) - 100000), ky = (int)((k & 0xffffffff) - 100000); if (kx == x && ky < best) best = ky; }
            return best == int.MaxValue ? 0 : best;
        }

        public void Rect(float x0, float y0, float x1, float y1, Color32 c, float a = 1f)
        {
            for (int y = Mathf.RoundToInt(y0); y <= Mathf.RoundToInt(y1); y++)
                for (int x = Mathf.RoundToInt(x0); x <= Mathf.RoundToInt(x1); x++) Set(x, y, c, a);
        }

        public void Line(float x0, float y0, float x1, float y1, Func<int, int, float, Color32?> fn)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))));
            for (int i = 0; i <= n; i++)
            {
                int x = Mathf.RoundToInt(x0 + (x1 - x0) * i / n), y = Mathf.RoundToInt(y0 + (y1 - y0) * i / n);
                var c = fn(x, y, i / (float)n);
                if (c.HasValue) Set(x, y, c.Value);
            }
        }
        public void Line(float x0, float y0, float x1, float y1, Color32 c) => Line(x0, y0, x1, y1, (x, y, f) => c);

        /// <summary>A box at 3/4 (the mock's box3): x its left, y the bottom row of its front face; w wide,
        /// d deep on top, h tall. The near top edge catches the light.</summary>
        public void Box3(float x, float y, int w, int d, int h, Color32[] r,
                         Func<int, int, Color32?> front = null, Func<int, int, Color32?> top = null, Color32? lip = null, bool noLip = false)
        {
            int x0 = Mathf.RoundToInt(x), y0 = Mathf.RoundToInt(y);
            for (int yy = 0; yy < h; yy++)
                for (int xx = 0; xx < w; xx++)
                    Set(x0 + xx, y0 - yy, xx == 0 || xx == w - 1 || yy == 0 ? K : front?.Invoke(xx, yy) ?? (xx < 2 ? r[1] : r[2]));
            for (int yy = 0; yy < d; yy++)
                for (int xx = 0; xx < w; xx++)
                    Set(x0 + xx, y0 - h - yy, xx == 0 || xx == w - 1 || yy == d - 1 ? K : top?.Invoke(xx, yy) ?? r[1]);
            if (!noLip) for (int xx = 1; xx < w - 1; xx++) Set(x0 + xx, y0 - h, lip ?? r[0]);
        }

        /// <summary>A cylinder at 3/4 (the mock's cyl3): base centred cx, its front point on row y; rx wide,
        /// h tall. Returns the top ellipse's centre row.</summary>
        public float Cyl3(float cx, float y, int rx, int h, Color32[] r,
                          Func<int, int, float, Color32?> body = null, Func<int, int, Color32?> top = null)
        {
            float ry = Mathf.Max(1f, rx * 0.38f), cyB = y - ry, cyT = cyB - h;
            for (int x = -rx; x <= rx; x++)
            {
                float e = ry * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / (float)rx) * (x / (float)rx))), u = (x + rx) / (2f * rx);
                int lo = Mathf.RoundToInt(cyT + e), hi = Mathf.RoundToInt(cyB + e);
                for (int yy = lo; yy <= hi; yy++)
                    Set(cx + x, yy, Mathf.Abs(x) == rx || yy == hi ? K : body?.Invoke(x, yy, u) ?? Ramp(r, 0.15f + u * 0.6f));
            }
            for (int x = -rx; x <= rx; x++)
            {
                float e = ry * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / (float)rx) * (x / (float)rx)));
                int lo = Mathf.RoundToInt(cyT - e), hi = Mathf.RoundToInt(cyT + e);
                for (int yy = lo; yy <= hi; yy++)
                    Set(cx + x, yy, yy == lo || Mathf.Abs(x) == rx ? K : yy >= hi ? r[0] : top?.Invoke(x, yy) ?? r[1]);
            }
            return cyT;
        }

        /// <summary>Every texel whose (squashed) distance from (cx, cy) lies in [r0, r1], with its distance
        /// and angle - the mock's ring(), y squashed by <paramref name="sq"/>.</summary>
        public void Ring(float cx, float cy, float r0, float r1, float sq, Action<int, int, float, float> fn)
        {
            int e = Mathf.CeilToInt(r1) + 1;
            for (int y = -e; y <= e; y++)
                for (int x = -e; x <= e; x++)
                {
                    float d = Mathf.Sqrt(x * x + (y / sq) * (y / sq));
                    if (d >= r0 && d <= r1) fn(Mathf.RoundToInt(cx) + x, Mathf.RoundToInt(cy) + y, d, Mathf.Atan2(y / sq, x));
                }
        }

        /// <summary>A filled ellipse (the mock's ell / disc): fn gets each texel and its normalised distance.</summary>
        public void Disc(float cx, float cy, float rx, float ry, Action<int, int, float> fn)
        {
            int ex = Mathf.CeilToInt(rx), ey = Mathf.CeilToInt(ry);
            for (int y = -ey; y <= ey; y++)
                for (int x = -ex; x <= ex; x++)
                {
                    float d = Mathf.Sqrt((x / rx) * (x / rx) + (y / ry) * (y / ry));
                    if (d <= 1f) fn(Mathf.RoundToInt(cx + x), Mathf.RoundToInt(cy + y), d);
                }
        }

        public static Color32 Ramp(Color32[] r, float u) => r[Mathf.Clamp(Mathf.FloorToInt(u * r.Length), 0, r.Length - 1)];
        public static Color32 Mix(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, Mathf.Clamp01(t));
        public static float Hash(int a, int b = 0, int c = 0) => Combat.FireArt.Hash(a, b, c);
        public static float Noise(float x, float y) => Combat.FireArt.Noise(x, y);
        public static Color32 Fire(float u) => Combat.FireArt.At(Mathf.Clamp01(u));
        public static float C01(float v) => Mathf.Clamp01(v);

        /// <summary>A little fire rising from (x, y) - the mock's fireAt6.</summary>
        public void Flame(float x, float y, int w, int h, float t, int seed = 0)
        {
            for (int yy = 0; yy < h; yy++)
                for (int xx = -w; xx <= w; xx++)
                {
                    float v = Noise((xx + seed) * 0.6f, (yy - t * 14f) * 0.5f) - Mathf.Abs(xx) / (w + 1f) * 0.5f - yy / (float)h * 0.45f;
                    if (v > 0.2f) Set(x + xx, y - yy, Fire(0.9f - v * 1.6f));
                }
        }

        /// <summary>A crystal spire rising from (x, y), the mock's crystal().</summary>
        public void Crystal(float x, float y, int h, float w, Color32[] r)
        {
            for (int yy = 0; yy < h; yy++)
            {
                int ww = Mathf.RoundToInt(w * (1f - yy / (float)h));
                for (int xx = -ww; xx <= ww; xx++) Set(x + xx, y - yy, xx < 0 ? r[1] : xx == 0 ? r[0] : r[2]);
            }
        }

        /// <summary>The picture, pivoted on the foot (0, 0) - a pad of one texel round it.</summary>
        public Sprite ToSprite(string name, float ppu)
        {
            if (_px.Count == 0) { _minX = _maxX = _minY = _maxY = 0; }
            int x0 = Mathf.Min(_minX, 0) - 1, x1 = Mathf.Max(_maxX, 0) + 1, y0 = Mathf.Min(_minY, 0) - 1, y1 = Mathf.Max(_maxY, 0) + 1;
            int w = x1 - x0 + 1, h = y1 - y0 + 1;
            var px = new Color32[w * h];
            foreach (var kv in _px)
            {
                int x = (int)((kv.Key >> 32) - 100000), y = (int)((kv.Key & 0xffffffff) - 100000);
                px[(y1 - y) * w + (x - x0)] = kv.Value;   // y DOWN in the sketch, UP in the texture
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave, name = name,
            };
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2((-x0 + 0.5f) / w, (y1 + 0.5f) / h), ppu, 0, SpriteMeshType.FullRect);
            s.name = name;
            return s;
        }

        /// <summary>Draw once per key and cache (HubArt's cache).</summary>
        public static Sprite Cached(string key, float t, Action<HubSketch> draw)
            => HubArt.Get(key, () => { var s = new HubSketch { T = t }; draw(s); return s.ToSprite(key, HubArt.Ppu); });
    }

    /// <summary>Which theme each hub item is wearing, asked by the fixtures as they build. HubRoom sets
    /// it before building the room (a reload loses it, and everything reads the Cloister until the hub
    /// is next shown - which rebuilds the room anyway).</summary>
    public static class HubDress
    {
        public static Func<HubItem, HubTheme> Source;
        public static HubTheme Of(HubItem item) => Source?.Invoke(item) ?? HubTheme.Cloister;
    }
}
