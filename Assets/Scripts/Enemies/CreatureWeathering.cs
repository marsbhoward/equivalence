using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Biomes;

namespace Convergence.Enemies
{
    /// <summary>
    /// CREATURES WEATHERED BY THE PLACE (the user's call, 2026-10-22; mock
    /// https://claude.ai/artifact/HkpeaEuh4ha9LgpEaZcFR3): a layer DERIVED from each finished frame,
    /// so it covers every frame of every kind and both materials with nothing authored per frame.
    ///
    ///   Drowned Forest   damp and darker all over, MOSS on every upward-facing edge (one or two
    ///                    texels, ragged), lichen flecks under it
    ///   Salt Flats       the forest's rule in salt: a crust on every upward edge, crystal flecks,
    ///                    a little paler and drier - kept a step under the floor's white
    ///   Sky Ruin         colour drawn out and mid-tones lifted, the darks kept so it still stands
    ///                    off pale stone; a scoured pale rim on the top edges
    ///
    /// Never touched: the outline, anything translucent (smears, glows), each kind's RESERVED
    /// colours (its tells - <c>XArt.Reserved</c>), the biome ACCENTS a rig draws (they are already
    /// the place's), and any bright saturated texel as a backstop. A tell must read the same in
    /// every biome. The pattern is hashed from the texel's COLUMN and its depth below the edge -
    /// never its row - so a walk's bob does not make the moss crawl.
    ///
    /// The Tower draws creatures as they are. Cached per (sprite, biome).
    /// </summary>
    public static class CreatureWeathering
    {
        static readonly Dictionary<(Sprite, BiomeKind), Sprite> _cache = new();

        // ---- the biome ACCENTS a rig draws (per kind, per biome) - never weathered over ----
        /// <summary>A bracket fungus: gill shadow, underside, cap, lit cap.</summary>
        public static readonly Color[] Fungus = Hex("#2a1c12", "#785c3c", "#d6be8c", "#f0e0ba");
        /// <summary>A shelf of salt cubes: shadow, pink face, cube top, lit top.</summary>
        public static readonly Color[] SaltCubes = Hex("#5a3c44", "#ceb0b6", "#e8e0de", "#f6f0ec");
        /// <summary>Torn cloth streaming in the wind.</summary>
        public static readonly Color[] Streamer = Hex("#4e463a", "#787062", "#a89e88", "#c4baa0");

        static readonly Color[] Moss = Hex("#263a1a", "#3e5a22", "#607c30");
        static readonly Color Lichen = Hex("#768460")[0];
        static readonly Color[] Salt = Hex("#b0a0a0", "#ccbebc", "#e6dcd8");
        static readonly Color SaltFleck = Hex("#e2ced0")[0];
        static readonly Color SkyPale = new(0.78f, 0.81f, 0.84f), SkyRim = new(0.886f, 0.902f, 0.925f);

        /// <summary><paramref name="frames"/> as they look in <paramref name="biome"/>. The array
        /// is new; the sprites are shared through the cache.</summary>
        public static Sprite[] For(Sprite[] frames, BiomeKind biome, Color[] reserved)
        {
            if (frames == null || biome == BiomeKind.Tower) return frames;
            var o = new Sprite[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                var s = frames[i];
                if (s == null) continue;
                if (!_cache.TryGetValue((s, biome), out var w) || w == null)
                    _cache[(s, biome)] = w = Weather(s, biome, reserved);
                o[i] = w;
            }
            return o;
        }

        static Sprite Weather(Sprite s, BiomeKind biome, Color[] reserved)
        {
            var tex = s.texture;
            if (tex == null || !tex.isReadable) return s;
            var r = s.rect;
            int x0 = (int)r.x, y0 = (int)r.y, W = (int)r.width, H = (int)r.height;
            var src = tex.GetPixels(x0, y0, W, H);
            var px = (Color[])src.Clone();

            // 0 empty, 1 outline, 2 kept (effect, tell, accent), 3 weatherable
            var kind = new byte[W * H];
            for (int i = 0; i < src.Length; i++) kind[i] = Classify(src[i], reserved);
            byte K(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? (byte)0 : kind[y * W + x];

            for (int x = 0; x < W; x++)
            {
                // Texture rows run bottom-up: walk DOWN from the top, counting depth below the
                // nearest upward-facing edge.
                int depth = 99;
                float h = Hash(x >> 1, 0, 7);
                int thick = h < 0.45f ? 1 : h < 0.8f ? 2 : 0;
                for (int y = H - 1; y >= 0; y--)
                {
                    if (K(x, y) != 3) { depth = 99; continue; }
                    depth = K(x, y + 1) <= 1 ? 0 : depth + 1;
                    int i = y * W + x;
                    var c = src[i];
                    float L = Lum(c);
                    bool fleck = depth > thick && depth <= thick + 2 && Hash(x, depth, 11) < 0.12f;
                    Color o;
                    switch (biome)
                    {
                        case BiomeKind.Forest:
                            o = Color.Lerp(c, new Color(c.r * 0.82f, c.g * 0.92f, c.b * 0.78f), 0.8f);
                            if (depth <= thick) o = Moss[L > 0.35f ? 2 : L > 0.2f ? 1 : 0];
                            else if (fleck) o = Lichen;
                            break;
                        case BiomeKind.SaltFlats:
                            o = Color.Lerp(c, new Color(c.r * 1.06f + 0.024f, c.g + 0.016f, c.b + 0.016f), 0.7f);
                            if (depth <= thick) o = Salt[L > 0.35f ? 2 : L > 0.2f ? 1 : 0];
                            else if (fleck) o = SaltFleck;
                            break;
                        case BiomeKind.SkyRuin:
                            o = Color.Lerp(c, new Color(L, L, L * 1.04f), 0.45f);
                            o = Color.Lerp(o, SkyPale, 0.12f * Mathf.Min(1f, L * 2.2f));
                            if (depth == 0 && h < 0.5f) o = Color.Lerp(o, SkyRim, 0.55f);
                            break;
                        default:
                            o = c;
                            break;
                    }
                    o.a = c.a;
                    px[i] = o;
                }
            }

            var t = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave, name = s.name + "." + biome,
            };
            t.SetPixels(px);
            t.Apply();
            var w = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(s.pivot.x / W, s.pivot.y / H), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            w.name = t.name;
            w.hideFlags = HideFlags.HideAndDontSave;
            return w;
        }

        static byte Classify(Color c, Color[] reserved)
        {
            if (c.a <= 0f) return 0;
            if (c.r < 0.066f && c.g < 0.066f && c.b < 0.066f) return 1;   // the outline (every kind's is under #111)
            if (c.a < 0.999f) return 2;                                 // smears, glows, halos
            if (Matches(c, reserved) || Matches(c, Fungus) || Matches(c, SaltCubes) || Matches(c, Streamer) || CreatureAccents.IsAccent(c)) return 2;
            Color.RGBToHSV(c, out _, out float sat, out float val);
            if (sat > 0.5f && val > 0.55f) return 2;                    // a bright glow nobody listed
            return 3;
        }

        static bool Matches(Color c, Color[] set)
        {
            if (set == null) return false;
            foreach (var k in set)
                if (Mathf.Abs(k.r - c.r) + Mathf.Abs(k.g - c.g) + Mathf.Abs(k.b - c.b) < 0.04f) return true;
            return false;
        }

        static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + (s + 1) * 982451653);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static Color[] Hex(params string[] hex) => TexelRig.Hex(hex);
    }
}
