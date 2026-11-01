using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// FOREGROUND FRAMING (the user's call, 2026-11-01): dark shapes past the room's south and side
    /// edges, nearer the camera than the floor - they sweep past faster than the room as it pans
    /// (<see cref="Parallax"/> depth over 1) and crop the frame's edge, so the room is looked INTO
    /// rather than down on. The tower's great columns and hanging chains, the forest's boughs, the
    /// sky ruin's broken arches, the salt's crystal clusters.
    ///
    /// NOTHING LIES OVER THE PLAYER: a shape with the player or an enemy behind it thins to
    /// <see cref="Tuning.Foreground.Behind"/> (the forest canopy's rule). Drawn over the light field,
    /// in its own dark: it stands between the camera and the room's light.
    /// </summary>
    public class Foreground : MonoBehaviour
    {
        const float Ppu = Scenery.Ppu;

        struct Piece { public SpriteRenderer Sr; public float Alpha; }
        readonly List<Piece> _pieces = new();
        Transform _player;

        public static void Build(Transform parent, SceneryOwned owned, int seed)
        {
            var go = new GameObject("foreground");
            go.transform.SetParent(parent, false);
            var fg = go.AddComponent<Foreground>();
            var rng = new System.Random(seed * 23 + 6);
            var inner = Arena.Inner;
            float south = -inner.y - Tuning.Foreground.SouthOut, side = inner.x + Tuning.Foreground.SideOut;

            // Along the south: a shape every few units, from corner to corner.
            int n = Mathf.RoundToInt(inner.x * 2f / Tuning.Foreground.Spacing) + 2;
            for (int i = 0; i < n; i++)
            {
                float x = -inner.x - 2f + i * Tuning.Foreground.Spacing + (float)(rng.NextDouble() - 0.5) * 1.4f;
                fg.Add(owned, new Vector2(x, south - (float)rng.NextDouble() * 0.8f), rng.Next(), false);
            }
            // Down each side.
            for (float y = -inner.y; y < inner.y; y += Tuning.Foreground.Spacing * 1.3f)
                foreach (float s in new[] { -1f, 1f })
                    fg.Add(owned, new Vector2(s * (side + (float)rng.NextDouble() * 0.8f), y + (float)rng.NextDouble()), rng.Next(), true);
        }

        void Add(SceneryOwned owned, Vector2 at, int seed, bool side)
        {
            var spr = Shape(owned, Biome.Current, seed, side);
            if (spr == null) return;
            var go = new GameObject("fg");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.sortingOrder = SortingOrders.StatusOverlay - 8;
            var p = go.AddComponent<Parallax>();
            p.Depth = Tuning.Foreground.Depth;
            p.Anchor = at;
            _pieces.Add(new Piece { Sr = sr, Alpha = 1f });
        }

        void LateUpdate()
        {
            if (_player == null)
            {
                var pc = FindAnyObjectByType<Player.PlayerController>();
                if (pc != null) _player = pc.transform;
            }
            var player = _player;
            for (int i = 0; i < _pieces.Count; i++)
            {
                var p = _pieces[i];
                if (p.Sr == null) continue;
                var b = p.Sr.bounds;
                b.Expand(new Vector3(0.3f, 0.3f, 0f));
                bool behind = player != null && Covers(b, player.position);
                if (!behind)
                    foreach (var e in Enemies.EnemyRegistry.All)
                        if (e != null && Covers(b, e.transform.position)) { behind = true; break; }
                float want = behind ? Tuning.Foreground.Behind : 1f;
                p.Alpha = Mathf.MoveTowards(p.Alpha, want, Time.deltaTime / Tuning.Foreground.FadeSeconds);
                var c = p.Sr.color; c.a = p.Alpha; p.Sr.color = c;
                _pieces[i] = p;
            }
        }

        /// <summary>Does the shape stand over a body at <paramref name="feet"/> - its feet or the
        /// body above them?</summary>
        static bool Covers(Bounds b, Vector3 feet)
            => b.Contains(new Vector3(feet.x, feet.y, b.center.z)) || b.Contains(new Vector3(feet.x, feet.y + 1f, b.center.z));

        // ------------------------------------------------------------------ the shapes

        static Sprite Shape(SceneryOwned owned, BiomeKind biome, int seed, bool side)
        {
            switch (biome)
            {
                case BiomeKind.Forest: return Bough(owned, seed, side);
                case BiomeKind.SkyRuin: return Arch(owned, seed, side);
                case BiomeKind.SaltFlats: return Crystals(owned, seed);
                case BiomeKind.Caves: return Crystals(owned, seed);
                case BiomeKind.SnowyPeaks: return Pine(owned, seed);
                default: return side && (seed & 1) == 0 ? Chain(owned, seed) : Column(owned, seed);
            }
        }

        static Color Dark => Biome.Current switch
        {
            BiomeKind.Forest => new Color(0.03f, 0.06f, 0.04f),
            BiomeKind.SkyRuin => new Color(0.20f, 0.22f, 0.30f),
            BiomeKind.SaltFlats => new Color(0.36f, 0.30f, 0.34f),
            BiomeKind.Caves => new Color(0.02f, 0.02f, 0.03f),
            BiomeKind.SnowyPeaks => new Color(0.10f, 0.14f, 0.17f),
            _ => new Color(0.035f, 0.03f, 0.045f),
        };

        /// <summary>What catches the room's light on the shape's inner edge.</summary>
        static Color Rim => Biome.Current switch
        {
            BiomeKind.Forest => new Color(0.10f, 0.16f, 0.09f),
            BiomeKind.SkyRuin => new Color(0.42f, 0.44f, 0.52f),
            BiomeKind.SaltFlats => new Color(0.58f, 0.50f, 0.54f),
            BiomeKind.Caves => new Color(0.10f, 0.22f, 0.24f),
            BiomeKind.SnowyPeaks => new Color(0.80f, 0.86f, 0.94f),
            _ => new Color(0.14f, 0.11f, 0.10f),
        };

        static Sprite Column(SceneryOwned owned, int seed)
        {
            int w = 56 + (seed & 7) * 2, h = 84 + (seed >> 3 & 31);
            var px = new Color32[w * h];
            var dark = Dark; var rim = Rim;
            int shaft = w - 16;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool capital = y > h - 18;
                int half = capital ? w / 2 : shaft / 2;
                int dx = x - w / 2;
                if (dx < -half || dx >= half) continue;
                Color c = dark;
                if (!capital && (dx + half) % 9 == 0) c = Color.Lerp(dark, rim, 0.35f);   // fluting
                if (dx == -half) c = rim;
                if (capital && (y == h - 18 || y == h - 10)) c = Color.Lerp(dark, rim, 0.5f);
                px[y * w + x] = c;
            }
            return Make(owned, px, w, h, new Vector2(0.5f, 0f));
        }

        static Sprite Chain(SceneryOwned owned, int seed)
        {
            int w = 14, h = 170 + (seed & 31);
            var px = new Color32[w * h];
            var dark = Dark; var rim = Rim;
            for (int y = 0; y < h; y++)
            {
                int link = y % 14;
                bool flat = (y / 14) % 2 == 0;
                for (int x = 0; x < w; x++)
                {
                    int dx = x - w / 2;
                    bool on = flat ? (link < 2 || link > 11 ? Mathf.Abs(dx) < 4 : Mathf.Abs(Mathf.Abs(dx) - 4) < 1.5f)
                                   : Mathf.Abs(dx) < 1.5f;
                    if (on) px[y * w + x] = dx < 0 ? rim : dark;
                }
            }
            // Hung from above: pivot at the top.
            return Make(owned, px, w, h, new Vector2(0.5f, 0f));
        }

        static Sprite Bough(SceneryOwned owned, int seed, bool side)
        {
            int w = 130 + (seed & 31), h = 74 + (seed >> 5 & 31);
            var px = new Color32[w * h];
            var dark = Dark; var rim = Rim;
            var rng = new System.Random(seed);
            // Leaf masses: overlapping lumps along a curving bough.
            for (int k = 0; k < 9; k++)
            {
                float cx = w * (0.15f + 0.7f * (float)rng.NextDouble()), cy = h * (0.25f + 0.6f * (float)rng.NextDouble());
                float rx = 14 + rng.Next(14), ry = 10 + rng.Next(10);
                for (int y = (int)(cy - ry); y <= cy + ry; y++)
                for (int x = (int)(cx - rx); x <= cx + rx; x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    float u = (x - cx) / rx, v = (y - cy) / ry;
                    float r = u * u + v * v;
                    float jag = 0.85f + 0.15f * Mathf.Sin(x * 1.3f + y * 0.7f + k);
                    if (r > jag) continue;
                    px[y * w + x] = v > 0.55f && r > 0.5f ? rim : dark;
                }
            }
            for (int y = 0; y < h * 0.5f; y++)
            {
                int x = (int)(w * 0.5f + Mathf.Sin(y * 0.05f + seed) * 10f);
                for (int k = -3; k <= 3; k++) if (x + k >= 0 && x + k < w) px[y * w + x + k] = dark;
            }
            return Make(owned, px, w, h, new Vector2(0.5f, 0f));
        }

        static Sprite Arch(SceneryOwned owned, int seed, bool side)
        {
            int w = 100, h = 100;
            var px = new Color32[w * h];
            var dark = Dark; var rim = Rim;
            float cx = (seed & 1) == 0 ? 0f : w, cy = 0f, ro = 96f, ri = 68f;
            float broken = 0.55f + 0.3f * ((seed >> 2 & 15) / 15f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x - cx, dy = y - cy, r = Mathf.Sqrt(dx * dx + dy * dy);
                bool ring = r < ro && r > ri && Mathf.Atan2(dy, Mathf.Abs(dx)) < broken * Mathf.PI * 0.5f;
                bool pier = Mathf.Abs(x - (cx == 0f ? 82f : 18f)) < 14f && y < 40;
                if (!ring && !pier) continue;
                Color c = dark;
                if (ring && (r - ri < 2f || ((int)(Mathf.Atan2(dy, Mathf.Abs(dx)) * 30f)) % 4 == 0)) c = rim;
                px[y * w + x] = c;
            }
            return Make(owned, px, w, h, new Vector2(0.5f, 0f));
        }

        static Sprite Crystals(SceneryOwned owned, int seed)
        {
            int w = 110, h = 110;
            var px = new Color32[w * h];
            var dark = Dark; var rim = Rim;
            var rng = new System.Random(seed);
            for (int k = 0; k < 5; k++)
            {
                float bx = 20 + (float)rng.NextDouble() * 70f, lean = ((float)rng.NextDouble() - 0.5f) * 0.6f;
                float hw = 7 + rng.Next(7), len = 45 + rng.Next(60);
                for (int y = 0; y < len && y < h; y++)
                {
                    float half = hw * (1f - y / len);
                    float cx = bx + lean * y;
                    for (int x = (int)(cx - half); x <= cx + half; x++)
                        if (x >= 0 && x < w) px[y * w + x] = x < cx - half * 0.4f ? rim : dark;
                }
            }
            return Make(owned, px, w, h, new Vector2(0.5f, 0f));
        }

        /// <summary>
        /// A LADEN FIR near the camera (the user's pick, 2026-11-01 - Option A of
        /// https://claude.ai/artifact/3rchYWJPXpbBV259rHWQJt): dense tiers drawn from the bottom up, so
        /// each upper tier's needle fringe drapes over the snow of the one below; every tier rises in a
        /// shoulder to the trunk and hangs in scallops, one per bough; snow lies in pillows on each
        /// bough's top, lit from the north-west and shaded on the east. In the near dark's palette.
        /// </summary>
        static Sprite Pine(SceneryOwned owned, int seed)
        {
            seed &= 0x7fff;
            int W = 92 + seed % 9, H = 116 + (seed >> 3) % 13;
            // Drawn top row first (y down), flipped into the texture at the end.
            var px = new Color32[W * H];
            bool[] set = new bool[W * H];
            void Put(float fx, float fy, Color32 c)
            {
                int x = Mathf.FloorToInt(fx), y = Mathf.FloorToInt(fy);
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                px[(H - 1 - y) * W + x] = c;
                set[y * W + x] = true;
            }

            Color32 needle = new(16, 27, 33, 255), mid = new(23, 38, 48, 255), rim = new(42, 65, 76, 255),
                    under = new(10, 18, 22, 255), snow = new(198, 209, 226, 255), snowTop = new(227, 233, 243, 255),
                    snowShade = new(151, 165, 190, 255), bark = new(29, 24, 23, 255), barkLit = new(51, 43, 39, 255);

            const float baseAt = 0.84f, taper = 1.05f, overlap = 1.35f, maxW = 42f, shoulder = 0.5f, dip = 2f,
                        droop = 5f, scallopDepth = 4f, fringe = 3f, boughs = 2.2f, snowLay = 4.6f, pillowH = 4f;
            const int trunk = 3;
            int top = 3, baseY = Mathf.RoundToInt(H * baseAt), n = 6 + seed % 2;
            float cx = W / 2f;

            for (int y = baseY - 6; y < H; y++)
            {
                int half = trunk + (y > H - 6 ? 1 : 0);
                for (int x = Mathf.RoundToInt(cx - half); x <= cx + half; x++) Put(x, y, x < cx - half + 2 ? barkLit : bark);
            }
            for (int i = n - 1; i >= 0; i--)
            {
                float f = (i + 1) / (float)n;
                float yB = top + (baseY - top) * Mathf.Pow(f, taper);
                float th = (baseY - top) / (float)n * overlap;
                float yT = Mathf.Max(top, yB - th);
                float wL = maxW * Mathf.Pow(f, 0.9f) * (0.88f + 0.24f * H2(i, 1, seed));
                float wR = maxW * Mathf.Pow(f, 0.9f) * (0.88f + 0.24f * H2(i, 2, seed));
                for (int x = Mathf.FloorToInt(cx - wL); x <= cx + wR; x++)
                {
                    int side = x < cx ? -1 : 1;
                    float w = side < 0 ? wL : wR, t = Mathf.Min(1f, Mathf.Abs(x - cx) / Mathf.Max(1f, w));
                    float k = t * boughs + H2(i, side, seed) * 0.6f, fr = k - Mathf.Floor(k);
                    float scallop = Mathf.Sin(Mathf.PI * fr);
                    float up = yT + th * shoulder * Mathf.Pow(t, 0.85f) + (1f - scallop) * dip * t;
                    float low = yB + droop * Mathf.Pow(t, 1.6f) - (1f - scallop) * scallopDepth * (0.4f + t);
                    low += Mathf.Floor(H2(x >> 1, i, seed + 3) * fringe);       // the needle fringe
                    if (low < up + 1f) low = up + 1f;
                    for (int y = Mathf.FloorToInt(up); y <= low; y++)
                    {
                        float d = (y - up) / Mathf.Max(1f, low - up);
                        var c = d > 0.72f ? under : d > 0.4f ? needle : mid;
                        if (side < 0 && t > 0.5f && d < 0.5f) c = rim;           // lit from the north-west
                        if (H2(x >> 1, y >> 1, seed + 5) < 0.08f && d > 0.2f) c = under;   // needle clusters
                        Put(x, y, c);
                    }
                    // Snow in pillows on each bough, thicker toward the trunk, shaded on the east.
                    float lay = snowLay * (0.55f + 0.45f * scallop) * (1f - 0.5f * t) * (0.7f + 0.6f * Vn(x / 4f + i * 7, seed + 7));
                    if (lay > 0.6f)
                    {
                        int pillow = Mathf.RoundToInt(pillowH * scallop * (1f - t * 0.5f));
                        int y0 = Mathf.FloorToInt(up) - pillow, y1 = Mathf.FloorToInt(up) + Mathf.RoundToInt(lay);
                        for (int y = y0; y <= y1; y++)
                        {
                            var c = side > 0 ? snowShade : snow;
                            if (y == y0) c = side > 0 ? snow : snowTop;
                            Put(x, y, c);
                        }
                    }
                }
            }
            // The leader: a thin spire over the top tier, a cap of snow.
            for (int y = top + 3; y > top - 4; y--) Put(cx, y, needle);
            Put(cx, top - 3, snowTop);
            Put(cx - 1, top - 1, snow);
            return Make(owned, px, W, H, new Vector2(0.5f, 0f));
        }

        static float H2(int x, int y, int s)
        {
            unchecked
            {
                int n = x * 374761393 + y * 668265263 + s * 1442695041;
                n = (int)((uint)(n ^ (int)((uint)n >> 13)) * 1274126177u);
                n ^= (int)((uint)n >> 16);
                return (uint)n / 4294967296f;
            }
        }

        static float Vn(float x, int s)
        {
            int i = Mathf.FloorToInt(x);
            float f = x - i, u = f * f * (3f - 2f * f);
            return H2(i, 0, s) * (1f - u) + H2(i + 1, 0, s) * u;
        }

        static Sprite Make(SceneryOwned owned, Color32[] px, int w, int h, Vector2 pivot)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, Ppu, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            if (owned != null) { owned.Add(tex); owned.Add(s); }
            return s;
        }
    }
}
