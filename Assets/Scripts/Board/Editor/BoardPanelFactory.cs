using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Morsoul.Board.Editor
{
    public static class BoardPanelFactory
    {
        const string PrefabPath = "Assets/Prefabs/UI/BoardPanel.prefab";
        const string UxmlPath = "Assets/Board.uxml";
        const string PanelSettingsPath = "Assets/UI Toolkit/MainPanelSettings.asset";
        const string ExpectedPanelSettingsGuid = "50b0a700fb7fda64abced48bc2d03124";

        static void CreateBoardPanelPrefab()
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (panelSettings == null)
            {
                Debug.LogError($"BoardPanelFactory: PanelSettings not found at '{PanelSettingsPath}'.");
                return;
            }
            if (uxml == null)
            {
                Debug.LogError($"BoardPanelFactory: UXML not found at '{UxmlPath}'. " +
                               "Import Assets/Board.uxml first (save/refresh the project).");
                return;
            }
            string guid = AssetDatabase.AssetPathToGUID(PanelSettingsPath);
            if (guid != ExpectedPanelSettingsGuid)
                Debug.LogWarning($"BoardPanelFactory: PanelSettings guid {guid} differs from " +
                                 $"expected {ExpectedPanelSettingsGuid}; using '{PanelSettingsPath}' anyway.");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Create Board Panel Prefab",
                    $"'{PrefabPath}' already exists. Overwrite it?",
                    "Overwrite",
                    "Cancel");
                if (!overwrite) return;
            }

            var go = new GameObject("BoardPanel");
            try
            {
                var renderer = go.AddComponent<PanelRenderer>();
                renderer.panelSettings = panelSettings;
                renderer.visualTreeAsset = uxml;
                // caseData intentionally left null — wired up per scene.
                go.AddComponent<BoardController>();

                EnsureFolder("Assets/Prefabs/UI");
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                if (prefab != null)
                    Debug.Log($"BoardPanelFactory: board panel prefab saved to '{PrefabPath}'.", prefab);
                else
                    Debug.LogError($"BoardPanelFactory: failed to save prefab to '{PrefabPath}'.");
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
