using System.Collections.Generic;
using Morsoul.Dialogue;
using Morsoul.Dialogue.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/Hub_Guild.unity — the enterable Lower Market Guild
    /// hall using Pixel Crawler interior tiles, furniture, and animated NPCs.
    /// Ground floor (y[-4.5,5.5]): front desk + clerk, notice board, tavern
    /// seating, stairs east. Upstairs (y[19.5,29.5]): Ovan's hardwood office.
    /// </summary>
    public static class GuildInteriorSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Hub_Guild.unity";
        const string GuildDialogueFolder = GuildIntroContentGenerator.Folder;
        const string OvanDialoguePath = "Assets/Data/Dialogue/Case01/D_Ovan_01.asset";
        const string IntPropsPath = "Assets/Art/Environments/Buildings/Interior/Interior_Props_01.png";
        const string FurniturePath = "Assets/Art/Environments/Props/Static/Furniture.png";
        const string DungeonPropsPath = "Assets/Art/Environments/Props/Static/Dungeon_Props.png";
        const string IntWallsSheet = "Interior_Walls_01";

        /// <summary>
        /// One-step orchestration: dialogue assets -> Hub_Guild scene -> Lower
        /// Market expansion -> Pixel Crawler Lower Market rebuild.
        /// </summary>
        public static void BuildAll()
        {
            HubContentSetup.Install();
            DemoStandInFactory.PatchRouteWiring();
            Build();
            MarketExpansionInstaller.Install();
            LowerMarketSceneBuilder.Rebuild();
            Debug.Log("Guild expansion built: hub content restored, Hub_Guild " +
                      "(Pixel Crawler), market door + arrival escort, Lower Market rebuilt.");
        }

        [MenuItem("Morsoul/Scenes/Guild Interior", priority = 14)]
        public static void Build()
        {
            GuildIntroContentGenerator.Generate();
            PixelCrawlerHubToolkit.ClearCache();

            var scene = HubSceneFactory.NewEmptyScene();

            var player = HubSceneFactory.CreatePlayer();
            var cameraFollow = HubSceneFactory.CreateCamera(player.transform);
            cameraFollow.boundsMin = new Vector2(-8f, -6f);
            cameraFollow.boundsMax = new Vector2(8f, 30.5f);
            cameraFollow.useBounds = true;
            var cam = cameraFollow.GetComponent<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.05f, 0.07f, 1f);
                cam.orthographicSize = 5.2f;
            }
            HubSceneFactory.CreateSpawnPoint();
            var spawn = GameObject.FindGameObjectWithTag("PlayerSpawn");
            if (spawn != null) spawn.transform.position = new Vector3(0f, -3f, 0f);
            var context = HubSceneFactory.CreateHubContext();
            if (context.hud != null)
            {
                context.hud.caseText = "";
                context.hud.objectiveText = "Follow Eliza to meet Guildmaster Ovan.";
            }
            HubSceneFactory.WirePlayer(player, cameraFollow, context);

            // Pixel Crawler floors, walls, staircases & furnishings (Tilemaps sit at z >= 1.97, behind all 1F & 2F actors).
            BuildGuildTilemaps(scene);
            BuildGuildFurnishings(scene);
            BuildGuildBounds(scene);

            // Exit back to the market (always open).
            HubSceneFactory.CreateInteractable("GuildExit",
                new Color(0.5f, 0.35f, 0.2f), new Vector3(0f, -4.1f, 0f),
                out GateInteractable exit);
            exit.promptText = "[E] Leave";
            exit.requiredFlag = "";
            exit.targetScene = "Hub_LowerMarket";
            exit.entryId = "guild_door";
            var exitProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Guild_ExitDoor", IntPropsPath, "Int_Interior_Props_01_43", null, Vector2.zero);
            PixelCrawlerHubToolkit.BindActorVisual(exit.gameObject, exitProf, addYSort: true);

            // Stair pair between the two stacked floors (aligned cleanly over staircase tiles).
            var landingUp = Marker("StairLanding_Up", new Vector3(5.2f, 21.2f, 0f));
            var landingDown = Marker("StairLanding_Down", new Vector3(5.2f, 2.2f, 0f));
            HubSceneFactory.CreateInteractable("StairsUp",
                new Color(0.5f, 0.4f, 0.25f), new Vector3(6.2f, 2.8f, 0f),
                out StairInteractable stairsUp);
            stairsUp.promptText = "[E] Upstairs";
            stairsUp.destination = landingUp;
            HideInteractableQuad(stairsUp.gameObject);

            HubSceneFactory.CreateInteractable("StairsDown",
                new Color(0.5f, 0.4f, 0.25f), new Vector3(6.2f, 21.2f, 0f),
                out StairInteractable stairsDown);
            stairsDown.promptText = "[E] Downstairs";
            stairsDown.destination = landingDown;
            HideInteractableQuad(stairsDown.gameObject);

            // Front desk: clerk directly behind the reception counter (y=2.35), generous front trigger (y=0.0..2.4).
            HubSceneFactory.CreateInteractable("GuildClerk",
                new Color(0.75f, 0.6f, 0.45f), new Vector3(0f, 2.35f, 0f),
                out NpcInteractable clerk);
            clerk.promptText = "[E] Talk";
            clerk.dialogue = LoadDialogue(GuildDialogueFolder + "/D_GuildClerk.asset");
            var clerkCol = clerk.GetComponent<BoxCollider2D>();
            if (clerkCol != null)
            {
                clerkCol.offset = new Vector2(0f, -1.15f);
                clerkCol.size = new Vector2(4.4f, 2.5f);
            }

            // Notice board to the left of the reception counter.
            HubSceneFactory.CreateInteractable("GuildBoard",
                new Color(0.6f, 0.48f, 0.3f), new Vector3(-4.0f, 2.3f, 0f),
                out InspectionInteractable board);
            board.promptText = "[E] Inspect";
            board.blocksMovement = true;
            var boardProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Guild_Board", IntPropsPath, "Int_Interior_Props_01_77", null, Vector2.zero);
            PixelCrawlerHubToolkit.BindActorVisual(board.gameObject, boardProf, addYSort: true);

            // Adventurers — tavern seating & hall (clear of tables and stairs).
            var adventurerSpecs = new (string name, Vector3 pos, bool flip)[]
            {
                ("Adventurer_Young", new Vector3(-5.2f, -2.3f, 0f), false),
                ("Adventurer_Scarred", new Vector3(3.3f, -2.1f, 0f), true),
                ("Adventurer_Ranger", new Vector3(3.2f, 0.7f, 0f), true),
            };
            var advNpcs = new Dictionary<string, GameObject>();
            foreach (var spec in adventurerSpecs)
            {
                HubSceneFactory.CreateInteractable(spec.name,
                    new Color(0.55f, 0.45f, 0.35f), spec.pos, out NpcInteractable npc);
                npc.promptText = "[E] Talk";
                npc.dialogue = LoadDialogue(GuildDialogueFolder + "/D_GuildBarks.asset");
                advNpcs[spec.name] = npc.gameObject;
            }

            // Ovan — upstairs office directly behind his desk (y=25.55), front trigger reaches across desk (y=23.2..25.6).
            HubSceneFactory.CreateInteractable("Ovan",
                new Color(0.8f, 0.6f, 0.2f), new Vector3(0f, 25.55f, 0f),
                out NpcInteractable ovan);
            ovan.promptText = "[E] Talk";
            ovan.dialogue = LoadDialogue(OvanDialoguePath);
            var ovanCol = ovan.GetComponent<BoxCollider2D>();
            if (ovanCol != null)
            {
                ovanCol.offset = new Vector2(0f, -1.15f);
                ovanCol.size = new Vector2(4.2f, 2.6f);
            }

            // Eliza stand-in for the intro escort (activated by the controller).
            var eliza = HubSceneFactory.NewQuadObject("Eliza",
                new Color(0.9f, 0.55f, 0.35f), new Vector3(0.8f, -3f, 0f));
            if (eliza.GetComponent<YSort2D>() == null)
                eliza.AddComponent<YSort2D>();
            eliza.SetActive(false);

            // Bind Pixel Crawler animated character visuals to Player + NPCs.
            BindCharacterVisuals(player, eliza, clerk.gameObject, ovan.gameObject, advNpcs);

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

        static void BuildGuildTilemaps(Scene scene)
        {
            var grid = PixelCrawlerHubToolkit.EnsureGrid(scene, "EnvironmentGrid");
            var floorMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "Floor", -10, 2.0f);
            var accentMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "FloorAccent", -9, 1.99f);
            var stairsMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "Stairs", -8, 1.98f);
            var wallsMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "Walls", -6, 1.95f);

            // 1F Floor: warm oak wood planks (x=-8..7, y=-5..4).
            FillRandomTileBlock(floorMap, -8, -5, 7, 4, rowMin: 37, rowMax: 39, colMin: 1, colMax: 3);
            // Central stone runner leading from entrance (-1..1, y=-4..1) to Reception Desk.
            FillRandomTileBlock(accentMap, -1, -4, 1, 1, rowMin: 37, rowMax: 39, colMin: 6, colMax: 8);

            // 2F Floor: herringbone hardwood (x=-8..7, y=19..28).
            FillRandomTileBlock(floorMap, -8, 19, 7, 28, rowMin: 37, rowMax: 39, colMin: 11, colMax: 13);
            // Upper center study inset (tan parquet runner x=-2..2, y=21..25).
            FillRandomTileBlock(accentMap, -2, 21, 2, 25, rowMin: 37, rowMax: 39, colMin: 16, colMax: 18);

            // Interior Perimeter Walls (rows 5..7 are seamless interior walls without top black corner gaps):
            // 1F North Wall (y=4..6, Cream Plaster + Timber rows 5..7, cols 31..34).
            PaintInteriorWallBand(wallsMap, xStart: -8, width: 16, yBottom: 4, colOffset: 31);
            // 2F North Wall (y=27..29, Dark Timber Planks rows 5..7, cols 25..28).
            PaintInteriorWallBand(wallsMap, xStart: -8, width: 16, yBottom: 27, colOffset: 25);

            // Wooden staircases at East end (x=5..7): 1F going up (y=1..3, below north wall) and 2F landing (y=20..22).
            StampStaircase(stairsMap, xStart: 5, yBottom: 1);
            StampStaircase(stairsMap, xStart: 5, yBottom: 20);

            floorMap.RefreshAllTiles();
            accentMap.RefreshAllTiles();
            stairsMap.RefreshAllTiles();
            wallsMap.RefreshAllTiles();
        }

        static void FillRandomTileBlock(Tilemap map, int xMin, int yMin, int xMax, int yMax,
            int rowMin, int rowMax, int colMin, int colMax)
        {
            var pool = new List<TileBase>();
            for (int r = rowMin; r <= rowMax; r++)
                for (int c = colMin; c <= colMax; c++)
                {
                    var t = PixelCrawlerHubToolkit.LoadStaticTile(IntWallsSheet, $"{IntWallsSheet}_{r}_{c}");
                    if (t != null) pool.Add(t);
                }
            if (pool.Count == 0) return;

            for (int y = yMin; y <= yMax; y++)
                for (int x = xMin; x <= xMax; x++)
                {
                    int idx = Mathf.Abs((x * 73856093) ^ (y * 19349663)) % pool.Count;
                    map.SetTile(new Vector3Int(x, y, 0), pool[idx]);
                }
        }

        static void PaintInteriorWallBand(Tilemap wallsMap, int xStart, int width, int yBottom, int colOffset)
        {
            // Rows 5..7 in Interior_Walls_01 hold seamless beam -> wall -> baseboard tiles (avoiding corner cutouts).
            for (int dx = 0; dx < width; dx++)
            {
                int col = colOffset + (dx % 3);
                for (int dy = 0; dy < 3; dy++)
                {
                    int sheetRow = 7 - dy;
                    var t = PixelCrawlerHubToolkit.LoadStaticTile(IntWallsSheet, $"{IntWallsSheet}_{sheetRow}_{col}");
                    if (t != null)
                        wallsMap.SetTile(new Vector3Int(xStart + dx, yBottom + dy, 0), t);
                }
            }
        }

        static void StampStaircase(Tilemap map, int xStart, int yBottom)
        {
            // Rows 37..39, cols 23..25 give a 3-tile-wide wooden staircase.
            for (int dx = 0; dx < 3; dx++)
                for (int dy = 0; dy < 3; dy++)
                {
                    int sheetRow = 39 - dy;
                    int sheetCol = 23 + dx;
                    var t = PixelCrawlerHubToolkit.LoadStaticTile(IntWallsSheet, $"{IntWallsSheet}_{sheetRow}_{sheetCol}");
                    if (t != null)
                        map.SetTile(new Vector3Int(xStart + dx, yBottom + dy, 0), t);
                }
        }

        static void BuildGuildFurnishings(Scene scene)
        {
            var root = PixelCrawlerHubToolkit.EnsurePropsRoot(scene, "EnvironmentProps").transform;

            var woodDesk = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_00");
            var roundTable = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_02");
            var chair = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_13");
            var oakBench = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_99");
            var wallShelf = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_32");
            var chestWood = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_19");
            var plantFlower = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_123");
            var plantGreen = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_124");
            var crateCluster = PixelCrawlerHubToolkit.LoadSprite(FurniturePath, "Furniture_13");
            var bannerRed = PixelCrawlerHubToolkit.LoadSprite(DungeonPropsPath, "Dungeon_Props_14");
            var bannerBlue = PixelCrawlerHubToolkit.LoadSprite(DungeonPropsPath, "Dungeon_Props_15");
            var bannerGreen = PixelCrawlerHubToolkit.LoadSprite(DungeonPropsPath, "Dungeon_Props_16");

            // --- 1F Ground Hall ---
            // Reception counter at y=1.35 (sleek oak desk; Clerk stands at y=2.35 right behind it).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_ReceptionCounter", woodDesk,
                new Vector2(0f, 1.35f), colliderSize: new Vector2(4.0f, 0.85f), colliderOffset: new Vector2(0f, 0.5f));

            // Tavern seating on West & East sides (clear of NPCs, central hall & stairs).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Table_W", roundTable,
                new Vector2(-5.2f, -0.55f), colliderSize: new Vector2(1.8f, 1.0f), colliderOffset: new Vector2(0f, 0.55f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Stool_W1", chair, new Vector2(-6.5f, -0.35f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Bench_W", oakBench,
                new Vector2(-5.4f, 0.9f), colliderSize: new Vector2(2.6f, 0.6f), colliderOffset: new Vector2(0f, 0.35f));

            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Table_E", roundTable,
                new Vector2(4.8f, -0.65f), colliderSize: new Vector2(1.8f, 1.0f), colliderOffset: new Vector2(0f, 0.55f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Stool_E1", chair, new Vector2(6.1f, -0.45f));

            // North wall shelves, Guild banners (clear of shelves), crates, chest & plants.
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_WallShelf_W", wallShelf,
                new Vector2(-5.6f, 4.15f), zBias: 0f, sortingOrder: -2);
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_WallShelf_E", wallShelf,
                new Vector2(5.4f, 4.15f), zBias: 0f, sortingOrder: -2);
            if (bannerGreen != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Banner_W", bannerGreen, new Vector2(-1.6f, 4.7f), zBias: 0f, sortingOrder: -2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Banner_E", bannerGreen, new Vector2(1.6f, 4.7f), zBias: 0f, sortingOrder: -2);
            }
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Plant_NW", plantFlower, new Vector2(-7.0f, 3.2f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Crates_SW", crateCluster,
                new Vector2(-6.0f, -3.7f), colliderSize: new Vector2(2.4f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Chest_SE", chestWood,
                new Vector2(6.2f, -3.7f), colliderSize: new Vector2(1.5f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));

            // --- 2F Ovan's Office ---
            // Executive mahogany desk at y=24.42 (Ovan stands at y=25.55 cleanly head-and-shoulders visible behind it).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_OvanDesk", woodDesk,
                new Vector2(0f, 24.42f), colliderSize: new Vector2(3.8f, 0.85f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_VisitorChair_W", chair, new Vector2(-1.1f, 23.4f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_VisitorChair_E", chair, new Vector2(1.1f, 23.4f));

            // Office study tables, archive chests, wall shelves, banners & potted plants (clear of shelves).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_WallShelf_W", wallShelf,
                new Vector2(-5.6f, 27.15f), zBias: 0f, sortingOrder: -2);
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_WallShelf_E", wallShelf,
                new Vector2(5.6f, 27.15f), zBias: 0f, sortingOrder: -2);
            if (bannerBlue != null && bannerRed != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Banner_W", bannerBlue, new Vector2(-1.8f, 27.6f), zBias: 0f, sortingOrder: -2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Banner_E", bannerRed, new Vector2(1.8f, 27.6f), zBias: 0f, sortingOrder: -2);
            }
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Chest_NW", chestWood,
                new Vector2(-6.3f, 25.5f), colliderSize: new Vector2(1.4f, 0.85f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Chest_W", chestWood,
                new Vector2(-6.3f, 20.5f), colliderSize: new Vector2(1.4f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Table_W", roundTable,
                new Vector2(-5.2f, 22.8f), colliderSize: new Vector2(1.8f, 1.0f), colliderOffset: new Vector2(0f, 0.55f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Bench_S", oakBench,
                new Vector2(0f, 20.0f), colliderSize: new Vector2(2.6f, 0.6f), colliderOffset: new Vector2(0f, 0.35f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Plant_NW", plantGreen ?? plantFlower, new Vector2(-7.1f, 26.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Plant_NE", plantFlower, new Vector2(7.0f, 26.5f));
        }

        static void BuildGuildBounds(Scene scene)
        {
            var old = PixelCrawlerHubToolkit.FindRoot(scene, "GuildBounds");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("GuildBounds");
            SceneManager.MoveGameObjectToScene(root, scene);

            // 1F Room Boundaries: x[-7.5, 7.5], y[-4.5, 4.0].
            AddBox(root, "F1_North", new Vector2(0f, 4.6f), new Vector2(17f, 1.4f));
            AddBox(root, "F1_South", new Vector2(0f, -5.0f), new Vector2(17f, 1.2f));
            AddBox(root, "F1_West", new Vector2(-8.2f, 0f), new Vector2(1.2f, 11f));
            AddBox(root, "F1_East", new Vector2(8.2f, 0f), new Vector2(1.2f, 11f));

            // 2F Room Boundaries: x[-7.5, 7.5], y[19.5, 27.0].
            AddBox(root, "F2_North", new Vector2(0f, 27.6f), new Vector2(17f, 1.4f));
            AddBox(root, "F2_South", new Vector2(0f, 18.8f), new Vector2(17f, 1.2f));
            AddBox(root, "F2_West", new Vector2(-8.2f, 23.5f), new Vector2(1.2f, 11f));
            AddBox(root, "F2_East", new Vector2(8.2f, 23.5f), new Vector2(1.2f, 11f));
        }

        static void AddBox(GameObject parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = size;
        }

        static void BindCharacterVisuals(GameObject player, GameObject eliza, GameObject clerk,
            GameObject ovan, Dictionary<string, GameObject> advNpcs)
        {
            var playerProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Player", "Assets/Art/Characters/Player/Body_A/Idle_Base/Idle_Down-Sheet.png",
                "Idle_Down-Sheet_0", "Assets/Animations/Player/Player_BodyA.controller",
                new Vector2(0f, -0.35f));
            var elizaProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Eliza", "Assets/Art/Characters/NPCs/Citizen_F/Peasant_A/Idle/Idle-Sheet.png",
                "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_PeasantA.controller",
                new Vector2(0f, -0.35f), flipX: true);
            var clerkProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Townsperson", "Assets/Art/Characters/NPCs/Citizen_F/Tavern_B/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_TavernB.controller",
                new Vector2(0f, -0.35f));
            var ovanProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Ovan", "Assets/Art/Characters/NPCs/Named/Ovan/Idle/Idle_Down-Sheet.png",
                "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Ovan.controller",
                new Vector2(0f, -0.35f));
            PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Carrier", "Assets/Art/Characters/NPCs/Named/Carrier/Idle/Idle_Down-Sheet.png",
                "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Carrier.controller",
                new Vector2(0f, -0.35f));
            var advAProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Adventurer_A", "Assets/Art/Characters/NPCs/Citizen_M/Adventurer_A/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_AdventurerA.controller",
                new Vector2(0f, -0.35f));
            var advBProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Adventurer_B", "Assets/Art/Characters/NPCs/Citizen_M/Adventurer_B/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_AdventurerB.controller",
                new Vector2(0f, -0.35f), flipX: true);
            // Refresh Ignatius's shared profile too so Guild Interior rebuilds keep it clean.
            PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Ignatius", "Assets/Art/Characters/NPCs/Named/Ignatius/Idle/Idle_Down-Sheet.png",
                "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Ignatius.controller",
                new Vector2(0f, -0.35f));

            PixelCrawlerHubToolkit.BindActorVisual(player, playerProf, addYSort: true);
            PixelCrawlerHubToolkit.BindActorVisual(eliza, elizaProf, addYSort: true);
            PixelCrawlerHubToolkit.BindActorVisual(clerk, clerkProf, addYSort: true);
            PixelCrawlerHubToolkit.BindActorVisual(ovan, ovanProf, addYSort: true);

            if (advNpcs.TryGetValue("Adventurer_Young", out var aYoung))
                PixelCrawlerHubToolkit.BindActorVisual(aYoung, advAProf, addYSort: true);
            if (advNpcs.TryGetValue("Adventurer_Scarred", out var aScar))
                PixelCrawlerHubToolkit.BindActorVisual(aScar, advBProf, addYSort: true);
            if (advNpcs.TryGetValue("Adventurer_Ranger", out var aRang))
                PixelCrawlerHubToolkit.BindActorVisual(aRang, elizaProf, addYSort: true);
        }

        static void HideInteractableQuad(GameObject go)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            var ev = go.GetComponent<EntityVisual>();
            if (ev != null) ev.Apply(null);
            if (mr != null) mr.enabled = false;
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
