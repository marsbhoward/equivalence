using System.Collections.Generic;
using UnityEngine;
using Convergence.Art;

namespace Convergence.EditorTools
{
    /// <summary>
    /// Eight-direction hair review (RIG.md): each style in four columns - turned (today's front and front 3/4),
    /// SIDE, BACK 3/4, back - every one through the game's own head composition, hair pass and palette.
    /// The new views' grids come from a proposals file (Tools/hair/views5.py writes the drafts; hand fixes edit it):
    ///
    ///     @key
    ///     side.cap            18 rows of 16 cells
    ///     side.mane 44        rows in cells; the number is maneTop, texels
    ///     q.cap
    ///     q.mane 44
    ///
    ///     unity command eval --code 'return Convergence.EditorTools.HairViews5.Sheet("/abs/props.txt", "/abs/out.png", "shock", "brown,black,blonde", 3);'
    ///
    /// Works in edit mode; nothing is registered with the game.
    /// </summary>
    public static class HairViews5
    {
        sealed class Props { public string[] SideCap, SideMane, QCap, QMane, TCap, TMane, BCap, BMane; public bool BSet; public int SideTop = BodyLook.ManeHeadTop, QTop = BodyLook.ManeHeadTop, TTop = BodyLook.ManeHeadTop, BTop = BodyLook.ManeHeadTop; }

        static string[] Double(List<string> cells)
        {
            if (cells == null || cells.Count == 0) return null;
            var rows = new string[cells.Count * 2];
            for (int y = 0; y < cells.Count; y++)
            {
                var sb = new System.Text.StringBuilder();
                foreach (char c in cells[y]) sb.Append(c, 2);
                rows[y * 2] = rows[y * 2 + 1] = sb.ToString();
            }
            return rows;
        }

        static Dictionary<string, Props> Load(string path)
        {
            var all = new Dictionary<string, Props>();
            string key = null, part = null; var parts = new Dictionary<string, List<string>>(); var tops = new Dictionary<string, int>();
            void Flush()
            {
                if (key == null) return;
                List<string> G(string p) => parts.TryGetValue(p, out var r) ? r : null;
                var pr = new Props { SideCap = Double(G("side.cap")), SideMane = Double(G("side.mane")), QCap = Double(G("q.cap")), QMane = Double(G("q.mane")), TCap = Double(G("turned.cap")), TMane = Double(G("turned.mane")), BCap = Double(G("back.cap")), BMane = Double(G("back.mane")), BSet = parts.ContainsKey("back.cap") };
                if (tops.TryGetValue("turned.mane", out var tt)) pr.TTop = tt;
                if (tops.TryGetValue("back.mane", out var bt)) pr.BTop = bt;
                if (tops.TryGetValue("side.mane", out var a)) pr.SideTop = a;
                if (tops.TryGetValue("q.mane", out var b)) pr.QTop = b;
                all[key] = pr;
            }
            foreach (var raw in System.IO.File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("@")) { Flush(); key = line.Substring(1).Trim(); parts = new(); tops = new(); part = null; continue; }
                if (line.StartsWith("side.") || line.StartsWith("q.") || line.StartsWith("turned.") || line.StartsWith("back."))
                {
                    var sp = line.Split(' '); part = sp[0]; parts[part] = new List<string>();
                    if (sp.Length > 1) tops[part] = int.Parse(sp[1]);
                    continue;
                }
                if (part != null) parts[part].Add(line);
            }
            Flush();
            return all;
        }

        /// <param name="keys">Comma-separated styles.</param>
        /// <param name="hairs">Comma-separated hair swatches - one row block per swatch.</param>
        /// <param name="px">Screen pixels per detail texel.</param>
        public static string Sheet(string propsPath, string outPath, string keys, string hairs = "brown", int px = 3,
                                   string expression = "neutral", string brows = "natural", string skin = "fair", string eyes = "slate")
        {
            var props = Load(propsPath);
            int s = BodyLook.DetailScale, headW = 32 * s, cellW = 64 * s, cellH = 76 * s;
            var keyList = keys.Split(','); var hairList = hairs.Split(',');
            int W = cellW * 4 * hairList.Length, H = cellH * keyList.Length;
            var img = new Color32[W * H];
            var bg = new Color32(76, 80, 90, 255);
            for (int i = 0; i < img.Length; i++) img[i] = bg;

            void Blit(string[] rows, Dictionary<char, Color> pal, int ox, int oy)
            {
                if (rows == null) return;
                for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    char c = rows[y][x];
                    if (c == '.' || !pal.TryGetValue(c, out var col)) continue;
                    int X = ox + x, Y = oy + y;
                    if (X < 0 || Y < 0 || X >= W || Y >= H) continue;
                    img[(H - 1 - Y) * W + X] = col;
                }
            }

            for (int k = 0; k < keyList.Length; k++)
            for (int hcol = 0; hcol < hairList.Length; hcol++)
            {
                string key = BodyLook.HairStyle(keyList[k].Trim()).Key, hair = hairList[hcol].Trim();
                var pal = BodyLook.HeadPalette(BodyLook.SkinRamp(skin), BodyLook.HairRamp(hair), BodyLook.EyeRamp(eyes));
                props.TryGetValue(keyList[k].Trim(), out var pr);
                var turned = BodyLook.View(key, back: false); var back = BodyLook.View(key, back: true);
                // (head rows, mane rows, mane top, mane over the head)
                var cols = new (string[] head, string[] mane, int top, bool over)[]
                {
                    pr?.TCap != null
                        ? (BodyLook.HeadTurnedDetail(expression, brows, hair, pr.TCap, pr.TMane != null, mane: pr.TMane, maneTop: pr.TTop), BodyLook.ManeFromRows(pr.TMane, pr.TTop, hair), pr.TTop, false)
                        : (BodyLook.HeadDetail(expression, key, "none", brows, hair, gaze: 2), BodyLook.ManeDetail(key, false, hair), turned?.ManeTop ?? 32, false),
                    (pr?.SideCap != null ? BodyLook.HeadProfileDetail(expression, brows, hair, pr.SideCap, pr.SideMane != null, mane: pr.SideMane, maneTop: pr.SideTop) : null,
                     BodyLook.ManeFromRows(pr?.SideMane, pr?.SideTop ?? 32, hair), pr?.SideTop ?? 32, false),
                    (pr?.QCap != null ? BodyLook.HeadBackQuarterDetail(hair, pr.QCap, pr.QMane != null) : null,
                     BodyLook.ManeFromRows(pr?.QMane, pr?.QTop ?? 32, hair), pr?.QTop ?? 32, true),
                    pr != null && pr.BSet
                        ? (BodyLook.HeadBackQuarterDetail(hair, pr.BCap, pr.BMane != null), BodyLook.ManeFromRows(pr.BMane, pr.BTop, hair), pr.BTop, true)
                        : (BodyLook.HeadBackDetail(key, hair, s, false), BodyLook.ManeDetail(key, false, hair, back: true), back?.ManeTop ?? 32, true),
                };
                for (int v = 0; v < 4; v++)
                {
                    int ox = (hcol * 4 + v) * cellW + (cellW - headW) / 2, oy = k * cellH + 14 * s;
                    var c = cols[v];
                    int mw = c.mane != null && c.mane.Length > 0 ? c.mane[0].Length : 0;
                    int mox = ox + (headW - mw) / 2, moy = oy + (32 - c.top) * s;
                    if (!c.over) Blit(c.mane, pal, mox, moy);
                    Blit(c.head, pal, ox, oy);
                    if (c.over) Blit(c.mane, pal, mox, moy);
                }
            }

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(img); tex.Apply();
            // scale up by px, point
            var big = new Texture2D(W * px, H * px, TextureFormat.RGBA32, false);
            var src = tex.GetPixels32(); var dst = new Color32[W * px * H * px];
            for (int y = 0; y < H * px; y++) for (int x = 0; x < W * px; x++) dst[y * W * px + x] = src[(y / px) * W + x / px];
            big.SetPixels32(dst); big.Apply();
            System.IO.File.WriteAllBytes(outPath, big.EncodeToPNG());
            Object.DestroyImmediate(tex); Object.DestroyImmediate(big);
            return $"{W * px}x{H * px}";
        }
    }
}
