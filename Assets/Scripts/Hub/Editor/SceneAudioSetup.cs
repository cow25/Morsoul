using System.Collections.Generic;
using System.IO;
using Morsoul.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Production-readiness installer: creates the missing hub art overlay
    /// scenes and drops a <see cref="SceneAudioLoop"/> GameObject into every
    /// delivered 2D scene so music/ambience clips become assign-and-go.
    /// Every step is idempotent — existing files and components are left
    /// untouched.
    /// </summary>
    public static class SceneAudioSetup
    {
        static readonly string[] LoopScenes =
        {
            "Assets/Scenes/Title.unity",
            "Assets/Scenes/Hub_LowerMarket.unity",
            "Assets/Scenes/Hub_CathedralNave.unity",
            "Assets/Scenes/Hub_Wilderness.unity",
            "Assets/Scenes/Hub_IronHollow.unity",
        };

        public static void CreateCathedralArt() => HubArtSceneFactory.CreateArtScene("Hub_CathedralNave_Art");

        public static void CreateIronHollowArt() => HubArtSceneFactory.CreateArtScene("Hub_IronHollow_Art");

        /// <summary>Creates both missing art overlays and installs all audio loops.</summary>
        public static void InstallAll()
        {
            ParkOnSavedScene();
            CreateCathedralArt();
            CreateIronHollowArt();
            RegisterArtOverlays();
            InstallAudioLoops();
        }

        /// <summary>
        /// Registers existing art overlay scenes in Build Settings so the
        /// hubs' additive HubArtSceneLoader check passes.
        /// </summary>
        static void RegisterArtOverlays()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            void Add(string path)
            {
                foreach (var s in list)
                    if (s.path == path) { s.enabled = true; return; }
                list.Add(new EditorBuildSettingsScene(path, true));
            }
            foreach (var art in new[]
            {
                "Assets/Scenes/Hub_CathedralNave_Art.unity",
                "Assets/Scenes/Hub_IronHollow_Art.unity",
            })
            {
                if (File.Exists(art)) Add(art);
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        /// <summary>Adds a "SceneAudio" GameObject to every delivered 2D scene.</summary>
        [MenuItem("Morsoul/Scenes/Install Scene Audio Loops", priority = 30)]
        public static void InstallAudioLoops()
        {
            ParkOnSavedScene();
            foreach (var path in LoopScenes) InstallLoop(path);
        }

        /// <summary>
        /// Batch mode starts on an untitled scene that can't host additive
        /// scene creates/opens — park on a saved scene first.
        /// </summary>
        static void ParkOnSavedScene()
        {
            if (Application.isBatchMode && SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(SceneManager.GetActiveScene().path) &&
                !SceneManager.GetActiveScene().isDirty &&
                File.Exists(LoopScenes[1]))
                EditorSceneManager.OpenScene(LoopScenes[1], OpenSceneMode.Single);
        }

        static void InstallLoop(string scenePath)
        {
            if (!File.Exists(scenePath)) return;
            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                // Search inside the target scene only — FindAnyObjectByType
                // could return the parked scene's loop and false-negative.
                SceneAudioLoop existing = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    existing = root.GetComponentInChildren<SceneAudioLoop>(true);
                    if (existing != null) break;
                }
                if (existing == null)
                {
                    var go = new GameObject("SceneAudio");
                    SceneManager.MoveGameObjectToScene(go, scene);
                    go.AddComponent<SceneAudioLoop>();
                    EditorSceneManager.MarkSceneDirty(scene);
                    Debug.Log($"SceneAudioSetup: SceneAudio loop added to '{scenePath}'.");
                }
                else
                {
                    Debug.Log($"SceneAudioSetup: '{scenePath}' already has a SceneAudio loop.");
                }
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
