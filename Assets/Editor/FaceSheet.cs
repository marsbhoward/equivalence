using System.Collections.Generic;
using UnityEngine;
using Convergence.Art;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.EditorTools
{
    /// <summary>
    /// A contact sheet of faces - every expression against every brow, at an eye size - for the eyes and
    /// brows mock-up. Play mode only, two calls a moment apart (the rig poses itself in its own Update):
    ///
    ///     unity command eval --code 'return Convergence.EditorTools.FaceSheet.Setup(1, "light", "blue");'
    ///     unity command eval --code 'return Convergence.EditorTools.FaceSheet.Capture("/tmp/faces.png", 3, 0, 10);'
    ///
    /// One column per expression x brow (expressions outer), two rows: the arena's turned view and the
    /// character screen's menu art. <paramref name="grow"/> is BodyLook.DevEyeGrow (0 = today's eyes).
    /// </summary>
    public static class FaceSheet
    {
        const string RootName = "face-sheet";
        static readonly Vector2 Origin = new(7000f, 5000f);
        const float CellW = 1.1f, CellH = 1.15f;
        const int Views = 2;

        static List<string> _combos = new();

        public static string Setup(int grow = 0, string skin = "light", string eyes = "blue", string hair = "brown",
                                   string hairStyle = "fade", string expressions = "", string brows = "",
                                   string mark = "none", string head = "", string beards = "none")
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);
            BodyLook.DevEyeGrow = grow;

            var exprs = new List<string>(); var brs = new List<string>();
            if (string.IsNullOrEmpty(expressions)) foreach (var e in BodyLook.Expressions) exprs.Add(e.Key);
            else exprs.AddRange(expressions.Split(','));
            if (string.IsNullOrEmpty(brows)) foreach (var b in BodyLook.Brows) brs.Add(b.Key);
            else brs.AddRange(brows.Split(','));

            _combos.Clear();
            foreach (var e in exprs) foreach (var b in brs) foreach (var bd in beards.Split(','))
            foreach (var mk in mark.Split(',')) _combos.Add(e + ":" + b + ":" + bd + ":" + mk);
            for (int s = 0; s < _combos.Count; s++)
            for (int v = 0; v < Views; v++)
            {
                var parts = _combos[s].Split(':');
                var go = new GameObject($"{_combos[s]}.{v}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(Origin.x + s * CellW, Origin.y - v * CellH, 0f);
                var rig = CharacterRigFactory.Build(go, ElementType.Fire, 0);
                rig.SetDetailArt(v == 1);
                rig.SetAppearance(new Appearance { Hair = hair, Skin = skin, Eyes = eyes, HairStyle = hairStyle,
                                                   Expression = parts[0], Brows = parts[1], Beard = parts[2], Mark = parts[3] });
                var loadout = new Loadout();
                if (!string.IsNullOrEmpty(head)) loadout.Set(GearSlot.Head, head);   // e.g. wraith_eye
                rig.Apply(loadout);
                rig.SetContactShadow(false);
                rig.SetFacing(Vector2.right);
            }
            return $"{_combos.Count} faces at eye size {grow}";
        }

        /// <param name="pxPerTexel">Screen pixels per BODY texel.</param>
        public static string Capture(string path, int pxPerTexel = 3, int first = 0, int count = 99)
        {
            var root = GameObject.Find(RootName);
            if (root == null) return "run Setup first";
            int n = Mathf.Min(count, _combos.Count - first);
            float ppu = PixelSprite.FinestUnit * pxPerTexel;
            int w = Mathf.RoundToInt(n * CellW * ppu), h = Mathf.RoundToInt(Views * CellH * ppu);

            var camGo = new GameObject("face-sheet-cam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Views * CellH * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.30f, 0.32f, 0.36f);
            camGo.transform.position = new Vector3(Origin.x + first * CellW + (n - 1) * CellW * 0.5f,
                                                   Origin.y - (Views - 1) * CellH * 0.5f + 0.12f, -10f);
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
            return $"{w}x{h}: " + string.Join(" ", _combos.GetRange(first, n));
        }

        public static string Teardown()
        {
            BodyLook.DevEyeGrow = 0;
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            return "ok";
        }
    }
}
