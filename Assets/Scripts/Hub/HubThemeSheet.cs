using System;
using UnityEngine;

namespace Convergence.Hub
{
    /// <summary>Testing: one hub item drawn in every theme, side by side, from the game's own code -
    /// to check a port against its mock. Draws in batches (an eval has 5 s); the last call writes.</summary>
    public static class HubThemeSheet
    {
        /// <summary>Builds themes [from, to) of <paramref name="draw"/> into the cache, or with
        /// <paramref name="path"/> set, writes all fourteen to a PNG at 2x.</summary>
        public static string Render(Func<HubTheme, Sprite> draw, int from, int to, string path = null)
        {
            for (int i = from; i < to && i < HubThemes.Count; i++) draw((HubTheme)i);
            if (string.IsNullOrEmpty(path)) return "built " + from + ".." + to;
            int cell = 96, cols = 7, rows = 2, z = 2;
            var outT = new Texture2D(cols * cell * z, rows * cell * z, TextureFormat.RGBA32, false);
            var px = new Color32[outT.width * outT.height];
            var bg = new Color32(60, 56, 64, 255);
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            for (int i = 0; i < HubThemes.Count; i++)
            {
                var s = draw((HubTheme)i);
                if (s == null) continue;
                var r = s.textureRect; var src = s.texture.GetPixels32(); int sw = s.texture.width;
                int ox = (i % cols) * cell, oy = (rows - 1 - i / cols) * cell;
                int px0 = Mathf.RoundToInt(cell / 2f - s.pivot.x), py0 = 8;
                for (int y = 0; y < (int)r.height; y++)
                    for (int x = 0; x < (int)r.width; x++)
                    {
                        var c = src[(int)(r.y + y) * sw + (int)(r.x + x)];
                        if (c.a == 0) continue;
                        int tx = px0 + x, ty = py0 + y - Mathf.RoundToInt(s.pivot.y) + 2;
                        if (tx < 0 || ty < 0 || tx >= cell || ty >= cell) continue;
                        for (int dy = 0; dy < z; dy++) for (int dx = 0; dx < z; dx++)
                            px[((oy + ty) * z + dy) * outT.width + (ox + tx) * z + dx] = c;
                    }
            }
            outT.SetPixels32(px); outT.Apply();
            System.IO.File.WriteAllBytes(path, ImageConversion.EncodeToPNG(outT));
            return "wrote " + path;
        }
    }
}
