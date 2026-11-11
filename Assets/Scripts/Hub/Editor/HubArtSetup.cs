using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub.Editor
{
    public static class HubArtSetup
    {
        const string LowerMarketScenePath = "Assets/Scenes/Hub_LowerMarket.unity";
        const string WildernessScenePath = "Assets/Scenes/Hub_Wilderness.unity";
        const string LowerMarketArtScenePath = "Assets/Scenes/Hub_LowerMarket_Art.unity";
        const string WildernessArtScenePath = "Assets/Scenes/Hub_Wilderness_Art.unity";

        public static void Install()
        {
            if (Application.isBatchMode && SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(SceneManager.GetActiveScene().path) &&
                !SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene(LowerMarketScenePath, OpenSceneMode.Single);
            HubArtFolderSetup.CreateFolders();
            HubArtSceneFactory.CreateLowerMarketArt();
            HubArtSceneFactory.CreateWildernessArt();
            AddBuildScene(LowerMarketArtScenePath);
            AddBuildScene(WildernessArtScenePath);
            IntegrateHubScene(LowerMarketScenePath, "Hub_LowerMarket_Art");
            IntegrateHubScene(WildernessScenePath, "Hub_Wilderness_Art");
        }

        static void AddBuildScene(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"HubArtSetup: '{path}' does not exist; not added to Build Settings.");
                return;
            }
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var entry in scenes)
                if (entry.path == path) return;
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"HubArtSetup: could not resolve GUID for '{path}'.");
                return;
            }
            scenes.Add(new EditorBuildSettingsScene(new GUID(guid), true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"HubArtSetup: added '{path}' to Build Settings.");
        }

        static void IntegrateHubScene(string scenePath, string artSceneName)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"HubArtSetup: '{scenePath}' not found; skipped.");
                return;
            }
            var preExisting = SceneManager.GetSceneByPath(scenePath);
            bool wasOpen = preExisting.IsValid() && preExisting.isLoaded;
            if (wasOpen && preExisting.isDirty)
            {
                Debug.LogError($"HubArtSetup: '{scenePath}' is open with unsaved changes; " +
                               "skipped. Save or revert it first.");
                return;
            }
            var previousActive = SceneManager.GetActiveScene();
            var scene = wasOpen ? preExisting
                : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                bool touched = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.GetComponent<HubPlayerController>() == null
                        && root.GetComponent<Interactable2D>() == null)
                        continue;
                    HubSceneFactory.ApplyVisualProfile(root);
                    touched = true;
                }
                if (FindLoader(scene) == null)
                {
                    var loader = new GameObject("ArtSceneLoader");
                    SceneManager.MoveGameObjectToScene(loader, scene);
                    loader.AddComponent<HubArtSceneLoader>().artSceneName = artSceneName;
                    touched = true;
                }
                if (touched)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (EditorSceneManager.SaveScene(scene))
                        Debug.Log($"HubArtSetup: integrated art pipeline into '{scenePath}'.");
                    else
                        Debug.LogError($"HubArtSetup: failed to save '{scenePath}'.");
                }
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
                if (!wasOpen && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static HubArtSceneLoader FindLoader(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var loader = root.GetComponent<HubArtSceneLoader>();
                if (loader != null) return loader;
            }
            return null;
        }
    }
}
