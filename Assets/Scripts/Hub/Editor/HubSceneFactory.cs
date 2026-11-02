using System.IO;
using Morsoul.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Menu-driven factory that blockouts the 2D hub scenes. Creates a
    /// placeholder white sprite, an orthographic camera with CameraFollow2D, a
    /// Rigidbody2D player, the HubContext (flags + journal + dialogue panel)
    /// wiring and sample interactables. Idempotent: existing scenes prompt a
    /// warn+overwrite dialog.
    /// </summary>
    public static class HubSceneFactory
    {
        const string ScenesFolder = "Assets/Scenes";
        const string LowerMarketScenePath = ScenesFolder + "/Hub_LowerMarket.unity";
        const string WildernessScenePath = ScenesFolder + "/Hub_Wilderness.unity";

        const string PanelSettingsPath = "Assets/UI Toolkit/MainPanelSettings.asset";
        const string DialogueUxmlPath = "Assets/Dialogue.uxml";
        const string Case01DialogueFolder = "Assets/Data/Dialogue/Case01";

        [MenuItem("Morsoul/Hub/Create Hub Scene (Lower Market)")]
        static void CreateLowerMarket()
        {
            if (!ConfirmOverwrite(LowerMarketScenePath)) return;
            var scene = NewEmptyScene();

            var player = CreatePlayer();
            var cameraFollow = CreateCamera(player.transform);
            CreateSpawnPoint();
            var context = CreateHubContext();
            WirePlayer(player, cameraFollow, context);

            // Notice board: one-shot clue inspection point.
            CreateInteractable("NoticeBoard",
                new Color(0.65f, 0.5f, 0.25f), new Vector3(2.5f, 1f, 0f),
                out InspectionInteractable boardInteractable);
            boardInteractable.promptText = "[E] Inspect";
            // clue left unassigned - designer assigns a ClueData in the inspector.

            // Vault gate: locked behind the warrant flag.
            CreateInteractable("VaultGate",
                new Color(0.45f, 0.3f, 0.7f), new Vector3(6f, -1f, 0f),
                out GateInteractable gateInteractable);
            gateInteractable.promptText = "[E] Enter";
            gateInteractable.requiredFlag = "C1_WarrantGranted";
            gateInteractable.targetScene = "C1_Vault";
            gateInteractable.lockedDialogue =
                LoadDialogue(Case01DialogueFolder + "/D_Gate_Locked_01.asset");

            // Ovan: market contact; his tree registers T1 and can grant the warrant.
            CreateInteractable("Ovan",
                new Color(0.8f, 0.6f, 0.2f), new Vector3(2.5f, -1f, 0f),
                out NpcInteractable ovan);
            ovan.promptText = "[E] Talk";
            ovan.dialogue = LoadDialogue(Case01DialogueFolder + "/D_Ovan_01.asset");

            // Ignatius: his claim registers T2.
            CreateInteractable("Ignatius",
                new Color(0.6f, 0.25f, 0.25f), new Vector3(4f, 1.5f, 0f),
                out NpcInteractable ignatius);
            ignatius.promptText = "[E] Talk";
            ignatius.dialogue =
                LoadDialogue(Case01DialogueFolder + "/D_Ignatius_Claim_01.asset");

            // Market crowd: ambient barks.
            CreateInteractable("MarketCrowd",
                new Color(0.5f, 0.5f, 0.5f), new Vector3(-2.5f, 1.5f, 0f),
                out NpcInteractable crowd);
            crowd.promptText = "[E] Listen";
            crowd.dialogue =
                LoadDialogue(Case01DialogueFolder + "/D_MarketBarks_01.asset");

            // Wilderness carrier: free travel to the next hub.
            CreateInteractable("Carrier",
                new Color(0.3f, 0.6f, 0.4f), new Vector3(-6f, -1f, 0f),
                out CarrierInteractable carrierInteractable);
            carrierInteractable.promptText = "[E] Travel";
            carrierInteractable.targetScene = "Hub_Wilderness";

            SaveScene(scene, LowerMarketScenePath);
        }

        [MenuItem("Morsoul/Hub/Create Hub Scene (Wilderness)")]
        static void CreateWilderness()
        {
            if (!ConfirmOverwrite(WildernessScenePath)) return;
            var scene = NewEmptyScene();

            var player = CreatePlayer();
            var cameraFollow = CreateCamera(player.transform);
            CreateSpawnPoint();
            var context = CreateHubContext();
            WirePlayer(player, cameraFollow, context);

            // Trail exit: always-open gate back to the Lower Market.
            CreateInteractable("TrailExit",
                new Color(0.35f, 0.5f, 0.35f), new Vector3(-6f, 0f, 0f),
                out GateInteractable exitGate);
            exitGate.promptText = "[E] Leave";
            exitGate.requiredFlag = ""; // ungated
            exitGate.targetScene = "Hub_LowerMarket";

            SaveScene(scene, WildernessScenePath);
        }

        // --- Scene assembly ---------------------------------------------------

        static Scene NewEmptyScene() =>
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        static GameObject CreatePlayer()
        {
            var player = NewQuadObject("Player",
                new Color(0.4f, 0.7f, 1f), Vector3.zero);
            player.tag = "Player";
            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            player.AddComponent<HubPlayerController>();
            return player;
        }

        static CameraFollow2D CreateCamera(Transform target)
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            var follow = cameraGo.AddComponent<CameraFollow2D>();
            follow.target = target;
            return follow;
        }

        static void CreateSpawnPoint()
        {
            var spawn = new GameObject("PlayerSpawn");
            spawn.tag = "PlayerSpawn";
            var entry = new GameObject("SceneEntryPoint");
            entry.AddComponent<SceneEntryPoint>();
        }

        static HubContext CreateHubContext()
        {
            var contextGo = new GameObject("HubContext");
            var context = contextGo.AddComponent<HubContext>();
            context.flags = contextGo.AddComponent<HubFlags>();
            context.journal = contextGo.AddComponent<HubJournal>();

            var dialogueGo = new GameObject("DialoguePanel");
            var renderer = dialogueGo.AddComponent<PanelRenderer>();
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings != null) renderer.panelSettings = panelSettings;
            else Debug.LogWarning($"HubSceneFactory: PanelSettings not found at '{PanelSettingsPath}'.");
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DialogueUxmlPath);
            if (uxml != null) renderer.visualTreeAsset = uxml;
            else Debug.LogWarning($"HubSceneFactory: UXML not found at '{DialogueUxmlPath}'.");
            context.dialogue = dialogueGo.AddComponent<DialogueController>();
            return context;
        }

        static void WirePlayer(GameObject player, CameraFollow2D cameraFollow, HubContext context)
        {
            var controller = player.GetComponent<HubPlayerController>();
            controller.context = context;
            if (cameraFollow != null) cameraFollow.target = player.transform;
        }

        static GameObject CreateInteractable<T>(string name, Color tint,
            Vector3 position, out T interactable) where T : Interactable2D
        {
            var go = NewQuadObject(name, tint, position);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            interactable = go.AddComponent<T>();

            var prompt = NewQuadObject("Prompt",
                new Color(1f, 0.9f, 0.2f), go.transform.position + new Vector3(0f, 0.9f, 0f));
            prompt.transform.SetParent(go.transform, true);
            prompt.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
            prompt.SetActive(false);
            interactable.promptIndicator = prompt;
            return go;
        }

        /// <summary>
        /// Blockout visual: a 1x1 quad with a per-tint URP unlit material.
        /// Sprites proved unreliable in this project (import/PPU/material
        /// pitfalls); a quad with a colored material always renders.
        /// </summary>
        static GameObject NewQuadObject(string name, Color tint, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.position = position;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = EnsureBlockoutMaterial(tint);
            return go;
        }

        // --- Assets -----------------------------------------------------------

        static DialogueData LoadDialogue(string path)
        {
            var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (data == null)
                Debug.LogWarning($"HubSceneFactory: DialogueData not found at '{path}' " +
                                 "(run Morsoul/Data/Generate Case 1 Hub Dialogue first).");
            return data;
        }

        /// <summary>
        /// One URP unlit material asset per tint, kept under
        /// Assets/Materials/Blockout. Idempotent; updates color on reuse.
        /// </summary>
        static Material EnsureBlockoutMaterial(Color tint)
        {
            EnsureFolder("Assets/Materials/Blockout");
            string path = $"Assets/Materials/Blockout/" +
                          $"{ColorUtility.ToHtmlStringRGB(tint)}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    Debug.LogError("HubSceneFactory: URP Unlit shader not found.");
                    return null;
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", tint);
            EditorUtility.SetDirty(material);
            return material;
        }

        static bool ConfirmOverwrite(string scenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null
                && !File.Exists(scenePath))
                return true;
            bool overwrite = EditorUtility.DisplayDialog(
                "Create Hub Scene",
                $"'{scenePath}' already exists. Overwrite it?",
                "Overwrite",
                "Cancel");
            if (!overwrite)
                Debug.LogWarning($"HubSceneFactory: cancelled; '{scenePath}' left untouched.");
            return overwrite;
        }

        static void SaveScene(Scene scene, string scenePath)
        {
            EnsureFolder(ScenesFolder);
            if (EditorSceneManager.SaveScene(scene, scenePath))
                Debug.Log($"HubSceneFactory: hub scene saved to '{scenePath}'.");
            else
                Debug.LogError($"HubSceneFactory: failed to save scene to '{scenePath}'.");
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
