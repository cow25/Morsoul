using System.IO;
using Morsoul.Board;
using Morsoul.Cutscene;
using Morsoul.Dialogue;
using Morsoul.Hub;
using Morsoul.Interrogation;
using Morsoul.Journal;
using Morsoul.Resolution;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace Morsoul.Flow.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/CaseLoop.unity — the playable case-loop composition:
    /// a <see cref="GameFlow"/> bound to board, interrogation, resolution,
    /// cutscene, dialogue and journal panels. Assign a CaseData on the
    /// GameFlow's activeCase (e.g. Case 5 works standalone; investigation cases
    /// need their evidence collected into the Core session first).
    /// Idempotent: an existing scene prompts an overwrite dialog.
    /// </summary>
    public static class FlowSceneFactory
    {
        const string ScenePath = "Assets/Scenes/CaseLoop.unity";
        const string PanelSettingsPath = "Assets/UI Toolkit/MainPanelSettings.asset";
        const string BoardUxmlPath = "Assets/Board.uxml";
        const string InterrogationUxmlPath = "Assets/Interrogation.uxml";
        const string ResolutionUxmlPath = "Assets/Resolution.uxml";
        const string CutsceneUxmlPath = "Assets/Cutscene.uxml";
        const string DialogueUxmlPath = "Assets/Dialogue.uxml";
        const string JournalUxmlPath = "Assets/Journal.uxml";

        [MenuItem("Morsoul/Scene/Create Case Loop Scene")]
        public static void CreateCaseLoopScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                || File.Exists(ScenePath))
            {
                bool overwrite = EditorUtility.DisplayDialog("Create Case Loop Scene",
                    $"'{ScenePath}' already exists. Overwrite it?", "Overwrite", "Cancel");
                if (!overwrite) return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.03f, 0.055f);

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
                Debug.LogWarning($"FlowSceneFactory: PanelSettings not found at '{PanelSettingsPath}'.");

            // Shared session-side stores so journal/flags sync with Core.
            var storesGo = new GameObject("CaseContext");
            var hubJournal = storesGo.AddComponent<HubJournal>();
            var hubFlags = storesGo.AddComponent<HubFlags>();

            var board = CreatePanel<BoardController>("BoardPanel", panelSettings, BoardUxmlPath);
            var interrogation = CreatePanel<InterrogationController>(
                "InterrogationPanel", panelSettings, InterrogationUxmlPath);
            var resolution = CreatePanel<ResolutionController>(
                "ResolutionPanel", panelSettings, ResolutionUxmlPath);
            var dialogue = CreatePanel<DialogueController>(
                "DialoguePanel", panelSettings, DialogueUxmlPath);
            var journal = CreatePanel<JournalController>(
                "JournalPanel", panelSettings, JournalUxmlPath);

            var cutsceneGo = new GameObject("CutscenePanel");
            var cutsceneRenderer = cutsceneGo.AddComponent<PanelRenderer>();
            if (panelSettings != null) cutsceneRenderer.panelSettings = panelSettings;
            var cutsceneUxml = LoadUxml(CutsceneUxmlPath);
            if (cutsceneUxml != null) cutsceneRenderer.visualTreeAsset = cutsceneUxml;
            cutsceneGo.AddComponent<VideoPlayer>();
            var cutscene = cutsceneGo.AddComponent<CutscenePlayerController>();

            journal.journal = hubJournal;
            journal.SetFlags(hubFlags.Set);
            dialogue.SetFlags(hubFlags.Set);

            var flowGo = new GameObject("GameFlow");
            var flow = flowGo.AddComponent<GameFlow>();
            flow.board = board;
            flow.interrogation = interrogation;
            flow.resolution = resolution;
            flow.cutscenePlayer = cutscene;
            flow.dialogue = dialogue;
            flow.hubJournal = hubJournal;
            flow.flags = hubFlags;
            // flow.activeCase stays unassigned on purpose — pick the case to
            // run in the inspector before pressing Play.

            if (EditorSceneManager.SaveScene(scene, ScenePath))
                Debug.Log($"FlowSceneFactory: case-loop scene saved to '{ScenePath}'. " +
                          "Assign GameFlow.activeCase and add the scene to Build Settings.", flow);
            else
                Debug.LogError($"FlowSceneFactory: failed to save '{ScenePath}'.");
        }

        static T CreatePanel<T>(string name, PanelSettings panelSettings, string uxmlPath)
            where T : Component
        {
            var go = new GameObject(name);
            var renderer = go.AddComponent<PanelRenderer>();
            if (panelSettings != null) renderer.panelSettings = panelSettings;
            var uxml = LoadUxml(uxmlPath);
            if (uxml != null) renderer.visualTreeAsset = uxml;
            return go.AddComponent<T>();
        }

        static VisualTreeAsset LoadUxml(string path)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            if (uxml == null)
                Debug.LogWarning($"FlowSceneFactory: UXML not found at '{path}'.");
            return uxml;
        }
    }
}
