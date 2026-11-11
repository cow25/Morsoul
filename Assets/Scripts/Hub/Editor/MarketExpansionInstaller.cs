using Morsoul.Dialogue;
using Morsoul.Dialogue.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Expands the saved Hub_LowerMarket scene for the Guild-first-visit flow,
    /// without regenerating it: deactivates the outdoor Ovan (his desk moved
    /// upstairs inside the Guild), adds the GuildEntrance door, two ambient
    /// townsfolk, the inactive ArrivalEliza stand-in, and the
    /// MarketArrivalController that escorts the player inside. Idempotent —
    /// objects are looked up before creation.
    /// </summary>
    public static class MarketExpansionInstaller
    {
        const string MarketPath = "Assets/Scenes/Hub_LowerMarket.unity";
        const string BarksPath = "Assets/Data/Dialogue/Case01/D_MarketBarks_01.asset";

        public static void Install()
        {
            GuildIntroContentGenerator.Generate();

            var preExisting = SceneManager.GetSceneByPath(MarketPath);
            bool wasOpen = preExisting.IsValid() && preExisting.isLoaded;
            var scene = wasOpen ? preExisting : EditorSceneManager.OpenScene(MarketPath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            try
            {
                // Ovan's desk is upstairs inside the Guild now — keep the old
                // object for rollback but take it out of play.
                var ovan = FindRoot(scene, "Ovan");
                if (ovan != null && ovan.activeSelf)
                {
                    ovan.SetActive(false);
                    Debug.Log("MarketExpansionInstaller: outdoor Ovan deactivated " +
                              "(his office moved inside the Guild).");
                }

                // The door into the Guild hall — always open.
                if (FindRoot(scene, "GuildEntrance") == null)
                {
                    HubSceneFactory.CreateInteractable("GuildEntrance",
                        new Color(0.55f, 0.55f, 0.65f), new Vector3(3f, 2.9f, 0f),
                        out GateInteractable door);
                    door.promptText = "[E] Enter";
                    door.requiredFlag = "";
                    door.targetScene = "Hub_Guild";
                }

                // Named spawn at the Guild door — exits from the Guild carry
                // entryId "guild_door" and land here instead of the plaza spawn.
                var marker = FindRoot(scene, "Spawn_GuildDoor");
                if (marker == null)
                {
                    marker = new GameObject("Spawn_GuildDoor");
                    marker.AddComponent<SceneSpawnMarker>().entryId = "guild_door";
                }
                marker.transform.position = new Vector3(3f, 2.4f, 0f);

                // Ambient townsfolk placed sensibly around the market:
                // Vendor behind the west stall, Townsperson near north houses.
                var vendor = FindRoot(scene, "Vendor");
                if (vendor == null)
                {
                    HubSceneFactory.CreateInteractable("Vendor",
                        new Color(0.6f, 0.5f, 0.4f), new Vector3(-4.5f, -2.4f, 0f), out NpcInteractable npc);
                    npc.promptText = "[E] Talk";
                    npc.dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(BarksPath);
                }
                else
                {
                    vendor.transform.position = new Vector3(-4.5f, -2.4f, 0f);
                }

                var townsperson = FindRoot(scene, "Townsperson");
                if (townsperson == null)
                {
                    HubSceneFactory.CreateInteractable("Townsperson",
                        new Color(0.6f, 0.5f, 0.4f), new Vector3(-5.2f, 2.2f, 0f), out NpcInteractable npc);
                    npc.promptText = "[E] Talk";
                    npc.dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(BarksPath);
                }
                else
                {
                    townsperson.transform.position = new Vector3(-5.2f, 2.2f, 0f);
                }

                // Central crowd positioned in a clear plaza alcove
                var centralCrowd = FindRoot(scene, "MarketCrowd");
                if (centralCrowd != null)
                {
                    centralCrowd.transform.position = new Vector3(-3.8f, 0.8f, 0f);
                    var crowdNpc = centralCrowd.GetComponent<NpcInteractable>();
                    if (crowdNpc != null)
                    {
                        crowdNpc.promptText = "[E] Listen";
                        crowdNpc.blocksMovement = true;
                    }
                }

                // De-clutter crowd members across logical market spots:
                // MarketCrowd_Vendor behind east stall, Townsperson strolling north,
                // Adventurer conversing near the plaza well.
                var crowdPlacements = new (string, string, Vector3, Color)[]
                {
                    ("MarketCrowd_Vendor", "Vendor", new Vector3(4.5f, -2.4f, 0f), new Color(0.6f, 0.5f, 0.4f)),
                    ("MarketCrowd_Townsperson", "Townsperson", new Vector3(-2.8f, 1.8f, 0f), new Color(0.5f, 0.55f, 0.45f)),
                    ("MarketCrowd_Adventurer", "Adventurer_Young", new Vector3(-2.3f, -2.0f, 0f), new Color(0.55f, 0.45f, 0.35f)),
                };

                foreach (var (name, profileName, pos, tint) in crowdPlacements)
                {
                    var existingMember = FindRoot(scene, name);
                    if (existingMember == null)
                    {
                        HubSceneFactory.CreateInteractable(name, tint, pos, out NpcInteractable crowdNpc);
                        crowdNpc.promptText = "[E] Listen";
                        crowdNpc.dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(BarksPath);
                        crowdNpc.blocksMovement = true;
                        var visual = crowdNpc.GetComponent<EntityVisual>();
                        if (visual != null)
                        {
                            visual.profile = AssetDatabase.LoadAssetAtPath<VisualProfile>(
                                $"Assets/Art/Profiles/{profileName}.asset");
                            visual.Apply(visual.profile);
                        }
                    }
                    else
                    {
                        existingMember.transform.position = pos;
                    }
                }

                // Rest Spot: relocated to the wooden bench beside the NoticeBoard.
                var restSpot = FindRoot(scene, "RestSpot");
                if (restSpot != null)
                {
                    restSpot.transform.position = new Vector3(-2.45f, 2.3f, 0f);
                }

                // Ignatius: positioned by the east road with comfortable interaction collider.
                var ignatius = FindRoot(scene, "Ignatius");
                if (ignatius != null)
                {
                    ignatius.transform.position = new Vector3(4.0f, 1.5f, 0f);
                    var ignatiusNpc = ignatius.GetComponent<NpcInteractable>();
                    if (ignatiusNpc != null)
                    {
                        ignatiusNpc.blocksMovement = true;
                        ignatiusNpc.interrogationCaseId = 1;
                        ignatiusNpc.promptText = "[E] Talk";
                        ignatiusNpc.dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(
                            "Assets/Data/Dialogue/Case01/D_Ignatius_Claim_01.asset");
                    }
                    var col = ignatius.GetComponent<BoxCollider2D>();
                    if (col != null)
                    {
                        col.size = new Vector2(1.8f, 1.8f);
                        col.offset = new Vector2(-0.3f, -0.3f);
                    }
                }

                // Eliza stand-in for the arrival escort.
                GameObject eliza = FindRoot(scene, "ArrivalEliza");
                if (eliza == null)
                {
                    eliza = HubSceneFactory.NewQuadObject("ArrivalEliza",
                        new Color(0.9f, 0.55f, 0.35f), new Vector3(-9f, 0.2f, 0f));
                    HubSceneFactory.ApplyVisualProfile(eliza);
                    if (eliza.GetComponent<YSort2D>() == null)
                        eliza.AddComponent<YSort2D>();
                    eliza.SetActive(false);
                }

                // The arrival controller.
                var arrivalGo = FindRoot(scene, "MarketArrivalController");
                if (arrivalGo == null) arrivalGo = new GameObject("MarketArrivalController");
                var arrival = arrivalGo.GetComponent<MarketArrivalController>();
                if (arrival == null) arrival = arrivalGo.AddComponent<MarketArrivalController>();
                arrival.context = FindInScene<HubContext>(scene);
                var playerGo = GameObject.FindGameObjectWithTag("Player");
                arrival.player = playerGo != null
                    ? playerGo.GetComponent<HubPlayerController>() : null;
                arrival.eliza = eliza.transform;
                arrival.entryDialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(
                    GuildIntroContentGenerator.Folder + "/D_Market_Arrival.asset");
                arrival.doorDialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(
                    GuildIntroContentGenerator.Folder + "/D_Guild_Door.asset");

                // The notice board is a physical object placed prominently near the central plaza path crossing.
                foreach (var interactable in Object.FindObjectsByType<Interactable2D>(
                    FindObjectsInactive.Include))
                    if (interactable.gameObject.scene == scene &&
                        interactable.name == "NoticeBoard")
                    {
                        interactable.blocksMovement = true;
                        interactable.transform.position = new Vector3(1.5f, 0.8f, 0f);
                        if (interactable is InspectionInteractable inspection &&
                            inspection.inspectionDialogue == null)
                            inspection.inspectionDialogue = EnsureNoticeDialogue();
                    }

                // Camera framing: solid color background to prevent any skybox bleeding at edges,
                // and bounds clamped to the dressed environment.
                var cam = FindInScene<Camera>(scene);
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.08f, 0.10f, 0.08f, 1f);
                }
                var cameraFollow = FindInScene<CameraFollow2D>(scene);
                if (cameraFollow != null)
                {
                    cameraFollow.useBounds = true;
                    cameraFollow.boundsMin = new Vector2(-11f, -5.5f);
                    cameraFollow.boundsMax = new Vector2(11f, 6.5f);
                }

                // Belt-and-braces: dynamic Y-sort on every actor serialized
                // before the RequireComponent rule existed.
                foreach (var interactable in Object.FindObjectsByType<Interactable2D>(
                    FindObjectsInactive.Include))
                    if (interactable.GetComponent<YSort2D>() == null)
                        interactable.gameObject.AddComponent<YSort2D>();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("MarketExpansionInstaller: Lower Market expanded " +
                          "(GuildEntrance, townsfolk, arrival escort).");
            }
            finally
            {
                if (!wasOpen && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>Re-readable market notice text for the physical board.</summary>
        static DialogueData EnsureNoticeDialogue()
        {
            const string path = "Assets/Data/Dialogue/Case01/D_NoticeBoard_01.asset";
            var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (data != null) return data;
            data = ScriptableObject.CreateInstance<DialogueData>();
            data.id = "D_NoticeBoard_01";
            data.startNodeId = "n1";
            data.nodes = new[]
            {
                new DialogueNodeData
                {
                    id = "n1",
                    speakerName = "Notice Board",
                    text = "GUILD NOTICE — Vault goods must be sealed and " +
                           "registered before market days, by order of the " +
                           "Merchant Guild.",
                    nextNodeId = "n2",
                },
                new DialogueNodeData
                {
                    id = "n2",
                    speakerName = "Notice Board",
                    text = "WANTED — Steady hands for caravan escort to the " +
                           "Whisperwood road. Apply at the Guild counter.",
                    nextNodeId = "n3",
                },
                new DialogueNodeData
                {
                    id = "n3",
                    speakerName = "Notice Board",
                    text = "LOST — One copper kettle, dented, answers to no " +
                           "name. Return to the plaza stalls.",
                },
            };
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }
    }
}
