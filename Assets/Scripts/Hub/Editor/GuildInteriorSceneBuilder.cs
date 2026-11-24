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
        const string VillagePropsRoot = "Assets/Art/Environments/Village_Interiors/furniture_and_props_sprites/";
        const string BulletinBoardPath = "Assets/Art/Environments/Fantasy_Tileset/Props/BulletinBoard_1.png";
        const string CustomStairsUpPath = "Assets/Art/Environments/Custom/Guild_Stairs_Up.png";
        const string CustomStairsDownPath = "Assets/Art/Environments/Custom/Guild_Stairs_Down.png";
        const string IntWallsSheet = "Interior_Walls_01";

        /// <summary>
        /// One-step orchestration: dialogue assets -> Hub_Guild scene -> Lower
        /// Market expansion -> Pixel Crawler Lower Market rebuild.
        /// </summary>
        public static void BuildAll()
        {
            Build();
            LowerMarketSceneBuilder.Rebuild();
            Debug.Log("Guild expansion built: Hub_Guild (Pixel Crawler) and Lower Market rebuilt.");
        }

        [MenuItem("Morsoul/Scenes/Guild Interior", priority = 14)]
        public static void Build()
        {
            GuildIntroContentGenerator.Generate();
            PixelCrawlerHubToolkit.ClearCache();

            var scene = HubSceneFactory.NewEmptyScene();
            RenderSettings.skybox = null;
            RenderSettings.ambientLight = Color.white;

            var player = HubSceneFactory.CreatePlayer();
            var cameraFollow = HubSceneFactory.CreateCamera(player.transform);
            cameraFollow.boundsMin = new Vector2(-23.5f, -12.5f);
            cameraFollow.boundsMax = new Vector2(26.5f, 37.0f);
            cameraFollow.useBounds = true;
            var cam = cameraFollow.GetComponent<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.04f, 0.06f, 1f);
                cam.orthographicSize = 5.4f;
            }
            HubSceneFactory.CreateSpawnPoint();
            var spawn = GameObject.FindGameObjectWithTag("PlayerSpawn");
            if (spawn != null) spawn.transform.position = new Vector3(0f, -9.2f, 0f);
            var context = HubSceneFactory.CreateHubContext();
            if (context.hud != null)
            {
                context.hud.caseText = "";
                context.hud.objectiveText = "";
                context.hud.objectiveCaseId = 0;
                context.hud.postInvestigationObjective = "";
            }
            HubSceneFactory.WirePlayer(player, cameraFollow, context);

            // Ensure custom 16-PPU oak staircases are cleanly imported with BottomCenter pivot.
            EnsureCustomSpriteImport(CustomStairsUpPath);
            EnsureCustomSpriteImport(CustomStairsDownPath);

            // Pixel Crawler floors, walls, staircases & furnishings (Tilemaps sit behind all 1F & 2F actors).
            BuildGuildTilemaps(scene);
            BuildGuildFurnishings(scene);
            BuildGuildBounds(scene);

            // Entrance Doorway centered ON the South threshold (y = -11.2f, leading out to Lower Market).
            HubSceneFactory.CreateInteractable("GuildExit",
                new Color(0.5f, 0.35f, 0.2f), new Vector3(0f, -11.2f, 0f),
                out GateInteractable exit);
            exit.promptText = "[E] Leave";
            exit.requiredFlag = "";
            exit.targetScene = "Hub_LowerMarket";
            exit.entryId = "guild_door";
            var exitProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Guild_ExitDoor", IntPropsPath, "Int_Interior_Props_01_43", null, Vector2.zero);
            PixelCrawlerHubToolkit.BindActorVisual(exit.gameObject, exitProf, addYSort: true);

            // Stair pair between 1F East Stairwell and 2F Study Landing (custom 16-PPU oak stairs).
            var landingUp = Marker("StairLanding_Up", new Vector3(8.2f, 23.8f, 0f));
            var landingDown = Marker("StairLanding_Down", new Vector3(9.2f, 3.2f, 0f));
            HubSceneFactory.CreateInteractable("StairsUp",
                new Color(0.5f, 0.4f, 0.25f), new Vector3(11.5f, 4.2f, 0f),
                out StairInteractable stairsUp);
            stairsUp.promptText = "[E] Upstairs";
            stairsUp.destination = landingUp;
            var stairsUpCol = stairsUp.GetComponent<BoxCollider2D>();
            if (stairsUpCol != null) stairsUpCol.size = new Vector2(2.8f, 2.4f);
            HideInteractableQuad(stairsUp.gameObject);

            HubSceneFactory.CreateInteractable("StairsDown",
                new Color(0.5f, 0.4f, 0.25f), new Vector3(10.5f, 24.5f, 0f),
                out StairInteractable stairsDown);
            stairsDown.promptText = "[E] Downstairs";
            stairsDown.destination = landingDown;
            var stairsDownCol = stairsDown.GetComponent<BoxCollider2D>();
            if (stairsDownCol != null) stairsDownCol.size = new Vector2(2.8f, 2.4f);
            HideInteractableQuad(stairsDown.gameObject);

            // Grand Reception Counter: GuildClerk behind left/center counter (y=4.85), front trigger covers entire desk.
            HubSceneFactory.CreateInteractable("GuildClerk",
                new Color(0.75f, 0.6f, 0.45f), new Vector3(-1.2f, 4.85f, 0f),
                out NpcInteractable clerk);
            clerk.promptText = "[E] Talk";
            clerk.dialogue = LoadDialogue(GuildDialogueFolder + "/D_GuildClerk.asset");
            var clerkCol = clerk.GetComponent<BoxCollider2D>();
            if (clerkCol != null)
            {
                clerkCol.offset = new Vector2(1.2f, -1.75f);
                clerkCol.size = new Vector2(7.6f, 2.8f);
            }

            // Second guild receptionist behind the East counter bay (enclosed behind front desk + E partition).
            var assistantClerk = HubSceneFactory.NewQuadObject("GuildClerk_Assistant",
                new Color(0.75f, 0.6f, 0.45f), new Vector3(1.6f, 4.85f, 0f));
            if (assistantClerk.GetComponent<YSort2D>() == null)
                assistantClerk.AddComponent<YSort2D>();

            // Quest Notice Board mounted prominently on the 1F North Wall (east of Reception Counter).
            HubSceneFactory.CreateInteractable("GuildBoard",
                new Color(0.6f, 0.48f, 0.3f), new Vector3(6.5f, 6.75f, 0f),
                out InspectionInteractable board);
            board.promptText = "[E] Inspect";
            board.blocksMovement = false;
            var boardCol = board.GetComponent<BoxCollider2D>();
            if (boardCol != null)
            {
                boardCol.offset = new Vector2(0f, -0.8f);
                boardCol.size = new Vector2(3.2f, 2.2f);
            }
            var boardProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Guild_Board", BulletinBoardPath, "BulletinBoard_1", null, Vector2.zero);
            PixelCrawlerHubToolkit.BindActorVisual(board.gameObject, boardProf, addYSort: true);

            // Adventurers, Tavern Patrons & Staff — positioned cleanly in their respective zones.
            var adventurerSpecs = new (string name, Vector3 pos, bool flip)[]
            {
                ("Adventurer_Young", new Vector3(-9.5f, 2.2f, 0f), false),
                ("Adventurer_Scarred", new Vector3(-7.5f, 2.0f, 0f), true),
                ("Adventurer_Ranger", new Vector3(5.2f, 4.5f, 0f), true),
                ("Adventurer_Guard", new Vector3(8.2f, 4.5f, 0f), true),
                ("Adventurer_Smith", new Vector3(-20.4f, -3.4f, 0f), false),
                ("Adventurer_MerchantW", new Vector3(-13.8f, -8.8f, 0f), true),
                ("Adventurer_MerchantE", new Vector3(-7.0f, -3.5f, 0f), true),
                ("Adventurer_PatronSW", new Vector3(-13.5f, -4.2f, 0f), true),
                ("Tavern_Barkeeper", new Vector3(-17.0f, 3.0f, 0f), false),
                ("Tavern_Patron_Bar", new Vector3(-15.8f, -0.8f, 0f), false),
                ("Tavern_Patron_Dining", new Vector3(-5.0f, -7.2f, 0f), true),
                ("Guild_Briefing_A", new Vector3(5.6f, -3.5f, 0f), false),
                ("Guild_Briefing_B", new Vector3(9.6f, -7.2f, 0f), true),
                ("Staff_Clerk", new Vector3(15.6f, 5.0f, 0f), false),
                ("Staff_Archivist", new Vector3(23.4f, 5.0f, 0f), true),
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

            // Ovan — 2F Study behind his centered mahogany desk (y=29.8), front trigger reaches visitor chairs.
            HubSceneFactory.CreateInteractable("Ovan",
                new Color(0.8f, 0.6f, 0.2f), new Vector3(0f, 29.8f, 0f),
                out NpcInteractable ovan);
            ovan.promptText = "[E] Talk";
            ovan.dialogue = LoadDialogue(OvanDialoguePath);
            var ovanCol = ovan.GetComponent<BoxCollider2D>();
            if (ovanCol != null)
            {
                ovanCol.offset = new Vector2(0f, -1.35f);
                ovanCol.size = new Vector2(4.4f, 2.8f);
            }

            // Eliza stand-in for the intro escort (activated by the controller).
            var eliza = HubSceneFactory.NewQuadObject("Eliza",
                new Color(0.9f, 0.55f, 0.35f), new Vector3(1.1f, -9.4f, 0f));
            if (eliza.GetComponent<YSort2D>() == null)
                eliza.AddComponent<YSort2D>();
            eliza.SetActive(false);

            // Bind Pixel Crawler animated character visuals to Player + NPCs.
            BindCharacterVisuals(player, eliza, clerk.gameObject, assistantClerk, ovan.gameObject, advNpcs);

            var intro = new GameObject("GuildIntroController");
            var controller = intro.AddComponent<GuildIntroController>();
            controller.context = context;
            controller.player = player.GetComponent<HubPlayerController>();
            controller.eliza = eliza.transform;
            controller.elizaEntry = new Vector3(1.1f, -9.4f, 0f);
            controller.elizaDesk = new Vector3(1.2f, 1.9f, 0f);
            controller.playerDesk = new Vector3(-0.8f, 1.9f, 0f);
            controller.elizaStairs = new Vector3(11.85f, 2.45f, 0f);
            controller.playerStairs = new Vector3(11.15f, 2.35f, 0f);
            controller.upstairsEliza = new Vector3(9.0f, 24.2f, 0f);
            controller.upstairsPlayer = new Vector3(8.2f, 23.6f, 0f);
            controller.officeEliza = new Vector3(1.0f, 26.5f, 0f);
            controller.officePlayer = new Vector3(-0.6f, 26.5f, 0f);
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

            // 1F Grand Hall & Combined Tavern/Guild Floor (48x18 = 864 tiles^2): x=-22..25, y=-11..6.
            FillRandomTileBlock(floorMap, -22, -11, 25, 6, rowMin: 37, rowMax: 39, colMin: 1, colMax: 3);
            // Symmetric central stone runner (x=-2..1 => [-2,+2]) leading from South entrance up to the Reception Desk.
            FillRandomTileBlock(accentMap, -2, -10, 1, 3, rowMin: 37, rowMax: 39, colMin: 6, colMax: 8);
            // Staff Office tan parquet (NE wing x=14..25, y=0..6) & Storage Room stone floor (x=14..18, y=-11..-2).
            FillRandomTileBlock(accentMap, 14, 0, 25, 6, rowMin: 37, rowMax: 39, colMin: 16, colMax: 18);
            FillRandomTileBlock(accentMap, 14, -11, 18, -2, rowMin: 37, rowMax: 39, colMin: 6, colMax: 8);

            // 2F Guildmaster Study Floor (26x12 = 312 tiles^2, ~2x previous area): x=-13..12, y=21..32.
            FillRandomTileBlock(floorMap, -13, 21, 12, 32, rowMin: 37, rowMax: 39, colMin: 11, colMax: 13);
            // Symmetric upper center study inset (tan parquet runner x=-4..3 => [-4,+4], y=24..30).
            FillRandomTileBlock(accentMap, -4, 24, 3, 30, rowMin: 37, rowMax: 39, colMin: 16, colMax: 18);

            // 1F North Wall (y=7..9, full 48 width): Clean continuous Cream Plaster + Oak Wainscot.
            PaintSeamlessWallBand(wallsMap, xStart: -22, width: 48, yBottom: 7, rowTop: 6, colLeft: 31, colMid: 32, colRight: 33);
            PaintRoomTopAndSideWalls(wallsMap, xMin: -22, xMax: 25, yFloorMin: -11, yWallTopRow: 10,
                colLeftCap: 31, colMid: 32, colRightCap: 34, colWest: 30, colEast: 35);

            // Interior Staff-Only Wing Partitions (x=13 separates public hall from Staff Office / Storage / Break).
            PaintConnectedStaffWall(wallsMap, x: 13, yBottom: -11, yTop: -2);
            PaintConnectedStaffWall(wallsMap, x: 13, yBottom: 1, yTop: 6);
            PaintConnectedStaffWall(wallsMap, x: 19, yBottom: -11, yTop: -2);

            // 2F North Wall (y=33..35, full 26 width): Seamless Dark Timber Planks + 2.5D perimeter frame.
            PaintSeamlessWallBand(wallsMap, xStart: -13, width: 26, yBottom: 33, rowTop: 10, colLeft: 25, colMid: 26, colRight: 27);
            PaintRoomTopAndSideWalls(wallsMap, xMin: -13, xMax: 12, yFloorMin: 21, yWallTopRow: 36,
                colLeftCap: 25, colMid: 26, colRightCap: 28, colWest: 24, colEast: 29);

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

        static void SetWallTile(Tilemap map, int x, int y, int sheetRow, int sheetCol)
        {
            var t = PixelCrawlerHubToolkit.LoadStaticTile(IntWallsSheet, $"{IntWallsSheet}_{sheetRow}_{sheetCol}");
            if (t != null)
                map.SetTile(new Vector3Int(x, y, 0), t);
        }

        static void PaintSeamlessWallBand(Tilemap wallsMap, int xStart, int width, int yBottom,
            int rowTop, int colLeft, int colMid, int colRight)
        {
            for (int dx = 0; dx < width; dx++)
            {
                int col = dx == 0 ? colLeft : (dx == width - 1 ? colRight : colMid);
                for (int dy = 0; dy < 3; dy++)
                {
                    int sheetRow = rowTop + (2 - dy);
                    SetWallTile(wallsMap, xStart + dx, yBottom + dy, sheetRow, col);
                }
            }
        }

        static void PaintRoomTopAndSideWalls(Tilemap wallsMap, int xMin, int xMax, int yFloorMin, int yWallTopRow,
            int colLeftCap, int colMid, int colRightCap, int colWest, int colEast)
        {
            // Top oak cornice ridge across North wall (y = yWallTopRow) + clean NW/NE outer step corners.
            SetWallTile(wallsMap, xMin, yWallTopRow, 0, colLeftCap);
            for (int x = xMin + 1; x < xMax; x++)
                SetWallTile(wallsMap, x, yWallTopRow, 0, colMid);
            SetWallTile(wallsMap, xMax, yWallTopRow, 0, colRightCap);

            SetWallTile(wallsMap, xMin - 1, yWallTopRow - 1, 1, colWest);
            SetWallTile(wallsMap, xMax + 1, yWallTopRow - 1, 1, colEast);

            // West & East perimeter side walls running cleanly from NW/NE corner down to South floor edge.
            for (int y = yFloorMin; y < yWallTopRow - 1; y++)
            {
                SetWallTile(wallsMap, xMin - 1, y, 2, colWest);
                SetWallTile(wallsMap, xMax + 1, y, 2, colEast);
            }
        }

        static void PaintConnectedStaffWall(Tilemap wallsMap, int x, int yBottom, int yTop)
        {
            // Pure solid oak beam pillar (col 35 rows 10..12) without cream wall stripes.
            for (int y = yBottom; y <= yTop; y++)
            {
                int sheetRow = y == yBottom ? 12 : (y == yTop ? 10 : 11);
                SetWallTile(wallsMap, x, y, sheetRow, 35);
            }
        }

        static void BuildGuildFurnishings(Scene scene)
        {
            var root = PixelCrawlerHubToolkit.EnsurePropsRoot(scene, "EnvironmentProps").transform;

            var woodDesk = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_00");
            var chairRed = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_14");
            var carpetGreen = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Guild_CarpetGreen");
            var carpetSlate = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Guild_CarpetSlate");

            var stairsUpSprite = LoadSingleSprite(CustomStairsUpPath);
            var stairsDownSprite = LoadSingleSprite(CustomStairsDownPath);

            var hearthHall = LoadVillageProp("fireplace_1.png");
            var hearthStudy = LoadVillageProp("fireplace_0.png");
            var tableOakSq = LoadVillageProp("tables_0.png");
            var tableOakWide = LoadVillageProp("tables_1.png");
            var tableRoundA = LoadVillageProp("big_round_table_0.png");
            var tableRoundB = LoadVillageProp("big_round_table_2.png");
            var tableRectStudy = LoadVillageProp("big_rect_table_3.png");
            var cabinetTall = LoadVillageProp("cabinet_20.png");
            var cabinetWideA = LoadVillageProp("cabinet_21.png");
            var cabinetWideB = LoadVillageProp("cabinet_27.png");
            var barrelGroupA = LoadVillageProp("barrels_5.png");
            var barrelGroupB = LoadVillageProp("barrels_6.png");
            var crateSupply = LoadVillageProp("crates_supply_0.png");
            var crateSmall = LoadVillageProp("crates_11.png");
            var chestStudy = LoadVillageProp("chests_0.png");
            var chestHall = LoadVillageProp("chests_1.png");
            var plantBroad = LoadVillageProp("plants_1.png");
            var plantLush = LoadVillageProp("plants_17.png");
            var bannerBlue = LoadVillageProp("banner_0.png");
            var bannerRed = LoadVillageProp("banner_1.png");
            var bannerGreen = LoadVillageProp("banner_4.png");
            var shieldCrestA = LoadVillageProp("decor_shields_0.png");
            var shieldCrestB = LoadVillageProp("decor_shields_1.png");
            var armorStand = LoadVillageProp("decors_armor_6.png");
            var weaponRack = LoadVillageProp("decors_weapon_5.png");
            var globeProp = LoadVillageProp("decors_globe.png");
            var paintingWide = LoadVillageProp("decors_paintings_7.png");
            var paintingSmall = LoadVillageProp("decors_paintings_4.png");
            var drinksShelfMain = LoadVillageProp("drinks_shelf_0.png");
            var barCounterL = LoadVillageProp("stand-2.png");
            var stoolCushion = LoadVillageProp("table_bench_0.png");
            var stoolWood = LoadVillageProp("big_round_table_chairs_0.png");
            var benchDining = LoadVillageProp("table_bench_7.png");
            var tableFeastA = LoadVillageProp("big_rect_table_0.png");
            var tableFeastB = LoadVillageProp("big_rect_table_1.png");
            var benchLong = LoadVillageProp("bench_0.png");
            var pianoProp = LoadVillageProp("piano.png");
            var wineCask = LoadVillageProp("barrels2_0.png");
            var barrelCluster = LoadVillageProp("barrels_7.png");
            var supplyBags = LoadVillageProp("supply_bags_0.png");
            var kitchenCounter = LoadVillageProp("storage_countertop_0.png");
            var storageShelfA = LoadVillageProp("shelves_0.png");
            var storageShelfB = LoadVillageProp("shelves_2.png");
            var breakCot = LoadVillageProp("bed_1.png");
            var breakStove = LoadVillageProp("stove.png");
            var mugProp = LoadVillageProp("kitchen_props_0.png");
            var bottleProp = LoadVillageProp("kitchen_props_2.png");
            var bottleBlue = LoadVillageProp("kitchen_props_5.png");
            var bowlProp = LoadVillageProp("kitchen_props2_0.png");
            var candleProp = LoadVillageProp("candlesticks_6.png");
            var papersProp = LoadVillageProp("decors_paper1.png");
            var booksProp = LoadVillageProp("decors_books_0.png");

            // --- 1F Combined Tavern & Investigator's Guild (48x18: x=-22..25, y=-11..6) ---
            if (carpetGreen != null)
            {
                var runner = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Carpet_Runner", carpetGreen,
                    new Vector2(0f, -9.5f), zBias: 0f, sortingOrder: -8);
                runner.transform.localScale = new Vector3(1.0f, 3.85f, 1f);
            }
            if (carpetSlate != null)
            {
                var loungeRug = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_TavernLounge_Rug", carpetSlate,
                    new Vector2(-8.5f, 2.2f), zBias: 0f, sortingOrder: -8);
                loungeRug.transform.localScale = new Vector3(1.3f, 1.0f, 1f);
                var diningRug = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_TavernDining_Rug", carpetSlate,
                    new Vector2(-13.5f, -7.2f), zBias: 0f, sortingOrder: -8);
                diningRug.transform.localScale = new Vector3(2.4f, 1.8f, 1f);
                var briefRug = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Briefing_Rug", carpetSlate,
                    new Vector2(7.5f, -6.5f), zBias: 0f, sortingOrder: -8);
                briefRug.transform.localScale = new Vector3(1.6f, 1.2f, 1f);
                var breakRug = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_BreakRoom_Rug", carpetSlate,
                    new Vector2(22.2f, -7.2f), zBias: 0f, sortingOrder: -8);
                breakRug.transform.localScale = new Vector3(1.1f, 1.0f, 1f);
            }

            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "F1_Bar_Glow", new Vector2(-17.5f, 4.0f), new Color(1.0f, 0.70f, 0.32f, 0.28f), scale: 5.0f);
            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "F1_Dining_Glow", new Vector2(-13.5f, -6.0f), new Color(1.0f, 0.72f, 0.35f, 0.22f), scale: 6.5f);
            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "F1_Hearth_Glow", new Vector2(-8.5f, 5.8f), new Color(1.0f, 0.65f, 0.28f, 0.34f), scale: 4.5f);
            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "F1_Hall_Glow", new Vector2(0f, 1.5f), new Color(1.0f, 0.85f, 0.55f, 0.22f), scale: 5.5f);
            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "F1_Briefing_Glow", new Vector2(7.5f, -5.0f), new Color(1.0f, 0.80f, 0.45f, 0.22f), scale: 5.0f);
            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "F1_Office_Glow", new Vector2(19.5f, 3.5f), new Color(1.0f, 0.85f, 0.55f, 0.22f), scale: 5.0f);
            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "F2_Hearth_Glow", new Vector2(-10.4f, 31.8f), new Color(1.0f, 0.65f, 0.28f, 0.30f), scale: 4.0f);
            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "F2_Desk_Glow", new Vector2(0f, 29.0f), new Color(1.0f, 0.85f, 0.55f, 0.22f), scale: 4.5f);

            // Custom 16-PPU Oak Staircase ascending North at x=10..13, y=3..6 (triggers on StairsUp).
            if (stairsUpSprite != null)
            {
                var stairsUpVis = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_StairsUp_Visual", stairsUpSprite,
                    new Vector2(11.5f, 3.0f), zBias: 0f, sortingOrder: -3);
                AddStairRailColliders(stairsUpVis);
            }

            // === SECTION A: PUBLIC TAVERN AREA (West Wing x=-22..-4) ===
            // 1) Drinking Area (NW Bar: back-bar shelves stocked with bottles, wine casks, L-bar, 4 stools)
            if (drinksShelfMain != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Tavern_BackBar", drinksShelfMain,
                    new Vector2(-17.0f, 6.4f), colliderSize: new Vector2(7.6f, 1.1f), colliderOffset: new Vector2(0f, 0.6f), sortingOrder: -2);
            if (bottleProp != null && bottleBlue != null && mugProp != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Shelf_Btl1", bottleProp, new Vector2(-19.0f, 8.2f), sortingOrder: -1);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Shelf_Btl2", bottleBlue, new Vector2(-17.8f, 8.2f), sortingOrder: -1);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Shelf_Btl3", bottleProp, new Vector2(-16.2f, 8.2f), sortingOrder: -1);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Shelf_Mug1", mugProp, new Vector2(-15.0f, 8.2f), sortingOrder: -1);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Shelf_Btl4", bottleBlue, new Vector2(-18.6f, 9.6f), sortingOrder: -1);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Shelf_Btl5", bottleProp, new Vector2(-17.0f, 9.6f), sortingOrder: -1);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Shelf_Btl6", bottleBlue, new Vector2(-15.5f, 9.6f), sortingOrder: -1);
            }
            if (wineCask != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Bar_WineCask1", wineCask,
                    new Vector2(-20.2f, 6.2f), colliderSize: new Vector2(1.4f, 1.4f), colliderOffset: new Vector2(0f, 0.8f), sortingOrder: -2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Bar_WineCask2", wineCask,
                    new Vector2(-12.6f, 6.2f), colliderSize: new Vector2(1.4f, 1.4f), colliderOffset: new Vector2(0f, 0.8f), sortingOrder: -2);
            }
            if (barCounterL != null)
            {
                var barObj = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Tavern_BarCounter", barCounterL,
                    new Vector2(-17.0f, 1.0f), colliderSize: new Vector2(7.8f, 1.3f), colliderOffset: new Vector2(0f, 0.7f));
                var leftArm = barObj.AddComponent<BoxCollider2D>();
                leftArm.offset = new Vector2(-3.2f, 2.8f);
                leftArm.size = new Vector2(1.4f, 4.8f);
            }
            if (mugProp != null && bottleProp != null && bottleBlue != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Bar_MugA", mugProp, new Vector2(-18.8f, 2.0f), sortingOrder: 2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Bar_BottleA", bottleBlue, new Vector2(-17.4f, 2.0f), sortingOrder: 2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Bar_BottleB", bottleProp, new Vector2(-16.0f, 2.0f), sortingOrder: 2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Bar_MugB", mugProp, new Vector2(-14.6f, 2.0f), sortingOrder: 2);
            }
            if (stoolCushion != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_BarStool_1", stoolCushion, new Vector2(-19.4f, 0.0f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_BarStool_2", stoolCushion, new Vector2(-17.6f, 0.0f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_BarStool_3", stoolCushion, new Vector2(-15.8f, 0.0f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_BarStool_4", stoolCushion, new Vector2(-14.0f, 0.0f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
            }
            if (barrelCluster != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Tavern_WineBarrelsNW", barrelCluster,
                    new Vector2(-19.5f, 3.8f), colliderSize: new Vector2(3.6f, 1.4f), colliderOffset: new Vector2(0f, 0.8f));

            // 2) Social Space (Center-West Lounge: Hearth, Piano, Bench, Armchairs)
            if (hearthHall != null)
            {
                var hearth = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Fireplace_Lounge", hearthHall,
                    new Vector2(-8.5f, 6.75f), colliderSize: new Vector2(3.4f, 0.9f), colliderOffset: new Vector2(0f, 0.5f), sortingOrder: -2);
                hearth.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            }
            if (pianoProp != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Tavern_Piano", pianoProp,
                    new Vector2(-5.2f, 5.6f), colliderSize: new Vector2(3.4f, 1.3f), colliderOffset: new Vector2(0f, 0.8f));
            if (benchLong != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Lounge_Bench", benchLong,
                    new Vector2(-8.5f, 3.8f), colliderSize: new Vector2(4.6f, 0.9f), colliderOffset: new Vector2(0f, 0.5f));

            // 3) Dining Area (SW Tavern Floor: 2 Feast Tables w/ Benches & Chairs + 2 Round Tables w/ Chairs + Bridge Table + Cellar Barrels)
            if (tableFeastA != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_DiningTable_NW", tableFeastA,
                    new Vector2(-17.5f, -4.2f), colliderSize: new Vector2(6.6f, 1.8f), colliderOffset: new Vector2(0f, 1.4f));
                if (benchDining != null)
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_DiningBench_NW_Bot", benchDining, new Vector2(-17.5f, -5.0f), colliderSize: new Vector2(3.4f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                if (stoolCushion != null)
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_DiningChair_NW_E", stoolCushion, new Vector2(-13.8f, -3.5f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                if (mugProp != null && bowlProp != null && candleProp != null)
                {
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_NW_Candle", candleProp, new Vector2(-17.5f, -3.1f), sortingOrder: 2);
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_NW_Mug", mugProp, new Vector2(-19.0f, -3.2f), sortingOrder: 2);
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_NW_Bowl", bowlProp, new Vector2(-16.0f, -3.3f), sortingOrder: 2);
                }
            }
            if (tableFeastB != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_DiningTable_SW", tableFeastB,
                    new Vector2(-17.5f, -8.8f), colliderSize: new Vector2(6.6f, 1.8f), colliderOffset: new Vector2(0f, 1.4f));
                if (benchDining != null)
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_DiningBench_SW_Bot", benchDining, new Vector2(-17.5f, -9.6f), colliderSize: new Vector2(3.4f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                if (stoolCushion != null)
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_DiningChair_SW_E", stoolCushion, new Vector2(-13.8f, -8.1f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                if (bottleProp != null && bowlProp != null && mugProp != null)
                {
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_SW_Bottle", bottleProp, new Vector2(-17.5f, -7.7f), sortingOrder: 2);
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_SW_Bowl", bowlProp, new Vector2(-18.8f, -7.9f), sortingOrder: 2);
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_SW_Mug", mugProp, new Vector2(-16.0f, -7.8f), sortingOrder: 2);
                }
            }
            if (tableRoundA != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_DiningRound_N", tableRoundA,
                    new Vector2(-9.8f, -3.5f), colliderSize: new Vector2(2.0f, 1.2f), colliderOffset: new Vector2(0f, 1.0f));
                if (stoolCushion != null)
                {
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_RoundN_ChairW", stoolCushion, new Vector2(-11.4f, -3.3f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_RoundN_ChairE", stoolCushion, new Vector2(-8.2f, -3.3f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_RoundN_ChairS", stoolCushion, new Vector2(-9.8f, -4.6f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                }
                if (mugProp != null) PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_RoundN_Mug", mugProp, new Vector2(-9.8f, -2.6f), sortingOrder: 2);
            }
            if (tableRoundB != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_DiningRound_S", tableRoundB,
                    new Vector2(-9.8f, -8.2f), colliderSize: new Vector2(2.0f, 1.2f), colliderOffset: new Vector2(0f, 1.0f));
                if (stoolWood != null)
                {
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_RoundS_ChairW", stoolWood, new Vector2(-11.4f, -8.0f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_RoundS_ChairE", stoolWood, new Vector2(-8.2f, -8.0f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_RoundS_ChairS", stoolWood, new Vector2(-9.8f, -9.3f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                }
                if (bottleBlue != null) PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_RoundS_Btl", bottleBlue, new Vector2(-9.8f, -7.3f), sortingOrder: 2);
            }
            if (tableOakSq != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_DiningBridge_Table", tableOakSq,
                    new Vector2(-5.0f, -6.0f), colliderSize: new Vector2(2.4f, 1.4f), colliderOffset: new Vector2(0f, 1.0f));
                if (stoolCushion != null)
                {
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Bridge_ChairW", stoolCushion, new Vector2(-6.6f, -5.8f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Bridge_ChairE", stoolCushion, new Vector2(-3.4f, -5.8f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                }
                if (candleProp != null) PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Bridge_Candle", candleProp, new Vector2(-5.0f, -5.0f), sortingOrder: 2);
            }
            if (barrelCluster != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Dining_WineClusterSW", barrelCluster,
                    new Vector2(-19.5f, -10.5f), colliderSize: new Vector2(3.6f, 1.2f), colliderOffset: new Vector2(0f, 0.7f));
            if (wineCask != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Dining_WineCaskSW", wineCask,
                    new Vector2(-15.5f, -10.5f), colliderSize: new Vector2(1.4f, 1.2f), colliderOffset: new Vector2(0f, 0.7f));

            // === SECTION B: GUILD OPERATIONS AREA (Center-North, Briefing Lounge & East Staff Wing) ===
            // 1) Front Desk / Reception (y=3.35, sealed left/right with plants & colliders)
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_ReceptionCounter_W", woodDesk,
                new Vector2(-1.9f, 3.35f), colliderSize: new Vector2(3.8f, 0.85f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_ReceptionCounter_E", woodDesk,
                new Vector2(1.9f, 3.35f), colliderSize: new Vector2(3.8f, 0.85f), colliderOffset: new Vector2(0f, 0.5f));
            if (plantBroad != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_ReceptionPlant_W", plantBroad,
                    new Vector2(-3.8f, 3.8f), colliderSize: new Vector2(1.0f, 2.8f), colliderOffset: new Vector2(0f, 1.4f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_ReceptionPlant_E", plantBroad,
                    new Vector2(3.8f, 3.8f), colliderSize: new Vector2(1.0f, 2.8f), colliderOffset: new Vector2(0f, 1.4f));
            }
            if (cabinetTall != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_ClerkCabinet_W", cabinetTall,
                    new Vector2(-2.5f, 6.85f), colliderSize: new Vector2(2.0f, 0.8f), colliderOffset: new Vector2(0f, 0.45f), sortingOrder: -2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_ClerkCabinet_E", cabinetTall,
                    new Vector2(2.5f, 6.85f), colliderSize: new Vector2(2.0f, 0.8f), colliderOffset: new Vector2(0f, 0.45f), sortingOrder: -2);
            }
            if (bannerGreen != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Banner_BoothW", bannerGreen, new Vector2(-1.1f, 7.35f), zBias: 0f, sortingOrder: -2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Banner_BoothE", bannerGreen, new Vector2(1.1f, 7.35f), zBias: 0f, sortingOrder: -2);
            }
            if (shieldCrestA != null)
            {
                var crest = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_GuildCrest", shieldCrestA,
                    new Vector2(0f, 7.8f), zBias: 0f, sortingOrder: -2);
                crest.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            }

            // 2) Quest Board Area (x=5..9, y=4..7, flanked by guild banners & armor stand)
            if (bannerBlue != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Banner_QuestW", bannerBlue, new Vector2(4.6f, 7.35f), zBias: 0f, sortingOrder: -2);
            if (bannerRed != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Banner_QuestE", bannerRed, new Vector2(8.4f, 7.35f), zBias: 0f, sortingOrder: -2);
            if (armorStand != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_ArmorStand_Quest", armorStand,
                    new Vector2(9.4f, 6.7f), colliderSize: new Vector2(0.8f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));

            // 2b) Center-East Guild Briefing & Waiting Lounge (x=5..11, y=-9..-3)
            if (tableRectStudy != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Briefing_Table", tableRectStudy,
                    new Vector2(7.5f, -6.2f), colliderSize: new Vector2(3.8f, 1.3f), colliderOffset: new Vector2(0f, 0.9f));
            if (benchDining != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Briefing_BenchS", benchDining,
                    new Vector2(7.5f, -7.8f), colliderSize: new Vector2(3.4f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
            if (stoolCushion != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Briefing_ChairW", stoolCushion, new Vector2(4.6f, -5.6f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Briefing_ChairE", stoolCushion, new Vector2(10.4f, -5.6f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
            }
            if (papersProp != null && booksProp != null && candleProp != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Briefing_Papers", papersProp, new Vector2(6.6f, -4.8f), sortingOrder: 2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Briefing_Books", booksProp, new Vector2(8.4f, -4.8f), sortingOrder: 2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Briefing_Candle", candleProp, new Vector2(7.5f, -4.7f), sortingOrder: 2);
            }
            if (weaponRack != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Briefing_WeaponRack", weaponRack,
                    new Vector2(11.6f, -3.5f), colliderSize: new Vector2(0.9f, 0.6f), colliderOffset: new Vector2(0f, 0.35f));
            if (armorStand != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Briefing_ArmorStand", armorStand,
                    new Vector2(11.6f, -8.5f), colliderSize: new Vector2(0.8f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));

            // 3) Staff-Only Wing Checkpoint Gate (full woodDesk counter across x=13.5 doorway)
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_StaffGate_Counter", woodDesk,
                new Vector2(13.5f, -0.8f), colliderSize: new Vector2(2.6f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));

            // 4) Staff Office (NE Wing: 2 horizontal woodDesks w/ chairs & paperwork + central Map/Globe table)
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Office_DeskW", woodDesk,
                new Vector2(17.0f, 3.6f), colliderSize: new Vector2(3.8f, 0.85f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Office_DeskE", woodDesk,
                new Vector2(22.0f, 3.6f), colliderSize: new Vector2(3.8f, 0.85f), colliderOffset: new Vector2(0f, 0.5f));
            if (chairRed != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Office_ChairW", chairRed, new Vector2(17.0f, 4.9f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Office_ChairE", chairRed, new Vector2(22.0f, 4.9f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Office_VisitorW", chairRed, new Vector2(17.0f, 2.5f));
            }
            if (papersProp != null && booksProp != null && candleProp != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Office_PapersW", papersProp, new Vector2(16.4f, 4.4f), sortingOrder: 2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Office_CandleW", candleProp, new Vector2(17.8f, 4.4f), sortingOrder: 2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Office_BooksE", booksProp, new Vector2(21.4f, 4.4f), sortingOrder: 2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Office_PapersE", papersProp, new Vector2(22.6f, 4.4f), sortingOrder: 2);
            }
            if (tableOakSq != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Office_MapTable", tableOakSq,
                    new Vector2(19.5f, 0.8f), colliderSize: new Vector2(2.4f, 1.4f), colliderOffset: new Vector2(0f, 1.0f));
                if (globeProp != null)
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Office_Globe", globeProp, new Vector2(19.5f, 2.1f), sortingOrder: 2);
            }
            if (cabinetWideA != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Office_CabinetW", cabinetWideA,
                    new Vector2(16.5f, 6.85f), colliderSize: new Vector2(3.1f, 0.85f), colliderOffset: new Vector2(0f, 0.45f), sortingOrder: -2);
            if (cabinetWideB != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Office_CabinetE", cabinetWideB,
                    new Vector2(21.5f, 6.85f), colliderSize: new Vector2(3.1f, 0.85f), colliderOffset: new Vector2(0f, 0.45f), sortingOrder: -2);

            // 5) Storage Room (SE-W Wing: stocked shelves, wine barrels, crates, supply bags, archive chests)
            if (storageShelfA != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Storage_ShelfN", storageShelfA,
                    new Vector2(16.2f, -1.2f), colliderSize: new Vector2(4.6f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));
            if (storageShelfB != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Storage_ShelfM", storageShelfB,
                    new Vector2(16.2f, -4.6f), colliderSize: new Vector2(4.4f, 1.2f), colliderOffset: new Vector2(0f, 0.6f));
            if (wineCask != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Storage_Wine1", wineCask,
                    new Vector2(14.8f, -7.2f), colliderSize: new Vector2(1.4f, 1.2f), colliderOffset: new Vector2(0f, 0.7f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Storage_Wine2", wineCask,
                    new Vector2(16.4f, -7.2f), colliderSize: new Vector2(1.4f, 1.2f), colliderOffset: new Vector2(0f, 0.7f));
            }
            if (supplyBags != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Storage_Bags", supplyBags,
                    new Vector2(18.0f, -7.2f), colliderSize: new Vector2(1.2f, 0.9f), colliderOffset: new Vector2(0f, 0.5f));
            if (crateSupply != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Storage_Supplies", crateSupply,
                    new Vector2(17.2f, -10.0f), colliderSize: new Vector2(2.8f, 1.1f), colliderOffset: new Vector2(0f, 0.7f));
            if (chestHall != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Storage_Chest", chestHall,
                    new Vector2(14.8f, -10.0f), colliderSize: new Vector2(1.9f, 0.85f), colliderOffset: new Vector2(0f, 0.45f));

            // 6) Break Room (SE-E Wing: kitchenette, staff dining w/ chairs, rest cots & footlocker)
            if (cabinetWideA != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Break_DividerN", cabinetWideA,
                    new Vector2(23.0f, -1.2f), colliderSize: new Vector2(4.8f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            if (breakStove != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Break_Stove", breakStove,
                    new Vector2(20.6f, -3.2f), colliderSize: new Vector2(2.4f, 1.2f), colliderOffset: new Vector2(0f, 0.8f));
            if (kitchenCounter != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Break_Counter", kitchenCounter,
                    new Vector2(22.2f, -3.2f), colliderSize: new Vector2(1.8f, 1.2f), colliderOffset: new Vector2(0f, 0.7f));
            if (tableOakSq != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Break_Table", tableOakSq,
                    new Vector2(21.4f, -7.2f), colliderSize: new Vector2(2.4f, 1.4f), colliderOffset: new Vector2(0f, 1.0f));
                if (stoolCushion != null)
                {
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Break_ChairW", stoolCushion, new Vector2(20.0f, -7.0f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Break_ChairE", stoolCushion, new Vector2(22.6f, -7.0f), colliderSize: new Vector2(0.9f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
                }
                if (mugProp != null && bowlProp != null)
                {
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Break_Mug", mugProp, new Vector2(21.0f, -6.2f), sortingOrder: 2);
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Break_Bowl", bowlProp, new Vector2(21.8f, -6.3f), sortingOrder: 2);
                }
            }
            if (breakCot != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Break_CotN", breakCot,
                    new Vector2(24.2f, -4.6f), colliderSize: new Vector2(2.6f, 3.6f), colliderOffset: new Vector2(0f, 1.8f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Break_CotS", breakCot,
                    new Vector2(24.2f, -9.8f), colliderSize: new Vector2(2.6f, 3.6f), colliderOffset: new Vector2(0f, 1.8f));
            }
            if (chestStudy != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Break_Footlocker", chestStudy,
                    new Vector2(21.4f, -10.2f), colliderSize: new Vector2(1.9f, 0.85f), colliderOffset: new Vector2(0f, 0.45f));

            if (plantBroad != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Plant_DoorW", plantBroad, new Vector2(-2.6f, -10.5f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Plant_StairsW", plantBroad, new Vector2(9.6f, 5.8f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Plant_LoungeW", plantBroad, new Vector2(-10.8f, 6.3f));
            }
            if (plantLush != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Plant_DoorE", plantLush, new Vector2(2.6f, -10.5f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Plant_OfficeE", plantLush, new Vector2(24.5f, 6.2f));
            }

            // --- 2F Ovan's Executive Study (26x12 expanded upstairs, NO bottom walls) ---
            var studyRug = carpetSlate ?? carpetGreen;
            if (studyRug != null)
            {
                var centerRug = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Carpet_Center", studyRug, new Vector2(0f, 25.4f), zBias: 0f, sortingOrder: -8);
                centerRug.transform.localScale = new Vector3(1.8f, 1.45f, 1f);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Carpet_Lounge", studyRug, new Vector2(-6.2f, 25.0f), zBias: 0f, sortingOrder: -8);
            }

            if (stairsDownSprite != null)
            {
                var stairsDownVis = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_StairsDown_Visual", stairsDownSprite,
                    new Vector2(10.5f, 23.0f), zBias: 0f, sortingOrder: -3);
                AddStairRailColliders(stairsDownVis);
            }

            // Executive mahogany desk at y=28.65 (Ovan stands at y=29.8 head-and-shoulders clear).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_OvanDesk", woodDesk,
                new Vector2(0f, 28.65f), colliderSize: new Vector2(3.8f, 0.85f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_VisitorChair_W", chairRed, new Vector2(-1.1f, 27.6f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_VisitorChair_E", chairRed, new Vector2(1.1f, 27.6f));

            // 2F North Wall Set Dressing (framed within Ovan's study camera view).
            if (hearthStudy != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Hearth_NW", hearthStudy,
                    new Vector2(-7.8f, 32.8f), colliderSize: new Vector2(3.8f, 0.9f), colliderOffset: new Vector2(0f, 0.5f), sortingOrder: -2);
            if (cabinetWideA != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Cabinet_NW", cabinetWideA,
                    new Vector2(-4.4f, 32.85f), colliderSize: new Vector2(3.1f, 0.85f), colliderOffset: new Vector2(0f, 0.45f), sortingOrder: -2);
            if (cabinetWideB != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Cabinet_NE", cabinetWideB,
                    new Vector2(4.4f, 32.85f), colliderSize: new Vector2(3.1f, 0.85f), colliderOffset: new Vector2(0f, 0.45f), sortingOrder: -2);
            if (cabinetTall != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Cabinet_FarE", cabinetTall,
                    new Vector2(7.8f, 32.85f), colliderSize: new Vector2(2.0f, 0.85f), colliderOffset: new Vector2(0f, 0.45f), sortingOrder: -2);
            if (paintingWide != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Painting_W", paintingWide, new Vector2(-2.6f, 33.8f), zBias: 0f, sortingOrder: -2);
            if (paintingSmall != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Painting_E", paintingSmall, new Vector2(2.6f, 33.8f), zBias: 0f, sortingOrder: -2);
            if (bannerBlue != null && bannerRed != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Banner_W", bannerBlue, new Vector2(-1.4f, 33.35f), zBias: 0f, sortingOrder: -2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Banner_E", bannerRed, new Vector2(1.4f, 33.35f), zBias: 0f, sortingOrder: -2);
            }
            if (shieldCrestB != null)
            {
                var crest2F = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_GuildCrest", shieldCrestB,
                    new Vector2(0f, 33.75f), zBias: 0f, sortingOrder: -2);
                crest2F.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            }

            // 2F West Briefing & Map Lounge + East Study Archive (framed around Ovan's desk).
            if (tableRectStudy != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_BriefingTable_W", tableRectStudy,
                    new Vector2(-6.2f, 26.8f), colliderSize: new Vector2(3.8f, 1.3f), colliderOffset: new Vector2(0f, 0.9f));
            if (tableRoundA != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_LoungeTable_SW", tableRoundA,
                    new Vector2(-6.2f, 22.8f), colliderSize: new Vector2(2.0f, 1.2f), colliderOffset: new Vector2(0f, 1.0f));
            if (tableRoundB != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_StudyTable_E", tableRoundB,
                    new Vector2(5.2f, 27.8f), colliderSize: new Vector2(2.0f, 1.2f), colliderOffset: new Vector2(0f, 1.0f));
            if (globeProp != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Globe_E", globeProp,
                    new Vector2(5.2f, 29.1f), sortingOrder: 2);

            if (chestStudy != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Chest_NW", chestStudy,
                    new Vector2(-10.8f, 30.2f), colliderSize: new Vector2(1.9f, 0.85f), colliderOffset: new Vector2(0f, 0.45f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Chest_SE", chestStudy,
                    new Vector2(5.2f, 22.8f), colliderSize: new Vector2(1.9f, 0.85f), colliderOffset: new Vector2(0f, 0.45f));
            }

            if (plantBroad != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Plant_NW", plantBroad, new Vector2(-11.8f, 31.8f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Plant_DeskE", plantBroad, new Vector2(2.8f, 29.0f));
            }
            if (plantLush != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Plant_NE", plantLush, new Vector2(11.8f, 31.8f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Plant_DeskW", plantLush, new Vector2(-2.8f, 29.0f));
            }
        }

        static void BuildGuildBounds(Scene scene)
        {
            var old = PixelCrawlerHubToolkit.FindRoot(scene, "GuildBounds");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("GuildBounds");
            SceneManager.MoveGameObjectToScene(root, scene);

            // 1F Room Boundaries: Floor x[-22, 26], y[-11, 7].
            AddBox(root, "F1_North", new Vector2(1.5f, 7.6f), new Vector2(50f, 1.4f));
            AddBox(root, "F1_South", new Vector2(1.5f, -11.65f), new Vector2(50f, 1.3f));
            AddBox(root, "F1_West", new Vector2(-22.6f, -2f), new Vector2(1.2f, 20f));
            AddBox(root, "F1_East", new Vector2(26.6f, -2f), new Vector2(1.2f, 20f));

            // Reception Booth Side Boundaries (seal left/right of Reception Counter so visitors stay in front).
            AddBox(root, "F1_ReceptionGate_W", new Vector2(-3.75f, 5.2f), new Vector2(0.8f, 3.8f));
            AddBox(root, "F1_ReceptionGate_E", new Vector2(3.75f, 5.2f), new Vector2(0.8f, 3.8f));

            // Staff-Only Wing Partition Colliders (x=13.5 separates Public Hall from Staff Office / Storage / Break).
            AddBox(root, "F1_StaffWall_N", new Vector2(13.5f, 3.8f), new Vector2(1.0f, 6.4f));
            AddBox(root, "F1_StaffWall_S", new Vector2(13.5f, -6.5f), new Vector2(1.0f, 10.0f));
            AddBox(root, "F1_StaffGate_Door", new Vector2(13.5f, -0.4f), new Vector2(1.0f, 2.2f));
            AddBox(root, "F1_StaffDiv_H1", new Vector2(16.2f, -0.6f), new Vector2(4.8f, 0.9f));
            AddBox(root, "F1_StaffDiv_H2", new Vector2(23.2f, -0.6f), new Vector2(5.0f, 0.9f));
            AddBox(root, "F1_StaffDiv_V", new Vector2(19.5f, -6.8f), new Vector2(0.9f, 9.5f));

            // 2F Room Boundaries: Floor x[-13, 13], y[21, 33].
            AddBox(root, "F2_North", new Vector2(0f, 33.6f), new Vector2(28f, 1.4f));
            AddBox(root, "F2_South", new Vector2(0f, 20.3f), new Vector2(28f, 1.4f));
            AddBox(root, "F2_West", new Vector2(-13.6f, 27f), new Vector2(1.2f, 14f));
            AddBox(root, "F2_East", new Vector2(13.6f, 27f), new Vector2(1.2f, 14f));
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
            GameObject assistantClerk, GameObject ovan, Dictionary<string, GameObject> advNpcs)
        {
            var playerProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Player", "Assets/Art/Characters/Player/Golem/Idle/Idle_Down-Sheet.png",
                "Idle_Down-Sheet_0", "Assets/Animations/Player/Player_Golem.controller",
                new Vector2(0f, -0.35f));
            var elizaProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Eliza", "Assets/Art/Characters/NPCs/Named/Eliza/Idle/Idle_Down-Sheet.png",
                "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Eliza.controller",
                new Vector2(0f, -0.35f));
            var peasantProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "PeasantF", "Assets/Art/Characters/NPCs/Citizen_F/Peasant_A/Idle/Idle-Sheet.png",
                "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_PeasantA.controller",
                new Vector2(0f, -0.35f), flipX: true);
            var clerkProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Townsperson", "Assets/Art/Characters/NPCs/Citizen_F/Tavern_B/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_TavernB.controller",
                new Vector2(0f, -0.35f));
            var assistantClerkProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "GuildReceptionist_East", "Assets/Art/Characters/NPCs/Citizen_F/Tavern_A/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_TavernA.controller",
                new Vector2(0f, -0.35f), flipX: true);
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
            var guardProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Guard_A", "Assets/Art/Characters/NPCs/Citizen_M/Guard_A/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_GuardA.controller",
                new Vector2(0f, -0.35f));
            var smithProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Smith_A", "Assets/Art/Characters/NPCs/Citizen_M/Smith_A/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_SmithA.controller",
                new Vector2(0f, -0.35f), flipX: true);
            var merchAProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Merchant_A", "Assets/Art/Characters/NPCs/Citizen_M/Merchant_A/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_MerchantA.controller",
                new Vector2(0f, -0.35f));
            var merchBProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Merchant_B", "Assets/Art/Characters/NPCs/Citizen_M/Merchant_B/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_MerchantB.controller",
                new Vector2(0f, -0.35f), flipX: true);
            var villagerProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Villager_A", "Assets/Art/Characters/NPCs/Citizen_M/Villager_A/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_VillagerA.controller",
                new Vector2(0f, -0.35f), flipX: true);
            // Refresh Ignatius's shared profile too so Guild Interior rebuilds keep it clean.
            PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Ignatius", "Assets/Art/Characters/NPCs/Named/Ignatius/Idle/Idle_Down-Sheet.png",
                "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Ignatius.controller",
                new Vector2(0f, -0.35f));

            PixelCrawlerHubToolkit.BindActorVisual(player, playerProf, addYSort: true);
            PixelCrawlerHubToolkit.BindActorVisual(eliza, elizaProf, addYSort: true);
            PixelCrawlerHubToolkit.BindActorVisual(clerk, clerkProf, addYSort: true);
            PixelCrawlerHubToolkit.BindActorVisual(assistantClerk, assistantClerkProf, addYSort: true);
            PixelCrawlerHubToolkit.BindActorVisual(ovan, ovanProf, addYSort: true);

            if (advNpcs.TryGetValue("Adventurer_Young", out var aYoung))
                PixelCrawlerHubToolkit.BindActorVisual(aYoung, advAProf, addYSort: true);
            if (advNpcs.TryGetValue("Adventurer_Scarred", out var aScar))
                PixelCrawlerHubToolkit.BindActorVisual(aScar, advBProf, addYSort: true);
            if (advNpcs.TryGetValue("Adventurer_Ranger", out var aRang))
                PixelCrawlerHubToolkit.BindActorVisual(aRang, peasantProf, addYSort: true);
            if (advNpcs.TryGetValue("Adventurer_Guard", out var aGuard))
                PixelCrawlerHubToolkit.BindActorVisual(aGuard, guardProf, addYSort: true);
            if (advNpcs.TryGetValue("Adventurer_Smith", out var aSmith))
                PixelCrawlerHubToolkit.BindActorVisual(aSmith, smithProf, addYSort: true);
            if (advNpcs.TryGetValue("Adventurer_MerchantW", out var aMerchW))
                PixelCrawlerHubToolkit.BindActorVisual(aMerchW, merchAProf, addYSort: true);
            if (advNpcs.TryGetValue("Adventurer_MerchantE", out var aMerchE))
                PixelCrawlerHubToolkit.BindActorVisual(aMerchE, merchBProf, addYSort: true);
            if (advNpcs.TryGetValue("Adventurer_PatronSW", out var aPatron))
                PixelCrawlerHubToolkit.BindActorVisual(aPatron, villagerProf, addYSort: true);
            if (advNpcs.TryGetValue("Tavern_Barkeeper", out var aBarkeeper))
                PixelCrawlerHubToolkit.BindActorVisual(aBarkeeper, assistantClerkProf, addYSort: true);
            if (advNpcs.TryGetValue("Tavern_Patron_Bar", out var aBarPatron))
                PixelCrawlerHubToolkit.BindActorVisual(aBarPatron, advAProf, addYSort: true);
            if (advNpcs.TryGetValue("Tavern_Patron_Dining", out var aDinPatron))
                PixelCrawlerHubToolkit.BindActorVisual(aDinPatron, peasantProf, addYSort: true);
            if (advNpcs.TryGetValue("Guild_Briefing_A", out var aBriefA))
                PixelCrawlerHubToolkit.BindActorVisual(aBriefA, guardProf, addYSort: true);
            if (advNpcs.TryGetValue("Guild_Briefing_B", out var aBriefB))
                PixelCrawlerHubToolkit.BindActorVisual(aBriefB, merchBProf, addYSort: true);
            if (advNpcs.TryGetValue("Staff_Clerk", out var aStaffClerk))
                PixelCrawlerHubToolkit.BindActorVisual(aStaffClerk, clerkProf, addYSort: true);
            if (advNpcs.TryGetValue("Staff_Archivist", out var aStaffArch))
                PixelCrawlerHubToolkit.BindActorVisual(aStaffArch, villagerProf, addYSort: true);
        }

        static Sprite LoadVillageProp(string fileName) => LoadSingleSprite(VillagePropsRoot + fileName);

        static Sprite LoadSingleSprite(string assetPath)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (s == null)
                Debug.LogWarning($"GuildInteriorSceneBuilder: Sprite missing at '{assetPath}'.");
            return s;
        }

        static void EnsureCustomSpriteImport(string assetPath)
        {
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) return;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            bool dirty = false;
            if (ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                dirty = true;
            }
            if (ti.spriteImportMode != SpriteImportMode.Single)
            {
                ti.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }
            if (Mathf.Abs(ti.spritePixelsPerUnit - 16f) > 0.01f)
            {
                ti.spritePixelsPerUnit = 16f;
                dirty = true;
            }
            if (ti.filterMode != FilterMode.Point)
            {
                ti.filterMode = FilterMode.Point;
                dirty = true;
            }
            if (ti.textureCompression != TextureImporterCompression.Uncompressed)
            {
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }
            if (settings.spriteAlignment != (int)SpriteAlignment.BottomCenter ||
                (settings.spritePivot - new Vector2(0.5f, 0f)).sqrMagnitude > 0.0001f)
            {
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.spritePivot = new Vector2(0.5f, 0f);
                ti.SetTextureSettings(settings);
                dirty = true;
            }
            if (dirty) ti.SaveAndReimport();
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

        static void AddStairRailColliders(GameObject visual)
        {
            if (visual == null) return;
            foreach (var (offset, size) in new[]
            {
                (new Vector2(-1.35f, 1.5f), new Vector2(0.3f, 3.0f)),
                (new Vector2(1.35f, 1.5f), new Vector2(0.3f, 3.0f)),
                (new Vector2(0f, 2.85f), new Vector2(3.0f, 0.3f)),
            })
            {
                var col = visual.AddComponent<BoxCollider2D>();
                col.offset = offset;
                col.size = size;
            }
        }
    }
}
