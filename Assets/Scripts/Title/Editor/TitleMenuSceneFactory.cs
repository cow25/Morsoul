using System.IO;
using Morsoul.Cutscene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace Morsoul.Title.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/Title.unity as a clean UI-Toolkit-only non-gameplay
    /// scene (no 3D lights, world geometry, or floating uGUI Canvas), matching
    /// the Figma candlelit study, Save Slots modal, and Settings modal.
    /// </summary>
    public static class TitleMenuSceneFactory
    {
        const string ScenePath = "Assets/Scenes/Title.unity";
        const string PanelSettingsPath = "Assets/UI Toolkit/MainPanelSettings.asset";
        const string TitleUxmlPath = "Assets/Title.uxml";
        const string CutscenePrefabPath = "Assets/Prefabs/UI/CutscenePanel.prefab";
        const string CutsceneUxmlPath = "Assets/Cutscene.uxml";

        [MenuItem("Morsoul/Scenes/Title Scene", priority = 10)]
        public static void CreateTitleScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null || File.Exists(ScenePath))
            {
                if (!EditorUtility.DisplayDialog(
                        "Overwrite Title scene?",
                        $"'{ScenePath}' already exists. Overwrite it?", "Overwrite", "Cancel"))
                    return;
            }
            BuildAndSave();
        }

        public static void CreateTitleSceneForce() => BuildAndSave();

        static void BuildAndSave()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Minimal orthographic camera with dark clear background so Scene View stays clean
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.04f, 1f);
            camGo.AddComponent<AudioListener>();

            // UI Toolkit Title Menu
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var menuGo = new GameObject("TitleMenu");
            var pr = menuGo.AddComponent<PanelRenderer>();
            pr.panelSettings = panelSettings;
            pr.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TitleUxmlPath);
            var menuCtrl = menuGo.AddComponent<TitleMenuController>();

            // Opening Cutscene Panel (renders above TitleMenu)
            CutscenePlayerController cutsceneCtrl = null;
            var cutscenePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CutscenePrefabPath);
            if (cutscenePrefab != null)
            {
                var cutsceneGo = (GameObject)PrefabUtility.InstantiatePrefab(cutscenePrefab);
                cutsceneCtrl = cutsceneGo.GetComponent<CutscenePlayerController>();
                var cutsceneRenderer = cutsceneGo.GetComponent<PanelRenderer>();
                if (cutsceneRenderer != null) cutsceneRenderer.sortingOrder = 10;
            }
            else
            {
                var cutsceneGo = new GameObject("CutscenePanel");
                var cutsceneRenderer = cutsceneGo.AddComponent<PanelRenderer>();
                cutsceneRenderer.panelSettings = panelSettings;
                cutsceneRenderer.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CutsceneUxmlPath);
                cutsceneRenderer.sortingOrder = 10;
                cutsceneGo.AddComponent<VideoPlayer>();
                cutsceneCtrl = cutsceneGo.AddComponent<CutscenePlayerController>();
            }
            menuCtrl.openingCutscenePlayer = cutsceneCtrl;

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            if (EditorSceneManager.SaveScene(scene, ScenePath))
                Debug.Log($"TitleMenuSceneFactory: UI Toolkit title scene saved to '{ScenePath}'.");
            else
                Debug.LogError($"TitleMenuSceneFactory: failed to save '{ScenePath}'.");
            AssetDatabase.Refresh();
        }
    }
}
