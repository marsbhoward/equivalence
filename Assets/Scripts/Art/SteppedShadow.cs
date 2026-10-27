using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art
{
    /// <summary>
    /// A contact shadow cut on the texel grid in two hard bands - a fainter outer ellipse and a
    /// darker core (group 7, the user's pick "stepped", the light pools' hard steps). It replaced
    /// the soft <c>Spr.Circle</c> under the weapon rack, the armour stand, the standing frames and
    /// the spire. White texels: the renderer's colour (black, with the shadow's strength as alpha)
    /// does the rest, so a caller can still fade it by its alpha.
    /// </summary>
    public static class SteppedShadow
    {
        static readonly Dictionary<long, Sprite> Cache = new();

        /// <summary>Put the shadow on <paramref name="sr"/>: <paramref name="w"/> x
        /// <paramref name="h"/> world units at <paramref name="ppu"/>, centred on its transform.</summary>
        public static void Apply(SpriteRenderer sr, float w, float h, float ppu)
        {
            sr.sprite = For(Mathf.Max(3, Mathf.RoundToInt(w * ppu)), Mathf.Max(3, Mathf.RoundToInt(h * ppu)), ppu);
            sr.transform.localScale = Vector3.one;
        }

        static Sprite For(int tw, int th, float ppu)
        {
            long key = ((long)tw << 32) | ((long)th << 16) | (long)Mathf.RoundToInt(ppu * 8f);
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var px = new Color32[tw * th];
            float cx = (tw - 1) * 0.5f, cy = (th - 1) * 0.5f, rx = tw * 0.5f, ry = th * 0.5f;
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    float d = Mathf.Sqrt(((x - cx) / rx) * ((x - cx) / rx) + ((y - cy) / ry) * ((y - cy) / ry));
                    if (d > 1f) continue;
                    px[y * tw + x] = new Color32(255, 255, 255, (byte)(d < 0.6f ? 255 : 120));
                }
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply();
            return Cache[key] = Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        }
    }
}
