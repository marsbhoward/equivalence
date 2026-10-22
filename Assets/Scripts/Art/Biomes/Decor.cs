using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// SET DRESSING: small props scattered over the floor that say where the room is - bones and
    /// candles and a dropped sword in the tower, mushrooms and ferns in the forest, rubble and
    /// feathers on the sky ruin, crystals and bleached skulls on the salt. Purely a picture: no
    /// collider, no rule. FLAT props lie on the ground layer; STANDING ones depth-sort like any
    /// body. Candles carry a small light.
    ///
    /// Placed after the room's hazards (GameBootstrap.BuildFight), from the floor's own seed on a
    /// System.Random, on open floor clear of walls, chasms, pits, columns and the spire; parented
    /// under the hazard root, so a floor's props go with it. Pictures are cached by (kind,
    /// variant), so textures never pile up across floors.
    /// </summary>
    public static class Decor
    {
        const float Ppu = FloorArt.PixelsPerUnit;
        const int Variants = 4;
        static readonly Dictionary<int, Sprite> _cache = new();

        enum Kind
        {
            Bones, Candles, Sword, Banner,                 // tower
            Mushrooms, Fern, Leaves, MossRock,             // forest
            Rubble, Drum, Feathers,                        // sky
            Crystals, Skull, SaltCubes,                    // salt
            Cobweb,                                        // tower corners
        }

        static bool Standing(Kind k) => k is Kind.Candles or Kind.Mushrooms or Kind.Fern or Kind.MossRock
                                           or Kind.Drum or Kind.Crystals or Kind.SaltCubes;

        static Kind[] Flat(BiomeKind b) => b switch
        {
            BiomeKind.Forest => new[] { Kind.Leaves, Kind.Leaves, Kind.Bones },
            BiomeKind.SkyRuin => new[] { Kind.Rubble, Kind.Rubble, Kind.Feathers },
            BiomeKind.SaltFlats => new[] { Kind.Skull, Kind.Bones },
            _ => new[] { Kind.Bones, Kind.Bones, Kind.Sword, Kind.Banner },
        };

        static Kind[] Upright(BiomeKind b) => b switch
        {
            BiomeKind.Forest => new[] { Kind.Mushrooms, Kind.Fern, Kind.Fern, Kind.MossRock },
            BiomeKind.SkyRuin => new[] { Kind.Drum },
            BiomeKind.SaltFlats => new[] { Kind.Crystals, Kind.Crystals, Kind.SaltCubes },
            _ => new[] { Kind.Candles },
        };

        /// <summary>Dress the floor. <paramref name="avoid"/> is a point to keep clear (the spire).</summary>
        public static void Dress(Transform parent, int seed, Vector2 half, Vector2? avoid)
        {
            if (parent == null) return;
            var root = new GameObject("decor").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(seed * 7 + 3);
            var b = Biome.Current;
            var placed = new List<Vector2>();

            int flat = Tuning.Decor.FlatMin + rng.Next(Tuning.Decor.FlatMax - Tuning.Decor.FlatMin + 1);
            int up = Tuning.Decor.StandingMin + rng.Next(Tuning.Decor.StandingMax - Tuning.Decor.StandingMin + 1);
            var flats = Flat(b); var ups = Upright(b);
            for (int i = 0; i < flat; i++) Place(root, flats[rng.Next(flats.Length)], rng, half, avoid, placed);
            for (int i = 0; i < up; i++) Place(root, ups[rng.Next(ups.Length)], rng, half, avoid, placed);

            // Cobwebs in the tower's corners - the room's own corners, where nothing walks.
            if (b == BiomeKind.Tower)
            {
                float inset = Tuning.Decor.CobwebInset;
                foreach (var (sx, sy) in new[] { (-1, 1), (1, 1), (-1, -1), (1, -1) })
                {
                    if (rng.NextDouble() > Tuning.Decor.CobwebChance) continue;
                    var at = new Vector2(sx * (half.x - inset), sy * (half.y - inset));
                    if (!Arena.OnFloor(at, 0.05f)) continue;
                    var sr = Make(root, Kind.Cobweb, (sx > 0 ? 1 : 0) | (sy > 0 ? 2 : 0), at, false);
                    if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.75f);
                }
            }
        }

        static void Place(Transform root, Kind kind, System.Random rng, Vector2 half, Vector2? avoid, List<Vector2> placed)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var p = new Vector2((float)(rng.NextDouble() * 2 - 1) * (half.x - 0.8f), (float)(rng.NextDouble() * 2 - 1) * (half.y - 0.8f));
                if (!Arena.OnFloor(p, 0.35f)) continue;
                if (Hazards.FloorPits.At(p) != null) continue;
                if (avoid.HasValue && (p - avoid.Value).sqrMagnitude < 2.4f * 2.4f) continue;
                if (Physics2D.OverlapCircle(p, 0.55f) != null) continue;   // a column, a wall, the player
                bool crowded = false;
                foreach (var q in placed) if ((q - p).sqrMagnitude < 1.4f * 1.4f) { crowded = true; break; }
                if (crowded) continue;
                placed.Add(p);
                Make(root, kind, rng.Next(Variants), p, rng.NextDouble() < 0.5);
                return;
            }
        }

        static SpriteRenderer Make(Transform root, Kind kind, int variant, Vector2 at, bool flip)
        {
            var sprite = SpriteFor(kind, variant);
            if (sprite == null) return null;
            var go = new GameObject($"decor.{kind}");
            go.transform.SetParent(root, false);
            go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flip && kind != Kind.Cobweb;
            if (Standing(kind)) DepthSorted.Attach(go, 0f, true, sr);
            else sr.sortingOrder = SortingOrders.GroundDecal;
            if (kind == Kind.Candles)
                Lighting.Glow.Attach(go, Tuning.Decor.CandleRadius, Tuning.Decor.CandleColor, Tuning.Decor.CandleIntensity, 0.22f)
                        .Offset = new Vector2(0f, 0.15f);
            return sr;
        }

        // ------------------------------------------------------------------ the pictures

        sealed class Canvas
        {
            public readonly int W, H;
            public readonly Color[] Px;
            public Canvas(int w, int h) { W = w; H = h; Px = new Color[w * h]; }
            public void Put(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) Px[y * W + x] = c; }
            public bool Has(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && Px[y * W + x].a > 0f;
            public void Line(float x0, float y0, float x1, float y1, Color c)
            {
                int n = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) + 1;
                for (int i = 0; i <= n; i++) { float t = i / (float)n; Put(Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), c); }
            }
            public void Disc(float cx, float cy, float rx, float ry, Color c)
            {
                for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Put(x, y, c);
                }
            }
            /// <summary>One texel of dark line round everything drawn - the props' outline.</summary>
            public void Outline(Color line)
            {
                var add = new List<int>();
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (Has(x, y)) continue;
                    if (Has(x + 1, y) || Has(x - 1, y) || Has(x, y + 1) || Has(x, y - 1)) add.Add(y * W + x);
                }
                foreach (int i in add) Px[i] = line;
            }
        }

        static readonly Color Ink = new(0.06f, 0.05f, 0.05f);

        /// <summary>Darker, alpha untouched (Color * float would thin it too).</summary>
        static Color Dim(Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);

        static Sprite SpriteFor(Kind kind, int variant)
        {
            int key = (int)kind * 16 + variant;
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            var rng = new System.Random(key * 7919 + 17);
            Canvas c = kind switch
            {
                Kind.Bones => Bones(rng, new Color(0.84f, 0.80f, 0.70f)),
                Kind.Candles => Candles(rng),
                Kind.Sword => Sword(rng),
                Kind.Banner => Banner(rng),
                Kind.Mushrooms => Mushrooms(rng),
                Kind.Fern => Fern(rng),
                Kind.Leaves => Leaves(rng),
                Kind.MossRock => MossRock(rng),
                Kind.Rubble => Rubble(rng),
                Kind.Drum => Drum(rng),
                Kind.Feathers => Feathers(rng),
                Kind.Crystals => Crystals(rng),
                Kind.Skull => Skull(rng),
                Kind.SaltCubes => SaltCubes(rng),
                Kind.Cobweb => Cobweb(variant),
                _ => null,
            };
            if (c == null) return null;
            var tex = new Texture2D(c.W, c.H, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels(c.Px);
            tex.Apply();
            var pivot = kind == Kind.Cobweb ? new Vector2((variant & 1) != 0 ? 1f : 0f, (variant & 2) != 0 ? 1f : 0f)
                      : Standing(kind) ? new Vector2(0.5f, 0.12f) : new Vector2(0.5f, 0.5f);
            s = Sprite.Create(tex, new Rect(0, 0, c.W, c.H), pivot, Ppu);
            s.hideFlags = HideFlags.HideAndDontSave;
            return _cache[key] = s;
        }

        static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        static Canvas Bones(System.Random r, Color bone)
        {
            var c = new Canvas(36, 24);
            var shade = Dim(bone, 0.72f);
            for (int i = 0; i < 2 + r.Next(2); i++)
            {
                float x0 = R(r, 4, 16), y0 = R(r, 4, 18), a = R(r, 0, Mathf.PI), len = R(r, 10, 17);
                float x1 = x0 + Mathf.Cos(a) * len, y1 = y0 + Mathf.Sin(a) * len * 0.5f;
                c.Line(x0, y0, x1, y1, bone); c.Line(x0, y0 - 1, x1, y1 - 1, shade);
                c.Disc(x0, y0, 2f, 1.6f, bone); c.Disc(x1, y1, 2f, 1.6f, bone);
            }
            if (r.NextDouble() < 0.7)
            {
                float sx = R(r, 22, 28), sy = R(r, 9, 13);
                c.Disc(sx, sy, 5f, 4f, bone);
                c.Disc(sx, sy - 3.5f, 3.2f, 2.2f, bone);                       // the jaw
                c.Disc(sx - 2, sy + 0.5f, 1.2f, 1.2f, Ink); c.Disc(sx + 2, sy + 0.5f, 1.2f, 1.2f, Ink);
                c.Put(Mathf.RoundToInt(sx), Mathf.RoundToInt(sy) - 2, Ink);
                c.Line(sx - 2, sy - 4, sx + 2, sy - 4, shade);
            }
            c.Outline(new Color(0.12f, 0.10f, 0.09f, 0.9f));
            return c;
        }

        static Canvas Candles(System.Random r)
        {
            var c = new Canvas(24, 28);
            var wax = new Color(0.90f, 0.85f, 0.70f); var waxS = new Color(0.70f, 0.64f, 0.50f);
            c.Disc(12, 3.5f, 10f, 3.2f, waxS);                           // the drips pooled on the floor
            int n = 2 + r.Next(3);
            for (int i = 0; i < n; i++)
            {
                int x = 3 + i * 5 + r.Next(2), h = 7 + r.Next(10);
                for (int y = 3; y < 3 + h; y++) { c.Put(x, y, wax); c.Put(x + 1, y, wax); c.Put(x + 2, y, y % 4 == 0 ? waxS : Dim(wax, 0.85f)); }
                c.Put(x + 1, 3 + h, new Color(0.2f, 0.15f, 0.1f));   // the wick
                c.Put(x + 1, 4 + h, new Color(1f, 0.92f, 0.5f)); c.Put(x + 1, 5 + h, new Color(1f, 0.72f, 0.25f));
                c.Put(x, 4 + h, new Color(1f, 0.62f, 0.2f)); c.Put(x + 2, 4 + h, new Color(1f, 0.62f, 0.2f));
            }
            c.Outline(Ink);
            return c;
        }

        static Canvas Sword(System.Random r)
        {
            var c = new Canvas(46, 18);
            var steel = new Color(0.68f, 0.70f, 0.74f); var dark = new Color(0.40f, 0.41f, 0.45f); var edge = new Color(0.86f, 0.88f, 0.92f);
            float a = R(r, -0.2f, 0.2f);
            float x0 = 3, y0 = 9, x1 = 34, y1 = 9 + a * 31;
            c.Line(x0, y0 + 1, x1, y1 + 1, edge); c.Line(x0, y0, x1, y1, steel); c.Line(x0 + 1, y0 - 1, x1, y1 - 1, dark);
            c.Line(x1, y1 - 5, x1, y1 + 5, new Color(0.60f, 0.46f, 0.22f)); c.Line(x1 + 1, y1 - 5, x1 + 1, y1 + 5, new Color(0.44f, 0.32f, 0.15f));
            c.Line(x1 + 2, y1, x1 + 8, y1, new Color(0.30f, 0.20f, 0.12f)); c.Line(x1 + 2, y1 + 1, x1 + 8, y1 + 1, new Color(0.22f, 0.14f, 0.08f));
            c.Disc(x1 + 9, y1 + 0.5f, 1.6f, 1.6f, new Color(0.60f, 0.46f, 0.22f));          // the pommel
            c.Outline(Ink);
            return c;
        }

        static Canvas Banner(System.Random r)
        {
            var c = new Canvas(34, 24);
            Color[] cloths = { new(0.50f, 0.13f, 0.12f), new(0.17f, 0.21f, 0.44f), new(0.25f, 0.31f, 0.17f) };
            var cloth = cloths[r.Next(cloths.Length)]; var fold = Dim(cloth, 0.7f);
            for (int y = 3; y < 21; y++)
            for (int x = 3; x < 31; x++)
            {
                if (x > 22 && r.NextDouble() < (x - 22) * 0.12) continue;   // torn, frayed end
                c.Put(x, y, (x + y / 2) % 9 < 2 ? fold : cloth);
            }
            var gold = new Color(0.82f, 0.68f, 0.30f);
            c.Disc(12, 12, 4f, 4f, gold); c.Disc(12, 12, 2.2f, 2.2f, cloth);              // its device
            c.Line(3, 3, 30, 3, gold); c.Line(3, 20, 24, 20, gold);                         // the trim
            c.Line(1, 2, 1, 21, new Color(0.32f, 0.22f, 0.12f)); c.Line(2, 2, 2, 21, new Color(0.24f, 0.16f, 0.09f));
            c.Outline(new Color(0.08f, 0.05f, 0.05f, 0.9f));
            return c;
        }

        static Canvas Mushrooms(System.Random r)
        {
            var c = new Canvas(26, 22);
            bool red = r.NextDouble() < 0.5;
            var cap = red ? new Color(0.74f, 0.16f, 0.12f) : new Color(0.80f, 0.70f, 0.55f);
            var under = red ? new Color(0.50f, 0.10f, 0.08f) : new Color(0.58f, 0.50f, 0.38f);
            var stem = new Color(0.90f, 0.86f, 0.76f);
            for (int i = 0; i < 2 + r.Next(3); i++)
            {
                float x = R(r, 5, 21), h = R(r, 5, 12), w = R(r, 3, 5.5f);
                c.Line(x, 1, x, h, stem); c.Line(x + 1, 1, x + 1, h, Dim(stem, 0.85f));
                c.Disc(x + 0.5f, h + 0.5f, w, w * 0.45f, under);
                c.Disc(x + 0.5f, h + 1.5f, w, w * 0.55f, cap);
                if (red) { c.Put(Mathf.RoundToInt(x - w * 0.4f), Mathf.RoundToInt(h + 2), Color.white); c.Put(Mathf.RoundToInt(x + w * 0.3f), Mathf.RoundToInt(h + 1.5f), Color.white); }
            }
            c.Outline(Ink);
            return c;
        }

        static Canvas Fern(System.Random r)
        {
            var c = new Canvas(34, 24);
            var leaf = new Color(0.22f, 0.40f, 0.17f); var lit = new Color(0.36f, 0.56f, 0.23f);
            for (int f = 0; f < 6; f++)
            {
                float a = Mathf.Lerp(0.3f, 2.85f, f / 5f) + R(r, -0.12f, 0.12f), len = R(r, 12, 17);
                for (int i = 0; i < len; i++)
                {
                    float t = i / len, x = 17 + Mathf.Cos(a) * i, y = 1 + Mathf.Sin(a) * i * 0.85f - t * t * 4f;
                    int xi = Mathf.RoundToInt(x), yi = Mathf.RoundToInt(y);
                    c.Put(xi, yi, leaf);
                    if (i > 1 && i < len - 1) { int w = Mathf.Max(1, (int)((1f - t) * 3f)); for (int k = 1; k <= w; k++) { c.Put(xi, yi + k, lit); c.Put(xi, yi - k, leaf); } }
                }
            }
            c.Outline(new Color(0.03f, 0.06f, 0.03f));
            return c;
        }

        static Canvas Leaves(System.Random r)
        {
            var c = new Canvas(32, 22);
            Color[] cols = { new(0.62f, 0.34f, 0.12f), new(0.74f, 0.52f, 0.17f), new(0.50f, 0.23f, 0.10f), new(0.38f, 0.44f, 0.17f) };
            for (int i = 0; i < 22; i++)
            {
                float x = R(r, 4, 28), y = R(r, 4, 18);
                if ((x - 16) * (x - 16) / 150f + (y - 11) * (y - 11) / 50f > 1f) continue;   // a heap, not a scatter
                var col = cols[r.Next(cols.Length)];
                c.Disc(x, y, 2.4f, 1.5f, col);
                c.Line(x - 2, y, x + 2, y, Dim(col, 0.7f));
            }
            c.Outline(new Color(0.10f, 0.08f, 0.04f, 0.6f));
            return c;
        }

        static Canvas MossRock(System.Random r)
        {
            var c = new Canvas(28, 22);
            var stone = new Color(0.38f, 0.38f, 0.34f); var dark = new Color(0.24f, 0.24f, 0.22f);
            var moss = new Color(0.24f, 0.40f, 0.16f); var mossLit = new Color(0.34f, 0.52f, 0.22f);
            float w = R(r, 9, 12), h = R(r, 6, 8);
            c.Disc(14, 8, w, h, dark);
            c.Disc(13, 9, w - 1.5f, h - 1.5f, stone);
            c.Disc(12, 12, w - 3f, 3.5f, moss); c.Disc(11, 13, w - 5f, 2f, mossLit);
            c.Outline(Ink);
            return c;
        }

        static Canvas Rubble(System.Random r)
        {
            var c = new Canvas(32, 22);
            var pale = new Color(0.74f, 0.71f, 0.64f); var shade = new Color(0.52f, 0.50f, 0.45f);
            for (int i = 0; i < 4 + r.Next(4); i++)
            {
                float x = R(r, 5, 27), y = R(r, 5, 17), s = R(r, 2f, 4.5f);
                c.Disc(x, y, s, s * 0.75f, shade); c.Disc(x - 0.6f, y + 0.6f, s * 0.75f, s * 0.5f, pale);
            }
            c.Outline(new Color(0.2f, 0.19f, 0.17f));
            return c;
        }

        static Canvas Drum(System.Random r)
        {
            // A column drum lying on its side: the round end toward us, fluted side behind.
            var c = new Canvas(40, 26);
            var side = new Color(0.68f, 0.65f, 0.59f); var end = new Color(0.82f, 0.79f, 0.72f);
            for (int x = 10; x < 34; x++) c.Line(x, 4, x, 19, (x % 4 == 0) ? Dim(side, 0.86f) : side);
            c.Line(10, 19, 33, 19, Dim(side, 1.15f));
            c.Disc(10, 11.5f, 6f, 7.5f, end);
            c.Disc(10, 11.5f, 2.8f, 3.6f, Dim(end, 0.9f));
            c.Outline(new Color(0.22f, 0.21f, 0.19f));
            return c;
        }

        static Canvas Feathers(System.Random r)
        {
            var c = new Canvas(28, 18);
            for (int i = 0; i < 2 + r.Next(2); i++)
            {
                float x = R(r, 7, 21), y = R(r, 5, 13), a = R(r, 0, Mathf.PI);
                float dx = Mathf.Cos(a) * 7f, dy = Mathf.Sin(a) * 3.5f;
                c.Line(x - dx, y - dy, x + dx, y + dy, new Color(0.62f, 0.62f, 0.66f));       // the quill
                for (int k = 1; k <= 2; k++)
                {
                    c.Line(x - dx * 0.8f, y - dy * 0.8f + k, x + dx * 0.9f, y + dy * 0.9f + k, new Color(0.96f, 0.96f, 0.98f));
                    c.Line(x - dx * 0.8f, y - dy * 0.8f - k, x + dx * 0.7f, y + dy * 0.7f - k, new Color(0.84f, 0.86f, 0.90f));
                }
            }
            c.Outline(new Color(0.40f, 0.42f, 0.48f, 0.7f));
            return c;
        }

        static Canvas Crystals(System.Random r)
        {
            var c = new Canvas(26, 32);
            var lit = new Color(0.98f, 0.94f, 0.96f); var mid = new Color(0.88f, 0.76f, 0.82f); var dark = new Color(0.66f, 0.52f, 0.60f);
            for (int i = 0; i < 3 + r.Next(3); i++)
            {
                float x = R(r, 7, 19), h = R(r, 10, 27), lean = R(r, -0.25f, 0.25f), wb = R(r, 2.5f, 4f);
                for (int y = 1; y < h; y++)
                {
                    float w = Mathf.Max(0.5f, wb * (1f - y / h) + 0.6f);
                    float cx = x + lean * y;
                    for (int dx = -Mathf.CeilToInt(w); dx <= Mathf.CeilToInt(w); dx++)
                        c.Put(Mathf.RoundToInt(cx) + dx, y, dx < 0 ? lit : dx == 0 ? mid : dark);
                }
            }
            c.Outline(new Color(0.36f, 0.26f, 0.32f));
            return c;
        }

        static Canvas Skull(System.Random r)
        {
            // A bleached beast's skull with its horns, lying on the crust.
            var c = new Canvas(40, 22);
            var bone = new Color(0.94f, 0.92f, 0.86f); var shade = new Color(0.74f, 0.70f, 0.64f);
            c.Disc(20, 11, 7f, 5f, bone);
            c.Disc(20, 6, 3.6f, 3.2f, bone);
            c.Disc(17, 12, 1.6f, 1.6f, Ink); c.Disc(23, 12, 1.6f, 1.6f, Ink);
            c.Put(19, 4, shade); c.Put(21, 4, shade);
            for (int i = 0; i < 12; i++)
            {
                float t = i / 11f, y = 14 + Mathf.Sin(t * 2.6f) * 5f;
                c.Disc(13 - i * 1.0f, y, 1.6f - t, 1.4f - t * 0.8f, shade);
                c.Disc(27 + i * 1.0f, y, 1.6f - t, 1.4f - t * 0.8f, shade);
            }
            c.Outline(new Color(0.42f, 0.36f, 0.36f));
            return c;
        }

        static Canvas SaltCubes(System.Random r)
        {
            var c = new Canvas(26, 22);
            var top = new Color(0.99f, 0.98f, 0.97f); var left = new Color(0.90f, 0.86f, 0.86f); var right = new Color(0.74f, 0.66f, 0.70f);
            for (int i = 0; i < 2 + r.Next(3); i++)
            {
                int x = 3 + r.Next(15), y = 1 + r.Next(6), s = 4 + r.Next(5);
                for (int yy = 0; yy < s; yy++) for (int xx = 0; xx < s; xx++) c.Put(x + xx, y + yy, xx < s / 2 ? left : right);
                for (int yy = 0; yy < 2; yy++) for (int xx = 0; xx < s; xx++) c.Put(x + xx + yy, y + s + yy, top);
            }
            c.Outline(new Color(0.40f, 0.30f, 0.36f));
            return c;
        }

        /// <summary>A cobweb filling a corner: threads from the corner and sagging cross threads.
        /// <paramref name="corner"/> bit 1 = east, bit 2 = north - the web hangs INTO the room.</summary>
        static Canvas Cobweb(int corner)
        {
            const int S = 46;
            var c = new Canvas(S, S);
            var silk = new Color(0.86f, 0.86f, 0.90f, 0.55f);
            bool east = (corner & 1) != 0, north = (corner & 2) != 0;
            int ox = east ? S - 1 : 0, oy = north ? S - 1 : 0, sx = east ? -1 : 1, sy = north ? -1 : 1;
            for (int k = 0; k < 5; k++)
            {
                float a = k / 4f * Mathf.PI * 0.5f;
                c.Line(ox, oy, ox + sx * Mathf.Cos(a) * (S - 2), oy + sy * Mathf.Sin(a) * (S - 2), silk);
            }
            for (int ring = 6; ring < S; ring += 6)
            for (int i = 0; i <= 12; i++)
            {
                float a = i / 12f * Mathf.PI * 0.5f, d = ring - Mathf.Sin(i / 12f * Mathf.PI) * 1.5f;
                c.Put(Mathf.RoundToInt(ox + sx * Mathf.Cos(a) * d), Mathf.RoundToInt(oy + sy * Mathf.Sin(a) * d), silk);
            }
            return c;
        }
    }
}
