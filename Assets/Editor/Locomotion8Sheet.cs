using UnityEngine;
using Convergence.Art;
using Convergence.Art.Gear;

namespace Convergence.EditorTools
{
    /// <summary>
    /// Phase 2 check: Locomotion8's legs in the eight directions at four phases, the mock's own still layout
    /// (Tools/views5/mock/mock8.js STILL=run|walk): rows are phases 0, 1/8, 2/8, 3/8, columns N NE E SE S SW W NW.
    ///     unity command eval --code 'return Convergence.EditorTools.Locomotion8Sheet.Still("/abs/out.png", "run", 2);'
    /// </summary>
    public static class Locomotion8Sheet
    {
        public static string Still(string outPath, string gait = "run", int px = 2, float size = 1f)
        {
            var g = gait == "walk" ? Locomotion8.Walk : Locomotion8.Run;
            float[] dirs = { 90f, 45f, 0f, -45f, -90f, -135f, 180f, 135f };   // N NE E SE S SW W NW (degrees, up = away)
            float[] phases = { 0f, 0.125f, 0.25f, 0.375f };
            int cw = Locomotion8.CanvasW, ch = Locomotion8.CanvasH, W = cw * dirs.Length, H = ch * phases.Length;
            var pal = Palette.Of(new Palette.Ramp(new Color(0.24f, 0.24f, 0.27f), 0.28f));
            var img = new Color32[W * H];
            var bg = new Color32(74, 78, 87, 255);
            for (int i = 0; i < img.Length; i++) img[i] = bg;
            for (int r = 0; r < phases.Length; r++)
            for (int d = 0; d < dirs.Length; d++)
            {
                var rows = Locomotion8.DrawLegs(Locomotion8.PoseAt(dirs[d], g, phases[r], size));
                for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    if (rows[y][x] == '.' || !pal.TryGetValue(rows[y][x], out var col)) continue;
                    int X = d * cw + x, Y = r * ch + y;
                    img[(H - 1 - Y) * W + X] = col;
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
