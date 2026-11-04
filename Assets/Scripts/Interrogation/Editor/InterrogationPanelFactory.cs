using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Morsoul.Interrogation.Editor
{
    public static class InterrogationPanelFactory
    {
        const string PrefabPath = "Assets/Prefabs/UI/InterrogationPanel.prefab";
        const string UxmlPath = "Assets/Interrogation.uxml";
        const string PanelSettingsPath = "Assets/UI Toolkit/MainPanelSettings.asset";
        const string ExpectedPanelSettingsGuid = "50b0a700fb7fda64abced48bc2d03124";

        [MenuItem("Morsoul/UI/Create Interrogation Panel Prefab")]
        static void CreateInterrogationPanelPrefab()
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (panelSettings == null)
            {
                Debug.LogError($"InterrogationPanelFactory: PanelSettings not found at '{PanelSettingsPath}'.");
                return;
            }
            if (uxml == null)
            {
                Debug.LogError($"InterrogationPanelFactory: UXML not found at '{UxmlPath}'. " +
                               "Import Assets/Interrogation.uxml first (save/refresh the project).");
                return;
            }
            string guid = AssetDatabase.AssetPathToGUID(PanelSettingsPath);
            if (guid != ExpectedPanelSettingsGuid)
                Debug.LogWarning($"InterrogationPanelFactory: PanelSettings guid {guid} differs from " +
                                 $"expected {ExpectedPanelSettingsGuid}; using '{PanelSettingsPath}' anyway.");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Create Interrogation Panel Prefab",
                    $"'{PrefabPath}' already exists. Overwrite it?",
                    "Overwrite",
                    "Cancel");
                if (!overwrite) return;
            }

            var go = new GameObject("InterrogationPanel");
            try
            {
                var renderer = go.AddComponent<PanelRenderer>();
                renderer.panelSettings = panelSettings;
                renderer.visualTreeAsset = uxml;
                // interrogationData intentionally left null — wired up per scene.
                go.AddComponent<InterrogationController>();

                EnsureFolder("Assets/Prefabs/UI");
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                if (prefab != null)
                    Debug.Log($"InterrogationPanelFactory: interrogation panel prefab saved to '{PrefabPath}'.", prefab);
                else
                    Debug.LogError($"InterrogationPanelFactory: failed to save prefab to '{PrefabPath}'.");
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
