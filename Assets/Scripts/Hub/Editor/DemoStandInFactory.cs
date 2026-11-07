using System.Collections.Generic;
using System.IO;
using Morsoul.Board;
using Morsoul.Data;
using Morsoul.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Builds the 2D demo stand-in scenes that let the campaign loop run while
    /// the 3D lane finishes the real environments:
    ///   - Prologue_2D: tutorial stand-in (five Case 0 clue inspections +
    ///     weighted board + exit to the market hub).
    ///   - Investigation_01_Placeholder: vault stand-in (Case 1 physical clue
    ///     inspections + board + exit back to the market hub).
    /// Also patches Hub_LowerMarket: VaultGate targets the placeholder vault
    /// and a RestSpot is placed near the market square.
    /// These are explicit placeholders — when the real 3D scenes ship, the
    /// opening/investigation.1 routes and VaultGate target are re-pointed and
    /// these scenes are retired. Idempotent; batch-mode safe.
    /// </summary>
    public static class DemoStandInFactory
    {
        const string PrologueScenePath = "Assets/Scenes/Prologue_2D.unity";
        const string VaultScenePath = "Assets/Scenes/Investigation_01_Placeholder.unity";
        const string LowerMarketScenePath = "Assets/Scenes/Hub_LowerMarket.unity";
        const string PanelSettingsPath = "Assets/UI Toolkit/MainPanelSettings.asset";
        const string WeightedBoardUxmlPath = "Assets/WeightedBoard.uxml";
        const string MarketSceneName = "Hub_LowerMarket";

        [MenuItem("Morsoul/Setup/Build Demo Stand-In Scenes")]
        public static void BuildAll()
        {
            CreatePrologue();
            CreateVaultPlaceholder();
            PatchLowerMarket();
            RegisterBuildScenes();
        }

        // --- Prologue stand-in ------------------------------------------------

        static void CreatePrologue()
        {
            if (!Overwrite(PrologueScenePath)) return;
            var scene = HubSceneFactory.NewEmptyScene();

            var player = HubSceneFactory.CreatePlayer();
            var cameraFollow = HubSceneFactory.CreateCamera(player.transform);
            HubSceneFactory.CreateSpawnPoint();
            var context = HubSceneFactory.CreateHubContext();
            HubSceneFactory.WirePlayer(player, cameraFollow, context);
            CreateBoardPanel();

            // The five prologue clues, laid out along the walk line.
            Inspection("Desk",        new Color(0.55f, 0.4f, 0.25f), new Vector3(-4f, 1f, 0f),
                "Assets/Data/Cases/Case00/C1_00.asset");
            Inspection("CapacitorRelay", new Color(0.4f, 0.4f, 0.9f), new Vector3(-2f, -1f, 0f),
                "Assets/Data/Cases/Case00/C2_00.asset");
            Inspection("KeyDebris",   new Color(0.85f, 0.75f, 0.2f), new Vector3(0f, 1.5f, 0f),
                "Assets/Data/Cases/Case00/C3_00.asset");
            Inspection("Diagnostics", new Color(0.3f, 0.85f, 0.85f), new Vector3(2f, -1f, 0f),
                "Assets/Data/Cases/Case00/T1_00.asset");
            Inspection("RustedPipe",  new Color(0.55f, 0.35f, 0.2f), new Vector3(4f, 1f, 0f),
                "Assets/Data/Cases/Case00/RH1_00.asset");

            // Exit to Asterveil: gated on the solved tutorial board, then
            // advances CurrentCase to 1 so the market's CaseDoor begins the
            // vault case.
            HubSceneFactory.CreateInteractable("RuinsExit",
                new Color(0.35f, 0.6f, 0.4f), new Vector3(6f, 0f, 0f),
                out PrologueExitInteractable exit);
            exit.promptText = "[E] Leave the ruins";
            exit.checkpointReason = "PrologueExit";
            exit.targetScene = MarketSceneName;
            exit.nextCaseId = 1;
            exit.boardCaseId = 0;
            exit.lockedDialogue = EnsurePrologueLockedDialogue();

            HubSceneFactory.SaveScene(scene, PrologueScenePath);
        }

        // --- Vault stand-in ---------------------------------------------------

        static void CreateVaultPlaceholder()
        {
            if (!Overwrite(VaultScenePath)) return;
            var scene = HubSceneFactory.NewEmptyScene();

            var player = HubSceneFactory.CreatePlayer();
            var cameraFollow = HubSceneFactory.CreateCamera(player.transform);
            HubSceneFactory.CreateSpawnPoint();
            var context = HubSceneFactory.CreateHubContext();
            HubSceneFactory.WirePlayer(player, cameraFollow, context);
            CreateBoardPanel();

            Inspection("PristineLock",  new Color(0.45f, 0.3f, 0.7f), new Vector3(-3f, 1f, 0f),
                "Assets/Data/Cases/Case01/C1_01.asset");
            Inspection("SolventResidue", new Color(0.3f, 0.7f, 0.35f), new Vector3(-1f, -1f, 0f),
                "Assets/Data/Cases/Case01/C2_01.asset");
            Inspection("AlteredLedger", new Color(0.7f, 0.55f, 0.25f), new Vector3(1f, 1.5f, 0f),
                "Assets/Data/Cases/Case01/C3_01.asset");
            Inspection("BentLockpick",  new Color(0.5f, 0.5f, 0.5f), new Vector3(3f, -1f, 0f),
                "Assets/Data/Cases/Case01/RH1_01.asset");

            HubSceneFactory.CreateInteractable("VaultExit",
                new Color(0.35f, 0.6f, 0.4f), new Vector3(6f, 0f, 0f),
                out CarrierInteractable exit);
            exit.promptText = "[E] Return to the market";
            exit.checkpointReason = "Investigation";
            exit.targetScene = MarketSceneName;

            HubSceneFactory.SaveScene(scene, VaultScenePath);
        }

        // --- Hub patch ---------------------------------------------------------

        static void PatchLowerMarket()
        {
            if (!File.Exists(LowerMarketScenePath))
            {
                Debug.LogWarning($"DemoStandInFactory: '{LowerMarketScenePath}' not found; skipped.");
                return;
            }
            var preExisting = SceneManager.GetSceneByPath(LowerMarketScenePath);
            bool wasOpen = preExisting.IsValid() && preExisting.isLoaded;
            if (wasOpen && preExisting.isDirty)
            {
                Debug.LogError($"DemoStandInFactory: '{LowerMarketScenePath}' is open with " +
                               "unsaved changes; skipped. Save or revert it first.");
                return;
            }
            var previousActive = SceneManager.GetActiveScene();
            var scene = wasOpen ? preExisting
                : EditorSceneManager.OpenScene(LowerMarketScenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                bool dirty = false;

                // VaultGate now targets the placeholder vault (was "C1_Vault").
                foreach (var gate in Object.FindObjectsByType<GateInteractable>())
                {
                    if (gate.gameObject.scene != scene || gate.name != "VaultGate") continue;
                    if (gate.targetScene != "Investigation_01_Placeholder")
                    {
                        gate.targetScene = "Investigation_01_Placeholder";
                        dirty = true;
                    }
                }

                // One reachable rest spot (slice requirement).
                bool hasRest = false;
                foreach (var rest in Object.FindObjectsByType<RestSpot>())
                    if (rest.gameObject.scene == scene) hasRest = true;
                if (!hasRest)
                {
                    HubSceneFactory.CreateInteractable("RestSpot",
                        new Color(0.4f, 0.55f, 0.35f), new Vector3(0f, 2.5f, 0f),
                        out RestSpot rest);
                    rest.promptText = "[E] Rest";
                    dirty = true;
                }

                if (dirty)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("DemoStandInFactory: patched VaultGate + RestSpot in Hub_LowerMarket.");
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

        // --- Shared bits -------------------------------------------------------

        static void Inspection(string name, Color tint, Vector3 pos, string cluePath)
        {
            HubSceneFactory.CreateInteractable(name, tint, pos, out InspectionInteractable inspect);
            inspect.promptText = "[E] Inspect";
            inspect.clue = AssetDatabase.LoadAssetAtPath<ClueData>(cluePath);
            if (inspect.clue == null)
                Debug.LogWarning($"DemoStandInFactory: clue asset missing at '{cluePath}'.");
        }

        static DialogueData EnsurePrologueLockedDialogue()
        {
            const string path = "Assets/Data/Dialogue/Case00/D_Prologue_Locked.asset";
            var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (data != null) return data;
            HubSceneFactory.EnsureFolder("Assets/Data/Dialogue/Case00");
            data = ScriptableObject.CreateInstance<DialogueData>();
            data.id = "D_Prologue_Locked";
            data.startNodeId = "n1";
            data.nodes = new[]
            {
                new DialogueNodeData
                {
                    id = "n1",
                    speakerName = "Diagnostics",
                    text = "Manual override unresolved. The blast door stays " +
                           "sealed until the deduction board reaches a " +
                           "supported conclusion.",
                },
            };
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        static void CreateBoardPanel()
        {
            var go = new GameObject("WeightedBoardPanel");
            var renderer = go.AddComponent<PanelRenderer>();
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings != null) renderer.panelSettings = panelSettings;
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(WeightedBoardUxmlPath);
            if (uxml != null) renderer.visualTreeAsset = uxml;
            else Debug.LogWarning($"DemoStandInFactory: UXML not found at '{WeightedBoardUxmlPath}'.");
            var controller = go.AddComponent<WeightedBoardController>();
            var hotkey = go.AddComponent<BoardHotkey>();
            hotkey.board = controller;
        }

        static void RegisterBuildScenes()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            void Add(string path)
            {
                foreach (var s in list)
                    if (s.path == path) { s.enabled = true; return; }
                list.Add(new EditorBuildSettingsScene(path, true));
            }
            Add(PrologueScenePath);
            Add(VaultScenePath);
            EditorBuildSettings.scenes = list.ToArray();
        }

        static bool Overwrite(string scenePath)
        {
            if (Application.isBatchMode) return true;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null
                && !File.Exists(scenePath))
                return true;
            bool overwrite = EditorUtility.DisplayDialog("Demo Stand-In Scenes",
                $"'{scenePath}' already exists. Overwrite it?", "Overwrite", "Cancel");
            if (!overwrite)
                Debug.LogWarning($"DemoStandInFactory: '{scenePath}' left untouched.");
            return overwrite;
        }
    }
}
