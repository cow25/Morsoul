using System.IO;
using Morsoul.Board;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Adds a WeightedBoardPanel (PanelRenderer + WeightedBoardController +
    /// BoardHotkey) to existing saved hub scenes so "H opens the Deduction
    /// Board everywhere" holds in hubs, without regenerating their layouts.
    /// Idempotent: a scene that already has a WeightedBoardPanel is skipped.
    /// </summary>
    public static class HubBoardSetup
    {
        const string LowerMarketScenePath = "Assets/Scenes/Hub_LowerMarket.unity";
        const string WildernessScenePath = "Assets/Scenes/Hub_Wilderness.unity";
        const string CathedralScenePath = "Assets/Scenes/Hub_CathedralNave.unity";
        const string IronHollowScenePath = "Assets/Scenes/Hub_IronHollow.unity";
        const string PanelSettingsPath = "Assets/UI Toolkit/MainPanelSettings.asset";
        const string WeightedBoardUxmlPath = "Assets/WeightedBoard.uxml";

        public static void Install()
        {
            if (Application.isBatchMode && SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(SceneManager.GetActiveScene().path) &&
                !SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene(LowerMarketScenePath, OpenSceneMode.Single);
            Integrate(LowerMarketScenePath);
            Integrate(WildernessScenePath);
            Integrate(CathedralScenePath);
            Integrate(IronHollowScenePath);
        }

        static void Integrate(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"HubBoardSetup: '{scenePath}' not found; skipped.");
                return;
            }
            var preExisting = SceneManager.GetSceneByPath(scenePath);
            bool wasOpen = preExisting.IsValid() && preExisting.isLoaded;
            if (wasOpen && preExisting.isDirty)
            {
                Debug.LogError($"HubBoardSetup: '{scenePath}' is open with unsaved changes; " +
                               "skipped. Save or revert it first.");
                return;
            }
            var previousActive = SceneManager.GetActiveScene();
            var scene = wasOpen ? preExisting
                : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                if (FindBoardPanel(scene) != null)
                {
                    Debug.Log($"HubBoardSetup: '{scenePath}' already has a board panel; skipped.");
                    return;
                }
                SceneManager.SetActiveScene(scene);
                var go = new GameObject("WeightedBoardPanel");
                var renderer = go.AddComponent<PanelRenderer>();
                var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
                if (panelSettings != null) renderer.panelSettings = panelSettings;
                var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(WeightedBoardUxmlPath);
                if (uxml != null) renderer.visualTreeAsset = uxml;
                else Debug.LogWarning($"HubBoardSetup: UXML not found at '{WeightedBoardUxmlPath}'.");
                var controller = go.AddComponent<WeightedBoardController>();
                var hotkey = go.AddComponent<BoardHotkey>();
                hotkey.board = controller;
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                    Debug.Log($"HubBoardSetup: added WeightedBoardPanel to '{scenePath}'.");
                else
                    Debug.LogError($"HubBoardSetup: failed to save '{scenePath}'.");
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
                if (!wasOpen && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static GameObject FindBoardPanel(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "WeightedBoardPanel") return root;
            return null;
        }
    }
}
