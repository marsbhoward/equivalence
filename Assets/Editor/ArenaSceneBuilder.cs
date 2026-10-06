using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Convergence.Core;

namespace Convergence.EditorTools
{
    /// <summary>
    /// Scene scaffolding, driven from the Unity CLI.
    ///
    /// Per the Unity handoff doc: scenes are written with the *Editor-time* API
    /// (UnityEditor.SceneManagement.EditorSceneManager), not the runtime SceneManager -
    /// only the Editor API actually writes a .unity asset to disk. The scene is saved
    /// explicitly after every mutation so no unsaved-scene dialog can block automation.
    ///
    /// Invoke from a shell:
    ///   unity command eval --code "Convergence.EditorTools.ArenaSceneBuilder.BuildArenaScene()"
    /// or via the Unity menu: Convergence > Rebuild Arena Scene.
    /// </summary>
    public static class ArenaSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Arena.unity";

        [MenuItem("Convergence/Rebuild Arena Scene")]
        public static string BuildArenaScene()
        {
            // EditorSceneManager refuses to work in play mode and throws a raw
            // InvalidOperationException. Rebuilding the scene under a running game would also
            // destroy the live one, so refuse clearly rather than letting it blow up.
            if (EditorApplication.isPlaying)
            {
                const string msg = "Stop play mode before rebuilding the scene.";
                Debug.LogWarning("[Convergence] " + msg);
                return msg;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera. Orthographic 2D, dark clear colour; GameBootstrap re-applies these at
            // runtime so the scene stays correct even if someone edits the camera by hand.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.06f, 0.078f);
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();

            // The single bootstrap object. Everything else is built at runtime, which is what
            // keeps this scene regenerable from the CLI with no prefab/asset references.
            var boot = new GameObject("GameBootstrap");
            boot.AddComponent<GameBootstrap>();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Convergence] Built {ScenePath}");
            return ScenePath;
        }

        /// <summary>Make the scene index 0 so a build actually boots into it.</summary>
        public static void AddToBuildSettings(string path)
        {
            var existing = EditorBuildSettings.scenes;
            foreach (var s in existing)
                if (s.path == path) return;

            var next = new EditorBuildSettingsScene[existing.Length + 1];
            next[0] = new EditorBuildSettingsScene(path, true);
            for (int i = 0; i < existing.Length; i++) next[i + 1] = existing[i];
            EditorBuildSettings.scenes = next;
        }

        /// <summary>Open the arena scene and enter play mode - the CLI path to "just play it".</summary>
        [MenuItem("Convergence/Play Arena")]
        public static string PlayArena()
        {
            if (EditorApplication.isPlaying) return "already playing";

            if (!File.Exists(ScenePath)) BuildArenaScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
            return "playing " + ScenePath;
        }

        /// <summary>Greys the menu items out while the game is running.</summary>
        [MenuItem("Convergence/Rebuild Arena Scene", validate = true)]
        static bool CanRebuild() => !EditorApplication.isPlaying;

        [MenuItem("Convergence/Play Arena", validate = true)]
        static bool CanPlay() => !EditorApplication.isPlaying;
    }
}
