using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// THE TIER MOCK'S TEXEL KIT IN C# (Tools/tier-art/kit.js), for pieces drawn LIVE rather than baked:
    /// the accessories (<see cref="Accessories"/>), whose phials empty, sand runs and cloth ripples.
    /// Coordinates are the mock's: body texels at 150 per unit, x right, y DOWN, the bare body's capture
    /// (rig root at 19, 56). Shapes are sets of texels; a layer is a colour per texel with a depth and,
    /// unless switched off, the gear outline (4 texels, as the game strokes every gear sprite) drawn
    /// round it when layers are composed. The JS rounds half up (Math.round), so <see cref="R"/> does too.
    /// </summary>
    public static class TexelKit
    {
        public const int OutR = 4;
        public static readonly Color32 Outline = new(0x14, 0x12, 0x1C, 255);
        public const float FarShade = 0.85f;

        public static int K(int x, int y) => ((y + 1024) << 11) | (x + 1024);
        public static int KX(int k) => (k & 2047) - 1024;
        public static int KY(int k) => (k >> 11) - 1024;

        /// <summary>Math.round: half up, never to even.</summary>
        public static int R(float v) => Mathf.FloorToInt(v + 0.5f);

        // ---------------------------------------------------------------- colours

        public static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        /// <summary>A tone ramp - Palette.Ramp's six (line, deep, dark, base, light, glow) as the
        /// mock's ramp() builds them: highlights toward <paramref name="sheen"/> when one is given.</summary>
        public sealed class Tones
        {
            public readonly Color32[] T;
            public Tones(Color32[] t) { T = t; }
            public Color32 this[int i] => T[Mathf.Clamp(i, 0, 5)];
        }

        public static Tones Ramp(string baseHex, string sheen = null, float lift = 0.34f, float shade = 0.30f)
            => Ramp((Color)Hex(baseHex), sheen, lift, shade);

        public static Tones Ramp(Color b, string sheen = null, float lift = 0.34f, float shade = 0.30f)
        {
            var r = new Palette.Ramp(b, lift, shade);
            if (sheen != null) r = r.WithHighlightsToward(Hex(sheen), lift);
            return new Tones(new Color32[] { r.Line, r.Deep, r.Dark, r.Base, r.Light, r.Glow });
        }

        public static Color32 Scale(Color32 c, float f) => new(
            (byte)Mathf.Clamp(R(c.r * f), 0, 255), (byte)Mathf.Clamp(R(c.g * f), 0, 255),
            (byte)Mathf.Clamp(R(c.b * f), 0, 255), c.a);

        // ---------------------------------------------------------------- masks

        public sealed class Mask
        {
            public readonly HashSet<int> S = new();
            public Mask Add(int x, int y) { S.Add(K(x, y)); return this; }
            public bool Has(int x, int y) => S.Contains(K(x, y));
            public void Del(int x, int y) => S.Remove(K(x, y));
            public int Count => S.Count;
            public IEnumerable<(int x, int y)> Pts() { foreach (int k in S) yield return (KX(k), KY(k)); }
            public Mask Union(Mask m) { S.UnionWith(m.S); return this; }
            public Mask Minus(Mask m) { S.ExceptWith(m.S); return this; }
            public Mask Clip(Mask m) { S.IntersectWith(m.S); return this; }
            public (int x0, int y0, int x1, int y1, int w, int h) BBox()
            {
                int a = int.MaxValue, b = int.MaxValue, c = int.MinValue, d = int.MinValue;
                foreach (int k in S) { int x = KX(k), y = KY(k); a = Mathf.Min(a, x); b = Mathf.Min(b, y); c = Mathf.Max(c, x); d = Mathf.Max(d, y); }
                return (a, b, c, d, c - a + 1, d - b + 1);
            }
        }

        public static Mask Rect(float x0, float y0, float x1, float y1)
        {
            var m = new Mask();
            for (int y = R(y0); y <= R(y1); y++) for (int x = R(x0); x <= R(x1); x++) m.Add(x, y);
            return m;
        }

        /// <summary>Texel centres inside the polygon (even-odd).</summary>
        public static Mask Poly(params Vector2[] p)
        {
            var m = new Mask();
            float y0 = float.MaxValue, y1 = float.MinValue, x0 = float.MaxValue, x1 = float.MinValue;
            foreach (var v in p) { y0 = Mathf.Min(y0, v.y); y1 = Mathf.Max(y1, v.y); x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); }
            for (int y = Mathf.FloorToInt(y0); y <= Mathf.CeilToInt(y1); y++)
                for (int x = Mathf.FloorToInt(x0); x <= Mathf.CeilToInt(x1); x++)
                {
                    float px = x + 0.5f, py = y + 0.5f; bool inside = false;
                    for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                        if ((p[i].y > py) != (p[j].y > py) && px < (p[j].x - p[i].x) * (py - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                            inside = !inside;
                    if (inside) m.Add(x, y);
                }
            return m;
        }

        public static Mask Ell(float cx, float cy, float rx, float ry)
        {
            var m = new Mask();
            for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) m.Add(x, y);
                }
            return m;
        }

        /// <summary>An ellipse rotated by <paramref name="deg"/> (0 = long axis vertical; positive leans
        /// the top to the right).</summary>
        public static Mask Rell(float cx, float cy, float rx, float ry, float deg)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a), Rr = Mathf.Max(rx, ry) + 1f;
            var m = new Mask();
            for (int y = Mathf.FloorToInt(cy - Rr); y <= Mathf.CeilToInt(cy + Rr); y++)
                for (int x = Mathf.FloorToInt(cx - Rr); x <= Mathf.CeilToInt(cx + Rr); x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float u = dx * c + dy * sn, v = -dx * sn + dy * c;
                    if ((u / rx) * (u / rx) + (v / ry) * (v / ry) <= 1f) m.Add(x, y);
                }
            return m;
        }

        /// <summary>The texels within <paramref name="d"/> of the mask's edge.</summary>
        public static Mask Band(Mask m, int d)
        {
            var o = new Mask();
            foreach (var (x, y) in m.Pts())
            {
                bool edge = false;
                for (int dy = -d; dy <= d && !edge; dy++)
                    for (int dx = -d; dx <= d; dx++)
                        if (!m.Has(x + dx, y + dy)) { edge = true; break; }
                if (edge) o.Add(x, y);
            }
            return o;
        }

        public static Mask U(params Mask[] ms)
        {
            var m = new Mask();
            foreach (var a in ms) m.Union(a);
            return m;
        }

        // ---------------------------------------------------------------- layers

        public delegate Color32 Shader(int x, int y, Mask m);

        public sealed class Layer
        {
            public float Depth;
            public readonly Dictionary<int, Color32> C = new();
            public bool Outlined = true;
            public bool Far;
            public int OutR = TexelKit.OutR;
            /// <summary>What the layer rides (ArmPieces: arm, shoulder or torso) and whether it stands above
            /// the shoulder (turned away, it draws over the cape). The accessories leave both alone.</summary>
            public int Joint;
            public bool Rise;

            public Layer(float depth) { Depth = depth; }

            public void Set(int x, int y, Color32 c) => C[K(x, y)] = c;
            public bool Has(int x, int y) => C.ContainsKey(K(x, y));

            public Layer Fill(Mask m, Shader s) { foreach (var (x, y) in m.Pts()) Set(x, y, s(x, y, m)); return this; }
            public Layer Fill(Mask m, Color32 c) { foreach (var (x, y) in m.Pts()) Set(x, y, c); return this; }

            /// <summary>Paint only over texels this layer already has.</summary>
            public Layer Over(Mask m, Shader s) { foreach (var (x, y) in m.Pts()) if (Has(x, y)) Set(x, y, s(x, y, m)); return this; }
            public Layer Over(Mask m, Color32 c) { foreach (var (x, y) in m.Pts()) if (Has(x, y)) Set(x, y, c); return this; }

            public Layer Dot(int x, int y, Color32 c, bool onlyOver = true)
            {
                if (!onlyOver || Has(x, y)) Set(x, y, c);
                return this;
            }

            /// <summary>A Bresenham line, rounded at its ends - the mock's line(); by default only over
            /// texels the layer already has.</summary>
            public Layer Line(float x0f, float y0f, float x1f, float y1f, Color32 c, bool onlyOver = true)
            {
                int x0 = R(x0f), y0 = R(y0f), x1 = R(x1f), y1 = R(y1f);
                int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, e = dx + dy;
                for (int guard = 0; guard < 4096; guard++)
                {
                    if (!onlyOver || Has(x0, y0)) Set(x0, y0, c);
                    if (x0 == x1 && y0 == y1) break;
                    int e2 = 2 * e;
                    if (e2 >= dy) { e += dy; x0 += sx; }
                    if (e2 <= dx) { e += dx; y0 += sy; }
                }
                return this;
            }

            /// <summary>The texels this layer has, as a mask.</summary>
            public Mask ToMask() { var m = new Mask(); foreach (int k in C.Keys) m.S.Add(k); return m; }
        }

        // ---------------------------------------------------------------- shaders (light from the upper left)

        static readonly Vector3 Light = new Vector3(-0.55f, -0.65f, 0.55f).normalized;

        public static int LitTone(float nx, float ny, float nz, float bias)
        {
            float d = nx * Light.x + ny * Light.y + nz * Light.z + bias;
            return d > 0.86f ? 5 : d > 0.62f ? 4 : d > 0.30f ? 3 : d > 0.02f ? 2 : 1;
        }

        /// <summary>A sphere-ish dome over the ellipse (cx, cy, rx, ry).</summary>
        public static Shader Dome(Tones r, float cx, float cy, float rx, float ry, float bias = 0f) => (x, y, _) =>
        {
            float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry, d = nx * nx + ny * ny;
            if (d > 1f) { float s = 1f / Mathf.Sqrt(d); nx *= s; ny *= s; d = 1f; }
            return r[LitTone(nx, ny, Mathf.Sqrt(1f - d), bias)];
        };

        /// <summary>A flat plate with a bevelled rim of <paramref name="w"/>: lit on top and left,
        /// dark on bottom and right.</summary>
        public static Shader Bevel(Tones r, int w = 1, int face = 3) => (x, y, m) =>
        {
            bool up = false, lf = false, rt = false, dn = false;
            for (int i = 1; i <= w; i++)
            {
                up |= !m.Has(x, y - i); lf |= !m.Has(x - i, y);
                rt |= !m.Has(x + i, y); dn |= !m.Has(x, y + i);
            }
            if (up && lf) return r[face + 2];
            if (up || lf) return r[face + 1];
            if (dn || rt) return r[face - 1];
            return r[face];
        };

        // ---------------------------------------------------------------- composition

        /// <summary>Layers flattened in depth order (stable), each outlined first unless it opts out,
        /// a far layer shaded - the mock's compose().</summary>
        public static Dictionary<int, Color32> Compose(List<Layer> layers)
        {
            var grid = new Dictionary<int, Color32>();
            var all = new List<Layer>(layers);
            // stable: List.Sort is not, so the index breaks ties
            var idx = new Dictionary<Layer, int>();
            for (int i = 0; i < all.Count; i++) idx[all[i]] = i;
            all.Sort((a, b) => a.Depth != b.Depth ? a.Depth.CompareTo(b.Depth) : idx[a].CompareTo(idx[b]));

            foreach (var l in all)
            {
                if (l.C.Count == 0) continue;
                if (l.Outlined)
                {
                    int rr = l.OutR, lim = rr * rr + 2;
                    foreach (int k in l.C.Keys)
                    {
                        int x = KX(k), y = KY(k);
                        for (int dy = -rr; dy <= rr; dy++)
                            for (int dx = -rr; dx <= rr; dx++)
                            {
                                if (dx * dx + dy * dy > lim) continue;
                                int kk = K(x + dx, y + dy);
                                if (!l.C.ContainsKey(kk)) grid[kk] = Outline;
                            }
                    }
                }
                foreach (var kv in l.C) grid[kv.Key] = l.Far ? Scale(kv.Value, FarShade) : kv.Value;
            }
            return grid;
        }
    }
}
