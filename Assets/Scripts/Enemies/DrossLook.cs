using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>
    /// A DROSS's colour (Tuning.Dross, the user's pick from the mock-up 2026-11-15): every lesser
    /// body in ONE dull verdigris, the crust on a spent metal - the tier reads at a glance across
    /// kinds, and the silhouette (one per kind) still says which kind. The elite is bigger and
    /// brighter; a Dross is smaller and duller.
    ///
    /// Laid over the finished frames, weathering included: the outline and everything the
    /// weathering keeps (tells, glows, smears, biome accents - CreatureWeathering.Classify) keep
    /// their colour, so a lesser Brute still lights magenta before it swings. The rest maps by
    /// brightness, spread over the WHOLE set's range so a walk never flickers between steps, onto
    /// four steps of the ramp.
    /// </summary>
    public static class DrossLook
    {
        static readonly Color[] Ramp = TexelRig.Hex("#172422", "#2d4843", "#4d776d", "#7fa89a");
        static readonly Dictionary<Sprite, Sprite> _cache = new();

        /// <summary><paramref name="frames"/> in the Dross colour. The array is new; the sprites are
        /// shared through the cache.</summary>
        public static Sprite[] For(Sprite[] frames, Color[] reserved)
        {
            if (frames == null || frames.Length == 0) return frames;
            var o = new Sprite[frames.Length];
            bool all = true;
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] == null || !_cache.TryGetValue(frames[i], out o[i]) || o[i] == null) { all = false; break; }
            if (all) return o;

            float lo = 1f, hi = 0f;
            foreach (var s in frames)
            {
                if (!Readable(s)) continue;
                foreach (var c in Pixels(s))
                    if (CreatureWeathering.Classify(c, reserved) == 3) { float l = Lum(c); lo = Mathf.Min(lo, l); hi = Mathf.Max(hi, l); }
            }
            for (int i = 0; i < frames.Length; i++)
            {
                var s = frames[i];
                if (s == null) continue;
                if (!_cache.TryGetValue(s, out var d) || d == null)
                    _cache[s] = d = Recolour(s, reserved, lo, hi);
                o[i] = d;
            }
            return o;
        }

        static bool Readable(Sprite s) => s != null && s.texture != null && s.texture.isReadable;

        static Color[] Pixels(Sprite s)
        {
            var r = s.rect;
            return s.texture.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
        }

        static Sprite Recolour(Sprite s, Color[] reserved, float lo, float hi)
        {
            if (!Readable(s)) return s;
            var r = s.rect;
            int W = (int)r.width, H = (int)r.height;
            var px = Pixels(s);
            float span = Mathf.Max(1e-3f, hi - lo);
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (CreatureWeathering.Classify(c, reserved) != 3) continue;
                var k = Ramp[Mathf.Clamp((int)((Lum(c) - lo) / span * Ramp.Length), 0, Ramp.Length - 1)];
                px[i] = new Color(k.r, k.g, k.b, c.a);
            }
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave, name = s.name + ".dross",
            };
            t.SetPixels(px);
            t.Apply();
            var d = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(s.pivot.x / W, s.pivot.y / H), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            d.name = t.name;
            d.hideFlags = HideFlags.HideAndDontSave;
            return d;
        }

        static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    }
}
