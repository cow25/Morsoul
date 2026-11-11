using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Rebuilds Hub_LowerMarket and its art overlay Hub_LowerMarket_Art using
    /// Pixel Crawler tilesets, exterior buildings, market props, and animated
    /// NPC VisualProfiles while preserving every existing gameplay interactable,
    /// dialogue, and arrival choreography hook.
    /// </summary>
    public static class LowerMarketSceneBuilder
    {
        const string MainScenePath = "Assets/Scenes/Hub_LowerMarket.unity";
        const string ArtScenePath = "Assets/Scenes/Hub_LowerMarket_Art.unity";

        const string WallsTexPath = "Assets/Art/Environments/Buildings/Exterior/Walls.png";
        const string RoofsPath = "Assets/Art/Environments/Buildings/Exterior/Roofs.png";
        const string ExtPropsPath = "Assets/Art/Environments/Buildings/Exterior/Props.png";
        const string FurniturePath = "Assets/Art/Environments/Props/Static/Furniture.png";
        const string FarmPath = "Assets/Art/Environments/Props/Static/Farm.png";
        const string VegPath = "Assets/Art/Environments/Props/Static/Vegetation.png";
        const string RocksPath = "Assets/Art/Environments/Props/Static/Rocks.png";
        const string DungeonPropsPath = "Assets/Art/Environments/Props/Static/Dungeon_Props.png";
        const string IntPropsPath = "Assets/Art/Environments/Buildings/Interior/Interior_Props_01.png";
        const string TreeM1Path = "Assets/Art/Environments/Props/Trees/Model_01/Size_03.png";
        const string TreeM2Path = "Assets/Art/Environments/Props/Trees/Model_02/Size_03.png";

        [MenuItem("Morsoul/Hub/Rebuild Lower Market (Pixel Crawler)")]
        public static void RebuildMenu() => Rebuild();

        public static void Rebuild()
        {
            PixelCrawlerHubToolkit.ClearCache();
            RebuildArtOverlay();
            RebuildMainSceneBindings();
            AssetDatabase.SaveAssets();
            Debug.Log("LowerMarketSceneBuilder: rebuilt Hub_LowerMarket and Hub_LowerMarket_Art (Pixel Crawler).");
        }

        // --- 1. Art Overlay (Hub_LowerMarket_Art.unity) ----------------------

        static void RebuildArtOverlay()
        {
            var preExisting = SceneManager.GetSceneByPath(ArtScenePath);
            bool wasOpen = preExisting.IsValid() && preExisting.isLoaded;
            var prevActive = SceneManager.GetActiveScene();
            var scene = wasOpen
                ? preExisting
                : EditorSceneManager.OpenScene(ArtScenePath, OpenSceneMode.Additive);

            try
            {
                SceneManager.SetActiveScene(scene);

                // Clear legacy Quad/Dressing root.
                var oldEnv = PixelCrawlerHubToolkit.FindRoot(scene, "Environment");
                if (oldEnv != null) Object.DestroyImmediate(oldEnv);

                BuildMarketTerrain(scene);
                BuildNorthBuildings(scene);
                BuildMarketProps(scene);
                BuildTownBoundaryColliders(scene);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ArtScenePath);
            }
            finally
            {
                if (prevActive.IsValid() && prevActive.isLoaded)
                    SceneManager.SetActiveScene(prevActive);
                if (!wasOpen && scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void BuildMarketTerrain(Scene scene)
        {
            var grid = PixelCrawlerHubToolkit.EnsureGrid(scene, "EnvironmentGrid");
            var grassTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Grass");
            var dirtTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Dirt");
            var cobbleTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Cobble");
            var waterTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Water");

            var grassMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "GroundGrass", -10, 2.0f);
            var dirtMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "GroundDirt", -9, 1.99f);
            var cobbleMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "GroundCobble", -8, 1.98f);
            var waterMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "TownBrook", -7, 1.96f, addCollider: true);

            PixelCrawlerHubToolkit.FillRect(grassMap, grassTile, -18, -12, 18, 12);

            // Dirt approaches connecting West wilderness road, South gate, East Vault path.
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -16, -2, -5, 0);
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -2, -10, 1, -2);
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, 5, -2, 15, 0);

            // Cobblestone Town Plaza + building aprons.
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -7, -3, 6, 2);
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -9, -2, -6, 0);
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -8, 2, -3, 3);
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, 0, 2, 6, 3);

            // Small town brook at SE corner.
            PixelCrawlerHubToolkit.FillRect(waterMap, waterTile, 7, -9, 13, -6);

            grassMap.RefreshAllTiles();
            dirtMap.RefreshAllTiles();
            cobbleMap.RefreshAllTiles();
            waterMap.RefreshAllTiles();
        }

        static void BuildNorthBuildings(Scene scene)
        {
            var grid = PixelCrawlerHubToolkit.FindRoot(scene, "EnvironmentGrid").transform;
            var wallsMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid, "BuildingWalls", -4, 1.5f);
            var propsRoot = PixelCrawlerHubToolkit.EnsurePropsRoot(scene, "EnvironmentProps");

            // West Town House (x = -9..-3, y = 3..6) using Cream Half-Timber Tudor (Kit 3, cols 18..23).
            PaintWallFacade(wallsMap, xStart: -9, width: 7, yBottom: 3, kitColOffset: 18);

            // Grand Guild Hall (x = -1..6, y = 3..6) using Brick-Base Stucco (Kit 5, cols 30..35).
            PaintWallFacade(wallsMap, xStart: -1, width: 8, yBottom: 3, kitColOffset: 30);

            wallsMap.RefreshAllTiles();

            // Roofs: warm brown roof over West Cottage, emerald green Guild roof centered at GuildEntrance x=3.
            var roofBrown = PixelCrawlerHubToolkit.LoadSprite(RoofsPath, "Ext_Roofs_00");
            var roofGreen = PixelCrawlerHubToolkit.LoadSprite(RoofsPath, "Ext_Roofs_01");

            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Cottage_Roof",
                roofBrown, new Vector2(-5.5f, 4.9f), zBias: -0.4f, sortingOrder: 4);

            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Roof",
                roofGreen ?? roofBrown, new Vector2(3.0f, 5.15f), zBias: -0.45f, sortingOrder: 5);

            // Doors, shuttered windows, flower boxes, and Guild banners.
            var cottageDoor = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_06");
            var guildArchDoor = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_00");
            var windowShuttered = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_08");
            var windowTall = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_11");
            var flowerBox = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_12");
            var greenBanner = PixelCrawlerHubToolkit.LoadSprite(DungeonPropsPath, "Dungeon_Props_16");

            // West Cottage facade details.
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Cottage_Door", cottageDoor, new Vector2(-5.5f, 3.05f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Cottage_Win_W", windowShuttered, new Vector2(-7.3f, 3.65f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Cottage_Win_E", windowShuttered, new Vector2(-3.7f, 3.65f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Cottage_FlowerBox", flowerBox, new Vector2(-7.3f, 3.0f), zBias: 0.24f);

            // Guild Hall facade details — arched door centered at x=3.0 directly behind GuildEntrance (3.0, 2.9).
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Door", guildArchDoor, new Vector2(3.0f, 3.0f), zBias: 0.25f);
            if (greenBanner != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Banner_W", greenBanner, new Vector2(2.0f, 3.45f), zBias: 0.25f);
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Banner_E", greenBanner, new Vector2(4.0f, 3.45f), zBias: 0.25f);
            }
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Win_W", windowTall ?? windowShuttered, new Vector2(0.6f, 3.55f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Win_E", windowTall ?? windowShuttered, new Vector2(5.4f, 3.55f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Flower_W", flowerBox, new Vector2(0.6f, 3.0f), zBias: 0.24f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Flower_E", flowerBox, new Vector2(5.4f, 3.0f), zBias: 0.24f);
        }

        static void PaintWallFacade(Tilemap wallsMap, int xStart, int width, int yBottom, int kitColOffset)
        {
            // Walls.png rows 11..14 give 4 tiles height (eaves, upper wall, mid wall, foundation).
            for (int dx = 0; dx < width; dx++)
            {
                int col = dx == 0 ? kitColOffset
                    : dx == width - 1 ? kitColOffset + 5
                    : kitColOffset + 1 + ((dx - 1) % 4);

                for (int rowOffset = 0; rowOffset < 4; rowOffset++)
                {
                    int sheetRow = 14 - rowOffset;
                    var tile = PixelCrawlerHubToolkit.LoadStaticTile("Exterior_Walls", $"Walls_{sheetRow}_{col}");
                    if (tile != null)
                        wallsMap.SetTile(new Vector3Int(xStart + dx, yBottom + rowOffset, 0), tile);
                }
            }
        }

        static void BuildMarketProps(Scene scene)
        {
            var root = PixelCrawlerHubToolkit.FindRoot(scene, "EnvironmentProps").transform;

            var crateCluster = PixelCrawlerHubToolkit.LoadSprite(FurniturePath, "Furniture_13");
            var woodCounter = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_00");
            var woodTable = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_55");
            var barrelJar = PixelCrawlerHubToolkit.LoadSprite(FurniturePath, "Furniture_17");
            var bucket = PixelCrawlerHubToolkit.LoadSprite(FurniturePath, "Furniture_03");
            var lampPost = PixelCrawlerHubToolkit.LoadSprite(FurniturePath, "Furniture_52");
            var oakBench = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_99");
            var produceCrates = PixelCrawlerHubToolkit.LoadSprite(FarmPath, "Farm_26");
            var supplySack = PixelCrawlerHubToolkit.LoadSprite(FarmPath, "Farm_06");

            var oakGreen = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_00");
            var oakGold = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_01");
            var pineA = PixelCrawlerHubToolkit.LoadSprite(TreeM2Path, "Tree_Model_02_Size_03_00");
            var pineB = PixelCrawlerHubToolkit.LoadSprite(TreeM2Path, "Tree_Model_02_Size_03_01");
            var bushMed = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_04");
            var bushSmall = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_12");
            var flowerA = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_34");
            var boulder = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_05");

            // West Market Stall (south of Vendor at -4.5, -2.4 — warm dark wood counter + cabbage crates).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_West_Counter", woodCounter ?? woodTable, new Vector2(-4.5f, -3.55f), colliderSize: new Vector2(3.6f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_West_Crates", produceCrates ?? crateCluster, new Vector2(-6.7f, -3.35f), colliderSize: new Vector2(1.4f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_West_Bucket", bucket, new Vector2(-2.2f, -3.25f));

            // East Market Stall (kept south at y=-3.75, clear of MarketCrowd_Vendor at 4.5,-2.4 and RestSpot at 5,-2).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_Counter", woodTable ?? produceCrates, new Vector2(4.4f, -3.75f), colliderSize: new Vector2(2.6f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_Crates", crateCluster, new Vector2(2.2f, -3.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_Barrels", barrelJar, new Vector2(6.5f, -3.45f), colliderSize: new Vector2(1.2f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));

            // Corner Street Lanterns (avoid crowding doors, crates, or NPCs).
            foreach (var pos in new[] { new Vector2(-8.6f, 2.3f), new Vector2(7.0f, 2.3f), new Vector2(-8.1f, -3.5f), new Vector2(7.7f, -3.4f) })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Lamp_{pos.x:F0}_{pos.y:F0}", lampPost, pos);

            // Alley oak bench tucked cleanly in garden nook between West Cottage and Guild Hall.
            if (oakBench != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Plaza_Bench_Alley", oakBench, new Vector2(-1.7f, 2.35f), colliderSize: new Vector2(2.4f, 0.6f), colliderOffset: new Vector2(0f, 0.35f));

            // Trees & bushes framing the market perimeter (clear of playable center, doors & stalls).
            var trees = new (Vector2 pos, Sprite spr)[]
            {
                (new Vector2(-10.2f, 3.8f), oakGreen),
                (new Vector2(-10.0f, 0.8f), oakGold),
                (new Vector2(-9.6f, -3.8f), pineA),
                (new Vector2(8.6f, 3.8f), pineB),
                (new Vector2(8.6f, 0.8f), oakGreen),
                (new Vector2(8.8f, -3.8f), oakGold),
                // South deep backdrop trees (pushed to y <= -9.0 so tops never overlap stalls or vendors).
                (new Vector2(-6.8f, -9.0f), pineA),
                (new Vector2(-3.8f, -9.2f), oakGreen),
                (new Vector2(3.2f, -9.0f), pineB),
                (new Vector2(5.8f, -9.2f), oakGold),
            };
            for (int i = 0; i < trees.Length; i++)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Market_Tree_{i:D2}", trees[i].spr, trees[i].pos);

            // Bushes, wildflowers, and supply sacks around town borders.
            foreach (var pos in new[] { new Vector2(-9.2f, 2.2f), new Vector2(7.8f, 2.3f), new Vector2(-8.4f, -2.8f), new Vector2(7.8f, -2.7f), new Vector2(-1.4f, -4.5f), new Vector2(1.4f, -4.5f) })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Market_Bush_{pos.x:F0}_{pos.y:F0}", bushMed, pos);

            foreach (var pos in new[] { new Vector2(-8.4f, 3.1f), new Vector2(-1.7f, 3.4f), new Vector2(-0.5f, 3.0f), new Vector2(6.5f, 1.5f) })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Market_Flower_{pos.x:F0}_{pos.y:F0}", flowerA, pos);

            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Market_Sack_W", supplySack ?? bushSmall, new Vector2(-7.8f, -3.6f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Market_Boulder_SE", boulder, new Vector2(7.2f, -5.0f));
        }

        static void BuildTownBoundaryColliders(Scene scene)
        {
            var oldBounds = PixelCrawlerHubToolkit.FindRoot(scene, "MarketBounds");
            if (oldBounds != null) Object.DestroyImmediate(oldBounds);
            var root = new GameObject("MarketBounds");
            SceneManager.MoveGameObjectToScene(root, scene);

            AddBoundaryBox(root, "Bound_NorthBuildings", new Vector2(0f, 4.4f), new Vector2(22f, 2.2f));
            AddBoundaryBox(root, "Bound_West", new Vector2(-9.8f, -0.5f), new Vector2(1.2f, 12f));
            AddBoundaryBox(root, "Bound_East", new Vector2(8.8f, -0.5f), new Vector2(1.2f, 12f));
            AddBoundaryBox(root, "Bound_SouthW", new Vector2(-5.5f, -4.4f), new Vector2(7.5f, 1.2f));
            AddBoundaryBox(root, "Bound_SouthE", new Vector2(5.5f, -4.4f), new Vector2(7.5f, 1.2f));
            AddBoundaryBox(root, "Bound_SouthCap", new Vector2(0f, -6.5f), new Vector2(6f, 1.2f));
        }

        static void AddBoundaryBox(GameObject parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = size;
        }

        // --- 2. Main Scene Actor & Interactable Visuals (Hub_LowerMarket.unity)

        static void RebuildMainSceneBindings()
        {
            var preExisting = SceneManager.GetSceneByPath(MainScenePath);
            bool wasOpen = preExisting.IsValid() && preExisting.isLoaded;
            var prevActive = SceneManager.GetActiveScene();
            var scene = wasOpen
                ? preExisting
                : EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);

            try
            {
                SceneManager.SetActiveScene(scene);

                var cam = FindInScene<CameraFollow2D>(scene);
                if (cam != null)
                {
                    cam.useBounds = true;
                    cam.boundsMin = new Vector2(-9f, -4.5f);
                    cam.boundsMax = new Vector2(9f, 5.0f);
                    var c = cam.GetComponent<Camera>();
                    if (c != null)
                    {
                        c.orthographicSize = 5.2f;
                        c.backgroundColor = new Color(0.09f, 0.14f, 0.09f, 1f);
                    }
                }

                // Bind Pixel Crawler Animated Characters.
                var playerProfile = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Player", "Assets/Art/Characters/Player/Body_A/Idle_Base/Idle_Down-Sheet.png",
                    "Idle_Down-Sheet_0", "Assets/Animations/Player/Player_BodyA.controller",
                    new Vector2(0f, -0.35f));
                var elizaProfile = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Eliza", "Assets/Art/Characters/NPCs/Citizen_F/Peasant_A/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_PeasantA.controller",
                    new Vector2(0f, -0.35f), flipX: true);
                var knightProfile = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Ignatius", "Assets/Art/Characters/NPCs/Knight/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_Knight.controller",
                    new Vector2(0f, -0.35f), flipX: true);
                var wizardProfile = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Ovan", "Assets/Art/Characters/NPCs/Wizard/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_Wizard.controller",
                    new Vector2(0f, -0.35f), flipX: true);
                var tavernAProfile = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Vendor", "Assets/Art/Characters/NPCs/Citizen_F/Tavern_A/Idle/Idle_Side-Sheet.png",
                    "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_TavernA.controller",
                    new Vector2(0f, -0.35f));
                var tavernBProfile = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Townsperson", "Assets/Art/Characters/NPCs/Citizen_F/Tavern_B/Idle/Idle_Side-Sheet.png",
                    "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_TavernB.controller",
                    new Vector2(0f, -0.35f), flipX: true);
                var rogueProfile = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Carrier", "Assets/Art/Characters/NPCs/Rogue/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_Rogue.controller",
                    new Vector2(0f, -0.35f));

                BindRoot(scene, "Player", playerProfile);
                BindRoot(scene, "ArrivalEliza", elizaProfile);
                BindRoot(scene, "Ignatius", knightProfile);
                BindRoot(scene, "Ovan", wizardProfile);
                BindRoot(scene, "Vendor", tavernAProfile);
                BindRoot(scene, "MarketCrowd_Vendor", tavernBProfile);
                BindRoot(scene, "Townsperson", elizaProfile);
                BindRoot(scene, "MarketCrowd", tavernBProfile);
                BindRoot(scene, "MarketCrowd_Townsperson", elizaProfile);
                BindRoot(scene, "MarketCrowd_Adventurer", rogueProfile);
                BindRoot(scene, "Carrier", rogueProfile);

                // Bind Pixel Crawler Interactive Props.
                BindStaticProp(scene, "NoticeBoard", "NoticeBoard", IntPropsPath, "Int_Interior_Props_01_77");
                BindStaticProp(scene, "RestSpot", "RestSpot", ExtPropsPath, "Ext_Props_09");
                BindStaticProp(scene, "VaultGate", "VaultGate", IntPropsPath, "Int_Interior_Props_01_45");

                // Doorway trigger zones have drawn buildings in Hub_LowerMarket_Art — hide quad visuals.
                HideTriggerVisual(scene, "GuildEntrance");
                HideTriggerVisual(scene, "CaseDoor");

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, MainScenePath);
            }
            finally
            {
                if (prevActive.IsValid() && prevActive.isLoaded)
                    SceneManager.SetActiveScene(prevActive);
                if (!wasOpen && scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void BindRoot(Scene scene, string name, VisualProfile profile)
        {
            var go = PixelCrawlerHubToolkit.FindRoot(scene, name);
            if (go != null) PixelCrawlerHubToolkit.BindActorVisual(go, profile, addYSort: true);
        }

        static void BindStaticProp(Scene scene, string rootName, string profileName, string sheetPath, string spriteName)
        {
            var go = PixelCrawlerHubToolkit.FindRoot(scene, rootName);
            if (go == null) return;

            var profile = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                profileName, sheetPath, spriteName, null, Vector2.zero, flipX: false);
            PixelCrawlerHubToolkit.BindActorVisual(go, profile, addYSort: true);
        }

        static void HideTriggerVisual(Scene scene, string rootName)
        {
            var go = PixelCrawlerHubToolkit.FindRoot(scene, rootName);
            if (go == null) return;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            var ev = go.GetComponent<EntityVisual>();
            if (ev != null) ev.Apply(null);
            if (mr != null) mr.enabled = false;
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var c in Object.FindObjectsByType<T>())
                if (c != null && c.gameObject.scene == scene) return c;
            return null;
        }
    }
}

