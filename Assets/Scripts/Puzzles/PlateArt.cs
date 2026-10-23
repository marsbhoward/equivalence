using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Biomes;

namespace Convergence.Puzzles
{
    /// <summary>
    /// A puzzle stone's picture, per place (2026-10-23), drawn texel by texel at the floor's density
    /// (37.5 a unit) - the stone set into the ground, and a CHANNEL cut round it that carries the
    /// stone's light. The face is baked in the place's colours; the channel is white texels the
    /// plate tints by its state (dim, lit, dead, wrong), so the answer reads the same everywhere.
    ///
    ///   TOWER   a round flagstone, bevelled, set flush in the floor
    ///   FOREST  a flat stone with moss over its rim and leaves round it
    ///   SKY     pale marble ringed in gilt, a fine crack across it
    ///   SALT    a six-sided plate of salt, its rim raised in crystal
    ///
    /// The circle the player presses is still <c>radius</c> - the picture never changes the rule.
    /// </summary>
    public static class PlateArt
    {
        const float Ppu = 37.5f;
        static readonly Dictionary<int, Sprite> _cache = new();

        /// <summary>Inner and outer edge of the light's channel, as shares of the radius.</summary>
        const float ChannelIn = 0.70f, ChannelOut = 0.86f;

        public static Sprite Face(BiomeKind place, float radius) => Get(place, radius, 0);
        public static Sprite Channel(BiomeKind place, float radius) => Get(place, radius, 1);

        /// <summary>A white texel disc of this world diameter (the pips).</summary>
        public static Sprite Dot(float diameter)
        {
            float r = Mathf.Max(1f, diameter * Ppu * 0.5f);
            int key = 9 << 24 | Mathf.RoundToInt(r * 4f);
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            var px = new Dictionary<Vector2Int, Color>();
            int n = Mathf.CeilToInt(r);
            for (int x = -n; x < n; x++)
            for (int y = -n; y < n; y++)
                if ((x + 0.5f) * (x + 0.5f) + (y + 0.5f) * (y + 0.5f) < r * r) px[new Vector2Int(x, y)] = Color.white;
            return _cache[key] = ToSprite(px);
        }

        static Sprite Get(BiomeKind place, float radius, int layer)
        {
            float r = radius * Ppu;
            int key = (int)place << 20 | layer << 16 | Mathf.RoundToInt(r * 4f);
            if (_cache.TryGetValue(key, out var s) && s != null) return s;
            var px = new Dictionary<Vector2Int, Color>();
            if (layer == 0) DrawFace(px, place, r); else DrawChannel(px, place, r);
            return _cache[key] = ToSprite(px);
        }

        // ---- shape ----

        /// <summary>How far out from the centre the stone's edge is, as a share of r, at (x, y) -
        /// a circle, or the salt's flat-topped hexagon (so its channel follows it).</summary>
        static float Reach(BiomeKind place, float x, float y, float r)
        {
            if (place != BiomeKind.SaltFlats) return Mathf.Sqrt(x * x + y * y) / r;
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            return Mathf.Max(ay, ax * 0.866f + ay * 0.5f) / r;   // apothem r: the circle sits inside it
        }

        static float Hash(int a, int b)
        {
            uint h = (uint)(a * 374761393 + b * 668265263 + 1013);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }

        static void DrawChannel(Dictionary<Vector2Int, Color> px, BiomeKind place, float r)
        {
            int n = Mathf.CeilToInt(r) + 1;
            for (int x = -n; x < n; x++)
            for (int y = -n; y < n; y++)
            {
                float d = Reach(place, x + 0.5f, y + 0.5f, r);
                if (d >= ChannelIn && d < ChannelOut) px[new Vector2Int(x, y)] = Color.white;
            }
        }

        static void DrawFace(Dictionary<Vector2Int, Color> px, BiomeKind place, float r)
        {
            Color face, faceAlt, lit, dark, groove, edge;
            switch (place)
            {
                case BiomeKind.Forest:
                    face = new(0.25f, 0.26f, 0.22f); faceAlt = new(0.29f, 0.30f, 0.25f);
                    lit = new(0.38f, 0.39f, 0.32f); dark = new(0.15f, 0.16f, 0.13f); groove = new(0.08f, 0.08f, 0.06f); edge = new(0.06f, 0.06f, 0.05f); break;
                case BiomeKind.SkyRuin:
                    face = new(0.67f, 0.65f, 0.59f); faceAlt = new(0.72f, 0.70f, 0.64f);
                    lit = new(0.85f, 0.82f, 0.76f); dark = new(0.48f, 0.46f, 0.42f); groove = new(0.30f, 0.27f, 0.22f); edge = new(0.20f, 0.18f, 0.15f); break;
                case BiomeKind.SaltFlats:
                    face = new(0.86f, 0.82f, 0.82f); faceAlt = new(0.90f, 0.87f, 0.86f);
                    lit = new(0.99f, 0.98f, 0.97f); dark = new(0.68f, 0.61f, 0.64f); groove = new(0.46f, 0.40f, 0.43f); edge = new(0.40f, 0.34f, 0.37f); break;
                default:
                    face = new(0.20f, 0.17f, 0.13f); faceAlt = new(0.23f, 0.19f, 0.15f);
                    lit = new(0.32f, 0.27f, 0.21f); dark = new(0.12f, 0.10f, 0.08f); groove = new(0.06f, 0.05f, 0.04f); edge = new(0.05f, 0.04f, 0.03f); break;
            }
            var gilt = new Color(0.78f, 0.62f, 0.26f); var giltLit = new Color(0.95f, 0.82f, 0.42f);
            var moss = new Color(0.21f, 0.33f, 0.14f); var mossLit = new Color(0.33f, 0.47f, 0.19f);

            int n = Mathf.CeilToInt(r) + 2;
            for (int x = -n; x < n; x++)
            for (int y = -n; y < n; y++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float d = Reach(place, fx, fy, r);
                // The forest's stone is rough-edged: its rim wanders a texel or two.
                float rim = 1f;
                if (place == BiomeKind.Forest)
                {
                    float a = Mathf.Atan2(fy, fx);
                    rim = 1f + (0.05f * Mathf.Sin(a * 3f + 1.3f) + 0.035f * Mathf.Sin(a * 7f + 0.4f));
                }
                float outer = rim + 1.6f / r;       // the dark seam the stone is set in
                if (d >= outer) continue;
                Color c;
                if (d >= rim) c = edge;
                else if (d >= rim - 2.2f / r)
                {
                    // The bevel: lit where it faces the upper-left light, dark on the far side.
                    float facing = (-fx + fy) / Mathf.Max(0.01f, Mathf.Sqrt(fx * fx + fy * fy));
                    c = facing > 0.3f ? lit : facing < -0.3f ? dark : face;
                    if (place == BiomeKind.SaltFlats) c = facing > -0.3f ? lit : face;   // a raised crystal rim
                }
                else if (d >= ChannelIn - 1f / r && d < ChannelOut + 1f / r) c = groove;   // the channel's bed
                else
                {
                    c = Hash(x / 2, y / 2) < 0.4f ? faceAlt : face;
                    if (Hash(x, y) < 0.05f) c = Color.Lerp(c, dark, 0.6f);
                    if (Hash(x, y) > 0.97f) c = Color.Lerp(c, lit, 0.6f);
                }

                if (place == BiomeKind.SkyRuin && d >= ChannelOut + 1f / r && d < rim - 2.2f / r)
                    c = (-fx + fy) > 0f ? giltLit : gilt;                                        // the gilt ring
                if (place == BiomeKind.Forest && d >= ChannelOut - 0.05f)
                {
                    float a = Mathf.Atan2(fy, fx);
                    if (Mathf.Sin(a * 2f + 0.8f) + 0.6f * Mathf.Sin(a * 5f) > 0.55f + 0.6f * (d - ChannelOut))
                        c = Hash(x, y) < 0.18f ? mossLit : moss;                                    // moss over the rim
                }
                px[new Vector2Int(x, y)] = c;
            }

            if (place == BiomeKind.SkyRuin)
            {
                // A fine crack across the inner face, a lit lip on its upper side.
                int cx = Mathf.RoundToInt(-r * 0.5f), cy = Mathf.RoundToInt(r * 0.15f);
                for (int i = 0; i < Mathf.RoundToInt(r * 0.6f); i++)
                {
                    var p = new Vector2Int(cx + i, cy - i / 3 - (Hash(i, 3) < 0.3f ? 1 : 0));
                    if (px.ContainsKey(p) && Reach(place, p.x + 0.5f, p.y + 0.5f, r) < ChannelIn - 1.5f / r)
                    {
                        px[p] = groove;
                        var up = p + Vector2Int.up;
                        if (px.ContainsKey(up)) px[up] = lit;
                    }
                }
            }
            if (place == BiomeKind.Forest)
            {
                // Leaves fallen round it.
                var leaf = new Color(0.30f, 0.36f, 0.14f); var leafDry = new Color(0.42f, 0.30f, 0.14f);
                for (int k = 0; k < 7; k++)
                {
                    float a = k * 0.9f + 0.4f + Hash(k, 1);
                    float dist = r * (1.05f + 0.15f * Hash(k, 2));
                    int lx = Mathf.RoundToInt(Mathf.Cos(a) * dist), ly = Mathf.RoundToInt(Mathf.Sin(a) * dist);
                    var col = Hash(k, 5) < 0.5f ? leaf : leafDry;
                    foreach (var o in new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up, new Vector2Int(1, 1), new Vector2Int(2, 1) })
                    {
                        var p = new Vector2Int(lx, ly) + o;
                        if (!px.ContainsKey(p)) px[p] = col;
                    }
                }
            }
        }

        static Sprite ToSprite(Dictionary<Vector2Int, Color> px)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var p in px.Keys)
            {
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            int w = maxX - minX + 1, h = maxY - minY + 1;
            var pixels = new Color[w * h];
            foreach (var kv in px) pixels[(kv.Key.y - minY) * w + (kv.Key.x - minX)] = kv.Value;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(pixels);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(-minX / (float)w, -minY / (float)h), Ppu);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }
}
