using System.Runtime.InteropServices;
using UnityEngine;

namespace Convergence.UI
{
    /// <summary>
    /// Hands a picture to the player: on the web build the system share sheet on a phone, a download
    /// elsewhere (Plugins/WebGL/EquivalencePicture.jslib); in the Editor and a desktop build a PNG in
    /// the save folder's Pictures. The portrait booth's SAVE A PICTURE (UI/BoothScreen).
    /// </summary>
    public static class PictureSaver
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void EquivalenceSavePicture(byte[] data, int length, string name);
#endif

        /// <summary>Saves <paramref name="png"/> as <paramref name="name"/>; returns what to tell the
        /// player happened.</summary>
        public static string Save(byte[] png, string name)
        {
            if (png == null || png.Length == 0) return "the picture could not be made";
#if UNITY_WEBGL && !UNITY_EDITOR
            EquivalenceSavePicture(png, png.Length, name);
            return Core.Controls.TouchMode ? "picture ready to share" : "picture downloaded";
#else
            var dir = System.IO.Path.Combine(Application.persistentDataPath, "Pictures");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, name);
            System.IO.File.WriteAllBytes(path, png);
            Debug.Log($"[Booth] picture saved: {path}");
            return "picture saved";
#endif
        }

        /// <summary>What a render texture shows now, as a PNG.</summary>
        public static byte[] Png(RenderTexture rt)
        {
            if (rt == null) return null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var png = tex.EncodeToPNG();
            Object.Destroy(tex);
            return png;
        }
    }
}
