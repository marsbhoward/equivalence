using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Convergence.EditorTools
{
    /// <summary>
    /// The browser build, as a menu item and a CLI entry - the same shape as ArenaSceneBuilder,
    /// so it can be driven from `unity command eval` like everything else in this project.
    /// </summary>
    public static class WebBuild
    {
        public const string OutputDir = "Build/Web";

        [MenuItem("Convergence/Build for Web")]
        public static string BuildWeb()
        {
            ApplyWebSettings();

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                return "no scenes in Build Settings - run ArenaSceneBuilder.BuildArenaScene() first";
            }

            Directory.CreateDirectory(OutputDir);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            });

            var s = report.summary;
            var msg = $"{s.result}  {s.totalSize / (1024 * 1024)} MB  {s.totalTime.TotalSeconds:0}s  -> {OutputDir}";
            Debug.Log($"[WebBuild] {msg}");
            return msg;
        }

        /// <summary>
        /// Settings the browser build needs, applied here rather than left in the .asset so a
        /// clean checkout builds the same way. Everything here is a WebGL-only setting; nothing
        /// touches the shared player settings the native targets read.
        /// </summary>
        [MenuItem("Convergence/Apply Web Settings")]
        public static string ApplyWebSettings()
        {
            PlayerSettings.WebGL.template = "PROJECT:Coalescence";

            // Brotli is roughly a third the size of gzip and the download is the first thing a
            // player experiences. It needs the server to send Content-Encoding: br - if you are
            // serving from something that will not, switch to Gzip or the build will not load.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;

            PlayerSettings.WebGL.dataCaching = true;

            // Every sprite in this game is rasterised at startup, so the heap is large early and
            // then flat. Starting high avoids repeated growth during boot, which on Safari is
            // where a tab is most likely to be killed.
            PlayerSettings.WebGL.initialMemorySize = 512;
            PlayerSettings.WebGL.maximumMemorySize = 2048;

            // Explicit throws only: full stack traces roughly double the code size and this
            // project's diagnostics go through Debug.Log into the browser console anyway.
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            PlayerSettings.WebGL.threadsSupport = false;   // needs COOP/COEP headers; not worth it here

            PlayerSettings.runInBackground = false;
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Master);

            // Landscape, for the reason in CLAUDE.md. The browser mostly decides this itself, but
            // the flags also feed Add-to-Home-Screen behaviour.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            AssetDatabase.SaveAssets();
            return "web settings applied: template=Coalescence, brotli, 512/2048 MB, landscape only";
        }
    }
}
