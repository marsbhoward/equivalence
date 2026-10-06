using System.Collections.Generic;
using UnityEngine;
using Convergence.Art;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.EditorTools
{
    /// <summary>
    /// A contact sheet of every hairstyle, for judging hair art from the CLI. Play mode only - the
    /// rig poses itself in its own Update, so a sheet captured in the same frame it was built
    /// shows the unposed rig. Two calls, a moment apart:
    ///
    ///     unity command eval 'return Convergence.EditorTools.HairSheet.Setup("brown");'
    ///     unity command eval 'return Convergence.EditorTools.HairSheet.Capture("/tmp/hair.png", 4, 0, 7);'
    ///
    /// One column per style (in BodyLook.HairStyles order), one row per view: turned (the arena's
    /// side view), facing away, and the character screen's menu art.
    /// </summary>
    public static class HairSheet
    {
        const string RootName = "hair-sheet";
        static readonly Vector2 Origin = new(5000f, 5000f);
        const float CellW = 1.1f, CellH = 1.15f;
        const int Views = 3;

        /// <summary>The styles on the current sheet, in column order.</summary>
        static List<string> _keys = new();

        /// <param name="keys">Comma-separated style keys to lay out, or empty for all of them.</param>
        /// <param name="gear">Comma-separated item ids worn by every figure (a helm, a hood, a
        /// tie-back), for checking how hair behaves under them.</param>
        /// <param name="expression">Face for every figure (BodyLook.Expressions key).</param>
        /// <param name="brows">Brows for every figure (BodyLook.Brows key).</param>
        public static string Setup(string hair = "brown", string skin = "fair", string keys = "",
                                   string gear = "", string expression = "neutral", string brows = "natural")
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);

            var styles = new List<BodyLook.Style>();
            foreach (var st in BodyLook.HairStyles)
                if (string.IsNullOrEmpty(keys) || System.Array.IndexOf(keys.Split(','), st.Key) >= 0)
                    styles.Add(st);
            _keys.Clear();
            foreach (var st in styles) _keys.Add(st.Key);
            for (int s = 0; s < styles.Count; s++)
            for (int v = 0; v < Views; v++)
            {
                var go = new GameObject($"{styles[s].Key}.{v}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(Origin.x + s * CellW, Origin.y - v * CellH, 0f);

                var rig = CharacterRigFactory.Build(go, ElementType.Fire, 0);
                var look = new Appearance { Hair = hair, Skin = skin, HairStyle = styles[s].Key,
                                            Expression = expression, Brows = brows };
                rig.SetDetailArt(v == 2);
                rig.SetAppearance(look);
                var loadout = new Loadout();
                foreach (var id in gear.Split(','))
                {
                    var item = string.IsNullOrEmpty(id) ? null : GearCatalog.Get(id.Trim());
                    if (item != null) loadout.Set(item.Slot, item.ItemId);
                }
                rig.Apply(loadout);
                rig.SetContactShadow(false);
                rig.SetFacing(Vector2.right);
                if (v == 1) rig.SetFacingAway(true);
            }
            return $"{styles.Count} styles x {Views} views";
        }

        /// <param name="pxPerTexel">Screen pixels per BODY texel (BodyPpu).</param>
        /// <param name="first">First style column to include.</param>
        /// <param name="count">How many style columns.</param>
        public static string Capture(string path, int pxPerTexel = 4, int first = 0, int count = 99)
        {
            var root = GameObject.Find(RootName);
            if (root == null) return "run Setup first";

            int n = Mathf.Min(count, _keys.Count - first);
            float ppu = PixelSprite.FinestUnit * pxPerTexel;   // the body's density (Proportions.BodyPpu)
            int w = Mathf.RoundToInt(n * CellW * ppu), h = Mathf.RoundToInt(Views * CellH * ppu);

            var camGo = new GameObject("hair-sheet-cam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Views * CellH * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.30f, 0.32f, 0.36f);
            float cx = Origin.x + first * CellW + (n - 1) * CellW * 0.5f;
            float cy = Origin.y - (Views - 1) * CellH * 0.5f + 0.12f;
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

            var keys = new List<string>();
            for (int i = first; i < first + n; i++) keys.Add(_keys[i]);
            return $"{w}x{h}: " + string.Join(" ", keys);
        }

        public static string Teardown()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            return "ok";
        }
    }
}
