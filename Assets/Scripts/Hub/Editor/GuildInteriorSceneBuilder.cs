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
            RenderSettings.skybox = null;
            RenderSettings.ambientLight = Color.white;

            var player = HubSceneFactory.CreatePlayer();
            var cameraFollow = HubSceneFactory.CreateCamera(player.transform);
            cameraFollow.boundsMin = new Vector2(-15.5f, -8.5f);
            cameraFollow.boundsMax = new Vector2(15.5f, 37.0f);
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
            if (spawn != null) spawn.transform.position = new Vector3(0f, -5.2f, 0f);
            var context = HubSceneFactory.CreateHubContext();
            if (context.hud != null)
            {
                context.hud.caseText = "";
                context.hud.objectiveText = "Follow Eliza to meet Guildmaster Ovan.";
            }
            HubSceneFactory.WirePlayer(player, cameraFollow, context);

            // Ensure custom 16-PPU oak staircases are cleanly imported with BottomCenter pivot.
            EnsureCustomSpriteImport(CustomStairsUpPath);
            EnsureCustomSpriteImport(CustomStairsDownPath);

            // Pixel Crawler floors, walls, staircases & furnishings (Tilemaps sit behind all 1F & 2F actors).
            BuildGuildTilemaps(scene);
            BuildGuildFurnishings(scene);
            BuildGuildBounds(scene);

            // Entrance Doorway centered ON the South threshold (y = -7.2f, leading out to Lower Market).
            HubSceneFactory.CreateInteractable("GuildExit",
                new Color(0.5f, 0.35f, 0.2f), new Vector3(0f, -7.2f, 0f),
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

            // Grand Reception Counter: GuildClerk behind left/center counter (y=4.45), front trigger covers entire desk.
            HubSceneFactory.CreateInteractable("GuildClerk",
                new Color(0.75f, 0.6f, 0.45f), new Vector3(-1.2f, 4.45f, 0f),
                out NpcInteractable clerk);
            clerk.promptText = "[E] Talk";
            clerk.dialogue = LoadDialogue(GuildDialogueFolder + "/D_GuildClerk.asset");
            var clerkCol = clerk.GetComponent<BoxCollider2D>();
            if (clerkCol != null)
            {
                clerkCol.offset = new Vector2(1.2f, -1.35f);
                clerkCol.size = new Vector2(7.6f, 2.6f);
            }

            // Second guild receptionist behind the East counter bay (enclosed behind front desk + E partition).
            var assistantClerk = HubSceneFactory.NewQuadObject("GuildClerk_Assistant",
                new Color(0.75f, 0.6f, 0.45f), new Vector3(1.6f, 4.45f, 0f));
            if (assistantClerk.GetComponent<YSort2D>() == null)
                assistantClerk.AddComponent<YSort2D>();

            // Quest Notice Board mounted against the new Left Front-Desk Partition Wall (x=-9..-5, y=4..7).
            HubSceneFactory.CreateInteractable("GuildBoard",
                new Color(0.6f, 0.48f, 0.3f), new Vector3(-7.0f, 3.88f, 0f),
                out InspectionInteractable board);
            board.promptText = "[E] Inspect";
            board.blocksMovement = false;
            var boardCol = board.GetComponent<BoxCollider2D>();
            if (boardCol != null)
            {
                boardCol.offset = new Vector2(0f, -0.25f);
                boardCol.size = new Vector2(3.0f, 1.8f);
            }
            var boardProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Guild_Board", BulletinBoardPath, "BulletinBoard_1", null, Vector2.zero);
            PixelCrawlerHubToolkit.BindActorVisual(board.gameObject, boardProf, addYSort: true);

            // 8 Adventurers & Guild Patrons — positioned in open floor lanes & tables (clear of clerk booth & Eliza path).
            var adventurerSpecs = new (string name, Vector3 pos, bool flip)[]
            {
                ("Adventurer_Young", new Vector3(-7.4f, 2.5f, 0f), false),
                ("Adventurer_Scarred", new Vector3(-5.9f, 2.3f, 0f), true),
                ("Adventurer_Ranger", new Vector3(4.8f, 1.5f, 0f), true),
                ("Adventurer_Guard", new Vector3(-12.2f, 4.7f, 0f), false),
                ("Adventurer_Smith", new Vector3(-7.5f, -1.8f, 0f), true),
                ("Adventurer_MerchantW", new Vector3(5.2f, -1.2f, 0f), false),
                ("Adventurer_MerchantE", new Vector3(11.6f, -1.3f, 0f), true),
                ("Adventurer_PatronSW", new Vector3(-7.2f, -4.8f, 0f), true),
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
                new Color(0.9f, 0.55f, 0.35f), new Vector3(1.1f, -5.4f, 0f));
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
            controller.elizaEntry = new Vector3(1.1f, -5.4f, 0f);
            controller.elizaDesk = new Vector3(1.2f, 1.9f, 0f);
            controller.playerDesk = new Vector3(-0.8f, 1.9f, 0f);
            controller.elizaStairs = new Vector3(11.2f, 3.6f, 0f);
            controller.playerStairs = new Vector3(10.0f, 3.2f, 0f);
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

            // 1F Grand Hall Floor (28x14 = 392 tiles^2, >2.4x previous area): x=-14..13, y=-7..6.
            FillRandomTileBlock(floorMap, -14, -7, 13, 6, rowMin: 37, rowMax: 39, colMin: 1, colMax: 3);
            // Symmetric central stone runner (x=-2..1 => [-2,+2]) leading from South entrance up to the Reception Desk.
            FillRandomTileBlock(accentMap, -2, -6, 1, 3, rowMin: 37, rowMax: 39, colMin: 6, colMax: 8);

            // 2F Guildmaster Study Floor (26x12 = 312 tiles^2, ~2x previous area): x=-13..12, y=21..32.
            FillRandomTileBlock(floorMap, -13, 21, 12, 32, rowMin: 37, rowMax: 39, colMin: 11, colMax: 13);
            // Symmetric upper center study inset (tan parquet runner x=-4..3 => [-4,+4], y=24..30).
            FillRandomTileBlock(accentMap, -4, 24, 3, 30, rowMin: 37, rowMax: 39, colMin: 16, colMax: 18);

            // 1F North Wall (y=7..9, full 28 width): Seamless Cream Plaster + Oak Wainscot (no 3-tile vertical seams).
            PaintSeamlessWallBand(wallsMap, xStart: -14, width: 28, yBottom: 7, rowTop: 6, colLeft: 31, colMid: 32, colRight: 33);
            PaintRoomTopAndSideWalls(wallsMap, xMin: -14, xMax: 13, yFloorMin: -7, yWallTopRow: 10,
                colLeftCap: 31, colMid: 32, colRightCap: 34, colWest: 30, colEast: 35);

            // Stepped-forward 2.5D Left Quest Wall Projection (x=-9..-5, face y=4..6, wall-top cap y=7..9).
            PaintSeamlessWallBand(wallsMap, xStart: -9, width: 5, yBottom: 4, rowTop: 6, colLeft: 31, colMid: 32, colRight: 33);
            for (int x = -9; x <= -5; x++)
            {
                int rimCol = x == -9 ? 31 : (x == -5 ? 34 : 32);
                SetWallTile(wallsMap, x, 7, 5, rimCol);
                int topCol = x == -9 ? 30 : (x == -5 ? 35 : 32);
                for (int y = 8; y <= 9; y++)
                    SetWallTile(wallsMap, x, y, 2, topCol);
                SetWallTile(wallsMap, x, 10, 0, 32);
            }

            // Right Front-Desk Vertical Partition Wall (x=+4, y=3..6) meeting North wall baseboard cleanly.
            PaintConnectedStaffWall(wallsMap, x: 4, yBottom: 3, yTop: 6);

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

            // --- 1F Ground Hall (28x14 expanded guildhall, NO bottom walls) ---
            if (carpetGreen != null)
            {
                var runner = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Carpet_Runner", carpetGreen,
                    new Vector2(0f, -5.9f), zBias: 0f, sortingOrder: -8);
                runner.transform.localScale = new Vector3(1.0f, 2.95f, 1f);
            }

            // Custom 16-PPU Oak Staircase ascending East at x=10..13, y=3..6 (triggers on StairsUp).
            if (stairsUpSprite != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_StairsUp_Visual", stairsUpSprite,
                    new Vector2(11.5f, 3.0f), zBias: 0f, sortingOrder: -3);

            // Twin Grand Reception Counter at y=3.35 (staff booth x[-3.8,+3.8], y[3.35,7.0] sealed L/R).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_ReceptionCounter_W", woodDesk,
                new Vector2(-1.9f, 3.35f), colliderSize: new Vector2(3.8f, 0.85f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_ReceptionCounter_E", woodDesk,
                new Vector2(1.9f, 3.35f), colliderSize: new Vector2(3.8f, 0.85f), colliderOffset: new Vector2(0f, 0.5f));

            // NW Hearth Corner (x=-14..-10) + Weapon Rack left of the new stepped Quest Wall.
            if (hearthHall != null)
            {
                var hearth = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Fireplace_NW", hearthHall,
                    new Vector2(-12.0f, 6.75f), colliderSize: new Vector2(3.4f, 0.9f), colliderOffset: new Vector2(0f, 0.5f), sortingOrder: -2);
                hearth.transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            }
            if (weaponRack != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_WeaponRack_NW", weaponRack,
                    new Vector2(-9.6f, 6.85f), colliderSize: new Vector2(0.9f, 0.6f), colliderOffset: new Vector2(0f, 0.35f), sortingOrder: -2);

            // Reception Booth Backdrop (y=6.85..8.0 behind sealed clerks).
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
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Banner_NE", bannerGreen, new Vector2(10.0f, 7.35f), zBias: 0f, sortingOrder: -2);
            }
            if (shieldCrestA != null)
            {
                var crest = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_GuildCrest", shieldCrestA,
                    new Vector2(0f, 7.8f), zBias: 0f, sortingOrder: -2);
                crest.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            }

            // NE Archive Wall right of the vertical Staff Partition (x=5..14).
            if (cabinetWideB != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Bookshelf_NE", cabinetWideB,
                    new Vector2(7.0f, 6.85f), colliderSize: new Vector2(3.1f, 0.85f), colliderOffset: new Vector2(0f, 0.45f), sortingOrder: -2);
            if (cabinetTall != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Cabinet_NE", cabinetTall,
                    new Vector2(12.2f, 6.85f), colliderSize: new Vector2(2.0f, 0.85f), colliderOffset: new Vector2(0f, 0.45f), sortingOrder: -2);
            if (armorStand != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_ArmorStand_Staff", armorStand,
                    new Vector2(5.3f, 6.8f), colliderSize: new Vector2(0.8f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));

            // West Tavern Seating Wing (clean Village_Interiors tables, clear of Quest Wall y=4..7 & NPCs).
            if (tableOakSq != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_TavernTable_W", tableOakSq,
                    new Vector2(-10.2f, -0.5f), colliderSize: new Vector2(2.5f, 1.5f), colliderOffset: new Vector2(0f, 1.4f));
            if (tableRoundA != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_RoundTable_SW", tableRoundA,
                    new Vector2(-9.5f, -5.2f), colliderSize: new Vector2(2.0f, 1.2f), colliderOffset: new Vector2(0f, 1.0f));

            // East Tavern Seating Wing (keeps Eliza's walk lane y[1.8..3.4] & East Stairs completely clear).
            if (tableOakWide != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_FeastTable_E", tableOakWide,
                    new Vector2(8.5f, -2.5f), colliderSize: new Vector2(3.6f, 1.5f), colliderOffset: new Vector2(0f, 1.4f));
            if (tableRoundB != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_RoundTable_SE", tableRoundB,
                    new Vector2(5.2f, -5.8f), colliderSize: new Vector2(2.0f, 1.2f), colliderOffset: new Vector2(0f, 1.0f));

            // Corner supplies, chests & greenery (100% Village_Interiors, replacing uncovered bottom props).
            if (barrelGroupA != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Barrels_SW", barrelGroupA,
                    new Vector2(-12.5f, -6.2f), colliderSize: new Vector2(2.0f, 1.2f), colliderOffset: new Vector2(0f, 0.7f));
            if (crateSmall != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Crate_SW", crateSmall,
                    new Vector2(-11.0f, -6.3f), colliderSize: new Vector2(0.95f, 0.7f), colliderOffset: new Vector2(0f, 0.4f));
            if (barrelGroupB != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Barrels_SE", barrelGroupB,
                    new Vector2(12.4f, -4.8f), colliderSize: new Vector2(2.0f, 1.2f), colliderOffset: new Vector2(0f, 0.7f));
            if (chestHall != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Chest_SE", chestHall,
                    new Vector2(11.6f, -6.3f), colliderSize: new Vector2(1.9f, 0.85f), colliderOffset: new Vector2(0f, 0.45f));
            if (crateSupply != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Supplies_NW", crateSupply,
                    new Vector2(-12.4f, 1.5f), colliderSize: new Vector2(2.8f, 1.1f), colliderOffset: new Vector2(0f, 0.7f));

            if (plantBroad != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Plant_DoorW", plantBroad, new Vector2(-2.6f, -6.5f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Plant_QuestW", plantBroad, new Vector2(-9.5f, 3.75f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Plant_StairsW", plantBroad, new Vector2(9.3f, 5.8f));
            }
            if (plantLush != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Plant_DoorE", plantLush, new Vector2(2.6f, -6.5f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F1_Plant_StairsE", plantLush, new Vector2(13.3f, 6.0f));
            }

            // --- 2F Ovan's Executive Study (26x12 expanded upstairs, NO bottom walls) ---
            if (carpetSlate != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Carpet_Center", carpetSlate, new Vector2(0f, 26.8f), zBias: 0f, sortingOrder: -8);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Carpet_Lounge", carpetSlate, new Vector2(-8.0f, 25.4f), zBias: 0f, sortingOrder: -8);
            }

            if (stairsDownSprite != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_StairsDown_Visual", stairsDownSprite,
                    new Vector2(10.5f, 23.0f), zBias: 0f, sortingOrder: -3);

            // Executive mahogany desk at y=28.65 (Ovan stands at y=29.8 head-and-shoulders clear).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_OvanDesk", woodDesk,
                new Vector2(0f, 28.65f), colliderSize: new Vector2(3.8f, 0.85f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_VisitorChair_W", chairRed, new Vector2(-1.1f, 27.6f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_VisitorChair_E", chairRed, new Vector2(1.1f, 27.6f));

            // 2F North Wall Set Dressing (Village_Interiors cohesive wood & crimson furnishings).
            if (hearthStudy != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Hearth_NW", hearthStudy,
                    new Vector2(-10.4f, 32.8f), colliderSize: new Vector2(3.8f, 0.9f), colliderOffset: new Vector2(0f, 0.5f), sortingOrder: -2);
            if (cabinetWideA != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Cabinet_NW", cabinetWideA,
                    new Vector2(-6.5f, 32.85f), colliderSize: new Vector2(3.1f, 0.85f), colliderOffset: new Vector2(0f, 0.45f), sortingOrder: -2);
            if (cabinetWideB != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Cabinet_NE", cabinetWideB,
                    new Vector2(6.8f, 32.85f), colliderSize: new Vector2(3.1f, 0.85f), colliderOffset: new Vector2(0f, 0.45f), sortingOrder: -2);
            if (cabinetTall != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Cabinet_FarE", cabinetTall,
                    new Vector2(10.6f, 32.85f), colliderSize: new Vector2(2.0f, 0.85f), colliderOffset: new Vector2(0f, 0.45f), sortingOrder: -2);
            if (paintingWide != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Painting_W", paintingWide, new Vector2(-4.0f, 33.8f), zBias: 0f, sortingOrder: -2);
            if (paintingSmall != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Painting_E", paintingSmall, new Vector2(4.0f, 33.8f), zBias: 0f, sortingOrder: -2);
            if (bannerBlue != null && bannerRed != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Banner_W", bannerBlue, new Vector2(-1.8f, 33.35f), zBias: 0f, sortingOrder: -2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Banner_E", bannerRed, new Vector2(1.8f, 33.35f), zBias: 0f, sortingOrder: -2);
            }
            if (shieldCrestB != null)
            {
                var crest2F = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_GuildCrest", shieldCrestB,
                    new Vector2(0f, 33.75f), zBias: 0f, sortingOrder: -2);
                crest2F.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            }

            // 2F West Briefing & Map Lounge + East Study Archive (well inside y[21..32] floor).
            if (tableRectStudy != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_BriefingTable_W", tableRectStudy,
                    new Vector2(-8.0f, 25.8f), colliderSize: new Vector2(3.8f, 1.3f), colliderOffset: new Vector2(0f, 0.9f));
            if (tableRoundA != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_LoungeTable_SW", tableRoundA,
                    new Vector2(-8.0f, 22.0f), colliderSize: new Vector2(2.0f, 1.2f), colliderOffset: new Vector2(0f, 1.0f));
            if (tableRoundB != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_StudyTable_E", tableRoundB,
                    new Vector2(6.5f, 28.4f), colliderSize: new Vector2(2.0f, 1.2f), colliderOffset: new Vector2(0f, 1.0f));
            if (globeProp != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Globe_E", globeProp,
                    new Vector2(6.5f, 29.7f), sortingOrder: 2);

            if (chestStudy != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Chest_NW", chestStudy,
                    new Vector2(-11.4f, 30.2f), colliderSize: new Vector2(1.9f, 0.85f), colliderOffset: new Vector2(0f, 0.45f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Chest_SE", chestStudy,
                    new Vector2(5.6f, 22.0f), colliderSize: new Vector2(1.9f, 0.85f), colliderOffset: new Vector2(0f, 0.45f));
            }

            if (plantBroad != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Plant_NW", plantBroad, new Vector2(-12.2f, 31.8f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Plant_DeskE", plantBroad, new Vector2(3.4f, 31.2f));
            }
            if (plantLush != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Plant_NE", plantLush, new Vector2(12.0f, 31.8f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "F2_Plant_DeskW", plantLush, new Vector2(-3.4f, 31.2f));
            }
        }

        static void BuildGuildBounds(Scene scene)
        {
            var old = PixelCrawlerHubToolkit.FindRoot(scene, "GuildBounds");
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("GuildBounds");
            SceneManager.MoveGameObjectToScene(root, scene);

            // 1F Room Boundaries: Floor x[-14, 14], y[-7, 7].
            AddBox(root, "F1_North", new Vector2(0f, 7.6f), new Vector2(30f, 1.4f));
            AddBox(root, "F1_South", new Vector2(0f, -7.65f), new Vector2(30f, 1.3f));
            AddBox(root, "F1_West", new Vector2(-14.6f, 0f), new Vector2(1.2f, 16f));
            AddBox(root, "F1_East", new Vector2(14.6f, 0f), new Vector2(1.2f, 16f));

            // Seal the clerk area behind the front desks: stepped Left Quest Wall (x[-9.1,-3.7], y[4,7])
            // and East vertical staff partition (x[3.75,5.05], y[3.3,7.0]) overlap F1_ReceptionCounter cleanly.
            AddBox(root, "F1_QuestWall", new Vector2(-6.4f, 5.5f), new Vector2(5.4f, 3.0f));
            AddBox(root, "F1_StaffWall_E", new Vector2(4.4f, 5.15f), new Vector2(1.3f, 3.7f));

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
    }
}
