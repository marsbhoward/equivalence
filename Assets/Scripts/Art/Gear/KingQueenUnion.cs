using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// The UNION of the Aether Dual Discs (the user's call, 2026-10-16): King and Queen's two
    /// halves joined into ONE disc at the top of the art's shadow step - the lion's spirit (his head
    /// on his ring of energy) with the lioness's HEAD, mirrored to face him, set on the same ring
    /// opposite. Built at runtime from the two spirit pictures (GearSpirit), texel for texel, so the
    /// Secret Fire marks keep their reserved colours and KindledMarks lights them as ever.
    ///
    /// Her head is cut from her spirit picture by its bounds (<see cref="HeadMin"/>..<see cref="HeadMax"/>,
    /// read off the picture - an automatic split by the ring's band took a strip of ring with only
    /// part of the head), mirrored, and laid over the open top-right of his ring.
    /// </summary>
    public static class KingQueenUnion
    {
        /// <summary>Her head in her spirit picture (texels, inclusive) - read off the picture: the
        /// mane and crown above where her ring runs into it.</summary>
        public static Vector2Int HeadMin = new(36, 58), HeadMax = new(76, 84);
        /// <summary>Where her mirrored head goes in his picture: added to its mirrored position.</summary>
        public static Vector2Int Shift = new(28, -4);

        // Non-readonly: rebuilt after a domain reload.
        static Dictionary<(Sprite, Sprite), Sprite> _cache = new();

        public static Sprite Of(Sprite lion, Sprite lioness)
        {
            if (lion == null || lioness == null) return lion;
            if (_cache.TryGetValue((lion, lioness), out var s) && s != null) return s;
            s = Build(lion, lioness);
            _cache[(lion, lioness)] = s;
            return s;
        }

        public static void Clear() => _cache.Clear();

        static Color32[] Read(Sprite sp, out int w, out int h)
        {
            var r = sp.rect;
            w = Mathf.RoundToInt(r.width); h = Mathf.RoundToInt(r.height);
            var all = sp.texture.GetPixels32();
            int tw = sp.texture.width;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = all[((int)r.y + y) * tw + (int)r.x + x];
            return px;
        }

        static float Lum(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;

        static Sprite Build(Sprite lion, Sprite lioness)
        {
            var a = Read(lion, out int w, out int h);
            var b = Read(lioness, out int bw, out int bh);
            var px = (Color32[])a.Clone();
            // Her head, mirrored so she faces him, laid over his ring's open top-right: the lit
            // texels of it (mane, face, crown - not the dark of her ring, which ran with it and left
            // a hard-edged block) and the one-texel dark outline round them.
            bool Lit(int x, int y)
            {
                if (x < HeadMin.x || y < HeadMin.y || x > HeadMax.x || y > HeadMax.y || x >= bw || y >= bh) return false;
                var c = b[y * bw + x];
                return c.a >= 8 && Lum(c) > 0.25f;
            }
            for (int y = HeadMin.y; y <= HeadMax.y && y < bh; y++)
                for (int x = HeadMin.x; x <= HeadMax.x && x < bw; x++)
                {
                    var c = b[y * bw + x];
                    if (c.a < 8) continue;
                    if (!Lit(x, y))
                    {
                        bool edge = false;
                        for (int oy = -1; oy <= 1 && !edge; oy++)
                            for (int ox = -1; ox <= 1; ox++)
                                if (Lit(x + ox, y + oy)) { edge = true; break; }
                        if (!edge) continue;
                    }
                    int nx = (bw - 1 - x) + Shift.x, ny = y + Shift.y;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    px[ny * w + nx] = c;
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "kq.union" };
            tex.SetPixels32(px);
            tex.Apply(false);
            var pivot = new Vector2(lion.pivot.x / w, lion.pivot.y / h);
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, lion.pixelsPerUnit);
        }
    }
}
