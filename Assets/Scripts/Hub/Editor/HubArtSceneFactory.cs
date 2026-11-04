using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub.Editor
{
    public static class HubArtSceneFactory
    {
        const string ScenesFolder = "Assets/Scenes";

        [MenuItem("Morsoul/Hub/Create Art Overlay Scene (Lower Market)")]
        public static void CreateLowerMarketArt() => CreateArtScene("Hub_LowerMarket_Art");

        [MenuItem("Morsoul/Hub/Create Art Overlay Scene (Wilderness)")]
        public static void CreateWildernessArt() => CreateArtScene("Hub_Wilderness_Art");

        static void CreateArtScene(string sceneName)
        {
            string path = $"{ScenesFolder}/{sceneName}.unity";
            if (File.Exists(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                Debug.LogWarning($"HubArtSceneFactory: '{path}' already exists; left untouched.");
                return;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var root = new GameObject("Art");
            root.transform.position = Vector3.zero;
            SceneManager.MoveGameObjectToScene(root, scene);
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (EditorSceneManager.SaveScene(scene, path))
                Debug.Log($"HubArtSceneFactory: art-only scene saved to '{path}'. " +
                          "Add it to Build Settings to enable additive loading.");
            else
                Debug.LogError($"HubArtSceneFactory: failed to save scene to '{path}'.");
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
