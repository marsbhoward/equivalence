using System;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// A small CPU canvas for the hub's texel art (the user's group-6 picks off
    /// https://claude.ai/artifact/UwqhVPtsQBdZVjJXMHuNMU, 2026-10-26). y runs UP from the bottom row;
    /// every fixture is drawn SOLID at three-quarters - a front face and a top, like the columns
    /// (the user's call) - with <see cref="Box3"/> and <see cref="Cyl3"/>.
    /// </summary>
    public sealed class TexelCanvas
    {
        public readonly int W, H;
        public readonly Color32[] Px;
        public static readonly Color32 K = new(13, 11, 14, 255);

        public TexelCanvas(int w, int h) { W = w; H = h; Px = new Color32[w * h]; }

        public bool Has(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && Px[y * W + x].a > 0;

        public void Set(int x, int y, Color32 c, float a = 1f)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || a <= 0f) return;
            int i = y * W + x;
            if (a >= 1f || Px[i].a == 0) { c.a = (byte)(c.a * Mathf.Clamp01(a)); Px[i] = c; return; }
            Px[i] = Color32.Lerp(Px[i], new Color32(c.r, c.g, c.b, 255), a);
        }

        public void Rect(int x0, int y0, int x1, int y1, Color32 c, float a = 1f)
        {
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Set(x, y, c, a);
        }

        public void Line(float x0, float y0, float x1, float y1, Color32 c, float a = 1f)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))));
            for (int i = 0; i <= n; i++) Set(Mathf.RoundToInt(x0 + (x1 - x0) * i / n), Mathf.RoundToInt(y0 + (y1 - y0) * i / n), c, a);
        }

        /// <summary>A box at 3/4: <paramref name="x"/> its left, <paramref name="y"/> the bottom row of
        /// its front face; w wide, h tall at the front, d deep on top. The near top edge catches the light.</summary>
        public void Box3(int x, int y, int w, int d, int h, Color32[] r,
                         Func<int, int, Color32?> front = null, Func<int, int, Color32?> top = null, bool lip = true)
        {
            for (int yy = 0; yy < h; yy++)
                for (int xx = 0; xx < w; xx++)
                {
                    bool edge = xx == 0 || xx == w - 1 || yy == 0;
                    Set(x + xx, y + yy, edge ? K : front?.Invoke(xx, yy) ?? (xx < 2 ? r[1] : r[2]));
                }
            for (int yy = 0; yy < d; yy++)
                for (int xx = 0; xx < w; xx++)
                {
                    bool edge = xx == 0 || xx == w - 1 || yy == d - 1;
                    Set(x + xx, y + h + yy, edge ? K : top?.Invoke(xx, yy) ?? r[1]);
                }
            if (lip) for (int xx = 1; xx < w - 1; xx++) Set(x + xx, y + h, r[0]);
        }

        /// <summary>A cylinder at 3/4: its base centred on <paramref name="cx"/>, the base's front point
        /// on row <paramref name="y"/>; rx wide, h tall. Returns the row of the top ellipse's centre.</summary>
        public int Cyl3(int cx, int y, int rx, int h, Color32[] r,
                        Func<int, int, float, Color32?> body = null, Func<int, int, Color32?> top = null)
        {
            float ry = Mathf.Max(1f, rx * 0.38f);
            float cyB = y + ry, cyT = cyB + h;
            for (int x = -rx; x <= rx; x++)
            {
                float e = ry * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / (float)rx) * (x / (float)rx)));
                float u = (x + rx) / (2f * rx);
                int lo = Mathf.RoundToInt(cyB - e), hi = Mathf.RoundToInt(cyT - e);
                for (int yy = lo; yy <= hi; yy++)
                    Set(cx + x, yy, Mathf.Abs(x) == rx || yy == lo ? K : body?.Invoke(x, yy, u) ?? Ramp(r, 0.15f + u * 0.6f));
            }
            for (int x = -rx; x <= rx; x++)
            {
                float e = ry * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / (float)rx) * (x / (float)rx)));
                int lo = Mathf.RoundToInt(cyT - e), hi = Mathf.RoundToInt(cyT + e);
                for (int yy = lo; yy <= hi; yy++)
                    Set(cx + x, yy, yy == hi || Mathf.Abs(x) == rx ? K : yy == lo ? r[0] : top?.Invoke(x, yy) ?? r[1]);
            }
            return Mathf.RoundToInt(cyT);
        }

        public static Color32 Ramp(Color32[] r, float u) => r[Mathf.Clamp(Mathf.FloorToInt(u * r.Length), 0, r.Length - 1)];

        /// <summary>A sprite pivoted on texel (<paramref name="px"/>, <paramref name="py"/>) - a fixture's foot.</summary>
        public Sprite ToSprite(string name, int px, int py, float ppu)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave, name = name,
            };
            tex.SetPixels32(Px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2((px + 0.5f) / W, (py + 0.5f) / H), ppu, 0, SpriteMeshType.FullRect);
            s.name = name;
            return s;
        }
    }

    /// <summary>The hub's materials and the picked fixtures' drawings, cached per key.</summary>
    public static class HubArt
    {
        public const float Ppu = Combat.TexelFx.Body;

        static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static Color32[] R(params string[] h) { var r = new Color32[h.Length]; for (int i = 0; i < h.Length; i++) r[i] = C(h[i]); return r; }

        public static readonly Color32[] Oak = R("#c89a64", "#a07448", "#7a5432", "#58391f", "#3a2412", "#20140a");
        public static readonly Color32[] DarkWood = R("#8a6248", "#6a4632", "#4e3122", "#382216", "#24150c", "#140b06");
        public static readonly Color32[] Stone = R("#b8b2a8", "#948e84", "#726c64", "#544f48", "#38342f", "#1f1d1a");
        public static readonly Color32[] Brass = R("#fff0b0", "#e8c060", "#c09030", "#8a6418", "#58400c", "#2c2006");
        public static readonly Color32[] Iron = R("#b8bec8", "#8a929e", "#626a76", "#444a54", "#2c3038", "#16181c");
        public static readonly Color32[] Parch = R("#fbf3d8", "#efe0b4", "#d8c48c", "#b09a64", "#806c40", "#4c3e22");
        public static readonly Color32[] Gold = R("#fff8e0", "#ffe39a", "#f2c25a", "#b8862a", "#6e4e14", "#382808");
        public static readonly Color32[] Teal = R("#e8fffb", "#9ff0e4", "#4cc8bc", "#2a8c88", "#18545a", "#0a2a30");

        static Dictionary<string, Sprite> _cache = new();

        /// <summary>A drawing, built once per key (a reload empties the cache and it refills).</summary>
        public static Sprite Get(string key, Func<Sprite> build)
        {
            _cache ??= new Dictionary<string, Sprite>();
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            return _cache[key] = build();
        }

        public static Sprite[] Frames(string key, int n, Func<int, Sprite> build)
        {
            var a = new Sprite[n];
            for (int i = 0; i < n; i++) { int k = i; a[i] = Get(key + "#" + i, () => build(k)); }
            return a;
        }

        static float Noise(float x, float y) => Combat.FireArt.Noise(x, y);
        static float Hash(int a, int b = 0, int c = 0) => Combat.FireArt.Hash(a, b, c);

        static void Fire(TexelCanvas c, int x, int y, int w, int h, float t, float strength)
        {
            for (int yy = 0; yy < h; yy++)
                for (int xx = -w; xx <= w; xx++)
                {
                    float v = Noise(xx * 0.6f, (yy - t * 14f) * 0.5f) * strength - Mathf.Abs(xx) / (w + 1f) * 0.5f - yy / (float)h * 0.45f;
                    if (v > 0.2f) c.Set(x + xx, y + yy, Combat.FireArt.At(Mathf.Clamp01(0.9f - v * 1.6f)));
                }
        }

        // ---- the forge: hearth and anvil ----

        /// <summary>A stone hearth, its hood rising toward the wall, a fire in its mouth (full while the
        /// forge has something to do, embers otherwise), an anvil before it. Pivot at the hearth's foot.</summary>
        public static Sprite Hearth(int frame, bool lit) => Get($"hearth.{frame}.{lit}", () =>
        {
            var c = new TexelCanvas(64, 70);
            int fx = 34, fy = 8;
            c.Box3(fx - 15, fy, 30, 7, 17, Stone, (x, y) => y % 6 == 0 ? Stone[3] : x < 2 ? Stone[1] : Stone[2]);
            for (int y = fy + 24; y < fy + 52; y++)
            {
                int w = 9 - (y - fy - 24) / 8;
                c.Rect(fx - w, y, fx + w, y, (y - fy) % 5 == 0 ? Stone[4] : Stone[2]);
                c.Set(fx - w, y, TexelCanvas.K); c.Set(fx + w, y, TexelCanvas.K);
            }
            // The mouth: an arch, dark inside, the fire standing in it.
            for (int y = fy + 1; y <= fy + 11; y++)
                for (int x = fx - 7; x <= fx + 7; x++)
                    if (y < fy + 7 || Mathf.Pow((x - fx) / 7.5f, 2) + Mathf.Pow((y - fy - 7) / 5f, 2) < 1f) c.Set(x, y, new Color32(18, 8, 6, 255));
            float t = frame / 8f * 0.9f;
            if (lit) Fire(c, fx, fy + 1, 6, 10, t, 1f);
            else for (int x = fx - 5; x <= fx + 5; x++) if (Hash(x, frame) > 0.5f) c.Set(x, fy + 1, Combat.FireArt.At(0.65f + Hash(x, 2) * 0.3f));
            // The anvil, in front and to the left.
            c.Box3(12, 0, 12, 3, 5, Iron);
            c.Rect(9, 8, 12, 8, Iron[2]); c.Set(8, 8, TexelCanvas.K);
            return c.ToSprite("hearth", fx, fy, Ppu);
        });

        // ---- the terminal: the printing cabinet ----

        /// <summary>A brass-bound cabinet with a dial; a paper tape curls out of its slot onto the floor,
        /// a line printing as it runs. Pivot at the cabinet's foot.</summary>
        public static Sprite Cabinet(int frame) => Get($"cabinet.{frame}", () =>
        {
            var c = new TexelCanvas(56, 46);
            int fx = 18, fy = 6;
            c.Box3(fx - 12, fy, 24, 8, 26, DarkWood, (x, y) => x < 2 || x > 21 ? Brass[3] : y == 13 ? Brass[2] : x < 4 ? DarkWood[1] : DarkWood[2]);
            float a = frame / 12f * Mathf.PI * 2f;
            for (float t = 0; t < 6.3f; t += 0.2f) c.Set(fx + Mathf.RoundToInt(Mathf.Cos(t) * 4), fy + 20 + Mathf.RoundToInt(Mathf.Sin(t) * 4), Brass[1]);
            c.Line(fx, fy + 20, fx + Mathf.Cos(a) * 3f, fy + 20 + Mathf.Sin(a) * 3f, TexelCanvas.K);
            c.Rect(fx - 5, fy + 9, fx + 5, fy + 10, TexelCanvas.K);   // the slot
            // The tape: out of the slot, down over the front, and away across the floor.
            int printed = frame * 2;
            for (int i = 0; i < 26; i++)
            {
                int x = fx + 2 + i, y = i < 7 ? fy + 9 - i : Mathf.Max(0, fy + 2 - (i - 7) / 4);
                for (int w = 0; w < 4; w++) c.Set(x, y - w * 0 + (i < 7 ? 0 : 0), Parch[1]);
                c.Set(x, y - 1, Parch[3]);
                if (i % 3 == 0 && i < printed) c.Set(x, y, TexelCanvas.K);
            }
            return c.ToSprite("cabinet", fx, fy, Ppu);
        });

        // ---- the photo booth: the standing mirror ----

        /// <summary>A tall oval mirror in a gilt frame on clawed feet, the glass shimmering; white for a
        /// moment when the portrait is taken. Pivot at its feet.</summary>
        public static Sprite Mirror(int frame, bool flash) => Get($"mirror.{frame}.{flash}", () =>
        {
            var c = new TexelCanvas(30, 54);
            int cx = 15, cy = 28;
            for (int y = 0; y < 54; y++)
                for (int x = 0; x < 30; x++)
                {
                    float d = Mathf.Sqrt(Mathf.Pow((x - cx) / 12f, 2) + Mathf.Pow((y - cy) / 21f, 2));
                    if (d > 1f) continue;
                    Color32 col;
                    if (d > 0.88f) col = Hash(x, y) > 0.5f ? Gold[1] : Gold[2];
                    else if (d > 0.82f) col = TexelCanvas.K;
                    else if (flash) col = new Color32(255, 255, 255, 255);
                    else
                    {
                        float k = Mathf.Clamp01(1f - (x - cx + 12) / 24f + Mathf.Sin((y + x) * 0.2f + frame * 0.8f) * 0.1f);
                        col = Color32.Lerp(C("#3a4a5c"), C("#8aa6c0"), k);
                    }
                    c.Set(x, y, col);
                }
            for (int s = -1; s <= 1; s += 2) { c.Line(cx + s * 3, 7, cx + s * 7, 1, Gold[3]); c.Set(cx + s * 7, 0, Gold[1]); c.Set(cx + s * 8, 0, Gold[2]); }
            return c.ToSprite("mirror", cx, 0, Ppu);
        });

        // ---- the manual shrine: altar and tome ----

        public static Sprite Altar(int frame) => Get($"altar.{frame}", () =>
        {
            var c = new TexelCanvas(40, 34);
            int fx = 20, fy = 2;
            c.Box3(fx - 16, fy, 32, 8, 13, Stone, (x, y) => y == 9 ? Stone[1] : x == 7 || x == 24 ? Stone[3] : x < 2 ? Stone[1] : Stone[2]);
            int top = fy + 13;
            float glow = 0.3f + 0.2f * Mathf.Sin(frame / 6f * Mathf.PI * 2f);
            for (int y = top + 2; y <= top + 8; y++)
                for (int x = fx - 9; x <= fx + 9; x++)
                    c.Set(x, y, x == fx ? Parch[4] : y == top + 2 ? DarkWood[2] : Color32.Lerp(Parch[1], Gold[0], glow));
            for (int i = 0; i < 3; i++) for (int x = fx - 7; x <= fx - 2; x++) c.Set(x, top + 7 - i * 2, Parch[3]);
            for (int s = -1; s <= 1; s += 2)
            {
                int x = fx + s * 13; c.Rect(x, top + 1, x, top + 5, Parch[1]);
                c.Set(x, top + 6, Combat.FireArt.At((frame + (s > 0 ? 1 : 0)) % 2 == 0 ? 0.1f : 0.3f)); c.Set(x, top + 7, Combat.FireArt.At(0.2f), 0.7f);
            }
            return c.ToSprite("altar", fx, fy, Ppu);
        });

        // ---- the mastery table: the oak map table ----

        /// <summary>A heavy oak table on turned legs with parchment across its top. Pivot at the front
        /// legs' feet. The board's own sketch is laid over the parchment by GridTable.</summary>
        public static Sprite MapTable(int w, int d) => Get($"maptable.{w}.{d}", () =>
        {
            var c = new TexelCanvas(w + 2, d + 18);
            int x0 = 1, legs = 11;
            foreach (int lx in new[] { x0 + 1, x0 + w - 4 }) c.Box3(lx, 0, 3, 1, legs, Oak);
            c.Box3(x0, legs, w, d, 3, Oak, null, (x, y) => x > 2 && x < w - 3 && y > 1 && y < d - 2 ? ((x + y * 2) % 7 == 0 ? Parch[3] : Parch[1]) : Oak[1]);
            return c.ToSprite("maptable", x0 + w / 2, 0, Ppu);
        });

        // ---- the couch: the wooden settle ----

        /// <summary>A high-backed oak settle of <paramref name="w"/> texels, cushions along its seat.
        /// Pivot at the front of its seat, in the middle.</summary>
        public static Sprite Settle(int w) => Get($"settle.{w}", () =>
        {
            var c = new TexelCanvas(w + 2, 44);
            c.Box3(1, 8, w, 4, 26, Oak, (x, y) => x % 12 == 0 ? Oak[3] : y > 22 ? Oak[1] : Oak[2]);
            c.Box3(1, 0, w, 8, 8, Oak);
            for (int x = 3; x < w - 1; x++) if ((x - 3) % 14 < 12) { c.Set(x, 9, Teal[3]); c.Set(x, 10, Teal[2]); c.Set(x, 11, Teal[2]); }
            return c.ToSprite("settle", 1 + w / 2, 0, Ppu);
        });

        // ---- the crate: banded ----

        public static Sprite Crate() => Get("crate", () =>
        {
            var c = new TexelCanvas(36, 30);
            c.Box3(2, 1, 32, 10, 17, Oak,
                   (x, y) => x == 4 || x == 27 ? Iron[3] : y % 5 == 0 ? Oak[3] : x < 2 ? Oak[1] : Oak[2],
                   (x, y) => x == 4 || x == 27 ? Iron[2] : x % 6 == 0 ? Oak[3] : Oak[1]);
            for (int x = 3; x < 33; x++) c.Set(x, 0, TexelCanvas.K, 0.35f);
            return c.ToSprite("crate", 18, 1, Ppu);
        });

        // ---- the weapon rack: the oak A-frame ----

        /// <summary>Two oak posts and crossbars, <paramref name="w"/> texels wide, the weapon held upright
        /// between them. Pivot at the posts' feet.</summary>
        public static Sprite AFrame(int w, int h) => Get($"aframe.{w}.{h}", () =>
        {
            var c = new TexelCanvas(w + 2, h + 6);
            c.Box3(1, 0, 4, 2, h, Oak);
            c.Box3(w - 3, 0, 4, 2, h, Oak);
            c.Box3(1, h - 8, w, 2, 3, Oak);
            c.Box3(1, 6, w, 2, 3, Oak);
            return c.ToSprite("aframe", 1 + w / 2, 0, Ppu);
        });

        // ---- the armour stand: the carved mannequin ----

        /// <summary>A carved wooden block for the mannequin, <paramref name="w"/> x <paramref name="h"/>
        /// texels at the rig's density (the figure is scaled like the avatar), bevelled, grained, a
        /// round joint at its top. Pivot in its middle - the dummy's blocks are placed by centre.</summary>
        public static Sprite MannequinBlock(int w, int h, bool round) => Get($"mannequin.{w}.{h}.{round}", () =>
        {
            var c = new TexelCanvas(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (round && Mathf.Pow((x + 0.5f - w / 2f) / (w / 2f), 2) + Mathf.Pow((y + 0.5f - h / 2f) / (h / 2f), 2) > 1f) continue;
                    bool edge = round
                        ? Mathf.Pow((x + 0.5f - w / 2f) / (w / 2f - 1f), 2) + Mathf.Pow((y + 0.5f - h / 2f) / (h / 2f - 1f), 2) > 1f
                        : x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    Color32 col = edge ? Oak[4] : x < w / 3 ? Oak[0] : x > w * 2 / 3 ? Oak[2] : Oak[1];
                    if (!edge && !round && (x + y * 3) % 7 == 0) col = Oak[2];
                    c.Set(x, y, col);
                }
            if (!round && w >= 6) { c.Set(w / 2, h - 2, Oak[0]); c.Set(w / 2 - 1, h - 2, Oak[3]); }
            return c.ToSprite("mannequin", w / 2, h / 2, Combat.TexelFx.Body * Tuning.Player.ArenaVisualScale);
        });

        /// <summary>The mannequin's round oak base, a cylinder. Pivot at its front point.</summary>
        public static Sprite MannequinBase() => Get("mannequin.base", () =>
        {
            var c = new TexelCanvas(28, 14);
            c.Cyl3(14, 0, 12, 4, Oak);
            c.Rect(13, 9, 15, 13, Oak[2]);
            return c.ToSprite("mannequin.base", 14, 0, Ppu);
        });

        // ---- the trophy frames: gilt and glass ----

        /// <summary>A gilt frame round <paramref name="inner"/> texels of art. Pivot in its middle.</summary>
        public static Sprite GiltFrame(int inner) => Get($"gilt.{inner}", () =>
        {
            int b = 4, n = inner + b * 2;
            var c = new TexelCanvas(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool frame = x < b || y < b || x >= n - b || y >= n - b;
                    if (!frame) { c.Set(x, y, C("#201e24")); continue; }
                    bool outer = x == 0 || y == 0 || x == n - 1 || y == n - 1, inner1 = x == b - 1 || y == b - 1 || x == n - b || y == n - b;
                    c.Set(x, y, outer || inner1 ? Gold[4] : (x + y) % 5 == 0 ? Gold[0] : y > n / 2 ? Gold[1] : Gold[2]);
                }
            return c.ToSprite("gilt", n / 2, n / 2, Ppu);
        });

        /// <summary>A stone plinth for a standing piece: a cylinder with a gilt band round its top.</summary>
        public static Sprite Plinth(int rx, int h) => Get($"plinth.{rx}.{h}", () =>
        {
            var c = new TexelCanvas(rx * 2 + 3, h + rx + 4);
            int top = c.Cyl3(rx + 1, 0, rx, h, Stone, (x, y, u) => y >= h - 1 ? Gold[2] : null);
            return c.ToSprite("plinth", rx + 1, 0, Ppu);
        });

        /// <summary>A glass bell jar over a standing piece, <paramref name="w"/> x <paramref name="h"/>
        /// texels: an outline catching the light and one long highlight. Pivot at its foot's middle.</summary>
        public static Sprite BellJar(int w, int h) => Get($"jar.{w}.{h}", () =>
        {
            var c = new TexelCanvas(w + 2, h + 2);
            Color32 rim = C("#d8f0ff"), hi = new(255, 255, 255, 255);
            float cx = (w + 1) / 2f, r = w / 2f;
            for (int y = 0; y <= h; y++)
            {
                float half = y < h - r ? r : Mathf.Sqrt(Mathf.Max(0f, r * r - Mathf.Pow(y - (h - r), 2)));
                c.Set(Mathf.RoundToInt(cx - half), y, rim, 0.75f); c.Set(Mathf.RoundToInt(cx + half), y, rim, 0.75f);
                if (y > 3 && y < h - r * 0.4f) c.Set(Mathf.RoundToInt(cx - half + 3), y, hi, 0.45f);
            }
            for (int x = 0; x <= w; x++) c.Set(x + 1, h, rim, 0.6f);
            return c.ToSprite("jar", Mathf.RoundToInt(cx), 0, Ppu);
        });

        // ---- the armoury doorway: the raised portcullis ----

        /// <summary>An iron-framed opening, black, its portcullis drawn up into the top. Pivot at the
        /// threshold's middle.</summary>
        public static Sprite Portcullis(int halfW, int h) => Get($"portcullis.{halfW}.{h}", () =>
        {
            int w = halfW * 2 + 1;
            var c = new TexelCanvas(w + 8, h + 5);
            int x0 = 4;
            c.Rect(x0, 1, x0 + w - 1, h, C("#050406"));
            for (int y = 1; y <= h + 3; y++) for (int k = 1; k <= 3; k++) { c.Set(x0 - k, y, k == 3 ? TexelCanvas.K : Iron[k == 1 ? 2 : 1]); c.Set(x0 + w - 1 + k, y, k == 3 ? TexelCanvas.K : Iron[3]); }
            for (int x = x0 - 3; x <= x0 + w + 2; x++) { c.Set(x, h + 1, Iron[1]); c.Set(x, h + 2, Iron[2]); c.Set(x, h + 3, TexelCanvas.K); }
            // The gate itself, drawn up: its last bars and the spikes hanging from the top.
            for (int x = x0; x < x0 + w; x += 3) for (int y = h - 6; y <= h; y++) c.Set(x, y, Iron[2]);
            for (int x = x0; x < x0 + w; x++) c.Set(x, h - 3, Iron[1]);
            for (int x = x0; x < x0 + w; x += 3) c.Set(x, h - 7, Iron[3]);
            for (int x = x0 - 2; x < x0 + w + 2; x++) c.Set(x, 0, Stone[2]);
            return c.ToSprite("portcullis", x0 + halfW, 0, Ppu);
        });
    }

    /// <summary>
    /// A hub fixture's picture (group 6): one renderer cycling its frames, an OUTLINE while the
    /// player is near it (the target rim's method - it replaced the glows that brightened), and
    /// SEE-THROUGH while the player stands behind it (the columns' rule, the user's call). Sorted by
    /// the fixture's own DepthSorted, which the owner attaches with this renderer.
    /// </summary>
    public class HubFixture : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _art, _rim;
        [SerializeField] float _alpha = 1f, _fps = 8f;
        [SerializeField] bool _focus;
        Sprite[] _frames;
        Func<int, Sprite> _frameAt;
        int _count = 1;
        static Transform _player;

        public SpriteRenderer Art => _art;
        public SpriteRenderer RimRenderer => _rim;

        public static HubFixture Attach(GameObject owner, Sprite art, Vector2 foot, int order = 2)
        {
            var go = new GameObject("fixture.art");
            go.transform.SetParent(owner.transform, false);
            go.transform.localPosition = foot;
            var f = owner.AddComponent<HubFixture>();
            f._art = go.AddComponent<SpriteRenderer>();
            f._art.sprite = art;
            f._art.sortingOrder = order;
            var rimGo = new GameObject("fixture.rim");
            rimGo.transform.SetParent(go.transform, false);
            f._rim = rimGo.AddComponent<SpriteRenderer>();
            f._rim.sortingOrder = order - 1;
            f._rim.color = new Color(0.96f, 0.94f, 0.9f, 0.9f);
            f._rim.enabled = false;
            return f;
        }

        /// <summary>Animate: <paramref name="frameAt"/> answers frame i of <paramref name="count"/>, at
        /// <paramref name="fps"/>. Rebuilt lazily - a reload loses the delegate and the picture holds.</summary>
        public void Animate(int count, float fps, Func<int, Sprite> frameAt) { _count = Mathf.Max(1, count); _fps = fps; _frameAt = frameAt; }

        public void SetFocus(bool on) => _focus = on;

        public static void SetPlayer(Transform t) => _player = t;

        void LateUpdate()
        {
            if (_art == null) return;
            if (_frameAt != null)
            {
                int i = Mathf.FloorToInt(Time.unscaledTime * _fps) % _count;
                var s = _frameAt(i);
                if (s != null && _art.sprite != s) _art.sprite = s;
            }
            if (_rim != null)
            {
                _rim.enabled = _focus && _art.enabled;
                if (_rim.enabled)
                {
                    var r = Convergence.Art.PixelSprite.Rim(_art.sprite, 1, false);
                    if (_rim.sprite != r) _rim.sprite = r;
                    _rim.sortingOrder = _art.sortingOrder - 1;
                }
            }

            // Behind it: the player's feet above the fixture's foot, inside its drawn width and under its top.
            if (_player == null) { var a = FindAnyObjectByType<HubAvatar>(); if (a != null) _player = a.transform; }
            bool behind = false;
            if (_player != null && _player.gameObject.activeInHierarchy && _art.sprite != null)
            {
                var b = _art.bounds; var p = _player.position;
                behind = p.y > _art.transform.position.y + 0.02f && p.y < b.max.y && p.x > b.min.x - 0.15f && p.x < b.max.x + 0.15f;
            }
            _alpha = Mathf.MoveTowards(_alpha, behind ? Tuning.Hazards.ColumnBehindAlpha : 1f, Time.unscaledDeltaTime / Tuning.Hazards.ColumnFadeSeconds);
            var c = _art.color;
            if (!Mathf.Approximately(c.a, _alpha)) { c.a = _alpha; _art.color = c; }
        }
    }
}
