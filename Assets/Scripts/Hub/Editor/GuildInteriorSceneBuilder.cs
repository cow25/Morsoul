using System.Collections.Generic;
using Morsoul.Dialogue;
using Morsoul.Dialogue.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/Hub_Guild.unity — the enterable Lower Market Guild
    /// hall. Ground floor: front desk + clerk, notice board, adventurers,
    /// stairs east. Upstairs (offset ~24y): Ovan's office. The GuildIntro
    /// controller runs the one-shot escort sequence when the market arrival
    /// hands off; afterwards Ovan's ordinary NpcInteractable (D_Ovan_01)
    /// carries the mission — nothing is auto-granted.
    /// </summary>
    public static class GuildInteriorSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Hub_Guild.unity";
        const string GuildDialogueFolder = GuildIntroContentGenerator.Folder;
        const string OvanDialoguePath = "Assets/Data/Dialogue/Case01/D_Ovan_01.asset";

        /// <summary>
        /// One-step orchestration: dialogue assets -> Hub_Guild scene -> Lower
        /// Market expansion -> environment re-dress (fixed prop colliders and
        /// the corrected depth bias land in the saved scenes).
        /// </summary>
        [MenuItem("Morsoul/Hub/Build Guild Expansion (All)")]
        public static void BuildAll()
        {
            Build();
            MarketExpansionInstaller.Install();
            HubDressingBuilder.DressAll();
            Debug.Log("Guild expansion built: Hub_Guild scene, market door + " +
                      "arrival escort, re-dressed environments.");
        }

        [MenuItem("Morsoul/Hub/Create Guild Interior Scene (Hub_Guild)")]
        public static void Build()
        {
            // Dialogue assets are generated first so references resolve.
            GuildIntroContentGenerator.Generate();

            var scene = HubSceneFactory.NewEmptyScene();

            var player = HubSceneFactory.CreatePlayer();
            var cameraFollow = HubSceneFactory.CreateCamera(player.transform);
            // Bounds cover both floors: hall y[-4.5,5.5], office y[19.5,29.5].
            cameraFollow.boundsMin = new Vector2(-8f, -6f);
            cameraFollow.boundsMax = new Vector2(8f, 30f);
            HubSceneFactory.CreateSpawnPoint();
            var spawn = GameObject.FindGameObjectWithTag("PlayerSpawn");
            if (spawn != null) spawn.transform.position = new Vector3(0f, -3f, 0f);
            var context = HubSceneFactory.CreateHubContext();
            HubSceneFactory.WirePlayer(player, cameraFollow, context);

            // Interior dressing lives in the main scene — no art overlay.
            var environment = new GameObject("Environment");
            HubDressingBuilder.PlaceProps(
                environment.transform, HubDressingBuilder.GuildInterior());

            // Exit back to the market (always open).
            HubSceneFactory.CreateInteractable("GuildExit",
                new Color(0.5f, 0.35f, 0.2f), new Vector3(0f, -4f, 0f),
                out GateInteractable exit);
            exit.promptText = "[E] Leave";
            exit.requiredFlag = "";
            exit.targetScene = "Hub_LowerMarket";
            exit.entryId = "guild_door";

            // Stair pair between the two stacked floors.
            var landingUp = Marker("StairLanding_Up", new Vector3(6.5f, 20.4f, 0f));
            var landingDown = Marker("StairLanding_Down", new Vector3(6.5f, 2.5f, 0f));
            HubSceneFactory.CreateInteractable("StairsUp",
                new Color(0.5f, 0.4f, 0.25f), new Vector3(6.5f, 2.9f, 0f),
                out StairInteractable stairsUp);
            stairsUp.promptText = "[E] Upstairs";
            stairsUp.destination = landingUp;
            HubSceneFactory.CreateInteractable("StairsDown",
                new Color(0.5f, 0.4f, 0.25f), new Vector3(6.5f, 20.9f, 0f),
                out StairInteractable stairsDown);
            stairsDown.promptText = "[E] Downstairs";
            stairsDown.destination = landingDown;

            // Front desk: clerk behind the counter, notice board beside it.
            HubSceneFactory.CreateInteractable("GuildClerk",
                new Color(0.75f, 0.6f, 0.45f), new Vector3(0f, 3.2f, 0f),
                out NpcInteractable clerk);
            clerk.promptText = "[E] Talk";
            clerk.dialogue = LoadDialogue(GuildDialogueFolder + "/D_GuildClerk.asset");

            HubSceneFactory.CreateInteractable("GuildBoard",
                new Color(0.6f, 0.48f, 0.3f), new Vector3(-3f, 1.8f, 0f),
                out InspectionInteractable board);
            board.promptText = "[E] Inspect";

            // Adventurers — the guild floor feels inhabited.
            foreach (var (name, pos) in new (string, Vector3)[]
            {
                ("Adventurer_Scarred", new Vector3(3.2f, -2.2f, 0f)),
                ("Adventurer_Young", new Vector3(-4.2f, -2.6f, 0f)),
                ("Adventurer_Ranger", new Vector3(5.4f, 0.4f, 0f)),
            })
            {
                HubSceneFactory.CreateInteractable(name,
                    new Color(0.55f, 0.45f, 0.35f), pos, out NpcInteractable npc);
                npc.promptText = "[E] Talk";
                npc.dialogue = LoadDialogue(GuildDialogueFolder + "/D_GuildBarks.asset");
            }

            // Ovan — upstairs office, mission interaction unchanged.
            HubSceneFactory.CreateInteractable("Ovan",
                new Color(0.8f, 0.6f, 0.2f), new Vector3(0f, 26.4f, 0f),
                out NpcInteractable ovan);
            ovan.promptText = "[E] Talk";
            ovan.dialogue = LoadDialogue(OvanDialoguePath);

            // Eliza stand-in for the intro escort (activated by the controller).
            var eliza = HubSceneFactory.NewQuadObject("Eliza",
                new Color(0.9f, 0.55f, 0.35f), new Vector3(0.8f, -3f, 0f));
            HubSceneFactory.ApplyVisualProfile(eliza);
            if (eliza.GetComponent<YSort2D>() == null)
                eliza.AddComponent<YSort2D>();
            eliza.SetActive(false);

            var intro = new GameObject("GuildIntroController");
            var controller = intro.AddComponent<GuildIntroController>();
            controller.context = context;
            controller.player = player.GetComponent<HubPlayerController>();
            controller.eliza = eliza.transform;
            controller.receptionDialogue =
                LoadDialogue(GuildDialogueFolder + "/D_Guild_Reception.asset");
            controller.ovanDialogue =
                LoadDialogue(GuildDialogueFolder + "/D_Ovan_Meeting.asset");

            HubSceneFactory.SaveScene(scene, ScenePath);
            RegisterBuildScene(ScenePath);
        }

        static Transform Marker(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            return go.transform;
        }

        static DialogueData LoadDialogue(string path)
        {
            var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (data == null)
                Debug.LogWarning($"GuildInteriorSceneBuilder: DialogueData missing at '{path}'.");
            return data;
        }

        static void RegisterBuildScene(string scenePath)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list)
            {
                if (s.path == scenePath)
                {
                    s.enabled = true;
                    EditorBuildSettings.scenes = list.ToArray();
                    return;
                }
            }
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
