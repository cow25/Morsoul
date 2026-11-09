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

        [MenuItem("Morsoul/Hub/Expand Lower Market (Guild Entrance + Arrival)")]
        public static void Install()
        {
            GuildIntroContentGenerator.Generate();

            var scene = EditorSceneManager.OpenScene(MarketPath, OpenSceneMode.Single);
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
                marker.transform.position = new Vector3(3f, 2.3f, 0f);

                // Ambient townsfolk so the market reads inhabited.
                foreach (var (name, pos) in new (string, Vector3)[]
                {
                    ("Vendor", new Vector3(-4.5f, -1.8f, 0f)),
                    ("Townsperson", new Vector3(-5.6f, 1.2f, 0f)),
                })
                {
                    if (FindRoot(scene, name) != null) continue;
                    HubSceneFactory.CreateInteractable(name,
                        new Color(0.6f, 0.5f, 0.4f), pos, out NpcInteractable npc);
                    npc.promptText = "[E] Talk";
                    npc.dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(BarksPath);
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
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
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
