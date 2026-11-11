using System.Collections.Generic;
using UnityEngine;
using Convergence.Art;

namespace Convergence.EditorTools
{
    /// <summary>
    /// Eight-direction FACE review (RIG.md): brows, beards and marks in four columns - turned (today's), SIDE,
    /// BACK 3/4, back (today's) - through the game's own head composition, hair pass and palette, on a hairstyle's
    /// approved views (Tools/hair/views5). The new views' grids come from a props file (Tools/faces/&lt;kind&gt;.txt):
    ///
    ///     @key
    ///     side 14            the first composed row, then the rows (32 wide); omitted = the draft
    ///     q 22
    ///
    ///     unity command eval --code 'return Convergence.EditorTools.FaceViews5.Sheet("/abs/brow.txt", "/abs/out.png", "brow", "flat,arched", "brown", 3);'
    ///
    /// Drafts(kind, keys, out) writes the generated first drafts in the same format, for hand fixing.
    /// </summary>
    public static class FaceViews5
    {
        static Dictionary<string, (string[] side, string[] q, string[] sideL)> Load(string path)
        {
            var all = new Dictionary<string, (string[] side, string[] q, string[] sideL)>();
            if (path == null || !System.IO.File.Exists(path)) return all;
            string key = null, part = null; int start = 0; List<string> rows = null;
            string[] side = null, q = null, sideL = null;
            string[] Place(List<string> r, int at)
            {
                var g = new string[36];
                for (int i = 0; i < g.Length; i++) g[i] = new string('.', 32);
                for (int i = 0; i < r.Count && at + i < g.Length; i++) g[at + i] = (r[i] + new string('.', 32)).Substring(0, 32);
                return g;
            }
            void Close() { if (part == "side") side = Place(rows, start); else if (part == "q") q = Place(rows, start); else if (part == "sideL") sideL = Place(rows, start); part = null; }
            void Flush() { Close(); if (key != null) all[key] = (side, q, sideL); side = q = sideL = null; }
            foreach (var raw in System.IO.File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("@")) { Flush(); key = line.Substring(1).Trim(); continue; }
                var sp = line.Split(' ');
                if ((sp[0] == "side" || sp[0] == "q" || sp[0] == "sideL") && sp.Length == 2 && int.TryParse(sp[1], out var at))
                { Close(); part = sp[0]; start = at; rows = new List<string>(); continue; }
                if (part != null) rows.Add(line);
            }
            Flush();
            return all;
        }

        public static string Drafts(string kind, string keys, string outPath)
        {
            var sb = new System.Text.StringBuilder($"# {kind}: first drafts (FaceViews5.Drafts)\n");
            foreach (var k in keys.Split(','))
            {
                sb.Append('@').Append(k).Append('\n');
                Band(sb, "side", BodyLook.DraftSide(kind, k));
                if (BodyLook.Asymmetric(kind, k)) Band(sb, "sideL", BodyLook.DraftSide(kind, k, otherWay: true));
                Band(sb, "q", BodyLook.DraftBackQuarter(kind, k));
            }
            System.IO.File.WriteAllText(outPath, sb.ToString());
            return outPath;
        }

        static string[] MirrorOrNull(string[] g) => g == null ? null : BodyLook.Mirror(g);

        static void Band(System.Text.StringBuilder sb, string name, string[] g)
        {
            int a = -1, b = -1;
            for (int y = 0; y < g.Length; y++) if (g[y].Trim('.').Length > 0) { if (a < 0) a = y; b = y; }
            if (a < 0) { sb.Append(name).Append(" 0\n"); return; }
            sb.Append(name).Append(' ').Append(a).Append('\n');
            for (int y = a; y <= b; y++) sb.Append(g[y]).Append('\n');
        }

        public static string Sheet(string propsPath, string outPath, string kind, string keys, string hairs = "brown",
                                   int px = 3, string style = "fade", string expression = "neutral",
                                   string hairProps = "/Users/marshoward/Coalescence/Tools/hair/views5/")
        {
            var props = Load(propsPath);
            var hp = HairViews5.Load(hairProps + style + ".txt");
            hp.TryGetValue(style, out var h);
            int s = BodyLook.DetailScale, headW = 32 * s, cellW = 64 * s, cellH = 76 * s;
            var keyList = keys.Split(','); var hairList = hairs.Split(',');
            int W = cellW * 6 * hairList.Length, H = cellH * keyList.Length;
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
            string skey = BodyLook.HairStyle(style).Key;
            for (int k = 0; k < keyList.Length; k++)
            for (int hc = 0; hc < hairList.Length; hc++)
            {
                string key = keyList[k].Trim(), hair = hairList[hc].Trim();
                var pal = BodyLook.HeadPalette(BodyLook.SkinRamp("fair"), BodyLook.HairRamp(hair), BodyLook.EyeRamp("slate"));
                props.TryGetValue(key, out var pr);
                var sideG = pr.side ?? BodyLook.DraftSide(kind, key, expression);
                var qG = pr.q ?? BodyLook.DraftBackQuarter(kind, key);
                var sideLG = pr.sideL ?? (BodyLook.Asymmetric(kind, key) ? BodyLook.DraftSide(kind, key, expression, otherWay: true) : sideG);
                string beard = kind == "beard" ? key : "none", brow = kind == "brow" ? key : "natural", mark = kind == "mark" ? key : "none";
                var turned = BodyLook.View(skey, back: false); var back = BodyLook.View(skey, back: true);
                // the turned head facing the OTHER way: drawn from the mirrored part, then mirrored for display
                string other = key;
                if (BodyLook.Asymmetric(kind, key) && kind != "beard")
                {
                    other = key + "~";
                    var src = kind == "brow" ? BodyLook.Brow(key).Grid : BodyLook.Mark(key).Grid;
                    string joined = string.Join("|", BodyLook.Mirror(src));
                    if (kind == "brow") BodyLook.DevAddBrow(other, other, joined); else BodyLook.DevAddMark(other, other, joined);
                }
                var cols = new (string[] head, string[] mane, int top, bool over)[]
                {
                    (BodyLook.HeadDetail(expression, skey, beard, brow, hair, gaze: 2, mark: mark),
                     BodyLook.ManeDetail(skey, false, hair), turned?.ManeTop ?? 32, false),
                    (BodyLook.HeadProfileFull(expression, hair, h?.SideCap, h?.SideMane != null,
                         kind == "brow" ? sideG : null, kind == "beard" ? sideG : null, kind == "mark" ? sideG : null,
                         h?.SideMane, h?.SideTop ?? 32),
                     BodyLook.ManeFromRows(h?.SideMane, h?.SideTop ?? 32, hair), h?.SideTop ?? 32, false),
                    (BodyLook.Mirror(BodyLook.HeadDetail(expression, skey, beard, kind == "brow" ? other : brow, hair, gaze: 2,
                         mark: kind == "mark" ? other : mark)),
                     MirrorOrNull(BodyLook.ManeDetail(skey, false, hair)), turned?.ManeTop ?? 32, false),
                    (BodyLook.Mirror(BodyLook.HeadProfileFull(expression, hair, h?.SideCap, h?.SideMane != null,
                         kind == "brow" ? sideLG : null, kind == "beard" ? sideLG : null, kind == "mark" ? sideLG : null,
                         h?.SideMane, h?.SideTop ?? 32)),
                     MirrorOrNull(BodyLook.ManeFromRows(h?.SideMane, h?.SideTop ?? 32, hair)), h?.SideTop ?? 32, false),
                    (BodyLook.HeadBackQuarterFull(hair, h?.QCap, h?.QMane != null,
                         kind == "beard" ? qG : null, kind == "mark" ? qG : null),
                     BodyLook.ManeFromRows(h?.QMane, h?.QTop ?? 32, hair), h?.QTop ?? 32, true),
                    (BodyLook.HeadBackDetail(skey, hair, s, false), BodyLook.ManeDetail(skey, false, hair, back: true), back?.ManeTop ?? 32, true),
                };
                for (int v = 0; v < 6; v++)
                {
                    int ox = (hc * 6 + v) * cellW + (cellW - headW) / 2, oy = k * cellH + 14 * s;
                    var c = cols[v];
                    int mw = c.mane != null && c.mane.Length > 0 ? c.mane[0].Length : 0;
                    int mox = ox + (headW - mw) / 2, moy = oy + (32 - c.top) * s;
                    if (!c.over) Blit(c.mane, pal, mox, moy);
                    Blit(c.head, pal, ox, oy);
                    if (c.over) Blit(c.mane, pal, mox, moy);
                }
            }
            var tex = new Texture2D(W * px, H * px, TextureFormat.RGBA32, false);
            var dst = new Color32[W * px * H * px];
            for (int y = 0; y < H * px; y++) for (int x = 0; x < W * px; x++) dst[y * W * px + x] = img[(y / px) * W + x / px];
            tex.SetPixels32(dst); tex.Apply();
            System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return $"{W * px}x{H * px}";
        }
    }
}
