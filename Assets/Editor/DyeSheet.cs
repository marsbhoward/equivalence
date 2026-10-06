using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.EditorTools
{
    /// <summary>
    /// A contact sheet of one Diamond armour SET in every dye, worn whole on real rigs - for
    /// judging the dye recipe (does dyed silver still read as metal, dyed cloth as cloth) before
    /// any of it has a UI. GearSheet's Play-mode reason (the rig poses itself in its own Update),
    /// so two calls, a moment apart:
    ///
    ///     unity command eval 'return Convergence.EditorTools.DyeSheet.Setup("orichalc_");'
    ///     unity command eval 'return Convergence.EditorTools.DyeSheet.Capture("/tmp/dye.png", 2);'
    ///
    /// Columns: as authored, then every swatch in DyeCatalog order. Rows: the swatch on the MAIN
    /// channel, then on the ACCENT. Each dyed piece is registered in the catalogue under its
    /// <c>id@dye...</c> id, so the figures go through the ordinary loadout path.
    /// </summary>
    public static class DyeSheet
    {
        const string RootName = "dye-sheet";
        static readonly Vector2 Origin = new(9000f, 5000f);
        const float CellW = 0.8f, CellH = 1.0f;

        static int _cols, _rows;

        public static string Setup(string setPrefix)
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var designs = GearCatalog.All.Values
                .Where(i => i.ItemId.StartsWith(setPrefix) && i.ItemId.IndexOf('@') < 0
                         && i.DyeChannels is { Length: > 0 })
                .OrderBy(i => i.Slot).ToList();
            if (designs.Count == 0) return $"no dyeable items start with '{setPrefix}'";

            var root = new GameObject(RootName);
            _rows = designs.Max(d => d.DyeChannels.Length);
            _cols = DyeCatalog.All.Length + 1;

            for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
            {
                var swatch = c == 0 ? null : DyeCatalog.All[c - 1];
                var go = new GameObject($"{r}.{c}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(Origin.x + c * CellW, Origin.y - r * CellH, 0f);

                var rig = CharacterRigFactory.Build(go, ElementType.Fire, 0);
                rig.SetAppearance(new Appearance());
                var loadout = new Loadout();
                foreach (var d in designs)
                {
                    var dyes = new string[d.DyeChannels.Length];
                    if (swatch != null && r < dyes.Length) dyes[r] = swatch.Id;
                    var worn = GearDye.DyedCopy(d, dyes);
                    if (worn != d) GearCatalog.Register(worn);
                    loadout.Set(worn.Slot, worn.ItemId);
                }
                rig.Apply(loadout);
                rig.SetContactShadow(false);
                rig.SetFacing(Vector2.right);
            }

            var chNames = string.Join(", ", designs[0].DyeChannels.Select(ch => $"{ch.Name} ({ch.Material})"));
            return $"{designs.Count} pieces [{string.Join(" ", designs.Select(d => d.ItemId))}]; " +
                   $"rows: {chNames}; columns: authored | {string.Join(" | ", DyeCatalog.All.Select(s => s.Name))}";
        }

        /// <param name="pxPerTexel">Screen pixels per BODY texel (BodyPpu).</param>
        public static string Capture(string path, int pxPerTexel = 1)
        {
            if (GameObject.Find(RootName) == null) return "run Setup first";

            float ppu = Art.PixelSprite.FinestUnit * pxPerTexel;
            int w = Mathf.RoundToInt(_cols * CellW * ppu), h = Mathf.RoundToInt(_rows * CellH * ppu);

            var camGo = new GameObject("dye-sheet-cam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = _rows * CellH * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            // The arena floor - what a dyed piece has to read against in play.
            cam.backgroundColor = new Color(0.100f, 0.110f, 0.140f);
            float cx = Origin.x + (_cols - 1) * CellW * 0.5f;
            // A figure spans about -0.46..+0.36 around its root; centre on that.
            float cy = Origin.y - (_rows - 1) * CellH * 0.5f - 0.05f;
            camGo.transform.position = new Vector3(cx, cy, -10f);

            var rt = RenderTexture.GetTemporary(w, h, 16, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(tex);
            return $"{w}x{h}";
        }

        public static string Teardown()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            return "ok";
        }
    }
}
