using UnityEngine;

namespace Convergence.Art
{
    /// <summary>
    /// The arena's BRICK FLOOR - procedural, like PitArt, and for the same two reasons: POINT
    /// filtering (bilinear smears a one-pixel mortar line into a grey haze) and TILING at a fixed
    /// density, so a brick is the same size on screen whatever the arena's footprint.
    ///
    /// After the user's reference (a clear pixel game): a warm, MID-VALUE, LOW-CONTRAST floor. That
    /// is what lets every sprite on it read - the near-black floor this replaced sat dark gear on
    /// dark ground, and its bright grid lines competed with the characters for attention. The
    /// bricks also do the grid's one job (showing movement), so the grid goes when this is used.
    ///
    /// ONE TEXEL IS ONE BODY ART PIXEL (37.5 per unit): the floor shares the characters' pixel
    /// size rather than adding a third one. The tile is 128 x 64 texels of running-bond bricks
    /// (16 x 8, one-pixel mortar), every brick its own slight tone, lit on its top-left edge and
    /// shaded on its bottom-right, with a little speckle and the odd chip or crack - variation
    /// inside a narrow value range, so it textures the ground without drawing the eye.
    /// </summary>
    public static class FloorArt
    {
        const int TileW = 128, TileH = 64;
        const int BrickW = 16, BrickH = 8;
        /// <summary>Texels per world unit - the body's art density.</summary>
        public const float PixelsPerUnit = 37.5f;

        static readonly Color Mortar = new(0.31f, 0.25f, 0.18f);
        static readonly Color Brick = new(0.55f, 0.45f, 0.31f);

        static Sprite _bricks;

        /// <summary>The tile, built once. FullRect, so a SpriteRenderer can draw it Tiled.</summary>
        public static Sprite Bricks => _bricks != null ? _bricks : (_bricks = Build());

        static float Hash(int a, int b)
        {
            unchecked
            {
                int h = a * 374761393 + b * 668265263 + 1013904223;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static Color Shade(Color c, float v) => new(
            Mathf.Clamp01(c.r + v), Mathf.Clamp01(c.g + v * 0.92f), Mathf.Clamp01(c.b + v * 0.8f), 1f);

        static Sprite Build()
        {
            var px = new Color[TileW * TileH];
            for (int ty = 0; ty < TileH; ty++)          // ty counts DOWN from the top, as drawn
            for (int tx = 0; tx < TileW; tx++)
            {
                int row = ty / BrickH;
                int shift = (row & 1) * (BrickW / 2);    // running bond - wraps within the tile
                int bx = (tx + shift) % TileW;
                int col = bx / BrickW;
                int lx = bx % BrickW, ly = ty % BrickH;
                int id = col * 31 + row * 7;

                Color c;
                bool mortar = lx == BrickW - 1 || ly == BrickH - 1;
                if (mortar) c = Shade(Mortar, (Hash(tx, ty) - 0.5f) * 0.03f);
                else
                {
                    // the brick's own tone - a little warmer or cooler, lighter or darker
                    float tone = (Hash(id, 11) - 0.5f) * 0.09f;
                    var b = Shade(Brick, tone);
                    b.r += (Hash(id, 23) - 0.5f) * 0.03f;

                    float v = 0f;
                    if (ly == 0) v += 0.05f;                              // lit top edge
                    if (lx == 0) v += 0.03f;                              // lit left edge
                    if (ly == BrickH - 2) v -= 0.05f;                     // shaded bottom edge
                    if (lx == BrickW - 2) v -= 0.035f;                    // shaded right edge

                    float speck = Hash(tx * 3 + 1, ty * 5 + 2);
                    if (speck < 0.05f) v -= 0.045f;
                    else if (speck > 0.97f) v += 0.03f;

                    // a chipped corner on some bricks, a short crack on a few
                    if (Hash(id, 41) < 0.18f && lx >= BrickW - 4 && ly <= 1) v -= 0.09f;
                    if (Hash(id, 59) < 0.12f)
                    {
                        int cx = 3 + (int)(Hash(id, 61) * (BrickW - 8));
                        if (ly >= 2 && ly <= 5 && lx == cx + (ly - 2)) v -= 0.10f;
                    }
                    c = Shade(b, v);
                }
                px[(TileH - 1 - ty) * TileW + tx] = c;      // texture rows start at the bottom
            }

            var tex = new Texture2D(TileW, TileH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, TileW, TileH), new Vector2(0.5f, 0.5f),
                                 PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }
}
