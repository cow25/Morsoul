using System.Linq;
using Morsoul.Data;
using Morsoul.Hub;
using Morsoul.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Editor
{
    /// <summary>
    /// Idempotent, non-destructive patcher that adds the 4 new 3D ClueNodes
    /// (C4_01, C4_02, C4_03, C5_04) and 4 new 2D Hub InspectionInteractables
    /// (C5_01, C5_02, C5_03, C4_04) to existing live scenes without overwriting
    /// manual scene edits.
    /// </summary>
    public static class DeductionScenePatcher
    {
        const string Mfp = "Assets/Medieval Furniture Pack/Prefabs/";
        const string Dfk = "Assets/KUBIKOS - RPG & Fantasy/Dungeon Fantasy Kit/Prefabs/";

        [MenuItem("Morsoul/Data/Patch 2D & 3D Scenes With New Clues")]
        public static void PatchAllScenes()
        {
            string originalScene = SceneManager.GetActiveScene().path;

            Patch3DScene(
                "Assets/Scenes/3D/Case1UndegroundStorageVault.unity",
                "C4_IndemnityClaim", "C4_01",
                "Pre-signed Treasury compensation claim for Batch #408, dated two days before the breach, naming Ignatius as sole beneficiary.",
                new Vector3(-2.4f, 0.86f, 8.8f), new Vector3(-2.4f, 1.0f, 8.8f), new Vector3(1.4f, 1.1f, 1.4f),
                Mfp + "Miscellaneous/scroll.prefab", new Vector3(0f, 35f, 0f), new Vector3(1.3f, 1.3f, 1.3f),
                PrimitiveType.Cube, Color.white,
                new[] { "C1_01", "C2_01", "C3_01", "C4_01" },
                "Inspect vault lock, scan solvent residue [Tab], check cargo desk, and search the inner vault ({0}/{1}).");

            Patch3DScene(
                "Assets/Scenes/3D/Case2CathedralBelfry.unity",
                "C4_SootStainedRag", "C4_02",
                "Oily polishing cloth tucked into Accord's belfry bench, coated in the same black metallic damping soot found on the clapper.",
                new Vector3(4.6f, 4.05f, 12.6f), new Vector3(4.6f, 4.35f, 12.6f), new Vector3(1.4f, 1.1f, 1.4f),
                Dfk + "Decoration/Textiles/dfk_carpet_black_small.prefab", new Vector3(0f, -20f, 0f), new Vector3(0.45f, 0.45f, 0.45f),
                PrimitiveType.Cube, new Color(0.18f, 0.16f, 0.20f),
                new[] { "C1_02", "C2_02", "C3_02", "C4_02" },
                "Inspect the shattered crystal, climb to the Great Bell [Tab], and search both upper galleries ({0}/{1}).");

            Patch3DScene(
                "Assets/Scenes/3D/Case3WhisperwoodGrove.unity",
                "C4_ManaResinCanister", "C4_03",
                "Pressurized brass canister filled with luminous sap, stenciled for Veiled Circle shipment to Iron Hollow Drift 09.",
                new Vector3(4.8f, 0.45f, 8.2f), new Vector3(4.8f, 0.55f, 8.2f), new Vector3(1.4f, 1.2f, 1.4f),
                null, Vector3.zero, new Vector3(0.45f, 0.45f, 0.45f),
                PrimitiveType.Cylinder, new Color(0.76f, 0.56f, 0.22f),
                new[] { "C1_03", "C2_03", "C3_03", "C4_03" },
                "Inspect the Elder Tree siphon, ritual dagger, Vane's cloak, and mana canister ({0}/{1}).");

            Patch3DScene(
                "Assets/Scenes/3D/Case4IronHollowSubFacility.unity",
                "C5_StasisOverrideRelay", "C5_04",
                "Auxiliary lockout relay mounted outside the blast door, bridged with Veiled Circle copper shunts to trap Brokk inside.",
                new Vector3(2.3f, 1.2f, 11.4f), new Vector3(2.3f, 1.2f, 11.4f), new Vector3(1.3f, 1.4f, 1.2f),
                null, Vector3.zero, new Vector3(0.45f, 0.65f, 0.35f),
                PrimitiveType.Cube, new Color(0.55f, 0.32f, 0.18f),
                new[] { "C1_04", "C2_04", "C3_04", "C5_04" },
                "Inspect pneumatic valves, scan bypass conduits [Tab], check the blast door, and examine the stasis relay ({0}/{1}).");

            Patch2DHubScene(
                "Assets/Scenes/Hub_LowerMarket.unity",
                "SolventCrate_C5_01",
                "Assets/Data/Cases/Case01/C5_01.asset",
                "[E] Inspect Unmarked Solvent Crate",
                new[] { "Shop_IgnatiusWorkshop", "Ignatius", "VaultGrate" },
                new Vector3(1.4f, -0.6f, 0f));

            Patch2DHubScene(
                "Assets/Scenes/Hub_CathedralNave.unity",
                "SanctuaryNotice_C5_02",
                "Assets/Data/Cases/Case02/C5_02.asset",
                "[E] Inspect Marian's Sanctuary Debt Notice",
                new[] { "MotherLyra", "Accord", "NavePew" },
                new Vector3(-1.6f, -0.8f, 0f));

            Patch2DHubScene(
                "Assets/Scenes/Hub_Wilderness.unity",
                "VaneSurveyLedger_C5_03",
                "Assets/Data/Cases/Case03/C5_03.asset",
                "[E] Inspect Vane's Harvest Tally",
                new[] { "Vane", "Kaelen", "ForestOutpost" },
                new Vector3(1.5f, -0.5f, 0f));

            Patch2DHubScene(
                "Assets/Scenes/Hub_IronHollow.unity",
                "DeepDriftManifest_C4_04",
                "Assets/Data/Cases/Case04/C4_04.asset",
                "[E] Inspect Deep-Drift Freight Manifest",
                new[] { "Iron_Workbench_East", "Harlan", "Thrun" },
                new Vector3(1.2f, -0.6f, 0f));

            if (!string.IsNullOrEmpty(originalScene) && System.IO.File.Exists(originalScene))
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

            Debug.Log("DeductionScenePatcher: completed patching all 4 3D scenes and 4 2D hub scenes.");
        }

        static void Patch3DScene(
            string scenePath,
            string nodeName,
            string clueId,
            string description,
            Vector3 propPos,
            Vector3 nodePos,
            Vector3 colliderSize,
            string prefabPath,
            Vector3 propEuler,
            Vector3 propScale,
            PrimitiveType fallbackPrimitive,
            Color fallbackColor,
            string[] requiredClues,
            string gatherFormat)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool dirty = false;

            var existing = Object.FindObjectsByType<ClueNode>(FindObjectsInactive.Include)
                .FirstOrDefault(n => n.id == clueId);
            if (existing == null)
            {
                var cluesRoot = GameObject.Find("Clues")?.transform;
                GameObject visual = null;
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab != null)
                    {
                        visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                        if (cluesRoot != null) visual.transform.SetParent(cluesRoot, false);
                        visual.transform.position = propPos;
                        visual.transform.eulerAngles = propEuler;
                        visual.transform.localScale = propScale;
                    }
                }
                if (visual == null)
                {
                    visual = GameObject.CreatePrimitive(fallbackPrimitive);
                    visual.name = nodeName + "_Visual";
                    if (cluesRoot != null) visual.transform.SetParent(cluesRoot, false);
                    visual.transform.position = propPos;
                    visual.transform.eulerAngles = propEuler;
                    visual.transform.localScale = propScale;
                    var col = visual.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);
                    var rend = visual.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                        mat.color = fallbackColor;
                        rend.sharedMaterial = mat;
                    }
                }

                var go = new GameObject(nodeName);
                int interactLayer = LayerMask.NameToLayer("Interactable");
                if (interactLayer >= 0) go.layer = interactLayer;
                SceneManager.MoveGameObjectToScene(go, scene);
                if (cluesRoot != null) go.transform.SetParent(cluesRoot, false);
                go.transform.position = nodePos;
                var box = go.AddComponent<BoxCollider>();
                box.size = colliderSize;
                box.isTrigger = true;

                var node = go.AddComponent<ClueNode>();
                node.id = clueId;
                node.kind = ClueNode.Kind.Physical;
                node.requiredTool = ClueNode.Tool.None;
                node.state = ClueNode.State.Revealed;
                node.defaultDescription = description;
                node.inspectVisual = visual;

                var embersPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ground Embers Variant.prefab");
                if (embersPrefab != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(embersPrefab, go.transform);
                    inst.name = "Ground Embers";
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localRotation = Quaternion.identity;
                    inst.transform.localScale = new Vector3(0.01f, 0.2f, 0.01f);
                    var ps = inst.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        var main = ps.main;
                        main.playOnAwake = false;
                        node.scanParticles = ps;
                    }
                }
                dirty = true;
            }

            var objCtrl = Object.FindAnyObjectByType<InvestigationObjectiveController>(FindObjectsInactive.Include);
            if (objCtrl != null)
            {
                if (objCtrl.requiredClueIds == null || !objCtrl.requiredClueIds.SequenceEqual(requiredClues))
                {
                    objCtrl.requiredClueIds = requiredClues;
                    objCtrl.gatherObjectiveFormat = gatherFormat;
                    EditorUtility.SetDirty(objCtrl);
                    dirty = true;
                }
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"DeductionScenePatcher: updated 3D scene {scenePath} (+{clueId}).");
            }
            else
            {
                Debug.Log($"DeductionScenePatcher: 3D scene {scenePath} already up to date.");
            }
        }

        static void Patch2DHubScene(
            string scenePath,
            string goName,
            string clueAssetPath,
            string promptText,
            string[] anchorNames,
            Vector3 offset)
        {
            var clue = AssetDatabase.LoadAssetAtPath<ClueData>(clueAssetPath);
            if (clue == null)
            {
                Debug.LogError($"DeductionScenePatcher: missing clue asset '{clueAssetPath}'.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var existing = Object.FindObjectsByType<InspectionInteractable>(FindObjectsInactive.Include)
                .FirstOrDefault(i => (i.clue != null && i.clue.id == clue.id) || i.gameObject.name == goName);
            if (existing != null)
            {
                if (existing.clue != clue)
                {
                    existing.clue = clue;
                    EditorUtility.SetDirty(existing);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                Debug.Log($"DeductionScenePatcher: 2D hub {scenePath} already has {clue.id}.");
                return;
            }

            Vector3 spawnPos = Vector3.zero;
            Transform parent = null;
            foreach (string anchorName in anchorNames)
            {
                var anchor = GameObject.Find(anchorName);
                if (anchor != null)
                {
                    spawnPos = anchor.transform.position + offset;
                    parent = anchor.transform.parent;
                    break;
                }
            }

            // Clone sprite style from an existing InspectionInteractable in the hub if available.
            var template = Object.FindObjectsByType<InspectionInteractable>(FindObjectsInactive.Include)
                .FirstOrDefault();

            var go = new GameObject(goName);
            SceneManager.MoveGameObjectToScene(go, scene);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.85f;

            var sr = go.AddComponent<SpriteRenderer>();
            if (template != null && template.TryGetComponent<SpriteRenderer>(out var templateSr) && templateSr.sprite != null)
            {
                sr.sprite = templateSr.sprite;
                sr.sortingLayerID = templateSr.sortingLayerID;
                sr.sortingOrder = templateSr.sortingOrder;
                sr.color = new Color(0.95f, 0.85f, 0.65f, 1f);
            }

            var interactable = go.AddComponent<InspectionInteractable>();
            interactable.clue = clue;
            interactable.promptText = promptText;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"DeductionScenePatcher: updated 2D hub {scenePath} (+{clue.id} at {go.transform.position}).");
        }
    }
}
