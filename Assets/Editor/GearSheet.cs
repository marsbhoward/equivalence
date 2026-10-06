using System.Collections.Generic;
using UnityEngine;
using Convergence.Art;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.EditorTools
{
    /// <summary>
    /// A contact sheet of gear pieces on the same figure, for judging a piece's SIZE against the
    /// body and against its neighbours from the CLI - HairSheet's shape, one column per item.
    /// Play mode only, for HairSheet's reason (the rig poses itself in its own Update). Two calls,
    /// a moment apart:
    ///
    ///     unity command eval 'return Convergence.EditorTools.GearSheet.Setup("leather_pouch,shadow_relic");'
    ///     unity command eval 'return Convergence.EditorTools.GearSheet.Capture("/tmp/gear.png");'
    ///
    /// Two rows: facing right and facing left (the mirror swaps which hip a piece sits on).
    /// </summary>
    public static class GearSheet
    {
        const string RootName = "gear-sheet";
        static readonly Vector2 Origin = new(6000f, 5000f);
        const float CellW = 0.8f, CellH = 1.0f;
        const int Views = 2;

        static readonly List<string> _ids = new();

        /// <param name="items">Comma-separated columns; each is one item id, or several joined by '+'.</param>
        /// <param name="gear">Comma-separated item ids worn by EVERY figure (a weapon, a belt).</param>
        public static string Setup(string items, string gear = "")
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);

            _ids.Clear();
            foreach (var id in items.Split(','))
                if (!string.IsNullOrWhiteSpace(id)) _ids.Add(id.Trim());

            for (int s = 0; s < _ids.Count; s++)
            for (int v = 0; v < Views; v++)
            {
                var go = new GameObject($"{_ids[s]}.{v}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(Origin.x + s * CellW, Origin.y - v * CellH, 0f);

                var rig = CharacterRigFactory.Build(go, ElementType.Fire, 0);
                rig.SetAppearance(new Appearance());
                var loadout = new Loadout();
                foreach (var id in gear.Split(','))
                {
                    var worn = string.IsNullOrWhiteSpace(id) ? null : GearCatalog.Get(id.Trim());
                    if (worn != null) loadout.Set(worn.Slot, worn.ItemId);
                }
                // A column may wear several pieces at once - "a+b+c" - so a SET can be judged whole.
                foreach (var id in _ids[s].Split('+'))
                {
                    var item = GearCatalog.Get(id.Trim());
                    if (item != null) loadout.Set(item.Slot, item.ItemId);
                }
                rig.Apply(loadout);
                rig.SetContactShadow(false);
                rig.SetFacing(v == 0 ? Vector2.right : Vector2.left);
            }
            return $"{_ids.Count} items x {Views} views";
        }

        /// <param name="pxPerTexel">Screen pixels per BODY texel (BodyPpu).</param>
        public static string Capture(string path, int pxPerTexel = 4)
        {
            var root = GameObject.Find(RootName);
            if (root == null) return "run Setup first";

            int n = _ids.Count;
            float ppu = PixelSprite.FinestUnit * pxPerTexel;
            int w = Mathf.RoundToInt(n * CellW * ppu), h = Mathf.RoundToInt(Views * CellH * ppu);

            var camGo = new GameObject("gear-sheet-cam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Views * CellH * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.30f, 0.32f, 0.36f);
            float cx = Origin.x + (n - 1) * CellW * 0.5f;
            float cy = Origin.y - (Views - 1) * CellH * 0.5f + 0.1f;
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
            return $"{w}x{h}: " + string.Join(" ", _ids);
        }

        public static string Teardown()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            return "ok";
        }
    }
}
