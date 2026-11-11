using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace Morsoul.Cutscene.Editor
{
    public static class CutscenePanelFactory
    {
        const string PrefabPath = "Assets/Prefabs/UI/CutscenePanel.prefab";
        const string UxmlPath = "Assets/Cutscene.uxml";
        const string PanelSettingsPath = "Assets/UI Toolkit/MainPanelSettings.asset";
        const string ExpectedPanelSettingsGuid = "50b0a700fb7fda64abced48bc2d03124";

        static void CreateCutscenePanelPrefab()
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (panelSettings == null)
            {
                Debug.LogError($"CutscenePanelFactory: PanelSettings not found at '{PanelSettingsPath}'.");
                return;
            }
            if (uxml == null)
            {
                Debug.LogError($"CutscenePanelFactory: UXML not found at '{UxmlPath}'. " +
                               "Import Assets/Cutscene.uxml first (save/refresh the project).");
                return;
            }
            string guid = AssetDatabase.AssetPathToGUID(PanelSettingsPath);
            if (guid != ExpectedPanelSettingsGuid)
                Debug.LogWarning($"CutscenePanelFactory: PanelSettings guid {guid} differs from " +
                                 $"expected {ExpectedPanelSettingsGuid}; using '{PanelSettingsPath}' anyway.");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Create Cutscene Panel Prefab",
                    $"'{PrefabPath}' already exists. Overwrite it?",
                    "Overwrite",
                    "Cancel");
                if (!overwrite) return;
            }

            var go = new GameObject("CutscenePanel");
            try
            {
                var renderer = go.AddComponent<PanelRenderer>();
                renderer.panelSettings = panelSettings;
                renderer.visualTreeAsset = uxml;
                go.AddComponent<VideoPlayer>();
                // cutsceneData intentionally left null - wired up per scene.
                go.AddComponent<CutscenePlayerController>();

                EnsureFolder("Assets/Prefabs/UI");
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                if (prefab != null)
                    Debug.Log($"CutscenePanelFactory: cutscene panel prefab saved to '{PrefabPath}'.", prefab);
                else
                    Debug.LogError($"CutscenePanelFactory: failed to save prefab to '{PrefabPath}'.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }
    }
}
