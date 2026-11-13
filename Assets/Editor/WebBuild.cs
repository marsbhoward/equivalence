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
        public const string OutputDir = "Builds/WebGL";   // what CHAIN.md's upload step reads

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
            var msg = $"{s.result}  {s.totalSize / (1024 * 1024)} MB  {s.totalTime.TotalSeconds:0}s  errors={BuildErrors(report)}  -> {OutputDir}";
            Debug.Log($"[WebBuild] {msg}");
            return msg;
        }

        /// <summary>
        /// The build's own errors. Not BuildSummary.totalErrors: run from `unity command eval`, a build holds the main
        /// thread past eval's 5 s, and the CLI's "Main thread operation timed out" is logged in the middle of the build
        /// and counted with it. (Queuing the build instead does not work - a background Editor never ticks delayCall.)
        /// </summary>
        static int BuildErrors(BuildReport report)
        {
            int n = 0;
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if ((m.type == LogType.Error || m.type == LogType.Exception)
                        && !m.content.Contains("Main thread operation timed out"))
                        n++;
            return n;
        }

        public const string PerfOutputDir = "Builds/WebGLPerf";   // never uploaded: Tools/perf-serve serves it

        /// <summary>
        /// THE PERF-TEST BUILD (Core/PerfTest): the same optimised build with EQ_PERFTEST - eight directions on, saves
        /// in the browser, the pose-cache readout, a report posted to the serving host - and gzip, because the phone
        /// loads it over plain http on the LAN, where browsers will not take Brotli, with plain http requests allowed
        /// for the report. The project's settings are put back afterwards, so the next ordinary web build is unchanged.
        /// </summary>
        [MenuItem("Convergence/Build for Web (perf test)")]
        public static string BuildWebPerfTest()
        {
            ApplyWebSettings();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            // the report posts to the LAN host over plain http, which the project refuses by default
            var http = PlayerSettings.insecureHttpOption;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            string msg;
            try
            {
                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length == 0) return "no scenes in Build Settings - run ArenaSceneBuilder.BuildArenaScene() first";
                Directory.CreateDirectory(PerfOutputDir);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = PerfOutputDir,
                    target = BuildTarget.WebGL,
                    targetGroup = BuildTargetGroup.WebGL,
                    options = BuildOptions.None,
                    extraScriptingDefines = new[] { "EQ_PERFTEST" },
                });
                var s = report.summary;
                msg = $"{s.result}  {s.totalSize / (1024 * 1024)} MB  {s.totalTime.TotalSeconds:0}s  errors={BuildErrors(report)}  -> {PerfOutputDir}";
            }
            finally
            {
                ApplyWebSettings();   // Brotli again for the real build
                PlayerSettings.insecureHttpOption = http;
            }
            Debug.Log($"[WebBuild] perf test: {msg}");
            File.WriteAllText("Builds/webgl-perf-result.txt", msg + "\n");
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
            PlayerSettings.WebGL.template = "PROJECT:Equivalence";

            // Brotli is roughly a third the size of gzip and the download is the first thing a
            // player experiences. It needs the server to send Content-Encoding: br - if you are
            // serving from something that will not, switch to Gzip or the build will not load.
            // No fallback: the service (web/service/src/play.ts) sends the headers, and a fallback
            // ships a slower JS decompressor nobody needs.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = false;
            // Content-hash names, which play.ts caches for a year.
            PlayerSettings.WebGL.nameFilesAsHashes = true;

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
            return "web settings applied: template=Equivalence, brotli (no fallback), hashed names, 512/2048 MB, landscape only";
        }
    }
}
