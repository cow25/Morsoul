using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Adds the case-loop entry interactable ("CaseDoor" — an ungated
    /// CarrierInteractable targeting the CaseLoop scene) to existing saved hub
    /// scenes without regenerating their layouts. Idempotent: existing doors
    /// are repaired to routeKey "caseloop" with "CaseLoop" as the fallback.
    /// </summary>
    public static class HubCaseLoopSetup
    {
        const string LowerMarketScenePath = "Assets/Scenes/Hub_LowerMarket.unity";
        const string WildernessScenePath = "Assets/Scenes/Hub_Wilderness.unity";
        const string CathedralScenePath = "Assets/Scenes/Hub_CathedralNave.unity";
        const string IronHollowScenePath = "Assets/Scenes/Hub_IronHollow.unity";

        public static void Install()
        {
            // Batch mode starts on an untitled scene that can't host additive
            // opens — park on the first hub first (same guard as HubArtSetup).
            if (Application.isBatchMode && SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(SceneManager.GetActiveScene().path) &&
                !SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene(LowerMarketScenePath, OpenSceneMode.Single);
            Integrate(LowerMarketScenePath, new Vector3(-6f, 2.5f, 0f));
            Integrate(WildernessScenePath, new Vector3(6f, -1f, 0f));
            Integrate(CathedralScenePath, new Vector3(-6f, 2.5f, 0f));
            Integrate(IronHollowScenePath, new Vector3(-6f, -1f, 0f));
        }

        static void Integrate(string scenePath, Vector3 position)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"HubCaseLoopSetup: '{scenePath}' not found; skipped.");
                return;
            }
            var preExisting = SceneManager.GetSceneByPath(scenePath);
            bool wasOpen = preExisting.IsValid() && preExisting.isLoaded;
            if (wasOpen && preExisting.isDirty)
            {
                Debug.LogError($"HubCaseLoopSetup: '{scenePath}' is open with unsaved changes; " +
                               "skipped. Save or revert it first.");
                return;
            }
            var previousActive = SceneManager.GetActiveScene();
            var scene = wasOpen ? preExisting
                : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var existing = FindCaseDoor(scene);
                if (scenePath != IronHollowScenePath)
                {
                    if (existing != null)
                    {
                        existing.SetActive(false);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    return;
                }
                if (existing != null)
                {
                    var existingDoor = existing.GetComponent<CarrierInteractable>();
                    if (existingDoor != null &&
                        (existingDoor.routeKey != "caseloop" ||
                         existingDoor.targetScene != "CaseLoop"))
                    {
                        existingDoor.routeKey = "caseloop";
                        existingDoor.targetScene = "CaseLoop";
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    Debug.Log($"HubCaseLoopSetup: '{scenePath}' already has a CaseDoor; skipped.");
                    return;
                }
                SceneManager.SetActiveScene(scene);
                var go = HubSceneFactory.CreateInteractable("CaseDoor",
                    new Color(0.7f, 0.35f, 0.5f), position,
                    out CarrierInteractable door);
                door.promptText = "[E] Begin Case";
                door.checkpointReason = "CaseDoor";
                door.routeKey = "caseloop";
                door.targetScene = "CaseLoop";
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                    Debug.Log($"HubCaseLoopSetup: added CaseDoor to '{scenePath}'.");
                else
                    Debug.LogError($"HubCaseLoopSetup: failed to save '{scenePath}'.");
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
                if (!wasOpen && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static GameObject FindCaseDoor(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "CaseDoor") return root;
            return null;
        }
    }
}
