using System.Collections.Generic;
using Morsoul.Dialogue;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/Hub_CathedralCourtyard.unity — a compact exterior
    /// stone approach (matching Prologue_Wilderness & Lower Market PixelCrawler foliage)
    /// where the Carrier drops the player off on the West path and walking into the
    /// stony Cathedral doorway on the East triggers entry into Hub_CathedralNave.
    /// </summary>
    public static class CathedralCourtyardSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Hub_CathedralCourtyard.unity";
        const string PanelSettingsPath = "Assets/UI Toolkit/MainPanelSettings.asset";
        const string TravelMenuUxmlPath = "Assets/TravelMenu.uxml";
        const string HubHudUxmlPath = "Assets/HubHud.uxml";
        const string BarksPath = "Assets/Data/Dialogue/Case02/D_NaveBarks_02.asset";

        const string TreeM1Path = "Assets/Art/Environments/Props/Trees/Model_01/Size_03.png";
        const string TreeM2Path = "Assets/Art/Environments/Props/Trees/Model_02/Size_03.png";
        const string TreeM3Path = "Assets/Art/Environments/Props/Trees/Model_03/Size_03.png";
        const string VegPath = "Assets/Art/Environments/Props/Static/Vegetation.png";
        const string RocksPath = "Assets/Art/Environments/Props/Static/Rocks.png";
        const string DungeonPropsPath = "Assets/Art/Environments/Props/Static/Dungeon_Props.png";
        const string ExtPropsPath = "Assets/Art/Environments/Buildings/Exterior/Props.png";
        const string ResourcesPath = "Assets/Art/Environments/Props/Static/Resources.png";

        [MenuItem("Morsoul/Scenes/Cathedral Courtyard", priority = 15)]
        public static void BuildMenu() => Build();

        public static void Build()
        {
            PixelCrawlerHubToolkit.ClearCache();

            var scene = HubSceneFactory.NewEmptyScene();
            RenderSettings.skybox = null;
            RenderSettings.ambientLight = Color.white;

            var player = HubSceneFactory.CreatePlayer();
            player.transform.position = new Vector3(-6.2f, -0.3f, 0f);

            var cameraFollow = HubSceneFactory.CreateCamera(player.transform);
            cameraFollow.boundsMin = new Vector2(-10.5f, -4.5f);
            cameraFollow.boundsMax = new Vector2(12.0f, 4.8f);
            cameraFollow.useBounds = true;
            var cam = cameraFollow.GetComponent<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.11f, 0.08f, 1f);
                cam.orthographicSize = 5.0f;
            }

            HubSceneFactory.CreateSpawnPoint();
            var spawn = GameObject.FindGameObjectWithTag("PlayerSpawn");
            if (spawn != null) spawn.transform.position = new Vector3(-6.2f, -0.3f, 0f);

            // Spawn marker for exiting Hub_CathedralNave back outside into the courtyard (west of trigger tile).
            var doorSpawn = new GameObject("Spawn_CourtyardDoor");
            doorSpawn.transform.position = new Vector3(7.0f, -0.3f, 0f);
            doorSpawn.AddComponent<SceneSpawnMarker>().entryId = "courtyard_door";

            var context = HubSceneFactory.CreateHubContext();
            WireUiPanels(scene, context);
            HubSceneFactory.WirePlayer(player, cameraFollow, context);

            BuildTerrain(scene);
            BuildStoneCathedralApproach(scene);
            BuildFoliageAndProps(scene);

            // Compact corridor bounds matching Prologue_Wilderness (West drop-off x=-9 -> East cathedral doorway x=9.5).
            PixelCrawlerHubToolkit.RebuildBounds(scene, "CourtyardBounds",
                ("Bound_North", new Vector2(0f, 2.7f), new Vector2(24f, 1.4f)),
                ("Bound_South", new Vector2(0f, -3.0f), new Vector2(24f, 1.4f)),
                ("Bound_West", new Vector2(-9.6f, 0f), new Vector2(1.4f, 8f)),
                ("Bound_East", new Vector2(10.6f, 0f), new Vector2(1.4f, 8f)));

            // Walk-in Trigger Tile at the East Cathedral wall -> Hub_CathedralNave (Spawn_CathedralDoor).
            var triggerGo = new GameObject("CathedralEntranceTrigger");
            triggerGo.transform.position = new Vector3(8.9f, -0.3f, 0f);
            var triggerCol = triggerGo.AddComponent<BoxCollider2D>();
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector2(1.4f, 2.6f);
            var exitTrigger = triggerGo.AddComponent<SceneExitTrigger>();
            exitTrigger.targetScene = "Hub_CathedralNave";
            exitTrigger.entryId = "cathedral_door";

            // Carrier Drop-off Node on the West arrival road.
            HubSceneFactory.CreateInteractable("Carrier",
                new Color(0.3f, 0.6f, 0.4f), new Vector3(-7.8f, 0.1f, 0f),
                out CarrierInteractable carrier);
            carrier.promptText = "[E] Travel";
            carrier.targetScene = "Hub_LowerMarket";
            carrier.offerTravelMenu = true;

            // Wooden Rest Bench along the northern churchyard edge (matching Prologue_Wilderness bench).
            HubSceneFactory.CreateInteractable("RestSpot",
                new Color(0.45f, 0.6f, 0.7f), new Vector3(-1.2f, 1.75f, 0f),
                out RestSpot rest);
            rest.promptText = "[E] Rest";
            var benchSprite = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_14");
            if (benchSprite != null)
            {
                var restProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Courtyard_RestBench", ExtPropsPath, "Ext_Props_14", null, Vector2.zero);
                PixelCrawlerHubToolkit.BindActorVisual(rest.gameObject, restProf, addYSort: true);
            }

            // Two ambient churchyard NPCs near the stone steps.
            var barkDialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(BarksPath);
            var npcSpecs = new (string name, Vector3 pos, string sheet, string sprite, string ctrl, bool flip)[]
            {
                ("Courtyard_Guard", new Vector3(4.6f, 1.2f, 0f),
                    "Assets/Art/Characters/NPCs/Citizen_M/Guard_A/Idle/Idle_Side-Sheet.png",
                    "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_GuardA.controller", true),
                ("Courtyard_Novice", new Vector3(3.0f, -0.6f, 0f),
                    "Assets/Art/Characters/NPCs/Citizen_F/Peasant_A/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_PeasantA.controller", false),
            };
            var npcs = new Dictionary<string, (GameObject go, VisualProfile prof)>();
            foreach (var spec in npcSpecs)
            {
                HubSceneFactory.CreateInteractable(spec.name,
                    new Color(0.55f, 0.55f, 0.65f), spec.pos, out NpcInteractable npc);
                npc.promptText = "[E] Talk";
                npc.dialogue = barkDialogue;
                var prof = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    spec.name, spec.sheet, spec.sprite, spec.ctrl, new Vector2(0f, -0.35f), spec.flip);
                npcs[spec.name] = (npc.gameObject, prof);
            }

            // Bind Player & Carrier visuals.
            var playerProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Player", "Assets/Art/Characters/Player/Golem/Idle/Idle_Down-Sheet.png",
                "Idle_Down-Sheet_0", "Assets/Animations/Player/Player_Golem.controller",
                new Vector2(0f, -0.35f));
            var carrierProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Carrier", "Assets/Art/Characters/NPCs/Named/Carrier/Idle/Idle_Down-Sheet.png",
                "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Carrier.controller",
                new Vector2(0f, -0.35f));

            PixelCrawlerHubToolkit.BindActorVisual(player, playerProf, addYSort: true);
            PixelCrawlerHubToolkit.BindActorVisual(carrier.gameObject, carrierProf, addYSort: true);
            foreach (var kv in npcs)
                PixelCrawlerHubToolkit.BindActorVisual(kv.Value.go, kv.Value.prof, addYSort: true);

            HubSceneFactory.SaveScene(scene, ScenePath);
            RegisterBuildScene(ScenePath);
            Debug.Log("CathedralCourtyardSceneBuilder: built compact Assets/Scenes/Hub_CathedralCourtyard.unity.");
        }

        static void WireUiPanels(Scene scene, HubContext context)
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var travelUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TravelMenuUxmlPath);
            if (settings != null && travelUxml != null)
            {
                var go = new GameObject("TravelMenuPanel");
                var doc = go.AddComponent<UIDocument>();
                doc.panelSettings = settings;
                doc.visualTreeAsset = travelUxml;
                doc.sortingOrder = 20;
                context.travelMenu = go.AddComponent<TravelMenuController>();
            }

            var hudUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HubHudUxmlPath);
            if (settings != null && hudUxml != null)
            {
                var go = new GameObject("HubHudPanel");
                var doc = go.AddComponent<UIDocument>();
                doc.panelSettings = settings;
                doc.visualTreeAsset = hudUxml;
                doc.sortingOrder = 10;
                var hud = go.AddComponent<HubHudController>();
                hud.caseText = "";
                hud.objectiveText = "";
                hud.objectiveCaseId = 0;
                hud.postInvestigationObjective = "";
                context.hud = hud;
            }
        }

        static void BuildTerrain(Scene scene)
        {
            var grid = PixelCrawlerHubToolkit.EnsureGrid(scene, "EnvironmentGrid");
            var grassTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Grass");
            var dirtTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Dirt");
            var cobbleTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Cobble");

            var grassMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "GroundGrass", -10, 2.0f);
            var dirtMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "GroundDirt", -9, 1.99f);
            var cobbleMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "GroundCobble", -8, 1.98f);

            PixelCrawlerHubToolkit.FillRect(grassMap, grassTile, -20, -10, 22, 10);

            // West Carrier dirt arrival road (-16..-4, y=-2..1).
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -16, -2, -4, 1);

            // Stony cobblestone cathedral forecourt leading straight East into the cathedral doorway (-4..9, y=-2..1).
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -4, -2, 9, 1);
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, 4, -3, 8, 2);

            grassMap.RefreshAllTiles();
            dirtMap.RefreshAllTiles();
            cobbleMap.RefreshAllTiles();
        }

        static void BuildStoneCathedralApproach(Scene scene)
        {
            var grid = PixelCrawlerHubToolkit.FindRoot(scene, "EnvironmentGrid").transform;
            var wallsMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid, "CathedralStoneWalls", -4, 1.5f);
            var propsRoot = PixelCrawlerHubToolkit.EnsurePropsRoot(scene, "EnvironmentProps").transform;

            // North, South (bottom), and East stone cathedral walls framing the hidden East trigger alcove.
            PaintStoneBand(wallsMap, xMin: 4, xMax: 11, yBottom: 2, rowsHigh: 3);
            PaintStoneBand(wallsMap, xMin: 6, xMax: 11, yBottom: -4, rowsHigh: 3);
            PaintStoneBand(wallsMap, xMin: 9, xMax: 11, yBottom: -1, rowsHigh: 3);
            wallsMap.RefreshAllTiles();
            PixelCrawlerHubToolkit.PlaceSoftGlow(propsRoot, "Cathedral_Portal_Glow",
                new Vector2(8.8f, -0.3f), new Color(1.0f, 0.85f, 0.55f, 0.28f), scale: 3.6f);

            var carrierWagon = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/Carrier_Wagon.png", 16f);
            if (carrierWagon != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Carrier_Wagon", carrierWagon,
                    new Vector2(-8.6f, 0.7f), colliderSize: new Vector2(4.2f, 1.4f), colliderOffset: new Vector2(0f, 0.7f));
                PixelCrawlerHubToolkit.PlaceSoftGlow(propsRoot, "Carrier_Lantern_Glow",
                    new Vector2(-7.0f, 1.4f), new Color(1.0f, 0.78f, 0.4f, 0.25f), scale: 2.6f);
            }

            var bannerBlue = PixelCrawlerHubToolkit.LoadSprite(DungeonPropsPath, "Dungeon_Props_15");
            var bannerRed = PixelCrawlerHubToolkit.LoadSprite(DungeonPropsPath, "Dungeon_Props_14");
            if (bannerBlue != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Cathedral_Banner_N", bannerBlue,
                    new Vector2(6.0f, 2.8f), zBias: 0.2f, sortingOrder: -2);
            if (bannerRed != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Cathedral_Banner_E1", bannerRed,
                    new Vector2(9.8f, 1.5f), zBias: 0.2f, sortingOrder: -2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Cathedral_Banner_E2", bannerRed,
                    new Vector2(9.8f, -1.6f), zBias: 0.2f, sortingOrder: -2);
            }
        }

        static void BuildFoliageAndProps(Scene scene)
        {
            var root = PixelCrawlerHubToolkit.FindRoot(scene, "EnvironmentProps").transform;

            var oakGreen = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_00");
            var oakGold = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_01");
            var pineA = PixelCrawlerHubToolkit.LoadSprite(TreeM2Path, "Tree_Model_02_Size_03_00");
            var pineB = PixelCrawlerHubToolkit.LoadSprite(TreeM2Path, "Tree_Model_02_Size_03_01");
            var tallPine = PixelCrawlerHubToolkit.LoadSprite(TreeM3Path, "Tree_Model_03_Size_03_00");

            var bushMed = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_04");
            var bushGold = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_05");
            var bushSmall = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_12");
            var fern = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_26");
            var flowerA = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_34");
            var flowerB = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_35");
            var flowerCluster = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_147");

            var greyBoulder = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_05");
            var smallRock = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_03");
            var supplyCrate = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_34");
            var crateAlt = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_35");
            var stonePile = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_02");

            // Carrier Drop-off Supplies on the West road (-9..-8).
            if (supplyCrate != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Carrier_Crate_A", supplyCrate,
                    new Vector2(-8.9f, 0.6f), colliderSize: new Vector2(0.85f, 0.55f), colliderOffset: new Vector2(0f, 0.25f));
            if (crateAlt != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Carrier_Crate_B", crateAlt,
                    new Vector2(-8.7f, -0.7f), colliderSize: new Vector2(0.85f, 0.55f), colliderOffset: new Vector2(0f, 0.25f));
            if (stonePile != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Forecourt_StonePile", stonePile, new Vector2(4.2f, -2.6f));

            // Grey stone boulders along the stony cathedral approach.
            if (greyBoulder != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stone_Boulder_N", greyBoulder,
                    new Vector2(2.6f, 2.2f), colliderSize: new Vector2(0.85f, 0.5f), colliderOffset: new Vector2(0f, 0.25f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stone_Boulder_S", greyBoulder,
                    new Vector2(5.4f, -2.55f), colliderSize: new Vector2(0.85f, 0.5f), colliderOffset: new Vector2(0f, 0.25f));
            }
            if (smallRock != null)
            {
                foreach (var p in new[] { new Vector2(-3.8f, 1.8f), new Vector2(1.4f, -2.3f), new Vector2(-0.8f, -2.3f) })
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"SmallRock_{p.x:F1}_{p.y:F1}", smallRock, p);
            }

            // Perimeter trees matching Prologue_Wilderness & Lower Market (PixelCrawler Model_01/02/03).
            Sprite[] trees = { pineA, oakGreen, pineB, oakGold, tallPine };
            var treeCoords = new[]
            {
                new Vector2(-10.2f, 2.6f), new Vector2(-7.0f, 2.9f), new Vector2(-3.6f, 2.8f), new Vector2(0.8f, 2.9f),
                new Vector2(-10.0f, -6.2f), new Vector2(-6.6f, -6.3f), new Vector2(-2.8f, -6.2f),
                new Vector2(1.0f, -6.3f), new Vector2(4.8f, -6.2f), new Vector2(13.5f, -6.4f),
            };
            for (int i = 0; i < treeCoords.Length; i++)
            {
                var sp = trees[i % trees.Length];
                if (sp != null)
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Tree_{i:D2}", sp, treeCoords[i],
                        colliderSize: new Vector2(1.1f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
            }

            // PixelCrawler bushes, ferns & flowers framing the path edges.
            Sprite[] bushes = { bushMed, bushSmall, bushGold };
            var bushCoords = new[]
            {
                new Vector2(-5.2f, 2.2f), new Vector2(-2.2f, 2.3f), new Vector2(1.8f, 2.3f),
                new Vector2(-5.4f, -2.9f), new Vector2(-1.8f, -3.0f), new Vector2(2.4f, -3.1f),
            };
            for (int i = 0; i < bushCoords.Length; i++)
            {
                var sp = bushes[i % bushes.Length];
                if (sp != null)
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Bush_{i:D2}", sp, bushCoords[i],
                        colliderSize: new Vector2(0.8f, 0.45f), colliderOffset: new Vector2(0f, 0.22f));
            }

            Sprite[] accents = { fern, flowerA, flowerB, flowerCluster };
            var accentCoords = new[]
            {
                new Vector2(-6.5f, 1.8f), new Vector2(-3.1f, -2.4f), new Vector2(-3.2f, 2.0f),
                new Vector2(0.4f, -2.5f), new Vector2(3.8f, 1.9f), new Vector2(3.2f, -2.3f),
            };
            for (int i = 0; i < accentCoords.Length; i++)
            {
                var sp = accents[i % accents.Length];
                if (sp != null)
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Foliage_{i:D2}", sp, accentCoords[i]);
            }
        }

        static void PaintStoneBand(Tilemap wallsMap, int xMin, int xMax, int yBottom, int rowsHigh)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                for (int dy = 0; dy < rowsHigh; dy++)
                {
                    int r = dy == rowsHigh - 1 ? 0 : (dy % 2 == 0 ? 2 : 1);
                    int c = 1 + (Mathf.Abs(x) % 2);
                    var tile = PixelCrawlerHubToolkit.LoadStaticTile("Dungeon_Tiles", $"Dungeon_Tiles_{r}_{c}");
                    if (tile != null)
                        wallsMap.SetTile(new Vector3Int(x, yBottom + dy, 0), tile);
                }
            }
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
