using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Builds the dedicated 2D wilderness approach between the ruins gate and
    /// the Lower Market. Idempotent and batch-safe; it owns only this scene's
    /// Prologue staging objects and shares the standard hub player/camera/UI.
    /// </summary>
    public static class PrologueWildernessSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Prologue_Wilderness.unity";
        const string SceneName = "Prologue_Wilderness";

        [MenuItem("Morsoul/Hub/Create Prologue Wilderness Scene")]
        public static void CreateOrUpdate()
        {
            var preExisting = SceneManager.GetSceneByPath(ScenePath);
            bool wasOpen = preExisting.IsValid() && preExisting.isLoaded;
            if (wasOpen && preExisting.isDirty)
            {
                Debug.LogError($"PrologueWildernessSceneBuilder: '{ScenePath}' is open " +
                               "with unsaved changes; skipped. Save or revert it first.");
                return;
            }

            bool created = !File.Exists(ScenePath);
            if (!created && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Prologue Wilderness Scene",
                    $"'{ScenePath}' already exists. Update its generated staging objects?",
                    "Update", "Cancel"))
            {
                Debug.LogWarning($"PrologueWildernessSceneBuilder: '{ScenePath}' left untouched.");
                return;
            }

            var previousActive = SceneManager.GetActiveScene();
            var scene = wasOpen ? preExisting
                : created ? HubSceneFactory.NewEmptyScene()
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var context = EnsureBaseScene(scene);
                var player = FindComponent<HubPlayerController>(scene);
                var camera = FindComponent<CameraFollow2D>(scene);
                var cameraTarget = EnsureEmpty("ElizaIntroCameraTarget", new Vector3(8.75f, 0f, 0f));
                var eliza = EnsureQuad("Eliza", new Color(0.55f, 0.2f, 0.2f),
                    new Vector3(12f, 0f, 0f), active: false);
                var key = EnsureQuad("AncientChamberKey", new Color(0.9f, 0.75f, 0.25f),
                    new Vector3(7.7f, 0.35f, 0f), active: false);
                key.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
                var beacon = EnsureQuad("DiagnosticBeacon", new Color(0.1f, 0.85f, 1f),
                    new Vector3(7.2f, 0.55f, 0f), active: false);
                beacon.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
                var blocker = EnsureQuad("EasternRouteBlocker", new Color(0.2f, 0.25f, 0.3f),
                    new Vector3(24f, -0.5f, 0f), active: true);
                blocker.transform.localScale = new Vector3(1f, 4f, 1f);
                var blockerCollider = EnsureComponent<BoxCollider2D>(blocker);
                blockerCollider.isTrigger = false;
                blockerCollider.size = Vector2.one;

                EnsureQuad("WildernessPath", new Color(0.22f, 0.28f, 0.2f),
                    new Vector3(8f, -0.75f, 0f), active: true)
                    .transform.localScale = new Vector3(34f, 1f, 1f);
                var northTrees = EnsureQuad("NorthTreeLine", new Color(0.08f, 0.18f, 0.11f),
                    new Vector3(8f, 2.2f, 0f), active: true);
                northTrees.transform.localScale = new Vector3(34f, 1.5f, 1f);
                var northCollider = EnsureComponent<BoxCollider2D>(northTrees);
                northCollider.isTrigger = false;
                northCollider.size = Vector2.one;
                var southTrees = EnsureQuad("SouthTreeLine", new Color(0.07f, 0.14f, 0.1f),
                    new Vector3(8f, -3.2f, 0f), active: true);
                southTrees.transform.localScale = new Vector3(34f, 1.5f, 1f);
                var southCollider = EnsureComponent<BoxCollider2D>(southTrees);
                southCollider.isTrigger = false;
                southCollider.size = Vector2.one;
                EnsureQuad("ElizaCover", new Color(0.1f, 0.22f, 0.14f),
                    new Vector3(12.7f, 0.2f, 0f), active: true)
                    .transform.localScale = new Vector3(1.2f, 1.4f, 1f);
                EnsureBoundary("WestBoundary", new Vector3(-10f, -0.5f, 0f),
                    new Vector2(1f, 7f));

                var elizaEmote = EnsureLabel("ElizaEmote", "",
                    eliza.transform.position + new Vector3(0f, 1.4f, 0f));
                elizaEmote.transform.SetParent(eliza.transform, true);
                elizaEmote.gameObject.SetActive(false);
                var playerEmote = EnsureLabel("PlayerEmote", "",
                    player.transform.position + new Vector3(0f, 1.4f, 0f));
                playerEmote.transform.SetParent(player.transform, true);
                playerEmote.gameObject.SetActive(false);

                var trigger = EnsureEmpty("ElizaIntroTrigger", new Vector3(8f, -0.5f, 0f));
                var triggerCollider = EnsureComponent<BoxCollider2D>(trigger);
                triggerCollider.isTrigger = true;
                triggerCollider.size = new Vector2(2f, 5f);
                var intro = EnsureComponent<PrologueElizaIntroController>(trigger);
                intro.context = context;
                intro.player = player;
                intro.cameraFollow = camera;
                intro.cameraTarget = cameraTarget.transform;
                intro.eliza = eliza.transform;
                intro.keyProp = key.transform;
                intro.diagnosticBeacon = beacon.transform;
                intro.elizaEmote = elizaEmote;
                intro.playerEmote = playerEmote;
                intro.routeBlocker = blocker;
                intro.safeResumePosition = new Vector3(6f, 0f, 0f);
                intro.nextSceneName = "Hub_LowerMarket";
                intro.nextCaseId = 1;
                intro.autoLoadNextScene = true;

                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene, ScenePath))
                    Debug.Log($"PrologueWildernessSceneBuilder: '{ScenePath}' {(created ? "created" : "updated")}.");
                else
                    Debug.LogError($"PrologueWildernessSceneBuilder: failed to save '{ScenePath}'.");
                RegisterBuildScene();
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
                if (!wasOpen && scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static HubContext EnsureBaseScene(Scene scene)
        {
            var player = FindComponent<HubPlayerController>(scene);
            if (player == null)
            {
                var playerGo = HubSceneFactory.CreatePlayer();
                player = playerGo.GetComponent<HubPlayerController>();
            }
            player.transform.position = new Vector3(-8f, 0f, 0f);

            var camera = FindComponent<CameraFollow2D>(scene);
            if (camera == null) camera = HubSceneFactory.CreateCamera(player.transform);
            var spawn = FindRoot(scene, "PlayerSpawn");
            if (spawn == null)
            {
                spawn = new GameObject("PlayerSpawn");
                spawn.tag = "PlayerSpawn";
            }
            spawn.transform.position = new Vector3(-8f, 0f, 0f);

            var entry = FindComponent<SceneEntryPoint>(scene);
            if (entry == null)
            {
                var entryGo = new GameObject("SceneEntryPoint");
                entry = entryGo.AddComponent<SceneEntryPoint>();
            }
            entry.spawnPointTag = "PlayerSpawn";
            entry.player = player.transform;

            var context = FindComponent<HubContext>(scene);
            if (context == null) context = HubSceneFactory.CreateHubContext();
            HubSceneFactory.WirePlayer(player.gameObject, camera, context);
            return context;
        }

        static T FindComponent<T>(Scene scene) where T : Component
        {
            foreach (var component in Object.FindObjectsByType<T>())
                if (component != null && component.gameObject.scene == scene)
                    return component;
            return null;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        static GameObject EnsureEmpty(string name, Vector3 position)
        {
            var scene = SceneManager.GetActiveScene();
            var go = FindRoot(scene, name) ?? new GameObject(name);
            go.transform.position = position;
            return go;
        }

        static GameObject EnsureQuad(string name, Color tint, Vector3 position, bool active)
        {
            var scene = SceneManager.GetActiveScene();
            var go = FindRoot(scene, name) ?? HubSceneFactory.NewQuadObject(name, tint, position);
            go.name = name;
            go.transform.position = position;
            go.SetActive(active);
            return go;
        }

        static GameObject EnsureBoundary(string name, Vector3 position, Vector2 size)
        {
            var go = EnsureEmpty(name, position);
            var collider = EnsureComponent<BoxCollider2D>(go);
            collider.isTrigger = false;
            collider.size = size;
            return go;
        }

        static InteractableLabel EnsureLabel(string name, string text, Vector3 position)
        {
            var scene = SceneManager.GetActiveScene();
            var go = FindRoot(scene, name);
            InteractableLabel label;
            if (go == null)
            {
                go = HubSceneFactory.NewWorldLabel(name, text, position,
                    HubSceneFactory.NameFontSize + 4, Color.white);
                label = go.GetComponent<InteractableLabel>();
            }
            else
            {
                go.transform.SetParent(null, true);
                go.transform.position = position;
                label = EnsureComponent<InteractableLabel>(go);
            }
            label.text = text;
            label.fontSize = HubSceneFactory.NameFontSize + 4;
            return label;
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        static void RegisterBuildScene()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var scene in list)
            {
                if (scene.path == ScenePath)
                {
                    scene.enabled = true;
                    EditorBuildSettings.scenes = list.ToArray();
                    return;
                }
            }
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
