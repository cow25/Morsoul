using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

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
            var menuGo = new GameObject("TitleMenu");
            var pr = menuGo.AddComponent<PanelRenderer>();
            pr.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            pr.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TitleUxmlPath);
            menuGo.AddComponent<TitleMenuController>();

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
