using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// Low fog drifting over the void just past the room's lip (the user's call, 2026-11-01) - a
    /// few banded puffs that soften where the floor stops. Under everything that stands, so it never
    /// lies over a body; a picture only. Placed and timed from the floor's seed.
    /// </summary>
    public class EdgeFog : MonoBehaviour
    {
        struct Puff { public Transform T; public Vector2 Home; public float Phase, Speed; }
        readonly List<Puff> _puffs = new();
        static readonly Dictionary<int, Sprite> _sprites = new();

        public static void Spawn(Transform parent, float[] dist, int W, int H, Vector2 origin, float ppu, int seed)
        {
            var go = new GameObject("fog");
            go.transform.SetParent(parent, false);
            var fog = go.AddComponent<EdgeFog>();
            var rng = new System.Random(seed * 41 + 9);
            var col = Biome.FogColor;
            int placed = 0;
            for (int tries = 0; tries < 600 && placed < Core.Tuning.Ground.FogPuffs; tries++)
            {
                int i = rng.Next(W), j = rng.Next(H);
                float d = dist[j * W + i];
                if (d < Core.Tuning.Ground.FogNear || d > Core.Tuning.Ground.FogFar) continue;
                var at = origin + new Vector2((i + 0.5f) / ppu, (j + 0.5f) / ppu);
                // Not over the north face - the fog lies over drops, not up a wall.
                if (at.y > Core.Arena.Inner.y - 0.2f) continue;
                int variant = rng.Next(3);
                var p = new GameObject("puff");
                p.transform.SetParent(go.transform, false);
                p.transform.position = at;
                var sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = Sprite(variant);
                sr.color = col;
                sr.sortingOrder = Core.Tuning.Ground.Order + 1;
                fog._puffs.Add(new Puff
                {
                    T = p.transform, Home = at,
                    Phase = (float)rng.NextDouble() * Mathf.PI * 2f,
                    Speed = 0.7f + (float)rng.NextDouble() * 0.6f,
                });
                placed++;
            }
        }

        void Update()
        {
            float t = Time.time * Mathf.PI * 2f / Core.Tuning.Ground.FogSeconds;
            foreach (var p in _puffs)
            {
                if (p.T == null) continue;
                float s = t * p.Speed + p.Phase;
                var at = p.Home + new Vector2(Mathf.Sin(s) * Core.Tuning.Ground.FogDrift, Mathf.Sin(s * 0.6f) * 0.08f);
                // Snapped to the floor's texel grid, so a puff never swims between texels.
                p.T.position = new Vector3(Mathf.Round(at.x * Scenery.Ppu) / Scenery.Ppu, Mathf.Round(at.y * Scenery.Ppu) / Scenery.Ppu, 0f);
            }
        }

        /// <summary>A flat puff, three bands of alpha out from a ragged heart (white, tinted by the
        /// renderer). Wide and low, as fog lying on air would be drawn.</summary>
        static Sprite Sprite(int variant)
        {
            if (_sprites.TryGetValue(variant, out var s) && s != null) return s;
            int w = 96 + variant * 16, h = 30 + variant * 4;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                float wob = Mathf.Sin(x * 0.21f + variant * 2f) * 0.08f + Mathf.Sin(x * 0.07f + variant) * 0.1f;
                float r = Mathf.Sqrt(u * u + (v - wob) * (v - wob));
                byte a = r < 0.45f ? (byte)255 : r < 0.72f ? (byte)150 : r < 0.95f ? (byte)70 : (byte)0;
                px[y * w + x] = new Color32(255, 255, 255, a);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply();
            s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Scenery.Ppu, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            _sprites[variant] = s;
            return s;
        }
    }
}
