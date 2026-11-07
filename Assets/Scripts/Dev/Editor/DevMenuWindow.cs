using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Morsoul.Dev.Editor
{
    /// <summary>
    /// Editor-side half of the dev menu: one-click regeneration/wiring
    /// actions, scene shortcuts, play-from-scene, and an EditMode test
    /// runner. Companion to the F1 runtime overlay (<see cref="DevMenuOverlay"/>).
    /// </summary>
    public class DevMenuWindow : EditorWindow
    {
        Vector2 _scroll;
        string _testSummary = "No run yet.";
        TestCallbacks _callbacks;

        static readonly (string label, string path)[] Scenes =
        {
            ("Title", "Assets/Scenes/Title.unity"),
            ("CaseLoop", "Assets/Scenes/CaseLoop.unity"),
            ("Prologue_2D", "Assets/Scenes/Prologue_2D.unity"),
            ("Hub 1: Lower Market", "Assets/Scenes/Hub_LowerMarket.unity"),
            ("Hub 2: Cathedral Nave", "Assets/Scenes/Hub_CathedralNave.unity"),
            ("Hub 3: Whisperwood", "Assets/Scenes/Hub_Wilderness.unity"),
            ("Hub 4: Iron Hollow", "Assets/Scenes/Hub_IronHollow.unity"),
            ("Investigation 01 (placeholder)",
                "Assets/Scenes/Investigation_01_Placeholder.unity"),
            ("PrologueRuins (3D)", "Assets/Scenes/PrologueRuins.unity"),
        };

        [MenuItem("Morsoul/Dev/Dev Menu")]
        public static void Open() => GetWindow<DevMenuWindow>("Dev Menu");

        void OnGUI()
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            Section("Runtime overlay");
            EditorGUILayout.HelpBox(
                "In Play mode press F1 for the in-game dev menu " +
                "(scenes, session, flags, clues, Focus, hub tools, scenarios, " +
                "smoke sequences). Present in editor + development builds only.",
                MessageType.Info);

            Section("Campaign wiring / generation");
            if (GUILayout.Button("Rebuild Campaign Wiring (all-in-one)"))
                Morsoul.Flow.Editor.CampaignBootstrap.RunAll();
            EditorGUI.indentLevel++;
            if (GUILayout.Button("Generate hub dialogue (Cases 2-4 + interludes)"))
                Morsoul.Dialogue.Editor.HubDialogueContentGenerator.Generate();
            if (GUILayout.Button("Install 2D content into hub scenes"))
                Morsoul.Hub.Editor.HubContentSetup.Install();
            if (GUILayout.Button("Install case-loop entries"))
                Morsoul.Hub.Editor.HubCaseLoopSetup.Install();
            if (GUILayout.Button("Install board panels"))
                Morsoul.Hub.Editor.HubBoardSetup.Install();
            EditorGUI.indentLevel--;

            Section("Data");
            if (GUILayout.Button("Rebuild case database"))
                Morsoul.Data.Editor.CaseDatabaseGenerator.Rebuild();
            if (GUILayout.Button("Rebuild clue registry"))
                Morsoul.Data.Editor.ClueRegistryGenerator.Rebuild();
            if (GUILayout.Button("Audit evidence graphs"))
                Morsoul.Data.Editor.EvidenceGraphAuditor.AuditAll();

            Section("Stand-ins");
            if (GUILayout.Button("Build demo stand-in scenes"))
                Morsoul.Hub.Editor.DemoStandInFactory.BuildAll();

            Section("Scenes");
            for (int i = 0; i < Scenes.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Scenes[i].label, GUILayout.Width(200));
                bool exists = System.IO.File.Exists(Scenes[i].path);
                GUI.enabled = exists;
                if (GUILayout.Button("Open", GUILayout.Width(70)))
                    EditorSceneManager.OpenScene(Scenes[i].path);
                if (GUILayout.Button("Play from here", GUILayout.Width(110)))
                    PlayFrom(Scenes[i].path);
                GUI.enabled = true;
                if (!exists) GUILayout.Label("(missing)");
                GUILayout.EndHorizontal();
            }

            Section("EditMode tests");
            GUI.enabled = !EditorApplication.isCompiling &&
                          !EditorApplication.isPlayingOrWillChangePlaymode;
            if (GUILayout.Button("Run all EditMode tests"))
                RunTests();
            GUI.enabled = true;
            GUILayout.Label(_testSummary, EditorStyles.wordWrappedLabel);

            GUILayout.EndScrollView();
        }

        static void Section(string title) =>
            GUILayout.Label(title, EditorStyles.boldLabel);

        static void PlayFrom(string scenePath)
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (scene == null) return;
            EditorSceneManager.playModeStartScene = scene;
            EditorApplication.EnterPlaymode();
        }

        void RunTests()
        {
            _testSummary = "Running…";
            _callbacks = new TestCallbacks(r =>
            {
                _testSummary =
                    $"EditMode: {r.PassCount} passed, {r.FailCount} failed, " +
                    $"{r.SkipCount} skipped ({r.Duration:0.0}s)";
                Repaint();
            });
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(_callbacks);
            api.Execute(new ExecutionSettings(
                new Filter { testMode = TestMode.EditMode }));
        }

        class TestCallbacks : ICallbacks
        {
            readonly System.Action<ITestResultAdaptor> _onFinished;
            public TestCallbacks(System.Action<ITestResultAdaptor> onFinished) =>
                _onFinished = onFinished;
            public void RunStarted(ITestAdaptor tests) { }
            public void RunFinished(ITestResultAdaptor result) =>
                _onFinished(result);
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }
    }
}
