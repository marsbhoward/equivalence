using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// WHAT LIES PAST THE LIP (the user's call, 2026-11-01): each place's void has depth - layers
    /// that drift slower than the room as the camera pans (<see cref="Parallax"/>), and the
    /// place's own landmarks out in it.
    ///
    ///   TOWER   the shaft the tower stands round: its far walls going down, the lit windows of the
    ///           floors below (the floors already climbed).
    ///   SKY     clouds in two layers, the far ones slower; floating islands with their waterfalls.
    ///   FOREST  the lake runs on: dead trees standing in it, a drowned stone head.
    ///   SALT    the brine runs on: salt spires rising out of it, a giant's hand half sunk.
    ///   CAVES   the abyss: the glow of fungus and crystal far below, a far wall of hanging stone;
    ///           crystals grow at the lip itself, and they are the room's light.
    ///   PEAKS   the valley far below - forest and a river under drifting cloud - and ranges of
    ///           peaks beyond it.
    ///
    /// A picture only. Built with the ground (Ground.Build), under the floor's Scenery object;
    /// placed from the floor's seed. All of it under the ground: the light field's edge dark lies
    /// over it, so the far windows are drawn bright enough to glow through it.
    /// </summary>
    public static class FarLayers
    {
        const float Ppu = Scenery.Ppu;

        public static void Build(Transform parent, SceneryOwned owned, int seed)
        {
            var root = new GameObject("far").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(seed * 19 + 4);
            switch (Biome.Current)
            {
                case BiomeKind.Tower: Shaft(root, owned, rng); break;
                case BiomeKind.SkyRuin: Sky(root, owned, rng); break;
                case BiomeKind.Forest: Lake(root, owned, rng); break;
                case BiomeKind.SaltFlats: Brine(root, owned, rng); break;
                case BiomeKind.Caves: Abyss(root, owned, rng); break;
                case BiomeKind.SnowyPeaks: Valley(root, owned, rng); break;
            }
        }

        // ------------------------------------------------------------------ the caves' abyss

        static void Abyss(Transform root, SceneryOwned owned, System.Random rng)
        {
            // Far below: points of light, teal and violet, in loose drifts.
            const int W = 256, H = 256;
            var glow = new Color32[W * H];
            int seed = rng.Next();
            for (int k = 0; k < 70; k++)
            {
                int cx = (int)(Hash(k, 1, seed) * W), cy = (int)(Hash(k, 2, seed) * H);
                var c = Hash(k, 3, seed) < 0.65f ? (Color32)new Color(0.40f, 0.95f, 0.86f) : (Color32)new Color(0.70f, 0.52f, 1f);
                int size = Hash(k, 4, seed) > 0.8f ? 2 : 1;
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) glow[((cy + y) % H) * W + (cx + x) % W] = c;
            }
            var span = Arena.HalfExtents * 2f + Vector2.one * 30f;
            Layer(root, owned, "abyss.glow", glow, W, H, span, 0.22f, Tuning.Ground.VoidOrder + 1);

            // Nearer: the far wall's hanging stone, dim, a little slower than the room.
            for (int i = 0; i < 14; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var at = new Vector2(side * (13.5f + (float)rng.NextDouble() * 6f), -9f + (float)rng.NextDouble() * 16f);
                Place(root, "hanging", Stalactite(owned, 18 + rng.Next(18), 60 + rng.Next(70), rng.Next()), at, 0.55f, Tuning.Ground.VoidOrder + 2);
            }
        }

        static Sprite Stalactite(SceneryOwned owned, int w, int h, int seed)
        {
            var px = new Color32[w * h];
            var dark = new Color(0.07f, 0.07f, 0.09f);
            var lit = new Color(0.13f, 0.14f, 0.17f);
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)h;                 // 0 at the tip (bottom) .. 1 at the root
                float half = w * 0.5f * Mathf.Pow(t, 0.7f);
                for (int x = 0; x < w; x++)
                {
                    float dx = x + 0.5f - w * 0.5f;
                    if (Mathf.Abs(dx) > half) continue;
                    px[y * w + x] = dx < -half * 0.4f ? lit : dark;
                }
            }
            return Make(owned, px, w, h, new Vector2(0.5f, 0f));
        }

        // ------------------------------------------------------------------ the peaks' valley

        static void Valley(Transform root, SceneryOwned owned, System.Random rng)
        {
            // The valley floor far below: a snowy forest of tiny pines, a river winding through.
            const int W = 384, H = 384;
            var px = new Color32[W * H];
            int seed = rng.Next();
            var snow = new Color(0.78f, 0.82f, 0.90f);
            var pine = new Color(0.28f, 0.36f, 0.42f);
            var river = new Color(0.52f, 0.64f, 0.80f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float drift = Mathf.Sin(2f * Mathf.PI * x / W * 3f + Mathf.Sin(2f * Mathf.PI * y / H * 2f) * 2f) * 0.5f + 0.5f;
                Color c = Color.Lerp(snow, new Color(0.70f, 0.75f, 0.85f), Mathf.Floor(drift * 3f) / 3f * 0.6f);
                float rx = W * 0.5f + Mathf.Sin(2f * Mathf.PI * y / H * 2f) * 60f + Mathf.Sin(2f * Mathf.PI * y / H * 5f) * 14f;
                if (Mathf.Abs(x - rx) < 4f) c = river;
                px[y * W + x] = c;
            }
            // Small laden firs dotted over the valley (the foreground's firs, far off): rows of dark
            // needles widening downward, snow along every third row's lit half, a texel of trunk.
            // Stamped with wrap-around so the tiled valley shows no seam.
            var snowLit = new Color(0.91f, 0.93f, 0.97f);
            for (int k = 0; k < 220; k++)
            {
                int bx = (int)(Hash(k, 1, seed + 1) * W), by = (int)(Hash(k, 2, seed + 1) * H), size = 4 + (int)(Hash(k, 3, seed + 1) * 4);
                float rxAt = W * 0.5f + Mathf.Sin(2f * Mathf.PI * by / H * 2f) * 60f + Mathf.Sin(2f * Mathf.PI * by / H * 5f) * 14f;
                if (Mathf.Abs(bx - rxAt) < 8f) continue;                     // not in the river
                for (int r = 0; r < size; r++)
                {
                    int half = Mathf.FloorToInt(r / 2.2f), yy = ((by - r) % H + H) % H;
                    for (int dx = -half; dx <= half; dx++)
                    {
                        int xx = ((bx + dx) % W + W) % W;
                        px[yy * W + xx] = r % 3 == 1 && dx <= 0 ? (Color32)snowLit : (Color32)pine;
                    }
                }
                px[(((by - size) % H + H) % H) * W + bx] = new Color32(42, 38, 34, 255);
            }
            var span = Arena.HalfExtents * 2f + Vector2.one * 30f;
            Layer(root, owned, "valley", px, W, H, span, 0.25f, Tuning.Ground.VoidOrder + 1);

            // Ranges of peaks beyond, at the sides; then cloud drifting between.
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var at = new Vector2(side * (15f + (float)rng.NextDouble() * 6f), -10f + i * 3.4f);
                Place(root, "peak", Peak(owned, 120 + rng.Next(80), 70 + rng.Next(50), rng.Next()), at, 0.4f, Tuning.Ground.VoidOrder + 2);
            }
            for (int i = 0; i < 10; i++)
            {
                var at = new Vector2((float)(rng.NextDouble() * 2 - 1) * 26f, (float)(rng.NextDouble() * 2 - 1) * 18f);
                var p = Place(root, "cloud", Cloud(owned, 100 + rng.Next(80), 28 + rng.Next(16), rng.Next(), 0.7f), at, 0.55f, Tuning.Ground.VoidOrder + 3);
                p.Drift = 0.16f;
                p.WrapSpan = 60f;
            }
        }

        static Sprite Peak(SceneryOwned owned, int w, int h, int seed)
        {
            var px = new Color32[w * h];
            var rock = new Color(0.44f, 0.50f, 0.62f);
            var shade = new Color(0.36f, 0.41f, 0.53f);
            var snow = new Color(0.92f, 0.94f, 0.98f);
            float apex = w * (0.35f + 0.3f * Hash(1, 1, seed));
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float t = 1f - y / (float)h;
                float left = apex - apex * t, right = apex + (w - apex) * t;
                if (x < left || x > right) continue;
                Color c = x < apex ? rock : shade;
                if (y > h * (0.62f + 0.08f * Mathf.Sin(x * 0.3f + seed))) c = x < apex ? snow : Color.Lerp(snow, shade, 0.3f);
                px[y * w + x] = c;
            }
            return Make(owned, px, w, h, new Vector2(0.5f, 0f));
        }

        // ------------------------------------------------------------------ the tower's shaft

        static void Shaft(Transform root, SceneryOwned owned, System.Random rng)
        {
            // The far walls: dark coursed stone, buttresses every so often, and the windows of the
            // floors below on their own layer over the dark.
            const int W = 320, H = 360;
            var wall = new Color32[W * H];
            var lit = new Color32[W * H];
            var stone = Scenery.WallStone;
            var baseC = new Color(stone.r * 0.42f, stone.g * 0.42f, stone.b * 0.48f, 1f);
            var seam = new Color(stone.r * 0.22f, stone.g * 0.22f, stone.b * 0.27f, 1f);
            int seed = rng.Next();
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int course = y / 10, off = (course & 1) * 13;
                bool joint = y % 10 == 0 || (x + off) % 26 == 0;
                bool buttress = (x % 160) < 22;
                var c = joint ? seam : Shade(baseC, (Hash(x / 26, course, seed) - 0.5f) * 0.03f);
                if (buttress) c = Shade(c, (x % 160) < 3 ? 0.03f : -0.025f);
                wall[y * W + x] = c;
            }
            // Windows: in rows a storey apart, warm, some dark (nobody home).
            var warm = new Color32(255, 214, 150, 255);
            var dim = new Color32(230, 150, 80, 255);
            for (int storey = 0; storey < H / 60; storey++)
            for (int x = 30; x < W - 6; x += 40)
            {
                if ((x % 160) < 26) continue;
                float h = Hash(x, storey, seed + 1);
                if (h < 0.45f) continue;
                int wy = storey * 60 + 22, wx = x + (int)(Hash(x, storey, seed + 2) * 8);
                for (int y = 0; y < 7; y++)
                for (int k = 0; k < 4; k++)
                {
                    bool arch = y == 6 && (k == 0 || k == 3);
                    if (arch) continue;
                    lit[(wy + y) * W + wx + k] = h > 0.8f ? warm : dim;
                }
            }
            var span = Arena.HalfExtents * 2f + Vector2.one * 30f;
            Layer(root, owned, "shaft", wall, W, H, span, 0.32f, Tuning.Ground.VoidOrder + 1);
            Layer(root, owned, "shaft.lights", lit, W, H, span, 0.32f, Tuning.Ground.VoidOrder + 2);
        }

        // ------------------------------------------------------------------ the sky

        static void Sky(Transform root, SceneryOwned owned, System.Random rng)
        {
            // Far islands first (slowest), then two layers of cloud over them.
            var spots = new[] { new Vector2(-17f, 4f), new Vector2(18f, 1f), new Vector2(-15f, -9f), new Vector2(16f, -10f), new Vector2(2f, -13f) };
            foreach (var at in spots)
            {
                int w = 70 + rng.Next(60);
                var spr = Island(owned, w, rng.Next());
                Place(root, "island", spr, at, 0.28f, Tuning.Ground.VoidOrder + 1);
            }
            for (int layer = 0; layer < 2; layer++)
            {
                float depth = layer == 0 ? 0.42f : 0.62f;
                int n = layer == 0 ? 12 : 9;
                for (int i = 0; i < n; i++)
                {
                    var at = new Vector2((float)(rng.NextDouble() * 2 - 1) * 26f, (float)(rng.NextDouble() * 2 - 1) * 18f);
                    var spr = Cloud(owned, 90 + rng.Next(90), 30 + rng.Next(20), rng.Next(), layer == 0 ? 0.55f : 0.8f);
                    var p = Place(root, "cloud", spr, at, depth, Tuning.Ground.VoidOrder + 2 + layer);
                    p.Drift = layer == 0 ? 0.12f : 0.22f;
                    p.WrapSpan = 60f;
                }
            }
        }

        static Sprite Island(SceneryOwned owned, int w, int seed)
        {
            int h = w;
            var px = new Color32[w * h];
            var grass = new Color(0.44f, 0.56f, 0.34f);
            var rock = new Color(0.58f, 0.56f, 0.53f);
            var under = new Color(0.36f, 0.40f, 0.50f);
            int top = h - 8;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                // An upturned cone of rock under a flat top, ragged at the sides.
                float depth = (1f - Mathf.Abs(u)) * h * 0.9f * (0.75f + 0.25f * Hash(x / 4, 0, seed));
                int yy = top - y;
                Color c = default; bool on = false;
                if (yy >= 0 && yy <= depth && Mathf.Abs(u) < 0.96f)
                {
                    on = true;
                    float f = yy / Mathf.Max(1f, depth);
                    c = Color.Lerp(rock, under, f);
                    if ((yy + x / 3) % 7 == 0) c = Shade(c, -0.05f);
                    if (yy < 3) c = grass;
                }
                else if (y >= top && y < top + 3 && Mathf.Abs(u) < 0.9f) { on = true; c = Shade(grass, 0.06f); }
                // A ruin's stump on top, and a thread of falling water off one side.
                if (y >= top + 3 && y < top + 8 && Mathf.Abs(u - 0.2f) < 0.08f) { on = true; c = new Color(0.82f, 0.80f, 0.76f); }
                if (!on && Mathf.Abs(u + 0.55f) < 0.03f && y < top && y > top - h * 0.7f) { on = true; c = new Color(0.86f, 0.93f, 1f, 0.7f); }
                if (on) { c.a = c.a <= 0f ? 1f : c.a; px[y * w + x] = c; }
            }
            return Make(owned, px, w, h, new Vector2(0.5f, 0.5f));
        }

        static Sprite Cloud(SceneryOwned owned, int w, int h, int seed, float alpha)
        {
            var px = new Color32[w * h];
            var lit = new Color(1f, 1f, 1f, alpha);
            var shade = new Color(0.82f, 0.87f, 0.95f, alpha);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                float lumps = 0.55f + 0.25f * Mathf.Sin(u * 7f + seed) + 0.15f * Mathf.Sin(u * 13f + seed * 0.3f);
                float top = lumps * (1f - u * u);
                if (v > top || v < -0.55f * (1f - u * u)) continue;
                px[y * w + x] = v < -0.1f ? shade : lit;
            }
            return Make(owned, px, w, h, new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ the forest's lake

        static void Lake(Transform root, SceneryOwned owned, System.Random rng)
        {
            var spots = new List<Vector2>();
            for (int i = 0; i < 10; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                spots.Add(new Vector2(side * (14f + (float)rng.NextDouble() * 6f), -9f + (float)rng.NextDouble() * 15f));
            }
            for (int i = 0; i < 4; i++)
                spots.Add(new Vector2(-10f + i * 6.5f + (float)rng.NextDouble() * 2f, -10.5f - (float)rng.NextDouble() * 2f));
            foreach (var at in spots)
                Place(root, "deadtree", DeadTree(owned, 26 + rng.Next(18), 80 + rng.Next(50), rng.Next()), at, 1f, Tuning.Ground.Order + 2);
            float headSide = rng.Next(2) == 0 ? -1f : 1f;
            Place(root, "drowned.head", Head(owned, rng.Next(), false), new Vector2(headSide * 15.5f, -3.5f), 1f, Tuning.Ground.Order + 2);
        }

        static Sprite DeadTree(SceneryOwned owned, int w, int h, int seed)
        {
            var px = new Color32[w * h];
            var bark = new Color(0.10f, 0.11f, 0.09f);
            var lit = new Color(0.20f, 0.22f, 0.17f);
            int cx = w / 2;
            void Dot(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = c; }
            // The trunk, narrowing, then a few bare limbs.
            for (int y = 0; y < h * 0.8f; y++)
            {
                int half = Mathf.Max(1, Mathf.RoundToInt(3.5f - y * 2.5f / h));
                int lean = Mathf.RoundToInt(Mathf.Sin(y * 0.05f + seed) * 2f);
                for (int k = -half; k <= half; k++) Dot(cx + k + lean, y, k == -half ? lit : bark);
            }
            for (int b = 0; b < 4; b++)
            {
                int y0 = (int)(h * (0.4f + 0.12f * b)), dir = (b + seed) % 2 == 0 ? 1 : -1;
                int len = 8 + (int)(Hash(b, seed, 3) * 10);
                for (int t = 0; t < len; t++) Dot(cx + dir * t, y0 + t / 2 + (int)(Mathf.Sin(t * 0.4f) * 1.5f), bark);
            }
            // The water's ring at its foot.
            for (int k = -6; k <= 6; k++) Dot(cx + k, 0, new Color(0.30f, 0.42f, 0.36f, 0.7f));
            return Make(owned, px, w, h, new Vector2(0.5f, 0f));
        }

        static Sprite Head(SceneryOwned owned, int seed, bool salt)
        {
            const int w = 110, h = 74;
            var px = new Color32[w * h];
            var stone = salt ? new Color(0.86f, 0.82f, 0.80f) : new Color(0.30f, 0.33f, 0.28f);
            var dark = salt ? new Color(0.62f, 0.55f, 0.58f) : new Color(0.12f, 0.14f, 0.11f);
            var moss = salt ? new Color(0.97f, 0.96f, 0.95f) : new Color(0.26f, 0.38f, 0.18f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h;
                // A face half under the surface, tipped back: the brow and nose stand out of it.
                float r = Mathf.Sqrt(u * u * 1.1f + (v - 0.1f) * (v - 0.1f) * 1.6f);
                if (r > 0.95f) continue;
                Color c = Color.Lerp(stone, dark, Mathf.Clamp01((0.95f - r) < 0.08f ? 0.6f : v * 0.4f));
                if (Mathf.Abs(Mathf.Abs(u) - 0.35f) < 0.12f && Mathf.Abs(v - 0.55f) < 0.07f) c = dark;   // the eyes
                if (Mathf.Abs(u) < 0.06f && v > 0.25f && v < 0.55f) c = Shade(stone, 0.06f);            // the nose
                if (v > 0.78f && Hash(x / 3, y / 3, seed) > 0.4f) c = moss;
                if (y < 3) c = new Color(0.36f, 0.48f, 0.42f, 0.8f);
                px[y * w + x] = c;
            }
            return Make(owned, px, w, h, new Vector2(0.5f, 0f));
        }

        // ------------------------------------------------------------------ the salt's brine

        static void Brine(Transform root, SceneryOwned owned, System.Random rng)
        {
            for (int i = 0; i < 12; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var at = new Vector2(side * (13.5f + (float)rng.NextDouble() * 7f), -10f + (float)rng.NextDouble() * 16f);
                Place(root, "spire", Spire(owned, 14 + rng.Next(14), 40 + rng.Next(70), rng.Next()), at, 1f, Tuning.Ground.Order + 2);
            }
            float side2 = rng.Next(2) == 0 ? -1f : 1f;
            Place(root, "giant.hand", Hand(owned, rng.Next()), new Vector2(side2 * 16f, -6f), 1f, Tuning.Ground.Order + 2);
        }

        static Sprite Spire(SceneryOwned owned, int w, int h, int seed)
        {
            var px = new Color32[w * h];
            var lit = new Color(0.98f, 0.97f, 0.96f);
            var mid = new Color(0.88f, 0.84f, 0.84f);
            var dark = new Color(0.70f, 0.62f, 0.66f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                float half = 1f - y / (float)h;          // a six-sided point, narrowing to the top
                if (Mathf.Abs(u) > half) continue;
                var c = u < -half * 0.3f ? lit : u < half * 0.35f ? mid : dark;
                if (y < 2) c = new Color(0.84f, 0.90f, 0.88f, 0.8f);
                px[y * w + x] = c;
            }
            return Make(owned, px, w, h, new Vector2(0.5f, 0f));
        }

        static Sprite Hand(SceneryOwned owned, int seed)
        {
            const int w = 90, h = 96;
            var px = new Color32[w * h];
            var salt = new Color(0.93f, 0.90f, 0.88f);
            var shade = new Color(0.74f, 0.66f, 0.70f);
            // A finger: a tapering column with a rounded tip, leaning a little, its far side in shade
            // and a crease at each knuckle.
            void Finger(float x0, float width, int len, float lean, int from)
            {
                for (int y = from; y < from + len && y < h; y++)
                {
                    float t = (y - from) / (float)len;
                    float half = width * 0.5f * (1f - 0.25f * t);
                    float cx = x0 + lean * (y - from);
                    float tip = from + len - half;
                    for (int x = Mathf.FloorToInt(cx - half); x <= Mathf.CeilToInt(cx + half); x++)
                    {
                        if (x < 0 || x >= w) continue;
                        float dx = x + 0.5f - cx;
                        if (y > tip && dx * dx + (y - tip) * (y - tip) > half * half) continue;
                        if (Mathf.Abs(dx) > half) continue;
                        var c = dx > half * 0.35f ? shade : dx < -half * 0.6f ? Shade(salt, 0.04f) : salt;
                        if ((y - from) % 17 == 16) c = Shade(shade, -0.05f);
                        px[y * w + x] = c;
                    }
                }
            }
            for (int y = 0; y < 30; y++)                // the palm, sunk to the knuckles
            for (int x = 20; x < 72; x++)
            {
                if (y > 22 && (x < 23 + (y - 22) || x > 69 - (y - 22) / 2)) continue;
                px[y * w + x] = x > 62 ? shade : x < 23 ? Shade(salt, 0.04f) : salt;
            }
            Finger(27f, 11f, 46, -0.06f, 26); Finger(39f, 11f, 60, -0.02f, 26);
            Finger(51f, 11f, 56, 0.02f, 26); Finger(63f, 10f, 40, 0.07f, 26);
            Finger(14f, 12f, 30, -0.3f, 8);           // the thumb
            for (int x = 0; x < w; x++) px[x] = new Color32(214, 226, 222, 200);
            return Make(owned, px, w, h, new Vector2(0.5f, 0f));
        }

        // ------------------------------------------------------------------ pieces

        static Transform Layer(Transform root, SceneryOwned owned, string name, Color32[] px, int w, int h,
                               Vector2 span, float depth, int order)
        {
            var spr = Make(owned, px, w, h, new Vector2(0.5f, 0.5f));
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = span;
            sr.sortingOrder = order;
            var p = go.AddComponent<Parallax>();
            p.Depth = depth;
            p.Anchor = Vector2.zero;
            return go.transform;
        }

        static Parallax Place(Transform root, string name, Sprite spr, Vector2 at, float depth, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.sortingOrder = order;
            var p = go.AddComponent<Parallax>();
            p.Depth = depth;
            p.Anchor = at;
            if (depth == 1f) p.enabled = false;   // on the floor's own plane: it simply stands there
            return p;
        }

        static Sprite Make(SceneryOwned owned, Color32[] px, int w, int h, Vector2 pivot)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, Ppu, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            if (owned != null) { owned.Add(tex); owned.Add(s); }
            return s;
        }

        static float Hash(int a, int b, int s)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263 + s * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
            }
        }

        static Color Shade(Color c, float v) => new(Mathf.Clamp01(c.r + v), Mathf.Clamp01(c.g + v), Mathf.Clamp01(c.b + v), c.a);
    }
}
