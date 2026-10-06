using UnityEngine;
using Convergence.Art;

namespace Convergence.EditorTools
{
    /// <summary>
    /// Frame-by-frame capture of the live player, for judging an animation from the CLI. Pause and
    /// step - each Step advances one frame, which in the Editor measured a fixed 0.02s of game
    /// time (the t each call prints is the authority, not that number):
    ///
    ///     unity command eval 'return Convergence.EditorTools.FrameGrab.Begin();'
    ///     unity command eval 'return Convergence.EditorTools.FrameGrab.Step();'
    ///     unity command eval 'return Convergence.EditorTools.FrameGrab.Player("/tmp/f000.png");'
    ///     ...
    ///     unity command eval 'return Convergence.EditorTools.FrameGrab.End();'
    ///
    /// Renders its own camera centred on the player rather than reading the game view: the arena
    /// camera is framed for play, and the character is a small part of it.
    /// </summary>
    public static class FrameGrab
    {
        public static string Begin()
        {
            UnityEditor.EditorApplication.isPaused = true;
            return $"paused at frame {Time.frameCount} t={Time.time:F3}";
        }

        public static string Step()
        {
            UnityEditor.EditorApplication.Step();
            return $"frame {Time.frameCount} t={Time.time:F3}";
        }

        /// <summary>Capture this frame, then queue the next - one round trip per frame.</summary>
        public static string GrabAndStep(string path, float halfHeight = 0.9f, float aspect = 1.2f,
                                         int pxPerTexel = 3, float lift = 0.45f)
        {
            string grabbed = Player(path, halfHeight, aspect, pxPerTexel, lift);
            UnityEditor.EditorApplication.Step();
            return grabbed;
        }

        public static string End()
        {
            UnityEditor.EditorApplication.isPaused = false;
            return "running";
        }

        /// <param name="halfHeight">Half the frame's height, in world units.</param>
        /// <param name="aspect">Width over height.</param>
        /// <param name="pxPerTexel">Screen pixels per BODY texel at the player's own visual scale.</param>
        /// <param name="lift">How far above the player's origin (the feet) to centre the frame.</param>
        public static string Player(string path, float halfHeight = 0.9f, float aspect = 1.2f,
                                    int pxPerTexel = 3, float lift = 0.45f)
        {
            var player = Object.FindAnyObjectByType<Player.PlayerController>();
            if (player == null) return "no player";

            float scale = Core.Tuning.Player.ArenaVisualScale;
            float ppu = PixelSprite.FinestUnit * scale * pxPerTexel;
            int h = Mathf.RoundToInt(halfHeight * 2f * ppu), w = Mathf.RoundToInt(h * aspect);

            var camGo = new GameObject("frame-grab-cam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = halfHeight;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.30f, 0.32f, 0.36f);
            var at = player.transform.position;
            camGo.transform.position = new Vector3(at.x, at.y + lift, -10f);

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
            return $"{w}x{h} frame {Time.frameCount} t={Time.time:F3}";
        }
    }
}
