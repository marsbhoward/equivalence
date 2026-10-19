using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>
    /// The bestiary's shared rasteriser (see ART-NOTES, "Enemies: the dark-fantasy bestiary"): a
    /// creature is a list of PARTS - ellipses, tapered capsules, polygons, or anything else that
    /// can answer "is this point inside me" - tested at every texel centre, last part in order
    /// wins, shaded by a light from the upper left, and outlined one texel round the whole
    /// silhouette. Each kind's *Art owns its rig, its poses and its colouring; this owns the
    /// geometry and the raster, which must not drift between kinds.
    ///
    /// Split out of ChaserArt when the Ranged became the second consumer (the project's rule:
    /// build the second one plain, generalise once it needs the identical shape). The mock-up
    /// pages run the same code in JavaScript, so a rig ports between the two line for line.
    ///
    /// Every sprite is drawn at <see cref="TexelsPerUnit"/> - the player's body density under the
    /// arena's 4/3 visual scale - and shown at scale 1, so an enemy's texel is the player's.
    /// </summary>
    public static class TexelRig
    {
        public const float TexelsPerUnit = 37.5f / (4f / 3f); // 28.125

        public struct Hit
        {
            public float Nx, Ny, T, S, E, K;
            public bool HasT;
        }

        public delegate bool Shape(float x, float y, out Hit hit);

        /// <summary>One shape in a rig. <see cref="Slot"/> and <see cref="Decor"/> are the kind's own
        /// enums cast to int; <see cref="Level"/> is a glow step; <see cref="Ref"/> lets a part
        /// colour itself as another (a cage showing the gland through its bars).</summary>
        public sealed class Part
        {
            public float Order;
            public int Index;
            public int Slot;
            public Shape Test;
            public bool Far, Sep;
            public int Decor;
            public int Level;
            public float Extra;
            public Part Ref;
        }

        public struct Cell
        {
            public Part Part;
            public Hit Hit;
            public float Px, Py;
        }

        /// <summary>Colours one texel. <paramref name="shade"/> is the light's 0-3 step with the
        /// far and separation rules already applied. Null leaves the texel transparent.</summary>
        public delegate Color? Colourer(in Cell c, int x, int y, int shade);

        /// <summary>A <see cref="Colourer"/> that can also look at the texels round it
        /// (<paramref name="at"/> gives the part on any texel, null where there is none).</summary>
        public delegate Color? NeighbourColourer(in Cell c, int x, int y, int shade, Func<int, int, Part> at);

        /// <summary>Paints what leaves the body - a smear, a halo, a muzzle burst - into empty
        /// texels after the body and its outline are down. <paramref name="filled"/> is true on
        /// the body and its outline.</summary>
        public delegate void Overlay(Color[] px, int w, int h, float scale, Func<int, int, bool> filled);

        public sealed class Rig
        {
            public readonly List<Part> Parts = new();

            public Part Add(float order, int slot, Shape test, bool far = false, bool sep = false,
                            int decor = 0, float level = 0, float extra = 0, Part refPart = null)
            {
                var p = new Part { Order = order, Index = Parts.Count, Slot = slot, Test = test, Far = far, Sep = sep,
                                   Decor = decor, Level = (int)level, Extra = extra, Ref = refPart };
                Parts.Add(p);
                return p;
            }
        }

        // ---------------------------------------------------------------- geometry

        public static float Rad(float d) => d * Mathf.Deg2Rad;
        public static Vector2 Pt(Vector2 p, float a, float l) => p + new Vector2(Mathf.Cos(Rad(a)), Mathf.Sin(Rad(a))) * l;

        /// <summary>A two-bone joint whose middle bends toward +x (a knee forward), or the other
        /// way with <paramref name="bend"/> -1 (a bird's knee, back).</summary>
        public static Vector2 Ik(Vector2 hip, Vector2 foot, float l1, float l2, float bend = 1f)
        {
            var d = foot - hip;
            float dist = Mathf.Min(d.magnitude, l1 + l2 - 0.01f);
            float a = Mathf.Atan2(d.y, d.x);
            float c = Mathf.Acos(Mathf.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2f * l1 * dist), -1f, 1f));
            return hip + new Vector2(Mathf.Cos(a + bend * c), Mathf.Sin(a + bend * c)) * l1;
        }

        public static Shape Ellipse(float cx, float cy, float rx, float ry) => (float x, float y, out Hit h) =>
        {
            float nx = (x - cx) / rx, ny = (y - cy) / ry;
            h = new Hit { Nx = nx, Ny = ny };
            return nx * nx + ny * ny <= 1f;
        };

        /// <summary>An ellipse turned <paramref name="rotDeg"/> degrees; its normal is reported in
        /// its own frame, so local -y is always the same side of it however it is turned.</summary>
        public static Shape Ellipse(float cx, float cy, float rx, float ry, float rotDeg)
        {
            float c = Mathf.Cos(Rad(-rotDeg)), s = Mathf.Sin(Rad(-rotDeg));
            return (float x, float y, out Hit h) =>
            {
                float dx = x - cx, dy = y - cy, lx = dx * c - dy * s, ly = dx * s + dy * c;
                float nx = lx / rx, ny = ly / ry;
                h = new Hit { Nx = nx, Ny = ny };
                return nx * nx + ny * ny <= 1f;
            };
        }

        public static Shape Taper(Vector2 a, Vector2 b, float ra, float rb) => (float x, float y, out Hit h) =>
        {
            float dx = b.x - a.x, dy = b.y - a.y, l2 = dx * dx + dy * dy;
            float t = l2 > 1e-4f ? Mathf.Clamp01(((x - a.x) * dx + (y - a.y) * dy) / l2) : 0f;
            float r = Mathf.Lerp(ra, rb, t);
            float vx = (x - (a.x + dx * t)) / r, vy = (y - (a.y + dy * t)) / r;
            h = new Hit { Nx = vx, Ny = vy, T = t, HasT = true };
            return vx * vx + vy * vy <= 1f;
        };

        /// <summary>A polygon, shaded flat (the Chaser's rags).</summary>
        public static Shape Poly(Vector2[] pts) => (float x, float y, out Hit h) =>
        {
            h = new Hit { Nx = 0f, Ny = 0.2f };
            return Inside(pts, x, y);
        };

        /// <summary>A polygon shaded as if it were a rounded mass centred on (cx, cy).</summary>
        public static Shape Poly(Vector2[] pts, float cx, float cy, float rx, float ry) => (float x, float y, out Hit h) =>
        {
            h = new Hit { Nx = (x - cx) / rx, Ny = (y - cy) / ry };
            return Inside(pts, x, y);
        };

        static bool Inside(Vector2[] pts, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                var pi = pts[i]; var pj = pts[j];
                if ((pi.y > y) != (pj.y > y) && x < (pj.x - pi.x) * (y - pi.y) / (pj.y - pi.y) + pi.x) inside = !inside;
            }
            return inside;
        }

        public static float Hash(int x, int y)
        {
            double v = Math.Sin(x * 127.1 + y * 311.7) * 43758.5453;
            return (float)(v - Math.Floor(v));
        }

        public static int BaseShade(float nx, float ny)
        {
            float l = -0.5f * nx + 0.8f * ny;
            return l > 0.5f ? 3 : l > 0f ? 2 : l > -0.55f ? 1 : 0;
        }

        public static Color[] Hex(params string[] hex)
        {
            var c = new Color[hex.Length];
            for (int i = 0; i < hex.Length; i++) ColorUtility.TryParseHtmlString(hex[i], out c[i]);
            return c;
        }

        // ---------------------------------------------------------------- raster

        /// <param name="w">Canvas width in texels - keep it EVEN, so the mirror lands on a texel boundary.</param>
        /// <param name="pivot">The body line in AUTHORED units; rounded to a whole texel.</param>
        public static Sprite Rasterise(string name, Rig rig, int w, int h, float scale, Color outline,
                                       Colourer colour, Vector2 pivot, Overlay overlay = null)
            => Rasterise(name, rig, w, h, scale, outline,
                         (in Cell c, int x, int y, int s, Func<int, int, Part> at) => colour(c, x, y, s), pivot, overlay);

        /// <param name="outlines">Which parts the one-texel outline goes round; null for all of
        /// them. Fire is drawn without one - a flame edged in black reads as a sticker.</param>
        public static Sprite Rasterise(string name, Rig rig, int w, int h, float scale, Color outline,
                                       NeighbourColourer colour, Vector2 pivot, Overlay overlay = null,
                                       Func<Part, bool> outlines = null)
        {
            var parts = rig.Parts;
            parts.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Index.CompareTo(b.Index));

            var cells = new Cell[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = (x + 0.5f) / scale, py = (y + 0.5f) / scale;
                foreach (var part in parts)
                    if (part.Test(px, py, out var hit))
                        cells[y * w + x] = new Cell { Part = part, Hit = hit, Px = px, Py = py };
            }

            Part At(int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? null : cells[y * w + x].Part;
            bool Near(int x, int y) => At(x + 1, y) != null || At(x - 1, y) != null || At(x, y + 1) != null || At(x, y - 1) != null;
            bool Solid(int x, int y) { var p = At(x, y); return p != null && (outlines == null || outlines(p)); }
            bool Edged(int x, int y) => Solid(x + 1, y) || Solid(x - 1, y) || Solid(x, y + 1) || Solid(x, y - 1);
            bool Below(int x, int y, Part p) { var n = At(x, y); return n != null && n.Order < p.Order - 0.5f; }

            var px32 = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = cells[y * w + x];
                if (c.Part == null) { px32[y * w + x] = Edged(x, y) ? outline : Color.clear; continue; }

                int s = BaseShade(c.Hit.Nx, c.Hit.Ny);
                if (c.Part.Far) s = Mathf.Max(0, s - 1);
                // A limb's border against what it covers reads as a separate shape, not a bulge.
                if (c.Part.Sep && (Below(x + 1, y, c.Part) || Below(x - 1, y, c.Part) || Below(x, y + 1, c.Part) || Below(x, y - 1, c.Part)))
                    s = 0;
                px32[y * w + x] = colour(c, x, y, s, At) ?? Color.clear;
            }

            overlay?.Invoke(px32, w, h, scale, (x, y) => At(x, y) != null || Edged(x, y));

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = name,
            };
            tex.SetPixels(px32);
            tex.Apply();

            var pivot01 = new Vector2(Mathf.Round(pivot.x * scale) / w, Mathf.Round(pivot.y * scale) / h);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), pivot01, TexelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        /// <summary>Blends <paramref name="c"/> at <paramref name="alpha"/> into an overlay texel.</summary>
        public static void Put(Color[] px, int w, int x, int y, Color c, float alpha)
        {
            c.a = alpha;
            px[y * w + x] = c;
        }
    }
}
