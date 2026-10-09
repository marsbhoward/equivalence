using System.IO;
using UnityEngine;
using Convergence.Art;

namespace Convergence.EditorTools
{
    /// <summary>
    /// A contact sheet of every loot box (Art.BoxArt) - each kind closed above open, on a dark
    /// ground, nearest-neighbour enlarged - for judging the art from the CLI:
    ///
    ///     unity command eval --code 'return Convergence.EditorTools.BoxSheet.Capture("/tmp/boxes.png", true, 2);'
    /// </summary>
    public static class BoxSheet
    {
        public static string Capture(string path, bool menu = true, int scale = 2)
        {
            var kinds = (BoxArt.Kind[])System.Enum.GetValues(typeof(BoxArt.Kind));
            var closed = new Sprite[kinds.Length];
            var open = new Sprite[kinds.Length];
            int cellW = 0, rowH0 = 0, rowH1 = 0;
            for (int i = 0; i < kinds.Length; i++)
            {
                closed[i] = BoxArt.Closed(kinds[i], menu);
                open[i] = BoxArt.Open(kinds[i], menu);
                cellW = Mathf.Max(cellW, (int)closed[i].rect.width, (int)open[i].rect.width);
                rowH0 = Mathf.Max(rowH0, (int)closed[i].rect.height);
                rowH1 = Mathf.Max(rowH1, (int)open[i].rect.height);
            }
            int pad = 8;
            int w = (cellW + pad) * kinds.Length + pad, h = rowH0 + rowH1 + pad * 3;
            var px = new Color[w * h];
            var ground = new Color(0.10f, 0.10f, 0.13f);
            for (int i = 0; i < px.Length; i++) px[i] = ground;

            for (int i = 0; i < kinds.Length; i++)
            {
                int x0 = pad + i * (cellW + pad);
                Blit(px, w, open[i], x0 + (cellW - (int)open[i].rect.width) / 2, pad);
                Blit(px, w, closed[i], x0 + (cellW - (int)closed[i].rect.width) / 2, pad * 2 + rowH1);
            }

            var tex = new Texture2D(w * scale, h * scale, TextureFormat.RGBA32, false);
            var big = new Color[w * scale * h * scale];
            for (int y = 0; y < h * scale; y++)
            for (int x = 0; x < w * scale; x++)
                big[y * w * scale + x] = px[(y / scale) * w + x / scale];
            tex.SetPixels(big);
            tex.Apply(false);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return $"{kinds.Length} kinds, {(menu ? "menu" : "arena")} density -> {path}";
        }

        /// <summary>One kind, closed beside open, large.</summary>
        public static string One(BoxArt.Kind kind, string path, bool menu = true, int scale = 4)
        {
            var a = BoxArt.Closed(kind, menu);
            var b = BoxArt.Open(kind, menu);
            int pad = 6, w = (int)a.rect.width + (int)b.rect.width + pad * 3;
            int h = Mathf.Max((int)a.rect.height, (int)b.rect.height) + pad * 2;
            var px = new Color[w * h];
            var ground = new Color(0.10f, 0.10f, 0.13f);
            for (int i = 0; i < px.Length; i++) px[i] = ground;
            Blit(px, w, a, pad, pad);
            Blit(px, w, b, pad * 2 + (int)a.rect.width, pad);
            var tex = new Texture2D(w * scale, h * scale, TextureFormat.RGBA32, false);
            var big = new Color[w * scale * h * scale];
            for (int y = 0; y < h * scale; y++)
            for (int x = 0; x < w * scale; x++)
                big[y * w * scale + x] = px[(y / scale) * w + x / scale];
            tex.SetPixels(big);
            tex.Apply(false);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return $"{kind} -> {path}";
        }

        static void Blit(Color[] px, int w, Sprite s, int ox, int oy)
        {
            var r = s.rect;
            var src = s.texture.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
            for (int y = 0; y < (int)r.height; y++)
            for (int x = 0; x < (int)r.width; x++)
            {
                var c = src[y * (int)r.width + x];
                if (c.a <= 0f) continue;
                int k = (oy + y) * w + ox + x;
                px[k] = Color.Lerp(px[k], new Color(c.r, c.g, c.b, 1f), c.a);
            }
        }
    }
}
