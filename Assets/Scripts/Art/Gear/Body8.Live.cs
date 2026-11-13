using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // EIGHT DIRECTIONS, THE ACCESSORIES THAT MOVE WITH THE GAME (RIG.md "Scarves, accessories, arm pieces"): the
    // bandolier's phials (they empty as the phial is drunk, uncork, drain, crack and spray), the harness's flask (its
    // level in thirds, its slosh, the drink's bubbles), the hourglass (the run's sand) and the banner's cloth (its
    // ripple). The approved 3D mocks (Tools/tier-art/views-extras.js phial3d, harness3d, hourglass3d, banner3d) ported
    // number for number, with the state the flat rig's Accessories draws from; bake8x.js leaves these pieces out and
    // writes where they sit (Body8Art.LivePhials ..). Drawn as their surface, sampled on a half-texel grid in the body's
    // frame and lit per view - the arm pieces' way - into the figure's depth buffer. A picture is cached per state.
    public static partial class Body8
    {
        /// <summary>One bandolier phial's state (Accessories.BandolierFront's): the liquid's rows (0..5), the cork
        /// seated, lifted (flat rows: up is negative) or gone, the glass cracked, the walk's sway (texels).</summary>
        public struct Phial8 { public int Rows; public bool Cork, Cracked; public int CorkDy, CorkDx, Sway; }

        /// <summary>What the live accessory pieces are drawn from this picture (PrimitiveCharacterRig.Dir8Extras.cs).</summary>
        public sealed class Live
        {
            public AccessoryKind Kind;
            /// <summary>The liquid's tones (the wearer's element) and the banner's cloth.</summary>
            public Color[] Liq, Cloth;
            public Phial8[] Phials;
            /// <summary>The flask's level (0..1, its surface from the bottom to the top of the inner glass), its tilt
            /// (flat rows per texel across), the cork's lift (texels), cracked, and a drink's phase for its bubbles (-1 none).</summary>
            public float FlaskLevel, FlaskTilt, FlaskCork, FlaskDrink = -1f;
            public bool FlaskCracked;
            /// <summary>A spill's phase (-1 none) and which phial (the flask: -1): the draught sprayed from the mouth.</summary>
            public float Spill = -1f;
            public int SpillIdx = -1;
            /// <summary>The hourglass's sand run (0..1, the run's share of the hour) and how far back a pack puts it.</summary>
            public float Sand, Push;
            /// <summary>The banner's ripple clock (the mock's t).</summary>
            public float Ripple;
        }

        // ------------------------------------------------------------- the SDF kit (views.js)
        static float SdEllipsoid(Vector3 p, Vector3 c, Vector3 r)
        {
            var q = new Vector3((p.x - c.x) / r.x, (p.y - c.y) / r.y, (p.z - c.z) / r.z);
            float k0 = q.magnitude, k1 = new Vector3(q.x / r.x, q.y / r.y, q.z / r.z).magnitude;
            return k1 == 0f ? -Mathf.Min(r.x, Mathf.Min(r.y, r.z)) : k0 * (k0 - 1f) / k1;
        }

        static float SdRoundBox(Vector3 p, Vector3 c, Vector3 b, float rad)
        {
            var q = new Vector3(Mathf.Abs(p.x - c.x) - b.x + rad, Mathf.Abs(p.y - c.y) - b.y + rad, Mathf.Abs(p.z - c.z) - b.z + rad);
            return new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f) - rad;
        }

        /// <summary>An upright cylinder from y0 to y1, radius r, rounded at the rims.</summary>
        static float SdTube(Vector3 p, float cx, float cz, float y0, float y1, float r, float round)
        {
            float e = (Mathf.Sqrt((p.x - cx) * (p.x - cx) + (p.z - cz) * (p.z - cz)) / r - 1f) * r;
            float h = Mathf.Max(y0 - p.y, p.y - y1);
            float qx = Mathf.Max(e + round, 0f), qy = Mathf.Max(h + round, 0f);
            return Mathf.Sqrt(qx * qx + qy * qy) + Mathf.Min(Mathf.Max(e + round, h + round), 0f) - round;
        }

        static Color Tone(Color32[] r, int i) => r[Mathf.Clamp(i, 0, 5)];
        static Color Tone(Color[] r, int i) => r[Mathf.Clamp(i, 0, 5)];

        // ------------------------------------------------------------- drawing a surface
        /// <summary>The surface of <paramref name="sdf"/> inside the box (the body's frame), sampled every half texel,
        /// each point lit by its normal in the drawn frame (<paramref name="colour"/>: the body point, that normal) and
        /// laid into the depth buffer as one part. <paramref name="back"/> moves it back along the facing.</summary>
        /// The surface is found once per shape (<paramref name="shape"/> names it with its state) and kept: each pose only
        /// places and lights its points.
        static void Splat(string shape, System.Func<Vector3, float> sdf, System.Func<Vector3, Vector3, Color> colour, Vector3 lo, Vector3 hi,
                          System.Func<Vector3, Vector3> body, Vector3 left, Vector3 f, int outR, float bias, bool overHead, float back, List<Part> parts)
        {
            int id = parts.Count; parts.Add(new Part { OutR = outR, Bias = bias, OverHead = overHead });
            var (qs, ns) = SurfaceOf(shape, sdf, lo, hi);
            for (int i = 0; i < qs.Length; i++)
            {
                var q = qs[i]; var n = ns[i];
                var nv = (left * n.x + Vector3.up * n.y + f * n.z).normalized;
                var p = body(q - new Vector3(0f, 0f, back));
                var sc = Locomotion8Screen(p);
                int X = Mathf.FloorToInt(sc.x), Y = Mathf.FloorToInt(sc.y);
                if (X < 0 || Y < 0 || X >= _W || Y >= _H) continue;
                Put(X, Y, p.z, colour(q, nv), id);
            }
        }

        static Dictionary<string, (Vector3[] q, Vector3[] n)> _surfaces;
        const int SurfaceCache = 96;

        /// <summary>The points of a shape's surface (the body's frame) every half texel, with their normals: cached by name.</summary>
        static (Vector3[] q, Vector3[] n) SurfaceOf(string shape, System.Func<Vector3, float> sdf, Vector3 lo, Vector3 hi)
        {
            _surfaces ??= new Dictionary<string, (Vector3[], Vector3[])>();
            if (_surfaces.TryGetValue(shape, out var hit)) return hit;
            if (_surfaces.Count >= SurfaceCache) _surfaces.Clear();
            var qs = new List<Vector3>(); var ns = new List<Vector3>();
            const float S = 0.5f, Shell = 0.36f, E = 0.25f;
            for (float x = lo.x; x <= hi.x; x += S)
            for (float y = lo.y; y <= hi.y; y += S)
            for (float z = lo.z; z <= hi.z; z += S)
            {
                var q = new Vector3(x, y, z);
                float d = sdf(q);
                if (d > Shell || d < -Shell) continue;
                var n = new Vector3(sdf(q + new Vector3(E, 0, 0)) - sdf(q - new Vector3(E, 0, 0)),
                                    sdf(q + new Vector3(0, E, 0)) - sdf(q - new Vector3(0, E, 0)),
                                    sdf(q + new Vector3(0, 0, E)) - sdf(q - new Vector3(0, 0, E)));
                if (n.sqrMagnitude < 1e-8f) continue;
                n.Normalize();
                qs.Add(q - n * d); ns.Add(n);   // onto the surface
            }
            var o = (qs.ToArray(), ns.ToArray());
            _surfaces[shape] = o;
            return o;
        }

        static int Lit(Vector3 nv) => TexelKit.LitTone(nv.x, -nv.y, nv.z, 0f);

        /// <summary>A dot of liquid (a spilled drop) at a body point, its own part.</summary>
        static void Drop(Vector3 q, Color c, System.Func<Vector3, Vector3> body, List<Part> parts)
        {
            var p = body(q); var sc = Locomotion8Screen(p);
            int X = Mathf.FloorToInt(sc.x), Y = Mathf.FloorToInt(sc.y);
            if (X < 0 || Y < 0 || X >= _W || Y >= _H) return;
            int id = parts.Count; parts.Add(new Part { OutR = 0, Bias = 20f });
            Put(X, Y, p.z + 20f, c, id);
        }

        /// <summary>The draught flung out of a mouth (Accessories.SprayDrops): seven drops thrown up and out, falling.</summary>
        static void Spray(Vector3 mouth, float p, float seed, Color[] liq, System.Func<Vector3, Vector3> body, List<Part> parts)
        {
            for (int i = 0; i < 7; i++)
            {
                float q = (p - 0.2f - i * 0.02f) / 0.6f;
                if (q <= 0f || q >= 1f) continue;
                float vx = Mathf.Sin(seed + i * 2.4f) * 14f, vy = -10f - (i % 3) * 4f;
                var at = mouth + new Vector3(vx * q, -(vy * q + 46f * q * q), 2f + 4f * q);
                Drop(at, liq[4], body, parts);
                if (i % 2 == 1) Drop(at - new Vector3(0f, 1f, 0f), liq[2], body, parts);
            }
        }

        static void DrawLive(Live lv, System.Func<Vector3, Vector3> body, Vector3 left, Vector3 f, List<Part> parts)
        {
            switch (lv.Kind)
            {
                case AccessoryKind.Bandolier:
                    for (int i = 0; i < lv.Phials.Length && i < Body8Art.LivePhials.Length; i++) DrawPhial(lv, i, body, left, f, parts);
                    break;
                case AccessoryKind.VialHarness: DrawFlask(lv, body, left, f, parts); break;
                case AccessoryKind.HourglassPack: DrawHourglass(lv, body, left, f, parts); break;
                case AccessoryKind.BannerPole: DrawBanner(lv, body, left, f, parts); break;
            }
        }

        // ------------------------------------------------------------- 1. the bandolier's phials (phial3d)
        static void DrawPhial(Live lv, int i, System.Func<Vector3, Vector3> body, Vector3 left, Vector3 f, List<Part> parts)
        {
            var st = lv.Phials[i];
            var a = Body8Art.LivePhials[i];
            float x = a.x + st.Sway, yb = a.y, z = a.z;
            float cy = -st.CorkDy, cx = x + st.CorkDx;   // the cork's lift (flat rows down -> up)
            float top = yb + st.Rows;                    // the liquid's surface
            float Glass(Vector3 p) => SdTube(p, x, z, yb, yb + 7f, 2.05f, 0.6f);
            float Neck(Vector3 p) => SdTube(p, x, z, yb + 7f, yb + 8.2f, 1.1f, 0.6f);
            float Cork(Vector3 p) => st.Cork ? SdTube(p, cx, z, yb + 8f + cy, yb + 10f + cy, 1.1f, 0.5f) : 1e9f;
            var liq = lv.Liq;
            // the crack (Accessories.Phial's four dots) on the glass's front
            Vector2[] crack = { new(x - 0.5f, yb + 5f), new(x + 0.5f, yb + 4f), new(x - 0.5f, yb + 3f), new(x + 0.5f, yb + 2f) };
            Splat($"phial{i}.{st.Sway}.{(st.Cork ? 1 : 0)}.{st.CorkDy}.{st.CorkDx}", p => Mathf.Min(Glass(p), Mathf.Min(Neck(p), Cork(p))), (p, n) =>
            {
                int l = Lit(n);
                if (Cork(p) < Mathf.Min(Glass(p), Neck(p))) return Tone(Body8Art.RampCork, p.y > yb + 9.3f + cy ? 4 : l >= 4 ? 3 : 2);
                if (st.Cracked && n.z > 0.2f)
                    foreach (var c in crack) if (Mathf.Abs(p.x - c.x) < 0.55f && Mathf.Abs(p.y - c.y) < 0.55f) return Outline;
                if (p.y < top && p.y > yb && st.Rows > 0) return Tone(liq, p.y > top - 1f ? 5 : l >= 4 ? 4 : l <= 2 ? 2 : 3);
                return Tone(Body8Art.RampGlass, l >= 5 ? 5 : l >= 4 ? 4 : l <= 2 ? 1 : 2);
            }, new Vector3(Mathf.Min(x, cx) - 3f, yb - 1f, z - 3f), new Vector3(Mathf.Max(x, cx) + 3f, yb + 11f + Mathf.Max(0f, cy), z + 3f),
               body, left, f, 1, 0f, true, 0f, parts);
            if (lv.Spill >= 0f && lv.SpillIdx == i) Spray(new Vector3(x, yb + 8.2f, z), lv.Spill, i, liq, body, parts);
        }

        // ------------------------------------------------------------- 4. the harness's flask (harness3d)
        static void DrawFlask(Live lv, System.Func<Vector3, Vector3> body, Vector3 left, Vector3 f, List<Part> parts)
        {
            var a = Body8Art.LiveFlask;
            float cx = a.x, cy = a.y, zf = a.z, R = a.w, lift = lv.FlaskCork;
            var c = new Vector3(cx, cy, zf);
            float Bulb(Vector3 p) => SdEllipsoid(p, c, new Vector3(R, R, R));
            float Neck(Vector3 p) => SdTube(p, cx, zf, cy + R - 1f, cy + R + 2.5f, 1.05f, 0.4f);
            float Collar(Vector3 p) => SdTube(p, cx, zf, cy + R + 0.6f, cy + R + 1.6f, 2.05f, 0.4f);
            float Cork(Vector3 p) => SdTube(p, cx, zf, cy + R + 2.5f + lift, cy + R + 4.5f + lift, 1.05f, 0.5f);
            // the surface (Accessories.HarnessFront): from the inner glass's bottom (level 0) to its top (1), tilted
            float inner = R - 1f, h0 = cy - inner + lv.FlaskLevel * 2f * inner;
            float Surface(float x) => h0 - (x - cx) * lv.FlaskTilt;
            var liq = lv.Liq;
            Vector2[] crack = { new(0.5f, 89.5f), new(1.5f, 88.5f), new(0.5f, 87.5f), new(1.5f, 86.5f) };
            Splat($"flask.{lift:0.0}", p => Mathf.Min(Mathf.Min(Bulb(p), Neck(p)), Mathf.Min(Collar(p), Cork(p))), (p, n) =>
            {
                int l = Lit(n);
                if (Cork(p) < Mathf.Min(Mathf.Min(Bulb(p), Neck(p)), Collar(p))) return Tone(Body8Art.RampCork, p.y > cy + R + 3.6f + lift ? 4 : 3);
                if (Collar(p) < 0.15f) return Tone(Body8Art.RampBronze, l >= 4 ? 5 : 2);
                if (p.y > cy + R - 1f) return Tone(Body8Art.RampGlass, l >= 4 ? 4 : 1);
                if (Mathf.Abs(p.y - cy) < 0.55f) return Tone(Body8Art.RampBronze, l >= 3 ? 5 : 2);   // the cage's band
                if (lv.FlaskCracked && n.z > 0.2f)
                    foreach (var k in crack) if (Mathf.Abs(p.x - k.x) < 0.55f && Mathf.Abs(p.y - k.y) < 0.55f) return Outline;
                bool rim = Mathf.Abs(n.z) < 0.45f && Mathf.Sqrt(n.x * n.x + n.y * n.y) > 0.9f;
                if (rim || p.y > cy + R - 1.6f) return Tone(Body8Art.RampGlass, l >= 4 ? 4 : l <= 2 ? 1 : 2);
                if (l >= 5 && p.y > cy + 1f) return Tone(Body8Art.RampGlass, 5);
                float s = Surface(p.x);
                if (lv.FlaskLevel <= 0.01f || p.y > s) return Tone(Body8Art.RampGlass, l >= 4 ? 4 : l <= 2 ? 1 : 2);
                // bubbles rising through what is left (a drink)
                if (lv.FlaskDrink >= 0f && n.z > 0.2f)
                    for (int i = 0; i < 4; i++)
                    {
                        float q = (lv.FlaskDrink * 3f + i * 0.27f) % 1f;
                        float bx = Mathf.Round(cx + 19f - 1f + (i * 1.7f) % 3f) - 18.5f, by = cy - inner + q * 7f;
                        if (Mathf.Abs(p.x - bx) < 0.55f && Mathf.Abs(p.y - by) < 0.55f && by <= s + 1f) return Tone(liq, 5);
                    }
                return Tone(liq, s - p.y < 1.2f ? 5 : l >= 4 ? 4 : l <= 2 ? 2 : 3);
            }, new Vector3(cx - 6f, cy - 6f, zf - 6f), new Vector3(cx + 6f, cy + R + 5f + lift, zf + 6f), body, left, f, 4, 0f, true, 0f, parts);
            if (lv.Spill >= 0f && lv.SpillIdx < 0) Spray(new Vector3(cx, cy + R + 2.5f, zf), lv.Spill, 3f, liq, body, parts);
        }

        // ------------------------------------------------------------- 8. the hourglass (hourglass3d)
        static void DrawHourglass(Live lv, System.Func<Vector3, Vector3> body, Vector3 left, Vector3 f, List<Part> parts)
        {
            float cz = Body8Art.LiveHourglassZ, fr = Mathf.Clamp01(lv.Sand);
            const float top = 32f, H = 34f;
            float Yr(float r) => 135f - r;
            float yT = Yr(top), yB = Yr(top + H);
            float Frame(Vector3 p)
            {
                float d = Mathf.Min(SdRoundBox(p, new Vector3(0f, yT - 1.5f, cz), new Vector3(10f, 1.5f, 7f), 0.5f),
                                    SdRoundBox(p, new Vector3(0f, yB + 1.5f, cz), new Vector3(10f, 1.5f, 7f), 0.5f));
                foreach (var (x, z) in new[] { (-8.5f, -5.5f), (8.5f, -5.5f), (-8.5f, 5.5f), (8.5f, 5.5f) })
                    d = Mathf.Min(d, SdTube(p, x, cz + z, yB + 2f, yT - 2f, 1f, 0.2f));
                return d;
            }
            var tB = new Vector3(0f, Yr(top + 9.5f), cz); var bB = new Vector3(0f, Yr(top + H - 10.5f), cz);
            float Bulbs(Vector3 p) => Mathf.Min(Mathf.Min(
                Mathf.Max(SdEllipsoid(p, tB, new Vector3(6f, 7f, 6f)), Mathf.Max(p.y - (yT - 3f), Yr(top + 15f) - p.y)),
                Mathf.Max(SdEllipsoid(p, bB, new Vector3(6f, 7f, 6f)), Mathf.Max(p.y - Yr(top + 17f), (yB + 3f) - p.y))),
                SdTube(p, 0f, cz, Yr(top + 18f), Yr(top + 14f), 1.05f, 0.2f));
            float sTop = Yr(top + 15f - (1f - fr) * 11f), pile = Yr(top + H - 3f - fr * 11f);
            Splat("hourglass", p => Mathf.Min(Frame(p), Bulbs(p)), (p, n) =>
            {
                int l = Lit(n);
                if (Frame(p) < Bulbs(p))
                {
                    if (p.y > yT - 3.2f || p.y < yB + 3.2f) return Tone(Body8Art.RampBronze, n.y > 0.7f ? 5 : l >= 4 ? 4 : l <= 2 ? 2 : 3);
                    return Tone(Body8Art.RampBronzeDk, l >= 4 ? 4 : 2);
                }
                float dx = Mathf.Abs(p.x);
                if (p.y > Yr(top + 16f) && p.y < sTop - Mathf.Max(0f, 2.2f - dx) * 0.9f && dx < 5.5f) return Tone(Body8Art.RampSand, l >= 4 ? 4 : 3);
                if (p.y < Yr(top + 16f) && p.y < pile - dx * 0.55f && dx < 5.5f) return Tone(Body8Art.RampSand, p.y > pile - dx * 0.55f - 1.2f ? 4 : 3);
                if (p.y < Yr(top + 16f) && p.y > pile && dx < 1.2f && Mathf.RoundToInt(p.y) % 2 != 0 && fr < 1f) return Tone(Body8Art.RampSand, 5);   // the stream
                return Tone(Body8Art.RampGlass, l >= 5 ? 5 : l >= 4 ? 4 : l <= 2 ? 1 : 2);
            }, new Vector3(-11f, yB - 1f, cz - 8f), new Vector3(11f, yT + 1f, cz + 8f), body, left, f, 4, 12f, true, lv.Push, parts);
        }

        // ------------------------------------------------------------- 3. the banner's cloth (banner3d)
        static void DrawBanner(Live lv, System.Func<Vector3, Vector3> body, Vector3 left, Vector3 f, List<Part> parts)
        {
            const float PX = 14f, PZ = -8.4f, H = 27f;
            float x0 = PX + 2f, top = 135f - (-37f), t = lv.Ripple;
            int Ripple(float y) => Mathf.RoundToInt(Mathf.Sin(t * 3.4f - (top - y) * 0.32f) * 1.0f);
            var cloth = lv.Cloth;
            Splat($"banner.{Mathf.Repeat(t * 3.4f, Mathf.PI * 2f):0.00}", p =>
            {
                float i = p.x - x0, dyy = top - p.y, ext = 12f + (dyy > 3f ? Ripple(p.y) : 0), notch = dyy > H - 5f ? dyy - (H - 5f) : -1f;
                float d = Mathf.Max(Mathf.Max(-i, i - ext), Mathf.Max(Mathf.Max(-dyy, dyy - H), Mathf.Abs(p.z - PZ) - 0.6f));
                if (notch >= 0f) d = Mathf.Max(d, notch - Mathf.Abs(i - 6f));
                return d;
            }, (p, n) =>
            {
                int i = Mathf.FloorToInt(p.x - x0);
                float y = top - p.y, d = Mathf.Sqrt((p.x - x0 - 6f) * (p.x - x0 - 6f) + (y - 11f) * (y - 11f));
                if (d > 3.2f && d < 4.4f) return Tone(Body8Art.RampGold, 4);
                if (d < 1.2f) return Tone(Body8Art.RampGold, 5);
                if (Mathf.Abs(y - (H - 9f) - 0.5f) < 0.5f) return Tone(Body8Art.RampGold, 3);
                float sl = Mathf.Sin(t * 3.4f - y * 0.32f + 1.2f);
                return Tone(cloth, i == 0 ? 2 : i == 1 ? 4 : sl > 0.4f ? 4 : sl < -0.4f ? 2 : 3);
            }, new Vector3(x0 - 1f, top - H - 1f, PZ - 1.5f), new Vector3(x0 + 14f, top + 1f, PZ + 1.5f), body, left, f, 4, 0f, true, 0f, parts);
        }
    }
}
