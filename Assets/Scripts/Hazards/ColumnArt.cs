using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>How a column is drawn. Append only - nothing saves it, but keep the habit.</summary>
    public enum ColumnStyle { Tall, Broken }

    /// <summary>
    /// The column's picture, standing up the way everything else on the floor does (the user's
    /// call, 2026-10-21 - the old one was a disc seen from straight overhead). Two styles from the
    /// mock-up https://claude.ai/artifact/H5BpxmCa7FWKydzKtUfWqL, rolled per column: a TALL fluted
    /// drum (base, shaft, round cap) and a BROKEN one, snapped off near the player's height with a
    /// rough slanted break.
    ///
    /// Drawn texel by texel at the room walls' density (75 a unit) in the walls' own stone
    /// (RoomWalls.WallArt), so a column is the room's material: a curved face is that stone's face
    /// colour pushed into a five-step ramp, lit from the upper left; the cap and the break are its
    /// top colours, the near lip lit and the far rim pale as the walls' north edge. Cracked is
    /// LIGHTER and warmer (the cracked walls' rule), chipped at its edges, fissured, with rubble at
    /// its foot.
    ///
    /// The FOOTPRINT is the collider: the sprite's pivot is the circle's centre, and a ground
    /// circle is drawn flattened by <see cref="Squash"/>. Everything above it is drawing only.
    /// </summary>
    static partial class ColumnArt
    {
        const float Ppu = 75f;
        /// <summary>How flat a circle on the ground is drawn (its depth over its width).</summary>
        public const float Squash = 0.5f;
        /// <summary>Cracked looks per (style, size) - each spawn picks one, so a floor's damage varies.</summary>
        public const int Variants = 3;

        static readonly Dictionary<int, Sprite> _cache = new();

        struct Pal { public Color[] Ramp; public Color Top, TopLight, Seam, Rim; }

        static readonly Color Line = new(0.06f, 0.05f, 0.04f), Crack = new(0.08f, 0.06f, 0.05f);

        static readonly Pal Stone = new()
        {
            Ramp = new[] { new Color(0.10f, 0.08f, 0.06f), new Color(0.14f, 0.11f, 0.085f), new Color(0.18f, 0.145f, 0.11f),
                           new Color(0.22f, 0.18f, 0.135f), new Color(0.26f, 0.215f, 0.16f) },
            Top = new(0.27f, 0.22f, 0.17f), TopLight = new(0.31f, 0.26f, 0.20f),
            Seam = new(0.20f, 0.16f, 0.12f), Rim = new(0.44f, 0.37f, 0.29f),
        };

        static readonly Pal Cracked = new()
        {
            Ramp = new[] { new Color(0.19f, 0.15f, 0.11f), new Color(0.25f, 0.195f, 0.14f), new Color(0.31f, 0.245f, 0.175f),
                           new Color(0.37f, 0.295f, 0.21f), new Color(0.43f, 0.345f, 0.25f) },
            Top = new(0.46f, 0.37f, 0.28f), TopLight = new(0.53f, 0.43f, 0.32f),
            Seam = new(0.36f, 0.29f, 0.22f), Rim = new(0.62f, 0.52f, 0.40f),
        };

        /// <summary>The Sky Ruin draws its columns in pale stone (set round one draw, never read elsewhere).</summary>
        static bool _skyPal, _saltPal;

        // The Salt Flats' pillars of salt - white with a blush, the cracked ones warmer.
        static readonly Pal Salt = new()
        {
            Ramp = new[] { new Color(0.58f, 0.50f, 0.54f), new Color(0.68f, 0.61f, 0.64f), new Color(0.78f, 0.72f, 0.74f),
                           new Color(0.86f, 0.82f, 0.82f), new Color(0.93f, 0.90f, 0.89f) },
            Top = new(0.95f, 0.93f, 0.92f), TopLight = new(0.99f, 0.98f, 0.97f),
            Seam = new(0.72f, 0.64f, 0.67f), Rim = new(1f, 1f, 1f),
        };

        static readonly Pal SaltCracked = new()
        {
            Ramp = new[] { new Color(0.66f, 0.52f, 0.48f), new Color(0.76f, 0.62f, 0.57f), new Color(0.84f, 0.72f, 0.66f),
                           new Color(0.90f, 0.80f, 0.74f), new Color(0.95f, 0.88f, 0.82f) },
            Top = new(0.97f, 0.91f, 0.85f), TopLight = new(1f, 0.96f, 0.91f),
            Seam = new(0.80f, 0.66f, 0.60f), Rim = new(1f, 0.98f, 0.94f),
        };

        static readonly Pal Marble = new()
        {
            Ramp = new[] { new Color(0.38f, 0.36f, 0.33f), new Color(0.48f, 0.46f, 0.42f), new Color(0.58f, 0.56f, 0.51f),
                           new Color(0.67f, 0.65f, 0.59f), new Color(0.76f, 0.74f, 0.68f) },
            Top = new(0.78f, 0.75f, 0.69f), TopLight = new(0.85f, 0.82f, 0.76f),
            Seam = new(0.55f, 0.52f, 0.47f), Rim = new(0.90f, 0.88f, 0.83f),
        };

        static readonly Pal MarbleCracked = new()
        {
            Ramp = new[] { new Color(0.45f, 0.40f, 0.34f), new Color(0.56f, 0.50f, 0.42f), new Color(0.66f, 0.59f, 0.50f),
                           new Color(0.75f, 0.68f, 0.58f), new Color(0.83f, 0.76f, 0.65f) },
            Top = new(0.85f, 0.78f, 0.67f), TopLight = new(0.91f, 0.85f, 0.74f),
            Seam = new(0.64f, 0.57f, 0.48f), Rim = new(0.95f, 0.90f, 0.80f),
        };

        /// <summary>How tall the drawn column stands, in world units (a cracked broken one is shorter).</summary>
        public static float HeightOf(ColumnStyle style, ColumnSize size, bool cracked)
        {
            float h = style == ColumnStyle.Tall
                ? size switch { ColumnSize.Small => Tuning.Hazards.ColumnTallSmall, ColumnSize.Medium => Tuning.Hazards.ColumnTallMedium, _ => Tuning.Hazards.ColumnTallLarge }
                : size switch { ColumnSize.Small => Tuning.Hazards.ColumnBrokenSmall, ColumnSize.Medium => Tuning.Hazards.ColumnBrokenMedium, _ => Tuning.Hazards.ColumnBrokenLarge };
            return style == ColumnStyle.Broken && cracked ? h * Tuning.Hazards.ColumnBrokenCrackedHeight : h;
        }

        /// <summary>Half the drawn SHAFT's width in world units - what covers a body standing behind it.</summary>
        public static float ShaftHalfWidth(ColumnStyle style, float radius) =>
            Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.Forest)
                ? radius * (style == ColumnStyle.Tall ? TreeCanopyWidth : 0.8f)   // a tree hides you behind its leaves
                : radius * (style == ColumnStyle.Tall ? 0.94f : 0.8f);

        public static Sprite For(ColumnStyle style, ColumnSize size, bool cracked, int variant)
        {
            bool forest = Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.Forest);
            variant = cracked || style == ColumnStyle.Broken || forest ? Mathf.Abs(variant) % Variants : 0;
            bool sky = Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SkyRuin);
            bool salt = Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SaltFlats);
            int key = (int)style | (int)size << 2 | (cracked ? 1 : 0) << 4 | variant << 5 | (forest ? 1 : 0) << 8 | (sky ? 1 : 0) << 9 | (salt ? 1 : 0) << 10;
            if (forest)
            {
                if (_cache.TryGetValue(key, out var t) && t != null) return t;
                return _cache[key] = Tree(style, size, cracked, variant);
            }
            if (_cache.TryGetValue(key, out var s) && s != null) return s;

            var rng = new System.Random(key * 7919 + 101);
            float r = Column.RadiusFor(size) * Ppu;
            float height = HeightOf(style, size, cracked) * Ppu;
            var g = new Grid();
            _skyPal = sky; _saltPal = salt;
            if (style == ColumnStyle.Tall) TallDrum(g, r, cracked, rng, height);
            else BrokenDrum(g, r, cracked, rng, height);
            _skyPal = false; _saltPal = false;
            if (sky) g.Vines(r, height, rng);
            return _cache[key] = g.ToSprite();
        }

        // ---- the two styles (r and heights in texels) ----

        static void TallDrum(Grid g, float r, bool cracked, System.Random rng, float H)
        {
            var pal = _saltPal ? (cracked ? SaltCracked : Salt) : _skyPal ? (cracked ? MarbleCracked : Marble) : cracked ? Cracked : Stone;
            float plinth = Mathf.Max(8f, r * 0.2f);
            float shaftR = r * 0.78f;
            float neck = H - Mathf.Max(10f, r * 0.28f);
            float echinus = neck + Mathf.Max(4f, r * 0.1f);

            g.Cylinder(r, 0f, plinth, pal);                                            // the base
            g.Cylinder(shaftR, plinth, neck, pal, flutes: r > 40f ? 5 : 4,
                       seams: new[] { plinth + (neck - plinth) / 3f, plinth + (neck - plinth) * 2f / 3f });
            g.Cylinder(shaftR * 1.1f, neck, echinus, pal, underline: true);
            g.Cylinder(r * 0.94f, echinus, H, pal, underline: true);                  // the cap
            if (cracked)
            {
                g.ChipTop(r * 0.55f, r * 0.3f, rng);
                g.ChipSide(neck * 0.55f, r * 0.17f, -1, rng);
                g.Rubble(rng, r, pal, 4);
            }
            g.Outline();
            if (cracked)
                g.Fissures(rng, 3, (int)(-shaftR * 0.7f), (int)(shaftR * 0.6f), (int)neck, (int)plinth, pal.Ramp[4]);
        }

        static void BrokenDrum(Grid g, float r, bool cracked, System.Random rng, float H)
        {
            var pal = _saltPal ? (cracked ? SaltCracked : Salt) : _skyPal ? (cracked ? MarbleCracked : Marble) : cracked ? Cracked : Stone;
            float plinth = Mathf.Max(8f, r * 0.2f);
            float shaftR = r * 0.8f;
            int side = rng.NextDouble() < 0.5 ? -1 : 1;

            // The break: a slant across the shaft plus a ragged walk, steeper and rougher cracked.
            int n = (int)shaftR + 2;
            var noise = new float[2 * n + 1];
            float v = 0f;
            for (int i = 0; i < noise.Length; i++)
            {
                v = Mathf.Clamp(v + Range(rng, -1.6f, 1.6f), -3f, 3f);
                noise[i] = v * (cracked ? 2f : 1f);
            }
            float slope = shaftR * (cracked ? 0.55f : 0.32f) * side;
            float Jag(float u) => slope * u + noise[Mathf.Clamp(Mathf.RoundToInt(u * shaftR) + n, 0, noise.Length - 1)];

            g.Cylinder(r, 0f, plinth, pal);
            g.Cylinder(shaftR, plinth, H, pal, flutes: r > 40f ? 5 : 4, seams: new[] { plinth + (H - plinth) * 0.5f },
                       jag: Jag, fracture: rng);
            if (cracked)
            {
                g.ChipSide(plinth + (H - plinth) * 0.45f, r * 0.18f, -side, rng);
                g.Rubble(rng, r, pal, 5);
            }
            else g.Rubble(rng, r, pal, 2);
            g.Outline();
            if (cracked)
                g.Fissures(rng, 3, (int)(-shaftR * 0.7f), (int)(shaftR * 0.6f), (int)(H - Mathf.Abs(slope)), (int)plinth, pal.Ramp[4]);
        }

        static float Range(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>Texels keyed by (x, y): x right, y UP from the footprint's centre.</summary>
        sealed partial class Grid
        {
            readonly Dictionary<Vector2Int, Color> _px = new();
            readonly HashSet<Vector2Int> _rubble = new();

            void Put(int x, int y, Color c) => _px[new Vector2Int(x, y)] = c;
            bool Has(int x, int y) => _px.ContainsKey(new Vector2Int(x, y));

            static Color Tone(in Pal pal, float u, int shift)
            {
                float l = -0.5f * u + 0.866f * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
                return pal.Ramp[Mathf.Clamp(Mathf.FloorToInt(l * 5f) + shift, 0, 4)];
            }

            /// <summary>
            /// An upright drum from height y0 to y1 above the ground: the curved face (its bottom and
            /// top edges follow the near half of the flattened circle) and the elliptical top over it.
            /// Drawn bottom up, each part covering the cap of the one below.
            /// </summary>
            public void Cylinder(float r, float y0, float y1, in Pal pal, int flutes = 0, float[] seams = null,
                                 bool underline = false, System.Func<float, float> jag = null,
                                 System.Random fracture = null)
            {
                int n = Mathf.CeilToInt(r);
                for (int tx = -n; tx < n; tx++)
                {
                    float u = (tx + 0.5f) / r;
                    if (Mathf.Abs(u) >= 1f) continue;
                    float dy = r * Squash * Mathf.Sqrt(1f - u * u);
                    float top = y1 + (jag != null ? jag(u) : 0f);
                    int bottom = Mathf.FloorToInt(y0 - dy);
                    for (int ty = bottom; ty < Mathf.CeilToInt(top - dy); ty++)
                    {
                        int shift = 0;
                        if (flutes > 0 && Mathf.Abs(u) < 0.93f && (tx + 400) % flutes == 0) shift--;   // a flute's groove
                        if (seams != null)
                            foreach (float s in seams)
                                if (Mathf.Abs(ty - (s - dy)) < 0.5f) shift--;                             // a drum's joint
                        Put(tx, ty, underline && ty == bottom ? Line : Tone(pal, u, shift));
                    }
                    for (int ty = Mathf.FloorToInt(top - dy); ty < Mathf.CeilToInt(top + dy); ty++)
                    {
                        float yy = ty + 0.5f - top;
                        if (Mathf.Abs(yy) > dy) continue;
                        Color c = pal.Top;
                        if (yy < -dy + 2.2f) c = pal.TopLight;        // the near lip catches the light
                        else if (yy > dy - 2.2f) c = pal.Rim;         // the far rim, as the walls' north edge
                        if (fracture != null && fracture.NextDouble() < 0.22)
                            c = Color.Lerp(c, fracture.NextDouble() < 0.6 ? pal.Seam : pal.TopLight, 0.8f);
                        Put(tx, ty, c);
                    }
                }
            }

            /// <summary>A ragged bite: a circle whose radius wanders round it.</summary>
            void Bite(float cx, float cy, float rad, System.Random rng)
            {
                var k = new float[9];
                for (int i = 0; i < 9; i++) k[i] = Range(rng, 0.55f, 1.15f);
                var gone = new List<Vector2Int>();
                foreach (var p in _px.Keys)
                {
                    float a = (Mathf.Atan2(p.y - cy, p.x - cx) + Mathf.PI) / (2f * Mathf.PI) * 9f;
                    int i = Mathf.FloorToInt(a) % 9;
                    float f = a - Mathf.Floor(a);
                    float rr = rad * (k[i] * (1f - f) + k[(i + 1) % 9] * f);
                    if ((p.x - cx) * (p.x - cx) + (p.y - cy) * (p.y - cy) < rr * rr) gone.Add(p);
                }
                foreach (var p in gone) _px.Remove(p);
            }

            /// <summary>A chip out of the TOP edge above column x - centred just outside it, so it
            /// notches the silhouette rather than punching a hole.</summary>
            public void ChipTop(float x, float rad, System.Random rng)
            {
                int col = (int)x, top = int.MinValue;
                foreach (var p in _px.Keys) if (p.x == col && !_rubble.Contains(p) && p.y > top) top = p.y;
                if (top != int.MinValue) Bite(x, top + rad * 0.3f, rad, rng);
            }

            /// <summary>A chip out of the left (side -1) or right (+1) edge at row y.</summary>
            public void ChipSide(float y, float rad, int side, System.Random rng)
            {
                int row = (int)y, edge = side > 0 ? int.MinValue : int.MaxValue;
                foreach (var p in _px.Keys)
                    if (p.y == row && !_rubble.Contains(p))
                        edge = side > 0 ? Mathf.Max(edge, p.x) : Mathf.Min(edge, p.x);
                if (edge != int.MinValue && edge != int.MaxValue) Bite(edge + side * rad * 0.35f, y, rad, rng);
            }

            /// <summary>Stones fallen in front of the foot.</summary>
            public void Rubble(System.Random rng, float r, in Pal pal, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    float a = Range(rng, Mathf.PI * 1.05f, Mathf.PI * 1.95f);
                    float d = r * Range(rng, 1f, 1.35f);
                    float cx = d * Mathf.Cos(a), cy = d * Squash * Mathf.Sin(a);
                    float s = Range(rng, 3f, 6.5f);
                    for (int x = (int)(cx - s); x <= (int)(cx + s); x++)
                    for (int y = (int)(cy - s); y <= (int)(cy + s); y++)
                    {
                        float ex = (x - cx) / s, ey = (y - cy) / (s * 0.75f);
                        if (ex * ex + ey * ey >= 1f || Has(x, y)) continue;
                        _rubble.Add(new Vector2Int(x, y));
                        Put(x, y, y > cy + s * 0.15f ? pal.TopLight : pal.Ramp[1]);
                    }
                }
            }

            /// <summary>Two texels of line round everything that faces open floor - WallArt draws two.</summary>
            public void Outline()
            {
                var first = Edge();
                var held = new Dictionary<Vector2Int, Color>();
                foreach (var p in first) { held[p] = Line; _px.Remove(p); }
                foreach (var p in Edge()) _px[p] = Line;
                foreach (var kv in held) _px[kv.Key] = kv.Value;
            }

            List<Vector2Int> Edge()
            {
                var edge = new List<Vector2Int>();
                foreach (var p in _px.Keys)
                    if (!Has(p.x + 1, p.y) || !Has(p.x - 1, p.y) || !Has(p.x, p.y + 1) || !Has(p.x, p.y - 1))
                        edge.Add(p);
                return edge;
            }

            /// <summary>Cracks wandering down the face, a lit texel on their left lip.</summary>
            public void Fissures(System.Random rng, int count, int xMin, int xMax, int yHigh, int yLow, Color lit)
            {
                for (int i = 0; i < count; i++)
                {
                    int x = rng.Next(xMin, xMax + 1);
                    for (int y = yHigh; y > yLow; y--)
                    {
                        if (_px.TryGetValue(new Vector2Int(x, y), out var c) && c != Line)
                        {
                            Put(x, y, Crack);
                            if (_px.TryGetValue(new Vector2Int(x - 1, y), out var l) && l != Line && rng.NextDouble() < 0.5)
                                Put(x - 1, y, lit);
                            if (rng.NextDouble() < 0.3 && _px.TryGetValue(new Vector2Int(x + 1, y), out var rr) && rr != Line)
                                Put(x + 1, y, Crack);
                        }
                        x += rng.Next(4) switch { 0 => -1, 3 => 1, _ => 0 };
                    }
                }
            }

            /// <summary>Pivoted on the footprint's centre - texel (0, 0)'s lower-left corner.</summary>
            public Sprite ToSprite()
            {
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                foreach (var p in _px.Keys)
                {
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                }
                int w = maxX - minX + 1, h = maxY - minY + 1;
                var pixels = new Color[w * h];
                foreach (var kv in _px) pixels[(kv.Key.y - minY) * w + (kv.Key.x - minX)] = kv.Value;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                tex.SetPixels(pixels);
                tex.Apply();
                var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(-minX / (float)w, -minY / (float)h), Ppu);
                s.hideFlags = HideFlags.HideAndDontSave;
                return s;
            }
        }
    }
}
