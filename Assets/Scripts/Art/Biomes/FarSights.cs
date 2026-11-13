using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// RARE THINGS IN THE DISTANCE (the user's calls, 2026-11-13, on the floor-height mock): each
    /// floor rolls 0 to 2 set pieces from its place's pool - none on Tuning.FarSights.NoneShare of
    /// floors, two on TwoShare - weighted common/rare/very rare, never the same twice on a floor.
    /// They stand out in the void to the west, east or south of the room, never behind the north
    /// wall, and move with the far layers' parallax (Tower and Sky far off; the forest's lake and
    /// the salt's brine on the water's own plane, as their drowned head and giant's hand do).
    /// Some move: a cage sways, an orrery turns, a lantern procession walks the water, a distant
    /// arena flashes with somebody else's fight.
    ///
    /// A picture only, built with the far layers (Ground.Build) and seeded from the floor's
    /// scenery - never UnityEngine.Random. Caves and Peaks have no pool yet (out of the rotation).
    /// </summary>
    public static class FarSights
    {
        const float Ppu = Scenery.Ppu;

        public enum Rarity { Common, Rare, VeryRare }

        /// <summary>One set piece: its name (dev tools, logs), rarity, and how it is drawn.</summary>
        public readonly struct Sight
        {
            public readonly string Name;
            public readonly Rarity Rarity;
            internal readonly System.Action<Pic, Pal, List<Part>> Draw;
            internal Sight(string name, Rarity rarity, System.Action<Pic, Pal, List<Part>> draw) { Name = name; Rarity = rarity; Draw = draw; }
        }

        /// <summary>The place's pool (empty for a place with none).</summary>
        public static Sight[] Pool(BiomeKind place) => place switch
        {
            BiomeKind.Tower => new[]
            {
                new Sight("neighbouring tower", Rarity.Common, Tower),
                new Sight("hanging cage", Rarity.Common, Cage),
                new Sight("broken stair", Rarity.Rare, Stair),
                new Sight("distant arena", Rarity.Rare, DistantArena),
                new Sight("fallen bell", Rarity.VeryRare, Bell),
            },
            BiomeKind.SkyRuin => new[]
            {
                new Sight("floating ruin", Rarity.Common, Ruin),
                new Sight("chained island", Rarity.Common, Chained),
                new Sight("orrery", Rarity.Rare, Orrery),
                new Sight("upturned temple", Rarity.Rare, Temple),
                new Sight("sky whale bones", Rarity.VeryRare, Whale),
            },
            BiomeKind.Forest => new[]
            {
                new Sight("drowned belfry", Rarity.Common, Belfry),
                new Sight("great dead tree", Rarity.Common, GreatTree),
                new Sight("antlered skull", Rarity.Rare, Skull),
                new Sight("sunken gate", Rarity.Rare, SunkenGate),
                new Sight("lantern procession", Rarity.VeryRare, Lanterns),
            },
            BiomeKind.SaltFlats => new[]
            {
                new Sight("stranded ship", Rarity.Common, Ribs),
                new Sight("salt obelisks", Rarity.Common, Obelisks),
                new Sight("buried crown", Rarity.Rare, Crown),
                new Sight("brine geyser", Rarity.Rare, Geyser),
                new Sight("pilgrim caravan", Rarity.VeryRare, Caravan),
            },
            _ => System.Array.Empty<Sight>(),
        };

        /// <summary>The floor's roll: which pieces, and which side each stands on (0 west, 1 east,
        /// 2 south). Pure - the same seed always gives the same answer.</summary>
        public static List<(Sight sight, int side, float u, float v)> Roll(BiomeKind place, int seed)
        {
            var rng = new System.Random(unchecked(seed * 7919 + 13));
            var outp = new List<(Sight, int, float, float)>();
            var pool = new List<Sight>(Pool(place));
            if (pool.Count == 0) return outp;
            double u0 = rng.NextDouble();
            int n = u0 < Tuning.FarSights.NoneShare ? 0 : u0 < 1.0 - Tuning.FarSights.TwoShare ? 1 : 2;
            var sides = new List<int> { 0, 1, 2 };
            for (int i = sides.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (sides[i], sides[j]) = (sides[j], sides[i]); }
            for (int i = 0; i < n && pool.Count > 0; i++)
            {
                float total = 0f;
                foreach (var s in pool) total += Weight(s.Rarity);
                float x = (float)rng.NextDouble() * total;
                int k = 0;
                for (; k < pool.Count - 1; k++) { x -= Weight(pool[k].Rarity); if (x < 0f) break; }
                outp.Add((pool[k], sides[i], (float)rng.NextDouble(), (float)rng.NextDouble()));
                pool.RemoveAt(k);
            }
            return outp;
        }

        static float Weight(Rarity r) => r switch
        {
            Rarity.Common => Tuning.FarSights.WeightCommon,
            Rarity.Rare => Tuning.FarSights.WeightRare,
            _ => Tuning.FarSights.WeightVeryRare,
        };

        /// <summary>DEV, one-shot: the next floor shows every piece of its place's pool (true), at
        /// the three sides in turn - to look at them.</summary>
        public static bool DevShowAll;

        public static void Build(Transform parent, SceneryOwned owned, int seed)
        {
            var place = Biome.Current;
            var roll = Roll(place, seed);
            if (DevShowAll)
            {
                DevShowAll = false;
                roll.Clear();
                var all = Pool(place);
                for (int i = 0; i < all.Length; i++) roll.Add((all[i], i % 3, (i / 3) * 0.6f + 0.2f, 0.25f + 0.5f * (i % 2)));
            }
            if (roll.Count == 0) return;
            var pal = PalFor(place);
            // The water places' pieces stand in the water, on its plane; the others far off.
            bool onWater = place is BiomeKind.Forest or BiomeKind.SaltFlats;
            float depth = onWater ? 1f : Tuning.FarSights.Depth;
            var root = new GameObject("farsights").transform;
            root.SetParent(parent, false);
            foreach (var (sight, side, u, v) in roll)
            {
                var pic = new Pic(Tuning.FarSights.CanvasW, Tuning.FarSights.CanvasH);
                var parts = new List<Part>();
                sight.Draw(pic, pal, parts);
                // Where it would be seen with the camera near that side's edge (Parallax's anchor).
                Vector2 seen = side switch
                {
                    0 => new Vector2(-Mathf.Lerp(14.5f, 18.5f, u), Mathf.Lerp(-8.5f, 2f, v)),
                    1 => new Vector2(Mathf.Lerp(14.5f, 18.5f, u), Mathf.Lerp(-8.5f, 2f, v)),
                    _ => new Vector2(Mathf.Lerp(-7f, 7f, u), Mathf.Lerp(-12.5f, -10.5f, v)),
                };
                Vector2 cam = new(side == 0 ? -8f : side == 1 ? 8f : 0f, side == 2 ? -3f : 0f);
                var at = depth >= 1f ? seen : cam + (seen - cam) / depth;
                var go = new GameObject($"farsight.{sight.Name}");
                go.transform.SetParent(root, false);
                go.transform.position = at;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = pic.Make(owned, new Vector2(0.5f, 0f));
                sr.sortingOrder = onWater ? Tuning.Ground.Order + 2 : Tuning.Ground.VoidOrder + 4;
                var p = go.AddComponent<Parallax>();
                p.Depth = depth;
                p.Anchor = at;
                if (depth >= 1f) p.enabled = false;
                if (parts.Count > 0)
                {
                    var motion = go.AddComponent<FarSightMotion>();
                    foreach (var part in parts) motion.Add(part.Build(go.transform, owned, sr.sortingOrder + 1));
                }
                Debug.Log($"[FarSights] {sight.Name} ({sight.Rarity}) {(side == 0 ? "west" : side == 1 ? "east" : "south")}");
            }
        }

        // ------------------------------------------------------------------ palettes

        internal readonly struct Pal
        {
            public readonly Color Sil, Sil2, Lit, Void;
            public Pal(Color sil, Color sil2, Color lit, Color v) { Sil = sil; Sil2 = sil2; Lit = lit; Void = v; }
        }

        static Pal PalFor(BiomeKind place)
        {
            var stone = Scenery.WallStone;
            return place switch
            {
                BiomeKind.SkyRuin => new Pal(new Color(0.48f, 0.56f, 0.68f), new Color(0.60f, 0.67f, 0.78f), new Color(1f, 0.95f, 0.78f), new Color(0.62f, 0.74f, 0.86f)),
                BiomeKind.Forest => new Pal(new Color(0.26f, 0.29f, 0.24f), new Color(0.38f, 0.42f, 0.34f), new Color(0.88f, 0.96f, 0.56f), new Color(0.05f, 0.09f, 0.08f)),
                BiomeKind.SaltFlats => new Pal(new Color(0.70f, 0.62f, 0.66f), new Color(0.88f, 0.84f, 0.84f), new Color(1f, 0.86f, 0.64f), new Color(0.32f, 0.47f, 0.52f)),
                // The edge's dark lies over the void (Biome.EdgeDark), so the tower's pieces are
                // drawn well above the shaft's own stone to read through it, as its windows are.
                _ => new Pal(new Color(stone.r * 1.9f, stone.g * 1.9f, stone.b * 2.1f), new Color(stone.r * 2.5f, stone.g * 2.5f, stone.b * 2.7f),
                             new Color(1f, 0.84f, 0.58f), new Color(0.04f, 0.035f, 0.05f)),
            };
        }

        // ------------------------------------------------------------------ the canvas

        /// <summary>A texel canvas, y up, its base at the bottom centre. Drawn at the scenery's
        /// density, in shapes twice the mock's (Tuning.FarSights.Scale).</summary>
        internal sealed class Pic
        {
            public readonly int W, H;
            public readonly Color32[] Px;
            public Pic(int w, int h) { W = w; H = h; Px = new Color32[w * h]; }
            static int S => Tuning.FarSights.Scale;
            /// <summary>A texel at mock coordinates (x from the centre, y up from the base).</summary>
            public void Dot(float x, float y, Color c, int w = 1, int h = 1)
            {
                int x0 = Mathf.RoundToInt(W * 0.5f + x * S), y0 = Mathf.RoundToInt(y * S);
                for (int j = 0; j < h * S; j++)
                for (int i = 0; i < w * S; i++)
                {
                    int px = x0 + i, py = y0 + j;
                    if (px < 0 || py < 0 || px >= W || py >= H) continue;
                    Px[py * W + px] = c;
                }
            }
            public void Rect(float x, float y, float w, float h, Color c) => Dot(x, y, c, Mathf.Max(1, Mathf.RoundToInt(w)), Mathf.Max(1, Mathf.RoundToInt(h)));
            public void Line(float x0, float y0, float x1, float y1, Color c)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * S));
                for (int i = 0; i <= n; i++)
                {
                    float t = i / (float)n;
                    int px = Mathf.RoundToInt(W * 0.5f + Mathf.Lerp(x0, x1, t) * S), py = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t) * S);
                    for (int dy = 0; dy < S; dy++) for (int dx = 0; dx < S; dx++)
                    {
                        int qx = px + dx, qy = py + dy;
                        if (qx >= 0 && qy >= 0 && qx < W && qy < H) Px[qy * W + qx] = c;
                    }
                }
            }
            public void Ell(float cx, float cy, float rx, float ry, Color c)
            {
                for (float dy = -ry; dy <= ry; dy += 1f / S)
                {
                    float w = rx * Mathf.Sqrt(Mathf.Max(0f, 1f - dy * dy / (ry * ry)));
                    for (float dx = -w; dx <= w; dx += 1f / S)
                    {
                        int px = Mathf.RoundToInt(W * 0.5f + (cx + dx) * S), py = Mathf.RoundToInt((cy + dy) * S);
                        if (px >= 0 && py >= 0 && px < W && py < H) Px[py * W + px] = c;
                    }
                }
            }
            public void Ring(float cx, float cy, float rx, float ry, Color c)
            {
                for (float a = 0f; a < Mathf.PI * 2f; a += 0.5f / (Mathf.Max(rx, ry) * S))
                {
                    int px = Mathf.RoundToInt(W * 0.5f + (cx + Mathf.Cos(a) * rx) * S), py = Mathf.RoundToInt((cy + Mathf.Sin(a) * ry) * S);
                    if (px >= 0 && py >= 0 && px < W && py < H) Px[py * W + px] = c;
                }
            }
            public Sprite Make(SceneryOwned owned, Vector2 pivot)
            {
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixels32(Px);
                tex.Apply();
                var s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, W, H), pivot, Ppu, 0, SpriteMeshType.FullRect);
                s.hideFlags = HideFlags.HideAndDontSave;
                if (owned != null) { owned.Add(tex); owned.Add(s); }
                return s;
            }
        }

        // ------------------------------------------------------------------ moving parts

        internal enum Move { Bob, Sway, Pulse, Orbit, March, Blink, Rise }

        /// <summary>A small piece drawn on its own canvas and moved by <see cref="FarSightMotion"/>.</summary>
        internal sealed class Part
        {
            public Pic Pic;
            public Vector2 At;           // mock units from the base centre
            public Move Move;
            public float A, B, Speed, Phase;
            public bool Whole;            // moves the whole piece instead of a part of it
            public FarSightMotion.Anim Build(Transform owner, SceneryOwned owned, int order)
            {
                Transform t = owner;
                SpriteRenderer sr = owner.GetComponent<SpriteRenderer>();
                if (!Whole)
                {
                    var go = new GameObject("part");
                    go.transform.SetParent(owner, false);
                    go.transform.localPosition = At * Tuning.FarSights.Scale / Ppu;
                    sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = Pic.Make(owned, new Vector2(0.5f, 0f));
                    sr.sortingOrder = order;
                    t = go.transform;
                }
                return new FarSightMotion.Anim(t, sr, Move, A * Tuning.FarSights.Scale / Ppu, B * Tuning.FarSights.Scale / Ppu, Speed, Phase, A, B);
            }
        }

        static Part PartOf(int w, int h, System.Action<Pic> draw, Vector2 at, Move move, float a, float b, float speed, float phase)
        {
            var pic = new Pic(w * Tuning.FarSights.Scale, h * Tuning.FarSights.Scale);
            draw(pic);
            return new Part { Pic = pic, At = at, Move = move, A = a, B = b, Speed = speed, Phase = phase };
        }

        static Part WholeMoves(Move move, float a, float speed, float phase)
            => new() { Whole = true, Move = move, A = a, Speed = speed, Phase = phase };

        // ------------------------------------------------------------------ the tower

        static void Tower(Pic p, Pal c, List<Part> parts)
        {
            const int h = 58, w = 12;
            for (int i = 0; i < h; i++) { float ww = w * (0.78f + 0.22f * (1f - i / (float)h)); p.Rect(-ww / 2f, i, ww, 1, c.Sil); }
            for (int i = 0; i < 11; i++) { float ww = w * 0.85f * (1f - i / 11f); p.Rect(-ww / 2f, h + i, ww, 1, c.Sil); }
            p.Rect(-1, h - 2, 2, 2, c.Sil2);
            // The windows: dark here, lit on their own part so they can flicker.
            parts.Add(PartOf(12, 56, q =>
            {
                for (int k = 0; k < 6; k++) if (k != 2) q.Rect(k % 2 == 0 ? -3 : 1, 6 + k * 8, 2, 3, c.Lit);
            }, Vector2.zero, Move.Pulse, 0.45f, 1f, 1.3f, 0f));
        }

        static void Cage(Pic p, Pal c, List<Part> parts)
        {
            // The chain from above; the cage on its own part, swaying.
            for (float y = 34; y < 120; y += 3) p.Rect(0, y, 1, 2, c.Sil);
            parts.Add(PartOf(18, 22, q =>
            {
                for (int i = 0; i < 5; i++) q.Rect(-6 + i * 3, 2, 1, 14, c.Sil);
                q.Rect(-6, 1, 13, 2, c.Sil); q.Rect(-6, 15, 13, 2, c.Sil); q.Ell(0.5f, 17, 4, 2, c.Sil);
                q.Rect(-1, 7, 2, 2, c.Lit);
            }, new Vector2(0f, 14f), Move.Sway, 2.5f, 0f, 0.7f, 0.3f));
        }

        static void Stair(Pic p, Pal c, List<Part> parts)
        {
            for (int i = 0; i < 9; i++) { p.Rect(-18 + i * 4, 6 + i * 5, 7, 3, c.Sil); p.Rect(-18 + i * 4, 6 + i * 5, 7, 1, c.Sil2); }
            p.Rect(-16, 0, 2, 9, c.Sil); p.Rect(-2, 0, 2, 26, c.Sil); p.Rect(12, 32, 2, 12, c.Sil);
            p.Rect(18, 52, 3, 2, c.Sil); p.Rect(22, 55, 2, 1, c.Sil);
        }

        static void DistantArena(Pic p, Pal c, List<Part> parts)
        {
            for (int i = 0; i < 9; i++) { float w = 22f * (1f - i / 10f); p.Rect(-w, 12 - i, 2 * w, 1, c.Sil); }
            p.Ell(0, 16, 22, 5, c.Sil2);
            // Somebody else's fight: three figures stepping in and out, and the flashes of it.
            for (int k = 0; k < 3; k++)
                parts.Add(PartOf(3, 4, q => q.Rect(-1, 0, 2, 3, k == 0 ? Color.Lerp(c.Sil, Color.white, 0.3f) : c.Sil),
                                 new Vector2(-10 + k * 9, 16 + (k & 1) * 2), Move.Sway, 3f, 0f, 1.6f, k * 1.7f));
            for (int k = 0; k < 3; k++)
                parts.Add(PartOf(5, 5, q => { q.Rect(-0.5f, 0, 1, 3, c.Lit); q.Rect(-1.5f, 1, 3, 1, c.Lit); },
                                 new Vector2(-8 + k * 8, 20), Move.Blink, 3f, 0.25f, 1f, k * 0.9f));
        }

        static void Bell(Pic p, Pal c, List<Part> parts)
        {
            for (int i = 0; i < 22; i++) { float u = i / 22f, w = 7 + u * u * 12; p.Rect(-w, 2 + (21 - i), 2 * w, 1, c.Sil); }
            p.Ell(0, 25, 6, 3, c.Sil); p.Rect(-1, 27, 3, 4, c.Sil);
            p.Line(3, 20, 1, 12, c.Void); p.Line(1, 12, 4, 5, c.Void);
            p.Rect(-21, 0, 42, 2, c.Sil2); p.Rect(-15, 2, 4, 2, c.Sil); p.Rect(10, 2, 5, 2, c.Sil);
            p.Rect(-8, 10, 1, 10, Color.Lerp(c.Sil, c.Lit, 0.25f));
        }

        // ------------------------------------------------------------------ the sky

        static void Island(Pic p, Pal c, float top, float w, float h)
        {
            for (int i = 0; i < h; i++) { float ww = w * Mathf.Pow(1f - i / h, 0.9f); p.Rect(-ww / 2f, top - i, ww, 1, i < 2 ? c.Sil2 : c.Sil); }
        }

        static void Ruin(Pic p, Pal c, List<Part> parts)
        {
            Island(p, c, 34, 34, 16);
            for (int k = 0; k < 4; k++) if (k != 2) p.Rect(-12 + k * 8, 35, 2, 12, c.Sil);
            p.Rect(-13, 47, 14, 2, c.Sil);
            for (int i = 0; i < 24; i++) p.Rect(11, 31 - i, 1, 1, new Color(1f, 1f, 1f, 0.6f * (1f - i / 24f)));
            parts.Add(WholeMoves(Move.Bob, 1.5f, 0.5f, 0f));
        }

        static void Chained(Pic p, Pal c, List<Part> parts)
        {
            for (float y = 30; y < 120; y += 3) { p.Rect(-10, y, 1, 2, c.Sil); p.Rect(10, y, 1, 2, c.Sil); }
            Island(p, c, 28, 28, 13);
            p.Rect(-4, 29, 8, 7, c.Sil); p.Rect(-1, 31, 2, 3, c.Lit);
            parts.Add(WholeMoves(Move.Bob, 1.5f, 0.45f, 1f));
        }

        static void Orrery(Pic p, Pal c, List<Part> parts)
        {
            p.Ring(0, 32, 21, 6, c.Sil); p.Ring(0, 32, 13, 11, c.Sil); p.Ell(0, 32, 3, 3, c.Sil);
            for (int k = 0; k < 3; k++)
                parts.Add(PartOf(4, 4, q => q.Ell(0, 1.5f, 1.5f, 1.5f, k == 0 ? c.Lit : c.Sil2),
                                 new Vector2(0f, 30.5f), Move.Orbit, k == 1 ? 13f : 21f, k == 1 ? 11f : 6f, 0.5f + k * 0.2f, k * 2.1f));
        }

        static void Temple(Pic p, Pal c, List<Part> parts)
        {
            Island(p, c, 52, 26, 10);
            for (int k = 0; k < 4; k++) p.Rect(-9 + k * 6, 28, 2, 14, c.Sil);
            p.Rect(-11, 26, 22, 2, c.Sil);
            for (int i = 0; i < 6; i++) { float w = 22 - i * 3.6f; p.Rect(-w / 2f, 25 - i, w, 1, c.Sil); }
            parts.Add(WholeMoves(Move.Bob, 1.5f, 0.4f, 2f));
        }

        static void Whale(Pic p, Pal c, List<Part> parts)
        {
            for (int i = -26; i <= 26; i++) p.Rect(i, 30 + Mathf.Cos(i / 26f * 1.4f) * 6, 1, 1, c.Sil);
            for (int k = -3; k <= 3; k++)
            {
                float rx = k * 6, ry = 30 + Mathf.Cos(k * 6 / 26f * 1.4f) * 6;
                p.Line(rx, ry, rx - 2, ry - 10, c.Sil); p.Line(rx - 2, ry - 10, rx + 1, ry - 16, c.Sil);
            }
            p.Ell(-31, 31, 5, 4, c.Sil); p.Line(26, 34, 33, 39, c.Sil);
            parts.Add(WholeMoves(Move.Sway, 4f, 0.2f, 0f));
        }

        // ------------------------------------------------------------------ the forest's lake

        static void Reflect(Pic p, Pal c, float w, int rows)
        {
            // Under the waterline (mock y 14), every other row, fading.
            for (int i = 0; i < rows; i += 2) p.Rect(-w / 2f + ((i & 2) != 0 ? 1 : 0), 13 - i, w, 1, new Color(c.Sil.r, c.Sil.g, c.Sil.b, 0.45f * (1f - i / (float)rows)));
        }

        static void Belfry(Pic p, Pal c, List<Part> parts)
        {
            p.Rect(-6, 14, 12, 30, c.Sil);
            for (int i = 0; i < 12; i++) { float w = 12f * (1f - i / 12f); p.Rect(-w / 2f, 44 + i, w, 1, c.Sil); }
            p.Rect(-3, 30, 6, 7, c.Void);
            Reflect(p, c, 12, 14);
            parts.Add(PartOf(6, 6, q => q.Rect(-2, 0, 4, 4, c.Lit), new Vector2(0, 31), Move.Pulse, 0.2f, 0.6f, 0.8f, 0f));
        }

        static void GreatTree(Pic p, Pal c, List<Part> parts)
        {
            for (int i = 0; i < 56; i++) { float w = 7 - i / 56f * 4; p.Rect(-w / 2f, 14 + i, w, 1, c.Sil); }
            p.Line(0, 52, -14, 66, c.Sil); p.Line(-8, 60, -10, 72, c.Sil); p.Line(0, 60, 12, 74, c.Sil);
            p.Line(6, 66, 16, 68, c.Sil); p.Line(0, 70, 2, 80, c.Sil);
            Reflect(p, c, 6, 14);
        }

        static void Skull(Pic p, Pal c, List<Part> parts)
        {
            p.Ell(0, 23, 8, 6, c.Sil); p.Rect(-4, 14, 8, 5, c.Sil);
            p.Rect(-4, 22, 3, 3, c.Void); p.Rect(2, 22, 3, 3, c.Void);
            foreach (int s in new[] { -1, 1 })
            {
                p.Line(s * 6, 28, s * 16, 44, c.Sil); p.Line(s * 11, 36, s * 20, 38, c.Sil);
                p.Line(s * 14, 41, s * 12, 50, c.Sil); p.Line(s * 16, 44, s * 22, 50, c.Sil);
            }
            Reflect(p, c, 10, 12);
            parts.Add(PartOf(10, 3, q => { q.Rect(-3, 0, 1, 1, c.Lit); q.Rect(3, 0, 1, 1, c.Lit); }, new Vector2(0, 23), Move.Pulse, 0.2f, 0.75f, 1.5f, 0f));
        }

        static void SunkenGate(Pic p, Pal c, List<Part> parts)
        {
            p.Rect(-14, 14, 5, 26, c.Sil); p.Rect(9, 14, 5, 26, c.Sil);
            for (float a = 0f; a <= Mathf.PI; a += 0.05f) p.Rect(Mathf.Cos(a) * 11.5f - 2, 40 + Mathf.Sin(a) * 10, 5, 3, c.Sil);
            Reflect(p, c, 28, 12);
            parts.Add(PartOf(30, 3, q => { q.Rect(-12, 0, 5, 1, c.Lit); q.Rect(4, 1, 6, 1, c.Lit); },
                             new Vector2(0, 13), Move.Sway, 3f, 0f, 0.6f, 0f));
        }

        static void Lanterns(Pic p, Pal c, List<Part> parts)
        {
            // A causeway just under the water, and the procession walking it.
            p.Rect(-36, 13, 72, 1, Color.Lerp(c.Sil, c.Void, 0.4f));
            for (int k = 0; k < 7; k++)
                parts.Add(PartOf(5, 12, q => { q.Rect(-1, 0, 2, 5, c.Sil); q.Rect(0, 7, 3, 3, new Color(c.Lit.r, c.Lit.g, c.Lit.b, 0.25f)); q.Rect(1, 8, 1, 1, c.Lit); },
                                 new Vector2(-36, 14), Move.March, 72f, 0f, 4f, k * 10f));
        }

        // ------------------------------------------------------------------ the salt's brine

        static void Ribs(Pic p, Pal c, List<Part> parts)
        {
            p.Rect(-26, 12, 52, 2, c.Sil);
            for (int k = 0; k < 7; k++)
            {
                float rx = -22 + k * 7, h = (k == 2 ? 9 : 16) + Mathf.Sin(k / 6f * Mathf.PI) * 10;
                for (int i = 0; i < h; i++) p.Rect(rx + Mathf.Round(Mathf.Pow(i / h, 2f) * 4 * (k < 3 ? 1 : -1)), 14 + i, 2, 1, c.Sil);
            }
            Reflect(p, c, 50, 10);
        }

        static void Obelisks(Pic p, Pal c, List<Part> parts)
        {
            foreach (var (x, h) in new[] { (-11, 30), (0, 46), (10, 24) })
            {
                p.Rect(x - 2, 14, 4, h - 3, c.Sil); p.Rect(x - 1, 14 + h - 3, 2, 3, c.Sil); p.Rect(x + 1, 14, 1, h - 3, c.Sil2);
            }
            Reflect(p, c, 26, 10);
        }

        static void Crown(Pic p, Pal c, List<Part> parts)
        {
            p.Rect(-14, 14, 28, 6, c.Sil);
            for (int k = 0; k < 5; k++) for (int i = 0; i < 8; i++) p.Rect(-13 + k * 6 + i * 0.25f, 20 + i, Mathf.Max(1f, 3 - i * 0.3f), 1, c.Sil);
            Reflect(p, c, 28, 10);
            parts.Add(PartOf(26, 3, q => { for (int k = 0; k < 4; k++) q.Rect(-10 + k * 6, 0, 1, 1, c.Lit); }, new Vector2(0, 16), Move.Pulse, 0.3f, 0.95f, 2f, 0f));
        }

        static void Geyser(Pic p, Pal c, List<Part> parts)
        {
            p.Ell(0, 14, 10, 2, c.Sil2);
            parts.Add(PartOf(10, 44, q =>
            {
                for (int i = 0; i < 44; i++)
                {
                    float w = 3 + Mathf.Sin(i * 0.7f) * 1.5f + i * 0.09f;
                    q.Rect(-w / 2f, i, w, 1, new Color(1f, 1f, 1f, 0.55f * (1f - i / 44f) + 0.1f));
                }
            }, new Vector2(0, 14), Move.Rise, 0.3f, 1f, 1.2f, 0f));
        }

        static void Caravan(Pic p, Pal c, List<Part> parts)
        {
            for (int k = 0; k < 5; k++)
            {
                bool lead = k == 4;
                parts.Add(PartOf(12, 12, q =>
                {
                    q.Rect(-3, 3, 5, 3, c.Sil); q.Rect(2, 5, 2, 3, c.Sil); q.Rect(-3, 0, 1, 3, c.Sil); q.Rect(1, 0, 1, 3, c.Sil);
                    if (lead) { q.Rect(4, 8, 3, 3, new Color(c.Lit.r, c.Lit.g, c.Lit.b, 0.25f)); q.Rect(5, 9, 1, 1, c.Lit); }
                }, new Vector2(-40, 14), Move.March, 80f, 0f, 2.5f, k * 9f));
            }
            Reflect(p, c, 60, 6);
        }
    }

    /// <summary>The far set pieces' moving parts (FarSights), on the floor's texel grid so nothing
    /// swims: a bob, a sway, a pulse of light, an orbit, a march across and round again, a blink,
    /// a rising spray. Scaled time - a pause holds them.</summary>
    public class FarSightMotion : MonoBehaviour
    {
        public readonly struct Anim
        {
            public readonly Transform T;
            public readonly SpriteRenderer Sr;
            internal readonly FarSights.Move Move;
            public readonly float A, B, Speed, Phase, RawA, RawB;
            public readonly Vector3 Home;
            internal Anim(Transform t, SpriteRenderer sr, FarSights.Move move, float a, float b, float speed, float phase, float rawA, float rawB)
            {
                T = t; Sr = sr; Move = move; A = a; B = b; Speed = speed; Phase = phase; RawA = rawA; RawB = rawB;
                Home = t.localPosition;
            }
        }

        // Not readonly: a domain reload brings it back null (the parts then simply stand still).
        List<Anim> _anims = new();
        Vector3 _rootHome;
        bool _rootSet;

        internal void Add(Anim a)
        {
            _anims ??= new List<Anim>();
            if (a.T == transform && !_rootSet) { _rootHome = transform.localPosition; _rootSet = true; }
            _anims.Add(a);
        }

        void Update()
        {
            if (_anims == null) return;
            float t = Time.time, ppu = Scenery.Ppu;
            float Snap(float v) => Mathf.Round(v * ppu) / ppu;
            foreach (var a in _anims)
            {
                if (a.T == null) continue;
                float s = t * a.Speed + a.Phase;
                switch (a.Move)
                {
                    case FarSights.Move.Bob:
                        Offset(a, new Vector3(0f, Snap(Mathf.Sin(s) * a.A), 0f));
                        break;
                    case FarSights.Move.Sway:
                        Offset(a, new Vector3(Snap(Mathf.Sin(s) * a.A), 0f, 0f));
                        break;
                    case FarSights.Move.Pulse:
                        if (a.Sr != null) { var c = a.Sr.color; c.a = Mathf.Lerp(a.RawA, a.RawB, Mathf.Sin(s) * 0.5f + 0.5f); a.Sr.color = c; }
                        break;
                    case FarSights.Move.Orbit:
                        Offset(a, new Vector3(Snap(Mathf.Cos(s) * a.A), Snap(Mathf.Sin(s) * a.B), 0f));
                        break;
                    case FarSights.Move.March:
                    {
                        float span = a.A;
                        float x = Mathf.Repeat(a.Phase * span / a.RawA + t * a.Speed * Tuning.FarSights.Scale / ppu, span);
                        Offset(a, new Vector3(Snap(x), 0f, 0f));
                        break;
                    }
                    case FarSights.Move.Blink:
                        if (a.Sr != null) a.Sr.enabled = Mathf.Repeat(t * a.Speed + a.Phase, a.RawA) < a.RawB;
                        break;
                    case FarSights.Move.Rise:
                    {
                        float k = Mathf.Lerp(a.RawA, a.RawB, Mathf.Sin(s) * 0.5f + 0.5f);
                        a.T.localScale = new Vector3(1f, Mathf.Round(k * 20f) / 20f, 1f);
                        break;
                    }
                }
            }
        }

        /// <summary>A part moves on its own transform; a whole piece cannot (Parallax owns its
        /// position), so it is shifted through its sprite's pivot instead.</summary>
        void Offset(in Anim a, Vector3 by)
        {
            if (a.T != transform) { a.T.localPosition = a.Home + by; return; }
            // The whole piece: Parallax writes its position each LateUpdate, after this - the
            // offset rides along as its Shift.
            var p = GetComponent<Parallax>();
            if (p != null && p.enabled) { p.Shift = (Vector2)by; return; }
            transform.localPosition = _rootHome + by;
        }
    }
}
