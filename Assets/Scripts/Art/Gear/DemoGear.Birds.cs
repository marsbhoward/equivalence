using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== the four birds' shared skeleton
    //
    // The FOUR BIRDS (the user's call, 2026-10-17): Crow (nigredo), Swan (albedo) and Peacock (cauda
    // pavonis) are Diamond bows that are to forge into the Phoenix (rubedo), Tria Prima's way. Each
    // is THE WHOLE BIRD AS THE BOW, diving at the target: the BODY is the riser (the hand holds its
    // back, by the tail), the head points down the arrow's line, and the two WINGS, swept back in a
    // stoop, are the limbs - each wing's LEADING EDGE bends forward at the wrist and back to the
    // tip, a recurve's line, the string nocked near the tips.
    //
    // This file is what they SHARE - the leading edges, the feathers laid off them, the body, the
    // tail, the string's nocks - so a fusion can sample the same field as its parts. What differs
    // is a BirdStyle: the head (and neck), the feathers' shape and every tone.
    //
    // One field over authored cells (x across the canvas, string at the left, the head to the
    // right; y down from the upper tip), sampled for the arena and (x4) the menu. Mocked up texel
    // for texel in Python first.
    public static partial class DemoGear
    {
        static int BirdRows => BowHeightRows + 3;

        /// <summary>The bird's back by the tail, where the hand holds it - every bird bow's pivot.</summary>
        static Vector2Int BirdGrip => new(13, 41);
        static Vector2Int BirdDetailGrip => new(BirdGrip.x * EmberDetailScale, BirdGrip.y * EmberDetailScale);

        // Lazy: static arrays in the partial files must not depend on another file's statics
        // being initialised first (Tria Prima's note).
        static Vector2[] _birdUpper, _birdLower;

        /// <summary>Each wing's LEADING EDGE: the shoulder, the wrist bent toward the target, the tip
        /// back at the string. Separate curves, never a mirror.</summary>
        static Vector2[] BirdUpper => _birdUpper ??= BirdCubic(
            new Vector2(19.5f, 38.5f), new Vector2(32f, 31f), new Vector2(26f, 10f), new Vector2(7.5f, 3.5f));
        static Vector2[] BirdLower => _birdLower ??= BirdCubic(
            new Vector2(19.5f, 43.5f), new Vector2(32f, 51f), new Vector2(26.5f, 72f), new Vector2(7.5f, 78.5f));

        /// <summary>How one bird is drawn on the shared skeleton.</summary>
        class BirdStyle
        {
            public int Canvas;
            public int Fingers, Coverts;
            /// <summary>Degrees each finger turns further toward the tip, and the feathers' jitter.</summary>
            public float FingerSplay, AngleJitter, LengthBase, LengthJitter, Half, FingerHalf;
            /// <summary>Bites out of some vanes and frayed ends (Fray on the quill).</summary>
            public bool Ragged;
            /// <summary>Feathers end ROUNDED (else they run to a point).</summary>
            public bool RoundTips;
            /// <summary>A dark line traced round the whole silhouette after sampling (a pale bird
            /// needs one; the dark Crow draws its own edges).</summary>
            public bool TraceOutline;
            /// <summary>Lengths subtracted from the fingers, the innermost first (0 = none).</summary>
            public float[] FingerShorten;
            /// <summary>The head (and neck): its char, or '.' when off it - checked FIRST.</summary>
            public Func<float, float, float, bool, char> Head;
            /// <summary>Body tone: (x, u along the body, dy from its centre line, half-width, fine).</summary>
            public Func<float, float, float, float, bool, char> Body;
            /// <summary>Tail tone inside its line: (v toward the fan's end, dy, half, fine).</summary>
            public Func<float, float, float, bool, char> Tail;
            /// <summary>Leading edge: (outer dark line, lit line, inside).</summary>
            public char EdgeLine, EdgeLit, EdgeInside;
            /// <summary>A feather's tone: (quill, b across it, tt along it, hw, bb rootward, e, fine).</summary>
            public Func<BirdQuill, float, float, float, float, float, bool, char> Feather;
            /// <summary>Drawn OVER every feather (the Peacock's eyes, which the next feather would
            /// otherwise cover): (style, x, y, fine) -> char, or '.'.</summary>
            public Func<BirdStyle, float, float, bool, char> OverFeathers;
            public char StringChar = '3';
            /// <summary>Replaces the plain string and the tail fan (the Phoenix's flaming tail, two
            /// feathers of which ARE the string): (style, x, y, e, fine) -> char, or '.'.</summary>
            public Func<BirdStyle, float, float, float, bool, char> StringAndTail;
            /// <summary>Glowing letters the trace neither outlines nor stops at (flame).</summary>
            public string Untraced = "";

            // Per-sample state.
            public BirdQuill[][] Wings;
            public Vector2 NockTop, NockBottom;
        }

        struct BirdQuill
        {
            public int Index;
            public Vector2 Root, Dir;
            public float Length, Half, Fray;
            public bool Covert, Notch;
        }

        /// <summary>The whole bow at <paramref name="k"/> texels per authored cell.</summary>
        static string[] BirdSample(BirdStyle st, int k)
        {
            st.Wings = new[] { BirdWing(st, BirdUpper, -1f, 1), BirdWing(st, BirdLower, 1f, 7) };
            BirdEdgeAt(BirdUpper, 0.955f, 1f, out var top, out _, out _);
            BirdEdgeAt(BirdLower, 0.955f, 1f, out var bottom, out _, out _);
            float sx = Mathf.Round(top.x) + 0.5f;
            st.NockTop = new Vector2(sx, top.y);
            st.NockBottom = new Vector2(sx, bottom.y);

            var rows = new string[BirdRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[st.Canvas * k];
                float y = (Y + 0.5f) / k;
                for (int X = 0; X < line.Length; X++)
                    line[X] = BirdTexel(st, (X + 0.5f) / k, y, k > 1);
                rows[Y] = new string(line);
            }
            if (st.TraceOutline)
            {
                rows = BirdTrace(rows, st.StringChar, st.Untraced);
                if (k > 1) rows = BirdTrace(rows, st.StringChar, st.Untraced);   // two texels on the menu art, as the edges
            }
            return rows;
        }

        /// <summary>Every texel of the bird touching the outside (or the string) becomes the line.</summary>
        static string[] BirdTrace(string[] rows, char stringChar, string untraced)
        {
            int h = rows.Length, w = rows[0].Length;
            var outRows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var line = rows[y].ToCharArray();
                for (int x = 0; x < w; x++)
                {
                    char c = rows[y][x];
                    if (c == '.' || c == stringChar || c == 'K' || untraced.IndexOf(c) >= 0) continue;
                    bool open(int xx, int yy) => xx < 0 || yy < 0 || xx >= w || yy >= h
                                                 || rows[yy][xx] == '.' || rows[yy][xx] == stringChar
                                                 || untraced.IndexOf(rows[yy][xx]) >= 0;
                    if (open(x + 1, y) || open(x - 1, y) || open(x, y + 1) || open(x, y - 1)) line[x] = 'k';
                }
                outRows[y] = new string(line);
            }
            return outRows;
        }

        /// <summary>A stable hash in 0..1 - a feather's own jitter.</summary>
        static float BirdHash(int i, int seed)
        {
            unchecked
            {
                uint h = (uint)i * 374761393u + (uint)seed * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>
        /// One wing's feathers off its leading edge: the secondaries trailing back toward the string,
        /// the primaries lying along toward the tip, the fingers splayed; then the coverts over their
        /// roots.
        /// </summary>
        static BirdQuill[] BirdWing(BirdStyle st, Vector2[] pts, float sign, int seed)
        {
            const int flight = 15;
            var wing = new List<BirdQuill>();
            for (int k = 0; k < flight; k++)
            {
                float s = 0.05f + 0.93f * k / (flight - 1);
                BirdEdgeAt(pts, s, sign, out var root, out var tan, out var n);
                int finger = Mathf.Max(0, k - (flight - 1 - st.Fingers));
                float phi = (84f - 66f * Mathf.Pow(s, 1.15f) - st.FingerSplay * finger
                             + (BirdHash(k, seed) - 0.5f) * st.AngleJitter) * Mathf.Deg2Rad;
                float length = st.LengthBase + 4.5f * Mathf.Pow(s, 1.4f) + (BirdHash(k + 40, seed) - 0.5f) * st.LengthJitter;
                if (finger > 0) length += 1.6f - st.FingerShorten[Mathf.Min(finger, st.FingerShorten.Length - 1)];
                wing.Add(new BirdQuill
                {
                    Index = k,
                    Root = root + n * 0.5f,
                    Dir = tan * Mathf.Cos(phi) + n * Mathf.Sin(phi),
                    Length = length,
                    Half = finger == 0 ? st.Half : st.FingerHalf,
                    Notch = st.Ragged && BirdHash(k + 80, seed) < 0.22f && finger == 0,
                    Fray = st.Ragged ? BirdHash(k + 120, seed) : 0f,
                });
            }
            for (int k = 0; k < st.Coverts; k++)
            {
                float s = 0.03f + 0.7f * k / st.Coverts;
                BirdEdgeAt(pts, s, sign, out var root, out var tan, out var n);
                float phi = (40f - 14f * s) * Mathf.Deg2Rad;
                wing.Add(new BirdQuill
                {
                    Index = k, Root = root + n * 0.3f, Dir = tan * Mathf.Cos(phi) + n * Mathf.Sin(phi),
                    Length = 4.6f, Half = 1.7f, Covert = true,
                });
            }
            return wing.ToArray();
        }

        static void BirdEdgeAt(Vector2[] pts, float s, float sign, out Vector2 at, out Vector2 tan, out Vector2 n)
        {
            float f = s * (pts.Length - 1);
            int i = Mathf.Min(pts.Length - 2, (int)f);
            at = Vector2.Lerp(pts[i], pts[i + 1], f - i);
            tan = (pts[i + 1] - pts[i]).normalized;
            n = new Vector2(-tan.y, tan.x) * sign;
        }

        static bool BirdFeatherAt(BirdStyle st, float x, float y, in BirdQuill q, out float b, out float tt, out float hw)
        {
            float px = x - q.Root.x, py = y - q.Root.y;
            float a = px * q.Dir.x + py * q.Dir.y;
            b = -px * q.Dir.y + py * q.Dir.x;
            tt = a / q.Length;
            hw = 0f;
            if (a <= 0f || a >= q.Length) return false;
            float profile = q.Covert
                ? Mathf.Min(1f, Mathf.Sqrt(tt / 0.15f)) * Mathf.Sqrt(Mathf.Max(0f, 1f - tt * tt * tt))
                : st.RoundTips
                    ? Mathf.Min(1f, Mathf.Sqrt(tt / 0.1f)) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Max(0f, (tt - 0.7f) / 0.3f), 2f)))
                    : Mathf.Min(1f, Mathf.Sqrt(tt / 0.1f)) * (1f - Mathf.Pow(Mathf.Max(0f, (tt - 0.6f) / 0.4f), 1.5f));
            hw = q.Half * Mathf.Min(1f, profile);
            if (Mathf.Abs(b) >= hw) return false;
            if (q.Notch && tt > 0.55f && tt < 0.75f && b > 0f) return false;   // a ragged bite out of the vane
            return true;
        }

        static float BirdEllipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float u = (x - cx) / rx, v = (y - cy) / ry;
            return u * u + v * v;
        }

        static char BirdTexel(BirdStyle st, float x, float y, bool fine)
        {
            float e = fine ? 0.45f : 0.85f;

            char head = st.Head(x, y, e, fine);
            if (head != '.') return head;

            // ---- the body: broad behind the head, tapering back to the tail ----
            if (x >= 10.5f && x <= 22.5f)
            {
                float u = (x - 10.5f) / 12f;
                float half = 1.4f + 3.8f * Mathf.Pow(Mathf.Sin(Mathf.Min(1f, u / 0.8f) * Mathf.PI / 2f), 1.2f)
                             - (u < 0.86f ? 0f : (u - 0.86f) / 0.14f);
                float dy = y - (41f - 0.5f * u);
                if (Mathf.Abs(dy) < half)
                {
                    if (half - Mathf.Abs(dy) < e * 0.95f) return 'k';
                    return st.Body(x, u, dy, half, fine);
                }
            }

            if (st.StringAndTail != null)
            {
                char t = st.StringAndTail(st, x, y, e, fine);
                if (t != '.') return t;
            }
            // ---- the string, between the nocks ----
            else if (y > st.NockTop.y && y < st.NockBottom.y)
            {
                float u = (y - st.NockTop.y) / (st.NockBottom.y - st.NockTop.y);
                float sx = Mathf.Lerp(st.NockTop.x, st.NockBottom.x, u);
                if (Mathf.Abs(x - sx) < (fine ? 0.25f : 0.5f)) return st.StringChar;
            }

            // ---- the tail: a fan back over the hand, its feathers' ends rounded ----
            if (st.StringAndTail == null && x >= 7f && x < 10.9f)
            {
                float v = (10.9f - x) / 3.9f;
                float half = 1.4f + 1.9f * v;
                float dy = y - 41f;
                float end = 7f + 0.5f * Mathf.Pow(Mathf.Abs(dy) / Mathf.Max(0.1f, half), 2f);
                if (Mathf.Abs(dy) < half && x >= end)
                {
                    if (half - Mathf.Abs(dy) < e * 0.9f || x < end + e) return 'k';
                    return st.Tail(v, dy, half, fine);
                }
            }

            // ---- the leading edges: each wing's bone, a smooth ridge with a lit line ----
            for (int w = 0; w < 2; w++)
            {
                var pts = w == 0 ? BirdUpper : BirdLower;
                float sign = w == 0 ? 1f : -1f;
                float best = 9f, side = 0f, at = 0f;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    var a = pts[i]; var v = pts[i + 1] - a;
                    float t = Mathf.Clamp01(((x - a.x) * v.x + (y - a.y) * v.y) / v.sqrMagnitude);
                    float cx = a.x + v.x * t, cy = a.y + v.y * t;
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d < best)
                    {
                        best = d; at = (i + t) / (pts.Length - 1);
                        side = ((x - cx) * -v.y - (y - cy) * v.x) * sign;   // > 0 outward, away from the feathers
                    }
                }
                if (at < 0.04f || at > 0.97f) continue;
                float width = fine ? 1.15f - 0.55f * at : 1.9f - 0.7f * at;
                if (best < width)
                {
                    if (side > 0f && best > width - e * 0.9f) return st.EdgeLine;
                    if (side > 0f && best > width * (fine ? 0.25f : 0.1f)) return st.EdgeLit;
                    return st.EdgeInside;
                }
                if (side > 0f && best < width + e * 0.9f) return st.EdgeLine;
            }

            if (st.OverFeathers != null)
            {
                char over = st.OverFeathers(st, x, y, fine);
                if (over != '.') return over;
            }

            // ---- the wings: the last feather found lies on top ----
            for (int w = 0; w < 2; w++)
            {
                bool found = false;
                BirdQuill q = default;
                float b = 0f, tt = 0f, hw = 0f;
                foreach (var f in st.Wings[w])
                    if (BirdFeatherAt(st, x, y, f, out float fb, out float ft, out float fh))
                    {
                        found = true; q = f; b = fb; tt = ft; hw = fh;
                    }
                if (!found) continue;
                return st.Feather(q, b, tt, hw, b * (w == 0 ? -1f : 1f), e, fine);
            }
            return '.';
        }

        static Vector2[] BirdCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            const int n = 60;
            var pts = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, v = 1f - u;
                pts[i] = v * v * v * p0 + 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u * p3;
            }
            return pts;
        }
    }
}
