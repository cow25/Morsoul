using System;
using System.Collections.Generic;
using System.IO;
using Morsoul.Data;
using Morsoul.Dialogue;
using Morsoul.Dialogue.Editor;
using Morsoul.Journal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Installs the complete 2D layer into the four campaign hub scenes:
    ///   - Hub_LowerMarket   (Case 1,  hub.0/hub.1)
    ///   - Hub_CathedralNave (Case 2,  hub.2)
    ///   - Hub_Wilderness    (Case 3,  hub.3 — Whisperwood boundary trail)
    ///   - Hub_IronHollow    (Case 4,  hub.4)
    /// Per hub: shared panels (travel menu, HUD), carrier destination picker,
    /// CaseDoor, rest spot, NPC/dialogue/inspection/gate content from the
    /// case specs, and the post-Case-4 backtrack interlude trigger. Missing
    /// scenes are created from the HubSceneFactory skeleton; existing scenes
    /// are only appended to — every addition is name-guarded and idempotent.
    /// Zone B seams use routeKey ("investigation.&lt;n&gt;") so the real 3D
    /// scenes register later without rewiring.
    /// </summary>
    public static class HubContentSetup
    {
        const string PanelSettingsPath = "Assets/UI Toolkit/MainPanelSettings.asset";
        const string TravelMenuUxmlPath = "Assets/TravelMenu.uxml";
        const string HubHudUxmlPath = "Assets/HubHud.uxml";

        const string MarketPath = "Assets/Scenes/Hub_LowerMarket.unity";
        const string CathedralPath = "Assets/Scenes/Hub_CathedralNave.unity";
        const string WildernessPath = "Assets/Scenes/Hub_Wilderness.unity";
        const string IronHollowPath = "Assets/Scenes/Hub_IronHollow.unity";

        class HubSpec
        {
            public string scenePath;
            public string artSceneName;
            public string caseText;
            public string objectiveText;
            public string postInvestigationObjective;
            public int journalCaseId;              // runic log + dissonance for JournalController
            public string interludeDialoguePath;   // null = no arrival interlude
            public string interludeFlag;
            public Action populate;                // per-hub content, name-guarded
        }

        [MenuItem("Morsoul/Hub/Install 2D Content Into Hub Scenes")]
        public static void Install()
        {
            // Batch mode starts on an untitled scene that can't host additive
            // opens — park on the market hub first (same guard as other setups).
            if (Application.isBatchMode && SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(SceneManager.GetActiveScene().path) &&
                !SceneManager.GetActiveScene().isDirty &&
                File.Exists(MarketPath))
                EditorSceneManager.OpenScene(MarketPath, OpenSceneMode.Single);

            foreach (var spec in Specs()) Integrate(spec);
            foreach (var extra in new[]
            {
                "Assets/Scenes/Prologue_2D.unity",
                "Assets/Scenes/Investigation_01_Placeholder.unity",
            })
                UpgradePresentationOnly(extra);
            RegisterBuildScenes();
        }

        static IEnumerable<HubSpec> Specs()
        {
            yield return new HubSpec
            {
                scenePath = MarketPath,
                artSceneName = "Hub_LowerMarket_Art",
                caseText = "Case 1: The Poisoned Vault",
                objectiveText = "Interview Merchant Guildmaster Ovan for storage clearance.",
                postInvestigationObjective = "The deduction is accepted - confront Master Alchemist Ignatius.",
                journalCaseId = 1,
                interludeDialoguePath = "Assets/Data/Dialogue/Interludes/D_Interlude_Market_01.asset",
                interludeFlag = "Interlude.C1.Played",
                populate = PopulateMarket,
            };
            yield return new HubSpec
            {
                scenePath = CathedralPath,
                artSceneName = "Hub_CathedralNave_Art",
                caseText = "Case 2: The Muted Belfry",
                objectiveText = "Examine Mother Lyra and secure the Belfry Key.",
                postInvestigationObjective = "The deduction is accepted - confront Accord the Bell-Ringer.",
                journalCaseId = 2,
                interludeDialoguePath = "Assets/Data/Dialogue/Interludes/D_Interlude_Cathedral_02.asset",
                interludeFlag = "Interlude.C2.Played",
                populate = PopulateCathedral,
            };
            yield return new HubSpec
            {
                scenePath = WildernessPath,
                artSceneName = "Hub_Wilderness_Art",
                caseText = "Case 3: The Dying Grove",
                objectiveText = "Receive Kaelen's briefing and acquire the Elder Grove Perimeter Seal.",
                postInvestigationObjective = "The deduction is accepted - confront Vane the Veiled Circle Operative.",
                journalCaseId = 3,
                interludeDialoguePath = "Assets/Data/Dialogue/Interludes/D_Interlude_Whisperwood_03.asset",
                interludeFlag = "Interlude.C3.Played",
                populate = PopulateWhisperwood,
            };
            yield return new HubSpec
            {
                scenePath = IronHollowPath,
                artSceneName = "Hub_IronHollow_Art",
                caseText = "Case 4: The Shattered Seam",
                objectiveText = "Present Guild authority to Overseer Thrun and reach Drift 09.",
                postInvestigationObjective = "The deduction is accepted - confront Harlan, the Veiled Circle Infiltrator.",
                journalCaseId = 4,
                interludeDialoguePath = null,
                interludeFlag = null,
                populate = PopulateIronHollow,
            };
        }

        // --- Scene integration -------------------------------------------------

        static void Integrate(HubSpec spec)
        {
            bool created = !File.Exists(spec.scenePath);
            var preExisting = SceneManager.GetSceneByPath(spec.scenePath);
            bool wasOpen = preExisting.IsValid() && preExisting.isLoaded;
            if (wasOpen && preExisting.isDirty)
            {
                Debug.LogError($"HubContentSetup: '{spec.scenePath}' is open with unsaved " +
                               "changes; skipped. Save or revert it first.");
                return;
            }
            var previousActive = SceneManager.GetActiveScene();
            var scene = wasOpen ? preExisting
                : created ? HubSceneFactory.NewEmptyScene()
                : EditorSceneManager.OpenScene(spec.scenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var context = EnsureContext(scene, spec);
                EnsurePanel<TravelMenuController>(scene, "TravelMenuPanel", TravelMenuUxmlPath,
                    out var menu);
                if (context.travelMenu != menu) context.travelMenu = menu;
                EnsurePanel<HubHudController>(scene, "HubHudPanel", HubHudUxmlPath,
                    out var hud);
                hud.caseText = spec.caseText;
                hud.objectiveText = spec.objectiveText;
                hud.objectiveCaseId = spec.journalCaseId;
                hud.postInvestigationObjective = spec.postInvestigationObjective;
                EnsureCaseDoor(scene);
                EnsureRestSpot(scene);
                EnsureCarrier(scene);
                ConfigureJournal(scene, spec.journalCaseId);
                if (spec.interludeDialoguePath != null)
                    EnsureInterlude(scene, spec);
                spec.populate?.Invoke();
                EnsureInteractablePresentation(scene);

                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene, spec.scenePath))
                    Debug.Log($"HubContentSetup: '{spec.scenePath}' {(created ? "created" : "updated")}.");
                else
                    Debug.LogError($"HubContentSetup: failed to save '{spec.scenePath}'.");
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
                if (!wasOpen && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static HubContext EnsureContext(Scene scene, HubSpec spec)
        {
            var context = FindComponent<HubContext>(scene);
            if (context != null) return context;

            // Missing or skeleton-less scene: assemble the standard parts.
            var player = FindComponent<HubPlayerController>(scene);
            if (player == null)
            {
                var playerGo = HubSceneFactory.CreatePlayer();
                player = playerGo.GetComponent<HubPlayerController>();
            }
            CameraFollow2D cameraFollow = null;
            var camera = FindComponent<Camera>(scene);
            if (camera != null) cameraFollow = camera.GetComponent<CameraFollow2D>();
            if (cameraFollow == null)
                cameraFollow = HubSceneFactory.CreateCamera(player.transform);
            if (FindRoot(scene, "PlayerSpawn") == null)
                HubSceneFactory.CreateSpawnPoint();
            context = HubSceneFactory.CreateHubContext();
            HubSceneFactory.WirePlayer(player.gameObject, cameraFollow, context);
            if (!string.IsNullOrEmpty(spec.artSceneName)
                && FindComponent<HubArtSceneLoader>(scene) == null)
                HubSceneFactory.CreateArtLoader(spec.artSceneName);
            return context;
        }

        static void EnsurePanel<T>(Scene scene, string name, string uxmlPath,
            out T controller) where T : Component
        {
            controller = FindComponent<T>(scene);
            if (controller != null) return;
            var go = new GameObject(name);
            var renderer = go.AddComponent<PanelRenderer>();
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings != null) renderer.panelSettings = panelSettings;
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (uxml != null) renderer.visualTreeAsset = uxml;
            else Debug.LogWarning($"HubContentSetup: UXML not found at '{uxmlPath}'.");
            controller = go.AddComponent<T>();
        }

        static void EnsureCaseDoor(Scene scene)
        {
            var existing = FindRoot(scene, "CaseDoor");
            if (scene.path != IronHollowPath)
            {
                if (existing != null) existing.SetActive(false);
                return;
            }
            if (existing != null) return;
            HubSceneFactory.CreateInteractable("CaseDoor",
                new Color(0.7f, 0.35f, 0.5f), new Vector3(6f, 2.5f, 0f),
                out CarrierInteractable door);
            door.promptText = "[E] Begin Case";
            door.checkpointReason = "CaseDoor";
            door.routeKey = "caseloop";
            door.targetScene = "CaseLoop";
        }

        static void EnsureRestSpot(Scene scene)
        {
            foreach (var rest in UnityEngine.Object.FindObjectsByType<RestSpot>())
                if (rest.gameObject.scene == scene) return;
            HubSceneFactory.CreateInteractable("RestSpot",
                new Color(0.4f, 0.55f, 0.35f), new Vector3(-3f, 2.5f, 0f),
                out RestSpot spot);
            spot.promptText = "[E] Rest";
        }

        /// <summary>The fast-travel carrier: becomes a destination picker.</summary>
        static void EnsureCarrier(Scene scene)
        {
            var carriers = UnityEngine.Object.FindObjectsByType<CarrierInteractable>();
            CarrierInteractable carrier = null;
            foreach (var c in carriers)
                if (c.gameObject.scene == scene && c.name == "Carrier") carrier = c;
            if (carrier == null)
            {
                HubSceneFactory.CreateInteractable("Carrier",
                    new Color(0.3f, 0.6f, 0.4f), new Vector3(-6f, -1f, 0f),
                    out carrier);
                carrier.promptText = "[E] Travel";
                carrier.targetScene = "Hub_LowerMarket";
            }
            carrier.offerTravelMenu = true;
        }

        /// <summary>Points the hub journal at the case's runic log + unease flag.</summary>
        static void ConfigureJournal(Scene scene, int caseId)
        {
            var journal = FindComponent<JournalController>(scene);
            if (journal == null) return;
            string logPath = $"Assets/Data/Cases/Case0{caseId}/RunicLog_0{caseId}.asset";
            var log = AssetDatabase.LoadAssetAtPath<RunicLogData>(logPath);
            if (log != null)
            {
                journal.allLogs = new[] { log };
                journal.dissonanceLocationText = log.location;
            }
            else Debug.LogWarning($"HubContentSetup: runic log missing at '{logPath}'.");
            journal.dissonanceFlag = $"Case{caseId}_UneaseActive";
        }

        static void EnsureInterlude(Scene scene, HubSpec spec)
        {
            if (FindComponent<HubArrivalTrigger>(scene) != null) return;
            var go = new GameObject("ArrivalTrigger");
            var trigger = go.AddComponent<HubArrivalTrigger>();
            trigger.dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(spec.interludeDialoguePath);
            if (trigger.dialogue == null)
                Debug.LogWarning($"HubContentSetup: interlude dialogue missing at " +
                                 $"'{spec.interludeDialoguePath}' (run the hub dialogue generator).");
            trigger.playedFlag = spec.interludeFlag;
            trigger.minCurrentCase = 5;
        }

        // --- Per-hub content -----------------------------------------------------

        /// <summary>Lower Market already ships its Case 1 content; nothing extra.</summary>
        static void PopulateMarket()
        {
            var suspect = FindRoot(SceneManager.GetActiveScene(), "Ignatius");
            var npc = suspect != null ? suspect.GetComponent<NpcInteractable>() : null;
            if (npc != null) npc.interrogationCaseId = 1;
        }

        static void PopulateCathedral()
        {
            Npc("ValeriusAltar", new Color(0.6f, 0.6f, 0.75f), new Vector3(1f, 3f, 0f),
                "Assets/Data/Dialogue/Case02/D_Valerius_Lyra_02.asset", "[E] Talk");
            Npc("Sylvan", new Color(0.4f, 0.75f, 0.6f), new Vector3(-4f, 3f, 0f),
                "Assets/Data/Dialogue/Case02/D_Sylvan_02.asset", "[E] Talk");
            Npc("Accord", new Color(0.7f, 0.55f, 0.3f), new Vector3(4.5f, 1f, 0f),
                "Assets/Data/Dialogue/Case02/D_Accord_02.asset", "[E] Talk", 2);
            Npc("NaveCrowd", new Color(0.5f, 0.5f, 0.5f), new Vector3(-1.5f, -2f, 0f),
                "Assets/Data/Dialogue/Case02/D_NaveBarks_02.asset", "[E] Listen");
            Inspection("TuningFork", new Color(0.85f, 0.8f, 0.5f), new Vector3(-4.5f, -1f, 0f),
                "Assets/Data/Cases/Case02/RH1_02.asset");
            Gate("BelfryStair", new Color(0.45f, 0.3f, 0.7f), new Vector3(6f, 2f, 0f),
                HubDialogueContentGenerator.BelfryKeyFlag, "investigation.2",
                "Assets/Data/Dialogue/Case02/D_Gate_Locked_02.asset", "[E] Enter");
        }

        static void PopulateWhisperwood()
        {
            Npc("Kaelen", new Color(0.35f, 0.55f, 0.3f), new Vector3(-2f, 1f, 0f),
                "Assets/Data/Dialogue/Case03/D_Kaelen_03.asset", "[E] Talk");
            Npc("Vane", new Color(0.55f, 0.3f, 0.55f), new Vector3(3.5f, 1.5f, 0f),
                "Assets/Data/Dialogue/Case03/D_Vane_03.asset", "[E] Talk", 3);
            Npc("ForestOutpost", new Color(0.4f, 0.5f, 0.35f), new Vector3(-4.5f, -2f, 0f),
                "Assets/Data/Dialogue/Case03/D_ForestBarks_03.asset", "[E] Listen");
            Inspection("BlightBark", new Color(0.5f, 0.4f, 0.3f), new Vector3(1f, -2.5f, 0f),
                "Assets/Data/Cases/Case03/RH1_03.asset");
            Gate("BriarGate", new Color(0.3f, 0.45f, 0.25f), new Vector3(6f, 0.5f, 0f),
                HubDialogueContentGenerator.PerimeterSealFlag, "investigation.3",
                "Assets/Data/Dialogue/Case03/D_Gate_Locked_03.asset", "[E] Enter");
        }

        static void PopulateIronHollow()
        {
            Npc("Thrun", new Color(0.65f, 0.45f, 0.25f), new Vector3(2f, 2f, 0f),
                "Assets/Data/Dialogue/Case04/D_Thrun_04.asset", "[E] Talk");
            Npc("Harlan", new Color(0.4f, 0.35f, 0.3f), new Vector3(4.5f, -1f, 0f),
                "Assets/Data/Dialogue/Case04/D_Harlan_04.asset", "[E] Talk", 4);
            Npc("MinerCrowd", new Color(0.45f, 0.4f, 0.35f), new Vector3(-3f, -2f, 0f),
                "Assets/Data/Dialogue/Case04/D_IndustrialBarks_04.asset", "[E] Listen");
            Inspection("DrillBit", new Color(0.55f, 0.55f, 0.6f), new Vector3(-4.5f, 1f, 0f),
                "Assets/Data/Cases/Case04/RH1_04.asset");
            Gate("DeepShaftLift", new Color(0.4f, 0.35f, 0.5f), new Vector3(6f, 1.5f, 0f),
                HubDialogueContentGenerator.OverrideClearanceFlag, "investigation.4",
                "Assets/Data/Dialogue/Case04/D_Gate_Locked_04.asset", "[E] Enter");
        }

        /// <summary>
        /// Upgrades pre-label interactables in place: a legacy quad "Prompt"
        /// becomes an "[E]" text label (Interactable2D syncs it to promptText
        /// when shown), and every block gets a permanent NameLabel above it.
        /// </summary>
        static void EnsureInteractablePresentation(Scene scene)
        {
            foreach (var it in UnityEngine.Object.FindObjectsByType<Interactable2D>())
            {
                if (it.gameObject.scene != scene) continue;
                var t = it.transform;
                var prompt = t.Find("Prompt");
                if (prompt != null && prompt.GetComponent<InteractableLabel>() == null)
                {
                    UnityEngine.Object.DestroyImmediate(prompt.gameObject);
                    prompt = null;
                }
                if (prompt == null)
                {
                    var label = HubSceneFactory.NewWorldLabel("Prompt", "[E]",
                        t.position + HubSceneFactory.PromptOffset,
                        HubSceneFactory.PromptFontSize, new Color(1f, 0.85f, 0.2f));
                    label.transform.SetParent(t, true);
                    label.SetActive(false);
                    it.promptIndicator = label;
                }
                else if (it.promptIndicator == null)
                    it.promptIndicator = prompt.gameObject;

                var nameLabel = t.Find("NameLabel");
                if (nameLabel != null && nameLabel.GetComponent<InteractableLabel>() == null)
                {
                    UnityEngine.Object.DestroyImmediate(nameLabel.gameObject);
                    nameLabel = null;
                }
                if (nameLabel == null)
                {
                    var tag = HubSceneFactory.NewWorldLabel("NameLabel", it.name,
                        t.position + HubSceneFactory.NameOffset,
                        HubSceneFactory.NameFontSize, new Color(1f, 1f, 1f, 0.85f));
                    tag.transform.SetParent(t, true);
                }
            }
        }

        /// <summary>Runs only the presentation upgrade on a non-hub scene.</summary>
        static void UpgradePresentationOnly(string scenePath)
        {
            if (!File.Exists(scenePath)) return;
            var preExisting = SceneManager.GetSceneByPath(scenePath);
            bool wasOpen = preExisting.IsValid() && preExisting.isLoaded;
            if (wasOpen && preExisting.isDirty)
            {
                Debug.LogError($"HubContentSetup: '{scenePath}' is open with " +
                               "unsaved changes; skipped.");
                return;
            }
            var previousActive = SceneManager.GetActiveScene();
            var scene = wasOpen ? preExisting
                : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                EnsureInteractablePresentation(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                    Debug.Log($"HubContentSetup: '{scenePath}' labels upgraded.");
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
                if (!wasOpen && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        // --- Content helpers (all name-guarded) --------------------------------

        static void Npc(string name, Color tint, Vector3 pos, string dialoguePath, string prompt,
            int interrogationCaseId = -1)
        {
            var existing = FindRoot(SceneManager.GetActiveScene(), name);
            if (existing != null)
            {
                var existingNpc = existing.GetComponent<NpcInteractable>();
                if (existingNpc != null && interrogationCaseId > 0)
                    existingNpc.interrogationCaseId = interrogationCaseId;
                return;
            }
            HubSceneFactory.CreateInteractable(name, tint, pos, out NpcInteractable npc);
            npc.interrogationCaseId = interrogationCaseId;
            npc.promptText = prompt;
            npc.dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(dialoguePath);
            if (npc.dialogue == null)
                Debug.LogWarning($"HubContentSetup: dialogue missing at '{dialoguePath}' " +
                                 "(run Morsoul/Data/Generate Hub Dialogue first).");
        }

        static void Inspection(string name, Color tint, Vector3 pos, string cluePath)
        {
            if (FindRoot(SceneManager.GetActiveScene(), name) != null) return;
            HubSceneFactory.CreateInteractable(name, tint, pos, out InspectionInteractable inspect);
            inspect.promptText = "[E] Inspect";
            inspect.clue = AssetDatabase.LoadAssetAtPath<ClueData>(cluePath);
            if (inspect.clue == null)
                Debug.LogWarning($"HubContentSetup: clue asset missing at '{cluePath}'.");
        }

        static void Gate(string name, Color tint, Vector3 pos, string flag, string routeKey,
            string lockedDialoguePath, string prompt)
        {
            if (FindRoot(SceneManager.GetActiveScene(), name) != null) return;
            HubSceneFactory.CreateInteractable(name, tint, pos, out GateInteractable gate);
            gate.promptText = prompt;
            gate.requiredFlag = flag;
            gate.routeKey = routeKey;
            gate.lockedDialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(lockedDialoguePath);
            if (gate.lockedDialogue == null)
                Debug.LogWarning($"HubContentSetup: locked dialogue missing at '{lockedDialoguePath}'.");
        }

        // --- Misc ----------------------------------------------------------------

        static T FindComponent<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
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
            Add(CathedralPath);
            Add(IronHollowPath);
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
