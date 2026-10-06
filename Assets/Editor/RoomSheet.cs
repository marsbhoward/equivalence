using UnityEngine;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.EditorTools
{
    /// <summary>
    /// Look at a room shape from the CLI, in Play mode during a run. Show swaps the live room's
    /// walls for a named shape (nothing else about the floor changes); Capture renders the whole
    /// arena from straight above to a PNG.
    ///
    ///     unity command eval 'return Convergence.EditorTools.RoomSheet.Show("plaza", 0);'
    ///     unity command eval 'return Convergence.EditorTools.RoomSheet.Capture("/tmp/room.png");'
    ///
    /// For a whole floor BUILT in a shape (pits, columns and the wave placed around it) set
    /// Hazards.RoomShape.DevForceNext before the next floor instead.
    /// </summary>
    public static class RoomSheet
    {
        public static string Show(string name, int variant = 0)
        {
            var root = GameObject.Find("Hazards");
            if (root == null) return "no Hazards root - start a run";
            var shape = name == "open" ? null : RoomShape.Get(name, variant);
            if (shape == null && name != "open") return $"no shape '{name}': {string.Join(",", RoomShape.Names)}";
            RoomWalls.Apply(shape, root.transform);
            return RoomWalls.CurrentName;
        }

        public static string Capture(string path, int pxPerUnit = 50)
        {
            var half = Arena.HalfExtents + Vector2.one * 0.8f;
            int w = Mathf.RoundToInt(half.x * 2f * pxPerUnit), h = Mathf.RoundToInt(half.y * 2f * pxPerUnit);

            var camGo = new GameObject("room-sheet-cam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = half.y;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            var rt = RenderTexture.GetTemporary(w, h, 16, RenderTextureFormat.ARGB32);
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
            return $"{w}x{h} {RoomWalls.CurrentName}";
        }
    }
}
